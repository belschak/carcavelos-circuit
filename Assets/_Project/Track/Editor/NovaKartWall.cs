using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // V21 (Luc's playtest of 13 Nov 2026): the Nova Kart wall, our own Mario-Kart-style track barrier.
    // V22w (Luc's reference model "w1"): it now looks like a moulded road barrier: a wide white foot, a slanted
    // Boost Orange stripe above it, a slimmer white body with a rounded top, and a joint groove every 2 m.
    // WhiteGuardrailBuilder hands in the pieces (two points each) or whole lines; Build() joins pieces that touch
    // into one line and pushes the same outline along it, so the wall turns corners without gaps. Every loose end
    // gets an end piece with cut-off corners (also used by FenceFreeZones where it cuts the wall).
    // One material, one 128 px texture: its lower half is white, its upper half orange, and a dark column at the
    // left and right edge draws the groove (the UV runs along the wall, once every 2 m). 30 triangles per piece.
    public class NovaKartWall
    {
        // TUNING (metres).
        public const float Height = 0.8f;          // top of the wall above the road
        public const float Thickness = 0.3f;       // at the foot: as thick as the invisible collision wall
        private const float Foot = -0.15f;         // the wall starts a bit below the road so it never floats
        private const float Module = 2f;           // a joint groove every so many metres
        private const float CapLength = 0.10f;     // the end piece reaches this far beyond the end of the wall ...
        private const float CapInside = 0.12f;     // ... and this far back over it
        private const float CapGrow = 0.012f;      // it is this much bigger than the wall all round, like a sleeve
        private const float CapCut = 0.03f;        // its corners at the very end are cut off by this much

        // Right half of the outline, seen from behind, foot to top: x = metres from the middle, y = height.
        // (tools/props/make_track_props.py has a copy of these numbers for its comparison picture.)
        private static readonly Vector2[] RightHalf =
        {
            new Vector2(0.150f, Foot),   // foot
            new Vector2(0.150f, 0.11f),  // top of the foot: the orange stripe starts
            new Vector2(0.135f, 0.17f),  // rounded shoulder
            new Vector2(0.085f, 0.235f), // slanted face, like a road barrier
            new Vector2(0.080f, 0.27f),  // the stripe ends, the white body starts
            new Vector2(0.080f, 0.74f),
            new Vector2(0.062f, 0.782f), // rounded top edge
            new Vector2(0.030f, Height)
        };
        private const int FirstOrange = 1, LastOrange = 3; // the faces of the right half that are orange

        private static readonly Color32 BoostOrange = new Color32(0xFF, 0x8A, 0x1F, 0xFF);
        private static readonly Color32 KartWhite = new Color32(0xF4, 0xF4, 0xF0, 0xFF);
        private const int TextureSize = 128;
        private const float WhiteV = 0.25f, OrangeV = 0.75f; // where the two colours lie in the texture
        private const float PlainU = 0.5f;                   // a spot far away from the groove
        private const string MaterialPath = "Assets/_Project/Track/Materials/NovaKartWall.mat";
        private const string TexturePath = "Assets/_Project/Track/Textures/NovaKartWall.png";

        private static Vector2[] outline;       // the whole outline: right foot, over the top, left foot
        private static Vector2[] outlineNormal; // which way the surface looks at each of its points

        private readonly List<List<Vector3>> lines = new List<List<Vector3>>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public int VertexCount
        {
            get { return vertices.Count; }
        }

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

        // Builds all walls. Returns their total length in metres. Afterwards ToMesh() gives the result.
        public float Build()
        {
            JoinLines();
            float metres = 0f;
            foreach (List<Vector3> line in lines)
            {
                int last = line.Count - 1;
                bool ring = Same(line[0], line[last]);
                metres += AddWall(line, ring);
                if (!ring) // a closed ring has no loose ends
                {
                    AddEndCap(vertices, normals, uvs, triangles, line[0], line[0] - line[1]);
                    AddEndCap(vertices, normals, uvs, triangles, line[last], line[last] - line[last - 1]);
                }
            }
            return metres;
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
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

        // The wall along one line: the same outline at every point, turned so that a bend closes without a gap.
        private float AddWall(List<Vector3> line, bool ring)
        {
            MakeOutline();
            int count = line.Count;
            int[] start = new int[count]; // first vertex of the outline at each point of the line
            float along = 0f;             // metres from the start of the line: decides where the grooves are
            for (int k = 0; k < count; k++)
            {
                Vector3 before = k > 0 ? Flat(line[k] - line[k - 1]).normalized : Vector3.zero;
                Vector3 after = k + 1 < count ? Flat(line[k + 1] - line[k]).normalized : Vector3.zero;
                if (ring && k == 0)
                {
                    before = Flat(line[count - 1] - line[count - 2]).normalized; // a ring bends at its seam too
                }
                if (ring && k == count - 1)
                {
                    after = Flat(line[1] - line[0]).normalized;
                }
                Vector3 direction = (before + after).normalized;
                if (direction.sqrMagnitude < 0.5f)
                {
                    direction = after.sqrMagnitude > 0.5f ? after : before; // the line turns straight back
                }
                bool bend = before.sqrMagnitude > 0.5f && after.sqrMagnitude > 0.5f;
                float cos = bend ? Mathf.Max(0.5f, Vector3.Dot(direction, after)) : 1f;
                Vector3 right = Vector3.Cross(Vector3.up, direction);
                if (k > 0)
                {
                    along += Vector3.Distance(line[k - 1], line[k]);
                }
                // In a bend the outline is wider by 1 / cos, so the wall keeps its thickness on both sides of it.
                start[k] = AddOutline(vertices, normals, uvs, line[k], right, right / cos, 0f, along / Module);
            }
            for (int k = 0; k + 1 < count; k++)
            {
                AddSkin(triangles, start[k], start[k + 1]);
            }
            return along;
        }

        // An end piece on a loose end of the wall: a sleeve a little bigger than the wall with cut-off corners.
        // foot = the end point of the wall's line, outward = the direction the wall would go on in.
        // It writes into the given lists, so FenceFreeZones can use it on its trimmed copy of the mesh.
        public static void AddEndCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
                                     Vector3 foot, Vector3 outward)
        {
            MakeOutline();
            Vector3 forward = Flat(outward).sqrMagnitude > 0.000001f ? Flat(outward).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, -forward); // the wall runs towards the cap's back
            Vector3 back = foot - forward * CapInside;
            Vector3 shoulder = foot + forward * (CapLength - CapCut);
            Vector3 tip = foot + forward * CapLength;

            // The step from the wall's skin up to the sleeve (looks back along the wall).
            int onWall = AddOutline(vertices, normals, uvs, back, right, right, 0f, PlainU, -forward);
            int step = AddOutline(vertices, normals, uvs, back, right, right, CapGrow, PlainU, -forward);
            AddSkin(triangles, step, onWall);
            // The sleeve, then its cut-off corners.
            int sleeveStart = AddOutline(vertices, normals, uvs, back, right, right, CapGrow, PlainU);
            int sleeveEnd = AddOutline(vertices, normals, uvs, shoulder, right, right, CapGrow, PlainU);
            AddSkin(triangles, sleeveEnd, sleeveStart);
            int cutStart = AddOutline(vertices, normals, uvs, shoulder, right, right, CapGrow, PlainU, forward, 0.5f);
            int cutEnd = AddOutline(vertices, normals, uvs, tip, right, right, CapGrow - CapCut, PlainU, forward, 0.5f);
            AddSkin(triangles, cutEnd, cutStart);
            // The flat end: strips from the right half of the outline across to the left half, so the stripe goes round.
            int end = AddOutline(vertices, normals, uvs, tip, right, right, CapGrow - CapCut, PlainU, forward);
            int n = outline.Length;
            for (int s = 0; s + 1 < n / 2; s++)
            {
                int a = end + 2 * s, b = a + 1;           // the two ends of face s on the right ...
                int c = end + 2 * (n - 2 - s), d = c + 1; // ... and of its mirror face on the left
                AddQuad(triangles, vertices, a, b, c, d, forward);
            }
        }

        // Puts the outline at "centre": two vertices per face (start and end), because faces of different colour
        // share a corner but not its UV. Returns the index of the first vertex.
        // across = unit vector to the right (for the normals), wide = the same, longer in a bend (for the positions).
        // grow = the outline moved outwards by so much. u = position along the texture.
        // flat = if not zero, all normals point this way (for flat end faces); mix = how much of it (1 = all).
        private static int AddOutline(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, Vector3 centre,
                                      Vector3 across, Vector3 wide, float grow, float u,
                                      Vector3 flat = default(Vector3), float mix = 1f)
        {
            int first = vertices.Count;
            int n = outline.Length;
            for (int s = 0; s + 1 < n; s++)
            {
                float v = IsOrange(s) ? OrangeV : WhiteV;
                for (int end = s; end <= s + 1; end++)
                {
                    Vector2 p = outline[end];
                    float x = p.x + Mathf.Sign(p.x) * grow;
                    float y = p.y > 0f ? p.y + grow : p.y; // the foot stays in the ground
                    vertices.Add(centre + wide * x + Vector3.up * y);
                    Vector3 normal = across * outlineNormal[end].x + Vector3.up * outlineNormal[end].y;
                    if (flat != Vector3.zero)
                    {
                        normal = Vector3.Lerp(normal, flat, mix).normalized;
                    }
                    normals.Add(normal);
                    uvs.Add(new Vector2(u, v));
                }
            }
            return first;
        }

        // The faces between two outlines (from = earlier along the wall, to = later).
        private static void AddSkin(List<int> triangles, int from, int to)
        {
            int faces = outline.Length - 1;
            for (int s = 0; s < faces; s++)
            {
                int a0 = from + 2 * s, a1 = a0 + 1, b0 = to + 2 * s, b1 = b0 + 1;
                triangles.Add(a0); triangles.Add(a1); triangles.Add(b1); // clockwise seen from outside
                triangles.Add(a0); triangles.Add(b1); triangles.Add(b0);
            }
        }

        // A four-corner face a, b, c, d (going round) that must be visible from the side "looks" points to.
        private static void AddQuad(List<int> triangles, List<Vector3> vertices, int a, int b, int c, int d, Vector3 looks)
        {
            Vector3 front = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(front, looks) < 0f)
            {
                int swap = b;
                b = d;
                d = swap;
            }
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private static bool IsOrange(int face)
        {
            int mirror = outline.Length - 2 - face; // the same face on the left half
            return (face >= FirstOrange && face <= LastOrange) || (mirror >= FirstOrange && mirror <= LastOrange);
        }

        // The whole outline from its right half, and the direction the surface looks at each point: the average of
        // the two faces that meet there, the longer face counting more. So the big faces stay flat and only the
        // small ones (shoulder, top edge) are shaded round.
        private static void MakeOutline()
        {
            if (outline != null)
            {
                return;
            }
            int half = RightHalf.Length;
            outline = new Vector2[half * 2];
            for (int k = 0; k < half; k++)
            {
                outline[k] = RightHalf[k];
                outline[half * 2 - 1 - k] = new Vector2(-RightHalf[k].x, RightHalf[k].y);
            }
            int n = outline.Length;
            outlineNormal = new Vector2[n];
            for (int k = 0; k < n; k++)
            {
                Vector2 sum = Vector2.zero;
                if (k > 0)
                {
                    sum += FaceNormal(outline[k - 1], outline[k]) * Vector2.Distance(outline[k - 1], outline[k]);
                }
                if (k + 1 < n)
                {
                    sum += FaceNormal(outline[k], outline[k + 1]) * Vector2.Distance(outline[k], outline[k + 1]);
                }
                outlineNormal[k] = sum.normalized;
            }
        }

        // Outward direction of the face from p to q (the outline goes up on the right and down on the left).
        private static Vector2 FaceNormal(Vector2 p, Vector2 q)
        {
            Vector2 d = (q - p).normalized;
            return new Vector2(d.y, -d.x);
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

        // 128 x 128 pixels: lower half white, upper half orange, the two outer columns darker (the joint groove:
        // the texture repeats along the wall, so its left and right edge meet and make one 3 cm line every 2 m).
        private static Texture2D StripeTexture()
        {
            Texture2D pixels = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, false);
            for (int x = 0; x < TextureSize; x++)
            {
                bool groove = x == 0 || x == TextureSize - 1;
                for (int y = 0; y < TextureSize; y++)
                {
                    Color colour = y < TextureSize / 2 ? (Color)KartWhite : (Color)BoostOrange;
                    pixels.SetPixel(x, y, groove ? colour * 0.5f : colour);
                }
            }
            File.WriteAllBytes(TexturePath, pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            AssetDatabase.ImportAsset(TexturePath);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;                 // the wall is mostly seen at a flat angle
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;           // far away the grooves fade out and do not flicker
            importer.maxTextureSize = TextureSize;
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
