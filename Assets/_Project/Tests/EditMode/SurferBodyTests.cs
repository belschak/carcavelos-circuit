using CarcavelosCircuit.Ambience;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R12): the surfer and surfboard built in code, the poses, and the spray behind the board.
    public class SurferBodyTests
    {
        private static SurferLook Look(int hairStyle)
        {
            return Look(hairStyle, 0);
        }

        private static SurferLook Look(int hairStyle, int suitStyle)
        {
            var look = new SurferLook();
            look.Suit = new Color32(20, 20, 22, 255);
            look.SuitTop = new Color32(50, 50, 55, 255);
            look.Accent = new Color32(240, 110, 90, 255);
            look.Skin = new Color32(200, 150, 120, 255);
            look.Hair = new Color32(60, 40, 20, 255);
            look.Deck = new Color32(235, 235, 236, 255);
            look.HairStyle = hairStyle;
            look.SuitStyle = suitStyle;
            return look;
        }

        // A surfer brought into the wanted state through the allowed changes only.
        private static SurferBrain BrainIn(SurferState state)
        {
            var brain = new SurferBrain(new SurferTuning(), 0f, 1);
            if (state == SurferState.Waiting)
            {
                brain.TryChange(SurferState.Waiting);
            }
            if (state == SurferState.Catching || state == SurferState.Riding || state == SurferState.Falling || state == SurferState.Finished)
            {
                brain.TryChange(SurferState.Catching);
            }
            if (state == SurferState.Riding || state == SurferState.Falling || state == SurferState.Finished)
            {
                brain.TryChange(SurferState.Riding);
            }
            if (state == SurferState.Falling || state == SurferState.Finished)
            {
                brain.TryChange(state);
            }
            Assert.AreEqual(state, brain.State);
            return brain;
        }

        [Test]
        public void EveryState_EveryHairStyle_WritesSaneGeometry()
        {
            var vertices = new Vector3[SurferBody.VertsPerSurfer];
            var normals = new Vector3[vertices.Length];
            var colours = new Color32[vertices.Length];
            var joints = new Vector3[SurferBody.JointCount];
            SurferState[] states = { SurferState.Paddling, SurferState.Waiting, SurferState.Catching, SurferState.Riding, SurferState.Finished, SurferState.Falling };
            Vector3 place = new Vector3(5f, 0f, 5f);
            Matrix4x4 world = Matrix4x4.TRS(place, Quaternion.Euler(0f, 30f, 0f), Vector3.one * 1.3f);
            for (int style = 0; style < 4; style++)
            {
                foreach (SurferState state in states)
                {
                    SurferBrain brain = BrainIn(state);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = new Vector3(float.NaN, float.NaN, float.NaN);   // every point must be written
                    }
                    SurferBody.Write(brain, world, Look(style, style % 3), 0.45f, joints, vertices, normals, colours, 0);
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        Assert.IsFalse(float.IsNaN(vertices[v].x) || float.IsNaN(vertices[v].y) || float.IsNaN(vertices[v].z), state + " point " + v);
                        Assert.Less(Vector3.Distance(vertices[v], place), 3.5f, "a surfer fits in a 3.5 m ball");
                        Assert.AreEqual(1f, normals[v].magnitude, 0.01f, state + " normal " + v);
                        Assert.AreEqual(255, colours[v].a);
                    }
                }
            }
        }

        [Test]
        public void Triangles_PointAtRealPoints_ForEverySurferInTheSharedMesh()
        {
            var indices = new int[3 * SurferBody.IndicesPerSurfer];
            for (int surfer = 0; surfer < 3; surfer++)
            {
                SurferBody.WriteIndices(indices, surfer);
            }
            Assert.AreEqual(0, SurferBody.IndicesPerSurfer % 3);
            for (int surfer = 0; surfer < 3; surfer++)
            {
                var used = new bool[SurferBody.VertsPerSurfer];
                for (int i = 0; i < SurferBody.IndicesPerSurfer; i++)
                {
                    int index = indices[surfer * SurferBody.IndicesPerSurfer + i] - surfer * SurferBody.VertsPerSurfer;
                    Assert.That(index, Is.InRange(0, SurferBody.VertsPerSurfer - 1), "surfer " + surfer + " uses only its own points");
                    used[index] = true;
                }
                for (int v = 0; v < used.Length; v++)
                {
                    Assert.IsTrue(used[v], "point " + v + " is in no triangle");
                }
            }
        }

        [Test]
        public void EveryTriangleOfAStandingSurfer_FacesOutwards()
        {
            // Unity only draws the outside of the surfer mesh. Outside = the side its normals point to, so every
            // triangle's own front (corners clockwise) must agree with the normals stored at its corners.
            var vertices = new Vector3[SurferBody.VertsPerSurfer];
            var normals = new Vector3[vertices.Length];
            var colours = new Color32[vertices.Length];
            var joints = new Vector3[SurferBody.JointCount];
            var indices = new int[SurferBody.IndicesPerSurfer];
            SurferBody.WriteIndices(indices, 0);
            // Falling starts in the standing pose, Paddling is the lying pose, Waiting sits: all checked, with every hair
            // style and every wetsuit style.
            SurferState[] states = { SurferState.Falling, SurferState.Paddling, SurferState.Waiting };
            int wrong = 0;
            string first = "";
            for (int style = 0; style < 4; style++)
            {
                foreach (SurferState state in states)
                {
                    SurferBody.Write(BrainIn(state), Matrix4x4.identity, Look(style, style % 3), 0.45f, joints, vertices, normals, colours, 0);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                        Vector3 front = Vector3.Cross(b - a, c - a);
                        if (front.magnitude < 0.0000001f)
                        {
                            continue;                   // folded away (a hair style without a bun)
                        }
                        Vector3 stored = normals[indices[i]] + normals[indices[i + 1]] + normals[indices[i + 2]];
                        if (Vector3.Dot(front, stored) < 0f)
                        {
                            if (wrong == 0)
                            {
                                first = " (first: " + state + ", hair style " + style + ", point " + indices[i] + ")";
                            }
                            wrong++;
                        }
                    }
                }
            }
            Assert.AreEqual(0, wrong, "triangles drawn inside out" + first);
        }

        [Test]
        public void StandingIsTallerThanLying_KneesBentDeep()
        {
            var joints = new Vector3[SurferBody.JointCount];
            SurferBody.PoseJoints(SurferState.Paddling, 0f, 0f, 0.45f, 0f, joints);
            float lying = joints[SurferBody.Head].y;
            SurferBody.PoseJoints(SurferState.Riding, 5f, 0f, 0.45f, 0f, joints);
            Assert.Greater(joints[SurferBody.Head].y, lying + 0.7f);
            // Knees well forward of the hips and feet (a deep crouch), feet wide apart along the board.
            Assert.Greater(joints[SurferBody.KneeL].x, joints[SurferBody.HipL].x + 0.2f);
            Assert.Greater(joints[SurferBody.KneeL].x, joints[SurferBody.FootL].x + 0.2f);
            Assert.Greater(joints[SurferBody.FootL].z - joints[SurferBody.FootR].z, 0.7f);
            // Arms out: the hands are far apart along the board.
            Assert.Greater(joints[SurferBody.HandL].z - joints[SurferBody.HandR].z, 1.2f);
        }

        [Test]
        public void Riding_LeansIntoTheTurn_AndIsNeverQuiteStill()
        {
            var straight = new Vector3[SurferBody.JointCount];
            var leaning = new Vector3[SurferBody.JointCount];
            var later = new Vector3[SurferBody.JointCount];
            SurferBody.PoseJoints(SurferState.Riding, 5f, 0f, 0.45f, 0f, straight);
            SurferBody.PoseJoints(SurferState.Riding, 5f, 0f, 0.45f, 1f, leaning);
            SurferBody.PoseJoints(SurferState.Riding, 5.4f, 0f, 0.45f, 0f, later);
            Assert.Greater(leaning[SurferBody.Head].x, straight[SurferBody.Head].x + 0.1f, "the head goes into the turn");
            Assert.AreEqual(straight[SurferBody.FootL], leaning[SurferBody.FootL], "the feet stay on the board");
            Assert.AreNotEqual(straight[SurferBody.HandL].y, later[SurferBody.HandL].y, "the arms keep balancing");
            SurferBody.PoseJoints(SurferState.Riding, 5f, 0f, 0.45f, 99f, leaning);
            Assert.Less(leaning[SurferBody.Head].x, straight[SurferBody.Head].x + 0.3f, "a silly lean is cut to 1");
        }

        [Test]
        public void Paddling_LiesOnTheBoard_ArmsTakeTurns()
        {
            var joints = new Vector3[SurferBody.JointCount];
            SurferBody.PoseJoints(SurferState.Paddling, 1f, 0.25f, 0.45f, 0f, joints);
            Assert.Less(joints[SurferBody.Chest].y, 0.4f, "lying down");
            Assert.Greater(joints[SurferBody.Head].y, joints[SurferBody.Chest].y, "head up, looking ahead");
            Assert.Greater(joints[SurferBody.Head].z, joints[SurferBody.FootL].z + 1.2f, "stretched out along the board");
            float left = joints[SurferBody.HandL].y;
            float right = joints[SurferBody.HandR].y;
            Assert.Greater(left, right + 0.4f, "one arm over the water while the other pulls through it");
            SurferBody.PoseJoints(SurferState.Paddling, 1f, 0.75f, 0.45f, 0f, joints);
            Assert.Greater(joints[SurferBody.HandR].y, joints[SurferBody.HandL].y + 0.4f, "half a stroke later the other way round");
        }

        [Test]
        public void Board_HasRocker_FlatWhereTheSurferStands()
        {
            Assert.AreEqual(0f, SurferBoard.Rocker(0f), 0.0001f);
            Assert.AreEqual(0f, SurferBoard.Rocker(0.38f), 0.002f, "front foot");
            Assert.AreEqual(0f, SurferBoard.Rocker(-0.42f), 0.0001f, "back foot");
            Assert.Greater(SurferBoard.Rocker(1.06f), 0.08f, "the nose curves up");
            Assert.Greater(SurferBoard.Rocker(-1.02f), 0.02f, "the tail a little");
            Assert.Greater(SurferBoard.Rocker(1.06f), SurferBoard.Rocker(-1.02f));
            Assert.AreEqual(0f, SurferBoard.Rocker(float.NaN), "NaN: flat, not NaN");
            Assert.AreEqual(SurferBoard.Rocker(1.06f), SurferBoard.Rocker(50f), 0.0001f, "past the nose it stops growing");
        }

        [Test]
        public void Hidden_DrawsNothing_BadInputsIgnored()
        {
            var vertices = new Vector3[SurferBody.VertsPerSurfer];
            var normals = new Vector3[vertices.Length];
            var colours = new Color32[vertices.Length];
            var joints = new Vector3[SurferBody.JointCount];
            SurferBrain brain = BrainIn(SurferState.Falling);
            for (int i = 0; i < 30; i++)
            {
                brain.Step(0.1f, null);
            }
            Assert.IsFalse(brain.Visible);
            SurferBody.Write(brain, Matrix4x4.identity, Look(0), 0.45f, joints, vertices, normals, colours, 0);
            for (int v = 1; v < vertices.Length; v++)
            {
                Assert.AreEqual(vertices[0], vertices[v], "folded into one point");
            }
            Assert.DoesNotThrow(() => SurferBody.Write(null, Matrix4x4.identity, Look(0), 0.45f, joints, vertices, normals, colours, 0));
            Assert.DoesNotThrow(() => SurferBody.Write(brain, Matrix4x4.identity, Look(0), 0.45f, joints, vertices, normals, colours, 5), "would overrun: ignored");
            Assert.DoesNotThrow(() => SurferBody.Write(brain, Matrix4x4.identity, Look(0), 0.45f, null, vertices, normals, colours, 0));
            Assert.DoesNotThrow(() => SurferBody.PoseJoints(SurferState.Riding, float.NaN, float.NaN, 0f, float.NaN, null));
            SurferBody.PoseJoints(SurferState.Riding, float.NaN, float.PositiveInfinity, 0f, float.NaN, joints);
            Assert.IsFalse(float.IsNaN(joints[SurferBody.HandL].y), "bad numbers give a calm pose, not NaN");
        }

        [Test]
        public void Spray_OnlyWhileRiding_RisesAndFades()
        {
            var spray = new SprayTrail();
            Vector3 lower, upper;
            float alpha;
            Assert.IsFalse(spray.TryGet(0, 0f, out lower, out upper, out alpha), "nothing before the first ride");

            for (float t = 0f; t < 1f; t += 0.02f)
            {
                spray.Ride(new Vector3(t * 6f, 0f, 0f), Vector3.forward, t);   // 6 m/s along x
            }
            Assert.IsTrue(spray.TryGet(0, 1f, out lower, out upper, out alpha), "the point at the board's tail");
            Assert.AreEqual(0.85f, alpha, 0.0001f);
            Assert.AreEqual(SprayTrail.Points - 1, spray.Count);
            float previousX = lower.x;
            for (int k = 1; k < SprayTrail.Points; k++)
            {
                if (!spray.TryGet(k, 1f, out lower, out upper, out alpha))
                {
                    continue;                              // already gone
                }
                Assert.Less(lower.x, previousX + 0.0001f, "older points lie further back along the path");
                Assert.Greater(upper.y, lower.y, "spray goes up");
                Assert.That(alpha, Is.InRange(0f, 0.85f));
                previousX = lower.x;
            }

            spray.Rest();                                  // ride over
            Assert.IsFalse(spray.TryGet(0, 1.05f, out lower, out upper, out alpha), "no new spray at the tail");
            Assert.IsTrue(spray.TryGet(1, 1.05f, out lower, out upper, out alpha), "what is in the air still fades");
            for (int k = 0; k < SprayTrail.Points; k++)
            {
                Assert.IsFalse(spray.TryGet(k, 3f, out lower, out upper, out alpha), "all gone 2 s later");
            }
        }

        [Test]
        public void Spray_BadInputs_AndAClockThatJumpsBack()
        {
            var spray = new SprayTrail();
            Vector3 lower, upper;
            float alpha;
            spray.Ride(new Vector3(float.NaN, 0f, 0f), Vector3.forward, 1f);
            spray.Ride(Vector3.zero, Vector3.forward, float.NaN);
            Assert.AreEqual(0, spray.Count, "NaN is ignored");
            spray.Ride(Vector3.zero, Vector3.forward, 10f);
            spray.Ride(Vector3.one, Vector3.forward, 10.5f);
            Assert.AreEqual(2, spray.Count);
            spray.Ride(Vector3.zero, Vector3.forward, 2f);     // retry: the clock starts again
            Assert.AreEqual(1, spray.Count, "old spray forgotten");
            Assert.IsFalse(spray.TryGet(-1, 2f, out lower, out upper, out alpha));
            Assert.IsFalse(spray.TryGet(SprayTrail.Points, 2f, out lower, out upper, out alpha));
            Assert.IsFalse(spray.TryGet(0, float.NaN, out lower, out upper, out alpha));
            spray.Clear();
            Assert.AreEqual(0, spray.Count);
        }

        [Test]
        public void SprayRibbons_LiveInTheFoamLayer_InvisibleUntilSomeoneRides()
        {
            var field = new WaveField(new WaveTuning());
            var beach = new BeachLine(10);
            beach.Along = Vector3.right;
            beach.Seaward = Vector3.back;
            beach.Length = 90f;
            var writer = new WaveMeshWriter(field.Capacity, beach.Samples, Color.blue, Color.cyan, Color.white, Color.white, false, 2);
            int wavePoints = field.Capacity * beach.Samples * WaveProfile.SectionPoints;
            Assert.AreEqual(2, writer.Ribbons);
            Assert.AreEqual(wavePoints + 2 * SprayTrail.Points * 2, writer.Vertices.Length);
            Assert.AreEqual(writer.Indices.Length + 2 * (SprayTrail.Points - 1) * 6, writer.FoamIndices.Length);
            for (int i = 0; i < writer.FoamIndices.Length; i++)
            {
                Assert.That(writer.FoamIndices[i], Is.InRange(0, writer.Vertices.Length - 1));
            }
            for (int i = 0; i < writer.Indices.Length; i++)
            {
                Assert.Less(writer.Indices[i], wavePoints, "the water layer never draws the spray points");
            }

            var spray = new SprayTrail();
            writer.WriteSpray(0, spray, 0f);
            writer.WriteSpray(1, spray, 0f);
            for (int v = wavePoints; v < writer.Vertices.Length; v++)
            {
                Assert.AreEqual(0, writer.FoamColours[v].a, "no ride, no spray");
            }
            for (float t = 0f; t < 0.5f; t += 0.02f)
            {
                spray.Ride(new Vector3(t * 6f, 0.1f, -20f), Vector3.forward, t);
            }
            writer.WriteSpray(1, spray, 0.5f);
            int white = 0;
            for (int v = wavePoints; v < writer.Vertices.Length; v++)
            {
                Assert.AreEqual(0, writer.Colours[v].a, "spray is never in the water layer");
                white += writer.FoamColours[v].a;
            }
            Assert.Greater(white, 0);
            Assert.DoesNotThrow(() => writer.WriteSpray(5, spray, 0.5f), "no such ribbon: ignored");
            Assert.DoesNotThrow(() => writer.WriteSpray(0, null, 0.5f));
        }
    }
}
