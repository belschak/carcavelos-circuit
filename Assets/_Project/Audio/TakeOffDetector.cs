namespace CarcavelosCircuit.Audio
{
    // Spots a real take-off (audit 11): the kart left the ground and stayed in the air for minAirSeconds.
    // A tiny bump on the road is shorter and stays silent. One whoosh per jump. Plain C#, unit tested.
    // Works with IsGrounded alone, so Role 1's ramp jump (D4) needs no new contract when it arrives.
    public class TakeOffDetector
    {
        private readonly float minAirSeconds;
        private float airSeconds;
        private bool armed;   // true once the kart stood on the ground since the last take-off or teleport
        private bool fired;   // the whoosh of this jump already played

        public TakeOffDetector(float minAirSeconds)
        {
            this.minAirSeconds = minAirSeconds;
        }

        // Call once per frame. Returns true on the one frame the air time reaches minAirSeconds.
        // canDrive = false (countdown, results): the kart settling onto the grid is no jump.
        public bool Update(bool grounded, bool canDrive, float deltaTime)
        {
            if (grounded)
            {
                airSeconds = 0f;
                fired = false;
                armed = true;
                return false;
            }
            airSeconds += deltaTime;
            if (!armed || fired || !canDrive || airSeconds < minAirSeconds)
            {
                return false;
            }
            fired = true;
            return true;
        }

        // After a teleport the kart may drop a little onto the respawn point: that is no take-off.
        // Unarmed until it has touched the ground once.
        public void Reset()
        {
            airSeconds = 0f;
            armed = false;
            fired = false;
        }
    }
}
