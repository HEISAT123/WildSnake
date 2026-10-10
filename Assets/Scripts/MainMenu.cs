using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Главное меню поверх живой сцены: карточки режимов с рекордами и кнопки снизу.
    /// Выбор стрелками/W-S и мышью, подтверждение Enter/Space. Нажатия передаются кнопкам
    /// (их onClick обрабатывает UIController), здесь только выбор и подсветка.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Таблицы рекордов — для подписи «рекорд» на карточках режимов")]
        [SerializeField] private LeaderboardService _leaderboard;

        [Tooltip("Звук перемещения по меню")]
        [SerializeField] private AudioService _audio;

        [Header("Пункты")]
        [Tooltip("Кнопки сверху вниз; первые «Число режимов» — режимы игры по порядку GameMode")]
        [SerializeField] private Button[] _items = new Button[0];

        [Tooltip("Фоны пунктов (подсвечиваются у выбранного)")]
        [SerializeField] private Image[] _backgrounds = new Image[0];

        [Tooltip("Акцентные полоски выбранного пункта (могут быть пустыми)")]
        [SerializeField] private GameObject[] _accents = new GameObject[0];

        [Tooltip("Подписи «рекорд» на карточках режимов")]
        [SerializeField] private Text[] _recordTexts = new Text[0];

        [Tooltip("Сколько первых пунктов — режимы игры")]
        [SerializeField, Min(0)] private int _modeItemCount = 3;

        [Header("Цвета")]
        [Tooltip("Фон карточки режима")]
        [SerializeField] private Color _cardColor = new Color(1f, 1f, 1f, 0.06f);

        [Tooltip("Фон выбранной карточки режима")]
        [SerializeField] private Color _cardSelectedColor = new Color(1f, 1f, 1f, 0.16f);

        [Tooltip("Фон кнопки")]
        [SerializeField] private Color _buttonColor = new Color(1f, 1f, 1f, 0.08f);

        [Tooltip("Фон выбранной кнопки")]
        [SerializeField] private Color _buttonSelectedColor = new Color(1f, 1f, 1f, 0.22f);

        [Header("Окна поверх меню")]
        [Tooltip("Пока эти окна открыты (настройки, рекорды), меню не реагирует на клавиши")]
        [SerializeField] private GameObject[] _overlays = new GameObject[0];

        private MenuItemHover[] _hovers;
        private int _selected;
        private bool _recordsDirty = true;

        private void Awake()
        {
            _hovers = new MenuItemHover[_items.Length];
            for (int i = 0; i < _items.Length; i++)
            {
                _hovers[i] = _items[i].GetComponent<MenuItemHover>();
                if (_hovers[i] != null) _hovers[i].Index = i;
            }
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

            if (IsOverlayOpen() || _items.Length == 0) return;

            // Кнопку, выбранную кликом, сбрасываем: иначе Enter сработал бы дважды (у EventSystem и у нас).
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);

            if (GameInput.MenuUp) Select((_selected - 1 + _items.Length) % _items.Length, true);
            else if (GameInput.MenuDown) Select((_selected + 1) % _items.Length, true);
            else if (GameInput.Confirm) _items[_selected].onClick.Invoke();
        }

        /// <summary>Строки собираются только при показе меню, а не каждый кадр.</summary>
        private void RefreshRecords()
        {
            for (int m = 0; m < _recordTexts.Length && m < _modeItemCount; m++)
            {
                if (_recordTexts[m] == null) continue;
                int best = _leaderboard.Best((GameMode)m);
                _recordTexts[m].text = best > 0 ? "рекорд " + best : "нет рекорда";
            }
        }

        private void OnItemHovered(int index)
        {
            if (index != _selected) Select(index, true);
        }

        private void Select(int index, bool playSound)
        {
            _selected = index;
            for (int i = 0; i < _items.Length; i++)
            {
                bool on = i == index;
                bool isMode = i < _modeItemCount;
                if (i < _backgrounds.Length && _backgrounds[i] != null)
                    _backgrounds[i].color = isMode ? (on ? _cardSelectedColor : _cardColor) : (on ? _buttonSelectedColor : _buttonColor);
                if (i < _accents.Length && _accents[i] != null && _accents[i].activeSelf != on)
                    _accents[i].SetActive(on);
            }
            if (playSound) _audio.Play(SoundId.Click);
        }

        private bool IsOverlayOpen()
        {
            for (int i = 0; i < _overlays.Length; i++)
                if (_overlays[i] != null && _overlays[i].activeSelf) return true;
            return false;
        }
    }
}
