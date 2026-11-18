using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Race modes (help for Role 4, Luc 16 Nov 2026): which modes can be played, which have bots, what the finish shows.
    // M2 (online race): Online is playable through the lobby, never remembered, and switches items, ghost,
    // records and the leaderboard form off.
    public class RaceModeRulesTests
    {
        [Test]
        public void Playable_AllThreeModes_NotAnUnknownNumber()
        {
            Assert.IsTrue(RaceModeRules.IsPlayable(RaceMode.RaceVsBots));
            Assert.IsTrue(RaceModeRules.IsPlayable(RaceMode.TimeAttack));
            Assert.IsTrue(RaceModeRules.IsPlayable(RaceMode.OnlineRace), "online races through the lobby since M2");
            Assert.IsFalse(RaceModeRules.IsPlayable((RaceMode)7), "an unknown number is never playable");
        }

        [Test]
        public void CanPick_OnlineNeedsTheOnlineBuild_OthersAlways()
        {
            string message;
            Assert.IsTrue(RaceModeRules.CanPick(RaceMode.RaceVsBots, false, out message));
            Assert.AreEqual("", message);
            Assert.IsTrue(RaceModeRules.CanPick(RaceMode.TimeAttack, false, out message));
            Assert.IsTrue(RaceModeRules.CanPick(RaceMode.OnlineRace, true, out message), "the online build is in");
            Assert.AreEqual("", message);
            Assert.IsFalse(RaceModeRules.CanPick(RaceMode.OnlineRace, false, out message), "no online build");
            Assert.AreEqual(OnlineRaceHooks.UnavailableMessage, message, "the card says why");
            Assert.IsFalse(RaceModeRules.CanPick((RaceMode)7, true, out message), "an unknown number is refused");
            Assert.AreEqual("", message, "silently: there is no card for it");
        }

        [Test]
        public void Dimmed_OnlyOnlineWithoutTheOnlineBuild_AndUnknownModes()
        {
            Assert.IsFalse(RaceModeRules.LooksDimmed(RaceMode.RaceVsBots, false));
            Assert.IsFalse(RaceModeRules.LooksDimmed(RaceMode.TimeAttack, false));
            Assert.IsTrue(RaceModeRules.LooksDimmed(RaceMode.OnlineRace, false));
            Assert.IsFalse(RaceModeRules.LooksDimmed(RaceMode.OnlineRace, true));
            Assert.IsTrue(RaceModeRules.LooksDimmed((RaceMode)7, true));
        }

        // Luc, 18 Nov 2026: the mode card under the mouse turns orange, only one card is orange at a time.
        [Test]
        public void OrangeCard_FollowsTheMouse_ElseThePickedCard()
        {
            Assert.AreEqual(0, RaceModeRules.OrangeCard(0, -1, false, false), "no mouse on the row: the picked card");
            Assert.AreEqual(1, RaceModeRules.OrangeCard(0, 1, false, false), "mouse on TIME ATTACK: that card");
            Assert.AreEqual(2, RaceModeRules.OrangeCard(0, 2, false, false), "mouse on ONLINE RACE (online build): that card");
            Assert.AreEqual(1, RaceModeRules.OrangeCard(1, 1, false, false), "mouse on the picked card: still that one");
        }

        [Test]
        public void OrangeCard_Refused_DimmedCardAndTouchKeepThePickedCard()
        {
            Assert.AreEqual(0, RaceModeRules.OrangeCard(0, 2, true, false), "dimmed ONLINE card never turns orange");
            Assert.AreEqual(1, RaceModeRules.OrangeCard(1, 0, false, true), "touch: a tap leaves the hover behind, no orange");
            Assert.AreEqual(1, RaceModeRules.OrangeCard(1, 2, true, true));
        }

        [Test]
        public void OrangeCard_Edges_OutOfRangeIsIgnored()
        {
            Assert.AreEqual(-1, RaceModeRules.OrangeCard(-1, -1, false, false), "nothing picked, nothing hovered");
            Assert.AreEqual(2, RaceModeRules.OrangeCard(-1, 2, false, false), "nothing picked, mouse on a card");
            Assert.AreEqual(0, RaceModeRules.OrangeCard(0, 3, false, false), "no 4th card: the picked card");
            Assert.AreEqual(0, RaceModeRules.OrangeCard(0, -5, false, false));
            Assert.AreEqual(-1, RaceModeRules.OrangeCard(7, -1, false, false), "a picked card that does not exist");
        }

        [Test]
        public void Remembered_OnlyOfflineModes_SoTheNextVisitNeverStartsInALobby()
        {
            Assert.IsTrue(RaceModeRules.IsRemembered(RaceMode.RaceVsBots));
            Assert.IsTrue(RaceModeRules.IsRemembered(RaceMode.TimeAttack));
            Assert.IsFalse(RaceModeRules.IsRemembered(RaceMode.OnlineRace));
            Assert.IsFalse(RaceModeRules.IsRemembered((RaceMode)7));

            Assert.AreEqual(RaceMode.TimeAttack, RaceModeRules.StartMode(1));
            Assert.AreEqual(RaceMode.RaceVsBots, RaceModeRules.StartMode(0));
            Assert.AreEqual(RaceModeRules.Default, RaceModeRules.StartMode(2), "an old save with Online starts offline");
            Assert.AreEqual(RaceModeRules.Default, RaceModeRules.StartMode(-1), "edited PlayerPrefs");
            Assert.AreEqual(RaceModeRules.Default, RaceModeRules.StartMode(99));
        }

        [Test]
        public void Online_OpensTheLobby_AndNoKeyStartsIt()
        {
            Assert.IsTrue(RaceModeRules.OpensLobby(RaceMode.OnlineRace));
            Assert.IsFalse(RaceModeRules.OpensLobby(RaceMode.RaceVsBots));
            Assert.IsFalse(RaceModeRules.OpensLobby(RaceMode.TimeAttack));
            Assert.IsTrue(RaceModeRules.StartsOnAnyKey(RaceMode.RaceVsBots));
            Assert.IsTrue(RaceModeRules.StartsOnAnyKey(RaceMode.TimeAttack));
            Assert.IsFalse(RaceModeRules.StartsOnAnyKey(RaceMode.OnlineRace), "the host starts it for everyone");
            Assert.IsFalse(RaceModeRules.StartsOnAnyKey((RaceMode)7));
        }

        [Test]
        public void Online_HasItems_ButNoGhostNoRecordsNoLeaderboardForm()
        {
            Assert.IsTrue(RaceModeRules.HasItems(RaceMode.RaceVsBots));
            Assert.IsTrue(RaceModeRules.HasItems(RaceMode.TimeAttack));
            Assert.IsTrue(RaceModeRules.HasItems(RaceMode.OnlineRace), "online too: every item only acts on your own kart");
            Assert.IsTrue(RaceModeRules.HasGhost(RaceMode.TimeAttack));
            Assert.IsTrue(RaceModeRules.HasGhost(RaceMode.RaceVsBots));
            Assert.IsFalse(RaceModeRules.HasGhost(RaceMode.OnlineRace), "the real people are on the road");
            Assert.IsTrue(RaceModeRules.KeepsRecords(RaceMode.TimeAttack));
            Assert.IsTrue(RaceModeRules.KeepsRecords(RaceMode.RaceVsBots));
            Assert.IsFalse(RaceModeRules.KeepsRecords(RaceMode.OnlineRace), "a bumped lap never replaces the record");
            Assert.IsTrue(RaceModeRules.UsesLeaderboard(RaceMode.TimeAttack));
            Assert.IsTrue(RaceModeRules.UsesLeaderboard(RaceMode.RaceVsBots));
            Assert.IsFalse(RaceModeRules.UsesLeaderboard(RaceMode.OnlineRace), "the room shows its own results");
        }

        [Test]
        public void Finish_OnlineShowsAPlace_LikeARaceAgainstTheBots()
        {
            Assert.IsTrue(RaceModeRules.ShowsPlace(RaceMode.OnlineRace));
            Assert.IsTrue(RaceModeRules.ShowsPlace(RaceMode.RaceVsBots));
            Assert.IsFalse(RaceModeRules.ShowsPlace(RaceMode.TimeAttack));
            Assert.AreEqual("3RD", RaceModeRules.FinishHeadline(RaceMode.OnlineRace, 3, 7, "1:02.345"));
            Assert.AreEqual("OF 7  ·  1:02.345", RaceModeRules.FinishSubline(RaceMode.OnlineRace, 3, 7, "1:02.345", false));
            Assert.AreEqual("1:02.345", RaceModeRules.FinishHeadline(RaceMode.OnlineRace, 0, 7, "1:02.345"), "place not reported yet");
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
            Assert.AreEqual("RACE YOUR CLASS LIVE", RaceModeRules.Subtitle(RaceMode.OnlineRace));
            Assert.AreEqual("BEAT 5 RIVALS", RaceModeRules.Subtitle(RaceMode.RaceVsBots));
            Assert.AreEqual("", RaceModeRules.Title((RaceMode)9));
            Assert.AreEqual("", RaceModeRules.Subtitle((RaceMode)9));
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
