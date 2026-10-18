using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// «Физика» воды для шейдера WildSnake/Water: кольца ряби от змей и плывущей добычи (след на ходу, всплеск при входе),
    /// пузыри тёплых ключей и капли дождя. Источники — в кольцевом буфере из 16 колец, который каждый кадр уходит
    /// в глобальный массив шейдера; сама рябь, пена на гребнях и затухание считаются на GPU.
    /// Всплески — брызги частиц через Emit. Аллокаций в кадре нет: все массивы созданы в Awake.
    /// </summary>
    public class WaterRipples : MonoBehaviour
    {
        private const int MAX_RIPPLES = 16;
        private static readonly int RipplesId = Shader.PropertyToID("_WaterRipples");
        private static readonly int CountId = Shader.PropertyToID("_WaterRippleCount");
        private static readonly int TimeId = Shader.PropertyToID("_WaterTime");

        [Header("Ссылки")]
        [Tooltip("Сетка поля: где вода и где тёплые ключи")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Змеи, оставляющие след на воде")]
        [SerializeField] private SnakeController[] _snakes = new SnakeController[0];

        [Tooltip("Живность: плывущие животные тоже рябят воду")]
        [SerializeField] private WildlifeManager _wildlife;

        [Tooltip("Погода: в дождь по воде бегут круги от капель")]
        [SerializeField] private WeatherSystem _weather;

        [Tooltip("Камера: круги от дождя — только вокруг неё")]
        [SerializeField] private Transform _camera;

        [Tooltip("Брызги всплеска (Emit вручную)")]
        [SerializeField] private ParticleSystem _splash;

        [Header("Рябь")]
        [Tooltip("Шаг следа на воде, м: новое кольцо — каждые столько метров пути")]
        [SerializeField, Min(0.1f)] private float _trailStep = 0.5f;

        [Tooltip("Кругов от капель в секунду в сильный дождь")]
        [SerializeField, Min(0f)] private float _rainRate = 9f;

        [Tooltip("Кругов в секунду в тихую погоду (рыба клюёт мошку, водомерки) — вода не стоит мёртвым стеклом")]
        [SerializeField, Min(0f)] private float _calmRate = 1.2f;

        [Tooltip("Радиус дождевых кругов вокруг камеры, м")]
        [SerializeField, Min(1f)] private float _rainRadius = 12f;

        [Tooltip("Пауза между пузырями тёплых ключей, с")]
        [SerializeField, Min(0.05f)] private float _springInterval = 0.45f;

        [Tooltip("Брызг на всплеск")]
        [SerializeField, Range(0, 40)] private int _splashParticles = 14;

        private readonly Vector4[] _ripples = new Vector4[MAX_RIPPLES];
        private int _next;
        private int _count;
        private Vector3[] _snakeLast;
        private bool[] _snakeWet;
        private Vector3[] _preyLast;
        private bool[] _preyWet;
        private float _rainDebt;
        private float _calmDebt;
        private float _springTimer;

        private void Awake()
        {
            _snakeLast = new Vector3[_snakes.Length];
            _snakeWet = new bool[_snakes.Length];
        }

        private void Start()
        {
            int animals = _wildlife != null ? _wildlife.AnimalCount : 0;
            _preyLast = new Vector3[animals];
            _preyWet = new bool[animals];
            // Массив шейдера фиксирует длину при первой установке — сразу отдаём все 16.
            Shader.SetGlobalVectorArray(RipplesId, _ripples);
            Shader.SetGlobalFloat(CountId, 0f);
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(CountId, 0f);
        }

        /// <summary>Кольцо ряби в точке воды; strength ~0.3 — капля, 1 — всплеск.</summary>
        public void AddRipple(Vector3 position, float strength)
        {
            _ripples[_next] = new Vector4(position.x, position.z, Time.timeSinceLevelLoad, strength);
            _next = (_next + 1) % MAX_RIPPLES;
            if (_count < MAX_RIPPLES) _count++;
        }

        /// <summary>Всплеск: крупное кольцо и брызги.</summary>
        public void Splash(Vector3 position, float strength)
        {
            AddRipple(position, strength);
            if (_splash == null || _splashParticles <= 0) return;
            var p = new ParticleSystem.EmitParams { position = position + Vector3.up * 0.05f, applyShapeToPosition = true };
            _splash.Emit(p, Mathf.RoundToInt(_splashParticles * Mathf.Clamp(strength, 0.3f, 1.5f)));
        }

        private void Update()
        {
            if (_grid == null) return;
            float dt = Time.deltaTime;

            for (int i = 0; i < _snakes.Length; i++)
            {
                SnakeController s = _snakes[i];
                bool wet = s != null && s.gameObject.activeInHierarchy && s.IsAlive && s.IsInWater;
                if (wet) Track(s.HeadPosition, s.Speed > 0.05f, ref _snakeLast[i], ref _snakeWet[i], 1.1f, 0.55f);
                else _snakeWet[i] = false;
            }

            if (_wildlife != null)
            {
                int n = Mathf.Min(_preyLast.Length, _wildlife.AnimalCount);
                for (int i = 0; i < n; i++)
                {
                    PreyAnimal a = _wildlife.GetAnimal(i);
                    bool wet = a != null && a.IsActive && a.IsSwimming;
                    if (wet) Track(a.Position, true, ref _preyLast[i], ref _preyWet[i], 0.8f, 0.35f);
                    else _preyWet[i] = false;
                }
            }

            _springTimer -= dt;
            if (_springTimer <= 0f && _grid.HotSpringCount > 0)
            {
                _springTimer = _springInterval * Random.Range(0.6f, 1.4f);
                Vector2 o = Random.insideUnitCircle * 0.55f * _grid.CellSize;
                AddRipple(_grid.GetHotSpring(Random.Range(0, _grid.HotSpringCount)) + new Vector3(o.x, 0f, o.y), 0.3f);
            }

            float rain = _weather != null ? _weather.Rain01 : 0f;
            if (rain > 0.05f && _camera != null)
            {
                _rainDebt += _rainRate * rain * dt;
                for (int attempt = 0; _rainDebt >= 1f && attempt < 6; attempt++)
                {
                    _rainDebt -= 1f;
                    Vector2 o = Random.insideUnitCircle * _rainRadius;
                    Vector3 p = _camera.position + new Vector3(o.x, 0f, o.y);
                    if (_grid.IsWaterAt(p)) AddRipple(p, 0.22f);
                }
                _rainDebt = Mathf.Min(_rainDebt, 2f);
            }
            else if (_camera != null && _calmRate > 0f)
            {
                _calmDebt += _calmRate * dt;
                if (_calmDebt < 1f) return;
                _calmDebt = 0f;
                Vector2 o = Random.insideUnitCircle * _rainRadius;
                Vector3 p = _camera.position + new Vector3(o.x, 0f, o.y);
                if (_grid.IsWaterAt(p)) AddRipple(p, Random.Range(0.25f, 0.45f));
            }
        }

        /// <summary>След тела в воде: всплеск при входе, дальше — кольцо каждые _trailStep метров.</summary>
        private void Track(Vector3 pos, bool moving, ref Vector3 last, ref bool wasWet, float splash, float trail)
        {
            if (!wasWet)
            {
                wasWet = true;
                last = pos;
                Splash(pos, splash);
                return;
            }
            if (!moving || (pos - last).sqrMagnitude < _trailStep * _trailStep) return;
            last = pos;
            AddRipple(pos, trail);
        }

        private void LateUpdate()
        {
            Shader.SetGlobalVectorArray(RipplesId, _ripples);
            Shader.SetGlobalFloat(CountId, _count);
            Shader.SetGlobalFloat(TimeId, Time.timeSinceLevelLoad);
        }
    }
}
