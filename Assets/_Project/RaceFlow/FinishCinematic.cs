using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The finish cinematic after the line (Luc, 16 Nov 2026), timing and camera path in one place, so the HUD
    // (flag, headline, results card) and the camera (ChaseCameraRig) stay in step. Plain C#, Edit Mode tests in
    // FinishCinematicTests. All times are REAL seconds since the kart crossed the line (Time.unscaledTime), because
    // FinishMoment's slow motion changes Time.timeScale during the first 1.2 s.
    //
    //   0.0 s  a checkered flag waves in over the screen (slow motion of FinishMoment runs underneath)
    //   1.0 s  the camera starts to swing round the kart, from behind to in front of it (1.8 s, eased)
    //   2.2 s  the flag waves out; the place ("2ND") or the lap time lands in the middle of the screen
    //   2.8 s  the camera is in front: the driver cruises on towards it, the kart drives itself (autopilot)
    //   5.0 s  the results card comes in (a click or a key brings it earlier, from 1.5 s on)
    // Reduced motion: no waving, no swing (the camera stays behind the kart); flag and headline at once, card at 2 s.
    public static class FinishCinematic
    {
        public const float FlagOutSeconds = 2.2f;
        public const float SwingStartSeconds = 1.0f;
        public const float SwingSeconds = 1.8f;
        public const float HeadlineInSeconds = 2.2f;
        public const float ResultsCardSeconds = 5.0f;
        public const float SkipAllowedSeconds = 1.5f;      // a key before this could still be the player's finger on the gas
        public const float ReducedResultsCardSeconds = 2.0f;

        // Camera in the kart's yaw frame (x right, y up, z forward), metres. Start = the chase camera's own offset.
        public const float StartAngleDegrees = 180f;       // straight behind
        public const float EndAngleDegrees = 18f;          // in front, a little to the right, so the driver is not hidden by the wheel
        public const float StartDistance = 6.7f;
        public const float EndDistance = 5.4f;
        public const float StartHeight = 3f;
        public const float EndHeight = 1.25f;
        public const float StartLookHeight = 1f;
        public const float EndLookHeight = 0.75f;

        // Same window with and without reduced motion: only the waving stops (FlagWave), a fade is not motion.
        public static bool FlagShown(float seconds)
        {
            return seconds >= 0f && seconds < FlagOutSeconds;
        }

        public static bool HeadlineShown(float seconds, bool reducedMotion)
        {
            return seconds >= (reducedMotion ? 0f : HeadlineInSeconds);
        }

        public static float ResultsCardDelay(bool reducedMotion)
        {
            return reducedMotion ? ReducedResultsCardSeconds : ResultsCardSeconds;
        }

        // May a key or click bring the results card early now? Never in the first moment after the line.
        public static bool MaySkip(float seconds)
        {
            return seconds >= SkipAllowedSeconds;
        }

        // 0 = behind the kart, 1 = in front. Smoothstep, so the swing starts and stops gently. NaN = 0.
        public static float SwingProgress(float seconds, bool reducedMotion)
        {
            if (reducedMotion || !(seconds > SwingStartSeconds))
            {
                return 0f;
            }
            float t = (seconds - SwingStartSeconds) / SwingSeconds;
            if (t >= 1f)
            {
                return 1f;
            }
            return t * t * (3f - 2f * t);
        }

        // Where the camera sits at a swing progress, in the kart's yaw frame. It travels round the RIGHT side
        // of the kart (angle 180 -> 18 degrees, measured from the kart's nose), coming lower and closer.
        public static Vector3 CameraOffset(float progress)
        {
            float p = Mathf.Clamp01(float.IsNaN(progress) ? 0f : progress);
            float angle = Mathf.Lerp(StartAngleDegrees, EndAngleDegrees, p) * Mathf.Deg2Rad;
            float distance = Mathf.Lerp(StartDistance, EndDistance, p);
            float height = Mathf.Lerp(StartHeight, EndHeight, p);
            return new Vector3(Mathf.Sin(angle) * distance, height, Mathf.Cos(angle) * distance);
        }

        // Height above the kart's pivot that the camera looks at.
        public static float LookHeight(float progress)
        {
            float p = Mathf.Clamp01(float.IsNaN(progress) ? 0f : progress);
            return Mathf.Lerp(StartLookHeight, EndLookHeight, p);
        }

        // The flag's wave: sideways sway of one column of the flag (pixels), a travelling sine. Zero with reduced motion.
        public static float FlagWave(float seconds, int column, bool reducedMotion)
        {
            if (reducedMotion || float.IsNaN(seconds))
            {
                return 0f;
            }
            return Mathf.Sin(seconds * 9f - column * 0.8f) * (4f + column * 2.2f);
        }
    }
}
