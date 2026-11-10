using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Every race-flow tuning number in one asset (RaceFlow/Settings/RaceSettings.asset), so nobody hard-codes them.
    [CreateAssetMenu(fileName = "RaceSettings", menuName = "Carcavelos Circuit/Race Settings")]
    public class RaceSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Scene with the real campus track (Role 2). Loaded on top of Main.unity")]
        private string trackSceneName = "Track_Campus";

        [SerializeField, Tooltip("Tried in this order when the scene above has no track in it yet: Luc's campus world (#33), the Nova campus help scene (#31). The first one that is in the build list and has a track wins")]
        private string[] otherTrackSceneNames = { "Track_CampusWorld", "Track_NovaCampus" };

        [SerializeField, Tooltip("Used only while none of the scenes above has a track in it yet")]
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

        [SerializeField, Tooltip("After a recover the fall check waits this long, so one fall gives one recover")]
        private float fallCheckCooldownSeconds = 0.5f;

        // Medal times for a lap of medalReferenceLapMetres (our placeholder test loop: about 11 s for a first lap,
        // about 10 s for a good one). A longer track gets proportionally longer times (MedalScaling), so every track
        // has fair medals from day one. Final tuning on the real track: set the reference length to that track's
        // length (the Console prints it at race start) and the four times to the real targets.
        [Header("Medals (lap time in seconds or faster, for a lap of the reference length)")]
        [SerializeField, Tooltip("Lap length the four medal times below are meant for, measured checkpoint to checkpoint. 158 m = the placeholder test loop")]
        private float medalReferenceLapMetres = 158.1f;

        [SerializeField, Tooltip("A first decent lap gets at least this")]
        private float bronzeSeconds = 12f;

        [SerializeField, Tooltip("A first lap without big mistakes")]
        private float silverSeconds = 11f;

        [SerializeField, Tooltip("A clean lap: good lines, no recover")]
        private float goldSeconds = 10.2f;

        [SerializeField, Tooltip("Top tier: a clean lap plus big drift boosts")]
        private float deansListSeconds = 9.6f;

        [Header("Coach (one-time tips for new players)")]
        [SerializeField, Tooltip("Seconds of racing without any drift before the \"how to drift\" tip")]
        private float coachNoDriftSeconds = 5f;

        [SerializeField, Tooltip("How long a coach tip stays on screen (longer than a warning: it is a full sentence)")]
        private float coachHintSeconds = 3.5f;

        public string TrackSceneName { get { return trackSceneName; } }
        public string FallbackTrackSceneName { get { return fallbackTrackSceneName; } }
        public float MedalReferenceLapMetres { get { return medalReferenceLapMetres; } }
        public int CountdownFrom { get { return countdownFrom; } }
        public float CountdownStepSeconds { get { return countdownStepSeconds; } }
        public float RetryCountdownStepSeconds { get { return retryCountdownStepSeconds; } }
        public float SplitPopupSeconds { get { return splitPopupSeconds; } }
        public float HintSeconds { get { return hintSeconds; } }
        public float FallRecoverHeight { get { return fallRecoverHeight; } }
        public float FallCheckCooldownSeconds { get { return fallCheckCooldownSeconds; } }
        public float CoachNoDriftSeconds { get { return coachNoDriftSeconds; } }
        public float CoachHintSeconds { get { return coachHintSeconds; } }

        // Every track scene the race may load, in the order RaceBootstrap tries them (fallback last, empty names skipped).
        public string[] TrackScenesInOrder()
        {
            var names = new System.Collections.Generic.List<string>();
            AddName(names, trackSceneName);
            if (otherTrackSceneNames != null)
            {
                for (int i = 0; i < otherTrackSceneNames.Length; i++)
                {
                    AddName(names, otherTrackSceneNames[i]);
                }
            }
            AddName(names, fallbackTrackSceneName);
            return names.ToArray();
        }

        // Adds a scene name once; empty names and doubles are left out.
        private static void AddName(System.Collections.Generic.List<string> names, string name)
        {
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        // The four medal times in MedalRules order: Bronze, Silver, Gold, Dean's List. A new array per call,
        // so RaceManager calls it once at set-up.
        public float[] MedalThresholds()
        {
            return new float[] { bronzeSeconds, silverSeconds, goldSeconds, deansListSeconds };
        }
    }
}
