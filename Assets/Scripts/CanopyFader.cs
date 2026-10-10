using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Ничто не должно прятать змею от игрока: крона, ствол, пень, камень или бревно, которые действительно
    /// пересекают луч «камера — голова» (или в которые зашла сама камера), подменяются полупрозрачной копией
    /// своего материала, а потом возвращаются обратно. Проверка точная — отрезок против повёрнутого бокса объекта
    /// в его локальных координатах, поэтому длинное бревно рядом со змеёй, но не на линии взгляда, остаётся
    /// непрозрачным. Без физики и аллокаций.
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
        [Tooltip("Запас вокруг объекта при проверке луча, мировых единиц")]
        [SerializeField, Min(0f)] private float _margin = 0.1f;

        [Tooltip("Объект ближе этого к камере прячется всегда (камера почти внутри), мировых единиц")]
        [SerializeField, Min(0f)] private float _nearCamera = 0.35f;

        [Tooltip("Луч обрывается не доходя до головы на столько — то, что вплотную к змее, но не перед ней, не прячем")]
        [SerializeField, Min(0f)] private float _headGap = 0.45f;

        [Tooltip("Сколько объект должен быть свободен от луча, чтобы снова стать непрозрачным, сек (без мигания)")]
        [SerializeField, Min(0f)] private float _restoreDelay = 0.25f;

        [Tooltip("Как часто пересчитывать, сек")]
        [SerializeField, Min(0f)] private float _interval = 0.05f;

        private Renderer[] _items;
        private Matrix4x4[] _toLocal;
        private Vector3[] _boxMin;
        private Vector3[] _boxMax;
        private Vector3[] _nearPad;
        private Vector3[] _centers;
        private float[] _radii;
        private float[] _clearTime;
        private bool[] _isFaded;
        private int[] _pair;
        private float _timer;

        private void Start()
        {
            Renderer[] crowns = _board.Crowns;
            Renderer[] occluders = _board.Occluders;
            int n = crowns.Length + occluders.Length;
            _items = new Renderer[n];
            _toLocal = new Matrix4x4[n];
            _boxMin = new Vector3[n];
            _boxMax = new Vector3[n];
            _nearPad = new Vector3[n];
            _centers = new Vector3[n];
            _radii = new float[n];
            _clearTime = new float[n];
            _isFaded = new bool[n];
            _pair = new int[n];
            for (int i = 0; i < n; i++)
            {
                Renderer r = i < crowns.Length ? crowns[i] : occluders[i - crowns.Length];
                _items[i] = r;
                // Объекты поля неподвижны — матрицу и бокс считаем один раз.
                Transform t = r.transform;
                _toLocal[i] = t.worldToLocalMatrix;
                Vector3 scale = t.lossyScale;
                var inv = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(scale.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(scale.y)),
                    1f / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
                Bounds local = r.localBounds;
                Vector3 pad = inv * _margin;
                _boxMin[i] = local.min - pad;
                _boxMax[i] = local.max + pad;
                _nearPad[i] = inv * _nearCamera;
                Bounds world = r.bounds;
                _centers[i] = world.center;
                _radii[i] = world.extents.magnitude;
                _pair[i] = System.Array.IndexOf(_opaque, r.sharedMaterial);
            }
        }

        private void LateUpdate()
        {
            if (_items == null) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            float elapsed = _interval - _timer;
            _timer = _interval;

            Vector3 from = _camera.position;
            Vector3 head = _snake.HeadPosition + Vector3.up * 0.3f;
            Vector3 seg = head - from;
            float segLen = seg.magnitude;
            Vector3 to = segLen > _headGap ? head - seg * (_headGap / segLen) : from;
            float reach = Mathf.Max(segLen, 0f);
            for (int i = 0; i < _items.Length; i++)
            {
                int pair = _pair[i];
                if (pair < 0) continue;

                bool blocking = false;
                // Быстрый отсев: объект дальше отрезка плюс свой радиус — точно не мешает.
                float far = reach + _radii[i] + _nearCamera;
                if ((_centers[i] - from).sqrMagnitude < far * far)
                {
                    Matrix4x4 m = _toLocal[i];
                    Vector3 a = m.MultiplyPoint3x4(from);
                    Vector3 b = m.MultiplyPoint3x4(to);
                    blocking = SegmentHitsBox(a, b, _boxMin[i], _boxMax[i])
                               || Inside(a, _boxMin[i] - _nearPad[i], _boxMax[i] + _nearPad[i]);
                }

                bool fade;
                if (blocking)
                {
                    _clearTime[i] = 0f;
                    fade = true;
                }
                else
                {
                    _clearTime[i] += elapsed;
                    fade = _isFaded[i] && _clearTime[i] < _restoreDelay;
                }
                if (fade == _isFaded[i]) continue;
                _isFaded[i] = fade;
                _items[i].sharedMaterial = fade ? _faded[pair] : _opaque[pair];
            }
        }

        private static bool Inside(Vector3 p, Vector3 min, Vector3 max)
        {
            return p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y && p.z >= min.z && p.z <= max.z;
        }

        /// <summary>Отрезок a→b против бокса (метод плит): есть ли общий участок при t в [0, 1].</summary>
        private static bool SegmentHitsBox(Vector3 a, Vector3 b, Vector3 min, Vector3 max)
        {
            float t0 = 0f, t1 = 1f;
            return Slab(a.x, b.x - a.x, min.x, max.x, ref t0, ref t1)
                   && Slab(a.y, b.y - a.y, min.y, max.y, ref t0, ref t1)
                   && Slab(a.z, b.z - a.z, min.z, max.z, ref t0, ref t1);
        }

        private static bool Slab(float start, float dir, float min, float max, ref float t0, ref float t1)
        {
            if (Mathf.Abs(dir) < 1e-6f) return start >= min && start <= max;
            float inv = 1f / dir;
            float near = (min - start) * inv;
            float far = (max - start) * inv;
            if (near > far)
            {
                float tmp = near;
                near = far;
                far = tmp;
            }
            if (near > t0) t0 = near;
            if (far < t1) t1 = far;
            return t0 <= t1;
        }
    }
}
