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
Everything is a number in the constants below. The big white NOVA letters of the real hall stand in front of the
stair (the name may be used, Noah 2026-11-10). hall_kit.py has the parts.

V18j (Luc's request, 2026-11-12): the Hovione atrium follows the survey reports/north-atrium-survey.md (part 2): the
stair 6 m further east behind the portable stage, the landing gallery, one glazed roof field, the entrance bridge with
timber joists, the white block, the beige floor with joints, the north exit colonnade. Every change is marked
"V18j (Luc's request)". The lanes (ROADS, rounded_line, the corners) are untouched.

Run:  /Applications/Blender.app/Contents/MacOS/Blender -b -P nova_hall.py   ->  out/NovaHall.blend, out/NovaHall.fbx
      ... -b -P nova_hall.py -- world   ->  out/NovaHallWorld.fbx (at its place in Luc's campus world, for Unity)
"""
import json
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
# V18j (Luc's request): the gallery the stair lands on moved east with the stair and now reaches the east gallery
# (IMG_7725 276-296 s: its edge runs on east from the stair top, the exit doors are under it). Was (7.0, 27.0), (30.0, 40.5).
BRIDGE_U = (7.0, 27.5)       # landing gallery in the north-east of the atrium, u from .. to
BRIDGE_O = (30.0, 60.8)      # .. and o from .. to; the wide staircase arrives at its south edge
# V18j (Luc's request): the Hovione atrium as surveyed (reports/north-atrium-survey.md part 2; u, o in metres)
STAIR_U = (37.0, 27.5)       # the big stair: foot and top (rises north), was 38.0 -> 27.0
# V20c (Luc's request, playtest 2026-11-13, s17): "the lectern is further right, not straight in front of the door;
# the stairs are also further right". The stair 4 m and the stage with its seat arc 5 m further east (+o, to the right
# when driving in through D4), so the lane runs left of both. Were STAIR_O (40.3, 45.0), STAGE_O (38.6, 44.6),
# SEAT_ARC ((51.3, 44.2), 7.6, (51.5, 36.6), (44.0, 42.0), 15).
STAIR_O = (44.3, 49.0)       # .. 4.7 m wide
STAIR_STEPS = 30
STAGE_U = (51.0, 55.7)       # portable stage (was a 6 x 6 m block at u 47..53, o 41..47)
STAGE_O = (43.6, 49.6)
STAGE_H = 0.45
SEAT_ARC = ((51.3, 49.2), 7.6, (51.5, 41.6), (44.0, 47.0), 15)   # centre, radius, first seat, last seat, seats
CLEAR_UNDER = 3.4            # V20c: where the east lane passes under the stair's high end, every part of the stair over
                             # the lane (and 0.3 m beside it) is at least this high: the chase camera rides 3.0 m over
                             # the kart (ChaseCamera.prefab FollowOffset y 3, near clip 0.3 m)
GLASS_ROOF_U = (22.0, 54.0)  # the one glazed roof field (was four skylight bands over the whole block)
GLASS_ROOF_O = (31.0, 50.0)
FLOOR_PANEL = 5.6            # saw-cut joints of the polished concrete floor
ENTRANCE_BRIDGE_U = (7.0, 37.5)   # timber-joist gallery across the entrance zone (IMG_7722 28-41 s, N07, N11)
ENTRANCE_BRIDGE_O = (12.5, 17.5)
WHITE_BLOCK_U = (35.5, 44.0)      # white two-storey block south of the main entrance; its first 2 m are the balcony
WHITE_BLOCK_O = (1.0, 8.6)        # (real: o 5..14; ours ends 0.5 m before the west lane's rail)
WHITE_BLOCK_H = 8.4
# V20c (Luc's request, s19/s20 + Street View of the north front): the north-east face (door D5) is a colonnade. Its
# ground floor stands CANOPY_DEPTH back under the white upper storey, which rests on slim square white pillars on the
# facade line (= the OSM outline = the real roof edge in the orthophoto; survey part 1.3: 7 m deep, 4.0-4.3 m clear;
# Luc's own V15b campus model: 6 m, 4.0 m). It runs from the blank white north panel to the south end of the face.
# Replaces V18j's exit_colonnade (0.75 m pillars standing in front of a full-height glass facade).
CANOPY_LINE = ((78.31, 154.61), (66.58, 184.49))   # the face (two collinear OSM edges), south end -> north corner
CANOPY_DEPTH = 6.0           # set-back of the ground floor behind the facade line
CANOPY_Z = 4.4               # underside of the upper storey (= top of the ground-floor glass elsewhere)
CANOPY_U = (4.05, 31.49)     # u of the set-back: north = the white panel's south edge, south = end of the face
CANOPY_PILLAR = 0.5          # slim square white pillars (Street View: about 0.5 m)
CANOPY_PILLARS_U = (29.0, 16.6, 10.3)   # 6-7 m apart; the bay of the exit is left wide (>= 1.2 m off the road edges)
CANOPY_RIBBON = (5.6, 8.0)   # the storey's long window band (z from .. to)
EXIT_OPEN_U = (14.5, 28.5)   # Luc (s19): the exit wider, above all to the left; was door D5 at u 19.65..27.15
EXIT_HEADER_Z = 4.0          # the exit's header: high enough for the chase camera (3.0 m over the kart)
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
# No black glass posts here (u from .. to, o from .. to): the niche in front of door D4, whose glass side walls run
# right along the fence, so the posts stood on the road side of it (Noah, 2026-11-11).
NO_MULLION_ZONES = [(66.5, 72.0, 33.2, 41.8)]


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
    # V20c (Luc's request, s17): in through the middle of D4 (o 37.4; was 37.0-37.2, 0.4 m beside it), straight on
    # left of the stage and the stair, right turn at the back,
    # under the stair's high end (the lane's right half), out through the exit in the colonnade on the axis of door D5
    # as HallDoors.json has it (u 22.73: snapped_doors keeps it 0.6 m off the face's end; before, the lane left 0.7 m
    # beside it). Was radii [5, 5, 6, 6, 4], points (82, 37.2) (66, 37) (60, 31) (40, 31) (34, 26.5) (23.4, 26.5) (23.4, 78).
    {"name": "east", "width": 6.0, "radii": [7.0, 8.0, 8.0],
     "points": [(82.0, 37.4), (26.6, 37.4), (26.6, 54.5), (22.7, 62.0), (22.7, 78.0)]},
]
RAIL_FREE_ROADS = ("east",)  # V20c (Luc, s17: "there is still a fence on the left, remove it"): no hall rails on the
                             # east lane. Luc's NO_FENCE (fence_free.py) already trims the white fence there; what was
                             # left were the rails' swings to the door frames, which no route piece reaches.
RAIL_OFFSET = 0.35           # guard rail distance outside the road edge (m)
RAIL_STOP = 0.15             # rails stop this far inside a facade; at a door they then end exactly at the frame
RAIL_WALL_GAP = 0.6          # rails never run closer than this to the glass facade (the posts went through it)
RAIL_FLARE = 8.0             # within this many metres of a door the rails swing out (or in) to the door frame
FRAME_REACH = 4.5            # a rail ending this close to a door frame is joined to it (with a curve beyond 2.5 m)
FRAME_GAP = 0.35             # ... and that curve keeps this far into the opening, clear of the glass beside the door

# V21c (Luc's request, playtest 2 of 13 Nov 2026, p4 + p5): at the main entrance D1 the rails run STRAIGHT in from the
# two door jambs (before: an S-bend from the 9.5 m door to the 7.7 m rail gauge within RAIL_FLARE; "the fence is
# curved for no reason, I would make it straight up to the entrance"). Left of the lane (the outside of the right
# turn) the rail runs straight on, parallel to the lane's entry axis, to the far side of the turn and ends there;
# from that corner the turn's outside is a row of chevron boards pointing right (turn_boards) instead of the curved
# rail in front of the NOVA letters ("here I would rather use signs that point to the right"), and the corridor's
# left rail begins where the boards end. Right of the lane (the inside of the turn) the rail runs straight in and
# then turns on ONE arc into the corridor's right rail: it has to stay, it keeps the karts off the cafe table, the
# sofas and the white block that stand inside the turn.
STRAIGHT_DOOR = "D1_main_entrance"
ENTRY_ARC_R = 4.25           # radius of the right rail's arc (0.35 m outside the lane's inner edge at its closest)
TURN_BOARDS_END_U = 38.3     # the board row ends here (u); 1 m past the south end of the NOVA letters' plinth
TURN_BOARD_T, TURN_BOARD_H = 0.18, 0.85    # boards: as thick as the Nova Kart wall outside, standing on the floor
TURN_BOARDS = 7              # boards in the row (about 2.4 m each, the size of the track's corner signs)


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


# V18j (Luc's request): the rebuilt atrium parts are fixed pieces, so instead of being dropped they are checked: the
# build stops when one of them would stand on a lane (same clearance as free(): lane half width + 0.3 m).
def must_be_free(name, u0, u1, o0, o1, step=0.25):
    """Raises when the floor rectangle u0..u1 x o0..o1 (hall coordinates) touches a lane."""
    nu, no = max(1, int((u1 - u0) / step)), max(1, int((o1 - o0) / step))
    for i in range(nu + 1):
        for j in range(no + 1):
            u, o = u0 + (u1 - u0) * i / nu, o0 + (o1 - o0) * j / no
            if not free(u, o, 0.0):
                raise RuntimeError(f"V18j: {name} at u {u:.1f}, o {o:.1f} stands on a race lane")


round_table = guarded(K.round_table, 1.6)
long_table = guarded(K.long_table, 1.8)
planter = guarded(K.planter, 0.8)
board = guarded(K.board, 1.0)
big_plant = guarded(K.big_plant, 0.9)
info_stand = guarded(K.info_stand, 0.6)
grand_piano = guarded(K.grand_piano, 1.4)


def nearest_wall(x, y):
    """Distance from (x, y) to the nearest outer wall line, and the nearest point on it."""
    poly = footprint()
    best = None
    for a, b in zip(poly, poly[1:] + poly[:1]):
        L2 = (b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2 or 1.0
        t = max(0.0, min(1.0, ((x - a[0]) * (b[0] - a[0]) + (y - a[1]) * (b[1] - a[1])) / L2))
        qx, qy = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
        dist = math.hypot(qx - x, qy - y)
        if best is None or dist < best[0]:
            best = (dist, qx, qy)
    return best


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


def door_list(door_map):
    """Every door in world coordinates: centre, tangent along the facade, normal into the building, width."""
    doors = []
    for key, items in door_map.items():
        a, b = (key[0], key[1]), (key[2], key[3])
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        t = ((b[0] - a[0]) / L, (b[1] - a[1]) / L)
        for s, width, name in items:
            c = (a[0] + t[0] * s, a[1] + t[1] * s)
            n = (-t[1], t[0])
            if not E.inside_polygon(c[0] + n[0], c[1] + n[1], footprint()):
                n = (-n[0], -n[1])
            doors.append({"name": name, "c": c, "t": t, "n": n, "width": width})
    return doors


def write_rail_course_json():
    """HallRailingCourse.json for Unity: the rail posts on the hall floor (x = east, y = up, z = north) and which posts
    are neighbours. Role 2's white fence follows exactly this course inside the hall (no copying in Unity needed)."""
    posts = [{"x": round(x, 3), "y": FLOOR_Z, "z": round(y, 3)} for x, y in RAIL_POSTS]
    links = [i for pair in RAIL_LINKS for i in pair]
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "HallRailingCourse.json"), "w") as fh:
        json.dump({"posts": posts, "links": links}, fh, indent=1)


def write_doors_json(door_map):
    """HallDoors.json for Unity (x = east, z = north): the test scene lines the road up with each door."""
    out = [{"name": d["name"], "x": round(d["c"][0], 3), "z": round(d["c"][1], 3), "nx": round(d["n"][0], 4),
            "nz": round(d["n"][1], 4), "width": d["width"]} for d in door_list(door_map)]
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "HallDoors.json"), "w") as fh:
        json.dump({"doors": out}, fh, indent=1)


RAIL_POSTS, RAIL_LINKS = [], []     # filled by guard_rails(): every rail post (world x, y) and neighbouring pairs


def guard_rails(m, f, door_map):
    """Low rails on both sides of each road: white posts every 2 m and a light lilac board (no top tube, Noah).
    Near a door they swing to the door frame and end exactly at its edge, so the fence outside can join them."""
    frames = []                                                          # (door centre, half width, two frame points) in u/o
    straight = []                                                        # V21c: the frame points of STRAIGHT_DOOR
    for d in door_list(door_map):
        cu = f.uo(*d["c"])
        edges = [f.uo(d["c"][0] + d["t"][0] * sg * d["width"] / 2, d["c"][1] + d["t"][1] * sg * d["width"] / 2) for sg in (1, -1)]
        frames.append((cu, d["width"] / 2, edges))
        if d["name"] == STRAIGHT_DOOR:
            straight = edges
    for road in ROADS:
        if road["name"] in RAIL_FREE_ROADS:
            continue
        line = road["line"]
        for side in (1, -1):
            pts = []
            for k, (u, o) in enumerate(line):
                a, b = line[max(k - 1, 0)], line[min(k + 1, len(line) - 1)]
                du, do = b[0] - a[0], b[1] - a[1]
                d = math.hypot(du, do) or 1.0
                off = road["width"] / 2 + RAIL_OFFSET
                for cu, half, _edges in frames:                          # swing out (or in) to the door frame
                    near = math.hypot(u - cu[0], o - cu[1])
                    if near < RAIL_FLARE:
                        x = 1.0 - near / RAIL_FLARE
                        off += (half - off) * x * x * (3 - 2 * x)
                pu, po = u - do / d * off * side, o + du / d * off * side
                x, y, _ = f.at(pu, po)
                if E.inside_polygon(x, y, footprint()):
                    gap, qx, qy = nearest_wall(x, y)
                    if gap < RAIL_WALL_GAP:                              # too close to the glass: move it into the hall
                        k = RAIL_WALL_GAP / max(gap, 0.001)
                        x, y = qx + (x - qx) * k, qy + (y - qy) * k
                        pu, po = f.uo(x, y)
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
                for end in (0, -1):                                      # end at a door frame: add its exact edge point
                    best = None
                    for cu, _half, edges in frames:
                        for e in edges:
                            dist = math.hypot(e[0] - run[end][0], e[1] - run[end][1])
                            if dist < FRAME_REACH and (best is None or dist < best[0]):
                                best = (dist, e, cu)
                    if best is None:
                        continue
                    dist, e, cu = best
                    if dist < 2.5:
                        bridge = [e]                                     # close: straight to the frame
                    else:
                        # further away (D4 sits in a niche): a soft curve that leaves the frame straight into the hall
                        ax, ay = cu[0] - e[0], cu[1] - e[1]                # along the door line, towards its centre
                        al = math.hypot(ax, ay) or 1.0
                        rx, ry = run[end][0] - e[0], run[end][1] - e[1]
                        # control point: straight in from the frame, i.e. the run-end direction minus its part along the door
                        dot = (rx * ax + ry * ay) / al
                        ctrl = (e[0] + (rx - ax / al * dot) * 0.7, e[1] + (ry - ay / al * dot) * 0.7)
                        bridge = []
                        for k in range(8, -1, -1):                       # from the run end (k=8 excluded) to the frame
                            t = k / 8
                            gap = FRAME_GAP * (1 - t * t)                 # off the niche's glass wall, into the opening
                            bridge.append(((1 - t) ** 2 * e[0] + 2 * (1 - t) * t * ctrl[0] + t * t * run[end][0] + ax / al * gap,
                                           (1 - t) ** 2 * e[1] + 2 * (1 - t) * t * ctrl[1] + t * t * run[end][1] + ay / al * gap))
                        bridge = bridge[1:]
                    if end == 0:
                        for p in bridge:
                            run.insert(0, p)
                    else:
                        run.extend(bridge)
                # V21c: a run that starts at a jamb of the main entrance is re-laid straight (straight_entry)
                at_jamb = [e for e in straight if math.hypot(e[0] - run[0][0], e[1] - run[0][1]) < 0.01]
                for run in (straight_entry(road, run, side) if at_jamb and road["name"] == "west" else [run]):
                    sub = run[::2]                                       # one segment per metre
                    if sub[-1] != run[-1]:
                        sub.append(run[-1])
                    for (u0, o0), (u1, o1) in zip(sub, sub[1:]):
                        m.beam(f.at(u0, o0, 0.58), f.at(u1, o1, 0.58), 0.05, 0.16, "rail_lilac")
                    ends = run[::4] if (len(run) - 1) % 4 == 0 else run[::4] + [run[-1]]
                    for k, (u0, o0) in enumerate(ends):                  # a post every 2 m and one at the end
                        p = f.at(u0, o0, 0)
                        RAIL_POSTS.append((p[0], p[1]))                   # also kept for HallRailingCourse.json
                        if k > 0:
                            RAIL_LINKS.append((len(RAIL_POSTS) - 2, len(RAIL_POSTS) - 1))
                        m.box((p[0], p[1], 0.36), 0.05, 0.05, 0.36, "wall", f.h)


def entry_rail_o(side):
    """o of the west lane's corridor rail south of the entrance turn: side +1 = left of the lane (east), -1 = right."""
    road = ROADS[0]
    return road["points"][1][1] + side * (road["width"] / 2 + RAIL_OFFSET)


def straight_entry(road, run, side):
    """V21c (Luc's request, p4 + p5): the rail run that starts at a jamb of the main entrance, re-laid. Returns the
    runs that replace it (points every 0.5 m, hall coordinates). side +1 = left of the lane (outside of the right
    turn): straight from the jamb to the corridor rail's line, a gap for the chevron boards (turn_boards), then the
    old run from TURN_BOARDS_END_U on. side -1 = right (inside of the turn): straight in, one arc of ENTRY_ARC_R,
    then the old run. Every new point keeps RAIL_OFFSET - 0.05 m or more to the lane's edge."""
    ju, jo = run[0]                                                       # the jamb: the exact frame point
    target = entry_rail_o(side)

    def line(a, b):
        n = max(1, int(math.ceil(math.hypot(b[0] - a[0], b[1] - a[1]) / 0.5)))
        return [(a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n) for k in range(n + 1)]

    if side > 0:
        first = line((ju, jo), (ju, target))
        k = next(i for i, p in enumerate(run) if p[0] > TURN_BOARDS_END_U + 0.1 and abs(p[1] - target) < 0.05)
        out = [first, [(TURN_BOARDS_END_U + 0.1, target)] + run[k:]]
    else:
        R = ENTRY_ARC_R
        cu, co = ju + R, target - R                                       # arc centre: tangent to u = ju and to o = target
        first = line((ju, jo), (ju, co))
        n = max(2, int(math.ceil(R * math.pi / 2 / 0.5)))
        arc = [(cu - R * math.cos(math.pi / 2 * i / n), co + R * math.sin(math.pi / 2 * i / n)) for i in range(1, n + 1)]
        k = next(i for i, p in enumerate(run) if p[0] > cu + 0.25 and abs(p[1] - target) < 0.05)
        out = [first + arc + run[k:]]
    limit = road["width"] / 2 + RAIL_OFFSET - 0.05
    for pts in out:
        for u, o in pts:
            gap = min(math.hypot(u - pu, o - po) for pu, po in road["line"])
            if gap < limit:
                raise RuntimeError(f"V21c: entry rail at u {u:.1f}, o {o:.1f} is {gap - road['width'] / 2:.2f} m from the lane's edge")
    print(f"  V21c entry rail, side {side:+d}: jamb u {ju:.2f} o {jo:.2f}, {len(out)} run(s), "
          f"{sum(math.hypot(b[0] - a[0], b[1] - a[1]) for pts in out[:1] for a, b in zip(pts, pts[1:]) if abs(a[0] - ju) < 1e-6 and abs(b[0] - ju) < 1e-6):.1f} m straight from the jamb")
    return out


# ---------- outer walls with doors ----------
def snapped_doors(polys, quiet=False):
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
        if not quiet:
            print(f"  door {name}: {best[0]:.1f} m from the marked spot")
    return out


def edge_key(a, b):
    return (round(a[0], 3), round(a[1], 3), round(b[0], 3), round(b[1], 3))


def in_no_mullion_zone(p):
    """True for a black glass post inside one of NO_MULLION_ZONES (hall coordinates u0, u1, o0, o1)."""
    u, o = building_frame().uo(p[0], p[1])
    for u0, u1, o0, o1 in NO_MULLION_ZONES:
        if u0 <= u <= u1 and o0 <= o <= o1:
            return True
    return False


def facade(m, a, b, inside, H, openings, style, posts=True):
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
        if glazed_ground and posts:                                  # V18j (Luc's request): posts=False = no dark posts
            for k in range(int((s1 - s0) / 1.6) + 1):                # mullions
                p = P(s0 + k * 1.6, top / 2, False)
                if in_no_mullion_zone(p):
                    continue                                         # stood on the road side of the fence (Noah)
                m.box(p, 0.05, 0.06, top / 2, "steel_dark", heading)
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
    """Sliding glass door: stainless posts, two leaves slid open, header with the motor, exit signs (no door mat: the
    track runs through, Noah)."""
    h = math.atan2(t[1], t[0])
    for side in (-1, 1):
        p = (c[0] + t[0] * side * (width / 2 + 0.08), c[1] + t[1] * side * (width / 2 + 0.08))   # in the wall, not the opening
        m.box((p[0], p[1], DOOR_H / 2), 0.08, 0.12, DOOR_H / 2, "rail", h)
        q = (c[0] + t[0] * side * (width / 2 + 0.55) + n[0] * 0.25, c[1] + t[1] * side * (width / 2 + 0.55) + n[1] * 0.25)
        m.box((q[0], q[1], (DOOR_H - 0.2) / 2), 0.55, 0.02, (DOOR_H - 0.2) / 2, "glass", h)
        e = (c[0] + n[0] * 0.25 + t[0] * side * width * 0.3, c[1] + n[1] * 0.25 + t[1] * side * width * 0.3)
        m.box((e[0], e[1], DOOR_H + 0.25), 0.3, 0.04, 0.12, "exit", h)
    m.box((c[0], c[1], DOOR_H + 0.2), width / 2 + 0.1, 0.18, 0.2, "rail", h)


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


def on_canopy_line(p, tol=0.05):
    """True for a footprint point on the north-east face (CANOPY_LINE)."""
    (ax, ay), (bx, by) = CANOPY_LINE
    L = math.hypot(bx - ax, by - ay)
    tx, ty = (bx - ax) / L, (by - ay) / L
    s = (p[0] - ax) * tx + (p[1] - ay) * ty
    return -tol <= s <= L + tol and abs((p[0] - ax) * ty - (p[1] - ay) * tx) < tol


def north_canopy(m, f, inside, H):
    """V20c (Luc's request, s19/s20 and his Street View panoramas of the north front): the colonnade of the exit.
    On the facade line: the white upper storey with its long window band, on slim square white pillars, and the blank
    white panel at the north corner (full height). CANOPY_DEPTH behind it: the ground-floor front: a white wall
    section, the wide glass exit (EXIT_OPEN_U, header at EXIT_HEADER_Z), a glass shopfront with a cash niche (no
    lettering). Between them: the white soffit with round lights, the hall floor, white return walls at both ends and
    a grey concrete bench block against the north one. Door D5 in HallDoors.json stays on the facade line, so the
    door road, the route and the white fence in Unity do not change."""
    (ax, ay), (bx, by) = CANOPY_LINE
    L = math.hypot(bx - ax, by - ay)
    tx, ty = (bx - ax) / L, (by - ay) / L
    nx, ny = inside
    heading = math.atan2(ty, tx)

    def s_of(u):                                                          # hall u -> distance along the face from its south end
        x, y, _ = f.at(u, 68.4)
        return (x - ax) * tx + (y - ay) * ty

    def P(s, z, depth=0.0):                                               # depth: metres into the building (-WALL = outer face)
        return (ax + tx * s + nx * depth, ay + ty * s + ny * depth, z)

    def panel(s0, s1, z0, z1, depth, mat, both=True):
        """Vertical face along the face at `depth`, seen from outside (and from inside when both)."""
        if s1 - s0 < 0.01 or z1 - z0 < 0.01:
            return
        m.face([P(s0, z0, depth), P(s1, z0, depth), P(s1, z1, depth), P(s0, z1, depth)], mat, (-nx, -ny, 0))
        if both:
            m.face([P(s0, z0, depth), P(s1, z0, depth), P(s1, z1, depth), P(s0, z1, depth)], mat, (nx, ny, 0))

    def slab(s0, s1, d0, d1, z0, z1, mat):                                # box: s0..s1 along, d0..d1 deep, z0..z1
        c = P((s0 + s1) / 2, (z0 + z1) / 2, (d0 + d1) / 2)
        m.box(c, (s1 - s0) / 2, (d1 - d0) / 2, (z1 - z0) / 2, mat, heading)

    D, Z, top = CANOPY_DEPTH, CANOPY_Z, H + 0.8
    s_north, s_south = s_of(CANOPY_U[0]), 0.0                             # set-back from s_south to s_north
    # ---- facade line: the north panel full height, the upper storey over the colonnade
    panel(s_north, L, 0.0, top, 0.0, "wall")
    panel(s_north, L, 0.0, top, -WALL, "wall")
    panel(s_south, s_north, Z, CANOPY_RIBBON[0], 0.0, "wall")
    panel(s_south, s_north, Z, CANOPY_RIBBON[0], -WALL, "wall")
    r0, r1 = s_south + 0.6, s_north - 0.6                                 # window band with white mullions every 1.6 m
    panel(s_south, r0, CANOPY_RIBBON[0], CANOPY_RIBBON[1], 0.0, "wall")
    panel(s_south, r0, CANOPY_RIBBON[0], CANOPY_RIBBON[1], -WALL, "wall")
    panel(r1, s_north, CANOPY_RIBBON[0], CANOPY_RIBBON[1], 0.0, "wall")
    panel(r1, s_north, CANOPY_RIBBON[0], CANOPY_RIBBON[1], -WALL, "wall")
    panel(r0, r1, CANOPY_RIBBON[0], CANOPY_RIBBON[1], 0.0, "glass")
    panel(r0, r1, CANOPY_RIBBON[0], CANOPY_RIBBON[1], -WALL, "glass")
    for k in range(int((r1 - r0) / 1.6) + 1):
        slab(r0 + k * 1.6 - 0.04, r0 + k * 1.6 + 0.04, -WALL - 0.05, -WALL + 0.02, CANOPY_RIBBON[0], CANOPY_RIBBON[1], "steel_white")
    panel(s_south, s_north, CANOPY_RIBBON[1], top, 0.0, "wall")
    panel(s_south, s_north, CANOPY_RIBBON[1], top, -WALL, "wall")
    m.face([P(s_south, top), P(L, top), P(L, top, -WALL), P(s_south, top, -WALL)], "wall", (0, 0, 1))      # wall top
    m.face([P(s_north, -PLINTH, -WALL), P(L, -PLINTH, -WALL), P(L, 0.0, -WALL), P(s_north, 0.0, -WALL)], "wall", (-nx, -ny, 0))
    m.face([P(s_south, Z, 0.0), P(s_north, Z, 0.0), P(s_north, Z, -WALL), P(s_south, Z, -WALL)], "soffit", (0, 0, -1))
    # ---- the soffit (and its upper side, seen from the galleries through the slot along the facade), round lights
    for z, up in ((Z, (0, 0, -1)), (Z + 0.01, (0, 0, 1))):
        m.face([P(s_south, z, 0.0), P(s_north, z, 0.0), P(s_north, z, D), P(s_south, z, D)], "soffit", up)
    for u in f.samples(CANOPY_U[0] + 2.5, CANOPY_U[1] - 2.5, 5.5):
        c = P(s_of(u), Z, D / 2)
        m.cylinder((c[0], c[1], Z - 0.02), 0.11, 0.02, "light", 10)
    # ---- return walls at both ends, up to the gallery floor (closes the slot over the soffit too)
    for s0, s1 in ((s_south, s_south + 0.3), (s_north - 0.3, s_north)):
        slab(s0, s1, -WALL, D, 0.0, GALLERY_Z, "wall")
    # ---- the set-back front: white section (south), the exit, the shopfront (north); a white band up to the gallery
    e0, e1 = sorted((s_of(EXIT_OPEN_U[1]), s_of(EXIT_OPEN_U[0])))         # exit from .. to (s grows northwards)
    panel(s_south, e0, 0.0, Z, D, "wall")                                 # small white wall section
    panel(e1, s_north, 0.0, Z, D, "glass")                                # shopfront glass
    for k in range(int((s_north - e1) / 1.6) + 1):                        # its dark posts
        s = e1 + 0.15 + k * 1.6
        if s < s_north - 0.1:
            slab(s - 0.05, s + 0.05, D - 0.06, D + 0.06, 0.0, Z, "steel_dark")
    slab(s_north - 2.6, s_north - 1.4, D - 0.16, D - 0.02, 0.9, 2.2, "steel_dark")     # cash niche (no lettering)
    slab(s_north - 2.45, s_north - 1.55, D - 0.18, D - 0.16, 1.45, 1.95, "light")      # its screen
    panel(s_south, s_north, Z, GALLERY_Z, D, "wall")
    slab(e0, e1, D - 0.15, D + 0.15, EXIT_HEADER_Z, Z, "steel_white")                 # header over the exit
    for s in (e0, e1):                                                    # stainless posts of the sliding doors
        slab(s - 0.08, s + 0.08, D - 0.12, D + 0.12, 0.0, EXIT_HEADER_Z, "rail")
    for k in range(3):                                                    # the leaves slid open, stacked in front of the shopfront
        s = e1 + 0.15 + k * 1.2
        slab(s, s + 1.15, D + 0.2 + k * 0.06, D + 0.22 + k * 0.06, 0.0, EXIT_HEADER_Z - 0.1, "glass")
    for s in (e0 + (e1 - e0) * 0.3, e0 + (e1 - e0) * 0.7):                # exit signs, inside, over the opening
        slab(s - 0.3, s + 0.3, D + 0.16, D + 0.2, EXIT_HEADER_Z - 0.32, EXIT_HEADER_Z - 0.08, "exit")
    # ---- pillars on the facade line, flush with its outer face
    half = CANOPY_PILLAR / 2
    for u in CANOPY_PILLARS_U:
        s = s_of(u)
        c = P(s, 0.0, -WALL + half)
        pu, po = f.uo(c[0], c[1])
        if not free(pu, po, half * 1.42 + 0.9):                           # 1.2 m off the lane's edge
            raise RuntimeError(f"V20c: canopy pillar at u {pu:.1f}, o {po:.1f} stands less than 1.2 m from a race lane")
        slab(s - half, s + half, -WALL, -WALL + 2 * half, -0.3, Z, "wall")
    # grey concrete bench block against the north return wall (survey part 1.3), clear of the lane
    slab(s_north - 0.85, s_north - 0.35, 0.6, 3.1, 0.0, 0.45, "concrete")
    print(f"  canopy: {L:.1f} m face, set back {D} m from u {CANOPY_U[0]} to {CANOPY_U[1]}, exit u {EXIT_OPEN_U}, "
          f"{len(CANOPY_PILLARS_U)} pillars at u {CANOPY_PILLARS_U}")


def floor_joints(m, f, poly):
    """V18j (Luc's request): saw-cut joints of the polished concrete floor, a FLOOR_PANEL grid over the atrium. Thin
    strips 4 mm over the floor (under the 4 cm lane collider of NovaHallInsert, so no kart feels them)."""
    half, z = 0.0175, 0.004
    for axis in (0, 1):
        for k in range(1, int(SPLIT_U / FLOOR_PANEL) + 1):
            c = k * FLOOR_PANEL
            strip = K.clip_polygon(poly, lambda p: f.uo(*p)[axis] - (c - half))
            strip = K.clip_polygon(strip, lambda p: (c + half) - f.uo(*p)[axis])
            if len(strip) >= 3 and abs(K.signed_area(strip)) > 0.01:
                K.polygon_face(m, strip, z, "floor_joint", up=True)


def shell(m, f, polys, door_map):
    """Floors and outer walls. The wall between atrium and long room and the end wall are built separately."""
    atrium, long_room = polys[0], polys[1]
    K.polygon_face(m, atrium, 0.0, "floor", up=True)
    floor_joints(m, f, atrium)                                          # V18j (Luc's request)
    for part in floor_with_well(long_room, f):                          # V21c: the floor round the stair well
        K.polygon_face(m, part, 0.0, "floor", up=True)
    canopy_done = False
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
            # V20c (Luc's request): the north-east face with door D5 is the colonnade, built once by north_canopy()
            # (was V18j: this facade with white square pillars in front of it, exit_colonnade())
            if poly is atrium and on_canopy_line(a) and on_canopy_line(b):
                if not canopy_done:
                    north_canopy(m, f, inside, H)
                    canopy_done = True
                continue
            facade(m, a, b, inside, H, door_map.get(edge_key(a, b), []), style)


# ---------- atrium ----------
def atrium_roof(m, f, poly):
    # V18j (Luc's request): ONE glazed field (GLASS_ROOF_U x GLASS_ROOF_O) instead of the four skylight bands; the
    # roof is opaque everywhere else: a dark steel deck over the entrance zone (west of the field), white round it.
    (gu0, gu1), (go0, go1) = GLASS_ROOF_U, GLASS_ROOF_O

    def cut(part, axis, limit, keep_below):
        return K.clip_polygon(part, lambda p: (limit - f.uo(*p)[axis]) if keep_below else (f.uo(*p)[axis] - limit))

    east = cut(poly, 1, go0, False)                                      # everything east of the field's west edge
    middle = cut(cut(east, 0, gu0, False), 0, gu1, True)
    for part, material in ((cut(poly, 1, go0, True), "deck_dark"), (cut(east, 0, gu0, True), "soffit"),
                           (cut(east, 0, gu1, False), "soffit"), (cut(middle, 1, go1, False), "soffit")):
        if len(part) >= 3 and abs(K.signed_area(part)) > 0.01:
            K.polygon_face(m, part, ATRIUM_H, material, up=False)
            K.polygon_face(m, part, ATRIUM_H + 0.8, "concrete", up=True)
    top = ATRIUM_H + 0.8                                                 # the glass lies in the roof surface
    K.floor_rect(m, f, gu0, gu1, go0, go1, top - 0.04, "skylight", up=False)
    K.floor_rect(m, f, gu0, gu1, go0, go1, top, "skylight", up=True)
    K.wall_u(m, f, gu0, go0, go1, ATRIUM_H, top, "steel_white", facing=1)      # white reveal of the opening
    K.wall_u(m, f, gu1, go0, go1, ATRIUM_H, top, "steel_white", facing=-1)
    K.wall_o(m, f, go0, gu0, gu1, ATRIUM_H, top, "steel_white", facing=1)
    K.wall_o(m, f, go1, gu0, gu1, ATRIUM_H, top, "steel_white", facing=-1)
    trusses = K.Frame().samples(3.0, SPLIT_U - 3.0, 6.0)
    for u in trusses:                                                    # trusses across the whole block
        K.truss(m, f, u, 0.5, 67.8, ATRIUM_H - 1.6, ATRIUM_H - 0.15, bays=16)
    for o in (10.0, 22.0, 34.0, 46.0, 58.0):
        K.rod(m, f, (0.5, o, ATRIUM_H - 0.4), (SPLIT_U - 0.5, o, ATRIUM_H - 0.4), 0.2, "steel_white", 0.36)
    for a, b in zip(trusses, trusses[1:]):                               # under the glass: a truss every ~2.8 m ..
        if gu0 < (a + b) / 2 < gu1:
            K.truss(m, f, (a + b) / 2, go0, go1, ATRIUM_H - 1.1, ATRIUM_H - 0.15, bays=6)
    for k in range(7):                                                   # .. and white glazing bars on a ~3 m grid
        o = go0 + (go1 - go0) * k / 6
        K.rod(m, f, (gu0, o, top - 0.12), (gu1, o, top - 0.12), 0.1, "steel_white", 0.16)
    for k in range(11):
        u = gu0 + (gu1 - gu0) * k / 10
        K.rod(m, f, (u, go0, top - 0.12), (u, go1, top - 0.12), 0.1, "steel_white", 0.16)


def gallery_slab(m, f, u0, u1, o0, o1, open_sides, timber=False, joists="o"):
    """First-floor slab: floor, underside (timber joists or white), white edge beam and glass balustrade on the open
    sides ('u0', 'u1', 'o0', 'o1')."""
    z = GALLERY_Z
    K.floor_rect(m, f, u0, u1, o0, o1, z, "stair", up=True)
    # V18j (Luc's request): the joists hang visibly under the timber deck (tour slide 2, N07), every 1.2 m across the
    # short side (joists = the direction they run in, "o" or "u"). Before they sat inside the slab, above the
    # underside face at z - 0.45, where nobody saw them.
    K.floor_rect(m, f, u0, u1, o0, o1, z - (0.18 if timber else 0.45), "timber" if timber else "soffit", up=False)
    if timber and joists == "o":
        for u in K.Frame().samples(u0 + 0.3, u1 - 0.3, 1.2):
            K.block(m, f, u - 0.08, u + 0.08, o0, o1, z - 0.5, z - 0.18, "timber")
    elif timber:
        for o in K.Frame().samples(o0 + 0.3, o1 - 0.3, 1.2):
            K.block(m, f, u0, u1, o - 0.08, o + 0.08, z - 0.5, z - 0.18, "timber")
    for side in open_sides:
        if side in ("u0", "u1"):
            gallery_edge(m, f, "u", u0 if side == "u0" else u1, o0, o1)
        else:
            gallery_edge(m, f, "o", o0 if side == "o0" else o1, u0, u1)


def gallery_edge(m, f, across, at, a, b, balustrade=True):
    """White edge beam and glass balustrade of a gallery: across 'u' = an edge at u = `at` running from o = a to b,
    across 'o' = an edge at o = `at` running from u = a to b. (V18j, Luc's request: taken out of gallery_slab so an
    edge can cover part of a side; balustrade=False leaves the beam only, where the stair arrives.)"""
    z = GALLERY_Z
    if across == "u":
        K.block(m, f, at - 0.12, at + 0.12, a, b, z - 0.55, z + 0.02, "wall")
        if balustrade:
            K.wall_u(m, f, at, a, b, z + 0.05, z + 1.12, "glass", facing=1)
            K.wall_u(m, f, at, a, b, z + 0.05, z + 1.12, "glass", facing=-1)
            K.block(m, f, at - 0.04, at + 0.04, a, b, z + 1.1, z + 1.16, "rail")
    else:
        K.block(m, f, a, b, at - 0.12, at + 0.12, z - 0.55, z + 0.02, "wall")
        if balustrade:
            K.wall_o(m, f, at, a, b, z + 0.05, z + 1.12, "glass", facing=1)
            K.wall_o(m, f, at, a, b, z + 0.05, z + 1.12, "glass", facing=-1)
            K.block(m, f, a, b, at - 0.04, at + 0.04, z + 1.1, z + 1.16, "rail")


def atrium_structure(m, f):
    G = GALLERY_DEPTH
    # V18j (Luc's request): the galleries as in the survey. North gallery with timber joists in the west part (its
    # balustrade stops where the entrance bridge and the landing gallery join it), the landing gallery in the
    # north-east from the stair to the east gallery (white underneath; the exit doors D5 are under it), the east
    # gallery, the timber-joist bridge across the entrance zone, the west balcony.
    # Before: gallery_slab(0.3, G, 0.6, 67.8, ("u1",), timber) / (G, SPLIT_U - 0.4, 67.8 - G, 67.8, ("o0",)) /
    #         (BRIDGE_U, BRIDGE_O, ("o0", "u1"), timber) / (44.0, SPLIT_U - 0.4, 0.6, 5.0, ("o1",), timber).
    eb_o0, eb_o1 = ENTRANCE_BRIDGE_O
    gallery_slab(m, f, 0.3, G, 0.6, BRIDGE_O[0], (), timber=True, joists="u")
    gallery_slab(m, f, 0.3, G, BRIDGE_O[0], 67.8, ())
    K.wall_o(m, f, BRIDGE_O[0], 0.3, G, GALLERY_Z - 0.45, GALLERY_Z - 0.18, "soffit", facing=-1)   # step timber / white underside
    gallery_edge(m, f, "u", G, 0.6, eb_o0)
    gallery_edge(m, f, "u", G, eb_o1, BRIDGE_O[0])
    gallery_slab(m, f, G, BRIDGE_U[1], 67.8 - G, 67.8, ())
    gallery_slab(m, f, BRIDGE_U[1], SPLIT_U - 0.4, 67.8 - G, 67.8, ("o0",))
    gallery_slab(m, f, BRIDGE_U[0], BRIDGE_U[1], BRIDGE_O[0], BRIDGE_O[1], ("o0",))
    gallery_edge(m, f, "u", BRIDGE_U[1], BRIDGE_O[0], STAIR_O[0] - 0.19)
    gallery_edge(m, f, "u", BRIDGE_U[1], STAIR_O[0] - 0.19, STAIR_O[1] + 0.19, balustrade=False)   # the stair arrives here
    gallery_edge(m, f, "u", BRIDGE_U[1], STAIR_O[1] + 0.19, BRIDGE_O[1])
    gallery_slab(m, f, 44.0, SPLIT_U - 0.4, 0.6, 5.0, ("o1",), timber=True)
    entrance_bridge(m, f)
    for u in K.Frame().samples(G + 1.0, SPLIT_U - 2.0, 8.0):                                   # slim round columns
        if free(u, 67.8 - G - 0.3, 0.3):                                                       # (one gives way to the track)
            K.round_column(m, f, u, 67.8 - G - 0.3, 0, GALLERY_Z - 0.45, 0.22)
    for o in K.Frame().samples(6.0, 60.0, 8.0):
        K.round_column(m, f, G + 0.3, o, 0, GALLERY_Z - 0.45, 0.22)
    for u in (12.0, 19.5):
        K.round_column(m, f, u, BRIDGE_O[0] + 0.3, 0, GALLERY_Z - 0.45, 0.22)
    # V18j (Luc's request): columns of the landing gallery: along its south edge (clear of the stair) and one row
    # under its middle. Was: two columns at u BRIDGE_U[1] + 0.3, o BRIDGE_O[0] + 0.3 and BRIDGE_O[1] - 0.3.
    # V20c (Luc's request): the east lane now turns east under the gallery's south edge, so its three columns there
    # (u 27.8, o 38.6 / 46.7 / 53.8) move 7.3 m north to u 20.5 (the one over o 38.6 goes: the arrow boards stand
    # there, and its neighbour at u 16.5 carries the slab). The corner column at o 30.3 stays.
    for u, o in ((BRIDGE_U[1] + 0.3, BRIDGE_O[0] + 0.3), (20.5, 46.7), (20.5, 53.8),
                 (16.5, 38.6), (16.5, 46.7), (16.5, 53.8)):
        must_be_free("landing gallery column", u - 0.22, u + 0.22, o - 0.22, o + 0.22)
        K.round_column(m, f, u, o, 0, GALLERY_Z - 0.45, 0.22)
    for u in (11.5, 21.5):                                               # round ceiling lights under the landing gallery
        for o in (34.5, 42.5, 50.5, 57.5, 64.3):
            if o < 68.4 - CANOPY_DEPTH:                                  # V20c: not over the colonnade (its own soffit lights)
                K.downlight(m, f, u, o, GALLERY_Z - 0.45)
    K.block(m, f, 14.0, 18.5, BRIDGE_O[0] - 0.12, BRIDGE_O[0] - 0.05, GALLERY_Z + 0.3, GALLERY_Z + 2.8, "piano")   # big screen
    # north wall, ground floor (seen left of the main entrance): white square pillars, doors, red exit signs
    for o in (4.0, 10.0, 16.0, 22.0):
        K.square_column(m, f, 1.2, o, 0, GALLERY_Z - 0.45, 0.7)
    for o0 in (6.0, 18.0):
        K.block(m, f, 0.2, 0.3, o0, o0 + 2.0, 0.0, 2.4, "table")
        K.block(m, f, 0.3, 0.42, o0 + 0.6, o0 + 1.4, 2.6, 2.85, "red")
    # V18j (Luc's request): the free column at (36.0, 46.0) would stand 1 m beside the moved stair's foot; it is now
    # at (39.0, 48.5), right of and in front of the foot as in IMG_7725 278-284 s. The other five are unchanged.
    # V20c: the first one moves 4 m east with the stair, (39.0, 48.5) -> (39.0, 52.5): again right of and in front of its foot
    for u, o in ((39.0, 52.5), (36.0, 54.0), (46.0, 54.0), (56.0, 54.0), (46.0, 22.0), (56.0, 22.0)):
        K.round_column(m, f, u, o, 0, ATRIUM_H - 1.6, 0.24)                                    # free columns up to the roof
        m.cylinder(f.at(u, o, 0), 0.44, 0.5, "wall", 14, top_radius=0.25)                      # flared foot (IMG_7725)

    # V18j (Luc's request): the white block south of the entrance is back, as far as the west lane leaves room
    # (Noah had left it out: it stood in the way of the track). No lettering on it.
    white_block(m, f)
    # V18j (Luc's request): the wide white staircase stands 6 m further east, behind the stage (IMG_7725 268 s):
    # foot at u 37, top at the landing gallery at u 27.5, o 40.3-45. Was: stair(m, f, 38.0, BRIDGE_U[1], 34.0, 39.5, GALLERY_Z)
    grand_stair(m, f, STAIR_U[0], STAIR_U[1], STAIR_O[0], STAIR_O[1], GALLERY_Z)
    leaning_columns(m, f)
    side_door(m, f)                                                     # V21c (Luc's request, p6)
    # "With great power ..." wall on the east side: light panel with a dark band (no text in the model)
    K.block(m, f, 40.0, 50.0, 67.3, 67.6, 3.0, 4.6, "table")
    K.block(m, f, 42.0, 48.0, 67.25, 67.3, 3.6, 4.0, "steel_dark")


def stair(m, f, u_bottom, u_top, o0, o1, z_top):
    """Straight wide flight rising towards smaller u: white steps on a solid base, side stringers, glass balustrades.
    (V18j, Luc's request: no longer called; the atrium's stair is grand_stair() below.)"""
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


def grand_stair(m, f, u_foot, u_top, o0, o1, z_top):
    """V18j (Luc's request): the big stair as it is (IMG_7725 268-296 s, N09): one straight flight rising towards
    smaller u, white steps between two deep white stringers, a glass balustrade with a steel handrail on each
    stringer, open underneath (white sloping underside, two slim columns), palms in troughs under the high end."""
    n = STAIR_STEPS
    run, rise = (u_foot - u_top) / n, z_top / n
    slope = z_top / (u_foot - u_top)
    waist = 0.42                                                         # underside this far below the step edges
    # V20c (Luc's request, s17: "pass half or fully under the stairs"): the east lane may run under the high end. Every
    # part lower than CLEAR_UNDER (underside and stringers: up to 0.47 m under the step edges) stays off the lane.
    u_low = u_foot - (CLEAR_UNDER + 0.47) / slope
    must_be_free("grand stair (below the camera's clearance)", u_low, u_foot + 0.4, o0 - 0.2, o1 + 0.2)

    def edge_z(u):                                                       # height of the line through the step edges
        return (u_foot - u) * slope

    for i in range(n):
        top = (i + 1) * rise
        ua = u_foot - run * (i + 1)
        K.block(m, f, ua, ua + run, o0, o1, max(0.0, top - 0.36), top, "stair")
    u_low = u_foot - waist / slope                                       # where the underside meets the floor
    m.face([f.at(u_low, o0, 0.0), f.at(u_low, o1, 0.0), f.at(u_top, o1, z_top - waist), f.at(u_top, o0, z_top - waist)],
           "stair", (0, 0, -1))
    K.wall_u(m, f, u_top, o0, o1, z_top - waist, z_top - 0.36, "stair", facing=-1)   # closes the top under the last step
    for o in (o0 - 0.07, o1 + 0.07):
        # stringer: 0.14 m plate, 0.62 m deep, from below the floor to the gallery edge, its top 0.2 m over the step edges
        K.rod(m, f, (u_foot + 0.35, o, edge_z(u_foot + 0.35) - 0.153), (u_top, o, edge_z(u_top) - 0.153), 0.14, "stair", 0.62)
        a, b = f.at(u_foot + 0.2, o, edge_z(u_foot + 0.2) + 0.2), f.at(u_top, o, z_top + 0.2)
        nrm = f.left()
        for sgn in (1, -1):                                              # glass, seen from both sides
            m.face([a, b, (b[0], b[1], z_top + 1.1), (a[0], a[1], a[2] + 0.9)], "glass", (nrm[0] * sgn, nrm[1] * sgn, 0))
        K.rod(m, f, (u_foot + 0.2, o, edge_z(u_foot + 0.2) + 1.1), (u_top, o, z_top + 1.1), 0.05, "rail")
        for k in range(7):                                               # steel posts of the handrail
            u = u_foot + 0.2 + (u_top - u_foot - 0.2) * k / 6
            K.block(m, f, u - 0.02, u + 0.02, o - 0.02, o + 0.02, edge_z(u) + 0.2, edge_z(u) + 1.1, "rail")
    u_col = u_top + 3.4                                                  # two slim columns under the flight
    for o in (o0 + 0.5, o1 - 0.5):
        must_be_free("stair column", u_col - 0.36, u_col + 0.36, o - 0.36, o + 0.36)
        K.round_column(m, f, u_col, o, 0, edge_z(u_col) - waist, 0.2)
        m.cylinder(f.at(u_col, o, 0), 0.36, 0.4, "wall", 12, top_radius=0.21)
    # V20c: the palm troughs stood under the high end (u_top + 1.7), now the lane: they move 3.2 m south, where the
    # flight is still 2.1 m up, and the seat cubes behind them (were at u_col + 1.4). Together with the two columns they
    # keep karts out from under the low part of the flight.
    for k, o in enumerate((o0 + 1.2, o1 - 1.2)):                         # palms in black troughs under the flight
        must_be_free("palm trough", u_top + 4.2, u_top + 5.6, o - 0.3, o + 0.3)
        K.planter(m, f, u_top + 4.9, o, 70 + k, palm=True)
    for o in (o0 + 0.9, o0 + 2.35, o0 + 3.8):                            # seat cubes under the flight
        K.block(m, f, u_col + 2.7, u_col + 3.22, o - 0.26, o + 0.26, 0.0, 0.46, "grey")


def entrance_bridge(m, f):
    """V18j (Luc's request): the timber-joist gallery across the entrance zone. In IMG_7722 (28-41 s), N07 and N11 the
    joist gallery crosses the view from the main entrance 10 m inside, runs south and ends at the white block's
    balcony. It spans the west lane overhead (underside 4.75 m); its columns stand clear of both lanes."""
    (u0, u1), (o0, o1) = ENTRANCE_BRIDGE_U, ENTRANCE_BRIDGE_O
    bu0, bo1 = WHITE_BLOCK_U[0], WHITE_BLOCK_O[1]
    gallery_slab(m, f, u0, u1, o0, o1, (), timber=True)
    gallery_slab(m, f, bu0, u1, bo1, o0, ("u1", "u0"), timber=True)     # link to the balcony of the white block
    gallery_edge(m, f, "o", o0, u0, bu0)
    gallery_edge(m, f, "o", o1, u0, u1)
    gallery_edge(m, f, "u", u1, o0, o1)
    for u, o in ((14.0, o0 - 0.25), (14.0, o1 + 0.25), (u1 - 0.6, o1 + 0.25)):
        must_be_free("entrance bridge column", u - 0.22, u + 0.22, o - 0.22, o + 0.22)
        K.round_column(m, f, u, o, 0, GALLERY_Z - 0.45, 0.22)
    for u in (20.5, 26.0, 31.5):                                         # between them it hangs from the roof on thin
        for o in (o0, o1):                                               # white rods, so the view to the letters is free
            K.rod(m, f, (u, o, GALLERY_Z + 1.16), (u, o, ATRIUM_H - 0.3), 0.06, "steel_white")


def white_block(m, f):
    """V18j (Luc's request): the white two-storey block south of the main entrance (IMG_7722 36-41 s, tour slide 1):
    a tall white box, in front of it (north) a lower part whose roof is a balcony with a glass balustrade, two grey
    sofas and a round side table at its foot. No lettering. Narrower than the real one: the west lane passes."""
    (u0, u1), (o0, o1) = WHITE_BLOCK_U, WHITE_BLOCK_O
    must_be_free("white block", u0 - 1.0, u1, o0, o1)
    K.block(m, f, u0 + 2.0, u1, o0, o1, 0.0, WHITE_BLOCK_H, "wall")
    K.block(m, f, u0, u0 + 2.0, o0, o1, 0.0, GALLERY_Z, "wall")
    gallery_edge(m, f, "u", u0, o0, o1)                                  # balustrade along the balcony's north edge
    for oa in (o0 + 0.9, o0 + 4.1):                                      # sofas against the block, facing north
        K.block(m, f, u0 - 0.95, u0 - 0.1, oa, oa + 2.6, 0.12, 0.42, "grey")
        K.block(m, f, u0 - 0.3, u0 - 0.1, oa, oa + 2.6, 0.42, 0.78, "grey")
        for ob in (oa, oa + 2.45):
            K.block(m, f, u0 - 0.95, u0 - 0.1, ob, ob + 0.15, 0.42, 0.6, "grey")
    p = f.at(u0 - 1.7, o0 + 3.75, 0)
    m.cylinder((p[0], p[1], 0.0), 0.2, 0.03, "table", 12)                # round white side table
    m.cylinder((p[0], p[1], 0.0), 0.04, 0.4, "table", 8)
    m.cylinder((p[0], p[1], 0.4), 0.38, 0.04, "table", 16)


# V21c (Luc's request, p6: "here it actually also goes in to the right, even if that is not part of the race. A closed
# entrance door is enough"): the side entrance in the wall right of the corridor (the west facade under the timber
# balcony, where the Cascais Academic Hall adjoins: u 39..68). A closed, framed double door with side lights in a
# white portal. Solid: every hall mesh gets a MeshCollider in NovaHallInsert, and the concrete wall and the hall's
# facade stand right behind it.
SIDE_DOOR_U = (53.0, 58.0)   # the portal along the wall (u from .. to), in the middle of the balcony's stretch
SIDE_DOOR_H = 2.75           # clear height of the opening; the leaves are 2.2 m with a glazed transom above
SIDE_WALL_IN = 0.37          # the concrete wall seen there is the Cascais Academic Hall's east wall in the campus world
                             # (env_campus): its face stands this far inside the hall's west glass (ray check, V21c)


def side_door(m, f):
    u0, u1 = SIDE_DOOR_U
    wall_o = max(f.uo(*nearest_wall(*f.at(u, 0.6)[:2])[1:])[1] for u in (u0, u1))   # inner face of the west facade
    o0 = wall_o + SIDE_WALL_IN + 0.03                                     # just in front of the concrete wall
    must_be_free("side door", u0, u1, o0, o0 + 0.5)
    pier, deep = 0.35, 0.3
    for ua in (u0, u1 - pier):                                            # white portal: two piers and a lintel
        K.block(m, f, ua, ua + pier, o0 - 0.12, o0 + deep, 0.0, SIDE_DOOR_H + 0.35, "wall")   # (they reach into the wall)
    K.block(m, f, u0 + pier, u1 - pier, o0 - 0.12, o0 + deep, SIDE_DOOR_H, SIDE_DOOR_H + 0.35, "wall")
    a, b = u0 + pier, u1 - pier                                           # the opening
    K.wall_o(m, f, o0 + 0.05, a, b, 0.0, SIDE_DOOR_H, "grey", facing=1)   # dark behind the glass: nothing to see through
    K.wall_o(m, f, o0 + 0.16, a, b, 0.0, SIDE_DOOR_H, "glass", facing=1)
    mid, leaf, bar = (a + b) / 2, 1.35, 0.035
    stiles = (a + 0.04, mid - leaf, mid - 0.04, mid + 0.04, mid + leaf, b - 0.04)   # frame, side lights, meeting stiles
    for u in stiles:
        K.block(m, f, u - bar, u + bar, o0 + 0.12, o0 + 0.2, 0.0, SIDE_DOOR_H, "steel_dark")
    for z0, z1 in ((0.0, 0.1), (2.2, 2.28), (SIDE_DOOR_H - 0.07, SIDE_DOOR_H)):   # threshold rail, transom, head
        K.block(m, f, a, b, o0 + 0.12, o0 + 0.2, z0, z1, "steel_dark")
    for sg in (-1, 1):                                                    # two pull handles on the meeting stiles
        u = mid + sg * 0.16
        K.block(m, f, u - 0.02, u + 0.02, o0 + 0.25, o0 + 0.29, 0.75, 1.65, "rail")
        for z in (0.85, 1.55):
            K.block(m, f, u - 0.015, u + 0.015, o0 + 0.2, o0 + 0.25, z - 0.015, z + 0.015, "rail")
    K.floor_rect(m, f, a + 0.3, b - 0.3, o0 + deep, o0 + deep + 1.3, 0.006, "mat")   # door mat in front of it (off the lane)
    print(f"  V21c side door: u {u0}..{u1}, wall at o {wall_o:.2f}")


def leaning_columns(m, f):
    """V18j (Luc's request): the big white leaning tube columns just inside the west glass, each on a bolted foot
    (tour slide 1, N11): two north of the main entrance, one south of it, clear of the door and the lane."""
    for u_foot, u_head in ((9.5, 13.0), (19.0, 15.5), (32.0, 35.5)):
        must_be_free("leaning column", u_foot - 0.5, u_foot + 0.5, 1.2, 2.3)
        a, b = f.at(u_foot, 1.75, 0.3), f.at(u_head, 1.2, ATRIUM_H - 1.6)
        m.tube(a, b, 0.26, "wall", 14)
        base = f.at(u_foot, 1.75, 0.0)
        m.box((base[0], base[1], 0.03), 0.5, 0.5, 0.03, "wall", f.h)                 # foot plate
        m.cylinder((base[0], base[1], 0.06), 0.42, 0.34, "wall", 14, top_radius=0.27)   # cast foot
        for du in (-0.4, 0.4):
            for do in (-0.4, 0.4):
                q = f.at(u_foot + du, 1.75 + do, 0.06)
                m.cylinder(q, 0.035, 0.05, "rail", 6)                                 # bolts


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
    # V18j (Luc's request): the welcome desk stands under the north gallery in front of a dark blank wall (no logo),
    # between two of the white pillars (tour slide 5, N07). Was: K.desk(m, f, 16.5, 3.4, 1.2, 5.0) by the west glass.
    K.desk(m, f, 4.6, 13.0, 5.0, 1.2)                                     # welcome desk, left of the doors
    K.block(m, f, 4.0, 5.2, 9.6, 10.4, 0.0, 0.9, "table")                 # its low white side counter
    K.block(m, f, 0.3, 0.45, 10.4, 15.6, 0.0, 3.9, "grey")                # the dark wall behind it
    # the big white NOVA letters straight ahead of the main entrance, as in the real hall (Noah, 2026-11-10: the
    # name may be used now). Read from the entrance: u runs south = left to right. All letters 2.6 m tall, 0.6 m deep.
    SO = 20.5                                                            # between the two track parts in front of the stair,
    U0 = 25.5                                                            # across the view from the main entrance (Noah's mark)
    H = 2.6
    K.rod(m, f, (U0 + 0.3, SO, 0.0), (U0 + 0.3, SO, H), 0.6, "wall")     # N: left stem
    K.rod(m, f, (U0 + 0.3, SO, H), (U0 + 2.1, SO, 0.0), 0.6, "wall")     #    diagonal
    K.rod(m, f, (U0 + 2.1, SO, 0.0), (U0 + 2.1, SO, H), 0.6, "wall")     #    right stem
    t, nrm = f.tangent(), f.left()                                       # O: ring standing on the floor
    m.ring(f.at(U0 + 4.2, SO - 0.3, H / 2), 1.28, 0.72, 0.6, "wall", basis=((0, 0, 1), (t[0], t[1], 0), (nrm[0], nrm[1], 0)))
    K.rod(m, f, (U0 + 6.0, SO, H), (U0 + 7.2, SO, 0.0), 0.6, "wall")     # V: point at the bottom
    K.rod(m, f, (U0 + 8.4, SO, H), (U0 + 7.2, SO, 0.0), 0.6, "wall")
    K.rod(m, f, (U0 + 8.9, SO, 0.0), (U0 + 10.1, SO, H), 0.6, "wall")    # A: point at the top
    K.rod(m, f, (U0 + 11.3, SO, 0.0), (U0 + 10.1, SO, H), 0.6, "wall")
    K.rod(m, f, (U0 + 9.35, SO, 0.95), (U0 + 10.85, SO, 0.95), 0.4, "wall")  # A: crossbar
    # V18j (Luc's request): the long low white plinth the letters stand on (tour slide 3). The letters are unchanged.
    must_be_free("letter plinth", U0 - 0.2, U0 + 11.8, SO - 0.55, SO + 0.55)
    K.block(m, f, U0 - 0.2, U0 + 11.8, SO - 0.55, SO + 0.55, 0.0, 0.3, "table")
    for k in range(6):                                                   # dark blue boards and planter troughs (north part)
        u = 8.5 + k * 2.0
        if k % 2 == 0:
            board(m, f, u, 22.0, 1.8, 2.2)
        else:
            planter(m, f, u, 21.6, 40 + k, palm=k % 4 == 1)
    for k in range(9):                                                   # planters along the way to the artwork wall
        planter(m, f, 44.0 + k * 2.6, 19.0, 60 + k, palm=k % 2 == 0)
    for u, o in ((11.0, 34.0), (16.5, 35.0), (10.5, 26.5), (16.0, 26.0)):
        round_table(m, f, u, o, 4)
    K.block(m, f, 50.0, 50.6, 0.6, 0.9, 1.0, 1.8, "red")                 # fire hose cabinet

    # east zone (IMG_7725): stage, cube seats in an arc, study tables with boards, planters, sofas by the exit
    # V18j (Luc's request): the portable stage and the seat arc where the walk video shows them (IMG_7725 268-278 s);
    # the stair now stands straight behind the stage. Was: a 6 x 6 m block at u 47..53, o 41..47 with two steps at
    # u 53, and 14 seats on an arc round (50, 44), r 6.5.
    portable_stage(m, f)
    seat_arc(m, f)
    for u in (42.0, 46.5, 51.0, 55.5, 60.0):
        long_table(m, f, u, 30.5, seats=2)
    board(m, f, 44.2, 33.6, 1.8, 2.0)
    board(m, f, 57.8, 33.6, 1.8, 2.0)
    for k in range(10):
        planter(m, f, 30.0 + k * 3.2, 65.6, 80 + k, palm=k % 3 == 0)
    # V18j (Luc's request): the second row of black planter troughs on the east side, in front of the east gallery's
    # columns, and the troughs beside the stair (IMG_7725 278-296 s)
    for k, u in enumerate((34.3, 37.7, 42.3, 45.7, 50.3, 53.7)):
        planter(m, f, u, 59.6, 100 + k, palm=k % 3 == 1)
    # V20c: the two troughs east of the stair move with it (were (29.6, 47.6), (32.8, 47.6)); the lane passes at u 26.6
    for k, u in enumerate((32.0, 35.0)):
        planter(m, f, u, 51.6, 110 + k)
    # V20c (Luc's request, s19: "put the benches rather left against the wall"): the three dark blue sofas stood at
    # u 12..19, o 62..63, in front of the exit; now left of the widened exit, backs to the set-back shopfront
    for k in range(3):
        u0 = 4.4 + k * 2.1
        must_be_free("sofa", u0, u0 + 1.9, 61.0, 62.0)
        K.block(m, f, u0, u0 + 1.9, 61.0, 62.0, 0.0, 0.75, "board")
    big_plant(m, f, 14.0, 58.0, 3)
    big_plant(m, f, 60.0, 45.0, 4)                                     # V20c: with the stage, 5 m east (was o 40.0)
    exit_arrows(m, f)
    turn_boards(m, f)                                                   # V21c (Luc's request, p5)
    entrance_seating(m, f)


# V20c (Luc's request, s18: "arrows to the right so you see where it goes out, roughly where the front table with
# chairs stands"): white boards with Boost Orange chevrons, as the track's corner signs (env_trackside.add_sign), on
# the outside of the lane's right turn at the back of the atrium, facing the karts that come in from D4.
ARROW_U = 22.6               # board line (u); the turn's outer edge is 1.6 m south of its nearest end
ARROW_O = (33.3, 41.1)       # three boards side by side, o from .. to (the lane comes north on o 37)
ARROW_H, ARROW_LIFT, ARROW_T = 0.9, 0.6, 0.08


def exit_arrows(m, f):
    o0, o1 = ARROW_O
    w = (o1 - o0 - 2 * 0.15) / 3                                         # board width (2.4 m, as outside)
    must_be_free("arrow boards", ARROW_U - 0.3, ARROW_U + 0.3, o0, o1)
    tn = f.tangent()
    front = ARROW_U + ARROW_T / 2 + 0.01                                 # the boards face +u (south, towards D4)
    for k in range(3):
        a = o0 + k * (w + 0.15)
        c = a + w / 2
        K.block(m, f, ARROW_U - ARROW_T / 2, ARROW_U + ARROW_T / 2, a, a + w, ARROW_LIFT, ARROW_LIFT + ARROW_H, "table")
        for so in (-0.8, 0.8):                                           # two steel posts behind each board
            K.block(m, f, ARROW_U - ARROW_T / 2 - 0.09, ARROW_U - ARROW_T / 2 - 0.01, c + so - 0.04, c + so + 0.04,
                    0.0, ARROW_LIFT + 0.1, "rail")
        # three chevrons pointing right (+o, the driver's right when driving north), env_trackside's proportions
        cw, t, vm, vh = 0.34, 0.13, ARROW_LIFT + ARROW_H / 2, ARROW_H * 0.36
        for u0 in (-0.72, 0.0, 0.72):
            tip, back = c + u0 + cw / 2, c + u0 - cw / 2
            for vend in (vm + vh, vm - vh):
                quad = [(tip, vm), (tip - t, vm), (back - t, vend), (back, vend)]
                m.face([f.at(front, o, z) for o, z in quad], "rail_orange", (tn[0], tn[1], 0))


def turn_boards(m, f):
    """V21c (Luc's request, p5: "here I would also rather use signs that point to the right"): the outside of the
    right turn behind the main entrance is a row of low white boards with Boost Orange chevrons pointing right (the
    look of exit_arrows and of the track's corner signs), in place of the curved rail in front of the NOVA letters.
    The row starts at the corner where the left entry rail ends (straight_entry) and runs south on the corridor
    rail's line to TURN_BOARDS_END_U, where that rail takes over. The boards stand on the floor and are solid (every
    hall mesh gets a MeshCollider in NovaHallInsert), so they are the barrier there: behind them are the open floor
    in front of the letters, the entrance bridge's column and the way into the atrium's east half. They are low
    (TURN_BOARD_H), so the letters 3 m behind them stay in full view."""
    jamb_u = min(e[0] for e in door_frame_uo(f, STRAIGHT_DOOR))          # the left jamb = the entry rail's line
    front = entry_rail_o(1)                                               # the boards' face = the rail's line
    u0, u1 = jamb_u - 0.05, TURN_BOARDS_END_U
    gap = 0.12
    w = (u1 - u0 - (TURN_BOARDS - 1) * gap) / TURN_BOARDS
    must_be_free("turn boards", u0, u1, front, front + TURN_BOARD_T)
    nrm = f.left()
    face_o = front - 0.006
    for k in range(TURN_BOARDS):
        a = u0 + k * (w + gap)
        c = a + w / 2
        K.block(m, f, a, a + w, front, front + TURN_BOARD_T, 0.0, TURN_BOARD_H, "table")
        K.block(m, f, a, a + w, front - 0.004, front + TURN_BOARD_T + 0.004, TURN_BOARD_H, TURN_BOARD_H + 0.05, "rail_orange")   # cap
        # three chevrons pointing right (+u = south = the driver's right when driving in), exit_arrows' proportions
        cw, t, vm, vh = 0.34, 0.13, TURN_BOARD_H / 2, TURN_BOARD_H * 0.36
        for du in (-0.72, 0.0, 0.72):
            tip, back = c + du + cw / 2, c + du - cw / 2
            for vend in (vm + vh, vm - vh):
                quad = [(tip, vm), (tip - t, vm), (back - t, vend), (back, vend)]
                m.face([f.at(u, face_o, z) for u, z in quad], "rail_orange", (-nrm[0], -nrm[1], 0))
    print(f"  V21c turn boards: {TURN_BOARDS} boards of {w:.2f} m, u {u0:.2f}..{u1:.2f}, face at o {front:.2f}")


def door_frame_uo(f, name):
    """The two frame points of a door in hall coordinates (u, o)."""
    for d in door_list(snapped_doors(regions(f)[:2], quiet=True)):
        if d["name"] == name:
            return [f.uo(d["c"][0] + d["t"][0] * sg * d["width"] / 2, d["c"][1] + d["t"][1] * sg * d["width"] / 2) for sg in (1, -1)]
    raise RuntimeError(f"door {name} not found")


def portable_stage(m, f):
    """V18j (Luc's request): the portable stage (IMG_7725 270-278 s): a dark grey carpet deck 0.45 m up on an
    aluminium frame with slim legs, open underneath, and a two-step unit on its south side."""
    (u0, u1), (o0, o1), H = STAGE_U, STAGE_O, STAGE_H
    must_be_free("stage", u0, u1 + 0.7, o0, o1)
    K.block(m, f, u0, u1, o0, o1, H - 0.07, H, "stage")
    for ua, ub, oa, ob in ((u0, u1, o0, o0 + 0.05), (u0, u1, o1 - 0.05, o1), (u0, u0 + 0.05, o0, o1), (u1 - 0.05, u1, o0, o1)):
        K.block(m, f, ua, ub, oa, ob, H - 0.17, H - 0.07, "rail")       # frame under the deck's edge
    for i in range(4):
        for j in range(5):
            u, o = u0 + 0.06 + (u1 - u0 - 0.12) * i / 3, o0 + 0.06 + (o1 - o0 - 0.12) * j / 4
            K.block(m, f, u - 0.03, u + 0.03, o - 0.03, o + 0.03, 0.0, H - 0.07, "rail")
    for i in range(2):                                                   # steps at the east end of the south side
        K.block(m, f, u1, u1 + 0.6 - 0.3 * i, o1 - 1.9, o1 - 0.5, 0.15 * i, 0.15 * (i + 1), "steel_dark")


def seat_arc(m, f):
    """V18j (Luc's request): the dark seat cubes in one curved row west and north-west of the stage, with an aisle
    between the row and the stage (IMG_7725 270-278 s)."""
    (cu, co), r, first, last, n = SEAT_ARC
    a0 = math.atan2(first[1] - co, first[0] - cu)
    a1 = math.atan2(last[1] - co, last[0] - cu)
    if a1 - a0 > math.pi:
        a1 -= 2 * math.pi
    if a0 - a1 > math.pi:
        a1 += 2 * math.pi
    for i in range(n):
        a = a0 + (a1 - a0) * i / (n - 1)
        u, o = cu + r * math.cos(a), co + r * math.sin(a)
        must_be_free("seat cube", u - 0.37, u + 0.37, o - 0.37, o + 0.37)
        p = f.at(u, o, 0.23)
        m.box(p, 0.26, 0.26, 0.23, "grey", f.h + a)


# Tables and chairs like in the long room, in two free spots of the atrium (Noah, 2026-11-11): the open floor between
# the welcome desk and the blue boards (north of the main-entrance road), and the strip between the road and the west
# glass south of the main entrance. round_table / long_table are guarded, so nothing lands on the track.
ENTRANCE_ROUND_TABLES = [(10.5, 9.0), (10.5, 13.5), (14.5, 11.0), (14.5, 15.5), (18.5, 8.5), (18.5, 13.0), (18.5, 17.5)]
# V18j (Luc's request): the white block now stands at u 35.5..44 by the west glass, so the three tables there moved
# (same number of tables): the round one at (33.5, 5.0) 1.5 m north to the block's corner as in IMG_7722 37-41 s, the
# round one at (43.5, 5.0) to the old place of the welcome desk (16.5, 4.0), the long one at (38.5, 4.6) between the
# two lanes at (50.5, 23.8), where the real hall has its study tables.
# V21c (Luc's request, p6: "a closed entrance door is enough, and the chairs can go too, plus the tables"): the cafe
# sets along the side wall right of the corridor are gone, the side entrance stands there (side_door). Were round
# tables at (48.5, 5.0) (under the west balcony, Noah), (58.0, 5.0), (62.5, 5.0) and a long one at (53.0, 4.6).
WEST_GLASS_ROUND_TABLES = [(32.4, 5.3), (16.5, 4.0)]    # (32.4: was (32.0, 5.6); 0.4 m clear of the new entry rail)
WEST_GLASS_LONG_TABLES = [(50.5, 23.8)]


def entrance_seating(m, f):
    for u, o in ENTRANCE_ROUND_TABLES:
        round_table(m, f, u, o, 4)
    for u, o in WEST_GLASS_ROUND_TABLES:
        round_table(m, f, u, o, 4)
    for u, o in WEST_GLASS_LONG_TABLES:
        long_table(m, f, u, o, seats=3)


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
    # V18j (Luc's request, see-through check): the roof over the low-ceiling part had no underside, so from the long
    # room the sky showed between the deck and the artwork wall (back faces are culled in Unity). Same deck, 12 m more.
    K.floor_rect(m, f, u0, LOW_CEILING_END, 0.0, 30.0, LONG_H, "deck", up=False)
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


# V21c (Luc's request, p7: "I would pull the piano a bit towards the kart, directly behind the posts, and behind it a
# staircase must go down (wide, but it must not get in the way of the track)"). The grand piano moves 8.7 m north
# (towards the kart that comes in through the artwork wall) and 2.2 m towards the lane: its tail now ends 0.55 m
# behind the right rail's posts (rail at o 9.15). Same turn as before (keyboard to the glass, tail to the lane), so
# Noah's AtriumPiano only needs the new position. Was PIANO_U 93.0 at o 5.4 (Noah, T10).
PIANO_U = 84.3
PIANO_O = 7.6
# Behind it (south, further along the lane): the wide open stair down to Floor 0. A well in the hall floor between
# the west glass and the right rail, a straight flight going down southwards (so the kart sees the treads fall
# away), a landing with a dark doorway, glass balustrades on the three closed sides. All of it stays STAIR_WELL_KEEP
# or more behind the rail line; the rail (in Unity: the fence and its collision wall) keeps the karts out.
STAIR_WELL_U = (87.0, 95.0)  # the well in the floor: u from (top step) .. to (wall behind the landing)
STAIR_WELL_O = (2.9, 8.5)    # .. and o from .. to: a 5.6 m wide flight
STAIR_WELL_DROP = 3.3        # down to Floor 0 (11.2 m; the hall floor is 14.5 m)
STAIR_WELL_STEPS = 20        # 0.165 m risers, 0.30 m treads: a 6 m flight and a 2 m landing
STAIR_WELL_KEEP = 0.5        # the well's rim stays at least this far behind the rail line (the lane's edge + RAIL_OFFSET)
FURNITURE_FREE_U = (PIANO_U - 3.0, STAIR_WELL_U[1] + 4.0)   # no long tables along the glass here (piano, stair, its foot)


def long_room_furniture(m, f):
    for u in K.Frame().samples(84.0, LONG_END_U - 4.0, 4.6):
        # keep the terrace door free, and (V21c) the place of the piano and the stair behind it
        if abs(u - TERRACE_DOOR_U) > 3.5 and not FURNITURE_FREE_U[0] <= u <= FURNITURE_FREE_U[1]:
            long_table(m, f, u, 5.4, seats=3)
    for k, u in enumerate(K.Frame().samples(84.0, LONG_END_U - 4.0, 4.0)):   # round tables in two staggered rows
        round_table(m, f, u, 12.5 if k % 2 == 0 else 14.0, 4)
        round_table(m, f, u + 2.0, 19.5 if k % 2 == 0 else 21.0, 4 if k % 3 else 3)
    if not free(PIANO_U, PIANO_O, 1.4):
        raise RuntimeError("V21c: the grand piano stands too close to the lane")
    K.grand_piano(m, f, PIANO_U, PIANO_O, 90)                           # V21c: was grand_piano(m, f, 93.0, 5.4, 90)
    stair_down(m, f)                                                    # V21c (Luc's request, p7)
    # V18 (Luc, 11 Nov): the second grand piano at u 113 is gone; the atrium has one piano (T10 above)
    for k, u in enumerate(K.Frame().samples(82.0, LONG_END_U - 3.0, 16.0)):  # fig trees in pots along the glass
        if abs(u - TERRACE_DOOR_U) > 4.5:
            big_plant(m, f, u, 2.4, 9 + k)
    for k, (u, o) in enumerate(((71.0, 14.0), (86.0, 24.0), (150.0, 23.5), (180.0, 23.5))):
        info_stand(m, f, u, o, k)


def stair_down(m, f):
    """V21c (Luc's request, p7): the wide stair down behind the piano. See STAIR_WELL_*. The hall floor has the
    matching hole (floor_with_well). Treads and risers are plain faces (the well's walls hide what is under them)."""
    (u0, u1), (o0, o1), drop, n = STAIR_WELL_U, STAIR_WELL_O, STAIR_WELL_DROP, STAIR_WELL_STEPS
    road = ROADS[0]
    limit = road["width"] / 2 + RAIL_OFFSET + STAIR_WELL_KEEP
    for u, o in ((u0, o1), (u1, o1), ((u0 + u1) / 2, o1), (u1, (o0 + o1) / 2)):
        gap = min(math.hypot(u - pu, o - po) for pu, po in road["line"])
        if gap < limit:
            raise RuntimeError(f"V21c: stair well rim at u {u:.1f}, o {o:.1f} is only {gap - road['width'] / 2 - RAIL_OFFSET:.2f} m behind the rail line")
    rise, run = drop / n, 0.30
    t = f.tangent()
    for i in range(n):                                                    # riser i, then the tread below it
        u = u0 + i * run
        K.wall_u(m, f, u, o0, o1, -(i + 1) * rise, -i * rise, "stair", facing=1)
        if i < n - 1:
            K.floor_rect(m, f, u, u + run, o0, o1, -(i + 1) * rise, "stair", up=True)
    foot = u0 + (n - 1) * run
    K.floor_rect(m, f, foot, u1, o0, o1, -drop, "floor", up=True)        # landing on Floor 0
    for i in range(n - 1):                                                # the walls beside the flight, step by step
        ua, z = u0 + i * run, -(i + 1) * rise
        K.wall_o(m, f, o0, ua, ua + run, z, 0.0, "wall", facing=1)
        K.wall_o(m, f, o1, ua, ua + run, z, 0.0, "wall", facing=-1)
    K.wall_o(m, f, o0, foot, u1, -drop, 0.0, "wall", facing=1)
    K.wall_o(m, f, o1, foot, u1, -drop, 0.0, "wall", facing=-1)
    K.wall_u(m, f, u1, o0, o1, -drop, 0.0, "wall", facing=-1)             # the wall behind the landing ..
    mid = (o0 + o1) / 2
    K.wall_u(m, f, u1 - 0.02, mid - 1.6, mid + 1.6, -drop, -drop + 2.5, "pit", facing=-1)   # .. with a dark doorway
    K.block(m, f, u1 - 0.06, u1 - 0.02, mid - 1.7, mid + 1.7, -drop + 2.5, -drop + 2.62, "steel_dark")
    K.block(m, f, u1 - 0.07, u1 - 0.02, mid - 1.2, mid + 1.2, -0.75, -0.6, "light")           # light strip over it
    # the rim: a low white kerb, glass balustrades with a steel handrail on the three closed sides
    kerb, top = 0.12, 1.1
    for o in (o0 - 0.05, o1 + 0.05):                                      # the two long sides
        K.block(m, f, u0, u1 + 0.1, o - 0.05, o + 0.05, 0.0, kerb, "wall")
        K.block(m, f, u0, u1 + 0.1, o - 0.025, o + 0.025, top - 0.04, top + 0.02, "rail")
        for facing in (1, -1):
            K.wall_o(m, f, o, u0, u1 + 0.1, kerb, top - 0.04, "glass", facing=facing)
    K.block(m, f, u1, u1 + 0.1, o0, o1, 0.0, kerb, "wall")                # the south end
    K.block(m, f, u1 + 0.025, u1 + 0.075, o0, o1, top - 0.04, top + 0.02, "rail")
    for facing in (1, -1):
        K.wall_u(m, f, u1 + 0.05, o0, o1, kerb, top - 0.04, "glass", facing=facing)
    for u, o in ((u0, o0 - 0.05), (u0, o1 + 0.05), (u1 + 0.05, o0 - 0.05), (u1 + 0.05, o1 + 0.05),
                 ((u0 + u1) / 2, o0 - 0.05), ((u0 + u1) / 2, o1 + 0.05), (u1 + 0.05, mid)):
        K.block(m, f, u - 0.03, u + 0.03, o - 0.03, o + 0.03, 0.0, top, "rail")           # steel posts
    for o in (o0 + 0.07, o1 - 0.07):                                      # handrails along the flight, on brackets
        K.rod(m, f, (u0 - 0.3, o, 0.95), (u0, o, 0.95), 0.045, "rail")
        K.rod(m, f, (u0, o, 0.95), (foot, o, -drop + rise + 0.95), 0.045, "rail")
        for k in range(4):
            u = u0 + (foot - u0) * (k + 0.5) / 4
            z = 0.95 - (u - u0) / run * rise
            K.block(m, f, u - 0.015, u + 0.015, min(o, o + (0.07 if o < mid else -0.07)), max(o, o + (0.07 if o < mid else -0.07)), z - 0.06, z - 0.03, "rail")
    print(f"  V21c stair down: well u {u0}..{u1}, o {o0}..{o1}, {n} steps of {rise:.3f} m, landing {u1 - foot:.1f} m at {-drop} m")


def floor_with_well(poly, f):
    """V21c: the long room's floor polygon as pieces round the stair well (STAIR_WELL_U x STAIR_WELL_O): the strip
    west of it, the strip east of it, and the middle strip north and south of it. The strips get a vertex at the
    well's corners, so neighbouring pieces share their corners (no T-junction, no hairline gap)."""
    (u0, u1), (o0, o1) = STAIR_WELL_U, STAIR_WELL_O

    def cut(part, axis, limit, below):
        return K.clip_polygon(part, lambda p: (limit - f.uo(*p)[axis]) if below else (f.uo(*p)[axis] - limit))

    def with_corners(part, o):
        """Adds the well's two corners on the line o to the polygon's edge that runs along that line."""
        out = []
        for a, b in zip(part, part[1:] + part[:1]):
            out.append(a)
            (ua, oa), (ub, ob) = f.uo(*a), f.uo(*b)
            if abs(oa - o) < 1e-6 and abs(ob - o) < 1e-6:
                for u in sorted((u0, u1), reverse=ua > ub):
                    if min(ua, ub) + 1e-6 < u < max(ua, ub) - 1e-6:
                        out.append(f.at(u, o)[:2])
        return out

    middle = cut(cut(poly, 1, o0, False), 1, o1, True)
    pieces = [with_corners(cut(poly, 1, o0, True), o0), with_corners(cut(poly, 1, o1, False), o1),
              cut(middle, 0, u0, True), cut(middle, 0, u1, False)]
    return [part for part in pieces if len(part) >= 3 and abs(K.signed_area(part)) > 0.01]


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
                        ("Hall_Track_Rails", (lambda m: guard_rails(m, f, door_map),))):
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


FLOOR_Z = 14.5 + 0.06        # hall floor = the road SURFACE outside: terrain 14.5 m (route_profile.json) + env_track's
                             # ROAD_LIFT 0.06 m. At 14.5 the kart hit a 6 cm edge when it drove out through a door.


def build_world_fbx():
    """out/NovaHallWorld.fbx: the hall at its real place and height, in the same axes as Luc's CampusWorld.fbx
    (build_world.export_for_unity turns the geometry 180 degrees so that Unity x = east, z = north). In Unity it goes
    at position 0 / rotation 0 next to the CampusWorld model and replaces the Edificio Principal block."""
    from mathutils import Matrix
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objects = make_hall(E.collection("Nova_Hall"))
    write_rail_course_json()
    write_doors_json(snapped_doors(regions(building_frame())[:2]))
    del RAIL_POSTS[:], RAIL_LINKS[:]
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
