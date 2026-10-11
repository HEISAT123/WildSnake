using System.Globalization;

namespace WildSnake
{
    /// <summary>Форматирование времени для интерфейса: «2,4» суток, «3:07» минут. Вызывается по событиям, не каждый кадр.</summary>
    public static class TimeFormat
    {
        /// <summary>Прожитые сутки с одним знаком после запятой.</summary>
        public static string Days(float seconds, float dayLengthSeconds)
        {
            float days = dayLengthSeconds > 0f ? seconds / dayLengthSeconds : 0f;
            // Запятая по-русски; без культуры ru-RU — её может не оказаться в сборке.
            return days.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
        }

        /// <summary>Минуты и секунды: 3:07.</summary>
        public static string Clock(int totalSeconds)
        {
            int m = totalSeconds / 60;
            int s = totalSeconds % 60;
            return m + ":" + (s < 10 ? "0" : "") + s;
        }
    }
}
