using CarcavelosCircuit.Track;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // OLD NAME for Role 2's TrackInfo (T4), kept only for the switch-over week.
    // Since the swap (Q5) RaceFlow reads TrackInfo only. Two track builders still add this name:
    // Visuals/Editor/CampusWorldBuilder.cs (Luc) and Track/Editor/NovaCampusBuilder.cs (help scene #31).
    // Because this class is an empty subclass, Unity loads the saved fields of those scenes (checkpoints,
    // spawnPoint) straight into TrackInfo's fields with the same names, so their scenes race as they are.
    // Delete this file once both builders say AddComponent<TrackInfo>() (docs/explain/role4-walkthrough.md, section 5).
    [AddComponentMenu("")] // hidden from Add Component: new tracks use TrackInfo
    public class PlaceholderTrack : TrackInfo
    {
    }
}
