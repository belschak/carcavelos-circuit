using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // M2 online race: the place the finish overlay shows, RaceRoster's standings or the online room's place.
    public class ShownPlaceTests
    {
        [Test]
        public void Offline_ShowsTheRostersPlace()
        {
            ShownPlace shown = new ShownPlace();
            Assert.IsFalse(shown.IsExternal);
            Assert.AreEqual(0, shown.Place, "nothing written yet = unknown");
            Assert.AreEqual(0, shown.Racers);
            shown.RosterPlace = 2;
            shown.RosterRacers = 6;
            Assert.AreEqual(2, shown.Place);
            Assert.AreEqual(6, shown.Racers);
        }

        [Test]
        public void TheRoomsPlace_WinsWhileSet_AndTheRosterKeepsWriting()
        {
            ShownPlace shown = new ShownPlace();
            shown.RosterPlace = 1;
            shown.RosterRacers = 1;
            shown.SetExternal(3, 7);
            Assert.IsTrue(shown.IsExternal);
            Assert.AreEqual(3, shown.Place);
            Assert.AreEqual(7, shown.Racers);

            shown.RosterPlace = 1; // RaceRoster writes every frame, it must not undo the room's place
            shown.RosterRacers = 1;
            Assert.AreEqual(3, shown.Place);
            Assert.AreEqual(7, shown.Racers);

            shown.SetExternal(1, 7); // the room updates the place (someone else finished)
            Assert.AreEqual(1, shown.Place);
        }

        [Test]
        public void Cleared_GoesBackToTheRoster()
        {
            ShownPlace shown = new ShownPlace();
            shown.RosterPlace = 4;
            shown.RosterRacers = 6;
            shown.SetExternal(2, 9);
            shown.ClearExternal();
            Assert.IsFalse(shown.IsExternal);
            Assert.AreEqual(4, shown.Place);
            Assert.AreEqual(6, shown.Racers);
            shown.ClearExternal(); // twice is fine
            Assert.AreEqual(4, shown.Place);
        }

        [Test]
        public void BadNumbersFromTheRoom_BecomeUnknown()
        {
            ShownPlace shown = new ShownPlace();
            shown.SetExternal(-1, -5);
            Assert.IsTrue(shown.IsExternal, "the room still owns the place");
            Assert.AreEqual(0, shown.Place);
            Assert.AreEqual(0, shown.Racers);
            Assert.IsFalse(RaceModeRules.HasKnownPlace(shown.Place, shown.Racers), "the HUD then shows the lap time");
            shown.SetExternal(0, 3);
            Assert.AreEqual(0, shown.Place);
            Assert.AreEqual(3, shown.Racers);
            Assert.IsFalse(RaceModeRules.HasKnownPlace(shown.Place, shown.Racers));
        }
    }
}
