namespace CarcavelosCircuit.RaceFlow
{
    // The rules of the three race modes in one place (plain C#, Edit Mode tests in RaceModeRulesTests).
    // RaceManager asks if a mode may be picked, BotSquad asks if bots race, the HUD asks for its words.
    public static class RaceModeRules
    {
        public const RaceMode Default = RaceMode.RaceVsBots;
        public const int ModeCount = 3;
        public const string ComingSoonMessage = "ONLINE RACE IS COMING SOON";

        // The mode a saved number stands for. Anything we never saved (old save, edited PlayerPrefs) = the default.
        public static RaceMode FromSaved(int saved)
        {
            if (saved < 0 || saved >= ModeCount)
            {
                return Default;
            }
            return (RaceMode)saved;
        }

        // Can the player start a race in this mode today? Online is on the menu but not built yet.
        public static bool IsPlayable(RaceMode mode)
        {
            return mode == RaceMode.RaceVsBots || mode == RaceMode.TimeAttack;
        }

        // Do the bots race in this mode? (Online would race people, not bots.)
        public static bool HasBots(RaceMode mode)
        {
            return mode == RaceMode.RaceVsBots;
        }

        // Does the finish show a place ("2ND") instead of the lap time?
        public static bool ShowsPlace(RaceMode mode)
        {
            return mode == RaceMode.RaceVsBots || mode == RaceMode.OnlineRace;
        }

        // Big word on the mode card.
        public static string Title(RaceMode mode)
        {
            switch (mode)
            {
                case RaceMode.RaceVsBots: return "RACE VS BOTS";
                case RaceMode.TimeAttack: return "TIME ATTACK";
                case RaceMode.OnlineRace: return "ONLINE RACE";
                default: return "";
            }
        }

        // Small line under the title.
        public static string Subtitle(RaceMode mode)
        {
            switch (mode)
            {
                case RaceMode.RaceVsBots: return "BEAT 5 RIVALS";
                case RaceMode.TimeAttack: return "YOU VS THE CLOCK";
                case RaceMode.OnlineRace: return "COMING SOON";
                default: return "";
            }
        }

        // 1 -> "1ST", 2 -> "2ND", 3 -> "3RD", 4 -> "4TH", 11-13 -> "TH". 0 or less = unknown = "".
        public static string Ordinal(int place)
        {
            if (place <= 0)
            {
                return "";
            }
            int lastTwo = place % 100;
            if (lastTwo >= 11 && lastTwo <= 13)
            {
                return place + "TH";
            }
            switch (place % 10)
            {
                case 1: return place + "ST";
                case 2: return place + "ND";
                case 3: return place + "RD";
                default: return place + "TH";
            }
        }

        // The big word in the middle of the screen after the finish line.
        // Place modes: "1ST" (with a known place), else the lap time. Time Attack: always the lap time.
        public static string FinishHeadline(RaceMode mode, int place, int racers, string lapTime)
        {
            if (ShowsPlace(mode) && HasKnownPlace(place, racers))
            {
                return Ordinal(place);
            }
            return lapTime;
        }

        // The line under it: "OF 6  ·  1:02.345" in a race, "LAP TIME" (or "NEW RECORD") in Time Attack.
        public static string FinishSubline(RaceMode mode, int place, int racers, string lapTime, bool newRecord)
        {
            if (ShowsPlace(mode) && HasKnownPlace(place, racers))
            {
                return "OF " + racers + "  ·  " + lapTime;
            }
            return newRecord ? "NEW RECORD" : "LAP TIME";
        }

        // A place is only shown when it makes sense: 1..racers with at least two racers.
        public static bool HasKnownPlace(int place, int racers)
        {
            return racers >= 2 && place >= 1 && place <= racers;
        }
    }
}
