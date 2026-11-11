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
$PY make_sky.py            # painted golden-hour sky -> textures/sky_panorama.png (Unity skybox + reflections)
$PY make_groundmap.py      # ground colour map from the OSM shapes and the traced coast -> textures/ground_colour.png, ground_mask_data.png
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
| `coast.py` + `coast_trace.json` | the coast as data (V10a, 2026-11-10), traced from the DGT orthophoto: waterline (the terrain crosses z = 0 exactly on it), beach zone up to the backshore (OSM sand edge, then the promenade's sea edge w482695608 = V10b's kerb line), the cove and its kerb (sand 0.9 m below the promenade), 10 rock shelves, the far coast west of the world (Sea.shader uses the same straight line), the edge of the land east of the cove. Plain Python, shared by the terrain, the backdrop and the ground map |
| `env_terrain.py` | 5 m terrain shaped to the route: road bed, tunnel cover; coast (`coast.py`): seabed on the sea side, open sand on the beach profile in the beach zone, the cove bowl under its kerb, and no land point below 0.3 m anywhere inland (no hollow can show water) |
| `env_corridor.py` | high-resolution terrain strip (1–2 m) along the whole track: shoulders, 45° concrete cuttings at the tunnel ramps; the 5 m terrain has a hole under it. Reaches the tunnel headwalls (one extra row 0.6 m inside each portal) and runs as the bed under ground-level passages (Student Central, east end of the atrium), so there is ground beside the road everywhere. A 5 m cell is left out only where the corridor really has faces (`has_face`); where it has none but the cell would rise over the road (beside the jump kicker, at the tunnel portals) it stays out and gets a low floor with walls (`build_gap_patches`), so no gap shows the sky or the sea. Beside the sand cove the promenade stays flat to V10b's kerb and drops to the sand (`coast.KERB_DROP`) |
| `env_track.py` | 8 m road with one material per surface (asphalt, sand concrete, polished concrete, calçada, terracotta…), UVs in metres, kerbs in tight corners, jump kicker. The road lies 6 cm above its bed everywhere; indoors (atrium, terrace deck) it gets side skirts down to the floor |
| `env_trackside.py` | readability, exported with the track: painted edge lines 0.25 m inside each road edge (white on asphalt/tunnel floor/terracotta, graphite on the light concretes and calçada; none in the tunnel, across the jump or beside a kerb) and white chevron boards with Boost Orange arrows on the outside of the 4 sharpest outdoor corners (s 136, 256, 832, 1122). About 2,200 triangles |
| `env_tunnel.py` | beach tunnel: trapezoid section, ceiling lights, white headwalls, our own mural |
| `env_buildings.py` | campus facades (white walls, glass ground floor, ribbon windows, parapets), atrium glass roof with steel ribs, wedge-shaped Grand Auditorium, Horizon Hall cantilever; Portuguese colours and hip roofs around campus. OSM `building=roof` becomes an open canopy (0.3 m slab on slim pillars, none on the road); on a `drive_on_roof` building the parapet opens where the route crosses it. Drive-through passages keep a floor at driving height |
| `env_interior.py` | the drive-through halls: columns, glass walls, ceiling lights. In the main building (`WIDE_HALLS`) the passage opens into bays up to 11 m from the centreline (2.5 m inside the outer facade, 45 degree ends); `passages()` is shared by the cut, the interior and the furniture |
| `env_vegetation.py` | plant prototypes and placement. Soft-look test 2026-11-10 (local branch): stone pine, palm and olive are smooth shaded with custom normals (round trunks, crowns of smooth clumps) and use one ramp-textured material per tree (`textures/veg_ramp_*_albedo.png`, foliage and bark column, shade per vertex through the UV). Palm fronds are single smooth blades whose feathered outline is the alpha of `textures/palm_frond*_cutout_albedo.png` (`make_textures.py palm_frond`; material with `alpha_clip` and `double_sided` in materials.json, turned into URP Lit alpha clipping by CampusWorldBuilder). Wind: the soft trees carry a second UV layer `Sway` (bend weight by height, flutter weight at leaf tips) and a `wind` block in materials.json; CampusWorldBuilder then uses `Foliage.shader` (sway in world space, so it survives static batching; moving shadows; darker frond undersides). Triangles pine / palm / olive: 1,661 / 624-672 / 1,110 (before: 424 / 443-479 / 316) |
| `env_props.py` | street props and furniture: lamps, benches, railings, bins, bollards, parasols, lounge chairs; since 2026-11-09 cafe groups (round tables, chairs, cream parasols) on the plazas and the promenade, violet flower troughs along the straights, round planters, and the atrium bays (cafe and study tables, one grand piano) |
| `env_backdrop.py` | moving sea (test 2026-11-10): the flat water plane gets `Sea.shader` in Unity (two scrolling wave normals from `make_textures.py sea_waves`, shore colours and foam from the baked shore-distance map `textures/sea_shore_data.png`, written by `write_shore_map()`; data goes through the `water` block in materials.json). Since V10a the water plane is one polygon clipped to the sea side of the coast (it reaches 6 m under the beach and nowhere inland); the shore map covers the world plus 500 m west, 300 m east, 400 m south, and west of it the foam follows the straight far coast (`water.coast_west`). Carcavelos beach goes on for a kilometre beyond the west edge (`build_far_west`: a coast-following strip that continues the world's west-edge cross-section, sand, wet band, promenade, road, ground colour map read from the edge column, 842 triangles); the south skirt slopes into the sea round the fort; rock shelves on the beach and in the swash (`build_rocks`, about 2,200 triangles, ground-map material); land beyond the edge, the real buildings around, a far town (kept off the sea and the far beach), Forte de São Julião da Barra with its lighthouse |
| `make_grass_tufts.py` | 3D grass tufts (test 2026-11-10), a separate placement step that leaves the dressing alone: `out/grass_tufts.json` with tufts on lawn only, 1-6 m outside the road edge, at the feet of trees and props and along walls (every tuft re-checked against road, paths and buildings). CampusWorldBuilder builds them as crossed cards with `grass_tuft_cutout_albedo.png` (`make_textures.py grass_tuft`) and the Foliage shader, merged per 16 m cell and hidden beyond about 65 m |
| `env_materials.py` | puts `textures/*.png` on the materials; marks `W_ground_map` for `Ground.shader` (test 2026-11-10): lawn strokes and patches, sand ripples and grain, a sheen on the wet band at the waterline, rock shelves with strata lines (mask alpha = 1 - rock), from `ground_mask_data.png` (`make_groundmap.py`) and the tiling `ground_detail*_data.png` (`make_textures.py ground_detail`), through the `ground` block in materials.json |
| `env_render.py` | beauty renders (physical sky, golden-hour sun from the west-south-west, as in Unity) |
| `make_sky.py` | painted sky panorama (2048 x 1024, Unity lat-long mapping, sun at azimuth 245 / 19 degrees like the Unity sun) |

Unity import check (6000.3.28f1, 2026-11-08): world 92 meshes / about 101,000 triangles / 3.5 MB FBX, track 1,438 triangles, guide 1,186. Since 2026-11-09 the track has 4,154 triangles in Blender (edge lines and corner signs; not re-imported in Unity yet). Textures ≤ 1024 px except the mural and the ground map (2048).

## Sources and limits

- Map data © OpenStreetMap contributors (ODbL): credit in the game. Elevation: AWS Terrain Tiles (Terrarium, about 30 m), reshaped along the route with anchors from the walk videos.
- Coast (waterline, beach zone, rock shelves): hand-traced vector shapes from the DGT OrtoSat2023 orthophoto and the team's beach photos (`coast_trace.json`); no image data is copied.
- Building heights: estimates from the photos (±2 m) where `source = photo-estimate`, otherwise 3 storeys.
- The tunnel mural is an original design; the real murals, the surfer statue and the NOVA letters are other people's work or branding and are not copied.
- No flicker by design: no two surfaces closer than ~4 cm outside the halls (OSM paths only in the ground colour map, 5 m ground sunk under the corridor with an edge skirt, the beach crosses the water plane at 8 % so sand and water are within 4 cm of each other only in a 0.5 m band at the waterline). Check with the layer test in NEXT.md history.
- Unity: export_unity_bundle.py + CampusWorldBuilder.cs (self-test 2026-11-09: 1,074 trees, props and furniture placed, bounds equal to Blender to the millimetre, road collider under every checked route point). Not done yet: water shader, LODs, Role 2's TrackInfo instead of the race placeholders.

## Checks

`qa_geometry.py` (ray checks along the lap, ~2 s), `qa_compare.py` (two reports), `render_drive.py` + `contact_sheet.py` (chase-camera views), `wt_new.sh` (worktree per fix). State 2026-11-09 (`out/geometry_report_2026-11-09.json`): hole 0, intrusion 0, covered 0, overhead 0, chase_cam 0, edge_drop 0, edge_rise 0; coplanar 6 and height 15 are the jump kicker itself; tight 99 are the tunnel walls and the hall glazing (expected).
