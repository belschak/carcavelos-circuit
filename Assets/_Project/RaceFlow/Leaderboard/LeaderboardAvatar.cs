using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The small round avatar in front of every leaderboard name (task R15): a colour made from the name, so the list
    // reads as racers, not as a table, and the same name always gets the same colour (on every device, every day).
    // A name is turned into a number (a simple hash, the same idea as a checksum) and that number picks one of the
    // kart colours below. Plain C#, tested in LeaderboardAvatarTests.
    public static class LeaderboardAvatar
    {
        // Kart-like colours, all bright enough to read on the navy rows. Orange is left out on purpose:
        // orange means "you" everywhere in the HUD.
        private static readonly Color[] Colours =
        {
            new Color(0.27f, 0.66f, 0.95f, 1f), // Atlantic blue
            new Color(0.25f, 0.84f, 0.55f, 1f), // mint
            new Color(1.00f, 0.79f, 0.24f, 1f), // sun
            new Color(0.90f, 0.27f, 0.24f, 1f), // coral
            new Color(0.70f, 0.52f, 0.95f, 1f), // lilac
            new Color(0.95f, 0.55f, 0.78f, 1f), // pink
            new Color(0.40f, 0.85f, 0.90f, 1f), // aqua
            new Color(0.89f, 0.77f, 0.55f, 1f), // sand
        };

        public static int ColourCount { get { return Colours.Length; } }

        // Which colour (0 .. ColourCount-1) a name gets. Upper and lower case count as the same letter, so "ana" and
        // "Ana" match. Null or an empty name gets colour 0.
        public static int ColourIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0;
            }
            int hash = 17;
            for (int i = 0; i < name.Length; i++)
            {
                hash = unchecked(hash * 31 + char.ToUpperInvariant(name[i]));
            }
            int index = hash % Colours.Length;
            return index < 0 ? index + Colours.Length : index; // % keeps the sign of a negative hash
        }

        public static Color ColourFor(string name)
        {
            return Colours[ColourIndex(name)];
        }
    }
}
