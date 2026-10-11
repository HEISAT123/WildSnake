using UnityEngine;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Где опасность — видно сразу. Метка орла или лисы — значок зверя, стрелка и расстояние:
    /// • зверь в кадре — метка висит прямо над ним, стрелка смотрит на него сверху;
    /// • за кадром или позади — метка прижата к краю экрана в той стороне, где зверь, стрелка указывает туда.
    /// Крупное «ОРЁЛ ПИКИРУЕТ» вспыхивает один раз за атаку и быстро гаснет.
    /// </summary>
    public class ThreatIndicator : MonoBehaviour
    {
        private const int MAX_DISTANCE_TEXT = 300;

        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SnakeController _snake;
        [SerializeField] private EagleHunter _eagle;
        [SerializeField] private FoxHunter _fox;

        [Tooltip("Камера: по ней метки проецируются на экран")]
        [SerializeField] private Transform _camera;

        [Header("Метки")]
        [Tooltip("Метка орла: дочерние узлы — Arrow (стрелка), Icon (значок), Label (подпись с расстоянием)")]
        [SerializeField] private RectTransform _eagleMarker;

        [Tooltip("Метка лисы")]
        [SerializeField] private RectTransform _foxMarker;

        [Tooltip("Отступ меток от края экрана, пикселей интерфейса (по X, по Y)")]
        [SerializeField] private Vector2 _edgeMargin = new Vector2(110f, 90f);

        [Tooltip("На сколько метка выше зверя, когда он в кадре, пикселей")]
        [SerializeField] private float _aboveTarget = 70f;

        [Tooltip("Насколько стрелка отстоит от центра значка, пикселей")]
        [SerializeField] private float _arrowOffset = 38f;

        [Header("Крупное предупреждение")]
        [SerializeField] private CanvasGroup _alertGroup;
        [SerializeField] private Text _alertText;

        [Tooltip("Сколько секунд висит предупреждение о пике")]
        [SerializeField, Min(0.2f)] private float _alertTime = 1.3f;

        private struct Marker
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform Arrow;
            public RectTransform Label;
            public Text Text;
            public string[] Distances;
            public int Shown;
        }

        private Camera _view;
        private RectTransform _area;
        private Marker _eagleUi;
        private Marker _foxUi;
        private float _alertTimer;

        private void Awake()
        {
            _view = _camera.GetComponent<Camera>();
            _area = (RectTransform)_eagleMarker.parent;
            _eagleUi = CreateMarker(_eagleMarker, "ОРЁЛ");
            _foxUi = CreateMarker(_foxMarker, "ЛИСА");
            _alertGroup.alpha = 0f;
        }

        /// <summary>Подписи «ЛИСА · 24 м» готовим заранее — в кадре строки не собираются.</summary>
        private static Marker CreateMarker(RectTransform root, string name)
        {
            var m = new Marker
            {
                Root = root,
                Group = root.GetComponent<CanvasGroup>(),
                Arrow = (RectTransform)root.Find("Arrow"),
                Label = (RectTransform)root.Find("Label"),
                Distances = new string[MAX_DISTANCE_TEXT + 1],
                Shown = -1
            };
            m.Text = m.Label.GetComponent<Text>();
            for (int i = 0; i < MAX_DISTANCE_TEXT; i++) m.Distances[i] = name + "  " + i + " м";
            m.Distances[MAX_DISTANCE_TEXT] = name + "  далеко";
            m.Group.alpha = 0f;
            return m;
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
            float pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (_eagle.IsDiving ? 9f : 4f)));

            bool eagle = playing && _eagle.IsThreatening;
            UpdateMarker(ref _eagleUi, eagle, _eagle.Position, pulse, dt);
            bool fox = playing && _fox.IsActive && _fox.CurrentPhase != FoxHunter.Phase.Retreat;
            // Лиса — точка на земле; метку целим чуть выше, в её голову.
            UpdateMarker(ref _foxUi, fox, _fox.Position + Vector3.up * 0.6f, _fox.IsChasing ? pulse : 0.8f, dt);

            if (_alertTimer > 0f) _alertTimer -= dt;
            float target = playing && _alertTimer > 0f ? Mathf.Clamp01(_alertTimer / 0.3f) : 0f;
            _alertGroup.alpha = Mathf.MoveTowards(_alertGroup.alpha, target, dt * 8f);
        }

        private void UpdateMarker(ref Marker m, bool active, Vector3 world, float alpha, float dt)
        {
            m.Group.alpha = Mathf.MoveTowards(m.Group.alpha, active ? alpha : 0f, dt * 4f);
            if (!active) return;

            Vector2 half = _area.rect.size * 0.5f;
            Vector2 inner = half - _edgeMargin;
            Vector3 vp = _view.WorldToViewportPoint(world);
            Vector2 local = new Vector2((vp.x - 0.5f) * 2f * half.x, (vp.y - 0.5f) * 2f * half.y);

            Vector2 position;
            Vector2 arrowDir;
            if (vp.z > 0f && Mathf.Abs(local.x) <= inner.x && Mathf.Abs(local.y + _aboveTarget) <= inner.y)
            {
                // В кадре: метка над зверем, стрелка вниз — на него.
                position = local + new Vector2(0f, _aboveTarget);
                arrowDir = Vector2.down;
            }
            else
            {
                // За кадром: позади камеры проекция зеркальна — разворачиваем, чтобы метка встала с нужной стороны.
                Vector2 dir = vp.z > 0f ? local : -local;
                if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                float t = Mathf.Min(inner.x / Mathf.Max(Mathf.Abs(dir.x), 1e-3f), inner.y / Mathf.Max(Mathf.Abs(dir.y), 1e-3f));
                position = dir * t;
                arrowDir = dir.normalized;
            }

            m.Root.anchoredPosition = position;
            m.Arrow.anchoredPosition = arrowDir * _arrowOffset;
            m.Arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(arrowDir.y, arrowDir.x) * Mathf.Rad2Deg - 90f);
            // Подпись — с противоположной от стрелки стороны по вертикали, чтобы они не наезжали.
            m.Label.anchoredPosition = new Vector2(0f, arrowDir.y < -0.5f ? 40f : -40f);

            Vector3 delta = world - _snake.HeadPosition;
            delta.y = 0f;
            int meters = Mathf.Min(MAX_DISTANCE_TEXT, Mathf.RoundToInt(delta.magnitude));
            if (meters != m.Shown)
            {
                m.Shown = meters;
                m.Text.text = m.Distances[meters];
            }
        }
    }
}
