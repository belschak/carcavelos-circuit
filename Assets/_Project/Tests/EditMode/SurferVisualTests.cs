using CarcavelosCircuit.Ambience;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R12): the swap seam that lets a later imported rider model replace the code-built surfer
    // (ISurferVisual, SurferModelPuppet, SurferRiders, SurferBody.Write without the rider).
    public class SurferVisualTests
    {
        private static SurferBrain Riding()
        {
            var brain = new SurferBrain(new SurferTuning(), 0f, 1);
            brain.TryChange(SurferState.Catching);
            brain.TryChange(SurferState.Riding);
            Assert.AreEqual(SurferState.Riding, brain.State);
            return brain;
        }

        private static SurferBrain Gone()
        {
            SurferBrain brain = Riding();
            brain.TryChange(SurferState.Falling);
            for (int i = 0; i < 30; i++)
            {
                brain.Step(0.1f, null);
            }
            Assert.IsFalse(brain.Visible);
            return brain;
        }

        [Test]
        public void WithoutRider_OnlyTheBoardIsBuilt()
        {
            var vertices = new Vector3[SurferBody.VertsPerSurfer];
            var normals = new Vector3[vertices.Length];
            var colours = new Color32[vertices.Length];
            var joints = new Vector3[SurferBody.JointCount];
            var look = new SurferLook();
            look.Deck = new Color32(200, 200, 200, 255);
            look.Accent = new Color32(230, 70, 60, 255);
            Vector3 place = new Vector3(3f, 1f, -2f);
            SurferBody.Write(Riding(), Matrix4x4.Translate(place), look, 0.45f, joints, vertices, normals, colours, 0, false);
            for (int v = 0; v < SurferBody.RiderVerts; v++)
            {
                Assert.AreEqual(place, vertices[v], "rider point " + v + " folded into the board's middle");
                Assert.AreEqual(0, colours[v].a);
            }
            float nose = float.MinValue, tail = float.MaxValue;
            for (int v = SurferBody.RiderVerts; v < vertices.Length; v++)
            {
                nose = Mathf.Max(nose, vertices[v].z);
                tail = Mathf.Min(tail, vertices[v].z);
                Assert.AreEqual(1f, normals[v].magnitude, 0.01f);
                Assert.AreEqual(255, colours[v].a);
            }
            Assert.Greater(nose - tail, 1.9f, "the whole board is still there");
        }

        [Test]
        public void RiderAndBoardSpace_MoveOnlyInAFall()
        {
            Assert.AreEqual(Matrix4x4.identity, SurferBody.RiderSpace(Riding()), "riding: the rider stands on the board");
            Assert.AreEqual(Matrix4x4.identity, SurferBody.BoardSpace(Riding()));
            Assert.AreEqual(Matrix4x4.identity, SurferBody.RiderSpace(null), "no surfer: no move");
            SurferBrain falling = Riding();
            falling.TryChange(SurferState.Falling);
            falling.Step(0.6f, null);
            Vector3 rider = SurferBody.RiderSpace(falling).MultiplyPoint3x4(Vector3.zero);
            Vector3 board = SurferBody.BoardSpace(falling).MultiplyPoint3x4(Vector3.zero);
            Assert.Less(rider.y, 0f, "the rider drops into the water");
            Assert.Greater(board.y, 0f, "the board flips up");
            var paddling = new SurferBrain(new SurferTuning(), 0f, 1);
            Assert.Less(SurferBody.BoardSpace(paddling).MultiplyPoint3x4(Vector3.zero).y, 0f, "lying on it, the board sinks a little");
        }

        [Test]
        public void Puppet_FollowsTheBoard_HidesWhenTheSurferIsGone()
        {
            var go = new GameObject("puppet");
            try
            {
                SurferModelPuppet puppet = go.AddComponent<SurferModelPuppet>();
                Matrix4x4 rider = Matrix4x4.TRS(new Vector3(10f, 2f, 5f), Quaternion.Euler(0f, 40f, 0f), Vector3.one * 1.2f);
                var joints = new Vector3[SurferBody.JointCount];
                puppet.Show(Riding(), rider, joints);
                Assert.IsTrue(go.activeSelf);
                Assert.Less(Vector3.Distance(new Vector3(10f, 2f, 5f), go.transform.position), 0.001f);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 130f, 0f), go.transform.rotation), 0.1f, "turned 90 degrees: stands sideways");
                Assert.AreEqual(1.2f, go.transform.localScale.x, 0.001f);

                puppet.Show(Gone(), rider, joints);
                Assert.IsFalse(go.activeSelf, "gone after a fall");
                puppet.Show(Riding(), rider, joints);
                Assert.IsTrue(go.activeSelf, "back again");
                puppet.Hide();
                Assert.IsFalse(go.activeSelf);
                Assert.DoesNotThrow(() => puppet.Show(null, rider, null), "no brain: hidden, no crash (no Animator either)");
                Assert.IsFalse(go.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Riders_OneCopyPerSurfer_OnlyForAModelWithAPuppet()
        {
            var parent = new GameObject("ambience");
            var plain = new GameObject("plain model");
            var rigged = new GameObject("rigged model");
            rigged.AddComponent<SurferModelPuppet>();
            try
            {
                Assert.IsNull(SurferRiders.Spawn(null, parent.transform, 3), "no model: built in code");
                Assert.IsNull(SurferRiders.Spawn(rigged, parent.transform, 0), "no surfers: nothing");
                Assert.IsNull(SurferRiders.Spawn(plain, parent.transform, 3), "a model without a puppet is not used");
                Assert.AreEqual(0, parent.transform.childCount, "and nothing was copied for it");

                ISurferVisual[] riders = SurferRiders.Spawn(rigged, parent.transform, 3);
                Assert.AreEqual(3, riders.Length);
                Assert.AreEqual(3, parent.transform.childCount);
                for (int i = 0; i < riders.Length; i++)
                {
                    Assert.IsNotNull(riders[i]);
                    Assert.IsFalse(parent.transform.GetChild(i).gameObject.activeSelf, "hidden until its surfer shows up");
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(plain);
                Object.DestroyImmediate(rigged);
            }
        }
    }
}
