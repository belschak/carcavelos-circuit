"""V18i (Luc after his drive, 2026-11-12): the ground inside the Capricciosa hairpin is ONE smooth drivable surface.

Luc cuts the hairpin: from the Marginal's entry throat straight across the inside (lawn tip, calcada band, apron) to
the car-park aisle. A ray scan of the top collider on a 0.25 m grid (atlas report V18i-f) found there:
  - a step of up to 35 cm in an arc 2-3 m from the loop's centre: the corridor terrain's rows of the entry leg, of the
    loop and of the exit leg overlap inside the hairpin, each with its own shoulder profile, and the highest one wins;
    the paving is draped over that seam (V18h-q: "the paving lies up to 12.8 cm under the ground")
  - lips of 5-8 cm wherever a paving layer ends at the lawn (the paving lies 7 cm above the terrain)
  - kinks of the bank's foot under the car park's north-east edge
The race road is not touched. What changes, only inside the zone (the inside of the hairpin, closed in the east by
the chord between two route points):
  height()        one height field H: a thin plate (least bending) over a 1 m grid, clamped to the built ground
                  beside the road (the foot of its shoulder, same cross slope) along the three road sides and to the
                  ground as it is along the chord. Bicubic between the nodes: no step, no kink. In the first 1.5 m
                  beside the road and the last 4 m before the chord it runs over into the built ground.
  sink_terrain()  the terrain meshes (corridor rows, 5 m grid) are pushed under H inside the zone
  layer()         a paving layer over the whole zone with the terrain's own material and UVs (ground map): the lawn
                  is now part of the welded Street_Paving mesh like the calcada and the concrete, so a kart never
                  rolls from one collider onto another in here (V10i: that kicks karts even when both are level)
  lift()          the paving's 7 cm over the ground fall to nothing where that lawn layer ends at the chord
env_streets asks this module in ground(), _paving_z(), build_paving() and build_paint(); build_world calls apply()
(= plan() + sink_terrain()) once all ground meshes exist, right before the streets are built. Everything that stands in the zone (bins, racks, posts, cars) takes its height from
env_streets.ground() and so stands on H.
"""
import math

import env_common as E
import street_plan as S

ENTRY, EXIT = (-211.8, -27.6), (-212.0, -64.7)     # the chord's ends: the route points nearest to these (entry leg, exit leg)
FIX_D = 5.6               # m from the centreline: nodes this close keep the road bed's height (the plate is clamped to it)
CELL = 1.0                # m between the nodes of H
TENSION = 0.03            # a little membrane in the plate: no bulge between the two legs
MESH_UP = 0.03            # the corridor mesh lies this far above the analytic surface beside the road (env_corridor.build_mesh)
SINK = 0.10               # m: corridor terrain under H ...
SINK_COARSE = 0.40        # ... and the 5 m grid (its cells are flat over 5 m, H is not)
SINK_FROM, SINK_OVER = 5.6, 1.0     # m from the centreline: from here, over this distance, the terrain sinks (not
                                    # nearer: the corridor's strip between the shoulder and the paving's edge shows)
CLOSE = 3.0               # m before the chord over which the sink and the lawn layer's lift fade out
ROAD_BED = 4.3            # m from the centreline: nodes closer than this lie on the road itself
BLEND_ROAD = 1.5          # m from the paving's edge at the road over which H leaves the built ground
BLEND_CHORD = 4.0         # ... and before the chord over which it returns to it
MAX_EDGE = 1.6            # m: paving and paint triangles in the zone (facets of a curved surface)
LAYER_NAME = "V18i hairpin ground"

_st = {"ring": None, "box": None, "z": None, "prev": None, "chord": None, "paved": None, "tree": None}


def smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def _ring():
    pts = E.route_points()
    lo, hi = 200, 300
    ia = min(range(lo, hi), key=lambda i: math.hypot(pts[i]["x"] - ENTRY[0], pts[i]["y"] - ENTRY[1]))
    ib = min(range(lo, hi), key=lambda i: math.hypot(pts[i]["x"] - EXIT[0], pts[i]["y"] - EXIT[1]))
    if not (ia < ib and math.hypot(pts[ia]["x"] - ENTRY[0], pts[ia]["y"] - ENTRY[1]) < 3.0
            and math.hypot(pts[ib]["x"] - EXIT[0], pts[ib]["y"] - EXIT[1]) < 3.0):
        raise RuntimeError("hairpin_flush: the route no longer passes the hairpin where this module expects it")
    return [(pts[i]["x"], pts[i]["y"]) for i in range(ia, ib + 1)]


def plan():
    """After env_corridor.build_data() and the other ground plans: solve H and make E.ground_z return it in the zone."""
    import numpy as np
    import env_streets
    _st["tree"] = env_streets._ground_tree()        # the terrain meshes as built (before sink_terrain)
    ring = _ring()
    _st["ring"] = ring
    _st["chord"] = (ring[-1], ring[0])
    prev = E._surface["fn"] or E.coarse_ground_z
    _st["prev"] = prev
    x0 = math.floor(min(p[0] for p in ring)) - 4.0
    y0 = math.floor(min(p[1] for p in ring)) - 4.0
    nx = int(math.ceil(max(p[0] for p in ring)) + 4.0 - x0) + 1
    ny = int(math.ceil(max(p[1] for p in ring)) + 4.0 - y0) + 1
    z = np.zeros((ny, nx))
    free = {}
    for j in range(ny):
        for i in range(nx):
            x, y = x0 + i * CELL, y0 + j * CELL
            d = S.route_distance(x, y)
            z[j, i] = prev(x, y) + MESH_UP          # on the road: its bed (the corridor mesh lies 5 cm lower under it)
            if d >= ROAD_BED:                       # elsewhere: the built ground as it is, i.e. the highest of the
                g = _built(x, y)                    # overlapping corridor rows, not the analytic profile of one
                if g is not None:
                    z[j, i] = g
            if S.inside(x, y, ring) and d > FIX_D:
                free[(i, j)] = len(free)
    rows, rhs = [], []

    def equation(terms, weight):                    # sum(coefficient * z[node]) = 0, known nodes go to the right side
        row, b = {}, 0.0
        for (i, j), c in terms:
            if (i, j) in free:
                row[free[(i, j)]] = row.get(free[(i, j)], 0.0) + c * weight
            else:
                b -= c * weight * z[j, i]
        if row:
            rows.append(row)
            rhs.append(b)
    for j in range(ny):
        for i in range(nx):
            if 0 < i < nx - 1:
                equation([((i - 1, j), 1.0), ((i, j), -2.0), ((i + 1, j), 1.0)], 1.0)
            if 0 < j < ny - 1:
                equation([((i, j - 1), 1.0), ((i, j), -2.0), ((i, j + 1), 1.0)], 1.0)
            if i < nx - 1 and j < ny - 1:
                equation([((i, j), 1.0), ((i + 1, j), -1.0), ((i, j + 1), -1.0), ((i + 1, j + 1), 1.0)], math.sqrt(2.0))
            if i < nx - 1:
                equation([((i, j), 1.0), ((i + 1, j), -1.0)], TENSION)
            if j < ny - 1:
                equation([((i, j), 1.0), ((i, j + 1), -1.0)], TENSION)
    a = np.zeros((len(rows), len(free)))
    for r, row in enumerate(rows):
        for c, v in row.items():
            a[r, c] = v
    sol = np.linalg.lstsq(a, np.array(rhs), rcond=None)[0]
    before = [z[j, i] for (i, j) in free]
    for (i, j), k in free.items():
        z[j, i] = sol[k]
    _st["z"] = [[float(v) for v in row] for row in z]
    _st["box"] = (x0, y0, nx, ny)
    change = [sol[k] - before[k] for k in range(len(free))]
    print(f"  hairpin: one smooth ground inside the hairpin, {len(free)} nodes of {CELL:g} m solved (thin plate, clamped to "
          f"the road bed and to the ground at the chord x = {ring[0][0]:.1f}); against the old surface "
          f"{min(change):+.2f} .. {max(change):+.2f} m")

    def surface(x, y):
        h = height(x, y)
        return prev(x, y) if h is None else h
    E.set_surface(surface)


def _built(x, y):
    """The terrain as it was built (before sink_terrain), or None."""
    from mathutils import Vector
    hit = _st["tree"].ray_cast(Vector((x, y, 200.0)), Vector((0.0, 0.0, -1.0)), 400.0)
    return hit[0].z if hit[0] is not None else None


def in_zone(x, y):
    ring = _st["ring"]
    return ring is not None and S.inside(x, y, ring)


def chord_distance(x, y):
    (ax, ay), (bx, by) = _st["chord"]
    return S.seg_distance(x, y, ax, ay, bx, by)


def _cubic(p0, p1, p2, p3, t):                      # Catmull-Rom
    return p1 + 0.5 * t * (p2 - p0 + t * (2 * p0 - 5 * p1 + 4 * p2 - p3 + t * (3 * (p1 - p2) + p3 - p0)))


def height(x, y):
    """H at (x, y), or None outside the zone and on the road itself (closer than its shoulder's foot).
    Within BLEND_ROAD of the paving's edge at the road and within BLEND_CHORD of the chord, H runs over into the
    ground as it was built: the paving's edge at the road lies on the real foot of the shoulder as before, and the
    bank beyond the chord is met without a line."""
    if _st["z"] is None or not in_zone(x, y):
        return None
    d = S.route_distance(x, y)
    if d < S.KEEP_OUT - 0.05:
        return None
    w = smooth((d - S.KEEP_OUT) / BLEND_ROAD) * smooth(chord_distance(x, y) / BLEND_CHORD)
    built = _built(x, y) if w < 1.0 else None
    if built is None:
        w = 1.0
    elif w <= 0.0:
        return built
    x0, y0, nx, ny = _st["box"]
    fx, fy = (x - x0) / CELL, (y - y0) / CELL
    i, j = int(math.floor(fx)), int(math.floor(fy))
    if not (1 <= i < nx - 2 and 1 <= j < ny - 2):
        return None
    tx, ty = fx - i, fy - j
    z = _st["z"]
    plate = _cubic(*[_cubic(z[j + b][i - 1], z[j + b][i], z[j + b][i + 1], z[j + b][i + 2], tx) for b in (-1, 0, 1, 2)], ty)
    return plate if w >= 1.0 else w * plate + (1 - w) * built


def sink_terrain():
    """After every ground mesh is built: push what lies in the zone under H (never up)."""
    import bpy
    col = bpy.data.collections.get("World_Ground")
    count, deepest = 0, 0.0
    for obj in (col.objects if col else ()):
        if obj.type != "MESH":
            continue
        depth = SINK if obj.name.startswith("World_GroundCorridor") else SINK_COARSE
        for v in obj.data.vertices:
            x, y = v.co.x, v.co.y
            h = height(x, y)
            if h is None:
                continue
            top = h - depth * smooth((S.route_distance(x, y) - SINK_FROM) / SINK_OVER) * smooth(chord_distance(x, y) / CLOSE)
            if v.co.z > top:
                deepest = max(deepest, v.co.z - top)
                v.co.z = top
                count += 1
        obj.data.update()
    print(f"  hairpin: {count} terrain vertices pushed under the smooth ground (up to {deepest:.2f} m)")


def apply():
    plan()
    sink_terrain()


# ---------------------------------------------------------------------------------------------------------------
# Hooks for env_streets
# ---------------------------------------------------------------------------------------------------------------
def layers(plan_layers):
    """The paving plan with one more layer under all others: the whole zone, in the terrain's material and UVs."""
    lay = {"name": LAYER_NAME, "mat": "ground_map", "ring": list(_st["ring"]), "ground": None, "hull": 0.0,
           "uv": ((E.X0, E.Y0), (1.0, 0.0), E.X1 - E.X0, E.Y1 - E.Y0)}
    return [lay] + list(plan_layers)


def retag(soup, plan):
    """Triangles of the zone that belong to a hole layer (mat None: lawn, tree pits) go to the zone's own layer 0."""
    out = []
    for a, b, c, tag in soup.tris:
        if plan[tag]["mat"] is None:
            cx = (soup.xy[a][0] + soup.xy[b][0] + soup.xy[c][0]) / 3
            cy = (soup.xy[a][1] + soup.xy[b][1] + soup.xy[c][1]) / 3
            if in_zone(cx, cy):
                tag = 0
        out.append((a, b, c, tag))
    soup.tris = out


def refine(soup):
    """Short edges in the zone: the paving (and the paint on it) is a curved surface there."""
    def want(a, b):
        (xa, ya), (xb, yb) = soup.xy[a], soup.xy[b]
        return math.hypot(xa - xb, ya - yb) > MAX_EDGE and in_zone((xa + xb) / 2, (ya + yb) / 2)
    for _ in range(8):
        if not soup.split(want):
            break


def _paved_distance(x, y):
    """0 on a paved layer of the street plan, else the distance to the nearest one (only used near the chord)."""
    if _st["paved"] is None:
        (ax, ay), (bx, by) = _st["chord"]
        box = (min(ax, bx) - CLOSE - 2, min(ay, by) - 2, max(ax, bx) + 2, max(ay, by) + 2)
        _st["paved"] = [lay["ring"] for lay in S.LAYERS if lay["mat"] is not None
                        and max(p[0] for p in lay["ring"]) >= box[0] and min(p[0] for p in lay["ring"]) <= box[2]
                        and max(p[1] for p in lay["ring"]) >= box[1] and min(p[1] for p in lay["ring"]) <= box[3]]
    best = 1e9
    for ring in _st["paved"]:
        if S.inside(x, y, ring):
            return 0.0
        best = min(best, S.ring_distance(x, y, ring))
    return best


def lift(x, y, normal, edge):
    """The paving's height over the ground at (x, y): `normal` (env_streets.lift_at), but falling to `edge` where the
    zone's lawn layer ends at the chord (the lawn beyond it is plain terrain: no lip across the lawn). Within 1 m of
    a paved layer it stays `normal`: that paving runs on beyond the chord at its 7 cm."""
    if not in_zone(x, y):
        return normal
    c = chord_distance(x, y)
    if c >= CLOSE:
        return normal
    f = (1 - smooth(c / CLOSE)) * smooth(_paved_distance(x, y) / 1.0)
    return normal + (edge - normal) * f
