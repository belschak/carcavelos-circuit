using CarcavelosCircuit.Online;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The pose packet (M2 online race): small, and the same pose comes back out (within its resolution).
    public class KartPoseCodecTests
    {
        private static KartPose Sample()
        {
            KartPose pose = new KartPose();
            pose.Time = 1234.5678;
            pose.Position = new Vector3(12.345f, -0.5f, 987.654f);
            pose.Rotation = Quaternion.Euler(5f, 123.4f, -2.5f);
            pose.Speed = 23.45f;
            pose.Steer = -0.5f;
            pose.DriftStage = 2;
            pose.DriftCharge01 = 0.75f;
            pose.Flags = KartPose.MakeFlags(true, false, true);
            return pose;
        }

        [Test]
        public void Encode_Is33Bytes_WithVersion()
        {
            byte[] bytes = KartPoseCodec.Encode(Sample());
            Assert.AreEqual(KartPoseCodec.Size, bytes.Length);
            Assert.AreEqual(33, bytes.Length, "small packets matter on campus Wi-Fi");
            Assert.AreEqual(KartPoseCodec.Version, bytes[0]);
        }

        [Test]
        public void Roundtrip_KeepsEveryField_WithinResolution()
        {
            KartPose sent = Sample();
            KartPose back;
            Assert.IsTrue(KartPoseCodec.TryDecode(KartPoseCodec.Encode(sent), out back));
            Assert.AreEqual(sent.Time, back.Time, 0.0, "time travels as a full double");
            Assert.AreEqual(sent.Position.x, back.Position.x, 0.006f, "whole centimetres");
            Assert.AreEqual(sent.Position.y, back.Position.y, 0.006f);
            Assert.AreEqual(sent.Position.z, back.Position.z, 0.006f);
            Assert.Less(Quaternion.Angle(sent.Rotation, back.Rotation), 0.2f, "tenths of a degree");
            Assert.AreEqual(sent.Speed, back.Speed, 0.006f);
            Assert.AreEqual(sent.Steer, back.Steer, 0.01f);
            Assert.AreEqual(2, back.DriftStage);
            Assert.AreEqual(0.75f, back.DriftCharge01, 0.005f);
            Assert.IsTrue(back.IsDrifting);
            Assert.IsFalse(back.IsBoosting);
            Assert.IsTrue(back.IsGrounded);
        }

        [Test]
        public void TextRoundtrip_ForStringTransports()
        {
            KartPose back;
            string text = KartPoseCodec.EncodeText(Sample());
            Assert.LessOrEqual(text.Length, 64);
            Assert.IsTrue(KartPoseCodec.TryDecodeText(text, out back));
            Assert.AreEqual(1234.5678, back.Time, 0.0);
        }

        [Test]
        public void Decode_RefusesWrongLength_Version_AndGarbage()
        {
            KartPose unused;
            Assert.IsFalse(KartPoseCodec.TryDecode(null, out unused));
            Assert.IsFalse(KartPoseCodec.TryDecode(new byte[10], out unused));
            byte[] wrongVersion = KartPoseCodec.Encode(Sample());
            wrongVersion[0] = 9;
            Assert.IsFalse(KartPoseCodec.TryDecode(wrongVersion, out unused));
            Assert.IsFalse(KartPoseCodec.TryDecodeText("not base64 at all!!", out unused));
            Assert.IsFalse(KartPoseCodec.TryDecodeText("", out unused));
            Assert.IsFalse(KartPoseCodec.TryDecodeText(null, out unused));
        }

        [Test]
        public void Encode_TamesBadNumbers()
        {
            KartPose pose = Sample();
            pose.Speed = float.NaN;
            pose.Position = new Vector3(float.PositiveInfinity, 0f, 1e9f);
            pose.Rotation = new Quaternion(0f, 0f, 0f, 0f); // not a rotation
            pose.Time = double.NaN;
            pose.Steer = 7f;
            KartPose back;
            Assert.IsTrue(KartPoseCodec.TryDecode(KartPoseCodec.Encode(pose), out back));
            Assert.AreEqual(0f, back.Speed);
            Assert.AreEqual(0f, back.Position.x, "infinity becomes 0");
            Assert.AreEqual(KartPoseCodec.MaxMetres, back.Position.z, 0.01f, "far away is clamped");
            Assert.AreEqual(0.0, back.Time);
            Assert.AreEqual(1f, back.Steer, 0.01f);
            Assert.Less(Quaternion.Angle(Quaternion.identity, back.Rotation), 0.01f);
        }
    
        // G-audit (19 Nov): a hand-made packet with a huge position is clamped like a sent one.
        [Test]
        public void HugePositionInAPacket_IsClampedOnDecode()
        {
            byte[] bytes = KartPoseCodec.Encode(Sample());
            bytes[9] = 0xFF; bytes[10] = 0xFF; bytes[11] = 0xFF; bytes[12] = 0x7F;   // x = int.MaxValue cm
            bytes[17] = 0x00; bytes[18] = 0x00; bytes[19] = 0x00; bytes[20] = 0x80;  // z = int.MinValue cm
            KartPose pose;
            Assert.IsTrue(KartPoseCodec.TryDecode(bytes, out pose));
            Assert.AreEqual(KartPoseCodec.MaxMetres, pose.Position.x);
            Assert.AreEqual(-KartPoseCodec.MaxMetres, pose.Position.z);
        }
    }
}
