using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Marks one spot on the track for a wave box (V19, promenade shortcut; help PR for Role 4 by Luc's Claude).
    // Only the position counts. The track scene's builder puts these down (CampusWorldBuilder, from
    // Visuals/CampusWorld/Data/shortcuts.json); ItemSystem puts one box on each when the race is set up, and that box
    // always gives the Carcavelos Wave (ItemOdds.PickForBox). A scene without spots simply has no wave boxes.
    public class WaveBoxSpot : MonoBehaviour
    {
    }
}
