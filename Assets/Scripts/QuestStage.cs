namespace WildSnake
{
    /// <summary>Этапы главной цели «Продолжить род».</summary>
    public enum QuestStage
    {
        /// <summary>Повзрослеть: набрать длину и перелинять нужное число раз.</summary>
        Grow = 0,
        /// <summary>Найти гнездо и ночью, сытой, отложить яйца.</summary>
        Nest = 1,
        /// <summary>Яйца отложены — охранять кладку от лисы до вылупления; потом — следующее поколение.</summary>
        Guard = 2
    }
}
