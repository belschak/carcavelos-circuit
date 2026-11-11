namespace CarcavelosCircuit.RaceFlow
{
    // Task R9, plain C# (Edit Mode tested): one racer's progress over the whole race, built from the
    // "distance along the lap" (0 .. LapLength, ITrackProgress) it gets every frame.
    // That distance jumps back to 0 at the start/finish line, so on its own it cannot tell "just behind the line"
    // from "almost a full lap done". We count line crossings: a jump of more than half a lap backwards
    // (e.g. 1180 m -> 3 m) = crossed the line forwards, a lap more; a jump of more than half a lap forwards
    // (3 m -> 1180 m) = crossed it backwards (wrong way), a lap less.
    // The very first value decides the start: in the back half of the lap = still behind the start line
    // (the grid stands behind it), so LapsDone starts at -1 and the kart is a few metres short of 0, not a lap ahead.
    public class LapProgress
    {
        private readonly float lapLength;

        public LapProgress(float lapLength)
        {
            this.lapLength = IsNumber(lapLength) && lapLength > 0f ? lapLength : 0f;
        }

        // False until the first usable distance arrived (and again after Reset).
        public bool HasValue { get; private set; }

        // Start/finish line crossings so far: -1 = still on the grid behind the line, 0 = on the first lap, 1 = one lap done.
        public int LapsDone { get; private set; }

        // Last distance along the lap in metres (0 .. LapLength).
        public float Distance { get; private set; }

        // Metres since the start line over the whole race (negative while behind it). Ahead = bigger.
        public float Total { get { return LapsDone * lapLength + Distance; } }

        // Feed this frame's distance along the lap. NaN, infinity and values without a lap length are ignored,
        // so one bad frame from the track maths can never add or take away a lap.
        public void Feed(float distanceAlongLap)
        {
            if (lapLength <= 0f || !IsNumber(distanceAlongLap))
            {
                return;
            }
            float d = Wrap(distanceAlongLap);
            if (!HasValue)
            {
                HasValue = true;
                LapsDone = d > lapLength * 0.5f ? -1 : 0;
                Distance = d;
                return;
            }
            float jump = d - Distance;
            if (jump < -lapLength * 0.5f)
            {
                LapsDone++;
            }
            else if (jump > lapLength * 0.5f)
            {
                LapsDone--;
            }
            Distance = d;
        }

        // Race start and Retry: forget everything, the next Feed starts again.
        public void Reset()
        {
            HasValue = false;
            LapsDone = 0;
            Distance = 0f;
        }

        // True if this racer is further than the other one: more laps, or the same laps and further along.
        // Exactly equal = not ahead, so equal racers keep their order (RaceStandings relies on that).
        public bool IsAheadOf(LapProgress other)
        {
            if (other == null || !other.HasValue)
            {
                return HasValue;
            }
            if (!HasValue)
            {
                return false;
            }
            if (LapsDone != other.LapsDone)
            {
                return LapsDone > other.LapsDone;
            }
            return Distance > other.Distance;
        }

        private float Wrap(float distance)
        {
            float wrapped = distance % lapLength;
            if (wrapped < 0f)
            {
                wrapped += lapLength;
            }
            return wrapped;
        }

        private static bool IsNumber(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
