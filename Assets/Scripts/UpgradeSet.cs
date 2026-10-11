using System;

namespace Snake3D
{
    /// <summary>
    /// Набранные за партию улучшения и их эффекты в виде множителей. Обычный C#-класс — легко тестировать.
    /// Каждое улучшение можно взять до MAX_LEVEL раз; при линьке предлагаются три разных из ещё не исчерпанных.
    /// Баланс: отдача убывает (40% / 73% / 100% от предела за 1/2/3 уровня), а самые сильные улучшения
    /// чем-то платят — толстая чешуя замедляет, крепкое сердце и гибкое тело усиливают голод,
    /// медленный обмен замедляет заживление. Поэтому «собрать всё в одно» не делает змею неуязвимой.
    /// </summary>
    public class UpgradeSet
    {
        public const int TYPE_COUNT = 13;
        public const int MAX_LEVEL = 3;
        /// <summary>Прибавка к максимуму здоровья за уровень крепкого сердца.</summary>
        public const float STRONG_HEART_BONUS = 15f;

        // Доля от предела эффекта на уровнях 0..3 — убывающая отдача.
        private static readonly float[] Curve = { 0f, 0.4f, 0.73f, 1f };

        // Предел главного эффекта на третьем уровне, в процентах (у сердца — единиц здоровья за уровень).
        private static readonly float[] MaxPercent = { 30f, 25f, 30f, 50f, 25f, STRONG_HEART_BONUS, 10f, 30f, 40f, 40f, 30f, 40f, 30f };

        /// <summary>Плата толстой чешуи: замедление за уровень.</summary>
        public const float THICK_SCALES_SLOWDOWN = 0.02f;
        /// <summary>Плата крепкого сердца и гибкого тела: прибавка к голоду за уровень.</summary>
        public const float HUNGER_PENALTY = 0.04f;
        /// <summary>Плата медленного обмена: замедление заживления за уровень.</summary>
        public const float REGEN_PENALTY = 0.1f;

        private readonly int[] _levels = new int[TYPE_COUNT];
        private readonly int[] _pool = new int[TYPE_COUNT];

        public int Level(UpgradeType type) => _levels[(int)type];
        public bool CanUpgrade(UpgradeType type) => _levels[(int)type] < MAX_LEVEL;

        /// <summary>Доля от предела эффекта на уровне level (0..1).</summary>
        public static float Strength(int level) => Curve[Math.Max(0, Math.Min(MAX_LEVEL, level))];

        /// <summary>Главный эффект улучшения на уровне level: проценты, у крепкого сердца — единицы здоровья (для подписей).</summary>
        public static float EffectPercent(UpgradeType type, int level)
        {
            if (type == UpgradeType.StrongHeart) return STRONG_HEART_BONUS * Math.Max(0, Math.Min(MAX_LEVEL, level));
            return MaxPercent[(int)type] * Strength(level);
        }

        private float Effect(UpgradeType type) => MaxPercent[(int)type] * 0.01f * Strength(Level(type));

        public float DamageTakenMultiplier => 1f - Effect(UpgradeType.ThickScales);
        public float HungerMultiplier => (1f - Effect(UpgradeType.SlowMetabolism))
                                         * (1f + HUNGER_PENALTY * (Level(UpgradeType.StrongHeart) + Level(UpgradeType.SwiftBody)));
        public float HealthRegenMultiplier => 1f - REGEN_PENALTY * Level(UpgradeType.SlowMetabolism);
        public float StrikeDistanceMultiplier => 1f + Effect(UpgradeType.LongStrike);
        public float StrikeCostMultiplier => 1f - 0.66f * Effect(UpgradeType.LongStrike);
        public float SmellRadiusMultiplier => 1f + Effect(UpgradeType.KeenSmell);
        public float SmellCooldownMultiplier => 1f - 0.6f * Effect(UpgradeType.KeenSmell);
        public float AwarenessMultiplier => 1f - Effect(UpgradeType.SilentScales);
        public float MaxHealthBonus => STRONG_HEART_BONUS * Level(UpgradeType.StrongHeart);
        public float SpeedMultiplier => (1f + Effect(UpgradeType.SwiftBody)) * (1f - THICK_SCALES_SLOWDOWN * Level(UpgradeType.ThickScales));
        public float FeedMultiplier => 1f + Effect(UpgradeType.HeartyMeal);
        public float StaminaRegenMultiplier => 1f + Effect(UpgradeType.DeepBreath);
        public float CoolRateMultiplier => 1f - Effect(UpgradeType.WarmBlood);
        public float ConstrictDurationMultiplier => 1f - Effect(UpgradeType.IronGrip);
        public float AmbushDelayMultiplier => 1f - Effect(UpgradeType.Ambusher);
        public float SwimSpeedMultiplier => 1f + Effect(UpgradeType.Swimmer);

        public int TotalLevels
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < TYPE_COUNT; i++) sum += _levels[i];
                return sum;
            }
        }

        public void Reset()
        {
            Array.Clear(_levels, 0, TYPE_COUNT);
        }

        /// <summary>Уровни всех улучшений (для сохранения партии): копирует в result.</summary>
        public void CopyLevels(int[] result)
        {
            Array.Copy(_levels, result, Math.Min(result.Length, TYPE_COUNT));
        }

        /// <summary>Восстановить уровни из сохранения (с ограничением MAX_LEVEL). Старые сохранения короче — новые улучшения нулевые.</summary>
        public void SetLevels(int[] levels)
        {
            Reset();
            if (levels == null) return;
            for (int i = 0; i < TYPE_COUNT && i < levels.Length; i++) _levels[i] = Math.Max(0, Math.Min(MAX_LEVEL, levels[i]));
        }

        /// <summary>Взять улучшение. Возвращает false, если оно уже на максимуме.</summary>
        public bool Apply(UpgradeType type)
        {
            if (!CanUpgrade(type)) return false;
            _levels[(int)type]++;
            return true;
        }

        /// <summary>
        /// Выбрать до result.Length разных улучшений из доступных (перемешивание Фишера–Йетса без аллокаций).
        /// Возвращает, сколько вариантов записано.
        /// </summary>
        public int RollChoices(UpgradeType[] result, Random rng)
        {
            int available = 0;
            for (int i = 0; i < TYPE_COUNT; i++)
                if (_levels[i] < MAX_LEVEL) _pool[available++] = i;

            int count = Math.Min(result.Length, available);
            for (int i = 0; i < count; i++)
            {
                int j = i + rng.Next(available - i);
                int tmp = _pool[i];
                _pool[i] = _pool[j];
                _pool[j] = tmp;
                result[i] = (UpgradeType)_pool[i];
            }
            return count;
        }
    }
}
