using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The HUD palette for C# (the speed gauge and the gradient textures draw in code, so they cannot read USS).
    // Same values as the tokens at the top of RaceHud.uss: change both together.
    // Colours from docs/art-style.md (Luc), plus two UI-only tokens (BlueDeep, Coral) that still need Luc's OK as rows in docs/art-style.md (HUD spec, open question 1).
    public static class HudColors
    {
        public static readonly Color White = Hex(0xEA, 0xEA, 0xEC);
        public static readonly Color Black = Hex(0x1D, 0x1D, 0x1F);
        public static readonly Color Asphalt = Hex(0x55, 0x58, 0x5E);
        public static readonly Color Sand = Hex(0xE3, 0xC4, 0x8D);
        public static readonly Color Blue = Hex(0x2D, 0x8C, 0xD6);
        public static readonly Color BlueDeep = Hex(0x12, 0x3B, 0x5E);  // darker step of Atlantic Blue (UI only)
        public static readonly Color Orange = Hex(0xFF, 0x8A, 0x1F);
        public static readonly Color Coral = Hex(0xE6, 0x46, 0x3C);     // split "behind" (UI only, waiting for Luc's OK)

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
