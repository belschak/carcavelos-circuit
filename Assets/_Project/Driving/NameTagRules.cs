using System.Text;

namespace CarcavelosCircuit.Driving
{
    // Task D9: the rules of the name tags as plain C# (no Unity objects), so they can be unit tested.
    // Who gets a tag, what text may appear on it, and how visible it is at a given distance from the camera.
    public static class NameTagRules
    {
        // Longer names are cut, so a tag never becomes a banner across the road.
        public const int MaxNameLength = 16;

        // The player never sees a tag over their own kart, and a racer without a name gets none.
        public static bool ShouldShow(bool isPlayer, string cleanName)
        {
            return !isPlayer && !string.IsNullOrEmpty(cleanName);
        }

        // The name as it may appear on screen. Names can come from the leaderboard (typed by players), so:
        // no control characters (new lines, tabs), no spaces at the ends, at most MaxNameLength characters.
        // null becomes "" (= no tag).
        public static string CleanName(string raw)
        {
            if (raw == null)
            {
                return "";
            }
            StringBuilder kept = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (!char.IsControl(raw[i]))
                {
                    kept.Append(raw[i]);
                }
            }
            string clean = kept.ToString().Trim();
            if (clean.Length > MaxNameLength)
            {
                int cut = MaxNameLength;
                if (char.IsHighSurrogate(clean[cut - 1]))
                {
                    cut--; // never cut an emoji in half
                }
                clean = clean.Substring(0, cut).TrimEnd();
            }
            return clean;
        }

        // How visible a tag is, 1 = fully, 0 = not at all, by its distance to the camera in metres:
        // invisible up to nearHide (so it never covers the road right in front of you), fading in until nearFull,
        // fully visible until farFull, fading out until farHide, invisible beyond. A bad distance (NaN, negative) = 0.
        public static float Alpha(float distance, float nearHide, float nearFull, float farFull, float farHide)
        {
            if (float.IsNaN(distance) || distance <= nearHide || distance >= farHide)
            {
                return 0f;
            }
            if (distance < nearFull)
            {
                return nearFull > nearHide ? (distance - nearHide) / (nearFull - nearHide) : 1f;
            }
            if (distance <= farFull)
            {
                return 1f;
            }
            return farHide > farFull ? (farHide - distance) / (farHide - farFull) : 0f;
        }
    }
}
