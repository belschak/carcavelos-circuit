using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R10): online first, this device when offline; the request addresses; the board's words.
    // The device list lives in PlayerPrefs, so these tests use their own track name and delete its key afterwards.
    public class LeaderboardServiceTests
    {
        private const string Track = "EditModeTest_Track";

        private LeaderboardReply last;

        [SetUp]
        public void Clean()
        {
            PlayerPrefs.DeleteKey(LocalLeaderboardService.SaveKey(Track));
            last = null;
        }

        [TearDown]
        public void CleanUp()
        {
            PlayerPrefs.DeleteKey(LocalLeaderboardService.SaveKey(Track));
        }

        [Test]
        public void Online_Works_AnswersComeFromOnline()
        {
            FakeLeaderboardService online = new FakeLeaderboardService();
            FallbackLeaderboardService service = new FallbackLeaderboardService(online);

            service.SubmitTime(Track, "Ana", 61f, reply => last = reply);
            Assert.IsTrue(last.Success);
            Assert.AreEqual(LeaderboardSource.Online, last.Source);
            Assert.AreEqual(1, last.Rank);
            Assert.AreEqual(1, online.SubmitCalls);

            service.FetchTop(Track, 10, reply => last = reply);
            Assert.AreEqual(LeaderboardSource.Online, last.Source);
            Assert.AreEqual(1, last.Entries.Count);
        }

        [Test]
        public void Online_Fails_TheDeviceKeepsTheTime_AndSaysOffline()
        {
            FakeLeaderboardService online = new FakeLeaderboardService();
            online.Fails = true;
            FallbackLeaderboardService service = new FallbackLeaderboardService(online);

            service.SubmitTime(Track, "Ana", 61f, reply => last = reply);
            Assert.IsTrue(last.Success, "the time is still saved, on this device");
            Assert.AreEqual(LeaderboardSource.OfflineFallback, last.Source);
            Assert.AreEqual(1, last.Rank);
            Assert.AreEqual(1, online.SubmitCalls, "online was tried");

            service.FetchTop(Track, 10, reply => last = reply);
            Assert.IsTrue(last.Success);
            Assert.AreEqual(LeaderboardSource.OfflineFallback, last.Source);
            Assert.AreEqual(1, last.Entries.Count);
            Assert.AreEqual("Ana", last.Entries[0].name);
        }

        [Test]
        public void Online_Works_TheDeviceKeepsACopyAnyway()
        {
            FakeLeaderboardService online = new FakeLeaderboardService();
            FallbackLeaderboardService service = new FallbackLeaderboardService(online);
            service.SubmitTime(Track, "Ana", 61f, reply => last = reply);

            online.Fails = true; // the internet goes away later
            service.FetchTop(Track, 10, reply => last = reply);
            Assert.AreEqual(LeaderboardSource.OfflineFallback, last.Source);
            Assert.AreEqual(1, last.Entries.Count, "the time saved while online is still shown");
        }

        [Test]
        public void NoOnlineService_IsThisDevice_NotOffline()
        {
            FallbackLeaderboardService service = new FallbackLeaderboardService(null);
            service.FetchTop(Track, 10, reply => last = reply);
            Assert.IsTrue(last.Success);
            Assert.AreEqual(LeaderboardSource.ThisDevice, last.Source);
            Assert.AreEqual(0, last.Entries.Count, "empty list = no times yet");

            service.SubmitTime(Track, "Ana", 62f, reply => last = reply);
            service.SubmitTime(Track, "Rui", 61f, reply => last = reply);
            Assert.AreEqual(1, last.Rank, "Rui is faster");
            Assert.AreEqual(LeaderboardSource.ThisDevice, last.Source);
        }

        [Test]
        public void Device_BrokenTrackName_FailsCleanly()
        {
            FallbackLeaderboardService service = new FallbackLeaderboardService(null);
            service.FetchTop("bad track&x=1", 10, reply => last = reply);
            Assert.IsFalse(last.Success);
            Assert.AreEqual(0, last.Entries.Count);
        }

        [Test]
        public void Fake_RanksAndFails_AsTold()
        {
            FakeLeaderboardService fake = new FakeLeaderboardService();
            fake.SubmitTime(Track, "Ana", 62f, reply => last = reply);
            fake.SubmitTime(Track, "Rui", 61f, reply => last = reply);
            Assert.AreEqual(1, last.Rank);
            Assert.AreEqual("Rui", fake.LastName);
            fake.Fails = true;
            fake.FetchTop(Track, 10, reply => last = reply);
            Assert.IsFalse(last.Success);
            Assert.AreEqual(1, fake.FetchCalls);
        }

        [Test]
        public void Requests_HaveTheExpectedAddresses()
        {
            string baseUrl = "https://abcd.supabase.co";
            Assert.AreEqual("https://abcd.supabase.co/rest/v1/lap_times?select=name,seconds&track=eq.Track_CampusWorld&order=seconds.asc,created_at.asc&limit=10",
                SupabaseRequests.TopUrl(baseUrl, "Track_CampusWorld", 10));
            Assert.AreEqual("https://abcd.supabase.co/rest/v1/lap_times", SupabaseRequests.InsertUrl(baseUrl));
            Assert.AreEqual("https://abcd.supabase.co/rest/v1/lap_times?select=seconds&track=eq.T&seconds=lt.61.2345",
                SupabaseRequests.FasterCountUrl(baseUrl, "T", 61.2345f));
        }

        [Test]
        public void Requests_UseADotForDecimals_OnEveryComputer()
        {
            System.Globalization.CultureInfo before = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-PT"); // writes 61,5
                StringAssert.EndsWith("seconds=lt.61.5", SupabaseRequests.FasterCountUrl("https://a.supabase.co", "T", 61.5f));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [TestCase("Track_CampusWorld", "CAMPUS WORLD")]
        [TestCase("Track_NovaCampus", "NOVA CAMPUS")]
        [TestCase("Track_Campus", "CAMPUS")]
        [TestCase("RaceFlow_PlaceholderTrack", "RACE FLOW PLACEHOLDER TRACK")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void TrackTitle_IsReadable(string scene, string title)
        {
            Assert.AreEqual(title, LeaderboardPanel.TrackTitle(scene));
        }

        [Test]
        public void RankLine_OnlineOrThisDevice()
        {
            LeaderboardReply reply = new LeaderboardReply();
            reply.Success = true;
            reply.Rank = 4;
            reply.Source = LeaderboardSource.Online;
            Assert.AreEqual("YOU ARE #4", SaveTimeForm.RankLine(reply));
            reply.Source = LeaderboardSource.OfflineFallback;
            Assert.AreEqual("#4 ON THIS DEVICE", SaveTimeForm.RankLine(reply), "a local place never looks online");
            reply.Source = LeaderboardSource.ThisDevice;
            Assert.AreEqual("#4 ON THIS DEVICE", SaveTimeForm.RankLine(reply));
            reply.Rank = 0;
            Assert.AreEqual("SAVED", SaveTimeForm.RankLine(reply));
            Assert.AreEqual("NOT SAVED", SaveTimeForm.RankLine(LeaderboardReply.Failed()));
        }

        [Test]
        public void IsMine_OnlyTimesSavedHere()
        {
            System.Collections.Generic.List<LeaderboardEntry> mine = new System.Collections.Generic.List<LeaderboardEntry> { new LeaderboardEntry("Ana", 61.234f) };
            Assert.IsTrue(LeaderboardRanking.IsMine(new LeaderboardEntry("ANA", 61.234f), mine));
            Assert.IsFalse(LeaderboardRanking.IsMine(new LeaderboardEntry("Ana", 62f), mine), "another Ana's time is not mine");
            Assert.IsFalse(LeaderboardRanking.IsMine(new LeaderboardEntry("Rui", 61.234f), mine));
            Assert.IsFalse(LeaderboardRanking.IsMine(new LeaderboardEntry("Ana", 61.234f), null), "nothing saved here");
        }

        [Test]
        public void NoteFor_EmptyBoard_InvitesTheFirstTime()
        {
            Assert.AreEqual("NO TIMES YET - BE THE FIRST!", LeaderboardPanel.NoteFor(LeaderboardSource.ThisDevice, 0, false), "fresh browser, no backend");
            Assert.AreEqual("NO TIMES YET - BE THE FIRST!", LeaderboardPanel.NoteFor(LeaderboardSource.Online, 0, true));
            Assert.AreEqual("", LeaderboardPanel.NoteFor(LeaderboardSource.Online, 3, false), "a full board needs no note");
            Assert.AreEqual("", LeaderboardPanel.NoteFor(LeaderboardSource.ThisDevice, 10, true));
        }

        [Test]
        public void NoteFor_Offline_SaysItOnce()
        {
            Assert.AreEqual("LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE", LeaderboardPanel.NoteFor(LeaderboardSource.OfflineFallback, 2, true));
            Assert.AreEqual("LEADERBOARD OFFLINE - TIMES FROM THIS DEVICE", LeaderboardPanel.NoteFor(LeaderboardSource.OfflineFallback, 0, false), "offline wins over the empty note");
        }

        [Test]
        public void SavedHere_ListsOnlyThisDevicesTimes()
        {
            LocalLeaderboardService device = new LocalLeaderboardService(LeaderboardSource.ThisDevice);
            Assert.AreEqual(0, LocalLeaderboardService.SavedHere(Track).Count, "nothing saved yet: an empty list, not null");
            device.SubmitTime(Track, "Ana", 61.5f, reply => { });
            System.Collections.Generic.List<LeaderboardEntry> mine = LocalLeaderboardService.SavedHere(Track);
            Assert.AreEqual(1, mine.Count);
            Assert.AreEqual("Ana", mine[0].name);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("bad track name!")]
        public void SavedHere_BrokenTrackName_IsEmpty(string track)
        {
            Assert.AreEqual(0, LocalLeaderboardService.SavedHere(track).Count);
        }

        [Test]
        public void ResultLine_SaysWhereTheTimeIs()
        {
            LeaderboardReply reply = new LeaderboardReply();
            reply.Success = true;
            reply.Rank = 4;
            reply.Source = LeaderboardSource.Online;
            Assert.AreEqual("ON THE ONLINE LEADERBOARD", SaveTimeForm.ResultLine(reply));
            reply.Source = LeaderboardSource.OfflineFallback;
            Assert.AreEqual("", SaveTimeForm.ResultLine(reply), "offline is said once, by the card's note");
            reply.Source = LeaderboardSource.ThisDevice;
            reply.Rank = 0;
            Assert.AreEqual("OUTSIDE THE TOP 10 ON THIS DEVICE", SaveTimeForm.ResultLine(reply));
            Assert.AreEqual("THIS TIME COULD NOT BE SAVED", SaveTimeForm.ResultLine(LeaderboardReply.Failed()));
        }
    }
}
