using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Только отображение: HUD, стрелка на еду, меню, пауза, Game Over.
    /// Логики игры здесь нет.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        [Header("Источники данных")]
        [Tooltip("Менеджер игры (состояния)")]
        [SerializeField] private GameManager _game;

        [Tooltip("Сервис счёта")]
        [SerializeField] private ScoreService _score;

        [Tooltip("Камера: от её ориентации считается стрелка на еду")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Спавнер еды (есть ли сейчас еда)")]
        [SerializeField] private FoodSpawner _foodSpawner;

        [Tooltip("Объект еды")]
        [SerializeField] private Transform _food;

        [Tooltip("Голова змейки")]
        [SerializeField] private Transform _head;

        [Header("HUD")]
        [Tooltip("Текст текущего счёта")]
        [SerializeField] private Text _scoreText;

        [Tooltip("Текст рекорда")]
        [SerializeField] private Text _bestText;

        [Tooltip("Плашка со стрелкой на еду")]
        [SerializeField] private GameObject _foodPointer;

        [Tooltip("Поворачиваемая стрелка (смотрит вверх при нулевом повороте)")]
        [SerializeField] private RectTransform _foodArrow;

        [Header("Панели")]
        [Tooltip("Стартовое меню")]
        [SerializeField] private GameObject _menuPanel;

        [Tooltip("Экран паузы")]
        [SerializeField] private GameObject _pausePanel;

        [Tooltip("Экран Game Over")]
        [SerializeField] private GameObject _gameOverPanel;

        [Header("Game Over")]
        [Tooltip("Заголовок (проигрыш / победа)")]
        [SerializeField] private Text _gameOverTitle;

        [Tooltip("Итоговый счёт")]
        [SerializeField] private Text _finalScoreText;

        [Tooltip("Рекорд на экране Game Over")]
        [SerializeField] private Text _finalBestText;

        [Header("Кнопки")]
        [Tooltip("Кнопка «Играть» в меню")]
        [SerializeField] private Button _playButton;

        [Tooltip("Кнопка «Продолжить» на паузе")]
        [SerializeField] private Button _resumeButton;

        [Tooltip("Кнопка «Заново» на паузе")]
        [SerializeField] private Button _pauseRestartButton;

        [Tooltip("Кнопка рестарта на экране Game Over")]
        [SerializeField] private Button _restartButton;

        private void OnEnable()
        {
            _score.ScoreChanged += OnScoreChanged;
            _game.StateChanged += OnStateChanged;
            _playButton.onClick.AddListener(OnPlayClicked);
            _resumeButton.onClick.AddListener(OnResumeClicked);
            _pauseRestartButton.onClick.AddListener(OnRestartClicked);
            _restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void OnDisable()
        {
            _score.ScoreChanged -= OnScoreChanged;
            _game.StateChanged -= OnStateChanged;
            _playButton.onClick.RemoveListener(OnPlayClicked);
            _resumeButton.onClick.RemoveListener(OnResumeClicked);
            _pauseRestartButton.onClick.RemoveListener(OnRestartClicked);
            _restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        private void Start()
        {
            OnScoreChanged(_score.Score, _score.Best);
            OnStateChanged(_game.State);
        }

        private void LateUpdate()
        {
            bool show = _foodSpawner.HasFood && (_game.State == GameState.Playing || _game.State == GameState.Paused);
            if (_foodPointer.activeSelf != show) _foodPointer.SetActive(show);
            if (!show) return;

            // Направление на еду в осях камеры: 0° — прямо по взгляду, вправо — по часовой.
            Vector3 local = _cameraTransform.InverseTransformDirection(_food.position - _head.position);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            _foodArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        // Тексты меняются только по событию, а не каждый кадр, поэтому строки не мусорят.
        private void OnScoreChanged(int score, int best)
        {
            _scoreText.text = "Счёт: " + score;
            _bestText.text = "Рекорд: " + best;
        }

        private void OnStateChanged(GameState state)
        {
            _menuPanel.SetActive(state == GameState.Menu);
            _pausePanel.SetActive(state == GameState.Paused);
            _gameOverPanel.SetActive(state == GameState.GameOver);

            // Снимаем выделение с кнопок: иначе Enter/Space сработает и как Submit, и как клавиша игры.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            if (state == GameState.GameOver)
            {
                _gameOverTitle.text = _game.LastGameWon ? "Победа!" : "Игра окончена";
                _finalScoreText.text = "Счёт: " + _score.Score;
                _finalBestText.text = _score.IsNewBest ? "Новый рекорд: " + _score.Best : "Рекорд: " + _score.Best;
            }
        }

        private void OnPlayClicked() => _game.StartGame();
        private void OnResumeClicked() => _game.Resume();
        private void OnRestartClicked() => _game.Restart();
    }
}
