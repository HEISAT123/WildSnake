using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Бонус на поле: время от времени появляется кристалл случайного типа, крутится и парит,
    /// исчезает, если его не подобрать. Подбирает только игрок.
    /// </summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Параметры баланса (интервалы)")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Змейка игрока")]
        [SerializeField] private SnakeController _player;

        [Tooltip("Объект кристалла")]
        [SerializeField] private Transform _item;

        [Tooltip("Рендерер кристалла")]
        [SerializeField] private Renderer _itemRenderer;

        [Tooltip("Материалы по типам бонусов (порядок как в PowerUpType)")]
        [SerializeField] private Material[] _typeMaterials = new Material[5];

        [Header("Вид")]
        [Tooltip("Высота парения над полем (доля клетки)")]
        [SerializeField] private float _hoverHeight = 0.7f;

        [Tooltip("Размер кристалла (доля клетки)")]
        [SerializeField, Range(0.1f, 1f)] private float _size = 0.55f;

        [Tooltip("Скорость вращения, град/с")]
        [SerializeField] private float _spinSpeed = 120f;

        [Tooltip("Радиус подбора (доля клетки)")]
        [SerializeField, Min(0.1f)] private float _pickupRadius = 0.9f;

        private const int TYPE_COUNT = 5;

        /// <summary>(тип, позиция)</summary>
        public event Action<PowerUpType, Vector3> Collected;

        public bool IsRunning { get; set; }
        public bool HasItem { get; private set; }
        public bool WantsSpawn => IsRunning && !HasItem && _spawnTimer <= 0f;
        public Vector2Int Cell { get; private set; }
        public PowerUpType Type { get; private set; }

        private float _spawnTimer;
        private float _lifetime;
        private float _angle;

        public void ResetSpawner()
        {
            HideItem();
            _spawnTimer = _config.PowerUpFirstDelay;
        }

        /// <summary>Поставить бонус сейчас (GameManager перед этим размечает занятые клетки).</summary>
        public void SpawnNow()
        {
            if (!_grid.TryGetRandomFreeCell(out Vector2Int cell))
            {
                _spawnTimer = 2f;
                return;
            }

            Cell = cell;
            Type = (PowerUpType)UnityEngine.Random.Range(0, TYPE_COUNT);
            HasItem = true;
            _lifetime = _config.PowerUpLifetime;
            _itemRenderer.sharedMaterial = _typeMaterials[(int)Type];
            _item.position = _grid.CellToWorld(cell) + Vector3.up * (_hoverHeight * _grid.CellSize);
            _item.localScale = Vector3.one * (_size * _grid.CellSize);
            _item.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsRunning) return;
            float dt = Time.deltaTime;

            if (!HasItem)
            {
                _spawnTimer -= dt;
                return;
            }

            _lifetime -= dt;
            if (_lifetime <= 0f)
            {
                HideItem();
                _spawnTimer = _config.PowerUpInterval;
                return;
            }

            float cs = _grid.CellSize;
            _angle += _spinSpeed * dt;
            Vector3 basePos = _grid.CellToWorld(Cell);
            float bob = Mathf.Sin(Time.time * 3f) * 0.12f * cs;
            _item.SetPositionAndRotation(basePos + Vector3.up * (_hoverHeight * cs + bob), Quaternion.Euler(45f, _angle, 45f));
            bool blinkHidden = _lifetime < 2f && Mathf.Repeat(Time.time * 6f, 1f) < 0.4f;
            _itemRenderer.enabled = !blinkHidden;

            if (!_player.IsAlive) return;
            float r = _pickupRadius * cs;
            Vector3 d = _player.HeadPosition - _item.position;
            d.y *= 0.5f; // по высоте прощаем больше: подобрать можно и в прыжке
            if (d.sqrMagnitude < r * r)
            {
                PowerUpType type = Type;
                Vector3 pos = _item.position;
                HideItem();
                _spawnTimer = _config.PowerUpInterval;
                Collected?.Invoke(type, pos);
            }
        }

        private void HideItem()
        {
            HasItem = false;
            _item.gameObject.SetActive(false);
        }
    }
}
