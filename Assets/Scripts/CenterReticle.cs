using UnityEngine;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Центр экрана: тонкий прицел появляется только при замахе (ПКМ) и показывает силу броска кольцом,
    /// а при обвивании — кольцо борьбы, которое наполняется, пока зажата F.
    /// </summary>
    public class CenterReticle : MonoBehaviour
    {
        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SnakeController _snake;

        [Header("Прицел")]
        [SerializeField] private CanvasGroup _aimGroup;

        [Tooltip("Кольцо силы замаха (Image Filled Radial360)")]
        [SerializeField] private Image _chargeFill;

        [Header("Борьба")]
        [SerializeField] private CanvasGroup _struggleGroup;

        [Tooltip("Кольцо борьбы (Image Filled Radial360)")]
        [SerializeField] private Image _struggleFill;

        [Header("Цвета")]
        [SerializeField] private Color _readyColor = new Color(0.78f, 0.96f, 0.2f);
        [SerializeField] private Color _chargingColor = new Color(0.95f, 0.93f, 0.88f);
        [SerializeField] private Color _tiredColor = new Color(1f, 0.42f, 0.3f);

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool playing = _game.State == GameState.Playing;

            bool aiming = playing && _snake.IsCoiled;
            _aimGroup.alpha = Mathf.MoveTowards(_aimGroup.alpha, aiming ? 1f : 0f, dt * 8f);
            float charge = _snake.Coil01;
            _chargeFill.fillAmount = charge;
            _chargeFill.color = _snake.Stamina.IsExhausted ? _tiredColor : charge >= 1f ? _readyColor : _chargingColor;

            Constriction c = _game.Constriction;
            bool struggling = playing && c.IsActive;
            _struggleGroup.alpha = Mathf.MoveTowards(_struggleGroup.alpha, struggling ? 1f : 0f, dt * 6f);
            _struggleFill.fillAmount = c.Progress01;
            _struggleFill.color = _snake.Stamina.Value01 < 0.25f ? _tiredColor : _readyColor;
        }
    }
}
