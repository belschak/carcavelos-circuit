using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // USS in Unity 6.3 has no linear-gradient, so gradients are tiny textures made at runtime (no asset file).
    // A 1x128 or 128x1 texture is stretched over the element; bilinear filtering smooths it.
    // The HUD makes them once in OnEnable and destroys them in OnDisable.
    public static class HudTextures
    {
        private const int Size = 128;

        // Colour stops evenly spaced from top to bottom.
        public static Texture2D VerticalGradient(params Color[] stops)
        {
            Texture2D texture = NewTexture(1, Size);
            for (int y = 0; y < Size; y++)
            {
                float t = 1f - y / (Size - 1f); // texture row 0 is the bottom, so t = 0 at the top
                texture.SetPixel(0, y, Sample(stops, t));
            }
            texture.Apply();
            return texture;
        }

        // Colour stops evenly spaced from left to right.
        public static Texture2D HorizontalGradient(params Color[] stops)
        {
            Texture2D texture = NewTexture(Size, 1);
            for (int x = 0; x < Size; x++)
            {
                texture.SetPixel(x, 0, Sample(stops, x / (Size - 1f)));
            }
            texture.Apply();
            return texture;
        }

        // Boost edge glow: strong at the screen edge, fading to nothing towards the middle (quadratic = very soft end).
        // edgeOnLeft = true for the glow on the left side of the screen, false for the right one.
        public static Texture2D EdgeGlow(Color color, float edgeAlpha, bool edgeOnLeft)
        {
            Texture2D texture = NewTexture(Size, 1);
            for (int x = 0; x < Size; x++)
            {
                float distance = edgeOnLeft ? x / (Size - 1f) : 1f - x / (Size - 1f); // 0 at the screen edge
                float fade = 1f - distance;
                texture.SetPixel(x, 0, HudColors.WithAlpha(color, edgeAlpha * fade * fade));
            }
            texture.Apply();
            return texture;
        }

        public static void Destroy(Texture2D texture)
        {
            if (texture != null)
            {
                Object.Destroy(texture);
            }
        }

        private static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
        }

        // t from 0 to 1 across all stops.
        private static Color Sample(Color[] stops, float t)
        {
            if (stops.Length == 1)
            {
                return stops[0];
            }
            float s = t * (stops.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(s), stops.Length - 2);
            return Color.Lerp(stops[i], stops[i + 1], s - i);
        }
    }
}
