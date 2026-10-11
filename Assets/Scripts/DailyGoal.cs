namespace WildSnake
{
    /// <summary>Одна цель суток: что сделать, сколько раз и сколько уже сделано.</summary>
    public struct DailyGoal
    {
        public GoalType Type;
        public int Target;
        public int Progress;
        public bool Done;

        /// <summary>Награда: true — очко линьки, false — полное восстановление.</summary>
        public bool MoltReward;
    }
}
