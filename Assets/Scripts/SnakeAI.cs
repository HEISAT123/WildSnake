using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// ИИ змеи (соперницы или автопилота меню) в три слоя:
    /// 1) план — A* по сетке поля несколько раз в секунду; стоимость клетки учитывает препятствия,
    ///    тела обеих змей и зону перед головой другой змеи;
    /// 2) рефлексы — «щупальца» впереди: уворот от стен и камней, прыжок через брёвна и тела;
    /// 3) охота — выбирает самую выгодную добычу (сытность / расстояние), подкрадывается к ней,
    ///    целится с упреждением и делает бросок вблизи; иногда выходит наперерез игроку.
    /// 4) страховка от застревания — если змея почти не сдвинулась или долго не приближается к цели,
    ///    она отползает в самую свободную сторону, а недостижимую добычу на время бросает.
    /// Управляет змеёй тем же «намерением», что и игрок: желаемое направление + темп, бросок, прыжок.
    /// Все буферы выделяются один раз в Start, в игровом цикле аллокаций нет.
    /// Выполняется раньше SnakeController, чтобы намерение применилось в том же кадре.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SnakeAI : MonoBehaviour
    {
        private const int BLOCKED = -1;
        private const int STRAIGHT = 10;
        private const int DIAGONAL = 14;

        private static readonly int[] DirX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DirY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        [Header("Ссылки")]
        [Tooltip("Змейка, которой управляет ИИ")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Змейка игрока")]
        [SerializeField] private SnakeController _player;

        [Tooltip("Живность — основная цель")]
        [SerializeField] private WildlifeManager _wildlife;

        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Header("Планирование пути")]
        [Tooltip("Как часто перестраивать путь, сек")]
        [SerializeField, Min(0.05f)] private float _replanInterval = 0.25f;

        [Tooltip("На сколько клеток пути вперёд смотреть при рулении")]
        [SerializeField, Range(1, 6)] private int _waypointLookahead = 2;

        [Tooltip("Предел раскрытых клеток A* за одно планирование")]
        [SerializeField, Min(100)] private int _maxExpansions = 40000;

        [Header("Стоимость клеток")]
        [Tooltip("Рядом с колонной, блоком или стеной")]
        [SerializeField, Min(0)] private int _nearObstacleCost = 4;

        [Tooltip("Низкая стенка (придётся прыгать)")]
        [SerializeField, Min(0)] private int _lowWallCost = 3;

        [Tooltip("Клетка с телом змеи")]
        [SerializeField, Min(0)] private int _bodyCost = 40;

        [Tooltip("Рядом с телом змеи")]
        [SerializeField, Min(0)] private int _nearBodyCost = 6;

        [Tooltip("Зона перед головой игрока (риск лобового)")]
        [SerializeField, Min(0)] private int _headOnCost = 25;

        [Header("Рефлексы")]
        [Tooltip("Дальность ближнего щупальца, клеток")]
        [SerializeField, Min(0.3f)] private float _nearLook = 1.2f;

        [Tooltip("Дальность дальних щупалец, клеток")]
        [SerializeField, Min(0.5f)] private float _lookAhead = 2.6f;

        [Tooltip("Угол боковых щупалец, град")]
        [SerializeField, Range(10f, 80f)] private float _feelerAngle = 35f;

        [Tooltip("Угол до цели, при котором руль в упоре, град")]
        [SerializeField, Range(5f, 90f)] private float _fullSteerAngle = 35f;

        [Tooltip("Насколько отклоняется желаемое направление от курса при руле в упоре, град")]
        [SerializeField, Range(10f, 120f)] private float _steerToDirection = 80f;

        [Tooltip("Отступ от стен арены, клеток")]
        [SerializeField, Min(0f)] private float _wallAvoidMargin = 0.8f;

        [Tooltip("Умеет ли прыгать через низкие стенки (автопилот меню не прыгает — он должен ездить спокойно)")]
        [SerializeField] private bool _canJump = true;

        [Tooltip("Где приземлится прыжок, клеток впереди: прыгаем, только если там свободно")]
        [SerializeField, Min(1f)] private float _landingDistance = 2.7f;

        [Header("Характер")]
        [Tooltip("Склонность охотиться на игрока (0 — никогда, 1 — всегда, когда он рядом)")]
        [SerializeField, Range(0f, 1f)] private float _aggression = 0.55f;

        [Tooltip("Дистанция, на которой ИИ начинает охоту, клеток")]
        [SerializeField, Min(1f)] private float _huntRange = 10f;

        [Tooltip("Насколько далеко перед головой игрока целиться при перехвате, клеток")]
        [SerializeField, Min(0f)] private float _interceptLead = 3f;

        [Tooltip("Как часто ИИ пересматривает решение «охотиться или есть», сек")]
        [SerializeField, Min(0.5f)] private float _decisionInterval = 3f;

        [Tooltip("Максимальный угол до цели для броска, град")]
        [SerializeField, Range(1f, 45f)] private float _dashAngle = 12f;

        [Tooltip("С какого расстояния до добычи делать бросок, клеток")]
        [SerializeField, Min(0.5f)] private float _strikeDistance = 2f;

        [Tooltip("С какого расстояния до спокойной добычи начинать красться, клеток")]
        [SerializeField, Min(0f)] private float _stalkDistance = 6f;

        [Tooltip("Упреждение по скорости добычи, сек")]
        [SerializeField, Min(0f)] private float _preyLead = 0.35f;

        [Tooltip("Как часто выбирать новую добычу, сек")]
        [SerializeField, Min(0.1f)] private float _retargetInterval = 1f;

        [Header("Застревание")]
        [Tooltip("За это время змея должна сдвинуться хотя бы на _stuckDistance, иначе — застряла, сек")]
        [SerializeField, Min(0.3f)] private float _stuckTime = 1.2f;

        [Tooltip("Минимальный сдвиг за _stuckTime, клеток")]
        [SerializeField, Min(0.1f)] private float _stuckDistance = 0.8f;

        [Tooltip("Сколько можно не приближаться к цели, прежде чем сменить её, сек")]
        [SerializeField, Min(1f)] private float _noProgressTime = 7f;

        [Tooltip("Сколько длится отползание при застревании, сек")]
        [SerializeField, Min(0.2f)] private float _escapeTime = 1.1f;

        [Tooltip("Сколько не возвращаться к добыче, до которой не добраться, сек")]
        [SerializeField, Min(0f)] private float _ignorePreyTime = 10f;

        private enum Blocked
        {
            Free,
            Jumpable,
            Solid
        }

        private int _width;
        private int _height;
        private int[] _baseCost;
        private int[] _dynamicCost;
        private int[] _g;
        private int[] _f;
        private int[] _parent;
        private int[] _seenStamp;
        private int[] _closedStamp;
        private int[] _heap;
        private int _heapCount;
        private int _stamp;

        private int[] _path;
        private int _pathLength;
        private int _pathIndex;

        private float _replanTimer;
        private float _decisionTimer;
        private float _retargetTimer;
        private PreyAnimal _prey;
        private bool _hunting;
        private bool _initialized;
        private bool _wantJump;
        private bool _wantStrike;

        private Vector3 _stuckAnchor;
        private float _stuckTimer;
        private int _stuckStreak;
        private float _bestGoalDistance = float.MaxValue;
        private float _progressTimer;
        private Vector3 _escapeDir;
        private float _escapeTimer;
        private PreyAnimal _ignoredPrey;
        private float _ignoreTimer;
        private Vector3 _wanderGoal;
        private bool _hasWanderGoal;

        /// <summary>Другая змея на поле: выключенный объект (соперник отключён в настройках) не считается.</summary>
        private bool OtherAlive => _player != null && _player.isActiveAndEnabled && _player.IsAlive;

        private void Start()
        {
            _width = _grid.Width;
            _height = _grid.Height;
            int n = _width * _height;
            _baseCost = new int[n];
            _dynamicCost = new int[n];
            _g = new int[n];
            _f = new int[n];
            _parent = new int[n];
            _seenStamp = new int[n];
            _closedStamp = new int[n];
            _heap = new int[n * 8];
            _path = new int[n];
            BuildBaseCost();
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || !_snake.IsRunning || !_snake.IsAlive)
            {
                _pathLength = 0;
                _stuckAnchor = _snake.HeadPosition;
                _stuckTimer = 0f;
                _escapeTimer = 0f;
                return;
            }
            _wantJump = false;
            _wantStrike = false;

            float dt = Time.deltaTime;
            if (_ignoreTimer > 0f && (_ignoreTimer -= dt) <= 0f) _ignoredPrey = null;
            if (_escapeTimer > 0f)
            {
                // Отползаем, не думая о цели: иначе план тут же развернёт змею обратно в тупик.
                _escapeTimer -= dt;
                _snake.SetMoveIntent(_escapeDir, SnakePace.Walk);
                if (_escapeTimer <= 0f) _replanTimer = 0f;
                return;
            }
            if (CheckStuck(dt)) return;

            _decisionTimer -= dt;
            if (_decisionTimer <= 0f)
            {
                _decisionTimer = _decisionInterval;
                DecideMood();
            }

            _retargetTimer -= dt;
            if (_retargetTimer <= 0f || _prey == null || !_prey.IsActive || _prey.IsFlying)
            {
                _retargetTimer = _retargetInterval;
                PreyAnimal previous = _prey;
                _prey = _wildlife.FindBestTarget(_snake.HeadPosition, OtherAlive ? _player.HeadPosition : Vector3.zero, OtherAlive, _ignoredPrey);
                if (_prey != previous) ResetProgress();
            }

            _replanTimer -= dt;
            if (_replanTimer <= 0f)
            {
                _replanTimer = _replanInterval;
                Replan();
            }

            Steer();
        }

        // ---------------- Решения ----------------

        private void DecideMood()
        {
            _hunting = OtherAlive && DistanceCells(_player.HeadPosition) < _huntRange && Random.value < _aggression;
        }

        private Vector3 CurrentGoal()
        {
            if (_hunting && OtherAlive)
            {
                // Выходим наперерез: точка перед головой игрока, чтобы он врезался в наше тело.
                if (DistanceCells(_player.HeadPosition) > _huntRange * 1.5f) _hunting = false;
                else return _player.HeadPosition + _player.Forward * (_interceptLead * _grid.CellSize);
            }

            // Цель — место, где добыча окажется чуть позже (бегущую мышь ловят наперерез).
            if (HasPrey) return _prey.Position + _prey.Velocity * _preyLead;
            // Добычи нет — бродим по случайным свободным местам, а не кружим в одной точке.
            if (!_hasWanderGoal || DistanceCells(_wanderGoal) < 2f) PickWanderGoal();
            return _hasWanderGoal ? _wanderGoal : _grid.Center;
        }

        private void PickWanderGoal()
        {
            _hasWanderGoal = _grid.TryGetRandomFreeCell(out Vector2Int cell);
            if (_hasWanderGoal) _wanderGoal = _grid.CellToWorld(cell);
            ResetProgress();
        }

        // ---------------- Застревание ----------------

        private void ResetProgress()
        {
            _bestGoalDistance = float.MaxValue;
            _progressTimer = 0f;
        }

        /// <summary>
        /// Два признака беды: змея почти стоит на месте (упёрлась) или долго не приближается к цели (кружит).
        /// В обоих случаях отползаем в самую свободную сторону; если не помогает — бросаем цель.
        /// </summary>
        private bool CheckStuck(float dt)
        {
            Vector3 head = _snake.HeadPosition;
            bool busy = _snake.IsStriking || _snake.IsCoiled || !_snake.IsGrounded;

            _stuckTimer += dt;
            if (_stuckTimer >= _stuckTime)
            {
                Vector3 moved = head - _stuckAnchor;
                moved.y = 0f;
                bool stuck = !busy && moved.magnitude < _stuckDistance * _grid.CellSize;
                _stuckAnchor = head;
                _stuckTimer = 0f;
                if (!stuck) _stuckStreak = 0;
                else
                {
                    // Второй раз подряд упёрлись — цель, видимо, за препятствием: бросаем её.
                    _stuckStreak++;
                    StartEscape(_stuckStreak >= 2);
                    return true;
                }
            }

            float goalDistance = DistanceCells(CurrentGoal());
            if (goalDistance < _bestGoalDistance - 1f)
            {
                _bestGoalDistance = goalDistance;
                _progressTimer = 0f;
            }
            else if ((_progressTimer += dt) > _noProgressTime && !_hunting)
            {
                StartEscape(true);
                return true;
            }
            return false;
        }

        private void StartEscape(bool giveUpGoal)
        {
            if (giveUpGoal)
            {
                if (HasPrey)
                {
                    _ignoredPrey = _prey;
                    _ignoreTimer = _ignorePreyTime;
                    _prey = null;
                    _retargetTimer = 0f;
                }
                _hunting = false;
                _hasWanderGoal = false;
                _stuckStreak = 0;
            }
            _escapeDir = FreestDirection();
            _escapeTimer = _escapeTime;
            _pathLength = 0;
            ResetProgress();
        }

        /// <summary>Из 16 направлений — то, где дальше всего свободно; при равенстве — ближе к текущему курсу.</summary>
        private Vector3 FreestDirection()
        {
            Vector3 head = _snake.HeadPosition;
            Vector3 forward = _snake.Forward;
            float cs = _grid.CellSize;
            Vector3 best = -forward;
            float bestScore = float.MinValue;
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                int free = 0;
                for (int step = 1; step <= 6; step++)
                {
                    if (Probe(head + dir * (step * 0.7f * cs)) == Blocked.Solid) break;
                    free = step;
                }
                // При равной свободе — ближе к нынешнему курсу: разворот на месте змее даётся тяжело.
                float score = free * 10f + Vector3.Dot(dir, forward) * 2f + Random.value;
                if (score <= bestScore) continue;
                bestScore = score;
                best = dir;
            }
            return best;
        }

        // ---------------- Рулление ----------------

        private void Steer()
        {
            Vector3 head = _snake.HeadPosition;
            Vector3 forward = _snake.Forward;
            float cs = _grid.CellSize;
            Vector3 goal = CurrentGoal();

            Vector3 target = goal;
            if (_pathLength > 1 && DistanceCells(goal) > 2f)
            {
                // Пропускаем пройденные точки пути и рулим на точку чуть впереди — путь сглаживается сам.
                while (_pathIndex < _pathLength - 1 && (CellCenter(_path[_pathIndex]) - head).sqrMagnitude < cs * cs)
                    _pathIndex++;
                int look = Mathf.Min(_pathIndex + _waypointLookahead, _pathLength - 1);
                target = CellCenter(_path[look]);
            }

            Vector3 toTarget = target - head;
            toTarget.y = 0f;
            float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
            float steer = Mathf.Clamp(angle / _fullSteerAngle, -1f, 1f);

            // Рефлексы поверх плана.
            Vector3 leftDir = Quaternion.Euler(0f, -_feelerAngle, 0f) * forward;
            Vector3 rightDir = Quaternion.Euler(0f, _feelerAngle, 0f) * forward;
            Blocked near = Probe(head + forward * (_nearLook * cs));
            // Прыжок без места для приземления (стенка вдоль курса, колонна сразу за ней) — это авария.
            if (near == Blocked.Jumpable && !CanLand(head, forward)) near = Blocked.Solid;
            Blocked center = Probe(head + forward * (_lookAhead * cs));
            Blocked left = Probe(head + leftDir * (_lookAhead * cs));
            Blocked right = Probe(head + rightDir * (_lookAhead * cs));

            if (near == Blocked.Solid || center == Blocked.Solid)
            {
                bool leftFree = left != Blocked.Solid;
                bool rightFree = right != Blocked.Solid;
                if (leftFree && !rightFree) steer = -1f;
                else if (rightFree && !leftFree) steer = 1f;
                else if (!leftFree && !rightFree && near == Blocked.Jumpable) _wantJump = true;
                else steer = steer >= 0f ? 1f : -1f;
            }
            else
            {
                if (near == Blocked.Jumpable) _wantJump = true;
                if (left == Blocked.Solid) steer = Mathf.Max(steer, 0.6f);
                if (right == Blocked.Solid) steer = Mathf.Min(steer, -0.6f);
            }

            TryDash(goal, angle, near, center);
            Vector3 direction = Quaternion.Euler(0f, steer * _steerToDirection, 0f) * forward;
            _snake.SetMoveIntent(direction, ChoosePace(goal));
            if (_wantJump) _snake.RequestJump();
            if (_wantStrike) _snake.TryStrike(forward);
        }

        /// <summary>К спокойной добыче — крадётся, за убегающей и на перехват — спешит, иначе обычный шаг.</summary>
        private SnakePace ChoosePace(Vector3 goal)
        {
            if (_hunting) return SnakePace.Sprint;
            if (!HasPrey) return SnakePace.Walk;
            if (_prey.IsFleeing) return SnakePace.Sprint;
            float dist = DistanceCells(goal);
            return dist < _stalkDistance && dist > _strikeDistance * 0.5f ? SnakePace.Sneak : SnakePace.Walk;
        }

        private bool HasPrey => _prey != null && _prey.IsActive && !_prey.IsFlying;

        /// <summary>Бросок: при перехвате игрока или когда добыча прямо впереди на расстоянии броска.</summary>
        private void TryDash(Vector3 goal, float angle, Blocked near, Blocked center)
        {
            if (_snake.StrikeReady01 < 1f || near != Blocked.Free || center != Blocked.Free) return;
            if (Mathf.Abs(angle) > _dashAngle) return;

            float myDist = DistanceCells(goal);
            if (_hunting)
            {
                if (myDist < 3f) _wantStrike = true;
                return;
            }

            if (HasPrey && myDist <= _strikeDistance) _wantStrike = true;
        }

        private bool CanLand(Vector3 head, Vector3 forward)
        {
            return _canJump && _snake.IsGrounded && Probe(head + forward * (_landingDistance * _grid.CellSize)) == Blocked.Free;
        }

        private Blocked Probe(Vector3 point)
        {
            float cs = _grid.CellSize;
            Vector2 half = _grid.ArenaHalfExtents;
            Vector3 local = point - _grid.Center;
            float margin = _wallAvoidMargin * cs;
            if (Mathf.Abs(local.x) > half.x - margin || Mathf.Abs(local.z) > half.y - margin) return Blocked.Solid;

            Vector2Int cell = _grid.WorldToCell(point);
            if (_grid.IsInside(cell))
            {
                float h = _grid.GetObstacleHeight(cell);
                if (h > 0f) return _canJump && h < _snake.JumpHeightWorld * 0.6f ? Blocked.Jumpable : Blocked.Solid;
            }

            float radius = _snake.HeadRadiusWorld;
            // Тела перепрыгиваемы, но лучше объехать: план их и так обходит, а рефлекс прыгает в крайнем случае.
            if (OtherAlive && _player.BodyHits(point, radius, 0)) return Blocked.Jumpable;
            if (_snake.BodyHits(point, radius, 6)) return Blocked.Jumpable;
            return Blocked.Free;
        }

        // ---------------- A* ----------------

        /// <summary>Статичная часть стоимости: препятствия и их окрестности, края поля.</summary>
        private void BuildBaseCost()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int i = y * _width + x;
                    ObstacleType type = _grid.GetObstacle(new Vector2Int(x, y));
                    if (type == ObstacleType.Pillar || type == ObstacleType.Block || type == ObstacleType.Mountain)
                    {
                        _baseCost[i] = BLOCKED;
                        continue;
                    }

                    if (type == ObstacleType.LowWall && !_canJump)
                    {
                        _baseCost[i] = BLOCKED;
                        continue;
                    }

                    int cost = type == ObstacleType.LowWall ? 1 + _lowWallCost : 1;
                    if (x == 0 || y == 0 || x == _width - 1 || y == _height - 1 || NearSolid(x, y)) cost += _nearObstacleCost;
                    _baseCost[i] = cost;
                }
            }
        }

        private bool NearSolid(int cx, int cy)
        {
            for (int d = 0; d < 8; d++)
            {
                int x = cx + DirX[d], y = cy + DirY[d];
                if (x < 0 || y < 0 || x >= _width || y >= _height) continue;
                ObstacleType t = _grid.GetObstacle(new Vector2Int(x, y));
                if (t == ObstacleType.Pillar || t == ObstacleType.Block || t == ObstacleType.Mountain
                    || (t == ObstacleType.LowWall && !_canJump)) return true;
            }
            return false;
        }

        private void Replan()
        {
            BuildDynamicCost();

            Vector3 head = _snake.HeadPosition;
            Vector3 forward = _snake.Forward;
            int start = CellIndexClamped(head + forward * (0.8f * _grid.CellSize));
            if (CellCost(start) == BLOCKED) start = CellIndexClamped(head);
            int goal = CellIndexClamped(CurrentGoal());
            if (CellCost(goal) == BLOCKED) goal = start;

            _pathLength = FindPath(start, goal, forward) ? _pathLength : 0;
            _pathIndex = 0;
        }

        /// <summary>Временная часть стоимости: тела змей и зона перед головой другой змеи.</summary>
        private void BuildDynamicCost()
        {
            System.Array.Clear(_dynamicCost, 0, _dynamicCost.Length);

            if (OtherAlive)
            {
                for (int i = 0; i < _player.SpineCount; i++) MarkBody(_player.GetSpinePoint(i));
                Vector3 pf = _player.Forward * _grid.CellSize;
                for (int k = 1; k <= 3; k++) AddCost(_player.HeadPosition + pf * k, _headOnCost);
            }

            // Своё тело — без участка у головы, иначе путь «боится» стартовой клетки.
            for (int i = 6; i < _snake.SpineCount; i++) MarkBody(_snake.GetSpinePoint(i));
        }

        private void MarkBody(Vector3 point)
        {
            Vector2Int c = _grid.WorldToCell(point);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = c.x + dx, y = c.y + dy;
                    if (x < 0 || y < 0 || x >= _width || y >= _height) continue;
                    _dynamicCost[y * _width + x] += dx == 0 && dy == 0 ? _bodyCost : _nearBodyCost;
                }
            }
        }

        private void AddCost(Vector3 point, int cost)
        {
            Vector2Int c = _grid.WorldToCell(point);
            if (c.x < 0 || c.y < 0 || c.x >= _width || c.y >= _height) return;
            _dynamicCost[c.y * _width + c.x] += cost;
        }

        private int CellCost(int index)
        {
            int baseCost = _baseCost[index];
            if (baseCost == BLOCKED) return BLOCKED;
            var cell = new Vector2Int(index % _width, index / _width);
            if (!_grid.IsCellInArena(cell, 0)) return BLOCKED;
            return baseCost + _dynamicCost[index];
        }

        /// <summary>A* с 8 соседями; на первом шаге запрещены клетки позади — змея не разворачивается на месте.</summary>
        private bool FindPath(int start, int goal, Vector3 forward)
        {
            _stamp++;
            _heapCount = 0;
            _g[start] = 0;
            _f[start] = Heuristic(start, goal);
            _parent[start] = -1;
            _seenStamp[start] = _stamp;
            HeapPush(start);

            int expansions = 0;
            int best = start;
            int bestH = int.MaxValue;
            while (_heapCount > 0 && expansions < _maxExpansions)
            {
                int current = HeapPop();
                if (_closedStamp[current] == _stamp) continue;
                _closedStamp[current] = _stamp;
                expansions++;

                int h = Heuristic(current, goal);
                if (h < bestH)
                {
                    bestH = h;
                    best = current;
                }
                if (current == goal) break;

                int cx = current % _width, cy = current / _width;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DirX[d], ny = cy + DirY[d];
                    if (nx < 0 || ny < 0 || nx >= _width || ny >= _height) continue;
                    if (current == start && DirX[d] * forward.x + DirY[d] * forward.z < -0.2f) continue;

                    int next = ny * _width + nx;
                    if (_closedStamp[next] == _stamp) continue;
                    int cost = CellCost(next);
                    if (cost == BLOCKED) continue;

                    bool diagonal = d >= 4;
                    // По диагонали нельзя срезать угол твёрдого препятствия.
                    if (diagonal && (CellCost(cy * _width + nx) == BLOCKED || CellCost(ny * _width + cx) == BLOCKED)) continue;

                    int g = _g[current] + (diagonal ? DIAGONAL : STRAIGHT) * cost;
                    if (_seenStamp[next] == _stamp && g >= _g[next]) continue;

                    _seenStamp[next] = _stamp;
                    _g[next] = g;
                    _f[next] = g + Heuristic(next, goal);
                    _parent[next] = current;
                    HeapPush(next);
                }
            }

            // Цель недостижима — идём к ближайшей к ней клетке, до которой дошли.
            int end = _closedStamp[goal] == _stamp ? goal : best;
            if (end == start) return false;

            int length = 0;
            for (int node = end; node != -1 && length < _path.Length; node = _parent[node]) length++;
            int idx = length - 1;
            for (int node = end; node != -1 && idx >= 0; node = _parent[node]) _path[idx--] = node;
            _pathLength = length;
            return true;
        }

        /// <summary>Октильное расстояние × минимальная стоимость клетки — допустимая эвристика.</summary>
        private int Heuristic(int a, int b)
        {
            int dx = Mathf.Abs(a % _width - b % _width);
            int dy = Mathf.Abs(a / _width - b / _width);
            int min = Mathf.Min(dx, dy);
            int max = Mathf.Max(dx, dy);
            return DIAGONAL * min + STRAIGHT * (max - min);
        }

        private void HeapPush(int node)
        {
            if (_heapCount >= _heap.Length) return;
            int i = _heapCount++;
            _heap[i] = node;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_f[_heap[parent]] <= _f[_heap[i]]) break;
                int tmp = _heap[parent];
                _heap[parent] = _heap[i];
                _heap[i] = tmp;
                i = parent;
            }
        }

        private int HeapPop()
        {
            int top = _heap[0];
            _heap[0] = _heap[--_heapCount];
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, smallest = i;
                if (l < _heapCount && _f[_heap[l]] < _f[_heap[smallest]]) smallest = l;
                if (r < _heapCount && _f[_heap[r]] < _f[_heap[smallest]]) smallest = r;
                if (smallest == i) break;
                int tmp = _heap[smallest];
                _heap[smallest] = _heap[i];
                _heap[i] = tmp;
                i = smallest;
            }
            return top;
        }

        // ---------------- Утилиты ----------------

        private int CellIndexClamped(Vector3 world)
        {
            Vector2Int c = _grid.WorldToCell(world);
            int inset = Mathf.CeilToInt(_grid.ArenaInset) + 1;
            c.x = Mathf.Clamp(c.x, inset, _width - 1 - inset);
            c.y = Mathf.Clamp(c.y, inset, _height - 1 - inset);
            return c.y * _width + c.x;
        }

        private Vector3 CellCenter(int index)
        {
            return _grid.CellToWorld(new Vector2Int(index % _width, index / _width));
        }

        private float DistanceCells(Vector3 point)
        {
            Vector3 d = point - _snake.HeadPosition;
            d.y = 0f;
            return d.magnitude / _grid.CellSize;
        }
    }
}
