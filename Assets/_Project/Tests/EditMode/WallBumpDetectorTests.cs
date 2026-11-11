using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (audit 11): a wall bump is a big speed drop in one frame, never a brake.
    public class WallBumpDetectorTests
    {
        private const float Frame = 1f / 60f;

        private static WallBumpDetector Make()
        {
            return new WallBumpDetector(4f, 60f, 0.4f);
        }

        [Test]
        public void BigDropInOneFrame_IsABump()
        {
            WallBumpDetector bump = Make();
            bump.Update(20f, true, true, Frame);
            Assert.IsTrue(bump.Update(5f, true, true, Frame));
            Assert.AreEqual(15f, bump.LastDrop, 0.001f);
        }

        [Test]
        public void HardestBrake_IsNoBump_EvenInALongFrame()
        {
            WallBumpDetector bump = Make();
            float speed = 25f;
            bump.Update(speed, true, true, Frame);
            float hitch = 0.2f; // a slow laptop: 30 m/s2 x 0.2 s = 6 m/s lost in one frame
            Assert.IsFalse(bump.Update(speed - 30f * hitch, true, true, hitch));
        }

        [Test]
        public void SmallDrop_IsNoBump()
        {
            WallBumpDetector bump = Make();
            bump.Update(20f, true, true, Frame);
            Assert.IsFalse(bump.Update(17f, true, true, Frame), "a scrape along a wall loses less than 4 m/s");
        }

        [Test]
        public void InTheAir_OrWithoutInput_IsNoBump()
        {
            WallBumpDetector bump = Make();
            bump.Update(20f, false, true, Frame);
            Assert.IsFalse(bump.Update(0f, false, true, Frame), "in the air");
            bump.Update(20f, true, false, Frame);
            Assert.IsFalse(bump.Update(0f, true, false, Frame), "countdown or results");
        }

        [Test]
        public void GrindingAlongAWall_GivesOneThud_ThenOneMoreAfterTheCooldown()
        {
            WallBumpDetector bump = Make();
            bump.Update(20f, true, true, Frame);
            Assert.IsTrue(bump.Update(10f, true, true, Frame));
            bump.Update(20f, true, true, Frame);
            Assert.IsFalse(bump.Update(10f, true, true, Frame), "still in the cooldown");
            for (int i = 0; i < 30; i++)
            {
                bump.Update(20f, true, true, Frame); // half a second at speed
            }
            Assert.IsTrue(bump.Update(10f, true, true, Frame));
        }

        [Test]
        public void Reverse_CountsBySize()
        {
            WallBumpDetector bump = Make();
            bump.Update(-12f, true, true, Frame);
            Assert.IsTrue(bump.Update(0f, true, true, Frame), "backing into a wall");
        }

        [Test]
        public void FirstFrame_AndAfterReset_IsNoBump()
        {
            WallBumpDetector bump = Make();
            Assert.IsFalse(bump.Update(0f, true, true, Frame), "no speed before the first frame");
            bump.Update(20f, true, true, Frame);
            bump.Reset(); // teleport: the kart stands still now on purpose
            Assert.IsFalse(bump.Update(0f, true, true, Frame));
        }

        [Test]
        public void PausedFrame_IsNoBump()
        {
            WallBumpDetector bump = Make();
            bump.Update(20f, true, true, Frame);
            Assert.IsFalse(bump.Update(0f, true, true, 0f));
        }

        [Test]
        public void Volume_GrowsWithTheHit()
        {
            Assert.AreEqual(0.5f, WallBumpDetector.VolumeFor(4f, 4f, 15f), 0.001f);
            Assert.AreEqual(1f, WallBumpDetector.VolumeFor(15f, 4f, 15f), 0.001f);
            Assert.AreEqual(1f, WallBumpDetector.VolumeFor(30f, 4f, 15f), 0.001f, "capped");
            Assert.AreEqual(1f, WallBumpDetector.VolumeFor(5f, 4f, 4f), 0.001f, "bad settings stay safe");
        }
    }
}
