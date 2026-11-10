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
        [SerializeField, Tooltip("Bica: one short espresso kick")]
        private float bicaSeconds = 1f;
        [SerializeField]
        private float bicaMultiplier = 1.4f;

        [SerializeField, Tooltip("Triple Bica: each of the three kicks")]
        private float tripleBicaSeconds = 0.9f;
        [SerializeField]
        private float tripleBicaMultiplier = 1.4f;

        [SerializeField, Tooltip("Onda: one long Atlantic wave, the fastest thing in the game")]
        private float ondaSeconds = 2.5f;
        [SerializeField]
        private float ondaMultiplier = 1.6f;

        [Header("Catch-up odds (ItemOdds)")]
        [SerializeField, Tooltip("Within +- this many seconds of your best split you count as level")]
        private float levelSeconds = 0.5f;

        [SerializeField, Tooltip("This many seconds behind your best split (or more) gives the best odds")]
        private float farBehindSeconds = 2f;

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

        // Boost length for one use of this item (0 for None).
        public float BoostSecondsFor(ItemKind kind)
        {
            if (kind == ItemKind.Bica) return bicaSeconds;
            if (kind == ItemKind.TripleBica) return tripleBicaSeconds;
            if (kind == ItemKind.Onda) return ondaSeconds;
            return 0f;
        }

        // Top speed multiplier while this item's boost runs (1 for None).
        public float BoostMultiplierFor(ItemKind kind)
        {
            if (kind == ItemKind.Bica) return bicaMultiplier;
            if (kind == ItemKind.TripleBica) return tripleBicaMultiplier;
            if (kind == ItemKind.Onda) return ondaMultiplier;
            return 1f;
        }
    }
}
