using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Главное меню в стиле synthwave: список пунктов со стрелкой-курсором (клавиатура и мышь),
    /// рекорд выбранного режима и бегущая на зрителя неоновая сетка.
    /// Нажатия передаются кнопкам (их onClick обрабатывает UIController), здесь только выбор и анимация.
    /// </summary>
    public class NeonMenu : MonoBehaviour
    {
        private static readonly string[] ModeNames = { "Классика", "На время", "Выживание" };

        [Header("Ссылки")]
        [Tooltip("Таблицы рекордов — показываем рекорд выбранного режима")]
        [SerializeField] private LeaderboardService _leaderboard;

        [Tooltip("Звук перемещения по меню")]
        [SerializeField] private AudioService _audio;

        [Header("Пункты")]
        [Tooltip("Кнопки пунктов сверху вниз; первые «Число режимов» — режимы игры по порядку GameMode")]
        [SerializeField] private Button[] _items = new Button[0];

        [Tooltip("Подписи пунктов (в том же порядке)")]
        [SerializeField] private Text[] _labels = new Text[0];

        [Tooltip("Сколько первых пунктов — режимы игры")]
        [SerializeField, Min(0)] private int _modeItemCount = 3;

        [Tooltip("Цвет обычного пункта")]
        [SerializeField] private Color _normalColor = new Color(0.8f, 0.78f, 0.96f);

        [Tooltip("Цвет выбранного пункта")]
        [SerializeField] private Color _selectedColor = new Color(1f, 0.92f, 0.96f);

        [Tooltip("Текст рекорда выбранного режима")]
        [SerializeField] private Text _recordText;

        [Tooltip("Окна поверх меню (настройки, рекорды): пока они открыты, меню не реагирует на клавиши")]
        [SerializeField] private GameObject[] _overlays = new GameObject[0];

        [Header("Неоновая сетка")]
        [Tooltip("Горизонтальные линии пола, которые бегут к зрителю")]
        [SerializeField] private RectTransform[] _gridLines = new RectTransform[0];

        [Tooltip("Высота пола под горизонтом, пикселей канваса")]
        [SerializeField, Min(1f)] private float _floorHeight = 420f;

        [Tooltip("Скорость бега линий, линий в секунду")]
        [SerializeField, Min(0f)] private float _gridSpeed = 0.6f;

        [Tooltip("Как сильно линии сгущаются к горизонту (перспектива)")]
        [SerializeField, Range(1f, 4f)] private float _perspective = 2.6f;

        private string[] _normalTexts;
        private string[] _selectedTexts;
        private string[] _recordTexts;
        private MenuItemHover[] _hovers;
        private Image[] _gridImages;
        private int _selected;
        private float _gridPhase;
        private bool _recordsDirty = true;

        private void Awake()
        {
            // Строки с курсором и без готовим заранее — при навигации не создаём новых.
            _normalTexts = new string[_labels.Length];
            _selectedTexts = new string[_labels.Length];
            for (int i = 0; i < _labels.Length; i++)
            {
                _normalTexts[i] = "    " + _labels[i].text;
                _selectedTexts[i] = "›  " + _labels[i].text;
            }

            _hovers = new MenuItemHover[_items.Length];
            for (int i = 0; i < _items.Length; i++)
            {
                _hovers[i] = _items[i].GetComponent<MenuItemHover>();
                if (_hovers[i] != null) _hovers[i].Index = i;
            }

            _gridImages = new Image[_gridLines.Length];
            for (int i = 0; i < _gridLines.Length; i++) _gridImages[i] = _gridLines[i].GetComponent<Image>();

            _recordTexts = new string[_modeItemCount];
        }

        private void OnEnable()
        {
            for (int i = 0; i < _hovers.Length; i++)
                if (_hovers[i] != null) _hovers[i].Entered += OnItemHovered;

            // Рекорды могли измениться за партию. Пересчитываем в Update: при загрузке сцены
            // OnEnable меню может выполниться раньше, чем LeaderboardService прочитает таблицы.
            _recordsDirty = true;
        }

        private void OnDisable()
        {
            for (int i = 0; i < _hovers.Length; i++)
                if (_hovers[i] != null) _hovers[i].Entered -= OnItemHovered;
        }

        private void Update()
        {
            if (_recordsDirty)
            {
                _recordsDirty = false;
                RefreshRecords();
                Select(Mathf.Clamp(_selected, 0, _items.Length - 1), false);
            }

            AnimateGrid(Time.unscaledDeltaTime);
            if (IsOverlayOpen() || _items.Length == 0) return;

            // Кнопку, выбранную кликом, сбрасываем: иначе Enter сработал бы дважды (у EventSystem и у нас).
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);

            if (GameInput.MenuUp) Select((_selected - 1 + _items.Length) % _items.Length, true);
            else if (GameInput.MenuDown) Select((_selected + 1) % _items.Length, true);
            else if (GameInput.Confirm) _items[_selected].onClick.Invoke();
        }

        private void RefreshRecords()
        {
            for (int m = 0; m < _modeItemCount && m < ModeNames.Length; m++)
            {
                int best = _leaderboard.Best((GameMode)m);
                _recordTexts[m] = best > 0 ? ModeNames[m] + " · рекорд " + best : ModeNames[m] + " · рекорда пока нет";
            }
        }

        private void OnItemHovered(int index)
        {
            if (index != _selected) Select(index, true);
        }

        private void Select(int index, bool playSound)
        {
            _selected = index;
            for (int i = 0; i < _labels.Length; i++)
            {
                bool on = i == index;
                _labels[i].text = on ? _selectedTexts[i] : _normalTexts[i];
                _labels[i].color = on ? _selectedColor : _normalColor;
            }

            if (_recordText != null) _recordText.text = index < _modeItemCount ? _recordTexts[index] : string.Empty;
            if (playSound) _audio.Play(SoundId.Click);
        }

        private bool IsOverlayOpen()
        {
            for (int i = 0; i < _overlays.Length; i++)
                if (_overlays[i] != null && _overlays[i].activeSelf) return true;
            return false;
        }

        /// <summary>Линии равномерно «едут» по глубине, а на экран проецируются со сгущением к горизонту.</summary>
        private void AnimateGrid(float dt)
        {
            int count = _gridLines.Length;
            if (count == 0) return;
            _gridPhase = Mathf.Repeat(_gridPhase + dt * _gridSpeed, 1f);

            for (int i = 0; i < count; i++)
            {
                float depth = (i + _gridPhase) / count; // 0 — горизонт, 1 — у зрителя
                float y = _floorHeight * Mathf.Pow(depth, _perspective);
                RectTransform line = _gridLines[i];
                line.anchoredPosition = new Vector2(0f, -y);
                if (_gridImages[i] != null)
                {
                    Color c = _gridImages[i].color;
                    // У горизонта линии почти прозрачные — иначе они сливаются в яркую полосу.
                    c.a = Mathf.Lerp(0.03f, 0.95f, depth * depth);
                    _gridImages[i].color = c;
                }
            }
        }
    }
}
