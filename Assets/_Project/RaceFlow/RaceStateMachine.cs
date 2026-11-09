using System;

namespace CarcavelosCircuit.RaceFlow
{
    // Decides which phase the race is in: Ready -> Countdown -> Racing -> Results.
    // Plain C#: it only knows numbers and states, so we can test every transition without a scene.
    public class RaceStateMachine
    {
        public RaceState State { get; private set; } = RaceState.Ready;

        // The number on screen during the countdown: 3, 2, 1, then 0 means "GO".
        public int CountdownNumber { get; private set; }

        // The kart may only drive while we are racing.
        public bool InputAllowed
        {
            get { return State == RaceState.Racing; }
        }

        // Fired whenever the state changes (HUD and sounds listen to this).
        public event Action<RaceState> StateChanged;

        // Fired for every countdown step: 3, 2, 1 and finally 0 for GO.
        public event Action<int> CountdownStep;

        private readonly int countdownFrom;
        private float secondsPerStep;
        private float countdownSecondsLeft;

        public RaceStateMachine(int countdownFrom)
        {
            // At least "1, GO", so a bad setting can never skip the countdown completely.
            this.countdownFrom = Math.Max(1, countdownFrom);
        }

        // Ready -> Countdown (first start) or Racing/Results -> Countdown (retry).
        // The first start uses a slow countdown, a retry a quicker one, so we pass the step length in.
        public void BeginCountdown(float secondsPerStep)
        {
            // A positive step, so a bad setting can never freeze the race.
            this.secondsPerStep = Math.Max(0.05f, secondsPerStep);
            countdownSecondsLeft = countdownFrom * this.secondsPerStep;
            CountdownNumber = countdownFrom;
            SetState(RaceState.Countdown);
            if (CountdownStep != null)
            {
                CountdownStep(CountdownNumber);
            }
        }

        // Called every frame. Only does something during the countdown.
        public void Tick(float deltaTime)
        {
            if (State != RaceState.Countdown)
            {
                return;
            }

            countdownSecondsLeft -= deltaTime;

            // A while loop (not an if) so a long frame that skips two numbers still fires both steps.
            while (CountdownNumber > 0 && countdownSecondsLeft <= (CountdownNumber - 1) * secondsPerStep)
            {
                CountdownNumber = CountdownNumber - 1;
                if (CountdownStep != null)
                {
                    CountdownStep(CountdownNumber);
                }
            }

            if (CountdownNumber == 0)
            {
                SetState(RaceState.Racing);
            }
        }

        // Racing -> Results. Returns false if we were not racing (for example a second finish crossing).
        public bool Finish()
        {
            if (State != RaceState.Racing)
            {
                return false;
            }
            SetState(RaceState.Results);
            return true;
        }

        private void SetState(RaceState newState)
        {
            State = newState;
            if (StateChanged != null)
            {
                StateChanged(newState);
            }
        }
    }
}
