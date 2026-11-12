"""Coast section (V16e, 2026-11-11): builds coast_plan.py, from the photosphere survey V15 (Appendix H, area B).

Objects (collection World_Promenade, exported with CampusWorld.fbx; one material W_coast_atlas unless noted):
  Coast_Forecourt            tunnel south forecourt: white wall on the north-west side (0.55 m rising to 3 m, behind the
                             cutting's bank), white precast kerb-bench on the south side, the old grey wall at the
                             field's corner
  Coast_Terrace              beige concrete terrace west of the cove (4 m panels), rubble revetment on its sea sides
  Coast_Details              anthracite totem (blank), drinking fountain, twin bins, white rip-rap chute on the berm,
                             tamarisk hedge along the east lawn
  Coast_Rails                two-rail railings (the promenade's steel material, env_beachfront.railing_line): on the
                             forecourt wall, on the cove's sea wall where it is 6.6 m or more from the race line
  Coast_CoveRail_NoCollide   the same railing where the sea wall runs beside the road (closer than 6.6 m): to look at
  Coast_RampUpstand_NoCollide  0.28 m concrete upstand under the railings on both rims of the tunnel ramp
  B_w482697584 / B_w213240239  the beach bar and the white prow (handed over by env_beachfront.build_one)
road_paint() adds flush paint to Track_EdgeLines (env_trackside hook): slot drains, the three white-nosed steps and
the concentric kerb arcs on the promenade, the tunnel's lengthwise slot drain. dress() edits the dressing lists
(called by env_street_objects.dress_promenade): palms, umbrella trees, shrubs, cars on the berm, bollards, the fence
on the south portal's deck; items standing on ground this plan moves are seated again.

Everything that stands up keeps coast_plan.ROUTE_CLEAR (6.6 m) from the race centreline; the build stops otherwise.
"""
import math
import random

import coast
import coast_plan as P
import env_beachfront as B
import env_common as E

COLLECTION = B.COLLECTION
REPORT = {}                # object -> minimum distance of a solid vertex to the race centreline
_state = {}


# =====================================================================================================
# Material, face helpers
# =====================================================================================================
def atlas_material():
    return B.textured(E.material("coast_atlas", "#CDB894", 0.9), "coast_atlas", 0.9)


def pal(g, corners, colour, out):
    g.face(corners, 0, out=out, uvs=[P.pal_uv(colour)] * len(corners))


def wall_quad(g, a, b, za, zb, ta, tb, band, ua, ub, out, v_top=None):
    """Upright quad from plan point a to b, from z?[0] (foot) to top t?; the band's top edge lies on the wall's top."""
    vm = P.ATLAS[band]["v_m"] if v_top is None else v_top
    g.face([(a[0], a[1], za), (b[0], b[1], zb), (b[0], b[1], tb), (a[0], a[1], ta)], 0, out=out,
           uvs=[P.band_uv(band, ua, vm - (ta - za)), P.band_uv(band, ub, vm - (tb - zb)), P.band_uv(band, ub, vm),
                P.band_uv(band, ua, vm)])


def pbox(g, c, u, hu, hv, z0, z1, colour, skip=("bottom",)):
    """Flat-colour box centred at c (x, y), long axis u, half sizes along u and across, z0..z1."""
    ux, uy = u
    vx, vy = -uy, ux

    def p(a, b, z):
        return (c[0] + ux * a + vx * b, c[1] + uy * a + vy * b, z)
    for key, (k0, k1, out) in {"+u": ((hu, -hv), (hu, hv), (ux, uy)), "-u": ((-hu, hv), (-hu, -hv), (-ux, -uy)),
                               "+v": ((hu, hv), (-hu, hv), (vx, vy)), "-v": ((-hu, -hv), (hu, -hv), (-vx, -vy))}.items():
        if key not in skip:
            pal(g, [p(*k0, z0), p(*k1, z0), p(*k1, z1), p(*k0, z1)], colour, (out[0], out[1], 0.0))
    ring = [(-hu, -hv), (hu, -hv), (hu, hv), (-hu, hv)]
    if "top" not in skip:
        pal(g, [p(a, b, z1) for a, b in ring], colour, (0, 0, 1))
    if "bottom" not in skip:
        pal(g, [p(a, b, z0) for a, b in ring], colour, (0, 0, -1))


def pbeam(g, a, b, w, h, colour):
    """Flat-colour bar from a to b (3-D centre line), w wide, h high."""
    from mathutils import Vector
    a, b = Vector(a), Vector(b)
    d = b - a
    flat = Vector((d.x, d.y, 0.0))
    side = Vector((-flat.y, flat.x, 0.0)).normalized() * (w / 2) if flat.length > 1e-6 else Vector((w / 2, 0, 0))
    up = d.cross(side).normalized() * (h / 2)
    if up.z < 0:
        up = -up
    for (s0, t0), (s1, t1) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
        o0, o1 = side * s0 + up * t0, side * s1 + up * t1
        pal(g, [a + o0, a + o1, b + o1, b + o0], colour, (o0 + o1) / 2)
    ring = [side * s + up * t for s, t in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    pal(g, [a + o for o in ring], colour, -d)
    pal(g, [b + o for o in ring], colour, d)


def guard(name, g, low=None):
    """Stops the build if a vertex of a solid object stands closer than ROUTE_CLEAR to the race centreline
    (`low`: vertices up to this height above the ground do not count: flush paving)."""
    best = 1e9
    for v in g.verts:
        if low is not None and v.z <= E.ground_z(v.x, v.y) + low:
            continue
        best = min(best, P.route_distance(v.x, v.y))
    REPORT[name] = best
    if best < P.ROUTE_CLEAR - 0.005:
        raise RuntimeError(f"V16e coast: '{name}' stands {best:.2f} m from the race centreline (limit {P.ROUTE_CLEAR} m)")
    return best


def finish(name, g, mats=None):
    if not g.faces:
        return None
    obj = E.new_object(name, g.mesh(name, mats or [atlas_material()]), E.collection(COLLECTION))
    return obj


def tris(g):
    return sum(len(f[0]) - 2 for f in g.faces)


# =====================================================================================================
# Tunnel south forecourt (item 13)
# =====================================================================================================
def forecourt_wall():
    """[(x, y, foot z, top z, arc)] of the white wall's centre line on the north-west side."""
    f = P.FORE
    t0, t1 = f["wall"]
    rise_end = t1 - 5.0                                   # full height from 10 m before the mouth
    out, arc, prev = [], 0.0, None
    n = int(round((t1 - t0) * 2))
    for k in range(n + 1):
        t = t0 + (t1 - t0) * k / n
        x, y = P.route_side(t, f["wall_offset"])
        road = P.route_at(t)[2]
        h = f["wall_h"][0] + (f["wall_h"][1] - f["wall_h"][0]) * min(1.0, (t - t0) / (rise_end - t0))
        gz = max(E.ground_z(x, y), E.ground_z(*P.route_side(t, f["wall_offset"] - 0.3)))
        top = max(road + h, gz + 0.45)
        if prev:
            arc += math.hypot(x - prev[0], y - prev[1])
        prev = (x, y)
        out.append((x, y, min(gz, road) - 0.4, top, arc))
    return out


def build_forecourt():
    f = P.FORE
    g, rails = B.Geo(), B.Geo()
    # white wall, north-west side
    line = forecourt_wall()
    half = f["wall_thick"] / 2
    for (x0, y0, f0, t0, s0), (x1, y1, f1, t1, s1) in zip(line, line[1:]):
        u = P.unit(x1 - x0, y1 - y0)
        n = (u[1], -u[0])                                 # towards the road (the wall is on the left of the route)
        a0, a1 = (x0 + n[0] * half, y0 + n[1] * half), (x1 + n[0] * half, y1 + n[1] * half)
        b0, b1 = (x0 - n[0] * half, y0 - n[1] * half), (x1 - n[0] * half, y1 - n[1] * half)
        for q0, q1, sgn in ((a0, a1, 1), (b0, b1, -1)):   # V18h: white wall panels of the trim atlas (80 px per metre)
            g.face([(*q0, f0), (*q1, f1), (*q1, t1), (*q0, t0)], 1, out=(n[0] * sgn, n[1] * sgn, 0),
                   uvs=[P.trim_uv("wall", s0, 3.2 - (t0 - f0)), P.trim_uv("wall", s1, 3.2 - (t1 - f1)),
                        P.trim_uv("wall", s1, 3.2), P.trim_uv("wall", s0, 3.2)])
        g.face([(*a0, t0), (*a1, t1), (*b1, t1), (*b0, t0)], 1, out=(0, 0, 1),
               uvs=[P.trim_uv("precast_top", s0, 0.1), P.trim_uv("precast_top", s1, 0.1), P.trim_uv("precast_top", s1, 0.4),
                    P.trim_uv("precast_top", s0, 0.4)])
    (x0, y0, f0, t0, _s), (x1, y1, *_r) = line[0], line[1]
    u = P.unit(x1 - x0, y1 - y0)
    pal(g, [(x0 + u[1] * half, y0 - u[0] * half, f0), (x0 - u[1] * half, y0 + u[0] * half, f0),
            (x0 - u[1] * half, y0 + u[0] * half, t0), (x0 + u[1] * half, y0 - u[0] * half, t0)], "white", (-u[0], -u[1], 0))
    B.railing_line(rails, [(x, y, top) for x, y, _f, top, _s in line], B.STEEL_SLOT, post=0.05, rails=(0.55, 1.1),
                   rail=0.045, step=2.0)
    # white precast kerb-bench on the south side: 3 m segments on the ground behind the bank
    t0, t1 = f["bench"]
    pts = P.route_line(t0, t1, f["bench_offset"], 0.5)
    dense = [(p[0], p[1]) for p in pts]
    w, h = f["bench_size"]
    count, acc, start = 0, 0.0, dense[0]
    for a, b in zip(dense, dense[1:]):
        acc += math.hypot(b[0] - a[0], b[1] - a[1])
        if acc >= f["bench_len"]:
            u = P.unit(b[0] - start[0], b[1] - start[1])
            length = math.hypot(b[0] - start[0], b[1] - start[1]) - f["bench_gap"]
            c = (start[0] + u[0] * length / 2, start[1] + u[1] * length / 2)
            za, zb = E.ground_z(*start), E.ground_z(*b)
            tbox(g, c, u, length / 2, w / 2, min(za, zb) - 0.25, max(za, zb) + h - 0.5 * abs(za - zb),
                 s0=2.4 * (count % 4), slot=1)                                    # V18h: precast blocks with joints
            start, acc = b, 0.0
            count += 1
    # the old grey wall at the field's north-west corner
    wh, wt = P.FIELD_WALL_SIZE
    st = P.polyline_points(P.FIELD_WALL, 2.5)
    for (xa, ya, ua, sa), (xb, yb, ub, sb) in zip(st, st[1:]):
        na, nb = (-ua[1], ua[0]), (-ub[1], ub[0])
        za, zb = E.ground_z(xa, ya), E.ground_z(xb, yb)
        for sgn in (1, -1):
            a = (xa + na[0] * sgn * wt / 2, ya + na[1] * sgn * wt / 2)
            b = (xb + nb[0] * sgn * wt / 2, yb + nb[1] * sgn * wt / 2)
            wall_quad(g, a, b, za - 0.3, zb - 0.3, za + wh, zb + wh, "oldwall", sa, sb, (na[0] * sgn, na[1] * sgn, 0), v_top=1.3)
        pal(g, [(xa + na[0] * wt / 2, ya + na[1] * wt / 2, za + wh), (xb + nb[0] * wt / 2, yb + nb[1] * wt / 2, zb + wh),
                (xb - nb[0] * wt / 2, yb - nb[1] * wt / 2, zb + wh), (xa - na[0] * wt / 2, ya - na[1] * wt / 2, za + wh)],
            "shade", (0, 0, 1))
    for (x, y, u, _s), sgn in ((st[0], -1), (st[-1], 1)):
        n = (-u[1], u[0])
        z = E.ground_z(x, y)
        pal(g, [(x + n[0] * wt / 2, y + n[1] * wt / 2, z - 0.3), (x - n[0] * wt / 2, y - n[1] * wt / 2, z - 0.3),
                (x - n[0] * wt / 2, y - n[1] * wt / 2, z + wh), (x + n[0] * wt / 2, y + n[1] * wt / 2, z + wh)],
            "shade", (u[0] * sgn, u[1] * sgn, 0))
    guard("Coast_Forecourt", g)
    guard("Coast_Rails (forecourt)", rails)
    finish("Coast_Forecourt", g, [atlas_material(), trim_material()])
    print(f"  coast forecourt: wall {line[-1][4]:.0f} m ({line[0][3] - P.route_at(f['wall'][0])[2]:.2f} m to "
          f"{line[-1][3] - P.route_at(f['wall'][1])[2]:.2f} m above the road), {count} kerb-bench blocks, field wall "
          f"{st[-1][3]:.0f} m; {tris(g)} triangles")
    return rails


# =====================================================================================================
# Terrace west of the cove (item 26): 4 m concrete panels, rubble revetment on the sea sides
# =====================================================================================================
def _clip_rect(ring, x0, y0, x1, y1):
    for nx, ny, d in ((1, 0, x0), (-1, 0, -x1), (0, 1, y0), (0, -1, -y1)):
        ring = coast._clip(ring, nx, ny, d)
        if len(ring) < 3:
            return []
    return ring


def build_terrace():
    g = B.Geo()
    ring = coast.ring_ccw(P.TERRACE)
    level = P.TERRACE_LEVEL + 0.04

    def top(x, y):
        gz = E.ground_z(x, y)
        if P.route_distance(x, y) < 7.7:                  # the edge beside the road: flush with the ground, no lip
            return gz + 0.03
        return max(level, gz + 0.04)
    xs, ys = [p[0] for p in ring], [p[1] for p in ring]
    panels = 0
    for i in range(int(math.floor(min(xs) / 4.0)), int(math.ceil(max(xs) / 4.0))):
        for j in range(int(math.floor(min(ys) / 4.0)), int(math.ceil(max(ys) / 4.0))):
            cell = _clip_rect(ring, i * 4.0, j * 4.0, i * 4.0 + 4.0, j * 4.0 + 4.0)
            if len(cell) < 3 or coast.ring_area(cell) < 0.02:
                continue
            g.face([(x, y, top(x, y)) for x, y in cell], 0, out=(0, 0, 1),
                   uvs=[P.band_uv("broom", x, y - j * 4.0) for x, y in cell])
            panels += 1
    # revetment: the edges away from the road and the cove wall slope down to the sand / rock
    n = len(ring)
    slope_m = 0
    for k in range(n):
        a, b = ring[k], ring[(k + 1) % n]
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        if P.route_distance(*mid) < 13.0 or coast.kerb_point(*mid)[0] < 6.0:
            continue
        u = P.unit(b[0] - a[0], b[1] - a[1])
        out = (u[1], -u[0])                               # outwards of a counter-clockwise ring
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        steps = max(1, int(round(length / 4.0)))
        for s in range(steps):
            p = (a[0] + (b[0] - a[0]) * s / steps, a[1] + (b[1] - a[1]) * s / steps)
            q = (a[0] + (b[0] - a[0]) * (s + 1) / steps, a[1] + (b[1] - a[1]) * (s + 1) / steps)
            zp, zq = top(*p), top(*q)
            run = 1.9
            fp = min(E.ground_z(p[0] + out[0] * run, p[1] + out[1] * run), zp - 0.6) - 0.35
            fq = min(E.ground_z(q[0] + out[0] * run, q[1] + out[1] * run), zq - 0.6) - 0.35
            po, qo = (p[0] + out[0] * run, p[1] + out[1] * run), (q[0] + out[0] * run, q[1] + out[1] * run)
            lp, lq = math.hypot(run, zp - fp), math.hypot(run, zq - fq)
            ua, ub = slope_m + length * s / steps, slope_m + length * (s + 1) / steps
            g.face([(*po, fp), (*qo, fq), (*q, zq), (*p, zp)], 0, out=(out[0], out[1], 1.0),
                   uvs=[P.band_uv("rubble", ua, 2.4 - lp), P.band_uv("rubble", ub, 2.4 - lq),
                        P.band_uv("rubble", ub, 2.4), P.band_uv("rubble", ua, 2.4)])
        slope_m += length
    guard("Coast_Terrace", g, low=0.12)
    finish("Coast_Terrace", g)
    print(f"  coast terrace: {panels} concrete panels at {level:.2f} m, revetment {slope_m:.0f} m; {tris(g)} triangles")


# =====================================================================================================
# Beach bar (item 26): low long building, terracotta hip roof, white-framed glazed front, raised deck with a white
# post-and-rail, cream stair at the deck's west end. No names, signs or painted panels.
# =====================================================================================================
def bar_layout():
    if "bar" in _state:
        return _state["bar"]
    b = B.footprint(P.BAR_ID)
    pts = b["points"]
    (cx, cy), u, hl, hw = B.rect_frame(pts)
    if u[0] < 0:
        u = (-u[0], -u[1])                                # u runs west to east
    v = (u[1], -u[0])                                     # towards the sea (south)
    spec = P.BAR
    cut = spec["west_cut"]                                # the OSM outline's west end is 6.5 m from the race line
    cx, cy, hl = cx + u[0] * cut / 2, cy + u[1] * cut / 2, hl - cut / 2
    floor = max(B.ground(x, y) for x, y in pts) + 0.05

    def at(a, s):
        return (cx + u[0] * a + v[0] * s, cy + u[1] * a + v[1] * s)
    d0, d1 = hw, hw + spec["deck_depth"]
    a0, a1 = -hl, hl - spec["deck_cut"]
    deck = [at(a0, d0), at(a1, d0), at(a1, d1), at(a0, d1)]
    lay = dict(pts=pts, c=(cx, cy), u=u, v=v, hl=hl, hw=hw, floor=floor, top=floor + spec["walls"], at=at,
               deck=deck, deck_z=floor - 0.06, a0=a0, a1=a1, d0=d0, d1=d1)
    _state["bar"] = lay
    return lay


def build_bar(b):
    lay = bar_layout()
    spec = P.BAR
    u, v, hl, hw, floor, top, at = lay["u"], lay["v"], lay["hl"], lay["hw"], lay["floor"], lay["top"], lay["at"]
    cx, cy = lay["c"]
    vol, det = B.Geo(), B.Geo()
    base = min(B.ground(x, y) for x, y in lay["pts"]) - 0.6
    corners = [at(-hl, -hw), at(hl, -hw), at(hl, hw), at(-hl, hw)]
    sides = [(corners[0], corners[1], (-v[0], -v[1]), 2 * hl), (corners[1], corners[2], u, 2 * hw),
             (corners[3], corners[0], (-u[0], -u[1]), 2 * hw)]
    for a, c, out, length in sides:                       # back and end walls: white boards on a pale base
        wall_quad(vol, a, c, base, base, top, top, "boards", 0.0, length, (out[0], out[1], 0), v_top=top - base)
    # sea front: white frame of slim posts with dark glazing between, a white lintel
    bays = 7
    lintel = 0.35
    for k in range(bays):
        a, c = -hl + 2 * hl * k / bays, -hl + 2 * hl * (k + 1) / bays
        pal(vol, [(*at(a + 0.12, hw), floor + 0.25), (*at(c - 0.12, hw), floor + 0.25), (*at(c - 0.12, hw), top - lintel),
                  (*at(a + 0.12, hw), top - lintel)], "glass", (v[0], v[1], 0))
        pal(vol, [(*at(a, hw), base), (*at(c, hw), base), (*at(c, hw), floor + 0.25), (*at(a, hw), floor + 0.25)],
            "timber_white", (v[0], v[1], 0))
        for e in (a, c - 0.12):
            pal(vol, [(*at(e, hw), floor + 0.25), (*at(e + 0.12, hw), floor + 0.25), (*at(e + 0.12, hw), top - lintel),
                      (*at(e, hw), top - lintel)], "white", (v[0], v[1], 0))
    pal(vol, [(*at(-hl, hw), top - lintel), (*at(hl, hw), top - lintel), (*at(hl, hw), top), (*at(-hl, hw), top)],
        "white", (v[0], v[1], 0))
    # terracotta hip roof with a small white fascia
    e = spec["eave"]
    L, W = hl + e, hw + e
    rise = W * spec["pitch"]
    ridge = max(L - W, 0.3)
    ze, zr = top + 0.04, top + 0.04 + rise
    c4 = [(*at(-L, -W), ze), (*at(L, -W), ze), (*at(L, W), ze), (*at(-L, W), ze)]
    r0, r1 = (*at(-ridge, 0), zr), (*at(ridge, 0), zr)
    sl = math.hypot(W, rise)
    vol.face([c4[0], c4[1], r1, r0], 0, out=(-v[0], -v[1], 1),
             uvs=[P.band_uv("tiles", 0, 0), P.band_uv("tiles", 2 * L, 0), P.band_uv("tiles", L + ridge, sl),
                  P.band_uv("tiles", L - ridge, sl)])
    vol.face([c4[2], c4[3], r0, r1], 0, out=(v[0], v[1], 1),
             uvs=[P.band_uv("tiles", 0, 0), P.band_uv("tiles", 2 * L, 0), P.band_uv("tiles", L + ridge, sl),
                  P.band_uv("tiles", L - ridge, sl)])
    vol.face([c4[1], c4[2], r1], 0, out=(u[0], u[1], 1),
             uvs=[P.band_uv("tiles", 0, 0), P.band_uv("tiles", 2 * W, 0), P.band_uv("tiles", W, sl)])
    vol.face([c4[3], c4[0], r0], 0, out=(-u[0], -u[1], 1),
             uvs=[P.band_uv("tiles", 0, 0), P.band_uv("tiles", 2 * W, 0), P.band_uv("tiles", W, sl)])
    pal(vol, c4, "white", (0, 0, -1))
    for k in range(4):
        p, q = c4[k], c4[(k + 1) % 4]
        mid = ((p[0] + q[0]) / 2 - cx, (p[1] + q[1]) / 2 - cy, 0)
        pal(det, [(p[0], p[1], ze - 0.2), (q[0], q[1], ze - 0.2), (q[0], q[1], ze), (p[0], p[1], ze)], "white", mid)
    # raised deck south of the building
    dz, a0, a1, d0, d1 = lay["deck_z"], lay["a0"], lay["a1"], lay["d0"], lay["d1"]
    for k in range(int(math.ceil((d1 - d0) / 2.0))):       # boards along the deck, 2 m strips
        s0, s1 = d0 + 2.0 * k, min(d0 + 2.0 * k + 2.0, d1)
        det.face([(*at(a0, s0), dz), (*at(a1, s0), dz), (*at(a1, s1), dz), (*at(a0, s1), dz)], 0, out=(0, 0, 1),
                 uvs=[P.band_uv("deck", 0, 0), P.band_uv("deck", a1 - a0, 0), P.band_uv("deck", a1 - a0, s1 - s0),
                      P.band_uv("deck", 0, s1 - s0)])
    edge = [(at(a0, d0), at(a0, d1), (-u[0], -u[1])), (at(a0, d1), at(a1, d1), v), (at(a1, d1), at(a1, d0), u)]
    low = min(B.ground(*p) for p in lay["deck"]) - 0.4
    for a, c, out in edge:                                # white boarding under the deck
        length = math.hypot(c[0] - a[0], c[1] - a[1])
        wall_quad(det, a, c, low, low, dz, dz, "boards", 0.0, length, (out[0], out[1], 0), v_top=dz - low)
    # stair at the west end (cream limestone, cheek walls); the white post-and-rail leaves it open
    sw = spec["stair_w"]
    s_mid = d0 + 1.2 + sw / 2
    foot_probe = at(a0 - 2.0, s_mid)
    drop = max(dz - B.ground(*foot_probe), 2 * spec["riser"])
    nr = max(2, int(math.ceil(drop / spec["riser"])))
    rise = drop / nr
    for k in range(nr - 1):
        z = dz - (k + 1) * rise
        c = at(a0 - (k + 0.5) * spec["tread"], s_mid)
        pbox(det, c, u, spec["tread"] / 2, sw / 2, low, z, "cream", skip=("bottom", "+u"))
    run = (nr - 1) * spec["tread"]
    for sgn in (-1, 1):                                   # cheeks following the slope
        s = s_mid + sgn * (sw / 2 + 0.175)
        p0, p1 = at(a0, s), at(a0 - run - 0.3, s)
        for off in (-0.175, 0.175):
            a, c = at(a0, s + off), at(a0 - run - 0.3, s + off)
            pal(det, [(*a, low), (*c, low), (*c, dz - drop + 0.5), (*a, dz + 0.5)], "cream", (v[0] * off, v[1] * off, 0))
        pal(det, [(*at(a0, s - 0.175), dz + 0.5), (*at(a0 - run - 0.3, s - 0.175), dz - drop + 0.5),
                  (*at(a0 - run - 0.3, s + 0.175), dz - drop + 0.5), (*at(a0, s + 0.175), dz + 0.5)], "cream", (0, 0, 1))
        pal(det, [(*at(a0 - run - 0.3, s - 0.175), low), (*at(a0 - run - 0.3, s + 0.175), low),
                  (*at(a0 - run - 0.3, s + 0.175), dz - drop + 0.5), (*at(a0 - run - 0.3, s - 0.175), dz - drop + 0.5)],
            "cream", (-u[0], -u[1], 0))
    runs = [[at(a0, d0 + 0.1), at(a0, s_mid - sw / 2 - 0.35)], [at(a0, s_mid + sw / 2 + 0.35), at(a0, d1), at(a1, d1), at(a1, d0 + 0.1)]]
    posts = 0
    for line in runs:
        for a, c in zip(line, line[1:]):
            length = math.hypot(c[0] - a[0], c[1] - a[1])
            if length < 0.3:
                continue
            for h in (spec["rail"], spec["rail"] * 0.5):
                pbeam(det, (*a, dz + h), (*c, dz + h), 0.07, 0.07, "white")
            m = max(1, int(round(length / 1.8)))
            for k in range(m + 1):
                x, y = a[0] + (c[0] - a[0]) * k / m, a[1] + (c[1] - a[1]) * k / m
                pbox(det, (x, y), u, 0.045, 0.045, dz, dz + spec["rail"] + 0.04, "white")
                posts += 1
    guard("beach bar", vol)
    guard("beach bar deck", det)
    print(f"  coast beach bar: floor {floor:.2f} m, eave {top:.2f} m, ridge {zr:.2f} m, deck {a1 - a0:.1f} x "
          f"{d1 - d0:.1f} m at {dz:.2f} m, {nr} risers of {rise:.2f} m, {posts} rail posts; "
          f"{tris(vol) + tris(det)} triangles")
    mats = [atlas_material()]
    return vol.mesh("B_" + b["id"], mats), det.mesh("BD_" + b["id"], mats)


# =====================================================================================================
# The white prow with its hut (item 26): a solid white masonry bow on the terrace's sea corner
# =====================================================================================================
def build_prow(b):
    spec = P.PROW
    cx, cy = spec["centre"]
    d = P.unit(*spec["bow_dir"])
    e = (-d[1], d[0])
    R = spec["radius"]
    tz = P.TERRACE_LEVEL + 0.04
    vol, det = B.Geo(), B.Geo()
    rim = []
    steps = 10
    for k in range(steps + 1):
        th = -math.pi / 2 + math.pi * k / steps
        r_along = R * 1.35
        x = cx + e[0] * R * math.sin(th) + d[0] * r_along * math.cos(th)
        y = cy + e[1] * R * math.sin(th) + d[1] * r_along * math.cos(th)
        out = P.unit(e[0] * math.sin(th) * r_along + d[0] * math.cos(th) * R, e[1] * math.sin(th) * r_along + d[1] * math.cos(th) * R)
        z = tz + spec["base"] + (spec["tip"] - spec["base"]) * math.cos(th) ** 2
        rim.append((x, y, z, out))
    arc = 0.0
    for (x0, y0, z0, o0), (x1, y1, z1, o1) in zip(rim, rim[1:]):
        f0 = (x0 + o0[0] * 1.3, y0 + o0[1] * 1.3)
        f1 = (x1 + o1[0] * 1.3, y1 + o1[1] * 1.3)
        g0 = min(E.ground_z(*f0), tz - 0.8) - 0.5
        g1 = min(E.ground_z(*f1), tz - 0.8) - 0.5
        seg = math.hypot(x1 - x0, y1 - y0)
        out = (o0[0] + o1[0], o0[1] + o1[1], 0.6)
        vol.face([(*f0, g0), (*f1, g1), (x1, y1, z1), (x0, y0, z0)], 0, out=out,
                 uvs=[P.band_uv("render", arc, 4.0 - (z0 - g0)), P.band_uv("render", arc + seg, 4.0 - (z1 - g1)),
                      P.band_uv("render", arc + seg, 4.0), P.band_uv("render", arc, 4.0)])
        arc += seg
    back = (cx - d[0] * 0.2, cy - d[1] * 0.2, tz + spec["base"])
    for (x0, y0, z0, _o0), (x1, y1, z1, _o1) in zip(rim, rim[1:]):                # the top: a fan rising to the bow
        pal(vol, [back, (x0, y0, z0), (x1, y1, z1)], "white", (0, 0, 1))
    a, c = rim[0], rim[-1]                                                         # the back wall, on the terrace
    wall_quad(vol, (c[0], c[1]), (a[0], a[1]), tz - 0.3, tz - 0.3, c[2], a[2], "render", 0.0, 2 * R, (-d[0], -d[1], 0))
    # hut on the north side: white render, flat roof with a small overhang, a grey door towards the terrace
    hx, hy = spec["hut"]
    sx, sy, sh = spec["hut_size"]
    hu = (-d[1], d[0])
    for sgn_u, sgn_v, out, half_len, dirv in ((0, -1, (-d[0], -d[1]), sx / 2, hu), (0, 1, d, sx / 2, hu),
                                              (1, 0, hu, sy / 2, d), (-1, 0, (-hu[0], -hu[1]), sy / 2, d)):
        mx = hx + hu[0] * sgn_u * sx / 2 + d[0] * sgn_v * sy / 2
        my = hy + hu[1] * sgn_u * sx / 2 + d[1] * sgn_v * sy / 2
        a = (mx - dirv[0] * half_len, my - dirv[1] * half_len)
        c = (mx + dirv[0] * half_len, my + dirv[1] * half_len)
        wall_quad(vol, a, c, tz - 0.3, tz - 0.3, tz + sh, tz + sh, "render", 0.0, 2 * half_len, (out[0], out[1], 0))
    pbox(det, (hx, hy), hu, sx / 2 + 0.15, sy / 2 + 0.15, tz + sh, tz + sh + 0.16, "white", skip=())
    door = (hx - d[0] * (sy / 2 + 0.02), hy - d[1] * (sy / 2 + 0.02))
    pal(det, [(door[0] - hu[0] * 0.5, door[1] - hu[1] * 0.5, tz + 0.02), (door[0] + hu[0] * 0.5, door[1] + hu[1] * 0.5, tz + 0.02),
              (door[0] + hu[0] * 0.5, door[1] + hu[1] * 0.5, tz + 2.05), (door[0] - hu[0] * 0.5, door[1] - hu[1] * 0.5, tz + 2.05)],
        "grey_door", (-d[0], -d[1], 0))
    guard("white prow", vol)
    print(f"  coast prow: bow {2 * R:.0f} m across, {spec['base']:.2f} to {spec['tip']:.2f} m above the terrace "
          f"({tz:.2f} m), hut {sx} x {sy} x {sh} m; {tris(vol) + tris(det)} triangles")
    mats = [atlas_material()]
    return vol.mesh("B_" + b["id"], mats), det.mesh("BD_" + b["id"], mats)


# =====================================================================================================
# Cove sea wall (item 12): the coursed-stone face under the kerb (env_beachfront.build_promenade calls this with
# its kerb samples) and the two-rail railing on top
# =====================================================================================================
def _beach_steps_spots():
    import street_plan as S
    st = S.PROM_STEPS
    return [S.route_side(t, -(st["start"] + d)) for t in (st["run"][0], sum(st["run"]) / 2, st["run"][1]) for d in (0.3, 2.0)]


WALL_BATTER = 0.16         # m outwards per metre of height (about 9 degrees, like a masonry sea wall)


def build_cove_wall(samples, skip):
    """samples: env_beachfront.kerb_samples of the cove line; skip: arc ranges left open (the stair's landing)."""
    g, rail_far, rail_near = B.Geo(), B.Geo(), B.Geo()
    h = B.KERB_W / 2
    rows = []
    for x, y, mx, my, s in samples:
        zt = B.kerb_top(x, y, mx, my)
        sand = min(B.ground(x + mx * (h + 0.5), y + my * (h + 0.5)), B.ground(x + mx * (h + 1.3), y + my * (h + 1.3)))
        foot = min(sand, zt - B.CAP_DEPTH - 0.05) - 0.35
        lean = WALL_BATTER * (zt - B.CAP_DEPTH - foot)        # the face leans back: its foot stands further out
        rows.append(((x + mx * (h - 0.03), y + my * (h - 0.03)), zt - B.CAP_DEPTH, foot, s,
                     (mx, my), zt, zt - sand, (x + mx * (h - 0.03 + lean), y + my * (h - 0.03 + lean))))
    for (p0, t0, f0, s0, m0, _z0, _h0, q0), (p1, t1, f1, s1, m1, _z1, _h1, q1) in zip(rows, rows[1:]):
        vm = P.ATLAS["stone"]["v_m"]
        g.face([(*q0, f0), (*q1, f1), (*p1, t1), (*p0, t0)], 0, out=(m0[0] + m1[0], m0[1] + m1[1], 0.3),
               uvs=[P.band_uv("stone", s0, vm - (t0 - f0)), P.band_uv("stone", s1, vm - (t1 - f1)),
                    P.band_uv("stone", s1, vm), P.band_uv("stone", s0, vm)])
    heights = [r[6] for r in rows]
    # railing on the kerb: left open at the stair's landing and at the white beach steps
    steps = _beach_steps_spots()
    open_at = lambda r: any(a - 0.3 <= r[3] <= b + 0.3 for a, b in skip) or \
        any(math.hypot(r[0][0] - sx, r[0][1] - sy) < 3.4 for sx, sy in steps)          # noqa: E731
    runs, cur = [], None
    for r in rows[::2] + ([rows[-1]] if len(rows) % 2 == 0 else []):
        x, y = r[0][0] - r[4][0] * (h - 0.03), r[0][1] - r[4][1] * (h - 0.03)             # the kerb's centre line
        if open_at(r) or r[6] < 0.6:
            cur = None
            continue
        near = P.route_distance(x, y) < P.ROUTE_CLEAR + 0.05
        if cur is None or cur[0] != near:                 # the joint between two runs belongs to the near one
            if cur is not None and cur[0]:
                cur[1].append((x, y, r[5]))
            start = [cur[1][-1]] if cur is not None and near else []
            cur = (near, start + [(x, y, r[5])])
            runs.append(cur)
        else:
            cur[1].append((x, y, r[5]))
    length = {True: 0.0, False: 0.0}
    for near, pts in runs:
        if len(pts) < 2:
            continue
        B.railing_line(rail_near if near else rail_far, pts, B.STEEL_SLOT, post=0.05,
                       rails=(P.COVE_RAIL["height"] / 2, P.COVE_RAIL["height"]), rail=0.045, step=P.COVE_RAIL["post_step"])
        length[near] += sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(pts, pts[1:]))
    finish("Coast_CoveWall", g)
    guard("Coast_Rails (cove)", rail_far)
    finish("Coast_CoveRail_NoCollide", rail_near, B.promenade_materials())
    apex = min(rows, key=lambda r: P.route_distance(*r[0]))
    print(f"  coast cove wall: {samples[-1][4]:.0f} m, height {min(heights):.2f} to {max(heights):.2f} m "
          f"({apex[6]:.2f} m where the road is nearest, {P.route_distance(*apex[0]):.2f} m from the centreline); railing "
          f"{length[False]:.0f} m solid, {length[True]:.0f} m beside the road without a collider; "
          f"{tris(g) + tris(rail_far) + tris(rail_near)} triangles")
    _state["cove_rail"] = rail_far


# =====================================================================================================
# Details: totem, fountain, twin bins, rip-rap chute, tamarisk hedge (items 9, 19)
# =====================================================================================================
def build_details():
    g = B.Geo()
    rng = random.Random(1611)
    t = P.TOTEM
    a = math.radians(t["bearing"])
    u = (math.sin(a), math.cos(a))
    z = E.ground_z(*t["at"])
    pbox(g, t["at"], u, 0.65, 0.5, z - 0.1, z + 0.06, "shade")                     # concrete pad
    pbox(g, t["at"], u, t["size"][0] / 2, t["size"][1] / 2, z + 0.06, z + 0.06 + t["size"][2], "anthracite")
    fx, fy = P.FOUNTAIN                                                             # stainless drinking fountain
    fz = E.ground_z(fx, fy)
    ring = [(fx + 0.15 * math.cos(k * math.pi / 3), fy + 0.15 * math.sin(k * math.pi / 3)) for k in range(6)]
    for k in range(6):
        p, q = ring[k], ring[(k + 1) % 6]
        pal(g, [(*p, fz - 0.1), (*q, fz - 0.1), (*q, fz + 0.9), (*p, fz + 0.9)], "galv",
            (p[0] + q[0] - 2 * fx, p[1] + q[1] - 2 * fy, 0))
    pal(g, [(fx + 0.2 * math.cos(k * math.pi / 3), fy + 0.2 * math.sin(k * math.pi / 3), fz + 0.9) for k in range(6)],
        "galv", (0, 0, 1))
    bx, by = P.TWIN_BINS                                                            # twin grey bins on one post
    bz = E.ground_z(bx, by)
    pbox(g, (bx, by), u, 0.04, 0.04, bz - 0.1, bz + 1.0, "steel_dark")
    for sgn in (-1, 1):
        pbox(g, (bx + u[0] * sgn * 0.27, by + u[1] * sgn * 0.27), u, 0.2, 0.16, bz + 0.45, bz + 0.98, "grey_door", skip=())
    stones = 0                                                                      # white rip-rap boulders
    for x, y, tu, _s in P.polyline_points(P.RIPRAP, 0.55):
        n = (-tu[1], tu[0])
        for off in (-0.45, 0.25):
            px, py = x + n[0] * (off + rng.uniform(-0.2, 0.2)), y + n[1] * (off + rng.uniform(-0.2, 0.2))
            r, hgt = rng.uniform(0.22, 0.42), rng.uniform(0.18, 0.4)
            pz = E.ground_z(px, py)
            ang = rng.uniform(0, math.pi)
            base = [(px + r * math.cos(ang + k * math.pi / 2) * rng.uniform(0.8, 1.2),
                     py + r * math.sin(ang + k * math.pi / 2) * rng.uniform(0.8, 1.2), pz - 0.08) for k in range(4)]
            tip = (px + rng.uniform(-0.08, 0.08), py + rng.uniform(-0.08, 0.08), pz + hgt)
            colour = rng.choice(("riprap", "white", "cream", "riprap"))
            for k in range(4):
                p, q = base[k], base[(k + 1) % 4]
                pal(g, [p, q, tip], colour, (p[0] + q[0] - 2 * px, p[1] + q[1] - 2 * py, 0.8))
            stones += 1
    pg = P.PERGOLA                                                                  # V18g: open frame beside the upper path
    a = math.radians(pg["bearing"])
    pu, (pl, pw, ph), pp = (math.sin(a), math.cos(a)), pg["size"], pg["post"]
    pv = (-pu[1], pu[0])
    corners = [(pg["at"][0] + pu[0] * sa * pl / 2 + pv[0] * sb * pw / 2, pg["at"][1] + pu[1] * sa * pl / 2 + pv[1] * sb * pw / 2)
               for sa, sb in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    pz = max(E.ground_z(*c) for c in corners) + ph
    for c in corners:
        pbox(g, c, pu, pp / 2, pp / 2, E.ground_z(*c) - 0.15, pz, "galv")
    for c0, c1 in zip(corners, corners[1:] + corners[:1]):
        pbeam(g, (*c0, pz), (*c1, pz), pp, pp * 1.3, "galv")
    for k in range(1, 6):                                                           # slats across
        f = k / 6.0
        p0 = (corners[0][0] + (corners[1][0] - corners[0][0]) * f, corners[0][1] + (corners[1][1] - corners[0][1]) * f)
        p1 = (corners[3][0] + (corners[2][0] - corners[3][0]) * f, corners[3][1] + (corners[2][1] - corners[3][1]) * f)
        pbeam(g, (*p0, pz + 0.08), (*p1, pz + 0.08), 0.07, 0.12, "galv")
    flowers = 0
    for hd, red in [(P.HEDGE, False)] + [(q, True) for q in P.RED_HEDGES]:           # tamarisk mass; red-flowering hedges
        st = P.polyline_points(hd["line"], 4.0 if not red else 2.0)
        half = hd["depth"] / 2
        sections = []
        for x, y, tu, s in st:
            n = (-tu[1], tu[0])
            k = hd["height"] * rng.uniform(0.86, 1.08)
            gz = min(E.ground_z(x + n[0] * half, y + n[1] * half), E.ground_z(x - n[0] * half, y - n[1] * half))
            prof = [(-half, gz - 0.2, 0.0), (-half * rng.uniform(0.9, 1.05), gz + 0.62 * k, 1.9), (-half * 0.5, gz + 0.95 * k, 2.7),
                    (half * 0.45, gz + k, 3.0), (half * rng.uniform(0.9, 1.05), gz + 0.6 * k, 1.9), (half, gz - 0.2, 0.0)]
            sections.append(([(x + n[0] * o, y + n[1] * o, zz, vv) for o, zz, vv in prof], s, n))
        for (pa, sa, na), (pb, sb, _nb) in zip(sections, sections[1:]):
            for k in range(5):
                a0, a1, b0, b1 = pa[k], pa[k + 1], pb[k], pb[k + 1]
                side = -1.0 if k < 2 else (1.0 if k > 2 else 0.0)
                g.face([a0[:3], b0[:3], b1[:3], a1[:3]], 0, out=(na[0] * side, na[1] * side, 1.0 if k in (1, 2, 3) else 0.2),
                       uvs=[P.band_uv("hedge", sa, a0[3]), P.band_uv("hedge", sb, b0[3]), P.band_uv("hedge", sb, b1[3]),
                            P.band_uv("hedge", sa, a1[3])])
        for (prof, s, n), sgn in ((sections[0], -1), (sections[-1], 1)):
            tu = (n[1], -n[0])
            g.face([q[:3] for q in prof], 0, out=(tu[0] * sgn, tu[1] * sgn, 0),
                   uvs=[P.band_uv("hedge", s + q[3] * 0.0 + k * 0.6, q[3]) for k, q in enumerate(prof)])
        if red:                                                                     # red blossoms on the top and the south face
            for (prof, s0, n) in sections:
                for _k in range(9):
                    q = prof[rng.choice((2, 3, 4))]
                    fx, fy, fz = q[0] + rng.uniform(-0.8, 0.8) * n[1] + rng.uniform(-0.2, 0.2) * n[0], \
                        q[1] - rng.uniform(-0.8, 0.8) * n[0] + rng.uniform(-0.2, 0.2) * n[1], q[2] + 0.04
                    d = rng.uniform(0.08, 0.14)
                    for quad in ([(fx - d, fy, fz), (fx + d, fy, fz), (fx + d, fy, fz + 2 * d), (fx - d, fy, fz + 2 * d)],
                                 [(fx - d, fy - d, fz + d), (fx + d, fy - d, fz + d), (fx + d, fy + d, fz + d), (fx - d, fy + d, fz + d)]):
                        pal(g, quad, "terracotta", None)
                        pal(g, quad[::-1], "terracotta", None)
                    flowers += 1
    guard("Coast_Details", g)
    finish("Coast_Details", g)
    print(f"  coast details: totem, fountain, twin bins, {stones} rip-rap stones, hedge {st[-1][3]:.0f} m; {tris(g)} triangles")


# =====================================================================================================
# Tunnel ramp: concrete upstand under the railings on both rims (item 14)
# =====================================================================================================
UPSTAND_H, UPSTAND_W = 0.28, 0.3


def ramp_rim_lines():
    """Per side, the railing line on top of the ramp's cut walls: [(x, y, ground z)] (env_corridor.railing_line)."""
    import env_corridor
    out = {}
    pts = E.route_points()
    fr = env_corridor.frames(pts)
    for side in (1, -1):
        line = []
        for p in pts:
            off = env_corridor.railing_line(p, side)
            if off is None:
                continue
            nrm = fr[p["i"]][1]
            x, y = p["x"] + nrm[0] * off * side, p["y"] + nrm[1] * off * side
            line.append((x, y, E.ground_z(x, y)))
        out[side] = line
    return out


def build_ramp_upstand():
    g = B.Geo()
    total = 0.0
    for side, line in ramp_rim_lines().items():
        for (x0, y0, z0), (x1, y1, z1) in zip(line, line[1:]):
            u = P.unit(x1 - x0, y1 - y0)
            n = (-u[1], u[0])
            hw = UPSTAND_W / 2
            a0, a1 = (x0 + n[0] * hw, y0 + n[1] * hw), (x1 + n[0] * hw, y1 + n[1] * hw)
            b0, b1 = (x0 - n[0] * hw, y0 - n[1] * hw), (x1 - n[0] * hw, y1 - n[1] * hw)
            pal(g, [(*a0, z0 - 0.2), (*a1, z1 - 0.2), (*a1, z1 + UPSTAND_H), (*a0, z0 + UPSTAND_H)], "beige", (n[0], n[1], 0))
            pal(g, [(*b0, z0 - 0.2), (*b1, z1 - 0.2), (*b1, z1 + UPSTAND_H), (*b0, z0 + UPSTAND_H)], "beige", (-n[0], -n[1], 0))
            pal(g, [(*a0, z0 + UPSTAND_H), (*a1, z1 + UPSTAND_H), (*b1, z1 + UPSTAND_H), (*b0, z0 + UPSTAND_H)], "beige", (0, 0, 1))
            total += math.hypot(x1 - x0, y1 - y0)
    finish("Coast_RampUpstand_NoCollide", g)
    print(f"  coast ramp: {UPSTAND_H} m upstand under the rim railings, {total:.0f} m; {tris(g)} triangles")


# =====================================================================================================
# Flush paint on the road (env_trackside.build_lines hook): faces go into Track_EdgeLines, which has no collider.
# Material index 0 = white, 1 = graphite (the edge lines' own two materials).
# =====================================================================================================
def road_paint(bm, pts, fr):
    import env_track as T
    import env_trackside
    lift = env_trackside.LINE_LIFT
    made = 0

    def quad(corners, mat):
        f = bm.faces.new([bm.verts.new(c) for c in corners])
        f.material_index = mat
        return 1

    def road_point(t):
        i = int(math.floor(t))
        a, b = pts[i % len(pts)], pts[(i + 1) % len(pts)]
        k = t - i
        return (a["x"] + (b["x"] - a["x"]) * k, a["y"] + (b["y"] - a["y"]) * k,
                T.road_z(a) + (T.road_z(b) - T.road_z(a)) * k + lift), P.unit(b["x"] - a["x"], b["y"] - a["y"])

    for t, length, colour in P.ROAD_ACROSS:               # strips across the road
        (x, y, z), u = road_point(t)
        n = (-u[1], u[0])
        hl = length / 2
        for s0, s1 in ((-P.PAINT_HALF, 0.0), (0.0, P.PAINT_HALF)):
            made += quad([(x + n[0] * s0 - u[0] * hl, y + n[1] * s0 - u[1] * hl, z), (x + n[0] * s0 + u[0] * hl, y + n[1] * s0 + u[1] * hl, z),
                          (x + n[0] * s1 + u[0] * hl, y + n[1] * s1 + u[1] * hl, z), (x + n[0] * s1 - u[0] * hl, y + n[1] * s1 - u[1] * hl, z)],
                         0 if colour == "white" else 1)

    arcs = P.ROAD_ARCS or dict(centre=(0.0, 0.0), radii=(), width=0.3)   # concentric kerb arcs (off: see the plan)
    cx, cy = arcs["centre"]

    def on_road(x, y):
        best = None
        for i in range(350, 380):
            a, b = pts[i], pts[i + 1]
            dx, dy = b["x"] - a["x"], b["y"] - a["y"]
            k = max(0.0, min(1.0, ((x - a["x"]) * dx + (y - a["y"]) * dy) / (dx * dx + dy * dy)))
            d = math.hypot(x - a["x"] - k * dx, y - a["y"] - k * dy)
            if best is None or d < best[0]:
                best = (d, T.road_z(a) + (T.road_z(b) - T.road_z(a)) * k + lift)
        return best
    for r in arcs["radii"]:
        prev = None
        for k in range(0, 721):
            ang = math.radians(180.0 + k * 0.25)          # the southern half circle
            inner = (cx + (r - arcs["width"] / 2) * math.cos(ang), cy + (r - arcs["width"] / 2) * math.sin(ang))
            outer = (cx + (r + arcs["width"] / 2) * math.cos(ang), cy + (r + arcs["width"] / 2) * math.sin(ang))
            di, do = on_road(*inner), on_road(*outer)
            cur = (inner, outer, (di[1] + do[1]) / 2) if max(di[0], do[0]) <= P.PAINT_HALF else None
            if cur and prev and k % 4 == 0:
                made += quad([(*prev[0], prev[2]), (*cur[0], cur[2]), (*cur[1], cur[2]), (*prev[1], prev[2])], 0)
                prev = cur
            elif cur and not prev:
                prev = cur
            elif not cur:
                prev = None

    d = P.TUNNEL_DRAIN                                    # the tunnel's lengthwise slot drain
    for i in range(P.PORTAL_S, P.PORTAL_N):
        a, b = pts[i], pts[i + 1]
        (_ta, na), (_tb, nb) = fr[i], fr[i + 1]
        o0, o1 = d["offset"] - d["width"] / 2, d["offset"] + d["width"] / 2
        za, zb = T.road_z(a) + lift, T.road_z(b) + lift
        made += quad([(a["x"] + na[0] * o0, a["y"] + na[1] * o0, za), (b["x"] + nb[0] * o0, b["y"] + nb[1] * o0, zb),
                      (b["x"] + nb[0] * o1, b["y"] + nb[1] * o1, zb), (a["x"] + na[0] * o1, a["y"] + na[1] * o1, za)], 1)
    print(f"  coast road paint (flush, in Track_EdgeLines, no collider): {made} quads: slot drains, three step "
          f"nosings, the tunnel's drain")


# =====================================================================================================
# Dressing (called by env_street_objects.dress_promenade before it writes the placement files)
# =====================================================================================================
def _entry(proto, x, y, z, rot_z=0.0, scale=1.0, **extra):
    e = {"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(z, 3), "rot_z": round(rot_z, 4),
         "scale": round(scale, 3)}
    e.update({k: round(v, 4) + 0.0 for k, v in extra.items()})
    return e


def dress(veg, props):
    """(veg, props) after this plan's edits. Explicit and local: drops, re-seats, additions; everything else stays."""
    import env_terrain
    rng = random.Random(1612)
    bar = bar_layout()
    wall = [(x, y) for x, y, *_r in forecourt_wall()]

    def dropped(it):
        if any(x0 <= it["x"] <= x1 and y0 <= it["y"] <= y1 and it["proto"].startswith(pre) for x0, y0, x1, y1, pre in P.DROP):
            return True
        if it["proto"].startswith("Prop_") and min(P.seg_distance(it["x"], it["y"], a, b) for a, b in zip(wall, wall[1:])) < 2.2:
            return True                                   # stands where the forecourt's white wall now is
        return False
    gone = [it for it in list(veg) + list(props) if dropped(it)]
    ids = {id(it) for it in gone}
    veg = [it for it in veg if id(it) not in ids]
    props = [it for it in props if id(it) not in ids]

    seated = lifted = 0
    rims = ramp_rim_lines()
    rim_pts = [q for line in rims.values() for q in line]
    for it in veg + props:
        x, y = it["x"], it["y"]
        if E.inside_polygon(x, y, bar["deck"]):              # furniture on the bar's deck
            it["z"] = round(bar["deck_z"], 3)
            seated += 1
        elif P.in_changed_terrain(x, y) or coast.in_cove(x, y):
            z = round(E.ground_z(x, y), 3)
            if abs(z - it["z"]) > 0.02:
                it["z"] = z
                seated += 1
        elif it["proto"] == "Prop_RailingSegment" and rim_pts and min(math.hypot(x - q[0], y - q[1]) for q in rim_pts) < 1.2:
            it["z"] = round(it["z"] + UPSTAND_H - 0.03, 3)   # the ramp's railings stand on the new upstand
            lifted += 1

    new_v, new_p = [], []

    def add(lst, proto, x, y, rot=0.0, scale=1.0, clear=P.ROUTE_CLEAR, z=None, **extra):
        d = P.route_distance(x, y)
        if d < clear:
            raise RuntimeError(f"V16e coast dressing: {proto} at ({x:.1f}, {y:.1f}) is {d:.2f} m from the race line "
                               f"(limit {clear} m)")
        lst.append(_entry(proto, x, y, E.ground_z(x, y) if z is None else z, rot, scale, **extra))

    for x, y, s in P.PALM_CLUSTER:                        # Canary date palms on the berm's car-park side
        add(new_v, rng.choice(("Veg_Palm", "Veg_Palm_B")), x, y, rng.uniform(-math.pi, math.pi), s, clear=P.TREE_CLEAR)
    for x, y, s in P.UMBRELLA_TREES:                      # two more dense umbrella trees: seven on the lawn terrace
        add(new_v, rng.choice(("Veg_UmbrellaPine", "Veg_UmbrellaPine_B")), x, y, rng.uniform(-math.pi, math.pi), s,
            clear=P.TREE_CLEAR + 5.0 * s)
    others = veg + props
    for t, off in P.WASHINGTONIAS:                        # slim fan palms along the car park
        x, y = P.route_side(t, off)
        if all(math.hypot(x - q["x"], y - q["y"]) > 2.5 for q in others):
            add(new_v, "Veg_SlimPalm", x, y, rng.uniform(-math.pi, math.pi), rng.uniform(0.95, 1.1), clear=P.TREE_CLEAR)
    line = P.BERM["line"]
    total = sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(line, line[1:]))
    placed = 0
    for k in range(P.BERM_SHRUBS * 6):                    # grey-green shrubs on the bank
        if placed >= P.BERM_SHRUBS:
            break
        s = rng.uniform(2.0, total - 2.0)
        v = rng.uniform(-4.0, -0.8)                       # V18g: on the bank, south of the upper level's edge
        pts = P.polyline_points(line, 1.0)
        x0, y0, u, _s = min(pts, key=lambda q: abs(q[3] - s))
        x, y = x0 - u[1] * v, y0 + u[0] * v
        if P.berm_rise(x, y) < 0.1 or P.route_distance(x, y) < 12.0:
            continue
        if any(math.hypot(x - q["x"], y - q["y"]) < 2.0 for q in others + new_v + new_p):
            continue
        if any(math.hypot(x - a, y - b) < 2.5 for a, b in P.RIPRAP):
            continue
        add(new_v, rng.choice(("Veg_Shrub", "Veg_Shrub_B")), x, y, rng.uniform(-math.pi, math.pi), rng.uniform(1.25, 1.9))
        placed += 1
    def free(x, y, gap):
        return all(math.hypot(x - q["x"], y - q["y"]) > gap for q in others + new_v + new_p)
    for x, y, deg, proto in P.CREST_CARS:                 # V18g: parked cars where the satellite view has them
        if free(x, y, 2.0):
            add(new_p, proto, x, y, math.radians(90.0 - deg) + rng.choice((0.0, math.pi)) + rng.uniform(-0.05, 0.05),
                clear=P.TREE_CLEAR)
    t, t1, step, off = P.PATH_POSTS                       # V18g: low timber posts on the upper path's north edge
    while t <= t1:
        x, y = P.route_side(t, off)
        if free(x, y, 1.6) and all(math.hypot(x - a, y - b) > 2.6 for a, b in P.PATH_POST_SKIP):
            add(new_p, "Prop_TimberPost", x, y)
        t += step
    posts = []                                            # V18h: 1 m timber bollards (built by build_trim, not props)
    wx, wy = P.WASTE["at"]
    for line, off, step in P.POST_LINES:
        for x0, y0, u, _s in P.polyline_points(line, step):
            x, y = x0 - u[1] * off, y0 + u[0] * off
            if P.route_distance(x, y) >= P.ROUTE_CLEAR + 0.1 and free(x, y, 1.0) and \
                    not (abs(x - wx) < 3.2 and abs(y - wy) < 1.6) and math.hypot(x - P.PERGOLA["at"][0], y - P.PERGOLA["at"][1]) > 3.6:
                posts.append((x, y))
    _state["posts"] = posts
    for x, y, sc in P.EXTRA_PALMS:                        # second round: the palm plaza's loose grid
        if free(x, y, 2.6):
            add(new_v, rng.choice(("Veg_Palm", "Veg_Palm_B")), x, y, rng.uniform(-math.pi, math.pi), sc, clear=P.TREE_CLEAR)
    bl = P.BELT                                           # the shrub belt on the mulch bed at the north bank's foot
    x = bl["first"]
    belt = 0
    while x <= bl["last"]:
        bx, by = x + rng.uniform(-0.5, 0.5), P.north_kerb_y(x) + bl["back"] + rng.uniform(-0.3, 0.5)
        if free(bx, by, 1.7) and not (abs(bx - wx) < 3.4 and abs(by - wy) < 1.8) and P.route_distance(bx, by) >= P.TREE_CLEAR \
                and math.hypot(bx - P.PERGOLA["at"][0], by - P.PERGOLA["at"][1]) > 4.5:
            add(new_v, rng.choice(("Veg_Shrub", "Veg_Shrub_B")), bx, by, rng.uniform(-math.pi, math.pi), rng.uniform(*bl["scale"]))
            belt += 1
        x += bl["step"]
    for x, y in P.PLAZA_SAPLINGS:                         # V18g: young trees along the east road's south kerb
        if free(x, y, 2.0):
            add(new_v, "Veg_Sapling", x, y, rng.uniform(-math.pi, math.pi), rng.uniform(1.0, 1.25), clear=P.TREE_CLEAR)
    for x, y, proto, scale in P.LAWN_TREES:               # V18g: the dense trees between the bar and the plaza
        if free(x, y, 2.5):
            add(new_v, proto, x, y, rng.uniform(-math.pi, math.pi), scale, clear=P.TREE_CLEAR)
    for t, off in P.FORE["bollards"]:                     # steel bollards at the forecourt's west end, off the line
        x, y = P.route_side(t, off)
        add(new_p, "Prop_Bollard", x, y)
    # close-bar fence along the deck edge of the south portal (on the headwall's top, far above the road)
    (p, (dx, dy)) = env_terrain.portals()[0]
    import env_tunnel
    nrm = (-dy, dx)
    topz = p["track_z"] + env_terrain.PROFILE.get("tunnel_cover_m", 4.8) + 1.0
    half = env_tunnel.HEADWALL_HALF - 0.1
    k = 0
    while -half + (k + 1) * 2.0 <= half:
        s = -half + k * 2.0
        new_p.append(_entry("Prop_RailingSegment", p["x"] + nrm[0] * s + dx * 0.3, p["y"] + nrm[1] * s + dy * 0.3, topz,
                            math.atan2(nrm[1], nrm[0]), rot_y=0.0))
        k += 1
    lifted_paving = 0
    import street_plan as S
    for it in veg + props + new_v + new_p:                # V18h: what stands on the new paving stands on its surface
        if it["proto"].startswith("Prop_RailingSegment") or not P.paved(it["x"], it["y"]):
            continue
        if (S.layer_at(it["x"], it["y"]) or {}).get("mat") or P.route_distance(it["x"], it["y"]) < 5.0:
            continue
        z = round(E.ground_z(it["x"], it["y"]) + P.PAVING_LIFT, 3)
        if abs(z - it["z"]) < 0.3 and abs(z - it["z"]) > 0.005:
            it["z"] = z
            lifted_paving += 1
    _state["veg"] = veg + new_v
    _state["items"] = veg + props + new_v + new_p
    print(f"  coast dressing (V18h): {len(posts)} spots for timber bollards, {belt} belt shrubs, {lifted_paving} items stand on the paving")
    print(f"  coast dressing: removed {', '.join(sorted(it['proto'] + '@' + str(round(it['x'])) + ',' + str(round(it['y'])) for it in gone)) or 'nothing'}; "
          f"{seated} items seated on the new ground or deck, {lifted} ramp railings lifted onto the upstand; added "
          f"{len(new_v)} plants, {len(new_p)} props ({k} fence segments on the south portal's deck)")
    return veg + new_v, props + new_p


# =====================================================================================================
# V18h: the paved ground (coast_plan.paving_layers) as one sheet of real paving with tiling textures, kerb stones
# along its edges, covers and gullies, the white precast seat blocks, palm pits and bins. Street View Oct 2009 /
# Oct 2019 / Dec 2022 for what is there, the orthophoto for where.
#   Coast_Paving            the sheet, 7 cm over the terrain, cut at the road's keep-out (4.95 m): W_coast_concrete_beige
#                           / _ochre / _pale, the streets' setts and asphalt
#   Coast_Edges_NoCollide   kerb stones (5 cm), manhole covers and gully grates (flush): W_coast_trim
#   Coast_Trim              seat blocks, the kerb-bench east of the portal with two lamp posts, palm pits, bins
# =====================================================================================================
def concrete_material(key):
    import env_streets as ST
    name = "coast_concrete_" + key
    return ST._textured(name, P.CONCRETE[key]["colour"], name, 0.9)


def paving_material(key):
    import env_streets as ST
    return concrete_material(key) if key in P.CONCRETE else ST.mat(key)


def trim_material():
    return B.textured(E.material("coast_trim", "#E6E2D8", 0.9), "coast_trim", 0.9)


def road_look(key):
    """env_track.ROAD_LOOK hook: registers the coast's concrete `key` as a road surface (material and UV frame of the
    paving beside the road) and returns the surface's name."""
    import env_track as T
    name = "coast_" + key
    looks = T._looks()
    if name not in looks:
        o, u = P.FRAMES[key]
        tile = P.CONCRETE[key]["tile"]
        looks[name] = (concrete_material(key), T._frame(o, u, tile, tile))
    return name


def paving_z(x, y):
    import env_streets as ST
    import street_plan as S
    return ST.ground(x, y) + ST.lift_at(S.route_distance(x, y))


def build_paving():
    import bpy
    import env_streets as ST
    import street_plan as S
    layers = P.paving_layers()
    xs = [q[0] for la in layers for q in la["ring"]]
    ys = [q[1] for la in layers for q in la["ring"]]
    box = (min(xs), min(ys), max(xs), max(ys))
    mine = len(layers)
    for la in S.LAYERS:                                   # the streets' own layers (paving or a hole) win: cut along them
        b = (min(q[0] for q in la["ring"]), min(q[1] for q in la["ring"]), max(q[0] for q in la["ring"]), max(q[1] for q in la["ring"]))
        if b[2] > box[0] and b[0] < box[2] and b[3] > box[1] and b[1] < box[3]:
            layers.append(dict(name="street: " + la["name"], mat=None, ring=la["ring"], uv=None))
    soup = ST.overlay(layers)
    soup.tris = [t for t in soup.tris if t[3] < mine]
    soup.refine_near_route(3.0)
    soup.clip_route()
    soup.follow_ground(paving_z)
    keys = []
    for la in layers[:mine]:
        if la["mat"] not in keys:
            keys.append(la["mat"])
    z = [paving_z(x, y) for x, y in soup.xy]
    used, verts, faces, slots, uvs = {}, [], [], [], []
    area = {}
    for a, b, c, tag in soup.tris:
        (xa, ya), (xb, yb), (xc, yc) = soup.xy[a], soup.xy[b], soup.xy[c]
        if (xb - xa) * (yc - ya) - (xc - xa) * (yb - ya) < 0:
            a, b, c = c, b, a
        tri = []
        for v in (a, b, c):
            if v not in used:
                used[v] = len(verts)
                verts.append((soup.xy[v][0], soup.xy[v][1], z[v]))
            tri.append(used[v])
        la = layers[tag]
        faces.append(tri)
        slots.append(keys.index(la["mat"]))
        uvs.append([ST._layer_uv(la, soup.xy[v]) for v in (a, b, c)])
        area[la["mat"]] = area.get(la["mat"], 0.0) + abs((xb - xa) * (yc - ya) - (xc - xa) * (yb - ya)) / 2
    mesh = bpy.data.meshes.new("Coast_Paving")
    mesh.from_pydata(verts, [], faces)
    for k in keys:
        mesh.materials.append(paving_material(k))
    mesh.polygons.foreach_set("material_index", slots)
    uv = mesh.uv_layers.new(name="UVMap")
    for poly, tri_uv in zip(mesh.polygons, uvs):
        for li, t in zip(poly.loop_indices, tri_uv):
            uv.data[li].uv = t
    mesh.validate()
    mesh.update()
    E.new_object("Coast_Paving", mesh, E.collection(COLLECTION))
    print(f"  coast paving (V18h): {len(faces)} triangles; " + ", ".join(f"{k} {v:.0f} m2" for k, v in area.items()))


def tpal(g, corners, colour, out):
    g.face(corners, 0, out=out, uvs=[P.trim_pal_uv(colour)] * len(corners))


def tbox(g, c, u, hu, hv, z0, z1, s0=0.0, side="precast", top="precast_top", slot=0):
    """Box in the trim atlas, centred at c, long axis u: its long faces take the band `side` (u along the box, v up
    from the foot), the top takes `top`; the two ends take the side band across."""
    ux, uy = u
    vx, vy = -uy, ux

    def p(a, b, z):
        return (c[0] + ux * a + vx * b, c[1] + uy * a + vy * b, z)
    h = z1 - z0
    vm = P.TRIM[side]["v_m"]
    for sgn in (1, -1):
        g.face([p(-hu, sgn * hv, z0), p(hu, sgn * hv, z0), p(hu, sgn * hv, z1), p(-hu, sgn * hv, z1)], slot, out=(vx * sgn, vy * sgn, 0),
               uvs=[P.trim_uv(side, s0, vm - h), P.trim_uv(side, s0 + 2 * hu, vm - h), P.trim_uv(side, s0 + 2 * hu, vm),
                    P.trim_uv(side, s0, vm)])
        g.face([p(sgn * hu, -hv, z0), p(sgn * hu, hv, z0), p(sgn * hu, hv, z1), p(sgn * hu, -hv, z1)], slot, out=(ux * sgn, uy * sgn, 0),
               uvs=[P.trim_uv(side, s0 + 0.1, vm - h), P.trim_uv(side, s0 + 0.1 + 2 * hv, vm - h),
                    P.trim_uv(side, s0 + 0.1 + 2 * hv, vm), P.trim_uv(side, s0 + 0.1, vm)])
    g.face([p(-hu, -hv, z1), p(hu, -hv, z1), p(hu, hv, z1), p(-hu, hv, z1)], slot, out=(0, 0, 1),
           uvs=[P.trim_uv(top, s0, 0.0), P.trim_uv(top, s0 + 2 * hu, 0.0), P.trim_uv(top, s0 + 2 * hu, 2 * hv),
                P.trim_uv(top, s0, 2 * hv)])


def tcyl(g, x, y, r, z0, z1, colour, n=10):
    ring = [(x + r * math.cos(2 * math.pi * k / n), y + r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    for k in range(n):
        a, b = ring[k], ring[(k + 1) % n]
        tpal(g, [(*a, z0), (*b, z0), (*b, z1), (*a, z1)], colour, (a[0] + b[0] - 2 * x, a[1] + b[1] - 2 * y, 0))
    tpal(g, [(*q, z1) for q in ring], colour, (0, 0, 1))


def build_edges():
    """Kerb stones along the paving's edges, covers and gullies: low and without a collider."""
    import street_plan as S
    g = B.Geo()
    hw, kh = P.KERB_W / 2, P.KERB_H
    total = 0.0
    for line in P.KERBS:
        run = []
        for x, y, u, s in P.polyline_points(line, 1.0):
            if S.route_distance(x, y) < S.KEEP_OUT + 0.2 or (S.layer_at(x, y) or {}).get("mat"):
                run = []
                continue
            n = (-u[1], u[0])
            run.append(((x + n[0] * hw, y + n[1] * hw), (x - n[0] * hw, y - n[1] * hw), paving_z(x, y), s, n))
            if len(run) < 2:
                continue
            (a0, b0, z0, s0, n0), (a1, b1, z1, s1, n1) = run[-2], run[-1]
            if s1 - s0 > 1.6:                             # the line was interrupted: no stone across the gap
                continue
            g.face([(*a0, z0 + kh), (*a1, z1 + kh), (*b1, z1 + kh), (*b0, z0 + kh)], 0, out=(0, 0, 1),
                   uvs=[P.trim_uv("kerb", s0, 0.03), P.trim_uv("kerb", s1, 0.03), P.trim_uv("kerb", s1, 0.22), P.trim_uv("kerb", s0, 0.22)])
            for (q0, q1), sgn in (((a0, a1), 1), ((b0, b1), -1)):
                g.face([(*q0, z0 - 0.06), (*q1, z1 - 0.06), (*q1, z1 + kh), (*q0, z0 + kh)], 0, out=(n0[0] * sgn, n0[1] * sgn, 0),
                       uvs=[P.trim_uv("kerb", s0, 0.0), P.trim_uv("kerb", s1, 0.0), P.trim_uv("kerb", s1, 0.03), P.trim_uv("kerb", s0, 0.03)])
            total += s1 - s0
    def sett_band(a, b, width):
        """Flush band of dark setts from a to b; left out where the race road or a street layer lies."""
        n = max(1, int(round(math.hypot(b[0] - a[0], b[1] - a[1]) / 0.8)))
        u = P.unit(b[0] - a[0], b[1] - a[1])
        nv = (-u[1] * width / 2, u[0] * width / 2)
        made, prev = 0.0, None
        for k in range(n + 1):
            x, y = a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n
            ok = S.route_distance(x, y) >= S.KEEP_OUT + 0.15 and P.paved(x, y) and not (S.layer_at(x, y) or {}).get("mat")
            cur = (x, y, paving_z(x, y) + 0.014, k * math.hypot(b[0] - a[0], b[1] - a[1]) / n) if ok else None
            if cur and prev:
                (x0, y0, z0, s0), (x1, y1, z1, s1) = prev, cur
                g.face([(x0 - nv[0], y0 - nv[1], z0), (x1 - nv[0], y1 - nv[1], z1), (x1 + nv[0], y1 + nv[1], z1), (x0 + nv[0], y0 + nv[1], z0)],
                       0, out=(0, 0, 1), uvs=[P.trim_uv("setts", s0, 0.25 * (0.5 - width / 0.5)), P.trim_uv("setts", s1, 0.25 * (0.5 - width / 0.5)),
                                              P.trim_uv("setts", s1, 0.25 * (0.5 + width / 0.5)), P.trim_uv("setts", s0, 0.25 * (0.5 + width / 0.5))])
                made += s1 - s0
            prev = cur
        return made
    setts = 0.0
    for (sx, sy), deg in P.SETT_BANDS:                    # the diagonal bands across the upper road, the single one
        a = math.radians(deg)
        d = (P.UPPER_U[0] * math.cos(a) + P.UPPER_U[1] * math.sin(a), P.UPPER_U[1] * math.cos(a) - P.UPPER_U[0] * math.sin(a))
        reach = 0.0
        while reach < 30.0 and P.inside(sx + d[0] * (reach + 0.5), sy + d[1] * (reach + 0.5), P.UPPER):
            reach += 0.5
        setts += sett_band((sx + d[0] * 0.15, sy + d[1] * 0.15), (sx + d[0] * reach, sy + d[1] * reach), P.SETT_W)
    rp = P.RAMP                                           # the stepped ramp: a sett band on every nosing, the slot drain
    for y in rp["nosings"]:
        setts += sett_band((rp["x"][0], y), (rp["x"][1], y - 0.06), 0.3)
    dz = [(x, rp["drain"]) for x in (rp["x"][0], rp["x"][1])]
    if all(S.route_distance(*q) >= S.KEEP_OUT + 0.2 for q in dz):
        za, zb = paving_z(*dz[0]) + 0.014, paving_z(*dz[1]) + 0.014
        tpal(g, [(dz[0][0], dz[0][1] - 0.075, za), (dz[1][0], dz[1][1] - 0.075, zb), (dz[1][0], dz[1][1] + 0.075, zb),
                 (dz[0][0], dz[0][1] + 0.075, za)], "iron", (0, 0, 1))
    covers = 0
    for cell, spots, half, sides in ((0, P.MANHOLES, 0.42, 12), (1, P.GULLIES, 0.27, 4)):
        for x, y in spots:
            if S.route_distance(x, y) < S.KEEP_OUT + half + 0.1 or not P.paved(x, y):
                continue
            z = max(paving_z(x + dx, y + dy) for dx in (-half, 0, half) for dy in (-half, 0, half)) + 0.012
            if sides == 4:
                quad = ((-1, -1), (1, -1), (1, 1), (-1, 1))
                g.face([(x + half * a, y + half * b, z) for a, b in quad], 0, out=(0, 0, 1),
                       uvs=[P.iron_uv(cell, 0.5 + 0.5 * a, 0.5 + 0.5 * b) for a, b in quad])
            else:
                ring = [(math.cos(2 * math.pi * k / sides), math.sin(2 * math.pi * k / sides)) for k in range(sides)]
                g.face([(x + half * c, y + half * sn, z) for c, sn in ring], 0, out=(0, 0, 1),
                       uvs=[P.iron_uv(cell, 0.5 + 0.485 * c, 0.5 + 0.485 * sn) for c, sn in ring])
            covers += 1
    finish("Coast_Edges_NoCollide", g, [trim_material()])
    print(f"  coast edges (V18h, no collider): kerb stones {total:.0f} m, sett bands {setts:.0f} m, {covers} covers and gullies; "
          f"{tris(g)} triangles")


def build_trim(veg):
    """Seat and barrier blocks, the kerb-bench east of the portal with its lamp posts, timber bollards, palm pits, the
    waste station, the ramp's bollards and hydrant. Everything keeps ROUTE_CLEAR from the race centreline (guard)."""
    g = B.Geo()
    blocks = 0

    def block(c, u, size, s0):
        half = size[0] / 2
        zs = [paving_z(c[0] + u[0] * k * half, c[1] + u[1] * k * half) for k in (-1, 0, 1)]
        ends = [(c[0] + u[0] * k * half, c[1] + u[1] * k * half) for k in (-1, 1)]
        if all(P.route_distance(*q) >= P.ROUTE_CLEAR + size[1] / 2 + 0.1 for q in ends + [c]):
            tbox(g, c, u, half, size[1] / 2, min(zs) - 0.25, max(zs) + size[2], s0=s0)
            return 1
        return 0
    bl = P.BLOCK
    for row in P.BLOCK_ROWS:
        size = row.get("size", (bl["length"], bl["width"], bl["height"]))
        pts = P.polyline_points(row["line"], 0.2)
        total = pts[-1][3]
        s = 0.0
        while s + size[0] <= total + 1e-6:
            a = min(pts, key=lambda q: abs(q[3] - s))
            b = min(pts, key=lambda q: abs(q[3] - (s + size[0] - 0.06)))
            u = P.unit(b[0] - a[0], b[1] - a[1])
            blocks += block(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2), u, (math.hypot(b[0] - a[0], b[1] - a[1]), size[1], size[2]), 2.4 * (blocks % 4))
            s += row["pitch"]
    others = _state.get("items", [])
    for t, off in P.GAP_BLOCKS:                             # barrier blocks on both edges of the gap section
        c = P.route_side(t, off)
        if all(math.hypot(c[0] - q["x"], c[1] - q["y"]) > 2.2 for q in others):
            blocks += block(c, P.route_at(t)[1], P.GAP_BLOCK, 2.4 * (blocks % 4) + 0.5)
    for x, y in P.BENCH_LAMPS:                              # slim dark posts with a flat head (Dec 2022)
        z = paving_z(x, y)
        pbox_t(g, (x, y), (1.0, 0.0), 0.06, 0.06, z, z + 5.2, "anthracite")
        pbox_t(g, (x, y - 0.35), (1.0, 0.0), 0.16, 0.5, z + 5.2, z + 5.28, "anthracite", skip=())
        tpal(g, [(x - 0.12, y - 0.75, z + 5.195), (x + 0.12, y - 0.75, z + 5.195), (x + 0.12, y - 0.15, z + 5.195),
                 (x - 0.12, y - 0.15, z + 5.195)], "lamp", (0, 0, -1))
    r, ph = P.POST["radius"], P.POST["height"]
    posts = 0
    for x, y in _state.get("posts", []):                    # round timber bollards, 1 m, on a small concrete collar
        z = E.ground_z(x, y)
        ring = [(x + r * math.cos(2 * math.pi * k / 8), y + r * math.sin(2 * math.pi * k / 8)) for k in range(8)]
        for k in range(8):
            a, b = ring[k], ring[(k + 1) % 8]
            g.face([(*a, z - 0.15), (*b, z - 0.15), (*b, z + ph), (*a, z + ph)], 0, out=(a[0] + b[0] - 2 * x, a[1] + b[1] - 2 * y, 0),
                   uvs=[P.trim_uv("timber", 0.0, 0.03 * k + 0.02), P.trim_uv("timber", 0.0, 0.03 * k + 0.05),
                        P.trim_uv("timber", ph + 0.15, 0.03 * k + 0.05), P.trim_uv("timber", ph + 0.15, 0.03 * k + 0.02)])
        tpal(g, [(*q, z + ph) for q in ring], "timber", (0, 0, 1))
        pbox_t(g, (x, y), (1.0, 0.0), 0.17, 0.17, z - 0.1, z + 0.04, "kerb")
        posts += 1
    pits = 0
    h = P.PALM_PIT / 2
    for it in veg:                                          # square pit with a dark edging and mulch round each palm
        if not it["proto"].startswith("Veg_Palm") or not P.inside(it["x"], it["y"], P.UPPER):
            continue
        x, y = it["x"], it["y"]
        if P.route_distance(x, y) < P.ROUTE_CLEAR + h * 1.5:
            continue
        z = max(paving_z(x + dx, y + dy) for dx in (-h, h) for dy in (-h, h))
        u = P.UPPER_U
        v = (-u[1], u[0])
        for (ca, cb), axis in (((0, 1), u), ((0, -1), u), ((1, 0), v), ((-1, 0), v)):
            c = (x + u[0] * ca * h + v[0] * cb * h, y + u[1] * ca * h + v[1] * cb * h)
            pbox_t(g, c, axis, h + 0.06, 0.06, z - 0.2, z + 0.06, "sett")
        corners = [(x + u[0] * a * h + v[0] * b * h, y + u[1] * a * h + v[1] * b * h, z + 0.03) for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        g.face(corners, 0, out=(0, 0, 1), uvs=[P.iron_uv(2, a, b) for a, b in ((0, 0), (1, 0), (1, 1), (0, 1))])
        pits += 1
    ws = P.WASTE                                            # underground-waste station: platform, four inlets with a stripe
    (wx, wy), (wl, ww, wh) = ws["at"], ws["size"]
    wz = max(E.ground_z(wx + P.UPPER_U[0] * k, wy + P.UPPER_U[1] * k) for k in (-wl / 2, 0, wl / 2))
    tbox(g, (wx, wy), P.UPPER_U, wl / 2, ww / 2, wz - 0.5, wz + wh, s0=0.0)
    for k, colour in enumerate(ws["stripes"]):
        a = -wl / 2 + wl * (k + 0.5) / len(ws["stripes"])
        cx, cy = wx + P.UPPER_U[0] * a, wy + P.UPPER_U[1] * a
        tcyl(g, cx, cy, 0.25, wz + wh, wz + wh + 1.1, "galv")
        pbox_t(g, (cx, cy - 0.245), (1.0, 0.0), 0.07, 0.02, wz + wh + 0.25, wz + wh + 0.95, colour)
        tpal(g, [(cx - 0.3, cy - 0.68, wz + wh + 0.006), (cx + 0.3, cy - 0.68, wz + wh + 0.006),
                 (cx + 0.3, cy - 0.32, wz + wh + 0.006), (cx - 0.3, cy - 0.32, wz + wh + 0.006)], "galv", (0, 0, 1))   # steel lid plate
    (ux, uy), (ul, uw, uh) = P.UTILITY_BOX["at"], P.UTILITY_BOX["size"]
    uz = E.ground_z(ux, uy)
    pbox_t(g, (ux, uy), P.UPPER_U, ul / 2, uw / 2, uz - 0.1, uz + uh, "terracotta")
    for x, y in P.RAMP["bollards"]:                         # slim black steel bollards at the top of the stepped ramp
        z = paving_z(x, y)
        tcyl(g, x, y, 0.05, z - 0.1, z + 0.9, "black", n=6)
    hx, hy = P.RAMP["hydrant"]                              # the red hydrant on the grass corner
    hz = E.ground_z(hx, hy)
    tcyl(g, hx, hy, 0.09, hz - 0.1, hz + 0.6, "red", n=8)
    tcyl(g, hx, hy, 0.12, hz + 0.6, hz + 0.72, "red", n=8)
    pbox_t(g, (hx, hy), (1.0, 0.0), 0.2, 0.045, hz + 0.38, hz + 0.47, "red", skip=())
    guard("Coast_Trim", g)
    finish("Coast_Trim", g, [trim_material()])
    print(f"  coast trim (V18h): {blocks} white blocks, {len(P.BENCH_LAMPS)} lamp posts, {posts} timber bollards, {pits} palm pits, "
          f"the waste station, a utility box, {len(P.RAMP['bollards'])} ramp bollards, a hydrant; {tris(g)} triangles")


def pbox_t(g, c, u, hu, hv, z0, z1, colour, skip=("bottom",)):
    """pbox in the trim atlas' flat colours."""
    ux, uy = u
    vx, vy = -uy, ux

    def p(a, b, z):
        return (c[0] + ux * a + vx * b, c[1] + uy * a + vy * b, z)
    for key, (k0, k1, out) in {"+u": ((hu, -hv), (hu, hv), (ux, uy)), "-u": ((-hu, hv), (-hu, -hv), (-ux, -uy)),
                               "+v": ((hu, hv), (-hu, hv), (vx, vy)), "-v": ((-hu, -hv), (hu, -hv), (-vx, -vy))}.items():
        tpal(g, [p(*k0, z0), p(*k1, z0), p(*k1, z1), p(*k0, z1)], colour, (out[0], out[1], 0.0))
    ring = [(-hu, -hv), (hu, -hv), (hu, hv), (-hu, hv)]
    tpal(g, [p(a, b, z1) for a, b in ring], colour, (0, 0, 1))
    if "bottom" not in skip:
        tpal(g, [p(a, b, z0) for a, b in ring], colour, (0, 0, -1))


# =====================================================================================================
def build():
    build_paving()
    build_edges()
    build_trim(_state.get("veg", []))
    rails = build_forecourt()
    build_terrace()
    build_details()
    build_ramp_upstand()
    cove = _state.get("cove_rail")
    if cove is not None:                                  # one railing object for the solid runs
        base = len(rails.verts)
        rails.verts.extend(cove.verts)
        rails.faces.extend(([i + base for i in idx], slot, uvs) for idx, slot, uvs in cove.faces)
    finish("Coast_Rails", rails, B.promenade_materials())
    print("  coast (V16e): clearance to the race centreline (limit "
          f"{P.ROUTE_CLEAR} m): " + ", ".join(f"{k} {v:.2f} m" for k, v in sorted(REPORT.items())))
