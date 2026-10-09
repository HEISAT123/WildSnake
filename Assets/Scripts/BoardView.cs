using UnityEngine;
using UnityEngine.Rendering;

namespace Snake3D
{
    /// <summary>
    /// Визуал поля: шахматные плитки, стены по периметру и препятствия из раскладки GridManager.
    /// Строится один раз (в редакторе SnakeSceneBuilder'ом или в Start, если раскладку поменяли в инспекторе).
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Сетка, по которой строится поле")]
        [SerializeField] private GridManager _grid;

        [Header("Материалы")]
        [Tooltip("Светлые клетки")]
        [SerializeField] private Material _lightTileMaterial;

        [Tooltip("Тёмные клетки")]
        [SerializeField] private Material _darkTileMaterial;

        [Tooltip("Стены по периметру")]
        [SerializeField] private Material _wallMaterial;

        [Tooltip("Низкие стенки (перепрыгиваемые)")]
        [SerializeField] private Material _lowWallMaterial;

        [Tooltip("Колонны")]
        [SerializeField] private Material _pillarMaterial;

        [Tooltip("Большие блоки")]
        [SerializeField] private Material _blockMaterial;

        [Header("Размеры")]
        [Tooltip("Толщина плитки клетки")]
        [SerializeField, Min(0.01f)] private float _tileHeight = 0.2f;

        [Tooltip("Зазор между плитками (доля клетки)")]
        [SerializeField, Range(0f, 0.2f)] private float _tileGap = 0.02f;

        [Tooltip("Высота стен по периметру над поверхностью поля")]
        [SerializeField, Min(0.05f)] private float _wallHeight = 0.8f;

        [Tooltip("Толщина стен (доля клетки)")]
        [SerializeField, Min(0.05f)] private float _wallThickness = 0.5f;

        // Запоминаем, для какой раскладки построено поле, чтобы не пересоздавать его без нужды.
        [SerializeField, HideInInspector] private int _builtLayoutVersion;
        [SerializeField, HideInInspector] private bool _isBuilt;

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
            BuildTiles(cs);
            BuildWalls(cs);
            BuildObstacles(cs);

            _builtLayoutVersion = _grid.LayoutVersion;
            _isBuilt = true;
        }

        private void BuildTiles(float cs)
        {
            float tileSize = cs * (1f - _tileGap);
            var tileScale = new Vector3(tileSize, _tileHeight, tileSize);
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    Vector3 pos = _grid.CellToWorld(new Vector2Int(x, y)) + Vector3.down * (_tileHeight * 0.5f);
                    Material mat = ((x + y) & 1) == 0 ? _lightTileMaterial : _darkTileMaterial;
                    Renderer r = CreateShape(PrimitiveType.Cube, "Tile", pos, tileScale, mat);
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        private void BuildWalls(float cs)
        {
            float halfW = _grid.Width * cs * 0.5f;
            float halfH = _grid.Height * cs * 0.5f;
            float t = _wallThickness * cs;
            float wallY = (_wallHeight - _tileHeight) * 0.5f;
            float wallH = _wallHeight + _tileHeight;
            Vector3 c = _grid.Center;

            CreateShape(PrimitiveType.Cube, "Wall_North", c + new Vector3(0f, wallY, halfH + t * 0.5f), new Vector3(halfW * 2f + t * 2f, wallH, t), _wallMaterial);
            CreateShape(PrimitiveType.Cube, "Wall_South", c + new Vector3(0f, wallY, -halfH - t * 0.5f), new Vector3(halfW * 2f + t * 2f, wallH, t), _wallMaterial);
            CreateShape(PrimitiveType.Cube, "Wall_East", c + new Vector3(halfW + t * 0.5f, wallY, 0f), new Vector3(t, wallH, halfH * 2f), _wallMaterial);
            CreateShape(PrimitiveType.Cube, "Wall_West", c + new Vector3(-halfW - t * 0.5f, wallY, 0f), new Vector3(t, wallH, halfH * 2f), _wallMaterial);
        }

        private void BuildObstacles(float cs)
        {
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    ObstacleType type = _grid.GetObstacle(cell);
                    if (type == ObstacleType.None) continue;

                    float h = _grid.GetObstacleHeight(cell);
                    Vector3 basePos = _grid.CellToWorld(cell);
                    switch (type)
                    {
                        case ObstacleType.Pillar:
                        {
                            // Цилиндр Unity высотой 2 при scale.y = 1.
                            float d = _grid.GetObstacleHalfSize(cell) * 2f;
                            CreateShape(PrimitiveType.Cylinder, "Pillar", basePos + Vector3.up * (h * 0.5f), new Vector3(d, h * 0.5f, d), _pillarMaterial);
                            break;
                        }
                        case ObstacleType.Block:
                            CreateShape(PrimitiveType.Cube, "Block", basePos + Vector3.up * (h * 0.5f), new Vector3(cs, h, cs), _blockMaterial);
                            break;
                        default:
                            CreateShape(PrimitiveType.Cube, "LowWall", basePos + Vector3.up * (h * 0.5f), new Vector3(cs * 0.96f, h, cs * 0.96f), _lowWallMaterial);
                            break;
                    }
                }
            }
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
