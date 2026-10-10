namespace Snake3D
{
    /// <summary>Цели суток. Числовые значения — индексы массивов подписей в UI.</summary>
    public enum GoalType
    {
        /// <summary>Поймать птицу броском из засады.</summary>
        AmbushBird = 0,
        /// <summary>Пережить ночь, ни разу не заходя в нору.</summary>
        NightOutside = 1,
        /// <summary>Задушить кролика.</summary>
        ConstrictRabbit = 2,
        /// <summary>Поймать несколько мышей.</summary>
        CatchMice = 3,
        /// <summary>Отпугнуть лису броском.</summary>
        ScareFox = 4,
        /// <summary>Уйти от атаки орла.</summary>
        EvadeEagle = 5,
        /// <summary>Прогреться на солнечном камне.</summary>
        BaskStone = 6,
        /// <summary>Поймать рыбу броском.</summary>
        CatchFish = 7,
        /// <summary>Съесть яйца из птичьих гнёзд.</summary>
        EatEggs = 8
    }
}
