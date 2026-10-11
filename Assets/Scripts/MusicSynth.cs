using System;

namespace WildSnake
{
    /// <summary>
    /// Синтез фоновой музыки в стиле synthwave / chill electronic: пэды на расстроенных пилах,
    /// басовая линия с фильтром, FM-арпеджио с пинг-понг дилеем, мелодия, барабаны, реверб и сайдчейн.
    /// Только System.Math и массивы — без Unity API, поэтому можно вызывать из фонового потока.
    /// Результат — бесшовная петля (хвосты реверба и дилея заворачиваются в начало).
    /// </summary>
    public static class MusicSynth
    {
        public const int SAMPLE_RATE = 44100;

        private const double BPM = 98.0;
        private const int BARS = 16;
        private const double TWO_PI = Math.PI * 2.0;

        // Аккорды по тактам (MIDI): Am9, Fmaj9, Cmaj9, G6 — повторяются каждые 4 такта.
        private static readonly int[][] Chords =
        {
            new[] { 57, 60, 64, 67, 71 },
            new[] { 53, 57, 60, 64, 67 },
            new[] { 48, 52, 55, 59, 62 },
            new[] { 55, 59, 62, 64, 67 }
        };

        private static readonly int[] BassRoots = { 33, 29, 36, 31 };

        // Мелодия на 4 такта: (доля начала, длительность в долях, нота). Ля-минорная пентатоника.
        private static readonly double[][] MelodyA =
        {
            new[] { 0.0, 1.5, 76.0 }, new[] { 1.5, 0.5, 74.0 }, new[] { 2.0, 2.0, 72.0 },
            new[] { 4.0, 1.0, 69.0 }, new[] { 5.0, 1.0, 72.0 }, new[] { 6.0, 2.0, 74.0 },
            new[] { 8.0, 1.5, 76.0 }, new[] { 9.5, 0.5, 79.0 }, new[] { 10.0, 2.0, 76.0 },
            new[] { 12.0, 1.0, 74.0 }, new[] { 13.0, 1.0, 72.0 }, new[] { 14.0, 2.0, 71.0 }
        };

        private static readonly double[][] MelodyB =
        {
            new[] { 0.0, 0.5, 81.0 }, new[] { 0.5, 0.5, 79.0 }, new[] { 1.0, 1.0, 76.0 }, new[] { 2.0, 2.0, 74.0 },
            new[] { 4.0, 1.5, 72.0 }, new[] { 5.5, 0.5, 74.0 }, new[] { 6.0, 2.0, 76.0 },
            new[] { 8.0, 1.0, 79.0 }, new[] { 9.0, 1.0, 81.0 }, new[] { 10.0, 2.0, 84.0 },
            new[] { 12.0, 1.0, 81.0 }, new[] { 13.0, 1.0, 79.0 }, new[] { 14.0, 2.0, 76.0 }
        };

        // Порядок нот аккорда в арпеджио на 16 шестнадцатых.
        private static readonly int[] ArpPattern = { 0, 2, 4, 1, 3, 2, 4, 3, 0, 2, 4, 1, 3, 4, 2, 1 };

        // Громкость хэтов по шестнадцатым: акценты на восьмых.
        private static readonly float[] HatVelocity = { 0.9f, 0.35f, 0.6f, 0.35f, 0.9f, 0.35f, 0.6f, 0.45f, 0.9f, 0.35f, 0.6f, 0.35f, 0.9f, 0.35f, 0.7f, 0.5f };

        /// <summary>Отрисовать петлю. Возвращает чередующиеся стерео-сэмплы (L, R, L, R...).</summary>
        public static float[] RenderLoop(out int frames)
        {
            double beat = 60.0 / BPM;
            double bar = beat * 4.0;
            int loopFrames = (int)(BARS * bar * SAMPLE_RATE);
            int tail = (int)(2.0 * bar * SAMPLE_RATE);
            int total = loopFrames + tail;

            var left = new float[total];
            var right = new float[total];
            var arpL = new float[total];
            var arpR = new float[total];
            var send = new float[total];
            var rng = new SynthRng(20261010);

            for (int b = 0; b < BARS; b++)
            {
                double barStart = b * bar;
                int chord = b % 4;
                bool intro = b < 4;
                bool lead = b >= 8;

                AddPad(left, right, send, Chords[chord], barStart, bar, intro ? 0.05f : 0.065f, beat, intro);
                AddArp(arpL, arpR, Chords[chord], barStart, beat, intro ? 0.05f : 0.075f, intro);

                if (!intro)
                {
                    AddBassBar(left, right, BassRoots[chord], barStart, beat);
                    AddDrumsBar(left, right, send, barStart, beat, rng, b == BARS - 1);
                }
                else
                {
                    AddHatsBar(left, right, barStart, beat, rng, 0.5f);
                }

                // Мелодия: фразы по 4 такта, во второй половине — другая.
                if (lead && b % 4 == 0)
                    AddMelody(left, right, send, b >= 12 ? MelodyB : MelodyA, barStart, beat);
            }

            SynthDsp.ApplyPingPongDelay(arpL, arpR, (int)(beat * 0.75 * SAMPLE_RATE), 0.38f);
            for (int i = 0; i < total; i++)
            {
                // Сайдчейн: всё, кроме барабанов, «приседает» на каждую бочку — фирменная «качка» жанра.
                float duck = Duck(i, beat, 4 * bar);
                left[i] += arpL[i] * duck;
                right[i] += arpR[i] * duck;
                send[i] += (arpL[i] + arpR[i]) * 0.25f;
            }

            SynthDsp.ApplyReverb(send, left, right, 0.22f);

            // Заворачиваем хвосты в начало — петля звучит без шва.
            for (int i = 0; i < tail; i++)
            {
                left[i] += left[loopFrames + i];
                right[i] += right[loopFrames + i];
            }

            float peak = 0f;
            for (int i = 0; i < loopFrames; i++)
            {
                left[i] = SynthDsp.SoftClip(left[i]);
                right[i] = SynthDsp.SoftClip(right[i]);
                peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i])));
            }

            float gain = peak > 0f ? 0.85f / peak : 1f;
            var data = new float[loopFrames * 2];
            for (int i = 0; i < loopFrames; i++)
            {
                data[i * 2] = left[i] * gain;
                data[i * 2 + 1] = right[i] * gain;
            }

            frames = loopFrames;
            return data;
        }

        // ---------------- Инструменты ----------------

        /// <summary>Пэд: по три расстроенные пилы на ноту, мягкая атака, плавающий фильтр.</summary>
        private static void AddPad(float[] left, float[] right, float[] send, int[] chord, double start, double length, float gain, double beat, bool intro)
        {
            double attack = 0.5, release = 0.9;
            int s0 = (int)(start * SAMPLE_RATE);
            int n = (int)((length + release) * SAMPLE_RATE);
            double[] detune = { -0.11, 0.0, 0.09 };
            float[] pan = { -0.7f, 0f, 0.7f };

            for (int k = 0; k < chord.Length; k++)
            {
                double baseFreq = SynthDsp.MidiToHz(chord[k]);
                for (int v = 0; v < 3; v++)
                {
                    double freq = baseFreq * Math.Pow(2.0, detune[v] / 12.0);
                    double phase = (k * 0.37 + v * 0.21) % 1.0;
                    double lp1 = 0, lp2 = 0, a = 0;
                    float pl = (1f - pan[v]) * 0.5f, pr = (1f + pan[v]) * 0.5f;

                    for (int i = 0; i < n; i++)
                    {
                        int idx = s0 + i;
                        if (idx >= left.Length) break;
                        double t = (double)i / SAMPLE_RATE;
                        double env = t < attack ? t / attack : t < length ? 1.0 : Math.Max(0.0, 1.0 - (t - length) / release);

                        double dt = freq / SAMPLE_RATE;
                        phase += dt;
                        if (phase >= 1.0) phase -= 1.0;
                        double saw = 2.0 * phase - 1.0 - SynthDsp.PolyBlep(phase, dt);

                        // Фильтр медленно «дышит»: в интро закрыт сильнее. Коэффициент пересчитываем
                        // раз в 32 сэмпла — на слух не отличить, а экспонента дорогая.
                        if ((i & 31) == 0)
                        {
                            double time = (double)idx / SAMPLE_RATE;
                            double cutoff = (intro ? 600.0 : 1100.0) + 500.0 * Math.Sin(TWO_PI * time / (beat * 16.0));
                            a = 1.0 - Math.Exp(-TWO_PI * cutoff / SAMPLE_RATE);
                        }
                        lp1 += a * (saw - lp1);
                        lp2 += a * (lp1 - lp2);

                        float s = (float)(lp2 * env) * gain;
                        float duck = Duck(idx, beat, intro ? double.MaxValue : beat * 16.0);
                        left[idx] += s * pl * duck;
                        right[idx] += s * pr * duck;
                        send[idx] += s * 0.5f;
                    }
                }
            }
        }

        /// <summary>FM-«колокольчик»: модулятор на удвоенной частоте с затухающим индексом.</summary>
        private static void AddArp(float[] arpL, float[] arpR, int[] chord, double barStart, double beat, float gain, bool intro)
        {
            double step = beat / 4.0;
            for (int s = 0; s < 16; s++)
            {
                if (intro && s % 2 == 1) continue; // в интро — восьмыми, реже
                int note = chord[ArpPattern[s] % chord.Length] + 12;
                double start = barStart + s * step;
                float pan = s % 2 == 0 ? -0.45f : 0.45f;
                float vel = s % 4 == 0 ? 1f : 0.75f;
                AddFmPluck(arpL, arpR, start, SynthDsp.MidiToHz(note), 0.45, gain * vel, pan, 2.0, 2.5, 0.12);
            }
        }

        private static void AddFmPluck(float[] left, float[] right, double start, double freq, double duration, float gain, float pan,
            double ratio, double index, double indexDecay)
        {
            int s0 = (int)(start * SAMPLE_RATE);
            int n = (int)(duration * SAMPLE_RATE);
            float pl = (1f - pan) * 0.5f, pr = (1f + pan) * 0.5f;
            double cPhase = 0, mPhase = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= left.Length) break;
                double t = (double)i / SAMPLE_RATE;
                double amp = Math.Exp(-t / (duration * 0.35)) * Math.Min(1.0, t / 0.003);
                double modIndex = index * Math.Exp(-t / indexDecay);
                mPhase += freq * ratio / SAMPLE_RATE;
                cPhase += freq / SAMPLE_RATE;
                double s = Math.Sin(TWO_PI * cPhase + modIndex * Math.Sin(TWO_PI * mPhase));
                float v = (float)(s * amp) * gain;
                left[idx] += v * pl;
                right[idx] += v * pr;
            }
        }

        /// <summary>Бас: пила + саб-синус, фильтр с огибающей на каждой ноте. Восьмые с октавными скачками.</summary>
        private static void AddBassBar(float[] left, float[] right, int root, double barStart, double beat)
        {
            double eighth = beat / 2.0;
            int[] octave = { 0, 0, 12, 0, 0, 12, 0, 7 };
            double rootFreq = SynthDsp.MidiToHz(root);
            for (int e = 0; e < 8; e++)
            {
                double freq = rootFreq * Math.Pow(2.0, octave[e] / 12.0);
                int s0 = (int)((barStart + e * eighth) * SAMPLE_RATE);
                int n = (int)(eighth * 0.92 * SAMPLE_RATE);
                double phase = 0, sub = 0, lp1 = 0, lp2 = 0;
                for (int i = 0; i < n; i++)
                {
                    int idx = s0 + i;
                    if (idx >= left.Length) break;
                    double t = (double)i / SAMPLE_RATE;
                    double dt = freq / SAMPLE_RATE;
                    phase += dt;
                    if (phase >= 1.0) phase -= 1.0;
                    sub += dt * 0.5;
                    double saw = 2.0 * phase - 1.0 - SynthDsp.PolyBlep(phase, dt);

                    double cutoff = 180.0 + 1400.0 * Math.Exp(-t / 0.07);
                    double a = 1.0 - Math.Exp(-TWO_PI * cutoff / SAMPLE_RATE);
                    lp1 += a * (saw - lp1);
                    lp2 += a * (lp1 - lp2);

                    double env = Math.Min(1.0, t / 0.004) * Math.Min(1.0, (n - i) / (0.01 * SAMPLE_RATE));
                    float s = (float)((lp2 * 0.55 + Math.Sin(TWO_PI * sub) * 0.5) * env) * 0.32f;
                    float duck = Duck(idx, beat, beat * 16.0);
                    left[idx] += s * duck;
                    right[idx] += s * duck;
                }
            }
        }

        private static void AddDrumsBar(float[] left, float[] right, float[] send, double barStart, double beat, SynthRng rng, bool fill)
        {
            for (int q = 0; q < 4; q++)
            {
                AddKick(left, right, barStart + q * beat);
                if (q == 1 || q == 3) AddClap(left, right, send, barStart + q * beat, rng);
            }

            AddHatsBar(left, right, barStart, beat, rng, 1f);

            // Сбивка в последнем такте петли.
            if (fill)
                for (int s = 12; s < 16; s++)
                    AddClap(left, right, send, barStart + s * beat / 4.0, rng, 0.5f + 0.15f * (s - 12));
        }

        private static void AddKick(float[] left, float[] right, double start)
        {
            int s0 = (int)(start * SAMPLE_RATE);
            int n = (int)(0.45 * SAMPLE_RATE);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= left.Length) break;
                double t = (double)i / SAMPLE_RATE;
                double freq = 46.0 + 120.0 * Math.Exp(-t / 0.035);
                phase += freq / SAMPLE_RATE;
                double amp = Math.Exp(-t / 0.3) * Math.Min(1.0, t / 0.001);
                float s = (float)(Math.Sin(TWO_PI * phase) * amp) * 0.85f;
                left[idx] += s;
                right[idx] += s;
            }
        }

        /// <summary>Клэп: три коротких шумовых всплеска и хвост, полосовой «характер» через разность фильтров.</summary>
        private static void AddClap(float[] left, float[] right, float[] send, double start, SynthRng rng, float gain = 1f)
        {
            int s0 = (int)(start * SAMPLE_RATE);
            int n = (int)(0.3 * SAMPLE_RATE);
            double lpLow = 0, lpHigh = 0;
            double aLow = 1.0 - Math.Exp(-TWO_PI * 900.0 / SAMPLE_RATE);
            double aHigh = 1.0 - Math.Exp(-TWO_PI * 3500.0 / SAMPLE_RATE);
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= left.Length) break;
                double t = (double)i / SAMPLE_RATE;
                double env = t < 0.03 ? Math.Exp(-(t % 0.01) / 0.003) : Math.Exp(-(t - 0.03) / 0.08);
                double noise = rng.NextSigned();
                lpLow += aLow * (noise - lpLow);
                lpHigh += aHigh * (noise - lpHigh);
                double band = lpHigh - lpLow;
                float s = (float)(band * env) * 0.55f * gain;
                left[idx] += s * 0.9f;
                right[idx] += s;
                send[idx] += s * 0.8f;
            }
        }

        private static void AddHatsBar(float[] left, float[] right, double barStart, double beat, SynthRng rng, float gain)
        {
            double step = beat / 4.0;
            for (int s = 0; s < 16; s++)
            {
                bool open = s == 6 || s == 14;
                int s0 = (int)((barStart + s * step) * SAMPLE_RATE);
                double decay = open ? 0.12 : 0.025;
                int n = (int)(decay * 6.0 * SAMPLE_RATE);
                double lp = 0;
                double a = 1.0 - Math.Exp(-TWO_PI * 7000.0 / SAMPLE_RATE);
                float vel = HatVelocity[s] * gain * 0.13f;
                for (int i = 0; i < n; i++)
                {
                    int idx = s0 + i;
                    if (idx >= left.Length) break;
                    double t = (double)i / SAMPLE_RATE;
                    double noise = rng.NextSigned();
                    lp += a * (noise - lp);
                    double high = noise - lp;
                    float v = (float)(high * Math.Exp(-t / decay)) * vel;
                    left[idx] += v * 0.8f;
                    right[idx] += v;
                }
            }
        }

        /// <summary>Мелодия: две расстроенные пилы через фильтр, лёгкое вибрато, мягкая атака.</summary>
        private static void AddMelody(float[] left, float[] right, float[] send, double[][] notes, double phraseStart, double beat)
        {
            for (int k = 0; k < notes.Length; k++)
            {
                double start = phraseStart + notes[k][0] * beat;
                double length = notes[k][1] * beat;
                double freq = SynthDsp.MidiToHz((int)notes[k][2]);
                int s0 = (int)(start * SAMPLE_RATE);
                int n = (int)((length + 0.25) * SAMPLE_RATE);
                double p1 = 0, p2 = 0.5, lp1 = 0, lp2 = 0;
                for (int i = 0; i < n; i++)
                {
                    int idx = s0 + i;
                    if (idx >= left.Length) break;
                    double t = (double)i / SAMPLE_RATE;
                    double vibrato = 1.0 + 0.004 * Math.Sin(TWO_PI * 5.5 * t) * Math.Min(1.0, t / 0.3);
                    double f = freq * vibrato;
                    double dt1 = f / SAMPLE_RATE, dt2 = f * 1.006 / SAMPLE_RATE;
                    p1 += dt1; if (p1 >= 1.0) p1 -= 1.0;
                    p2 += dt2; if (p2 >= 1.0) p2 -= 1.0;
                    double saw = (2.0 * p1 - 1.0 - SynthDsp.PolyBlep(p1, dt1)) + (2.0 * p2 - 1.0 - SynthDsp.PolyBlep(p2, dt2));

                    double cutoff = 1400.0 + 1800.0 * Math.Exp(-t / 0.25);
                    double a = 1.0 - Math.Exp(-TWO_PI * cutoff / SAMPLE_RATE);
                    lp1 += a * (saw - lp1);
                    lp2 += a * (lp1 - lp2);

                    double env = Math.Min(1.0, t / 0.03) * (t < length ? 1.0 : Math.Max(0.0, 1.0 - (t - length) / 0.25));
                    float s = (float)(lp2 * env) * 0.07f;
                    left[idx] += s * 0.85f;
                    right[idx] += s;
                    send[idx] += s * 0.9f;
                }
            }
        }

        // ---------------- Утилиты ----------------

        /// <summary>Огибающая сайдчейна: провал после каждой доли, активен с момента activeFrom (сек).</summary>
        private static float Duck(int sampleIndex, double beat, double activeFrom)
        {
            double t = (double)sampleIndex / SAMPLE_RATE;
            if (t < activeFrom) return 1f;
            double sinceBeat = t % beat;
            return (float)(1.0 - 0.55 * Math.Exp(-sinceBeat / 0.12));
        }
    }
}
