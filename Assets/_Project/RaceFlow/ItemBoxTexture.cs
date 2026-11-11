using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The face of an item box (task R7): a Portuguese azulejo tile, cobalt blue on warm white, drawn pixel by pixel
    // in code once per race (own work, no image file). Every cube face shows one whole tile.
    // Pattern: a thick frame, a diamond through the edge midpoints, a ring with a dot in the middle and quarter
    // circles in the corners, the same shape language as the HUD's AzulejoPattern.
    public static class ItemBoxTexture
    {
        private const int Size = 128;
        private static readonly Color Tile = new Color(0.97f, 0.95f, 0.90f, 1f);  // warm glazed white
        private static readonly Color Cobalt = new Color(0.10f, 0.30f, 0.66f, 1f); // azulejo blue
        private static readonly Color Sky = new Color(0.18f, 0.55f, 0.84f, 1f);    // Atlantic Blue (HudColors.Blue)

        public static Texture2D Create()
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            texture.name = "ItemBoxAzulejo";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            Color[] pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = ColourAt((x + 0.5f) / Size, (y + 0.5f) / Size);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true); // mipmaps, then free the CPU copy (we never read it again)
            return texture;
        }

        // u, v from 0 to 1 across one face.
        private static Color ColourAt(float u, float v)
        {
            // Thick cobalt frame around the face.
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (edge < 0.07f)
            {
                return Cobalt;
            }
            float cx = u - 0.5f;
            float cy = v - 0.5f;
            float centre = Mathf.Sqrt(cx * cx + cy * cy);

            // Dot and ring in the middle.
            if (centre < 0.07f)
            {
                return Cobalt;
            }
            if (centre > 0.15f && centre < 0.2f)
            {
                return Sky;
            }

            // Diamond through the edge midpoints: |x| + |y| = 0.5, drawn as a band.
            float diamond = Mathf.Abs(cx) + Mathf.Abs(cy);
            if (diamond > 0.40f && diamond < 0.46f)
            {
                return Cobalt;
            }

            // Quarter circles in the four corners (inside the frame).
            float cornerX = Mathf.Min(u, 1f - u);
            float cornerY = Mathf.Min(v, 1f - v);
            float corner = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
            if (corner > 0.17f && corner < 0.22f)
            {
                return Sky;
            }

            // Four petals between the ring and the diamond (on the diagonals), the tile's "flower".
            float petal = Mathf.Abs(Mathf.Abs(cx) - Mathf.Abs(cy));
            if (petal < 0.035f && centre > 0.2f && diamond < 0.38f)
            {
                return Cobalt;
            }
            return Tile;
        }
    }
}
