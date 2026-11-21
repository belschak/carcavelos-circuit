using CarcavelosCircuit.Online;
using CarcavelosCircuit.Shared;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The online flags in the page address (M2): accepted, rejected and odd input.
    public class OnlineUrlFlagsTests
    {
        [Test]
        public void NoQuery_NoFlags()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse("https://belschak.github.io/carcavelos-circuit/");
            Assert.AreEqual("", flags.Net);
            Assert.IsFalse(flags.HasRoom);
            Assert.AreEqual("", flags.Name);
            Assert.IsFalse(flags.Autopilot);
            Assert.AreEqual(0, flags.AutoStart);
            Assert.IsFalse(flags.WantsAutoJoin);
            Assert.IsFalse(OnlineUrlFlags.Parse(null).HasRoom);
            Assert.IsFalse(OnlineUrlFlags.Parse("").HasRoom);
        }

        [Test]
        public void AllFlags_Read()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse("http://localhost:8000/index.html?net=local&room=sunset&name=Ana%20B&autopilot=1&autostart=4#top");
            Assert.AreEqual("local", flags.Net);
            Assert.IsTrue(flags.HasRoom, "the old sunset link opens the one room");
            Assert.IsTrue(flags.HasLight);
            Assert.AreEqual(TimeOfDay.Sunset, flags.Light, "and the sunset look for this visit");
            Assert.AreEqual("Ana B", flags.Name, "percent escapes are decoded");
            Assert.IsTrue(flags.Autopilot);
            Assert.AreEqual(4, flags.AutoStart);
            Assert.IsTrue(flags.WantsAutoJoin);
        }

        [Test]
        public void Render0_TurnsDrawingOff_OnlyWhenAsked()
        {
            Assert.IsTrue(OnlineUrlFlags.Parse("x?room=noon&name=CI&render=0").NoRender);
            Assert.IsTrue(OnlineUrlFlags.Parse("x?render=off").NoRender);
            Assert.IsFalse(OnlineUrlFlags.Parse("x?render=1").NoRender);
            Assert.IsFalse(OnlineUrlFlags.Parse("x?room=noon&name=CI").NoRender);
            Assert.IsFalse(OnlineUrlFlags.None.NoRender);
        }

        [Test]
        public void Keys_AnyCase_PlusIsSpace_OtherKeysIgnored()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse("x?NET=Photon&Room=NOON&name=Jo+Silva&foo=bar&autopilot=yes");
            Assert.AreEqual("photon", flags.Net);
            Assert.IsTrue(flags.HasRoom);
            Assert.IsTrue(flags.HasLight);
            Assert.AreEqual(TimeOfDay.Noon, flags.Light);
            Assert.AreEqual("Jo Silva", flags.Name);
            Assert.IsTrue(flags.Autopilot);
        }

        [Test]
        public void BadValues_AreRefused_NotCrashed()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse("x?net=tcp&room=night&autopilot=0&autostart=1&name=");
            Assert.AreEqual("", flags.Net, "an unknown transport is ignored");
            Assert.IsFalse(flags.HasRoom, "an unknown room is ignored");
            Assert.IsFalse(flags.HasLight);
            Assert.IsFalse(flags.Autopilot);
            Assert.AreEqual(0, flags.AutoStart, "autostart needs at least 2 players");
            Assert.IsFalse(flags.WantsAutoJoin);
            Assert.AreEqual(15, OnlineUrlFlags.Parse("x?autostart=99").AutoStart, "capped at the room size");
            Assert.AreEqual(0, OnlineUrlFlags.Parse("x?autostart=abc").AutoStart);
            Assert.AreEqual("%zz", OnlineUrlFlags.Decode("%zz"), "a broken escape is kept, never thrown");
        }

        [Test]
        public void LongValues_AreCut()
        {
            string longName = new string('a', 500);
            OnlineUrlFlags flags = OnlineUrlFlags.Parse("x?name=" + longName);
            Assert.AreEqual(OnlineUrlFlags.MaxValueLength, flags.Name.Length);
        }

        [Test]
        public void RoomWithoutName_DoesNotAutoJoin()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse("x?room=noon");
            Assert.IsTrue(flags.HasRoom);
            Assert.IsFalse(flags.WantsAutoJoin, "the lobby only opens");
        }

        [Test]
        public void Driver_ByNameOrIndex_ElseNone()
        {
            Assert.AreEqual(1, OnlineUrlFlags.Parse("x?driver=Tiago").Driver, "Luc's model name still works");
            Assert.AreEqual(1, OnlineUrlFlags.Parse("x?driver=Claudio").Driver, "the teacher's name without the accent");
            Assert.AreEqual(1, OnlineUrlFlags.Parse("x?driver=Cl%C3%A1udio").Driver, "and with it, as a link writes it");
            Assert.AreEqual(2, OnlineUrlFlags.Parse("x?driver=nova").Driver);
            Assert.AreEqual(-1, OnlineUrlFlags.Parse("x?driver=luis").Driver, "Luís drives a Classic bot, not a roster driver");
            Assert.AreEqual(-1, OnlineUrlFlags.Parse("x?driver=Joao").Driver, "João does not race");
            Assert.AreEqual(3, OnlineUrlFlags.Parse("x?driver=amara").Driver);
            Assert.AreEqual(2, OnlineUrlFlags.Parse("x?driver=2").Driver);
            Assert.AreEqual(0, OnlineUrlFlags.Parse("x?driver=rookie").Driver);
            Assert.AreEqual(-1, OnlineUrlFlags.Parse("x?driver=9").Driver, "outside the roster");
            Assert.AreEqual(-1, OnlineUrlFlags.Parse("x?driver=bob").Driver);
            Assert.AreEqual(-1, OnlineUrlFlags.Parse("x?room=carcavelos").Driver, "no flag = the player's own pick");
            Assert.AreEqual(-1, OnlineUrlFlags.None.Driver);
        }

        [Test]
        public void RoomByItsOwnName_LeavesTheLightAlone()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse("x?room=carcavelos&name=Ana");
            Assert.IsTrue(flags.HasRoom);
            Assert.IsTrue(flags.WantsAutoJoin);
            Assert.IsFalse(flags.HasLight, "noon or sunset is the player's own look");
            Assert.IsFalse(OnlineUrlFlags.None.HasLight);
        }

        [Test]
        public void PickTransport_PhotonFirst_LocalOnRequest_EditorFallsBackToLocal()
        {
            Assert.AreEqual("photon", OnlineUrlFlags.PickTransport("", true, true, false), "the real build");
            Assert.AreEqual("local", OnlineUrlFlags.PickTransport("local", true, true, false), "?net=local wins");
            Assert.AreEqual("photon", OnlineUrlFlags.PickTransport("photon", true, true, false));
            Assert.IsNull(OnlineUrlFlags.PickTransport("photon", false, true, false), "asked for photon, not in this build");
            Assert.IsNull(OnlineUrlFlags.PickTransport("", false, true, false), "a web build without Photon: the card says so");
            Assert.AreEqual("local", OnlineUrlFlags.PickTransport("", false, true, true), "the Editor can always open the lobby");
            Assert.IsNull(OnlineUrlFlags.PickTransport("local", true, false, false), "no local transport registered");
            Assert.IsNull(OnlineUrlFlags.PickTransport("", false, false, true));
        }
    }
}
