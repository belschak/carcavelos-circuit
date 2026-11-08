# Third-party assets and code

Every file not made by us goes here in the same PR that adds it. No licence = not allowed. Nintendo / Mario Kart material is never allowed.

| Asset / code | Source (URL) | Licence | Used where | Added by | Date |
|---|---|---|---|---|---|
| Map data (route tracing only) | https://www.openstreetmap.org/copyright | ODbL, credit "© OpenStreetMap contributors" | `docs/route.md`, game credits | | 2026-11-07 |
| Sound effect `el_boost_whoosh.wav` (generated with ElevenLabs Sound Effects v3) | https://elevenlabs.io/docs/help-center/legal/can-i-publish-the-content-i-generate-on-the-platform | ElevenLabs **Creator plan (paid)**: commercial licence included, no attribution required; credit "Sound effects: elevenlabs.io" given anyway | `Assets/_Project/Audio/Clips/`, wired in `KartAudio` prefab | David (Role 4) | 2026-11-08 |
| Kart sounds `kart_engine_low.wav`, `kart_engine_high.wav`, `kart_drift_squeal.wav`, `kart_drift_sparks.wav`, `kart_drift_start.wav`, `kart_drift_stage_up.wav` (made by Luc Tippmann, Role 3, with ElevenLabs Sound Effects v2, loops cut with his own script) | https://elevenlabs.io/docs/help-center/legal/can-i-publish-the-content-i-generate-on-the-platform | ElevenLabs, **plan: pending Luc's confirmation**. If free plan: non-commercial only, credit "Sound effects: elevenlabs.io" (already in the README credits) | `Assets/_Project/Audio/Clips/`, wired in `KartAudio` prefab | David (Role 4), files from Luc (Role 3) | 2026-11-08 |

Allowed without asking: Kenney assets (CC0, https://kenney.nl). Everything else: team OK first (`AGENTS.md`).

Audio from ElevenLabs: note the plan used per file. Free plan = non-commercial only, credit "Sound effects: elevenlabs.io" (music: "Eleven Music") in the game credits and on the itch.io page. No donations or "pay what you want" on itch with free-plan audio.

Own work (not third party, listed so nobody wonders): the other 15 files in `Assets/_Project/Audio/Clips/` (`sfx_*`, `ui_click`, `amb_wind_loop`, `loop_engine`, `loop_drift`, `music_campus_loop`) are synthesised by our own script `tools/audio-synth/synth_sfx.py` (pure Python, no samples). Released as CC0 by the team.
