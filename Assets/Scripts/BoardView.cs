using UnityEngine;
using UnityEngine.Rendering;

namespace Snake3D
{
    /// <summary>
    /// Визуал поля: шахматные плитки и невысокие стены. Строится один раз (в редакторе
    /// SnakeSceneBuilder'ом или в Start, если размер сетки поменяли в инспекторе).
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Сетка, по размерам которой строится поле")]
        [SerializeField] private GridManager _grid;

        [Header("Материалы")]
        [Tooltip("Светлые клетки")]
        [SerializeField] private Material _lightTileMaterial;

        [Tooltip("Тёмные клетки")]
        [SerializeField] private Material _darkTileMaterial;

        [Tooltip("Стены по периметру")]
        [SerializeField] private Material _wallMaterial;

        [Header("Размеры")]
        [Tooltip("Толщина плитки клетки")]
        [SerializeField, Min(0.01f)] private float _tileHeight = 0.2f;

        [Tooltip("Зазор между плитками (доля клетки)")]
        [SerializeField, Range(0f, 0.2f)] private float _tileGap = 0.02f;

        [Tooltip("Высота стен над поверхностью поля")]
        [SerializeField, Min(0.05f)] private float _wallHeight = 0.6f;

        [Tooltip("Толщина стен (доля клетки)")]
        [SerializeField, Min(0.05f)] private float _wallThickness = 0.5f;

        // Запоминаем, для какого размера построено поле, чтобы не пересоздавать его без нужды.
        [SerializeField, HideInInspector] private int _builtWidth = -1;
        [SerializeField, HideInInspector] private int _builtHeight = -1;
        [SerializeField, HideInInspector] private float _builtCellSize = -1f;

        private void Start()
        {
            bool sizeChanged = _builtWidth != _grid.Width || _builtHeight != _grid.Height
                               || !Mathf.Approximately(_builtCellSize, _grid.CellSize);
            if (sizeChanged || transform.childCount == 0) Build();
        }

        [ContextMenu("Rebuild Board")]
        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroySafe(transform.GetChild(i).gameObject);

            float cs = _grid.CellSize;
            float tileSize = cs * (1f - _tileGap);
            var tileScale = new Vector3(tileSize, _tileHeight, tileSize);

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Vector3 pos = _grid.CellToWorld(cell) + Vector3.down * (_tileHeight * 0.5f);
                    Material mat = ((x + y) & 1) == 0 ? _lightTileMaterial : _darkTileMaterial;
                    Renderer r = CreateCube("Tile", pos, tileScale, mat);
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            float halfW = _grid.Width * cs * 0.5f;
            float halfH = _grid.Height * cs * 0.5f;
            float t = _wallThickness * cs;
            float wallY = (_wallHeight - _tileHeight) * 0.5f;
            float wallH = _wallHeight + _tileHeight;
            Vector3 c = _grid.Center;

            CreateCube("Wall_North", c + new Vector3(0f, wallY, halfH + t * 0.5f), new Vector3(halfW * 2f + t * 2f, wallH, t), _wallMaterial);
            CreateCube("Wall_South", c + new Vector3(0f, wallY, -halfH - t * 0.5f), new Vector3(halfW * 2f + t * 2f, wallH, t), _wallMaterial);
            CreateCube("Wall_East", c + new Vector3(halfW + t * 0.5f, wallY, 0f), new Vector3(t, wallH, halfH * 2f), _wallMaterial);
            CreateCube("Wall_West", c + new Vector3(-halfW - t * 0.5f, wallY, 0f), new Vector3(t, wallH, halfH * 2f), _wallMaterial);

            _builtWidth = _grid.Width;
            _builtHeight = _grid.Height;
            _builtCellSize = cs;
        }

        private Renderer CreateCube(string objectName, Vector3 worldPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objectName;
            // Физика не используется: столкновения считает дискретная логика сетки.
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
