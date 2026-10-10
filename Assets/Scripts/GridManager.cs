using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Логика поля: размеры, препятствия, заросли высокой травы (укрытия), границы арены, клетки для появления
    /// добычи и перевод клетка &lt;-&gt; мир. Раскладка детерминирована сидом.
    /// Визуал поля строит BoardView.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Размер поля")]
        [Tooltip("Количество клеток по оси X")]
        [SerializeField, Min(10)] private int _width = 44;

        [Tooltip("Количество клеток по оси Z")]
        [SerializeField, Min(10)] private int _height = 44;

        [Tooltip("Размер одной клетки в мировых единицах")]
        [SerializeField, Min(0.1f)] private float _cellSize = 1f;

        [Header("Препятствия")]
        [Tooltip("Сид раскладки: одинаковый сид — одинаковая карта")]
        [SerializeField] private int _obstacleSeed = 2024;

        [Tooltip("Количество низких стенок (их можно перепрыгнуть)")]
        [SerializeField, Min(0)] private int _lowWallCount = 18;

        [Tooltip("Количество колонн")]
        [SerializeField, Min(0)] private int _pillarCount = 18;

        [Tooltip("Количество больших блоков 2x2")]
        [SerializeField, Min(0)] private int _blockCount = 9;

        [Tooltip("Высота низкой стенки, клеток. Должна быть меньше высоты прыжка змейки")]
        [SerializeField, Min(0.1f)] private float _lowWallHeight = 0.45f;

        [Tooltip("Высота колонны, клеток")]
        [SerializeField, Min(0.1f)] private float _pillarHeight = 2.5f;

        [Tooltip("Высота блока, клеток")]
        [SerializeField, Min(0.1f)] private float _blockHeight = 1.6f;

        [Tooltip("Радиус свободной зоны вокруг точки старта, клеток")]
        [SerializeField, Min(2)] private int _startClearRadius = 6;

        [Header("Высокая трава")]
        [Tooltip("Количество зарослей высокой травы (укрытий)")]
        [SerializeField, Min(0)] private int _coverPatchCount = 16;

        [Tooltip("Средний радиус зарослей, клеток")]
        [SerializeField, Min(0.5f)] private float _coverPatchRadius = 2.6f;

        [Header("Предметы")]
        [Tooltip("Минимальный отступ еды и бонусов от границы арены, клеток (чтобы не появлялись в углах)")]
        [SerializeField, Min(0)] private int _itemBorderMargin = 2;

        private const float PILLAR_HALF_SIZE = 0.38f;

        private bool[] _occupied;
        private bool[] _nearObstacle;
        private ObstacleType[] _obstacles;
        private bool[] _cover;

        public int Width => _width;
        public int Height => _height;
        public float CellSize => _cellSize;
        public int CellCount => _width * _height;
        public Vector3 Center => transform.position;

        /// <summary>Насколько арена сужена с каждой стороны, клеток (режим «Выживание»).</summary>
        public float ArenaInset { get; set; }

        /// <summary>Половины размеров текущей арены в мировых единицах (x — по X, y — по Z).</summary>
        public Vector2 ArenaHalfExtents => new Vector2(
            (_width * 0.5f - ArenaInset) * _cellSize,
            (_height * 0.5f - ArenaInset) * _cellSize);

        /// <summary>Меняется при любой настройке, влияющей на раскладку: BoardView по нему решает, пересобирать ли поле.</summary>
        public int LayoutVersion
        {
            get
            {
                unchecked
                {
                    int h = _width;
                    h = h * 31 + _height;
                    h = h * 31 + _cellSize.GetHashCode();
                    h = h * 31 + _obstacleSeed;
                    h = h * 31 + _lowWallCount;
                    h = h * 31 + _pillarCount;
                    h = h * 31 + _blockCount;
                    h = h * 31 + _lowWallHeight.GetHashCode();
                    h = h * 31 + _pillarHeight.GetHashCode();
                    h = h * 31 + _blockHeight.GetHashCode();
                    h = h * 31 + _startClearRadius;
                    h = h * 31 + _coverPatchCount;
                    h = h * 31 + _coverPatchRadius.GetHashCode();
                    return h;
                }
            }
        }

        private void Awake()
        {
            GenerateLayout();
        }

        /// <summary>Сгенерировать препятствия. Вызывается и из редактора (BoardView, тесты).</summary>
        public void GenerateLayout()
        {
            int n = CellCount;
            _occupied = new bool[n];
            _nearObstacle = new bool[n];
            _obstacles = new ObstacleType[n];
            _cover = new bool[n];

            // System.Random, а не UnityEngine.Random: свой генератор с сидом не сбивает глобальный.
            var rng = new System.Random(_obstacleSeed);
            PlaceObstacles(rng, ObstacleType.Block, _blockCount);
            PlaceObstacles(rng, ObstacleType.Pillar, _pillarCount);
            PlaceObstacles(rng, ObstacleType.LowWall, _lowWallCount);
            // Траву раскладываем отдельным генератором: изменение зарослей не сдвигает препятствия.
            PlaceCover(new System.Random(_obstacleSeed * 7 + 13));

            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    _nearObstacle[y * _width + x] = HasObstacleNear(x, y, 1);
        }

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;
        }

        /// <summary>Клетка внутри текущей арены с отступом margin клеток от её границы.</summary>
        public bool IsCellInArena(Vector2Int cell, int margin)
        {
            int inset = Mathf.CeilToInt(ArenaInset) + margin;
            return cell.x >= inset && cell.x < _width - inset && cell.y >= inset && cell.y < _height - inset;
        }

        public void SetOccupied(Vector2Int cell, bool value)
        {
            _occupied[ToIndex(cell)] = value;
        }

        public void ClearAll()
        {
            System.Array.Clear(_occupied, 0, _occupied.Length);
        }

        /// <summary>Клетка заросла высокой травой (укрытие от добычи и орла).</summary>
        public bool IsCover(Vector2Int cell)
        {
            return IsInside(cell) && _cover[ToIndex(cell)];
        }

        public bool IsCoverAt(Vector3 world)
        {
            return IsCover(WorldToCell(world));
        }

        public int CoverCellCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _cover.Length; i++)
                    if (_cover[i]) count++;
                return count;
            }
        }

        public ObstacleType GetObstacle(Vector2Int cell)
        {
            return _obstacles[ToIndex(cell)];
        }

        /// <summary>Высота препятствия в мировых единицах (0 — препятствия нет).</summary>
        public float GetObstacleHeight(Vector2Int cell)
        {
            switch (_obstacles[ToIndex(cell)])
            {
                case ObstacleType.LowWall: return _lowWallHeight * _cellSize;
                case ObstacleType.Pillar: return _pillarHeight * _cellSize;
                case ObstacleType.Block: return _blockHeight * _cellSize;
                default: return 0f;
            }
        }

        /// <summary>Половина ширины препятствия в мировых единицах (колонна тоньше клетки).</summary>
        public float GetObstacleHalfSize(Vector2Int cell)
        {
            float half = _obstacles[ToIndex(cell)] == ObstacleType.Pillar ? PILLAR_HALF_SIZE : 0.5f;
            return half * _cellSize;
        }

        /// <summary>
        /// Случайная клетка для предмета: внутри арены с отступом, не вплотную к препятствию
        /// и не занятая. Без аллокаций.
        /// </summary>
        public bool TryGetRandomFreeCell(out Vector2Int cell)
        {
            int free = 0;
            for (int i = 0; i < _occupied.Length; i++)
                if (IsSpawnable(i)) free++;

            if (free > 0)
            {
                int k = Random.Range(0, free);
                for (int i = 0; i < _occupied.Length; i++)
                {
                    if (!IsSpawnable(i)) continue;
                    if (k == 0)
                    {
                        cell = new Vector2Int(i % _width, i / _width);
                        return true;
                    }
                    k--;
                }
            }

            cell = default;
            return false;
        }

        /// <summary>Центр клетки на уровне поверхности поля (y = 0 относительно сетки).</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            float x = (cell.x - (_width - 1) * 0.5f) * _cellSize;
            float z = (cell.y - (_height - 1) * 0.5f) * _cellSize;
            return transform.position + new Vector3(x, 0f, z);
        }

        /// <summary>Ближайшая клетка к мировой точке (может оказаться вне поля — проверяй IsInside).</summary>
        public Vector2Int WorldToCell(Vector3 world)
        {
            Vector3 local = world - transform.position;
            int x = Mathf.RoundToInt(local.x / _cellSize + (_width - 1) * 0.5f);
            int y = Mathf.RoundToInt(local.z / _cellSize + (_height - 1) * 0.5f);
            return new Vector2Int(x, y);
        }

        private bool IsSpawnable(int index)
        {
            if (_occupied[index] || _nearObstacle[index]) return false;
            return IsCellInArena(new Vector2Int(index % _width, index / _width), _itemBorderMargin);
        }

        private void PlaceObstacles(System.Random rng, ObstacleType type, int count)
        {
            int placed = 0;
            int attempts = count * 40;
            while (placed < count && attempts-- > 0)
            {
                int sx = 1, sy = 1;
                if (type == ObstacleType.Block)
                {
                    sx = 2;
                    sy = 2;
                }
                else if (type == ObstacleType.LowWall)
                {
                    int length = rng.Next(3, 6);
                    if (rng.Next(2) == 0) sx = length;
                    else sy = length;
                }

                // Отступ 2 клетки от внешних стен, чтобы вдоль края всегда был проезд.
                int maxX = _width - 2 - sx;
                int maxY = _height - 2 - sy;
                if (maxX < 2 || maxY < 2) return;
                int ox = rng.Next(2, maxX + 1);
                int oy = rng.Next(2, maxY + 1);

                if (!CanPlace(ox, oy, sx, sy)) continue;
                for (int y = oy; y < oy + sy; y++)
                    for (int x = ox; x < ox + sx; x++)
                        _obstacles[y * _width + x] = type;
                placed++;
            }
        }

        /// <summary>
        /// Заросли — «кляксы» неровной формы: радиус клетки от центра сравнивается с радиусом,
        /// который меняется по углу. Центр старта остаётся открытым, чтобы змею было видно сразу.
        /// </summary>
        private void PlaceCover(System.Random rng)
        {
            float cx = (_width - 1) * 0.5f;
            float cy = (_height - 1) * 0.5f;
            float clear = _startClearRadius * 0.5f;
            for (int p = 0; p < _coverPatchCount; p++)
            {
                float px = 3f + (float)rng.NextDouble() * (_width - 7f);
                float py = 3f + (float)rng.NextDouble() * (_height - 7f);
                float radius = _coverPatchRadius * (0.7f + (float)rng.NextDouble() * 0.6f);
                float phase = (float)rng.NextDouble() * 6.283f;
                int r = Mathf.CeilToInt(radius * 1.4f);
                for (int y = Mathf.FloorToInt(py) - r; y <= Mathf.CeilToInt(py) + r; y++)
                {
                    for (int x = Mathf.FloorToInt(px) - r; x <= Mathf.CeilToInt(px) + r; x++)
                    {
                        if (x < 1 || x >= _width - 1 || y < 1 || y >= _height - 1) continue;
                        int i = y * _width + x;
                        if (_obstacles[i] != ObstacleType.None) continue;
                        float dx = x - px;
                        float dy = y - py;
                        float angle = Mathf.Atan2(dy, dx);
                        float edge = radius * (1f + 0.3f * Mathf.Sin(angle * 3f + phase));
                        if (dx * dx + dy * dy > edge * edge) continue;
                        float sx = x - cx;
                        float sy = y - cy;
                        if (sx * sx + sy * sy < clear * clear) continue;
                        _cover[i] = true;
                    }
                }
            }
        }

        /// <summary>Не рядом со стартом и с зазором в клетку от других препятствий, чтобы не было тупиков.</summary>
        private bool CanPlace(int ox, int oy, int sx, int sy)
        {
            float cx = (_width - 1) * 0.5f;
            float cy = (_height - 1) * 0.5f;
            float clearSqr = _startClearRadius * _startClearRadius;

            for (int y = oy - 1; y <= oy + sy; y++)
            {
                for (int x = ox - 1; x <= ox + sx; x++)
                {
                    if (x < 0 || x >= _width || y < 0 || y >= _height) continue;
                    if (_obstacles[y * _width + x] != ObstacleType.None) return false;
                }
            }

            for (int y = oy; y < oy + sy; y++)
            {
                for (int x = ox; x < ox + sx; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy < clearSqr) return false;
                }
            }
            return true;
        }

        private bool HasObstacleNear(int cx, int cy, int radius)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || x >= _width || y < 0 || y >= _height) continue;
                    if (_obstacles[y * _width + x] != ObstacleType.None) return true;
                }
            }
            return false;
        }

        private int ToIndex(Vector2Int cell)
        {
            return cell.y * _width + cell.x;
        }
    }
}
