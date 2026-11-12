"""V18k (Luc, 12 Nov: "as on Google: lawn"): where the race road crosses real lawn, the road shows the lawn.

The road takes the terrain's own material there (the ground colour map, world UVs, as env_north does behind the
hall), and the ground colour map is lawn under those road quads, with the real paths painted back on top. So the
lawn and the real 2.8 m path run on across the 8 m road; nothing moves, the road keeps its collider and height.
Sources: Google satellite 20z/21z and the orthophoto (reports/V18k-roads-survey.md items 4 and 5).
"""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
RUNS = ((82, 124),)     # west lawn below the CTT terrace, to the avenue sidewalk (survey item 4). The cove lawn
                        # (item 5) needs coast_plan's ground layers changed first: they paint over this lawn.


def on_run(i):
    return any(a <= i <= b for a, b in RUNS)


def road_look(x, y):
    """env_track.ROAD_LOOK hook for RUNS: the terrain's own material (lawn and the painted paths)."""
    import env_north
    return env_north.road_look_lawn()


def road_quads(route):
    """Every road quad of RUNS with its shoulders (4.95 m either side), 0.6 m over both ends (0.2 m map pixels)."""
    n = len(route)
    out = []
    for a0, b0 in RUNS:
        for i in range(a0, b0 + 1):
            a, b = route[i], route[(i + 1) % n]
            d = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
            tx, ty = (b["x"] - a["x"]) / d, (b["y"] - a["y"]) / d
            ends = []
            for p, q, r, along in ((a, route[i - 1], b, -0.6), (b, a, route[(i + 2) % n], 0.6)):
                dd = math.hypot(r["x"] - q["x"], r["y"] - q["y"]) or 1.0
                nx, ny = -(r["y"] - q["y"]) / dd, (r["x"] - q["x"]) / dd
                ends.append(((p["x"] + tx * along + nx * 4.95, p["y"] + ty * along + ny * 4.95),
                             (p["x"] + tx * along - nx * 4.95, p["y"] + ty * along - ny * 4.95)))
            out.append([ends[0][0], ends[1][0], ends[1][1], ends[0][1]])
    return out


def ground_paint(paint, lawn):
    """Lawn under the road quads of RUNS, then the real paths that cross them painted back on top."""
    import campus_west_plan as W
    route = json.load(open(os.path.join(HERE, "route.json")))["points"]
    for k, quad in enumerate(road_quads(route)):
        paint(f"lawn under the race road {k} (V18k)", quad, lawn, 255)
    paint("pale lawn path across the race road (V18k)", W.strip(W.LAWN_PATH, W.LAWN_PATH_W), W.PALE_PATH)
