using UnityEngine;

namespace Snake3D
{
    /// <summary>Камера от третьего лица: позади и выше головы змейки, с плавным следованием.</summary>
    public class CameraRig : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Голова змейки, за которой следует камера")]
        [SerializeField] private Transform _target;

        [Header("Положение")]
        [Tooltip("Расстояние позади головы")]
        [SerializeField, Min(0.5f)] private float _distance = 5f;

        [Tooltip("Высота над головой")]
        [SerializeField, Min(0f)] private float _height = 3.2f;

        [Tooltip("Насколько далеко перед головой точка взгляда")]
        [SerializeField, Min(0f)] private float _lookAhead = 3f;

        [Tooltip("Доля прыжка, которую повторяет камера (0 — камера не подпрыгивает)")]
        [SerializeField, Range(0f, 1f)] private float _followJump = 0.35f;

        [Header("Сглаживание")]
        [Tooltip("Время сглаживания позиции, сек")]
        [SerializeField, Min(0.01f)] private float _positionSmoothTime = 0.18f;

        [Tooltip("Резкость сглаживания поворота")]
        [SerializeField, Min(0.1f)] private float _rotationSharpness = 8f;

        private Vector3 _velocity;
        private float _groundY;

        /// <summary>Мгновенно поставить камеру на место (старт и рестарт).</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            _groundY = _target.position.y;
            GetTargetPose(out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos, rot);
            _velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // Unscaled время, чтобы камера оставалась плавной и на паузе (timeScale = 0).
            float dt = Time.unscaledDeltaTime;
            GetTargetPose(out Vector3 pos, out Quaternion rot);
            transform.position = Vector3.SmoothDamp(transform.position, pos, ref _velocity, _positionSmoothTime, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-_rotationSharpness * dt));
        }

        private void GetTargetPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 head = _target.position;
            // Гасим часть прыжка, чтобы картинка не скакала вместе с головой.
            head.y = Mathf.Lerp(_groundY, head.y, _followJump);

            Vector3 forward = _target.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            position = head - forward * _distance + Vector3.up * _height;
            rotation = Quaternion.LookRotation(head + forward * _lookAhead - position);
        }
    }
}
