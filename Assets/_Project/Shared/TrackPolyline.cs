using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Shared
{
    // Plain C# helper (no MonoBehaviour, Edit Mode tested): a closed racing line made of straight pieces, with the
    // maths every ITrackProgress needs. Role 2's TrackProgress can wrap one built from its road points; RaceFlow
    // builds one from the checkpoints as a fallback. Distances are flat (x/z only), so ramps and bridges do not
    // count twice and a kart in the air still has a position.
    public class TrackPolyline : ITrackProgress
    {
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> startDistance = new List<float>(); // distance from the start line to points[i]
        private float lapLength;

        // points = the line in driving order, first point = start/finish line. The line is closed automatically
        // (last point back to the first). Points closer than 1 cm to the previous one are skipped.
        public TrackPolyline(IList<Vector3> linePoints)
        {
            if (linePoints != null)
            {
                for (int i = 0; i < linePoints.Count; i++)
                {
                    if (points.Count == 0 || Flat(linePoints[i] - points[points.Count - 1]).magnitude > 0.01f)
                    {
                        points.Add(linePoints[i]);
                    }
                }
            }
            float total = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                startDistance.Add(total);
                total += SegmentLength(i);
            }
            lapLength = total;
        }

        // True when the line has at least 3 points and a length, so positions can be worked out.
        public bool IsValid { get { return points.Count >= 3 && lapLength > 0.01f; } }

        public float LapLength { get { return lapLength; } }

        public IReadOnlyList<Vector3> RacingLine { get { return points; } }

        public float DistanceAlongLap(Vector3 worldPosition)
        {
            if (!IsValid)
            {
                return 0f;
            }
            // Check every piece and keep the closest (a few hundred pieces at most, fine every frame).
            float bestSqr = float.MaxValue;
            float bestDistance = 0f;
            Vector3 p = Flat(worldPosition);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 a = Flat(points[i]);
                Vector3 b = Flat(points[(i + 1) % points.Count]);
                Vector3 ab = b - a;
                float lengthSqr = ab.sqrMagnitude;
                float t = lengthSqr > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSqr) : 0f;
                Vector3 closest = a + ab * t;
                float sqr = (p - closest).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestDistance = startDistance[i] + Mathf.Sqrt(lengthSqr) * t;
                }
            }
            return Wrap(bestDistance);
        }

        public Vector3 PointAt(float distance)
        {
            if (!IsValid)
            {
                return points.Count > 0 ? points[0] : Vector3.zero;
            }
            int i = SegmentIndexAt(Wrap(distance), out float t);
            return Vector3.Lerp(points[i], points[(i + 1) % points.Count], t);
        }

        public Vector3 DirectionAt(float distance)
        {
            if (!IsValid)
            {
                return Vector3.forward;
            }
            int i = SegmentIndexAt(Wrap(distance), out float t);
            Vector3 direction = Flat(points[(i + 1) % points.Count] - points[i]);
            return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
        }

        // Keeps any distance inside 0 .. LapLength (negative or more than one lap wraps around).
        public float Wrap(float distance)
        {
            if (lapLength <= 0f || float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return 0f;
            }
            float wrapped = distance % lapLength;
            if (wrapped < 0f)
            {
                wrapped += lapLength;
            }
            return wrapped;
        }

        // Which piece a distance lies on, and how far along it (0..1).
        private int SegmentIndexAt(float distance, out float t)
        {
            for (int i = 0; i < points.Count; i++)
            {
                float length = SegmentLength(i);
                if (distance <= startDistance[i] + length || i == points.Count - 1)
                {
                    t = length > 0f ? Mathf.Clamp01((distance - startDistance[i]) / length) : 0f;
                    return i;
                }
            }
            t = 0f;
            return 0;
        }

        private float SegmentLength(int i)
        {
            return Flat(points[(i + 1) % points.Count] - points[i]).magnitude;
        }

        private static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }
    }
}
