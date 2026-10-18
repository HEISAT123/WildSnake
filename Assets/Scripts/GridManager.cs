using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Логика мира: размеры, рельеф (карта высот по TerrainShape — холмы внутри, горы по краю как граница),
    /// препятствия, рощи (деревья с кронами — тень и укрытие от орла; часть рощ — берёзовые), заросли высокой травы
    /// и камыша (укрытия), озеро и пруды со своим уровнем воды, река с гор в озеро (уровень падает вниз по течению,
    /// через неё лежат брёвна-мосты), болото в пойме с бочагами, каменистая гряда, полые брёвна-туннели, птичьи гнёзда,
    /// солнечные камни, норы, гнездо (главная цель), заросли ежевики, мышиные городки с норками, тёплые ключи,
    /// осиные гнёзда на пнях, клетки для появления добычи и перевод клетка &lt;-&gt; мир.
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
        // Ниже этого просвета (клеток) под бревном не пролезть: у концов моста его переползают поверху.
        private const float MIN_BRIDGE_GAP = 0.9f;
        // Полуширина полосы клеток моста от оси бревна, клеток.
        private const float BRIDGE_HALF_WIDTH = 0.75f;
        private const int MAX_HOLLOW_LOGS = 24;
        private const int MAX_BIRD_NESTS = 16;
        private const int MAX_COLONIES = 6;
        private const int HOLES_PER_COLONY = 6;
        private const int MAX_HOT_SPRINGS = 4;
        private const int MAX_WASP_NESTS = 6;

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
        [SerializeField, Min(0)] private int _lowWallCount = 110;

        [Tooltip("Количество одиночных пней и стоячих камней вне рощ")]
        [SerializeField, Min(0)] private int _pillarCount = 75;

        [Tooltip("Количество валунов 2x2")]
        [SerializeField, Min(0)] private int _blockCount = 55;

        [Tooltip("Высота бревна, клеток. Должна быть меньше высоты прыжка змейки")]
        [SerializeField, Min(0.1f)] private float _lowWallHeight = 0.45f;

        [Tooltip("Высота пня или дерева для столкновений, клеток")]
        [SerializeField, Min(0.1f)] private float _pillarHeight = 2.5f;

        [Tooltip("Высота валуна, клеток")]
        [SerializeField, Min(0.1f)] private float _blockHeight = 1.6f;

        [Tooltip("Радиус свободной зоны вокруг точки старта, клеток")]
        [SerializeField, Min(2)] private int _startClearRadius = 6;

        [Header("Высокая трава")]
        [Tooltip("Сколько случайных зарослей высокой травы сверх троп из плана мира (на открытых полях их нет)")]
        [SerializeField, Min(0)] private int _coverPatchCount = 95;

        [Tooltip("Средний радиус зарослей, клеток")]
        [SerializeField, Min(0.5f)] private float _coverPatchRadius = 2.6f;

        [Header("Рощи")]
        [Tooltip("Сколько рощ из плана мира сажать")]
        [SerializeField, Range(0, 16)] private int _groveCount = 8;

        [Tooltip("Доля клеток рощи, на которых растёт дерево")]
        [SerializeField, Range(0f, 0.5f)] private float _treeDensity = 0.17f;

        [Tooltip("Насколько далеко от ствола тянется крона (тень, укрытие от орла), клеток")]
        [SerializeField, Min(0)] private int _canopyRadius = 2;

        [Header("Вода")]
        [Tooltip("Радиус большого озера, клеток (0 — без озера)")]
        [SerializeField, Min(0f)] private float _lakeRadius = 11f;

        [Tooltip("Сколько прудов из плана мира наполнить")]
        [SerializeField, Range(0, MAX_PONDS - 1)] private int _pondCount = 5;

        [Header("Река")]
        [Tooltip("Река с гор через равнину в озеро")]
        [SerializeField] private bool _river = true;

        [Tooltip("Полуширина русла у истока и у устья, клеток")]
        [SerializeField] private Vector2 _riverHalfWidth = new Vector2(0.9f, 1.8f);

        [Tooltip("Глубина русла под уровнем воды, мировых единиц")]
        [SerializeField, Min(0.2f)] private float _riverDepth = 0.9f;

        [Tooltip("Сколько брёвен-мостов лежит через реку")]
        [SerializeField, Range(0, MAX_BRIDGES)] private int _bridgeCount = 3;

        [Tooltip("Верх бревна-моста над водой, клеток: под ним проплывает змея")]
        [SerializeField, Min(0.6f)] private float _bridgeClearance = 1.3f;

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

        [Header("Ежевика, мышиные городки, тёплые ключи, осы")]
        [Tooltip("Сколько зарослей ежевики: укрытие, но колючки замедляют и царапают на бегу, а лиса туда не суётся")]
        [SerializeField, Min(0)] private int _brambleCount = 9;

        [Tooltip("Средний радиус зарослей ежевики, клеток")]
        [SerializeField, Min(0.5f)] private float _brambleRadius = 2.3f;

        [Tooltip("Сколько мышиных городков — полянок с норками, где живут мыши")]
        [SerializeField, Range(0, MAX_COLONIES)] private int _mouseColonyCount = 4;

        [Tooltip("Сколько тёплых ключей: парящая земля греет змею и днём, и ночью")]
        [SerializeField, Range(0, MAX_HOT_SPRINGS)] private int _hotSpringCount = 3;

        [Tooltip("Сколько осиных гнёзд на старых пнях")]
        [SerializeField, Range(0, MAX_WASP_NESTS)] private int _waspNestCount = 4;

        [Header("Солнечные камни")]
        [Tooltip("Сколько плоских камней, на которых змея греется")]
        [SerializeField, Min(0)] private int _sunStoneCount = 22;

        [Header("Норы")]
        [Tooltip("Сколько нор из плана мира вырыть")]
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
        // Уровень реки под бревном-мостом (-∞ — под бревном берег, там под ним ползут по земле).
        private float[] _underBridgeLevel;
        private bool[] _birdNestCell;
        private bool[] _bramble;
        private bool[] _hotSpring;
        private bool[] _colonyHole;
        // Места из плана мира: случайные камни и брёвна сюда не падают.
        private bool[] _reserved;
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

        private readonly Vector2Int[] _colonyCenters = new Vector2Int[MAX_COLONIES];
        private readonly Vector2Int[] _colonyHoles = new Vector2Int[MAX_COLONIES * HOLES_PER_COLONY];
        private int _colonies;
        private int _holes;

        private readonly Vector2Int[] _hotSprings = new Vector2Int[MAX_HOT_SPRINGS];
        private int _springs;

        private readonly Vector2Int[] _waspNests = new Vector2Int[MAX_WASP_NESTS];
        private int _wasps;

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
                    h = h * 31 + _treeDensity.GetHashCode();
                    h = h * 31 + _canopyRadius;
                    h = h * 31 + _lakeRadius.GetHashCode();
                    h = h * 31 + _pondCount;
                    h = h * 31 + _sunStoneCount;
                    h = h * 31 + _burrowCount;
                    h = h * 31 + _burrowSpacing.GetHashCode();
                    h = h * 31 + (_river ? 1 : 0);
                    h = h * 31 + _riverHalfWidth.GetHashCode();
                    h = h * 31 + _riverDepth.GetHashCode();
                    h = h * 31 + _bridgeCount;
                    h = h * 31 + _bridgeClearance.GetHashCode();
                    h = h * 31 + _swampRadius.GetHashCode();
                    h = h * 31 + _swampPoolThreshold.GetHashCode();
                    h = h * 31 + _swampTreeCount;
                    h = h * 31 + _ridgeRadius.GetHashCode();
                    h = h * 31 + _ridgeLift.GetHashCode();
                    h = h * 31 + _ridgeBoulders;
                    h = h * 31 + _ridgeStones;
                    h = h * 31 + _hollowLogCount;
                    h = h * 31 + _birdNestCount;
                    h = h * 31 + _brambleCount;
                    h = h * 31 + _brambleRadius.GetHashCode();
                    h = h * 31 + _mouseColonyCount;
                    h = h * 31 + _hotSpringCount;
                    h = h * 31 + _waspNestCount;
                    h = h * 31 + WorldBlueprint.VERSION;
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
            _bramble = new bool[n];
            _hotSpring = new bool[n];
            _colonyHole = new bool[n];
            _reserved = new bool[n];
            _waterKind = new byte[n];
            _biome = new byte[n];
            _shoreDist = new float[n];
            _underBridgeLevel = new float[n];
            for (int i = 0; i < n; i++)
            {
                _waterLevel[i] = float.NegativeInfinity;
                _underBridgeLevel[i] = float.NegativeInfinity;
            }

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
            // Места из плана застолбить до россыпи камней и брёвен.
            ReserveBlueprint();
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
            // Новые места — своими генераторами и после всего остального: старая раскладка не сдвигается.
            PlaceHotSprings(new System.Random(_obstacleSeed * 53 + 19));
            PlaceMouseColonies(new System.Random(_obstacleSeed * 59 + 23));
            PlaceBrambles(new System.Random(_obstacleSeed * 61 + 29));
            PlaceWaspNests(new System.Random(_obstacleSeed * 67 + 31));

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

        /// <summary>Заросли ежевики: укрытие, но змея в них ползёт медленнее, а на бегу царапается; лиса туда не лезет.</summary>
        public bool IsBramble(Vector2Int cell)
        {
            return IsInside(cell) && _bramble[ToIndex(cell)];
        }

        public bool IsBrambleAt(Vector3 world)
        {
            return IsBramble(WorldToCell(world));
        }

        /// <summary>Парящая земля тёплого ключа: греет и ночью.</summary>
        public bool IsHotSpring(Vector2Int cell)
        {
            return IsInside(cell) && _hotSpring[ToIndex(cell)];
        }

        public bool IsHotSpringAt(Vector3 world)
        {
            return IsHotSpring(WorldToCell(world));
        }

        /// <summary>Мышиная норка: испуганная мышь рядом с ней ныряет под землю.</summary>
        public bool IsColonyHole(Vector2Int cell)
        {
            return IsInside(cell) && _colonyHole[ToIndex(cell)];
        }

        /// <summary>Бревно-мост через реку: по нему ползут посуху.</summary>
        public bool IsBridge(Vector2Int cell)
        {
            return IsInside(cell) && _bridge[ToIndex(cell)];
        }

        /// <summary>
        /// Под бревном-мостом есть проход: level — по чему под ним плывут или ползут (вода реки или земля берега;
        /// вода — если level выше дна), deck — верх бревна. Где бревно почти лежит на земле (у концов), прохода нет —
        /// там его переползают поверху, как обычное бревно.
        /// </summary>
        public bool TryGetUnderBridge(Vector3 world, out float level, out float deck)
        {
            level = float.NegativeInfinity;
            deck = float.NegativeInfinity;
            Vector2Int c = WorldToCell(world);
            if (!IsInside(c)) return false;
            int i = ToIndex(c);
            if (!_bridge[i]) return false;
            deck = _waterLevel[i];
            level = Mathf.Max(GroundHeight(world), _underBridgeLevel[i]);
            return deck - level >= MIN_BRIDGE_GAP * _cellSize;
        }

        /// <summary>Под бревном-мостом течёт река (для меша воды): level — её уровень.</summary>
        public bool TryGetRiverUnderBridge(Vector2Int cell, out float level)
        {
            level = IsInside(cell) ? _underBridgeLevel[ToIndex(cell)] : float.NegativeInfinity;
            return level > float.NegativeInfinity;
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

        /// <summary>Высота полого бревна (диаметр ствола) — выше неё змея перелетает бревно прыжком.</summary>
        public float HollowLogHeight => 1.04f * _cellSize;

        /// <summary>
        /// Стенки полого бревна — две длинные боковины по краям клеток: войти можно только с торца, через дыру.
        /// Круг (pos, radius) выталкивается из боковины; возвращает true и нормаль, если упёрся.
        /// </summary>
        public bool CollideHollowLogWalls(ref Vector3 pos, float radius, out Vector3 normal)
        {
            normal = Vector3.zero;
            float half = 0.5f * _cellSize;
            for (int i = 0; i < _hollowLogs; i++)
            {
                GetHollowLog(i, out Vector3 a, out Vector3 b);
                bool alongX = _hollowAlongX[i];
                float u = alongX ? pos.x : pos.z;
                float v = alongX ? pos.z : pos.x;
                float u0 = (alongX ? Mathf.Min(a.x, b.x) : Mathf.Min(a.z, b.z)) - half;
                float u1 = (alongX ? Mathf.Max(a.x, b.x) : Mathf.Max(a.z, b.z)) + half;
                float axis = alongX ? a.z : a.x;
                if (u < u0 - radius || u > u1 + radius || Mathf.Abs(v - axis) > half + radius) continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    float du = u - Mathf.Clamp(u, u0, u1);
                    float dv = v - (axis + side * half);
                    float distSqr = du * du + dv * dv;
                    if (distSqr >= radius * radius) continue;
                    // Ровно на стенке — выталкиваем наружу.
                    if (distSqr < 1e-8f) { du = 0f; dv = side * 1e-4f; distSqr = dv * dv; }
                    float dist = Mathf.Sqrt(distSqr);
                    float nu = du / dist, nv = dv / dist;
                    u += nu * (radius - dist);
                    v += nv * (radius - dist);
                    if (alongX) { pos.x = u; pos.z = v; normal = new Vector3(nu, 0f, nv); }
                    else { pos.z = u; pos.x = v; normal = new Vector3(nv, 0f, nu); }
                    return true;
                }
            }
            return false;
        }

        /// <summary>Шаг между соседними клетками пересекает боковину полого бревна (в бревно — только вдоль него).</summary>
        public bool CrossesHollowWall(Vector2Int from, Vector2Int to)
        {
            bool fromHollow = IsHollow(from), toHollow = IsHollow(to);
            if (!fromHollow && !toHollow) return false;
            Vector2Int log = toHollow ? to : from;
            bool alongX = IsHollow(new Vector2Int(log.x - 1, log.y)) || IsHollow(new Vector2Int(log.x + 1, log.y));
            int dx = to.x - from.x, dy = to.y - from.y;
            return alongX ? dy != 0 : dx != 0;
        }

        /// <summary>Короткий шаг (не дальше соседней клетки) из from в to проходит сквозь боковину полого бревна.</summary>
        public bool CrossesHollowWall(Vector3 from, Vector3 to)
        {
            return CrossesHollowWall(WorldToCell(from), WorldToCell(to));
        }

        public int BirdNestCount => _placedBirdNests;

        public int MouseColonyCount => _colonies;

        /// <summary>Центр мышиного городка.</summary>
        public Vector3 GetMouseColony(int index) => CellToWorld(_colonyCenters[index]);

        public int ColonyHoleCount => _holes;

        public Vector3 GetColonyHole(int index) => CellToWorld(_colonyHoles[index]);

        /// <summary>Ближайшая мышиная норка: индекс (-1 — норок нет) и расстояние до неё в мировых единицах.</summary>
        public int NearestColonyHole(Vector3 world, out float distance)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _holes; i++)
            {
                Vector3 d = CellToWorld(_colonyHoles[i]) - world;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            distance = best >= 0 ? Mathf.Sqrt(bestSqr) : float.MaxValue;
            return best;
        }

        /// <summary>Свободная клетка рядом с мышиным городком (для появления мышей); городков нет — false.</summary>
        public bool TryGetColonyCell(out Vector2Int cell)
        {
            cell = default;
            if (_colonies == 0) return false;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector2Int c = _colonyCenters[Random.Range(0, _colonies)];
                var candidate = new Vector2Int(c.x + Random.Range(-3, 4), c.y + Random.Range(-3, 4));
                if (!IsInside(candidate) || !IsSpawnable(ToIndex(candidate))) continue;
                cell = candidate;
                return true;
            }
            return false;
        }

        public int HotSpringCount => _springs;

        /// <summary>Центр тёплого ключа (середина пятна 2x2).</summary>
        public Vector3 GetHotSpring(int index)
        {
            Vector2Int c = _hotSprings[index];
            return (CellToWorld(c) + CellToWorld(new Vector2Int(c.x + 1, c.y + 1))) * 0.5f;
        }

        public int WaspNestCount => _wasps;

        /// <summary>Пень, на котором висит осиное гнездо (центр клетки на земле).</summary>
        public Vector3 GetWaspNest(int index) => CellToWorld(_waspNests[index]);

        public Vector2Int GetWaspNestCell(int index) => _waspNests[index];

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
            if (_birdNestCell[index] || _hollow[index] || _hotSpring[index] || _colonyHole[index]) return false;
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

        /// <summary>
        /// Длина плеча моста от оси реки, клеток: дальше полуширины русла, пока земля не поднимется до низа опоры
        /// (rest — мировая высота земли, на которую ляжет бревно); не дальше склона долины.
        /// </summary>
        private float BridgeReach(Vector2 center, Vector2 dir, float half, float rest)
        {
            float max = half * 2.6f + 3f;
            for (float d = half * 1.15f + 0.6f; d < max; d += 0.25f)
            {
                Vector2 q = center + dir * d;
                if (GroundHeight(CellCenterFlat(q.x, q.y)) >= rest) return d + 0.4f;
            }
            return max;
        }

        private Vector3 CellCenterFlat(float x, float y)
        {
            return transform.position + new Vector3((x - (_width - 1) * 0.5f) * _cellSize, 0f, (y - (_height - 1) * 0.5f) * _cellSize);
        }

        /// <summary>Озеро и пруды — «кляксы» по плану мира (WorldBlueprint); сид меняет только форму берегов.</summary>
        private void PlaceWater(System.Random rng)
        {
            _ponds = 0;
            int ponds = Mathf.Min(_pondCount, WorldBlueprint.Ponds.Length);
            int total = Mathf.Min(MAX_PONDS, ponds + (_lakeRadius > 0f ? 1 : 0));
            for (int k = 0; k < total; k++)
            {
                bool lake = k == 0 && _lakeRadius > 0f;
                Vector3 plan = lake ? WorldBlueprint.Lake : WorldBlueprint.Ponds[k - (_lakeRadius > 0f ? 1 : 0)];
                float radius = lake ? _lakeRadius : plan.z;
                Vector2 c = PlanCell(plan.x, plan.y);
                float px = c.x, py = c.y;

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

        /// <summary>Точка плана мира (клетки от центра, y — на север) → координаты клетки.</summary>
        private Vector2 PlanCell(float x, float y)
        {
            return new Vector2((_width - 1) * 0.5f + x, (_height - 1) * 0.5f + y);
        }

        private Vector2 PlanCell(Vector2 p) => PlanCell(p.x, p.y);

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

                // Россыпь по смыслу, а не ровным слоем: брёвна — у лесов, валуны — у гряды и подножий гор,
                // на открытых полях почти пусто.
                if (rng.NextDouble() > ObstacleWeight(ox, oy, type)) continue;
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
            int groves = Mathf.Min(_groveCount, WorldBlueprint.Groves.Length);
            var dense = new bool[_forest.Length];
            for (int g = 0; g < groves; g++)
            {
                Vector4 plan = WorldBlueprint.Groves[g];
                Vector2 c = PlanCell(plan.x, plan.y);
                float px = c.x, py = c.y;
                float radius = plan.z;
                float phase = (float)rng.NextDouble() * 6.283f;
                bool birch = plan.w > 0.5f && plan.w < 1.5f;
                bool oldForest = plan.w > 1.5f;
                int r = Mathf.CeilToInt(radius * 1.35f);
                for (int y = Mathf.FloorToInt(py) - r; y <= Mathf.CeilToInt(py) + r; y++)
                {
                    for (int x = Mathf.FloorToInt(px) - r; x <= Mathf.CeilToInt(px) + r; x++)
                    {
                        if (x < 2 || y < 2 || x >= _width - 2 || y >= _height - 2) continue;
                        int i = y * _width + x;
                        if (_water[i] || _obstacles[i] == ObstacleType.Mountain || _biome[i] != 0) continue;
                        // На застолблённых местах (норы, городки, ключи) деревья не растут — поляна остаётся поляной.
                        if (_reserved[i]) continue;
                        float dx = x - px, dy = y - py;
                        float edge = radius * (1f + 0.25f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + phase));
                        if (dx * dx + dy * dy > edge * edge) continue;
                        _forest[i] = true;
                        _birch[i] = birch;
                        dense[i] = oldForest;
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
                    // Старый лес вдвое гуще: под его кронами орёл не достанет почти нигде.
                    if (rng.NextDouble() > (dense[i] ? _treeDensity * 1.7f : _treeDensity)) continue;
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

        /// <summary>Гнездо для кладки — на Северном поле за рекой (план мира): ближайшее открытое место к задуманному.</summary>
        private void PlaceNest(System.Random rng)
        {
            _hasNest = FindNear(WorldBlueprint.Nest, 8, (x, y) => IsOpenArea(x, y, 1, 2), out _nest);
        }

        /// <summary>Норы — сеть безопасных точек по плану мира: ближайшее открытое место к задуманному, вдали от воды.</summary>
        private void PlaceBurrows(System.Random rng)
        {
            _placedBurrows = 0;
            int target = Mathf.Min(_burrowCount, Mathf.Min(MAX_BURROWS, WorldBlueprint.Burrows.Length));
            for (int k = 0; k < target; k++)
            {
                if (!FindNear(WorldBlueprint.Burrows[k], 8, (x, y) => IsOpenArea(x, y, 1, 2) && HasShoreGap(x, y), out Vector2Int cell)) continue;
                _burrows[_placedBurrows++] = cell;
                _burrowCell[cell.y * _width + cell.x] = true;
            }
        }

        /// <summary>Не вплотную к воде: в двух клетках вокруг — суша.</summary>
        private bool HasShoreGap(int x, int y)
        {
            for (int yy = y - 2; yy <= y + 2; yy++)
                for (int xx = x - 2; xx <= x + 2; xx++)
                    if (xx >= 0 && yy >= 0 && xx < _width && yy < _height && _water[yy * _width + xx]) return false;
            return true;
        }

        /// <summary>
        /// Солнечные камни 2x2: сначала по одному у каждой норы (вышла утром — сразу греться), потом на гряде,
        /// остальные — на открытых местах вне рощ и болота.
        /// </summary>
        private void PlaceSunStones(System.Random rng)
        {
            _placedSunStones = 0;
            int target = Mathf.Min(_sunStoneCount, _sunStones.Length);
            for (int b = 0; b < _placedBurrows && _placedSunStones < target; b++)
            {
                Vector2Int burrow = _burrows[b];
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    float a = (float)rng.NextDouble() * 6.283f;
                    float r = 3.5f + (float)rng.NextDouble() * 3f;
                    int x = Mathf.RoundToInt(burrow.x + Mathf.Cos(a) * r), y = Mathf.RoundToInt(burrow.y + Mathf.Sin(a) * r);
                    if (x < 3 || y < 3 || x >= _width - 4 || y >= _height - 4) continue;
                    if (_forest[y * _width + x] || !IsOpenArea(x, y, 2, 1)) continue;
                    AddSunStone(x, y);
                    break;
                }
            }
            // Гряда — каменистое место: там камней больше всего.
            if (_hasRidge)
            {
                int ridgeTarget = Mathf.Min(target, _placedSunStones + 6);
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
                int i = y * _width + x;
                if (_forest[i] || _biome[i] == BIOME_SWAMP || _reserved[i]) continue;
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
        /// Русло по опорным точкам плана мира: гладкая кривая (Катмулл — Ром) с лёгким меандром от сида, от гор к озеру.
        /// Уровень воды вниз по течению не растёт, у устья равен уровню озера.
        /// </summary>
        private void PlanRiver(System.Random rng)
        {
            _riverCount = 0;
            _riverReach = 0f;
            if (!_river || _ponds == 0) return;
            Vector2[] plan = WorldBlueprint.River;
            float phase = (float)rng.NextDouble() * 6.283f;
            int mouth = -1;
            float walked = 0f;
            Vector2 last = PlanCell(plan[0]);
            for (int seg = 0; seg < plan.Length - 1 && mouth < 0; seg++)
            {
                Vector2 p0 = PlanCell(plan[Mathf.Max(0, seg - 1)]), p1 = PlanCell(plan[seg]);
                Vector2 p2 = PlanCell(plan[seg + 1]), p3 = PlanCell(plan[Mathf.Min(plan.Length - 1, seg + 2)]);
                int steps = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude));
                for (int k = 0; k < steps && mouth < 0 && _riverCount < MAX_RIVER_POINTS; k++)
                {
                    float t = (float)k / steps;
                    Vector2 pos = CatmullRom(p0, p1, p2, p3, t);
                    Vector2 tangent = CatmullRom(p0, p1, p2, p3, Mathf.Min(1f, t + 0.05f)) - pos;
                    if (tangent.sqrMagnitude > 1e-6f)
                    {
                        tangent.Normalize();
                        // Меандр — лёгкий, чтобы русло не уходило с задуманных мест.
                        pos += new Vector2(-tangent.y, tangent.x) * (Mathf.Sin(walked * 0.16f + phase) * 1.1f);
                    }
                    walked += (pos - last).magnitude;
                    last = pos;
                    _riverPts[_riverCount++] = pos;
                    for (int p = 0; p < _ponds && mouth < 0; p++)
                    {
                        Vector2 d = pos - _pondCenters[p];
                        if (d.magnitude < PondEdge(p, Mathf.Atan2(d.y, d.x)) * 0.8f) mouth = p;
                    }
                }
            }

            float level = float.MaxValue;
            float floor = mouth >= 0 ? _pondLevels[mouth] : _pondLevels[0];
            for (int i = 0; i < _riverCount; i++)
            {
                Vector2 p = _riverPts[i];
                level = Mathf.Min(level - 0.004f, CellHeight(p.x, p.y) - 0.45f);
                _riverLevels[i] = Mathf.Max(level, floor);
                float t = _riverCount > 1 ? (float)i / (_riverCount - 1) : 0f;
                _riverHalf[i] = Mathf.Lerp(_riverHalfWidth.x, _riverHalfWidth.y, t) * (0.85f + 0.3f * Mathf.PerlinNoise(i * 0.15f, 3.7f));
                _riverReach = Mathf.Max(_riverReach, _riverHalf[i] * 2.4f + 1f);
            }
            // Уровень не должен расти вниз по течению и после поднятия до уровня озера.
            for (int i = 1; i < _riverCount; i++) _riverLevels[i] = Mathf.Min(_riverLevels[i], _riverLevels[i - 1]);
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
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

        /// <summary>Болото у озера и каменная гряда на востоке — по плану мира.</summary>
        private void PlanSwampAndRidge(System.Random rng)
        {
            _swampPhase = (float)rng.NextDouble() * 6.283f;
            _ridgePhase = (float)rng.NextDouble() * 6.283f;
            _hasSwamp = _swampRadius > 0f;
            if (_hasSwamp)
            {
                _swampCenter = PlanCell(WorldBlueprint.Swamp.x, WorldBlueprint.Swamp.y);
                _swampRadius = Mathf.Min(_swampRadius, WorldBlueprint.Swamp.z);
                // Болото — низина чуть выше озера.
                _swampBase = (_ponds > 0 ? _pondLevels[0] : CellHeight(_swampCenter.x, _swampCenter.y)) + 0.3f;
            }
            _hasRidge = _ridgeRadius > 0f;
            if (_hasRidge)
            {
                _ridgeCenter = PlanCell(WorldBlueprint.Ridge.x, WorldBlueprint.Ridge.y);
                _ridgeRadius = Mathf.Min(_ridgeRadius, WorldBlueprint.Ridge.z);
            }
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
            for (int b = 0; b < count && b < WorldBlueprint.Bridges.Length; b++)
            {
                int i = Mathf.Clamp(NearestRiverPoint(PlanCell(WorldBlueprint.Bridges[b])), 1, _riverCount - 2);
                Vector2 p = _riverPts[i];
                // В болоте и у самых гор мост ни к чему.
                if (SwampBlend(p.x, p.y) > 0.3f || IsMountain(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)))) continue;
                Vector2 dir = (_riverPts[i + 1] - _riverPts[i - 1]).normalized;
                var n = new Vector2(-dir.y, dir.x);
                // Бревно лежит ровно и высоко над водой — под ним проплывает змея. Концы уходят по склону долины,
                // пока берег не поднимется до бревна: оно опирается на землю, а не висит в воздухе.
                float logTop = 0.42f * _cellSize;
                float deck = _riverLevels[i] + _bridgeClearance * _cellSize;
                float lenA = BridgeReach(p, -n, _riverHalf[i], deck - logTop);
                float lenE = BridgeReach(p, n, _riverHalf[i], deck - logTop);
                Vector2 a = p - n * lenA, e = p + n * lenE;
                Vector3 wa = CellCenterFlat(a.x, a.y);
                wa.y = deck;
                Vector3 we = CellCenterFlat(e.x, e.y);
                we.y = deck;
                _bridgeA[_bridges] = wa;
                _bridgeB[_bridges] = we;
                _bridges++;
                // Клетки моста — сплошная полоса вдоль оси бревна (и на диагонали без щелей между углами клеток):
                // иначе ползущая поверху змея на миг попадала бы в «воду» и проваливалась под бревно.
                Vector2 ab = e - a;
                float abLen2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
                int minX = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(a.x, e.x)) - 1), maxX = Mathf.Min(_width - 2, Mathf.CeilToInt(Mathf.Max(a.x, e.x)) + 1);
                int minY = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(a.y, e.y)) - 1), maxY = Mathf.Min(_height - 2, Mathf.CeilToInt(Mathf.Max(a.y, e.y)) + 1);
                for (int cy = minY; cy <= maxY; cy++)
                {
                    for (int cx = minX; cx <= maxX; cx++)
                    {
                        var c = new Vector2(cx, cy);
                        float t = Mathf.Clamp01(Vector2.Dot(c - a, ab) / abLen2);
                        if ((a + ab * t - c).sqrMagnitude > BRIDGE_HALF_WIDTH * BRIDGE_HALF_WIDTH) continue;
                        int k = cy * _width + cx;
                        if (_water[k] && _waterKind[k] == WATER_RIVER) _underBridgeLevel[k] = _waterLevel[k];
                        _bridge[k] = true;
                        _water[k] = false;
                        _waterKind[k] = 0;
                        _waterLevel[k] = deck;
                    }
                }
            }
        }

        /// <summary>
        /// Вероятность оставить препятствие в клетке: поваленные брёвна и пни тянутся к лесам, валуны и стоячие камни —
        /// к гряде и подножию гор; открытые поля почти чистые, на остальном лугу — реденько.
        /// </summary>
        private float ObstacleWeight(int x, int y, ObstacleType type)
        {
            if (InOpenField(x, y)) return 0.08f;
            float forest = 0f;
            Vector4[] groves = WorldBlueprint.Groves;
            for (int i = 0; i < groves.Length; i++)
            {
                Vector2 c = PlanCell(groves[i].x, groves[i].y);
                float d = (new Vector2(x, y) - c).magnitude / (groves[i].z * 1.5f);
                forest = Mathf.Max(forest, 1f - d);
            }
            float rock = 0f;
            if (_hasRidge) rock = Mathf.Max(rock, 1f - (new Vector2(x, y) - _ridgeCenter).magnitude / (_ridgeRadius * 1.9f));
            Vector3 world = CellCenterFlat(x, y);
            float foot = Mathf.InverseLerp(52f, 70f, (world - transform.position).magnitude / _cellSize);
            rock = Mathf.Max(rock, foot);
            forest = Mathf.Clamp01(forest);
            rock = Mathf.Clamp01(rock);
            switch (type)
            {
                case ObstacleType.LowWall: return 0.18f + 0.8f * forest + 0.15f * rock;
                case ObstacleType.Block: return 0.12f + 0.85f * rock + 0.2f * forest;
                default: return 0.2f + 0.5f * forest + 0.6f * rock;
            }
        }

        private int NearestRiverPoint(Vector2 p)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _riverCount; i++)
            {
                float d = (_riverPts[i] - p).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = i;
            }
            return best;
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

        /// <summary>
        /// Птичьи гнёзда — у задуманных мест плана (опушки, трава у старта и у реки): ближайшая укрытая клетка;
        /// если укрытия рядом нет — открытая, и вокруг неё подсаживается трава для подхода.
        /// </summary>
        private void PlaceBirdNests(System.Random rng)
        {
            _placedBirdNests = 0;
            int target = Mathf.Min(_birdNestCount, Mathf.Min(MAX_BIRD_NESTS, WorldBlueprint.BirdNests.Length));
            for (int k = 0; k < target; k++)
            {
                Vector2 plan = WorldBlueprint.BirdNests[k];
                bool hiddenFound = FindNear(plan, 7, (x, y) => IsBirdNestSpot(x, y) && ((_cover[y * _width + x] && !_reed[y * _width + x]) || _forest[y * _width + x]), out Vector2Int cell);
                if (!hiddenFound && !FindNear(plan, 7, IsBirdNestSpot, out cell)) continue;
                if (!hiddenFound) AddCoverPatch(cell.x, cell.y, 2.6f, (float)rng.NextDouble() * 6.283f);
                int i = cell.y * _width + cell.x;
                _birdNests[_placedBirdNests++] = cell;
                _birdNestCell[i] = true;
                // Маленькая полянка вокруг гнезда: само гнездо видно, а трава рядом — укрытие для подхода.
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int n = (cell.y + oy) * _width + cell.x + ox;
                        _cover[n] = false;
                        _reed[n] = false;
                    }
                }
            }
        }

        private bool IsBirdNestSpot(int x, int y)
        {
            int i = y * _width + x;
            if (_hollow[i] || _bridge[i] || _biome[i] == BIOME_SWAMP) return false;
            if (!IsOpenArea(x, y, 1, 1)) return false;
            return !_hasNest || (_nest - new Vector2Int(x, y)).sqrMagnitude >= 100;
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
                    if (_hollow[i] || _bridge[i] || _hotSpring[i] || _colonyHole[i]) return false;
                }
            }
            return true;
        }

        // ---------------- Ежевика, мышиные городки, тёплые ключи, осы ----------------

        /// <summary>Тёплые ключи — пятна 2x2 на ровном открытом месте рядом с задуманным; трава вокруг выгорает.</summary>
        private void PlaceHotSprings(System.Random rng)
        {
            _springs = 0;
            int target = Mathf.Min(_hotSpringCount, Mathf.Min(MAX_HOT_SPRINGS, WorldBlueprint.HotSprings.Length));
            for (int s = 0; s < target; s++)
            {
                // Лужица плоская — ищем ровное место, иначе корку срежет склон.
                // Ключ — часть замысла (у гнезда он греет ночную охрану кладки), поэтому без места не остаётся:
                // сначала ровное, потом просто открытое (корку BoardView кладёт по верхней точке склона).
                Vector2 plan = WorldBlueprint.HotSprings[s];
                bool found = FindNear(plan, 9, (x, y) => IsSpringSpot(x, y) && HeightSpread(x - 1, y - 1, 4) <= 0.25f, out Vector2Int cell)
                             || FindNear(plan, 9, (x, y) => IsSpringSpot(x, y) && HeightSpread(x - 1, y - 1, 4) <= 0.6f, out cell)
                             || FindNear(plan, 12, IsSpringSpot, out cell);
                if (!found) continue;
                _hotSprings[_springs++] = cell;
                for (int yy = cell.y - 1; yy <= cell.y + 2; yy++)
                {
                    for (int xx = cell.x - 1; xx <= cell.x + 2; xx++)
                    {
                        int k = yy * _width + xx;
                        _cover[k] = false;
                        _reed[k] = false;
                        if (xx >= cell.x && xx <= cell.x + 1 && yy >= cell.y && yy <= cell.y + 1) _hotSpring[k] = true;
                    }
                }
            }
        }

        private bool IsSpringSpot(int x, int y)
        {
            return _biome[y * _width + x] == 0 && IsOpenArea(x, y, 2, 2) && !NearMarked(x, y, 3, _birdNestCell);
        }

        /// <summary>
        /// Мышиные городки — открытые полянки с россыпью норок у задуманных мест: мыши появляются тут чаще и прячутся
        /// в норки, если их спугнуть, — брать их приходится из засады с края полянки.
        /// </summary>
        private void PlaceMouseColonies(System.Random rng)
        {
            _colonies = 0;
            _holes = 0;
            int target = Mathf.Min(_mouseColonyCount, Mathf.Min(MAX_COLONIES, WorldBlueprint.MouseColonies.Length));
            for (int c = 0; c < target; c++)
            {
                bool found = FindNear(WorldBlueprint.MouseColonies[c], 9, (x, y) => _biome[y * _width + x] == 0
                    && IsOpenArea(x - 2, y - 2, 5, 1) && !NearMarked(x, y, 3, _birdNestCell), out Vector2Int center);
                if (!found) continue;
                int x0 = center.x, y0 = center.y;
                _colonyCenters[_colonies++] = center;
                // Норки — по кругу с разбросом; трава вокруг выщипана, полянку видно издалека.
                double phase = rng.NextDouble() * 6.283;
                for (int h = 0; h < HOLES_PER_COLONY; h++)
                {
                    double a = phase + h * 6.283 / HOLES_PER_COLONY + (rng.NextDouble() - 0.5) * 0.6;
                    double r = 1.2 + rng.NextDouble() * 1.2;
                    var hole = new Vector2Int(x0 + (int)System.Math.Round(System.Math.Cos(a) * r), y0 + (int)System.Math.Round(System.Math.Sin(a) * r));
                    int hi = hole.y * _width + hole.x;
                    if (_colonyHole[hi]) continue;
                    _colonyHole[hi] = true;
                    _colonyHoles[_holes++] = hole;
                }
                for (int yy = y0 - 3; yy <= y0 + 3; yy++)
                {
                    for (int xx = x0 - 3; xx <= x0 + 3; xx++)
                    {
                        if ((xx - x0) * (xx - x0) + (yy - y0) * (yy - y0) > 10) continue;
                        int k = yy * _width + xx;
                        _cover[k] = false;
                        _reed[k] = false;
                    }
                }
            }
        }

        /// <summary>
        /// Ежевика по плану: треугольник убежищ у гнезда, островки на открытых местах, опушки. Тоже укрытие
        /// (отмечается и как трава), но колючее. Не растёт в воде, на камнях, в болоте и вплотную к гнёздам, норам и ключам.
        /// </summary>
        private void PlaceBrambles(System.Random rng)
        {
            Vector3[] plan = WorldBlueprint.Brambles;
            int count = Mathf.Min(_brambleCount, plan.Length);
            for (int b = 0; b < count; b++)
            {
                Vector2 c = PlanCell(plan[b].x, plan[b].y);
                float px = c.x, py = c.y;
                float radius = plan[b].z * (_brambleRadius / 2.3f);
                float phase = (float)rng.NextDouble() * 6.283f;
                int r = Mathf.CeilToInt(radius * 1.4f);
                for (int y = Mathf.FloorToInt(py) - r; y <= Mathf.CeilToInt(py) + r; y++)
                {
                    for (int x = Mathf.FloorToInt(px) - r; x <= Mathf.CeilToInt(px) + r; x++)
                    {
                        if (x < 2 || x >= _width - 2 || y < 2 || y >= _height - 2) continue;
                        int i = y * _width + x;
                        if (_obstacles[i] != ObstacleType.None || _water[i] || _sunStone[i] || _burrowCell[i] || _hollow[i] || _bridge[i]) continue;
                        if (_hotSpring[i] || _colonyHole[i] || _biome[i] != 0) continue;
                        if (_hasNest && Mathf.Abs(x - _nest.x) <= 2 && Mathf.Abs(y - _nest.y) <= 2) continue;
                        if (NearMarked(x, y, 1, _birdNestCell) || NearMarked(x, y, 2, _hotSpring) || NearMarked(x, y, 1, _burrowCell)) continue;
                        float ox = x - px, oy = y - py;
                        float edge = radius * (1f + 0.28f * Mathf.Sin(Mathf.Atan2(oy, ox) * 4f + phase));
                        if (ox * ox + oy * oy > edge * edge) continue;
                        _bramble[i] = true;
                        _cover[i] = true;
                        _reed[i] = false;
                    }
                }
            }
        }

        /// <summary>
        /// Осиные гнёзда — на одиноких пнях у задуманных мест (подход к мосту, короткая дорога к гряде, городки):
        /// если пня рядом нет, он ставится на свободную клетку. Не в роще, болоте и на гряде.
        /// </summary>
        private void PlaceWaspNests(System.Random rng)
        {
            _wasps = 0;
            int target = Mathf.Min(_waspNestCount, Mathf.Min(MAX_WASP_NESTS, WorldBlueprint.WaspNests.Length));
            for (int w = 0; w < target; w++)
            {
                Vector2 plan = WorldBlueprint.WaspNests[w];
                bool stump = FindNear(plan, 5, (x, y) => IsWaspStump(x, y) && _obstacles[y * _width + x] == ObstacleType.Pillar, out Vector2Int cell);
                if (!stump && !FindNear(plan, 6, (x, y) => IsWaspStump(x, y) && IsOpenArea(x, y, 1, 1), out cell)) continue;
                int i = cell.y * _width + cell.x;
                _obstacles[i] = ObstacleType.Pillar;
                _cover[i] = false;
                _bramble[i] = false;
                _waspNests[_wasps++] = cell;
            }
        }

        private bool IsWaspStump(int x, int y)
        {
            int i = y * _width + x;
            return !_forest[i] && _biome[i] == 0 && !_water[i] && !_burrowCell[i] && !_colonyHole[i] && !_hotSpring[i] && !_birdNestCell[i];
        }

        // ---------------- План мира: поиск мест и резерв ----------------

        /// <summary>
        /// Ближайшая к точке плана клетка в радиусе maxRadius, для которой ok(x, y) истинно (ok зовётся по мере
        /// приближения — сначала дальние кандидаты отсеиваются по расстоянию). Только при генерации.
        /// </summary>
        private bool FindNear(Vector2 plan, int maxRadius, System.Func<int, int, bool> ok, out Vector2Int cell)
        {
            Vector2 c = PlanCell(plan);
            int cx = Mathf.RoundToInt(c.x), cy = Mathf.RoundToInt(c.y);
            cell = default;
            int best = int.MaxValue;
            for (int r = 0; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int d2 = dx * dx + dy * dy;
                        if (d2 >= best || d2 > maxRadius * maxRadius) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 4 || y < 4 || x >= _width - 5 || y >= _height - 5) continue;
                        if (_obstacles[y * _width + x] == ObstacleType.Mountain || !ok(x, y)) continue;
                        best = d2;
                        cell = new Vector2Int(x, y);
                    }
                }
                // Кольца дальше уже не дадут клетку ближе найденной.
                if (best <= r * r) break;
            }
            return best != int.MaxValue;
        }

        /// <summary>Застолбить места плана (норы, гнездо, городки, ключи, осы, концы мостов), чтобы россыпь камней их не заняла.</summary>
        private void ReserveBlueprint()
        {
            ReserveCircle(PlanCell(WorldBlueprint.Nest), 4.5f);
            for (int i = 0; i < WorldBlueprint.Burrows.Length; i++) ReserveCircle(PlanCell(WorldBlueprint.Burrows[i]), 3.5f);
            for (int i = 0; i < WorldBlueprint.MouseColonies.Length; i++) ReserveCircle(PlanCell(WorldBlueprint.MouseColonies[i]), 4.5f);
            for (int i = 0; i < WorldBlueprint.HotSprings.Length; i++) ReserveCircle(PlanCell(WorldBlueprint.HotSprings[i]), 3.5f);
            for (int i = 0; i < WorldBlueprint.WaspNests.Length; i++) ReserveCircle(PlanCell(WorldBlueprint.WaspNests[i]), 2.5f);
            // У концов мостов — свободный подход.
            for (int b = 0; b < _bridges; b++)
            {
                ReserveCircle(WorldToCellFloat(_bridgeA[b]), 3f);
                ReserveCircle(WorldToCellFloat(_bridgeB[b]), 3f);
            }
        }

        private void ReserveCircle(Vector2 center, float radius)
        {
            int r = Mathf.CeilToInt(radius);
            for (int y = Mathf.RoundToInt(center.y) - r; y <= Mathf.RoundToInt(center.y) + r; y++)
            {
                for (int x = Mathf.RoundToInt(center.x) - r; x <= Mathf.RoundToInt(center.x) + r; x++)
                {
                    if (x < 0 || y < 0 || x >= _width || y >= _height) continue;
                    float dx = x - center.x, dy = y - center.y;
                    if (dx * dx + dy * dy <= radius * radius) _reserved[y * _width + x] = true;
                }
            }
        }

        /// <summary>Точка (координаты клетки) на открытом поле плана — там орёл, а травы и брёвен почти нет.</summary>
        private bool InOpenField(float x, float y)
        {
            Vector3[] fields = WorldBlueprint.OpenFields;
            for (int i = 0; i < fields.Length; i++)
            {
                Vector2 c = PlanCell(fields[i].x, fields[i].y);
                float dx = x - c.x, dy = y - c.y;
                if (dx * dx + dy * dy < fields[i].z * fields[i].z) return true;
            }
            return false;
        }

        /// <summary>Перепад высот углов клеток в квадрате size×size от (x, y), мировых единиц.</summary>
        private float HeightSpread(int x, int y, int size)
        {
            float min = float.MaxValue, max = float.MinValue;
            int stride = _width + 1;
            for (int yy = y; yy <= y + size; yy++)
            {
                for (int xx = x; xx <= x + size; xx++)
                {
                    if (xx < 0 || yy < 0 || xx > _width || yy > _height) continue;
                    float h = _heights[yy * stride + xx];
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                }
            }
            return max - min;
        }

        /// <summary>Есть ли отмеченная клетка в квадрате радиуса radius вокруг (x, y).</summary>
        private bool NearMarked(int x, int y, int radius, bool[] mask)
        {
            for (int yy = y - radius; yy <= y + radius; yy++)
            {
                for (int xx = x - radius; xx <= x + radius; xx++)
                {
                    if (xx < 0 || yy < 0 || xx >= _width || yy >= _height) continue;
                    if (mask[yy * _width + xx]) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Высокая трава: сначала тропы из плана мира — цепочки зарослей между укрытиями (тихий путь), потом случайные
        /// «кляксы», но не на открытых полях: там охотится орёл, и это должно читаться с первого взгляда.
        /// Центр старта остаётся открытым, чтобы змею было видно сразу.
        /// </summary>
        private void PlaceCover(System.Random rng)
        {
            Vector2[][] trails = WorldBlueprint.CoverTrails;
            for (int t = 0; t < trails.Length; t++)
            {
                for (int k = 0; k + 1 < trails[t].Length; k++)
                {
                    Vector2 a = PlanCell(trails[t][k]), b = PlanCell(trails[t][k + 1]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 2.6f));
                    for (int i = 0; i <= steps; i++)
                    {
                        Vector2 q = Vector2.Lerp(a, b, (float)i / steps);
                        float radius = _coverPatchRadius * (0.65f + (float)rng.NextDouble() * 0.3f);
                        AddCoverPatch(q.x, q.y, radius, (float)rng.NextDouble() * 6.283f);
                    }
                }
            }

            for (int p = 0; p < _coverPatchCount; p++)
            {
                float px = 3f + (float)rng.NextDouble() * (_width - 7f);
                float py = 3f + (float)rng.NextDouble() * (_height - 7f);
                float radius = _coverPatchRadius * (0.7f + (float)rng.NextDouble() * 0.6f);
                float phase = (float)rng.NextDouble() * 6.283f;
                if (InOpenField(px, py)) continue;
                AddCoverPatch(px, py, radius, phase);
            }
        }

        /// <summary>Заросли — «клякса» неровной формы: радиус клетки от центра сравнивается с радиусом, меняющимся по углу.</summary>
        private void AddCoverPatch(float px, float py, float radius, float phase)
        {
            float cx = (_width - 1) * 0.5f;
            float cy = (_height - 1) * 0.5f;
            float clear = _startClearRadius * 0.5f;
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
                    if (_obstacles[i] != ObstacleType.None || _water[i] || _reserved[i]) return false;
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
