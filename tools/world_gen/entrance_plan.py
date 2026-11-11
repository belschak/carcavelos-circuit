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
RING = [(-90.0, 102.0), (-64.0, 98.0), (15.0, 128.1), (16.91, 128.83), (8.93, 149.17), (7.06, 148.43), (2.8, 159.8),
        (1.5, 163.5), (-30.0, 159.0), (-38.0, 147.5), (-90.0, 147.5)]
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
CHANNEL_Y = 149.3
CHANNELS = [(-55.5, 165.0), (-40.5, 165.0), (-26.0, 165.0), (-10.7, 165.0)]
CHANNEL_ACROSS_PLANKS = 2  # index of the channel that continues across the plank band
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
J_PATH_COLOUR = "lamp_sand"

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
RAMP_NW, RAMP_NE = (-82.37, 130.36), (-55.47, 135.16)  # north edge: mouth -> portal
RAMP_SW, RAMP_SE = (-79.17, 116.86), (-52.67, 126.06)  # south edge: mouth -> portal
RAMP_DROP = 3.3            # m: floor at the portal below the plaza beside it (survey 3.2 +-0.5)
RAMP_WALL = 0.35           # m: thickness of the concrete side walls
RAMP_PARAPET = 1.1         # m: V16 (N06_z_ramp_h100, N14_h270): solid concrete parapets 1.1 m over the plaza, no railing
                           # (0 at the mouth, full from 3 m in; V15a: 0.3 m with a steel handrail)
RAMP_FASCIA = 0.55         # m: dark fascia band over the whole portal (N15_z_ramp_h084: 0.9 m under a 0.5 m white slab
                           # edge; ours is 3.3 m deep, so 0.55 m under 0.25 m of slab and the white parapet)
PORTAL_H = 2.5             # m: clear height of the black opening (V16: 2.5 m from the avenue, ST02; was 2.75)
CONCRETE = "#D6CFBA"       # pale warm concrete (survey #8c8b6a in mixed light)


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
    """[(point on the line, unit direction)] of every drain channel."""
    out = []
    for x, bearing in CHANNELS:
        a = math.radians(bearing)
        out.append(((x, CHANNEL_Y), (math.sin(a), math.cos(a))))
    mid, bearing = mouth_channel()
    a = math.radians(bearing)
    out.append((mid, (math.sin(a), math.cos(a))))
    return out


def seat_blocks(route_distance, trees=()):
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
        return (route_distance(x, y) >= SEAT_CLEAR and inside(x, y, RING) and ring_distance(x, y, RING) >= 1.0
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
