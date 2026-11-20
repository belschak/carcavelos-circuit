namespace CarcavelosCircuit.RaceFlow
{
    // Which keys RaceManager still obeys when someone else starts the race (M2 online race, plain C#, Edit Mode
    // tests in ExternalStartRulesTests). Online the lobby sets RaceManager.ExternalStartOnly: every player's
    // countdown must start at the same shared moment, so no single player may start, retry, leave or pause on
    // their own. The lobby does those for them (RaceManager.BeginRaceExternal, ResetToReadyExternal).
    public static class ExternalStartRules
    {
        // Does a key or click on the start screen start the countdown? Not while the lobby starts it, and never in
        // a mode that only starts from the lobby (Online), even if the lobby forgot to set ExternalStartOnly.
        public static bool MayStartFromKeys(bool externalStartOnly, RaceMode mode, bool menuUsesInput)
        {
            return !externalStartOnly && !menuUsesInput && RaceModeRules.StartsOnAnyKey(mode);
        }

        // Was the online mode left in this very frame (RaceManager.LeaveOnlineMode)? Then the key or click that left
        // (Q, BACK) must not also start an offline race in the same frame. leftOnlineFrame -1 = never left.
        public static bool IsLeavingFrame(int frame, int leftOnlineFrame)
        {
            return leftOnlineFrame >= 0 && frame == leftOnlineFrame;
        }

        // Enter = retry and Q = back to the menu (also the results card's buttons). Not while the lobby owns the race.
        public static bool MayRetryOrLeave(bool externalStartOnly)
        {
            return !externalStartOnly;
        }

        // Q = back to the start screen to pick another mode (RaceManager.BackToMenu). From the countdown, the race,
        // the pause and the results (20 Nov 2026: before it worked only on the results card, so a player in the wrong
        // mode had to finish a whole lap first). Never on the start screen itself (there Q is a normal "any key")
        // and never while the lobby owns the race (online, Q leaves the room instead: OnlineRaceDirector).
        public static bool MayGoBackToMenu(RaceState state, bool externalStartOnly)
        {
            return state != RaceState.Ready && MayRetryOrLeave(externalStartOnly);
        }

        // The P key pause. Not while the lobby owns the race: the others keep driving on the shared clock, and a
        // paused countdown would make this player's GO late.
        public static bool MayPauseByHand(bool externalStartOnly)
        {
            return !externalStartOnly;
        }

        // The lobby's start: only from the start screen (Ready) and only in a mode that can be raced.
        // (RaceManager ends a pause first, so a lost focus can never hold one player's countdown back.)
        public static bool MayBeginExternal(RaceState state, RaceMode mode)
        {
            return state == RaceState.Ready && RaceModeRules.IsPlayable(mode);
        }
    }
}
