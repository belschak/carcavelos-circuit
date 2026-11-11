# Role 2 walkthrough (Noah): track + race rules

Short notes per feature: what it does, why it is built this way, and its limits. For the final walkthrough.

## T8: TrackProgress, "how far along the lap"

**What.** `Track/TrackProgress.cs` sits on the same object as `TrackInfo` and implements the round 2 contract
`Shared/ITrackProgress`. Positions ("2nd / 6"), the minimap and the bots ask it two questions:
"how many metres along the lap is this kart?" (`DistanceAlongLap`) and "where is the road N metres further on?"
(`PointAt`, `DirectionAt`). RaceFlow finds it with `trackInfo.GetComponent<ITrackProgress>()`.

**How.**
1. The track builder hands over the points along the middle of the road once (`SetRoadPoints`). The scene save keeps them.
2. `Track/RacingLineBuilder.cs` (plain C#, no MonoBehaviour) turns them into the racing line:
   drops double points, starts at the point closest to checkpoint 0 (the start/finish line), turns the line round
   if it runs against checkpoint 0's forward direction, and keeps about one point every 6 m on straights but every
   point where the road bends more than 4 degrees.
3. That line goes into Role 4's `Shared/TrackPolyline`, which already does the maths (closest piece, wrap-around).
   `TrackProgress` only forwards the five members to it.

**Why.** The campus world's route data has a point every 2 m (672 points, 1,344 m). Thinning keeps the minimap and the
per-frame `DistanceAlongLap` cheap without cutting corners. Taking the start and the direction from checkpoint 0 means
the builder's point order does not matter, so the line can never start in the wrong place or run backwards.
The line is built on first use (not in `Awake`), so it does not matter which script asks first.

**Limits.** Distances are flat (x/z): a bridge over another road would confuse the closest-point search (our route has
none). No road points = `LapLength` 0, and RaceFlow quietly falls back to its rougher line through the checkpoints.
The race scene `Track_CampusWorld` belongs to Luc, so his `CampusWorldBuilder` adds the component (request Q20).

**Tests.** `Tests/EditMode/RacingLineBuilderTests.cs`: start point first, order kept, reversed line turned round,
no direction = order kept, duplicates removed, corners kept while thinning, no gap above spacing + 1 point,
null / too few points = empty line, and the component itself measures from the start line.

**See it.** Select the `Race` object in the scene: a yellow line over the road, a green ball at the start line.
