using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // "Is this saved lap from the road we race on now?" Plain C#, Edit Mode tested.
    // A ghost or best time is only fair on the same road. The track name and the checkpoint count stay the same when
    // the road between the checkpoints moves (V14 -> V15: same 12 checkpoints, lap 1364 m -> 1408 m), so every save
    // also keeps a layout stamp: the lap length of the track's own racing line (T8 TrackProgress), in whole metres.
    // Limit: a change that keeps the lap length within ToleranceMetres is not noticed.
    public static class TrackLayout
    {
        public const int Unknown = 0;          // the track has no racing line of its own (only checkpoints)
        public const int ToleranceMetres = 2;  // rounding and float noise, far below a real change of the road

        // The stamp of the road we race on: the own racing line's length, or Unknown.
        // Only the track's OWN line counts: the fallback line through the checkpoints hardly changes when the road
        // between them moves (V14 -> V15: 1130.0 m -> 1130.8 m), so it would wave old saves through.
        public static int StampOf(ITrackProgress ownProgress)
        {
            if (!TrackProgressSource.IsUsable(ownProgress))
            {
                return Unknown;
            }
            return Mathf.RoundToInt(ownProgress.LapLength);
        }

        // saved = the stamp inside the save, live = StampOf the track we race on now.
        public static bool Matches(int saved, int live)
        {
            if (live == Unknown)
            {
                return true; // nothing to compare with: name + checkpoint count decide, like before
            }
            if (saved == Unknown)
            {
                return false; // saved before the track had a line of its own: we cannot tell, so it goes
            }
            return Mathf.Abs(saved - live) <= ToleranceMetres;
        }
    }
}
