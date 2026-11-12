"""Terrain v2: shapes the coarse elevation data around the route so the track can be driven.

- Road bed: within the road (+1 m) the terrain takes the driving height (route.json track_z); outside it
  blends back to the original terrain over a shoulder. Cuttings (the tunnel ramp) get a short, steep shoulder.
- Tunnel cover: above the beach tunnel the terrain stays at least `tunnel_cover_m` above the tunnel floor.
- Coast (coast.py, traced from the orthophoto): every point gets its signed distance to the traced waterline. Sea side:
  below the water (seabed). Beach zone (between waterline and backshore): open sand on the beach profile, which crosses
  z = 0 exactly on the waterline, and the sand cove KERB_DROP below the curved promenade. All other land: never below
  0.3 m, so no hollow inland can show the water plane.
Indoor and on-roof route parts (atrium, terrace deck) are skipped: buildings carry them.
"""
import json
import math
import os

import coast
import env_common as E

PROFILE = json.load(open(os.path.join(E.HERE, "route_profile.json")))
ROAD_HALF = E.ROUTE_WIDTH / 2 + 1.0       # flat part (m from the centreline)
SHOULDER = {"ramp": 9.0, "tunnel plaza": 10.0, "lower plaza": 14.0, "upper plaza": 14.0}
DEFAULT_SHOULDER = 12.0
CUT_ZONES = {"ramp", "tunnel plaza"}      # sunken approaches to the beach tunnel
LAND_MIN = 0.3            # m above the sea plane: the lowest any land point may lie (no inland hollow shows water)
BACKSHORE_BLEND = 10.0    # m: inside the beach zone the sand profile takes over from the terrain over this distance


def outside_portal(x, y, tunnel, k):
    """True if (x, y) lies in front of the tunnel (beyond the first or last tunnel point)."""
    if k == 0:
        a, b = tunnel[0], tunnel[1]
        return (x - a["x"]) * (b["x"] - a["x"]) + (y - a["y"]) * (b["y"] - a["y"]) < 0
    if k == len(tunnel) - 1:
        a, b = tunnel[-1], tunnel[-2]
        return (x - a["x"]) * (b["x"] - a["x"]) + (y - a["y"]) * (b["y"] - a["y"]) < 0
    return False


def portals():
    """[(point, inward unit direction)] for the south and north tunnel portal."""
    tunnel = [p for p in E.route_points() if p["tunnel"]]
    out = []
    for a, b in ((tunnel[0], tunnel[1]), (tunnel[-1], tunnel[-2])):
        d = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
        out.append((a, ((b["x"] - a["x"]) / d, (b["y"] - a["y"]) / d)))
    return out


def in_portal_hole(x, y):
    """Terrain faces around a portal mouth are left out; the tunnel's headwall and wing walls close the gap."""
    for p, (dx, dy) in portals():
        rx, ry = x - p["x"], y - p["y"]
        along = rx * dx + ry * dy                  # > 0 = inside the tunnel
        side = abs(-rx * dy + ry * dx)
        if 0.0 <= along <= 9.0 and side <= 10.0:
            return True
    return False


HOLE_BEYOND_EDGE = 6.0      # terrain faces this far beyond the road edge are removed in cuttings


def in_cut_hole(x, y):
    """Terrain faces in a cutting next to the road are left out; env_track's 45 degree walls and caps close it."""
    best, best_d = None, 1e9
    for p in E.route_points():
        if p["zone"] not in CUT_ZONES or p["tunnel"]:
            continue
        d = (p["x"] - x) ** 2 + (p["y"] - y) ** 2
        if d < best_d:
            best, best_d = p, d
    if best is None:
        return False
    d = math.sqrt(best_d)
    if d > E.ROUTE_WIDTH / 2 + 0.3 + HOLE_BEYOND_EDGE:
        return False
    return E.DATA["grid"]["heights"] and _orig_height(x, y) - best["track_z"] > 0.6


def _orig_height(x, y):
    h = E.DATA["grid"]["heights"]
    i = int(round(min(max((x - E.X0) / E.STEP, 0), E.COLS - 1)))
    j = int(round(min(max((y - E.Y0) / E.STEP, 0), E.ROWS - 1)))
    return h[j][i]


def smoothstep(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def _bilinear(h, x, y):
    fx = min(max((x - E.X0) / E.STEP, 0), E.COLS - 1.001)
    fy = min(max((y - E.Y0) / E.STEP, 0), E.ROWS - 1.001)
    i, j = int(fx), int(fy)
    ax, ay = fx - i, fy - j
    return (h[j][i] * (1 - ax) * (1 - ay) + h[j][i + 1] * ax * (1 - ay) + h[j + 1][i] * (1 - ax) * ay
            + h[j + 1][i + 1] * ax * ay)


_SHAPED = {}                # the route-shaped height field before the coast rule (promenade heights at the kerb)


def kerb_height(kx, ky):
    """Promenade height at a point of the cove kerb: the road where the route runs along it, else the terrain."""
    p = E.nearest_route_point(kx, ky)
    if not p["tunnel"] and math.hypot(p["x"] - kx, p["y"] - ky) < 14.0:
        return p["track_z"] - 0.05
    h = _SHAPED.get("h") or E.DATA["grid"]["heights"]
    return _bilinear(h, kx, ky)


def coast_height(x, y, z):
    """Terrain height at (x, y) after the coast rule, given the route-shaped height z there."""
    s = coast.signed_distance(x, y)
    if s < 0:                                               # sea: seabed, below the water plane
        return min(z, coast.beach_z(s))
    floor = min(coast.beach_z(s), LAND_MIN)
    if coast.in_cove(x, y):
        return max(coast.cove_z(x, y, kerb_height), floor)
    if coast.in_beach(x, y):                                # open sand (the rock shelves are meshes on top)
        target = coast.beach_z(s)
        t = 1.0 if s <= 4.0 else smoothstep(coast.backshore_distance(x, y) / BACKSHORE_BLEND)
        return max(z * (1 - t) + target * t, floor)
    return max(z, floor)                                    # other land: never in a hollow below the water


def apply():
    """Returns the edited height field and installs it in env_common (E.ground_z uses it from now on)."""
    original = E.DATA["grid"]["heights"]
    heights = [row[:] for row in original]
    import plaza_plan                     # V10i: the stairs and the upper plaza shape their own ground (env_plaza)
    pts = [p for p in E.route_points() if not p["tunnel"] and not p["building"] and p["zone"] != "terrace deck"
           and not plaza_plan.owns_route_point(p)]
    cells = {}
    for p in pts:
        cells.setdefault((int(p["x"] // 20), int(p["y"] // 20)), []).append(p)
    reach = ROAD_HALF + max(max(SHOULDER.values()), DEFAULT_SHOULDER)

    changed = 0
    for j in range(E.ROWS):
        for i in range(E.COLS):
            x, y = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            best, best_d = None, reach
            cx, cy = int(x // 20), int(y // 20)
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    for p in cells.get((cx + dx, cy + dy), []):
                        d = math.hypot(p["x"] - x, p["y"] - y)
                        if d < best_d:
                            best, best_d = p, d
            if best is None:
                continue
            shoulder = SHOULDER.get(best["zone"], DEFAULT_SHOULDER)
            if best_d <= ROAD_HALF:
                heights[j][i] = best["track_z"] - 0.05
            elif best_d <= ROAD_HALF + shoulder:
                t = smoothstep((best_d - ROAD_HALF) / shoulder)
                heights[j][i] = (best["track_z"] - 0.05) * (1 - t) + original[j][i] * t
            if best["zone"] in CUT_ZONES and best_d > E.ROUTE_WIDTH / 2:
                # cutting: never above a 45 degree plane rising from the road edge (concrete walls follow it)
                heights[j][i] = min(heights[j][i], best["track_z"] - 0.05 + (best_d - E.ROUTE_WIDTH / 2))
            changed += 1

    # keep the tunnel covered (the Avenida Marginal runs over it), but only between the two portals
    cover = PROFILE.get("tunnel_cover_m", 4.8)
    tunnel = [p for p in E.route_points() if p["tunnel"]]
    for j in range(E.ROWS):
        for i in range(E.COLS):
            x, y = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            k = min(range(len(tunnel)), key=lambda q: (tunnel[q]["x"] - x) ** 2 + (tunnel[q]["y"] - y) ** 2)
            near = tunnel[k]
            if math.hypot(near["x"] - x, near["y"] - y) >= 11.0 or outside_portal(x, y, tunnel, k):
                continue
            heights[j][i] = max(heights[j][i], near["track_z"] + cover)

    import env_plaza                      # V10i: nodes under and round the upper plaza, the courtyard, the stairs
    env_plaza.shape_heights(heights)

    import coast_plan                     # V16e: the berm by the promenade and the terrace west of the cove (land)
    coast_plan.shape_heights(heights, E.X0, E.Y0, E.STEP)
    # coast (coast.py): seabed, beach and cove from the traced waterline; land never below LAND_MIN
    _SHAPED["h"] = [row[:] for row in heights]
    beach = 0
    for j in range(E.ROWS):
        for i in range(E.COLS):
            x, y = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            if E.distance_to_route(x, y) < ROAD_HALF + 4 and not coast.in_cove(x, y):   # V16e: the cove's sand lies
                continue                                  # beyond the kerb (never under the road), also close to it
            heights[j][i] = coast_height(x, y, heights[j][i])
            beach += coast.in_beach(x, y)
    print(f"  coast: {beach} terrain vertices on the beach (waterline from coast_trace.json)")

    import env_streets                    # V10f: the sunken terrace of the two beach restaurants (survey 4.2)
    env_streets.lower_nodes(heights)
    import env_campus                     # V10c2: the lower plaza east of the race line is level (Floor 0)
    env_campus.lower_nodes(heights)
    import env_campus_west                # V16c: the grass berm in front of hall E (campus_west_plan.BERM_*)
    env_campus_west.raise_nodes(heights)

    import env_entrance                   # V18h: the curve plaza at the entrance is level (entrance_plan.level_weight)
    env_entrance.raise_nodes(heights)
    import env_north                      # V18j: the north side on one level (north_plan.level_target)
    env_north.level_nodes(heights)
    E.set_heightfield(heights)
    print(f"  terrain: {changed} vertices reshaped along the route, tunnel covered by {cover} m")
    return heights
