# Role 3 walkthrough: kart, visuals + web release (Luc Tippmann)

Role 3 makes everything the player sees: the Carcavelos campus at real scale, the karts and drivers, the boost pads, checkpoint banners, drift and item effects, and the five bot rivals that race in Race vs Bots. It also makes the Web build small enough to load quickly and publishes every `main` build on GitHub Pages.

## Files

| File | What it does |
|---|---|
| `tools/world_gen/` (Python + Blender, outside `Assets/`) | Generates the campus from OpenStreetMap, elevation data and photo surveys: `world_data.py`, `route_from_sketch.py`, `make_*_textures.py`, `build_world.py`, `make_shadowmap.py`, `make_grass_tufts.py`, `export_unity_bundle.py` |
| `Visuals/Editor/CampusWorldBuilder.cs` | Menu Carcavelos Circuit > Build Campus World (Role 3): materials, prefabs, `CampusWorld.prefab`, the race scene `Visuals/Scenes/Track_CampusWorld.unity`, and a self-test (`BuildAndCheck`) |
| `Visuals/Editor/NovaHallInsert.cs`, `FenceFreeZones.cs` | Put Role 2's main hall and white fence into the world; leave the fence out where the world has its own edge (`Data/fence_free.json`) |
| `Visuals/CampusWorld/CampusWorldLighting.cs`, `TimeOfDayLook.cs` | Copy sky, sun, haze and colour grade into the scene; switch Noon and Sunset at runtime |
| `Visuals/CampusWorld/GrassField.cs`, `GrassFieldData.cs`, `GrassTuftShape.cs` | About 13,500 grass tufts drawn as GPU instances of 9 prototype meshes |
| `Visuals/BoostPads/BoostPad.cs`, `BoostPadPlan.cs`, `BoostPadRule.cs` | Glowing chevron pads: where they go, and one boost per pass through `IKartBoost` |
| `Visuals/CheckpointGates/CheckpointGate.cs`, `CheckpointGatePlan.cs` | A waving "NOVA KART" banner on each side of every checkpoint (decoration only) |
| `Visuals/Kart/` (`KartVisualFromState`, `KartVisualSteering`, `KartDriverArms` + `TwoBoneIK`, `KartVisualSwap`) | Kart model: steering wheel and wheels follow `IKartState`, hands stay on the rim, the model can be swapped for another driver's kart |
| `Visuals/Kart/Editor/NovaKartVisualBuilder.cs`, `NovaKartRosterBuilder.cs`, `Editor/DriverPortraitBuilder.cs`, `tools/kart_lod/` | Build the four kart prefabs, their far versions (`LODGroup`) and the driver portraits |
| `Visuals/Kart/KartLodWarmup.cs` | Draws every near and far kart material once into a hidden 16 x 16 picture so the browser compiles the shaders early |
| `Visuals/KartEffects/KartEffects.cs`, `KartEffectsRules.cs`, `KartEffectAnchors.cs` | Tyre smoke, blue and orange drift sparks, boost flames, reading `IKartState` only |
| `Visuals/ItemFx/` (`ItemFx`, `WaveRide` + `WaveMesh`, `EspressoFx`, `SeagullFx`, `DeansFx`, `ItemFxRules`) and `Visuals/Items/` | The four item effects round the kart and the azulejo item box look |
| `Visuals/Bots/BotRules.cs`, `BotStuckCheck.cs`, `BotTrackMap.cs` | Bot decisions as plain maths: steering, corner speed, pedals, rubber band, lanes, drift, stuck |
| `Visuals/Bots/BotDriver.cs`, `BotSquad.cs`, `BotSettings.cs`, `DriverSwap.cs`, `BotKartPaint.cs`, `FinishCruise.cs` | One bot on a normal kart, spawning the five, their tuning, the lineup after driver select, paint, and the autopilot after the finish |
| `Visuals/Editor/WebMeshBudget.cs`, `WebTextureBudget.cs`, `WebLoadBudget.cs`, `PropRenderBudget.cs`; `.github/workflows/web-build.yml` (`pages` job) | Lighter meshes, textures and props for the Web build; publishing `main` on GitHub Pages |

## How it works

1. **Generate the world (offline).** `tools/world_gen/` turns OpenStreetMap, elevation data and the team's photos into Blender models at 1 unit = 1 m, with a fixed origin (lat 38.6780, lon -9.3265, `tools/world_gen/README.md`). Data, not hand edits: heights in `buildings.csv`, driving heights in `route_profile.json`. `build_world.py` writes `CampusWorld.fbx`, `TrackSurface.fbx` and `RouteGuide.fbx`; `make_shadowmap.py` bakes sun shadows onto the ground; `export_unity_bundle.py` copies models, textures and `Data/` (`track.json`: 697 route points, 1,394 m, checkpoints 0 to 13; `dressing.json`: 1,541 trees and props) into `Visuals/CampusWorld/`.
2. **Build it in Unity.** `CampusWorldBuilder.Build` sets importers, builds materials and prefabs, then `BuildScene`: mesh colliders on everything except parts named `NoCollide`, the dressing from `dressing.json`, `NovaHallInsert.Apply`, `FenceFreeZones.Apply`, tunnel guards, `AddGrassTufts`, `AddSunAndSky`, and `WebMeshBudget.Finish`. It saves `CampusWorld.prefab`, then adds the race parts from `track.json` (`AddRaceParts`): Role 2's `TrackInfo`, `TrackProgress`, one trigger per checkpoint with a respawn point about 4 m on, the `SpawnPoint`, `AddBoostPads` and `AddCheckpointGates`. `BuildAndCheck` runs `SelfTest` and exits 0 or 1.
3. **Race start.** `RaceBootstrap` (Role 4) loads `Track_CampusWorld`. `CampusWorldLighting.ApplyIfActiveScene` writes sky, ambient light and haze only while the track scene is active; the player's Noon or Sunset choice picks one `TimeOfDayLook` (sun, sky, reflections, grade), with no lightmaps. `GrassField.Draw` shows a cell (16 m near, 32 m far) only within 65 m or 150 m and in view, and sends each prototype with `Graphics.DrawMeshInstanced` (up to 1023 per call).
4. **On the road.** A `BoostPad` trigger asks `BoostPadRule` (0.5 s cooldown per kart), then calls `IKartBoost`: jump pads 25 m and 6 m before the terrace edge give x1.6 for 1.0 s, tunnel pads every 28 m give x1.35 for 0.8 s (`BoostPadPlan`). Gate poles stand at the trigger's real edges (`CheckpointGatePlan.PostOffsetForTrigger`); the cloth is a 6 x 14 grid moved by `CheckpointGatePlan.Wave`.
5. **The kart.** `KartVisual_Classic_Rookie` sits in Role 1's `Kart/Visual` slot. `KartVisualFromState` reads `Speed` and `Steer`, `KartVisualSteering` turns the wheel and rolls the tyres, `KartDriverArms` bends the arms with `TwoBoneIK` (no package). The `LODGroup` keeps the full kart while it fills 12 % of the screen height (about 13 m) and hides it below 0.6 % (`NovaKartVisualBuilder`); the far Classic has 34,000 instead of 111,000 triangles and 8 instead of 41 draw calls.
6. **Effects.** `KartEffects` (prefab in RaceBootstrap's Kart Attachments) shows smoke while drifting, sparks at stage 1 (blue) and 2 (orange) and a flame in the released stage's colour. `ItemFx` listens to `ItemSystem` events (`ItemUsed`, `RingCollected`, `ShieldBlocked`) and draws the wave, crema ring, gull and laurel wreath on the player's kart only; RaceFlow still decides every item and boost.
7. **Modes, drivers, bots.** Bots race only in Race vs Bots (`RaceModeRules.HasBots`). After driver select, `DriverSwap.Lineup` seats the three drivers not picked in their own karts plus two repainted Nova Classics (red: the teacher whose kart the player took, green: João; every driver and bot carries a teacher's first name, `docs/decisions.md` 2026-11-20); `KartVisualSwap` and `KartEffects.Rebind` swap the models. `BotSquad` spawns them once `RaceRoster` is set up. Each frame a `BotDriver` finds its point on `BotTrackMap` (every 2 m), aims `4 m + 0.3 s x speed` ahead, steers by pure pursuit (`steer = curvature x speed / 110 deg/s`), keeps below the corner limit over the next 40 m, gets up to 6 % more pace far behind the player and 10 % less far ahead (full at 80 m), and backs up after 2 s standing (`BotStuckCheck`, 3 tries, then back on the line).
8. **Finish.** After the player's line `BotSquad` sets `FinishCruise` as the kart's `IKartDrivable.InputSource` (55 % of top speed, no drifts) while Role 4's `FinishCinematic` shows the flag, swings the camera at 1.0 s, the place or time at 2.2 s and the card at 5.0 s. Bots keep driving.
9. **Web release.** `Role4WebBuild` (Role 4's file, V37 help) turns off static batching on WebGL and strips unused mesh data; `WebMeshBudget`, `WebTextureBudget` and `WebLoadBudget` shrink meshes and textures for WebGL only. `web-build.yml` packs `Build-Web` and its `pages` job publishes it, only for `main` (environment `github-pages`).

## Why it is built this way

- **World from data** (architecture.md): the campus can be rebuilt and corrected from OSM, surveys and route data instead of being hand-placed; saving over the old prefab keeps its GUID, so Role 2's `Track_Campus` updates by itself (V8).
- **Lighting in a component** (V8): a prefab cannot carry scene light settings; time of day switches at runtime, so a mood costs one sky texture and one volume profile (V28).
- **Grass as GPU instances** (2026-11-16, V39): about 36 instead of 900 bytes per tuft, grass in the Web build 5.3 MB to 0.46 MB, 9 draw calls for about 2,600 visible tufts.
- **Lean Web build** (2026-11-16, V37): about 123 MB to 52 MB; static batching stored a second copy of every static mesh, and the SRP Batcher already keeps draw calls cheap.
- **Rule-based bots on the player's kart** (2026-11-13, V10): one kart physics for everyone, so bots cannot cheat and can be rammed; every rule fits one sentence and an Edit Mode test.
- **Far kart on one palette material** (V32, 2026-11-15): draw calls cost the Web build more than triangles; tyres are rebuilt round with baked maps because simplifying made them angular.
- **ParticleSystem for kart effects, no lights** (2026-11-09, V5) and **item effects as events on a prefab Role 3 owns** (2026-11-15, V34): no scene or RaceFlow logic change, the wave is one mesh with 2 draw calls.
- **Code-made banners, no cloth physics** (V13): almost free, and the Web build strips engine modules no scene uses.
- **GitHub Pages, deploy only from `main`** (2026-11-08, #79): no compression in Unity and hashed file names, because Pages gzips without the header Unity's own `.gz` files need; `main` is what is deployed.

## Limits

- Changing the world needs Blender and the scripts; `world_data.py` needs a 55 MB OSM download not in the repo; outputs are committed FBX files (`CampusWorld.fbx` about 7.8 MB).
- Bots use no items (open task O2), stay within 2.2 m of the racing line and plan with a copy of the kart's turn rate (110 deg/s in `BotSettings`), so a `KartTuning` change needs the same number there.
- The far kart's steering wheel does not turn; the frame rate gain on weak laptops and phones, and with the V40 roster karts, was not measured.
- Without static batching the frame rate in a visible browser was not measured (V37); one line in `Role4WebBuild` turns it back on.
- Item effect lengths are copied into `ItemFx` (wave 3 s, Espresso 2 s); the kart is not lifted onto the wave.
- All checkpoint gates look the same; highlighting the next one needs a `Shared` contract (V13).
- V6 (a full race at the deployed build) is done; the live build is checked by hand before the presentation (`docs/demo-plan.md`), and every deploy is logged in Actions > Web build (`docs/builds.md`).

## How it could change

- **Night mood:** a third `TimeOfDayLook` plus lit lamps (`CampusWorldLighting`, `make_sky.py`).
- **Bots with items:** `BotDriver` uses an item like the E key, with a rule in `BotRules` for when.
- **Difficulty levels:** one factor on every profile's pace in `BotSettings`.
- **A fifth driver:** an entry in `DriverRoster`, the kart in `NovaKartRosterBuilder`, its far version with `make_kart_lod.py` and a `palette_<Kart>.json`.
- **Next-gate highlight:** a small contract from RaceFlow, read by `CheckpointGate`.

## Tests

- `EditMode/BotRulesTests.cs` (33): steering sign, full lock, bad input does nothing; corner speed and braking; throttle never reverses; rubber band capped at top speed; passing lanes stay on the road; drift only on long, even, flat bends; stuck after 2 s, back on the line after 3 tries; the track map never jumps across the U-turn. `FinishCruiseTests.cs` (5): gas, steering back to the line, bad input stands still.
- `KartLevelOfDetailTests.cs` (7): two levels with cross-fade, far under 35 % of the triangles, driver in both, bot paint reaches the far body, warm-up lists every material once. `Visuals/KartEffectsRulesTests.cs`, `KartEffectAnchorsTests.cs`, `TwoBoneIKTests.cs`: sparks, bursts and flame colours, anchors found at any depth, arm keeps its bone lengths.
- `ItemFxRulesTests.cs` (20): envelopes, the wave never hides the kart from the chase camera, gull path, rounded box, azulejo tiles. `GrassFieldTests.cs` (8), `BoostPadTests.cs` (11), `CheckpointGateTests.cs` (10), `TimeOfDayTests.cs` (10), `Drivers/DriverRosterTests.cs` (3), and the Web parts of `WebBuild/WebLoadTimeTests.cs`.
- Play Mode: `BotRacePlayTests` (five bots on the pair grid, a full campus lap without getting stuck), `ItemFxPlayTests`, `DriverSelectPlayTests`, and Role 4's `RaceModesPlayTests` for the modes and the finish autopilot.
- Builders check themselves: `CampusWorldBuilder.BuildAndCheck` (every prop placed, tufts on plain ground, no missing material, bounds match Blender, road collider under the route, the promenade shortcut drivable) and `NovaKartVisualBuilder.BuildAndCheck`; `tools/world_gen/test_promenade_shortcut.py` is plain Python, run by hand.
- Not tested: how the campus and effects look, and frame rate. Those were checked with rendered shots (`RenderShots`, `render_beauty.py`), playtests on the campus, and measured Web builds (V32: 120 FPS on one Mac with five bots).
