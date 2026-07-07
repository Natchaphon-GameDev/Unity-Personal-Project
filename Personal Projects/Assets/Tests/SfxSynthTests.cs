using NUnit.Framework;
using UnityEngine;
using TaskbarHero;

namespace TaskbarHero.Tests
{
    /// <summary>Edit-mode tests for the procedural SFX waveform shapes.</summary>
    public class SfxSynthTests
    {
        const int Sr = 44100;

        [Test]
        public void Blip_HasSampleCountForDuration()
        {
            var buf = SfxSynth.Blip(880f, 0.05f, sampleRate: Sr);
            Assert.AreEqual((int)(0.05f * Sr), buf.Length);
        }

        [Test]
        public void Blip_SamplesAreFiniteAndInRange()
        {
            var buf = SfxSynth.Blip(880f, 0.05f, amplitude: 0.6f, sampleRate: Sr);
            foreach (float s in buf)
            {
                Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s));
                Assert.LessOrEqual(Mathf.Abs(s), 1f);
            }
        }

        [Test]
        public void Blip_EnvelopeDecays()
        {
            var buf = SfxSynth.Blip(880f, 0.2f, amplitude: 0.6f, decay: 20f, sampleRate: Sr);
            float earlyPeak = PeakInWindow(buf, 0, buf.Length / 10);
            float latePeak = PeakInWindow(buf, buf.Length * 9 / 10, buf.Length);
            Assert.Less(latePeak, earlyPeak);
            Assert.Less(latePeak, 0.1f); // near silence by the tail
        }

        [Test]
        public void Chirp_HasSampleCountForDuration()
        {
            var buf = SfxSynth.Chirp(700f, 200f, 0.14f, sampleRate: Sr);
            Assert.AreEqual((int)(0.14f * Sr), buf.Length);
        }

        [Test]
        public void Arpeggio_LengthIsSumOfNotes()
        {
            var buf = SfxSynth.Arpeggio(new[] { 523f, 659f, 784f }, 0.06f, sampleRate: Sr);
            // Compare to a real single-note length so both sides share the same runtime
            // float rounding (a recomputed constant can fold to a different integer).
            int perNote = SfxSynth.Blip(523f, 0.06f, sampleRate: Sr).Length;
            Assert.AreEqual(perNote * 3, buf.Length);
        }

        static float PeakInWindow(float[] buf, int start, int end)
        {
            float peak = 0f;
            for (int i = start; i < end && i < buf.Length; i++)
            {
                float a = Mathf.Abs(buf[i]);
                if (a > peak) peak = a;
            }
            return peak;
        }
    }
}
