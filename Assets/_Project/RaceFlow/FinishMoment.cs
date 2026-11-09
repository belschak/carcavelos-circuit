namespace CarcavelosCircuit.RaceFlow
{
    // Task P6b: the timing of the "finish moment" in one place, so the slow motion (RaceManager), the HUD (RaceHud)
    // and the sounds (RaceSounds) stay in step. Plain C#, so Edit Mode tests can check it.
    //
    // All times are REAL seconds (Time.unscaledTime). The slow motion changes Time.timeScale, so scaled time
    // (Time.time) would stretch the moment it is supposed to time.
    //
    //   0.00 s  the kart crosses the line: the game slows from full speed to 30 % (0.15 s, soft landing)
    //   0.15 s  held at 30 %: the kart glides over the line, the lap time panel grows and turns sand
    //   0.55 s  speed comes back, eased in and out (no jolt)
    //   0.60 s  the results card starts to come in (the "finish beat" is over)
    //   1.20 s  full speed again
    // Results card times (stamps, confetti) are counted from the moment the card starts to come in.
    public static class FinishMoment
    {
        public const float SlowestScale = 0.3f;        // never 0: the game must never stop by itself
        public const float SlowDownSeconds = 0.15f;    // 1 -> 0.3
        public const float HoldUntilSeconds = 0.55f;   // stay at 0.3 until here
        public const float SlowMotionSeconds = 1.2f;   // back at 1 here
        public const float BeatSeconds = 0.6f;         // the results card waits this long after the line

        // Seconds after the results card starts to come in, matched to RaceHud.uss:
        // medal badge = transition-delay 900 ms, record stamp = 1050 ms, both after the 33 ms "results--in" frame,
        // plus the part of the ease-out-back where the stamp visibly hits the card.
        public const float MedalStampLandsSeconds = 1.05f;
        public const float RecordStampLandsSeconds = 1.25f;

        // Time.timeScale at a moment of the finish moment. Before the start, after the end and for a broken
        // value (NaN) it is exactly 1, so a mistake can never leave the game slow.
        public static float TimeScaleAt(float secondsSinceFinish)
        {
            if (!(secondsSinceFinish > 0f) || secondsSinceFinish >= SlowMotionSeconds)
            {
                return 1f;
            }
            if (secondsSinceFinish < SlowDownSeconds)
            {
                // Ease-out: slows quickly, then settles on 0.3.
                float t = secondsSinceFinish / SlowDownSeconds;
                float eased = 1f - (1f - t) * (1f - t);
                return Lerp(1f, SlowestScale, eased);
            }
            if (secondsSinceFinish < HoldUntilSeconds)
            {
                return SlowestScale;
            }
            // Smoothstep: starts and ends gently, so the speed-up never feels like a jolt.
            float u = (secondsSinceFinish - HoldUntilSeconds) / (SlowMotionSeconds - HoldUntilSeconds);
            float smooth = u * u * (3f - 2f * u);
            return Lerp(SlowestScale, 1f, smooth);
        }

        // True while the slow motion still changes the time scale.
        public static bool IsSlowMotionRunning(float secondsSinceFinish)
        {
            return secondsSinceFinish >= 0f && secondsSinceFinish < SlowMotionSeconds;
        }

        // How long the results card waits. With reduced motion there is no slow motion to watch, so no wait.
        public static float BeatFor(bool reducedMotion)
        {
            return reducedMotion ? 0f : BeatSeconds;
        }

        // Confetti only when the lap earned it: a new record or a better medal than ever before.
        // An ordinary slower lap gets the plain results card, so the confetti keeps its meaning.
        public static bool Celebrates(bool newRecord, Medal medal, Medal bestMedalBefore, bool reducedMotion)
        {
            if (reducedMotion)
            {
                return false;
            }
            return newRecord || (medal != Medal.None && medal > bestMedalBefore);
        }

        // When the confetti bursts, in seconds after the results card starts to come in: with the stamp that
        // lands last (the NEW RECORD stamp if there is one, else the medal badge).
        public static float ConfettiDelay(bool newRecord)
        {
            return newRecord ? RecordStampLandsSeconds : MedalStampLandsSeconds;
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + (to - from) * t;
        }
    }
}
