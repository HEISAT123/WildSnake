using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Список пунктов в стиле интерфейса игры: выбор стрелками/W-S и мышью, Enter/Space — нажать.
    /// Выбранный пункт выезжает вправо, у него горит акцентная полоса и номер; при показе
    /// строки появляются лесенкой. Используется в главном меню, на паузе, на линьке и в итогах.
    /// Нажатия передаются кнопкам (их onClick обрабатывает UIController).
    /// </summary>
    public class MenuList : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Звук перемещения по списку")]
        [SerializeField] private AudioService _audio;

        [Tooltip("Таблица рекордов — для подписи «рекорд» у пункта «Выживание» (необязательно)")]
        [SerializeField] private LeaderboardService _leaderboard;

        [Tooltip("Длина суток — рекорд показывается в сутках (нужен, если есть подписи рекордов)")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Прозрачность всего экрана — для плавного появления (необязательно)")]
        [SerializeField] private CanvasGroup _group;

        [Header("Пункты")]
        [Tooltip("Кнопки сверху вниз")]
        [SerializeField] private Button[] _items = new Button[0];

        [Tooltip("Содержимое пунктов — оно выезжает при выборе")]
        [SerializeField] private RectTransform[] _contents = new RectTransform[0];

        [Tooltip("Акцентные метки выбранного пункта")]
        [SerializeField] private GameObject[] _accents = new GameObject[0];

        [Tooltip("Главные подписи пунктов")]
        [SerializeField] private Text[] _labels = new Text[0];

        [Tooltip("Номера пунктов «01», «02»… (могут быть пустыми)")]
        [SerializeField] private Text[] _indexes = new Text[0];

        [Tooltip("Подписи «рекорд» у пунктов (необязательно)")]
        [SerializeField] private Text[] _records = new Text[0];

        [Header("Вид")]
        [SerializeField] private Color _labelColor = new Color(0.62f, 0.63f, 0.6f);
        [SerializeField] private Color _labelSelectedColor = new Color(0.95f, 0.93f, 0.88f);
        [SerializeField] private Color _indexColor = new Color(0.42f, 0.44f, 0.44f);
        [SerializeField] private Color _indexSelectedColor = new Color(0.78f, 0.96f, 0.2f);

        [Tooltip("На сколько выезжает выбранный пункт, пикселей")]
        [SerializeField] private float _selectedShift = 22f;

        [Tooltip("С какого смещения строки въезжают при появлении, пикселей")]
        [SerializeField] private float _introShift = -48f;

        [Tooltip("Длительность появления одной строки, сек")]
        [SerializeField, Min(0.05f)] private float _introDuration = 0.35f;

        [Tooltip("Задержка между строками при появлении, сек")]
        [SerializeField, Min(0f)] private float _introStagger = 0.05f;

        [Header("Окна поверх списка")]
        [Tooltip("Пока эти окна открыты (настройки, рекорды), список не реагирует на клавиши")]
        [SerializeField] private GameObject[] _overlays = new GameObject[0];

        private MenuItemHover[] _hovers;
        private Vector2[] _basePositions;
        private float[] _shift;
        private int _selected;
        private float _introTime;
        private bool _recordsDirty = true;

        private void Awake()
        {
            _hovers = new MenuItemHover[_items.Length];
            for (int i = 0; i < _items.Length; i++)
            {
                _hovers[i] = _items[i].GetComponent<MenuItemHover>();
                if (_hovers[i] != null) _hovers[i].Index = i;
            }

            _basePositions = new Vector2[_contents.Length];
            for (int i = 0; i < _contents.Length; i++) _basePositions[i] = _contents[i].anchoredPosition;
            _shift = new float[_contents.Length];
        }

        private void OnEnable()
        {
            for (int i = 0; i < _hovers.Length; i++)
                if (_hovers[i] != null) _hovers[i].Entered += OnItemHovered;

            // Каждый показ — с первого пункта и с анимацией появления.
            _selected = 0;
            _introTime = 0f;
            for (int i = 0; i < _shift.Length; i++) _shift[i] = 0f;
            // Рекорды пересчитываем в Update: при загрузке сцены OnEnable может выполниться раньше,
            // чем LeaderboardService прочитает таблицы.
            _recordsDirty = true;
            Select(0, false);
            Animate(0f);
        }

        private void OnDisable()
        {
            for (int i = 0; i < _hovers.Length; i++)
                if (_hovers[i] != null) _hovers[i].Entered -= OnItemHovered;
        }

        private void Update()
        {
            // Unscaled: список работает и на паузе, где timeScale = 0.
            float dt = Time.unscaledDeltaTime;
            if (_recordsDirty)
            {
                _recordsDirty = false;
                RefreshRecords();
            }

            Animate(dt);
            if (IsOverlayOpen() || _items.Length == 0) return;

            // Кнопку, выбранную кликом, сбрасываем: иначе Enter сработал бы дважды (у EventSystem и у нас).
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);

            if (GameInput.MenuUp) Select(Step(-1), true);
            else if (GameInput.MenuDown) Select(Step(1), true);
            else if (GameInput.Confirm) _items[_selected].onClick.Invoke();
        }

        private void Animate(float dt)
        {
            _introTime += dt;
            if (_group != null) _group.alpha = Mathf.Clamp01(_introTime / (_introDuration * 0.8f));

            for (int i = 0; i < _contents.Length; i++)
            {
                float target = i == _selected ? _selectedShift : 0f;
                _shift[i] = Mathf.Lerp(_shift[i], target, 1f - Mathf.Exp(-14f * dt));

                float k = Mathf.Clamp01((_introTime - i * _introStagger) / _introDuration);
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);
                _contents[i].anchoredPosition = _basePositions[i] + new Vector2(_shift[i] + _introShift * (1f - ease), 0f);
            }
        }

        private void RefreshRecords()
        {
            if (_leaderboard == null || _config == null) return;
            int best = _leaderboard.BestSeconds;
            string text = best > 0 ? "РЕКОРД  " + TimeFormat.Days(best, _config.DaySeconds + _config.NightSeconds) + " СУТ." : "—";
            for (int i = 0; i < _records.Length; i++)
                if (_records[i] != null) _records[i].text = text;
        }

        /// <summary>Следующий видимый пункт: скрытые (на линьке бывает меньше трёх вариантов) пропускаем.</summary>
        private int Step(int direction)
        {
            int index = _selected;
            for (int i = 0; i < _items.Length; i++)
            {
                index = (index + direction + _items.Length) % _items.Length;
                if (_items[index].gameObject.activeSelf) return index;
            }
            return _selected;
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
                if (i < _labels.Length && _labels[i] != null) _labels[i].color = on ? _labelSelectedColor : _labelColor;
                if (i < _indexes.Length && _indexes[i] != null) _indexes[i].color = on ? _indexSelectedColor : _indexColor;
                if (i < _accents.Length && _accents[i] != null && _accents[i].activeSelf != on) _accents[i].SetActive(on);
            }
            if (playSound && _audio != null) _audio.Play(SoundId.Click);
        }

        private bool IsOverlayOpen()
        {
            for (int i = 0; i < _overlays.Length; i++)
                if (_overlays[i] != null && _overlays[i].activeSelf) return true;
            return false;
        }
    }
}
