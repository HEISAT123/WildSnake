using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Карта на весь экран и журнал целей по Tab (вместо постоянной мини-карты): пока Tab зажат,
    /// камера карты рисует поле, справа — цели суток с отметками и короткая сводка жизни.
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
            "Прогреться на солнечном камне"
        };

        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private DayNightCycle _dayNight;

        [Tooltip("Камера карты: включается только пока открыт журнал")]
        [SerializeField] private Camera _mapCamera;

        [Header("Элементы")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _dayText;

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
            _mapCamera.enabled = false;
        }

        private void OnEnable()
        {
            _game.GoalsRolled += MarkDirty;
            _game.GoalCompleted += OnGoalCompleted;
        }

        private void OnDisable()
        {
            _game.GoalsRolled -= MarkDirty;
            _game.GoalCompleted -= OnGoalCompleted;
            if (_mapCamera != null) _mapCamera.enabled = false;
        }

        /// <summary>Название цели для баннера при выполнении.</summary>
        public static string GoalName(GoalType type) => GoalTexts[(int)type];

        private void OnGoalCompleted(int index) => MarkDirty();

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
                _mapCamera.enabled = open;
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
