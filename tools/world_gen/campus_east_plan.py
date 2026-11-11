"""Plan of the campus's east side (V16b, 2026-11-11): Horizon Hall G, the Grand Auditorium F's prow, hall E, the lower
plaza with its east edge. Sources: photosphere survey V15 (sections 3.5, 3.6, appendices E and F: panoramas E34, E35,
S01, S02, S03, S12, S18, S19), the DGT orthophoto. Local metres, +x east, +y north. No Blender imports.
Read by env_campus_east.py (geometry) and, through campus_plan.py, by make_groundmap.py (ground colours).
"""
import json
import math
import os

import campus_plan as C

F0, F1, F2, F3 = C.F0, C.F1, C.F2, C.F3
PROP_CLEAR = 6.6                    # m from the race centreline: nothing of this module that stands up comes closer


def _unit(a, b):
    d = math.hypot(b[0] - a[0], b[1] - a[1])
    return ((b[0] - a[0]) / d, (b[1] - a[1]) / d)


# ----------------------------------------------------------------------------------------------------------------
# G  Horizon Hall (survey 3.5, E 2.3, E 2.9, F 2.6). Rings C.G_CREAM, C.G_BLOCK, C.G_BALCONY, C.G_ROOF stay.
# ----------------------------------------------------------------------------------------------------------------
G_O = C.G_ROOF[0]                                   # south-west corner of the roof square (132.3, 1.3)
G_U = _unit(C.G_ROOF[0], C.G_ROOF[1])               # along G's south side (heading about 69)
G_V = (-G_U[1], G_U[0])


def G(a, b):
    """Point in G's own grid: a metres along the south side, b metres towards the library."""
    return (G_O[0] + G_U[0] * a + G_V[0] * b, G_O[1] + G_U[1] * a + G_V[1] * b)


G_FASCIA = (F1 - 0.6, F1 + 0.4)                     # the Floor-1 slab's white edge: 1.0 m (survey), soffit 2.7 m clear
G_COLUMN_R = 0.225                                  # round columns 0.45 m under the slab edge ...
G_COLUMN_STEP = 7.5                                 # ... every 7-8 m
G_BAND = 3.9                                        # m: dark glass band under the box, toward the upper plaza
G_BOX_TOP = F1 + 10.5                               # the blank box (survey 8.5 m on the 3.9 m band, +-1.5; the flying
#                                                     roof beside it is the highest line of G in every panorama)
G_ROOF_Z = (24.7, 25.2)                             # thin flying roof slab (0.5 m edge)
# the slab covers the glazed block and projects 6 m beyond its south side and, south of the box, 6.5 m to the west
G_ROOF_RING = [G(-6.5, -6.0), G(19.5, -6.0), G(19.5, 27.6), G(-0.5, 27.6), G(-0.5, 7.5), G(-6.5, 7.5)]
G_TALL_COLUMN = G(-6.0, -5.5)                       # the ONE slender column under its outer corner (survey (124..128,
G_TALL_COLUMN_R = 0.2                               # -7..-4) +-4): on the lower plaza, through the terrace slab
G_TOWER = [G(-5.6, -1.2), G(0.6, -1.2), G(0.6, 5.2), G(-5.6, 5.2)]     # glazed entrance / stair tower, plaza to roof

# ----------------------------------------------------------------------------------------------------------------
# F  Grand Auditorium: the north-west prow (survey 3.6, F 2.4). Roof points as in V10c2 (comments in env_campus_east).
# ----------------------------------------------------------------------------------------------------------------
F_NOSE = (124.5, -17.0)             # lowest point of the white mass: 3.5 m over the plaza, 5 m north of the roof corner
F_NOSE_H = 3.5
F_LINTEL_END = (131.0, -18.5)       # the lintel runs from the nose to here
F_WING_FOOT = (137.0, -22.0)        # east of the entrance the white wing's lower edge comes down to the ground here
F_SETBACK = 5.0                     # glazed vestibule behind the lintel (survey 4-6 m)
F_GLASS_H = 2.9
F_BERM = (1.2, 9.0)                 # lawn against the west wall: rise, width (survey: about 1 m over 10 m)

# ----------------------------------------------------------------------------------------------------------------
# East edge of the lower plaza (survey E 2.2, F 2.5): dark terracotta seat tiers, the grey stair, lawn and fence
# ----------------------------------------------------------------------------------------------------------------
TIER_Y = [-22.0, -18.5, -15.0, -11.5, -7.9]                 # stations of the curved foot line, south to north
TIER_X = [137.5, 137.95, 138.6, 139.35, 140.3]              # ... its x there (survey: (137.5, -22) to (140, -7))
TIERS, TIER_H, TIER_D = 6, 0.45, 1.5
TIER_TOP = F0 + TIERS * TIER_H                              # 13.9 m = the east lawn
TIER_COLOUR = "#9A6850"             # survey: #6a402b in July shade, #9a7462 in neutral March light
STAIR_X0, STAIR_Y = 141.0, (-7.6, -4.0)                     # grey concrete stair, 3.6 m between its cheeks, going east
STAIR_RISERS, STAIR_RISE, STAIR_TREAD = 19, 0.15, 0.36
CHEEK, CHEEK_H = 0.3, 1.0
LAWN_X = 151.4                                              # the lawn cap behind the tiers ends here in the terrain
FENCE = [(153.6, -27.0), (154.3, -21.0), (155.0, -15.0), (155.6, -9.0), (156.0, -3.0)]   # bar fence on the bank
EAST_RING = [(TIER_X[0] - 0.4, TIER_Y[0] - 0.5), (LAWN_X, TIER_Y[0] - 5.0), (LAWN_X, STAIR_Y[1] + 0.5),
             (STAIR_X0 - 0.3, STAIR_Y[1] + 0.5), (TIER_X[-1] - 0.4, TIER_Y[-1])]         # keep-out for the dressing


def tier_x(k, i):
    return TIER_X[i] + TIER_D * k


# ----------------------------------------------------------------------------------------------------------------
# Lower plaza paving and fixed objects (survey 3.6, F 2.2)
# ----------------------------------------------------------------------------------------------------------------
PLAZA_CONCRETE = "#CBC0A8"          # the panels (survey: beige-grey about #c4baa2; a step paler for our warm light)
PLAZA_LOW = (203, 192, 168)         # the same as the ground colour under them
PANEL = 4.5                         # m: in-situ concrete panels with saw-cut joints
RAMP_DIR = (math.sin(math.radians(55.0)), math.cos(math.radians(55.0)))      # the ramp's axis (survey: 55 / 235)
STAIR_DIR = (-0.488, 0.873)         # the stairs' axis (plaza_plan.STAIR_AXIS)
# the whole paved plane: from the ramp head to the stair foot, from the main building to the tiers and F's entrance,
# with the arcade under G's Floor-1 slab (the road, the stairs and the voids beside them are cut out of it)
PLAZA_RING = [(98.5, -29.2), (103.6, -27.2), (106.5, -26.0), (108.4, -17.0), (115.2, -8.2),
              (122.4, -4.2), (114.1, 1.4), (112.7, 5.8), (125.8, 9.6), (131.0, -4.1), (147.2, 5.3), (151.2, 2.35),
              (151.6, 2.6), (141.0, -3.5), (140.7, -7.9), (TIER_X[-1], TIER_Y[-1]), (TIER_X[3], TIER_Y[3]),
              (TIER_X[2], TIER_Y[2]), (TIER_X[1], TIER_Y[1]), (TIER_X[0], TIER_Y[0]), (137.0, -22.6), (133.5, -26.0),
              (131.6, -27.6), (122.0, -32.4), (109.6, -35.4), (104.5, -37.2)]
# north of this line the panels follow the stairs' axis, south of it the ramp's (two skew grids, survey F 2.2)
GRID_SPLIT = [(96.0, -22.5), (112.0, -19.5), (128.0, -14.0), (152.0, -9.0)]
# the terrain has a 0.8 m deep hole at the stairs' east foot (between the road bed and env_plaza's Floor-0 paving,
# there since V10i): the paving, which has no collider, leaves it open instead of hiding it
PLAZA_PIT = [(114.8, -11.0), (120.4, -11.0), (120.4, -5.2), (114.8, -8.3)]
PLAZA_LEVEL = [(113.0, -1.5), (120.4, -5.4), (120.4, -9.6), (124.8, -9.6), (126.0, -3.0), (124.0, 5.0),
               (113.0, 2.5)]       # level floor in front of G (see env_campus_east.build_paving)
GRATINGS = [(120.6, -20.5), (122.75, -20.05), (124.9, -19.6), (127.05, -19.15)]   # 1.0 x 0.4 m, in front of F's doors
GRATING = (1.0, 0.4)
TOTEM = (104.5, -28.0)              # black slab at the head of the ramp's north-west railing (shape only, blank)
TOTEM_SIZE = (0.8, 0.3, 3.1)        # broad faces to the north-east and south-west
LAMPS = [(120.7, -32.9), (111.6, -57.5)]     # anthracite square columns (survey (121, -32) +-1.5 and (116, -58) +-4:
LAMP_SIZE = (0.2, 6.5)                       # the second stands on the lawn, 2.5 m off F's west wall)

# ----------------------------------------------------------------------------------------------------------------
# E  Executive Hall (survey 3.3, 3.6, F b3, b4; C 2.3). Outline: OSM without the sliver along the ramp and without
# the south corner (53.9, -57.9), (60.8, -52.2): the orthophoto shows planting there and S18 sees the window box free.
# ----------------------------------------------------------------------------------------------------------------
E_RING = [(45.1, -25.4), (42.3, -20.7), (34.6, -22.7), (34.5, -27.9), (22.6, -38.5), (27.7, -45.7), (32.8, -44.0),
          (29.3, -54.8), (37.9, -60.0), (39.9, -61.1), (47.4, -54.5), (59.4, -49.9),
          (78.7, -36.0), (67.6, -8.4), (46.8, -20.3), (48.6, -23.3)]
E_SOUTH_CORNER = [(47.4, -54.5), (53.9, -57.9), (60.8, -52.2), (59.4, -49.9)]     # ... lawn instead
E_PODIUM_TOP = F1                   # the terraces (14.5 m)
# upper volumes: (ring, height of each corner above the datum): white, sharp, with sloping parapet lines
E_VOLUMES = [
    ([(36.8, -27.8), (36.1, -42.7), (42.0, -50.9), (54.7, -47.9), (58.4, -39.7), (51.0, -28.5), (42.8, -20.9)],
     [19.4, 19.4, 20.4, 20.4, 19.8, 19.2, 19.2]),
    ([(51.0, -27.8), (59.2, -38.9), (75.4, -28.6), (68.1, -17.3)], [19.2, 20.2, 20.2, 19.2]),
    ([(22.9, -38.8), (27.6, -45.4), (35.5, -39.8), (34.3, -28.7)], [19.6, 20.2, 20.2, 19.6]),     # west end
]
# the corner box with the one big window above the ramp's north-west rim (survey (58..66, -50..-45), faces south-east)
E_BOX_A, E_BOX_B = (59.89, -49.55), (65.57, -45.46)        # 7 m on E's south-east face
E_BOX_DEPTH, E_BOX_OUT = 8.0, 1.0                           # the box reaches this far back / its frame projects this far
E_BOX_Z = (F1 - 0.2, F1 + 5.8)                              # 6 m high, 3.3 m over the rim path
E_WINDOW_FRAME = 0.5
E_SE_FACE = ((59.4, -49.9), (78.7, -36.0))                  # the face above the ramp's rim (no podium glazing)
E_WEST_FACE = ((22.6, -38.5), (27.7, -45.7))                # the west end's face (no podium glazing)
# the one big framed window of the west end, on the west volume's face: (a, b, sill z, width, height)
E_WINDOWS = [((22.9, -38.8), (27.6, -45.4), F1 + 0.2, 6.0, 4.6)]
E_LOUVRE_A, E_LOUVRE_B = (67.6, -44.0), (77.9, -36.58)     # dark louvre cladding under the terrace parapet (plant rooms)
E_LOUVRE_Z = (F0 - 0.2, F1 - 0.35)
E_TERRACE_RAIL = [(27.7, -45.7), (32.8, -44.0), (29.3, -54.8), (37.9, -60.0), (39.9, -61.1),
                  (47.4, -54.5), (59.4, -49.9)]                                    # railed terrace edge (south-west)
E_TERRACE_RAIL_2 = [(66.9, -44.5), (78.7, -36.0)]                                  # ... and over the louvre block

# the 4.2 m path along the north-west rim of the ramp cutting, a white upstand with a bar railing on its far side
RIM_ROUTE = (456, 476)              # route point indices of the open ramp
RIM_EDGE = (9.5, -0.197)            # the cutting's rim (its railing): m left of the centreline at point 456, change per
#                                     point (the cutting gets shallower towards the plaza: 5.8 m at point 475)
RIM_PATH = (0.6, 4.8)               # the path: from .. to .. m beyond the rim
RIM_WALL = (456, 466)               # the upstand runs along E only (further up stands the main building / the hall)
RIM_WALL_H, RIM_WALL_T = 1.2, 0.3


def _route():
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "route.json")
    return json.load(open(path))["points"]


def rim_line(i0, i1, beyond, pts=None, side=1):
    """Points `beyond` metres outside the cutting's rim, route points i0..i1 (side 1: north-west = left of the race
    centreline, -1: south-east)."""
    pts = pts or _route()
    out = []
    for i in range(i0, i1 + 1):
        a, b = pts[i - 1], pts[i + 1]
        dx, dy = b["x"] - a["x"], b["y"] - a["y"]
        d = math.hypot(dx, dy) or 1.0
        offset = side * (RIM_EDGE[0] + RIM_EDGE[1] * (i - RIM_ROUTE[0]) + beyond)
        out.append((pts[i]["x"] - dy / d * offset, pts[i]["y"] + dx / d * offset))
    return out


def ramp_slope_rings(i0=466, i1=478, beyond=0.6):
    """The cutting's two slope bands at the ramp head (centreline to just beyond each rim): no plaza paving there."""
    pts = _route()
    centre = [(pts[i]["x"], pts[i]["y"]) for i in range(i0, i1 + 1)]
    return [rim_line(i0, i1, beyond, pts, side) + centre[::-1] for side in (1, -1)]


def rim_path_ring():
    pts = _route()
    return rim_line(RIM_ROUTE[0], RIM_ROUTE[1], RIM_PATH[0], pts) + rim_line(RIM_ROUTE[0], RIM_ROUTE[1], RIM_PATH[1], pts)[::-1]


# ----------------------------------------------------------------------------------------------------------------
# Ground colours (make_groundmap.py through campus_plan.GROUND_PAINT; the paving meshes lie over most of it)
# ----------------------------------------------------------------------------------------------------------------
C.paint("lower plaza", [(96.0, -29.3), (106.6, -25.4), (104.5, -18.0), (110.5, -8.5), (114.1, 1.4), (123.1, 4.1),
                        (125.4, -2.0), (128.7, -10.6), (TIER_X[-1], TIER_Y[-1]), (TIER_X[0], TIER_Y[0]), (133.0, -27.5),
                        (130.5, -20.5), (122.3, -22.3), (122.6, -32.7), (114.0, -36.5), (104.0, -38.5)], PLAZA_LOW)
C.paint("lower plaza paving", PLAZA_RING, PLAZA_LOW)
C.paint("rim path above the ramp", rim_path_ring(), PLAZA_LOW)
C.paint("lawn above the seat tiers", [(TIER_X[0] + TIERS * TIER_D, TIER_Y[0] - 5.0), (158.0, TIER_Y[0] - 5.0),
                                      (158.0, STAIR_Y[1] + 2.0), (STAIR_X0 + 7.0, STAIR_Y[1] + 0.3),
                                      (TIER_X[-1] + TIERS * TIER_D, TIER_Y[-1])], C.LAWN, lawn=255)
C.paint("lawn at E's south corner", E_SOUTH_CORNER, C.LAWN, lawn=255)
