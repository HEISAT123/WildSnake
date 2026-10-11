namespace WildSnake
{
    /// <summary>Состояния игры.</summary>
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        /// <summary>Линька: игра на паузе, игрок выбирает улучшение.</summary>
        Molting,
        GameOver
    }
}
