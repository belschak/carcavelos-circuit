using System;
using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // Where the transports sign up (plain C#, tested in OnlineSessionsTests). The Online assembly registers "local"
    // (BroadcastChannelSession on WebGL, LoopbackSession elsewhere) at start-up; the Photon Fusion adapter registers
    // "photon" from its own [RuntimeInitializeOnLoadMethod]. Default is "photon" when it exists: the real build
    // races over Photon, the test build over tabs, and without either the Online card says it needs the online build.
    public static class OnlineSessions
    {
        public const string Photon = "photon";
        public const string Local = "local";

        private static readonly Dictionary<string, Func<IOnlineSession>> factories = new Dictionary<string, Func<IOnlineSession>>();

        // Fired after a transport was registered or the list was cleared (OnlineBootstrap refreshes the hook).
        public static event Action Changed;

        // Adds or replaces a transport. Null or empty names and null factories are refused (false).
        public static bool Register(string name, Func<IOnlineSession> factory)
        {
            if (string.IsNullOrEmpty(name) || factory == null)
            {
                return false;
            }
            factories[name] = factory;
            if (Changed != null)
            {
                Changed();
            }
            return true;
        }

        public static bool Has(string name)
        {
            return !string.IsNullOrEmpty(name) && factories.ContainsKey(name);
        }

        // "photon" when the Photon adapter is registered, else null (no default transport).
        public static string Default
        {
            get { return Has(Photon) ? Photon : null; }
        }

        // A new session of that transport, or null when the name is unknown (the caller shows a message).
        public static IOnlineSession Create(string name)
        {
            Func<IOnlineSession> factory;
            if (string.IsNullOrEmpty(name) || !factories.TryGetValue(name, out factory))
            {
                return null;
            }
            return factory();
        }

        // Tests start from an empty list.
        public static void Clear()
        {
            factories.Clear();
            if (Changed != null)
            {
                Changed();
            }
        }
    }
}
