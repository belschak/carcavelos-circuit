using System.Globalization;

namespace CarcavelosCircuit.RaceFlow
{
    // The player's settings (task P7): three volumes, mute and reduced motion. Plain C#, so it is unit tested.
    // Volumes are 0..1 in steps of 10 %. Saved as one text in PlayerPrefs (SettingsStore does that),
    // like PersonalBest: "1|0.8|1|0|0" = master | music | effects | muted | reduced motion.
    public class GameSettings
    {
        public const float VolumeStep = 0.1f;
        public const int MaxLevel = 10; // 10 steps of 10 % = 100 %

        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float EffectsVolume { get; private set; } = 1f;
        public bool Muted { get; private set; }
        public bool ReducedMotion { get; private set; }

        // What the AudioListener gets: 0 while muted. Muting keeps MasterVolume, so unmuting brings it back.
        public float ListenerVolume
        {
            get { return Muted ? 0f : MasterVolume; }
        }

        public void ToggleMute()
        {
            Muted = !Muted;
        }

        // The volume of one row (0 for the two on/off rows).
        public float VolumeOf(SettingsItem item)
        {
            switch (item)
            {
                case SettingsItem.Master: return MasterVolume;
                case SettingsItem.Music: return MusicVolume;
                case SettingsItem.Effects: return EffectsVolume;
                default: return 0f;
            }
        }

        // Is an on/off row on? "Sound" is on when not muted.
        public bool IsOn(SettingsItem item)
        {
            if (item == SettingsItem.Sound) return !Muted;
            if (item == SettingsItem.ReducedMotion) return ReducedMotion;
            return false;
        }

        public static bool IsVolume(SettingsItem item)
        {
            return item == SettingsItem.Master || item == SettingsItem.Music || item == SettingsItem.Effects;
        }

        // Left/right on a row: direction -1 = quieter or off, +1 = louder or on.
        // Returns true if something changed, so the caller only saves and redraws then.
        public bool Change(SettingsItem item, int direction)
        {
            if (IsVolume(item))
            {
                return SetVolumeLevel(item, Level(VolumeOf(item)) + direction);
            }
            return SetOn(item, direction > 0);
        }

        // A click on an on/off row flips it.
        public bool Toggle(SettingsItem item)
        {
            return SetOn(item, !IsOn(item));
        }

        // Sets a volume to "level" tenths (clamped to 0..10). Turning the master volume up also unmutes:
        // it means "I want to hear this".
        public bool SetVolumeLevel(SettingsItem item, int level)
        {
            if (level < 0) level = 0;
            if (level > MaxLevel) level = MaxLevel;
            float volume = level * VolumeStep;
            float before = VolumeOf(item);
            bool wasMuted = Muted;

            if (item == SettingsItem.Master)
            {
                if (volume > before) Muted = false;
                MasterVolume = volume;
            }
            else if (item == SettingsItem.Music)
            {
                MusicVolume = volume;
            }
            else if (item == SettingsItem.Effects)
            {
                EffectsVolume = volume;
            }
            else
            {
                return false;
            }
            return volume != before || Muted != wasMuted;
        }

        private bool SetOn(SettingsItem item, bool on)
        {
            if (item == SettingsItem.Sound)
            {
                bool muted = !on;
                if (muted == Muted) return false;
                Muted = muted;
                return true;
            }
            if (item == SettingsItem.ReducedMotion)
            {
                if (on == ReducedMotion) return false;
                ReducedMotion = on;
                return true;
            }
            return false;
        }

        // Volume as whole 10 % steps (0..10), for the segment bar and the "80 %" text. A loaded 0.83 counts as 8.
        public static int Level(float volume)
        {
            return (int)System.Math.Round(Clamp01(volume) / VolumeStep);
        }

        public static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f) return 0f; // NaN fails every comparison, so it is caught first
            return value > 1f ? 1f : value;
        }

        // ---- Saving (InvariantCulture: a Portuguese laptop would write 0,8 and break loading) ----

        public string ToSaveString()
        {
            return Number(MasterVolume) + "|" + Number(MusicVolume) + "|" + Number(EffectsVolume) + "|"
                + (Muted ? "1" : "0") + "|" + (ReducedMotion ? "1" : "0");
        }

        // A broken or missing part keeps its default, so an old or damaged save never breaks the game.
        public static GameSettings FromSaveString(string text)
        {
            GameSettings settings = new GameSettings();
            if (string.IsNullOrEmpty(text))
            {
                return settings;
            }
            string[] parts = text.Split('|');
            settings.MasterVolume = ReadVolume(parts, 0, settings.MasterVolume);
            settings.MusicVolume = ReadVolume(parts, 1, settings.MusicVolume);
            settings.EffectsVolume = ReadVolume(parts, 2, settings.EffectsVolume);
            settings.Muted = ReadFlag(parts, 3, settings.Muted);
            settings.ReducedMotion = ReadFlag(parts, 4, settings.ReducedMotion);
            return settings;
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static float ReadVolume(string[] parts, int index, float fallback)
        {
            float value;
            if (index < parts.Length
                && float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value) && !float.IsInfinity(value))
            {
                return Clamp01(value);
            }
            return fallback;
        }

        private static bool ReadFlag(string[] parts, int index, bool fallback)
        {
            if (index < parts.Length && (parts[index] == "0" || parts[index] == "1"))
            {
                return parts[index] == "1";
            }
            return fallback;
        }
    }
}
