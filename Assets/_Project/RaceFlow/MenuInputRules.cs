namespace CarcavelosCircuit.RaceFlow
{
    // Who owns a key or a click on the start screen (team test 17 Nov 2026). Plain C#, Edit Mode tests in
    // MenuInputRulesTests. The start screen's menus (mode cards, SETTINGS, CREDITS, SOUND, NOON/SUNSET) tell the
    // RaceManager when this frame's input is theirs, so it does not ALSO count as "press any key".
    public static class MenuInputRules
    {
        // A mouse press (or a phone tap, which the page turns into one) on a menu button belongs to that button:
        // its ClickEvent comes when the button goes up and does the button's job. A KEY pressed while the mouse only
        // rests on a button is still a key: before this rule a mouse parked on a mode card swallowed Space and Enter.
        public static bool ClickBelongsToButton(bool clickPressed, bool pointerOnButton)
        {
            return clickPressed && pointerOnButton;
        }

        // Every mode goes through the driver select first (Mario Kart style: mode -> driver -> go). A mode that
        // cannot be picked (Online without the online build) never gets there.
        public static bool OpensDriverSelect(RaceMode mode, bool onlineAvailable)
        {
            string unused;
            return RaceModeRules.CanPick(mode, onlineAvailable, out unused);
        }

        // READY on the driver select: an offline mode starts the countdown, Online opens the online lobby
        // (name + join; the room then starts the race for everyone).
        public static bool ReadyOpensLobby(RaceMode mode)
        {
            return RaceModeRules.OpensLobby(mode);
        }

        // The orange plate under the mode cards (RaceHud). The cards are the buttons, so the plate says so; the keys
        // still work (1 2 3 on the cards' keycaps, any other key = the highlighted mode).
        public static string StartPrompt(bool isPhone)
        {
            return isPhone ? "TAP A MODE TO PLAY" : "CLICK A MODE TO PLAY";
        }
    }
}
