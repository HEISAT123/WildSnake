using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Состояния и правила партии: режимы, уровни, еда, бонусы, опасности, соперник,
    /// статистика и рекорды. Остальные системы ничего не знают друг о друге — их связывает этот класс.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Настройки")]
        [Tooltip("Параметры баланса")]
        [SerializeField] private GameConfig _config;

        [Header("Поле")]
        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Вращающиеся балки уровней")]
        [SerializeField] private HazardManager _hazards;

        [Tooltip("Сужающаяся арена (режим «Выживание»)")]
        [SerializeField] private ShrinkingArena _arena;

        [Header("Змейки")]
        [Tooltip("Змейка игрока")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Змея-соперник под управлением ИИ (может отсутствовать)")]
        [SerializeField] private SnakeController _rival;

        [Tooltip("ИИ-автопилот змеи игрока: ведёт её на живом фоне главного меню")]
        [SerializeField] private SnakeAI _playerAutopilot;

        [Header("Предметы")]
        [Tooltip("Еда")]
        [SerializeField] private FoodSpawner _food;

        [Tooltip("Бонусы на поле")]
        [SerializeField] private PowerUpSpawner _powerUpSpawner;

        [Tooltip("Активные бонусы")]
        [SerializeField] private PowerUpService _powerUps;

        [Header("Сервисы")]
        [Tooltip("Счёт")]
        [SerializeField] private ScoreService _score;

        [Tooltip("Таблицы рекордов")]
        [SerializeField] private LeaderboardService _leaderboard;

        [Tooltip("Настройки игрока")]
        [SerializeField] private SettingsService _settings;

        [Tooltip("Звук и музыка")]
        [SerializeField] private AudioService _audio;

        [Tooltip("Камера")]
        [SerializeField] private CameraRig _cameraRig;

        [Header("Эффекты")]
        [Tooltip("Искры при поедании")]
        [SerializeField] private ParticleSystem _eatParticles;

        [Tooltip("Сколько частиц выпустить при поедании")]
        [SerializeField, Min(0)] private int _eatParticleCount = 24;

        [Tooltip("Искры при смерти")]
        [SerializeField] private ParticleSystem _deathParticles;

        [Tooltip("Сколько частиц выпустить при смерти")]
        [SerializeField, Min(0)] private int _deathParticleCount = 60;

        [Tooltip("Пыль при приземлении")]
        [SerializeField] private ParticleSystem _dustParticles;

        [Tooltip("Сколько частиц пыли при приземлении")]
        [SerializeField, Min(0)] private int _dustParticleCount = 16;

        [Tooltip("Вспышка при подборе бонуса")]
        [SerializeField] private ParticleSystem _pickupParticles;

        [Tooltip("Сколько частиц при подборе бонуса")]
        [SerializeField, Min(0)] private int _pickupParticleCount = 30;

        [Header("Живое меню")]
        [Tooltip("Через сколько секунд змея на фоне меню возрождается после аварии")]
        [SerializeField, Min(0f)] private float _menuRespawnDelay = 2f;

        [Tooltip("Максимальная длина змей на фоне меню, клеток (чтобы не заполнили поле)")]
        [SerializeField, Min(1f)] private float _menuMaxLength = 9f;

        [Header("Старт")]
        [Tooltip("Начинать игру сразу, минуя меню")]
        [SerializeField] private bool _skipMenu;

        public event Action<GameState> StateChanged;
        public event Action<int> LevelChanged;

        public GameState State { get; private set; } = GameState.Menu;
        public GameMode Mode { get; private set; } = GameMode.Classic;
        public int Level { get; private set; } = 1;
        public float TimeLeft { get; private set; }
        public bool IsTimed => Mode == GameMode.TimeAttack;
        public RunStats Stats => _stats;

        /// <summary>Место последней партии в таблице (0 — первое, -1 — не попала).</summary>
        public int LastRank { get; private set; } = -1;

        private RunStats _stats;
        private float _rivalRespawnTimer = -1f;
        private float _menuRespawnTimer = -1f;

        private bool RivalActive => _rival != null && _rival.gameObject.activeSelf;

        private void OnEnable()
        {
            _snake.FoodEaten += OnPlayerFoodEaten;
            _snake.Died += OnPlayerDied;
            _snake.Jumped += OnJumped;
            _snake.Landed += OnLanded;
            _snake.Dashed += OnDashed;
            _snake.ShieldUsed += OnShieldUsed;
            if (_rival != null)
            {
                _rival.FoodEaten += OnRivalFoodEaten;
                _rival.Died += OnRivalDied;
            }
            _food.Expired += OnFoodExpired;
            _powerUpSpawner.Collected += OnPowerUpCollected;
            _arena.Shrunk += OnArenaShrunk;
            _settings.Changed += ApplySettings;
        }

        private void OnDisable()
        {
            _snake.FoodEaten -= OnPlayerFoodEaten;
            _snake.Died -= OnPlayerDied;
            _snake.Jumped -= OnJumped;
            _snake.Landed -= OnLanded;
            _snake.Dashed -= OnDashed;
            _snake.ShieldUsed -= OnShieldUsed;
            if (_rival != null)
            {
                _rival.FoodEaten -= OnRivalFoodEaten;
                _rival.Died -= OnRivalDied;
            }
            _food.Expired -= OnFoodExpired;
            _powerUpSpawner.Collected -= OnPowerUpCollected;
            _arena.Shrunk -= OnArenaShrunk;
            _settings.Changed -= ApplySettings;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            ApplySettings();
            if (_skipMenu)
            {
                PrepareRound(true);
                SetState(GameState.Playing);
            }
            else
            {
                OpenMenu();
            }
        }

        private void Update()
        {
            switch (State)
            {
                case GameState.Menu:
                    TickMenu(Time.deltaTime);
                    break;
                case GameState.Playing:
                    if (GameInput.Pause) SetState(GameState.Paused);
                    else TickPlaying(Time.deltaTime);
                    break;
                case GameState.Paused:
                    if (GameInput.Pause) Resume();
                    else if (GameInput.Restart) Restart();
                    break;
                case GameState.GameOver:
                    if (GameInput.Restart || GameInput.Confirm) Restart();
                    break;
            }
        }

        // ---------------- Управление партией ----------------

        public void StartGame(GameMode mode)
        {
            // Из меню камера не прыгает, а плавно перелетает из витрины за змею.
            bool fromMenu = State == GameState.Menu;
            Mode = mode;
            SetAutopilot(false);
            _cameraRig.Showcase = false;
            PrepareRound(!fromMenu);
            SetState(GameState.Playing);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Restart()
        {
            StartGame(Mode);
        }

        public void OpenMenu()
        {
            _cameraRig.Showcase = true;
            PrepareRound(true);
            SetState(GameState.Menu);
        }

        private void PrepareRound(bool snapCamera)
        {
            Time.timeScale = 1f;
            _arena.ResetArena(Mode == GameMode.Survival);
            _hazards.ResetHazards();
            _powerUps.ResetAll();
            _powerUpSpawner.ResetSpawner();

            _snake.ResetSnake(_grid.Center, 90f);
            _snake.ExternalSpeedMultiplier = 1f;

            Level = 1;
            _stats = new RunStats { MaxLevel = 1 };
            TimeLeft = _config.TimeAttackSeconds;
            LastRank = -1;
            _score.ResetScore(_leaderboard.Best(Mode));

            _food.Hide();
            _food.MagnetActive = false;
            _food.MagnetTarget = _snake.Head;

            if (_rival != null)
            {
                bool rivalOn = _settings.RivalEnabled;
                _rival.gameObject.SetActive(rivalOn);
                _rivalRespawnTimer = -1f;
                if (rivalOn) SpawnRival();
            }

            SpawnFood();
            if (snapCamera) _cameraRig.SnapToTarget();
            _audio.SetMusicPitch(1f);
            LevelChanged?.Invoke(Level);
        }

        private void SetState(GameState state)
        {
            State = state;
            bool running = state == GameState.Playing;
            bool menu = state == GameState.Menu;
            // В меню змеи тоже ползают — это живой фон, игрок ими не управляет.
            _snake.IsRunning = running || menu;
            if (RivalActive) _rival.IsRunning = running || menu;
            SetAutopilot(menu);
            _cameraRig.Showcase = menu;
            _powerUps.IsTicking = running;
            _powerUpSpawner.IsRunning = running;
            _arena.IsRunning = running;
            Time.timeScale = state == GameState.Paused ? 0f : 1f;
            _audio.SetPaused(state == GameState.Paused);
            StateChanged?.Invoke(state);
        }

        private void TickPlaying(float dt)
        {
            _stats.Time += dt;

            if (IsTimed)
            {
                TimeLeft -= dt;
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    EndGame();
                    return;
                }
            }

            ApplyPowerUps();

            if (_hazards.CheckHit(_snake)) _snake.ApplyHit();
            if (State != GameState.Playing) return;

            if (RivalActive)
            {
                if (_rival.IsAlive)
                {
                    if (_hazards.CheckHit(_rival)) _rival.ApplyHit();
                    CheckSnakeCollisions();
                    if (State != GameState.Playing) return;
                }
                else
                {
                    TickRivalRespawn(dt);
                }
            }

            if (_powerUpSpawner.WantsSpawn)
            {
                PrepareOccupancy();
                _powerUpSpawner.SpawnNow();
            }
        }

        /// <summary>Живой фон меню: змеи ездят сами, после аварии возрождаются. Очки и рекорды не трогаем.</summary>
        private void TickMenu(float dt)
        {
            if (!_snake.IsAlive)
            {
                _menuRespawnTimer -= dt;
                if (_menuRespawnTimer <= 0f)
                {
                    _snake.ResetSnake(_grid.Center, UnityEngine.Random.Range(0f, 360f));
                    _snake.IsRunning = true;
                }
            }

            if (RivalActive && !_rival.IsAlive) TickRivalRespawn(dt);
        }

        private void TickRivalRespawn(float dt)
        {
            if (_rivalRespawnTimer <= 0f) return;
            _rivalRespawnTimer -= dt;
            if (_rivalRespawnTimer <= 0f) SpawnRival();
        }

        private void SetAutopilot(bool on)
        {
            _snake.AutoPilot = on;
            if (_playerAutopilot != null) _playerAutopilot.enabled = on;
        }

        private void ApplyPowerUps()
        {
            _snake.ExternalSpeedMultiplier = _powerUps.IsActive(PowerUpType.Speed) ? _config.SpeedBoostMultiplier : 1f;
            _score.Multiplier = _powerUps.IsActive(PowerUpType.DoubleScore) ? 2 : 1;
            _food.MagnetActive = _powerUps.IsActive(PowerUpType.Magnet);

            bool slow = _powerUps.IsActive(PowerUpType.SlowTime);
            Time.timeScale = slow ? _config.SlowTimeScale : 1f;
            float pitch = 1f + 0.03f * (Level - 1);
            _audio.SetMusicPitch(slow ? pitch * 0.8f : pitch);
        }

        /// <summary>Голова одной змеи о тело другой. Лоб в лоб — гибнут обе.</summary>
        private void CheckSnakeCollisions()
        {
            if (!_snake.IsAlive || !_rival.IsAlive) return;
            bool playerHit = _rival.BodyHits(_snake.HeadPosition, _snake.HeadRadiusWorld, 0);
            bool rivalHit = _snake.BodyHits(_rival.HeadPosition, _rival.HeadRadiusWorld, 0);
            if (rivalHit) _rival.ApplyHit();
            if (playerHit) _snake.ApplyHit();
        }

        private void SpawnRival()
        {
            // Ставим соперника подальше от игрока и разворачиваем к центру поля.
            PrepareOccupancy();
            Vector3 best = _grid.Center;
            float bestDist = -1f;
            for (int i = 0; i < 12; i++)
            {
                if (!_grid.TryGetRandomFreeCell(out Vector2Int cell)) break;
                Vector3 pos = _grid.CellToWorld(cell);
                float dist = (pos - _snake.HeadPosition).sqrMagnitude;
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = pos;
                }
            }

            Vector3 toCenter = _grid.Center - best;
            float yaw = toCenter.sqrMagnitude > 0.01f ? Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg : 0f;
            _rival.ResetSnake(best, yaw);
            _rival.IsRunning = State == GameState.Playing || State == GameState.Menu;
            _rivalRespawnTimer = -1f;
        }

        // ---------------- Предметы ----------------

        /// <summary>Разметить занятые клетки, чтобы новые предметы не появились на змеях, балках и друг на друге.</summary>
        private void PrepareOccupancy()
        {
            _grid.ClearAll();
            _snake.MarkOccupiedCells();
            if (RivalActive && _rival.IsAlive) _rival.MarkOccupiedCells();
            _hazards.MarkOccupiedCells();
            if (_food.HasFood) _grid.SetOccupied(_food.Cell, true);
            if (_powerUpSpawner.HasItem) _grid.SetOccupied(_powerUpSpawner.Cell, true);
        }

        private bool SpawnFood()
        {
            _food.Hide();
            PrepareOccupancy();
            return _food.Spawn();
        }

        private void OnFoodExpired()
        {
            SpawnFood();
        }

        private void OnPlayerFoodEaten(Vector3 position)
        {
            if (State == GameState.Menu)
            {
                if (_snake.BodyLengthCells < _menuMaxLength) _snake.Grow(_config.GrowthPerFood);
                Emit(_eatParticles, _food.Position, _eatParticleCount / 2);
                SpawnFood();
                return;
            }

            FoodKind kind = _food.Kind;
            switch (kind)
            {
                case FoodKind.Golden:
                    _score.AddPoints(_config.GoldenPoints);
                    _snake.Grow(_config.GrowthPerFood * 2f);
                    _snake.AddSpeed(_config.SpeedPerFood);
                    _stats.Apples++;
                    _stats.Golden++;
                    _audio.Play(SoundId.Golden);
                    _cameraRig.Shake(0.12f, 0.25f);
                    break;
                case FoodKind.Rotten:
                    _score.RemovePoints(_config.RottenPenalty);
                    _snake.Shrink(_config.RottenShrink);
                    _stats.Rotten++;
                    _audio.Play(SoundId.Rotten);
                    break;
                default:
                    _score.AddPoints(_config.NormalPoints);
                    _snake.Grow(_config.GrowthPerFood);
                    _snake.AddSpeed(_config.SpeedPerFood);
                    _stats.Apples++;
                    _audio.Play(SoundId.Eat);
                    break;
            }

            Emit(_eatParticles, _food.Position, _eatParticleCount);
            SpawnFood();

            int level = 1 + _stats.Apples / _config.ApplesPerLevel;
            if (level > Level) LevelUp(level);
        }

        private void OnRivalFoodEaten(Vector3 position)
        {
            bool capped = State == GameState.Menu && _rival.BodyLengthCells >= _menuMaxLength;
            if (_food.Kind == FoodKind.Rotten) _rival.Shrink(_config.RottenShrink);
            else if (!capped) _rival.Grow(_config.GrowthPerFood);
            Emit(_eatParticles, _food.Position, _eatParticleCount / 2);
            SpawnFood();
        }

        private void OnPowerUpCollected(PowerUpType type, Vector3 position)
        {
            _powerUps.Activate(type);
            if (type == PowerUpType.Shield) _snake.HasShield = true;
            _stats.PowerUps++;
            _audio.Play(SoundId.PowerUp);
            Emit(_pickupParticles, position, _pickupParticleCount);
        }

        private void LevelUp(int level)
        {
            Level = level;
            if (level > _stats.MaxLevel) _stats.MaxLevel = level;
            _hazards.SetLevel(level, _snake.HeadPosition);
            _audio.Play(SoundId.LevelUp);
            LevelChanged?.Invoke(level);
        }

        // ---------------- События змей ----------------

        private void OnPlayerDied(Vector3 position)
        {
            if (State == GameState.Menu)
            {
                Emit(_deathParticles, position, _deathParticleCount / 2);
                _menuRespawnTimer = _menuRespawnDelay;
                return;
            }

            Emit(_deathParticles, position, _deathParticleCount);
            _audio.Play(SoundId.Death);
            _cameraRig.Shake(0.45f, 0.6f);
            EndGame();
        }

        private void OnRivalDied(Vector3 position)
        {
            Emit(_deathParticles, position, _deathParticleCount / 2);
            if (State != GameState.Menu) _audio.Play(SoundId.Land);
            _rivalRespawnTimer = _config.RivalRespawnDelay;
        }

        // На фоне меню змеи прыгают и делают рывки сами — без звуков и тряски, чтобы не отвлекать.
        private void OnJumped()
        {
            if (State == GameState.Menu) return;
            _audio.Play(SoundId.Jump);
        }

        private void OnLanded(Vector3 position)
        {
            if (State != GameState.Menu) _audio.Play(SoundId.Land);
            Emit(_dustParticles, position, _dustParticleCount);
        }

        private void OnDashed()
        {
            if (State == GameState.Menu) return;
            _audio.Play(SoundId.Dash);
            _cameraRig.Shake(0.08f, 0.2f);
        }

        private void OnShieldUsed(Vector3 position)
        {
            _audio.Play(SoundId.Shield);
            _cameraRig.Shake(0.25f, 0.35f);
            Emit(_pickupParticles, position, _pickupParticleCount);
        }

        private void OnArenaShrunk()
        {
            _audio.Play(SoundId.Shrink);
            if (_food.HasFood && !_grid.IsCellInArena(_food.Cell, 1)) SpawnFood();
        }

        private void EndGame()
        {
            if (State == GameState.GameOver) return;
            _stats.Length = _snake.BodyLengthCells;
            var entry = new LeaderboardEntry(_score.Score, _stats.Length, _stats.Time, _stats.Apples);
            LastRank = _leaderboard.Submit(Mode, entry);
            _audio.SetMusicPitch(0.7f);
            SetState(GameState.GameOver);
        }

        private void ApplySettings()
        {
            _snake.SteerSensitivity = _settings.SteerSensitivity;
        }

        private static void Emit(ParticleSystem ps, Vector3 position, int count)
        {
            if (ps == null || count <= 0) return;
            ps.transform.position = position;
            ps.Emit(count);
        }
    }
}
