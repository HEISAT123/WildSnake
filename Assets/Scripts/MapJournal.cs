using UnityEngine;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Карта на весь экран и журнал по Tab (вместо постоянной мини-карты): пока Tab зажат, видна карта (её рисует MapIcons),
    /// справа — главная цель (этап и что сделать), цели дня (как выполнить и что дадут), сводка жизни и счёт состязания.
    /// Тексты обновляются только при открытии и при изменении целей.
    /// </summary>
    public class MapJournal : MonoBehaviour
    {
        private static readonly string[] GoalTexts =
        {
            "Поймать птицу броском из засады",
            "Пережить ночь, ни разу не зайдя в нору",
            "Задушить кролика",
            "Поймать трёх мышей",
            "Отпугнуть лису броском в морду",
            "Уйти от атаки орла",
            "Прогреться на солнечном камне",
            "Поймать две рыбы",
            "Съесть три яйца из птичьих гнёзд",
            "Укусить соперницу броском",
            "Перехватить добычу у соперницы",
            "Обмануть хищника, притворившись мёртвой"
        };

        // Как выполнить — первая строка под целью.
        private static readonly string[] GoalHowTo =
        {
            "Замрите в траве, пока змея не сольётся с местностью, — и бросок (ЛКМ).",
            "От заката до рассвета — без норы. Ночью греет тёплый ключ.",
            "Бросок хватает кролика; держите F, пока он не сдастся.",
            "Мыши водятся в мышиных городках — значок на карте.",
            "Ночью, пока здоровья больше половины, — бросок лисе в морду.",
            "Орёл пикирует — уходите в траву, под кроны или в полое бревно.",
            "Днём полежите на солнечном камне, пока не согреетесь.",
            "Рыба плавает в реке и прудах — бросок с берега или из воды.",
            "Гнёзда — на опушках. Птица-родитель клюётся, бросок её отгонит.",
            "Бросок (ЛКМ) в тело соперницы.",
            "Поймайте добычу, к которой ползёт соперница, раньше неё.",
            "Когда рядом орёл или лиса, держите X."
        };

        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private DayNightCycle _dayNight;

        [Header("Элементы")]
        [SerializeField] private CanvasGroup _group;
        [Tooltip("HUD: пока открыт журнал, он гаснет — мини-карта и надписи не просвечивают сквозь карту")]
        [SerializeField] private CanvasGroup _hud;
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

        [Tooltip("Номера целей «01», «02»: прячутся вместе со строкой, если цели нет")]
        [SerializeField] private Text[] _goalIndexes = new Text[DailyGoals.MAX_GOALS];

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
            if (_hud != null) _hud.alpha = 1f - _group.alpha;
        }

        private void Refresh()
        {
            _dayText.text = "ДЕНЬ " + _dayNight.Day + (_dayNight.IsNight ? "  ·  НОЧЬ" : "");
            RefreshQuest();
            RefreshGoals();
            RefreshSummary();
        }

        private void RefreshQuest()
        {
            MainQuest quest = _game.Quest;
            string generation = quest.Generation > 1 ? "Поколение " + quest.Generation + "  ·  " : "";
            if (quest.Stage == QuestStage.Grow)
            {
                _questText.text = generation + "Этап 1 из 3  ·  Повзрослеть";
                _questNote.text = "Нужно: длина тела " + _game.SnakeLength.ToString("0.0") + " из " + quest.TargetLength.ToString("0.#")
                                  + " (растёт от еды) и линек " + Mathf.Min(_game.GenerationMolts, quest.TargetMolts) + " из " + quest.TargetMolts
                                  + ". Потом на карте появится гнездо для кладки.";
            }
            else if (quest.Stage == QuestStage.Nest)
            {
                _questText.text = generation + "Этап 2 из 3  ·  Отложить яйца в гнезде";
                _questNote.text = "Гнездо — значок с яйцами в кольце на карте и столб света на поле. Приходите ночью, наевшись хотя бы на "
                                  + Mathf.RoundToInt(quest.NestSatiety * 100f) + "% (сейчас " + Mathf.RoundToInt(_game.Survival.Satiety01 * 100f)
                                  + "%), и нажмите E у гнезда.";
            }
            else
            {
                _questText.text = generation + "Этап 3 из 3  ·  Охранять кладку";
                _questNote.text = "В гнезде яиц: " + quest.Eggs + ". Лиса приходит за ними даже днём — будьте рядом, отгоняйте её броском или шипением (Q). "
                                  + "Змеёныши вылупятся через " + TimeFormat.Clock(Mathf.CeilToInt(quest.HatchSecondsLeft)) + ".";
            }
        }

        private void RefreshGoals()
        {
            DailyGoals goals = _game.Goals;
            string moltReward = "Награда: +" + Mathf.RoundToInt(_game.GoalMoltShare * 100f) + "% шкалы «до линьки»";
            for (int i = 0; i < _goalLabels.Length; i++)
            {
                bool has = i < goals.Count;
                _goalLabels[i].gameObject.SetActive(has);
                _goalNotes[i].gameObject.SetActive(has);
                if (i < _goalIndexes.Length && _goalIndexes[i] != null) _goalIndexes[i].gameObject.SetActive(has);
                if (!has)
                {
                    _goalChecks[i].SetActive(false);
                    continue;
                }
                DailyGoal g = goals.Get(i);
                _goalLabels[i].text = GoalTexts[(int)g.Type];
                _goalLabels[i].color = g.Done ? _doneColor : _openColor;
                if (g.Done) _goalNotes[i].text = "Выполнено — награда получена.";
                else
                {
                    string reward = g.MoltReward ? moltReward : "Награда: здоровье и выносливость — до полного";
                    string progress = g.Target > 1 ? "Сделано " + g.Progress + " из " + g.Target + "  ·  " : "";
                    _goalNotes[i].text = GoalHowTo[(int)g.Type] + "\n" + progress + reward;
                }
                _goalChecks[i].SetActive(g.Done);
            }
        }

        /// <summary>Сводка жизни и счёт дневного состязания с соперницей.</summary>
        private void RefreshSummary()
        {
            RunStats s = _game.Stats;
            string life = "Эта жизнь: добыча " + s.Prey + "  ·  линек " + s.Molts + "  ·  улучшений " + _game.Upgrades.TotalLevels
                          + " (список — U)  ·  длина " + _game.SnakeLength.ToString("0.0");
            string contest;
            if (!_game.RivalActive) contest = "Соперница выключена в настройках.";
            else if (_dayNight.IsNight) contest = "Состязание с соперницей ночью не идёт — новый счёт с утра.";
            else contest = "Состязание с соперницей: вы " + _game.ContestPlayer + " : " + _game.ContestRival
                           + " она (жуки не в счёт). На закате: победа — шаг к линьке, проигрыш — она окрепнет.";
            _summaryText.text = life + "\n" + contest;
        }
    }
}
