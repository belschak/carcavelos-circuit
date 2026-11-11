using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // All waves at the current time (from WaveSchedule) plus the answers surfers need (ISurfWaves).
    // Plain C#: Update(time) once per frame, then read. Distances are in the beach's own frame (see WaveProfile).
    public class WaveField : ISurfWaves
    {
        private readonly WaveSchedule schedule;
        private readonly WaveTuning tuning;
        private readonly WaveState[] waves;
        private int count;

        public WaveField(WaveTuning tuning)
        {
            schedule = new WaveSchedule(tuning);
            this.tuning = schedule.Tuning;
            waves = new WaveState[schedule.MaxActiveWaves];
        }

        public WaveTuning Tuning { get { return tuning; } }
        public int Count { get { return count; } }
        public int Capacity { get { return waves.Length; } }

        // Wave i of the current frame, the one furthest out first.
        public WaveState Get(int i)
        {
            return waves[i];
        }

        public void Update(float time)
        {
            count = schedule.ActiveWaves(time, waves);
        }

        // Smooth-water height of wave w at this spot (0 where it has broken or faded).
        public float HeightOf(WaveState w, float along)
        {
            return WaveProfile.UnbrokenHeight(tuning, w.Distance) * w.HeightScale * WaveProfile.EndFade(tuning, along);
        }

        public float SurfaceHeight(float along, float distance)
        {
            float best = 0f;
            for (int i = 0; i < count; i++)
            {
                WaveState w = waves[i];
                if (w.Swash > 0f)
                {
                    continue; // foam on the sand, nothing to float on
                }
                float broken = WaveProfile.Broken(tuning, along - w.PeakAlong, w.Distance);
                float smooth = HeightOf(w, along) * (1f - broken);
                float foam = WaveProfile.FoamHeight(tuning, w.Distance) * broken * WaveProfile.EndFade(tuning, along);
                float offset = w.Distance - distance;      // > 0 = the spot is on the beach side of the crest
                float h = WaveProfile.HeightAtOffset(smooth + foam, offset);
                if (h > best)
                {
                    best = h;                              // waves never overlap much: the highest one wins
                }
            }
            return best;
        }

        public bool FindIncoming(float along, float distance, float window, float minHeight, out WaveSample wave)
        {
            wave = new WaveSample();
            bool found = false;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                WaveState w = waves[i];
                float ahead = w.Distance - distance;      // metres the crest still has to roll to reach the spot
                if (w.Swash > 0f || ahead <= 0f || ahead > window || ahead >= nearest)
                {
                    continue;
                }
                if (WaveProfile.Broken(tuning, along - w.PeakAlong, w.Distance) > 0.05f)
                {
                    continue;
                }
                float sizeAtBreak = tuning.BreakHeight * w.HeightScale * WaveProfile.EndFade(tuning, along);
                if (sizeAtBreak < minHeight)
                {
                    continue;
                }
                nearest = ahead;
                wave = Sample(w, along);
                found = true;
            }
            return found;
        }

        public bool TryGetWave(int id, float along, out WaveSample wave)
        {
            for (int i = 0; i < count; i++)
            {
                if (waves[i].Id == id && waves[i].Swash <= 0f)
                {
                    wave = Sample(waves[i], along);
                    return true;
                }
            }
            wave = new WaveSample();
            return false;
        }

        private WaveSample Sample(WaveState w, float along)
        {
            var s = new WaveSample();
            s.Id = w.Id;
            s.Crest = w.Distance;
            s.Broken = WaveProfile.Broken(tuning, along - w.PeakAlong, w.Distance);
            s.Height = HeightOf(w, along);
            s.PeakAlong = w.PeakAlong;
            s.Speed = tuning.Speed;
            return s;
        }
    }
}
