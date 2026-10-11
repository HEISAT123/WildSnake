using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Плеер видео-обучения: проигрывает главы TutorialFilm одну за другой, как ролик. Таймлайн из отрезков-глав,
    /// субтитры, пауза (пробел, клик по кадру), перемотка по главам (← →, список справа), «Готово» и Esc — закрыть.
    /// Время — unscaled: плеер открывается и с паузы игры, где timeScale = 0. Каждое открытие — с первой главы.
    /// </summary>
    // Раньше настроек и справочника: Esc закрывает только плеер, а не панель под ним.
    [DefaultExecutionOrder(-110)]
    public class TutorialPlayer : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private AudioService _audio;
        [SerializeField] private TutorialCanvas _canvas;

        [Header("Тексты")]
        [SerializeField] private Text _chapterTitle;
        [SerializeField] private Text _chapterIndex;
        [SerializeField] private Text _caption;
        [SerializeField] private Text _clock;
        [SerializeField] private Text _playLabel;

        [Header("Таймлайн и главы")]
        [Tooltip("Заливки отрезков таймлайна, по одной на главу")]
        [SerializeField] private Image[] _segmentFills = new Image[0];

        [Tooltip("Строки списка глав (кнопки)")]
        [SerializeField] private Button[] _chapterButtons = new Button[0];

        [Tooltip("Подписи строк списка глав")]
        [SerializeField] private Text[] _chapterLabels = new Text[0];

        [Header("Кнопки")]
        [SerializeField] private Button _screenButton;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _backButton;

        [Header("Цвета")]
        [SerializeField] private Color _activeColor = new Color(0.78f, 0.96f, 0.2f);
        [SerializeField] private Color _idleColor = new Color(0.62f, 0.63f, 0.6f);
        [SerializeField] private Color _doneColor = new Color(0.95f, 0.93f, 0.88f);

        private UnityAction[] _chapterActions;
        private int _chapter;
        private float _time;
        private bool _playing;
        private bool _ended;
        private int _shownSecond = -1;
        private float _totalSeconds;
        private string _totalText;

        private void Awake()
        {
            // Обработчики строк глав создаём один раз — подписка и отписка одними и теми же делегатами.
            _chapterActions = new UnityAction[_chapterButtons.Length];
            for (int i = 0; i < _chapterActions.Length; i++)
            {
                int index = i;
                _chapterActions[i] = () => OnChapterClicked(index);
            }
            for (int i = 0; i < TutorialFilm.ChapterCount; i++) _totalSeconds += TutorialFilm.Durations[i];
            _totalText = " / " + TimeFormat.Clock(Mathf.RoundToInt(_totalSeconds));
        }

        private void OnEnable()
        {
            _screenButton.onClick.AddListener(OnPlayClicked);
            _playButton.onClick.AddListener(OnPlayClicked);
            _prevButton.onClick.AddListener(OnPrevClicked);
            _nextButton.onClick.AddListener(OnNextClicked);
            _backButton.onClick.AddListener(OnBackClicked);
            for (int i = 0; i < _chapterButtons.Length; i++) _chapterButtons[i].onClick.AddListener(_chapterActions[i]);
            SetChapter(0);
            _playing = true;
            RefreshPlayLabel();
        }

        private void OnDisable()
        {
            _screenButton.onClick.RemoveListener(OnPlayClicked);
            _playButton.onClick.RemoveListener(OnPlayClicked);
            _prevButton.onClick.RemoveListener(OnPrevClicked);
            _nextButton.onClick.RemoveListener(OnNextClicked);
            _backButton.onClick.RemoveListener(OnBackClicked);
            for (int i = 0; i < _chapterButtons.Length; i++) _chapterButtons[i].onClick.RemoveListener(_chapterActions[i]);
        }

        private void Update()
        {
            if (GameInput.Pause)
            {
                GameInput.ConsumePause();
                OnBackClicked();
                return;
            }
            if (GameInput.Confirm) OnPlayClicked();
            if (GameInput.MenuLeft) OnPrevClicked();
            if (GameInput.MenuRight) OnNextClicked();

            if (_playing)
            {
                _time += Time.unscaledDeltaTime;
                float duration = TutorialFilm.Durations[_chapter];
                if (_time >= duration)
                {
                    if (_chapter < TutorialFilm.ChapterCount - 1) SetChapter(_chapter + 1);
                    else
                    {
                        // Ролик кончился: стоп-кадр на последней сцене, кнопка предлагает смотреть заново.
                        _time = duration;
                        _playing = false;
                        _ended = true;
                        RefreshPlayLabel();
                    }
                }
            }

            _canvas.BeginFrame();
            TutorialFilm.Draw(_chapter, _time, _canvas);
            _canvas.EndFrame();
            _caption.text = TutorialFilm.Caption(_chapter, _time);
            RefreshTimeline();
        }

        // ---------------- Управление ----------------

        private void SetChapter(int chapter)
        {
            _chapter = Mathf.Clamp(chapter, 0, TutorialFilm.ChapterCount - 1);
            _time = 0f;
            _ended = false;
            _chapterTitle.text = TutorialFilm.Titles[_chapter];
            _chapterIndex.text = (_chapter + 1).ToString("00") + " / " + TutorialFilm.ChapterCount.ToString("00");
            for (int i = 0; i < _chapterLabels.Length; i++)
                _chapterLabels[i].color = i == _chapter ? _activeColor : i < _chapter ? _doneColor : _idleColor;
            _shownSecond = -1;
        }

        private void OnPlayClicked()
        {
            _audio.Play(SoundId.Click);
            if (_ended)
            {
                SetChapter(0);
                _playing = true;
            }
            else _playing = !_playing;
            RefreshPlayLabel();
        }

        private void OnPrevClicked()
        {
            _audio.Play(SoundId.Click);
            // Как в плеере: далеко от начала главы — к её началу, в самом начале — к предыдущей.
            SetChapter(_time > 1.5f ? _chapter : _chapter - 1);
            _playing = true;
            RefreshPlayLabel();
        }

        private void OnNextClicked()
        {
            _audio.Play(SoundId.Click);
            if (_chapter >= TutorialFilm.ChapterCount - 1) return;
            SetChapter(_chapter + 1);
            _playing = true;
            RefreshPlayLabel();
        }

        private void OnChapterClicked(int index)
        {
            _audio.Play(SoundId.Click);
            SetChapter(index);
            _playing = true;
            RefreshPlayLabel();
        }

        private void OnBackClicked()
        {
            _audio.Play(SoundId.Click);
            gameObject.SetActive(false);
        }

        // ---------------- Вид ----------------

        private void RefreshPlayLabel()
        {
            // Подписи — с разрядкой, как у остальных главных кнопок.
            _playLabel.text = _ended ? "З А Н О В О" : _playing ? "П А У З А" : "Д А Л Ь Ш Е";
        }

        private void RefreshTimeline()
        {
            float duration = TutorialFilm.Durations[_chapter];
            for (int i = 0; i < _segmentFills.Length; i++)
                _segmentFills[i].fillAmount = i < _chapter ? 1f : i == _chapter ? Mathf.Clamp01(_time / duration) : 0f;

            // Строку часов собираем, только когда сменилась секунда.
            float elapsed = _time;
            for (int i = 0; i < _chapter; i++) elapsed += TutorialFilm.Durations[i];
            int second = Mathf.FloorToInt(elapsed);
            if (second == _shownSecond) return;
            _shownSecond = second;
            _clock.text = TimeFormat.Clock(second) + _totalText;
        }
    }
}
