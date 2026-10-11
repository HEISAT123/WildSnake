using System.Collections.Generic;
using NUnit.Framework;

namespace Snake3D.Tests
{
    /// <summary>Улучшения линьки: уровни, предел, эффекты и выбор вариантов.</summary>
    public class UpgradeSetTests
    {
        [Test]
        public void TypeCount_MatchesEnum()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(UpgradeType)).Length, UpgradeSet.TYPE_COUNT);
        }

        [Test]
        public void Defaults_AreNeutral()
        {
            var set = new UpgradeSet();
            Assert.AreEqual(1f, set.DamageTakenMultiplier);
            Assert.AreEqual(1f, set.HungerMultiplier);
            Assert.AreEqual(1f, set.SpeedMultiplier);
            Assert.AreEqual(0f, set.MaxHealthBonus);
            Assert.AreEqual(0, set.TotalLevels);
        }

        [Test]
        public void Apply_StopsAtMaxLevel()
        {
            var set = new UpgradeSet();
            for (int i = 0; i < UpgradeSet.MAX_LEVEL; i++) Assert.IsTrue(set.Apply(UpgradeType.ThickScales));
            Assert.IsFalse(set.Apply(UpgradeType.ThickScales));
            Assert.AreEqual(UpgradeSet.MAX_LEVEL, set.Level(UpgradeType.ThickScales));
            Assert.Greater(set.DamageTakenMultiplier, 0f, "Даже на максимуме урон не должен исчезать");
        }

        [Test]
        public void Effects_ChangeInRightDirection()
        {
            var set = new UpgradeSet();
            foreach (UpgradeType t in System.Enum.GetValues(typeof(UpgradeType))) set.Apply(t);
            Assert.Less(set.DamageTakenMultiplier, 1f);
            Assert.Less(set.HungerMultiplier, 1f);
            Assert.Greater(set.StrikeDistanceMultiplier, 1f);
            Assert.Less(set.StrikeCostMultiplier, 1f);
            Assert.Greater(set.SmellRadiusMultiplier, 1f);
            Assert.Less(set.SmellCooldownMultiplier, 1f);
            Assert.Less(set.AwarenessMultiplier, 1f);
            Assert.Greater(set.MaxHealthBonus, 0f);
            Assert.Greater(set.SpeedMultiplier, 1f);
            Assert.Greater(set.FeedMultiplier, 1f);
            Assert.Greater(set.StaminaRegenMultiplier, 1f);
            Assert.Less(set.CoolRateMultiplier, 1f);
            Assert.Less(set.ConstrictDurationMultiplier, 1f);
            Assert.Less(set.AmbushDelayMultiplier, 1f);
            Assert.Greater(set.SwimSpeedMultiplier, 1f);
        }

        [Test]
        public void Effects_HaveDiminishingReturns()
        {
            float first = UpgradeSet.Strength(1);
            float second = UpgradeSet.Strength(2) - UpgradeSet.Strength(1);
            float third = UpgradeSet.Strength(3) - UpgradeSet.Strength(2);
            Assert.Greater(first, second);
            Assert.Greater(second, third);
            Assert.AreEqual(1f, UpgradeSet.Strength(UpgradeSet.MAX_LEVEL), 1e-5f);
        }

        [Test]
        public void MaxedOut_IsNotInvulnerable()
        {
            var set = new UpgradeSet();
            for (int i = 0; i < UpgradeSet.MAX_LEVEL; i++)
            {
                set.Apply(UpgradeType.ThickScales);
                set.Apply(UpgradeType.SlowMetabolism);
                set.Apply(UpgradeType.StrongHeart);
            }
            // Урон и голод заметно снижены, но не обнулены; у сильных улучшений есть плата.
            Assert.GreaterOrEqual(set.DamageTakenMultiplier, 0.65f);
            Assert.GreaterOrEqual(set.HungerMultiplier, 0.75f);
            Assert.Less(set.SpeedMultiplier, 1f);
            Assert.Less(set.HealthRegenMultiplier, 1f);
            Assert.LessOrEqual(set.MaxHealthBonus, 45f);
        }

        [Test]
        public void RollChoices_GivesDistinctAvailable()
        {
            var set = new UpgradeSet();
            var rng = new System.Random(1);
            var result = new UpgradeType[3];
            for (int attempt = 0; attempt < 50; attempt++)
            {
                Assert.AreEqual(3, set.RollChoices(result, rng));
                var seen = new HashSet<UpgradeType>(result);
                Assert.AreEqual(3, seen.Count, "Варианты не должны повторяться");
            }
        }

        [Test]
        public void RollChoices_SkipsMaxedOut()
        {
            var set = new UpgradeSet();
            foreach (UpgradeType t in System.Enum.GetValues(typeof(UpgradeType)))
            {
                if (t == UpgradeType.KeenSmell) continue;
                for (int i = 0; i < UpgradeSet.MAX_LEVEL; i++) set.Apply(t);
            }

            var result = new UpgradeType[3];
            Assert.AreEqual(1, set.RollChoices(result, new System.Random(7)));
            Assert.AreEqual(UpgradeType.KeenSmell, result[0]);

            for (int i = 0; i < UpgradeSet.MAX_LEVEL; i++) set.Apply(UpgradeType.KeenSmell);
            Assert.AreEqual(0, set.RollChoices(result, new System.Random(7)));
        }

        [Test]
        public void Reset_ClearsLevels()
        {
            var set = new UpgradeSet();
            set.Apply(UpgradeType.SwiftBody);
            set.Reset();
            Assert.AreEqual(0, set.TotalLevels);
            Assert.AreEqual(1f, set.SpeedMultiplier);
        }
    }
}
