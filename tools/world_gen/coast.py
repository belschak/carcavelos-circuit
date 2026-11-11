"""The coast as data: waterline, beach zone, cove, rock shelves and the coast beyond the world (coast_trace.json).

Plain Python (no Blender), so the terrain (env_terrain, env_corridor), the backdrop (env_backdrop) and the ground
map (make_groundmap) all read the same shapes:
    signed_distance(x, y)   metres to the waterline, > 0 on land, < 0 in the sea
    beach_z(s)              height of open sand at signed distance s (crosses z = 0 exactly on the waterline)
    in_beach(x, y)          between the waterline and the backshore (sand unless a rock or a path is there)
    cove_z(x, y)            the sand cove below the curved promenade: kerb - KERB_DROP at the kerb, 0 at the water
    COVE_KERB               the cove kerb's centre line, exactly as V10b builds its granite kerb
    full_coast()            the whole coast west to east (far west line, waterline, headland), land on the left
    far_line_y(x)           the straight far coast west of the shore map (Sea.shader uses the same line)
"""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
C = json.load(open(os.path.join(HERE, "coast_trace.json")))

WATERLINE = [tuple(p) for p in C["waterline"]]
BACKSHORE = [tuple(p) for p in C["backshore"]]
COVE = [tuple(p) for p in C["cove"]]
COVE_KERB_RAW = [tuple(p) for p in C["cove_kerb"]]
ROCKS = [dict(r, ring=[tuple(p) for p in r["ring"]]) for r in C["rocks"]]
HEADLAND = [tuple(p) for p in C["headland"]]
FAR_WEST = [tuple(p) for p in C["far_west"]]            # from the waterline's west end outwards (east to west)
FAR_X, FAR_Y = FAR_WEST[-1]                             # the straight far line starts here and runs west
FAR_SLOPE = C["far_line_slope"]                         # metres north per metre west

SEA_FLOOR = -2.0
KERB_DROP = 0.9          # the cove's sand lies this far below the promenade kerb (K03-K05, V10b's cove stair)
KERB_HALF = 0.2          # half the width of V10b's kerb blocks (env_beachfront.KERB_W)
KERB_ROAD_GAP = 0.45     # V10b keeps the kerb's inner face this far outside the road edge (KERB_ROAD_GAP)
COVE_CENTRE = (-66.0, -186.0)   # the sand side of the cove kerb faces this point
ROUTE = json.load(open(os.path.join(HERE, "route.json")))["points"]


def _route_clearance(x, y):
    """Metres from (x, y) to the nearest road edge (centreline segments minus the half width)."""
    best = float("inf")
    n = len(ROUTE)
    for i, p in enumerate(ROUTE):
        if abs(p["x"] - x) > 25 or abs(p["y"] - y) > 25:
            continue
        q = ROUTE[(i + 1) % n]
        dx, dy = q["x"] - p["x"], q["y"] - p["y"]
        l2 = dx * dx + dy * dy
        t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((x - p["x"]) * dx + (y - p["y"]) * dy) / l2))
        best = min(best, math.hypot(x - p["x"] - t * dx, y - p["y"] - t * dy) - p.get("width", 8.0) / 2)
    return best


def _catmull(points, step=1.0):
    P = [points[0]] + list(points) + [points[-1]]
    out = []
    for k in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[k - 1], P[k], P[k + 1], P[k + 2]
        n = max(1, int(math.hypot(p2[0] - p1[0], p2[1] - p1[1]) / step))
        for j in range(n):
            t = j / n
            out.append(tuple(0.5 * (2 * p1[i] + (-p0[i] + p2[i]) * t + (2 * p0[i] - 5 * p1[i] + 4 * p2[i] - p3[i]) * t * t
                                    + (-p0[i] + 3 * p1[i] - 3 * p2[i] + p3[i]) * t ** 3) for i in range(2)))
    out.append(tuple(points[-1]))
    return out


def _kerb_line():
    """The cove kerb's centre line as V10b builds it (env_beachfront.kerb_samples): the traced edge smoothed and
    pushed out towards the sand where the road would come closer than KERB_ROAD_GAP to the kerb's inner face."""
    pts = _catmull(COVE_KERB_RAW)
    out = []
    for k, (x, y) in enumerate(pts):
        a, b = pts[max(k - 1, 0)], pts[min(k + 1, len(pts) - 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        tl = math.hypot(tx, ty) or 1.0
        mx, my = -ty / tl, tx / tl
        if mx * (COVE_CENTRE[0] - x) + my * (COVE_CENTRE[1] - y) < 0:
            mx, my = -mx, -my
        for _ in range(4):
            gap = _route_clearance(x, y) - KERB_HALF - KERB_ROAD_GAP
            if gap >= 0:
                break
            x, y = x + mx * (-gap + 0.05), y + my * (-gap + 0.05)
        out.append((x, y))
    return out


COVE_KERB = _kerb_line()


def far_line_y(x):
    """The straight coast west of FAR_X (y of the waterline)."""
    return FAR_Y + FAR_SLOPE * (FAR_X - x)


_WEST = [(-4000.0, far_line_y(-4000.0))] + FAR_WEST[::-1][:-1] + WATERLINE      # x increasing


def coast_y_west(x):
    """y of the waterline at x along the west coast (the far line, the far-west curve, the traced waterline)."""
    if x <= _WEST[0][0]:
        return far_line_y(x)
    for (ax, ay), (bx, by) in zip(_WEST, _WEST[1:]):
        if ax <= x <= bx:
            return ay + (by - ay) * (x - ax) / ((bx - ax) or 1.0)
    return _WEST[-1][1]


def cos_west(x, h=5.0):
    """Cosine of the west coast's angle to the x axis at x (vertical offsets times this = distance to the line)."""
    slope = (coast_y_west(x + h) - coast_y_west(x - h)) / (2 * h)
    return 1.0 / math.sqrt(1.0 + slope * slope)


def full_coast(west_x=-4000.0):
    """West to east, land on the left: far line, far-west curve, traced waterline, then the coast east of the cove."""
    return [(west_x, coast_y_west(west_x))] + [p for p in _WEST if p[0] > west_x + 0.5] + HEADLAND[1:]


def beach_z(s):
    """Height of open sand (m above the sea plane) at signed distance s from the waterline: 8 % across the water's
    edge (the 5 m terrain grid crosses z = 0 on the traced line, and water and sand never lie within 4 cm of each
    other more than 0.5 m from it), then 3.5 % up the wet sand and 1.8 % up the dry beach. Seabed below 0."""
    if s < -8.0:
        return max(-0.64 + 0.1 * (s + 8.0), SEA_FLOOR)
    if s <= 4.0:
        return 0.08 * s
    if s <= 25.0:
        return 0.32 + 0.035 * (s - 4.0)
    return 1.055 + 0.018 * (s - 25.0)


def _segments(line):
    return list(zip(line, line[1:]))


_COAST_SEGS = _segments(full_coast())


def _closest(x, y, segs):
    """(distance, nearest point, segment index) to a polyline given as segments."""
    best, bp, bk = 1e18, None, -1
    for k, ((ax, ay), (bx, by)) in enumerate(segs):
        if min(ax, bx) - x > best or x - max(ax, bx) > best or min(ay, by) - y > best or y - max(ay, by) > best:
            continue
        dx, dy = bx - ax, by - ay
        seg = dx * dx + dy * dy or 1e-9
        t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / seg))
        px, py = ax + t * dx, ay + t * dy
        d = math.hypot(x - px, y - py)
        if d < best:
            best, bp, bk = d, (px, py), k
    return best, bp, bk


def signed_distance(x, y):
    """Metres from (x, y) to the coast, > 0 on land (left of the line), < 0 in the sea. At a vertex the sign comes
    from the direction to the nearest point compared with the two neighbouring segments' mean normal."""
    d, (px, py), k = _closest(x, y, _COAST_SEGS)
    (ax, ay), (bx, by) = _COAST_SEGS[k]
    nx, ny = -(by - ay), bx - ax                        # left normal of the segment
    if (px, py) in ((ax, ay), (bx, by)):                # nearest point is a vertex: average the two normals
        j = k + 1 if (px, py) == (bx, by) else k - 1
        if 0 <= j < len(_COAST_SEGS):
            (cx, cy), (ex, ey) = _COAST_SEGS[j]
            n2x, n2y = -(ey - cy), ex - cx
            l1, l2 = math.hypot(nx, ny) or 1.0, math.hypot(n2x, n2y) or 1.0
            nx, ny = nx / l1 + n2x / l2, ny / l1 + n2y / l2
    side = (x - px) * nx + (y - py) * ny
    return d if side >= 0 else -d


def inside(x, y, poly):
    hit = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            hit = not hit
    return hit


BEACH_ZONE = WATERLINE + BACKSHORE[::-1]                # closed ring: waterline west to east, backshore back west
_BACK_SEGS = _segments(BACKSHORE)
_KERB_SEGS = _segments(COVE_KERB)


def in_beach(x, y):
    return inside(x, y, BEACH_ZONE)


def backshore_distance(x, y):
    return _closest(x, y, _BACK_SEGS)[0]


def kerb_point(x, y):
    """(distance, nearest point) to the cove kerb."""
    d, p, _k = _closest(x, y, _KERB_SEGS)
    return d, p


def kerb_side(x, y):
    """(distance to the cove kerb's centre line, nearest kerb point, True if (x, y) is on the kerb's sand side)."""
    d, (kx, ky), k = _closest(x, y, _KERB_SEGS)
    (ax, ay), (bx, by) = _KERB_SEGS[k]
    mx, my = -(by - ay), bx - ax
    if mx * (COVE_CENTRE[0] - kx) + my * (COVE_CENTRE[1] - ky) < 0:
        mx, my = -mx, -my
    return d, (kx, ky), (x - kx) * mx + (y - ky) * my > 0


def in_cove(x, y):
    return inside(x, y, COVE)


_KERB_KNOTS = COVE_KERB[::4]                            # kerb samples about 4 m apart for the cove's rim height


def cove_z(x, y, kerb_height):
    """Sand in the cove: KERB_DROP below the promenade at the kerb, sloping down to the water. kerb_height(px, py)
    gives the promenade height at a kerb point; the rim height is the nearest kerb point's next to the kerb and,
    15 m out, a smooth inverse-distance blend along the whole kerb (the promenade ramps down at the arc's west end),
    so the bowl has no creases. Never below the open beach profile near the water."""
    s = signed_distance(x, y)
    dk, (px, py) = kerb_point(x, y)
    wsum = zsum = 0.0
    for kx, ky in _KERB_KNOTS:
        w = 1.0 / ((kx - x) ** 2 + (ky - y) ** 2 + 16.0)
        wsum += w
        zsum += w * kerb_height(kx, ky)
    t = min(dk / 15.0, 1.0)                              # right at the kerb: exactly KERB_DROP under it
    top = kerb_height(px, py) * (1 - t * t * (3 - 2 * t)) + zsum / wsum * t * t * (3 - 2 * t) - KERB_DROP
    z = top * max(s, 0.0) / (max(s, 0.0) + dk + 1e-6)
    if s < 6.0:
        return max(z, beach_z(s))
    return max(z, 0.6 * beach_z(s), 0.3)                 # where the promenade ramps down: still a sandy bowl


def rock_at(x, y):
    """The rock shelf containing (x, y), or None."""
    for r in ROCKS:
        xs, ys = [p[0] for p in r["ring"]], [p[1] for p in r["ring"]]
        if min(xs) <= x <= max(xs) and min(ys) <= y <= max(ys) and inside(x, y, r["ring"]):
            return r
    return None


def sea_polygon(inland=8.0, west_x=-4000.0, south_y=-4000.0, east_x=4000.0):
    """The sea as one closed ring: the whole coast moved `inland` metres onto the land (so the water plane reaches
    under the beach and meets it on the waterline), closed far out to the south."""
    line = full_coast(west_x)
    line = [p for p in line if p[0] <= east_x] + ([(east_x, _y_at(line, east_x))] if line[-1][0] > east_x else [])
    def normal(a, b):
        dx, dy = b[0] - a[0], b[1] - a[1]
        d = math.hypot(dx, dy) or 1.0
        return -dy / d, dx / d

    out = []
    for k, (x, y) in enumerate(line):                   # mitred offset: each segment moves by exactly `inland`
        n1 = normal(line[max(k - 1, 0)], line[max(k, 1)])
        n2 = normal(line[min(k, len(line) - 2)], line[min(k + 1, len(line) - 1)])
        dot = n1[0] * n2[0] + n1[1] * n2[1]
        m = inland / max(1.0 + dot, 0.35)
        out.append((x + (n1[0] + n2[0]) * m, y + (n1[1] + n2[1]) * m))
    return out + [(out[-1][0], south_y), (out[0][0], south_y)]


def _y_at(line, x):
    for (ax, ay), (bx, by) in zip(line, line[1:]):
        if ax <= x <= bx:
            return ay + (by - ay) * (x - ax) / ((bx - ax) or 1.0)
    return line[-1][1]
