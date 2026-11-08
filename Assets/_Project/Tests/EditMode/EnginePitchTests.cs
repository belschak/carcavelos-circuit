using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the engine-sound maths (two loops crossfaded by revs).
    public class EnginePitchTests
    {
        [TestCase(0f, 20f, 0f)]
        [TestCase(10f, 20f, 0.5f)]
        [TestCase(20f, 20f, 1f)]
        [TestCase(27f, 20f, 1f)]   // boosting above top speed is capped
        [TestCase(-10f, 20f, 0.5f)] // reversing still revs the engine
        [TestCase(5f, 0f, 0f)]      // a broken MaxSpeed never divides by zero
        public void Speed01_IsBetweenZeroAndOne(float speed, float maxSpeed, float expected)
        {
            Assert.AreEqual(expected, EnginePitch.Speed01(speed, maxSpeed), 0.0001f);
        }

        [Test]
        public void MoveLimited_RisesAtMostTheRiseRate()
        {
            // 1.4 per second for 0.1 s = at most 0.14 up, even if the target is far away.
            Assert.AreEqual(0.14f, EnginePitch.MoveLimited(0f, 1f, 1.4f, 2.2f, 0.1f), 0.0001f);
        }

        [Test]
        public void MoveLimited_FallsAtMostTheFallRate()
        {
            Assert.AreEqual(0.78f, EnginePitch.MoveLimited(1f, 0f, 1.4f, 2.2f, 0.1f), 0.0001f);
        }

        [Test]
        public void MoveLimited_StopsExactlyAtTheTarget()
        {
            Assert.AreEqual(0.5f, EnginePitch.MoveLimited(0.45f, 0.5f, 1.4f, 2.2f, 0.1f), 0.0001f);
            Assert.AreEqual(0.5f, EnginePitch.MoveLimited(0.55f, 0.5f, 1.4f, 2.2f, 0.1f), 0.0001f);
        }

        [TestCase(0f, 0f)]      // standing: only the low loop
        [TestCase(0.2f, 0f)]    // crossfade starts here
        [TestCase(0.45f, 0.5f)] // halfway: both loops equally loud
        [TestCase(0.7f, 1f)]    // crossfade ends here
        [TestCase(1f, 1f)]      // top speed: only the high loop
        public void HighLoopMix_CrossfadesBetween02And07(float revs, float expected)
        {
            Assert.AreEqual(expected, EnginePitch.HighLoopMix(revs), 0.0001f);
        }

        [Test]
        public void HighLoopMix_IsSmoothAtTheEdges()
        {
            // Smoothstep starts flat: just after 0.2 the high loop is still very quiet.
            Assert.Less(EnginePitch.HighLoopMix(0.25f), 0.03f);
        }

        [TestCase(0f, 0.75f, 0.8f)]
        [TestCase(1f, 1.6f, 1.35f)]
        public void LoopPitches_FollowLucsNumbers(float revs, float expectedLow, float expectedHigh)
        {
            Assert.AreEqual(expectedLow, EnginePitch.LowLoopPitch(revs), 0.0001f);
            Assert.AreEqual(expectedHigh, EnginePitch.HighLoopPitch(revs), 0.0001f);
        }
    }
}
