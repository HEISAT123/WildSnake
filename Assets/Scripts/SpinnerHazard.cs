using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Вращающаяся балка на столбике. Балка низкая — её можно перепрыгнуть,
    /// столбик высокий — его только объезжать.
    /// </summary>
    public class SpinnerHazard : MonoBehaviour
    {
        [Header("Части")]
        [Tooltip("Вращающаяся балка (длина по локальной оси Z)")]
        [SerializeField] private Transform _beam;

        [Header("Размеры (мировые единицы при клетке 1)")]
        [Tooltip("Высота верха балки над полем")]
        [SerializeField, Min(0.05f)] private float _beamTop = 0.45f;

        [Tooltip("Половина толщины балки")]
        [SerializeField, Min(0.01f)] private float _beamHalfWidth = 0.15f;

        [Tooltip("Радиус центрального столбика")]
        [SerializeField, Min(0.05f)] private float _postRadius = 0.3f;

        private float _halfLength;
        private float _speed;
        private float _angle;
        private float _cellSize = 1f;

        public Vector3 CenterPosition => transform.position;

        /// <summary>Радиус зоны, которую задевает балка.</summary>
        public float Radius => _halfLength;

        public void Configure(Vector3 center, float length, float speed, float cellSize)
        {
            transform.position = center;
            _halfLength = length * 0.5f;
            _speed = speed;
            _cellSize = cellSize;
            _angle = Random.Range(0f, 360f);
            Vector3 s = _beam.localScale;
            _beam.localScale = new Vector3(s.x, s.y, length);
            _beam.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }

        public void SetSpeed(float speed)
        {
            _speed = speed;
        }

        private void Update()
        {
            _angle += _speed * Time.deltaTime;
            _beam.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }

        /// <summary>Задевает ли голова (круг радиуса radius на высоте heightAboveGround) балку или столбик.</summary>
        public bool Hits(Vector3 headPosition, float radius, float heightAboveGround)
        {
            Vector3 c = transform.position;
            float dx = headPosition.x - c.x;
            float dz = headPosition.z - c.z;

            float post = radius + _postRadius * _cellSize;
            if (dx * dx + dz * dz < post * post) return true;

            if (heightAboveGround >= _beamTop * _cellSize) return false;

            // Расстояние от точки до отрезка балки в плоскости XZ.
            Vector3 dir = _beam.rotation * Vector3.forward;
            float along = Mathf.Clamp(dx * dir.x + dz * dir.z, -_halfLength, _halfLength);
            float px = dx - dir.x * along;
            float pz = dz - dir.z * along;
            float r = radius + _beamHalfWidth * _cellSize;
            return px * px + pz * pz < r * r;
        }
    }
}
