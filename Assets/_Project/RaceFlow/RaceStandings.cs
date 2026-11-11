using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R9, plain C# (Edit Mode tested): who is 1st, 2nd, 3rd ... right now.
    // Order = laps done, then distance along the lap (LapProgress counts the start line crossings, so the grid behind
    // the line is not "almost a lap ahead"). Racers who are exactly level keep the order they had (stable), so the
    // positions do not flicker while the karts wait on the grid. Racers who join are added at the back, racers who
    // left are dropped. A racer without a position (no Root) is always last.
    public class RaceStandings
    {
        private readonly List<IRacer> order = new List<IRacer>();           // current order, index 0 = leader
        private readonly List<LapProgress> progress = new List<LapProgress>(); // progress[i] belongs to order[i]
        private float lapLength = -1f;

        public int Count { get { return order.Count; } }

        // The racer in this place, 0 = leader. null outside 0 .. Count - 1.
        public IRacer RacerAt(int index)
        {
            return index >= 0 && index < order.Count ? order[index] : null;
        }

        // 1 = leader, 2 = second ... 0 = not in the race.
        public int PositionOf(IRacer racer)
        {
            int i = order.IndexOf(racer);
            return i < 0 ? 0 : i + 1;
        }

        // The racer's progress (laps and distance), null if it is not in the race.
        public LapProgress ProgressOf(IRacer racer)
        {
            int i = order.IndexOf(racer);
            return i < 0 ? null : progress[i];
        }

        // Race start and Retry: every racer starts counting again from the grid. The order stays until they move.
        public void Reset()
        {
            for (int i = 0; i < progress.Count; i++)
            {
                progress[i].Reset();
            }
        }

        // Once per frame: follow who races (racers = RacerRegistry.All), feed each one's distance along the lap,
        // then sort. track may be null or invalid: then only the list of racers is kept up to date.
        public void Update(IReadOnlyList<IRacer> racers, ITrackProgress track)
        {
            if (track != null && track.LapLength != lapLength)
            {
                // A different track (or the first one): old distances mean nothing on it.
                lapLength = track.LapLength;
                for (int i = 0; i < progress.Count; i++)
                {
                    progress[i] = new LapProgress(lapLength);
                }
            }
            RemoveLeavers(racers);
            AddNewcomers(racers);
            if (track != null)
            {
                for (int i = 0; i < order.Count; i++)
                {
                    Transform root = order[i].Root;
                    if (root != null)
                    {
                        progress[i].Feed(track.DistanceAlongLap(root.position));
                    }
                }
            }
            SortStable();
        }

        private void RemoveLeavers(IReadOnlyList<IRacer> racers)
        {
            for (int i = order.Count - 1; i >= 0; i--)
            {
                if (!Contains(racers, order[i]))
                {
                    order.RemoveAt(i);
                    progress.RemoveAt(i);
                }
            }
        }

        private void AddNewcomers(IReadOnlyList<IRacer> racers)
        {
            if (racers == null)
            {
                return;
            }
            for (int i = 0; i < racers.Count; i++)
            {
                if (racers[i] != null && !order.Contains(racers[i]))
                {
                    order.Add(racers[i]);
                    progress.Add(new LapProgress(lapLength));
                }
            }
        }

        // Insertion sort: moves a racer up only while it is strictly ahead of the one in front, so level racers never
        // swap. Six racers at most, so this is tiny, and it never allocates (no garbage in the Web build).
        private void SortStable()
        {
            for (int i = 1; i < order.Count; i++)
            {
                int j = i;
                while (j > 0 && progress[j].IsAheadOf(progress[j - 1]))
                {
                    Swap(j, j - 1);
                    j--;
                }
            }
        }

        private void Swap(int a, int b)
        {
            IRacer racer = order[a];
            order[a] = order[b];
            order[b] = racer;
            LapProgress p = progress[a];
            progress[a] = progress[b];
            progress[b] = p;
        }

        private static bool Contains(IReadOnlyList<IRacer> racers, IRacer racer)
        {
            if (racers == null)
            {
                return false;
            }
            for (int i = 0; i < racers.Count; i++)
            {
                if (racers[i] == racer)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
