using System;
using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Лиса — наземный хищник, чаще приходит ночью. Появляется у края поля, рыщет по следу змеи
    /// (запоминает, где змея проползала, и идёт по этим точкам с отставанием), а заметив её — бросается
    /// в погоню и кусает. Трава и засада прячут змею, в пруд лиса не заходит, в нору не пролезает.
    /// Бросок змеи в морду отпугивает лису, если змея ещё полна сил (решает GameManager через TryScare).
    /// Пока змея охраняет кладку (Raiding), лиса приходит чаще и идёт прямо к гнезду таскать яйца.
    /// В полом бревне змеи лиса не видит и не достаёт.
    /// Логика в данных, Transform только показывает результат.
    /// </summary>
    public class FoxHunter : MonoBehaviour
    {
        public enum Phase
        {
            Away,
            Prowl,
            Chase,
            Retreat
        }

        private const int TRAIL_SIZE = 24;
        private static readonly float[] AvoidAngles = { 35f, -35f, 70f, -70f, 110f, -110f, 150f, -150f };

        [Header("Ссылки")]
        [Tooltip("Параметры баланса (урон, интервалы, скорость)")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Змея, за которой охотится лиса")]
        [SerializeField] private SnakeController _target;

        [Tooltip("Модель лисы (корень)")]
        [SerializeField] private Transform _model;

        [Tooltip("Тело — его покачиваем на бегу")]
        [SerializeField] private Transform _body;

        [Tooltip("Хвост — машет")]
        [SerializeField] private Transform _tail;

        [Tooltip("Лапы: передние и задние шагают в противофазе")]
        [SerializeField] private Transform[] _legs = new Transform[0];

        [Header("Поведение")]
        [Tooltip("С какого расстояния лиса замечает змею на открытом месте, клеток")]
        [SerializeField, Min(1f)] private float _sightRadius = 7f;

        [Tooltip("Скорость, когда рыщет по следу, клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _prowlSpeed = 2.4f;

        [Tooltip("Дистанция укуса, клеток")]
        [SerializeField, Min(0.3f)] private float _biteRange = 0.95f;

        [Tooltip("Пауза между укусами, сек")]
        [SerializeField, Min(0.2f)] private float _biteCooldown = 2.4f;

        [Tooltip("Сколько секунд лиса гонится, потеряв змею из виду, прежде чем вернуться к следу")]
        [SerializeField, Min(0.5f)] private float _loseSightTime = 3f;

        [Tooltip("Сколько секунд лиса бродит по полю, прежде чем уйти")]
        [SerializeField, Min(5f)] private float _stayTime = 45f;

        [Tooltip("Как часто запоминать, где проползла змея, сек")]
        [SerializeField, Min(0.1f)] private float _trailInterval = 0.6f;

        [Tooltip("Скорость поворота, град/с")]
        [SerializeField, Min(30f)] private float _turnSpeed = 360f;

        [Tooltip("С какого расстояния до гнезда лиса достаёт яйца, клеток")]
        [SerializeField, Min(0.3f)] private float _raidRange = 1.4f;

        /// <summary>Лиса пришла на поле.</summary>
        public event Action Appeared;

        /// <summary>Лиса укусила змею (точка укуса).</summary>
        public event Action<Vector3> Bit;

        /// <summary>Змея отпугнула лису броском.</summary>
        public event Action Scared;

        /// <summary>Лиса утащила яйцо из кладки змеи.</summary>
        public event Action Raided;

        /// <summary>Лиса идёт за яйцами к RaidPoint (змея охраняет кладку).</summary>
        public bool Raiding { get; set; }

        /// <summary>Где кладка змеи.</summary>
        public Vector3 RaidPoint { get; set; }

        /// <summary>Во сколько раз чаще приходит лиса.</summary>
        public float Eagerness { get; set; } = 1f;

        /// <summary>Можно приходить на охоту (идёт партия).</summary>
        public bool HuntingAllowed { get; set; }

        /// <summary>Ночью лиса приходит чаще.</summary>
        public bool IsNight { get; set; }

        public Phase CurrentPhase => _phase;
        public bool IsActive => _phase != Phase.Away;
        public bool IsChasing => _phase == Phase.Chase;
        public Vector3 Position => _pos;

        private readonly Vector3[] _trail = new Vector3[TRAIL_SIZE];
        private int _trailHead;
        private int _trailCount;
        private int _trailCursor;
        private float _trailTimer;

        private Phase _phase;
        private float _timer;
        private float _stayLeft;
        private float _biteTimer;
        private float _lostTimer;
        private Vector3 _pos;
        private Vector3 _exit;
        private float _yaw;
        private float _speed;
        private float _animTime;
        private float _raidTimer;

        private void Awake()
        {
            SetVisible(false);
        }

        /// <summary>Новая партия: лиса ушла, первый визит через delay секунд.</summary>
        public void ResetHunter(float delay)
        {
            _phase = Phase.Away;
            _timer = delay;
            _trailCount = 0;
            _trailCursor = 0;
            SetVisible(false);
        }

        /// <summary>
        /// Бросок змеи пришёлся в лису. canScare — у змеи достаточно здоровья, чтобы лиса испугалась.
        /// Возвращает true, если лиса бросилась наутёк.
        /// </summary>
        public bool TryScare(Vector3 strikePosition, bool canScare)
        {
            if (!IsActive || _phase == Phase.Retreat) return false;
            Vector3 d = _pos - strikePosition;
            d.y = 0f;
            float r = 1.3f * _grid.CellSize;
            if (d.sqrMagnitude > r * r) return false;
            if (!canScare)
            {
                // Слабая змея лису не пугает — та лишь отскакивает и злится.
                _biteTimer = Mathf.Min(_biteTimer, 0.6f);
                return false;
            }
            BeginRetreat();
            Scared?.Invoke();
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _animTime += dt;
            RecordTrail(dt);

            switch (_phase)
            {
                case Phase.Away:
                    TickAway(dt);
                    break;
                case Phase.Prowl:
                    TickProwl(dt);
                    break;
                case Phase.Chase:
                    TickChase(dt);
                    break;
                case Phase.Retreat:
                    TickRetreat(dt);
                    break;
            }
            if (_phase != Phase.Away) ApplyVisual();
        }

        // ---------------- Фазы ----------------

        private void TickAway(float dt)
        {
            if (!HuntingAllowed || (!TargetPresent() && !Raiding)) return;
            _timer -= dt * (IsNight ? 1.6f : 1f) * Mathf.Max(0.1f, Eagerness);
            if (_timer > 0f) return;

            // Приходит издалека — из свободной точки подальше от змеи; за яйцами — с той же стороны воды, что и гнездо.
            _pos = Raiding ? RaidSpawn() : FarCorner(_target.HeadPosition);
            _exit = _pos;
            _yaw = Mathf.Atan2(_grid.Center.x - _pos.x, _grid.Center.z - _pos.z) * Mathf.Rad2Deg;
            _stayLeft = _stayTime;
            _biteTimer = 0f;
            _raidTimer = _config.FoxRaidInterval;
            _trailCursor = Mathf.Max(0, _trailCount - TRAIL_SIZE);
            _phase = Phase.Prowl;
            SetVisible(true);
            Appeared?.Invoke();
        }

        /// <summary>Идёт по следу: от старых точек к свежим; увидела змею — погоня.</summary>
        private void TickProwl(float dt)
        {
            // У кладки лиса не уходит сама — только пока не отгонят или не кончатся яйца.
            if (!Raiding) _stayLeft -= dt;
            if (_stayLeft <= 0f || !HuntingAllowed)
            {
                BeginRetreat();
                return;
            }
            if (CanSeeTarget())
            {
                _phase = Phase.Chase;
                _lostTimer = 0f;
                return;
            }

            if (Raiding)
            {
                TickRaid(dt);
                return;
            }
            Vector3 goal = NextTrailPoint();
            MoveTowards(goal, _prowlSpeed, dt);
        }

        /// <summary>К гнезду и таскать яйца по одному.</summary>
        private void TickRaid(float dt)
        {
            Vector3 d = RaidPoint - _pos;
            d.y = 0f;
            if (d.magnitude > _raidRange * _grid.CellSize)
            {
                MoveTowards(RaidPoint, _prowlSpeed * 1.3f, dt);
                return;
            }
            _speed = 0f;
            _raidTimer -= dt;
            if (_raidTimer > 0f) return;
            _raidTimer = _config.FoxRaidInterval;
            Raided?.Invoke();
        }

        private void TickChase(float dt)
        {
            _stayLeft -= dt;
            if (_biteTimer > 0f) _biteTimer -= dt;
            if (!TargetPresent() || !HuntingAllowed)
            {
                _phase = Phase.Prowl;
                return;
            }

            if (CanSeeTarget()) _lostTimer = 0f;
            else
            {
                _lostTimer += dt;
                if (_lostTimer >= _loseSightTime)
                {
                    _phase = Phase.Prowl;
                    return;
                }
            }

            Vector3 head = _target.HeadPosition;
            float cs = _grid.CellSize;
            Vector3 d = head - _pos;
            d.y = 0f;
            // Только что укусила — на миг отскакивает, а не висит на змее.
            float speed = _biteTimer > _biteCooldown - 0.6f ? -_config.FoxSpeed * 0.5f : _config.FoxSpeed;
            if (speed > 0f) MoveTowards(head, speed, dt);
            else MoveTowards(_pos - d.normalized, -speed, dt);

            bool reachable = !_grid.IsWaterAt(head) && !_grid.IsHollowAt(head);
            if (reachable && _biteTimer <= 0f && d.magnitude <= _biteRange * cs && _target.HeightAboveGround < 0.6f * cs)
            {
                _biteTimer = _biteCooldown;
                Bit?.Invoke(head);
            }
        }

        private void TickRetreat(float dt)
        {
            MoveTowards(_exit, _config.FoxSpeed * 1.1f, dt, true);
            Vector3 d = _exit - _pos;
            d.y = 0f;
            if (d.magnitude > 0.6f * _grid.CellSize) return;
            _phase = Phase.Away;
            _timer = UnityEngine.Random.Range(_config.FoxIntervalMin, _config.FoxIntervalMax);
            SetVisible(false);
        }

        private void BeginRetreat()
        {
            _phase = Phase.Retreat;
            _exit = FarCorner(_target != null ? _target.HeadPosition : _grid.Center);
        }

        // ---------------- Чутьё ----------------

        private bool TargetPresent()
        {
            return _target != null && _target.IsAlive && _target.IsRunning && !_target.InBurrow;
        }

        /// <summary>Видит ли змею: трава и засада прячут, в воде змея видна, но недосягаема.</summary>
        private bool CanSeeTarget()
        {
            if (!TargetPresent() || _grid.IsHollowAt(_target.HeadPosition)) return false;
            float r = _sightRadius * _grid.CellSize;
            if (_target.IsHidden) r *= 0.45f;
            r *= _target.Ambush.AwarenessMultiplier(_target.IsHidden);
            Vector3 d = _target.HeadPosition - _pos;
            d.y = 0f;
            return d.sqrMagnitude <= r * r;
        }

        /// <summary>Каждые _trailInterval секунд запоминаем, где голова змеи: по этим точкам лиса и идёт.</summary>
        private void RecordTrail(float dt)
        {
            if (!TargetPresent()) return;
            _trailTimer -= dt;
            if (_trailTimer > 0f) return;
            _trailTimer = _trailInterval;
            _trailHead = (_trailHead + 1) % TRAIL_SIZE;
            _trail[_trailHead] = _target.HeadPosition;
            _trailCount++;
        }

        /// <summary>Следующая точка следа после той, где лиса уже была; если след кончился — к змее.</summary>
        private Vector3 NextTrailPoint()
        {
            int oldest = Mathf.Max(0, _trailCount - TRAIL_SIZE);
            if (_trailCursor < oldest) _trailCursor = oldest;
            while (_trailCursor < _trailCount)
            {
                Vector3 p = _trail[(_trailHead - (_trailCount - 1 - _trailCursor) + TRAIL_SIZE * 4) % TRAIL_SIZE];
                Vector3 d = p - _pos;
                d.y = 0f;
                if (d.magnitude > 0.8f * _grid.CellSize && !_grid.IsWaterAt(p)) return p;
                _trailCursor++;
            }
            return TargetPresent() ? _target.HeadPosition : _grid.Center;
        }

        /// <summary>Свободная точка подальше от змеи (несколько случайных проб, берём самую дальнюю).</summary>
        private Vector3 FarCorner(Vector3 from)
        {
            Vector3 best = _grid.Center;
            float bestDist = -1f;
            for (int i = 0; i < 12; i++)
            {
                if (!_grid.TryGetRandomFreeCell(out Vector2Int cell)) break;
                Vector3 p = _grid.CellToWorld(cell);
                Vector3 d = p - from;
                d.y = 0f;
                float dist = d.sqrMagnitude;
                if (dist <= bestDist) continue;
                bestDist = dist;
                best = p;
            }
            return best;
        }

        /// <summary>
        /// Точка появления для набега: в 14–22 клетках от гнезда, откуда до него можно дойти по прямой посуху
        /// (лиса не плавает и реку обходить не умеет). Не нашлась — как обычно, подальше от змеи.
        /// </summary>
        private Vector3 RaidSpawn()
        {
            float cs = _grid.CellSize;
            for (int i = 0; i < 40; i++)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float r = UnityEngine.Random.Range(14f, 22f) * cs;
                Vector3 p = RaidPoint + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (IsBlocked(p, false) || !DryLine(p, RaidPoint)) continue;
                p.y = _grid.GroundHeight(p);
                return p;
            }
            return FarCorner(_target != null ? _target.HeadPosition : _grid.Center);
        }

        private bool DryLine(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            float step = 0.5f * _grid.CellSize;
            int steps = Mathf.CeilToInt(d.magnitude / step);
            for (int k = 1; k < steps; k++)
                if (_grid.IsWaterAt(from + d * ((float)k / steps))) return false;
            return true;
        }

        // ---------------- Движение и вид ----------------

        /// <summary>Шаг к цели с обходом камней, пней и воды.</summary>
        private void MoveTowards(Vector3 goal, float speedCells, float dt, bool ignoreWater = false)
        {
            Vector3 dir = goal - _pos;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f)
            {
                _speed = 0f;
                return;
            }
            dir.Normalize();
            float cs = _grid.CellSize;
            if (IsBlocked(_pos + dir * (0.6f * cs), ignoreWater))
            {
                bool found = false;
                for (int i = 0; i < AvoidAngles.Length && !found; i++)
                {
                    Vector3 alt = Quaternion.Euler(0f, AvoidAngles[i], 0f) * dir;
                    if (IsBlocked(_pos + alt * (0.7f * cs), ignoreWater)) continue;
                    dir = alt;
                    found = true;
                }
                if (!found)
                {
                    _speed = 0f;
                    return;
                }
            }

            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, _turnSpeed * dt);
            _speed = speedCells;
            _pos += dir * (speedCells * cs * dt);
            _pos.y = _grid.GroundHeight(_pos);
        }

        private bool IsBlocked(Vector3 point, bool ignoreWater)
        {
            Vector2 half = _grid.ArenaHalfExtents;
            Vector3 local = point - _grid.Center;
            float margin = 0.6f * _grid.CellSize;
            if (Mathf.Abs(local.x) > half.x - margin || Mathf.Abs(local.z) > half.y - margin) return true;
            Vector2Int cell = _grid.WorldToCell(point);
            if (!_grid.IsInside(cell)) return true;
            if (!ignoreWater && _grid.IsWater(cell)) return true;
            return _grid.GetObstacleHeight(cell) > 0f;
        }

        private void ApplyVisual()
        {
            _model.SetPositionAndRotation(_pos, Quaternion.Euler(0f, _yaw, 0f));
            float run = Mathf.Clamp01(_speed / Mathf.Max(0.1f, _config.FoxSpeed));
            float freq = 8f + 8f * run;
            float phase = _animTime * freq;
            if (_body != null) _body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * 0.06f * run, 0f);
            if (_tail != null) _tail.localRotation = Quaternion.Euler(-25f + Mathf.Sin(phase * 0.5f) * 8f, Mathf.Sin(_animTime * 3f) * 18f, 0f);
            for (int i = 0; i < _legs.Length; i++)
            {
                float s = (i % 2 == 0 ? 1f : -1f) * (i < 2 ? 1f : -1f);
                _legs[i].localRotation = Quaternion.Euler(Mathf.Sin(phase) * 35f * run * s, 0f, 0f);
            }
        }

        private void SetVisible(bool visible)
        {
            if (_model != null && _model.gameObject.activeSelf != visible) _model.gameObject.SetActive(visible);
        }
    }
}
