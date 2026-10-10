using UnityEngine;
using UnityEngine.Rendering;

namespace Snake3D
{
    /// <summary>
    /// Визуал мира поверх рельефа (сам рельеф с горами-границей строит EnvironmentBuilder) — «дикие» препятствия
    /// из раскладки GridManager: низкие стенки — поваленные брёвна, колонны — сломанные пни и стоячие камни (на гряде),
    /// сухие деревья (в болоте), блоки — валуны; в рощах — деревья и берёзы с кронами, лесная подстилка, папоротники
    /// и грибы; плюс высокая трава и камыш с рогозом, озеро и пруды, текущая река с брёвнами-мостами, болотные бочаги
    /// с кувшинками, полые брёвна, галька на гряде, птичьи гнёзда с яйцами, солнечные камни, норы и гнездо
    /// (кладка — главная цель; яйца и маяк над гнездом включает GameManager).
    /// Строится один раз (в редакторе SnakeSceneBuilder'ом или в Start, если раскладку поменяли в инспекторе).
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Сетка, по которой строится поле")]
        [SerializeField] private GridManager _grid;

        [Header("Материалы")]
        [Tooltip("Кора: брёвна и пни")]
        [SerializeField] private Material _barkMaterial;

        [Tooltip("Светлый спил на верхушке пня")]
        [SerializeField] private Material _stumpTopMaterial;

        [Tooltip("Валуны")]
        [SerializeField] private Material _blockMaterial;

        [Tooltip("Высокая трава (укрытия)")]
        [SerializeField] private Material _tallGrassMaterial;

        [Tooltip("Вода озера и прудов (полупрозрачная)")]
        [SerializeField] private Material _waterMaterial;

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

        [Header("Река, болото, березняк")]
        [Tooltip("Вода реки (рябь течёт — её гонит WaterFlow)")]
        [SerializeField] private Material _riverWaterMaterial;

        [Tooltip("Мутная вода болотных бочагов")]
        [SerializeField] private Material _swampWaterMaterial;

        [Tooltip("Кора берёзы")]
        [SerializeField] private Material _birchBarkMaterial;

        [Tooltip("Листва берёзы")]
        [SerializeField] private Material _birchLeavesMaterial;

        [Tooltip("Серая кора сухих деревьев болота")]
        [SerializeField] private Material _deadBarkMaterial;

        [Tooltip("Камыш")]
        [SerializeField] private Material _reedMaterial;

        [Tooltip("Початки рогоза")]
        [SerializeField] private Material _cattailMaterial;

        [Tooltip("Папоротник")]
        [SerializeField] private Material _fernMaterial;

        [Tooltip("Шляпки грибов")]
        [SerializeField] private Material _mushroomCapMaterial;

        [Tooltip("Ножки грибов")]
        [SerializeField] private Material _mushroomStemMaterial;

        [Tooltip("Листья кувшинок")]
        [SerializeField] private Material _lilyPadMaterial;

        [Tooltip("Яйца в птичьих гнёздах")]
        [SerializeField] private Material _birdEggMaterial;

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
        [Tooltip("Высота пня, клеток")]
        [SerializeField, Min(0.5f)] private float _stumpHeight = 1.5f;

        [Tooltip("Пучков высокой травы на клетку укрытия")]
        [SerializeField, Range(1, 6)] private int _tuftsPerCell = 3;

        [Tooltip("Высота высокой травы, клеток (ниже камеры, чтобы не закрывала змею)")]
        [SerializeField, Min(0.2f)] private float _tallGrassHeight = 0.85f;

        // Запоминаем, для какой раскладки построено поле, чтобы не пересоздавать его без нужды.
        [SerializeField, HideInInspector] private int _builtLayoutVersion;
        [SerializeField, HideInInspector] private bool _isBuilt;
        [SerializeField, HideInInspector] private Renderer[] _crowns = new Renderer[0];
        [SerializeField, HideInInspector] private Renderer[] _occluders = new Renderer[0];
        [SerializeField, HideInInspector] private GameObject _nestEggs;
        [SerializeField, HideInInspector] private GameObject _nestBeacon;
        [SerializeField, HideInInspector] private Renderer[] _birdEggs = new Renderer[0];

        /// <summary>Яйца птичьих гнёзд: по три на гнездо, по порядку гнёзд GridManager.</summary>
        public Renderer[] BirdEggs => _birdEggs;

        /// <summary>Кроны деревьев рощ — их прячет CanopyFader, когда они заслоняют змею.</summary>
        public Renderer[] Crowns => _crowns;

        /// <summary>Стволы, пни, камни и брёвна — их CanopyFader прячет, когда они перед самой камерой или заслоняют змею.</summary>
        public Renderer[] Occluders => _occluders;

        /// <summary>Яйца в гнезде (скрыты, пока змея их не отложит).</summary>
        public GameObject NestEggs => _nestEggs;

        /// <summary>Светящийся столб над гнездом (виден, когда змея готова к кладке).</summary>
        public GameObject NestBeacon => _nestBeacon;

        private readonly System.Collections.Generic.List<Renderer> _crownList = new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.List<Renderer> _occluderList = new System.Collections.Generic.List<Renderer>();

        private void Start()
        {
            if (!_isBuilt || _builtLayoutVersion != _grid.LayoutVersion || transform.childCount == 0) Build();
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
            _occluderList.Clear();
            BuildObstacles(cs);
            BuildHollowLogs(cs);
            BuildBridges(cs);
            _crowns = _crownList.ToArray();
            _occluders = _occluderList.ToArray();
            BuildForestFloor(cs);
            BuildNest(cs);
            BuildBirdNests(cs);
            BuildPond(cs);
            if (_riverWaterMaterial != null) BuildWaterSheet("RiverWater", true, _riverWaterMaterial, cs);
            if (_swampWaterMaterial != null) BuildWaterSheet("SwampWater", false, _swampWaterMaterial, cs);
            BuildDetails(cs);
            BuildSunStones(cs);
            BuildBurrows(cs);
            BuildTallGrass(cs);

            _builtLayoutVersion = _grid.LayoutVersion;
            _isBuilt = true;
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
                            else if (_grid.IsSwamp(cell)) BuildDeadTree(cell, cs);
                            else if (!_grid.IsRidge(cell) && Hash(x, y) < 0.55f) BuildStump(cell, cs);
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

            Occlude(CreateShape(PrimitiveType.Cylinder, "Stump", basePos + Vector3.up * (h * 0.5f), new Vector3(d * 0.95f, h * 0.5f, d * 0.95f), _barkMaterial));
            Occlude(CreateShape(PrimitiveType.Cylinder, "StumpTop", basePos + Vector3.up * h, new Vector3(d * 0.86f, 0.012f * cs, d * 0.86f), _stumpTopMaterial));

            // Обломок ствола торчит над спилом — силуэт сломанного дерева, а не ровного столба.
            float shard = h * 0.5f;
            Renderer s = Occlude(CreateShape(PrimitiveType.Cylinder, "Shard", basePos + turn * new Vector3(d * 0.2f, 0f, 0f) + Vector3.up * (h + shard * 0.35f),
                new Vector3(d * 0.5f, shard * 0.5f, d * 0.38f), _barkMaterial));
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
            if (_grid.IsBirch(cell) && _birchBarkMaterial != null && _birchLeavesMaterial != null)
            {
                BuildBirch(basePos, s, yaw, d, cs);
                return;
            }
            bool pine = Hash(cell.x + 7, cell.y + 3) < 0.4f && _pineMesh != null;

            float trunk = (pine ? 3.2f : 3.6f) * cs * s;
            // Ствол чуть уходит в землю — на склоне не висит в воздухе.
            Occlude(CreateShape(PrimitiveType.Cylinder, "Trunk", basePos + Vector3.up * (trunk * 0.5f - 0.3f * cs),
                new Vector3(d, trunk * 0.5f + 0.3f * cs, d), _barkMaterial));

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

        private Renderer Occlude(Renderer r)
        {
            _occluderList.Add(r);
            return r;
        }

        /// <summary>Нормаль рельефа в точке — по разности высот вокруг.</summary>
        private Vector3 GroundNormal(Vector3 p, float cs)
        {
            float hx = _grid.GroundHeight(p + Vector3.right * (0.5f * cs)) - _grid.GroundHeight(p - Vector3.right * (0.5f * cs));
            float hz = _grid.GroundHeight(p + Vector3.forward * (0.5f * cs)) - _grid.GroundHeight(p - Vector3.forward * (0.5f * cs));
            return new Vector3(-hx, cs, -hz).normalized;
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
                    Vector3 world = _grid.CellToWorld(cell);
                    Vector3 normal = GroundNormal(world, cs);
                    Vector3 c = world - origin + normal * (0.03f + Hash(x, y) * 0.006f);
                    float size = (1.7f + 0.6f * Hash(y, x)) * cs;
                    // Пятно листвы лежит по склону.
                    Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(90f, Hash(x + 11, y) * 360f, 0f);
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
                Occlude(CreateShape(PrimitiveType.Cube, "Stone", basePos + Vector3.up * (h * 0.5f), new Vector3(d, h, d), _blockMaterial));
                return;
            }
            Occlude(CreateMesh("StandingStone", _rockMesh, _blockMaterial, basePos + Vector3.up * (h * 0.45f), new Vector3(d * 1.35f, h, d * 1.15f), yaw));
        }

        private void BuildBoulder(Vector2Int cell, float cs)
        {
            Vector3 center = (_grid.CellToWorld(cell) + _grid.CellToWorld(new Vector2Int(cell.x + 1, cell.y + 1))) * 0.5f;
            float h = _grid.GetObstacleHeight(cell);
            float yaw = Hash(cell.x, cell.y) * 360f;
            if (_rockMesh == null)
            {
                Occlude(CreateShape(PrimitiveType.Cube, "Block", center + Vector3.up * (h * 0.5f), new Vector3(2f * cs, h, 2f * cs), _blockMaterial));
                return;
            }
            // Чуть шире квадрата 2x2, чтобы углы столкновения не торчали из камня.
            Occlude(CreateMesh("Boulder", _rockMesh, _blockMaterial, center + Vector3.up * (h * 0.42f), new Vector3(2.5f * cs, h * 1.25f, 2.5f * cs), yaw));
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
            // Цилиндр Unity вытянут по Y: кладём его вдоль бревна — с наклоном по склону — и чуть поворачиваем вокруг оси.
            Vector3 along = _grid.CellToWorld(last) - _grid.CellToWorld(first);
            if (along.sqrMagnitude < 1e-4f) along = alongX ? Vector3.right : Vector3.forward;
            Quaternion lay = Quaternion.FromToRotation(Vector3.up, along.normalized);
            Renderer log = Occlude(CreateShape(PrimitiveType.Cylinder, "Log", center + Vector3.up * (diameter * 0.45f),
                new Vector3(diameter, (along.magnitude + cs) * 0.5f, diameter), _barkMaterial));
            log.transform.rotation = lay * Quaternion.Euler(0f, Hash(x, y) * 360f, 0f);
        }

        /// <summary>
        /// Озеро и пруды — «веер» воды по форме берега из GridManager на уровне воды водоёма;
        /// дно-чашу уже вырезал рельеф. Форма берега совпадает с водными клетками логики.
        /// </summary>
        private void BuildPond(float cs)
        {
            if (_waterMaterial == null) return;
            for (int p = 0; p < _grid.PondCount; p++)
            {
                Vector3 center = _grid.GetPondCenter(p) - transform.position;
                CreateFan("Water_" + p, p, center + Vector3.up * 0.02f, 0.6f * cs, 1.08f, _waterMaterial);
            }
        }

        private void CreateFan(string objectName, int pond, Vector3 localCenter, float pad, float scale, Material mat)
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
                float r = _grid.PondEdgeWorld(pond, a) * scale + pad;
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
            int count = 0;
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                    if (_grid.IsCover(new Vector2Int(x, y)) && !_grid.IsReed(new Vector2Int(x, y)) && !_grid.IsHollow(new Vector2Int(x, y))) count += _tuftsPerCell;
            if (count == 0) return;

            var combine = new CombineInstance[count];
            int k = 0;
            Vector3 origin = transform.position;
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_grid.IsCover(cell) || _grid.IsReed(cell) || _grid.IsHollow(cell)) continue;
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

        // ---------------- Река, болото, лес: подробности ----------------

        /// <summary>Берёза: высокий белый ствол и несколько лёгких светлых комков листвы.</summary>
        private void BuildBirch(Vector3 basePos, float s, float yaw, float d, float cs)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            float trunk = 4.6f * cs * s;
            Occlude(CreateShape(PrimitiveType.Cylinder, "BirchTrunk", basePos + Vector3.up * (trunk * 0.5f - 0.3f * cs),
                new Vector3(d * 0.75f, trunk * 0.5f + 0.3f * cs, d * 0.75f), _birchBarkMaterial));
            Vector3[] offsets = { new Vector3(0f, 4.4f, 0f), new Vector3(0.6f, 5.2f, 0.2f), new Vector3(-0.5f, 4.9f, -0.4f), new Vector3(0.1f, 5.9f, -0.1f) };
            Vector3[] sizes = { new Vector3(2.5f, 2.6f, 2.5f), new Vector3(1.8f, 2f, 1.8f), new Vector3(1.9f, 2.1f, 1.9f), new Vector3(1.4f, 1.7f, 1.4f) };
            for (int t = 0; t < offsets.Length; t++)
                AddCrown(CreateMesh("BirchCrown", _crownMesh, _birchLeavesMaterial, basePos + turn * (offsets[t] * (cs * s)), sizes[t] * (cs * s), yaw + t * 50f));
        }

        /// <summary>Сухое дерево болота: серый ствол с обломанной верхушкой и голыми ветками.</summary>
        private void BuildDeadTree(Vector2Int cell, float cs)
        {
            Material bark = _deadBarkMaterial != null ? _deadBarkMaterial : _barkMaterial;
            Vector3 basePos = _grid.CellToWorld(cell);
            float s = 0.8f + 0.5f * Hash(cell.y, cell.x);
            float d = _grid.GetObstacleHalfSize(cell) * 1.6f;
            float h = 2.8f * cs * s;
            float yaw = Hash(cell.x, cell.y) * 360f;
            Occlude(CreateShape(PrimitiveType.Cylinder, "DeadTrunk", basePos + Vector3.up * (h * 0.5f - 0.2f * cs),
                new Vector3(d, h * 0.5f + 0.2f * cs, d), bark));
            for (int k = 0; k < 3; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, yaw + k * 120f + Hash(cell.x + k, cell.y) * 40f, 0f) * Vector3.forward;
                Vector3 v = (dir * Mathf.Cos(40f * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(40f * Mathf.Deg2Rad)).normalized;
                float len = (0.7f + 0.5f * Hash(k, cell.x)) * cs * s;
                Vector3 start = basePos + Vector3.up * (h * (0.5f + 0.15f * k));
                Renderer branch = Occlude(CreateShape(PrimitiveType.Cylinder, "DeadBranch", start + v * (len * 0.5f),
                    new Vector3(0.09f * cs, len * 0.5f, 0.09f * cs), bark));
                branch.transform.rotation = Quaternion.FromToRotation(Vector3.up, v);
            }
        }

        /// <summary>Полое бревно-туннель: толстый ствол лёжа и тёмные дыры на торцах.</summary>
        private void BuildHollowLogs(float cs)
        {
            float r = 0.52f * cs;
            for (int i = 0; i < _grid.HollowLogCount; i++)
            {
                _grid.GetHollowLog(i, out Vector3 a, out Vector3 b);
                Vector3 pa = a + Vector3.up * r;
                Vector3 pb = b + Vector3.up * r;
                Vector3 dir = (pb - pa).normalized;
                float length = (pb - pa).magnitude + cs;
                Vector3 center = (pa + pb) * 0.5f;
                Quaternion lay = Quaternion.FromToRotation(Vector3.up, dir);
                Renderer log = Occlude(CreateShape(PrimitiveType.Cylinder, "HollowLog", center, new Vector3(r * 2f, length * 0.5f, r * 2f), _barkMaterial));
                log.transform.rotation = lay * Quaternion.Euler(0f, Hash(i, 7) * 360f, 0f);
                if (_holeMaterial == null) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    Renderer hole = CreateShape(PrimitiveType.Cylinder, "HollowEnd", center + dir * (side * (length * 0.5f + 0.004f)),
                        new Vector3(r * 1.6f, 0.006f, r * 1.6f), _holeMaterial);
                    hole.transform.rotation = lay;
                    hole.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        /// <summary>Брёвна-мосты через реку: лежат концами на берегах.</summary>
        private void BuildBridges(float cs)
        {
            float r = 0.25f * cs;
            for (int i = 0; i < _grid.BridgeCount; i++)
            {
                _grid.GetBridge(i, out Vector3 a, out Vector3 b);
                Vector3 pa = a + Vector3.down * r;
                Vector3 pb = b + Vector3.down * r;
                Vector3 dir = (pb - pa).normalized;
                Renderer log = Occlude(CreateShape(PrimitiveType.Cylinder, "BridgeLog", (pa + pb) * 0.5f,
                    new Vector3(r * 2f, ((pb - pa).magnitude + 0.6f * cs) * 0.5f, r * 2f), _barkMaterial));
                log.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            }
        }

        /// <summary>
        /// Вода реки или болотных бочагов — «простыня» по клеткам с уровнем в углах (среднее соседей), с запасом
        /// в клетку: кромку прячет берег. У реки UV идёт вдоль русла — WaterFlow гонит рябь вниз по течению.
        /// </summary>
        private void BuildWaterSheet(string objectName, bool river, Material mat, float cs)
        {
            int w = _grid.Width, h = _grid.Height;
            var draw = new bool[w * h];
            bool any = false;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!IsSheetWater(x, y, river)) continue;
                    any = true;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            if (_grid.IsPondWater(new Vector2Int(nx, ny))) continue;
                            draw[ny * w + nx] = true;
                        }
                    }
                }
            }
            if (!any) return;

            int stride = w + 1;
            var level = new float[stride * (h + 1)];
            var known = new bool[level.Length];
            for (int cy = 0; cy <= h; cy++)
            {
                for (int cx = 0; cx <= w; cx++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int y = cy - 1; y <= cy; y++)
                    {
                        for (int x = cx - 1; x <= cx; x++)
                        {
                            if (x < 0 || y < 0 || x >= w || y >= h || !IsSheetWater(x, y, river)) continue;
                            sum += _grid.WaterLevelAt(_grid.CellToWorld(new Vector2Int(x, y)));
                            n++;
                        }
                    }
                    if (n == 0) continue;
                    level[cy * stride + cx] = sum / n;
                    known[cy * stride + cx] = true;
                }
            }
            // Углы кромки берут уровень соседних углов.
            for (int pass = 0; pass < 3; pass++)
            {
                var next = (bool[])known.Clone();
                for (int cy = 0; cy <= h; cy++)
                {
                    for (int cx = 0; cx <= w; cx++)
                    {
                        int i = cy * stride + cx;
                        if (known[i]) continue;
                        float sum = 0f;
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = cx + dx, ny = cy + dy;
                                if (nx < 0 || ny < 0 || nx > w || ny > h || !known[ny * stride + nx]) continue;
                                sum += level[ny * stride + nx];
                                n++;
                            }
                        }
                        if (n == 0) continue;
                        level[i] = sum / n;
                        next[i] = true;
                    }
                }
                known = next;
            }

            var index = new int[level.Length];
            for (int i = 0; i < index.Length; i++) index[i] = -1;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            Vector3 origin = _grid.CornerOrigin;
            Vector3 local = transform.position;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!draw[y * w + x]) continue;
                    int a = Corner(x, y), b = Corner(x + 1, y), c = Corner(x, y + 1), d = Corner(x + 1, y + 1);
                    if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                    triangles.Add(a); triangles.Add(d); triangles.Add(b);
                    triangles.Add(a); triangles.Add(c); triangles.Add(d);
                }
            }
            if (triangles.Count == 0) return;

            int Corner(int cx, int cy)
            {
                int i = cy * stride + cx;
                if (!known[i]) return -1;
                if (index[i] >= 0) return index[i];
                var p = new Vector3(origin.x + cx * cs, level[i], origin.z + cy * cs);
                index[i] = vertices.Count;
                vertices.Add(p - local);
                if (river)
                {
                    float along = _grid.RiverCoordinate(p, out float lateral);
                    uvs.Add(new Vector2(along * 0.22f, lateral * 0.3f));
                }
                else
                {
                    uvs.Add(new Vector2(p.x * 0.25f, p.z * 0.25f));
                }
                return index[i];
            }

            var mesh = new Mesh { name = objectName };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (river)
            {
                var flow = go.AddComponent<WaterFlow>();
                flow.Target = r;
            }
        }

        private bool IsSheetWater(int x, int y, bool river)
        {
            var cell = new Vector2Int(x, y);
            if (!_grid.IsWater(cell) || _grid.IsPondWater(cell)) return false;
            return _grid.IsRiver(cell) == river;
        }

        /// <summary>Камыш с початками рогоза по берегам и в болоте, папоротники и грибы в рощах, кувшинки, галька на гряде.</summary>
        private void BuildDetails(float cs)
        {
            Mesh cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            Mesh sphere = PrimitiveMesh(PrimitiveType.Sphere);
            var reeds = new System.Collections.Generic.List<CombineInstance>();
            var cattails = new System.Collections.Generic.List<CombineInstance>();
            var ferns = new System.Collections.Generic.List<CombineInstance>();
            var caps = new System.Collections.Generic.List<CombineInstance>();
            var stems = new System.Collections.Generic.List<CombineInstance>();
            var pads = new System.Collections.Generic.List<CombineInstance>();
            var pebbles = new System.Collections.Generic.List<CombineInstance>();
            Vector3 origin = transform.position;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Vector3 c = _grid.CellToWorld(cell) - origin;
                    bool free = _grid.GetObstacle(cell) == ObstacleType.None && !_grid.IsWater(cell);

                    if (_grid.IsReed(cell) && _grassTuftMesh != null)
                    {
                        for (int t = 0; t < 2; t++)
                        {
                            float hs = Hash(x * 3 + t, y * 7 + 2);
                            var pos = c + new Vector3((Hash(x + t * 5, y) - 0.5f) * 0.8f * cs, -0.02f, (Hash(x, y + t * 9) - 0.5f) * 0.8f * cs);
                            reeds.Add(Instance(_grassTuftMesh, pos, hs * 360f, new Vector3(0.42f, 1.1f + 0.4f * hs, 0.42f) * cs));
                        }
                        if (Hash(x * 13, y * 3) < 0.55f)
                        {
                            float th = (1.15f + 0.35f * Hash(y, x * 2)) * cs;
                            var pos = c + new Vector3((Hash(x, y * 2) - 0.5f) * 0.6f * cs, th, (Hash(x * 2, y) - 0.5f) * 0.6f * cs);
                            cattails.Add(Instance(cylinder, pos, 0f, new Vector3(0.07f, 0.12f, 0.07f) * cs));
                        }
                    }

                    if (_grid.IsForest(cell) && free && !_grid.IsCover(cell))
                    {
                        float hf = Hash(x * 5 + 1, y * 3 + 7);
                        if (hf < 0.3f && _grassTuftMesh != null)
                        {
                            for (int t = 0; t < 2; t++)
                                ferns.Add(Instance(_grassTuftMesh, c + new Vector3((Hash(x + t, y * 3) - 0.5f) * 0.6f * cs, -0.02f, (Hash(x * 3, y + t) - 0.5f) * 0.6f * cs),
                                    Hash(x + t * 7, y) * 360f, new Vector3(1.1f, 0.45f, 1.1f) * cs));
                        }
                        else if (hf > 0.93f)
                        {
                            int count = 1 + (int)(Hash(y * 3, x) * 3f);
                            for (int k = 0; k < count; k++)
                            {
                                float sz = (0.8f + 0.5f * Hash(x + k, y * 5)) * cs;
                                var pos = c + new Vector3((Hash(x * 7 + k, y) - 0.5f) * 0.7f * cs, 0f, (Hash(x, y * 7 + k) - 0.5f) * 0.7f * cs);
                                stems.Add(Instance(cylinder, pos + Vector3.up * (0.05f * sz), 0f, new Vector3(0.05f, 0.05f, 0.05f) * sz));
                                caps.Add(Instance(sphere, pos + Vector3.up * (0.11f * sz), 0f, new Vector3(0.17f, 0.08f, 0.17f) * sz));
                            }
                        }
                    }

                    if (_grid.IsWater(cell) && !_grid.IsRiver(cell) && Hash(x * 11, y * 13) < (_grid.IsPondWater(cell) ? 0.08f : 0.35f))
                    {
                        // Кувшинки — только у берега, где вода тихая.
                        if (_grid.IsPondWater(cell) && !NearLand(cell)) continue;
                        float level = _grid.WaterLevelAt(_grid.CellToWorld(cell));
                        var pos = new Vector3(c.x + (Hash(x, y * 5) - 0.5f) * 0.5f * cs, level - origin.y + 0.015f, c.z + (Hash(x * 5, y) - 0.5f) * 0.5f * cs);
                        pads.Add(Instance(cylinder, pos, Hash(y, x) * 360f, new Vector3(0.42f, 0.004f, 0.42f) * cs));
                    }

                    if (_grid.IsRidge(cell) && free && !_grid.IsSunStone(cell) && _rockMesh != null && Hash(x * 3, y * 11) < 0.45f)
                    {
                        float sz = (0.18f + 0.3f * Hash(x, y * 3)) * cs;
                        var pos = c + new Vector3((Hash(x * 9, y) - 0.5f) * 0.7f * cs, sz * 0.15f, (Hash(x, y * 9) - 0.5f) * 0.7f * cs);
                        pebbles.Add(Instance(_rockMesh, pos, Hash(y * 3, x) * 360f, new Vector3(sz * 1.3f, sz * 0.7f, sz)));
                    }
                }
            }

            Combined("Reeds", reeds, _reedMaterial, false);
            Combined("Cattails", cattails, _cattailMaterial, false);
            Combined("Ferns", ferns, _fernMaterial, false);
            Combined("MushroomStems", stems, _mushroomStemMaterial, false);
            Combined("MushroomCaps", caps, _mushroomCapMaterial, false);
            Combined("LilyPads", pads, _lilyPadMaterial, false);
            Combined("Pebbles", pebbles, _blockMaterial, true);
        }

        private bool NearLand(Vector2Int cell)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = new Vector2Int(cell.x + dx, cell.y + dy);
                    if (_grid.IsInside(n) && !_grid.IsWater(n)) return true;
                }
            return false;
        }

        /// <summary>Птичьи гнёзда: чашка из веточек и три яйца (их прячет BirdNests, когда змея их съест).</summary>
        private void BuildBirdNests(float cs)
        {
            var eggs = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < _grid.BirdNestCount; i++)
            {
                Vector3 pos = _grid.GetBirdNest(i);
                var root = new GameObject("BirdNest");
                root.transform.SetParent(transform, false);
                root.transform.position = pos;
                if (_dirtMaterial != null)
                    Parent(CreateShape(PrimitiveType.Sphere, "BirdNestBowl", pos + Vector3.up * (0.02f * cs), new Vector3(0.75f, 0.12f, 0.75f) * cs, _dirtMaterial), root);
                for (int k = 0; k < 10; k++)
                {
                    float a = k * Mathf.PI * 2f / 10f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Renderer twig = CreateShape(PrimitiveType.Cylinder, "Twig", pos + dir * (0.32f * cs) + Vector3.up * (0.08f * cs),
                        new Vector3(0.04f, 0.2f, 0.04f) * cs, _barkMaterial);
                    twig.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 70f + 30f * Hash(k, i));
                    Parent(twig, root);
                }
                if (_birdEggMaterial == null) continue;
                for (int k = 0; k < 3; k++)
                {
                    float a = k * 2.1f + i;
                    Vector3 p = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.1f * cs) + Vector3.up * (0.1f * cs);
                    Renderer egg = CreateShape(PrimitiveType.Sphere, "BirdEgg", p, new Vector3(0.12f, 0.16f, 0.12f) * cs, _birdEggMaterial);
                    egg.transform.rotation = Quaternion.Euler(65f, a * Mathf.Rad2Deg, 0f);
                    Parent(egg, root);
                    eggs.Add(egg);
                }
            }
            _birdEggs = eggs.ToArray();
        }

        private static CombineInstance Instance(Mesh mesh, Vector3 localPos, float yaw, Vector3 scale)
        {
            return new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(localPos, Quaternion.Euler(0f, yaw, 0f), scale) };
        }

        private void Combined(string objectName, System.Collections.Generic.List<CombineInstance> parts, Material mat, bool shadows)
        {
            if (parts.Count == 0 || mat == null) return;
            var mesh = new Mesh { name = objectName, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroySafe(temp);
            return mesh;
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
