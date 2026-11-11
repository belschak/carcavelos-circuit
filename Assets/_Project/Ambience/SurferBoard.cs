using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // The surfboard, built in code: 13 cross-sections from tail to nose (a pointed nose, a rounded squash tail, nose
    // and tail curve up = "rocker", thin rounded rails, a slightly domed deck with a coloured stripe down the middle)
    // and three fins under the tail (a "thruster"). Board space: origin = middle of the deck, +z = nose, +x = right,
    // +y = up, metres. About 2.1 m long, a typical shortboard / small funboard.
    public static class SurferBoard
    {
        private const int Stations = 13, StationVerts = 13, Fins = 3;
        public const int Verts = Stations * StationVerts + Fins * 6;
        public const int Indices = (Stations - 1) * 7 * 6 + Fins * 6;

        // Where the spray starts, in board space: just behind the tail.
        public static readonly Vector3 TailPoint = new Vector3(0f, 0.04f, -0.98f);

        // The outline from tail to nose: where each cross-section is and how wide (half width) the board is there.
        private static readonly float[] StationZ = { -1.03f, -1.0f, -0.92f, -0.76f, -0.52f, -0.24f, 0.04f, 0.32f, 0.56f, 0.76f, 0.91f, 1.0f, 1.06f };
        private static readonly float[] StationHalfWidth = { 0.03f, 0.12f, 0.185f, 0.232f, 0.26f, 0.272f, 0.27f, 0.256f, 0.222f, 0.17f, 0.11f, 0.052f, 0.012f };

        // How far the deck is lifted at z along the board: nose and tail curve up ("rocker"), flat under the feet.
        public static float Rocker(float z)
        {
            if (float.IsNaN(z))
            {
                return 0f;
            }
            if (z > 0.42f)
            {
                float t = Mathf.Min(1f, (z - 0.42f) / 0.64f);
                return 0.11f * t * t;
            }
            if (z < -0.5f)
            {
                float t = Mathf.Min(1f, (-z - 0.5f) / 0.53f);
                return 0.04f * t * t;
            }
            return 0f;
        }

        // Per cross-section 13 points: deck left edge, stripe edge, stripe (2), stripe edge, deck right edge,
        // bottom left / middle / right, left rail top + bottom, right rail top + bottom. Then 6 points per fin.
        public static int Write(Matrix4x4 m, Color32 deck, Color32 stripeColour, Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Color32 under = new Color32((byte)(deck.r * 0.8f), (byte)(deck.g * 0.8f), (byte)(deck.b * 0.8f), 255);
            Color32 rail = new Color32((byte)(deck.r * 0.92f), (byte)(deck.g * 0.92f), (byte)(deck.b * 0.92f), 255);
            Vector3 up = m.MultiplyVector(Vector3.up).normalized;
            Vector3 down = -up;
            for (int s = 0; s < Stations; s++)
            {
                float z = StationZ[s];
                float w = StationHalfWidth[s];
                float y = Rocker(z);
                float thick = 0.065f * (0.4f + 0.6f * w / 0.272f);     // thin at nose and tail
                float stripe = Mathf.Min(0.045f, w * 0.45f);
                float dome = 0.014f * w / 0.272f;                       // the deck is a little higher in the middle
                int b = v + s * StationVerts;
                Vector3 leftOut = m.MultiplyVector(new Vector3(-1f, 0.35f, 0f)).normalized;
                Vector3 rightOut = m.MultiplyVector(new Vector3(1f, 0.35f, 0f)).normalized;
                Vector3 leftLow = m.MultiplyVector(new Vector3(-1f, -0.6f, 0f)).normalized;
                Vector3 rightLow = m.MultiplyVector(new Vector3(1f, -0.6f, 0f)).normalized;
                Set(m, b, -w, y, z, up, deck, vertices, normals, colours);
                Set(m, b + 1, -stripe, y + dome, z, up, deck, vertices, normals, colours);
                Set(m, b + 2, -stripe, y + dome, z, up, stripeColour, vertices, normals, colours);
                Set(m, b + 3, stripe, y + dome, z, up, stripeColour, vertices, normals, colours);
                Set(m, b + 4, stripe, y + dome, z, up, deck, vertices, normals, colours);
                Set(m, b + 5, w, y, z, up, deck, vertices, normals, colours);
                Set(m, b + 6, -w * 0.82f, y - thick * 0.75f, z, down, under, vertices, normals, colours);
                Set(m, b + 7, 0f, y - thick, z, down, under, vertices, normals, colours);
                Set(m, b + 8, w * 0.82f, y - thick * 0.75f, z, down, under, vertices, normals, colours);
                Set(m, b + 9, -w, y, z, leftOut, rail, vertices, normals, colours);
                Set(m, b + 10, -w * 0.82f, y - thick * 0.75f, z, leftLow, rail, vertices, normals, colours);
                Set(m, b + 11, w, y, z, rightOut, rail, vertices, normals, colours);
                Set(m, b + 12, w * 0.82f, y - thick * 0.75f, z, rightLow, rail, vertices, normals, colours);
            }
            // Fins under the tail: a big one in the middle and two smaller ones near the rails.
            Vector3 right = m.MultiplyVector(Vector3.right).normalized;
            int f = v + Stations * StationVerts;
            f = Fin(m, 0f, 0.13f, right, stripeColour, vertices, normals, colours, f);
            f = Fin(m, -0.17f, 0.1f, right, stripeColour, vertices, normals, colours, f);
            f = Fin(m, 0.17f, 0.1f, right, stripeColour, vertices, normals, colours, f);
            return f;
        }

        public static int Triangles(int[] indices, int i, int v)
        {
            for (int s = 0; s < Stations - 1; s++)
            {
                int a = v + s * StationVerts;          // this cross-section (nearer the tail)
                int b = a + StationVerts;              // the next one (nearer the nose)
                i = Quad(indices, i, a, a + 1, b, b + 1, true);        // deck, left of the stripe
                i = Quad(indices, i, a + 2, a + 3, b + 2, b + 3, true); // the stripe
                i = Quad(indices, i, a + 4, a + 5, b + 4, b + 5, true); // deck, right of the stripe
                i = Quad(indices, i, a + 6, a + 7, b + 6, b + 7, false); // bottom
                i = Quad(indices, i, a + 7, a + 8, b + 7, b + 8, false);
                i = Quad(indices, i, a + 10, a + 9, b + 10, b + 9, true);   // left rail
                i = Quad(indices, i, a + 11, a + 12, b + 11, b + 12, true); // right rail
            }
            int f = v + Stations * StationVerts;
            for (int n = 0; n < Fins; n++)
            {
                indices[i++] = f; indices[i++] = f + 1; indices[i++] = f + 2;
                indices[i++] = f + 3; indices[i++] = f + 4; indices[i++] = f + 5;
                f += 6;
            }
            return i;
        }

        // One swept-back fin at x, 'depth' deep: a triangle drawn from both sides (left side points, then right).
        private static int Fin(Matrix4x4 m, float x, float depth, Vector3 right, Color32 colour,
                               Vector3[] vertices, Vector3[] normals, Color32[] colours, int f)
        {
            float back = x == 0f ? -0.9f : -0.8f;
            float bottom = Rocker(back) - 0.055f;
            Vector3 front = new Vector3(x, bottom, back + 0.17f), rear = new Vector3(x, bottom, back), tip = new Vector3(x, bottom - depth, back - 0.05f);
            Set(m, f, front.x, front.y, front.z, -right, colour, vertices, normals, colours);
            Set(m, f + 1, rear.x, rear.y, rear.z, -right, colour, vertices, normals, colours);
            Set(m, f + 2, tip.x, tip.y, tip.z, -right, colour, vertices, normals, colours);
            Set(m, f + 3, front.x, front.y, front.z, right, colour, vertices, normals, colours);
            Set(m, f + 4, tip.x, tip.y, tip.z, right, colour, vertices, normals, colours);
            Set(m, f + 5, rear.x, rear.y, rear.z, right, colour, vertices, normals, colours);
            return f + 6;
        }

        private static void Set(Matrix4x4 m, int index, float x, float y, float z, Vector3 normal, Color32 colour,
                                Vector3[] vertices, Vector3[] normals, Color32[] colours)
        {
            vertices[index] = m.MultiplyPoint3x4(new Vector3(x, y, z));
            normals[index] = normal;
            colours[index] = colour;
        }

        // Two triangles between a pair of points on one cross-section (left, right) and the same pair on the next.
        // faceUp = seen from above / outside (Unity draws a triangle from the side where its corners run clockwise).
        private static int Quad(int[] indices, int i, int left, int right, int nextLeft, int nextRight, bool faceUp)
        {
            if (faceUp)
            {
                indices[i++] = left; indices[i++] = nextLeft; indices[i++] = right;
                indices[i++] = right; indices[i++] = nextLeft; indices[i++] = nextRight;
            }
            else
            {
                indices[i++] = left; indices[i++] = right; indices[i++] = nextLeft;
                indices[i++] = right; indices[i++] = nextRight; indices[i++] = nextLeft;
            }
            return i;
        }
    }
}
