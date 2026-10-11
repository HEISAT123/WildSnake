using System;

namespace WildSnake
{
    /// <summary>
    /// Звуки природы, синтезированные кодом: дождь, птицы днём, сверчки ночью, шорох травы. Каждый — бесшовная
    /// петля (хвост плавно перетекает в начало). Работает в фоновом потоке: только System.Math, без Unity API.
    /// Результат — чередующиеся стерео-сэмплы (L, R, L, R…).
    /// </summary>
    public static class AmbientSynth
    {
        public const int SAMPLE_RATE = SynthDsp.SAMPLE_RATE;
        private const double FADE = 0.6;

        /// <summary>Дождь: шипящий шум в двух каналах и редкие тяжёлые капли.</summary>
        public static float[] Rain(out int frames)
        {
            var rng = new SynthRng(101);
            return Loop(9.0, out frames, (l, r, n) =>
            {
                double lpL = 0, lpR = 0, slowL = 0, slowR = 0;
                for (int i = 0; i < n; i++)
                {
                    lpL += (rng.NextSigned() - lpL) * 0.35;
                    lpR += (rng.NextSigned() - lpR) * 0.35;
                    // Убираем низ (вычитаем медленную составляющую) — получается шелест капель, а не гул.
                    slowL += (lpL - slowL) * 0.02;
                    slowR += (lpR - slowR) * 0.02;
                    l[i] = (float)((lpL - slowL) * 0.3);
                    r[i] = (float)((lpR - slowR) * 0.3);
                }
                // Тяжёлые капли: короткие щелчки с затуханием, раскиданные по панораме.
                for (int k = 0; k < (int)(n / (double)SAMPLE_RATE * 40); k++)
                {
                    int start = (int)(rng.Next01() * (n - 2000));
                    double pan = rng.Next01();
                    double amp = 0.08 + rng.Next01() * 0.12;
                    double freq = 2500 + rng.Next01() * 3500;
                    for (int j = 0; j < 1500; j++)
                    {
                        double env = Math.Exp(-j / 160.0);
                        double s = Math.Sin(SynthDsp.TWO_PI * freq * j / SAMPLE_RATE) * env * amp;
                        l[start + j] += (float)(s * (1 - pan));
                        r[start + j] += (float)(s * pan);
                    }
                }
            });
        }

        /// <summary>Птицы: тихий ветер и редкие трели — короткие свисты с подъёмом и спадом тона.</summary>
        public static float[] Birds(out int frames)
        {
            var rng = new SynthRng(202);
            return Loop(14.0, out frames, (l, r, n) =>
            {
                Wind(rng, l, r, n, 0.04);
                double t = 0.3;
                double length = n / (double)SAMPLE_RATE;
                while (t < length - 1.0)
                {
                    double pan = 0.15 + rng.Next01() * 0.7;
                    double baseHz = 2400 + rng.Next01() * 2600;
                    int notes = 2 + (int)(rng.Next01() * 5);
                    double amp = 0.05 + rng.Next01() * 0.07;
                    for (int k = 0; k < notes; k++)
                    {
                        double dur = 0.05 + rng.Next01() * 0.09;
                        double from = baseHz * (0.85 + rng.Next01() * 0.3);
                        double to = from * (rng.Next01() < 0.5 ? 1.35 : 0.75);
                        Chirp(l, r, t, dur, from, to, amp, pan);
                        t += dur + 0.03 + rng.Next01() * 0.05;
                    }
                    t += 0.5 + rng.Next01() * 1.8;
                }
            });
        }

        /// <summary>Сверчки: несколько «голосов» на высокой ноте, пачки коротких импульсов, и ночной ветерок.</summary>
        public static float[] Crickets(out int frames)
        {
            var rng = new SynthRng(303);
            return Loop(10.0, out frames, (l, r, n) =>
            {
                Wind(rng, l, r, n, 0.025);
                double length = n / (double)SAMPLE_RATE;
                for (int voice = 0; voice < 3; voice++)
                {
                    double hz = 4100 + voice * 330 + rng.Next01() * 200;
                    double pan = 0.2 + voice * 0.3;
                    double amp = 0.035 + rng.Next01() * 0.02;
                    double t = rng.Next01() * 0.5;
                    while (t < length - 0.5)
                    {
                        int pulses = 3 + (int)(rng.Next01() * 2);
                        for (int p = 0; p < pulses; p++)
                        {
                            Chirp(l, r, t, 0.022, hz, hz, amp, pan);
                            t += 0.045;
                        }
                        t += 0.35 + rng.Next01() * 0.4;
                    }
                }
            });
        }

        /// <summary>Шорох травы под змеёй: шум в средних частотах с неровной «дрожью» громкости.</summary>
        public static float[] Rustle(out int frames)
        {
            var rng = new SynthRng(404);
            return Loop(3.0, out frames, (l, r, n) =>
            {
                double bp = 0, lp = 0, flutter = 0.5;
                for (int i = 0; i < n; i++)
                {
                    double w = rng.NextSigned();
                    lp += (w - lp) * 0.25;
                    bp = w - lp;
                    if (i % 900 == 0) flutter = 0.35 + rng.Next01() * 0.65;
                    double s = bp * flutter * 0.18;
                    l[i] = (float)(s * (0.8 + 0.2 * Math.Sin(i * 0.0007)));
                    r[i] = (float)(s * (0.8 + 0.2 * Math.Cos(i * 0.0005)));
                }
            });
        }

        // ---------------- Блоки ----------------

        private static void Wind(SynthRng rng, float[] l, float[] r, int n, double amp)
        {
            double a = 0, b = 0, swell = 0;
            for (int i = 0; i < n; i++)
            {
                a += (rng.NextSigned() - a) * 0.004;
                b += (rng.NextSigned() - b) * 0.004;
                swell = 0.6 + 0.4 * Math.Sin(i / (double)SAMPLE_RATE * 0.7);
                l[i] += (float)(a * amp * 6 * swell);
                r[i] += (float)(b * amp * 6 * swell);
            }
        }

        /// <summary>Свист с плавным изменением тона и мягкой огибающей.</summary>
        private static void Chirp(float[] l, float[] r, double start, double duration, double fromHz, double toHz, double amp, double pan)
        {
            int s0 = (int)(start * SAMPLE_RATE);
            int len = (int)(duration * SAMPLE_RATE);
            double phase = 0;
            for (int j = 0; j < len && s0 + j < l.Length; j++)
            {
                double k = j / (double)len;
                double hz = fromHz + (toHz - fromHz) * k;
                phase += SynthDsp.TWO_PI * hz / SAMPLE_RATE;
                double env = Math.Sin(Math.PI * k);
                double v = Math.Sin(phase) * env * env * amp;
                l[s0 + j] += (float)(v * (1 - pan));
                r[s0 + j] += (float)(v * pan);
            }
        }

        /// <summary>
        /// Рендер с запасом на FADE секунд и склейка: хвост плавно накладывается на начало — петля без щелчка.
        /// </summary>
        private static float[] Loop(double seconds, out int frames, Action<float[], float[], int> render)
        {
            frames = (int)(seconds * SAMPLE_RATE);
            int fade = (int)(FADE * SAMPLE_RATE);
            int n = frames + fade;
            var l = new float[n];
            var r = new float[n];
            render(l, r, n);
            var result = new float[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                float vl = l[i], vr = r[i];
                if (i < fade)
                {
                    float k = i / (float)fade;
                    vl = vl * k + l[frames + i] * (1f - k);
                    vr = vr * k + r[frames + i] * (1f - k);
                }
                result[i * 2] = SynthDsp.SoftClip(vl);
                result[i * 2 + 1] = SynthDsp.SoftClip(vr);
            }
            return result;
        }
    }
}
