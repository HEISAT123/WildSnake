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
        [Header("Змейка: движение")]
        [Tooltip("Скорость, когда змея крадётся (Ctrl), клеток в секунду")]
        [SerializeField, Min(0.2f)] private float _sneakSpeed = 1.5f;

        [Tooltip("Обычная скорость ползания, клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _walkSpeed = 3.4f;

        [Tooltip("Быстрое ползание (Shift), клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _sprintSpeed = 5.6f;

        [Tooltip("Разгон, клеток в секунду²")]
        [SerializeField, Min(0.5f)] private float _acceleration = 12f;

        [Tooltip("Торможение, клеток в секунду²")]
        [SerializeField, Min(0.5f)] private float _deceleration = 16f;

        [Tooltip("Скорость поворота головы на ходу, град/с: змея заходит дугой, а не разворачивается на месте")]
        [SerializeField, Min(10f)] private float _turnRate = 230f;

        [Header("Змейка: прыжок и бросок")]
        [Tooltip("Высота прыжка, клеток")]
        [SerializeField, Min(0.1f)] private float _jumpHeight = 1.4f;

        [Tooltip("Гравитация, клеток в секунду²")]
        [SerializeField, Min(1f)] private float _gravity = 30f;

        [Tooltip("Дальность быстрого укуса (ЛКМ без замаха), клеток")]
        [SerializeField, Min(0.5f)] private float _strikeDistance = 2.2f;

        [Tooltip("Дальность броска после полного замаха (ПКМ), клеток")]
        [SerializeField, Min(0.5f)] private float _chargedStrikeDistance = 5.5f;

        [Tooltip("Длительность броска, сек")]
        [SerializeField, Min(0.05f)] private float _strikeDuration = 0.26f;

        [Tooltip("Пауза между бросками, сек")]
        [SerializeField, Min(0f)] private float _strikeCooldown = 0.45f;

        [Tooltip("За сколько секунд замах набирает полную силу")]
        [SerializeField, Min(0.1f)] private float _coilChargeTime = 1.1f;

        [Tooltip("Насколько свёрнутая змея может повернуть голову для прицела, град")]
        [SerializeField, Range(10f, 120f)] private float _coilAimLimit = 75f;

        [Tooltip("Насколько быстрый укус может отклониться от курса к прицелу, град")]
        [SerializeField, Range(0f, 90f)] private float _biteAimLimit = 50f;

        [Header("Выносливость")]
        [Tooltip("Запас выносливости")]
        [SerializeField, Min(1f)] private float _maxStamina = 100f;

        [Tooltip("Восстановление выносливости в секунду")]
        [SerializeField, Min(0f)] private float _staminaRegen = 18f;

        [Tooltip("Пауза после траты, прежде чем выносливость начнёт восстанавливаться, сек")]
        [SerializeField, Min(0f)] private float _staminaRegenDelay = 0.6f;

        [Tooltip("Расход выносливости на быстрое ползание в секунду")]
        [SerializeField, Min(0f)] private float _sprintStaminaDrain = 16f;

        [Tooltip("Цена быстрого укуса в выносливости")]
        [SerializeField, Min(0f)] private float _strikeStaminaCost = 14f;

        [Tooltip("Цена броска после полного замаха в выносливости")]
        [SerializeField, Min(0f)] private float _chargedStrikeStaminaCost = 34f;

        [Tooltip("Восстановление выносливости у голодной змеи (доля от обычного при пустой сытости)")]
        [SerializeField, Range(0f, 1f)] private float _hungryStaminaRegen = 0.4f;

        [Header("Засада и промах")]
        [Tooltip("Сколько секунд нужно не двигаться, чтобы змея начала сливаться с местностью")]
        [SerializeField, Min(0f)] private float _ambushDelay = 2f;

        [Tooltip("За сколько секунд маскировка становится полной")]
        [SerializeField, Min(0f)] private float _ambushRamp = 1f;

        [Tooltip("Множитель заметности затаившейся змеи на открытом месте")]
        [SerializeField, Range(0.05f, 1f)] private float _ambushAwareness = 0.35f;

        [Tooltip("Множитель заметности затаившейся змеи в траве")]
        [SerializeField, Range(0.05f, 1f)] private float _ambushCoverAwareness = 0.2f;

        [Tooltip("Радиус, с которого затаившаяся змея подманивает добычу кончиком хвоста, клеток")]
        [SerializeField, Min(0f)] private float _lureRadius = 8f;

        [Tooltip("Сколько секунд змея «открыта» после промаха броском")]
        [SerializeField, Min(0f)] private float _missExposure = 1f;

        [Tooltip("Скорость «открытой» змеи после промаха (множитель)")]
        [SerializeField, Range(0.1f, 1f)] private float _missSlowdown = 0.55f;

        [Tooltip("Насколько заметнее «открытая» змея после промаха (множитель радиуса)")]
        [SerializeField, Min(1f)] private float _missAwareness = 1.6f;

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
        [SerializeField] private PreyStats[] _prey = CreateDefaultPrey();

        [Tooltip("Размер бугра при переваривании по видам (порядок как в PreyKind; 0 — бугра нет, 1 — кролик)")]
        [SerializeField] private float[] _digestSize = { 0f, 0.3f, 0.3f, 0.45f, 1f };

        [Tooltip("За сколько секунд бугор размера 1 проходит всё тело")]
        [SerializeField, Min(1f)] private float _digestSeconds = 28f;

        [Header("Обвивание")]
        [Tooltip("Сколько секунд держать F, чтобы задушить крупную добычу")]
        [SerializeField, Min(0.2f)] private float _constrictDuration = 2.6f;

        [Tooltip("Расход выносливости в секунду, пока змея сжимает добычу")]
        [SerializeField, Min(0f)] private float _constrictStaminaDrain = 22f;

        [Tooltip("Сколько секунд добыча терпит без сжатия, прежде чем вырваться")]
        [SerializeField, Min(0.1f)] private float _constrictPatience = 1.5f;

        [Tooltip("Насколько хуже замечают змею в высокой траве (множитель радиуса)")]
        [SerializeField, Range(0.05f, 1f)] private float _coverAwareness = 0.45f;

        [Tooltip("Насколько лучше замечают змею во время броска (множитель радиуса)")]
        [SerializeField, Min(1f)] private float _dashAwareness = 1.4f;

        [Tooltip("Насколько хуже замечают крадущуюся змею (множитель радиуса)")]
        [SerializeField, Range(0.05f, 1f)] private float _sneakAwareness = 0.45f;

        [Tooltip("Насколько лучше замечают быстро ползущую змею (множитель радиуса)")]
        [SerializeField, Min(1f)] private float _sprintAwareness = 1.3f;

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

        public float SneakSpeed => _sneakSpeed;
        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float Acceleration => _acceleration;
        public float Deceleration => _deceleration;
        public float TurnRate => _turnRate;
        public float JumpHeight => _jumpHeight;
        public float Gravity => _gravity;
        public float StrikeDistance => _strikeDistance;
        public float ChargedStrikeDistance => _chargedStrikeDistance;
        public float StrikeDuration => _strikeDuration;
        public float StrikeCooldown => _strikeCooldown;
        public float CoilChargeTime => _coilChargeTime;
        public float CoilAimLimit => _coilAimLimit;
        public float BiteAimLimit => _biteAimLimit;
        public float MaxStamina => _maxStamina;
        public float StaminaRegen => _staminaRegen;
        public float StaminaRegenDelay => _staminaRegenDelay;
        public float SprintStaminaDrain => _sprintStaminaDrain;
        public float HungryStaminaRegen => _hungryStaminaRegen;
        public float AmbushDelay => _ambushDelay;
        public float AmbushRamp => _ambushRamp;
        public float AmbushAwareness => _ambushAwareness;
        public float AmbushCoverAwareness => _ambushCoverAwareness;
        public float LureRadius => _lureRadius;
        public float MissExposure => _missExposure;
        public float MissSlowdown => _missSlowdown;
        public float MissAwareness => _missAwareness;

        /// <summary>Цена броска в выносливости: чем сильнее замах, тем дороже.</summary>
        public float StrikeStaminaCost(float charge01)
        {
            return Mathf.Lerp(_strikeStaminaCost, _chargedStrikeStaminaCost, Mathf.Clamp01(charge01));
        }

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
        public float SneakAwareness => _sneakAwareness;
        public float SprintAwareness => _sprintAwareness;
        public float SleepAwareness => _sleepAwareness;
        public int PreyKindCount => Mathf.Max(_prey.Length, DefaultPreyCount);
        public float DigestSeconds => _digestSeconds;
        public float ConstrictDuration => _constrictDuration;
        public float ConstrictStaminaDrain => _constrictStaminaDrain;
        public float ConstrictPatience => _constrictPatience;

        /// <summary>Крупную добычу (кролика) берут только обвиванием: бросок лишь хватает её.</summary>
        public bool RequiresConstrict(PreyKind kind) => kind == PreyKind.Rabbit;

        /// <summary>Размер бугра при переваривании (0 — без бугра).</summary>
        public float DigestSize(PreyKind kind)
        {
            int i = (int)kind;
            return i < _digestSize.Length ? _digestSize[i] : 0f;
        }

        public float DaySeconds => _daySeconds;
        public float NightSeconds => _nightSeconds;
        public float StartPhase => _startPhase;

        public float EagleFirstDelay => _eagleFirstDelay;
        public float EagleIntervalMin => _eagleIntervalMin;
        public float EagleIntervalMax => Mathf.Max(_eagleIntervalMin, _eagleIntervalMax);
        public float EagleDamage => _eagleDamage;

        public float RivalRespawnDelay => _rivalRespawnDelay;
        public float RivalMaxHealth => _rivalMaxHealth;

        /// <summary>Скорость темпа, клеток в секунду.</summary>
        public float PaceSpeed(SnakePace pace)
        {
            switch (pace)
            {
                case SnakePace.Sneak: return _sneakSpeed;
                case SnakePace.Sprint: return _sprintSpeed;
                default: return _walkSpeed;
            }
        }

        /// <summary>Скорость броска (клеток в секунду), чтобы за время броска пролететь заданную дальность.</summary>
        public float StrikeSpeed(float charge01)
        {
            float distance = Mathf.Lerp(_strikeDistance, _chargedStrikeDistance, Mathf.Clamp01(charge01));
            return distance / _strikeDuration;
        }

        /// <summary>Параметры вида. Если ассет старый и вида в нём ещё нет — значения по умолчанию.</summary>
        public PreyStats GetPrey(PreyKind kind)
        {
            int i = (int)kind;
            if (i < _prey.Length) return _prey[i];
            if (_fallbackPrey == null) _fallbackPrey = CreateDefaultPrey();
            return _fallbackPrey[i];
        }

        /// <summary>
        /// Дописать в ассет виды добычи, появившиеся после его создания (вызывает SnakeSceneBuilder).
        /// Уже настроенные значения не трогаются. Возвращает true, если ассет изменился.
        /// </summary>
        public bool EnsurePreyDefaults()
        {
            PreyStats[] defaults = CreateDefaultPrey();
            if (_prey.Length >= defaults.Length) return false;
            var merged = new PreyStats[defaults.Length];
            for (int i = 0; i < merged.Length; i++) merged[i] = i < _prey.Length ? _prey[i] : defaults[i];
            _prey = merged;
            return true;
        }

        private const int DefaultPreyCount = 5;
        private PreyStats[] _fallbackPrey;

        private static PreyStats[] CreateDefaultPrey()
        {
            return new[]
            {
                new PreyStats(7f, 0.25f, 0.6f, 0f, 0f, 1f, false, 7, 5),      // Жук
                new PreyStats(22f, 0.7f, 1.4f, 5.6f, 4.5f, 2.2f, true, 3, 6),  // Мышь
                new PreyStats(26f, 0.8f, 0f, 4.4f, 3.5f, 2.4f, true, 3, 4),    // Лягушка
                new PreyStats(40f, 1.2f, 0.9f, 7f, 6f, 3f, true, 3, 2),        // Птица
                new PreyStats(70f, 1.8f, 1.6f, 6.4f, 5f, 3.2f, true, 2, 1)     // Кролик
            };
        }
    }
}
