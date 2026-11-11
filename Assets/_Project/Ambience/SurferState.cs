namespace CarcavelosCircuit.Ambience
{
    // What a surfer is doing. Allowed changes are in SurferBrain.IsAllowed.
    public enum SurferState
    {
        Hidden,    // gone after a fall, waits to appear again out at sea
        Paddling,  // lying on the board, paddling to the waiting spot behind the breaking waves
        Waiting,   // sitting on the board, looking out to sea for the next set
        Catching,  // turned to the beach, paddling hard to catch the wave behind them
        Riding,    // standing up, riding along the wave face with carving turns
        Falling,   // fell off: tumbling into the foam
        Finished   // ride over: kicked out over the back of the wave, sits down for a moment
    }
}
