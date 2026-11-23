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

        // ---- Pause menu (23 Nov 2026): the PAUSED card's keys, the online LEAVE THE RACE? card ----

        [Test]
        public void PausedKey_Q_GoesBackToTheMenu_EvenThoughItIsAlsoANormalKey()
        {
            Assert.AreEqual(PauseAction.BackToMenu, RacePause.PausedKey(false, true, false, true));
        }

        [Test]
        public void PausedKey_Enter_Retries_EvenThoughItIsAlsoANormalKey()
        {
            Assert.AreEqual(PauseAction.Retry, RacePause.PausedKey(false, false, true, true));
        }

        [TestCase(true, false, TestName = "P or Escape resumes")]
        [TestCase(false, true, TestName = "any other normal key resumes")]
        [TestCase(true, true, TestName = "P together with another key resumes once")]
        public void PausedKey_PEscapeOrAnotherKey_Resumes(bool pauseOrEscape, bool otherKey)
        {
            Assert.AreEqual(PauseAction.Resume, RacePause.PausedKey(pauseOrEscape, false, false, otherKey));
        }

        [Test]
        public void PausedKey_NoKey_DoesNothing_SoATapNextToTheButtonsNeverResumes()
        {
            // A click or tap is no key: only the card's buttons act on it (RaceKeys.AnyStartKeyPressed has no click).
            Assert.AreEqual(PauseAction.None, RacePause.PausedKey(false, false, false, false));
        }

        [TestCase(RaceState.Countdown, true)]
        [TestCase(RaceState.Racing, true)]
        [TestCase(RaceState.Ready, false)]
        [TestCase(RaceState.Results, false)]
        public void OnlineMayAsk_OnlyWhileThePlayersRaceRuns(RaceState state, bool expected)
        {
            Assert.AreEqual(expected, RacePause.OnlineMayAsk(state));
        }

        [TestCase(true, false, TestName = "P or Escape opens the card")]
        [TestCase(false, true, TestName = "one Q only opens the card, it never leaves at once")]
        public void OnlineKey_ClosedCard_POrQ_OpensIt(bool pauseOrEscape, bool q)
        {
            Assert.AreEqual(PauseAction.Open, RacePause.OnlineKey(false, pauseOrEscape, q));
        }

        [Test]
        public void OnlineKey_ClosedCard_NoKey_DoesNothing()
        {
            Assert.AreEqual(PauseAction.None, RacePause.OnlineKey(false, false, false));
        }

        [Test]
        public void OnlineKey_OpenCard_Q_Leaves()
        {
            Assert.AreEqual(PauseAction.Leave, RacePause.OnlineKey(true, false, true));
            Assert.AreEqual(PauseAction.Leave, RacePause.OnlineKey(true, true, true), "Q wins over P in the same frame");
        }

        [Test]
        public void OnlineKey_OpenCard_POrEscape_Continues()
        {
            Assert.AreEqual(PauseAction.Resume, RacePause.OnlineKey(true, true, false));
        }

        [Test]
        public void OnlineKey_OpenCard_NoKey_StaysOpen()
        {
            Assert.AreEqual(PauseAction.None, RacePause.OnlineKey(true, false, false));
        }
    }
}
