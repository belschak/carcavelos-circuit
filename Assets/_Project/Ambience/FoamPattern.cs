using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // The lacy look of sea foam as a small picture made in code (no imported texture): thin white lines around
    // bubble-like cells, two sizes on top of each other. It repeats without a seam, so one 128 px picture can cover
    // the whole beach. Plain maths (Edit Mode tested); SeasideAmbience turns it into a Texture2D once at the start.
    public static class FoamPattern
    {
        // How much foam (0..1) at this spot of the picture; u and v run 0..1 across it and repeat beyond.
        // cells = bubbles per side of the picture.
        public static float Lace(float u, float v, int cells, int seed)
        {
            if (float.IsNaN(u) || float.IsNaN(v) || cells < 1)
            {
                return 0f;
            }
            // Bend the grid a little (whole waves across the picture, so it still repeats): no straight rows of bubbles.
            float bentU = u + 0.04f * Mathf.Sin((v * 3f + 0.2f) * Mathf.PI * 2f);
            float bentV = v + 0.04f * Mathf.Sin((u * 2f + 0.7f) * Mathf.PI * 2f);
            float big = Web(bentU, bentV, cells, seed, 0.1f, 0.36f);                        // thick soft-edged net
            float small = Web(bentU + 0.37f, bentV + 0.11f, cells * 3, seed + 101, 0.04f, 0.3f);  // fine bubbles inside
            return Mathf.Clamp01(Mathf.Max(big, 0.4f * small));
        }

        // Fills 'pixels' (size x size, row by row) with white whose alpha is the lace. False = wrong array size.
        public static bool Fill(Color32[] pixels, int size, int cells, int seed)
        {
            if (pixels == null || size < 1 || pixels.Length != size * size)
            {
                return false;
            }
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float lace = Lace((x + 0.5f) / size, (y + 0.5f) / size, cells, seed);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(lace * 255f));
                }
            }
            return true;
        }

        // One layer: every grid square has one bubble centre at a "random" place; a spot is white where it is about
        // as far from its nearest centre as from the second nearest (= on the border between two bubbles).
        // solid = how close to a border counts as solid white, soft = where the white has faded to nothing.
        private static float Web(float u, float v, int cells, int seed, float solid, float soft)
        {
            float x = (u - Mathf.Floor(u)) * cells;
            float y = (v - Mathf.Floor(v)) * cells;
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            float nearest = 99f;
            float second = 99f;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cx + dx;
                    int gy = cy + dy;
                    // The square's number wraps round the edge of the picture: that makes it repeat without a seam.
                    int square = Wrap(gx, cells) * 131 + Wrap(gy, cells) * 7919;
                    float px = gx + 0.15f + 0.7f * WaveSchedule.Hash01(square, seed);
                    float py = gy + 0.15f + 0.7f * WaveSchedule.Hash01(square, seed + 1);
                    float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                    if (d < nearest)
                    {
                        second = nearest;
                        nearest = d;
                    }
                    else if (d < second)
                    {
                        second = d;
                    }
                }
            }
            return 1f - WaveProfile.Smooth(solid, soft, second - nearest);
        }

        private static int Wrap(int value, int size)
        {
            int m = value % size;
            return m < 0 ? m + size : m;
        }
    }
}
