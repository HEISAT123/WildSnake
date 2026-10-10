using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Время суток без часов: маленькая полудуга в правом верхнем углу, по которой днём идёт солнце,
    /// а ночью — луна. Номер суток крупно показывается только на рассвете (баннер).
    /// </summary>
    public class SunDial : MonoBehaviour
    {
        [Header("Источники")]
        [SerializeField] private DayNightCycle _dayNight;

        [Header("Элементы")]
        [Tooltip("Светило, которое едет по дуге")]
        [SerializeField] private RectTransform _body;

        [Tooltip("Картинка светила: днём солнце, ночью луна")]
        [SerializeField] private Image _bodyImage;

        [Tooltip("Пройденная часть дуги (Image Filled Radial180)")]
        [SerializeField] private Image _progress;

        [Tooltip("Радиус дуги, пикселей")]
        [SerializeField, Min(1f)] private float _radius = 38f;

        [Header("Цвета")]
        [SerializeField] private Color _sunColor = new Color(0.78f, 0.96f, 0.2f);
        [SerializeField] private Color _moonColor = new Color(0.82f, 0.86f, 0.95f);

        private void LateUpdate()
        {
            float t = _dayNight.PeriodProgress01;
            bool night = _dayNight.IsNight;
            // Слева направо по верхней полуокружности.
            float angle = Mathf.PI * (1f - t);
            _body.anchoredPosition = new Vector2(Mathf.Cos(angle) * _radius, Mathf.Sin(angle) * _radius);
            Color c = night ? _moonColor : _sunColor;
            _bodyImage.color = c;
            _progress.fillAmount = t;
            c.a = 0.55f;
            _progress.color = c;
        }
    }
}
