using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Одно животное-добыча: жук, мышь, лягушка или птица. Бродит, замечает змею (если та не в траве),
    /// после короткой реакции убегает, а устав — замирает. Птица не бегает, а улетает и исчезает.
    /// Логика в данных (позиция, курс, высота прыжка), Transform только показывает результат.
    /// Обновляет животных WildlifeManager — экземпляры берутся из пула, Instantiate/Destroy нет.
    /// </summary>
    public class PreyAnimal : MonoBehaviour
    {
        private enum Mode
        {
            Idle,
            Wander,
            Alert,
            Flee,
            Rest,
            FlyAway
        }

        private static readonly float[] AvoidAngles = { 40f, -40f, 85f, -85f, 130f, -130f };

        [Header("Вид")]
        [Tooltip("Какое это животное")]
        [SerializeField] private PreyKind _kind;

        [Tooltip("Визуал: его наклоняем и покачиваем")]
        [SerializeField] private Transform _body;

        [Tooltip("Левое крыло (только у птицы)")]
        [SerializeField] private Transform _leftWing;

        [Tooltip("Правое крыло (только у птицы)")]
        [SerializeField] private Transform _rightWing;

        [Tooltip("Светящаяся метка, видная, пока змея нюхает")]
        [SerializeField] private GameObject _scentMark;

        [Header("Поведение")]
        [Tooltip("Задержка между «заметил змею» и бегством, сек")]
        [SerializeField, Min(0f)] private float _reactionTime = 0.3f;

        [Tooltip("Сколько секунд добыча стоит без сил после бегства")]
        [SerializeField, Min(0f)] private float _restTime = 1.6f;

        [Tooltip("Высота прыжка (лягушка), клеток")]
        [SerializeField, Min(0f)] private float _hopHeight = 0.5f;

        [Tooltip("Длительность прыжка (лягушка), сек")]
        [SerializeField, Min(0.05f)] private float _hopDuration = 0.42f;

        [Tooltip("Пауза между прыжками (лягушка), сек")]
        [SerializeField, Min(0f)] private float _hopPause = 0.18f;

        [Tooltip("Скорость поворота, град/с")]
        [SerializeField, Min(10f)] private float _turnSpeed = 540f;

        [Tooltip("Отступ от стен арены, клеток")]
        [SerializeField, Min(0f)] private float _wallMargin = 1.2f;

        [Tooltip("Длительность анимации появления, сек")]
        [SerializeField, Min(0.01f)] private float _appearTime = 0.35f;

        [Tooltip("Через сколько секунд улетевшая птица исчезает")]
        [SerializeField, Min(0.5f)] private float _flyAwayTime = 3f;

        public PreyKind Kind => _kind;
        public bool IsActive { get; private set; }

        /// <summary>Центр животного в мире (с учётом прыжка и полёта).</summary>
        public Vector3 Position => _pos + Vector3.up * _height;
        public float HeightAboveGround => _height;
        public Vector3 Velocity => _velocity;
        public bool IsFleeing => _mode == Mode.Flee || _mode == Mode.FlyAway;
        public bool IsFlying => _mode == Mode.FlyAway;

        /// <summary>Устал после бегства и не может сорваться с места.</summary>
        public bool IsResting => _mode == Mode.Rest;

        private GridManager _grid;
        private PreyStats _stats;
        private float _sleepAwareness = 1f;
        private Vector3 _baseScale = Vector3.one;

        private Mode _mode;
        private Vector3 _pos;
        private Vector3 _velocity;
        private Vector3 _moveDir = Vector3.forward;
        private Vector3 _threatPos;
        private float _yaw;
        private float _height;
        private float _stateTimer;
        private float _appear;
        private float _hopPhase;
        private float _speedBoost;
        private float _animTime;
        private float _lureRadius;
        private bool _lured;
        private Vector3 _lurePos;

        /// <summary>Однократная настройка из WildlifeManager.</summary>
        public void Initialize(GridManager grid, GameConfig config)
        {
            _grid = grid;
            _stats = config.GetPrey(_kind);
            _sleepAwareness = config.SleepAwareness;
            _lureRadius = config.LureRadius;
            _baseScale = transform.localScale;
            IsActive = false;
            if (_scentMark != null) _scentMark.SetActive(false);
            gameObject.SetActive(false);
        }

        public void Spawn(Vector3 groundPosition, float yaw)
        {
            IsActive = true;
            _pos = new Vector3(groundPosition.x, _grid.Center.y, groundPosition.z);
            _yaw = yaw;
            _moveDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            _velocity = Vector3.zero;
            _height = 0f;
            _hopPhase = 0f;
            _speedBoost = 0f;
            _appear = 0f;
            _animTime = Random.value * 10f;
            SetMode(Mode.Idle, Random.Range(0.4f, 2f));
            SetScent(false);
            gameObject.SetActive(true);
            ApplyVisual();
        }

        public void Despawn()
        {
            IsActive = false;
            SetScent(false);
            gameObject.SetActive(false);
        }

        public void SetScent(bool on)
        {
            if (_scentMark != null && _scentMark.activeSelf != on) _scentMark.SetActive(on);
        }

        /// <summary>Змея коснулась без броска: добыча срывается сразу, без задержки реакции, и отскакивает.</summary>
        public void Startle(Vector3 threatPosition)
        {
            if (!IsActive || _mode == Mode.FlyAway) return;
            _threatPos = threatPosition;
            _speedBoost = 0.25f;
            if (_kind == PreyKind.Bird) SetMode(Mode.FlyAway, _flyAwayTime);
            else if (_stats.FleeSpeed > 0f) SetMode(Mode.Flee, _stats.FleeTime);
        }

        /// <summary>Шаг поведения. night — птицы спят и замечают змею хуже.</summary>
        public void Tick(float dt, in PreyThreat a, in PreyThreat b, bool night)
        {
            if (!IsActive || dt <= 0f) return;
            _appear += dt;
            _animTime += dt;
            _stateTimer -= dt;
            if (_speedBoost > 0f) _speedBoost -= dt;
            bool sleeping = night && _kind == PreyKind.Bird;
            UpdateLure(a, b);

            if (CanNotice())
            {
                float radius = _stats.AwareRadius * _grid.CellSize * (sleeping ? _sleepAwareness : 1f);
                if (Notices(a, radius) || Notices(b, radius))
                    SetMode(Mode.Alert, sleeping ? _reactionTime * 2f : _reactionTime);
            }

            _velocity = Vector3.zero;
            switch (_mode)
            {
                case Mode.Idle:
                    if (_stateTimer <= 0f && !sleeping) StartWander();
                    break;

                case Mode.Wander:
                    if (_kind == PreyKind.Frog) HopMove(_moveDir, _stats.FleeSpeed * 0.35f, dt);
                    else if (!TryMove(_moveDir, _stats.WalkSpeed, dt)) _stateTimer = 0f;
                    if (_stateTimer <= 0f && _height <= 0f) SetMode(Mode.Idle, Random.Range(0.8f, 3f));
                    break;

                case Mode.Alert:
                    FaceTowards(_threatPos - _pos, dt);
                    if (_stateTimer <= 0f)
                    {
                        if (_kind == PreyKind.Bird) SetMode(Mode.FlyAway, _flyAwayTime);
                        else SetMode(Mode.Flee, _stats.FleeTime);
                    }
                    break;

                case Mode.Flee:
                    TickFlee(dt);
                    break;

                case Mode.Rest:
                    if (_stateTimer <= 0f) SetMode(Mode.Idle, Random.Range(0.5f, 1.5f));
                    break;

                case Mode.FlyAway:
                    TickFlyAway(dt);
                    if (_stateTimer <= 0f)
                    {
                        Despawn();
                        return;
                    }
                    break;
            }

            ApplyVisual();
        }

        // ---------------- Состояния ----------------

        private bool CanNotice()
        {
            return _stats.AwareRadius > 0f && (_mode == Mode.Idle || _mode == Mode.Wander);
        }

        private bool Notices(in PreyThreat t, float radius)
        {
            if (!t.Active) return false;
            float r = radius * t.AwarenessMultiplier;
            Vector3 d = t.Position - _pos;
            d.y = 0f;
            if (d.sqrMagnitude > r * r) return false;
            _threatPos = t.Position;
            return true;
        }

        /// <summary>Затаившаяся змея рядом подманивает: подрагивающий кончик хвоста похож на червяка.</summary>
        private void UpdateLure(in PreyThreat a, in PreyThreat b)
        {
            _lured = false;
            if (_kind == PreyKind.Beetle) return;
            float r = _lureRadius * _grid.CellSize;
            if (TryLure(a, r) || TryLure(b, r)) _lured = true;
        }

        private bool TryLure(in PreyThreat t, float radius)
        {
            if (!t.Active || !t.Lure) return false;
            Vector3 d = t.Position - _pos;
            d.y = 0f;
            if (d.sqrMagnitude > radius * radius) return false;
            _lurePos = t.Position;
            return true;
        }

        private void StartWander()
        {
            // Случайное направление, но с уклоном к центру арены — чтобы не толпились у стен.
            Vector3 toCenter = _grid.Center - _pos;
            toCenter.y = 0f;
            Vector3 random = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 dir = random + toCenter.normalized * 0.4f;
            if (_lured)
            {
                Vector3 toLure = _lurePos - _pos;
                toLure.y = 0f;
                dir = random * 0.35f + toLure.normalized;
            }
            _moveDir = dir.normalized;
            float duration = _kind == PreyKind.Frog ? _hopDuration + _hopPause : Random.Range(0.8f, 2.4f);
            _hopPhase = 0f;
            SetMode(Mode.Wander, duration);
        }

        private void TickFlee(float dt)
        {
            Vector3 away = _pos - _threatPos;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -_moveDir;
            away.Normalize();

            // Мышь бежит зигзагом — так её труднее поймать по прямой.
            if (_kind == PreyKind.Mouse) away = Quaternion.Euler(0f, Mathf.Sin(_animTime * 6f) * 28f, 0f) * away;
            _moveDir = Vector3.Slerp(_moveDir, away, 1f - Mathf.Exp(-8f * dt));

            float speed = _stats.FleeSpeed * (_speedBoost > 0f ? 1.6f : 1f);
            if (_kind == PreyKind.Frog) HopMove(_moveDir, speed, dt);
            else if (_kind == PreyKind.Beetle) TryMove(_moveDir, _stats.WalkSpeed, dt);
            else TryMove(_moveDir, speed, dt);

            if (_stateTimer <= 0f && _height <= 0f) SetMode(Mode.Rest, _restTime);
        }

        private void TickFlyAway(float dt)
        {
            Vector3 away = _pos - _threatPos;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = _moveDir;
            away.Normalize();
            _moveDir = Vector3.Slerp(_moveDir, away, 1f - Mathf.Exp(-4f * dt));

            // Взлёт по крутой дуге: сначала вверх, потом вдаль. Стены и камни птице не помеха.
            float cs = _grid.CellSize;
            float speed = _stats.FleeSpeed * cs;
            Vector3 horizontal = _moveDir * (speed * 0.8f * dt);
            float climb = speed * Mathf.Lerp(1.1f, 0.5f, Mathf.Clamp01(_height / (4f * cs))) * dt;
            _pos += horizontal;
            _height += climb;
            _velocity = (horizontal + Vector3.up * climb) / dt;
            FaceTowards(_moveDir, dt);
        }

        // ---------------- Движение ----------------

        /// <summary>Шаг по земле с обходом камней и стен арены. Возвращает false, если пройти некуда.</summary>
        private bool TryMove(Vector3 dir, float speed, float dt)
        {
            if (speed <= 0f) return false;
            float cs = _grid.CellSize;
            Vector3 probe = dir * (0.5f * cs);
            if (IsBlocked(_pos + probe))
            {
                bool found = false;
                for (int i = 0; i < AvoidAngles.Length && !found; i++)
                {
                    Vector3 alt = Quaternion.Euler(0f, AvoidAngles[i], 0f) * dir;
                    if (IsBlocked(_pos + alt * (0.6f * cs))) continue;
                    dir = alt;
                    found = true;
                }
                if (!found) return false;
                _moveDir = dir;
            }

            Vector3 step = dir * (speed * cs * dt);
            _pos += step;
            _velocity = step / dt;
            FaceTowards(dir, dt);
            return true;
        }

        /// <summary>Походка лягушки: прыжок дугой, затем короткая пауза на земле.</summary>
        private void HopMove(Vector3 dir, float speed, float dt)
        {
            float cycle = _hopDuration + _hopPause;
            _hopPhase += dt;
            if (_hopPhase >= cycle) _hopPhase -= cycle;

            if (_hopPhase < _hopDuration)
            {
                float k = _hopPhase / _hopDuration;
                _height = Mathf.Sin(k * Mathf.PI) * _hopHeight * _grid.CellSize;
                // В воздухе летим быстрее, чтобы средняя скорость совпадала с заданной.
                TryMove(dir, speed * cycle / _hopDuration, dt);
            }
            else
            {
                _height = 0f;
            }
        }

        private bool IsBlocked(Vector3 point)
        {
            Vector2 half = _grid.ArenaHalfExtents;
            float margin = _wallMargin * _grid.CellSize;
            Vector3 local = point - _grid.Center;
            if (Mathf.Abs(local.x) > half.x - margin || Mathf.Abs(local.z) > half.y - margin) return true;
            Vector2Int cell = _grid.WorldToCell(point);
            return _grid.IsInside(cell) && _grid.GetObstacleHeight(cell) > 0f;
        }

        private void FaceTowards(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            float target = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            _yaw = Mathf.MoveTowardsAngle(_yaw, target, _turnSpeed * dt);
        }

        private void SetMode(Mode mode, float duration)
        {
            _mode = mode;
            _stateTimer = duration;
            if (mode != Mode.FlyAway && mode != Mode.Wander && mode != Mode.Flee) _height = 0f;
        }

        // ---------------- Визуал ----------------

        private void ApplyVisual()
        {
            float k = Mathf.Clamp01(_appear / _appearTime);
            // Появление с лёгким перелётом масштаба (easeOutBack).
            float s = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
            transform.localScale = _baseScale * Mathf.Max(0.01f, s);
            transform.SetPositionAndRotation(_pos + Vector3.up * _height, Quaternion.Euler(0f, _yaw, 0f));
            if (_body == null) return;

            bool moving = _velocity.sqrMagnitude > 0.01f;
            float pitch = 0f;
            float bob = 0f;
            switch (_kind)
            {
                case PreyKind.Bird:
                    if (_mode == Mode.FlyAway) pitch = -20f;
                    else if (_mode == Mode.Idle) pitch = Mathf.Max(0f, Mathf.Sin(_animTime * 3.1f)) * 35f; // клюёт землю
                    break;
                case PreyKind.Mouse:
                    if (moving) bob = Mathf.Abs(Mathf.Sin(_animTime * 26f)) * 0.04f;
                    break;
                case PreyKind.Frog:
                    pitch = _height > 0f ? -18f : 0f;
                    break;
                case PreyKind.Beetle:
                    if (moving) bob = Mathf.Abs(Mathf.Sin(_animTime * 30f)) * 0.015f;
                    break;
            }
            _body.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            _body.localPosition = new Vector3(0f, bob, 0f);

            if (_leftWing == null || _rightWing == null) return;
            if (_mode == Mode.FlyAway)
            {
                float flap = Mathf.Sin(_animTime * 26f) * 55f;
                _leftWing.localRotation = Quaternion.Euler(0f, 0f, flap);
                _rightWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
            else
            {
                // На земле крылья сложены вдоль спины.
                _leftWing.localRotation = Quaternion.Euler(0f, -78f, -12f);
                _rightWing.localRotation = Quaternion.Euler(0f, 78f, 12f);
            }
        }
    }
}
