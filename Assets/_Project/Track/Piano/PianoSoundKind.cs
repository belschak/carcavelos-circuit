namespace CarcavelosCircuit.Track
{
    // What the atrium piano does when a kart is near it (task T10).
    public enum PianoSoundKind
    {
        None,
        PassBy,  // a kart drives past: a short rising chord
        Hit      // a kart drives into the piano: a loud low crash of keys
    }
}
