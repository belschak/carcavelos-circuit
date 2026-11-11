# Walkthrough: the round 2 contracts (bots, positions, minimap, name tags)

For the repository walkthrough: any of us can be asked about these. Code: `Assets/_Project/Shared/`. Tests: `Tests/EditMode/RacerRegistryTests.cs`, `TrackPolylineTests.cs`. Decision: `docs/decisions.md` 2026-11-11.

## What problem they solve
Round 2 adds four features that need the same two answers:
1. **Who is in the race?** Positions, the minimap and the name tags must list the player and every bot.
2. **How far along the lap is a kart?** Positions sort by it, the minimap draws it, bots steer towards a point a bit further along.

And one more: **how does a bot press the pedals?** It should drive the same kart as the player, so a bot is never faster "by code".

## The pieces (all new files, nothing older changed)
| File | Answers | Who implements / uses it |
|---|---|---|
| `IRacer` | name, root transform, kart state, is it the player, map colour | player kart (Role 4), bots (Role 3); read by positions, minimap (Role 4), name tags (Role 1) |
| `RacerRegistry` | the one static list of racers: `Register`, `Unregister`, `All`, `Changed`, `FindPlayer` | everyone; cleared automatically before every Play Mode start |
| `ITrackProgress` | lap length, `DistanceAlongLap(point)`, `PointAt(distance)`, `DirectionAt(distance)`, `RacingLine` | Role 2's `TrackProgress` (T8); RaceFlow falls back to the checkpoints until it exists |
| `TrackPolyline` | the maths behind `ITrackProgress` for a closed line of points | used by the fallback and (optionally) by `TrackProgress` |
| `KartInput`, `IKartInputSource`, `IKartDrivable` | throttle, steer, drift from code instead of the keyboard | bots provide input (Role 3), `KartController` reads it (Role 1, D8) |

## How the maths works (`TrackPolyline`)
- The racing line is a list of points in driving order; the first point is the start/finish line. The loop closes itself (last point back to the first).
- Every point stores its distance from the start ("cumulative length"). `PointAt(d)` finds the piece that contains `d` and interpolates.
- `DistanceAlongLap(p)` checks every piece, projects `p` onto it (dot product, clamped to the piece) and keeps the closest. A few hundred pieces per call is cheap enough every frame.
- Everything is flat (x and z only), so a kart in the air over a ramp still has a position and a bridge does not count twice.
- `Wrap` keeps any distance inside one lap: -10 m = 10 m before the line, NaN = 0. Invalid input (fewer than 3 points) gives safe defaults instead of exceptions.

## Why it is built this way
- **Interfaces + one registry instead of scene references:** no `GameObject.Find`, no module reaches into another one's internals (our `AGENTS.md` rule), and the HUD works the same with 0 or 10 bots.
- **New optional interfaces instead of changing `IKartState`:** changing an existing contract would break every kart until all owners update it; a second interface (like `IKartBoost` for items) can be added by each owner when ready.
- **Bots drive through `IKartDrivable`:** one physics model, one `KartTuning` for player and bots. Fair by construction; `KartInput.Clamped()` stops a bot's maths bug (NaN, 5.0) from becoming super speed.
- **RaceFlow owns the race state** (who may drive, grid slots, retry): bots only register and drive, so the start, the finish and retry stay in one place.

## Limits and what we would change
- The checkpoint fallback line is rough (straight lines between 12 checkpoints), so positions can flicker in a tight corner until `TrackProgress` is merged.
- Closest-piece search can jump to a nearby parallel road section if two parts of the track run close together; a fix would be to search only near the last known distance.
- The registry is static (global): simple and testable, but a second race running at the same time (split screen) would need one registry per race.
- With more time: shortcuts detection (distance jumps forward too fast), a smoothed racing line (spline) for nicer bot lines.
