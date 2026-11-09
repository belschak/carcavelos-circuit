using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the coach gives each tip once, at the right moment, never while not racing.
    public class OnboardingCoachTests
    {
        private const float Frame = 0.1f;

        private static OnboardingCoach NewCoach()
        {
            return new OnboardingCoach(0, 5f); // nothing shown yet, "how to drift" after 5 s without a drift
        }

        // Racing frames with no drift and no boost. Returns the first hint given, or None.
        private static CoachHint Drive(OnboardingCoach coach, float seconds)
        {
            CoachHint first = CoachHint.None;
            for (float t = 0f; t < seconds - 0.0001f; t += Frame)
            {
                CoachHint hint = coach.Tick(Frame, true, false, 0, false);
                if (first == CoachHint.None) first = hint;
            }
            return first;
        }

        // One drift that reaches maxStage, then the release frame. Returns the hint on the release frame.
        private static CoachHint DriftAndRelease(OnboardingCoach coach, int maxStage, bool boostAfter)
        {
            for (int stage = 0; stage <= maxStage; stage++)
            {
                coach.Tick(Frame, true, true, stage, false);
            }
            return coach.Tick(Frame, true, false, 0, boostAfter);
        }

        [Test]
        public void NoDrift_AfterFiveSeconds_ExplainsDrifting_Once()
        {
            OnboardingCoach coach = NewCoach();
            Assert.AreEqual(CoachHint.None, Drive(coach, 4.8f));
            Assert.AreEqual(CoachHint.HowToDrift, Drive(coach, 0.3f));
            Assert.AreEqual(CoachHint.None, Drive(coach, 20f), "never twice");
        }

        [Test]
        public void DriftingEarly_MeansNoHowToDriftTip()
        {
            OnboardingCoach coach = NewCoach();
            Drive(coach, 2f);
            DriftAndRelease(coach, 1, true);
            Assert.AreEqual(CoachHint.None, Drive(coach, 20f));
            Assert.IsTrue(coach.IsDone(CoachHint.HowToDrift), "marked done without showing it");
        }

        [Test]
        public void ReleasedAtStageZero_SaysHoldLonger_Once()
        {
            OnboardingCoach coach = NewCoach();
            Assert.AreEqual(CoachHint.HoldLonger, DriftAndRelease(coach, 0, false));
            Assert.AreEqual(CoachHint.None, DriftAndRelease(coach, 0, false), "never twice");
        }

        [Test]
        public void ReleasedAtStageOne_IsNotTold_ToHoldLonger()
        {
            OnboardingCoach coach = NewCoach();
            Assert.AreEqual(CoachHint.None, DriftAndRelease(coach, 1, true));
        }

        [Test]
        public void FirstStageTwoBoost_IsPraised_Once()
        {
            OnboardingCoach coach = NewCoach();
            Assert.AreEqual(CoachHint.BigBoost, DriftAndRelease(coach, 2, true));
            coach.Tick(Frame, true, false, 0, false); // boost over
            Assert.AreEqual(CoachHint.None, DriftAndRelease(coach, 2, true), "never twice");
        }

        [Test]
        public void BoostOneFrameLater_StillCountsAsTheBigBoost()
        {
            OnboardingCoach coach = NewCoach();
            DriftAndRelease(coach, 2, false);
            Assert.AreEqual(CoachHint.BigBoost, coach.Tick(Frame, true, false, 0, true));
        }

        [Test]
        public void AfterTheBigBoost_HoldLongerIsNeverShown()
        {
            OnboardingCoach coach = NewCoach();
            DriftAndRelease(coach, 2, true);
            coach.Tick(Frame, true, false, 0, false);
            Assert.AreEqual(CoachHint.None, DriftAndRelease(coach, 0, false));
        }

        [Test]
        public void NothingWhileNotRacing_AndThatTimeDoesNotCount()
        {
            OnboardingCoach coach = NewCoach();
            for (int i = 0; i < 100; i++) // 10 s of countdown and results
            {
                Assert.AreEqual(CoachHint.None, coach.Tick(Frame, false, false, 0, false));
            }
            Assert.AreEqual(CoachHint.None, Drive(coach, 4.8f), "only racing time counts");
        }

        [Test]
        public void ADriftEndedByTheFinish_GivesNoTip()
        {
            OnboardingCoach coach = NewCoach();
            coach.Tick(Frame, true, true, 0, false);
            Assert.AreEqual(CoachHint.None, coach.Tick(Frame, false, false, 0, false), "results: no tip");
            Assert.AreEqual(CoachHint.None, coach.Tick(Frame, true, false, 0, false), "next race: the old drift is forgotten");
        }

        [Test]
        public void SavedFlags_StopTipsForReturningPlayers()
        {
            OnboardingCoach first = NewCoach();
            DriftAndRelease(first, 0, false);
            Drive(first, 10f);
            OnboardingCoach returning = new OnboardingCoach(first.DoneFlags, 5f);
            Assert.AreEqual(CoachHint.None, Drive(returning, 10f));
            Assert.AreEqual(CoachHint.None, DriftAndRelease(returning, 0, false));
        }

        [Test]
        public void EveryTip_HasTextAndAKind()
        {
            Assert.AreNotEqual("", OnboardingCoach.TextFor(CoachHint.HowToDrift));
            Assert.AreNotEqual("", OnboardingCoach.TextFor(CoachHint.HoldLonger));
            Assert.AreNotEqual("", OnboardingCoach.TextFor(CoachHint.BigBoost));
            Assert.AreEqual(HintKind.Praise, OnboardingCoach.KindFor(CoachHint.BigBoost));
            Assert.AreEqual(HintKind.Tip, OnboardingCoach.KindFor(CoachHint.HowToDrift));
        }
    }
}
