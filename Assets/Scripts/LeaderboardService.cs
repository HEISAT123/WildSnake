using UnityEngine;

namespace Snake3D
{
    /// <summary>Таблицы рекордов по режимам, хранятся в PlayerPrefs.</summary>
    public class LeaderboardService : MonoBehaviour
    {
        [Header("Сохранение")]
        [Tooltip("Префикс ключей PlayerPrefs")]
        [SerializeField] private string _keyPrefix = "Snake3D.Leaderboard.";

        private const int MODE_COUNT = 3;

        private readonly Leaderboard[] _boards = new Leaderboard[MODE_COUNT];

        private void Awake()
        {
            for (int i = 0; i < MODE_COUNT; i++)
                _boards[i] = Leaderboard.Parse(PlayerPrefs.GetString(Key((GameMode)i), string.Empty));
        }

        public Leaderboard GetBoard(GameMode mode)
        {
            return _boards[(int)mode];
        }

        public int Best(GameMode mode)
        {
            return _boards[(int)mode].BestScore;
        }

        /// <summary>Записать результат партии. Возвращает место (0 — первое) или -1.</summary>
        public int Submit(GameMode mode, LeaderboardEntry entry)
        {
            Leaderboard board = _boards[(int)mode];
            int rank = board.Insert(entry);
            if (rank >= 0)
            {
                PlayerPrefs.SetString(Key(mode), board.Serialize());
                PlayerPrefs.Save();
            }
            return rank;
        }

        private string Key(GameMode mode)
        {
            return _keyPrefix + mode;
        }
    }
}
