using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Where the item boxes stand (task R7). Plain C#, Edit Mode tested. RaceFlow only knows the track through its
    // contract (the checkpoints and their respawn points), so the boxes go in rows across the road a few metres
    // after some checkpoints, along the road direction there. ItemSystem then checks the ground and walls for every box.
    public static class ItemBoxLayout
    {
        // Rows after checkpoint 0 (right after the start line), 0 + every, 0 + 2 * every ...
        public static bool HasRow(int checkpointIndex, int every)
        {
            if (every <= 0 || checkpointIndex < 0)
            {
                return false;
            }
            return checkpointIndex % every == 0;
        }

        // Flat driving direction from one checkpoint to the next (y ignored, so a hill does not tilt the row).
        // Forward (+Z) if the two points are on top of each other.
        public static Vector3 FlatDirection(Vector3 from, Vector3 to)
        {
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }
            return flat.normalized;
        }

        // Which way the row faces: the road direction at the checkpoint (its respawn point) if there is one.
        // The straight line to the next checkpoint is only the fallback: it cuts every bend and put rows on the grass
        // (seen top-down on Track_CampusWorld, 2026-11-10).
        public static Vector3 RowDirection(Vector3 from, Vector3 to, Vector3 roadForward)
        {
            Vector3 flat = new Vector3(roadForward.x, 0f, roadForward.z);
            if (flat.sqrMagnitude < 0.01f)
            {
                return FlatDirection(from, to);
            }
            return flat.normalized;
        }

        // The middle of the row along a given direction: aheadMetres after "from", never more than 40 % of the gap.
        public static Vector3 RowCentre(Vector3 from, Vector3 to, Vector3 direction, float aheadMetres)
        {
            float distance = Mathf.Min(aheadMetres, (to - from).magnitude * 0.4f);
            return from + direction * distance;
        }

        // "count" boxes side by side across a given driving direction, "spacing" metres apart, centred.
        public static Vector3[] RowPositions(Vector3 centre, Vector3 direction, int count, float spacing)
        {
            if (count <= 0)
            {
                return new Vector3[0];
            }
            Vector3 right = Vector3.Cross(Vector3.up, direction); // 90 degrees to the right of the driving direction
            Vector3[] positions = new Vector3[count];
            float firstOffset = -(count - 1) * spacing * 0.5f;
            for (int i = 0; i < count; i++)
            {
                positions[i] = centre + right * (firstOffset + i * spacing);
            }
            return positions;
        }

        // The middle of the row: aheadMetres after "from" towards "to", but never further than 40 % of the way,
        // so on a short gap between two checkpoints the row stays on this side of the corner.
        public static Vector3 RowCentre(Vector3 from, Vector3 to, float aheadMetres)
        {
            return RowCentre(from, to, FlatDirection(from, to), aheadMetres);
        }

        // "count" boxes side by side across the road, "spacing" metres apart, centred on the row centre.
        public static Vector3[] RowPositions(Vector3 centre, Vector3 from, Vector3 to, int count, float spacing)
        {
            return RowPositions(centre, FlatDirection(from, to), count, spacing);
        }
    }
}
