using System.IO;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Task P4 tests (teacher tribute): the four names with their accents, which gates get a thank-you board,
    // where a board may stand, which part of the picture each board shows, and who wins the Gil Pereira Cup.
    public class TributeRulesTests
    {
        private static readonly string[] UpperNames =
        {
            "RICARDO JOÃO GIL PEREIRA", "CLÁUDIO VIEIRA", "LUÍS CARVALHO", "PAULO MARQUES",
        };

        // ---------- The names ----------

        [Test]
        public void Teachers_AreTheFourStaffMembers_SpelledWithTheirAccents()
        {
            Assert.AreEqual(new[] { "Ricardo João Gil Pereira", "Cláudio Vieira", "Luís Carvalho", "Paulo Marques" },
                TributeRules.Teachers);
        }

        [Test]
        public void CreditsPage_ListsAllFourNames()
        {
            string uxml = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/RaceFlow/UI/RaceHud.uxml"));
            StringAssert.Contains("name=\"credits-page-3\"", uxml);
            StringAssert.Contains("name=\"credits-dot-3\"", uxml);
            for (int i = 0; i < TributeRules.Teachers.Length; i++)
            {
                StringAssert.Contains("\"" + TributeRules.Teachers[i].ToUpperInvariant() + "\"", uxml);
            }
        }

        [Test]
        public void GivenNames_AreFirstNamesOfTheStaff_FirstNameIsTheFirstWord()
        {
            Assert.AreEqual(new[] { "Ricardo", "João", "Cláudio", "Luís", "Paulo" }, TributeRules.GivenNames);
            for (int i = 0; i < TributeRules.GivenNames.Length; i++)
            {
                bool found = false;
                for (int t = 0; t < TributeRules.Teachers.Length; t++)
                {
                    string[] words = TributeRules.Teachers[t].Split(' ');
                    // Portuguese names: given names first, the last two words are the family names (Gil Pereira).
                    int given = words.Length > 2 ? words.Length - 2 : 1;
                    for (int w = 0; w < given; w++)
                    {
                        found |= words[w] == TributeRules.GivenNames[i];
                    }
                }
                Assert.IsTrue(found, TributeRules.GivenNames[i] + " is a given name of one of the teachers");
            }
            Assert.AreEqual("Ricardo", TributeRules.FirstName(0));
            Assert.AreEqual("Cláudio", TributeRules.FirstName(1));
            Assert.AreEqual("Luís", TributeRules.FirstName(2));
            Assert.AreEqual("Paulo", TributeRules.FirstName(3));
            Assert.AreEqual("", TributeRules.FirstName(4));
            Assert.AreEqual("", TributeRules.FirstName(-1));
        }

        [Test]
        public void FemaleFirstName_LuisaAndPaula_OnlyForLuisAndPaulo()
        {
            // David + Luc, 22 Nov 2026: Luc's two female characters carry the female form of a teacher's name.
            Assert.AreEqual("", TributeRules.FemaleFirstName(0), "Ricardo has no female form here: he drives himself");
            Assert.AreEqual("", TributeRules.FemaleFirstName(1), "nor Cláudio");
            Assert.AreEqual("Luísa", TributeRules.FemaleFirstName(2));
            Assert.AreEqual("Paula", TributeRules.FemaleFirstName(3));
            Assert.AreEqual("", TributeRules.FemaleFirstName(4));
            Assert.AreEqual("", TributeRules.FemaleFirstName(-1));
        }

        [Test]
        public void CreditsPage4_ThanksTheTeachersByTheirFirstNames()
        {
            string uxml = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/RaceFlow/UI/RaceHud.uxml"));
            StringAssert.Contains("name=\"credits-page-4\"", uxml);
            StringAssert.Contains("name=\"credits-dot-4\"", uxml);
            StringAssert.Contains("THANK YOU, TEACHERS", uxml);
            StringAssert.Contains("Ricardo, Cláudio, Luís and Paulo", uxml);
            Assert.IsFalse(uxml.IndexOf((char)0x2014) >= 0 || uxml.IndexOf((char)0x2013) >= 0, "no long dashes in the credits");
        }

        [Test]
        public void CreditsPage1_ShowsEveryTeamMemberWithTheFullNameFromTheReadme()
        {
            // The 2026-11-08 rule: credits names exactly as docs/team.md, which now has every surname (Noah Schlag).
            string uxml = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/RaceFlow/UI/RaceHud.uxml"));
            string readme = File.ReadAllText(Path.Combine(Application.dataPath, "../README.md"));
            string[] team = { "David Belschak", "Luc Tippmann", "Alessandro Rossi", "Noah Schlag" };
            for (int i = 0; i < team.Length; i++)
            {
                StringAssert.Contains("| " + team[i] + " |", readme, "the README team table");
                StringAssert.Contains("text=\"" + team[i].ToUpperInvariant() + "\"", uxml, "credits page 1");
            }
        }

        [Test]
        public void BoardPicture_DrawsTheNamesInTheSameOrder()
        {
            string script = File.ReadAllText(Path.Combine(Application.dataPath, "../tools/textures/make_tribute_boards.py"));
            string expected = "NAMES = [\"" + string.Join("\", \"", UpperNames) + "\"]";
            StringAssert.Contains(expected, script);
            for (int i = 0; i < TributeRules.Teachers.Length; i++)
            {
                Assert.AreEqual(UpperNames[i], TributeRules.Teachers[i].ToUpperInvariant());
            }
        }

        [Test]
        public void BoardPicture_IsInResources_OneRowPerTeacher()
        {
            var picture = Resources.Load<Texture2D>("TributeBoards");
            Assert.IsNotNull(picture, "Tribute/Resources/TributeBoards.png");
            // Each row (one board) has the board's shape, 4:1, so the names are not stretched.
            float rowAspect = picture.width / (picture.height / (float)TributeRules.Teachers.Length);
            Assert.AreEqual(TributeRules.BoardWidth / TributeRules.BoardHeight, rowAspect, 0.01f);
        }

        // ---------- Gold Cup ----------

        [TestCase(Medal.Gold, true)]
        [TestCase(Medal.DeansList, true)]
        [TestCase(Medal.Silver, false)]
        [TestCase(Medal.Bronze, false)]
        [TestCase(Medal.None, false)]
        public void Cup_GoldOrBetterOnly(Medal medal, bool wins)
        {
            Assert.AreEqual(wins, TributeRules.WinsCup(medal));
        }

        [Test]
        public void CupLine_NamesTheCup()
        {
            StringAssert.Contains("GIL PEREIRA CUP", TributeRules.CupLine);
        }

        // ---------- Gate names ----------

        [TestCase("Gate 0", 0)]
        [TestCase("Gate 7", 7)]
        [TestCase("Gate 13", 13)]
        public void GateNumber_ReadsTheGateName(string name, int expected)
        {
            Assert.AreEqual(expected, TributeRules.GateNumber(name));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Gate")]
        [TestCase("Gate ")]
        [TestCase("Gate x")]
        [TestCase("Gate -1")]
        [TestCase("CheckpointGates")]
        [TestCase("gate 3")]
        public void GateNumber_AnythingElse_IsMinusOne(string name)
        {
            Assert.AreEqual(-1, TributeRules.GateNumber(name));
        }

        // ---------- Which gates get a board ----------

        [Test]
        public void PickGates_CampusLap_FourGatesSpreadOverTheLap()
        {
            // Track_CampusWorld has gates 0 .. 13.
            Assert.AreEqual(new[] { 2, 5, 9, 12 }, TributeRules.PickGates(14, 4));
        }

        [TestCase(14)]
        [TestCase(5)]
        [TestCase(30)]
        public void PickGates_NeverTheStartLine_NeverTwice_InDrivingOrder(int gateCount)
        {
            int[] gates = TributeRules.PickGates(gateCount, 4);
            Assert.AreEqual(4, gates.Length);
            for (int i = 0; i < gates.Length; i++)
            {
                Assert.Greater(gates[i], 0, "gate 0 is the start/finish line");
                Assert.Less(gates[i], gateCount);
                if (i > 0)
                {
                    Assert.Greater(gates[i], gates[i - 1]);
                }
            }
        }

        [Test]
        public void PickGates_FewerGatesThanBoards_EachGateOnce()
        {
            Assert.AreEqual(new[] { 1, 2 }, TributeRules.PickGates(3, 4));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(-5)]
        public void PickGates_NoGateBesideTheStart_NoBoards(int gateCount)
        {
            Assert.IsEmpty(TributeRules.PickGates(gateCount, 4));
        }

        [Test]
        public void PickGates_NoBoards_Empty()
        {
            Assert.IsEmpty(TributeRules.PickGates(14, 0));
        }

        [TestCase(5, 14, 6)]
        [TestCase(13, 14, 1)] // wraps round, skipping the start line
        [TestCase(1, 2, 1)]
        public void NextGate_GoesOnAndSkipsTheStart(int gate, int count, int expected)
        {
            Assert.AreEqual(expected, TributeRules.NextGate(gate, count));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void NextGate_NoOtherGate_MinusOne(int count)
        {
            Assert.AreEqual(-1, TributeRules.NextGate(0, count));
        }

        // ---------- Where a board stands ----------

        [Test]
        public void BoardSpots_BeforeTheGateFirst_RightThenLeft_ThenAfter()
        {
            Vector3[] spots = TributeRules.BoardSpots(new Vector3(-5f, 0.2f, 0f), new Vector3(6f, 0.1f, 0f));
            Assert.AreEqual(4, spots.Length);
            Assert.Less(spots[0].z, 0f);
            Assert.Greater(spots[0].x, 0f);
            Assert.Less(spots[1].z, 0f);
            Assert.Less(spots[1].x, 0f);
            Assert.Greater(spots[2].z, 0f);
            Assert.Greater(spots[3].z, 0f);
        }

        [Test]
        public void BoardSpots_NeverReachTowardsTheRoad()
        {
            // The inner edge of each board is exactly on its pole line, the rest is further out.
            Vector3 left = new Vector3(-18.44f, 0f, 0f), right = new Vector3(5.7f, 0f, 0f);
            Vector3[] spots = TributeRules.BoardSpots(left, right);
            float half = TributeRules.BoardWidth * 0.5f;
            Assert.AreEqual(right.x, spots[0].x - half, 1e-4f);
            Assert.AreEqual(left.x, spots[1].x + half, 1e-4f);
            Assert.AreEqual(0.0f, spots[0].y, 1e-4f, "the board stands at its pole's height");
        }

        [TestCase(0f, 0.3f, true)]
        [TestCase(0f, -0.9f, true)]
        [TestCase(2f, 2f, true)]
        [TestCase(0f, 1.5f, false)]  // a wall top or a roof
        [TestCase(0f, -6f, false)]   // the beach below the promenade
        [TestCase(0f, float.NaN, false)]
        public void GroundFits_OnlyCloseToThePoleFoot(float foot, float ground, bool fits)
        {
            Assert.AreEqual(fits, TributeRules.GroundFits(foot, ground));
        }

        // ---------- Which part of the picture ----------

        [Test]
        public void BoardUv_FirstBoardIsTheTopRow()
        {
            Assert.AreEqual(new Vector4(1f, 0.25f, 0f, 0.75f), TributeRules.BoardUv(0, 4));
            Assert.AreEqual(new Vector4(1f, 0.25f, 0f, 0f), TributeRules.BoardUv(3, 4));
        }

        [TestCase(-3, 0.75f)]
        [TestCase(9, 0f)]
        public void BoardUv_OutOfRange_ClampsToARealRow(int board, float offsetY)
        {
            Assert.AreEqual(offsetY, TributeRules.BoardUv(board, 4).w, 1e-5f);
        }

        [Test]
        public void BoardUv_NoBoards_WholePicture()
        {
            Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), TributeRules.BoardUv(0, 0));
        }
    }
}
