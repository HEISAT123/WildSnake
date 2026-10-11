using UnityEngine;

namespace WildSnake
{
    /// <summary>Таблица рекордов выживания, хранится в PlayerPrefs.</summary>
    public class LeaderboardService : MonoBehaviour
    {
        [Header("Сохранение")]
        [Tooltip("Ключ PlayerPrefs")]
        [SerializeField] private string _key = "WildSnake.Survival.Leaderboard";

        private Leaderboard _board;

        public Leaderboard Board
        {
            get
            {
                EnsureLoaded();
                return _board;
            }
        }

        public int BestSeconds => Board.BestSeconds;

        private void Awake()
        {
            EnsureLoaded();
        }

        /// <summary>Записать результат. Возвращает место (0 — первое) или -1.</summary>
        public int Submit(LeaderboardEntry entry)
        {
            int rank = Board.Insert(entry);
            if (rank >= 0)
            {
                PlayerPrefs.SetString(_key, _board.Serialize());
                PlayerPrefs.Save();
            }
            return rank;
        }

        // Таблицу могут запросить раньше нашего Awake (порядок Awake между объектами не задан).
        private void EnsureLoaded()
        {
            if (_board == null) _board = Leaderboard.Parse(PlayerPrefs.GetString(_key, string.Empty));
        }
    }
}
