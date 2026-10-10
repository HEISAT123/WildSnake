using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Отображение: HUD выживания (сутки и часы, здоровье, сытость, линька, бросок, нюх, тревога об орле,
    /// укрытие, мини-карта, баннеры, вспышка урона) и экраны (меню, пауза, линька, итоги).
    /// Логики игры здесь нет — только реакция на события и вызов публичных методов GameManager по кнопкам.
    /// Тексты меняются, только когда меняется показываемое значение, — без мусора каждый кадр.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        private static readonly string[] UpgradeNames =
        {
            "ТОЛСТАЯ ЧЕШУЯ", "МЕДЛЕННЫЙ ОБМЕН", "ДЛИННЫЙ БРОСОК", "ОСТРЫЙ НЮХ",
            "БЕСШУМНАЯ ЧЕШУЯ", "КРЕПКОЕ СЕРДЦЕ", "ГИБКОЕ ТЕЛО", "СЫТНАЯ ОХОТА"
        };

        private static readonly string[] UpgradeDescriptions =
        {
            "Урон от ударов и когтей меньше на 20%",
            "Голод растёт на 15% медленнее",
            "Бросок длится на 25% дольше, перезарядка быстрее",
            "Нюх чует на 30% дальше и восстанавливается быстрее",
            "Добыча замечает змею на 15% позже",
            "Максимум здоровья больше на 20",
            "Змея ползает на 7% быстрее",
            "Добыча насыщает на 20% сильнее"
        };

        private static readonly string[] PreyNames = { "ЖУК", "МЫШЬ", "ЛЯГУШКА", "ПТИЦА" };

        // Подписи HUD разрежены тонким пробелом, как в разметке UIBuilder.
        private const string SatietyCaption = "С Ы Т О С Т Ь";
        private const string HungerCaption = "Г О Л О Д";

        [Header("Источники данных")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SnakeController _snake;
        [SerializeField] private SettingsService _settings;
        [SerializeField] private AudioService _audio;
        [SerializeField] private LeaderboardService _leaderboard;
        [SerializeField] private WildlifeManager _wildlife;
        [SerializeField] private EagleHunter _eagle;
        [SerializeField] private DayNightCycle _dayNight;
        [SerializeField] private ScentTracker _scent;

        [Tooltip("Камера: от её ориентации считается стрелка нюха")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Камера мини-карты")]
        [SerializeField] private Camera _minimapCamera;

        [Header("HUD")]
        [Tooltip("Корень HUD: показывается только во время партии")]
        [SerializeField] private GameObject _hudRoot;

        [SerializeField] private Text _dayText;
        [SerializeField] private Text _clockText;
        [SerializeField] private Image _periodFill;
        [SerializeField] private Image _healthFill;
        [SerializeField] private Text _healthValue;
        [SerializeField] private Image _satietyFill;
        [SerializeField] private Text _satietyValue;
        [SerializeField] private Text _satietyLabel;
        [SerializeField] private Image _moltFill;

        [Tooltip("Кольцо-компас нюха")]
        [SerializeField] private GameObject _scentPointer;

        [Tooltip("Поворачиваемая стрелка (смотрит вверх при нулевом повороте)")]
        [SerializeField] private RectTransform _scentArrow;

        [SerializeField] private Image _dashFill;
        [SerializeField] private Image _smellFill;

        [Tooltip("Метка «в траве»")]
        [SerializeField] private GameObject _hiddenBadge;

        [Header("Тревога")]
        [SerializeField] private CanvasGroup _threatGroup;
        [SerializeField] private Text _threatTitle;
        [SerializeField] private Text _threatHint;

        [Tooltip("Красная виньетка при уроне и низком здоровье")]
        [SerializeField] private CanvasGroup _damageFlash;

        [Tooltip("Рамка мини-карты")]
        [SerializeField] private GameObject _minimapRoot;

        [Header("Баннер")]
        [SerializeField] private CanvasGroup _bannerGroup;
        [SerializeField] private Text _bannerText;

        [Tooltip("Сколько баннер висит на экране, сек")]
        [SerializeField, Min(0.2f)] private float _bannerDuration = 2f;

        [Header("Цвета")]
        [SerializeField] private Color _boneColor = new Color(0.95f, 0.93f, 0.88f);
        [SerializeField] private Color _venomColor = new Color(0.78f, 0.96f, 0.2f);
        [SerializeField] private Color _emberColor = new Color(1f, 0.42f, 0.3f);
        [SerializeField] private Color _skyColor = new Color(0.45f, 0.75f, 1f);
        [SerializeField] private Color _captionColor = new Color(0.95f, 0.93f, 0.88f, 0.78f);

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
        [Tooltip("Значения статистики: добыча, длина, линьки, орлы, время")]
        [SerializeField] private Text[] _statValues = new Text[5];

        [Header("Кнопки меню")]
        [SerializeField] private Button _playButton;
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

        private float _bannerTimer;
        private float _flash;
        private float _startleBannerTimer;
        private int _shownDay = -1;
        private int _shownClock = -1;
        private int _shownHealth = -1;
        private int _shownSatiety = -1;
        private bool _shownStarving;
        private bool _shownNight;
        private int _shownThreat = -1;

        private void OnEnable()
        {
            _game.StateChanged += OnStateChanged;
            _game.PreyCaught += OnPreyCaught;
            _game.PlayerDamaged += OnPlayerDamaged;
            _game.MoltCompleted += OnMoltCompleted;
            _wildlife.Startled += OnPreyStartled;
            _eagle.Missed += OnEagleMissed;
            _dayNight.DayStarted += OnDayStarted;
            _dayNight.NightStarted += OnNightStarted;
            _scent.Sniffed += OnSniffed;
            _settings.Changed += ApplyMinimap;

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
            _game.PlayerDamaged -= OnPlayerDamaged;
            _game.MoltCompleted -= OnMoltCompleted;
            _wildlife.Startled -= OnPreyStartled;
            _eagle.Missed -= OnEagleMissed;
            _dayNight.DayStarted -= OnDayStarted;
            _dayNight.NightStarted -= OnNightStarted;
            _scent.Sniffed -= OnSniffed;
            _settings.Changed -= ApplyMinimap;

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
            _threatGroup.alpha = 0f;
            _damageFlash.alpha = 0f;
            _settingsPanel.SetActive(false);
            _leaderboardPanel.SetActive(false);
            _guidePanel.SetActive(false);
            OnStateChanged(_game.State);
            ApplyMinimap();
        }

        private void LateUpdate()
        {
            if (_game.State == GameState.Playing)
            {
                UpdateClock();
                UpdateVitals();
                UpdateAbilities();
                UpdateScentPointer();
                UpdateThreat();
            }
            UpdateFlash();
            UpdateBanner();
        }

        // ---------------- HUD ----------------

        /// <summary>Сутки и часы: текст меняется раз в десять игровых минут.</summary>
        private void UpdateClock()
        {
            int day = _dayNight.Day;
            if (day != _shownDay)
            {
                _shownDay = day;
                _dayText.text = day.ToString();
            }

            int clock = _dayNight.Hours * 6 + _dayNight.Minutes / 10;
            bool night = _dayNight.IsNight;
            if (clock != _shownClock || night != _shownNight)
            {
                _shownClock = clock;
                _shownNight = night;
                int h = _dayNight.Hours;
                int m = _dayNight.Minutes / 10 * 10;
                _clockText.text = (h < 10 ? "0" : "") + h + ":" + (m < 10 ? "0" : "") + m + "   " + PeriodName(h);
            }

            _periodFill.fillAmount = _dayNight.PeriodProgress01;
            _periodFill.color = night ? _skyColor : _venomColor;
        }

        private static string PeriodName(int hour)
        {
            if (hour >= 5 && hour < 11) return "УТРО";
            if (hour >= 11 && hour < 17) return "ДЕНЬ";
            if (hour >= 17 && hour < 21) return "ВЕЧЕР";
            return "НОЧЬ";
        }

        private void UpdateVitals()
        {
            SurvivalStats s = _game.Survival;
            _healthFill.fillAmount = s.Health01;
            _healthFill.color = s.Health01 < 0.3f ? _emberColor : _boneColor;
            int health = Mathf.CeilToInt(s.Health);
            if (health != _shownHealth)
            {
                _shownHealth = health;
                _healthValue.text = health.ToString();
            }

            _satietyFill.fillAmount = s.Satiety01;
            int satiety = Mathf.CeilToInt(s.Satiety);
            if (satiety != _shownSatiety)
            {
                _shownSatiety = satiety;
                _satietyValue.text = satiety.ToString();
            }

            bool starving = s.Satiety01 < 0.2f;
            if (starving != _shownStarving)
            {
                _shownStarving = starving;
                _satietyLabel.text = starving ? HungerCaption : SatietyCaption;
                _satietyLabel.color = starving ? _emberColor : _captionColor;
                _satietyFill.color = starving ? _emberColor : _venomColor;
                if (starving) ShowBanner("ПОРА НА ОХОТУ");
            }

            _moltFill.fillAmount = _game.MoltProgress01;
        }

        private void UpdateAbilities()
        {
            _dashFill.fillAmount = _snake.StrikeReady01;
            _smellFill.fillAmount = _scent.Charge01;
            bool hidden = _snake.IsHidden;
            if (_hiddenBadge.activeSelf != hidden) _hiddenBadge.SetActive(hidden);
        }

        private void UpdateScentPointer()
        {
            bool show = _scent.HasTarget;
            if (_scentPointer.activeSelf != show) _scentPointer.SetActive(show);
            if (!show) return;

            // Направление на добычу в осях камеры: 0° — прямо по взгляду, вправо — по часовой.
            Vector3 local = _cameraTransform.InverseTransformDirection(_scent.TargetPosition - _snake.HeadPosition);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            _scentArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        /// <summary>Тревога об орле: заголовок и совет меняются по фазе, надпись пульсирует.</summary>
        private void UpdateThreat()
        {
            int threat = !_eagle.IsThreatening ? 0 : _eagle.IsDiving ? 2 : 1;
            if (threat > 0 && _snake.IsHidden) threat += 2;
            if (threat != _shownThreat)
            {
                _shownThreat = threat;
                switch (threat)
                {
                    case 1:
                        _threatTitle.text = "ОРЁЛ В НЕБЕ";
                        _threatHint.text = "Уходите в высокую траву";
                        break;
                    case 2:
                        _threatTitle.text = "ОРЁЛ ПИКИРУЕТ";
                        _threatHint.text = "В траву — или бросок в последний миг";
                        break;
                    case 3:
                    case 4:
                        _threatTitle.text = "ОРЁЛ ВАС ПОТЕРЯЛ";
                        _threatHint.text = "Оставайтесь в траве";
                        break;
                }
                _threatTitle.color = threat >= 3 ? _venomColor : _emberColor;
            }

            float target = threat == 0 ? 0f : threat == 2 ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f)) : 1f;
            _threatGroup.alpha = Mathf.MoveTowards(_threatGroup.alpha, target, Time.unscaledDeltaTime * 4f);
        }

        /// <summary>Вспышка урона гаснет; при низком здоровье виньетка медленно пульсирует.</summary>
        private void UpdateFlash()
        {
            float dt = Time.unscaledDeltaTime;
            _flash = Mathf.Max(0f, _flash - dt * 1.6f);
            float pulse = 0f;
            if (_game.State == GameState.Playing && _game.Survival.Health01 < 0.3f)
                pulse = 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
            _damageFlash.alpha = Mathf.Max(_flash, pulse);
        }

        private void UpdateBanner()
        {
            float dt = Time.unscaledDeltaTime;
            if (_startleBannerTimer > 0f) _startleBannerTimer -= dt;
            if (_bannerTimer <= 0f) return;
            _bannerTimer -= dt;
            // Последние 0.4 сек плавно гаснет.
            _bannerGroup.alpha = Mathf.Clamp01(_bannerTimer / 0.4f);
        }

        private void ShowBanner(string text)
        {
            _bannerText.text = text;
            _bannerGroup.alpha = 1f;
            _bannerTimer = _bannerDuration;
        }

        private void ApplyMinimap()
        {
            bool show = _settings.ShowMinimap;
            _minimapRoot.SetActive(show);
            _minimapCamera.enabled = show;
        }

        // ---------------- События ----------------

        private void OnPreyCaught(PreyKind kind)
        {
            if (kind != PreyKind.Beetle) ShowBanner("+ " + PreyNames[(int)kind]);
        }

        private void OnPlayerDamaged(float amount, DamageSource source)
        {
            // Голод «капает» каждый кадр — вспышка только от настоящих ударов.
            if (source != DamageSource.Starvation) _flash = Mathf.Clamp01(0.35f + amount / 60f);
        }

        private void OnMoltCompleted(UpgradeType type)
        {
            ShowBanner(UpgradeNames[(int)type]);
        }

        private void OnPreyStartled(PreyKind kind, Vector3 position)
        {
            if (_game.State != GameState.Playing || _startleBannerTimer > 0f) return;
            _startleBannerTimer = 8f;
            ShowBanner("НУЖЕН БРОСОК  ·  ЛКМ");
        }

        private void OnEagleMissed()
        {
            if (_game.State == GameState.Playing) ShowBanner("ОРЁЛ ПРОМАХНУЛСЯ");
        }

        private void OnDayStarted(int day)
        {
            if (_game.State == GameState.Playing) ShowBanner("ДЕНЬ " + day);
        }

        private void OnNightStarted()
        {
            if (_game.State == GameState.Playing) ShowBanner("НАСТУПАЕТ НОЧЬ");
        }

        private void OnSniffed(bool found)
        {
            if (!found && _game.State == GameState.Playing) ShowBanner("ДОБЫЧИ РЯДОМ НЕТ");
        }

        private void OnStateChanged(GameState state)
        {
            // HUD только во время партии: на паузе, линьке и в итогах он мешал бы композиции экрана.
            _hudRoot.SetActive(state == GameState.Playing);
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
            _shownDay = -1;
            _shownClock = -1;
            _shownHealth = -1;
            _shownSatiety = -1;
            _shownThreat = -1;
            _shownStarving = false;
            _satietyLabel.text = SatietyCaption;
            _satietyLabel.color = _captionColor;
            _satietyFill.color = _venomColor;

            if (state == GameState.Molting) FillMolt();
            if (state == GameState.GameOver) FillGameOver();
            if (state != GameState.Playing)
            {
                _bannerTimer = 0f;
                _bannerGroup.alpha = 0f;
            }
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
                _moltDescriptions[i].text = UpgradeDescriptions[(int)type];
                _moltLevels[i].text = level == 0 ? "НОВОЕ" : "УР " + (level + 1) + " / " + UpgradeSet.MAX_LEVEL;
            }
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
            SetStat(1, s.Length.ToString("0.0"));
            SetStat(2, s.Molts.ToString());
            SetStat(3, s.EaglesEvaded.ToString());
            SetStat(4, TimeFormat.Clock(Mathf.FloorToInt(s.Time)));
        }

        private static string CauseTitle(DamageSource cause)
        {
            switch (cause)
            {
                case DamageSource.Starvation: return "ГОЛОД ВЗЯЛ СВОЁ";
                case DamageSource.Eagle: return "ДОБЫЧА ОРЛА";
                case DamageSource.Rival: return "ПОБЕЖДЕНА СОПЕРНИЦЕЙ";
                default: return "СМЕРТЕЛЬНАЯ РАНА";
            }
        }

        private void SetStat(int index, string value)
        {
            if (index < _statValues.Length && _statValues[index] != null) _statValues[index].text = value;
        }

        // ---------------- Кнопки ----------------

        private void OnPlayClicked() { Click(); _game.StartGame(); }
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
