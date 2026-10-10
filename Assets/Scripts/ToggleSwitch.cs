using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>Вид «свитча» для Toggle: кружок плавно переезжает, дорожка меняет цвет.</summary>
    [RequireComponent(typeof(Toggle))]
    public class ToggleSwitch : MonoBehaviour
    {
        [Header("Части")]
        [Tooltip("Дорожка переключателя")]
        [SerializeField] private Image _track;

        [Tooltip("Кружок переключателя")]
        [SerializeField] private RectTransform _knob;

        [Header("Вид")]
        [Tooltip("Цвет дорожки, когда включено")]
        [SerializeField] private Color _onColor = new Color(0.38f, 0.85f, 0.5f);

        [Tooltip("Цвет дорожки, когда выключено")]
        [SerializeField] private Color _offColor = new Color(1f, 1f, 1f, 0.18f);

        [Tooltip("Смещение кружка от центра по X")]
        [SerializeField, Min(0f)] private float _knobTravel = 14f;

        [Tooltip("Скорость анимации, 1/с")]
        [SerializeField, Min(1f)] private float _speed = 14f;

        private Toggle _toggle;
        private float _position;

        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
        }

        private void OnEnable()
        {
            // Значение могли выставить без события (SetIsOnWithoutNotify) — сразу ставим вид без анимации.
            _position = _toggle.isOn ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            float target = _toggle.isOn ? 1f : 0f;
            if (Mathf.Approximately(_position, target)) return;
            _position = Mathf.MoveTowards(_position, target, _speed * Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            _knob.anchoredPosition = new Vector2(Mathf.Lerp(-_knobTravel, _knobTravel, _position), 0f);
            _track.color = Color.Lerp(_offColor, _onColor, _position);
        }
    }
}
