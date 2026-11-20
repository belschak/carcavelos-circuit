using System.Collections.Generic;
using CarcavelosCircuit.Online;
using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Presentation safety (20 Nov 2026): overflow rooms, test seats, same names, the room starting by itself, and a
    // message page that is never a dead end.
    public class OnlineUnbreakableTests
    {
        [SetUp]
        public void SetUp()
        {
            LoopbackHub.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            LoopbackHub.Reset();
        }

        // ---- Rooms ----

        [Test]
        public void RoomNames_RoomOneKeepsItsName_OverflowRoomsGetANumber()
        {
            Assert.AreEqual("carcavelos", OnlineRooms.SessionNameFor(1, 0), "the class room is the same room as before");
            Assert.AreEqual("carcavelos", OnlineRooms.SessionNameFor(1, OnlineRooms.MaxPlayers));
            Assert.AreEqual("carcavelos-2", OnlineRooms.SessionNameFor(2, 0));
            Assert.AreEqual("carcavelos-4", OnlineRooms.SessionNameFor(4, 0));
            Assert.AreEqual("carcavelos-4", OnlineRooms.SessionNameFor(9, 0), "never past the last room");
            Assert.AreEqual("carcavelos", OnlineRooms.SessionNameFor(0, 0));
        }

        [Test]
        public void TestSeats_HaveTheirOwnRooms_NeverTheClassRoom()
        {
            Assert.AreEqual("carcavelos-s3", OnlineRooms.SessionNameFor(1, 3));
            Assert.AreEqual("carcavelos-s3-2", OnlineRooms.SessionNameFor(2, 3));
            Assert.AreEqual(3, OnlineRooms.SeatsOf("carcavelos-s3"));
            Assert.AreEqual(3, OnlineRooms.SeatsOf("carcavelos-s3-2"));
            Assert.AreEqual(15, OnlineRooms.SeatsOf("carcavelos"));
            Assert.AreEqual(15, OnlineRooms.SeatsOf("carcavelos-2"));
            Assert.AreEqual(15, OnlineRooms.SeatsOf(null));
            Assert.AreEqual(15, OnlineRooms.SeatsOf("carcavelos-sx"), "a broken test name is a normal room");
            Assert.AreEqual(0, OnlineRooms.ParseSeats("1"));
            Assert.AreEqual(2, OnlineRooms.ParseSeats(" 2 "));
            Assert.AreEqual(15, OnlineRooms.ParseSeats("15"));
            Assert.AreEqual(0, OnlineRooms.ParseSeats("16"));
            Assert.AreEqual(0, OnlineRooms.ParseSeats("abc"));
        }

        [Test]
        public void NextRoom_GoesOnUntilTheLastRoom()
        {
            Assert.AreEqual(2, OnlineRooms.NextRoom(1));
            Assert.AreEqual(4, OnlineRooms.NextRoom(3));
            Assert.AreEqual(0, OnlineRooms.NextRoom(OnlineRooms.MaxRooms));
            Assert.LessOrEqual(OnlineRooms.MaxRooms * OnlineRooms.MaxPlayers, 100, "all rooms full stays under the 100 CCU of the free plan");
            Assert.AreEqual("CARCAVELOS ROOM", OnlineRooms.LabelFor(1));
            Assert.AreEqual("CARCAVELOS ROOM 2", OnlineRooms.LabelFor(2));
            StringAssert.Contains("JOINING ROOM 2", OnlineRooms.OverflowLine(1, 2));
        }

        [Test]
        public void ParseRoom_NumbersPickAnOverflowRoom()
        {
            bool setsLight;
            TimeOfDay light;
            int number;
            Assert.IsTrue(OnlineRooms.TryParseRoom("2", out setsLight, out light, out number));
            Assert.AreEqual(2, number);
            Assert.IsFalse(setsLight);
            Assert.IsTrue(OnlineRooms.TryParseRoom("carcavelos", out setsLight, out light, out number));
            Assert.AreEqual(1, number);
            Assert.IsFalse(OnlineRooms.TryParseRoom("5", out setsLight, out light, out number), "there is no room 5");
            Assert.IsFalse(OnlineRooms.TryParseRoom("0", out setsLight, out light, out number));
        }

        [Test]
        public void UrlFlags_SeatsAndRoomNumber()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse("https://x/?room=3&name=Ana&seats=2");
            Assert.IsTrue(flags.WantsAutoJoin);
            Assert.AreEqual(3, flags.RoomNumber);
            Assert.AreEqual(2, flags.Seats);
            OnlineUrlFlags plain = OnlineUrlFlags.Parse("https://x/?room=carcavelos&name=Ana&seats=99");
            Assert.AreEqual(1, plain.RoomNumber);
            Assert.AreEqual(0, plain.Seats, "out of range = the real room");
        }

        [Test]
        public void Loopback_ATestRoomIsFullAtItsSeats_TheNextRoomTakesTheRest()
        {
            string room1 = OnlineRooms.SessionNameFor(1, 2);
            string room2 = OnlineRooms.SessionNameFor(2, 2);
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            LoopbackSession c = new LoopbackSession();
            a.Join(room1, "Ana", 0, 0);
            b.Join(room1, "Bo", 1, 0);
            c.Join(room1, "Cy", 2, 0);
            Assert.AreEqual(OnlineStatus.InRoom, b.Status);
            Assert.AreEqual(OnlineStatus.Full, c.Status, "2 seats: the third player is refused");
            c.Leave();
            c.Join(room2, "Cy", 2, 0);
            Assert.AreEqual(OnlineStatus.InRoom, c.Status);
            Assert.IsTrue(c.IsMaster, "first in room 2 = its host");
            Assert.AreEqual(1, c.Players.Count);
            Assert.AreEqual(2, a.Players.Count, "room 1 is unchanged");
        }

        // ---- Same names ----

        [Test]
        public void SameName_LaterJoinerShowsANumber_CaseDoesNotMatter()
        {
            List<OnlinePlayer> players = new List<OnlinePlayer>
            {
                new OnlinePlayer(1, "ANA", 0, 1.0),
                new OnlinePlayer(2, "Bo", 1, 2.0),
                new OnlinePlayer(3, "ana", 2, 3.0),
                new OnlinePlayer(4, "ANA", 3, 4.0),
            };
            OnlineNames.Assign(players);
            Assert.AreEqual("ANA", players[0].DisplayName);
            Assert.AreEqual("Bo", players[1].DisplayName);
            Assert.AreEqual("ana 2", players[2].DisplayName);
            Assert.AreEqual("ANA 3", players[3].DisplayName);
            Assert.AreEqual("ana", players[2].Name, "the name itself is unchanged");
        }

        [Test]
        public void SameName_ATagNeverChangesWhenTheFirstOneLeaves()
        {
            OnlinePlayer first = new OnlinePlayer(1, "ANA", 0, 1.0);
            OnlinePlayer second = new OnlinePlayer(2, "ANA", 1, 2.0);
            OnlineNames.Assign(new List<OnlinePlayer> { first, second });
            Assert.AreEqual("ANA 2", second.DisplayName);
            OnlineNames.Assign(new List<OnlinePlayer> { second }); // the first ANA left
            Assert.AreEqual("ANA 2", second.DisplayName, "stays ANA 2: the name tag over the kart is not rebuilt");
            OnlinePlayer third = new OnlinePlayer(3, "ANA", 2, 3.0);
            OnlineNames.Assign(new List<OnlinePlayer> { second, third });
            Assert.AreEqual("ANA", third.DisplayName, "ANA is free again");
        }

        [Test]
        public void SameName_LongNamesAreCutToFitTheNumber()
        {
            HashSet<string> taken = new HashSet<string> { "ABCDEFGHIJKLMNOP" };
            string shown = OnlineNames.Unique("ABCDEFGHIJKLMNOP", taken);
            Assert.AreEqual("ABCDEFGHIJKLMN 2", shown);
            Assert.LessOrEqual(shown.Length, OnlineNames.MaxDisplayLength);
            Assert.AreEqual("PLAYER", OnlineNames.Unique("  ", null));
        }

        // ---- The room starts by itself ----

        [Test]
        public void AutoStart_LobbyStartsAfterTheLastJoin_OnlyWithTwoPlayers()
        {
            Assert.IsNaN(OnlineStartRules.RoomAutoStartAt(1, 10.0, 0, double.NaN), "alone: never by itself");
            Assert.AreEqual(10.0 + OnlineStartRules.RoomStartSeconds, OnlineStartRules.RoomAutoStartAt(2, 10.0, 0, double.NaN));
            Assert.AreEqual(50.0 + OnlineStartRules.RoomStartSeconds, OnlineStartRules.RoomAutoStartAt(9, 50.0, 0, double.NaN),
                "every new player gives the class the full wait again");
        }

        [Test]
        public void AutoStart_AfterARace_OnlyOnceItIsOver()
        {
            Assert.IsNaN(OnlineStartRules.RoomAutoStartAt(5, 10.0, 3, double.NaN), "a race still running never restarts");
            Assert.AreEqual(200.0 + OnlineStartRules.RoomRematchSeconds, OnlineStartRules.RoomAutoStartAt(5, 10.0, 3, 200.0));
            Assert.AreEqual(230.0 + OnlineStartRules.RoomRematchSeconds, OnlineStartRules.RoomAutoStartAt(5, 230.0, 3, 200.0),
                "someone came in while the results showed: the wait counts from them");
        }

        [Test]
        public void AutoStart_LastJoinedAt_AndTheLine()
        {
            List<OnlinePlayer> players = new List<OnlinePlayer> { new OnlinePlayer(1, "A", 0, 4.0), new OnlinePlayer(2, "B", 0, 9.5) };
            Assert.AreEqual(9.5, OnlineStartRules.LastJoinedAt(players));
            Assert.IsNaN(OnlineStartRules.LastJoinedAt(new List<OnlinePlayer>()));
            Assert.AreEqual("RACE STARTS BY ITSELF IN 90 S", OnlineStartRules.AutoStartLine(88.2));
            Assert.AreEqual("RACE STARTS BY ITSELF IN 15 S", OnlineStartRules.AutoStartLine(11.0));
            Assert.AreEqual("RACE STARTS BY ITSELF IN 7 S", OnlineStartRules.AutoStartLine(6.1));
            Assert.AreEqual("RACE STARTS BY ITSELF IN 0 S", OnlineStartRules.AutoStartLine(-3.0));
            Assert.AreEqual("", OnlineStartRules.AutoStartLine(double.NaN));
        }

        // ---- Never a dead end ----

        [Test]
        public void MessagePage_OffersTryAgainAndTheBots()
        {
            VisualElement root = new VisualElement();
            OnlineLobbyView view = new OnlineLobbyView(root, false);
            view.ShowMessage(OnlineRooms.AllFullMessage(), true);
            Assert.IsTrue(view.OffersRetry);
            Assert.IsNotNull(root.Q<VisualElement>("online-retry"), "TRY AGAIN");
            Assert.IsNotNull(root.Q<VisualElement>("online-bots"), "RACE THE BOTS");
            view.ShowMessage("ONLINE NEEDS THE ONLINE BUILD");
            Assert.IsFalse(view.OffersRetry);
            Assert.IsNull(root.Q<VisualElement>("online-retry"));
            Assert.IsNotNull(root.Q<VisualElement>("online-bots"), "the bots are always there");
        }

        [Test]
        public void ConnectingPage_OffersTheBotsWhileItTriesAgain()
        {
            VisualElement root = new VisualElement();
            OnlineLobbyView view = new OnlineLobbyView(root, true);
            view.ShowConnecting("CONNECTING...");
            Assert.IsNull(root.Q<VisualElement>("online-bots"));
            view.ShowConnecting(OnlineJoinRetry.RetryLine(2), true);
            Assert.IsNotNull(root.Q<VisualElement>("online-bots"));
        }

        [Test]
        public void RoomPage_ShowsTheOverflowRoomAndItsSeats()
        {
            VisualElement root = new VisualElement();
            OnlineLobbyView view = new OnlineLobbyView(root, false);
            view.RoomLabel = OnlineRooms.LabelFor(2);
            view.Seats = 3;
            List<OnlinePlayer> players = new List<OnlinePlayer> { new OnlinePlayer(1, "A", 0, 1.0) };
            view.ShowRoom(players, 1, 1, OnlineStartRules.AutoStartLine(40.0), true, TimeOfDay.Noon);
            List<string> texts = new List<string>();
            root.Query<Label>().ForEach(l => texts.Add(l.text));
            CollectionAssert.Contains(texts, "CARCAVELOS ROOM 2");
            CollectionAssert.Contains(texts, "1/3");
            CollectionAssert.Contains(texts, "RACE STARTS BY ITSELF IN 40 S", "the host sees when the room starts by itself");
        }
    }
}
