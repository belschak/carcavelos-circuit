"""The upper plaza, the terracotta stairs, the sunken courtyard and the library as plain data (V10i, 2026-11-11).

From the campus survey (atlas report V10h, section 4; plan campus/library_loggia_plan.jpg) and Luc's reference photos.
No Blender here: route_from_sketch.py, make_groundmap.py, make_grass_tufts.py, env_plaza.py and env_library.py read it.
World metres, x east, y north. Levels: Floor 0 = 11.2 m (lower plaza, courtyard floor), Floor 1 = 14.5 m (upper plaza).

Deviations from the survey, all on purpose (the road is 8 m wide, the survey is good to +-2 m):
  stairs       9.5 m wide instead of 6.0 (8 m road + 0.75 m to each railing), along the race line (bearing 331, real
               340), 22.0 m long (11 route steps) instead of 21.8 m
  stair down   moved about 9 m east and turned parallel to the race line (survey 74..82.2 x 89.1..100.3, here a
               6.5 x 10 m slot whose west side is 5.2 m from the centreline): the 8 m road needs the neck beside
               the fence. Open at the top, an underpass links its foot to the courtyard (for a later lower path)
  S5           open plaza (survey text, photo I03)
  court, north the strip north of the S4 platform ends at y = 93.6 (survey 98.1): from the fence's corner the 8 m road
               has to swing 8 m west and turn north into the atrium; with the corner at 98.1 that bend had a 5 m
               radius, now 12 m
  fence        its south end runs round skylight S3 (survey: S3 "inside the fence"), S3 itself 0.5 m further west
  passage      9.6 m clear under the bridge (survey: about 8 m), so the road's shoulders are not inside the walls
"""
import math

FLOOR0 = 11.2
FLOOR1 = 14.5
LIFT = 0.06                 # driving surfaces lie this far above the route's track_z (env_track.ROAD_LIFT)
DECK_Z = FLOOR1 + LIFT      # the plaza deck = the road surface on the plaza
LOW_Z = FLOOR0 + LIFT       # the paving beside the stairs (voids) and the courtyard floor

# ---------------------------------------------------------------------------------------------------------------
# Stairs: along the race line between two route points (kept by the section override), straight between them
# ---------------------------------------------------------------------------------------------------------------
STAIR_FOOT = (111.07, -10.54)       # route point at the first riser (survey: bottom edge centre (109.6, -11.7))
STAIR_TOP = (100.33, 8.65)          # route point at the last riser (survey: top edge centre (102.1, 8.8))
STAIR_HALF = 4.75                   # m: half width of the stair block; railings stand 0.15 m inside
STAIR_RISERS = 21                   # east half: risers; the west half has seating tiers, two steps each
STAIR_SAG_M = 4.0                   # vertical curve at the foot, centred on the first riser
STAIR_CREST_M = 10.7                # vertical curve at the top (radius 67 m: at 21 m/s a kart needs 0.67 g to follow
                                    # it; with the first try, 8 m / radius 50 m, the bots floated 0.3 m over the top)
STAIR_CREST_BEFORE_TOP = 1.35       # ... its middle lies this far before the last riser: the curve ends 4 m after
                                    # it, the deck dips 12 cm at the last riser and is level 4 m further on
SEAM = 0.05                         # m: where the plaza's collider meets the border of the road's collider it lies this
                                    # far above it (see env_plaza.build_drive); PhysX makes contacts within 2 cm
SEAM_STEPS = (-0.06, -0.03, 0.0, 0.03)   # heights over the road of the strip that passes through the road's surface, one
                                    # value per route point (2 m) towards the border
JUDDER_M = 0.010                    # peak-to-peak height of the rounded ripple on the driving surface, one per step


def _unit(ax, ay, bx, by):
    d = math.hypot(bx - ax, by - ay)
    return (bx - ax) / d, (by - ay) / d, d


STAIR_AXIS = _unit(*STAIR_FOOT, *STAIR_TOP)[:2]                  # uphill
STAIR_LEN = _unit(*STAIR_FOOT, *STAIR_TOP)[2]
STAIR_RIGHT = (STAIR_AXIS[1], -STAIR_AXIS[0])                    # to the right going up (east)


def stair_frame(x, y):
    """(s along the stair from the first riser, u to the right of its axis), metres."""
    dx, dy = x - STAIR_FOOT[0], y - STAIR_FOOT[1]
    return dx * STAIR_AXIS[0] + dy * STAIR_AXIS[1], dx * STAIR_RIGHT[0] + dy * STAIR_RIGHT[1]


def stair_point(s, u):
    return (STAIR_FOOT[0] + STAIR_AXIS[0] * s + STAIR_RIGHT[0] * u, STAIR_FOOT[1] + STAIR_AXIS[1] * s + STAIR_RIGHT[1] * u)


def stair_profile(s):
    """Driving height (track_z) at s metres after the first riser: level, a sag curve round the foot, an even
    slope, a crest curve that ends STAIR_CREST_M / 2 - STAIR_CREST_BEFORE_TOP after the last riser, level.
    Radius of the crest = STAIR_CREST_M / slope (about 50 m: a kart at 22 m/s just stays on the ground)."""
    v0, v1 = 0.0, STAIR_LEN - STAIR_CREST_BEFORE_TOP              # the two vertices of the straight-line profile
    g = (FLOOR1 - FLOOR0) / (v1 - v0)
    a, c = STAIR_SAG_M / 2, STAIR_CREST_M / 2
    if s <= v0 - a:
        return FLOOR0
    if s < v0 + a:
        return FLOOR0 + g * (s - (v0 - a)) ** 2 / (4 * a)
    if s <= v1 - c:
        return FLOOR0 + g * (s - v0)
    if s < v1 + c:
        return FLOOR1 - g * (v1 + c - s) ** 2 / (4 * c)
    return FLOOR1


STAIR_SLOPE = (FLOOR1 - FLOOR0) / (STAIR_LEN - STAIR_CREST_BEFORE_TOP)
STAIR_REACH = (-STAIR_SAG_M / 2, STAIR_LEN - STAIR_CREST_BEFORE_TOP + STAIR_CREST_M / 2)   # s range that is not level


def judder(s):
    """Rounded ripple on the stair's driving surface (m, 0..JUDDER_M): one soft bump per step, fading in and out
    over two steps at each end of the even slope (a ripple on the crest curve helped lift the karts off)."""
    tread = STAIR_LEN / (STAIR_RISERS - 1)
    if s <= 0.0 or s >= STAIR_LEN:
        return 0.0
    lo, hi = STAIR_SAG_M / 2, STAIR_LEN - STAIR_CREST_BEFORE_TOP - STAIR_CREST_M / 2 - 2 * tread   # the even slope only,
    # ending two steps before the crest curve (a kart that comes onto the curve with a ripple's kick floats over the top):
    fade = max(0.0, min(1.0, (s - lo) / (2 * tread), (hi - s) / (2 * tread)))            # no ripple on the crest curve
    return JUDDER_M * fade * (0.5 - 0.5 * math.cos(2 * math.pi * s / tread))


# ---------------------------------------------------------------------------------------------------------------
# Buildings round the plaza (the game's outlines = OpenStreetMap, world_data.json)
# ---------------------------------------------------------------------------------------------------------------
MAIN_EAST = [(102.6, -15.1), (94.6, 5.3), (73.5, 59.8), (72.1, 63.2), (69.7, 75.0), (67.6, 74.7), (65.4, 80.6),
             (63.6, 85.1), (63.2, 86.2), (54.9, 107.3), (58.9, 108.8), (59.4, 106.2), (66.3, 107.8), (65.7, 111.5),
             (69.5, 112.9), (87.6, 120.0), (91.3, 121.4)]           # east edge of the main building, south to north
G_WEST = [(114.1, 1.4), (107.7, 21.4), (127.2, 29.1), (127.8, 27.6), (145.9, 38.2)]   # Horizon Hall, west and north

# Library (survey 4.4). Ground floor = the south wing and the north block east of the passage.
LIB_SOUTH_WING = [(105.0, 34.0), (123.3, 40.6), (114.8, 68.3), (92.8, 64.8)]
LIB_NORTH_BLOCK = [(80.8, 63.0), (115.9, 68.2), (113.9, 84.1), (79.4, 79.1)]
LIB_TOP = FLOOR1 + 8.8              # roof (23.3 m)
LIB_FLOOR2 = FLOOR1 + 3.6           # top of the glazed ground floor = underside of the white upper wall
PASSAGE_CLEAR = 3.4                 # m under the bridge
PASSAGE_HALF = 4.8                  # m from the passage axis to its walls
PASSAGE_AXIS = ((86.8, 63.9), (85.3, 79.95))     # middle of the bridge's south and north edge


def _on_line(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def _edge_point(a, b, x):
    """Point with this x on the line a-b."""
    t = (x - a[0]) / (b[0] - a[0])
    return (x, a[1] + (b[1] - a[1]) * t)


_pa = _unit(*PASSAGE_AXIS[0], *PASSAGE_AXIS[1])
PASSAGE_DIR = _pa[:2]
PASSAGE_RIGHT = (PASSAGE_DIR[1], -PASSAGE_DIR[0])                 # east


def passage_point(v, u):
    """v metres north of the bridge's south edge along the passage axis, u metres to the east of the axis."""
    a = PASSAGE_AXIS[0]
    return (a[0] + PASSAGE_DIR[0] * v + PASSAGE_RIGHT[0] * u, a[1] + PASSAGE_DIR[1] * v + PASSAGE_RIGHT[1] * u)


PASSAGE_LEN = _pa[2]
# west pier of the bridge: a white wall from the passage's west side out to the bridge's west face
PIER = [passage_point(-0.1, -PASSAGE_HALF - 1.5), passage_point(-0.1, -PASSAGE_HALF),
        passage_point(PASSAGE_LEN + 0.1, -PASSAGE_HALF), passage_point(PASSAGE_LEN + 0.1, -PASSAGE_HALF - 1.5)]
BRIDGE = [passage_point(-0.1, -PASSAGE_HALF - 1.5), passage_point(-0.1, PASSAGE_HALF + 1.4),
          passage_point(PASSAGE_LEN + 0.1, PASSAGE_HALF + 1.4), passage_point(PASSAGE_LEN + 0.1, -PASSAGE_HALF - 1.5)]
# ground floor outline (counter-clockwise): south wing, north block, the glazing on the passage's east side
LIB_GROUND = [LIB_SOUTH_WING[0], LIB_SOUTH_WING[1], LIB_SOUTH_WING[2], LIB_NORTH_BLOCK[1], LIB_NORTH_BLOCK[2],
              passage_point(PASSAGE_LEN + 0.1, PASSAGE_HALF), passage_point(-0.1, PASSAGE_HALF), LIB_SOUTH_WING[3]]

# south loggia (porch): front line = the south wing's south edge, 4.5 m deep, two full-height square columns and a
# one-storey corner pier; first-floor balcony behind the front 2 m; glazing at the back; solid east wall
LOGGIA_DEPTH = 4.5
LOGGIA_COLUMNS = [(106.6, 34.6), (112.1, 36.6)]
LOGGIA_PIER = (103.7, 38.2)
COLUMN = 0.9

# ---------------------------------------------------------------------------------------------------------------
# Skylights: flush frosted glass, drivable (survey 4.2). S3 and S4 lie inside the fence.
# ---------------------------------------------------------------------------------------------------------------
SKYLIGHTS = {
    "S2": [(92.8, 9.2), (100.3, 9.2), (102.7, 16.2), (95.2, 16.2)],
    "S1": [(88.7, 17.7), (96.6, 17.7), (99.8, 27.4), (91.9, 27.4)],
    "S3": [(75.1, 55.5), (81.1, 55.5), (81.1, 61.0), (75.1, 61.0)],
    "S4": [(65.3, 81.4), (71.7, 81.4), (71.7, 86.9), (63.0, 86.9), (63.6, 85.1)],
    "S5": [(62.4, 98.4), (68.1, 98.4), (68.1, 106.7), (62.4, 106.7)],
}

# ---------------------------------------------------------------------------------------------------------------
# Sunken courtyard (Floor 0) beside the Gallery, its fence, the stair down
# ---------------------------------------------------------------------------------------------------------------
_S3N = 61.2                          # the courtyard starts north of skylight S3
FENCE_SOUTH = [(74.5, 54.7), (81.9, 54.9), (81.9, _S3N), PIER[0]]            # round S3 to the bridge's west pier
COURT_END = (60.3, 93.6)             # the fence turns west here and meets the Gallery
COURT_CORNER = (70.55, 93.5)         # (survey: the strip goes on to y = 98.1; 4.6 m shorter here, see the note on top)
FENCE_NORTH = [PIER[3], (80.1, 82.4), (71.9, 87.0), COURT_CORNER, COURT_END]
S4_PLATFORM = [(65.16, 81.2), (71.9, 81.2), (71.9, 87.0), (62.85, 87.0), (63.2, 86.2), (63.6, 85.1)]
# two voids (counter-clockwise): the court south of the S4 platform and the strip north of it
COURT_SOUTH = [(72.9, _S3N), (81.9, _S3N), PIER[0], PIER[3], (80.1, 82.4), (79.2, 82.9), (74.5, 85.54), (71.9, 87.0), (71.9, 81.2), (65.16, 81.2),
               (65.4, 80.6), (67.6, 74.7), (69.7, 75.0), (72.1, 63.2)]
COURT_NORTH = [(62.85, 87.0), (71.9, 87.0), COURT_CORNER, COURT_END]
COURTS = [COURT_SOUTH, COURT_NORTH]
# Stair down to Floor 0 (the slot): a parallelogram beside the race line (4.9 m from the centreline, the stair stays
# on the right as in Luc's drawing 10), 6.5 m wide, 10 m long. Steps go down to the south; at the bottom a level
# landing, from there an underpass under the plaza (and under the race line) to the courtyard. Open at the top
# (north), so a later lower path (jump into the courtyard, through it, up this stair) only needs a ramp at S3.
SLOT_ORIGIN = (86.75, 91.38)         # south-west corner
SLOT_DIR = (-0.663, 0.749)           # uphill (north-north-west), parallel to the race line there
SLOT_RIGHT = (0.749, 0.663)
SLOT_LEN, SLOT_W, SLOT_LANDING = 10.0, 6.5, 2.5
SLOT_RISERS = 18


def slot_point(s, u):
    return (SLOT_ORIGIN[0] + SLOT_DIR[0] * s + SLOT_RIGHT[0] * u, SLOT_ORIGIN[1] + SLOT_DIR[1] * s + SLOT_RIGHT[1] * u)


def slot_frame(x, y):
    dx, dy = x - SLOT_ORIGIN[0], y - SLOT_ORIGIN[1]
    return dx * SLOT_DIR[0] + dy * SLOT_DIR[1], dx * SLOT_RIGHT[0] + dy * SLOT_RIGHT[1]


def slot_profile(s):
    """Driving height in the slot: level landing, then an even slope up to the plaza."""
    t = min(max((s - SLOT_LANDING) / (SLOT_LEN - SLOT_LANDING), 0.0), 1.0)
    return FLOOR0 + (FLOOR1 - FLOOR0) * t


STAIR_DOWN = [slot_point(0, 0), slot_point(0, SLOT_W), slot_point(SLOT_LEN, SLOT_W), slot_point(SLOT_LEN, 0)]
# underpass from the landing's west side to the courtyard's north-east corner (under the deck, 2.9 m clear)
UNDERPASS_COURT = ((79.2, 82.9), (74.5, 85.54))      # its mouth in the courtyard wall (on the diagonal fence line)
UNDERPASS = [slot_point(0, 0), slot_point(SLOT_LANDING, 0), UNDERPASS_COURT[1], UNDERPASS_COURT[0]]
UNDERPASS_CEILING = FLOOR1 - 0.35
FENCE_H = 1.1
KERB_H = 0.25

# ---------------------------------------------------------------------------------------------------------------
# The deck (Floor 1 paving, one surface from facade to facade) and the low paving beside the stairs
# ---------------------------------------------------------------------------------------------------------------


def _hit(p, d, a, b):
    """Intersection of the line p + t d with the line a-b."""
    ex, ey = b[0] - a[0], b[1] - a[1]
    den = d[0] * ey - d[1] * ex
    t = ((a[0] - p[0]) * ey - (a[1] - p[1]) * ex) / den
    return (p[0] + d[0] * t, p[1] + d[1] * t)


TOP_W = stair_point(STAIR_LEN, -STAIR_HALF)
TOP_E = stair_point(STAIR_LEN, STAIR_HALF)
FOOT_W = stair_point(0.0, -STAIR_HALF)
FOOT_E = stair_point(0.0, STAIR_HALF)
EDGE_W = _hit(STAIR_TOP, STAIR_RIGHT, MAIN_EAST[1], MAIN_EAST[2])     # the deck's south edge meets the Gallery
EDGE_E = _hit(STAIR_TOP, STAIR_RIGHT, G_WEST[0], G_WEST[1])           # ... and Horizon Hall
TREE_ROW = ((133.0, 35.0), (104.0, 112.0))                          # row of trees in pits east of the library (survey)
_east_dir = _unit(*TREE_ROW[0], *TREE_ROW[1])[:2]
DECK_SE = _edge_point(G_WEST[3], G_WEST[4], 137.5)
DECK_NE = _hit(DECK_SE, _east_dir, MAIN_EAST[16], (MAIN_EAST[16][0] + 0.931, MAIN_EAST[16][1] + 0.365))
DECK = [EDGE_W, TOP_W, TOP_E, EDGE_E, G_WEST[1], G_WEST[2], G_WEST[3], DECK_SE, DECK_NE, MAIN_EAST[16], MAIN_EAST[15],
        MAIN_EAST[14], MAIN_EAST[13], MAIN_EAST[12], MAIN_EAST[11], MAIN_EAST[10], MAIN_EAST[9], COURT_END, COURT_CORNER,
        (71.9, 87.0), (80.1, 82.4), PIER[3],
        PIER[0], (81.9, _S3N), (72.9, _S3N), MAIN_EAST[2]]
DECK_ISLANDS = [S4_PLATFORM]
# low paving (Floor 0) in the voids beside the stairs: west up to the Gallery, east up to Horizon Hall
_low_w = _hit(STAIR_FOOT, STAIR_RIGHT, MAIN_EAST[0], MAIN_EAST[1])
LOW_WEST = [FOOT_W, TOP_W, EDGE_W, MAIN_EAST[1]] + ([_low_w] if stair_frame(*_low_w)[1] < -STAIR_HALF - 0.3 else [])
LOW_EAST = [FOOT_E, stair_point(0.0, 13.0), G_WEST[0], EDGE_E, TOP_E]
# V10 integration: the Gallery's wall (V10c2, campus_plan) stands back under its roof edge, 6 m south of the white cross
# pier and 1.8 m north of it, with the Floor-1 deck of the Gallery above. So the Floor-0 paving goes on under that
# deck up to the wall: beside the courtyard (both voids; it also fills the sliver where the OSM outline bulged into
# the court) and beside the stairs' west void. Each strip is closed across its ends (UNDER_ENDS, env_plaza).
import campus_plan as _C                                            # noqa: E402  plain data, no Blender
_GV = lambda p: _C.MAIN_UV(*p)[1]                                   # noqa: E731  position along the Gallery
_UD, _US = _C.GALLERY_DEEP - 0.3, _C.GALLERY_SHALLOW - 0.3          # 0.3 m inside the two wall lines
_VB0, _VB1 = _C.GALLERY_BREAK
_S4W = (65.16, 81.2)
_CS = (72.9, _S3N)
UNDER_COURT_SOUTH = [_CS, MAIN_EAST[3], MAIN_EAST[4], MAIN_EAST[5], MAIN_EAST[6], _S4W, _C.MAIN(_US, _GV(_S4W)),
                     _C.MAIN(_US, _VB1), _C.MAIN(30.0, _VB1), _C.MAIN(30.0, _VB0), _C.MAIN(_UD, _VB0),
                     _C.MAIN(_UD, _GV(_CS))]
UNDER_COURT_NORTH = [(62.85, 87.0), COURT_END, _C.MAIN(_US, _GV(COURT_END)), _C.MAIN(_US, _GV((62.85, 87.0)))]
UNDER_STAIR_WEST = [LOW_WEST[-1], MAIN_EAST[1], EDGE_W, _C.MAIN(_UD, _GV(EDGE_W)), _C.MAIN(_UD, _GV(LOW_WEST[-1]))]
UNDERS = [UNDER_COURT_SOUTH, UNDER_COURT_NORTH, UNDER_STAIR_WEST]
UNDER_ENDS = [(_CS, UNDER_COURT_SOUTH[-1], UNDER_COURT_SOUTH), (_S4W, UNDER_COURT_SOUTH[6], UNDER_COURT_SOUTH),
              ((62.85, 87.0), UNDER_COURT_NORTH[3], UNDER_COURT_NORTH), (COURT_END, UNDER_COURT_NORTH[2], UNDER_COURT_NORTH),
              (EDGE_W, UNDER_STAIR_WEST[3], UNDER_STAIR_WEST)]
UNDER_TOP = FLOOR1 - 0.45            # the end walls stop inside the Gallery deck's band (FLOOR1 - 0.5 .. + 0.1)
# ... and the Gallery deck's edge over the courtyard gets the courtyard's fence (no kart drops in from that deck)
FENCE_GALLERY = [[_CS, _S4W], [(62.85, 87.0), COURT_END]]
LOWS = [LOW_WEST, LOW_EAST] + UNDERS
# planted barrier across the gap between the library and Horizon Hall (Luc: the way to the gym is off the map)
GAP_BARRIER = ((121.6, 38.9), (126.2, 29.6))
BARRIER_HALF = 0.9
BARRIER_H = 0.75


# ---------------------------------------------------------------------------------------------------------------
# Geometry helpers
# ---------------------------------------------------------------------------------------------------------------
def inside(x, y, poly):
    hit = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            hit = not hit
    return hit


def seg_distance(x, y, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    l2 = dx * dx + dy * dy
    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / l2))
    return math.hypot(x - a[0] - t * dx, y - a[1] - t * dy)


def poly_distance(x, y, poly):
    """Distance to the outline of a polygon (always >= 0)."""
    return min(seg_distance(x, y, a, b) for a, b in zip(poly, poly[1:] + poly[:1]))


def line_distance(x, y, line):
    return min(seg_distance(x, y, a, b) for a, b in zip(line, line[1:]))


def on_stairs(x, y, margin=0.0):
    s, u = stair_frame(x, y)
    return -margin <= s <= STAIR_LEN + margin and abs(u) <= STAIR_HALF + margin


def in_court(x, y):
    return any(inside(x, y, c) for c in COURTS)


def in_low(x, y):
    return any(inside(x, y, c) for c in LOWS)


def on_deck(x, y):
    """On the Floor-1 paving (the deck or the S4 platform), not in the stair-down slot."""
    if inside(x, y, STAIR_DOWN):
        return False
    return inside(x, y, DECK) or any(inside(x, y, i) for i in DECK_ISLANDS)


def deck_z(x, y):
    """Height of the deck: DECK_Z, except just above the stairs where it follows the end of their crest curve."""
    s, u = stair_frame(x, y)
    if STAIR_LEN - 0.02 <= s < STAIR_REACH[1] and abs(u) < STAIR_HALF + 2.0:      # (a vertex on the last riser may be 0.1 mm short)
        dip = FLOOR1 - stair_profile(s)
        w = 1.0 if abs(u) <= STAIR_HALF else 1.0 - (abs(u) - STAIR_HALF) / 2.0
        return DECK_Z - dip * w
    return DECK_Z


def surface_z(x, y):
    """Height of the built surface at (x, y) where this plan owns the ground, else None."""
    if on_stairs(x, y):
        return stair_profile(stair_frame(x, y)[0]) + LIFT
    if in_court(x, y) or in_low(x, y):
        return LOW_Z
    if inside(x, y, STAIR_DOWN):
        return slot_profile(slot_frame(x, y)[0]) + LIFT
    if on_deck(x, y):
        return deck_z(x, y)
    return None


def area(x, y, margin=0.0):
    """True within `margin` of anything this plan builds (used to keep generic dressing and tufts out)."""
    if surface_z(x, y) is not None:
        return True
    if margin <= 0.0:
        return False
    return (poly_distance(x, y, DECK) < margin or any(poly_distance(x, y, c) < margin for c in COURTS + LOWS)
            or on_stairs(x, y, margin))


def owns_route_point(p):
    """Route points whose road is the deck or the stairs (zones written by the section override)."""
    return p.get("zone") in ("stairs", "plaza deck")


# ---------------------------------------------------------------------------------------------------------------
# Terrain under and round the plaza (5 m grid nodes; env_terrain.apply)
# ---------------------------------------------------------------------------------------------------------------
LOW_REACH = 7.2             # m: every node this close to a Floor-0 area is low, so no 5 m cell slopes up into it
OPEN_EDGES = [(DECK_SE, DECK_NE), (DECK_NE, MAIN_EAST[16])]     # where the deck meets the lawn (east, north-east)
EDGE_BLEND = 15.0


def _smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def terrain_z(x, y, natural):
    """Height of a terrain node: below Floor 0 near the courtyard, the voids beside the stairs, the stair slot and
    the underpass; below the deck under it; level with the deck just outside its open edges, blending back to the
    natural ground over EDGE_BLEND."""
    lows = COURTS + LOWS + [STAIR_DOWN, UNDERPASS,
                            [stair_point(-3, -STAIR_HALF), stair_point(-3, STAIR_HALF), TOP_E, TOP_W]]
    if any(inside(x, y, c) or poly_distance(x, y, c) <= LOW_REACH for c in lows):
        return FLOOR0 - 0.15
    if inside(x, y, DECK):
        return FLOOR1 - 0.3
    d = min(seg_distance(x, y, a, b) for a, b in OPEN_EDGES)
    if d < EDGE_BLEND:
        t = _smooth((d - 1.5) / (EDGE_BLEND - 1.5))
        return (FLOOR1 - 0.02) * (1 - t) + natural * t
    return natural
