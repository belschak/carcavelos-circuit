using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Every number of the beach ambience in one asset (Ambience/Settings/AmbienceSettings.asset, task R12).
    // Made by code (AmbienceBuilder, run by the Role 4 scene builder); tune it in the Inspector.
    [CreateAssetMenu(fileName = "AmbienceSettings", menuName = "Carcavelos Circuit/Ambience Settings")]
    public class AmbienceSettings : ScriptableObject
    {
        [Header("Finding the sea and the beach (read only, nothing in the track scene is changed)")]
        [SerializeField, Tooltip("Names of the sea object in the track scene, tried in this order (exact name, any case). Luc's campus world calls it Backdrop_Sea")]
        private string[] seaNames = { "Backdrop_Sea", "Sea", "Ocean", "Water" };

        [SerializeField, Tooltip("Objects on the racing line (exact names). The one closest to the water decides which beach gets the waves")]
        private string[] anchorNames = { "SpawnPoint", "Checkpoint", "RespawnPoint" };

        [SerializeField, Tooltip("Metres of beach that get waves, centred on the beach closest to the racing line")]
        private float beachLength = 240f;

        [SerializeField, Tooltip("Points along the beach where the waterline is measured (more = follows the coast better)")]
        private int beachSamples = 120;

        [SerializeField, Tooltip("Metres between two ground checks when walking from the racing line to the water")]
        private float scanStep = 2f;

        [SerializeField, Tooltip("Furthest a beach may be from the racing line, in metres. Further = no waves")]
        private float maxScanDistance = 260f;

        [Header("Waves (distances in metres from the waterline, out to sea)")]
        [SerializeField, Tooltip("Where a swell appears out at sea")]
        private float spawnDistance = 105f;

        [SerializeField, Tooltip("Where the first part of a wave breaks (the peak)")]
        private float breakDistance = 26f;

        [SerializeField, Tooltip("How fast a wave rolls towards the beach, m/s")]
        private float waveSpeed = 4.2f;

        [SerializeField, Tooltip("Waves per set (surf comes in sets with a calm gap between them)")]
        private int wavesPerSet = 5;

        [SerializeField, Tooltip("Seconds between two waves of a set")]
        private float wavePeriod = 7f;

        [SerializeField, Tooltip("Extra calm seconds after a set")]
        private float setLull = 11f;

        [SerializeField, Tooltip("Height of a swell far out, in metres")]
        private float swellHeight = 0.45f;

        [SerializeField, Tooltip("Height of a set wave just before it breaks, in metres (stylised: a bit bigger than real)")]
        private float breakHeight = 2.3f;

        [SerializeField, Tooltip("Height of the white foam roll on its way in, as a share of the break height (right at the break the foam is as tall as the wave)")]
        private float whitewaterShare = 0.5f;

        [SerializeField, Tooltip("How fast the breaking point travels along the wave (the 'peel'), m/s. Surfers ride just ahead of it")]
        private float peelSpeed = 5.2f;

        [SerializeField, Tooltip("How far the last foam runs up the sand, in metres")]
        private float runUp = 7f;

        [SerializeField, Tooltip("Seconds the foam needs to run up the sand and fade")]
        private float swashSeconds = 7f;

        [SerializeField, Tooltip("Metres at each end of the beach where the waves fade out softly")]
        private float endFade = 32f;

        [Header("Wave colours (sRGB, art-style palette: Atlantic Blue family + white foam)")]
        [SerializeField, Tooltip("Water at the foot of a wave (blends into Luc's sea)")]
        private Color deepWater = new Color(0.13f, 0.47f, 0.78f, 1f);

        [SerializeField, Tooltip("Wave face")]
        private Color faceWater = new Color(0.05f, 0.36f, 0.60f, 1f);

        [SerializeField, Tooltip("Thin water near the crest, where the sun shines through")]
        private Color crestWater = new Color(0.40f, 0.90f, 0.88f, 1f);

        [SerializeField, Tooltip("Foam")]
        private Color foam = new Color(0.97f, 0.99f, 1f, 1f);

        [Header("Surfers")]
        [SerializeField, Tooltip("How many surfers (2 to 4)")]
        private int surferCount = 4;

        [SerializeField, Tooltip("Size of the surfers and boards (1 = real size). A bit bigger reads better from the racing line, 80 m away")]
        private float surferScale = 1.3f;

        [SerializeField, Tooltip("Paddling speed, m/s")]
        private float paddleSpeed = 1.4f;

        [SerializeField, Tooltip("A surfer starts paddling for a wave when its crest is this many metres away")]
        private float catchWindow = 8f;

        [SerializeField, Tooltip("A wave must be at least this high (m) where the surfer is, or they let it pass")]
        private float minRideHeight = 0.9f;

        [SerializeField, Tooltip("Slowest and fastest riding speed along the wave, m/s (slower than the peel = the foam catches you)")]
        private Vector2 rideSpeed = new Vector2(5.0f, 7.0f);

        [SerializeField, Tooltip("Chance (0..1) that a ride ends in a fall")]
        private float fallChance = 0.3f;

        [SerializeField, Tooltip("Seconds of a fall before the surfer is gone")]
        private float fallSeconds = 1.3f;

        [SerializeField, Tooltip("Seconds a surfer stays away after a fall, then appears again out at sea")]
        private float respawnSeconds = 4f;

        [SerializeField, Tooltip("A ride ends (kick out over the back) when the wave is this close to the beach, m")]
        private float kickOutDistance = 9f;

        [SerializeField, Tooltip("Seconds for one carving turn (bottom turn + top turn)")]
        private float carveSeconds = 2.2f;

        [SerializeField, Tooltip("Wetsuit base colours: dark slate / navy / teal, never pure black (black hides all shading). Art direction 2026-11-11: mid-dark slate (#404D5C) + a coloured panel")]
        private Color[] wetsuitColours = { new Color(0.25f, 0.3f, 0.36f), new Color(0.2f, 0.27f, 0.42f), new Color(0.3f, 0.31f, 0.34f), new Color(0.17f, 0.35f, 0.38f) };

        [SerializeField, Tooltip("Board deck colours (cream, sand, Atlantic Blue, white)")]
        private Color[] boardColours = { new Color(1f, 0.965f, 0.89f), new Color(0.914f, 0.784f, 0.58f), new Color(0.118f, 0.42f, 0.702f), new Color(0.95f, 0.96f, 0.97f) };

        [SerializeField, Tooltip("Accent per surfer: the suit's stripe and chest panel (deeper tone), the stripe down the board and the fins (coral, Atlantic Blue, sun, mint)")]
        private Color[] accentColours = { new Color(0.902f, 0.275f, 0.235f), new Color(0.271f, 0.659f, 0.949f), new Color(1f, 0.788f, 0.235f), new Color(0.247f, 0.839f, 0.549f) };

        [SerializeField, Tooltip("Skin tones")]
        private Color[] skinColours = { new Color(0.87f, 0.68f, 0.54f), new Color(0.60f, 0.42f, 0.30f), new Color(0.78f, 0.58f, 0.44f), new Color(0.43f, 0.29f, 0.21f) };

        [SerializeField, Tooltip("Hair colours")]
        private Color[] hairColours = { new Color(0.16f, 0.11f, 0.08f), new Color(0.62f, 0.48f, 0.28f), new Color(0.08f, 0.07f, 0.06f), new Color(0.35f, 0.22f, 0.12f) };

        [SerializeField, Tooltip("Optional: a rigged human model that rides instead of the surfer built in code. It needs a SurferModelPuppet (ISurferVisual) on it. Empty = built in code (SurferBody). Only licence-clean models (e.g. CC0), credited in THIRD_PARTY.md")]
        private GameObject surferModel;

        public string[] SeaNames { get { return seaNames; } }
        public string[] AnchorNames { get { return anchorNames; } }
        public float BeachLength { get { return beachLength; } }
        public int BeachSamples { get { return beachSamples; } }
        public float ScanStep { get { return scanStep; } }
        public float MaxScanDistance { get { return maxScanDistance; } }
        public Color DeepWater { get { return deepWater; } }
        public Color FaceWater { get { return faceWater; } }
        public Color CrestWater { get { return crestWater; } }
        public Color Foam { get { return foam; } }
        public int SurferCount { get { return surferCount; } }
        public float SurferScale { get { return float.IsNaN(surferScale) ? 1f : Mathf.Clamp(surferScale, 0.5f, 3f); } }
        public Color[] WetsuitColours { get { return wetsuitColours; } }
        public Color[] BoardColours { get { return boardColours; } }
        public Color[] AccentColours { get { return accentColours; } }
        public Color[] SkinColours { get { return skinColours; } }
        public Color[] HairColours { get { return hairColours; } }
        public GameObject SurferModel { get { return surferModel; } }

        // The wave numbers as one plain object, so the wave rules can be tested without an asset.
        public WaveTuning MakeWaveTuning()
        {
            var t = new WaveTuning();
            t.SpawnDistance = spawnDistance;
            t.BreakDistance = breakDistance;
            t.Speed = waveSpeed;
            t.WavesPerSet = wavesPerSet;
            t.Period = wavePeriod;
            t.SetLull = setLull;
            t.SwellHeight = swellHeight;
            t.BreakHeight = breakHeight;
            t.WhitewaterShare = whitewaterShare;
            t.PeelSpeed = peelSpeed;
            t.RunUp = runUp;
            t.SwashSeconds = swashSeconds;
            t.BeachLength = beachLength;
            t.EndFade = endFade;
            return t;
        }

        // The surfer numbers as one plain object (tested without an asset).
        public SurferTuning MakeSurferTuning()
        {
            var t = new SurferTuning();
            t.PaddleSpeed = paddleSpeed;
            t.CatchWindow = catchWindow;
            t.MinRideHeight = minRideHeight;
            t.RideSpeedMin = rideSpeed.x;
            t.RideSpeedMax = rideSpeed.y;
            t.FallChance = fallChance;
            t.FallSeconds = fallSeconds;
            t.RespawnSeconds = respawnSeconds;
            t.KickOutDistance = kickOutDistance;
            t.CarveSeconds = carveSeconds;
            t.LineupDistance = breakDistance + 4f;
            return t;
        }
    }
}
