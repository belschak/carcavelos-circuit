using System;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Driver select (Luc, 17 Nov 2026): the four drivers of Luc's NovaKart roster, each with their own kart (pick
    // the driver, get the kart). Looks only: every kart drives exactly the same (Role 1's KartController), so the
    // Time Attack leaderboard stays fair. The pick changes the player's kart model (Visuals/Bots/BotSquad swaps it),
    // the player's colour on the minimap and name tag, and the bots: the three drivers not picked race in their own
    // karts, plus two repainted Rookies (Visuals/Bots/DriverSwap).
    public static class DriverRoster
    {
        // Index of the Rookie: the kart's own orange paint, the player's driver before any pick.
        public const int Rookie = 0;

        private static readonly DriverInfo[] drivers =
        {
            new DriverInfo("Rookie", "rookie", "KartVisual_Classic_Rookie", PlayerRacer.PlayerColour,
                "A first-year Nova student who learned to drive in the campus car park."),
            new DriverInfo("Tiago", "tiago", "KartVisual_Buggy_Tiago", new Color(0.10f, 0.72f, 0.68f),
                "A Carcavelos surfer who drives his beach buggy straight off the sand."),
            new DriverInfo("Nova", "nova", "KartVisual_Orbit_Nova", new Color(0.30f, 0.55f, 1.00f),
                "A space cadet who flies her rocket kart a little too close to the ground."),
            new DriverInfo("Amara", "amara", "KartVisual_StreetGT_Amara", new Color(0.62f, 0.32f, 0.95f),
                "The kart mechanic who built her own hot rod from spare parts.")
        };

        public static int Count { get { return drivers.Length; } }

        // The driver at this index; out of range = the Rookie, so a bad saved number never breaks the game.
        public static DriverInfo Get(int index)
        {
            return IsValid(index) ? drivers[index] : drivers[Rookie];
        }

        public static bool IsValid(int index)
        {
            return index >= 0 && index < drivers.Length;
        }

        // Index of the driver with this name (case does not matter), -1 if none.
        public static int IndexOf(string displayName)
        {
            for (int i = 0; i < drivers.Length; i++)
            {
                if (string.Equals(drivers[i].Name, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        // Moves the highlight left (-1) or right (+1) and wraps around at the ends.
        public static int Step(int index, int direction)
        {
            if (!IsValid(index))
            {
                return direction < 0 ? drivers.Length - 1 : 0;
            }
            return ((index + direction) % drivers.Length + drivers.Length) % drivers.Length;
        }
    }

    // One driver: name above the portrait, map colour, kart model, one line under the portrait on hover.
    public class DriverInfo
    {
        public string Name { get; private set; }
        public string PortraitFile { get; private set; } // Resources/DriverPortraits/<this>.png (DriverPortraitBuilder)
        public string KartVisual { get; private set; }   // Role 3's prefab name in Visuals/Kart/Prefabs (kart + driver)
        public Color Colour { get; private set; }        // map dot, name tag and the frame behind the portrait
        public string Line { get; private set; }

        public DriverInfo(string name, string portraitFile, string kartVisual, Color colour, string line)
        {
            Name = name;
            PortraitFile = portraitFile;
            KartVisual = kartVisual;
            Colour = colour;
            Line = line;
        }
    }
}
