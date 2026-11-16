using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Visuals;
using UnityEngine;

namespace CarcavelosCircuit.Online
{
    // ?autopilot=1: the own kart drives the lap by itself, so one person can fill a room with browser tabs and watch
    // the online race from every seat. Reuses Role 3's FinishCruise (the finish cinematic's self-driver) at a racing
    // pace, fed to the kart through IKartDrivable like a bot (see Visuals/Bots/BotSquad.OnPlayerFinished).
    // Only while Racing: the countdown and the results are the lobby's business.
    public class OnlineAutopilot
    {
        public const float SpeedShare = 0.92f; // close to racing speed, a little under so it stays on the road

        private FinishCruise cruise;
        private IKartDrivable drivable;
        private BotSettings settings;

        public bool IsDriving { get { return cruise != null; } }

        // Takes the wheel. False when something is missing (no racing line, a kart without IKartDrivable).
        public bool Start(RaceManager raceManager, RaceRoster roster)
        {
            Stop();
            if (raceManager == null || roster == null || !raceManager.IsSetUp || raceManager.KartTransform == null || roster.Progress == null)
            {
                return false;
            }
            drivable = raceManager.KartTransform.GetComponent<IKartDrivable>();
            if (drivable == null)
            {
                return false;
            }
            BotTrackMap map = new BotTrackMap(roster.Progress);
            if (!map.IsValid)
            {
                drivable = null;
                return false;
            }
            settings = ScriptableObject.CreateInstance<BotSettings>(); // the built-in bot tuning
            cruise = new FinishCruise(raceManager.KartTransform, raceManager.Kart, map, settings, SpeedShare);
            cruise.Begin();
            drivable.InputSource = cruise;
            return true;
        }

        // Every frame while driving.
        public void Tick(float deltaTime)
        {
            if (cruise != null)
            {
                cruise.Tick(deltaTime);
            }
        }

        // Hands the kart back (only if it is still ours: the finish cinematic may have taken it already).
        public void Stop()
        {
            if (drivable != null && cruise != null && ReferenceEquals(drivable.InputSource, cruise))
            {
                drivable.InputSource = null;
            }
            cruise = null;
            drivable = null;
            if (settings != null)
            {
                Object.Destroy(settings);
                settings = null;
            }
        }
    }
}
