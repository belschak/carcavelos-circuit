using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Driver select (Luc, 17 Nov 2026): the four roster drivers with their karts, the bot lineup (the three drivers
    // not picked + two repainted Nova Classics) and the wrap-around of the arrow keys.
    // Names (David, 20 Nov 2026; Luc, 21 Nov 2026): Ricardo and Cláudio carry teachers' first names, Luc's two female
    // characters keep their own (Nova, Amara), and the teachers Luís and Paulo drive the two repainted Classics.
    public class DriverRosterTests
    {
        [Test]
        public void FourDrivers_UniqueNames_OwnKart_OneShortLineEach()
        {
            Assert.AreEqual(4, DriverRoster.Count);
            HashSet<string> names = new HashSet<string>();
            HashSet<string> karts = new HashSet<string>();
            for (int i = 0; i < DriverRoster.Count; i++)
            {
                DriverInfo driver = DriverRoster.Get(i);
                Assert.IsTrue(names.Add(driver.Name.ToLowerInvariant()), "two drivers called " + driver.Name);
                Assert.IsTrue(karts.Add(driver.KartVisual), driver.Name + ": every driver has their own kart");
                StringAssert.StartsWith(KartVisualSwap.ModelPrefix, driver.KartVisual);
                StringAssert.EndsWith("_" + driver.Model, driver.KartVisual, "the kart prefab keeps the name of Luc's model");
                Assert.AreEqual(driver.Model.ToLowerInvariant(), driver.PortraitFile, "the portrait file keeps the model's name");
                Assert.LessOrEqual(driver.Name.Length, 16, "names fit the name tag (max 16 letters)");
                Assert.LessOrEqual(driver.Line.Length, 80, driver.Name + ": one short sentence under the portrait");
                Assert.IsTrue(driver.Line.EndsWith("."), driver.Name + ": the line is one full sentence");
            }
            Assert.AreEqual(PlayerRacer.PlayerColour, DriverRoster.Get(DriverRoster.Rookie).Colour, "the first driver keeps the player's orange");
        }

        [Test]
        public void Drivers_TwoTeachersFirstNames_ThenLucsTwoWomenUnderTheirOwnNames()
        {
            Assert.AreEqual(new[] { "Ricardo", "Cláudio", "Nova", "Amara" },
                new[] { DriverRoster.Get(0).Name, DriverRoster.Get(1).Name, DriverRoster.Get(2).Name, DriverRoster.Get(3).Name });
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(TributeRules.FirstName(i), DriverRoster.Get(i).Name, "driver " + i + " = teacher " + i + " of the credits");
            }
            for (int i = 2; i < DriverRoster.Count; i++)
            {
                Assert.AreEqual(DriverRoster.Get(i).Model, DriverRoster.Get(i).Name, "Luc's female characters race under their own names");
                CollectionAssert.DoesNotContain(TributeRules.GivenNames, DriverRoster.Get(i).Name, "no woman drives under a teacher's (man's) name");
            }
            Assert.AreEqual(new[] { "Rookie", "Tiago", "Nova", "Amara" },
                new[] { DriverRoster.Get(0).Model, DriverRoster.Get(1).Model, DriverRoster.Get(2).Model, DriverRoster.Get(3).Model },
                "Luc's models keep their names (prefabs, portraits, textures)");
        }

        [Test]
        public void Lineup_TheThreeOthersInTheirKarts_ThenLuisAndPauloInRepaintedClassics()
        {
            BotSettings settings = ScriptableObject.CreateInstance<BotSettings>();
            try
            {
                for (int picked = 0; picked < DriverRoster.Count; picked++)
                {
                    List<DriverSwap.Seat> seats = DriverSwap.Lineup(picked, settings);
                    Assert.AreEqual(5, seats.Count, "five bots, six karts on the grid");
                    HashSet<string> names = new HashSet<string>();
                    for (int i = 0; i < seats.Count; i++)
                    {
                        Assert.IsTrue(names.Add(seats[i].Profile.displayName), "no name twice on the grid");
                        Assert.AreEqual(settings.Bot(i).pace, seats[i].Profile.pace, "seat " + i + " keeps that bot's driving style");
                    }
                    var expected = new List<string>(DriverSwap.ClassicNames);
                    for (int d = 0; d < DriverRoster.Count; d++)
                    {
                        if (d != picked)
                        {
                            expected.Add(DriverRoster.Get(d).Name);
                        }
                    }
                    CollectionAssert.AreEquivalent(expected, names, "the three drivers not picked, plus Luís and Paulo");
                    for (int i = 0; i < 3; i++)
                    {
                        int driver = DriverRoster.IndexOf(seats[i].Profile.displayName);
                        Assert.GreaterOrEqual(driver, 0);
                        Assert.AreNotEqual(picked, driver, "the picked teacher's own kart is the player's");
                        Assert.AreEqual(DriverRoster.Get(driver).KartVisual, seats[i].KartVisual, "a roster driver races in their own kart");
                        Assert.IsFalse(seats[i].Repaint, "roster karts keep their own paint");
                    }
                    string[] copies = { "Luís", "Paulo" };
                    for (int c = 0; c < 2; c++)
                    {
                        DriverSwap.Seat copy = seats[3 + c];
                        Assert.AreEqual(copies[c], copy.Profile.displayName, "the Classic (Luc's male Rookie model) only carries a man's name");
                        Assert.AreEqual(-1, DriverRoster.IndexOf(copy.Profile.displayName), "a Classic driver is never a roster driver");
                        Assert.AreEqual(DriverSwap.CopyColours[c], copy.Profile.colour);
                        Assert.AreEqual(DriverRoster.Get(DriverRoster.Rookie).KartVisual, copy.KartVisual, "the copies drive the Nova Classic");
                        Assert.IsTrue(copy.Repaint, "repainted, so they do not look like the player's Classic");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Lineup_PickOutsideTheRoster_FourDriversAndLuis()
        {
            List<DriverSwap.Seat> seats = DriverSwap.Lineup(-1, null);
            Assert.AreEqual(5, seats.Count, "still five bots");
            var names = new List<string>();
            for (int i = 0; i < seats.Count; i++)
            {
                names.Add(seats[i].Profile.displayName);
            }
            CollectionAssert.AreEquivalent(new[] { "Ricardo", "Cláudio", "Nova", "Amara", "Luís" }, names);
            Assert.AreEqual("Luís", seats[4].Profile.displayName);
            Assert.AreEqual(DriverSwap.CopyColours[1], seats[4].Profile.colour, "the one Classic is the green one");
        }

        [Test]
        public void BotSettingsDefaults_AreTheStaffsGivenNames()
        {
            BotSettings settings = ScriptableObject.CreateInstance<BotSettings>();
            try
            {
                for (int i = 0; i < TributeRules.GivenNames.Length; i++)
                {
                    Assert.AreEqual(TributeRules.GivenNames[i], settings.Bot(i).displayName);
                }
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [TestCase("Cláudio", 1)]
        [TestCase("claudio", 1)]
        [TestCase("  CLÁUDIO ", 1)]
        [TestCase("Tiago", 1)]   // Luc's model name: old ?driver=Tiago links keep their driver
        [TestCase("Ricardo", 0)]
        [TestCase("Rookie", 0)]
        [TestCase("NOVA", 2)]
        [TestCase("Nova", 2)]
        [TestCase("amara", 3)]
        [TestCase("Amara", 3)]
        [TestCase("Luís", -1)]   // drives a Classic, not a roster driver
        [TestCase("paulo", -1)]  // drives a Classic, not a roster driver
        [TestCase("João", -1)]   // not in the race any more
        [TestCase("", -1)]
        [TestCase(null, -1)]
        [TestCase("Claud", -1)]
        public void IndexOf_IgnoresCaseAndAccents_AndKnowsTheModelNames(string name, int expected)
        {
            Assert.AreEqual(expected, DriverRoster.IndexOf(name));
        }

        [Test]
        public void Plain_DropsThePortugueseAccents()
        {
            Assert.AreEqual("claudio luis joao", DriverRoster.Plain("Cláudio Luís João"));
            Assert.AreEqual("", DriverRoster.Plain(null));
        }

        [Test]
        public void Step_WrapsAround_BadIndexFallsBackToTheFirstDriver()
        {
            Assert.AreEqual(1, DriverRoster.Step(0, 1));
            Assert.AreEqual(DriverRoster.Count - 1, DriverRoster.Step(0, -1));
            Assert.AreEqual(0, DriverRoster.Step(DriverRoster.Count - 1, 1));
            Assert.AreEqual(0, DriverRoster.Step(-1, 1), "no pick yet + right = the first driver");
            Assert.AreEqual(DriverRoster.Count - 1, DriverRoster.Step(-1, -1));
            Assert.AreEqual("Ricardo", DriverRoster.Get(99).Name);
            Assert.IsFalse(DriverChoice.Select(99, false), "a driver outside the roster is refused");
        }
    }
}
