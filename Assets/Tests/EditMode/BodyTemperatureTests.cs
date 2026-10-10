using NUnit.Framework;

namespace Snake3D.Tests
{
    /// <summary>Температура: солнечный камень греет, ночь и вода остужают, холод замедляет.</summary>
    public class BodyTemperatureTests
    {
        [Test]
        public void Night_CoolsDown_AndSlows()
        {
            var t = new BodyTemperature();
            t.Reset(0.7f);
            for (int i = 0; i < 60; i++) t.Tick(1f, 0f, TemperatureSpot.Open);
            Assert.IsTrue(t.IsCold, "За минуту ночи змея должна замёрзнуть");
            Assert.Less(t.SpeedMultiplier, 1f);
            Assert.Less(t.StrikeMultiplier, 1f);
            Assert.Less(t.RegenMultiplier, 1f);
        }

        [Test]
        public void SunStone_WarmsFasterThanOpen()
        {
            var stone = new BodyTemperature();
            var open = new BodyTemperature();
            stone.Reset(0.2f);
            open.Reset(0.2f);
            for (int i = 0; i < 5; i++)
            {
                stone.Tick(1f, 1f, TemperatureSpot.SunStone);
                open.Tick(1f, 1f, TemperatureSpot.Open);
            }
            Assert.Greater(stone.Value01, open.Value01);
            for (int i = 0; i < 20; i++) stone.Tick(1f, 1f, TemperatureSpot.SunStone);
            Assert.Greater(stone.Warm01, 0f, "На камне в полдень змея прогревается сверх нормы");
            Assert.AreEqual(1f, stone.SpeedMultiplier);
        }

        [Test]
        public void Water_CoolsFasterThanShade()
        {
            var water = new BodyTemperature();
            var shade = new BodyTemperature();
            for (int i = 0; i < 4; i++)
            {
                water.Tick(1f, 0.5f, TemperatureSpot.Water);
                shade.Tick(1f, 0.5f, TemperatureSpot.Shade);
            }
            Assert.Less(water.Value01, shade.Value01);
        }

        [Test]
        public void Burrow_KeepsSteadyWarmth()
        {
            var t = new BodyTemperature();
            t.Reset(0.1f);
            for (int i = 0; i < 100; i++) t.Tick(1f, 0f, TemperatureSpot.Burrow);
            Assert.AreEqual(BodyTemperature.SpotTarget(TemperatureSpot.Burrow, 0f), t.Value01, 1e-4f);
            Assert.IsFalse(t.IsCold, "В норе ночью не холодно");
        }

        [Test]
        public void NeverOvershootsTarget()
        {
            var t = new BodyTemperature();
            t.Reset(0.5f);
            t.Tick(100f, 1f, TemperatureSpot.Open);
            Assert.AreEqual(BodyTemperature.SpotTarget(TemperatureSpot.Open, 1f), t.Value01, 1e-4f);
        }
    }
}
