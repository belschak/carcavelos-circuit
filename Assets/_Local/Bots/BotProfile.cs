using System;
using UnityEngine;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). One CPU opponent: its name, colour and how well it drives. Plain data, set in the Inspector.
    [Serializable]
    public class BotProfile
    {
        public string name = "Bot";
        public Color colour = Color.red;

        [Tooltip("Share of the kart's top speed this bot uses on straights (0.9 = 90 %)")]
        public float topSpeed = 0.95f;

        [Tooltip("How fast it dares to take corners (1 = the limit, 0.85 = careful)")]
        public float cornering = 0.9f;

        [Tooltip("How far it wanders left and right of its lane, in metres")]
        public float lineNoise = 0.4f;

        [Tooltip("Seconds between two decisions about lanes and other karts (bigger = sleepier)")]
        public float reaction = 0.2f;

        [Tooltip("Favourite lane: -1 = left edge, 0 = middle, +1 = right edge")]
        public float lane;

        [Tooltip("Wants to pass when its own free speed (without the kart ahead) is this much above that kart's speed, in m/s (aggressive 0.3, consistent 0.5, careful 0.7)")]
        public float passClosingSpeed = 1.5f;

        [Tooltip("Extra seconds of clear road it wants before starting a pass (careful 0.4)")]
        public float extraClearSeconds;

        [Tooltip("Chance of a small mistake at a corner entry (0.05 = 5 %); at most one per lap and one every 15 s")]
        public float mistakeChance = 0.07f;

        [Tooltip("Holds the drift key in long corners to earn a boost")]
        public bool drifts;
    }
}
