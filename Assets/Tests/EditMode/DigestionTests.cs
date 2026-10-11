using NUnit.Framework;

namespace WildSnake.Tests
{
    /// <summary>Переваривание: бугор ползёт к хвосту, замедляет и проходит; обвивание крупной добычи.</summary>
    public class DigestionTests
    {
        [Test]
        public void Empty_NoPenalty()
        {
            var d = new Digestion();
            Assert.IsFalse(d.IsDigesting);
            Assert.AreEqual(1f, d.SpeedMultiplier);
            Assert.AreEqual(1f, d.JumpMultiplier);
        }

        [Test]
        public void Meal_SlowsAndLowersJump_UntilPassed()
        {
            var d = new Digestion { SlowPerSize = 0.4f, JumpPenaltyPerSize = 0.5f };
            d.Add(1f, 10f);
            Assert.IsTrue(d.IsDigesting);
            Assert.AreEqual(0.6f, d.SpeedMultiplier, 1e-4f);
            Assert.AreEqual(0.5f, d.JumpMultiplier, 1e-4f);

            d.Tick(5f);
            d.GetMeal(0, out float pos, out float size);
            Assert.AreEqual(0.5f, pos, 1e-4f, "Бугор должен быть на середине тела");
            Assert.AreEqual(1f, size);
            Assert.Greater(d.SpeedMultiplier, 0.6f, "Уходя к хвосту, бугор тяжелит меньше");

            d.Tick(5.1f);
            Assert.IsFalse(d.IsDigesting, "Бугор должен пройти");
            Assert.AreEqual(1f, d.SpeedMultiplier);
        }

        [Test]
        public void BiggerMeal_IsHeavier()
        {
            var small = new Digestion();
            var big = new Digestion();
            small.Add(0.3f, 10f);
            big.Add(1f, 10f);
            Assert.Less(big.SpeedMultiplier, small.SpeedMultiplier);
        }

        [Test]
        public void Overflow_DropsOldestMeal()
        {
            var d = new Digestion();
            for (int i = 0; i < Digestion.MAX_MEALS + 2; i++) d.Add(0.1f + i * 0.1f, 100f);
            Assert.AreEqual(Digestion.MAX_MEALS, d.Count);
            d.GetMeal(Digestion.MAX_MEALS - 1, out _, out float last);
            Assert.AreEqual(0.1f + (Digestion.MAX_MEALS + 1) * 0.1f, last, 1e-4f);
        }

        [Test]
        public void Constriction_HoldingWins()
        {
            var c = new Constriction { Duration = 2f };
            c.Begin();
            Assert.AreEqual(ConstrictResult.Ongoing, c.Tick(1f, true, true));
            Assert.AreEqual(0.5f, c.Progress01, 1e-4f);
            Assert.AreEqual(ConstrictResult.Won, c.Tick(1.01f, true, true));
            Assert.IsFalse(c.IsActive);
        }

        [Test]
        public void Constriction_NoStamina_PreyEscapes()
        {
            var c = new Constriction { Duration = 2f };
            c.Begin();
            c.Tick(1f, true, true);
            Assert.AreEqual(ConstrictResult.Escaped, c.Tick(0.1f, true, false), "Кончились силы — добыча вырывается");
        }

        [Test]
        public void Constriction_LetGo_DecaysThenEscapes()
        {
            var c = new Constriction { Duration = 2f, Decay = 0.25f, Patience = 1.5f };
            c.Begin();
            c.Tick(1f, true, true);
            Assert.AreEqual(ConstrictResult.Ongoing, c.Tick(1f, false, true));
            Assert.AreEqual(0.25f, c.Progress01, 1e-4f, "Без сжатия шкала тает");
            Assert.AreEqual(ConstrictResult.Escaped, c.Tick(0.6f, false, true));
        }
    }
}
