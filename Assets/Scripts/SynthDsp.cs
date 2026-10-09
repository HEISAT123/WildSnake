using System;

namespace Snake3D
{
    /// <summary>
    /// Общие «кирпичики» синтеза для музыки (MusicSynth) и звуков (SfxSynth): осцилляторы без алиасинга,
    /// реверб, пинг-понг дилей, мягкое ограничение. Только System.Math — безопасно в фоновом потоке.
    /// </summary>
    public static class SynthDsp
    {
        public const int SAMPLE_RATE = 44100;
        public const double TWO_PI = Math.PI * 2.0;

        /// <summary>PolyBLEP убирает алиасинг на скачке пилы — звук чище, чем у «наивной» пилы.</summary>
        public static double PolyBlep(double t, double dt)
        {
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1.0;
            }
            if (t > 1.0 - dt)
            {
                t = (t - 1.0) / dt;
                return t * t + t + t + 1.0;
            }
            return 0.0;
        }

        public static float SoftClip(float x)
        {
            return (float)Math.Tanh(x * 1.2);
        }

        public static double MidiToHz(double midi)
        {
            return 440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0);
        }

        /// <summary>Коэффициент однополюсного фильтра нижних частот для частоты среза cutoff.</summary>
        public static double LowpassCoef(double cutoff)
        {
            return 1.0 - Math.Exp(-TWO_PI * Math.Min(cutoff, SAMPLE_RATE * 0.45) / SAMPLE_RATE);
        }

        /// <summary>Пинг-понг дилей: повторы перескакивают между каналами.</summary>
        public static void ApplyPingPongDelay(float[] left, float[] right, int delay, float feedback)
        {
            for (int i = delay; i < left.Length; i++)
            {
                float fromL = left[i - delay];
                float fromR = right[i - delay];
                left[i] += fromR * feedback;
                right[i] += fromL * feedback;
            }
        }

        /// <summary>Упрощённый Freeverb: 4 гребенчатых фильтра с демпфированием и 2 всепропускающих на канал.</summary>
        public static void ApplyReverb(float[] send, float[] left, float[] right, float wet, float feedback = 0.84f)
        {
            int[] combs = { 1116, 1188, 1277, 1356 };
            int[] allpass = { 556, 441 };
            const int spread = 23;
            ReverbChannel(send, left, combs, allpass, 0, wet, feedback);
            ReverbChannel(send, right, combs, allpass, spread, wet, feedback);
        }

        private static void ReverbChannel(float[] input, float[] output, int[] combLengths, int[] allpassLengths, int spread, float wet, float feedback)
        {
            int count = input.Length;
            var combBuf = new float[combLengths.Length][];
            var combIdx = new int[combLengths.Length];
            var combStore = new float[combLengths.Length];
            for (int c = 0; c < combLengths.Length; c++) combBuf[c] = new float[combLengths[c] + spread];

            var apBuf = new float[allpassLengths.Length][];
            var apIdx = new int[allpassLengths.Length];
            for (int a = 0; a < allpassLengths.Length; a++) apBuf[a] = new float[allpassLengths[a] + spread];

            const float damp = 0.25f;
            for (int i = 0; i < count; i++)
            {
                float x = input[i] * 0.3f;
                float sum = 0f;
                for (int c = 0; c < combBuf.Length; c++)
                {
                    float[] buf = combBuf[c];
                    float y = buf[combIdx[c]];
                    combStore[c] = y * (1f - damp) + combStore[c] * damp;
                    buf[combIdx[c]] = x + combStore[c] * feedback;
                    if (++combIdx[c] >= buf.Length) combIdx[c] = 0;
                    sum += y;
                }

                for (int a = 0; a < apBuf.Length; a++)
                {
                    float[] buf = apBuf[a];
                    float bufOut = buf[apIdx[a]];
                    float y = -sum + bufOut;
                    buf[apIdx[a]] = sum + bufOut * 0.5f;
                    if (++apIdx[a] >= buf.Length) apIdx[a] = 0;
                    sum = y;
                }

                output[i] += sum * wet;
            }
        }
    }
}
