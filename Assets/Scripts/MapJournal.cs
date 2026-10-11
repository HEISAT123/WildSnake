using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Карта на весь экран и журнал целей по Tab (вместо постоянной мини-карты): пока Tab зажат,
    /// видна карта (её рисует MapIcons), справа — главная цель, цели суток с отметками и короткая сводка жизни.
    /// Тексты журнала обновляются только при открытии и при изменении целей.
    /// </summary>
    public class MapJournal : MonoBehaviour
    {
        private static readonly string[] GoalTexts =
        {
            "Поймать птицу из засады",
            "Пережить ночь вне норы",
            "Задушить кролика",
            "Поймать трёх мышей",
            "Отпугнуть лису броском",
            "Уйти от атаки орла",
            "Прогреться на солнечном камне",
            "Поймать две рыбы",
            "Съесть три яйца из птичьих гнёзд"
        };

        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private DayNightCycle _dayNight;

        [Header("Элементы")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _dayText;

        [Tooltip("Главная цель: этап и что делать")]
        [SerializeField] private Text _questText;
        [SerializeField] private Text _questNote;

        [Tooltip("Строки целей (до двух)")]
        [SerializeField] private Text[] _goalLabels = new Text[DailyGoals.MAX_GOALS];

        [Tooltip("Награды и прогресс целей")]
        [SerializeField] private Text[] _goalNotes = new Text[DailyGoals.MAX_GOALS];

        [Tooltip("Отметки выполнения (лаймовая черта)")]
        [SerializeField] private GameObject[] _goalChecks = new GameObject[DailyGoals.MAX_GOALS];

        [SerializeField] private Text _summaryText;

        [Header("Цвета")]
        [SerializeField] private Color _openColor = new Color(0.95f, 0.93f, 0.88f);
        [SerializeField] private Color _doneColor = new Color(0.46f, 0.48f, 0.48f);

        private bool _open;
        private bool _dirty = true;

        private void Awake()
        {
            _group.alpha = 0f;
        }

        private void OnEnable()
        {
            _game.GoalsRolled += MarkDirty;
            _game.GoalCompleted += OnGoalCompleted;
            _game.QuestAdvanced += OnQuestAdvanced;
        }

        private void OnDisable()
        {
            _game.GoalsRolled -= MarkDirty;
            _game.GoalCompleted -= OnGoalCompleted;
            _game.QuestAdvanced -= OnQuestAdvanced;
        }

        /// <summary>Название цели для баннера при выполнении.</summary>
        public static string GoalName(GoalType type) => GoalTexts[(int)type];

        private void OnGoalCompleted(int index) => MarkDirty();
        private void OnQuestAdvanced(QuestStage stage) => MarkDirty();

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void Update()
        {
            bool open = _game.State == GameState.Playing && GameInput.MapHeld;
            if (open != _open)
            {
                _open = open;
                if (open) _dirty = true;
            }
            if (_open && _dirty)
            {
                _dirty = false;
                Refresh();
            }
            _group.alpha = Mathf.MoveTowards(_group.alpha, _open ? 1f : 0f, Time.unscaledDeltaTime * 10f);
        }

        private void Refresh()
        {
            _dayText.text = "ДЕНЬ " + _dayNight.Day + (_dayNight.IsNight ? "  ·  НОЧЬ" : "");
            MainQuest quest = _game.Quest;
            string generation = quest.Generation > 1 ? "Поколение " + quest.Generation + "  ·  " : "";
            if (quest.Stage == QuestStage.Grow)
            {
                _questText.text = generation + "1 из 3  ·  Повзрослеть";
                _questNote.text = "Длина " + _game.SnakeLength.ToString("0.0") + " из " + quest.TargetLength.ToString("0.#")
                                  + ", линьки " + Mathf.Min(_game.GenerationMolts, quest.TargetMolts) + " из " + quest.TargetMolts
                                  + ". Потом на карте появится гнездо.";
            }
            else if (quest.Stage == QuestStage.Nest)
            {
                _questText.text = generation + "2 из 3  ·  Отложить яйца в гнезде";
                _questNote.text = "Гнездо — значок с яйцами в кольце на карте и столб света на поле. Нужны ночь и сытость "
                                  + Mathf.RoundToInt(quest.NestSatiety * 100f) + "%.";
            }
            else
            {
                _questText.text = generation + "3 из 3  ·  Охранять кладку";
                _questNote.text = "Яиц " + quest.Eggs + ". Лиса придёт за ними — будьте рядом и отгоняйте её броском. "
                                  + "Вылупление через " + TimeFormat.Clock(Mathf.CeilToInt(quest.HatchSecondsLeft)) + ".";
            }
            DailyGoals goals = _game.Goals;
            for (int i = 0; i < _goalLabels.Length; i++)
            {
                bool has = i < goals.Count;
                _goalLabels[i].gameObject.SetActive(has);
                _goalNotes[i].gameObject.SetActive(has);
                if (!has)
                {
                    _goalChecks[i].SetActive(false);
                    continue;
                }
                DailyGoal g = goals.Get(i);
                _goalLabels[i].text = GoalTexts[(int)g.Type];
                _goalLabels[i].color = g.Done ? _doneColor : _openColor;
                string reward = g.MoltReward ? "награда — шаг к линьке" : "награда — полное восстановление";
                _goalNotes[i].text = g.Done ? "выполнено" : g.Target > 1 ? g.Progress + " из " + g.Target + "  ·  " + reward : reward;
                _goalChecks[i].SetActive(g.Done);
            }

            RunStats s = _game.Stats;
            _summaryText.text = "Добыча " + s.Prey + "   ·   линьки " + s.Molts + "   ·   длина " + _game.SnakeLength.ToString("0.0");
        }
    }
}
