using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Все игровые параметры баланса в одном ассете. Можно сделать несколько ассетов
    /// (лёгкий/сложный) и подставлять нужный в GameManager и SnakeController.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Snake3D/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Змейка: скорость и руль")]
        [Tooltip("Начальная скорость, клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _startSpeed = 4.5f;

        [Tooltip("Максимальная скорость (без бонусов), клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _maxSpeed = 9f;

        [Tooltip("Прибавка скорости за яблоко, клеток в секунду")]
        [SerializeField, Min(0f)] private float _speedPerFood = 0.1f;

        [Tooltip("Скорость поворота при руле до упора, град/с")]
        [SerializeField, Min(10f)] private float _turnSpeed = 190f;

        [Tooltip("Как быстро руль доходит до упора, 1/с")]
        [SerializeField, Min(0.5f)] private float _steerResponse = 6f;

        [Header("Змейка: прыжок и рывок")]
        [Tooltip("Высота прыжка, клеток")]
        [SerializeField, Min(0.1f)] private float _jumpHeight = 1.4f;

        [Tooltip("Гравитация, клеток в секунду²")]
        [SerializeField, Min(1f)] private float _gravity = 30f;

        [Tooltip("Множитель скорости во время рывка")]
        [SerializeField, Min(1f)] private float _dashMultiplier = 2.2f;

        [Tooltip("Длительность рывка, сек")]
        [SerializeField, Min(0.05f)] private float _dashDuration = 0.35f;

        [Tooltip("Перезарядка рывка, сек")]
        [SerializeField, Min(0.1f)] private float _dashCooldown = 3f;

        [Header("Змейка: длина")]
        [Tooltip("Начальная длина тела, клеток")]
        [SerializeField, Min(1f)] private float _startLength = 3f;

        [Tooltip("Максимальная длина тела, клеток")]
        [SerializeField, Min(2f)] private float _maxLength = 120f;

        [Tooltip("Рост за обычное яблоко, клеток")]
        [SerializeField, Min(0.1f)] private float _growthPerFood = 0.9f;

        [Header("Еда")]
        [Tooltip("Очки за обычное яблоко")]
        [SerializeField, Min(1)] private int _normalPoints = 10;

        [Tooltip("Очки за золотое яблоко")]
        [SerializeField, Min(1)] private int _goldenPoints = 50;

        [Tooltip("Штраф за гнилое яблоко")]
        [SerializeField, Min(0)] private int _rottenPenalty = 20;

        [Tooltip("Шанс золотого яблока (0..1)")]
        [SerializeField, Range(0f, 1f)] private float _goldenChance = 0.15f;

        [Tooltip("Шанс гнилого яблока (0..1)")]
        [SerializeField, Range(0f, 1f)] private float _rottenChance = 0.1f;

        [Tooltip("Время жизни золотого яблока, сек")]
        [SerializeField, Min(1f)] private float _goldenLifetime = 6f;

        [Tooltip("Время жизни гнилого яблока, сек")]
        [SerializeField, Min(1f)] private float _rottenLifetime = 8f;

        [Tooltip("Насколько укорачивает гнилое яблоко, клеток")]
        [SerializeField, Min(0f)] private float _rottenShrink = 1.5f;

        [Header("Бонусы")]
        [Tooltip("Задержка первого бонуса после старта, сек")]
        [SerializeField, Min(0f)] private float _powerUpFirstDelay = 8f;

        [Tooltip("Интервал между бонусами, сек")]
        [SerializeField, Min(1f)] private float _powerUpInterval = 14f;

        [Tooltip("Сколько бонус лежит на поле, сек")]
        [SerializeField, Min(1f)] private float _powerUpLifetime = 10f;

        [Tooltip("Длительность ускорения, сек")]
        [SerializeField, Min(0.5f)] private float _speedBoostDuration = 6f;

        [Tooltip("Множитель скорости при ускорении")]
        [SerializeField, Min(1f)] private float _speedBoostMultiplier = 1.5f;

        [Tooltip("Длительность замедления времени, сек (реального)")]
        [SerializeField, Min(0.5f)] private float _slowTimeDuration = 6f;

        [Tooltip("Масштаб времени при замедлении")]
        [SerializeField, Range(0.2f, 1f)] private float _slowTimeScale = 0.6f;

        [Tooltip("Длительность магнита, сек")]
        [SerializeField, Min(0.5f)] private float _magnetDuration = 8f;

        [Tooltip("Радиус магнита, клеток")]
        [SerializeField, Min(1f)] private float _magnetRadius = 7f;

        [Tooltip("Скорость притяжения еды магнитом, клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _magnetPullSpeed = 6f;

        [Tooltip("Длительность двойных очков, сек")]
        [SerializeField, Min(0.5f)] private float _doubleScoreDuration = 10f;

        [Header("Уровни")]
        [Tooltip("Сколько яблок нужно для следующего уровня")]
        [SerializeField, Min(1)] private int _applesPerLevel = 5;

        [Tooltip("Максимум вращающихся балок на поле")]
        [SerializeField, Min(0)] private int _maxSpinners = 4;

        [Tooltip("Скорость вращения балки на 2-м уровне, град/с")]
        [SerializeField, Min(0f)] private float _spinnerBaseSpeed = 45f;

        [Tooltip("Прибавка скорости балок за уровень, град/с")]
        [SerializeField, Min(0f)] private float _spinnerSpeedPerLevel = 8f;

        [Header("Режимы")]
        [Tooltip("Длительность режима «На время», сек")]
        [SerializeField, Min(10f)] private float _timeAttackSeconds = 120f;

        [Tooltip("Интервал сужения арены в режиме «Выживание», сек")]
        [SerializeField, Min(2f)] private float _shrinkInterval = 15f;

        [Tooltip("Минимальный размер арены в режиме «Выживание», клеток")]
        [SerializeField, Min(6)] private int _minArenaSize = 14;

        [Header("Соперник")]
        [Tooltip("Задержка возрождения змеи-соперника, сек")]
        [SerializeField, Min(0f)] private float _rivalRespawnDelay = 4f;

        public float StartSpeed => _startSpeed;
        public float MaxSpeed => _maxSpeed;
        public float SpeedPerFood => _speedPerFood;
        public float TurnSpeed => _turnSpeed;
        public float SteerResponse => _steerResponse;
        public float JumpHeight => _jumpHeight;
        public float Gravity => _gravity;
        public float DashMultiplier => _dashMultiplier;
        public float DashDuration => _dashDuration;
        public float DashCooldown => _dashCooldown;
        public float StartLength => _startLength;
        public float MaxLength => _maxLength;
        public float GrowthPerFood => _growthPerFood;

        public int NormalPoints => _normalPoints;
        public int GoldenPoints => _goldenPoints;
        public int RottenPenalty => _rottenPenalty;
        public float GoldenChance => _goldenChance;
        public float RottenChance => _rottenChance;
        public float GoldenLifetime => _goldenLifetime;
        public float RottenLifetime => _rottenLifetime;
        public float RottenShrink => _rottenShrink;

        public float PowerUpFirstDelay => _powerUpFirstDelay;
        public float PowerUpInterval => _powerUpInterval;
        public float PowerUpLifetime => _powerUpLifetime;
        public float SpeedBoostMultiplier => _speedBoostMultiplier;
        public float SlowTimeScale => _slowTimeScale;
        public float MagnetRadius => _magnetRadius;
        public float MagnetPullSpeed => _magnetPullSpeed;

        public int ApplesPerLevel => _applesPerLevel;
        public int MaxSpinners => _maxSpinners;
        public float SpinnerBaseSpeed => _spinnerBaseSpeed;
        public float SpinnerSpeedPerLevel => _spinnerSpeedPerLevel;

        public float TimeAttackSeconds => _timeAttackSeconds;
        public float ShrinkInterval => _shrinkInterval;
        public int MinArenaSize => _minArenaSize;

        public float RivalRespawnDelay => _rivalRespawnDelay;

        /// <summary>Длительность бонуса; для щита 0 — он действует до первого удара.</summary>
        public float GetPowerUpDuration(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Speed: return _speedBoostDuration;
                case PowerUpType.SlowTime: return _slowTimeDuration;
                case PowerUpType.Magnet: return _magnetDuration;
                case PowerUpType.DoubleScore: return _doubleScoreDuration;
                default: return 0f;
            }
        }
    }
}
