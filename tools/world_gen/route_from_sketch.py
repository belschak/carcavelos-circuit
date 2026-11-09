"""Turns the hand-drawn route (red line on the top preview) into a route in world metres.

Input : sketch/route_sketch_2026-11-08.webp  (screenshot of out/preview_top.png with red route,
        green jump mark, blue direction arrows)
Output: route.json (point every 2 m with ground height and flags), out/route_check.png, out/route_profile.png
Run with the media-tools Python after world_data.py.
"""
import json
import math
import os

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.path import Path
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SKETCH = os.path.join(HERE, "sketch", "route_sketch_2026-11-08.webp")
DATA = json.load(open(os.path.join(HERE, "world_data.json")))

# The preview camera (build_world.py, shot "top"): ortho 816 m wide, 1600 x 1100 px, centred on (-70, -15).
RENDER_W, M_PER_PX, LEFT_M, TOP_M = 1600, 816 / 1600, -478.0, 265.5
TUNNEL_START = (0.0, -110.0)      # route is walked from here, heading north through the beach tunnel
SPACING = 2.0                     # metres between route points


def sketch_strokes():
    im = Image.open(SKETCH).convert("RGB")
    w, h = im.size
    px = im.load()

    def dark_row(y):
        return sum(1 for x in range(0, w, 10) if sum(px[x, y]) < 60) > 0.95 * (w / 10)

    top = next(y for y in range(h) if not dark_row(y))      # letterbox bar above the image
    scale = w / RENDER_W

    def to_m(sx, sy):
        return LEFT_M + sx / scale * M_PER_PX, TOP_M - (sy - top) / scale * M_PER_PX

    red, green = [], []
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if r > 180 and g < 90 and b < 90:
                red.append(to_m(x, y))
            elif g > 150 and g - r > 70 and g - b > 70:
                green.append((x, y))
    # green = the word JUMP plus a scribble under it: the scribble (lower part) marks the spot
    ys = sorted(p[1] for p in green)
    cut = ys[0] + (ys[-1] - ys[0]) * 0.6
    scribble = [to_m(x, y) for x, y in green if y >= cut]
    jump = (sum(p[0] for p in scribble) / len(scribble), sum(p[1] for p in scribble) / len(scribble))
    return red, jump


def thin(points, cell=2.5):
    cells = {}
    for x, y in points:
        cells.setdefault((int(x // cell), int(y // cell)), []).append((x, y))
    return [(sum(p[0] for p in v) / len(v), sum(p[1] for p in v) / len(v)) for v in cells.values()]


def order_loop(points, start, clear_radius=3.5, search_radius=12.0):
    """Walks along the drawn line: clears everything within the line width around the current spot,
    then steps to the nearest remaining point that lies ahead (never back). Returns the path and the
    number of points it never reached."""
    rest = set(points)
    current = min(rest, key=lambda p: math.dist(p, start))
    path, heading = [current], None
    while True:
        rest -= {p for p in rest if math.dist(p, current) <= clear_radius}
        ahead = [p for p in rest if math.dist(p, current) <= search_radius and
                 (heading is None or (p[0] - current[0]) * heading[0] + (p[1] - current[1]) * heading[1] > 0)]
        if not ahead:
            break
        nxt = min(ahead, key=lambda p: math.dist(p, current))
        d = math.dist(nxt, current)
        heading = ((nxt[0] - current[0]) / d, (nxt[1] - current[1]) / d)
        path.append(nxt)
        current = nxt
    return path, len(rest)


def smooth_closed(path, window=4, passes=3):
    n = len(path)
    for _ in range(passes):
        path = [(sum(path[(i + k) % n][0] for k in range(-window, window + 1)) / (2 * window + 1),
                 sum(path[(i + k) % n][1] for k in range(-window, window + 1)) / (2 * window + 1)) for i in range(n)]
    return path


def closest_on_polyline(p, line):
    """Closest point on a polyline, its distance, and how far along the line it is (metres)."""
    best, along, walked = (None, float("inf"), 0.0), 0.0, 0.0
    for a, b in zip(line, line[1:]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        seg = math.hypot(dx, dy) or 1e-9
        t = max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / (seg * seg)))
        q = (a[0] + t * dx, a[1] + t * dy)
        if math.dist(p, q) < best[1]:
            best, along = (q, math.dist(p, q)), walked + t * seg
        walked += seg
    return best[0], best[1], along


def snap_to_line(route, line, full=8.0, none=14.0):
    """Pulls the drawn route onto a real line (the beach tunnel): fully within `full` metres,
    fading out until `none` metres, so the route enters and leaves the tunnel smoothly."""
    out = []
    for p in route:
        q, d, _ = closest_on_polyline(p, line)
        w = 1.0 if d <= full else max(0.0, (none - d) / (none - full))
        out.append((p[0] + (q[0] - p[0]) * w, p[1] + (q[1] - p[1]) * w))
    return out


def resample_closed(path, spacing):
    loop = [p for k, p in enumerate(path + [path[0]]) if k == 0 or math.dist(p, path[k - 1] if k < len(path) else path[-1]) > 1e-6]
    out, carry = [loop[0]], 0.0
    for a, b in zip(loop, loop[1:]):
        seg = math.dist(a, b)
        if seg < 1e-6:
            continue
        d = spacing - carry
        while d <= seg:
            t = d / seg
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            d += spacing
        carry = seg - (d - spacing)
    return out[:-1] if math.dist(out[-1], out[0]) < spacing / 2 else out


GRID = DATA["grid"]
X0, Y0, X1, Y1 = DATA["bounds"]


def ground_z(x, y):
    step, cols, rows, hts = GRID["step"], GRID["cols"], GRID["rows"], GRID["heights"]
    fx = min(max((x - X0) / step, 0), cols - 1.001)
    fy = min(max((y - Y0) / step, 0), rows - 1.001)
    i, j = int(fx), int(fy)
    ax, ay = fx - i, fy - j
    return (hts[j][i] * (1 - ax) * (1 - ay) + hts[j][i + 1] * ax * (1 - ay)
            + hts[j + 1][i] * (1 - ax) * ay + hts[j + 1][i + 1] * ax * ay)


def dist_to_polyline(p, line):
    best = float("inf")
    for a, b in zip(line, line[1:]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        t = max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / ((dx * dx + dy * dy) or 1)))
        best = min(best, math.dist(p, (a[0] + t * dx, a[1] + t * dy)))
    return best


PROFILE = json.load(open(os.path.join(HERE, "route_profile.json")))

CLEARANCE = 5.5        # wanted distance from the road centreline to a building wall (8 m road + 1.5 m to spare)


def ray_exit(o, d, poly):
    """Distance from o along direction d to the first polygon edge (inf if none)."""
    best = float("inf")
    (ox, oy), (dx, dy) = o, d
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        ex, ey = bx - ax, by - ay
        den = dx * ey - dy * ex
        if abs(den) < 1e-12:
            continue
        t = ((ax - ox) * ey - (ay - oy) * ex) / den
        u = ((ax - ox) * dy - (ay - oy) * dx) / den
        if t > 1e-6 and 0.0 <= u <= 1.0:
            best = min(best, t)
    return best


def wall_blockers(route, beach_tunnel, tunnel_len):
    """Buildings that count as walls for each route point, and the points that must not move.

    Not walls / not moved: the beach tunnel, the drive-through passages (runs of 4+ points inside a
    drive-through building; Student Central is crossed in two short runs) with 12 m either side, the terrace
    deck, and open roofs (osm building=roof, a kart passes under them).
    Returns (polygons, keep flags, per-point set of passable polygon indices)."""
    n = len(route)
    polys = [(b, Path(b["points"]), [tuple(q) for q in b["points"]]) for b in DATA["buildings"]
             if b["osm_building"] != "roof"]
    inside = [[k for k, (b, path, _) in enumerate(polys) if path.contains_point(tuple(p))] for p in route]
    keep = [False] * n
    passable = [set() for _ in range(n)]
    for i, (x, y) in enumerate(route):
        _, d_beach, along = closest_on_polyline((x, y), beach_tunnel)
        if d_beach < 3.0 and -10 < along < tunnel_len + 10:
            keep[i] = True
    for k, (b, path, _) in enumerate(polys):
        if b.get("drive_on_roof"):
            for i in range(n):
                if k in inside[i]:
                    for j in range(i - 2, i + 3):
                        keep[j % n] = True
                        passable[j % n].add(k)
        if not b.get("drive_through"):
            continue
        run = []
        for i in list(range(n)) + [0]:
            if k in inside[i % n]:
                run.append(i % n)
                continue
            if len(run) >= 4:                               # a real passage (8 m+), not a corner clip
                for j in range(run[0] - 6, run[-1] + 7):
                    keep[j % n] = True
                    passable[j % n].add(k)
            run = []
    return polys, keep, passable


def wall_distances(route, i, polys, passable, frame):
    """(left, right, push): distances to the nearest wall on each side; push if the point is inside a wall."""
    x, y = route[i]
    nx, ny = frame
    left = right = float("inf")
    for k, (b, path, poly) in enumerate(polys):
        if k in passable[i]:
            continue
        xs = [q[0] for q in poly]
        ys = [q[1] for q in poly]
        if x < min(xs) - 40 or x > max(xs) + 40 or y < min(ys) - 40 or y > max(ys) + 40:
            continue
        if path.contains_point((x, y)):                    # inside a wall: leave through the nearer side
            el, er = ray_exit((x, y), (nx, ny), poly), ray_exit((x, y), (-nx, -ny), poly)
            return left, right, ((el + CLEARANCE) if el < er else -(er + CLEARANCE))
        left = min(left, ray_exit((x, y), (nx, ny), poly))
        right = min(right, ray_exit((x, y), (-nx, -ny), poly))
    return left, right, None


def left_normals(route):
    n = len(route)
    out = []
    for i in range(n):
        a, b = route[i - 1], route[(i + 1) % n]
        tx, ty = b[0] - a[0], b[1] - a[1]
        d = math.hypot(tx, ty) or 1.0
        out.append((-ty / d, tx / d))
    return out


def clear_of_walls(route, beach_tunnel, tunnel_len, passes=((3, 6),) * 6 + ((1, 2),) * 4):
    """Bends the route sideways so building walls stay CLEARANCE m from the centreline.

    Where two walls are closer together than 2 x CLEARANCE (the alley between Edificio Principal and the
    library) the route runs down the middle instead; road_widths() then narrows the road there. What doesn't
    move: see wall_blockers(). Each pass measures, spreads the push over the neighbours and smooths it, so the
    line bends gently; several passes converge on the clearance.
    Returns (new route, report lines)."""
    n = len(route)
    route = [list(p) for p in route]
    polys, keep, passable = wall_blockers(route, beach_tunnel, tunnel_len)
    moved_max = 0.0
    for spread_w, smooth_w in passes:              # coarse passes bend gently, fine passes centre the alley
        frames, off = left_normals(route), [0.0] * n
        for i in range(n):
            if keep[i]:
                continue
            left, right, push = wall_distances(route, i, polys, passable, frames[i])
            if push is not None:
                off[i] = push
            elif left < CLEARANCE or right < CLEARANCE:
                if left + right >= 2 * CLEARANCE:
                    off[i] = -(CLEARANCE - left) if left < right else (CLEARANCE - right)
                else:
                    off[i] = (left - right) / 2           # alley: down the middle
        if max(abs(o) for o in off) < 0.05:
            break
        spread = [max((off[(i + k) % n] for k in range(-spread_w, spread_w + 1)), key=abs) for i in range(n)]
        smooth = [sum(spread[(i + k) % n] for k in range(-smooth_w, smooth_w + 1)) / (2 * smooth_w + 1) for i in range(n)]
        for i in range(n):
            if keep[i]:
                continue
            route[i][0] += frames[i][0] * smooth[i]
            route[i][1] += frames[i][1] * smooth[i]
            moved_max = max(moved_max, abs(smooth[i]))
    return [tuple(p) for p in route], [f"wall clearance: line moved up to {moved_max:.1f} m sideways"]



MAX_WIDTH, MIN_WIDTH, WALL_GAP = 8.0, 5.5, 0.4   # road width (m); in alleys the edge stays WALL_GAP off the wall


def road_widths(route, beach_tunnel, tunnel_len):
    """Road width per point: 8 m, narrower where walls on both sides leave less room (the alley)."""
    n = len(route)
    polys, keep, passable = wall_blockers(route, beach_tunnel, tunnel_len)
    frames = left_normals(route)
    raw = []
    for i in range(n):
        if keep[i]:
            raw.append(MAX_WIDTH)
            continue
        left, right, push = wall_distances(route, i, polys, passable, frames[i])
        room = 2 * (min(left, right) - WALL_GAP)
        raw.append(MAX_WIDTH if push is None and room >= MAX_WIDTH else max(MIN_WIDTH, min(MAX_WIDTH, room)))
    narrow = [min(raw[(i + k) % n] for k in range(-3, 4)) for i in range(n)]       # start narrowing early
    return [round(sum(narrow[(i + k) % n] for k in range(-3, 4)) / 7, 2) for i in range(n)]


def apply_profile(points, buildings):
    """Sets track_z (driving height), zone, surface and level on every route point.

    Fixed heights: the tunnel floor, the upper campus level (from the top of the terracotta steps through the
    atrium to the edge of the terrace deck) and the anchors in route_profile.json. Fixed points closer than
    link_gap_m are joined by straight ramps; longer free stretches follow the smoothed terrain and blend into
    the fixed heights. The terrace edge is a hard drop (the jump), so nothing blends across it.
    Returns (jump index, report lines)."""
    n, step = len(points), SPACING
    idx = lambda s: int(round(s / step)) % n

    def place(entry, key_s, key_at):
        """Index of a profile entry: by position ('at', nearest route point) if given, else by s."""
        if entry.get(key_at):
            ax, ay = entry[key_at]
            return min(range(n), key=lambda i: (points[i]["x"] - ax) ** 2 + (points[i]["y"] - ay) ** 2)
        return idx(entry[key_s])
    raw = [p["ground_z"] for p in points]
    w = max(1, int(PROFILE["smooth_m"] / step / 2))
    smooth = [sum(raw[(i + k) % n] for k in range(-w, w + 1)) / (2 * w + 1) for i in range(n)]
    fixed = {}
    zone = [""] * n

    t = PROFILE["tunnel_floor"]
    a, b = idx(t["s_from"]), idx(t["s_to"])
    for i in range(a, b + 1):
        f = (i - a) / max(b - a, 1)
        fixed[i] = t["z_from"] + (t["z_to"] - t["z_from"]) * f
        zone[i] = "tunnel"

    deck = next(((bd, path) for bd, path in buildings if bd.get("drive_on_roof") and bd.get("roof_z") is not None), None)
    edge = None
    if deck:
        on_deck = [i for i, p in enumerate(points) if deck[1].contains_point((p["x"], p["y"]))]
        if on_deck:
            edge = on_deck[-1]
            up = PROFILE["upper_level"]
            for i in range(place(up, "from_s", "from_at"), edge + 1):
                fixed[i] = up["z"]
                zone[i] = "upper plaza"
            for i in on_deck:
                zone[i] = "terrace deck"
    for anchor in PROFILE["anchors"]:
        i = place(anchor, "s", "at")
        fixed[i] = anchor["z"]
        zone[i] = anchor.get("zone", "")
    bounds = [(place(sf, "from_s", "from_at"), sf["surface"]) for sf in PROFILE["surfaces"]]
    surface_at = [None] * n
    for k, (start, name) in enumerate(bounds):
        end = bounds[(k + 1) % len(bounds)][0]
        i = start
        while i != end:
            surface_at[i] = name
            i = (i + 1) % n

    keys = sorted(fixed)
    z = smooth[:]
    for i in keys:
        z[i] = fixed[i]
    link, blend = PROFILE["link_gap_m"] / step, PROFILE["blend_m"] / step
    for a, b in zip(keys, keys[1:] + [keys[0] + n]):
        gap = b - a
        if gap <= 1:
            continue
        hard_drop = a == edge                              # never smooth across the jump
        za, zb = fixed[a % n], fixed[b % n]
        for k in range(1, gap):
            i = (a + k) % n
            if gap <= link and not hard_drop:
                z[i] = za + (zb - za) * k / gap
                flat_zone = zone[a % n] if k < gap / 2 else zone[b % n]
                zone[i] = zone[i] or ("ramp" if abs(zb - za) > 1.0 else flat_zone)
            else:
                wa = 0.0 if hard_drop else max(0.0, 1 - k / blend)
                wb = max(0.0, 1 - (gap - k) / blend)
                total = wa + wb
                if total > 1:
                    wa, wb = wa / total, wb / total
                z[i] = smooth[i] * (1 - wa - wb) + za * wa + zb * wb

    for i, p in enumerate(points):
        p["track_z"] = round(z[i], 2)
        if p["building"] and zone[i] == "upper plaza":
            zone[i] = "atrium"                             # the indoor part of the upper level
        if not zone[i]:
            zone[i] = "coastal road" if p["main_road"] else ("promenade" if p["ground_z"] < 5 else "lawn and paths")
        p["zone"] = zone[i]
        p["level"] = "terrace deck" if zone[i] == "terrace deck" else ""
        p["surface"] = surface_at[i] or "asphalt"

    notes = []
    worst = max(((abs(z[(i + 1) % n] - z[i]) / step, i) for i in range(n) if i != edge), key=lambda g: g[0])
    notes.append(f"steepest slope {worst[0] * 100:.0f} % at s = {points[worst[1]]['s']:.0f} m ({points[worst[1]]['zone']})")
    for i in range(n):
        g = abs(z[(i + 1) % n] - z[i]) / step
        if i != edge and g > PROFILE["max_grade_warn"]:
            notes.append(f"  warning: {g * 100:.0f} % at s = {points[i]['s']:.0f} m")
    if edge is not None:
        notes.append(f"upper level {PROFILE['upper_level']['z']} m from s = {PROFILE['upper_level']['from_s']} to "
                     f"{points[edge]['s']:.0f} m, drop at the terrace edge {z[edge] - z[(edge + 1) % n]:.1f} m")
    return (edge if edge is not None else 0), notes


def main():
    red, jump_mark = sketch_strokes()
    path, left_over = order_loop(thin(red), TUNNEL_START)
    route = resample_closed(smooth_closed(path), SPACING)
    # drive direction: from the beach heading north into the tunnel (blue arrows)
    k = min(range(len(route)), key=lambda i: math.dist(route[i], TUNNEL_START))
    if route[(k + 5) % len(route)][1] < route[k][1]:
        route.reverse()
    k = min(range(len(route)), key=lambda i: math.dist(route[i], TUNNEL_START))
    route = route[k:] + route[:k]               # point 0 = in front of the beach tunnel

    # the sketch passes 5-10 m beside the real tunnel: put that part exactly on the tunnel axis
    beach_tunnel = next(p["points"] for p in DATA["paths"] if p["tunnel"] and p["name"] == "Sagres Beach Way")
    route = resample_closed(smooth_closed(snap_to_line(route, beach_tunnel), window=2, passes=1), SPACING)
    k = min(range(len(route)), key=lambda i: math.dist(route[i], beach_tunnel[0]))
    route = route[k:] + route[:k]               # point 0 = south portal of the beach tunnel
    tunnel_len = sum(math.dist(a, b) for a, b in zip(beach_tunnel, beach_tunnel[1:]))
    route, clear_notes = clear_of_walls(route, beach_tunnel, tunnel_len)
    route = resample_closed(route, SPACING)
    k = min(range(len(route)), key=lambda i: math.dist(route[i], beach_tunnel[0]))
    route = route[k:] + route[:k]               # point 0 stays the south portal
    widths = road_widths(route, beach_tunnel, tunnel_len)
    narrow = [i for i, w in enumerate(widths) if w < MAX_WIDTH - 0.05]
    if narrow:
        clear_notes.append(f"road narrower than {MAX_WIDTH:.0f} m at {len(narrow)} points, down to {min(widths):.1f} m "
                           f"(s {narrow[0] * SPACING:.0f}-{narrow[-1] * SPACING:.0f} m)")

    tunnels = [p["points"] for p in DATA["paths"] if p["tunnel"]]
    primaries = [p for p in DATA["paths"] if p["type"] in ("primary", "primary_link")]
    buildings = [(b, Path(b["points"])) for b in DATA["buildings"]]
    jump_i = min(range(len(route)), key=lambda i: math.dist(route[i], jump_mark))
    points = []
    for i, (x, y) in enumerate(route):
        inside = [b["name"] or b["id"] for b, path in buildings if path.contains_point((x, y))]
        _, d_beach, along = closest_on_polyline((x, y), beach_tunnel)
        in_tunnel = (d_beach < 2.5 and 0.5 < along < tunnel_len - 0.5) or any(dist_to_polyline((x, y), t) < 1.0 for t in tunnels)
        on_main_road = any(dist_to_polyline((x, y), r["points"]) < r["width"] / 2 + 1 for r in primaries)
        points.append({"i": i, "x": round(x, 2), "y": round(y, 2), "ground_z": round(ground_z(x, y), 2),
                       "s": round(i * SPACING, 1), "width": widths[i], "building": inside[0] if inside else "",
                       "tunnel": in_tunnel, "main_road": on_main_road and not in_tunnel})

    # ---------- driving height (track_z), zones and surfaces ----------
    jump_sketch_i = jump_i
    jump_i, notes = apply_profile(points, buildings)
    notes = clear_notes + notes
    for line in notes:
        print(line)
    landing = points[(jump_i + 1) % len(points)]
    length = len(points) * SPACING
    result = {"source": "Luc's sketch on preview_top.png, 2026-11-08", "spacing_m": SPACING, "length_m": length,
              "closed_loop": True, "direction": "point order = driving direction (north through the beach tunnel)",
              "jump": {"index": jump_i, "x": points[jump_i]["x"], "y": points[jump_i]["y"], "s": points[jump_i]["s"],
                       "take_off_z": points[jump_i]["track_z"], "drop_m": round(points[jump_i]["track_z"] - landing["track_z"], 2),
                       "sketch_mark": {"index": jump_sketch_i, "s": points[jump_sketch_i]["s"],
                                       "note": "Luc's green mark; moved to the terrace edge (decided 2026-11-08)"}},
              "heights": "ground_z = terrain from the elevation data; track_z = driving height (route_profile.json); zone and surface per point",
              "axes": "x = east, y = north (Unity: x = east, z = north), metres from the world origin",
              "points": points}
    json.dump(result, open(os.path.join(HERE, "route.json"), "w"), indent=1)

    # ---------- report ----------
    print(f"sketch cells left out: {left_over}")
    print(f"lap length {length:.0f} m -> lap time {length / 17:.0f} s at 17 m/s average, {length / 22:.0f} s at 22 m/s")
    zs = [p["ground_z"] for p in points]
    print(f"ground height along route {min(zs):.1f} .. {max(zs):.1f} m")
    print(f"jump at s = {points[jump_i]['s']:.0f} m, ({points[jump_i]['x']:.0f}, {points[jump_i]['y']:.0f}), ground {points[jump_i]['ground_z']:.1f} m")
    sections, cur = [], None
    for p in points:
        tag = ("tunnel" if p["tunnel"] else "") or (("inside " + p["building"]) if p["building"] else "") or ("on Avenida Marginal" if p["main_road"] else "")
        if tag != (cur[0] if cur else None):
            if cur and cur[0]:
                sections.append(cur)
            cur = [tag, p["s"], p["s"]]
        else:
            cur[2] = p["s"]
    if cur and cur[0]:
        sections.append(cur)
    for tag, a, b in sections:
        if b - a >= 4:
            print(f"  s {a:5.0f}-{b:5.0f} m ({b - a + SPACING:4.0f} m): {tag}")

    # ---------- pictures ----------
    fig, ax = plt.subplots(figsize=(12, 9))
    for b in DATA["buildings"]:
        xs, ys = zip(*(b["points"] + b["points"][:1]))
        ax.fill(xs, ys, color="#555" if b["campus"] else "#bbb", zorder=2)
        if b["campus"] and b["name"]:
            ax.text(sum(xs) / len(xs), sum(ys) / len(ys), b["name"][:18], fontsize=6, color="white", ha="center", zorder=6)
    for p in DATA["paths"]:
        xs, ys = zip(*p["points"])
        ax.plot(xs, ys, color="#FF8A1F" if p["tunnel"] else ("#777" if p["type"].startswith("primary") else "#bbb"),
                lw=4 if p["type"].startswith("primary") else (3 if p["tunnel"] else 0.6), zorder=1)
    xs, ys, zs = [p["x"] for p in points], [p["y"] for p in points], [p["ground_z"] for p in points]
    sc = ax.scatter(xs, ys, c=zs, cmap="viridis", s=6, zorder=4)
    plt.colorbar(sc, ax=ax, shrink=0.6, label="ground height (m)")
    for i in range(0, len(points), 25):
        a, b = points[i], points[(i + 3) % len(points)]
        ax.annotate("", xy=(b["x"], b["y"]), xytext=(a["x"], a["y"]), arrowprops=dict(arrowstyle="->", color="red", lw=1.5), zorder=5)
        ax.text(a["x"] + 4, a["y"] + 4, f'{a["s"]:.0f}', fontsize=6, color="red", zorder=6)
    ax.scatter([points[jump_i]["x"]], [points[jump_i]["y"]], marker="*", s=300, color="lime", edgecolor="black", zorder=7, label="jump")
    ax.scatter([points[0]["x"]], [points[0]["y"]], marker="s", s=80, color="white", edgecolor="black", zorder=7, label="s = 0 (tunnel)")
    ax.set_xlim(X0, X1); ax.set_ylim(Y0, Y1); ax.set_aspect("equal"); ax.grid(alpha=.3); ax.legend(loc="upper left")
    ax.set_title(f"Route from Luc's sketch: {length:.0f} m, numbers = metres along the lap")
    fig.savefig(os.path.join(HERE, "out", "route_check.png"), dpi=100, bbox_inches="tight")

    fig, ax = plt.subplots(figsize=(12, 3))
    ax.plot([p["s"] for p in points], zs, color="black", label="ground")
    ax.plot([p["s"] for p in points], [p["track_z"] for p in points], color="#FF8A1F", lw=2, label="driving height")
    for p in points:
        if p["tunnel"]:
            ax.axvspan(p["s"] - 1, p["s"] + 1, color="#FF8A1F", alpha=0.3, lw=0)
        elif p["building"]:
            ax.axvspan(p["s"] - 1, p["s"] + 1, color="red", alpha=0.15, lw=0)
    ax.axvline(points[jump_i]["s"], color="green", ls="--", label="jump")
    ax.set_xlabel("metres along the lap"); ax.set_ylabel("ground height (m)"); ax.grid(alpha=.3); ax.legend()
    ax.set_title("Height profile (orange band = tunnel, red band = inside a building footprint)")
    fig.savefig(os.path.join(HERE, "out", "route_profile.png"), dpi=100, bbox_inches="tight")


if __name__ == "__main__":
    main()
