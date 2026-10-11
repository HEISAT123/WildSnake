using System;

namespace WildSnake
{
    /// <summary>
    /// Температура холоднокровной змеи (0 — окоченела, 1 — прогрелась на камне). Тянется к «температуре места»,
    /// которая зависит от света и укрытия: солнечный камень греет, тень и вода остужают, ночью холодно, в норе ровно.
    /// Холодная змея медленнее ползает, хуже бросается и дольше восстанавливает силы.
    /// Отдельной полосы у показателя нет — его передают цвет виньетки и иней на краях экрана.
    /// Обычный C#-класс без Unity API — легко тестировать.
    /// </summary>
    public class BodyTemperature
    {
        /// <summary>Ниже этого значения змее холодно.</summary>
        public float ColdThreshold { get; set; } = 0.35f;

        /// <summary>Выше этого значения змея «прогрета» (тёплая виньетка).</summary>
        public float WarmThreshold { get; set; } = 0.8f;

        /// <summary>Скорость нагрева, долей в секунду (на камне — в StoneBonus раз быстрее).</summary>
        public float HeatRate { get; set; } = 0.05f;

        /// <summary>Скорость остывания, долей в секунду (в воде — в WaterBonus раз быстрее).</summary>
        public float CoolRate { get; set; } = 0.022f;

        public float StoneBonus { get; set; } = 3f;
        public float WaterBonus { get; set; } = 2.5f;

        /// <summary>Насколько холод замедляет движение и бросок при полном окоченении.</summary>
        public float ColdSlowdown { get; set; } = 0.3f;

        /// <summary>Насколько остужает дождь (снижение «температуры места»; в норе не действует).</summary>
        public float Chill { get; set; }

        public float Value01 { get; private set; }

        /// <summary>Насколько змее холодно, 0..1.</summary>
        public float Cold01 => ColdThreshold > 0f ? Clamp01((ColdThreshold - Value01) / ColdThreshold) : 0f;

        /// <summary>Насколько змея прогрета сверх нормы, 0..1.</summary>
        public float Warm01 => WarmThreshold < 1f ? Clamp01((Value01 - WarmThreshold) / (1f - WarmThreshold)) : 0f;

        public bool IsCold => Value01 < ColdThreshold;

        public float SpeedMultiplier => 1f - ColdSlowdown * Cold01;
        public float StrikeMultiplier => 1f - ColdSlowdown * Cold01;
        public float RegenMultiplier => 1f - 0.5f * Cold01;

        public BodyTemperature()
        {
            Reset(0.7f);
        }

        public void Reset(float value01)
        {
            Value01 = Clamp01(value01);
        }

        /// <summary>«Температура места»: к ней тянется тело. daylight01 — 0 ночь, 1 полдень.</summary>
        public static float SpotTarget(TemperatureSpot spot, float daylight01)
        {
            float d = Clamp01(daylight01);
            switch (spot)
            {
                case TemperatureSpot.Shade: return Lerp(0.05f, 0.55f, d);
                case TemperatureSpot.SunStone: return Lerp(0.25f, 1f, d);
                case TemperatureSpot.Water: return Lerp(0.05f, 0.4f, d);
                case TemperatureSpot.Burrow: return 0.55f;
                // Ключ греет ровно, но не жарче полуденного камня: ночное убежище от холода, а не замена солнцу.
                case TemperatureSpot.HotSpring: return 0.85f;
                default: return Lerp(0.05f, 0.75f, d);
            }
        }

        public void Tick(float dt, float daylight01, TemperatureSpot spot)
        {
            if (dt <= 0f) return;
            float target = SpotTarget(spot, daylight01);
            if (spot != TemperatureSpot.Burrow) target = Math.Max(0f, target - Chill);
            if (Value01 < target)
            {
                float rate = HeatRate * (spot == TemperatureSpot.SunStone ? StoneBonus : spot == TemperatureSpot.HotSpring ? 2f : 1f);
                Value01 = Math.Min(target, Value01 + rate * dt);
            }
            else
            {
                float rate = CoolRate * (spot == TemperatureSpot.Water ? WaterBonus : 1f);
                Value01 = Math.Max(target, Value01 - rate * dt);
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
