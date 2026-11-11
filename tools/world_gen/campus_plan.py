"""Campus plan (V10c2, 2026-11-11): the Nova SBE buildings west of and on the spine as plain data.

Read by env_campus.py (geometry, inside Blender) and make_groundmap.py (ground colours). No Blender imports.
Source: the campus survey V10h (atlas report), i.e. the DGT orthophoto (reference for every footprint, +-1 m), the
Google satellite tiles (+-2 m) and the team's walk photos. Every shape here is a hand-traced vector outline in local
metres (x east, y north, origin lat 38.6780 / lon -9.3265); no image data is copied.

Levels (official floor numbering): Floor 0 = 11.2 m (lower plaza, courtyards), Floor 1 = 14.5 m (upper plaza, atrium,
Gallery, CTT roof terrace), Floor 2 = 18.3 m, Floor 3 = 22.0 m.
"""
import math

import campus_west_plan as WEST      # V16c: plain data of the campus west package (pool, pad, paths, berm)

F0, F1, F2, F3 = 11.2, 14.5, 18.3, 22.0


def frame(origin, u):
    """Local frame: returns f(a, b) -> world (x, y) with a along the unit vector u and b along its left normal."""
    def to_world(a, b):
        return (origin[0] + u[0] * a - u[1] * b, origin[1] + u[1] * a + u[0] * b)
    return to_world


def unframe(origin, u):
    def to_local(x, y):
        dx, dy = x - origin[0], y - origin[1]
        return (dx * u[0] + dy * u[1], -dx * u[1] + dy * u[0])
    return to_local


def inside(x, y, ring):
    hit = False
    for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            hit = not hit
    return hit


def seg_distance(x, y, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    ll = dx * dx + dy * dy
    t = 0.0 if ll == 0 else min(1.0, max(0.0, ((x - ax) * dx + (y - ay) * dy) / ll))
    return math.hypot(x - ax - dx * t, y - ay - dy * t)


def ring_distance(x, y, ring):
    return min(seg_distance(x, y, a[0], a[1], b[0], b[1]) for a, b in zip(ring, ring[1:] + ring[:1]))


# ----------------------------------------------------------------------------------------------------------------
# Main building (w665606788): the Gallery spine (I) and the Hovione Atrium block (A).
# Frame: origin = south-west corner of the spine, u across the spine (east-north-east), v along it (north-north-west).
# In this frame the OSM outline is the spine [0, 30] x [0, 142] plus the atrium block [0.9, 69.3] x [142, 210].
# ----------------------------------------------------------------------------------------------------------------
MAIN_ID = "w665606788"
MAIN_O, MAIN_U = (78.7, -36.0), (0.9308, 0.3656)
MAIN = frame(MAIN_O, MAIN_U)
MAIN_UV = unframe(MAIN_O, MAIN_U)
MAIN_ROOF_Z = 24.5            # flat grey roof (Gallery eave about 24 m, atrium about 25 m)
MAIN_SOFFIT_Z = 21.6          # underside of the roof slab = head of the 7 m glass walls (hall ceiling is 21.5)
GALLERY_DEEP = 24.0           # u of the Gallery's east glass wall under the deep roof overhang (roof edge u = 30)
GALLERY_SHALLOW = 28.2        # same, where the drive-through hall behind reaches u = 27.7 (v 106..140)
GALLERY_BREAK = (104.0, 106.0)   # v of the white cross pier between the two
ENTRANCE_V = (173.0, 198.0)   # v range of the glazed west entrance front (set back 2 m under the roof edge)
ENTRANCE_U = 2.9
# V15b (survey V15a, N_p04/N_p03): the colonnade of the north exit, a ground-floor recess in the atrium's east face
# under one white storey: u = its back wall (6 m behind the facade line u = 69.3), v = from Student Central's
# north face to the blank white panel at the north end; clear height 4.0 m
COLONNADE_U = 63.3
COLONNADE_V = (174.5, 205.0)
COLONNADE_CLEAR = 4.0
# roof outline (= OSM, straightened) and the body below the soffit (east glass wall and west entrance front set back)
MAIN_ROOF_UV = [(0, 0), (30, 0), (30, 142), (34.5, 142), (34.0, 139.4), (41.1, 138.4), (41.8, 142), (69.3, 142),
                (69.3, 210), (0.9, 210), (0.9, 142.2), (0.1, 113.6), (0, 97.1)]
MAIN_BODY_UV = [(0, 0), (30, 0), (30, 2), (GALLERY_DEEP, 2), (GALLERY_DEEP, GALLERY_BREAK[0]), (30, GALLERY_BREAK[0]),
                (30, GALLERY_BREAK[1]), (GALLERY_SHALLOW, GALLERY_BREAK[1]), (GALLERY_SHALLOW, 140), (30, 140),
                (30, 142), (34.5, 142), (34.0, 139.4), (41.1, 138.4), (41.8, 142), (69.3, 142),
                (69.3, COLONNADE_V[0]), (COLONNADE_U, COLONNADE_V[0]), (COLONNADE_U, COLONNADE_V[1]),
                (69.3, COLONNADE_V[1]), (69.3, 210),            # V15b: the north exit colonnade is open below
                (0.9, 210), (0.9, ENTRANCE_V[1]), (ENTRANCE_U, ENTRANCE_V[1]), (ENTRANCE_U, ENTRANCE_V[0]),
                (0.9, ENTRANCE_V[0]), (0.9, 142.2), (0.1, 113.6), (0, 97.1)]
MAIN_ROOF = [MAIN(u, v) for u, v in MAIN_ROOF_UV]
MAIN_BODY = [MAIN(u, v) for u, v in MAIN_BODY_UV]
GLASS_FIELD_UV = (32.5, 161.0, 52.0, 195.0)   # the one real glass roof (about 20 x 34 m, Google tile +-2 m)
SOLAR_ROWS_UV = [(6.5, 13.5), (15.0, 22.0)]   # two long rows of panels on the west half of the spine roof (u ranges)
SOLAR_RUNS_V = [(6.0, 48.0), (98.0, 200.0)]   # ... from y = -28 to 12 and from y = 58 to 155

# ----------------------------------------------------------------------------------------------------------------
# B  Cascais Academic Hall (w496301640). Frame: origin = west end of the OSM south edge, s east along the bar,
# t north across it (OSM envelope t = 0..31.9: Floor-1 body 2..29, roof storey 9..15 under a slab 7.8..16.2).
# ----------------------------------------------------------------------------------------------------------------
B_ID = "w496301640"
B_O, B_U = (-52.9, 62.6), (0.9338, 0.3579)
B = frame(B_O, B_U)
B_BODY_ST = (-1.0, 87.6, 2.0, 29.0)
B_STOREY_ST = (13.0, 79.0, 9.0, 15.0)
B_SLAB_ST = (11.5, 80.5, 7.8, 16.2)
B_HEAD = [(-79.6, 77.7), (-67.8, 59.7), (-57.3, 63.6), (-57.6, 72.5), (-61.2, 86.2), (-57.5, 88.5), (-61.4, 93.4),
          (-64.3, 98.1), (-68.2, 96.0), (-65.8, 90.9)]
B_LINK = [(-57.6, 72.5), (-50.5, 73.5), (-52.5, 86.5), (-61.2, 86.2)]          # dark brown roof field
B_HOOD = [(-57.9, 63.4), (-57.4, 60.3), (-52.4, 60.5), (-50.5, 73.5), (-57.6, 72.5)]   # portal hood strip
HEAD_TOP = 20.0               # heads of B and D
WING_TOP = 19.0               # top of the white band over the Floor-1 ribbon
WING_STOREY_TOP = 21.6        # roof storey glazing; its slab reaches 22.0

# ----------------------------------------------------------------------------------------------------------------
# D  Santander Academic Hall (w1174472383): B mirrored. Frame: origin = west end of the OSM north edge, s east,
# t SOUTH across it (envelope 0..31.6: Floor-1 body 0.5..26, roof storey 12.7..20.3 under a slab 11.5..21.5,
# a low planted terrace over Floor 0 from 26 to 31.6).
# ----------------------------------------------------------------------------------------------------------------
D_ID = "w1174472383"
D_O, D_U = (-33.5, 12.0), (0.9326, 0.3611)
_D = frame(D_O, D_U)


def D(s, t):
    return _D(s, -t)


D_BODY_ST = (-2.0, 87.4, 0.5, 26.0)
D_STOREY_ST = (21.5, 82.0, 12.7, 20.3)
D_SLAB_ST = (20.0, 83.5, 11.5, 21.5)
D_PODIUM_ST = (3.5, 87.4, 26.0, 31.6)
D_HEAD = [(-33.5, 12.0), (-42.3, 18.7), (-44.8, 15.9), (-37.5, 7.3), (-48.1, -10.1), (-33.3, -22.9), (-23.3, -16.6),
          (-21.5, -20.8), (-18.2, -20.0), (-18.8, -16.3)]
# the dark glazed base under the cantilevered head (arm tip 6 m back, west and south faces 3 m in)
D_HEAD_BASE = [(-33.5, 12.0), (-38.0, 14.5), (-40.5, 11.7), (-35.5, 6.0), (-44.5, -9.2), (-33.4, -19.4), (-23.0, -13.5),
               (-18.8, -16.3)]
D_LINK = [(-29.5, 3.0), (-23.5, 6.5), (-17.5, -4.0), (-23.5, -9.5)]             # dark brown roof field
D_POOL_V10 = [(-54.0, 14.0), (-50.6, -21.5), (-35.4, -31.5), (-1.2, -15.0), (-15.3, 23.0)]   # basin before V16c
D_POOL = WEST.D_POOL          # V16c (survey Appendix C 2.2): five-sided basin with the arm along D's south facade
D_POOL_Z = WEST.D_POOL_Z      # water level (the lawn around it lies at 9.0-9.9 m)

# ----------------------------------------------------------------------------------------------------------------
# C  CTT Student Hall (w496301638): the roof terrace is the race deck and stays as it is. Beside it the two sunken
# courtyards at Floor 0 and, at the west end, the cheeks of the real stair (outside the jump corridor).
# ----------------------------------------------------------------------------------------------------------------
COURT_Z = F0
# V10 integration: both west ends stop 5 m further east than in V10c2 (north wall x = -4.8 -> 0.3, south court's west
# edge moved 5 m along the bar). V10i's landing road starts right under the terrace lip (route point 82), so the lawn
# corridor (26 m to each side) now begins there and covers the 5 m cells west of x = 0 (north) and x = 5 (south).
COURT_N = [(0.3, 65.3), (8.7, 65.3), (37.3, 69.7), (28.7, 93.3), (0.3, 82.45)]
COURT_S = [(5.45, 48.06), (11.97, 30.39), (47.2, 43.9), (43.5, 53.9)]

# ----------------------------------------------------------------------------------------------------------------
# A  west entrance: the shallow pool in front of the white end wall
# ----------------------------------------------------------------------------------------------------------------
A_POOL = [(-3.34, 144.8), (7.01, 148.93), (2.75, 159.79), (-7.59, 155.86)]    # = the OSM water area (survey: right place)

# ----------------------------------------------------------------------------------------------------------------
# F  Grand Auditorium (w496301621): faceted white monolith. Roof corner heights (m) in the comments of env_campus.
# G  Horizon Hall / Westmont (w496301627): podium at Floor 1 on round columns, cream box, glazed block, flying roof.
# ----------------------------------------------------------------------------------------------------------------
F_ID, G_ID, E_ID, SC_ID = "w496301621", "w496301627", "w496301634", "w1174472373"
G_CREAM = [(113.6, 3.2), (126.2, 8.1), (119.6, 24.9), (107.0, 20.0)]            # the cream box (2 storeys on Floor 1)
G_ROOF = [(132.3, 1.3), (150.0, 8.2), (140.1, 33.4), (122.4, 26.4)]             # the flying roof slab (roof square)
G_BLOCK = [(135.8, 10.2), (147.4, 14.8), (140.1, 33.4), (128.5, 28.8)]           # glazed block, set back under it
G_BALCONY = [(134.3, 5.2), (149.5, 11.1), (141.3, 34.4), (126.1, 28.5)]          # Floor-2 balcony band around it
G_COLUMNS = [(133.0, 2.9), (138.5, 4.8)]                                         # the two tall columns
G_STEPS = [(137.4, -22.6), (149.6, -15.2), (149.6, -7.4), (142.4, -7.0)]        # terracotta seat steps south of G
# lower plaza (EDP Plaza, 11.2 m) east of the race line: the terrain is levelled here so G stands on its columns
LOWER_PLAZA = [(111.0, -30.0), (122.3, -22.3), (130.5, -20.5), (133.0, -27.5), (137.4, -22.6), (150.0, -14.0),
               (150.0, -6.5), (149.0, 9.0), (133.0, 0.5), (127.5, 12.0), (114.0, 11.5), (114.1, 1.4),
               (112.5, -6.0), (111.0, -12.0)]
# (V10 integration: the outline reaches 4 to 6 m behind G's set-back Floor-0 front, so the 5 m nodes just behind the
# glass are level too; before, the ground rose to them and stood as a mound in front of the glass)
LOWER_PLAZA_Z = F0 - 0.05


# ----------------------------------------------------------------------------------------------------------------
# Ground colours (make_groundmap.py): drawn in this order over the OSM areas and paths, under the street section.
# (name, ring, colour RGB, lawn 0..255 for the ground mask's red channel)
# ----------------------------------------------------------------------------------------------------------------
OCHRE = (222, 194, 146)       # plaza and courtyard paving (survey: sunny #e9c488; a step paler for our warm light)
STONE = (208, 203, 192)       # pale grey stone slabs of the entrance plaza
CONCRETE = (196, 192, 184)    # brushed concrete (pad at the foot of the terrace stair)
LAWN = (143, 179, 122)
CALCADA = (226, 221, 208)
GROUND_PAINT = []


def paint(name, ring, colour, lawn=0):
    GROUND_PAINT.append({"name": name, "ring": ring, "colour": colour, "lawn": lawn})


BEIGE = (214, 205, 182)       # the stepped amphitheatre between D and E
paint("entrance plaza", [(-90.0, 102.0), (-64.0, 98.0), (16.0, 128.5), (7.2, 148.6), (2.8, 159.8), (1.5, 163.5),
                         (-30.0, 159.0), (-38.0, 147.5), (-90.0, 147.5)], STONE)
# (V16c: the pad at the terrace stair moved north of the landing road with the stair: WEST.add_paint below)
paint("path along B", [B(-2.0, -6.2), B(50.0, -6.2), B(50.0, -3.6), B(-2.0, -3.6)], CALCADA)
paint("amphitheatre", [(21.0, -4.5), (57.5, 9.5), (66.0, -9.5), (47.0, -20.0), (42.3, -20.7), (34.6, -22.7),
                       (28.0, -19.0)], BEIGE)
paint("terrace of E", [(13.0, -47.0), (23.9, -39.1), (29.3, -46.2), (27.0, -52.0), (18.5, -55.4)], OCHRE)
paint("north courtyard", COURT_N, WEST.GRAVEL)       # V16c: ochre stabilised gravel (survey #a2896b)
paint("south courtyard", COURT_S, WEST.GRAVEL)
paint("lower plaza", [(96.0, -29.3), (106.6, -25.4), (104.5, -18.0), (110.5, -8.5), (114.1, 1.4), (123.1, 4.1),
                      (125.4, -2.0), (128.7, -10.6), (142.4, -7.0), (137.4, -22.6), (133.0, -27.5), (130.5, -20.5),
                      (122.3, -22.3), (122.6, -32.7), (114.0, -36.5), (104.0, -38.5)], OCHRE)
# V10 integration: the ground under the Gallery's arcade (beside the lower plaza and the stairs' west void) and in
# Horizon Hall's undercroft is paving too (it was lawn green behind the columns)
paint("arcade under the Gallery deck", [MAIN(23.4, 1.0), MAIN(30.6, 1.0), MAIN(30.6, 34.0), MAIN(23.4, 34.0)], OCHRE)
paint("undercroft of Horizon Hall", [(113.6, 0.9), (123.1, 4.1), (125.4, -2.0), (128.7, -10.6), (151.6, 2.6),
                                     (147.4, 5.8), (131.3, -3.5), (126.1, 10.1), (112.4, 6.2)], OCHRE)


# ----------------------------------------------------------------------------------------------------------------
# V15b (2026-11-11, photosphere survey V15a): west entrance lettering and space frame, Milestone J, the north loop.
# ----------------------------------------------------------------------------------------------------------------
J_ID = "w622493089"                     # Milestone residence: 3 storeys on a plinth, loggia checkerboard to the south
J_PLINTH, J_STOREY, J_STOREYS, J_PARAPET = 0.6, 3.3, 3, 0.45
J_LOGGIA_DEPTH = 1.3                    # m, the dark back wall of the loggias behind the white frame (survey 1.2-1.5)
J_UNIT = 3.2                            # m, width of one loggia / panel
HOUSING_IDS = ("w179614712", "w179614681")      # the two apartment blocks north-west of the drop-off plaza (N_p01)
HOUSING_STOREYS, HOUSING_STOREY = 5, 3.0
# Student Central: dark angular pavilion (N_p03_h190); own outline inside the OSM footprint, its north-west part
# pulled back from the race line, roof corners at different heights (m above Floor 1)
SC_RING = [(86.5, 133.8), (100.5, 135.9), (98.8, 146.9), (93.0, 150.5), (91.5, 157.0), (84.5, 153.5), (80.68, 148.5)]
SC_TOPS = [5.4, 6.0, 7.6, 8.3, 5.9, 8.0, 6.8]
# drop-off plaza: a flush ring of dark setts in pale granite setts. Real: 9-10 m across, centre (80, 177); the race
# line passes 1 m from the centre, so the ring is drawn 12 m across and only its arcs outside the road shoulder exist
DROPOFF_C, DROPOFF_R, DROPOFF_BAND = (80.3, 177.0), 6.0, 0.45
# bin enclosure: real about (60..70, 190..196), 3.5 m from route point 600; here against the housing block's south
# wall (centre, length along that wall, depth, height)
BIN_C, BIN_LEN, BIN_DEPTH, BIN_H = (56.5, 198.75), 9.0, 1.4, 2.3
BIN_DIR = (0.9893, -0.1457)             # along the south face of w179614712
SETTS = (204, 196, 172)                 # pale granite setts of the forecourt (survey #c5bda4, a step paler)
BUFF = (196, 178, 140)                  # buff concrete floor of the colonnade (survey #877549 in deep shade)
paint("drop-off plaza", [(78.1, 155.0), (80.0, 150.0), (93.0, 150.0), (97.5, 160.0), (98.5, 178.0), (96.0, 196.0),
                         (86.0, 200.0), (72.0, 199.6), (65.6, 198.6), (43.2, 201.9), (36.0, 196.0), (27.0, 186.0),
                         (30.0, 171.0), (66.4, 184.8)], SETTS)
paint("colonnade floor", [MAIN(COLONNADE_U - 0.3, COLONNADE_V[0] - 0.3), MAIN(69.6, COLONNADE_V[0] - 0.3),
                          MAIN(69.6, COLONNADE_V[1] + 0.3), MAIN(COLONNADE_U - 0.3, COLONNADE_V[1] + 0.3)], BUFF)


def nova_letters():
    """The four letters on the end wall as flat polygons (nova_letters.json: glyph outlines of the HUD's display
    font, Unbounded Black, SIL OFL 1.1, flattened once with Blender; em = 1). Not traced from the real sign."""
    import json
    import os
    return json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "nova_letters.json")))["letters"]


LETTER_H = 1.6                          # m, cap height on the wall
LETTER_GAP = 0.22                       # m between letters
LETTER_OUT = 0.10                       # m, raised from the wall
LETTER_V, LETTER_Z = 203.7, 20.9        # centre of the word along the wall (v) and the letters' base line (m)


WEST.add_paint(paint, D_POOL_V10)       # V16c: lawn over the former basin, pale lawn path, pad, perimeter path
