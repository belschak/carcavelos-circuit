using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the checkpoint check that runs when the race is set up (swap-day safety net).
    public class CheckpointValidationTests
    {
        [Test]
        public void Indices_ZeroToNMinusOne_AreFine_InAnyListOrder()
        {
            Assert.AreEqual("", CheckpointValidation.FindProblem(new[] { 0, 1, 2, 3 }));
            Assert.AreEqual("", CheckpointValidation.FindProblem(new[] { 2, 0, 3, 1 }));
        }

        [Test]
        public void NoCheckpoints_IsAProblem()
        {
            Assert.AreNotEqual("", CheckpointValidation.FindProblem(new int[0]));
            Assert.AreNotEqual("", CheckpointValidation.FindProblem(null));
        }

        [Test]
        public void NumberingFromOne_IsAProblem()
        {
            // 1, 2, 3 instead of 0, 1, 2: the lap could never finish, because nothing is the start/finish line.
            string problem = CheckpointValidation.FindProblem(new[] { 1, 2, 3 });
            StringAssert.Contains("index 3", problem);
        }

        [Test]
        public void DuplicateIndex_IsAProblem()
        {
            string problem = CheckpointValidation.FindProblem(new[] { 0, 1, 1, 2 });
            StringAssert.Contains("used twice", problem);
        }

        [Test]
        public void NegativeIndex_IsAProblem()
        {
            Assert.AreNotEqual("", CheckpointValidation.FindProblem(new[] { 0, -1, 2 }));
        }
    }
}
