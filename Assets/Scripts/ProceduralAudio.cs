using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Синтез звуковых эффектов кодом (без аудиофайлов): FM-колокольчики, фильтрованный шум,
    /// мягкие удары. Вызывается один раз при старте — здесь аллокации допустимы.
    /// Музыку синтезирует MusicSynth.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SAMPLE_RATE = 44100;

        public static AudioClip CreateSound(SoundId id)
        {
            float[] data;
            switch (id)
            {
                case SoundId.Eat:
                    data = Buffer(0.35f);
                    Bell(data, 0f, 784f, 0.3f, 0.45f, 3f);
                    Bell(data, 0.06f, 1175f, 0.25f, 0.35f, 3f);
                    break;
                case SoundId.Golden:
                    data = Buffer(0.9f);
                    float[] golden = { 880f, 1109f, 1319f, 1760f };
                    for (int i = 0; i < golden.Length; i++) Bell(data, i * 0.07f, golden[i], 0.6f, 0.3f, 3.5f);
                    break;
                case SoundId.Rotten:
                    data = Buffer(0.45f);
                    Growl(data, 0f, 140f, 70f, 0.4f, 0.45f);
                    break;
                case SoundId.Jump:
                    data = Buffer(0.25f);
                    Sweep(data, 0f, 260f, 620f, 0.2f, 0.35f);
                    Whoosh(data, 0f, 0.2f, 0.15f, 2500f);
                    break;
                case SoundId.Land:
                    data = Buffer(0.2f);
                    Thump(data, 0f, 0.5f);
                    Whoosh(data, 0f, 0.12f, 0.12f, 900f);
                    break;
                case SoundId.Death:
                    data = Buffer(1.2f);
                    Sweep(data, 0f, 330f, 55f, 1.0f, 0.4f);
                    Whoosh(data, 0f, 1.1f, 0.35f, 1200f);
                    Thump(data, 0f, 0.7f);
                    break;
                case SoundId.PowerUp:
                    data = Buffer(0.8f);
                    float[] chord = { 659f, 831f, 988f, 1319f };
                    for (int i = 0; i < chord.Length; i++) Bell(data, i * 0.045f, chord[i], 0.6f, 0.26f, 2.5f);
                    break;
                case SoundId.Shield:
                    data = Buffer(0.7f);
                    Bell(data, 0f, 523f, 0.65f, 0.45f, 6f, 3.5f);
                    break;
                case SoundId.Dash:
                    data = Buffer(0.35f);
                    Whoosh(data, 0f, 0.3f, 0.45f, 3500f);
                    break;
                case SoundId.LevelUp:
                    data = Buffer(1.2f);
                    float[] notes = { 523f, 659f, 784f, 1047f, 1319f };
                    for (int i = 0; i < notes.Length; i++) Bell(data, i * 0.09f, notes[i], 0.7f, 0.3f, 3f);
                    break;
                case SoundId.Click:
                    data = Buffer(0.06f);
                    Bell(data, 0f, 1600f, 0.05f, 0.25f, 1f);
                    break;
                case SoundId.Shrink:
                    data = Buffer(0.9f);
                    Growl(data, 0f, 90f, 60f, 0.85f, 0.4f);
                    Whoosh(data, 0f, 0.8f, 0.2f, 600f);
                    break;
                default:
                    data = Buffer(0.05f);
                    break;
            }

            AudioClip clip = AudioClip.Create(id.ToString(), data.Length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float[] Buffer(float seconds)
        {
            return new float[Mathf.Max(1, Mathf.RoundToInt(seconds * SAMPLE_RATE))];
        }

        /// <summary>FM-колокольчик: модулятор с затухающим индексом даёт «металлическую» атаку и чистый хвост.</summary>
        private static void Bell(float[] data, float start, float freq, float duration, float volume, float index, float ratio = 2f)
        {
            int s0 = Mathf.RoundToInt(start * SAMPLE_RATE);
            int n = Mathf.RoundToInt(duration * SAMPLE_RATE);
            double c = 0, m = 0;
            for (int i = 0; i < n && s0 + i < data.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                m += freq * ratio / SAMPLE_RATE;
                c += freq / (double)SAMPLE_RATE;
                float mod = index * Mathf.Exp(-t / (duration * 0.25f));
                float s = Mathf.Sin((float)(c * 2.0 * System.Math.PI) + mod * Mathf.Sin((float)(m * 2.0 * System.Math.PI)));
                float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t / (duration * 0.3f));
                data[s0 + i] += s * env * volume;
            }
        }

        /// <summary>Синусоида со сползающей высотой и мягкой огибающей.</summary>
        private static void Sweep(float[] data, float start, float fromHz, float toHz, float duration, float volume)
        {
            int s0 = Mathf.RoundToInt(start * SAMPLE_RATE);
            int n = Mathf.RoundToInt(duration * SAMPLE_RATE);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < data.Length; i++)
            {
                float k = (float)i / n;
                float freq = Mathf.Lerp(fromHz, toHz, k * (2f - k));
                phase += freq / (double)SAMPLE_RATE;
                float s = Mathf.Sin((float)(phase * 2.0 * System.Math.PI));
                s += 0.3f * Mathf.Sin((float)(phase * 4.0 * System.Math.PI));
                float env = Mathf.Min(1f, k / 0.05f) * (1f - k) * (1f - k);
                data[s0 + i] += s * env * volume;
            }
        }

        /// <summary>Шум через фильтр нижних частот — «вжух» ветра.</summary>
        private static void Whoosh(float[] data, float start, float duration, float volume, float cutoff)
        {
            int s0 = Mathf.RoundToInt(start * SAMPLE_RATE);
            int n = Mathf.RoundToInt(duration * SAMPLE_RATE);
            var rng = new System.Random(n);
            float lp = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SAMPLE_RATE);
            for (int i = 0; i < n && s0 + i < data.Length; i++)
            {
                float k = (float)i / n;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (noise - lp);
                float env = Mathf.Sin(k * Mathf.PI);
                data[s0 + i] += lp * env * volume * 2f;
            }
        }

        /// <summary>Мягкий удар: синус с быстро падающей частотой, как у бочки.</summary>
        private static void Thump(float[] data, float start, float volume)
        {
            int s0 = Mathf.RoundToInt(start * SAMPLE_RATE);
            int n = Mathf.RoundToInt(0.18f * SAMPLE_RATE);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < data.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float freq = 50f + 110f * Mathf.Exp(-t / 0.03f);
                phase += freq / SAMPLE_RATE;
                data[s0 + i] += Mathf.Sin((float)(phase * 2.0 * System.Math.PI)) * Mathf.Exp(-t / 0.06f) * volume;
            }
        }

        /// <summary>Низкое «рычание»: две расстроенные пилы через фильтр.</summary>
        private static void Growl(float[] data, float start, float fromHz, float toHz, float duration, float volume)
        {
            int s0 = Mathf.RoundToInt(start * SAMPLE_RATE);
            int n = Mathf.RoundToInt(duration * SAMPLE_RATE);
            double p1 = 0, p2 = 0;
            float lp = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * 700f / SAMPLE_RATE);
            for (int i = 0; i < n && s0 + i < data.Length; i++)
            {
                float k = (float)i / n;
                float freq = Mathf.Lerp(fromHz, toHz, k);
                p1 += freq / SAMPLE_RATE;
                p2 += freq * 1.02 / SAMPLE_RATE;
                float saw = (float)((p1 - System.Math.Floor(p1)) + (p2 - System.Math.Floor(p2)) - 1.0);
                lp += a * (saw - lp);
                float env = Mathf.Min(1f, k / 0.04f) * (1f - k);
                data[s0 + i] += lp * env * volume;
            }
        }
    }
}
