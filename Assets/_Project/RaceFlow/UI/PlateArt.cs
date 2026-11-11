using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The pixel maths behind the HUD's "plates" (task R15, arcade UI restyle). USS in Unity 6.3 has no box-shadow and
    // no gradients, so every plate (panel, button, chip, rank shield) is a small PNG: a rounded shape with a vertical
    // colour gradient, a light stroke round it, a thin bright line under the top edge (the "bevel") and a hard shadow
    // (or a coloured "lip") straight below it. The USS shows them 9-sliced (-unity-slice-*), so one image fits any size.
    // Editor/HudArtBuilder writes the PNGs once with these methods; the game only loads the finished images.
    // Plain C# without Unity objects, so Edit Mode tests can check the shapes (PlateArtTests).
    // Pixel rows go from the TOP down here (row 0 = top), like the picture you look at; HudArtBuilder flips them for Texture2D.
    public static class PlateArt
    {
        // How much of a pixel (0..1) lies inside a rounded rectangle. The rectangle starts at (left, top), is
        // width x height big and has corners of the given radius. Uses the distance to the edge, so the edge is
        // soft for one pixel (anti-aliasing) instead of jagged.
        public static float RoundedRectCoverage(float px, float py, float left, float top, float width, float height, float radius)
        {
            if (width <= 0f || height <= 0f)
            {
                return 0f; // a shape with no size covers nothing
            }
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(width, height) * 0.5f);
            // Distance from the pixel centre to the rectangle, measured from the middle (one quarter is enough: it is symmetric).
            float cx = px + 0.5f - (left + width * 0.5f);
            float cy = py + 0.5f - (top + height * 0.5f);
            float qx = Mathf.Abs(cx) - (width * 0.5f - radius);
            float qy = Mathf.Abs(cy) - (height * 0.5f - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            float distance = outside + inside - radius; // < 0 inside, > 0 outside
            return Mathf.Clamp01(0.5f - distance);
        }

        // How much of a pixel lies inside a "shield" (the podium rank badge): straight sides with round top corners,
        // and the bottom third runs to a point in the middle. Measured with 4 x 4 samples per pixel (anti-aliasing).
        public static float ShieldCoverage(float px, float py, float left, float top, float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                return 0f;
            }
            int hits = 0;
            for (int sy = 0; sy < 4; sy++)
            {
                for (int sx = 0; sx < 4; sx++)
                {
                    float x = px + (sx + 0.5f) / 4f - left;
                    float y = py + (sy + 0.5f) / 4f - top;
                    if (InsideShield(x, y, width, height))
                    {
                        hits++;
                    }
                }
            }
            return hits / 16f;
        }

        private static bool InsideShield(float x, float y, float width, float height)
        {
            if (x < 0f || x > width || y < 0f || y > height)
            {
                return false;
            }
            float corner = width * 0.22f;
            // Round top corners.
            if (y < corner)
            {
                float dx = x < corner ? corner - x : (x > width - corner ? x - (width - corner) : 0f);
                float dy = corner - y;
                if (dx > 0f && dx * dx + dy * dy > corner * corner)
                {
                    return false;
                }
            }
            // The bottom part narrows to a point: at the bottom edge only the middle is left.
            float pointStart = height * 0.55f;
            if (y > pointStart)
            {
                float t = (y - pointStart) / (height - pointStart); // 0 where it starts to narrow, 1 at the tip
                float halfWidth = width * 0.5f * (1f - t);
                return Mathf.Abs(x - width * 0.5f) <= halfWidth;
            }
            return true;
        }

        // A whole plate picture, width x height pixels, row 0 at the top.
        // The body fills the picture except the bottom shadowPx rows, where the shadow (a copy of the body moved down)
        // shows. Layers from back to front: shadow, stroke, gradient fill, a light bevel line under the top edge.
        public static Color[] Plate(int width, int height, PlateLook look)
        {
            if (width <= 0 || height <= 0)
            {
                return new Color[0];
            }
            Color[] pixels = new Color[width * height];
            float bodyHeight = height - look.ShadowPx;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float shadow = RoundedRectCoverage(x, y, 0f, look.ShadowPx, width, bodyHeight, look.RadiusPx);
                    float outer = RoundedRectCoverage(x, y, 0f, 0f, width, bodyHeight, look.RadiusPx);
                    float inner = RoundedRectCoverage(x, y, look.StrokePx, look.StrokePx,
                        width - 2f * look.StrokePx, bodyHeight - 2f * look.StrokePx, look.RadiusPx - look.StrokePx);
                    float bevel = RoundedRectCoverage(x, y, look.StrokePx, look.StrokePx + look.BevelPx,
                        width - 2f * look.StrokePx, bodyHeight - 2f * look.StrokePx - look.BevelPx, look.RadiusPx - look.StrokePx);
                    float t = Mathf.Clamp01((y - look.StrokePx) / Mathf.Max(1f, bodyHeight - 2f * look.StrokePx));
                    Color fill = Color.Lerp(look.FillTop, look.FillBottom, t);
                    Color bevelColour = Color.Lerp(fill, Color.white, 0.35f);
                    bevelColour.a = fill.a;

                    Color pixel = Over(new Color(0f, 0f, 0f, 0f), Fade(look.Shadow, shadow));
                    pixel = Over(pixel, Fade(look.Stroke, outer));
                    pixel = Over(pixel, Fade(bevelColour, inner));      // the bevel line = the inner shape...
                    pixel = Over(pixel, Fade(fill, bevel));             // ...minus the fill, moved down a little
                    pixels[y * width + x] = pixel;
                }
            }
            return pixels;
        }

        // The podium badge: a shield in a gradient with a light stroke and a hard shadow.
        public static Color[] Shield(int width, int height, PlateLook look)
        {
            if (width <= 0 || height <= 0)
            {
                return new Color[0];
            }
            Color[] pixels = new Color[width * height];
            float bodyHeight = height - look.ShadowPx;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float shadow = ShieldCoverage(x, y, 0f, look.ShadowPx, width, bodyHeight);
                    float outer = ShieldCoverage(x, y, 0f, 0f, width, bodyHeight);
                    float inner = ShieldCoverage(x, y, look.StrokePx, look.StrokePx, width - 2f * look.StrokePx, bodyHeight - 2.6f * look.StrokePx);
                    float t = Mathf.Clamp01(y / Mathf.Max(1f, bodyHeight));
                    Color fill = Color.Lerp(look.FillTop, look.FillBottom, t);
                    Color pixel = Over(new Color(0f, 0f, 0f, 0f), Fade(look.Shadow, shadow));
                    pixel = Over(pixel, Fade(look.Stroke, outer));
                    pixel = Over(pixel, Fade(fill, inner));
                    pixels[y * width + x] = pixel;
                }
            }
            return pixels;
        }

        // The item slot frame: an azulejo tile. Navy middle, a cream outer stroke, a blue band with white diamonds
        // all round (the tile border), a thin cream line inside. size x size pixels, row 0 at the top.
        public static Color[] AzulejoFrame(int size, Color navy, Color blue, Color cream, Color ink)
        {
            if (size <= 0)
            {
                return new Color[0];
            }
            Color[] pixels = new Color[size * size];
            float shadowPx = size * 0.05f;
            float body = size - shadowPx;
            float radius = size * 0.18f;
            float stroke = size * 0.03f;
            float band = size * 0.11f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float shadow = RoundedRectCoverage(x, y, 0f, shadowPx, size, body, radius);
                    float outer = RoundedRectCoverage(x, y, 0f, 0f, size, body, radius);
                    float bandArea = RoundedRectCoverage(x, y, stroke, stroke, size - 2f * stroke, body - 2f * stroke, radius - stroke);
                    float line = RoundedRectCoverage(x, y, stroke + band, stroke + band, size - 2f * (stroke + band), body - 2f * (stroke + band), radius - stroke - band * 0.6f);
                    float middle = RoundedRectCoverage(x, y, stroke + band + stroke * 0.7f, stroke + band + stroke * 0.7f,
                        size - 2f * (stroke + band + stroke * 0.7f), body - 2f * (stroke + band + stroke * 0.7f), radius - stroke - band * 0.6f);
                    // Diamonds on the band: a repeating pattern along the band, one diamond every "band" pixels.
                    float bandMiddle = stroke + band * 0.5f;
                    float along = (Mathf.Abs(y + 0.5f - bandMiddle) < band * 0.5f || Mathf.Abs(y + 0.5f - (body - bandMiddle)) < band * 0.5f) ? x : y;
                    float across = Mathf.Min(Mathf.Min(Mathf.Abs(x + 0.5f - bandMiddle), Mathf.Abs(x + 0.5f - (size - bandMiddle))),
                        Mathf.Min(Mathf.Abs(y + 0.5f - bandMiddle), Mathf.Abs(y + 0.5f - (body - bandMiddle))));
                    float phase = Mathf.Repeat(along + 0.5f - bandMiddle, band) - band * 0.5f;
                    float diamond = Mathf.Clamp01(band * 0.28f - (Mathf.Abs(phase) + across) + 0.5f);
                    bool nearSide = x < stroke + band || x > size - stroke - band;
                    bool nearTopOrBottom = y < stroke + band || y > body - stroke - band;
                    if (nearSide && nearTopOrBottom)
                    {
                        diamond = 0f; // the four corners stay plain blue (the pattern would be cut there)
                    }

                    Color pixel = Over(new Color(0f, 0f, 0f, 0f), Fade(HudColors.WithAlpha(ink, 0.55f), shadow));
                    pixel = Over(pixel, Fade(cream, outer));
                    pixel = Over(pixel, Fade(blue, bandArea));
                    pixel = Over(pixel, Fade(cream, bandArea * diamond * (1f - line)));
                    pixel = Over(pixel, Fade(cream, line));
                    pixel = Over(pixel, Fade(navy, middle));
                    pixels[y * size + x] = pixel;
                }
            }
            return pixels;
        }

        // A colour with its alpha multiplied by coverage (how much of the pixel the shape covers).
        private static Color Fade(Color colour, float coverage)
        {
            colour.a *= coverage;
            return colour;
        }

        // "top over bottom": normal alpha blending of two straight-alpha colours.
        public static Color Over(Color bottom, Color top)
        {
            float alpha = top.a + bottom.a * (1f - top.a);
            if (alpha <= 0f)
            {
                return new Color(0f, 0f, 0f, 0f);
            }
            Color mixed = (top * top.a + bottom * bottom.a * (1f - top.a)) / alpha;
            mixed.a = alpha;
            return mixed;
        }
    }
}
