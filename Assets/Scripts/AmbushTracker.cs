namespace WildSnake
{
    /// <summary>
    /// Засада: змея, неподвижная дольше Delay секунд, за Ramp секунд сливается с местностью.
    /// Чем глубже маскировка, тем хуже её замечает добыча (в траве — ещё хуже), а полностью затаившаяся змея
    /// подманивает добычу кончиком хвоста. Обычный C#-класс без Unity API — легко тестировать.
    /// </summary>
    public class AmbushTracker
    {
        /// <summary>Сколько секунд нужно не двигаться, чтобы маскировка началась.</summary>
        public float Delay { get; set; } = 2f;

        /// <summary>За сколько секунд маскировка доходит до полной.</summary>
        public float Ramp { get; set; } = 1f;

        /// <summary>Множитель заметности при полной маскировке на открытом месте.</summary>
        public float OpenAwareness { get; set; } = 0.35f;

        /// <summary>Множитель заметности при полной маскировке в траве.</summary>
        public float CoverAwareness { get; set; } = 0.15f;

        public float StillTime { get; private set; }

        /// <summary>Глубина маскировки 0..1.</summary>
        public float Concealment01
        {
            get
            {
                if (StillTime <= Delay) return 0f;
                if (Ramp <= 0f) return 1f;
                float k = (StillTime - Delay) / Ramp;
                return k > 1f ? 1f : k;
            }
        }

        /// <summary>Змея полностью затаилась — подманивает добычу.</summary>
        public bool IsLuring => Concealment01 >= 1f;

        public void Tick(float dt, bool still)
        {
            if (still) StillTime += dt;
            else StillTime = 0f;
        }

        /// <summary>Сорвать засаду (бросок, прыжок, удар).</summary>
        public void Break()
        {
            StillTime = 0f;
        }

        /// <summary>Множитель радиуса, с которого добыча замечает змею: 1 — без маскировки.</summary>
        public float AwarenessMultiplier(bool inCover)
        {
            float target = inCover ? CoverAwareness : OpenAwareness;
            float k = Concealment01;
            return 1f + (target - 1f) * k;
        }
    }
}
