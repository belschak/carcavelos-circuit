"""Entrance plaza (V15a, 2026-11-11): the plan of the start plaza as plain data, read by env_entrance.py (geometry and
dressing), make_groundmap.py (ground colour under the paving) and make_entrance_textures.py.

Where: between Milestone J (north) and Cascais Hall B (south), from the avenue sidewalk to the white end wall of the
main building. Race start/finish: route i0-i15 runs east into the main building, the run-in comes down from the
north (V15b: i640-i646 north of the pool, a loop out west to 7.4 m from the car-park ramp, i647-i666, and the start
straight i667-i681). World metres, x east, y north, origin lat 38.6780 / lon -9.3265.

Sources (survey V15a, atlas reports): DGT orthophoto with a 5 m grid (campus_sv/ortho_grid_entrance_plaza.jpg, 9 px
per metre), the 2026 satellite tile (campus/gsat_c010_150...), Street View Jul 2020 (P_p00, P_p01, luc_1..5, N_r1,
N_r2). Positions are good to about 1 m, the ramp corners to 1.5-2 m. Nothing is copied from the images: shapes only.
"""
import math

# ----------------------------------------------------------------------------------------------------------------
# The paved plaza. = campus_plan's "entrance plaza" ring, with the east edge taken into the recess of the glazed
# west entrance (2 m under the roof edge) so the paving reaches the glass.
# ----------------------------------------------------------------------------------------------------------------
# V18g (Google satellite 2026, 20z/21z, +-0.7 m): the north edge is not a straight line at y = 147.5 but the foot of
# Milestone's lawn bank, (-88.5, 140.3) - (-58.6, 146.5) - (-41.2, 150.5); the east lawn's edge runs (-26, 152) -
# (-2, 161.6); the south edge is the north edge of B's planted strip (PLANTED, s = 6.2) west of t = 42.5.
RING_V18G = [(-90.0, 102.0), (-88.0, 97.0), (-85.36, 96.44), (-26.54, 119.02), (-24.32, 113.23), (15.0, 128.1),
             (16.91, 128.83), (8.93, 149.17), (7.06, 148.43), (2.8, 159.8), (1.5, 163.5), (-2.0, 161.6), (-26.0, 152.0),
             (-28.4, 160.5), (-30.4, 166.9), (-31.4, 169.6), (-38.9, 168.6), (-39.3, 161.3), (-39.3, 150.9), (-41.2, 150.5),
             (-58.6, 146.5), (-88.5, 140.3), (-90.0, 140.0)]
# V18h: RING = RING_V18G plus the curve plaza's fan (the paving takes a round bite out of Milestone's lawn bank), see
# the section "V18h" below, where RING, J_LAWN and J_BANK get their final shape.
TREE_PAD = 3.0             # m: trees this close outside the ring (the row along B's planted strip) are plaza trees too
# V15b (start straight): inside this box (x0, y0, x1, y1: the changed section of the race line with its surroundings)
# env_entrance moves or drops the frozen plants and props that the new line passes too closely
SWEEP_BOX = (-64.0, 116.0, 12.0, 164.0)

# calcada of small pale cubes. V16 (photospheres N06/N10/N12/N14): cream to grey-beige (#beb5aa under cloud, #cabb99
# in the sun), stones 9 x 9 to 9 x 18 cm in straight rows parallel to B's facade. Was #D8CEAC (too yellow-tan),
# stones 12.5 x 19 cm in rows along the drain channels.
CALCADA = "#D5CDB9"
CALCADA_RGB = (213, 205, 185)
CALCADA_TILE = 3.0         # m per texture tile (1024 px: stones of 9.4 x 14.3 cm)
BAND_U = (0.9336, 0.3584)  # along wing B's facade (bearing 69 deg): the plank band, the tree rows, the seat row
BAND_N = (-0.3584, 0.9336)  # across it, into the plaza
ROW_DIR = BAND_U           # the stone rows run parallel to B's facade

# ----------------------------------------------------------------------------------------------------------------
# Drain channels. V16 (photospheres N10, N12, N14, N15): flush cast-iron slot gratings, rust-brown #57382a, only
# 0.20-0.25 m wide, parallel, 14.5 m apart, bearing 345/165, through (-38, 140), (-24, 142), (-9, 143); a fourth one
# 14.5 m further west is likely (not photographed). They run from the plank band to the north edge of the setts; one
# of them goes on across the planks. (V15a had them 0.35 m wide, oxblood red, bearing 158.)
# Each: x where the line crosses y = 149.3, bearing in degrees. The last one in channel_lines() is the grating across
# the mouth of the car-park ramp, which runs on along the sidewalk.
# ----------------------------------------------------------------------------------------------------------------
# V18g (Google satellite 2026, rectified along B): the channels are square to B's facade (bearing 159, not 165),
# 13.5-20.5 m apart, at t = -13, 22.5 (it passes the ramp's portal wall), 43 (the east end of B's planted strip), 56.5
# and 73.5 m along B; every one runs from the plaza's north edge to B's planted strip or wall.
CHANNEL_T = (-13.0, 22.5, 43.0, 56.5, 73.5)
CHANNEL_W = 0.22           # m
DRAIN = "#5E3B2B"          # rust-brown (survey #57382a, a hair lighter so the slots still read)

# ----------------------------------------------------------------------------------------------------------------
# Plank band along wing B (V16, photospheres N08, N10, N13): large pale concrete planks 0.25-0.4 x 0.6-1.5 m laid
# lengthwise, 12-14 m wide from B's planted strip, straight flush edge to the setts. In the frame of B's facade:
# origin = the ring's south corner, t along BAND_U, s along BAND_N. It ends 9 m before the glass front of the west
# entrance (the sett apron, 2020 state) at a row of slim steel bollards.
# ----------------------------------------------------------------------------------------------------------------
BAND_O = (-64.0, 98.0)
BAND_T = (-18.0, 75.75)    # m along B: from the plaza's south-west edge to the apron line
BAND_S = (-0.5, 11.2)      # m across: from the ring's south edge (clipped to the ring) to the flush edge
PLANK = "#C4C0B6"          # V18 (team walk 7 Nov 2026): today large grey rectangular slabs (survey 2019: pale planks)
PLANK_TILE = 6.0           # m per texture tile (V18: 10 rows of 0.6 m, slabs 0.9-1.8 m long)
PIT = 1.6                  # m: square gravel pit under each young tree
PIT_COLOUR = "timber"      # street palette cell (warm grey-brown gravel)
BOLLARD_S = (0.9, 3.4, 5.9, 8.4, 10.9, 16.9, 19.4, 21.9)   # slim steel bollards on the apron line (those off the line stay)
# tan path from the plaza to Milestone's glazed entrance link, 10 m wide (photospheres N06, N14, ortho)
J_PATH = [(-38.4, 147.2), (-28.4, 147.2), (-28.4, 160.5), (-30.4, 166.9), (-31.4, 169.6), (-38.9, 168.6), (-39.2, 161.3)]
J_PATH_COLOUR = None     # V18g: Google shows the plaza's own pale paving here (was the tan "lamp_sand")

# ----------------------------------------------------------------------------------------------------------------
# White concrete seat blocks: one long row on the ortho, 5.7 m apart, from (4.3, 137.5) to (-75.3, 107.1), i.e. on a
# line of bearing 69 deg (parallel to B). Each lies along the row.
# ----------------------------------------------------------------------------------------------------------------
SEAT_FIRST = (4.3, 137.5)
SEAT_STEP = (-5.686, -2.171)       # to the next block (west-south-west)
SEAT_COUNT = 16                    # V16 (N12, N14, ortho): about 16 blocks of 3.0 x 0.6 x 0.45 m (was 15 of 2.5 x 0.8)
SEAT_SIZE = (3.0, 0.6, 0.45)       # length, width, height (m)
SEAT_CLEAR = 6.6 + 1.55            # centre of a block from the race centreline: 6.6 m for its nearest corner
SEAT_WHITE = "#E3DCCB"             # pale concrete (survey #b29f7e top in the processed 2020 shots)

# ----------------------------------------------------------------------------------------------------------------
# Slim lamp columns in the tree rows. V16 (N08, N13): dark anthracite columns of 0.12-0.18 m, 4.5-6.5 m high, the
# lantern built into the top, no arm, every 18-20 m (V15a: 7 m poles with a head on a short arm)
# ----------------------------------------------------------------------------------------------------------------
LAMPS = [(-74.2, 105.6), (-58.0, 113.2), (-41.1, 119.8), (-25.1, 125.9), (-10.1, 123.4), (1.8, 124.2)]
LAMP_H = 5.5
LAMP_DARK = "#2F3134"

# ----------------------------------------------------------------------------------------------------------------
# Underground car-park ramp (survey notes_N 1.6; satellite corners +-1.5 m). It descends east (bearing 76 deg).
# ----------------------------------------------------------------------------------------------------------------
# Survey corners: north edge (-81.4, 130.6)-(-54.5, 135.4), south edge (-78.2, 117.1)-(-51.7, 126.3). Here the whole
# ramp lies 1.0 m further west along its own axis: its portal corner then keeps 1.3 m from the outer edge of the
# track corridor (26 m from the centreline), which must stay as it is.
RAMP_NW, RAMP_NE_V15 = (-82.37, 130.36), (-55.47, 135.16)  # north edge: mouth -> portal
RAMP_SW, RAMP_SE_V15 = (-79.17, 116.86), (-52.67, 126.06)  # south edge: mouth -> portal
# V18h (curve plaza): the portal wall stood 7.4 m from the race line, exactly where a kart swings out of the loop. The
# ramp is RAMP_SHORTEN m shorter at its portal end (the mouth at the avenue stays where Google shows it; the portal is
# then 6 m west of the real one, the ramp 21.6 m long instead of 27.6). 0.0 restores the V15b ramp.
RAMP_SHORTEN = 6.0


def _towards(a, b, d):
    ln = math.hypot(b[0] - a[0], b[1] - a[1])
    return (round(a[0] + (b[0] - a[0]) * d / ln, 2), round(a[1] + (b[1] - a[1]) * d / ln, 2))


RAMP_NE = _towards(RAMP_NE_V15, RAMP_NW, RAMP_SHORTEN)
RAMP_SE = _towards(RAMP_SE_V15, RAMP_SW, RAMP_SHORTEN)
RAMP_DROP = 3.3            # m: floor at the portal below the plaza beside it (survey 3.2 +-0.5)
RAMP_WALL = 0.35           # m: thickness of the concrete side walls
RAMP_PARAPET = 1.1         # m: V16 (N06_z_ramp_h100, N14_h270): solid concrete parapets 1.1 m over the plaza, no railing
                           # (0 at the mouth, full from 3 m in; V15a: 0.3 m with a steel handrail)
RAMP_FASCIA = 0.55         # m: dark fascia band over the whole portal (N15_z_ramp_h084: 0.9 m under a 0.5 m white slab
                           # edge; ours is 3.3 m deep, so 0.55 m under 0.25 m of slab and the white parapet)
PORTAL_H = 2.5             # m: clear height of the black opening (V16: 2.5 m from the avenue, ST02; was 2.75)
CONCRETE = "#D6CFBA"       # pale warm concrete (survey #8c8b6a in mixed light)


# ----------------------------------------------------------------------------------------------------------------
# V18g (2026-11-12): the plaza's surroundings as on Google Maps. Sources: satellite 2026 at 20z / 21z (atlas
# google_survey/n/sat*.jpg, rectified mosaic 8 px per metre, +-0.7 m), Street View May 2026 at (-100.2, 126.7).
# ----------------------------------------------------------------------------------------------------------------
# B's planted strip (band frame, m): lawn with a row of clipped shrubs between the plaza and B's wall, t -20.5 .. 42.5;
# east of it the paving reaches the wall
PLANTED_T, PLANTED_S = (-20.5, 42.5), (-3.7, 6.2)
SHRUB_ROW = dict(s=0.9, t0=5.0, t1=41.0, step=2.4)      # dark clipped shrubs, one row (21z: blobs of 1.5-2 m)
TREE_ROW_S = 8.7           # the south tree row stands 2.5 m inside the paving (was 6.9: on the strip's edge)
# Milestone J's south lawn: a raised lawn inside a flush pale concrete edging (0.5 m), a grass bank down to the plaza
J_LAWN_V18G = [(-96.0, 166.3), (-86.1, 147.9), (-42.6, 154.9), (-41.6, 162.3)]
J_LAWN_EDGING = 0.5
J_BANK_V18G = [(-99.0, 168.0), (-96.1, 160.0), (-92.65, 150.0), (-90.65, 143.0), (-90.0, 140.0), (-88.5, 140.3),
               (-58.6, 146.5), (-41.2, 150.5), (-39.3, 150.9), (-39.3, 163.0)]
J_SAPLINGS = [(-87.5, 157.0), (-82.8, 158.0), (-78.0, 154.3), (-69.0, 155.3), (-63.3, 157.6), (-62.8, 153.8),
              (-52.0, 157.3), (-48.0, 159.3)]
J_TREE = (-56.8, 156.4)    # one larger dark tree on the lawn
# The avenue's entrance side, east of the separator (the roadway west of it is the street section's). South -> north.
AV_KERB = [(-91.1, 100.0), (-93.0, 110.0), (-93.6, 116.0), (-94.8, 121.0), (-95.95, 130.0), (-98.1, 140.0)]   # asphalt | setts
AV_PLAZA_X = -89.8         # the setts of the sidewalk reach 0.2 m into the plaza's ring
# red-brown bus bay: a lens between the separator and the lane, apex in the north, widest at y 117-121, closed in the
# south by the pale arc of the car lay-by (no lettering on it)
BUS_BAY = [(-102.0, 130.2), (-101.3, 127.5), (-100.2, 125.2), (-98.3, 122.6), (-96.5, 121.3), (-94.9, 120.6),
           (-94.2, 118.0), (-93.7, 116.0), (-94.6, 114.3), (-96.0, 113.0), (-97.0, 111.2), (-97.5, 109.7), (-97.6, 105.0),
           (-98.6, 105.0), (-99.5, 110.0), (-100.6, 115.0), (-101.3, 120.0), (-101.7, 125.0)]
BUS_BAY_ARC = [(-93.7, 116.0), (-94.6, 114.3), (-96.0, 113.0), (-97.0, 111.2), (-97.5, 109.7)]   # the lay-by's pale kerb arc
# two-way cycle track from the north along the avenue; it ends at the plaza's north-west corner. Centreline, 3.3 m wide
# (0.5-1 m west of the satellite north of y = 160, so that it passes our outline of Milestone J)
CYCLE = [(-91.6, 140.6), (-92.3, 143.0), (-94.3, 150.0), (-97.4, 160.0), (-99.9, 166.0), (-101.0, 172.0)]
CYCLE_HALF, WALK_W = 1.65, 3.6          # the calcada sidewalk lies between the track and the roadway
# timber-coloured round posts (Street View): along the kerb, and in two rows beside the car-park driveway
POST_STEP = 2.6
DRIVE_ROWS = [((-95.2, 131.4), (-83.6, 131.2)), ((-93.0, 114.6), (-80.6, 115.5))]
SIGN_BOARD = dict(at=(-94.9, 130.7), bearing=350.0, width=2.4, top=3.3)   # two blank boards on two grey posts
CYCLE_SIGN = (-90.6, 141.6)             # blue round sign on a post where the track ends
KERB_LAMPS = [(-97.2, 137.6), (-102.6, 161.0)]
LAYBY_CARS = [(-95.3, 107.6, 349.0), (-93.4, 101.8, 349.0)]     # two cars parked in the lay-by south of the bus bay: x, y, bearing


# ----------------------------------------------------------------------------------------------------------------
# V18h (2026-11-12, Luc after driving): the loop before the finish becomes a curve plaza: one wide, flat, paved area
# with nothing standing on it, where a kart can swing out wide or cut tight on the inside. The race line stays.
# Read by env_entrance (paving, dressing, terrain), env_north (lawn edging), env_trackside (no chevron board here).
# ----------------------------------------------------------------------------------------------------------------
LOOP_C, LOOP_R = (-38.36, 140.51), 10.15   # the race line's loop: circle through route points 660-673 (fit, +-1 cm)
FAN_R = 20.3               # m from LOOP_C: the paving's rim on the north side (twice the loop: 10.15 m outside the line)
FAN_EDGE = 0.45            # flush pale edging stones along that rim (inside FAN_R)
EDGING = "#D9D3C4"         # pale precast concrete kerb stones (as the edging of Milestone's lawn)
EDGING_TILE = (3.0, 0.45)  # m per texture tile: along the rim (three stones of 1.0 m), across it
FAN_HEM = 0.7              # beyond the rim the sheet dips under the lawn over this width (no lip to hit coming back)
FAN_LAWN_GAP = 2.6         # Milestone's raised lawn (its 0.15 m edging) starts this far behind the rim; grass between
FAN_CLEAR = 10.0           # m from the race centreline: nothing solid stands closer beside route points FAN_I
FAN_I = (650, 678)         # route points with the full FAN_CLEAR ...
FAN_TAPER = 7              # ... back to the usual 6.6 m over this many points before and after (the start line: 685 = 0)
FAN_NO_SIGN = (655, 676)   # no chevron board beside these route points (it stood 1.2 m outside the road, in the drift zone)
CENTRE_RING = dict(r=3.4, band=0.4)        # flush ring of dark setts round LOOP_C (the campus' forecourt has the same
                                           # motif): it marks the middle to go round; None = no ring
RIM_LAMP = (322.0, FAN_R + 1.3)            # bearing from LOOP_C, radius: the loop's lamp and bin stand on the lawn bank
RIM_BIN = (314.5, FAN_R + 1.2)             # behind the rim (they stood inside the loop)
TREE_EDGE_S = 7.1          # trees the plaza pushes out go no closer to B's planted strip than this (band frame, m)
# the ground (5 m terrain nodes) is raised to the plaza's level south and south-west of the loop, where it fell away
# to B by up to 1.0 m from 7 m beside the line: full effect inside t LEVEL_T[1..2] and s LEVEL_S[1..2] (band frame)
PLAZA_LEVEL = 14.5
LEVEL_T = (1.0, 17.0, 62.0, 70.0)
LEVEL_S = (3.0, 7.0, 40.0, 46.0)
FILL_I = (646, 8)          # route points beside which the plaza sheet lies level with the road right up to the road's
FILL_TAPER = 5             # edge (it covers the shoulder and its gutter); fades out over this many points
FILL_REACH = 6.0           # m beyond the shoulder's foot over which the sheet returns to its draped height


def _ss(v):
    v = min(max(v, 0.0), 1.0)
    return v * v * (3 - 2 * v)


def level_weight(x, y):
    """0..1: how far the terrain at (x, y) is raised to PLAZA_LEVEL (never lowered)."""
    if not (-75.0 < x < 15.0 and 95.0 < y < 165.0):
        return 0.0
    t, s = band_ts(x, y)
    return (_ss((t - LEVEL_T[0]) / (LEVEL_T[1] - LEVEL_T[0])) * (1.0 - _ss((t - LEVEL_T[2]) / (LEVEL_T[3] - LEVEL_T[2])))
            * _ss((s - LEVEL_S[0]) / (LEVEL_S[1] - LEVEL_S[0])) * (1.0 - _ss((s - LEVEL_S[2]) / (LEVEL_S[3] - LEVEL_S[2]))))


def fan_weight(i, n=685):
    """0..1 for route index i: 1 beside the curve plaza (FAN_I), fading over FAN_TAPER points on both sides."""
    a, b = FAN_I
    d = 0 if a <= i <= b else min((a - i) % n, (i - b) % n)
    return max(0.0, 1.0 - d / FAN_TAPER)


def fill_weight(i, n=685):
    """0..1 for route index i: how far the plaza sheet is filled up to road level beside it."""
    a, b = FILL_I
    inside_ = (a <= i or i <= b) if a > b else a <= i <= b
    d = 0 if inside_ else min((a - i) % n, (i - b) % n)
    return max(0.0, 1.0 - d / FILL_TAPER)


def in_hem(x, y):
    """Metres beyond the fan's rim where the rim borders the lawn (0 inside the rim or elsewhere)."""
    b, r = loop_polar(x, y)
    return max(0.0, r - FAN_R) if FAN_ARC[0] - 0.01 <= b <= FAN_ARC[1] + 0.01 and r <= FAN_R + FAN_HEM + 0.05 else 0.0


def arc_band(b0, b1, r0, r1, step=4.0):
    """Ring sector round LOOP_C from bearing b0 to b1 (b1 > b0), radii r0 < r1, as one polygon."""
    m = max(1, int(math.ceil((b1 - b0) / step)))
    inner = [loop_pt(b0 + (b1 - b0) * k / m, r0) for k in range(m + 1)]
    outer = [loop_pt(b0 + (b1 - b0) * k / m, r1) for k in range(m + 1)]
    return inner + outer[::-1]


def polar_uv(b0, r0, r_mid, su, sv):
    """uv function for an arc band: u along the arc (m at radius r_mid / su), v across it ((r - r0) / sv)."""
    def uv(x, y):
        b, r = loop_polar(x, y)
        rel = (b - b0) % 360.0
        if rel > 350.0:
            rel -= 360.0
        return (math.radians(rel) * r_mid / su, (r - r0) / sv)
    return uv


def loop_polar(x, y):
    """(bearing in degrees clockwise from north, radius) of (x, y) seen from LOOP_C."""
    dx, dy = x - LOOP_C[0], y - LOOP_C[1]
    return math.degrees(math.atan2(dx, dy)) % 360.0, math.hypot(dx, dy)


def loop_pt(bearing, r):
    a = math.radians(bearing)
    return (LOOP_C[0] + r * math.sin(a), LOOP_C[1] + r * math.cos(a))


def bite(poly, r, step=4.0):
    """poly without the disc of radius r round LOOP_C: where the outline enters the disc it follows the circle to the
    point where it leaves again (one bite; the polygon's corners inside the disc are dropped)."""
    def cross(a, b):                                   # the point of a -> b on the circle (a and b on different sides)
        lo, hi = 0.0, 1.0
        ins = loop_polar(*a)[1] < r
        for _ in range(40):
            m = (lo + hi) / 2
            q = (a[0] + (b[0] - a[0]) * m, a[1] + (b[1] - a[1]) * m)
            if (loop_polar(*q)[1] < r) == ins:
                lo = m
            else:
                hi = m
        return (a[0] + (b[0] - a[0]) * hi, a[1] + (b[1] - a[1]) * hi)
    n = len(poly)
    inside_ = [loop_polar(*p)[1] < r for p in poly]
    if not any(inside_) and all(seg_distance(LOOP_C[0], LOOP_C[1], a[0], a[1], b[0], b[1]) >= r
                                for a, b in zip(poly, poly[1:] + poly[:1])):
        return list(poly)
    start = next(k for k in range(n) if not inside_[k])
    out, entry = [], None
    for k in range(n):
        a, b = poly[(start + k) % n], poly[(start + k + 1) % n]
        ia, ib = inside_[(start + k) % n], inside_[(start + k + 1) % n]
        if not ia:
            out.append(a)
        if ia == ib and not ia:                        # both outside: does the edge dip into the disc?
            if seg_distance(LOOP_C[0], LOOP_C[1], a[0], a[1], b[0], b[1]) < r:
                dx, dy = b[0] - a[0], b[1] - a[1]
                tm = ((LOOP_C[0] - a[0]) * dx + (LOOP_C[1] - a[1]) * dy) / (dx * dx + dy * dy)
                mid = (a[0] + dx * tm, a[1] + dy * tm)
                out += _arc(cross(a, mid), cross(mid, b), r, step)
        elif not ia and ib:
            entry = cross(a, b)
        elif ia and not ib:
            out += _arc(entry, cross(a, b), r, step)
    return [(round(x, 2), round(y, 2)) for x, y in out]


def _arc(p, q, r, step):
    """Points on the circle from p to q, the short way round (both included)."""
    b0, b1 = loop_polar(*p)[0], loop_polar(*q)[0]
    d = (b1 - b0 + 180.0) % 360.0 - 180.0
    m = max(1, int(math.ceil(abs(d) / step)))
    return [loop_pt(b0 + d * k / m, r) for k in range(m + 1)]


def _fan_ring(base):
    """base (the V18g plaza ring) with the fan added: its north edge between the bank's foot and Milestone's entrance
    path follows the circle FAN_R round LOOP_C instead of the foot of the grass bank."""
    lawn = bite(J_BANK_V18G, FAN_R)
    arc = [p for p in lawn if abs(loop_polar(*p)[1] - FAN_R) < 0.02]          # runs from the bank's foot to the path
    k0 = base.index((-39.3, 161.3))
    k1 = base.index((-58.6, 146.5))
    return base[:k0 + 1] + arc[::-1] + base[k1:]


def planted_ring():
    (t0, t1), (s0, s1) = PLANTED_T, PLANTED_S
    return [band_pt(t0, s0), band_pt(t1, s0), band_pt(t1, s1), band_pt(t0, s1)]


def in_planted(x, y, pad=0.0):
    t, s = band_ts(x, y)
    return PLANTED_T[0] - pad <= t <= PLANTED_T[1] + pad and PLANTED_S[0] - pad <= s <= PLANTED_S[1] + pad


def kerb_posts():
    """[(x, y)] of the timber posts: 0.45 m behind the kerb from the track's end down to the driveway, along the car
    lay-by south of it, and the two rows beside the driveway to the car-park ramp."""
    def along(a, b, first=0.0):
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        n = int((d - first) // POST_STEP)
        return [(a[0] + (b[0] - a[0]) * (first + k * POST_STEP) / d, a[1] + (b[1] - a[1]) * (first + k * POST_STEP) / d)
                for k in range(n + 1)]
    out = along((-97.55, 139.6), (-95.6, 132.6))                  # kerb north of the driveway
    out += along((-92.55, 110.0), (-90.7, 100.6))                 # kerb of the car lay-by
    for a, b in DRIVE_ROWS:
        out += along(a, b)
    return out


def avenue_side(S):
    """The street plan's layers of the avenue's entrance side east of the separator (called from
    street_plan.avenue_entrance with the module itself): lane asphalt up to the measured kerb, the red bus bay, the
    sett sidewalk, the cycle track, and the lawns beside the plaza as ground colours (they also cut the plaza sheet)."""
    y0, y1 = S.AV_ENTRANCE
    sep = S.offset(S.av_entrance_line(y0, y1), S.AV_SEPARATOR[1])           # east edge of the separator, south -> north
    walk_w = S.offset(CYCLE, CYCLE_HALF + WALK_W)
    track_w, track_e = S.offset(CYCLE, CYCLE_HALF), S.offset(CYCLE, -CYCLE_HALF)
    north = [p for p in walk_w if p[1] <= y1 + 0.5]
    S.layer("B2 lane asphalt", "asphalt", sep + (AV_KERB + north)[::-1])
    S.layer("B2 sett sidewalk", "calcada", AV_KERB + walk_w + track_w[::-1] + [(AV_PLAZA_X, 140.2), (AV_PLAZA_X, y0)])
    S.layer("B2 cycle track", "asphalt", track_w + track_e[::-1])
    S.layer("B2 bus bay", "pal:brick_red", list(BUS_BAY))
    S.paint_ribbon("white", BUS_BAY[:8], -0.07, 0.07)                       # its outline towards the lane, no word on it
    S.paint_ribbon("kerb", BUS_BAY_ARC, -0.12, 0.12)
    S.paint_ribbon("kerb", AV_KERB, -0.14, 0.14)
    for line, d in ((CYCLE, CYCLE_HALF), (CYCLE, -CYCLE_HALF)):
        S.paint_ribbon("kerb", line, d - 0.06, d + 0.06)
    S.dashes(CYCLE, 0.0, 0.1, 1.0, 2.0)
    # lawns (ground colours, lawn mask): Milestone's bank and raised lawn with its pale edging, B's planted strip
    S.layer("N Milestone bank", None, list(J_BANK), ground="lawn")
    S.layer("N Milestone lawn", None, list(J_LAWN), ground="lawn")           # (its pale edging: env_north.build_structures)
    S.layer("N planted strip at B", None, planted_ring(), ground="lawn")


def inside(x, y, poly):
    hit = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
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


def near_ring(x, y, ring, pad):
    return inside(x, y, ring) or ring_distance(x, y, ring) <= pad


def _unit(a, b):
    d = math.hypot(b[0] - a[0], b[1] - a[1])
    return ((b[0] - a[0]) / d, (b[1] - a[1]) / d)


def ramp_axis():
    """Unit vector down the ramp (mouth middle -> portal middle)."""
    m = ((RAMP_NW[0] + RAMP_SW[0]) / 2, (RAMP_NW[1] + RAMP_SW[1]) / 2)
    p = ((RAMP_NE[0] + RAMP_SE[0]) / 2, (RAMP_NE[1] + RAMP_SE[1]) / 2)
    return _unit(m, p)


def ramp_inner():
    """The ramp floor, counter-clockwise: SW, SE, NE, NW."""
    return [RAMP_SW, RAMP_SE, RAMP_NE, RAMP_NW]


def ramp_outer(extra=0.0):
    """The floor plus the side walls and the portal wall (the mouth stays open), counter-clockwise: SW, SE, NE, NW."""
    t = RAMP_WALL + extra
    ax = ramp_axis()
    us, un = _unit(RAMP_SW, RAMP_SE), _unit(RAMP_NW, RAMP_NE)
    ns, nn = (us[1], -us[0]), (-un[1], un[0])                  # outward normals of the south and the north edge
    return [(RAMP_SW[0] + ns[0] * t - us[0] * extra, RAMP_SW[1] + ns[1] * t - us[1] * extra),
            (RAMP_SE[0] + ns[0] * t + ax[0] * t, RAMP_SE[1] + ns[1] * t + ax[1] * t),
            (RAMP_NE[0] + nn[0] * t + ax[0] * t, RAMP_NE[1] + nn[1] * t + ax[1] * t),
            (RAMP_NW[0] + nn[0] * t - un[0] * extra, RAMP_NW[1] + nn[1] * t - un[1] * extra)]


def mouth_channel():
    """(point, bearing) of the grating across the ramp mouth: 0.45 m outside the mouth edge, parallel to it."""
    ax = ramp_axis()
    u = _unit(RAMP_NW, RAMP_SW)
    mid = ((RAMP_NW[0] + RAMP_SW[0]) / 2 - ax[0] * 0.45, (RAMP_NW[1] + RAMP_SW[1]) / 2 - ax[1] * 0.45)
    return mid, math.degrees(math.atan2(u[0], u[1])) % 360.0


def band_pt(t, s):
    """Point in the frame of B's facade: t m along it from BAND_O, s m across into the plaza."""
    return (BAND_O[0] + BAND_U[0] * t + BAND_N[0] * s, BAND_O[1] + BAND_U[1] * t + BAND_N[1] * s)


def band_ts(x, y):
    dx, dy = x - BAND_O[0], y - BAND_O[1]
    return (dx * BAND_U[0] + dy * BAND_U[1], dx * BAND_N[0] + dy * BAND_N[1])


def band_ring():
    (t0, t1), (s0, s1) = BAND_T, BAND_S
    return [band_pt(t0, s0), band_pt(t1, s0), band_pt(t1, s1), band_pt(t0, s1)]


def in_band(x, y):
    t, s = band_ts(x, y)
    return BAND_T[0] <= t <= BAND_T[1] and s <= BAND_S[1]


def channel_lines():
    """[(point on the line, unit direction)] of every drain channel (V18g: square to B's facade; the satellite shows
    no line along the ramp mouth: the west channel passes 5 m west of it)."""
    return [(band_pt(t, 0.0), (-BAND_N[0], -BAND_N[1])) for t in CHANNEL_T]


def seat_blocks(route_distance, trees=(), clear_at=None):
    """[(x, y, bearing of the long side in degrees, moved)] of the seat blocks. A block of the real row that would
    stand within SEAT_CLEAR of the race centreline (the race line crosses the real row) is pushed straight
    south-south-east, across the row, until it is clear (V15b). V16 (3.0 m blocks): at each step across it may also
    slide up to 2.8 m along the row, so that it keeps 2.0 m from every trunk and lamp and 3.6 m from the other blocks."""
    ux, uy = _unit((0.0, 0.0), SEAT_STEP)
    px, py = uy, -ux                                   # across the row, towards the south-south-east (away from the road)
    if py > 0:
        px, py = -px, -py
    bearing = math.degrees(math.atan2(ux, uy)) % 180.0
    out = []

    def ok(x, y):
        need = SEAT_CLEAR if clear_at is None else clear_at(x, y) + SEAT_CLEAR - 6.6       # V18h: the curve plaza
        return (route_distance(x, y) >= need and inside(x, y, RING) and ring_distance(x, y, RING) >= 1.0
                and all(math.hypot(tx - x, ty - y) >= 2.0 for tx, ty in trees)
                and all(math.hypot(o[0] - x, o[1] - y) >= 3.6 for o in out))
    slides = [0.0] + [sign * 0.4 * j for j in range(1, 8) for sign in (1.0, -1.0)]
    for k in range(SEAT_COUNT):
        x0, y0 = SEAT_FIRST[0] + SEAT_STEP[0] * k, SEAT_FIRST[1] + SEAT_STEP[1] * k
        spot, moved = None, 0.0
        while spot is None and moved <= 20.0:
            for along in slides:
                x, y = x0 + px * moved + ux * along, y0 + py * moved + uy * along
                if ok(x, y):
                    spot = (round(x, 2), round(y, 2), bearing, moved)
                    break
            else:
                moved += 0.25
        if spot:
            out.append(spot)
    return out


# V18h: the final shapes (the helpers above are defined now)
J_BANK = bite(J_BANK_V18G, FAN_R)
J_LAWN = bite(J_LAWN_V18G, FAN_R + FAN_LAWN_GAP)
RING = _fan_ring(RING_V18G)
FAN_ARC = sorted(loop_polar(*p)[0] for p in J_BANK if abs(loop_polar(*p)[1] - FAN_R) < 0.02)
FAN_ARC = (FAN_ARC[0], FAN_ARC[-1])        # bearings between which the rim borders the lawn (about 288 .. 357)
