using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Shared
{
    // Round 2 contract (docs/interfaces.md, decision 2026-11-11): how far along the lap a point is.
    // Role 2's TrackProgress implements it on the track's TrackInfo root (task T8). Positions, the minimap and the
    // bots read it. Until it exists, RaceFlow builds the same thing from the checkpoints (a rougher line).
    // Distances are metres along the racing line, 0 = the start/finish line, growing in driving direction.
    public interface ITrackProgress
    {
        float LapLength { get; }                        // metres for one lap along the racing line
        float DistanceAlongLap(Vector3 worldPosition);  // 0 .. LapLength for the closest point on the racing line
        Vector3 PointAt(float distance);                // world point on the racing line; any distance, wraps around the lap
        Vector3 DirectionAt(float distance);            // unit driving direction there (flat, y = 0)
        IReadOnlyList<Vector3> RacingLine { get; }      // the closed line's points in driving order, first = start line
    }
}
