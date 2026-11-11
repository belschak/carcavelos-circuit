using System;

namespace CarcavelosCircuit.RaceFlow
{
    // Online first, this device when offline (task R10). The game must keep working when the service is down
    // (AGENTS.md, course rules), so:
    // - SubmitTime ALWAYS saves on this device first, then online. Online fails: the answer comes from the device,
    //   marked OfflineFallback, and the HUD says "Leaderboard offline - your time is saved on this device".
    // - FetchTop asks online; when that fails it shows the device's list, also marked OfflineFallback.
    // - online == null (no URL set up): only the device, marked ThisDevice (no "offline" warning: nothing is broken).
    public class FallbackLeaderboardService : ILeaderboardService
    {
        private readonly ILeaderboardService online;
        private readonly ILeaderboardService device;

        public FallbackLeaderboardService(ILeaderboardService online)
        {
            this.online = online;
            device = new LocalLeaderboardService(online == null ? LeaderboardSource.ThisDevice : LeaderboardSource.OfflineFallback);
        }

        public void SubmitTime(string track, string name, float seconds, Action<LeaderboardReply> done)
        {
            LeaderboardReply local = null;
            device.SubmitTime(track, name, seconds, reply => local = reply); // the device answers at once
            if (online == null)
            {
                done(local);
                return;
            }
            online.SubmitTime(track, name, seconds, reply => done(reply.Success ? reply : local));
        }

        public void FetchTop(string track, int count, Action<LeaderboardReply> done)
        {
            if (online == null)
            {
                device.FetchTop(track, count, done);
                return;
            }
            online.FetchTop(track, count, reply =>
            {
                if (reply.Success)
                {
                    done(reply);
                }
                else
                {
                    device.FetchTop(track, count, done);
                }
            });
        }
    }
}
