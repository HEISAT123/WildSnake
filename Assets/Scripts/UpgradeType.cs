namespace Snake3D
{
    /// <summary>Улучшения, которые змея выбирает при линьке. Значения — индексы массивов.</summary>
    public enum UpgradeType
    {
        /// <summary>Толстая чешуя: меньше урона.</summary>
        ThickScales = 0,
        /// <summary>Медленный обмен: голод растёт медленнее.</summary>
        SlowMetabolism = 1,
        /// <summary>Длинный бросок: бросок дольше и чаще.</summary>
        LongStrike = 2,
        /// <summary>Острый нюх: дальше и чаще.</summary>
        KeenSmell = 3,
        /// <summary>Бесшумная чешуя: добыча замечает позже.</summary>
        SilentScales = 4,
        /// <summary>Крепкое сердце: больше здоровья.</summary>
        StrongHeart = 5,
        /// <summary>Гибкое тело: выше скорость.</summary>
        SwiftBody = 6,
        /// <summary>Сытная охота: добыча насыщает сильнее.</summary>
        HeartyMeal = 7
    }
}
