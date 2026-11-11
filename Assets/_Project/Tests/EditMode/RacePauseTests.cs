using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task P7): when focus loss pauses the race and what ends the pause.
    public class RacePauseTests
    {
        [TestCase(RaceState.Countdown)]
        [TestCase(RaceState.Racing)]
        public void FocusLost_WhileTheRaceRuns_Pauses(RaceState state)
        {
            RacePause pause = new RacePause();
            Assert.IsTrue(pause.OnFocusLost(state));
            Assert.IsTrue(pause.IsPaused);
        }

        [TestCase(RaceState.Ready)]
        [TestCase(RaceState.Results)]
        public void FocusLost_OnTheStartOrResultsScreen_DoesNothing(RaceState state)
        {
            RacePause pause = new RacePause();
            Assert.IsFalse(pause.OnFocusLost(state));
            Assert.IsFalse(pause.IsPaused);
        }

        [Test]
        public void FocusLostTwice_StartsThePauseOnlyOnce()
        {
            RacePause pause = new RacePause();
            Assert.IsTrue(pause.OnFocusLost(RaceState.Racing));
            Assert.IsFalse(pause.OnFocusLost(RaceState.Racing), "focus and app-pause can both arrive; the second changes nothing");
            Assert.IsTrue(pause.IsPaused);
        }

        [Test]
        public void StaysPaused_UntilAKeyOrClick()
        {
            RacePause pause = new RacePause();
            pause.OnFocusLost(RaceState.Racing);
            for (int frame = 0; frame < 100; frame++)
            {
                Assert.IsFalse(pause.TryResume(false));
            }
            Assert.IsTrue(pause.IsPaused, "getting focus back alone does not resume");
            Assert.IsTrue(pause.TryResume(true));
            Assert.IsFalse(pause.IsPaused);
        }

        [Test]
        public void KeyWhileNotPaused_DoesNotCountAsResume()
        {
            RacePause pause = new RacePause();
            Assert.IsFalse(pause.TryResume(true));
        }

        // Audit 9: the P key pauses exactly like a lost focus, and P again continues.
        [TestCase(RaceState.Countdown)]
        [TestCase(RaceState.Racing)]
        public void PauseKey_WhileTheRaceRuns_Pauses(RaceState state)
        {
            RacePause pause = new RacePause();
            Assert.IsTrue(pause.OnPauseKey(state));
            Assert.IsTrue(pause.IsPaused);
        }

        [TestCase(RaceState.Ready)]
        [TestCase(RaceState.Results)]
        public void PauseKey_OnTheStartOrResultsScreen_DoesNothing(RaceState state)
        {
            RacePause pause = new RacePause();
            Assert.IsFalse(pause.OnPauseKey(state));
            Assert.IsFalse(pause.IsPaused);
        }

        [Test]
        public void PauseKey_AfterAFocusLoss_DoesNotStartASecondPause()
        {
            RacePause pause = new RacePause();
            pause.OnFocusLost(RaceState.Racing);
            Assert.IsFalse(pause.OnPauseKey(RaceState.Racing), "already paused: RaceManager treats P as 'continue' instead");
            Assert.IsTrue(pause.TryResume(true), "P while paused is passed to TryResume and continues");
            Assert.IsFalse(pause.IsPaused);
        }

        [Test]
        public void Clear_EndsThePause()
        {
            RacePause pause = new RacePause();
            pause.OnFocusLost(RaceState.Countdown);
            pause.Clear();
            Assert.IsFalse(pause.IsPaused);
            Assert.IsTrue(pause.OnFocusLost(RaceState.Racing), "a later focus loss pauses again");
        }
    }
}
