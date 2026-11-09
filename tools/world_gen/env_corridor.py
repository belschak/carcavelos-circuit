"""High-resolution terrain corridor along the route (racing-game style).

The 5 m world grid can't trace a road bed, shoulders or 45 degree cuttings. Along the whole route (outdoor
parts) a curvilinear mesh is built instead: cross-sections every 2 m, samples 1 m apart near the road and
2 m further out, up to CORRIDOR m each side (less on the inside of tight corners so it never folds).
Its heights come straight from the driving profile (route.json track_z):
  road (+1 m)    flat at the driving height
  shoulder       smooth blend to the terrain
  cuttings       never above a 45 degree plane from the road edge; those faces get the concrete material
  outer part     blends into the 5 m world terrain, which has a hole under the corridor (covers_cell).
After build_data(), env_common.ground_z returns this surface, so paths, props and trees sit on it.
"""
import math

import bmesh
import bpy

import env_common as E

CORRIDOR = 26.0
ROAD_HALF = E.ROUTE_WIDTH / 2 + 1.0
SHOULDER = {"ramp": 9.0, "tunnel plaza": 10.0, "lower plaza": 14.0, "upper plaza": 14.0}
DEFAULT_SHOULDER = 12.0
CUT_ZONES = {"ramp", "tunnel plaza", "tunnel"}   # "tunnel" = the few open metres in front of a portal
FRACTIONS = [k / 26.0 for k in range(0, 13)] + [k / 26.0 for k in range(14, 27, 2)]   # 1 m, then 2 m steps
SKIP_ZONES = {"terrace deck"}           # on the CTT roof: the building carries the road

_state = {"pts": [], "frames": [], "width": [], "cells": {}}


def ground_level_passage(p):
    """Route point inside a drive-through building whose original ground is close to the road (Student Central,
    the east end of the atrium): the corridor is the bed under the passage floor, so nothing gapes beside it."""
    return bool(p["building"]) and p["ground_z"] > p["track_z"] - 1.5


def eligible(p):
    if p["tunnel"] or p["zone"] in SKIP_ZONES:
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
    return sm


def profile_height(p, offset, coarse):
    """Height of the corridor at a signed lateral offset from route point p."""
    d = abs(offset)
    road = p["track_z"] - 0.05
    half = E.half_width(p)
    road_half = half + 1.0                  # flat part: the road plus 1 m
    shoulder = SHOULDER.get(p["zone"], DEFAULT_SHOULDER)
    x = p["x"] + _nrm(p)[0] * offset
    y = p["y"] + _nrm(p)[1] * offset
    outer = coarse(x, y)
    if d <= road_half:
        z = road
    elif d <= road_half + shoulder:
        z = road + (outer - road) * smoothstep((d - road_half) / shoulder)
    else:
        z = outer
    if p["zone"] in CUT_ZONES and d > half:
        z = min(z, road + (d - half))
    return z


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


def inside_depth(x, y):
    """How far (m) a point lies inside the corridor's outer edge; <= 0 outside or where the corridor has no faces."""
    p, d = nearest(x, y)
    if p is None:
        return -1.0
    pts, n, i = _state["pts"], len(_state["pts"]), p["i"]
    for j in (i - 1, i):                         # faces on both sides of p must exist (no drop, both rows built)
        a, b = pts[j % n], pts[(j + 1) % n]
        if not (eligible(a) and eligible(b)) or abs(a["track_z"] - b["track_z"]) > 1.5:
            return -1.0
    nrm = _nrm(p)
    off = (x - p["x"]) * nrm[0] + (y - p["y"]) * nrm[1]
    w = _state["width"][i][1 if off > 0 else -1]
    return w - abs(off) if d <= w + 1.0 else -1.0


SKIRT = 0.6        # m: vertical strip under both outer edges, hides the sunken 5 m ground behind it


def covers_cell(cx, cy):
    """True if a 5 m terrain face centred at (cx, cy) is fully replaced by the corridor."""
    p, d = nearest(cx, cy)
    if p is None:
        return False
    nrm = _nrm(p)
    off = (cx - p["x"]) * nrm[0] + (cy - p["y"]) * nrm[1]
    side = 1 if off > 0 else -1
    return d < _state["width"][p["i"]][side] - 4.5


def is_cut_face(p, o0, o1):
    if p["zone"] not in CUT_ZONES:
        return False
    lo, hi = min(abs(o0), abs(o1)), max(abs(o0), abs(o1))
    if lo < E.half_width(p) - 0.01 or (o0 > 0) != (o1 > 0):
        return False
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
        offs = [-f * w[-1] for f in reversed(FRACTIONS[1:])] + [f * w[1] for f in FRACTIONS]
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
