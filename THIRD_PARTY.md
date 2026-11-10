# Third-party assets and code

Every file not made by us goes here in the same PR that adds it. No licence = not allowed. Nintendo / Mario Kart material is never allowed.

| Asset / code | Source (URL) | Licence | Used where | Added by | Date |
|---|---|---|---|---|---|
| Map data (route tracing only) | https://www.openstreetmap.org/copyright | ODbL, credit "© OpenStreetMap contributors" | `docs/route.md`, game credits | | 2026-11-07 |
| Map data for the campus world (building footprints, paths, coastline, trees) | https://www.openstreetmap.org/copyright | ODbL, credit "© OpenStreetMap contributors" | `tools/world_gen/world_data.json`, `Assets/_Project/Visuals/CampusWorld/`, game credits | Luc (Role 3) | 2026-11-09 |
| Elevation (AWS Terrain Tiles, Terrarium PNG, zoom 15) | https://registry.opendata.aws/terrain-tiles/ | Open data, attribution as listed in https://github.com/tilezen/joerd/blob/master/docs/attribution.md (SRTM, EU-DEM and others) | `tools/world_gen/dem/`, terrain of the campus world | Luc (Role 3) | 2026-11-09 |
| Sound effect `el_boost_whoosh.wav` (generated with ElevenLabs Sound Effects v3) | https://elevenlabs.io/docs/help-center/legal/can-i-publish-the-content-i-generate-on-the-platform | ElevenLabs **Creator plan (paid)**: commercial licence included, no attribution required; credit "Sound effects: elevenlabs.io" given anyway | `Assets/_Project/Audio/Clips/`, wired in `KartAudio` prefab | David (Role 4) | 2026-11-08 |
| Kart sounds `kart_engine_low.wav`, `kart_engine_high.wav`, `kart_drift_squeal.wav`, `kart_drift_sparks.wav`, `kart_drift_start.wav`, `kart_drift_stage_up.wav` (made by Luc Tippmann, Role 3, with ElevenLabs Sound Effects v2, loops cut with his own script) | https://elevenlabs.io/docs/help-center/legal/can-i-publish-the-content-i-generate-on-the-platform | ElevenLabs, **plan: pending Luc's confirmation**. If free plan: non-commercial only, credit "Sound effects: elevenlabs.io" (already in the README credits) | `Assets/_Project/Audio/Clips/`, wired in `KartAudio` prefab | David (Role 4), files from Luc (Role 3) | 2026-11-08 |

Allowed without asking: Kenney assets (CC0, https://kenney.nl). Everything else: team OK first (`AGENTS.md`).

Audio from ElevenLabs: note the plan used per file. Free plan = non-commercial only, credit "Sound effects: elevenlabs.io" (music: "Eleven Music") in the game credits and on the itch.io page. No donations or "pay what you want" on itch with free-plan audio.

Own work (not third party, listed so nobody wonders): the other 18 files in `Assets/_Project/Audio/Clips/` (`sfx_*` incl. `sfx_hint_warning` and `sfx_shift_pop`, `ui_click`, `amb_wind_loop`, `loop_engine`, `loop_drift`, `music_campus_loop`, `music_results_loop`) are synthesised by our own script `tools/audio-synth/synth_sfx.py` (pure Python, no samples). Released as CC0 by the team. Same for the five item-box sounds `sfx_item_box_break`, `sfx_item_roll_tick`, `sfx_item_ready`, `sfx_item_bica`, `sfx_item_onda` (task R7, 2026-11-10).

All WAVs in the table above and the own files are mastered by our own `tools/audio-synth/process_wavs.py` (loudness level, loop padding, loop points; 2026-11-08, P4). That changes only level and adds silence, not the content, so the licences above are unchanged.
