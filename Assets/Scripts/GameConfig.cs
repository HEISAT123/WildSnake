using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Все параметры баланса выживания в одном ассете: движение змеи, голод и здоровье,
    /// нюх, линька, добыча, смена дня и ночи, орёл. Подставляется в GameManager и системы.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Snake3D/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Змейка: скорость и руль")]
        [Tooltip("Скорость ползания, клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _moveSpeed = 4.6f;

        [Tooltip("Скорость поворота при руле до упора, град/с")]
        [SerializeField, Min(10f)] private float _turnSpeed = 190f;

        [Tooltip("Как быстро руль доходит до упора, 1/с")]
        [SerializeField, Min(0.5f)] private float _steerResponse = 6f;

        [Header("Змейка: прыжок и бросок")]
        [Tooltip("Высота прыжка, клеток")]
        [SerializeField, Min(0.1f)] private float _jumpHeight = 1.4f;

        [Tooltip("Гравитация, клеток в секунду²")]
        [SerializeField, Min(1f)] private float _gravity = 30f;

        [Tooltip("Множитель скорости во время броска")]
        [SerializeField, Min(1f)] private float _dashMultiplier = 2.4f;

        [Tooltip("Длительность броска, сек")]
        [SerializeField, Min(0.05f)] private float _dashDuration = 0.35f;

        [Tooltip("Перезарядка броска, сек")]
        [SerializeField, Min(0.1f)] private float _dashCooldown = 2.2f;

        [Header("Змейка: длина")]
        [Tooltip("Начальная (и минимальная) длина тела, клеток")]
        [SerializeField, Min(1f)] private float _startLength = 3f;

        [Tooltip("Максимальная длина тела, клеток")]
        [SerializeField, Min(2f)] private float _maxLength = 120f;

        [Header("Выживание: здоровье")]
        [Tooltip("Максимум здоровья")]
        [SerializeField, Min(1f)] private float _maxHealth = 100f;

        [Tooltip("Урон от удара о стену, камень или дерево")]
        [SerializeField, Min(0f)] private float _wallDamage = 15f;

        [Tooltip("Урон от удара головой о тело (своё или соперницы)")]
        [SerializeField, Min(0f)] private float _bodyDamage = 20f;

        [Tooltip("Восстановление здоровья в секунду, пока змея сыта")]
        [SerializeField, Min(0f)] private float _regenPerSecond = 2.5f;

        [Tooltip("Доля сытости, начиная с которой идёт восстановление")]
        [SerializeField, Range(0f, 1f)] private float _wellFedThreshold = 0.6f;

        [Header("Выживание: голод")]
        [Tooltip("Максимум сытости")]
        [SerializeField, Min(1f)] private float _maxSatiety = 100f;

        [Tooltip("Сытость в начале партии")]
        [SerializeField, Min(0f)] private float _startSatiety = 80f;

        [Tooltip("Расход сытости в секунду")]
        [SerializeField, Min(0f)] private float _hungerPerSecond = 1.1f;

        [Tooltip("Множитель голода ночью (холодно)")]
        [SerializeField, Min(0f)] private float _nightHungerMultiplier = 1.25f;

        [Tooltip("Цена броска в сытости")]
        [SerializeField, Min(0f)] private float _dashHungerCost = 4f;

        [Tooltip("Цена прыжка в сытости")]
        [SerializeField, Min(0f)] private float _jumpHungerCost = 1.5f;

        [Tooltip("При пустой сытости тело худеет, клеток в секунду")]
        [SerializeField, Min(0f)] private float _starveShrinkPerSecond = 0.3f;

        [Tooltip("Урон в секунду от голода, когда худеть уже некуда")]
        [SerializeField, Min(0f)] private float _starveDamagePerSecond = 6f;

        [Header("Нюх")]
        [Tooltip("Радиус, в котором язык чует добычу, клеток")]
        [SerializeField, Min(1f)] private float _smellRadius = 22f;

        [Tooltip("Сколько секунд виден след запаха")]
        [SerializeField, Min(0.5f)] private float _smellDuration = 3.5f;

        [Tooltip("Перезарядка нюха, сек")]
        [SerializeField, Min(0.5f)] private float _smellCooldown = 6f;

        [Header("Линька")]
        [Tooltip("Сколько клеток роста нужно до следующей линьки")]
        [SerializeField, Min(0.5f)] private float _moltGrowth = 4.5f;

        [Header("Добыча")]
        [Tooltip("Радиус поимки касанием, клеток")]
        [SerializeField, Min(0.1f)] private float _touchCatchRadius = 0.8f;

        [Tooltip("Радиус поимки броском, клеток")]
        [SerializeField, Min(0.1f)] private float _strikeCatchRadius = 1.3f;

        [Tooltip("Параметры видов добычи (порядок как в PreyKind)")]
        [SerializeField] private PreyStats[] _prey =
        {
            new PreyStats(7f, 0.25f, 0.6f, 0f, 0f, 1f, false, 7, 5),     // Жук
            new PreyStats(22f, 0.7f, 1.4f, 5.6f, 4.5f, 2.2f, true, 3, 6), // Мышь
            new PreyStats(26f, 0.8f, 0f, 4.4f, 3.5f, 2.4f, true, 3, 4),   // Лягушка
            new PreyStats(40f, 1.2f, 0.9f, 7f, 6f, 3f, true, 3, 2)        // Птица
        };

        [Tooltip("Насколько хуже замечают змею в высокой траве (множитель радиуса)")]
        [SerializeField, Range(0.05f, 1f)] private float _coverAwareness = 0.45f;

        [Tooltip("Насколько лучше замечают змею во время броска (множитель радиуса)")]
        [SerializeField, Min(1f)] private float _dashAwareness = 1.4f;

        [Tooltip("Насколько хуже замечают спящие ночью птицы (множитель радиуса)")]
        [SerializeField, Range(0.05f, 1f)] private float _sleepAwareness = 0.35f;

        [Header("День и ночь")]
        [Tooltip("Длительность дня, сек")]
        [SerializeField, Min(5f)] private float _daySeconds = 150f;

        [Tooltip("Длительность ночи, сек")]
        [SerializeField, Min(5f)] private float _nightSeconds = 70f;

        [Tooltip("Время суток в начале партии (0 — полночь, 0.25 — рассвет, 0.5 — полдень, 0.75 — закат)")]
        [SerializeField, Range(0f, 1f)] private float _startPhase = 0.33f;

        [Header("Орёл")]
        [Tooltip("Через сколько секунд после старта появляется первый орёл")]
        [SerializeField, Min(0f)] private float _eagleFirstDelay = 35f;

        [Tooltip("Пауза между охотами орла, сек (мин)")]
        [SerializeField, Min(1f)] private float _eagleIntervalMin = 28f;

        [Tooltip("Пауза между охотами орла, сек (макс)")]
        [SerializeField, Min(1f)] private float _eagleIntervalMax = 50f;

        [Tooltip("Урон от удара орла")]
        [SerializeField, Min(0f)] private float _eagleDamage = 35f;

        [Header("Соперница")]
        [Tooltip("Задержка возрождения змеи-соперницы, сек")]
        [SerializeField, Min(0f)] private float _rivalRespawnDelay = 6f;

        [Tooltip("Здоровье змеи-соперницы")]
        [SerializeField, Min(1f)] private float _rivalMaxHealth = 60f;

        public float MoveSpeed => _moveSpeed;
        public float TurnSpeed => _turnSpeed;
        public float SteerResponse => _steerResponse;
        public float JumpHeight => _jumpHeight;
        public float Gravity => _gravity;
        public float DashMultiplier => _dashMultiplier;
        public float DashDuration => _dashDuration;
        public float DashCooldown => _dashCooldown;
        public float StartLength => _startLength;
        public float MaxLength => _maxLength;

        public float MaxHealth => _maxHealth;
        public float WallDamage => _wallDamage;
        public float BodyDamage => _bodyDamage;
        public float RegenPerSecond => _regenPerSecond;
        public float WellFedThreshold => _wellFedThreshold;
        public float MaxSatiety => _maxSatiety;
        public float StartSatiety => _startSatiety;
        public float HungerPerSecond => _hungerPerSecond;
        public float NightHungerMultiplier => _nightHungerMultiplier;
        public float DashHungerCost => _dashHungerCost;
        public float JumpHungerCost => _jumpHungerCost;
        public float StarveShrinkPerSecond => _starveShrinkPerSecond;
        public float StarveDamagePerSecond => _starveDamagePerSecond;

        public float SmellRadius => _smellRadius;
        public float SmellDuration => _smellDuration;
        public float SmellCooldown => _smellCooldown;
        public float MoltGrowth => _moltGrowth;

        public float TouchCatchRadius => _touchCatchRadius;
        public float StrikeCatchRadius => _strikeCatchRadius;
        public float CoverAwareness => _coverAwareness;
        public float DashAwareness => _dashAwareness;
        public float SleepAwareness => _sleepAwareness;
        public int PreyKindCount => _prey.Length;

        public float DaySeconds => _daySeconds;
        public float NightSeconds => _nightSeconds;
        public float StartPhase => _startPhase;

        public float EagleFirstDelay => _eagleFirstDelay;
        public float EagleIntervalMin => _eagleIntervalMin;
        public float EagleIntervalMax => Mathf.Max(_eagleIntervalMin, _eagleIntervalMax);
        public float EagleDamage => _eagleDamage;

        public float RivalRespawnDelay => _rivalRespawnDelay;
        public float RivalMaxHealth => _rivalMaxHealth;

        public PreyStats GetPrey(PreyKind kind)
        {
            return _prey[(int)kind];
        }
    }
}
