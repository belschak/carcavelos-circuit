using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Tests for the arm maths that keeps the driver's hands on the steering wheel (V3, TwoBoneIK).
    public class TwoBoneIKTests
    {
        private static readonly Vector3 Shoulder = Vector3.zero;
        private const float Upper = 0.13f, Lower = 0.125f;

        [Test]
        public void ElbowKeepsBothBoneLengths()
        {
            Vector3 target = new Vector3(0.05f, -0.05f, 0.2f);
            Vector3 elbow = TwoBoneIK.ElbowPosition(Shoulder, Upper, Lower, target, new Vector3(0.2f, -0.1f, 0.1f));
            Assert.AreEqual(Upper, Vector3.Distance(Shoulder, elbow), 0.0001f, "upper arm length");
            Assert.AreEqual(Lower, Vector3.Distance(elbow, target), 0.0001f, "forearm length: the hand reaches the target");
        }

        [Test]
        public void ElbowBendsTowardsThePole()
        {
            Vector3 target = new Vector3(0f, 0f, 0.2f);
            Vector3 down = TwoBoneIK.ElbowPosition(Shoulder, Upper, Lower, target, new Vector3(0f, -1f, 0.1f));
            Vector3 left = TwoBoneIK.ElbowPosition(Shoulder, Upper, Lower, target, new Vector3(-1f, 0f, 0.1f));
            Assert.Less(down.y, -0.01f, "pole below: elbow points down");
            Assert.Less(left.x, -0.01f, "pole to the left: elbow points left");
        }

        [Test]
        public void TargetOutOfReach_ArmPointsStraightAtIt()
        {
            Vector3 target = new Vector3(0f, 0f, 1f);   // much further than 0.255 m
            Vector3 elbow = TwoBoneIK.ElbowPosition(Shoulder, Upper, Lower, target, Vector3.down);
            Assert.AreEqual(0f, elbow.x, 0.001f);
            // ElbowPosition keeps 0.1 mm short of full reach, which leaves a 4 mm bend: nearly straight.
            Assert.AreEqual(0f, elbow.y, 0.005f, "arm nearly straight, no real sideways bend");
            Assert.AreEqual(Upper, elbow.z, 0.001f);
        }

        [Test]
        public void PoleOnTheLine_StillGivesAnElbow()
        {
            Vector3 target = new Vector3(0f, 0f, 0.2f);
            Vector3 elbow = TwoBoneIK.ElbowPosition(Shoulder, Upper, Lower, target, new Vector3(0f, 0f, 0.1f));
            Assert.IsFalse(float.IsNaN(elbow.x) || float.IsNaN(elbow.y) || float.IsNaN(elbow.z));
            Assert.AreEqual(Upper, Vector3.Distance(Shoulder, elbow), 0.0001f);
        }
    }
}
