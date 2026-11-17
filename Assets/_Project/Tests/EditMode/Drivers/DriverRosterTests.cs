using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Driver select (Luc, 17 Nov 2026): the four roster drivers with their karts, the bot lineup (the three drivers
    // not picked + two repainted Rookies) and the wrap-around of the arrow keys.
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
                StringAssert.EndsWith("_" + driver.Name, driver.KartVisual, "the kart prefab is named after its driver");
                Assert.LessOrEqual(driver.Name.Length, 16, "names fit the name tag (max 16 letters)");
                Assert.LessOrEqual(driver.Line.Length, 80, driver.Name + ": one short sentence under the portrait");
                Assert.IsTrue(driver.Line.EndsWith("."), driver.Name + ": the line is one full sentence");
            }
            Assert.AreEqual("Rookie", DriverRoster.Get(DriverRoster.Rookie).Name);
            Assert.AreEqual(PlayerRacer.PlayerColour, DriverRoster.Get(DriverRoster.Rookie).Colour, "the Rookie keeps the player's orange");
        }

        [Test]
        public void Lineup_TheThreeOthersInTheirKarts_PlusTwoRepaintedRookies()
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
                        Assert.AreNotEqual(DriverRoster.Get(picked).Name, seats[i].Profile.displayName, "the picked driver is not a bot");
                        Assert.AreEqual(settings.Bot(i).pace, seats[i].Profile.pace, "seat " + i + " keeps that bot's driving style");
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        int driver = DriverRoster.IndexOf(seats[i].Profile.displayName);
                        Assert.GreaterOrEqual(driver, 0);
                        Assert.AreEqual(DriverRoster.Get(driver).KartVisual, seats[i].KartVisual, "a roster driver races in their own kart");
                        Assert.IsFalse(seats[i].Repaint, "roster karts keep their own paint");
                    }
                    for (int c = 0; c < 2; c++)
                    {
                        DriverSwap.Seat copy = seats[3 + c];
                        Assert.AreEqual(DriverSwap.CopyNames[c], copy.Profile.displayName);
                        Assert.AreEqual(DriverRoster.Get(DriverRoster.Rookie).KartVisual, copy.KartVisual, "the copies drive the Rookie's Classic");
                        Assert.IsTrue(copy.Repaint, "repainted, so they do not look like the Rookie");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Step_WrapsAround_BadIndexFallsBackToTheRookie()
        {
            Assert.AreEqual(1, DriverRoster.Step(0, 1));
            Assert.AreEqual(DriverRoster.Count - 1, DriverRoster.Step(0, -1));
            Assert.AreEqual(0, DriverRoster.Step(DriverRoster.Count - 1, 1));
            Assert.AreEqual(0, DriverRoster.Step(-1, 1), "no pick yet + right = the first driver");
            Assert.AreEqual(DriverRoster.Count - 1, DriverRoster.Step(-1, -1));
            Assert.AreEqual("Rookie", DriverRoster.Get(99).Name);
            Assert.IsFalse(DriverChoice.Select(99, false), "a driver outside the roster is refused");
        }
    }
}
