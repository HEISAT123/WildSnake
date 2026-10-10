using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Логика мира: размеры, рельеф (карта высот по TerrainShape — холмы внутри, горы по краю как граница),
    /// препятствия, рощи (деревья с кронами — тень и укрытие от орла), заросли высокой травы (укрытия), озеро и пруды
    /// со своим уровнем воды, солнечные камни, норы, гнездо (главная цель), клетки для появления добычи
    /// и перевод клетка &lt;-&gt; мир. Раскладка детерминирована сидом. Визуал строит BoardView.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        private const float PILLAR_HALF_SIZE = 0.38f;
        private const float MOUNTAIN_HEIGHT = 60f;
        private const int MAX_BURROWS = 12;
        private const int MAX_PONDS = 6;

        [Header("Размер мира")]
        [Tooltip("Количество клеток по оси X")]
        [SerializeField, Min(10)] private int _width = 180;

        [Tooltip("Количество клеток по оси Z")]
        [SerializeField, Min(10)] private int _height = 180;

        [Tooltip("Размер одной клетки в мировых единицах")]
        [SerializeField, Min(0.1f)] private float _cellSize = 1f;

        [Tooltip("С какой «горности» (0..1) клетка считается непроходимой горой")]
        [SerializeField, Range(0.01f, 1f)] private float _mountainThreshold = 0.08f;

        [Header("Препятствия")]
        [Tooltip("Сид раскладки: одинаковый сид — одинаковая карта")]
        [SerializeField] private int _obstacleSeed = 2024;

        [Tooltip("Количество поваленных брёвен (их можно перепрыгнуть)")]
        [SerializeField, Min(0)] private int _lowWallCount = 150;

        [Tooltip("Количество одиночных пней и стоячих камней вне рощ")]
        [SerializeField, Min(0)] private int _pillarCount = 100;

        [Tooltip("Количество валунов 2x2")]
        [SerializeField, Min(0)] private int _blockCount = 70;

        [Tooltip("Высота бревна, клеток. Должна быть меньше высоты прыжка змейки")]
        [SerializeField, Min(0.1f)] private float _lowWallHeight = 0.45f;

        [Tooltip("Высота пня или дерева для столкновений, клеток")]
        [SerializeField, Min(0.1f)] private float _pillarHeight = 2.5f;

        [Tooltip("Высота валуна, клеток")]
        [SerializeField, Min(0.1f)] private float _blockHeight = 1.6f;

        [Tooltip("Радиус свободной зоны вокруг точки старта, клеток")]
        [SerializeField, Min(2)] private int _startClearRadius = 6;

        [Header("Высокая трава")]
        [Tooltip("Количество зарослей высокой травы (укрытий)")]
        [SerializeField, Min(0)] private int _coverPatchCount = 130;

        [Tooltip("Средний радиус зарослей, клеток")]
        [SerializeField, Min(0.5f)] private float _coverPatchRadius = 2.6f;

        [Header("Рощи")]
        [Tooltip("Сколько рощ в мире")]
        [SerializeField, Range(0, 16)] private int _groveCount = 10;

        [Tooltip("Средний радиус рощи, клеток")]
        [SerializeField, Min(2f)] private float _groveRadius = 10f;

        [Tooltip("Доля клеток рощи, на которых растёт дерево")]
        [SerializeField, Range(0f, 0.5f)] private float _treeDensity = 0.17f;

        [Tooltip("Насколько далеко от ствола тянется крона (тень, укрытие от орла), клеток")]
        [SerializeField, Min(0)] private int _canopyRadius = 2;

        [Header("Вода")]
        [Tooltip("Радиус большого озера, клеток (0 — без озера)")]
        [SerializeField, Min(0f)] private float _lakeRadius = 11f;

        [Tooltip("Сколько небольших прудов")]
        [SerializeField, Range(0, MAX_PONDS - 1)] private int _pondCount = 4;

        [Tooltip("Радиус пруда, клеток (мин, макс)")]
        [SerializeField] private Vector2 _pondRadius = new Vector2(3.5f, 5f);

        [Header("Солнечные камни")]
        [Tooltip("Сколько плоских камней, на которых змея греется")]
        [SerializeField, Min(0)] private int _sunStoneCount = 30;

        [Header("Норы")]
        [Tooltip("Сколько нор в мире")]
        [SerializeField, Range(0, MAX_BURROWS)] private int _burrowCount = 10;

        [Tooltip("Минимальное расстояние между норами, клеток")]
        [SerializeField, Min(2f)] private float _burrowSpacing = 20f;

        [Header("Предметы")]
        [Tooltip("Минимальный отступ появления добычи от края поля, клеток")]
        [SerializeField, Min(0)] private int _itemBorderMargin = 2;

        private bool[] _occupied;
        private bool[] _nearObstacle;
        private ObstacleType[] _obstacles;
        private bool[] _cover;
        private bool[] _water;
        private float[] _waterLevel;
        private bool[] _sunStone;
        private bool[] _burrowCell;
        private bool[] _forest;
        private bool[] _canopy;
        private float[] _heights;
        private Vector2Int _nest;
        private bool _hasNest;
        private readonly Vector2Int[] _burrows = new Vector2Int[MAX_BURROWS];
        private readonly Vector2Int[] _sunStones = new Vector2Int[48];
        private readonly Vector2[] _pondCenters = new Vector2[MAX_PONDS];
        private readonly float[] _pondRadii = new float[MAX_PONDS];
        private readonly float[] _pondPhases = new float[MAX_PONDS];
        private readonly float[] _pondLevels = new float[MAX_PONDS];
        private int _ponds;
        private int _placedBurrows;
        private int _placedSunStones;

        public int Width => _width;
        public int Height => _height;
        public float CellSize => _cellSize;
        public int CellCount => _width * _height;
        public Vector3 Center => transform.position;

        /// <summary>Насколько поле сужено с каждой стороны, клеток.</summary>
        public float ArenaInset { get; set; }

        /// <summary>Половины размеров поля в мировых единицах (x — по X, y — по Z).</summary>
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
                    h = h * 31 + _mountainThreshold.GetHashCode();
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
                    h = h * 31 + _groveCount;
                    h = h * 31 + _groveRadius.GetHashCode();
                    h = h * 31 + _treeDensity.GetHashCode();
                    h = h * 31 + _canopyRadius;
                    h = h * 31 + _lakeRadius.GetHashCode();
                    h = h * 31 + _pondCount;
                    h = h * 31 + _pondRadius.GetHashCode();
                    h = h * 31 + _sunStoneCount;
                    h = h * 31 + _burrowCount;
                    h = h * 31 + _burrowSpacing.GetHashCode();
                    return h;
                }
            }
        }

        private void Awake()
        {
            GenerateLayout();
        }

        /// <summary>Сгенерировать мир. Вызывается и из редактора (BoardView, EnvironmentBuilder, тесты).</summary>
        public void GenerateLayout()
        {
            int n = CellCount;
            _occupied = new bool[n];
            _nearObstacle = new bool[n];
            _obstacles = new ObstacleType[n];
            _cover = new bool[n];
            _water = new bool[n];
            _waterLevel = new float[n];
            _sunStone = new bool[n];
            _burrowCell = new bool[n];
            _forest = new bool[n];
            _canopy = new bool[n];
            for (int i = 0; i < n; i++) _waterLevel[i] = float.NegativeInfinity;

            // Горы — граница мира: непроходимые клетки по краю.
            PlaceMountains();
            // System.Random, а не UnityEngine.Random: свой генератор с сидом не сбивает глобальный.
            // Вода — первой, чтобы препятствия её обходили; рельеф под водой — чаша.
            PlaceWater(new System.Random(_obstacleSeed * 13 + 5));
            BuildHeights();
            var rng = new System.Random(_obstacleSeed);
            PlaceObstacles(rng, ObstacleType.Block, _blockCount);
            PlaceObstacles(rng, ObstacleType.Pillar, _pillarCount);
            PlaceObstacles(rng, ObstacleType.LowWall, _lowWallCount);
            PlaceGroves(new System.Random(_obstacleSeed * 23 + 11));
            var features = new System.Random(_obstacleSeed * 17 + 3);
            PlaceBurrows(features);
            PlaceSunStones(features);
            PlaceNest(features);
            // Траву раскладываем отдельным генератором: изменение зарослей не сдвигает препятствия.
            PlaceCover(new System.Random(_obstacleSeed * 7 + 13));

            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    _nearObstacle[y * _width + x] = HasObstacleNear(x, y, 1);
        }

        // ---------------- Клетки ----------------

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;
        }

        /// <summary>Клетка внутри поля с отступом margin клеток от его края.</summary>
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

        /// <summary>Гора — граница мира, пройти нельзя.</summary>
        public bool IsMountain(Vector2Int cell)
        {
            return !IsInside(cell) || _obstacles[ToIndex(cell)] == ObstacleType.Mountain;
        }

        /// <summary>Клетка озера или пруда: змея плывёт медленнее, наземный хищник сюда не заходит.</summary>
        public bool IsWater(Vector2Int cell)
        {
            return IsInside(cell) && _water[ToIndex(cell)];
        }

        public bool IsWaterAt(Vector3 world)
        {
            return IsWater(WorldToCell(world));
        }

        /// <summary>Плоский солнечный камень: днём на нём змея быстро греется.</summary>
        public bool IsSunStone(Vector2Int cell)
        {
            return IsInside(cell) && _sunStone[ToIndex(cell)];
        }

        public bool IsSunStoneAt(Vector3 world)
        {
            return IsSunStone(WorldToCell(world));
        }

        /// <summary>Клетка рощи (деревья ставятся здесь; BoardView рисует на ней лесную подстилку).</summary>
        public bool IsForest(Vector2Int cell)
        {
            return IsInside(cell) && _forest[ToIndex(cell)];
        }

        /// <summary>Под кронами: тень (прохладнее) и укрытие от орла.</summary>
        public bool IsCanopy(Vector2Int cell)
        {
            return IsInside(cell) && _canopy[ToIndex(cell)];
        }

        public bool IsCanopyAt(Vector3 world)
        {
            return IsCanopy(WorldToCell(world));
        }

        /// <summary>Есть ли в мире гнездо (место для кладки — главная цель).</summary>
        public bool HasNest => _hasNest;

        public Vector2Int NestCell => _nest;

        /// <summary>Центр гнезда в мире (на земле).</summary>
        public Vector3 NestPosition => CellToWorld(_nest);

        public bool IsBurrowCell(Vector2Int cell)
        {
            return IsInside(cell) && _burrowCell[ToIndex(cell)];
        }

        public int BurrowCount => _placedBurrows;

        /// <summary>Центр норы i в мире (на земле).</summary>
        public Vector3 GetBurrow(int index)
        {
            return CellToWorld(_burrows[index]);
        }

        public Vector2Int GetBurrowCell(int index) => _burrows[index];

        public int SunStoneCount => _placedSunStones;

        /// <summary>Левая нижняя клетка солнечного камня 2x2.</summary>
        public Vector2Int GetSunStone(int index) => _sunStones[index];

        /// <summary>Ближайшая нора к точке; -1, если нор нет.</summary>
        public int NearestBurrow(Vector3 world, out float distance)
        {
            int best = -1;
            distance = float.MaxValue;
            for (int i = 0; i < _placedBurrows; i++)
            {
                Vector3 d = GetBurrow(i) - world;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist >= distance) continue;
                distance = dist;
                best = i;
            }
            return best;
        }

        // ---------------- Вода ----------------

        /// <summary>Сколько водоёмов (озеро — нулевой).</summary>
        public int PondCount => _ponds;

        /// <summary>Центр водоёма на уровне воды.</summary>
        public Vector3 GetPondCenter(int index)
        {
            Vector2 c = _pondCenters[index];
            Vector3 p = CornerOrigin + new Vector3((c.x + 0.5f) * _cellSize, 0f, (c.y + 0.5f) * _cellSize);
            p.y = _pondLevels[index];
            return p;
        }

        /// <summary>Уровень воды водоёма в мире.</summary>
        public float GetPondLevel(int index) => _pondLevels[index];

        /// <summary>Радиус берега водоёма в направлении angle (рад, от оси X к оси Z), мировых единиц.</summary>
        public float PondEdgeWorld(int index, float angle) => PondEdge(index, angle) * _cellSize;

        private float PondEdge(int index, float angle)
        {
            float phase = _pondPhases[index];
            return _pondRadii[index] * (1f + 0.22f * Mathf.Sin(angle * 2f + phase) + 0.1f * Mathf.Sin(angle * 5f + phase * 2f));
        }

        public int WaterCellCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _water.Length; i++)
                    if (_water[i]) count++;
                return count;
            }
        }

        // ---------------- Рельеф ----------------

        /// <summary>Высота земли в точке (билинейно по карте высот); за пределами поля — по форме гор.</summary>
        public float GroundHeight(Vector3 world)
        {
            if (_heights == null) return Center.y;
            Vector3 o = CornerOrigin;
            float u = (world.x - o.x) / _cellSize;
            float v = (world.z - o.z) / _cellSize;
            if (u < 0f || v < 0f || u > _width || v > _height) return Center.y + TerrainShape.Height(world.x, world.z, Center);
            int x0 = Mathf.Min((int)u, _width - 1);
            int y0 = Mathf.Min((int)v, _height - 1);
            float fx = u - x0;
            float fy = v - y0;
            int stride = _width + 1;
            float h00 = _heights[y0 * stride + x0];
            float h10 = _heights[y0 * stride + x0 + 1];
            float h01 = _heights[(y0 + 1) * stride + x0];
            float h11 = _heights[(y0 + 1) * stride + x0 + 1];
            return Mathf.Lerp(Mathf.Lerp(h00, h10, fx), Mathf.Lerp(h01, h11, fx), fy);
        }

        /// <summary>Уровень воды в точке; -∞, если воды нет.</summary>
        public float WaterLevelAt(Vector3 world)
        {
            Vector2Int c = WorldToCell(world);
            return IsInside(c) ? _waterLevel[ToIndex(c)] : float.NegativeInfinity;
        }

        /// <summary>По чему идёт тот, кто плывёт: над водой — её уровень, иначе — земля.</summary>
        public float SurfaceHeight(Vector3 world)
        {
            return Mathf.Max(GroundHeight(world), WaterLevelAt(world));
        }

        /// <summary>Высота угла сетки (вершина меша земли): x, y — индексы углов 0..Width, 0..Height.</summary>
        public float GetCornerHeight(int x, int y) => _heights[y * (_width + 1) + x];

        /// <summary>Мировая точка угла (0, 0) сетки.</summary>
        public Vector3 CornerOrigin => transform.position - new Vector3(_width * 0.5f * _cellSize, 0f, _height * 0.5f * _cellSize);

        // ---------------- Препятствия ----------------

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
                case ObstacleType.Mountain: return MOUNTAIN_HEIGHT;
                default: return 0f;
            }
        }

        /// <summary>Половина ширины препятствия в мировых единицах (пень и ствол тоньше клетки).</summary>
        public float GetObstacleHalfSize(Vector2Int cell)
        {
            float half = _obstacles[ToIndex(cell)] == ObstacleType.Pillar ? PILLAR_HALF_SIZE : 0.5f;
            return half * _cellSize;
        }

        // ---------------- Появление ----------------

        /// <summary>
        /// Случайная клетка для добычи: на равнине, не вплотную к препятствию, не в воде и не занятая.
        /// Сначала пробуем случайные клетки, и только если не повезло — полный перебор. Без аллокаций.
        /// </summary>
        public bool TryGetRandomFreeCell(out Vector2Int cell)
        {
            int n = _occupied.Length;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                int i = Random.Range(0, n);
                if (!IsSpawnable(i)) continue;
                cell = new Vector2Int(i % _width, i / _width);
                return true;
            }

            int free = 0;
            for (int i = 0; i < n; i++)
                if (IsSpawnable(i)) free++;
            if (free > 0)
            {
                int k = Random.Range(0, free);
                for (int i = 0; i < n; i++)
                {
                    if (!IsSpawnable(i)) continue;
                    if (k-- > 0) continue;
                    cell = new Vector2Int(i % _width, i / _width);
                    return true;
                }
            }

            cell = default;
            return false;
        }

        /// <summary>Случайная свободная клетка у самой воды (для лягушек); если воды нет — любая свободная.</summary>
        public bool TryGetRandomShoreCell(out Vector2Int cell)
        {
            if (_ponds == 0) return TryGetRandomFreeCell(out cell);
            // Случайная точка на берегу случайного водоёма, чуть дальше кромки воды.
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int p = Random.Range(0, _ponds);
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = PondEdge(p, a) + Random.Range(0.8f, 2.5f);
                Vector2 c = _pondCenters[p];
                var candidate = new Vector2Int(Mathf.RoundToInt(c.x + Mathf.Cos(a) * r), Mathf.RoundToInt(c.y + Mathf.Sin(a) * r));
                if (!IsInside(candidate)) continue;
                int i = ToIndex(candidate);
                if (!IsSpawnable(i)) continue;
                cell = candidate;
                return true;
            }
            return TryGetRandomFreeCell(out cell);
        }

        // ---------------- Мир ↔ клетки ----------------

        /// <summary>Центр клетки на земле.</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            float x = (cell.x - (_width - 1) * 0.5f) * _cellSize;
            float z = (cell.y - (_height - 1) * 0.5f) * _cellSize;
            Vector3 p = transform.position + new Vector3(x, 0f, z);
            if (_heights != null && IsInside(cell))
            {
                int stride = _width + 1;
                int i = cell.y * stride + cell.x;
                p.y = (_heights[i] + _heights[i + 1] + _heights[i + stride] + _heights[i + stride + 1]) * 0.25f;
            }
            return p;
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
            if (_occupied[index] || _nearObstacle[index] || _water[index] || _burrowCell[index]) return false;
            if (_obstacles[index] != ObstacleType.None) return false;
            if (_hasNest && index == _nest.y * _width + _nest.x) return false;
            return IsCellInArena(new Vector2Int(index % _width, index / _width), _itemBorderMargin);
        }

        // ---------------- Генерация ----------------

        /// <summary>Клетки, ушедшие в горы, и рамка по краю поля — непроходимы.</summary>
        private void PlaceMountains()
        {
            Vector3 c = Center;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    bool border = x < 2 || y < 2 || x >= _width - 2 || y >= _height - 2;
                    Vector3 p = CellCenterFlat(x, y);
                    if (border || TerrainShape.MountainFactor(p.x, p.z, c) > _mountainThreshold)
                        _obstacles[y * _width + x] = ObstacleType.Mountain;
                }
            }
        }

        private Vector3 CellCenterFlat(float x, float y)
        {
            return transform.position + new Vector3((x - (_width - 1) * 0.5f) * _cellSize, 0f, (y - (_height - 1) * 0.5f) * _cellSize);
        }

        /// <summary>Озеро и пруды — «кляксы» на равнине, не у старта и не друг на друге.</summary>
        private void PlaceWater(System.Random rng)
        {
            _ponds = 0;
            float cx = (_width - 1) * 0.5f;
            float cy = (_height - 1) * 0.5f;
            float play = TerrainShape.PlayRadius / _cellSize;
            int total = Mathf.Min(MAX_PONDS, _pondCount + (_lakeRadius > 0f ? 1 : 0));
            for (int attempt = 0; attempt < 200 && _ponds < total; attempt++)
            {
                bool lake = _ponds == 0 && _lakeRadius > 0f;
                float radius = lake ? _lakeRadius : Mathf.Lerp(_pondRadius.x, _pondRadius.y, (float)rng.NextDouble());
                float angle = (float)rng.NextDouble() * 6.283f;
                float dist = lake ? play * 0.55f : Mathf.Lerp(_startClearRadius + radius + 6f, play - radius - 10f, (float)rng.NextDouble());
                float px = cx + Mathf.Cos(angle) * dist;
                float py = cy + Mathf.Sin(angle) * dist;
                bool free = true;
                for (int i = 0; i < _ponds && free; i++)
                {
                    float gap = _pondRadii[i] + radius + 8f;
                    free = (_pondCenters[i] - new Vector2(px, py)).sqrMagnitude > gap * gap;
                }
                if (!free) continue;

                int index = _ponds++;
                _pondCenters[index] = new Vector2(px, py);
                _pondRadii[index] = radius;
                _pondPhases[index] = (float)rng.NextDouble() * 6.283f;
                _pondLevels[index] = ShoreLevel(index);
                int r = Mathf.CeilToInt(radius * 1.4f);
                for (int y = Mathf.FloorToInt(py) - r; y <= Mathf.CeilToInt(py) + r; y++)
                {
                    for (int x = Mathf.FloorToInt(px) - r; x <= Mathf.CeilToInt(px) + r; x++)
                    {
                        if (x < 3 || x >= _width - 3 || y < 3 || y >= _height - 3) continue;
                        int i = y * _width + x;
                        if (_obstacles[i] != ObstacleType.None) continue;
                        float dx = x - px;
                        float dy = y - py;
                        float edge = PondEdge(index, Mathf.Atan2(dy, dx));
                        if (dx * dx + dy * dy > edge * edge) continue;
                        _water[i] = true;
                        _waterLevel[i] = _pondLevels[index];
                    }
                }
            }
        }

        /// <summary>Уровень воды — чуть ниже самой низкой точки берега, чтобы вода не «висела» над склоном.</summary>
        private float ShoreLevel(int index)
        {
            Vector2 c = _pondCenters[index];
            float min = float.MaxValue;
            for (int k = 0; k < 32; k++)
            {
                float a = k * Mathf.PI * 2f / 32f;
                float r = PondEdge(index, a) * 1.25f;
                Vector3 p = CellCenterFlat(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r);
                min = Mathf.Min(min, Center.y + TerrainShape.Height(p.x, p.z, Center));
            }
            return min - 0.3f;
        }

        /// <summary>Карта высот в углах клеток: холмы и горы по TerrainShape, под водой — чаши.</summary>
        private void BuildHeights()
        {
            int stride = _width + 1;
            _heights = new float[stride * (_height + 1)];
            Vector3 o = CornerOrigin;
            Vector3 c = Center;
            for (int y = 0; y <= _height; y++)
            {
                for (int x = 0; x <= _width; x++)
                {
                    float wx = o.x + x * _cellSize;
                    float wz = o.z + y * _cellSize;
                    float h = c.y + TerrainShape.Height(wx, wz, c);
                    for (int p = 0; p < _ponds; p++)
                    {
                        // Угол сетки в координатах клеток (центры клеток — целые числа).
                        float dx = x - 0.5f - _pondCenters[p].x;
                        float dy = y - 0.5f - _pondCenters[p].y;
                        float edge = PondEdge(p, Mathf.Atan2(dy, dx)) * 1.08f;
                        float d2 = (dx * dx + dy * dy) / (edge * edge);
                        if (d2 >= 1f) continue;
                        float depth = Mathf.Min(1.4f, 0.22f * _pondRadii[p]);
                        h = Mathf.Min(h, _pondLevels[p] - 0.12f - depth * (1f - d2));
                    }
                    _heights[y * stride + x] = h;
                }
            }
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
        /// Рощи: «кляксы» на равнине подальше от старта. Внутри — деревья (препятствие-колонна) с зазором в клетку,
        /// а вокруг каждого ствола — крона: тень и укрытие от орла.
        /// </summary>
        private void PlaceGroves(System.Random rng)
        {
            float cx = (_width - 1) * 0.5f;
            float cy = (_height - 1) * 0.5f;
            float play = TerrainShape.PlayRadius / _cellSize;
            float clear = _startClearRadius + _groveRadius * 0.6f;
            for (int g = 0; g < _groveCount; g++)
            {
                float angle = (float)rng.NextDouble() * 6.283f;
                float dist = Mathf.Lerp(clear, play - _groveRadius * 0.5f, Mathf.Sqrt((float)rng.NextDouble()));
                float px = cx + Mathf.Cos(angle) * dist;
                float py = cy + Mathf.Sin(angle) * dist;
                float radius = _groveRadius * (0.8f + (float)rng.NextDouble() * 0.4f);
                float phase = (float)rng.NextDouble() * 6.283f;
                int r = Mathf.CeilToInt(radius * 1.35f);
                for (int y = Mathf.FloorToInt(py) - r; y <= Mathf.CeilToInt(py) + r; y++)
                {
                    for (int x = Mathf.FloorToInt(px) - r; x <= Mathf.CeilToInt(px) + r; x++)
                    {
                        if (x < 2 || y < 2 || x >= _width - 2 || y >= _height - 2) continue;
                        int i = y * _width + x;
                        if (_water[i] || _obstacles[i] == ObstacleType.Mountain) continue;
                        float dx = x - px, dy = y - py;
                        float edge = radius * (1f + 0.25f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + phase));
                        if (dx * dx + dy * dy <= edge * edge) _forest[i] = true;
                    }
                }
            }

            // Деревья: случайные клетки рощи с зазором от других препятствий.
            for (int y = 2; y < _height - 2; y++)
            {
                for (int x = 2; x < _width - 2; x++)
                {
                    int i = y * _width + x;
                    if (!_forest[i] || _obstacles[i] != ObstacleType.None) continue;
                    if (rng.NextDouble() > _treeDensity) continue;
                    if (!CanPlace(x, y, 1, 1)) continue;
                    _obstacles[i] = ObstacleType.Pillar;
                    for (int dy = -_canopyRadius; dy <= _canopyRadius; dy++)
                    {
                        for (int dx = -_canopyRadius; dx <= _canopyRadius; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= _width || ny >= _height) continue;
                            if (dx * dx + dy * dy > _canopyRadius * _canopyRadius + 1) continue;
                            _canopy[ny * _width + nx] = true;
                        }
                    }
                }
            }
        }

        /// <summary>Гнездо — открытое место в глубине мира, не у старта и не у самых гор.</summary>
        private void PlaceNest(System.Random rng)
        {
            _hasNest = false;
            float cx = (_width - 1) * 0.5f;
            float cy = (_height - 1) * 0.5f;
            float play = TerrainShape.PlayRadius / _cellSize;
            for (int attempt = 0; attempt < 600 && !_hasNest; attempt++)
            {
                float angle = (float)rng.NextDouble() * 6.283f;
                float dist = Mathf.Lerp(play * 0.45f, play * 0.75f, (float)rng.NextDouble());
                int x = Mathf.RoundToInt(cx + Mathf.Cos(angle) * dist);
                int y = Mathf.RoundToInt(cy + Mathf.Sin(angle) * dist);
                if (!IsOpenArea(x, y, 1, 2)) continue;
                _nest = new Vector2Int(x, y);
                _hasNest = true;
            }
        }

        /// <summary>Норы: открытые клетки подальше от старта, друг от друга и от воды, с проходом вокруг.</summary>
        private void PlaceBurrows(System.Random rng)
        {
            _placedBurrows = 0;
            float cx = (_width - 1) * 0.5f;
            float cy = (_height - 1) * 0.5f;
            int target = Mathf.Min(_burrowCount, MAX_BURROWS);
            for (int attempt = 0; attempt < 2000 && _placedBurrows < target; attempt++)
            {
                int x = rng.Next(4, _width - 4);
                int y = rng.Next(4, _height - 4);
                float sx = x - cx, sy = y - cy;
                if (sx * sx + sy * sy < _startClearRadius * _startClearRadius) continue;
                if (!IsOpenArea(x, y, 1, 2)) continue;
                bool farEnough = true;
                for (int i = 0; i < _placedBurrows && farEnough; i++)
                {
                    float bx = _burrows[i].x - x, by = _burrows[i].y - y;
                    farEnough = bx * bx + by * by >= _burrowSpacing * _burrowSpacing;
                }
                if (!farEnough) continue;
                _burrows[_placedBurrows++] = new Vector2Int(x, y);
                _burrowCell[y * _width + x] = true;
            }
        }

        /// <summary>Солнечные камни 2x2 на открытых местах.</summary>
        private void PlaceSunStones(System.Random rng)
        {
            _placedSunStones = 0;
            int target = Mathf.Min(_sunStoneCount, _sunStones.Length);
            for (int attempt = 0; attempt < 3000 && _placedSunStones < target; attempt++)
            {
                int x = rng.Next(3, _width - 4);
                int y = rng.Next(3, _height - 4);
                if (!IsOpenArea(x, y, 2, 1)) continue;
                _sunStones[_placedSunStones++] = new Vector2Int(x, y);
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                        _sunStone[(y + dy) * _width + x + dx] = true;
            }
        }

        /// <summary>Квадрат size×size от (x, y) и кольцо margin клеток вокруг: без препятствий, воды, нор и камней.</summary>
        private bool IsOpenArea(int x, int y, int size, int margin)
        {
            for (int yy = y - margin; yy < y + size + margin; yy++)
            {
                for (int xx = x - margin; xx < x + size + margin; xx++)
                {
                    if (xx < 1 || yy < 1 || xx >= _width - 1 || yy >= _height - 1) return false;
                    int i = yy * _width + xx;
                    if (_obstacles[i] != ObstacleType.None || _water[i] || _burrowCell[i] || _sunStone[i]) return false;
                }
            }
            return true;
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
                        if (_obstacles[i] != ObstacleType.None || _water[i] || _sunStone[i] || _burrowCell[i]) continue;
                        if (_hasNest && x == _nest.x && y == _nest.y) continue;
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

        /// <summary>Не рядом со стартом и с зазором в клетку от других препятствий и воды, чтобы не было тупиков.</summary>
        private bool CanPlace(int ox, int oy, int sx, int sy)
        {
            float cx = (_width - 1) * 0.5f;
            float cy = (_height - 1) * 0.5f;
            float clearSqr = _startClearRadius * _startClearRadius;

            for (int y = oy - 1; y <= oy + sy; y++)
            {
                for (int x = ox - 1; x <= ox + sx; x++)
                {
                    if (x < 0 || x >= _width || y < 0 || y >= _height) return false;
                    int i = y * _width + x;
                    if (_obstacles[i] != ObstacleType.None || _water[i]) return false;
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
