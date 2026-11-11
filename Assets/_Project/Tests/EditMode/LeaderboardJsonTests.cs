using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R10): everything that comes back from the internet is checked, everything sent is built safely.
    public class LeaderboardJsonTests
    {
        private static List<LeaderboardEntry> Parse(string json, bool expectOk)
        {
            List<LeaderboardEntry> rows;
            bool ok = LeaderboardJson.TryParseRows(json, 10, out rows);
            Assert.AreEqual(expectOk, ok, json);
            Assert.IsNotNull(rows, "never null, so the HUD can always loop over it");
            return rows;
        }

        [Test]
        public void GoodAnswer_IsRead()
        {
            List<LeaderboardEntry> rows = Parse("[{\"name\":\"Ana\",\"seconds\":61.234},{\"name\":\"Rui Costa\",\"seconds\":62.5}]", true);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Ana", rows[0].name);
            Assert.AreEqual(61.234f, rows[0].seconds, 0.0001f);
        }

        [Test]
        public void EmptyList_IsAnAnswer_WithNoRows()
        {
            Assert.AreEqual(0, Parse("[]", true).Count);
            Assert.AreEqual(0, Parse("  [ ]  ", true).Count);
        }

        [Test]
        public void BadRows_AreDropped_GoodOnesKept()
        {
            string json = "["
                + "{\"name\":\"Ana\",\"seconds\":61.2},"
                + "{\"name\":\"<script>alert(1)</script>\",\"seconds\":60},"   // HTML
                + "{\"name\":\"'; drop table lap_times; --\",\"seconds\":60}," // SQL-ish
                + "{\"name\":\"X\",\"seconds\":60},"                            // too short
                + "{\"name\":\"WayTooLongNameForTheBoard\",\"seconds\":60},"    // too long
                + "{\"name\":\"fuck\",\"seconds\":60},"                         // rude
                + "{\"name\":\" Ana\",\"seconds\":60},"                         // not clean
                + "{\"name\":\"Rui\",\"seconds\":0.5},"                         // too fast
                + "{\"name\":\"Rui\",\"seconds\":99999},"                       // too slow
                + "{\"name\":\"Rui\"},"                                         // no time = 0
                + "{\"seconds\":61},"                                           // no name
                + "{},"
                + "{\"name\":\"Zoe\",\"seconds\":63.1,\"extra\":\"ignored\"}"   // extra fields are fine
                + "]";
            List<LeaderboardEntry> rows = Parse(json, true);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Ana", rows[0].name);
            Assert.AreEqual("Zoe", rows[1].name);
        }

        [Test]
        public void NotAList_OrBroken_IsRefused()
        {
            Parse(null, false);
            Parse("", false);
            Parse("{\"message\":\"permission denied for table lap_times\"}", false); // a PostgREST error object
            Parse("<html><body>502 Bad Gateway</body></html>", false);
            Parse("[{\"name\":\"Ana\",", false);                                        // cut off
            Parse("[1,2,3", false);
        }

        [Test]
        public void HugeAnswer_IsRefused()
        {
            string huge = "[" + new string(' ', LeaderboardJson.MaxAnswerLength) + "]";
            Parse(huge, false);
        }

        [Test]
        public void AtMostMaxRows_AreKept()
        {
            string json = "[";
            for (int i = 0; i < 30; i++)
            {
                json += (i > 0 ? "," : "") + "{\"name\":\"Player" + i + "\",\"seconds\":" + (60 + i) + "}";
            }
            json += "]";
            Assert.AreEqual(10, Parse(json, true).Count);
        }

        [Test]
        public void InsertBody_IsValidJson_WithTheThreeColumns()
        {
            string body = LeaderboardJson.InsertBody("Track_CampusWorld", "José Müller", 61.25f);
            StringAssert.Contains("\"track\":\"Track_CampusWorld\"", body);
            StringAssert.Contains("\"seconds\":61.25", body);
            LapTimeInsert back = UnityEngine.JsonUtility.FromJson<LapTimeInsert>(body);
            Assert.AreEqual("José Müller", back.name, "accents survive the round trip");
        }

        [Test]
        public void InsertBody_EscapesQuotes_EvenIfOneGotThrough()
        {
            // PlayerNameRules never lets a quote through; JsonUtility still escapes it, so the text cannot break out.
            string body = LeaderboardJson.InsertBody("T", "a\",\"seconds\":1,\"x\":\"", 61f);
            LapTimeInsert back = UnityEngine.JsonUtility.FromJson<LapTimeInsert>(body);
            Assert.AreEqual(61f, back.seconds);
            Assert.AreEqual("a\",\"seconds\":1,\"x\":\"", back.name);
        }

        [TestCase("0-9/37", true, 37)]
        [TestCase("*/0", true, 0)]
        [TestCase("*/123456789", true, 123456789)]
        [TestCase("0-0/*", false, 0)]        // total unknown
        [TestCase("*/1234567890", false, 0)] // more than 9 digits
        [TestCase("*/12a", false, 0)]
        [TestCase("*/-3", false, 0)]
        [TestCase("37", false, 0)]
        [TestCase("*/", false, 0)]
        [TestCase("", false, 0)]
        [TestCase(null, false, 0)]
        public void ContentRange_Total(string header, bool ok, int expected)
        {
            int total;
            Assert.AreEqual(ok, LeaderboardJson.TryParseTotal(header, out total), header);
            Assert.AreEqual(expected, total);
        }
    }
}
