using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Строка текущей главной цели слева сверху: что делать дальше и сколько осталось («Повзрослеть: длина 6,4 из 11…»,
    /// «Охраняйте кладку: яиц 5 · вылупление через 2:10»). Со второго поколения — с номером поколения.
    /// Текст меняется, только когда меняется показываемое значение.
    /// </summary>
    public class HudObjective : MonoBehaviour
    {
        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private DayNightCycle _dayNight;

        [Header("Элементы")]
        [SerializeField] private Text _text;

        [Tooltip("Полоса прогресса взросления (Image Filled Horizontal)")]
        [SerializeField] private Image _progress;

        [Tooltip("Полоса до следующей линьки (Image Filled Horizontal)")]
        [SerializeField] private Image _moltFill;

        [Tooltip("Процент до следующей линьки")]
        [SerializeField] private Text _moltValue;

        private int _shownKey = -1;
        private int _shownMolt = -1;

        private void OnEnable()
        {
            _shownKey = -1;
            _shownMolt = -1;
        }

        private void LateUpdate()
        {
            MainQuest quest = _game.Quest;
            float length = _game.SnakeLength;
            int molts = _game.GenerationMolts;
            _progress.fillAmount = quest.Stage == QuestStage.Grow ? quest.GrowProgress01(length, molts)
                : quest.Stage == QuestStage.Guard ? quest.HatchProgress01 : 1f;

            // До следующей линьки: сколько ещё съесть, видно сразу.
            float molt = _game.MoltProgress01;
            _moltFill.fillAmount = molt;
            int moltPercent = Mathf.FloorToInt(molt * 100f);
            if (moltPercent != _shownMolt)
            {
                _shownMolt = moltPercent;
                _moltValue.text = moltPercent + "%";
            }

            // Ключ показываемого состояния: этап, длина до десятых, линьки, ночь и сытость — текст пересобираем только при его смене.
            int secondsLeft = Mathf.CeilToInt(quest.HatchSecondsLeft);
            int stateKey = quest.Stage == QuestStage.Grow
                ? Mathf.RoundToInt(Mathf.Min(length, quest.TargetLength) * 10f) * 16 + Mathf.Min(molts, quest.TargetMolts)
                : quest.Stage == QuestStage.Guard ? 200000 + quest.Eggs * 10000 + secondsLeft
                : 100000 + (int)_game.NestStatus;
            stateKey += quest.Generation * 1000000;
            if (stateKey == _shownKey) return;
            _shownKey = stateKey;

            string generation = quest.Generation > 1 ? "Поколение " + quest.Generation + "  ·  " : "";
            if (quest.Stage == QuestStage.Grow)
            {
                _text.text = generation + "Повзрослеть:  длина " + Mathf.Min(length, quest.TargetLength).ToString("0.0") + " из " + quest.TargetLength.ToString("0.#")
                             + "  ·  линьки " + Mathf.Min(molts, quest.TargetMolts) + " из " + quest.TargetMolts;
                return;
            }
            if (quest.Stage == QuestStage.Guard)
            {
                _text.text = generation + "Охраняйте кладку:  яиц " + quest.Eggs + "  ·  вылупление через " + TimeFormat.Clock(secondsLeft);
                return;
            }

            switch (_game.NestStatus)
            {
                case NestCheck.NeedNight:
                    _text.text = "Отложить яйца в гнезде (светлый столб) — дождитесь ночи";
                    break;
                case NestCheck.NeedFood:
                    _text.text = "Отложить яйца в гнезде — сначала наешьтесь";
                    break;
                default:
                    _text.text = "Ползите к гнезду (светлый столб) и отложите яйца — E";
                    break;
            }
        }
    }
}
