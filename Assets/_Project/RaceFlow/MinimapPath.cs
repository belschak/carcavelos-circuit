using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R9, plain C# (Edit Mode tested): a smooth closed line for the minimap.
    // The racing line is a list of corner points (only about a dozen when it comes from the checkpoints), so drawn
    // straight it looks like a polygon. A Catmull-Rom curve goes through every point and bends softly in between,
    // like a road. The result is many short pieces, which Painter2D strokes with round joins.
    public static class MinimapPath
    {
        // Closed curve through all points: stepsPerPiece points between two neighbours (2 .. 24). The first point comes
        // first and is not repeated at the end. Fewer than 3 points come back unchanged (nothing to smooth).
        public static List<Vector2> SmoothClosed(IList<Vector2> points, int stepsPerPiece)
        {
            List<Vector2> result = new List<Vector2>();
            if (points == null)
            {
                return result;
            }
            int count = points.Count;
            if (count < 3)
            {
                for (int i = 0; i < count; i++)
                {
                    result.Add(points[i]);
                }
                return result;
            }
            int steps = Mathf.Clamp(stepsPerPiece, 2, 24);
            for (int i = 0; i < count; i++)
            {
                Vector2 p0 = points[(i - 1 + count) % count];
                Vector2 p1 = points[i];
                Vector2 p2 = points[(i + 1) % count];
                Vector2 p3 = points[(i + 2) % count];
                for (int s = 0; s < steps; s++)
                {
                    result.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
                }
            }
            return result;
        }

        // Fewer points for a very dense line (e.g. a racing line with a point every metre): keeps every n-th point so
        // the map mesh stays small (at most about `maximum` points; 0 or less = keep all).
        public static List<Vector2> Thin(IList<Vector2> points, int maximum)
        {
            List<Vector2> result = new List<Vector2>();
            if (points == null)
            {
                return result;
            }
            int step = maximum > 0 && points.Count > maximum ? Mathf.CeilToInt(points.Count / (float)maximum) : 1;
            for (int i = 0; i < points.Count; i += step)
            {
                result.Add(points[i]);
            }
            return result;
        }

        // Length of the closed line (last point back to the first).
        public static float ClosedLength(IList<Vector2> points)
        {
            if (points == null || points.Count < 2)
            {
                return 0f;
            }
            float length = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                length += Vector2.Distance(points[i], points[(i + 1) % points.Count]);
            }
            return length;
        }

        // Standard uniform Catmull-Rom: at t = 0 it is p1, at t = 1 it is p2.
        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
