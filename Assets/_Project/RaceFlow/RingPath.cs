using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Where the Seagull Delivery drops its lifebuoy rings (task R7). Plain C#, Edit Mode tested.
    // The road ahead is not a straight line from the kart's nose, so the rings follow a path:
    // kart -> next checkpoint -> the checkpoint after it (checkpoints sit on the road, in driving order).
    // A ring at distance d stands d metres along that path and faces along it, so the kart drives through it.
    public static class RingPath
    {
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
