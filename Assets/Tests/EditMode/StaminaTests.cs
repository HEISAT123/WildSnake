using NUnit.Framework;

namespace Snake3D.Tests
{
    /// <summary>Выносливость: траты, восстановление, «выдохлась», влияние голода.</summary>
    public class StaminaTests
    {
        private static Stamina Create()
        {
            var s = new Stamina { RegenPerSecond = 10f, RegenDelay = 0.5f, StillBonus = 2f, RecoverThreshold01 = 0.3f };
            s.Reset(100f);
            return s;
        }

        [Test]
        public void Reset_StartsFull()
        {
            Stamina s = Create();
            Assert.AreEqual(100f, s.Value);
            Assert.AreEqual(1f, s.Value01);
            Assert.IsFalse(s.IsExhausted);
        }

        [Test]
        public void TrySpend_NotEnough_SpendsNothing()
        {
            Stamina s = Create();
            Assert.IsTrue(s.TrySpend(80f));
            Assert.AreEqual(20f, s.Value, 1e-4f);
            Assert.IsFalse(s.TrySpend(30f), "Бросок без запаса выносливости должен не состояться");
            Assert.AreEqual(20f, s.Value, 1e-4f);
        }

        [Test]
        public void Regen_WaitsForDelay_ThenRestores()
        {
            Stamina s = Create();
            s.TrySpend(50f);
            s.Tick(0.4f, false);
            Assert.AreEqual(50f, s.Value, 1e-4f, "Во время паузы после траты восстановления нет");
            s.Tick(0.2f, false);
            s.Tick(1f, false);
            Assert.AreEqual(60f, s.Value, 1e-3f);
        }

        [Test]
        public void Regen_IsFaster_WhenStill()
        {
            Stamina moving = Create();
            Stamina still = Create();
            moving.TrySpend(50f);
            still.TrySpend(50f);
            moving.Tick(1f, false);
            still.Tick(1f, true);
            moving.Tick(1f, false);
            still.Tick(1f, true);
            Assert.Greater(still.Value, moving.Value);
        }

        [Test]
        public void Hunger_SlowsRegen()
        {
            Stamina fed = Create();
            Stamina hungry = Create();
            hungry.RegenMultiplier = 0.4f;
            fed.TrySpend(50f);
            hungry.TrySpend(50f);
            fed.Tick(1f, false);
            hungry.Tick(1f, false);
            fed.Tick(2f, false);
            hungry.Tick(2f, false);
            Assert.AreEqual(70f, fed.Value, 1e-3f);
            Assert.AreEqual(58f, hungry.Value, 1e-3f);
        }

        [Test]
        public void Drain_ToZero_Exhausts_UntilThreshold()
        {
            Stamina s = Create();
            s.Drain(150f);
            Assert.AreEqual(0f, s.Value);
            Assert.IsTrue(s.IsExhausted);
            Assert.IsFalse(s.CanSpend(1f), "Выдохшаяся змея не может тратить");

            s.Tick(1f, false);
            s.Tick(2f, false);
            Assert.IsTrue(s.IsExhausted, "20% — ещё ниже порога восстановления");
            s.Tick(1.5f, false);
            Assert.IsFalse(s.IsExhausted);
        }
    }
}
