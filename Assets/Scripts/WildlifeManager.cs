using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Живность на арене: держит численность каждого вида (днём и ночью разную), подселяет новых
    /// животных подальше от змей, обновляет их поведение и проверяет поимку.
    /// Мелочь ловится касанием; крупную добычу — только броском, касание её лишь спугивает.
    /// Кролика бросок только хватает (событие Grabbed) — душит его змея игрока обвиванием, и поимку
    /// завершает GameManager через CompleteCatch.
    /// Все животные заранее созданы в пуле (SnakeSceneBuilder), в игре нет Instantiate/Destroy.
    /// </summary>
    public class WildlifeManager : MonoBehaviour
    {
        private const int KIND_COUNT = 6;

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

        /// <summary>Змея игрока схватила броском добычу, которую нужно задушить (кролик).</summary>
        public event Action<PreyAnimal> Grabbed;

        /// <summary>Змея игрока укусила свернувшегося ежа и укололась.</summary>
        public event Action Pricked;

        /// <summary>Сила дождя 0..1: птицы, мыши и жуки прячутся, лягушек больше, шум дождя скрывает змею.</summary>
        public float Rain01 { get; set; }

        /// <summary>Ночью меняется состав и птицы спят.</summary>
        public bool IsNight { get; set; }

        /// <summary>Живность двигается и ловится (в меню тоже — это живой фон).</summary>
        public bool IsRunning { get; set; }

        /// <summary>Множитель заметности игрока от улучшений.</summary>
        public float PlayerAwarenessMultiplier { get; set; } = 1f;

        public int AnimalCount => _animals.Length;

        private readonly float[] _spawnTimers = new float[KIND_COUNT];
        private float[] _startleTimers;
        private float _hideTimer;
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

        /// <summary>Задушенная добыча съедена: убрать её и сообщить о поимке, как при обычной охоте.</summary>
        public void CompleteCatch(PreyAnimal animal, SnakeController snake)
        {
            if (animal == null || !animal.IsActive) return;
            Vector3 pos = animal.Position;
            PreyKind kind = animal.Kind;
            animal.Despawn();
            ScheduleRespawn(kind);
            Caught?.Invoke(snake, kind, pos);
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
                if (!a.IsActive || a.IsFlying || a.IsHeld) continue;
                Vector3 d = a.Position - from;
                d.y = 0f;
                float dist = d.sqrMagnitude;
                if (dist > best) continue;
                best = dist;
                nearest = a;
            }
            return nearest != null;
        }

        /// <summary>Ближайшая добыча, которую берут броском (для подсказок): жуки и схваченные не считаются.</summary>
        public bool TryGetNearestBig(Vector3 from, float maxDistance, out PreyAnimal nearest)
        {
            nearest = null;
            float best = maxDistance * maxDistance;
            for (int i = 0; i < _animals.Length; i++)
            {
                PreyAnimal a = _animals[i];
                if (!a.IsActive || a.IsFlying || a.IsHeld || !_config.GetPrey(a.Kind).RequiresStrike) continue;
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
                // Кролика ИИ не душит — не гоняется за ним зря.
                if (!a.IsActive || a.IsFlying || a.IsHeld || a.IsCurled || _config.RequiresConstrict(a.Kind)) continue;
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

        /// <summary>
        /// Насколько заметна змея: трава и засада прячут, крадущийся шаг тише, быстрое ползание и бросок
        /// выдают, а после промаха змея на секунду «открыта».
        /// </summary>
        private PreyThreat MakeThreat(SnakeController snake, float multiplier)
        {
            if (!IsHunting(snake)) return default;
            float m = multiplier;
            if (snake.IsHidden) m *= _config.CoverAwareness;
            if (snake.IsStriking) m *= _config.DashAwareness;
            else if (snake.IsMoving && snake.Pace == SnakePace.Sneak) m *= _config.SneakAwareness;
            else if (snake.IsMoving && snake.Pace == SnakePace.Sprint) m *= _config.SprintAwareness;
            m *= snake.Ambush.AwarenessMultiplier(snake.IsHidden);
            // Шум дождя скрывает шорох змеи.
            m *= 1f - 0.3f * Rain01;
            if (snake.IsExposed) m *= _config.MissAwareness;
            return new PreyThreat(true, snake.HeadPosition, m, snake.Ambush.IsLuring);
        }

        private static bool IsHunting(SnakeController snake)
        {
            return snake != null && snake.isActiveAndEnabled && snake.IsAlive && snake.IsRunning && !snake.InBurrow;
        }

        private void CheckCatches(SnakeController snake)
        {
            if (!IsHunting(snake)) return;
            float cs = _grid.CellSize;
            bool strike = snake.IsStriking;
            float r = (strike ? _config.StrikeCatchRadius : _config.TouchCatchRadius) * cs;
            float rSqr = r * r;
            float maxDy = _catchHeight * cs;
            Vector3 head = snake.HeadPosition;
            float headBottom = snake.HeightAboveGround;

            for (int i = 0; i < _animals.Length; i++)
            {
                PreyAnimal a = _animals[i];
                if (!a.IsActive || a.IsHeld) continue;
                if (Mathf.Abs(a.HeightAboveGround - headBottom) > maxDy) continue;
                Vector3 d = a.Position - head;
                d.y = 0f;
                if (d.sqrMagnitude > rSqr) continue;

                PreyStats stats = _config.GetPrey(a.Kind);
                if (a.IsCurled)
                {
                    // Колючий шар: укус ранит саму змею, ёж остаётся.
                    snake.TakeHit(_config.HedgehogDamage, DamageSource.Hedgehog, head - a.Position);
                    snake.CancelStrike();
                    if (snake == _player) Pricked?.Invoke();
                    return;
                }
                if (_config.RequiresConstrict(a.Kind))
                {
                    // Крупную добычу бросок только хватает — и только змея игрока умеет её задушить.
                    if (strike && snake == _player && Grabbed != null && snake.IsRunning)
                    {
                        a.Grab();
                        snake.NotifyStrikeHit();
                        snake.CancelStrike();
                        Grabbed.Invoke(a);
                        return;
                    }
                    a.Startle(head);
                    continue;
                }

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
            _hideTimer -= dt;
            for (int k = 0; k < KIND_COUNT; k++)
            {
                var kind = (PreyKind)k;
                int active = CountActive(kind);
                int target = TargetCount(kind);
                // В дождь лишние животные понемногу прячутся — те, что далеко от змеи.
                if (active > target)
                {
                    if (_hideTimer <= 0f && HideOne(kind)) _hideTimer = 2f;
                    continue;
                }
                if (active >= target) continue;
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
            int count = IsNight ? s.NightCount : s.DayCount;
            return Mathf.RoundToInt(count * Mathf.Lerp(1f, RainFactor(kind), Rain01));
        }

        /// <summary>Кто как переносит дождь: лягушкам раздолье, птицы и жуки прячутся.</summary>
        private static float RainFactor(PreyKind kind)
        {
            switch (kind)
            {
                case PreyKind.Frog: return 1.5f;
                case PreyKind.Bird: return 0.35f;
                case PreyKind.Beetle: return 0.5f;
                case PreyKind.Mouse: return 0.6f;
                case PreyKind.Rabbit: return 0.5f;
                default: return 0.8f;
            }
        }

        /// <summary>Убрать одно животное вида подальше от змеи (ушло в укрытие). Возвращает true, если нашлось.</summary>
        private bool HideOne(PreyKind kind)
        {
            float minDist = 15f * _grid.CellSize;
            for (int i = 0; i < _animals.Length; i++)
            {
                PreyAnimal a = _animals[i];
                if (!a.IsActive || a.Kind != kind || a.IsHeld) continue;
                if (DistanceToSnake(_player, a.Position) < minDist) continue;
                a.Despawn();
                return true;
            }
            return false;
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
                // Лягушки живут у пруда.
                bool found = kind == PreyKind.Frog ? _grid.TryGetRandomShoreCell(out Vector2Int cell) : _grid.TryGetRandomFreeCell(out cell);
                if (!found) break;
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
