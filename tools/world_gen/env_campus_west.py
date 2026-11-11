"""Campus west (V16c, 2026-11-11): what the player sees between the hall's west door and the avenue, closer to the
real place. Plan data and sources: campus_west_plan.py (photosphere survey V15: sections 3.2, 3.3, Appendices B, C).

  CTT Student Hall    owned here now (it was the generic ribbon box): the same closed volume and the same roof = deck
                      at Floor 1 (the driving surface: not moved), a blank white west end instead of the glazed box,
                      the deck almost white with hairline saw cuts along its axis, bronze-tan flat-bar railings at
                      the deck's long edges instead of the white rim
  terrace west end    the real ramp block (rough concrete ramp, flat crest, inclined white west face, tan railings)
                      south of the jump corridor; the long open stair with white cheeks, tan handrails and the brown
                      louvred door in its south cheek north of it; pad and apron as ground paint
  B                   perforated terracotta brick screen with a white coping along the terrace side, ribbon with
                      three white bars, framed glass box on the head's south-west face
  D                   two glazed storeys behind white piers toward the terrace; south side: upper storey projecting
                      over a glazed ground floor behind slim columns; head: solid white wedge with a deep frame box
                      (set-back glazing, glass balustrade), dark inclined deck with stone courses into the water,
                      prow with slot and balcony
  D's pool            five-sided basin with a 4 m arm along D's south facade, flush pale kerb, olive-grey water
  lawns               grass berm in front of hall E (terrain, raise_nodes), stone path with its low wall, stepping
                      slabs, sapling rows in the two courtyards, the three real lawn trees

Nothing solid of this module stands closer than campus_west_plan.CLEAR (6.6 m) to the race centreline (clear() stops
the build otherwise); the jump corridor is given in the jump frame and stays empty. The route, the deck's surface,
the kicker and the landing road are not touched.
"""
import math

from mathutils import Vector
from mathutils.geometry import tessellate_polygon

import campus_plan as C
import campus_west_plan as P
import env_common as E
import env_campus as K            # imported from env_campus itself (after its OWNED table): helpers, Mesh, slots
import env_materials

F0, F1 = C.F0, C.F1
C_ID = "w496301638"               # CTT Student Hall

# extra material slots of this module (on top of env_campus' list; resolved in WMesh.finish)
DECK, RAIL_TAN, SCREEN, OLIVE = 100, 101, 102, 103
K.TILE_M[SCREEN] = 1.0
env_materials.TEXTURED["W_B_deck_white"] = ("deck_sawcut", 0.9)
env_materials.TEXTURED["W_B_brick_screen"] = ("brick_screen", 0.92)
env_materials.MATTE.add("W_B_deck_white")
DECK_TILE = 6.0                   # m per tile of deck_sawcut (make_west_textures.py)


def extra_materials():
    return {DECK: E.material("B_deck_white", "#E9E7D4", 0.9),
            RAIL_TAN: K.railing_material("tan"),
            SCREEN: E.material("B_brick_screen", "#755B41", 0.92),
            OLIVE: E.material("B_pool_water_olive", "#7A7862", roughness=0.06, metallic=0.25)}


class _Slots:
    def __init__(self, base, extra):
        self.base, self.extra = base, extra

    def __getitem__(self, k):
        return self.extra[k] if k >= 100 else self.base[k]


class WMesh(K.Mesh):
    """env_campus.Mesh with this module's extra material slots and the tan 2 m railing."""

    def railing(self, line, h=1.1, module=2.0, mat=RAIL_TAN):
        for a, b in zip(line, line[1:]):
            length = math.dist(a, b)
            if length < 0.4:
                continue
            n = max(1, int(round(length / module)))
            for k in range(n):
                p = tuple(a[i] + (b[i] - a[i]) * k / n for i in range(3))
                q = tuple(a[i] + (b[i] - a[i]) * (k + 1) / n for i in range(3))
                self.face([p, q, (q[0], q[1], q[2] + h), (p[0], p[1], p[2] + h)], mat,
                          uvs=[(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)])

    def finish(self, name):
        base = K.slot_materials
        K.slot_materials = lambda: _Slots(base(), extra_materials())
        try:
            return super().finish(name)
        finally:
            K.slot_materials = base


# ----------------------------------------------------------------------------------------------------------------
# helpers
# ----------------------------------------------------------------------------------------------------------------
def clear(name, pts):
    """Stops the build if a solid of this module comes closer than P.CLEAR to the race centreline."""
    gap = K.note_clear("V16c " + name, pts)
    if gap < P.CLEAR - 0.005:
        raise RuntimeError(f"V16c: '{name}' stands {gap:.2f} m from the race line (limit {P.CLEAR} m)")
    return gap


def inset(ring, d):
    """ccw ring moved inwards by d metres (mitred corners)."""
    n, out = len(ring), []
    for k in range(n):
        p0, p1, p2 = ring[k - 1], ring[k], ring[(k + 1) % n]
        l1, l2 = K.dist(p0, p1) or 1.0, K.dist(p1, p2) or 1.0
        n1 = (-(p1[1] - p0[1]) / l1, (p1[0] - p0[0]) / l1)
        n2 = (-(p2[1] - p1[1]) / l2, (p2[0] - p1[0]) / l2)
        bx, by = n1[0] + n2[0], n1[1] + n2[1]
        bl = math.hypot(bx, by) or 1.0
        bx, by = bx / bl, by / bl
        c = max(0.3, bx * n1[0] + by * n1[1])
        out.append((p1[0] + bx * d / c, p1[1] + by * d / c))
    return out


def fill(m, ring, z, mat):
    """Flat face over a (possibly concave) ring, as triangles."""
    for i, j, k in tessellate_polygon([[Vector((x, y, 0.0)) for x, y in ring]]):
        m.up([(*ring[i], z), (*ring[j], z), (*ring[k], z)], mat)


def jump_frame():
    """Q(a, l): a metres west of the kicker's lip along the flight, l metres to its left (south). = the frame of the
    former terrace_stair; checked against the numbers in campus_west_plan."""
    pts = E.route_points()
    j = E.ROUTE["jump"]["index"]
    o, f = pts[j], pts[j + 3]
    d = K.dist((o["x"], o["y"]), (f["x"], f["y"]))
    u = ((f["x"] - o["x"]) / d, (f["y"] - o["y"]) / d)
    off = max(K.dist((o["x"] + u[0] * a - u[1] * l, o["y"] + u[1] * a + u[0] * l), P.Q(a, l))
              for a, l in ((0.0, 0.0), (20.0, 12.0), (20.0, -12.0)))
    if off > 0.05:
        raise RuntimeError(f"V16c: the jump frame in campus_west_plan is {off:.2f} m off the route's (JUMP_O, JUMP_DIR)")
    return lambda a, l: (o["x"] + u[0] * a - u[1] * l, o["y"] + u[1] * a + u[0] * l)


def deck_uv(p):
    ux, uy = P.DECK_U
    return ((p[0] * ux + p[1] * uy) / DECK_TILE, (-p[0] * uy + p[1] * ux) / DECK_TILE)


def bars(det, a, e, zs, proud=0.09, h=0.06):
    """Thin white horizontal bars in front of a ribbon (sun louvres): one quad each, visible from the right of a -> e."""
    nx, ny = K.right_normal(a, e)
    p, q = (a[0] + nx * proud, a[1] + ny * proud), (e[0] + nx * proud, e[1] + ny * proud)
    for z in zs:
        det.face([(*p, z), (*q, z), (*q, z + h), (*p, z + h)], K.WALL)


# ----------------------------------------------------------------------------------------------------------------
# CTT Student Hall: the terrace deck
# ----------------------------------------------------------------------------------------------------------------
def build_C(b):
    """The generic box (env_buildings.build_one) with the same points, base and roof height, so the deck the karts
    drive on is where it was; only its look changes."""
    import env_buildings
    pts = [tuple(p) for p in b["points"]]
    n = len(pts)
    base = min(E.ground_z(x, y) for x, y in pts) - 1.0
    top = b["roof_z"]
    vol, det = WMesh(), WMesh()
    for k in range(n):
        vol.wall(pts[k], pts[(k + 1) % n], base, top, K.WALL)
    vol.face([(*p, top) for p in pts], DECK, uvs=[deck_uv(p) for p in pts])
    vol.face([(*p, base) for p in pts][::-1], K.WALL)
    xs = [p[0] for p in pts]
    on_roof = env_buildings.roof_route_points(b, top)
    for k in range(n):
        a, e = pts[k], pts[(k + 1) % n]
        seg = K.dist(a, e)
        west = max(a[0], e[0]) < min(xs) + 3.0                     # the three short segments of the west end
        if seg >= 4.0 and not west:                                # ground-storey glazing toward the courtyards
            nx, ny = K.right_normal(a, e)
            g = E.ground_z((a[0] + e[0]) / 2 + nx * 1.5, (a[1] + e[1]) / 2 + ny * 1.5)
            if top - g >= 3.0:
                det.glass(a, e, g + 0.25, g + 3.0, off=0.05, inset=0.6)
        long_edge = seg > 30.0
        if not long_edge:                                          # west end and hall side: the white rim as before
            for q, r in env_buildings.split_around(a, e, top, top + 0.6, on_roof):
                det.face([(*q, top), (*r, top), (*r, top + 0.6), (*q, top + 0.6)], K.WALL)
            continue
        # long edges: no kerb, the railing stands 0.3 m inside the slab edge (survey 2.3, 2.4)
        nx, ny = K.right_normal(a, e)
        count = max(1, int(round(seg / 2.0)))
        run = []
        for i in range(count + 1):
            p = K.lerp2(a, e, i / count)
            p = (p[0] - nx * 0.3, p[1] - ny * 0.3)
            if K.route_gap(*p) >= P.CLEAR:
                run.append((*p, top))
                continue
            if len(run) > 1:
                det.railing(run)
            run = []
        if len(run) > 1:
            det.railing(run)
            clear("deck railing", [p[:2] for p in run])
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


# ----------------------------------------------------------------------------------------------------------------
# B and D
# ----------------------------------------------------------------------------------------------------------------
def wing(vol, det, Pf, body, storey, slab, s_lawn, bands, ribbons, piers=None, screen=None):
    """A west wing in its frame Pf(s, t): Floor-1 body, planted roof strips, set-back glazed roof storey under a thin
    slab. bands: [(t0, t1, material)] across the roof. ribbons: {t of a long facade: (lo, hi, bars)} = the Floor-1
    ribbon there (m above Floor 1) with `bars` thin white horizontal bars. piers: t of the facade that shows Floor 0
    as glazing behind white piers every 3.3 m. screen: t of the facade whose base is the perforated brick screen."""
    s0, s1, t0, t1 = body
    ring = K.rect(Pf, s0, s1, t0, t1)
    base = min(E.ground_z(x, y) for x, y in ring) - 1.0
    top = C.WING_TOP
    vol.prism(ring, base, top, K.WALL, None)
    for ta, tb, mat in bands:
        vol.up([(*Pf(s0, ta), top), (*Pf(s1, ta), top), (*Pf(s1, tb), top), (*Pf(s0, tb), top)], mat)
    mid = (t0 + t1) / 2
    for t, (lo, hi, n_bars) in ribbons.items():
        out = Pf((s0 + s1) / 2, t + (t - mid))
        a, e = K.facing(Pf(s0 + 0.4, t), Pf(s1 - 0.4, t), out)
        det.glass(a, e, F1 + lo, F1 + hi, tile=5.0)
        if n_bars:
            bars(det, a, e, [F1 + lo + (hi - lo) * (k + 1) / (n_bars + 1) for k in range(n_bars)])
        if t == piers:
            det.glass(a, e, F0 + 0.2, F0 + 2.8, mat=K.DARK, bay=3.3, pier=0.45)
        if t == screen:
            nx, ny = K.right_normal(a, e)
            p, q = (a[0] + nx * 0.12, a[1] + ny * 0.12), (e[0] + nx * 0.12, e[1] + ny * 0.12)
            low = min(E.ground_z(*K.lerp2(p, q, k / 20)) for k in range(21)) - 0.4
            det.face([(*p, low), (*q, low), (*q, P.SCREEN_TOP), (*p, P.SCREEN_TOP)], SCREEN)
            c0, c1 = (a[0] + nx * 0.3, a[1] + ny * 0.3), (e[0] + nx * 0.3, e[1] + ny * 0.3)       # white coping
            det.face([(*c0, P.SCREEN_TOP - 0.02), (*c1, P.SCREEN_TOP - 0.02), (*c1, P.SCREEN_TOP + 0.15),
                      (*c0, P.SCREEN_TOP + 0.15)], K.WALL)
            det.up([(*a, P.SCREEN_TOP + 0.15), (*e, P.SCREEN_TOP + 0.15), (*c1, P.SCREEN_TOP + 0.15),
                    (*c0, P.SCREEN_TOP + 0.15)], K.WALL)
    a0, a1, b0, b1 = storey
    vol.prism(K.rect(Pf, a0, a1, b0, b1), top, C.WING_STOREY_TOP, K.GLASS, None, tile=5.0)
    c0, c1, d0, d1 = slab
    vol.prism(K.rect(Pf, c0, c1, d0, d1), C.WING_STOREY_TOP, C.WING_STOREY_TOP + 0.4, K.WALL, K.ROOF, K.WALL)
    K.Mesh.railing(det, [(*Pf(s0 + 0.5, s_lawn), top), (*Pf(s1 - 0.5, s_lawn), top)])


def frame_box(vol, det, a, e, spec):
    """Framed glass box on the blank face a -> e (visible from its right): rows x bays of glazing inside a white
    frame that stands `proud` of the wall."""
    L = K.dist(a, e)
    pa, pe = K.lerp2(a, e, spec["t0"]), K.lerp2(a, e, spec["t1"])
    nx, ny = K.right_normal(a, e)
    tx, ty = (e[0] - a[0]) / L, (e[1] - a[1]) / L
    f, d = spec["frame"], spec["proud"]
    z0, z1 = spec["z0"], spec["z1"]
    out = lambda p, k=d: (p[0] + nx * k, p[1] + ny * k)                              # noqa: E731
    along = lambda p, k: (p[0] + tx * k, p[1] + ty * k)                              # noqa: E731
    for lo, hi in ((z0 - f, z0), (z1, z1 + f)):                                      # bottom and top member
        vol.prism([along(pa, -f), along(pe, f), out(along(pe, f)), out(along(pa, -f))], lo, hi, K.WALL, K.WALL, K.WALL)
    for p, k in ((pa, -f), (pe, f)):                                                 # jambs
        q = along(p, k)
        ring = [q, p, out(p), out(q)] if k < 0 else [p, q, out(q), out(p)]
        vol.prism(ring, z0, z1, K.WALL, None)
    width = K.dist(pa, pe)
    rows = spec["rows"]
    for r in range(rows):
        lo, hi = z0 + (z1 - z0) * r / rows, z0 + (z1 - z0) * (r + 1) / rows
        det.glass(pa, pe, lo + (0.06 if r else 0.0), hi - (0.06 if r < rows - 1 else 0.0), off=0.05,
                  tile=4.0 * width / spec["bays"])
    for r in range(1, rows):                                                         # white transom
        z = z0 + (z1 - z0) * r / rows
        det.face([(*out(pa, 0.1), z - 0.06), (*out(pe, 0.1), z - 0.06), (*out(pe, 0.1), z + 0.06),
                  (*out(pa, 0.1), z + 0.06)], K.WALL)


def build_B(b):
    Pf = C.B
    vol, det = WMesh(), WMesh()
    s0, s1, t0, t1 = C.B_BODY_ST
    wing(vol, det, Pf, C.B_BODY_ST, C.B_STOREY_ST, C.B_SLAB_ST, t0 + 0.15,
         [(t0, t0 + 0.8, K.WALL), (t0 + 0.8, C.B_SLAB_ST[2], K.PLANT), (C.B_SLAB_ST[2], C.B_SLAB_ST[3], K.ROOF),
          (C.B_SLAB_ST[3], t1 - 0.8, K.PLANT), (t1 - 0.8, t1, K.WALL)],
         {t0: (0.9, 3.3, 3), t1: (0.4, 3.4, 0)}, screen=t0)
    # the head: white kite with the portal hood on its south-east corner, dark roofed link to the bar
    base = min(E.ground_z(x, y) for x, y in C.B_HEAD) - 1.0
    vol.prism(C.B_HEAD, base, C.HEAD_TOP, K.WALL, K.WALL)
    vol.prism(C.B_LINK, base, C.WING_TOP + 0.5, K.WALL, K.DARKROOF)
    a, e = K.facing(C.B_HEAD[0], C.B_HEAD[1], (-90.0, 55.0))        # the south-west face: framed glass box (item 18)
    frame_box(vol, det, a, e, P.B_BOX)
    hood = K.ccw(C.B_HOOD)
    sill = F1 - 0.2
    tops = [22.0 if p[1] < 62.0 else (21.6 if p[1] < 65.0 else C.HEAD_TOP + 0.05) for p in hood]
    front = [k for k in range(len(hood)) if hood[k][1] < 62.0 and hood[(k + 1) % len(hood)][1] < 62.0]
    vol.prism(hood, base, tops, K.WALL, K.WALL, skip=front)
    a, e = K.facing(hood[front[0]], hood[(front[0] + 1) % len(hood)], (-55.0, 50.0))
    nx, ny = K.right_normal(a, e)
    ia, ie = (a[0] - nx * 2.5, a[1] - ny * 2.5), (e[0] - nx * 2.5, e[1] - ny * 2.5)       # recess 2.5 m deep
    pa, pe = K.lerp2(a, e, 0.1), K.lerp2(a, e, 0.9)                                       # the frame's jambs
    qa, qe = K.lerp2(ia, ie, 0.1), K.lerp2(ia, ie, 0.9)
    head = sill + 6.0
    vol.wall(a, pa, base, 22.0, K.WALL)
    vol.wall(pe, e, base, 22.0, K.WALL)
    vol.wall(pa, pe, head, 22.0, K.WALL)
    vol.wall(pa, pe, base, sill, K.WALL)
    vol.face([(*qa, sill), (*qe, sill), (*qe, head), (*qa, head)], K.DARK)                # dark glazed entrance
    vol.towards([(*pa, sill), (*qa, sill), (*qa, head), (*pa, head)], K.WALL, (*K.lerp2(pa, pe, 0.5), sill + 3))
    vol.towards([(*pe, sill), (*qe, sill), (*qe, head), (*pe, head)], K.WALL, (*K.lerp2(pa, pe, 0.5), sill + 3))
    vol.up([(*pa, sill), (*pe, sill), (*qe, sill), (*qa, sill)], K.CONC)
    vol.down([(*pa, head), (*pe, head), (*qe, head), (*qa, head)], K.WALL)
    for k in range(4):                                                                    # steps down to the lawn
        d = 0.35 * (k + 1)
        sa, se = (pa[0] + nx * d, pa[1] + ny * d), (pe[0] + nx * d, pe[1] + ny * d)
        det.prism(K.ccw([pa, pe, se, sa]), base, sill - 0.17 * (k + 1), K.CONC, K.CONC)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


def build_D(b):
    Pf = C.D
    vol, det = WMesh(), WMesh()
    s0, s1, t0, t1 = C.D_BODY_ST
    ts = P.D_SOUTH_T
    wing(vol, det, Pf, C.D_BODY_ST, C.D_STOREY_ST, C.D_SLAB_ST, ts - 0.15,
         [(t0, t0 + 0.8, K.WALL), (t0 + 0.8, C.D_SLAB_ST[2], K.PLANT), (C.D_SLAB_ST[2], C.D_SLAB_ST[3], K.ROOF),
          (C.D_SLAB_ST[3], t1, K.PLANT)], {t0: (0.9, 3.3, 3)}, piers=t0)
    # south side (survey C 2.2): glazed ground floor behind slim white columns, the upper storey projects 1.5 m
    p0, p1, q0, q1 = C.D_PODIUM_ST
    ring = K.rect(Pf, p0, p1, q0, q1)
    base = min(E.ground_z(x, y) for x, y in ring) - 1.0
    vol.prism(ring, base, F1, K.WALL, None)
    out = Pf((p0 + p1) / 2, ts + 20)
    a, e = K.facing(Pf(p0 + 0.3, q1), Pf(p1 - 0.3, q1), out)
    det.glass(a, e, F0 + 0.15, F1 - 0.4, tile=5.5)
    upper = K.rect(Pf, p0, p1, q0, ts)
    vol.prism(upper, F1, C.WING_TOP, K.WALL, None, K.WALL)
    top = C.WING_TOP
    for ta, tb, mat in ((q0, ts - 0.8, K.PLANT), (ts - 0.8, ts, K.WALL)):
        vol.up([(*Pf(p0, ta), top), (*Pf(p1, ta), top), (*Pf(p1, tb), top), (*Pf(p0, tb), top)], mat)
    a, e = K.facing(Pf(p0 + 0.4, ts), Pf(p1 - 0.4, ts), out)
    det.glass(a, e, F1 + 0.9, F1 + 3.3, tile=5.0)
    count = int((p1 - p0 - 1.0) // 5.5)
    for k in range(count + 1):                                       # slim columns at 5.5 m
        c = Pf(p0 + 0.5 + k * 5.5, ts - 0.2)
        det.box(c, C.D_U, 0.15, 0.15, E.ground_z(*c) - 0.3, F1, K.WALL)
    build_D_head(vol, det)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


def build_D_head(vol, det):
    """D's head (survey C 2.2, W27): a solid white wedge standing in the pool. South-west: a deep white frame box
    with set-back full-height glazing and a glass balustrade; south: blank wall; then the prow with a slot and a
    small balcony; north-west tip: the dark glazed 'mouth'. The dark inclined deck and the stone courses at its
    foot are built with the grounds (build_grounds)."""
    head = K.ccw(C.D_HEAD)
    base = min(E.ground_z(x, y) for x, y in head) - 1.0
    f = P.FRAME
    a, e = P.HEAD_SW
    i = min(range(len(head)), key=lambda k: K.dist(head[k], a))
    if K.dist(head[(i + 1) % len(head)], e) > 0.01:
        raise RuntimeError("V16c: campus_west_plan.HEAD_SW is not an edge of campus_plan.D_HEAD")
    nx, ny = K.right_normal(a, e)                                    # outwards
    pa, pe = K.lerp2(a, e, f["t0"]), K.lerp2(a, e, f["t1"])
    qa, qe = (pa[0] - nx * f["depth"], pa[1] - ny * f["depth"]), (pe[0] - nx * f["depth"], pe[1] - ny * f["depth"])
    notched = head[:i + 1] + [pa, qa, qe, pe] + head[i + 1:]
    z0, z1 = f["z0"], f["z1"]
    vol.prism(head, base, z0, K.WALL, None)
    vol.prism(notched, z0, z1, K.WALL, None)
    vol.prism(head, z1, C.HEAD_TOP, K.WALL, K.WALL)
    vol.up([(*pa, z0), (*pe, z0), (*qe, z0), (*qa, z0)], K.CONC)                         # loggia floor
    vol.down([(*pa, z1), (*pe, z1), (*qe, z1), (*qa, z1)], K.WALL)                       # its soffit
    det.glass(qa, qe, z0 + 0.08, z1 - 0.08, off=0.06, tile=4.0 * K.dist(qa, qe) / 6)     # set-back glazing, 6 panes
    ba, be = K.lerp2(pa, pe, 0.01), K.lerp2(pa, pe, 0.99)
    ba, be = (ba[0] - nx * 0.12, ba[1] - ny * 0.12), (be[0] - nx * 0.12, be[1] - ny * 0.12)
    det.face([(*ba, z0), (*be, z0), (*be, z0 + 1.0), (*ba, z0 + 1.0)], K.GLASS_ROOF)      # glass balustrade
    vol.prism(C.D_LINK, C.WING_TOP, C.HEAD_TOP + 0.3, K.WALL, K.DARKROOF)
    # the prow (ring points 6..9 of D_HEAD): higher, with an angled top, a tall slot and a small balcony
    prow = [C.D_HEAD[k] for k in (6, 7, 8, 9)]
    vol.prism(prow, C.HEAD_TOP, [C.HEAD_TOP + 0.25, C.HEAD_TOP + 1.7, C.HEAD_TOP + 1.7, C.HEAD_TOP + 0.25],
              K.WALL, K.WALL)             # (the same list for either winding: the two south points are the high ones)
    a, e = K.facing(C.D_HEAD[7], C.D_HEAD[8], (-20.0, -40.0))
    det.glass(K.lerp2(a, e, 0.3), K.lerp2(a, e, 0.7), F1 + 0.3, C.HEAD_TOP - 0.6, mat=K.DARK)        # the slot
    nx, ny = K.right_normal(a, e)
    b0, b1 = K.lerp2(a, e, 0.12), K.lerp2(a, e, 0.88)
    o0, o1 = (b0[0] + nx * 1.2, b0[1] + ny * 1.2), (b1[0] + nx * 1.2, b1[1] + ny * 1.2)
    det.prism(K.ccw([b0, b1, o1, o0]), F1 + 0.1, F1 + 0.3, K.WALL, K.CONC, K.WALL)                  # balcony slab
    for p, q in ((b0, o0), (o0, o1), (o1, b1)):
        det.face([(*p, F1 + 0.3), (*q, F1 + 0.3), (*q, F1 + 1.3), (*p, F1 + 1.3)], K.GLASS_ROOF)
        det.face([(*q, F1 + 0.3), (*p, F1 + 0.3), (*p, F1 + 1.3), (*q, F1 + 1.3)], K.GLASS_ROOF)
    # the north-west tip: dark glazed mouth in the white shell
    a, e = K.facing(C.D_HEAD[1], C.D_HEAD[2], (-60.0, 30.0))
    g = E.ground_z(*K.lerp2(a, e, 0.5))
    det.glass(a, e, g + 0.5, C.HEAD_TOP - 1.4, mat=K.DARK, inset=0.45)


# ----------------------------------------------------------------------------------------------------------------
# Grounds: terrace west end, pool, D's deck, path wall, slabs
# ----------------------------------------------------------------------------------------------------------------
def build_grounds():
    m = WMesh()
    Q = jump_frame()
    west_wedge(m, Q)
    west_stair(m, Q)
    pool(m)
    head_deck(m)
    path_wall(m)
    mesh = m.finish("World_CampusWest")
    E.new_object("World_CampusWest", mesh, E.collection("World_Buildings_Campus"))
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    s = WMesh()
    slabs(s)
    smesh = s.finish("World_CampusWestSlabs")
    E.new_object("World_CampusWestSlabs_NoCollide", smesh, E.collection("World_Buildings_Campus"))
    print(f"  campus west grounds: {tris} + {sum(len(p.vertices) - 2 for p in smesh.polygons)} triangles")
    K.keep_out(K.ccw([tuple(p) for p in P.PAD_RING]))
    K.keep_out(P.strip(P.PATH, P.PATH_W + 1.6))
    # no generated lamp, bin or plant between the jump corridor and the two flanks (as before V16c: from 5.5 m out)
    w, st = P.WEDGE, P.STAIR
    far = st["a_top"] + st["run"] + 4.0
    K.keep_out([Q(-1.0, 5.5), Q(far, 5.5), Q(far, w["l1"] + 1.0), Q(-1.0, w["l1"] + 1.0)])
    north = st["l0"] + st["clear"] + 2 * st["cheek"] + 1.0
    K.keep_out([Q(-1.0, -5.5), Q(-1.0, -north), Q(far, -north), Q(far, -5.5)])


def west_wedge(m, Q):
    """The ramp block south of the jump corridor: rough concrete ramp from deck level up to the flat crest, the
    inclined white west face down to the lawn, vertical white flanks, tan railings along both sides and across the
    crest's west edge."""
    w = P.WEDGE
    l0, l1, af, ac, ae, zc = w["l0"], w["l1"], w["a_foot"], w["a_crest"], w["a_edge"], w["crest_z"]
    g_toe = min(E.ground_z(*Q(ae + 5.5, l)) for l in (l0, (l0 + l1) / 2, l1))
    zb = min(min(E.ground_z(*Q(a, l)) for a in (af, 0.0, ae, ae + 5.5) for l in (l0, l1)), g_toe) - 0.4
    a_toe = ae + (zc - zb)                                           # 45 degrees
    P3 = lambda a, l, z: (*Q(a, l), z)                               # noqa: E731
    m.up([P3(af, l0, F1), P3(ac, l0, zc), P3(ac, l1, zc), P3(af, l1, F1)], K.CONC)             # ramp
    m.up([P3(ac, l0, zc), P3(ae, l0, zc), P3(ae, l1, zc), P3(ac, l1, zc)], K.CONC)             # crest
    m.up([P3(ae, l0, zc), P3(a_toe, l0, zb), P3(a_toe, l1, zb), P3(ae, l1, zc)], K.WALL)       # inclined west face
    for l, towards in ((l0, l0 - 5.0), (l1, l1 + 5.0)):                                        # flanks
        m.towards([P3(af, l, zb), P3(a_toe, l, zb), P3(ae, l, zc), P3(ac, l, zc), P3(af, l, F1)], K.WALL,
                  P3(0.0, towards, F1))
    m.towards([P3(af, l0, zb), P3(af, l1, zb), P3(af, l1, F1), P3(af, l0, F1)], K.WALL, P3(af - 5.0, (l0 + l1) / 2, F1))
    for l in (l0 + 0.15, l1 - 0.15):
        m.railing([P3(af, l, F1), P3(ac, l, zc), P3(ae - 0.1, l, zc)])
    m.railing([P3(ae - 0.1, l0 + 0.15, zc), P3(ae - 0.1, l1 - 0.15, zc)])
    ring = [Q(af, l0), Q(a_toe, l0), Q(a_toe, l1), Q(af, l1)]
    clear("terrace wedge", K.densify(ring, 1.0, closed=True))
    K.keep_out(K.ccw(ring), 1.0)


def west_stair(m, Q):
    """The long open stair north of the jump corridor, from a landing at deck level down west to the pad: white
    cheek walls with tan handrails (and one in the middle), the brown louvred door and a vent grille in the south
    cheek, which faces the landing road."""
    s = P.STAIR
    n = -1.0                                                         # north = negative l
    li, lo_ = s["l0"], s["l0"] + s["cheek"]                          # south cheek: |l| from li to lo_
    ni, no = lo_ + s["clear"], lo_ + s["clear"] + s["cheek"]         # north cheek
    a0, at, run, steps = s["a_land"], s["a_top"], s["run"], s["steps"]
    a_end = at + run
    going = run / steps
    z_foot = max(E.ground_z(*Q(a_end + 0.3, n * l)) for l in (lo_, (lo_ + ni) / 2, ni)) + 0.02
    riser = (F1 - z_foot) / steps
    zb = min(E.ground_z(*Q(a, n * l)) for a in (a0, at, at + run / 2, a_end) for l in (li, no)) - 0.4
    line = lambda a: F1 if a <= at else F1 - (F1 - z_foot) * (a - at) / run            # noqa: E731
    par = s["parapet"]
    P3 = lambda a, l, z: (*Q(a, n * l), z)                           # noqa: E731
    # landing at deck level: from the deck's north-west corner and the courtyard wall to the top riser
    deck_nw, court_sw = (-1.3, 63.5), tuple(K.ccw(C.COURT_N)[0])
    court_w = (court_sw[0], Q(at, n * no)[1] + 0.8)
    land = K.ccw([Q(a0, n * li), Q(at, n * li), Q(at, n * no), court_w, court_sw, deck_nw])
    fill(m, land, F1, K.CONC)
    for p, q in zip(land, land[1:] + land[:1]):
        if K.dist(p, q) > 0.3:
            m.wall(p, q, zb, F1, K.WALL)
    for k in range(steps):                                           # treads and risers
        ka, kb = at + k * going, at + (k + 1) * going
        z = F1 - (k + 1) * riser
        m.up([P3(ka, lo_, z), P3(kb, lo_, z), P3(kb, ni, z), P3(ka, ni, z)], K.CONC)
        m.towards([P3(ka, lo_, z), P3(ka, ni, z), P3(ka, ni, z + riser), P3(ka, lo_, z + riser)], K.CONC,
                  P3(ka + 5.0, (lo_ + ni) / 2, z + 2.0))
    for inner, outer in ((li, lo_), (no, ni)):                       # cheeks (inner = the face away from the flight)
        for l, towards in ((inner, inner + (inner - outer) * 20), (outer, outer + (outer - inner) * 20)):
            m.towards([P3(at, l, zb), P3(a_end, l, zb), P3(a_end, l, z_foot + par), P3(at, l, F1 + par)], K.WALL,
                      P3((at + a_end) / 2, towards, F1))
        m.up([P3(at, inner, F1 + par), P3(a_end, inner, z_foot + par), P3(a_end, outer, z_foot + par),
              P3(at, outer, F1 + par)], K.WALL)
        m.towards([P3(a_end, inner, zb), P3(a_end, outer, zb), P3(a_end, outer, z_foot + par),
                   P3(a_end, inner, z_foot + par)], K.WALL, P3(a_end + 5.0, inner, F1))
        m.towards([P3(at, inner, F1), P3(at, outer, F1), P3(at, outer, F1 + par), P3(at, inner, F1 + par)], K.WALL,
                  P3(at - 5.0, inner, F1 + 1.0))
        mid = (inner + outer) / 2
        m.railing([P3(at, mid, F1 + par), P3(a_end, mid, z_foot + par)], h=0.6)
    m.railing([P3(at, (lo_ + ni) / 2, F1), P3(a_end, (lo_ + ni) / 2, z_foot)], h=0.95)   # middle handrail
    m.railing([P3(a0, li + 0.15, F1), P3(at, li + 0.15, F1)], h=1.1)                    # landing, towards the jump
    m.railing([(*K.lerp2(land[land.index(Q(at, n * no))], court_w, 0.04), F1), (*court_w, F1)], h=1.1)   # north edge
    # door and vent on the south cheek's outer face
    d, v = P.DOOR, P.VENT
    lf = li - 0.05                                                   # 5 cm proud of the cheek (no flicker)
    g = max(E.ground_z(*Q(d["a0"], n * li)), E.ground_z(*Q(d["a1"], n * li)))
    if line(d["a1"]) + par - g < d["h"] + 0.4:
        raise RuntimeError("V16c: the door does not fit under the stair cheek")
    face = lambda pts, mat: m.towards(pts, mat, P3(3.0, li - 20.0, F1))                  # noqa: E731
    face([P3(d["a0"], lf, g + 0.03), P3(d["a1"], lf, g + 0.03), P3(d["a1"], lf, g + d["h"]), P3(d["a0"], lf, g + d["h"])],
         K.UMBER)
    mid = (d["a0"] + d["a1"]) / 2
    face([P3(mid - 0.02, lf - 0.05, g + 0.03), P3(mid + 0.02, lf - 0.05, g + 0.03), P3(mid + 0.02, lf - 0.05, g + d["h"]),
          P3(mid - 0.02, lf - 0.05, g + d["h"])], K.SLAT)             # the gap between the two leaves
    face([P3(v["a0"], lf, g + v["z"]), P3(v["a1"], lf, g + v["z"]), P3(v["a1"], lf, g + v["z"] + 0.4),
          P3(v["a0"], lf, g + v["z"] + 0.4)], K.SLAT)
    ring = [Q(a0, n * (li - 0.1)), Q(a_end, n * (li - 0.1)), Q(a_end, n * no), Q(a0, n * no)]
    clear("terrace stair", K.densify(ring, 1.0, closed=True) + land)
    K.keep_out(K.ccw(ring), 1.0)
    K.keep_out(land, 0.5)


def pool(m):
    """D's reflecting pool: flush pale concrete kerb along the edges that meet the lawn, olive-grey water."""
    ring = K.ccw([tuple(p) for p in P.D_POOL])
    if ring != [tuple(p) for p in P.D_POOL]:
        raise RuntimeError("V16c: campus_west_plan.D_POOL must be counter-clockwise (KERB_EDGES index its edges)")
    inner = inset(ring, P.KERB_W)
    top = P.D_POOL_Z + 0.12                                          # the tray's rim (apron) lies at + 0.10
    n = len(ring)
    for k in P.KERB_EDGES:
        j = (k + 1) % n
        m.up([(*ring[k], top), (*ring[j], top), (*inner[j], top), (*inner[k], top)], K.CONC)
        m.wall(inner[j], inner[k], P.D_POOL_Z - 0.5, top, K.CONC)
        m.wall(ring[k], ring[j], P.D_POOL_Z - 0.3, top, K.CONC)
    fill(m, inset(ring, 0.2), P.D_POOL_Z - 0.03, OLIVE)
    K.keep_out(ring, 1.0)


def head_deck(m):
    """Between D's head and the water (south-west and south faces): a dark inclined deck and, at its lower edge,
    three courses of beige stone blocks stepping down into the water."""
    h = P.HEAD_DECK
    a, b = P.HEAD_SW
    c = P.HEAD_S[1]
    n1, n2 = K.right_normal(a, b), K.right_normal(b, c)              # outwards (D_HEAD is ccw)
    bx, by = n1[0] + n2[0], n1[1] + n2[1]
    bl = math.hypot(bx, by)
    bis = (bx / bl, by / bl)
    cosh = bis[0] * n1[0] + bis[1] * n1[1]

    def row(d):
        return [(a[0] + n1[0] * d, a[1] + n1[1] * d), (b[0] + bis[0] * d / cosh, b[1] + bis[1] * d / cosh),
                (c[0] + n2[0] * d, c[1] + n2[1] * d)]
    stations = [(0.0, h["top"], K.DARKROOF), (h["width"], h["foot"], None)]
    for k in range(h["courses"]):
        stations.append((h["width"] + (k + 1) * h["tread"], h["foot"] - k * h["riser"], K.CREAM))
    low = P.D_POOL_Z - 0.5
    prev_d, prev_z = stations[0][0], stations[0][1]
    for d, z, mat in stations[1:]:
        r0, r1 = row(prev_d), row(d)
        z0 = prev_z if mat is None else z                            # the deck slopes, the courses are flat
        for i in range(2):
            m.up([(*r0[i], z0), (*r0[i + 1], z0), (*r1[i + 1], z), (*r1[i], z)], K.DARKROOF if mat is None else mat)
            if mat is not None and prev_z > z + 0.01:                # riser above this course
                m.wall(r0[i], r0[i + 1], z, prev_z, K.CREAM)
        prev_d, prev_z = d, z
    r1 = row(prev_d)
    for i in range(2):
        m.wall(r1[i], r1[i + 1], low, prev_z, K.CREAM)               # the last course's front, into the water
    for i, towards in ((0, (-60.0, 0.0)), (2, (-20.0, -40.0))):      # end caps
        p0, p1, p2 = row(0.0)[i], row(h["width"])[i], row(prev_d)[i]
        m.towards([(*p0, low), (*p2, low), (*p2, prev_z), (*p1, h["foot"]), (*p0, h["top"])], K.WALL, (*towards, 10.0))
    K.keep_out(K.ccw(row(0.0) + row(prev_d)[::-1]))


def path_wall(m):
    """The continuous low wall on the outer side of the stone-paved perimeter path (pale concrete, 0.45 m high,
    0.4 m thick), following the ground."""
    half = P.PATH_W / 2
    outer = P.strip(P.PATH, 2 * half + P.WALL_T)                     # outer side = the right of the path's direction
    k = len(P.PATH)
    line = outer[k:][::-1]                                           # the right-hand offsets, in path order
    pts = K.densify(line, 4.0)
    clear("perimeter wall", pts)
    rows = []
    for i, p in enumerate(pts):
        a, b = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
        nx, ny = K.right_normal(a, b)
        t = P.WALL_T / 2
        gi, go = (p[0] - nx * t, p[1] - ny * t), (p[0] + nx * t, p[1] + ny * t)
        z = max(E.ground_z(*gi), E.ground_z(*go))
        rows.append((gi, go, z + P.WALL_H, min(E.ground_z(*gi), E.ground_z(*go)) - 0.3))
    for (i0, o0, z0, b0), (i1, o1, z1, b1) in zip(rows, rows[1:]):
        m.up([(*i0, z0), (*i1, z1), (*o1, z1), (*o0, z0)], K.CONC)
        m.face([(*i1, b1), (*i0, b0), (*i0, z0), (*i1, z1)], K.CONC)             # towards the path
        m.face([(*o0, b0), (*o1, b1), (*o1, z1), (*o0, z0)], K.CONC)             # towards the road
    for (i_, o_, z, b), flip in ((rows[0], False), (rows[-1], True)):
        pts4 = [(*i_, b), (*o_, b), (*o_, z), (*i_, z)]
        m.face(pts4[::-1] if flip else pts4, K.CONC)


def slabs(m):
    """Stepping slabs set flush in the south lawn, toward D (0.6 x 1.2 m, 5 cm over the grass, no collider)."""
    s = P.SLABS
    a, b = s["from"], s["to"]
    L = K.dist(a, b)
    tx, ty = (b[0] - a[0]) / L, (b[1] - a[1]) / L
    nx, ny = -ty, tx
    w, d = s["size"][1] / 2, s["size"][0] / 2
    count = int(L // s["pitch"]) + 1
    pts = []
    for k in range(count):
        c = (a[0] + tx * k * s["pitch"], a[1] + ty * k * s["pitch"])
        ring = [(c[0] + tx * i + nx * j, c[1] + ty * i + ny * j) for i, j in ((-d, -w), (d, -w), (d, w), (-d, w))]
        z = max(E.ground_z(*p) for p in ring) + 0.05
        m.up([(*p, z) for p in ring], K.CONC)
        pts += ring
    clear("stepping slabs", pts)


# ----------------------------------------------------------------------------------------------------------------
# Terrain: the grass berm in front of hall E (called from env_terrain.apply)
# ----------------------------------------------------------------------------------------------------------------
def raise_nodes(heights):
    """The 5 m nodes inside campus_west_plan.BERM_TOP go up to BERM_Z, those on the slope band between BERM_FOOT
    and BERM_TOP in between (never down). The band lies 60 m and more from the race line."""
    foot, top = P.BERM_FOOT, P.BERM_TOP
    xs, ys = [p[0] for p in foot], [p[1] for p in foot]
    n = 0
    for j in range(E.ROWS):
        for i in range(E.COLS):
            x, y = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            if not (min(xs) <= x <= max(xs) and min(ys) <= y <= max(ys)) or not C.inside(x, y, foot):
                continue
            if E.distance_to_route(x, y) < 20.0:
                raise RuntimeError(f"V16c: the berm reaches the race line at ({x}, {y})")
            if C.inside(x, y, top):
                t = 1.0
            else:
                df, dt = C.ring_distance(x, y, foot), C.ring_distance(x, y, top)
                t = df / max(df + dt, 0.01)
                t = t * t * (3 - 2 * t)
            new = heights[j][i] + max(P.BERM_Z - heights[j][i], 0.0) * t
            if new - heights[j][i] > 0.01:
                heights[j][i] = new
                n += 1
    print(f"  campus west: {n} terrain nodes raised for the berm in front of hall E (top {P.BERM_Z:.2f} m)")


# ----------------------------------------------------------------------------------------------------------------
# Dressing: courtyards and the real lawn trees (called from env_campus.dress)
# ----------------------------------------------------------------------------------------------------------------
def drops(p):
    """True for a generated plant this package removes: every tree and shrub in the two courtyards (they get the
    sapling rows instead) and on the berm (plain grass)."""
    x, y = p["x"], p["y"]
    return C.inside(x, y, C.COURT_N) or C.inside(x, y, C.COURT_S) or C.inside(x, y, P.BERM_FOOT)


def plants(veg):
    """Rows of thin staked saplings in the courtyards, parallel to the deck, and the real trees of the two lawns
    (each only where no tree stands within 3 m already)."""
    out = []
    ux, uy = P.DECK_U
    for ring, rows in ((C.COURT_S, P.COURT_S_ROWS), (C.COURT_N, P.COURT_N_ROWS)):
        ox, oy = rows["origin"]
        for r, off in enumerate(rows["rows"]):
            for k in range(rows["count"]):
                a = k * rows["pitch"] + (rows["pitch"] / 2 if r % 2 else 0.0)
                x, y = ox + ux * a - uy * off, oy + uy * a + ux * off
                if not C.inside(x, y, ring) or C.ring_distance(x, y, ring) < 1.6:
                    continue
                h = (math.sin(x * 12.9898 + y * 78.233) * 43758.5453) % 1.0
                lo, hi = P.SAPLING_SCALE
                out.append({"proto": "Veg_PlazaTree" if (k + r) % 2 else "Veg_PlazaTree_B", "x": round(x, 3),
                            "y": round(y, 3), "z": C.COURT_Z, "rot_z": round(h * 6.283, 4),
                            "scale": round(lo + (hi - lo) * h, 3)})
    g = P.B_GRASS                                                    # low grasses in the two strips along B's foot
    s = g["s"][0]
    while s <= g["s"][1]:
        for r, t in enumerate(g["t"]):
            x, y = C.B(s + (g["pitch"] / 2 if r else 0.0), t)
            h = (math.sin(x * 12.9898 + y * 78.233) * 43758.5453) % 1.0
            if K.route_gap(x, y) >= 8.0 and not any(C.inside(x, y, ring) for ring in K.KEEP_OUT):
                out.append({"proto": "Veg_Grass", "x": round(x, 3), "y": round(y, 3),
                            "z": round(E.ground_z(x, y) - 0.03, 3), "rot_z": round(h * 6.283, 4),
                            "scale": round(g["scale"][0] + (g["scale"][1] - g["scale"][0]) * h, 3)})
        s += g["pitch"]
    for x, y, proto, scale in P.TREES:
        if any(math.hypot(q["x"] - x, q["y"] - y) < 3.0 and not q["proto"].startswith(("Veg_Shrub", "Veg_Grass"))
               for q in veg):
            continue
        if K.route_gap(x, y) < 7.0:
            raise RuntimeError(f"V16c: tree at ({x}, {y}) stands {K.route_gap(x, y):.1f} m from the race line")
        out.append({"proto": proto, "x": x, "y": y, "z": round(E.ground_z(x, y) - 0.05, 3), "rot_z": 0.6, "scale": scale})
    return out


def relevel(props):
    """Props that stand on the berm keep their place and get the new ground height."""
    n = 0
    for p in props:
        if C.inside(p["x"], p["y"], P.BERM_FOOT):
            z = round(E.ground_z(p["x"], p["y"]), 3)
            if abs(z - p["z"]) > 0.02:
                p["z"] = z
                n += 1
    return n


K.OWNED[C_ID] = build_C           # env_buildings.build_one hands the CTT hall over like the other campus buildings
