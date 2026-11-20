using System;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Driver select (Luc, 17 Nov 2026): the four drivers of Luc's NovaKart roster, each with their own kart (pick
    // the driver, get the kart). Looks only: every kart drives exactly the same (Role 1's KartController), so the
    // Time Attack leaderboard stays fair. The pick changes the player's kart model (Visuals/Bots/BotSquad swaps it),
    // the player's colour on the minimap and name tag, and the bots: the three drivers not picked race in their own
    // karts, plus two repainted Nova Classics (Visuals/Bots/DriverSwap).
    // Names (David, 20 Nov 2026): every driver carries the first name of one of the Introduction to Programming
    // teachers (Tribute/TributeRules.Teachers, same order as the credits): Ricardo, Cláudio, Luís and Paulo. Luc's
    // models keep their own names (Rookie, Tiago, Nova, Amara: prefabs, portraits, textures), see DriverInfo.Model.
    public static class DriverRoster
    {
        // Index of the first driver (Ricardo, on Luc's Rookie model): the kart's own orange paint, the player's
        // driver before any pick.
        public const int Rookie = 0;

        private static readonly DriverInfo[] drivers =
        {
            new DriverInfo(TributeRules.FirstName(0), "Rookie", "KartVisual_Classic_Rookie", PlayerRacer.PlayerColour,
                "Responsible teacher. Drives the Nova Classic, the kart everyone starts in."),
            new DriverInfo(TributeRules.FirstName(1), "Tiago", "KartVisual_Buggy_Tiago", new Color(0.10f, 0.72f, 0.68f),
                "Wrote most of the course. Takes the beach buggy straight off the sand."),
            new DriverInfo(TributeRules.FirstName(2), "Nova", "KartVisual_Orbit_Nova", new Color(0.30f, 0.55f, 1.00f),
                "Teaching staff. Flies the rocket kart a little too close to the ground."),
            new DriverInfo(TributeRules.FirstName(3), "Amara", "KartVisual_StreetGT_Amara", new Color(0.62f, 0.32f, 0.95f),
                "Teaching staff. Drives a hot rod built from spare parts.")
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

        // Index of the driver with this name, -1 if none. Case and accents do not matter ("claudio" = "Cláudio"),
        // and Luc's model names still work ("Tiago" = Cláudio's driver), so old ?driver= links keep their driver.
        public static int IndexOf(string displayName)
        {
            string wanted = Plain(displayName);
            if (wanted.Length == 0)
            {
                return -1;
            }
            for (int i = 0; i < drivers.Length; i++)
            {
                if (Plain(drivers[i].Name) == wanted || Plain(drivers[i].Model) == wanted)
                {
                    return i;
                }
            }
            return -1;
        }

        // Lower case without the Portuguese accents: "Cláudio" -> "claudio", "LUÍS" -> "luis", "João" -> "joao".
        // A small own table instead of string.Normalize, which the Web build's trimmed .NET may not carry. Null = "".
        private const string Accented = "àáâãäçèéêëìíîïñòóôõöùúûü";
        private const string Unaccented = "aaaaaceeeeiiiinooooouuuu";

        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            char[] letters = text.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < letters.Length; i++)
            {
                int at = Accented.IndexOf(letters[i]);
                if (at >= 0)
                {
                    letters[i] = Unaccented[at];
                }
            }
            return new string(letters);
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
        public string Name { get; private set; }         // the teacher's first name: card, name tag, bots, results, lobby
        public string Model { get; private set; }        // Luc's character model (Rookie, Tiago, Nova, Amara): asset names only
        public string PortraitFile { get; private set; } // Resources/DriverPortraits/<this>.png (DriverPortraitBuilder)
        public string KartVisual { get; private set; }   // Role 3's prefab name in Visuals/Kart/Prefabs (kart + driver)
        public Color Colour { get; private set; }        // map dot, name tag and the frame behind the portrait
        public string Line { get; private set; }

        public DriverInfo(string name, string model, string kartVisual, Color colour, string line)
        {
            Name = name;
            Model = model;
            PortraitFile = model.ToLowerInvariant();
            KartVisual = kartVisual;
            Colour = colour;
            Line = line;
        }
    }
}
