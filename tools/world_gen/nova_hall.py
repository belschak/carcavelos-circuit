"""The Nova SBE main building from the inside, built in its real footprint (Edificio Principal, OpenStreetMap) and placed
the way Noah marked it on the campus map (doors in red, video paths in green = IMG_7722 and purple = IMG_7725):

  Atrium (north block, about 68 x 68 m, two storeys, steel-and-glass roof)
    D1  main entrance on the west facade (IMG_7722 start): entrance zone with reception, partition boards with
        planters, the white "Hovione" wall block, a timber-joist gallery along the north side
    D4  door on the south edge of the block (IMG_7725): stage, cube seats, the wide white staircase, round columns,
        planters along the east wall, study tables
    D5  door out on the north-east facade (IMG_7725 end)
    D2  white artwork wall with doors into the long room (IMG_7722)
  Long room (the wing south of the block, about 30 m wide): low ceiling first, then the big steel hall with the glass
    facade on the west side, tables, chairs, pianos, plants
    D3  door to the terrace on the west facade (IMG_7722, "Tuer zur Schanze")

Coordinates: one Frame for the whole building: origin = north-west corner, u = metres south along the west wall,
o = metres east from it, z = metres above the floor (nova_context.py lifts the objects to plaza height).
Everything is a number in the constants below. No Nova logo (docs/decisions.md): the big letters of the real hall are
an abstract white sculpture. hall_kit.py has the parts.

Run:  /Applications/Blender.app/Contents/MacOS/Blender -b -P nova_hall.py   ->  out/NovaHall.blend, out/NovaHall.fbx
      ... -b -P nova_hall.py -- world   ->  out/NovaHallWorld.fbx (at its place in Luc's campus world, for Unity)
"""
import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import env_common as E          # noqa: E402
import hall_kit as K            # noqa: E402

OUT = os.path.join(HERE, "out")
BUILDING_ID = "w665606788"                 # Edificio Principal in world_data.json
NW_CORNER, NE_CORNER = (3.0, 160.0), (67.0, 184.0)

# ---------- sizes (m) ----------
SPLIT_U = 68.0               # line between the atrium block and the long room (through (28, 97) and (55, 107))
LONG_END_U = 209.8           # the long room runs through the whole wing to its south facade (map: wing ends at u 210)
ATRIUM_H = 12.5              # clear height under the atrium roof
LONG_H = 11.0                # clear height of the long room
LOW_CEILING_END = 80.0       # first part of the long room has a flat ceiling at LOW_Z
LOW_Z = 4.6
GALLERY_Z = 5.2              # first floor
GALLERY_DEPTH = 7.0          # depth of the galleries along the walls
BRIDGE_U = (7.0, 27.0)       # north-south gallery in the middle of the atrium (seen from the main entrance), u from .. to
BRIDGE_O = (30.0, 40.5)      # .. and o from .. to; the wide staircase arrives at its south end
WALL = 0.35                  # wall thickness (outer face offset)
PLINTH = 5.0                 # outer walls go this far below the hall floor (the ground falls away to the south)
SOLID_NORTH_U = 3.0          # facade pieces of the atrium closer than this to the north side are closed white walls
DOOR_H = 3.2

# doors in hall coordinates (u, o) and width. They sit where Noah's route crosses the facades (route.json), so the
# track outside joins without a jog; the track doors are wide enough for the karts.
DOORS_UO = {
    "D1_main_entrance": ((25.5, 0.0), 9.5),     # route in at u 25.5 (IMG_7722 start)
    "D3_terrace": ((103.5, 0.0), 9.5),          # route out onto the CTT terrace, towards the jump
    "D4_entrance_room": ((70.9, 36.9), 6.0),    # route in from the south (IMG_7725); the facade piece is only 7 m long
    "D5_out_north_east": ((23.4, 68.3), 7.5),   # route out on the east facade
}
D2_O = (8.5, 17.5)           # opening in the artwork wall (o from .. to): the track into the long room


def footprint():
    for b in E.DATA["buildings"]:
        if b["id"] == BUILDING_ID:
            return [(p[0], p[1]) for p in b["points"]]
    raise RuntimeError("Edificio Principal not in world_data.json")


def building_frame():
    (ax, ay), (bx, by) = NW_CORNER, NE_CORNER
    d = math.hypot(bx - ax, by - ay)
    tx, ty = (bx - ax) / d, (by - ay) / d                       # along the north facade (east)
    return K.Frame(ax, ay, math.degrees(math.atan2(-tx, ty)))  # axis perpendicular to it, pointing south


def regions(f):
    fp = footprint()
    atrium = K.clip_polygon(fp, lambda p: SPLIT_U - f.uo(*p)[0])
    long_room = K.clip_polygon(fp, lambda p: f.uo(*p)[0] - SPLIT_U)
    rest = K.clip_polygon(long_room, lambda p: f.uo(*p)[0] - LONG_END_U)
    long_room = K.clip_polygon(long_room, lambda p: LONG_END_U - f.uo(*p)[0])
    return atrium, long_room, rest


# ---------- the track through the building ----------
# Centre lines in hall coordinates (u, o) as straight pieces with rounded corners (radius per corner). They follow Noah's
# route plan: west part = main entrance, right turn, through the artwork wall into the long room, out to the terrace;
# east part = in from the south (entrance room door), through the east half of the atrium, out on the east facade.
# The floor stays one even grey; only guard rails show the way. Furniture in the way is left out (free()),
# columns, the stair, the stage and the white block stay and the line goes round them.
ROADS = [
    {"name": "west", "width": 7.0, "radii": [9.0, 9.0],
     "points": [(25.5, -8.0), (25.5, 13.0), (103.5, 13.0), (103.5, -10.0)]},
    {"name": "east", "width": 6.0, "radii": [5.0, 5.0, 6.0, 6.0, 4.0],
     "points": [(82.0, 37.2), (66.0, 37.0), (60.0, 31.0), (40.0, 31.0), (34.0, 26.5), (23.4, 26.5), (23.4, 78.0)]},
]
RAIL_OFFSET = 0.35           # guard rail distance outside the road edge (m)
RAIL_STOP = 1.2              # rails stop this far before a facade (the doors)


def rounded_line(points, radii, step=0.5):
    """Points every `step` m along a polyline whose corners are replaced by circular arcs of the given radii."""
    pts = [points[0]]
    for i in range(1, len(points) - 1):
        a, c, b = points[i - 1], points[i], points[i + 1]
        din = ((c[0] - a[0]), (c[1] - a[1]))
        dout = ((b[0] - c[0]), (b[1] - c[1]))
        lin, lout = math.hypot(*din), math.hypot(*dout)
        din, dout = (din[0] / lin, din[1] / lin), (dout[0] / lout, dout[1] / lout)
        turn = math.atan2(din[0] * dout[1] - din[1] * dout[0], din[0] * dout[0] + din[1] * dout[1])
        R = radii[i - 1]
        t = R * math.tan(abs(turn) / 2)
        t1 = (c[0] - din[0] * t, c[1] - din[1] * t)
        side = 1 if turn > 0 else -1                                    # left turn: centre on the left
        centre = (t1[0] - din[1] * R * side, t1[1] + din[0] * R * side)
        a0 = math.atan2(t1[1] - centre[1], t1[0] - centre[0])
        n = max(2, int(abs(turn) * R / step))
        pts.append(t1)
        for k in range(1, n + 1):
            ang = a0 + turn * k / n
            pts.append((centre[0] + R * math.cos(ang), centre[1] + R * math.sin(ang)))
    pts.append(points[-1])
    dense = [pts[0]]                                                     # even spacing
    for p, q in zip(pts, pts[1:]):
        L = math.hypot(q[0] - p[0], q[1] - p[1])
        for k in range(1, max(1, int(L / step)) + 1):
            dense.append((p[0] + (q[0] - p[0]) * k / max(1, int(L / step)), p[1] + (q[1] - p[1]) * k / max(1, int(L / step))))
    return dense


for road in ROADS:
    road["line"] = rounded_line(road["points"], road["radii"])


def free(u, o, radius):
    """False when something of this radius at (u, o) would stand on a road (with 0.3 m to spare)."""
    for road in ROADS:
        limit = road["width"] / 2 + radius + 0.3
        for (pu, po) in road["line"]:
            if (pu - u) ** 2 + (po - o) ** 2 < limit * limit:
                return False
    return True


def guarded(fn, radius):
    """Furniture part that is only placed where free() allows it."""
    def place(m, f, u, o, *args, **kwargs):
        if free(u, o, radius):
            fn(m, f, u, o, *args, **kwargs)
    return place


round_table = guarded(K.round_table, 1.6)
long_table = guarded(K.long_table, 1.8)
planter = guarded(K.planter, 0.8)
board = guarded(K.board, 1.0)
big_plant = guarded(K.big_plant, 0.9)
info_stand = guarded(K.info_stand, 0.6)
grand_piano = guarded(K.grand_piano, 1.4)


def inside_footprint(x, y, margin):
    """Inside the building and at least `margin` m from its outer walls."""
    poly = footprint()
    if not E.inside_polygon(x, y, poly):
        return False
    for a, b in zip(poly, poly[1:] + poly[:1]):
        L2 = (b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2 or 1.0
        t = max(0.0, min(1.0, ((x - a[0]) * (b[0] - a[0]) + (y - a[1]) * (b[1] - a[1])) / L2))
        if math.hypot(a[0] + (b[0] - a[0]) * t - x, a[1] + (b[1] - a[1]) * t - y) < margin:
            return False
    return True


def guard_rails(m, f):
    """Rails on both sides of each road, like the ramp rails in the art reference: white posts every 2 m, a round orange
    top tube at 1 m and a light lilac board below. They stop RAIL_STOP m before the facades (door openings)."""
    for road in ROADS:
        line = road["line"]
        for side in (1, -1):
            off = road["width"] / 2 + RAIL_OFFSET
            pts = []
            for k, (u, o) in enumerate(line):
                a, b = line[max(k - 1, 0)], line[min(k + 1, len(line) - 1)]
                du, do = b[0] - a[0], b[1] - a[1]
                d = math.hypot(du, do) or 1.0
                pu, po = u - do / d * off * side, o + du / d * off * side
                x, y, _ = f.at(pu, po)
                pts.append((pu, po) if inside_footprint(x, y, RAIL_STOP) else None)
            runs, cur = [], []
            for p in pts:
                if p is None:
                    if len(cur) > 1:
                        runs.append(cur)
                    cur = []
                else:
                    cur.append(p)
            if len(cur) > 1:
                runs.append(cur)
            for run in runs:
                sub = run[::2]                                           # one segment per metre
                if sub[-1] != run[-1]:
                    sub.append(run[-1])
                for (u0, o0), (u1, o1) in zip(sub, sub[1:]):
                    m.tube(f.at(u0, o0, 0.98), f.at(u1, o1, 0.98), 0.06, "rail_orange", 8)
                    m.beam(f.at(u0, o0, 0.58), f.at(u1, o1, 0.58), 0.05, 0.16, "rail_lilac")
                for (u0, o0) in run[::4]:                                # a post every 2 m
                    p = f.at(u0, o0, 0)
                    m.box((p[0], p[1], 0.5), 0.05, 0.05, 0.5, "wall", f.h)


# ---------- outer walls with doors ----------
def snapped_doors(polys):
    """Door centre on the nearest facade edge (edges of at least 5 m): {edge key: [(distance along edge, width, name)]}."""
    out = {}
    f = building_frame()
    for name, ((du, do), width) in DOORS_UO.items():
        x, y, _ = f.at(du, do)
        best = None
        for poly in polys:
            for a, b in zip(poly, poly[1:] + poly[:1]):
                L = math.hypot(b[0] - a[0], b[1] - a[1])
                if L < 5.0:
                    continue
                t = ((x - a[0]) * (b[0] - a[0]) + (y - a[1]) * (b[1] - a[1])) / (L * L)
                t = max((width / 2 + 0.6) / L, min(1 - (width / 2 + 0.6) / L, t))
                px, py = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
                d = math.hypot(px - x, py - y)
                if best is None or d < best[0]:
                    best = (d, edge_key(a, b), t * L)
        out.setdefault(best[1], []).append((best[2], width, name))
        print(f"  door {name}: {best[0]:.1f} m from the marked spot")
    return out


def edge_key(a, b):
    return (round(a[0], 3), round(a[1], 3), round(b[0], 3), round(b[1], 3))


def facade(m, a, b, inside, H, openings, style):
    """One straight piece of outer wall from a to b. inside = unit normal pointing into the building.
    style 'glass': glazed ground floor, white band, glazed upper floor, white fascia; 'solid': white wall.
    openings: [(distance along the edge, width, name)] for doors."""
    ax, ay = a
    L = math.hypot(b[0] - ax, b[1] - ay)
    tx, ty = (b[0] - ax) / L, (b[1] - ay) / L
    nx, ny = inside
    heading = math.atan2(ty, tx)

    def P(s, z, outer=False):
        off = -WALL if outer else 0.0
        return (ax + tx * s + nx * off, ay + ty * s + ny * off, z)

    def band(s0, s1, z0, z1, mat):
        if s1 - s0 < 0.01 or z1 - z0 < 0.01:
            return
        m.face([P(s0, z0), P(s1, z0), P(s1, z1), P(s0, z1)], mat, (nx, ny, 0))                        # inside face
        m.face([P(s0, z0, True), P(s1, z0, True), P(s1, z1, True), P(s0, z1, True)], mat, (-nx, -ny, 0))  # outside face

    cuts = sorted((s - w / 2, s + w / 2) for s, w, _ in openings)
    pieces, s = [], 0.0
    for c0, c1 in cuts:
        pieces.append((s, c0))
        s = c1
    pieces.append((s, L))
    top = 4.4                                                         # top of the ground floor band
    glazed_ground = style in ("glass", "glass_low")                      # glass_low: glass on the ground floor only
    for s0, s1 in pieces:
        band(s0, s1, 0.0, top, "glass" if glazed_ground else "wall")
        if glazed_ground:
            for k in range(int((s1 - s0) / 1.6) + 1):                # mullions
                m.box(P(s0 + k * 1.6, top / 2, False), 0.05, 0.06, top / 2, "steel_dark", heading)
    for c0, c1 in cuts:
        band(c0, c1, DOOR_H + 0.4, top, "glass" if glazed_ground else "wall")
        door(m, P((c0 + c1) / 2, 0.0), (tx, ty), (nx, ny), c1 - c0)
    if style == "glass":
        band(0, L, top, GALLERY_Z + 0.6, "wall")                       # white band at the first floor
        band(0, L, GALLERY_Z + 0.6, H - 1.0, "glass")
        for k in range(int(L / 1.6) + 1):
            m.box(P(k * 1.6, (GALLERY_Z + 0.6 + H - 1.0) / 2), 0.05, 0.06, (H - 1.0 - GALLERY_Z - 0.6) / 2, "steel_white", heading)
        band(0, L, H - 1.0, H + 0.8, "wall")
    else:
        band(0, L, top, H + 0.8, "wall")
    m.face([P(0, H + 0.8), P(L, H + 0.8), P(L, H + 0.8, True), P(0, H + 0.8, True)], "wall", (0, 0, 1))  # wall top
    m.face([P(0, -PLINTH, True), P(L, -PLINTH, True), P(L, 0.0, True), P(0, 0.0, True)], "wall", (-nx, -ny, 0))   # plinth


def door(m, c, t, n, width):
    """Sliding glass door: stainless posts, two leaves slid open, header with the motor, exit signs, door mat."""
    h = math.atan2(t[1], t[0])
    for side in (-1, 1):
        p = (c[0] + t[0] * side * width / 2, c[1] + t[1] * side * width / 2)
        m.box((p[0], p[1], DOOR_H / 2), 0.08, 0.12, DOOR_H / 2, "rail", h)
        q = (c[0] + t[0] * side * (width / 2 + 0.55) + n[0] * 0.25, c[1] + t[1] * side * (width / 2 + 0.55) + n[1] * 0.25)
        m.box((q[0], q[1], (DOOR_H - 0.2) / 2), 0.55, 0.02, (DOOR_H - 0.2) / 2, "glass", h)
        e = (c[0] + n[0] * 0.25 + t[0] * side * width * 0.3, c[1] + n[1] * 0.25 + t[1] * side * width * 0.3)
        m.box((e[0], e[1], DOOR_H + 0.25), 0.3, 0.04, 0.12, "exit", h)
    m.box((c[0], c[1], DOOR_H + 0.2), width / 2 + 0.1, 0.18, 0.2, "rail", h)
    mat = (c[0] + n[0] * 1.6, c[1] + n[1] * 1.6)
    m.box((mat[0], mat[1], 0.012), width / 2 - 0.2, 1.4, 0.012, "mat", h)


def rest_of_wing(m, f, poly):
    """The rest of the wing south of the long room (no interior yet): closed block with the same facade and roof, so the
    building on the map stays complete when Luc's Edificio Principal block is switched off."""
    if len(poly) < 3 or abs(K.signed_area(poly)) < 1.0:
        return                                                           # nothing left south of the long room
    ccw = K.signed_area(poly) > 0
    for a, b in zip(poly, poly[1:] + poly[:1]):
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        if L < 0.05:
            continue
        if abs(f.uo(*a)[0] - LONG_END_U) < 0.05 and abs(f.uo(*b)[0] - LONG_END_U) < 0.05:
            continue                                                     # shared with the long room end wall
        tx, ty = (b[0] - a[0]) / L, (b[1] - a[1]) / L
        facade(m, a, b, (-ty, tx) if ccw else (ty, -tx), ATRIUM_H, [], "glass")
    K.polygon_face(m, poly, ATRIUM_H + 0.8, "concrete", up=True)
    K.polygon_face(m, poly, 0.0, "floor", up=True)


def shell(m, f, polys, door_map):
    """Floors and outer walls. The wall between atrium and long room and the end wall are built separately."""
    atrium, long_room = polys[0], polys[1]
    K.polygon_face(m, atrium, 0.0, "floor", up=True)
    K.polygon_face(m, long_room, 0.0, "floor", up=True)
    for poly, H in ((atrium, ATRIUM_H), (long_room, LONG_H)):
        ccw = K.signed_area(poly) > 0
        for a, b in zip(poly, poly[1:] + poly[:1]):
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            if L < 0.05:
                continue
            ua, ub = f.uo(*a)[0], f.uo(*b)[0]
            if (abs(ua - SPLIT_U) < 0.05 and abs(ub - SPLIT_U) < 0.05) or (abs(ua - LONG_END_U) < 0.05 and abs(ub - LONG_END_U) < 0.05):
                continue
            tx, ty = (b[0] - a[0]) / L, (b[1] - a[1]) / L
            inside = (-ty, tx) if ccw else (ty, -tx)
            mid_u, mid_o = f.uo((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
            style = "glass"
            if poly is long_room and mid_o > 15:                       # long room east side (towards the steps):
                style = "glass_low"                                    # glass at table level, white wall above (Noah)
            if poly is atrium and mid_u < SOLID_NORTH_U:               # atrium north facade: closed white wall (Noah)
                style = "solid"
            facade(m, a, b, inside, H, door_map.get(edge_key(a, b), []), style)


# ---------- atrium ----------
def atrium_roof(m, f, poly):
    K.polygon_face(m, poly, ATRIUM_H, "deck", up=False)
    K.polygon_face(m, poly, ATRIUM_H + 0.8, "concrete", up=True)
    for u in K.Frame().samples(3.0, SPLIT_U - 3.0, 6.0):                # trusses across the whole block
        K.truss(m, f, u, 0.5, 67.8, ATRIUM_H - 1.6, ATRIUM_H - 0.15, bays=16)
    for o in (10.0, 22.0, 34.0, 46.0, 58.0):
        K.rod(m, f, (0.5, o, ATRIUM_H - 0.4), (SPLIT_U - 0.5, o, ATRIUM_H - 0.4), 0.2, "steel_white", 0.36)
    for o in (16.0, 28.0, 40.0, 52.0):                                   # skylight bands between the beams
        K.floor_rect(m, f, GALLERY_DEPTH + 1.0, SPLIT_U - 3.0, o - 2.2, o + 2.2, ATRIUM_H - 0.03, "skylight", up=False)


def gallery_slab(m, f, u0, u1, o0, o1, open_sides, timber=False):
    """First-floor slab: floor, underside (timber joists or white), white edge beam and glass balustrade on the open
    sides ('u0', 'u1', 'o0', 'o1')."""
    z = GALLERY_Z
    K.floor_rect(m, f, u0, u1, o0, o1, z, "stair", up=True)
    K.floor_rect(m, f, u0, u1, o0, o1, z - 0.45, "timber" if timber else "soffit", up=False)
    if timber:
        for u in K.Frame().samples(u0, u1, 1.2):
            K.block(m, f, u - 0.08, u + 0.08, o0, o1, z - 0.45, z - 0.05, "timber")
    for side in open_sides:
        if side in ("u0", "u1"):
            u = u0 if side == "u0" else u1
            K.block(m, f, u - 0.12, u + 0.12, o0, o1, z - 0.55, z + 0.02, "wall")
            K.wall_u(m, f, u, o0, o1, z + 0.05, z + 1.12, "glass", facing=1)
            K.wall_u(m, f, u, o0, o1, z + 0.05, z + 1.12, "glass", facing=-1)
            K.block(m, f, u - 0.04, u + 0.04, o0, o1, z + 1.1, z + 1.16, "rail")
        else:
            o = o0 if side == "o0" else o1
            K.block(m, f, u0, u1, o - 0.12, o + 0.12, z - 0.55, z + 0.02, "wall")
            K.wall_o(m, f, o, u0, u1, z + 0.05, z + 1.12, "glass", facing=1)
            K.wall_o(m, f, o, u0, u1, z + 0.05, z + 1.12, "glass", facing=-1)
            K.block(m, f, u0, u1, o - 0.04, o + 0.04, z + 1.1, z + 1.16, "rail")


def atrium_structure(m, f):
    G = GALLERY_DEPTH
    # north gallery (timber joists underneath, IMG_7722), east gallery, bridge across the east half, west balcony
    gallery_slab(m, f, 0.3, G, 0.6, 67.8, ("u1",), timber=True)
    gallery_slab(m, f, G, SPLIT_U - 0.4, 67.8 - G, 67.8, ("o0",))
    gallery_slab(m, f, BRIDGE_U[0], BRIDGE_U[1], BRIDGE_O[0], BRIDGE_O[1], ("o0", "u1"), timber=True)
    gallery_slab(m, f, 44.0, SPLIT_U - 0.4, 0.6, 5.0, ("o1",), timber=True)
    for u in K.Frame().samples(G + 1.0, SPLIT_U - 2.0, 8.0):                                   # slim round columns
        if free(u, 67.8 - G - 0.3, 0.3):                                                       # (one gives way to the track)
            K.round_column(m, f, u, 67.8 - G - 0.3, 0, GALLERY_Z - 0.45, 0.22)
    for o in K.Frame().samples(6.0, 60.0, 8.0):
        K.round_column(m, f, G + 0.3, o, 0, GALLERY_Z - 0.45, 0.22)
    for u in (12.0, 19.5):
        K.round_column(m, f, u, BRIDGE_O[0] + 0.3, 0, GALLERY_Z - 0.45, 0.22)
    for o in (BRIDGE_O[0] + 0.3, BRIDGE_O[1] - 0.3):
        K.round_column(m, f, BRIDGE_U[1] + 0.3, o, 0, GALLERY_Z - 0.45, 0.22)
    K.block(m, f, 14.0, 18.5, BRIDGE_O[0] - 0.12, BRIDGE_O[0] - 0.05, GALLERY_Z + 0.3, GALLERY_Z + 2.8, "piano")   # big screen
    # north wall, ground floor (seen left of the main entrance): white square pillars, doors, red exit signs
    for o in (4.0, 10.0, 16.0, 22.0):
        K.square_column(m, f, 1.2, o, 0, GALLERY_Z - 0.45, 0.7)
    for o0 in (6.0, 18.0):
        K.block(m, f, 0.2, 0.3, o0, o0 + 2.0, 0.0, 2.4, "table")
        K.block(m, f, 0.3, 0.42, o0 + 0.6, o0 + 1.4, 2.6, 2.85, "red")
    for u, o in ((36.0, 46.0), (36.0, 54.0), (46.0, 54.0), (56.0, 54.0), (46.0, 22.0), (56.0, 22.0)):
        K.round_column(m, f, u, o, 0, ATRIUM_H - 1.6, 0.24)                                    # free columns up to the roof
        m.cylinder(f.at(u, o, 0), 0.44, 0.5, "wall", 14, top_radius=0.25)                      # flared foot (IMG_7725)

    # the white "Hovione" wall block on the west side, south of the entrance, with an empty sign plate
    K.block(m, f, 30.5, 44.5, 0.6, 9.0, 0, 9.0, "wall")
    K.block(m, f, 30.44, 30.5, 2.0, 7.6, 4.2, 5.8, "table")
    # the wide white staircase: from the floor at u 38 up to the bridge at u 27, o 34-39.5
    stair(m, f, 38.0, BRIDGE_U[1], 34.0, 39.5, GALLERY_Z)
    # "With great power ..." wall on the east side: light panel with a dark band (no text in the model)
    K.block(m, f, 40.0, 50.0, 67.3, 67.6, 3.0, 4.6, "table")
    K.block(m, f, 42.0, 48.0, 67.25, 67.3, 3.6, 4.0, "steel_dark")


def stair(m, f, u_bottom, u_top, o0, o1, z_top):
    """Straight wide flight rising towards smaller u: white steps on a solid base, side stringers, glass balustrades."""
    n = int(round(z_top / 0.17))
    run = (u_bottom - u_top) / n
    for i in range(n):
        top = (i + 1) * z_top / n
        ua = u_bottom - run * (i + 1)
        K.block(m, f, ua, ua + run, o0, o1, 0.0, top, "stair")
    nrm = f.left()
    for o in (o0 - 0.12, o1 + 0.12):
        K.rod(m, f, (u_bottom, o, 1.1), (u_top, o, z_top + 1.1), 0.05, "rail")
        a, b = f.at(u_bottom, o, 0.15), f.at(u_top, o, z_top + 0.15)
        for sgn in (1, -1):
            m.face([a, b, (b[0], b[1], b[2] + 0.9), (a[0], a[1], a[2] + 0.9)], "glass", (nrm[0] * sgn, nrm[1] * sgn, 0))


def artwork_wall(m, f):
    """White wall between atrium and long room (IMG_7722): colourful artwork, two double doors, the open passage D2."""
    u = SPLIT_U
    o_end = 29.7                                                         # east end of the wall (where the wing starts)
    for o0, o1 in ((0.0, D2_O[0]), (D2_O[1], o_end)):
        K.block(m, f, u - 0.15, u + 0.15, o0, o1, 0.0, ATRIUM_H + 0.8, "wall")
    K.block(m, f, u - 0.15, u + 0.15, D2_O[0], D2_O[1], LOW_Z, ATRIUM_H + 0.8, "wall")
    for o0 in (19.5, 22.7):                                              # double doors
        K.block(m, f, u - 0.2, u - 0.15, o0, o0 + 2.6, 0.0, 2.4, "table")
        K.block(m, f, u - 0.22, u - 0.2, o0 + 1.29, o0 + 1.31, 0.0, 2.4, "steel_dark")
    for k, o0 in enumerate((26.0, 1.2, 4.6)):                            # artwork: coloured bars
        for j, c in enumerate(("red", "blue", "yellow", "blue")):
            z = 2.4 + (j % 2) * 0.5 + k * 0.2
            K.block(m, f, u - 0.2, u - 0.16, o0 + j * 0.6, o0 + j * 0.6 + 1.2, z, z + 0.2, c)
    for o in (D2_O[0] - 0.6, D2_O[1] + 0.6):                             # white square columns at the passage
        K.square_column(m, f, u + 1.0, o, 0, LOW_Z, 0.8)
    K.block(m, f, u + 0.58, u + 0.6, D2_O[1] + 0.4, D2_O[1] + 0.8, 1.4, 1.8, "black")            # round badge


def atrium_furniture(m, f):
    # entrance zone (west, IMG_7722): reception desk left of the door, sculpture ahead, boards with planter troughs
    K.desk(m, f, 16.5, 3.4, 1.2, 5.0)                                     # welcome desk, left of the doors
    # abstract sculpture straight ahead of the main entrance, arranged and sized like the real logo letters
    # (no readable logo, docs/decisions.md): tall block, ring standing on a long low plinth, a V, a chevron
    SO = 16.0                                                            # o of the row (across the view from the door)
    K.block(m, f, 6.4, 8.0, SO - 0.3, SO + 0.3, 0.0, 2.6, "wall")
    K.block(m, f, 8.4, 14.8, SO - 0.45, SO + 0.45, 0.0, 0.5, "wall")
    t, nrm = f.tangent(), f.left()
    m.ring(f.at(10.4, SO - 0.3, 1.78), 1.28, 0.72, 0.6, "wall", basis=((0, 0, 1), (t[0], t[1], 0), (nrm[0], nrm[1], 0)))
    K.rod(m, f, (15.2, SO, 2.6), (16.4, SO, 0.0), 0.6, "wall")          # V: point at the bottom
    K.rod(m, f, (17.6, SO, 2.6), (16.4, SO, 0.0), 0.6, "wall")
    K.rod(m, f, (18.0, SO, 0.0), (19.2, SO, 2.6), 0.6, "wall")          # chevron: point at the top
    K.rod(m, f, (20.4, SO, 0.0), (19.2, SO, 2.6), 0.6, "wall")
    for k in range(8):                                                   # dark blue boards and planter troughs behind it
        u = 14.0 + k * 2.6
        if k % 2 == 0:
            board(m, f, u, 21.0, 1.8, 2.2)
        else:
            planter(m, f, u, 20.6, 40 + k, palm=k % 4 == 1)
    for k in range(9):                                                   # planters along the way to the artwork wall
        planter(m, f, 44.0 + k * 2.6, 19.0, 60 + k, palm=k % 2 == 0)
    for u, o in ((14.5, 25.5), (19.5, 26.5), (25.0, 25.5), (31.0, 24.0), (36.0, 22.0)):
        round_table(m, f, u, o, 4)
    K.block(m, f, 50.0, 50.6, 0.6, 0.9, 1.0, 1.8, "red")                 # fire hose cabinet

    # east zone (IMG_7725): stage, cube seats in an arc, study tables with boards, planters, sofas by the exit
    K.block(m, f, 47.0, 53.0, 41.0, 47.0, 0.0, 0.42, "grey")
    for i in range(2):
        K.block(m, f, 53.0, 53.6 - 0.3 * i, 43.0, 45.0, 0.0, 0.28 - 0.14 * i, "steel_dark")
    for i in range(14):                                                  # seats on an arc west of the stage
        a = math.radians(215 + 110 * i / 13)
        u, o = 50.0 + 6.5 * math.cos(a), 44.0 + 6.5 * math.sin(a)
        K.block(m, f, u - 0.27, u + 0.27, o - 0.27, o + 0.27, 0.0, 0.46, "grey")
    for u in (42.0, 46.5, 51.0, 55.5, 60.0):
        long_table(m, f, u, 30.5, seats=2)
    board(m, f, 44.2, 33.6, 1.8, 2.0)
    board(m, f, 57.8, 33.6, 1.8, 2.0)
    for k in range(10):
        planter(m, f, 30.0 + k * 3.2, 65.6, 80 + k, palm=k % 3 == 0)
    for k in range(3):
        if free(13.0 + k * 2.4, 62.5, 1.2):
            K.block(m, f, 12.0 + k * 2.4, 14.0 + k * 2.4, 62.0, 63.0, 0.0, 0.75, "board")        # dark blue sofas
    big_plant(m, f, 14.0, 58.0, 3)
    big_plant(m, f, 60.0, 40.0, 4)


# ---------- long room ----------
FACADE_FRAME_STEP = 3.0                     # grid of the steel space frame in front of the west glass


def twisted_sculpture(m, f, u, o, height, size=0.9, step=0.12, material="black"):
    """Tall stack of thin slabs, each one turned a bit more: the black twisted column in the long room, and the
    spiral topiaries by the terrace door (material 'leaf')."""
    n = int(height / step)
    for i in range(n):
        p = f.at(u, o, i * step)
        m.box((p[0], p[1], i * step + step * 0.3), size / 2 * (1 - 0.35 * abs(math.sin(i * 0.15))), 0.05, step * 0.3,
              material, f.h + i * 0.22)


TERRACE_DOOR_U = 103.5                      # u of the terrace door (keeps the space frame out of the doorway)


def space_frame(m, f, u0, u1):
    """White steel space frame in front of the west glass (IMG_7722): two layers of tubes, a square grid with crossed
    diagonals, the layers tied together. Nothing below 4.6 m in front of the terrace door."""
    H = LONG_H
    step = FACADE_FRAME_STEP
    us = K.Frame().samples(u0, u1, step)
    zs = [0.6 + k * (H - 1.0) / 4 for k in range(5)]
    half = DOORS_UO["D3_terrace"][1] / 2 + 0.4
    door0, door1 = TERRACE_DOOR_U - half, TERRACE_DOOR_U + half

    def in_door(a, b, z):
        return z < DOOR_H + 1.4 and b > door0 and a < door1

    for layer_o in (0.45, 1.5):
        for u in us:
            if door0 < u < door1:
                K.rod(m, f, (u, layer_o, DOOR_H + 1.4), (u, layer_o, H - 0.3), 0.09, "steel_white")
            else:
                K.rod(m, f, (u, layer_o, 0.2), (u, layer_o, H - 0.3), 0.09, "steel_white")
        for z in zs:
            if z < DOOR_H + 1.4:
                K.rod(m, f, (u0, layer_o, z), (door0, layer_o, z), 0.09, "steel_white")
                K.rod(m, f, (door1, layer_o, z), (u1, layer_o, z), 0.09, "steel_white")
            else:
                K.rod(m, f, (u0, layer_o, z), (u1, layer_o, z), 0.09, "steel_white")
        for a, b in zip(us, us[1:]):
            for z0, z1 in zip(zs, zs[1:]):
                if in_door(a, b, z0):
                    continue
                K.rod(m, f, (a, layer_o, z0), (b, layer_o, z1), 0.05, "steel_white")
                K.rod(m, f, (a, layer_o, z1), (b, layer_o, z0), 0.05, "steel_white")
    for u in us[::2]:
        for z in zs:
            if not in_door(u - 0.1, u + 0.1, z):
                K.rod(m, f, (u, 0.45, z), (u, 1.5, z), 0.06, "steel_white")


def tall_pot(m, f, u, o, seed):
    """Tall white tile-pattern planter with a spiral topiary (by the terrace door)."""
    K.block(m, f, u - 0.35, u + 0.35, o - 0.35, o + 0.35, 0.0, 1.1, "azulejo")
    for k in range(4):
        K.block(m, f, u - 0.36, u + 0.36, o - 0.36, o + 0.36, 0.2 + k * 0.25, 0.28 + k * 0.25, "blue")
    twisted_sculpture(m, f, u, o, 1.6, size=0.7, step=0.1, material="leaf")
    p = f.at(u, o, 0)
    m.blob((p[0], p[1], 1.5), 0.35, "leaf", seed, 0.9)


def glass_vestibule(m, f, u0, u1, depth):
    """Glass box with a sliding door inside the secondary entrance on the west facade (low ceiling part)."""
    o1 = depth
    K.wall_o(m, f, o1, u0, u1, 0.0, 3.3, "glass", facing=1)
    K.wall_o(m, f, o1, u0, u1, 0.0, 3.3, "glass", facing=-1)
    for u in (u0, u1):
        K.wall_u(m, f, u, 0.3, o1, 0.0, 3.3, "glass", facing=1)
        K.wall_u(m, f, u, 0.3, o1, 0.0, 3.3, "glass", facing=-1)
        K.block(m, f, u - 0.05, u + 0.05, o1 - 0.05, o1 + 0.05, 0.0, 3.3, "rail")
    K.block(m, f, u0, u1, 0.3, o1 + 0.05, 3.3, 3.4, "rail")
    mid = (u0 + u1) / 2
    K.block(m, f, mid - 1.2, mid + 1.2, o1 - 0.02, o1 + 0.02, 0.0, 3.0, "rail")       # door frame (leaves open)
    K.floor_rect(m, f, u0 + 0.2, u1 - 0.2, 0.4, o1 - 0.1, 0.012, "mat")

def long_room_shell(m, f):
    u0, u1 = SPLIT_U, LONG_END_U
    K.floor_rect(m, f, u0, LOW_CEILING_END, 0.3, 29.7, LOW_Z, "soffit", up=False)
    for u in (u0 + 3.0, u0 + 9.0):
        for o in (4.0, 15.0, 24.0):
            K.downlight(m, f, u, o, LOW_Z)
    for u in (u0 + 4.0, u0 + 10.0):
        for o in (8.0, 20.0):
            K.square_column(m, f, u, o, 0, LOW_Z, 0.7)
    K.block(m, f, u0, LOW_CEILING_END, 0.3, 29.7, LOW_Z, LOW_Z + 0.4, "wall")                   # floor above
    K.floor_rect(m, f, LOW_CEILING_END, u1, 0.0, 30.0, LONG_H, "deck", up=False)
    K.floor_rect(m, f, u0, u1, 0.0, 30.0, LONG_H + 0.8, "concrete", up=True)
    for u in K.Frame().samples(LOW_CEILING_END + 2.0, u1 - 2.0, 6.0):
        K.truss(m, f, u, 0.0, 30.0, LONG_H - 1.6, LONG_H - 0.2, bays=10)
    for o in (3.0, 9.0, 15.0, 21.0, 27.0):
        K.rod(m, f, (LOW_CEILING_END, o, LONG_H - 0.45), (u1, o, LONG_H - 0.45), 0.2, "steel_white", 0.38)
    K.floor_rect(m, f, LOW_CEILING_END + 3.0, u1 - 3.0, 4.0, 7.0, LONG_H - 0.05, "skylight", up=False)
    K.floor_rect(m, f, LOW_CEILING_END + 3.0, u1 - 3.0, 18.0, 21.0, LONG_H - 0.05, "skylight", up=False)
    # longitudinal trusses under the roof with light strips, as in the video
    for o in (7.5, 15.0, 22.5):
        for u_a, u_b in zip(K.Frame().samples(LOW_CEILING_END, u1, 3.0), K.Frame().samples(LOW_CEILING_END, u1, 3.0)[1:]):
            K.rod(m, f, (u_a, o, LONG_H - 1.8), (u_b, o, LONG_H - 0.25), 0.07, "steel_white")
        K.rod(m, f, (LOW_CEILING_END, o, LONG_H - 1.8), (u1, o, LONG_H - 1.8), 0.14, "steel_white", 0.2)
        K.floor_rect(m, f, LOW_CEILING_END + 2.0, u1 - 2.0, o - 0.1, o + 0.1, LONG_H - 1.92, "light", up=False)
    for o in K.Frame().samples(0.5, 29.5, 1.0):                         # ribs of the grey roof deck
        K.block(m, f, LOW_CEILING_END, u1, o - 0.04, o + 0.04, LONG_H - 0.12, LONG_H, "deck")
    # the white space frame in front of the west glass, and big leaning round columns
    space_frame(m, f, LOW_CEILING_END, u1)
    for k in range(int((u1 - LOW_CEILING_END) // 12) + 1):
        ua = LOW_CEILING_END + 6.0 + k * 12.0                            # 86, 98, 110, 122: clear of the terrace door
        if ua > u1 - 2.0:
            continue
        a, b = f.at(ua, 2.4, 0.0), f.at(ua, 4.6, LONG_H - 0.2)
        m.tube(a, b, 0.32, "wall", 14)
        m.cylinder(a, 0.6, 0.35, "wall", 14, top_radius=0.34)


def long_room_end(m, f):
    u = LONG_END_U
    K.block(m, f, u - 0.15, u + 0.15, 0.0, 30.0, 0.0, LONG_H + 0.8, "wall")
    for o0 in (10.0, 17.0):
        K.block(m, f, u - 0.2, u - 0.15, o0, o0 + 2.6, 0.0, 2.4, "table")
    K.block(m, f, u - 0.25, u - 0.2, 13.2, 13.9, 2.7, 3.0, "exit")


def long_room_structure(m, f):
    glass_vestibule(m, f, 71.0, 77.5, 3.2)
    for du in (-5.8, 5.8):                                               # tall planters both sides of the terrace door
        tall_pot(m, f, TERRACE_DOOR_U + du, 1.2, 20 + int(du))
    for u0 in K.Frame().samples(84.0, LONG_END_U - 26.0, 22.0):         # white boxes with dark brown recesses (east side)
        K.block(m, f, u0, u0 + 11.0, 26.5, 29.7, 0.0, 3.8, "brown")
        K.block(m, f, u0 - 1.0, u0 + 12.0, 25.2, 29.7, 3.8, 6.4, "wall")
        for u in (u0 + 1.0, u0 + 10.0):
            K.round_column(m, f, u, 25.6, 0, 3.8, 0.28)
    K.block(m, f, LONG_END_U - 12.0, LONG_END_U - 0.3, 22.0, 29.7, 6.4, 9.6, "wall")             # mezzanine box at the end
    K.wall_o(m, f, 21.95, LONG_END_U - 12.0, LONG_END_U - 0.3, 6.6, 7.8, "glass", facing=-1)


def long_room_furniture(m, f):
    for u in K.Frame().samples(84.0, LONG_END_U - 4.0, 4.6):
        if abs(u - TERRACE_DOOR_U) > 3.5:                                # keep the terrace door free
            long_table(m, f, u, 5.4, seats=3)
    for k, u in enumerate(K.Frame().samples(84.0, LONG_END_U - 4.0, 4.0)):   # round tables in two staggered rows
        round_table(m, f, u, 12.5 if k % 2 == 0 else 14.0, 4)
        round_table(m, f, u + 2.0, 19.5 if k % 2 == 0 else 21.0, 4 if k % 3 else 3)
    grand_piano(m, f, 86.0, 5.4, 90)
    grand_piano(m, f, 113.0, 8.6, 90)
    for k, u in enumerate(K.Frame().samples(82.0, LONG_END_U - 3.0, 16.0)):  # fig trees in pots along the glass
        if abs(u - TERRACE_DOOR_U) > 4.5:
            big_plant(m, f, u, 2.4, 9 + k)
    for k, (u, o) in enumerate(((71.0, 14.0), (86.0, 24.0), (150.0, 23.5), (180.0, 23.5))):
        info_stand(m, f, u, o, k)


# ---------- assembly ----------
def make_hall(col):
    """Builds the interior in world coordinates (floor at z 0) and returns the objects."""
    f = building_frame()
    polys = regions(f)
    door_map = snapped_doors(polys[:2])
    objects = []
    for name, parts in (("Hall_Shell", (lambda m: shell(m, f, polys, door_map), lambda m: rest_of_wing(m, f, polys[2]))),
                        ("Hall_Roof", (lambda m: atrium_roof(m, f, polys[0]), lambda m: long_room_shell(m, f))),
                        ("Hall_Structure", (lambda m: atrium_structure(m, f), lambda m: artwork_wall(m, f),
                                            lambda m: long_room_end(m, f), lambda m: long_room_structure(m, f))),
                        ("Hall_Furniture", (lambda m: atrium_furniture(m, f), lambda m: long_room_furniture(m, f))),
                        ("Hall_Track_Rails", (lambda m: guard_rails(m, f),))):
        m = K.Mesh()
        for part in parts:
            part(m)
        obj = m.to_object(name, col)
        print(f"  {name}: {obj['triangles']} faces")
        objects.append(obj)
    return objects


def export_fbx(path):
    for o in bpy.data.objects:
        o.select_set(o.type == "MESH")
    bpy.ops.export_scene.fbx(filepath=path, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", use_selection=True, use_triangles=True, mesh_smooth_type="FACE")


FLOOR_Z = 14.5               # hall floor in the campus world = the road level outside (route_profile.json: 14.5 m)


def build_world_fbx():
    """out/NovaHallWorld.fbx: the hall at its real place and height, in the same axes as Luc's CampusWorld.fbx
    (build_world.export_for_unity turns the geometry 180 degrees so that Unity x = east, z = north). In Unity it goes
    at position 0 / rotation 0 next to the CampusWorld model and replaces the Edificio Principal block."""
    from mathutils import Matrix
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objects = make_hall(E.collection("Nova_Hall"))
    turn = Matrix.Rotation(math.pi, 4, "Z")
    for obj in objects:
        obj.location.z = FLOOR_Z
        obj.data.transform(turn)
        obj.location = turn @ obj.location
    for o in bpy.data.objects:
        o.select_set(o.type == "MESH")
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "NovaHallWorld.fbx"), object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", use_selection=True, use_triangles=True)
    print("done (world)")


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    make_hall(E.collection("Nova_Hall"))
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "NovaHall.blend"))
    export_fbx(os.path.join(OUT, "NovaHall.fbx"))
    print("done")


if __name__ == "__main__":
    if "world" in sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else False:
        build_world_fbx()                  # blender -b -P nova_hall.py -- world
    else:
        build()
