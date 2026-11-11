using CarcavelosCircuit.Ambience;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R12): the foam picture made in code, and that a wave never pops: it fades in when it
    // appears, changes smoothly from rolling foam into the sheet on the sand, and fades out when it ends.
    public class WaveFoamTests
    {
        private static BeachLine StraightBeach(int samples)
        {
            var beach = new BeachLine(samples);
            beach.Origin = new Vector3(0f, 0f, -50f);
            beach.Along = Vector3.right;
            beach.Seaward = Vector3.back;
            beach.Length = 120f;
            for (int i = 0; i < beach.Samples; i++)
            {
                beach.Slope[i] = 0.08f;
                beach.Open[i] = 1f;
            }
            return beach;
        }

        // The slot (0, 1, 2 ...) the wave with this number is drawn in right now, -1 if it is gone.
        private static int SlotOf(WaveField field, int id)
        {
            for (int i = 0; i < field.Count; i++)
            {
                if (field.Get(i).Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        [Test]
        public void FoamPicture_RepeatsWithoutASeam()
        {
            for (float t = 0.013f; t < 1f; t += 0.071f)
            {
                Assert.AreEqual(FoamPattern.Lace(0f, t, 6, 5), FoamPattern.Lace(1f, t, 6, 5), 0.0001f, "left edge = right edge");
                Assert.AreEqual(FoamPattern.Lace(t, 0f, 6, 5), FoamPattern.Lace(t, 1f, 6, 5), 0.0001f, "bottom edge = top edge");
                Assert.AreEqual(FoamPattern.Lace(t, 0.3f, 6, 5), FoamPattern.Lace(t - 2f, 3.3f, 6, 5), 0.001f, "repeats beyond 0..1");
            }
        }

        [Test]
        public void FoamPicture_HasLinesAndHoles_AlwaysBetween0And1()
        {
            float lowest = 1f, highest = 0f, sum = 0f;
            int count = 0;
            for (float u = 0f; u < 1f; u += 0.017f)
            {
                for (float v = 0f; v < 1f; v += 0.019f)
                {
                    float lace = FoamPattern.Lace(u, v, 6, 2026);
                    Assert.That(lace, Is.InRange(0f, 1f));
                    lowest = Mathf.Min(lowest, lace);
                    highest = Mathf.Max(highest, lace);
                    sum += lace;
                    count++;
                }
            }
            Assert.Less(lowest, 0.05f, "holes between the foam lines");
            Assert.Greater(highest, 0.95f, "solid white on the lines");
            Assert.That(sum / count, Is.InRange(0.25f, 0.8f), "lace, not a white sheet and not empty");
        }

        [Test]
        public void FoamPicture_SameSeedSamePicture_OtherSeedOtherPicture()
        {
            var a = new Color32[32 * 32];
            var b = new Color32[32 * 32];
            var c = new Color32[32 * 32];
            Assert.IsTrue(FoamPattern.Fill(a, 32, 4, 1));
            Assert.IsTrue(FoamPattern.Fill(b, 32, 4, 1));
            Assert.IsTrue(FoamPattern.Fill(c, 32, 4, 2));
            int different = 0;
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].a, b[i].a);
                Assert.AreEqual(255, a[i].r, "foam is white, only its alpha changes");
                if (a[i].a != c[i].a)
                {
                    different++;
                }
            }
            Assert.Greater(different, 100);
        }

        [Test]
        public void FoamPicture_BadInputs_Refused()
        {
            Assert.IsFalse(FoamPattern.Fill(null, 32, 4, 1));
            Assert.IsFalse(FoamPattern.Fill(new Color32[10], 32, 4, 1), "wrong array size");
            Assert.IsFalse(FoamPattern.Fill(new Color32[0], 0, 4, 1));
            Assert.AreEqual(0f, FoamPattern.Lace(float.NaN, 0.5f, 6, 1));
            Assert.AreEqual(0f, FoamPattern.Lace(0.5f, 0.5f, 0, 1), "no cells = no foam");
        }

        [Test]
        public void NewWave_FadesIn_InvisibleAtFirst()
        {
            var field = new WaveField(new WaveTuning());
            var schedule = new WaveSchedule(new WaveTuning());
            BeachLine beach = StraightBeach(20);
            var writer = new WaveMeshWriter(field.Capacity, beach.Samples, Color.blue, Color.cyan, Color.white, Color.white, false);
            int id = 10;
            field.Update(schedule.SpawnTime(id) + 0.02f);
            writer.Write(field, beach);
            int slot = SlotOf(field, id);
            Assert.AreEqual(0, slot, "the newest wave is the one furthest out");
            int perSlot = beach.Samples * WaveProfile.SectionPoints;
            for (int v = 0; v < perSlot; v++)
            {
                Assert.LessOrEqual(writer.Colours[v].a, 3, "no pop when a swell appears");
                Assert.LessOrEqual(writer.FoamColours[v].a, 3);
            }
        }

        [Test]
        public void DyingWave_HasFadedOut()
        {
            var tuning = new WaveTuning();
            var field = new WaveField(tuning);
            var schedule = new WaveSchedule(tuning);
            BeachLine beach = StraightBeach(20);
            var writer = new WaveMeshWriter(field.Capacity, beach.Samples, Color.blue, Color.cyan, Color.white, Color.white, false);
            int id = 10;
            field.Update(schedule.SpawnTime(id) + schedule.Tuning.LifeSeconds - 0.02f);
            writer.Write(field, beach);
            int slot = SlotOf(field, id);
            Assert.GreaterOrEqual(slot, 0);
            int perSlot = beach.Samples * WaveProfile.SectionPoints;
            for (int v = slot * perSlot; v < (slot + 1) * perSlot; v++)
            {
                Assert.LessOrEqual(writer.Colours[v].a, 3, "no pop when the last foam goes");
                Assert.LessOrEqual(writer.FoamColours[v].a, 3);
            }
        }

        [Test]
        public void ReachingTheSand_FoamTurnsIntoTheSheetWithoutAJump()
        {
            var tuning = new WaveTuning();
            var field = new WaveField(tuning);
            var schedule = new WaveSchedule(tuning);
            BeachLine beach = StraightBeach(20);
            var writer = new WaveMeshWriter(field.Capacity, beach.Samples, Color.blue, Color.cyan, Color.white, Color.white, false);
            int id = 10;
            float landing = schedule.SpawnTime(id) + schedule.Tuning.TravelSeconds;
            int perSlot = beach.Samples * WaveProfile.SectionPoints;

            field.Update(landing - 0.01f);                 // still rolling, 4 cm before the waterline
            writer.Write(field, beach);
            int before = SlotOf(field, id);
            Assert.AreEqual(0f, field.Get(before).Swash);
            var positions = new Vector3[perSlot];
            var soft = new byte[perSlot];
            var lace = new byte[perSlot];
            var uv = new Vector2[perSlot];
            for (int v = 0; v < perSlot; v++)
            {
                positions[v] = writer.Vertices[before * perSlot + v];
                soft[v] = writer.Colours[before * perSlot + v].a;
                lace[v] = writer.FoamColours[before * perSlot + v].a;
                uv[v] = writer.FoamUV[before * perSlot + v];
            }

            field.Update(landing + 0.01f);                 // the first moment as foam on the sand
            writer.Write(field, beach);
            int after = SlotOf(field, id);
            Assert.Greater(field.Get(after).Swash, 0f);
            for (int v = 0; v < perSlot; v++)
            {
                Assert.Less(Vector3.Distance(positions[v], writer.Vertices[after * perSlot + v]), 0.2f, "point " + v + " jumped");
                Assert.LessOrEqual(Mathf.Abs(soft[v] - writer.Colours[after * perSlot + v].a), 12, "soft foam of point " + v + " popped");
                Assert.LessOrEqual(Mathf.Abs(lace[v] - writer.FoamColours[after * perSlot + v].a), 12, "lace of point " + v + " popped");
                Assert.Less(Vector2.Distance(uv[v], writer.FoamUV[after * perSlot + v]), 0.05f, "the foam picture of point " + v + " slipped");
            }
        }

        [Test]
        public void FoamLace_OnlyWhereThereIsFoam()
        {
            var tuning = new WaveTuning();
            var field = new WaveField(tuning);
            var schedule = new WaveSchedule(tuning);
            BeachLine beach = StraightBeach(20);
            var writer = new WaveMeshWriter(field.Capacity, beach.Samples, Color.blue, Color.cyan, Color.white, Color.white, false);
            int id = 10;
            // 5 s after it appeared a wave is a low smooth swell far out: water, but no foam lace at all.
            field.Update(schedule.SpawnTime(id) + 5f);
            writer.Write(field, beach);
            int slot = SlotOf(field, id);
            int perSlot = beach.Samples * WaveProfile.SectionPoints;
            int water = 0;
            for (int v = slot * perSlot; v < (slot + 1) * perSlot; v++)
            {
                Assert.LessOrEqual(writer.FoamColours[v].a, 3);
                water += writer.Colours[v].a;
            }
            Assert.Greater(water, 0, "the swell itself is drawn");
        }
    }
}
