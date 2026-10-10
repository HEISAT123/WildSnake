using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Состояние змеи по краям экрана — без полос и цифр. Остаются и в режиме «без интерфейса»:
    /// красная вспышка с той стороны, откуда пришёл удар, медленная пульсация при малом здоровье,
    /// иней по краям, когда змея замёрзла, тёплое свечение на солнечном камне и чернильная тень в засаде.
    /// </summary>
    public class ScreenVignettes : MonoBehaviour
    {
        private const int LEFT = 0;
        private const int RIGHT = 1;
        private const int TOP = 2;
        private const int BOTTOM = 3;

        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SnakeController _snake;

        [Tooltip("Камера: от неё считается, с какой стороны пришёл удар")]
        [SerializeField] private Transform _camera;

        [Header("Слои")]
        [Tooltip("Красные края урона: слева, справа, сверху, снизу")]
        [SerializeField] private CanvasGroup[] _damageEdges = new CanvasGroup[4];

        [Tooltip("Красная виньетка по всему краю (низкое здоровье)")]
        [SerializeField] private CanvasGroup _lowHealth;

        [Tooltip("Иней по краям (холод)")]
        [SerializeField] private CanvasGroup _frost;

        [Tooltip("Тёплое свечение (прогрелась на камне)")]
        [SerializeField] private CanvasGroup _warmth;

        [Tooltip("Чернильная тень по краям (засада)")]
        [SerializeField] private CanvasGroup _ambush;

        private readonly float[] _edge = new float[4];

        private void OnEnable()
        {
            _game.PlayerDamagedFrom += OnDamaged;
            _game.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            _game.PlayerDamagedFrom -= OnDamaged;
            _game.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Playing) return;
            for (int i = 0; i < _edge.Length; i++) _edge[i] = 0f;
        }

        /// <summary>Удар: красим край со стороны источника (сверху — орёл, сбоку и сзади — по направлению).</summary>
        private void OnDamaged(float amount, DamageSource source, Vector3 from)
        {
            if (source == DamageSource.Starvation) return;
            float strength = Mathf.Clamp01(0.45f + amount / 50f);
            if (source == DamageSource.Eagle)
            {
                _edge[TOP] = Mathf.Max(_edge[TOP], strength);
                _edge[LEFT] = Mathf.Max(_edge[LEFT], strength * 0.5f);
                _edge[RIGHT] = Mathf.Max(_edge[RIGHT], strength * 0.5f);
                return;
            }

            Vector3 local = _camera.InverseTransformDirection(from - _snake.HeadPosition);
            float x = local.x;
            float z = local.z;
            if (Mathf.Abs(x) < 1e-3f && Mathf.Abs(z) < 1e-3f) z = 1f;
            float len = Mathf.Sqrt(x * x + z * z);
            x /= len;
            z /= len;
            // Спереди — верхний край, сзади — нижний, по бокам — левый и правый.
            if (x < 0f) _edge[LEFT] = Mathf.Max(_edge[LEFT], strength * -x);
            else _edge[RIGHT] = Mathf.Max(_edge[RIGHT], strength * x);
            if (z > 0f) _edge[TOP] = Mathf.Max(_edge[TOP], strength * z);
            else _edge[BOTTOM] = Mathf.Max(_edge[BOTTOM], strength * -z);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool playing = _game.State == GameState.Playing;
            for (int i = 0; i < _edge.Length; i++)
            {
                _edge[i] = Mathf.Max(0f, _edge[i] - dt * 1.4f);
                _damageEdges[i].alpha = _edge[i];
            }

            float health = _game.Survival.Health01;
            float low = playing && health < 0.3f ? 0.22f + 0.13f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
            Fade(_lowHealth, low, dt);
            Fade(_frost, playing ? _snake.Temperature.Cold01 * 0.7f : 0f, dt);
            Fade(_warmth, playing ? _snake.Temperature.Warm01 * 0.35f : 0f, dt);
            Fade(_ambush, playing && !_snake.InBurrow ? _snake.Ambush.Concealment01 * 0.4f : 0f, dt);
        }

        private static void Fade(CanvasGroup group, float target, float dt)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, target, dt * 1.5f);
        }
    }
}
