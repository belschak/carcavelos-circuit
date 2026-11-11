namespace CarcavelosCircuit.Ambience
{
    // What a surfer can see of one wave at its own spot on the beach. Plain data.
    public struct WaveSample
    {
        public int Id;            // the wave's running number (WaveState.Id)
        public float Crest;       // metres from the waterline to the crest
        public float Height;      // metres of smooth water at the surfer's spot (0 = no wave here)
        public float Broken;      // 0 smooth .. 1 white foam at the surfer's spot
        public float PeakAlong;   // metres along the beach where it broke first
        public float Speed;       // m/s towards the beach
    }
}
