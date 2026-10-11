using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Карта по Tab: подложка, нарисованная по данным поля (MapPainter), и значки поверх неё —
    /// стрелки змей по направлению головы, следы добычи, рыбы и жуки, орёл и лиса, норы, гнёзда, солнечные камни.
    /// Неподвижные значки ставятся один раз при старте, подвижные двигаются только пока карта видна.
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
        [SerializeField, Range(1, 8)] private int _pixelsPerCell = 4;

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

        private void Start()
        {
            _texture = MapPainter.Paint(_grid, _pixelsPerCell);
            _map.texture = _texture;
            _map.color = Color.white;
            _origin = _grid.CornerOrigin;
            _worldSize = new Vector2(_grid.Width * _grid.CellSize, _grid.Height * _grid.CellSize);
            _mapSize = ((RectTransform)_map.transform).rect.size;

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
            if (_texture != null) Destroy(_texture);
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
            if (_group.alpha <= 0f) return;
            float t = Time.unscaledTime;

            SetArrow(_playerIcon, _player, true);
            _playerRing.anchoredPosition = _playerIcon.anchoredPosition;
            float pulse = 1f + 0.22f * Mathf.Sin(t * 5f);
            _playerRing.localScale = new Vector3(pulse, pulse, 1f);

            SetArrow(_rivalIcon, _rival, _rival.gameObject.activeInHierarchy);
            SetThreat(_eagleIcon, _eagle.CurrentPhase != EagleHunter.Phase.Away, _eagle.GroundPosition);
            SetThreat(_foxIcon, _fox.IsActive, _fox.Position);

            int count = Mathf.Min(_preyIcons.Length, _wildlife.AnimalCount);
            for (int i = 0; i < count; i++)
            {
                PreyAnimal animal = _wildlife.GetAnimal(i);
                RectTransform icon = _preyIcons[i].rectTransform;
                bool show = animal.IsActive;
                if (icon.gameObject.activeSelf != show) icon.gameObject.SetActive(show);
                if (show) icon.anchoredPosition = ToMap(animal.Position);
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

        /// <summary>Мировая точка → позиция на карте (центр карты — ноль, вверх — +Z мира).</summary>
        private Vector2 ToMap(Vector3 world)
        {
            float u = (world.x - _origin.x) / _worldSize.x - 0.5f;
            float v = (world.z - _origin.z) / _worldSize.y - 0.5f;
            return new Vector2(u * _mapSize.x, v * _mapSize.y);
        }
    }
}
