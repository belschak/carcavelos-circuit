using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Shared
{
    // Round 2 (docs/interfaces.md): the one list of everyone in the race. Bots (Role 3) and the player's kart
    // (Role 4) register here; RaceFlow's positions and minimap read it. Static, so nobody needs a scene reference
    // (no GameObject.Find). The list is cleared when Play Mode starts, so it never keeps racers from an old run.
    public static class RacerRegistry
    {
        private static readonly List<IRacer> racers = new List<IRacer>();

        // Fired after a racer was added or removed (the minimap adds or removes a dot).
        public static event Action Changed;

        // Read only view of everyone racing, in the order they registered.
        public static IReadOnlyList<IRacer> All { get { return racers; } }

        public static int Count { get { return racers.Count; } }

        // Adds a racer. Returns false (and adds nothing) for null or for a racer that is already in the list,
        // so registering twice by mistake can never show the same kart twice on the map.
        public static bool Register(IRacer racer)
        {
            if (racer == null || racers.Contains(racer))
            {
                return false;
            }
            racers.Add(racer);
            if (Changed != null)
            {
                Changed();
            }
            return true;
        }

        // Removes a racer (call it in OnDisable or OnDestroy). Returns false if it was not in the list.
        public static bool Unregister(IRacer racer)
        {
            if (racer == null || !racers.Remove(racer))
            {
                return false;
            }
            if (Changed != null)
            {
                Changed();
            }
            return true;
        }

        // The human player's racer, or null if none is registered yet.
        public static IRacer FindPlayer()
        {
            for (int i = 0; i < racers.Count; i++)
            {
                if (racers[i].IsPlayer)
                {
                    return racers[i];
                }
            }
            return null;
        }

        // Empties the list and drops all listeners. Tests call it; Unity calls it before every Play Mode start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            racers.Clear();
            Changed = null;
        }
    }
}
