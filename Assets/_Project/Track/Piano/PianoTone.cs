using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // Plain C# (Edit Mode tested, task T10): makes piano-like notes as sample arrays, so the atrium piano needs no
    // recorded sound file (own work, no licence question). A note = a few harmonics that fade out, the high ones faster,
    // with a very short attack so it sounds struck, not switched on.
    public static class PianoTone
    {
        public const int SampleRate = 44100;
        private const float AttackSeconds = 0.004f; // a few milliseconds of fade-in, so there is no click
        private const float Peak = 0.8f;            // loudest sample after mixing (headroom below 1.0)

        // Frequency of a key: 0 = A4 (440 Hz), +12 = one octave up, -9 = middle C.
        public static float Frequency(int semitonesFromA4)
        {
            return 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);
        }

        // Several notes, each starting at its own time (seconds), mixed into one sound of `seconds` length.
        // Empty or broken input gives silence, never an error.
        public static float[] Notes(int[] semitonesFromA4, float[] startSeconds, float seconds)
        {
            int length = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0f, seconds) * SampleRate));
            float[] samples = new float[length];
            if (semitonesFromA4 == null || startSeconds == null)
            {
                return samples;
            }
            int count = Mathf.Min(semitonesFromA4.Length, startSeconds.Length);
            for (int n = 0; n < count; n++)
            {
                AddNote(samples, Frequency(semitonesFromA4[n]), startSeconds[n]);
            }
            Normalise(samples);
            return samples;
        }

        // Adds one struck note from `start` seconds on (four harmonics, each fading out on its own).
        private static void AddNote(float[] samples, float frequency, float start)
        {
            if (float.IsNaN(start) || start < 0f)
            {
                start = 0f;
            }
            int first = Mathf.RoundToInt(start * SampleRate);
            float[] strength = { 1f, 0.5f, 0.25f, 0.12f };
            float[] fadePerSecond = { 2.2f, 3.5f, 5f, 7f }; // higher harmonics die away faster, like a real string
            for (int i = first; i < samples.Length; i++)
            {
                float t = (i - first) / (float)SampleRate;
                float attack = Mathf.Clamp01(t / AttackSeconds);
                float value = 0f;
                for (int h = 0; h < strength.Length; h++)
                {
                    value += strength[h] * Mathf.Exp(-fadePerSecond[h] * t) * Mathf.Sin(2f * Mathf.PI * frequency * (h + 1) * t);
                }
                samples[i] += attack * value;
            }
        }

        // Scales everything so the loudest sample is exactly Peak (silence stays silence).
        private static void Normalise(float[] samples)
        {
            float loudest = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(samples[i]));
            }
            if (loudest <= 0f)
            {
                return;
            }
            float scale = Peak / loudest;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }
    }
}
