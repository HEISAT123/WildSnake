using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Одно животное-добыча: жук, мышь, лягушка, птица, кролик, ёж или рыба. Бродит, замечает змею (если та не в траве),
    /// после короткой реакции убегает, а устав — замирает. Птица не бегает, а улетает и исчезает.
    /// Кролик скачет; схваченный броском, он бьётся в кольцах змеи (Held), пока та его душит или он не вырвется.
    /// Ёж сворачивается в колючий шар; шар катится от толчка носом, в воде ёж разворачивается и плывёт к берегу
    /// беззащитным, а перед тем как развернуться, ненадолго выглядывает (Peek) — тогда его можно укусить.
    /// Мышь у мышиного городка убегает к ближайшей норке.
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
            FlyAway,
            Held,
            Curled,
            Peek,
            Swim,
            Wet
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

        [Tooltip("Что прячется, когда ёж сворачивается в шар (мордочка и лапы)")]
        [SerializeField] private GameObject _curlHide;

        [Tooltip("Сколько секунд ёж остаётся свёрнутым")]
        [SerializeField, Min(0.5f)] private float _curlTime = 4f;

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

        [Tooltip("Рыба: как часто выпрыгивает из воды, раз в секунду")]
        [SerializeField, Min(0f)] private float _leapRate = 0.06f;

        [Tooltip("Рыба: длительность прыжка над водой, сек")]
        [SerializeField, Min(0.1f)] private float _leapTime = 0.55f;

        public PreyKind Kind => _kind;
        public bool IsActive { get; private set; }

        /// <summary>Трофей дня: над ним горит метка, за ним охотится и соперница.</summary>
        public bool IsTrophy { get; private set; }

        /// <summary>Центр животного в мире (с учётом прыжка и полёта).</summary>
        public Vector3 Position => _pos + Vector3.up * _height;
        public float HeightAboveGround => _height;
        public Vector3 Velocity => _velocity;
        public bool IsFleeing => _mode == Mode.Flee || _mode == Mode.FlyAway;
        public bool IsFlying => _mode == Mode.FlyAway;

        /// <summary>Устал после бегства и не может сорваться с места.</summary>
        public bool IsResting => _mode == Mode.Rest;

        /// <summary>Схвачен змеёй и бьётся в её кольцах.</summary>
        public bool IsHeld => _mode == Mode.Held;

        /// <summary>Ёж свернулся колючим шаром: кусать нельзя — уколешься. Зато его можно катить носом.</summary>
        public bool IsCurled => _mode == Mode.Curled;

        /// <summary>Ёж высунул мордочку, проверяя, ушла ли змея: укус сейчас его поймает.</summary>
        public bool IsPeeking => _mode == Mode.Peek;

        /// <summary>Ёж в воде или отряхивается на берегу: свернуться не может, берётся даже касанием.</summary>
        public bool IsDefenseless => _mode == Mode.Swim || _mode == Mode.Wet;

        /// <summary>Ёж в воде (для подсказок).</summary>
        public bool IsSwimming => _mode == Mode.Swim;

        /// <summary>Свёрнутый ёж катится после толчка.</summary>
        public bool IsRolling => _mode == Mode.Curled && _rollVelocity.sqrMagnitude > 0.01f;

        /// <summary>Передвигается прыжками (лягушка, кролик).</summary>
        private bool Hops => _kind == PreyKind.Frog || _kind == PreyKind.Rabbit;

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
        private float _leap;

        // Ёж: перекатывание, заплыв к берегу, выглядывание.
        private float _rollSpeed = 4.5f;
        private float _swimSpeed = 0.8f;
        private float _peekTime = 1.1f;
        private float _wetTime = 2.2f;
        private Vector3 _rollVelocity;
        private float _rollAngle;
        private Vector3 _swimTarget;

        private bool IsFish => _kind == PreyKind.Fish;

        // На земле крылья сложены вдоль спины.
        private static readonly Quaternion WingFoldL = Quaternion.Euler(0f, -78f, -12f);
        private static readonly Quaternion WingFoldR = Quaternion.Euler(0f, 78f, 12f);

        // Визуал: сглаженные части позы.
        private Vector3 _bodyScale = Vector3.one;
        private float _noiseSeed;
        private float _lastYaw;
        private float _lean;
        private float _look;
        private float _slowPitch;
        private float _landSquash;
        private bool _wasAirborne;
        private float _wingOpen;

        /// <summary>Однократная настройка из WildlifeManager.</summary>
        public void Initialize(GridManager grid, GameConfig config)
        {
            _grid = grid;
            _stats = config.GetPrey(_kind);
            _sleepAwareness = config.SleepAwareness;
            _lureRadius = config.LureRadius;
            _rollSpeed = config.HedgehogRollSpeed;
            _swimSpeed = config.HedgehogSwimSpeed;
            _peekTime = config.HedgehogPeekTime;
            _wetTime = config.HedgehogWetTime;
            _baseScale = transform.localScale;
            if (_body != null) _bodyScale = _body.localScale;
            _noiseSeed = Random.value * 100f;
            IsActive = false;
            if (_scentMark != null) _scentMark.SetActive(false);
            gameObject.SetActive(false);
        }

        public void Spawn(Vector3 groundPosition, float yaw)
        {
            IsActive = true;
            _pos = new Vector3(groundPosition.x, 0f, groundPosition.z);
            _pos.y = Surface(_pos);
            _yaw = yaw;
            _moveDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            _velocity = Vector3.zero;
            _height = 0f;
            _hopPhase = 0f;
            _speedBoost = 0f;
            _appear = 0f;
            _leap = 0f;
            _rollVelocity = Vector3.zero;
            _rollAngle = 0f;
            _animTime = Random.value * 10f;
            _lastYaw = yaw;
            _lean = 0f;
            _look = 0f;
            _slowPitch = 0f;
            _landSquash = 0f;
            _wasAirborne = false;
            _wingOpen = 0f;
            SetMode(Mode.Idle, Random.Range(0.4f, 2f));
            IsTrophy = false;
            SetScent(false);
            gameObject.SetActive(true);
            ApplyVisual();
        }

        public void Despawn()
        {
            IsActive = false;
            IsTrophy = false;
            SetScent(false);
            gameObject.SetActive(false);
        }

        /// <summary>Метка над добычей: подсвечена нюхом или горит постоянно у трофея.</summary>
        public void SetScent(bool on)
        {
            on |= IsTrophy;
            if (_scentMark != null && _scentMark.activeSelf != on) _scentMark.SetActive(on);
        }

        /// <summary>Сделать добычу трофеем дня (или снять с неё эту роль).</summary>
        public void SetTrophy(bool on)
        {
            IsTrophy = on;
            SetScent(false);
        }

        /// <summary>Схватить: животное перестаёт бегать, его положение задаёт змея (HoldAt).</summary>
        public void Grab()
        {
            SetMode(Mode.Held, 0f);
            _velocity = Vector3.zero;
            SetScent(false);
        }

        /// <summary>Положение в кольцах змеи (вызывается каждый кадр, пока животное схвачено).</summary>
        public void HoldAt(Vector3 groundPosition, float yaw)
        {
            _pos = new Vector3(groundPosition.x, 0f, groundPosition.z);
            _pos.y = Surface(_pos);
            _yaw = yaw;
        }

        /// <summary>Вырвался: сразу бежит прочь от змеи.</summary>
        public void Release(Vector3 threatPosition)
        {
            if (_mode != Mode.Held) return;
            SetMode(Mode.Idle, 0f);
            Startle(threatPosition);
        }

        /// <summary>Змея коснулась без броска: добыча срывается сразу, без задержки реакции, и отскакивает.</summary>
        public void Startle(Vector3 threatPosition)
        {
            if (!IsActive || _mode == Mode.FlyAway || _mode == Mode.Held) return;
            _threatPos = threatPosition;
            if (_kind == PreyKind.Hedgehog)
            {
                // В воде и мокрым ёж свернуться не может; уже свёрнутый просто дольше держит шар.
                if (IsDefenseless) return;
                if (_mode == Mode.Curled) _stateTimer = Mathf.Max(_stateTimer, _curlTime * 0.6f);
                else SetMode(Mode.Curled, _curlTime);
                return;
            }
            _speedBoost = 0.25f;
            if (_kind == PreyKind.Bird) SetMode(Mode.FlyAway, _flyAwayTime);
            else if (_stats.FleeSpeed > 0f) SetMode(Mode.Flee, _stats.FleeTime);
        }

        /// <summary>
        /// Змея толкнула свёрнутого ежа носом: колючий шар катится прочь от неё. Закатился в воду — разворачивается
        /// и плывёт к берегу беззащитным. Возвращает true, если толчок получился.
        /// </summary>
        public bool Push(Vector3 from)
        {
            if (!IsActive || _mode != Mode.Curled) return false;
            Vector3 dir = _pos - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = _moveDir;
            dir.Normalize();
            _rollVelocity = dir * (_rollSpeed * _grid.CellSize);
            _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            // Пока катится — не разворачивается.
            _stateTimer = Mathf.Max(_stateTimer, _curlTime * 0.75f);
            return true;
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
                {
                    // Ёж не бежит — сразу сворачивается в колючий шар.
                    if (_kind == PreyKind.Hedgehog) SetMode(Mode.Curled, _curlTime);
                    else SetMode(Mode.Alert, sleeping ? _reactionTime * 2f : _reactionTime);
                }
            }

            _velocity = Vector3.zero;
            if (IsFish) TickLeap(dt);
            switch (_mode)
            {
                case Mode.Idle:
                    if (_stateTimer <= 0f && !sleeping) StartWander();
                    break;

                case Mode.Wander:
                    if (_kind == PreyKind.Frog) HopMove(_moveDir, _stats.FleeSpeed * 0.35f, dt);
                    else if (_kind == PreyKind.Rabbit) HopMove(_moveDir, _stats.WalkSpeed, dt);
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

                case Mode.Held:
                    break;

                case Mode.Curled:
                    if (TickRoll(dt)) break;
                    if (_stateTimer > 0f) break;
                    // Змея ещё рядом — ёж сначала осторожно выглядывает, иначе просто разворачивается.
                    float peekRadius = _stats.AwareRadius * _grid.CellSize;
                    if (Notices(a, peekRadius) || Notices(b, peekRadius)) SetMode(Mode.Peek, _peekTime);
                    else SetMode(Mode.Idle, Random.Range(0.5f, 1.2f));
                    break;

                case Mode.Peek:
                    FaceTowards(_threatPos - _pos, dt);
                    if (_stateTimer > 0f) break;
                    float stillRadius = _stats.AwareRadius * _grid.CellSize;
                    if (Notices(a, stillRadius) || Notices(b, stillRadius)) SetMode(Mode.Curled, _curlTime);
                    else SetMode(Mode.Idle, Random.Range(0.3f, 0.8f));
                    break;

                case Mode.Swim:
                    TickSwim(dt);
                    break;

                case Mode.Wet:
                    if (_stateTimer <= 0f) SetMode(Mode.Idle, Random.Range(0.3f, 0.8f));
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

        /// <summary>Рыба изредка выпрыгивает из воды — так её видно издалека; испуганная — чаще.</summary>
        private void TickLeap(float dt)
        {
            if (_leap > 0f)
            {
                _leap -= dt;
                _height = _leap > 0f ? Mathf.Sin((1f - _leap / _leapTime) * Mathf.PI) * 0.55f * _grid.CellSize : 0f;
                return;
            }
            float rate = _mode == Mode.Flee ? _leapRate * 4f : _leapRate;
            if (Random.value < rate * dt) _leap = _leapTime;
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
            if (_kind == PreyKind.Rabbit) duration = (_hopDuration + _hopPause) * Random.Range(2, 5);
            _hopPhase = 0f;
            SetMode(Mode.Wander, duration);
        }

        private void TickFlee(float dt)
        {
            Vector3 away = _pos - _threatPos;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -_moveDir;
            away.Normalize();

            // Мышь бежит зигзагом — так её труднее поймать по прямой; у городка — прямо к ближайшей норке, если та не за змеёй.
            if (_kind == PreyKind.Mouse)
            {
                away = Quaternion.Euler(0f, Mathf.Sin(_animTime * 6f) * 28f, 0f) * away;
                int hole = _grid.NearestColonyHole(_pos, out float holeDist);
                if (hole >= 0 && holeDist < 6f * _grid.CellSize)
                {
                    Vector3 toHole = _grid.GetColonyHole(hole) - _pos;
                    toHole.y = 0f;
                    if (Vector3.Dot(toHole.normalized, away) > -0.2f) away = toHole.normalized;
                }
            }
            _moveDir = Vector3.Slerp(_moveDir, away, 1f - Mathf.Exp(-8f * dt));

            float speed = _stats.FleeSpeed * (_speedBoost > 0f ? 1.6f : 1f);
            if (Hops) HopMove(_moveDir, speed, dt);
            else if (_kind == PreyKind.Beetle) TryMove(_moveDir, _stats.WalkSpeed, dt);
            else TryMove(_moveDir, speed, dt);

            if (_stateTimer <= 0f && _height <= 0f) SetMode(Mode.Rest, _restTime);
        }

        /// <summary>
        /// Свёрнутый ёж катится после толчка и постепенно останавливается; от камней отскакивает.
        /// Скатился в воду — разворачивается и плывёт. Возвращает true, пока шар в движении.
        /// </summary>
        private bool TickRoll(float dt)
        {
            float speed = _rollVelocity.magnitude;
            if (speed < 0.05f)
            {
                _rollVelocity = Vector3.zero;
                return false;
            }
            float cs = _grid.CellSize;
            Vector3 next = _pos + _rollVelocity * dt;
            Vector2Int cell = _grid.WorldToCell(next);
            if (_grid.IsInside(cell) && _grid.IsWater(cell) && !_grid.IsBridge(cell))
            {
                _pos = next;
                StartSwim();
                return true;
            }
            if (IsRollBlocked(next) || _grid.CrossesHollowWall(_pos, next))
            {
                _rollVelocity *= -0.35f;
                return true;
            }
            _pos = next;
            _velocity = _rollVelocity;
            _rollAngle += speed / (0.22f * cs) * Mathf.Rad2Deg * dt;
            _rollVelocity = Vector3.MoveTowards(_rollVelocity, Vector3.zero, _rollSpeed * cs * 1.3f * dt);
            return true;
        }

        private bool IsRollBlocked(Vector3 point)
        {
            Vector2 half = _grid.ArenaHalfExtents;
            float margin = _wallMargin * _grid.CellSize;
            Vector3 local = point - _grid.Center;
            if (Mathf.Abs(local.x) > half.x - margin || Mathf.Abs(local.z) > half.y - margin) return true;
            Vector2Int cell = _grid.WorldToCell(point);
            return !_grid.IsInside(cell) || _grid.GetObstacleHeight(cell) > 0f;
        }

        /// <summary>В воде ёж разворачивается и гребёт к ближайшему берегу — в шар свернуться не может.</summary>
        private void StartSwim()
        {
            _rollVelocity = Vector3.zero;
            _rollAngle = 0f;
            _swimTarget = _pos;
            float cs = _grid.CellSize;
            float best = float.MaxValue;
            for (int r = 1; r <= 8 && best == float.MaxValue; r++)
            {
                for (int k = 0; k < 16; k++)
                {
                    float angle = k * Mathf.PI / 8f;
                    Vector3 p = _pos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (r * cs);
                    Vector2Int cell = _grid.WorldToCell(p);
                    if (!_grid.IsInside(cell) || _grid.IsWater(cell) || IsBlocked(p)) continue;
                    float d = (p - _pos).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    _swimTarget = p;
                }
            }
            SetMode(Mode.Swim, 12f);
        }

        private void TickSwim(float dt)
        {
            Vector3 to = _swimTarget - _pos;
            to.y = 0f;
            float cs = _grid.CellSize;
            if (to.sqrMagnitude > 1e-4f)
            {
                Vector3 step = to.normalized * Mathf.Min(to.magnitude, _swimSpeed * cs * dt);
                _pos += step;
                _velocity = step / dt;
                FaceTowards(to, dt);
            }
            // Выбрался на сушу (или так и не нашёл берега) — отряхивается.
            Vector2Int cell = _grid.WorldToCell(_pos);
            bool onLand = !_grid.IsInside(cell) || !_grid.IsWater(cell);
            if (onLand || _stateTimer <= 0f) SetMode(Mode.Wet, _wetTime);
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
            if (IsStepBlocked(_pos + probe))
            {
                bool found = false;
                for (int i = 0; i < AvoidAngles.Length && !found; i++)
                {
                    Vector3 alt = Quaternion.Euler(0f, AvoidAngles[i], 0f) * dir;
                    if (IsStepBlocked(_pos + alt * (0.6f * cs))) continue;
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

        /// <summary>Шаг из текущей точки: кроме камней и воды, полое бревно пускает только через торцы.</summary>
        private bool IsStepBlocked(Vector3 point)
        {
            return IsBlocked(point) || (!IsFish && _grid.CrossesHollowWall(_pos, point));
        }

        private bool IsBlocked(Vector3 point)
        {
            Vector2 half = _grid.ArenaHalfExtents;
            float margin = _wallMargin * _grid.CellSize;
            Vector3 local = point - _grid.Center;
            if (Mathf.Abs(local.x) > half.x - margin || Mathf.Abs(local.z) > half.y - margin) return true;
            Vector2Int cell = _grid.WorldToCell(point);
            if (!_grid.IsInside(cell)) return false;
            // Рыба не выходит из воды, в воду заходит только лягушка.
            if (IsFish) return !_grid.IsWater(cell);
            if (_kind != PreyKind.Frog && _grid.IsWater(cell)) return true;
            return _grid.GetObstacleHeight(cell) > 0f;
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
            if (mode != Mode.Curled)
            {
                _rollVelocity = Vector3.zero;
                _rollAngle = 0f;
            }
            if (mode != Mode.FlyAway && mode != Mode.Wander && mode != Mode.Flee && !(IsFish && _leap > 0f)) _height = 0f;
        }

        // ---------------- Визуал ----------------

        /// <summary>По чему идёт животное: лягушка держится на воде, рыба — под самой поверхностью, остальные — по земле.</summary>
        private float Surface(Vector3 p)
        {
            if (IsFish)
            {
                float level = _grid.WaterLevelAt(p);
                return level > float.NegativeInfinity ? level - 0.12f * _grid.CellSize : _grid.GroundHeight(p);
            }
            // Плывущий ёж держит над водой только нос и спину.
            if (_mode == Mode.Swim) return _grid.SurfaceHeight(p) - 0.12f * _grid.CellSize;
            return _kind == PreyKind.Frog ? _grid.SurfaceHeight(p) : _grid.GroundHeight(p);
        }

        private void ApplyVisual()
        {
            // Шаги идут по горизонтали, а высоту берём у рельефа под ногами.
            _pos.y = Surface(_pos);
            float k = Mathf.Clamp01(_appear / _appearTime);
            // Появление с лёгким перелётом масштаба (easeOutBack).
            float s = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
            transform.localScale = _baseScale * Mathf.Max(0.01f, s);
            transform.SetPositionAndRotation(_pos + Vector3.up * _height, Quaternion.Euler(0f, _yaw, 0f));
            if (_body == null) return;

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float cs = _grid.CellSize;
            float speed = _velocity.magnitude / cs;
            bool moving = speed > 0.1f;
            // Крен на вираже: животное ложится в поворот, а не крутится волчком.
            float turn = Mathf.DeltaAngle(_lastYaw, _yaw) / dt;
            _lastYaw = _yaw;
            float lean = moving ? Mathf.Clamp(-turn * 0.035f, -14f, 14f) : 0f;
            _lean = Mathf.Lerp(_lean, lean, 1f - Mathf.Exp(-8f * dt));
            // Приземление — короткое сплющивание.
            bool airborne = _height > 0.02f * cs;
            if (_wasAirborne && !airborne) _landSquash = 1f;
            _wasAirborne = airborne;
            _landSquash = Mathf.MoveTowards(_landSquash, 0f, dt * 5f);
            UpdateWings(dt);

            if (_kind == PreyKind.Hedgehog)
            {
                bool curled = _mode == Mode.Curled;
                if (_curlHide != null && _curlHide.activeSelf == curled) _curlHide.SetActive(!curled);
                if (_curlHide != null && _mode == Mode.Peek) _curlHide.transform.localScale = Vector3.one * (0.75f + 0.08f * Mathf.Sin(_animTime * 18f));
                else if (_curlHide != null) _curlHide.transform.localScale = Vector3.one;
                // Выглядывая, ёж ещё наполовину свёрнут — видно, что вот-вот снова спрячется.
                Vector3 target = curled ? new Vector3(1.12f, 0.82f, 0.82f) : _mode == Mode.Peek ? new Vector3(1.06f, 0.9f, 0.9f) : Vector3.one;
                if (!curled && _mode != Mode.Peek) target.y += Mathf.Sin(_animTime * 3.2f) * 0.02f; // дышит
                _body.localScale = Vector3.Lerp(_body.localScale, target, 1f - Mathf.Exp(-14f * dt));
                if (curled)
                {
                    // Катится вокруг центра шара, а не вокруг точки на земле.
                    Quaternion rolled = Quaternion.Euler(_rollAngle, 0f, 0f);
                    var center = new Vector3(0f, 0.17f, 0f);
                    _body.localRotation = rolled;
                    _body.localPosition = center - rolled * center;
                    return;
                }
                if (_mode == Mode.Swim || _mode == Mode.Wet)
                {
                    // Плывёт — покачивается на воде и гребёт; на берегу отряхивается мелкой дрожью.
                    float shake = _mode == Mode.Wet ? Mathf.Sin(_animTime * 46f) * 12f : Mathf.Sin(_animTime * 5f) * 6f;
                    float paddle = _mode == Mode.Swim ? Mathf.Sin(_animTime * 9f) * 3f : 0f;
                    _body.localRotation = Quaternion.Euler((_mode == Mode.Swim ? -12f : 0f) + paddle, 0f, shake);
                    _body.localPosition = Vector3.zero;
                    return;
                }
            }

            if (_mode == Mode.Held)
            {
                // Бьётся в кольцах: частая неровная дрожь.
                _body.localRotation = Quaternion.Euler(Mathf.Sin(_animTime * 41f) * 14f, Mathf.Sin(_animTime * 23f) * 10f, Mathf.Sin(_animTime * 33f) * 16f);
                _body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(_animTime * 29f)) * 0.05f, 0f);
                if (_kind != PreyKind.Hedgehog) _body.localScale = _bodyScale;
                return;
            }

            // Поза = медленная часть (сглаживается: оглядки, стойка) + быстрая (шаг, дыхание — без сглаживания).
            float t = _animTime;
            float slowPitch = 0f, look = 0f;
            float pitch = 0f, yaw = 0f, roll = 0f, bob = 0f;
            float wide = 0f, tall = 0f, longer = 0f;
            switch (_kind)
            {
                case PreyKind.Mouse:
                    if (moving)
                    {
                        // Семенит: тело тянется и сжимается в такт скачкам, нос клюёт.
                        float ph = t * 26f;
                        bob = Mathf.Abs(Mathf.Sin(ph)) * 0.045f;
                        longer = Mathf.Sin(ph * 2f) * 0.07f;
                        pitch = Mathf.Sin(ph * 2f) * 5f;
                    }
                    else
                    {
                        // Принюхивается, вертит головой, иногда встаёт столбиком.
                        look = Noise(t * 0.6f, 0f) * 70f;
                        slowPitch = Noise(t * 0.25f, 5f) > 0.3f ? -28f : 0f;
                        pitch = Mathf.Max(0f, Mathf.Sin(t * 17f)) * 4f * Mathf.Max(0f, Mathf.Sin(t * 0.9f));
                        tall = Mathf.Sin(t * 7f) * 0.025f;
                    }
                    break;
                case PreyKind.Frog:
                case PreyKind.Rabbit:
                {
                    bool frog = _kind == PreyKind.Frog;
                    if (airborne)
                    {
                        // В прыжке: нос вверх на взлёте, вниз к посадке; тело вытянуто в полёте.
                        float kk = _leap > 0f ? 1f - _leap / _leapTime : Mathf.Clamp01(_hopPhase / _hopDuration);
                        pitch = Mathf.Lerp(frog ? -32f : -22f, frog ? 22f : 16f, kk);
                        longer = Mathf.Sin(kk * Mathf.PI) * (frog ? 0.22f : 0.16f);
                        tall = -longer * 0.45f;
                    }
                    else if (moving)
                    {
                        // Пауза между прыжками: к её концу приседает — копит толчок.
                        float pk = Mathf.Clamp01((_hopPhase - _hopDuration) / Mathf.Max(0.01f, _hopPause));
                        tall = -pk * 0.12f;
                        wide = pk * 0.05f;
                        pitch = pk * 6f;
                    }
                    else if (frog)
                    {
                        // Сидит: горло раздувается, изредка поворачивается.
                        tall = Mathf.Abs(Mathf.Sin(t * 5.5f)) * 0.035f;
                        wide = tall * 0.6f;
                        look = Noise(t * 0.3f, 3f) * 40f;
                    }
                    else
                    {
                        // Кролик: дёргает носом, поводит головой, иногда приподнимается осмотреться.
                        pitch = Mathf.Sin(t * 21f) * 1.5f * Mathf.Max(0f, Mathf.Sin(t * 1.3f));
                        look = Noise(t * 0.45f, 7f) * 60f;
                        slowPitch = Noise(t * 0.22f, 2f) > 0.35f ? -18f : 0f;
                        tall = Mathf.Sin(t * 4.5f) * 0.02f;
                    }
                    // Посадка: сплющивается и клюёт носом.
                    tall -= _landSquash * 0.16f;
                    wide += _landSquash * 0.09f;
                    pitch += _landSquash * 8f;
                    break;
                }
                case PreyKind.Beetle:
                    if (moving)
                    {
                        // Переваливается с боку на бок на шести лапах.
                        roll = Mathf.Sin(t * 30f) * 5f;
                        bob = Mathf.Abs(Mathf.Sin(t * 30f)) * 0.015f;
                        yaw = Mathf.Sin(t * 15f) * 4f;
                    }
                    else
                    {
                        look = Noise(t * 0.5f, 9f) * 30f;
                        pitch = Mathf.Sin(t * 2f) * 2f;
                    }
                    break;
                case PreyKind.Bird:
                    if (_mode == Mode.FlyAway)
                    {
                        // В полёте тело подбрасывает каждым взмахом.
                        float ph = t * 26f;
                        slowPitch = -20f;
                        pitch = Mathf.Sin(ph) * 4f;
                        bob = -Mathf.Sin(ph) * 0.03f;
                    }
                    else if (moving)
                    {
                        // Скачет на двух лапках.
                        float ph = t * 16f;
                        bob = Mathf.Abs(Mathf.Sin(ph)) * 0.06f;
                        pitch = -Mathf.Abs(Mathf.Sin(ph)) * 6f;
                        tall = -Mathf.Abs(Mathf.Cos(ph)) * 0.05f;
                    }
                    else
                    {
                        // Резко клюёт и дёргано оглядывается.
                        float peck = Mathf.Max(0f, Mathf.Sin(t * 3.1f));
                        pitch = peck * peck * peck * 40f;
                        look = Mathf.Round(Noise(t * 1.2f, 4f) * 4f) * 14f;
                        tall = Mathf.Sin(t * 6f) * 0.015f;
                    }
                    break;
                case PreyKind.Fish:
                    if (_leap > 0f)
                    {
                        // В прыжке тело следует дуге и бьёт хвостом.
                        pitch = Mathf.Lerp(-40f, 40f, 1f - _leap / _leapTime);
                        yaw = Mathf.Sin(t * 30f) * 18f;
                    }
                    else
                    {
                        // Виляет всем телом; на ходу — чаще и сильнее, с креном.
                        float f = moving ? 16f : 5f;
                        yaw = Mathf.Sin(t * f) * (moving ? 16f : 7f);
                        roll = Mathf.Sin(t * f - 1.2f) * (moving ? 7f : 3f);
                        bob = Mathf.Sin(t * 1.7f) * 0.02f;
                    }
                    break;
                case PreyKind.Hedgehog:
                    if (moving)
                    {
                        // Семенит вперевалку.
                        roll = Mathf.Sin(t * 14f) * 5f;
                        bob = Mathf.Abs(Mathf.Sin(t * 14f)) * 0.02f;
                    }
                    else
                    {
                        look = Noise(t * 0.4f, 6f) * 40f;
                        pitch = Mathf.Max(0f, Mathf.Sin(t * 13f)) * 3f * Mathf.Max(0f, Mathf.Sin(t * 0.8f));
                    }
                    break;
            }

            float ease = 1f - Mathf.Exp(-6f * dt);
            _slowPitch = Mathf.Lerp(_slowPitch, slowPitch, ease);
            _look = Mathf.Lerp(_look, look, ease);
            _body.localRotation = Quaternion.Euler(_slowPitch + pitch, _look + yaw, _lean + roll);
            _body.localPosition = new Vector3(0f, bob, 0f);
            if (_kind != PreyKind.Hedgehog)
                _body.localScale = Vector3.Scale(_bodyScale, new Vector3(1f + wide - longer * 0.3f, 1f + tall, 1f + longer));
        }

        /// <summary>Крылья: плавно раскрываются при взлёте и складываются на земле; взмах вниз быстрее, чем вверх.</summary>
        private void UpdateWings(float dt)
        {
            if (_leftWing == null || _rightWing == null) return;
            bool flying = _mode == Mode.FlyAway;
            _wingOpen = Mathf.MoveTowards(_wingOpen, flying ? 1f : 0f, dt * (flying ? 8f : 3f));
            float ph = _animTime * 26f;
            float flap = 12f + Mathf.Sin(ph + 0.5f * Mathf.Cos(ph)) * 55f;
            // На земле изредка вздрагивает крыльями.
            float flick = !flying && _mode != Mode.Held && Noise(_animTime * 0.7f, 8f) > 0.42f ? Mathf.Abs(Mathf.Sin(_animTime * 30f)) * 0.25f : 0f;
            float open = Mathf.Max(_wingOpen, flick);
            _leftWing.localRotation = Quaternion.Slerp(WingFoldL, Quaternion.Euler(0f, 0f, flap), open);
            _rightWing.localRotation = Quaternion.Slerp(WingFoldR, Quaternion.Euler(0f, 0f, -flap), open);
        }

        /// <summary>Плавный шум -0.5..0.5 со своим зерном у каждого животного: оглядки не синхронны во всей стае.</summary>
        private float Noise(float time, float lane)
        {
            return Mathf.PerlinNoise(time + _noiseSeed, lane * 3.7f + _noiseSeed * 0.13f) - 0.5f;
        }
    }
}
