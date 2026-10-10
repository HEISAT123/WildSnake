using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Течение реки: сдвигает карту нормалей и цвет воды вдоль UV (у реки U идёт по руслу от истока),
    /// поэтому рябь бежит вниз по течению. Работает со своей копией материала, чтобы не менять ассет.
    /// </summary>
    public class WaterFlow : MonoBehaviour
    {
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [Header("Ссылки")]
        [Tooltip("Рендерер воды реки")]
        [SerializeField] private Renderer _target;

        [Header("Течение")]
        [Tooltip("Скорость ряби, UV в секунду (минус по U — вниз по течению)")]
        [SerializeField] private Vector2 _speed = new Vector2(-0.22f, 0f);

        /// <summary>Рендерер задаёт BoardView при сборке поля.</summary>
        public Renderer Target
        {
            get => _target;
            set => _target = value;
        }

        private Material _material;
        private Vector2 _offset;

        private void Awake()
        {
            // Копия материала — один раз: сдвиг текстуры не должен попадать в общий ассет.
            if (_target != null) _material = _target.material;
        }

        private void Update()
        {
            if (_material == null) return;
            _offset += _speed * Time.deltaTime;
            _offset.x = Mathf.Repeat(_offset.x, 100f);
            _offset.y = Mathf.Repeat(_offset.y, 100f);
            if (_material.HasProperty(BumpMapId)) _material.SetTextureOffset(BumpMapId, _offset);
            if (_material.HasProperty(MainTexId)) _material.SetTextureOffset(MainTexId, _offset * 0.5f);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
