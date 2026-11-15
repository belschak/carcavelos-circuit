using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // The Carcavelos Wave's water around the kart (task V34), as plain arrays so Edit Mode tests can check it.
    // A ring of water round the kart's footprint: low bow wave in front, a wall at the sides, and behind a crest
    // that curls its white lip forward towards the kart's tail (the wave is pushing the kart). WaveRide copies the
    // arrays into one Mesh every frame (drawn twice: water body and foam layer = two draw calls).
    //
    // Kart space: x = right, y = up (0 = the road under the kart), z = forward. One "column" per angle round the
    // kart (angle 0 = forward), each column is the same cross-section (Profile), scaled by direction.
    public static class WaveMesh
    {
        // Cross-section from the inner edge (under the kart's sides) out to the outer edge on the road.
        // R = share of the wave's width outwards, H = share of the crest height. Rows 4 and 5 lean inwards at the
        // back (LipCurl): that overhang is the breaking lip. UV v runs from the inner edge (0) to the outer edge (1),
        // so the textures can put light shallow water inside, deep teal on the outer face and foam on the crest.
        private static readonly float[] ProfileR = { 0.00f, 0.14f, 0.28f, 0.38f, 0.44f, 0.40f, 0.50f, 0.72f, 1.00f };
        private static readonly float[] ProfileH = { 0.10f, 0.26f, 0.60f, 0.90f, 1.00f, 0.90f, 0.78f, 0.36f, 0.00f };
        public static int Rows { get { return ProfileR.Length; } }

        // Columns = segments + 1 (the last column repeats the first with u = 1, so the texture wraps cleanly).
        public static int VertexCount(int segments)
        {
            return (segments + 1) * Rows;
        }

        // Writes the vertices and UVs of the wave at one moment.
        // rise 0..1: how far the wave has come up out of the road (ItemFxRules.Envelope).
        // collapse 0..1: 0 while surfing, 1 when it has fallen flat (it also spreads outwards as it falls).
        // time: seconds, for the ripples.
        public static void Write(WaveShape shape, float rise, float collapse, float time, int segments,
            Vector3[] vertices, Vector2[] uvs)
        {
            rise = Mathf.Clamp01(rise);
            collapse = Mathf.Clamp01(collapse);
            int rows = Rows;
            for (int c = 0; c <= segments; c++)
            {
                float angle = 360f * c / segments;
                Vector3 inner = ItemFxRules.Ellipse(angle, shape.InnerHalfWidth, shape.InnerHalfLength);
                Vector3 outward = inner.sqrMagnitude > 0.0001f ? inner.normalized : Vector3.forward;
                float width = ItemFxRules.AroundKart(angle, shape.WidthFront, shape.WidthSide, shape.WidthBack)
                    * (1f + 0.7f * collapse) * Mathf.Lerp(0.55f, 1f, rise);
                float crest = ItemFxRules.AroundKart(angle, shape.CrestFront, shape.CrestSide, shape.CrestBack);
                float ripple = ItemFxRules.Ripple(angle, time) * shape.RippleHeight;
                float height = Mathf.Max(0f, (crest + ripple) * rise * (1f - collapse * collapse));
                float curl = ItemFxRules.AroundKart(angle, 0f, shape.LipCurl * 0.35f, shape.LipCurl) * (1f - collapse);
                for (int r = 0; r < rows; r++)
                {
                    float along = ProfileR[r];
                    if (r == 4 || r == 5)
                    {
                        along -= curl * (r == 5 ? 1f : 0.5f); // the lip leans in over the kart's tail
                    }
                    float h = ProfileH[r] * height;
                    if (r == 0)
                    {
                        h = Mathf.Min(h, shape.MaxInnerHeight); // never through the kart's floor
                    }
                    int i = c * rows + r;
                    vertices[i] = inner + outward * (along * width) + Vector3.up * h;
                    uvs[i] = new Vector2((float)c / segments * shape.TextureRepeats, (float)r / (rows - 1));
                }
            }
        }

        // Triangles of the whole surface, wound so the visible side faces up and out. WaveRide draws them twice:
        // the water body, then the foam layer on top (its texture puts the foam on the crest, the lip and the
        // outer edge where the water meets the road).
        public static int[] Triangles(int segments)
        {
            int rows = Rows;
            int[] triangles = new int[segments * (rows - 1) * 6];
            int k = 0;
            for (int c = 0; c < segments; c++)
            {
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = c * rows + r;
                    int b = a + 1;
                    int d = a + rows;
                    int e = d + 1;
                    triangles[k++] = a;
                    triangles[k++] = b;
                    triangles[k++] = d;
                    triangles[k++] = b;
                    triangles[k++] = e;
                    triangles[k++] = d;
                }
            }
            return triangles;
        }
    }

    // The size of the wave round the kart (metres). Defaults fit the Classic kart (about 1.3 m wide, 2 m long).
    [System.Serializable]
    public struct WaveShape
    {
        public float InnerHalfWidth;   // inner edge: half the kart's width
        public float InnerHalfLength;  // inner edge: half the kart's length
        public float WidthFront;       // how far the water reaches out in front ...
        public float WidthSide;        // ... at the sides ...
        public float WidthBack;        // ... and behind
        public float CrestFront;       // crest height in front (a low bow wave: the road ahead stays visible)
        public float CrestSide;
        public float CrestBack;        // the big crest behind; below the chase camera's sight line to the kart
        public float LipCurl;          // share of the width the lip leans in over the tail
        public float RippleHeight;     // metres of ripple on the crest
        public float MaxInnerHeight;   // inner edge cap (the kart's floor)
        public float TextureRepeats;   // water texture repeats once round the kart

        public static WaveShape Default
        {
            get
            {
                return new WaveShape
                {
                    InnerHalfWidth = 0.5f,
                    InnerHalfLength = 0.85f,
                    WidthFront = 0.9f,
                    WidthSide = 1.2f,
                    WidthBack = 1.7f,
                    CrestFront = 0.18f,
                    CrestSide = 0.42f,
                    CrestBack = 0.68f,
                    LipCurl = 0.1f,
                    RippleHeight = 0.06f,
                    MaxInnerHeight = 0.12f,
                    TextureRepeats = 4f,
                };
            }
        }
    }
}
