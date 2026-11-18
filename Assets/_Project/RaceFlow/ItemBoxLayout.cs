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

        // ---- Rows along the track's own racing line (package C, 17.11.2026) ----
        // The team saw rows missing (atrium), on the beach tunnel roof and in the terrace jump's landing zone. The
        // straight line from the checkpoint left the road on bends, and the ground ray from 3 m above the checkpoint
        // started above the tunnel portal. On a track with its own racing line (T8) the row now stands ON the line:
        // that line runs on the road surface, through the hall, the tunnel and the stairs.

        public const float LineRayAboveMetres = 2f;      // the ground ray starts this far above the line: under every ceiling
        public const float MaxGroundOffLineMetres = 1.5f; // the road is at most this far above or below the line
        public const float MinGroundNormalY = 0.7f;      // flatter than ~45 degrees: road, not a wall or a guard side
        public const float JumpSlope = 0.3f;             // the line falls more than 30 cm per metre: a jump edge (stairs: < 0.1)
        public const float JumpDropMetres = 1.5f;        // ... and falls more than this in total
        // No row this close after a jump edge: the karts fly over it. With its two boost pads the terrace jump throws a
        // kart about 47-61 m (BoostPadPlan), and the landing road runs straight for about 70 m after it.
        public const float ClearAfterJumpMetres = 66f;
        public const float MaxShareOfGap = 0.95f;        // a row moved out of a landing zone stays before the next checkpoint

        // Metres along the lap where the row stands: aheadMetres after the checkpoint, never more than 40 % of the
        // way to the next one (same rule as the straight-line row). gapMetres = lap metres to the next checkpoint.
        public static float RowDistance(float checkpointDistance, float gapMetres, float aheadMetres)
        {
            float ahead = Mathf.Max(0f, aheadMetres);
            if (gapMetres > 0f)
            {
                ahead = Mathf.Min(ahead, gapMetres * 0.4f);
            }
            return checkpointDistance + ahead;
        }

        // The row again, but out of a jump's landing zone: if the line drops off an edge between the checkpoint and
        // the row (the terrace jump), the row moves to ClearAfterJumpMetres after that edge, where the karts are
        // back on the road. heightAt = the line's height at a lap distance. Sampled every metre.
        public static float OutOfJumpLanding(float checkpointDistance, float rowDistance, float gapMetres,
            System.Func<float, float> heightAt)
        {
            if (heightAt == null || rowDistance <= checkpointDistance)
            {
                return rowDistance;
            }
            float limit = gapMetres > 0f ? checkpointDistance + gapMetres * MaxShareOfGap : float.MaxValue;
            float row = rowDistance;
            float d = checkpointDistance;
            float runStart = float.NaN; // where the current steep run began
            float runTop = 0f;
            int guard = 0;
            while (d < row && guard++ < 4000)
            {
                float here = heightAt(d);
                float next = heightAt(d + 1f);
                bool steep = here - next > JumpSlope;
                if (steep && float.IsNaN(runStart))
                {
                    runStart = d;
                    runTop = here;
                }
                if (!steep && !float.IsNaN(runStart))
                {
                    if (runTop - here > JumpDropMetres)
                    {
                        row = Mathf.Max(row, Mathf.Min(runStart + ClearAfterJumpMetres, limit)); // keep scanning: a second drop
                    }
                    runStart = float.NaN;
                }
                d += 1f;
            }
            if (!float.IsNaN(runStart) && runTop - heightAt(d) > JumpDropMetres)
            {
                row = Mathf.Max(row, Mathf.Min(runStart + ClearAfterJumpMetres, limit)); // the drop runs into the row
            }
            return row;
        }

        // Which ray hit is the road under a box: the one closest to the line's height that faces up and lies within
        // MaxGroundOffLineMetres of it (maxOffLine < 0 = any height, for tracks without their own line).
        // -1 = no road there (over the jump gap, inside a building, off the map).
        public static int PickGround(float lineHeight, float[] hitHeights, float[] normalYs, float maxOffLine)
        {
            int best = -1;
            float bestGap = float.MaxValue;
            int count = hitHeights == null || normalYs == null ? 0 : Mathf.Min(hitHeights.Length, normalYs.Length);
            for (int i = 0; i < count; i++)
            {
                if (normalYs[i] < MinGroundNormalY)
                {
                    continue; // a wall, a guard or a roof edge seen from the side
                }
                float gap = Mathf.Abs(hitHeights[i] - lineHeight);
                if (maxOffLine >= 0f && gap > maxOffLine)
                {
                    continue; // a roof above or a floor far below the road
                }
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }
            return best;
        }

        // Where to try a box that does not fit on its spot (a pillar, a guard, a lamp post), in order:
        // x = metres along the road (+ = ahead), y = share of the way to the row's middle (0 = stay in its lane).
        // The first entry is the spot itself. Small steps first, so the row keeps its shape.
        public static readonly Vector2[] Nudges =
        {
            new Vector2(0f, 0f),
            new Vector2(2f, 0f), new Vector2(-2f, 0f),
            new Vector2(4f, 0f), new Vector2(-4f, 0f),
            new Vector2(0f, 0.5f), new Vector2(2f, 0.5f), new Vector2(-2f, 0.5f),
            new Vector2(6f, 0f), new Vector2(-6f, 0f),
        };

        // Two boxes closer than this (centre to centre) would overlap: a nudged box must keep its distance.
        public static bool IsClearOfBoxes(Vector3 position, System.Collections.Generic.IList<Vector3> placed, float minDistance)
        {
            if (placed == null)
            {
                return true;
            }
            for (int i = 0; i < placed.Count; i++)
            {
                if ((placed[i] - position).sqrMagnitude < minDistance * minDistance)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
