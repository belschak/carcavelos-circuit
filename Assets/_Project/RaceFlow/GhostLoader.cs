namespace CarcavelosCircuit.RaceFlow
{
    // Decides which ghost drives along: the player's own best lap if there is a valid one, otherwise the team's
    // shipped "Dean's ghost", otherwise none. Plain C#, so the choice is unit tested without PlayerPrefs.
    public static class GhostLoader
    {
        // PlayerPrefs key of the player's ghost, next to the personal best ("CarcavelosCircuit.Best.<track>").
        public static string SaveKey(string trackName)
        {
            return "CarcavelosCircuit.Ghost." + trackName;
        }

        // For a track without a racing line of its own (layout stamp unknown).
        public static GhostRecording Choose(string ownSave, string shippedText, string trackName, int checkpointCount, out bool isShipped)
        {
            return Choose(ownSave, shippedText, trackName, checkpointCount, TrackLayout.Unknown, out isShipped);
        }

        // ownSave = the player's ghost text from PlayerPrefs ("" if none), shippedText = the Dean's ghost file.
        // A ghost from another track, checkpoint count or road (layout stamp, TrackLayout) is skipped.
        // isShipped tells the caller whose it is.
        public static GhostRecording Choose(string ownSave, string shippedText, string trackName, int checkpointCount,
            int liveLayoutStamp, out bool isShipped)
        {
            isShipped = false;
            GhostRecording own = GhostRecording.FromSaveString(ownSave, trackName, checkpointCount, liveLayoutStamp);
            if (own != null && own.Count > 1)
            {
                return own;
            }
            GhostRecording shipped = GhostRecording.FromSaveString(shippedText, trackName, checkpointCount, liveLayoutStamp);
            if (shipped != null && shipped.Count > 1)
            {
                isShipped = true;
                return shipped;
            }
            return null;
        }
    }
}
