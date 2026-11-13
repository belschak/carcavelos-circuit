# Role 3 walkthrough (Luc): kart, visuals, bots, web release

## Bots (task V10)

**What.** Five rule-based opponents race the player on the campus: three locals (Gaivota, Bica, Sardinha), The Dean and Prof. Pastel. All names and looks are made up. Each bot drives a normal `Driving/Kart.prefab`, the same kart and the same `KartTuning` as the player, painted in its own colour. They start in pairs in front of the player (the player is last), drive full laps, slow down for corners, sometimes drift, pass slower karts and can be rammed. No machine learning: every decision is a short rule.

**How it is built.** Files in `Assets/_Project/Visuals/Bots/`:

| File | Job |
|---|---|
| `BotRules.cs` | The decisions as plain maths (Edit Mode tested): steering, corner speed, braking point, pedals, rubber band, passing lane, drift yes/no |
| `BotStuckCheck.cs` | Stuck rule (plain C#): standing still 2 s = back up 1.2 s, after 3 failed tries = put back on the line |
| `BotTrackMap.cs` | Role 2's racing line (`ITrackProgress`) cut into points every 2 m, with how sharp the road bends at each point, worked out once per race |
| `BotDriver.cs` | One bot. Sits on the kart, is its `IKartInputSource` (the kart reads it instead of the keyboard, Role 1's D8) and an `IRacer` (name tag, minimap, positions) |
| `BotSquad.cs` | Spawns the five bots when RaceFlow's `RaceRoster` has set the race up, and feeds each bot its gap to the player |
| `BotSettings.cs` + `.asset` | Names, colours and every tuning number (Inspector) |
| `BotProfile.cs`, `BotKartPaint.cs` | One bot's data; the body paint per kart (MaterialPropertyBlock, the material file stays as it is) |

The rules, in the order a bot asks them every frame:
1. **Where am I?** The nearest racing line point, searched only a few metres around last frame's point, so a bot never "jumps" to the other side of the U-turn wall at the start plaza.
2. **Where do I aim?** A point on the racing line `4 m + 0.3 s x speed` ahead, moved sideways to the bot's own lane (so they do not drive in a queue). After GO it starts from where it stands and moves over slowly. A kart close ahead in my lane: aim beside it. A kart right beside me: stay on my side of it (on the grid the player stands next to a bot).
3. **How do I steer?** Pure pursuit: drive the circle arc through the aim point. The arc needs `speed x curvature` of turning, the kart turns `steer x 110 deg/s`, so `steer = curvature x speed / turn rate`.
4. **How fast may I go?** A bend with radius R can be driven at `turn rate x R`. A bend d metres ahead allows `sqrt(v_corner^2 + 2 x braking x d)` now (braking distance). The lowest limit over the next 40 m wins. Gas below it, roll a little above it, brake clearly above it.
5. **Rubber band.** Far behind the player: up to 6 % more pace, far ahead: up to 10 % less, but never above the kart's top speed.
6. **Drift** through long, even, flat bends (50 to 130 degrees within 24 m, no part tighter than a 14 m radius, no ramp), only for the bots whose profile says so; let go when the bend opens, for the drift boost. The first version also drifted into the 90 degree corner in the 6 m wide atrium and into the bend before the stairs, and got stuck there; the radius and ramp checks came from that test run.
7. **Stuck** (2 s standing): back up and steer out; fell off or 3 failed tries: back on the racing line a few metres ahead.

**Who does what.** RaceFlow (Role 4) owns the race for every racer: grid slots, input on at GO and off at the finish or on pause, Retry. The bots only register and drive (`docs/interfaces.md`, Round 2). `BotSquad` sits on the "Racers" object in `Main.unity`; Role 4's scene builder puts it there (`Role4RacersBuilder`).

**Why this way.** One kart physics for everyone, so a bot can never cheat by code and the player can ram it. Rules instead of learning, so every decision can be explained in one sentence and tested. All maths in plain C# classes with Edit Mode tests (`Tests/EditMode/BotRulesTests.cs`), and one Play Mode test (`Tests/PlayMode/BotRacePlayTests.cs`) that lets the five bots race a full campus lap.

**Limits.** Bots do not use items (item boxes count only for the player; open task O2). They know only the racing line, not the road edges, so their lanes stay within 2.2 m of it. They plan with a copy of the kart's turn rate (`BotSettings`), so a change to `KartTuning` needs the same number there. A bot that cannot get out after three tries is put back on the line (the only time a bot calls `ResetTo`, like the player's Recover key).

**How it could change.** Difficulty levels = one factor on every profile's pace. Item use = a bot "presses E" when it holds an item and a kart is ahead. Different characters = a model per profile instead of the shared rookie driver (task O3).
