using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // When things happen in an online race, all on the room's shared clock (plain C#, tested in
    // OnlineStartRulesTests). The master picks StartAt = Now + StartLeadSeconds; every client begins its own
    // 3-2-1 countdown so that GO lands on StartAt on every screen, however late the control message arrived.
    public static class OnlineStartRules
    {
        public const double StartLeadSeconds = 4.0;    // room for the message to reach everyone before 3-2-1 starts
        public const float CountdownStepSeconds = 1f;  // the online countdown counts in whole seconds when on time
        public const float MinCountdownStepSeconds = 0.05f; // the quickest step for a client that is very late
        public const double AutoStartSeconds = 5.0;    // ?autostart=N: the host waits this long after the Nth player
        public const double AutoRematchSeconds = 8.0;  // ?autostart=N: results stay this long before the next round
        // The room never waits for ever (20 Nov 2026, presentation safety): the host may be a student whose phone went
        // into a pocket. With 2+ players the host's screen starts the race by itself RoomStartSeconds after the last
        // player came in (every new player gives the class the full wait again), and the next round RoomRematchSeconds
        // after a race is over for everyone (or after the last join, whichever is later). The host can always start earlier.
        public const double RoomStartSeconds = 90.0;
        public const double RoomRematchSeconds = 45.0;
        public const int RoomAutoStartPlayers = 2;

        // The moment of GO for a race the master starts now.
        public static double StartAtFor(double now)
        {
            return now + StartLeadSeconds;
        }

        // Does this client begin its countdown now? countdownFrom = 3 means the countdown takes 3 steps before GO.
        public static bool ShouldBeginCountdown(double now, double startAt, int countdownFrom, float stepSeconds)
        {
            int steps = countdownFrom < 1 ? 1 : countdownFrom;
            float step = stepSeconds < 0.05f ? 0.05f : stepSeconds;
            return now >= startAt - steps * (double)step;
        }

        // The length of one countdown step for a client that begins now, so GO still lands on StartAt when the
        // control message came in late (bad Wi-Fi): steps = the time left / countdownFrom, at most the normal 1 s,
        // at least 0.05 s (RaceStateMachine's own floor). Already past StartAt: the quickest countdown (RaceManager
        // has no way to start the race clock part-way, so that client is a fraction of a second late, never 3 s).
        public static float CountdownStepFor(double now, double startAt, int countdownFrom)
        {
            int steps = countdownFrom < 1 ? 1 : countdownFrom;
            double step = (startAt - now) / steps;
            if (double.IsNaN(step) || step < MinCountdownStepSeconds)
            {
                return MinCountdownStepSeconds;
            }
            return step > CountdownStepSeconds ? CountdownStepSeconds : (float)step;
        }

        // The roster of a race the master starts now: the room's players in join order (IOnlineSession.Players is
        // sorted that way, the master first). A player's index in it is their grid slot (RaceRoster.PlayerSlotOverride).
        public static int[] RosterFor(IReadOnlyList<OnlinePlayer> players)
        {
            int count = players == null ? 0 : players.Count;
            int[] ids = new int[count];
            for (int i = 0; i < count; i++)
            {
                ids[i] = players[i].Id;
            }
            return ids;
        }

        // My grid slot before the first race (lobby, RaceId 0): the slot the first race will give me (my index in
        // RosterFor), so every kart stands on its own grid slot instead of all in box 1. -1 = not in the room (yet),
        // which RaceRoster reads as "no override" (slot 0).
        public static int LobbySlot(IReadOnlyList<OnlinePlayer> players, int localId)
        {
            int[] ids = RosterFor(players);
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == localId)
                {
                    return i;
                }
            }
            return -1;
        }

        // May the host start a race right now (button or Enter, and ?autostart)? Before the first race: from the
        // start screen. After a race: only once it is over for everyone, that is every roster player finished or
        // left, or the race time ran out (OnlineRaceLimitRules). Never while anyone is still racing, or the
        // others would lose their lap.
        public static bool HostMayStart(int raceId, bool atStartScreen, bool raceOver)
        {
            return raceId <= 0 ? atStartScreen : raceOver;
        }

        // The phase the master should announce for a running race: Countdown until GO, Racing until everyone is
        // done (finished, left or out of time, see OnlineRaceLimitRules), then Results.
        public static OnlinePhase PhaseFor(RaceControl control, double now, bool allDone)
        {
            if (control == null || control.RaceId == 0)
            {
                return OnlinePhase.Lobby;
            }
            if (now < control.StartAt)
            {
                return OnlinePhase.Countdown;
            }
            return allDone ? OnlinePhase.Results : OnlinePhase.Racing;
        }

        // ?autostart=N: are enough players in to start the clock? (0 = the flag is off, never.)
        public static bool AutoStartHasEnough(int wanted, int players)
        {
            return wanted >= 2 && players >= wanted;
        }

        // The moment the room starts by itself (shared clock), or NaN when it does not (fewer than 2 players).
        // lastJoinedAt = the latest JoinedAt in the room; overSince = when the last race was over on this screen
        // (NaN before the first race). The same players give the same moment on every screen in the lobby; after a
        // race each screen counts from when it saw the race end (a fraction of a second apart).
        public static double RoomAutoStartAt(int players, double lastJoinedAt, int raceId, double overSince)
        {
            if (players < RoomAutoStartPlayers || double.IsNaN(lastJoinedAt))
            {
                return double.NaN;
            }
            if (raceId <= 0)
            {
                return lastJoinedAt + RoomStartSeconds;
            }
            if (double.IsNaN(overSince))
            {
                return double.NaN; // the race is not over yet
            }
            // a player who came in while the results showed gets the same rematch wait from their join
            return (overSince > lastJoinedAt ? overSince : lastJoinedAt) + RoomRematchSeconds;
        }

        // The latest JoinedAt in the room (NaN for nobody).
        public static double LastJoinedAt(IReadOnlyList<OnlinePlayer> players)
        {
            double latest = double.NaN;
            for (int i = 0; players != null && i < players.Count; i++)
            {
                if (players[i] != null && (double.IsNaN(latest) || players[i].JoinedAt > latest))
                {
                    latest = players[i].JoinedAt;
                }
            }
            return latest;
        }

        // "RACE STARTS BY ITSELF IN 45 S" (whole 5 s steps above 10 s, so the lobby is not rebuilt every second).
        public static string AutoStartLine(double secondsLeft)
        {
            if (double.IsNaN(secondsLeft))
            {
                return "";
            }
            int s = secondsLeft <= 0.0 ? 0 : (int)System.Math.Ceiling(secondsLeft);
            if (s > 10)
            {
                s = ((s + 4) / 5) * 5;
            }
            return "RACE STARTS BY ITSELF IN " + s + " S";
        }
    }
}
