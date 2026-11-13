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
KERB_DROP = 0.9          # the least the cove's sand lies below the promenade kerb (K03-K05, V10b's cove stair)
# V16e (survey V15, 2.6): the cove is a sand basin inside a sea wall, about 1.5 m high at the north apex and 2.5-3 m
# on the sides. The wall's top is the promenade as built (the kerb line, nothing beside the road moves); its foot is
# the sand, which lies COVE_FOOT[0] + COVE_FOOT[1] * (open beach height at that distance from the water) above the
# sea, so the wall is as high as the promenade allows: never under KERB_DROP, never over COVE_WALL_MAX.
COVE_FOOT = (0.30, 0.52)
COVE_WALL_MAX = 3.0
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
# V16e: the cove's outline follows the kerb as built (the smoothed, pushed-out line), not the straight chords between
# the traced points. Between two traced points the built kerb bulges up to 0.5 m towards the land; sand in that
# sliver was "not in the cove", kept the 5 m terrain's slope and stood as a mound against the sea wall.
if COVE[:len(COVE_KERB_RAW)] == COVE_KERB_RAW:
    COVE = COVE_KERB + COVE[len(COVE_KERB_RAW):]


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
_FOOT = {}


def cove_drop(kx, ky, rim):
    """Height of the cove's sea wall at a kerb point whose top (the promenade) lies at `rim`."""
    key = (round(kx, 2), round(ky, 2))
    if key not in _FOOT:
        _FOOT[key] = COVE_FOOT[0] + COVE_FOOT[1] * beach_z(max(signed_distance(kx, ky), 0.0))
    return min(max(rim - _FOOT[key], KERB_DROP), COVE_WALL_MAX)


def cove_z(x, y, kerb_height):
    """Sand in the cove: cove_drop() below the promenade at the kerb, sloping down to the water. kerb_height(px, py)
    gives the promenade height at a kerb point; the rim height is the nearest kerb point's next to the kerb and,
    15 m out, a smooth inverse-distance blend along the whole kerb (the promenade ramps down at the arc's west end),
    so the bowl has no creases. Never below the open beach profile near the water."""
    s = signed_distance(x, y)
    dk, (px, py) = kerb_point(x, y)
    wsum = zsum = 0.0
    for kx, ky in _KERB_KNOTS:
        w = 1.0 / ((kx - x) ** 2 + (ky - y) ** 2 + 16.0)
        wsum += w
        rim = kerb_height(kx, ky)
        zsum += w * (rim - cove_drop(kx, ky, rim))
    t = min(dk / 15.0, 1.0)                              # right at the kerb: exactly the wall's height under it
    rim = kerb_height(px, py)
    top = (rim - cove_drop(px, py, rim)) * (1 - t * t * (3 - 2 * t)) + zsum / wsum * t * t * (3 - 2 * t)
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


# ------------------------------------------------------------------------------------------------------------------
# V10d (2026-11-11): the rock shelves broken up and loose rocks in the swash. The traced shelves (ROCKS) read as flat
# pancakes; the photos (K03, K06, K07) and the ortho show low limestone ledges broken into pieces with sand between
# them, ragged stepped edges, and separate dark rocks in the swash. Plain Python and seeded, so the 3D rocks
# (env_backdrop.build_rocks) and the ground map's rock paint (make_groundmap) see exactly the same pieces.
# ------------------------------------------------------------------------------------------------------------------
ROCK_PIECE_STEP = 1.5      # m between outline points of a rock piece (ragged edge)
ROCK_SEED = 1105


def ring_ccw(ring):
    a = sum(ax * by - bx * ay for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]))
    return list(ring) if a > 0 else list(ring[::-1])


def ring_area(ring):
    return 0.5 * abs(sum(ax * by - bx * ay for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1])))


def ring_centroid(ring):
    a = cx = cy = 0.0
    for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]):
        c = ax * by - bx * ay
        a += c
        cx += (ax + bx) * c
        cy += (ay + by) * c
    return cx / (3.0 * a), cy / (3.0 * a)


def densify(ring, step):
    out = []
    for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]):
        n = max(1, int(round(math.hypot(bx - ax, by - ay) / step)))
        out += [(ax + (bx - ax) * k / n, ay + (by - ay) * k / n) for k in range(n)]
    return out


def _clip(ring, nx, ny, d):
    """The part of a ring where nx * x + ny * y >= d (Sutherland-Hodgman against one line)."""
    out = []
    for k, p in enumerate(ring):
        q = ring[(k + 1) % len(ring)]
        fp, fq = nx * p[0] + ny * p[1] - d, nx * q[0] + ny * q[1] - d
        if fp >= 0:
            out.append(p)
        if (fp >= 0) != (fq >= 0):
            t = fp / (fp - fq)
            out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def _long_axis(ring, cx, cy):
    pts = densify(ring, 0.5)
    sxx = sum((x - cx) ** 2 for x, _ in pts)
    syy = sum((y - cy) ** 2 for _, y in pts)
    sxy = sum((x - cx) * (y - cy) for x, y in pts)
    ang = 0.5 * math.atan2(2.0 * sxy, sxx - syy)
    return ang, (math.cos(ang), math.sin(ang))


_PIECES = []


def rock_pieces():
    """The rock shelves as pieces: each traced shelf cut across its long axis about every 13 m (cut lines up to 29
    degrees off square, 0.6-2.8 m of sand between the pieces), each piece with a ragged outline (outline points
    1.5 m apart pulled 0-0.7 m towards the piece's centre, now and then a notch up to 2.1 m deep). A piece's height
    varies (0.75-1.15 x its shelf's) and tilts along its long axis (one end up to 30 % higher).
    Returns dicts: shelf, ring (counter-clockwise, star-shaped about centre), centre, height, axis, half, tilt."""
    if _PIECES:
        return _PIECES
    import random
    rng = random.Random(ROCK_SEED)
    for rock in ROCKS:
        ring = ring_ccw(rock["ring"])
        cx, cy = ring_centroid(ring)
        ang, (ax, ay) = _long_axis(ring, cx, cy)
        proj = [(x - cx) * ax + (y - cy) * ay for x, y in ring]
        t0, t1 = min(proj), max(proj)
        n = max(1, min(4, int(round((t1 - t0) / 13.0 + rng.uniform(-0.3, 0.3)))))
        if rock.get("pieces"):                          # V16e: a ridge or a reef finger in a set number of pieces
            n = rock["pieces"]
        cuts = []
        for k in range(1, n):
            t = t0 + (t1 - t0) * (k + rng.uniform(-0.15, 0.15)) / n
            turn = rng.uniform(-0.5, 0.5)
            nx, ny = math.cos(ang + turn), math.sin(ang + turn)
            cuts.append((nx, ny, (cx + ax * t) * nx + (cy + ay * t) * ny,
                         rng.uniform(0.6, 2.8) * rock.get("gap", 1.0)))     # V16e: "gap" narrows the sand between
        for k in range(n):
            poly = ring
            if k > 0:
                nx, ny, d, gap = cuts[k - 1]
                poly = _clip(poly, nx, ny, d + gap / 2)
            if k < n - 1 and len(poly) >= 3:
                nx, ny, d, gap = cuts[k]
                poly = _clip(poly, -nx, -ny, -(d - gap / 2))
            if len(poly) < 3 or ring_area(poly) < 5.0:
                continue
            pcx, pcy = ring_centroid(poly)
            out = []
            for x, y in densify(poly, ROCK_PIECE_STEP):
                dx, dy = pcx - x, pcy - y
                r = math.hypot(dx, dy) or 1.0
                rag = rng.uniform(0.0, 0.7) + (rng.uniform(0.6, 1.4) if rng.random() < 0.15 else 0.0)
                rag = min(rag, 0.3 * r)
                out.append((x + dx / r * rag, y + dy / r * rag))
            pang, (pax, pay) = _long_axis(out, pcx, pcy)
            half = max(abs((x - pcx) * pax + (y - pcy) * pay) for x, y in out)
            _PIECES.append({"shelf": rock["name"], "ring": out, "centre": (pcx, pcy),
                            "height": rock["height"] * rng.uniform(0.75, 1.15), "axis": (pax, pay), "half": half,
                            "tilt": rng.uniform(-0.3, 0.3) * rock.get("tilt", 1.0), "tiers": rock.get("tiers")})
    return _PIECES


def piece_height_factor(piece, x, y):
    """How much of the piece's height stands at (x, y): the piece tilts along its long axis."""
    (cx, cy), (ax, ay) = piece["centre"], piece["axis"]
    return min(1.4, max(0.6, 1.0 + piece["tilt"] * ((x - cx) * ax + (y - cy) * ay) / piece["half"]))


_BOULDERS = []


def rock_boulders():
    """Loose rocks round the shelves, mostly on their seaward side and in the swash (K03, K07): per piece near the
    water (signed distance under 30 m) 1-3, further up the beach at most 1; 0.5-1.7 m long, 0.3-0.9 m above the
    sand. Never inside a piece, within 10 m of a road edge or more than 4 m out into the sea. Returns dicts: centre,
    size (length, width, height above the sand), yaw, seed, piece (index into rock_pieces(), whose interior colour
    the rock uses)."""
    if _BOULDERS:
        return _BOULDERS
    import random
    rng = random.Random(ROCK_SEED + 1)
    pieces = rock_pieces()

    def clear(x, y, r):
        for p in pieces:
            (cx, cy), ring = p["centre"], p["ring"]
            if math.hypot(x - cx, y - cy) > 40:
                continue
            if inside(x, y, ring) or _closest(x, y, _segments(ring + ring[:1]))[0] < r + 0.3:
                return False
        return all(math.hypot(x - b["centre"][0], y - b["centre"][1]) > r + b["size"][0] / 2 + 0.4 for b in _BOULDERS)

    for i, p in enumerate(pieces):
        (cx, cy), ring = p["centre"], p["ring"]
        near = signed_distance(cx, cy) < 30.0
        want = rng.randint(1, 3) if near else rng.randint(0, 1)
        for _ in range(want):
            for _try in range(12):
                cand = [ring[rng.randrange(len(ring))] for _ in range(3)]
                x, y = min(cand, key=lambda q: signed_distance(*q)) if near else cand[0]
                dx, dy = x - cx, y - cy
                r = math.hypot(dx, dy) or 1.0
                out = rng.uniform(0.7, 3.5)
                bx, by = x + dx / r * out, y + dy / r * out
                length = rng.uniform(0.5, 1.7)
                size = (length, length * rng.uniform(0.55, 0.9), min(0.9, max(0.3, length * rng.uniform(0.4, 0.6))))
                if signed_distance(bx, by) < -4.0 or _route_clearance(bx, by) < 10.0 or not clear(bx, by, length / 2):
                    continue
                _BOULDERS.append({"centre": (bx, by), "size": size, "yaw": rng.uniform(0, 2 * math.pi),
                                  "seed": rng.randrange(1 << 30), "piece": i})
                break
    return _BOULDERS
