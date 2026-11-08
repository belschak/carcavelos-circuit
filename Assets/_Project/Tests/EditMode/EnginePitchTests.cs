using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the engine-sound maths.
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

        [TestCase(0f, 0.8f)]
        [TestCase(0.5f, 1.3f)]
        [TestCase(1f, 1.8f)]
        [TestCase(2f, 1.8f)]
        public void FromSpeed01_MapsIdleToTopSpeed(float speed01, float expected)
        {
            Assert.AreEqual(expected, EnginePitch.FromSpeed01(speed01, 0.8f, 1.8f), 0.0001f);
        }

        [Test]
        public void Smooth_MovesTowardsTarget_WithoutOvershoot()
        {
            float value = EnginePitch.Smooth(1f, 2f, 6f, 0.016f);
            Assert.Greater(value, 1f);
            Assert.Less(value, 2f);
        }

        [Test]
        public void Smooth_IsFrameRateIndependent()
        {
            // 2 frames of 1/60 s must land in the same place as 1 frame of 1/30 s.
            float twoSmallFrames = EnginePitch.Smooth(EnginePitch.Smooth(1f, 2f, 6f, 1f / 60f), 2f, 6f, 1f / 60f);
            float oneBigFrame = EnginePitch.Smooth(1f, 2f, 6f, 1f / 30f);
            Assert.AreEqual(oneBigFrame, twoSmallFrames, 0.0001f);
        }
    }
}
