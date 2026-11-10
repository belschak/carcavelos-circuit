"""Terrain v2: shapes the coarse elevation data around the route so the track can be driven.

- Road bed: within the road (+1 m) the terrain takes the driving height (route.json track_z); outside it
  blends back to the original terrain over a shoulder. Cuttings (the tunnel ramp) get a short, steep shoulder.
- Tunnel cover: above the beach tunnel the terrain stays at least `tunnel_cover_m` above the tunnel floor.
- Seabed: sea cells go below sea level so the water plane at z = 0 shows.
Indoor and on-roof route parts (atrium, terrace deck) are skipped: buildings carry them.
"""
import json
import math
import os

import env_common as E

PROFILE = json.load(open(os.path.join(E.HERE, "route_profile.json")))
ROAD_HALF = E.ROUTE_WIDTH / 2 + 1.0       # flat part (m from the centreline)
SHOULDER = {"ramp": 9.0, "tunnel plaza": 10.0, "lower plaza": 14.0, "upper plaza": 14.0}
DEFAULT_SHOULDER = 12.0
CUT_ZONES = {"ramp", "tunnel plaza"}      # sunken approaches to the beach tunnel
SEA_FLOOR = -1.5
BEACH_MIN, BEACH_TOP = 0.3, 1.5    # beach height at the waterline and 30 m inland (m above the sea plane)


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


def coast_side(x, y):
    """Distance (m) to the nearest OSM coastline segment and whether (x, y) is on its land side."""
    best, land = None, True
    for line in E.DATA["coast"]:
        for (ax, ay), (bx, by) in zip(line, line[1:]):
            dx, dy = bx - ax, by - ay
            seg = dx * dx + dy * dy or 1e-9
            t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / seg))
            d = math.hypot(x - (ax + t * dx), y - (ay + t * dy))
            if best is None or d < best:
                best, land = d, (dx * (y - ay) - dy * (x - ax)) > 0
    return best, land


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


def apply():
    """Returns the edited height field and installs it in env_common (E.ground_z uses it from now on)."""
    original = E.DATA["grid"]["heights"]
    heights = [row[:] for row in original]
    pts = [p for p in E.route_points() if not p["tunnel"] and not p["building"] and p["zone"] != "terrace deck"]
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

    # shoreline along the real OSM coastline (land is on the left of the line): a beach that runs into
    # the water at z = 0, so the visible water edge follows the coast instead of the 5 m grid
    for j in range(E.ROWS):
        for i in range(E.COLS):
            x, y = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            if E.distance_to_route(x, y) < ROAD_HALF + 4:
                continue
            d, land = coast_side(x, y)
            h0 = heights[j][i]
            if land and d is not None and h0 < BEACH_TOP:
                # the DEM puts the wide Carcavelos beach at about 0 m, right on the water plane (z-fighting):
                # the beach rises from BEACH_MIN at the waterline to BEACH_TOP 30 m inland
                heights[j][i] = max(h0, BEACH_MIN + (BEACH_TOP - BEACH_MIN) * min(d, 30.0) / 30.0)
            elif d is None and E.DATA["grid"]["classes"][j][i] == "sand":
                heights[j][i] = max(h0, BEACH_MIN)
            if d is None or d > 40:
                if E.DATA["grid"]["classes"][j][i] == "sea":
                    heights[j][i] = min(heights[j][i], SEA_FLOOR)
                continue
            if not land:
                heights[j][i] = min(heights[j][i], -0.35 + (SEA_FLOOR + 0.35) * smoothstep(d / 10.0))
            elif d < 18 and heights[j][i] < 4.0:
                heights[j][i] = min(heights[j][i], 0.3 + d * 0.18)

    E.set_heightfield(heights)
    print(f"  terrain: {changed} vertices reshaped along the route, tunnel covered by {cover} m")
    return heights
