using NUnit.Framework;

namespace WildSnake.Tests
{
    /// <summary>Тексты улучшений: у каждого есть название и эффект, плата — только у сильных, на линьке видно прошлое значение.</summary>
    public class UpgradeTextsTests
    {
        [Test]
        public void EveryUpgrade_HasNameAndEffect()
        {
            for (int i = 0; i < UpgradeSet.TYPE_COUNT; i++)
            {
                var type = (UpgradeType)i;
                Assert.IsNotEmpty(UpgradeTexts.Name(type), type.ToString());
                StringAssert.DoesNotContain("{", UpgradeTexts.Effect(type, 1), type.ToString());
            }
        }

        [Test]
        public void Cost_OnlyForStrongUpgrades_AndGrowsWithLevel()
        {
            Assert.AreEqual("Плата: скорость −2%", UpgradeTexts.Cost(UpgradeType.ThickScales, 1));
            Assert.AreEqual("Плата: голод +12%", UpgradeTexts.Cost(UpgradeType.StrongHeart, 3));
            Assert.AreEqual("Плата: заживление ран −20%", UpgradeTexts.Cost(UpgradeType.SlowMetabolism, 2));
            Assert.IsEmpty(UpgradeTexts.Cost(UpgradeType.Swimmer, 3));
            Assert.IsEmpty(UpgradeTexts.Cost(UpgradeType.ThickScales, 0));
        }

        [Test]
        public void Offer_ShowsNextAndPreviousValue()
        {
            Assert.AreEqual("Запас здоровья больше на 15 ед. (обычно 100). Плата: голод +4%", UpgradeTexts.Offer(UpgradeType.StrongHeart, 0));
            string second = UpgradeTexts.Offer(UpgradeType.SwiftBody, 1);
            StringAssert.Contains("7% (было 4%)", second);
            StringAssert.Contains("голод +8% (было 4%)", second);
        }
    }
}
