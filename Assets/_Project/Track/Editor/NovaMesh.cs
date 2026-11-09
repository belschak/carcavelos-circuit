using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Collects quads and triangles, then turns them into one Unity Mesh.
    // One mesh per material keeps the Web build fast (few draw calls).
    public class NovaMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public int VertexCount
        {
            get { return vertices.Count; }
        }

        // A four-corner face, corners in clockwise order seen from the side that should be visible.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            uvs.Add(uvA);
            uvs.Add(uvB);
            uvs.Add(uvC);
            uvs.Add(uvD);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        // A vertical wall from a to b (bottom edge), "height" tall, visible from the side where a->b runs clockwise.
        // UVs in metres divided by "tile", so a texture repeats every "tile" metres.
        public void Wall(Vector3 a, Vector3 b, float bottomA, float bottomB, float topA, float topB, float uStart, float tile)
        {
            float length = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
            Vector3 a0 = new Vector3(a.x, bottomA, a.z);
            Vector3 b0 = new Vector3(b.x, bottomB, b.z);
            Vector3 a1 = new Vector3(a.x, topA, a.z);
            Vector3 b1 = new Vector3(b.x, topB, b.z);
            float u0 = uStart / tile;
            float u1 = (uStart + length) / tile;
            Quad(a0, a1, b1, b0, new Vector2(u0, bottomA / tile), new Vector2(u0, topA / tile), new Vector2(u1, topB / tile), new Vector2(u1, bottomB / tile));
        }

        // A box (all six sides) with UVs in metres / tile. Used for slabs, railings, columns and simple props.
        public void Box(Vector3 centre, Vector3 size, Quaternion rotation, float tile)
        {
            Vector3 h = size * 0.5f;
            Vector3[] c = new Vector3[8];
            int k = 0;
            for (int ix = -1; ix <= 1; ix += 2)
            {
                for (int iy = -1; iy <= 1; iy += 2)
                {
                    for (int iz = -1; iz <= 1; iz += 2)
                    {
                        c[k] = centre + rotation * new Vector3(ix * h.x, iy * h.y, iz * h.z);
                        k++;
                    }
                }
            }
            // corner index = (x? 4:0) + (y? 2:0) + (z? 1:0)
            Face(c[0], c[2], c[6], c[4], size.x, size.y, tile); // -Z side
            Face(c[5], c[7], c[3], c[1], size.x, size.y, tile); // +Z side
            Face(c[1], c[3], c[2], c[0], size.z, size.y, tile); // -X side
            Face(c[4], c[6], c[7], c[5], size.z, size.y, tile); // +X side
            Face(c[2], c[3], c[7], c[6], size.x, size.z, tile); // top
            Face(c[1], c[0], c[4], c[5], size.x, size.z, tile); // bottom
        }

        private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width, float height, float tile)
        {
            Quad(a, b, c, d, new Vector2(0f, 0f), new Vector2(0f, height / tile), new Vector2(width / tile, height / tile), new Vector2(width / tile, 0f));
        }

        // A flat polygon (seen from above) at height y, filled with ear clipping. UVs = world x/z divided by tile.
        // Same as Wall, but the top edge is pushed sideways (a battered, sloping wall like the beach ramp's).
        public void SlopedWall(Vector3 a, Vector3 b, float bottomA, float bottomB, float topA, float topB, Vector3 shiftA, Vector3 shiftB, float uStart, float tile)
        {
            float length = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
            Vector3 a0 = new Vector3(a.x, bottomA, a.z);
            Vector3 b0 = new Vector3(b.x, bottomB, b.z);
            Vector3 a1 = new Vector3(a.x, topA, a.z) + shiftA;
            Vector3 b1 = new Vector3(b.x, topB, b.z) + shiftB;
            float u0 = uStart / tile;
            float u1 = (uStart + length) / tile;
            Quad(a0, a1, b1, b0, new Vector2(u0, bottomA / tile), new Vector2(u0, (bottomA + Vector3.Distance(a0, a1)) / tile), new Vector2(u1, (bottomB + Vector3.Distance(b0, b1)) / tile), new Vector2(u1, bottomB / tile));
        }

        // A polygon whose corners have their own heights (sloped roofs).
        public void SlopedPolygon(Vector2[] outline, float[] y, float tile)
        {
            List<int> ears = NovaTriangulator.Triangulate(outline);
            int start = vertices.Count;
            for (int i = 0; i < outline.Length; i++)
            {
                vertices.Add(new Vector3(outline[i].x, y[i], outline[i].y));
                uvs.Add(outline[i] / tile);
            }
            for (int i = 0; i < ears.Count; i++)
            {
                triangles.Add(start + ears[i]);
            }
        }

        // A flat polygon seen from below (soffits under cantilevers).
        public void FlatPolygonDown(Vector2[] outline, float y, float tile)
        {
            List<int> ears = NovaTriangulator.Triangulate(outline);
            int start = vertices.Count;
            for (int i = 0; i < outline.Length; i++)
            {
                vertices.Add(new Vector3(outline[i].x, y, outline[i].y));
                uvs.Add(outline[i] / tile);
            }
            for (int i = 0; i + 2 < ears.Count; i += 3)
            {
                triangles.Add(start + ears[i]);
                triangles.Add(start + ears[i + 2]);
                triangles.Add(start + ears[i + 1]);
            }
        }

        public void FlatPolygon(Vector2[] outline, float y, float tile)
        {
            List<int> ears = NovaTriangulator.Triangulate(outline);
            int start = vertices.Count;
            for (int i = 0; i < outline.Length; i++)
            {
                vertices.Add(new Vector3(outline[i].x, y, outline[i].y));
                uvs.Add(outline[i] / tile);
            }
            for (int i = 0; i < ears.Count; i++)
            {
                triangles.Add(start + ears[i]);
            }
        }

        // Adds one triangle from three new vertices (used by the ground grid and the leaning auditorium).
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            uvs.Add(uvA);
            uvs.Add(uvB);
            uvs.Add(uvC);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        // An upright prism with "sides" faces (6-8 sides reads as a round pole or trunk in our low-poly style).
        public void Prism(Vector3 bottomCentre, float radius, float height, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = bottomCentre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = bottomCentre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Wall(p0, p1, bottomCentre.y, bottomCentre.y, bottomCentre.y + height, bottomCentre.y + height, 0f, 1f);
            }
            Vector3 top = bottomCentre + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Triangle(top, top + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius, top + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius, Vector2.zero, Vector2.right, Vector2.up);
            }
        }

        // A low-poly "blob" (two cones base to base): tree crowns, bushes, parasol tops.
        public void Blob(Vector3 centre, float radius, float halfHeightUp, float halfHeightDown, int sides)
        {
            Vector3 top = centre + Vector3.up * halfHeightUp;
            Vector3 bottom = centre - Vector3.up * halfHeightDown;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Triangle(top, p1, p0, Vector2.zero, Vector2.right, Vector2.up);
                if (halfHeightDown > 0.001f)
                {
                    Triangle(bottom, p0, p1, Vector2.zero, Vector2.right, Vector2.up);
                }
            }
        }

        // A rounded crown (sine profile, flatter underneath): olives, pines, shrubs. Reads round, not like a diamond.
        public void Dome(Vector3 centre, float radius, float up, float down, int sides, int rings)
        {
            for (int k = 0; k < rings * 2; k++)
            {
                float phi0 = -Mathf.PI * 0.5f + k * Mathf.PI / (rings * 2);
                float phi1 = -Mathf.PI * 0.5f + (k + 1) * Mathf.PI / (rings * 2);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides;
                    float a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 l0 = DomePoint(centre, radius, up, down, phi0, a0);
                    Vector3 u0 = DomePoint(centre, radius, up, down, phi1, a0);
                    Vector3 u1 = DomePoint(centre, radius, up, down, phi1, a1);
                    Vector3 l1 = DomePoint(centre, radius, up, down, phi0, a1);
                    Quad(l0, u0, u1, l1, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
            }
        }

        private static Vector3 DomePoint(Vector3 centre, float radius, float up, float down, float phi, float a)
        {
            float r = radius * Mathf.Cos(phi);
            float y = Mathf.Sin(phi) * (phi > 0f ? up : down);
            return centre + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // the road and ground have more than 65k vertices
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
