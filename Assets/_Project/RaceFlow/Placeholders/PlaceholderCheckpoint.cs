using CarcavelosCircuit.Track;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // OLD NAME for Role 2's Checkpoint (T4), kept only for the switch-over week (see PlaceholderTrack).
    // Empty subclass: the saved index and respawnPoint of existing scenes load into Checkpoint's fields.
    // Delete this file together with PlaceholderTrack.
    [AddComponentMenu("")] // hidden from Add Component: new tracks use Checkpoint
    public class PlaceholderCheckpoint : Checkpoint
    {
    }
}
