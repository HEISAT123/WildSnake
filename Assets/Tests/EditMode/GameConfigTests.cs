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
        public void ShieldDuration_IsZero()
        {
            Assert.AreEqual(0f, _config.GetPowerUpDuration(PowerUpType.Shield));
        }

        [TestCase(PowerUpType.Speed)]
        [TestCase(PowerUpType.SlowTime)]
        [TestCase(PowerUpType.Magnet)]
        [TestCase(PowerUpType.DoubleScore)]
        public void TimedPowerUpDuration_IsPositive(PowerUpType type)
        {
            Assert.Greater(_config.GetPowerUpDuration(type), 0f);
        }

        [Test]
        public void Defaults_AreConsistent()
        {
            Assert.Greater(_config.StartSpeed, 0f);
            Assert.GreaterOrEqual(_config.MaxSpeed, _config.StartSpeed, "Максимальная скорость меньше стартовой");
            Assert.Greater(_config.MaxLength, _config.StartLength, "Максимальная длина не больше стартовой");
            Assert.Greater(_config.GrowthPerFood, 0f);
            Assert.Greater(_config.ApplesPerLevel, 0);
            Assert.LessOrEqual(_config.GoldenChance + _config.RottenChance, 1f, "Сумма шансов золотого и гнилого больше 1");
            Assert.Greater(_config.TimeAttackSeconds, 0f);
            Assert.Greater(_config.SlowTimeScale, 0f);
            Assert.LessOrEqual(_config.SlowTimeScale, 1f);
        }
    }
}
