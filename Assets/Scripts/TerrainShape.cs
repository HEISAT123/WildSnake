using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Форма мира: пологие холмы внутри и кольцо гор по краю — это и есть граница, за которую не выйти.
    /// Одна функция для логики (GridManager строит по ней карту высот) и для вида (EnvironmentBuilder строит
    /// по ней горы за пределами поля), поэтому рельеф стыкуется без швов. Детерминирована и считается от центра мира
    /// (форма не зависит от того, где стоит поле): только PerlinNoise.
    /// </summary>
    public static class TerrainShape
    {
        /// <summary>Радиус, с которого начинаются горы, мировых единиц от центра мира.</summary>
        public const float PlayRadius = 78f;

        /// <summary>Радиус ровной площадки на старте.</summary>
        public const float FlatRadius = 6f;

        /// <summary>Высота рельефа над центром мира.</summary>
        public static float Height(float x, float z, Vector3 center)
        {
            float lx = x - center.x;
            float lz = z - center.z;
            float r = Mathf.Sqrt(lx * lx + lz * lz);

            // Холмы: две октавы, у старта — ровно.
            float hills = (Mathf.PerlinNoise(lx * 0.028f + 13.1f, lz * 0.028f + 7.7f) - 0.5f) * 4.2f
                          + (Mathf.PerlinNoise(lx * 0.075f + 3.3f, lz * 0.075f + 91.1f) - 0.5f) * 1.1f;
            hills *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FlatRadius, FlatRadius + 14f, r));

            float m = MountainFactor(x, z, center);
            float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(lx * 0.025f + 40f, lz * 0.025f + 11f) * 2f - 1f);
            float mountains = m * m * (16f + ridge * 20f + Mathf.PerlinNoise(lx * 0.06f + 7f, lz * 0.06f + 7f) * 5f);
            return hills + mountains;
        }

        /// <summary>Насколько точка уже в горах: 0 — равнина, 1 — склоны. Граница неровная (шум по углу).</summary>
        public static float MountainFactor(float x, float z, Vector3 center)
        {
            float lx = x - center.x;
            float lz = z - center.z;
            float r = Mathf.Sqrt(lx * lx + lz * lz);
            float wobble = (Mathf.PerlinNoise(lx * 0.018f + 50f, lz * 0.018f + 50f) - 0.5f) * 18f;
            float edge = PlayRadius + wobble;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 3f, edge + 24f, r));
        }
    }
}
