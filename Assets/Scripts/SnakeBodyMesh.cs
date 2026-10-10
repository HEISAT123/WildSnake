using UnityEngine;
using UnityEngine.Rendering;

namespace Snake3D
{
    /// <summary>
    /// Гладкое тело змеи: процедурная «трубка» вдоль траектории головы.
    /// Кольца вершин ставятся с шагом _ringSpacing по позвоночнику; радиус задаёт профиль
    /// (тонкая шея → толстое тело → острый хвост), сечение слегка приплюснуто, как у настоящей змеи.
    /// Все буферы выделяются один раз, каждый кадр только перезаписываются.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SnakeBodyMesh : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Змейка, по траектории которой строится тело")]
        [SerializeField] private SnakeController _snake;

        [Header("Форма")]
        [Tooltip("Количество граней по окружности")]
        [SerializeField, Range(6, 32)] private int _radialSegments = 14;

        [Tooltip("Шаг колец вдоль тела (доля клетки). Меньше — глаже изгибы")]
        [SerializeField, Range(0.05f, 0.5f)] private float _ringSpacing = 0.2f;

        [Tooltip("Профиль толщины: 0 — у головы, 1 — кончик хвоста. Значение умножается на радиус тела")]
        [SerializeField] private AnimationCurve _radiusProfile = new AnimationCurve(
            new Keyframe(0f, 0.75f), new Keyframe(0.12f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0.05f));

        [Tooltip("Приплюснутость сечения по вертикали (1 — круглое)")]
        [SerializeField, Range(0.5f, 1f)] private float _verticalScale = 0.82f;

        [Tooltip("Длина одного повтора узора шкуры, клеток")]
        [SerializeField, Min(0.1f)] private float _patternLength = 0.9f;

        [Tooltip("Частота мигания при неуязвимости, Гц")]
        [SerializeField, Min(1f)] private float _blinkFrequency = 12f;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uvs;
        private int[] _triangles;
        private float[] _cos;
        private float[] _sin;
        private int _maxRings;
        private int _ringVertexCount;
        private int _activeRings = -1;
        private Vector3 _lastForward = Vector3.forward;

        /// <summary>Скрыть или показать тело (эффект смерти).</summary>
        public bool Visible { get; set; } = true;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        private void Start()
        {
            _ringVertexCount = _radialSegments + 1; // дубль вершины на шве для UV
            _maxRings = Mathf.CeilToInt(_snake.MaxBodyLengthCells / _ringSpacing) + 2;
            int vertexCount = _maxRings * _ringVertexCount;

            _vertices = new Vector3[vertexCount];
            _normals = new Vector3[vertexCount];
            _uvs = new Vector2[vertexCount];
            _triangles = new int[(_maxRings - 1) * _radialSegments * 6];
            _cos = new float[_ringVertexCount];
            _sin = new float[_ringVertexCount];

            for (int s = 0; s < _ringVertexCount; s++)
            {
                float a = (float)s / _radialSegments * Mathf.PI * 2f;
                _cos[s] = Mathf.Cos(a);
                _sin[s] = Mathf.Sin(a);
            }

            int t = 0;
            for (int r = 0; r < _maxRings - 1; r++)
            {
                int a0 = r * _ringVertexCount;
                int b0 = a0 + _ringVertexCount;
                for (int s = 0; s < _radialSegments; s++)
                {
                    // Кольцо r ближе к голове; обход выбран так, чтобы нормали смотрели наружу.
                    _triangles[t++] = a0 + s;
                    _triangles[t++] = b0 + s;
                    _triangles[t++] = a0 + s + 1;
                    _triangles[t++] = a0 + s + 1;
                    _triangles[t++] = b0 + s;
                    _triangles[t++] = b0 + s + 1;
                }
            }

            _mesh = new Mesh { name = "SnakeBody" };
            if (vertexCount > 65000) _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.uv = _uvs;
            // Границы с запасом на всё поле: так не нужно пересчитывать их каждый кадр.
            _mesh.bounds = new Bounds(transform.InverseTransformPoint(Vector3.zero), Vector3.one * 2000f);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        /// <summary>
        /// Скопировать текущую форму тела в другой меш (сброшенная при линьке кожа).
        /// Буферы те же, что у тела, поэтому копия не создаёт мусора, кроме внутренних буферов меша.
        /// </summary>
        public void CopyMeshTo(Mesh target)
        {
            if (_mesh == null || _activeRings < 2) return;
            int vertexCount = _activeRings * _ringVertexCount;
            target.Clear();
            target.SetVertices(_vertices, 0, vertexCount);
            target.SetNormals(_normals, 0, vertexCount);
            target.SetUVs(0, _uvs, 0, vertexCount);
            target.SetTriangles(_triangles, 0, (_activeRings - 1) * _radialSegments * 6, 0, false);
            target.bounds = _mesh.bounds;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void LateUpdate()
        {
            if (_mesh == null) return;

            bool blinkHidden = _snake.IsInvulnerable && Mathf.Repeat(Time.time * _blinkFrequency, 1f) < 0.5f;
            _renderer.enabled = Visible && !blinkHidden;
            if (!Visible) return;

            Rebuild();
        }

        private void Rebuild()
        {
            float cs = _snake.CellSize;
            float length = Mathf.Max(0.01f, _snake.BodyLengthWorld);
            float spacing = _ringSpacing * cs;
            int rings = Mathf.Clamp(Mathf.CeilToInt(length / spacing) + 1, 2, _maxRings);
            float baseRadius = _snake.BodyRadiusWorld;
            float patternWorld = _patternLength * cs;

            // Вершины в мировых координатах, а объект может стоять где угодно — переводим один раз матрицей.
            Matrix4x4 toLocal = transform.worldToLocalMatrix;

            for (int r = 0; r < rings; r++)
            {
                float d = Mathf.Min(r * spacing, length);
                float t = d / length;
                _snake.SampleSpine(d, out Vector3 center, out Vector3 forward);
                if (forward.sqrMagnitude < 1e-8f) forward = _lastForward;
                forward.Normalize();
                _lastForward = forward;

                Vector3 right = Vector3.Cross(Vector3.up, forward);
                if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
                right.Normalize();
                Vector3 up = Vector3.Cross(forward, right);

                float radius = baseRadius * _radiusProfile.Evaluate(t);
                float radiusV = radius * _verticalScale;
                // Брюхо остаётся у земли, поэтому приплюснутое сечение сдвигаем вниз.
                center -= up * (radius - radiusV);
                float v = d / patternWorld;

                int baseIndex = r * _ringVertexCount;
                for (int s = 0; s < _ringVertexCount; s++)
                {
                    float c = _cos[s];
                    float sn = _sin[s];
                    Vector3 offset = right * (c * radius) + up * (sn * radiusV);
                    Vector3 normal = right * (c * _verticalScale) + up * sn;
                    int i = baseIndex + s;
                    _vertices[i] = toLocal.MultiplyPoint3x4(center + offset);
                    _normals[i] = toLocal.MultiplyVector(normal).normalized;
                    _uvs[i] = new Vector2((float)s / _radialSegments, v);
                }
            }

            // Неиспользуемые кольца стягиваем в кончик хвоста: треугольники на них всё равно не ссылаются.
            if (rings < _activeRings)
            {
                Vector3 tip = _vertices[(rings - 1) * _ringVertexCount];
                for (int i = rings * _ringVertexCount; i < _activeRings * _ringVertexCount; i++) _vertices[i] = tip;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            if (rings != _activeRings)
            {
                _mesh.SetTriangles(_triangles, 0, (rings - 1) * _radialSegments * 6, 0, false);
                _activeRings = rings;
            }
        }
    }
}
