namespace CarcavelosCircuit.Audio
{
    // The maths behind the drift sounds, as plain C# so it can be unit tested.
    // Works for any number of drift stages: every new stage sounds a little higher and the sparks get louder.
    // Numbers from Luc's audio handoff (section 3).
    public static class DriftSound
    {
        // Stage-up chime: stage 1 = normal pitch, every further stage 12 % higher.
        public static float StageUpPitch(int stage)
        {
            if (stage < 1) stage = 1;
            return 1f + 0.12f * (stage - 1);
        }

        // Sparks loop: silent without sparks (stage 0), then louder with every stage, never above full volume.
        public static float SparksVolume(int stage)
        {
            if (stage < 1) return 0f;
            float volume = 0.55f + 0.15f * stage;
            return volume > 1f ? 1f : volume;
        }

        public static float SparksPitch(int stage)
        {
            if (stage < 1) stage = 1;
            return 0.95f + 0.07f * stage;
        }
    }
}
