using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Орёл — дневной хищник. Прилетает с криком, кружит над змеёй (по земле скользит его тень),
    /// затем пикирует. Спасают высокая трава (орёл теряет змею из виду и уходит) и бросок в последний
    /// момент: точка удара догоняет змею, но не успевает за броском. В меню орёл лишь кружит для красоты.
    /// </summary>
    public class EagleHunter : MonoBehaviour
    {
        public enum Phase
        {
            Away,
            Circling,
            Diving,
            Climbing
        }

        [Header("Ссылки")]
        [Tooltip("Параметры баланса (интервалы охоты)")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля (уровень земли, размер клетки)")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Змея, на которую охотится орёл")]
        [SerializeField] private SnakeController _target;

        [Tooltip("Модель орла")]
        [SerializeField] private Transform _model;

        [Tooltip("Левое крыло (шарнир у плеча)")]
        [SerializeField] private Transform _leftWing;

        [Tooltip("Правое крыло (шарнир у плеча)")]
        [SerializeField] private Transform _rightWing;

        [Tooltip("Тень на земле")]
        [SerializeField] private Renderer _shadow;

        [Header("Полёт")]
        [Tooltip("Радиус кружения над змеёй, клеток")]
        [SerializeField, Min(1f)] private float _circleRadius = 9f;

        [Tooltip("Высота кружения, мировых единиц")]
        [SerializeField, Min(2f)] private float _circleHeight = 13f;

        [Tooltip("Угловая скорость кружения, град/с")]
        [SerializeField] private float _circleSpeed = 42f;

        [Tooltip("Сколько кружит перед пикированием, сек")]
        [SerializeField, Min(0.5f)] private float _circleTime = 5f;

        [Tooltip("Сколько ещё кружит, если змея спряталась, сек — потом улетает")]
        [SerializeField, Min(0f)] private float _searchTime = 2.5f;

        [Tooltip("Длительность пикирования, сек")]
        [SerializeField, Min(0.3f)] private float _diveTime = 1.7f;

        [Tooltip("Скорость, с которой точка удара догоняет змею, клеток в секунду")]
        [SerializeField, Min(0f)] private float _trackSpeed = 5f;

        [Tooltip("Радиус удара когтями, клеток")]
        [SerializeField, Min(0.1f)] private float _strikeRadius = 1.3f;

        [Tooltip("Длительность набора высоты после атаки, сек")]
        [SerializeField, Min(0.3f)] private float _climbTime = 2.5f;

        [Tooltip("Максимальная скорость полёта, мировых единиц в секунду")]
        [SerializeField, Min(1f)] private float _maxSpeed = 22f;

        [Header("Тень")]
        [Tooltip("Размер тени, когда орёл высоко")]
        [SerializeField, Min(0.1f)] private float _shadowHighSize = 4.5f;

        [Tooltip("Размер тени у самой земли")]
        [SerializeField, Min(0.1f)] private float _shadowLowSize = 2.2f;

        [Tooltip("Прозрачность тени высоко / у земли")]
        [SerializeField] private Vector2 _shadowAlpha = new Vector2(0.25f, 0.6f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Орёл появился и закричал.</summary>
        public event Action Screeched;

        /// <summary>Орёл начал пикировать.</summary>
        public event Action DiveStarted;

        /// <summary>Удар пришёлся по змее (точка удара).</summary>
        public event Action<Vector3> Struck;

        /// <summary>Атака сорвалась: змея спряталась или увернулась.</summary>
        public event Action Missed;

        /// <summary>Можно начинать новую охоту (идёт партия и светло).</summary>
        public bool HuntingAllowed { get; set; }

        /// <summary>Меню: орёл кружит для атмосферы, но не нападает.</summary>
        public bool Ambient { get; set; }

        public Phase CurrentPhase => _phase;
        public bool IsThreatening => _phase == Phase.Circling || _phase == Phase.Diving;
        public bool IsDiving => _phase == Phase.Diving;
        public float DiveProgress01 => _phase == Phase.Diving ? Mathf.Clamp01(_phaseTime / _diveTime) : 0f;

        private Phase _phase;
        private float _phaseTime;
        private float _timer;
        private float _searchLeft;
        private Vector3 _pos;
        private Vector3 _velocity;
        private Vector3 _circleCenter;
        private Vector3 _diveStart;
        private Vector3 _aim;
        private float _angle;
        private float _bank;
        private float _wingTime;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetVisible(false);
        }

        /// <summary>Сбросить охоту: орёл улетел, первая атака через delay секунд.</summary>
        public void ResetHunter(float delay)
        {
            _phase = Phase.Away;
            _timer = delay;
            _velocity = Vector3.zero;
            SetVisible(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _phaseTime += dt;
            _wingTime += dt;

            switch (_phase)
            {
                case Phase.Away:
                    TickAway(dt);
                    break;
                case Phase.Circling:
                    TickCircling(dt);
                    break;
                case Phase.Diving:
                    TickDiving(dt);
                    break;
                case Phase.Climbing:
                    TickClimbing(dt);
                    break;
            }

            if (_phase != Phase.Away) ApplyVisual(dt);
        }

        // ---------------- Фазы ----------------

        private void TickAway(float dt)
        {
            bool canStart = (HuntingAllowed || Ambient) && TargetAlive();
            if (!canStart) return;
            _timer -= dt;
            if (_timer > 0f) return;

            // Заходим издалека, с высоты, с случайной стороны.
            _angle = UnityEngine.Random.Range(0f, 360f);
            _circleCenter = _target.HeadPosition;
            Vector3 dir = Quaternion.Euler(0f, _angle, 0f) * Vector3.forward;
            _pos = _circleCenter + dir * 60f + Vector3.up * (_circleHeight + 8f);
            _velocity = -dir * _maxSpeed * 0.6f;
            _searchLeft = _searchTime;
            SetPhase(Phase.Circling);
            SetVisible(true);
            Screeched?.Invoke();
        }

        private void TickCircling(float dt)
        {
            if (!TargetAlive())
            {
                SetPhase(Phase.Climbing);
                return;
            }

            // Центр круга плавно следует за змеёй.
            _circleCenter = Vector3.Lerp(_circleCenter, _target.HeadPosition, 1f - Mathf.Exp(-0.8f * dt));
            _angle += _circleSpeed * dt;
            float r = _circleRadius * _grid.CellSize;
            Vector3 desired = _circleCenter + Quaternion.Euler(0f, _angle, 0f) * Vector3.forward * r;
            desired.y = Ground() + _circleHeight;
            FlyTowards(desired, dt, 1.6f);

            if (_phaseTime < _circleTime) return;
            if (Ambient && !HuntingAllowed)
            {
                SetPhase(Phase.Climbing);
                return;
            }

            if (_target.IsHidden)
            {
                // Змея в траве: орёл покружит ещё немного и улетит ни с чем.
                _searchLeft -= dt;
                if (_searchLeft > 0f) return;
                Missed?.Invoke();
                SetPhase(Phase.Climbing);
                return;
            }

            _diveStart = _pos;
            _aim = _target.HeadPosition;
            SetPhase(Phase.Diving);
            DiveStarted?.Invoke();
        }

        private void TickDiving(float dt)
        {
            if (!TargetAlive())
            {
                SetPhase(Phase.Climbing);
                return;
            }

            // Пока орёл высоко, трава ещё спасает — он теряет цель и выходит из пике.
            float t = Mathf.Clamp01(_phaseTime / _diveTime);
            if (_target.IsHidden && t < 0.75f)
            {
                Missed?.Invoke();
                SetPhase(Phase.Climbing);
                return;
            }

            Vector3 head = _target.HeadPosition;
            head.y = Ground();
            _aim = Vector3.MoveTowards(_aim, head, _trackSpeed * _grid.CellSize * dt);

            Vector3 strikePoint = _aim + Vector3.up * 0.5f;
            Vector3 previous = _pos;
            // Пике ускоряется: путь пропорционален квадрату времени.
            _pos = Vector3.Lerp(_diveStart, strikePoint, t * t);
            _velocity = (_pos - previous) / dt;

            if (t < 1f) return;

            Vector3 d = _target.HeadPosition - _aim;
            d.y = 0f;
            float r = _strikeRadius * _grid.CellSize;
            if (d.sqrMagnitude <= r * r && !_target.IsHidden) Struck?.Invoke(_target.HeadPosition);
            else Missed?.Invoke();
            SetPhase(Phase.Climbing);
        }

        private void TickClimbing(float dt)
        {
            Vector3 horizontal = _velocity;
            horizontal.y = 0f;
            if (horizontal.sqrMagnitude < 1f) horizontal = Quaternion.Euler(0f, _angle, 0f) * Vector3.forward;
            Vector3 desired = _pos + horizontal.normalized * 20f + Vector3.up * 14f;
            FlyTowards(desired, dt, 2.5f);

            if (_phaseTime < _climbTime) return;
            _phase = Phase.Away;
            _timer = UnityEngine.Random.Range(_config.EagleIntervalMin, _config.EagleIntervalMax);
            SetVisible(false);
        }

        // ---------------- Движение и вид ----------------

        private void FlyTowards(Vector3 desired, float dt, float sharpness)
        {
            Vector3 wanted = (desired - _pos) * sharpness;
            if (wanted.sqrMagnitude > _maxSpeed * _maxSpeed) wanted = wanted.normalized * _maxSpeed;
            _velocity = Vector3.Lerp(_velocity, wanted, 1f - Mathf.Exp(-3f * dt));
            _pos += _velocity * dt;
        }

        private void ApplyVisual(float dt)
        {
            Vector3 flat = _velocity;
            flat.y = 0f;
            float yaw = flat.sqrMagnitude > 0.01f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : _model.eulerAngles.y;
            float pitch = Mathf.Clamp(-Mathf.Atan2(_velocity.y, Mathf.Max(0.1f, flat.magnitude)) * Mathf.Rad2Deg, -70f, 70f);
            float targetBank = _phase == Phase.Circling ? -28f : 0f;
            _bank = Mathf.Lerp(_bank, targetBank, 1f - Mathf.Exp(-3f * dt));
            _model.SetPositionAndRotation(_pos, Quaternion.Euler(pitch, yaw, _bank));

            // Парит с редкими взмахами, в пике крылья прижаты, при наборе высоты машет часто.
            float wing;
            switch (_phase)
            {
                case Phase.Diving: wing = 55f; break;
                case Phase.Climbing: wing = Mathf.Sin(_wingTime * 11f) * 40f; break;
                default: wing = Mathf.Sin(_wingTime * 2.2f) * 8f; break;
            }
            _leftWing.localRotation = Quaternion.Euler(0f, 0f, -wing);
            _rightWing.localRotation = Quaternion.Euler(0f, 0f, wing);

            float ground = Ground();
            float height = Mathf.Max(0f, _pos.y - ground);
            float k = Mathf.Clamp01(height / (_circleHeight + 4f));
            float size = Mathf.Lerp(_shadowLowSize, _shadowHighSize, k);
            Transform shadow = _shadow.transform;
            shadow.SetPositionAndRotation(new Vector3(_pos.x, ground + 0.03f, _pos.z), Quaternion.Euler(90f, yaw, 0f));
            shadow.localScale = new Vector3(size, size * 0.55f, 1f);
            _block.SetColor(ColorId, new Color(0f, 0f, 0f, Mathf.Lerp(_shadowAlpha.y, _shadowAlpha.x, k)));
            _shadow.SetPropertyBlock(_block);
        }

        private void SetPhase(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
        }

        private void SetVisible(bool visible)
        {
            if (_model != null && _model.gameObject.activeSelf != visible) _model.gameObject.SetActive(visible);
            if (_shadow != null && _shadow.gameObject.activeSelf != visible) _shadow.gameObject.SetActive(visible);
        }

        private bool TargetAlive()
        {
            return _target != null && _target.IsAlive && _target.IsRunning && !_target.InBurrow;
        }

        private float Ground()
        {
            return _grid.Center.y;
        }
    }
}
