# Gaussian-splat references (V36)

Splats of campus landmarks, made from the team's walk videos, used in Blender 5.3 as true-scale references for the
generated world. They are a modelling reference only: Unity's web build cannot draw splats, and no splat, frame or
video goes into this repo (raw media rule, `AGENTS.md`). Everything lives on Luc's machine under `~/nova/reference/`.

## Pipeline (local, Apple M4 Pro, no CUDA, no cloud)

| Step | Tool | Time for ~240 frames |
|---|---|---|
| Frames from the original MOV (3 fps, 1920 px, one segment) | Swift + AVFoundation (`extract.swift`) | 20 s |
| Structure from motion (SIFT, sequential matching, incremental mapping) | pycolmap 4.2.1 (CPU) | about 3 min |
| Splat training | Brush v0.3.0 (Metal) | 5,000 steps in about 5 min |
| Import, place, compare | Blender 5.3 (`wm.ply_import` gives a Gaussian point cloud) | |

Walking footage gives several pieces with COLMAP's default thresholds; lower `abs_pose_min_num_inliers` (12) and
`abs_pose_min_inlier_ratio` (0.12) joined the main-entrance walk into one model.

## Scale and placement

- **Gravity:** a RANSAC plane through the floor points under the walk (not the phone's image axis: the phone's tilt
  biases that by 1-3 degrees).
- **Scale:** phone height over the floor, assumed 1.45 m (+-0.10 m), so every measurement here is **+-7 %**. Outside,
  the camera's height over the floor stays constant to 1 %; inside, the polished floor mirrors (fake points under
  the floor) and the scale drifts 5-12 % through a door, so indoor heights use the local camera height per segment.
- **Placement:** the roof-edge line of the west entrance front = the OSM outline (hall `o` = 0), the camera in the
  door = hall `u` 25.5 (route D1), floor = `nova_hall.FLOOR_Z`. Same frame as `nova_hall.building_frame()`.

## Main entrance + Hovione atrium (IMG_7722, 0-80 s)

| What | Real (splat, +-7 %) | World before | Change |
|---|---|---|---|
| Roof edge of the west entrance front | top 9.8 m, frame underside 8.8-9.0 m, banner 5.6-8.0 m | glass to 12.5 m, roof top 13.3 m | own lower front: `FRONT_H` 9.0 (white fascia 8.0-9.8 m, upper glass band 5.8-8.0 m), a lower roof strip `FRONT_DEPTH` 5.65 m deep, then a white step up to the atrium roof (11.6 m). The step's place is the footprint corner, not measured |
| Atrium roof trusses (south part, 54-71 s) | 10.1-11.1 m | 10.9-12.35 m | same (now 9.2-10.65 m) |
| Entrance zone under the bridge (24-29 s) | structure up to 10.3 m | | |
| Low ceiling before the long room (72-80 s) | 4.3-4.5 m | `LOW_Z` 4.6 | none (inside the error) |
| Entrance front | banner screen from 4.0 to 9.8 m on the roof-edge line, open below; glass doors 4.5 m behind it (covered porch) | full-height glass on the OSM line | **not changed**: moving the glass back moves door D1, the route and the rails (proposal below) |

`buildings.csv` (old campus block, roof 24.5 m = 10.0 m over the plaza) already matched the real front.
Also fixed: the wall between atrium and long room ended at the atrium's roof (11.6 m) under the long room's (11.8 m),
a 20 cm slit; it now goes up to the higher roof.
Check: same objects, `HallDoors.json` and `HallRailingCourse.json` byte-identical; only roof, shell and structure change.

**Proposal (not done, needs Noah):** the west entrance as the real 4.5 m deep porch: glass and doors 4.5 m into the
building, the banner screen from 4.0 to 9.8 m on the OSM line. Changes D1, `hall_join.join_west`, the entry rails.

## KPMG Gallery = the long room (IMG_7722, 58-112 s)

The Nova SBE campus tour calls the KPMG Gallery "the clearest and widest space on campus", with the Antena 2 piano,
so it is `nova_hall.py`'s long room (`PIANO_U` 84.3). 163 frames, one model, local scale 4.5-4.9 m per unit.

| What | Real (+-7 %) | World | Change |
|---|---|---|---|
| Steel hall roof structure (82-112 s) | top 10.2-11.1 m | `LONG_H` 11.0: trusses 9.4-10.8 m | none |
| Low ceiling before it (70-81 s) | 4.0-4.3 m | `LOW_Z` 4.6 | none (edge of the error) |
| Width, east wall to the far west layer | about 28 m | OSM 30 m | none |
| West facade zone | two vertical layers 4.5 m apart (doors, planters and railing in the inner one, the big X-braced frame in the outer one); verticals every ~2.0 m | glass on the OSM line, frame 0.45 and 1.5 m inside it, 3.0 m grid (`FACADE_FRAME_STEP`) | **not changed**: the sparse points do not show which layer is the glass; see proposal |

**Proposal (not done):** a bolder space frame in the KPMG Gallery (2.0 m module, thicker tubes, the big raking
round columns of the photos). Which of the two layers is the glass needs one look on site or a denser splat
before the frame moves, because the west lane and the terrace door D3 run through that zone.
