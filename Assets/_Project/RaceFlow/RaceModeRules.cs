namespace CarcavelosCircuit.RaceFlow
{
    // The rules of the three race modes in one place (plain C#, Edit Mode tests in RaceModeRulesTests).
    // RaceManager asks if a mode may be picked, BotSquad asks if bots race, the HUD asks for its words.
    public static class RaceModeRules
    {
        public const RaceMode Default = RaceMode.RaceVsBots;
        public const int ModeCount = 3;

        // The mode a saved number stands for. Anything we never saved (old save, edited PlayerPrefs) = the default.
        public static RaceMode FromSaved(int saved)
        {
            if (saved < 0 || saved >= ModeCount)
            {
                return Default;
            }
            return (RaceMode)saved;
        }

        // The mode the game starts in on the next visit. Only an offline mode is remembered (IsRemembered), so a
        // saved Online (or a number we never saved) starts as the default: Online always starts from the lobby.
        public static RaceMode StartMode(int saved)
        {
            RaceMode mode = FromSaved(saved);
            return IsRemembered(mode) ? mode : Default;
        }

        // Can the player race in this mode? All three since M2 (Online races through the online lobby).
        public static bool IsPlayable(RaceMode mode)
        {
            return mode == RaceMode.RaceVsBots || mode == RaceMode.TimeAttack || mode == RaceMode.OnlineRace;
        }

        // May the start screen switch to this mode right now? Online also needs the online build (OnlineRaceHooks):
        // without it the card answers with a message and nothing changes. An unknown number is refused silently.
        public static bool CanPick(RaceMode mode, bool onlineAvailable, out string message)
        {
            message = "";
            if (!IsPlayable(mode))
            {
                return false;
            }
            if (mode == RaceMode.OnlineRace && !onlineAvailable)
            {
                message = OnlineRaceHooks.UnavailableMessage;
                return false;
            }
            return true;
        }

        // Is the mode saved in PlayerPrefs as "the last mode played"? Online is not: the next visit must not start
        // in a lobby that may not connect.
        public static bool IsRemembered(RaceMode mode)
        {
            return mode == RaceMode.RaceVsBots || mode == RaceMode.TimeAttack;
        }

        // Does picking this mode open the online lobby instead of starting the countdown?
        public static bool OpensLobby(RaceMode mode)
        {
            return mode == RaceMode.OnlineRace;
        }

        // Does any key or click on the start screen start the race? Not online: there the host starts it for everyone.
        public static bool StartsOnAnyKey(RaceMode mode)
        {
            return mode == RaceMode.RaceVsBots || mode == RaceMode.TimeAttack;
        }

        // Item boxes and items: in every mode, online too (package C, 17.11.2026). Every item only acts on your own
        // kart (a boost, your own rings, your own shield) and your kart's pose is synced, so the others see you speed
        // up. The boxes are each player's own: one you break stays whole on the other screens.
        public static bool HasItems(RaceMode mode)
        {
            return mode == RaceMode.RaceVsBots || mode == RaceMode.TimeAttack || mode == RaceMode.OnlineRace;
        }

        // The ghost kart (record and replay). Not online: the real people are on the road.
        public static bool HasGhost(RaceMode mode)
        {
            return mode != RaceMode.OnlineRace;
        }

        // Personal best, medals and the ghost save. Not online: a lap there may have been bumped or held up by
        // others, so it never replaces your time trial record.
        public static bool KeepsRecords(RaceMode mode)
        {
            return mode != RaceMode.OnlineRace;
        }

        // The "SAVE YOUR TIME" form for the leaderboard. Not online: the online room shows its own results.
        public static bool UsesLeaderboard(RaceMode mode)
        {
            return mode != RaceMode.OnlineRace;
        }

        // Is the mode card dimmed? An unknown mode, or Online in a build without the online part.
        public static bool LooksDimmed(RaceMode mode, bool onlineAvailable)
        {
            string unused;
            return !CanPick(mode, onlineAvailable, out unused);
        }

        // Which of the three mode cards is orange (Luc, 18 Nov 2026: "the mode under my mouse turns orange").
        // The orange follows the mouse, so only ONE card is orange at a time: the card under the mouse, else the
        // picked card. No orange for a dimmed card (it cannot be picked) and no hover on touch (a tap leaves the
        // hover behind, so the picked card stays orange). -1 = no card (nothing picked and nothing hovered).
        public static int OrangeCard(int picked, int hovered, bool hoveredDimmed, bool touch)
        {
            if (hovered >= 0 && hovered < ModeCount && !hoveredDimmed && !touch)
            {
                return hovered;
            }
            if (picked >= 0 && picked < ModeCount)
            {
                return picked;
            }
            return -1;
        }

        // Do the bots race in this mode? (Online races people, not bots.)
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
                case RaceMode.OnlineRace: return "RACE YOUR CLASS LIVE";
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
