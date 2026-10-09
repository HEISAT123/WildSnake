using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>Состояния игры и связка систем: змейка, еда, счёт, эффекты.</summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Змейка")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Спавнер еды")]
        [SerializeField] private FoodSpawner _food;

        [Tooltip("Счёт и рекорд")]
        [SerializeField] private ScoreService _score;

        [Header("Эффекты")]
        [Tooltip("Частицы при поедании")]
        [SerializeField] private ParticleSystem _eatParticles;

        [Tooltip("Сколько частиц выпустить при поедании")]
        [SerializeField, Min(0)] private int _eatParticleCount = 24;

        [Tooltip("Частицы при смерти")]
        [SerializeField] private ParticleSystem _deathParticles;

        [Tooltip("Сколько частиц выпустить при смерти")]
        [SerializeField, Min(0)] private int _deathParticleCount = 70;

        [Header("Старт")]
        [Tooltip("Начинать игру сразу, минуя меню")]
        [SerializeField] private bool _skipMenu;

        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Menu;
        public bool LastGameWon { get; private set; }

        private void OnEnable()
        {
            _snake.FoodEaten += OnFoodEaten;
            _snake.Died += OnSnakeDied;
        }

        private void OnDisable()
        {
            _snake.FoodEaten -= OnFoodEaten;
            _snake.Died -= OnSnakeDied;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            PrepareRound();
            if (_skipMenu) SetState(GameState.Playing);
            else SetState(GameState.Menu);
        }

        private void Update()
        {
            switch (State)
            {
                case GameState.Menu:
                    if (GameInput.Confirm) StartGame();
                    break;
                case GameState.Playing:
                    if (GameInput.Pause) SetState(GameState.Paused);
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

        public void StartGame()
        {
            if (State == GameState.Menu) SetState(GameState.Playing);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Restart()
        {
            // Рекорд мог вырасти в прерванной партии (рестарт с паузы).
            _score.SaveBest();
            PrepareRound();
            SetState(GameState.Playing);
        }

        private void OnApplicationQuit()
        {
            _score.SaveBest();
        }

        private void PrepareRound()
        {
            LastGameWon = false;
            _snake.ResetSnake();
            _score.ResetScore();
            _food.Spawn();
        }

        private void SetState(GameState state)
        {
            State = state;
            _snake.IsRunning = state == GameState.Playing;
            // Пауза через timeScale замораживает и шаги змейки, и частицы, и пульсацию еды.
            Time.timeScale = state == GameState.Paused ? 0f : 1f;
            StateChanged?.Invoke(state);
        }

        private void OnFoodEaten(Vector3 position)
        {
            _score.AddFood();
            Emit(_eatParticles, position, _eatParticleCount);
            if (!_food.Spawn()) EndGame(true); // свободных клеток нет — поле заполнено
        }

        private void OnSnakeDied(Vector3 position)
        {
            Emit(_deathParticles, position, _deathParticleCount);
            EndGame(false);
        }

        private void EndGame(bool won)
        {
            LastGameWon = won;
            _score.SaveBest();
            SetState(GameState.GameOver);
        }

        private static void Emit(ParticleSystem ps, Vector3 position, int count)
        {
            if (ps == null || count <= 0) return;
            ps.transform.position = position;
            ps.Emit(count);
        }
    }
}
