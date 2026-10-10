using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Живность на арене: держит численность каждого вида (днём и ночью разную), подселяет новых
    /// животных подальше от змей, обновляет их поведение и проверяет поимку.
    /// Мелочь ловится касанием; крупную добычу — только броском, касание её лишь спугивает.
    /// Все животные заранее созданы в пуле (SnakeSceneBuilder), в игре нет Instantiate/Destroy.
    /// </summary>
    public class WildlifeManager : MonoBehaviour
    {
        private const int KIND_COUNT = 4;

        [Header("Ссылки")]
        [Tooltip("Параметры баланса")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Змея игрока")]
        [SerializeField] private SnakeController _player;

        [Tooltip("Змея-соперница (может отсутствовать)")]
        [SerializeField] private SnakeController _rival;

        [Tooltip("Пул животных всех видов")]
        [SerializeField] private PreyAnimal[] _animals = new PreyAnimal[0];

        [Header("Подселение")]
        [Tooltip("Пауза перед появлением нового животного вместо пойманного, сек (мин, макс)")]
        [SerializeField] private Vector2 _respawnDelay = new Vector2(2f, 6f);

        [Tooltip("Не ближе к змеям, клеток")]
        [SerializeField, Min(0f)] private float _minSpawnDistance = 8f;

        [Tooltip("Максимальная разница высот головы и добычи для поимки, клеток")]
        [SerializeField, Min(0.1f)] private float _catchHeight = 0.9f;

        [Tooltip("Как часто одна и та же добыча может «пискнуть», что её спугнули, сек")]
        [SerializeField, Min(0.1f)] private float _startleCooldown = 1f;

        /// <summary>(кто поймал, вид, где) — рост и сытость решает GameManager.</summary>
        public event Action<SnakeController, PreyKind, Vector3> Caught;

        /// <summary>Змея игрока коснулась крупной добычи без броска и спугнула её.</summary>
        public event Action<PreyKind, Vector3> Startled;

        /// <summary>Ночью меняется состав и птицы спят.</summary>
        public bool IsNight { get; set; }

        /// <summary>Живность двигается и ловится (в меню тоже — это живой фон).</summary>
        public bool IsRunning { get; set; }

        /// <summary>Множитель заметности игрока от улучшений.</summary>
        public float PlayerAwarenessMultiplier { get; set; } = 1f;

        public int AnimalCount => _animals.Length;

        private readonly float[] _spawnTimers = new float[KIND_COUNT];
        private float[] _startleTimers;
        private bool _initialized;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _startleTimers = new float[_animals.Length];
            for (int i = 0; i < _animals.Length; i++) _animals[i].Initialize(_grid, _config);
        }

        public PreyAnimal GetAnimal(int index)
        {
            return _animals[index];
        }

        /// <summary>Убрать всех и сразу заселить арену (новая партия, меню).</summary>
        public void ResetAll()
        {
            Initialize();
            for (int i = 0; i < _animals.Length; i++) _animals[i].Despawn();
            for (int k = 0; k < KIND_COUNT; k++)
            {
                _spawnTimers[k] = 0f;
                int target = TargetCount((PreyKind)k);
                for (int n = 0; n < target; n++) SpawnOne((PreyKind)k);
            }
        }

        /// <summary>Подсветить (или погасить) метки запаха у всей добычи в радиусе.</summary>
        public void SetScentMarks(bool on, Vector3 center, float radius)
        {
            float rSqr = radius * radius;
            for (int i = 0; i < _animals.Length; i++)
            {
                PreyAnimal a = _animals[i];
                if (!a.IsActive) continue;
                Vector3 d = a.Position - center;
                d.y = 0f;
                a.SetScent(on && d.sqrMagnitude <= rSqr);
            }
        }

        /// <summary>Ближайшая добыча в радиусе (для нюха). Улетающие птицы не считаются.</summary>
        public bool TryGetNearest(Vector3 from, float maxDistance, out PreyAnimal nearest)
        {
            nearest = null;
            float best = maxDistance * maxDistance;
            for (int i = 0; i < _animals.Length; i++)
            {
                PreyAnimal a = _animals[i];
                if (!a.IsActive || a.IsFlying) continue;
                Vector3 d = a.Position - from;
                d.y = 0f;
                float dist = d.sqrMagnitude;
                if (dist > best) continue;
                best = dist;
                nearest = a;
            }
            return nearest != null;
        }

        /// <summary>
        /// Лучшая цель для ИИ: сытность делится на расстояние, убегающая и чужая (рядом с другой змеёй) — хуже.
        /// </summary>
        public PreyAnimal FindBestTarget(Vector3 from, Vector3 otherHead, bool otherActive)
        {
            PreyAnimal best = null;
            float bestScore = 0f;
            float cs = _grid.CellSize;
            for (int i = 0; i < _animals.Length; i++)
            {
                PreyAnimal a = _animals[i];
                if (!a.IsActive || a.IsFlying) continue;
                Vector3 d = a.Position - from;
                d.y = 0f;
                float dist = d.magnitude / cs;
                float score = _config.GetPrey(a.Kind).Satiety / (dist + 4f);
                if (a.IsFleeing) score *= 0.5f;
                if (otherActive)
                {
                    Vector3 o = a.Position - otherHead;
                    o.y = 0f;
                    if (o.magnitude / cs < dist * 0.6f) score *= 0.4f;
                }
                if (score <= bestScore) continue;
                bestScore = score;
                best = a;
            }
            return best;
        }

        private void Update()
        {
            if (!IsRunning) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            PreyThreat playerThreat = MakeThreat(_player, PlayerAwarenessMultiplier);
            PreyThreat rivalThreat = MakeThreat(_rival, 1f);
            for (int i = 0; i < _animals.Length; i++)
            {
                PreyAnimal a = _animals[i];
                if (!a.IsActive) continue;
                a.Tick(dt, playerThreat, rivalThreat, IsNight);
                if (_startleTimers[i] > 0f) _startleTimers[i] -= dt;
            }

            CheckCatches(_player);
            CheckCatches(_rival);
            MaintainPopulation(dt);
        }

        private PreyThreat MakeThreat(SnakeController snake, float multiplier)
        {
            if (!IsHunting(snake)) return default;
            float m = multiplier;
            if (snake.IsHidden) m *= _config.CoverAwareness;
            if (snake.IsDashing) m *= _config.DashAwareness;
            return new PreyThreat(true, snake.HeadPosition, m);
        }

        private static bool IsHunting(SnakeController snake)
        {
            return snake != null && snake.isActiveAndEnabled && snake.IsAlive && snake.IsRunning;
        }

        private void CheckCatches(SnakeController snake)
        {
            if (!IsHunting(snake)) return;
            float cs = _grid.CellSize;
            bool strike = snake.IsDashing;
            float r = (strike ? _config.StrikeCatchRadius : _config.TouchCatchRadius) * cs;
            float rSqr = r * r;
            float maxDy = _catchHeight * cs;
            Vector3 head = snake.HeadPosition;
            float headBottom = snake.HeightAboveGround;

            for (int i = 0; i < _animals.Length; i++)
            {
                PreyAnimal a = _animals[i];
                if (!a.IsActive) continue;
                if (Mathf.Abs(a.HeightAboveGround - headBottom) > maxDy) continue;
                Vector3 d = a.Position - head;
                d.y = 0f;
                if (d.sqrMagnitude > rSqr) continue;

                PreyStats stats = _config.GetPrey(a.Kind);
                // Уставшая добыча не успевает увернуться — её можно взять и касанием.
                if (stats.RequiresStrike && !strike && !a.IsResting)
                {
                    a.Startle(head);
                    if (snake == _player && _startleTimers[i] <= 0f)
                    {
                        _startleTimers[i] = _startleCooldown;
                        Startled?.Invoke(a.Kind, a.Position);
                    }
                    continue;
                }

                Vector3 pos = a.Position;
                a.Despawn();
                ScheduleRespawn(a.Kind);
                Caught?.Invoke(snake, a.Kind, pos);
            }
        }

        private void MaintainPopulation(float dt)
        {
            for (int k = 0; k < KIND_COUNT; k++)
            {
                var kind = (PreyKind)k;
                if (CountActive(kind) >= TargetCount(kind)) continue;
                _spawnTimers[k] -= dt;
                if (_spawnTimers[k] > 0f) continue;
                SpawnOne(kind);
                ScheduleRespawn(kind);
            }
        }

        private void ScheduleRespawn(PreyKind kind)
        {
            float delay = UnityEngine.Random.Range(_respawnDelay.x, _respawnDelay.y);
            _spawnTimers[(int)kind] = Mathf.Max(_spawnTimers[(int)kind], delay);
        }

        private int TargetCount(PreyKind kind)
        {
            PreyStats s = _config.GetPrey(kind);
            return IsNight ? s.NightCount : s.DayCount;
        }

        private int CountActive(PreyKind kind)
        {
            int n = 0;
            for (int i = 0; i < _animals.Length; i++)
                if (_animals[i].IsActive && _animals[i].Kind == kind) n++;
            return n;
        }

        /// <summary>Свободная клетка подальше от змей: несколько случайных попыток, берём самую дальнюю.</summary>
        private void SpawnOne(PreyKind kind)
        {
            PreyAnimal animal = null;
            for (int i = 0; i < _animals.Length; i++)
            {
                if (_animals[i].IsActive || _animals[i].Kind != kind) continue;
                animal = _animals[i];
                break;
            }
            if (animal == null) return;

            PrepareOccupancy();
            float cs = _grid.CellSize;
            float minDist = _minSpawnDistance * cs;
            Vector3 best = Vector3.zero;
            float bestDist = -1f;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (!_grid.TryGetRandomFreeCell(out Vector2Int cell)) break;
                Vector3 pos = _grid.CellToWorld(cell);
                float dist = Mathf.Min(DistanceToSnake(_player, pos), DistanceToSnake(_rival, pos));
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = pos;
                }
                if (dist >= minDist) break;
            }
            if (bestDist < 0f) return;
            animal.Spawn(best, UnityEngine.Random.Range(0f, 360f));
        }

        private void PrepareOccupancy()
        {
            _grid.ClearAll();
            if (IsPresent(_player)) _player.MarkOccupiedCells();
            if (IsPresent(_rival)) _rival.MarkOccupiedCells();
            for (int i = 0; i < _animals.Length; i++)
            {
                if (!_animals[i].IsActive) continue;
                Vector2Int cell = _grid.WorldToCell(_animals[i].Position);
                if (_grid.IsInside(cell)) _grid.SetOccupied(cell, true);
            }
        }

        private static bool IsPresent(SnakeController snake)
        {
            return snake != null && snake.isActiveAndEnabled && snake.IsAlive;
        }

        private static float DistanceToSnake(SnakeController snake, Vector3 pos)
        {
            if (!IsPresent(snake)) return float.MaxValue;
            Vector3 d = snake.HeadPosition - pos;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
