using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Камера от третьего лица: позади и выше головы, плавное следование,
    /// лёгкий крен в поворотах и тряска (смерть, щит, рывок).
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Змейка игрока")]
        [SerializeField] private SnakeController _snake;

        [Header("Положение")]
        [Tooltip("Расстояние позади головы")]
        [SerializeField, Min(0.5f)] private float _distance = 5.5f;

        [Tooltip("Высота над головой")]
        [SerializeField, Min(0f)] private float _height = 3.4f;

        [Tooltip("Насколько далеко перед головой точка взгляда")]
        [SerializeField, Min(0f)] private float _lookAhead = 3f;

        [Tooltip("Доля прыжка, которую повторяет камера (0 — камера не подпрыгивает)")]
        [SerializeField, Range(0f, 1f)] private float _followJump = 0.35f;

        [Header("Сглаживание")]
        [Tooltip("Время сглаживания позиции, сек")]
        [SerializeField, Min(0.01f)] private float _positionSmoothTime = 0.18f;

        [Tooltip("Резкость сглаживания поворота")]
        [SerializeField, Min(0.1f)] private float _rotationSharpness = 8f;

        [Header("Эффекты")]
        [Tooltip("Крен камеры при руле до упора, град")]
        [SerializeField, Range(0f, 20f)] private float _steerTilt = 5f;

        [Tooltip("Частота тряски, Гц")]
        [SerializeField, Min(1f)] private float _shakeFrequency = 25f;

        [Header("Витрина (главное меню)")]
        [Tooltip("Расстояние облёта вокруг змеи")]
        [SerializeField, Min(1f)] private float _showcaseDistance = 9f;

        [Tooltip("Высота камеры при облёте")]
        [SerializeField, Min(0f)] private float _showcaseHeight = 4.2f;

        [Tooltip("Скорость облёта, град/с")]
        [SerializeField] private float _showcaseOrbitSpeed = 7f;

        [Tooltip("Сдвиг змеи вправо на экране (мир. единицы): слева её закрывала бы панель меню")]
        [SerializeField] private float _showcaseScreenOffset = 3.2f;

        [Tooltip("Сглаживание позиции в витрине и при перелёте в игру, сек")]
        [SerializeField, Min(0.01f)] private float _showcaseSmoothTime = 0.9f;

        [Tooltip("Длительность плавного перелёта из витрины в игровую позицию, сек")]
        [SerializeField, Min(0f)] private float _transitionTime = 1.2f;

        /// <summary>Режим витрины: медленный облёт змеи на фоне главного меню.</summary>
        public bool Showcase
        {
            get => _showcase;
            set
            {
                // Из витрины в игру не прыгаем, а перелетаем: на это время сглаживание мягче.
                if (_showcase && !value) _transitionTimer = _transitionTime;
                _showcase = value;
            }
        }

        private Vector3 _velocity;
        private float _groundY;
        private Vector3 _smoothPosition;
        private Quaternion _smoothRotation = Quaternion.identity;
        private float _tilt;
        private float _shakeAmplitude;
        private float _shakeDuration;
        private float _shakeTime;
        private bool _showcase;
        private float _orbitAngle;
        private float _transitionTimer;

        /// <summary>Мгновенно поставить камеру на место (старт и рестарт).</summary>
        public void SnapToTarget()
        {
            _groundY = _snake.HeadPosition.y - _snake.HeightAboveGround;
            GetTargetPose(out _smoothPosition, out _smoothRotation);
            _velocity = Vector3.zero;
            _shakeTime = _shakeDuration;
            transform.SetPositionAndRotation(_smoothPosition, _smoothRotation);
        }

        /// <summary>Тряска с затуханием; amplitude в мировых единицах.</summary>
        public void Shake(float amplitude, float duration)
        {
            if (amplitude < _shakeAmplitude * (1f - _shakeTime / Mathf.Max(0.001f, _shakeDuration))) return;
            _shakeAmplitude = amplitude;
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeTime = 0f;
        }

        private void LateUpdate()
        {
            // Unscaled время, чтобы камера оставалась плавной и на паузе, и при замедлении.
            float dt = Time.unscaledDeltaTime;
            if (_showcase) _orbitAngle += _showcaseOrbitSpeed * dt;
            if (_transitionTimer > 0f) _transitionTimer -= dt;

            GetTargetPose(out Vector3 pos, out Quaternion rot);
            float k = _transitionTime > 0f ? Mathf.Clamp01(_transitionTimer / _transitionTime) : 0f;
            float smoothTime = _showcase ? _showcaseSmoothTime : Mathf.Lerp(_positionSmoothTime, _showcaseSmoothTime, k);
            float sharpness = _showcase ? 2f : Mathf.Lerp(_rotationSharpness, 2.5f, k);
            _smoothPosition = Vector3.SmoothDamp(_smoothPosition, pos, ref _velocity, smoothTime, Mathf.Infinity, dt);
            _smoothRotation = Quaternion.Slerp(_smoothRotation, rot, 1f - Mathf.Exp(-sharpness * dt));

            float targetTilt = _snake.IsAlive && !_showcase ? -_snake.Steer * _steerTilt : 0f;
            _tilt = Mathf.Lerp(_tilt, targetTilt, 1f - Mathf.Exp(-6f * dt));

            Vector3 shake = Vector3.zero;
            if (_shakeTime < _shakeDuration)
            {
                _shakeTime += dt;
                float fade = 1f - Mathf.Clamp01(_shakeTime / _shakeDuration);
                float t = Time.unscaledTime * _shakeFrequency;
                shake = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * _shakeAmplitude * fade);
            }

            Quaternion finalRotation = _smoothRotation * Quaternion.Euler(0f, 0f, _tilt);
            transform.SetPositionAndRotation(_smoothPosition + finalRotation * shake, finalRotation);
        }

        private void GetTargetPose(out Vector3 position, out Quaternion rotation)
        {
            if (_showcase)
            {
                GetShowcasePose(out position, out rotation);
                return;
            }

            Vector3 head = _snake.HeadPosition;
            // Гасим часть прыжка, чтобы картинка не скакала вместе с головой.
            head.y = Mathf.Lerp(_groundY, head.y, _followJump);

            Vector3 forward = _snake.Forward;
            position = head - forward * _distance + Vector3.up * _height;
            rotation = Quaternion.LookRotation(head + forward * _lookAhead - position);
        }

        /// <summary>Облёт по кругу вокруг головы; точка взгляда смещена влево, чтобы змея оказалась правее центра.</summary>
        private void GetShowcasePose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 head = _snake.HeadPosition;
            head.y = _groundY + 0.4f;
            Vector3 dir = Quaternion.Euler(0f, _orbitAngle, 0f) * Vector3.back;
            position = head + dir * _showcaseDistance + Vector3.up * _showcaseHeight;
            Vector3 toHead = head - position;
            Vector3 right = Vector3.Cross(Vector3.up, toHead).normalized;
            rotation = Quaternion.LookRotation(head - right * _showcaseScreenOffset - position);
        }
    }
}
