using System;

namespace WildSnake
{
    /// <summary>
    /// Звуковые эффекты в стиле саундтрека: те же расстроенные пилы с фильтром, FM-колокольчики,
    /// фильтрованный шум и саб-удары, стерео и общий реверб; ноты — в ля миноре, как музыка.
    /// Природные звуки (хруст добычи, крик орла, шипение языка) собраны из тех же блоков.
    /// Только System.Math — рендерится в фоновом потоке вместе с музыкой.
    /// </summary>
    public static class SfxSynth
    {
        public const int SOUND_COUNT = 16;
        private const int SR = SynthDsp.SAMPLE_RATE;

        /// <summary>Все звуки по индексу SoundId; каждый — чередующиеся стерео-сэмплы.</summary>
        public static float[][] RenderAll()
        {
            var result = new float[SOUND_COUNT][];
            for (int i = 0; i < SOUND_COUNT; i++) result[i] = Render((SoundId)i);
            return result;
        }

        public static float[] Render(SoundId id)
        {
            Track t;
            switch (id)
            {
                case SoundId.Catch:
                    // Сухой хруст добычи: короткий щелчок шума, низкий «глоток» и колокольчик вверх.
                    t = new Track(0.8f, 11);
                    t.Noise(0.0, 0.05, 7000, 2500, 1200, -0.2f, 0.2f, 0.001, 0.5f, 0.1f);
                    t.Sine(0.02, 0.12, 260, 110, 0.003, 0.45f, 0f, 0f);
                    t.Pluck(0.05, 81, 0.35, 0.45f, -0.25f, 0.4f);
                    t.Pluck(0.1, 88, 0.45, 0.4f, 0.25f, 0.5f);
                    return t.Finish(0.3f, 0.55f);

                case SoundId.Feast:
                    // Крупная добыча: хруст, тяжёлый глоток и тёплое арпеджио Am9.
                    t = new Track(1.6f, 12);
                    t.Noise(0.0, 0.08, 6000, 1800, 600, -0.3f, 0.3f, 0.001, 0.6f, 0.15f);
                    t.Sub(0.03, 0.4, 160, 55, 0.6f);
                    t.Sine(0.05, 0.25, 320, 90, 0.005, 0.35f, 0f, 0.1f);
                    int[] feast = { 69, 76, 81, 84, 88 };
                    for (int i = 0; i < feast.Length; i++) t.Pluck(0.12 + i * 0.055, feast[i], 0.55, 0.42f, i % 2 == 0 ? -0.35f : 0.35f, 0.6f, 3.0, 2.0);
                    return t.Finish(0.4f, 0.7f);

                case SoundId.Hurt:
                    // Удар: глухой бум, рваная пила вниз и шумовой хлопок.
                    t = new Track(0.9f, 13);
                    t.Sub(0.0, 0.35, 140, 40, 0.9f);
                    t.SuperSaw(0.0, 0.4, 50, 38, 4, 35, 2200, 160, 0.003, 0.18, 0.45f, 0.2f);
                    t.Noise(0.0, 0.18, 3000, 400, 100, 0f, 0f, 0.001, 0.45f, 0.2f);
                    return t.Finish(0.2f, 0.75f);

                case SoundId.Jump:
                    // Восходящий «вжух»: шум открывается вверх, под ним тон.
                    t = new Track(0.55f, 14);
                    t.Noise(0.0, 0.26, 500, 5500, 300, 0f, 0f, 0.12, 0.35f, 0.3f);
                    t.Sine(0.0, 0.22, 300, 760, 0.01, 0.22f, 0f, 0.2f);
                    return t.Finish(0.2f, 0.5f);

                case SoundId.Land:
                    t = new Track(0.5f, 15);
                    t.Sub(0.0, 0.3, 130, 45, 0.85f);
                    t.Noise(0.0, 0.2, 900, 300, 80, 0f, 0f, 0.003, 0.3f, 0.1f);
                    return t.Finish(0.1f, 0.6f);

                case SoundId.Death:
                    // Даунлифтер: аккорд Am съезжает на октаву вниз, фильтр закрывается, гул и бум.
                    t = new Track(2.6f, 16);
                    foreach (int n in new[] { 57, 60, 64 }) t.SuperSaw(0.0, 1.6, n, n - 12, 5, 25, 4000, 150, 0.005, 0.9, 0.22f, 0.6f);
                    t.Noise(0.0, 1.4, 6000, 300, 100, -0.5f, 0.5f, 0.01, 0.25f, 0.5f);
                    t.Sub(0.0, 0.7, 85, 30, 0.9f);
                    return t.Finish(0.45f, 0.85f);

                case SoundId.EagleCry:
                    // Крик хищной птицы «кии-ии-ар»: три нисходящих свиста с обертоном и хрипом, с эхом.
                    t = new Track(2.2f, 17);
                    double[] cry = { 0.0, 0.42, 0.78 };
                    double[] len = { 0.36, 0.3, 0.55 };
                    for (int i = 0; i < cry.Length; i++)
                    {
                        float pan = i == 1 ? 0.35f : -0.25f;
                        t.Sine(cry[i], len[i], 2300 - i * 150, 1500 - i * 200, 0.02, 0.32f, pan, 0.7f);
                        t.Sine(cry[i], len[i], 4600 - i * 300, 3000 - i * 400, 0.02, 0.1f, pan, 0.7f);
                        t.Noise(cry[i], len[i], 5200, 3200, 2400, pan, pan, 0.02, 0.18f, 0.7f);
                    }
                    return t.Finish(0.6f, 0.6f);

                case SoundId.EagleStrike:
                    // Пике: рёв воздуха сверху вниз, затем тяжёлый удар когтей.
                    t = new Track(1.4f, 18);
                    t.Noise(0.0, 0.7, 8000, 900, 300, -0.6f, 0.6f, 0.4, 0.55f, 0.3f);
                    t.Sub(0.62, 0.45, 180, 38, 0.95f);
                    t.Noise(0.62, 0.2, 4000, 500, 150, 0f, 0f, 0.001, 0.5f, 0.3f);
                    return t.Finish(0.35f, 0.8f);

                case SoundId.Dash:
                    // Бросок: шум с открывающимся фильтром и пила «вверх» пролетают слева направо.
                    t = new Track(0.8f, 19);
                    t.Noise(0.0, 0.42, 900, 7000, 400, -0.8f, 0.8f, 0.15, 0.55f, 0.4f);
                    t.SuperSaw(0.0, 0.3, 57, 69, 3, 30, 300, 3000, 0.05, 0.15, 0.12f, 0.3f);
                    return t.Finish(0.25f, 0.6f);

                case SoundId.Molt:
                    // Линька: шелест старой кожи, светлый пад и взлетающее арпеджио — «перерождение».
                    t = new Track(2.6f, 20);
                    t.Noise(0.0, 0.9, 3000, 9000, 1500, -0.7f, 0.7f, 0.3, 0.25f, 0.5f);
                    foreach (int n in new[] { 57, 64, 67, 71 }) t.SuperSaw(0.1, 1.4, n, n, 5, 18, 700, 4500, 0.4, 0.6, 0.12f, 0.6f);
                    int[] arp = { 69, 72, 76, 79, 81, 84, 88, 91 };
                    for (int i = 0; i < arp.Length; i++) t.Pluck(0.35 + i * 0.07, arp[i], 0.55, 0.38f, i % 2 == 0 ? -0.35f : 0.35f, 0.65f);
                    return t.Finish(0.5f, 0.75f);

                case SoundId.Click:
                    t = new Track(0.25f, 21);
                    t.Pluck(0.0, 96, 0.08, 0.5f, 0f, 0.15f, 2.0, 1.5);
                    t.Noise(0.0, 0.015, 9000, 9000, 3000, 0f, 0f, 0.0005, 0.2f, 0f);
                    return t.Finish(0.1f, 0.3f);

                case SoundId.Sniff:
                    // Язык: два коротких шипения «тс-тс» и тихий высокий отклик.
                    t = new Track(0.9f, 22);
                    t.Noise(0.0, 0.07, 9000, 6000, 3500, -0.3f, -0.1f, 0.005, 0.35f, 0.2f);
                    t.Noise(0.11, 0.07, 9000, 6000, 3500, 0.1f, 0.3f, 0.005, 0.35f, 0.2f);
                    t.Pluck(0.2, 93, 0.5, 0.25f, 0f, 0.6f, 3.0, 1.5);
                    return t.Finish(0.45f, 0.45f);

                case SoundId.Dawn:
                    // Рассвет: медленно раскрывающийся мажорный пад и колокольчики.
                    t = new Track(3.2f, 23);
                    foreach (int n in new[] { 60, 64, 67, 72 }) t.SuperSaw(0.0, 2.6, n, n, 5, 15, 400, 3500, 0.9, 1.0, 0.13f, 0.6f);
                    int[] dawn = { 79, 84, 88 };
                    for (int i = 0; i < dawn.Length; i++) t.Pluck(0.8 + i * 0.25, dawn[i], 0.9, 0.3f, i % 2 == 0 ? -0.4f : 0.4f, 0.7f, 3.0, 1.5);
                    return t.Finish(0.55f, 0.6f);

                case SoundId.Dusk:
                    // Закат: минорный пад тонет вниз, фильтр закрывается, низкий гул.
                    t = new Track(3.2f, 24);
                    foreach (int n in new[] { 57, 60, 64 }) t.SuperSaw(0.0, 2.6, n, n - 2, 5, 20, 2500, 300, 0.6, 1.2, 0.15f, 0.6f);
                    t.Sub(0.0, 2.0, 55, 45, 0.35f);
                    t.Pluck(0.4, 76, 1.2, 0.25f, 0.3f, 0.7f, 3.5, 2.0);
                    return t.Finish(0.55f, 0.6f);

                case SoundId.FoxYip:
                    // Лисий лай «вяу-вяу»: два хриплых тявка с подъёмом и спадом тона.
                    t = new Track(1.2f, 25);
                    for (int i = 0; i < 2; i++)
                    {
                        double at = i * 0.26;
                        t.SuperSaw(at, 0.16, 74 - i * 2, 66 - i * 2, 3, 20, 2600, 900, 0.01, 0.08, 0.2f, 0.4f);
                        t.Noise(at, 0.14, 3500, 1500, 900, -0.2f, 0.2f, 0.005, 0.2f, 0.4f);
                    }
                    return t.Finish(0.4f, 0.55f);

                case SoundId.Goal:
                    // Цель суток: короткая восходящая квинта колокольчиков.
                    t = new Track(1.4f, 26);
                    int[] goal = { 76, 83, 88 };
                    for (int i = 0; i < goal.Length; i++) t.Pluck(i * 0.09, goal[i], 0.7, 0.4f, i == 1 ? 0.3f : -0.3f, 0.6f, 3.0, 1.8);
                    return t.Finish(0.45f, 0.6f);

                default:
                    return new float[2];
            }
        }

        /// <summary>Стерео-дорожка одного звука с посылом на реверб.</summary>
        private sealed class Track
        {
            private readonly float[] _left;
            private readonly float[] _right;
            private readonly float[] _send;
            private readonly SynthRng _rng;

            public Track(float seconds, uint seed)
            {
                int n = (int)(seconds * SR);
                _left = new float[n];
                _right = new float[n];
                _send = new float[n];
                _rng = new SynthRng(seed);
            }

            /// <summary>
            /// Суперпила: voices расстроенных пил, глайд высоты midiFrom→midiTo, фильтр cutFrom→cutTo
            /// (экспоненциально), огибающая: атака и экспоненциальный спад.
            /// </summary>
            public void SuperSaw(double start, double duration, double midiFrom, double midiTo, int voices, double detuneCents,
                double cutFrom, double cutTo, double attack, double decay, float gain, float send)
            {
                int s0 = (int)(start * SR);
                int n = (int)(duration * SR);
                for (int v = 0; v < voices; v++)
                {
                    double spread = voices > 1 ? (double)v / (voices - 1) * 2.0 - 1.0 : 0.0;
                    double detune = spread * detuneCents / 100.0;
                    float pan = (float)spread * 0.7f;
                    float pl = (1f - pan) * 0.5f, pr = (1f + pan) * 0.5f;
                    double phase = _rng.Next01();
                    double lp1 = 0, lp2 = 0, a = 0;

                    for (int i = 0; i < n; i++)
                    {
                        int idx = s0 + i;
                        if (idx >= _left.Length) break;
                        double t = (double)i / SR;
                        double k = t / duration;
                        double ease = k * (2.0 - k);
                        double freq = SynthDsp.MidiToHz(midiFrom + (midiTo - midiFrom) * ease + detune);
                        double dt = freq / SR;
                        phase += dt;
                        if (phase >= 1.0) phase -= 1.0;
                        double saw = 2.0 * phase - 1.0 - SynthDsp.PolyBlep(phase, dt);

                        if ((i & 15) == 0) a = SynthDsp.LowpassCoef(cutFrom * Math.Pow(cutTo / cutFrom, k));
                        lp1 += a * (saw - lp1);
                        lp2 += a * (lp1 - lp2);

                        double env = Math.Min(1.0, t / attack) * Math.Exp(-Math.Max(0.0, t - attack) / decay) * FadeOut(i, n);
                        float s = (float)(lp2 * env) * gain / voices * 2f;
                        _left[idx] += s * pl;
                        _right[idx] += s * pr;
                        _send[idx] += s * send;
                    }
                }
            }

            /// <summary>FM-колокольчик: модулятор с затухающим индексом — яркая атака и чистый хвост.</summary>
            public void Pluck(double start, double midi, double duration, float gain, float pan, float send, double ratio = 2.0, double index = 2.5)
            {
                int s0 = (int)(start * SR);
                int n = (int)(duration * SR);
                double freq = SynthDsp.MidiToHz(midi);
                float pl = (1f - pan) * 0.5f, pr = (1f + pan) * 0.5f;
                double c = 0, m = 0;
                for (int i = 0; i < n; i++)
                {
                    int idx = s0 + i;
                    if (idx >= _left.Length) break;
                    double t = (double)i / SR;
                    m += freq * ratio / SR;
                    c += freq / SR;
                    double mod = index * Math.Exp(-t / (duration * 0.25));
                    double s = Math.Sin(SynthDsp.TWO_PI * c + mod * Math.Sin(SynthDsp.TWO_PI * m));
                    double env = Math.Min(1.0, t / 0.002) * Math.Exp(-t / (duration * 0.35)) * FadeOut(i, n);
                    float v = (float)(s * env) * gain;
                    _left[idx] += v * pl;
                    _right[idx] += v * pr;
                    _send[idx] += v * send;
                }
            }

            /// <summary>Полосовой шум (разность двух фильтров), срез cutFrom→cutTo, панорама panFrom→panTo.</summary>
            public void Noise(double start, double duration, double cutFrom, double cutTo, double highpass, float panFrom, float panTo,
                double attack, float gain, float send)
            {
                int s0 = (int)(start * SR);
                int n = (int)(duration * SR);
                double lp = 0, hp = 0, a = 0;
                double aHp = SynthDsp.LowpassCoef(highpass);
                for (int i = 0; i < n; i++)
                {
                    int idx = s0 + i;
                    if (idx >= _left.Length) break;
                    double t = (double)i / SR;
                    double k = t / duration;
                    if ((i & 15) == 0) a = SynthDsp.LowpassCoef(cutFrom * Math.Pow(cutTo / cutFrom, k));
                    double x = _rng.NextSigned();
                    lp += a * (x - lp);
                    hp += aHp * (lp - hp);
                    double band = lp - hp;

                    double env = Math.Min(1.0, t / attack) * (1.0 - k) * (1.0 - k) * FadeOut(i, n);
                    float pan = panFrom + (panTo - panFrom) * (float)k;
                    float v = (float)(band * env) * gain * 2f;
                    _left[idx] += v * (1f - pan) * 0.5f;
                    _right[idx] += v * (1f + pan) * 0.5f;
                    _send[idx] += v * send;
                }
            }

            /// <summary>Саб-удар: синус с быстро падающей частотой, как у бочки.</summary>
            public void Sub(double start, double duration, double fFrom, double fTo, float gain)
            {
                int s0 = (int)(start * SR);
                int n = (int)(duration * SR);
                double phase = 0;
                for (int i = 0; i < n; i++)
                {
                    int idx = s0 + i;
                    if (idx >= _left.Length) break;
                    double t = (double)i / SR;
                    double freq = fTo + (fFrom - fTo) * Math.Exp(-t / (duration * 0.15));
                    phase += freq / SR;
                    double env = Math.Min(1.0, t / 0.001) * Math.Exp(-t / (duration * 0.45)) * FadeOut(i, n);
                    float v = (float)(Math.Sin(SynthDsp.TWO_PI * phase) * env) * gain;
                    _left[idx] += v;
                    _right[idx] += v;
                }
            }

            /// <summary>Синус с глайдом частоты (Гц) и плавной огибающей.</summary>
            public void Sine(double start, double duration, double fFrom, double fTo, double attack, float gain, float pan, float send)
            {
                int s0 = (int)(start * SR);
                int n = (int)(duration * SR);
                float pl = (1f - pan) * 0.5f, pr = (1f + pan) * 0.5f;
                double phase = 0;
                for (int i = 0; i < n; i++)
                {
                    int idx = s0 + i;
                    if (idx >= _left.Length) break;
                    double t = (double)i / SR;
                    double k = t / duration;
                    double freq = fFrom + (fTo - fFrom) * k * (2.0 - k);
                    phase += freq / SR;
                    double env = Math.Min(1.0, t / attack) * (1.0 - k) * (1.0 - k);
                    float v = (float)(Math.Sin(SynthDsp.TWO_PI * phase) * env) * gain;
                    _left[idx] += v * pl;
                    _right[idx] += v * pr;
                    _send[idx] += v * send;
                }
            }

            /// <summary>Реверб, мягкое ограничение, нормализация пика и чередование каналов.</summary>
            public float[] Finish(float wet, float peak)
            {
                SynthDsp.ApplyReverb(_send, _left, _right, wet, 0.8f);

                float max = 0f;
                for (int i = 0; i < _left.Length; i++)
                {
                    _left[i] = SynthDsp.SoftClip(_left[i]);
                    _right[i] = SynthDsp.SoftClip(_right[i]);
                    max = Math.Max(max, Math.Max(Math.Abs(_left[i]), Math.Abs(_right[i])));
                }

                float gain = max > 0f ? peak / max : 1f;
                int n = _left.Length;
                var data = new float[n * 2];
                for (int i = 0; i < n; i++)
                {
                    // Короткое затухание в конце, чтобы хвост реверба не обрывался щелчком.
                    float fade = i > n - 2205 ? (n - i) / 2205f : 1f;
                    data[i * 2] = _left[i] * gain * fade;
                    data[i * 2 + 1] = _right[i] * gain * fade;
                }
                return data;
            }

            /// <summary>Последние 5 мс ноты плавно гасим — без щелчков.</summary>
            private static double FadeOut(int i, int n)
            {
                int left = n - i;
                return left < 220 ? left / 220.0 : 1.0;
            }
        }
    }
}
