using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Loads, saves and applies GameSettings (task P7). The rules live in GameSettings (unit tested);
    // this is only the glue to Unity: PlayerPrefs (in the browser: the site's IndexedDB storage) and the audio.
    public static class SettingsStore
    {
        private const string SaveKey = "CarcavelosCircuit.Settings"; // one key for all tracks: it is about the player

        public static GameSettings Load()
        {
            return GameSettings.FromSaveString(PlayerPrefs.GetString(SaveKey, ""));
        }

        public static void Save(GameSettings settings)
        {
            PlayerPrefs.SetString(SaveKey, settings.ToSaveString());
            PlayerPrefs.Save(); // in a Web build this writes to the browser right away
        }

        // ---- The last name typed on the leaderboard (task R10), so the save form is filled in next time ----
        private const string NameKey = "CarcavelosCircuit.PlayerName";

        // "" if none is saved, or if the saved one no longer passes the name rules (edited by hand, rules changed).
        public static string LoadPlayerName()
        {
            string name = PlayerPrefs.GetString(NameKey, "");
            return PlayerNameRules.IsValidStoredName(name) ? name : "";
        }

        public static void SavePlayerName(string name)
        {
            PlayerPrefs.SetString(NameKey, name);
            PlayerPrefs.Save();
        }

        // Master volume and mute: AudioListener.volume scales every sound in the game at once.
        // Music and effects: read by RaceMusic and RaceSounds through SoundLevels.
        public static void Apply(GameSettings settings)
        {
            AudioListener.volume = settings.ListenerVolume;
            SoundLevels.Music = settings.MusicVolume;
            SoundLevels.Effects = settings.EffectsVolume;
        }
    }
}
