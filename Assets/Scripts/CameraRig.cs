using UnityEngine;

namespace Snake3D
{
    /// <summary>Камера: вид сверху под углом на центр поля или вид от третьего лица за головой (клавиша C).</summary>
    public class CameraRig : MonoBehaviour
    {
        public enum ViewMode
        {
            TopDown,
            ThirdPerson
        }

        [Header("Ссылки")]
        [Tooltip("Сетка поля (центр и размер для вида сверху)")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Голова змейки для вида от третьего лица")]
        [SerializeField] private Transform _target;

        [Header("Режим")]
        [Tooltip("Режим камеры при старте")]
        [SerializeField] private ViewMode _startMode = ViewMode.TopDown;

        [Header("Вид сверху")]
        [Tooltip("Высота камеры относительно размера поля")]
        [SerializeField, Min(0.1f)] private float _topDownHeightFactor = 0.95f;

        [Tooltip("Отступ камеры назад относительно размера поля (задаёт наклон)")]
        [SerializeField, Min(0f)] private float _topDownBackFactor = 0.55f;

        [Header("Третье лицо")]
        [Tooltip("Расстояние позади головы")]
        [SerializeField, Min(0.5f)] private float _followDistance = 4.5f;

        [Tooltip("Высота над головой")]
        [SerializeField, Min(0f)] private float _followHeight = 3f;

        [Tooltip("Насколько далеко вперёд смотреть")]
        [SerializeField, Min(0f)] private float _lookAhead = 2.5f;

        [Header("Сглаживание")]
        [Tooltip("Время сглаживания позиции, сек")]
        [SerializeField, Min(0.01f)] private float _positionSmoothTime = 0.25f;

        [Tooltip("Резкость сглаживания поворота")]
        [SerializeField, Min(0.1f)] private float _rotationSharpness = 8f;

        private ViewMode _mode;
        private Vector3 _velocity;

        public ViewMode Mode => _mode;

        private void Start()
        {
            _mode = _startMode;
            GetTargetPose(out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        public void ToggleMode()
        {
            _mode = _mode == ViewMode.TopDown ? ViewMode.ThirdPerson : ViewMode.TopDown;
        }

        private void LateUpdate()
        {
            if (GameInput.ToggleCamera) ToggleMode();

            // Unscaled время, чтобы камера переключалась и на паузе (timeScale = 0).
            float dt = Time.unscaledDeltaTime;
            GetTargetPose(out Vector3 pos, out Quaternion rot);
            transform.position = Vector3.SmoothDamp(transform.position, pos, ref _velocity, _positionSmoothTime, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-_rotationSharpness * dt));
        }

        private void GetTargetPose(out Vector3 position, out Quaternion rotation)
        {
            if (_mode == ViewMode.ThirdPerson && _target != null)
            {
                Vector3 forward = _target.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();

                position = _target.position - forward * _followDistance + Vector3.up * _followHeight;
                Vector3 lookPoint = _target.position + forward * _lookAhead;
                rotation = Quaternion.LookRotation(lookPoint - position);
                return;
            }

            Vector3 center = _grid.Center;
            float extent = Mathf.Max(_grid.Width, _grid.Height) * _grid.CellSize;
            position = center + new Vector3(0f, extent * _topDownHeightFactor, -extent * _topDownBackFactor);
            rotation = Quaternion.LookRotation(center - position);
        }
    }
}
