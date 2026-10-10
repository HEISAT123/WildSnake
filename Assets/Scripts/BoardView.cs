using UnityEngine;
using UnityEngine.Rendering;

namespace Snake3D
{
    /// <summary>
    /// Визуал поля: пол одним объектом, стены по периметру и «дикие» препятствия из раскладки GridManager:
    /// низкие стенки — поваленные брёвна, колонны — сломанные пни и стоячие камни, блоки — валуны;
    /// в рощах колонны — деревья с кронами и лесная подстилка; плюс заросли высокой травы, пруд, плоские солнечные камни,
    /// норы и гнездо (кладка — главная цель; яйца и маяк над гнездом включает GameManager).
    /// Строится один раз (в редакторе SnakeSceneBuilder'ом или в Start, если раскладку поменяли в инспекторе).
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Сетка, по которой строится поле")]
        [SerializeField] private GridManager _grid;

        [Header("Материалы")]
        [Tooltip("Пол с шахматной текстурой 2x2 (повторяется по размеру поля)")]
        [SerializeField] private Material _floorMaterial;

        [Tooltip("Земля вокруг поля (необязательно — если есть рельеф окружения, оставьте пустым)")]
        [SerializeField] private Material _groundMaterial;

        [Tooltip("Стены по периметру")]
        [SerializeField] private Material _wallMaterial;

        [Tooltip("Парапет стен")]
        [SerializeField] private Material _lowWallMaterial;

        [Tooltip("Кора: брёвна и пни")]
        [SerializeField] private Material _barkMaterial;

        [Tooltip("Светлый спил на верхушке пня")]
        [SerializeField] private Material _stumpTopMaterial;

        [Tooltip("Валуны")]
        [SerializeField] private Material _blockMaterial;

        [Tooltip("Высокая трава (укрытия)")]
        [SerializeField] private Material _tallGrassMaterial;

        [Tooltip("Вода пруда (полупрозрачная)")]
        [SerializeField] private Material _waterMaterial;

        [Tooltip("Илистое дно и берег пруда")]
        [SerializeField] private Material _pondBedMaterial;

        [Tooltip("Солнечные камни")]
        [SerializeField] private Material _sunStoneMaterial;

        [Tooltip("Земляной холмик норы")]
        [SerializeField] private Material _dirtMaterial;

        [Tooltip("Тёмный вход в нору")]
        [SerializeField] private Material _holeMaterial;

        [Header("Роща")]
        [Tooltip("Листва лиственных деревьев")]
        [SerializeField] private Material _leavesMaterial;

        [Tooltip("Хвоя")]
        [SerializeField] private Material _pineMaterial;

        [Tooltip("Лесная подстилка (полупрозрачная россыпь листьев)")]
        [SerializeField] private Material _litterMaterial;

        [Tooltip("Меш кроны лиственного дерева")]
        [SerializeField] private Mesh _crownMesh;

        [Tooltip("Меш яруса ели")]
        [SerializeField] private Mesh _pineMesh;

        [Tooltip("Слой крон (камера карты их не рисует, иначе они закрыли бы метки)")]
        [SerializeField] private int _canopyLayer;

        [Header("Гнездо")]
        [Tooltip("Яйца в гнезде")]
        [SerializeField] private Material _eggMaterial;

        [Tooltip("Светящийся столб над гнездом")]
        [SerializeField] private Material _beaconMaterial;

        [Header("Меши")]
        [Tooltip("Меш валуна")]
        [SerializeField] private Mesh _rockMesh;

        [Tooltip("Меш пучка травы")]
        [SerializeField] private Mesh _grassTuftMesh;



        [Header("Размеры")]
        [Tooltip("Толщина пола")]
        [SerializeField, Min(0.01f)] private float _floorThickness = 0.2f;

        [Tooltip("Высота стен по периметру над поверхностью поля")]
        [SerializeField, Min(0.05f)] private float _wallHeight = 0.8f;

        [Tooltip("Толщина стен (доля клетки)")]
        [SerializeField, Min(0.05f)] private float _wallThickness = 0.5f;

        [Tooltip("Насколько земля выходит за поле, клеток")]
        [SerializeField, Min(0f)] private float _groundPadding = 40f;

        [Tooltip("Высота пня, клеток")]
        [SerializeField, Min(0.5f)] private float _stumpHeight = 1.5f;

        [Tooltip("Пучков высокой травы на клетку укрытия")]
        [SerializeField, Range(1, 6)] private int _tuftsPerCell = 3;

        [Tooltip("Высота высокой травы, клеток (ниже камеры, чтобы не закрывала змею)")]
        [SerializeField, Min(0.2f)] private float _tallGrassHeight = 0.85f;

        // Запоминаем, для какой раскладки построено поле, чтобы не пересоздавать его без нужды.
        [SerializeField, HideInInspector] private int _builtLayoutVersion;
        [SerializeField, HideInInspector] private bool _isBuilt;
        [SerializeField, HideInInspector] private Renderer _floorRenderer;
        [SerializeField, HideInInspector] private Renderer[] _crowns = new Renderer[0];
        [SerializeField, HideInInspector] private GameObject _nestEggs;
        [SerializeField, HideInInspector] private GameObject _nestBeacon;

        /// <summary>Кроны деревьев рощ — их прячет CanopyFader, когда они заслоняют змею.</summary>
        public Renderer[] Crowns => _crowns;

        /// <summary>Яйца в гнезде (скрыты, пока змея их не отложит).</summary>
        public GameObject NestEggs => _nestEggs;

        /// <summary>Светящийся столб над гнездом (виден, когда змея готова к кладке).</summary>
        public GameObject NestBeacon => _nestBeacon;

        private readonly System.Collections.Generic.List<Renderer> _crownList = new System.Collections.Generic.List<Renderer>();

        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        private void Start()
        {
            if (!_isBuilt || _builtLayoutVersion != _grid.LayoutVersion || transform.childCount == 0) Build();
            // MaterialPropertyBlock не сохраняется в сцене, поэтому повторение текстуры пола ставим при каждом запуске.
            else ApplyFloorTiling();
        }

        [ContextMenu("Rebuild Board")]
        public void Build()
        {
            // В редакторе Awake у сетки не вызывается, поэтому раскладку генерируем сами.
            if (!Application.isPlaying) _grid.GenerateLayout();

            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroySafe(transform.GetChild(i).gameObject);

            float cs = _grid.CellSize;
            _crownList.Clear();
            BuildFloor(cs);
            BuildWalls(cs);
            BuildObstacles(cs);
            _crowns = _crownList.ToArray();
            BuildForestFloor(cs);
            BuildNest(cs);
            BuildPond(cs);
            BuildSunStones(cs);
            BuildBurrows(cs);
            BuildTallGrass(cs);

            _builtLayoutVersion = _grid.LayoutVersion;
            _isBuilt = true;
        }

        /// <summary>Один куб вместо сотен плиток: шахматку даёт текстура 2x2, повторённая на половину размеров поля.</summary>
        private void BuildFloor(float cs)
        {
            float w = _grid.Width * cs;
            float h = _grid.Height * cs;
            Renderer floor = CreateShape(PrimitiveType.Cube, "Floor",
                _grid.Center + Vector3.down * (_floorThickness * 0.5f), new Vector3(w, _floorThickness, h), _floorMaterial);
            floor.shadowCastingMode = ShadowCastingMode.Off;
            _floorRenderer = floor;
            ApplyFloorTiling();

            // Земля вокруг — только если нет отдельного рельефа (материал не задан).
            if (_groundMaterial == null) return;
            float pad = _groundPadding * cs * 2f;
            Renderer ground = CreateShape(PrimitiveType.Cube, "Ground",
                _grid.Center + Vector3.down * (_floorThickness + 0.05f), new Vector3(w + pad, 0.1f, h + pad), _groundMaterial);
            ground.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Текстура пола покрывает 2x2 клетки — повторяем её на половину размеров поля.</summary>
        private void ApplyFloorTiling()
        {
            if (_floorRenderer == null) return;
            var block = new MaterialPropertyBlock();
            var tiling = new Vector4(_grid.Width * 0.5f, _grid.Height * 0.5f, 0f, 0f);
            block.SetVector(MainTexStId, tiling);
            block.SetVector(BaseMapStId, tiling);
            _floorRenderer.SetPropertyBlock(block);
        }

        private void BuildWalls(float cs)
        {
            float halfW = _grid.Width * cs * 0.5f;
            float halfH = _grid.Height * cs * 0.5f;
            float t = _wallThickness * cs;
            float wallY = (_wallHeight - _floorThickness) * 0.5f;
            float wallH = _wallHeight + _floorThickness;
            Vector3 c = _grid.Center;

            CreateShape(PrimitiveType.Cube, "Wall_North", c + new Vector3(0f, wallY, halfH + t * 0.5f), new Vector3(halfW * 2f + t * 2f, wallH, t), _wallMaterial);
            CreateShape(PrimitiveType.Cube, "Wall_South", c + new Vector3(0f, wallY, -halfH - t * 0.5f), new Vector3(halfW * 2f + t * 2f, wallH, t), _wallMaterial);
            CreateShape(PrimitiveType.Cube, "Wall_East", c + new Vector3(halfW + t * 0.5f, wallY, 0f), new Vector3(t, wallH, halfH * 2f), _wallMaterial);
            CreateShape(PrimitiveType.Cube, "Wall_West", c + new Vector3(-halfW - t * 0.5f, wallY, 0f), new Vector3(t, wallH, halfH * 2f), _wallMaterial);

            // Парапет — чуть более широкий каменный край поверх стен.
            float capY = _wallHeight + 0.05f;
            float capT = t * 1.3f;
            CreateShape(PrimitiveType.Cube, "Coping_North", c + new Vector3(0f, capY, halfH + t * 0.5f), new Vector3(halfW * 2f + t * 2.6f, 0.1f, capT), _lowWallMaterial);
            CreateShape(PrimitiveType.Cube, "Coping_South", c + new Vector3(0f, capY, -halfH - t * 0.5f), new Vector3(halfW * 2f + t * 2.6f, 0.1f, capT), _lowWallMaterial);
            CreateShape(PrimitiveType.Cube, "Coping_East", c + new Vector3(halfW + t * 0.5f, capY, 0f), new Vector3(capT, 0.1f, halfH * 2f), _lowWallMaterial);
            CreateShape(PrimitiveType.Cube, "Coping_West", c + new Vector3(-halfW - t * 0.5f, capY, 0f), new Vector3(capT, 0.1f, halfH * 2f), _lowWallMaterial);
        }

        private void BuildObstacles(float cs)
        {
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    ObstacleType type = _grid.GetObstacle(cell);
                    switch (type)
                    {
                        case ObstacleType.Pillar:
                            if (_grid.IsForest(cell) && _crownMesh != null) BuildTree(cell, cs);
                            else if (Hash(x, y) < 0.55f) BuildStump(cell, cs);
                            else BuildStandingStone(cell, cs);
                            break;
                        case ObstacleType.Block:
                            // Блок 2x2 — один валун, строим его от левой нижней клетки.
                            if (!IsType(x - 1, y, type) && !IsType(x, y - 1, type)) BuildBoulder(cell, cs);
                            break;
                        case ObstacleType.LowWall:
                            BuildLogFrom(x, y, cs);
                            break;
                    }
                }
            }
        }

        /// <summary>Сломанный пень: толстый ствол со светлым спилом, острый обломок сбоку и корни.</summary>
        private void BuildStump(Vector2Int cell, float cs)
        {
            Vector3 basePos = _grid.CellToWorld(cell);
            float d = _grid.GetObstacleHalfSize(cell) * 2f;
            float h = _stumpHeight * cs * (0.85f + 0.3f * Hash(cell.y, cell.x));
            float yaw = Hash(cell.x, cell.y) * 360f;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);

            CreateShape(PrimitiveType.Cylinder, "Stump", basePos + Vector3.up * (h * 0.5f), new Vector3(d * 0.95f, h * 0.5f, d * 0.95f), _barkMaterial);
            CreateShape(PrimitiveType.Cylinder, "StumpTop", basePos + Vector3.up * h, new Vector3(d * 0.86f, 0.012f * cs, d * 0.86f), _stumpTopMaterial);

            // Обломок ствола торчит над спилом — силуэт сломанного дерева, а не ровного столба.
            float shard = h * 0.5f;
            Renderer s = CreateShape(PrimitiveType.Cylinder, "Shard", basePos + turn * new Vector3(d * 0.2f, 0f, 0f) + Vector3.up * (h + shard * 0.35f),
                new Vector3(d * 0.5f, shard * 0.5f, d * 0.38f), _barkMaterial);
            s.transform.rotation = turn * Quaternion.Euler(0f, 0f, -12f);

            for (int i = 0; i < 4; i++)
            {
                Quaternion around = turn * Quaternion.Euler(0f, i * 90f + 20f * Hash(cell.x + i, cell.y), 0f);
                Renderer root = CreateShape(PrimitiveType.Sphere, "Root", basePos + around * new Vector3(0f, 0.06f * cs, d * 0.5f),
                    new Vector3(d * 0.32f, 0.22f * cs, d * 0.6f), _barkMaterial);
                root.transform.rotation = around * Quaternion.Euler(18f, 0f, 0f);
            }
        }

        /// <summary>
        /// Дерево рощи: лиственное (ствол и три кома листвы) или ель (ствол и три яруса). Кроны высоко,
        /// чтобы камера чаще шла под ними; заслоняющие змею кроны делает прозрачными CanopyFader.
        /// </summary>
        private void BuildTree(Vector2Int cell, float cs)
        {
            Vector3 basePos = _grid.CellToWorld(cell);
            float s = 0.85f + 0.45f * Hash(cell.y, cell.x);
            float yaw = Hash(cell.x, cell.y) * 360f;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            float d = _grid.GetObstacleHalfSize(cell) * 2f * 1.05f;
            bool pine = Hash(cell.x + 7, cell.y + 3) < 0.4f && _pineMesh != null;

            float trunk = (pine ? 3.2f : 3.6f) * cs * s;
            CreateShape(PrimitiveType.Cylinder, "Trunk", basePos + Vector3.up * (trunk * 0.5f), new Vector3(d, trunk * 0.5f, d), _barkMaterial);
            for (int i = 0; i < 4; i++)
            {
                Quaternion around = turn * Quaternion.Euler(0f, i * 90f + 20f * Hash(cell.x + i, cell.y), 0f);
                Renderer root = CreateShape(PrimitiveType.Sphere, "Root", basePos + around * new Vector3(0f, 0.05f * cs, d * 0.5f),
                    new Vector3(d * 0.3f, 0.2f * cs, d * 0.55f), _barkMaterial);
                root.transform.rotation = around * Quaternion.Euler(18f, 0f, 0f);
            }

            if (pine)
            {
                float[] heights = { 3f, 4f, 4.9f };
                float[] widths = { 3.2f, 2.5f, 1.7f };
                float[] tall = { 2.4f, 2.2f, 2f };
                for (int t = 0; t < 3; t++)
                    AddCrown(CreateMesh("PineTier", _pineMesh, _pineMaterial, basePos + Vector3.up * (heights[t] * cs * s),
                        new Vector3(widths[t], tall[t], widths[t]) * (cs * s), yaw + t * 25f));
                return;
            }

            Vector3[] offsets = { new Vector3(0f, 4.3f, 0f), new Vector3(0.8f, 4.9f, 0.4f), new Vector3(-0.7f, 4.7f, -0.5f) };
            Vector3[] sizes = { new Vector3(3.6f, 2.7f, 3.6f), new Vector3(2.5f, 2f, 2.5f), new Vector3(2.3f, 1.9f, 2.3f) };
            for (int t = 0; t < 3; t++)
                AddCrown(CreateMesh("Crown", _crownMesh, _leavesMaterial, basePos + turn * (offsets[t] * (cs * s)), sizes[t] * (cs * s), yaw + t * 60f));
        }

        private void AddCrown(Renderer crown)
        {
            if (_canopyLayer > 0) crown.gameObject.layer = _canopyLayer;
            _crownList.Add(crown);
        }

        /// <summary>Лесная подстилка: пятна опавших листьев на клетках рощ, одним мешем.</summary>
        private void BuildForestFloor(float cs)
        {
            if (_litterMaterial == null) return;
            var quads = new System.Collections.Generic.List<CombineInstance>();
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Mesh quad = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroySafe(temp);
            Vector3 origin = transform.position;
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_grid.IsForest(cell) || _grid.IsWater(cell) || Hash(x * 3, y * 5) > 0.8f) continue;
                    Vector3 c = _grid.CellToWorld(cell) - origin + new Vector3(0f, 0.012f + Hash(x, y) * 0.004f, 0f);
                    float size = (1.7f + 0.6f * Hash(y, x)) * cs;
                    Quaternion rot = Quaternion.Euler(90f, Hash(x + 11, y) * 360f, 0f);
                    quads.Add(new CombineInstance { mesh = quad, transform = Matrix4x4.TRS(c, rot, new Vector3(size, size, 1f)) });
                }
            }
            if (quads.Count == 0) return;
            var mesh = new Mesh { name = "ForestFloor" };
            if (quads.Count * 4 > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(quads.ToArray(), true, true);
            mesh.RecalculateBounds();
            var go = new GameObject("ForestFloor");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _litterMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Гнездо: кольцо веток вокруг ямки с листьями; яйца и маяк созданы скрытыми.</summary>
        private void BuildNest(float cs)
        {
            _nestEggs = null;
            _nestBeacon = null;
            if (!_grid.HasNest || _barkMaterial == null) return;
            Vector3 pos = _grid.NestPosition;
            var root = new GameObject("Nest");
            root.transform.SetParent(transform, false);
            root.transform.position = pos;

            if (_dirtMaterial != null)
                Parent(CreateShape(PrimitiveType.Sphere, "NestBowl", pos + Vector3.down * (0.02f * cs), new Vector3(1.5f * cs, 0.16f * cs, 1.5f * cs), _dirtMaterial), root);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Renderer twig = CreateShape(PrimitiveType.Cylinder, "Twig", pos + dir * (0.68f * cs) + Vector3.up * (0.1f * cs),
                    new Vector3(0.07f * cs, 0.42f * cs, 0.07f * cs), _barkMaterial);
                twig.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 70f + 30f * Hash(i, 3));
                Parent(twig, root);
            }

            if (_eggMaterial != null)
            {
                _nestEggs = new GameObject("NestEggs");
                _nestEggs.transform.SetParent(root.transform, false);
                _nestEggs.transform.position = pos;
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.3f;
                    Vector3 p = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.22f * cs) + Vector3.up * (0.12f * cs);
                    Renderer egg = CreateShape(PrimitiveType.Sphere, "Egg", p, new Vector3(0.22f, 0.3f, 0.22f) * cs, _eggMaterial);
                    egg.transform.rotation = Quaternion.Euler(70f, a * Mathf.Rad2Deg, 0f);
                    egg.transform.SetParent(_nestEggs.transform, true);
                }
                _nestEggs.SetActive(false);
            }

            if (_beaconMaterial != null)
            {
                Renderer beacon = CreateShape(PrimitiveType.Cylinder, "NestBeacon", pos + Vector3.up * (5f * cs), new Vector3(0.9f * cs, 5f * cs, 0.9f * cs), _beaconMaterial);
                beacon.shadowCastingMode = ShadowCastingMode.Off;
                beacon.receiveShadows = false;
                Parent(beacon, root);
                _nestBeacon = beacon.gameObject;
                _nestBeacon.SetActive(false);
            }
        }

        private static void Parent(Renderer r, GameObject root)
        {
            r.transform.SetParent(root.transform, true);
        }

        /// <summary>Стоячий камень — вытянутый вверх валун.</summary>
        private void BuildStandingStone(Vector2Int cell, float cs)
        {
            Vector3 basePos = _grid.CellToWorld(cell);
            float d = _grid.GetObstacleHalfSize(cell) * 2f;
            float h = _grid.GetObstacleHeight(cell) * (0.85f + 0.2f * Hash(cell.y, cell.x));
            float yaw = Hash(cell.x, cell.y) * 360f;
            if (_rockMesh == null)
            {
                CreateShape(PrimitiveType.Cube, "Stone", basePos + Vector3.up * (h * 0.5f), new Vector3(d, h, d), _blockMaterial);
                return;
            }
            CreateMesh("StandingStone", _rockMesh, _blockMaterial, basePos + Vector3.up * (h * 0.45f), new Vector3(d * 1.35f, h, d * 1.15f), yaw);
        }

        private void BuildBoulder(Vector2Int cell, float cs)
        {
            Vector3 center = (_grid.CellToWorld(cell) + _grid.CellToWorld(new Vector2Int(cell.x + 1, cell.y + 1))) * 0.5f;
            float h = _grid.GetObstacleHeight(cell);
            float yaw = Hash(cell.x, cell.y) * 360f;
            if (_rockMesh == null)
            {
                CreateShape(PrimitiveType.Cube, "Block", center + Vector3.up * (h * 0.5f), new Vector3(2f * cs, h, 2f * cs), _blockMaterial);
                return;
            }
            // Чуть шире квадрата 2x2, чтобы углы столкновения не торчали из камня.
            CreateMesh("Boulder", _rockMesh, _blockMaterial, center + Vector3.up * (h * 0.42f), new Vector3(2.5f * cs, h * 1.25f, 2.5f * cs), yaw);
        }

        /// <summary>Бревно на всю длину стенки: строим только от её начала (слева или снизу).</summary>
        private void BuildLogFrom(int x, int y, float cs)
        {
            bool alongX = IsType(x + 1, y, ObstacleType.LowWall) || IsType(x - 1, y, ObstacleType.LowWall);
            if (alongX ? IsType(x - 1, y, ObstacleType.LowWall) : IsType(x, y - 1, ObstacleType.LowWall)) return;

            int length = 1;
            while (alongX ? IsType(x + length, y, ObstacleType.LowWall) : IsType(x, y + length, ObstacleType.LowWall)) length++;

            var first = new Vector2Int(x, y);
            var last = alongX ? new Vector2Int(x + length - 1, y) : new Vector2Int(x, y + length - 1);
            Vector3 center = (_grid.CellToWorld(first) + _grid.CellToWorld(last)) * 0.5f;
            float diameter = Mathf.Max(_grid.GetObstacleHeight(first), 0.3f * cs) * 1.1f;
            // Цилиндр Unity вытянут по Y: кладём его на бок вдоль X или Z и чуть поворачиваем вокруг оси (кора не повторяется).
            Quaternion lay = alongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
            Renderer log = CreateShape(PrimitiveType.Cylinder, "Log", center + Vector3.up * (diameter * 0.5f),
                new Vector3(diameter, length * cs * 0.5f, diameter), _barkMaterial);
            log.transform.rotation = lay * Quaternion.Euler(0f, Hash(x, y) * 360f, 0f);
        }

        /// <summary>
        /// Пруд — два «веера» по форме берега из GridManager: широкое тёмное дно-берег и над ним
        /// полупрозрачная вода. Форма берега совпадает с водными клетками логики.
        /// </summary>
        private void BuildPond(float cs)
        {
            if (!_grid.HasPond || _waterMaterial == null) return;
            Vector3 center = _grid.PondCenter - transform.position;
            if (_pondBedMaterial != null)
                CreateFan("PondBed", center + Vector3.up * 0.006f, 0.55f * cs, 1.06f, _pondBedMaterial);
            CreateFan("PondWater", center + Vector3.up * 0.04f, 0.25f * cs, 1f, _waterMaterial);
        }

        private void CreateFan(string objectName, Vector3 localCenter, float pad, float scale, Material mat)
        {
            const int segments = 64;
            var vertices = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = localCenter;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float r = _grid.PondEdgeWorld(a) * scale + pad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i + 1] = localCenter + dir * r;
                uvs[i + 1] = new Vector2(0.5f + dir.x * r * 0.25f, 0.5f + dir.z * r * 0.25f);
                // Обход по часовой стрелке, если смотреть сверху, — лицом вверх.
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = objectName, vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r2 = go.AddComponent<MeshRenderer>();
            r2.sharedMaterial = mat;
            r2.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Плоский светлый камень на 2x2 клетки: на него можно заползти и греться.</summary>
        private void BuildSunStones(float cs)
        {
            if (_sunStoneMaterial == null) return;
            for (int i = 0; i < _grid.SunStoneCount; i++)
            {
                Vector2Int cell = _grid.GetSunStone(i);
                Vector3 center = (_grid.CellToWorld(cell) + _grid.CellToWorld(new Vector2Int(cell.x + 1, cell.y + 1))) * 0.5f;
                float yaw = Hash(cell.x, cell.y) * 360f;
                if (_rockMesh != null)
                    CreateMesh("SunStone", _rockMesh, _sunStoneMaterial, center, new Vector3(2.3f * cs, 0.12f * cs, 2f * cs), yaw);
                else
                    CreateShape(PrimitiveType.Cylinder, "SunStone", center, new Vector3(2f * cs, 0.03f * cs, 2f * cs), _sunStoneMaterial);
            }
        }

        /// <summary>Нора: низкий земляной холмик и тёмный лаз, повёрнутый к центру поля.</summary>
        private void BuildBurrows(float cs)
        {
            if (_dirtMaterial == null || _holeMaterial == null) return;
            for (int i = 0; i < _grid.BurrowCount; i++)
            {
                Vector3 pos = _grid.GetBurrow(i);
                Vector3 toCenter = _grid.Center - pos;
                toCenter.y = 0f;
                Quaternion face = Quaternion.LookRotation(toCenter.sqrMagnitude > 0.01f ? toCenter.normalized : Vector3.forward);
                Vector3 back = face * Vector3.back;

                Renderer mound = CreateShape(PrimitiveType.Sphere, "BurrowMound", pos + back * (0.35f * cs), new Vector3(1.9f * cs, 0.55f * cs, 1.7f * cs), _dirtMaterial);
                mound.transform.rotation = face;
                Renderer lip = CreateShape(PrimitiveType.Sphere, "BurrowLip", pos + face * Vector3.forward * (0.2f * cs), new Vector3(1.1f * cs, 0.3f * cs, 0.7f * cs), _dirtMaterial);
                lip.transform.rotation = face;
                Renderer hole = CreateShape(PrimitiveType.Sphere, "BurrowHole", pos + face * new Vector3(0f, 0.12f * cs, 0.42f * cs), new Vector3(0.62f * cs, 0.36f * cs, 0.3f * cs), _holeMaterial);
                hole.transform.rotation = face * Quaternion.Euler(-20f, 0f, 0f);
                hole.shadowCastingMode = ShadowCastingMode.Off;
                // Насыпанная вокруг земля.
                for (int k = 0; k < 5; k++)
                {
                    Quaternion around = face * Quaternion.Euler(0f, 120f + k * 30f + Hash(i, k) * 12f, 0f);
                    CreateShape(PrimitiveType.Sphere, "Clod", pos + around * Vector3.forward * (0.95f * cs),
                        new Vector3(0.35f * cs, 0.16f * cs, 0.3f * cs) * (0.8f + 0.4f * Hash(k, i)), _dirtMaterial);
                }
            }
        }

        /// <summary>Заросли: несколько пучков на клетку со случайным сдвигом, все в одном меше — один вызов отрисовки.</summary>
        private void BuildTallGrass(float cs)
        {
            if (_grassTuftMesh == null || _tallGrassMaterial == null) return;
            int count = _grid.CoverCellCount * _tuftsPerCell;
            if (count == 0) return;

            var combine = new CombineInstance[count];
            int k = 0;
            Vector3 origin = transform.position;
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_grid.IsCover(cell)) continue;
                    Vector3 c = _grid.CellToWorld(cell) - origin;
                    for (int t = 0; t < _tuftsPerCell; t++)
                    {
                        float hx = Hash(x * 7 + t, y * 3 + 1) - 0.5f;
                        float hz = Hash(x * 5 + 2, y * 11 + t) - 0.5f;
                        float hs = Hash(x + t * 13, y + 5);
                        var pos = c + new Vector3(hx * 0.8f * cs, -0.02f, hz * 0.8f * cs);
                        float width = (0.9f + 0.4f * hs) * cs;
                        var scale = new Vector3(width, _tallGrassHeight * (0.8f + 0.4f * hs) * cs, width);
                        combine[k++] = new CombineInstance
                        {
                            mesh = _grassTuftMesh,
                            transform = Matrix4x4.TRS(pos, Quaternion.Euler(0f, hs * 360f + t * 40f, 0f), scale)
                        };
                    }
                }
            }

            var mesh = new Mesh { name = "TallGrass" };
            if (count * _grassTuftMesh.vertexCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();

            var go = new GameObject("TallGrass");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _tallGrassMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private bool IsType(int x, int y, ObstacleType type)
        {
            var cell = new Vector2Int(x, y);
            return _grid.IsInside(cell) && _grid.GetObstacle(cell) == type;
        }

        /// <summary>Детерминированный «случайный» 0..1 по координатам клетки: поле одинаково при каждой сборке.</summary>
        private static float Hash(int x, int y)
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

        private Renderer CreateMesh(string objectName, Mesh mesh, Material mat, Vector3 worldPos, Vector3 scale, float yaw)
        {
            var go = new GameObject(objectName);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = scale;
            go.transform.SetParent(transform, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return r;
        }

        private Renderer CreateShape(PrimitiveType type, string objectName, Vector3 worldPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            // Физика не используется: столкновения считает логика по данным сетки.
            DestroySafe(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
            go.transform.localScale = scale;
            go.transform.SetParent(transform, true);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return r;
        }

        private static void DestroySafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
