using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Логика змейки: непрерывное движение, руль, прыжок, бросок, рост и столкновения.
    /// Голова пишет траекторию в кольцевой буфер точек с равным шагом; тело (меш и точки
    /// позвоночника для столкновений) строится по этой траектории, поэтому повторяет
    /// и повороты, и прыжки. Удары не убивают сразу: змея отскакивает и сообщает об уроне
    /// (событие Hurt), а жива ли она — решает GameManager. Управляется игроком или ИИ (AiSteer/AiJump).
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
        [Tooltip("Управляется игроком; иначе — ИИ через AiSteer и AiJump")]
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
        [Tooltip("Участок тела за головой, который не проверяется на столкновение с собой (клеток вдоль тела)")]
        [SerializeField, Min(0.5f)] private float _selfCollisionSkipDistance = 2.2f;

        [Tooltip("Расстояние голова-позвоночник, при котором засчитывается удар о тело (доля клетки)")]
        [SerializeField, Min(0.1f)] private float _selfCollisionRadius = 0.55f;

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
        [SerializeField, Range(0f, 45f)] private float _bankAngle = 18f;

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
        public event Action Dashed;
        public event Action Respawned;

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                // Запоминаем кадр старта: Space, которым начали игру, не должен сразу стать прыжком.
                if (value && !_isRunning) _runStartFrame = Time.frameCount;
                _isRunning = value;
            }
        }

        public bool IsAlive { get; private set; }
        public bool IsPlayer => _isPlayer;
        public bool IsDashing => _dashTimer > 0f;
        public bool IsInvulnerable => _invulnerableTimer > 0f;

        /// <summary>Змея лежит в высокой траве: добыча и орёл её почти не видят. В прыжке укрытия нет.</summary>
        public bool IsHidden => _hidden;

        /// <summary>Тело уже минимальной длины — худеть от голода некуда.</summary>
        public bool IsAtMinLength => _targetLengthCells <= _config.StartLength + 0.01f;

        /// <summary>Готовность рывка: 1 — можно использовать.</summary>
        public float DashCharge01 => _dashCooldownTotal > 0f ? 1f - Mathf.Clamp01(_dashCooldownTimer / _dashCooldownTotal) : 1f;

        /// <summary>Положение руля от -1 (влево) до 1 (вправо).</summary>
        public float Steer => _steer;

        public Transform Head => _head;
        public Vector3 HeadPosition => _headPos;
        public Vector3 Forward => Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
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

        /// <summary>Множители длительности и перезарядки броска от улучшений.</summary>
        public float DashDurationScale { get; set; } = 1f;
        public float DashCooldownScale { get; set; } = 1f;

        /// <summary>Чувствительность руля из настроек.</summary>
        public float SteerSensitivity { get; set; } = 1f;

        /// <summary>Руль от ИИ (-1..1), используется если змейка не игрока.</summary>
        public float AiSteer { get; set; }

        /// <summary>Запрос прыжка от ИИ, сбрасывается после обработки.</summary>
        public bool AiJump { get; set; }

        /// <summary>Запрос рывка от ИИ, сбрасывается после обработки.</summary>
        public bool AiDash { get; set; }

        /// <summary>Автопилот: змея игрока управляется ИИ (живой фон главного меню).</summary>
        public bool AutoPilot { get; set; }

        private bool UsesPlayerInput => _isPlayer && !AutoPilot;

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
        private float _steer;
        private float _speed;
        private float _lengthCells;
        private float _targetLengthCells;

        private float _height;
        private float _verticalSpeed;
        private bool _grounded;

        private float _dashTimer;
        private float _dashCooldownTimer;
        private float _dashCooldownTotal;
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
            _steer = 0f;
            _speed = _config.MoveSpeed;
            _height = 0f;
            _verticalSpeed = 0f;
            _grounded = true;
            _dashTimer = 0f;
            _dashCooldownTimer = 0f;
            _dashCooldownTotal = _config.DashCooldown;
            _invulnerableTimer = 0f;
            _hidden = false;
            _squashTimer = _squashDuration;
            AiSteer = 0f;
            AiJump = false;
            AiDash = false;
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
        /// Удар извне (орёл, соперница) или о тело. awayDirection — куда отскочить (нулевой — не отскакивать).
        /// После удара змея ненадолго неуязвима. Возвращает true, если удар засчитан.
        /// </summary>
        public bool TakeHit(float damage, DamageSource source, Vector3 awayDirection)
        {
            if (!IsAlive || _invulnerableTimer > 0f) return false;
            _invulnerableTimer = _invulnerabilityTime;
            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude > 1e-6f) Deflect(awayDirection.normalized);
            _squashTimer = 0f;
            Hurt?.Invoke(_headPos, damage, source);
            return true;
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

        private void Update()
        {
            if (!IsRunning || !IsAlive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateDash(dt);
            UpdateSteer(dt);
            UpdateJump(dt);
            if (_invulnerableTimer > 0f) _invulnerableTimer -= dt;

            float speed = _speed * _speedScale * ExternalSpeedMultiplier * (IsDashing ? _config.DashMultiplier : 1f);
            _headPos += Forward * (speed * _cellSize * dt);
            _headPos.y = _groundY + _height;
            _lengthCells = Mathf.MoveTowards(_lengthCells, _targetLengthCells, _lengthChangeSpeed * dt);

            bool hitWall = ClampToArena(out Vector3 wallNormal);
            bool hitObstacle = !hitWall && HitsObstacle(out wallNormal);
            RecordPath();
            ComputeSpine();

            // Стены и камни непроницаемы: от них отскакиваем всегда, а урон — только вне неуязвимости.
            if (hitWall || hitObstacle)
            {
                Deflect(wallNormal);
                TakeHit(_config.WallDamage, DamageSource.Collision, Vector3.zero);
            }
            else if (HitsSelf(out Vector3 selfNormal))
            {
                TakeHit(_config.BodyDamage, DamageSource.Collision, selfNormal);
            }
            if (!IsAlive) return;

            _hidden = _grounded && _grid.IsCoverAt(_headPos);
            ApplyHeadVisual(dt);
        }

        private void UpdateSteer(float dt)
        {
            float target;
            if (UsesPlayerInput)
            {
                target = 0f;
                if (GameInput.LeftHeld) target -= 1f;
                if (GameInput.RightHeld) target += 1f;
            }
            else
            {
                target = Mathf.Clamp(AiSteer, -1f, 1f);
            }

            // Руль не прыгает сразу в упор, а доворачивается — так поворот получается дугой.
            _steer = Mathf.MoveTowards(_steer, target, _config.SteerResponse * dt);
            _yaw += _steer * _config.TurnSpeed * SteerSensitivity * dt;
        }

        private void UpdateJump(float dt)
        {
            bool wantJump = UsesPlayerInput ? GameInput.Jump && Time.frameCount != _runStartFrame : AiJump;
            AiJump = false;

            if (_grounded && wantJump)
            {
                _verticalSpeed = Mathf.Sqrt(2f * _config.Gravity * _config.JumpHeight) * _cellSize;
                _grounded = false;
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

        private void UpdateDash(float dt)
        {
            if (_dashTimer > 0f) _dashTimer -= dt;
            if (_dashCooldownTimer > 0f) _dashCooldownTimer -= dt;

            bool wantDash = UsesPlayerInput ? GameInput.Dash : AiDash;
            AiDash = false;
            if (wantDash && _dashCooldownTimer <= 0f)
            {
                _dashTimer = _config.DashDuration * DashDurationScale;
                _dashCooldownTotal = _config.DashCooldown * DashCooldownScale;
                _dashCooldownTimer = _dashCooldownTotal;
                Dashed?.Invoke();
            }
        }

        /// <summary>
        /// Отскок от поверхности с нормалью normal: змея скользит вдоль неё и немного отходит,
        /// а не разворачивается на 180° (иначе врезалась бы в собственную шею).
        /// </summary>
        private void Deflect(Vector3 normal)
        {
            Vector3 f = Forward;
            float into = Vector3.Dot(f, normal);
            if (into >= 0f) return;
            Vector3 tangent = f - normal * into;
            if (tangent.sqrMagnitude < 0.04f) tangent = Vector3.Cross(Vector3.up, normal) * (_steer >= 0f ? 1f : -1f);
            Vector3 dir = tangent.normalized * 0.8f + normal * 0.6f;
            _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            _steer *= 0.3f;
        }

        /// <summary>Не выпускаем голову за арену; возвращает true и нормаль стены, если был удар.</summary>
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

        /// <summary>Точка тела мешает, только если она близко по горизонтали и примерно на той же высоте — так прыжок спасает.</summary>
        private bool HitsSelf(out Vector3 normal)
        {
            normal = Vector3.zero;
            float radius = _selfCollisionRadius * _cellSize;
            float radiusSqr = radius * radius;
            float clearance = _jumpClearance * _cellSize;
            int skip = Mathf.Max(1, Mathf.CeilToInt(_selfCollisionSkipDistance / _spineSpacing));
            for (int i = skip; i < _spineCount; i++)
            {
                Vector3 d = _spine[i] - _headPos;
                if (Mathf.Abs(d.y) >= clearance) continue;
                d.y = 0f;
                if (d.sqrMagnitude < radiusSqr)
                {
                    normal = -d;
                    return true;
                }
            }
            return false;
        }

        private void Die()
        {
            IsAlive = false;
            ApplyHeadVisual(0f);
            Died?.Invoke(_headPos);
        }

        private void ApplyHeadVisual(float dt)
        {
            _head.position = _headPos;
            float pitch = Mathf.Clamp(-_verticalSpeed / _cellSize * _pitchPerSpeed, -35f, 35f);
            float roll = -_steer * _bankAngle;
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
