namespace CarcavelosCircuit.RaceFlow
{
    // What happened in a menu, so RaceSounds can answer with the right sound (audit 10).
    // HudMenus only says WHAT happened; which clip, pitch and volume that gets is decided in RaceSounds.OnMenuSound.
    public enum MenuSound
    {
        Open,       // settings or credits card opens
        Close,      // a card closes (key, CLOSE button or a click next to it)
        PageTurn,   // the credits go to the other page
        SliderStep, // a volume bar moved one step: the click plays at the NEW effects level, as a preview
        Toggle,     // an on/off row flipped (sound, reduced motion)
        Mute        // M or the SOUND button (muting silences everything at once, so only "sound on" is heard)
    }
}
