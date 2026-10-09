using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Обучение при первом запуске: по шагам подсказывает руль, прыжок, рывок, еду и бонусы.
    /// Шаг засчитывается, когда игрок сделал действие (или прошло время для информационных шагов).
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        private const string KEY_DONE = "Snake3D.TutorialDone";

        private static readonly string[] Steps =
        {
            "Зажмите A / D или ← / →, чтобы рулить",
            "Нажмите Space, чтобы прыгнуть — так можно перелететь низкую стенку или свой хвост",
            "Нажмите Shift для рывка",
            "Яблоки: красное — рост, золотое — много очков, гнилое — укорачивает",
            "Кристаллы — бонусы: ускорение, замедление, щит, магнит и очки x2"
        };

        [Header("Ссылки")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SnakeController _snake;

        [Tooltip("Плашка подсказки")]
        [SerializeField] private GameObject _plate;

        [Tooltip("Текст подсказки")]
        [SerializeField] private Text _text;

        [Header("Тайминги")]
        [Tooltip("Сколько секунд нужно рулить, чтобы засчитать шаг")]
        [SerializeField, Min(0.1f)] private float _steerTime = 1f;

        [Tooltip("Сколько висят информационные подсказки, сек")]
        [SerializeField, Min(1f)] private float _infoTime = 5f;

        private int _step;
        private float _timer;
        private bool _done;

        private void Awake()
        {
            _done = PlayerPrefs.GetInt(KEY_DONE, 0) == 1;
        }

        private void OnEnable()
        {
            _snake.Jumped += OnJumped;
            _snake.Dashed += OnDashed;
        }

        private void OnDisable()
        {
            _snake.Jumped -= OnJumped;
            _snake.Dashed -= OnDashed;
        }

        private void Start()
        {
            ShowStep(0);
        }

        public void ResetTutorial()
        {
            PlayerPrefs.SetInt(KEY_DONE, 0);
            PlayerPrefs.Save();
            _done = false;
            ShowStep(0);
        }

        private void Update()
        {
            bool show = !_done && _game.State == GameState.Playing;
            if (_plate.activeSelf != show) _plate.SetActive(show);
            if (!show) return;

            float dt = Time.unscaledDeltaTime;
            switch (_step)
            {
                case 0:
                    if (Mathf.Abs(_snake.Steer) > 0.5f) _timer += dt;
                    if (_timer >= _steerTime) ShowStep(1);
                    break;
                case 3:
                case 4:
                    _timer += dt;
                    if (_timer >= _infoTime) ShowStep(_step + 1);
                    break;
            }
        }

        private void OnJumped()
        {
            if (_step == 1) ShowStep(2);
        }

        private void OnDashed()
        {
            if (_step == 2) ShowStep(3);
        }

        private void ShowStep(int step)
        {
            _step = step;
            _timer = 0f;
            if (step >= Steps.Length)
            {
                _done = true;
                PlayerPrefs.SetInt(KEY_DONE, 1);
                PlayerPrefs.Save();
                _plate.SetActive(false);
                return;
            }
            _text.text = Steps[step];
        }
    }
}
