using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the checkpoint pips on the HUD (passed / next / ahead, the last pip is the finish line).
    public class CheckpointProgressTests
    {
        [Test]
        public void StartFinishLine_DoesNotCountAsACheckpointToPass()
        {
            Assert.AreEqual(3, CheckpointProgress.CheckpointsToPass(4)); // 0 = start/finish, then 1, 2, 3
            Assert.AreEqual(0, CheckpointProgress.CheckpointsToPass(1));
            Assert.AreEqual(0, CheckpointProgress.CheckpointsToPass(0));
        }

        [Test]
        public void AtTheStart_TheFirstPipIsNext_TheRestAhead()
        {
            Assert.AreEqual(CheckpointProgress.PipState.Next, CheckpointProgress.StateOf(0, 0));
            Assert.AreEqual(CheckpointProgress.PipState.Ahead, CheckpointProgress.StateOf(1, 0));
            Assert.AreEqual(CheckpointProgress.PipState.Ahead, CheckpointProgress.StateOf(3, 0));
        }

        [Test]
        public void AfterTwoCheckpoints_TwoPassed_ThirdNext()
        {
            Assert.AreEqual(CheckpointProgress.PipState.Passed, CheckpointProgress.StateOf(0, 2));
            Assert.AreEqual(CheckpointProgress.PipState.Passed, CheckpointProgress.StateOf(1, 2));
            Assert.AreEqual(CheckpointProgress.PipState.Next, CheckpointProgress.StateOf(2, 2));
            Assert.AreEqual(CheckpointProgress.PipState.Ahead, CheckpointProgress.StateOf(3, 2));
        }

        [Test]
        public void AfterAllCheckpoints_TheFinishPipIsNext()
        {
            // 4 checkpoints on the track = 3 to pass + the finish pip (index 3).
            Assert.AreEqual(CheckpointProgress.PipState.Passed, CheckpointProgress.StateOf(2, 3));
            Assert.AreEqual(CheckpointProgress.PipState.Next, CheckpointProgress.StateOf(3, 3));
        }
    }
}
