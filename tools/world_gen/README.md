# Carcavelos campus world generator

Builds the whole Nova SBE Carcavelos campus, the beach tunnel, the promenade and the beach edge at real scale (1 unit = 1 m) from OpenStreetMap, elevation data, the team's walk photos and videos, and Luc's route sketch. Made by Role 3 (Luc) with Claude; the track itself is Role 2's (Noah): this world and the route guide are offered to him. In the team repo this folder is `tools/world_gen/`; the Unity side is `Assets/_Project/Visuals/CampusWorld/` (built by `CampusWorldBuilder.cs`).

## Run it (in this order)

```bash
cd tools/world_gen          # or wherever this folder lives
PY=python3                  # with numpy, pillow, matplotlib
BL=/Applications/Blender.app/Contents/MacOS/Blender
$PY world_data.py          # OSM + elevation -> world_data.json, buildings.csv (keeps your edits)
$PY route_from_sketch.py   # sketch + route_profile.json -> route.json (driving heights, zones, surfaces)
$PY make_textures.py       # procedural textures -> textures/*.png (no photos, original mural)
$PY make_groundmap.py      # ground colour map from the OSM shapes -> textures/ground_colour.png
$BL -b -P build_world.py   # -> out/CampusWorld.blend, CampusWorld.fbx, TrackSurface.fbx, RouteGuide.fbx, previews
$BL -b -P render_beauty.py # -> out/beauty_*.png (EEVEE, sky, sun, kart-eye shots, hero kart)
$PY export_unity_bundle.py # -> out/unity_export/: copy Models, Textures, Data into Assets/_Project/Visuals/CampusWorld/
```
Then in Unity: **Carcavelos Circuit > Build Campus World (Role 3)** builds materials, prefabs and `Assets/_Project/Visuals/Scenes/Track_CampusWorld.unity`. Batch with self-test: `Unity -batchmode -projectPath <repo> -executeMethod CarcavelosCircuit.Visuals.Editor.CampusWorldBuilder.BuildAndCheck`.
`world_data.py` needs the OpenStreetMap download (55 MB, not in the repo, set `CAMPUS_OSM`); everything after it runs from the committed `world_data.json`.
Media helpers (walk videos and photos, local only): `extract_frames.py`, `photo_targets.py`, `video_sheets.py`. Findings per route section: `route_notes.md`.

## Rules that keep later changes cheap

- **Fixed origin:** (0, 0) = lat 38.6780, lon -9.3265. Never move it.
- **Axes in Unity:** +X = east, +Z = north, +Y = up. Checked in Unity 6000.3.28f1. Unity always maps Blender −Y to Unity +Z, so `build_world.py` turns the geometry 180° only for the FBX export; the `.blend` stays north-up like the map.
- **Data, not hand edits:** building heights, drive-through passages and decks in `buildings.csv`; driving heights, zones and surface types in `route_profile.json`; the route from the sketch (`route.json`). Change the data, run again.
- **One object per building** `B_<osm id>_<name>` (details as child `BD_…`): a landmark model replaces it in place.
- **Separate files:** world (`CampusWorld.fbx`), track (`TrackSurface.fbx`), guide (`RouteGuide.fbx`).

## Route (from Luc's sketch, 2026-11-08)

Closed loop, 1,184 m, point 0 = south portal of the beach tunnel, point order = driving direction. Tunnel 84 m (floor 3.8–5.0 m) → sunken ramp to the lower plaza (11.2 m) → terracotta steps as a ramp → upper level 14.5 m (plaza, atrium, terrace deck) → jump off the terrace edge (s = 506 m, 3.8 m drop) → lawns → Avenida Marginal → promenade (about 2–4 m) → tunnel plaza. Steepest slope 14 % (tunnel ramp).

## What is in the world (modules)

| Module | What it builds |
|---|---|
| `env_terrain.py` | 5 m terrain shaped to the route: road bed, tunnel cover, shoreline along the real OSM coast |
| `env_corridor.py` | high-resolution terrain strip (1–2 m) along the whole track: shoulders, 45° concrete cuttings at the tunnel ramps; the 5 m terrain has a hole under it. Reaches the tunnel headwalls (one extra row 0.6 m inside each portal) and runs as the bed under ground-level passages (Student Central, east end of the atrium), so there is ground beside the road everywhere |
| `env_track.py` | 8 m road with one material per surface (asphalt, sand concrete, polished concrete, calçada, terracotta…), UVs in metres, kerbs in tight corners, jump kicker. The road lies 6 cm above its bed everywhere; indoors (atrium, terrace deck) it gets side skirts down to the floor |
| `env_trackside.py` | readability, exported with the track: painted edge lines 0.25 m inside each road edge (white on asphalt/tunnel floor/terracotta, graphite on the light concretes and calçada; none in the tunnel, across the jump or beside a kerb) and white chevron boards with Boost Orange arrows on the outside of the 4 sharpest outdoor corners (s 136, 256, 832, 1122). About 2,200 triangles |
| `env_tunnel.py` | beach tunnel: trapezoid section, ceiling lights, white headwalls, our own mural |
| `env_buildings.py` | campus facades (white walls, glass ground floor, ribbon windows, parapets), atrium glass roof with steel ribs, wedge-shaped Grand Auditorium, Horizon Hall cantilever; Portuguese colours and hip roofs around campus. OSM `building=roof` becomes an open canopy (0.3 m slab on slim pillars, none on the road); on a `drive_on_roof` building the parapet opens where the route crosses it. Drive-through passages keep a floor at driving height |
| `env_interior.py` | the atrium hall along the passage: columns, glass walls, ceiling lights |
| `env_backdrop.py` | sea to the horizon, land beyond the edge, the 92 real buildings around, a far town, Forte de São Julião da Barra with its lighthouse |
| `env_materials.py` | puts `textures/*.png` on the materials |
| `env_render.py` | beauty renders (physical sky, low afternoon sun from the south-west as on the walk at 16:00) |

Unity import check (6000.3.28f1, 2026-11-08): world 92 meshes / about 101,000 triangles / 3.5 MB FBX, track 1,438 triangles, guide 1,186. Since 2026-11-09 the track has 4,154 triangles in Blender (edge lines and corner signs; not re-imported in Unity yet). Textures ≤ 1024 px except the mural and the ground map (2048).

## Sources and limits

- Map data © OpenStreetMap contributors (ODbL): credit in the game. Elevation: AWS Terrain Tiles (Terrarium, about 30 m), reshaped along the route with anchors from the walk videos.
- Building heights: estimates from the photos (±2 m) where `source = photo-estimate`, otherwise 3 storeys.
- The tunnel mural is an original design; the real murals, the surfer statue and the NOVA letters are other people's work or branding and are not copied.
- No flicker by design: no two surfaces closer than ~4 cm outside the halls (OSM paths only in the ground colour map, 5 m ground sunk under the corridor with an edge skirt, beach 0.3-1.5 m above the water plane). Check with the layer test in NEXT.md history.
- Unity: export_unity_bundle.py + CampusWorldBuilder.cs (self-test: 904 trees and props placed, bounds equal to Blender to the millimetre, road collider under every checked route point). Not done yet: water shader, LODs, Role 2's TrackInfo instead of the race placeholders.

## Checks

`qa_geometry.py` (ray checks along the lap, ~2 s), `qa_compare.py` (two reports), `render_drive.py` + `contact_sheet.py` (chase-camera views), `wt_new.sh` (worktree per fix). State 2026-11-09 (`out/geometry_report_2026-11-09.json`): hole 0, intrusion 0, covered 0, overhead 0, chase_cam 0, edge_drop 0, edge_rise 0; coplanar 6 and height 15 are the jump kicker itself; tight 99 are the tunnel walls and the hall glazing (expected).
