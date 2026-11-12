"""Track surface along the route (a proposal for Role 2's drivable road, built from route.json).

Objects in collection Track_Surface:
  Track_Road      8 m road, one material per surface type (route_profile.json "surfaces"), UVs in real metres.
                  V18g: outside the hall it shows the real ground under it (ROAD_LOOK: the material and the UV frame
                  of the paving beside it), so the race line is a hidden path through the map
  Track_Verges    2.5 m strips beside the road that blend into the terrain (outdoors only)
  Track_Kerbs     striped kerbs on the inside of tight corners (V18g: off, RACE_KERBS; the object is not built)
  Track_CutWalls  sloped concrete walls where the road runs in a cutting deeper than 1 m
  Track_JumpKicker small kicker ending at the terrace edge
"""
import math

import bmesh
import bpy

import env_common as E

HALF = E.ROUTE_WIDTH / 2
ROAD_LIFT = 0.06            # road surface above the terrain bed
KERB_HEIGHT = 0.012         # painted kerb: flush with the road, it is paint and not a bump
SHOULDER_M = 0.9            # width of the gentle shoulder that takes the road down to its bed (no step at the edge)
VERGE = 2.5                 # metres beside the road
TILE_M = {                  # real-world size of one texture tile (m), so UVs are in tiles
    "asphalt": 2.0, "concrete_sand": 2.0, "concrete_polished": 2.0, "concrete_light": 2.0, "tunnel_floor": 2.0,
    "calcada": 1.0, "terracotta": 1.0, "lawn": 2.0, "kerb": 2.0, "cut_wall": 2.0, "sand": 4.0,
    "street_concrete": 4.0, "street_setts": 2.0,
}
SURFACE_COLOUR = {          # used until textures exist (env_materials upgrades them)
    "asphalt": "asphalt", "concrete_sand": "concrete_sand", "concrete_polished": "#9A9C9E", "concrete_light": "concrete_light",
    "tunnel_floor": "#8E8A82", "calcada": "calcada", "terracotta": "terracotta", "lawn": "lawn", "cut_wall": "concrete_sand",
    "sand": "sand",
}
OUTDOOR_NO_VERGE = {"tunnel", "atrium", "terrace deck"}
KERB_ZONES = {"lawn and paths", "coastal road", "promenade", "lower plaza", "upper plaza", "tunnel plaza"}


# =====================================================================================================
# V18g (2026-11-12): the map comes first. Outside the hall the race road shows what is really under it and carries
# no race paint (no orange kerbs, no race edge lines): the race line is a hidden path through the real place;
# checkpoints, markers and run-off limits are drawn on top later. Colour, material slot and UVs only: every
# vertex of Track_Road stays where it was.
# =====================================================================================================
RACE_KERBS = False                 # the orange/white kerb paint on the inside of tight corners (Track_Kerbs)
RACE_LINE_ZONES = ("atrium",)      # zones that keep the race edge lines: only the hall's interior (Noah's lanes)


def race_kerb_sides(pts, i):
    """Sides (+1 left, -1 right) of route point i that carry a race kerb. One rule for build_kerbs and
    env_trackside.kerb_sides. V18r: none on the roundabout; V18g: none at all while RACE_KERBS is off."""
    import street_plan
    if not RACE_KERBS or pts[i]["zone"] not in KERB_ZONES or street_plan.race_kerbs_off(i):
        return ()
    k = curvature(pts, i)
    radius = 1 / abs(k) if abs(k) > 1e-6 else 1e9
    if radius >= 35:
        return ()
    return (1 if k > 0 else -1,) + ((-1 if k > 0 else 1,) if radius < 20 else ())


def _entrance_look(x, y):
    """Entrance plaza: the big slabs of the band along wing B, else the plaza's calcada."""
    import entrance_plan as P
    return "entrance_planks" if P.in_band(x, y) else "entrance_calcada"


def _east_look(x, y):
    """Lower plaza: the concrete panels in the grid of the ramp's axis, north of the split in the stairs' grid."""
    import campus_east_plan as P
    import campus_plan as C
    north = P.GRID_SPLIT + [(P.GRID_SPLIT[-1][0], 25.0), (P.GRID_SPLIT[0][0], 25.0)]
    return "east_concrete_stair" if C.inside(x, y, north) else "east_concrete_ramp"


# What the road shows, stretch by stretch (first route index, last, surface name or f(x, y) -> name, asked at the
# middle of the road quad and of each shoulder). Stretches not listed: street_plan.SURFACE_RUNS (V10f: car park,
# forecourt, asphalt square), then route.json's "surface". Sources: Google satellite 20z/21z of 12 Nov 2026
# (google_survey/r/), the DGT orthophoto, reports/V18g-r.md.
ROAD_LOOK = [
    (0, 14, _entrance_look),          # start straight on the entrance plaza: calcada, slabs where the band lies under it
    (61, 81, "deck"),                 # CTT roof terrace: the deck's own pale sawn concrete
    (127, 135, "asphalt"),            # from the avenue's kerb on: the avenue's asphalt (the calcada ends with the sidewalk)
    (242, 310, lambda x, y: __import__("env_streets").road_look("slab")),           # V18h-q: car-park concrete slabs
    (311, 320, lambda x, y: __import__("env_streets").road_look("asphalt_worn")),   # V18h-q: the old asphalt square
    (336, 350, "street_concrete"),    # promenade: pale concrete slabs (route.json: asphalt / sand)
    (351, 366, "street_setts"),       # the curved grey sett band above the cove
    (367, 399, "street_concrete"),    # the pale concrete path past the beach bar to the tunnel plaza
    (478, 490, _east_look),           # lower plaza: beige-grey concrete panels with saw-cut joints
    (583, 599, "north_calcada"),      # drop-off forecourt north of the hall: pale setts in rows along the hall's face
    (600, 639, "street_concrete"),    # the pale paved strip behind the hall
    (640, 684, _entrance_look),       # the run-in loop and the start straight on the entrance plaza
]


def _frame(origin, u, su, sv):
    """UVs in a world frame (the neighbouring paving's own: env_streets._layer_uv), so the pattern runs on."""
    (ox, oy), (ux, uy) = origin, u
    return lambda x, y: (((x - ox) * ux + (y - oy) * uy) / su, (-(x - ox) * uy + (y - oy) * ux) / sv)


def _looks():
    """name -> (material, uv(x, y)): surfaces that share the material and the UV frame of the paving beside the road."""
    if _LOOKS:
        return _LOOKS
    import campus_east_plan as CE
    import campus_west_plan as CW
    import entrance_plan as EP
    import env_north
    import env_streets as ST
    import plaza_plan
    calcada = ST._textured("entrance_calcada", EP.CALCADA, "entrance_calcada", 0.9)
    east = ST._textured("campus_east_concrete", CE.PLAZA_CONCRETE, "campus_east_concrete", 0.9)
    _LOOKS.update({
        "entrance_calcada": (calcada, _frame((0.0, 0.0), EP.ROW_DIR, EP.CALCADA_TILE, EP.CALCADA_TILE)),
        "entrance_planks": (ST._textured("entrance_planks", EP.PLANK, "entrance_planks", 0.9),
                            _frame(EP.BAND_O, EP.BAND_U, EP.PLANK_TILE, EP.PLANK_TILE)),
        "north_calcada": (calcada, _frame((0.0, 0.0), env_north.FACE_U, EP.CALCADA_TILE, EP.CALCADA_TILE)),
        "deck": (E.material("B_deck_white", "#E9E7D4", 0.9), _frame((0.0, 0.0), CW.DECK_U, 6.0, 6.0)),   # env_campus_west
        "east_concrete_ramp": (east, _frame((104.5, -37.0), CE.RAMP_DIR, CE.PANEL, CE.PANEL)),      # env_campus_east
        "east_concrete_stair": (east, _frame(plaza_plan.STAIR_FOOT, CE.STAIR_DIR, CE.PANEL, CE.PANEL)),
    })
    return _LOOKS


_LOOKS = {}


def visual_surface(p, x=None, y=None):
    """Surface shown at a route point (or at the point (x, y) beside it): ROAD_LOOK, else the street plan's (V10f:
    pale concrete in the car park, setts in the entry throat), else route.json's. Colour only: route.json and
    every driving value stay as they are."""
    import street_plan
    for i0, i1, look in ROAD_LOOK:
        if i0 <= p["i"] <= i1:
            return look if isinstance(look, str) else look(p["x"] if x is None else x, p["y"] if y is None else y)
    return street_plan.track_surface(p) or p["surface"]


def surface_material(name):
    if name in _looks():
        return _looks()[name][0]
    if name.startswith("street_"):                     # V10f: the same material as the paving beside the road
        import env_streets
        return env_streets.mat(name[len("street_"):])
    colour = SURFACE_COLOUR.get(name, "asphalt")
    hexcol = colour if colour.startswith("#") else E.PALETTE[colour]
    rough = 0.35 if name == "concrete_polished" else 0.9
    return E.material("T_" + name, hexcol, roughness=rough)


def frames(pts):
    """Unit tangent and left normal at every route point (closed loop)."""
    n = len(pts)
    out = []
    for i in range(n):
        a, b = pts[(i - 1) % n], pts[(i + 1) % n]
        dx, dy = b["x"] - a["x"], b["y"] - a["y"]
        d = math.hypot(dx, dy) or 1.0
        out.append(((dx / d, dy / d), (-dy / d, dx / d)))
    return out


def curvature(pts, i, span=3):
    """Signed curvature (1/m) around point i; > 0 = turning left."""
    n = len(pts)
    a, b, c = pts[(i - span) % n], pts[i], pts[(i + span) % n]
    h1 = math.atan2(b["y"] - a["y"], b["x"] - a["x"])
    h2 = math.atan2(c["y"] - b["y"], c["x"] - b["x"])
    turn = (h2 - h1 + math.pi) % (2 * math.pi) - math.pi
    return turn / (span * 2.0)


PLAZA_ZONES = ("stairs", "plaza deck")   # V10i: env_plaza.py builds the driving surface, no road strip
INDOOR = ("atrium", "terrace deck")   # the road lies on a building floor / roof at track_z


def road_z(p):
    """Road surface height. Same lift everywhere: indoors it used to be 2 cm, within z-fighting distance of
    the passage floor and the CTT roof (coplanar check ~1,250 points)."""
    return p["track_z"] + ROAD_LIFT


def is_drop(a, b):
    if "kicker" in (a["zone"], b["zone"]):
        return False                                   # the concrete wedge is a steep but continuous slab
    return abs(a["track_z"] - b["track_z"]) > 1.5


def build_road(pts, fr, col):
    mats, slots = [], {}                           # material slots in order of first use; one slot per material

    def slot(surf):
        name = surface_material(surf).name
        if name not in slots:
            slots[name] = len(mats)
            mats.append(surface_material(surf))
        return slots[name]

    def world_uv(face, surf):
        """V18g: surfaces that continue the paving beside the road take its UV frame (the pattern runs on)."""
        fn = _looks().get(surf, (None, None))[1]
        if fn:
            for loop in face.loops:
                loop[uv].uv = fn(loop.vert.co.x, loop.vert.co.y)
    for p in pts:                                  # the slots of route.json's surfaces first, in the old order
        if p["zone"] not in PLAZA_ZONES:
            slot(visual_surface(p))
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        if is_drop(a, b):
            if E.ROUTE["jump"].get("kicker", {}).get("type") == "ramp":
                # V10i: the landing road starts under the lip (its border edge lies at the foot of the terrace wall,
                # not 2 m out on the lawn where a slow kart would roll over it)
                (_, na), (_, nb) = fr[i], fr[(i + 1) % n]
                zb, h = road_z(b), E.half_width(b)
                q = [bm.verts.new((a["x"] + na[0] * h, a["y"] + na[1] * h, zb)), bm.verts.new((a["x"] - na[0] * h, a["y"] - na[1] * h, zb)),
                     bm.verts.new((b["x"] - nb[0] * h, b["y"] - nb[1] * h, zb)), bm.verts.new((b["x"] + nb[0] * h, b["y"] + nb[1] * h, zb))]
                f = bm.faces.new(q)
                f.material_index = slot(visual_surface(b))
                for loop, c in zip(f.loops, ((0, 0), (4, 0), (4, 1), (0, 1))):
                    loop[uv].uv = c
                world_uv(f, visual_surface(b))
            continue                               # the jump: no road across the drop
        if a["zone"] in PLAZA_ZONES:
            continue                               # V10i: the stairs and the plaza deck are the road there (env_plaza)
        if i in kicker_rows(pts):
            continue                               # V10i: the kicker ramp is the road on its last metres (build_ramp)
        (_, na), (_, nb) = fr[i], fr[(i + 1) % n]
        mid = ((a["x"] + b["x"]) / 2, (a["y"] + b["y"]) / 2)
        nm = ((na[0] + nb[0]) / 2, (na[1] + nb[1]) / 2)
        surf = visual_surface(a, *mid)
        tile = TILE_M.get(surf, 2.0)
        za, zb = road_z(a), road_z(b)
        ha, hb = E.half_width(a), E.half_width(b)      # narrower in the alley (route.json 'width')
        quad = [bm.verts.new((a["x"] + na[0] * ha, a["y"] + na[1] * ha, za)),
                bm.verts.new((a["x"] - na[0] * ha, a["y"] - na[1] * ha, za)),
                bm.verts.new((b["x"] - nb[0] * hb, b["y"] - nb[1] * hb, zb)),
                bm.verts.new((b["x"] + nb[0] * hb, b["y"] + nb[1] * hb, zb))]
        face = bm.faces.new(quad)
        face.material_index = slot(surf)
        sa, sb = a["s"] / tile, (a["s"] + 2.0) / tile
        ua0, ua1 = (HALF - ha) / tile, (HALF + ha) / tile    # u stays centred on the road axis
        ub0, ub1 = (HALF - hb) / tile, (HALF + hb) / tile
        for loop, (u, v) in zip(face.loops, ((ua0, sa), (ua1, sa), (ub1, sb), (ub0, sb))):
            loop[uv].uv = (u, v)
        world_uv(face, surf)
        if not ((a["tunnel"] and b["tunnel"]) or "kicker" in (a["zone"], b["zone"])):
            # Shoulders: the road is 6 cm above its bed. A vertical edge there (or a free edge in mid air) threw
            # karts that came back onto the road. A 0.9 m wide strip now slopes down to just below the bed
            # (about 5 degrees), on both sides, indoors and outdoors.
            def down(p, nrm, h, side):
                o = h + SHOULDER_M
                return bm.verts.new((p["x"] + side * nrm[0] * o, p["y"] + side * nrm[1] * o, p["track_z"] - 0.02))
            # quad = [a left, a right, b right, b left] (left = +normal side)
            for side, top_a, top_b in ((1, quad[0], quad[3]), (-1, quad[1], quad[2])):
                low_a, low_b = down(a, na, ha, side), down(b, nb, hb, side)
                ring = (top_a, top_b, low_b, low_a) if side == 1 else (top_b, top_a, low_a, low_b)
                shoulder = bm.faces.new(ring)
                o = (ha + hb) / 2 + SHOULDER_M / 2     # V18g: the shoulder shows what lies under it
                ssurf = visual_surface(a, mid[0] + side * nm[0] * o, mid[1] + side * nm[1] * o)
                stile = TILE_M.get(ssurf, 2.0)
                shoulder.material_index = slot(ssurf)
                for loop, (u, v) in zip(shoulder.loops, ((0.0, sa * tile / stile), (0.0, sb * tile / stile), (SHOULDER_M / stile, sb * tile / stile), (SHOULDER_M / stile, sa * tile / stile))
                                        if side == 1 else ((0.0, sb * tile / stile), (0.0, sa * tile / stile), (SHOULDER_M / stile, sa * tile / stile), (SHOULDER_M / stile, sb * tile / stile))):
                    loop[uv].uv = (u, v)
                world_uv(shoulder, ssurf)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.01)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new("Track_Road")
    bm.to_mesh(mesh)
    bm.free()
    for mat in mats:
        mesh.materials.append(mat)
    return E.new_object("Track_Road", mesh, col)


def verge_material(p):
    if p["zone"] in ("lawn and paths",):
        return "lawn"
    if p["zone"] == "coastal road":
        return "concrete_light"
    return p["surface"]


def near_portal(p, metres=18.0):
    portals = [q for q in E.route_points() if q["tunnel"]]
    ends = (portals[0], portals[-1]) if portals else ()
    return any(math.hypot(p["x"] - e["x"], p["y"] - e["y"]) < metres for e in ends)


def build_verges(pts, fr, col):
    walls = getattr(build_cut_walls, "walls", {1: [0] * len(pts), -1: [0] * len(pts)})
    names = []
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        if a["zone"] in OUTDOOR_NO_VERGE or b["zone"] in OUTDOOR_NO_VERGE or is_drop(a, b) or near_portal(a):
            continue
        name = verge_material(a)
        if name not in names:
            names.append(name)
        (_, na), (_, nb) = fr[i], fr[(i + 1) % n]
        for side in (1, -1):
            if walls[side][i] > 0 or walls[side][(i + 1) % n] > 0:
                continue                                 # the cut wall covers this side
            inner_a = (a["x"] + side * na[0] * HALF, a["y"] + side * na[1] * HALF)
            inner_b = (b["x"] + side * nb[0] * HALF, b["y"] + side * nb[1] * HALF)
            outer_a = (a["x"] + side * na[0] * (HALF + VERGE), a["y"] + side * na[1] * (HALF + VERGE))
            outer_b = (b["x"] + side * nb[0] * (HALF + VERGE), b["y"] + side * nb[1] * (HALF + VERGE))
            vs = [bm.verts.new((*inner_a, road_z(a) - 0.01)), bm.verts.new((*outer_a, E.ground_z(*outer_a) + 0.03)),
                  bm.verts.new((*outer_b, E.ground_z(*outer_b) + 0.03)), bm.verts.new((*inner_b, road_z(b) - 0.01))]
            face = bm.faces.new(vs if side == 1 else vs[::-1])
            face.material_index = names.index(name)
            tile = TILE_M.get(name, 2.0)
            for loop, (u, v) in zip(face.loops, ((0, a["s"] / tile), (VERGE / tile, a["s"] / tile),
                                                 (VERGE / tile, (a["s"] + 2) / tile), (0, (a["s"] + 2) / tile))):
                loop[uv].uv = (u, v)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:                               # make sure the strips face up
        if f.normal.z < 0:
            f.normal_flip()
    mesh = bpy.data.meshes.new("Track_Verges")
    bm.to_mesh(mesh)
    bm.free()
    for name in names:
        mesh.materials.append(surface_material(name))
    return E.new_object("Track_Verges", mesh, col)


def build_kerbs(pts, fr, col):
    """Kerbs on the inside of corners tighter than 35 m radius (both sides below 20 m). V18g: off (RACE_KERBS)."""
    n = len(pts)
    flags = [list(race_kerb_sides(pts, i)) for i in range(n)]      # V18g: one rule (RACE_KERBS, the roundabout)
    if not any(flags):
        print("  track: no race kerbs (env_track.RACE_KERBS is off): Track_Kerbs is not built")
        return None
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for side in (1, -1):
        run = []
        for i in range(n + 1):
            on = side in flags[i % n] and not is_drop(pts[i % n], pts[(i + 1) % n])
            if on:
                run.append(i % n)
            if (not on or i == n) and len(run) >= 4:
                add_kerb_run(bm, uv, pts, fr, run, side)
            if not on:
                run = []
    mesh = bpy.data.meshes.new("Track_Kerbs")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("T_kerb", "#FF8A1F", roughness=0.7))
    return E.new_object("Track_Kerbs", mesh, col)


def add_kerb_run(bm, uv, pts, fr, run, side):
    rings = []
    for i in run:
        p, (_, nrm) = pts[i], fr[i]
        z = road_z(p)
        ring = []
        h = E.half_width(p)
        # Flush with the road (1.2 cm, only enough not to flicker): a raised kerb with a ramp face launched karts.
        for offset, dz in ((h - 0.9, 0.005), (h - 0.6, KERB_HEIGHT), (h + 0.1, KERB_HEIGHT), (h + 0.3, -0.05)):
            ring.append(bm.verts.new((p["x"] + side * nrm[0] * offset, p["y"] + side * nrm[1] * offset, z + dz)))
        rings.append((ring, p["s"]))
    for (r0, s0), (r1, s1) in zip(rings, rings[1:]):
        for k in range(3):
            vs = [r0[k], r0[k + 1], r1[k + 1], r1[k]]
            face = bm.faces.new(vs if side == -1 else vs[::-1])
            for loop, (u, v) in zip(face.loops, ((k / 3, s0 / 2), ((k + 1) / 3, s0 / 2), ((k + 1) / 3, s1 / 2), (k / 3, s1 / 2))):
                loop[uv].uv = (u, v)


CUT_ZONES = {"ramp", "tunnel plaza"}


def wall_heights(pts, fr, side):
    """Per route point: how high the 45 degree cut wall on this side must go (0 = no wall), smoothed."""
    raw = []
    for i, p in enumerate(pts):
        (_, nrm) = fr[i]
        h = 0.0
        if p["zone"] in CUT_ZONES:
            for k in range(1, 30):                       # walk up the 45 degree plane until it meets the terrain
                o = HALF + k * 0.3
                x, y = p["x"] + side * nrm[0] * o, p["y"] + side * nrm[1] * o
                if p["track_z"] + (o - HALF) >= E.ground_z(x, y) - 0.02:
                    h = o - HALF
                    break
            else:
                h = 9.0
        raw.append(h if h > 0.6 else 0.0)
    n = len(raw)
    out = []
    for i in range(n):                                   # smooth so the wall top doesn't zig-zag
        window = [raw[(i + k) % n] for k in range(-3, 4)]
        out.append(sum(window) / 7 if raw[i] > 0 else 0.0)
    return out


_WALLS = {}
HOLE_EXTRA = 1.0            # terrain hole reaches this far past the wall top
CAP_EXTRA = 3.6             # the cap reaches this far past the hole (covers the 5 m terrain cells)


def compute_walls():
    """Wall heights per route point and side; call before the ground is built (it decides the holes)."""
    pts = E.route_points()
    fr = frames(pts)
    _WALLS.clear()
    _WALLS.update({1: wall_heights(pts, fr, 1), -1: wall_heights(pts, fr, -1)})
    return _WALLS


def in_cut_hole(x, y):
    """True if a terrain face centred at (x, y) lies where a cut wall and its cap replace the terrain."""
    if not _WALLS:
        return False
    pts = E.route_points()
    p = E.nearest_route_point(x, y)
    i = pts.index(p)
    (_, nrm) = frames([pts[(i - 1) % len(pts)], p, pts[(i + 1) % len(pts)]])[1]
    off = (x - p["x"]) * nrm[0] + (y - p["y"]) * nrm[1]
    if p["tunnel"]:
        # just in front of / at a portal mouth: no terrain across the opening
        return near_portal(p, 6.0) and abs(off) <= HALF + 6.0
    if p["zone"] not in CUT_ZONES:
        return False
    side = 1 if off > 0 else -1
    h = _WALLS[side][i]
    if abs(off) <= HALF:
        return h > 0 or _WALLS[-side][i] > 0 or near_portal(p, 12.0)   # under the road in a cutting
    return h > 0 and abs(off) <= HALF + h + HOLE_EXTRA


def build_cut_walls(pts, fr, col):
    """Sloped (45 degree) concrete walls where the road runs in a cutting (the sunken tunnel approaches)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    n = len(pts)
    walls = {}
    for side in (1, -1):
        hs = _WALLS[side] if _WALLS else wall_heights(pts, fr, side)
        walls[side] = hs
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            ha, hb = hs[i], hs[(i + 1) % n]
            if ha <= 0 or hb <= 0 or is_drop(a, b) or a["tunnel"] and b["tunnel"]:
                continue
            (_, na), (_, nb) = fr[i], fr[(i + 1) % n]
            def at(p, nrm, o, z):
                return (p["x"] + side * nrm[0] * o, p["y"] + side * nrm[1] * o, z)
            vs = [at(a, na, HALF - 0.05, road_z(a) - 0.02), at(a, na, HALF + ha + 0.1, a["track_z"] + ha + 0.12),
                  at(b, nb, HALF + hb + 0.1, b["track_z"] + hb + 0.12), at(b, nb, HALF - 0.05, road_z(b) - 0.02)]
            face = bm.faces.new([bm.verts.new(v) for v in (vs if side == 1 else vs[::-1])])
            face.material_index = 0
            for loop, c in zip(face.loops, ((0, a["s"] / 2), (1.5, a["s"] / 2), (1.5, (a["s"] + 2) / 2), (0, (a["s"] + 2) / 2))
                               if side == 1 else ((0, (a["s"] + 2) / 2), (1.5, (a["s"] + 2) / 2), (1.5, a["s"] / 2), (0, a["s"] / 2))):
                loop[uv].uv = c
            # cap: from the wall top outwards over the terrain hole, in the ground's own colour map
            ta, tb = HALF + ha + 0.1, HALF + hb + 0.1
            ea = HALF + ha + HOLE_EXTRA + CAP_EXTRA
            eb = HALF + hb + HOLE_EXTRA + CAP_EXTRA
            cap = [at(a, na, ta, a["track_z"] + ha + 0.12), at(a, na, ea, E.ground_z(*at(a, na, ea, 0)[:2]) + 0.04),
                   at(b, nb, eb, E.ground_z(*at(b, nb, eb, 0)[:2]) + 0.04), at(b, nb, tb, b["track_z"] + hb + 0.12)]
            cface = bm.faces.new([bm.verts.new(v) for v in (cap if side == 1 else cap[::-1])])
            cface.material_index = 1
            for loop in cface.loops:
                co = loop.vert.co
                loop[uv].uv = ((co.x - E.X0) / (E.X1 - E.X0), (co.y - E.Y0) / (E.Y1 - E.Y0))
    bm.normal_update()
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    mesh = bpy.data.meshes.new("Track_CutWalls")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(surface_material("cut_wall"))
    mesh.materials.append(E.material("ground_map", "#A6AA82", roughness=0.95))
    build_cut_walls.walls = walls
    return E.new_object("Track_CutWalls", mesh, col)


def build_wedge(pts, fr, col, wedge):
    """Concrete wedge at the terrace edge (videos IMG_7723 0-12 s): the road runs down its sloped top (build_road),
    this adds the solid body under it: two side walls from the road edge to the lawn and the end face at the foot."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    run = [wedge[0] - 1] + wedge + [wedge[-1] + 1]          # from the crest to the foot
    n = len(pts)
    for side in (1, -1):
        for a_i, b_i in zip(run, run[1:]):
            a, b = pts[a_i % n], pts[b_i % n]
            (_, na), (_, nb) = fr[a_i % n], fr[b_i % n]
            ha, hb = E.half_width(a) + 0.15, E.half_width(b) + 0.15
            ta = (a["x"] + side * na[0] * ha, a["y"] + side * na[1] * ha)
            tb = (b["x"] + side * nb[0] * hb, b["y"] + side * nb[1] * hb)
            za, zb = road_z(a), road_z(b)
            ga, gb = E.coarse_ground_z(*ta) - 0.3, E.coarse_ground_z(*tb) - 0.3
            vs = [bm.verts.new((ta[0], ta[1], ga)), bm.verts.new((tb[0], tb[1], gb)),
                  bm.verts.new((tb[0], tb[1], zb)), bm.verts.new((ta[0], ta[1], za))]
            face = bm.faces.new(vs if side == 1 else vs[::-1])
            for loop, (u, v) in zip(face.loops, ((a["s"] / 2, ga / 2), (b["s"] / 2, gb / 2), (b["s"] / 2, zb / 2), (a["s"] / 2, za / 2))):
                loop[uv].uv = (u, v if side == 1 else v)
    bm.normal_update()
    mesh = bpy.data.meshes.new("Track_JumpKicker")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(surface_material("concrete_light"))
    return E.new_object("Track_JumpKicker", mesh, col)


def kicker_rows(pts):
    """Route segments (index of their first point) that lie under the kicker ramp (route.json jump.kicker type "ramp")."""
    kick = E.ROUTE["jump"].get("kicker", {})
    if kick.get("type") != "ramp":
        return ()
    j, n = E.ROUTE["jump"]["index"], len(pts)
    return {(j - k) % n for k in range(1, int(math.ceil(kick["length_m"] / 2.0)) + 1)}


def build_ramp(pts, fr, col, kick):
    """V10i: the kicker ramp on the terrace deck. It rises kick["rise_m"] over its last kick["length_m"] up to the
    terrace edge (z = rise * t ** power: no kink at its start, steepest at the lip), 0.25 m rows, Boost Orange
    stripes across (kerb texture), solid sides, a front face at the lip. Beyond the lip: nothing, a free drop."""
    j, n = E.ROUTE["jump"]["index"], len(pts)
    length, rise, power = kick["length_m"], kick["rise_m"], kick.get("power", 1.5)
    steps = int(round(length / 0.25))
    base = road_z(dict(pts[j], track_z=pts[j]["track_z"] - rise))            # the deck road under the ramp
    half = E.half_width(pts[j])

    def row(k):
        d = length * (1 - k / steps)                                         # metres before the lip
        f = d / 2.0
        i0 = int(math.floor(f))
        t = f - i0
        a, b = pts[(j - i0) % n], pts[(j - i0 - 1) % n]
        (_, na), (_, nb) = fr[(j - i0) % n], fr[(j - i0 - 1) % n]
        x, y = a["x"] + (b["x"] - a["x"]) * t, a["y"] + (b["y"] - a["y"]) * t
        nx, ny = na[0] + (nb[0] - na[0]) * t, na[1] + (nb[1] - na[1]) * t
        z = base + rise * (k / steps) ** power
        return (x + nx * half, y + ny * half), (x - nx * half, y - ny * half), z, length - d
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rows = [row(k) for k in range(steps + 1)]
    for (l0, r0, z0, s0), (l1, r1, z1, s1) in zip(rows, rows[1:]):
        top = bm.faces.new([bm.verts.new((*l0, z0)), bm.verts.new((*r0, z0)), bm.verts.new((*r1, z1)), bm.verts.new((*l1, z1))])
        for loop, c in zip(top.loops, ((0.0, s0 / 2), (2 * half / 2, s0 / 2), (2 * half / 2, s1 / 2), (0.0, s1 / 2))):
            loop[uv].uv = c
        for p0, p1, flip in ((l0, l1, True), (r0, r1, False)):               # solid sides down to the deck
            vs = [bm.verts.new((*p0, base - 0.05)), bm.verts.new((*p1, base - 0.05)), bm.verts.new((*p1, z1)), bm.verts.new((*p0, z0))]
            side = bm.faces.new(vs[::-1] if flip else vs)
            for loop in side.loops:
                loop[uv].uv = (0.05, loop.vert.co.z)
    l, r, z, _s = rows[-1]                                                   # front face at the lip
    front = bm.faces.new([bm.verts.new((*l, base - 0.3)), bm.verts.new((*l, z)), bm.verts.new((*r, z)), bm.verts.new((*r, base - 0.3))])
    for loop in front.loops:
        loop[uv].uv = (0.05, loop.vert.co.z)
    bm.normal_update()
    mesh = bpy.data.meshes.new("Track_JumpKicker")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("T_kerb", "#FF8A1F", roughness=0.7))
    E.new_object("Track_JumpKicker_NoCollide", mesh, col)        # to look at; the kart drives on the collider below

    # The collider (not drawn): the same ramp 5 cm higher at its start (easing to 0.5 cm at the lip), cut into
    # 0.5 x 1 m faces (sliver triangles kicked the karts), and before it a strip along the road that comes up
    # through the road's surface at a flat angle: a kart must never roll over the border of a collider (it was
    # kicked into the air there), so it changes from the road to this mesh where both are plain faces.
    cols = [half * c / 4 for c in range(-4, 5)]
    grid = []
    for back, rel in ((4, -0.06), (3, -0.03), (2, 0.0), (1, 0.03)):
        i = (j - int(round(length / 2.0)) - back) % n
        p, (_, nrm) = pts[i], fr[i]
        z = road_z(p)
        grid.append([(p["x"] + nrm[0] * u, p["y"] + nrm[1] * u, z - 0.1 if abs(u) > half - 0.01 else z + rel) for u in cols])
    csteps = int(round(length / 0.5))
    for k in range(csteps + 1):
        t = k / csteps
        l, r, _z, _s = row(k * steps // csteps)
        z = base + 0.05 * (1 - t) ** 2 + 0.005 * t + rise * t ** power
        grid.append([(l[0] + (r[0] - l[0]) * (half - u) / (2 * half), l[1] + (r[1] - l[1]) * (half - u) / (2 * half), z) for u in cols])
    cm = bmesh.new()

    def cface(ring, up=True):
        f = cm.faces.new([cm.verts.new(q) for q in ring])
        f.normal_update()
        if up and f.normal.z < 0:
            f.normal_flip()
        return f
    for r0, r1 in zip(grid, grid[1:]):
        for c in range(len(cols) - 1):
            cface([r0[c], r0[c + 1], r1[c + 1], r1[c]])
    def outward(f, d):                                          # turn an upright face so it looks along d
        if f.normal.x * d[0] + f.normal.y * d[1] < 0:
            f.normal_flip()
    (tx, ty), (nx, ny) = fr[j]
    for r0, r1 in zip(grid[4:], grid[5:]):                     # the ramp's solid sides and its front
        for c, sgn in ((0, -1), (len(cols) - 1, 1)):            # column 0 = right of the driving direction
            outward(cface([(r0[c][0], r0[c][1], base - 0.1), (r1[c][0], r1[c][1], base - 0.1), r1[c], r0[c]], up=False),
                    (nx * sgn, ny * sgn))
    end = grid[-1]
    outward(cface([(end[0][0], end[0][1], base - 0.3), end[0], end[-1], (end[-1][0], end[-1][1], base - 0.3)], up=False), (tx, ty))
    bmesh.ops.remove_doubles(cm, verts=cm.verts[:], dist=0.004)
    cm.normal_update()
    cmesh = bpy.data.meshes.new("Track_JumpKickerDrive")
    cm.to_mesh(cmesh)
    cm.free()
    cmesh.materials.append(E.material("T_kerb", "#FF8A1F", roughness=0.7))
    obj = E.new_object("Track_JumpKickerDrive_ColliderOnly", cmesh, col)
    obj.hide_render = True
    obj["collider_only"] = 1
    return obj


def build_kicker(pts, fr, col):
    wedge = [i for i, p in enumerate(pts) if p["zone"] == "kicker"]
    if wedge:
        return build_wedge(pts, fr, col, wedge)
    kick = E.ROUTE["jump"].get("kicker", {})
    if kick.get("type") == "ramp":
        return build_ramp(pts, fr, col, kick)
    j = E.ROUTE["jump"]["index"]
    p, (t, nrm) = pts[j], fr[j]
    z = road_z(p)
    length, height = 4.0, 0.5
    verts = []
    for along, top in ((-length, 0.0), (0.0, height)):
        for side in (1, -1):
            x = p["x"] + t[0] * along + side * nrm[0] * HALF
            y = p["y"] + t[1] * along + side * nrm[1] * HALF
            verts += [(x, y, z), (x, y, z + top)]
    # verts: back-left low/high, back-right low/high, front-left low/high, front-right low/high
    faces = [(0, 2, 6, 4), (1, 5, 7, 3), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6)]
    mesh = bpy.data.meshes.new("Track_JumpKicker")
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:                              # stripes run across the kicker
        co = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (((co.x - p["x"]) * nrm[0] + (co.y - p["y"]) * nrm[1]) / 2.0, co.z)
    mesh.materials.append(E.material("T_kerb", "#FF8A1F", roughness=0.7))
    return E.new_object("Track_JumpKicker", mesh, col)


def build():
    pts = E.route_points()
    if not pts:
        return
    col = E.collection("Track_Surface")
    fr = frames(pts)
    build_road(pts, fr, col)
    # cut walls and verges are now part of the high-res corridor terrain (env_corridor.py)
    build_kerbs(pts, fr, col)
    build_kicker(pts, fr, col)
