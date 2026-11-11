namespace CarcavelosCircuit.Ambience
{
    // The wave numbers (from AmbienceSettings) as a plain object. Plain public fields on purpose: tests set them directly.
    // Sanitised() turns typos in the Inspector (0, negative, NaN) into safe values, so the waves never divide by zero.
    public class WaveTuning
    {
        public float SpawnDistance = 105f;   // metres out to sea where a swell appears
        public float BreakDistance = 26f;    // metres out where the peak breaks
        public float Speed = 4.2f;           // m/s towards the beach
        public int WavesPerSet = 5;
        public float Period = 7f;            // seconds between waves of one set
        public float SetLull = 11f;          // extra calm seconds after a set
        public float SwellHeight = 0.45f;    // metres, far out
        public float BreakHeight = 2.3f;     // metres, just before the break
        public float WhitewaterShare = 0.5f; // foam roll height on its way in, as a share of BreakHeight
        public float PeelSpeed = 5.2f;       // m/s the break travels along the wave
        public float RunUp = 7f;             // metres the foam runs up the sand
        public float SwashSeconds = 7f;      // seconds for the run-up and fade
        public float BeachLength = 240f;     // metres of beach with waves
        public float EndFade = 32f;          // metres at each end where the waves fade

        public WaveTuning Sanitised()
        {
            var t = new WaveTuning();
            t.SpawnDistance = AtLeast(SpawnDistance, 20f, 105f);
            t.BreakDistance = Clamp(BreakDistance, 2f, t.SpawnDistance - 10f, 26f);
            t.Speed = AtLeast(Speed, 0.5f, 4.2f);
            t.WavesPerSet = WavesPerSet < 1 ? 1 : (WavesPerSet > 12 ? 12 : WavesPerSet);
            t.Period = AtLeast(Period, 2f, 7f);
            t.SetLull = AtLeast(SetLull, 0f, 11f);
            t.SwellHeight = AtLeast(SwellHeight, 0f, 0.45f);
            t.BreakHeight = AtLeast(BreakHeight, 0.1f, 2.3f);
            t.WhitewaterShare = Clamp(WhitewaterShare, 0.05f, 1f, 0.5f);
            t.PeelSpeed = AtLeast(PeelSpeed, 0.5f, 5.2f);
            t.RunUp = AtLeast(RunUp, 0f, 7f);
            t.SwashSeconds = AtLeast(SwashSeconds, 0.5f, 7f);
            t.BeachLength = AtLeast(BeachLength, 20f, 240f);
            t.EndFade = Clamp(EndFade, 1f, t.BeachLength * 0.45f, 32f);
            return t;
        }

        // Seconds from "appears out at sea" to "reaches the waterline".
        public float TravelSeconds { get { return SpawnDistance / Speed; } }

        // Seconds a wave exists in total: rolling in, then the foam running up the sand.
        public float LifeSeconds { get { return TravelSeconds + SwashSeconds; } }

        // Seconds from the first wave of one set to the first wave of the next.
        public float SetSeconds { get { return WavesPerSet * Period + SetLull; } }

        private static float AtLeast(float value, float min, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }
            return value < min ? min : value;
        }

        private static float Clamp(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
            }
            if (max < min)
            {
                max = min;
            }
            return value < min ? min : (value > max ? max : value);
        }
    }
}
