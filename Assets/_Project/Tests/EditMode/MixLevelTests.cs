using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (P4): dB to volume, and the level fades that replace mixer snapshots on the Web.
    public class MixLevelTests
    {
        [TestCase(0f, 1f)]
        [TestCase(-6f, 0.501f)]
        [TestCase(-20f, 0.1f)]
        [TestCase(-80f, 0f)]   // "off" is exactly silent
        [TestCase(-120f, 0f)]
        public void Decibels_ToVolume(float db, float expected)
        {
            Assert.AreEqual(expected, Decibels.ToVolume(db), 0.001f);
        }

        [Test]
        public void LevelFade_TakesTheGivenTime()
        {
            LevelFade level = new LevelFade(-80f);
            level.FadeTo(-8f, 0.12f);
            level.Tick(0.06f);
            Assert.AreEqual(-44f, level.CurrentDb, 0.01f); // halfway after half the time
            level.Tick(0.06f);
            Assert.AreEqual(-8f, level.CurrentDb, 0.01f);
            level.Tick(1f);
            Assert.AreEqual(-8f, level.CurrentDb, 0.01f);  // and stays there
        }

        [Test]
        public void LevelFade_ZeroSecondsJumps()
        {
            LevelFade level = new LevelFade(-10f);
            level.FadeTo(-2f, 0f);
            Assert.AreEqual(-2f, level.CurrentDb, 0.0001f);
        }

        [Test]
        public void LevelFade_NewTargetMidwayStartsFromWhereItIs()
        {
            LevelFade level = new LevelFade(0f);
            level.FadeTo(-10f, 1f);
            level.Tick(0.5f);                // now at -5
            level.FadeTo(0f, 0.5f);          // back up: 5 dB in 0.5 s
            level.Tick(0.25f);
            Assert.AreEqual(-2.5f, level.CurrentDb, 0.01f);
        }
    }
}
