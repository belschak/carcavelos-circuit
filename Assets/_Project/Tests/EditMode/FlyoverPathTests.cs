using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the maths of the map fly-over camera.
    public class FlyoverPathTests
    {
        [Test]
        public void Measure_Square_CentreInTheMiddle_RadiusToTheFarthestPoint()
        {
            Vector3[] square = { new Vector3(-10f, 0f, -10f), new Vector3(10f, 0f, -10f), new Vector3(10f, 4f, 10f), new Vector3(-10f, 0f, 10f) };
            Vector3 centre;
            float radius;
            FlyoverPath.Measure(square, out centre, out radius);
            Assert.AreEqual(0f, centre.x, 0.001f);
            Assert.AreEqual(0f, centre.z, 0.001f);
            Assert.AreEqual(Mathf.Sqrt(200f), radius, 0.001f); // flat distance, the height of one point does not count
        }

        [Test]
        public void Measure_NoPoints_IsZero()
        {
            Vector3 centre;
            float radius;
            FlyoverPath.Measure(null, out centre, out radius);
            Assert.AreEqual(Vector3.zero, centre);
            Assert.AreEqual(0f, radius);
        }

        [Test]
        public void Orbit_BigTrack_HighAndFarEnoughToSeeAllOfIt()
        {
            Vector3 p = FlyoverPath.OrbitPosition(Vector3.zero, 200f, 0f, 35f);
            Assert.AreEqual(120f, p.y, 0.001f);   // 0.6 x radius
            Assert.AreEqual(180f, p.x, 0.001f);   // 0.9 x radius, angle 0 = +x
        }

        [Test]
        public void Orbit_TinyTrack_NeverLowerThanTheMinimum()
        {
            Vector3 p = FlyoverPath.OrbitPosition(new Vector3(0f, 5f, 0f), 10f, 90f, 35f);
            Assert.AreEqual(40f, p.y, 0.001f);     // centre 5 + 35
            Assert.AreEqual(35f, p.z, 0.001f);     // angle 90 = +z
        }

        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        public void Ease_StaysInsideZeroToOne(float t, float expected)
        {
            Assert.AreEqual(expected, FlyoverPath.Ease(t), 0.0001f);
        }

        [Test]
        public void Glide_EndsExactlyAtTheTarget_WithAHumpOnTheWay()
        {
            Vector3 from = new Vector3(0f, 100f, 0f);
            Vector3 to = new Vector3(50f, 2f, 0f);
            Assert.Less(Vector3.Distance(from, FlyoverPath.Glide(from, to, 0f, 12f)), 0.001f);
            Assert.Less(Vector3.Distance(to, FlyoverPath.Glide(from, to, 1f, 12f)), 0.001f);
            Vector3 middle = FlyoverPath.Glide(from, to, 0.5f, 12f);
            Assert.AreEqual(51f + 12f, middle.y, 0.001f); // halfway height + the full hump
        }

        // V28 mood preview: from the land side, looking towards the sun, a low sun in the strip along the top edge.
        [Test]
        public void PreviewPose_LowSun_StandsOppositeTheSunAndLooksAtIt()
        {
            Vector3 towardsSun = new Vector3(-0.94f, 0.1f, -0.34f).normalized;    // low, west-south-west
            Vector3 position;
            Quaternion rotation;
            FlyoverPath.PreviewPose(Vector3.zero, 300f, towardsSun, 60f, 35f, out position, out rotation);
            Vector3 flatSun = new Vector3(towardsSun.x, 0f, towardsSun.z).normalized;
            Assert.Less(Vector3.Dot(new Vector3(position.x, 0f, position.z).normalized, flatSun), -0.99f, "camera on the far side");
            Assert.AreEqual(75f, position.y, 0.01f);                               // radius * 0.25
            Vector3 look = rotation * Vector3.forward;
            Assert.Greater(Vector3.Dot(new Vector3(look.x, 0f, look.z).normalized, flatSun), 0.99f, "looks towards the sun");
            Assert.Less(look.y, 0f, "a low sun: looks down onto the campus");
            float sunAboveCentre = Vector3.SignedAngle(look, towardsSun, rotation * Vector3.right) * -1f;
            Assert.That(sunAboveCentre, Is.InRange(22f, 29f), "sun in the strip along the top edge, inside the 60 deg picture");
        }

        [Test]
        public void PreviewPose_HighSun_TiltIsCappedSoTheCampusStaysInThePicture()
        {
            Vector3 towardsSun = new Vector3(-0.53f, 0.67f, -0.53f).normalized;    // noon: 42 deg high, south-west
            Vector3 position;
            Quaternion rotation;
            FlyoverPath.PreviewPose(Vector3.zero, 300f, towardsSun, 60f, 35f, out position, out rotation);
            Vector3 look = rotation * Vector3.forward;
            float pitch = Mathf.Asin(look.y) * Mathf.Rad2Deg;
            Assert.AreEqual(-10f, pitch, 0.01f, "the noon sun is out of the picture anyway: look down onto campus and sea");
        }

        [Test]
        public void PreviewPose_SunStraightUp_LooksSouthToTheSea()
        {
            Vector3 position;
            Quaternion rotation;
            FlyoverPath.PreviewPose(Vector3.zero, 10f, Vector3.up, 60f, 35f, out position, out rotation);
            Vector3 look = rotation * Vector3.forward;
            Assert.Less(look.z, -0.9f);
            Assert.AreEqual(35f, position.y, 0.01f, "small track: at least minHeight");
        }
    }
}
