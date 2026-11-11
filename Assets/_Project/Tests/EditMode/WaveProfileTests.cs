using CarcavelosCircuit.Ambience;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R12): the shape of a wave from swell to foam on the sand.
    public class WaveProfileTests
    {
        private static WaveTuning Tuning()
        {
            var t = new WaveTuning();
            t.SpawnDistance = 100f;
            t.BreakDistance = 30f;
            t.Speed = 4f;
            t.SwellHeight = 0.4f;
            t.BreakHeight = 2f;
            t.WhitewaterShare = 0.5f;
            t.PeelSpeed = 5f;
            t.BeachLength = 200f;
            t.EndFade = 30f;
            t.RunUp = 7f;
            return t.Sanitised();
        }

        [Test]
        public void Swell_GrowsTowardsTheBreak()
        {
            WaveTuning t = Tuning();
            float far = WaveProfile.UnbrokenHeight(t, 80f);
            float near = WaveProfile.UnbrokenHeight(t, 45f);
            float atBreak = WaveProfile.UnbrokenHeight(t, 30f);
            Assert.Less(far, near);
            Assert.Less(near, atBreak);
            Assert.AreEqual(2f, atBreak, 0.01f, "full break height at the break line");
            Assert.AreEqual(0.4f, far, 0.05f, "a low swell far out");
        }

        [Test]
        public void Swell_FadesIn_NeverPopsUp()
        {
            WaveTuning t = Tuning();
            Assert.AreEqual(0f, WaveProfile.UnbrokenHeight(t, 100f), 0.0001f);
            Assert.Greater(WaveProfile.UnbrokenHeight(t, 90f), 0f);
        }

        [Test]
        public void Peak_BreaksFirst_ThenTheBreakPeelsSideways()
        {
            WaveTuning t = Tuning();
            Assert.AreEqual(0f, WaveProfile.Broken(t, 0f, 31f), 0.0001f, "before the break line nothing is broken");
            // 2 s after the break (crest at 22 m) the break has run 10 m to each side.
            Assert.AreEqual(1f, WaveProfile.Broken(t, 4f, 22f), 0.0001f);
            Assert.AreEqual(1f, WaveProfile.Broken(t, -4f, 22f), 0.0001f, "both sides");
            Assert.AreEqual(0f, WaveProfile.Broken(t, 25f, 22f), 0.0001f, "far from the peak still smooth");
        }

        [Test]
        public void CloseToTheSand_EverythingHasBroken()
        {
            WaveTuning t = Tuning();
            Assert.AreEqual(1f, WaveProfile.Broken(t, 90f, 3f), 0.0001f);
        }

        [Test]
        public void Curl_IsStrongestNextToTheBreak()
        {
            WaveTuning t = Tuning();
            float atEdge = WaveProfile.Curl(t, 10f, 22f);      // the break has reached 10 m
            float farAway = WaveProfile.Curl(t, 40f, 22f);
            Assert.Greater(atEdge, farAway);
            Assert.That(atEdge, Is.InRange(0f, 1f));
        }

        [Test]
        public void Foam_ShrinksTowardsTheSand_FlatAtTheWaterline()
        {
            WaveTuning t = Tuning();
            Assert.Greater(WaveProfile.FoamHeight(t, 25f), WaveProfile.FoamHeight(t, 8f));
            Assert.AreEqual(0f, WaveProfile.FoamHeight(t, 0f), 0.0001f);
        }

        [Test]
        public void EndFade_ZeroAtTheEnds_OneInTheMiddle()
        {
            WaveTuning t = Tuning();
            Assert.AreEqual(1f, WaveProfile.EndFade(t, 0f), 0.0001f);
            Assert.AreEqual(1f, WaveProfile.EndFade(t, 69f), 0.0001f);
            Assert.AreEqual(0f, WaveProfile.EndFade(t, 100f), 0.0001f);
            Assert.AreEqual(0f, WaveProfile.EndFade(t, -150f), 0.0001f, "beyond the end");
        }

        [Test]
        public void HeightAtOffset_PeaksAtTheCrest_ZeroFarAway()
        {
            Assert.AreEqual(2f, WaveProfile.HeightAtOffset(2f, 0f), 0.0001f);
            Assert.Less(WaveProfile.HeightAtOffset(2f, 1f), 2f, "down the face");
            Assert.Less(WaveProfile.HeightAtOffset(2f, -3f), 2f, "down the back");
            Assert.AreEqual(0f, WaveProfile.HeightAtOffset(2f, 30f), 0.0001f);
            Assert.AreEqual(0f, WaveProfile.HeightAtOffset(2f, -30f), 0.0001f);
            Assert.AreEqual(0f, WaveProfile.HeightAtOffset(0f, 0f), 0.0001f, "no wave");
            Assert.AreEqual(0f, WaveProfile.HeightAtOffset(-1f, 0f), 0.0001f, "negative height = no wave");
        }

        [Test]
        public void Section_StartsAndEndsFlatAndInvisible()
        {
            float offset, y, foam, alpha;
            WaveProfile.Section(0, 2f, 1f, 0.3f, 0.5f, out offset, out y, out foam, out alpha);
            Assert.AreEqual(0f, y, 0.0001f);
            Assert.AreEqual(0f, alpha, 0.0001f);
            WaveProfile.Section(WaveProfile.SectionPoints - 1, 2f, 1f, 0.3f, 0.5f, out offset, out y, out foam, out alpha);
            Assert.AreEqual(0f, y, 0.0001f);
            Assert.AreEqual(0f, alpha, 0.0001f);
        }

        [Test]
        public void Section_CurlThrowsTheLipForward()
        {
            float calmX, curlX, y, foam, alpha;
            WaveProfile.Section(4, 2f, 1f, 0f, 0f, out calmX, out y, out foam, out alpha);
            WaveProfile.Section(4, 2f, 1f, 0f, 1f, out curlX, out y, out foam, out alpha);
            Assert.Greater(curlX, calmX);
            Assert.AreEqual(1f, foam, 0.0001f, "a curling lip is white");
        }

        [Test]
        public void Section_BrokenIsWhite()
        {
            float offset, y, foam, alpha;
            WaveProfile.Section(3, 2f, 1f, 1f, 0f, out offset, out y, out foam, out alpha);
            Assert.AreEqual(1f, foam, 0.0001f);
            Assert.AreEqual(1f, y, 0.0001f, "foam height, not water height");
        }

        [Test]
        public void Section_BackToFront_ForEveryShape()
        {
            for (float h = 0f; h <= 3f; h += 0.5f)
            {
                for (float b = 0f; b <= 1f; b += 0.25f)
                {
                    float previous = float.MinValue;
                    for (int p = 0; p < WaveProfile.SectionPoints; p++)
                    {
                        float offset, y, foam, alpha;
                        WaveProfile.Section(p, h, h * 0.5f, b, 0f, out offset, out y, out foam, out alpha);
                        Assert.IsFalse(float.IsNaN(offset) || float.IsNaN(y) || float.IsNaN(alpha));
                        if (p != 5)       // point 5 sits under the lip (4), so it may lie behind it
                        {
                            Assert.GreaterOrEqual(offset, previous - 0.0001f, "point " + p + " h " + h + " b " + b);
                            previous = offset;
                        }
                    }
                }
            }
        }

        [Test]
        public void Swash_RunsUpTheSand_ThenStays()
        {
            WaveTuning t = Tuning();
            float front, back;
            WaveProfile.SwashEdges(t, 0f, out front, out back);
            Assert.Greater(back, front, "back edge on the sea side");
            float startFront = front;
            WaveProfile.SwashEdges(t, 0.7f, out front, out back);
            Assert.Less(front, startFront, "runs up the sand (negative = up the beach)");
            Assert.AreEqual(-t.RunUp, front, 0.0001f, "full run-up reached");
            WaveProfile.SwashEdges(t, float.NaN, out front, out back);
            Assert.IsFalse(float.IsNaN(front) || float.IsNaN(back));
        }

        [Test]
        public void NaNInputs_GiveZero()
        {
            WaveTuning t = Tuning();
            Assert.AreEqual(0f, WaveProfile.UnbrokenHeight(t, float.NaN));
            Assert.AreEqual(0f, WaveProfile.FoamHeight(t, float.NaN));
            Assert.AreEqual(0f, WaveProfile.Broken(t, float.NaN, 10f));
            Assert.AreEqual(0f, WaveProfile.Curl(t, 1f, float.NaN));
            Assert.AreEqual(0f, WaveProfile.EndFade(t, float.NaN));
            Assert.AreEqual(0f, WaveProfile.HeightAtOffset(float.NaN, 0f));
        }

        [Test]
        public void Smooth_WorksInBothDirections()
        {
            Assert.AreEqual(0f, WaveProfile.Smooth(10f, 0f, 10f), 0.0001f);
            Assert.AreEqual(1f, WaveProfile.Smooth(10f, 0f, 0f), 0.0001f);
            Assert.AreEqual(0.5f, WaveProfile.Smooth(0f, 10f, 5f), 0.0001f);
            Assert.AreEqual(1f, WaveProfile.Smooth(3f, 3f, 3f), 0.0001f, "equal edges = a step, no division by zero");
        }
    }
}
