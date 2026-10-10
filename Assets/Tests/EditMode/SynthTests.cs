using NUnit.Framework;
using Snake3D;

/// <summary>Синтез звуков и музыки: всё рендерится, без NaN/бесконечностей и без перегрузки.</summary>
public class SynthTests
{
    [Test]
    public void Sfx_AllSoundsRendered_StereoFiniteAndNotClipping()
    {
        float[][] sounds = SfxSynth.RenderAll();
        Assert.AreEqual(SfxSynth.SOUND_COUNT, sounds.Length);
        Assert.AreEqual(System.Enum.GetValues(typeof(SoundId)).Length, SfxSynth.SOUND_COUNT, "SOUND_COUNT должен совпадать с SoundId");

        for (int i = 0; i < sounds.Length; i++)
        {
            float[] data = sounds[i];
            Assert.IsNotNull(data, ((SoundId)i).ToString());
            Assert.Greater(data.Length, 100, ((SoundId)i).ToString());
            Assert.AreEqual(0, data.Length % 2, "стерео — чётное число сэмплов");

            float peak = 0f;
            foreach (float s in data)
            {
                Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), ((SoundId)i).ToString());
                peak = System.Math.Max(peak, System.Math.Abs(s));
            }
            Assert.Greater(peak, 0.05f, ((SoundId)i) + " не должен быть тишиной");
            Assert.LessOrEqual(peak, 1f, ((SoundId)i) + " не должен перегружаться");
        }
    }

    [Test]
    public void Music_LoopRendered_StereoFiniteAndNotClipping()
    {
        float[] data = MusicSynth.RenderLoop(out int frames);
        Assert.Greater(frames, MusicSynth.SAMPLE_RATE * 10, "петля длиннее 10 секунд");
        Assert.AreEqual(frames * 2, data.Length);

        float peak = 0f;
        for (int i = 0; i < data.Length; i += 7)
        {
            Assert.IsFalse(float.IsNaN(data[i]) || float.IsInfinity(data[i]));
            peak = System.Math.Max(peak, System.Math.Abs(data[i]));
        }
        Assert.Greater(peak, 0.1f);
        Assert.LessOrEqual(peak, 1f);
    }

    [Test]
    public void Ambience_Loops_HaveLengthAndNoClipping()
    {
        float[][] loops =
        {
            AmbientSynth.Rain(out int rain), AmbientSynth.Birds(out int birds),
            AmbientSynth.Crickets(out int crickets), AmbientSynth.Rustle(out int rustle)
        };
        int[] frames = { rain, birds, crickets, rustle };
        for (int i = 0; i < loops.Length; i++)
        {
            Assert.AreEqual(frames[i] * 2, loops[i].Length, "Петля " + i + ": стерео-данные не той длины");
            Assert.Greater(frames[i], AmbientSynth.SAMPLE_RATE, "Петля " + i + " слишком короткая");
            float peak = 0f;
            foreach (float s in loops[i])
            {
                Assert.IsFalse(float.IsNaN(s), "NaN в петле " + i);
                peak = System.Math.Max(peak, System.Math.Abs(s));
            }
            Assert.Greater(peak, 0.01f, "Петля " + i + " беззвучна");
            Assert.LessOrEqual(peak, 1f, "Петля " + i + " перегружена");
        }
    }
}
