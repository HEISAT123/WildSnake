using UnityEngine;
using UnityEngine.Rendering;

namespace Snake3D
{
    /// <summary>
    /// Визуал поля: пол одним объектом, стены по периметру и «дикие» препятствия из раскладки GridManager:
    /// низкие стенки — поваленные брёвна, колонны — сломанные пни и стоячие камни, блоки — валуны;
    /// плюс заросли высокой травы.
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
            BuildFloor(cs);
            BuildWalls(cs);
            BuildObstacles(cs);
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
                            if (Hash(x, y) < 0.55f) BuildStump(cell, cs);
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
