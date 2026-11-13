using System.Collections;
using CarcavelosCircuit.Visuals;
using UnityEngine;

namespace CarcavelosCircuit.Tests.PlayMode
{
    // Since V10 Main.unity races with Role 3's five bots (BotSquad on the Racers object). Tests that check one rule
    // of the race alone (RaceFlow with its dev test bots, the items) assume an empty road, so they take the bots
    // out of the race first. RaceRoster then lays out the grid again for the player alone (slot 0).
    public static class BotsOffForTests
    {
        public static IEnumerator RemoveFromRace(float timeoutSeconds)
        {
            BotSquad squad = Object.FindAnyObjectByType<BotSquad>();
            float waited = 0f;
            while (squad != null && !squad.HasSpawned && waited < timeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (squad != null)
            {
                squad.RemoveBots();
            }
            yield return null; // Destroy happens at the end of the frame
            yield return null; // and RaceRoster re-grids in the frame after
        }
    }
}
