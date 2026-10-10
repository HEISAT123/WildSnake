using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Контекстные подсказки вместо отдельного обучения: маленькая подпись с клавишей у центра экрана
    /// появляется, только когда действие возможно прямо сейчас (у добычи — «ЛКМ укус», у норы — «E войти»…).
    /// Каждая подсказка показывается первые несколько раз, потом больше не появляется (счётчик в PlayerPrefs).
    /// Решение пересчитывается несколько раз в секунду; текст меняется только при смене подсказки.
    /// </summary>
    public class ContextHints : MonoBehaviour
    {
        private enum Hint
        {
            None = -1,
            Constrict,
            BurrowExit,
            Bite,
            Grab,
            Coil,
            ScareFox,
            BurrowEnter,
            Eagle,
            Jump,
            Sneak,
            Move,
            Cold,
            Smell,
            Ambush,
            Count
        }

        private const string KEY_PREFIX = "Snake3D.Hints.";

        private static readonly string[] Keys =
        {
            "F", "E", "ЛКМ", "ЛКМ", "ПКМ", "ЛКМ", "E", "", "SPACE", "CTRL", "WASD", "", "E", ""
        };

        private static readonly string[] Texts =
        {
            "держать — обвить",
            "выйти из норы",
            "укус",
            "схватить кролика",
            "замах — бросок дальше",
            "бросок отпугнёт лису",
            "войти в нору",
            "орёл — уходите в высокую траву",
            "прыжок через бревно",
            "красться — добыча не заметит",
            "ползти · мышь — камера",
            "холодно — погрейтесь на солнечном камне",
            "нюх — найти добычу",
            "замрите — добыча подойдёт сама"
        };

        // Сколько раз показывать каждую подсказку.
        private static readonly int[] Limits = { 6, 4, 4, 4, 4, 3, 4, 3, 3, 3, 3, 3, 3, 3 };

        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private GameConfig _config;
        [SerializeField] private SnakeController _snake;
        [SerializeField] private WildlifeManager _wildlife;
        [SerializeField] private GridManager _grid;
        [SerializeField] private EagleHunter _eagle;
        [SerializeField] private FoxHunter _fox;
        [SerializeField] private ScentTracker _scent;

        [Header("Элементы")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private GameObject _keycap;
        [SerializeField] private Text _keyText;
        [SerializeField] private Text _label;

        [Header("Поведение")]
        [Tooltip("Как часто пересчитывать подсказку, сек")]
        [SerializeField, Min(0.05f)] private float _interval = 0.2f;

        [Tooltip("Подсказка висит хотя бы столько, сек, чтобы не мигала")]
        [SerializeField, Min(0f)] private float _minShowTime = 1.2f;

        private readonly int[] _shown = new int[(int)Hint.Count];
        private Hint _current = Hint.None;
        private float _timer;
        private float _currentTime;
        private bool _movedThisRun;

        private void Awake()
        {
            for (int i = 0; i < _shown.Length; i++) _shown[i] = PlayerPrefs.GetInt(KEY_PREFIX + i, 0);
            _group.alpha = 0f;
        }

        private void OnEnable()
        {
            _game.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            _game.StateChanged -= OnStateChanged;
        }

        /// <summary>Показать все подсказки заново (кнопка «Показать обучение» в настройках).</summary>
        public void ResetCounters()
        {
            for (int i = 0; i < _shown.Length; i++)
            {
                _shown[i] = 0;
                PlayerPrefs.DeleteKey(KEY_PREFIX + i);
            }
            PlayerPrefs.Save();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Menu || state == GameState.GameOver) _movedThisRun = false;
            if (state != GameState.Playing) SetHint(Hint.None);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _currentTime += dt;
            bool playing = _game.State == GameState.Playing;
            _group.alpha = Mathf.MoveTowards(_group.alpha, playing && _current != Hint.None ? 1f : 0f, dt * 5f);
            // Интерфейс скрыт (H) — подсказки не показываются и не расходуют свои «первые разы».
            if (!playing || !_group.gameObject.activeInHierarchy) return;
            if (_snake.IsMoving) _movedThisRun = true;

            _timer -= dt;
            if (_timer > 0f) return;
            _timer = _interval;

            Hint next = Choose();
            if (next == _current) return;
            // Не дёргаем подсказку: текущая висит хотя бы _minShowTime, если только её действие не стало невозможным.
            if (_current != Hint.None && next != Hint.None && _currentTime < _minShowTime) return;
            SetHint(next);
        }

        private void SetHint(Hint hint)
        {
            if (hint == _current) return;
            _current = hint;
            _currentTime = 0f;
            if (hint == Hint.None) return;

            int i = (int)hint;
            _shown[i]++;
            PlayerPrefs.SetInt(KEY_PREFIX + i, _shown[i]);
            string key = Keys[i];
            bool hasKey = key.Length > 0;
            if (_keycap.activeSelf != hasKey) _keycap.SetActive(hasKey);
            _keyText.text = key;
            _label.text = Texts[i];
        }

        /// <summary>Самая важная из возможных сейчас подсказок, которые ещё не надоели.</summary>
        private Hint Choose()
        {
            if (!_snake.IsAlive) return Hint.None;
            if (_game.Constriction.IsActive) return Allow(Hint.Constrict);
            if (_snake.InBurrow) return Allow(Hint.BurrowExit);
            return Pick();
        }

        private Hint Pick()
        {
            float cs = _grid.CellSize;
            Vector3 head = _snake.HeadPosition;
            bool hasPrey = _wildlife.TryGetNearestBig(head, 9f * cs, out PreyAnimal prey);
            float preyDist = float.MaxValue;
            if (hasPrey)
            {
                Vector3 d = prey.Position - head;
                d.y = 0f;
                preyDist = d.magnitude / cs;
            }

            if (hasPrey && !prey.IsFleeing && preyDist <= _config.StrikeDistance + 0.4f && FacesTowards(prey.Position, 60f))
            {
                Hint h = Allow(prey.Kind == PreyKind.Rabbit ? Hint.Grab : Hint.Bite);
                if (h != Hint.None) return h;
            }
            if (hasPrey && !prey.IsFleeing && !_snake.IsCoiled && preyDist > _config.StrikeDistance && preyDist <= _config.ChargedStrikeDistance
                && Allow(Hint.Coil) != Hint.None) return Hint.Coil;

            if (_fox.IsChasing && _game.Survival.Health01 > _config.FoxScareHealth)
            {
                Vector3 d = _fox.Position - head;
                d.y = 0f;
                if (d.magnitude < 4f * cs && Allow(Hint.ScareFox) != Hint.None) return Hint.ScareFox;
            }

            if (_game.NearbyBurrow >= 0 && Allow(Hint.BurrowEnter) != Hint.None) return Hint.BurrowEnter;
            if (_eagle.IsThreatening && !_snake.IsHidden && Allow(Hint.Eagle) != Hint.None) return Hint.Eagle;
            if (_snake.IsMoving && LogAhead() && Allow(Hint.Jump) != Hint.None) return Hint.Jump;

            if (hasPrey && !prey.IsFleeing && _snake.IsMoving && _snake.Pace != SnakePace.Sneak
                && preyDist <= _config.GetPrey(prey.Kind).AwareRadius * 1.6f && Allow(Hint.Sneak) != Hint.None) return Hint.Sneak;

            if (!_movedThisRun && Allow(Hint.Move) != Hint.None) return Hint.Move;
            if (_snake.Temperature.IsCold && !_wildlife.IsNight && Allow(Hint.Cold) != Hint.None) return Hint.Cold;
            if (_game.Survival.Satiety01 < 0.5f && !hasPrey && _scent.Charge01 >= 1f && Allow(Hint.Smell) != Hint.None) return Hint.Smell;
            if (_snake.IsHidden && !_snake.IsMoving && hasPrey && preyDist < 7f && !_snake.Ambush.IsLuring
                && Allow(Hint.Ambush) != Hint.None) return Hint.Ambush;
            return Hint.None;
        }

        /// <summary>Подсказка ещё не исчерпана (или она уже на экране — тогда досматриваем).</summary>
        private Hint Allow(Hint hint)
        {
            if (hint == _current) return hint;
            return _shown[(int)hint] < Limits[(int)hint] ? hint : Hint.None;
        }

        private bool FacesTowards(Vector3 point, float angle)
        {
            Vector3 d = point - _snake.HeadPosition;
            d.y = 0f;
            return d.sqrMagnitude < 1e-4f || Vector3.Angle(_snake.Forward, d) <= angle;
        }

        /// <summary>Прямо по курсу бревно (низкая стенка) — через него можно перепрыгнуть.</summary>
        private bool LogAhead()
        {
            Vector3 probe = _snake.HeadPosition + _snake.Forward * (1.2f * _grid.CellSize);
            Vector2Int cell = _grid.WorldToCell(probe);
            return _grid.IsInside(cell) && _grid.GetObstacle(cell) == ObstacleType.LowWall;
        }
    }
}
