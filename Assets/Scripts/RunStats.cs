namespace WildSnake
{
    /// <summary>Статистика одной жизни змеи (для экрана итогов и таблицы рекордов).</summary>
    public struct RunStats
    {
        /// <summary>Сколько секунд прожила.</summary>
        public float Time;

        /// <summary>Сколько суток прожила (дробное).</summary>
        public float Days;

        public int Prey;
        public int Molts;

        /// <summary>Сколько атак орла пережила без удара.</summary>
        public int EaglesEvaded;

        /// <summary>Сколько раз отпугнула лису.</summary>
        public int FoxesScared;

        public float Length;
        public DamageSource Cause;

        /// <summary>Род продолжен хотя бы раз: яйца отложены.</summary>
        public bool Won;

        /// <summary>Сколько поколений сменилось (вылуплений).</summary>
        public int Generations;

        /// <summary>Сколько змеёнышей вылупилось за все поколения.</summary>
        public int Hatchlings;

        /// <summary>Сколько птичьих яиц съедено.</summary>
        public int Eggs;
    }
}
