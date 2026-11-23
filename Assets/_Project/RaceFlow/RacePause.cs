namespace CarcavelosCircuit.RaceFlow
{
    // What a key or a button on the pause card does (pause menu, 23 Nov 2026). PauseAction.None = nothing.
    public enum PauseAction
    {
        None,
        Open,        // online: open the small LEAVE RACE / CONTINUE card (the race keeps running)
        Resume,      // offline: end the pause; online: close the card (CONTINUE)
        Retry,       // offline: a new attempt from the grid
        BackToMenu,  // offline: back to the start screen, the lap is forgotten
        Leave        // online: leave the room, the others keep racing and this kart counts as DNF
    }

    // Decides when the race pauses and resumes (task P7). Plain C#, so it is unit tested; RaceManager does the
    // Unity part (Time.timeScale, kart input, sound).
    // - The browser tab or game window loses focus during the countdown or the race: pause.
    // - The player presses P during the countdown or the race: the same pause (audit 9). P again continues.
    // - Nothing to pause on the start or results screen (nothing is running there).
    // - Coming back does NOT resume by itself: the player presses RESUME (or P, Escape, any other key) when ready.
    // Pause menu (23 Nov 2026, Luc: "you cannot pause during the race and go to the menu"): the PAUSED card has real
    // buttons RESUME, RETRY and BACK TO MENU, so a phone can leave too. A click or tap only counts on a button (a tap
    // next to them does nothing), so a press on BACK TO MENU can never resume first. Online a race cannot pause for
    // everyone: P, Escape or Q open a small LEAVE RACE / CONTINUE card on top of the running race (OnlineKey).
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

        // A key while the offline race is paused: Q = back to the menu, Enter = retry, P, Escape or any other key
        // = resume. Q and Enter come first, because they are "other keys" too. otherKey = a normal key
        // (RaceKeys.AnyStartKeyPressed, so never M, O, C, T or a system key), never a click.
        public static PauseAction PausedKey(bool pauseOrEscape, bool q, bool enter, bool otherKey)
        {
            if (q)
            {
                return PauseAction.BackToMenu;
            }
            if (enter)
            {
                return PauseAction.Retry;
            }
            if (pauseOrEscape || otherKey)
            {
                return PauseAction.Resume;
            }
            return PauseAction.None;
        }

        // Online the card can only be asked for while this player's race runs (countdown or racing). On the start
        // screen, in the lobby and on the results the room's own buttons and Q do it (OnlineRaceDirector).
        public static bool OnlineMayAsk(RaceState state)
        {
            return CanPauseIn(state);
        }

        // A key in a running online race. Closed card: P, Escape or Q open it (one Q never throws the race away, the
        // same rule as offline). Open card: Q (a second time) leaves, P or Escape continue.
        public static PauseAction OnlineKey(bool askOpen, bool pauseOrEscape, bool q)
        {
            if (!askOpen)
            {
                return pauseOrEscape || q ? PauseAction.Open : PauseAction.None;
            }
            if (q)
            {
                return PauseAction.Leave;
            }
            return pauseOrEscape ? PauseAction.Resume : PauseAction.None;
        }

        // Retry, or the race was set up again: never stay paused by mistake.
        public void Clear()
        {
            IsPaused = false;
        }
    }
}
