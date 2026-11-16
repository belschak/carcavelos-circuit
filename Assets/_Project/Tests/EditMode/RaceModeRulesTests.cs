using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Race modes (help for Role 4, Luc 16 Nov 2026): which modes can be played, which have bots, what the finish shows.
    public class RaceModeRulesTests
    {
        [Test]
        public void Playable_BotsAndTimeAttack_NotOnline()
        {
            Assert.IsTrue(RaceModeRules.IsPlayable(RaceMode.RaceVsBots));
            Assert.IsTrue(RaceModeRules.IsPlayable(RaceMode.TimeAttack));
            Assert.IsFalse(RaceModeRules.IsPlayable(RaceMode.OnlineRace), "online is on the menu but coming soon");
            Assert.IsFalse(RaceModeRules.IsPlayable((RaceMode)7), "an unknown number is never playable");
        }

        [Test]
        public void Bots_OnlyInRaceVsBots()
        {
            Assert.IsTrue(RaceModeRules.HasBots(RaceMode.RaceVsBots));
            Assert.IsFalse(RaceModeRules.HasBots(RaceMode.TimeAttack));
            Assert.IsFalse(RaceModeRules.HasBots(RaceMode.OnlineRace));
        }

        [Test]
        public void FromSaved_AcceptsKnownNumbers_RejectsTheRest()
        {
            Assert.AreEqual(RaceMode.RaceVsBots, RaceModeRules.FromSaved(0));
            Assert.AreEqual(RaceMode.TimeAttack, RaceModeRules.FromSaved(1));
            Assert.AreEqual(RaceMode.OnlineRace, RaceModeRules.FromSaved(2));
            Assert.AreEqual(RaceModeRules.Default, RaceModeRules.FromSaved(-1), "edited PlayerPrefs");
            Assert.AreEqual(RaceModeRules.Default, RaceModeRules.FromSaved(3));
            Assert.AreEqual(RaceModeRules.Default, RaceModeRules.FromSaved(int.MaxValue));
        }

        [Test]
        public void Titles_AreTheMenuWords()
        {
            Assert.AreEqual("RACE VS BOTS", RaceModeRules.Title(RaceMode.RaceVsBots));
            Assert.AreEqual("TIME ATTACK", RaceModeRules.Title(RaceMode.TimeAttack));
            Assert.AreEqual("ONLINE RACE", RaceModeRules.Title(RaceMode.OnlineRace));
            Assert.AreEqual("COMING SOON", RaceModeRules.Subtitle(RaceMode.OnlineRace));
            Assert.AreEqual("", RaceModeRules.Title((RaceMode)9));
        }

        [Test]
        public void Ordinal_EnglishEndings()
        {
            Assert.AreEqual("1ST", RaceModeRules.Ordinal(1));
            Assert.AreEqual("2ND", RaceModeRules.Ordinal(2));
            Assert.AreEqual("3RD", RaceModeRules.Ordinal(3));
            Assert.AreEqual("4TH", RaceModeRules.Ordinal(4));
            Assert.AreEqual("11TH", RaceModeRules.Ordinal(11));
            Assert.AreEqual("12TH", RaceModeRules.Ordinal(12));
            Assert.AreEqual("13TH", RaceModeRules.Ordinal(13));
            Assert.AreEqual("21ST", RaceModeRules.Ordinal(21));
            Assert.AreEqual("", RaceModeRules.Ordinal(0), "unknown place");
            Assert.AreEqual("", RaceModeRules.Ordinal(-2));
        }

        [Test]
        public void Finish_RaceShowsPlace_TimeAttackShowsTime()
        {
            Assert.AreEqual("2ND", RaceModeRules.FinishHeadline(RaceMode.RaceVsBots, 2, 6, "1:02.345"));
            Assert.AreEqual("OF 6  ·  1:02.345", RaceModeRules.FinishSubline(RaceMode.RaceVsBots, 2, 6, "1:02.345", false));
            Assert.AreEqual("1:02.345", RaceModeRules.FinishHeadline(RaceMode.TimeAttack, 1, 1, "1:02.345"));
            Assert.AreEqual("LAP TIME", RaceModeRules.FinishSubline(RaceMode.TimeAttack, 1, 1, "1:02.345", false));
            Assert.AreEqual("NEW RECORD", RaceModeRules.FinishSubline(RaceMode.TimeAttack, 1, 1, "1:02.345", true));
        }

        [Test]
        public void Finish_RaceWithUnknownPlace_FallsBackToTheTime()
        {
            // The bots failed to spawn (alone on the grid) or the standings had no track progress.
            Assert.AreEqual("0:59.000", RaceModeRules.FinishHeadline(RaceMode.RaceVsBots, 1, 1, "0:59.000"));
            Assert.AreEqual("0:59.000", RaceModeRules.FinishHeadline(RaceMode.RaceVsBots, 0, 6, "0:59.000"));
            Assert.AreEqual("0:59.000", RaceModeRules.FinishHeadline(RaceMode.RaceVsBots, 7, 6, "0:59.000"), "place beyond the field");
            Assert.AreEqual("LAP TIME", RaceModeRules.FinishSubline(RaceMode.RaceVsBots, 0, 6, "0:59.000", false));
        }
    }
}
