# V19 Promenade Shortcut + Wave Boxes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Behind Bar dos Gémeos the lap gets a second way: today's route (green, down to the beach at Praia do Moinho) stays the main line and gets a row of three item boxes that always give the Carcavelos Wave; a new straight shortcut (red, along the parking aisle) is 33 m shorter and has no boxes.

**Architecture:** Same pattern as the V14b courtyard shortcut (`plaza_plan.py` + `env_plaza.py`): one pure-Python plan module holds the shortcut's geometry and checks (testable without Blender); a Blender module paints it and clears dressing off it; `fence_free.py` opens the white fence at its two mouths; a small data file tells Unity where the wave boxes go. The main line, the checkpoints' order, the bots, `TrackProgress` and the minimap stay on green; only checkpoint 6 moves out of the fork. RaceFlow (Role 4) gets wave boxes through a help PR.

**Tech Stack:** Python 3 (system `python3`, no numpy in the plan module), Blender (generator), Unity 6000.3.28f1 C#, NUnit Edit Mode tests.

**Spec:** Luc's sketch and answers in the Claude session of 2026-11-11/12 (variant A, "red = new shortcut, green = today's route with wave boxes, move the checkpoint"). Picture: `docs/superpowers/plans/2026-11-12-promenade-shortcut-overlay.png` (drawn in the `tools/nova_track/layout.json` frame, see Global Constraints).

## Global Constraints

- Unity **6000.3.28f1**, never upgrade; no new packages.
- Never text-edit `.unity`, `.prefab`, `.asset`, `.mat`, `.meta`; scenes and prefabs change only through builder code run in the Editor.
- Coordinates: route.json Blender x/y = track.json Unity x/z, same origin (lat 38.6780 / lon -9.3265). `tools/nova_track/layout.json` and the overlay picture use another origin: layout = Unity + (-130.6, +16.5). Never mix them.
- Shortcut values: split `(-173.36, -102.51)`, merge `(-45.24, -122.35)`, via `(-159.0, -104.0)`, `(-140.0, -114.0)`, `(-121.0, -118.0)`, `(-83.0, -123.0)`; width `7.0` m; resample step `2.0` m; clearance `4.5` m (half width + 1 m); snap limit `3.0` m.
- Wave row: anchor `(-123.16, -150.58)` (old checkpoint 6 anchor = bottom of the green dip), `3` boxes, `2.6` m apart (ItemSettings.boxSpacing).
- Checkpoint 6 anchor moves from `(-123.16, -150.58)` to `(-179.18, -97.02)`; checkpoint count stays 12.
- Ownership (AGENTS.md): Role 3 (Luc) owns `tools/world_gen/**`, `Assets/_Project/Visuals/**`. `Assets/_Project/RaceFlow/**` only through branch `help/role4-R7-wave-boxes-luc`, merged by David. `Assets/_Project/Track/**` is not touched.
- Git: commits as Luc's own account, files added by name, every `.cs` with its `.meta`, commit and push only after Luc says yes. No force push, no rebase of pushed branches.
- Deadlines: every feature merged **Fri 13 Nov 2026**, feature freeze **Sun 22 Nov**.

## Prerequisites (gate, before Task 1)

- [ ] Luc posts in the group: shortcut + wave boxes, checkpoint 6 anchor move (Noah's anchors, his condition of 2026-11-11), help PR for RaceFlow (David). Wait for Noah's and David's OK in the chat.
- [ ] Rows in `TASKS.md`: `V19` (Role 3, claimed (Luc, 2026-11-12)), two requests with the next free Q numbers (Q21 is already taken by the fence plan): `3 -> 4 | review + merge help PR wave boxes` and `3 -> 2 | OK for checkpoint 6 anchor move`. Row in `docs/decisions.md`: `2026-11-12: promenade shortcut (V19); checkpoint 6 anchor (-123.16, -150.58) -> (-179.18, -97.02); wave boxes via WaveBoxSpot (RaceFlow)`.
- [ ] First commit on `role3/V19-promenade-shortcut` = only these rows; push; `gh pr create --draft --fill --base role3/V18-world-pass` (V18 must be pushed first; if it is not, ask Luc).

## Review Focus

1. A static that is not dressing (kerb, planter, bar terrace wall, parking island) stands in the red lane: the kart stops dead. Expected: nothing to hit between split and merge. Test: Task 5 sweep.
2. The white fence still crosses a mouth of the shortcut. Expected: both mouths open on the left side. Test: Task 1 `test_free_runs_open_every_piece_near_the_shortcut` + Task 5 sweep (it runs from split to merge, so a fence at a mouth is a hit).
3. A later export puts a checkpoint back inside the fork (anchors file reverted, respread). Expected: the pipeline stops with a clear message. Test: Task 1 `test_check_flags_checkpoint_on_fork`, Task 2 `test_v19_track_has_no_checkpoint_on_fork`.
4. WRONG WAY shows on the red lane. Expected: never (WrongWayDetector compares heading with the direction to the next checkpoint, which is checkpoint 7 at `(-27.26, -123.06)`). Test: Task 1 `test_shortcut_heads_towards_next_checkpoint` (every segment's cosine to checkpoint 7 >= 0.15, the detector's ExitDot).
5. Lap progress runs backwards on red (TrackPolyline projects onto green). Expected: never backwards (a forward jump of about 63 m is fine). Test: Task 1 `test_progress_never_goes_back_on_shortcut`.

---

### Task 1: Shortcut plan module (pure Python)

**Files:**
- Create: `tools/world_gen/promenade_shortcut_plan.py`
- Test: `tools/world_gen/test_promenade_shortcut.py` (unittest, run with system `python3`)

**Interfaces:**
- Produces (used by Tasks 2-5): constants `SPLIT, MERGE, VIA, WIDTH, STEP, CLEAR, SNAP_LIMIT, WAVE_ANCHOR, WAVE_COUNT, WAVE_SPACING, NEXT_CHECKPOINT`; and
  - `centreline() -> list[tuple[float, float]]`: SPLIT, VIA, MERGE resampled every STEP; first and last point exact.
  - `length(line) -> float`
  - `distance(x: float, y: float, line) -> float`: flat distance to the polyline.
  - `progress(points, x: float, y: float, key="z") -> float`: distance along the closed route to the nearest point on it, flat (same rule as `TrackPolyline.DistanceAlongLap`).
  - `fork(points: list[dict], key: str = "z") -> tuple[int, int]`: route indices nearest SPLIT and MERGE (`key` = `"z"` for track.json, `"y"` for route.json); raises `ValueError` if a snap is farther than SNAP_LIMIT or split >= merge.
  - `check(points, checkpoints: list[int], key="z") -> list[str]`: error strings, empty = fine.
  - `wave_spots(points, key="z") -> list[tuple[float, float]]`
  - `free_runs(n_pieces: int, fence_point, line) -> list[tuple[int, int]]`: `fence_point(k) -> (x, y)` is the left fence point of piece k; returns (from, to) runs (from <= k < to) of pieces whose fence point is within CLEAR of `line`.
  - CLI: `python3 tools/world_gen/promenade_shortcut_plan.py [--check]` reads `Assets/_Project/Visuals/CampusWorld/Data/track.json`, prints split/merge index, red and green length, errors; exit 1 on any error. Without `--check` it also writes `Data/shortcuts.json` (Task 4 defines the format).

- [ ] **Step 1: Write the failing tests**

```python
import json, math, os, unittest
import promenade_shortcut_plan as P
TRACK = os.path.join(os.path.dirname(__file__), "../../Assets/_Project/Visuals/CampusWorld/Data/track.json")

def straight(n, step=2.0, y=0.0):  # fake route along +x
    return [{"x": i * step, "z": y, "s": i * step} for i in range(n)]

class PlanTests(unittest.TestCase):
    def test_centreline_ends_and_step(self):
        line = P.centreline()
        self.assertEqual(line[0], P.SPLIT); self.assertEqual(line[-1], P.MERGE)
        self.assertTrue(all(math.dist(a, b) <= P.STEP + 1e-6 for a, b in zip(line, line[1:])))
    def test_shortcut_is_131_m(self):
        self.assertAlmostEqual(P.length(P.centreline()), 131.4, delta=0.5)
    def test_fork_on_real_track(self):
        pts = json.load(open(TRACK))["points"]
        a, b = P.fork(pts)
        self.assertEqual(b - a, 82)
        self.assertAlmostEqual(pts[b]["s"] - pts[a]["s"], 164.0, delta=0.5)
    def test_fork_rejects_far_route(self):
        with self.assertRaises(ValueError):
            P.fork(straight(50, y=500.0))
    def test_check_flags_checkpoint_on_fork(self):
        pts = json.load(open(TRACK))["points"]
        a, b = P.fork(pts)
        errors = P.check(pts, [0, a + 10])
        self.assertTrue(any("checkpoint 1" in e for e in errors))
    def test_wave_spots_row(self):
        pts = json.load(open(TRACK))["points"]
        spots = P.wave_spots(pts)
        self.assertEqual(len(spots), 3)
        self.assertAlmostEqual(math.dist(spots[0], spots[1]), 2.6, delta=0.01)
        self.assertLess(min(math.hypot(p["x"] - spots[1][0], p["z"] - spots[1][1]) for p in pts), 0.01)
    def test_free_runs_open_every_piece_near_the_shortcut(self):
        line = [(10.0, 0.0), (30.0, 0.0)]
        fence = [(float(k), 0.0 if 8 <= k <= 12 else 50.0) for k in range(20)]
        self.assertEqual(P.free_runs(20, lambda k: fence[k], line), [(8, 13)])
    def test_shortcut_heads_towards_next_checkpoint(self):
        line = P.centreline()
        for a, b in zip(line, line[1:]):
            h = (b[0] - a[0], b[1] - a[1]); t = (P.NEXT_CHECKPOINT[0] - a[0], P.NEXT_CHECKPOINT[1] - a[1])
            cos = (h[0] * t[0] + h[1] * t[1]) / (math.hypot(*h) * math.hypot(*t))
            self.assertGreaterEqual(cos, 0.15)
    def test_progress_never_goes_back_on_shortcut(self):
        pts = json.load(open(TRACK))["points"]
        along = [P.progress(pts, x, y) for x, y in P.centreline()]
        self.assertTrue(all(b >= a - 0.5 for a, b in zip(along, along[1:])))

if __name__ == "__main__":
    unittest.main()
```

`NEXT_CHECKPOINT = (-27.26, -123.06)` (checkpoint 7 anchor).

- [ ] **Step 2: Run, verify they fail**

Run: `cd tools/world_gen && python3 test_promenade_shortcut.py`
Expected: `ModuleNotFoundError: No module named 'promenade_shortcut_plan'`

- [ ] **Step 3: Implement `promenade_shortcut_plan.py`** with the constants from Global Constraints and the interface above. Docstring in the house style (what, why, how to run, "rerun after fence_free.py"). `check()` reports, one string each: a snap over SNAP_LIMIT, split >= merge, red not shorter than green (`s[merge] - s[split]`), and every checkpoint index c with split <= c <= merge as `"checkpoint {n} (index {c}) lies on the fork: move its anchor in checkpoint_anchors.json"`.

- [ ] **Step 4: Run, verify all pass** (`test_fork_on_real_track` etc. read the branch's track.json)

Run: `cd tools/world_gen && python3 test_promenade_shortcut.py`
Expected: `OK` (9 tests)

- [ ] **Step 5: Commit** `tools/world_gen/promenade_shortcut_plan.py`, `tools/world_gen/test_promenade_shortcut.py`: `V19: promenade shortcut plan (geometry + checks, tested)`

### Task 2: Move checkpoint 6 out of the fork

**Files:**
- Modify: `tools/world_gen/checkpoint_anchors.json` (entry `n: 6`)
- Modify: `Assets/_Project/Visuals/CampusWorld/Data/track.json` (by running `pin_checkpoints.py`, never by hand)
- Test: `tools/world_gen/test_promenade_shortcut.py`

**Interfaces:**
- Consumes: `P.check`, `P.fork` (Task 1).

- [ ] **Step 1: Write the failing test**

```python
    def test_v19_track_has_no_checkpoint_on_fork(self):
        t = json.load(open(TRACK))
        self.assertEqual(P.check(t["points"], t["checkpoints"]), [])
        self.assertEqual(len(t["checkpoints"]), 12)
```

- [ ] **Step 2: Run, verify it fails** with the message about checkpoint 6.

Run: `cd tools/world_gen && python3 test_promenade_shortcut.py`
Expected: FAIL, `checkpoint 6 (index 340) lies on the fork`

- [ ] **Step 3: Set anchor `n: 6` to `x: -179.18, z: -97.02`**, add `"why": "V19: out of the promenade fork (route point 4 before the split), Noah's OK <date>"`, then run `python3 tools/world_gen/pin_checkpoints.py` (it keeps the order check).

- [ ] **Step 4: Run, verify it passes**

Run: `cd tools/world_gen && python3 test_promenade_shortcut.py && python3 promenade_shortcut_plan.py --check`
Expected: `OK`; CLI prints split 305, merge 387, red 131 m, green 164 m, no errors.

- [ ] **Step 5: Commit** anchors file + track.json: `V19: checkpoint 6 anchor before the promenade fork (Noah's OK)`

### Task 3: Paint and clear the shortcut in the generator (Blender)

**Files:**
- Create: `tools/world_gen/env_promenade_shortcut.py`
- Modify: `tools/world_gen/build_world.py` (import; `dress` after `env_capricciosa.dress`, `build()` after `env_coast.build()`)

**Interfaces:**
- Consumes: `P.centreline, P.distance, P.WIDTH, P.CLEAR` (Task 1); `env_common` (`E.ground_z`, `E.material`, `E.new_object`) as `env_plaza.build_paint` uses them.
- Produces: Blender object `Track_PromenadeShortcutPaint_NoCollide` (the Unity builder already skips colliders for names containing `NoCollide`).

- [ ] **Step 1: Implement `dress(veg, props, protos) -> (veg, props)`**: drop every item with `P.distance(x, y, P.centreline()) < P.CLEAR`; print `promenade shortcut: dressing: removed <proto@x,y, ...>` like `env_capricciosa.dress`.
- [ ] **Step 2: Implement `build()`**: white edge lines 0.14 m wide at +-WIDTH/2 and three Boost Orange chevrons at the split mouth pointing along the shortcut, `PAINT_LIFT = 0.04` m over `E.ground_z`, materials as `env_trackside` lines and `env_plaza` chevrons. Then the ground check: sample `E.ground_z` on the centreline every STEP; raise `RuntimeError("promenade shortcut: ground steps {dz:.2f} m at ({x:.0f}, {y:.0f})")` if two neighbours differ by more than 0.6 m. Print `promenade shortcut: {length:.0f} m, ground {zmin:.2f}..{zmax:.2f} m, max step {dz:.2f} m`.
- [ ] **Step 3: Build**

Run: `$BL -b -P build_world.py` (from `tools/world_gen`, `$BL` as in `tools/world_gen/README.md`)
Expected: the two `promenade shortcut:` lines, max step <= 0.6 m, the build finishes. A removed list naming parked cars, palms, bollards, parking signs, the pay machine and railing segments near the parking aisle is expected (14 kinds within 5 m of the line in the V18 dressing).

- [ ] **Step 4: Commit** `env_promenade_shortcut.py`, `build_world.py`: `V19: promenade shortcut painted, dressing cleared, ground checked`

### Task 4: Fence gaps and Unity data

**Files:**
- Modify: `tools/world_gen/fence_free.py` (one `add(...)` per run from `P.free_runs`, side `-1`, why `"V19 promenade shortcut mouth"`)
- Modify: `tools/world_gen/promenade_shortcut_plan.py` (write `Data/shortcuts.json` without `--check`)
- Modify: `tools/world_gen/README.md` (pipeline order: export -> hall_route.py -> hall_join.py -> pin_checkpoints.py -> fence_free.py -> promenade_shortcut_plan.py)
- Output: `Assets/_Project/Visuals/CampusWorld/Data/fence_free.json`, `Assets/_Project/Visuals/CampusWorld/Data/shortcuts.json`
- Test: `tools/world_gen/test_promenade_shortcut.py`

**Interfaces:**
- Produces `shortcuts.json` (JsonUtility-friendly, Unity axes, y found in Unity by raycast):
  `{"note": "...", "shortcuts": [{"name": "promenade straight", "width": 7.0, "points": [{"x": -173.36, "z": -102.51}, ...]}], "wave_boxes": [{"x": ..., "z": ...}, {...}, {...}]}`

- [ ] **Step 1: Write the failing test**

```python
    def test_shortcuts_json_matches_plan(self):
        data = json.load(open(os.path.join(os.path.dirname(TRACK), "shortcuts.json")))
        s = data["shortcuts"][0]
        self.assertEqual(s["name"], "promenade straight"); self.assertEqual(s["width"], 7.0)
        self.assertEqual(len(s["points"]), len(P.centreline()))
        self.assertEqual(len(data["wave_boxes"]), 3)
    def test_fence_free_opens_both_mouths(self):
        free = json.load(open(os.path.join(os.path.dirname(TRACK), "fence_free.json")))["free"]
        mouths = [f for f in free if "V19" in f["why"]]
        self.assertGreaterEqual(len(mouths), 2)
        self.assertTrue(all(f["side"] == -1 for f in mouths))
```

- [ ] **Step 2: Run, verify they fail** (`FileNotFoundError: shortcuts.json`).
- [ ] **Step 3: Implement** the fence runs and the writer.
- [ ] **Step 4: Run the pipeline and the tests**

Run: `python3 tools/world_gen/fence_free.py && python3 tools/world_gen/promenade_shortcut_plan.py && cd tools/world_gen && python3 test_promenade_shortcut.py`
Expected: fence_free prints the two V19 runs; plan CLI prints no errors; `OK` (12 tests).

- [ ] **Step 5: Commit** `fence_free.py`, `promenade_shortcut_plan.py`, `README.md`, `fence_free.json`, `shortcuts.json`, plus the re-exported FBX/data from Task 3's build: `V19: fence open at the shortcut's mouths, shortcuts.json for Unity`

### Task 5 (help PR for Role 4): wave boxes in RaceFlow

Branch `help/role4-R7-wave-boxes-luc` from fresh `main`, PR to `main`, David reviews and merges. Commit messages start with `help(role4):`, body "done by Luc's Claude".

**Files:**
- Create: `Assets/_Project/RaceFlow/WaveBoxSpot.cs` (+ `.meta` from the Editor)
- Modify: `Assets/_Project/RaceFlow/ItemOdds.cs`, `ItemBox.cs`, `ItemSystem.cs`
- Modify: `docs/interfaces.md` (one row: "A `WaveBoxSpot` in the track scene = one box that always gives the Carcavelos Wave; ItemSystem places it like a row box")
- Test: `Assets/_Project/Tests/EditMode/ItemOddsTests.cs`

**Interfaces:**
- Produces: `public class WaveBoxSpot : MonoBehaviour { }` (namespace `CarcavelosCircuit.RaceFlow`; marker only, position = where the box goes). `public static ItemKind ItemOdds.PickForBox(bool waveBox, int[] weights, float roll01)`. `ItemBox.SetUp(ItemSystem owner, Material material, float boxSize, float phase, bool givesWave = false)` and `public bool GivesWave { get; }`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test] public void PickForBox_WaveBox_GivesWaveForEveryRoll()
{
    foreach (float roll in new[] { 0f, 0.5f, 0.999f })
        Assert.AreEqual(ItemKind.CarcavelosWave, ItemOdds.PickForBox(true, new[] { 100, 0, 0, 0 }, roll));
}
[Test] public void PickForBox_WaveBox_EvenWhenAhead()
{
    int[] ahead = ItemOdds.WeightsFor(true, -10f, 1f, 5f);
    Assert.AreEqual(ItemKind.CarcavelosWave, ItemOdds.PickForBox(true, ahead, 0f));
}
[Test] public void PickForBox_NormalBox_SameAsPick()
{
    int[] level = { 40, 30, 22, 8 };
    Assert.AreEqual(ItemKind.Espresso, ItemOdds.PickForBox(false, level, 0.1f));
    Assert.AreEqual(ItemKind.DeansList, ItemOdds.PickForBox(false, level, 0.95f));
}
```

- [ ] **Step 2: Run, verify they fail** (Unity Test Runner, Edit Mode, `ItemOddsTests`): compile error `PickForBox` not found.
- [ ] **Step 3: Implement.** `OnBoxHit` uses `ItemOdds.PickForBox(box.GivesWave, weights, UnityEngine.Random.value)`. `SpawnBoxes` after the rows: one box per `FindObjectsByType<WaveBoxSpot>(FindObjectsSortMode.None)` through `TryPlaceBox(spot.transform.position, 900 + k, true)` (so walls and missing ground still skip it); no spots = only the normal rows, no warning. Wave boxes use a copy of the live material tinted with the wave turquoise of `UI/ItemIcon.cs`, so the player sees which boxes are special. Log line: `ItemSystem: N item boxes in R rows, W wave boxes.`
- [ ] **Step 4: Run, verify all Edit Mode tests pass** (whole suite green, count = before + 3).
- [ ] **Step 5: Commit, push, PR** (`gh pr create --fill`, reviewer David). Tell the group.

### Task 6: Wave box spots and shortcut self-test in the campus world

Needs Task 5 on `main`: in the V19 worktree `git merge main` (AGENTS.md A.6; conflicts in others' files -> stop and ask Luc).

**Files:**
- Modify: `Assets/_Project/Visuals/Editor/CampusWorldBuilder.cs`

**Interfaces:**
- Consumes: `shortcuts.json` (Task 4), `WaveBoxSpot` (Task 5; `CarcavelosCircuit.Visuals.Editor` already references RaceFlow).
- Produces: `private static void AddWaveBoxSpots(GameObject root, ShortcutFile data)`, `private static int ShortcutSelfTest(StringBuilder report)`, serializable `ShortcutFile { ShortcutLine[] shortcuts; XZ[] wave_boxes; }`, `ShortcutLine { string name; float width; XZ[] points; }`, `XZ { float x, z; }`.

- [ ] **Step 1: Add the self-test first** (it fails until the spots exist): `ShortcutSelfTest` counts `WaveBoxSpot` in the scene (must be 3), then walks the shortcut every 2 m from split to merge: ground ray from y 300 down must hit; neighbours differ by <= 0.6 m in height; `Physics.OverlapBox(ground + up * 0.8, new Vector3(1.0f, 0.6f, 1.2f), Quaternion.LookRotation(direction), ~0, QueryTriggerInteraction.Ignore)` must be empty, else report every collider name and the position. Wire it into `SelfTest()`'s failure count. Missing `shortcuts.json` = one failure with that message.
- [ ] **Step 2: Run, verify it fails**: menu item that calls `BuildAndCheck`. Expected: `FAIL ... 0 of 3 wave box spots`.
- [ ] **Step 3: Implement `AddWaveBoxSpots`** under the race parts root (next to the checkpoints in `AddRaceParts`): one GameObject `WaveBoxSpot n` per entry, positioned on the raycast hit, with `AddComponent<WaveBoxSpot>()`.
- [ ] **Step 4: Run `BuildAndCheck` again.** Expected: PASS, the report lists 3 spots and the shortcut sweep with 0 blockers. If the sweep names blockers: move them in the generator (Task 3 module or the module that owns the object) or nudge a VIA point by up to 3 m in Task 1's constants, rerun Tasks 1, 3, 4 and this step.
- [ ] **Step 5: Commit** `CampusWorldBuilder.cs` + the rebuilt scene/prefab (saved by the builder): `V19: wave box spots + shortcut self-test in the campus world`

### Task 7: Play test and hand-over

**Files:**
- Modify: `TASKS.md` (V19 -> in review), PR description with the measured times.

- [ ] **Step 1: Edit Mode suite** in Unity: all green.
- [ ] **Step 2: Play, three laps on each way**, write down lap times. Expected: no "Missed a checkpoint!", no WRONG WAY on red, the three turquoise boxes at the bottom of green give the Wave, the regular row after checkpoint 6 sits on green just after the fork (fine), red without wave about as fast as green with the wave used on the climb (target: within 1 s). Also compare a red lap with the medal targets and tell David if gold gets too easy.
- [ ] **Step 3: Web build** (`docs/builds.md`): console clean.
- [ ] **Step 4: Commit + push**, mark the PR ready, ask Noah to review (anchor) and tell the group "V19 ready, needs #<help PR> merged first".
