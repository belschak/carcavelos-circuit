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
            if p.get("tunnel") and q.get("tunnel"):      # V18g: the beach tunnel runs 4 m under the N6; what lies on
                continue                                 # the ground above it (the N6's paving) is not near the road
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


def paint(colour, ring, road=False):
    """road (V18r): this marking may lie on the race road (inside KEEP_OUT). Where it does, env_streets hands it to
    Track_EdgeLines (flush paint on the road, no collider); every other marking is cut off at the keep-out.
    road="fill": only the part inside the keep-out is painted (paving that the road's shoulder has cut away)."""
    PAINT.append({"colour": colour, "ring": [(float(x), float(y)) for x, y in ring],
                  "road": road if road == "fill" else bool(road)})


def paint_ribbon(colour, line, d0, d1, seg=6.0, road=False):
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
        paint(colour, [lo[k], lo[k + 1], hi[k + 1], hi[k]], road)


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


def dashes(line, d, width, dash, gap, colour="white", s0=0.0, s1=None, road=False):
    """A dashed line at offset d (to the left) of a polyline."""
    s1 = line_length(line) if s1 is None else s1
    s = s0
    while s + dash <= s1 + 1e-6:
        piece = cut(line, s, s + dash)
        paint(colour, ribbon(piece, d - width / 2, d + width / 2), road)
        s += dash + gap


def smooth(points, step=1.0):
    """A smooth line through the points (Catmull-Rom), a vertex about every `step` metres: kerbs without corners."""
    p = [points[0]] + list(points) + [points[-1]]
    out = []
    for k in range(1, len(p) - 2):
        a, b, c, d = p[k - 1], p[k], p[k + 1], p[k + 2]
        n = max(1, int(math.ceil(math.hypot(c[0] - b[0], c[1] - b[1]) / step)))
        for j in range(n):
            t = j / n
            out.append(tuple(0.5 * (2 * b[i] + (c[i] - a[i]) * t + (2 * a[i] - 5 * b[i] + 4 * c[i] - d[i]) * t * t
                                    + (3 * b[i] - a[i] - 3 * c[i] + d[i]) * t ** 3) for i in (0, 1)))
    out.append(tuple(points[-1]))
    return out


def clip_half(ring, a, b):
    """The part of a polygon to the left of the line a -> b (Sutherland-Hodgman)."""
    def side(p):
        return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])
    out = []
    for p, q in zip(ring, ring[1:] + ring[:1]):
        sp, sq = side(p), side(q)
        if sp >= 0:
            out.append(p)
        if (sp >= 0) != (sq >= 0):
            t = sp / (sp - sq)
            out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def clip_band_y(ring, y0, y1):
    """The part of a polygon with y0 <= y <= y1."""
    return clip_half(clip_half(ring, (0.0, y0), (1.0, y0)), (1.0, y1), (0.0, y1))


def zebra(a, b, half, along=None, keep=None, road=False, bar=0.5, gap=0.5, piece=0.3):
    """A pedestrian crossing from the kerb point a to the kerb point b (the walking line): white bars 0.5 m wide and
    0.5 m apart, `half` metres to either side of the walking line, parallel to `along` (the road's direction; default:
    square to the walking line). keep(x, y): only the pieces of a bar for which it is true are painted (a bar ends
    where the asphalt ends)."""
    u = unit(b[0] - a[0], b[1] - a[1])
    v = along or left_of(u)
    total = math.hypot(b[0] - a[0], b[1] - a[1])
    n = int((total - gap / 2) // (bar + gap))
    t0 = (total - n * (bar + gap) + gap) / 2
    m = max(1, int(round(2 * half / piece)))
    for k in range(n):
        t = t0 + k * (bar + gap)
        run = []
        for j in range(m + 1):
            s0 = -half + 2 * half * j / m
            ok = j < m and (keep is None or keep(a[0] + u[0] * (t + bar / 2) + v[0] * (s0 + half / m),
                                                 a[1] + u[1] * (t + bar / 2) + v[1] * (s0 + half / m)))
            if ok:
                run.append(s0)
            if (not ok or j == m) and run:
                lo, hi = run[0], run[-1] + 2 * half / m
                paint("white", [(a[0] + u[0] * tt + v[0] * ss, a[1] + u[1] * tt + v[1] * ss)
                                for tt, ss in ((t, lo), (t + bar, lo), (t + bar, hi), (t, hi))], road)
                run = []


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


MEDIAN_SHIFT = 0.0                 # V18g: the median stays on its real line (it was bent up to 2.8 m south to keep
                                   # clear of the race road); where the road lies on it, it goes on as flush paint
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
    # north of the road: the dusty car park (V18g, dustlot(): drawn before this section)
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
    # V18g: the raised median stands only where it is clear of the race road on its real line; the pale band and the
    # solid lines beside it run the whole way, also across the race road (flush paint, road=True), and stop only at
    # the real gap in the median (GAP_X).
    for run in median_runs():
        RAISED.append({"kind": "median", "line": run, "half": MED_HALF, "height": 0.2})
    for x0, x1 in ((-418.0, GAP_X[0]), (GAP_X[1], -148.4)):
        piece = cut(MEDIAN, med_s(x0), med_s(x1), step=2.5)
        paint_ribbon("kerb", piece, -MED_HALF, MED_HALF, seg=2.5, road=True)
        for d in (MED_HALF + 0.3, -MED_HALF - 0.3):      # solid edge lines 0.3 m off the median, both sides
            paint_ribbon("white", piece, d - 0.07, d + 0.07, seg=2.5, road=True)
    # markings: dashed lane lines (5 m dash, 2 m gap); the westbound one runs on across the race road
    lane = cut(MEDIAN, med_s(-400.0), med_s(-150.0))
    dashes(lane, WB[0] + 3.35, 0.14, 5.0, 2.0, road=True)
    dashes(lane, EB[1] - 3.2, 0.14, 5.0, 2.0, road=True)
    # the pedestrian crossing west of the restaurants: zebra bands across both carriageways
    p, u = at_s(MEDIAN, med_s(-303.0))
    stripes(p, u, -2.0, 2.0, WB[0] + 0.3, WB[1] - 0.2, 0.5, 0.5)
    stripes(p, u, -2.0, 2.0, EB[0] + 0.2, EB[1] - 0.3, 0.5, 0.5)
    # eastbound optical speed bars towards the roundabout: pairs of white bars, the spacing shrinks
    for bx in (-242.5, -231.7, -217.5, -205.4, -195.0, -185.2, -177.5, -171.7, -167.9, -164.6, -160.4, -156.3):
        p, u = at_s(MEDIAN, med_s(bx))
        for t in (-0.7, 0.25):
            paint("white", rect(p, u, t, t + 0.45, EB[0] + 0.25, EB[1] - 0.45), road=True)


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
        paint("sett_pale", rect((-258.8, -18.4), zu, k * 1.0, k * 1.0 + 0.5, -1.5, 1.5), road=True)
    # yield bar of dashes at the road edge (V16g: the yellow line of the old apron is gone with it)
    dashes([med_pt(MOUTH_X[0] + 0.5, EB[0] - 0.25), med_pt(MOUTH_X[1] - 0.5, EB[0] - 0.25)], 0.0, 0.4, 0.5, 0.5, road=True)
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
        arrow((ax, ay), bearing(heading), road=True)
    # V18g: the race road (8 m and its shoulders) is wider than the aisle (7.5 m) and takes the front metre of both bay
    # rows: the bay lines and the dark sett band go on there as flush paint (only inside the road's keep-out)
    for k in range(39):
        paint("white", [kerb_pt(k * BAY[0] - 0.05, 2.6), kerb_pt(k * BAY[0] + 0.05, 2.6), kerb_pt(k * BAY[0] + 0.05, 5.0),
                        kerb_pt(k * BAY[0] - 0.05, 5.0)], road="fill")
    for k in range(42):
        t = SW_ROW_T0 + k * BAY[0]
        paint("white", [kerb_pt(t - 0.05, 12.5), kerb_pt(t + 0.05, 12.5), kerb_pt(t + 0.05, 14.9), kerb_pt(t - 0.05, 14.9)],
              road="fill")
    paint("setts_dark", ribbon([(-243.0, -46.0), (-243.0, -76.5)], -0.28, 0.28), road="fill")
    paint("setts_dark", ribbon([(-262.0, -30.0), (-252.0, -40.0)], -0.18, 0.18), road="fill")
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


def arrow(at, u, colour="white", road=False):
    """A straight lane arrow, 3.6 m long, pointing along u."""
    paint(colour, rect(at, u, -1.8, 0.6, -0.09, 0.09), road)
    paint(colour, [pt(at, u, 0.6, -0.42), pt(at, u, 1.8, 0.0), pt(at, u, 0.6, 0.42)], road)


# =====================================================================================================
# Section C: the roundabout where the N6-7 meets the Avenida Marginal / N6 (V18r, 2026-11-12: rebuilt).
# Kerb lines, island, arms and crossings are read off the DGT orthophoto (3 px/m, +-0.7 m); the markings and the
# arrangement of the island (five long corten beds on pale gravel) from the satellite view. Nothing is a traced image.
#
# The race road (8 m + shoulders, route points 150-195) comes down the middle roadway of the north arm, runs over the
# north-west quarter of the ring and leaves on the westbound carriageway. It cannot move, so here it IS the real
# asphalt: env_track paints no orange kerb and env_trackside no race edge line in the zone below, and the markings
# flagged road=True (ring lane line, island edge line, give-way lines, zebras) run on across the road; where they lie
# on it env_streets puts them into Track_EdgeLines (flush, no collider). Where the road is wider than the real
# carriageway the scenery gives way: the north-west sidewalk and the paved wedge begin at the road's edge.
# =====================================================================================================
RB = (-107.3, -64.4)
RB_KERB, RB_BED, RB_OUT = 14.3, 11.5, 22.7     # island kerb, edge of the gravel bed, outer kerb of the ring
RB_SEG = 96                                    # segments of the ring's and the island's circles (chords under 1.5 m)
RB_APRON_KERB = 0.28                           # the island's pale kerb stone (flush: the race line passes 4.3 m away)
RB_EDGE_LINE = (0.5, 0.15)                     # white edge line round the island: distance outside the kerb, width
RB_LANE = (19.2, 0.14, 3.0, 5.04)              # the ring's dashed lane line: radius, width, dash, gap (15 dashes)
RB_NO_KERBS = (158, 192)                       # route points: no orange race kerb (it stood at i 163-173)
RB_NO_LINES = (164, 187)                       # ... no race edge lines: from the north arm's zebra to the west arm's
EAST_ARM = [(-78.5, -76.6), (-30.0, -94.25)]   # median line of the N6 east of the ring (bearing 110 deg)
EAST_U = unit(EAST_ARM[1][0] - EAST_ARM[0][0], EAST_ARM[1][1] - EAST_ARM[0][1])
EAST = dict(median=0.5, entry=5.1, island=5.7, slip=9.2, walk_n=11.4, exit=-5.85, walk_s=-8.05, end=52.0, island_end=22.0)
AV_SOUTH = -10.0                               # the avenue's own layers end here; south of it the roundabout's north arm

BLADE_AXIS = bearing(115.5)                    # long axis of the corten beds (east-south-east)
BLADE_RIM, BLADE_LOW, BLADE_INSET = 0.5, 0.2, 0.15   # corten rim round the dark gravel top, height at the low end, gap to the bed's edge
BLADES = [   # across the island (m from its centre towards north-north-east): from, to; high end (+1 = east-south-east); height
    (8.3, 9.7, -1, 0.85), (2.95, 6.5, 1, 1.25), (-1.0, 2.6, -1, 1.35), (-5.8, -3.2, 1, 1.2), (-8.6, -6.05, -1, 1.0),
]


# V18g: the same on every public street the race road lies on. Route points (2 m apart):
#   122-157  the avenue N6-7: from the east sidewalk (where the lawn path meets it) down the middle roadway
#   158-192  the roundabout (V18r, unchanged)
#   193-246  the Avenida Marginal's westbound carriageway, the median gap and the car park's entry throat
#   247-320  the hairpin on the forecourt, the car park's main aisle and the asphalt square at its south-east end
# (321 on is the path down to the promenade and the promenade itself: the coast's.)
# env_track.build_kerbs and env_trackside.kerb_sides / build_lines ask the two functions below, nothing else.
STREET_NO_KERBS = [(122, 157), RB_NO_KERBS, (193, 320)]
STREET_NO_LINES = [(122, 163), RB_NO_LINES, (188, 320)]


def race_kerbs_off(i):
    """env_track / env_trackside: no orange kerb at route point i (the race road is a real street there)."""
    return any(a <= i <= b for a, b in STREET_NO_KERBS)


def race_lines_off(i):
    """env_trackside: no race edge line at route point i (the real street's own markings run across the road)."""
    return any(a <= i <= b for a, b in STREET_NO_LINES)


def rb_pt(angle, r=RB_OUT):
    """Point on a circle round the island's centre; angle in degrees from +x (east), counter-clockwise."""
    return (RB[0] + r * math.cos(math.radians(angle)), RB[1] + r * math.sin(math.radians(angle)))


def rb_arc(a0, a1, r=RB_OUT, step=3.75):
    n = max(1, int(round(abs(a1 - a0) / step)))
    return [rb_pt(a0 + (a1 - a0) * k / n, r) for k in range(n + 1)]


def east_pt(t, d):
    """Point t metres along the N6's median line east of the ring, d metres to its left (north)."""
    return pt(EAST_ARM[0], EAST_U, t, d)


def blade_outline(t0, t1, inset=0.0, n=6):
    """Outline of a corten bed: the strip t0..t1 across the island, cut off by the round edge of the gravel bed."""
    r = RB_BED - BLADE_INSET - inset
    lo, hi = t0 + inset, t1 - inset
    an = left_of(BLADE_AXIS)

    def p(s, t):
        return (RB[0] + BLADE_AXIS[0] * s + an[0] * t, RB[1] + BLADE_AXIS[1] * s + an[1] * t)
    ts = [lo + (hi - lo) * k / n for k in range(n + 1)]
    east = [p(math.sqrt(max(r * r - t * t, 0.0)), t) for t in ts]
    west = [p(-math.sqrt(max(r * r - t * t, 0.0)), t) for t in reversed(ts)]
    return east + west


def blade_height(x, y, t0, t1, high_end, height):
    """Height of a bed's top above the island's ground at (x, y): a wedge, BLADE_LOW at one end, `height` at the other."""
    half = math.sqrt(max((RB_BED - BLADE_INSET) ** 2 - min(abs(t0), abs(t1)) ** 2, 1.0))
    s = (x - RB[0]) * BLADE_AXIS[0] + (y - RB[1]) * BLADE_AXIS[1]
    f = min(max((s * high_end + half) / (2 * half), 0.0), 1.0)
    return BLADE_LOW + (height - BLADE_LOW) * f


def roundabout():
    c = RB
    E = EAST

    def on_asphalt(x, y):
        lay = layer_at(x, y)
        return route_distance(x, y) < KEEP_OUT - 0.03 or (lay is not None and lay["mat"] == "asphalt")

    # ---------------- kerb lines ----------------
    # north arm, west roadway's west kerb, on round the north-west corner into the Marginal's north kerb (west of
    # x -119 the race road covers it)
    kw = smooth([(-109.2, AV_SOUTH), (-109.5, -13.0), (-110.5, -17.5), (-111.4, -22.5), (-112.0, -28.0), (-112.7, -33.5),
                 (-114.3, -38.0), (-117.0, -41.6), (-120.5, -44.9), (-124.0, -47.5), (-128.0, -49.4), (-132.0, -50.2),
                 (-140.0, -49.8), (-150.0, -48.6)])
    # north arm, east kerb of the middle roadway down to the ring (the race road covers it north of y -40)
    ke_n = smooth([(-93.5, AV_SOUTH), (-94.4, -14.0), (-95.6, -20.0), (-96.2, -28.0), (-96.3, -34.0), (-96.0, -38.5),
                   (-95.3, -42.5), rb_pt(55.0)])
    # south side, west to east: the Marginal's south kerb, the ring's south half, the N6's south kerb
    ks_w = smooth([med_pt(-148.6, EB[0]), (-145.0, -62.9), (-140.0, -65.1), (-136.0, -67.5), (-133.0, -69.9),
                   (-130.4, -72.2), (-128.4, -74.3), rb_pt(211.0)])
    ks_e = smooth([rb_pt(291.0), rb_pt(295.0), (-94.6, -84.75), (-90.5, -84.45), (-85.5, -84.3), (-80.5, -84.35),
                   (-75.5, -84.75), east_pt(10.2, E["exit"])]) + [east_pt(E["end"], E["exit"])]
    kerb_s = ks_w + rb_arc(211.0, 291.0)[1:-1] + ks_e
    # the right-turn lane from the N6 to the N6-7 (north-east of the ring): west and east edge, north to south-east
    slw = smooth([(-91.6, AV_SOUTH), (-91.7, -30.0), (-91.4, -40.0), (-90.3, -46.0), (-88.2, -51.5), (-85.2, -56.3),
                  (-82.8, -60.0), (-80.0, -64.5), (-77.0, -68.3), (-73.5, -71.6), east_pt(9.0, E["island"])])
    sle = smooth([(-86.0, AV_SOUTH), (-87.3, -20.0), (-87.5, -30.0), (-87.3, -40.0), (-86.2, -46.0), (-84.0, -51.5),
                  (-81.2, -56.0), (-78.6, -60.0), (-75.8, -64.0), (-72.5, -67.5), (-69.0, -70.2),
                  east_pt(13.5, E["slip"])])
    # the N6's westbound entry: its north kerb from the ring out (the long thin island's south edge)
    kn_e = smooth([rb_pt(11.0), rb_pt(5.0), (-82.9, -66.2), (-80.0, -69.6), (-76.5, -72.2), (-73.0, -73.6),
                   east_pt(9.0, E["entry"])])

    # ---------------- sidewalks, the plaza, verges (under the asphalt) ----------------
    layer("C south verge", None, circle(c, RB_OUT + 9.0, 48, 204.0, 302.0) + circle(c, RB_OUT + 2.0, 48, 204.0, 302.0)[::-1],
          ground="lawn")
    # lawn behind the N6's south sidewalk (the ground map's older road band showed there as a dark strip)
    layer("C east arm south verge", None, ribbon(ks_e[4:], E["walk_s"] - E["exit"] - 7.0, 0.0), ground="lawn")
    layer("C south sidewalk", "calcada", ribbon(kerb_s, -2.2, 0.4))
    layer("C north-west sidewalk", "calcada", ribbon(kw, -2.3, 0.4))
    # ... which the race road pushes north from the corner on: it runs beside the road (as along the Marginal)
    layer("C north-west sidewalk beside the road", "calcada",
          route_line(174.0, 185.0, -(KEEP_OUT - 0.1)) + route_line(174.0, 185.0, -(KEEP_OUT + 2.1))[::-1])
    plaza = (sle + [east_pt(E["end"], E["slip"]), east_pt(E["end"], E["walk_n"]), east_pt(34.0, E["walk_n"]),
                    (-48.0, -71.5), (-55.5, -66.0), (-66.0, -58.0), (-79.3, -47.5), (-79.9, -28.0), (-80.2, -18.0),
                    (-79.2, AV_SOUTH)])
    layer("C plaza north-east of the ring", "calcada", plaza)

    # ---------------- asphalt ----------------
    layer("C ring", "asphalt", circle(c, RB_OUT, RB_SEG))
    k_nw = next(k for k, p in enumerate(kw) if p[0] < -126.0)
    layer("C north arm", "asphalt", kw[:k_nw + 1] + [c] + ke_n[::-1])
    layer("C west arm", "asphalt", kw[k_nw - 1:] + ks_w + [c])
    layer("C east arm", "asphalt", sle + [east_pt(E["end"], E["slip"])] + ks_e[::-1] + [c, (-91.6, AV_SOUTH)])

    # ---------------- islands ----------------
    # the paved wedge between the middle roadway, the ring and the right-turn lane, and its long thin tail between
    # that lane and the N6's entry. North of y -31 the race road takes its place (it ends in a blunt nose there).
    tail = [east_pt(E["island_end"], E["entry"]), east_pt(E["island_end"] + 0.5, (E["entry"] + E["island"]) / 2),
            east_pt(E["island_end"], E["island"])]
    k_n = next(k for k, p in enumerate(slw) if p[1] < -31.2)
    k_e = next(k for k, p in enumerate(ke_n) if p[1] < -31.0)
    wedge = (ke_n[k_e:] + rb_arc(55.0, 11.0)[1:-1] + kn_e + tail + slw[k_n:][::-1]
             + [(-91.95, -30.7), (-92.5, -30.4), (-93.2, -30.4)])
    layer("C wedge island", "calcada", wedge)
    # splitter of the north arm (the lamp median widens into it). The race road runs over its east side and its
    # nose: the island keeps to the road's edge and ends in a round nose where less than 1.2 m of it is left.
    west = smooth([(-102.3, AV_SOUTH), (-102.9, -14.0), (-103.9, -19.0), (-104.9, -24.0), (-105.6, -29.0),
                   (-106.3, -33.0), (-107.2, -36.5), (-107.7, -38.6), (-108.0, -41.0)], 0.5)
    east = smooth([(-100.4, AV_SOUTH), (-101.6, -14.0), (-102.6, -19.0), (-103.3, -24.0), (-103.4, -29.0),
                   (-103.0, -34.0), (-102.5, -38.0), (-102.3, -41.0)], 0.5)

    def x_at(line, y):
        for (xa, ya), (xb, yb) in zip(line, line[1:]):
            if ya >= y >= yb:
                return xa + (xb - xa) * (ya - y) / ((ya - yb) or 1.0)
        return line[-1][0]
    rows, y = [], AV_SOUTH
    while y > -41.0:
        xw, xe = x_at(west, y), x_at(east, y)
        x = xw
        while x < xe and route_distance(x, y) >= KEEP_OUT + 0.02:
            x += 0.02
        if x - xw < 1.2 and rows:
            break
        rows.append(((xw, y), (x, y)))
        y -= 0.5
    for _ in range(3):                                   # no corner where the road's edge takes over from the kerb line
        rows = [rows[0]] + [(b[0], (min(b[1][0], (a[1][0] + 2 * b[1][0] + c_[1][0]) / 4), b[1][1]))
                            for a, b, c_ in zip(rows, rows[1:], rows[2:])] + [rows[-1]]
    (xw, y), (xe, _y) = rows[-1]
    r = (xe - xw) / 2
    nose = [(xw + r - r * math.cos(math.radians(a)), y - r * math.sin(math.radians(a))) for a in (30, 60, 90, 120, 150)]
    split_n = [w for w, _e in rows] + nose + [e for _w, e in rows][::-1]
    layer("C north splitter", "calcada", split_n)
    # splitter of the west arm (the Marginal's median widens into it)
    split_w = ([med_pt(-149.6, MED_HALF), (-144.0, -55.6), (-138.0, -56.5), (-131.9, -57.0), (-130.3, -57.05)]
               + rb_arc(163.0, 180.0, RB_OUT + 0.45) + [(-130.75, -64.85), med_pt(-149.6, -MED_HALF)])
    layer("C west splitter", "calcada", split_w)
    raised_w = clip_half(split_w, (-143.0, 0.0), (-143.0, -100.0))          # its tip stays flush (6.5 m from the race line)
    RAISED.append({"kind": "island", "ring": raised_w, "height": 0.13, "top": "calcada", "side": "kerb", "rim": 0.0})
    # splitter of the east arm and the N6's median
    split_e = (rb_arc(-17.0, -35.5, RB_OUT + 0.45) + [east_pt(1.6, -E["median"]), east_pt(E["end"], -E["median"]),
                                                     east_pt(E["end"], E["median"]), east_pt(1.6, E["median"])])
    layer("C east splitter", "calcada", split_e)
    RAISED.append({"kind": "island", "ring": split_e, "height": 0.13, "top": "calcada", "side": "kerb", "rim": 0.0})
    # the island: pale kerb ring, paved band, pale gravel bed with the corten beds (env_street_objects)
    # (the outer metre of the band tells the ground colour map "asphalt": beside the island the terrain shows in a
    # hand-wide strip at the foot of the race road's shoulder, and a pale rim would bleed into it)
    layer("C island band", "calcada", circle(c, RB_KERB, RB_SEG), ground="asphalt")
    layer("C island band", "calcada", circle(c, RB_KERB - 1.0, RB_SEG))
    layer("C island bed", None, circle(c, RB_BED, 72), ground="gravel")

    # ---------------- kerb stones (flush pale bands) ----------------
    for line, d0, d1 in ((kw, -0.2, 0.0), (kerb_s, -0.2, 0.0), ([q for q in ke_n if route_distance(*q) > KEEP_OUT + 0.3], 0.0, 0.2), (rb_arc(55.0, 11.0), 0.0, 0.2),
                         (kn_e, 0.0, 0.2), (slw[k_n:], -0.2, 0.0), (sle, 0.0, 0.2),
                         ([east_pt(9.0, E["island"]), east_pt(E["island_end"], E["island"])], -0.2, 0.0),
                         ([east_pt(9.0, E["entry"]), east_pt(E["island_end"], E["entry"])], 0.0, 0.2),
                         ([east_pt(13.5, E["slip"]), east_pt(E["end"], E["slip"])], 0.0, 0.2)):
        paint_ribbon("kerb", line, d0, d1, seg=3.0)
    for ring in (split_n, split_w):
        ring = ring if sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(ring, ring[1:] + ring[:1])) > 0 else ring[::-1]
        paint_ribbon("kerb", ring + ring[:1], 0.0, 0.2, seg=3.0)
    # ... and along the race road where paving begins at its edge: wedge, north splitter, north-west sidewalk
    for t0, t1, side in ((156.0, 171.0, 1), (152.5, 187.2, -1)):
        run, t = [], t0
        while t <= t1 + 0.126:
            lay = layer_at(*route_side(t, side * (KEEP_OUT + 0.25))) if t <= t1 + 1e-6 else None
            if lay is not None and lay["mat"] in ("calcada", "pal:brick_red") and lay["name"] != "C north splitter":
                # a sidewalk, not more asphalt (the north splitter has its kerb all round already)
                run.append(t)
            else:
                if len(run) > 1:
                    paint_ribbon("kerb", [route_side(k, side * (KEEP_OUT + 0.11)) for k in run], -0.09, 0.09, seg=3.0)
                run = []
            t += 0.125
    ko, ki = circle(c, RB_KERB, RB_SEG), circle(c, RB_KERB - RB_APRON_KERB, RB_SEG)
    band = circle(c, RB_KERB - 1.0, RB_SEG)
    for k in range(RB_SEG):
        k1 = (k + 1) % RB_SEG
        paint("kerb", [ki[k], ko[k], ko[k1], ki[k1]], road=True)
        if min(route_distance(*q) for q in (band[k], band[k1], ki[k], ki[k1])) < KEEP_OUT:
            # the race road's shoulder reaches 0.8 m into the island here: its paved band goes on as flush paint
            paint("render_white", [band[k], ki[k], ki[k1], band[k1]], road="fill")

    # ---------------- markings ----------------
    r_edge = RB_KERB + RB_EDGE_LINE[0]
    lo, hi = circle(c, r_edge - RB_EDGE_LINE[1] / 2, RB_SEG), circle(c, r_edge + RB_EDGE_LINE[1] / 2, RB_SEG)
    for k in range(RB_SEG):
        k1 = (k + 1) % RB_SEG
        paint("white", [lo[k], hi[k], hi[k1], lo[k1]], road=True)
    r_lane, w_lane, dash, gap = RB_LANE
    dashes(circle(c, r_lane, 180, 8.0, 367.9), 0.0, w_lane, dash, gap, road=True)
    # give-way lines: thick dashes across the three entries
    for line in ([(-108.15, -41.55), (-117.2, -42.3)], rb_arc(182.0, 201.0, RB_OUT + 0.3, 1.0),
                 rb_arc(4.0, -16.0, RB_OUT + 0.3, 1.0)):
        dashes(line, 0.0, 0.4, 0.5, 0.5, road=True)
    # lane lines of the arms (dashed) and the solid lines along the splitters
    dashes(smooth([(-105.75, AV_SOUTH - 0.5), (-106.6, -16.0), (-107.95, -22.0), (-108.9, -28.0), (-109.3, -32.5)]),
           0.0, 0.14, 3.0, 4.0)
    dashes([(-149.2, -58.25), (-140.6, -62.2)], 0.0, 0.14, 5.0, 2.0)
    dashes([east_pt(9.0, 2.8), east_pt(E["end"], 2.8)], 0.0, 0.14, 3.0, 4.0)
    dashes([east_pt(11.0, -3.2), east_pt(E["end"], -3.2)], 0.0, 0.14, 3.0, 4.0)
    for d in (E["median"] + 0.3, -E["median"] - 0.3):
        paint_ribbon("white", [east_pt(2.5, d), east_pt(E["end"], d)], -0.06, 0.06, seg=5.0)
    paint_ribbon("white", [east_pt(E["island_end"] + 1.0, E["entry"] - 0.3), east_pt(10.0, E["entry"] - 0.3)], -0.06, 0.06, seg=5.0)
    paint_ribbon("white", [(-146.0, -57.25), (-137.7, -61.7)], -0.06, 0.06, seg=5.0)         # west splitter, south side
    # zebra crossings (bars along the road), each from kerb to kerb
    zebra((-113.3, -35.8), (-107.05, -35.8), 1.8, along=(0.0, 1.0), keep=on_asphalt)                     # north arm, entry
    zebra((-105.6, -37.2), (-94.2, -37.2), 1.8, along=(0.0, 1.0), keep=on_asphalt, road=True)           # ... exit = race road
    zebra((-91.65, -36.4), (-87.45, -36.4), 1.8, along=(0.0, 1.0), keep=on_asphalt)                      # right-turn lane
    zebra((-133.9, -56.9), (-133.9, -44.85), 2.1, along=(1.0, 0.0), keep=on_asphalt, road=True)         # west arm, exit = race road
    zebra((-135.2, -62.45), (-137.35, -66.45), 2.0, along=unit(17.9, -9.56), keep=on_asphalt)           # west arm, entry
    zebra((-82.3, -73.1), (-80.7, -68.7), 2.0, along=EAST_U, keep=on_asphalt)                             # east arm, entry
    zebra((-85.8, -77.8), (-85.8, -84.3), 2.2, along=(1.0, 0.0), keep=on_asphalt)                        # east arm, exit
    zebra((-78.45, -66.45), (-75.3, -63.95), 1.8, along=unit(3.0, -3.8), keep=on_asphalt)                # right-turn lane, east
    # red-brown block paving where the north arm's crossings wait (after the zebras: on_asphalt looks at the layers)
    k0, k1 = (next(k for k, p in enumerate(kw) if p[1] < y) for y in (-34.0, -37.6))
    layer("C zebra wait", "pal:brick_red", ribbon(kw[k0 - 1:k1 + 1], -2.1, -0.2))
    layer("C zebra wait", "pal:brick_red", clip_band_y(split_n, -38.6, -34.2))
    layer("C zebra wait", "pal:brick_red", clip_band_y(wedge, -38.6, -34.6))
    k0, k1 = (next(k for k, p in enumerate(sle) if p[1] < y) for y in (-34.6, -38.2))
    layer("C zebra wait", "pal:brick_red", ribbon(sle[k0 - 1:k1 + 1], 0.2, 2.2))


# =====================================================================================================
# Sections A + B: avenue N6-7. Three parallel roadways; the route runs in the middle one (x = -96).
# AVENUE = axis at x = -96, south to north; offsets to the left = west. North of y = 37 the avenue leans north-north-west.
# =====================================================================================================
# V18g: north of y = 100 the avenue bends on to the north-north-west (median centre on the orthophoto: x -110.7 at
# y 116, -113.8 at y 132, -118 at y 148); it ran straight on before and lay 6 m too far east at the campus entrance.
AVENUE = [(-96.0, -46.0), (-96.0, 37.0), (-101.9, 100.0), (-105.4, 116.0), (-108.5, 132.0), (-112.7, 148.0), (-116.4, 160.0)]
AV_WEST_WALK = (13.2, 15.5)
AV_WEST_ROAD = (6.2, 13.2)
AV_MEDIAN = (4.3, 6.2)
AV_MID_ROAD = (-2.5, 4.2)
AV_SEPARATOR = (-4.2, -2.6)        # only north of y = +19
AV_SLIP = (-10.0, -4.4)
AV_EAST_WALK = (-13.3, -10.0)
# V18g: centre line of the second path east of the avenue (orthophoto), north to south, then south-east along the
# campus embankment to where the ground map's own path goes on
AV_EAST_PATH = [(-84.2, 99.0), (-82.9, 88.0), (-82.3, 78.0), (-80.2, 66.0), (-79.2, 58.0), (-78.4, 48.0), (-77.3, 42.0),
                (-76.6, 34.0), (-75.5, 24.0), (-74.3, 14.0), (-73.3, 6.0), (-72.7, -2.0), (-71.5, -12.0), (-70.2, -24.0),
                (-69.0, -34.0), (-65.5, -40.0), (-58.0, -45.2), (-50.0, -50.5), (-40.0, -56.3), (-30.0, -62.0)]


def av_s(y):
    """Arc length along AVENUE at a given y (the axis runs steadily north)."""
    acc = 0.0
    for a, b in zip(AVENUE, AVENUE[1:]):
        seg = math.hypot(b[0] - a[0], b[1] - a[1])
        if y <= b[1] or b is AVENUE[-1]:
            return acc + seg * (y - a[1]) / (b[1] - a[1])
        acc += seg


def av_pt(y, d):
    """Point at offset d (west positive) from the avenue axis at a given y."""
    p, u = at_s(AVENUE, av_s(y))
    return pt(p, u, 0.0, d)


def avenue():
    north = 100.0
    south = AV_SOUTH                                  # V18r: south of it the roundabout section draws the north arm
    whole = cut(AVENUE, av_s(south), av_s(north))
    # the dusty car park west of the avenue (2023 state) and the embankment lawn east of it: ground colours only
    layer("B east verge", None, ribbon(cut(AVENUE, av_s(-20.0), av_s(north)), AV_EAST_WALK[0] - 9.0, AV_EAST_WALK[0]),
          ground="lawn")
    layer("B asphalt", "asphalt", ribbon(whole, AV_SLIP[0], AV_WEST_ROAD[1]))
    layer("B west sidewalk", "calcada", ribbon(cut(AVENUE, av_s(south), av_s(north)), AV_WEST_WALK[0], AV_WEST_WALK[1]))
    paint_ribbon("kerb", cut(AVENUE, av_s(south), av_s(north)), AV_WEST_ROAD[1] - 0.02, AV_WEST_WALK[0] + 0.14)
    # lamp median (V18r: its widening into the north splitter, the paved wedge, the right-turn lane's bend, the zebras
    # and the apron in front of the campus embankment are the roundabout section's now)
    layer("B lamp median", "calcada", ribbon(cut(AVENUE, av_s(south), av_s(north)), AV_MEDIAN[0], AV_MEDIAN[1]))
    # the race line (8 m wide) covers the middle roadway and the real median's place: the median lies beside the road
    layer("B lamp median beside the road", "calcada", route_ribbon(133, 152, -1, KEEP_OUT - 0.1, KEEP_OUT + 1.8))
    layer("B separator", "calcada", ribbon(cut(AVENUE, av_s(19.0), av_s(north)), AV_SEPARATOR[0], AV_SEPARATOR[1]))
    # east sidewalk, widening southwards
    # (V18g, orthophoto: 6 m wide at y -2, 3.3 m at y 34, 2.3 m north of y 42; a lawn strip and a second, 4.4 m path
    # lie east of it, the path bending off south-east along the campus embankment at y -38)
    walk = ([av_pt(north, AV_EAST_WALK[1]), av_pt(37.0, AV_EAST_WALK[1]), av_pt(south, AV_EAST_WALK[1]), (-79.2, south),
             (-80.5, -3.0), (-81.3, 14.0), (-82.3, 24.0), (-82.8, 34.0), (-83.7, 42.0), av_pt(66.0, AV_EAST_WALK[1] - 2.3),
             av_pt(north, AV_EAST_WALK[1] - 2.3)])
    layer("B east sidewalk", "calcada", walk)
    layer("B east path", "calcada", ribbon(smooth(AV_EAST_PATH, 4.0), -2.2, 2.2))
    paint_ribbon("kerb", cut(AVENUE, av_s(south), av_s(north)), AV_EAST_WALK[1] - 0.14, AV_SLIP[0] + 0.02)
    # markings: dashed lane lines, transverse double bars on the west roadway, the chevron gore at the bollards
    dashes(cut(AVENUE, av_s(south), av_s(north)), (AV_WEST_ROAD[0] + AV_WEST_ROAD[1]) / 2, 0.14, 3.0, 4.0)
    dashes(cut(AVENUE, av_s(20.0), av_s(north)), -7.2, 0.14, 3.0, 4.0)
    # V18g: the middle roadway's own lane line (two lanes, satellite view): the race road lies on this roadway from
    # y 34 south, so the line runs on across it (flush paint in Track_EdgeLines)
    dashes(cut(AVENUE, av_s(south + 0.5), av_s(north)), (AV_MID_ROAD[0] + AV_MID_ROAD[1]) / 2, 0.14, 3.0, 4.0, road=True)
    for y in (91.0, 73.0, 54.6, 38.2, 27.0, 17.4, 9.9, 3.7, 1.2, -1.3, -3.8, -6.3, -8.8, -10.9, -12.6):
        for dy in (0.0, 0.75):                                     # (V18g: also the four pairs north of y 30)
            a, b = av_pt(y + dy, AV_WEST_ROAD[1] - 0.2), av_pt(y + dy, AV_WEST_ROAD[0] + 0.2)
            paint("white", [a, b, (b[0], b[1] + 0.4), (a[0], a[1] + 0.4)])
    for k in range(7):                                             # chevron hatching on the gore, y +20 .. 0
        y = 18.0 - k * 2.8
        w = 0.5 + 1.5 * k / 6
        paint("white", [(-92.9, y), (-92.9 + w, y - 1.4), (-92.9 + w, y - 1.9), (-92.9, y - 0.5)], road=True)


# =====================================================================================================
# V16 (photospheres ST01, ST02, N15; survey 3.9, item 29): the avenue's entrance side, y +100 .. +148. The street
# section ended at y = 100; here the roadways run on to the campus entrance: the east lane becomes the red-brown bus
# lay-by (3.5 m x 45 m), a calcada strip joins it to the entrance plaza (env_entrance), flush, no kerb.
# The steel posts along the lay-by, the pylon and the flag poles are env_north's.
# =====================================================================================================
AV_ENTRANCE = (100.0, 148.0)        # y range of this piece (the avenue's axis is extended in its last direction)
AV_LAYBY = (107.5, 129.0)           # y range of the bus bay (V18g: it was a 45 m strip, y 100.5 .. 145.5)
AV_LAYBY_W = 3.5                    # (the old strip's width; the bay is a lens now, see avenue_entrance)
AV_LAYBY_BULGE = 3.2                # m: how far the bay's round rim reaches east of the lane's own edge
AV_PLAZA_X = -89.8                  # the calcada strip reaches 0.2 m into the entrance plaza's ring (west edge x = -90)


def av_entrance_line(y0, y1):
    """The avenue's axis at y0 and y1 as a two-point line (env_north puts the lay-by's posts beside it)."""
    return [av_pt(y, 0.0) for y in (y0, y1)]


def av_layby_rim(n=14):
    """The bus bay's round east rim, south to north (V18g: env_north's posts belong 0.4 m outside this line)."""
    y0, y1 = AV_LAYBY
    return [av_pt(y0 + (y1 - y0) * k / n,
                  AV_SLIP[1] + (AV_SLIP[0] - AV_SLIP[1] - AV_LAYBY_BULGE) * math.sin(math.pi * k / n) ** 0.6)
            for k in range(n + 1)]


def avenue_entrance():
    y0, y1 = AV_ENTRANCE
    line = cut(AVENUE, av_s(y0), av_s(y1))
    layer("B2 asphalt", "asphalt", ribbon(line, AV_SLIP[0], AV_WEST_ROAD[1]))
    layer("B2 west sidewalk", "calcada", ribbon(line, AV_WEST_WALK[0], AV_WEST_WALK[1]))
    paint_ribbon("kerb", line, AV_WEST_ROAD[1] - 0.02, AV_WEST_WALK[0] + 0.14)
    layer("B2 lamp median", "calcada", ribbon(line, AV_MEDIAN[0], AV_MEDIAN[1]))
    layer("B2 separator", "calcada", ribbon(line, AV_SEPARATOR[0], AV_SEPARATOR[1]))
    east = offset(line, AV_SLIP[0])
    layer("B2 plaza strip", "calcada", east + [(AV_PLAZA_X, y1), (AV_PLAZA_X, y0)])
    # V18g (satellite view): the bus bay is a red-brown lens, 21 m long: the whole east lane from the separator's
    # kerb, bulging 4 m into the plaza with a round pale kerb. No word on it.
    rim = av_layby_rim()
    west = [av_pt(AV_LAYBY[0] + (AV_LAYBY[1] - AV_LAYBY[0]) * k / 4, AV_SLIP[1]) for k in range(5)]
    layer("B2 bus lay-by", "pal:brick_red", west + rim[::-1])
    paint_ribbon("kerb", rim[1:-1], -0.2, 0.0, seg=3.0)
    dashes(line, (AV_WEST_ROAD[0] + AV_WEST_ROAD[1]) / 2, 0.14, 3.0, 4.0)
    dashes(line, (AV_MID_ROAD[0] + AV_MID_ROAD[1]) / 2, 0.14, 3.0, 4.0)
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
# V18g: the dusty car park north-west of the roundabout (between the avenue and the Avenida Marginal). Satellite view
# (2026): bare pale dust, parked cars in loose rows, dark scrub in large patches, a dry grass verge with bushes along
# both streets. It was a meadow with paths and saplings here before. Ground colours only (the terrain shows them);
# the cars and bushes are env_street_objects.dustlot_placements(). Drawn first: the streets' own layers lie on top.
# =====================================================================================================
DUST_SCRUB = [      # outlines of the scrub patches (satellite view, +-2 m)
    [(-215.3, 120.9), (-212.0, 150.0), (-172.0, 150.0), (-169.5, 114.0), (-162.0, 90.0), (-157.5, 62.0), (-161.0, 38.0),
     (-171.7, 24.8), (-182.6, 27.0), (-191.0, 38.0), (-200.0, 46.6), (-208.8, 57.5), (-213.0, 79.4), (-217.5, 101.0)],
    [(-241.5 + 9.0 * math.cos(math.radians(a)), 84.0 + 7.0 * math.sin(math.radians(a))) for a in range(0, 360, 30)],
    [(-278.0, 98.0), (-282.0, 150.0), (-238.0, 150.0), (-226.0, 122.0), (-228.0, 100.0), (-244.0, 96.0)],
]
DUST_ROWS = [       # parked cars: polyline of a row's middle, cars square to it; double = two rows nose to nose
    dict(line=[(-170.6, -10.0), (-170.6, -31.0)], double=False, share=0.7),      # the grid at the roundabout's corner
    dict(line=[(-153.0, -9.0), (-153.0, -33.0)], double=True, share=0.6),
    dict(line=[(-142.0, -9.0), (-142.0, -34.0)], double=False, share=0.7),
    dict(line=[(-133.0, -15.0), (-134.0, 20.0), (-135.0, 58.0)], double=False, share=0.62),   # columns beside the avenue
    dict(line=[(-144.0, -4.0), (-144.5, 30.0)], double=False, share=0.45),
    dict(line=[(-265.5, 34.0), (-268.0, 50.0), (-271.0, 70.0), (-273.0, 95.0)], double=True, share=0.3),   # rows further west
    dict(line=[(-252.0, 30.0), (-250.5, 50.0), (-249.0, 66.0)], double=False, share=0.45),
    dict(line=[(-287.0, 48.0), (-292.0, 62.0), (-297.0, 82.0), (-302.0, 99.0)], double=False, share=0.4),
]
DUST_ROAD_ROW = dict(x=(-296.0, -176.0), offset=17.6, share=0.7)   # the row along the Marginal: m north of the median


def dust_outline():
    south = [med_pt(x, N_WALK[1]) for x in (-335.0, -300.0, -270.0, -240.0, -216.6, -195.1, -172.3, -150.0)]
    corner = [(-126.0, -46.5), (-118.5, -41.5), (-114.6, -34.0), (-113.3, -22.0), (-111.8, -10.0)]
    east = [av_pt(y, AV_WEST_WALK[1]) for y in (0.0, 37.0, 100.0, 116.0, 132.0, 148.0, 160.0)]
    return south + corner + east + [(-335.0, 160.0)]


def dustlot():
    layer("L dust lot", None, dust_outline(), ground="dust")
    for ring in DUST_SCRUB:
        layer("L scrub", None, smooth(ring + ring[:1], 3.0)[:-1], ground="scrub")
    # dry grass verges between the sidewalks and the parked cars (a wire fence on timber posts stood here in 2022)
    layer("L verge at the Marginal", None, ribbon(cut(MEDIAN, med_s(-300.0), med_s(-150.0), step=6.0), N_WALK[1],
                                                  N_WALK[1] + 6.0), ground="meadow")
    layer("L verge at the avenue", None, ribbon(cut(AVENUE, av_s(-12.0), av_s(150.0)), AV_WEST_WALK[1],
                                                AV_WEST_WALK[1] + 11.0), ground="meadow")


# =====================================================================================================
# V18g: the N6 (Avenida Marginal) east of the roundabout, from where the roundabout section ends (52 m along its east
# arm, x -30) to the junction at x 180. Orthophoto: kerb lines by brightness profiles across the road every 15-20 m
# (+-0.7 m); satellite view and Street View (May 2026) for the markings and what the surfaces are. Two carriageways
# either side of a median paved in grey calcada between pale kerbs, which widens from 1 m to 4 m; the westbound
# carriageway gains a third lane towards the roundabout; calcada sidewalks; south of the road a grass strip with date
# palms and, from x 40, a pale concrete car park with one row of bays, noses to the road.
# The beach tunnel passes 4 m under the road at x 15-40 (route_distance leaves the tunnel's points out).
# Each row: x, y of the median's centre, its half width, north kerb, south kerb (m from the centre; south negative).
# =====================================================================================================
N6E = [(-29.64, -94.38, 0.5, 9.2, -5.85), (-15.0, -98.2, 0.5, 9.7, -6.3), (0.0, -100.3, 0.5, 9.3, -5.9),
       (15.0, -101.2, 0.85, 9.7, -6.1), (30.0, -101.7, 1.35, 10.4, -6.8), (45.0, -102.2, 1.5, 10.9, -6.4),
       (60.0, -102.5, 1.8, 11.3, -7.0), (80.0, -102.7, 2.0, 10.2, -7.7), (100.0, -102.4, 1.65, 8.2, -8.1),
       (120.0, -102.3, 1.65, 7.9, -8.2), (140.0, -102.2, 1.85, 7.7, -8.3), (160.0, -102.4, 1.9, 7.3, -9.0),
       (180.0, -102.7, 2.0, 7.2, -11.3)]
N6E_WALK = (3.6, 2.0)               # widths of the north and the south sidewalk
N6E_LOT = [(40.0, -115.6), (60.0, -116.4), (100.0, -115.8), (140.0, -117.0), (169.0, -117.6), (170.0, -130.0),
           (40.0, -129.0)]          # the concrete car park south of the road
N6E_BAYS = (58.0, 168.0)            # x range of its bay row (along the north edge, 5 m deep)
N6E_PORTAL_CUT = ((2.6, -104.5), (11.0, -112.0))       # box round the terrain's cut under the portal deck (x, y corners)
N6E_PALMS = [(47.0, -113.2), (118.0, -113.5), (127.0, -113.5), (137.0, -113.4), (166.0, -113.6)]


def n6e_lot_edge(x):
    """y of the car park's north edge (the front of its bay row) at a given x."""
    for a, b in zip(N6E_LOT[:5], N6E_LOT[1:5]):
        if a[0] <= x <= b[0]:
            return a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0])
    return N6E_LOT[4][1]


def n6e_rows(step=5.0):
    rows = []
    for a, b in zip(N6E, N6E[1:]):
        n = max(1, int(round((b[0] - a[0]) / step)))
        rows += [tuple(a[k] + (b[k] - a[k]) * j / n for k in range(5)) for j in range(n)]
    return rows + [N6E[-1]]


def n6e_line(col, step=5.0):
    """Polyline at an offset from the median's centre line (north positive): col = a column of N6E or a function of
    its row."""
    rows = n6e_rows(step)
    centre = [(r[0], r[1]) for r in rows]
    out = []
    for k, r in enumerate(rows):
        a, b = centre[max(k - 1, 0)], centre[min(k + 1, len(rows) - 1)]
        nrm = left_of(unit(b[0] - a[0], b[1] - a[1]))
        d = col(r) if callable(col) else r[col]
        out.append((r[0] + nrm[0] * d, r[1] + nrm[1] * d))
    return out


def n6_east():
    wn, ws = N6E_WALK
    med_n, med_s_ = n6e_line(lambda r: r[2]), n6e_line(lambda r: -r[2])
    kerb_n, kerb_s = n6e_line(3), n6e_line(4)
    walk_n, walk_s = n6e_line(lambda r: r[3] + wn), n6e_line(lambda r: r[4] - ws)
    # the paved triangle north-east of the ring reaches further north-east than the roundabout section drew it
    # (orthophoto: its edge runs from (-79, -48) through (-45, -67) and (-30, -76) to the sidewalk at x -17)
    E = EAST
    layer("N plaza north-east of the ring", "calcada",
          [(-79.3, -47.5), (-45.0, -67.3), (-30.0, -75.7), (-17.5, -83.0), east_pt(E["end"], E["walk_n"]),
           east_pt(34.0, E["walk_n"]), (-48.0, -71.5), (-55.5, -66.0), (-66.0, -58.0)])
    # grass south of the road (the palms' strip), the car park, the road, the sidewalks, the median
    layer("N grass strip", None, kerb_s + n6e_line(lambda r: r[4] - 9.0)[::-1], ground="lawn")
    conc_uv = frame_uv(N6E_LOT[0], (1.0, 0.0), 4.0, 4.0)
    layer("N car park", "concrete", N6E_LOT, conc_uv)
    x0, x1 = N6E_BAYS
    n = int((x1 - x0) / BAY[0])
    top = [(x0 + (x1 - x0) * k / 8, n6e_lot_edge(x0 + (x1 - x0) * k / 8)) for k in range(9)]
    layer("N bays", "bays", top + [(x, y - BAY[1]) for x, y in top][::-1], frame_uv(top[0], (1.0, 0.0), (x1 - x0) / n, -BAY[1]))
    layer("N asphalt", "asphalt", kerb_n + kerb_s[::-1], hull=1.2)     # (hull: the terrain has sharp creases here)
    layer("N north sidewalk", "calcada", walk_n + kerb_n[::-1])
    layer("N south sidewalk", "calcada", kerb_s + walk_s[::-1])
    # the beach tunnel's south portal: the terrain is cut down to the tunnel's floor under the portal's deck, which
    # reaches 3 m under the road's south edge. There the paving lies on the hull of the ground round the cut.
    for name, mat_, ring in (("N asphalt over the portal", "asphalt", kerb_n + kerb_s[::-1]),
                             ("N south sidewalk over the portal", "calcada", kerb_s + walk_s[::-1])):
        (ax, ay), (bx, by) = N6E_PORTAL_CUT
        for a, b in (((ax, ay), (bx, ay)), ((bx, ay), (bx, by)), ((bx, by), (ax, by)), ((ax, by), (ax, ay))):
            ring = clip_half(ring, b, a)
        if len(ring) >= 3:
            layer(name, mat_, ring, hull=4.6)
    layer("N median", "calcada", med_n + med_s_[::-1])
    RAISED.append({"kind": "island", "ring": med_n + med_s_[::-1], "height": 0.13, "top": "calcada", "side": "kerb", "rim": 0.0})
    for line, d0, d1 in ((kerb_n, 0.0, 0.2), (kerb_s, -0.2, 0.0)):
        paint_ribbon("kerb", line, d0, d1, seg=5.0)
    # markings: solid lines beside the median, dashed lane lines (the westbound carriageway has three lanes where it
    # is wider than 9 m), a zebra across both carriageways before the junction
    paint_ribbon("white", n6e_line(lambda r: r[2] + 0.3), -0.06, 0.06, seg=5.0)
    paint_ribbon("white", n6e_line(lambda r: -r[2] - 0.3), -0.06, 0.06, seg=5.0)
    dashes(n6e_line(lambda r: r[2] + min(3.3, (r[3] - r[2]) / 2.0)), 0.0, 0.14, 3.0, 4.0)
    third = [q for q, r in zip(n6e_line(lambda r: r[2] + 6.6), n6e_rows()) if r[3] - r[2] > 8.6]
    dashes(third, 0.0, 0.14, 3.0, 4.0)
    dashes(n6e_line(lambda r: (r[4] - r[2]) / 2.0), 0.0, 0.14, 3.0, 4.0)
    for a, b in ((n6e_line(lambda r: r[2] + 0.4)[-2], n6e_line(lambda r: r[3] - 0.3)[-2]),
                 (n6e_line(lambda r: -r[2] - 0.4)[-2], n6e_line(lambda r: r[4] + 0.3)[-2])):
        zebra(a, b, 2.0, along=(1.0, 0.0))


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
    dustlot()              # V18g: under everything else
    marginal()
    forecourt()
    carpark()
    avenue()
    avenue_entrance()      # V16
    roundabout()           # V18r: after the avenue, its north arm lies on top of the avenue's south end
    n6_east()              # V18g: the N6 from the roundabout's east arm to the junction at x 180
    promenade()            # V15c


build()
