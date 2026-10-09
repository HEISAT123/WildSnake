using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Опасности уровней: с каждым уровнем включается ещё одна вращающаяся балка
    /// (из заранее созданного пула) в открытом месте поля.
    /// </summary>
    public class HazardManager : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Параметры баланса")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Пул балок (неактивные объекты в сцене)")]
        [SerializeField] private SpinnerHazard[] _spinners = new SpinnerHazard[0];

        [Header("Размещение")]
        [Tooltip("Длина балки, клеток")]
        [SerializeField, Min(1f)] private float _beamLength = 5f;

        [Tooltip("Минимальное расстояние от головы игрока при появлении, клеток")]
        [SerializeField, Min(0f)] private float _minDistanceFromPlayer = 7f;

        private int _activeCount;

        public int ActiveCount => _activeCount;

        public void ResetHazards()
        {
            for (int i = 0; i < _spinners.Length; i++) _spinners[i].gameObject.SetActive(false);
            _activeCount = 0;
        }

        /// <summary>Довести число балок до (уровень - 1), не ставя новые рядом с игроком.</summary>
        public void SetLevel(int level, Vector3 avoidPosition)
        {
            int desired = Mathf.Min(Mathf.Min(level - 1, _config.MaxSpinners), _spinners.Length);
            float speed = _config.SpinnerBaseSpeed + _config.SpinnerSpeedPerLevel * Mathf.Max(0, level - 2);

            for (int i = 0; i < _activeCount; i++)
                _spinners[i].SetSpeed(SpeedSign(i) * speed);

            while (_activeCount < desired)
            {
                if (!TryFindPlace(avoidPosition, out Vector3 center)) break;
                SpinnerHazard spinner = _spinners[_activeCount];
                spinner.Configure(center, _beamLength * _grid.CellSize, SpeedSign(_activeCount) * speed, _grid.CellSize);
                spinner.gameObject.SetActive(true);
                _activeCount++;
            }
        }

        public bool CheckHit(SnakeController snake)
        {
            if (!snake.IsAlive) return false;
            for (int i = 0; i < _activeCount; i++)
                if (_spinners[i].Hits(snake.HeadPosition, snake.HeadRadiusWorld, snake.HeightAboveGround)) return true;
            return false;
        }

        /// <summary>Отметить клетки под балками, чтобы еда и бонусы там не появлялись.</summary>
        public void MarkOccupiedCells()
        {
            for (int i = 0; i < _activeCount; i++)
            {
                Vector2Int c = _grid.WorldToCell(_spinners[i].CenterPosition);
                int r = Mathf.CeilToInt(_spinners[i].Radius / _grid.CellSize);
                for (int y = c.y - r; y <= c.y + r; y++)
                {
                    for (int x = c.x - r; x <= c.x + r; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (_grid.IsInside(cell)) _grid.SetOccupied(cell, true);
                    }
                }
            }
        }

        // Соседние балки крутятся в разные стороны — так интереснее.
        private static float SpeedSign(int index) => (index & 1) == 0 ? 1f : -1f;

        /// <summary>Ищем место, где круг балки не задевает препятствий и других балок и далеко от игрока.</summary>
        private bool TryFindPlace(Vector3 avoidPosition, out Vector3 center)
        {
            float cs = _grid.CellSize;
            int r = Mathf.CeilToInt(_beamLength * 0.5f) + 1;
            for (int attempt = 0; attempt < 200; attempt++)
            {
                var cell = new Vector2Int(Random.Range(r + 1, _grid.Width - r - 1), Random.Range(r + 1, _grid.Height - r - 1));
                if (!_grid.IsCellInArena(cell, r + 1)) continue;
                Vector3 pos = _grid.CellToWorld(cell);

                Vector3 toPlayer = pos - avoidPosition;
                toPlayer.y = 0f;
                float minDist = (_minDistanceFromPlayer + r) * cs;
                if (toPlayer.sqrMagnitude < minDist * minDist) continue;
                if (!IsAreaClear(cell, r) || OverlapsSpinner(pos, r * 2f * cs)) continue;

                center = pos;
                return true;
            }

            center = default;
            return false;
        }

        private bool IsAreaClear(Vector2Int c, int r)
        {
            for (int y = c.y - r; y <= c.y + r; y++)
            {
                for (int x = c.x - r; x <= c.x + r; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_grid.IsInside(cell) || _grid.GetObstacle(cell) != ObstacleType.None) return false;
                }
            }
            return true;
        }

        private bool OverlapsSpinner(Vector3 pos, float minDistance)
        {
            for (int i = 0; i < _activeCount; i++)
            {
                Vector3 d = _spinners[i].CenterPosition - pos;
                d.y = 0f;
                if (d.sqrMagnitude < minDistance * minDistance) return true;
            }
            return false;
        }
    }
}
