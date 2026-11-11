using UnityEngine;

namespace CarcavelosCircuit.Shared
{
    // Round 2 contract (docs/interfaces.md, decision 2026-11-11): anyone who races, the player's kart and every bot.
    // Positions, the minimap and the name tags only read this, so they work the same for the player and for bots.
    // A racer adds itself with RacerRegistry.Register when it spawns and removes itself with Unregister.
    public interface IRacer
    {
        string DisplayName { get; } // shown on the name tag, the positions list and the minimap legend
        Transform Root { get; }     // the object that moves (the kart root); position and forward are read from it
        IKartState Kart { get; }    // the kart this racer drives (speed, drift, boost), read only
        bool IsPlayer { get; }      // true only for the human player's kart
        Color MapColor { get; }     // dot colour on the minimap and colour of the name tag
    }
}
