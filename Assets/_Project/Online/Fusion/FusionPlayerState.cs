using Fusion;

namespace CarcavelosCircuit.Online.Fusion
{
    // One player of the room as Fusion carries it. Every client spawns one of these for itself (it has state
    // authority: only it writes) and sees everyone else's as a read-only copy. The properties marked [Networked]
    // are what travels; Fusion's weaver turns them into networked state and only sends the words that changed.
    // The pose is the same 33 bytes KartPoseCodec packs for the tab transport, stored in 9 ints, so every transport
    // quantises the same way (whole centimetres, tenths of a degree). FusionSession reads these on the main thread.
    public class FusionPlayerState : NetworkBehaviour
    {
        public const int PoseWordCount = 9; // 33 bytes rounded up to whole ints (36 bytes)
        public const int MaxNameLength = 32;

        [Networked] public int OwnerId { get; set; }                   // OnlinePlayer.Id: Photon's actor number, unique for the life of the room
        [Networked] public double JoinedAt { get; set; }               // shared clock when the player spawned (join order)
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        [Networked] public int ColourIndex { get; set; }
        [Networked] public int DriverIndex { get; set; }               // KartLoadout.Code (driver + kart + tyres): the kart everyone builds for me
        [Networked] public int PoseSeq { get; set; }                   // counts up per SendPose, so a copy knows a new pose arrived
        [Networked, Capacity(PoseWordCount)] public NetworkArray<int> PoseWords => default;
        [Networked] public int FinishRaceId { get; set; }              // 0 = no finish reported yet
        [Networked] public double FinishSeconds { get; set; }

        // Called by the owner right before the object goes live (Fusion's OnBeforeSpawned), so the first state
        // everyone receives already carries name, colour and driver.
        public void Init(int ownerId, double joinedAt, string name, int colourIndex, int driverIndex)
        {
            OwnerId = ownerId;
            JoinedAt = joinedAt;
            string safe = name ?? "";
            PlayerName = safe.Length > MaxNameLength ? safe.Substring(0, MaxNameLength) : safe;
            ColourIndex = colourIndex;
            DriverIndex = OnlinePlayer.SafeDriver(driverIndex);
            PoseSeq = 0;
            FinishRaceId = 0;
            FinishSeconds = 0.0;
        }

        public override void Spawned()
        {
            FusionSession session = FusionSession.Of(Runner);
            if (session != null)
            {
                session.OnPlayerSpawned(this);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            FusionSession session = FusionSession.Of(runner);
            if (session != null)
            {
                session.OnPlayerDespawned(this);
            }
        }

        // Owner only: the newest pose, packed. Little endian bytes into ints, the last int is one byte plus padding.
        public void WritePose(in KartPose pose)
        {
            byte[] bytes = KartPoseCodec.Encode(pose);
            for (int word = 0; word < PoseWordCount; word++)
            {
                int value = 0;
                for (int b = 0; b < 4; b++)
                {
                    int at = word * 4 + b;
                    if (at < bytes.Length)
                    {
                        value |= bytes[at] << (8 * b);
                    }
                }
                PoseWords.Set(word, value);
            }
            PoseSeq = PoseSeq + 1;
        }

        // Anyone: the pose back out of the ints. False when the bytes are not a pose (a foreign or broken client).
        public bool TryReadPose(out KartPose pose)
        {
            byte[] bytes = new byte[KartPoseCodec.Size];
            for (int at = 0; at < bytes.Length; at++)
            {
                bytes[at] = (byte)(PoseWords.Get(at / 4) >> (8 * (at % 4)));
            }
            return KartPoseCodec.TryDecode(bytes, out pose);
        }

        // Owner only.
        public void WriteFinish(int raceId, double seconds)
        {
            FinishRaceId = raceId;
            FinishSeconds = seconds;
        }
    }
}
