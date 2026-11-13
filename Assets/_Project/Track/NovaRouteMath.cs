using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // Plain maths for the Nova campus route (no scene objects, so it can be unit tested).
    // The route is a closed loop of centre points in metres: +X east, +Z north, Y = height.
    public static class NovaRouteMath
    {
        // Length of the closed loop: every segment plus the one from the last point back to the first.
        public static float LoopLength(Vector3[] points)
        {
            float length = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                length += Vector3.Distance(points[i], points[(i + 1) % points.Length]);
            }
            return length;
        }

        // Radius of the circle through three points seen from above (flat). Straight = very large number.
        public static float TurnRadius(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector2 p = new Vector2(a.x, a.z);
            Vector2 q = new Vector2(b.x, b.z);
            Vector2 r = new Vector2(c.x, c.z);
            float ab = Vector2.Distance(p, q);
            float bc = Vector2.Distance(q, r);
            float ca = Vector2.Distance(r, p);
            float cross = Mathf.Abs((q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x)); // twice the triangle area
            if (cross < 0.0001f)
            {
                return float.MaxValue;
            }
            return ab * bc * ca / (2f * cross);
        }

        // Tightest corner of the loop, so a test can make sure the kart can actually drive it.
        public static float MinTurnRadius(Vector3[] points)
        {
            float min = float.MaxValue;
            int n = points.Length;
            for (int i = 0; i < n; i++)
            {
                float radius = TurnRadius(points[(i - 1 + n) % n], points[i], points[(i + 1) % n]);
                if (radius < min)
                {
                    min = radius;
                }
            }
            return min;
        }

        // Steepest slope between neighbouring points in percent (rise / run * 100). Ramps above ~20 % feel like walls.
        public static float MaxSlopePercent(Vector3[] points)
        {
            float max = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Length];
                float run = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                if (run < 0.001f)
                {
                    continue;
                }
                float slope = Mathf.Abs(b.y - a.y) / run * 100f;
                if (slope > max)
                {
                    max = slope;
                }
            }
            return max;
        }

        // Direction of travel at point i (average of the incoming and outgoing segment), flat on the ground.
        public static Vector3 Forward(Vector3[] points, int i)
        {
            int n = points.Length;
            Vector3 dir = points[(i + 1) % n] - points[(i - 1 + n) % n];
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.000001f)
            {
                return Vector3.forward;
            }
            return dir.normalized;
        }

        // Unit vector to the right of the driving direction, used to place the road edges and walls.
        public static Vector3 Right(Vector3[] points, int i)
        {
            Vector3 f = Forward(points, i);
            return new Vector3(f.z, 0f, -f.x);
        }

        // Smooth Catmull-Rom curve through the points, resampled about every "spacing" metres.
        // Gives the road mesh soft corners even when the input points are a bit rough.
        public static Vector3[] Smooth(Vector3[] points, float spacing)
        {
            int n = points.Length;
            System.Collections.Generic.List<Vector3> result = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = points[(i - 1 + n) % n];
                Vector3 p1 = points[i];
                Vector3 p2 = points[(i + 1) % n];
                Vector3 p3 = points[(i + 2) % n];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / spacing));
                for (int s = 0; s < steps; s++)
                {
                    result.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
                }
            }
            return result.ToArray();
        }

        // Standard uniform Catmull-Rom formula: passes through p1 at t = 0 and p2 at t = 1.
        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        // Index of the route point closest to a world position (flat), e.g. to put a checkpoint on the road.
        public static int ClosestIndex(Vector3[] points, Vector3 position)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                float d = new Vector2(points[i].x - position.x, points[i].z - position.z).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
