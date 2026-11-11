namespace CarcavelosCircuit.RaceFlow
{
    // Decides when the race pauses and resumes (task P7). Plain C#, so it is unit tested; RaceManager does the
    // Unity part (Time.timeScale, kart input, sound).
    // - The browser tab or game window loses focus during the countdown or the race: pause.
    // - The player presses P during the countdown or the race: the same pause (audit 9). P again continues.
    // - Nothing to pause on the start or results screen (nothing is running there).
    // - Coming back does NOT resume by itself: the player clicks or presses a key when ready.
    public class RacePause
    {
        public bool IsPaused { get; private set; }

        public static bool CanPauseIn(RaceState state)
        {
            return state == RaceState.Countdown || state == RaceState.Racing;
        }

        // Focus lost (or the app was sent to the background). Returns true only on the frame it starts the pause.
        public bool OnFocusLost(RaceState state)
        {
            return TryStart(state);
        }

        // The P key. Same rules as a lost focus, so both pauses behave exactly alike.
        public bool OnPauseKey(RaceState state)
        {
            return TryStart(state);
        }

        private bool TryStart(RaceState state)
        {
            if (IsPaused || !CanPauseIn(state))
            {
                return false;
            }
            IsPaused = true;
            return true;
        }

        // Called every paused frame. Returns true on the frame the player continues (any key, a click, or P again).
        public bool TryResume(bool keyOrClickPressed)
        {
            if (!IsPaused || !keyOrClickPressed)
            {
                return false;
            }
            IsPaused = false;
            return true;
        }

        // Retry, or the race was set up again: never stay paused by mistake.
        public void Clear()
        {
            IsPaused = false;
        }
    }
}
