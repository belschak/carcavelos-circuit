"""Where the 3D grass tufts stand: a separate, small placement step. The trees and props (dressing.json) are not
touched.

Run after make_groundmap.py (it needs textures/ground_mask_data.png and ground_colour.png):
    python3 make_grass_tufts.py [out.json] [dressing.json] [ground_colour.png]
Output: out/grass_tufts.json (Unity axes: x east, z north; yaw in degrees; uniform scale; r, g, b = the colour of
the ground colour map under the tuft, so it blends with the lawn it stands on). CampusWorldBuilder puts each tuft
on the ground with a ray, builds the tufts as crossed cards (12 triangles; 24 for the "big" ones in the first 2 m
of the fringe), merges them into one mesh per 16 m cell
and hides each cell beyond about 65 m, so only the tufts near the kart are drawn. The run fails if the tufts break
the triangle budget (TOTAL_BUDGET in all, VISIBLE_BUDGET inside the cull distance at the worst point of the lap).

Tufts stand only on lawn (mask R), in three places:
  - along the track, 1 to 6 m outside the road edge: a fringe that is densest in the first 2 m and thins out,
    in clumps with gaps (a slow noise decides where grass grows thick);
  - around the feet of trees, benches, lamps and bins within 70 m of the track;
  - along building walls within 50 m of the track.
  - V16h: "far" tufts on every lawn up to FAR_REACH metres from the track: sparse, larger clumps of one cross of two
    cards (4 triangles), in their own 32 m cells that stay visible to FAR_CULL = 150 m, so lawns keep a broken
    silhouette and texture where the fine tufts are already hidden. They have their own budget (FAR_VISIBLE_BUDGET).
Never on the road, on a path, in a tunnel or inside a building: verify() checks every tuft again and the run fails
if one breaks a rule. dressing.json = Unity's Data/dressing.json or, by default, the placements in out/.
"""
import json
import math
import os
import random

import plaza_plan
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
DATA = json.load(open(os.path.join(HERE, "world_data.json")))
ROUTE = json.load(open(os.path.join(HERE, "route.json")))["points"]
X0, Y0, X1, Y1 = DATA["bounds"]
SEED = 20261110
LAWN_MIN = 0.85                 # mask R: full lawn only (open ground, sand and paved areas stay bare)
ROAD_GAP = 1.0                  # metres between the road edge and the nearest tuft
PATH_GAP = 0.5                  # same for every other road and footpath
EDGE_REACH = 6.0                # tufts along the track: up to this far outside the road edge
TREE_NEAR, WALL_NEAR = 70.0, 50.0
FEET = {"Veg_UmbrellaPine": (10, 0.55, 1.2), "Veg_Palm": (10, 0.75, 1.4), "Veg_Olive": (8, 0.5, 1.1),
        "Prop_Bench": (7, 0.9, 1.4), "Prop_Lamp": (4, 0.3, 0.6), "Prop_Bin": (3, 0.4, 0.6)}   # count, ring from, to (m)
EDGE_TRIES = 60                 # tries per route point (2 m) and side; lawn, clumps and distance thin them out
TRIS_SMALL, TRIS_BIG, CELL, CULL = 12, 24, 16.0, 65.0   # as in CampusWorldBuilder.AddGrassTufts
BIG_BAND = 2.0                  # tufts in the first metres of the fringe are fuller (24 triangles) and larger
TOTAL_BUDGET, VISIBLE_BUDGET = 150000, 60000    # triangles: all tufts / inside the cull distance at the worst point

# V16h: the far LOD
FAR_REACH, FAR_STEP, FAR_KEEP = 110.0, 2.4, 0.6     # metres from the road edge; grid of the candidates; share kept where grass is thick
TRIS_FAR, FAR_CELL, FAR_CULL = 4, 32.0, 150.0       # as in CampusWorldBuilder.AddGrassTufts
FAR_VISIBLE_BUDGET = 11000                          # triangles of far tufts inside FAR_CULL at the worst point

COLOUR_FILE = sys.argv[3] if len(sys.argv) > 3 else os.path.join(HERE, "textures", "ground_colour.png")
COLOUR = np.asarray(Image.open(COLOUR_FILE).convert("RGB"))
_NOISE_STEP = 2.6
_noise = np.random.default_rng(20261110).random((int((Y1 - Y0) / _NOISE_STEP) + 3, int((X1 - X0) / _NOISE_STEP) + 3))
MASK = np.asarray(Image.open(os.path.join(HERE, "textures", "ground_mask_data.png")).convert("RGB"), np.float32)[..., 0] / 255.0
RX = np.array([p["x"] for p in ROUTE]); RY = np.array([p["y"] for p in ROUTE])
RHW = np.array([p.get("width", 8.0) / 2 for p in ROUTE])
PATHS = [(np.array(p["points"], float), p["width"] / 2) for p in DATA["paths"] if not p["tunnel"] and len(p["points"]) > 1]
BUILDINGS = [np.array(b["points"], float) for b in DATA["buildings"]]


def lawn(x, y):
    h, w = MASK.shape
    i, j = int((x - X0) / (X1 - X0) * w), int((Y1 - y) / (Y1 - Y0) * h)
    return float(MASK[j, i]) if 0 <= i < w and 0 <= j < h else 0.0


def ground_colour(x, y):
    h, w = COLOUR.shape[:2]
    i, j = min(max(int((x - X0) / (X1 - X0) * w), 0), w - 1), min(max(int((Y1 - y) / (Y1 - Y0) * h), 0), h - 1)
    return [int(v) for v in COLOUR[j, i]]


def clump(x, y):
    """Slow noise 0..1 (about 2.6 m between values): where grass grows thick and where it leaves gaps."""
    fx, fy = (x - X0) / _NOISE_STEP, (y - Y0) / _NOISE_STEP
    i, j = int(fx), int(fy)
    ax, ay = fx - i, fy - j
    n = _noise
    return (n[j, i] * (1 - ax) + n[j, i + 1] * ax) * (1 - ay) + (n[j + 1, i] * (1 - ax) + n[j + 1, i + 1] * ax) * ay


def segment_distance(x, y, ax, ay, bx, by):
    """Distance from (x, y) to every segment a-b (arrays), and the index of the nearest one."""
    dx, dy = bx - ax, by - ay
    t = np.clip(((x - ax) * dx + (y - ay) * dy) / np.maximum(dx * dx + dy * dy, 1e-9), 0.0, 1.0)
    d = np.hypot(x - (ax + t * dx), y - (ay + t * dy))
    k = int(np.argmin(d))
    return float(d[k]), k


def route_clearance(x, y):
    """Metres between the point and the edge of the route's road (negative = on the road)."""
    d, k = segment_distance(x, y, RX, RY, np.roll(RX, -1), np.roll(RY, -1))
    return d - max(RHW[k], RHW[(k + 1) % len(RHW)])


def path_clearance(x, y):
    best = 1e9
    for pts, hw in PATHS:
        if abs(x - pts[:, 0]).min() > 40 and abs(y - pts[:, 1]).min() > 40:
            continue
        d, _k = segment_distance(x, y, pts[:-1, 0], pts[:-1, 1], pts[1:, 0], pts[1:, 1])
        best = min(best, d - hw)
    return best


def inside(x, y, poly):
    n, hit = len(poly), False
    for a in range(n):
        (x1, y1), (x2, y2) = poly[a], poly[(a + 1) % n]
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            hit = not hit
    return hit


def in_building(x, y):
    return any(b[:, 0].min() <= x <= b[:, 0].max() and b[:, 1].min() <= y <= b[:, 1].max() and inside(x, y, b)
               for b in BUILDINGS)


def allowed(x, y):
    """The rules for one tuft. Returns None if it may stand here, else the rule it breaks."""
    if lawn(x, y) < LAWN_MIN:
        return "not lawn"
    if route_clearance(x, y) < ROAD_GAP:
        return "road"
    if path_clearance(x, y) < PATH_GAP:
        return "path"
    if in_building(x, y):
        return "building"
    if plaza_plan.area(x, y, 1.0):
        return "plaza"                                      # V10i: the paved upper plaza, its courtyard and stairs
    return None


def load_dressing(path):
    """Trees and props as (prototype, x, y) in map axes, from Unity's dressing.json or the placements in out/."""
    if path and os.path.exists(path):
        return [(i["proto"], i["x"], i["z"]) for i in json.load(open(path))["items"]]
    items = []
    for name in ("placements_vegetation.json", "placements_props.json"):
        file = os.path.join(OUT, name)
        if os.path.exists(file):
            items += [(p["proto"], p["x"], p["y"]) for p in json.load(open(file))["placements"]]
    return items


def place(dressing):
    rng = random.Random(SEED)
    tufts, tried = [], 0

    def add(x, y, scale, big=0, far=0):
        nonlocal tried
        tried += 1
        x, y = round(x, 2), round(y, 2)                     # the numbers as written, so verify() sees the same
        if allowed(x, y) is None:
            r, g, b = ground_colour(x, y)
            tufts.append({"x": x, "z": y, "yaw": round(rng.uniform(0, 360), 1), "s": round(scale, 2), "r": r, "g": g, "b": b,
                          "big": big, "far": far})

    n = len(ROUTE)
    for i, p in enumerate(ROUTE):                           # along the track, denser close to the road edge
        if p["tunnel"] or p["building"]:
            continue
        # V10i: random numbers keyed on the place, not on the order of the lap: a route change in one section
        # leaves the tufts everywhere else exactly where they were
        rng = random.Random(f"{SEED}|route|{p['x']}|{p['y']}")
        a, b = ROUTE[i - 1], ROUTE[(i + 1) % n]
        tx, ty = b["x"] - a["x"], b["y"] - a["y"]
        length = math.hypot(tx, ty) or 1.0
        tx, ty = tx / length, ty / length
        for side in (-1, 1):
            for _ in range(EDGE_TRIES):
                out = rng.random() ** 2.0                               # 0 at the fringe .. 1 at EDGE_REACH: most stay close
                d = RHW[i] + ROAD_GAP + (EDGE_REACH - ROAD_GAP) * out
                along = rng.uniform(-1.0, 1.0)
                x, y = p["x"] + tx * along - ty * side * d, p["y"] + ty * along + tx * side * d
                thick = min(max(0.2 + 1.5 * (clump(x, y) - 0.33), 0.0), 1.0) * (1.0 - 0.6 * out)
                if rng.random() < thick:
                    near = d - RHW[i] - ROAD_GAP < BIG_BAND            # the fringe itself: fuller, taller, wider tufts
                    add(x, y, rng.uniform(1.0, 1.55) if near else rng.uniform(0.85, 1.3) * (1.0 - 0.2 * out), 1 if near else 0)
    for proto, x, y in dressing:                            # around the feet of trees, benches, lamps, bins
        rule = next((v for k, v in FEET.items() if proto.startswith(k)), None)
        if rule and route_clearance(x, y) < TREE_NEAR:
            rng = random.Random(f"{SEED}|feet|{proto}|{x}|{y}")
            count, r0, r1 = rule
            for _ in range(count):
                az, r = rng.uniform(0, 2 * math.pi), rng.uniform(r0, r1)
                add(x + math.cos(az) * r, y + math.sin(az) * r, rng.uniform(0.9, 1.4))
    for poly in BUILDINGS:                                  # along walls: just outside the footprint
        for a in range(len(poly)):
            (x1, y1), (x2, y2) = poly[a], poly[(a + 1) % len(poly)]
            length = math.hypot(x2 - x1, y2 - y1)
            if length < 1.0 or route_clearance((x1 + x2) / 2, (y1 + y2) / 2) > WALL_NEAR:
                continue
            rng = random.Random(f"{SEED}|wall|{x1}|{y1}|{x2}|{y2}")
            nx, ny = (y2 - y1) / length, -(x2 - x1) / length
            if inside((x1 + x2) / 2 + nx * 0.3, (y1 + y2) / 2 + ny * 0.3, poly):
                nx, ny = -nx, -ny
            for k in range(int(length / 0.6)):
                t = (k + rng.random()) * 0.6 / length
                off = rng.uniform(0.3, 0.7)
                add(x1 + (x2 - x1) * t + nx * off, y1 + (y2 - y1) * t + ny * off, rng.uniform(0.9, 1.3))
    # V16h: the far LOD: a jittered grid over the lawns round the lap, kept where the clump noise says grass is thick
    gx0, gy0 = math.floor((RX.min() - FAR_REACH) / FAR_STEP), math.floor((RY.min() - FAR_REACH) / FAR_STEP)
    gx1, gy1 = math.ceil((RX.max() + FAR_REACH) / FAR_STEP), math.ceil((RY.max() + FAR_REACH) / FAR_STEP)
    for gy in range(gy0, gy1):
        for gx in range(gx0, gx1):
            rng = random.Random(f"{SEED}|far|{gx}|{gy}")
            x, y = (gx + rng.random()) * FAR_STEP, (gy + rng.random()) * FAR_STEP
            if not (X0 < x < X1 and Y0 < y < Y1) or lawn(x, y) < LAWN_MIN:
                continue
            if rng.random() > FAR_KEEP * min(max(0.3 + 1.6 * (clump(x, y) - 0.3), 0.0), 1.0) / 0.8:
                continue
            c = route_clearance(x, y)
            if EDGE_REACH + 1.0 < c < FAR_REACH:
                add(x, y, rng.uniform(1.7, 2.5), far=1)
    return tufts, tried


def verify(tufts):
    """Every tuft against every rule again, from the written numbers. Returns the broken rules with counts."""
    broken = {}
    for t in tufts:
        why = allowed(t["x"], t["z"])
        if why:
            broken[why] = broken.get(why, 0) + 1
    return broken


def budget(tufts):
    """Triangles of all tufts, and of the tufts in the cells inside the cull distance at the worst route point."""
    cells, far = {}, {}
    for t in tufts:
        if t.get("far"):
            key = (math.floor(t["x"] / FAR_CELL), math.floor(t["z"] / FAR_CELL))
            far[key] = far.get(key, 0) + TRIS_FAR
            continue
        key = (math.floor(t["x"] / CELL), math.floor(t["z"] / CELL))
        cells[key] = cells.get(key, 0) + (TRIS_BIG if t["big"] else TRIS_SMALL)
    worst, worst_far = (0, 0.0), (0, 0.0)
    for p in ROUTE[::2]:
        n = sum(c for (i, j), c in cells.items()
                if math.hypot((i + 0.5) * CELL - p["x"], (j + 0.5) * CELL - p["y"]) < CULL + CELL * 0.7)
        worst = max(worst, (n, p["s"]))
        n = sum(c for (i, j), c in far.items()
                if math.hypot((i + 0.5) * FAR_CELL - p["x"], (j + 0.5) * FAR_CELL - p["y"]) < FAR_CULL + FAR_CELL * 0.7)
        worst_far = max(worst_far, (n, p["s"]))
    return sum(cells.values()), worst[0], worst[1], len(cells), sum(far.values()), worst_far[0], worst_far[1], len(far)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(OUT, "grass_tufts.json")
    tufts, tried = place(load_dressing(sys.argv[2] if len(sys.argv) > 2 else None))
    broken = verify(tufts)
    clear = min(route_clearance(t["x"], t["z"]) for t in tufts)
    print(f"grass tufts: {len(tufts)} placed of {tried} tried; nearest to the road edge {clear:.2f} m; "
          f"rule check: {sum(broken.values())} problems {broken or ''}")
    total, visible, at, cells, far_total, far_visible, far_at, far_cells = budget(tufts)
    print(f"  triangles: {total} in all (budget {TOTAL_BUDGET}), {visible} inside the cull distance at the worst point "
          f"(s = {at:.0f} m, budget {VISIBLE_BUDGET}); {cells} cells")
    print(f"  far LOD (V16h): {far_total // TRIS_FAR} tufts, {far_total} triangles in all, {far_visible} inside {FAR_CULL:.0f} m at the "
          f"worst point (s = {far_at:.0f} m, budget {FAR_VISIBLE_BUDGET}); {far_cells} cells of {FAR_CELL:.0f} m")
    if broken or total > TOTAL_BUDGET or visible > VISIBLE_BUDGET or far_visible > FAR_VISIBLE_BUDGET:
        sys.exit(1)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    json.dump({"note": "Unity axes (x east, z north); yaw in degrees; s = uniform scale; r, g, b = ground colour under "
                       "the tuft (sRGB 0-255); big = 1: fuller tuft at the track edge; far = 1: far LOD clump (one cross, own cells). Height comes from a ray onto the ground in CampusWorldBuilder.", "tufts": tufts}, open(out, "w"), indent=0)
    print("  ->", out)


if __name__ == "__main__":
    main()
