using UnityEngine;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). The racing line: the route points of the track, in driving order, as a closed loop.
    // BotRaceSceneBuilder bakes them from CampusWorld/Data/track.json, so the game needs no file at run time.
    // Everything a kart needs to know about "where am I on the road" is answered here:
    // which route point is nearest, how far across the road am I, how sharp is the corner ahead.
    public class BotRoute : MonoBehaviour
    {
        [Tooltip("Road centre points in driving order, about 2 m apart (closed loop)")]
        public Vector3[] points;

        [Tooltip("Road width in metres at each point")]
        public float[] widths;

        [Tooltip("Route index of each checkpoint (element 0 = start/finish line)")]
        public int[] checkpointAt;

        [Tooltip("Route index where the ramp jump starts (karts are in the air after it)")]
        public int jumpIndex;

        [Tooltip("Metres a bot may land beside its line after flying over a bump (start or top of a ramp); smaller = slower over bumps")]
        public float maxLandingError = 0.6f;

        [Header("Racing line: wide in, inside at the apex, wide out, at a few hand-picked corners")]
        [Tooltip("Route index of the apex (middle) of each of these corners")]
        public int[] lineApex = new int[0];
        [Tooltip("Points (2 m each) from the widest spot before the apex to the apex, per corner")]
        public int[] lineHalfLength = new int[0];
        [Tooltip("Metres the line moves to the inside at the apex (to the outside before and after: 80 % of it)")]
        public float[] lineMetres = new float[0];
        [Tooltip("Test switch: false = drive the centre line everywhere")]
        public bool useRacingLine = true;

        [Header("Drift corner (one marked corner; same index twice = none)")]
        [Tooltip("Route index where a drifting bot starts to drift")]
        public int driftFrom;
        [Tooltip("Route index where it lets go")]
        public int driftTo;

        private Vector3[] forward;   // driving direction at each point, worked out once
        private int[] checkpointOf;  // last checkpoint passed at each point, worked out once
        private float[] lineOffset; // metres right (+) / left (-) of the centre line where the racing line runs
        private float[] curvature; // 1 / corner radius at each point, + = right turn, - = left turn, 0 = straight
        private bool[] noPass;     // true where bots must not START a pass: narrow bends, ramp edges, the jump and its landing
        private float[] bumpSpeed; // speed limit over a bump next to a corner (very big = no limit)

        public int Count { get { return points.Length; } }
        public float Spacing { get; private set; }   // average metres between two points
        public float Length { get { return Spacing * Count; } }

        private void Awake()
        {
            Rebuild();
        }

        // Works everything out from the points again (the test calls it after switching the racing line off).
        public void Rebuild()
        {
            float total = 0f;
            for (int i = 0; i < Count; i++)
            {
                total += Vector3.Distance(points[i], points[Wrap(i + 1)]);
            }
            Spacing = total / Count;

            forward = new Vector3[Count];
            checkpointOf = new int[Count];
            for (int i = 0; i < Count; i++)
            {
                Vector3 d = Flat(points[Wrap(i + 1)] - points[Wrap(i - 1)]);
                forward[i] = d.sqrMagnitude > 0.000001f ? d.normalized : Vector3.forward;
                for (int c = 0; c < checkpointAt.Length; c++)
                {
                    if (checkpointAt[c] <= i)
                    {
                        checkpointOf[i] = c;
                    }
                }
            }

            // Corner radius from three points (8 m before, here, 8 m after): the circle through them.
            lineOffset = new float[Count];
            curvature = MeasureCurvature();
            // Bake the racing line, then measure the corners again ON that line: a wider line is a rounder corner,
            // so the speed limits (and with them the braking points) come from the line the bots really drive.
            if (useRacingLine)
            {
                BakeRacingLine();
                curvature = MeasureCurvature();
            }

            // Bumps: where the road's slope changes suddenly (start or top of a ramp) a fast kart takes off, and in
            // the air it flies straight on. If the road bends meanwhile, it lands beside the road. So each bump gets
            // a speed limit: the fastest speed at which the kart lands at most maxLandingError beside its line.
            bumpSpeed = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                float slopeBefore = (points[i].y - points[Wrap(i - 1)].y) / Spacing;
                float slopeAfter = (points[Wrap(i + 1)].y - points[i].y) / Spacing;
                float change = Mathf.Abs(slopeAfter - slopeBefore);
                bumpSpeed[i] = float.MaxValue;
                if (change > 0.05f && !IsJumpZone(i))
                {
                    bumpSpeed[i] = SafeBumpSpeed(change, SharpestCornerNear(i));
                }
            }
            MarkNoPassZones();
        }

        // No new passes: in a narrow bend, within 16 m of a ramp edge, and on the jump and its landing.
        private void MarkNoPassZones()
        {
            noPass = new bool[Count];
            for (int i = 0; i < Count; i++)
            {
                bool narrowBend = widths[i] < 6.6f && Mathf.Abs(curvature[i]) > 1f / 40f;
                bool rampEdge = false;
                for (int step = -8; step <= 8; step++)
                {
                    int j = Wrap(i + step);
                    float before = (points[j].y - points[Wrap(j - 1)].y) / Spacing;
                    float after = (points[Wrap(j + 1)].y - points[j].y) / Spacing;
                    if (Mathf.Abs(after - before) > 0.05f)
                    {
                        rampEdge = true;
                    }
                }
                noPass[i] = narrowBend || rampEdge || IsJumpZone(i);
            }
        }

        public bool IsNoPassZone(int index)
        {
            return noPass[Wrap(index)];
        }

        // Physics of a hop: the bump kicks the kart up with (speed x slope change) m/s, so it flies for
        // t = 2 x kick / g seconds. A road with corner radius R moves sideways by 1/2 x speed^2 / R x t^2 in that time.
        // We simply try speeds from fast to slow until that sideways error is small enough.
        private float SafeBumpSpeed(float slopeChange, float cornerCurvature)
        {
            for (float speed = 40f; speed > 6f; speed -= 0.5f)
            {
                float flightSeconds = 2f * speed * slopeChange / 9.81f;
                float landingError = 0.5f * speed * speed * cornerCurvature * flightSeconds * flightSeconds;
                if (landingError <= maxLandingError)
                {
                    return speed >= 40f ? float.MaxValue : speed;
                }
            }
            return 6f;
        }

        // The sharpest bend (as 1 / radius) from 4 m before to 24 m after this point.
        private float SharpestCornerNear(int index)
        {
            float sharpest = 0f;
            for (int step = -2; step <= 12; step++)
            {
                sharpest = Mathf.Max(sharpest, Mathf.Abs(curvature[Wrap(index + step)]));
            }
            return sharpest;
        }

        private float[] MeasureCurvature()
        {
            float[] result = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                Vector3 a = Flat(LinePoint(i - 4));
                Vector3 b = Flat(LinePoint(i));
                Vector3 c = Flat(LinePoint(i + 4));
                float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x); // twice the triangle area, signed
                float sides = Vector3.Distance(a, b) * Vector3.Distance(b, c) * Vector3.Distance(a, c);
                result[i] = sides < 0.001f ? 0f : -2f * cross / sides; // minus: Unity's x/z plane turns right for negative cross
            }
            return result;
        }

        // A point of the racing line: the centre point moved sideways by the line offset.
        private Vector3 LinePoint(int index)
        {
            int i = Wrap(index);
            return points[i] + Right(i) * lineOffset[i];
        }

        // For each hand-picked corner: 0 far away, to the OUTSIDE at the entry, to the INSIDE at the apex, to the
        // outside again at the exit, 0 after it. Each piece is an S-curve, so the line has no kinks.
        // The move is limited to half of the free road (road half width minus kart half width minus 0.4 m wall room).
        private void BakeRacingLine()
        {
            const int taper = 8; // points over which the line leaves and rejoins the centre line
            for (int c = 0; c < lineApex.Length; c++)
            {
                int half = lineHalfLength[c];
                float inside = curvature[Wrap(lineApex[c])] > 0f ? 1f : -1f; // right turn: the inside is on the right (+)
                for (int d = -(half + taper); d <= half + taper; d++)
                {
                    int i = Wrap(lineApex[c] + d);
                    float distance = Mathf.Abs(d);
                    float shape; // +1 = fully inside, -0.8 = fully outside
                    if (distance <= half)
                    {
                        shape = Mathf.Lerp(1f, -0.8f, Smooth(distance / half));
                    }
                    else
                    {
                        shape = Mathf.Lerp(-0.8f, 0f, Smooth((distance - half) / taper));
                    }
                    float room = widths[i] * 0.5f - 0.5f - 0.4f;
                    float metres = Mathf.Min(lineMetres[c], 0.5f * room);
                    if (IsJumpZone(i))
                    {
                        metres = 0f; // the jump and its landing keep the tested centre line
                    }
                    lineOffset[i] = inside * shape * metres;
                }
            }
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // Metres right (+) / left (-) of the centre line where the racing line runs at a route position.
        public float LineOffset(float routePos)
        {
            int lo = Mathf.FloorToInt(routePos);
            return Mathf.Lerp(lineOffset[Wrap(lo)], lineOffset[Wrap(lo + 1)], routePos - lo);
        }

        public int Wrap(int index)
        {
            int n = Count;
            return ((index % n) + n) % n;
        }

        // How many points b is ahead of a, the short way round the loop (negative = behind).
        public int IndexDifference(int a, int b)
        {
            int d = Wrap(b - a);
            return d > Count / 2 ? d - Count : d;
        }

        // Driving direction at a point (flat, length 1). Looked up, not recomputed: this is asked many times per step.
        public Vector3 Forward(int index)
        {
            if (forward == null || forward.Length != Count)
            {
                // Not baked yet (the scene builder asks before the game runs): work it out directly.
                Vector3 d = Flat(points[Wrap(index + 1)] - points[Wrap(index - 1)]);
                return d.sqrMagnitude > 0.000001f ? d.normalized : Vector3.forward;
            }
            return forward[Wrap(index)];
        }

        // Points to the right-hand side of the road at a point.
        public Vector3 Right(int index)
        {
            return Vector3.Cross(Vector3.up, Forward(index));
        }

        public float Curvature(int index)
        {
            return curvature[Wrap(index)];
        }

        // Speed limit over a bump at this point (very big = none).
        public float BumpSpeed(int index)
        {
            return bumpSpeed[Wrap(index)];
        }

        public float WidthAt(int index)
        {
            return widths[Wrap(index)];
        }

        // A point on the centre line; routePos is a route index with a fraction (12.5 = half way between 12 and 13).
        public Vector3 PointAt(float routePos)
        {
            int lo = Mathf.FloorToInt(routePos);
            return Vector3.Lerp(points[Wrap(lo)], points[Wrap(lo + 1)], routePos - lo);
        }

        // Nearest route point, looking only a few points back and ahead of where the kart was last step.
        // Searching only near the old index is what makes lap counting safe where the road passes close to itself.
        public int NearestInWindow(Vector3 position, int from, int back, int ahead)
        {
            int best = Wrap(from);
            float bestDistance = float.MaxValue;
            for (int step = -back; step <= ahead; step++)
            {
                int i = Wrap(from + step);
                float d = (points[i] - position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        // Nearest route point on the whole loop (used after a teleport, when the old index is no help).
        public int NearestAnywhere(Vector3 position)
        {
            return NearestInWindow(position, 0, 0, Count - 1);
        }

        // Metres to the right (+) or left (-) of the centre line.
        public float Lateral(Vector3 position, int index)
        {
            return Vector3.Dot(position - points[Wrap(index)], Right(index));
        }

        // Route index with a fraction for a position near the given point.
        public float RoutePos(Vector3 position, int index)
        {
            float along = Vector3.Dot(position - points[Wrap(index)], Forward(index));
            return index + Mathf.Clamp(along / Spacing, -1f, 1f);
        }

        // Which checkpoint was passed last when a kart is at this route index (0 = the start/finish line).
        public int CheckpointBefore(int index)
        {
            return checkpointOf[Wrap(index)];
        }

        // True on the ramp jump and its landing zone: flying there is normal, not a problem.
        public bool IsJumpZone(int index)
        {
            int d = IndexDifference(jumpIndex, index);
            return d >= -3 && d <= 25;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
