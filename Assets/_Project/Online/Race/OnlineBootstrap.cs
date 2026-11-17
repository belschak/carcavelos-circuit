using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Online
{
    // Plugs the online race into RaceFlow at start-up (M2). RaceFlow never references this assembly; it only has
    // OnlineRaceHooks.OpenLobby, and this sets it when a transport exists for this build and page address:
    // - "local" is always registered here: BroadcastChannelSession in a Web build, LoopbackSession elsewhere.
    // - "photon" is registered by the Fusion adapter (CARCAVELOS_FUSION), from its own RuntimeInitializeOnLoadMethod
    //   (any load type: OnlineSessions.Changed refreshes the hook whenever a transport arrives).
    // - Which one the lobby uses: OnlineUrlFlags.PickTransport (?net=..., the default, the Editor fallback).
    // No transport fits = OpenLobby stays null = the Online card says it needs the online build.
    // With ?room=...&name=... in the address the lobby opens and joins by itself (presentation link, test tabs).
    public static class OnlineBootstrap
    {
        private static bool listening;

        public static OnlineUrlFlags Flags
        {
            get { return OnlineUrlFlags.Parse(Application.absoluteURL); }
        }

        // The transport the lobby would use right now, or null.
        public static string TransportName(OnlineUrlFlags flags)
        {
            if (flags == null)
            {
                flags = OnlineUrlFlags.None;
            }
            return OnlineUrlFlags.PickTransport(flags.Net, OnlineSessions.Has(OnlineSessions.Photon), OnlineSessions.Has(OnlineSessions.Local), Application.isEditor);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            OnlineSessions.Register(OnlineSessions.Local, CreateLocal);
            if (!listening)
            {
                OnlineSessions.Changed += RefreshHook;
                listening = true;
            }
            RefreshHook();

            OnlineUrlFlags flags = Flags;
            if (flags.WantsAutoJoin && TransportName(flags) != null)
            {
                OnlineRaceDirector.OpenForAutoJoin(flags);
            }
        }

        // OnlineRaceHooks.Available is "OpenLobby != null", so the hook is only set when a transport fits.
        public static void RefreshHook()
        {
            OnlineRaceHooks.OpenLobby = TransportName(Flags) != null ? (System.Action<RaceManager>)OpenLobby : null;
        }

        private static void OpenLobby(RaceManager raceManager)
        {
            OnlineRaceDirector.Open(raceManager, Flags);
        }

        private static IOnlineSession CreateLocal()
        {
            if (BroadcastChannelSession.IsSupported)
            {
                return new BroadcastChannelSession();
            }
            return new LoopbackSession();
        }
    }
}
