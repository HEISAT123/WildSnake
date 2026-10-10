using UnityEngine;

namespace Snake3D
{
    /// <summary>Хищник глазами добычи: где голова змеи и насколько её легко заметить.</summary>
    public struct PreyThreat
    {
        public bool Active;
        public Vector3 Position;

        /// <summary>Множитель радиуса, с которого добыча замечает змею (трава — меньше, бросок — больше).</summary>
        public float AwarenessMultiplier;

        /// <summary>Змея затаилась и подманивает кончиком хвоста: ничего не подозревающая добыча бредёт к ней.</summary>
        public bool Lure;

        public PreyThreat(bool active, Vector3 position, float awarenessMultiplier, bool lure)
        {
            Active = active;
            Position = position;
            AwarenessMultiplier = awarenessMultiplier;
            Lure = lure;
        }
    }
}
