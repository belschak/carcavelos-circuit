"""The promenade shortcut (V19, 2026-11-12): Luc's red line behind Bar dos Gemeos, as numbers and checks.

Behind Bar dos Gemeos the lap dips down to the beach at Praia do Moinho and climbs back up (Luc's green line, the
race line since V14). The shortcut keeps straight on along the parking aisle instead: 131 m against 164 m. The
race line, the bots, the minimap and TrackProgress stay on green; three item boxes at the bottom of the dip always
give the Carcavelos Wave, so the long way pays back about what the short way saves (3 s at x1.6).

This file is plain Python (no Blender, no numpy), so the tests run anywhere and every other step reads one source:
  env_promenade_shortcut.py   paints the lane, clears the dressing off it, checks the ground (Blender)
  fence_free.py               opens the white fence at the two mouths (free_runs)
  Data/shortcuts.json         the lane and the wave box spots for Unity (CampusWorldBuilder), written by main()

Coordinates: route.json x/y = track.json x/z (Unity), origin lat 38.6780 / lon -9.3265. The points come from Luc's
Google Maps sketch, georeferenced on Bar dos Gemeos (about +-3 m).

    python3 tools/world_gen/promenade_shortcut_plan.py --check   # checks track.json, prints the fork
    python3 tools/world_gen/promenade_shortcut_plan.py           # the same, then writes Data/shortcuts.json
Rerun after fence_free.py (pipeline order in README.md).
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
TRACK = os.path.join(REPO, "Assets/_Project/Visuals/CampusWorld/Data/track.json")
SHORTCUTS = os.path.join(REPO, "Assets/_Project/Visuals/CampusWorld/Data/shortcuts.json")

SPLIT = (-173.36, -102.51)            # route point where the shortcut leaves the race line
MERGE = (-45.24, -122.35)             # route point where it comes back
VIA = [(-150.0, -110.0), (-130.0, -113.5), (-65.0, -114.0)]   # along the parking aisle, north of the planted berm
WIDTH = 7.0                           # m, lane width
STEP = 2.0                            # m between points, as the route
CLEAR = WIDTH / 2 + 1.0               # m from the centreline: no dressing, no fence
SNAP_LIMIT = 3.0                      # m: SPLIT/MERGE must be this close to a route point
WAVE_ANCHOR = (-123.16, -150.58)      # bottom of the green dip (the old checkpoint 6 anchor)
WAVE_COUNT = 3
WAVE_SPACING = 2.6                    # m, as ItemSettings.boxSpacing
NEXT_CHECKPOINT = (-27.26, -123.06)   # checkpoint 7 anchor: WrongWayDetector looks at it on the shortcut


def centreline():
    """SPLIT, VIA, MERGE with a point at least every STEP metres; the corners and both ends stay exact."""
    corners = [SPLIT] + VIA + [MERGE]
    line = [SPLIT]
    for a, b in zip(corners, corners[1:]):
        n = max(1, math.ceil(math.dist(a, b) / STEP))
        for k in range(1, n + 1):
            line.append(b if k == n else (a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n))
    return line


def length(line):
    return sum(math.dist(a, b) for a, b in zip(line, line[1:]))


def _seg_distance(x, y, a, b):
    """(distance from (x, y) to segment a-b, how far along it the nearest point is, 0..1)."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    l2 = dx * dx + dy * dy
    u = 0.0 if l2 == 0 else max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / l2))
    return math.hypot(x - a[0] - u * dx, y - a[1] - u * dy), u


def distance(x, y, line):
    """Flat metres from (x, y) to the polyline."""
    return min(_seg_distance(x, y, a, b)[0] for a, b in zip(line, line[1:]))


def _xy(points, key):
    return [(p["x"], p[key]) for p in points]


def _nearest(points, x, y, key):
    """(index, metres) of the route point nearest (x, y)."""
    return min(((i, math.hypot(px - x, py - y)) for i, (px, py) in enumerate(_xy(points, key))), key=lambda t: t[1])


def fork(points, key="z"):
    """Route indices where the shortcut leaves and rejoins. ValueError if an end is off the route or they swap."""
    a, da = _nearest(points, *SPLIT, key)
    b, db = _nearest(points, *MERGE, key)
    if da > SNAP_LIMIT or db > SNAP_LIMIT:
        raise ValueError(f"shortcut ends are {da:.1f} m / {db:.1f} m from the route (snap limit {SNAP_LIMIT} m)")
    if a >= b:
        raise ValueError(f"split index {a} is not before merge index {b}")
    return a, b


def check(points, checkpoints, key="z"):
    """Everything that would break the race, one plain sentence each. Empty list = fine."""
    try:
        a, b = fork(points, key)
    except ValueError as e:
        return [str(e)]
    errors = []
    green = points[b]["s"] - points[a]["s"]
    red = length(centreline())
    if red >= green:
        errors.append(f"the shortcut ({red:.0f} m) is not shorter than the race line ({green:.0f} m)")
    for n, c in enumerate(checkpoints):
        if a <= c <= b:
            errors.append(f"checkpoint {n} (index {c}) lies on the fork: move its anchor in checkpoint_anchors.json")
    return errors


def wave_spots(points, key="z"):
    """WAVE_COUNT spots across the road at the route point nearest WAVE_ANCHOR, WAVE_SPACING apart."""
    xy = _xy(points, key)
    i, _ = _nearest(points, *WAVE_ANCHOR, key)
    (ax, ay), (bx, by) = xy[i - 1], xy[(i + 1) % len(xy)]
    d = math.hypot(bx - ax, by - ay)
    sx, sy = -(by - ay) / d, (bx - ax) / d          # sideways (left of the driving direction)
    cx, cy = xy[i]
    mid = (WAVE_COUNT - 1) / 2
    return [(cx + sx * (k - mid) * WAVE_SPACING, cy + sy * (k - mid) * WAVE_SPACING) for k in range(WAVE_COUNT)]


def free_runs(n_pieces, fence_point, line):
    """(from, to) runs, from <= k < to, of fence pieces whose fence point is within CLEAR of `line`."""
    runs, start = [], None
    for k in range(n_pieces + 1):
        near = k < n_pieces and distance(*fence_point(k), line) < CLEAR
        if near and start is None:
            start = k
        elif not near and start is not None:
            runs.append((start, k))
            start = None
    return runs


def progress(points, x, y, key="z"):
    """Metres along the closed route to the nearest point on it, flat (TrackPolyline.DistanceAlongLap's rule)."""
    xy = _xy(points, key)
    best, along, run = None, 0.0, 0.0
    for i in range(len(xy)):
        a, b = xy[i], xy[(i + 1) % len(xy)]
        d, u = _seg_distance(x, y, a, b)
        seg = math.dist(a, b)
        if best is None or d < best:
            best, along = d, run + u * seg
        run += seg
    return along


def main(args):
    track = json.load(open(TRACK))
    points = track["points"]
    errors = check(points, track["checkpoints"])
    if not errors:
        a, b = fork(points)
        print(f"promenade shortcut: split {a}, merge {b}, red {length(centreline()):.0f} m, "
              f"green {points[b]['s'] - points[a]['s']:.0f} m")
    for e in errors:
        print("ERROR: " + e)
    if errors:
        return 1
    if "--check" not in args:
        out = {"note": "V19 promenade shortcut (tools/world_gen/promenade_shortcut_plan.py): the lane's centreline and "
                       "the wave box spots, Unity x/z; CampusWorldBuilder finds the heights by raycast.",
               "shortcuts": [{"name": "promenade straight", "width": WIDTH,
                              "points": [{"x": round(x, 2), "z": round(y, 2)} for x, y in centreline()]}],
               "wave_boxes": [{"x": round(x, 2), "z": round(y, 2)} for x, y in wave_spots(points)]}
        with open(SHORTCUTS, "w") as fh:
            json.dump(out, fh, indent=1)
        print("written:", os.path.relpath(SHORTCUTS, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
