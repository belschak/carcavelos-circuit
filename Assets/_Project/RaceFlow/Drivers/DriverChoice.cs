using System;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Driver select (Luc, 17 Nov 2026), kart builder since 18 Nov 2026: which driver, kart and tyres the player picked on
    // the start screen (KartLoadout). Saved like the race mode, so the next visit starts with the same set already picked.
    // Static, because several places read it without knowing each other: the start screen (UI/KartBuilder), the
    // player's IRacer (PlayerRacer: map dot and name tag colour), Role 3's BotSquad (the player's kart model, which bot
    // sits out) and the online race (the loadout code everyone builds my kart from).
    // Kart builder rule (like the 3DS original): picking another driver keeps the kart and tyres already picked. The
    // first visit starts with the Rookie's own set; a save from before the builder (only a driver) starts with that
    // driver's own kart and tyres, so returning players see what they drove before.
    public static class DriverChoice
    {
        private const string SaveKey = "CarcavelosCircuit.Driver";
        private const string KartKey = "CarcavelosCircuit.Kart";
        private const string TyresKey = "CarcavelosCircuit.Tyres";
        private const int NotLoaded = -2;

        private static int index = NotLoaded;
        private static int kart = NotLoaded;
        private static int tyres = NotLoaded;

        // A new driver was picked (the new index). BotSquad swaps the bots (the driver picked sits out).
        public static event Action<int> Changed;

        // Driver, kart or tyres changed (the new set). BotSquad rebuilds the player's kart model.
        public static event Action<KartLoadout> LoadoutChanged;

        // The picked driver, the Rookie until the player picks someone else.
        public static int Index
        {
            get
            {
                Load();
                return index;
            }
        }

        public static DriverInfo Current { get { return DriverRoster.Get(Index); } }

        // The whole set: driver, kart and tyres.
        public static KartLoadout Loadout
        {
            get
            {
                Load();
                return new KartLoadout(index, kart, tyres);
            }
        }

        // The player has picked a driver before (in this visit or a saved one). Until then the builder shows the
        // Rookie's set without a pick, so the first READY is a deliberate choice.
        public static bool HasPicked { get { return picked || PlayerPrefs.HasKey(SaveKey); } }

        private static bool picked;

        // Reads the saved set once. Every saved number is checked: a bad one falls back to the driver's own part.
        private static void Load()
        {
            if (index != NotLoaded)
            {
                return;
            }
            int savedDriver = PlayerPrefs.GetInt(SaveKey, DriverRoster.Rookie);
            index = DriverRoster.IsValid(savedDriver) ? savedDriver : DriverRoster.Rookie;
            int savedKart = PlayerPrefs.GetInt(KartKey, index);
            int savedTyres = PlayerPrefs.GetInt(TyresKey, index);
            kart = KartLoadout.IsPart(savedKart) ? savedKart : index;
            tyres = KartLoadout.IsPart(savedTyres) ? savedTyres : index;
        }

        // Picks a driver and keeps the kart and tyres. save = false for tests, so a test run never changes the
        // player's saved pick. Returns false (and changes nothing) for an index outside the roster.
        public static bool Select(int driver, bool save = true)
        {
            if (!DriverRoster.IsValid(driver))
            {
                return false;
            }
            Load();
            return Apply(new KartLoadout(driver, kart, tyres), save);
        }

        public static bool SelectKart(int newKart, bool save = true)
        {
            if (!KartLoadout.IsPart(newKart))
            {
                return false;
            }
            Load();
            return Apply(new KartLoadout(index, newKart, tyres), save);
        }

        public static bool SelectTyres(int newTyres, bool save = true)
        {
            if (!KartLoadout.IsPart(newTyres))
            {
                return false;
            }
            Load();
            return Apply(new KartLoadout(index, kart, newTyres), save);
        }

        // Picks a whole set at once (tests, the ?driver= test flag). False for an invalid set.
        public static bool SelectLoadout(KartLoadout loadout, bool save = true)
        {
            if (!loadout.IsValid)
            {
                return false;
            }
            Load();
            return Apply(loadout, save);
        }

        private static bool Apply(KartLoadout next, bool save)
        {
            bool driverChanged = next.Driver != index;
            bool anyChange = driverChanged || next.Kart != kart || next.Tyres != tyres;
            index = next.Driver;
            kart = next.Kart;
            tyres = next.Tyres;
            picked = true;
            if (save)
            {
                PlayerPrefs.SetInt(SaveKey, index);
                PlayerPrefs.SetInt(KartKey, kart);
                PlayerPrefs.SetInt(TyresKey, tyres);
                PlayerPrefs.Save();
            }
            if (driverChanged && Changed != null)
            {
                Changed(index);
            }
            if (anyChange && LoadoutChanged != null)
            {
                LoadoutChanged(next);
            }
            return true;
        }

        // Tests: forget what was loaded, so the next read takes the saved numbers again.
        public static void ReloadForTests()
        {
            index = NotLoaded;
            kart = NotLoaded;
            tyres = NotLoaded;
            picked = false;
        }
    }
}
