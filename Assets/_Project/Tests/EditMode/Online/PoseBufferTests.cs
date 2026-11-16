using CarcavelosCircuit.Online;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Snapshot interpolation of a remote kart (M2 online race): blend between poses, extrapolate a little, then hold.
    public class PoseBufferTests
    {
        private static KartPose At(double time, float x, float speed = 10f)
        {
            KartPose pose = new KartPose();
            pose.Time = time;
            pose.Position = new Vector3(x, 0f, 0f);
            pose.Rotation = Quaternion.identity;
            pose.Speed = speed;
            return pose;
        }

        [Test]
        public void Empty_HasNothingToShow()
        {
            PoseBuffer buffer = new PoseBuffer();
            KartPose unused;
            Assert.IsFalse(buffer.TrySample(1.0, out unused));
            Assert.AreEqual(-1.0, buffer.NewestTime);
        }

        [Test]
        public void Add_NewestWins_OldOrDoubledDropped()
        {
            PoseBuffer buffer = new PoseBuffer();
            Assert.IsTrue(buffer.Add(At(1.0, 0f)));
            Assert.IsFalse(buffer.Add(At(1.0, 5f)), "the same time again is a doubled packet");
            Assert.IsFalse(buffer.Add(At(0.5, 5f)), "a late packet is older than the newest");
            Assert.IsTrue(buffer.Add(At(1.5, 5f)));
            Assert.AreEqual(2, buffer.Count);
        }

        [Test]
        public void Sample_BlendsBetweenTwoPoses()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(1.0, 0f, 0f));
            buffer.Add(At(2.0, 10f, 20f));
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(1.25, out shown));
            Assert.AreEqual(2.5f, shown.Position.x, 0.001f);
            Assert.AreEqual(5f, shown.Speed, 0.001f);
            Assert.AreEqual(1.25, shown.Time, 0.0);
        }

        [Test]
        public void Sample_BeforeOldest_ShowsOldest()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(5.0, 3f));
            buffer.Add(At(6.0, 4f));
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(1.0, out shown));
            Assert.AreEqual(3f, shown.Position.x, 0.001f);
        }

        [Test]
        public void Sample_AfterNewest_ExtrapolatesThenHolds()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(1.0, 0f));
            buffer.Add(At(2.0, 10f)); // 10 m/s
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(2.1, out shown));
            Assert.AreEqual(11f, shown.Position.x, 0.001f, "0.1 s ahead at 10 m/s");
            Assert.IsTrue(buffer.TrySample(2.25, out shown));
            Assert.AreEqual(12.5f, shown.Position.x, 0.001f, "the most we ever extrapolate");
            Assert.IsTrue(buffer.TrySample(5.0, out shown));
            Assert.AreEqual(12.5f, shown.Position.x, 0.001f, "after that the kart holds still");
        }

        [Test]
        public void OnePose_IsShownAsItIs()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(1.0, 7f));
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(9.0, out shown));
            Assert.AreEqual(7f, shown.Position.x, 0.001f);
        }

        [Test]
        public void Capacity_ForgetsTheOldest()
        {
            PoseBuffer buffer = new PoseBuffer();
            for (int i = 0; i < PoseBuffer.Capacity + 5; i++)
            {
                buffer.Add(At(i, i));
            }
            Assert.AreEqual(PoseBuffer.Capacity, buffer.Count);
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(0.0, out shown));
            Assert.AreEqual(5f, shown.Position.x, 0.001f, "poses 0..4 are gone, the oldest kept is 5");
        }

        [Test]
        public void Clear_Empties()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(1.0, 0f));
            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);
        }
    }
}
