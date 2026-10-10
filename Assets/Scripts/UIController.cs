using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Отображение: HUD (счёт, уровень, таймер, рывок, бонусы, стрелка на еду, мини-карта, баннеры)
    /// и экраны (меню, пауза, Game Over). Логики игры здесь нет — только реакция на события
    /// и вызов публичных методов GameManager по кнопкам.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        private static readonly string[] PowerUpNames = { "Ускорение", "Замедление", "Щит", "Магнит", "Очки x2" };
        private const int POWER_UP_COUNT = 5;

        [Header("Источники данных")]
        [SerializeField] private GameManager _game;
        [SerializeField] private ScoreService _score;
        [SerializeField] private SnakeController _snake;
        [SerializeField] private PowerUpService _powerUps;
        [SerializeField] private SettingsService _settings;
        [SerializeField] private AudioService _audio;
        [SerializeField] private ShrinkingArena _arena;
        [SerializeField] private FoodSpawner _food;

        [Tooltip("Камера: от её ориентации считается стрелка на еду")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Камера мини-карты")]
        [SerializeField] private Camera _minimapCamera;

        [Header("HUD")]
        [Tooltip("Корень HUD: в главном меню скрыт, чтобы фон был чистым")]
        [SerializeField] private GameObject _hudRoot;

        [SerializeField] private Text _scoreText;
        [SerializeField] private Text _bestText;
        [SerializeField] private Text _levelText;
        [SerializeField] private GameObject _timerPlate;
        [SerializeField] private Text _timerText;

        [Tooltip("Плашка со стрелкой на еду")]
        [SerializeField] private GameObject _foodPointer;

        [Tooltip("Поворачиваемая стрелка (смотрит вверх при нулевом повороте)")]
        [SerializeField] private RectTransform _foodArrow;

        [Tooltip("Полоса готовности рывка (Image типа Filled)")]
        [SerializeField] private Image _dashFill;

        [Tooltip("Строки активных бонусов (порядок как в PowerUpType)")]
        [SerializeField] private GameObject[] _powerUpRows = new GameObject[POWER_UP_COUNT];

        [Tooltip("Полосы оставшегося времени бонусов (порядок как в PowerUpType)")]
        [SerializeField] private Image[] _powerUpFills = new Image[POWER_UP_COUNT];

        [Tooltip("Рамка мини-карты")]
        [SerializeField] private GameObject _minimapRoot;

        [Header("Баннер")]
        [SerializeField] private CanvasGroup _bannerGroup;
        [SerializeField] private Text _bannerText;

        [Tooltip("Сколько баннер висит на экране, сек")]
        [SerializeField, Min(0.2f)] private float _bannerDuration = 1.8f;

        [Header("Панели")]
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _leaderboardPanel;

        [Header("Game Over")]
        [SerializeField] private Text _gameOverTitle;
        [SerializeField] private Text _finalScoreText;
        [SerializeField] private Text _rankText;
        [SerializeField] private Text _statsText;

        [Header("Кнопки меню")]
        [SerializeField] private Button _classicButton;
        [SerializeField] private Button _timeAttackButton;
        [SerializeField] private Button _survivalButton;
        [SerializeField] private Button _menuLeaderboardButton;
        [SerializeField] private Button _menuSettingsButton;
        [SerializeField] private Button _quitButton;

        [Header("Кнопки паузы")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _pauseRestartButton;
        [SerializeField] private Button _pauseSettingsButton;
        [SerializeField] private Button _pauseMenuButton;

        [Header("Кнопки Game Over")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _gameOverMenuButton;

        private float _bannerTimer;
        private int _shownSecond = -1;

        private void OnEnable()
        {
            _score.ScoreChanged += OnScoreChanged;
            _game.StateChanged += OnStateChanged;
            _game.LevelChanged += OnLevelChanged;
            _powerUps.Activated += OnPowerUpActivated;
            _snake.ShieldUsed += OnShieldUsed;
            _arena.Shrunk += OnArenaShrunk;
            _settings.Changed += ApplyMinimap;

            _classicButton.onClick.AddListener(OnClassicClicked);
            _timeAttackButton.onClick.AddListener(OnTimeAttackClicked);
            _survivalButton.onClick.AddListener(OnSurvivalClicked);
            _menuLeaderboardButton.onClick.AddListener(OnLeaderboardClicked);
            _menuSettingsButton.onClick.AddListener(OnSettingsClicked);
            _quitButton.onClick.AddListener(OnQuitClicked);
            _resumeButton.onClick.AddListener(OnResumeClicked);
            _pauseRestartButton.onClick.AddListener(OnRestartClicked);
            _pauseSettingsButton.onClick.AddListener(OnSettingsClicked);
            _pauseMenuButton.onClick.AddListener(OnMenuClicked);
            _restartButton.onClick.AddListener(OnRestartClicked);
            _gameOverMenuButton.onClick.AddListener(OnMenuClicked);
        }

        private void OnDisable()
        {
            _score.ScoreChanged -= OnScoreChanged;
            _game.StateChanged -= OnStateChanged;
            _game.LevelChanged -= OnLevelChanged;
            _powerUps.Activated -= OnPowerUpActivated;
            _snake.ShieldUsed -= OnShieldUsed;
            _arena.Shrunk -= OnArenaShrunk;
            _settings.Changed -= ApplyMinimap;

            _classicButton.onClick.RemoveListener(OnClassicClicked);
            _timeAttackButton.onClick.RemoveListener(OnTimeAttackClicked);
            _survivalButton.onClick.RemoveListener(OnSurvivalClicked);
            _menuLeaderboardButton.onClick.RemoveListener(OnLeaderboardClicked);
            _menuSettingsButton.onClick.RemoveListener(OnSettingsClicked);
            _quitButton.onClick.RemoveListener(OnQuitClicked);

            // Возвращаем курсор, если UI выключили посреди партии (например, выход из Play Mode).
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            _resumeButton.onClick.RemoveListener(OnResumeClicked);
            _pauseRestartButton.onClick.RemoveListener(OnRestartClicked);
            _pauseSettingsButton.onClick.RemoveListener(OnSettingsClicked);
            _pauseMenuButton.onClick.RemoveListener(OnMenuClicked);
            _restartButton.onClick.RemoveListener(OnRestartClicked);
            _gameOverMenuButton.onClick.RemoveListener(OnMenuClicked);
        }

        private void Start()
        {
            _bannerGroup.alpha = 0f;
            _settingsPanel.SetActive(false);
            _leaderboardPanel.SetActive(false);
            OnScoreChanged(_score.Score, _score.Best);
            OnLevelChanged(_game.Level);
            OnStateChanged(_game.State);
            ApplyMinimap();
        }

        private void LateUpdate()
        {
            UpdateFoodPointer();
            UpdateTimer();
            UpdateBars();
            UpdateBanner();
        }

        // ---------------- HUD ----------------

        private void UpdateFoodPointer()
        {
            bool playing = _game.State == GameState.Playing || _game.State == GameState.Paused;
            bool show = playing && _food.HasFood;
            if (_foodPointer.activeSelf != show) _foodPointer.SetActive(show);
            if (!show) return;

            // Направление на еду в осях камеры: 0° — прямо по взгляду, вправо — по часовой.
            Vector3 local = _cameraTransform.InverseTransformDirection(_food.Position - _snake.HeadPosition);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            _foodArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        /// <summary>Текст таймера меняется раз в секунду, а не каждый кадр.</summary>
        private void UpdateTimer()
        {
            bool show = _game.IsTimed && _game.State != GameState.Menu;
            if (_timerPlate.activeSelf != show) _timerPlate.SetActive(show);
            if (!show) return;

            int seconds = Mathf.CeilToInt(_game.TimeLeft);
            if (seconds == _shownSecond) return;
            _shownSecond = seconds;
            _timerText.text = FormatTime(seconds);
            _timerText.color = seconds <= 10 ? new Color(1f, 0.45f, 0.4f) : Color.white;
        }

        private void UpdateBars()
        {
            _dashFill.fillAmount = _snake.DashCharge01;

            for (int i = 0; i < POWER_UP_COUNT; i++)
            {
                bool shield = i == (int)PowerUpType.Shield;
                bool active = shield ? _snake.HasShield : _powerUps.IsActive((PowerUpType)i);
                if (_powerUpRows[i].activeSelf != active) _powerUpRows[i].SetActive(active);
                if (active) _powerUpFills[i].fillAmount = shield ? 1f : _powerUps.Remaining01((PowerUpType)i);
            }
        }

        private void UpdateBanner()
        {
            if (_bannerTimer <= 0f) return;
            _bannerTimer -= Time.unscaledDeltaTime;
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

        // Тексты меняются только по событию, а не каждый кадр, поэтому строки не мусорят.
        private void OnScoreChanged(int score, int best)
        {
            _scoreText.text = "Счёт: " + score;
            _bestText.text = "Рекорд: " + best;
        }

        private void OnLevelChanged(int level)
        {
            _levelText.text = "Уровень " + level;
            if (level > 1 && _game.State == GameState.Playing) ShowBanner("Уровень " + level + "!");
        }

        private void OnPowerUpActivated(PowerUpType type)
        {
            ShowBanner(PowerUpNames[(int)type] + "!");
        }

        private void OnShieldUsed(Vector3 position)
        {
            ShowBanner("Щит спас!");
        }

        private void OnArenaShrunk()
        {
            ShowBanner("Арена сужается!");
        }

        private void OnStateChanged(GameState state)
        {
            _hudRoot.SetActive(state != GameState.Menu);
            _menuPanel.SetActive(state == GameState.Menu);
            _pausePanel.SetActive(state == GameState.Paused);
            _gameOverPanel.SetActive(state == GameState.GameOver);
            if (state == GameState.Playing)
            {
                _settingsPanel.SetActive(false);
                _leaderboardPanel.SetActive(false);
            }

            ApplyCursor(state);

            // Снимаем выделение с кнопок: иначе Enter/Space сработает и как Submit, и как клавиша игры.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _shownSecond = -1;

            if (state == GameState.GameOver) FillGameOver();
        }

        /// <summary>
        /// Экран итогов: крупный счёт, место в таблице и столбец значений статистики
        /// (подписи к ним статичны и лежат рядом в разметке).
        /// </summary>
        private void FillGameOver()
        {
            bool timeUp = _game.IsTimed && _game.TimeLeft <= 0f;
            _gameOverTitle.text = timeUp ? "Время вышло" : "Игра окончена";
            _finalScoreText.text = _score.Score.ToString();

            int rank = _game.LastRank;
            _rankText.text = rank == 0 ? "Новый рекорд!" : rank > 0 ? "Место " + (rank + 1) + " в таблице рекордов" : "Рекорд: " + _score.Best;

            RunStats s = _game.Stats;
            _statsText.text =
                s.Length.ToString("0.0") + "\n" +
                s.Apples + (s.Golden > 0 || s.Rotten > 0 ? "  (золотых " + s.Golden + ", гнилых " + s.Rotten + ")" : "") + "\n" +
                s.PowerUps + "\n" +
                s.MaxLevel + "\n" +
                FormatTime(Mathf.FloorToInt(s.Time));
        }

        private static string FormatTime(int totalSeconds)
        {
            int m = totalSeconds / 60;
            int sec = totalSeconds % 60;
            return m + ":" + (sec < 10 ? "0" : "") + sec;
        }

        // ---------------- Кнопки ----------------

        private void OnClassicClicked() { Click(); _game.StartGame(GameMode.Classic); }
        private void OnTimeAttackClicked() { Click(); _game.StartGame(GameMode.TimeAttack); }
        private void OnSurvivalClicked() { Click(); _game.StartGame(GameMode.Survival); }
        private void OnResumeClicked() { Click(); _game.Resume(); }
        private void OnRestartClicked() { Click(); _game.Restart(); }
        private void OnMenuClicked() { Click(); _game.OpenMenu(); }
        private void OnSettingsClicked() { Click(); _settingsPanel.SetActive(true); }
        private void OnLeaderboardClicked() { Click(); _leaderboardPanel.SetActive(true); }

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

        /// <summary>Во время партии курсор скрыт и зафиксирован в центре окна, в меню и на паузе — виден.</summary>
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
