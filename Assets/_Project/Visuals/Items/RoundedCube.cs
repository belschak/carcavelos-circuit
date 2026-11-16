using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // A cube with rounded edges and corners (task V34), the body of the new item box: a chunky glazed tile block
    // instead of a sharp primitive cube (docs/art-style.md: "small bevel on the edges that catch light").
    // Each face is a grid; every grid point is pushed onto the rounded shape: the point is clamped to an inner
    // cube (edge - 2 x radius), then put radius away from it. The normal is that direction, so light rolls round
    // the edges. Every face shows the whole tile picture (UV 0..1). Plain arrays: Edit Mode tests check them.
    public static class RoundedCube
    {
        public struct Data
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public int[] Triangles;
        }

        // edge = full edge length (m), radius = corner radius (m, at most edge / 2), grid = quads per face side.
        public static Data Build(float edge, float radius, int grid)
        {
            grid = Mathf.Max(2, grid);
            radius = Mathf.Clamp(radius, 0f, edge * 0.5f);
            float half = edge * 0.5f;
            float inner = half - radius;
            int perFace = (grid + 1) * (grid + 1);
            Data data = new Data
            {
                Vertices = new Vector3[perFace * 6],
                Normals = new Vector3[perFace * 6],
                Uvs = new Vector2[perFace * 6],
                Triangles = new int[grid * grid * 6 * 6],
            };
            // Each face: its outward normal, and the two directions u and v run along it (u x v = normal side).
            Vector3[] normals = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            Vector3[] us = { Vector3.left, Vector3.right, Vector3.right, Vector3.right, Vector3.forward, Vector3.back };
            Vector3[] vs = { Vector3.up, Vector3.up, Vector3.forward, Vector3.back, Vector3.up, Vector3.up };
            int t = 0;
            for (int f = 0; f < 6; f++)
            {
                int start = f * perFace;
                for (int j = 0; j <= grid; j++)
                {
                    for (int i = 0; i <= grid; i++)
                    {
                        float a = (float)i / grid;
                        float b = (float)j / grid;
                        Vector3 onCube = normals[f] * half + us[f] * ((a - 0.5f) * edge) + vs[f] * ((b - 0.5f) * edge);
                        Vector3 core = new Vector3(
                            Mathf.Clamp(onCube.x, -inner, inner),
                            Mathf.Clamp(onCube.y, -inner, inner),
                            Mathf.Clamp(onCube.z, -inner, inner));
                        Vector3 outward = onCube - core;
                        Vector3 normal = outward.sqrMagnitude > 1e-10f ? outward.normalized : normals[f];
                        int k = start + j * (grid + 1) + i;
                        data.Vertices[k] = core + normal * radius;
                        data.Normals[k] = normal;
                        data.Uvs[k] = new Vector2(a, b);
                    }
                }
                for (int j = 0; j < grid; j++)
                {
                    for (int i = 0; i < grid; i++)
                    {
                        int p = start + j * (grid + 1) + i;
                        int q = p + 1;
                        int r = p + grid + 1;
                        int s = r + 1;
                        // Clockwise seen from outside (Unity's front face).
                        data.Triangles[t++] = p;
                        data.Triangles[t++] = r;
                        data.Triangles[t++] = q;
                        data.Triangles[t++] = q;
                        data.Triangles[t++] = r;
                        data.Triangles[t++] = s;
                    }
                }
            }
            return data;
        }

        public static Mesh ToMesh(Data data, string name)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.vertices = data.Vertices;
            mesh.normals = data.Normals;
            mesh.uv = data.Uvs;
            mesh.triangles = data.Triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
