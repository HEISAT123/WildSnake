namespace WildSnake
{
    /// <summary>Можно ли отложить яйца прямо сейчас, а если нет — чего не хватает.</summary>
    public enum NestCheck
    {
        Ok = 0,
        /// <summary>Змея ещё не повзрослела.</summary>
        NotGrown = 1,
        /// <summary>Кладка — только ночью.</summary>
        NeedNight = 2,
        /// <summary>Нужно сначала наесться.</summary>
        NeedFood = 3,
        /// <summary>Цель уже выполнена.</summary>
        AlreadyDone = 4
    }
}
