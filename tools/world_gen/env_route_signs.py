"""Route signs and the start gate (V20a, Luc's playtest drive of 13 Nov 2026), exported with the track.

Luc's notes (atlas luc_notes/2026-11-13_playtest, s01-s20) asked for boards that tell the driver where the lap goes,
and for a start gate. Everything here is data first (SIGNS), then a few boxes per board, in the style of
env_trackside's corner signs (white boards, Boost Orange chevrons, steel posts) and with the same three materials:

  Track_RouteSigns   the boards of SIGNS. Types: "left" / "right" (three chevrons pointing that way, as the
                     approaching driver reads them) and "two_way" (two chevrons each way, "<< >>", for a fork).
                     Sizes: normal (2.4 x 0.8 m, like the corner signs), big (3.6 x 1.2 m), fork (6.0 x 2.0 m on
                     three posts, 0.9 m up) and overhead (4.4 x 1.6 m on one slim mast, board 3.0-4.6 m up: it may
                     reach over the shortcut's lane, never over the race road; only the mast stands between them).
  Track_StartGate    two white pylons 6 m either side of the start/finish line (1.65 m clear of the road edge) with
                     Boost Orange caps and a banner between them 5.4 to 7.0 m over the road (START, NOVA KART,
                     chequered border: make_textures.py start_banner). One new material, W_start_banner.
  start line         a chequered band 1 m deep across the road at checkpoint 0 (16 x 2 squares of 0.5 m), flush paint
                     4 cm over the road, added to Track_EdgeLines (no collider in Unity, the one mesh qa_geometry.py
                     allows on the road) with its white and graphite materials.

A placement gives the board's centre either as "at" (x, y) or as "near" (x, y) + "side" (+1 right of the driving
direction, -1 left) + "offset" (m from the centreline at the route point nearest to "near"), and the way it faces
either as "face_from" (x, y: the place the driver reads it from) or as "yaw" (bearing, degrees clockwise from north,
from the board towards the driver). Positions are generator metres (x east, y north) = track.json x / z.
Every board is checked against the race line (route.json, its half width) and against the extra lanes in LANES
(the plaza shortcut, the s09 walkway branch): the log prints the clearance of each board, and a WARNING where it is
less than GAP (1.2 m) or, for fork boards, FORK_GAP (1.5 m). Boards stand on the ground found by a ray (vegetation and
props ignored). Signs inside the hall are package C's (atrium arrows), not here.
"""
import math

import bmesh
import bpy
from mathutils import Vector

import env_common as E

GAP, FORK_GAP = 1.2, 1.5            # m between a board (or post) and the road edge / a fork lane
SIZES = {   # width, height, lift (bottom over the ground), thickness, post offsets along the board, post half size
    "normal": dict(w=2.4, h=0.8, lift=0.5, t=0.08, posts=(-0.8, 0.8), post=0.04),
    "big": dict(w=3.6, h=1.2, lift=0.6, t=0.10, posts=(-1.3, 1.3), post=0.06),
    "fork": dict(w=6.0, h=2.0, lift=0.9, t=0.14, posts=(-2.5, 0.0, 2.5), post=0.08),
    "overhead": dict(w=4.4, h=1.6, lift=3.0, t=0.12, posts=(0.9,), post=0.09),   # mast 0.9 m right of the middle
}

# Lanes that are not the race line but must stay clear: lists of (centre polyline, half width in m).
# shortcut: the V14b courtyard shortcut on the upper plaza (plaza_plan: axis bearing 338 from the stairs' top). Its
#   run-up and painted pad: u -2.6..0.8 (centre u -0.9, half 1.7) up to the kicker; the kicker u -3.1..1.3 (half 2.2)
#   to its lip.
# s09_walkway: the left branch of the fork with the yellow markings (s09): the paved walkway north of the lawn with
#   the umbrella pines, from the race band's bollard line east to x -100, then south-east to the calcada before the
#   beach cafe, where it meets the race line again (both branches reach checkpoint 8 before the beach tunnel).
LANES = {
    "s09_walkway": [([(-136.0, -129.0), (-128.0, -131.2), (-110.0, -131.4), (-101.0, -132.2), (-96.5, -137.5),
                      (-91.5, -142.5), (-90.0, -146.0), (-88.5, -148.5)], 2.5)],
}


def _shortcut_lane():
    import plaza_plan as P
    return [([P.climb_point(s, -0.9) for s in (-60.0, -45.0, P.KICK_START_S)], 1.7),
            ([P.climb_point(s, -0.9) for s in (P.KICK_START_S, P.KICK_LIP_S)], 2.2)]


# Luc's places, in driving order. 'shot' = his screenshot; idx = the final track.json index nearby (V20 build).
SIGNS = [
    # s05 roundabout: "make these 'to the right' signs so that nobody turns left in the roundabout by accident"
    dict(id="RB1", shot="s05", type="right", size="big", at=(-100.5, -55.5), face_from=(-97.0, -27.0),
         note="on the island's north-east rim, facing the north arm (idx 162-168)"),
    dict(id="RB2", shot="s05", type="right", size="big", at=(-91.5, -44.0), face_from=(-97.3, -28.9),
         note="on the splitter island where a wrong left turn would go (east side of the ring)"),
    # s07 hairpin: "put more left signs here so that nobody drives straight on"
    dict(id="H2", shot="s07", type="left", size="normal", near=(-219.0, -24.0), side=-1, offset=7.5,
         face_from=(-208.2, -29.3), note="early, on the verge left of the Marginal at idx ~232"),
    dict(id="H1", shot="s07", type="left", size="big", at=(-253.0, -15.5), face_from=(-228.4, -20.6),
         note="straight ahead where a kart that misses the hairpin would go on along the Marginal"),
    # s09 fork with the yellow markings: one big two-way board in the middle
    dict(id="F1", shot="s09", type="two_way", size="fork", at=(-118.5, -138.5), face_from=(-156.4, -116.5),
         note="on the lawn wedge between the walkway (left branch) and the race band (right branch)"),
    # ... and a series along the right branch towards the beach tunnel (with the corner signs at s 686 and 762)
    dict(id="F2", shot="s09", type="left", size="normal", near=(-125.4, -147.3), side=1, offset=7.5,
         face_from=(-137.8, -131.7), note="sea side of the bend at idx ~338, before the corner sign at s 686"),
    # s10: "here before the cafe a sign to the left"
    dict(id="F3", shot="s10", type="left", size="big", at=(-65.0, -152.0), face_from=(-85.9, -152.5),
         note="on the paving in front of the beach cafe's terrace, right of the left turn at idx 368-376"),
    # s12: "put an arrow to the left in front of the tables"
    dict(id="T1", shot="s12", type="left", size="big", at=(120.2, -26.6), face_from=(95.3, -44.5),
         note="lower plaza, outside the left turn at idx 480-486, in front of the picnic tables"),
    # s14: "again a sign that you can go left and right" (shortcut left, main line right)
    dict(id="P1", shot="s14", type="two_way", size="overhead", at="shortcut_fork", face_from=(98.85, 14.41),
         note="one mast between the shortcut's pad lane and the main line, board 3 m up, facing checkpoint 10"),
    # s19 / s20 north plaza: "shortly before the cars please arrows to the left, and after turning left again to the left"
    dict(id="N1", shot="s19", type="left", size="big", at=(84.5, 163.5), face_from=(69.6, 159.8),
         note="in front of the parked cars, facing the atrium exit door D5"),
    dict(id="N2", shot="s20", type="left", size="big", at=(82.5, 190.5), face_from=(79.6, 172.0),
         note="outside the second left turn (idx 616-626), after the corner sign at s 1186"),
]

SHORTCUT_FORK = (-24.0, 1.6)     # P1's middle: plaza_plan.climb_point(s, u), between the pad's end and the kicker;
                                 # its mast stands 0.9 m further right, at u 2.5: 1.6 m from the pad lane and the road


def _seg_dist(p, a, b):
    ax, ay = a
    dx, dy = b[0] - ax, b[1] - ay
    ll = dx * dx + dy * dy
    t = 0.0 if ll == 0 else max(0.0, min(1.0, ((p[0] - ax) * dx + (p[1] - ay) * dy) / ll))
    return math.hypot(p[0] - ax - t * dx, p[1] - ay - t * dy)


def road_clearance(x, y):
    """Metres from (x, y) to the race road's edge (negative = on the road), along the closed route.json polyline."""
    pts = E.route_points()
    n = len(pts)
    best = float("inf")
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        d = _seg_dist((x, y), (a["x"], a["y"]), (b["x"], b["y"])) - E.half_width(a)
        best = min(best, d)
    return best


def lane_clearance(x, y, lane):
    return min(_seg_dist((x, y), a, b) - half for line, half in lane for a, b in zip(line, line[1:]))


def _route_frame(x, y):
    pts = E.route_points()
    n = len(pts)
    i = min(range(n), key=lambda k: (pts[k]["x"] - x) ** 2 + (pts[k]["y"] - y) ** 2)
    a, b = pts[(i - 1) % n], pts[(i + 1) % n]
    f = Vector((b["x"] - a["x"], b["y"] - a["y"], 0)).normalized()
    return pts[i], f, Vector((f.y, -f.x, 0))


def resolve(sign):
    """Board centre (Vector, z 0) and f = unit vector from the driver towards the board (ground plane)."""
    if sign.get("at") == "shortcut_fork":
        import plaza_plan as P
        c = Vector((*P.climb_point(*SHORTCUT_FORK), 0))
    elif "at" in sign:
        c = Vector((sign["at"][0], sign["at"][1], 0))
    else:
        p, _f, r = _route_frame(*sign["near"])
        c = Vector((p["x"], p["y"], 0)) + r * (sign["side"] * sign["offset"])
    if "yaw" in sign:
        yaw = math.radians(sign["yaw"])
        f = -Vector((math.sin(yaw), math.cos(yaw), 0))
    else:
        f = (c - Vector((sign["face_from"][0], sign["face_from"][1], 0))).normalized()
    return c, f


_SKIP = ("Veg_", "Prop_", "Track_RouteSigns", "Track_StartGate", "Hero", "DriveCam")


def ground_at(x, y, z_ref):
    """Highest built surface under (x, y) below z_ref + 3 m (vegetation, props and the route guide ignored)."""
    scene = bpy.context.scene
    deps = bpy.context.evaluated_depsgraph_get()
    origin = Vector((x, y, z_ref + 3.0))
    for _ in range(12):
        hit, loc, _n, _i, obj, _m = scene.ray_cast(deps, origin, Vector((0, 0, -1)), distance=40.0)
        if not hit:
            break
        if obj.name.startswith(_SKIP) or (obj.users_collection and obj.users_collection[0].name == "Track_RouteGuide") \
                or obj.hide_render and not obj.get("collider_only"):
            origin = loc - Vector((0, 0, 0.01))
            continue
        return loc.z
    return E.ground_z(x, y)


def add_box(bm, centre, u, f, half_u, half_f, z0, z1, mat):
    vs = []
    for z in (z0, z1):
        for su, sf in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs.append(bm.verts.new(centre + u * (su * half_u) + f * (sf * half_f) + Vector((0, 0, z))))
    made = []
    for idx in [(0, 1, 2, 3), (4, 7, 6, 5)] + [(k, 4 + k, 4 + (k + 1) % 4, (k + 1) % 4) for k in range(4)]:
        made.append(bm.faces.new([vs[k] for k in idx]))
        made[-1].material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=made)
    return made


def chevron(bm, front, u, f, u0, point, vm, h):
    """One chevron (two slanted bars) centred at u0 across the board, tip towards point (-1 driver's left, +1 right)."""
    cw, t, vh = 0.425 * h, 0.1625 * h, 0.36 * h
    tip, back = u0 + point * cw / 2, u0 - point * cw / 2
    for vend in (vm + vh, vm - vh):
        quad = [(tip, vm), (tip - point * t, vm), (back - point * t, vend), (back, vend)]
        face = bm.faces.new([bm.verts.new(front + u * uu + Vector((0, 0, vv))) for uu, vv in quad])
        face.material_index = 1
        face.normal_update()
        if face.normal.dot(-f) < 0:
            face.normal_flip()


def add_board(bm, sign, c, f):
    s = SIZES[sign["size"]]
    u = Vector((f.y, -f.x, 0))                                  # the driver's right
    z_ref = E.nearest_route_point(c.x, c.y)["track_z"]
    feet = [c + u * pu for pu in s["posts"]]
    grounds = [ground_at(p.x, p.y, z_ref) for p in feet]
    z0 = max(grounds) + s["lift"]
    add_box(bm, c, u, f, s["w"] / 2, s["t"] / 2, z0, z0 + s["h"], 0)                       # board (white)
    for p, g in zip(feet, grounds):                                                        # posts behind it
        add_box(bm, p + f * (s["t"] / 2 + s["post"] + 0.01), u, f, s["post"], s["post"], g - 0.1, z0 + s["h"] * 0.6, 2)
    front = c - f * (s["t"] / 2 + 0.01)
    vm, h = z0 + s["h"] / 2, s["h"]
    if sign["type"] == "two_way":
        sp = 0.8 * h
        for u0, point in ((-1.5 * sp, -1), (-0.5 * sp, -1), (0.5 * sp, 1), (1.5 * sp, 1)):
            chevron(bm, front, u, f, u0, point, vm, h)
    else:
        point = -1 if sign["type"] == "left" else 1
        sp = 0.9 * h
        for u0 in (-sp, 0.0, sp):
            chevron(bm, front, u, f, u0, point, vm, h)
    return u, feet, grounds, z0


def check(sign, c, f, u, feet):
    """Clearances of the board footprint (or, for an overhead board, its mast) to the road edge and the lanes."""
    s = SIZES[sign["size"]]
    if sign["size"] == "overhead":
        probes = list(feet)
        pad = s["post"]
    else:
        probes = [c + u * (su * s["w"] / 2) + f * (sf * s["t"] / 2) for su in (-1, 1) for sf in (-1, 1)] + feet
        pad = 0.0
    road = min(road_clearance(p.x, p.y) for p in probes) - pad
    lanes = dict(LANES)
    lanes["shortcut"] = _shortcut_lane()
    lane = {k: min(lane_clearance(p.x, p.y, ln) for p in probes) - pad for k, ln in lanes.items()}
    near = {k: v for k, v in lane.items() if v < 6.0}
    need = FORK_GAP if sign["type"] == "two_way" else GAP
    low = road < need or any(v < need for v in near.values())
    if sign["size"] == "overhead":                 # the board itself must not reach over the race road
        over = min(road_clearance(p.x, p.y) for p in (c + u * (s["w"] / 2), c - u * (s["w"] / 2)))
        low = low or over < 0.0
    else:
        over = None
    return road, near, low, over


def build_signs(col):
    bm = bmesh.new()
    lines = []
    for sign in SIGNS:
        c, f = resolve(sign)
        u, feet, grounds, z0 = add_board(bm, sign, c, f)
        road, near, low, over = check(sign, c, f, u, feet)
        yaw = math.degrees(math.atan2(-f.x, -f.y)) % 360
        lane_txt = "".join(f", {k} {v:.2f} m" for k, v in near.items())
        over_txt = f", board edge to road edge {over:.2f} m" if over is not None else ""
        lines.append(f"    {'WARNING ' if low else ''}{sign['id']:4s} {sign['shot']} {sign['type']:7s} {sign['size']:8s} "
                     f"({c.x:.2f}, {c.y:.2f}) faces {yaw:3.0f} deg, ground {min(grounds):.2f} m, "
                     f"road edge {road:.2f} m{lane_txt}{over_txt}")
    mesh = bpy.data.meshes.new("Track_RouteSigns")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("sign_white", "#F4F4F0", roughness=0.6))
    mesh.materials.append(E.material("sign_chevron", E.PALETTE["boost_orange"], roughness=0.6))
    mesh.materials.append(E.material("sign_post", E.PALETTE["steel"], roughness=0.4, metallic=0.6))
    obj = E.new_object("Track_RouteSigns", mesh, col)
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    print(f"  route signs (V20a): {len(SIGNS)} boards, {tris} triangles")
    print("\n".join(lines))
    return obj


# ---------------------------------------------------------------------------------------------------------------
# Start gate and start line (s01: "start line with a chequered flag and in general a gate with a bold START on it
# and Nova Kart")
# ---------------------------------------------------------------------------------------------------------------
GATE_HALF = 6.0                  # m from the centreline to each pylon's middle (road half width 4 m)
PYLON = 0.35                     # half size of a pylon
GATE_CLEAR, BANNER_H, BANNER_T = 5.4, 1.6, 0.3     # banner bottom over the road, height, thickness
CAP = 0.4                        # Boost Orange cap on each pylon
LINE_SQ, LINE_ROWS, LINE_LIFT = 0.5, 2, 0.04       # chequer squares, rows along the road, height over the road
ROAD_LIFT = 0.06                 # env_track.ROAD_LIFT


def banner_material():
    mat = bpy.data.materials.get("W_start_banner")
    if mat:
        return mat
    mat = E.material("start_banner", "#FFFFFF", roughness=0.6)
    import os
    path = os.path.join(E.TEXTURES, "start_banner_albedo.png")
    if os.path.exists(path):                    # make_textures.py start_banner; without it the banner stays white
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(path, check_existing=True)
        tex.interpolation = "Linear"
        links.new(tex.outputs["Color"], nodes.get("Principled BSDF").inputs["Base Color"])
    return mat


def start_frame():
    pts = E.route_points()
    i = E.ROUTE.get("start_index", 0)
    p, a, b = pts[i], pts[i - 1], pts[(i + 1) % len(pts)]
    f = Vector((b["x"] - a["x"], b["y"] - a["y"], 0)).normalized()
    return p, f, Vector((f.y, -f.x, 0))


def build_start_gate(col):
    p, f, r = start_frame()
    c = Vector((p["x"], p["y"], 0))
    road = p["track_z"] + ROAD_LIFT
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    top = road + GATE_CLEAR + BANNER_H
    for side in (-1, 1):
        foot = c + r * (side * GATE_HALF)
        g = ground_at(foot.x, foot.y, p["track_z"])
        add_box(bm, foot, r, f, PYLON, PYLON, g - 0.1, top + 0.2, 0)                       # white pylon
        add_box(bm, foot, r, f, PYLON + 0.05, PYLON + 0.05, top + 0.2, top + 0.2 + CAP, 1)  # orange cap
        add_box(bm, foot, r, f, PYLON + 0.05, PYLON + 0.05, g - 0.1, g + 0.5, 1)            # orange foot band
    inner = GATE_HALF - PYLON
    faces = add_box(bm, c, r, f, inner, BANNER_T / 2, road + GATE_CLEAR, top, 0)
    for face in faces:                           # front and back carry the banner texture, read the right way round
        nz = face.normal
        if abs(nz.dot(f)) < 0.9:
            continue
        face.material_index = 2
        viewer_right = r if nz.dot(f) < 0 else -r
        for loop in face.loops:
            co = loop.vert.co - c
            loop[uv].uv = ((co.dot(viewer_right) + inner) / (2 * inner), (co.z - road - GATE_CLEAR) / BANNER_H)
    mesh = bpy.data.meshes.new("Track_StartGate")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("sign_white", "#F4F4F0", roughness=0.6))
    mesh.materials.append(E.material("sign_chevron", E.PALETTE["boost_orange"], roughness=0.6))
    mesh.materials.append(banner_material())
    obj = E.new_object("Track_StartGate", mesh, col)
    tris = sum(len(q.vertices) - 2 for q in mesh.polygons)
    clear = min(road_clearance(*(c + r * (s * (GATE_HALF - PYLON)))[:2]) for s in (-1, 1))
    print(f"  start gate (V20a): pylons {GATE_HALF:.1f} m either side of the line at ({p['x']:.2f}, {p['y']:.2f}), "
          f"{clear:.2f} m clear of the road edge; banner {GATE_CLEAR:.1f}-{GATE_CLEAR + BANNER_H:.1f} m over the road; "
          f"{tris} triangles")
    return obj


def add_start_line():
    """The chequered band across the road at checkpoint 0, into Track_EdgeLines (flush paint, no collider)."""
    obj = bpy.data.objects.get("Track_EdgeLines")
    if obj is None:
        print("  start line (V20a): no Track_EdgeLines, left out")
        return
    mesh = obj.data
    slots = {m.name: k for k, m in enumerate(mesh.materials) if m}
    white, dark = slots.get("W_edge_line_white"), slots.get("W_edge_line_graphite")
    p, f, r = start_frame()
    c = Vector((p["x"], p["y"], 0))
    half = E.half_width(p)
    n_across = int(round(2 * half / LINE_SQ))
    z = p["track_z"] + ROAD_LIFT + LINE_LIFT
    band = [c + f * (LINE_SQ * (k - LINE_ROWS / 2)) for k in range(LINE_ROWS + 1)]
    bm = bmesh.new()
    bm.from_mesh(mesh)
    # older flush paint under the band (the entrance plaza's drain channel crosses the road right at the line) would
    # lie in the same plane: the start line is painted over it, so its pieces inside the band go
    inside = lambda v: abs((v.co - c).dot(f)) <= LINE_SQ * LINE_ROWS / 2 + 0.02 and abs((v.co - c).dot(r)) <= half + 0.3
    under = [q for q in bm.faces if any(inside(v) for v in q.verts)]
    bmesh.ops.delete(bm, geom=under, context="FACES")
    clash = len(under)
    made = 0
    for row in range(LINE_ROWS):
        for k in range(n_across):
            a0 = -half + k * LINE_SQ
            corners = [band[row] + r * a0, band[row] + r * (a0 + LINE_SQ), band[row + 1] + r * (a0 + LINE_SQ),
                       band[row + 1] + r * a0]
            face = bm.faces.new([bm.verts.new((q.x, q.y, z)) for q in corners])
            face.material_index = white if (row + k) % 2 == 0 else dark
            face.normal_update()
            if face.normal.z < 0:
                face.normal_flip()
            made += 1
    bm.to_mesh(mesh)
    bm.free()
    print(f"  start line (V20a): {made} chequer squares ({LINE_SQ} m) across the road at checkpoint 0 in Track_EdgeLines; "
          f"{clash} faces of older flush paint (drain channel) under the band removed")


def build():
    if not E.route_points():
        return
    col = E.collection("Track_Surface")
    bpy.context.view_layer.update()          # the rays below need every built mesh in the depsgraph
    build_signs(col)
    build_start_gate(col)
    add_start_line()
