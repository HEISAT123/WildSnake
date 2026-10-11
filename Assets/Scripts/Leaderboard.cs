using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WildSnake
{
    /// <summary>
    /// Таблица из 10 самых долгих жизней змеи. При равном времени выше та, что поймала больше добычи.
    /// Обычный C#-класс без зависимостей от Unity — легко тестировать.
    /// Формат строки: "секунды,добыча,длина,линьки;секунды,добыча,длина,линьки;..."
    /// </summary>
    public class Leaderboard
    {
        public const int MAX_ENTRIES = 10;

        private readonly List<LeaderboardEntry> _entries = new List<LeaderboardEntry>(MAX_ENTRIES + 1);

        public IReadOnlyList<LeaderboardEntry> Entries => _entries;
        public int Count => _entries.Count;
        public int BestSeconds => _entries.Count > 0 ? _entries[0].Seconds : 0;

        /// <summary>Добавить результат. Возвращает место (0 — первое) или -1, если в таблицу не попал.</summary>
        public int Insert(LeaderboardEntry entry)
        {
            int index = 0;
            // При полном равенстве новый результат ниже старого: старый рекорд был поставлен раньше.
            while (index < _entries.Count && !IsBetter(entry, _entries[index])) index++;
            if (index >= MAX_ENTRIES) return -1;

            _entries.Insert(index, entry);
            if (_entries.Count > MAX_ENTRIES) _entries.RemoveAt(_entries.Count - 1);
            return index;
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _entries.Count; i++)
            {
                LeaderboardEntry e = _entries[i];
                if (i > 0) sb.Append(';');
                sb.Append(e.Seconds.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(e.Prey.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(e.Length.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                  .Append(e.Molts.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>Разбор строки; битые записи пропускаются, порядок восстанавливается сортировкой.</summary>
        public static Leaderboard Parse(string data)
        {
            var board = new Leaderboard();
            if (string.IsNullOrEmpty(data)) return board;

            string[] rows = data.Split(';');
            for (int i = 0; i < rows.Length; i++)
            {
                string[] parts = rows[i].Split(',');
                if (parts.Length != 4) continue;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)) continue;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int prey)) continue;
                if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float length)) continue;
                if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int molts)) continue;
                board.Insert(new LeaderboardEntry(seconds, prey, length, molts));
            }
            return board;
        }

        private static bool IsBetter(LeaderboardEntry a, LeaderboardEntry b)
        {
            if (a.Seconds != b.Seconds) return a.Seconds > b.Seconds;
            return a.Prey > b.Prey;
        }
    }
}
