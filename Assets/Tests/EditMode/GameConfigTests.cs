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
            Assert.Greater(_config.MoveSpeed, 0f);
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
        public void FleeingPrey_IsFasterThanSnake_ButSlowerThanStrike()
        {
            float strike = _config.MoveSpeed * _config.DashMultiplier;
            PreyStats mouse = _config.GetPrey(PreyKind.Mouse);
            Assert.Greater(mouse.FleeSpeed, _config.MoveSpeed, "Мышь должна убегать от ползущей змеи — иначе бросок не нужен");
            Assert.Less(mouse.FleeSpeed, strike, "Бросок должен догонять мышь");
        }
    }
}
