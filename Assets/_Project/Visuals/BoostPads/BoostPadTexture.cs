using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // The boost pad picture (task V11), drawn in code: yellow and orange chevrons pointing forward (texture "up"),
    // with a pale rim on both sides so the pad stands out on the beige tunnel floor. Two chevrons per tile; the
    // texture repeats, so scrolling the offset makes them run.
    public static class BoostPadTexture
    {
        private const int Size = 128;
        private static readonly Color Yellow = new Color(1f, 0.84f, 0.2f, 1f);
        private static readonly Color Orange = new Color(1f, 0.45f, 0.08f, 1f);
        private static readonly Color Rim = new Color(1f, 0.97f, 0.85f, 1f);

        public static Texture2D Create()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            texture.name = "BoostPad chevrons";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = ColourAt((x + 0.5f) / Size, (y + 0.5f) / Size);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        // u across the pad (0 = left, 1 = right), v along it (1 = forward).
        public static Color ColourAt(float u, float v)
        {
            float side = Mathf.Abs(u - 0.5f);
            if (side > 0.44f)
            {
                return Rim;
            }
            // A "^" shape: the further from the middle, the further back the stripe starts.
            float stripe = Mathf.Repeat(v * 2f + side * 1.4f, 1f);
            return stripe < 0.5f ? Orange : Yellow;
        }
    }
}
