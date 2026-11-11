using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Surf comes in sets: a few waves a fixed period apart, then a calm gap (WaveTuning). This class answers
    // "which waves are on the water at this time, and where?". Pure maths on the clock, nothing is stored between
    // frames, so the same time always gives the same waves (handy for tests and for the QA renders).
    public class WaveSchedule
    {
        private readonly WaveTuning tuning;

        public WaveSchedule(WaveTuning tuning)
        {
            this.tuning = tuning != null ? tuning.Sanitised() : new WaveTuning().Sanitised();
        }

        public WaveTuning Tuning { get { return tuning; } }

        // Most waves that can be on the water at once (sizes the buffers).
        public int MaxActiveWaves
        {
            get
            {
                // A set puts one wave in per period; the lull only lowers the count, so this is an upper bound.
                return Mathf.CeilToInt(tuning.LifeSeconds / tuning.Period) + 1;
            }
        }

        // When wave number id appears out at sea. Works for negative numbers too, so at the race start (time 0)
        // the sea is already full of waves instead of empty.
        public float SpawnTime(int id)
        {
            int perSet = tuning.WavesPerSet;
            int set = FloorDiv(id, perSet);
            int inSet = id - set * perSet;
            return set * tuning.SetSeconds + inSet * tuning.Period;
        }

        // Fills 'result' with every wave alive at 'time', the one furthest out first. Returns how many.
        // NaN or infinite time = no waves (never throws).
        public int ActiveWaves(float time, WaveState[] result)
        {
            if (result == null || float.IsNaN(time) || float.IsInfinity(time))
            {
                return 0;
            }
            float life = tuning.LifeSeconds;
            int perSet = tuning.WavesPerSet;
            int firstSet = Mathf.FloorToInt((time - life) / tuning.SetSeconds) - 1;
            int lastSet = Mathf.FloorToInt(time / tuning.SetSeconds) + 1;
            int count = 0;
            // Oldest first = furthest along = closest to the beach; we want the one furthest out first, so we
            // walk from the newest wave back to the oldest.
            for (int set = lastSet; set >= firstSet; set--)
            {
                for (int inSet = perSet - 1; inSet >= 0; inSet--)
                {
                    int id = set * perSet + inSet;
                    float age = time - SpawnTime(id);
                    if (age < 0f || age >= life || count >= result.Length)
                    {
                        continue;
                    }
                    result[count] = Describe(id, age);
                    count++;
                }
            }
            return count;
        }

        // Where wave 'id' is when it is 'age' seconds old.
        public WaveState Describe(int id, float age)
        {
            var w = new WaveState();
            w.Id = id;
            w.Age = age;
            float travelled = age * tuning.Speed;
            w.Distance = Mathf.Max(0f, tuning.SpawnDistance - travelled);
            float swashAge = age - tuning.TravelSeconds;
            w.Swash = swashAge <= 0f ? 0f : Mathf.Clamp01(swashAge / tuning.SwashSeconds);
            w.HeightScale = SetShape(id) * (0.86f + 0.28f * Hash01(id, 17));
            // Peaks stay inside the beach (away from the fading ends) so every wave breaks where it can be seen.
            float usable = tuning.BeachLength * 0.5f - tuning.EndFade;
            w.PeakAlong = (Hash01(id, 91) * 2f - 1f) * Mathf.Max(0f, usable) * 0.8f;
            return w;
        }

        // The middle waves of a set are the biggest, the first and last ones smaller (like real sets).
        public float SetShape(int id)
        {
            int perSet = tuning.WavesPerSet;
            int inSet = id - FloorDiv(id, perSet) * perSet;
            float x = (inSet + 0.5f) / perSet;          // 0..1 across the set
            return 0.7f + 0.3f * Mathf.Sin(x * Mathf.PI);
        }

        // A repeatable "random" number 0..1 for a wave: the same wave always gets the same size and peak.
        public static float Hash01(int id, int salt)
        {
            unchecked
            {
                uint h = (uint)id * 747796405u + (uint)salt * 2891336453u;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // Integer division that rounds towards minus infinity (-1 / 4 = -1, not 0), for negative wave numbers.
        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0)))
            {
                q--;
            }
            return q;
        }
    }
}
