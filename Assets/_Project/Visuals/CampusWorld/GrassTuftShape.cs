using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // The shape of one grass tuft (task V39), as plain arrays so Edit Mode tests can check it. Until V37 every tuft was
    // baked into big cell meshes (5.3 MB for 5 842 tufts); now the builder makes a few of these prototypes and
    // GrassField draws each tuft as an instance of one of them (position, turn, size and lawn colour per instance).
    // A tuft = a few crossed cards (two or three upright quads round one foot), like the V16 cell tufts:
    //   small = 3 crosses of 2 cards (12 triangles), big = 4 crosses of 3 cards (24), far = 1 cross of 2 cards (4).
    // Unit size (scale 1), foot at the origin, the kart's up = +y. Vertex colour: rgb white (the instance tints it),
    // alpha = height of the blade tip (the Foliage shader's sway weight); UV1 = sway (0 at the root, 1 at the tip).
    public static class GrassTuftShape
    {
        public enum Kind { Small, Big, Far }

        public struct Data
        {
            public List<Vector3> Vertices;
            public List<Vector3> Normals;
            public List<Vector2> Uv;
            public List<Vector2> Sway;
            public List<Color> Colours;
            public List<int> Triangles;
        }

        public static int Crosses(Kind kind)
        {
            return kind == Kind.Far ? 1 : kind == Kind.Big ? 4 : 3;
        }

        public static int Cards(Kind kind)
        {
            return kind == Kind.Big ? 3 : 2;
        }

        // One prototype; seed picks its random shape (cross offsets, heights, which half of the texture).
        public static Data Build(Kind kind, int seed)
        {
            var rng = new System.Random(7919 * seed + 17 + (int)kind * 104729);
            float R() => (float)rng.NextDouble();
            var d = new Data
            {
                Vertices = new List<Vector3>(), Normals = new List<Vector3>(), Uv = new List<Vector2>(),
                Sway = new List<Vector2>(), Colours = new List<Color>(), Triangles = new List<int>(),
            };
            int crosses = Crosses(kind);
            int cards = Cards(kind);
            for (int cross = 0; cross < crosses; cross++)
            {
                float away = cross == 0 ? 0f : 0.1f + 0.14f * R();
                float around = (cross * 120f + 60f * R()) * Mathf.Deg2Rad;
                Vector3 foot = new Vector3(Mathf.Cos(around), 0f, Mathf.Sin(around)) * away;
                float height = 0.15f + 0.12f * R();
                float half = 0.15f + 0.07f * R();
                float turn = R() * Mathf.PI;
                float halfU = R() < 0.5f ? 0f : 0.5f;            // which of the two tufts in the texture
                for (int card = 0; card < cards; card++)
                {
                    float a = turn + card * Mathf.PI / cards;
                    Vector3 side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * half;
                    Vector3 lean = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * (0.06f * (R() - 0.5f));
                    int n = d.Vertices.Count;
                    d.Vertices.Add(foot - side + Vector3.down * 0.04f);
                    d.Vertices.Add(foot + side + Vector3.down * 0.04f);
                    d.Vertices.Add(foot + side + lean + Vector3.up * height);
                    d.Vertices.Add(foot - side + lean + Vector3.up * height);
                    d.Uv.Add(new Vector2(halfU, 0f));
                    d.Uv.Add(new Vector2(halfU + 0.5f, 0f));
                    d.Uv.Add(new Vector2(halfU + 0.5f, 1f));
                    d.Uv.Add(new Vector2(halfU, 1f));
                    d.Sway.Add(Vector2.zero);
                    d.Sway.Add(Vector2.zero);
                    d.Sway.Add(new Vector2(0f, 1f));
                    d.Sway.Add(new Vector2(0f, 1f));
                    for (int k = 0; k < 4; k++)
                    {
                        d.Normals.Add(Vector3.up);                         // lit like the lawn under it
                        d.Colours.Add(new Color(1f, 1f, 1f, height + 0.04f));
                    }
                    d.Triangles.AddRange(new[] { n, n + 2, n + 1, n, n + 3, n + 2 });
                }
            }
            return d;
        }

        public static Mesh ToMesh(Data d, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(d.Vertices);
            mesh.SetNormals(d.Normals);
            mesh.SetUVs(0, d.Uv);
            mesh.SetUVs(1, d.Sway);
            mesh.SetColors(d.Colours);
            mesh.SetTriangles(d.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Which prototype a tuft uses: a fixed hash of its number, so a rebuild keeps every tuft's look.
        public static int Variant(int tuftIndex, int variants)
        {
            unchecked
            {
                uint h = (uint)tuftIndex * 2654435761u;
                h ^= h >> 15;
                return variants <= 1 ? 0 : (int)(h % (uint)variants);
            }
        }
    }
}
