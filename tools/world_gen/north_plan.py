"""The north side on one level (V18j-n, 2026-11-12): plan data, no Blender. Read by env_north (terrain, paving,
structures, dressing, the road's look), entrance_plan.avenue_side (ground colours) and the atlas' scan scripts.

Luc drove it: on the sett plaza at the north exit the ground rose and the kart got stuck; in reality the drop-off
forecourt, the floor along the north-east face, the street behind the hall and the start plaza are ONE flat level, and
the street simply continues west. Source: atlas report north-atrium-survey.md, part 1 (team walk video IMG_7725 of
7 Nov 2026, photospheres N01-N04) and the Google satellite mosaic of V18g-n (google_survey/n/, 8 px per metre), on
which the street's lawn edge, the path north of the lawn and the forecourt's outline were measured (+-0.5 m).

Frame of the hall's north face (nova_hall.NW_CORNER -> NE_CORNER): a = metres east along the face from the north-west
corner (3, 160), o = metres off the face to the north. The real street, south -> north:
  o 0 .. 1.5        buff concrete apron along the facade (flush)
  o 1.5 .. EDGE     pale granite setts (EDGE = 6.6 m at the west end, 8.5 m from a = 27 on: 5 to 7 m of setts)
  EDGE              a flush rust-brown steel strip of 0.15 m, then the lawn
  lawn              a triangle between the street, Milestone's east wing and the public path at y 189.6 .. 194.3
"""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
TRACK_JSON = os.path.join(REPO, "Assets", "_Project", "Visuals", "CampusWorld", "Data", "track.json")
DOORS_JSON = os.path.join(REPO, "Assets", "_Project", "Visuals", "CampusWorld", "Data", "HallDoors.json")

# ---------------------------------------------------------------------------------------------------------------
# Heights (m). route.json's track_z is 14.50 on the whole north side; env_track lays the road 6 cm over it.
# ---------------------------------------------------------------------------------------------------------------
ROAD_Z = 14.56             # the race road's surface, Unity's door roads (HallDoorRoad_*) and the hall's floor
LEVEL = 14.564             # the paving sheets: 4 mm over the road, right up to the road's edge (V18h-p's rule)
GROUND = 14.45             # the terrain under the whole level area = the road's bed (track_z - 0.05)
LAWN_DROP = 0.5            # the east lawn lies this far below the forecourt (survey 1.1: 3 risers down at (95, 174))
EAST_LAWN = LEVEL - LAWN_DROP
KEEP_OUT = 4.02            # m from the centreline: the sheet covers the road's shoulder (as the start plaza's sheet)
UNDER_ROAD = 0.032         # the door aprons lie this far under the road (just over qa_geometry's 3 cm coplanar limit)
BLEND = 14.0               # m beyond the level area over which the terrain returns to the elevation model
HEM = 1.0                  # m: beyond its free edges the sheet goes down under the ground (no lip to hit coming back)
HEM_DROP = LEVEL - GROUND + 0.04

# ---------------------------------------------------------------------------------------------------------------
# The frame of the hall's north face
# ---------------------------------------------------------------------------------------------------------------
NW, NE = (3.0, 160.0), (67.0, 184.0)                    # nova_hall.NW_CORNER, NE_CORNER
FACE_LEN = math.hypot(NE[0] - NW[0], NE[1] - NW[1])     # 68.35 m
U = ((NE[0] - NW[0]) / FACE_LEN, (NE[1] - NW[1]) / FACE_LEN)
N = (-U[1], U[0])


def pt(a, o):
    return (NW[0] + U[0] * a + N[0] * o, NW[1] + U[1] * a + N[1] * o)


def ao(x, y):
    dx, dy = x - NW[0], y - NW[1]
    return (dx * U[0] + dy * U[1], dx * N[0] + dy * N[1])


# ---------------------------------------------------------------------------------------------------------------
# The street behind the hall
# ---------------------------------------------------------------------------------------------------------------
APRON = 1.5                                             # m: buff concrete along the facade
EDGE_AO = [(-12.0, 6.6), (5.0, 6.6), (16.0, 7.5), (27.0, 8.5), (62.2, 8.3)]      # the setts' north edge (a, o), satellite
PATH_Y = (189.6, 194.3)                                 # the public path north of the lawn (pale setts), x 13 .. 62
PATH_X = (14.0, 62.0)
LAWN_APEX = (58.3, PATH_Y[0])                           # where the street's edge meets the path: the lawn's east tip
STRIP = 0.15                                            # m: the rust-brown steel strip between setts and lawn
# the real lawn between the street, Milestone's east wing (its walls) and the path
LAWN = [pt(*EDGE_AO[0]), pt(*EDGE_AO[1]), pt(*EDGE_AO[2]), pt(*EDGE_AO[3]), LAWN_APEX, (PATH_X[0], PATH_Y[0]),
        (15.2, 176.3), (9.7, 175.5), (10.1, 173.3), (-10.0, 170.0)]
# steel drain gratings across the setts: (a, o of the middle, length across the street, width). The two wide ones are
# on the satellite view ((31.7, 177.7) and (38.4, 180.3), 0.9 x 4.7 m); the linear drains of the walk video
# (369 s, 377 s: 0.35-0.4 m wide, 6-8 m long, every 15-20 m) stand between them and the street's ends
DRAINS = [(4.0, 4.0, 4.6, 0.38), (20.0, 4.6, 5.6, 0.38), (33.5, 5.0, 4.7, 0.9), (40.6, 5.0, 4.7, 0.9), (56.0, 4.9, 6.2, 0.38)]
APRON_GRATE = [(12.0, 22.0), (44.0, 52.0)]              # a from .. to: the 0.5 m steel grating strip in the apron (389 s)
MANHOLES = [(2.2, 164.9), (-6.4, 162.5), (12.6, 168.2), (48.0, 183.6)]      # round covers in the street (satellite)
PATH_COVERS = [(33.0, 191.0), (51.5, 191.2)]            # square covers in the path (satellite)
# the east end of the facade (survey 1.3: around x 55-62, from the video, +-3 m): a grey metal louvre screen on the
# wall, and a basement stair that runs down westwards along the facade inside a white wall, tube railing on top
LOUVRE_A, LOUVRE_H = (59.9, 64.6), 3.4                  # (east of a = 65 the race line is closer than 6.6 m)
STAIR_A, STAIR_O = (52.4, 59.6), (0.12, 1.42)           # the stair well (a from .. to, o from .. to), inside the apron
STAIR_DEPTH = 2.6                                       # m below the street at its west end
WELL_WALL, WELL_H, RAIL_H = 0.2, 0.25, 1.1              # the well's white upstand and the galvanised tube railing on it
# the two grey galvanised boxes of the lawn (real: (37.5, 185.3) and (41.5, 187.3), 3 x 1.5 x 1.1 m, on the race
# line): the nearest free lawn north of the road, long side along the street
BOXES = [(26.4, 187.5), (21.9, 186.2)]                  # world x, y of their middles
BOX_SIZE = (3.0, 1.5, 1.1)
# young staked trees of the lawn strip (real: a row 2 m behind the street's edge, every 6-10 m; that row is under the
# race road): three stand in the free corner of the lawn north of the road, 7.6 m and more from the race line
TREES = [(16.3, 181.0), (18.6, 183.9), (16.2, 187.4)]    # world x, y
NORTH_WALL = [(19.0, 194.75), (46.2, 194.75)]           # the low white wall on the path's north side (survey: 1.0-1.1 m)
NORTH_WALL_SIZE = (0.35, 1.05)
# forecourt: grey concrete bench blocks (2.5 x 0.5 x 0.45 m; real: one against the corner pier, others loose near the
# colonnade, all within 6.6 m of our race line) and the bus stop (real: along the colonnade): the nearest free places
BENCHES = [(72.0, 196.3, 100.0), (87.6, 185.4, 69.0), (90.4, 171.4, 158.0)]      # x, y, bearing of the long side
BENCH_SIZE = (2.5, 0.5, 0.45)
BUS_STOP = (83.0, 191.0)                                # a slim post with a blank plate (the tour map marks a stop here)
# Rua da Holanda starts at the forecourt's north edge: asphalt x 75.5 .. 80.7 from y 192.5 (satellite), level
NORTH_Y = 197.6                                         # the sheet's north edge between x 62 and 88.5
HOLANDA_Y = 200.6                                       # ... and of the street's tab (the map's road starts there)
HOLANDA = [(75.5, 192.5), (81.0, 192.5), (81.0, HOLANDA_Y), (75.5, HOLANDA_Y)]     # V20d: east kerb 81.0 (was 80.7)
EAST_PATH = [(126.0, 190.2), (126.0, 194.3), (88.5, 194.3)]      # the paved band east of the waste station (north kerb y 194.3)


def inside(x, y, poly):
    hit = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            hit = not hit
    return hit


def seg_distance(x, y, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    t = min(max(((x - a[0]) * dx + (y - a[1]) * dy) / max(dx * dx + dy * dy, 1e-12), 0.0), 1.0)
    return math.hypot(x - a[0] - dx * t, y - a[1] - dy * t)


def ring_distance(x, y, ring):
    return min(seg_distance(x, y, a, b) for a, b in zip(ring, ring[1:] + ring[:1]))


def out_distance(x, y, ring):
    """0 inside the ring, else the distance to it."""
    return 0.0 if inside(x, y, ring) else ring_distance(x, y, ring)


def _ss(v):
    v = min(max(v, 0.0), 1.0)
    return v * v * (3 - 2 * v)


# ---------------------------------------------------------------------------------------------------------------
# The forecourt (env_north has the ring, the bands, the stair and the waste station)
# ---------------------------------------------------------------------------------------------------------------
EDGE = [(104.5, 147.6), (92.5, 177.3)]                  # east edge of the setts, south -> north (V18g, satellite 21z:
                                                        # (100.8, 156.8) - (92.5, 177.3); V18j: the same line on to y 147.6)
BANDS = 2.3                                             # m: the pink-buff and the ochre band east of it
_eu = ((EDGE[1][0] - EDGE[0][0]) / math.hypot(EDGE[1][0] - EDGE[0][0], EDGE[1][1] - EDGE[0][1]),
       (EDGE[1][1] - EDGE[0][1]) / math.hypot(EDGE[1][0] - EDGE[0][0], EDGE[1][1] - EDGE[0][1]))
_ee = (_eu[1], -_eu[0])                                 # east across the edge
RIM = [(EDGE[0][0] + _ee[0] * BANDS, EDGE[0][1] + _ee[1] * BANDS),      # the bands' outer edge: the drop to the lawn
       (EDGE[1][0] + _ee[0] * BANDS, EDGE[1][1] + _ee[1] * BANDS)]
LAWN_N = [EDGE[1], (112.0, 184.8), (126.0, 190.2)]      # the east lawn's north edge (bearing 69), west -> east
PLAZA_CORNER = [(2.8, 159.8), (1.5, 163.5)]             # entrance_plan.RING's north-east end: the start plaza's sheet
                                                        # ends on this line, ours begins there
# the sheet's outline (clockwise from the start plaza; the hall, the buildings and the road are cut out of it):
# street, path, forecourt, the band east of the waste station, the start of R. da Holanda
SHEET = [PLAZA_CORNER[1], PLAZA_CORNER[0], (72.0, 156.5), (82.0, 153.0), (93.0, 149.5), EDGE[0], EDGE[1], LAWN_N[1],
         EAST_PATH[0], EAST_PATH[1], EAST_PATH[2], (88.5, NORTH_Y), (HOLANDA[2][0] + 0.15, NORTH_Y),
         (HOLANDA[2][0] + 0.15, HOLANDA_Y), (HOLANDA[3][0] - 0.15, HOLANDA_Y), (HOLANDA[3][0] - 0.15, NORTH_Y),
         (PATH_X[1], NORTH_Y), (PATH_X[1], PATH_Y[1]),
         (PATH_X[0] - 0.1, PATH_Y[1]), (PATH_X[0], PATH_Y[0]), LAWN_APEX, pt(*EDGE_AO[3]), pt(*EDGE_AO[2]), pt(*EDGE_AO[1]),
         pt(0.7, EDGE_AO[1][1])]
# free edges of the sheet that border lawn or open ground at the same level: a hem goes down under the ground there
# (the hem lies to the left of each line's direction)
HEM_LINES = [[LAWN_APEX, (PATH_X[0], PATH_Y[0])],                                   # the path's south edge at the lawn
             [(PATH_X[0] - 0.1, PATH_Y[1]), (PATH_X[1], PATH_Y[1])],                # ... and its north edge
             [(PATH_X[1], NORTH_Y), (HOLANDA[3][0] - 0.15, NORTH_Y), (HOLANDA[3][0] - 0.15, HOLANDA_Y)],   # north edge west of
             [(HOLANDA[2][0] + 0.15, HOLANDA_Y), (HOLANDA[2][0] + 0.15, NORTH_Y), (88.5, NORTH_Y), (88.5, 194.3),     # R. da Holanda
              (126.0, 194.3)],                                                                                       # and east of it
             [(126.0, 194.3), (126.0, 190.2)]]
SOUTH_EDGE = [RIM[0], EDGE[0], (93.0, 149.5)]           # the buff concrete's south edge: the ground falls away south of
                                                        # it (to Student Central's east side); the rim's kerb wall goes round


def hem_ring(line):
    """The hem's polygon outside a free edge (to the left of the line's direction), mitred at the corners."""
    out = []
    n = len(line)
    for k, p in enumerate(line):
        ns = []
        for a, b in ([(line[k - 1], p)] if k > 0 else []) + ([(p, line[k + 1])] if k < n - 1 else []):
            d = math.hypot(b[0] - a[0], b[1] - a[1])
            ns.append((-(b[1] - a[1]) / d, (b[0] - a[0]) / d))
        if len(ns) == 1:
            out.append((p[0] + ns[0][0] * HEM, p[1] + ns[0][1] * HEM))
        else:
            f = HEM / (1.0 + ns[0][0] * ns[1][0] + ns[0][1] * ns[1][1])
            out.append((p[0] + (ns[0][0] + ns[1][0]) * f, p[1] + (ns[0][1] + ns[1][1]) * f))
    return list(line) + out[::-1]


def hem_drop(x, y):
    """Metres the sheet lies under LEVEL at (x, y): 0 on the sheet, HEM_DROP at the hem's outer edge."""
    best = 0.0
    for line in HEM_LINES:
        ring = hem_ring(line)
        if inside(x, y, ring) or ring_distance(x, y, ring) < 0.004:
            d = min(seg_distance(x, y, a, b) for a, b in zip(line, line[1:]))
            best = max(best, HEM_DROP * min(d / HEM, 1.0))
    return best


# ---------------------------------------------------------------------------------------------------------------
# Terrain: one level, the east lawn 0.5 m lower
# ---------------------------------------------------------------------------------------------------------------
# where the 5 m terrain nodes go to GROUND: street, lawn triangle, path, forecourt, the buff concrete at Student
# Central, the band east of the waste station, the first metres of R. da Holanda. South of it lies the hall.
ZONE = [pt(-11.0, -0.5), pt(-11.0, 9.6), pt(18.0, 9.6), (15.9, 176.6), (13.7, 189.8), (13.7, 195.3), (46.6, 195.3),
        (46.6, 197.6), (66.0, 197.6), (68.0, 199.6), (128.0, 199.6), (128.0, 191.0), LAWN_N[1], RIM[1], RIM[0],
        (106.3, 146.2), (99.0, 146.4), (93.0, 150.5), (91.5, 157.0), (84.5, 153.5), (80.7, 148.5), (78.3, 154.6),
        (76.7, 158.7), (66.6, 184.5), (8.3, 161.9), (2.75, 159.79)]
WEST_FADE = (-14.0, -8.0)  # x: the terrain change fades out between these (west of it the start plaza has its own level)
POOL = [(-3.34, 144.8), (7.01, 148.93), (2.75, 159.79), (-7.59, 155.86)]    # campus_plan.A_POOL: its basin stays
# the east lawn: below the forecourt's east rim and south of the paved band; at its north-east end it comes up to
# the band (RETURN: from x .. to x, from y .. to y), so a kart that dropped onto the lawn can drive back up
EAST = [RIM[0], RIM[1], LAWN_N[1], LAWN_N[2], (130.0, 191.7), (130.0, 144.0), (106.0, 144.0)]
RETURN = ((108.0, 122.0), (174.0, 184.0))
EAST_REACH = 7.2           # m: nodes this close to the east lawn (under the sheet's rim) are as low as the lawn, so
                           # the terrain at the rim is at lawn level (a 5 m cell cannot make the step; the rim wall does)


def east_lawn_z(x, y):
    (xa, xb), (ya, yb) = RETURN
    return EAST_LAWN + (GROUND - EAST_LAWN) * _ss((x - xa) / (xb - xa)) * _ss((y - ya) / (yb - ya))


# Luc, 12 Nov 2026 (after driving V18j, screenshot from the ring looking north): the ground north of the forecourt
# rose as a grass hill (the elevation model climbs 3 m to the world's edge, the 14 m BLEND squeezed the first 1.5 m
# into one bank). He wants the view to run on level ("weiter geradeaus, nicht ueber einen Huegel"), as the survey's
# Rua da Holanda continues level: from NORTH_OPEN_Y to the world's edge the terrain is the street's level, fading
# back to the elevation model over the west and east x bands.
NORTH_OPEN_Y = 190.0
NORTH_OPEN_X = ((-14.0, 8.0), (140.0, 165.0))     # (west fade from .. to), (east fade from .. to)


def north_open_weight(x, y):
    """Weight 0..1 with which the terrain north of the forecourt takes the street's level (Luc, 12 Nov)."""
    (wa, wb), (ea, eb) = NORTH_OPEN_X
    if y < NORTH_OPEN_Y - 6.0 or x <= wa or x >= eb:
        return 0.0
    return _ss((x - wa) / (wb - wa)) * (1.0 - _ss((x - ea) / (eb - ea))) * _ss((y - NORTH_OPEN_Y + 6.0) / 6.0)


def level_target(x, y):
    """(height, weight 0..1) the terrain takes at (x, y); weight 0 = the elevation model stays."""
    z, w = _level_target(x, y)
    wn = north_open_weight(x, y)
    if wn > w and z >= GROUND - 1e-6:
        return GROUND, wn
    return z, w


def _level_target(x, y):
    if not (WEST_FADE[0] < x < 152.0 and 122.0 < y < 216.0) or out_distance(x, y, POOL) < 2.5:
        return GROUND, 0.0
    fade = _ss((x - WEST_FADE[0]) / (WEST_FADE[1] - WEST_FADE[0]))
    de = out_distance(x, y, EAST)
    if de <= EAST_REACH:
        return east_lawn_z(x, y), 1.0
    dz = out_distance(x, y, ZONE)
    if dz <= 0.0:
        return GROUND, fade
    if de - EAST_REACH < dz:
        return east_lawn_z(x, y), 1.0 - _ss((de - EAST_REACH) / BLEND)
    return GROUND, fade * (1.0 - _ss(dz / BLEND))


# ---------------------------------------------------------------------------------------------------------------
# What Unity does at the hall's doors (HallDoorApproach.cs + NovaHallInsert.cs, read 2026-11-12; checked against the
# generated HallDoorRoad_*.asset: 0.03 mm). In front of each door the route is lined up with the door's axis, the old
# road is cut away 5.5 m either side of the old line (and width / 2 + 1.5 m either side of the new one) and a straight
# door road is laid at ROAD_Z. Whatever lies between that road and our paving was bare terrain 13 cm lower.
# ---------------------------------------------------------------------------------------------------------------
DOOR_REACH, DOOR_FULL, FENCE_GAP, DOOR_RUN = 14.0, 3.0, 0.3, 10


class Doors:
    def __init__(self):
        self.ok = os.path.exists(TRACK_JSON) and os.path.exists(DOORS_JSON)
        self.pieces = []
        if not self.ok:
            return
        pts = json.load(open(TRACK_JSON))["points"]
        self.before = [(p["x"], p["z"]) for p in pts]
        self.wb = [p["width"] for p in pts]
        self.after, self.wa = list(self.before), list(self.wb)
        names, facade = [], []
        n = len(pts)
        for d in json.load(open(DOORS_JSON))["doors"]:
            c, inward, along = (d["x"], d["z"]), (d["nx"], d["nz"]), (d["nz"], -d["nx"])
            door = min(range(n), key=lambda i: math.hypot(self.after[i][0] - c[0], self.after[i][1] - c[1]))
            if math.hypot(self.after[door][0] - c[0], self.after[door][1] - c[1]) > 6.0:
                continue
            on = door
            for k in range(-DOOR_RUN, DOOR_RUN + 1):
                i = (door + k) % n
                rel = (self.after[i][0] - c[0], self.after[i][1] - c[1])
                depth = rel[0] * inward[0] + rel[1] * inward[1]
                if depth > 0.05:
                    continue
                w = _ss((depth + DOOR_REACH) / (DOOR_REACH - DOOR_FULL)) * _ss((DOOR_RUN - abs(k)) / (DOOR_RUN / 2))
                s = (rel[0] * along[0] + rel[1] * along[1]) * w
                self.after[i] = (self.after[i][0] - along[0] * s, self.after[i][1] - along[1] * s)
                self.wa[i] = self.wa[i] + (d["width"] - 2 * FENCE_GAP - self.wa[i]) * w
                o = (self.after[on][0] - c[0], self.after[on][1] - c[1])
                if abs(depth) < abs(o[0] * inward[0] + o[1] * inward[1]):
                    on = i
            self.after[on] = c
            facade.append(on)
            names.append(d["name"])
        i = 0
        while i < n:
            if not self._moved(i):
                i += 1
                continue
            a = i
            while i + 1 < n and self._moved(i + 1):
                i += 1
            self.pieces.append((a if a in facade else a - 1, i if i in facade else i + 1))
            i += 1
        # the door of each piece: the one whose facade point is an end of the piece
        self.names, self.facade = [], []
        for first, last in self.pieces:
            k = next((j for j, f in enumerate(facade) if f in (first, last)), None)
            self.names.append(names[k] if k is not None else "")
            self.facade.append(facade[k] if k is not None else first)

    def _moved(self, i):
        return self.after[i] != self.before[i] or abs(self.wa[i] - self.wb[i]) > 1e-6

    def road(self, k):
        """Left and right edge of door road k (NovaHallInsert.MakeDoorRoad)."""
        first, last = self.pieces[k]
        left, right = [], []
        for i in range(first, last + 1):
            c = self.after[i]
            a, b = self.after[max(i - 1, first)], self.after[min(i + 1, last)]
            d = math.hypot(b[0] - a[0], b[1] - a[1]) or 1.0
            fx, fy = (b[0] - a[0]) / d, (b[1] - a[1]) / d
            h = self.wa[i] / 2
            lp, rp = (c[0] - fy * h, c[1] + fx * h), (c[0] + fy * h, c[1] - fx * h)
            if left and (lp[0] - left[-1][0]) * fx + (lp[1] - left[-1][1]) * fy <= 0:
                lp = left[-1]
            if right and (rp[0] - right[-1][0]) * fx + (rp[1] - right[-1][1]) * fy <= 0:
                rp = right[-1]
            left.append(lp)
            right.append(rp)
        return left, right

    def cut_points(self, k, pad=0.5):
        """Points that span what Unity cuts round door piece k: both lines of the piece, offset by the cut's reach
        plus `pad`."""
        first, last = self.pieces[k]
        out = []
        for line, width in ((self.before, self.wb), (self.after, self.wa)):
            for i in range(first, last + 1):
                a, b = line[max(i - 1, first)], line[min(i + 1, last)]
                d = math.hypot(b[0] - a[0], b[1] - a[1]) or 1.0
                fx, fy = (b[0] - a[0]) / d, (b[1] - a[1]) / d
                h = width[i] / 2 + 1.5 + pad
                c = line[i]
                ends = (-0.25, 0.0) if i == first else (0.0, 0.25) if i == last else (0.0,)
                for e in ends:
                    cx, cy = c[0] + fx * e * 2.0, c[1] + fy * e * 2.0
                    out += [(cx - fy * h, cy + fx * h), (cx + fy * h, cy - fx * h)]
        return out


def hull(points):
    """Convex hull (counter-clockwise)."""
    pts = sorted(set((round(x, 4), round(y, 4)) for x, y in points))
    if len(pts) < 3:
        return pts

    def half(seq):
        out = []
        for p in seq:
            while len(out) >= 2 and (out[-1][0] - out[-2][0]) * (p[1] - out[-2][1]) - (out[-1][1] - out[-2][1]) * (p[0] - out[-2][0]) <= 0:
                out.pop()
            out.append(p)
        return out[:-1]
    return half(pts) + half(pts[::-1])


APRON_DOORS = ("D1_main_entrance", "D5_out_north_east")     # doors on open ground (D3 lies on the terrace's roof deck,
                                                            # D4 on the plaza deck: nothing is bare there)


def door_aprons(route):
    """[(door name, ring)]: the ground round each open-air hall door that Unity's cut can lay bare: the hull of the
    cut's reach round the door piece and of the old road (route.json) on its last metres before the facade.
    route: route.json's points."""
    d = Doors()
    out = []
    if not d.ok:
        return out
    for k, name in enumerate(d.names):
        if name not in APRON_DOORS or k >= len(d.pieces):
            continue
        pts = d.cut_points(k)
        door = d.after[d.facade[k]]
        near = [i for i, p in enumerate(route) if math.hypot(p["x"] - door[0], p["y"] - door[1]) < 9.0]
        for i in near:                                      # the old road where it runs on into the building
            a, b = route[i - 1], route[(i + 1) % len(route)]
            dd = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
            nx, ny = -(b["y"] - a["y"]) / dd, (b["x"] - a["x"]) / dd
            pts += [(route[i]["x"] + nx * s, route[i]["y"] + ny * s) for s in (-5.2, 5.2)]
        out.append((name, hull(pts)))
    return out


# ---------------------------------------------------------------------------------------------------------------
# The race road's look and the ground colour map
# ---------------------------------------------------------------------------------------------------------------
LOOK_I = (583, 639)        # route points whose road shows the north side's real ground (env_track.ROAD_LOOK)
ROAD_ON_LAWN = False       # Luc, 12 Nov 2026 (after driving V18j): the race road stays paved where it crosses the street's
                           # lawn ("grass in the middle of the road"); True = the road reads as the lawn it runs over


def on_lawn(x, y):
    """True where the real ground is the street's lawn."""
    return inside(x, y, LAWN)


def road_on_lawn(x, y):
    """True where the race road reads as lawn (only with ROAD_ON_LAWN; otherwise the road is setts everywhere)."""
    return ROAD_ON_LAWN and on_lawn(x, y)


def lawn_road_quads(route):
    """The road quads (with their shoulders, 4.95 m either side) whose middle lies on the real lawn: the ground
    colour map is lawn under them, so the road, which takes the terrain's own material there, shows grass over its
    whole width."""
    n = len(route)
    out = []
    for i in range(LOOK_I[0], LOOK_I[1] + 1):
        a, b = route[i], route[(i + 1) % n]
        if not road_on_lawn((a["x"] + b["x"]) / 2, (a["y"] + b["y"]) / 2):
            continue
        d = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
        tx, ty = (b["x"] - a["x"]) / d, (b["y"] - a["y"]) / d
        ends = []
        for p, q, r, along in ((a, route[i - 1], b, -0.6), (b, a, route[(i + 2) % n], 0.6)):       # 0.6 m over both ends:
            dd = math.hypot(r["x"] - q["x"], r["y"] - q["y"]) or 1.0                                 # the map is 0.2 m per pixel
            nx, ny = -(r["y"] - q["y"]) / dd, (r["x"] - q["x"]) / dd
            ends.append(((p["x"] + tx * along + nx * 4.95, p["y"] + ty * along + ny * 4.95),
                         (p["x"] + tx * along - nx * 4.95, p["y"] + ty * along - ny * 4.95)))
        out.append([ends[0][0], ends[1][0], ends[1][1], ends[0][1]])
    return out


# ---------------------------------------------------------------------------------------------------------------
# V20d (Luc's playtest 13 Nov 2026, s20: "on Google Maps the houses on the right side are much further away"): what
# lies north of the forecourt, measured on the DGT orthophoto (ortho_area, 3 px per metre, aligned with track.json),
# the Google satellite mosaic (8 px per metre) and Street View N01 / N02 (Jun 2019). The OSM footprints there are
# right (the villas' roofs and pools line up with the orthophoto); what was wrong is what stood on them: generic
# 10.5 m flat-roofed blocks in pink and cream, in the open. Real: two-storey villas and terraced pairs with orange
# hip roofs, ochre or white render, each behind a front garden with a pool, the gardens closed to the dry field by a
# tall hedge, so from the forecourt only roofs and upper floors show above it, 45-90 m away.
# ---------------------------------------------------------------------------------------------------------------
HOUSE_STOREY = 3.0          # m per storey of the villas
HOUSE_EAVE = 0.45           # m: the roof's overhang
HOUSE_PITCH = 26.0          # degrees: Portuguese tile roofs
# per OSM id: storeys, the parts as four corners (counter-clockwise, from the OSM vertices; small jogs of the OSM
# outline are left out), and the wall colour of each part's units along its long side ("white" / "ochre"; a terraced
# pair is two units under one roof); "chimney": the part that gets a chimney
HOUSES = {
    "w228204169": dict(storeys=2, parts=[[(108.9, 230.4), (119.0, 228.7), (120.4, 237.6), (110.3, 239.3)],
                                         [(106.6, 234.4), (109.5, 233.9), (110.0, 236.8), (107.0, 237.2)]],
                       walls=[["white"], ["white"]]),
    "w228204163": dict(storeys=2, parts=[[(129.8, 227.2), (156.0, 222.9), (157.5, 231.4), (131.3, 235.8)]],
                       walls=[["ochre", "ochre"]]),
    "w228204161": dict(storeys=2, parts=[[(160.5, 221.9), (184.9, 218.6), (186.1, 227.3), (161.7, 230.6)]],
                       walls=[["white", "ochre"]]),
    "w228204164": dict(storeys=2, parts=[[(190.3, 217.8), (200.3, 216.3), (201.6, 224.9), (191.6, 226.4)],
                                         [(200.6, 219.5), (203.3, 219.1), (203.7, 222.0), (201.0, 222.4)]],
                       walls=[["ochre"], ["ochre"]]),
    "w228204167": dict(storeys=2, parts=[[(223.8, 223.1), (242.6, 219.3), (244.1, 224.7), (224.8, 228.6)],
                                         [(228.5, 199.0), (234.1, 196.5), (241.2, 217.6), (235.5, 220.0)],
                                         [(236.6, 203.6), (243.2, 201.6), (245.3, 207.2), (238.7, 210.3)]],
                       walls=[["white"], ["white"], ["white"]]),
}
# the gardens' tall hedge towards the dry field (orthophoto: the green plots end on a straight line), the hedges
# between the plots (at these x on that line, square to it, up to the houses) and the pools (orthophoto, blue pixels)
GARDEN_LINE = [(106.0, 222.2), (201.0, 202.6)]
GARDEN_DIVIDERS = [129.5, 141.0, 157.5, 170.0, 185.5]
HEDGE_DEPTH, HEDGE_H, DIVIDER_H = 0.9, 2.1, 1.7
POOLS = [(117.8, 220.2, 122.4, 224.0), (123.8, 217.5, 128.8, 224.2), (147.2, 214.5, 151.5, 221.8),
         (162.8, 212.6, 166.4, 219.4), (177.5, 209.8, 181.2, 217.2), (191.2, 206.2, 196.2, 214.5)]
GARDEN_TREES = [(113.0, 226.0, "Veg_Olive", 0.8), (139.0, 219.0, "Veg_Olive_B", 0.7), (171.5, 214.0, "Veg_Olive", 0.75),
                (125.0, 241.5, "Veg_UmbrellaPine_B", 0.85), (166.0, 238.0, "Veg_UmbrellaPine", 0.8),
                (198.0, 233.0, "Veg_UmbrellaPine_B", 0.8)]
FIELD_MARK = [(109.0, 202.4), (121.3, 202.6), (123.0, 210.6), (110.6, 214.6)]     # the white-lined plot in the dry field


# the apartment blocks along Rua da Holanda north of the world's edge (OSM; env_backdrop drew them as plain 3-level
# boxes): as the two blocks at the forecourt (env_campus.build_housing: white render, balconies, terracotta roof
# edge, roof storey), storeys from Street View N01_h000 / N01_h315 / N02_h000 (west side 5, east side 4)
FAR_HOUSING = {"w179614678": 5, "w179614659": 5, "w179614657": 5, "w179614766": 4, "w179614764": 4}


def garden_y(x):
    """y of the gardens' hedge line at x."""
    (ax, ay), (bx, by) = GARDEN_LINE
    return ay + (by - ay) * (x - ax) / (bx - ax)


# Rua da Holanda north of the forecourt (Google mosaic and sat20_x085_y185: asphalt x 75.5 .. 81.0, straight north
# to the orthophoto's edge at y 222; further north the street turns like OSM's line, 0.1645 m east per metre, along
# the next apartment blocks). West of it a row of perpendicular parking bays (cars nose-in towards the apartment
# block), a narrow sidewalk at the block; east of it a sett strip with one car parked along the kerb, the calcada
# path north-north-east beside the dry field, and its low white seat wall (N01_h000 / h045 / h090: 0.45 m high,
# 0.5 m wide; it turns east along the field's south edge)
HOLANDA_KERBS = (75.5, 81.0)
HOLANDA_BEND, HOLANDA_SLOPE = 218.0, 0.1645
HOLANDA_ROW = (200.6, 241.0)             # from the forecourt sheet's tab to beyond the world's edge (y 240)
BAY_DEPTH, BAY_W = 5.5, 2.5              # the bays west of the asphalt
WALK_WEST = 3.4                          # the sidewalk between the bays and the apartment block (runs under its wall)
STRIP_EAST, PATH_W = 3.3, 5.0            # sett strip east of the asphalt, then the calcada path
SEAT_WALL_W, SEAT_WALL_H = 0.5, 0.45
SEAT_WALL_SOUTH_Y = 194.55               # its east-west leg along the field's south edge (EAST_PATH's north kerb)
SEAT_WALL_EAST_X = 126.0
BAY_SHARE = 0.8                          # share of the bays with a car (N01_h315: nearly all taken)
PARKING_SIGN = (86.9, 198.6)             # the blue P sign at the corner (N01_h000), facing the forecourt
STONE_BOLLARD = (86.3, 197.4)            # the rough stone block bollard beside it
SLIM_BOLLARDS = [(82.0, 193.4), (82.0, 195.4), (82.0, 197.4), (82.0, 199.4)]   # slim steel bollards along the kerb
SIGN_SLAB = (90.9, 196.3)                # the black sign slab at the field's corner (blank: the real one names a firm)
SCOOTERS = [(88.4, 199.6, 205.0), (88.5, 206.5, 200.0)]                   # x, y, bearing of the nose
LAY_BY_CAR = (82.7, 204.0)               # the one car parked along the east kerb (satellite)
STREET_LAMPS = [(83.6, 205.0), (85.4, 226.0)]
CORNER_PINE = (70.5, 195.8)              # the big umbrella pine at the apartment block's corner (satellite crown r 4.5 m)


def holanda_dx(y):
    """Metres the street lies east of its line at the forecourt, at y (0 up to HOLANDA_BEND)."""
    return max(0.0, y - HOLANDA_BEND) * HOLANDA_SLOPE


def holanda_band(x0, x1, ya=None, yb=None, step=4.0):
    """Ring of the band between x0 and x1 (at the forecourt's line, both moved east with the street) from ya to yb."""
    ya, yb = ya if ya is not None else HOLANDA_ROW[0], yb if yb is not None else HOLANDA_ROW[1]
    ys = [ya + (yb - ya) * k / max(1, int((yb - ya) / step)) for k in range(max(1, int((yb - ya) / step)) + 1)]
    if HOLANDA_BEND not in ys and ya < HOLANDA_BEND < yb:
        ys = sorted(ys + [HOLANDA_BEND])
    return [(x0 + holanda_dx(y), y) for y in ys] + [(x1 + holanda_dx(y), y) for y in ys[::-1]]


def holanda_bays():
    """[(x, y of the car's middle, unit vector of its nose)] of the bays west of the asphalt."""
    out = []
    y = HOLANDA_ROW[0] + 0.6 + BAY_W / 2
    d = math.hypot(HOLANDA_SLOPE, 1.0)
    while y < HOLANDA_ROW[1] - BAY_W / 2:
        along = (HOLANDA_SLOPE / d, 1.0 / d) if y > HOLANDA_BEND else (0.0, 1.0)
        nose = (-along[1], along[0])                                    # west, square to the street
        x = HOLANDA_KERBS[0] - BAY_DEPTH / 2 + 0.25 + holanda_dx(y)
        out.append((x, y, nose))
        y += BAY_W
    return out


def ground_paint(paint, setts, lawn):
    """Ground colours and lawn mask under the north side (called from entrance_plan.avenue_side with
    campus_plan.paint and its colours; make_groundmap draws campus_plan.GROUND_PAINT in order). Paved under the
    sheet (no grass tufts through it), lawn in the street's triangle and under the road quads that read as lawn.
    Not street-plan layers: those would be cut into the streets' paving mesh and shift its triangles."""
    route = json.load(open(os.path.join(HERE, "route.json")))["points"]
    paint("north side paving (V18j)", list(SHEET), setts)
    paint("north street lawn (V18j)", [p for p in LAWN if p[0] > -9.9] + [(-9.9, 170.0), (-9.9, 162.3)], lawn, 255)
    for k, quad in enumerate(lawn_road_quads(route)):
        paint(f"lawn under the race road {k} (V18j)", quad, lawn, 255)
    # V20d: the dry field north of the curved path, the gardens behind their hedge, Rua da Holanda with its bays,
    # sidewalks and the path, the white-lined plot in the field
    meadow, asphalt, calcada, kerb = (178, 172, 122), (85, 88, 94), (226, 221, 208), (207, 201, 188)
    paint("dry field north of the curved path (V20d)", [(89.8, 194.9), (126.0, 194.9), (190.0, 196.5), (206.0, 200.0),
                                                        (201.0, 202.6), (106.0, 222.2), (96.0, 241.0), (89.8, 241.0)],
          meadow, 235)
    paint("gardens behind the hedge (V20d)", [(106.0, 222.2), (201.0, 202.6), (204.0, 214.0), (206.0, 241.0),
                                              (100.0, 241.0)], lawn, 255)
    k0, k1 = HOLANDA_KERBS
    x_bays, x_walk = k0 - BAY_DEPTH, k0 - BAY_DEPTH - WALK_WEST
    paint("R. da Holanda sidewalk at the block (V20d)", holanda_band(x_walk, x_bays), setts)
    paint("R. da Holanda bays (V20d)", holanda_band(x_bays, k0), (96, 98, 102))
    paint("R. da Holanda (V20d)", holanda_band(k0, k1), asphalt)
    paint("R. da Holanda east strip (V20d)", holanda_band(k1, k1 + STRIP_EAST, 197.6), setts)
    paint("path beside the field (V20d)", holanda_band(k1 + STRIP_EAST, k1 + STRIP_EAST + PATH_W, 194.3), calcada)
    for name, a, b in (("west kerb", k0 - 0.12, k0 + 0.03), ("east kerb", k1 - 0.03, k1 + 0.12),
                       ("bay kerb", x_bays - 0.12, x_bays + 0.02)):
        paint(f"R. da Holanda {name} (V20d)", holanda_band(a, b), kerb)
    for a, b in zip(FIELD_MARK, FIELD_MARK[1:] + FIELD_MARK[:1]):
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        nx, ny = -(b[1] - a[1]) / d * 0.22, (b[0] - a[0]) / d * 0.22
        paint("white line in the field (V20d)", [(a[0] - nx, a[1] - ny), (b[0] - nx, b[1] - ny), (b[0] + nx, b[1] + ny),
                                                 (a[0] + nx, a[1] + ny)], (222, 219, 206), 0)
