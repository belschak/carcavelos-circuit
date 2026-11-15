using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // The face of the new item box (task V34): a hand-painted Portuguese azulejo, own design, drawn in code by the
    // builder into a PNG (Visuals/Items/Textures/). Plain maths, so Edit Mode tests can check it.
    //   Classic (row boxes): cobalt and sky blue on warm glazed white, a double frame, an eight-petal rosette with
    //   an ochre heart, leaf sprigs on the diagonals and quarter rings in the corners (four faces meeting at a
    //   corner of the cube close those into circles).
    //   Wave (V19 wave boxes): deep teal and turquoise, a breaking wave medallion instead of the rosette.
    // Every edge is soft (about 1.5 px), like a brush stroke in glaze, and the white has a faint uneven glaze.
    // Not a Nintendo item box: no question mark, no see-through rainbow cube (docs/art-style.md).
    public static class AzulejoTile
    {
        public enum Variant { Classic, Wave }

        private static readonly Color Glaze = new Color(0.97f, 0.95f, 0.90f, 1f);
        private static readonly Color Cobalt = new Color(0.08f, 0.24f, 0.62f, 1f);
        private static readonly Color Sky = new Color(0.18f, 0.55f, 0.84f, 1f);   // Atlantic Blue
        private static readonly Color Ochre = new Color(0.89f, 0.66f, 0.22f, 1f);
        private static readonly Color Teal = new Color(0.02f, 0.38f, 0.45f, 1f);
        private static readonly Color Turquoise = new Color(0.08f, 0.80f, 0.86f, 1f); // Luc's wave

        // Colour at (u, v), both 0..1 across one face. px = the size of one pixel in u (for the soft edges).
        public static Color ColourAt(float u, float v, Variant variant, float px)
        {
            Color ink = variant == Variant.Wave ? Teal : Cobalt;
            Color wash = variant == Variant.Wave ? Turquoise : Sky;
            float soft = px * 1.5f;

            // Glazed white, a touch darker where the glaze pools near the edges, with a slow uneven shimmer.
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float pool = Mathf.Lerp(0.93f, 1f, Mathf.Clamp01(edge / 0.18f));
            float shimmer = 1f + 0.018f * Mathf.Sin(u * 23f + v * 7f) * Mathf.Sin(v * 19f - u * 5f);
            Color colour = Glaze * (pool * shimmer);
            colour.a = 1f;

            // Double frame: a thick band at the edge and a thin line inside it.
            colour = Paint(colour, ink, Band(edge, 0f, 0.075f, soft));
            colour = Paint(colour, wash, Band(edge, 0.105f, 0.125f, soft));

            // Quarter rings in the corners (they close into circles where four faces meet).
            float cx = Mathf.Min(u, 1f - u);
            float cy = Mathf.Min(v, 1f - v);
            float corner = Mathf.Sqrt(cx * cx + cy * cy);
            colour = Paint(colour, wash, Band(corner, 0.15f, 0.2f, soft));
            colour = Paint(colour, ink, Band(corner, 0.225f, 0.245f, soft));

            float x = u - 0.5f;
            float y = v - 0.5f;
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);

            // Leaf sprigs on the four diagonals, between the middle motif and the corner rings.
            float diagonal = Mathf.Abs(Mathf.Abs(x) - Mathf.Abs(y)) / 1.4142f; // distance to the nearest diagonal
            float along = r;
            float leaf = Mathf.Max(0f, 1f - Mathf.Abs(along - 0.37f) / 0.08f) * 0.045f; // a lens: widest in its middle
            colour = Paint(colour, ink, Inside(diagonal, leaf, soft) * Step(along, 0.29f, 0.45f, soft));

            if (variant == Variant.Classic)
            {
                // Eight-petal rosette: petal edge r = 0.25 |cos 4a|^0.6, washed in sky blue with a cobalt rim.
                float petal = 0.25f * Mathf.Pow(Mathf.Abs(Mathf.Cos(4f * a)), 0.6f);
                float petalInside = Inside(r, petal, soft);
                colour = Paint(colour, ink, petalInside);
                colour = Paint(colour, wash, Inside(r, petal - 0.03f, soft));
                colour = Paint(colour, ink, Band(r, 0.075f, 0.095f, soft));
                colour = Paint(colour, Ochre, Inside(r, 0.06f, soft));
            }
            else
            {
                // A breaking wave in a round medallion: turquoise sea with a wavy top and a white foam line,
                // a curl rolling over on the crest, three foam drops flying off it.
                float disc = Inside(r, 0.27f, soft);
                colour = Paint(colour, new Color(0.88f, 0.97f, 0.98f, 1f), disc);
                float crest = -0.02f + 0.045f * Mathf.Sin((x + 0.02f) * 14f);
                colour = Paint(colour, wash, Inside(y, crest, soft) * disc);
                colour = Paint(colour, Color.white, Step(y, crest - 0.012f, crest + 0.012f, soft) * disc);
                float curlX = x + 0.08f;
                float curlY = y - 0.035f;
                float curlR = Mathf.Sqrt(curlX * curlX + curlY * curlY);
                float turn = Mathf.Atan2(curlY, curlX);
                if (turn < 0f) turn += 2f * Mathf.PI;
                float best = 1f;
                for (int k = 0; k < 3; k++)
                {
                    float spiral = 0.012f + 0.011f * (turn + k * 2f * Mathf.PI); // grows ~0.07 per turn
                    if (spiral > 0.13f) break;
                    best = Mathf.Min(best, Mathf.Abs(curlR - spiral));
                }
                colour = Paint(colour, ink, Inside(best, 0.015f, soft) * Inside(curlR, 0.135f, soft) * Inside(curlY, 0.08f, soft));
                colour = Paint(colour, wash, Inside(Dist(x, y, 0.12f, 0.09f), 0.018f, soft) * disc);
                colour = Paint(colour, wash, Inside(Dist(x, y, 0.16f, 0.03f), 0.012f, soft) * disc);
                colour = Paint(colour, wash, Inside(Dist(x, y, 0.07f, 0.15f), 0.012f, soft) * disc);
                colour = Paint(colour, ink, Band(r, 0.27f, 0.29f, soft));
            }
            return colour;
        }

        // The whole face as pixels, row by row from the bottom (Texture2D.SetPixels order).
        public static Color[] Paint(int size, Variant variant)
        {
            Color[] pixels = new Color[size * size];
            float px = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = ColourAt((x + 0.5f) * px, (y + 0.5f) * px, variant, px);
                }
            }
            return pixels;
        }

        private static float Dist(float x, float y, float cx, float cy)
        {
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        }

        // 1 inside [from, to], 0 outside, soft edges.
        private static float Band(float value, float from, float to, float soft)
        {
            return Step(value, from, to, soft);
        }

        private static float Step(float value, float from, float to, float soft)
        {
            return Mathf.Clamp01((value - from) / soft + 0.5f) * Mathf.Clamp01((to - value) / soft + 0.5f);
        }

        // 1 where value < limit, soft edge.
        private static float Inside(float value, float limit, float soft)
        {
            return Mathf.Clamp01((limit - value) / soft + 0.5f);
        }

        private static Color Paint(Color under, Color ink, float amount)
        {
            Color c = Color.Lerp(under, ink, Mathf.Clamp01(amount));
            c.a = 1f;
            return c;
        }
    }
}
