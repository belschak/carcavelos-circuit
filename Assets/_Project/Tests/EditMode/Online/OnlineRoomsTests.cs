using CarcavelosCircuit.Online;
using CarcavelosCircuit.Shared;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The two online rooms (M2): names, the 15 player cap, the words on screen.
    public class OnlineRoomsTests
    {
        [Test]
        public void SessionNames_AndBack()
        {
            Assert.AreEqual("carcavelos-noon", OnlineRooms.SessionName(TimeOfDay.Noon));
            Assert.AreEqual("carcavelos-sunset", OnlineRooms.SessionName(TimeOfDay.Sunset));
            TimeOfDay room;
            Assert.IsTrue(OnlineRooms.TryRoomOfSession("carcavelos-sunset", out room));
            Assert.AreEqual(TimeOfDay.Sunset, room);
            Assert.IsFalse(OnlineRooms.TryRoomOfSession("carcavelos-night", out room));
            Assert.IsFalse(OnlineRooms.TryRoomOfSession(null, out room));
        }

        [Test]
        public void ParseRoom_FromTheAddress()
        {
            TimeOfDay room;
            Assert.IsTrue(OnlineRooms.TryParseRoom("noon", out room));
            Assert.AreEqual(TimeOfDay.Noon, room);
            Assert.IsTrue(OnlineRooms.TryParseRoom(" SUNSET ", out room));
            Assert.AreEqual(TimeOfDay.Sunset, room);
            Assert.IsFalse(OnlineRooms.TryParseRoom("midnight", out room));
            Assert.IsFalse(OnlineRooms.TryParseRoom("", out room));
            Assert.IsFalse(OnlineRooms.TryParseRoom(null, out room));
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
            Assert.AreEqual("NOON ROOM", OnlineRooms.Label(TimeOfDay.Noon));
            Assert.AreEqual("SUNSET ROOM", OnlineRooms.Label(TimeOfDay.Sunset));
            Assert.AreEqual("7/15", OnlineRooms.CountLine(7));
            Assert.AreEqual("0/15", OnlineRooms.CountLine(-3), "a bad count never shows a minus");
            StringAssert.Contains("15/15", OnlineRooms.FullMessage(TimeOfDay.Noon));
            StringAssert.Contains("SUNSET", OnlineRooms.FullMessage(TimeOfDay.Noon), "points to the other room");
            StringAssert.Contains("NOON", OnlineRooms.FullMessage(TimeOfDay.Sunset));
        }
    }
}
