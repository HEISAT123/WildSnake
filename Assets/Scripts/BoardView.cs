using UnityEngine;
using UnityEngine.Rendering;

namespace Snake3D
{
    /// <summary>
    /// Визуал поля: пол одним объектом с шахматной текстурой, земля вокруг, стены по периметру
    /// и препятствия из раскладки GridManager. Строится один раз (в редакторе SnakeSceneBuilder'ом
    /// или в Start, если раскладку поменяли в инспекторе).
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

        [Tooltip("Низкие стенки (перепрыгиваемые)")]
        [SerializeField] private Material _lowWallMaterial;

        [Tooltip("Колонны")]
        [SerializeField] private Material _pillarMaterial;

        [Tooltip("Большие блоки")]
        [SerializeField] private Material _blockMaterial;

        [Header("Размеры")]
        [Tooltip("Толщина пола")]
        [SerializeField, Min(0.01f)] private float _floorThickness = 0.2f;

        [Tooltip("Высота стен по периметру над поверхностью поля")]
        [SerializeField, Min(0.05f)] private float _wallHeight = 0.8f;

        [Tooltip("Толщина стен (доля клетки)")]
        [SerializeField, Min(0.05f)] private float _wallThickness = 0.5f;

        [Tooltip("Насколько земля выходит за поле, клеток")]
        [SerializeField, Min(0f)] private float _groundPadding = 40f;

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
                    if (type == ObstacleType.None) continue;

                    float h = _grid.GetObstacleHeight(cell);
                    Vector3 basePos = _grid.CellToWorld(cell);
                    switch (type)
                    {
                        case ObstacleType.Pillar:
                        {
                            // Античная колонна: база, ствол (цилиндр Unity высотой 2 при scale.y = 1) и капитель.
                            float d = _grid.GetObstacleHalfSize(cell) * 2f;
                            float plinth = 0.14f * cs;
                            CreateShape(PrimitiveType.Cube, "PillarBase", basePos + Vector3.up * (plinth * 0.5f), new Vector3(d * 1.25f, plinth, d * 1.25f), _pillarMaterial);
                            CreateShape(PrimitiveType.Cylinder, "Pillar", basePos + Vector3.up * (h * 0.5f), new Vector3(d * 0.85f, h * 0.5f, d * 0.85f), _pillarMaterial);
                            CreateShape(PrimitiveType.Cylinder, "PillarNeck", basePos + Vector3.up * (h - plinth * 1.4f), new Vector3(d, plinth * 0.4f, d), _pillarMaterial);
                            CreateShape(PrimitiveType.Cube, "PillarCapital", basePos + Vector3.up * (h - plinth * 0.5f), new Vector3(d * 1.25f, plinth, d * 1.25f), _pillarMaterial);
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
