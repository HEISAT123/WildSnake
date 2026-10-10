using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Показатели змеи: три тонкие дуги вокруг маленького значка змеи (здоровье снаружи, сытость посередине,
    /// выносливость внутри) и подписи с процентами справа. Блок виден всегда; ниже порога дуга и число краснеют.
    /// </summary>
    public class HudVitals : MonoBehaviour
    {
        private const int HEALTH = 0;
        private const int SATIETY = 1;
        private const int STAMINA = 2;
        private const int COUNT = 3;

        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SnakeController _snake;

        [Header("Элементы")]
        [Tooltip("Прозрачность всего блока (значок змеи)")]
        [SerializeField] private CanvasGroup _group;

        [Tooltip("Заливки дуг: здоровье, сытость, выносливость (Image Filled Radial360)")]
        [SerializeField] private Image[] _fills = new Image[COUNT];

        [Tooltip("Прозрачность каждой дуги вместе с её дорожкой")]
        [SerializeField] private CanvasGroup[] _arcs = new CanvasGroup[COUNT];

        [Tooltip("Подписи дуг («ЗДОРОВЬЕ 75»)")]
        [SerializeField] private CanvasGroup[] _labels = new CanvasGroup[COUNT];

        [Tooltip("Числа в подписях, проценты")]
        [SerializeField] private Text[] _values = new Text[COUNT];

        [Header("Поведение")]
        [Tooltip("Ниже этой доли дуга и число красные")]
        [SerializeField, Range(0f, 1f)] private float _lowThreshold = 0.4f;

        [Tooltip("Какую долю дуги занимает полный запас (0.75 — три четверти круга)")]
        [SerializeField, Range(0.2f, 1f)] private float _arcSpan = 0.75f;

        [Header("Цвета")]
        [SerializeField] private Color _healthColor = new Color(0.95f, 0.93f, 0.88f);
        [SerializeField] private Color _satietyColor = new Color(0.78f, 0.96f, 0.2f);
        [SerializeField] private Color _staminaColor = new Color(0.62f, 0.63f, 0.6f);
        [SerializeField] private Color _lowColor = new Color(1f, 0.42f, 0.3f);

        private readonly float[] _shown = new float[COUNT];
        private readonly int[] _shownPercent = { -1, -1, -1 };
        private bool _primed;

        private void OnEnable()
        {
            _primed = false;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < COUNT; i++)
            {
                float v = Value(i);
                if (!_primed) _shown[i] = v;

                bool low = v < _lowThreshold;
                _arcs[i].alpha = 1f;
                _shown[i] = Mathf.Lerp(_shown[i], v, 1f - Mathf.Exp(-10f * dt));
                _fills[i].fillAmount = _shown[i] * _arcSpan;
                Color color = low ? _lowColor : BaseColor(i);
                _fills[i].color = color;

                _labels[i].alpha = 1f;
                int percent = Mathf.RoundToInt(v * 100f);
                if (percent != _shownPercent[i])
                {
                    _shownPercent[i] = percent;
                    _values[i].text = percent.ToString();
                }
                _values[i].color = color;
            }
            _primed = true;
            _group.alpha = 1f;
        }

        private float Value(int i)
        {
            switch (i)
            {
                case HEALTH: return _game.Survival.Health01;
                case SATIETY: return _game.Survival.Satiety01;
                default: return _snake.Stamina.Value01;
            }
        }

        private Color BaseColor(int i)
        {
            switch (i)
            {
                case HEALTH: return _healthColor;
                case SATIETY: return _satietyColor;
                default: return _staminaColor;
            }
        }
    }
}
