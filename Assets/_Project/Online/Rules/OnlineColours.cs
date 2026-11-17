using UnityEngine;

namespace CarcavelosCircuit.Online
{
    // The kart colours of the online room: fifteen that stay apart on the road, the minimap and the name tags
    // (one per possible player, OnlineRooms.MaxPlayers). A player's colour index is picked at join and travels
    // with the player, so everyone paints that kart the same.
    public static class OnlineColours
    {
        private static readonly Color[] Palette =
        {
            new Color(0.88f, 0.16f, 0.16f), // red
            new Color(0.16f, 0.46f, 0.96f), // blue
            new Color(0.16f, 0.66f, 0.36f), // green
            new Color(0.98f, 0.80f, 0.12f), // yellow
            new Color(0.56f, 0.27f, 0.86f), // purple
            new Color(1.00f, 0.54f, 0.12f), // orange
            new Color(0.10f, 0.75f, 0.80f), // teal
            new Color(0.95f, 0.40f, 0.70f), // pink
            new Color(0.55f, 0.35f, 0.15f), // brown
            new Color(0.75f, 0.85f, 0.25f), // lime
            new Color(0.95f, 0.95f, 0.95f), // white
            new Color(0.20f, 0.20f, 0.25f), // charcoal
            new Color(0.40f, 0.60f, 0.95f), // sky
            new Color(0.80f, 0.20f, 0.45f), // wine
            new Color(0.30f, 0.80f, 0.60f)  // mint
        };

        public static int Count { get { return Palette.Length; } }

        // The colour for an index; any number wraps around, so a bad index still gives a real colour.
        public static Color Of(int index)
        {
            int wrapped = index % Palette.Length;
            if (wrapped < 0)
            {
                wrapped += Palette.Length;
            }
            return Palette[wrapped];
        }

        // A random index for a new player (two of fifteen may clash now and then, the names tell them apart).
        public static int RandomIndex(System.Random random)
        {
            return random == null ? 0 : random.Next(Palette.Length);
        }
    }
}
