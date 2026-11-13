using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // V21 (Luc's playtest of 13 Nov 2026): the Nova Kart wall, our own Mario-Kart-style track barrier.
    // A thin low wall with a bevelled top edge in 1 m blocks of Boost Orange and Kart White (the kerb colours).
    // WhiteGuardrailBuilder hands in the pieces (two points each) or whole lines; Build() joins pieces that touch
    // into one line, so the wall turns corners without gaps and the blocks run on evenly, and puts an orange
    // end post on every loose end. The colours come from one tiny texture (4 pixels: white, orange, orange, white):
    // the wall's UV runs along its length, so one material draws both colours and a 2 m piece needs only 10 triangles.
    public class NovaKartWall
    {
        // TUNING (metres).
        public const float Height = 0.8f;          // top of the wall above the road
        public const float Thickness = 0.18f;
        private const float Bevel = 0.04f;         // the top edges are cut off by this much
        private const float Block = 1f;            // length of one colour block
        private const float Foot = -0.15f;         // the wall starts a bit below the road so it never floats
        public const float EndPostSize = 0.26f;    // square end post, a little thicker and higher than the wall
        public const float EndPostHeight = 0.9f;
        public static readonly Vector2 OrangeUv = new Vector2(0.5f, 0.5f); // a spot of the texture that is orange

        private static readonly Color32 BoostOrange = new Color32(0xFF, 0x8A, 0x1F, 0xFF);
        private static readonly Color32 KartWhite = new Color32(0xF4, 0xF4, 0xF0, 0xFF);
        private const string MaterialPath = "Assets/_Project/Track/Materials/NovaKartWall.mat";
        private const string TexturePath = "Assets/_Project/Track/Textures/NovaKartWall.png";

        private readonly List<List<Vector3>> lines = new List<List<Vector3>>();

        // One piece of wall from a to b (foot points). A piece that starts where the last one ended continues its line.
        public void AddPiece(Vector3 a, Vector3 b)
        {
            if ((b - a).sqrMagnitude < 0.0001f)
            {
                return;
            }
            if (lines.Count > 0)
            {
                List<Vector3> last = lines[lines.Count - 1];
                if (Same(last[last.Count - 1], a))
                {
                    last.Add(b);
                    return;
                }
            }
            lines.Add(new List<Vector3> { a, b });
        }

        // A whole line of wall through these points.
        public void AddLine(Vector3[] points)
        {
            for (int k = 0; k + 1 < points.Length; k++)
            {
                AddPiece(points[k], points[k + 1]);
            }
        }

        // Adds all walls to the mesh. Returns their total length in metres.
        public float Build(NovaMesh mesh)
        {
            JoinLines();
            float metres = 0f;
            foreach (List<Vector3> line in lines)
            {
                metres += AddWall(mesh, line);
                int last = line.Count - 1;
                if (!Same(line[0], line[last])) // a closed ring has no loose ends
                {
                    AddEndPost(mesh, line[0], line[1] - line[0]);
                    AddEndPost(mesh, line[last], line[last] - line[last - 1]);
                }
            }
            return metres;
        }

        // Lines whose ends touch become one line (the pieces arrive in any order and direction).
        private void JoinLines()
        {
            bool joined = true;
            while (joined)
            {
                joined = false;
                for (int a = 0; a < lines.Count && !joined; a++)
                {
                    for (int b = a + 1; b < lines.Count && !joined; b++)
                    {
                        List<Vector3> first = lines[a];
                        List<Vector3> second = lines[b];
                        if (Same(first[first.Count - 1], second[second.Count - 1]))
                        {
                            second.Reverse(); // end meets end
                        }
                        else if (Same(first[0], second[0]))
                        {
                            first.Reverse();  // start meets start
                        }
                        else if (Same(first[0], second[second.Count - 1]))
                        {
                            lines[a] = second; // the second line comes first
                            lines[b] = first;
                            first = lines[a];
                            second = lines[b];
                        }
                        if (Same(first[first.Count - 1], second[0]))
                        {
                            first.AddRange(second.GetRange(1, second.Count - 1));
                            lines.RemoveAt(b);
                            joined = true;
                        }
                    }
                }
            }
        }

        // The wall along one line: the same cross-section at every point, turned so that a bend closes without a gap.
        private static float AddWall(NovaMesh mesh, List<Vector3> line)
        {
            int count = line.Count;
            Vector3[] sideways = new Vector3[count]; // to the right at each point, longer in a bend so the wall keeps its thickness
            for (int k = 0; k < count; k++)
            {
                Vector3 before = k > 0 ? Flat(line[k] - line[k - 1]).normalized : Vector3.zero;
                Vector3 after = k + 1 < count ? Flat(line[k + 1] - line[k]).normalized : Vector3.zero;
                Vector3 direction = (before + after).normalized;
                if (direction.sqrMagnitude < 0.5f)
                {
                    direction = after.sqrMagnitude > 0.5f ? after : before; // the line turns straight back
                }
                float cos = k > 0 && k + 1 < count ? Mathf.Max(0.5f, Vector3.Dot(direction, after)) : 1f;
                sideways[k] = Vector3.Cross(Vector3.up, direction) / cos;
            }

            // Cross-section seen from behind, going round from the right foot over the top to the left foot.
            float half = Thickness * 0.5f;
            Vector2[] section =
            {
                new Vector2(half, Foot), new Vector2(half, Height - Bevel), new Vector2(half - Bevel, Height),
                new Vector2(-half + Bevel, Height), new Vector2(-half, Height - Bevel), new Vector2(-half, Foot)
            };
            float along = 0f; // metres from the start of the line: decides the colour
            for (int k = 0; k + 1 < count; k++)
            {
                float length = Vector3.Distance(line[k], line[k + 1]);
                Vector2 uvStart = BlockUv(along);
                Vector2 uvEnd = BlockUv(along + length);
                for (int s = 0; s + 1 < section.Length; s++)
                {
                    Vector3 a0 = line[k] + sideways[k] * section[s].x + Vector3.up * section[s].y;
                    Vector3 a1 = line[k] + sideways[k] * section[s + 1].x + Vector3.up * section[s + 1].y;
                    Vector3 b0 = line[k + 1] + sideways[k + 1] * section[s].x + Vector3.up * section[s].y;
                    Vector3 b1 = line[k + 1] + sideways[k + 1] * section[s + 1].x + Vector3.up * section[s + 1].y;
                    mesh.Quad(a0, a1, b1, b0, uvStart, uvStart, uvEnd, uvEnd);
                }
                along += length;
            }
            return along;
        }

        // The texture repeats every two blocks: its middle half is orange, the rest white.
        private static Vector2 BlockUv(float along)
        {
            return new Vector2(along / (2f * Block) + 0.25f, 0.5f);
        }

        // An orange post on a loose end of the wall, turned like the wall ("direction" points along it).
        private static void AddEndPost(NovaMesh mesh, Vector3 foot, Vector3 direction)
        {
            Vector3 forward = Flat(direction).sqrMagnitude > 0.000001f ? Flat(direction).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float half = EndPostSize * 0.5f;
            Vector3[] low = new Vector3[4];
            Vector3[] high = new Vector3[4];
            Vector3[] corner = { right - forward, right + forward, -right + forward, -right - forward }; // going round, seen from above
            for (int k = 0; k < 4; k++)
            {
                low[k] = foot + corner[k] * half + Vector3.up * Foot;
                high[k] = foot + corner[k] * half + Vector3.up * EndPostHeight;
            }
            for (int k = 0; k < 4; k++)
            {
                int next = (k + 1) % 4;
                mesh.Quad(low[k], high[k], high[next], low[next], OrangeUv, OrangeUv, OrangeUv, OrangeUv);
            }
            mesh.Quad(high[0], high[3], high[2], high[1], OrangeUv, OrangeUv, OrangeUv, OrangeUv); // top
        }

        // Created once, then only updated, so its GUID stays the same.
        public static Material Material()
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            m.SetTexture("_BaseMap", StripeTexture());
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.3f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // 4 x 4 pixels: white, orange, orange, white. Sharp pixels (no blending), so the blocks have clean edges.
        private static Texture2D StripeTexture()
        {
            Texture2D pixels = new Texture2D(4, 4, TextureFormat.RGB24, false);
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    pixels.SetPixel(x, y, x == 1 || x == 2 ? BoostOrange : KartWhite);
                }
            }
            File.WriteAllBytes(TexturePath, pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            AssetDatabase.ImportAsset(TexturePath);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static bool Same(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 0.0001f; // 1 cm
        }

        private static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }
    }
}
