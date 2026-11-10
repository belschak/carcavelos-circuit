using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The maths of the map fly-over (MapFlyover): where the camera floats above the track, and how it glides
    // from there down behind the kart. Plain C#, so Edit Mode tests can check it without a scene.
    public static class FlyoverPath
    {
        // The middle of the track and how far its checkpoints reach from there (flat, ignoring height).
        // No points = centre 0 and radius 0.
        public static void Measure(Vector3[] points, out Vector3 centre, out float radius)
        {
            centre = Vector3.zero;
            radius = 0f;
            if (points == null || points.Length == 0)
            {
                return;
            }
            Vector3 min = points[0];
            Vector3 max = points[0];
            for (int i = 1; i < points.Length; i++)
            {
                min = Vector3.Min(min, points[i]);
                max = Vector3.Max(max, points[i]);
            }
            centre = (min + max) * 0.5f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 flat = points[i] - centre;
                flat.y = 0f;
                radius = Mathf.Max(radius, flat.magnitude);
            }
        }

        // A point on a circle around and above the track, high enough to see all of it.
        // angleDegrees goes round the track; the camera looks at the centre from there.
        public static Vector3 OrbitPosition(Vector3 centre, float radius, float angleDegrees, float minHeight)
        {
            float a = angleDegrees * Mathf.Deg2Rad;
            float distance = Mathf.Max(radius * 0.9f, minHeight);
            float height = Mathf.Max(radius * 0.6f, minHeight);
            return centre + new Vector3(Mathf.Cos(a) * distance, height, Mathf.Sin(a) * distance);
        }

        // Smooth start and smooth stop (smoothstep): 0 -> 0, 0.5 -> 0.5, 1 -> 1, never outside 0..1.
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // Camera position part-way from "from" to "to". A gentle hump on the way, so the camera does not skim
        // through buildings on a straight line down.
        public static Vector3 Glide(Vector3 from, Vector3 to, float t, float humpMetres)
        {
            float e = Ease(t);
            Vector3 position = Vector3.Lerp(from, to, e);
            position.y += Mathf.Sin(e * Mathf.PI) * humpMetres;
            return position;
        }
    }
}
