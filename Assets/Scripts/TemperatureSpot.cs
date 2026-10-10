namespace Snake3D
{
    /// <summary>Где сейчас змея — от этого зависит, греется она или остывает.</summary>
    public enum TemperatureSpot
    {
        /// <summary>Открытое место: на солнце тепло, ночью холодно.</summary>
        Open = 0,
        /// <summary>Тень высокой травы: прохладнее, чем на открытом месте.</summary>
        Shade = 1,
        /// <summary>Солнечный камень: днём быстро греет, вечером ещё отдаёт тепло.</summary>
        SunStone = 2,
        /// <summary>Вода: быстро остужает.</summary>
        Water = 3,
        /// <summary>Нора: ровное умеренное тепло днём и ночью.</summary>
        Burrow = 4
    }
}
