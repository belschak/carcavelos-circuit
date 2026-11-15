using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // The maths behind the item effects (task V34), in plain C# so Edit Mode tests can check it without a scene.
    // Nothing here touches a GameObject: the effect scripts (WaveRide, EspressoFx, SeagullFx, DeansFx) call these
    // and only move transforms, meshes and particles with the answers.
    public static class ItemFxRules
    {
        // How "on" an effect is at time t (s since use): 0 before, rises over form seconds (ease out),
        // 1 while it holds, falls over collapse seconds (ease in), 0 after. form/collapse 0 = a hard edge.
        public static float Envelope(float t, float form, float hold, float collapse)
        {
            if (t < 0f)
            {
                return 0f;
            }
            if (t < form)
            {
                float p = t / form;
                return 1f - (1f - p) * (1f - p); // ease out: fast rise, soft top
            }
            t -= form;
            if (t < hold)
            {
                return 1f;
            }
            t -= hold;
            if (t < collapse)
            {
                float p = t / collapse;
                return 1f - p * p; // ease in: holds a moment, then drops
            }
            return 0f;
        }

        // Total length of an envelope.
        public static float Length(float form, float hold, float collapse)
        {
            return form + hold + collapse;
        }

        // Smooth 0..1 blend of a value around the kart by direction: angle 0 = straight ahead, 180 = behind.
        // front/side/back are the values in those three directions; in between it blends with a cosine.
        public static float AroundKart(float angleDegrees, float front, float side, float back)
        {
            float a = Mathf.Abs(Mathf.DeltaAngle(0f, angleDegrees)); // 0..180
            if (a <= 90f)
            {
                float p = 0.5f - 0.5f * Mathf.Cos(a / 90f * Mathf.PI);
                return Mathf.Lerp(front, side, p);
            }
            float q = 0.5f - 0.5f * Mathf.Cos((a - 90f) / 90f * Mathf.PI);
            return Mathf.Lerp(side, back, q);
        }

        // Point on an ellipse round the kart (x = right, z = forward) for an angle (0 = forward, clockwise from above).
        public static Vector3 Ellipse(float angleDegrees, float halfWidth, float halfLength)
        {
            float r = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r) * halfWidth, 0f, Mathf.Cos(r) * halfLength);
        }

        // Small travelling ripples along the crest so the water never looks frozen (about +-1).
        public static float Ripple(float angleDegrees, float time)
        {
            float r = angleDegrees * Mathf.Deg2Rad;
            return 0.6f * Mathf.Sin(r * 5f - time * 7f) + 0.4f * Mathf.Sin(r * 9f + time * 5.3f);
        }

        // An expanding ring (crema shock ring, gold flash, splash ring): scale 0 -> max with ease out,
        // alpha 1 -> 0. p = 0..1 of its life.
        public static float RingScale(float p, float maxScale)
        {
            p = Mathf.Clamp01(p);
            return maxScale * (1f - (1f - p) * (1f - p) * (1f - p));
        }

        public static float RingAlpha(float p)
        {
            p = Mathf.Clamp01(p);
            return (1f - p) * (1f - p);
        }

        // Dean's List: the wreath and cap blink in the shield's last seconds (8 blinks a second at the very end,
        // 4 at the start of the warning), like the coins did.
        public static bool ShieldVisible(float secondsLeft, float time, float warnSeconds)
        {
            if (secondsLeft > warnSeconds)
            {
                return true;
            }
            float rate = Mathf.Lerp(8f, 4f, Mathf.Clamp01(secondsLeft / Mathf.Max(0.01f, warnSeconds)));
            return Mathf.Repeat(time * rate, 1f) < 0.6f;
        }

        // The mortarboard thrown up when the shield blocks a bump: a parabola back down to 0 after duration.
        public static float TossHeight(float t, float duration, float peak)
        {
            if (t <= 0f || t >= duration)
            {
                return 0f;
            }
            float p = t / duration;
            return 4f * peak * p * (1f - p);
        }

        // Catmull-Rom point between b and c (a and d steer the curve). The gull's flight path uses it.
        public static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (3f * b - a - 3f * c + d) * t3);
        }

        // A point along a path through all points, s from 0 (first point) to 1 (last point), each segment the same
        // share of s. Fewer than 2 points: the first point (or zero).
        public static Vector3 PathPoint(Vector3[] points, float s)
        {
            if (points == null || points.Length == 0)
            {
                return Vector3.zero;
            }
            if (points.Length == 1)
            {
                return points[0];
            }
            int segments = points.Length - 1;
            float x = Mathf.Clamp01(s) * segments;
            int i = Mathf.Min(Mathf.FloorToInt(x), segments - 1);
            float t = x - i;
            Vector3 a = points[Mathf.Max(0, i - 1)];
            Vector3 b = points[i];
            Vector3 c = points[i + 1];
            Vector3 d = points[Mathf.Min(points.Length - 1, i + 2)];
            return CatmullRom(a, b, c, d, t);
        }

        // Wing flap angle (degrees) for the gull: fast while it climbs or speeds up, a glide in between.
        public static float WingFlap(float time, float strength)
        {
            return Mathf.Lerp(8f, 48f, Mathf.Clamp01(strength)) * Mathf.Sin(time * 15f);
        }

        // The gull's barrel roll as it leaves: one full turn over the exit, eased in and out (degrees).
        public static float ExitRoll(float p)
        {
            p = Mathf.Clamp01(p);
            return 360f * p * p * (3f - 2f * p);
        }
    }
}
