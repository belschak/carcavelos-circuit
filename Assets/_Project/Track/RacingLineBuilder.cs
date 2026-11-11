using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // Plain C# (Edit Mode tested, task T8): turns the road's centre points into the racing line TrackPolyline wants.
    // The builder's points can start anywhere and run either way round, so this
    // 1. drops points that sit on top of each other,
    // 2. starts the line at the point closest to the start/finish line,
    // 3. turns the line round if it runs against the driving direction,
    // 4. keeps about one point every `spacing` metres on straights, but every point where the road bends.
    public static class RacingLineBuilder
    {
        private const float SamePointMetres = 0.01f; // closer than 1 cm (flat) = the same point

        // Returns the racing line, first point = start line, in driving order. Empty list for fewer than 3 points.
        // startForward = driving direction at the start line (zero = keep the order as given).
        // spacing <= 0 keeps every point.
        public static List<Vector3> Build(IList<Vector3> roadPoints, Vector3 startPosition, Vector3 startForward,
            float spacing, float keepTurnDegrees)
        {
            List<Vector3> clean = WithoutDuplicates(roadPoints);
            if (clean.Count < 3)
            {
                return new List<Vector3>();
            }
            int start = ClosestIndex(clean, startPosition);
            bool reverse = RunsBackwards(clean, start, startForward);
            List<Vector3> ordered = StartAt(clean, start, reverse);
            return Thin(ordered, spacing, keepTurnDegrees);
        }

        // Copy without points closer than 1 cm to the one before; also drops a last point that repeats the first.
        private static List<Vector3> WithoutDuplicates(IList<Vector3> points)
        {
            var result = new List<Vector3>();
            if (points == null)
            {
                return result;
            }
            for (int i = 0; i < points.Count; i++)
            {
                if (result.Count == 0 || FlatDistance(points[i], result[result.Count - 1]) > SamePointMetres)
                {
                    result.Add(points[i]);
                }
            }
            // A closed loop drawn with the first point repeated at the end: the line closes itself anyway.
            while (result.Count > 1 && FlatDistance(result[0], result[result.Count - 1]) <= SamePointMetres)
            {
                result.RemoveAt(result.Count - 1);
            }
            return result;
        }

        // Index of the point closest to the start line (flat, so the checkpoint's height does not matter).
        private static int ClosestIndex(List<Vector3> points, Vector3 position)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float d = FlatDistance(points[i], position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        // True when the road from the start point to the next one points against the driving direction.
        private static bool RunsBackwards(List<Vector3> points, int start, Vector3 startForward)
        {
            Vector3 forward = Flat(startForward);
            if (forward.sqrMagnitude < 0.0001f)
            {
                return false; // no direction given: trust the builder's order
            }
            Vector3 along = Flat(points[(start + 1) % points.Count] - points[start]);
            return Vector3.Dot(along, forward) < 0f;
        }

        // The same loop, read from `start` onwards, forwards or backwards.
        private static List<Vector3> StartAt(List<Vector3> points, int start, bool reverse)
        {
            var result = new List<Vector3>();
            int n = points.Count;
            for (int k = 0; k < n; k++)
            {
                int i = reverse ? (start - k + n) % n : (start + k) % n;
                result.Add(points[i]);
            }
            return result;
        }

        // Fewer points on straights (the minimap and every DistanceAlongLap call get cheaper), all points in bends.
        private static List<Vector3> Thin(List<Vector3> points, float spacing, float keepTurnDegrees)
        {
            if (spacing <= 0f)
            {
                return points;
            }
            var result = new List<Vector3>();
            result.Add(points[0]); // the start line always stays first
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 lastKept = result[result.Count - 1];
                Vector3 next = points[(i + 1) % points.Count];
                bool farEnough = FlatDistance(points[i], lastKept) >= spacing;
                bool bends = TurnDegrees(lastKept, points[i], next) > keepTurnDegrees;
                if (farEnough || bends)
                {
                    result.Add(points[i]);
                }
            }
            return result;
        }

        // How much the road turns at `middle` (0 = straight on, 90 = right angle), seen from above.
        private static float TurnDegrees(Vector3 before, Vector3 middle, Vector3 after)
        {
            Vector3 inDirection = Flat(middle - before);
            Vector3 outDirection = Flat(after - middle);
            if (inDirection.sqrMagnitude < 0.0001f || outDirection.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }
            return Vector3.Angle(inDirection, outDirection);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Flat(a - b).magnitude;
        }

        private static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }
    }
}
