using System;
using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // The room, seen from the game (design doc "Session interface", 2026-11-16). One implementation per transport:
    // LoopbackSession (same process, Edit Mode tests and the Editor), BroadcastChannelSession (tabs of one browser,
    // ?net=local) and later the Photon Fusion adapter ("photon", behind CARCAVELOS_FUSION). The director only
    // talks to this interface, so the race logic is the same on every transport.
    // Rules every implementation keeps (the director relies on them):
    // - Players is everyone in the room including me, ordered by join order (earliest first). That order is the
    //   roster order the master sends, so the grid slot is the index in it.
    // - Changed and PoseReceived fire on the main thread only, and only after Status/Players/Control already show
    //   the new state. A handler may call back into the session.
    // - Now is the same clock on every client of the room (within about a frame); it only moves forward.
    // - MasterSetControl does nothing unless IsMaster. Exactly one player in the room is master; when it leaves,
    //   another one becomes master (join order).
    // - SendPose: newest wins, nothing is queued; ReportFinish: the first report per raceId counts.
    // - Leave is safe at any time; after it Status is Idle and the lists are empty. Dispose = Leave.
    public interface IOnlineSession : IDisposable
    {
        OnlineStatus Status { get; }
        string FailReason { get; }
        int LocalId { get; }
        bool IsMaster { get; }
        double Now { get; }
        IReadOnlyList<OnlinePlayer> Players { get; }
        RaceControl Control { get; }
        // driverIndex = the picked driver (DriverRoster), so the others build my kart with that driver's model.
        void Join(string room, string name, int colourIndex, int driverIndex);
        void Leave();
        void SendPose(in KartPose pose);
        void ReportFinish(int raceId, double lapSeconds);
        void MasterSetControl(RaceControl c);
        event Action<int, KartPose> PoseReceived;
        event Action Changed;
    }
}
