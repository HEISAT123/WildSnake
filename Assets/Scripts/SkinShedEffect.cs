using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Сброшенная при линьке кожа: полупрозрачная копия тела остаётся лежать на земле и медленно тает.
    /// Меш создаётся один раз и при каждой линьке перезаписывается.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SkinShedEffect : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        // В URP Lit цвет — _BaseColor; задаём оба, лишнее свойство шейдер просто не читает.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Ссылки")]
        [Tooltip("Тело змеи, с которого снимается кожа")]
        [SerializeField] private SnakeBodyMesh _body;

        [Header("Вид")]
        [Tooltip("Сколько секунд кожа лежит на земле")]
        [SerializeField, Min(0.5f)] private float _duration = 10f;

        [Tooltip("Цвет кожи (альфа — начальная прозрачность)")]
        [SerializeField] private Color _color = new Color(0.92f, 0.88f, 0.74f, 0.7f);

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _timer;

        private void Awake()
        {
            _mesh = new Mesh { name = "ShedSkin" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.enabled = false;
            _block = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        public void Shed()
        {
            Transform source = _body.transform;
            transform.SetPositionAndRotation(source.position, source.rotation);
            transform.localScale = source.lossyScale;
            _body.CopyMeshTo(_mesh);
            _timer = _duration;
            _renderer.enabled = true;
            ApplyAlpha();
        }

        public void Clear()
        {
            _timer = 0f;
            if (_renderer != null) _renderer.enabled = false;
        }

        private void Update()
        {
            if (_timer <= 0f) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                Clear();
                return;
            }
            ApplyAlpha();
        }

        /// <summary>Первую половину срока кожа видна полностью, потом плавно исчезает.</summary>
        private void ApplyAlpha()
        {
            float k = Mathf.Clamp01(_timer / (_duration * 0.5f));
            var c = new Color(_color.r, _color.g, _color.b, _color.a * k);
            _block.SetColor(ColorId, c);
            _block.SetColor(BaseColorId, c);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
