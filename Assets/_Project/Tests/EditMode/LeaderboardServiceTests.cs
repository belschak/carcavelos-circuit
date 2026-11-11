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
        public void ResultLine_SaysWhereTheTimeIs()
        {
            LeaderboardReply reply = new LeaderboardReply();
            reply.Success = true;
            reply.Rank = 4;
            reply.Source = LeaderboardSource.Online;
            Assert.AreEqual("ON THE ONLINE LEADERBOARD", SaveTimeForm.ResultLine(reply));
            reply.Source = LeaderboardSource.OfflineFallback;
            Assert.AreEqual("LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE", SaveTimeForm.ResultLine(reply));
            reply.Source = LeaderboardSource.ThisDevice;
            reply.Rank = 0;
            StringAssert.StartsWith("OUTSIDE THE TOP 10", SaveTimeForm.ResultLine(reply));
        }
    }
}
