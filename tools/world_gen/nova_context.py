"""Test world for the Nova hall: the hall placed inside the real footprint of the Edificio Principal, with the ground and the
neighbouring buildings around it, so entrances and proportions can be checked against the real campus.

What is in the scene (separate collections, each one an FBX object you can hide in Unity):
  Context_Ground       ground around the building (flat plaza level near the building, the elevation data further out)
  Context_Buildings    the neighbouring buildings as simple blocks (footprints from OpenStreetMap, heights from buildings.csv)
  Ref_Building_Ghost   the Edificio Principal footprint as a see-through block (13 m, buildings.csv)
  Ref_Outline          red kerb along the footprint (where the walls really are)
  Nova_Hall            the hall from nova_hall.py, north doors on the north facade, glass facade along the west facade

Run:  blender -b -P nova_context.py   ->  out/NovaHallContext.blend, out/NovaHallContext.fbx, out/previews_context/*.png
"""
import csv
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import env_common as E          # noqa: E402
import hall_kit as K            # noqa: E402
import nova_hall as H           # noqa: E402

OUT = os.path.join(HERE, "out")
BUILDING_ID = "w665606788"                       # Edificio Principal
REGION = (-90.0, 170.0, 30.0, 235.0)             # x0, x1, y0, y1 of the piece of map that is copied (m)
PLAZA_Z, PLAZA_CENTRE, PLAZA_FLAT, PLAZA_FADE = 14.45, (35.0, 125.0), 95.0, 150.0   # flat plaza level, blend to terrain
NORTH_FACE = ((3.0, 160.0), (67.0, 184.0))       # the two north corners of the footprint (from world_data.json)
FACADE_WEST_INSET = 0.0                          # hall glass facade distance from the west wall (m)


def ctx_material(name, colour, rough=0.9, alpha=1.0):
    mat = E.material(name, colour, rough, 0.0, None, alpha)
    if alpha < 1.0 and hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "BLENDED"
    return mat


def heights_csv():
    out = {}
    for row in csv.DictReader(open(os.path.join(HERE, "buildings.csv"), encoding="utf-8")):
        try:
            out[row["id"]] = float(row["height_m"]) if row["height_m"] else (float(row["levels"]) * 3.5 if row["levels"] else 9.0)
        except ValueError:
            out[row["id"]] = 9.0
    return out


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def ground_height(x, y):
    d = math.hypot(x - PLAZA_CENTRE[0], y - PLAZA_CENTRE[1])
    w = smooth((d - PLAZA_FLAT) / (PLAZA_FADE - PLAZA_FLAT))
    return PLAZA_Z * (1 - w) + E.ground_z(x, y) * w


def build_ground(col):
    x0, x1, y0, y1 = REGION
    step = 4.0
    nx, ny = int((x1 - x0) / step), int((y1 - y0) / step)
    bm = bmesh.new()
    verts = [[bm.verts.new((x0 + i * step, y0 + j * step, ground_height(x0 + i * step, y0 + j * step))) for i in range(nx + 1)] for j in range(ny + 1)]
    for j in range(ny):
        for i in range(nx):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for loop in f.loops:
            loop[uv].uv = (loop.vert.co.x / 4.0, loop.vert.co.y / 4.0)
    mesh = bpy.data.meshes.new("Context_Ground")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(ctx_material("N_ctx_ground", "#B9BBAA", 0.95))
    E.new_object("Context_Ground", mesh, col)


def prism(points, z0, z1, name, col, wall_mat, roof_mat):
    bm = bmesh.new()
    low = [bm.verts.new((x, y, z0)) for x, y in points]
    high = [bm.verts.new((x, y, z1)) for x, y in points]
    n = len(points)
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((low[i], low[j], high[j], high[i]))
        f.material_index = 0
    roof = bm.faces.new(high)
    roof.material_index = 1
    bm.normal_update()
    if roof.normal.z < 0:                                          # make every face point outward
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(wall_mat)
    mesh.materials.append(roof_mat)
    return E.new_object(name, mesh, col)


def build_buildings(col_ctx, col_ref):
    heights = heights_csv()
    x0, x1, y0, y1 = REGION
    wall = ctx_material("N_ctx_wall", "#E6E4DE", 0.85)
    roof = ctx_material("N_ctx_roof", "#BDBBB5", 0.9)
    ghost = ctx_material("N_ref_ghost", "#E86A5A", 0.9, 0.28)
    outline = ctx_material("N_ref_outline", "#D8352A", 0.8)
    footprint = None
    for b in E.DATA["buildings"]:
        pts = [(p[0], p[1]) for p in b["points"]]
        cx, cy = sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)
        if not (x0 - 30 < cx < x1 + 30 and y0 - 30 < cy < y1 + 30):
            continue
        base = min(ground_height(x, y) for x, y in pts) - 0.3
        h = heights.get(b["id"], 9.0)
        if b["id"] == BUILDING_ID:
            footprint = pts
            prism(pts, base, base + h + 0.0, "Ref_Building_Ghost", col_ref, ghost, ghost)
            continue
        prism(pts, base, base + h, "B_" + (b["id"]), col_ctx, wall, roof)
    # red kerb along the Edificio Principal footprint
    m = K.Mesh()
    for (ax, ay), (bx, by) in zip(footprint, footprint[1:] + footprint[:1]):
        length = math.hypot(bx - ax, by - ay)
        if length < 0.5:
            continue
        mid = ((ax + bx) / 2, (ay + by) / 2)
        z = ground_height(*mid) + 0.2
        m.box((mid[0], mid[1], z), length / 2, 0.35, 0.2, "outline", math.atan2(by - ay, bx - ax))
    K.MATERIALS["outline"] = lambda: outline
    m.to_object("Ref_Outline", col_ref)
    return footprint


def hall_frame():
    """Frame of the hall: north doors in the middle of the strip next to the west wall, axis perpendicular to the north facade."""
    (ax, ay), (bx, by) = NORTH_FACE                    # west corner, east corner
    d = math.hypot(bx - ax, by - ay)
    tx, ty = (bx - ax) / d, (by - ay) / d              # along the north facade, towards the east
    ux, uy = ty, -tx                                   # hall axis: perpendicular to the facade, pointing south
    heading = math.atan2(uy, ux)
    # left of the axis = (-uy, ux) = towards the east along the facade; the west wall lies H.B_RIGHT to the right of the axis
    ox = ax + tx * (H.B_RIGHT + FACADE_WEST_INSET)
    oy = ay + ty * (H.B_RIGHT + FACADE_WEST_INSET)
    return K.Frame(ox, oy, math.degrees(heading))


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    col_ground, col_ctx = E.collection("Context_Ground"), E.collection("Context_Buildings")
    col_ref, col_hall = E.collection("Ref"), E.collection("Nova_Hall")
    build_ground(col_ground)
    build_buildings(col_ctx, col_ref)
    f = hall_frame()
    floor_z = ground_height(f.x, f.y) + 0.02
    for obj in H.make_hall(f, col_hall):
        obj.location.z = floor_z
    print(f"  hall origin ({f.x:.1f}, {f.y:.1f}), heading {math.degrees(f.h):.1f} deg, floor z {floor_z:.2f}")
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "NovaHallContext.blend"))
    H.export_fbx(os.path.join(OUT, "NovaHallContext.fbx"))
    print("done")


if __name__ == "__main__":
    build()
