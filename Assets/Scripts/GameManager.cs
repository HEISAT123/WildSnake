using System;
using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Правила выживания: голод и здоровье, охота, удары, орёл, смена дня и ночи, линька с улучшениями,
    /// соперница, статистика и рекорды. Остальные системы ничего не знают друг о друге — их связывает этот класс.
    /// Соперничество: дневное состязание (кто больше добыл до заката), трофей дня, кража душимой добычи,
    /// обида и уступка соперницы; умения змеи — шипение (Q) и «мёртвая змея» (X).
    /// Меню — живой фон: змеи ездят на автопилоте и охотятся, но урон, голод и рекорды не считаются.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const int MOLT_CHOICES = 3;

        [Header("Настройки")]
        [Tooltip("Параметры баланса")]
        [SerializeField] private GameConfig _config;

        [Header("Поле")]
        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Header("Змейки")]
        [Tooltip("Змейка игрока")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Голова игрока: язык для нюха")]
        [SerializeField] private SnakeHeadAnimator _headAnimator;

        [Tooltip("Змея-соперница под управлением ИИ (может отсутствовать)")]
        [SerializeField] private SnakeController _rival;

        [Tooltip("ИИ соперницы: её характер (обида, уступка, кража добычи)")]
        [SerializeField] private SnakeAI _rivalAI;

        [Tooltip("ИИ-автопилот змеи игрока: ведёт её на живом фоне главного меню")]
        [SerializeField] private SnakeAI _playerAutopilot;

        [Tooltip("Ввод игрока: включается только во время партии")]
        [SerializeField] private PlayerSnakeInput _playerInput;

        [Header("Природа")]
        [Tooltip("Живность-добыча")]
        [SerializeField] private WildlifeManager _wildlife;

        [Tooltip("Орёл")]
        [SerializeField] private EagleHunter _eagle;

        [Tooltip("Лиса — наземный хищник")]
        [SerializeField] private FoxHunter _fox;

        [Tooltip("Смена дня и ночи")]
        [SerializeField] private DayNightCycle _dayNight;

        [Tooltip("Погода: дождь")]
        [SerializeField] private WeatherSystem _weather;

        [Tooltip("Нюх")]
        [SerializeField] private ScentTracker _scent;

        [Tooltip("Сброшенная кожа при линьке")]
        [SerializeField] private SkinShedEffect _skinShed;

        [Header("Гнёзда и потомство")]
        [Tooltip("Птичьи гнёзда с яйцами")]
        [SerializeField] private BirdNests _birdNests;

        [Tooltip("Змеёныши, расползающиеся при вылуплении")]
        [SerializeField] private Hatchlings _hatchlings;

        [Tooltip("Осиные гнёзда на пнях")]
        [SerializeField] private WaspNests _wasps;

        [Header("Гнездо")]
        [Tooltip("Яйца в гнезде: появляются, когда змея их отложит")]
        [SerializeField] private GameObject _nestEggs;

        [Tooltip("Светящийся столб над гнездом: виден, когда змея повзрослела")]
        [SerializeField] private GameObject _nestBeacon;

        [Header("Сервисы")]
        [Tooltip("Таблица рекордов")]
        [SerializeField] private LeaderboardService _leaderboard;

        [Tooltip("Настройки игрока")]
        [SerializeField] private SettingsService _settings;

        [Tooltip("Звук и музыка")]
        [SerializeField] private AudioService _audio;

        [Tooltip("Камера")]
        [SerializeField] private CameraRig _cameraRig;

        [Header("Эффекты")]
        [Tooltip("Брызги при поимке добычи")]
        [SerializeField] private ParticleSystem _eatParticles;

        [Tooltip("Сколько частиц при поимке")]
        [SerializeField, Min(0)] private int _eatParticleCount = 24;

        [Tooltip("Искры при смерти и ударах")]
        [SerializeField] private ParticleSystem _deathParticles;

        [Tooltip("Сколько частиц при смерти")]
        [SerializeField, Min(0)] private int _deathParticleCount = 60;

        [Tooltip("Пыль при приземлении")]
        [SerializeField] private ParticleSystem _dustParticles;

        [Tooltip("Сколько частиц пыли при приземлении")]
        [SerializeField, Min(0)] private int _dustParticleCount = 16;

        [Tooltip("Блёстки при линьке")]
        [SerializeField] private ParticleSystem _moltParticles;

        [Tooltip("Сколько частиц при линьке")]
        [SerializeField, Min(0)] private int _moltParticleCount = 40;

        [Header("Живое меню")]
        [Tooltip("Через сколько секунд змея на фоне меню возрождается после аварии")]
        [SerializeField, Min(0f)] private float _menuRespawnDelay = 2f;

        [Tooltip("Максимальная длина змей на фоне меню, клеток (чтобы не заполнили поле)")]
        [SerializeField, Min(1f)] private float _menuMaxLength = 9f;

        [Tooltip("Время суток на фоне меню (0.7 — золотой час перед закатом)")]
        [SerializeField, Range(0f, 1f)] private float _menuPhase = 0.7f;

        [Tooltip("Через сколько секунд над меню пролетает орёл")]
        [SerializeField, Min(0f)] private float _menuEagleDelay = 6f;

        [Header("Старт")]
        [Tooltip("Начинать игру сразу, минуя меню")]
        [SerializeField] private bool _skipMenu;

        public event Action<GameState> StateChanged;

        /// <summary>Игрок поймал добычу.</summary>
        public event Action<PreyKind> PreyCaught;

        /// <summary>Игрок получил урон (сколько, откуда).</summary>
        public event Action<float, DamageSource> PlayerDamaged;

        /// <summary>Игрок получил удар: сколько, от чего и из какой точки мира он пришёл (для виньетки).</summary>
        public event Action<float, DamageSource, Vector3> PlayerDamagedFrom;

        /// <summary>Линька завершена, взято улучшение.</summary>
        public event Action<UpgradeType> MoltCompleted;

        /// <summary>Змея схватила крупную добычу и начала её душить.</summary>
        public event Action<PreyKind> ConstrictionStarted;

        /// <summary>Обвивание закончилось: true — задушила, false — добыча вырвалась.</summary>
        public event Action<bool> ConstrictionEnded;

        /// <summary>Змея вошла в нору (true, прогресс сохранён) или вышла из неё (false).</summary>
        public event Action<bool> BurrowChanged;

        /// <summary>На рассвете выбраны новые цели суток.</summary>
        public event Action GoalsRolled;

        /// <summary>Выполнена цель суток (индекс в Goals).</summary>
        public event Action<int> GoalCompleted;

        /// <summary>Лиса отпугнута броском.</summary>
        public event Action FoxScared;

        /// <summary>Змея укусила свернувшегося ежа и укололась.</summary>
        public event Action HedgehogPricked;

        /// <summary>Пошёл дождь (true) или кончился (false).</summary>
        public event Action<bool> RainChanged;

        /// <summary>Главная цель перешла на новый этап.</summary>
        public event Action<QuestStage> QuestAdvanced;

        /// <summary>Змея у гнезда, но отложить яйца пока нельзя (чего не хватает).</summary>
        public event Action<NestCheck> NestBlocked;

        /// <summary>Лиса утащила яйцо из кладки (сколько осталось).</summary>
        public event Action<int> ClutchRaided;

        /// <summary>Лиса утащила все яйца — кладку надо делать заново.</summary>
        public event Action ClutchLost;

        /// <summary>Кладка вылупилась: (номер нового поколения, сколько змеёнышей).</summary>
        public event Action<int, int> GenerationStarted;

        /// <summary>Бросок отогнал птицу от её гнезда.</summary>
        public event Action BirdScared;

        /// <summary>Из гнезда вылетел осиный рой и погнался за змеёй.</summary>
        public event Action WaspsSwarmed;

        /// <summary>Рой ужалил лису, и та убежала.</summary>
        public event Action FoxStungByWasps;

        /// <summary>Змея закатила свёрнутого ежа в воду.</summary>
        public event Action HedgehogSplashed;

        /// <summary>Соперничество, трофей дня, шипение и «мёртвая змея» — для баннеров.</summary>
        public event Action<GameNews> News;

        public GameState State { get; private set; } = GameState.Menu;
        public RunStats Stats => _stats;
        public SurvivalStats Survival => _survival;

        /// <summary>Шкала борьбы при обвивании.</summary>
        public Constriction Constriction => _constriction;

        /// <summary>Цели текущих суток.</summary>
        public DailyGoals Goals => _goals;

        /// <summary>Идёт дневное состязание с соперницей (день, соперница на поле).</summary>
        public bool ContestActive => RivalActive && !_dayNight.IsNight;

        /// <summary>Добыча игрока за сегодняшний день (состязание).</summary>
        public int ContestPlayer => _contestPlayer;

        /// <summary>Добыча соперницы за сегодняшний день (состязание).</summary>
        public int ContestRival => _contestRival;

        /// <summary>Соперница идёт отбивать добычу, которую душит игрок.</summary>
        public bool RivalStealing => _constriction.IsActive && _rivalAI != null && _rivalAI.StealTarget != null;

        /// <summary>Длина змеи игрока, клеток.</summary>
        public float SnakeLength => _snake.BodyLengthCells;

        /// <summary>Главная цель «Продолжить род».</summary>
        public MainQuest Quest => _quest;

        /// <summary>Сколько линек пережила змея текущего поколения (для цели «повзрослеть»).</summary>
        public int GenerationMolts => _generationMolts;

        /// <summary>Змея у гнезда (можно попробовать отложить яйца клавишей E).</summary>
        public bool NearNest
        {
            get
            {
                if (!_grid.HasNest || _snake.InBurrow || !_snake.IsAlive) return false;
                Vector3 d = _grid.NestPosition - _snake.HeadPosition;
                d.y = 0f;
                return d.magnitude <= _config.NestRadius * _grid.CellSize;
            }
        }

        /// <summary>Что мешает отложить яйца прямо сейчас (для подсказок).</summary>
        public NestCheck NestStatus => _quest.CanLay(_dayNight.IsNight, _survival.Satiety01);
        public UpgradeSet Upgrades => _upgrades;

        /// <summary>Место последней жизни в таблице (0 — первое, -1 — не попала).</summary>
        public int LastRank { get; private set; } = -1;

        /// <summary>Сколько роста набрано к следующей линьке, 0..1.</summary>
        public float MoltProgress01 => Mathf.Clamp01(_moltProgress / _config.MoltGrowth);

        /// <summary>Какую долю шкалы линьки даёт охотничья цель дня (для журнала).</summary>
        public float GoalMoltShare => _config.GoalMoltReward;

        /// <summary>Варианты улучшений текущей линьки.</summary>
        public int MoltChoiceCount { get; private set; }

        public UpgradeType GetMoltChoice(int index) => _moltChoices[index];

        /// <summary>Длина суток в секундах (день + ночь).</summary>
        public float DayLengthSeconds => _config.DaySeconds + _config.NightSeconds;

        /// <summary>Змея пережидает в норе.</summary>
        public bool InBurrow => _snake.InBurrow;

        /// <summary>Нора, в которую можно войти прямо сейчас (-1 — рядом нет).</summary>
        public int NearbyBurrow
        {
            get
            {
                if (_snake.InBurrow || !_snake.IsAlive) return -1;
                int i = _grid.NearestBurrow(_snake.HeadPosition, out float dist);
                return i >= 0 && dist <= _config.BurrowEnterRadius * _grid.CellSize ? i : -1;
            }
        }

        private readonly UpgradeType[] _moltChoices = new UpgradeType[MOLT_CHOICES];
        private readonly System.Random _rng = new System.Random();
        private SurvivalStats _survival;
        private readonly UpgradeSet _upgrades = new UpgradeSet();
        private readonly Constriction _constriction = new Constriction();
        private readonly DailyGoals _goals = new DailyGoals();
        private readonly MainQuest _quest = new MainQuest();
        private bool _nightOutside;
        private PreyAnimal _heldPrey;
        private int _burrowIndex;
        private RunStats _stats;
        private float _moltProgress;
        private bool _moltPending;
        private int _generationMolts;
        private float _rivalHealth;
        private float _rivalRespawnTimer = -1f;
        private float _menuRespawnTimer = -1f;
        private float _thornTimer;
        private float _rivalReturnLength;
        private bool _rivalHitByPlayer;
        private int _contestPlayer;
        private int _contestRival;
        private bool _trophyDone;
        private float _trophyRetry;

        /// <summary>Соперница в этой жизни есть (не выключена в настройках).</summary>
        public bool RivalActive => _rival != null && _rival.gameObject.activeSelf;

        private void Awake()
        {
            _survival = new SurvivalStats(_config.MaxHealth, _config.MaxSatiety, _config.StartSatiety)
            {
                RegenPerSecond = _config.RegenPerSecond,
                WellFedThreshold = _config.WellFedThreshold
            };
            _quest.BaseLength = _config.QuestLength;
            _quest.LengthStep = _config.GenerationLengthStep;
            _quest.TargetMolts = _config.QuestMolts;
            _quest.NestSatiety = _config.NestSatiety;
            _quest.ClutchSize = _config.ClutchSize;
            _quest.HatchSeconds = _config.HatchSeconds;
            _constriction.Duration = _config.ConstrictDuration;
            _constriction.Patience = _config.ConstrictPatience;
        }

        private void OnEnable()
        {
            _snake.Hurt += OnPlayerHurt;
            _snake.Died += OnPlayerDied;
            _snake.Jumped += OnJumped;
            _snake.Landed += OnLanded;
            _snake.StrikeStarted += OnStrikeStarted;
            _snake.Hissed += OnHissed;
            if (_rival != null)
            {
                _rival.Hurt += OnRivalHurt;
                _rival.Died += OnRivalDied;
            }
            _wildlife.Caught += OnPreyCaught;
            _wildlife.Grabbed += OnPreyGrabbed;
            _wildlife.Pricked += OnPricked;
            _wildlife.Pushed += OnHedgehogPushed;
            _wildlife.HedgehogSplashed += OnHedgehogSplashed;
            _wildlife.MouseHid += OnMouseHid;
            _wildlife.TrophyCaught += OnTrophyCaught;
            _wildlife.TrophyLost += OnTrophyLost;
            _wasps.SwarmStarted += OnWaspsSwarmed;
            _wasps.Stung += OnWaspStung;
            _wasps.FoxStung += OnFoxStung;
            _weather.RainStarted += OnRainStarted;
            _weather.RainStopped += OnRainStopped;
            _eagle.Screeched += OnEagleScreeched;
            _eagle.DiveStarted += OnEagleDive;
            _eagle.Struck += OnEagleStruck;
            _eagle.Missed += OnEagleMissed;
            _fox.Appeared += OnFoxAppeared;
            _fox.Bit += OnFoxBit;
            _fox.Raided += OnFoxRaided;
            _fox.LostInterest += OnFoxLostInterest;
            _birdNests.EggEaten += OnEggEaten;
            _birdNests.Pecked += OnBirdPecked;
            _birdNests.ParentScared += OnBirdScared;
            _dayNight.DayStarted += OnDayStarted;
            _dayNight.NightStarted += OnNightStarted;
            _settings.Changed += ApplySettings;
        }

        private void OnDisable()
        {
            _snake.Hurt -= OnPlayerHurt;
            _snake.Died -= OnPlayerDied;
            _snake.Jumped -= OnJumped;
            _snake.Landed -= OnLanded;
            _snake.StrikeStarted -= OnStrikeStarted;
            _snake.Hissed -= OnHissed;
            if (_rival != null)
            {
                _rival.Hurt -= OnRivalHurt;
                _rival.Died -= OnRivalDied;
            }
            _wildlife.Caught -= OnPreyCaught;
            _wildlife.Grabbed -= OnPreyGrabbed;
            _wildlife.Pricked -= OnPricked;
            _wildlife.Pushed -= OnHedgehogPushed;
            _wildlife.HedgehogSplashed -= OnHedgehogSplashed;
            _wildlife.MouseHid -= OnMouseHid;
            _wildlife.TrophyCaught -= OnTrophyCaught;
            _wildlife.TrophyLost -= OnTrophyLost;
            _wasps.SwarmStarted -= OnWaspsSwarmed;
            _wasps.Stung -= OnWaspStung;
            _wasps.FoxStung -= OnFoxStung;
            _weather.RainStarted -= OnRainStarted;
            _weather.RainStopped -= OnRainStopped;
            _eagle.Screeched -= OnEagleScreeched;
            _eagle.DiveStarted -= OnEagleDive;
            _eagle.Struck -= OnEagleStruck;
            _eagle.Missed -= OnEagleMissed;
            _fox.Appeared -= OnFoxAppeared;
            _fox.Bit -= OnFoxBit;
            _fox.Raided -= OnFoxRaided;
            _fox.LostInterest -= OnFoxLostInterest;
            _birdNests.EggEaten -= OnEggEaten;
            _birdNests.Pecked -= OnBirdPecked;
            _birdNests.ParentScared -= OnBirdScared;
            _dayNight.DayStarted -= OnDayStarted;
            _dayNight.NightStarted -= OnNightStarted;
            _settings.Changed -= ApplySettings;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            ApplySettings();
            if (_skipMenu) StartGame();
            else OpenMenu();
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
                    // «Заново» на паузе — только пунктом меню: случайное R не должно стирать жизнь.
                    if (GameInput.Pause) Resume();
                    break;
                case GameState.GameOver:
                    // Enter/Space обрабатывает список на экране итогов («Ещё раз» выбран по умолчанию).
                    if (GameInput.Restart) Restart();
                    break;
            }
            // На линьке выбор делает список улучшений на экране — здесь ждать нечего.
        }

        // ---------------- Управление партией ----------------

        /// <summary>Новая жизнь: старое сохранение у норы стирается.</summary>
        public void StartGame()
        {
            RunSaveStore.Clear();
            StartRun(null);
        }

        /// <summary>«Играть» в меню: если есть сохранение у норы — продолжить, иначе новая жизнь.</summary>
        public void PlayFromMenu()
        {
            if (RunSaveStore.TryLoad(out RunSave save)) StartRun(save);
            else StartGame();
        }

        private void StartRun(RunSave save)
        {
            // Из меню камера не прыгает, а плавно перелетает из витрины за змею.
            bool fromMenu = State == GameState.Menu;
            SetAutopilot(false);
            _cameraRig.Showcase = false;
            if (save != null) _dayNight.Restore(save.Day, save.Phase);
            else _dayNight.ResetCycle(_config.StartPhase);
            _eagle.Ambient = false;
            _eagle.ResetHunter(_config.EagleFirstDelay);
            _fox.ResetHunter(_config.FoxFirstDelay);
            _weather.ResetWeather(_config.FirstRainDelay);
            PrepareRound(!fromMenu);
            _goals.Roll(_dayNight.Day, _rng, RivalActive);
            _nightOutside = false;
            GoalsRolled?.Invoke();
            if (save != null) ApplySave(save);
            SetState(GameState.Playing);
            if (save != null) EnterBurrow(Mathf.Clamp(save.Burrow, 0, Mathf.Max(0, _grid.BurrowCount - 1)), false);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Restart()
        {
            StartGame();
        }

        public void OpenMenu()
        {
            _cameraRig.Showcase = true;
            _dayNight.SetPhase(_menuPhase);
            _eagle.Ambient = true;
            _eagle.ResetHunter(_menuEagleDelay);
            _fox.ResetHunter(_config.FoxFirstDelay);
            _weather.ResetWeather(_config.FirstRainDelay);
            _goals.Clear();
            PrepareRound(true);
            SetState(GameState.Menu);
        }

        /// <summary>Выбор улучшения на экране линьки.</summary>
        public void ChooseUpgrade(int slot)
        {
            if (State != GameState.Molting || slot < 0 || slot >= MoltChoiceCount) return;
            UpgradeType type = _moltChoices[slot];
            _upgrades.Apply(type);
            if (type == UpgradeType.StrongHeart) _survival.AddMaxHealth(UpgradeSet.STRONG_HEART_BONUS);
            ApplyUpgrades();

            // Новая кожа: здоровье восстановлено, старая шкурка остаётся лежать на земле.
            _survival.HealFull();
            _skinShed.Shed();
            _stats.Molts++;
            _generationMolts++;
            Emit(_moltParticles, _snake.HeadPosition, _moltParticleCount);
            SetState(GameState.Playing);
            MoltCompleted?.Invoke(type);
        }

        private void PrepareRound(bool snapCamera)
        {
            Time.timeScale = 1f;
            _snake.ResetSnake(_grid.Center, 90f);

            _upgrades.Reset();
            _survival.Reset(_config.MaxHealth, _config.MaxSatiety, _config.StartSatiety);
            ApplyUpgrades();
            _moltProgress = 0f;
            _moltPending = false;
            _generationMolts = 0;
            MoltChoiceCount = 0;
            _stats = new RunStats();
            LastRank = -1;
            _quest.Reset();
            SetNestVisuals(false, false);
            SetRaid(false);
            _birdNests.ResetAll();
            _wasps.ResetAll();
            _thornTimer = 0f;
            _hatchlings.Clear();
            _contestPlayer = 0;
            _contestRival = 0;
            _trophyDone = false;
            _trophyRetry = 0f;

            if (_rival != null)
            {
                bool rivalOn = _settings.RivalEnabled;
                _rival.gameObject.SetActive(rivalOn);
                _rivalRespawnTimer = -1f;
                _rivalReturnLength = 0f;
                if (_rivalAI != null) _rivalAI.ResetMood();
                if (rivalOn) SpawnRival();
            }

            // Состав живности зависит от времени суток — оно уже выставлено.
            EndConstriction(false, false);
            _wildlife.IsNight = _dayNight.IsNight;
            _wildlife.ResetAll();
            _scent.ResetScent();
            _skinShed.Clear();
            _playerInput.MovementLocked = false;

            if (snapCamera) _cameraRig.SnapToTarget();
            _audio.SetMusicPitch(1f);
        }

        private void SetState(GameState state)
        {
            State = state;
            bool running = state == GameState.Playing;
            bool menu = state == GameState.Menu;
            bool frozen = state == GameState.Paused || state == GameState.Molting;
            // В меню змеи тоже ползают — это живой фон, игрок ими не управляет.
            _snake.IsRunning = running || menu;
            if (RivalActive) _rival.IsRunning = running || menu;
            SetAutopilot(menu);
            _playerInput.enabled = running;
            _cameraRig.Showcase = menu;
            _cameraRig.OrbitInput = running;
            _wildlife.IsRunning = running || menu;
            _birdNests.IsRunning = running;
            _wasps.IsRunning = running;
            _dayNight.Running = running;
            _eagle.HuntingAllowed = running && !_dayNight.IsNight;
            _fox.HuntingAllowed = running;
            _weather.Running = running;
            Time.timeScale = frozen ? 0f : 1f;
            _audio.SetPaused(frozen);
            StateChanged?.Invoke(state);
        }

        private void TickPlaying(float dt)
        {
            if (GameInput.Interact) Interact();
            _snake.Daylight01 = _dayNight.Daylight01;
            // Дождь остужает; под кронами меньше, в норе не действует вовсе (это решает BodyTemperature).
            _snake.Temperature.Chill = _weather.Rain01 * _config.RainChill * (_snake.UnderCanopy ? 0.35f : 1f);
            _wildlife.Rain01 = _weather.Rain01;

            _stats.Time += dt;
            _stats.Days = _stats.Time / DayLengthSeconds;
            _wildlife.IsNight = _dayNight.IsNight;
            _eagle.HuntingAllowed = !_dayNight.IsNight;
            _fox.IsNight = _dayNight.IsNight;
            _birdNests.IsNight = _dayNight.IsNight;
            _wasps.IsNight = _dayNight.IsNight;

            TickHunger(dt);
            if (State != GameState.Playing) return;
            TickConstriction(dt);
            TickThorns(dt);
            // Голодная и замёрзшая змея медленнее восстанавливает силы.
            _snake.Stamina.RegenMultiplier = Mathf.Lerp(_config.HungryStaminaRegen, 1f, Mathf.Clamp01(_survival.Satiety01 * 2f))
                                             * _snake.Temperature.RegenMultiplier * _upgrades.StaminaRegenMultiplier;
            TickFoxScare();
            // Кладка зреет, пока змея её охраняет; вылупились — следующее поколение.
            if (_quest.TickHatch(dt))
            {
                HatchClutch();
                return;
            }
            if (_quest.Update(_snake.BodyLengthCells, _generationMolts))
            {
                SetNestVisuals(true, false);
                _audio.Play(SoundId.Goal);
                QuestAdvanced?.Invoke(_quest.Stage);
            }
            if (_snake.Spot == TemperatureSpot.SunStone && _snake.Temperature.Warm01 >= 0.75f) ReportGoal(GoalType.BaskStone);

            // Ночь в норе пролетает быстрее (голод при этом идёт).
            float timeScale = _snake.InBurrow && _dayNight.IsNight ? _config.BurrowNightTimeScale : 1f;
            if (!Mathf.Approximately(Time.timeScale, timeScale)) Time.timeScale = timeScale;

            if (RivalActive)
            {
                if (_rival.IsAlive)
                {
                    CheckSnakeCollisions();
                    // Длинная соперница смелее нападает, короткая осторожничает.
                    if (_rivalAI != null) _rivalAI.Boldness = Mathf.Clamp(_rival.BodyLengthCells / Mathf.Max(1f, _snake.BodyLengthCells), 0.5f, 1.8f);
                }
                else TickRivalRespawn(dt);
            }
            TickTrophy(dt);

            // Линька начинается на земле, не посреди атаки орла и не во время борьбы с добычей.
            if (_moltPending && _snake.IsAlive && _snake.IsGrounded && !_eagle.IsDiving && !_constriction.IsActive) BeginMolt();
        }

        /// <summary>Голод: сытость убывает (ночью быстрее); на нуле худеет тело, а потом идёт урон.</summary>
        private void TickHunger(float dt)
        {
            float hunger = _config.HungerPerSecond * _upgrades.HungerMultiplier;
            if (_dayNight.IsNight) hunger *= _config.NightHungerMultiplier;
            _survival.Tick(dt, hunger);
            if (!_survival.IsStarving) return;

            if (!_snake.IsAtMinLength) _snake.Shrink(_config.StarveShrinkPerSecond * dt);
            else DamagePlayer(_config.StarveDamagePerSecond * dt, DamageSource.Starvation);
        }

        /// <summary>Живой фон меню: змеи ездят сами и охотятся, урон и голод не считаются.</summary>
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

            if (RivalActive)
            {
                if (_rival.IsAlive) CheckSnakeCollisions();
                else TickRivalRespawn(dt);
            }
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

        /// <summary>Эффекты улучшений раздаём системам: им не нужно знать про UpgradeSet.</summary>
        private void ApplyUpgrades()
        {
            _snake.ExternalSpeedMultiplier = _upgrades.SpeedMultiplier;
            _snake.StrikeDistanceScale = _upgrades.StrikeDistanceMultiplier;
            _snake.StrikeCostScale = _upgrades.StrikeCostMultiplier;
            _scent.RadiusMultiplier = _upgrades.SmellRadiusMultiplier;
            _scent.CooldownMultiplier = _upgrades.SmellCooldownMultiplier;
            _wildlife.PlayerAwarenessMultiplier = _upgrades.AwarenessMultiplier;
            _snake.SetCoolRateScale(_upgrades.CoolRateMultiplier);
            _snake.SetAmbushDelayScale(_upgrades.AmbushDelayMultiplier);
            _snake.SwimSpeedScale = _upgrades.SwimSpeedMultiplier;
            _survival.RegenPerSecond = _config.RegenPerSecond * _upgrades.HealthRegenMultiplier;
            _constriction.Duration = _config.ConstrictDuration * _upgrades.ConstrictDurationMultiplier;
        }

        private void BeginMolt()
        {
            _moltPending = false;
            _moltProgress = Mathf.Max(0f, _moltProgress - _config.MoltGrowth);
            MoltChoiceCount = _upgrades.RollChoices(_moltChoices, _rng);
            // Все улучшения уже взяты: просто обновляем кожу без выбора.
            if (MoltChoiceCount == 0)
            {
                _survival.HealFull();
                _skinShed.Shed();
                _stats.Molts++;
                _generationMolts++;
                return;
            }
            _audio.Play(SoundId.Molt);
            SetState(GameState.Molting);
        }

        /// <summary>
        /// Голова одной змеи о тело другой. Броском — это укус: ранена та, в чьё тело попали.
        /// На шагу змеи просто упираются друг в друга и расходятся.
        /// </summary>
        private void CheckSnakeCollisions()
        {
            if (!_snake.IsAlive || !_rival.IsAlive) return;
            if (_rival.BodyHits(_snake.HeadPosition, _snake.HeadRadiusWorld, 0, out Vector3 hitOnRival))
                Bump(_snake, _rival, _snake.HeadPosition - hitOnRival);
            if (_snake.BodyHits(_rival.HeadPosition, _rival.HeadRadiusWorld, 0, out Vector3 hitOnPlayer))
                Bump(_rival, _snake, _rival.HeadPosition - hitOnPlayer);
        }

        private void Bump(SnakeController attacker, SnakeController victim, Vector3 away)
        {
            if (attacker.IsStriking)
            {
                attacker.NotifyStrikeHit();
                attacker.CancelStrike();
                bool hit = victim.TakeHit(_config.BodyDamage, DamageSource.Rival, -away);
                if (hit && attacker == _snake && State == GameState.Playing) OnRivalBitten();
            }
            attacker.PushAway(away, 0.12f * _grid.CellSize);
        }

        /// <summary>Игрок укусил соперницу: цель дня; соперница злится (или, если она куда меньше, уступает).</summary>
        private void OnRivalBitten()
        {
            ReportGoal(GoalType.BiteRival);
            if (_rivalAI == null || !_rival.IsAlive) return;
            if (_rival.BodyLengthCells < _snake.BodyLengthCells * 0.7f)
            {
                _rivalAI.Yield(_config.RivalYieldTime);
                News?.Invoke(GameNews.RivalYielded);
                return;
            }
            bool wasAngry = _rivalAI.IsGrudging;
            _rivalAI.SetGrudge(_config.RivalGrudgeTime);
            if (!wasAngry) News?.Invoke(GameNews.RivalAngered);
        }

        /// <summary>Здоровье соперницы растёт с её длиной: выросшая — крепче.</summary>
        private float RivalMaxHealthNow => _config.RivalMaxHealth
                                           + _config.RivalHealthPerCell * Mathf.Max(0f, _rival.BodyLengthCells - _config.StartLength);

        private void SpawnRival()
        {
            // Ставим соперницу подальше от игрока и разворачиваем к центру поля.
            _grid.ClearAll();
            _snake.MarkOccupiedCells();
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
            // После поражения соперница возвращается почти прежней — соперничество не обнуляется.
            bool returning = _rivalReturnLength > _config.StartLength && State == GameState.Playing;
            if (returning) _rival.SetLength(_rivalReturnLength);
            _rival.IsRunning = State == GameState.Playing || State == GameState.Menu;
            _rivalHealth = RivalMaxHealthNow;
            _rivalHitByPlayer = false;
            _rivalRespawnTimer = -1f;
            if (_rivalAI != null) _rivalAI.ResetMood();
            if (returning) News?.Invoke(GameNews.RivalReturned);
        }

        // ---------------- Урон и смерть ----------------

        private void DamagePlayer(float amount, DamageSource source)
        {
            if (State != GameState.Playing || _survival.IsDead) return;
            if (source != DamageSource.Starvation) amount *= _upgrades.DamageTakenMultiplier;
            bool dead = _survival.Damage(amount);
            PlayerDamaged?.Invoke(amount, source);
            if (!dead) return;
            _stats.Cause = source;
            _snake.Kill();
        }

        private void OnPlayerHurt(Vector3 position, float damage, DamageSource source)
        {
            // В меню змея только отскакивает — живой фон не умирает.
            if (State != GameState.Playing) return;
            // Раненая змея разжимает кольца.
            EndConstriction(false, true);
            Emit(_deathParticles, position, _deathParticleCount / 4);
            _audio.Play(SoundId.Hurt);
            _cameraRig.Shake(0.25f, 0.35f);
            // Откуда пришёл удар: орёл — сверху, известный толчок — против него, иначе — спереди (камень, бревно).
            Vector3 away = _snake.LastHitAway;
            Vector3 from = source == DamageSource.Eagle ? position + Vector3.up
                : away.sqrMagnitude > 0f ? position - away : position + _snake.Forward;
            PlayerDamagedFrom?.Invoke(damage, source, from);
            DamagePlayer(damage, source);
        }

        private void OnRivalHurt(Vector3 position, float damage, DamageSource source)
        {
            if (State != GameState.Playing) return;
            Emit(_deathParticles, position, _deathParticleCount / 6);
            _rivalHitByPlayer = source == DamageSource.Rival;
            _rivalHealth -= damage;
            if (_rivalHealth <= 0f) _rival.Kill();
        }

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
            if (State != GameState.Playing) return;
            _rivalReturnLength = Mathf.Max(_config.StartLength, _rival.BodyLengthCells * _config.RivalLengthKeep);
            if (_rivalAI != null) _rivalAI.ResetMood();
            // Победа в схватке — шаг к линьке.
            if (_rivalHitByPlayer) AddMoltProgress(_config.RivalDefeatMoltReward);
            _audio.Play(SoundId.Goal);
            News?.Invoke(GameNews.RivalDefeated);
        }

        private void AddMoltProgress(float share)
        {
            _moltProgress += _config.MoltGrowth * share;
            if (_moltProgress >= _config.MoltGrowth) _moltPending = true;
        }

        private void EndGame()
        {
            if (State == GameState.GameOver) return;
            // Жизнь кончилась — сохранение у норы больше не нужно.
            RunSaveStore.Clear();
            _stats.Length = _snake.BodyLengthCells;
            _stats.Days = _stats.Time / DayLengthSeconds;
            var entry = new LeaderboardEntry(Mathf.FloorToInt(_stats.Time), _stats.Prey, _stats.Length, _stats.Molts);
            LastRank = _leaderboard.Submit(entry);
            _audio.SetMusicPitch(_stats.Won ? 1.05f : 0.7f);
            _eagle.HuntingAllowed = false;
            SetState(GameState.GameOver);
        }

        // ---------------- Охота ----------------

        private void OnPreyCaught(SnakeController hunter, PreyKind kind, Vector3 position)
        {
            hunter.NotifyStrikeHit();
            PreyStats prey = _config.GetPrey(kind);
            bool big = kind != PreyKind.Beetle;

            if (hunter != _snake)
            {
                if (State == GameState.Playing && hunter == _rival && kind != PreyKind.Beetle && !_dayNight.IsNight) _contestRival++;
                bool capped = State == GameState.Menu && hunter.BodyLengthCells >= _menuMaxLength;
                if (!capped) hunter.Grow(prey.Growth);
                Emit(_eatParticles, position, _eatParticleCount / 2);
                return;
            }

            if (State == GameState.Menu)
            {
                if (_snake.BodyLengthCells < _menuMaxLength) _snake.Grow(prey.Growth);
                Emit(_eatParticles, position, _eatParticleCount / 2);
                return;
            }

            _survival.Feed(prey.Satiety * _upgrades.FeedMultiplier);
            _snake.Grow(prey.Growth);
            float digest = _config.DigestSize(kind);
            _snake.Digestion.Add(digest, _config.DigestSeconds * Mathf.Max(0.35f, digest));
            _moltProgress += prey.Growth;
            if (_moltProgress >= _config.MoltGrowth) _moltPending = true;
            _stats.Prey++;
            if (kind == PreyKind.Mouse) ReportGoal(GoalType.CatchMice);
            if (kind == PreyKind.Rabbit) ReportGoal(GoalType.ConstrictRabbit);
            if (kind == PreyKind.Bird && _snake.LastStrikeFromAmbush) ReportGoal(GoalType.AmbushBird);
            if (kind == PreyKind.Fish) ReportGoal(GoalType.CatchFish);
            if (kind != PreyKind.Beetle && !_dayNight.IsNight) _contestPlayer++;
            CheckStolenFromRival(position);

            Emit(_eatParticles, position, big ? _eatParticleCount : _eatParticleCount / 2);
            _audio.Play(big ? SoundId.Feast : SoundId.Catch);
            if (big) _cameraRig.Shake(0.08f, 0.2f);
            PreyCaught?.Invoke(kind);
        }

        /// <summary>Игрок поймал добычу, за которой рядом охотилась соперница: цель дня, а соперница обижена.</summary>
        private void CheckStolenFromRival(Vector3 position)
        {
            if (!RivalActive || !_rival.IsAlive || _rivalAI == null) return;
            if (_rivalAI.CurrentPrey == null || _rivalAI.CurrentPrey != _wildlife.LastCaught) return;
            Vector3 d = _rival.HeadPosition - position;
            d.y = 0f;
            if (d.magnitude > _config.StealGoalRange * _grid.CellSize) return;
            ReportGoal(GoalType.StealFromRival);
            News?.Invoke(GameNews.PreyStolen);
            if (!_rivalAI.IsYielding) _rivalAI.SetGrudge(_config.RivalGrudgeTime);
        }

        // ---------------- Трофей дня и состязание ----------------

        /// <summary>Днём, в заданную долю светлого времени, одна добыча становится трофеем — за ней охотятся обе змеи.</summary>
        private void TickTrophy(float dt)
        {
            if (_trophyDone || _dayNight.IsNight || _dayNight.PeriodProgress01 < _config.TrophyDayPoint) return;
            if (_trophyRetry > 0f)
            {
                _trophyRetry -= dt;
                return;
            }
            if (!_wildlife.TryMakeTrophy())
            {
                _trophyRetry = 5f;
                return;
            }
            _trophyDone = true;
            _audio.Play(SoundId.Goal);
            News?.Invoke(GameNews.TrophyAppeared);
        }

        private void OnTrophyCaught(SnakeController hunter)
        {
            if (State != GameState.Playing) return;
            if (hunter == _snake)
            {
                _survival.Feed(_config.MaxSatiety);
                AddMoltProgress(_config.TrophyMoltReward);
                News?.Invoke(GameNews.TrophyPlayer);
                return;
            }
            if (hunter == _rival)
            {
                _rival.Grow(_config.RivalWinGrowth);
                _rivalHealth = RivalMaxHealthNow;
                News?.Invoke(GameNews.TrophyRival);
            }
        }

        private void OnTrophyLost()
        {
            if (State == GameState.Playing) News?.Invoke(GameNews.TrophyLost);
        }

        /// <summary>Закат: итог дневного состязания с соперницей.</summary>
        private void JudgeContest()
        {
            if (!RivalActive || _contestPlayer + _contestRival == 0) return;
            if (_contestPlayer > _contestRival)
            {
                AddMoltProgress(_config.ContestMoltReward);
                _audio.Play(SoundId.Goal);
                News?.Invoke(GameNews.ContestWon);
            }
            else if (_contestRival > _contestPlayer)
            {
                // Соперница окрепла: выросла и залечилась.
                if (_rival.IsAlive)
                {
                    _rival.Grow(_config.RivalWinGrowth);
                    _rivalHealth = RivalMaxHealthNow;
                }
                News?.Invoke(GameNews.ContestLost);
            }
            else News?.Invoke(GameNews.ContestTie);
        }

        // ---------------- Шипение и «мёртвая змея» ----------------

        /// <summary>
        /// Шипение: лиса отступает; соперница поменьше (или любая, если змея защищает душимую добычу) уступает,
        /// а та, что крупнее, злится. Ближняя мелочь пугается — шипеть на охоте нельзя.
        /// </summary>
        private void OnHissed(Vector3 head)
        {
            if (State != GameState.Playing) return;
            _audio.Play(SoundId.Hiss);
            float radius = _config.HissRadius * _grid.CellSize;
            if (_fox.Flinch(head, radius, _config.FoxHissFlinch)) News?.Invoke(GameNews.FoxHissed);

            if (RivalActive && _rival.IsAlive && _rivalAI != null)
            {
                Vector3 d = _rival.HeadPosition - head;
                d.y = 0f;
                if (d.magnitude <= radius * 1.2f)
                {
                    bool defending = _constriction.IsActive;
                    if (defending || _rival.BodyLengthCells <= _snake.BodyLengthCells * 1.15f)
                    {
                        _rivalAI.Yield(_config.RivalYieldTime);
                        News?.Invoke(GameNews.RivalYielded);
                    }
                    else
                    {
                        bool wasAngry = _rivalAI.IsGrudging;
                        _rivalAI.SetGrudge(_config.RivalGrudgeTime);
                        if (!wasAngry) News?.Invoke(GameNews.RivalAngered);
                    }
                }
            }

            float rSqr = radius * radius;
            for (int i = 0; i < _wildlife.AnimalCount; i++)
            {
                PreyAnimal a = _wildlife.GetAnimal(i);
                if (!a.IsActive || a.IsHeld) continue;
                Vector3 d = a.Position - head;
                d.y = 0f;
                if (d.sqrMagnitude <= rSqr) a.Startle(head);
            }
        }

        private void OnFoxLostInterest()
        {
            if (State != GameState.Playing) return;
            ReportGoal(GoalType.PlayDead);
            News?.Invoke(GameNews.PredatorFooled);
        }

        // ---------------- Нора, нюх и сохранение ----------------

        /// <summary>E: в норе — выйти, у норы — войти, иначе нюх.</summary>
        private void Interact()
        {
            if (_snake.InBurrow)
            {
                ExitBurrow();
                return;
            }

            if (NearNest && _quest.Stage == QuestStage.Nest && !_constriction.IsActive)
            {
                TryLayEggs();
                return;
            }

            int burrow = NearbyBurrow;
            if (burrow >= 0 && !_constriction.IsActive && _snake.IsGrounded)
            {
                EnterBurrow(burrow, true);
                return;
            }

            if (_scent.TrySniff())
            {
                _headAnimator.Flick();
                _audio.Play(SoundId.Sniff);
            }
        }

        /// <summary>Кладка у гнезда: ночью и сытой — род продолжен, начинается охрана кладки; иначе объясняем, чего не хватает.</summary>
        private void TryLayEggs()
        {
            NestCheck check = NestStatus;
            if (check != NestCheck.Ok)
            {
                NestBlocked?.Invoke(check);
                return;
            }
            _quest.TryLay(_dayNight.IsNight, _survival.Satiety01);
            SetNestVisuals(false, true);
            ShowClutch(_quest.Eggs);
            SetRaid(true);
            _stats.Won = true;
            _audio.Play(SoundId.Molt);
            Emit(_moltParticles, _grid.NestPosition, _moltParticleCount);
            QuestAdvanced?.Invoke(_quest.Stage);
        }

        private void SetNestVisuals(bool beacon, bool eggs)
        {
            if (_nestBeacon != null && _nestBeacon.activeSelf != beacon) _nestBeacon.SetActive(beacon);
            if (_nestEggs != null && _nestEggs.activeSelf != eggs) _nestEggs.SetActive(eggs);
        }

        private void EnterBurrow(int index, bool save)
        {
            _burrowIndex = index;
            // Ночь, пережитая в норе, не засчитывается в цель «ночь вне норы».
            if (_dayNight.IsNight) _nightOutside = false;
            _snake.EnterBurrow(_grid.GetBurrow(index));
            _playerInput.MovementLocked = true;
            _scent.ResetScent();
            if (save) RunSaveStore.Save(BuildSave());
            _audio.Play(SoundId.Land);
            BurrowChanged?.Invoke(true);
        }

        private void ExitBurrow()
        {
            Vector3 pos = _grid.GetBurrow(_burrowIndex);
            Vector3 face = _grid.Center - pos;
            face.y = 0f;
            face = face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward;
            _snake.ExitBurrow(pos + face * (1.2f * _grid.CellSize), Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
            _playerInput.MovementLocked = false;
            Time.timeScale = 1f;
            _cameraRig.LookBehindSnake();
            BurrowChanged?.Invoke(false);
        }

        private RunSave BuildSave()
        {
            var save = new RunSave
            {
                Day = _dayNight.Day,
                Phase = _dayNight.Phase,
                Time = _stats.Time,
                Health = _survival.Health,
                MaxHealth = _survival.MaxHealth,
                Satiety = _survival.Satiety,
                Length = _snake.BodyLengthCells,
                MoltProgress = _moltProgress,
                Temperature = _snake.Temperature.Value01,
                Prey = _stats.Prey,
                Molts = _stats.Molts,
                EaglesEvaded = _stats.EaglesEvaded,
                FoxesScared = _stats.FoxesScared,
                Burrow = _burrowIndex,
                Quest = (int)_quest.Stage,
                Generation = _quest.Generation,
                GenerationMolts = _generationMolts,
                ClutchEggs = _quest.Eggs,
                HatchTime = _quest.HatchTime,
                Generations = _stats.Generations,
                Hatchlings = _stats.Hatchlings
            };
            _upgrades.CopyLevels(save.Upgrades);
            return save;
        }

        private void ApplySave(RunSave save)
        {
            _upgrades.SetLevels(save.Upgrades);
            ApplyUpgrades();
            _survival.Restore(save.Health, save.MaxHealth, save.Satiety);
            _snake.SetLength(save.Length);
            _snake.Temperature.Reset(save.Temperature);
            _moltProgress = save.MoltProgress;
            _stats.Time = save.Time;
            _stats.Days = save.Time / DayLengthSeconds;
            _stats.Prey = save.Prey;
            _stats.Molts = save.Molts;
            _stats.EaglesEvaded = save.EaglesEvaded;
            _stats.FoxesScared = save.FoxesScared;
            var stage = (QuestStage)Mathf.Clamp(save.Quest, 0, (int)QuestStage.Guard);
            _quest.Restore(stage, save.Generation, save.ClutchEggs, save.HatchTime);
            _generationMolts = save.GenerationMolts;
            _stats.Generations = save.Generations;
            _stats.Hatchlings = save.Hatchlings;
            _stats.Won = save.Generations > 0 || stage == QuestStage.Guard;
            bool guard = _quest.Stage == QuestStage.Guard;
            SetNestVisuals(_quest.Stage == QuestStage.Nest, guard);
            if (guard) ShowClutch(_quest.Eggs);
            SetRaid(guard);
        }

        // ---------------- Птичьи гнёзда, кладка и поколения ----------------

        private void OnEggEaten(Vector3 position)
        {
            if (State != GameState.Playing) return;
            _survival.Feed(_config.EggSatiety * _upgrades.FeedMultiplier);
            _snake.Grow(_config.EggGrowth);
            _moltProgress += _config.EggGrowth;
            if (_moltProgress >= _config.MoltGrowth) _moltPending = true;
            _stats.Eggs++;
            ReportGoal(GoalType.EatEggs);
            Emit(_eatParticles, position, _eatParticleCount / 2);
            _audio.Play(SoundId.Catch);
        }

        private void OnBirdPecked(Vector3 from)
        {
            if (State != GameState.Playing) return;
            _snake.TakeHit(_config.BirdPeckDamage, DamageSource.Bird, _snake.HeadPosition - from);
        }

        private void OnBirdScared()
        {
            if (State != GameState.Playing) return;
            _audio.Play(SoundId.Dash);
            BirdScared?.Invoke();
        }

        /// <summary>Лиса у кладки утащила яйцо; все яйца — кладка погибла, откладывать заново.</summary>
        private void OnFoxRaided()
        {
            if (State != GameState.Playing || _quest.Stage != QuestStage.Guard) return;
            bool lost = _quest.LoseEgg();
            ShowClutch(_quest.Eggs);
            _audio.Play(SoundId.FoxYip);
            if (!lost)
            {
                ClutchRaided?.Invoke(_quest.Eggs);
                return;
            }
            SetRaid(false);
            SetNestVisuals(true, false);
            ClutchLost?.Invoke();
            QuestAdvanced?.Invoke(_quest.Stage);
        }

        /// <summary>
        /// Кладка вылупилась: змеёныши расползаются, а игра продолжается одним из них — маленькой змейкой у гнезда
        /// с тем же набором улучшений. Цель следующего поколения — повзрослеть чуть сильнее.
        /// </summary>
        private void HatchClutch()
        {
            Vector3 nest = _grid.NestPosition;
            int hatched = _quest.Hatch();
            SetRaid(false);
            SetNestVisuals(false, false);
            _hatchlings.Release(nest, hatched);
            _stats.Generations++;
            _stats.Hatchlings += hatched;

            EndConstriction(false, false);
            _snake.SetLength(_config.StartLength);
            Vector3 face = _grid.Center - nest;
            face.y = 0f;
            face = face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward;
            _snake.ExitBurrow(nest + face * (0.8f * _grid.CellSize), Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
            _snake.Digestion.Reset();
            _survival.HealFull();
            _moltProgress = 0f;
            _moltPending = false;
            _generationMolts = 0;
            _cameraRig.LookBehindSnake();
            _audio.Play(SoundId.Goal);
            Emit(_moltParticles, nest, _moltParticleCount);
            GenerationStarted?.Invoke(_quest.Generation, hatched);
            QuestAdvanced?.Invoke(_quest.Stage);
        }

        /// <summary>Лиса идёт к кладке, пока её охраняют.</summary>
        private void SetRaid(bool on)
        {
            _fox.Raiding = on;
            _fox.RaidPoint = _grid.NestPosition;
            _fox.Eagerness = on ? _config.GuardFoxEagerness : 1f;
        }

        /// <summary>В гнезде видно столько яиц, сколько осталось в кладке.</summary>
        private void ShowClutch(int eggs)
        {
            if (_nestEggs == null) return;
            Transform t = _nestEggs.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                GameObject egg = t.GetChild(i).gameObject;
                bool visible = i < eggs;
                if (egg.activeSelf != visible) egg.SetActive(visible);
            }
        }

        // ---------------- Обвивание ----------------

        private void OnPreyGrabbed(PreyAnimal animal)
        {
            // На фоне меню душить некому — добыча сразу вырывается.
            if (State != GameState.Playing || _constriction.IsActive)
            {
                animal.Release(_snake.HeadPosition);
                return;
            }
            _heldPrey = animal;
            _constriction.Begin();
            _playerInput.MovementLocked = true;
            _snake.IsConstricting = true;
            _audio.Play(SoundId.Catch);
            _cameraRig.Shake(0.12f, 0.25f);
            ConstrictionStarted?.Invoke(animal.Kind);
        }

        /// <summary>Добыча в кольцах: держать F — шкала растёт и тратятся силы; кончились силы или отпустил надолго — вырвалась.</summary>
        private void TickConstriction(float dt)
        {
            if (!_constriction.IsActive) return;
            if (_heldPrey == null || !_heldPrey.IsActive || !_snake.IsAlive)
            {
                EndConstriction(false, false);
                return;
            }

            if (TickRivalSteal()) return;

            bool squeezing = GameInput.ConstrictHeld;
            if (squeezing)
            {
                _snake.Stamina.Drain(_config.ConstrictStaminaDrain * dt);
                _cameraRig.Shake(0.03f, 0.1f);
            }
            bool hasStamina = !_snake.Stamina.IsExhausted;
            Vector3 forward = _snake.Forward;
            _heldPrey.HoldAt(_snake.HeadPosition + forward * (0.75f * _grid.CellSize), _snake.Yaw + 180f);

            ConstrictResult result = _constriction.Tick(dt, squeezing, hasStamina);
            if (result == ConstrictResult.Won) EndConstriction(true, false);
            else if (result == ConstrictResult.Escaped) EndConstriction(false, true);
        }

        /// <summary>
        /// Пока змея душит добычу, соперница поблизости идёт её отбить. Дотянулась — добыча её.
        /// Отогнать можно шипением (Q). Возвращает true, если добычу отбили.
        /// </summary>
        private bool TickRivalSteal()
        {
            if (!RivalActive || !_rival.IsAlive || _rivalAI == null || _rivalAI.IsYielding) return false;
            Vector3 d = _rival.HeadPosition - _heldPrey.Position;
            d.y = 0f;
            float dist = d.magnitude;
            float cs = _grid.CellSize;
            if (_rivalAI.StealTarget != _heldPrey)
            {
                if (dist > 14f * cs) return false;
                _rivalAI.StealTarget = _heldPrey;
                News?.Invoke(GameNews.StealStarted);
            }
            if (dist > _config.RivalStealRange * cs) return false;

            PreyAnimal prey = _heldPrey;
            _heldPrey = null;
            _constriction.Cancel();
            _playerInput.MovementLocked = false;
            _snake.IsConstricting = false;
            _rivalAI.StealTarget = null;
            _wildlife.CompleteCatch(prey, _rival);
            _audio.Play(SoundId.Hurt);
            News?.Invoke(GameNews.RivalStole);
            return true;
        }

        /// <summary>Конец борьбы: задушила — поимка как обычно, иначе добыча вырывается и убегает.</summary>
        private void EndConstriction(bool won, bool notify)
        {
            PreyAnimal prey = _heldPrey;
            _heldPrey = null;
            bool wasActive = prey != null;
            _constriction.Cancel();
            _playerInput.MovementLocked = false;
            _snake.IsConstricting = false;
            if (_rivalAI != null) _rivalAI.StealTarget = null;
            if (!wasActive) return;

            if (won) _wildlife.CompleteCatch(prey, _snake);
            else prey.Release(_snake.HeadPosition);
            if (notify || won) ConstrictionEnded?.Invoke(won);
        }

        // ---------------- Орёл ----------------

        // В меню орёл только кружит, и звуков нет — фон не должен отвлекать.
        private void OnEagleScreeched()
        {
            if (State == GameState.Playing) _audio.Play(SoundId.EagleCry);
        }

        private void OnEagleDive()
        {
            if (State == GameState.Playing) _audio.Play(SoundId.EagleCry);
        }

        private void OnEagleStruck(Vector3 position)
        {
            if (State != GameState.Playing) return;
            _audio.Play(SoundId.EagleStrike);
            _cameraRig.Shake(0.5f, 0.6f);
            _snake.TakeHit(_config.EagleDamage, DamageSource.Eagle, Vector3.zero);
        }

        private void OnEagleMissed()
        {
            if (State != GameState.Playing) return;
            _stats.EaglesEvaded++;
            ReportGoal(GoalType.EvadeEagle);
            if (_snake.IsPlayingDead)
            {
                ReportGoal(GoalType.PlayDead);
                News?.Invoke(GameNews.PredatorFooled);
            }
        }

        // ---------------- Погода и ёж ----------------

        private void OnPricked()
        {
            if (State == GameState.Playing) HedgehogPricked?.Invoke();
        }

        private void OnHedgehogPushed(Vector3 position)
        {
            if (State == GameState.Playing) _audio.Play(SoundId.Land);
        }

        private void OnHedgehogSplashed(Vector3 position)
        {
            if (State != GameState.Playing) return;
            _audio.Play(SoundId.Splash);
            HedgehogSplashed?.Invoke();
        }

        private void OnMouseHid(Vector3 position)
        {
            if (State != GameState.Playing) return;
            Emit(_dustParticles, position, _dustParticleCount / 2);
        }

        // ---------------- Ежевика и осы ----------------

        /// <summary>
        /// Быстрое ползание по ежевике — колючки царапают; шагом и крадучись — нет. Бросок из засады в зарослях
        /// не наказывается, иначе колючки сбивали бы его и ежевика была бы бесполезна для охоты.
        /// </summary>
        private void TickThorns(float dt)
        {
            if (_thornTimer > 0f) _thornTimer -= dt;
            if (!_snake.InBramble || !_snake.IsHurrying || _snake.IsStriking || _thornTimer > 0f) return;
            _thornTimer = _config.ThornInterval;
            _snake.TakeHit(_config.ThornDamage, DamageSource.Thorns, Vector3.zero);
        }

        private void OnWaspsSwarmed(Vector3 nest)
        {
            if (State != GameState.Playing) return;
            _audio.Play(SoundId.Buzz);
            WaspsSwarmed?.Invoke();
        }

        private void OnWaspStung(Vector3 from)
        {
            if (State != GameState.Playing) return;
            _audio.Play(SoundId.Buzz);
            _snake.TakeHit(_config.WaspStingDamage, DamageSource.Wasps, _snake.HeadPosition - from);
        }

        /// <summary>Рой ужалил лису: засчитывается как отпугнутая — змея сама её сюда привела.</summary>
        private void OnFoxStung()
        {
            if (State != GameState.Playing) return;
            _stats.FoxesScared++;
            _audio.Play(SoundId.FoxYip);
            ReportGoal(GoalType.ScareFox);
            FoxStungByWasps?.Invoke();
        }

        private void OnRainStarted()
        {
            if (State == GameState.Playing) RainChanged?.Invoke(true);
        }

        private void OnRainStopped()
        {
            if (State == GameState.Playing) RainChanged?.Invoke(false);
        }

        // ---------------- Лиса ----------------

        private void OnFoxAppeared()
        {
            if (State == GameState.Playing) _audio.Play(SoundId.FoxYip);
        }

        private void OnFoxBit(Vector3 position)
        {
            if (State != GameState.Playing) return;
            _cameraRig.Shake(0.3f, 0.4f);
            _snake.TakeHit(_config.FoxDamage, DamageSource.Fox, _snake.HeadPosition - _fox.Position);
        }

        /// <summary>Бросок в лису: при здоровье выше половины она убегает, иначе бросок лишь сбит.</summary>
        private void TickFoxScare()
        {
            if (!_snake.IsStriking || !_fox.IsActive) return;
            bool canScare = _survival.Health01 > _config.FoxScareHealth;
            Vector3 d = _fox.Position - _snake.HeadPosition;
            d.y = 0f;
            if (d.magnitude > 1.3f * _grid.CellSize) return;
            _snake.NotifyStrikeHit();
            _snake.CancelStrike();
            if (!_fox.TryScare(_snake.HeadPosition, canScare)) return;
            _stats.FoxesScared++;
            _audio.Play(SoundId.FoxYip);
            ReportGoal(GoalType.ScareFox);
            FoxScared?.Invoke();
        }

        // ---------------- Цели суток ----------------

        private void ReportGoal(GoalType type)
        {
            if (State != GameState.Playing) return;
            int index = _goals.Report(type);
            if (index < 0) return;
            if (_goals.Get(index).MoltReward) AddMoltProgress(_config.GoalMoltReward);
            else
            {
                _survival.HealFull();
                _snake.Stamina.Reset(_config.MaxStamina);
            }
            _audio.Play(SoundId.Goal);
            GoalCompleted?.Invoke(index);
        }

        // ---------------- День и ночь ----------------

        private void OnDayStarted(int day)
        {
            if (State == GameState.Playing)
            {
                _audio.Play(SoundId.Dawn);
                // Сначала засчитываем пережитую ночь, потом выдаём цели нового дня.
                if (_nightOutside) ReportGoal(GoalType.NightOutside);
                _goals.Roll(day, _rng, RivalActive);
                GoalsRolled?.Invoke();
            }
            // Новый день — новое состязание и новый трофей.
            _contestPlayer = 0;
            _contestRival = 0;
            _trophyDone = false;
            _trophyRetry = 0f;
            _audio.SetMusicPitch(1f);
        }

        private void OnNightStarted()
        {
            if (State == GameState.Playing)
            {
                _audio.Play(SoundId.Dusk);
                JudgeContest();
                // Трофей — дневная добыча: ночью его роль пропадает.
                if (_wildlife.Trophy != null)
                {
                    _wildlife.ClearTrophy();
                    News?.Invoke(GameNews.TrophyLost);
                }
            }
            _nightOutside = !_snake.InBurrow;
            // Ночью музыка чуть ниже и тягучее.
            _audio.SetMusicPitch(0.94f);
        }

        // ---------------- Движение змеи ----------------

        // На фоне меню змеи прыгают и делают броски сами — без звуков, тряски и затрат сытости.
        private void OnJumped()
        {
            if (State != GameState.Playing) return;
            _survival.Spend(_config.JumpHungerCost);
            _audio.Play(SoundId.Jump);
        }

        private void OnLanded(Vector3 position)
        {
            if (State == GameState.Playing) _audio.Play(SoundId.Land);
            Emit(_dustParticles, position, _dustParticleCount);
        }

        private void OnStrikeStarted(float charge)
        {
            if (State != GameState.Playing) return;
            _survival.Spend(_config.DashHungerCost * (1f + charge));
            _audio.Play(SoundId.Dash);
            _cameraRig.Shake(0.05f + 0.06f * charge, 0.2f);
        }

        private void ApplySettings()
        {
            _cameraRig.Sensitivity = _settings.MouseSensitivity;
            _cameraRig.InvertY = _settings.InvertY;
        }

        private static void Emit(ParticleSystem ps, Vector3 position, int count)
        {
            if (ps == null || count <= 0) return;
            ps.transform.position = position;
            ps.Emit(count);
        }
    }
}
