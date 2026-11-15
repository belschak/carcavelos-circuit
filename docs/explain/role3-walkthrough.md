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

## Light far kart (task V32)

**What.** Karts further away than about 13 m are drawn as a light copy: 34,000 instead of 111,000 triangles and 8 instead of 41 draw calls per kart (body, four tyres, driver). With five bots on track that is most of the karts most of the time. Unity blends the two versions over a short stretch (cross-fade), so the swap is not visible; from about 260 m a kart is not drawn at all.

**How it is built.**

| File | Job |
|---|---|
| `tools/kart_lod/make_kart_lod.py` | Blender script. Body: splits `Kart_Classic.fbx` by material, simplifies each part (Decimate, 20 %, number and checkers 60 %), points every part except the body paint at one cell of a colour palette. Tyre: measures the outline of `Tyre_Slick.fbx` with rays, spins it into a round shell (40 steps, 2,320 triangles) and bakes the full tyre onto it with Cycles: an ID map (which material where), a normal map (spokes, letters) and ambient occlusion (grooves, the deep brake disc). Writes `Kart_Classic_Far.fbx`, `Tyre_Slick_Far.fbx` and the maps |
| `tools/kart_lod/palette.json` | Which material sits in which palette cell, and each tyre material's ID colour (shared by the Blender script and Unity) |
| `Visuals/Kart/Editor/NovaKartVisualBuilder.cs` | Paints the 16 x 16 pixel palette textures and the far tyre's colour and metal maps (ID colour = that material's real look, darkened by the baked shade) from the real kart materials, puts the far copies next to the full ones and adds the `LODGroup`; `BuildAndCheck` measures that both versions line up (within 1 cm) and that the far one is cheap |
| `Visuals/Kart/KartLodWarmup.cs` | When the first kart appears, draws every far and near material once, plain and with the cross-fade keyword, into a hidden 16 x 16 picture, so the browser compiles those shaders before a bot drives far away (one web test had shown two 218 ms hitches right after the start) |
| `Tests/EditMode/KartLevelOfDetailTests.cs` | The built prefab has two levels with cross-fade, the far one is under 35 % of the triangles, the driver is in both, bot paint reaches the far body, painting nothing paints nothing |

**Why this way.** Draw calls cost the web build more than triangles, so the far kart uses one material instead of 23: a palette texture, where each face shows exactly its old colour, needs no texture baking and cannot blur. The tyre is different: simplifying a round tyre makes it angular (the first try looked like a polygon), so it is rebuilt round and gets its detail from baked textures, the usual way games make distant wheels. The colours still come from the Unity materials, so a colour change reaches the far tyre on the next build. The body paint stays its own material so `BotKartPaint` still recolours distant bots. The driver keeps its full model in both levels because the hand IK needs the full rig.

**Limits.** The far kart's steering wheel does not turn (the tyres still roll and steer). Thin parts like bolts are gone from the body; the tyre's grooves and spokes are only painted on (shade and normal map), which is invisible at 13 m. Black spots in the shade map (bake rays grazing the tyre's inside) are ignored by the builder. Measured in the web build on Luc's Mac (2560 x 1440, five bots, 60 s): 120 FPS and a 1 % low of 91 FPS with and without V32, because that Mac is held at its 120 Hz screen; with the warm-up no frame above 12 ms during the race. The gain shows on weaker laptops and phones, which have not been measured.

**How it could change.** Other karts from the roster: run the script with their FBX and add their materials to `palette.json`. A second, even lighter level (or a far driver) if the bots ever get more. The switch distance is one number (`NearUntil`) in the builder.

## Item effects and item boxes (task V34)

**What.** Using an item now shows it round the kart, not only on the HUD. Carcavelos Wave: turquoise water rises round the kart, with a big crest behind whose white lip curls over the tail, walls of water at the sides and a low bow wave in front. Spray fans out both sides, mist and foam patches stay behind, and there is a splash when the wave forms and when it lands. Espresso: a caramel crema ring races over the road, two glowing crema trails come off the rear wheels, speed lines rush past and the exhaust burns hotter. Seagull Delivery: Nori, the courier gull, pops in over the kart, tosses a lifebuoy onto each ring spot and leaves with a barrel roll; driving through a ring sparkles. Dean's List: a gold laurel wreath turns round the kart under a floating mortarboard; a blocked bump throws the cap up with confetti, and when the shield runs out the wreath bursts into gold leaves. The item box is a rounded azulejo block with a glaze, a soft glow, sparkles and a ring of light on the road (own design: no question mark, no see-through cube).

**How it is built.** Files in `Assets/_Project/Visuals/ItemFx/` and `Visuals/Items/`:

| File | Job |
|---|---|
| `ItemFx.cs` | Sits on the `KartEffects` prefab, so it reaches the kart through RaceBootstrap's "Kart Attachments" like the drift sparks. Listens to Role 4's `ItemSystem` (`ItemUsed`, `RingCollected`, `ShieldBlocked`, `ShieldActive`) and the race (`Restarted`, `Recovered`) and runs the four effects. Quiet on any kart that is not the race's player kart |
| `WaveRide.cs` + `WaveMesh.cs` | The wave: one ring mesh round the kart, rewritten every frame from a cross-section (`WaveMesh`, plain arrays, tested), two materials (water, foam band) + particles |
| `EspressoFx.cs`, `SeagullFx.cs`, `DeansFx.cs` | The other three effects (plain classes, ItemFx ticks them) |
| `ItemFxRules.cs` | The maths: envelopes, values round the kart, the gull's path, blink, toss, roll (Edit Mode tested) |
| `FxParts.cs` | Particle systems and model copies made in code, silent until an effect emits |
| `ItemFxLook.cs` + `.asset` | All materials and Luc's six item models (Items v01, Meshy) the effects draw with |
| `Items/AzulejoTile.cs`, `Items/RoundedCube.cs` | The box face (drawn in code, two variants) and the rounded box body (tested) |
| `Items/ItemBoxLookFx.cs` | The light on the road under a box: pulses, bursts when the box breaks, pops when it is back |
| `Editor/ItemFxBuilder.cs` | Menu Carcavelos Circuit > Visuals > Build Item FX: textures, materials, prefabs, the look asset, then the KartEffects prefab |

**Who does what.** RaceFlow (Role 4) still decides everything: which item, the boosts (through `IKartBoost`), the rings and the shield. ItemFx only draws. The boxes and rings are made by RaceFlow; they take Role 3's look prefabs through `ItemSettings` (box look, wave box look, ground look, ring look, coin ring off), filled by `Role4SceneBuilder`. That part is a help PR for Role 4. Without it, the boxes and rings keep their code-made look and the coin ring stays, so the merge order does not matter.

**Why this way.** No scene and no RaceFlow logic change for the effects: one component on a prefab Luc already owns. Events instead of polling, so an effect starts on the exact frame the item does. The wave is a mesh, not a big particle cloud: two draw calls and almost no overdraw, and a test makes sure it never covers the kart from the chase camera. Only URP Lit and Particles Unlit are used, which the Web build already ships.

**Limits.** The effects follow ItemSettings' default lengths (wave 3 s, Espresso 2 s, copied into ItemFx's Inspector) because ItemSystem does not expose its settings. The kart is not lifted onto the wave (the kart prefab is Role 1's). Bots have no items (O2), so they have no item effects.

**How it could change.** Another item = one more effect class and one line in `ItemFx.Play`. Bigger or calmer effects = the numbers at the top of each effect class and the `WaveShape` defaults.
