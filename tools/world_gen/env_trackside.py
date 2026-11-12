"""Trackside readability: painted edge lines and chevron corner signs, exported with the track.

On the light plazas, the promenade and the wide asphalt of the coastal road the road edge was hard to read
from the chase camera. Two cheap aids, both in collection Track_Surface (TrackSurface.fbx):
  Track_EdgeLines    0.18 m painted line 0.25 m inside each road edge, 4 cm above the road. White on dark
                     surfaces (asphalt, tunnel floor, terracotta), graphite on the light ones (sand, light and
                     polished concrete, calcada). Left out in the tunnel (its footways mark the edge), across the
                     jump drop and on a side where a kerb already marks a tight corner (env_track.build_kerbs rule).
                     V18r: none on the roundabout (street_plan.RB_NO_LINES); there the mesh carries the roundabout's
                     own markings where they lie on the race road (env_streets.road_paint), like the coast's paint.
  Track_CornerSigns  white boards with Boost Orange chevrons on the outside of the sharpest outdoor corners,
                     facing the approaching kart. Orange on white is the one allowed use of the signal colour in
                     the scenery (route arrows); the boards keep at least 1.2 m from the road edge.
About 2,200 triangles in all (lines 2,026, four signs 192, measured 2026-11-09).
"""
import math

import bmesh
import bpy
from mathutils import Vector

import env_common as E
import env_track as T
import street_plan

LINE_W = 0.18               # m, painted line width
LINE_INSET = 0.25           # m, from the road edge to the outer side of the line
LINE_LIFT = 0.04            # m above the road surface (2 cm still flickered at a distance)
DARK_SURFACES = {"asphalt", "tunnel_floor", "terracotta", "street_setts"}

SIGN_ZONES = {"lawn and paths", "coastal road", "promenade", "lower plaza", "upper plaza", "tunnel plaza"}
SIGNS_MAX = 6
SIGN_SPACING_M = 60.0       # at most one sign per 60 m of lap
SIGN_MAX_RADIUS = 30.0      # only corners tighter than this
SIGN_W, SIGN_H, SIGN_LIFT, SIGN_T = 2.4, 0.8, 0.5, 0.08
SIGN_GAP = 1.2              # m between the road edge and the nearest board corner
LOOK_BACK = 6               # route points (12 m): the board faces the kart this far before the apex


def kerb_sides(pts):
    """Per route point, the sides that already carry a kerb (same rule as env_track.build_kerbs)."""
    out = []
    for i, p in enumerate(pts):
        k = T.curvature(pts, i)
        radius = 1 / abs(k) if abs(k) > 1e-6 else 1e9
        sides = set()
        if p["zone"] in T.KERB_ZONES and radius < 35 and not street_plan.race_kerbs_off(i):   # V18r: the roundabout
            sides.add(1 if k > 0 else -1)
            if radius < 20:
                sides.add(-1 if k > 0 else 1)
        out.append(sides)
    return out


def build_lines(pts, fr, col):
    n = len(pts)
    kerbs = kerb_sides(pts)
    bm = bmesh.new()
    for side in (1, -1):
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            if a["tunnel"] or b["tunnel"] or T.is_drop(a, b) or a["zone"] in T.PLAZA_ZONES or b["zone"] in T.PLAZA_ZONES:
                continue                           # V10i: no painted lines on the stairs and the plaza
            if side in kerbs[i] and side in kerbs[(i + 1) % n]:
                continue
            if street_plan.race_lines_off(i) and street_plan.race_lines_off((i + 1) % n):
                continue                           # V18r: on the roundabout the road carries the real markings
            (_, na), (_, nb) = fr[i], fr[(i + 1) % n]
            za, zb = T.road_z(a) + LINE_LIFT, T.road_z(b) + LINE_LIFT

            def at(p, nrm, o, z):
                return bm.verts.new((p["x"] + side * nrm[0] * o, p["y"] + side * nrm[1] * o, z))
            oa, ob = E.half_width(a) - LINE_INSET, E.half_width(b) - LINE_INSET
            face = bm.faces.new((at(a, na, oa, za), at(b, nb, ob, zb), at(b, nb, ob - LINE_W, zb), at(a, na, oa - LINE_W, za)))
            face.material_index = 0 if T.visual_surface(a) in DARK_SURFACES else 1
    import env_coast                       # V16e: flush paint of the coast section (slot drains, step nosings)
    env_coast.road_paint(bm, pts, fr)
    import env_streets                     # V18r: the roundabout's own markings where they lie on the race road
    extra = env_streets.road_paint(bm, first_slot=2)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.005)
    bm.normal_update()
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    mesh = bpy.data.meshes.new("Track_EdgeLines")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("edge_line_white", "#EDEDE8", roughness=0.6))
    mesh.materials.append(E.material("edge_line_graphite", "#4A4D52", roughness=0.7))
    for name, hexcol in extra:
        mesh.materials.append(E.material(name, hexcol, roughness=0.8))
    return E.new_object("Track_EdgeLines", mesh, col)


def corners(pts):
    """Apex indices of the sharpest outdoor corners, at least SIGN_SPACING_M apart, sharpest first."""
    n = len(pts)
    scored = []
    for i, p in enumerate(pts):
        if p["zone"] not in SIGN_ZONES or any(pts[(i + d) % n]["tunnel"] or pts[(i + d) % n]["building"] for d in range(-8, 9)):
            continue                                     # not within 16 m of a tunnel or a building passage
        k = T.curvature(pts, i, span=4)
        if abs(k) > 1 / SIGN_MAX_RADIUS:
            scored.append((abs(k), i, 1 if k > 0 else -1))
    lap = pts[-1]["s"] + 2.0
    chosen = []
    for _, i, turn in sorted(scored, reverse=True):
        if all(min(abs(pts[i]["s"] - pts[j]["s"]), lap - abs(pts[i]["s"] - pts[j]["s"])) >= SIGN_SPACING_M for j, _ in chosen):
            chosen.append((i, turn))
        if len(chosen) == SIGNS_MAX:
            break
    return chosen


def clear_of_road(x, y):
    p = E.nearest_route_point(x, y)
    return E.distance_to_route(x, y) >= E.half_width(p) + SIGN_GAP


def add_box(bm, centre, u, f, half_u, half_f, z0, z1, mat):
    """Box with its width along u and depth along f (both unit vectors in the ground plane)."""
    vs = []
    for z in (z0, z1):
        for su, sf in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs.append(bm.verts.new(centre + u * (su * half_u) + f * (sf * half_f) + Vector((0, 0, z))))
    faces = [(0, 1, 2, 3), (4, 7, 6, 5)] + [(k, 4 + k, 4 + (k + 1) % 4, (k + 1) % 4) for k in range(4)]
    made = []
    for idx in faces:
        made.append(bm.faces.new([vs[k] for k in idx]))
        made[-1].material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=made)        # closed box: normals point outwards


def add_sign(bm, pts, fr, i, turn):
    p = pts[i]
    (_, nrm) = fr[i]
    out = -turn                                          # the outside of the corner
    look = pts[(i - LOOK_BACK) % len(pts)]
    dist = E.half_width(p) + SIGN_GAP + SIGN_W / 2
    for _ in range(12):                                  # push outwards until both board ends clear the road
        c = Vector((p["x"] + out * nrm[0] * dist, p["y"] + out * nrm[1] * dist, 0))
        f = Vector((c.x - look["x"], c.y - look["y"], 0)).normalized()     # kart -> board
        u = Vector((f.y, -f.x, 0))                                          # the driver's right
        ends = [c + u * (SIGN_W / 2), c - u * (SIGN_W / 2)]
        if all(clear_of_road(e.x, e.y) for e in ends):
            break
        dist += 0.5
    ground = min(E.ground_z(e.x, e.y) for e in ends + [c])
    z0 = ground + SIGN_LIFT
    add_box(bm, c, u, f, SIGN_W / 2, SIGN_T / 2, z0, z0 + SIGN_H, 0)                 # board
    for su in (-0.8, 0.8):                                                         # posts
        add_box(bm, c + u * su + f * (SIGN_T / 2 + 0.05), u, f, 0.04, 0.04, ground - 0.1, z0 + 0.1, 2)
    # chevrons on the front (the side facing the kart), pointing into the turn
    front = c - f * (SIGN_T / 2 + 0.01)
    point = -1 if turn > 0 else 1                         # left turn: chevrons point to the driver's left
    cw, t, vm, vh = 0.34, 0.13, z0 + SIGN_H / 2, SIGN_H * 0.36
    for u0 in (-0.72, 0.0, 0.72):
        tip, back = u0 + point * cw / 2, u0 - point * cw / 2
        for vend in (vm + vh, vm - vh):
            quad = [(tip, vm), (tip - point * t, vm), (back - point * t, vend), (back, vend)]
            face = bm.faces.new([bm.verts.new(front + u * uu + Vector((0, 0, vv))) for uu, vv in quad])
            face.material_index = 1
            face.normal_update()
            if face.normal.dot(-f) < 0:
                face.normal_flip()
    return p["s"]


def build_signs(pts, fr, col):
    bm = bmesh.new()
    placed = [add_sign(bm, pts, fr, i, turn) for i, turn in corners(pts)]
    mesh = bpy.data.meshes.new("Track_CornerSigns")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("sign_white", "#F4F4F0", roughness=0.6))
    mesh.materials.append(E.material("sign_chevron", E.PALETTE["boost_orange"], roughness=0.6))
    mesh.materials.append(E.material("sign_post", E.PALETTE["steel"], roughness=0.4, metallic=0.6))
    print(f"  trackside: corner signs at s = {', '.join(str(round(s)) for s in sorted(placed))} m")
    return E.new_object("Track_CornerSigns", mesh, col)


def build():
    pts = E.route_points()
    if not pts:
        return
    col = E.collection("Track_Surface")
    fr = T.frames(pts)
    build_lines(pts, fr, col)
    build_signs(pts, fr, col)
