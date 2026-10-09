namespace Snake3D
{
    /// <summary>Одна строка таблицы рекордов.</summary>
    public struct LeaderboardEntry
    {
        public int Score;
        public float Length;
        public float Time;
        public int Apples;

        public LeaderboardEntry(int score, float length, float time, int apples)
        {
            Score = score;
            Length = length;
            Time = time;
            Apples = apples;
        }
    }
}
