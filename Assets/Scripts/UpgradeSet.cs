using System;

namespace Snake3D
{
    /// <summary>
    /// Набранные за партию улучшения и их эффекты в виде множителей. Обычный C#-класс — легко тестировать.
    /// Каждое улучшение можно взять до MAX_LEVEL раз; при линьке предлагаются три разных из ещё не исчерпанных.
    /// </summary>
    public class UpgradeSet
    {
        public const int TYPE_COUNT = 8;
        public const int MAX_LEVEL = 3;
        public const float STRONG_HEART_BONUS = 20f;

        private readonly int[] _levels = new int[TYPE_COUNT];
        private readonly int[] _pool = new int[TYPE_COUNT];

        public int Level(UpgradeType type) => _levels[(int)type];
        public bool CanUpgrade(UpgradeType type) => _levels[(int)type] < MAX_LEVEL;

        public float DamageTakenMultiplier => 1f - 0.2f * Level(UpgradeType.ThickScales);
        public float HungerMultiplier => 1f - 0.15f * Level(UpgradeType.SlowMetabolism);
        public float DashDurationMultiplier => 1f + 0.25f * Level(UpgradeType.LongStrike);
        public float DashCooldownMultiplier => 1f - 0.15f * Level(UpgradeType.LongStrike);
        public float SmellRadiusMultiplier => 1f + 0.3f * Level(UpgradeType.KeenSmell);
        public float SmellCooldownMultiplier => 1f - 0.25f * Level(UpgradeType.KeenSmell);
        public float AwarenessMultiplier => 1f - 0.15f * Level(UpgradeType.SilentScales);
        public float MaxHealthBonus => STRONG_HEART_BONUS * Level(UpgradeType.StrongHeart);
        public float SpeedMultiplier => 1f + 0.07f * Level(UpgradeType.SwiftBody);
        public float FeedMultiplier => 1f + 0.2f * Level(UpgradeType.HeartyMeal);

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
