# Verification checklist

Graded under "verification" and "it works". Run before every merge into `main` that touches gameplay, and in full before 12, 17 and 20 Nov.

| Check | Expected result | Automated? |
|---|---|---|
| Countdown | Kart can't move, timer not started | Manual |
| Drift + boost | Holding drift while steering charges; release when charged = one short boost + visible cue; early release = no boost | Manual |
| Ordered checkpoints | A valid forward lap gives exactly one time and freezes it | Edit Mode test (`RaceRules`) + Play Mode `RaceSmokeTests` on the real track scene |
| Missed / duplicate / wrong-way crossing | Can't be exploited, race does not finish early | Edit Mode test (`RaceRules`) + Play Mode (skipped, out-of-order, second crossing) |
| Ramp jump | Kart lands upright, camera stays behind the kart | Manual |
| Recovery (R) | Kart returns to last valid checkpoint, timer keeps running, no progress awarded | Manual + Play Mode `RaceSmokeTests` |
| Retry | Clean timer, checkpoints, velocity and effects | Manual + Play Mode `RaceSmokeTests` |
| Audio | Starts after first input, no errors in browser console | Manual |
| Deployed build | A teammate on another computer opens the link and finishes two runs in a row | Manual |
| Fresh clone | A teammate opens the project in the pinned Unity version and sees all scenes and prefabs, no missing scripts | Manual |

Log each build test in `docs/builds.md`: date, URL, commit, browser, result.
