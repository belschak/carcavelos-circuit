"""Interior dressing for the drive-through passages (the atrium and the hall at Student Central).

From the walk videos (IMG_7722, IMG_7725): a long bright hall with slim white columns, glass walls with
mullions, a light ceiling with lights. Per passage piece: columns every 8 m along both sides, glazing
between them, light panels in the ceiling. Kept simple (a few hundred triangles per passage).
"""
import math

import bmesh
import bpy

import env_common as E

COLUMN_EVERY = 4            # route points (2 m each) -> a column every 8 m
COLUMN = 0.45               # column width (m)
HALL_HEIGHT = 7.0           # matches the passage cut height in build_world.py

# Wide halls (2026-11-09, design pass): in these buildings the passage opens into bays on both sides, so the
# atrium reads as the real hall with cafe tables, study tables and the piano (env_props.place_interior).
# Half-width per side = min(HALL_MAX, room to the outer facade - HALL_WALL_GAP), never below the plain passage,
# and it grows by at most HALL_TAPER per route point from the openings (45 degree walls at the ends).
WIDE_HALLS = {"Edifício Principal": 11.0}
HALL_WALL_GAP = 2.5
HALL_TAPER = 1.0            # metres per route point (2 m)


def frame(run, k):
    a, b = run[max(k - 1, 0)], run[min(k + 1, len(run) - 1)]
    dx, dy = b["x"] - a["x"], b["y"] - a["y"]
    d = math.hypot(dx, dy) or 1.0
    return (dx / d, dy / d), (-dy / d, dx / d)


PASSAGE_EXTEND = 5          # route points (2 m each) the cut reaches past each wall (build_world.py uses this)


def passages():
    """Every drive-through piece: [{"building", "idx" (route indices incl. PASSAGE_EXTEND past each wall),
    "points", "widths" (per point (left, right) half-widths)}], for the buildings marked drive_through.
    Shared by the passage cut (build_world.py), the interior (dress) and the furniture (env_props)."""
    pts = E.route_points()
    n = len(pts)
    base = (E.ROUTE_WIDTH + 2.0) / 2
    out = []
    for b in E.DATA["buildings"]:
        if not b.get("drive_through"):
            continue
        hits = [i for i, p in enumerate(pts) if E.inside_polygon(p["x"], p["y"], b["points"])]
        if not hits:
            continue
        runs = [[hits[0]]]                                 # group into continuous pieces
        for i in hits[1:]:
            if i == runs[-1][-1] + 1:
                runs[-1].append(i)
            else:
                runs.append([i])
        if len(runs) > 1 and runs[0][0] == 0 and runs[-1][-1] == n - 1:
            runs[0] = runs.pop() + runs[0]                 # piece that wraps over the lap start
        for run in runs:
            idx = ([(run[0] - k) % n for k in range(PASSAGE_EXTEND, 0, -1)] + run
                   + [(run[-1] + k) % n for k in range(1, PASSAGE_EXTEND + 1)])
            points = [pts[i] for i in idx]
            out.append({"building": b, "run": run, "idx": idx, "points": points,
                        "widths": hall_widths(points, b, base)})
    return out


def side_room(p, nrm, side, poly, reach=40.0, step=0.25):
    """How far (m) one can go from route point p along +-nrm before leaving the building polygon."""
    o = 0.0
    while o < reach and E.inside_polygon(p["x"] + nrm[0] * side * (o + step), p["y"] + nrm[1] * side * (o + step), poly):
        o += step
    return o


def hall_widths(run, building, base_half):
    """Per route point of a passage run: (left, right) half-widths (m) of the drive-through space.
    left = +normal side (left of the driving direction). Plain passages: base_half on both sides."""
    if not building or building["name"] not in WIDE_HALLS:
        return [(base_half, base_half)] * len(run)
    hall_max = WIDE_HALLS[building["name"]]
    poly = building["points"]
    raw = []
    for k, p in enumerate(run):
        _t, nrm = frame(run, k)
        inside = E.inside_polygon(p["x"], p["y"], poly)
        raw.append(tuple(max(base_half, min(hall_max, side_room(p, nrm, side, poly) - HALL_WALL_GAP))
                         if inside else base_half for side in (1, -1)))
    out = []
    for k in range(len(run)):                   # taper: never wider than the walls of the neighbours allow
        out.append(tuple(min(raw[j][s] + abs(j - k) * HALL_TAPER for j in range(len(run))) for s in (0, 1)))
    return out


def box(bm, centre, t, nrm, half_t, half_n, z0, z1, mat):
    cx, cy = centre
    corners = []
    for a, s in ((-half_t, -half_n), (half_t, -half_n), (half_t, half_n), (-half_t, half_n)):
        corners.append((cx + t[0] * a + nrm[0] * s, cy + t[1] * a + nrm[1] * s))
    low = [bm.verts.new((x, y, z0)) for x, y in corners]
    high = [bm.verts.new((x, y, z1)) for x, y in corners]
    for k in range(4):
        f = bm.faces.new((low[k], low[(k + 1) % 4], high[(k + 1) % 4], high[k]))
        f.material_index = mat
    bm.faces.new(high).material_index = mat


def dress(runs, passage_width, widths=None):
    """runs: list of lists of route point dicts (the route inside each passage, already extended);
    widths: per run, per point (left, right) half-widths from hall_widths() (default: passage_width / 2)."""
    if not runs:
        return
    widths = widths or [[(passage_width / 2, passage_width / 2)] * len(run) for run in runs]
    # only the part inside the building, and only real halls (the short corner clips stay bare)
    # V10c2: and only inside the walls of the building itself (the passage reaches 10 m past each wall: its
    # columns and glazing stood on the CTT roof terrace and under the roof overhang)
    import env_campus
    pairs = [[(p, w) for p, w in zip(run, ws) if p["building"] and env_campus.in_hall(p)] for run, ws in zip(runs, widths)]
    pairs = [pr for pr in pairs if len(pr) >= 10]
    if not pairs:
        return
    col = E.collection("World_Interiors")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for pr in pairs:
        run = [p for p, _w in pr]
        halves = [w for _p, w in pr]
        for k, p in enumerate(run):
            t, nrm = frame(run, k)
            floor = p["track_z"]
            if k % COLUMN_EVERY == 0:
                for side, half in ((1, halves[k][0]), (-1, halves[k][1])):
                    c = (p["x"] + nrm[0] * side * (half - 0.6), p["y"] + nrm[1] * side * (half - 0.6))
                    box(bm, c, t, nrm, COLUMN / 2, COLUMN / 2, floor, floor + HALL_HEIGHT, 0)
            if k % 3 == 0:                                   # ceiling light panel every 6 m
                centres = [0.0] + [side * h * 0.62 for side, h in ((1, halves[k][0]), (-1, halves[k][1])) if h > 7.5]
                for v0 in centres:                           # wide halls: two more rows over the bays
                    corners = [(p["x"] + t[0] * a + nrm[0] * (v0 + s), p["y"] + t[1] * a + nrm[1] * (v0 + s),
                                floor + HALL_HEIGHT - 0.05)
                               for a, s in ((-1.2, -0.6), (-1.2, 0.6), (1.2, 0.6), (1.2, -0.6))]
                    f = bm.faces.new([bm.verts.new(c) for c in corners])
                    f.material_index = 2
            if k + 1 < len(run):                             # glazing on both walls, slightly in front of them
                q = run[k + 1]
                _, nq = frame(run, k + 1)
                for side, hp, hq in ((1, halves[k][0], halves[k + 1][0]), (-1, halves[k][1], halves[k + 1][1])):
                    op, oq = side * (hp - 0.08), side * (hq - 0.08)
                    vs = [(p["x"] + nrm[0] * op, p["y"] + nrm[1] * op, floor + 0.2),
                          (q["x"] + nq[0] * oq, q["y"] + nq[1] * oq, q["track_z"] + 0.2),
                          (q["x"] + nq[0] * oq, q["y"] + nq[1] * oq, q["track_z"] + 4.2),
                          (p["x"] + nrm[0] * op, p["y"] + nrm[1] * op, floor + 4.2)]
                    face = bm.faces.new([bm.verts.new(v) for v in (vs if side == 1 else vs[::-1])])
                    face.material_index = 1
                    u0, u1 = p["s"] / 6.0, q["s"] / 6.0
                    for loop, c in zip(face.loops, ((u0, 0), (u1, 0), (u1, 1), (u0, 1)) if side == 1 else ((u0, 1), (u1, 1), (u1, 0), (u0, 0))):
                        loop[uv].uv = c
    bm.normal_update()
    for f in bm.faces:
        if f.material_index == 2 and f.normal.z > 0:
            f.normal_flip()                                  # light panels face down
    mesh = bpy.data.meshes.new("Interior_Passages")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("B_interior_column", "#F4F3F0", 0.6))
    mesh.materials.append(E.material("B_glass"))
    mesh.materials.append(E.material("interior_light", "#FFF8EA", 0.5, emission="#FFF8EA"))
    E.new_object("Interior_Passages", mesh, col)
