"""Tests for promenade_shortcut_plan.py (V19). Plain Python, no Blender:

    cd tools/world_gen && python3 test_promenade_shortcut.py
"""
import json
import math
import os
import unittest

import promenade_shortcut_plan as P

TRACK = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../Assets/_Project/Visuals/CampusWorld/Data/track.json")


def straight(n, step=2.0, y=0.0):
    """Fake route along +x, n points `step` apart, at height line `y`."""
    return [{"x": i * step, "z": y, "s": i * step} for i in range(n)]


class PlanTests(unittest.TestCase):
    def test_centreline_ends_and_step(self):
        line = P.centreline()
        self.assertEqual(line[0], P.SPLIT)
        self.assertEqual(line[-1], P.MERGE)
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
            h = (b[0] - a[0], b[1] - a[1])
            t = (P.NEXT_CHECKPOINT[0] - a[0], P.NEXT_CHECKPOINT[1] - a[1])
            cos = (h[0] * t[0] + h[1] * t[1]) / (math.hypot(*h) * math.hypot(*t))
            self.assertGreaterEqual(cos, 0.15)

    def test_progress_never_goes_back_on_shortcut(self):
        pts = json.load(open(TRACK))["points"]
        along = [P.progress(pts, x, y) for x, y in P.centreline()]
        self.assertTrue(all(b >= a - 0.5 for a, b in zip(along, along[1:])))

    def test_v19_track_has_no_checkpoint_on_fork(self):
        t = json.load(open(TRACK))
        self.assertEqual(P.check(t["points"], t["checkpoints"]), [])
        self.assertEqual(len(t["checkpoints"]), 12)

    def test_shortcuts_json_matches_plan(self):
        data = json.load(open(os.path.join(os.path.dirname(TRACK), "shortcuts.json")))
        s = data["shortcuts"][0]
        self.assertEqual(s["name"], "promenade straight")
        self.assertEqual(s["width"], 7.0)
        self.assertEqual(len(s["points"]), len(P.centreline()))
        self.assertEqual(len(data["wave_boxes"]), 3)

    def test_fence_free_opens_both_mouths(self):
        free = json.load(open(os.path.join(os.path.dirname(TRACK), "fence_free.json")))["free"]
        mouths = [f for f in free if "V19" in f["why"]]
        self.assertGreaterEqual(len(mouths), 2)
        self.assertTrue(all(f["side"] == -1 for f in mouths))


if __name__ == "__main__":
    unittest.main()
