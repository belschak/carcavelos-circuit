using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Rejected and edge cases for the two keys a Web build carries (the Photon App ID and the Supabase key) and for
    // the online leaderboard service: a bad value stops the build or the request at once, before anything is sent.
    public class BuildKeyAndServiceInputTests
    {
        private const string Url = "https://abcdefghijklmnop.supabase.co";
        private const string PublishableKey = "sb_publishable_abcdefghijklmnopqrstuvwxyz012345";

        // ---- Photon App ID (OnlineAppIdRules, checked by OnlineBuildConfig before a build may carry it) ----

        [TestCase("1a2b3c4d-1234-abcd-ABCD-0123456789ab")]   // mixed case hex
        [TestCase("  1a2b3c4d-1234-abcd-abcd-0123456789ab \n")] // pasted with spaces and a line break
        public void AppId_AGuid_IsAccepted(string appId)
        {
            Assert.IsTrue(OnlineAppIdRules.IsValid(appId));
            Assert.IsFalse(OnlineAppIdRules.IsEmpty(appId));
        }

        [TestCase("{1a2b3c4d-1234-abcd-abcd-0123456789ab}")] // braces
        [TestCase("1a2b3c4d-1234-abcd-abcd-0123456789ab0")]  // one digit too many
        [TestCase("1a2b3c4d-1234-abcd-abcd-0123456789a")]    // one digit too few
        [TestCase("1a2b3c4d-1234-abcd-abcg-0123456789ab")]   // g is not hex
        [TestCase("1a2b3c4d-1234-abcd -abcd-0123456789ab")]  // space inside
        [TestCase("1a2b3c4d12344abcdabcd0123456789ab")]      // no dashes
        [TestCase("https://dashboard.photonengine.com/app/1a2b3c4d-1234-abcd-abcd-0123456789ab")]
        [TestCase("1a2b3c4d-1234-abcd-abcd-0123456789ab;rm -rf")]
        public void AppId_AnythingElse_IsRefused(string appId)
        {
            Assert.IsFalse(OnlineAppIdRules.IsValid(appId), appId);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t\n")]
        public void AppId_Blank_IsEmpty_NotValid(string appId)
        {
            Assert.IsTrue(OnlineAppIdRules.IsEmpty(appId), "empty = single-player build, not an error");
            Assert.IsFalse(OnlineAppIdRules.IsValid(appId));
        }

        // ---- Supabase key (LeaderboardConfigCheck) ----

        [Test]
        public void SupabaseKey_PastedWithSpaces_IsStillThePublicKey()
        {
            Assert.AreEqual("", LeaderboardConfigCheck.Problem("  " + Url + " ", " " + PublishableKey + "\n"));
        }

        [TestCase("sb_secret_abcdefghijklmnopqrstuvwxyz0123456789")]
        [TestCase("  sb_secret_abcdefghijklmnopqrstuvwxyz0123456789  ")]
        public void SupabaseKey_SecretKey_IsRefused_EvenWithSpaces(string key)
        {
            Assert.AreNotEqual("", LeaderboardConfigCheck.KeyProblem(key));
            Assert.AreNotEqual("", LeaderboardConfigCheck.Problem(Url, key));
        }

        [Test]
        public void SupabaseKey_JwtWhosePayloadIsNotBase64_IsRefused()
        {
            // Only legal characters, three parts, but the middle part does not decode to JSON with role anon.
            Assert.AreNotEqual("", LeaderboardConfigCheck.KeyProblem("eyJhbGciOiJIUzI1NiJ9.a.signaturesignaturesignature"));
        }

        // ---- The online leaderboard service: a bad track never becomes a web request ----

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Track X")]
        [TestCase("Track&select=*")]                  // would add a query parameter
        [TestCase("Track_CampusWorld/../../admin")]
        [TestCase("Track%2F")]
        [TestCase("Track_Ä")]
        public void Service_BadTrack_FailsAtOnce_WithoutARequest(string track)
        {
            SupabaseLeaderboardService service = new SupabaseLeaderboardService(Url, PublishableKey, 5);

            LeaderboardReply saved = null;
            service.SubmitTime(track, "Ana", 61.5f, reply => saved = reply);
            Assert.IsNotNull(saved, "answered at once (synchronously), so no request was started");
            Assert.IsFalse(saved.Success);

            LeaderboardReply top = null;
            service.FetchTop(track, 10, reply => top = reply);
            Assert.IsNotNull(top);
            Assert.IsFalse(top.Success);
        }

        [Test]
        public void Service_TrackOneLongerThanTheLimit_FailsAtOnce()
        {
            SupabaseLeaderboardService service = new SupabaseLeaderboardService(Url, PublishableKey, 5);
            LeaderboardReply saved = null;
            service.SubmitTime(new string('a', LapTimeRules.MaxTrackLength + 1), "Ana", 61.5f, reply => saved = reply);
            Assert.IsNotNull(saved);
            Assert.IsFalse(saved.Success);
        }
    }
}
