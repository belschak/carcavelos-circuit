"""Entrance plaza (V15a, 2026-11-11): the plan of the start plaza as plain data, read by env_entrance.py (geometry and
dressing), make_groundmap.py (ground colour under the paving) and make_entrance_textures.py.

Where: between Milestone J (north) and Cascais Hall B (south), from the avenue sidewalk to the white end wall of the
main building. Race start/finish: route i0-i15 runs east into the main building, the run-in i640-i662 comes down from
the north. World metres, x east, y north, origin lat 38.6780 / lon -9.3265.

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

# calcada of small pale cubes: survey #d1c9a5 in the sun; a step warmer and paler for our golden-hour light
CALCADA = "#D8CEAC"
CALCADA_RGB = (216, 206, 172)
CALCADA_TILE = 4.0         # m per texture tile (1024 px: stones of 12.5 x 19 cm)
ROW_DIR = (0.3746, -0.9272)   # the stone rows run along the drain channels (bearing 158 deg)

# ----------------------------------------------------------------------------------------------------------------
# Drain channels: thin dark-red gratings straight across the plaza, bearing about 158 deg (ortho: 155.5, 159, 157.6).
# Each: x where the line crosses y = 149.3 (measured on the ortho grid; +-1 m), bearing in degrees.
# The one at -57.2 was measured at -58.9: moved 1.7 m east so it passes the corner of the ramp portal.
# The one at -6.0 was measured at -5.4 (it runs along the pool's west rim): moved 0.6 m west, off the rim.
# The last one is the grating across the mouth of the car-park ramp, which runs on along the sidewalk.
# ----------------------------------------------------------------------------------------------------------------
CHANNEL_Y = 149.3
CHANNELS = [(-57.2, 158.0), (-39.4, 158.0), (-23.3, 158.0), (-6.0, 158.0)]
CHANNEL_W = 0.35           # m
DRAIN = "#7C3B2E"          # rust / oxblood red

# ----------------------------------------------------------------------------------------------------------------
# White concrete seat blocks: one long row on the ortho, 5.7 m apart, from (4.3, 137.5) to (-75.3, 107.1), i.e. on a
# line of bearing 69 deg (parallel to B). Each lies along the row.
# ----------------------------------------------------------------------------------------------------------------
SEAT_FIRST = (4.3, 137.5)
SEAT_STEP = (-5.686, -2.171)       # to the next block (west-south-west)
SEAT_COUNT = 15
SEAT_SIZE = (2.5, 0.8, 0.45)       # length, width, height (m)
SEAT_CLEAR = 6.5 + 1.45            # centre of a block from the race centreline: 6.5 m for its nearest corner
SEAT_WHITE = "#EDEAE0"

# ----------------------------------------------------------------------------------------------------------------
# Slim lamp posts in the tree rows (Street View: black poles of about 7 m with a small head, 15-20 m apart)
# ----------------------------------------------------------------------------------------------------------------
LAMPS = [(-74.2, 105.6), (-58.0, 113.2), (-41.1, 119.8), (-25.1, 125.9), (-10.1, 123.4), (1.8, 124.2)]
LAMP_H = 7.0
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
RAMP_PARAPET = 0.3         # m: the walls stand this much over the plaza (0 at the mouth, full from 3 m in)
RAMP_RAIL = 0.95           # m: steel handrail above the wall top
PORTAL_H = 2.75            # m: clear height of the black opening
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
    south-south-east, across the row, until it is clear; then along the row until it is 1.4 m from every tree."""
    ux, uy = _unit((0.0, 0.0), SEAT_STEP)
    px, py = uy, -ux                                   # across the row, towards the south-south-east (away from the road)
    if py > 0:
        px, py = -px, -py
    bearing = math.degrees(math.atan2(ux, uy)) % 180.0
    out = []
    for k in range(SEAT_COUNT):
        x, y = SEAT_FIRST[0] + SEAT_STEP[0] * k, SEAT_FIRST[1] + SEAT_STEP[1] * k
        moved = 0.0
        while route_distance(x, y) < SEAT_CLEAR and moved < 20.0:
            x, y, moved = x + px * 0.25, y + py * 0.25, moved + 0.25
        for _ in range(12):
            if all(math.hypot(tx - x, ty - y) >= 1.6 for tx, ty in trees):
                break
            x, y = x + ux * 0.4, y + uy * 0.4
        if route_distance(x, y) < SEAT_CLEAR or not inside(x, y, RING) or ring_distance(x, y, RING) < 1.0:
            continue
        out.append((round(x, 2), round(y, 2), bearing, moved))
    return out
