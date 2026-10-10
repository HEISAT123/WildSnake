using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Вылупившиеся змеёныши: несколько крошечных змеек расползаются от гнезда в разные стороны, виляя,
    /// и через несколько секунд скрываются в траве. Только зрелище — на игру не влияют.
    /// Модели заранее созданы (SnakeSceneBuilder): корень и цепочка шариков-сегментов, без Instantiate.
    /// </summary>
    public class Hatchlings : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Сетка: высота земли под змеёнышами")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Корни змеёнышей; дочерние объекты — сегменты от головы к хвосту")]
        [SerializeField] private Transform[] _roots = new Transform[0];

        [Header("Поведение")]
        [Tooltip("Сколько секунд змеёныши видны")]
        [SerializeField, Min(1f)] private float _lifetime = 7f;

        [Tooltip("Скорость, клеток в секунду")]
        [SerializeField, Min(0.1f)] private float _speed = 1.4f;

        [Tooltip("Шаг между сегментами, клеток")]
        [SerializeField, Min(0.02f)] private float _spacing = 0.075f;

        private Transform[] _segments;
        private Vector3[] _segmentPos;
        private Vector3[] _baseScale;
        private int _perHatchling;
        private Vector3[] _dir;
        private float[] _age;
        private float[] _phase;
        private bool[] _active;

        private void Awake()
        {
            int n = _roots.Length;
            _perHatchling = n > 0 ? _roots[0].childCount : 0;
            _segments = new Transform[n * _perHatchling];
            _segmentPos = new Vector3[_segments.Length];
            _baseScale = new Vector3[_segments.Length];
            _dir = new Vector3[n];
            _age = new float[n];
            _phase = new float[n];
            _active = new bool[n];
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < _perHatchling; k++)
                {
                    Transform seg = _roots[i].GetChild(k);
                    _segments[i * _perHatchling + k] = seg;
                    _baseScale[i * _perHatchling + k] = seg.localScale;
                }
                _roots[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Выпустить count змеёнышей из точки.</summary>
        public void Release(Vector3 at, int count)
        {
            float cs = _grid.CellSize;
            int n = Mathf.Min(count, _roots.Length);
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float yaw = start + i * 360f / n + Random.Range(-20f, 20f);
                _dir[i] = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                _age[i] = -i * 0.25f;
                _phase[i] = Random.value * 6f;
                _active[i] = true;
                for (int k = 0; k < _perHatchling; k++)
                    _segmentPos[i * _perHatchling + k] = at - _dir[i] * (k * _spacing * cs);
                _roots[i].gameObject.SetActive(true);
            }
        }

        /// <summary>Убрать всех сразу (новая партия).</summary>
        public void Clear()
        {
            for (int i = 0; i < _roots.Length; i++)
            {
                _active[i] = false;
                _roots[i].gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float cs = _grid.CellSize;
            for (int i = 0; i < _roots.Length; i++)
            {
                if (!_active[i]) continue;
                _age[i] += dt;
                if (_age[i] < 0f) continue;
                if (_age[i] >= _lifetime)
                {
                    _active[i] = false;
                    _roots[i].gameObject.SetActive(false);
                    continue;
                }

                // Голова виляет и ползёт вперёд, сегменты тянутся за ней на постоянном расстоянии.
                Vector3 wiggle = Quaternion.Euler(0f, Mathf.Sin(_age[i] * 7f + _phase[i]) * 35f, 0f) * _dir[i];
                int b = i * _perHatchling;
                _segmentPos[b] += wiggle * (_speed * cs * dt);
                for (int k = 1; k < _perHatchling; k++)
                {
                    Vector3 d = _segmentPos[b + k] - _segmentPos[b + k - 1];
                    d.y = 0f;
                    float len = d.magnitude;
                    if (len > 1e-4f) _segmentPos[b + k] = _segmentPos[b + k - 1] + d / len * (_spacing * cs);
                }
                // В конце змеёныш «уходит в траву» — уменьшается.
                float fade = Mathf.Clamp01((_lifetime - _age[i]) / 1f);
                for (int k = 0; k < _perHatchling; k++)
                {
                    Vector3 p = _segmentPos[b + k];
                    p.y = _grid.GroundHeight(p) + 0.05f * cs;
                    _segments[b + k].position = p;
                    _segments[b + k].localScale = _baseScale[b + k] * fade;
                }
            }
        }
    }
}
