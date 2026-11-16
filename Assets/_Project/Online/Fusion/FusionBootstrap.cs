#if CARCAVELOS_FUSION
using UnityEngine;
using UnityEngine.Scripting;

namespace CarcavelosCircuit.Online.Fusion
{
    // Plugs the Photon transport into the game. This whole assembly only exists in the online build (the asmdef has
    // the define constraint CARCAVELOS_FUSION, which OnlineBuildConfig sets when the Fusion SDK and an App ID are
    // there). At start-up it signs "photon" up in OnlineSessions; the Online assembly then makes it the default
    // transport and the Online card on the start screen becomes playable. Nothing else references this class:
    // [Preserve] keeps it through code stripping, the attribute below makes Unity call it before the first scene.
    [Preserve]
    public static class FusionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            OnlineSessions.Register(OnlineSessions.Photon, CreateSession);
        }

        private static IOnlineSession CreateSession()
        {
            return new FusionSession();
        }
    }
}
#endif
