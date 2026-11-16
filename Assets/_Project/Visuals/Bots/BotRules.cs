using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Task V10: every decision a bot makes, as plain maths (no MonoBehaviour), so Edit Mode tests can check it.
    // BotDriver asks these questions once per frame; nothing in here knows about Unity scenes or other karts' scripts.
    // Every method is NaN-safe: a bad number gives the "do nothing" answer, never a crazy input.
    public static class BotRules
    {
        // Rule 1, steering ("pure pursuit"): drive the circle arc that passes through the aim point.
        // That arc has curvature 2 x sideways / distance^2. The kart turns (steer x turn rate) per second, and an arc
        // needs speed x curvature, so steer = curvature x speed / turn rate. Below fullSteerSpeed the kart steers
        // weaker (Role 1's KartController), so we count at least that speed. Returns -1 (full left) .. +1 (full right).
        public static float SteerTowards(Vector3 position, Vector3 heading, Vector3 target, float speed,
            float turnDegreesPerSecond, float fullSteerSpeed)
        {
            Vector3 forward = new Vector3(heading.x, 0f, heading.z);
            Vector3 toTarget = new Vector3(target.x - position.x, 0f, target.z - position.z);
            if (IsBad(toTarget.x) || IsBad(toTarget.z) || IsBad(forward.x) || IsBad(forward.z)
                || forward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f || turnDegreesPerSecond <= 0f)
            {
                return 0f;
            }
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float sideways = Vector3.Dot(toTarget, right);
            if (Vector3.Dot(toTarget, forward) < 0f)
            {
                return sideways >= 0f ? 1f : -1f; // the aim point is behind us: turn round as fast as possible
            }
            float curvature = 2f * sideways / toTarget.sqrMagnitude;
            float useSpeed = Mathf.Max(IsBad(speed) ? 0f : Mathf.Abs(speed), fullSteerSpeed);
            float turnRate = turnDegreesPerSecond * Mathf.Deg2Rad;
            return Mathf.Clamp(curvature * useSpeed / turnRate, -1f, 1f);
        }

        // How sharp the road bends at b, from the circle through three points (flat, x/z): 1 / radius in 1/m.
        // + = right turn, - = left turn, 0 = straight (or points on top of each other).
        public static float Curvature(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector2 p = new Vector2(a.x, a.z);
            Vector2 q = new Vector2(b.x, b.z);
            Vector2 r = new Vector2(c.x, c.z);
            float cross = (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x); // twice the triangle area, signed
            float sides = Vector2.Distance(p, q) * Vector2.Distance(q, r) * Vector2.Distance(p, r);
            if (IsBad(cross) || IsBad(sides) || sides < 0.0001f)
            {
                return 0f;
            }
            return -2f * cross / sides; // minus: in Unity's x/z plane a right turn has a negative cross product
        }

        // Rule 2, corners: at full steer the kart turns turnDegreesPerSecond, so a bend with radius R can be driven at
        // turn rate x R (any faster and the kart cannot turn tightly enough). cornering < 1 keeps a safety margin.
        // The answer stays between minSpeed and topSpeed.
        public static float CornerSpeed(float curvature, float turnDegreesPerSecond, float cornering, float minSpeed, float topSpeed)
        {
            float bend = Mathf.Abs(curvature);
            if (IsBad(bend) || IsBad(topSpeed) || bend < 0.0001f)
            {
                return IsBad(topSpeed) ? minSpeed : topSpeed; // straight
            }
            float speed = turnDegreesPerSecond * Mathf.Deg2Rad / bend * Mathf.Clamp01(cornering);
            return Mathf.Clamp(speed, minSpeed, Mathf.Max(minSpeed, topSpeed));
        }

        // How fast the bot may still be now with a corner metresAway ahead, so braking gets it down in time:
        // v^2 = cornerSpeed^2 + 2 x braking x distance (the school formula for braking distance, solved for v).
        public static float SpeedBeforeCorner(float cornerSpeed, float metresAway, float braking)
        {
            if (IsBad(cornerSpeed) || IsBad(metresAway) || IsBad(braking))
            {
                return 0f;
            }
            return Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * Mathf.Max(0f, braking) * Mathf.Max(0f, metresAway));
        }

        // Rule 3, pedals: gas while slower than allowed, roll when a little too fast, brake when clearly too fast.
        // Standing (or rolling backwards) always means gas: the brake key would drive the kart backwards.
        public static float Throttle(float speed, float allowedSpeed, float brakeMargin)
        {
            if (IsBad(speed) || IsBad(allowedSpeed))
            {
                return 0f;
            }
            if (speed < allowedSpeed || speed <= 0.5f)
            {
                return 1f;
            }
            if (speed > allowedSpeed + Mathf.Max(0f, brakeMargin))
            {
                return -1f;
            }
            return 0f;
        }

        // Rule 4, the light rubber band. metresAheadOfPlayer > 0 = the bot is ahead of the player.
        // Far behind (fullAtMetres or more) = top speed x (1 + boost), far ahead = x (1 - slow), in between a straight line.
        public static float RubberBand(float metresAheadOfPlayer, float fullAtMetres, float boost, float slow)
        {
            if (IsBad(metresAheadOfPlayer) || IsBad(fullAtMetres) || fullAtMetres <= 0f)
            {
                return 1f;
            }
            float t = Mathf.Clamp(metresAheadOfPlayer / fullAtMetres, -1f, 1f);
            return t < 0f ? 1f - t * Mathf.Max(0f, boost) : 1f - t * Mathf.Max(0f, slow);
        }

        // The share of the kart's top speed this bot drives at: its own pace x the rubber band.
        // Never above 1: the bot drives the same kart as the player, so it is never faster on a straight by code.
        public static float PaceShare(float pace, float rubberBand)
        {
            if (IsBad(pace) || IsBad(rubberBand))
            {
                return 1f;
            }
            return Mathf.Clamp(pace * rubberBand, 0.3f, 1f);
        }

        // Traffic: another kart ahead sits otherLane metres beside the line, I want myLane. If we are closer than
        // sideMetres, pass it on the side with more room (it is right of the middle = I go left), within +-maxLane.
        public static float LaneToPass(float myLane, float otherLane, float sideMetres, float maxLane)
        {
            if (IsBad(myLane) || IsBad(otherLane))
            {
                return 0f;
            }
            if (Mathf.Abs(myLane - otherLane) >= sideMetres)
            {
                return myLane; // already beside it
            }
            float lane = otherLane >= 0f ? otherLane - sideMetres : otherLane + sideMetres;
            return Mathf.Clamp(lane, -maxLane, maxLane);
        }

        // Traffic: a kart right beside me (not ahead) at otherLane, I am at myLane. Stay on my side of it with at least
        // sideMetres between our lanes, so I never steer into it (at the start the grid has karts side by side).
        public static float LaneBeside(float wantedLane, float myLane, float otherLane, float sideMetres)
        {
            if (IsBad(wantedLane) || IsBad(myLane) || IsBad(otherLane))
            {
                return IsBad(wantedLane) ? 0f : wantedLane;
            }
            if (otherLane >= myLane)
            {
                return Mathf.Min(wantedLane, otherLane - sideMetres); // it is on my right: stay left of it
            }
            return Mathf.Max(wantedLane, otherLane + sideMetres);     // it is on my left: stay right of it
        }

        // Drift on long bends only. driftBendAhead = BotTrackMap.IsDriftBend (a long, even, flat bend comes up).
        // A new drift also needs speed and the ground under the kart; a running drift is held while at least half
        // of minBend is still ahead, then let go (that release gives the drift boost).
        public static bool WantsDrift(bool profileDrifts, bool driftBendAhead, float bendDegreesAhead, float speed01,
            bool grounded, bool drifting, float minBend, float minSpeed01)
        {
            if (!profileDrifts || !grounded || IsBad(bendDegreesAhead) || IsBad(speed01))
            {
                return false;
            }
            if (drifting)
            {
                return Mathf.Abs(bendDegreesAhead) >= minBend * 0.5f;
            }
            return driftBendAhead && speed01 >= minSpeed01;
        }

        private static bool IsBad(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value);
        }
    }
}
