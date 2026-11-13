using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Task V10: the racing line (Role 2's ITrackProgress) cut into points every `step` metres, with how sharp the
    // road bends at each point worked out ONCE when the race is set up, so five bots can ask "how sharp is the road
    // 30 m ahead?" every frame without redoing the maths. All bots share one map. Plain C#, Edit Mode tested.
    public class BotTrackMap
    {
        private readonly Vector3[] points;    // racing line point at index x step metres after the start line
        private readonly float[] curvature;   // 1 / radius at each point, + = right turn, - = left turn
        private readonly float step;
        private readonly ITrackProgress track;

        public int Count { get { return points.Length; } }
        public float Step { get { return step; } }
        public bool IsValid { get { return points.Length >= 3; } }

        // curveMetres: the bend at a point is measured over this many metres before and after it (smooths the
        // small kinks between road points).
        public BotTrackMap(ITrackProgress progress, float stepMetres = 2f, float curveMetres = 6f)
        {
            track = progress;
            step = Mathf.Max(0.5f, float.IsNaN(stepMetres) ? 2f : stepMetres);
            float lap = progress != null ? progress.LapLength : 0f;
            int count = lap > 0f && !float.IsNaN(lap) ? Mathf.FloorToInt(lap / step) : 0;
            points = new Vector3[count];
            curvature = new float[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = progress.PointAt(i * step);
            }
            int reach = Mathf.Max(1, Mathf.RoundToInt(curveMetres / step));
            for (int i = 0; i < count; i++)
            {
                curvature[i] = BotRules.Curvature(Point(i - reach), Point(i), Point(i + reach));
            }
        }

        // Any index, also negative or past the end: the line is a loop.
        public int Wrap(int index)
        {
            int n = Mathf.Max(1, points.Length);
            return ((index % n) + n) % n;
        }

        public Vector3 Point(int index)
        {
            return points.Length == 0 ? Vector3.zero : points[Wrap(index)];
        }

        public float CurvatureAt(int index)
        {
            return points.Length == 0 ? 0f : curvature[Wrap(index)];
        }

        // Driving direction at a point (flat, length 1).
        public Vector3 Forward(int index)
        {
            Vector3 d = Point(index + 1) - Point(index - 1);
            d.y = 0f;
            return d.sqrMagnitude > 0.000001f ? d.normalized : Vector3.forward;
        }

        // Points to the right-hand side of the road at a point.
        public Vector3 Right(int index)
        {
            return Vector3.Cross(Vector3.up, Forward(index));
        }

        // Metres right (+) or left (-) of the racing line.
        public float Lateral(Vector3 position, int index)
        {
            Vector3 offset = position - Point(index);
            offset.y = 0f;
            return Vector3.Dot(offset, Right(index));
        }

        // Nearest point after a teleport (start of the race, put back): asks the whole lap via Role 2's progress.
        public int NearestAnywhere(Vector3 position)
        {
            if (track == null || points.Length == 0)
            {
                return 0;
            }
            float distance = track.DistanceAlongLap(position);
            return float.IsNaN(distance) ? 0 : Wrap(Mathf.RoundToInt(distance / step));
        }

        // Nearest point, searching only a little behind and ahead of where the bot was last frame. That is cheaper,
        // and where the road runs close beside itself (the U-turn at the start plaza) the bot never jumps onto the
        // other side of the wall.
        public int NearestNear(Vector3 position, int lastIndex, int back, int ahead)
        {
            int best = Wrap(lastIndex);
            float bestDistance = float.MaxValue;
            for (int k = -back; k <= ahead; k++)
            {
                Vector3 gap = Point(lastIndex + k) - position;
                gap.y = 0f;
                float d = gap.sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = Wrap(lastIndex + k);
                }
            }
            return best;
        }

        // Rules 2 + 3 together: the fastest speed allowed at `index`, looking planMetres ahead. Each bend ahead allows
        // CornerSpeed in it and a bit more before it (braking distance); the lowest of all these limits wins.
        public float AllowedSpeed(int index, float topSpeed, float cornering, float turnDegreesPerSecond,
            float minCornerSpeed, float braking, float planMetres)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(0f, planMetres) / step);
            float allowed = topSpeed;
            for (int k = 0; k <= steps; k++)
            {
                float corner = BotRules.CornerSpeed(CurvatureAt(index + k), turnDegreesPerSecond, cornering, minCornerSpeed, topSpeed);
                if (corner >= topSpeed)
                {
                    continue; // straight enough: no limit from this point
                }
                allowed = Mathf.Min(allowed, BotRules.SpeedBeforeCorner(corner, k * step, braking));
            }
            return allowed;
        }

        // How many degrees the road's direction changes from `index` to `metres` further on. Signed: + = right,
        // - = left. An S-bend comes out near 0, so it never counts as one long bend to drift through.
        public float BendAhead(int index, float metres)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(metres / step));
            float total = Vector3.SignedAngle(Forward(index), Forward(index + steps), Vector3.up);
            return float.IsNaN(total) ? 0f : total;
        }

        // A bend worth drifting through: from `index` over the next `metres` the road turns minBend to maxBend degrees
        // one way, has no tight spot (every point's radius at least minRadius: a 90 degree corner inside the 6 m
        // atrium is a corner, not a drift) and climbs or drops at most maxClimb metres (no ramp or stairs).
        public bool IsDriftBend(int index, float metres, float minBend, float maxBend, float minRadius, float maxClimb)
        {
            if (!IsValid)
            {
                return false;
            }
            float bend = Mathf.Abs(BendAhead(index, metres));
            if (bend < minBend || bend > maxBend)
            {
                return false;
            }
            int steps = Mathf.Max(1, Mathf.RoundToInt(metres / step));
            float maxCurvature = minRadius > 0f ? 1f / minRadius : float.MaxValue;
            for (int k = 0; k <= steps; k++)
            {
                if (Mathf.Abs(CurvatureAt(index + k)) > maxCurvature)
                {
                    return false;
                }
                if (Mathf.Abs(Point(index + k).y - Point(index).y) > maxClimb)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
