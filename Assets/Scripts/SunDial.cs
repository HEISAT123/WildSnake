using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Время суток без часов: маленькая полудуга в правом верхнем углу, по которой днём идёт солнце,
    /// а ночью — луна. Под ней мелко — номер суток и время дня; крупно сутки показываются на рассвете (баннер).
    /// </summary>
    public class SunDial : MonoBehaviour
    {
        private static readonly string[] PeriodNames = { "УТРО", "ДЕНЬ", "ВЕЧЕР", "НОЧЬ" };

        private static int Period(int hour)
        {
            if (hour >= 5 && hour < 11) return 0;
            if (hour >= 11 && hour < 17) return 1;
            if (hour >= 17 && hour < 21) return 2;
            return 3;
        }

        [Header("Источники")]
        [SerializeField] private DayNightCycle _dayNight;

        [Header("Элементы")]
        [Tooltip("Светило, которое едет по дуге")]
        [SerializeField] private RectTransform _body;

        [Tooltip("Картинка светила: днём солнце, ночью луна")]
        [SerializeField] private Image _bodyImage;

        [Tooltip("Пройденная часть дуги (Image Filled Radial360 от левого края)")]
        [SerializeField] private Image _progress;

        [Tooltip("Подпись под дугой: номер суток и время дня")]
        [SerializeField] private Text _label;

        [Tooltip("Радиус дуги, пикселей")]
        [SerializeField, Min(1f)] private float _radius = 38f;

        [Header("Цвета")]
        [SerializeField] private Color _sunColor = new Color(0.78f, 0.96f, 0.2f);
        [SerializeField] private Color _moonColor = new Color(0.82f, 0.86f, 0.95f);

        private int _shownKey = -1;

        private void OnEnable()
        {
            _shownKey = -1;
        }

        private void LateUpdate()
        {
            int period = Period(_dayNight.Hours);
            int key = _dayNight.Day * 8 + period;
            if (key != _shownKey)
            {
                _shownKey = key;
                _label.text = "ДЕНЬ " + _dayNight.Day + "  ·  " + PeriodNames[period];
            }

            float t = _dayNight.PeriodProgress01;
            bool night = _dayNight.IsNight;
            // Слева направо по верхней полуокружности.
            float angle = Mathf.PI * (1f - t);
            _body.anchoredPosition = new Vector2(Mathf.Cos(angle) * _radius, Mathf.Sin(angle) * _radius);
            Color c = night ? _moonColor : _sunColor;
            _bodyImage.color = c;
            // Дорожка — полукруг (половина заливки Radial360), поэтому пройденная часть — до половины.
            _progress.fillAmount = t * 0.5f;
            c.a = 0.55f;
            _progress.color = c;
        }
    }
}
