using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Строка текущей главной цели слева сверху: что делать дальше и сколько осталось («Повзрослеть: длина 6,4 из 11…»).
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

        private int _shownKey = -1;

        private void OnEnable()
        {
            _shownKey = -1;
        }

        private void LateUpdate()
        {
            MainQuest quest = _game.Quest;
            float length = _game.SnakeLength;
            int molts = _game.Stats.Molts;
            _progress.fillAmount = quest.Stage == QuestStage.Grow ? quest.GrowProgress01(length, molts) : 1f;

            // Ключ показываемого состояния: этап, длина до десятых, линьки, ночь и сытость — текст пересобираем только при его смене.
            int stateKey = quest.Stage == QuestStage.Grow
                ? Mathf.RoundToInt(Mathf.Min(length, quest.TargetLength) * 10f) * 16 + Mathf.Min(molts, quest.TargetMolts)
                : 100000 + (int)_game.NestStatus;
            if (stateKey == _shownKey) return;
            _shownKey = stateKey;

            if (quest.Stage == QuestStage.Grow)
            {
                _text.text = "Повзрослеть:  длина " + Mathf.Min(length, quest.TargetLength).ToString("0.0") + " из " + quest.TargetLength.ToString("0")
                             + "  ·  линьки " + Mathf.Min(molts, quest.TargetMolts) + " из " + quest.TargetMolts;
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
