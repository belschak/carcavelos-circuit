using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Draws the track's textures in code (no photos: the repo is public and photos may show people).
    // Each texture is saved as a PNG in Track/Textures/Nova and imported by Unity, so it gets a normal .meta file.
    public static class NovaTextures
    {
        public const string Folder = "Assets/_Project/Track/Textures/Nova/";

        // Campus wing facade (video IMG_7723): per floor a white slab band, then a full-height band of light
        // silver-grey glass (it mirrors the overcast sky) in a dense near-black frame grid: 2 m panes, two transoms,
        // a heavier double mullion every 4 m. One tile = 4 m wide, 4 m tall = one floor.
        public static Texture2D Facade(Color wall, Color glass, Color frame, string name = "NovaFacade")
        {
            int size = 256;
            Texture2D t = NewTexture(size);
            for (int y = 0; y < size; y++)
            {
                float metres = y / (float)size * 4f;
                bool band = metres > 0.75f && metres < 3.95f; // white slab 0.8 m, glass 3.2 m
                for (int x = 0; x < size; x++)
                {
                    float mx = x / (float)size * 4f;
                    Color c = wall;
                    if (band)
                    {
                        float g = metres - 0.75f;
                        bool heavy = mx < 0.07f || mx > 3.93f;
                        bool mullion = heavy || Mathf.Abs(mx - 2f) < 0.035f;
                        bool transom = Mathf.Abs(g - 1.07f) < 0.03f || Mathf.Abs(g - 2.13f) < 0.03f || g < 0.05f || g > 3.15f;
                        // Lighter towards the top of each pane (sky reflection), a touch darker in the lowest row.
                        float row = (g % 1.067f) / 1.067f;
                        float sheen = Mathf.Lerp(0.9f, 1.08f, row) * (g < 1.07f ? 0.94f : 1f);
                        c = mullion || transom ? frame : glass * sheen;
                        c.a = 1f;
                    }
                    else if (metres > 0.7f && metres <= 0.75f)
                    {
                        c = wall * 0.82f; // shadow line under the slab nose
                        c.a = 1f;
                    }
                    t.SetPixel(x, y, c);
                }
            }
            return Save(t, name);
        }

        // Light concrete with expansion joints every 2 m (one tile = 4 m) and a little noise.
        public static Texture2D Concrete(Color baseColour)
        {
            int size = 256;
            Texture2D t = NewTexture(size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.08f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.04f;
                    Color c = baseColour * (0.96f + n);
                    if (x % 128 < 2 || y % 128 < 2)
                    {
                        c = baseColour * 0.82f;
                    }
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            }
            return Save(t, "NovaConcrete");
        }

        // Portuguese pavement (calcada): irregular light stones with dark joints, made with a Voronoi pattern.
        public static Texture2D Calcada(Color stone, Color joint, string name = "NovaCalcada")
        {
            int size = 256;
            int cells = 12;
            Random.State saved = Random.state;
            Random.InitState(7);
            Vector2[] seeds = new Vector2[cells * cells];
            float[] shade = new float[seeds.Length];
            for (int i = 0; i < seeds.Length; i++)
            {
                seeds[i] = new Vector2(((i % cells) + Random.value) / cells, ((i / cells) + Random.value) / cells);
                shade[i] = Random.Range(0.88f, 1.08f);
            }
            Random.state = saved;
            Texture2D t = NewTexture(size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x / (float)size, y / (float)size);
                    float best = 9f;
                    float second = 9f;
                    int bestIndex = 0;
                    for (int i = 0; i < seeds.Length; i++)
                    {
                        Vector2 d = p - seeds[i];
                        d.x -= Mathf.Round(d.x); // wrap around so the texture tiles without seams
                        d.y -= Mathf.Round(d.y);
                        float dist = d.sqrMagnitude;
                        if (dist < best)
                        {
                            second = best;
                            best = dist;
                            bestIndex = i;
                        }
                        else if (dist < second)
                        {
                            second = dist;
                        }
                    }
                    bool edge = Mathf.Sqrt(second) - Mathf.Sqrt(best) < 0.010f;
                    Color c = edge ? joint : stone * shade[bestIndex];
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            }
            return Save(t, name);
        }

        // An ORIGINAL mural for the tunnel walls in the spirit of the real one (flat bold shapes, grids, stars).
        // Never a copy of the real artwork. One tile = 16 m long, 4 m tall.
        public static Texture2D Mural(Color[] colours, Color background)
        {
            int w = 1024;
            int h = 256;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = background;
            }
            Random.State saved = Random.state;
            Random.InitState(2026);
            for (int block = 0; block < 8; block++)
            {
                // Full-height background blocks first, so little of the wall stays white.
                Color bc = colours[Random.Range(0, colours.Length - 1)];
                int x0 = block * w / 8;
                for (int y = 0; y < h; y++)
                {
                    for (int x = x0; x < x0 + w / 8; x++)
                    {
                        pixels[y * w + x] = bc;
                    }
                }
            }
            for (int shape = 0; shape < 30; shape++)
            {
                Color c = colours[Random.Range(0, colours.Length)];
                int cx = Random.Range(0, w);
                int cy = Random.Range(0, h);
                int r = Random.Range(45, 140);
                int kind = Random.Range(0, 4);
                for (int y = cy - r; y <= cy + r; y++)
                {
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        int px = (x + w) % w; // wrap horizontally so the wall tiles
                        if (y < 0 || y >= h)
                        {
                            continue;
                        }
                        float dx = x - cx;
                        float dy = y - cy;
                        bool hit = false;
                        if (kind == 0)
                        {
                            hit = dx * dx + dy * dy <= r * r; // disc
                        }
                        else if (kind == 1)
                        {
                            hit = Mathf.Abs(dx) <= r && Mathf.Abs(dy) <= r * 0.6f; // block
                        }
                        else if (kind == 2)
                        {
                            hit = dy >= -r && dy <= r && Mathf.Abs(dx) <= (r - dy) * 0.5f; // triangle
                        }
                        else
                        {
                            hit = Mathf.Abs(dx) <= r && Mathf.Abs(dy) <= r && ((int)(dx + r) % 16 < 3 || (int)(dy + r) % 16 < 3); // grid
                        }
                        if (hit)
                        {
                            pixels[y * w + px] = c;
                        }
                    }
                }
            }
            Random.state = saved;
            for (int y = h - 26; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = new Color(0.71f, 0.67f, 0.60f); // plain concrete band under the ceiling
                }
            }
            t.SetPixels(pixels);
            t.Apply();
            return Save(t, "NovaMural");
        }

        // Overcast Atlantic sky for Skybox/Panoramic (lat-long): pale grey-blue zenith, warm streak at the horizon.
        public static Texture2D Sky()
        {
            int w = 16;
            int h = 512;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color zenith = NovaLook.Hex("#AEBDD0");
            Color mid = NovaLook.Hex("#BCC8D6");
            Color horizon = NovaLook.Hex("#D6DCE1");
            Color warm = NovaLook.Hex("#E4D6BE");
            Color below = NovaLook.Hex("#9CA3A8");
            for (int y = 0; y < h; y++)
            {
                float elevation = (y / (float)(h - 1) - 0.5f) * 180f; // degrees above the horizon
                Color c;
                if (elevation < 0f)
                {
                    c = Color.Lerp(horizon, below, Mathf.Clamp01(-elevation / 10f));
                }
                else if (elevation < 3f)
                {
                    c = Color.Lerp(warm, horizon, elevation / 3f);
                }
                else if (elevation < 54f)
                {
                    c = Color.Lerp(horizon, mid, (elevation - 3f) / 51f);
                }
                else
                {
                    c = Color.Lerp(mid, zenith, (elevation - 54f) / 36f);
                }
                c.a = 1f;
                for (int x = 0; x < w; x++)
                {
                    t.SetPixel(x, y, c);
                }
            }
            return Save(t, "NovaSky", TextureWrapMode.Clamp);
        }

        // Off-campus blocks: off-white wall with punched windows (one tile = 4 m x 4 m, one window per tile).
        public static Texture2D Punched(Color wall, Color window, Color frame)
        {
            int size = 256;
            Texture2D t = NewTexture(size);
            for (int y = 0; y < size; y++)
            {
                float my = y / (float)size * 4f;
                for (int x = 0; x < size; x++)
                {
                    float mx = x / (float)size * 4f;
                    bool inX = Mathf.Abs(mx - 2f) < 0.8f;
                    bool inY = my > 1.1f && my < 3.0f;
                    bool frameX = Mathf.Abs(Mathf.Abs(mx - 2f) - 0.8f) < 0.06f && my > 1.04f && my < 3.06f;
                    bool frameY = (Mathf.Abs(my - 1.1f) < 0.06f || Mathf.Abs(my - 3.0f) < 0.06f) && Mathf.Abs(mx - 2f) < 0.86f;
                    float n = Mathf.PerlinNoise(x * 0.07f, y * 0.07f) * 0.04f;
                    Color c = frameX || frameY ? frame : (inX && inY ? window * Mathf.Lerp(0.9f, 1.15f, (my - 1.1f) / 1.9f) : wall * (0.97f + n));
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            }
            return Save(t, "NovaPunched");
        }

        // Dull November lawn: two greens blended with seamless noise plus a few dry patches (one tile = 8 m).
        public static Texture2D Grass(Color light, Color dark, Color dry)
        {
            int size = 256;
            Texture2D t = NewTexture(size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float blend = Seamless(x, y, size, 0.03f, 0f);
                    float patch = Seamless(x, y, size, 0.05f, 37f);
                    float fine = Seamless(x, y, size, 0.4f, 91f) * 0.08f;
                    Color c = Color.Lerp(dark, light, blend);
                    if (patch > 0.62f)
                    {
                        c = Color.Lerp(c, dry, Mathf.Clamp01((patch - 0.62f) * 5f));
                    }
                    c *= 0.96f + fine;
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            }
            return Save(t, "NovaGrass");
        }

        // Beach promenade: pale concrete slabs (joints every 2 m) crossed by one band of small dark setts (one tile = 4 m).
        public static Texture2D Promenade(Color slab, Color sett)
        {
            int size = 256;
            Texture2D t = NewTexture(size);
            for (int y = 0; y < size; y++)
            {
                float my = y / (float)size * 4f;
                for (int x = 0; x < size; x++)
                {
                    float mx = x / (float)size * 4f;
                    Color c;
                    if (my > 3.2f && my < 3.8f)
                    {
                        int cx = (int)(mx / 0.08f);
                        int cy = (int)(my / 0.08f);
                        float jitter = 0.9f + ((cx * 73 + cy * 37) % 16) / 100f;
                        bool joint = (mx % 0.08f) < 0.012f || (my % 0.08f) < 0.012f;
                        c = joint ? sett * 0.7f : sett * jitter;
                    }
                    else
                    {
                        float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.06f;
                        c = (x % 128 < 1 || y % 128 < 1) ? slab * 0.85f : slab * (0.97f + n);
                    }
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            }
            return Save(t, "NovaPromenadeSlabs");
        }

        // Perlin noise that repeats every "size" pixels (four samples blended across the tile edges).
        private static float Seamless(int x, int y, int size, float scale, float offset)
        {
            float u = x / (float)size;
            float v = y / (float)size;
            float a = Mathf.PerlinNoise(offset + x * scale, offset + y * scale);
            float b = Mathf.PerlinNoise(offset + (x - size) * scale, offset + y * scale);
            float c = Mathf.PerlinNoise(offset + x * scale, offset + (y - size) * scale);
            float d = Mathf.PerlinNoise(offset + (x - size) * scale, offset + (y - size) * scale);
            return (a * (1f - u) + b * u) * (1f - v) + (c * (1f - u) + d * u) * v;
        }

        // Start/finish chequer strip: 8 x 2 squares.
        public static Texture2D Chequer()
        {
            int size = 64;
            Texture2D t = NewTexture(size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool dark = ((x / 8) + (y / 32)) % 2 == 0;
                    t.SetPixel(x, y, dark ? new Color(0.1f, 0.1f, 0.12f) : Color.white);
                }
            }
            return Save(t, "NovaChequer");
        }

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, true);
        }

        // Writes the PNG, lets Unity import it (repeat wrap, mipmaps) and returns the imported asset.
        private static Texture2D Save(Texture2D texture, string name)
        {
            return Save(texture, name, TextureWrapMode.Repeat);
        }

        private static Texture2D Save(Texture2D texture, string name, TextureWrapMode wrap)
        {
            texture.Apply();
            string path = Folder + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
