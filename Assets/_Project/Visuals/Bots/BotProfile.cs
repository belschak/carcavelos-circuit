using System;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Task V10: one bot driver, its made-up cartoon name, colour and driving style. Plain data in BotSettings.
    [Serializable]
    public class BotProfile
    {
        [Tooltip("Name tag and positions list (made-up cartoon names only, max 16 letters)")]
        public string displayName = "Bot";

        [Tooltip("Kart paint, minimap dot and name tag colour")]
        public Color colour = Color.red;

        [Range(0.7f, 1f), Tooltip("Share of the kart's top speed on straights (1 = as fast as the player's kart can go)")]
        public float pace = 0.95f;

        [Range(0.5f, 1f), Tooltip("How close to the kart's turning limit it takes corners (1 = the limit, 0.85 = careful)")]
        public float cornering = 0.9f;

        [Range(-2f, 2f), Tooltip("Favourite lane in metres right (+) or left (-) of the racing line, so the bots do not drive in a queue")]
        public float lane;

        [Tooltip("Drifts through long bends (for the boost)")]
        public bool drifts;

        public BotProfile()
        {
        }

        public BotProfile(string displayName, Color colour, float pace, float cornering, float lane, bool drifts)
        {
            this.displayName = displayName;
            this.colour = colour;
            this.pace = pace;
            this.cornering = cornering;
            this.lane = lane;
            this.drifts = drifts;
        }
    }
}
