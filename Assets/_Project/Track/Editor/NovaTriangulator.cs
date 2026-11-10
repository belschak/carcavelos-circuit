using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Ear clipping: cuts a simple polygon (no holes) into triangles so flat roofs and ground areas can be drawn.
    // Returns indices into the outline, three per triangle, wound so the face points up (+Y) in Unity.
    public static class NovaTriangulator
    {
        public static List<int> Triangulate(Vector2[] outline)
        {
            List<int> result = new List<int>();
            List<int> remaining = new List<int>();
            bool counterClockwise = SignedArea(outline) > 0f;
            for (int i = 0; i < outline.Length; i++)
            {
                remaining.Add(counterClockwise ? i : outline.Length - 1 - i);
            }

            int guard = outline.Length * outline.Length; // stops on broken outlines instead of looping forever
            while (remaining.Count > 3 && guard > 0)
            {
                guard--;
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    int curr = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];
                    if (IsEar(outline, remaining, prev, curr, next))
                    {
                        AddTriangle(result, prev, curr, next);
                        remaining.RemoveAt(i);
                        clipped = true;
                        break;
                    }
                }
                if (!clipped)
                {
                    break; // self-intersecting outline: give up on the rest instead of hanging the editor
                }
            }
            if (remaining.Count == 3)
            {
                AddTriangle(result, remaining[0], remaining[1], remaining[2]);
            }
            return result;
        }

        // Counter-clockwise in x/z means clockwise seen from above in Unity, which is the visible side for +Y faces.
        private static void AddTriangle(List<int> result, int a, int b, int c)
        {
            result.Add(a);
            result.Add(c);
            result.Add(b);
        }

        public static float SignedArea(Vector2[] outline)
        {
            float area = 0f;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Length];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static bool IsEar(Vector2[] p, List<int> remaining, int prev, int curr, int next)
        {
            Vector2 a = p[prev];
            Vector2 b = p[curr];
            Vector2 c = p[next];
            if (Cross(b - a, c - b) <= 0f)
            {
                return false; // reflex corner, cannot be an ear
            }
            for (int i = 0; i < remaining.Count; i++)
            {
                int k = remaining[i];
                if (k == prev || k == curr || k == next)
                {
                    continue;
                }
                if (Inside(p[k], a, b, c))
                {
                    return false;
                }
            }
            return true;
        }

        private static float Cross(Vector2 u, Vector2 v)
        {
            return u.x * v.y - u.y * v.x;
        }

        private static bool Inside(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, q - a) >= 0f && Cross(c - b, q - b) >= 0f && Cross(a - c, q - c) >= 0f;
        }
    }
}
