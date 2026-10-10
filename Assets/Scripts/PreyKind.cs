namespace Snake3D
{
    /// <summary>Виды добычи. Числовые значения используются как индексы массивов.</summary>
    public enum PreyKind
    {
        /// <summary>Жук: мелочь, не убегает, ловится касанием.</summary>
        Beetle = 0,
        /// <summary>Мышь: убегает рывками, ловится броском.</summary>
        Mouse = 1,
        /// <summary>Лягушка: упрыгивает, ловится броском.</summary>
        Frog = 2,
        /// <summary>Птица: самая сытная и пугливая, улетает; ночью спит.</summary>
        Bird = 3
    }
}
