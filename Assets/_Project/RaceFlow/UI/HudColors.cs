using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The HUD palette for C# (the speed gauge, the minimap, the item slot and the plate pictures draw in code, so they
    // cannot read USS). Same values as the tokens at the top of UI/HudArt.uss: change both together.
    // Base colours from docs/art-style.md (Luc). The "Azulejo Arcade" UI tokens (task R15: Ink, Plate, the plate
    // gradients, Cream, Sun/Silver/Bronze, Coral, Mint) are UI only and still need Luc's OK as rows in docs/art-style.md.
    public static class HudColors
    {
        // Text and light strokes are a warm cream (matches the sand), dark text and outlines a deep navy "ink":
        // a black outline on blue looks dirty, navy looks printed.
        public static readonly Color White = Hex(0xFF, 0xF6, 0xE3);     // = Cream
        public static readonly Color Black = Hex(0x0E, 0x22, 0x38);     // = Ink
        public static readonly Color Asphalt = Hex(0x55, 0x58, 0x5E);
        public static readonly Color Sand = Hex(0xE3, 0xC4, 0x8D);
        public static readonly Color Blue = Hex(0x2D, 0x8C, 0xD6);
        public static readonly Color BlueDeep = Hex(0x12, 0x3B, 0x5E);  // darker step of Atlantic Blue (UI only)
        public static readonly Color Orange = Hex(0xFF, 0x8A, 0x1F);
        public static readonly Color Coral = Hex(0xE6, 0x46, 0x3C);     // behind / slower (chips only)

        public static readonly Color Ink = Hex(0x0E, 0x22, 0x38);       // every text outline and hard shadow
        public static readonly Color Cream = Hex(0xFF, 0xF6, 0xE3);     // text on dark plates, plate strokes
        public static readonly Color Plate = Hex(0x13, 0x31, 0x4F);     // panel fill (used at 0.94)
        public static readonly Color PlateTop = Hex(0x1D, 0x46, 0x73);  // top of the panel gradient
        public static readonly Color PlateAlt = Hex(0x18, 0x3C, 0x60);  // every second leaderboard row
        public static readonly Color PlateBlueHi = Hex(0x45, 0xA8, 0xF2);
        public static readonly Color PlateBlueLo = Hex(0x1E, 0x6B, 0xB3);
        public static readonly Color PlateOrangeHi = Hex(0xFF, 0xB0, 0x4A);
        public static readonly Color PlateOrangeLo = Hex(0xF2, 0x7A, 0x12);
        public static readonly Color LipOrange = Hex(0xA8, 0x4A, 0x00); // the lip under orange buttons
        public static readonly Color Sun = Hex(0xFF, 0xC9, 0x3C);       // 1st place, gold medal
        public static readonly Color Silver = Hex(0xD6, 0xDE, 0xE8);    // 2nd place
        public static readonly Color Bronze = Hex(0xD9, 0x8B, 0x4E);    // 3rd place
        public static readonly Color Mint = Hex(0x3F, 0xD6, 0x8C);      // ahead / new record

        // Drift stage colours: 1 = blue, 2 = orange (IKartState.DriftStage). Same as --drift-s1/--drift-s2 in the USS.
        public static Color DriftStage(int stage)
        {
            if (stage >= 2) return Orange;
            return stage == 1 ? Blue : White;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
