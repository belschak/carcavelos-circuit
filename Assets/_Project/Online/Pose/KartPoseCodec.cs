using System;
using UnityEngine;

namespace CarcavelosCircuit.Online
{
    // Packs a KartPose into 33 bytes and back. Small packets matter on bad campus Wi-Fi (a WebGL connection is TCP,
    // so a lost packet stalls everything behind it). Same idea as GhostRecording: whole centimetres, tenths of a degree.
    // Layout (little endian, written by hand so it is the same on every machine):
    //   byte 0      format version (1)
    //   bytes 1-8   time, double (seconds on the shared clock)
    //   bytes 9-20  position x, y, z as int32 centimetres
    //   bytes 21-26 pitch, yaw, roll as int16 tenths of a degree (-180.0 .. 180.0)
    //   bytes 27-28 speed as int16 centimetres per second
    //   byte 29     steer as sbyte (-127 .. 127)
    //   byte 30     drift stage (0, 1, 2)
    //   byte 31     drift charge 0..255
    //   byte 32     flags (drifting, boosting, grounded)
    public static class KartPoseCodec
    {
        public const int Size = 33;
        public const byte Version = 1;

        // Positions further than this from the centre (metres) are clamped: the campus is about 1 km wide.
        public const float MaxMetres = 20000f;

        public static byte[] Encode(KartPose pose)
        {
            byte[] bytes = new byte[Size];
            bytes[0] = Version;
            WriteInt64(bytes, 1, BitConverter.DoubleToInt64Bits(SafeTime(pose.Time)));
            WriteInt32(bytes, 9, Centimetres(pose.Position.x));
            WriteInt32(bytes, 13, Centimetres(pose.Position.y));
            WriteInt32(bytes, 17, Centimetres(pose.Position.z));

            Vector3 euler = SafeRotation(pose.Rotation).eulerAngles; // 0..360 per axis
            WriteInt16(bytes, 21, Tenths(euler.x));
            WriteInt16(bytes, 23, Tenths(euler.y));
            WriteInt16(bytes, 25, Tenths(euler.z));

            WriteInt16(bytes, 27, ClampShort(Mathf.RoundToInt(Safe(pose.Speed) * 100f)));
            bytes[29] = (byte)(sbyte)Mathf.RoundToInt(Mathf.Clamp(Safe(pose.Steer), -1f, 1f) * 127f);
            bytes[30] = (byte)Mathf.Clamp(pose.DriftStage, 0, 2);
            bytes[31] = (byte)Mathf.RoundToInt(Mathf.Clamp01(Safe(pose.DriftCharge01)) * 255f);
            bytes[32] = (byte)(pose.Flags & (KartPose.DriftingFlag | KartPose.BoostingFlag | KartPose.GroundedFlag));
            return bytes;
        }

        // False (and a default pose) for anything that is not exactly one pose of our format: wrong length,
        // another version, or a time that is not a real number. Everything from the network goes through here.
        public static bool TryDecode(byte[] bytes, out KartPose pose)
        {
            pose = new KartPose();
            if (bytes == null || bytes.Length != Size || bytes[0] != Version)
            {
                return false;
            }
            double time = BitConverter.Int64BitsToDouble(ReadInt64(bytes, 1));
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0.0)
            {
                return false;
            }
            pose.Time = time;
            pose.Position = new Vector3(Metres(ReadInt32(bytes, 9)), Metres(ReadInt32(bytes, 13)), Metres(ReadInt32(bytes, 17)));
            pose.Rotation = Quaternion.Euler(ReadInt16(bytes, 21) / 10f, ReadInt16(bytes, 23) / 10f, ReadInt16(bytes, 25) / 10f);
            pose.Speed = ReadInt16(bytes, 27) / 100f;
            pose.Steer = Mathf.Clamp((sbyte)bytes[29] / 127f, -1f, 1f);
            pose.DriftStage = Mathf.Clamp(bytes[30], 0, 2);
            pose.DriftCharge01 = bytes[31] / 255f;
            pose.Flags = (byte)(bytes[32] & (KartPose.DriftingFlag | KartPose.BoostingFlag | KartPose.GroundedFlag));
            return true;
        }

        // Base64 text for transports that only carry strings (the BroadcastChannel test transport).
        public static string EncodeText(KartPose pose)
        {
            return Convert.ToBase64String(Encode(pose));
        }

        public static bool TryDecodeText(string text, out KartPose pose)
        {
            pose = new KartPose();
            if (string.IsNullOrEmpty(text) || text.Length > 64)
            {
                return false;
            }
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(text);
            }
            catch (FormatException)
            {
                return false; // not base64: a broken or foreign message
            }
            return TryDecode(bytes, out pose);
        }

        // An angle 0..360 as signed tenths of a degree (-1800 .. 1800), so it fits an int16.
        private static int Tenths(float degrees)
        {
            float signed = Mathf.Repeat(degrees + 180f, 360f) - 180f;
            return Mathf.RoundToInt(signed * 10f);
        }

        private static int Centimetres(float metres)
        {
            return Mathf.RoundToInt(Mathf.Clamp(Safe(metres), -MaxMetres, MaxMetres) * 100f);
        }

        // A received position, clamped like a sent one: a hand-made packet cannot put a kart 20 000 km away.
        private static float Metres(int centimetres)
        {
            return Mathf.Clamp(centimetres / 100f, -MaxMetres, MaxMetres);
        }

        private static int ClampShort(int value)
        {
            return Mathf.Clamp(value, short.MinValue, short.MaxValue);
        }

        // NaN or infinity (a broken physics frame) becomes 0, so it never travels over the network.
        private static float Safe(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static double SafeTime(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 ? 0.0 : value;
        }

        // A default (all zero) quaternion is not a rotation; it becomes "no rotation".
        private static Quaternion SafeRotation(Quaternion q)
        {
            float lengthSquared = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (lengthSquared < 0.0001f || float.IsNaN(lengthSquared))
            {
                return Quaternion.identity;
            }
            return q;
        }

        private static void WriteInt16(byte[] b, int at, int value)
        {
            b[at] = (byte)value;
            b[at + 1] = (byte)(value >> 8);
        }

        private static short ReadInt16(byte[] b, int at)
        {
            return (short)(b[at] | (b[at + 1] << 8));
        }

        private static void WriteInt32(byte[] b, int at, int value)
        {
            for (int i = 0; i < 4; i++)
            {
                b[at + i] = (byte)(value >> (8 * i));
            }
        }

        private static int ReadInt32(byte[] b, int at)
        {
            return b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);
        }

        private static void WriteInt64(byte[] b, int at, long value)
        {
            for (int i = 0; i < 8; i++)
            {
                b[at + i] = (byte)(value >> (8 * i));
            }
        }

        private static long ReadInt64(byte[] b, int at)
        {
            long value = 0;
            for (int i = 0; i < 8; i++)
            {
                value |= (long)b[at + i] << (8 * i);
            }
            return value;
        }
    }
}
