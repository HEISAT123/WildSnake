using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Камера, привязанная к голове змейки. Три вида переключаются клавишей C по кругу:
    /// от третьего лица (сзади), от второго лица (спереди, лицом к змейке), от первого лица (из глаз).
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public enum ViewMode
        {
            ThirdPerson,
            SecondPerson,
            FirstPerson
        }

        private const int VIEW_MODE_COUNT = 3;

        [Header("Ссылки")]
        [Tooltip("Голова змейки, за которой следует камера")]
        [SerializeField] private Transform _target;

        [Header("Режим")]
        [Tooltip("Вид камеры при старте")]
        [SerializeField] private ViewMode _startMode = ViewMode.ThirdPerson;

        [Header("Третье лицо (сзади)")]
        [Tooltip("Расстояние позади головы")]
        [SerializeField, Min(0.5f)] private float _thirdDistance = 4.5f;

        [Tooltip("Высота над головой")]
        [SerializeField, Min(0f)] private float _thirdHeight = 3f;

        [Tooltip("Насколько далеко перед головой точка взгляда")]
        [SerializeField, Min(0f)] private float _thirdLookAhead = 2.5f;

        [Header("Второе лицо (спереди)")]
        [Tooltip("Расстояние перед головой")]
        [SerializeField, Min(0.5f)] private float _secondDistance = 5f;

        [Tooltip("Высота камеры над головой")]
        [SerializeField, Min(0f)] private float _secondHeight = 2.4f;

        [Tooltip("Насколько выше центра головы точка взгляда")]
        [SerializeField] private float _secondLookHeight = 0.3f;

        [Header("Первое лицо (из глаз)")]
        [Tooltip("Высота глаз над центром головы")]
        [SerializeField] private float _firstEyeHeight = 0.25f;

        [Tooltip("Смещение вперёд, чтобы камера была перед мордой, а не внутри куба")]
        [SerializeField, Min(0f)] private float _firstForwardOffset = 0.55f;

        [Tooltip("Дистанция до точки взгляда")]
        [SerializeField, Min(0.5f)] private float _firstLookAhead = 4f;

        [Tooltip("Насколько опустить точку взгляда (наклон вниз)")]
        [SerializeField, Min(0f)] private float _firstLookDown = 0.7f;

        [Header("Сглаживание")]
        [Tooltip("Время сглаживания позиции для видов от 2-го и 3-го лица, сек")]
        [SerializeField, Min(0.01f)] private float _positionSmoothTime = 0.15f;

        [Tooltip("Время сглаживания позиции для вида от 1-го лица, сек")]
        [SerializeField, Min(0.001f)] private float _firstPersonSmoothTime = 0.03f;

        [Tooltip("Резкость сглаживания поворота")]
        [SerializeField, Min(0.1f)] private float _rotationSharpness = 10f;

        [Tooltip("Резкость сглаживания поворота для вида от 1-го лица")]
        [SerializeField, Min(0.1f)] private float _firstPersonRotationSharpness = 20f;

        public event Action<ViewMode> ViewChanged;

        public ViewMode Mode => _mode;

        private ViewMode _mode;
        private Vector3 _velocity;

        private void Awake()
        {
            // Режим ставим в Awake, чтобы другие объекты могли прочитать его в своём Start.
            _mode = _startMode;
        }

        public void NextMode()
        {
            _mode = (ViewMode)(((int)_mode + 1) % VIEW_MODE_COUNT);
            ViewChanged?.Invoke(_mode);
        }

        /// <summary>Мгновенно поставить камеру в позицию текущего вида (старт и рестарт).</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            GetTargetPose(out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos, rot);
            _velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (GameInput.ToggleCamera) NextMode();
            if (_target == null) return;

            // Unscaled время, чтобы камера переключалась и на паузе (timeScale = 0).
            float dt = Time.unscaledDeltaTime;
            bool firstPerson = _mode == ViewMode.FirstPerson;
            float smoothTime = firstPerson ? _firstPersonSmoothTime : _positionSmoothTime;
            float sharpness = firstPerson ? _firstPersonRotationSharpness : _rotationSharpness;

            GetTargetPose(out Vector3 pos, out Quaternion rot);
            transform.position = Vector3.SmoothDamp(transform.position, pos, ref _velocity, smoothTime, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-sharpness * dt));
        }

        private void GetTargetPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 head = _target.position;
            Vector3 forward = _target.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 lookPoint;
            switch (_mode)
            {
                case ViewMode.SecondPerson:
                    position = head + forward * _secondDistance + Vector3.up * _secondHeight;
                    lookPoint = head + Vector3.up * _secondLookHeight;
                    break;
                case ViewMode.FirstPerson:
                    position = head + forward * _firstForwardOffset + Vector3.up * _firstEyeHeight;
                    lookPoint = position + forward * _firstLookAhead + Vector3.down * _firstLookDown;
                    break;
                default:
                    position = head - forward * _thirdDistance + Vector3.up * _thirdHeight;
                    lookPoint = head + forward * _thirdLookAhead;
                    break;
            }

            rotation = Quaternion.LookRotation(lookPoint - position);
        }
    }
}
