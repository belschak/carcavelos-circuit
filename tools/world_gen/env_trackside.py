"""Trackside readability: painted edge lines and chevron corner signs, exported with the track.

On the light plazas, the promenade and the wide asphalt of the coastal road the road edge was hard to read
from the chase camera. Two cheap aids, both in collection Track_Surface (TrackSurface.fbx):
  Track_EdgeLines    0.18 m painted line 0.25 m inside each road edge, 4 cm above the road. White on dark
                     surfaces (asphalt, tunnel floor, terracotta), graphite on the light ones (sand, light and
                     polished concrete, calcada). Left out in the tunnel (its footways mark the edge), across the
                     jump drop and on a side where a kerb already marks a tight corner (env_track.build_kerbs rule).
                     V18g: only inside the hall (env_track.RACE_LINE_ZONES); everywhere else the road carries no race
                     paint, only the real markings below. V18r: none on the roundabout (street_plan.RB_NO_LINES); there the mesh carries the roundabout's
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
    return [set(T.race_kerb_sides(pts, i)) for i in range(len(pts))]


def build_lines(pts, fr, col):
    n = len(pts)
    kerbs = kerb_sides(pts)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")           # V18g: only plaza_paint's faces use it (textured materials)
    for side in (1, -1):
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            if a["tunnel"] or b["tunnel"] or T.is_drop(a, b) or a["zone"] in T.PLAZA_ZONES or b["zone"] in T.PLAZA_ZONES:
                continue                           # V10i: no painted lines on the stairs and the plaza
            if side in kerbs[i] and side in kerbs[(i + 1) % n]:
                continue
            if a["zone"] not in T.RACE_LINE_ZONES:
                continue                           # V18g: race edge lines only inside the hall (the map comes first)
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
    plaza = plaza_paint(bm, uv, first_slot=2 + len(extra))
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
    for mat in plaza:
        mesh.materials.append(mat)
    return E.new_object("Track_EdgeLines", mesh, col)


def plaza_paint(bm, uv, first_slot):
    """V18g: details of the real paving that the race road interrupts, continued across it as flush paint (like
    env_streets.road_paint: draped 4 cm over Track_Road and its shoulders, no collider): the entrance plaza's
    rust-brown drain channels and the dark sett ring of the drop-off forecourt. They use the materials and UVs of
    the pieces beside the road (entrance_drain, the street palette), so nothing new is drawn. Returns the materials."""
    import entrance_plan as EP
    import env_entrance as EN
    import env_north
    import env_streets as ST
    from mathutils.bvhtree import BVHTree
    road = bpy.data.objects.get("Track_Road")
    tree = BVHTree.FromPolygons([road.matrix_world @ v.co for v in road.data.vertices],
                                [tuple(q.vertices) for q in road.data.polygons], epsilon=0.0)
    edge = street_plan.KEEP_OUT - 0.03               # the paving beside the road begins at KEEP_OUT

    def strip(rows, slot, uvs):
        """rows: [(inner point, outer point)] along a band; a piece is drawn where it lies on the road."""
        made = 0
        for (a0, a1), (b0, b1), (ua, ub) in zip(rows, rows[1:], zip(uvs, uvs[1:])):
            cx, cy = (a0[0] + a1[0] + b0[0] + b1[0]) / 4, (a0[1] + a1[1] + b0[1] + b1[1]) / 4
            if street_plan.route_distance(cx, cy) > edge:
                continue
            vs = []
            for x, y in (a0, b0, b1, a1):
                hit = tree.ray_cast(Vector((x, y, 200.0)), Vector((0.0, 0.0, -1.0)), 400.0)
                z = hit[0].z if hit[0] is not None else E.nearest_route_point(x, y)["track_z"] - 0.02   # the shoulder's foot
                vs.append(bm.verts.new((x, y, z + LINE_LIFT)))
            f = bm.faces.new(vs)
            f.material_index = slot
            for loop, t in zip(f.loops, (ua[0], ub[0], ub[1], ua[1])):
                loop[uv].uv = t
            made += 1
        return made
    w, step, drains = EP.CHANNEL_W, 0.25, 0
    for a, d, length in EN._channel_quads():        # the channels as env_entrance lays them (its sheet stops at the road)
        n = (-d[1], d[0])
        o = (a[0] - n[0] * w / 2, a[1] - n[1] * w / 2)
        ts = [k * step for k in range(int(length / step) + 1)]
        rows = [((o[0] + d[0] * t, o[1] + d[1] * t), (o[0] + d[0] * t + n[0] * w, o[1] + d[1] * t + n[1] * w)) for t in ts]
        drains += strip(rows, first_slot, [((t / 0.5, 0.0), (t / 0.5, 1.0)) for t in ts])
    (cx, cy), r1 = env_north.RING_C, env_north.RING_R
    r0, cell = r1 - env_north.RING_BAND, ST.pal_uv("setts_dark")
    rows = [((cx + r0 * math.cos(math.radians(k * 2.0)), cy + r0 * math.sin(math.radians(k * 2.0))),
             (cx + r1 * math.cos(math.radians(k * 2.0)), cy + r1 * math.sin(math.radians(k * 2.0)))) for k in range(181)]
    ring = strip(rows, first_slot + 1, [(cell, cell)] * len(rows))
    print(f"  trackside: real paving details across the race road (flush, in Track_EdgeLines): {drains} quads of the "
          f"entrance plaza's drain channels, {ring} of the forecourt's dark sett ring")
    return [EN.materials()["drain"], ST.mat("palette")]


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
