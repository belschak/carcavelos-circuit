namespace CarcavelosCircuit.Ambience
{
    // The questions a surfer asks the sea. WaveField answers them; tests answer them with a fake sea.
    public interface ISurfWaves
    {
        // Metres of water above the calm sea at this spot (along the beach, distance from the waterline).
        float SurfaceHeight(float along, float distance);

        // The next wave rolling towards this spot from the sea: its crest is at most 'window' metres further out,
        // it has not broken here yet and it will be at least 'minHeight' metres high when it breaks. False = none.
        bool FindIncoming(float along, float distance, float window, float minHeight, out WaveSample wave);

        // The same wave again, seen from a new spot. False = that wave is gone (it reached the sand).
        bool TryGetWave(int id, float along, out WaveSample wave);
    }
}
