using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Отображение: экраны (меню, пауза, линька, итоги) и «тихий» HUD. На экране во время игры только то, что
    /// важно сейчас: дуги показателей, дуга солнца, подсказки и метки угроз — это отдельные компоненты
    /// (HudVitals, SunDial, ContextHints, ThreatIndicator, CenterReticle, MapJournal, ScreenVignettes).
    /// Здесь — очередь баннеров (не больше одного одновременно), режим «без интерфейса» (H), курсор и кнопки.
    /// Логики игры здесь нет — только реакция на события и вызов публичных методов GameManager.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        private const int BANNER_QUEUE = 4;

        private static readonly string[] UpgradeNames =
        {
            "ТОЛСТАЯ ЧЕШУЯ", "МЕДЛЕННЫЙ ОБМЕН", "ДЛИННЫЙ БРОСОК", "ОСТРЫЙ НЮХ",
            "БЕСШУМНАЯ ЧЕШУЯ", "КРЕПКОЕ СЕРДЦЕ", "ГИБКОЕ ТЕЛО", "СЫТНАЯ ОХОТА",
            "ГЛУБОКОЕ ДЫХАНИЕ", "ТЁПЛАЯ КРОВЬ", "СТАЛЬНАЯ ХВАТКА", "ТЕРПЕЛИВЫЙ ОХОТНИК", "ПЛОВЕЦ"
        };

        // {0} — эффект следующего уровня (с прошлым значением в скобках). Плата сильных улучшений — после точки.
        private static readonly string[] UpgradeDescriptions =
        {
            "Урон от ударов, когтей и зубов меньше на {0}. Плата: скорость −2% за уровень",
            "Голод растёт на {0} медленнее. Плата: заживление −10% за уровень",
            "Бросок летит на {0} дальше и стоит меньше сил",
            "Нюх чует на {0} дальше и перезаряжается быстрее",
            "Добыча замечает змею на {0} позже",
            "Максимум здоровья больше на {0}. Плата: голод +4% за уровень",
            "Змея ползает на {0} быстрее. Плата: голод +4% за уровень",
            "Добыча насыщает на {0} сильнее",
            "Выносливость восстанавливается на {0} быстрее",
            "Тело остывает на {0} медленнее: ночь и дождь не так страшны",
            "Кролик сдаётся в кольцах на {0} быстрее",
            "Засада наступает на {0} быстрее",
            "В воде змея плывёт на {0} быстрее"
        };

        [Header("Источники данных")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SettingsService _settings;
        [SerializeField] private AudioService _audio;
        [SerializeField] private LeaderboardService _leaderboard;
        [SerializeField] private EagleHunter _eagle;
        [SerializeField] private DayNightCycle _dayNight;
        [SerializeField] private ScentTracker _scent;

        [Header("HUD")]
        [Tooltip("Корень HUD: виден только во время партии и если интерфейс не выключен (H)")]
        [SerializeField] private GameObject _hudRoot;

        [Tooltip("Виньетки состояния: видны во время партии всегда, даже без интерфейса")]
        [SerializeField] private GameObject _vignetteRoot;

        [Header("Баннер")]
        [SerializeField] private CanvasGroup _bannerGroup;
        [SerializeField] private Text _bannerText;

        [Tooltip("Сколько баннер висит на экране, сек")]
        [SerializeField, Min(0.2f)] private float _bannerDuration = 2f;

        [Header("Панели")]
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _moltPanel;
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _leaderboardPanel;
        [SerializeField] private GameObject _guidePanel;

        [Header("Линька")]
        [SerializeField] private Button[] _moltButtons = new Button[GameManager.MOLT_CHOICES];
        [SerializeField] private Text[] _moltLabels = new Text[GameManager.MOLT_CHOICES];
        [SerializeField] private Text[] _moltDescriptions = new Text[GameManager.MOLT_CHOICES];
        [SerializeField] private Text[] _moltLevels = new Text[GameManager.MOLT_CHOICES];

        [Header("Итоги")]
        [SerializeField] private Text _gameOverTitle;
        [SerializeField] private Text _daysText;
        [SerializeField] private Text _rankText;
        [Tooltip("Значения статистики: добыча, длина, линьки, хищники, время")]
        [SerializeField] private Text[] _statValues = new Text[5];

        [Header("Кнопки меню")]
        [SerializeField] private Button _playButton;

        [Tooltip("Подпись и описание пункта «Играть»: при сохранении у норы — «Продолжить»")]
        [SerializeField] private Text _playLabel;
        [SerializeField] private Text _playDescription;
        [SerializeField] private Button _guideButton;
        [SerializeField] private Button _menuLeaderboardButton;
        [SerializeField] private Button _menuSettingsButton;
        [SerializeField] private Button _quitButton;

        [Header("Кнопки паузы")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _pauseRestartButton;
        [SerializeField] private Button _pauseSettingsButton;
        [SerializeField] private Button _pauseMenuButton;

        [Header("Кнопки итогов")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _gameOverMenuButton;

        // Очередь баннеров — кольцевой буфер строк.
        private readonly string[] _bannerQueue = new string[BANNER_QUEUE];
        private int _bannerHead;
        private int _bannerCount;
        private float _bannerTimer;
        private bool _hungerWarned;

        private void OnEnable()
        {
            _game.StateChanged += OnStateChanged;
            _game.PreyCaught += OnPreyCaught;
            _game.MoltCompleted += OnMoltCompleted;
            _game.ConstrictionEnded += OnConstrictionEnded;
            _game.BurrowChanged += OnBurrowChanged;
            _game.GoalCompleted += OnGoalCompleted;
            _game.FoxScared += OnFoxScared;
            _game.QuestAdvanced += OnQuestAdvanced;
            _game.HedgehogPricked += OnHedgehogPricked;
            _game.RainChanged += OnRainChanged;
            _game.NestBlocked += OnNestBlocked;
            _game.ClutchRaided += OnClutchRaided;
            _game.ClutchLost += OnClutchLost;
            _game.GenerationStarted += OnGenerationStarted;
            _game.BirdScared += OnBirdScared;
            _eagle.Missed += OnEagleMissed;
            _dayNight.DayStarted += OnDayStarted;
            _dayNight.NightStarted += OnNightStarted;
            _scent.Sniffed += OnSniffed;
            _settings.Changed += ApplyHudVisibility;

            _playButton.onClick.AddListener(OnPlayClicked);
            _guideButton.onClick.AddListener(OnGuideClicked);
            _menuLeaderboardButton.onClick.AddListener(OnLeaderboardClicked);
            _menuSettingsButton.onClick.AddListener(OnSettingsClicked);
            _quitButton.onClick.AddListener(OnQuitClicked);
            _resumeButton.onClick.AddListener(OnResumeClicked);
            _pauseRestartButton.onClick.AddListener(OnRestartClicked);
            _pauseSettingsButton.onClick.AddListener(OnSettingsClicked);
            _pauseMenuButton.onClick.AddListener(OnMenuClicked);
            _restartButton.onClick.AddListener(OnRestartClicked);
            _gameOverMenuButton.onClick.AddListener(OnMenuClicked);
            _moltButtons[0].onClick.AddListener(OnMolt0Clicked);
            _moltButtons[1].onClick.AddListener(OnMolt1Clicked);
            _moltButtons[2].onClick.AddListener(OnMolt2Clicked);
        }

        private void OnDisable()
        {
            _game.StateChanged -= OnStateChanged;
            _game.PreyCaught -= OnPreyCaught;
            _game.MoltCompleted -= OnMoltCompleted;
            _game.ConstrictionEnded -= OnConstrictionEnded;
            _game.BurrowChanged -= OnBurrowChanged;
            _game.GoalCompleted -= OnGoalCompleted;
            _game.FoxScared -= OnFoxScared;
            _game.QuestAdvanced -= OnQuestAdvanced;
            _game.HedgehogPricked -= OnHedgehogPricked;
            _game.RainChanged -= OnRainChanged;
            _game.NestBlocked -= OnNestBlocked;
            _game.ClutchRaided -= OnClutchRaided;
            _game.ClutchLost -= OnClutchLost;
            _game.GenerationStarted -= OnGenerationStarted;
            _game.BirdScared -= OnBirdScared;
            _eagle.Missed -= OnEagleMissed;
            _dayNight.DayStarted -= OnDayStarted;
            _dayNight.NightStarted -= OnNightStarted;
            _scent.Sniffed -= OnSniffed;
            _settings.Changed -= ApplyHudVisibility;

            _playButton.onClick.RemoveListener(OnPlayClicked);
            _guideButton.onClick.RemoveListener(OnGuideClicked);
            _menuLeaderboardButton.onClick.RemoveListener(OnLeaderboardClicked);
            _menuSettingsButton.onClick.RemoveListener(OnSettingsClicked);
            _quitButton.onClick.RemoveListener(OnQuitClicked);
            _resumeButton.onClick.RemoveListener(OnResumeClicked);
            _pauseRestartButton.onClick.RemoveListener(OnRestartClicked);
            _pauseSettingsButton.onClick.RemoveListener(OnSettingsClicked);
            _pauseMenuButton.onClick.RemoveListener(OnMenuClicked);
            _restartButton.onClick.RemoveListener(OnRestartClicked);
            _gameOverMenuButton.onClick.RemoveListener(OnMenuClicked);
            _moltButtons[0].onClick.RemoveListener(OnMolt0Clicked);
            _moltButtons[1].onClick.RemoveListener(OnMolt1Clicked);
            _moltButtons[2].onClick.RemoveListener(OnMolt2Clicked);

            // Возвращаем курсор, если UI выключили посреди партии (например, выход из Play Mode).
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Start()
        {
            _bannerGroup.alpha = 0f;
            _settingsPanel.SetActive(false);
            _leaderboardPanel.SetActive(false);
            _guidePanel.SetActive(false);
            OnStateChanged(_game.State);
        }

        private void Update()
        {
            if (_game.State == GameState.Playing && GameInput.ToggleHud) _settings.SetShowHud(!_settings.ShowHud);
        }

        private void LateUpdate()
        {
            if (_game.State == GameState.Playing) WatchHunger();
            UpdateBanner();
        }

        // ---------------- HUD ----------------

        /// <summary>Голод напоминает о себе один раз, пока змея снова не наестся.</summary>
        private void WatchHunger()
        {
            float satiety = _game.Survival.Satiety01;
            if (!_hungerWarned && satiety < 0.2f)
            {
                _hungerWarned = true;
                ShowBanner("ПОРА НА ОХОТУ");
            }
            else if (_hungerWarned && satiety > 0.35f)
            {
                _hungerWarned = false;
            }
        }

        private void ApplyHudVisibility()
        {
            bool playing = _game.State == GameState.Playing;
            bool hud = playing && _settings.ShowHud;
            if (_hudRoot.activeSelf != hud) _hudRoot.SetActive(hud);
            if (_vignetteRoot.activeSelf != playing) _vignetteRoot.SetActive(playing);
        }

        // ---------------- Баннеры ----------------

        /// <summary>В очередь: одновременно на экране не больше одного баннера.</summary>
        private void ShowBanner(string text)
        {
            if (_game.State != GameState.Playing) return;
            // Тот же текст уже ждёт в очереди или на экране — не дублируем.
            for (int i = 0; i < _bannerCount; i++)
                if (_bannerQueue[(_bannerHead + i) % BANNER_QUEUE] == text) return;
            if (_bannerTimer > 0f && _bannerText.text == text) return;
            if (_bannerCount == BANNER_QUEUE) return;
            _bannerQueue[(_bannerHead + _bannerCount) % BANNER_QUEUE] = text;
            _bannerCount++;
        }

        private void UpdateBanner()
        {
            float dt = Time.unscaledDeltaTime;
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= dt;
                float shown = _bannerDuration - _bannerTimer;
                // Быстро проявляется, последние 0.4 сек плавно гаснет.
                _bannerGroup.alpha = Mathf.Min(Mathf.Clamp01(shown / 0.15f), Mathf.Clamp01(_bannerTimer / 0.4f));
                return;
            }

            _bannerGroup.alpha = 0f;
            if (_bannerCount == 0) return;
            _bannerText.text = _bannerQueue[_bannerHead];
            _bannerQueue[_bannerHead] = null;
            _bannerHead = (_bannerHead + 1) % BANNER_QUEUE;
            _bannerCount--;
            _bannerTimer = _bannerDuration;
        }

        private void ClearBanners()
        {
            _bannerCount = 0;
            _bannerTimer = 0f;
            _bannerGroup.alpha = 0f;
        }

        // ---------------- События ----------------

        // Мелкая добыча обходится без баннера — только крупная.
        private void OnPreyCaught(PreyKind kind)
        {
            if (kind == PreyKind.Rabbit) ShowBanner("+ КРОЛИК");
            else if (kind == PreyKind.Hedgehog) ShowBanner("+ ЁЖ");
            else if (kind == PreyKind.Bird) ShowBanner("+ ПТИЦА");
            else if (kind == PreyKind.Fish) ShowBanner("+ РЫБА");
        }

        private void OnMoltCompleted(UpgradeType type)
        {
            ShowBanner(UpgradeNames[(int)type]);
        }

        private void OnConstrictionEnded(bool won)
        {
            if (!won) ShowBanner("ВЫРВАЛСЯ");
        }

        private void OnBurrowChanged(bool entered)
        {
            if (!entered) return;
            ShowBanner(_dayNight.IsNight ? "НОРА · НОЧЬ ПРОЙДЁТ БЫСТРЕЕ" : "НОРА · ПРОГРЕСС СОХРАНЁН");
        }

        private void OnGoalCompleted(int index)
        {
            ShowBanner("ЦЕЛЬ ДНЯ · " + MapJournal.GoalName(_game.Goals.Get(index).Type).ToUpperInvariant());
        }

        private void OnQuestAdvanced(QuestStage stage)
        {
            if (stage == QuestStage.Nest && _game.Quest.Eggs == 0 && _game.State == GameState.Playing && !_clutchJustLost)
                ShowBanner("ВЗРОСЛАЯ ЗМЕЯ · ГНЕЗДО ОТМЕЧЕНО НА КАРТЕ");
            else if (stage == QuestStage.Guard) ShowBanner("КЛАДКА · ОХРАНЯЙТЕ ЯЙЦА ОТ ЛИСЫ");
            _clutchJustLost = false;
        }

        private bool _clutchJustLost;

        private void OnClutchRaided(int left)
        {
            ShowBanner("ЛИСА УТАЩИЛА ЯЙЦО · ОСТАЛОСЬ " + left);
        }

        private void OnClutchLost()
        {
            _clutchJustLost = true;
            ShowBanner("КЛАДКА ПОГИБЛА · ОТЛОЖИТЕ ЯЙЦА СНОВА");
        }

        private void OnGenerationStarted(int generation, int hatched)
        {
            ShowBanner("ПОКОЛЕНИЕ " + generation + " · ЗМЕЕНЫШЕЙ: " + hatched);
        }

        private void OnBirdScared()
        {
            ShowBanner("ПТИЦА ОТОГНАНА");
        }

        private void OnHedgehogPricked()
        {
            ShowBanner("ЁЖ КОЛЮЧИЙ — ЖДИТЕ, ПОКА РАЗВЕРНЁТСЯ");
        }

        private void OnRainChanged(bool raining)
        {
            ShowBanner(raining ? "ДОЖДЬ · УКРОЙТЕСЬ ПОД КРОНАМИ" : "ДОЖДЬ ПРОШЁЛ");
        }

        private void OnNestBlocked(NestCheck check)
        {
            if (check == NestCheck.NeedNight) ShowBanner("ЯЙЦА — ТОЛЬКО НОЧЬЮ");
            else if (check == NestCheck.NeedFood) ShowBanner("СНАЧАЛА НАЕШЬТЕСЬ");
        }

        private void OnFoxScared()
        {
            ShowBanner("ЛИСА УБЕЖАЛА");
        }

        private void OnEagleMissed()
        {
            ShowBanner("ОРЁЛ ПРОМАХНУЛСЯ");
        }

        private void OnDayStarted(int day)
        {
            ShowBanner("ДЕНЬ " + day);
        }

        private void OnNightStarted()
        {
            ShowBanner("НАСТУПАЕТ НОЧЬ");
        }

        private void OnSniffed(bool found)
        {
            if (!found) ShowBanner("ДОБЫЧИ РЯДОМ НЕТ");
        }

        private void OnStateChanged(GameState state)
        {
            // До показа меню: пункт «Играть» превращается в «Продолжить», если змея ждёт в норе.
            if (state == GameState.Menu) RefreshPlayRow();

            ApplyHudVisibility();
            _menuPanel.SetActive(state == GameState.Menu);
            _pausePanel.SetActive(state == GameState.Paused);
            _moltPanel.SetActive(state == GameState.Molting);
            _gameOverPanel.SetActive(state == GameState.GameOver);
            if (state == GameState.Playing)
            {
                _settingsPanel.SetActive(false);
                _leaderboardPanel.SetActive(false);
                _guidePanel.SetActive(false);
            }

            ApplyCursor(state);

            // Снимаем выделение с кнопок: иначе Enter/Space сработает и как Submit, и как клавиша игры.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (state == GameState.Menu || state == GameState.GameOver) _hungerWarned = false;

            if (state == GameState.Molting) FillMolt();
            if (state == GameState.GameOver) FillGameOver();
            if (state != GameState.Playing) ClearBanners();
        }

        private void FillMolt()
        {
            for (int i = 0; i < _moltButtons.Length; i++)
            {
                bool has = i < _game.MoltChoiceCount;
                _moltButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                UpgradeType type = _game.GetMoltChoice(i);
                int level = _game.Upgrades.Level(type);
                _moltLabels[i].text = UpgradeNames[(int)type];
                _moltDescriptions[i].text = string.Format(UpgradeDescriptions[(int)type], UpgradeEffect(type, level));
                _moltLevels[i].text = level == 0 ? "НОВОЕ" : "УР " + (level + 1) + " / " + UpgradeSet.MAX_LEVEL;
            }
        }

        /// <summary>Эффект следующего уровня («22% (было 12%)»): видно, что отдача убывает. Строится при открытии линьки.</summary>
        private static string UpgradeEffect(UpgradeType type, int level)
        {
            bool heart = type == UpgradeType.StrongHeart;
            string next = Mathf.RoundToInt(UpgradeSet.EffectPercent(type, level + 1)) + (heart ? "" : "%");
            if (level == 0) return next;
            return next + " (было " + Mathf.RoundToInt(UpgradeSet.EffectPercent(type, level)) + (heart ? "" : "%") + ")";
        }

        /// <summary>Итоги: отчего погибла, сколько суток прожила, место в таблице и строка статистики.</summary>
        private void FillGameOver()
        {
            RunStats s = _game.Stats;
            _gameOverTitle.text = CauseTitle(s.Cause);
            _daysText.text = TimeFormat.Days(s.Time, _game.DayLengthSeconds);

            int rank = _game.LastRank;
            _rankText.text = rank == 0 ? "НОВЫЙ РЕКОРД"
                : rank > 0 ? "МЕСТО " + (rank + 1) + " В ТАБЛИЦЕ"
                : "РЕКОРД  " + TimeFormat.Days(_leaderboard.BestSeconds, _game.DayLengthSeconds) + " СУТ.";

            SetStat(0, s.Prey.ToString());
            SetStat(1, (s.Generations + 1).ToString());
            SetStat(2, s.Molts.ToString());
            SetStat(3, (s.EaglesEvaded + s.FoxesScared).ToString());
            SetStat(4, TimeFormat.Clock(Mathf.FloorToInt(s.Time)));
        }

        private static string CauseTitle(DamageSource cause)
        {
            switch (cause)
            {
                case DamageSource.Starvation: return "ГОЛОД ВЗЯЛ СВОЁ";
                case DamageSource.Eagle: return "ДОБЫЧА ОРЛА";
                case DamageSource.Fox: return "ДОБЫЧА ЛИСЫ";
                case DamageSource.Hedgehog: return "ИСКОЛОТА ЁЖИКОМ";
                case DamageSource.Rival: return "ПОБЕЖДЕНА СОПЕРНИЦЕЙ";
                case DamageSource.Bird: return "ЗАКЛЁВАНА ПТИЦЕЙ";
                default: return "СМЕРТЕЛЬНАЯ РАНА";
            }
        }

        private void SetStat(int index, string value)
        {
            if (index < _statValues.Length && _statValues[index] != null) _statValues[index].text = value;
        }

        private void RefreshPlayRow()
        {
            if (_playLabel == null) return;
            bool hasSave = RunSaveStore.TryLoad(out RunSave save);
            _playLabel.text = hasSave ? "ПРОДОЛЖИТЬ" : "ИГРАТЬ";
            if (_playDescription != null) _playDescription.text = hasSave ? "день " + save.Day + " · новая жизнь — «заново» в паузе" : "";
        }

        // ---------------- Кнопки ----------------

        private void OnPlayClicked() { Click(); _game.PlayFromMenu(); }
        private void OnResumeClicked() { Click(); _game.Resume(); }
        private void OnRestartClicked() { Click(); _game.Restart(); }
        private void OnMenuClicked() { Click(); _game.OpenMenu(); }
        private void OnSettingsClicked() { Click(); _settingsPanel.SetActive(true); }
        private void OnLeaderboardClicked() { Click(); _leaderboardPanel.SetActive(true); }
        private void OnGuideClicked() { Click(); _guidePanel.SetActive(true); }
        private void OnMolt0Clicked() { Click(); _game.ChooseUpgrade(0); }
        private void OnMolt1Clicked() { Click(); _game.ChooseUpgrade(1); }
        private void OnMolt2Clicked() { Click(); _game.ChooseUpgrade(2); }

        private void OnQuitClicked()
        {
            Click();
#if UNITY_EDITOR
            // В редакторе Application.Quit ничего не делает — останавливаем Play Mode.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Во время партии курсор скрыт и зафиксирован в центре окна, в меню, на паузе и на линьке — виден.</summary>
        private static void ApplyCursor(GameState state)
        {
            bool playing = state == GameState.Playing;
            Cursor.visible = !playing;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        }

        // После Alt+Tab Windows возвращает курсор — снова прячем, если идёт игра.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) ApplyCursor(_game.State);
        }

        private void Click()
        {
            _audio.Play(SoundId.Click);
        }
    }
}
