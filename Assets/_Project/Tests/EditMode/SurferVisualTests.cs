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
            Assert.Greater(SurferBody.BoardSpace(paddling).MultiplyPoint3x4(Vector3.zero).y, 0f, "lying on it, the board floats a little higher");
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
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 40f, 0f), go.transform.rotation), 0.1f, "turned like the board");
                Assert.AreEqual(1.2f, go.transform.localScale.x, 0.001f, "no bones: no leg scaling, only the board's size");
                Assert.IsFalse(puppet.CanPose, "no bones set: moved, not posed");

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

        // A tiny stand-in skeleton with the 18 bones a SurferModelPuppet needs, standing straight (legs 1 m long).
        private static SurferModelPuppet FakeRiggedModel(out GameObject root)
        {
            root = new GameObject("model");
            Transform pelvis = Bone("pelvis", root.transform, new Vector3(0f, 1f, 0f));
            Transform spine = Bone("spine", pelvis, new Vector3(0f, 1.05f, 0f));
            Transform neck = Bone("neck", spine, new Vector3(0f, 1.5f, 0f));
            Transform head = Bone("head", neck, new Vector3(0f, 1.6f, 0f));
            Transform[] arm = new Transform[6];
            arm[0] = Bone("upperArmL", spine, new Vector3(-0.2f, 1.45f, 0f));
            arm[1] = Bone("forearmL", arm[0], new Vector3(-0.5f, 1.45f, 0f));
            arm[2] = Bone("handL", arm[1], new Vector3(-0.75f, 1.45f, 0f));
            arm[3] = Bone("upperArmR", spine, new Vector3(0.2f, 1.45f, 0f));
            arm[4] = Bone("forearmR", arm[3], new Vector3(0.5f, 1.45f, 0f));
            arm[5] = Bone("handR", arm[4], new Vector3(0.75f, 1.45f, 0f));
            Transform[] leg = new Transform[8];
            leg[0] = Bone("thighL", pelvis, new Vector3(-0.1f, 1f, 0f));
            leg[1] = Bone("calfL", leg[0], new Vector3(-0.1f, 0.5f, 0f));
            leg[2] = Bone("footL", leg[1], new Vector3(-0.1f, 0f, 0f));
            leg[3] = Bone("toeL", leg[2], new Vector3(-0.1f, 0f, 0.15f));
            leg[4] = Bone("thighR", pelvis, new Vector3(0.1f, 1f, 0f));
            leg[5] = Bone("calfR", leg[4], new Vector3(0.1f, 0.5f, 0f));
            leg[6] = Bone("footR", leg[5], new Vector3(0.1f, 0f, 0f));
            leg[7] = Bone("toeR", leg[6], new Vector3(0.1f, 0f, 0.15f));
            SurferModelPuppet puppet = root.AddComponent<SurferModelPuppet>();
            var so = new UnityEditor.SerializedObject(puppet);
            Transform[] bones = { pelvis, spine, neck, head, arm[0], arm[1], arm[2], arm[3], arm[4], arm[5], leg[0], leg[1], leg[2], leg[3], leg[4], leg[5], leg[6], leg[7] };
            string[] fields = { "pelvis", "spine", "neck", "head", "upperArmL", "forearmL", "handL", "upperArmR", "forearmR", "handR",
                                "thighL", "calfL", "footL", "toeL", "thighR", "calfR", "footR", "toeR" };
            for (int i = 0; i < fields.Length; i++)
            {
                so.FindProperty(fields[i]).objectReferenceValue = bones[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return puppet;
        }

        private static Transform Bone(string name, Transform parent, Vector3 position)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.position = position;
            return bone;
        }

        [Test]
        public void Puppet_BendsTheModelIntoTheSurfStance()
        {
            GameObject root;
            SurferModelPuppet puppet = FakeRiggedModel(out root);
            try
            {
                Assert.IsTrue(puppet.CanPose);
                var joints = new Vector3[SurferBody.JointCount];
                SurferBrain brain = Riding();
                SurferBody.PoseJoints(SurferState.Riding, 5f, 0f, 0.45f, 0.5f, joints);   // standing, leaning into a turn
                Matrix4x4 rider = Matrix4x4.TRS(new Vector3(4f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f), Vector3.one);
                puppet.Show(brain, rider, joints);
                Transform pelvis = root.transform.Find("pelvis");
                Assert.Less(Vector3.Distance(rider.MultiplyPoint3x4(joints[SurferBody.Pelvis]), pelvis.position), 0.001f, "hips where the pose wants them");
                Assert.AreEqual(SurferModelPuppet.CodeLegLength, root.transform.localScale.x, 0.001f, "legs scaled to the code-built ones (1 m -> 0.86 m)");
                Transform thigh = pelvis.Find("thighL"), calf = thigh.Find("calfL"), foot = calf.Find("footL");
                Vector3 wantThigh = rider.MultiplyVector(joints[SurferBody.KneeL] - joints[SurferBody.HipL]).normalized;
                Vector3 wantShin = rider.MultiplyVector(joints[SurferBody.FootL] - joints[SurferBody.KneeL]).normalized;
                Assert.Greater(Vector3.Dot((calf.position - thigh.position).normalized, wantThigh), 0.999f, "thigh points at the knee");
                Assert.Greater(Vector3.Dot((foot.position - calf.position).normalized, wantShin), 0.999f, "shin points at the foot");
                Vector3 kneeBefore = calf.position;
                puppet.Show(brain, rider, joints);
                Assert.Less(Vector3.Distance(kneeBefore, calf.position), 0.0001f, "same pose twice = same result (starts from the rest pose every frame)");
                Assert.DoesNotThrow(() => puppet.Show(brain, rider, new Vector3[3]), "too few joints: only moved");
            }
            finally
            {
                Object.DestroyImmediate(root);
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
                Assert.IsNull(SurferRiders.Spawn(null, parent.transform, 3), "no models: built in code");
                Assert.IsNull(SurferRiders.Spawn(new GameObject[0], parent.transform, 3), "empty list: built in code");
                Assert.IsNull(SurferRiders.Spawn(new[] { rigged }, parent.transform, 0), "no surfers: nothing");
                Assert.IsNull(SurferRiders.Spawn(new[] { plain }, parent.transform, 3), "a model without a puppet is not used");
                Assert.IsNull(SurferRiders.Spawn(new[] { rigged, null }, parent.transform, 3), "one missing model: all built in code, never a mix");
                Assert.IsNull(SurferRiders.Spawn(new[] { rigged, plain }, parent.transform, 3));
                Assert.AreEqual(0, parent.transform.childCount, "and nothing was copied for them");

                ISurferVisual[] riders = SurferRiders.Spawn(new[] { rigged }, parent.transform, 3);
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
