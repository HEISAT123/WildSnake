using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Опасность без лишних надписей: у края экрана — пульсирующая метка в сторону орла или лисы,
    /// а крупное «ОРЁЛ ПИКИРУЕТ» вспыхивает один раз за атаку и быстро гаснет.
    /// </summary>
    public class ThreatIndicator : MonoBehaviour
    {
        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SnakeController _snake;
        [SerializeField] private EagleHunter _eagle;
        [SerializeField] private FoxHunter _fox;

        [Tooltip("Камера: метки ставятся по направлению угрозы относительно взгляда")]
        [SerializeField] private Transform _camera;

        [Header("Метки")]
        [Tooltip("Метка орла: первый дочерний узел — стрелка (она поворачивается), второй — подпись")]
        [SerializeField] private RectTransform _eagleMarker;

        [Tooltip("Метка лисы")]
        [SerializeField] private RectTransform _foxMarker;

        [Tooltip("Полуоси эллипса, по которому ездят метки, пикселей (от центра экрана)")]
        [SerializeField] private Vector2 _radius = new Vector2(760f, 380f);

        [Header("Крупное предупреждение")]
        [SerializeField] private CanvasGroup _alertGroup;
        [SerializeField] private Text _alertText;

        [Tooltip("Сколько секунд висит предупреждение о пике")]
        [SerializeField, Min(0.2f)] private float _alertTime = 1.3f;

        private CanvasGroup _eagleGroup;
        private CanvasGroup _foxGroup;
        private Transform _eagleArrow;
        private Transform _foxArrow;
        private float _alertTimer;

        private void Awake()
        {
            _eagleGroup = _eagleMarker.GetComponent<CanvasGroup>();
            _foxGroup = _foxMarker.GetComponent<CanvasGroup>();
            _eagleArrow = _eagleMarker.GetChild(0);
            _foxArrow = _foxMarker.GetChild(0);
            _eagleGroup.alpha = 0f;
            _foxGroup.alpha = 0f;
            _alertGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            _eagle.DiveStarted += OnDiveStarted;
        }

        private void OnDisable()
        {
            _eagle.DiveStarted -= OnDiveStarted;
        }

        private void OnDiveStarted()
        {
            if (_game.State != GameState.Playing) return;
            _alertText.text = "ОРЁЛ ПИКИРУЕТ";
            _alertTimer = _alertTime;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool playing = _game.State == GameState.Playing;
            float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (_eagle.IsDiving ? 9f : 4f)));

            bool eagle = playing && _eagle.IsThreatening;
            UpdateMarker(_eagleMarker, _eagleArrow, _eagleGroup, eagle, _eagle.GroundPosition, pulse, dt);
            bool fox = playing && _fox.IsActive && _fox.CurrentPhase != FoxHunter.Phase.Retreat;
            UpdateMarker(_foxMarker, _foxArrow, _foxGroup, fox, _fox.Position, _fox.IsChasing ? pulse : 0.55f, dt);

            if (_alertTimer > 0f) _alertTimer -= dt;
            float target = playing && _alertTimer > 0f ? Mathf.Clamp01(_alertTimer / 0.3f) : 0f;
            _alertGroup.alpha = Mathf.MoveTowards(_alertGroup.alpha, target, dt * 8f);
        }

        /// <summary>Метка на эллипсе вокруг центра экрана в сторону угрозы (по взгляду камеры, вид сверху).</summary>
        private void UpdateMarker(RectTransform marker, Transform arrow, CanvasGroup group, bool active, Vector3 worldPosition, float alpha, float dt)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, active ? alpha : 0f, dt * 4f);
            if (!active) return;

            Vector3 local = _camera.InverseTransformDirection(worldPosition - _snake.HeadPosition);
            Vector2 dir = new Vector2(local.x, local.z);
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;
            dir.Normalize();
            marker.anchoredPosition = new Vector2(dir.x * _radius.x, dir.y * _radius.y);
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
