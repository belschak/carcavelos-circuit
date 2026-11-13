using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R9: where positions, the minimap and the dev test bots get "how far along the lap" from.
    // First choice: Role 2's TrackProgress (task T8) on the TrackInfo root, found with GetComponent<ITrackProgress>().
    // Until it exists: a TrackPolyline through the checkpoints in driving order (RaceManager.TrackPoints, start line
    // first). Rougher (straight pieces between checkpoints), but every track with checkpoints gets positions and a map.
    public static class TrackProgressSource
    {
        // The track's own progress if it has one and it is usable, otherwise the checkpoint fallback, otherwise null.
        public static ITrackProgress For(Component trackRoot, IList<Vector3> checkpointsInDrivingOrder)
        {
            if (trackRoot != null)
            {
                ITrackProgress own = trackRoot.GetComponent<ITrackProgress>();
                if (own != null && IsUsable(own))
                {
                    return own;
                }
            }
            return FromCheckpoints(checkpointsInDrivingOrder);
        }

        // Only the track's own progress (T8), never the checkpoint fallback; null if it has none or it is not usable.
        // For callers that need the real road: the Seagull rings and the layout stamp of saves (TrackLayout).
        public static ITrackProgress OwnOf(Component trackRoot)
        {
            if (trackRoot == null)
            {
                return null;
            }
            ITrackProgress own = trackRoot.GetComponent<ITrackProgress>();
            return IsUsable(own) ? own : null;
        }

        // The fallback: a closed line through the checkpoints. null for fewer than 3 usable points (no lap to measure).
        public static ITrackProgress FromCheckpoints(IList<Vector3> checkpointsInDrivingOrder)
        {
            TrackPolyline line = new TrackPolyline(checkpointsInDrivingOrder);
            return line.IsValid ? line : null;
        }

        // A track progress with a real lap and a line to draw. Guards against a half set-up component.
        public static bool IsUsable(ITrackProgress progress)
        {
            if (progress == null)
            {
                return false;
            }
            float length = progress.LapLength;
            IReadOnlyList<Vector3> line = progress.RacingLine;
            return !float.IsNaN(length) && length > 1f && line != null && line.Count >= 3;
        }
    }
}
