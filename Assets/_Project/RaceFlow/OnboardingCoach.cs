namespace CarcavelosCircuit.RaceFlow
{
    // Smart one-time hints for first-time players ("coach"). RaceManager feeds it the kart's drift and boost flags
    // every frame and shows whatever hint it returns in the normal hint chip. Plain C#, tested in Edit Mode.
    // Rules:
    // - Each tip is shown at most once, ever: DoneFlags is saved in PlayerPrefs, so returning players are not nagged.
    // - A tip the player clearly does not need is marked done without showing it (drifted on their own = no
    //   "how to drift"; got the big boost = no "hold longer").
    // - Nothing happens while not racing (countdown, results), and racing time only counts until the first drift.
    public class OnboardingCoach
    {
        public int DoneFlags { get; private set; }

        private readonly float noDriftSeconds;      // racing time without a drift before "how to drift"
        private readonly float boostWindowSeconds;  // a boost this soon after a drift ended belongs to that drift

        private float racingSeconds;          // racing time so far, across retries, until the first drift
        private bool wasDrifting;
        private bool wasBoosting;
        private int stageThisDrift;           // highest drift stage seen in the drift that is running now
        private int stageLastDrift;           // the same for the drift that just ended
        private float sinceDriftEnded = 999f; // seconds since the last drift ended

        public OnboardingCoach(int savedDoneFlags, float noDriftSeconds, float boostWindowSeconds = 0.25f)
        {
            DoneFlags = savedDoneFlags;
            this.noDriftSeconds = noDriftSeconds;
            this.boostWindowSeconds = boostWindowSeconds;
        }

        public bool IsDone(CoachHint hint)
        {
            return (DoneFlags & (int)hint) != 0;
        }

        // One frame. driftStage = IKartState.DriftStage (0 when not drifting). Returns a hint to show, or None.
        public CoachHint Tick(float deltaTime, bool racing, bool drifting, int driftStage, bool boosting)
        {
            if (!racing)
            {
                // Countdown, results or a retry in between: forget the drift in progress, give no tips.
                wasDrifting = false;
                wasBoosting = false;
                stageThisDrift = 0;
                stageLastDrift = 0;
                sinceDriftEnded = 999f;
                return CoachHint.None;
            }

            CoachHint result = CoachHint.None;
            sinceDriftEnded += deltaTime;

            // Drift started: the player knows how to drift, so the "how to drift" tip is no longer needed.
            if (drifting && !wasDrifting)
            {
                MarkDone(CoachHint.HowToDrift);
                stageThisDrift = 0;
            }
            if (drifting && driftStage > stageThisDrift)
            {
                stageThisDrift = driftStage;
            }

            // Drift ended. Released before the first sparks = no boost: suggest holding longer.
            if (!drifting && wasDrifting)
            {
                stageLastDrift = stageThisDrift;
                sinceDriftEnded = 0f;
                if (stageLastDrift == 0)
                {
                    result = Give(CoachHint.HoldLonger);
                }
            }

            // Boost started right after a stage 2 drift: that is the big boost, praise it once.
            if (boosting && !wasBoosting && stageLastDrift >= 2 && sinceDriftEnded <= boostWindowSeconds)
            {
                MarkDone(CoachHint.HoldLonger); // they have learned it, never tell them to hold longer
                result = Give(CoachHint.BigBoost);
            }

            // Still no drift after a while of racing: explain it.
            if (!IsDone(CoachHint.HowToDrift))
            {
                racingSeconds += deltaTime;
                if (racingSeconds >= noDriftSeconds)
                {
                    result = Give(CoachHint.HowToDrift);
                }
            }

            wasDrifting = drifting;
            wasBoosting = boosting;
            return result;
        }

        // Returns the hint and marks it done, or None if it was already shown.
        private CoachHint Give(CoachHint hint)
        {
            if (IsDone(hint))
            {
                return CoachHint.None;
            }
            MarkDone(hint);
            return hint;
        }

        private void MarkDone(CoachHint hint)
        {
            DoneFlags = DoneFlags | (int)hint;
        }

        // The text in the hint chip. Constant strings: nothing is built at runtime.
        public static string TextFor(CoachHint hint)
        {
            switch (hint)
            {
                case CoachHint.HowToDrift: return "Hold SPACE while steering to drift";
                case CoachHint.HoldLonger: return "Hold the drift until the sparks, then let go";
                case CoachHint.BigBoost: return "Nice! Orange sparks = the big boost";
                default: return "";
            }
        }

        // Tips are blue, the praise is sand coloured (see HintKind).
        public static HintKind KindFor(CoachHint hint)
        {
            return hint == CoachHint.BigBoost ? HintKind.Praise : HintKind.Tip;
        }
    }
}
