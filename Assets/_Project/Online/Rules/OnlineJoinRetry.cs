namespace CarcavelosCircuit.Online
{
    // When the lobby tries a room again by itself instead of showing "CONNECTION FAILED" (plain C#, tested in
    // OnlineJoinRetryTests). Scale test, 17 Nov 2026: two clients on one machine got ERR_CONNECTION_RESET from Photon's
    // game server at the same moment while ten others joined fine; a second try is what a player would do anyway.
    // - At most MaxAttempts tries per join (the first one included), DelayAfter apart; the count starts again
    //   once the player was in the room, so a dropped connection later also gets its tries (back into the lobby).
    // - Never for answers that will not change by trying again: a full room or server, a refused App ID or version,
    //   a build without Photon, a closed room, or the player's own cancel.
    public static class OnlineJoinRetry
    {
        // 5 tries (was 3; final scale test 20 Nov 2026: two bots on one CI machine failed 3 tries in 6 s against a
        // game server the rest of the room used fine), the waits grow (2, 3, 5, 8 s) so a short Wi-Fi gap passes.
        // RACE THE BOTS is on the screen from the second try, so nobody has to wait for the last one.
        public const int MaxAttempts = 5;
        public const float RetryDelaySeconds = 2f;
        private static readonly float[] Delays = { 2f, 3f, 5f, 8f };

        // The wait after the try `attempt` failed (1 = the first).
        public static float DelayAfter(int attempt)
        {
            int i = attempt < 1 ? 0 : attempt - 1;
            return Delays[i < Delays.Length ? i : Delays.Length - 1];
        }

        private static readonly string[] Final =
        {
            "FULL", "APP ID", "NO PHOTON", "NO ROOM NAME", "ANOTHER VERSION", "CLOSED", "CANCELLED", "NOT AVAILABLE"
        };

        // attempt = the try that just failed (1 = the first).
        public static bool ShouldRetry(string failReason, int attempt)
        {
            if (attempt >= MaxAttempts)
            {
                return false;
            }
            string reason = (failReason ?? "").ToUpperInvariant();
            for (int i = 0; i < Final.Length; i++)
            {
                if (reason.Contains(Final[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static string RetryLine(int nextAttempt)
        {
            return "CONNECTING AGAIN (TRY " + nextAttempt + " OF " + MaxAttempts + ")";
        }
    }
}
