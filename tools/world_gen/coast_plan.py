"""Coast plan (V16e, 2026-11-11): the coast section as data, from the photosphere survey V15 (Appendix H, area B):
tunnel south forecourt, cove sea wall, berm between car park and promenade, the white prow and its terrace, the beach
bar, promenade details, the dry field east of the forecourt.

Plain Python (no Blender), shared by
  env_terrain.py      shape_heights(): the berm and the level terrace (one hook; the cove bowl is coast.py's)
  env_coast.py        builds the walls, slabs, buildings, road paint and the dressing from it
  env_beachfront.py   the cove wall's texture band, the two buildings handed over (bar, prow)
  make_groundmap.py   ground_layers(): forecourt concrete, straw field, rock tints
  make_coast_textures.py  the atlas layout (ATLAS, PALETTE)
Coordinates: local metres, x east, y north (origin lat 38.6780 / lon -9.3265), like route.json. Positions along the
race line are fractional route indices (2 m per point), offsets are metres from the centreline (+ = left of the
driving direction). Nothing here is an image, a sign text or a brand; colours are averaged hex values of the survey.
"""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROUTE = json.load(open(os.path.join(HERE, "route.json")))["points"]
N = len(ROUTE)
ROUTE_CLEAR = 6.6          # m: nothing that stands up closer than this to the race centreline (as street_plan.PROP_CLEAR)
TREE_CLEAR = 9.0           # m: trunks of trees
ROAD_HALF = 4.0


# =====================================================================================================
# Geometry helpers
# =====================================================================================================
def unit(dx, dy):
    d = math.hypot(dx, dy) or 1.0
    return (dx / d, dy / d)


def smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def seg_distance(x, y, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    l2 = dx * dx + dy * dy
    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / l2))
    return math.hypot(x - a[0] - t * dx, y - a[1] - t * dy)


_cells = {}


def _index():
    if not _cells:
        for i, p in enumerate(ROUTE):
            _cells.setdefault((int(p["x"] // 20), int(p["y"] // 20)), []).append(i)
    return _cells


def route_distance(x, y):
    """Metres from (x, y) to the race centreline (segments between the route points); 1e9 beyond about 40 m."""
    cells = _index()
    cx, cy = int(x // 20), int(y // 20)
    best = 1e9
    for dx in (-2, -1, 0, 1, 2):
        for dy in (-2, -1, 0, 1, 2):
            for i in cells.get((cx + dx, cy + dy), ()):
                a, b = ROUTE[i], ROUTE[(i + 1) % N]
                best = min(best, seg_distance(x, y, (a["x"], a["y"]), (b["x"], b["y"])))
    return best


def route_at(t):
    """((x, y), unit tangent, driving height) at the fractional route index t."""
    i = int(math.floor(t))
    a, b = ROUTE[i % N], ROUTE[(i + 1) % N]
    f = t - i
    return ((a["x"] + (b["x"] - a["x"]) * f, a["y"] + (b["y"] - a["y"]) * f), unit(b["x"] - a["x"], b["y"] - a["y"]),
            a["track_z"] + (b["track_z"] - a["track_z"]) * f)


def route_side(t, d):
    """Point d metres to the left (d < 0: right) of the centreline at the fractional route index t, moved out along
    the local normal until it really is |d| from the whole centreline (inside a bend the plain normal comes closer)."""
    (x, y), u, _z = route_at(t)
    n = (-u[1], u[0]) if d >= 0 else (u[1], -u[0])
    r = abs(d)
    q = (x + n[0] * r, y + n[1] * r)
    for _ in range(8):
        short = abs(d) - route_distance(*q)
        if short <= 0.005:
            break
        r += short + 0.01
        q = (x + n[0] * r, y + n[1] * r)
    return q


def route_line(t0, t1, d, step=2.0):
    n = max(1, int(round((t1 - t0) * 2.0 / step)))
    return [route_side(t0 + (t1 - t0) * k / n, d) for k in range(n + 1)]


def inside(x, y, poly):
    hit = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            hit = not hit
    return hit


def line_frame(x, y, line):
    """(distance, signed side: > 0 left of the polyline, arc length of the nearest point) for a polyline."""
    best, side, arc, acc = 1e18, 1.0, 0.0, 0.0
    for a, b in zip(line, line[1:]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        l2 = dx * dx + dy * dy
        t = max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / l2))
        d = math.hypot(x - a[0] - t * dx, y - a[1] - t * dy)
        if d < best:
            best, arc = d, acc + t * math.sqrt(l2)
            side = 1.0 if dx * (y - a[1]) - dy * (x - a[0]) >= 0 else -1.0
        acc += math.sqrt(l2)
    return best, side, arc


def polyline_points(line, step):
    """Points every `step` metres along a polyline: [(x, y, unit tangent, arc length)], both ends included."""
    out, acc = [], 0.0
    for a, b in zip(line, line[1:]):
        seg = math.hypot(b[0] - a[0], b[1] - a[1])
        u = unit(b[0] - a[0], b[1] - a[1])
        n = max(1, int(round(seg / step)))
        for k in range(n):
            out.append((a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n, u, acc + seg * k / n))
        acc += seg
    a, b = line[-2], line[-1]
    out.append((b[0], b[1], unit(b[0] - a[0], b[1] - a[1]), acc))
    return out


# =====================================================================================================
# Terrain (env_terrain.apply hook): the berm between car park and promenade, the level terrace west of the cove.
# Both are land outside the beach zone. Near the race line nothing changes: the rise fades out between TERRAIN_FAR and
# TERRAIN_NEAR metres from the centreline, and the corridor's own shoulder blends the rest (env_corridor).
# =====================================================================================================
TERRAIN_NEAR, TERRAIN_FAR = 10.0, 16.0
# V18g (Google satellite 2026, DGT orthophoto, team walk 7 Nov): there is no ridge between the car park and the
# promenade. The upper road and its palm car park (UPPER, traced on the orthophoto) are one level that climbs from the
# asphalt square to the tunnel plaza; a grass bank falls from its south edge to the lower road and the lawn. V16e had
# a 2.7 m ridge along y -128, on top of the real lower road. BERM["line"] is now the bank's top edge (west to east).
UPPER = [(-139.5, -108.4), (-99.5, -111.8), (-72.3, -113.6), (-57.0, -115.0), (-57.0, -124.6), (-58.6, -130.0),
         (-59.5, -134.1), (-84.5, -135.5), (-86.8, -134.5), (-97.7, -122.7), (-100.5, -121.4), (-138.2, -120.5),
         (-139.5, -114.0)]
UPPER_LEVEL = ((-138.0, 4.3), (-60.0, 4.8))               # (x, height): the level climbs eastwards
BANK = 5.0                                                # m: the bank's width in plan (one terrain cell)
BERM = dict(line=[(-138.2, -120.5), (-100.5, -121.4), (-97.7, -122.7), (-86.8, -134.5), (-84.5, -135.5), (-59.5, -134.1)])
# Survey 2.7: beige concrete terrace about 25 x 20 m west of the cove, level with the promenade (the road beside it)
TERRACE_LEVEL = 2.36
TERRACE = [route_side(t, -7.3) for t in (341.0, 343.0, 345.0, 347.0, 349.0, 351.0)] + \
          [(-103.6, -166.5), (-105.2, -172.0), (-107.2, -180.3), (-117.0, -182.0), (-129.5, -179.0), (-131.0, -166.0)]


def ring_distance(x, y, ring):
    return min(seg_distance(x, y, p, q) for p, q in zip(ring, ring[1:] + ring[:1]))


def upper_level(x):
    (xa, za), (xb, zb) = UPPER_LEVEL
    return za + (zb - za) * min(max((x - xa) / (xb - xa), 0.0), 1.0)


def upper_share(x, y):
    """1 on the upper level, falling to 0 over BANK metres outside its outline."""
    if not (-146.0 <= x <= -50.0 and -142.0 <= y <= -102.0):
        return 0.0
    if inside(x, y, UPPER):
        return 1.0
    return smooth(1.0 - ring_distance(x, y, UPPER) / BANK)


def berm_rise(x, y):
    """> 0 on the grass bank south of the upper level (0 on the level itself and away from it)."""
    if inside(x, y, UPPER):
        return 0.0
    d, side, _arc = line_frame(x, y, BERM["line"])
    return upper_share(x, y) if side < 0 and d < BANK else 0.0


def terrain_weight(x, y):
    return smooth((route_distance(x, y) - TERRAIN_NEAR) / (TERRAIN_FAR - TERRAIN_NEAR))


def shape_heights(heights, x0, y0, step):
    """Moves the 5 m terrain nodes of the upper level (V18g) and levels those under the terrace. Returns the number changed."""
    changed = 0
    for j, row in enumerate(heights):
        y = y0 + j * step
        if not -190.0 <= y <= -102.0:
            continue
        for i in range(len(row)):
            x = x0 + i * step
            if not -145.0 <= x <= -55.0:
                continue
            w = terrain_weight(x, y)
            if w <= 0.0:
                continue
            share = upper_share(x, y) * w
            if share > 0.01:
                row[i] += (upper_level(x) - row[i]) * share
                changed += 1
            elif inside(x, y, TERRACE):
                row[i] = row[i] + (TERRACE_LEVEL - row[i]) * smooth((route_distance(x, y) - 7.0) / 4.0)
                changed += 1
    return changed


def in_changed_terrain(x, y):
    """True where this plan moves the ground (the dressing there is seated again on the new ground)."""
    return upper_share(x, y) > 0.02 or inside(x, y, TERRACE)


# =====================================================================================================
# Texture atlas (make_coast_textures.py -> textures/coast_atlas_albedo.png, one material W_coast_atlas).
# Horizontal bands that tile along u only; a face maps v inside its band. First band = flat colours (palette).
# band: (name, height in pixels, metres per tile along u, metres over the band's height)
# =====================================================================================================
ATLAS_SIZE = 1024
BANDS = [("pal", 64, 1.0, 1.0), ("stone", 160, 12.8, 3.0), ("rubble", 128, 9.6, 2.4), ("broom", 128, 16.0, 4.0),
         ("tiles", 96, 6.4, 4.0), ("boards", 96, 6.4, 3.0), ("oldwall", 96, 9.6, 1.3), ("render", 96, 8.0, 4.0),
         ("deck", 96, 6.4, 2.0), ("hedge", 64, 6.4, 3.0)]
PALETTE = {  # flat colours, one 64 px cell each
    "white": "#EDE9E0", "cream": "#DCCFB4", "beige": "#CDB894", "galv": "#A9A69B", "anthracite": "#3A3D42",
    "grey_door": "#8A8D90", "terracotta": "#B8705C", "fascia": "#C98A52", "straw": "#B6A277", "riprap": "#E6E2D6",
    "timber_white": "#E8E4DA", "stone_dark": "#6F675C", "steel_dark": "#5B5F66", "canvas": "#F3EFE5",
    "shade": "#B9B2A3", "glass": "#2E3A46",
}
ATLAS = {}
_row = 0
for _name, _h, _um, _vm in BANDS:
    ATLAS[_name] = dict(row=_row, px=_h, u_m=_um, v_m=_vm,
                        v0=1.0 - (_row + _h - 3) / ATLAS_SIZE, v1=1.0 - (_row + 3) / ATLAS_SIZE)   # 3 px gutter
    _row += _h
assert _row == ATLAS_SIZE


def band_uv(band, u_m, v_m):
    """Atlas UV for a point u_m metres along and v_m metres up a band (v clamped into the band)."""
    b = ATLAS[band]
    return (u_m / b["u_m"], b["v0"] + (b["v1"] - b["v0"]) * min(max(v_m / b["v_m"], 0.0), 1.0))


def pal_uv(colour):
    k = list(PALETTE).index(colour)
    b = ATLAS["pal"]
    return ((k + 0.5) / 16.0, (b["v0"] + b["v1"]) / 2)


# =====================================================================================================
# Tunnel south forecourt (survey 2.8, item 13). The road is in a cutting here; route index 398 = where the wall
# starts, PORTAL_S = the first point inside the tunnel.
# =====================================================================================================
PORTAL_S = next(i for i, p in enumerate(ROUTE) if p["tunnel"])
PORTAL_N = max(i for i, p in enumerate(ROUTE) if p["tunnel"])
FORE = dict(first=392.0, wall=(398.0, PORTAL_S + 0.0), wall_offset=6.85, wall_h=(0.55, 3.0), wall_thick=0.3,
            bench=(396.5, PORTAL_S - 6.5), bench_offset=-7.1, bench_size=(0.5, 0.5), bench_len=3.0, bench_gap=0.06,
            bollards=[(402.0, -7.4), (403.0, -8.6), (404.0, -9.8)],
            paint_half=12.0)                              # concrete up to the wall (left) and over the upper walkway (right)
# V18g: the old wall follows the field's kerb (orthophoto, walk video 12:10-12:50): up the lane's east side, round the
# plaza's south-east corner and along the east road
FIELD_WALL = [(4.9, -186.0), (4.9, -150.0), (6.4, -142.6), (9.0, -137.2), (14.5, -132.7), (22.0, -130.9), (38.0, -130.9),
              (132.0, -130.7)]
FIELD_WALL_SIZE = (1.5, 0.45)

# =====================================================================================================
# Cove (survey 2.6, item 12): the wall itself follows coast.COVE_KERB (env_beachfront), heights from coast.cove_drop
# =====================================================================================================
COVE_RAIL = dict(height=1.0, post_step=2.0)

# =====================================================================================================
# Beach bar (survey 2.4) and prow (survey 2.7), item 26: OSM footprints w482697584 and w213240239 (Fonte dos Ingleses)
# =====================================================================================================
BAR_ID, PROW_ID = "w482697584", "w213240239"
BAR = dict(walls=2.7, eave=0.5, pitch=0.32, west_cut=1.3, deck_depth=5.6, deck_cut=0.7, rail=1.0, stair_w=2.6, riser=0.16, tread=0.35)
PROW = dict(centre=(-110.2, -180.6), radius=5.0, bow_dir=(-0.26, -0.966), base=1.25, tip=3.7,
            hut=(-108.6, -174.6), hut_size=(3.6, 3.6, 2.6))

# =====================================================================================================
# Promenade details (survey 2.2, 2.3), item 9 and the berm's dressing (item 19)
# =====================================================================================================
TOTEM = dict(at=(-99.0, -146.9), bearing=100.0, size=(0.9, 0.25, 2.3))
FOUNTAIN = (-97.2, -147.3)
TWIN_BINS = (-95.6, -147.6)
RIPRAP = [(-85.6, -137.6), (-86.6, -133.8), (-88.2, -130.4), (-90.3, -127.6)]       # white boulder chute up the berm
HEDGE = dict(line=[(-12.5, -145.5), (-12.0, -153.0), (-12.8, -161.0)],
             height=3.4, depth=7.0)      # V18g: one dense bush mass west of the lane's parking (was a 60 m L-shaped hedge)
# flush paint on the road (into Track_EdgeLines, no collider): (fractional route index, length along the road, colour)
ROAD_ACROSS = [(323.0, 0.35, "graphite"),                       # cast-iron slot drain at the promenade entrance
               (364.3, 0.14, "white"), (364.95, 0.14, "white"), (365.6, 0.14, "white"),   # three white-nosed steps
               (PORTAL_S + 0.15, 0.24, "graphite")]             # slot drain across the tunnel's south mouth
ROAD_ARCS = None        # the concentric white kerb arcs: left out (on this road they run almost along the driving
                        # direction and read as lane lines; dict(centre=(x, y), radii=(...), width=0.3) draws them)
TUNNEL_DRAIN = dict(offset=-1.35, width=0.16)                                       # one lengthwise slot drain
PAINT_HALF = 3.5                                                # m: paint stays inside the road's edge lines

# dressing added by env_coast.dress (prototype, x, y, bearing or None, scale)
PALM_CLUSTER = [(-86.5, -121.8, 0.92), (-80.4, -124.6, 1.02), (-74.6, -121.0, 0.84), (-68.9, -124.2, 0.96),
                (-64.0, -119.6, 0.88), (-79.0, -118.2, 0.78)]
UMBRELLA_TREES = [(-109.0, -134.6, 0.56), (-96.6, -139.6, 0.60)]
WASHINGTONIAS = [(312.0, 11.0), (316.0, 10.4), (320.0, 11.2)]      # route index, offset (north side of the lane)
BERM_SHRUBS = 14
# V18g (orthophoto / Google satellite): parked cars. (x, y, bearing of the car's long axis in degrees, prototype)
CREST_CARS = [  # the upper road's north edge and the palm car park
    (-124.0, -112.4, 5, "Prop_CarB"), (-118.0, -112.9, 5, "Prop_CarA"), (-112.0, -113.4, 5, "Prop_CarH"),
    (-106.0, -113.9, 5, "Prop_CarD"), (-91.8, -115.2, 4, "Prop_CarG"), (-85.0, -115.8, 4, "Prop_CarC"),
    (-79.5, -116.3, 4, "Prop_CarE"), (-88.8, -126.2, 0, "Prop_CarA"), (-77.0, -128.2, 0, "Prop_CarF"),
    (-70.0, -128.6, 0, "Prop_CarG"),
    # the plaza's south-west edge and the lane's west side
    (-9.2, -133.4, 56, "Prop_CarC"), (-7.4, -136.4, 56, "Prop_CarA"), (-6.0, -139.6, 64, "Prop_CarH"),
    (-5.4, -142.9, 78, "Prop_CarB"),
    (-6.4, -146.4, 90, "Prop_CarD"), (-6.4, -149.1, 90, "Prop_CarG"), (-6.4, -151.8, 90, "Prop_CarA"),
    (-6.4, -154.5, 90, "Prop_CarE"), (-6.4, -159.9, 90, "Prop_CarF"), (-6.4, -162.6, 90, "Prop_CarC"),
    (-6.4, -165.3, 90, "Prop_CarB"), (-6.4, -170.7, 90, "Prop_CarH"), (-6.4, -173.4, 90, "Prop_CarA"),
    (-6.4, -178.8, 90, "Prop_CarD"),
] + [(x, -119.7, 0, p) for x, p in zip(      # the bay row along the east road
    (52.8, 55.5, 61.0, 63.7, 66.4, 69.1, 71.8, 74.5, 82.8, 88.9, 91.6, 105.6, 113.3, 118.9, 121.6),
    ("Prop_CarA", "Prop_CarD", "Prop_CarG", "Prop_CarB", "Prop_CarH", "Prop_CarC", "Prop_CarA", "Prop_CarE", "Prop_CarF",
     "Prop_CarG", "Prop_CarD", "Prop_CarA", "Prop_CarC", "Prop_CarH", "Prop_CarB"))]
# V18g (walk video 7:36, orthophoto): open frame (pergola) on the upper path's north side; low timber posts along the
# path's north edge between the seat blocks: (first route index, last, step in indices, offset)
PERGOLA = dict(at=(-57.5, -112.2), bearing=96.0, size=(4.6, 3.0, 2.5), post=0.14)
PATH_POSTS = (386.5, 402.5, 1.5, 6.9)
PATH_POST_SKIP = [(-18.9, -116.6), (-11.8, -116.9)]         # V18's seat blocks on the north side stand here
# young trees in the paving along the east road's south kerb (orthophoto: blobs with a 2 m shadow, about 10 m apart)
PLAZA_SAPLINGS = [(27.8, -127.4), (38.9, -127.4), (59.8, -127.6), (69.4, -127.6), (79.4, -127.7), (94.2, -127.8),
                  (105.0, -127.8), (114.4, -127.8)]
# the dense round trees on the lawn between the bar and the plaza (Google satellite, walk video 7:15)
LAWN_TREES = [(-27.0, -132.8, "Veg_Shrub", 3.4), (-22.0, -133.4, "Veg_Shrub_B", 3.0), (-18.2, -135.0, "Veg_Olive_B", 1.25),
              (-25.5, -137.4, "Veg_Shrub_B", 3.6), (-20.5, -140.6, "Veg_Shrub", 3.2), (-26.0, -143.0, "Veg_Olive", 1.3),
              (-14.0, -138.6, "Veg_Shrub_B", 3.3)]
DROP = [  # frozen dressing that stands where something of this plan now is: (x0, y0, x1, y1, prototype prefixes)
    (-101.5, -150.5, -97.0, -148.0, ("Prop_TroughBloom",)),
]

# =====================================================================================================
# Ground paint (make_groundmap.py hook): colour RGB, lawn mask value (255 = lawn with grass tufts), opacity
# =====================================================================================================
def ground_paint():
    f = FORE
    fore = route_line(f["first"], PORTAL_S + 1.0, f["wall_offset"]) + route_line(f["first"], PORTAL_S + 1.0, -f["paint_half"])[::-1]
    field_e = [(100.0, -130.2), (172.0, -129.0), (172.0, -143.0), (135.0, -143.0), (135.0, -150.0), (100.0, -150.0)]
    field = [(22.0, -130.6), (60.0, -130.4), (100.0, -130.2), (100.0, -262.0), (2.0, -262.0), (-5.0, -220.0),
             (4.8, -150.0), (6.3, -142.6), (8.9, -137.1), (14.4, -132.6)]
    conc, dry, grass = (206, 190, 158), (163, 163, 116), (143, 179, 122)
    # V18g, traced on the orthophoto (3 px/m) and checked on the Google satellite view; drawn in this order
    slope = [(-139.5, -106.0), (-120.0, -99.0), (-100.0, -97.0), (-66.0, -96.0), (-39.0, -101.5), (-8.0, -112.5), (-3.9, -116.7),
             (-7.8, -118.3), (-25.0, -117.3), (-57.0, -115.0), (-72.3, -113.6), (-99.5, -111.8), (-139.5, -108.4)]
    path = [(-57.0, -115.0), (-25.0, -117.3), (-7.8, -118.3), (-3.9, -116.7), (0.0, -122.2), (46.4, -122.0), (46.4, -117.2),
            (135.0, -117.2), (135.0, -130.2), (22.0, -130.2), (14.4, -132.2), (8.9, -136.7), (6.1, -142.2), (5.0, -146.0),
            (-9.0, -146.0), (-9.0, -140.0), (-12.5, -129.5), (-13.0, -126.1), (-25.0, -126.3), (-55.5, -124.5)]
    wedge = [(-139.0, -121.2), (-100.5, -121.6), (-97.7, -122.9), (-85.2, -135.6), (-86.6, -142.5), (-101.0, -129.6),
             (-138.2, -126.5), (-139.6, -124.0)]
    lower = [(-139.5, -126.5), (-101.0, -129.6), (-86.8, -142.8), (-90.5, -147.3), (-102.3, -135.9), (-135.9, -133.6),
             (-139.5, -132.3)]
    setts = [(-86.8, -142.9), (-69.5, -142.6), (-71.0, -147.5), (-80.0, -152.0), (-94.5, -160.0), (-100.5, -154.5)]
    import coast
    ring_in = [tuple(p) for p in coast.C["cove_kerb"]]                 # the cove ring: 8.5 m of concrete behind the sea wall
    cc = (-68.0, -188.0)
    ring_out = [(p[0] + unit(p[0] - cc[0], p[1] - cc[1])[0] * 8.5, p[1] + unit(p[0] - cc[0], p[1] - cc[1])[1] * 8.5)
                for p in ring_in]
    prom = [(-126.4, -156.6), (-147.7, -150.0), (-167.7, -133.3), (-177.4, -127.7), (-184.1, -125.4), (-220.4, -102.1),
            (-239.9, -91.4)]                                           # the promenade's sea edge (env_beachfront's kerb line)
    prom_in = [(x + 0.742 * 5.6, y + 0.670 * 5.6) for x, y in prom]
    pale = (214, 207, 192)                                             # beside the sand: paler and greyer, so it reads
    out = [dict(name="promenade concrete", ring=prom + prom_in[::-1], colour=pale, lawn=0, alpha=1.0),
           dict(name="cove ring concrete", ring=ring_in + ring_out[::-1], colour=pale, lawn=0, alpha=1.0),
           dict(name="dry slope below the Marginal", ring=slope, colour=dry, lawn=120, alpha=0.85),
           dict(name="upper road and palm car park", ring=UPPER, colour=conc, lawn=0, alpha=1.0),
           dict(name="lawn wedge", ring=wedge, colour=grass, lawn=255, alpha=1.0),
           dict(name="lower road", ring=lower, colour=conc, lawn=0, alpha=1.0),
           dict(name="sett paving at the cove", ring=setts, colour=(128, 118, 116), lawn=0, alpha=1.0),
           dict(name="upper path, tunnel plaza, east road", ring=path, colour=conc, lawn=0, alpha=1.0),
           dict(name="lane with its parking strip", ring=[(-9.0, -146.0), (5.0, -146.0), (4.8, -186.0), (-9.0, -186.0)],
                colour=(85, 88, 94), lawn=0, alpha=1.0),
           dict(name="forecourt concrete", ring=fore, colour=(204, 187, 152), lawn=0, alpha=1.0),
           dict(name="dry field", ring=field, colour=(154, 150, 106), lawn=70, alpha=1.0),
           dict(name="dry field east", ring=field_e, colour=(154, 150, 106), lawn=70, alpha=1.0),
           dict(name="terrace concrete", ring=TERRACE, colour=(208, 192, 162), lawn=0, alpha=1.0)]
    for r in coast.ROCKS:                                  # rock tints: the ground map paints every shelf ochre
        if r.get("tint"):
            c = tuple(int(r["tint"][k:k + 2], 16) for k in (1, 3, 5))
            out.append(dict(name="rock tint " + r["name"], ring=[tuple(p) for p in r["ring"]], colour=c, lawn=None,
                            alpha=r.get("tint_alpha", 0.6)))
    return out


def ground_layers(size, to_px, lawn_mask=False):
    """RGBA image of this plan's ground paint (PIL), drawn with the map's own to_px at the given pixel size."""
    from PIL import Image, ImageDraw
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    for lay in ground_paint():
        if lawn_mask and lay["lawn"] is None:
            continue
        c = (lay["lawn"],) * 3 if lawn_mask else lay["colour"]
        one = Image.new("RGBA", size, (0, 0, 0, 0))
        ImageDraw.Draw(one).polygon([to_px(x, y) for x, y in lay["ring"]], fill=tuple(c) + (int(255 * lay["alpha"]),))
        img = Image.alpha_composite(img, one)
    return img
