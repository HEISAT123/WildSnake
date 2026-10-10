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

        public PreyThreat(bool active, Vector3 position, float awarenessMultiplier)
        {
            Active = active;
            Position = position;
            AwarenessMultiplier = awarenessMultiplier;
        }
    }
}
