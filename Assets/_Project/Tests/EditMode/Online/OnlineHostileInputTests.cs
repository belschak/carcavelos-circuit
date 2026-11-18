using System;
using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Everything that reaches the online race from outside: the page address (anyone can edit or share a link) and
    // the messages of the other players (anyone can send bytes into a room). Rejected and edge cases on top of
    // OnlineUrlFlagsTests, RaceControlCodecTests and KartPoseCodecTests: odd shapes must be refused, never crash.
    public class OnlineHostileInputTests
    {
        private const string Page = "https://belschak.github.io/carcavelos-circuit/";

        // ---- Page address ----

        [Test]
        public void RepeatedKey_TheLastOneWins_ABadSecondRoomCancelsTheFirst()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse(Page + "?room=carcavelos&name=Ana&room=midnight");
            Assert.IsFalse(flags.HasRoom, "the second, unknown room wins");
            Assert.IsFalse(flags.WantsAutoJoin, "so nothing joins by itself");
        }

        [Test]
        public void KeysWithoutValues_AndEmptyPairs_AreHarmless()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse(Page + "?&&autopilot&name=Ana&&=x&");
            Assert.IsFalse(flags.Autopilot, "a key without =1 does not switch the autopilot on");
            Assert.AreEqual("Ana", flags.Name);
            Assert.AreEqual("", flags.Net);
        }

        [TestCase("-3")]
        [TestCase("0")]
        [TestCase("1")]                            // one player cannot race online
        [TestCase("99999999999")]                  // overflows an int
        [TestCase("2.5")]
        [TestCase("abc")]
        [TestCase("")]
        public void AutoStart_ImpossibleCounts_StayOff(string value)
        {
            Assert.AreEqual(0, OnlineUrlFlags.Parse(Page + "?autostart=" + value).AutoStart);
        }

        [TestCase("2", 2)]
        [TestCase("15", 15)]
        [TestCase("16", 15)]                       // more than a room holds: the room size
        [TestCase("2147483647", 15)]
        public void AutoStart_IsKeptInsideTheRoomSize(string value, int expected)
        {
            Assert.AreEqual(expected, OnlineUrlFlags.Parse(Page + "?autostart=" + value).AutoStart);
        }

        [Test]
        public void NameOfOnlySpaces_DoesNotJoinByItself()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse(Page + "?room=carcavelos&name=%20%20+");
            Assert.AreEqual("", flags.Name);
            Assert.IsFalse(flags.WantsAutoJoin, "an empty name opens the name field instead");
        }

        [Test]
        public void FlagsAfterTheAnchor_AreIgnored()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse(Page + "?name=Ana#room=carcavelos&autopilot=1");
            Assert.IsFalse(flags.HasRoom);
            Assert.IsFalse(flags.Autopilot);
            Assert.AreEqual("Ana", flags.Name);
        }

        [Test]
        public void EscapeCutAtTheLengthLimit_IsKeptAsText_NotACrash()
        {
            string raw = new string('a', OnlineUrlFlags.MaxValueLength - 1) + "%20";
            string decoded = OnlineUrlFlags.Decode(raw);
            Assert.LessOrEqual(decoded.Length, OnlineUrlFlags.MaxValueLength);
            Assert.AreEqual(OnlineUrlFlags.MaxValueLength, OnlineUrlFlags.Decode(new string('x', 5000)).Length);
            Assert.AreEqual("", OnlineUrlFlags.Decode(null));
        }

        [TestCase("")]
        [TestCase("-1")]
        [TestCase("4")]
        [TestCase("999")]
        [TestCase("<script>")]
        [TestCase("rookie2")]
        public void UnknownDriver_IsNone(string value)
        {
            Assert.AreEqual(-1, OnlineUrlFlags.Parse(Page + "?driver=" + value).Driver);
        }

        [TestCase("LOCAL", "local")]
        [TestCase(" photon ", "photon")]
        [TestCase("websocket", "")]
        [TestCase("local;photon", "")]
        public void Net_OnlyTheTwoKnownTransports(string value, string expected)
        {
            Assert.AreEqual(expected, OnlineUrlFlags.Parse(Page + "?net=" + value).Net);
        }

        // ---- Room control messages ("raceId;phase;startAt;ids") ----

        [Test]
        public void RaceControl_AtMost64Players_OneMoreIsRefused()
        {
            RaceControl control;
            Assert.IsTrue(RaceControlCodec.TryDecode("1;1;10;" + Ids(RaceControlCodec.MaxRoster), out control));
            Assert.AreEqual(RaceControlCodec.MaxRoster, control.Roster.Length);
            Assert.IsFalse(RaceControlCodec.TryDecode("1;1;10;" + Ids(RaceControlCodec.MaxRoster + 1), out control));
        }

        [TestCase("-1;1;10;1,2")]                  // negative race number
        [TestCase("1;-1;10;1,2")]                  // phase below the first
        [TestCase("1;99;10;1,2")]                  // phase after Results
        [TestCase("1;1;Infinity;1,2")]
        [TestCase("1;1;-Infinity;1,2")]
        [TestCase("1;1;NaN;1,2")]
        [TestCase("1;1;10;1,,2")]                  // empty id
        [TestCase("1;1;10;1,99999999999")]         // id overflows an int
        [TestCase("1;1;10;1;2")]                   // five parts
        [TestCase("1;1;10,5;1")]                   // decimal comma
        [TestCase("")]
        public void RaceControl_BrokenNumbers_AreRefused(string text)
        {
            RaceControl control;
            Assert.IsFalse(RaceControlCodec.TryDecode(text, out control), text);
        }

        [Test]
        public void RaceControl_TooLongMessage_IsRefusedBeforeReading()
        {
            RaceControl control;
            string text = "1;1;10;" + new string('1', RaceControlCodec.MaxTextLength);
            Assert.IsFalse(RaceControlCodec.TryDecode(text, out control));
        }

        // ---- Kart pose messages (33 bytes, or base64 text of them) ----

        [Test]
        public void Pose_TimeThatIsNotARealNumber_IsRefused()
        {
            KartPose pose;
            Assert.IsFalse(KartPoseCodec.TryDecode(PoseBytes(double.NaN), out pose), "NaN");
            Assert.IsFalse(KartPoseCodec.TryDecode(PoseBytes(double.PositiveInfinity), out pose), "infinity");
            Assert.IsFalse(KartPoseCodec.TryDecode(PoseBytes(-1.0), out pose), "before the shared clock started");
        }

        [Test]
        public void Pose_TextLongerThanOnePose_IsRefusedBeforeDecoding()
        {
            KartPose pose;
            string longText = Convert.ToBase64String(new byte[60]); // valid base64, 80 characters
            Assert.IsFalse(KartPoseCodec.TryDecodeText(longText, out pose));
            Assert.IsFalse(KartPoseCodec.TryDecodeText(Convert.ToBase64String(PoseBytes(double.NaN)), out pose),
                "valid text of a pose with a NaN time");
        }

        [Test]
        public void Pose_FutureVersion_IsRefused()
        {
            KartPose pose;
            byte[] bytes = PoseBytes(5.0);
            bytes[0] = KartPoseCodec.Version + 1;
            Assert.IsFalse(KartPoseCodec.TryDecode(bytes, out pose));
        }

        private static string Ids(int count)
        {
            string text = "";
            for (int i = 0; i < count; i++)
            {
                text += (i == 0 ? "" : ",") + (1000 + i);
            }
            return text;
        }

        // A pose buffer of our format with only the time filled in (little endian, like KartPoseCodec writes it).
        private static byte[] PoseBytes(double time)
        {
            byte[] bytes = new byte[KartPoseCodec.Size];
            bytes[0] = KartPoseCodec.Version;
            long bits = BitConverter.DoubleToInt64Bits(time);
            for (int i = 0; i < 8; i++)
            {
                bytes[1 + i] = (byte)(bits >> (8 * i));
            }
            return bytes;
        }
    }
}
