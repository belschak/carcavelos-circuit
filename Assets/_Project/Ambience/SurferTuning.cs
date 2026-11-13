namespace CarcavelosCircuit.Ambience
{
    // The surfer numbers (from AmbienceSettings) as a plain object; tests set the fields directly.
    public class SurferTuning
    {
        public float PaddleSpeed = 1.4f;     // m/s
        public float CatchWindow = 8f;       // metres: start paddling when a crest is this close
        public float MinRideHeight = 0.9f;   // metres: smaller waves are let through
        public float RideSpeedMin = 5f;      // m/s along the wave
        public float RideSpeedMax = 7f;
        public float FallChance = 0.3f;      // 0..1 per ride
        public float FallSeconds = 1.3f;
        public float RespawnSeconds = 4f;
        public float KickOutDistance = 9f;   // metres from the waterline: the ride ends here
        public float CarveSeconds = 2.2f;    // one bottom turn + one top turn
        public float LineupDistance = 30f;   // metres out where surfers wait (just behind the break)
        public float PopUpSeconds = 0.45f;   // lying -> standing
        public float BeachHalfLength = 63f;  // metres from the centre to where the rideable part of the beach ends
    }
}
