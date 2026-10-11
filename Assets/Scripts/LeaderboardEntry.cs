namespace WildSnake
{
    /// <summary>Одна строка таблицы рекордов: сколько прожила змея и чего добилась.</summary>
    public struct LeaderboardEntry
    {
        /// <summary>Прожито секунд — по этому полю сортируется таблица.</summary>
        public int Seconds;
        public int Prey;
        public float Length;
        public int Molts;

        public LeaderboardEntry(int seconds, int prey, float length, int molts)
        {
            Seconds = seconds;
            Prey = prey;
            Length = length;
            Molts = molts;
        }
    }
}
