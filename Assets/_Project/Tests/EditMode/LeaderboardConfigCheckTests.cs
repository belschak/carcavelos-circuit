using System;
using System.Text;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R10): only a safe address and a public key ever turn the online leaderboard on.
    // The keys here are made up for the test (a JWT is three base64 parts; only the middle one is read).
    public class LeaderboardConfigCheckTests
    {
        private const string GoodUrl = "https://abcdefghijklmnop.supabase.co";
        private const string Publishable = "sb_publishable_AbCdEf0123456789_xyz-test";

        private static string Jwt(string payloadJson)
        {
            string header = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return header + "." + payload + ".c2lnbmF0dXJlLW5vdC1yZWFsLWZvci10aGUtdGVzdA";
        }

        [Test]
        public void Empty_MeansThisDeviceOnly_NotAnError()
        {
            Assert.IsTrue(LeaderboardConfigCheck.IsEmpty("", ""));
            Assert.IsTrue(LeaderboardConfigCheck.IsEmpty(null, null));
            Assert.IsTrue(LeaderboardConfigCheck.IsEmpty("  ", " "));
            Assert.IsFalse(LeaderboardConfigCheck.IsEmpty(GoodUrl, ""));
        }

        [Test]
        public void HalfFilledIn_IsAProblem()
        {
            Assert.IsNotEmpty(LeaderboardConfigCheck.Problem(GoodUrl, ""));
            Assert.IsNotEmpty(LeaderboardConfigCheck.Problem("", Publishable));
        }

        [Test]
        public void GoodUrl_AndPublishableKey_AreAccepted()
        {
            Assert.AreEqual("", LeaderboardConfigCheck.Problem(GoodUrl, Publishable));
            Assert.AreEqual("", LeaderboardConfigCheck.Problem(GoodUrl + "/", Publishable), "a trailing slash is fine");
            Assert.AreEqual("", LeaderboardConfigCheck.Problem("  " + GoodUrl + "  ", "  " + Publishable + " "), "pasted with spaces");
            Assert.AreEqual(GoodUrl, LeaderboardConfigCheck.BaseUrl(GoodUrl + "/"));
        }

        [Test]
        public void AnonJwt_IsAccepted_AndSentAsBearer()
        {
            string anon = Jwt("{\"iss\":\"supabase\",\"ref\":\"abcdefghijklmnop\",\"role\":\"anon\",\"iat\":1700000000}");
            Assert.AreEqual("", LeaderboardConfigCheck.Problem(GoodUrl, anon));
            Assert.IsTrue(LeaderboardConfigCheck.IsJwt(anon));
            Assert.IsFalse(LeaderboardConfigCheck.IsJwt(Publishable), "publishable keys go only in the apikey header");
            Assert.AreEqual("", LeaderboardConfigCheck.Problem(GoodUrl, Jwt("{\"role\": \"anon\"}")), "spaces in the JSON");
        }

        [TestCase("http://abcdefghijklmnop.supabase.co")]           // not https
        [TestCase("abcdefghijklmnop.supabase.co")]                  // no scheme
        [TestCase("https://abcdefghijklmnop.supabase.com")]         // wrong domain
        [TestCase("https://example.com")]
        [TestCase("https://supabase.co")]                           // no project
        [TestCase("https://.supabase.co")]
        [TestCase("https://evil.example/x.supabase.co")]            // a path that only ends like ours
        [TestCase("https://abcd.supabase.co.evil.example")]
        [TestCase("https://user@abcd.supabase.co")]                 // user name in the address
        [TestCase("https://abcd.supabase.co:8443")]                 // port
        [TestCase("https://abcd.supabase.co/rest/v1")]              // path
        [TestCase("https://ab_cd.supabase.co")]
        [TestCase("javascript:alert(1)//.supabase.co")]
        public void BadUrls_AreRefused(string url)
        {
            Assert.IsNotEmpty(LeaderboardConfigCheck.Problem(url, Publishable), url);
        }

        [Test]
        public void SecretKeys_AreRefused()
        {
            Assert.IsNotEmpty(LeaderboardConfigCheck.Problem(GoodUrl, "sb_secret_AbCdEf0123456789_xyz-test"), "new-style secret key");
            Assert.IsNotEmpty(LeaderboardConfigCheck.Problem(GoodUrl, Jwt("{\"iss\":\"supabase\",\"role\":\"service_role\"}")), "service_role JWT");
            Assert.IsNotEmpty(LeaderboardConfigCheck.Problem(GoodUrl, Jwt("{\"iss\":\"supabase\"}")), "a JWT without a role");
        }

        [TestCase("short")]
        [TestCase("sb_publishable_has space inside")]
        [TestCase("sb_publishable_quote\"inside0000")]
        [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]   // neither publishable nor a JWT
        [TestCase("aaaa.%%%%.bbbb0000000000000000")]
        [TestCase("aaaa.!!!!.bbbb0000000000000000")]
        public void MalformedKeys_AreRefused(string key)
        {
            Assert.IsNotEmpty(LeaderboardConfigCheck.Problem(GoodUrl, key), key);
        }

        [Test]
        public void KeyThatIsTooLong_IsRefused()
        {
            Assert.IsNotEmpty(LeaderboardConfigCheck.Problem(GoodUrl, "sb_publishable_" + new string('a', 3000)));
        }
    }
}
