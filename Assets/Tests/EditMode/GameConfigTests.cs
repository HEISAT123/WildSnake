using NUnit.Framework;
using UnityEngine;

namespace Snake3D.Tests
{
    /// <summary>Значения баланса по умолчанию.</summary>
    public class GameConfigTests
    {
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Defaults_AreConsistent()
        {
            Assert.Greater(_config.WalkSpeed, _config.SneakSpeed, "Красться должно быть медленнее шага");
            Assert.Greater(_config.SprintSpeed, _config.WalkSpeed, "Быстрое ползание должно быть быстрее шага");
            Assert.Greater(_config.ChargedStrikeDistance, _config.StrikeDistance, "Замах должен давать дальний бросок");
            Assert.Greater(_config.MaxLength, _config.StartLength, "Максимальная длина не больше стартовой");
            Assert.Greater(_config.MaxHealth, 0f);
            Assert.Greater(_config.MaxSatiety, 0f);
            Assert.LessOrEqual(_config.StartSatiety, _config.MaxSatiety, "Стартовая сытость больше максимума");
            Assert.Greater(_config.HungerPerSecond, 0f, "Без голода нет выживания");
            Assert.Greater(_config.StrikeCatchRadius, _config.TouchCatchRadius, "Бросок должен ловить дальше касания");
            Assert.Greater(_config.MoltGrowth, 0f);
            Assert.LessOrEqual(_config.EagleIntervalMin, _config.EagleIntervalMax);
            Assert.Greater(_config.DaySeconds, 0f);
            Assert.Greater(_config.NightSeconds, 0f);
        }

        [Test]
        public void PreyStats_ExistForEveryKind()
        {
            int kinds = System.Enum.GetValues(typeof(PreyKind)).Length;
            Assert.AreEqual(kinds, _config.PreyKindCount, "Число параметров добычи должно совпадать с PreyKind");
            for (int i = 0; i < kinds; i++)
            {
                PreyStats s = _config.GetPrey((PreyKind)i);
                Assert.Greater(s.Satiety, 0f, ((PreyKind)i).ToString());
                Assert.Greater(s.Growth, 0f, ((PreyKind)i).ToString());
                Assert.Greater(Mathf.Max(s.DayCount, s.NightCount), 0, ((PreyKind)i) + " никогда не появляется");
            }
        }

        [Test]
        public void Beetle_IsCaughtByTouch_BigPreyNeedsStrike()
        {
            Assert.IsFalse(_config.GetPrey(PreyKind.Beetle).RequiresStrike);
            Assert.IsTrue(_config.GetPrey(PreyKind.Mouse).RequiresStrike);
            Assert.IsTrue(_config.GetPrey(PreyKind.Frog).RequiresStrike);
            Assert.IsTrue(_config.GetPrey(PreyKind.Bird).RequiresStrike);
        }

        [Test]
        public void Rabbit_NeedsConstriction_AndIsHeaviestMeal()
        {
            Assert.IsTrue(_config.RequiresConstrict(PreyKind.Rabbit));
            Assert.IsFalse(_config.RequiresConstrict(PreyKind.Bird));
            Assert.Greater(_config.GetPrey(PreyKind.Rabbit).Satiety, _config.GetPrey(PreyKind.Bird).Satiety, "Кролик должен быть сытнее птицы");
            Assert.Greater(_config.DigestSize(PreyKind.Rabbit), _config.DigestSize(PreyKind.Mouse));
            Assert.AreEqual(0f, _config.DigestSize(PreyKind.Beetle), "От жука бугра нет");
        }

        [Test]
        public void EnsurePreyDefaults_ExtendsOldAsset_KeepsValues()
        {
            var field = typeof(GameConfig).GetField("_prey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var old = new[] { new PreyStats(1f, 1f, 1f, 1f, 1f, 1f, false, 1, 1) };
            field.SetValue(_config, old);
            Assert.IsTrue(_config.EnsurePreyDefaults());
            Assert.AreEqual(1f, _config.GetPrey(PreyKind.Beetle).Satiety, "Настроенное значение не должно сбрасываться");
            Assert.Greater(_config.GetPrey(PreyKind.Rabbit).Satiety, 1f);
            Assert.IsFalse(_config.EnsurePreyDefaults());
        }

        [Test]
        public void FleeingPrey_IsFasterThanSnake_ButSlowerThanStrike()
        {
            float strike = _config.StrikeSpeed(0f);
            PreyStats mouse = _config.GetPrey(PreyKind.Mouse);
            Assert.Greater(mouse.FleeSpeed, _config.WalkSpeed, "Мышь должна убегать от ползущей змеи — иначе бросок не нужен");
            Assert.Less(mouse.FleeSpeed, strike, "Бросок должен догонять мышь");
        }
    }
}
