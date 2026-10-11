using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Все параметры баланса выживания в одном ассете: движение змеи, голод и здоровье,
    /// нюх, линька, добыча, смена дня и ночи, орёл. Подставляется в GameManager и системы.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "WildSnake/Game Config")]
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

        [Header("Вода и температура")]
        [Tooltip("Скорость в воде (множитель): змея плывёт медленнее")]
        [SerializeField, Range(0.1f, 1f)] private float _swimSpeed = 0.6f;

        [Tooltip("Температура в начале партии (0 — окоченела, 1 — прогрета)")]
        [SerializeField, Range(0f, 1f)] private float _startTemperature = 0.7f;

        [Tooltip("Скорость нагрева, долей в секунду (на солнечном камне — втрое быстрее)")]
        [SerializeField, Min(0f)] private float _heatRate = 0.05f;

        [Tooltip("Скорость остывания, долей в секунду (в воде — в 2.5 раза быстрее)")]
        [SerializeField, Min(0f)] private float _coolRate = 0.022f;

        [Tooltip("Насколько замёрзшая змея медленнее ползает и короче бросается")]
        [SerializeField, Range(0f, 0.9f)] private float _coldSlowdown = 0.3f;

        [Header("Норы")]
        [Tooltip("С какого расстояния можно войти в нору, клеток")]
        [SerializeField, Min(0.3f)] private float _burrowEnterRadius = 1.6f;

        [Tooltip("Во сколько раз быстрее идёт ночь, пока змея пережидает её в норе")]
        [SerializeField, Range(1f, 12f)] private float _burrowNightTimeScale = 6f;

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
        [SerializeField] private float[] _digestSize = { 0f, 0.3f, 0.3f, 0.45f, 1f, 0.5f, 0.4f };

        [Tooltip("Урон от колючек свернувшегося ежа")]
        [SerializeField, Min(0f)] private float _hedgehogDamage = 14f;

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

        [Header("Лиса")]
        [Tooltip("Через сколько секунд после старта может прийти лиса")]
        [SerializeField, Min(0f)] private float _foxFirstDelay = 75f;

        [Tooltip("Пауза между визитами лисы, сек (мин)")]
        [SerializeField, Min(1f)] private float _foxIntervalMin = 50f;

        [Tooltip("Пауза между визитами лисы, сек (макс)")]
        [SerializeField, Min(1f)] private float _foxIntervalMax = 90f;

        [Tooltip("Скорость лисы в погоне, клеток в секунду (быстрее шага змеи, медленнее быстрого ползания)")]
        [SerializeField, Min(0.5f)] private float _foxSpeed = 4.4f;

        [Tooltip("Урон от укуса лисы")]
        [SerializeField, Min(0f)] private float _foxDamage = 22f;

        [Tooltip("Отпугнуть лису броском можно, только если здоровья больше этой доли")]
        [SerializeField, Range(0f, 1f)] private float _foxScareHealth = 0.5f;

        [Header("Погода")]
        [Tooltip("Через сколько секунд после старта может пойти первый дождь")]
        [SerializeField, Min(0f)] private float _firstRainDelay = 100f;

        [Tooltip("Пауза между дождями, сек (мин, макс)")]
        [SerializeField] private Vector2 _rainInterval = new Vector2(120f, 220f);

        [Tooltip("Сколько длится дождь, сек (мин, макс)")]
        [SerializeField] private Vector2 _rainDuration = new Vector2(50f, 100f);

        [Tooltip("Насколько дождь остужает змею (снижение «температуры места»); под кронами и в норе — слабее")]
        [SerializeField, Range(0f, 1f)] private float _rainChill = 0.3f;

        [Header("Главная цель")]
        [Tooltip("Какой длины должна стать змея, чтобы повзрослеть, клеток")]
        [SerializeField, Min(1f)] private float _questLength = 11f;

        [Tooltip("Сколько линек нужно пережить, чтобы повзрослеть")]
        [SerializeField, Min(0)] private int _questMolts = 2;

        [Tooltip("Сытость (доля), без которой нельзя отложить яйца")]
        [SerializeField, Range(0f, 1f)] private float _nestSatiety = 0.6f;

        [Tooltip("С какого расстояния до гнезда можно отложить яйца, клеток")]
        [SerializeField, Min(0.3f)] private float _nestRadius = 1.6f;

        [Header("Птичьи гнёзда")]
        [Tooltip("Сколько сытости даёт яйцо из птичьего гнезда")]
        [SerializeField, Min(0f)] private float _eggSatiety = 14f;

        [Tooltip("На сколько клеток удлиняет змею яйцо")]
        [SerializeField, Min(0f)] private float _eggGrowth = 0.35f;

        [Tooltip("Урон от клевка птицы, защищающей гнездо")]
        [SerializeField, Min(0f)] private float _birdPeckDamage = 6f;

        [Tooltip("Через сколько секунд разорённое гнездо снова полно яиц")]
        [SerializeField, Min(1f)] private float _nestRefillSeconds = 200f;

        [Header("Кладка и поколения")]
        [Tooltip("Сколько яиц в кладке змеи")]
        [SerializeField, Range(1, 9)] private int _clutchSize = 5;

        [Tooltip("Сколько секунд кладка зреет до вылупления")]
        [SerializeField, Min(5f)] private float _hatchSeconds = 170f;

        [Tooltip("Как часто лиса у гнезда утаскивает яйцо, сек")]
        [SerializeField, Min(0.5f)] private float _foxRaidInterval = 6f;

        [Tooltip("Во сколько раз чаще приходит лиса, пока змея охраняет кладку")]
        [SerializeField, Min(1f)] private float _guardFoxEagerness = 3f;

        [Tooltip("На сколько клеток длиннее должна вырасти змея каждого следующего поколения")]
        [SerializeField, Min(0f)] private float _generationLengthStep = 1.5f;

        [Header("Цели суток")]
        [Tooltip("Какую долю роста до линьки даёт выполненная охотничья цель")]
        [SerializeField, Range(0f, 1f)] private float _goalMoltReward = 0.5f;

        [Header("Ёж")]
        [Tooltip("С какой скоростью катится свёрнутый ёж, если подтолкнуть его носом, клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _hedgehogRollSpeed = 4.5f;

        [Tooltip("Скорость ежа, плывущего к берегу (беззащитен — свернуться в воде не может), клеток в секунду")]
        [SerializeField, Min(0.1f)] private float _hedgehogSwimSpeed = 0.8f;

        [Tooltip("Сколько секунд ёж выглядывает перед тем, как развернуться: укус в мордочку в это время ловит его")]
        [SerializeField, Min(0.2f)] private float _hedgehogPeekTime = 1.1f;

        [Tooltip("Сколько секунд мокрый ёж отряхивается на берегу и не может свернуться")]
        [SerializeField, Min(0f)] private float _hedgehogWetTime = 2.2f;

        [Header("Ежевика и тёплые ключи")]
        [Tooltip("Скорость змеи в ежевике (множитель)")]
        [SerializeField, Range(0.2f, 1f)] private float _brambleSpeed = 0.7f;

        [Tooltip("Урон от колючек, когда змея спешит в ежевике")]
        [SerializeField, Min(0f)] private float _thornDamage = 4f;

        [Tooltip("Как часто колючки царапают спешащую змею, сек")]
        [SerializeField, Min(0.1f)] private float _thornInterval = 0.9f;

        [Header("Мышиные городки")]
        [Tooltip("Доля мышей, появляющихся у мышиных городков")]
        [SerializeField, Range(0f, 1f)] private float _colonyMouseShare = 0.45f;

        [Tooltip("С какого расстояния до норки испуганная мышь в неё ныряет, клеток")]
        [SerializeField, Min(0.2f)] private float _colonyHideRadius = 1.3f;

        [Header("Осы")]
        [Tooltip("В каком радиусе от гнезда осы замечают шум, клеток")]
        [SerializeField, Min(1f)] private float _waspAlertRadius = 4.5f;

        [Tooltip("Сколько «злости» в секунду набирают осы от змеи рядом: шагом, быстро; крадущаяся змея их не тревожит")]
        [SerializeField] private Vector2 _waspAngerRate = new Vector2(0.35f, 1.2f);

        [Tooltip("Скорость роя, клеток в секунду (чуть быстрее быстрого ползания)")]
        [SerializeField, Min(0.5f)] private float _waspSpeed = 6.2f;

        [Tooltip("Урон от укуса осы")]
        [SerializeField, Min(0f)] private float _waspStingDamage = 4f;

        [Tooltip("Как часто жалит рой, сек")]
        [SerializeField, Min(0.1f)] private float _waspStingInterval = 0.85f;

        [Tooltip("Сколько секунд рой гонится, прежде чем вернуться к гнезду")]
        [SerializeField, Min(1f)] private float _waspChaseTime = 8f;

        [Tooltip("Дальше этого расстояния от гнезда рой отстаёт, клеток")]
        [SerializeField, Min(2f)] private float _waspLeash = 16f;

        [Tooltip("Сколько секунд гнездо успокаивается после атаки, прежде чем снова разозлиться")]
        [SerializeField, Min(0f)] private float _waspCalmTime = 20f;

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
        public float SwimSpeed => _swimSpeed;
        public float StartTemperature => _startTemperature;
        public float HeatRate => _heatRate;
        public float CoolRate => _coolRate;
        public float ColdSlowdown => _coldSlowdown;
        public float BurrowEnterRadius => _burrowEnterRadius;
        public float BurrowNightTimeScale => _burrowNightTimeScale;
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

        public float FoxFirstDelay => _foxFirstDelay;
        public float FoxIntervalMin => _foxIntervalMin;
        public float FoxIntervalMax => Mathf.Max(_foxIntervalMin, _foxIntervalMax);
        public float FoxSpeed => _foxSpeed;
        public float FoxDamage => _foxDamage;
        public float FoxScareHealth => _foxScareHealth;
        public float GoalMoltReward => _goalMoltReward;
        public float HedgehogDamage => _hedgehogDamage;
        public float FirstRainDelay => _firstRainDelay;
        public Vector2 RainInterval => _rainInterval;
        public Vector2 RainDuration => _rainDuration;
        public float RainChill => _rainChill;
        public float QuestLength => _questLength;
        public int QuestMolts => _questMolts;
        public float NestSatiety => _nestSatiety;
        public float NestRadius => _nestRadius;

        public float EggSatiety => _eggSatiety;
        public float EggGrowth => _eggGrowth;
        public float BirdPeckDamage => _birdPeckDamage;
        public float NestRefillSeconds => _nestRefillSeconds;
        public int ClutchSize => _clutchSize;
        public float HatchSeconds => _hatchSeconds;
        public float FoxRaidInterval => _foxRaidInterval;
        public float GuardFoxEagerness => _guardFoxEagerness;
        public float GenerationLengthStep => _generationLengthStep;

        public float HedgehogRollSpeed => _hedgehogRollSpeed;
        public float HedgehogSwimSpeed => _hedgehogSwimSpeed;
        public float HedgehogPeekTime => _hedgehogPeekTime;
        public float HedgehogWetTime => _hedgehogWetTime;
        public float BrambleSpeed => _brambleSpeed;
        public float ThornDamage => _thornDamage;
        public float ThornInterval => _thornInterval;
        public float ColonyMouseShare => _colonyMouseShare;
        public float ColonyHideRadius => _colonyHideRadius;
        public float WaspAlertRadius => _waspAlertRadius;
        public Vector2 WaspAngerRate => _waspAngerRate;
        public float WaspSpeed => _waspSpeed;
        public float WaspStingDamage => _waspStingDamage;
        public float WaspStingInterval => _waspStingInterval;
        public float WaspChaseTime => _waspChaseTime;
        public float WaspLeash => _waspLeash;
        public float WaspCalmTime => _waspCalmTime;

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
            bool changed = false;
            if (_prey.Length < defaults.Length)
            {
                var merged = new PreyStats[defaults.Length];
                for (int i = 0; i < merged.Length; i++) merged[i] = i < _prey.Length ? _prey[i] : defaults[i];
                _prey = merged;
                changed = true;
            }
            // Бугор переваривания новых видов — тоже по умолчанию.
            float[] digest = { 0f, 0.3f, 0.3f, 0.45f, 1f, 0.5f, 0.4f };
            if (_digestSize.Length < digest.Length)
            {
                var merged = new float[digest.Length];
                for (int i = 0; i < merged.Length; i++) merged[i] = i < _digestSize.Length ? _digestSize[i] : digest[i];
                _digestSize = merged;
                changed = true;
            }
            return changed;
        }

        private const int DefaultPreyCount = 7;
        private PreyStats[] _fallbackPrey;

        private static PreyStats[] CreateDefaultPrey()
        {
            return new[]
            {
                new PreyStats(7f, 0.25f, 0.6f, 0f, 0f, 1f, false, 40, 30),      // Жук
                new PreyStats(22f, 0.7f, 1.4f, 5.6f, 4.5f, 2.2f, true, 18, 32), // Мышь
                new PreyStats(26f, 0.8f, 0f, 4.4f, 3.5f, 2.4f, true, 18, 22),   // Лягушка
                new PreyStats(40f, 1.2f, 0.9f, 7f, 6f, 3f, true, 18, 12),       // Птица
                new PreyStats(70f, 1.8f, 1.6f, 6.4f, 5f, 3.2f, true, 12, 6),    // Кролик
                new PreyStats(38f, 1f, 0.7f, 0f, 3f, 1f, true, 4, 10),           // Ёж
                new PreyStats(30f, 0.9f, 0.9f, 4.6f, 3.2f, 1.6f, true, 16, 12)   // Рыба
            };
        }
    }
}
