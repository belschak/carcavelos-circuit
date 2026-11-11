namespace CarcavelosCircuit.Ambience
{
    // One wave at one moment, as WaveSchedule sees it. Plain data, no Unity objects.
    public struct WaveState
    {
        public int Id;              // running number of the wave (the same wave keeps its number while it rolls in)
        public float Age;           // seconds since it appeared out at sea
        public float Distance;      // metres from the waterline to the crest (0 = reaches the sand, never below 0)
        public float Swash;         // 0 while rolling in, then 0..1 while the foam runs up the sand and fades
        public float HeightScale;   // 0.6..1.2: bigger and smaller waves in a set
        public float PeakAlong;     // metres along the beach (0 = centre) where this wave breaks first
    }
}
