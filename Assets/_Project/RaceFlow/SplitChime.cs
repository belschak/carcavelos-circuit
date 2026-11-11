using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // How the checkpoint chime tells you ahead or behind without looking (audit 19). Plain C#, unit tested.
    // Ahead of your best at this checkpoint: 2 semitones higher. Behind: 2 lower. No best lap yet: normal pitch.
    // Ahead/behind uses the same rounded milliseconds as the HUD's split chip, so sound and chip always agree
    // ("+0.000" is BEHIND on the chip, so it is the low chime too).
    public static class SplitChime
    {
        public const int AheadSemitones = 2;
        public const int BehindSemitones = -2;

        public static int Semitones(bool hasSplitDelta, float deltaSeconds)
        {
            if (!hasSplitDelta)
            {
                return 0;
            }
            return RaceTimer.ToMilliseconds(deltaSeconds) < 0 ? AheadSemitones : BehindSemitones;
        }

        // A semitone is the twelfth root of 2 in frequency, so +12 semitones = double speed = one octave up.
        public static float Pitch(int semitones)
        {
            return Mathf.Pow(2f, semitones / 12f);
        }
    }
}
