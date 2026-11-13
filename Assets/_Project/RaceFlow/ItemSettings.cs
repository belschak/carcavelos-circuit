using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Every item-box tuning number in one asset (RaceFlow/Settings/ItemSettings.asset, task R7).
    [CreateAssetMenu(fileName = "ItemSettings", menuName = "Carcavelos Circuit/Item Settings")]
    public class ItemSettings : ScriptableObject
    {
        [Header("Boxes on the track")]
        [SerializeField, Tooltip("A row of boxes after checkpoint 0, then after every Nth checkpoint (2 = after 0, 2, 4 ...)")]
        private int rowEveryCheckpoints = 2;

        [SerializeField, Tooltip("How many metres after the checkpoint the row stands")]
        private float rowAheadMetres = 14f;

        [SerializeField, Tooltip("Boxes per row, side by side across the road")]
        private int boxesPerRow = 3;

        [SerializeField, Tooltip("Metres between two boxes in a row (the campus road is about 8 m wide)")]
        private float boxSpacing = 2.6f;

        [SerializeField, Tooltip("Edge length of one box in metres")]
        private float boxSize = 1.2f;

        [SerializeField, Tooltip("Height of the box centre above the road")]
        private float hoverHeight = 1.1f;

        [SerializeField, Tooltip("Seconds until a broken box grows back")]
        private float respawnSeconds = 3f;

        [Header("Slot")]
        [SerializeField, Tooltip("Seconds the icons spin in the HUD slot before the item is ready")]
        private float rollSeconds = 1.1f;

        [Header("Boosts (seconds, top speed multiplier; the drift boost is 0.4-1.2 s at 1.35)")]
        [SerializeField, Tooltip("Espresso: one coffee kick (Luc's design: holds 2 s)")]
        private float espressoSeconds = 2f;
        [SerializeField]
        private float espressoMultiplier = 1.4f;

        [SerializeField, Tooltip("Carcavelos Wave: you surf one long wave, the fastest thing in the game")]
        private float waveSeconds = 3f;
        [SerializeField]
        private float waveMultiplier = 1.6f;

        [Header("Seagull Delivery (lifebuoy rings on the road ahead)")]
        [SerializeField, Tooltip("How many rings the gull drops")]
        private int ringCount = 3;

        [SerializeField, Tooltip("Metres from the kart to the first ring")]
        private float ringFirstMetres = 18f;

        [SerializeField, Tooltip("Metres between two rings")]
        private float ringSpacingMetres = 14f;

        [SerializeField, Tooltip("Radius of a ring in metres (the kart drives through the middle)")]
        private float ringRadius = 1.7f;

        [SerializeField, Tooltip("Seconds a ring stays on the road before the gull takes it back")]
        private float ringLifeSeconds = 9f;

        [SerializeField, Tooltip("Boost per ring you drive through (rings chain)")]
        private float ringBoostSeconds = 0.9f;
        [SerializeField]
        private float ringBoostMultiplier = 1.5f;

        [Header("Dean's List (golden boost + bump shield)")]
        [SerializeField, Tooltip("The golden boost the moment you use it")]
        private float deansBoostSeconds = 1.5f;
        [SerializeField]
        private float deansBoostMultiplier = 1.5f;

        [SerializeField, Tooltip("Seconds the shield waits for a bump before it runs out")]
        private float shieldSeconds = 8f;

        [SerializeField, Tooltip("The boost that gives your speed back after the shield took a bump")]
        private float shieldRefundSeconds = 1.2f;
        [SerializeField]
        private float shieldRefundMultiplier = 1.5f;

        [SerializeField, Tooltip("A bump = the speed falls by at least this many m/s from one frame to the next ...")]
        private float bumpMinDrop = 4f;

        [SerializeField, Tooltip("... and by at least this share of the speed (0.3 = 30 %); braking is much slower")]
        private float bumpDropShare = 0.3f;

        [Header("Catch-up odds (ItemOdds)")]
        [SerializeField, Tooltip("Within +- this many seconds of your best split you count as level")]
        private float levelSeconds = 0.5f;

        [SerializeField, Tooltip("This many seconds behind your best split (or more) gives the best odds")]
        private float farBehindSeconds = 2f;

        [Header("HUD icons (Role 3's PNGs, request Q16; empty = the icon drawn in code, UI/ItemIcon.cs)")]
        [SerializeField, Tooltip("Visuals/Items/Icons/Espresso.png (Role4SceneBuilder fills these four by itself)")]
        private Texture2D espressoIcon;
        [SerializeField, Tooltip("Visuals/Items/Icons/SeagullDelivery.png")]
        private Texture2D seagullDeliveryIcon;
        [SerializeField, Tooltip("Visuals/Items/Icons/CarcavelosWave.png")]
        private Texture2D carcavelosWaveIcon;
        [SerializeField, Tooltip("Visuals/Items/Icons/DeansList.png")]
        private Texture2D deansListIcon;

        public int RowEveryCheckpoints { get { return rowEveryCheckpoints; } }
        public float RowAheadMetres { get { return rowAheadMetres; } }
        public int BoxesPerRow { get { return boxesPerRow; } }
        public float BoxSpacing { get { return boxSpacing; } }
        public float BoxSize { get { return boxSize; } }
        public float HoverHeight { get { return hoverHeight; } }
        public float RespawnSeconds { get { return respawnSeconds; } }
        public float RollSeconds { get { return rollSeconds; } }
        public float LevelSeconds { get { return levelSeconds; } }
        public float FarBehindSeconds { get { return farBehindSeconds; } }
        public int RingCount { get { return ringCount; } }
        public float RingFirstMetres { get { return ringFirstMetres; } }
        public float RingSpacingMetres { get { return ringSpacingMetres; } }
        public float RingRadius { get { return ringRadius; } }
        public float RingLifeSeconds { get { return ringLifeSeconds; } }
        public float RingBoostSeconds { get { return ringBoostSeconds; } }
        public float RingBoostMultiplier { get { return ringBoostMultiplier; } }
        public float ShieldSeconds { get { return shieldSeconds; } }
        public float ShieldRefundSeconds { get { return shieldRefundSeconds; } }
        public float ShieldRefundMultiplier { get { return shieldRefundMultiplier; } }
        public float BumpMinDrop { get { return bumpMinDrop; } }
        public float BumpDropShare { get { return bumpDropShare; } }

        // The boost the moment this item is used (0 seconds = no boost on use: the Seagull's rings boost later).
        public float BoostSecondsFor(ItemKind kind)
        {
            if (kind == ItemKind.Espresso) return espressoSeconds;
            if (kind == ItemKind.CarcavelosWave) return waveSeconds;
            if (kind == ItemKind.DeansList) return deansBoostSeconds;
            return 0f;
        }

        // Luc's picture for this item in the HUD slot (request Q16). null = no picture set (or ItemKind.None):
        // ItemIcon then draws its own code-drawn icon, so the slot never stays empty.
        public Texture2D IconFor(ItemKind kind)
        {
            if (kind == ItemKind.Espresso) return espressoIcon;
            if (kind == ItemKind.SeagullDelivery) return seagullDeliveryIcon;
            if (kind == ItemKind.CarcavelosWave) return carcavelosWaveIcon;
            if (kind == ItemKind.DeansList) return deansListIcon;
            return null;
        }

        // Top speed multiplier while that boost runs (1 = no boost).
        public float BoostMultiplierFor(ItemKind kind)
        {
            if (kind == ItemKind.Espresso) return espressoMultiplier;
            if (kind == ItemKind.CarcavelosWave) return waveMultiplier;
            if (kind == ItemKind.DeansList) return deansBoostMultiplier;
            return 1f;
        }
    }
}
