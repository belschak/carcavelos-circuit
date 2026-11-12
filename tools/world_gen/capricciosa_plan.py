"""Capricciosa plan (V16g, 2026-11-11): the hairpin forecourt, the two beach restaurants' sea side and the south-west
corner of the coastal car park as data. Source: photosphere survey V15 (section 3.7, items 15 and 27, Appendix G) and the
street survey V10e; positions measured there, nothing here is an image, a text or a brand.

Plain Python (no Blender). street_plan.py calls extend() at the end of its car-park section (paving layers and paint on
top of the older ones) and terrain_target() / terrain_zone() from its terrace functions; env_capricciosa.py and
env_beachfront.py read the constants. This module never imports street_plan at import time (street_plan imports it
while it is still being built).

Why the levels differ from the survey: the game's terrain is lower here than the real ground. Forecourt at the portal
5.9 m (real 6.8), restaurant terrace 4.25 m (real 5.0), car-park corner 3.9 m (real 5.8), sand 1.8 m (real about 1).
So the stair in the portal has the drop the game has (the race corridor's ground, 26 m either side of the centreline,
is fixed and reaches under the portal), the stair at the car-park corner has 6 risers instead of 16, and the sea wall
and the cliff are about 2.6 m and 1.3 m high instead of 3-3.5 m and 2-2.5 m.
"""
import math

S = None                       # street_plan, set by extend()

# =====================================================================================================
# Small geometry helpers (own copies: usable before street_plan is complete)
# =====================================================================================================
def unit(dx, dy):
    d = math.hypot(dx, dy) or 1.0
    return (dx / d, dy / d)


def inside(x, y, poly):
    hit = False
    for (x1, y1), (x2, y2) in zip(poly, poly[1:] + poly[:1]):
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            hit = not hit
    return hit


def along(line, x, y):
    """(arc position, signed distance to the left) of (x, y) at the nearest point of a polyline."""
    best = None
    acc = 0.0
    for a, b in zip(line, line[1:]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        l = math.hypot(dx, dy)
        t = max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / (l * l)))
        px, py = a[0] + dx * t, a[1] + dy * t
        d = math.hypot(x - px, y - py)
        side = (dx * (y - a[1]) - dy * (x - a[0])) / l
        if best is None or d < best[0]:
            best = (d, acc + l * t, side)
        acc += l
    return best[1], best[2]


def line_length(line):
    return sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(line, line[1:]))


def at_s(line, s):
    """(point, unit direction) at arc position s of a polyline."""
    acc = 0.0
    for a, b in zip(line, line[1:]):
        l = math.hypot(b[0] - a[0], b[1] - a[1])
        if s <= acc + l or (a, b) == (line[-2], line[-1]):
            t = (s - acc) / l
            return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t), unit(b[0] - a[0], b[1] - a[1])
        acc += l


def side_pt(line, s, d):
    """Point at arc position s, d metres to the RIGHT of the line's direction."""
    (x, y), u = at_s(line, s)
    return (x + u[1] * d, y - u[0] * d)


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


# =====================================================================================================
# 1. The paved island inside the widened hairpin (V14c: 12.5 m radius). Centre and radius fitted to route.json
#    points 244-257 (all within 1 cm of the circle).
# =====================================================================================================
LOOP_C = (-238.70, -31.86)
LOOP_R = 12.5
ISLAND_R = 8.15                 # the paving is clipped at street_plan.KEEP_OUT (4.95 m from the centreline = r 7.55)
ISLAND_EAST = ((-220.5, -34.0), (-220.5, -43.0))      # blunt east end: the lawn belt begins here
ISLAND_BAND = 1.15              # dark granite-sett band along the island's edge
BED_C = (-238.2, -31.9)         # V16g had a round planted bed with a palm here; V18 (team walk 7 Nov, Street View 2022/2026):
BED_R = 2.9                     # the real tip is plain calcada with a kerb and a stop sign, so no bed and no palm
STOP_SIGN = (-244.4, -31.9)     # V18: stop sign (round sign above) at the island's west tip, 6.8 m off the centreline
BIKE_DOCK = ((-231.2, -37.0), (-223.4, -41.0))        # generic racks on the apron beside the lawn (V18h: east of the bins)
BIN_PAD = ((-226.9, -41.0), (-222.1, -41.3))          # (V16g place of the bins; V18h: BIN_PADS)
# V18h (satellite 2026, Street View Dec 2022): there is no island in reality. Inside the loop lie the tip of the lawn
# between the Marginal and the car park (north-east of LAWN_EDGE) and the car park's concrete apron with the two pads
# of the underground bins (steel plates about 4.4 m x 2.6 m, long side parallel to the lawn's edge).
LAWN_EDGE = ((-245.3, -22.0), (-222.5, -39.1))        # the lawn lies to the left of this line (north-east)
BIN_PADS = ((-240.7, -32.7), (-235.9, -36.3))         # centres (the second 0.5 m nearer the loop's middle: prop clearance)
BIN_PAD_SIZE = (4.4, 2.6)
APRON_END = (-229.0, -34.2)     # on LAWN_EDGE: from here the lawn's edge turns south to the head of the north-east kerb
SCREEN_AT = (-212.0, -38.0)     # the advert screen's place: a blank dark screen box, nothing on it
SIGN_AT = (-216.6, -36.4)       # round sign over a triangular one (blank symbols as before)


def island_ring(r=ISLAND_R, inset=0.0):
    cx, cy = LOOP_C
    pts = [(cx + (r - inset) * math.cos(math.radians(a)), cy + (r - inset) * math.sin(math.radians(a)))
           for a in range(55, 306, 10)]
    (ax, ay), (bx, by) = ISLAND_EAST
    return pts + [(bx - inset, by + inset), (ax - inset, ay - inset)]


# =====================================================================================================
# 2. Entrance portal (survey Appendix G 2.2): a funnel-shaped white wedge in the hedge row. Hedge frame of
#    street_plan: t along the bay row (south-east), s behind the bay front.
# =====================================================================================================
PORTAL = dict(t_mid=20.5, mouth=5.1, back=3.8, s0=5.3, s1=9.3, front_h=3.4, back_h=0.85, wall=0.35,
              riser=0.16, tread=0.35, stair_s=5.9, stair_half=1.0)   # V18 (walk): the stair is 2 m wide, white cheeks beside it
HATCH = dict(t=(18.15, 22.85), s=(-0.2, 4.8), stripe=0.45)          # stripes straight across, zone about 5 x 5 m (V18: walk)
HEDGE_KERB = (0.25, 0.3)        # concrete upstand in front of the hedge: height, width
SIGN_WALL = dict(at=(-261.0, -45.0), length=3.0, height=1.2)        # blank white wall on the hedge line
DATE_PALM = (-262.0, -44.0)     # the broad low date-type palm south-east of the portal
PLANTER = dict(t=(15.6, 18.3), s=(10.6, 12.1), height=0.9)          # white slatted planter with an olive, in the court

# =====================================================================================================
# 3. Sea side of the restaurants. SEA_LINE is the terrace's sea edge (street_plan.TERRACE runs along it): the terrain
#    steps down here, the visible wall face stands SEA_FACE m further out, the wall's top is a stone walk over the step.
# =====================================================================================================
SEA_LINE = [(-310.2, -49.4), (-304.0, -56.0), (-274.0, -84.0)]      # north-west to south-east; the sea is on the right
SEA_FOOT_W = 13.0               # m: the sand in front of the wall is lowered over this width
SEA_FOOT_Z = 1.75
SEA_FACE = 1.7                  # m: wall face in front of the terrain step
SEA_BACK = 1.2                  # m: the stone walk reaches this far behind the step
SEA_TOP = 0.13                  # m: the walk lies this much above the terrace
SEA_LEDGE = (0.9, 0.5)          # concrete footing ledge: depth, height
SEA_TAPER = 6.0                 # m at both ends over which the lowered sand returns to the natural ground
PALM_ROW = dict(s0=23.5, step=4.0, n=6, back=1.0)                   # tall palms on the wall, m along the Capricciosa front
COVERED = dict(t=(1.0, 22.0), depth=8.6, post_step=4.2, rafter=1.05, eave=2.45, head=2.75)   # the covered terrace
OPEN_DECK = dict(t=(22.0, 36.2), depth=6.4)

# =====================================================================================================
# 4. South-west corner of the car park: stairs down to a concrete esplanade, a limestone cliff below it.
#    Corner frame: origin CORNER_A on the car park's south edge, q along it (east), d down the stair (south).
# =====================================================================================================
CORNER_A = (-248.5, -76.5)
CORNER_Q = unit(7.5, -1.5)
CORNER_D = (CORNER_Q[1], -CORNER_Q[0])
ESPL_Z = 3.0
STEP_D = 1.0                    # the terrain steps down this far south of the car park's edge (under walls and stair)
STAIR = dict(q=(2.0, 5.0), top=1.0, risers=6, tread=0.32)           # 3 m wide; a landing of 1 m, then the flight
PLINTH = dict(q=5.9, d=0.75, size=1.2, height=0.9)                  # stone plinth of the mast
CLIFF_Z = 1.8
CLIFF_W = 10.0
CLIFF_TAPER = 4.0
PARAPET_IN = 1.0                # the rubble parapet stands this far inside the cliff line
BOX_D = 2.0                     # retaining boxes (stone-clad wall, planted top) reach this far south of the terrain step


def corner_pt(q, d):
    return (CORNER_A[0] + CORNER_Q[0] * q + CORNER_D[0] * d, CORNER_A[1] + CORNER_Q[1] * q + CORNER_D[1] * d)


ESPL_N = [(-264.0, -84.0), corner_pt(0.0, STEP_D), corner_pt(6.9, STEP_D)]      # north edge, west to east
ESPL_S = [(-265.0, -91.0), corner_pt(6.9, STEP_D + 9.0)]                        # south edge = cliff line, west to east
ESPLANADE = ESPL_N + ESPL_S[::-1]
CLIFF_RUN = (2.6, None)         # the cliff along ESPL_S from here (east of the older beach steps) to its east end
SHOWER = corner_pt(1.0, 5.2)

# beach stair of the west car park (survey 2.8): between terracotta-red walls 1.2 m high, at about (-366, 12)
WEST_STAIR = dict(at=(-366.0, 12.0), heading=(-0.731, -0.682), width=3.0, wall=1.2, length=6.0)
WEST_PALMS = [(-352.0, 19.5), (-360.0, 23.6), (-368.0, 27.7), (-376.0, 31.8), (-384.0, 35.9)]   # fan palms 7-9 m


def _cliff_ring():
    total = line_length(ESPL_S)
    a, b = CLIFF_RUN[0], total
    return [side_pt(ESPL_S, a, 0.0), side_pt(ESPL_S, b, 0.0), side_pt(ESPL_S, b, CLIFF_W), side_pt(ESPL_S, a, CLIFF_W)]


def _sea_ring():
    total = line_length(SEA_LINE)
    stops = [0.0, line_length(SEA_LINE[:2]), total]
    return [side_pt(SEA_LINE, s, 0.0) for s in stops] + [side_pt(SEA_LINE, s, SEA_FOOT_W) for s in stops[::-1]]


SEA_FOOT = _sea_ring()
CLIFF_FOOT = _cliff_ring()
TERRAIN_RINGS = [SEA_FOOT, ESPLANADE, CLIFF_FOOT]       # env_streets adds their edges to the terrace lattice
TERRAIN_BOX = (-325.0, -105.0, -240.0, -40.0)


def terrain_zone(x, y):
    """Which of our lowered areas (x, y) lies in: 'sea', 'espl', 'cliff' or None."""
    if not (TERRAIN_BOX[0] < x < TERRAIN_BOX[2] and TERRAIN_BOX[1] < y < TERRAIN_BOX[3]):
        return None
    if inside(x, y, ESPLANADE):
        return "espl"
    if inside(x, y, SEA_FOOT):
        return "sea"
    if inside(x, y, CLIFF_FOOT):
        return "cliff"
    return None


def terrain_target(x, y):
    """('flat', z) like street_plan.terrace_target (the ground goes down to z, never up), or None."""
    zone = terrain_zone(x, y)
    if zone is None:
        return None
    if zone == "espl":
        return ("flat", ESPL_Z + 0.7 * smooth((-260.5 - x) / 4.0))        # the west end runs out into the old ground
    if zone == "sea":
        s, d = along(SEA_LINE, x, y)
        total = line_length(SEA_LINE)
        w = smooth(min(s, total - s) / SEA_TAPER)
        return ("flat", SEA_FOOT_Z + 0.45 * (abs(d) / SEA_FOOT_W) ** 2 + 3.2 * (1.0 - w))
    s, d = along(ESPL_S, x, y)
    total = line_length(ESPL_S)
    w = smooth(min(s - CLIFF_RUN[0], total - s) / CLIFF_TAPER)
    return ("flat", CLIFF_Z + 0.55 * (abs(d) / CLIFF_W) ** 2 + 2.0 * (1.0 - w))


def extend(sp):
    """Layers and paint of this plan, on top of street_plan's (called at the end of its car-park section)."""
    global S
    S = sp
    hp = S.hedge_pt
    # --- inside the hairpin (V18h): the car park's concrete runs through; the lawn's tip with a row of flush kerb
    #     stones along its edge; the two steel-plate pads of the underground bins ---
    head = S.kerb_pt(1.5, 0.0)
    S.layer("H apron", "slab", [(-241.5, -24.85), APRON_END, head, S.NE_KERB_A, (-232.0, -37.0), (-240.0, -31.0)], S.q_uv())
    S.kerb_stones("H apron kerb", [APRON_END, head], 0.0, 0.22)
    ring = island_ring()
    S.layer("H island", "slab", ring, S.q_uv())
    (ax, ay), (bx, by) = LAWN_EDGE
    lawn = S.clip_half(ring, (ax, ay), (bx, by))
    S.layer("H island lawn", None, lawn, ground="lawn")
    eu = unit(bx - ax, by - ay)
    on = sorted((q for q in lawn if abs((q[0] - ax) * eu[1] - (q[1] - ay) * eu[0]) < 1e-6),
                key=lambda q: (q[0] - ax) * eu[0] + (q[1] - ay) * eu[1])
    if len(on) >= 2:
        S.kerb_stones("H lawn kerb", [on[0], on[-1]], -0.22, 0.0)
    w, d = BIN_PAD_SIZE
    for c in BIN_PADS:
        S.frame_paint("gravel", S.rect(c, eu, -w / 2, w / 2, -d / 2, d / 2), 0.14)
        S.paint("steel_plate", S.rect(c, eu, -w / 2 + 0.14, w / 2 - 0.14, -d / 2 + 0.14, d / 2 - 0.14))
    # --- hatched pedestrian zone in front of the portal: stripes straight across the walking direction ---
    (t0, t1), (s0, s1), w = HATCH["t"], HATCH["s"], HATCH["stripe"]
    s = s0
    while s + w <= s1 + 1e-6:
        S.paint("white", [hp(t0, s), hp(t1, s), hp(t1, s + w), hp(t0, s + w)])
        s += 2 * w
    for t in (t0 - 0.15, t1 + 0.15):
        S.paint_ribbon("white", [hp(t, s0), hp(t, 0.0)], -0.06, 0.06)
    # --- the esplanade below the car park's south-west corner: pale concrete, a dark sett band, the shower's grating ---
    inner_s = [side_pt(ESPL_S, k, -PARAPET_IN - 0.2) for k in (0.0, line_length(ESPL_S) - BOX_D)]
    inner_n = [side_pt(ESPL_N, k, BOX_D - 0.05) for k in (0.0, line_length(ESPL_N[:2]) + 1.2, line_length(ESPL_N) - BOX_D)]
    ring = inner_n + inner_s[::-1]
    S.layer("H esplanade", "concrete", ring, S.frame_uv(CORNER_A, CORNER_Q, 4.0, 4.0))
    S.layer("H esplanade sett band", "setts", S.ribbon([corner_pt(-6.0, 3.6), corner_pt(-5.4, 8.6)], -0.25, 0.25))
    S.paint("setts_dark", S.rect(SHOWER, CORNER_Q, -1.25, 1.25, -2.0, 2.0))
    # --- the west car park (west of A Pastorinha): concrete with two sett bands, a zebra to the beach stair ---
    wu = unit(-41.0, 21.0)
    west = [(-348.0, 16.5), (-389.0, 37.5), (-392.0, 31.5), (-372.0, 12.0), (-362.0, 8.5), (-348.0, 11.0)]
    # V18h (satellite 2026): west of x -350 the car park is old dark paving like the square at the far end, not slabs
    S.layer("H west car park", "asphalt_worn", west, S.frame_uv((-348.0, 16.5), wu, 4.0, 4.0))
    for k in (10.0, 26.0):
        S.layer("H west sett band", "setts", S.ribbon([S.pt((-348.0, 16.5), wu, k, 0.3), S.pt((-348.0, 16.5), wu, k, 6.0)],
                                                      -0.3, 0.3))
    ws = WEST_STAIR
    S.stripes(ws["at"], ws["heading"], -5.5, -0.5, -1.4, 1.4, 0.5, 0.5, along=False)
