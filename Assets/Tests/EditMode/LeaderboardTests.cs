using NUnit.Framework;

namespace Snake3D.Tests
{
    /// <summary>Таблица рекордов: порядок, лимит, места, сериализация.</summary>
    public class LeaderboardTests
    {
        private static LeaderboardEntry Entry(int score, int apples = 0)
        {
            return new LeaderboardEntry(score, 3f, 10f, apples);
        }

        [Test]
        public void Insert_KeepsDescendingOrder()
        {
            var board = new Leaderboard();
            int[] scores = { 50, 10, 90, 30, 70, 0, 100 };
            foreach (int s in scores) board.Insert(Entry(s));

            Assert.AreEqual(scores.Length, board.Count);
            for (int i = 1; i < board.Count; i++)
                Assert.GreaterOrEqual(board.Entries[i - 1].Score, board.Entries[i].Score, $"Нарушен порядок на позиции {i}");
            Assert.AreEqual(100, board.BestScore);
            Assert.AreEqual(0, board.Entries[board.Count - 1].Score);
        }

        [Test]
        public void Insert_ReturnsRank()
        {
            var board = new Leaderboard();
            Assert.AreEqual(0, board.Insert(Entry(50)));
            Assert.AreEqual(0, board.Insert(Entry(80)));
            Assert.AreEqual(2, board.Insert(Entry(10)));
            Assert.AreEqual(2, board.Insert(Entry(30)));
        }

        [Test]
        public void Insert_LimitsToMaxEntries()
        {
            var board = new Leaderboard();
            for (int i = 1; i <= 15; i++) board.Insert(Entry(i * 10));

            Assert.AreEqual(Leaderboard.MAX_ENTRIES, board.Count);
            Assert.AreEqual(150, board.Entries[0].Score);
            Assert.AreEqual(60, board.Entries[Leaderboard.MAX_ENTRIES - 1].Score, "Должны вытесняться худшие результаты");
        }

        [Test]
        public void Insert_IntoFullTable_TooLowScore_ReturnsMinusOne()
        {
            var board = new Leaderboard();
            for (int i = 1; i <= Leaderboard.MAX_ENTRIES; i++) board.Insert(Entry(i * 10));

            Assert.AreEqual(-1, board.Insert(Entry(5)));
            Assert.AreEqual(Leaderboard.MAX_ENTRIES, board.Count);
            Assert.AreEqual(10, board.Entries[Leaderboard.MAX_ENTRIES - 1].Score);
        }

        [Test]
        public void Insert_IntoFullTable_EqualToLast_ReturnsMinusOne()
        {
            var board = new Leaderboard();
            for (int i = 1; i <= Leaderboard.MAX_ENTRIES; i++) board.Insert(Entry(i * 10, apples: 1));

            // Равный последнему встаёт ниже него, то есть за пределы таблицы.
            Assert.AreEqual(-1, board.Insert(Entry(10, apples: 2)));
            Assert.AreEqual(1, board.Entries[Leaderboard.MAX_ENTRIES - 1].Apples);
        }

        [Test]
        public void Insert_IntoFullTable_HighScore_PushesOutLast()
        {
            var board = new Leaderboard();
            for (int i = 1; i <= Leaderboard.MAX_ENTRIES; i++) board.Insert(Entry(i * 10));

            Assert.AreEqual(0, board.Insert(Entry(1000)));
            Assert.AreEqual(Leaderboard.MAX_ENTRIES, board.Count);
            Assert.AreEqual(1000, board.Entries[0].Score);
            Assert.AreEqual(20, board.Entries[Leaderboard.MAX_ENTRIES - 1].Score);
        }

        [Test]
        public void Insert_EqualScore_NewEntryGoesBelow()
        {
            var board = new Leaderboard();
            board.Insert(Entry(100, apples: 1));
            int rank = board.Insert(Entry(100, apples: 2));

            Assert.AreEqual(1, rank);
            Assert.AreEqual(1, board.Entries[0].Apples, "Старый результат должен остаться выше");
            Assert.AreEqual(2, board.Entries[1].Apples);
        }

        [Test]
        public void Clear_EmptiesTable()
        {
            var board = new Leaderboard();
            board.Insert(Entry(10));
            board.Clear();
            Assert.AreEqual(0, board.Count);
            Assert.AreEqual(0, board.BestScore);
        }

        [Test]
        public void SerializeParse_RoundTrip()
        {
            var board = new Leaderboard();
            board.Insert(new LeaderboardEntry(120, 12.5f, 63.4f, 11));
            board.Insert(new LeaderboardEntry(300, 25.25f, 140f, 27));
            board.Insert(new LeaderboardEntry(40, 3f, 9.5f, 4));

            string data = board.Serialize();
            Leaderboard parsed = Leaderboard.Parse(data);

            Assert.AreEqual(board.Count, parsed.Count, $"Строка: {data}");
            for (int i = 0; i < board.Count; i++)
            {
                LeaderboardEntry a = board.Entries[i];
                LeaderboardEntry b = parsed.Entries[i];
                Assert.AreEqual(a.Score, b.Score, $"Score [{i}]");
                Assert.AreEqual(a.Length, b.Length, 0.01f, $"Length [{i}]");
                Assert.AreEqual(a.Time, b.Time, 0.05f, $"Time [{i}]");
                Assert.AreEqual(a.Apples, b.Apples, $"Apples [{i}]");
            }
            Assert.AreEqual(data, parsed.Serialize());
        }

        [Test]
        public void Serialize_Empty_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, new Leaderboard().Serialize());
        }

        [Test]
        public void Parse_NullOrEmpty_ReturnsEmptyBoard()
        {
            Assert.AreEqual(0, Leaderboard.Parse(null).Count);
            Assert.AreEqual(0, Leaderboard.Parse(string.Empty).Count);
        }

        [Test]
        public void Parse_Garbage_SkipsBrokenRows()
        {
            Leaderboard board = Leaderboard.Parse("abc;1,2;10,3,4,5;x,1,1,1;7,1,1,1,1;;20,abc,1,1;30,2.5,8,2;");

            Assert.AreEqual(2, board.Count);
            Assert.AreEqual(30, board.Entries[0].Score);
            Assert.AreEqual(2.5f, board.Entries[0].Length, 0.001f);
            Assert.AreEqual(10, board.Entries[1].Score);
        }

        [Test]
        public void Parse_UnsortedInput_IsSortedAndLimited()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 15; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append((i * 7) % 15 * 10).Append(",3,10,1");
            }
            Leaderboard board = Leaderboard.Parse(sb.ToString());

            Assert.AreEqual(Leaderboard.MAX_ENTRIES, board.Count);
            Assert.AreEqual(140, board.BestScore);
            for (int i = 1; i < board.Count; i++)
                Assert.GreaterOrEqual(board.Entries[i - 1].Score, board.Entries[i].Score);
        }
    }
}
