# Route: Carcavelos campus loop

Status: **draft until the campus walk (7 Nov)**. Role 2 owns this file after that.

## How the track gets built (order matters, saves time and AI tokens)
1. Route as a line on the map: direction, start/finish, corners, elevation changes.
2. Grey blocks for the main buildings and walls, at real scale (1 unit = 1 m).
3. Checkpoints, spawn, respawn points. Drive it. Lap time 35-60 s?
4. Only then: landmark models, colours, textures, props.
Never give an AI a pile of photos and say "build the track". Give it this file plus one section at a time.

## Candidate sections (fill in after the walk)
| # | Section | Feature | Photos |
|---|---|---|---|
| 01 | Start / finish | Wide, readable start grid | `01_start_*` |
| 02 | Walk of Founders | Straight | `02_founders_*` |
| 03 | EDP Plaza | Wide drift corner | `03_plaza_entry`, `03_plaza_exit` |
| 04 | Ramp between D and B wing (level 1) | **Jump** | `04_ramp_*` |
| 05 | Library facade | Landmark | `05_library_*` |
| 06 | Tunnel to the beach | Tunnel, light change | `06_tunnel_*` |
| 07 | Beach promenade | Fast straight, sea view | `07_promenade_*` |

Photo names: `<section>_<place>_<entry|exit|front|angle|detail|ground>_<nn>.jpg`. Raw photos and videos stay in the shared drive, never in the repo; only edited textures (max 1024 px) go into `Assets/_Project/Visuals/Textures/`.

Map reference for tracing the layout: OpenStreetMap (credit "© OpenStreetMap contributors" in the game credits). Google Maps / satellite images are reference only, never inside the game.

## Final route (after the walk)
_Sketch image path, section list, approximate lengths, where the real path was widened or shortened for play._
