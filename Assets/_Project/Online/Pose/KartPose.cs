using UnityEngine;

namespace CarcavelosCircuit.Online
{
    // One moment of a remote kart, as it travels over the network: where it is, how it is turned and what the
    // kart model needs to look alive (speed for the wheels, steer for the steering wheel, drift and boost).
    // A small value type (struct), so sending 15 of them per second creates no garbage.
    public struct KartPose
    {
        // Flag bits, packed into one byte by KartPoseCodec.
        public const byte DriftingFlag = 1;
        public const byte BoostingFlag = 2;
        public const byte GroundedFlag = 4;

        public double Time;          // shared clock (IOnlineSession.Now) when the sender took this pose, seconds
        public Vector3 Position;     // kart root position in metres
        public Quaternion Rotation;  // kart root rotation
        public float Speed;          // forward speed in m/s
        public float Steer;          // -1 full left .. +1 full right
        public int DriftStage;       // 0 none, 1 blue sparks, 2 orange sparks
        public float DriftCharge01;  // 0..1
        public byte Flags;           // DriftingFlag | BoostingFlag | GroundedFlag

        public bool IsDrifting { get { return (Flags & DriftingFlag) != 0; } }
        public bool IsBoosting { get { return (Flags & BoostingFlag) != 0; } }
        public bool IsGrounded { get { return (Flags & GroundedFlag) != 0; } }

        // Builds the flag byte from three yes/no values.
        public static byte MakeFlags(bool drifting, bool boosting, bool grounded)
        {
            byte flags = 0;
            if (drifting) flags |= DriftingFlag;
            if (boosting) flags |= BoostingFlag;
            if (grounded) flags |= GroundedFlag;
            return flags;
        }
    }
}
