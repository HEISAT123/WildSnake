using UnityEngine;

namespace Snake3D
{
    /// <summary>Еда: появление в случайной свободной клетке и пульсация.</summary>
    public class FoodSpawner : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Объект еды (сфера)")]
        [SerializeField] private Transform _food;

        [Header("Вид")]
        [Tooltip("Высота центра еды над полем (доля клетки)")]
        [SerializeField] private float _heightOffset = 0.45f;

        [Tooltip("Базовый диаметр еды (доля клетки)")]
        [SerializeField, Range(0.1f, 1f)] private float _baseScale = 0.7f;

        [Header("Пульсация")]
        [Tooltip("Амплитуда пульсации размера")]
        [SerializeField, Range(0f, 0.5f)] private float _pulseAmplitude = 0.12f;

        [Tooltip("Скорость пульсации (рад/с)")]
        [SerializeField, Min(0f)] private float _pulseSpeed = 6f;

        [Tooltip("Длительность анимации появления, сек")]
        [SerializeField, Min(0f)] private float _appearDuration = 0.2f;

        private float _appearTimer;

        public bool HasFood { get; private set; }
        public Vector2Int Cell { get; private set; }

        /// <summary>Возвращает false, если свободных клеток нет (поле заполнено).</summary>
        public bool Spawn()
        {
            if (!_grid.TryGetRandomFreeCell(out Vector2Int cell))
            {
                Hide();
                return false;
            }

            Cell = cell;
            HasFood = true;
            _appearTimer = 0f;
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

            _appearTimer += Time.deltaTime;
            float appear = _appearDuration > 0f ? Mathf.Clamp01(_appearTimer / _appearDuration) : 1f;
            float pulse = 1f + _pulseAmplitude * Mathf.Sin(Time.time * _pulseSpeed);
            _food.localScale = Vector3.one * (_baseScale * _grid.CellSize * pulse * appear);
        }
    }
}
