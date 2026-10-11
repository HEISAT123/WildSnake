using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Карта: подложка, нарисованная по данным поля (MapPainter), и значки поверх неё —
    /// стрелки змей по направлению головы, следы добычи, рыбы и жуки, орёл и лиса, норы, гнёзда, солнечные камни.
    /// Два режима одного компонента:
    /// • карта журнала (Tab) — всё поле целиком, север вверху;
    /// • мини-карта HUD (_follow) — круглое окно вокруг змеи, повёрнутое по камере (вверху — куда смотрит камера),
    ///   значки стоят прямо, видно только ближние.
    /// Подложку рисует один экземпляр, второй берёт её у него (_textureSource), чтобы не рисовать дважды.
    /// </summary>
    public class MapIcons : MonoBehaviour
    {
        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private GridManager _grid;
        [SerializeField] private SnakeController _player;
        [SerializeField] private SnakeController _rival;
        [SerializeField] private WildlifeManager _wildlife;
        [SerializeField] private EagleHunter _eagle;
        [SerializeField] private FoxHunter _fox;

        [Header("Карта")]
        [Tooltip("Сюда ставится нарисованная подложка")]
        [SerializeField] private RawImage _map;

        [Tooltip("Видимость карты: пока она 0, значки не двигаем")]
        [SerializeField] private CanvasGroup _group;

        [Tooltip("Пикселей подложки на клетку поля")]
        [SerializeField, Range(1, 12)] private int _pixelsPerCell = 8;

        [Tooltip("Чужая карта, у которой берётся уже нарисованная подложка (для мини-карты)")]
        [SerializeField] private MapIcons _textureSource;

        [Header("Мини-карта")]
        [Tooltip("Окно вокруг змеи, повёрнутое по камере; иначе — всё поле, север вверху")]
        [SerializeField] private bool _follow;

        [Tooltip("Камера: мини-карта поворачивается вслед за ней")]
        [SerializeField] private CameraRig _camera;

        [Tooltip("Узел, который поворачивается по камере (родитель подложки)")]
        [SerializeField] private RectTransform _rotator;

        [Tooltip("Сколько метров от змеи до края круга")]
        [SerializeField, Min(5f)] private float _viewRadius = 32f;

        [Tooltip("Радиус круга мини-карты, пикселей интерфейса")]
        [SerializeField, Min(10f)] private float _windowRadius = 120f;

        [Tooltip("Метка «С» (север) на краю мини-карты")]
        [SerializeField] private RectTransform _north;

        [Tooltip("Настройки: мини-карту можно выключить")]
        [SerializeField] private SettingsService _settings;

        [Header("Подвижные значки")]
        [SerializeField] private RectTransform _playerIcon;
        [Tooltip("Пульсирующее кольцо вокруг вашей стрелки")]
        [SerializeField] private RectTransform _playerRing;
        [SerializeField] private RectTransform _rivalIcon;
        [SerializeField] private RectTransform _eagleIcon;
        [SerializeField] private RectTransform _foxIcon;
        [Tooltip("По значку на каждое животное пула WildlifeManager, в том же порядке")]
        [SerializeField] private Image[] _preyIcons = new Image[0];

        [Header("Неподвижные значки")]
        [SerializeField] private RectTransform _nestIcon;
        [Tooltip("Кольцо вокруг гнезда, пока к нему надо идти")]
        [SerializeField] private RectTransform _nestRing;
        [SerializeField] private RectTransform[] _burrowIcons = new RectTransform[0];
        [SerializeField] private RectTransform[] _birdNestIcons = new RectTransform[0];
        [SerializeField] private RectTransform[] _sunStoneIcons = new RectTransform[0];

        [Header("Добыча")]
        [SerializeField] private Sprite _pawSprite;
        [SerializeField] private Sprite _fishSprite;
        [SerializeField] private Sprite _beetleSprite;
        [SerializeField] private Color _preyColor = new Color(1f, 0.9f, 0.55f);
        [SerializeField] private Color _fishColor = new Color(0.72f, 0.92f, 1f);
        [Tooltip("Ёж колется — его след приглушён")]
        [SerializeField] private Color _hedgehogColor = new Color(0.72f, 0.68f, 0.62f);
        [SerializeField] private float _preySize = 18f;
        [SerializeField] private float _beetleSize = 12f;

        private Texture2D _texture;
        private Vector3 _origin;
        private Vector2 _worldSize;
        private Vector2 _mapSize;
        private Quaternion _upright = Quaternion.identity;

        /// <summary>Подложка карты (рисуется при первом обращении).</summary>
        public Texture2D Texture
        {
            get
            {
                if (_texture == null) _texture = _textureSource != null ? _textureSource.Texture : MapPainter.Paint(_grid, _pixelsPerCell);
                return _texture;
            }
        }

        private void Start()
        {
            _map.texture = Texture;
            _map.color = Color.white;
            _origin = _grid.CornerOrigin;
            _worldSize = new Vector2(_grid.Width * _grid.CellSize, _grid.Height * _grid.CellSize);
            if (_follow)
            {
                // Масштаб мини-карты: радиус круга — это _viewRadius метров.
                float pixelsPerMeter = _windowRadius / _viewRadius;
                _map.rectTransform.sizeDelta = _worldSize * pixelsPerMeter;
            }
            _mapSize = _map.rectTransform.rect.size;
            if (_follow) _mapSize = _map.rectTransform.sizeDelta;

            for (int i = 0; i < _burrowIcons.Length; i++) PlaceStatic(_burrowIcons[i], i < _grid.BurrowCount, i < _grid.BurrowCount ? _grid.GetBurrow(i) : Vector3.zero);
            for (int i = 0; i < _birdNestIcons.Length; i++) PlaceStatic(_birdNestIcons[i], i < _grid.BirdNestCount, i < _grid.BirdNestCount ? _grid.GetBirdNest(i) : Vector3.zero);
            for (int i = 0; i < _sunStoneIcons.Length; i++)
            {
                bool has = i < _grid.SunStoneCount;
                // Солнечный камень 2x2: клетка — его левый нижний угол, значок — в центре камня.
                Vector3 stone = has ? _grid.CellToWorld(_grid.GetSunStone(i)) + new Vector3(0.5f, 0f, 0.5f) * _grid.CellSize : Vector3.zero;
                PlaceStatic(_sunStoneIcons[i], has, stone);
            }
            PlaceStatic(_nestIcon, _grid.HasNest, _grid.HasNest ? _grid.NestPosition : Vector3.zero);
            if (_nestRing != null) _nestRing.anchoredPosition = _nestIcon.anchoredPosition;

            SetupPreyIcons();
        }

        private void OnDestroy()
        {
            // Подложку уничтожает тот, кто её нарисовал.
            if (_texture != null && _textureSource == null) Destroy(_texture);
        }

        private void SetupPreyIcons()
        {
            int count = _wildlife.AnimalCount;
            for (int i = 0; i < _preyIcons.Length; i++)
            {
                Image icon = _preyIcons[i];
                if (i >= count)
                {
                    icon.gameObject.SetActive(false);
                    continue;
                }
                PreyKind kind = _wildlife.GetAnimal(i).Kind;
                bool fish = kind == PreyKind.Fish, beetle = kind == PreyKind.Beetle;
                icon.sprite = fish ? _fishSprite : beetle ? _beetleSprite : _pawSprite;
                icon.color = fish ? _fishColor : kind == PreyKind.Hedgehog ? _hedgehogColor : _preyColor;
                float size = beetle ? _beetleSize : _preySize;
                icon.rectTransform.sizeDelta = new Vector2(size, size);
            }
        }

        private void Update()
        {
            if (_settings != null) _group.alpha = _settings.ShowMinimap ? 1f : 0f;
            if (_group.alpha <= 0f) return;
            float t = Time.unscaledTime;

            if (_follow) UpdateWindow();

            SetArrow(_playerIcon, _player, true);
            _playerRing.anchoredPosition = _playerIcon.anchoredPosition;
            float pulse = 1f + 0.22f * Mathf.Sin(t * 5f);
            _playerRing.localScale = new Vector3(pulse, pulse, 1f);

            SetArrow(_rivalIcon, _rival, _rival.gameObject.activeInHierarchy);
            SetThreat(_eagleIcon, _eagle.CurrentPhase != EagleHunter.Phase.Away, _eagle.GroundPosition);
            SetThreat(_foxIcon, _fox.IsActive, _fox.Position);

            Vector3 head = _player.HeadPosition;
            float range = _viewRadius * 1.15f;
            int count = Mathf.Min(_preyIcons.Length, _wildlife.AnimalCount);
            for (int i = 0; i < count; i++)
            {
                PreyAnimal animal = _wildlife.GetAnimal(i);
                RectTransform icon = _preyIcons[i].rectTransform;
                Vector3 p = animal.Position;
                // На мини-карте дальнюю добычу не двигаем — она всё равно за краем круга.
                bool show = animal.IsActive && (!_follow || (Mathf.Abs(p.x - head.x) < range && Mathf.Abs(p.z - head.z) < range));
                if (icon.gameObject.activeSelf != show) icon.gameObject.SetActive(show);
                if (!show) continue;
                icon.anchoredPosition = ToMap(p);
                if (_follow) icon.localRotation = _upright;
            }

            // Гнездо появляется на карте, когда змея повзрослела, и подсвечено кольцом, пока кладку не охраняют.
            QuestStage stage = _game.Quest.Stage;
            bool nest = _grid.HasNest && stage != QuestStage.Grow;
            if (_nestIcon.gameObject.activeSelf != nest) _nestIcon.gameObject.SetActive(nest);
            bool ring = nest && stage == QuestStage.Nest;
            if (_nestRing.gameObject.activeSelf != ring) _nestRing.gameObject.SetActive(ring);
            if (ring)
            {
                float k = 1f + 0.3f * (0.5f + 0.5f * Mathf.Sin(t * 3f));
                _nestRing.localScale = new Vector3(k, k, 1f);
            }
        }

        /// <summary>Мини-карта: змея в центре, верх — направление камеры, значки стоят прямо.</summary>
        private void UpdateWindow()
        {
            float yaw = _camera.Yaw;
            _rotator.localRotation = Quaternion.Euler(0f, 0f, yaw);
            _upright = Quaternion.Euler(0f, 0f, -yaw);
            _map.rectTransform.anchoredPosition = -ToMap(_player.HeadPosition);
            if (_north != null)
            {
                _north.anchoredPosition = Quaternion.Euler(0f, 0f, yaw) * new Vector3(0f, _windowRadius - 14f, 0f);
            }
            for (int i = 0; i < _burrowIcons.Length; i++) _burrowIcons[i].localRotation = _upright;
            for (int i = 0; i < _birdNestIcons.Length; i++) _birdNestIcons[i].localRotation = _upright;
            for (int i = 0; i < _sunStoneIcons.Length; i++) _sunStoneIcons[i].localRotation = _upright;
            _nestIcon.localRotation = _upright;
            _eagleIcon.localRotation = _upright;
            _foxIcon.localRotation = _upright;
        }

        private void SetArrow(RectTransform icon, SnakeController snake, bool active)
        {
            bool show = active && snake.IsAlive;
            if (icon.gameObject.activeSelf != show) icon.gameObject.SetActive(show);
            if (!show) return;
            icon.anchoredPosition = ToMap(snake.HeadPosition);
            Vector3 f = snake.Forward;
            // Вверх на карте — +Z мира; угол по часовой от севера, а поворот UI — против часовой.
            icon.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
        }

        private void SetThreat(RectTransform icon, bool active, Vector3 position)
        {
            if (icon.gameObject.activeSelf != active) icon.gameObject.SetActive(active);
            if (active) icon.anchoredPosition = ToMap(position);
        }

        private void PlaceStatic(RectTransform icon, bool active, Vector3 position)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(active);
            if (active) icon.anchoredPosition = ToMap(position);
        }

        /// <summary>Мировая точка → позиция на подложке (центр подложки — ноль, вверх — +Z мира).</summary>
        private Vector2 ToMap(Vector3 world)
        {
            float u = (world.x - _origin.x) / _worldSize.x - 0.5f;
            float v = (world.z - _origin.z) / _worldSize.y - 0.5f;
            return new Vector2(u * _mapSize.x, v * _mapSize.y);
        }
    }
}
