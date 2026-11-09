namespace CarcavelosCircuit.RaceFlow
{
    // Decides when the race pauses and resumes (task P7). Plain C#, so it is unit tested; RaceManager does the
    // Unity part (Time.timeScale, kart input, sound).
    // - The browser tab or game window loses focus during the countdown or the race: pause.
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
            if (IsPaused || !CanPauseIn(state))
            {
                return false;
            }
            IsPaused = true;
            return true;
        }

        // Called every paused frame. Returns true on the frame the player continues.
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
