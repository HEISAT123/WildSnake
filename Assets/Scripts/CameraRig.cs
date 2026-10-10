using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Камера от третьего лица, как в обычных играх: мышь вращает её вокруг змеи (орбита с ограничением
    /// по вертикали), колесо приближает, при замахе камера подходит ближе и смещается через плечо.
    /// Камни, пни и стены арены не пропускают камеру: короткий проход по сетке поля придвигает её к змее.
    /// В меню — облёт-витрина. Тряска — смерть, удары, бросок.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Змейка игрока")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Сетка поля: по ней камера обходит препятствия")]
        [SerializeField] private GridManager _grid;

        [Header("Орбита")]
        [Tooltip("Расстояние до змеи по умолчанию")]
        [SerializeField, Min(0.5f)] private float _distance = 6f;

        [Tooltip("Пределы приближения колесом (мин, макс)")]
        [SerializeField] private Vector2 _distanceRange = new Vector2(2.5f, 11f);

        [Tooltip("Наклон камеры по умолчанию, град (0 — горизонтально)")]
        [SerializeField, Range(-10f, 80f)] private float _defaultPitch = 24f;

        [Tooltip("Пределы наклона, град (мин, макс)")]
        [SerializeField] private Vector2 _pitchRange = new Vector2(2f, 72f);

        [Tooltip("Высота точки взгляда над землёй")]
        [SerializeField, Min(0f)] private float _pivotHeight = 1f;

        [Tooltip("Градусов поворота на единицу оси мыши (при чувствительности 1)")]
        [SerializeField, Min(0.1f)] private float _lookSpeed = 2.2f;

        [Tooltip("Шаг приближения колесом, мировых единиц на «щелчок»")]
        [SerializeField, Min(0.1f)] private float _zoomStep = 8f;

        [Tooltip("Доля прыжка, которую повторяет камера (0 — камера не подпрыгивает)")]
        [SerializeField, Range(0f, 1f)] private float _followJump = 0.35f;

        [Tooltip("Время сглаживания следования за змеёй, сек")]
        [SerializeField, Min(0.01f)] private float _followSmoothTime = 0.08f;

        [Header("Замах")]
        [Tooltip("Во сколько раз камера ближе при замахе")]
        [SerializeField, Range(0.3f, 1f)] private float _coilZoom = 0.68f;

        [Tooltip("Смещение через плечо при замахе, мировых единиц вправо")]
        [SerializeField] private float _coilShoulder = 0.7f;

        [Header("Препятствия")]
        [Tooltip("Отступ камеры от препятствия")]
        [SerializeField, Min(0f)] private float _collisionMargin = 0.4f;

        [Tooltip("Шаг проверки луча камеры по сетке, мировых единиц")]
        [SerializeField, Min(0.05f)] private float _collisionStep = 0.25f;

        [Tooltip("Высота стен арены для камеры")]
        [SerializeField, Min(0f)] private float _wallHeight = 1.1f;

        [Tooltip("Насколько камера может сама подняться над препятствием позади, град")]
        [SerializeField, Range(0f, 60f)] private float _maxAutoPitch = 40f;

        [Tooltip("Скорость автоподъёма камеры, град/с")]
        [SerializeField, Min(1f)] private float _autoPitchSpeed = 70f;

        [Header("Эффекты")]
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
                if (_showcase && !value)
                {
                    _transitionTimer = _transitionTime;
                    ResetOrbitBehindSnake();
                }
                _showcase = value;
            }
        }

        /// <summary>Мышь вращает камеру (только во время партии — иначе курсор нужен меню).</summary>
        public bool OrbitInput { get; set; }

        /// <summary>Чувствительность мыши из настроек.</summary>
        public float Sensitivity { get; set; } = 1f;

        public bool InvertY { get; set; }

        /// <summary>Поворот камеры вокруг вертикали, град.</summary>
        public float Yaw => _orbitYaw;

        /// <summary>Направление взгляда камеры по земле (для WASD и прицела).</summary>
        public Vector3 PlanarForward => Quaternion.Euler(0f, _orbitYaw, 0f) * Vector3.forward;

        private Vector3 _velocity;
        private Vector3 _pivot;
        private Vector3 _pivotVelocity;
        private float _groundY;
        private Vector3 _smoothPosition;
        private Quaternion _smoothRotation = Quaternion.identity;
        private float _shakeAmplitude;
        private float _shakeDuration;
        private float _shakeTime;
        private bool _showcase;
        private float _orbitAngle;
        private float _transitionTimer;
        private float _orbitYaw;
        private float _orbitPitch;
        private float _zoomDistance;
        private float _coilBlend;
        private float _collisionDistance = float.MaxValue;
        private float _autoPitch;

        private void Awake()
        {
            _zoomDistance = _distance;
            _orbitPitch = _defaultPitch;
        }

        /// <summary>Мгновенно поставить камеру на место (старт и рестарт).</summary>
        public void SnapToTarget()
        {
            _groundY = _snake.HeadPosition.y - _snake.HeightAboveGround;
            ResetOrbitBehindSnake();
            _pivot = PivotTarget();
            _pivotVelocity = Vector3.zero;
            _collisionDistance = float.MaxValue;
            _autoPitch = 0f;
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

        /// <summary>Развернуть камеру за спину змеи (выход из норы).</summary>
        public void LookBehindSnake()
        {
            ResetOrbitBehindSnake();
            _autoPitch = 0f;
        }

        private void ResetOrbitBehindSnake()
        {
            _orbitYaw = _snake.Yaw;
            _orbitPitch = _defaultPitch;
        }

        private void LateUpdate()
        {
            // Unscaled время, чтобы камера оставалась плавной и на паузе, и при замедлении.
            float dt = Time.unscaledDeltaTime;
            if (_showcase) _orbitAngle += _showcaseOrbitSpeed * dt;
            if (_transitionTimer > 0f) _transitionTimer -= dt;
            if (!_showcase) UpdateOrbitInput();

            _coilBlend = Mathf.MoveTowards(_coilBlend, _snake.IsCoiled && !_showcase ? 1f : 0f, dt * 4f);
            _pivot = Vector3.SmoothDamp(_pivot, PivotTarget(), ref _pivotVelocity, _followSmoothTime, Mathf.Infinity, dt);

            GetTargetPose(out Vector3 pos, out Quaternion rot);
            float k = _transitionTime > 0f ? Mathf.Clamp01(_transitionTimer / _transitionTime) : 0f;
            if (_showcase || k > 0f)
            {
                // Витрина и перелёт — мягкое сглаживание; в игре камера следует за мышью без задержки.
                float smoothTime = _showcase ? _showcaseSmoothTime : Mathf.Lerp(0.05f, _showcaseSmoothTime, k);
                float sharpness = _showcase ? 2f : Mathf.Lerp(20f, 2.5f, k);
                _smoothPosition = Vector3.SmoothDamp(_smoothPosition, pos, ref _velocity, smoothTime, Mathf.Infinity, dt);
                _smoothRotation = Quaternion.Slerp(_smoothRotation, rot, 1f - Mathf.Exp(-sharpness * dt));
            }
            else
            {
                _smoothPosition = pos;
                _smoothRotation = rot;
                _velocity = Vector3.zero;
            }

            Vector3 shake = Vector3.zero;
            if (_shakeTime < _shakeDuration)
            {
                _shakeTime += dt;
                float fade = 1f - Mathf.Clamp01(_shakeTime / _shakeDuration);
                float t = Time.unscaledTime * _shakeFrequency;
                shake = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * _shakeAmplitude * fade);
            }

            transform.SetPositionAndRotation(_smoothPosition + _smoothRotation * shake, _smoothRotation);
        }

        private void UpdateOrbitInput()
        {
            if (!OrbitInput) return;
            Vector2 look = GameInput.Look * (_lookSpeed * Sensitivity);
            _orbitYaw = Mathf.Repeat(_orbitYaw + look.x, 360f);
            _orbitPitch = Mathf.Clamp(_orbitPitch + (InvertY ? look.y : -look.y), _pitchRange.x, _pitchRange.y);
            float zoom = GameInput.Zoom;
            if (Mathf.Abs(zoom) > 1e-4f)
                _zoomDistance = Mathf.Clamp(_zoomDistance - zoom * _zoomStep, _distanceRange.x, _distanceRange.y);
        }

        private Vector3 PivotTarget()
        {
            Vector3 head = _snake.HeadPosition;
            float ground = head.y - _snake.HeightAboveGround;
            if (_snake.IsGrounded) _groundY = ground;
            // Гасим часть прыжка, чтобы картинка не скакала вместе с головой.
            head.y = Mathf.Lerp(_groundY, head.y, _followJump) + _pivotHeight;
            return head;
        }

        private void GetTargetPose(out Vector3 position, out Quaternion rotation)
        {
            if (_showcase)
            {
                GetShowcasePose(out position, out rotation);
                return;
            }

            Vector3 right = Quaternion.Euler(0f, _orbitYaw, 0f) * Vector3.right;
            Vector3 pivot = _pivot + right * (_coilShoulder * _coilBlend);
            float wanted = _zoomDistance * Mathf.Lerp(1f, _coilZoom, _coilBlend);
            UpdateAutoPitch(pivot, wanted);

            rotation = Quaternion.Euler(Mathf.Min(_pitchRange.y, _orbitPitch + _autoPitch), _orbitYaw, 0f);
            Vector3 back = rotation * Vector3.back;

            // К препятствию камера придвигается сразу, а отходит плавно — без рывков на краю камня.
            float free = FreeDistance(pivot, back, wanted);
            _collisionDistance = free < _collisionDistance ? free : Mathf.MoveTowards(_collisionDistance, free, Time.unscaledDeltaTime * 6f);
            position = pivot + back * Mathf.Min(wanted, _collisionDistance);
        }

        /// <summary>
        /// Камни и пни позади змеи не должны прижимать камеру к её затылку: если луч упирается в препятствие,
        /// ищем наклон круче, при котором камера проходит над ним, и плавно поднимаемся туда.
        /// </summary>
        private void UpdateAutoPitch(Vector3 pivot, float wanted)
        {
            float target = 0f;
            float best = FreeDistance(pivot, Quaternion.Euler(_orbitPitch, _orbitYaw, 0f) * Vector3.back, wanted);
            for (float extra = 10f; best < wanted * 0.8f && extra <= _maxAutoPitch; extra += 10f)
            {
                float pitch = Mathf.Min(_pitchRange.y, _orbitPitch + extra);
                float free = FreeDistance(pivot, Quaternion.Euler(pitch, _orbitYaw, 0f) * Vector3.back, wanted);
                if (free <= best + 0.2f) continue;
                best = free;
                target = extra;
            }
            _autoPitch = Mathf.MoveTowards(_autoPitch, target, _autoPitchSpeed * Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Сколько можно отодвинуть камеру от точки взгляда, не заходя в препятствие: шагаем вдоль луча
        /// и сравниваем высоту точки над землёй с высотой препятствия в клетке; стены арены — тоже препятствие.
        /// </summary>
        private float FreeDistance(Vector3 pivot, Vector3 direction, float maxDistance)
        {
            Vector3 c = _grid.Center;
            Vector2 half = _grid.ArenaHalfExtents;
            for (float t = _collisionStep; t <= maxDistance; t += _collisionStep)
            {
                Vector3 p = pivot + direction * t;
                float above = p.y - c.y;
                bool outside = Mathf.Abs(p.x - c.x) > half.x || Mathf.Abs(p.z - c.z) > half.y;
                bool hit = above < 0.25f || (outside && above < _wallHeight);
                if (!hit && !outside)
                {
                    Vector2Int cell = _grid.WorldToCell(p);
                    hit = _grid.IsInside(cell) && _grid.GetObstacleHeight(cell) + 0.15f > above;
                }
                if (hit) return Mathf.Max(_distanceRange.x * 0.6f, t - _collisionMargin);
            }
            return maxDistance;
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
