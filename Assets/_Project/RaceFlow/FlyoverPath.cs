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

        // V28 mood preview: a shot from above the land side of the track, looking past the campus out to the sea in
        // the sun's direction, tilted so a low sun sits in the strip along the top edge (the start screen covers the
        // middle with the title, the menu and the leaderboard). A high sun cannot be in the picture, so the camera
        // always looks down at least 10 degrees: campus and sea instead of empty sky.
        // towardsSun = unit vector to the sun (from RenderSettings.sun); straight up or zero = look south.
        public static void PreviewPose(Vector3 centre, float radius, Vector3 towardsSun, float verticalFov, float minHeight,
                                       out Vector3 position, out Quaternion rotation)
        {
            Vector3 flat = new Vector3(towardsSun.x, 0f, towardsSun.z);
            if (flat.sqrMagnitude < 1e-6f)
            {
                flat = Vector3.back;                                         // -z = south, the sea at Carcavelos
            }
            flat.Normalize();
            float elevation = towardsSun.sqrMagnitude > 1e-6f ? Mathf.Asin(Mathf.Clamp(towardsSun.normalized.y, -1f, 1f)) * Mathf.Rad2Deg : 30f;
            float distance = Mathf.Max(radius * 0.7f, minHeight);
            float height = Mathf.Max(radius * 0.25f, minHeight);
            position = centre - flat * distance + Vector3.up * height;
            float pitch = Mathf.Clamp(elevation - 0.45f * verticalFov, -35f, -10f);   // sun 90 % of the way to the top
            rotation = Quaternion.LookRotation(flat) * Quaternion.Euler(-pitch, 0f, 0f);
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
