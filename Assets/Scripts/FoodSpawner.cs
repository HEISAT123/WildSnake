using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Еда: обычное, золотое (быстро исчезает) и гнилое яблоко. Появление в свободной клетке,
    /// пульсация, мигание перед исчезновением, притяжение магнитом.
    /// </summary>
    public class FoodSpawner : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Параметры баланса (шансы и время жизни)")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Объект еды")]
        [SerializeField] private Transform _food;

        [Tooltip("Рендерер еды (меняем материал по виду)")]
        [SerializeField] private Renderer _foodRenderer;

        [Header("Материалы")]
        [Tooltip("Обычное яблоко")]
        [SerializeField] private Material _normalMaterial;

        [Tooltip("Золотое яблоко")]
        [SerializeField] private Material _goldenMaterial;

        [Tooltip("Гнилое яблоко")]
        [SerializeField] private Material _rottenMaterial;

        [Header("Вид")]
        [Tooltip("Высота центра еды над полем (доля клетки)")]
        [SerializeField] private float _heightOffset = 0.42f;

        [Tooltip("Базовый диаметр еды (доля клетки)")]
        [SerializeField, Range(0.1f, 1f)] private float _baseScale = 0.7f;

        [Tooltip("Амплитуда пульсации размера")]
        [SerializeField, Range(0f, 0.5f)] private float _pulseAmplitude = 0.12f;

        [Tooltip("Скорость пульсации (рад/с)")]
        [SerializeField, Min(0f)] private float _pulseSpeed = 6f;

        [Tooltip("Длительность анимации появления, сек")]
        [SerializeField, Min(0f)] private float _appearDuration = 0.2f;

        [Tooltip("За сколько секунд до исчезновения еда начинает мигать")]
        [SerializeField, Min(0f)] private float _blinkBeforeExpire = 1.5f;

        /// <summary>Временная еда исчезла сама (нужно поставить новую).</summary>
        public event Action Expired;

        public bool HasFood { get; private set; }
        public FoodKind Kind { get; private set; }
        public Vector2Int Cell { get; private set; }
        public Vector3 Position => _food.position;

        /// <summary>Куда тянет магнит (голова игрока); работает, пока MagnetActive.</summary>
        public Transform MagnetTarget { get; set; }
        public bool MagnetActive { get; set; }

        private float _appearTimer;
        private float _lifetime;

        /// <summary>Поставить еду случайного вида. Возвращает false, если свободных клеток нет.</summary>
        public bool Spawn()
        {
            float r = UnityEngine.Random.value;
            FoodKind kind = FoodKind.Normal;
            if (r < _config.GoldenChance) kind = FoodKind.Golden;
            else if (r < _config.GoldenChance + _config.RottenChance) kind = FoodKind.Rotten;
            return Spawn(kind);
        }

        public bool Spawn(FoodKind kind)
        {
            if (!_grid.TryGetRandomFreeCell(out Vector2Int cell))
            {
                Hide();
                return false;
            }

            Kind = kind;
            Cell = cell;
            HasFood = true;
            _appearTimer = 0f;
            _lifetime = kind == FoodKind.Golden ? _config.GoldenLifetime
                : kind == FoodKind.Rotten ? _config.RottenLifetime
                : -1f;

            _foodRenderer.sharedMaterial = kind == FoodKind.Golden ? _goldenMaterial
                : kind == FoodKind.Rotten ? _rottenMaterial
                : _normalMaterial;

            _food.position = _grid.CellToWorld(cell) + Vector3.up * (_heightOffset * _grid.CellSize);
            _food.localScale = Vector3.zero;
            _food.gameObject.SetActive(true);
            return true;
        }

        public void Hide()
        {
            HasFood = false;
            _food.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!HasFood) return;
            float dt = Time.deltaTime;

            if (_lifetime > 0f)
            {
                _lifetime -= dt;
                if (_lifetime <= 0f)
                {
                    Hide();
                    Expired?.Invoke();
                    return;
                }
            }

            if (MagnetActive && MagnetTarget != null) PullToMagnet(dt);

            _appearTimer += dt;
            float appear = _appearDuration > 0f ? Mathf.Clamp01(_appearTimer / _appearDuration) : 1f;
            float pulse = 1f + _pulseAmplitude * Mathf.Sin(Time.time * _pulseSpeed);
            float blink = _lifetime > 0f && _lifetime < _blinkBeforeExpire && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? 0.55f : 1f;
            _food.localScale = Vector3.one * (_baseScale * _grid.CellSize * pulse * appear * blink);
        }

        private void PullToMagnet(float dt)
        {
            float cs = _grid.CellSize;
            Vector3 pos = _food.position;
            Vector3 delta = MagnetTarget.position - pos;
            delta.y = 0f;
            float radius = _config.MagnetRadius * cs;
            float distSqr = delta.sqrMagnitude;
            if (distSqr > radius * radius || distSqr < 1e-6f) return;

            float step = _config.MagnetPullSpeed * cs * dt;
            float dist = Mathf.Sqrt(distSqr);
            _food.position = pos + delta * (Mathf.Min(step, dist) / dist);
        }
    }
}
