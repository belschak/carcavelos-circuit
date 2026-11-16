namespace CarcavelosCircuit.Online
{
    // When things happen in an online race, all on the room's shared clock (plain C#, tested in
    // OnlineStartRulesTests). The master picks StartAt = Now + StartLeadSeconds; every client begins its own
    // 3-2-1 countdown so that GO lands on StartAt on every screen, however late the control message arrived.
    public static class OnlineStartRules
    {
        public const double StartLeadSeconds = 4.0;    // room for the message to reach everyone before 3-2-1 starts
        public const float CountdownStepSeconds = 1f;  // the online countdown always counts in whole seconds
        public const double AutoStartSeconds = 5.0;    // ?autostart=N: the host waits this long after the Nth player
        public const double AutoRematchSeconds = 8.0;  // ?autostart=N: results stay this long before the next round

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

        // The phase the master should announce for a running race: Countdown until GO, Racing until everyone is
        // done (finished or left), then Results.
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
    }
}
