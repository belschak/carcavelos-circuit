using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (audit 11): the boost whoosh on a new boost and on a chained boost, never twice for one.
    public class BoostCueTests
    {
        // One frame of a kart on the ground with input on.
        private static bool Frame(BoostCue cue, bool boosting, bool drifting, int stage, bool bumped = false)
        {
            return cue.Update(boosting, drifting, stage, true, true, bumped);
        }

        [Test]
        public void BoostStart_Whooshes_Once()
        {
            BoostCue cue = new BoostCue();
            Assert.IsFalse(Frame(cue, false, true, 1));
            Assert.IsTrue(Frame(cue, true, false, 0), "drift released, boost starts in the same frame");
            Assert.IsFalse(Frame(cue, true, false, 0));
        }

        [Test]
        public void BoostOneFrameLater_StillWhooshesOnce()
        {
            BoostCue cue = new BoostCue();
            Frame(cue, false, true, 2);
            Assert.IsFalse(Frame(cue, false, false, 0));
            Assert.IsTrue(Frame(cue, true, false, 0));
        }

        [Test]
        public void ChainedBoost_WhooshesAgain()
        {
            BoostCue cue = new BoostCue();
            Frame(cue, true, false, 0);                // first boost running
            Assert.IsFalse(Frame(cue, true, true, 1)); // new drift while boosting, sparks
            Assert.IsTrue(Frame(cue, true, false, 0), "the drift ended while boosting: the boost got longer");
        }

        [Test]
        public void DriftWithoutSparks_WhileBoosting_IsSilent()
        {
            BoostCue cue = new BoostCue();
            Frame(cue, true, false, 0);
            Frame(cue, true, true, 0);
            Assert.IsFalse(Frame(cue, true, false, 0), "stage 0 gives no boost");
        }

        [Test]
        public void DriftCancelledByAWall_IsSilent()
        {
            BoostCue cue = new BoostCue();
            Frame(cue, true, false, 0);
            Frame(cue, true, true, 2);
            Assert.IsFalse(Frame(cue, true, false, 0, true), "a wall cancels the drift without a boost");
        }

        [Test]
        public void DriftEndingInTheAirOrWithoutInput_IsSilent()
        {
            BoostCue cue = new BoostCue();
            cue.Update(true, false, 0, true, true, false);
            cue.Update(true, true, 1, true, true, false);
            Assert.IsFalse(cue.Update(true, false, 0, false, true, false), "in the air the drift ends without a boost");
            cue.Update(true, true, 1, true, true, false);
            Assert.IsFalse(cue.Update(true, false, 0, true, false, false), "finish line: input off ends it without a boost");
        }

        [Test]
        public void Reset_ForgetsTheRunningBoost()
        {
            BoostCue cue = new BoostCue();
            Frame(cue, true, false, 0);
            cue.Reset();
            Assert.IsTrue(Frame(cue, true, false, 0), "a boost right after a teleport is a new boost");
        }
    }
}
