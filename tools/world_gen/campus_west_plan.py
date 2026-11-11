"""Campus west plan (V16c, 2026-11-11): the CTT terrace's west end, D's pool and head, B's head, the west and
south-west lawns as plain data. Read by env_campus_west.py (geometry, inside Blender), env_terrain.py (the berm, via
env_campus_west.raise_nodes) and campus_plan.py (ground paint, via add_paint). No Blender imports, and no import of
campus_plan (campus_plan imports this file at its end).

Source: photosphere survey V15 (atlas report, sections 3.2, 3.3, top-30 items 9, 16, 17, 18, Appendices B and C:
panoramas W03, W04, W05, W08, W27 and the orthophoto). Every shape is a hand-set outline in local metres (x east,
y north); colours are averaged hex values from the survey, a step paler where the game's warm light needs it.
No image data is copied.

The race line is not touched. Everything solid here keeps CLEAR (6.6 m) off the centreline; around the jump the
numbers are given in the jump frame Q(a, l): a = metres west of the kicker's lip along the flight, l = metres to the
left of it (south positive), so the corridor |l| < 6.6 stays empty for the whole flight.
"""
import math

F0, F1 = 11.2, 14.5
CLEAR = 6.6                      # m from the race centreline: nothing new of this package stands closer

# ----------------------------------------------------------------------------------------------------------------
# Terrace west end (survey Appendix B 2.6). Real: ramp block on the deck's southern 7.7 m (foot x 4.5, crest
# 16.45 m at x -1.5..-3.5, inclined white west face down to x -8.7), a long open stair north of it (x -5..-19) down
# to a pad, a brown louvred door in the stair's south cheek. The game jumps where the real void between ramp block
# and stair lies, so both move outwards: the ramp block to the south of the corridor, the stair to the north.
# ----------------------------------------------------------------------------------------------------------------
WEDGE = {"l0": CLEAR + 0.15, "l1": CLEAR + 7.95,  # 7.8 m wide (real 7.7); the route is not quite straight here
         "a_foot": -3.5, "a_crest": 1.5, "a_edge": 3.5,   # ramp 5 m long (real 6), flat crest 2 m (real 2)
         "crest_z": F1 + 1.75}                     # real 16.45 m; here 16.25
STAIR = {"l0": CLEAR + 0.6,                        # the landing road drifts 0.5 m north along the stair's run
         "clear": 4.2, "cheek": 0.3,               # 4.2 m between the cheeks (real 4-5)
         "a_land": -0.3, "a_top": 1.0, "run": 14.0, "steps": 22,
         "parapet": 0.45}                          # cheek top above the flight line; a 0.6 m handrail stands on it
DOOR = {"a0": 2.3, "a1": 3.9, "h": 2.2}            # louvred double door 1.6 x 2.2 m in the south cheek
VENT = {"a0": 4.5, "a1": 4.9, "z": 1.5}            # 0.4 m grille right of it (height of its foot above the ground)
# the jump frame as numbers (route.json jump: lip and the direction to the third point after it); env_campus_west
# stops the build if the route's own frame differs by more than 5 cm
JUMP_O, JUMP_DIR = (-0.28, 56.45), (-0.9798, -0.19996)


def Q(a, l):
    return (JUMP_O[0] + JUMP_DIR[0] * a - JUMP_DIR[1] * l, JUMP_O[1] + JUMP_DIR[1] * a + JUMP_DIR[0] * l)


# pad at the stair foot and the apron in front of the door wall (paint; real pad x -30.5..-19, y 51..59.5)
PAD_RING = [Q(8.8, -3.9), Q(27.0, -3.9), Q(27.0, -15.4), Q(15.6, -15.4), Q(15.6, -11.6), Q(8.8, -11.6)]
# the real pale path from the avenue pavement to the pad (2.8 m wide, heading 66): it crosses the lawn road at a
# flat angle, so only its two ends show beside the road (paint)
LAWN_PATH = [(-76.0, 39.5), (-20.0, 60.0)]
LAWN_PATH_W = 2.8
# stepping slabs across the south lawn toward D (0.6 x 1.2 m, pitch 1.1 m)
SLABS = {"from": (-56.5, 32.5), "to": (-41.0, 20.5), "pitch": 1.1, "size": (0.6, 1.2)}

# ----------------------------------------------------------------------------------------------------------------
# D's pool (survey Appendix C 2.2): five-sided basin, flush pale kerb, a 4 m arm east along D's south facade.
# The ring closes through the head building (no water there); KERB_EDGES = the ring edges that meet the lawn.
# ----------------------------------------------------------------------------------------------------------------
D_O, D_U = (-33.5, 12.0), (0.9326, 0.3611)        # = campus_plan.D_O, D_U


def D(s, t):
    """D's frame: s east along the bar, t south across it (as campus_plan.D)."""
    return (D_O[0] + D_U[0] * s + D_U[1] * t, D_O[1] + D_U[1] * s - D_U[0] * t)


D_SOUTH_T = 33.1                 # the upper storey's south face (1.5 m beyond the ground floor's glass at 31.6)
ARM_T = (33.4, 37.4)             # the pool's arm in front of it
ARM_S = (3.5, 15.4)              # ... from the prow to about x = -8
D_POOL = [(-53.5, 15.0), (-50.0, -21.0), (-34.0, -31.0), (-18.0, -22.0), D(ARM_S[0], ARM_T[1]),
          D(ARM_S[1], ARM_T[1]), D(ARM_S[1], ARM_T[0]), D(ARM_S[0], ARM_T[0]), (-19.5, -17.5), (-22.5, -12.5),
          (-36.5, 9.0), (-43.0, 16.8)]
KERB_EDGES = (11, 0, 1, 2, 3, 4, 5, 6)             # edge k = ring[k] -> ring[k + 1]
D_POOL_Z = 9.35                  # water level; the kerb's top lies 0.15 m above it, flush with the lawn
KERB_W = 0.4
# the head: frame box on the south-west face, inclined dark deck with three stone courses along the south-west and
# south faces, the prow with a slot and a small balcony, the dark glazed 'mouth' at the north-west tip
HEAD_SW = ((-48.1, -10.1), (-33.3, -22.9))         # = campus_plan.D_HEAD[4], [5]
HEAD_S = ((-33.3, -22.9), (-23.3, -16.6))          # [5], [6]: the blank 9 m wall
FRAME = {"t0": 0.19, "t1": 0.81, "z0": 12.0, "z1": 16.8, "depth": 2.0, "band": 0.6}   # 12.2 m wide opening
# (real: deck about 5 m and 5-9 m of water; the game's head stands 7 m from the basin's rim, so the deck is
# narrower here and 4-7 m of water stay in front of it)
HEAD_DECK = {"width": 1.8, "top": 10.9, "foot": 9.8, "tread": 0.35, "riser": 0.15, "courses": 3}

# ----------------------------------------------------------------------------------------------------------------
# B (survey Appendix B 2.8, 2.11; top-30 item 18)
# ----------------------------------------------------------------------------------------------------------------
SCREEN_TOP = 15.2                # top of the perforated brick screen along B's terrace side (0.7 m above Floor 1)
B_BOX = {"t0": 0.06, "t1": 0.94, "z0": 12.6, "z1": 19.4, "bays": 8, "rows": 2, "frame": 0.6, "proud": 0.45}

# ----------------------------------------------------------------------------------------------------------------
# South-west lawn (survey Appendix C 2.1, 2.3, 2.5)
# ----------------------------------------------------------------------------------------------------------------
# (the south-east corner stays 20 m off the beach tunnel that runs under the Marginal here)
BERM_FOOT = [(20.0, -30.0), (-14.0, -51.0), (-4.0, -75.0), (24.0, -75.0), (33.0, -66.0), (36.0, -58.0),
             (33.0, -50.0), (27.0, -40.0)]
BERM_TOP = [(10.0, -45.0), (-5.0, -53.0), (0.0, -71.0), (20.0, -70.0), (29.0, -63.0), (31.0, -55.0),
            (30.0, -48.0), (24.0, -41.0)]
BERM_Z = F0 - 0.05               # flat top = Floor 0 of hall E (the lawn lies at 9.2-9.9 m: about 1.5-2 m higher)
BERM_KERB = [(20.0, -30.0), (-14.0, -51.0), (-4.0, -75.0), (24.0, -75.0)]     # pale edging line at its foot (paint)
PATH = [(-72.0, -38.0), (-8.0, -80.0), (30.0, -78.0)]       # centre line of the stone-paved perimeter path
PATH_W = 4.5
WALL_H, WALL_T = 0.45, 0.4       # the continuous low wall on its outer side
# the real trees of the two lawns (survey: W08, W27, orthophoto), moved out of the road's clearance where needed:
# (x, y, prototype, scale)
TREES = [(-69.3, 53.6, "Veg_Olive", 0.62),          # old olive, real (-69, 52): 5.6 m from the centreline there
         (-27.0, 45.0, "Veg_PlazaTree", 0.62),      # young tree, real (-27, 48.5): 4 m from the centreline there
         (-60.5, -31.0, "Veg_Olive_B", 0.66)]       # the one old olive of the south-west lawn

# the two planted strips with low grasses along B's foot (B's frame: s along the bar, t = metres south of the facade
# line): clumps every 4 m, left out where they would stand on the stair, the pad or within 8 m of the race line
B_GRASS = {"s": (2.0, 50.0), "pitch": 4.0, "t": (-1.6, -8.0), "scale": (0.6, 0.85)}

# courtyards: rows of thin staked saplings parallel to the deck (frame of the CTT hall: u east along the deck)
DECK_U = (0.9882, 0.1529)        # the deck's long axis (heading 81)
SAPLING_SCALE = (0.62, 0.74)     # Veg_PlazaTree is 5 m at scale 1: 3.1-3.7 m
COURT_S_ROWS = {"origin": (13.0, 44.2), "rows": (0.0, -6.5), "count": 7, "pitch": 4.9}
COURT_N_ROWS = {"origin": (5.5, 70.5), "rows": (0.0, 6.0, 12.0), "count": 5, "pitch": 5.2}

# ----------------------------------------------------------------------------------------------------------------
# colours (RGB) of the ground paint
# ----------------------------------------------------------------------------------------------------------------
GRAVEL = (182, 156, 122)         # ochre stabilised gravel of the courtyards (survey #a2896b, a step paler)
PAD = (206, 194, 162)            # pale buff concrete of the pad (survey #ccbd98)
PALE_PATH = (214, 207, 184)      # the plain pale lawn path
STONE_PATH = (190, 178, 164)     # stone paving of the perimeter path (survey #a89a8e, a step paler)
KERB_LINE = (198, 186, 134)      # edging at the berm's foot (survey #c3b479)
LAWN = (143, 179, 122)           # = campus_plan.LAWN


def strip(line, width):
    """Closed ring around a polyline, `width` wide (mitred joints)."""
    left, right = [], []
    n = len(line)
    for k, p in enumerate(line):
        a, b = line[max(k - 1, 0)], line[min(k + 1, n - 1)]
        dirs = []
        for q0, q1 in ((a, p), (p, b)):
            d = math.hypot(q1[0] - q0[0], q1[1] - q0[1])
            if d > 1e-6:
                dirs.append(((q1[0] - q0[0]) / d, (q1[1] - q0[1]) / d))
        tx, ty = sum(d[0] for d in dirs), sum(d[1] for d in dirs)
        t = math.hypot(tx, ty) or 1.0
        tx, ty = tx / t, ty / t
        nx, ny = -ty, tx
        scale = 1.0 / max(0.5, dirs[0][0] * tx + dirs[0][1] * ty)          # mitre length
        h = width / 2 * scale
        left.append((p[0] + nx * h, p[1] + ny * h))
        right.append((p[0] - nx * h, p[1] - ny * h))
    return left + right[::-1]


def add_paint(paint, old_pool):
    """Ground colours of this package, appended to campus_plan.GROUND_PAINT (campus_plan calls this at its end).
    old_pool: the basin as drawn before V16c (lawn again where the new basin is smaller)."""
    paint("lawn over the former pool outline", old_pool, LAWN, lawn=255)
    paint("pale lawn path", strip(LAWN_PATH, LAWN_PATH_W), PALE_PATH)
    paint("pad at the terrace stair (V16c)", PAD_RING, PAD)
    paint("perimeter path", strip(PATH, PATH_W), STONE_PATH)
    paint("edging at the berm's foot", strip(BERM_KERB, 0.8), KERB_LINE)
