using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Ничто не должно прятать змею от игрока: крона, ствол, пень, камень или бревно, оказавшиеся между камерой
    /// и головой змеи или вплотную к самой камере, подменяются полупрозрачной копией своего материала,
    /// а потом возвращаются обратно. Проверка — расстояние от центра объекта до отрезка «камера — голова»,
    /// без физики и аллокаций.
    /// </summary>
    public class CanopyFader : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Поле: из него берутся кроны и препятствия")]
        [SerializeField] private BoardView _board;

        [Tooltip("Камера игрока")]
        [SerializeField] private Transform _camera;

        [Tooltip("Змея игрока")]
        [SerializeField] private SnakeController _snake;

        [Header("Материалы")]
        [Tooltip("Обычные материалы (кроны, кора, камень…) — по индексу им соответствуют прозрачные копии")]
        [SerializeField] private Material[] _opaque = new Material[0];

        [Tooltip("Полупрозрачные копии тех же материалов")]
        [SerializeField] private Material[] _faded = new Material[0];

        [Header("Поведение")]
        [Tooltip("Запас к радиусу объекта при проверке, мировых единиц")]
        [SerializeField, Min(0f)] private float _margin = 0.4f;

        [Tooltip("Объекты ближе этого к камере прячутся всегда, мировых единиц")]
        [SerializeField, Min(0f)] private float _nearCamera = 2.2f;

        [Tooltip("Как часто пересчитывать, сек")]
        [SerializeField, Min(0f)] private float _interval = 0.05f;

        private Renderer[] _items;
        private Vector3[] _centers;
        private float[] _radii;
        private bool[] _isFaded;
        private int[] _pair;
        private float _timer;

        private void Start()
        {
            Renderer[] crowns = _board.Crowns;
            Renderer[] occluders = _board.Occluders;
            int n = crowns.Length + occluders.Length;
            _items = new Renderer[n];
            _centers = new Vector3[n];
            _radii = new float[n];
            _isFaded = new bool[n];
            _pair = new int[n];
            for (int i = 0; i < n; i++)
            {
                Renderer r = i < crowns.Length ? crowns[i] : occluders[i - crowns.Length];
                _items[i] = r;
                Bounds b = r.bounds;
                _centers[i] = b.center;
                _radii[i] = Mathf.Max(b.extents.x, b.extents.z);
                _pair[i] = System.Array.IndexOf(_opaque, r.sharedMaterial);
            }
        }

        private void LateUpdate()
        {
            if (_items == null) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = _interval;

            Vector3 from = _camera.position;
            Vector3 to = _snake.HeadPosition + Vector3.up * 0.3f;
            Vector3 seg = to - from;
            float segLenSqr = Mathf.Max(1e-4f, seg.sqrMagnitude);
            float nearSqr = _nearCamera * _nearCamera;
            for (int i = 0; i < _items.Length; i++)
            {
                int pair = _pair[i];
                if (pair < 0) continue;
                Vector3 c = _centers[i];
                // Быстрый отсев далёких объектов: дальше отрезка плюс радиус — точно не мешают.
                float far = segLenSqr + (_radii[i] + _nearCamera) * (_radii[i] + _nearCamera) * 4f;
                bool fade = false;
                if ((c - from).sqrMagnitude < far)
                {
                    float t = Mathf.Clamp01(Vector3.Dot(c - from, seg) / segLenSqr);
                    float dist = (from + seg * t - c).magnitude;
                    fade = dist < _radii[i] + _margin || (_items[i].bounds.ClosestPoint(from) - from).sqrMagnitude < nearSqr;
                }
                if (fade == _isFaded[i]) continue;
                _isFaded[i] = fade;
                _items[i].sharedMaterial = fade ? _faded[pair] : _opaque[pair];
            }
        }
    }
}
