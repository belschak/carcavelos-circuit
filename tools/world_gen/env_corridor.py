"""High-resolution terrain corridor along the route (racing-game style).

The 5 m world grid can't trace a road bed, shoulders or 45 degree cuttings. Along the whole route (outdoor
parts) a curvilinear mesh is built instead: cross-sections every 2 m, samples 1 m apart near the road and
2 m further out, up to CORRIDOR m each side (less on the inside of tight corners so it never folds).
Its heights come straight from the driving profile (route.json track_z):
  road (+1 m)    flat at the driving height
  shoulder       smooth blend to the terrain
  cuttings       never above a 45 degree plane from the road edge; those faces get the concrete material
  wall cuts      (V10g, route.json "wall_cuts") flat 45 degree walls from the road edge up to the natural ground
                 with a sharp top edge, no shoulder: the straight sunken ramp at the north portal
  outer part     blends into the 5 m world terrain, which has a hole under the corridor (covers_cell) only where
                 corridor faces really exist (has_face), so no gap opens next to a drop, a portal or the jump
  cove kerb      on the sand side of the cove kerb (coast.COVE_KERB, the line of V10b's granite kerb) the ground
                 drops from the promenade to the cove sand (coast.KERB_DROP) within KERB_BANK of the kerb's sea face
After build_data(), env_common.ground_z returns this surface, so paths, props and trees sit on it.
"""
import math

import bmesh
import bpy

import coast
import env_common as E
import env_terrain

CORRIDOR = 26.0
ROAD_HALF = E.ROUTE_WIDTH / 2 + 1.0
SHOULDER = {"ramp": 9.0, "tunnel plaza": 10.0, "lower plaza": 14.0, "upper plaza": 14.0}
DEFAULT_SHOULDER = 12.0
CUT_ZONES = {"ramp", "tunnel plaza", "tunnel"}   # "tunnel" = the few open metres in front of a portal
FRACTIONS = [k / 26.0 for k in range(0, 13)] + [k / 26.0 for k in range(14, 27, 2)]   # 1 m, then 2 m steps
SKIP_ZONES = {"terrace deck", "kicker"}           # on the CTT roof: the building carries the road

KERB_BANK = 0.4           # m: width of the drop from the promenade to the cove sand, from the kerb's sea face

_state = {"pts": [], "frames": [], "width": [], "cells": {}}


def ground_level_passage(p):
    """Route point inside a drive-through building whose original ground is close to the road (Student Central,
    the east end of the atrium): the corridor is the bed under the passage floor, so nothing gapes beside it."""
    return bool(p["building"]) and p["ground_z"] > p["track_z"] - 1.5


def eligible(p):
    if p["tunnel"] or p["zone"] in SKIP_ZONES or p["zone"] in ("stairs", "plaza deck"):   # V10i: env_plaza builds those
        return False
    if p["building"]:
        return ground_level_passage(p)
    return p["zone"] != "atrium"


def is_portal(p):
    """First or last point inside the beach tunnel: the corridor gets one extra row here so the ground reaches
    the headwall (it used to stop 2-6 m short and the sea plane showed through beside the road)."""
    pts = _state["pts"]
    n = len(pts)
    i = p["i"]
    return p["tunnel"] and (not pts[(i - 1) % n]["tunnel"] or not pts[(i + 1) % n]["tunnel"])


# V10g: straight sunken ramps (route.json "wall_cuts", written by route_from_sketch.py for a section override):
# flat 45 degree walls from the road edge straight up to the natural ground, a sharp top edge, no soft shoulder
# (Luc's photo of the north ramp, S01, S02, S08). Rows in these index ranges use wall_offsets()/the wall profile.
WALL_CUTS = [(c["from_index"], c["to_index"]) for c in (E.ROUTE or {}).get("wall_cuts", [])]
WALL_BLEND = 6.0          # m before the corridor's outer edge over which a wall row returns to the 5 m terrain
_crest = {}


def wall_row(p):
    return any(a <= p["i"] <= b for a, b in WALL_CUTS)


def natural_z(x, y):
    """The terrain as it was before the route shaped it (elevation data)."""
    return env_terrain._bilinear(E.DATA["grid"]["heights"], x, y)


def wall_crest(p, side):
    """Depth (m, measured outwards from the road edge = height) at which the 45 degree wall of a wall row meets
    the natural ground on `side` (+1 left, -1 right)."""
    key = (p["i"], side)
    if key not in _crest:
        nrm, half, road = _nrm(p), E.half_width(p), p["track_z"] - 0.05
        limit = _state["width"][p["i"]][side] - WALL_BLEND - half - 0.5
        c = 0.05
        while c < limit and road + c < natural_z(p["x"] + nrm[0] * side * (half + c), p["y"] + nrm[1] * side * (half + c)):
            c += 0.05
        _crest[key] = c
    return _crest[key]


def wall_offsets(p, side):
    """Sample offsets (> 0) of a wall row on one side, as many as a normal row has: the road, three on the wall
    (the last exactly on its top edge), then out to the corridor edge, closer together near the wall."""
    half, c, w = E.half_width(p), wall_crest(p, side), _state["width"][p["i"]][side]
    road = [half * k / 4 for k in range(1, 5)]
    wall = [half + c * k / 3 for k in range(1, 4)]
    rest = len(FRACTIONS) - 1 - len(road) - len(wall)
    return road + wall + [half + c + (w - half - c) * (k / rest) ** 1.5 for k in range(1, rest + 1)]


def railing_line(p, side):
    """(offset from the centreline, on the wall top?) for a ramp railing at route point p: on top of the wall of a
    wall row (0.35 m behind its edge), None where the row is a normal one."""
    if not wall_row(p) or not _state["width"]:
        return None
    return E.half_width(p) + wall_crest(p, side) + 0.35


def smoothstep(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def frames(pts):
    n = len(pts)
    out = []
    for i in range(n):
        a, b = pts[(i - 1) % n], pts[(i + 1) % n]
        dx, dy = b["x"] - a["x"], b["y"] - a["y"]
        d = math.hypot(dx, dy) or 1.0
        out.append(((dx / d, dy / d), (-dy / d, dx / d)))
    return out


def widths(pts):
    """Half-width per side, limited on the inside of tight corners (radius * 0.85)."""
    n = len(pts)
    out = []
    for i in range(n):
        a, b, c = pts[(i - 4) % n], pts[i], pts[(i + 4) % n]
        h1 = math.atan2(b["y"] - a["y"], b["x"] - a["x"])
        h2 = math.atan2(c["y"] - b["y"], c["x"] - b["x"])
        turn = (h2 - h1 + math.pi) % (2 * math.pi) - math.pi
        radius = 16.0 / abs(turn) if abs(turn) > 1e-4 else 1e9
        inner = min(CORRIDOR, 0.85 * radius)
        left = inner if turn > 0 else CORRIDOR             # turning left: the left side is the inside
        right = inner if turn < 0 else CORRIDOR
        out.append({1: max(left, ROAD_HALF + 2), -1: max(right, ROAD_HALF + 2)})
    # smooth the widths so the corridor edge doesn't jump
    sm = []
    for i in range(n):
        sm.append({s: sum(out[(i + k) % n][s] for k in range(-3, 4)) / 7 for s in (1, -1)})
    return _hold_back(pts, sm)


# V15b (start straight): the race line now turns back 7.4 m from the underground car-park ramp (entrance_plan), which
# is cut into the 5 m terrain and must not lie under corridor faces. Rows whose side would reach the ramp end
# RAMP_GAP before its outer walls (never less than the flat road bed); the rows before and after them widen again
# by RAMP_TAPER per row, so the corridor's edge swings round the ramp instead of jumping. Rows further away keep
# their width, so the corridor is unchanged everywhere else.
RAMP_GAP = 1.8            # m between the corridor's edge and the ramp's outer walls
RAMP_TAPER = 2.0          # m per row (2 m of route): how fast the edge returns to its full width
HELD_BACK = {}            # (route index, side) -> width, for the build report


def _hold_back(pts, width):
    import entrance_plan as P
    ring = P.ramp_outer()
    n = len(pts)
    fr = frames(pts)
    cx, cy = sum(p[0] for p in ring) / len(ring), sum(p[1] for p in ring) / len(ring)
    reach = max(math.hypot(p[0] - cx, p[1] - cy) for p in ring) + CORRIDOR + 6.0
    cap = {}
    for i, p in enumerate(pts):
        if math.hypot(p["x"] - cx, p["y"] - cy) > reach:
            continue
        nrm = fr[i][1]
        for side in (1, -1):
            o = 0.0
            while o <= width[i][side]:
                x, y = p["x"] + nrm[0] * side * o, p["y"] + nrm[1] * side * o
                if P.inside(x, y, ring) or P.ring_distance(x, y, ring) < RAMP_GAP:
                    cap[(i, side)] = max(ROAD_HALF + 0.3, o - 0.25)
                    break
                o += 0.25
    HELD_BACK.clear()
    span = int(CORRIDOR / RAMP_TAPER) + 3
    for (i, side), o in cap.items():                       # the two rows next to a held-back row end with it
        for k in range(-span, span + 1):
            j = (i + k) % n
            w = min(o + RAMP_TAPER * max(0, abs(k) - 2), width[j][side])
            if w < HELD_BACK.get((j, side), width[j][side]):
                HELD_BACK[(j, side)] = w
    for (j, side), w in HELD_BACK.items():
        width[j][side] = w
    return width


def profile_height(p, offset, coarse):
    """Height of the corridor at a signed lateral offset from route point p."""
    d = abs(offset)
    road = p["track_z"] - 0.05
    half = E.half_width(p)
    road_half = half + 1.0                  # flat part: the road plus 1 m
    shoulder = SHOULDER.get(p["zone"], DEFAULT_SHOULDER)
    held = HELD_BACK.get((p["i"], 1 if offset > 0 else -1))
    if held is not None:                    # V15b: a row held back at the car-park ramp is level with the terrain
        shoulder = min(shoulder, max(0.5, held - road_half - 1.0))       # 1 m before its edge (no step there)
    x = p["x"] + _nrm(p)[0] * offset
    y = p["y"] + _nrm(p)[1] * offset
    outer = coarse(x, y)
    if wall_row(p):
        side = 1 if offset > 0 else -1
        if d <= half:
            return road
        c = wall_crest(p, side)
        if d <= half + c:
            return road + (d - half)
        t = smoothstep((d - (_state["width"][p["i"]][side] - WALL_BLEND)) / WALL_BLEND)
        return min(natural_z(x, y), road + (d - half)) * (1 - t) + outer * t
    if d <= road_half:
        z = road
    elif d <= road_half + shoulder:
        z = road + (outer - road) * smoothstep((d - road_half) / shoulder)
    else:
        z = outer
    if p["zone"] in CUT_ZONES and d > half:
        z = min(z, road + (d - half))
    if d > half + 0.3 and _near_cove(x, y):
        dk, (kx, ky), sand_side = coast.kerb_side(x, y)
        if sand_side and coast.in_cove(x, y):      # beyond the cove kerb: down to the sand right at its sea face
            rim = env_terrain.kerb_height(kx, ky)
            sand = coast.cove_z(x, y, env_terrain.kerb_height)
            t = min(max((dk - coast.KERB_HALF) / KERB_BANK, 0.0), 1.0)
            z = min(z, rim + (sand - rim) * t)
    return z


def kerb_crossing(p, side):
    """Lateral offset (m, > 0) at which the normal of route point p meets the cove kerb on `side`, or None."""
    nrm = _nrm(p)
    rx, ry = side * nrm[0], side * nrm[1]
    best = None
    for (ax, ay), (bx, by) in zip(coast.COVE_KERB, coast.COVE_KERB[1:]):
        ex, ey = bx - ax, by - ay
        den = rx * ey - ry * ex
        if abs(den) < 1e-9:
            continue
        t = ((ax - p["x"]) * ey - (ay - p["y"]) * ex) / den
        u = ((ax - p["x"]) * ry - (ay - p["y"]) * rx) / den
        if 0.0 <= u <= 1.0 and E.half_width(p) + 0.3 < t < CORRIDOR and (best is None or t < best):
            best = t
    return best


def snap_to_kerb(p, offs):
    """Where a row crosses the cove kerb, move its nearest two samples onto the kerb line and the foot of the bank,
    so the drop to the sand follows the kerb instead of zig-zagging between rows (the order of the samples stays)."""
    if not _near_cove(p["x"], p["y"]) and not any(_near_cove(p["x"] + _nrm(p)[0] * o, p["y"] + _nrm(p)[1] * o)
                                                 for o in (-CORRIDOR, CORRIDOR)):
        return offs
    offs = list(offs)
    for side in (1, -1):
        k = kerb_crossing(p, side)
        if k is None:
            continue
        idx = [i for i, o in enumerate(offs) if o * side > E.half_width(p) + 0.3]
        idx.sort(key=lambda i: abs(offs[i]))
        a = min(idx, key=lambda i: abs(abs(offs[i]) - k))
        pos = idx.index(a)
        if pos + 1 >= len(idx):
            continue
        b = idx[pos + 1]
        nxt = abs(offs[idx[pos + 2]]) if pos + 2 < len(idx) else 1e9
        prev = abs(offs[idx[pos - 1]]) if pos > 0 else E.half_width(p) + 0.3
        rim, foot = max(k, prev + 0.05), min(k + KERB_BANK + 0.05, nxt - 0.05)
        if prev < rim < foot < nxt:
            offs[a], offs[b] = side * rim, side * foot
    return offs


_COVE_BOX = (min(p[0] for p in coast.COVE) - 2, min(p[1] for p in coast.COVE) - 2,
             max(p[0] for p in coast.COVE) + 2, max(p[1] for p in coast.COVE) + 2)


def _near_cove(x, y):
    return _COVE_BOX[0] <= x <= _COVE_BOX[2] and _COVE_BOX[1] <= y <= _COVE_BOX[3]


def _nrm(p):
    return _state["frames"][p["i"]][1]


def build_data():
    """Prepares the corridor and makes env_common.ground_z return the corridor surface where it exists."""
    pts = E.route_points()
    _state["pts"] = pts
    _state["frames"] = frames(pts)
    _state["width"] = widths(pts)
    cells = {}
    for p in pts:
        if eligible(p):
            cells.setdefault((int(p["x"] // 20), int(p["y"] // 20)), []).append(p)
    _state["cells"] = cells
    E.set_surface(surface_z)


def nearest(x, y):
    cx, cy = int(x // 20), int(y // 20)
    best, best_d = None, 1e18
    for dx in (-2, -1, 0, 1, 2):
        for dy in (-2, -1, 0, 1, 2):
            for p in _state["cells"].get((cx + dx, cy + dy), []):
                d = (p["x"] - x) ** 2 + (p["y"] - y) ** 2
                if d < best_d:
                    best, best_d = p, d
    return best, math.sqrt(best_d) if best else 1e9


def surface_z(x, y):
    p, d = nearest(x, y)
    if p is None:
        return E.coarse_ground_z(x, y)
    nrm = _nrm(p)
    off = (x - p["x"]) * nrm[0] + (y - p["y"]) * nrm[1]
    side = 1 if off > 0 else -1
    w = _state["width"][p["i"]][side]
    if abs(off) > w or d > w + 2:
        return E.coarse_ground_z(x, y)
    return profile_height(p, off, E.coarse_ground_z)


def has_face(x, y):
    """True if the corridor mesh has a face at (x, y): the two rows around the nearest point on that side are both
    built and not split by a drop (the jump), and the point lies within the corridor's width."""
    p, d = nearest(x, y)
    if p is None:
        return False
    pts, n, i = _state["pts"], len(_state["pts"]), p["i"]
    t, nrm = _state["frames"][i]
    along = (x - p["x"]) * t[0] + (y - p["y"]) * t[1]
    j = i if along >= 0 else i - 1
    a, b = pts[j % n], pts[(j + 1) % n]
    if not ((eligible(a) or is_portal(a)) and (eligible(b) or is_portal(b))) or abs(a["track_z"] - b["track_z"]) > 1.5:
        return False
    other = b if along >= 0 else a                       # the neighbouring row on that side
    if not eligible(other):                             # last row before a portal: no faces beyond it
        if abs(along) > math.hypot(other["x"] - p["x"], other["y"] - p["y"]) + 0.3:
            return False
    off = (x - p["x"]) * nrm[0] + (y - p["y"]) * nrm[1]
    return abs(off) <= _state["width"][i][1 if off > 0 else -1] and d <= _state["width"][i][1 if off > 0 else -1] + 1.0


def inside_depth(x, y):
    """How far (m) a point lies inside the corridor's outer edge; <= 0 outside or where the corridor has no faces."""
    if not has_face(x, y):
        return -1.0
    p, d = nearest(x, y)
    nrm = _nrm(p)
    off = (x - p["x"]) * nrm[0] + (y - p["y"]) * nrm[1]
    return _state["width"][p["i"]][1 if off > 0 else -1] - abs(off)


SKIRT = 0.6        # m: vertical strip under both outer edges, hides the sunken 5 m ground behind it


ROAD_CLEAR = 3.0    # m beyond the road edge: a 5 m terrain cell this close to the road must lie below it
TUNNEL_COVER_OK = 4.5   # m above the tunnel floor: terrain this high over the tunnel is its cover, not in the way


def _rises_over_road(cx, cy, half):
    """True if the 5 m cell centred at (cx, cy) comes within ROAD_CLEAR of the road (any route point, tunnel and
    kicker included) with a corner higher than the road there: such a cell would cut into the road, the kicker or
    a tunnel mouth, so it stays out even where the corridor has no faces."""
    corners = ((-half, -half), (half, -half), (half, half), (-half, half))
    for p in _state["pts"]:
        if abs(p["x"] - cx) > half + 12 or abs(p["y"] - cy) > half + 12:
            continue
        for ox, oy in corners:
            x, y = cx + ox, cy + oy
            z = E.coarse_ground_z(x, y)
            top = p["track_z"] + TUNNEL_COVER_OK if p["tunnel"] else 1e9
            if math.hypot(p["x"] - x, p["y"] - y) < E.half_width(p) + ROAD_CLEAR and p["track_z"] - 0.15 < z < top:
                return True
    return False


def cell_state(cx, cy, half=2.5):
    """Why a 5 m terrain face centred at (cx, cy) is left out: "corridor" (its centre lies 4.5 m inside the corridor
    edge and the corridor has faces at the centre and all four corners), "road" (the corridor has no faces there,
    next to the jump drop, a tunnel portal or the end of a ground-level passage, but the cell would rise over the
    road; build_gap_patches puts a low floor there), or None (the cell stays)."""
    p, d = nearest(cx, cy)
    if p is None:
        return None
    nrm = _nrm(p)
    off = (cx - p["x"]) * nrm[0] + (cy - p["y"]) * nrm[1]
    side = 1 if off > 0 else -1
    if d >= _state["width"][p["i"]][side] - 4.5:
        return None
    if all(has_face(cx + ox, cy + oy) for ox, oy in ((0, 0), (-half, -half), (half, -half), (half, half), (-half, half))):
        return "corridor"
    return "road" if _rises_over_road(cx, cy, half) else None


def covers_cell(cx, cy, half=2.5):
    """True if a 5 m terrain face centred at (cx, cy) is left out (cell_state)."""
    return cell_state(cx, cy, half) is not None


SINK_UNDER_CORRIDOR = 0.35   # as in build_world.build_ground: the 5 m ground sinks this much under the corridor
PATCH_BELOW_ROAD = 0.3       # m: a gap patch lies this far under the lowest road point near it
PATCH_OVERLAP = 1.0          # m: and reaches this far under its neighbours


def build_gap_patches():
    """Low floors for the 5 m cells that stay out because they would rise over the road where the corridor has no
    faces (beside the jump kicker, at the tunnel portals; also under the portal mouths, floor only): without them the
    hole showed the water plane, and with the sea clipped to the coast it would show the sky. Each floor lies
    PATCH_BELOW_ROAD under the lowest road point near it, overlaps its neighbours, and gets walls up to every
    neighbouring terrain cell that is drawn (not inside a tunnel mouth), so the gap reads as a shallow pit in the
    ground. Ground-map material."""
    gx = lambda i: E.X0 + i * E.STEP                                    # noqa: E731
    gy = lambda j: E.Y0 + j * E.STEP                                    # noqa: E731

    def drawn(i, j):
        if not (0 <= i < E.COLS - 1 and 0 <= j < E.ROWS - 1):
            return False
        cx, cy = gx(i) + E.STEP / 2, gy(j) + E.STEP / 2
        return not env_terrain.in_portal_hole(cx, cy) and not covers_cell(cx, cy)

    def vertex_z(i, j):
        x, y = gx(i), gy(j)
        return E.coarse_ground_z(x, y) - SINK_UNDER_CORRIDOR * smoothstep((inside_depth(x, y) - 0.5) / 2.0)

    tunnel = [p for p in _state["pts"] if p["tunnel"]]

    ends = env_terrain.portals() if tunnel else []

    def in_tunnel(x, y):
        """Inside the tunnel's shell in plan: behind both portal planes and within its width at the ceiling (the
        walls lean out from 4.6 m at the floor to 8.5 m at the ceiling). Before 2026-11-11 only 6 m counted, so a
        patch wall with a corner 6 to 8.5 m from the axis rose through the leaning wall beside the north mouth: a
        pale wedge on the mural right where the tunnel wall ends (Luc: "you can see into nothing")."""
        if not any(math.hypot(p["x"] - x, p["y"] - y) < E.half_width(p) + 4.9 for p in tunnel):
            return False
        return all((x - p["x"]) * dx + (y - p["y"]) * dy > -0.05 for p, (dx, dy) in ends)

    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    count = 0
    for j in range(E.ROWS - 1):
        for i in range(E.COLS - 1):
            cx, cy = gx(i) + E.STEP / 2, gy(j) + E.STEP / 2
            portal = env_terrain.in_portal_hole(cx, cy)        # the headwall closes these; a floor catches slivers
            if not portal and cell_state(cx, cy) != "road":
                continue
            near = [p["track_z"] for p in _state["pts"]
                    if math.hypot(p["x"] - cx, p["y"] - cy) < E.STEP + E.half_width(p) + ROAD_CLEAR + 2.0]
            corners = [vertex_z(i, j), vertex_z(i + 1, j), vertex_z(i + 1, j + 1), vertex_z(i, j + 1)]
            floor = min(corners + near) - PATCH_BELOW_ROAD
            e = PATCH_OVERLAP
            quad = [(gx(i) - e, gy(j) - e), (gx(i + 1) + e, gy(j) - e), (gx(i + 1) + e, gy(j + 1) + e), (gx(i) - e, gy(j + 1) + e)]
            faces = [bm.faces.new([bm.verts.new((x, y, floor)) for x, y in quad])]
            # walls up to the drawn neighbours: (edge corner a, edge corner b, neighbour cell)
            for (ai, aj), (bi, bj), (ni, nj) in (((i, j), (i + 1, j), (i, j - 1)), ((i + 1, j), (i + 1, j + 1), (i + 1, j)),
                                                  ((i + 1, j + 1), (i, j + 1), (i, j + 1)), ((i, j + 1), (i, j), (i - 1, j))):
                if portal or not drawn(ni, nj):
                    continue
                za, zb = vertex_z(ai, aj), vertex_z(bi, bj)
                if max(za, zb) <= floor + 0.01 or in_tunnel(gx(ai), gy(aj)) or in_tunnel(gx(bi), gy(bj)):
                    continue                                # no wall inside a tunnel mouth (the portal closes it)
                wall = [bm.verts.new((gx(ai), gy(aj), floor)), bm.verts.new((gx(bi), gy(bj), floor)),
                        bm.verts.new((gx(bi), gy(bj), zb)), bm.verts.new((gx(ai), gy(aj), za))]
                f = bm.faces.new(wall)
                f.normal_update()
                mx, my = (gx(ai) + gx(bi)) / 2, (gy(aj) + gy(bj)) / 2
                if f.normal.x * (cx - mx) + f.normal.y * (cy - my) < 0:   # face into the pit
                    f.normal_flip()
                faces.append(f)
            for f in faces:
                f.normal_update()
                if f is faces[0] and f.normal.z < 0:
                    f.normal_flip()
                for loop in f.loops:
                    co = loop.vert.co
                    loop[uv].uv = ((co.x - E.X0) / (E.X1 - E.X0), (co.y - E.Y0) / (E.Y1 - E.Y0))
            count += 1
    if not count:
        bm.free()
        return
    mesh = bpy.data.meshes.new("Ground_GapPatches")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("ground_map", "#A6AA82", roughness=0.95))
    E.new_object("World_GroundGapPatches", mesh, E.collection("World_Ground"))
    print(f"  ground gaps: {count} cells beside the kicker and the portals get a low floor")


def is_cut_face(p, o0, o1):
    if p["zone"] not in CUT_ZONES:
        return False
    lo, hi = min(abs(o0), abs(o1)), max(abs(o0), abs(o1))
    if lo < E.half_width(p) - 0.01 or (o0 > 0) != (o1 > 0):
        return False
    if wall_row(p):                                     # exactly the wall, up to its top edge
        return hi <= E.half_width(p) + wall_crest(p, 1 if o0 > 0 else -1) + 0.01
    mid = (o0 + o1) / 2
    return profile_height(p, mid, E.coarse_ground_z) < E.coarse_ground_z(p["x"] + _nrm(p)[0] * mid, p["y"] + _nrm(p)[1] * mid) - 0.3


def build_mesh():
    pts = _state["pts"]
    n = len(pts)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rows = []
    for p in pts:
        if not (eligible(p) or is_portal(p)):       # portal rows are not in the lookup (covers_cell/surface_z)
            rows.append(None)
            continue
        nrm = _nrm(p)
        w = _state["width"][p["i"]]
        offs = snap_to_kerb(p, [-f * w[-1] for f in reversed(FRACTIONS[1:])] + [f * w[1] for f in FRACTIONS])
        if wall_row(p):
            offs = [-o for o in reversed(wall_offsets(p, -1))] + [0.0] + wall_offsets(p, 1)
        shift = (0.0, 0.0)
        if is_portal(p):
            # 0.6 m into the tunnel, under its floor: the tunnel ring and this row use slightly different
            # frames, so meeting exactly at the portal left a thin slit
            t = _state["frames"][p["i"]][0]
            inward = 0.6 if pts[(p["i"] + 1) % n]["tunnel"] else -0.6
            shift = (t[0] * inward, t[1] * inward)
        row = []
        for o in offs:
            x, y = p["x"] + nrm[0] * o + shift[0], p["y"] + nrm[1] * o + shift[1]
            z = profile_height(p, o, E.coarse_ground_z) - (0.02 if abs(o) <= E.half_width(p) else -0.03)
            row.append((bm.verts.new((x, y, z)), o))
        rows.append(row)
    for i in range(n):
        a, b = rows[i], rows[(i + 1) % n]
        if a is None or b is None or abs(pts[i]["track_z"] - pts[(i + 1) % n]["track_z"]) > 1.5:
            continue
        for k in range(len(a) - 1):
            face = bm.faces.new((a[k][0], b[k][0], b[k + 1][0], a[k + 1][0]))
            face.material_index = 1 if is_cut_face(pts[i], a[k][1], a[k + 1][1]) else 0
            for loop in face.loops:
                co = loop.vert.co
                if face.material_index == 0:
                    loop[uv].uv = ((co.x - E.X0) / (E.X1 - E.X0), (co.y - E.Y0) / (E.Y1 - E.Y0))
                else:
                    loop[uv].uv = (co.x / 2.0, co.z / 2.0)
    skirt_faces = []
    for i in range(n):
        a, b = rows[i], rows[(i + 1) % n]
        if a is None or b is None or abs(pts[i]["track_z"] - pts[(i + 1) % n]["track_z"]) > 1.5:
            continue
        for k, side in ((0, -1), (-1, 1)):           # first column = right edge, last = left edge
            va, vb = a[k][0], b[k][0]
            la = bm.verts.new((va.co.x, va.co.y, va.co.z - SKIRT))
            lb = bm.verts.new((vb.co.x, vb.co.y, vb.co.z - SKIRT))
            face = bm.faces.new((va, vb, lb, la))
            face.material_index = 0
            nrm = _nrm(pts[i])
            skirt_faces.append((face, (nrm[0] * side, nrm[1] * side)))
            for loop in face.loops:
                co = loop.vert.co
                loop[uv].uv = ((co.x - E.X0) / (E.X1 - E.X0), (co.y - E.Y0) / (E.Y1 - E.Y0))
    bm.normal_update()
    skirt = {f for f, _ in skirt_faces}
    for f in bm.faces:
        if f not in skirt and f.normal.z < 0:
            f.normal_flip()
    for f, (ox, oy) in skirt_faces:                  # skirts face outwards
        if f.normal.x * ox + f.normal.y * oy < 0:
            f.normal_flip()
    mesh = bpy.data.meshes.new("Ground_Corridor")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("ground_map", "#A6AA82", roughness=0.95))
    mesh.materials.append(E.material("T_cut_wall", E.PALETTE["concrete_sand"], roughness=0.9))
    E.new_object("World_GroundCorridor", mesh, E.collection("World_Ground"))
    build_gap_patches()
