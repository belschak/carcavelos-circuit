using CarcavelosCircuit.Ambience;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R12): the meshes made in code (waves, surfers) and the beach frame that places them.
    public class AmbienceMeshTests
    {
        // A straight beach along +x, sea towards -z, sand rising 0.1 per metre.
        private static BeachLine StraightBeach(int samples)
        {
            var beach = new BeachLine(samples);
            beach.Origin = new Vector3(10f, 0f, -50f);
            beach.Along = Vector3.right;
            beach.Seaward = Vector3.back;
            beach.SeaLevel = 0f;
            beach.Length = 100f;
            for (int i = 0; i < beach.Samples; i++)
            {
                beach.Slope[i] = 0.1f;
                beach.Open[i] = 1f;
            }
            return beach;
        }

        [Test]
        public void BeachLine_PlacesPointsInTheWorld()
        {
            BeachLine beach = StraightBeach(11);
            Assert.AreEqual(-50f, beach.AlongOf(0), 0.0001f);
            Assert.AreEqual(50f, beach.AlongOf(10), 0.0001f);
            Assert.AreEqual(new Vector3(10f, 2f, -70f), beach.PointAt(0f, 20f, 2f), "20 m out to sea, 2 m above the water");
            Vector3 onSand = beach.PointAt(0f, -5f, 0f);
            Assert.AreEqual(0.5f, onSand.y, 0.0001f, "up the sand the point follows its slope");
            Assert.AreEqual(beach.Point(5, 3f, 1f), beach.PointAt(0f, 3f, 1f), "sample 5 is the middle");
        }

        [Test]
        public void BeachLine_FarOutUsesTheSmoothedCoast()
        {
            BeachLine beach = StraightBeach(11);
            beach.Waterline[5] = 10f;                     // a bump in the coast
            beach.SmoothFar(5);
            Assert.AreEqual(10f / 11f, beach.WaterlineFar[5], 0.0001f);
            Assert.AreEqual(10f, BeachLine.WaterlineFor(10f, 1f, 0f), 0.0001f, "at the sand: the real waterline");
            Assert.AreEqual(1f, BeachLine.WaterlineFor(10f, 1f, 60f), 0.0001f, "far out: the smoothed coast");
        }

        [Test]
        public void BeachLine_HeadingZeroFacesTheBeach()
        {
            BeachLine beach = StraightBeach(11);
            Assert.Less(Vector3.Distance(Vector3.forward, beach.Rotation(0f) * Vector3.forward), 0.001f, "beach = +z here");
            Assert.Less(Vector3.Distance(Vector3.right, beach.Rotation(Mathf.PI * 0.5f) * Vector3.forward), 0.001f);
        }

        [Test]
        public void BeachLine_NaNAlong_IsSafe()
        {
            BeachLine beach = StraightBeach(11);
            Vector3 p = beach.PointAt(float.NaN, 0f, 0f);
            Assert.IsFalse(float.IsNaN(p.y));
            Assert.AreEqual(0f, beach.OpenAt(float.NaN));
        }

        [Test]
        public void WaveMesh_SizesMatch_AndNothingIsNaN()
        {
            var field = new WaveField(new WaveTuning());
            BeachLine beach = StraightBeach(24);
            var writer = new WaveMeshWriter(field.Capacity, beach.Samples, Color.blue, Color.cyan, Color.white, Color.white, true);
            Assert.AreEqual(field.Capacity * beach.Samples * WaveProfile.SectionPoints, writer.Vertices.Length);
            Assert.AreEqual(writer.Vertices.Length, writer.Colours.Length);
            Assert.AreEqual(0, writer.Indices.Length % 3);
            for (int i = 0; i < writer.Indices.Length; i++)
            {
                Assert.That(writer.Indices[i], Is.InRange(0, writer.Vertices.Length - 1));
            }
            for (float time = 0f; time < 120f; time += 3.3f)
            {
                field.Update(time);
                writer.Write(field, beach);
                for (int v = 0; v < writer.Vertices.Length; v++)
                {
                    Vector3 p = writer.Vertices[v];
                    Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), "time " + time);
                    Assert.Less(p.y, 6f, "no wave taller than a house");
                }
            }
        }

        [Test]
        public void WaveMesh_ClosedSea_Invisible()
        {
            var field = new WaveField(new WaveTuning());
            BeachLine beach = StraightBeach(12);
            for (int i = 0; i < beach.Samples; i++)
            {
                beach.Open[i] = 0f;                        // rocks everywhere
            }
            var writer = new WaveMeshWriter(field.Capacity, beach.Samples, Color.blue, Color.cyan, Color.white, Color.white, false);
            field.Update(30f);
            writer.Write(field, beach);
            for (int v = 0; v < writer.Colours.Length; v++)
            {
                Assert.AreEqual(0, writer.Colours[v].a);
            }
        }

        [Test]
        public void WaveMesh_NullInputs_DoNotThrow()
        {
            var writer = new WaveMeshWriter(0, 0, Color.blue, Color.cyan, Color.white, Color.white, false);
            Assert.AreEqual(1, writer.Slots, "at least one slot");
            Assert.DoesNotThrow(() => writer.Write(null, null));
        }

        [Test]
        public void WaveField_FindsIncomingWave_OnlyFromTheSeaSide()
        {
            var field = new WaveField(new WaveTuning());
            field.Update(20f);
            Assert.Greater(field.Count, 0);
            WaveState first = field.Get(0);              // furthest out
            WaveSample sample;
            bool found = field.FindIncoming(first.PeakAlong, first.Distance - 3f, 8f, 0f, out sample);
            Assert.IsTrue(found);
            Assert.AreEqual(first.Id, sample.Id);
            Assert.IsFalse(field.FindIncoming(first.PeakAlong, first.Distance + 1f, 8f, 0f, out sample) && sample.Id == first.Id,
                "a wave that already passed is not incoming");
            Assert.IsFalse(field.TryGetWave(-99999, 0f, out sample), "unknown wave");
        }

        [Test]
        public void WaveField_SurfaceIsHigherOnACrest()
        {
            var field = new WaveField(new WaveTuning());
            field.Update(20f);
            WaveState w = field.Get(field.Count - 1);
            if (w.Swash > 0f && field.Count > 1)
            {
                w = field.Get(field.Count - 2);
            }
            float onCrest = field.SurfaceHeight(w.PeakAlong + 50f, w.Distance);
            float between = field.SurfaceHeight(w.PeakAlong + 50f, w.Distance + 15f);
            Assert.GreaterOrEqual(onCrest, between);
            Assert.GreaterOrEqual(onCrest, 0f);
        }

        [Test]
        public void SurferBody_EveryStateWritesSaneGeometry()
        {
            var vertices = new Vector3[SurferBody.VertsPerSurfer];
            var normals = new Vector3[vertices.Length];
            var colours = new Color32[vertices.Length];
            var joints = new Vector3[SurferBody.JointCount];
            var indices = new int[SurferBody.IndicesPerSurfer];
            SurferBody.WriteIndices(indices, 0);
            for (int i = 0; i < indices.Length; i++)
            {
                Assert.That(indices[i], Is.InRange(0, vertices.Length - 1));
            }
            var brain = new SurferBrain(new SurferTuning(), 0f, 1);
            SurferState[] states = { SurferState.Paddling, SurferState.Waiting, SurferState.Catching, SurferState.Riding, SurferState.Finished, SurferState.Falling };
            Matrix4x4 world = Matrix4x4.TRS(new Vector3(5f, 0f, 5f), Quaternion.Euler(0f, 30f, 0f), Vector3.one);
            Color32 c = new Color32(10, 20, 30, 255);
            foreach (SurferState state in states)
            {
                SurferBody.PoseJoints(state, 0.3f, 1.7f, 0.45f, joints);
                SurferBody.Write(brain, world, c, c, c, c, 0.45f, joints, vertices, normals, colours, 0);
                for (int v = 0; v < vertices.Length; v++)
                {
                    Assert.IsFalse(float.IsNaN(vertices[v].x) || float.IsNaN(vertices[v].y), state.ToString());
                    Assert.Less(Vector3.Distance(vertices[v], new Vector3(5f, 0f, 5f)), 3f, "a surfer fits in a 3 m ball");
                    Assert.AreEqual(1f, normals[v].magnitude, 0.01f);
                }
            }
        }

        [Test]
        public void SurferBody_StandingIsTallerThanLying()
        {
            var joints = new Vector3[SurferBody.JointCount];
            SurferBody.PoseJoints(SurferState.Paddling, 0f, 0f, 0.45f, joints);
            float lying = joints[SurferBody.Head].y;
            SurferBody.PoseJoints(SurferState.Riding, 5f, 0f, 0.45f, joints);
            float standing = joints[SurferBody.Head].y;
            Assert.Greater(standing, lying + 0.8f);
        }

        [Test]
        public void SurferBody_Hidden_DrawsNothing_BadInputsIgnored()
        {
            var vertices = new Vector3[SurferBody.VertsPerSurfer];
            var normals = new Vector3[vertices.Length];
            var colours = new Color32[vertices.Length];
            var joints = new Vector3[SurferBody.JointCount];
            var brain = new SurferBrain(new SurferTuning(), 0f, 1);
            brain.TryChange(SurferState.Catching);
            brain.TryChange(SurferState.Riding);
            brain.TryChange(SurferState.Falling);
            brain.Step(0.1f, null);
            for (int i = 0; i < 30; i++)
            {
                brain.Step(0.1f, null);
            }
            Assert.IsFalse(brain.Visible);
            Color32 c = new Color32(10, 20, 30, 255);
            SurferBody.Write(brain, Matrix4x4.identity, c, c, c, c, 0.45f, joints, vertices, normals, colours, 0);
            for (int v = 1; v < vertices.Length; v++)
            {
                Assert.AreEqual(vertices[0], vertices[v], "folded into one point");
            }
            Assert.DoesNotThrow(() => SurferBody.Write(null, Matrix4x4.identity, c, c, c, c, 0.45f, joints, vertices, normals, colours, 0));
            Assert.DoesNotThrow(() => SurferBody.Write(brain, Matrix4x4.identity, c, c, c, c, 0.45f, joints, vertices, normals, colours, 5), "would overrun: ignored");
            Assert.DoesNotThrow(() => SurferBody.PoseJoints(SurferState.Riding, float.NaN, float.NaN, 0f, null));
        }
    }
}
