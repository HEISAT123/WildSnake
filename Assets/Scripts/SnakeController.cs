using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Логика змейки: движение по желаемому направлению и темпу, прыжок, замах и бросок, рост и столкновения.
    /// Управляет ею «намерение» (SetMoveIntent, SetCoil, TryStrike, RequestJump) — его выдают и игрок
    /// (PlayerSnakeInput), и ИИ (SnakeAI). Без намерения змея стоит. Голова поворачивается с ограниченной
    /// скоростью, поэтому разворот идёт дугой, а тело повторяет траекторию головы.
    /// Голова пишет траекторию в кольцевой буфер точек с равным шагом; тело (меш и точки позвоночника
    /// для столкновений) строится по этой траектории. Удары не убивают сразу: змея сообщает об уроне
    /// (событие Hurt), а жива ли она — решает GameManager.
    /// Выносливость (Stamina) тратят быстрое ползание и бросок; неподвижная змея уходит в засаду (AmbushTracker);
    /// промах броском на секунду делает змею «открытой» — медленнее и заметнее.
    /// </summary>
    public class SnakeController : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Параметры баланса")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля (границы, препятствия, размер клетки)")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Логическая голова: позиция и поворот. Визуал головы — её дочерние объекты")]
        [SerializeField] private Transform _head;

        [Header("Роль")]
        [Tooltip("Змея игрока (её ведёт PlayerSnakeInput, а в меню — автопилот)")]
        [SerializeField] private bool _isPlayer = true;

        [Tooltip("Множитель скорости этой змейки (соперник чуть медленнее игрока)")]
        [SerializeField, Range(0.3f, 1.5f)] private float _speedScale = 1f;

        [Header("Тело")]
        [Tooltip("Шаг записи траектории головы (доля клетки). Меньше — точнее, но больше памяти")]
        [SerializeField, Range(0.02f, 0.5f)] private float _pathSampleDistance = 0.1f;

        [Tooltip("Шаг точек позвоночника для столкновений (доля клетки)")]
        [SerializeField, Range(0.1f, 1f)] private float _spineSpacing = 0.4f;

        [Tooltip("Радиус тела (доля клетки)")]
        [SerializeField, Range(0.1f, 0.5f)] private float _bodyRadius = 0.36f;

        [Tooltip("Скорость, с которой длина догоняет целевую при росте и укорачивании, клеток в секунду")]
        [SerializeField, Min(0.1f)] private float _lengthChangeSpeed = 4f;

        [Header("Столкновения")]
        [Tooltip("Радиус головы для препятствий и опасностей (доля клетки)")]
        [SerializeField, Min(0.1f)] private float _headRadius = 0.38f;

        [Tooltip("Отступ головы от стены, при котором засчитывается удар (доля клетки)")]
        [SerializeField, Min(0f)] private float _wallMargin = 0.45f;

        [Tooltip("На сколько голова должна быть выше или ниже тела, чтобы пролететь над ним (доля клетки)")]
        [SerializeField, Min(0.1f)] private float _jumpClearance = 0.6f;

        [Tooltip("Запас при перепрыгивании препятствия (доля клетки): чем больше, тем проще")]
        [SerializeField, Min(0f)] private float _obstacleForgiveness = 0.1f;

        [Tooltip("Неуязвимость после удара, сек")]
        [SerializeField, Min(0f)] private float _invulnerabilityTime = 1.2f;

        [Header("Визуал головы")]
        [Tooltip("Высота оси тела над полем (доля клетки)")]
        [SerializeField] private float _bodyHeight = 0.36f;

        [Tooltip("Крен головы в повороте, град")]
        [SerializeField, Range(0f, 45f)] private float _bankAngle = 14f;

        [Tooltip("На сколько свёрнутая для броска змея приподнимает голову (доля клетки)")]
        [SerializeField, Min(0f)] private float _coilLift = 0.22f;

        [Tooltip("Насколько задирает голову при замахе, град")]
        [SerializeField, Range(0f, 40f)] private float _coilPitch = 14f;

        [Tooltip("Наклон головы в прыжке на единицу вертикальной скорости, град")]
        [SerializeField, Min(0f)] private float _pitchPerSpeed = 2.5f;

        [Tooltip("Длительность сквош-стретча (еда, приземление), сек")]
        [SerializeField, Min(0.01f)] private float _squashDuration = 0.25f;

        [Tooltip("Сила сквош-стретча")]
        [SerializeField, Range(0f, 0.6f)] private float _squashAmount = 0.25f;

        /// <summary>(точка удара, урон, источник) — змея ударилась; решение о смерти за GameManager.</summary>
        public event Action<Vector3, float, DamageSource> Hurt;
        public event Action<Vector3> Died;
        public event Action Jumped;
        public event Action<Vector3> Landed;

        /// <summary>Бросок начался; аргумент — сила замаха 0..1.</summary>
        public event Action<float> StrikeStarted;

        /// <summary>Бросок закончился (долетел или упёрся в препятствие).</summary>
        public event Action StrikeEnded;

        /// <summary>Бросок никого не задел — змея на время «открыта».</summary>
        public event Action StrikeMissed;

        /// <summary>Хотела броситься, но не хватило выносливости.</summary>
        public event Action OutOfStamina;
        public event Action Respawned;

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                // Запоминаем кадр старта: Space или клик, которым начали игру, не должен сразу стать прыжком или броском.
                if (value && !_isRunning) _runStartFrame = Time.frameCount;
                _isRunning = value;
            }
        }

        public bool IsAlive { get; private set; }
        public bool IsPlayer => _isPlayer;
        public bool IsStriking => _strikeTimer > 0f;
        public bool IsInvulnerable => _invulnerableTimer > 0f;

        /// <summary>Змея свернулась для броска (замах).</summary>
        public bool IsCoiled => _coiled;

        /// <summary>Выносливость: быстрое ползание, бросок, обвивание.</summary>
        public Stamina Stamina => _stamina;

        /// <summary>Засада: неподвижная змея сливается с местностью.</summary>
        public AmbushTracker Ambush => _ambush;

        /// <summary>После промаха змея на секунду медленнее и заметнее.</summary>
        public bool IsExposed => _exposedTimer > 0f;

        /// <summary>Сила замаха 0..1 (растёт, пока змея свёрнута).</summary>
        public float Coil01 => _coiled ? Mathf.Clamp01(_coilTime / _config.CoilChargeTime) : 0f;

        /// <summary>Змея лежит в высокой траве: добыча и орёл её почти не видят. В прыжке укрытия нет.</summary>
        public bool IsHidden => _hidden;

        /// <summary>Тело уже минимальной длины — худеть от голода некуда.</summary>
        public bool IsAtMinLength => _targetLengthCells <= _config.StartLength + 0.01f;

        /// <summary>Готовность броска: 1 — можно бросаться.</summary>
        public float StrikeReady01 => _strikeCooldownTimer > 0f ? 1f - Mathf.Clamp01(_strikeCooldownTimer / Mathf.Max(0.01f, _config.StrikeCooldown)) : 1f;

        /// <summary>Текущий темп (по намерению).</summary>
        public SnakePace Pace => _pace;

        /// <summary>Текущая скорость, клеток в секунду.</summary>
        public float Speed => _speed;
        public float WalkSpeed => _config.WalkSpeed;
        public bool IsMoving => _speed > 0.05f;

        /// <summary>Пройденный путь в мировых единицах: по нему тело «бежит» волной, а при остановке замирает.</summary>
        public float Odometer => _odometer;

        /// <summary>Скорость поворота относительно предельной, -1..1 (крен головы и тела).</summary>
        public float Turn01 => _turn01;

        public Transform Head => _head;
        public Vector3 HeadPosition => _headPos;
        public Vector3 Forward => Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
        public float Yaw => _yaw;
        public float HeightAboveGround => _height;
        public float CellSize => _cellSize;
        public float BodyLengthCells => _lengthCells;
        public float BodyLengthWorld => _lengthCells * _cellSize;
        public float MaxBodyLengthCells => _config.MaxLength;
        public float BodyRadiusWorld => _bodyRadius * _cellSize;
        public float HeadRadiusWorld => _headRadius * _cellSize;
        public float JumpHeightWorld => _config.JumpHeight * _cellSize;

        /// <summary>Множитель скорости от улучшений (ставит GameManager).</summary>
        public float ExternalSpeedMultiplier { get; set; } = 1f;

        /// <summary>Множитель дальности броска от улучшений.</summary>
        public float StrikeDistanceScale { get; set; } = 1f;

        /// <summary>Множитель цены броска в выносливости от улучшений.</summary>
        public float StrikeCostScale { get; set; } = 1f;

        /// <summary>Автопилот: змея игрока управляется ИИ (живой фон главного меню).</summary>
        public bool AutoPilot { get; set; }

        /// <summary>Сколько точек позвоночника сейчас активно ([0] — голова).</summary>
        public int SpineCount => _spineCount;

        public bool IsGrounded => _grounded;

        private bool _isRunning;
        private int _runStartFrame;

        // Траектория головы: кольцевой буфер, _pathHead — индекс самой свежей точки.
        private Vector3[] _path;
        private int _pathHead;
        private int _pathCount;

        // Позвоночник для столкновений: точки с шагом _spineSpacing, [0] — голова.
        private Vector3[] _spine;
        private int _spineCount;

        private Vector3 _headPos;
        private float _yaw;
        private float _speed;
        private float _turn01;
        private float _odometer;
        private float _lengthCells;
        private float _targetLengthCells;

        // Намерение: куда и как ползти, свёрнута ли, где прицел.
        private Vector3 _moveDir;
        private float _moveAmount;
        private SnakePace _pace = SnakePace.Walk;
        private bool _jumpRequested;
        private bool _coiled;
        private float _coilTime;
        private float _coilBaseYaw;
        private float _coilBlend;
        private Vector3 _aimDir = Vector3.forward;

        private float _height;
        private float _verticalSpeed;
        private bool _grounded;

        private readonly Stamina _stamina = new Stamina();
        private readonly AmbushTracker _ambush = new AmbushTracker();
        private float _exposedTimer;
        private bool _strikeHit;
        private float _strikeTimer;
        private float _strikeSpeed;
        private float _strikeCooldownTimer;
        private float _invulnerableTimer;
        private bool _hidden;

        private float _squashTimer;
        private Vector3 _headBaseScale;

        // Кэш геометрии, пересчитывается в ResetSnake.
        private float _cellSize = 1f;
        private float _groundY;
        private float _sampleDist;

        private void Awake()
        {
            _headBaseScale = _head.localScale;

            // Всё выделяем заранее под максимальную длину: в игре аллокаций нет.
            int pathCapacity = Mathf.CeilToInt((_config.MaxLength + 2f) / _pathSampleDistance) + 8;
            _path = new Vector3[pathCapacity];
            _spine = new Vector3[Mathf.CeilToInt(_config.MaxLength / _spineSpacing) + 2];

            _stamina.RegenPerSecond = _config.StaminaRegen;
            _stamina.RegenDelay = _config.StaminaRegenDelay;
            _ambush.Delay = _config.AmbushDelay;
            _ambush.Ramp = _config.AmbushRamp;
            _ambush.OpenAwareness = _config.AmbushAwareness;
            _ambush.CoverAwareness = _config.AmbushCoverAwareness;
        }

        /// <summary>Поставить змейку в точку с заданным курсом (град, 0 — вдоль +Z).</summary>
        public void ResetSnake(Vector3 position, float yaw)
        {
            _cellSize = _grid.CellSize;
            _groundY = _grid.Center.y + _bodyHeight * _cellSize;
            _sampleDist = _pathSampleDistance * _cellSize;

            _lengthCells = _config.StartLength;
            _targetLengthCells = _lengthCells;
            _yaw = yaw;
            _speed = 0f;
            _turn01 = 0f;
            _height = 0f;
            _verticalSpeed = 0f;
            _grounded = true;
            _strikeTimer = 0f;
            _strikeCooldownTimer = 0f;
            _invulnerableTimer = 0f;
            _hidden = false;
            _squashTimer = _squashDuration;
            _moveDir = Vector3.zero;
            _moveAmount = 0f;
            _pace = SnakePace.Walk;
            _jumpRequested = false;
            _coiled = false;
            _coilTime = 0f;
            _coilBlend = 0f;
            _exposedTimer = 0f;
            _strikeHit = false;
            _stamina.Reset(_config.MaxStamina);
            _ambush.Break();
            IsAlive = true;
            IsRunning = false;

            _headPos = new Vector3(position.x, _groundY, position.z);

            // Заполняем траекторию прямой линией позади головы.
            Vector3 forward = Forward;
            int needed = Mathf.Min(_path.Length, Mathf.CeilToInt((_lengthCells + 1f) / _pathSampleDistance) + 2);
            _pathHead = _path.Length - 1;
            _pathCount = 0;
            for (int j = needed - 1; j >= 0; j--) PushPathPoint(_headPos - forward * (j * _sampleDist));

            _head.localScale = _headBaseScale;
            ComputeSpine();
            ApplyHeadVisual(0f);
            Respawned?.Invoke();
        }

        // ---------------- Намерение (игрок и ИИ) ----------------

        /// <summary>
        /// Куда ползти: направление в мире (длина 0..1 — сила, как наклон стика) и темп.
        /// Нулевое направление — змея останавливается.
        /// </summary>
        public void SetMoveIntent(Vector3 direction, SnakePace pace)
        {
            direction.y = 0f;
            float amount = Mathf.Min(1f, direction.magnitude);
            _moveAmount = amount;
            _moveDir = amount > 1e-4f ? direction / direction.magnitude : Vector3.zero;
            _pace = pace;
        }

        /// <summary>Свернуться для броска (замах) и куда целиться. Свёрнутая змея стоит и копит силу броска.</summary>
        public void SetCoil(bool coil, Vector3 aim)
        {
            aim.y = 0f;
            if (aim.sqrMagnitude > 1e-6f) _aimDir = aim.normalized;
            if (coil == _coiled) return;
            if (coil && (!_grounded || IsStriking)) return;
            _coiled = coil;
            _coilTime = 0f;
            _coilBaseYaw = _yaw;
        }

        /// <summary>
        /// Бросок-укус в сторону прицела (в пределах угла от курса). Если змея свёрнута — летит дальше
        /// на силу замаха. Возвращает false, если бросок сейчас невозможен.
        /// </summary>
        public bool TryStrike(Vector3 aim)
        {
            if (!IsAlive || !IsRunning || IsStriking || _strikeCooldownTimer > 0f || !_grounded) return false;
            if (Time.frameCount - _runStartFrame < 2) return false;

            float charge = Coil01;
            if (!_stamina.TrySpend(_config.StrikeStaminaCost(charge) * StrikeCostScale))
            {
                OutOfStamina?.Invoke();
                return false;
            }

            float limit = _coiled ? _config.CoilAimLimit : _config.BiteAimLimit;
            float baseYaw = _coiled ? _coilBaseYaw : _yaw;
            aim.y = 0f;
            if (aim.sqrMagnitude > 1e-6f)
            {
                float aimYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                _yaw = baseYaw + Mathf.Clamp(Mathf.DeltaAngle(baseYaw, aimYaw), -limit, limit);
            }

            _coiled = false;
            _coilTime = 0f;
            _strikeSpeed = _config.StrikeSpeed(charge) * StrikeDistanceScale;
            _strikeTimer = _config.StrikeDuration;
            _strikeCooldownTimer = _config.StrikeCooldown;
            _strikeHit = false;
            _exposedTimer = 0f;
            _ambush.Break();
            _squashTimer = 0f;
            StrikeStarted?.Invoke(charge);
            return true;
        }

        /// <summary>Бросок кого-то задел (поймала добычу, укусила соперницу) — промаха не будет.</summary>
        public void NotifyStrikeHit()
        {
            if (IsStriking) _strikeHit = true;
        }

        public void RequestJump()
        {
            if (Time.frameCount != _runStartFrame) _jumpRequested = true;
        }

        // ---------------- Рост и урон ----------------

        public void Grow(float cells)
        {
            _targetLengthCells = Mathf.Min(_config.MaxLength, _targetLengthCells + cells);
            _squashTimer = 0f;
        }

        public void Shrink(float cells)
        {
            _targetLengthCells = Mathf.Max(_config.StartLength, _targetLengthCells - cells);
        }

        /// <summary>
        /// Удар извне (орёл, соперница, препятствие на броске). awayDirection — куда оттолкнуть голову
        /// (нулевой — не толкать). После удара змея ненадолго неуязвима. Возвращает true, если удар засчитан.
        /// </summary>
        public bool TakeHit(float damage, DamageSource source, Vector3 awayDirection)
        {
            if (!IsAlive || _invulnerableTimer > 0f) return false;
            _invulnerableTimer = _invulnerabilityTime;
            _ambush.Break();
            PushAway(awayDirection, 0.35f * _cellSize);
            CancelStrike();
            _squashTimer = 0f;
            Hurt?.Invoke(_headPos, damage, source);
            return true;
        }

        /// <summary>Оттолкнуть голову по горизонтали (столкновение с чужим телом) и сбить скорость.</summary>
        public void PushAway(Vector3 direction, float distance)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return;
            _headPos += direction.normalized * distance;
            _speed *= 0.4f;
        }

        /// <summary>Оборвать бросок (упёрлась в препятствие, укусила тело).</summary>
        public void CancelStrike()
        {
            if (_strikeTimer <= 0f) return;
            EndStrike();
            _speed = Mathf.Min(_speed, _config.WalkSpeed * 0.5f);
        }

        private void EndStrike()
        {
            _strikeTimer = 0f;
            if (!_strikeHit)
            {
                _exposedTimer = _config.MissExposure;
                StrikeMissed?.Invoke();
            }
            StrikeEnded?.Invoke();
        }

        /// <summary>Гибель (здоровье кончилось) — решает GameManager.</summary>
        public void Kill()
        {
            if (IsAlive) Die();
        }

        /// <summary>Отметить в сетке клетки под змейкой и прямо перед ней (для появления предметов).</summary>
        public void MarkOccupiedCells()
        {
            MarkCell(_headPos);
            Vector3 forward = Forward;
            for (int j = 1; j <= 3; j++) MarkCell(_headPos + forward * (j * _cellSize));
            for (int i = 1; i < _spineCount; i++) MarkCell(_spine[i]);
        }

        /// <summary>
        /// Задевает ли точка (с радиусом) тело змейки начиная с точки позвоночника startIndex.
        /// Учитывает высоту: можно перепрыгнуть.
        /// </summary>
        public bool BodyHits(Vector3 point, float radius, int startIndex)
        {
            return BodyHits(point, radius, startIndex, out _);
        }

        /// <summary>То же, плюс точка позвоночника, которую задели (для направления отскока).</summary>
        public bool BodyHits(Vector3 point, float radius, int startIndex, out Vector3 hitPoint)
        {
            float r = radius + BodyRadiusWorld;
            float rSqr = r * r;
            float clearance = _jumpClearance * _cellSize;
            for (int i = Mathf.Max(0, startIndex); i < _spineCount; i++)
            {
                Vector3 d = _spine[i] - point;
                if (Mathf.Abs(d.y) >= clearance) continue;
                d.y = 0f;
                if (d.sqrMagnitude < rSqr)
                {
                    hitPoint = _spine[i];
                    return true;
                }
            }
            hitPoint = default;
            return false;
        }

        /// <summary>Точка позвоночника (0 — голова); индекс должен быть меньше SpineCount.</summary>
        public Vector3 GetSpinePoint(int index)
        {
            return _spine[index];
        }

        /// <summary>Точка траектории на расстоянии distance позади головы и направление «к голове» в ней.</summary>
        public void SampleSpine(float distance, out Vector3 position, out Vector3 forward)
        {
            Vector3 newest = _path[_pathHead];
            Vector3 toHead = _headPos - newest;
            float r = toHead.magnitude;
            if (distance <= r)
            {
                position = r > 1e-5f ? Vector3.Lerp(_headPos, newest, distance / r) : _headPos;
                forward = r > 1e-5f ? toHead : Forward;
                return;
            }

            float k = (distance - r) / _sampleDist;
            int i = (int)k;
            int last = _pathCount - 1;
            if (i >= last)
            {
                position = PathPoint(last);
                forward = last > 0 ? PathPoint(last - 1) - position : Forward;
                return;
            }

            Vector3 a = PathPoint(i);
            Vector3 b = PathPoint(i + 1);
            position = Vector3.Lerp(a, b, k - i);
            forward = a - b;
        }

        // ---------------- Кадр ----------------

        private void Update()
        {
            if (!IsRunning || !IsAlive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_strikeCooldownTimer > 0f) _strikeCooldownTimer -= dt;
            if (_invulnerableTimer > 0f) _invulnerableTimer -= dt;
            if (_exposedTimer > 0f) _exposedTimer -= dt;
            UpdateJump(dt);

            float previousYaw = _yaw;
            if (IsStriking) UpdateStrike(dt);
            else if (_coiled) UpdateCoil(dt);
            else UpdateCrawl(dt);
            _turn01 = Mathf.Clamp(Mathf.DeltaAngle(previousYaw, _yaw) / (dt * _config.TurnRate), -1f, 1f);
            UpdateStaminaAndAmbush(dt);
            _coilBlend = Mathf.MoveTowards(_coilBlend, _coiled ? 1f : 0f, dt * 5f);

            float step = _speed * _cellSize * dt;
            _headPos += Forward * step;
            _odometer += step;
            _headPos.y = _groundY + _height;
            _lengthCells = Mathf.MoveTowards(_lengthCells, _targetLengthCells, _lengthChangeSpeed * dt);

            bool blocked = ClampToArena(out Vector3 normal) | HitsObstacle(out Vector3 obstacleNormal);
            if (blocked)
            {
                // Камни и стены непроницаемы: на шагу змея скользит вдоль них, а с броска — бьётся головой.
                if (IsStriking)
                {
                    CancelStrike();
                    TakeHit(_config.WallDamage, DamageSource.Collision, Vector3.zero);
                }
                else
                {
                    Vector3 n = obstacleNormal.sqrMagnitude > 0f ? obstacleNormal : normal;
                    float into = -Vector3.Dot(Forward, n);
                    if (into > 0.7f) _speed *= 1f - Mathf.Clamp01(dt * 8f);
                }
            }
            if (!IsAlive) return;

            RecordPath();
            ComputeSpine();
            _hidden = _grounded && _grid.IsCoverAt(_headPos);
            ApplyHeadVisual(dt);
        }

        /// <summary>
        /// Обычное ползание: скорость тянется к скорости темпа, голова поворачивается к желаемому
        /// направлению не быстрее TurnRate, а на малой скорости — ещё медленнее: разворот идёт дугой.
        /// </summary>
        private void UpdateCrawl(float dt)
        {
            float target = 0f;
            if (_moveAmount > 1e-3f)
            {
                float desiredYaw = Mathf.Atan2(_moveDir.x, _moveDir.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(_yaw, desiredYaw);
                float walk = _config.WalkSpeed;
                float turnRate = _config.TurnRate * Mathf.Clamp01(0.2f + _speed / (walk * 0.6f));
                _yaw = Mathf.MoveTowardsAngle(_yaw, desiredYaw, turnRate * dt);

                target = _config.PaceSpeed(EffectivePace) * _moveAmount * _speedScale * ExternalSpeedMultiplier;
                if (IsExposed) target *= _config.MissSlowdown;
                // Крутой разворот — сбавляем ход, иначе дуга вышла бы слишком широкой.
                float turnAbs = Mathf.Abs(delta);
                if (turnAbs > 90f) target *= Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(90f, 170f, turnAbs));
            }

            float rate = target > _speed ? _config.Acceleration : _config.Deceleration;
            _speed = Mathf.MoveTowards(_speed, target, rate * dt);
        }

        /// <summary>Выдохшаяся змея быстро ползти не может — переходит на шаг.</summary>
        private SnakePace EffectivePace => _pace == SnakePace.Sprint && _stamina.IsExhausted ? SnakePace.Walk : _pace;

        /// <summary>Быстрое ползание тратит выносливость, покой её возвращает; неподвижность на земле — засада.</summary>
        private void UpdateStaminaAndAmbush(float dt)
        {
            bool sprinting = !IsStriking && EffectivePace == SnakePace.Sprint && _moveAmount > 0.1f
                             && _speed > _config.WalkSpeed * 0.9f;
            if (sprinting) _stamina.Drain(_config.SprintStaminaDrain * dt);
            _stamina.Tick(dt, !IsMoving);
            _ambush.Tick(dt, !IsMoving && _grounded && !IsStriking);
        }

        /// <summary>Замах: змея быстро гасит ход, приподнимает голову и поворачивает её к прицелу в пределах угла.</summary>
        private void UpdateCoil(float dt)
        {
            _coilTime += dt;
            _speed = Mathf.MoveTowards(_speed, 0f, _config.Deceleration * 2f * dt);
            float aimYaw = Mathf.Atan2(_aimDir.x, _aimDir.z) * Mathf.Rad2Deg;
            float limit = _config.CoilAimLimit;
            float wanted = _coilBaseYaw + Mathf.Clamp(Mathf.DeltaAngle(_coilBaseYaw, aimYaw), -limit, limit);
            _yaw = Mathf.MoveTowardsAngle(_yaw, wanted, 420f * dt);
        }

        private void UpdateStrike(float dt)
        {
            _speed = _strikeSpeed;
            _strikeTimer -= dt;
            if (_strikeTimer > 0f) return;
            // После броска змея не стоит как вкопанная, а дотягивается по инерции.
            _speed = _config.WalkSpeed * 0.5f;
            EndStrike();
        }

        private void UpdateJump(float dt)
        {
            bool wantJump = _jumpRequested;
            _jumpRequested = false;

            if (_grounded && wantJump && !_coiled)
            {
                _verticalSpeed = Mathf.Sqrt(2f * _config.Gravity * _config.JumpHeight) * _cellSize;
                _grounded = false;
                _ambush.Break();
                Jumped?.Invoke();
            }

            if (_grounded) return;

            _verticalSpeed -= _config.Gravity * _cellSize * dt;
            _height += _verticalSpeed * dt;
            if (_height <= 0f)
            {
                _height = 0f;
                _verticalSpeed = 0f;
                _grounded = true;
                _squashTimer = 0f;
                Landed?.Invoke(new Vector3(_headPos.x, _grid.Center.y, _headPos.z));
            }
        }

        /// <summary>Не выпускаем голову за арену; возвращает true и нормаль стены, если упёрлась.</summary>
        private bool ClampToArena(out Vector3 normal)
        {
            normal = Vector3.zero;
            Vector2 half = _grid.ArenaHalfExtents;
            float margin = _wallMargin * _cellSize;
            float hx = half.x - margin;
            float hz = half.y - margin;
            Vector3 c = _grid.Center;
            float lx = _headPos.x - c.x;
            float lz = _headPos.z - c.z;
            bool hit = lx > hx || lx < -hx || lz > hz || lz < -hz;
            if (hit)
            {
                if (lx > hx) normal.x -= 1f;
                else if (lx < -hx) normal.x += 1f;
                if (lz > hz) normal.z -= 1f;
                else if (lz < -hz) normal.z += 1f;
                normal.Normalize();
                _headPos.x = c.x + Mathf.Clamp(lx, -hx, hx);
                _headPos.z = c.z + Mathf.Clamp(lz, -hz, hz);
            }
            return hit;
        }

        /// <summary>Добавляем точки строго через _sampleDist, чтобы точка тела находилась по индексу за O(1).</summary>
        private void RecordPath()
        {
            Vector3 newest = _path[_pathHead];
            Vector3 delta = _headPos - newest;
            float dist = delta.magnitude;
            while (dist >= _sampleDist)
            {
                newest += delta * (_sampleDist / dist);
                PushPathPoint(newest);
                delta = _headPos - newest;
                dist = delta.magnitude;
            }
        }

        private void PushPathPoint(Vector3 point)
        {
            _pathHead = (_pathHead + 1) % _path.Length;
            _path[_pathHead] = point;
            if (_pathCount < _path.Length) _pathCount++;
        }

        /// <summary>k = 0 — самая свежая точка траектории.</summary>
        private Vector3 PathPoint(int k)
        {
            return _path[(_pathHead - k + _path.Length) % _path.Length];
        }

        private void ComputeSpine()
        {
            float spacing = _spineSpacing * _cellSize;
            _spineCount = Mathf.Min(_spine.Length, Mathf.FloorToInt(BodyLengthWorld / spacing) + 1);
            _spine[0] = _headPos;
            for (int i = 1; i < _spineCount; i++)
                SampleSpine(i * spacing, out _spine[i], out _);
        }

        /// <summary>Проверяем 3x3 клетки вокруг головы: круг головы против квадрата препятствия, с учётом высоты прыжка.</summary>
        private bool HitsObstacle(out Vector3 normal)
        {
            normal = Vector3.zero;
            Vector2Int center = _grid.WorldToCell(_headPos);
            float radius = HeadRadiusWorld;
            float radiusSqr = radius * radius;
            float forgiveness = _obstacleForgiveness * _cellSize;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (!_grid.IsInside(cell)) continue;
                    float obstacleHeight = _grid.GetObstacleHeight(cell);
                    if (obstacleHeight <= 0f) continue;
                    // _height — высота низа головы над полем: выше препятствия — пролетаем.
                    if (_height + forgiveness >= obstacleHeight) continue;

                    Vector3 c = _grid.CellToWorld(cell);
                    float half = _grid.GetObstacleHalfSize(cell);
                    float nx = _headPos.x - Mathf.Clamp(_headPos.x, c.x - half, c.x + half);
                    float nz = _headPos.z - Mathf.Clamp(_headPos.z, c.z - half, c.z + half);
                    float distSqr = nx * nx + nz * nz;
                    if (distSqr >= radiusSqr) continue;

                    // Выталкиваем голову из препятствия по нормали (центр внутри — от центра клетки).
                    if (distSqr < 1e-6f)
                    {
                        nx = _headPos.x - c.x;
                        nz = _headPos.z - c.z;
                        if (nx * nx + nz * nz < 1e-6f) nx = -Forward.x;
                    }
                    normal = new Vector3(nx, 0f, nz).normalized;
                    _headPos += normal * (radius - Mathf.Sqrt(distSqr));
                    return true;
                }
            }
            return false;
        }

        private void Die()
        {
            IsAlive = false;
            _strikeTimer = 0f;
            _coiled = false;
            ApplyHeadVisual(0f);
            Died?.Invoke(_headPos);
        }

        private void ApplyHeadVisual(float dt)
        {
            float lift = _coilBlend * _coilLift * _cellSize;
            _head.position = _headPos + Vector3.up * lift;
            float pitch = Mathf.Clamp(-_verticalSpeed / _cellSize * _pitchPerSpeed, -35f, 35f) - _coilBlend * _coilPitch;
            float roll = -_turn01 * _bankAngle;
            _head.rotation = Quaternion.Euler(pitch, _yaw, roll);

            if (_squashTimer < _squashDuration)
            {
                _squashTimer += dt;
                float k = Mathf.Clamp01(_squashTimer / _squashDuration);
                float s = Mathf.Sin(k * Mathf.PI) * _squashAmount;
                _head.localScale = Vector3.Scale(_headBaseScale, new Vector3(1f + s, 1f - s, 1f + s));
            }
            else
            {
                _head.localScale = _headBaseScale;
            }
        }

        private void MarkCell(Vector3 world)
        {
            Vector2Int cell = _grid.WorldToCell(world);
            if (_grid.IsInside(cell)) _grid.SetOccupied(cell, true);
        }
    }
}
