namespace WildSnake
{
    /// <summary>Улучшения, которые змея выбирает при линьке. Значения — индексы массивов (и сохранений — не переставлять).</summary>
    public enum UpgradeType
    {
        /// <summary>Толстая чешуя: меньше урона, но чуть медленнее.</summary>
        ThickScales = 0,
        /// <summary>Медленный обмен: голод растёт медленнее, но и здоровье восстанавливается медленнее.</summary>
        SlowMetabolism = 1,
        /// <summary>Длинный бросок: бросок дальше и дешевле.</summary>
        LongStrike = 2,
        /// <summary>Острый нюх: дальше и чаще.</summary>
        KeenSmell = 3,
        /// <summary>Бесшумная чешуя: добыча замечает позже.</summary>
        SilentScales = 4,
        /// <summary>Крепкое сердце: больше здоровья, но больше и голод.</summary>
        StrongHeart = 5,
        /// <summary>Гибкое тело: быстрее, но голоднее.</summary>
        SwiftBody = 6,
        /// <summary>Сытная охота: добыча насыщает сильнее.</summary>
        HeartyMeal = 7,
        /// <summary>Глубокое дыхание: выносливость восстанавливается быстрее.</summary>
        DeepBreath = 8,
        /// <summary>Тёплая кровь: тело остывает медленнее.</summary>
        WarmBlood = 9,
        /// <summary>Стальная хватка: кролика душит быстрее.</summary>
        IronGrip = 10,
        /// <summary>Терпеливый охотник: засада наступает быстрее.</summary>
        Ambusher = 11,
        /// <summary>Пловец: быстрее в воде.</summary>
        Swimmer = 12
    }
}
