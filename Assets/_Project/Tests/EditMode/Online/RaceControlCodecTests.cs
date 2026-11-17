using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // RaceControl as a text line (M2 test transport): roundtrip, and every broken line refused.
    public class RaceControlCodecTests
    {
        [Test]
        public void Roundtrip()
        {
            RaceControl sent = new RaceControl(3, OnlinePhase.Countdown, 1234.5, new[] { 17, 42, 8 });
            string text = RaceControlCodec.Encode(sent);
            Assert.AreEqual("3;1;1234.5;17,42,8", text);
            RaceControl back;
            Assert.IsTrue(RaceControlCodec.TryDecode(text, out back));
            Assert.IsTrue(sent.SameAs(back));
            Assert.AreEqual(1, back.IndexOf(42));
            Assert.AreEqual(-1, back.IndexOf(99));
            Assert.IsTrue(back.IsRaceUnderway);
        }

        [Test]
        public void Lobby_RoundtripsWithEmptyRoster()
        {
            RaceControl back;
            Assert.IsTrue(RaceControlCodec.TryDecode(RaceControlCodec.Encode(RaceControl.Lobby), out back));
            Assert.AreEqual(0, back.RosterCount);
            Assert.AreEqual(OnlinePhase.Lobby, back.Phase);
            Assert.IsFalse(back.IsRaceUnderway);
            Assert.IsTrue(RaceControlCodec.TryDecode(RaceControlCodec.Encode(null), out back), "null encodes as the lobby");
        }

        [Test]
        public void BrokenLines_AreRefused()
        {
            RaceControl unused;
            Assert.IsFalse(RaceControlCodec.TryDecode(null, out unused));
            Assert.IsFalse(RaceControlCodec.TryDecode("", out unused));
            Assert.IsFalse(RaceControlCodec.TryDecode("3;1;1234.5", out unused), "a field missing");
            Assert.IsFalse(RaceControlCodec.TryDecode("3;1;1234.5;1;2", out unused), "a field too many");
            Assert.IsFalse(RaceControlCodec.TryDecode("x;1;1234.5;1", out unused), "race id not a number");
            Assert.IsFalse(RaceControlCodec.TryDecode("-1;1;1234.5;1", out unused), "negative race id");
            Assert.IsFalse(RaceControlCodec.TryDecode("3;9;1234.5;1", out unused), "unknown phase");
            Assert.IsFalse(RaceControlCodec.TryDecode("3;1;NaN;1", out unused), "start time not a real number");
            Assert.IsFalse(RaceControlCodec.TryDecode("3;1;1234.5;1,abc", out unused), "a roster id not a number");
            Assert.IsFalse(RaceControlCodec.TryDecode(new string('1', 700), out unused), "far too long");
            Assert.AreEqual(0, unused.RaceId, "a refused line leaves the lobby control");
        }

        [Test]
        public void SameAs_ComparesEveryField()
        {
            RaceControl a = new RaceControl(1, OnlinePhase.Countdown, 10.0, new[] { 1, 2 });
            Assert.IsTrue(a.SameAs(new RaceControl(1, OnlinePhase.Countdown, 10.0, new[] { 1, 2 })));
            Assert.IsFalse(a.SameAs(new RaceControl(2, OnlinePhase.Countdown, 10.0, new[] { 1, 2 })));
            Assert.IsFalse(a.SameAs(new RaceControl(1, OnlinePhase.Racing, 10.0, new[] { 1, 2 })));
            Assert.IsFalse(a.SameAs(new RaceControl(1, OnlinePhase.Countdown, 11.0, new[] { 1, 2 })));
            Assert.IsFalse(a.SameAs(new RaceControl(1, OnlinePhase.Countdown, 10.0, new[] { 2, 1 })));
            Assert.IsFalse(a.SameAs(null));
            Assert.AreEqual(OnlinePhase.Racing, a.WithPhase(OnlinePhase.Racing).Phase);
            Assert.AreEqual(2, a.WithPhase(OnlinePhase.Racing).RosterCount);
        }
    }
}
