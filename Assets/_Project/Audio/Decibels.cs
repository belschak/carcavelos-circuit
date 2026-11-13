namespace CarcavelosCircuit.Audio
{
    // Mixing is easiest in decibels (dB): -6 dB is about half the volume, 0 dB = unchanged, -80 dB = off.
    // AudioSource.volume wants a plain factor (0..1), so this converts. Plain C#, unit tested.
    public static class Decibels
    {
        // At or below this the sound counts as off (factor exactly 0).
        public const float Silent = -80f;

        public static float ToVolume(float db)
        {
            if (db <= Silent)
            {
                return 0f;
            }
            return (float)System.Math.Pow(10.0, db / 20.0);
        }
    }
}
