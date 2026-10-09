using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Синтез звуков и музыки кодом (без аудиофайлов). Вызывается один раз при старте:
    /// здесь аллокации допустимы, в игровом цикле — нет.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SAMPLE_RATE = 44100;

        private enum Wave
        {
            Sine,
            Square,
            Triangle,
            Noise
        }

        public static AudioClip CreateSound(SoundId id)
        {
            switch (id)
            {
                case SoundId.Eat: return Sweep("Eat", Wave.Square, 520f, 880f, 0.09f, 0.35f);
                case SoundId.Golden: return Arpeggio("Golden", Wave.Square, new[] { 660f, 880f, 1100f, 1320f }, 0.06f, 0.3f);
                case SoundId.Rotten: return Sweep("Rotten", Wave.Square, 220f, 90f, 0.25f, 0.35f);
                case SoundId.Jump: return Sweep("Jump", Wave.Triangle, 300f, 700f, 0.14f, 0.5f);
                case SoundId.Land: return Sweep("Land", Wave.Sine, 140f, 60f, 0.1f, 0.6f, 0.4f);
                case SoundId.Death: return Sweep("Death", Wave.Square, 400f, 50f, 0.7f, 0.4f, 0.5f);
                case SoundId.PowerUp: return Arpeggio("PowerUp", Wave.Triangle, new[] { 523f, 659f, 784f, 1046f }, 0.07f, 0.45f);
                case SoundId.Shield: return Sweep("Shield", Wave.Triangle, 900f, 300f, 0.3f, 0.5f, 0.2f);
                case SoundId.Dash: return Sweep("Dash", Wave.Noise, 0f, 0f, 0.22f, 0.35f);
                case SoundId.LevelUp: return Arpeggio("LevelUp", Wave.Square, new[] { 523f, 659f, 784f, 659f, 784f, 1046f }, 0.08f, 0.3f);
                case SoundId.Click: return Sweep("Click", Wave.Sine, 1200f, 900f, 0.03f, 0.4f);
                case SoundId.Shrink: return Sweep("Shrink", Wave.Square, 160f, 120f, 0.45f, 0.3f, 0.3f);
                default: return Sweep("Unknown", Wave.Sine, 440f, 440f, 0.05f, 0.2f);
            }
        }

        /// <summary>
        /// Фоновая петля: 8 тактов, 120 BPM, Am–F–C–G. Бас, арпеджио и хай-хэт.
        /// Темп поднимается через pitch источника, поэтому петля одна.
        /// </summary>
        public static AudioClip CreateMusicLoop()
        {
            const float bpm = 120f;
            const int bars = 8;
            float beat = 60f / bpm;
            int length = Mathf.RoundToInt(bars * 4 * beat * SAMPLE_RATE);
            var data = new float[length];

            // Корни аккордов (Гц) и трезвучия для арпеджио.
            float[] roots = { 110f, 87.31f, 130.81f, 98f };
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f },
                new[] { 174.61f, 220f, 261.63f },
                new[] { 261.63f, 329.63f, 392f },
                new[] { 196f, 246.94f, 293.66f }
            };

            var rng = new System.Random(7);
            int sixteenth = Mathf.RoundToInt(beat * 0.25f * SAMPLE_RATE);
            for (int step = 0; step < bars * 16; step++)
            {
                int bar = step / 16;
                int chord = (bar / 2) % 4;
                int start = step * sixteenth;

                // Бас на каждую четверть.
                if (step % 4 == 0) AddNote(data, start, sixteenth * 3, roots[chord], Wave.Triangle, 0.32f, rng);
                // Арпеджио восьмыми.
                if (step % 2 == 0)
                {
                    float[] c = chords[chord];
                    float f = c[(step / 2) % 3] * ((step / 6) % 2 == 0 ? 2f : 1f);
                    AddNote(data, start, sixteenth * 2, f, Wave.Square, 0.07f, rng);
                }
                // Хай-хэт на слабые доли.
                if (step % 4 == 2) AddNote(data, start, sixteenth / 2, 0f, Wave.Noise, 0.05f, rng);
            }

            AudioClip clip = AudioClip.Create("MusicLoop", length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Sweep(string name, Wave wave, float fromHz, float toHz, float duration, float volume, float noiseMix = 0f)
        {
            int length = Mathf.Max(1, Mathf.RoundToInt(duration * SAMPLE_RATE));
            var data = new float[length];
            var rng = new System.Random(name.GetHashCode());
            double phase = 0;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float freq = Mathf.Lerp(fromHz, toHz, t);
                phase += freq / SAMPLE_RATE;
                float s = Sample(wave, phase, rng);
                if (noiseMix > 0f) s = Mathf.Lerp(s, (float)(rng.NextDouble() * 2.0 - 1.0), noiseMix);
                data[i] = s * volume * Envelope(t);
            }

            AudioClip clip = AudioClip.Create(name, length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arpeggio(string name, Wave wave, float[] notes, float noteDuration, float volume)
        {
            int noteLength = Mathf.RoundToInt(noteDuration * SAMPLE_RATE);
            var data = new float[noteLength * notes.Length];
            var rng = new System.Random(name.GetHashCode());
            for (int n = 0; n < notes.Length; n++)
                AddNote(data, n * noteLength, noteLength, notes[n], wave, volume, rng);

            AudioClip clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void AddNote(float[] data, int start, int length, float freq, Wave wave, float volume, System.Random rng)
        {
            double phase = 0;
            for (int i = 0; i < length && start + i < data.Length; i++)
            {
                float t = (float)i / length;
                phase += freq / SAMPLE_RATE;
                data[start + i] += Sample(wave, phase, rng) * volume * Envelope(t);
            }
        }

        /// <summary>Быстрая атака и плавное затухание — без щелчков на краях.</summary>
        private static float Envelope(float t)
        {
            float attack = Mathf.Clamp01(t / 0.05f);
            float release = 1f - t;
            return attack * release * release;
        }

        private static float Sample(Wave wave, double phase, System.Random rng)
        {
            double p = phase - System.Math.Floor(phase);
            switch (wave)
            {
                case Wave.Square: return p < 0.5 ? 1f : -1f;
                case Wave.Triangle: return (float)(p < 0.5 ? p * 4.0 - 1.0 : 3.0 - p * 4.0);
                case Wave.Noise: return (float)(rng.NextDouble() * 2.0 - 1.0);
                default: return Mathf.Sin((float)(p * 2.0 * System.Math.PI));
            }
        }
    }
}
