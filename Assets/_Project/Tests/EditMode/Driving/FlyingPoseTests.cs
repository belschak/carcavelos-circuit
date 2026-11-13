using CarcavelosCircuit.Driving;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Tests for the kart model's lean in the air (D10): level on the ground, nose up when rising,
    // nose down when falling, clamped angles, a small bounded wobble, and bad numbers never tilt the kart.
    public class FlyingPoseTests
    {
        // Numbers used: at most 20 degrees, reached at 8 m/s vertical speed.
        private const float MaxPitch = 20f;
        private const float FullSpeed = 8f;

        [Test]
        public void OnTheGround_IsLevel_EvenWithBigVerticalSpeed()
        {
            Assert.AreEqual(0f, FlyingPose.PitchDegrees(true, 12f, MaxPitch, FullSpeed), 0.0001f);
            Assert.AreEqual(0f, FlyingPose.PitchDegrees(true, -12f, MaxPitch, FullSpeed), 0.0001f);
        }

        [Test]
        public void Rising_NoseUp()
        {
            Assert.AreEqual(10f, FlyingPose.PitchDegrees(false, 4f, MaxPitch, FullSpeed), 0.0001f);
        }

        [Test]
        public void Falling_NoseDown()
        {
            Assert.AreEqual(-10f, FlyingPose.PitchDegrees(false, -4f, MaxPitch, FullSpeed), 0.0001f);
        }

        [Test]
        public void TopOfTheJump_IsLevel()
        {
            Assert.AreEqual(0f, FlyingPose.PitchDegrees(false, 0f, MaxPitch, FullSpeed), 0.0001f);
        }

        [Test]
        public void VerySteep_IsClampedToTheMaximum()
        {
            Assert.AreEqual(20f, FlyingPose.PitchDegrees(false, 50f, MaxPitch, FullSpeed), 0.0001f);
            Assert.AreEqual(-20f, FlyingPose.PitchDegrees(false, -50f, MaxPitch, FullSpeed), 0.0001f);
            Assert.AreEqual(20f, FlyingPose.PitchDegrees(false, float.PositiveInfinity, MaxPitch, FullSpeed), 0.0001f);
        }

        [Test]
        public void BadNumbers_StayLevel()
        {
            Assert.AreEqual(0f, FlyingPose.PitchDegrees(false, float.NaN, MaxPitch, FullSpeed), 0.0001f);
            Assert.AreEqual(0f, FlyingPose.PitchDegrees(false, 4f, 0f, FullSpeed), 0.0001f, "no angle allowed");
            Assert.AreEqual(0f, FlyingPose.PitchDegrees(false, 4f, -5f, FullSpeed), 0.0001f, "negative angle");
            Assert.AreEqual(0f, FlyingPose.PitchDegrees(false, 4f, MaxPitch, 0f), 0.0001f, "zero full speed would divide by zero");
        }

        [Test]
        public void Wobble_OnTheGroundOrAtTakeOff_IsZero()
        {
            Assert.AreEqual(0f, FlyingPose.RollDegrees(true, 1f, 3f, 1.2f), 0.0001f, "grounded");
            Assert.AreEqual(0f, FlyingPose.RollDegrees(false, 0f, 3f, 1.2f), 0.0001f, "just took off");
            Assert.AreEqual(0f, FlyingPose.RollDegrees(false, -1f, 3f, 1.2f), 0.0001f, "negative time");
        }

        [Test]
        public void Wobble_NeverExceedsTheAmplitude()
        {
            for (float t = 0.01f; t < 6f; t += 0.01f)
            {
                float roll = FlyingPose.RollDegrees(false, t, 3f, 1.2f);
                Assert.LessOrEqual(System.Math.Abs(roll), 3f + 0.0001f, "at " + t + " s");
            }
        }

        [Test]
        public void Wobble_GrowsInAfterTakeOff()
        {
            // a quarter of the way through the fade-in, the wobble is at most a quarter of what it can be
            Assert.LessOrEqual(System.Math.Abs(FlyingPose.RollDegrees(false, 0.0625f, 3f, 1.2f)), 3f * 0.25f + 0.0001f);
        }

        [Test]
        public void Wobble_NoAmplitude_IsZero()
        {
            Assert.AreEqual(0f, FlyingPose.RollDegrees(false, 1f, 0f, 1.2f), 0.0001f);
        }
    }
}
