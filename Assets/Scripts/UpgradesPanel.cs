using UnityEngine;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Окно «Улучшения» во время партии: U — открыть/закрыть, Esc тоже закрывает (и не ставит игру на паузу).
    /// Все 13 улучшений: уровень (три деления), что даёт сейчас и чем платит; не взятые — что дадут на первом уровне.
    /// Игра не останавливается, как и с картой по Tab; карта (Tab) закрывает окно.
    /// Тексты собираются только при открытии.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UpgradesPanel : MonoBehaviour
    {
        [Header("Источники")]
        [SerializeField] private GameManager _game;

        [Header("Элементы")]
        [SerializeField] private CanvasGroup _group;

        [Tooltip("Сводка: сколько уровней взято и сколько до линьки")]
        [SerializeField] private Text _summary;

        [Tooltip("Названия улучшений (порядок UpgradeType)")]
        [SerializeField] private Text[] _names = new Text[UpgradeSet.TYPE_COUNT];

        [Tooltip("Подписи уровня справа от названия")]
        [SerializeField] private Text[] _levels = new Text[UpgradeSet.TYPE_COUNT];

        [Tooltip("Что даёт и чем платит")]
        [SerializeField] private Text[] _effects = new Text[UpgradeSet.TYPE_COUNT];

        [Tooltip("Деления уровня: по MAX_LEVEL на улучшение подряд")]
        [SerializeField] private Image[] _pips = new Image[UpgradeSet.TYPE_COUNT * UpgradeSet.MAX_LEVEL];

        [Header("Цвета")]
        [SerializeField] private Color _takenColor = new Color(0.95f, 0.93f, 0.88f);
        [SerializeField] private Color _maxColor = new Color(0.78f, 0.96f, 0.2f);
        [SerializeField] private Color _emptyColor = new Color(0.46f, 0.48f, 0.48f);
        [SerializeField] private Color _pipOff = new Color(0.95f, 0.93f, 0.88f, 0.16f);

        [Tooltip("Цвет платы в описании (rich text)")]
        [SerializeField] private string _costColor = "#E8836B";

        private bool _open;

        /// <summary>Окно открыто (для других частей интерфейса).</summary>
        public bool IsOpen => _open;

        private void Awake()
        {
            _group.alpha = 0f;
        }

        private void Update()
        {
            bool playing = _game.State == GameState.Playing;
            if (!playing || GameInput.MapHeld) _open = false;
            else if (GameInput.Upgrades)
            {
                _open = !_open;
                if (_open) Refresh();
            }
            // Esc закрывает окно и не доходит до паузы.
            if (_open && GameInput.Pause)
            {
                GameInput.ConsumePause();
                _open = false;
            }
            _group.alpha = Mathf.MoveTowards(_group.alpha, _open ? 1f : 0f, Time.unscaledDeltaTime * 10f);
        }

        private void Refresh()
        {
            UpgradeSet upgrades = _game.Upgrades;
            int total = upgrades.TotalLevels;
            int max = UpgradeSet.TYPE_COUNT * UpgradeSet.MAX_LEVEL;
            string next = total >= max
                ? "Всё взято — линька теперь просто лечит раны."
                : "До следующей линьки: " + Mathf.RoundToInt(_game.MoltProgress01 * 100f) + "% шкалы.";
            _summary.text = "Взято уровней: " + total + " из " + max + ".   " + next;

            for (int i = 0; i < UpgradeSet.TYPE_COUNT; i++)
            {
                var type = (UpgradeType)i;
                int level = upgrades.Level(type);
                bool full = level >= UpgradeSet.MAX_LEVEL;
                _names[i].text = UpgradeTexts.Name(type);
                _names[i].color = level == 0 ? _emptyColor : full ? _maxColor : _takenColor;
                _levels[i].text = level == 0 ? "не взято" : full ? "максимум" : "уровень " + level + " из " + UpgradeSet.MAX_LEVEL;
                _levels[i].color = full ? _maxColor : _emptyColor;

                if (level == 0)
                {
                    _effects[i].text = "На 1-м уровне: " + Lower(UpgradeTexts.Effect(type, 1));
                }
                else
                {
                    string cost = UpgradeTexts.Cost(type, level);
                    _effects[i].text = UpgradeTexts.Effect(type, level)
                                       + (cost.Length > 0 ? "\n<color=" + _costColor + ">" + cost + "</color>" : "");
                }
                _effects[i].color = level == 0 ? _emptyColor : _takenColor;

                for (int k = 0; k < UpgradeSet.MAX_LEVEL; k++)
                {
                    Image pip = _pips[i * UpgradeSet.MAX_LEVEL + k];
                    pip.color = k < level ? (full ? _maxColor : _takenColor) : _pipOff;
                }
            }
        }

        private static string Lower(string text)
        {
            return text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text.Substring(1);
        }
    }
}
