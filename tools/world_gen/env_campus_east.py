"""Campus east side (V16b, 2026-11-11), from the photosphere survey V15 (plan data and sources: campus_east_plan.py).

Buildings (env_campus.build_G / build_F / build_E hand over to this module; same objects B_<id> / BD_<id>):
  G  Horizon Hall   Floor-1 terrace slab with a 1.0 m white fascia on round columns 0.45 m every 7.5 m over the open
                    arcade, bar railing; toward the upper plaza a blank warm-white box on a 3.9 m dark glass band,
                    its south face a framed glazed front; glazed block with the Floor-2 balcony; glazed entrance
                    tower from the plaza to the roof; the thin flying roof slab that projects 6 m beyond the south
                    and west sides on ONE slender round column standing on the lower plaza
  F  auditorium     the north-west prow: the west facet folds out to a nose only 3.5 m over the plaza, 5 m north of
                    the roof corner; a lintel with the glazed vestibule 5 m behind it under a warm soffit; the white
                    wing east of it comes down to the ground at the seat tiers
  E  Executive Hall white podium with railed terraces, three sharp white volumes with sloping parapets, the corner
                    box with the one big framed window above the ramp's north-west rim, the framed window of the
                    west end, dark louvre cladding under the terrace parapet
Grounds (build_grounds(), called from env_campus.build_extras):
  World_CampusEast                  six dark terracotta seat tiers on a curved foot line, the grey concrete stair
                                    between white cheeks up to the east lawn, the lawn behind them with a bar fence,
                                    the blank black totem slab, two anthracite lamp columns, the white upstand with
                                    a railing along the rim path above the ramp
  World_CampusEastPaving_NoCollide  the lower plaza's in-situ concrete panels (4.5 m, saw-cut joints) in two skew
                                    grids (ramp axis, stair axis), four gratings in front of F's doors, the 4.2 m
                                    rim path; draped over the built terrain like env_streets' paving, nothing within
                                    4.95 m of the centreline (the road keeps its own surface), no collider
Nothing that stands up is closer than campus_east_plan.PROP_CLEAR (6.6 m) to the race centreline: the build stops
otherwise. Route files, the road and every collider of the track are untouched.
"""
import math

import campus_east_plan as P
import campus_plan as C
import env_campus as K
import env_common as E

F0, F1, F2, F3 = C.F0, C.F1, C.F2, C.F3
WALL, GLASS, ROOF, CREAM, DARK, SAND, CONC, GROUND, SLAT = (K.WALL, K.GLASS, K.ROOF, K.CREAM, K.DARK, K.SAND, K.CONC,
                                                           K.GROUND, K.SLAT)
TIER = K.BRICK                      # slot stand-in: finish_with_tiers() puts the tiers' own material into it
G_UNDER = [(112.7, 5.8), (125.8, 9.6), (131.0, -4.1), (147.2, 5.3), (151.2, 2.35)]    # set-back Floor-0 front
G_GYM = [(151.2, 2.35), (167.6, 11.8), (150.6, 41.0), (145.9, 38.2), (127.8, 27.6), (139.6, 32.6), (149.2, 8.8)]


def clear(name, pts, overhead=None):
    """Notes the least distance of a solid to the race centreline (env_campus.clearance_report) and stops the build
    under PROP_CLEAR."""
    gap = K.note_clear("V16b " + name, pts, overhead)
    if overhead is None and gap < P.PROP_CLEAR:
        raise RuntimeError(f"V16b: '{name}' comes within {gap:.2f} m of the race line (limit {P.PROP_CLEAR} m)")
    return gap


def tier_material():
    return E.material("B_tier_terracotta", P.TIER_COLOUR, roughness=0.9)


def finish_with_tiers(m, name):
    mesh = m.finish(name)
    stand_in = K.slot_materials()[TIER]
    for k, mat in enumerate(mesh.materials):
        if mat == stand_in:
            mesh.materials[k] = tier_material()
    return mesh


def strip(m, a, e, z0, za, ze, thick, mat, top=None):
    """Wall over a -> e (2D), `thick` wide, from z0 up to za at a and ze at e (a sloping top)."""
    nx, ny = K.right_normal(a, e)
    h = thick / 2
    ring = [(a[0] + nx * h, a[1] + ny * h), (a[0] - nx * h, a[1] - ny * h),
            (e[0] - nx * h, e[1] - ny * h), (e[0] + nx * h, e[1] + ny * h)]
    tops = [za, za, ze, ze]
    area = sum(p[0] * q[1] - q[0] * p[1] for p, q in zip(ring, ring[1:] + ring[:1]))
    if area < 0:
        ring, tops = ring[::-1], tops[::-1]
    m.prism(ring, z0, tops, mat, top if top is not None else mat)


def framed_window(vol, det, a, e, z0, z1, frame=0.5, depth=1.0, tile=8.0, rows=3):
    """A window box on the wall a -> e (seen from its right-hand side): a white frame `frame` thick that projects
    `depth` in front of the wall, the glass at the wall's plane."""
    nx, ny = K.right_normal(a, e)
    length = K.dist(a, e)
    ux, uy = (e[0] - a[0]) / length, (e[1] - a[1]) / length

    def q(s, o):
        return (a[0] + ux * s + nx * o, a[1] + uy * s + ny * o)
    for s0, s1, lo, hi in ((0.0, frame, z0, z1), (length - frame, length, z0, z1),
                           (frame, length - frame, z0, z0 + frame), (frame, length - frame, z1 - frame, z1)):
        vol.prism(K.ccw([q(s0, 0.0), q(s1, 0.0), q(s1, depth), q(s0, depth)]), lo, hi, WALL, WALL, WALL)
    glass_rows(det, q(frame, 0.0), q(length - frame, 0.0), z0 + frame, z1 - frame, rows, off=0.05, tile=tile)
    return [q(0.0, depth), q(length, depth)]


def glass_rows(m, a, e, lo, hi, rows, **kw):
    """Glazing in `rows` storeys (one window-band tile high each)."""
    for k in range(rows):
        m.glass(a, e, lo + (hi - lo) * k / rows, lo + (hi - lo) * (k + 1) / rows, **kw)


# ----------------------------------------------------------------------------------------------------------------
# G  Horizon Hall
# ----------------------------------------------------------------------------------------------------------------
def g_rings(points):
    """(deck outline ccw, set-back solid Floor-0 outline, the deck's front edge toward the lower plaza)."""
    ring = K.ccw(points)
    n = len(ring)
    k1 = min(range(n), key=lambda k: K.dist(ring[k], (114.1, 1.4)))
    k5 = min(range(n), key=lambda k: K.dist(ring[k], (167.6, 11.8)))
    lower, k = [], k5 % n
    while k != k1:                                   # from the east corner round the north and west sides
        lower.append(ring[k])
        k = (k + 1) % n
    lower += G_UNDER                                 # then the set-back front instead of the south-west corners
    last = ring[(k1 + 3) % n]
    front = [ring[k1], ring[(k1 + 1) % n], ring[(k1 + 2) % n], last,
             K.lerp2(last, ring[k5], 24.0 / K.dist(last, ring[k5]))]
    return ring, lower, front


def build_G(b):
    vol, det = K.Mesh(), K.Mesh()
    ring, lower, front = g_rings(b["points"])
    ground = C.LOWER_PLAZA_Z
    base = min([E.ground_z(x, y) for x, y in ring] + [ground]) - 1.0
    soffit, fascia_top = P.G_FASCIA
    K.stepped_solid(vol, lower, ring, base, soffit, F1, WALL, SAND, WALL)
    for a, e in zip(G_UNDER, G_UNDER[1:]):
        det.glass(a, e, F0 + 0.15, soffit - 0.1, mat=DARK)
    cen = K.centroid(ring)
    rail, columns = [], []
    for a, e in zip(front, front[1:]):
        # the white fascia continues 0.4 m above the terrace as an upstand, flush with the slab's edge
        nx, ny = K.right_normal(a, e)
        vol.prism(K.ccw([a, e, (e[0] - nx * 0.3, e[1] - ny * 0.3), (a[0] - nx * 0.3, a[1] - ny * 0.3)]), F1, fascia_top,
                  WALL, WALL)
        rail += [(a[0] - nx * 0.15, a[1] - ny * 0.15, fascia_top), (e[0] - nx * 0.15, e[1] - ny * 0.15, fascia_top)]
        n = max(1, int(round(K.dist(a, e) / P.G_COLUMN_STEP)))                 # round columns under the slab edge
        for j in range(n + (1 if e is front[-1] else 0)):
            p = K.lerp2(a, e, j / n)
            d = K.dist(p, cen)
            c = (p[0] + (cen[0] - p[0]) / d * 0.7, p[1] + (cen[1] - p[1]) / d * 0.7)
            if K.route_gap(*c) > P.PROP_CLEAR + 1.0:
                det.column(c, P.G_COLUMN_R, min(E.ground_z(*c), ground) - 0.3, soffit, n=10)
                columns.append(c)
    det.railing(rail)
    # toward the upper plaza: the blank warm-white box on a dark glass band; its south face is a framed glazed front
    box = K.ccw(C.G_CREAM)
    vol.prism(K.grow(box, -0.4), F1, F1 + P.G_BAND, GLASS, None, tile=4.8)
    vol.prism(box, F1 + P.G_BAND, P.G_BOX_TOP, CREAM, ROOF, CREAM)
    south = min(range(len(box)), key=lambda k: box[k][1] + box[(k + 1) % len(box)][1])
    glass_rows(det, box[south], box[(south + 1) % len(box)], F1 + P.G_BAND + 0.7, P.G_BOX_TOP - 0.7, 2, inset=0.7, tile=4.8)
    # glazed block with the Floor-2 balcony band, the glazed entrance tower from the plaza to the roof
    z_roof = P.G_ROOF_Z[0]
    block = K.ccw(C.G_BLOCK)
    vol.prism(block, F1, F2 - 0.3, GLASS, None)
    balcony = K.ccw(C.G_BALCONY)
    vol.prism(balcony, F2 - 0.3, F2 + 0.2, WALL, SAND, WALL)
    mid = (F2 + 0.2 + z_roof) / 2
    vol.prism(block, F2 + 0.2, mid, GLASS, None)
    vol.prism(block, mid, z_roof, GLASS, None)
    inner = K.grow(balcony, -0.15)
    det.railing([(*inner[3], F2 + 0.2), (*inner[0], F2 + 0.2), (*inner[1], F2 + 0.2)])
    for lo, hi in ((ground - 0.3, F1), (F1, F2), (F2, (F2 + z_roof) / 2), ((F2 + z_roof) / 2, z_roof)):
        vol.prism(K.ccw(P.G_TOWER), lo, hi, GLASS, None, tile=4.8)
    # the thin flying roof slab on ONE slender round column standing on the lower plaza
    vol.prism(K.ccw(P.G_ROOF_RING), z_roof, P.G_ROOF_Z[1], WALL, ROOF, WALL)
    det.railing([(*P.G(-6.2, 7.2), P.G_ROOF_Z[1]), (*P.G(-6.2, -5.7), P.G_ROOF_Z[1]), (*P.G(19.2, -5.7), P.G_ROOF_Z[1]),
                 (*P.G(19.2, 27.3), P.G_ROOF_Z[1])])
    det.column(P.G_TALL_COLUMN, P.G_TALL_COLUMN_R, ground - 0.3, z_roof, n=10)
    # the gym's roof east of the block
    gym = K.ccw(G_GYM)
    vol.prism(gym, soffit, F2, WALL, ROOF)
    for a, e in zip(gym, gym[1:] + gym[:1]):
        if K.dist(a, e) > 8.0 and not C.inside(*K.lerp2(a, e, 0.5), K.grow(K.ccw(C.G_ROOF), 0.5)):
            det.glass(a, e, F1 + 0.9, F1 + 2.5, inset=0.6)
    clear("G tall column", [P.G_TALL_COLUMN])
    clear("G arcade columns and flying roof", columns + P.G_ROOF_RING)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


# ----------------------------------------------------------------------------------------------------------------
# F  Grand Auditorium: faceted white monolith with the prow over the lower plaza
# ----------------------------------------------------------------------------------------------------------------
F_A, F_B, F_CW = (122.3, -22.0, 26.0), (130.5, -20.2, 25.0), (122.6, -32.7, 22.8)     # roof corners at the prow
F_IE = (131.8, -28.0, 23.0)


def f_prow():
    """Points of the prow: nose and lintel end (3D), wing foot, vestibule front, ground contact (2D)."""
    zl = F0 + P.F_NOSE_H
    ux, uy = P._unit(P.F_NOSE, P.F_LINTEL_END)
    back = (uy, -ux) if -ux < 0 else (-uy, ux)                    # away from the plaza (south)
    va = (P.F_NOSE[0] + back[0] * P.F_SETBACK, P.F_NOSE[1] + back[1] * P.F_SETBACK)
    vb = (P.F_LINTEL_END[0] + back[0] * P.F_SETBACK, P.F_LINTEL_END[1] + back[1] * P.F_SETBACK)
    return (*P.F_NOSE, zl), (*P.F_LINTEL_END, zl), va, vb


def f_foot_ring():
    """What stands on the ground under the prow (for the paving's cut-out)."""
    _n, _n2, va, vb = f_prow()
    return [F_CW[:2], va, vb, P.F_LINTEL_END, P.F_WING_FOOT, F_IE[:2]]


def build_F(b):
    vol, det = K.Mesh(), K.Mesh()
    A, B_, Cw, Ie = F_A, F_B, F_CW, F_IE
    Dw, Ew, Fw = (118.7, -50.2, 19.5), (109.0, -66.2, 16.5), (114.2, -70.7, 16.5)
    F2w, Gw, Gs = (113.0, -75.8, 16.0), (112.6, -77.5, 15.5), (143.3, -84.8, 15.5)
    Je, He, H2 = (147.3, -70.3, 16.5), (156.3, -37.2, 17.5), (153.6, -36.2, 17.8)
    Sw, Se = (120.8, -70.3, 19.0), (139.6, -71.8, 19.0)
    outline = [A, Cw, Dw, Ew, Fw, F2w, Gw, Gs, Je, He, H2, Ie, B_]            # ccw
    base = min(E.ground_z(p[0], p[1]) for p in outline) - 1.0
    for a, e in zip(outline, outline[1:] + outline[:1]):
        if (a, e) in ((A, Cw), (B_, A), (Ie, B_)):           # the prow replaces these walls
            continue
        vol.face([(a[0], a[1], base), (e[0], e[1], base), e, a], WALL)
    for tri in ((A, Sw, Se), (A, Se, B_),                                      # the long central facet (ridge)
                (A, Cw, Sw), (Cw, Dw, Sw), (Dw, Ew, Sw), (Ew, Fw, Sw),         # west facet
                (B_, Se, Ie), (Ie, Se, H2), (H2, Se, He), (He, Se, Je),        # east facet
                (Sw, Fw, F2w), (Sw, F2w, Gw), (Sw, Gw, Gs), (Sw, Gs, Se), (Se, Gs, Je)):   # south facet
        vol.up(list(tri), WALL)
    # --- the prow (survey F 2.4): ground contact Cw, peak A, nose N; lintel N-N2; wing down to the tiers ---
    N, N2, va, vb = f_prow()
    g = C.LOWER_PLAZA_Z - 0.3                                # foot of everything at the prow (under the paving)
    zs, zg = F0 + 3.3, F0 + P.F_GLASS_H                      # soffit at the vestibule, head of its glass
    Cg = (Cw[0], Cw[1], E.ground_z(Cw[0], Cw[1]) - 0.1)      # ground contact: here the facet's lower edge starts
    W, Ig = (*P.F_WING_FOOT, g), (Ie[0], Ie[1], g)
    west, north, east = (95.0, -25.0, 14.0), (127.0, 5.0, 16.0), (150.0, -32.0, 14.0)
    vol.towards([Cg, A, Cw], WALL, west)                     # the west wall's last triangle, up to the peak
    vol.towards([Cg, N, A], WALL, (100.0, -12.0, 9.0))       # the facet that folds out over the plaza
    vol.towards([N, N2, B_], WALL, north)                    # the big facet over the entrance
    vol.towards([N, B_, A], WALL, north)
    vol.towards([N2, W, B_], WALL, (142.0, -8.0, 18.0))      # the wing
    vol.towards([W, Ie, B_], WALL, (142.0, -8.0, 18.0))
    vol.towards([W, Ig, Ie], WALL, east)
    vol.towards([(N2[0], N2[1], g), W, N2], WALL, north)     # under the wing's lower edge
    vol.down([Cg, N, (*va, zs)], CREAM)                      # warm soffit rising from the ground contact to the lintel
    vol.down([(*va, zs), N, N2, (*vb, zs)], CREAM)
    vol.towards([(Cg[0], Cg[1], g), (*va, g), (*va, zs), Cg], DARK, west)     # the vestibule's glazed west return
    vol.towards([(*va, g), (*vb, g), (*vb, zs), (*va, zs)], CREAM, north)      # vestibule front, 5 m behind the lintel
    ga, gb = K.facing(va, vb, north)
    vol.glass(ga, gb, F0 + 0.05, zg, off=0.06, mat=DARK, bay=2.2, pier=0.14)   # ... its glazing in slender frames
    vol.towards([(*vb, g), (N2[0], N2[1], g), N2, (*vb, zs)], CREAM, (120.0, -21.0, 12.0))
    for k in range(5):                                                         # skylights on the south facet
        c = K.lerp2((123.5, -73.6), (137.5, -75.0), k / 4)
        det.box(c, (0.995, -0.1), 1.1, 0.6, 17.6, 18.55, WALL, K.GLASS_ROOF)
    K.berm(det, [Cw[:2], Dw[:2], Ew[:2]], (100.0, -45.0), P.F_BERM[0], width=P.F_BERM[1])   # lawn against the west wall
    clear("F prow (nose, lintel, wing foot)", [P.F_NOSE, P.F_LINTEL_END, P.F_WING_FOOT, Cw[:2], va, vb])
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


def f_berm_height(x, y):
    """Height of F's west lawn bank over the ground at (x, y) (as env_campus.berm builds it)."""
    line = [F_CW[:2], (118.7, -50.2), (109.0, -66.2)]
    d = min(C.seg_distance(x, y, a[0], a[1], e[0], e[1]) for a, e in zip(line, line[1:]))
    return P.F_BERM[0] * max(0.0, 1.0 - d / P.F_BERM[1])


# ----------------------------------------------------------------------------------------------------------------
# E  Executive Hall
# ----------------------------------------------------------------------------------------------------------------
def build_E(b):
    vol, det = K.Mesh(), K.Mesh()
    ring = K.ccw(P.E_RING)
    base = min(E.ground_z(x, y) for x, y in ring) - 1.0
    vol.prism(ring, base, P.E_PODIUM_TOP, WALL, ROOF)
    for a, e in zip(ring, ring[1:] + ring[:1]):                 # ground-floor glazing, not on the blank south sides
        if K.dist(a, e) > 6.0 and (a, e) not in (P.E_SE_FACE, P.E_WEST_FACE) and K.right_normal(a, e)[1] > -0.3:
            det.glass(a, e, F0 + 0.7, F0 + 2.5, inset=0.6)
    for k, (upper, tops) in enumerate(P.E_VOLUMES):
        area = sum(p[0] * q[1] - q[0] * p[1] for p, q in zip(upper, upper[1:] + upper[:1]))
        if area < 0:
            upper, tops = upper[::-1], tops[::-1]
        n = len(upper)
        roof = min(tops) - 0.35
        vol.prism(upper, P.E_PODIUM_TOP, tops, WALL, None)
        vol.face([(*p, roof) for p in upper], ROOF)
        for i in range(n):                                   # inner faces of the sloping parapet
            j = (i + 1) % n
            vol.face([(*upper[j], roof), (*upper[i], roof), (*upper[i], tops[i]), (*upper[j], tops[j])], WALL)
        if k < 2:
            for a, e in zip(upper, upper[1:] + upper[:1]):
                if K.dist(a, e) > 5.0:
                    det.glass(a, e, F1 + 0.9, F1 + 2.9, inset=0.6)
    # the corner box with the one big framed window, facing south-east over the ramp
    a, e = P.E_BOX_A, P.E_BOX_B
    nx, ny = K.right_normal(a, e)
    body = K.ccw([a, e, (e[0] - nx * P.E_BOX_DEPTH, e[1] - ny * P.E_BOX_DEPTH),
                  (a[0] - nx * P.E_BOX_DEPTH, a[1] - ny * P.E_BOX_DEPTH)])
    vol.prism(body, P.E_PODIUM_TOP, P.E_BOX_Z[1], WALL, ROOF)
    tips = framed_window(vol, det, a, e, P.E_BOX_Z[0], P.E_BOX_Z[1], frame=P.E_WINDOW_FRAME, depth=P.E_BOX_OUT)
    # the framed window of the west end
    for wa, we, sill, width, height in P.E_WINDOWS:
        length = K.dist(wa, we)
        p, q = K.lerp2(wa, we, 0.5 - width / 2 / length), K.lerp2(wa, we, 0.5 + width / 2 / length)
        framed_window(vol, det, p, q, sill, sill + height, frame=0.45, depth=0.8, tile=7.0, rows=2)
    # dark louvre cladding (plant rooms) under the terrace parapet
    la, le = P.E_LOUVRE_A, P.E_LOUVRE_B
    z0, z1 = P.E_LOUVRE_Z
    det.wall(la, le, z0, z1, SLAT, off=0.06)
    lx, ly = K.right_normal(la, le)
    fins = 8
    for k in range(fins):
        z = z0 + 0.25 + k * (z1 - z0 - 0.4) / fins
        fin = K.ccw([(la[0] + lx * 0.06, la[1] + ly * 0.06), (le[0] + lx * 0.06, le[1] + ly * 0.06),
                     (le[0] + lx * 0.2, le[1] + ly * 0.2), (la[0] + lx * 0.2, la[1] + ly * 0.2)])
        det.prism(fin, z, z + 0.14, SLAT, SLAT, SLAT)
    det.railing([(*p, P.E_PODIUM_TOP) for p in P.E_TERRACE_RAIL[1:]])
    det.railing([(*p, P.E_PODIUM_TOP) for p in P.E_TERRACE_RAIL_2])
    clear("E (outline, window box)", list(ring) + tips)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


# ----------------------------------------------------------------------------------------------------------------
# Grounds: seat tiers, grey stair, lawn and fence, totem, lamp columns, rim wall
# ----------------------------------------------------------------------------------------------------------------
def ground(x, y):
    import env_streets
    return env_streets.ground(x, y)


def build_tiers(m):
    ys, n = P.TIER_Y, len(P.TIER_Y)
    z0 = C.LOWER_PLAZA_Z - 0.4
    for k in range(P.TIERS):
        front = [(P.tier_x(k, i), ys[i]) for i in range(n)]
        back = [(P.tier_x(k + 1, i), ys[i]) for i in range(n)]
        top = F0 + (k + 1) * P.TIER_H
        low = z0 if k == 0 else top - P.TIER_H - 0.05
        for i in range(n - 1):
            m.wall(front[i + 1], front[i], low, top, TIER)                      # riser, facing the plaza
            m.up([(*front[i], top), (*back[i], top), (*back[i + 1], top), (*front[i + 1], top)], TIER)
    # white sloping cheek at the south end (the stair's cheek closes the north end)
    a, e = (P.TIER_X[0] - 0.3, ys[0] - P.CHEEK / 2), (P.tier_x(P.TIERS, 0) + 0.3, ys[0] - P.CHEEK / 2)
    strip(m, a, e, z0, F0 + 0.7, P.TIER_TOP + P.CHEEK_H, P.CHEEK, WALL)
    clear("seat tiers", [(P.TIER_X[i], ys[i]) for i in range(n)])


def build_stair(m):
    x0, (y0, y1) = P.STAIR_X0, P.STAIR_Y
    z0 = C.LOWER_PLAZA_Z - 0.4
    for j in range(P.STAIR_RISERS):
        xa, za = x0 + j * P.STAIR_TREAD, F0 + j * P.STAIR_RISE
        m.wall((xa, y1), (xa, y0), za - (0.4 if j == 0 else 0.0), za + P.STAIR_RISE, CONC)
        xb = xa + P.STAIR_TREAD if j < P.STAIR_RISERS - 1 else P.LAWN_X - 0.6     # the last tread is the top landing
        m.up([(xa, y0, za + P.STAIR_RISE), (xb, y0, za + P.STAIR_RISE), (xb, y1, za + P.STAIR_RISE),
              (xa, y1, za + P.STAIR_RISE)], CONC)
    x1 = x0 + P.STAIR_RISERS * P.STAIR_TREAD
    top = F0 + P.STAIR_RISERS * P.STAIR_RISE
    for y, xs in ((y0 - P.CHEEK / 2, P.TIER_X[-1] - 0.2), (y1 + P.CHEEK / 2, x0 - 0.2)):
        strip(m, (xs, y), (x1, y), z0, F0 + P.CHEEK_H, top + P.CHEEK_H, P.CHEEK, WALL)
        strip(m, (x1, y), (P.LAWN_X - 0.6, y), z0, top + P.CHEEK_H, top + P.CHEEK_H, P.CHEEK, WALL)
    clear("grey stair", [(x0 - 0.2, y0), (x0 - 0.2, y1)])
    return top


def build_lawn(m):
    """The lawn behind the tiers: level with their top, then into the terrain; a slope south of the south cheek."""
    ys, n = P.TIER_Y, len(P.TIER_Y)
    xe = P.LAWN_X + 1.2
    rows = []
    for i in range(n):
        xb = P.tier_x(P.TIERS, i)
        mid = (xb + 1.2, ys[i], max(P.TIER_TOP, ground(xb + 1.2, ys[i]) + 0.10))
        rows.append(((xb, ys[i], P.TIER_TOP), mid, (xe, ys[i], ground(xe, ys[i]) - 0.08)))
    for r0, r1 in zip(rows, rows[1:]):
        m.up([r0[0], r0[1], r1[1], r1[0]], GROUND)
        m.up([r0[1], r0[2], r1[2], r1[1]], GROUND)
    ys0 = ys[0] - P.CHEEK
    s = [(rows[0][0][0], ys0, P.TIER_TOP), (rows[0][1][0], ys0, rows[0][1][2]), (xe, ys0, rows[0][2][2])]
    m.up([rows[0][0], s[0], s[1], rows[0][1]], GROUND)
    m.up([rows[0][1], s[1], s[2], rows[0][2]], GROUND)
    yf = ys0 - 4.6
    foot = [(s[0][0] - 2.5, yf, ground(s[0][0] - 2.5, yf) - 0.08), (xe, yf, ground(xe, yf) - 0.08)]
    m.up([s[0], foot[0], foot[1]], GROUND)
    m.up([s[0], foot[1], s[2]], GROUND)
    m.towards([s[0], s[2], s[1]], GROUND, (150.0, -40.0, 14.0))
    m.railing([(x, y, ground(x, y)) for x, y in P.FENCE], module=2.0)
    clear("fence on the east bank", P.FENCE)


def off_the_line(x, y, corners, step=0.1):
    """(x, y) moved straight away from the race line until every corner (offsets) keeps PROP_CLEAR + 0.05."""
    for _ in range(40):
        if min(K.route_gap(x + dx, y + dy) for dx, dy in corners) >= P.PROP_CLEAR + 0.05:
            break
        p = E.nearest_route_point(x, y)
        d = math.hypot(x - p["x"], y - p["y"]) or 1.0
        x, y = x + (x - p["x"]) / d * step, y + (y - p["y"]) / d * step
    return x, y


def build_objects(m):
    # totem: blank black slab, broad faces to the north-east and south-west
    w, t, h = P.TOTEM_SIZE
    u = (math.sqrt(0.5), -math.sqrt(0.5))
    v = (-u[1], u[0])
    corners = [(u[0] * a + v[0] * c, u[1] * a + v[1] * c) for a in (-w / 2, w / 2) for c in (-t / 2, t / 2)]
    x, y = off_the_line(*P.TOTEM, corners)
    g = ground(x, y)
    m.box((x, y), u, w / 2, t / 2, g - 0.3, g + h, DARK, DARK)
    clear("totem slab", [(x + dx, y + dy) for dx, dy in corners])
    K.keep_out([(x - 1.6, y - 1.6), (x + 1.6, y - 1.6), (x + 1.6, y + 1.6), (x - 1.6, y + 1.6)])
    print(f"  campus east: totem at ({x:.2f}, {y:.2f}) (survey {P.TOTEM})")
    # lamp columns: slim anthracite square columns, a pale light slot in the top metre
    s, h = P.LAMP_SIZE
    for (x, y) in P.LAMPS:
        x, y = off_the_line(x, y, [(-s / 2, -s / 2), (s / 2, -s / 2), (s / 2, s / 2), (-s / 2, s / 2)])
        g = ground(x, y)
        top = g + f_berm_height(x, y) + h
        m.box((x, y), (1.0, 0.0), s / 2, s / 2, g - 0.3, top - 1.0, SLAT, SLAT)
        m.box((x, y), (1.0, 0.0), s / 2 - 0.02, s / 2 - 0.02, top - 1.0, top - 0.12, WALL, WALL)
        m.box((x, y), (1.0, 0.0), s / 2, s / 2, top - 0.12, top, SLAT, SLAT, SLAT)
        clear("lamp column", [(x, y)])
        K.keep_out([(x - 1.0, y - 1.0), (x + 1.0, y - 1.0), (x + 1.0, y + 1.0), (x - 1.0, y + 1.0)])


def build_rim_wall(m):
    """White upstand with a bar railing on the far side of the rim path, along E."""
    line = P.rim_line(P.RIM_WALL[0], P.RIM_WALL[1], P.RIM_PATH[1] + P.RIM_WALL_T / 2 + 0.05)[::2]
    zs = [ground(x, y) for x, y in line]
    for (a, za), (e, ze) in zip(zip(line, zs), list(zip(line, zs))[1:]):
        strip(m, a, e, min(za, ze) - 0.4, za + P.RIM_WALL_H, ze + P.RIM_WALL_H, P.RIM_WALL_T, WALL)
    m.railing([(x, y, z + P.RIM_WALL_H) for (x, y), z in zip(line, zs)], h=1.0)
    clear("rim wall above the ramp", line)


# ----------------------------------------------------------------------------------------------------------------
# Paving: the lower plaza's concrete panels in two skew grids, gratings, the rim path
# ----------------------------------------------------------------------------------------------------------------
def _g_solid():
    for b in E.DATA["buildings"]:
        if b["id"] == C.G_ID:
            return g_rings([tuple(p) for p in b["points"]])[1]
    return None


def _g_deck():
    for b in E.DATA["buildings"]:
        if b["id"] == C.G_ID:
            return g_rings([tuple(p) for p in b["points"]])[0]
    return None


def _f_ring():
    for b in E.DATA["buildings"]:
        if b["id"] == C.F_ID:
            return [tuple(p) for p in b["points"]]
    return None


def _grating_ring(c):
    ux, uy = P._unit(P.GRATINGS[0], P.GRATINGS[-1])
    hl, hw = P.GRATING[0] / 2, P.GRATING[1] / 2
    return [(c[0] + ux * a - uy * w, c[1] + uy * a + ux * w) for a, w in ((-hl, -hw), (hl, -hw), (hl, hw), (-hl, hw))]


def build_paving(col):
    import env_entrance
    import env_streets as ST
    import plaza_plan
    import street_plan as S
    rim = P.rim_path_ring()
    north = P.GRID_SPLIT + [(P.GRID_SPLIT[-1][0], 25.0), (P.GRID_SPLIT[0][0], 25.0)]
    gratings = [_grating_ring(c) for c in P.GRATINGS]
    cutters = [r for r in (list(C.MAIN_ROOF), _g_solid(), list(P.G_TOWER), f_foot_ring(), _f_ring(), P.PLAZA_PIT) if r]
    slopes = P.ramp_slope_rings()                   # the plaza's paving ends at the cutting's rims
    rings = [P.PLAZA_RING, rim, north] + gratings + cutters + slopes
    soup = ST.overlay([{"name": str(k), "ring": r, "mat": "concrete", "uv": None} for k, r in enumerate(rings)])
    grids = [{"mat": "concrete", "uv": ((104.5, -37.0), P.RAMP_DIR, P.PANEL, P.PANEL)},
             {"mat": "concrete", "uv": (plaza_plan.STAIR_FOOT, P.STAIR_DIR, P.PANEL, P.PANEL)}]
    tris = []
    for a, b, c, _tag in soup.tris:
        x = (soup.xy[a][0] + soup.xy[b][0] + soup.xy[c][0]) / 3
        y = (soup.xy[a][1] + soup.xy[b][1] + soup.xy[c][1]) / 3
        on_rim = C.inside(x, y, rim)
        if not (C.inside(x, y, P.PLAZA_RING) or on_rim) or any(C.inside(x, y, r) for r in cutters):
            continue
        if not on_rim and any(C.inside(x, y, r) for r in slopes):
            continue
        if any(C.inside(x, y, r) for r in gratings):
            tris.append((a, b, c, 2))
        else:
            tris.append((a, b, c, 1 if C.inside(x, y, north) else 0))
    soup.tris = tris
    counts = [len(soup.tris)]
    soup.refine_near_route(3.0)
    soup.clip_route()
    counts.append(len(soup.tris))

    level = [r for r in (_g_deck(), P.PLAZA_LEVEL) if r]

    def zf(x, y):
        d = S.route_distance(x, y)
        z = ST._paving_z(d, x, y)
        if any(C.inside(x, y, r) for r in level):   # beside env_plaza's Floor-0 paving the terrain cells are sunk by
            z = max(z, C.LOWER_PLAZA_Z + ST.lift_at(d))     # 0.3 m: the floor under and in front of G is one plane
        return z
    soup.limit_edges(5.0)
    soup.follow_ground(zf, up=0.012, down=0.05)
    counts.append(len(soup.tris))
    worst, where, low = 1.0, None, []               # proof: how close does the paving come to the ground anywhere?
    m = env_entrance.Faces()
    z, area = {}, 0.0
    for a, b, c, tag in soup.tris:
        pts = []
        for v in (a, b, c):
            if v not in z:
                z[v] = zf(*soup.xy[v])
            pts.append((soup.xy[v][0], soup.xy[v][1], z[v]))
        for wa, wb, wc in ((1 / 3, 1 / 3, 1 / 3), (0.6, 0.2, 0.2), (0.2, 0.6, 0.2), (0.2, 0.2, 0.6)):
            x = wa * pts[0][0] + wb * pts[1][0] + wc * pts[2][0]
            y = wa * pts[0][1] + wb * pts[1][1] + wc * pts[2][1]
            gap = wa * pts[0][2] + wb * pts[1][2] + wc * pts[2][2] - ST.ground(x, y)
            if gap < -0.02:
                low.append((round(gap * 100), round(x, 1), round(y, 1)))
            if gap < worst:
                worst, where = gap, (round(x, 1), round(y, 1))
        area += abs((pts[1][0] - pts[0][0]) * (pts[2][1] - pts[0][1]) - (pts[2][0] - pts[0][0]) * (pts[1][1] - pts[0][1])) / 2
        if tag == 2:
            m.face(pts, "grate", (0, 0, 1), [(0.5, 0.5)] * 3)
        else:
            m.face(pts, "concrete", (0, 0, 1), [ST._layer_uv(grids[tag], p) for p in pts])
    concrete = ST._textured("campus_east_concrete", P.PLAZA_CONCRETE, "campus_east_concrete", 0.9)
    obj = m.build("World_CampusEastPaving_NoCollide", col, {"concrete": concrete, "grate": K.slot_materials()[SLAT]})
    env_entrance._weld(obj.data)
    d_min = min(soup.dist(v) for tr in soup.tris for v in tr[:3])
    print(f"  campus east: paving {len(obj.data.polygons)} triangles, {area:.0f} m2 (lower plaza, rim path, "
          f"{len(gratings)} gratings); overlay / clip at the road / follow the ground: {counts}; nearest point "
          f"{d_min:.2f} m from the centreline; least clearance over the ground {worst * 100:.1f} cm at {where}; "
          f"{len(low)} samples more than 2 cm under the ground {sorted(low)[:12]}")


def _rect(c, bearing, length, width):
    """(unit vector along the long axis, ring) of a rectangle given like campus_east_plan.COMPOUND."""
    u = (math.sin(math.radians(bearing)), math.cos(math.radians(bearing)))
    v = (-u[1], u[0])
    return u, [(c[0] + u[0] * a + v[0] * b, c[1] + u[1] * a + v[1] * b)
               for a, b in ((-length / 2, -width / 2), (length / 2, -width / 2), (length / 2, width / 2), (-length / 2, width / 2))]


def build_court(m):
    """V18g: the court east of G (orthophoto, Google 20z): the walled plant compound (open to the sky, dark inside,
    two plant boxes), the dark shed beside it, the one tall lamp mast."""
    c, bearing, length, width, h = P.COMPOUND
    u, ring = _rect(c, bearing, length, width)
    g = min(ground(x, y) for x, y in ring)
    top = max(ground(x, y) for x, y in ring) + h
    for a, e in zip(ring, ring[1:] + ring[:1]):
        strip(m, a, e, g - 0.4, top, top, 0.25, CONC)
    floor = max(ground(x, y) for x, y in ring) + 0.06
    m.up([(x, y, floor) for x, y in ring], DARK)
    v = (-u[1], u[0])
    for along, across, hu, hv, hb in ((-2.6, 0.6, 2.2, 1.5, 1.9), (2.9, -0.5, 1.6, 2.0, 1.6)):
        m.box((c[0] + u[0] * along + v[0] * across, c[1] + u[1] * along + v[1] * across), u, hu, hv, floor, floor + hb, SLAT, SLAT)
    clear("plant compound in the court", ring)
    K.keep_out(ring, 1.0)
    c, bearing, length, width, h = P.SHED
    u, ring = _rect(c, bearing, length, width)
    g = min(ground(x, y) for x, y in ring)
    m.box(c, u, length / 2, width / 2, g - 0.3, max(ground(x, y) for x, y in ring) + h, SLAT, DARK)
    clear("shed in the court", ring)
    K.keep_out(ring, 1.0)
    x, y = P.COURT_MAST
    g = ground(x, y)
    m.box((x, y), (1.0, 0.0), 0.09, 0.09, g - 0.3, g + P.COURT_MAST_H, SLAT, SLAT)
    m.box((x, y), (1.0, 0.0), 0.45, 0.16, g + P.COURT_MAST_H, g + P.COURT_MAST_H + 0.12, SLAT, SLAT, WALL)
    clear("lamp mast in the court", [(x, y)])
    K.keep_out([(x - 1.0, y - 1.0), (x + 1.0, y - 1.0), (x + 1.0, y + 1.0), (x - 1.0, y + 1.0)])


def dress(veg, props):
    """V18g: the east grounds' planting and the lower plaza's furniture as on Google Maps (an explicit edit of the
    lists, after dressing_reality.apply; rewrites the dressing files). Only items inside this module's own rings
    change: the court east of G (young trees in single pits instead of the two generated olive rows), the big lawn
    (no shrub groups: it is a plain lawn), the scrub beyond the east path (scattered shrubs, the pine clump
    south-east of F), G's arcade (timber picnic tables instead of the two parasol sets)."""
    import json
    import os
    import random
    import plaza_plan
    rng = random.Random(20261112)
    inside = plaza_plan.inside
    lawn = next(lay["ring"] for lay in C.GROUND_PAINT if lay["name"] == "east lawn")
    veg2, gone = [], {}
    for p in veg:
        x, y = p["x"], p["y"]
        if (inside(x, y, P.COURT_RING) and p["proto"].startswith("Veg_Olive")) or \
                (inside(x, y, lawn) and y < 132.0 and p["proto"].startswith("Veg_Shrub")):
            gone[p["proto"]] = gone.get(p["proto"], 0) + 1
            continue
        veg2.append(p)

    strip_ring = next(lay["ring"] for lay in C.GROUND_PAINT if lay["name"] == "sand strip with the two tree rows")
    moved = 0
    for k, p in enumerate(veg2):                            # the strip's two rows: to their real lines beside the lawn
        if p["proto"].startswith("Veg_PlazaTree") and p["y"] > 58.0 and inside(p["x"], p["y"], strip_ring):
            x, y = p["x"] + P.TREE_ROW_SHIFT[0], p["y"] + P.TREE_ROW_SHIFT[1]
            z = plaza_plan.DECK_Z if plaza_plan.on_deck(x, y) else ground(x, y)
            veg2[k] = dict(p, x=round(x, 3), y=round(y, 3), z=round(z, 3))
            moved += 1

    def plant(proto, x, y, scale):
        veg2.append({"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(ground(x, y), 3),
                     "rot_z": round(rng.uniform(0, 2 * math.pi), 4), "scale": round(scale, 3)})
    for k, (x, y) in enumerate(P.COURT_TREES):
        plant("Veg_PlazaTree" if k % 2 else "Veg_PlazaTree_B", x, y, rng.uniform(0.85, 1.05))
    for k, (x, y) in enumerate(P.SCRUB_SHRUBS):
        plant("Veg_Shrub" if k % 3 else "Veg_Shrub_B", x, y, rng.uniform(1.25, 1.75))
    for k, (x, y) in enumerate(P.PINES):
        plant("Veg_UmbrellaPine" if k % 2 else "Veg_UmbrellaPine_B", x, y, rng.uniform(0.9, 1.1))
    # G's arcade: the two parasol sets go, timber picnic tables stand at the slab's edge (Google 20z, walk 7 Nov)
    sets = [(p["x"], p["y"]) for p in props if p["proto"] == "Prop_Parasol" and inside(p["x"], p["y"], C.LOWER_PLAZA)]
    props2 = [p for p in props if not any(math.hypot(p["x"] - x, p["y"] - y) < 1.0 for x, y in sets)]
    for x, y, rot in P.ARCADE_TABLES:
        if K.route_gap(x, y) < P.PROP_CLEAR + 1.0:
            raise RuntimeError(f"V18g: picnic table at ({x}, {y}) is too close to the race line")
        props2.append({"proto": "Prop_PicnicTable", "x": x, "y": y, "z": round(max(ground(x, y) + 0.04, F0 + 0.03), 3), "rot_z": rot,
                       "scale": 1.0})       # (the paving / the void's Floor-0 paving lies at 11.22 .. 11.26 here)
    print(f"  campus east: dressing: removed {gone} and {len(props) - len(props2)} props of {len(sets)} parasol sets; "
          f"{len(P.COURT_TREES)} court trees, {len(P.SCRUB_SHRUBS)} scrub shrubs, {len(P.PINES)} pines, "
          f"{len(P.ARCADE_TABLES)} picnic tables at G's arcade; {moved} trees of the strip's rows moved by {P.TREE_ROW_SHIFT}")
    for fname, items in (("placements_vegetation.json", veg2), ("placements_props.json", props2)):
        path = os.path.join(E.OUT, fname)
        data = json.load(open(path))
        data["placements"] = items
        counts = {}
        for it in items:
            counts[it["proto"]] = counts.get(it["proto"], 0) + 1
        data["counts"] = counts
        json.dump(data, open(path, "w"), indent=1)
    return veg2, props2


def build_grounds():
    """Called from env_campus.build_extras (before the dressing: the keep-out rings are read by env_campus.dress)."""
    col = E.collection("World_Buildings_Campus")
    m = K.Mesh()
    build_tiers(m)
    build_stair(m)
    build_lawn(m)
    build_objects(m)
    build_rim_wall(m)
    build_court(m)
    K.keep_out(P.EAST_RING)
    mesh = finish_with_tiers(m, "World_CampusEast")
    E.new_object("World_CampusEast", mesh, col)
    print(f"  campus east: grounds {sum(len(p.vertices) - 2 for p in mesh.polygons)} triangles "
          f"(tiers, stair, lawn, fence, totem, lamp columns, rim wall)")
    build_paving(col)
