using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Small round shapes for meshes built in code (the surfers): a smooth tube through several rings (arms, legs,
    // body), a ball (head, hands, feet) and a cap (hair). Each one writes its points, normals and colours into shared
    // arrays and returns the next free point; the matching ...Triangles method writes its triangles. The normals point
    // straight out of the surface, so the light shades every shape round and smooth instead of in flat facets.
    // Points are given in "rider space" and moved into the world with the matrix m.
    public static class MeshShapes
    {
        // ---------- tube through rings ----------

        public static int ChainVerts(int rings, int sides)
        {
            return rings * sides;
        }

        public static int ChainIndices(int rings, int sides)
        {
            return (rings - 1) * sides * 6;
        }

        // A smooth tube through 'rings' rings: ring r sits at at[r], faces along dir[r] (the bone it sits on; zero =
        // from the ring before to the ring after), is rx[r] wide across 'side' and rz[r] the other way, and has the
        // colour colour[r]. Two rings close together with different colours give a sharp colour edge (a sleeve
        // ending, a stripe). Rings next to a bent knee face along their own bone, so they never cross each other on
        // the inside of the bend. Every ring is turned as little as possible from the one before: a bent arm or leg
        // never gets twisted.
        public static int Chain(Matrix4x4 m, Vector3[] at, Vector3[] dir, float[] rx, float[] rz, Color32[] colour, int rings, Vector3 side, int sides,
                                Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Vector3 ex = Vector3.right;
            for (int r = 0; r < rings; r++)
            {
                Vector3 ey = dir[r];
                if (ey.sqrMagnitude < 0.00000001f)
                {
                    // No bone given: from the ring before to the ring after (at the two ends: the end piece).
                    ey = at[r < rings - 1 ? r + 1 : r] - at[r > 0 ? r - 1 : r];
                }
                ey = ey.sqrMagnitude < 0.00000001f ? Vector3.up : ey.normalized;
                Vector3 reference = r == 0 ? side : ex;
                Vector3 x = reference - Vector3.Dot(reference, ey) * ey;
                if (x.sqrMagnitude < 0.0001f)
                {
                    x = Perpendicular(ey);
                }
                ex = x.normalized;
                Vector3 ez = Vector3.Cross(ex, ey);
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    // On an oval the outward direction leans towards the flat side: (rz cos, rx sin), not (cos, sin).
                    Vector3 normal = ex * (c * rz[r]) + ez * (s * rx[r]);
                    if (normal.sqrMagnitude < 0.00000001f)
                    {
                        normal = ex * c + ez * s;
                    }
                    int index = v + r * sides + k;
                    vertices[index] = m.MultiplyPoint3x4(at[r] + ex * (c * rx[r]) + ez * (s * rz[r]));
                    normals[index] = m.MultiplyVector(normal).normalized;
                    colours[index] = colour[r];
                }
            }
            return v + rings * sides;
        }

        public static int ChainTriangles(int[] indices, int i, int v, int rings, int sides)
        {
            for (int r = 0; r < rings - 1; r++)
            {
                int a = v + r * sides;       // this ring
                int b = a + sides;           // the next one
                for (int k = 0; k < sides; k++)
                {
                    int next = (k + 1) % sides;
                    indices[i++] = a + k; indices[i++] = b + k; indices[i++] = a + next;
                    indices[i++] = a + next; indices[i++] = b + k; indices[i++] = b + next;
                }
            }
            return i;
        }

        // ---------- ball ----------

        public static int BallVerts(int segments, int bands)
        {
            return (bands - 1) * segments + 2;
        }

        public static int BallIndices(int segments, int bands)
        {
            return segments * 6 + (bands - 2) * segments * 6;
        }

        // A ball: a top point, bands - 1 rings, a bottom point. ex, ey, ez = its own axes (ez must be Cross(ex, ey)),
        // radii = half sizes along them. chin = 0..1, how much narrower the lower half gets (a jaw instead of a globe).
        public static int Ball(Matrix4x4 m, Vector3 centre, Vector3 ex, Vector3 ey, Vector3 ez, Vector3 radii, float chin, Color32 colour,
                               int segments, int bands, Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            v = Point(m, centre, ex, ey, ez, radii, chin, 0f, 0f, colour, vertices, normals, colours, v);
            for (int ring = 1; ring < bands; ring++)
            {
                float polar = Mathf.PI * ring / bands;
                for (int k = 0; k < segments; k++)
                {
                    v = Point(m, centre, ex, ey, ez, radii, chin, polar, k * Mathf.PI * 2f / segments, colour, vertices, normals, colours, v);
                }
            }
            return Point(m, centre, ex, ey, ez, radii, chin, Mathf.PI, 0f, colour, vertices, normals, colours, v);
        }

        public static int BallTriangles(int[] indices, int i, int v, int segments, int bands)
        {
            i = Fan(indices, i, v, segments);
            i = Bands(indices, i, v + 1, segments, bands - 1);
            int last = v + 1 + (bands - 2) * segments;
            int bottom = last + segments;
            for (int k = 0; k < segments; k++)
            {
                int next = (k + 1) % segments;
                indices[i++] = bottom; indices[i++] = last + k; indices[i++] = last + next;
            }
            return i;
        }

        // ---------- cap (the top part of a ball, open at the bottom) ----------

        public static int CapVerts(int segments, int rings)
        {
            return rings * segments + 1;
        }

        public static int CapIndices(int segments, int rings)
        {
            return segments * 3 + (rings - 1) * segments * 6;
        }

        // The top of a ball down to 'reach' degrees from its top point (90 = half a ball).
        public static int Cap(Matrix4x4 m, Vector3 centre, Vector3 ex, Vector3 ey, Vector3 ez, Vector3 radii, float reach, Color32 colour,
                              int segments, int rings, Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            v = Point(m, centre, ex, ey, ez, radii, 0f, 0f, 0f, colour, vertices, normals, colours, v);
            for (int ring = 1; ring <= rings; ring++)
            {
                float polar = reach * Mathf.Deg2Rad * ring / rings;
                for (int k = 0; k < segments; k++)
                {
                    v = Point(m, centre, ex, ey, ez, radii, 0f, polar, k * Mathf.PI * 2f / segments, colour, vertices, normals, colours, v);
                }
            }
            return v;
        }

        public static int CapTriangles(int[] indices, int i, int v, int segments, int rings)
        {
            i = Fan(indices, i, v, segments);
            return Bands(indices, i, v + 1, segments, rings);
        }

        // ---------- helpers ----------

        // A direction square to 'direction' (any one), for a tube that runs exactly along the 'side' direction.
        public static Vector3 Perpendicular(Vector3 direction)
        {
            Vector3 x = Vector3.Cross(direction, Vector3.forward);
            if (x.sqrMagnitude < 0.0001f)
            {
                x = Vector3.Cross(direction, Vector3.right);
            }
            return x.normalized;
        }

        // One point on a ball: 'polar' = angle down from the top, 'around' = angle round the up axis.
        private static int Point(Matrix4x4 m, Vector3 centre, Vector3 ex, Vector3 ey, Vector3 ez, Vector3 radii, float chin, float polar, float around,
                                 Color32 colour, Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            float ring = Mathf.Sin(polar);
            float height = Mathf.Cos(polar);
            float narrow = height < 0f ? 1f + chin * height : 1f;       // lower half only
            float cx = ring * Mathf.Cos(around), cz = ring * Mathf.Sin(around);
            Vector3 local = ex * (cx * radii.x * narrow) + ey * (height * radii.y) + ez * (cz * radii.z * narrow);
            vertices[v] = m.MultiplyPoint3x4(centre + local);
            normals[v] = m.MultiplyVector(ex * cx + ey * height + ez * cz).normalized;
            colours[v] = colour;
            return v + 1;
        }

        // Triangles from the top point 'top' to the first ring right after it.
        private static int Fan(int[] indices, int i, int top, int segments)
        {
            int first = top + 1;
            for (int k = 0; k < segments; k++)
            {
                int next = (k + 1) % segments;
                indices[i++] = top; indices[i++] = first + next; indices[i++] = first + k;
            }
            return i;
        }

        // Quads between 'rings' rings that follow each other, starting at point 'first'.
        private static int Bands(int[] indices, int i, int first, int segments, int rings)
        {
            for (int ring = 0; ring < rings - 1; ring++)
            {
                int upper = first + ring * segments;
                int lower = upper + segments;
                for (int k = 0; k < segments; k++)
                {
                    int next = (k + 1) % segments;
                    indices[i++] = lower + k; indices[i++] = upper + k; indices[i++] = lower + next;
                    indices[i++] = lower + next; indices[i++] = upper + k; indices[i++] = upper + next;
                }
            }
            return i;
        }
    }
}
