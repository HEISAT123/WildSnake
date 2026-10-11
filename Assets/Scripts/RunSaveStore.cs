using UnityEngine;

namespace WildSnake
{
    /// <summary>Сохранение партии в PlayerPrefs (одна ячейка — одна жизнь змеи).</summary>
    public static class RunSaveStore
    {
        private const string KEY = "WildSnake.Survival.Save";

        public static bool HasSave => PlayerPrefs.HasKey(KEY);

        public static void Save(RunSave save)
        {
            PlayerPrefs.SetString(KEY, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        public static bool TryLoad(out RunSave save)
        {
            save = null;
            if (!HasSave) return false;
            string json = PlayerPrefs.GetString(KEY, string.Empty);
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                save = JsonUtility.FromJson<RunSave>(json);
            }
            catch (System.ArgumentException)
            {
                // Испорченная запись — как будто сохранения нет.
                save = null;
            }
            return save != null && save.Health > 0f;
        }

        public static void Clear()
        {
            if (!HasSave) return;
            PlayerPrefs.DeleteKey(KEY);
            PlayerPrefs.Save();
        }
    }
}
