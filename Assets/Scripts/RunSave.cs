using System;

namespace Snake3D
{
    /// <summary>
    /// Сохранение партии у норы: всё, чтобы продолжить жизнь змеи с того же места.
    /// Хранится в PlayerPrefs как JSON (RunSaveStore). Со смертью змеи сохранение стирается.
    /// </summary>
    [Serializable]
    public class RunSave
    {
        public int Day = 1;
        public float Phase;
        public float Time;
        public float Health;
        public float MaxHealth;
        public float Satiety;
        public float Length;
        public float MoltProgress;
        public float Temperature;
        public int Prey;
        public int Molts;
        public int EaglesEvaded;
        public int FoxesScared;
        public int Burrow;
        public int Quest;
        public int Generation = 1;
        public int GenerationMolts;
        public int ClutchEggs;
        public float HatchTime;
        public int Generations;
        public int Hatchlings;
        public int[] Upgrades = new int[UpgradeSet.TYPE_COUNT];
    }
}
