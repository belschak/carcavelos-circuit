using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R9, plain C# (Edit Mode tested): turns world positions into minimap pixels, for any track size.
    // The map is turned ONCE so the start direction points up (at GO "up on the map" = "ahead on screen"), then it stays
    // fixed for the whole race: the player learns one picture of the track, and a small phone map stays readable.
    // The racing line's bounding box (after that turn) is scaled to fill the map rect inside the padding, keeping the
    // track's shape (same scale on both axes) and centring it. Only x and z count, so height never moves a dot.
    // UI Toolkit coordinates: origin top left, x to the right, y DOWN.
    public class MinimapFit
    {
        private readonly Vector3 up;    // world direction (flat, unit) that points up on the map
        private readonly Vector3 right; // world direction that points right on the map
        private readonly float mapWidth;
        private readonly float mapHeight;
        private readonly float centreU;
        private readonly float centreV;

        // worldPoints = the racing line; upDirection = world direction that should point up (y is ignored; zero = +Z).
        public MinimapFit(IList<Vector3> worldPoints, Vector3 upDirection, float mapWidth, float mapHeight, float padding)
        {
            up = new Vector3(upDirection.x, 0f, upDirection.z);
            if (up.sqrMagnitude < 0.0001f || float.IsNaN(up.x) || float.IsNaN(up.z))
            {
                up = Vector3.forward;
            }
            up.Normalize();
            right = new Vector3(up.z, 0f, -up.x); // up turned 90 degrees clockwise seen from above
            this.mapWidth = Mathf.Max(0f, mapWidth);
            this.mapHeight = Mathf.Max(0f, mapHeight);

            // Bounding box in the turned frame (u = right, v = up).
            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            int used = 0;
            if (worldPoints != null)
            {
                for (int i = 0; i < worldPoints.Count; i++)
                {
                    Vector3 p = worldPoints[i];
                    if (float.IsNaN(p.x) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.z))
                    {
                        continue;
                    }
                    float u = Vector3.Dot(p, right);
                    float v = Vector3.Dot(p, up);
                    minU = Mathf.Min(minU, u);
                    maxU = Mathf.Max(maxU, u);
                    minV = Mathf.Min(minV, v);
                    maxV = Mathf.Max(maxV, v);
                    used++;
                }
            }
            float extentU = maxU - minU;
            float extentV = maxV - minV;
            float innerWidth = this.mapWidth - 2f * padding;
            float innerHeight = this.mapHeight - 2f * padding;
            if (used < 2 || innerWidth <= 0f || innerHeight <= 0f || (extentU < 0.01f && extentV < 0.01f))
            {
                return; // IsValid stays false: the HUD then shows no map instead of a dot soup
            }
            // The tighter side decides, so the whole line fits. A straight line (one extent 0) uses the other side.
            float scaleU = extentU > 0.01f ? innerWidth / extentU : float.MaxValue;
            float scaleV = extentV > 0.01f ? innerHeight / extentV : float.MaxValue;
            Scale = Mathf.Min(scaleU, scaleV);
            centreU = (minU + maxU) * 0.5f;
            centreV = (minV + maxV) * 0.5f;
            IsValid = true;
        }

        // False for fewer than 2 usable points, a zero-size track or a map too small for its padding.
        public bool IsValid { get; private set; }

        // Map pixels per world metre.
        public float Scale { get; private set; }

        // A world position in map pixels (may lie outside the map for a kart far off the track; see Clamp).
        public Vector2 ToMap(Vector3 world)
        {
            if (!IsValid)
            {
                return new Vector2(mapWidth * 0.5f, mapHeight * 0.5f);
            }
            float u = Vector3.Dot(world, right) - centreU;
            float v = Vector3.Dot(world, up) - centreV;
            return new Vector2(mapWidth * 0.5f + u * Scale, mapHeight * 0.5f - v * Scale);
        }

        // Heading on the map in degrees, clockwise from up (UI Toolkit's rotate): 0 = up, 90 = right, 180 = down.
        public float ToMapAngle(Vector3 worldForward)
        {
            float u = Vector3.Dot(worldForward, right);
            float v = Vector3.Dot(worldForward, up);
            if (Mathf.Abs(u) < 0.0001f && Mathf.Abs(v) < 0.0001f)
            {
                return 0f;
            }
            return Mathf.Atan2(u, v) * Mathf.Rad2Deg;
        }

        // Keeps a dot inside the map, margin pixels from the edge, so a kart that fell off the world is still shown.
        public Vector2 Clamp(Vector2 mapPoint, float margin)
        {
            float x = Mathf.Clamp(mapPoint.x, margin, Mathf.Max(margin, mapWidth - margin));
            float y = Mathf.Clamp(mapPoint.y, margin, Mathf.Max(margin, mapHeight - margin));
            return new Vector2(x, y);
        }
    }
}
