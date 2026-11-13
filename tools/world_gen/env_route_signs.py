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

V21a (Luc's second free-roam drive of 13 Nov 2026, luc_notes/2026-11-13_playtest2 p1 / p2), data in start_plan.py:

  grid boxes         ten start positions painted on the home straight, Boost Orange, exactly where RacerGrid puts the
                     karts (slot 0 = the SpawnPoint): an open bracket of 1.7 x 2.3 m, line 0.12 m, open towards the
                     start line, and the position number 1-10 in the gap in front of it. Flush paint in
                     Track_EdgeLines like the start line, 1 cm higher (a drain channel's paint crosses row 2).
  U-turn boards      four left boards (uturn_signs) behind the outside wall of the U-turn before the home straight:
                     U1 straight ahead of the westbound leg, U2 at the apex, U3 and U4 round to where the home
                     straight starts. A sign may now give "lift" (board bottom over the ground; these stand 0.95 m
                     up, over the 0.8 m wall) and "face": "road" (parallel to the road at its route point).
  barriers           the centre lines of the Nova Kart walls package V21b builds in Unity (start_plan.barriers: the
                     median between the two legs, the outside of the U-turn) get their paving heights here by ray
                     and go to out/start_barriers.json; export_unity_bundle.py writes them into track.json
                     "barriers". Nothing is built for them in Blender. The log prints every clearance.
"""
import math

import bmesh
import bpy
from mathutils import Vector

import env_common as E
import start_plan

GAP, FORK_GAP = 1.2, 1.5            # m between a board (or post) and the road edge / a fork lane
SIZES = {   # width, height, lift (bottom over the ground), thickness, post offsets along the board, post half size
    "normal": dict(w=2.4, h=0.8, lift=0.5, t=0.08, posts=(-0.8, 0.8), post=0.04),
    "big": dict(w=3.6, h=1.2, lift=0.6, t=0.10, posts=(-1.3, 1.3), post=0.06),
    "fork": dict(w=6.0, h=2.0, lift=0.9, t=0.14, posts=(-2.5, 0.0, 2.5), post=0.08),
    "overhead": dict(w=3.2, h=1.4, lift=4.0, t=0.12, posts=(0.9,), post=0.09),   # mast 0.9 m right of the middle
    # (V20 integration: 3.2 m wide, 4 m up, so on V20b's axis it stays off the race road and the chase camera passes under)
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
    # V20 integration: V20b replaced V14b's climb by the jump over the courtyard (plaza_plan.sc_point: s along the
    # jump axis, 0 = kicker lip). Run-up with the pad, the kicker, and the flight and run-out into door D4.
    pad_u = (P.PAD_U[0] + P.PAD_U[1]) / 2
    ku = (P.KICK_U[0] + P.KICK_U[1]) / 2
    return [([P.sc_point(s, pad_u) for s in (-60.0, -45.0, P.KICK_START_S)], (P.PAD_U[1] - P.PAD_U[0]) / 2),
            ([P.sc_point(s, ku) for s in (P.KICK_START_S, P.KICK_LIP_S)], (P.KICK_U[1] - P.KICK_U[0]) / 2),
            ([P.sc_point(s, ku) for s in (P.KICK_LIP_S, 30.0)], 2.5)]


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

SHORTCUT_FORK = (-14.0, 2.0)     # P1's middle: plaza_plan.sc_point(s, u) on V20b's jump axis, 3.5 m before the pad, where
                                 # the shortcut leaves the main line; mast 0.9 m further right, at u 2.5 (V20 integration)


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
        c = Vector((*P.sc_point(*SHORTCUT_FORK), 0))
    elif "at" in sign:
        c = Vector((sign["at"][0], sign["at"][1], 0))
    else:
        p, _f, r = _route_frame(*sign["near"])
        c = Vector((p["x"], p["y"], 0)) + r * (sign["side"] * sign["offset"])
    if sign.get("face") == "road":                  # V21a: parallel to the road, read from the road
        return c, r * sign["side"]
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
    z0 = max(grounds) + sign.get("lift", s["lift"])
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


# V21a, p1 ("here we need arrows and a barrier to the left, so you know where the home straight is"): boards on the
# outside of the U-turn, behind the wall start_plan.uturn_outside (V21b builds it). k = route points after the
# bend's first point (of 14); 'back' = metres between the wall's outer face and the board's nearest part.
UTURN_LIFT = 0.95                # board bottom over the paving: clear of the 0.8 m wall in front of it
UTURN_BOARDS = [
    dict(id="U1", k=4, size="big", back=0.35, face_k=-12,
         note="straight ahead of the westbound leg, facing the driver from 24 m before the bend"),
    dict(id="U2", k=7, size="big", back=0.35, note="at the apex, parallel to the wall"),
    dict(id="U3", k=10, size="normal", back=0.35, note="three quarters round, parallel to the wall"),
    dict(id="U4", k=14, size="normal", back=0.35, note="where the home straight starts, parallel to the wall"),
]


def uturn_signs():
    pts = E.route_points()
    n = len(pts)
    first, _last, _c, _r = start_plan.uturn(E.ROUTE)
    out = []
    for b in UTURN_BOARDS:
        i = first + b["k"]
        p = pts[i % n]
        s = SIZES[b["size"]]
        rx, ry = start_plan.right_of(pts, i)
        wall = E.half_width(p) + start_plan.UTURN_GAP + start_plan.WALL_T / 2          # the wall's outer face
        sign = dict(id=b["id"], shot="p1", type="left", size=b["size"], lift=UTURN_LIFT, note=b["note"])
        if "face_k" in b:                           # turned towards the approaching driver: its near end swings in
            q = pts[(first + b["face_k"]) % n]
            sign["face_from"] = (q["x"], q["y"])
            off = wall + b["back"] + s["t"] / 2
            for _ in range(3):                      # the offset depends on the angle and the angle on the offset
                c = Vector((p["x"] + rx * off, p["y"] + ry * off, 0))
                f = (c - Vector((q["x"], q["y"], 0))).normalized()
                swing = abs(f.x * ry - f.y * rx) * s["w"] / 2 + abs(f.x * rx + f.y * ry) * s["t"] / 2
                off = wall + b["back"] + swing
        else:
            sign["face"] = "road"
            off = wall + b["back"] + s["t"] / 2
            sign["side"] = 1
        sign["at"] = (p["x"] + rx * off, p["y"] + ry * off)
        if sign.get("face") == "road":
            sign["near"], sign["offset"] = (p["x"], p["y"]), off
            del sign["at"]
        out.append(sign)
    return out


def build_signs(col):
    bm = bmesh.new()
    lines = []
    signs = SIGNS + uturn_signs()
    for sign in signs:
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
    print(f"  route signs (V20a, V21a): {len(signs)} boards, {tris} triangles")
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


# ---------------------------------------------------------------------------------------------------------------
# V21a: start grid boxes (p2: "we still need small start positions marked on the ground. Make them orange too.
# Say 10 positions.")
# ---------------------------------------------------------------------------------------------------------------
GRID_LIFT = 0.05                 # over the road: 1 cm above the other flush paint (drain channel under row 2)
DIGIT_H, DIGIT_W, DIGIT_T = 0.44, 0.24, 0.065      # position numbers: seven-segment digits, read from behind
DIGIT_GAP = 0.13                 # between the bracket's open end and its number; also between the digits of "10"
_SEG = {"a": (0, 1, "h"), "g": (0, 0, "h"), "d": (0, -1, "h"),           # (u, v in half units, across or upright)
        "f": (-1, 0.5, "v"), "b": (1, 0.5, "v"), "e": (-1, -0.5, "v"), "c": (1, -0.5, "v")}
_DIGITS = {"0": "abcdef", "1": "bc", "2": "abged", "3": "abgcd", "4": "fgbc", "5": "afgcd", "6": "afgedc",
           "7": "abc", "8": "abcdefg", "9": "abcdfg"}


def _digit_rects(ch, u0, v0):
    """Rectangles (u_min, u_max, v_min, v_max) of one digit centred at (u0, v0); u = driver's right, v = ahead."""
    hu, hv, t = (DIGIT_W - DIGIT_T) / 2, (DIGIT_H - DIGIT_T) / 2, DIGIT_T / 2
    out = []
    for seg in _DIGITS[ch]:
        su, sv, kind = _SEG[seg]
        cu, cv = u0 + su * hu, v0 + sv * hv
        if kind == "h":
            out.append((u0 - hu - t, u0 + hu + t, cv - t, cv + t))
        else:
            out.append((cu - t, cu + t, cv - hv / 2 - t, cv + hv / 2 + t))
    return out


def grid_rects(slot):
    """The paint of one slot in its own frame (u right, v ahead, origin = the kart's position)."""
    hw, hl, t = start_plan.BOX_W / 2, start_plan.BOX_L / 2, start_plan.BOX_LINE
    rects = [(-hw, hw, -hl, -hl + t), (-hw, -hw + t, -hl + t, hl), (hw - t, hw, -hl + t, hl)]   # back, left, right
    text = str(slot + 1)
    v0 = hl + DIGIT_GAP + DIGIT_H / 2
    total = len(text) * DIGIT_W + (len(text) - 1) * DIGIT_GAP
    for k, ch in enumerate(text):
        rects += _digit_rects(ch, -total / 2 + DIGIT_W / 2 + k * (DIGIT_W + DIGIT_GAP), v0)
    return rects


def add_grid_boxes():
    """Ten Boost Orange start boxes with their numbers, into Track_EdgeLines (flush paint, no collider)."""
    obj = bpy.data.objects.get("Track_EdgeLines")
    if obj is None:
        print("  grid boxes (V21a): no Track_EdgeLines, left out")
        return
    mesh = obj.data
    orange = E.material("sign_chevron", E.PALETTE["boost_orange"], roughness=0.6)
    if orange.name not in [m.name for m in mesh.materials if m]:
        mesh.materials.append(orange)
    slot_index = [m.name if m else "" for m in mesh.materials].index(orange.name)
    slots = start_plan.grid_slots(E.ROUTE)
    side, row = start_plan.grid_numbers()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    made, lines, worst = 0, [], 1e9
    gate_p, gate_f, _r = start_frame()
    for s in slots:
        c = Vector((s["x"], s["y"], 0))
        f, r = Vector((*s["forward"], 0)), Vector((*s["right"], 0))
        z = s["track_z"] + ROAD_LIFT + GRID_LIFT
        rects = grid_rects(s["slot"])
        for u0, u1, v0, v1 in rects:
            face = bm.faces.new([bm.verts.new((*(c + r * u + f * v)[:2], z))
                                 for u, v in ((u0, v0), (u1, v0), (u1, v1), (u0, v1))])
            face.material_index = slot_index
            face.normal_update()
            if face.normal.z < 0:
                face.normal_flip()
            made += 1
        corners = [c + r * u + f * v for u0, u1, v0, v1 in rects for u in (u0, u1) for v in (v0, v1)]
        edge = min(road_clearance(q.x, q.y) for q in corners)            # negative = on the road
        worst = min(worst, -edge)
        ahead = min((Vector((gate_p["x"], gate_p["y"], 0)) - q).dot(gate_f) for q in corners)
        lines.append(f"    slot {s['slot']} (number {s['slot'] + 1}) at ({s['x']:.2f}, {s['y']:.2f}), paint "
                     f"{-edge:.2f} m inside the road edge, {ahead:.2f} m before the start line")
    bm.to_mesh(mesh)
    bm.free()
    print(f"  grid boxes (V21a): {len(slots)} boxes {start_plan.BOX_W} x {start_plan.BOX_L} m (line "
          f"{start_plan.BOX_LINE} m) with numbers, {made} quads in Track_EdgeLines, {GRID_LIFT * 100:.0f} cm over the "
          f"road; RacerGrid {side:g} m sideways, {row:g} m per row (RaceSettings.asset); all paint at least "
          f"{worst:.2f} m inside the road edge")
    print("\n".join(lines))


# ---------------------------------------------------------------------------------------------------------------
# V21a: barrier centre lines for package V21b's Nova Kart walls (heights by ray, checks, out/start_barriers.json)
# ---------------------------------------------------------------------------------------------------------------
BARRIER_STEP = 1.0               # m between height samples along a barrier
BARRIER_Z_TOL = 0.02             # m: a sample is kept where the paving leaves the straight line between its neighbours
WALL_H = 0.8                     # the Nova Kart wall's height (BRIEF_V21): for the obstacle check only


def _resample(points, step):
    out = [Vector((*points[0], 0))]
    for a, b in zip(points, points[1:]):
        a, b = Vector((*a, 0)), Vector((*b, 0))
        k = max(1, int(math.ceil((b - a).length / step - 1e-6)))
        out += [a + (b - a) * (j / k) for j in range(1, k + 1)]
    return out


def _thin(samples, corners):
    """Keeps the ends, the polyline's own corners, and as few samples between them as keep every dropped sample's
    height within BARRIER_Z_TOL of the straight line between the kept ones."""
    def fits(i, j):
        a, b = samples[i], samples[j]
        span = max((b.xy - a.xy).length, 1e-9)
        return all(abs(a.z + (b.z - a.z) * (samples[m].xy - a.xy).length / span - samples[m].z) <= BARRIER_Z_TOL
                   for m in range(i + 1, j))
    keep, last = [0], len(samples) - 1
    while keep[-1] < last:
        i = j = keep[-1]
        while j < last and fits(i, j + 1):
            j += 1
            if j in corners:
                break
        keep.append(max(j, i + 1))
    return [samples[i] for i in keep]


def _obstacles(a, b, ground):
    """Names of built things (not the ground) that reach into the wall's box between two samples."""
    scene, deps = bpy.context.scene, bpy.context.evaluated_depsgraph_get()
    side = Vector(((b - a).y, -(b - a).x, 0)).normalized() * (start_plan.WALL_T / 2)
    found = set()
    steps = max(1, int((b - a).length / 0.25))
    for j in range(steps + 1):
        p = a + (b - a) * (j / steps)
        for off in (-1, 0, 1):
            q = p + side * off
            hit, loc, _n, _i, obj, _m = scene.ray_cast(deps, Vector((q.x, q.y, ground + WALL_H + 0.05)),
                                                       Vector((0, 0, -1)), distance=WALL_H)
            if hit and loc.z > ground + 0.08 and not obj.name.startswith(("Track_RouteGuide", "RouteGuide")) \
                    and not (obj.users_collection and obj.users_collection[0].name == "Track_RouteGuide"):
                found.add(obj.name)
    return found


def write_barriers():
    import json
    import os
    pts = E.route_points()
    gate_p, gate_f, gate_r = start_frame()
    pylons = [Vector((gate_p["x"], gate_p["y"], 0)) + gate_r * (s * GATE_HALF) for s in (-1, 1)]
    boxes = []
    for s in start_plan.grid_slots(E.ROUTE):
        c, f, r = Vector((s["x"], s["y"], 0)), Vector((*s["forward"], 0)), Vector((*s["right"], 0))
        boxes += [c + r * u + f * v for u0, u1, v0, v1 in grid_rects(s["slot"]) for u in (u0, u1) for v in (v0, v1)]
    out, lines = [], []
    for bar in start_plan.barriers(E.ROUTE):
        corners_xy = bar["points"]
        samples = _resample(corners_xy, BARRIER_STEP)
        corners = {i for i, q in enumerate(samples) if any((q.xy - Vector(c)).length < 1e-6 for c in corners_xy)}
        z_ref = E.nearest_route_point(samples[0].x, samples[0].y)["track_z"]
        for q in samples:
            q.z = ground_at(q.x, q.y, z_ref)
        kept = _thin(samples, corners)
        length = sum((b.xy - a.xy).length for a, b in zip(samples, samples[1:]))
        road = min(start_plan.road_clearance(q.x, q.y, E.ROUTE) for q in samples) - start_plan.WALL_T / 2
        seg = lambda q, pts_: min(_seg_dist((q.x, q.y), (a.x, a.y), (b.x, b.y)) for a, b in zip(pts_, pts_[1:]))
        gate = min(seg(p, samples) for p in pylons) - PYLON - start_plan.WALL_T / 2
        grid = min(seg(p, samples) for p in boxes) - start_plan.WALL_T / 2
        hits = set()
        for a, b in zip(samples, samples[1:]):
            hits |= _obstacles(a, b, max(a.z, b.z))
        zs = [q.z for q in samples]
        warn = "WARNING " if hits or road < 0.8 or gate < 1.0 or grid < 1.0 else ""
        lines.append(f"    {warn}{bar['name']}: {len(kept)} points, {length:.1f} m, paving {min(zs):.2f}-{max(zs):.2f} m; "
                     f"wall face to road edge {road:.2f} m, to the gate's pylons {gate:.2f} m, to the grid paint "
                     f"{grid:.2f} m; in its way: {', '.join(sorted(hits)) or 'nothing'}")
        out.append({"name": bar["name"], "points": [[round(q.x, 3), round(q.y, 3), round(q.z, 3)] for q in kept]})
    path = os.path.join(E.OUT, "start_barriers.json")
    json.dump({"note": "V21a: centre lines of the Nova Kart walls at the start plaza, generator metres "
                       "[x east, y north, z paving height]. env_route_signs.write_barriers -> "
                       "export_unity_bundle.barriers -> track.json 'barriers' (Unity axes).",
               "barriers": out}, open(path, "w"), indent=1)
    print(f"  barriers (V21a): {len(out)} wall centre lines for V21b -> out/start_barriers.json")
    print("\n".join(lines))
    return out


def build():
    if not E.route_points():
        return
    col = E.collection("Track_Surface")
    bpy.context.view_layer.update()          # the rays below need every built mesh in the depsgraph
    build_signs(col)
    build_start_gate(col)
    add_start_line()
    add_grid_boxes()                         # V21a
    write_barriers()                         # V21a
