using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Логика мира: размеры, рельеф (карта высот по TerrainShape — холмы внутри, горы по краю как граница),
    /// препятствия, рощи (деревья с кронами — тень и укрытие от орла; часть рощ — берёзовые), заросли высокой травы
    /// и камыша (укрытия), озеро и пруды со своим уровнем воды, река с гор в озеро (уровень падает вниз по течению,
    /// через неё лежат брёвна-мосты), болото в пойме с бочагами, каменистая гряда, полые брёвна-туннели, птичьи гнёзда,
    /// солнечные камни, норы, гнездо (главная цель), клетки для появления добычи и перевод клетка &lt;-&gt; мир.
    /// Раскладка детерминирована сидом. Визуал строит BoardView.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        private const float PILLAR_HALF_SIZE = 0.38f;
        private const float MOUNTAIN_HEIGHT = 60f;
        private const int MAX_BURROWS = 12;
        private const int MAX_PONDS = 6;
        private const int MAX_RIVER_POINTS = 512;
        private const int MAX_BRIDGES = 6;
        private const int MAX_HOLLOW_LOGS = 24;
        private const int MAX_BIRD_NESTS = 16;

        /// <summary>Чья вода в клетке: пруда, реки или болотного бочага.</summary>
        private const byte WATER_POND = 1;
        private const byte WATER_RIVER = 2;
        private const byte WATER_SWAMP = 3;

        private const byte BIOME_SWAMP = 1;
        private const byte BIOME_RIDGE = 2;

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

        [Tooltip("Доля рощ, которые растут березняком")]
        [SerializeField, Range(0f, 1f)] private float _birchShare = 0.35f;

        [Header("Вода")]
        [Tooltip("Радиус большого озера, клеток (0 — без озера)")]
        [SerializeField, Min(0f)] private float _lakeRadius = 11f;

        [Tooltip("Сколько небольших прудов")]
        [SerializeField, Range(0, MAX_PONDS - 1)] private int _pondCount = 4;

        [Tooltip("Радиус пруда, клеток (мин, макс)")]
        [SerializeField] private Vector2 _pondRadius = new Vector2(3.5f, 5f);

        [Header("Река")]
        [Tooltip("Река с гор через равнину в озеро")]
        [SerializeField] private bool _river = true;

        [Tooltip("Полуширина русла у истока и у устья, клеток")]
        [SerializeField] private Vector2 _riverHalfWidth = new Vector2(0.9f, 1.8f);

        [Tooltip("Глубина русла под уровнем воды, мировых единиц")]
        [SerializeField, Min(0.2f)] private float _riverDepth = 0.9f;

        [Tooltip("Сколько брёвен-мостов лежит через реку")]
        [SerializeField, Range(0, MAX_BRIDGES)] private int _bridgeCount = 3;

        [Header("Болото")]
        [Tooltip("Радиус болота в пойме реки, клеток (0 — без болота)")]
        [SerializeField, Min(0f)] private float _swampRadius = 13f;

        [Tooltip("Порог шума для бочагов: меньше — больше открытой воды")]
        [SerializeField, Range(0.3f, 0.9f)] private float _swampPoolThreshold = 0.57f;

        [Tooltip("Сколько сухих деревьев стоит в болоте")]
        [SerializeField, Min(0)] private int _swampTreeCount = 10;

        [Header("Каменистая гряда")]
        [Tooltip("Радиус гряды, клеток (0 — без гряды)")]
        [SerializeField, Min(0f)] private float _ridgeRadius = 11f;

        [Tooltip("Насколько гряда поднимается над равниной, мировых единиц")]
        [SerializeField, Min(0f)] private float _ridgeLift = 1.8f;

        [Tooltip("Валунов на гряде")]
        [SerializeField, Min(0)] private int _ridgeBoulders = 10;

        [Tooltip("Стоячих камней на гряде")]
        [SerializeField, Min(0)] private int _ridgeStones = 9;

        [Header("Полые брёвна и птичьи гнёзда")]
        [Tooltip("Сколько полых брёвен-туннелей: внутри змею не видят ни орёл, ни лиса")]
        [SerializeField, Range(0, MAX_HOLLOW_LOGS)] private int _hollowLogCount = 14;

        [Tooltip("Сколько птичьих гнёзд с яйцами в траве и по опушкам")]
        [SerializeField, Range(0, MAX_BIRD_NESTS)] private int _birdNestCount = 8;

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
        private bool[] _birch;
        private bool[] _reed;
        private bool[] _hollow;
        private bool[] _bridge;
        private bool[] _birdNestCell;
        private byte[] _waterKind;
        private byte[] _biome;
        private float[] _shoreDist;
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

        private readonly Vector2[] _riverPts = new Vector2[MAX_RIVER_POINTS];
        private readonly float[] _riverLevels = new float[MAX_RIVER_POINTS];
        private readonly float[] _riverHalf = new float[MAX_RIVER_POINTS];
        private int _riverCount;
        private float _riverReach;

        private readonly Vector3[] _bridgeA = new Vector3[MAX_BRIDGES];
        private readonly Vector3[] _bridgeB = new Vector3[MAX_BRIDGES];
        private int _bridges;

        private readonly Vector2Int[] _hollowStart = new Vector2Int[MAX_HOLLOW_LOGS];
        private readonly bool[] _hollowAlongX = new bool[MAX_HOLLOW_LOGS];
        private readonly int[] _hollowLength = new int[MAX_HOLLOW_LOGS];
        private int _hollowLogs;

        private readonly Vector2Int[] _birdNests = new Vector2Int[MAX_BIRD_NESTS];
        private int _placedBirdNests;

        private Vector2 _swampCenter;
        private float _swampPhase;
        private float _swampBase;
        private bool _hasSwamp;
        private Vector2 _ridgeCenter;
        private float _ridgePhase;
        private bool _hasRidge;

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
                    h = h * 31 + _birchShare.GetHashCode();
                    h = h * 31 + (_river ? 1 : 0);
                    h = h * 31 + _riverHalfWidth.GetHashCode();
                    h = h * 31 + _riverDepth.GetHashCode();
                    h = h * 31 + _bridgeCount;
                    h = h * 31 + _swampRadius.GetHashCode();
                    h = h * 31 + _swampPoolThreshold.GetHashCode();
                    h = h * 31 + _swampTreeCount;
                    h = h * 31 + _ridgeRadius.GetHashCode();
                    h = h * 31 + _ridgeLift.GetHashCode();
                    h = h * 31 + _ridgeBoulders;
                    h = h * 31 + _ridgeStones;
                    h = h * 31 + _hollowLogCount;
                    h = h * 31 + _birdNestCount;
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
            _birch = new bool[n];
            _reed = new bool[n];
            _hollow = new bool[n];
            _bridge = new bool[n];
            _birdNestCell = new bool[n];
            _waterKind = new byte[n];
            _biome = new byte[n];
            _shoreDist = new float[n];
            for (int i = 0; i < n; i++) _waterLevel[i] = float.NegativeInfinity;

            // Горы — граница мира: непроходимые клетки по краю.
            PlaceMountains();
            // System.Random, а не UnityEngine.Random: свой генератор с сидом не сбивает глобальный.
            // Вода — первой, чтобы препятствия её обходили; рельеф под водой — чаша, под рекой — русло.
            PlaceWater(new System.Random(_obstacleSeed * 13 + 5));
            PlanRiver(new System.Random(_obstacleSeed * 29 + 7));
            PlanSwampAndRidge(new System.Random(_obstacleSeed * 31 + 17));
            BuildHeights();
            MarkRiverAndSwamp();
            PlaceBridges();
            PlaceRidgeRocks(new System.Random(_obstacleSeed * 37 + 5));
            var rng = new System.Random(_obstacleSeed);
            PlaceObstacles(rng, ObstacleType.Block, _blockCount);
            PlaceObstacles(rng, ObstacleType.Pillar, _pillarCount);
            PlaceObstacles(rng, ObstacleType.LowWall, _lowWallCount);
            PlaceSwampTrees(new System.Random(_obstacleSeed * 47 + 3));
            PlaceGroves(new System.Random(_obstacleSeed * 23 + 11));
            PlaceHollowLogs(new System.Random(_obstacleSeed * 41 + 9));
            var features = new System.Random(_obstacleSeed * 17 + 3);
            PlaceBurrows(features);
            PlaceSunStones(features);
            PlaceNest(features);
            ComputeShoreDistance();
            // Траву раскладываем отдельным генератором: изменение зарослей не сдвигает препятствия.
            PlaceCover(new System.Random(_obstacleSeed * 7 + 13));
            PlaceReedsAndLogCover();
            PlaceBirdNests(new System.Random(_obstacleSeed * 43 + 21));

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

        /// <summary>Роща растёт березняком (белые стволы, светлая листва).</summary>
        public bool IsBirch(Vector2Int cell)
        {
            return IsInside(cell) && _birch[ToIndex(cell)];
        }

        /// <summary>Укрытие здесь — камыш у воды, а не трава.</summary>
        public bool IsReed(Vector2Int cell)
        {
            return IsInside(cell) && _reed[ToIndex(cell)];
        }

        /// <summary>Внутри полого бревна: змею не видят ни орёл, ни лиса.</summary>
        public bool IsHollow(Vector2Int cell)
        {
            return IsInside(cell) && _hollow[ToIndex(cell)];
        }

        public bool IsHollowAt(Vector3 world)
        {
            return IsHollow(WorldToCell(world));
        }

        /// <summary>Бревно-мост через реку: по нему ползут посуху.</summary>
        public bool IsBridge(Vector2Int cell)
        {
            return IsInside(cell) && _bridge[ToIndex(cell)];
        }

        /// <summary>Клетка болота.</summary>
        public bool IsSwamp(Vector2Int cell)
        {
            return IsInside(cell) && _biome[ToIndex(cell)] == BIOME_SWAMP;
        }

        /// <summary>Клетка каменистой гряды.</summary>
        public bool IsRidge(Vector2Int cell)
        {
            return IsInside(cell) && _biome[ToIndex(cell)] == BIOME_RIDGE;
        }

        /// <summary>Вода реки (течёт; там живёт рыба).</summary>
        public bool IsRiver(Vector2Int cell)
        {
            return IsInside(cell) && _waterKind[ToIndex(cell)] == WATER_RIVER;
        }

        /// <summary>Вода озера или пруда (у них своя круглая «чаша» и свой меш).</summary>
        public bool IsPondWater(Vector2Int cell)
        {
            return IsInside(cell) && _waterKind[ToIndex(cell)] == WATER_POND;
        }

        /// <summary>Насколько точка в болоте, 0..1 (для окраски земли).</summary>
        public float SwampWeight(Vector3 world)
        {
            Vector2 c = WorldToCellFloat(world);
            return SwampBlend(c.x, c.y);
        }

        /// <summary>Насколько точка на каменистой гряде, 0..1 (для окраски земли).</summary>
        public float RidgeWeight(Vector3 world)
        {
            Vector2 c = WorldToCellFloat(world);
            return RidgeBlend(c.x, c.y);
        }

        /// <summary>Расстояние до ближайшей воды, клеток (плавно между центрами клеток; вне поля — далеко).</summary>
        public float ShoreDistance(Vector3 world)
        {
            if (_shoreDist == null) return float.MaxValue;
            Vector2 c = WorldToCellFloat(world);
            int x0 = Mathf.FloorToInt(c.x);
            int y0 = Mathf.FloorToInt(c.y);
            if (x0 < 0 || y0 < 0 || x0 >= _width - 1 || y0 >= _height - 1) return float.MaxValue;
            float fx = c.x - x0, fy = c.y - y0;
            float a = _shoreDist[y0 * _width + x0], b = _shoreDist[y0 * _width + x0 + 1];
            float d = _shoreDist[(y0 + 1) * _width + x0], e = _shoreDist[(y0 + 1) * _width + x0 + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(d, e, fx), fy);
        }

        /// <summary>Точек на осевой линии реки (0 — реки нет).</summary>
        public int RiverPointCount => _riverCount;

        /// <summary>Точка осевой линии реки на уровне воды.</summary>
        public Vector3 GetRiverPoint(int index)
        {
            Vector2 c = _riverPts[index];
            Vector3 p = CornerOrigin + new Vector3((c.x + 0.5f) * _cellSize, 0f, (c.y + 0.5f) * _cellSize);
            p.y = _riverLevels[index];
            return p;
        }

        /// <summary>
        /// Координаты точки относительно реки: возвращает путь вдоль русла от истока (клеток), lateral — смещение
        /// поперёк (клеток, со знаком). Для UV воды: рябь бежит вниз по течению.
        /// </summary>
        public float RiverCoordinate(Vector3 world, out float lateral)
        {
            lateral = 0f;
            Vector2 p = WorldToCellFloat(world);
            float best = float.MaxValue, along = 0f;
            for (int i = 0; i + 1 < _riverCount; i++)
            {
                Vector2 a = _riverPts[i], ab = _riverPts[i + 1] - a;
                float len2 = Mathf.Max(1e-5f, ab.sqrMagnitude);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                Vector2 q = a + ab * t;
                float d = (p - q).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                // Точки русла идут через одну клетку — путь вдоль равен индексу.
                along = i + t;
                lateral = (ab.x * (p.y - a.y) - ab.y * (p.x - a.x)) / Mathf.Sqrt(len2);
            }
            return along;
        }

        /// <summary>Полуширина реки в точке, мировых единиц.</summary>
        public float GetRiverHalfWidth(int index) => _riverHalf[index] * _cellSize;

        public int BridgeCount => _bridges;

        /// <summary>Концы бревна-моста (на берегах, по верху бревна).</summary>
        public void GetBridge(int index, out Vector3 a, out Vector3 b)
        {
            a = _bridgeA[index];
            b = _bridgeB[index];
        }

        public int HollowLogCount => _hollowLogs;

        /// <summary>Центры крайних клеток полого бревна (на земле).</summary>
        public void GetHollowLog(int index, out Vector3 a, out Vector3 b)
        {
            Vector2Int s = _hollowStart[index];
            Vector2Int e = _hollowAlongX[index] ? new Vector2Int(s.x + _hollowLength[index] - 1, s.y) : new Vector2Int(s.x, s.y + _hollowLength[index] - 1);
            a = CellToWorld(s);
            b = CellToWorld(e);
        }

        public int BirdNestCount => _placedBirdNests;

        /// <summary>Птичье гнездо i в мире (на земле).</summary>
        public Vector3 GetBirdNest(int index) => CellToWorld(_birdNests[index]);

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

        /// <summary>
        /// Случайная клетка поглубже в воде (для рыбы): сама вода и четыре соседа — вода, не мост и не гора.
        /// Без аллокаций; если воды нет — false.
        /// </summary>
        public bool TryGetRandomWaterCell(out Vector2Int cell)
        {
            int n = _water.Length;
            for (int attempt = 0; attempt < 400; attempt++)
            {
                int i = Random.Range(0, n);
                if (!IsDeepWater(i)) continue;
                cell = new Vector2Int(i % _width, i / _width);
                return true;
            }
            cell = default;
            return false;
        }

        private bool IsDeepWater(int i)
        {
            if (!_water[i] || _occupied[i] || _obstacles[i] != ObstacleType.None) return false;
            int x = i % _width, y = i / _width;
            if (x < 1 || y < 1 || x >= _width - 1 || y >= _height - 1) return false;
            return _water[i - 1] && _water[i + 1] && _water[i - _width] && _water[i + _width];
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
            if (_birdNestCell[index] || _hollow[index]) return false;
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
                        _waterKind[i] = WATER_POND;
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
                    // Угол сетки в координатах клеток (центры клеток — целые числа).
                    float gx = x - 0.5f, gy = y - 0.5f;
                    h += RidgeLift(gx, gy);
                    h = SwampFlatten(h, gx, gy);
                    h = CarveRiver(h, gx, gy);
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
                bool birch = rng.NextDouble() < _birchShare;
                int r = Mathf.CeilToInt(radius * 1.35f);
                for (int y = Mathf.FloorToInt(py) - r; y <= Mathf.CeilToInt(py) + r; y++)
                {
                    for (int x = Mathf.FloorToInt(px) - r; x <= Mathf.CeilToInt(px) + r; x++)
                    {
                        if (x < 2 || y < 2 || x >= _width - 2 || y >= _height - 2) continue;
                        int i = y * _width + x;
                        if (_water[i] || _obstacles[i] == ObstacleType.Mountain || _biome[i] != 0) continue;
                        float dx = x - px, dy = y - py;
                        float edge = radius * (1f + 0.25f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + phase));
                        if (dx * dx + dy * dy > edge * edge) continue;
                        _forest[i] = true;
                        _birch[i] = birch;
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
            // Гряда — каменистое место: сначала несколько камней там.
            if (_hasRidge)
            {
                int ridgeTarget = Mathf.Min(target, 6);
                for (int attempt = 0; attempt < 400 && _placedSunStones < ridgeTarget; attempt++)
                {
                    int x = Mathf.RoundToInt(_ridgeCenter.x + ((float)rng.NextDouble() * 2f - 1f) * _ridgeRadius * 0.8f);
                    int y = Mathf.RoundToInt(_ridgeCenter.y + ((float)rng.NextDouble() * 2f - 1f) * _ridgeRadius * 0.8f);
                    if (x < 3 || y < 3 || x >= _width - 4 || y >= _height - 4) continue;
                    if (IsOpenArea(x, y, 2, 1)) AddSunStone(x, y);
                }
            }
            for (int attempt = 0; attempt < 3000 && _placedSunStones < target; attempt++)
            {
                int x = rng.Next(3, _width - 4);
                int y = rng.Next(3, _height - 4);
                if (IsOpenArea(x, y, 2, 1)) AddSunStone(x, y);
            }
        }

        private void AddSunStone(int x, int y)
        {
            _sunStones[_placedSunStones++] = new Vector2Int(x, y);
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                    _sunStone[(y + dy) * _width + x + dx] = true;
        }

        // ---------------- Река, болото, гряда ----------------

        private float CellHeight(float x, float y)
        {
            Vector3 p = CellCenterFlat(x, y);
            return Center.y + TerrainShape.Height(p.x, p.z, Center);
        }

        private Vector2 WorldToCellFloat(Vector3 world)
        {
            Vector3 local = world - transform.position;
            return new Vector2(local.x / _cellSize + (_width - 1) * 0.5f, local.z / _cellSize + (_height - 1) * 0.5f);
        }

        private static float Hash01(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>
        /// Русло: от подножия гор к озеру. Каждый шаг — в сторону озера, с плавным меандром и уклоном вниз по склону
        /// (вода ищет низину), в обход старта. Уровень воды вниз по течению не растёт, у устья равен уровню водоёма.
        /// </summary>
        private void PlanRiver(System.Random rng)
        {
            _riverCount = 0;
            _riverReach = 0f;
            if (!_river || _ponds == 0) return;
            var c = new Vector2((_width - 1) * 0.5f, (_height - 1) * 0.5f);
            Vector2 lake = _pondCenters[0];
            Vector2 toLake = lake - c;
            float lakeAngle = Mathf.Atan2(toLake.y, toLake.x);
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
            float a0 = lakeAngle + side * (1.1f + 0.5f * (float)rng.NextDouble());
            float startR = TerrainShape.PlayRadius / _cellSize + 1f;
            Vector2 pos = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * startR;
            Vector2 heading = (lake - pos).normalized;
            float phase = (float)rng.NextDouble() * 6.283f;
            float clear = _startClearRadius + 8f;
            int mouth = -1;
            for (int step = 0; step < MAX_RIVER_POINTS; step++)
            {
                _riverPts[_riverCount++] = pos;
                for (int p = 0; p < _ponds && mouth < 0; p++)
                {
                    Vector2 d = pos - _pondCenters[p];
                    if (d.magnitude < PondEdge(p, Mathf.Atan2(d.y, d.x)) * 0.8f) mouth = p;
                }
                if (mouth >= 0 || _riverCount >= MAX_RIVER_POINTS) break;

                Vector2 toTarget = (lake - pos).normalized;
                var perp = new Vector2(-toTarget.y, toTarget.x);
                var grad = new Vector2(CellHeight(pos.x + 1f, pos.y) - CellHeight(pos.x - 1f, pos.y),
                    CellHeight(pos.x, pos.y + 1f) - CellHeight(pos.x, pos.y - 1f)) * 0.5f;
                // После 250 шагов — прямо к озеру, чтобы река наверняка до него дошла.
                float wander = step < 250 ? 1f : 0f;
                Vector2 want = toTarget + (perp * (Mathf.Sin(step * 0.11f + phase) * 0.75f) - grad * 0.8f) * wander;
                Vector2 fromC = pos - c;
                float dc = fromC.magnitude;
                if (dc < clear && dc > 1e-3f) want += fromC / dc * 1.5f;
                heading = Vector2.Lerp(heading, want.normalized, 0.3f).normalized;
                pos += heading;
            }

            float level = float.MaxValue;
            float floor = mouth >= 0 ? _pondLevels[mouth] : float.NegativeInfinity;
            for (int i = 0; i < _riverCount; i++)
            {
                Vector2 p = _riverPts[i];
                level = Mathf.Min(level - 0.004f, CellHeight(p.x, p.y) - 0.45f);
                _riverLevels[i] = Mathf.Max(level, floor);
                float t = _riverCount > 1 ? (float)i / (_riverCount - 1) : 0f;
                _riverHalf[i] = Mathf.Lerp(_riverHalfWidth.x, _riverHalfWidth.y, t) * (0.85f + 0.3f * Mathf.PerlinNoise(i * 0.15f, 3.7f));
                _riverReach = Mathf.Max(_riverReach, _riverHalf[i] * 2.4f + 1f);
            }
        }

        /// <summary>Расстояние (клеток) от точки до осевой линии реки; level и half — уровень и полуширина в ближайшем месте.</summary>
        private float RiverQuery(float x, float y, out float level, out float half)
        {
            level = 0f;
            half = 0f;
            float best = float.MaxValue;
            float reach = _riverReach;
            for (int i = 0; i + 1 < _riverCount; i++)
            {
                Vector2 a = _riverPts[i], b = _riverPts[i + 1];
                if (x < Mathf.Min(a.x, b.x) - reach || x > Mathf.Max(a.x, b.x) + reach ||
                    y < Mathf.Min(a.y, b.y) - reach || y > Mathf.Max(a.y, b.y) + reach) continue;
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(((x - a.x) * ab.x + (y - a.y) * ab.y) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                float dx = a.x + ab.x * t - x, dy = a.y + ab.y * t - y;
                float d = dx * dx + dy * dy;
                if (d >= best) continue;
                best = d;
                level = Mathf.Lerp(_riverLevels[i], _riverLevels[i + 1], t);
                half = Mathf.Lerp(_riverHalf[i], _riverHalf[i + 1], t);
            }
            return best == float.MaxValue ? float.MaxValue : Mathf.Sqrt(best);
        }

        /// <summary>Русло — желоб под уровнем воды, берега — пологая долина, а не ступенька.</summary>
        private float CarveRiver(float h, float x, float y)
        {
            if (_riverCount < 2) return h;
            float d = RiverQuery(x, y, out float level, out float half);
            if (d == float.MaxValue) return h;
            float bed = half * 1.15f;
            if (d < bed)
            {
                float k = d / bed;
                return Mathf.Min(h, level - 0.12f - _riverDepth * (1f - k * k));
            }
            float valley = half * 2.4f;
            if (d >= valley) return h;
            // Берег плавно поднимается от кромки к природной высоте; на низкой стороне склона — невысокий вал,
            // чтобы вода не растекалась за русло.
            float s = (d - bed) / (valley - bed);
            return Mathf.Lerp(level + 0.22f, h, s * s * (3f - 2f * s));
        }

        /// <summary>Болото — в пойме сбоку от реки; гряда — подальше от воды, болота и старта.</summary>
        private void PlanSwampAndRidge(System.Random rng)
        {
            _hasSwamp = false;
            _hasRidge = false;
            var c = new Vector2((_width - 1) * 0.5f, (_height - 1) * 0.5f);
            float play = TerrainShape.PlayRadius / _cellSize;
            _swampPhase = (float)rng.NextDouble() * 6.283f;
            _ridgePhase = (float)rng.NextDouble() * 6.283f;

            if (_swampRadius > 0f && _riverCount > 8)
            {
                for (int attempt = 0; attempt < 32 && !_hasSwamp; attempt++)
                {
                    int i = Mathf.Clamp((int)(_riverCount * (0.35f + 0.4f * (float)rng.NextDouble())), 1, _riverCount - 2);
                    Vector2 dir = (_riverPts[i + 1] - _riverPts[i - 1]).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    Vector2 p = _riverPts[i] + perp * (_swampRadius * 0.45f);
                    float dist = (p - c).magnitude;
                    if (dist < _swampRadius + _startClearRadius + 4f || dist + _swampRadius > play - 2f) continue;
                    if (!FarFromPonds(p, _swampRadius * 0.6f)) continue;
                    _swampCenter = p;
                    _swampBase = _riverLevels[i] + 0.3f;
                    _hasSwamp = true;
                }
            }

            if (_ridgeRadius > 0f)
            {
                for (int attempt = 0; attempt < 300 && !_hasRidge; attempt++)
                {
                    float a = (float)rng.NextDouble() * 6.283f;
                    float dist = Mathf.Lerp(play * 0.3f, play * 0.72f, (float)rng.NextDouble());
                    Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
                    if (dist < _ridgeRadius + _startClearRadius + 4f || dist + _ridgeRadius > play - 3f) continue;
                    if (!FarFromPonds(p, _ridgeRadius + 6f)) continue;
                    if (_hasSwamp && (p - _swampCenter).magnitude < _swampRadius + _ridgeRadius + 6f) continue;
                    if (NearRiver(p, _ridgeRadius + 6f)) continue;
                    _ridgeCenter = p;
                    _hasRidge = true;
                }
            }
        }

        private bool FarFromPonds(Vector2 p, float gap)
        {
            for (int i = 0; i < _ponds; i++)
                if ((p - _pondCenters[i]).magnitude < _pondRadii[i] + gap) return false;
            return true;
        }

        private bool NearRiver(Vector2 p, float gap)
        {
            for (int i = 0; i < _riverCount; i++)
                if ((p - _riverPts[i]).sqrMagnitude < gap * gap) return true;
            return false;
        }

        private float SwampBlend(float x, float y)
        {
            if (!_hasSwamp) return 0f;
            float dx = x - _swampCenter.x, dy = y - _swampCenter.y;
            float edge = _swampRadius * (1f + 0.2f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + _swampPhase));
            float k = 1f - Mathf.Sqrt(dx * dx + dy * dy) / edge;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 2.5f));
        }

        private float RidgeBlend(float x, float y)
        {
            if (!_hasRidge) return 0f;
            float dx = x - _ridgeCenter.x, dy = y - _ridgeCenter.y;
            float edge = _ridgeRadius * (1f + 0.25f * Mathf.Sin(Mathf.Atan2(dy, dx) * 2f + _ridgePhase));
            float k = 1f - Mathf.Sqrt(dx * dx + dy * dy) / edge;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 2f));
        }

        private float PoolNoise(float x, float y) => Mathf.PerlinNoise(x * 0.23f + 71.3f, y * 0.23f + 12.9f);

        /// <summary>Гряда — бугристый каменный холм.</summary>
        private float RidgeLift(float x, float y)
        {
            if (!_hasRidge) return 0f;
            return _ridgeLift * RidgeBlend(x, y) * (0.6f + 0.8f * Mathf.PerlinNoise(x * 0.3f + 17f, y * 0.3f + 5f));
        }

        /// <summary>Болото — плоская низина чуть выше реки; в бочагах дно ниже воды.</summary>
        private float SwampFlatten(float h, float x, float y)
        {
            float w = SwampBlend(x, y);
            if (w <= 0f) return h;
            float flat = Mathf.Lerp(h, _swampBase, w);
            if (w > 0.5f) flat = Mathf.Max(flat, _swampBase - 0.05f);
            if (w > 0.55f && PoolNoise(x, y) > _swampPoolThreshold) flat = Mathf.Min(flat, _swampBase - 0.42f);
            return flat;
        }

        /// <summary>Вода реки и бочагов болота, области болота и гряды.</summary>
        private void MarkRiverAndSwamp()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int i = y * _width + x;
                    bool mountain = _obstacles[i] == ObstacleType.Mountain;
                    float sw = SwampBlend(x, y);
                    if (!mountain && sw > 0.4f) _biome[i] = BIOME_SWAMP;
                    else if (!mountain && RidgeBlend(x, y) > 0.4f) _biome[i] = BIOME_RIDGE;
                    if (_water[i] || x < 3 || y < 3 || x >= _width - 3 || y >= _height - 3) continue;

                    // Река течёт и по подножию гор — там её видно, хотя змея туда не доберётся.
                    if (_riverCount > 1 && RiverQuery(x, y, out float level, out float half) < half)
                    {
                        _water[i] = true;
                        _waterKind[i] = WATER_RIVER;
                        _waterLevel[i] = level;
                    }
                    else if (!mountain && sw > 0.55f && PoolNoise(x, y) > _swampPoolThreshold + 0.03f)
                    {
                        _water[i] = true;
                        _waterKind[i] = WATER_SWAMP;
                        _waterLevel[i] = _swampBase - 0.12f;
                    }
                }
            }
        }

        /// <summary>Брёвна-мосты поперёк реки: клетки под ними — суша на высоте бревна.</summary>
        private void PlaceBridges()
        {
            _bridges = 0;
            if (_riverCount < 20) return;
            int count = Mathf.Min(_bridgeCount, MAX_BRIDGES);
            for (int b = 0; b < count; b++)
            {
                int i = Mathf.Clamp((int)(_riverCount * (0.2f + 0.65f * (b + 0.5f) / count)), 1, _riverCount - 2);
                Vector2 p = _riverPts[i];
                // В болоте и у самых гор мост ни к чему.
                if (SwampBlend(p.x, p.y) > 0.3f || IsMountain(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)))) continue;
                Vector2 dir = (_riverPts[i + 1] - _riverPts[i - 1]).normalized;
                var n = new Vector2(-dir.y, dir.x);
                float halfLen = _riverHalf[i] * 1.3f + 1.6f;
                Vector2 a = p - n * halfLen, e = p + n * halfLen;
                float logTop = 0.42f * _cellSize;
                Vector3 wa = CellCenterFlat(a.x, a.y);
                wa.y = GroundHeight(wa) + logTop;
                Vector3 we = CellCenterFlat(e.x, e.y);
                we.y = GroundHeight(we) + logTop;
                _bridgeA[_bridges] = wa;
                _bridgeB[_bridges] = we;
                _bridges++;
                float step = 0.4f / (halfLen * 2f);
                for (float t = 0f; t <= 1f; t += step)
                {
                    Vector2 q = Vector2.Lerp(a, e, t);
                    int cx = Mathf.RoundToInt(q.x), cy = Mathf.RoundToInt(q.y);
                    if (cx < 1 || cy < 1 || cx >= _width - 1 || cy >= _height - 1) continue;
                    int k = cy * _width + cx;
                    _bridge[k] = true;
                    _water[k] = false;
                    _waterKind[k] = 0;
                    _waterLevel[k] = Mathf.Lerp(wa.y, we.y, t);
                }
            }
        }

        private void PlaceRidgeRocks(System.Random rng)
        {
            if (!_hasRidge) return;
            PlaceInRegion(rng, ObstacleType.Block, _ridgeBoulders, _ridgeCenter, _ridgeRadius * 0.9f);
            PlaceInRegion(rng, ObstacleType.Pillar, _ridgeStones, _ridgeCenter, _ridgeRadius);
        }

        /// <summary>Сухие деревья в болоте (колонны: BoardView рисует их голыми стволами).</summary>
        private void PlaceSwampTrees(System.Random rng)
        {
            if (_hasSwamp) PlaceInRegion(rng, ObstacleType.Pillar, _swampTreeCount, _swampCenter, _swampRadius * 0.85f);
        }

        private void PlaceInRegion(System.Random rng, ObstacleType type, int count, Vector2 center, float radius)
        {
            int size = type == ObstacleType.Block ? 2 : 1;
            int placed = 0;
            for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
            {
                float a = (float)rng.NextDouble() * 6.283f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                int ox = Mathf.RoundToInt(center.x + Mathf.Cos(a) * r);
                int oy = Mathf.RoundToInt(center.y + Mathf.Sin(a) * r);
                if (ox < 2 || oy < 2 || ox + size >= _width - 2 || oy + size >= _height - 2) continue;
                if (!CanPlace(ox, oy, size, size)) continue;
                for (int y = oy; y < oy + size; y++)
                    for (int x = ox; x < ox + size; x++)
                        _obstacles[y * _width + x] = type;
                placed++;
            }
        }

        /// <summary>Полые брёвна-туннели по 3 клетки: внутри — укрытие, по сторонам свободно.</summary>
        private void PlaceHollowLogs(System.Random rng)
        {
            _hollowLogs = 0;
            int target = Mathf.Min(_hollowLogCount, MAX_HOLLOW_LOGS);
            float cx = (_width - 1) * 0.5f, cy = (_height - 1) * 0.5f;
            float play = TerrainShape.PlayRadius / _cellSize - 5f;
            float clear = _startClearRadius + 3f;
            const int length = 3;
            for (int attempt = 0; attempt < 3000 && _hollowLogs < target; attempt++)
            {
                bool alongX = rng.Next(2) == 0;
                int ox = rng.Next(4, _width - 8), oy = rng.Next(4, _height - 8);
                float dx = ox - cx, dy = oy - cy;
                float r2 = dx * dx + dy * dy;
                if (r2 > play * play || r2 < clear * clear) continue;
                int sx = alongX ? length : 1, sy = alongX ? 1 : length;
                bool ok = true;
                for (int y = oy - 1; y <= oy + sy && ok; y++)
                {
                    for (int x = ox - 1; x <= ox + sx && ok; x++)
                    {
                        int i = y * _width + x;
                        ok = _obstacles[i] == ObstacleType.None && !_water[i] && !_hollow[i] && !_bridge[i] && _biome[i] != BIOME_RIDGE;
                    }
                }
                if (!ok) continue;
                for (int y = oy; y < oy + sy; y++)
                    for (int x = ox; x < ox + sx; x++)
                        _hollow[y * _width + x] = true;
                _hollowStart[_hollowLogs] = new Vector2Int(ox, oy);
                _hollowAlongX[_hollowLogs] = alongX;
                _hollowLength[_hollowLogs] = length;
                _hollowLogs++;
            }
        }

        /// <summary>Расстояние до воды в клетках: два прохода «фаски» — приближённо евклидово.</summary>
        private void ComputeShoreDistance()
        {
            const float Far = 999f;
            const float Diagonal = 1.414f;
            for (int i = 0; i < _shoreDist.Length; i++) _shoreDist[i] = _water[i] ? 0f : Far;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int i = y * _width + x;
                    float d = _shoreDist[i];
                    if (x > 0) d = Mathf.Min(d, _shoreDist[i - 1] + 1f);
                    if (y > 0)
                    {
                        d = Mathf.Min(d, _shoreDist[i - _width] + 1f);
                        if (x > 0) d = Mathf.Min(d, _shoreDist[i - _width - 1] + Diagonal);
                        if (x < _width - 1) d = Mathf.Min(d, _shoreDist[i - _width + 1] + Diagonal);
                    }
                    _shoreDist[i] = d;
                }
            }
            for (int y = _height - 1; y >= 0; y--)
            {
                for (int x = _width - 1; x >= 0; x--)
                {
                    int i = y * _width + x;
                    float d = _shoreDist[i];
                    if (x < _width - 1) d = Mathf.Min(d, _shoreDist[i + 1] + 1f);
                    if (y < _height - 1)
                    {
                        d = Mathf.Min(d, _shoreDist[i + _width] + 1f);
                        if (x < _width - 1) d = Mathf.Min(d, _shoreDist[i + _width + 1] + Diagonal);
                        if (x > 0) d = Mathf.Min(d, _shoreDist[i + _width - 1] + Diagonal);
                    }
                    _shoreDist[i] = d;
                }
            }
        }

        /// <summary>Камыш: полосой по берегам и пятнами по болоту — тоже укрытие. Полые брёвна прячут всегда.</summary>
        private void PlaceReedsAndLogCover()
        {
            for (int y = 1; y < _height - 1; y++)
            {
                for (int x = 1; x < _width - 1; x++)
                {
                    int i = y * _width + x;
                    if (_hollow[i])
                    {
                        _cover[i] = true;
                        continue;
                    }
                    if (_water[i] || _obstacles[i] != ObstacleType.None || _sunStone[i] || _burrowCell[i] || _bridge[i]) continue;
                    if (_hasNest && x == _nest.x && y == _nest.y) continue;
                    bool swamp = _biome[i] == BIOME_SWAMP && Mathf.PerlinNoise(x * 0.31f + 5.1f, y * 0.31f + 9.4f) > 0.56f;
                    bool bank = _shoreDist[i] <= 1.5f && Hash01(x, y) < 0.35f;
                    if (!swamp && !bank) continue;
                    _cover[i] = true;
                    _reed[i] = true;
                }
            }
        }

        /// <summary>Птичьи гнёзда — в траве или в роще, далеко друг от друга, от старта и от гнезда змеи.</summary>
        private void PlaceBirdNests(System.Random rng)
        {
            _placedBirdNests = 0;
            int target = Mathf.Min(_birdNestCount, MAX_BIRD_NESTS);
            float cx = (_width - 1) * 0.5f, cy = (_height - 1) * 0.5f;
            float play = TerrainShape.PlayRadius / _cellSize - 5f;
            float clear = _startClearRadius + 6f;
            for (int attempt = 0; attempt < 4000 && _placedBirdNests < target; attempt++)
            {
                int x = rng.Next(4, _width - 4), y = rng.Next(4, _height - 4);
                int i = y * _width + x;
                float dx = x - cx, dy = y - cy;
                float r2 = dx * dx + dy * dy;
                if (r2 > play * play || r2 < clear * clear) continue;
                bool hidden = (_cover[i] && !_reed[i]) || _forest[i];
                if (!hidden || _hollow[i] || _bridge[i] || _biome[i] == BIOME_SWAMP) continue;
                if (!IsOpenArea(x, y, 1, 1)) continue;
                var cell = new Vector2Int(x, y);
                if (_hasNest && (_nest - cell).sqrMagnitude < 100) continue;
                bool far = true;
                for (int k = 0; k < _placedBirdNests && far; k++) far = (_birdNests[k] - cell).sqrMagnitude >= 256;
                if (!far) continue;
                _birdNests[_placedBirdNests++] = cell;
                _birdNestCell[i] = true;
                // Маленькая полянка вокруг гнезда: само гнездо видно, а трава рядом — укрытие для подхода.
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int k = (y + oy) * _width + x + ox;
                        _cover[k] = false;
                        _reed[k] = false;
                    }
                }
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
                    if (_hollow[i] || _bridge[i]) return false;
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
                        // На голых камнях гряды трава не растёт.
                        if (_biome[i] == BIOME_RIDGE) continue;
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
