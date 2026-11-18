using CarcavelosCircuit.Online;
using CarcavelosCircuit.Shared;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The one online room of the class (feedback round 17 Nov 2026): its name, the old links as aliases, the 15 player
    // cap, the words on screen.
    public class OnlineRoomsTests
    {
        [Test]
        public void OneSessionName_NewOnPurpose()
        {
            Assert.AreEqual("carcavelos", OnlineRooms.SessionName);
            Assert.AreNotEqual("carcavelos-noon", OnlineRooms.SessionName, "an old two-room page never meets this build's players");
            Assert.AreNotEqual("carcavelos-sunset", OnlineRooms.SessionName);
        }

        [Test]
        public void ParseRoom_OldLinksAreAliasesOfTheOneRoom()
        {
            bool setsLight;
            TimeOfDay light;
            Assert.IsTrue(OnlineRooms.TryParseRoom("noon", out setsLight, out light));
            Assert.IsTrue(setsLight);
            Assert.AreEqual(TimeOfDay.Noon, light);
            Assert.IsTrue(OnlineRooms.TryParseRoom(" SUNSET ", out setsLight, out light));
            Assert.IsTrue(setsLight, "the old sunset link still opens at sunset (for this visit)");
            Assert.AreEqual(TimeOfDay.Sunset, light);
            Assert.IsTrue(OnlineRooms.TryParseRoom("Carcavelos", out setsLight, out light));
            Assert.IsFalse(setsLight, "the room's own name leaves the player's light alone");
            Assert.IsTrue(OnlineRooms.TryParseRoom("1", out setsLight, out light));
            Assert.IsFalse(setsLight);
            Assert.IsFalse(OnlineRooms.TryParseRoom("midnight", out setsLight, out light));
            Assert.IsFalse(setsLight);
            Assert.IsFalse(OnlineRooms.TryParseRoom("", out setsLight, out light));
            Assert.IsFalse(OnlineRooms.TryParseRoom(null, out setsLight, out light));
        }

        [Test]
        public void Cap_IsFifteen()
        {
            Assert.AreEqual(15, OnlineRooms.MaxPlayers);
            Assert.IsTrue(OnlineRooms.HasSpace(0));
            Assert.IsTrue(OnlineRooms.HasSpace(14));
            Assert.IsFalse(OnlineRooms.HasSpace(15));
            Assert.IsFalse(OnlineRooms.HasSpace(99));
        }

        [Test]
        public void Words_OnScreen()
        {
            Assert.AreEqual("CARCAVELOS ROOM", OnlineRooms.Label);
            Assert.AreEqual("7/15", OnlineRooms.CountLine(7));
            Assert.AreEqual("0/15", OnlineRooms.CountLine(-3), "a bad count never shows a minus");
            string full = OnlineRooms.FullMessage();
            StringAssert.Contains("15/15", full);
            StringAssert.Contains("TRY AGAIN AFTER THIS RACE", full);
            StringAssert.DoesNotContain("OTHER ROOM", full, "there is no other room any more");
            StringAssert.DoesNotContain("SUNSET", full);
            StringAssert.DoesNotContain("NOON", full);
        }

        [Test]
        public void LightButton_SaysMyLook()
        {
            Assert.AreEqual("LIGHT: NOON", OnlineLobbyView.LightText(TimeOfDay.Noon));
            Assert.AreEqual("LIGHT: SUNSET", OnlineLobbyView.LightText(TimeOfDay.Sunset));
        }
    }
}
