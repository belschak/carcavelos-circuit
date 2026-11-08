using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Every race-flow tuning number in one asset (RaceFlow/Settings/RaceSettings.asset), so nobody hard-codes them.
    [CreateAssetMenu(fileName = "RaceSettings", menuName = "Carcavelos Circuit/Race Settings")]
    public class RaceSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Scene with the real campus track (Role 2). Loaded on top of Main.unity")]
        private string trackSceneName = "Track_Campus";

        [SerializeField, Tooltip("Used only while the campus scene has no track in it yet")]
        private string fallbackTrackSceneName = "RaceFlow_PlaceholderTrack";

        [SerializeField, Tooltip("The countdown starts at this number: 3, 2, 1, GO")]
        private int countdownFrom = 3;

        [SerializeField, Tooltip("Seconds per countdown number on the first start")]
        private float countdownStepSeconds = 1f;

        [SerializeField, Tooltip("Seconds per countdown number after a retry (quicker, players retry a lot)")]
        private float retryCountdownStepSeconds = 0.5f;

        [SerializeField, Tooltip("How long the checkpoint split (+0.412) stays on screen")]
        private float splitPopupSeconds = 2f;

        [SerializeField, Tooltip("How long a hint like \"Missed a checkpoint!\" stays on screen")]
        private float hintSeconds = 2.5f;

        [SerializeField, Tooltip("If the kart falls below this height it is recovered automatically")]
        private float fallRecoverHeight = -20f;

        public string TrackSceneName { get { return trackSceneName; } }
        public string FallbackTrackSceneName { get { return fallbackTrackSceneName; } }
        public int CountdownFrom { get { return countdownFrom; } }
        public float CountdownStepSeconds { get { return countdownStepSeconds; } }
        public float RetryCountdownStepSeconds { get { return retryCountdownStepSeconds; } }
        public float SplitPopupSeconds { get { return splitPopupSeconds; } }
        public float HintSeconds { get { return hintSeconds; } }
        public float FallRecoverHeight { get { return fallRecoverHeight; } }
    }
}
