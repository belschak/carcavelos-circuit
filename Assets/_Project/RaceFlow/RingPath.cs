using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Where the Seagull Delivery drops its lifebuoy rings (task R7). Plain C#, Edit Mode tested.
    // The rings stand along a path of corner points (ItemSystem.RoadAhead). On a track with its own racing line (T8)
    // the path follows the road (AlongTrack), so rings stay on it through the hall, the stairs and the bends.
    // Otherwise it is a straight line from the kart's nose (a path through the checkpoints cut the bends in the
    // web build, 2026-11-10). A ring at distance d stands d metres along that path and faces along it.
    public static class RingPath
    {
        public const float StepMetres = 4f;          // one path point every 4 m along the racing line
        public const float MaxOffLineMetres = 10f;   // farther from the line (a shortcut, the grass): straight line instead
        public const float MaxGroundDropMetres = 1.5f; // ground lower than this under the line = a gap (the jump): no ring

        // The path along the track's racing line, starting where the kart is, long enough for "metres".
        // null = use the straight line instead: no line, the kart is off the line or it drives the wrong way.
        public static Vector3[] AlongTrack(ITrackProgress progress, Vector3 kart, Vector3 kartForward, float metres)
        {
            if (!TrackProgressSource.IsUsable(progress) || metres <= 0f)
            {
                return null;
            }
            float start = progress.DistanceAlongLap(kart);
            Vector3 onLine = progress.PointAt(start);
            Vector3 offLine = kart - onLine;
            offLine.y = 0f;
            if (offLine.magnitude > MaxOffLineMetres)
            {
                return null; // on the yellow shortcut the line is somewhere else: rings go where the kart looks
            }
            Vector3 forward = Flat(kartForward);
            if (Vector3.Dot(forward, progress.DirectionAt(start)) < 0f)
            {
                return null; // driving the wrong way: rings ahead of the kart, not behind it
            }
            int steps = Mathf.CeilToInt(metres / StepMetres);
            Vector3[] path = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                path[i] = progress.PointAt(start + i * StepMetres); // PointAt wraps over the start line
            }
            return path;
        }

        // A ring on the racing line only stands where the ground is close below the line: over the jump gap the
        // ground is metres down (or missing), and a ring there could never be driven through.
        public static bool IsGroundNearLine(float lineHeight, float groundHeight)
        {
            return groundHeight >= lineHeight - MaxGroundDropMetres && groundHeight <= lineHeight + MaxGroundDropMetres;
        }
        // path = the corner points, distances = metres from path[0], one per ring (growing).
        // positions[i] and facings[i] are filled for every ring; a distance beyond the end of the path keeps
        // going straight on along the last piece (better a ring on the road ahead than no ring).
        public static void Place(Vector3[] path, float[] distances, Vector3[] positions, Vector3[] facings)
        {
            for (int r = 0; r < distances.Length; r++)
            {
                PointAt(path, distances[r], out positions[r], out facings[r]);
            }
        }

        // One point "metres" along the path, and the flat direction of the piece it is on.
        public static void PointAt(Vector3[] path, float metres, out Vector3 position, out Vector3 facing)
        {
            float left = metres;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 piece = path[i + 1] - path[i];
                float length = piece.magnitude;
                bool lastPiece = i == path.Length - 2;
                if (length < 0.01f && !lastPiece)
                {
                    continue; // two corner points on top of each other: nothing to walk
                }
                if (left <= length || lastPiece)
                {
                    Vector3 direction = length < 0.01f ? Vector3.forward : piece / length;
                    position = path[i] + direction * left;
                    facing = Flat(direction);
                    return;
                }
                left -= length;
            }
            position = path.Length > 0 ? path[0] : Vector3.zero; // a path with one point: everything stays there
            facing = Vector3.forward;
        }

        // The direction without its up/down part, length 1 (the ring stands upright even on a slope).
        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
        }
    }
}
