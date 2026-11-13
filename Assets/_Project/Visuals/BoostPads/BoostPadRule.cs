using System.Collections.Generic;

namespace CarcavelosCircuit.Visuals
{
    // When a boost pad fires (task V11). Plain C#, Edit Mode tested. A pad boosts every kart that drives over it,
    // the player and every bot alike, but each kart only once per pass: a kart that bounces on the pad (landing,
    // hitting the wall next to it) would otherwise get the boost several times in a row.
    public class BoostPadRule
    {
        private readonly float cooldownSeconds;
        private readonly Dictionary<int, float> lastBoost = new Dictionary<int, float>();

        public BoostPadRule(float cooldownSeconds)
        {
            this.cooldownSeconds = cooldownSeconds;
        }

        // True if this kart gets a boost now (and remembers the time), false while its cooldown still runs.
        public bool TryBoost(int kartId, float now)
        {
            float last;
            if (lastBoost.TryGetValue(kartId, out last) && now - last < cooldownSeconds)
            {
                return false;
            }
            lastBoost[kartId] = now;
            return true;
        }
    }
}
