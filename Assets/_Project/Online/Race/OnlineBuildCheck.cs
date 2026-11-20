#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CarcavelosCircuit.Online
{
    // Is a newer build of the game online than the one on this page? (Plugins/OnlineBuildCheck.jslib, 20 Nov 2026)
    // The lobby starts the check when it opens and asks once a second; a newer build = the lobby shows a line and
    // reloads into it, back in the lobby with the same name. Outside a Web build: never newer.
    public static class OnlineBuildCheck
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void OnlineBuildCheckStart();
        [DllImport("__Internal")] private static extern string OnlineBuildCheckNewer();
        [DllImport("__Internal")] private static extern void OnlineBuildReload(string build, string room, string name);
#endif

        public const string ReloadLine = "A NEWER VERSION OF THE GAME IS OUT, LOADING IT...";
        public const float ReloadDelaySeconds = 1.5f;

        public static void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OnlineBuildCheckStart();
#endif
        }

        // The newer build's id, or "".
        public static string Newer()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return OnlineBuildCheckNewer() ?? "";
#else
            return "";
#endif
        }

        public static void Reload(string build, string room, string name)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OnlineBuildReload(build ?? "", room ?? "", name ?? "");
#endif
        }
    }
}
