"""The start plaza's race furniture as plain data (V21a, Luc's second free-roam drive of 13 Nov 2026, photos p1 / p2).

No Blender here: env_route_signs.py paints and builds from it, export_unity_bundle.py writes BARRIERS' polylines
into track.json, and a render or check script can read it too. Everything is derived from route.json, so it follows
the route if the start loop ever moves. Generator metres: x east, y north (track.json: x, z).

  grid_slots()   the ten start positions, exactly where RaceFlow/RacerGrid.cs puts the karts. V23 (Luc: "put 1 and 2
                 side by side, and in general all of them side by side"): pairs in rows, the front row centred on
                 the SpawnPoint (CampusWorldBuilder.AddRaceParts: track point spawn_index, facing Forward() = from
                 point -2 to point +2). Slot k (box number k + 1): row k // 2, GRID_SIDE m to the left (even k = odd
                 box number) or right (odd k), GRID_ROW m back per row. GRID_SIDE / GRID_ROW are read from
                 RaceSettings.asset (gridSidewaysMetres, gridRowMetres), so the paint moves with the numbers Role 4
                 sets there. slot_offset() is RacerGrid.OffsetOf() in Python: KEEP THE TWO IN SYNC.
  barriers()     centre lines of the Nova Kart walls package V21b builds in Unity (WhiteGuardrailBuilder reads
                 track.json "barriers"): the median between the westbound leg and the home straight, and the
                 outside of the U-turn.
"""
import json
import math
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
RACE_SETTINGS = os.path.join(REPO, "Assets", "_Project", "RaceFlow", "Settings", "RaceSettings.asset")

SLOTS = 10                              # Luc: "say 10 positions"
BOX_W, BOX_L, BOX_LINE = 1.7, 2.3, 0.12   # box around a kart of about 1.4 x 2.0 m, centred on the slot; painted line width
BOX_SIDE = 1.0                          # V23, F1 style: line across the front + side lines this long running back, open behind
SPAWN_BACK = 4                          # export_unity_bundle.unity_track: spawn = start - 4 points (8 m behind the line)

# --- barriers ---------------------------------------------------------------------------------------------------
WALL_T = 0.18                           # the Nova Kart wall's thickness (BRIEF_V21): for clearances only
UTURN_GAP = 1.0                         # m from the road's outer edge to the U-turn wall's centre line


def _route():
    return json.load(open(os.path.join(HERE, "route.json")))


def grid_numbers():
    """(sideways, row) metres from RaceSettings.asset; RacerGrid's own defaults (2, 3) if the file is not readable."""
    side, row = 2.0, 3.0
    try:
        text = open(RACE_SETTINGS).read()
        m = re.search(r"^\s*gridSidewaysMetres:\s*([-0-9.eE]+)", text, re.M)
        side = float(m.group(1)) if m else side
        m = re.search(r"^\s*gridRowMetres:\s*([-0-9.eE]+)", text, re.M)
        row = float(m.group(1)) if m else row
    except OSError:
        pass
    ok = lambda v: 0.1 < v < 50.0                   # RacerGrid.IsUsable
    return (side if ok(side) else 2.0), (row if ok(row) else 3.0)


def spawn_frame(route=None):
    """Spawn point (x, y), unit forward (fx, fy), unit right (rx, ry), track_z: the SpawnPoint's frame in Unity."""
    route = route or _route()
    pts, n = route["points"], len(route["points"])
    i = (route.get("start_index", 0) - SPAWN_BACK) % n
    a, b = pts[(i - 2) % n], pts[(i + 2) % n]                 # CampusWorldBuilder.Forward
    dx, dy = b["x"] - a["x"], b["y"] - a["y"]
    d = math.hypot(dx, dy)
    fx, fy = dx / d, dy / d
    return (pts[i]["x"], pts[i]["y"]), (fx, fy), (fy, -fx), pts[i]["track_z"], i


def slot_offset(slot, side, row):
    """RacerGrid.OffsetOf (Assets/_Project/RaceFlow/RacerGrid.cs), the one grid formula: (metres to the right, metres
    forward) of a slot seen from the SpawnPoint. Pairs side by side: even slot = left, odd slot = right, row = slot // 2.
    KEEP IN SYNC with the C# (its test RacerGridTests.SlotOffsets_AreTheTableTheGeneratorPaints has SLOT_TABLE_2_3)."""
    slot = max(slot, 0)
    return (-side if slot % 2 == 0 else side), -(slot // 2) * row


# slot_offset(k, 2, 3) for k = 0..9, the same table as in RacerGridTests.cs; checked at import so the two cannot drift
SLOT_TABLE_2_3 = [(-2, 0), (2, 0), (-2, -3), (2, -3), (-2, -6), (2, -6), (-2, -9), (2, -9), (-2, -12), (2, -12)]
assert [slot_offset(k, 2.0, 3.0) for k in range(10)] == SLOT_TABLE_2_3, "start_plan.slot_offset differs from RacerGrid"


def grid_slots(route=None):
    """[{slot, x, y, forward (fx, fy), right (rx, ry), track_z}] for slots 0 .. SLOTS - 1."""
    (sx, sy), f, r, z, _i = spawn_frame(route)
    side, row = grid_numbers()
    out = []
    for k in range(SLOTS):
        right, fwd = slot_offset(k, side, row)
        out.append({"slot": k, "x": sx + r[0] * right + f[0] * fwd, "y": sy + r[1] * right + f[1] * fwd,
                    "forward": f, "right": r, "track_z": z})
    return out


# ---------------------------------------------------------------------------------------------------------------
# Barriers (p1: "arrows and a barrier to the left, so you know where the home straight is")
# ---------------------------------------------------------------------------------------------------------------
TURN_STEP_DEG = 5.0                     # a route point belongs to a bend when the line turns more than this at it
MEDIAN_TURN_GAP = -2.0                  # m from the U-turn chord centre to the median's west end; -2 = 2 m past it, 4.1 m from the inner road edge (Luc: visible from point 667, turn stays open)
MEDIAN_POOL_GAP = 0.6                   # m between the median's east end and the entrance pool's rim (no kart fits)
POOL_RIM = 0.3                          # env_entrance: the pool's ring grown by its rim


def _bearing_step(pts, i):
    n = len(pts)
    a, b, c = pts[(i - 1) % n], pts[i % n], pts[(i + 1) % n]
    h1 = math.atan2(b["y"] - a["y"], b["x"] - a["x"])
    h2 = math.atan2(c["y"] - b["y"], c["x"] - b["x"])
    return math.degrees((h2 - h1 + math.pi) % (2 * math.pi) - math.pi)          # + = left turn


def uturn(route=None):
    """The left U-turn before the home straight: (first index, last index, centre (x, y), radius). Indices are
    route.json's and may be negative (counted back from the start line). The bend = the last run of points before
    the start whose turn per point exceeds TURN_STEP_DEG; centre = the middle of the chord between its ends."""
    route = route or _route()
    pts, start = route["points"], route.get("start_index", 0)
    i = start - 1
    while _bearing_step(pts, i) <= TURN_STEP_DEG:
        i -= 1
    last = i
    while _bearing_step(pts, i - 1) > TURN_STEP_DEG:
        i -= 1
    first = i
    n = len(pts)
    a, b = pts[(first - 1) % n], pts[(last + 1) % n]
    centre = ((a["x"] + b["x"]) / 2, (a["y"] + b["y"]) / 2)
    return first, last, centre, math.hypot(a["x"] - b["x"], a["y"] - b["y"]) / 2


def _unit(dx, dy):
    d = math.hypot(dx, dy)
    return dx / d, dy / d


def _ray_ring(o, d, ring):
    """Smallest t >= 0 with o + t d on the closed ring (None if the ray misses it)."""
    best = None
    for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]):
        ex, ey = bx - ax, by - ay
        den = d[0] * ey - d[1] * ex
        if abs(den) < 1e-9:
            continue
        t = ((ax - o[0]) * ey - (ay - o[1]) * ex) / den
        u = ((ax - o[0]) * d[1] - (ay - o[1]) * d[0]) / den
        if t >= 0 and 0 <= u <= 1 and (best is None or t < best):
            best = t
    return best


def median(route=None):
    """The straight wall between the two legs: from MEDIAN_TURN_GAP east of the U-turn's centre, along the bisector
    of the home straight and the westbound leg, to MEDIAN_POOL_GAP before the entrance pool's rim. [(x, y), (x, y)]."""
    route = route or _route()
    pts, n, start = route["points"], len(route["points"]), route.get("start_index", 0)
    first, last, c, _r = uturn(route)
    home = _unit(pts[start]["x"] - pts[(last + 1) % n]["x"], pts[start]["y"] - pts[(last + 1) % n]["y"])
    lead = pts[(first - 1 - (start - last - 1)) % n]            # as far before the bend as the start line is after it
    west = _unit(lead["x"] - pts[(first - 1) % n]["x"], lead["y"] - pts[(first - 1) % n]["y"])
    d = _unit(home[0] + west[0], home[1] + west[1])
    import campus_plan
    pool = list(campus_plan.A_POOL)
    t_pool = _ray_ring(c, d, pool)
    if t_pool is None:                                          # no pool on the line: as long as the home straight
        t_end = math.hypot(pts[start]["x"] - c[0], pts[start]["y"] - c[1])
    else:
        # the ray meets the pool's west side at an angle: step back until the wall's end is clear of the rim
        t_end = t_pool - POOL_RIM - MEDIAN_POOL_GAP
        while _ring_dist((c[0] + d[0] * t_end, c[1] + d[1] * t_end), pool) < POOL_RIM + MEDIAN_POOL_GAP:
            t_end -= 0.05
    return [(c[0] + d[0] * t, c[1] + d[1] * t) for t in (MEDIAN_TURN_GAP, t_end)]


def _seg_dist(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    ll = dx * dx + dy * dy
    t = 0.0 if ll == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / ll))
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)


def _ring_dist(p, ring):
    return min(_seg_dist(p, a, b) for a, b in zip(ring, ring[1:] + ring[:1]))


def road_clearance(x, y, route=None):
    """Metres from (x, y) to the race road's edge (negative = on the road)."""
    pts = (route or _route())["points"]
    n = len(pts)
    return min(_seg_dist((x, y), (pts[i]["x"], pts[i]["y"]), (pts[(i + 1) % n]["x"], pts[(i + 1) % n]["y"]))
               - pts[i].get("width", 8.0) / 2 for i in range(n))


def right_of(pts, i):
    """Unit vector to the right of the driving direction at point i, over 4 points like WhiteGuardrailBuilder.Forward."""
    n = len(pts)
    a, b = pts[(i - 2) % n], pts[(i + 2) % n]
    fx, fy = _unit(b["x"] - a["x"], b["y"] - a["y"])
    return fy, -fx


UTURN_LEAD, UTURN_TAIL = 3, 1           # route points the outside wall starts before / ends after the bend


def uturn_outside(route=None):
    """The wall around the outside (right) of the U-turn: the road's outer edge moved out by UTURN_GAP, from
    UTURN_LEAD points before the bend to UTURN_TAIL points after it. [(x, y), ...] in driving order."""
    route = route or _route()
    pts, n = route["points"], len(route["points"])
    first, last, _c, _r = uturn(route)
    out = []
    for i in range(first - UTURN_LEAD, last + UTURN_TAIL + 1):
        p = pts[i % n]
        rx, ry = right_of(pts, i)
        off = p.get("width", 8.0) / 2 + UTURN_GAP
        out.append((p["x"] + rx * off, p["y"] + ry * off))
    return out


def barriers(route=None):
    """[{name, points [(x, y), ...]}] in generator metres; heights come from the built world (env_route_signs)."""
    route = route or _route()
    return [{"name": "start_median", "points": median(route)},
            {"name": "uturn_outside", "points": uturn_outside(route)}]


if __name__ == "__main__":
    r = _route()
    f, l, c, rad = uturn(r)
    n = len(r["points"])
    print(f"U-turn: route points {f % n}..{l % n}, centre ({c[0]:.2f}, {c[1]:.2f}), radius {rad:.2f} m")
    for s in grid_slots(r):
        print(f"slot {s['slot']}: ({s['x']:.2f}, {s['y']:.2f})  road edge {-road_clearance(s['x'], s['y'], r):.2f} m inside")
    for b in barriers(r):
        cl = min(road_clearance(x, y, r) for x, y in b["points"])
        print(b["name"], [(round(x, 2), round(y, 2)) for x, y in b["points"]], f"min road-edge clearance of its points {cl:.2f} m")
