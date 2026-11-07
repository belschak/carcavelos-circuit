# Plan and milestones

Team goal: **every role's part is done and merged by Fri 13 Nov 2026** (team meetup). After that: integrate, polish, rehearse.
Final presentation: **23 Nov 2026** per Luc's brief (course calendar shows W11 Tue 24 Nov / W12 Fri 4 Dec: confirm the exact slot with the TA).

| Date | Milestone | Done when |
|---|---|---|
| Sat 7 Nov | Campus walk + photos | Route sketch + labelled photos in the shared drive, route recorded in `docs/route.md` |
| Sun 8 Nov | First playable pieces | Unity project opens from a fresh clone; minimal Web build opens on another laptop; grey kart drives on a test surface; grey loop exists; one FBX imported at the right scale |
| Thu 12 Nov | Complete race at the live URL | Countdown -> valid lap -> finish time -> retry works in the browser, placeholders allowed |
| **Fri 13 Nov** | **Every part done** | Each role's checklist below is ticked and merged into `main` |
| Tue 17 Nov | Feature freeze | Drift + boost tuned on the campus track, landmarks in, outside playtest done |
| Fri 20 Nov | Release candidate | Two clean runs at the hosted URL in a fresh browser + on the presentation laptop |
| 21-22 Nov | Rehearsal | Walkthrough drill: random file, random teammate explains it |
| 23 Nov | Presentation | Live lap, drift, retry, then code walkthrough |

## Definition of done per role (by Fri 13 Nov)

**1 Driving + camera**
- [ ] Kart prefab with `KartController` matching `docs/interfaces.md`
- [ ] Accelerate, brake/reverse, steer; drift charges and releases a boost; early release gives no boost
- [ ] Ramp jump works (airborne, lands without flipping); recovery works when stuck or flipped
- [ ] Chase camera that follows smoothly, also through the jump
- [ ] Tuning values in a ScriptableObject (`KartTuning`), not hard-coded

**2 Campus track + race rules**
- [ ] `Track_Campus.unity`: closed loop of the agreed route, grey blocks first, elevation + ramp, walls so you can't leave the track
- [ ] Spawn point, ordered checkpoints with respawn points, `TrackInfo`
- [ ] `RaceRules` with Edit Mode tests: order, wrong way, duplicate finish, missed checkpoint
- [ ] Lap time on the track is 35-60 s at normal speed

**3 Kart, visuals + web release**
- [ ] Palette + style sheet (`docs/art-style.md`): colours, shapes, one reference image
- [ ] Kart + simple seated driver as FBX, swapped into `Kart/Visual`, scale checked against a 1 m cube
- [ ] 2-3 campus landmarks as low-poly FBX (from the photos), placed with Role 2
- [ ] `KartEffects`: visible drift sparks and boost cue
- [ ] Web build on itch.io, link in README, commit hash of each build in `docs/builds.md`
- [ ] Build steps in README so a second person can reproduce the export

**4 Race flow, UI + sound**
- [ ] `Main.unity` loads track + kart; `RaceManager` Ready -> Countdown -> Racing -> Results -> Retry
- [ ] HUD: countdown, timer, final time, boost cue, retry button
- [ ] Audio: engine (pitch by speed), drift, boost, countdown beeps, finish jingle, music (ElevenLabs)

## Stretch (only after 13 Nov and only if the core is solid)
Local personal best, a ghost of your best lap, a second kart colour, a short intro camera fly-over of the campus. Out of scope: items, AI opponents, multiplayer/split screen, kart selection, touch controls.
