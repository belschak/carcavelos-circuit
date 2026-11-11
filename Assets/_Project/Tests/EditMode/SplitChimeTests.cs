using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (audit 19): the checkpoint chime is higher when ahead, lower when behind, like the split chip.
    public class SplitChimeTests
    {
        [Test]
        public void Ahead_IsTwoSemitonesHigher()
        {
            Assert.AreEqual(2, SplitChime.Semitones(true, -0.25f));
        }

        [Test]
        public void Behind_IsTwoSemitonesLower()
        {
            Assert.AreEqual(-2, SplitChime.Semitones(true, 0.4f));
        }

        [Test]
        public void NoBestLapYet_IsTheNormalChime()
        {
            Assert.AreEqual(0, SplitChime.Semitones(false, -3f), "no comparison: the delta value means nothing");
        }

        [Test]
        public void ExactlyEven_IsBehind_LikeTheChip()
        {
            Assert.AreEqual(-2, SplitChime.Semitones(true, 0f), "the chip shows +0.000 as BEHIND");
            Assert.AreEqual(-2, SplitChime.Semitones(true, -0.0004f), "rounds to 0 ms, same as the chip");
            Assert.AreEqual(2, SplitChime.Semitones(true, -0.0006f), "rounds to -1 ms: ahead");
        }

        [Test]
        public void Pitch_FollowsTheTwelfthRootOfTwo()
        {
            Assert.AreEqual(1f, SplitChime.Pitch(0), 0.0001f);
            Assert.AreEqual(1.1225f, SplitChime.Pitch(2), 0.0001f);
            Assert.AreEqual(0.8909f, SplitChime.Pitch(-2), 0.0001f);
            Assert.AreEqual(2f, SplitChime.Pitch(12), 0.0001f, "an octave doubles the pitch");
        }
    }
}
