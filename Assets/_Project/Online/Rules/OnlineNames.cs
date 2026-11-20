using System;
using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // Two players with the same name (20 Nov 2026, presentation safety: a class often has two ANAs, or someone types
    // "TEST" like the person before): the later one shows as "ANA 2" (then "ANA 3") in the lobby, on the name tag
    // and in the results, so nobody wonders which ANA won. Compared without case and outer spaces. Plain C#, tested
    // in OnlineNamesTests.
    public static class OnlineNames
    {
        public const int MaxDisplayLength = 16; // PlayerNameRules.MaxLength 12 + " 15" fits; longer names are cut first

        // Gives every player in `players` (join order) that has no display name yet one that no other player in the
        // list shows. Players that already have one keep it (a name tag never changes while the player is in).
        public static void Assign(IReadOnlyList<OnlinePlayer> players)
        {
            if (players == null)
            {
                return;
            }
            HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].HasDisplayName)
                {
                    taken.Add(Key(players[i].DisplayName));
                }
            }
            for (int i = 0; i < players.Count; i++)
            {
                OnlinePlayer player = players[i];
                if (player == null || player.HasDisplayName)
                {
                    continue;
                }
                string shown = Unique(player.Name, taken);
                taken.Add(Key(shown));
                player.SetDisplayName(shown);
            }
        }

        // name, or "name 2", "name 3" ... the first one not in taken (keys from Key).
        public static string Unique(string name, ICollection<string> taken)
        {
            string baseName = string.IsNullOrWhiteSpace(name) ? "PLAYER" : name.Trim();
            if (taken == null || !taken.Contains(Key(baseName)))
            {
                return baseName;
            }
            for (int n = 2; n < 100; n++)
            {
                string suffix = " " + n;
                string head = baseName.Length + suffix.Length > MaxDisplayLength
                    ? baseName.Substring(0, Math.Max(1, MaxDisplayLength - suffix.Length)).TrimEnd()
                    : baseName;
                string candidate = head + suffix;
                if (!taken.Contains(Key(candidate)))
                {
                    return candidate;
                }
            }
            return baseName;
        }

        private static string Key(string name)
        {
            return (name ?? "").Trim().ToUpperInvariant();
        }
    }
}
