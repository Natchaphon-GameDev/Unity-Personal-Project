using System;
using System.Collections.Generic;

namespace TaskbarHero
{
    /// <summary>
    /// Generates short sound-effect waveforms as float samples (mono, [-1, 1]) with an
    /// exponential decay envelope. Pure and Unity-free so the shapes are unit-testable;
    /// <see cref="SfxPlayer"/> wraps the arrays in AudioClips. Keeping SFX synthesized
    /// means the project ships no audio assets.
    /// </summary>
    public static class SfxSynth
    {
        public const int DefaultSampleRate = 44100;

        /// <summary>A single decaying tone.</summary>
        public static float[] Blip(float freqHz, float seconds, float amplitude = 0.6f, float decay = 12f, int sampleRate = DefaultSampleRate)
            => Sweep(freqHz, freqHz, seconds, amplitude, decay, sampleRate);

        /// <summary>A tone that glides from one frequency to another (rising or falling).</summary>
        public static float[] Chirp(float fromHz, float toHz, float seconds, float amplitude = 0.6f, float decay = 8f, int sampleRate = DefaultSampleRate)
            => Sweep(fromHz, toHz, seconds, amplitude, decay, sampleRate);

        /// <summary>A run of blips at the given frequencies, played back to back.</summary>
        public static float[] Arpeggio(float[] freqs, float noteSeconds, float amplitude = 0.5f, float decay = 10f, int sampleRate = DefaultSampleRate)
        {
            var samples = new List<float>();
            foreach (float f in freqs)
                samples.AddRange(Blip(f, noteSeconds, amplitude, decay, sampleRate));
            return samples.ToArray();
        }

        static float[] Sweep(float fromHz, float toHz, float seconds, float amplitude, float decay, int sampleRate)
        {
            int n = Math.Max(1, (int)(seconds * sampleRate));
            var buffer = new float[n];
            double dt = 1.0 / sampleRate;
            double phase = 0.0;

            for (int i = 0; i < n; i++)
            {
                double frac = n <= 1 ? 0.0 : (double)i / (n - 1);
                double freq = fromHz + (toHz - fromHz) * frac;
                phase += 2.0 * Math.PI * freq * dt;
                double envelope = Math.Exp(-decay * (i * dt));
                double sample = Math.Sin(phase) * envelope * amplitude;
                buffer[i] = (float)Math.Max(-1.0, Math.Min(1.0, sample));
            }

            return buffer;
        }
    }
}
