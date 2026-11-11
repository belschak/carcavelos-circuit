using System.Collections.Generic;

namespace CarcavelosCircuit.Visuals
{
    // Where the boost pads go (task V11). Plain C#, Edit Mode tested. Works on distances along the route in metres
    // (the "s" of every route point), so it does not care which track it is: the builder hands in the take-off
    // point of the jump and the tunnel stretches, and gets back the distances where a pad goes.
    //   Jump: one pad a bit before the jump, one right before the edge, so the kart is still boosting when it flies.
    //   Tunnel: the long straight gets a chain of pads, so it is not boring.
    public static class BoostPadPlan
    {
        public const float BeforeJumpMetres = 25f;   // "kurz vor der Rampe"
        public const float OnRampMetres = 6f;        // on the run-up, the boost still runs at the edge
        public const float TunnelFirstMetres = 14f;  // first pad this far into the tunnel
        public const float TunnelSpacingMetres = 28f;
        public const float TunnelEndGapMetres = 10f; // no pad closer than this to the tunnel exit
        public const float MinTunnelMetres = 30f;    // shorter covered bits (a bridge, a deck) get no pads

        // The two jump pads, in driving order. Distances can go below 0 when the jump is right after the start line;
        // the caller wraps them around the lap.
        public static List<float> JumpPads(float takeOffDistance)
        {
            return new List<float> { takeOffDistance - BeforeJumpMetres, takeOffDistance - OnRampMetres };
        }

        // Pads every TunnelSpacingMetres from TunnelFirstMetres after the entry, none in the last TunnelEndGapMetres.
        // A tunnel shorter than MinTunnelMetres gets none.
        public static List<float> TunnelPads(float tunnelStart, float tunnelEnd)
        {
            var pads = new List<float>();
            if (tunnelEnd - tunnelStart < MinTunnelMetres)
            {
                return pads;
            }
            for (float d = tunnelStart + TunnelFirstMetres; d <= tunnelEnd - TunnelEndGapMetres; d += TunnelSpacingMetres)
            {
                pads.Add(d);
            }
            return pads;
        }

        // Every run of covered points as (first index, last index), in route order.
        public static List<int[]> CoveredRuns(bool[] covered)
        {
            var runs = new List<int[]>();
            int start = -1;
            for (int i = 0; i <= covered.Length; i++)
            {
                bool on = i < covered.Length && covered[i];
                if (on && start < 0)
                {
                    start = i;
                }
                else if (!on && start >= 0)
                {
                    runs.Add(new[] { start, i - 1 });
                    start = -1;
                }
            }
            return runs;
        }

        // Index of the route point closest to a distance. Distances outside the lap wrap around it first.
        // distances must be in increasing order (the route's "s").
        public static int ClosestPoint(float[] distances, float lapLength, float distance)
        {
            if (distances.Length == 0)
            {
                return -1;
            }
            if (lapLength > 0f)
            {
                distance %= lapLength;
                if (distance < 0f)
                {
                    distance += lapLength;
                }
            }
            int best = 0;
            float bestGap = float.MaxValue;
            for (int i = 0; i < distances.Length; i++)
            {
                float gap = System.Math.Abs(distances[i] - distance);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }
            return best;
        }
    }
}
