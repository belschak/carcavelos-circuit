using System;

namespace CarcavelosCircuit.Visuals
{
    // Numbers behind the checkpoint gates (task V13). Plain C#, Edit Mode tested.
    //   Where the two banners stand: just outside the road (and outside Noah's white fence, 0.3 m off the edge).
    //   The feather shape: the top edge falls from the pole top towards the free edge.
    //   How the cloth waves: a wave runs from the pole to the free edge. At the pole the cloth is held (no movement),
    //   the free edge swings most; three sine waves so it never looks like a loop, and a seed per banner so
    //   neighbouring banners do not wave in step. Values in metres, sideways to the cloth.
    public static class CheckpointGatePlan
    {
        public const float PostMargin = 0.9f;     // metres outside the road edge
        public const float MaxWave = 0.22f;       // the free edge never swings further than this
        public const float TopDrop = 0.6f;        // the top corner at the free edge is this much lower than the pole top

        public const float InsideTrigger = 0.3f;  // V33: a pole stands this far inside its checkpoint's trigger edge

        // V33 (Luc, 15 Nov): the poles show how wide the checkpoint really is. Distance from the route point to the
        // pole on one side: the trigger's extent on that side minus a little, so a kart passing just inside a pole
        // still counts; never closer than the old place just outside the road.
        public static float PostOffsetForTrigger(float roadWidth, float triggerSide)
        {
            return Math.Max(PostOffset(roadWidth), triggerSide - InsideTrigger);
        }

        // Distance from the road centre to each pole.
        public static float PostOffset(float roadWidth)
        {
            return Math.Max(0f, roadWidth) * 0.5f + PostMargin;
        }

        // Height of the cloth's top edge above the pole top at u (0 = pole, 1 = free edge): a rounded fall.
        public static float TopEdge(float u)
        {
            float x = Clamp01(u);
            return -TopDrop * x * x;
        }

        // Sideways offset of the cloth at u (0 = pole .. 1 = free edge), v (0 = bottom .. 1 = top) and time.
        public static float Wave(float u, float v, float time, float seed)
        {
            float x = Clamp01(u), y = Clamp01(v);
            double t = time + seed * 5.37;
            double phase = t * 3.1 - x * 3.4 + y * 0.8;            // runs outwards and a little upwards
            double wave = 0.6 * Math.Sin(phase) + 0.25 * Math.Sin(phase * 2.3 + 1.1) + 0.15 * Math.Sin(t * 1.3 + y * 2.0);
            double hold = Math.Pow(x, 1.4);                        // the pole holds the cloth
            return (float)(MaxWave * hold * wave);
        }

        private static float Clamp01(float f)
        {
            return f < 0f ? 0f : (f > 1f ? 1f : f);
        }
    }
}
