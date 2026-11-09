# Nova campus track data (help for Role 2)

`make_track_data.py` turns `layout.json` (OpenStreetMap export of the Nova SBE Carcavelos campus, local metres, x = east, z = north) plus the hand-drawn route (`CTRL` list at the top of the script) into `Assets/_Project/Track/Editor/NovaTrackData.json`.

Rebuild the track after changing the route or props:

1. `python -I tools/nova_track/make_track_data.py tools/nova_track/layout.json Assets/_Project/Track/Editor/NovaTrackData.json`
2. Unity menu: Carcavelos Circuit > Build Nova Campus Track (help Role 2). This regenerates `Track_NovaCampus.unity` plus its meshes, materials and textures.

Map data (c) OpenStreetMap contributors, ODbL 1.0, https://www.openstreetmap.org/copyright. No campus photos are in this repo; textures are drawn in code (`NovaTextures.cs`).
