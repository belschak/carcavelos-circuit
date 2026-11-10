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


def frame(run, k):
    a, b = run[max(k - 1, 0)], run[min(k + 1, len(run) - 1)]
    dx, dy = b["x"] - a["x"], b["y"] - a["y"]
    d = math.hypot(dx, dy) or 1.0
    return (dx / d, dy / d), (-dy / d, dx / d)


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


def dress(runs, passage_width):
    """runs: list of lists of route point dicts (the route inside each passage, already extended)."""
    if not runs:
        return
    # only the part inside the building, and only real halls (the short corner clips stay bare)
    runs = [[p for p in run if p["building"]] for run in runs]
    runs = [run for run in runs if len(run) >= 10]
    if not runs:
        return
    col = E.collection("World_Interiors")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    half = passage_width / 2
    for run in runs:
        for k, p in enumerate(run):
            t, nrm = frame(run, k)
            floor = p["track_z"]
            if k % COLUMN_EVERY == 0:
                for side in (1, -1):
                    c = (p["x"] + nrm[0] * side * (half - 0.6), p["y"] + nrm[1] * side * (half - 0.6))
                    box(bm, c, t, nrm, COLUMN / 2, COLUMN / 2, floor, floor + HALL_HEIGHT, 0)
            if k % 3 == 0:                                   # ceiling light panel every 6 m
                corners = [(p["x"] + t[0] * a + nrm[0] * s, p["y"] + t[1] * a + nrm[1] * s, floor + HALL_HEIGHT - 0.05)
                           for a, s in ((-1.2, -0.6), (-1.2, 0.6), (1.2, 0.6), (1.2, -0.6))]
                f = bm.faces.new([bm.verts.new(c) for c in corners])
                f.material_index = 2
            if k + 1 < len(run):                             # glazing on both walls, slightly in front of them
                q = run[k + 1]
                _, nq = frame(run, k + 1)
                for side in (1, -1):
                    off = side * (half - 0.08)
                    vs = [(p["x"] + nrm[0] * off, p["y"] + nrm[1] * off, floor + 0.2),
                          (q["x"] + nq[0] * off, q["y"] + nq[1] * off, q["track_z"] + 0.2),
                          (q["x"] + nq[0] * off, q["y"] + nq[1] * off, q["track_z"] + 4.2),
                          (p["x"] + nrm[0] * off, p["y"] + nrm[1] * off, floor + 4.2)]
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
