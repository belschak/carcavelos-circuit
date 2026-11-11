using System.Collections.Generic;
using CarcavelosCircuit.Ambience;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R12): finding the beach next to the racing line, with a made-up ground instead of a scene.
    public class BeachScannerTests
    {
        // Land in the north, a sloping beach, sea in the south: ground height = 5 m at z = 0, dropping 0.1 m per
        // metre south, so the waterline (height 0) is at z = -50. Optional hole without ground (like a tunnel cut)
        // and an optional rock out at sea.
        private class SlopedBeach : IGroundProbe
        {
            public float HoleFromZ = float.NaN, HoleToZ = float.NaN;
            public float RockFromZ = float.NaN, RockToZ = float.NaN;
            public float Tilt;               // the coast turns: waterline z changes by Tilt per metre of x

            public bool TryGetHeight(float x, float z, out float height)
            {
                height = 5f + 0.1f * (z - Tilt * x);
                if (!float.IsNaN(HoleFromZ) && z <= HoleFromZ && z >= HoleToZ)
                {
                    return false;
                }
                if (!float.IsNaN(RockFromZ) && z <= RockFromZ && z >= RockToZ)
                {
                    height = 2f;
                }
                if (height < -3f)
                {
                    height = -3f;               // the sea floor
                }
                return true;
            }
        }

        // The sea: a big flat plane at y = 0 under the whole world, like Luc's.
        private static readonly Bounds Sea = new Bounds(new Vector3(0f, 0f, -1000f), new Vector3(4000f, 0f, 2000f));

        private static List<Vector3> Track()
        {
            return new List<Vector3> { new Vector3(-20f, 5f, 0f), new Vector3(20f, 5f, 0f), new Vector3(0f, 9f, 40f) };
        }

        [Test]
        public void FindsTheWaterline_FromTheClosestTrackPoint()
        {
            var scanner = new BeachScanner(new SlopedBeach(), 1f, 200f);
            BeachLine beach = scanner.Scan(Sea, 0f, Track(), 100f, 21, 60f);
            Assert.IsNotNull(beach, scanner.Problem);
            Assert.AreEqual(-50f, beach.Origin.z, 1.1f, "the waterline is where the ground reaches the sea level");
            Assert.AreEqual(0f, beach.Origin.y, 0.0001f);
            Assert.Less(Vector3.Distance(new Vector3(0f, 0f, -1f), beach.Seaward), 0.01f, "out to sea = south");
            Assert.AreEqual(0f, beach.Along.y, 0.0001f);
            Assert.AreEqual(0f, Vector3.Dot(beach.Along, beach.Seaward), 0.0001f, "along the coast, square to the sea");
            for (int i = 0; i < beach.Samples; i++)
            {
                Assert.AreEqual(0f, beach.Waterline[i], 1.1f);
                Assert.AreEqual(1f, beach.Open[i], 0.0001f);
                Assert.AreEqual(0.1f, beach.Slope[i], 0.02f, "the sand rises 0.1 m per metre");
            }
        }

        [Test]
        public void TurnedCoast_TheFrameTurnsWithIt()
        {
            var ground = new SlopedBeach();
            ground.Tilt = 0.2f;                          // the waterline runs at about 11 degrees
            var scanner = new BeachScanner(ground, 0.5f, 200f);
            BeachLine beach = scanner.Scan(Sea, 0f, Track(), 100f, 21, 60f);
            Assert.IsNotNull(beach, scanner.Problem);
            float angle = Vector3.Angle(new Vector3(1f, 0f, 0.2f).normalized, beach.Along);
            if (angle > 90f)
            {
                angle = 180f - angle;                    // either way along the coast is fine
            }
            Assert.Less(angle, 2f);
            for (int i = 0; i < beach.Samples; i++)
            {
                Assert.AreEqual(0f, beach.Waterline[i], 1.5f, "the base line now follows the coast");
            }
        }

        [Test]
        public void HoleInTheGround_IsNotTakenForTheSea()
        {
            var ground = new SlopedBeach();
            ground.HoleFromZ = -10f;
            ground.HoleToZ = -18f;                       // 8 m without ground, then land again
            var scanner = new BeachScanner(ground, 1f, 200f);
            BeachLine beach = scanner.Scan(Sea, 0f, Track(), 100f, 11, 60f);
            Assert.IsNotNull(beach, scanner.Problem);
            Assert.AreEqual(-50f, beach.Origin.z, 1.1f);
        }

        [Test]
        public void EndOfTheGroundMesh_CountsAsOpenSea()
        {
            var scanner = new BeachScanner(new SlopedBeach(), 1f, 200f);
            float distance;
            // From z = 0, ground ends at z = -30 here (hole to -200): 20 m of nothing in a row = the sea.
            var ground = new SlopedBeach();
            ground.HoleFromZ = -30f;
            ground.HoleToZ = -500f;
            scanner = new BeachScanner(ground, 1f, 200f);
            Assert.IsTrue(scanner.WalkToWater(Vector3.zero, Vector3.back, 0f, 200f, out distance));
            Assert.AreEqual(29.5f, distance, 0.6f);
        }

        [Test]
        public void StartingInTheWater_IsNotABeach()
        {
            var scanner = new BeachScanner(new SlopedBeach(), 1f, 200f);
            float distance;
            Assert.IsFalse(scanner.WalkToWater(new Vector3(0f, 0f, -80f), Vector3.back, 0f, 100f, out distance));
        }

        [Test]
        public void TrackTooFarFromTheSea_NoBeach()
        {
            var scanner = new BeachScanner(new SlopedBeach(), 1f, 30f);
            BeachLine beach = scanner.Scan(Sea, 0f, Track(), 100f, 11, 60f);
            Assert.IsNull(beach);
            StringAssert.Contains("no water", scanner.Problem);
        }

        [Test]
        public void RockOutAtSea_NotOpen_NoWaves()
        {
            var ground = new SlopedBeach();
            ground.RockFromZ = -80f;
            ground.RockToZ = -90f;                       // a reef across the whole bay
            var scanner = new BeachScanner(ground, 1f, 200f);
            BeachLine beach = scanner.Scan(Sea, 0f, Track(), 100f, 11, 60f);
            Assert.IsNull(beach);
            StringAssert.Contains("not open", scanner.Problem);
        }

        [Test]
        public void NoTrackPoints_NoGround_NoBeach()
        {
            var scanner = new BeachScanner(new SlopedBeach(), 1f, 200f);
            Assert.IsNull(scanner.Scan(Sea, 0f, new List<Vector3>(), 100f, 11, 60f));
            Assert.IsNull(scanner.Scan(Sea, 0f, null, 100f, 11, 60f));
            var blind = new BeachScanner(null, 1f, 200f);
            Assert.IsNull(blind.Scan(Sea, 0f, Track(), 100f, 11, 60f));
            Assert.AreEqual("no ground probe", blind.Problem);
        }

        [Test]
        public void BadStep_IsMadeSafe()
        {
            var scanner = new BeachScanner(new SlopedBeach(), float.NaN, float.NaN);
            float distance;
            Assert.DoesNotThrow(() => scanner.WalkToWater(Vector3.zero, Vector3.back, 0f, 10f, out distance));
        }

        [Test]
        public void FillGapsAndSmooth()
        {
            float[] values = { float.NaN, 2f, float.NaN, float.NaN, 8f };
            BeachScanner.FillGaps(values);
            Assert.AreEqual(2f, values[0]);
            Assert.AreEqual(2f, values[2], "nearest measured neighbour");
            float[] spike = { 0f, 0f, 9f, 0f, 0f };
            BeachScanner.Smooth(spike);
            Assert.AreEqual(3f, spike[2], 0.0001f);
            Assert.AreEqual(3f, spike[1], 0.0001f);
            Assert.AreEqual(0f, spike[0], 0.0001f, "the ends stay");
        }
    }
}
