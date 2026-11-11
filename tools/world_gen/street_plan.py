"""Street plan (V10f, 2026-11-11): the street section of the route as data, sections A-F of the Street View survey
(atlas report V10e): avenue N6-7, roundabout, Avenida Marginal, the hairpin forecourt of the two beach restaurants and
the coastal car park.

Plain Python (no Blender), shared by
  env_streets.py      builds the paving, markings, kerbs, walls, props and the sunken restaurant terrace from it
  env_track.py        visual surface of the road and its verges (colour only, the route data stay as they are)
  make_groundmap.py   paints the same shapes into the ground colour map and the lawn mask (grass tufts follow)
Coordinates: local metres, x east, y north (origin lat 38.6780 / lon -9.3265), like route.json.

Sources: positions and sizes measured by the survey on the DGT orthophoto and triangulated from Street View panoramas;
nothing here is an image, a traced photo, a sign text or a brand. Accuracy about 1-2 m (see the survey).

LAYERS   flat paving, in drawing order: a later layer covers an earlier one (mat None = a hole that shows the terrain,
         i.e. lawn). Each layer: name, mat, ring, uv = (origin, u direction, tile along u, tile across) or None.
PAINT    road markings on top of the paving: colour (palette name), ring.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROUTE = json.load(open(os.path.join(HERE, "route.json")))["points"]

ROAD_HALF = 4.0
KEEP_OUT = 4.95            # m from the route centreline: road (4.0) + its shoulder (0.9) + a hair; no paving or paint inside
PROP_CLEAR = 6.6           # m: nothing that stands up (props, raised kerbs, walls) closer than this to the centreline
CAR_CLEAR = 9.0            # m: parked cars (centre) keep this far from the centreline


# =====================================================================================================
# Geometry helpers
# =====================================================================================================
def unit(dx, dy):
    d = math.hypot(dx, dy) or 1.0
    return (dx / d, dy / d)


def bearing(deg):
    """Unit vector for a compass bearing (0 = north, 90 = east)."""
    a = math.radians(deg)
    return (math.sin(a), math.cos(a))


def left_of(u):
    return (-u[1], u[0])


def pt(origin, u, t, s=0.0):
    """origin + u * t + left_of(u) * s."""
    return (origin[0] + u[0] * t - u[1] * s, origin[1] + u[1] * t + u[0] * s)


def rect(origin, u, t0, t1, s0, s1):
    """Rectangle in the frame (origin, u): t along u, s to the left of u."""
    return [pt(origin, u, t0, s0), pt(origin, u, t1, s0), pt(origin, u, t1, s1), pt(origin, u, t0, s1)]


def line_length(line):
    return sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(line, line[1:]))


def offset(line, d):
    """Polyline moved d metres to its left (mitred corners)."""
    out = []
    n = len(line)
    for k in range(n):
        a, b = line[max(k - 1, 0)], line[min(k + 1, n - 1)]
        if k == 0:
            b = line[1]
            a = line[0]
        elif k == n - 1:
            a, b = line[n - 2], line[n - 1]
        if 0 < k < n - 1:
            u0, u1 = unit(line[k][0] - a[0], line[k][1] - a[1]), unit(b[0] - line[k][0], b[1] - line[k][1])
            n0, n1 = left_of(u0), left_of(u1)
            m = unit(n0[0] + n1[0], n0[1] + n1[1])
            cos = max(m[0] * n1[0] + m[1] * n1[1], 0.3)
            out.append((line[k][0] + m[0] * d / cos, line[k][1] + m[1] * d / cos))
        else:
            nrm = left_of(unit(b[0] - a[0], b[1] - a[1]))
            out.append((line[k][0] + nrm[0] * d, line[k][1] + nrm[1] * d))
    return out


def ribbon(line, d0, d1):
    """Polygon between the offsets d0 and d1 (to the left) of a polyline."""
    return offset(line, d0) + offset(line, d1)[::-1]


def at_s(line, s):
    """(point, unit tangent) at arc length s along a polyline (clamped, extrapolated at the ends)."""
    acc = 0.0
    for a, b in zip(line, line[1:]):
        seg = math.hypot(b[0] - a[0], b[1] - a[1])
        if s <= acc + seg or (a, b) == (line[-2], line[-1]):
            u = unit(b[0] - a[0], b[1] - a[1])
            return (a[0] + u[0] * (s - acc), a[1] + u[1] * (s - acc)), u
        acc += seg
    return line[-1], unit(line[-1][0] - line[-2][0], line[-1][1] - line[-2][1])


def s_of(line, x, y):
    """Arc length of the point of the polyline nearest to (x, y)."""
    best, best_s, acc = 1e18, 0.0, 0.0
    for a, b in zip(line, line[1:]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        l2 = dx * dx + dy * dy
        t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / l2))
        d = (x - a[0] - t * dx) ** 2 + (y - a[1] - t * dy) ** 2
        if d < best:
            best, best_s = d, acc + t * math.sqrt(l2)
        acc += math.sqrt(l2)
    return best_s


def cut(line, s0, s1, step=None):
    """The piece of a polyline between arc lengths s0 and s1 (with its inner vertices; resampled if step)."""
    out = [at_s(line, s0)[0]]
    acc = 0.0
    for a, b in zip(line, line[1:]):
        acc += math.hypot(b[0] - a[0], b[1] - a[1])
        if s0 < acc < s1:
            out.append(b)
    out.append(at_s(line, s1)[0])
    if step:
        n = max(1, int(round((s1 - s0) / step)))
        out = [at_s(out, line_length(out) * k / n)[0] for k in range(n + 1)]
    return out


def circle(c, r, n=48, a0=0.0, a1=360.0):
    """Points on a circle, counter-clockwise from angle a0 to a1 (degrees from +x); a full circle has n points."""
    full = abs(a1 - a0) >= 360.0 - 1e-9
    k = n if full else max(2, int(round(n * abs(a1 - a0) / 360.0)) + 1)
    return [(c[0] + r * math.cos(math.radians(a0 + (a1 - a0) * i / (k if full else k - 1))),
             c[1] + r * math.sin(math.radians(a0 + (a1 - a0) * i / (k if full else k - 1)))) for i in range(k)]


def inside(x, y, poly):
    hit = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            hit = not hit
    return hit


def seg_distance(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    l2 = dx * dx + dy * dy
    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / l2))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def ring_distance(x, y, ring):
    """Distance to the outline of a polygon (always >= 0)."""
    return min(seg_distance(x, y, a[0], a[1], b[0], b[1]) for a, b in zip(ring, ring[1:] + ring[:1]))


_route_cells = {}


def route_distance(x, y):
    """Metres from (x, y) to the route centreline (segments, exact to the millimetre)."""
    if not _route_cells:
        n = len(ROUTE)
        for i, p in enumerate(ROUTE):
            q = ROUTE[(i + 1) % n]
            if abs(q["track_z"] - p["track_z"]) > 1.5:
                continue
            for cx in range(int((min(p["x"], q["x"]) - 30) // 30), int((max(p["x"], q["x"]) + 30) // 30) + 1):
                for cy in range(int((min(p["y"], q["y"]) - 30) // 30), int((max(p["y"], q["y"]) + 30) // 30) + 1):
                    _route_cells.setdefault((cx, cy), []).append((p["x"], p["y"], q["x"], q["y"]))
    best = 1e9
    for ax, ay, bx, by in _route_cells.get((int(x // 30), int(y // 30)), ()):
        best = min(best, seg_distance(x, y, ax, ay, bx, by))
    return best


LAYERS, PAINT = [], []

# Flat colours of the palette texture (make_street_textures.py street_palette; 8 x 8 cells, in this order). Markings,
# kerbs, signs, bollards, masts and the parked cars all use this one material. Survey colour tables, judged in Unity.
PALETTE = {
    "white": "#E8E5DB", "yellow": "#DDB23A", "sett_pale": "#BDB9AE", "setts_dark": "#6B6964", "kerb": "#CFC9BC",
    "gravel": "#6F6D69", "soil": "#8A7355", "sign_red": "#C23B32",
    "sign_blue": "#2F62A8", "black": "#1E1E20", "mast": "#DFDBCD", "bollard_pale": "#D8D2C2", "steel_grey": "#9EA2A6",
    "timber": "#B7A58A", "totem_green": "#3D4A45", "bin_grey": "#8C9094",
    "bin_green": "#4D8A4A", "bin_yellow": "#D9B63A", "bin_blue": "#3F6FB0", "car_silver": "#B9BCC0", "car_grey": "#5E6166",
    "car_white": "#E6E6E2", "car_navy": "#2F3E55", "car_red": "#7E3A36",
    "car_black": "#2A2B2E", "car_sand": "#B8A98C", "glass": "#27313B", "tyre": "#232325", "brick_red": "#9A6A55",
    "chevron_yellow": "#F0B400", "cream": "#E6D9B8", "ochre": "#C8A24A",
    "lamp_sand": "#C9BFA6", "light": "#FFF3D6", "terracotta": "#B5694A", "leaf": "#5E7F4A", "leaf_dark": "#46643C",
    "concrete": "#CDC4B6", "pay_blue": "#2B3F66", "grey_back": "#8F9396",
    "roof_dark": "#4A4744", "wood_dark": "#6B5744", "render_white": "#E9E4D8", "bank_green": "#7C7A46",
}


def layer(name, mat, ring, uv=None, ground=None, hull=0.0):
    """ground: what the ground colour map paints here ('lawn', 'meadow', 'bank'); default = the paving's colour.
    Pale kerb bands (mat 'kerb') are thin: they go on top of the paving like the markings instead of cutting it up."""
    if mat == "kerb":
        paint("kerb", ring)
        return
    LAYERS.append({"name": name, "mat": mat, "ring": [(float(x), float(y)) for x, y in ring], "uv": uv, "ground": ground,
                   "hull": hull})


def paint(colour, ring):
    PAINT.append({"colour": colour, "ring": [(float(x), float(y)) for x, y in ring]})


def paint_ribbon(colour, line, d0, d1, seg=6.0):
    """A painted band between the offsets d0 and d1 of a polyline, as short quads (cheap to drape, no long slivers)."""
    total = line_length(line)
    cuts = {0.0, total}
    acc = 0.0
    for a_, b_ in zip(line, line[1:]):
        acc += math.hypot(b_[0] - a_[0], b_[1] - a_[1])
        cuts.add(min(acc, total))
    for k in range(1, int(total // seg) + 1):
        cuts.add(min(k * seg, total))
    cuts = sorted(cuts)
    cuts = [c for k, c in enumerate(cuts) if k == 0 or c - cuts[k - 1] > 0.05]
    pts = [at_s(line, c) for c in cuts]
    lo, hi = offset([p for p, _u in pts], d0), offset([p for p, _u in pts], d1)
    for k in range(len(pts) - 1):
        paint(colour, [lo[k], lo[k + 1], hi[k + 1], hi[k]])


def route_ribbon(i0, i1, side, d0, d1):
    """Polygon beside the race line from route point i0 to i1: side +1 = left of the driving direction, d0..d1 metres
    from the centreline. For a strip of paving that the road has pushed aside (a sidewalk, a median)."""
    line = [(ROUTE[i]["x"], ROUTE[i]["y"]) for i in range(i0, i1 + 1)]
    return ribbon(line, side * d0, side * d1)


def frame_uv(origin, u, tile_u, tile_v):
    return (origin, u, tile_u, tile_v)


def stripes(origin, u, t0, t1, s0, s1, width, gap, colour="white", along=True):
    """Bands of paint filling a rectangle: `along` = bands run along u and repeat across (s), else the reverse."""
    k = 0
    lo, hi = (s0, s1) if along else (t0, t1)
    while lo + k * (width + gap) + width <= hi + 1e-6:
        a = lo + k * (width + gap)
        paint(colour, rect(origin, u, t0, t1, a, a + width) if along else rect(origin, u, a, a + width, s0, s1))
        k += 1


def dashes(line, d, width, dash, gap, colour="white", s0=0.0, s1=None):
    """A dashed line at offset d (to the left) of a polyline."""
    s1 = line_length(line) if s1 is None else s1
    s = s0
    while s + dash <= s1 + 1e-6:
        piece = cut(line, s, s + dash)
        paint(colour, ribbon(piece, d - width / 2, d + width / 2))
        s += dash + gap


# =====================================================================================================
# Section D + E: Avenida Marginal. The median's centre line, west to east (so "left" = north).
# Read off the orthophoto with brightness profiles across the road (+-0.7 m); bearing about 300 deg here.
# =====================================================================================================
MEDIAN = [(-420.0, 88.3), (-298.1, 18.0), (-278.7, 6.8), (-259.3, -4.4), (-239.8, -15.25), (-230.1, -21.0),
          (-216.6, -28.8), (-206.1, -34.4), (-195.1, -39.9), (-183.9, -45.1), (-172.3, -48.7), (-160.6, -52.3),
          (-148.4, -54.9)]
MED_HALF = 0.45
WB = (0.45, 6.75)          # westbound carriageway: offsets north of the median centre
EB = (-6.45, -0.45)        # eastbound
N_WALK = (6.75, 8.95)      # north sidewalk, white calcada
S_WALK = (-8.35, -6.45)    # south sidewalk
GAP_X = (-244.5, -230.5)   # the median is open here: the race line crosses it (survey 4.4: x -232..-243)
MOUTH_X = (-258.0, -243.0)  # the car-park entry: no south sidewalk across it


def med_s(x):
    """Arc length along MEDIAN at a given x (the line runs steadily east)."""
    acc = 0.0
    for a, b in zip(MEDIAN, MEDIAN[1:]):
        seg = math.hypot(b[0] - a[0], b[1] - a[1])
        if a[0] <= x <= b[0]:
            return acc + seg * (x - a[0]) / (b[0] - a[0])
        acc += seg
    return acc


def med_pt(x, d=0.0):
    """Point at offset d (north positive) from the median centre line at a given x."""
    p, u = at_s(MEDIAN, med_s(x))
    return pt(p, u, 0.0, d)


MEDIAN_SHIFT = 2.8
MEDIAN_CLEAR = KEEP_OUT + 0.45     # the median's near edge keeps this from the centreline (raised kerb: PROP_CLEAR + 0.4)


def median_runs(step=2.5):
    """The median as polylines (west to east), bent south where the race line comes close, cut where it crosses."""
    total = line_length(MEDIAN)
    runs, run = [], []
    s = med_s(-418.0)
    end = med_s(-148.4)
    while s <= end + 1e-6:
        p, u = at_s(MEDIAN, s)
        need = 0.0
        for _ in range(4):                               # shift south until its north edge is clear of the road
            q = pt(p, u, 0.0, -need)
            d = route_distance(*pt(q, u, 0.0, MED_HALF))
            if d >= MEDIAN_CLEAR:
                break
            need += MEDIAN_CLEAR - d + 0.02
        q = pt(p, u, 0.0, -need)
        ok = need <= MEDIAN_SHIFT and route_distance(*pt(q, u, 0.0, MED_HALF)) >= MEDIAN_CLEAR - 0.05 \
            and route_distance(*pt(q, u, 0.0, -MED_HALF)) >= MEDIAN_CLEAR - 0.05
        if ok:
            run.append(q)
        elif run:
            runs.append(run)
            run = []
        s += step
    if run:
        runs.append(run)
    out = []
    for r in runs:                                       # smooth the bend (the shift changes by centimetres per step)
        if len(r) >= 3:
            sm = [r[0]] + [((a[0] + 2 * b[0] + c[0]) / 4, (a[1] + 2 * b[1] + c[1]) / 4) for a, b, c in zip(r, r[1:], r[2:])] + [r[-1]]
            out.append(sm)
    return out


def marginal():
    asphalt_uv = None
    west, east = med_s(-418.0), med_s(-148.4)
    whole = cut(MEDIAN, west, east)
    # meadow north of the road (2026 state: a landscaped dry meadow with young trees, not the dusty car park)
    layer("D meadow", None, ribbon(cut(MEDIAN, med_s(-330.0), med_s(-150.0)), N_WALK[1], N_WALK[1] + 62.0), ground="meadow")
    # pale gravel paths through it (ground colour only)
    path = [med_pt(-158.0 - 14.0 * k, N_WALK[1] + 17.0 + 7.0 * math.sin(k * 1.1)) for k in range(12)]
    layer("D meadow path", None, ribbon(path, -1.1, 1.1), ground="gravel")
    layer("D meadow path", None, ribbon([med_pt(-226.0, N_WALK[1]), med_pt(-229.0, N_WALK[1] + 12.0),
                                         med_pt(-238.0, N_WALK[1] + 34.0), med_pt(-252.0, N_WALK[1] + 60.0)], -1.0, 1.0),
          ground="gravel")
    # belt between the road and the car park (lawn, shrubs, trees), down to the car park's north-east kerb
    # (its west end is the lawn island inside the hairpin)
    belt = (offset(cut(MEDIAN, med_s(-243.5), med_s(-148.4)), S_WALK[0]) + [(-138.0, -62.0), (-139.0, -108.5),
            kerb_pt(NE_KERB_LEN, 0.0), NE_KERB_A, (-232.0, -37.0), (-240.0, -31.0)])
    layer("D south belt", None, belt, ground="lawn")
    layer("D carriageways", "asphalt", ribbon(whole, EB[0], WB[1]), asphalt_uv)
    layer("D north sidewalk", "calcada", ribbon(whole, N_WALK[0], N_WALK[1]))
    # from the roundabout to x -205 the race line lies on the north sidewalk's line: the sidewalk runs beside the road
    layer("D north sidewalk beside the road", "calcada", route_ribbon(183, 222, -1, KEEP_OUT - 0.1, KEEP_OUT + 2.1))
    for x0, x1 in ((-418.0, MOUTH_X[0]), (MOUTH_X[1], -148.4)):
        layer("D south sidewalk", "calcada", ribbon(cut(MEDIAN, med_s(x0), med_s(x1)), S_WALK[0], S_WALK[1]))
    # kerb lines (flush pale stone bands; the raised kerbs are built from RAISED where the track is far enough)
    paint_ribbon("kerb", whole, N_WALK[0] - 0.02, N_WALK[0] + 0.16)
    for x0, x1 in ((-418.0, MOUTH_X[0]), (MOUTH_X[1], -148.4)):
        paint_ribbon("kerb", cut(MEDIAN, med_s(x0), med_s(x1)), S_WALK[1] - 0.16, S_WALK[1] + 0.02)
    # The median. The race line runs 6 m north of it from the roundabout on, drifts onto it and crosses it at
    # x -234..-239 (survey 4.4). Scenery gives way: where the road's verge reaches the median, the median moves south,
    # at most MEDIAN_SHIFT (the eastbound carriageway keeps one lane); where that is not enough there is no median.
    for run in median_runs():
        paint_ribbon("kerb", run, -MED_HALF, MED_HALF, seg=5.0)
        RAISED.append({"kind": "median", "line": run, "half": MED_HALF, "height": 0.2})
    # markings: dashed lane lines (5 m dash, 2 m gap), solid edge lines 0.3 m off the median
    lane = cut(MEDIAN, med_s(-400.0), med_s(-150.0))
    dashes(lane, WB[0] + 3.35, 0.14, 5.0, 2.0)
    dashes(lane, EB[1] - 3.2, 0.14, 5.0, 2.0)
    for run in median_runs():                            # solid edge lines 0.3 m off the median, both sides
        for d in (MED_HALF + 0.3, -MED_HALF - 0.3):
            paint_ribbon("white", run, d - 0.07, d + 0.07, seg=5.0)
    # the pedestrian crossing west of the restaurants: zebra bands across both carriageways
    p, u = at_s(MEDIAN, med_s(-303.0))
    stripes(p, u, -2.0, 2.0, WB[0] + 0.3, WB[1] - 0.2, 0.5, 0.5)
    stripes(p, u, -2.0, 2.0, EB[0] + 0.2, EB[1] - 0.3, 0.5, 0.5)
    # eastbound optical speed bars towards the roundabout: pairs of white bars, the spacing shrinks
    for bx in (-242.5, -231.7, -217.5, -205.4, -195.0, -185.2, -177.5, -171.7, -167.9, -164.6, -160.4, -156.3):
        p, u = at_s(MEDIAN, med_s(bx))
        for t in (-0.7, 0.25):
            paint("white", rect(p, u, t, t + 0.45, EB[0] + 0.25, EB[1] - 0.45))


# =====================================================================================================
# Section E: the hairpin forecourt of the two beach restaurants
# =====================================================================================================
HEDGE_A = (-277.4, -18.5)                      # front line (aisle edge) of the hedge-side bay row, north-west end
                                               # (the survey's line (-281,-22)..(-247,-57) is the hedge, 5 m behind it)
HEDGE_U = unit(34.0, -35.0)                    # ... running south-east (bearing 136 deg)
HEDGE_LEN = 47.5                               # 19 bays of 2.5 m
BAY = (2.5, 5.0)                               # bay width, depth
HEDGE_DEPTH = 1.5                              # clipped hedge right behind the bays
PLANT_STRIP = 3.6                              # planted strip (hedge, tall fan palms) between the bays and the retaining wall
WALL_S = BAY[1] + PLANT_STRIP                  # the retaining wall of the restaurant terrace, m behind the bay front
PORTAL_T = (18.0, 23.0)                        # the entrance portal stands in the bay row here (two bays wide)
BRICK_A = (-299.0, 3.9)                        # back line (white wall) of the brick-paver bays, west end
BRICK_U = unit(30.0, -16.0)
BRICK_LEN = 35.0                               # 14 bays
GRASS_ISLAND = [(-266.6, -12.6), (-263.8, -11.4), (-260.6, -13.4), (-259.6, -16.6), (-261.2, -19.6), (-264.4, -20.8),
                (-267.0, -19.0), (-267.6, -15.4)]
SPLITTER = [(-256.0, -14.3), (-253.7, -15.6), (-254.4, -18.4), (-256.0, -20.6), (-257.0, -19.4), (-257.1, -16.4)]
NE_KERB_A = (-226.0, -44.0)                    # north-east kerb of the car-park aisle (section F), north-west end
NE_KERB_U = unit(76.0, -60.0)                  # bearing 128 deg
NE_KERB_LEN = 96.8
MAST_A = (-265.4, -17.4)
RAISED, PROPS, HEDGES, WALLS = [], [], [], []


def hedge_pt(t, s):
    """Frame of the hedge-side bay row: t along the row (south-east), s behind the front line (towards the hedge)."""
    return pt(HEDGE_A, HEDGE_U, t, -s)


def _clip_t(poly, t0, t1):
    """Clip a polygon to t0 <= t <= t1 in the hedge row's frame (Sutherland-Hodgman, two half planes)."""
    def tt(p):
        return (p[0] - HEDGE_A[0]) * HEDGE_U[0] + (p[1] - HEDGE_A[1]) * HEDGE_U[1]
    for lim, keep in ((t0, lambda v: v >= t0), (t1, lambda v: v <= t1)):
        out = []
        for a, b in zip(poly, poly[1:] + poly[:1]):
            ta, tb = tt(a), tt(b)
            if keep(ta):
                out.append(a)
            if keep(ta) != keep(tb):
                f = (lim - ta) / (tb - ta)
                out.append((a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f))
        poly = out
        if len(poly) < 3:
            return []
    return poly


def brick_pt(t, s):
    """Frame of the brick bays: t along the wall (east-south-east), s out from the wall (towards the aisle)."""
    return pt(BRICK_A, BRICK_U, t, -s)


def kerb_pt(t, s):
    """Frame of the car-park aisle: t along the north-east kerb (south-east), s from the kerb towards the sea."""
    return pt(NE_KERB_A, NE_KERB_U, t, -s)


def forecourt():
    conc_uv = frame_uv(HEDGE_A, HEDGE_U, 4.0, 4.0)
    # the whole concrete car park and forecourt: one outline, everything else is drawn on top
    sw = [kerb_pt(t, 17.5) for t in (NE_KERB_LEN, 27.0)]
    outline = ([(-348.0, 16.5), (-324.0, 8.0), (-306.0, 4.5), BRICK_A, brick_pt(BRICK_LEN, 0.0),
                med_pt(-266.0, S_WALK[0]), med_pt(-243.5, S_WALK[0]),
                (-240.0, -31.0), (-232.0, -37.0), NE_KERB_A, kerb_pt(NE_KERB_LEN, 0.0)]
               + sw + [(-203.0, -87.2), (-241.0, -78.0), (-248.5, -76.5), hedge_pt(HEDGE_LEN + 1.5, 5.0),
                       hedge_pt(0.0, 5.0), hedge_pt(-3.5, 5.0), SERVICE_RAMP[1], SERVICE_RAMP[0], (-296.0, -13.5),
                       (-307.0, -5.0), (-323.0, 3.2), (-348.0, 11.0)])
    # the terrace of the two restaurants behind the hedge: paved ground (the ground map shows it, no paving mesh)
    layer("E terrace floor", None, TERRACE, ground="paved")
    layer("E concrete", "concrete", outline, conc_uv)
    # service ramp between the two restaurants (falls south to the terrace level)
    layer("E service ramp", "concrete", RAMP_PAVING, conc_uv)
    # hedge-side bays (white lines), without the two bays of the portal
    for t0, t1 in ((0.0, PORTAL_T[0]), (PORTAL_T[1], HEDGE_LEN)):
        layer("E hedge bays", "bays", [hedge_pt(t0, 0.0), hedge_pt(t1, 0.0), hedge_pt(t1, BAY[1]), hedge_pt(t0, BAY[1])],
              frame_uv(hedge_pt(t0, 0.0), HEDGE_U, BAY[0], -BAY[1]))
    # the planted strip behind the bays (hedge, fan palms), open at the portal
    layer("E planting strip", None, [hedge_pt(-1.0, BAY[1]), hedge_pt(HEDGE_LEN + 1.5, BAY[1]),
                                     hedge_pt(HEDGE_LEN + 1.5, WALL_S), hedge_pt(-1.0, WALL_S)], ground="bank")
    layer("E portal floor", "concrete", [hedge_pt(PORTAL_T[0], BAY[1]), hedge_pt(PORTAL_T[1], BAY[1]),
                                         hedge_pt(PORTAL_T[1], STAIR_S[0]), hedge_pt(PORTAL_T[0], STAIR_S[0])], conc_uv)
    layer("E portal pit", None, [hedge_pt(PORTAL_T[0], STAIR_S[0]), hedge_pt(PORTAL_T[1], STAIR_S[0]),
                                 hedge_pt(PORTAL_T[1], WALL_S + 1.0), hedge_pt(PORTAL_T[0], WALL_S + 1.0)], ground="paved")
    # brick-paver bays against the white wall
    layer("E brick bays", "brick", [brick_pt(0.0, 0.0), brick_pt(BRICK_LEN, 0.0), brick_pt(BRICK_LEN, BAY[1]),
                                    brick_pt(0.0, BAY[1])], frame_uv(BRICK_A, BRICK_U, BAY[0], -BAY[1]))
    # planted bank between the wall and the Marginal's sidewalk
    layer("E bank", None, [BRICK_A, (-306.0, 4.5), (-324.0, 8.0), (-348.0, 16.5), med_pt(-348.0, S_WALK[0]),
                           med_pt(-306.0, S_WALK[0]), med_pt(-266.0, S_WALK[0]), brick_pt(BRICK_LEN, 0.0)], ground="bank")
    # granite-sett entry throat
    throat = [med_pt(MOUTH_X[0], EB[0]), med_pt(MOUTH_X[1], EB[0]), (-243.2, -22.0), (-244.6, -25.0), (-247.6, -28.2),
              (-252.5, -28.4), (-257.5, -26.2), (-260.2, -22.4), (-260.0, -17.0)]
    layer("E throat setts", "setts", throat, frame_uv((-252.0, -20.0), bearing(205.0), 2.0, 2.0))
    # V16g: the old apron, the paved tip of the lawn island and the bins' pad lay where the widened hairpin (V14c)
    # now runs; the island inside the loop is capricciosa_plan's (drawn last, see carpark())
    layer("E grass island", None, GRASS_ISLAND, ground="lawn")
    layer("E splitter", "calcada", SPLITTER)
    RAISED.append({"kind": "island", "ring": GRASS_ISLAND, "height": 0.35, "top": None, "side": "wall_white", "rim": 0.25})
    RAISED.append({"kind": "island", "ring": SPLITTER, "height": 0.12, "top": "calcada", "side": "kerb", "rim": 0.0})
    # dark sett bands: across the concrete from the throat (slot drain line) and north-south to mast M1
    layer("E sett band", "setts", ribbon([(-262.0, -30.0), (-252.0, -40.0)], -0.18, 0.18))
    layer("F sett band", "setts", ribbon([(-243.0, -46.0), (-243.0, -76.5)], -0.28, 0.28))
    layer("E sett band at the ramp", "setts", ribbon([(-286.6, -12.6), (-280.4, -19.4)], -0.18, 0.18))
    # markings
    zebra = (-283.0, -17.0)                                    # zebra across the aisle at the top of the service ramp
    stripes(zebra, left_of(HEDGE_U), -3.0, 3.0, -2.75, 2.75, 0.5, 0.5)
    # hatched pedestrian zone in front of the portal: V16g, capricciosa_plan (stripes straight across)
    paint_ribbon("white", [hedge_pt(PORTAL_T[0], 5.0), hedge_pt(PORTAL_T[0], 0.0), hedge_pt(PORTAL_T[1], 0.0),
                           hedge_pt(PORTAL_T[1], 5.0)], -0.06, 0.06)
    # the sett zebra across the throat: pale bands 0.5 m wide and 3 m long, 0.5 m apart
    zu = unit(6.0, -3.2)
    for k in range(11):
        paint("sett_pale", rect((-258.8, -18.4), zu, k * 1.0, k * 1.0 + 0.5, -1.5, 1.5))
    # yield bar of dashes at the road edge (V16g: the yellow line of the old apron is gone with it)
    dashes([med_pt(MOUTH_X[0] + 0.5, EB[0] - 0.25), med_pt(MOUTH_X[1] - 0.5, EB[0] - 0.25)], 0.0, 0.4, 0.5, 0.5)
    # disabled bays: the four western brick bays get a blue square with a pale symbol block
    for k in range(4):
        c = brick_pt(k * BAY[0] + 1.25, 2.6)
        paint("sign_blue", rect(c, BRICK_U, -0.55, 0.55, -0.55, 0.55))
        paint("white", rect(c, BRICK_U, -0.12, 0.12, -0.35, 0.35))


SERVICE_RAMP = [(-291.4, -17.6), (-286.4, -19.8), (-283.6, -26.6), (-285.3, -32.6), (-288.0, -29.9), (-291.9, -18.9)]
RAMP_PAVING = [(-290.9, -18.2), (-286.9, -19.9), (-284.3, -26.6), (-285.6, -31.2), (-287.5, -29.2), (-290.9, -19.4)]
# The sunken restaurant terrace (env_streets.plan_terrace): the ground behind the hedge lies at TERRACE_Z; the floors of
# both restaurants are 0.15 m above it. The outline's land side is the retaining wall under the hedge.
TERRACE_Z = 4.25
RESTAURANT_FLOOR = 4.40
RESTAURANT_STOREY = {"capricciosa": 2.75, "pastorinha": 2.65}   # V10b: 3.9 / 3.4 m, V10f: 3.0 / 2.9 m; V16g: the roof
                                                               # edge 1.2-1.5 m above the forecourt at the portal
STAIR_T = (19.35, 21.65)                       # the stair inside the portal, along the hedge row (2.3 m wide)
STAIR_S = (6.2, WALL_S)                        # ... its top 6.2 m behind the bay front, its foot at the retaining wall
PORTAL_S = (5.3, 9.3)                          # the portal box stands in the planted strip, behind the two hatched bays
TERRACE = ([hedge_pt(-0.5, WALL_S), hedge_pt(PORTAL_T[0], WALL_S), hedge_pt(PORTAL_T[0], STAIR_S[0]),
            hedge_pt(PORTAL_T[1], STAIR_S[0]), hedge_pt(PORTAL_T[1], WALL_S), hedge_pt(HEDGE_LEN + 2.0, WALL_S),
            (-257.0, -70.0), (-274.0, -84.0), (-304.0, -56.0), (-334.0, -24.0), (-334.0, -10.0), (-321.0, 0.4),
            (-306.0, -8.0), (-297.0, -15.0), (-292.7, -17.5)])


def terrace_target(x, y):
    """Height the terrace asks for at (x, y), or None outside it: the flat terrace, the stair pit and the ramp."""
    if inside(x, y, SERVICE_RAMP):
        top, foot = (-288.9, -18.7), (-285.6, -30.6)
        u = unit(foot[0] - top[0], foot[1] - top[1])
        t = ((x - top[0]) * u[0] + (y - top[1]) * u[1]) / math.hypot(foot[0] - top[0], foot[1] - top[1])
        return ("ramp", min(max(t, 0.0), 1.0))
    if inside(x, y, TERRACE):
        return ("flat", TERRACE_Z)
    import capricciosa_plan                        # V16g: sand below the sea wall, esplanade and cliff foot
    return capricciosa_plan.terrain_target(x, y)


def terrace_zone(x, y):
    """Which lowered area a point lies in (None outside): the lattice puts a wall face between two different ones."""
    if inside(x, y, SERVICE_RAMP) or inside(x, y, TERRACE):
        return "terrace"
    import capricciosa_plan
    return capricciosa_plan.terrain_zone(x, y)


# =====================================================================================================
# Section F: the coastal car park (the route runs down its main aisle)
# =====================================================================================================
def carpark():
    conc_uv = frame_uv(NE_KERB_A, NE_KERB_U, 4.0, 4.0)
    # the aisle part gets its own slab direction (joints parallel to the kerb)
    layer("F aisle concrete", "concrete", [kerb_pt(-2.0, 0.0), kerb_pt(NE_KERB_LEN, 0.0), kerb_pt(NE_KERB_LEN, 17.5),
                                           kerb_pt(27.0, 17.5), kerb_pt(27.0, 22.5), kerb_pt(-18.0, 22.5),
                                           kerb_pt(-14.0, 5.0)], conc_uv)
    # bay rows: north-east row (noses to the lawn), the south-west row = north-east half of the central double row,
    # its partner row in the wide part, and the south row
    def row(name, t0, t1, s0, s1, flip=False):
        ring = [kerb_pt(t0, s0), kerb_pt(t1, s0), kerb_pt(t1, s1), kerb_pt(t0, s1)]
        layer(name, "bays", ring, frame_uv(kerb_pt(t0, s1 if flip else s0), NE_KERB_U, BAY[0], BAY[1] if flip else -BAY[1]))
    row("F NE bays", 0.0, 95.0, 0.0, 5.0, flip=True)
    row("F SW bays", SW_ROW_T0, 95.0, 12.5, 17.5)
    row("F central row", CENTRAL_T0, 27.5, 17.5, 22.5, flip=True)
    sa, su = (-241.0, -72.6), unit(39.0, -8.9)                 # south row: front line along the second aisle
    layer("F south bays", "bays", rect(sa, su, 0.0, 40.0, -5.0, 0.0), frame_uv(sa, su, BAY[0], -BAY[1]))
    # tree pits in the north-east bay row (square, bare soil): every fifth bay in the north-west, every third further on
    for t in PIT_T:
        layer("F tree pit", None, [kerb_pt(t - 0.75, 0.6), kerb_pt(t + 0.75, 0.6), kerb_pt(t + 0.75, 2.1),
                                   kerb_pt(t - 0.75, 2.1)], ground="soil")
    # zebra Z1 in the south-west row (replaces two bays), the footpath to the beach behind it
    z1 = s_kerb(-198.0, -82.0)
    layer("F zebra pad", "concrete", [kerb_pt(z1 - 2.5, 12.5), kerb_pt(z1 + 2.5, 12.5), kerb_pt(z1 + 2.5, 17.5),
                                      kerb_pt(z1 - 2.5, 17.5)], conc_uv)
    stripes(kerb_pt(z1 - 2.5, 17.3), NE_KERB_U, 0.0, 5.0, 0.3, 4.6, 0.45, 0.45, along=True)
    layer("F beach path", "concrete", ribbon([kerb_pt(z1, 17.5), (-203.5, -93.0), (-207.0, -100.0)], -1.5, 1.5), conc_uv)
    # the yellow hatched box in the south-west row, direction arrows in the aisle
    yb = s_kerb(-177.0, -102.0)
    box = [kerb_pt(yb - 1.25, 12.6), kerb_pt(yb + 1.25, 12.6), kerb_pt(yb + 1.25, 17.4), kerb_pt(yb - 1.25, 17.4)]
    paint_ribbon("yellow", box + box[:1], -0.06, 0.06)
    paint_ribbon("yellow", [box[0], box[2]], -0.06, 0.06)
    paint_ribbon("yellow", [box[1], box[3]], -0.06, 0.06)
    for (ax, ay), heading in (((-215.0, -64.0), 308.0), ((-178.0, -95.0), 308.0), ((-176.0, -91.5), 128.0)):
        arrow((ax, ay), bearing(heading))
    # the asphalt square at the south-east end, a sett band on its north-west edge
    square = [(-160.0, -112.0), (-151.0, -106.0), (-139.0, -108.5), (-139.0, -131.0), (-146.0, -131.0), (-160.0, -118.0)]
    layer("F asphalt square", "asphalt", square)
    layer("F square sett band", "setts", ribbon([(-160.6, -111.6), (-151.2, -105.4)], -0.3, 0.3),
          frame_uv((-160.6, -111.6), unit(9.4, 6.2), 2.0, 2.0))
    # the path down to the promenade (the route runs on it): pale concrete, 5 m, the track covers most of it
    layer("F promenade path", "concrete", ribbon([(-146.5, -124.5), (-136.0, -133.5), (-128.0, -143.5)], -4.5, 4.5), conc_uv)
    # the lawn triangle south of the wide part and the lawn strip along the south-west kerb
    layer("F lawn triangle", None, [(-241.0, -78.2), (-203.0, -87.4), kerb_pt(30.0, 17.7), (-207.0, -102.0), (-226.0, -86.0)],
          ground="lawn")
    layer("F SW lawn", None, [kerb_pt(27.0, 17.7), kerb_pt(NE_KERB_LEN - 4.0, 17.7), kerb_pt(NE_KERB_LEN - 4.0, 30.0),
                              kerb_pt(27.0, 34.0)], ground="lawn")
    import capricciosa_plan                        # V16g: hairpin island, portal hatch, esplanade, west car park
    capricciosa_plan.extend(sys.modules[__name__])


SW_ROW_T0, CENTRAL_T0 = -7.5, 0.0     # where the south-west bay row and its partner row begin (m along the kerb)
PIT_T = [5.0, 17.5, 30.0, 37.5, 45.0, 52.5, 60.0, 67.5, 75.0, 82.5, 90.0]     # along the north-east kerb (palms stand here)


def s_kerb(x, y):
    """t along the north-east kerb of the point nearest to (x, y)."""
    return (x - NE_KERB_A[0]) * NE_KERB_U[0] + (y - NE_KERB_A[1]) * NE_KERB_U[1]


def arrow(at, u, colour="white"):
    """A straight lane arrow, 3.6 m long, pointing along u."""
    paint(colour, rect(at, u, -1.8, 0.6, -0.09, 0.09))
    paint(colour, [pt(at, u, 0.6, -0.42), pt(at, u, 1.8, 0.0), pt(at, u, 0.6, 0.42)])


# =====================================================================================================
# Section C: the roundabout. Centre and radii from the orthophoto (+-0.7 m).
# =====================================================================================================
RB = (-107.0, -64.5)
RB_KERB, RB_BED, RB_OUT = 14.3, 11.6, 22.7     # island kerb, inner gravel bed, outer kerb of the ring
EAST_ARM = [(-86.0, -72.0), (-30.0, -92.4)]    # median line of the Marginal east of the ring (bearing 110 deg)
BLADES = [                                      # rust-coloured steel wedges in the gravel bed: OUR OWN arrangement
    # offset across (m, towards north-north-east), width, low end and high end along the axis (m), height at the high end
    (8.2, 2.5, 6.0, -6.4, 1.15), (4.2, 2.9, 9.6, -9.4, 1.4), (0.0, 3.1, 10.4, -10.5, 1.55), (-4.1, 2.9, 9.4, -9.6, 1.35),
    (-8.1, 2.5, -6.2, 6.4, 1.1),
]
BLADE_AXIS = bearing(108.0)                     # the wedges' long axis (east-south-east)


def roundabout():
    c = RB
    layer("C outer sidewalk", "calcada", circle(c, RB_OUT + 2.5, 48))
    layer("C south verge", None, circle(c, RB_OUT + 9.0, 40, 200.0, 318.0) + circle(c, RB_OUT + 2.4, 40, 200.0, 318.0)[::-1],
          ground="lawn")
    layer("C ring", "asphalt", circle(c, RB_OUT, 48))
    # west arm: both carriageways from the end of the Marginal's median to the ring; its splitter island
    layer("C west arm", "asphalt", [med_pt(-148.6, WB[1]), (-135.4, -51.1), (-127.6, -53.0), (-127.6, -70.4),
                                    (-136.1, -65.4), med_pt(-148.6, EB[0])])
    layer("C west arm south walk", "calcada", ribbon([med_pt(-148.6, EB[0]), (-136.1, -65.4), (-129.2, -69.6)], -1.9, 0.0))
    west_split = [(-146.4, -55.2), (-136.0, -57.2), (-128.8, -56.4), (-127.4, -58.6), (-128.4, -63.0), (-136.0, -60.7)]
    layer("C west splitter", "calcada", west_split)
    RAISED.append({"kind": "island", "ring": west_split, "height": 0.13, "top": "calcada", "side": "kerb", "rim": 0.0})
    # east arm (the Marginal towards Lisbon): two carriageways and a calcada median, sidewalks, the splitter
    layer("C east arm walk", "calcada", ribbon(EAST_ARM, -10.4, 10.4))
    layer("C east arm", "asphalt", ribbon([(-90.0, -70.5)] + EAST_ARM[1:], -7.8, 7.8))
    layer("C east median", "calcada", ribbon([(-80.0, -74.2)] + EAST_ARM[1:], -0.75, 0.75))
    east_split = [(-88.2, -70.4), (-84.6, -70.2), (-80.4, -72.9), (-80.9, -75.4), (-85.2, -74.8)]
    layer("C east splitter", "calcada", east_split)
    RAISED.append({"kind": "island", "ring": east_split, "height": 0.13, "top": "calcada", "side": "kerb", "rim": 0.0})
    RAISED.append({"kind": "median", "line": cut([(-80.0, -74.2)] + EAST_ARM[1:], 1.0, 50.0, step=2.5), "half": 0.75,
                   "height": 0.13})
    # the island: pale kerb ring, sett apron, dark gravel bed with the steel wedges
    layer("C apron", "setts", circle(c, RB_KERB, 48), frame_uv(c, (1.0, 0.0), 2.0, 2.0))
    layer("C gravel bed", "pal:gravel", circle(c, RB_BED, 40))
    kerb_o, kerb_i = circle(c, RB_KERB, 48), circle(c, RB_KERB - 0.25, 48)
    for k in range(48):
        paint("kerb", [kerb_i[k], kerb_o[k], kerb_o[(k + 1) % 48], kerb_i[(k + 1) % 48]])
    # white edge line round the island, yield dashes at the three entries
    edge = circle(c, RB_KERB + 0.65, 64)
    for a, b in zip(edge, edge[1:] + edge[:1]):
        paint_ribbon("white", [a, b], -0.15, 0.15)
    for a0, a1 in ((97.0, 113.0), (188.0, 204.0), (338.0, 354.0)):          # north, west, east entries (angle from +x)
        arc = circle(c, RB_OUT - 0.35, 96, a0, a1)
        dashes(arc, 0.0, 0.45, 0.5, 0.5)
    # zebras: across the west arm's eastbound lane and the east arm, 5-6 m back from the ring
    stripes((-133.6, -63.4), unit(-9.1, 4.2), -2.0, 2.0, -2.4, 2.4, 0.5, 0.5, along=False)
    for d0, d1 in ((1.3, 7.0), (-7.0, -1.3)):
        p, u = at_s(EAST_ARM, 7.5)
        stripes(p, u, -2.0, 2.0, d0, d1, 0.5, 0.5)


# =====================================================================================================
# Sections A + B: avenue N6-7. Three parallel roadways; the route runs in the middle one (x = -96).
# AVENUE = axis at x = -96, south to north; offsets to the left = west. North of y = 37 the avenue leans north-north-west.
# =====================================================================================================
AVENUE = [(-96.0, -46.0), (-96.0, 37.0), (-101.9, 100.0)]
AV_WEST_WALK = (13.2, 15.5)
AV_WEST_ROAD = (6.2, 13.2)
AV_MEDIAN = (4.3, 6.2)
AV_MID_ROAD = (-2.5, 4.2)
AV_SEPARATOR = (-4.2, -2.6)        # only north of y = +19
AV_SLIP = (-10.0, -4.4)
AV_EAST_WALK = (-13.3, -10.0)


def av_s(y):
    return y + 46.0 if y <= 37.0 else 83.0 + (y - 37.0) * math.hypot(5.9, 63.0) / 63.0


def av_pt(y, d):
    """Point at offset d (west positive) from the avenue axis at a given y."""
    p, u = at_s(AVENUE, av_s(y))
    return pt(p, u, 0.0, d)


def avenue():
    north = 100.0
    whole = cut(AVENUE, av_s(-41.0), av_s(north))
    # the dusty car park west of the avenue (2023 state) and the embankment lawn east of it: ground colours only
    layer("B dusty car park", None, ribbon(cut(AVENUE, av_s(-30.0), av_s(north)), AV_WEST_WALK[1], AV_WEST_WALK[1] + 60.0),
          ground="dust")
    layer("B east verge", None, ribbon(cut(AVENUE, av_s(-20.0), av_s(north)), AV_EAST_WALK[0] - 9.0, AV_EAST_WALK[0]),
          ground="lawn")
    layer("B asphalt", "asphalt", ribbon(whole, AV_SLIP[0], AV_WEST_ROAD[1]))
    layer("B west sidewalk", "calcada", ribbon(cut(AVENUE, av_s(-36.0), av_s(north)), AV_WEST_WALK[0], AV_WEST_WALK[1]))
    paint_ribbon("kerb", cut(AVENUE, av_s(-36.0), av_s(north)), AV_WEST_ROAD[1] - 0.02, AV_WEST_WALK[0] + 0.14)
    # lamp median, widening into the north splitter at the ring
    med = (offset(cut(AVENUE, av_s(-24.0), av_s(north)), AV_MEDIAN[0])
           + offset(cut(AVENUE, av_s(-24.0), av_s(north)), AV_MEDIAN[1])[::-1]
           + [(-104.6, -34.0), (-105.8, -42.2), (-100.4, -42.9), (-99.4, -36.0)])
    layer("B lamp median", "calcada", med)
    # the race line (8 m wide) covers the middle roadway and the real median's place: the median lies beside the road
    layer("B lamp median beside the road", "calcada", route_ribbon(133, 160, -1, KEEP_OUT - 0.1, KEEP_OUT + 1.8))
    layer("B separator", "calcada", ribbon(cut(AVENUE, av_s(19.0), av_s(north)), AV_SEPARATOR[0], AV_SEPARATOR[1]))
    # the paved wedge between the middle roadway and the slip lane, widening into the apron at the ring's north-east
    ring_ne = circle(RB, RB_OUT + 0.15, 96, 14.0, 53.5)
    wedge = [(-93.5, 0.0), (-91.3, 0.0), (-88.8, -34.0), (-88.3, -44.0), (-85.2, -53.0), (-81.0, -61.0)] + ring_ne
    layer("B wedge island", "calcada", wedge)
    # slip lane south of the avenue: it curves away to the Marginal's east arm
    layer("B slip lane", "asphalt", ribbon([(-87.4, -30.0), (-86.6, -44.0), (-83.2, -54.0), (-77.6, -62.6), (-70.5, -69.0)],
                                           -1.75, 1.75))
    # east sidewalk, widening southwards into the apron in front of the campus embankment
    walk = ([av_pt(north, AV_EAST_WALK[1]), av_pt(37.0, AV_EAST_WALK[1]), (-85.8, -30.0), (-84.9, -44.4), (-81.6, -54.6),
             (-76.2, -63.4), (-69.4, -70.2), (-64.0, -68.0), (-72.6, -55.0), (-76.4, -40.0), (-77.8, -21.0), (-80.1, -3.0),
             (-81.8, 15.0), (-82.6, 33.0), av_pt(north, AV_EAST_WALK[0])])
    layer("B east sidewalk", "calcada", walk)
    paint_ribbon("kerb", cut(AVENUE, av_s(-28.0), av_s(north)), AV_EAST_WALK[1] - 0.14, AV_SLIP[0] + 0.02)
    # zebra crossings at y -33..-38 with red-brown block paving at the waits
    for x0, x1 in ((-109.2, -102.5), (-91.4, -86.0)):
        stripes((x0, -37.6), (1.0, 0.0), 0.0, x1 - x0, 0.0, 4.2, 0.5, 0.5, along=False)
    for ring in ([(-111.6, -38.0), (-109.3, -38.0), (-109.3, -33.0), (-111.6, -33.0)],
                 [(-102.3, -38.0), (-100.4, -38.0), (-100.4, -33.0), (-102.3, -33.0)],
                 [(-93.4, -38.0), (-91.5, -38.0), (-91.5, -33.0), (-93.4, -33.0)],
                 [(-85.9, -38.0), (-83.0, -38.0), (-83.0, -33.0), (-85.9, -33.0)]):
        layer("B zebra wait", "pal:brick_red", ring)
    # markings: dashed lane lines, transverse double bars on the west roadway, the chevron gore at the bollards
    dashes(cut(AVENUE, av_s(-24.0), av_s(north)), (AV_WEST_ROAD[0] + AV_WEST_ROAD[1]) / 2, 0.14, 3.0, 4.0)
    dashes(cut(AVENUE, av_s(20.0), av_s(north)), -7.2, 0.14, 3.0, 4.0)
    for y in (27.0, 17.4, 9.9, 3.7, 1.2, -1.3, -3.8, -6.3, -8.8, -10.9, -12.6):
        for dy in (0.0, 0.75):
            paint("white", rect((-109.0, y + dy), (1.0, 0.0), 0.0, 6.6, 0.0, 0.4))
    for k in range(7):                                             # chevron hatching on the gore, y +20 .. 0
        y = 18.0 - k * 2.8
        w = 0.5 + 1.5 * k / 6
        paint("white", [(-92.9, y), (-92.9 + w, y - 1.4), (-92.9 + w, y - 1.9), (-92.9, y - 0.5)])


# =====================================================================================================
# V16 (photospheres ST01, ST02, N15; survey 3.9, item 29): the avenue's entrance side, y +100 .. +148. The street
# section ended at y = 100; here the roadways run on to the campus entrance: the east lane becomes the red-brown bus
# lay-by (3.5 m x 45 m), a calcada strip joins it to the entrance plaza (env_entrance), flush, no kerb.
# The steel posts along the lay-by, the pylon and the flag poles are env_north's.
# =====================================================================================================
AV_ENTRANCE = (100.0, 148.0)        # y range of this piece (the avenue's axis is extended in its last direction)
AV_LAYBY = (100.5, 145.5)           # y range of the bus lay-by
AV_LAYBY_W = 3.5
AV_PLAZA_X = -89.8                  # the calcada strip reaches 0.2 m into the entrance plaza's ring (west edge x = -90)


def av_entrance_line(y0, y1):
    (ax, ay), (bx, by) = AVENUE[-2], AVENUE[-1]
    u = unit(bx - ax, by - ay)
    return [(bx + u[0] * (y - by) / u[1], y) for y in (y0, y1)]


def avenue_entrance():
    y0, y1 = AV_ENTRANCE
    line = av_entrance_line(y0, y1)
    layer("B2 asphalt", "asphalt", ribbon(line, AV_SLIP[0], AV_WEST_ROAD[1]))
    layer("B2 west sidewalk", "calcada", ribbon(line, AV_WEST_WALK[0], AV_WEST_WALK[1]))
    layer("B2 lamp median", "calcada", ribbon(line, AV_MEDIAN[0], AV_MEDIAN[1]))
    layer("B2 separator", "calcada", ribbon(line, AV_SEPARATOR[0], AV_SEPARATOR[1]))
    east = offset(line, AV_SLIP[0])
    layer("B2 plaza strip", "calcada", east + [(AV_PLAZA_X, y1), (AV_PLAZA_X, y0)])
    lay = av_entrance_line(*AV_LAYBY)
    layer("B2 bus lay-by", "pal:brick_red", ribbon(lay, AV_SLIP[0], AV_SLIP[0] + AV_LAYBY_W))
    paint_ribbon("white", lay, AV_SLIP[0] + AV_LAYBY_W - 0.07, AV_SLIP[0] + AV_LAYBY_W + 0.07)     # outline, no word on it
    dashes(line, (AV_WEST_ROAD[0] + AV_WEST_ROAD[1]) / 2, 0.14, 3.0, 4.0)
    dashes(line, -7.2, 0.14, 3.0, 4.0, s1=AV_LAYBY[0] - y0 - 1.0)


# =====================================================================================================
# Section G (V15c): the beach promenade, route i321-i398 (photospheres S_p01..S_p05, survey notes_S 1.1-1.4)
# The race road (8 m) IS the promenade here, so everything stands beside it: "land" = left of the driving direction.
# Positions are given along the route (fractional point index, 2 m per point) and as an offset from the centreline.
# =====================================================================================================
PROM = (321, 398)                   # first and last route point of the dressing (the tunnel forecourt, x > -5, is V10j's)
PROM_SETT = (KEEP_OUT - 0.35, 5.65)  # granite-sett border outside the road's shoulder (the paving is clipped at KEEP_OUT)
PROM_SETT_SEA = ((321, 334), (373, 398))   # sea side: only where there is land beside the road (sand / kerb between)
PROM_BOLLARD = dict(offset=6.75, step=2.75, size=0.25, height=0.85,   # pale limestone posts on the lawn edge (S_p01)
                    land=(321.0, 398.0), sea=(321.0, 334.5),          # route index runs: land side, sea-side lawn
                    gaps=((321.0, 326.4), (355.8, 357.6), (371.6, 373.6), (377.2, 381.0)))   # path mouths (land side)
PROM_MAST = dict(offset=8.2, at=(325.0, 347.0, 370.5, 392.0), height=13.5)   # white flood masts, every 43-47 m
PROM_SEAT = dict(offset=6.95, width=0.4, height=0.48,               # curved white seat-walls (S_p03 h080), centre line
                 runs=((358.0, 364.0), (366.0, 371.0)))              # ... 6.75 m inner face; from/to route index
PROM_DRAIN = dict(at=323.0, width=0.3, reach=6.35)                   # cast-iron slot drain at the entrance (S_p01 h135)
PROM_STEPS = dict(run=(356.6, 359.0), start=4.9, rise=0.16, tread=0.34)   # white steps down to the cove sand (S_p03)
PROM_FITNESS = (                    # outdoor fitness machines on gravel pads on the lawn (S_p01 h135): centre, bearing
    dict(at=(-148.5, -136.5), bearing=40.0, size=(5.0, 3.2), protos=("Prop_FitnessTwist", "Prop_FitnessBars")),
    dict(at=(-115.5, -147.0), bearing=100.0, size=(4.4, 3.0), protos=("Prop_FitnessBars", "Prop_FitnessWheel")),
)
OLD_OLIVE = dict(survey=(-64.6, 51.8), at=(-63.62, 56.55), scale=0.85)   # lawn path, i114: moved out from 4.9 m


def route_at(t):
    """((x, y), unit tangent) at the fractional route index t."""
    i = int(math.floor(t))
    a, b = ROUTE[i % len(ROUTE)], ROUTE[(i + 1) % len(ROUTE)]
    f = t - i
    return (a["x"] + (b["x"] - a["x"]) * f, a["y"] + (b["y"] - a["y"]) * f), unit(b["x"] - a["x"], b["y"] - a["y"])


def route_side(t, d):
    """Point d metres to the left (d < 0: right) of the centreline at the fractional route index t, moved out along
    the local normal until it really is |d| from the whole centreline (inside a bend the plain normal comes closer)."""
    (x, y), u = route_at(t)
    n = left_of(u) if d >= 0 else (u[1], -u[0])
    r = abs(d)
    for _ in range(8):
        q = (x + n[0] * r, y + n[1] * r)
        short = abs(d) - route_distance(*q)
        if short <= 0.005:
            break
        r += short + 0.01
    return q


def route_line(t0, t1, d, step=1.0):
    """Polyline beside the route from index t0 to t1 at the offset d, a point about every `step` metres of route."""
    n = max(1, int(round((t1 - t0) * 2.0 / step)))
    return [route_side(t0 + (t1 - t0) * k / n, d) for k in range(n + 1)]


def promenade():
    sett_uv = None
    i0, i1 = PROM
    # land side: one border along the whole stretch; at the seat-walls the setts reach the wall's foot
    for a, b in ((i0, i1),):
        layer("G sett border land", "setts", route_line(a, b, PROM_SETT[0], 2.0) + route_line(a, b, PROM_SETT[1], 2.0)[::-1],
              sett_uv)
    for a, b in PROM_SEAT["runs"]:
        foot = PROM_SEAT["offset"] - PROM_SEAT["width"] / 2 + 0.02
        layer("G sett strip at seat-wall", "setts",
              route_line(a - 0.4, b + 0.4, PROM_SETT[1] - 0.05) + route_line(a - 0.4, b + 0.4, foot)[::-1], sett_uv)
    for a, b in PROM_SETT_SEA:
        layer("G sett border sea", "setts",
              route_line(a, b, -PROM_SETT[0], 2.0) + route_line(a, b, -PROM_SETT[1], 2.0)[::-1], sett_uv)
    # the slot drain: the road itself is the race surface and stays as it is, so the grate shows only on both verges
    t, w = PROM_DRAIN["at"], PROM_DRAIN["width"] / 4.0          # width in route-index units: 2 m per index
    for side in (1, -1):
        layer("G drain apron", "concrete", route_line(t - 1.6, t + 1.6, side * (KEEP_OUT - 0.35))
              + route_line(t - 1.6, t + 1.6, side * (PROM_DRAIN["reach"] + 0.5))[::-1])
        paint("roof_dark", [route_side(t - w, side * (KEEP_OUT + 0.02)), route_side(t + w, side * (KEEP_OUT + 0.02)),
                            route_side(t + w, side * PROM_DRAIN["reach"]), route_side(t - w, side * PROM_DRAIN["reach"])])
    # gravel pads under the fitness machines (ground paint only: no mesh)
    for f in PROM_FITNESS:
        u = bearing(f["bearing"])
        layer("G fitness pad", None, rect(f["at"], u, -f["size"][0] / 2, f["size"][0] / 2, -f["size"][1] / 2, f["size"][1] / 2),
              ground="gravel")


# =====================================================================================================
# Track surface (colour only): which material the road and its verges show. The route data are not touched.
# =====================================================================================================
SURFACE_RUNS = [            # first index, last index, surface
    (242, 246, "street_setts"),          # entry throat: dark granite setts from the kerb line
    (247, 310, "street_concrete"),       # forecourt and car-park aisle: pale concrete slabs
    (311, 320, "asphalt"),               # the asphalt square
    (321, 335, "street_concrete"),       # the concrete path down to the promenade
]
def track_surface(p):
    """Visual surface of the road at a route point (None: keep route.json's)."""
    for i0, i1, name in SURFACE_RUNS:
        if i0 <= p["i"] <= i1:
            return name
    return None


def layer_at(x, y):
    """The topmost layer at a point, or None."""
    hit = None
    for lay in LAYERS:
        b = lay.get("bbox")
        if b is None:
            xs, ys = [q[0] for q in lay["ring"]], [q[1] for q in lay["ring"]]
            b = lay["bbox"] = (min(xs), min(ys), max(xs), max(ys))
        if b[0] <= x <= b[2] and b[1] <= y <= b[3] and inside(x, y, lay["ring"]):
            hit = lay
    return hit


def build():
    if LAYERS:
        return
    marginal()
    forecourt()
    carpark()
    roundabout()
    avenue()
    avenue_entrance()      # V16
    promenade()            # V15c


build()
