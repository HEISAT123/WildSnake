using NUnit.Framework;

namespace Snake3D.Tests
{
    /// <summary>Голод, восстановление и урон змеи.</summary>
    public class SurvivalStatsTests
    {
        private static SurvivalStats Create(float startSatiety = 80f)
        {
            return new SurvivalStats(100f, 100f, startSatiety) { RegenPerSecond = 5f, WellFedThreshold = 0.6f };
        }

        [Test]
        public void Reset_StartsHealthy()
        {
            SurvivalStats s = Create();
            Assert.AreEqual(100f, s.Health);
            Assert.AreEqual(80f, s.Satiety);
            Assert.IsFalse(s.IsDead);
            Assert.IsFalse(s.IsStarving);
        }

        [Test]
        public void Tick_DrainsSatiety_NotBelowZero()
        {
            SurvivalStats s = Create(10f);
            s.Tick(1f, 4f);
            Assert.AreEqual(6f, s.Satiety, 1e-4f);
            s.Tick(10f, 4f);
            Assert.AreEqual(0f, s.Satiety);
            Assert.IsTrue(s.IsStarving);
        }

        [Test]
        public void Tick_WellFed_RegeneratesHealth()
        {
            SurvivalStats s = Create(90f);
            s.Damage(30f);
            s.Tick(2f, 0f);
            Assert.AreEqual(80f, s.Health, 1e-4f);
        }

        [Test]
        public void Tick_Hungry_DoesNotRegenerate()
        {
            SurvivalStats s = Create(30f);
            s.Damage(30f);
            s.Tick(2f, 0f);
            Assert.AreEqual(70f, s.Health, 1e-4f);
        }

        [Test]
        public void Feed_ClampsToMax()
        {
            SurvivalStats s = Create(90f);
            s.Feed(50f);
            Assert.AreEqual(100f, s.Satiety);
        }

        [Test]
        public void Damage_ReportsDeathOnlyOnce()
        {
            SurvivalStats s = Create();
            Assert.IsFalse(s.Damage(60f));
            Assert.IsTrue(s.Damage(60f), "Удар, обнуливший здоровье, должен сообщить о смерти");
            Assert.IsTrue(s.IsDead);
            Assert.IsFalse(s.Damage(10f), "Мёртвую змею повторно не убивают");
            Assert.AreEqual(0f, s.Health);
        }

        [Test]
        public void Dead_CannotBeHealedOrFed()
        {
            SurvivalStats s = Create(10f);
            s.Damage(200f);
            s.Heal(50f);
            s.Feed(50f);
            s.HealFull();
            Assert.AreEqual(0f, s.Health);
            Assert.AreEqual(10f, s.Satiety);
        }

        [Test]
        public void AddMaxHealth_RaisesCurrentToo()
        {
            SurvivalStats s = Create();
            s.Damage(50f);
            s.AddMaxHealth(20f);
            Assert.AreEqual(120f, s.MaxHealth);
            Assert.AreEqual(70f, s.Health);
        }

        [Test]
        public void Spend_TakesSatiety()
        {
            SurvivalStats s = Create(10f);
            s.Spend(4f);
            Assert.AreEqual(6f, s.Satiety, 1e-4f);
            s.Spend(100f);
            Assert.AreEqual(0f, s.Satiety);
        }
    }
}
