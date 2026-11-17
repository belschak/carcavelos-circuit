using System;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Driver select (Luc, 17 Nov 2026): which driver the player picked on the start screen (DriverRoster index).
    // Saved like the race mode, so the next visit starts with the same driver already picked.
    // Static, because three places read it without knowing each other: the start screen (UI/DriverPicker), the
    // player's IRacer (PlayerRacer: map dot and name tag colour) and Role 3's BotSquad (kart paint, which bot sits out).
    public static class DriverChoice
    {
        private const string SaveKey = "CarcavelosCircuit.Driver";
        private const int NotLoaded = -2;

        private static int index = NotLoaded;

        // A new driver was picked (the new index). BotSquad repaints the player's kart and swaps the bot.
        public static event Action<int> Changed;

        // The picked driver, the Rookie until the player picks someone else.
        public static int Index
        {
            get
            {
                if (index == NotLoaded)
                {
                    index = DriverRoster.IsValid(PlayerPrefs.GetInt(SaveKey, DriverRoster.Rookie))
                        ? PlayerPrefs.GetInt(SaveKey, DriverRoster.Rookie)
                        : DriverRoster.Rookie;
                }
                return index;
            }
        }

        public static DriverInfo Current { get { return DriverRoster.Get(Index); } }

        // The player has picked a driver before (in this visit or a saved one). Until then the driver select shows
        // no pick and no READY button, so the first visit asks for a click on a driver.
        public static bool HasPicked { get { return picked || PlayerPrefs.HasKey(SaveKey); } }

        private static bool picked;

        // Picks a driver. save = false for tests, so a test run never changes the player's saved pick.
        // Returns false (and changes nothing) for an index outside the roster.
        public static bool Select(int driver, bool save = true)
        {
            if (!DriverRoster.IsValid(driver))
            {
                return false;
            }
            bool changed = driver != Index;
            index = driver;
            picked = true;
            if (save)
            {
                PlayerPrefs.SetInt(SaveKey, driver);
                PlayerPrefs.Save();
            }
            if (changed && Changed != null)
            {
                Changed(driver);
            }
            return true;
        }
    }
}
