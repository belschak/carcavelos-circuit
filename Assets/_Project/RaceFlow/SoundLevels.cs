namespace CarcavelosCircuit.RaceFlow
{
    // The player's music and effects volume (0..1) from the settings panel (task P7).
    // SettingsStore writes them; RaceMusic and RaceSounds multiply their own volumes by them.
    // Static, because those scripts sit on other objects in Main.unity and need no extra Inspector reference.
    // The master volume and mute need no hook: they go straight to AudioListener.volume.
    public static class SoundLevels
    {
        public static float Music = 1f;
        public static float Effects = 1f;
    }
}
