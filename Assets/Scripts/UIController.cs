using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>Только отображение: HUD, меню, пауза, Game Over. Логики игры здесь нет.</summary>
    public class UIController : MonoBehaviour
    {
        [Header("Источники данных")]
        [Tooltip("Менеджер игры (состояния)")]
        [SerializeField] private GameManager _game;

        [Tooltip("Сервис счёта")]
        [SerializeField] private ScoreService _score;

        [Header("HUD")]
        [Tooltip("Текст текущего счёта")]
        [SerializeField] private Text _scoreText;

        [Tooltip("Текст рекорда")]
        [SerializeField] private Text _bestText;

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

        [Tooltip("Кнопка рестарта на экране Game Over")]
        [SerializeField] private Button _restartButton;

        private void OnEnable()
        {
            _score.ScoreChanged += OnScoreChanged;
            _game.StateChanged += OnStateChanged;
            _playButton.onClick.AddListener(OnPlayClicked);
            _resumeButton.onClick.AddListener(OnResumeClicked);
            _restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void OnDisable()
        {
            _score.ScoreChanged -= OnScoreChanged;
            _game.StateChanged -= OnStateChanged;
            _playButton.onClick.RemoveListener(OnPlayClicked);
            _resumeButton.onClick.RemoveListener(OnResumeClicked);
            _restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        private void Start()
        {
            OnScoreChanged(_score.Score, _score.Best);
            OnStateChanged(_game.State);
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
