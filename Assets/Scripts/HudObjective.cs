using UnityEngine;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Строка текущей главной цели слева сверху: что делать дальше и сколько осталось («Повзрослеть: длина 6,4 из 11…»,
    /// «Охраняйте кладку: яиц 5 · вылупление через 2:10»). Со второго поколения — с номером поколения.
    /// Днём, пока на поле соперница, в конце — счёт дневного состязания («Охота: вы 3 : 2 она»).
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
        private int _shownContest = -2;

        private void OnEnable()
        {
            _shownKey = -1;
            _shownMolt = -1;
            _shownContest = -2;
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
            int contestKey = _game.ContestActive ? Mathf.Min(_game.ContestPlayer, 999) * 1000 + Mathf.Min(_game.ContestRival, 999) : -1;
            if (stateKey == _shownKey && contestKey == _shownContest) return;
            _shownKey = stateKey;
            _shownContest = contestKey;

            string contest = contestKey >= 0 ? "      Охота:  вы " + _game.ContestPlayer + " : " + _game.ContestRival + " она" : "";
            _text.text = QuestText(quest, length, molts, secondsLeft) + contest;
        }

        private string QuestText(MainQuest quest, float length, int molts, int secondsLeft)
        {
            string generation = quest.Generation > 1 ? "Поколение " + quest.Generation + "  ·  " : "";
            if (quest.Stage == QuestStage.Grow)
                return generation + "Повзрослеть:  длина " + Mathf.Min(length, quest.TargetLength).ToString("0.0") + " из " + quest.TargetLength.ToString("0.#")
                       + "  ·  линьки " + Mathf.Min(molts, quest.TargetMolts) + " из " + quest.TargetMolts;
            if (quest.Stage == QuestStage.Guard)
                return generation + "Охраняйте кладку:  яиц " + quest.Eggs + "  ·  вылупление через " + TimeFormat.Clock(secondsLeft);

            switch (_game.NestStatus)
            {
                case NestCheck.NeedNight: return "Отложить яйца в гнезде (светлый столб) — дождитесь ночи";
                case NestCheck.NeedFood: return "Отложить яйца в гнезде — сначала наешьтесь";
                default: return "Ползите к гнезду (светлый столб) и отложите яйца — E";
            }
        }
    }
}
