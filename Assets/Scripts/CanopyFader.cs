using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Кроны рощ не должны прятать змею от игрока: крона, которая оказалась между камерой и головой змеи
    /// (или рядом с самой камерой), подменяется полупрозрачной копией материала, а потом возвращается обратно.
    /// Проверка — расстояние от центра кроны до отрезка «камера — голова», без физики и аллокаций.
    /// </summary>
    public class CanopyFader : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Поле: из него берутся кроны")]
        [SerializeField] private BoardView _board;

        [Tooltip("Камера игрока")]
        [SerializeField] private Transform _camera;

        [Tooltip("Змея игрока")]
        [SerializeField] private SnakeController _snake;

        [Header("Материалы")]
        [SerializeField] private Material _leaves;
        [SerializeField] private Material _leavesFade;
        [SerializeField] private Material _pine;
        [SerializeField] private Material _pineFade;

        [Header("Поведение")]
        [Tooltip("Запас к радиусу кроны при проверке, мировых единиц")]
        [SerializeField, Min(0f)] private float _margin = 0.6f;

        [Tooltip("Как часто пересчитывать, сек")]
        [SerializeField, Min(0f)] private float _interval = 0.05f;

        private Renderer[] _crowns;
        private Vector3[] _centers;
        private float[] _radii;
        private bool[] _faded;
        private float _timer;

        private void Start()
        {
            _crowns = _board.Crowns;
            int n = _crowns.Length;
            _centers = new Vector3[n];
            _radii = new float[n];
            _faded = new bool[n];
            for (int i = 0; i < n; i++)
            {
                Bounds b = _crowns[i].bounds;
                _centers[i] = b.center;
                _radii[i] = Mathf.Max(b.extents.x, b.extents.z);
            }
        }

        private void LateUpdate()
        {
            if (_crowns == null) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = _interval;

            Vector3 from = _camera.position;
            Vector3 to = _snake.HeadPosition + Vector3.up * 0.3f;
            Vector3 seg = to - from;
            float segLenSqr = Mathf.Max(1e-4f, seg.sqrMagnitude);
            for (int i = 0; i < _crowns.Length; i++)
            {
                Vector3 c = _centers[i];
                float t = Mathf.Clamp01(Vector3.Dot(c - from, seg) / segLenSqr);
                float dist = (from + seg * t - c).magnitude;
                bool fade = dist < _radii[i] + _margin;
                if (fade == _faded[i]) continue;
                _faded[i] = fade;
                Renderer r = _crowns[i];
                Material current = r.sharedMaterial;
                if (fade) r.sharedMaterial = current == _pine ? _pineFade : _leavesFade;
                else r.sharedMaterial = current == _pineFade ? _pine : _leaves;
            }
        }
    }
}
