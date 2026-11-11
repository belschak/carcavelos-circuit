"""Campus buildings v3 (V10c2, 2026-11-11): every Nova SBE building on and west of the spine as itself.

Replaces the generic three-storey ribbon box for the buildings in OWNED (env_buildings.build_one hands them over).
Plan data and sources: campus_plan.py (survey V10h: DGT orthophoto, Google tiles, the team's photos).
  main building   Gallery spine: 7 m glass wall set back under the overhanging roof slab (white fascia, truss fins,
                  slender columns), Floor-1 deck over a dark Floor-0 arcade at the lower plaza; atrium block: grey
                  roof with the one real glass field (lantern with a steel grid), glazed west entrance front, blank
                  white end wall, solar rows on the west half of the spine roof. The drive-through hall, its bays and
                  floor are cut exactly as before (build_world.carve_drive_throughs): the body keeps a closed volume.
  B, D            Floor-1 ribbon storey with white slab bands, planted roof strips, set-back glazed roof storey under
                  a thin slab, blank angular white heads (B: portal hood; D: cantilever over a dark base, in a pool)
  E               white podium with two upper boxes under dark roofs
  F               faceted white monolith: ridge, crease, undercut north-west corner with a dark entrance, grass bank
  G               Floor-1 podium on round columns over a dark undercroft, cream box over a glazed front, glazed
                  block with a balcony, flying roof slab on two tall columns, seat steps to the south
  SC              two-storey white box
Grounds: the two sunken courtyards beside the CTT terrace and D's reflecting pool are 'trays' (the 5 m terrain cells
they touch are replaced by an apron mesh that meets the tray outline; E.ground_z returns the tray floor), the lower
plaza east of the race line is levelled to Floor 0 (lower_nodes), grass berms rise against the heads and F.
Nothing here stands closer than ROUTE_CLEAR to the race line; route files and the road are untouched.
"""
import math
import os

import bmesh
import bpy
from mathutils.geometry import delaunay_2d_cdt
from mathutils import Vector

import campus_plan as C
import env_common as E

F0, F1, F2, F3 = C.F0, C.F1, C.F2, C.F3
ROUTE_CLEAR = 6.5            # m from the race centreline: nothing of ours stands closer (road half-width 4 m)

# material slots, the same list on every mesh of this module (unused slots are dropped per mesh)
(WALL, GLASS, ROOF, STEEL, GLASS_ROOF, SOLAR, CREAM, DARK, DARKROOF, PLANT, TERRA, CONC, SAND, WATER, BRICK, GROUND,
 RAIL) = range(17)
TILE_M = {TERRA: 1.0, CONC: 2.0, SAND: 2.0, BRICK: 2.0}       # metres per texture tile; everything else 3 m


def slot_materials():
    return [E.material("B_wall_white", "#EAEAEC", 0.85),
            E.material("B_glass", "#2E3A46", roughness=0.08, metallic=0.5),
            E.material("B_roof", "#D5D3CE", 0.9),
            E.material("B_steel", "#A7ABB2", roughness=0.35, metallic=0.8),
            E.material("B_glass_roof", "#8FB2C9", roughness=0.05, metallic=0.3),
            E.material("B_solar", "#243349", roughness=0.75, metallic=0.0),
            E.material("B_wall_cream", "#E2DACA", 0.85),
            E.material("B_glass_dark", "#22292F", roughness=0.1, metallic=0.4),
            E.material("B_roof_brown", "#5C5048", 0.9),      # (B_roof_dark is the beachfront's)
            E.material("B_roof_planted", "#6F7D54", 0.95),
            E.material("T_terracotta", E.PALETTE["terracotta"], roughness=0.9),
            E.material("T_concrete_light", E.PALETTE["concrete_light"], roughness=0.9),
            E.material("B_deck_paving", E.PALETTE["concrete_sand"], roughness=0.9),    # decks at plaza level
            E.material("B_pool_water", "#7F968A", roughness=0.05, metallic=0.25),
            E.material("B_brick_buff", "#B89C7A", 0.9),
            E.material("ground_map", "#A6AA82", roughness=0.95),
            railing_material()]


def railing_material():
    """Bar railing as one alpha-clipped sheet per 3 m (textures/railing_cutout_albedo.png, make_textures.py railing):
    custom properties alpha_clip / double_sided go through materials.json like the leaf cutouts."""
    mat = E.material("B_railing", "#C9CCD0", roughness=0.45, metallic=0.5)
    mat["alpha_clip"], mat["double_sided"] = 0.5, 1
    mat.use_backface_culling = False
    path = os.path.join(E.TEXTURES, "railing_cutout_albedo.png")
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    if os.path.exists(path) and not any(n.type == "TEX_IMAGE" for n in nodes):
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(path, check_existing=True)
        tex.extension = "EXTEND"
        bsdf = nodes.get("Principled BSDF")
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    return mat


# ----------------------------------------------------------------------------------------------------------------
# small geometry helpers
# ----------------------------------------------------------------------------------------------------------------
def ccw(ring):
    area = sum(ax * by - bx * ay for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]))
    return list(ring) if area > 0 else list(ring)[::-1]


def rect(P, s0, s1, t0, t1):
    return ccw([P(s0, t0), P(s1, t0), P(s1, t1), P(s0, t1)])


def lerp2(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def facing(a, b, out):
    """(a, b) ordered so that the right-hand normal of a -> b points towards the point `out`."""
    nx, ny = b[1] - a[1], -(b[0] - a[0])
    return (a, b) if nx * (out[0] - a[0]) + ny * (out[1] - a[1]) > 0 else (b, a)


def right_normal(a, b):
    d = dist(a, b) or 1.0
    return ((b[1] - a[1]) / d, -(b[0] - a[0]) / d)


def centroid(ring):
    return (sum(p[0] for p in ring) / len(ring), sum(p[1] for p in ring) / len(ring))


def grow(ring, d):
    """Ring scaled about its centroid so that its points move about d metres outwards (simple shapes only)."""
    c = centroid(ring)
    out = []
    for p in ring:
        r = dist(p, c) or 1.0
        out.append((p[0] + (p[0] - c[0]) / r * d, p[1] + (p[1] - c[1]) / r * d))
    return out


def densify(line, step, closed=False):
    pts = list(line) + ([line[0]] if closed else [])
    out = []
    for a, b in zip(pts, pts[1:]):
        n = max(1, int(math.ceil(dist(a, b) / step)))
        out += [lerp2(a, b, k / n) for k in range(n)]
    if not closed:
        out.append(pts[-1])
    return out


def route_blockers(ring, pad=12.0):
    """Route points near a footprint: (x, y, floor z), for env_buildings.split_around (facade openings)."""
    xs, ys = [p[0] for p in ring], [p[1] for p in ring]
    return [(p["x"], p["y"], p["track_z"]) for p in E.route_points()
            if min(xs) - pad <= p["x"] <= max(xs) + pad and min(ys) - pad <= p["y"] <= max(ys) + pad]


class Mesh:
    """bmesh with the module's material slots; UVs in metres are set in finish()."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.tile = self.bm.faces.layers.float.new("tile")       # glass: metres per window_band tile (4 panes)
        self.fixed = self.bm.faces.layers.int.new("uv_fixed")    # 1 = UVs were given explicitly
        self._shared = {}

    def vert(self, p, shared=False):
        if not shared:
            return self.bm.verts.new(p)
        key = (round(p[0], 3), round(p[1], 3), round(p[2], 3))
        if key not in self._shared:
            self._shared[key] = self.bm.verts.new(p)
        return self._shared[key]

    def face(self, pts, mat, tile=6.0, uvs=None, shared=False):
        """Face from 3D points, counter-clockwise seen from its visible side."""
        vs = [self.vert(p, shared) for p in pts]
        if len(set(vs)) < 3:
            return None
        try:
            f = self.bm.faces.new(vs)
        except ValueError:
            return None
        f.material_index = mat
        f[self.tile] = tile
        if uvs:
            f[self.fixed] = 1
            for loop, c in zip(f.loops, uvs):
                loop[self.uv].uv = c
        return f

    def up(self, pts, mat, **kw):
        """Face from 3D points, turned so that it faces up."""
        n = Vector((0, 0, 0))
        for a, b in zip(pts, pts[1:] + pts[:1]):
            n += Vector(a).cross(Vector(b))
        return self.face(pts if n.z > 0 else pts[::-1], mat, **kw)

    def down(self, pts, mat, **kw):
        n = Vector((0, 0, 0))
        for a, b in zip(pts, pts[1:] + pts[:1]):
            n += Vector(a).cross(Vector(b))
        return self.face(pts if n.z < 0 else pts[::-1], mat, **kw)

    def towards(self, pts, mat, target, **kw):
        """Face turned so that it faces the 3D point `target`."""
        n = Vector((0, 0, 0))
        for a, b in zip(pts, pts[1:] + pts[:1]):
            n += Vector(a).cross(Vector(b))
        c = sum((Vector(p) for p in pts), Vector((0, 0, 0))) / len(pts)
        return self.face(pts if n.dot(Vector(target) - c) > 0 else pts[::-1], mat, **kw)

    def wall(self, a, b, z0, z1, mat, off=0.0, z0b=None, z1b=None, **kw):
        """Vertical quad over a -> b (2D), visible from the right-hand side of a -> b (outside of a ccw ring)."""
        nx, ny = right_normal(a, b)
        a, b = (a[0] + nx * off, a[1] + ny * off), (b[0] + nx * off, b[1] + ny * off)
        z0b = z0 if z0b is None else z0b
        z1b = z1 if z1b is None else z1b
        return self.face([(*a, z0), (*b, z0b), (*b, z1b), (*a, z1)], mat, **kw)

    def prism(self, ring, z0, z1, side=WALL, top=ROOF, bottom=None, skip=(), **kw):
        """Extruded ccw ring. z1: a number or one height per ring point. skip: edge indices without a wall."""
        ring = ccw(ring)
        n = len(ring)
        tops = z1 if isinstance(z1, (list, tuple)) else [z1] * n
        for k in range(n):
            if k in skip or side is None:
                continue
            j = (k + 1) % n
            self.wall(ring[k], ring[j], z0, tops[k], side, z1b=tops[j], **kw)
        if top is not None:
            self.face([(*p, z) for p, z in zip(ring, tops)], top)
        if bottom is not None:
            self.face([(*p, z0) for p in ring][::-1], bottom)

    def box(self, c, u, hu, hv, z0, z1, side=WALL, top=None, bottom=None):
        """Box centred at c (2D), half-size hu along the unit vector u and hv across it."""
        v = (-u[1], u[0])
        ring = [(c[0] + u[0] * a + v[0] * b, c[1] + u[1] * a + v[1] * b) for a, b in ((-hu, -hv), (hu, -hv), (hu, hv), (-hu, hv))]
        self.prism(ring, z0, z1, side, top if top is not None else side, bottom)

    def column(self, c, r, z0, z1, mat=WALL, n=8):
        ring = [(c[0] + r * math.cos(2 * math.pi * k / n), c[1] + r * math.sin(2 * math.pi * k / n)) for k in range(n)]
        self.prism(ring, z0, z1, mat, None)

    def glass(self, a, b, lo, hi, off=0.06, tile=6.0, mat=GLASS, blocked=None, bay=None, pier=0.6, inset=0.0):
        """Glazing strip on the wall a -> b (visible from its right-hand side), optionally in bays with white piers
        left between them, minus the parts in front of a passage opening (blocked: route points)."""
        length = dist(a, b)
        if length < 0.5 + 2 * inset:
            return
        nx, ny = right_normal(a, b)
        a = (a[0] + nx * off, a[1] + ny * off)
        b = (b[0] + nx * off, b[1] + ny * off)
        a, b = lerp2(a, b, inset / length), lerp2(a, b, 1 - inset / length)
        length -= 2 * inset
        pieces = [(a, b)]
        if bay:
            count = max(1, int(round(length / bay)))
            step = length / count
            pieces = [(lerp2(a, b, (k * step + pier / 2) / length), lerp2(a, b, ((k + 1) * step - pier / 2) / length))
                      for k in range(count)]
        if blocked:
            import env_buildings
            pieces = [q for p0, p1 in pieces for q in env_buildings.split_around(p0, p1, lo, hi, blocked)]
        for p0, p1 in pieces:
            self.face([(*p0, lo), (*p1, lo), (*p1, hi), (*p0, hi)], mat, tile=tile)

    def railing(self, line, h=1.1, module=3.0):
        """Bar railing along a 3D polyline (alpha-clipped sheets, one texture module per about 3 m)."""
        for a, b in zip(line, line[1:]):
            length = math.dist(a, b)
            if length < 0.4:
                continue
            n = max(1, int(round(length / module)))
            for k in range(n):
                p = tuple(a[i] + (b[i] - a[i]) * k / n for i in range(3))
                q = tuple(a[i] + (b[i] - a[i]) * (k + 1) / n for i in range(3))
                self.face([p, q, (q[0], q[1], q[2] + h), (p[0], p[1], p[2] + h)], RAIL,
                          uvs=[(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)])

    def finish(self, name):
        bm, uv = self.bm, self.uv
        bm.normal_update()
        w, h = E.X1 - E.X0, E.Y1 - E.Y0
        for f in bm.faces:
            if f[self.fixed]:
                continue
            m, n = f.material_index, f.normal
            if m == GROUND:
                for loop in f.loops:
                    loop[uv].uv = ((loop.vert.co.x - E.X0) / w, (loop.vert.co.y - E.Y0) / h)
                continue
            t = TILE_M.get(m, 3.0)
            if abs(n.z) > 0.7:
                for loop in f.loops:
                    loop[uv].uv = (loop.vert.co.x / t, loop.vert.co.y / t)
                continue
            d = math.hypot(n.x, n.y) or 1.0
            tx, ty = -n.y / d, n.x / d
            if m == GLASS:
                zs = [l.vert.co.z for l in f.loops]
                lo, hi = min(zs), max(zs)
                for loop in f.loops:
                    co = loop.vert.co
                    loop[uv].uv = ((co.x * tx + co.y * ty) / f[self.tile], (co.z - lo) / max(hi - lo, 0.01))
            else:
                for loop in f.loops:
                    co = loop.vert.co
                    loop[uv].uv = ((co.x * tx + co.y * ty) / t, co.z / t)
        used = sorted({f.material_index for f in bm.faces})
        remap = {old: new for new, old in enumerate(used)}
        for f in bm.faces:
            f.material_index = remap[f.material_index]
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        mats = slot_materials()
        for old in used:
            mesh.materials.append(mats[old])
        return mesh


def _key(p):
    return (round(p[0], 3), round(p[1], 3))


def _with_points(ring, extra):
    """ring with every point of `extra` that lies on one of its edges inserted there (shared vertices, no T-joints)."""
    out = []
    have = {_key(p) for p in ring}
    for a, b in zip(ring, ring[1:] + ring[:1]):
        out.append(a)
        on = [p for p in extra if _key(p) not in have and C.seg_distance(p[0], p[1], a[0], a[1], b[0], b[1]) < 0.004]
        on.sort(key=lambda p: dist(a, p))
        for p in on:
            have.add(_key(p))
            out.append(p)
    return out


def stepped_solid(m, lower, upper, z0, zs, z1, side=WALL, top=ROOF, soffit=WALL, ledge=None, bottom=WALL, under=None):
    """One closed, watertight solid: the ring `lower` from z0 to zs and the ring `upper` from zs to z1 on top of it
    (an overhanging roof slab, a deck on a set-back ground floor). Where upper sticks out the underside gets `soffit`,
    where lower sticks out its top gets `ledge`. Used for volumes that are cut by a boolean afterwards.
    under: {point of upper: z} lifts the underside at those points (a cantilever whose soffit rises to the tip)."""
    lower, upper = ccw(lower), ccw(upper)
    lo, up = _with_points(lower, upper), _with_points(upper, lower)
    lift = {_key(p): z for p, z in (under or {}).items()}
    zu = lambda p: lift.get(_key(p), zs)                                       # noqa: E731
    for p, q in zip(lo, lo[1:] + lo[:1]):
        m.face([(*p, z0), (*q, z0), (*q, zs), (*p, zs)], side, shared=True)
    for p, q in zip(up, up[1:] + up[:1]):
        m.face([(*p, zu(p)), (*q, zu(q)), (*q, z1), (*p, z1)], side, shared=True)
    m.face([(*p, z1) for p in up], top, shared=True)
    if bottom is not None:
        m.face([(*p, z0) for p in lo][::-1], bottom, shared=True)
    pts = []
    index = {}
    for p in lo + up:
        if _key(p) not in index:
            index[_key(p)] = len(pts)
            pts.append(p)
    edges = [(index[_key(p)], index[_key(q)]) for ring in (lo, up) for p, q in zip(ring, ring[1:] + ring[:1])]
    edges = list({tuple(sorted(e)) for e in edges if e[0] != e[1]})
    verts, _e, faces, *_ = delaunay_2d_cdt([Vector(p) for p in pts], edges, [], 0, 1e-4)
    for tri in faces:
        if len(tri) != 3:
            continue
        a, b, c = (tuple(verts[i]) for i in tri)
        cx, cy = (a[0] + b[0] + c[0]) / 3, (a[1] + b[1] + c[1]) / 3
        in_lo, in_up = C.inside(cx, cy, lo), C.inside(cx, cy, up)
        if in_up and not in_lo:
            m.down([(*a, zu(a)), (*b, zu(b)), (*c, zu(c))], soffit, shared=True)
        elif in_lo and not in_up:
            m.up([(*a, zs), (*b, zs), (*c, zs)], ledge if ledge is not None else top, shared=True)


# ----------------------------------------------------------------------------------------------------------------
# keep-out rings for the dressing (filled while building, read by dress())
# ----------------------------------------------------------------------------------------------------------------
KEEP_OUT = []


def keep_out(ring, pad=0.0):
    KEEP_OUT.append(grow(ring, pad) if pad else list(ring))


def route_room(x, y, want, clear=ROUTE_CLEAR):
    """How far (m, at most `want`) something may reach from (x, y) towards the race line and stay `clear` off it."""
    return max(0.0, min(want, E.distance_to_route(x, y) - clear))


def berm(m, line, out, rise, width=7.0, mat=GROUND, step=2.5, back=0.3):
    """Grass bank against a wall: along the wall line (2D polyline) the ground rises by `rise` metres (a number or
    f(point)) at the wall and meets the terrain `width` metres out on the side of the point `out`. The foot keeps
    ROUTE_CLEAR off the race line and dips 6 cm under the terrain."""
    pts = densify(line, step)
    rows = []
    for k, p in enumerate(pts):
        a, b = pts[max(k - 1, 0)], pts[min(k + 1, len(pts) - 1)]
        a, b = facing(a, b, out)
        nx, ny = right_normal(a, b)
        w = width
        while w > 0.0 and E.distance_to_route(p[0] + nx * w, p[1] + ny * w) < ROUTE_CLEAR:
            w -= 0.5                                         # the foot keeps off the race line
        w = max(w, 0.0)
        if E.distance_to_route(p[0], p[1]) < ROUTE_CLEAR + 0.5:
            w = 0.0
        foot = (p[0] + nx * w, p[1] + ny * w)
        r = rise(p) if callable(rise) else rise
        g_wall, g_foot = E.ground_z(p[0], p[1]), E.ground_z(*foot)
        top = max(g_wall, g_foot) + r * min(1.0, w / max(width, 0.01))
        rows.append(((p[0] - nx * back, p[1] - ny * back, top), (foot[0], foot[1], g_foot - 0.06), w))
    for (t0, f0, w0), (t1, f1, w1) in zip(rows, rows[1:]):
        if w0 < 1.0 and w1 < 1.0:
            continue
        m.up([t0, f0, f1, t1], mat)
    ring = [r[0][:2] for r in rows] + [r[1][:2] for r in rows][::-1]
    keep_out(ring)


# ----------------------------------------------------------------------------------------------------------------
# main building: Gallery spine (I) + Hovione Atrium (A)
# ----------------------------------------------------------------------------------------------------------------
def in_main_body(x, y):
    """True inside the walls of the main building below the roof slab (not under its overhang): the interior
    dressing of the drive-through hall (env_interior) stays inside this."""
    return C.inside(x, y, C.MAIN_BODY)


def in_hall(p):
    """Route point inside a drive-through hall that gets columns, glazing and lights (env_interior.dress)."""
    if p["building"] == "Edifício Principal":
        return in_main_body(p["x"], p["y"])
    return p["building"] != "CTT Student Hall"


def build_main(b):
    P = C.MAIN
    vol, det = Mesh(), Mesh()
    base = min(E.ground_z(x, y) for x, y in C.MAIN_ROOF) - 1.0
    zs, zr = C.MAIN_SOFFIT_Z, C.MAIN_ROOF_Z
    stepped_solid(vol, C.MAIN_BODY, C.MAIN_ROOF, base, zs, zr, WALL, ROOF, WALL)
    blocked = route_blockers(C.MAIN_ROOF)
    g0, g1 = F1 + 0.15, zs - 0.1          # the tall glass walls of Floor 1
    deep, shallow, (vb0, vb1) = C.GALLERY_DEEP, C.GALLERY_SHALLOW, C.GALLERY_BREAK

    # --- Gallery, east side: glass wall under the roof overhang, deck, Floor-0 arcade, fins and columns ---
    det.glass(P(deep, 2.3), P(deep, vb0 - 0.3), g0, g1, tile=12.0)
    det.glass(P(shallow, vb1 + 0.3), P(shallow, 139.7), g0, g1, tile=12.0)
    for k, v in enumerate((66.0, 69.0, 75.0, 78.0, 81.0)):          # a few tinted panes at eye height (our own order)
        det.glass(P(deep, v + 0.1), P(deep, v + 2.9), F1 + 0.9, F1 + 2.1, off=0.1, mat=(GLASS_ROOF, SOLAR)[k % 2])
    det.glass(P(deep, 2.3), P(deep, 52.0), F0 + 0.1, F1 - 0.6, mat=DARK)           # Main Deck arcade (Floor 0)
    det.glass(P(shallow, vb1 + 0.3), P(shallow, 139.7), F0 + 0.1, F1 - 0.6, mat=DARK)
    # Floor-1 deck / plinth. V10 integration: north of the stairs' void it reaches 0.4 m past the roof edge, under
    # which V10i's plaza deck ends (the OSM outline): the 0.6 m between the two was an open groove down to the terrain
    import plaza_plan
    vs = C.MAIN_UV(*plaza_plan.EDGE_W)[1]
    for ring in (rect(P, deep - 0.2, 29.4, 2.0, vs), rect(P, deep - 0.2, 30.4, vs, vb0),
                 rect(P, shallow - 0.2, 30.4, vb1, 140.0)):
        det.prism(ring, F1 - 0.5, F1 + 0.1, WALL, SAND, WALL)
    run = []                                                  # railing where the deck stands above the ground
    for k in range(52):
        v = 2.2 + k * 2.0
        if E.ground_z(*P(30.6, v)) < F1 - 0.9:
            run.append((*P(29.3, v), F1 + 0.1))
        elif run:
            break
    if len(run) > 1:
        det.railing(run)
    v = 8.0
    while v < vb0 - 1:
        c = P(29.0, v)
        if E.ground_z(*c) < F1 - 1.2:                                              # arcade column under the deck
            det.box(c, C.MAIN_U, 0.25, 0.25, E.ground_z(*c) - 0.3, F1 - 0.5)
        det.box(P(deep + 1.2, v), C.MAIN_U, 0.14, 0.14, F1 + 0.1, zs)              # slender column at the glass
        a, e, d = P(deep + 0.05, v - 0.1), P(29.9, v - 0.1), P(deep + 0.05, v + 0.1)
        f = P(29.9, v + 0.1)                                                       # tapered truss fin under the soffit
        det.towards([(*a, zs), (*e, zs), (*a, zs - 1.6)], WALL, (*P(deep + 2, v - 3), zs - 0.5))
        det.towards([(*d, zs), (*f, zs), (*d, zs - 1.6)], WALL, (*P(deep + 2, v + 3), zs - 0.5))
        det.down([(*a, zs - 1.6), (*d, zs - 1.6), (*f, zs), (*e, zs)], WALL)
        v += 6.0
    v = vb1 + 5.0
    while v < 139.0:
        det.box(P(shallow + 0.7, v), C.MAIN_U, 0.12, 0.12, F1 + 0.1, zs)
        v += 6.0

    # --- west side: glass above the courtyards, the amphitheatre and the terrace; the entrance front ---
    west = lambda v: 0.0 if v <= 97.1 else (0.1 * (v - 97.1) / 16.5 if v <= 113.6 else 0.1 + 0.8 * (v - 113.6) / 28.6)   # noqa: E731
    for v0, v1, low in ((53.4, 29.9, True), (59.0, 53.8, False), (96.8, 85.5, True), (113.4, 97.3, False),
                        (142.2, 113.9, True)):
        a, e = P(west(v0), v0), P(west(v1), v1)
        det.glass(a, e, g0, g1, tile=12.0, blocked=blocked)
        if low:
            det.glass(a, e, F0 + 0.1, F1 - 0.6, mat=DARK)
    eu, (ev0, ev1) = C.ENTRANCE_U, C.ENTRANCE_V
    det.glass(P(eu, ev1 - 0.3), P(eu, ev0 + 0.3), g0, g1, tile=12.0, blocked=blocked)
    det.glass(P(0.9, ev1 - 0.4), P(0.9, ev0 + 0.4), zs + 0.35, zr - 0.75, off=0.04, mat=DARK)
    for v in (ev0 + 0.6, ev0 + 5.5, ev1 - 5.0, ev1 - 0.6):                         # white trusses carrying the roof edge
        c = P(1.7, v)
        if E.distance_to_route(*c) > ROUTE_CLEAR:
            det.box(c, C.MAIN_U, 0.14, 0.14, F1, zs)

    # --- atrium block: north, east and south faces ---
    for a, e in ((P(69.0, 210.0), P(1.2, 210.0)), (P(69.3, 142.3), P(69.3, 209.7))):
        det.glass(a, e, F1 + 0.5, F1 + 3.5, blocked=blocked, bay=6.0)
        det.glass(a, e, F2 + 0.9, F2 + 2.5, blocked=blocked, inset=0.6)
    a, e = P(42.1, 142.0), P(69.0, 142.0)                                          # south-east wing on columns
    det.glass(a, e, F1 + 0.2, F1 + 3.5, mat=DARK, blocked=blocked)
    det.glass(a, e, F2 + 0.9, F2 + 2.5, blocked=blocked, inset=0.6)
    u = 45.0
    while u < 69.0:
        c = P(u, 141.1)
        if E.distance_to_route(*c) > ROUTE_CLEAR:
            det.column(c, 0.25, F1 - 0.2, F1 + 3.7)
        u += 6.0
    det.prism(rect(P, 59.5, 64.5, 141.4, 142.0), F2 + 0.3, F2 + 3.0, WALL, WALL, WALL)      # projecting framed window
    det.glass(P(60.0, 141.4), P(64.0, 141.4), F2 + 0.7, F2 + 2.6, off=0.03, mat=DARK)
    det.prism(rect(P, 69.3, 74.3, 178.6, 188.0), F1 + 3.9, F1 + 4.25, WALL, ROOF, WALL)     # canopy over the north exit

    # --- roof: the glass field as a lantern with a steel grid, solar rows ---
    u0, v0, u1, v1 = C.GLASS_FIELD_UV
    det.prism(rect(P, u0, u1, v0, v1), zr - 0.02, zr + 0.6, WALL, GLASS_ROOF)
    for k in range(10):
        u = u0 + (u1 - u0) * k / 9
        det.up([(*P(u - 0.07, v0), zr + 0.66), (*P(u + 0.07, v0), zr + 0.66), (*P(u + 0.07, v1), zr + 0.66),
                (*P(u - 0.07, v1), zr + 0.66)], STEEL)
    for k in range(13):
        v = v0 + (v1 - v0) * k / 12
        det.up([(*P(u0, v - 0.07), zr + 0.64), (*P(u1, v - 0.07), zr + 0.64), (*P(u1, v + 0.07), zr + 0.64),
                (*P(u0, v + 0.07), zr + 0.64)], STEEL)
    for ua, ub in C.SOLAR_ROWS_UV:
        for va, vb in C.SOLAR_RUNS_V:
            v = va
            while v + 5.5 <= vb:
                ring = rect(P, ua, ub, v, v + 5.5)
                det.prism(ring, zr + 0.02, zr + 0.36, STEEL, None)
                det.face([(*p, zr + 0.36) for p in ring], SOLAR,
                         uvs=[tuple(c / 2.0 for c in C.MAIN_UV(*p)) for p in ring])
                v += 6.0
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


# ----------------------------------------------------------------------------------------------------------------
# B and D: the west wings
# ----------------------------------------------------------------------------------------------------------------
def wing(vol, det, P, body, storey, slab, s_lawn, bands, ribbon_faces, low_faces=()):
    """A west wing in its frame P(s, t): Floor-1 body with ribbons, planted roof strips, set-back glazed roof storey
    under a thin slab. bands: [(t0, t1, material)] across the roof; ribbon_faces: the t of the two long facades;
    low_faces: t of a facade that also shows Floor 0 (dark glazing between white piers)."""
    s0, s1, t0, t1 = body
    ring = rect(P, s0, s1, t0, t1)
    base = min(E.ground_z(x, y) for x, y in ring) - 1.0
    top = C.WING_TOP
    vol.prism(ring, base, top, WALL, None)
    for ta, tb, mat in bands:
        vol.up([(*P(s0, ta), top), (*P(s1, ta), top), (*P(s1, tb), top), (*P(s0, tb), top)], mat)
    mid = (t0 + t1) / 2
    for t in ribbon_faces:
        out = P((s0 + s1) / 2, t + (t - mid))
        a, e = facing(P(s0 + 0.4, t), P(s1 - 0.4, t), out)
        det.glass(a, e, F1 + 0.4, F1 + 3.4, tile=5.0)
        if t in low_faces:
            det.glass(a, e, F0 + 0.3, F1 - 0.6, mat=DARK, bay=5.0, pier=0.5)
    a0, a1, b0, b1 = storey
    vol.prism(rect(P, a0, a1, b0, b1), top, C.WING_STOREY_TOP, GLASS, None, tile=5.0)
    c0, c1, d0, d1 = slab
    vol.prism(rect(P, c0, c1, d0, d1), C.WING_STOREY_TOP, C.WING_STOREY_TOP + 0.4, WALL, ROOF, WALL)
    det.railing([(*P(s0 + 0.5, s_lawn), top), (*P(s1 - 0.5, s_lawn), top)])


def build_B(b):
    P = C.B
    vol, det = Mesh(), Mesh()
    s0, s1, t0, t1 = C.B_BODY_ST
    wing(vol, det, P, C.B_BODY_ST, C.B_STOREY_ST, C.B_SLAB_ST, t0 + 0.15,
         [(t0, t0 + 0.8, WALL), (t0 + 0.8, C.B_SLAB_ST[2], PLANT), (C.B_SLAB_ST[2], C.B_SLAB_ST[3], ROOF),
          (C.B_SLAB_ST[3], t1 - 0.8, PLANT), (t1 - 0.8, t1, WALL)], (t0, t1))
    # the head: blank white kite with the portal hood on its south-east corner, dark roofed link to the bar
    base = min(E.ground_z(x, y) for x, y in C.B_HEAD) - 1.0
    vol.prism(C.B_HEAD, base, C.HEAD_TOP, WALL, WALL)
    vol.prism(C.B_LINK, base, C.WING_TOP + 0.5, WALL, DARKROOF)
    hood = ccw(C.B_HOOD)
    sill = F1 - 0.2
    tops = [22.0 if p[1] < 62.0 else (21.6 if p[1] < 65.0 else C.HEAD_TOP + 0.05) for p in hood]
    front = [k for k in range(len(hood)) if hood[k][1] < 62.0 and hood[(k + 1) % len(hood)][1] < 62.0]
    vol.prism(hood, base, tops, WALL, WALL, skip=front)
    a, e = facing(hood[front[0]], hood[(front[0] + 1) % len(hood)], (-55.0, 50.0))
    nx, ny = right_normal(a, e)
    ia, ie = (a[0] - nx * 2.5, a[1] - ny * 2.5), (e[0] - nx * 2.5, e[1] - ny * 2.5)       # recess 2.5 m deep
    pa, pe = lerp2(a, e, 0.1), lerp2(a, e, 0.9)                                           # the frame's jambs
    qa, qe = lerp2(ia, ie, 0.1), lerp2(ia, ie, 0.9)
    head = sill + 6.0
    vol.wall(a, pa, base, 22.0, WALL)
    vol.wall(pe, e, base, 22.0, WALL)
    vol.wall(pa, pe, head, 22.0, WALL)
    vol.wall(pa, pe, base, sill, WALL)
    vol.face([(*qa, sill), (*qe, sill), (*qe, head), (*qa, head)], DARK)                  # dark glazed entrance
    vol.towards([(*pa, sill), (*qa, sill), (*qa, head), (*pa, head)], WALL, (*lerp2(pa, pe, 0.5), sill + 3))
    vol.towards([(*pe, sill), (*qe, sill), (*qe, head), (*pe, head)], WALL, (*lerp2(pa, pe, 0.5), sill + 3))
    vol.up([(*pa, sill), (*pe, sill), (*qe, sill), (*qa, sill)], CONC)
    vol.down([(*pa, head), (*pe, head), (*qe, head), (*qa, head)], WALL)
    for k in range(4):                                                                    # steps down to the lawn
        d = 0.35 * (k + 1)
        sa, se = (pa[0] + nx * d, pa[1] + ny * d), (pe[0] + nx * d, pe[1] + ny * d)
        det.prism(ccw([pa, pe, se, sa]), base, sill - 0.17 * (k + 1), CONC, CONC)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


def build_D(b):
    P = C.D
    vol, det = Mesh(), Mesh()
    s0, s1, t0, t1 = C.D_BODY_ST
    wing(vol, det, P, C.D_BODY_ST, C.D_STOREY_ST, C.D_SLAB_ST, t1 - 0.15,
         [(t0, t0 + 0.8, WALL), (t0 + 0.8, C.D_SLAB_ST[2], PLANT), (C.D_SLAB_ST[2], C.D_SLAB_ST[3], ROOF),
          (C.D_SLAB_ST[3], t1 - 0.8, PLANT), (t1 - 0.8, t1, WALL)], (t0, t1), low_faces=(t0,))
    # low planted terrace over Floor 0 along the south side
    p0, p1, q0, q1 = C.D_PODIUM_ST
    ring = rect(P, p0, p1, q0, q1)
    base = min(E.ground_z(x, y) for x, y in ring) - 1.0
    vol.prism(ring, base, F1, WALL, PLANT)
    a, e = facing(P(p0 + 0.4, q1), P(p1 - 0.4, q1), P((p0 + p1) / 2, q1 + 10))
    det.glass(a, e, F0 + 0.5, F0 + 2.3, tile=5.0, bay=10.0, pier=1.2)
    # the head: white prism cantilevering over a dark glazed base that stands in the pool
    base = min(E.ground_z(x, y) for x, y in C.D_HEAD) - 1.0
    under = F0 + 1.5
    stepped_solid(vol, C.D_HEAD_BASE, C.D_HEAD, base, under, C.HEAD_TOP, WALL, WALL, WALL,
                  under={C.D_HEAD[1]: under + 4.6, C.D_HEAD[2]: under + 4.6, C.D_HEAD[4]: under + 1.0})                    # the soffit rises to the tips (D01, C04)
    hb = ccw(C.D_HEAD_BASE)
    for a, e in zip(hb[1:6], hb[2:7]):                               # glazing on the faces towards the pool
        det.glass(a, e, C.D_POOL_Z + 0.25, under - 0.15, mat=DARK)
    vol.prism(C.D_LINK, C.WING_TOP, C.HEAD_TOP + 0.3, WALL, DARKROOF)
    a, e = (-48.1, -10.1), (-33.3, -22.9)                            # two large framed panes on the south-west face
    det.glass(lerp2(a, e, 0.16), lerp2(a, e, 0.46), F1 + 0.9, F1 + 3.9, off=0.05, mat=DARK)
    det.glass(lerp2(a, e, 0.54), lerp2(a, e, 0.84), F1 + 0.9, F1 + 3.9, off=0.05, mat=DARK)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


# ----------------------------------------------------------------------------------------------------------------
# E  Executive Hall
# ----------------------------------------------------------------------------------------------------------------
E_RING = [(45.1, -25.4), (42.3, -20.7), (34.6, -22.7), (34.5, -27.9), (22.6, -38.5), (27.7, -45.7), (32.8, -44.0),
          (29.3, -54.8), (37.9, -60.0), (39.9, -61.1), (47.4, -54.5), (53.9, -57.9), (60.8, -52.2), (59.4, -49.9),
          (78.7, -36.0), (67.6, -8.4), (46.8, -20.3), (48.6, -23.3)]          # OSM without the sliver along the ramp
E_UPPER = [[(36.8, -27.8), (36.1, -42.7), (42.0, -50.9), (54.7, -47.9), (58.4, -39.7), (51.0, -28.5), (42.8, -20.9)],
           [(51.0, -27.8), (59.2, -38.9), (75.4, -28.6), (68.1, -17.3)]]


def build_E(b):
    vol, det = Mesh(), Mesh()
    ring = ccw(E_RING)
    base = min(E.ground_z(x, y) for x, y in ring) - 1.0
    vol.prism(ring, base, F1, WALL, ROOF)
    for a, e in zip(ring, ring[1:] + ring[:1]):
        if dist(a, e) > 6.0:
            det.glass(a, e, F0 + 0.7, F0 + 2.5, inset=0.6)
    for upper in E_UPPER:
        upper = ccw(upper)
        vol.prism(upper, F1, F2 + 0.3, WALL, DARKROOF)
        vol.prism(grow(upper, 0.25), F2 + 0.3, F2 + 0.55, WALL, None)            # white parapet rim
        for a, e in zip(upper, upper[1:] + upper[:1]):
            if dist(a, e) > 5.0:
                det.glass(a, e, F1 + 0.9, F1 + 2.9, inset=0.6)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


# ----------------------------------------------------------------------------------------------------------------
# F  Grand Auditorium: faceted white monolith
# ----------------------------------------------------------------------------------------------------------------
def build_F(b):
    vol, det = Mesh(), Mesh()
    A, B_, Cw = (122.3, -22.0, 26.0), (130.5, -20.2, 25.0), (122.6, -32.7, 22.8)
    Dw, Ew, Fw = (118.7, -50.2, 19.5), (109.0, -66.2, 16.5), (114.2, -70.7, 16.5)
    F2w, Gw, Gs = (113.0, -75.8, 16.0), (112.6, -77.5, 15.5), (143.3, -84.8, 15.5)
    Je, He, H2 = (147.3, -70.3, 16.5), (156.3, -37.2, 17.5), (153.6, -36.2, 17.8)
    Ie, Sw, Se = (131.8, -28.0, 23.0), (120.8, -70.3, 19.0), (139.6, -71.8, 19.0)
    outline = [A, Cw, Dw, Ew, Fw, F2w, Gw, Gs, Je, He, H2, Ie, B_]            # ccw
    base = min(E.ground_z(p[0], p[1]) for p in outline) - 1.0
    lift = F0 + 6.0                                                           # lower edge of the wall at the corner
    for a, e in zip(outline, outline[1:] + outline[:1]):
        undercut = (a, e) in ((A, Cw), (B_, A))              # the wall's lower edge rises to the corner here
        za = lift if undercut and a in (A, B_) else base
        ze = lift if undercut and e in (A, B_) else base
        vol.face([(a[0], a[1], za), (e[0], e[1], ze), e, a], WALL)
    for tri in ((A, Sw, Se), (A, Se, B_),                                      # the long central facet (ridge)
                (A, Cw, Sw), (Cw, Dw, Sw), (Dw, Ew, Sw), (Ew, Fw, Sw),         # west facet
                (B_, Se, Ie), (Ie, Se, H2), (H2, Se, He), (He, Se, Je),        # east facet
                (Sw, Fw, F2w), (Sw, F2w, Gw), (Sw, Gw, Gs), (Sw, Gs, Se), (Se, Gs, Je)):   # south facet
        vol.up(list(tri), WALL)
    # undercut north-west corner: dark glazed entrance 2.5 m behind the wall line, white soffit over it
    Ai, Bi = (124.1, -24.4), (131.0, -22.9)
    mid = (126.0, -21.0, F0 + 2.0)
    vol.towards([(*Ai, base), (Cw[0], Cw[1], base), (*Ai, lift)], DARK, (118.0, -27.0, F0 + 2))
    vol.towards([(*Ai, base), (*Bi, base), (*Bi, lift), (*Ai, lift)], DARK, mid)
    vol.towards([(*Bi, base), (B_[0], B_[1], base), (B_[0], B_[1], lift), (*Bi, lift)], WALL, mid)
    vol.down([(A[0], A[1], lift), (*Ai, lift), (Cw[0], Cw[1], base)], WALL)
    vol.down([(A[0], A[1], lift), (B_[0], B_[1], lift), (*Bi, lift), (*Ai, lift)], WALL)
    for k in range(5):                                                         # skylights on the south facet
        c = lerp2((123.5, -73.6), (137.5, -75.0), k / 4)
        det.box(c, (0.995, -0.1), 1.1, 0.6, 17.6, 18.55, WALL, GLASS_ROOF)
    berm(det, [Cw[:2], Dw[:2], Ew[:2]], (100.0, -45.0), 2.6, width=7.0)        # grass bank against the west wall
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


# ----------------------------------------------------------------------------------------------------------------
# G  Horizon Hall / Westmont Hospitality Hall (the gym is its Floor 0)
# ----------------------------------------------------------------------------------------------------------------
G_UNDER = [(112.7, 5.8), (125.8, 9.6), (131.0, -4.1), (147.2, 5.3), (151.2, 2.35)]    # set-back Floor-0 front
G_GYM = [(151.2, 2.35), (167.6, 11.8), (150.6, 41.0), (145.9, 38.2), (127.8, 27.6), (139.6, 32.6), (149.2, 8.8)]


def build_G(b):
    vol, det = Mesh(), Mesh()
    ring = ccw(b["points"])
    k1 = min(range(len(ring)), key=lambda k: dist(ring[k], (114.1, 1.4)))
    k5 = min(range(len(ring)), key=lambda k: dist(ring[k], (167.6, 11.8)))
    lower, k = [], (k5 % len(ring))
    while k != k1:                                   # from the east corner round the north and west sides
        lower.append(ring[k])
        k = (k + 1) % len(ring)
    lower += G_UNDER                                 # then the set-back front instead of the south-west corners
    ground = C.LOWER_PLAZA_Z
    base = min([E.ground_z(x, y) for x, y in ring] + [ground]) - 1.0
    stepped_solid(vol, lower, ring, base, F1 - 0.5, F1, WALL, SAND, WALL)
    for a, e in zip(G_UNDER, G_UNDER[1:]):
        det.glass(a, e, F0 + 0.15, F1 - 0.6, mat=DARK)
    front = [ring[k1], ring[(k1 + 1) % len(ring)], ring[(k1 + 2) % len(ring)], ring[(k1 + 3) % len(ring)],
             lerp2(ring[(k1 + 3) % len(ring)], ring[k5], 24.0 / dist(ring[(k1 + 3) % len(ring)], ring[k5]))]
    cen = centroid(ring)
    for a, e in zip(front, front[1:]):                                          # round columns under the deck edge
        n = max(1, int(round(dist(a, e) / 6.0)))
        for j in range(n + (1 if e is front[-1] else 0)):
            p = lerp2(a, e, j / n)
            d = dist(p, cen)
            c = (p[0] + (cen[0] - p[0]) / d * 0.7, p[1] + (cen[1] - p[1]) / d * 0.7)
            if E.distance_to_route(*c) > ROUTE_CLEAR + 1.0:
                det.column(c, 0.25, min(E.ground_z(*c), ground) - 0.3, F1 - 0.5)
    det.railing([(*p, F1) for p in front])
    # cream box over a glazed Floor-1 front
    cream = ccw(C.G_CREAM)
    vol.prism(grow(cream, -0.4), F1, F1 + 3.3, GLASS, None)
    vol.prism(cream, F1 + 3.3, F1 + 9.1, CREAM, ROOF, CREAM)
    # glazed block with a balcony band, flying roof on two tall columns
    block = ccw(C.G_BLOCK)
    vol.prism(block, F1, F2 - 0.3, GLASS, None)
    balcony = ccw(C.G_BALCONY)
    vol.prism(balcony, F2 - 0.3, F2 + 0.2, WALL, SAND, WALL)
    vol.prism(block, F2 + 0.2, 25.4, GLASS, None)
    inner = grow(balcony, -0.15)
    det.railing([(*inner[3], F2 + 0.2), (*inner[0], F2 + 0.2), (*inner[1], F2 + 0.2)])
    slab = grow(ccw(C.G_ROOF), 0.5)
    vol.prism(slab, 25.4, 26.0, WALL, ROOF, WALL)
    inner = grow(slab, -0.3)
    det.railing([(*inner[3], 26.0), (*inner[0], 26.0), (*inner[1], 26.0)])
    for c in C.G_COLUMNS:
        det.column(c, 0.3, F1, 25.4)
    # the gym's roof east of the block
    gym = ccw(G_GYM)
    vol.prism(gym, F1 - 0.5, F2, WALL, ROOF)
    for a, e in zip(gym, gym[1:] + gym[:1]):
        if dist(a, e) > 8.0 and not C.inside(*lerp2(a, e, 0.5), grow(ccw(C.G_ROOF), 0.5)):
            det.glass(a, e, F1 + 0.9, F1 + 2.5, inset=0.6)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


def build_SC(b):
    vol, det = Mesh(), Mesh()
    ring = ccw(b["points"])
    base = min(E.ground_z(x, y) for x, y in ring) - 1.0
    top = F1 + 7.1
    vol.prism(ring, base, top, WALL, ROOF)
    vol.prism(ring, top, top + 0.4, WALL, None)
    blocked = route_blockers(ring)
    for a, e in zip(ring, ring[1:] + ring[:1]):
        mid = lerp2(a, e, 0.5)
        nx, ny = right_normal(a, e)
        if dist(a, e) < 5.0 or C.inside(mid[0] + nx * 1.0, mid[1] + ny * 1.0, C.MAIN_ROOF):
            continue
        det.glass(a, e, F1 + 0.4, F1 + 3.2, blocked=blocked, bay=5.0, inset=0.3)
        det.glass(a, e, F2 + 0.9, F2 + 2.5, blocked=blocked, inset=0.6)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


OWNED = {C.MAIN_ID: build_main, C.B_ID: build_B, C.D_ID: build_D, C.E_ID: build_E, C.F_ID: build_F,
         C.G_ID: build_G, C.SC_ID: build_SC}


def owns(b):
    return b["id"] in OWNED


def build_one(b, index):
    volume, details = OWNED[b["id"]](b)
    print(f"  campus {b['name'] or b['id']}: {sum(len(p.vertices) - 2 for p in volume.polygons)} + "
          f"{sum(len(p.vertices) - 2 for p in details.polygons)} triangles")
    return volume, details


# ----------------------------------------------------------------------------------------------------------------
# Grounds: trays (courtyards, D's pool), the levelled lower plaza, pools, walls, steps
# ----------------------------------------------------------------------------------------------------------------
TRAYS = [
    {"name": "north courtyard", "ring": C.COURT_N, "z": C.COURT_Z, "rim": None},
    {"name": "south courtyard", "ring": C.COURT_S, "z": C.COURT_Z, "rim": None},
    {"name": "pool of D", "ring": C.D_POOL, "z": C.D_POOL_Z - 0.6, "rim": C.D_POOL_Z + 0.1},
]
_G = {"cells": set(), "tris": [], "grid": {}}


def _cell(x, y):
    return int(math.floor((x - E.X0) / E.STEP)), int(math.floor((y - E.Y0) / E.STEP))


def covers_cell(cx, cy):
    """True if the 5 m terrain face centred at (cx, cy) is replaced by the campus apron (build_world.build_ground)."""
    return _cell(cx, cy) in _G["cells"]


def lower_nodes(heights):
    """env_terrain.apply: the 5 m nodes of the lower plaza east of the race line go to Floor 0 (they rose to
    13-14 m, so Horizon Hall stood half buried). Full effect from 14 m off the centreline, none within 7 m: the
    road bed and its shoulder keep the heights the route gives them."""
    import plaza_plan
    n = 0
    for j in range(E.ROWS):
        for i in range(E.COLS):
            x, y = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            if not (100 < x < 160 and -40 < y < 20) or not C.inside(x, y, C.LOWER_PLAZA):
                continue
            d = E.distance_to_route(x, y)
            t = min(max((d - 7.0) / 7.0, 0.0), 1.0)
            t = t * t * (3 - 2 * t)
            if plaza_plan.owns_route_point(E.nearest_route_point(x, y)):
                t = 1.0        # V10 integration: beside V10i's stairs there is no road bed to keep (the stairs stand in
                               # a void at Floor 0), so the ground under Horizon Hall's podium is level up to them
            new = heights[j][i] * (1 - t) + C.LOWER_PLAZA_Z * t
            if abs(new - heights[j][i]) > 0.01:
                heights[j][i] = new
                n += 1
    print(f"  campus: {n} terrain nodes of the lower plaza levelled to {C.LOWER_PLAZA_Z:.2f} m")


def _segments_cross(p, q, a, b):
    def side(u, v, w):
        return (v[0] - u[0]) * (w[1] - u[1]) - (v[1] - u[1]) * (w[0] - u[0])
    d1, d2, d3, d4 = side(a, b, p), side(a, b, q), side(p, q, a), side(p, q, b)
    return (d1 > 0) != (d2 > 0) and (d3 > 0) != (d4 > 0)


def _touches_cell(ring, x0, y0, step, pad):
    """True if the polygon `ring` comes within `pad` of the square cell at (x0, y0): a corner of the padded square
    inside the ring, a ring point inside the padded square, or a ring edge crossing one of its sides."""
    ax, ay, bx, by = x0 - pad, y0 - pad, x0 + step + pad, y0 + step + pad
    sq = [(ax, ay), (bx, ay), (bx, by), (ax, by)]
    if any(C.inside(x, y, ring) for x, y in sq) or any(ax <= x <= bx and ay <= y <= by for x, y in ring):
        return True
    return any(_segments_cross(p, q, a, b) for p, q in zip(ring, ring[1:] + ring[:1]) for a, b in zip(sq, sq[1:] + sq[:1]))


def plan_ground():
    """Chooses the 5 m cells each tray replaces and makes E.ground_z return the tray floors and the apron
    (call after env_corridor.build_data and the other surface installers)."""
    import env_corridor
    import env_terrain
    cells = set()
    for tray in TRAYS:
        ring = tray["ring"]
        xs, ys = [p[0] for p in ring], [p[1] for p in ring]
        own = set()
        for i in range(int((min(xs) - E.X0) // E.STEP) - 1, int((max(xs) - E.X0) // E.STEP) + 2):
            for j in range(int((min(ys) - E.Y0) // E.STEP) - 1, int((max(ys) - E.Y0) // E.STEP) + 2):
                x0, y0 = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
                samples = [(x0 + E.STEP * a / 4, y0 + E.STEP * c / 4) for a in range(5) for c in range(5)]
                if not _touches_cell(ring, x0, y0, E.STEP, 0.15):
                    continue
                bad = [s for s in samples if env_corridor.has_face(*s)]
                cx, cy = x0 + E.STEP / 2, y0 + E.STEP / 2
                if bad or env_corridor.covers_cell(cx, cy) or env_terrain.in_portal_hole(cx, cy):
                    raise RuntimeError(f"campus tray '{tray['name']}' touches the track corridor at cell {i},{j} "
                                       f"({x0}, {y0}): move its outline")
                own.add((i, j))
        tray["cells"] = own
        cells |= own
    _G["cells"] = cells
    _G["tris"] = _apron_triangles()
    grid = {}
    for tri in _G["tris"]:
        for key in {_cell(x, y) for x, y, _z in tri} | {_cell(sum(p[0] for p in tri) / 3, sum(p[1] for p in tri) / 3)}:
            grid.setdefault(key, []).append(tri)
        xs, ys = [p[0] for p in tri], [p[1] for p in tri]
        for i in range(_cell(min(xs), 0)[0], _cell(max(xs), 0)[0] + 1):
            for j in range(_cell(0, min(ys))[1], _cell(0, max(ys))[1] + 1):
                if tri not in grid.setdefault((i, j), []):
                    grid[(i, j)].append(tri)
    _G["grid"] = grid
    prev = E._surface["fn"] or E.coarse_ground_z

    def surface(x, y):
        key = _cell(x, y)
        if key not in _G["cells"]:
            return prev(x, y)
        for tray in TRAYS:
            if C.inside(x, y, tray["ring"]):
                return tray["z"]
        for a, b, c in _G["grid"].get(key, ()):
            den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
            if abs(den) < 1e-9:
                continue
            w0 = ((b[1] - c[1]) * (x - c[0]) + (c[0] - b[0]) * (y - c[1])) / den
            w1 = ((c[1] - a[1]) * (x - c[0]) + (a[0] - c[0]) * (y - c[1])) / den
            if w0 >= -1e-6 and w1 >= -1e-6 and w0 + w1 <= 1 + 1e-6:
                return w0 * a[2] + w1 * b[2] + (1 - w0 - w1) * c[2]
        return prev(x, y)
    E.set_surface(surface)
    print(f"  campus: {len(TRAYS)} trays replace {len(cells)} cells of the 5 m grid ({len(_G['tris'])} apron triangles)")


def _apron_triangles():
    """Triangles between the outline of every tray and the edge of the cells it replaces, at terrain height on the
    grid nodes and at the tray's rim height (or the natural ground) on its outline."""
    cells = _G["cells"]
    node = lambda i, j: (E.X0 + i * E.STEP, E.Y0 + j * E.STEP)                  # noqa: E731
    pts, index, heights, edges = [], {}, {}, set()

    def add(p, z):
        k = _key(p)
        if k not in index:
            index[k] = len(pts)
            pts.append(p)
            heights[k] = z
        return index[k]
    for i, j in cells:
        corners = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
        ids = [add(node(a, c), E.coarse_ground_z(*node(a, c))) for a, c in corners]
        for k, (di, dj) in enumerate(((0, -1), (1, 0), (0, 1), (-1, 0))):       # edge to a cell that stays: fixed
            if (i + di, j + dj) not in cells:
                edges.add(tuple(sorted((ids[k], ids[(k + 1) % 4]))))
    for tray in TRAYS:
        ring = densify(ccw(tray["ring"]), 2.5, closed=True)
        ids = [add(p, tray["rim"] if tray["rim"] is not None else E.coarse_ground_z(*p)) for p in ring]
        for a, c in zip(ids, ids[1:] + ids[:1]):
            if a != c:
                edges.add(tuple(sorted((a, c))))
    verts, _e, faces, *_ = delaunay_2d_cdt([Vector(p) for p in pts], list(edges), [], 0, 1e-4)
    out = []
    for tri in faces:
        if len(tri) != 3:
            continue
        a, b, c = (tuple(verts[i]) for i in tri)
        cx, cy = (a[0] + b[0] + c[0]) / 3, (a[1] + b[1] + c[1]) / 3
        if _cell(cx, cy) not in cells or any(C.inside(cx, cy, t["ring"]) for t in TRAYS):
            continue
        out.append(tuple((p[0], p[1], heights.get(_key(p), E.coarse_ground_z(p[0], p[1]))) for p in (a, b, c)))
    return out


def build_ground_mesh():
    """The apron around the trays and the tray floors that are ground (courtyards), ground-map material."""
    if not _G["tris"]:
        return
    m = Mesh()
    for tri in _G["tris"]:
        m.up(list(tri), GROUND)
    for tray in TRAYS:
        if tray["rim"] is None:
            m.face([(*p, tray["z"]) for p in ccw(tray["ring"])], GROUND)
    mesh = m.finish("World_GroundCampus")
    E.new_object("World_GroundCampus", mesh, E.collection("World_Ground"))
    print(f"  campus ground: {sum(len(p.vertices) - 2 for p in mesh.polygons)} triangles")


def _rim(m, ring, z_top, width, z_low, mat=CONC):
    """Flat-topped rim wall along a ccw ring: `width` wide, centred on the ring, from z_low up to z_top."""
    outer, inner = grow(ring, width / 2), grow(ring, -width / 2)
    n = len(ring)
    for k in range(n):
        j = (k + 1) % n
        m.up([(*outer[k], z_top), (*outer[j], z_top), (*inner[j], z_top), (*inner[k], z_top)], mat)
        m.wall(outer[k], outer[j], z_low, z_top, mat)
        m.wall(inner[j], inner[k], z_low, z_top, mat)


def build_extras():
    """Everything of the campus that is not a building: pools, courtyard walls, the stair cheeks at the terrace's
    west end, G's seat steps, the berms at the heads. One object, collection World_Buildings_Campus."""
    m = Mesh()
    # --- D's reflecting pool: white rim, still water ---
    pool = ccw(C.D_POOL)
    rim = C.D_POOL_Z + 0.1
    _rim(m, pool, rim, 0.45, C.D_POOL_Z - 0.6)
    m.face([(*p, C.D_POOL_Z) for p in grow(pool, -0.2)], WATER)
    keep_out(pool, 1.0)
    # --- A's entrance pool: raised concrete rim, water flush with it ---
    apool = _clear_of_route(ccw(C.A_POOL), 6.0)
    if apool:
        g = min(E.ground_z(x, y) for x, y in apool)
        top = max(E.ground_z(x, y) for x, y in apool) + 0.45
        _rim(m, apool, top, 0.5, g - 0.3)
        m.face([(*p, top - 0.04) for p in grow(apool, -0.2)], WATER)
        keep_out(apool, 1.0)
    # --- courtyards: perforated buff brick walls where no building closes them ---
    n_ring = ccw(C.COURT_N)
    for a, e, top in ((n_ring[4], n_ring[0], C.COURT_Z + 2.0), (n_ring[0], n_ring[1], C.COURT_Z + 1.1),
                      (n_ring[3], n_ring[4], C.COURT_Z + 2.4)):
        _wall_strip(m, a, e, C.COURT_Z - 0.4, top, 0.35, BRICK)
    s_ring = ccw(C.COURT_S)
    _wall_strip(m, s_ring[0], s_ring[1], C.COURT_Z - 1.6, C.COURT_Z + 1.2, 0.35, BRICK)
    # planted strip between the north courtyard's wall and B's facade
    a, e = n_ring[3], n_ring[4]
    nx, ny = right_normal(a, e)                       # ring is ccw: right-hand side = outside = towards B
    m.up([(*a, C.COURT_Z + 2.4), (*e, C.COURT_Z + 2.4), (e[0] + nx * 2.6, e[1] + ny * 2.6, F1 - 0.4),
          (a[0] + nx * 2.6, a[1] + ny * 2.6, F1 - 0.4)], PLANT)
    terrace_stair(m)
    # --- G's terracotta seat steps (south of G, rising east from the lower plaza) ---
    s0, s1, s2, s3 = C.G_STEPS                         # s0-s3 = foot line (west), s1-s2 = top line (east)
    tiers = 8
    for k in range(tiers):
        fa, fb = lerp2(s0, s1, k / tiers), lerp2(s3, s2, k / tiers)
        ring = ccw([fa, s1, s2, fb])
        m.prism(ring, C.LOWER_PLAZA_Z - 0.6, C.LOWER_PLAZA_Z + (k + 1) * (F1 - 0.3 - C.LOWER_PLAZA_Z) / tiers,
                TERRA, TERRA)
    keep_out(C.G_STEPS, 1.0)
    # --- berms: the lawn rises against the blank heads of B and D's bar ---
    berm(m, [C.B_HEAD[0], C.B_HEAD[1], (-58.5, 62.9)], (-75.0, 55.0), 3.4, width=8.0)
    berm(m, [(-58.4, 60.2), (-51.6, 60.4)], (-55.0, 50.0), 2.6, width=6.0)          # in front of the portal hood
    berm(m, [C.B_HEAD[9], C.B_HEAD[0]], (-80.0, 90.0), 3.4, width=7.0)
    berm(m, [C.B(0.0, 2.0), C.B(51.0, 2.0)], C.B(25.0, -10.0), 2.4, width=5.5)
    mesh = m.finish("World_CampusGrounds")
    E.new_object("World_CampusGrounds", mesh, E.collection("World_Buildings_Campus"))
    print(f"  campus grounds: {sum(len(p.vertices) - 2 for p in mesh.polygons)} triangles")


STAIR_SIDE = 5.7          # m from the centreline: inner face of the cheek walls beside the jump (parapet gap: 5.5)
STAIR_SPLAY = 0.07        # the cheeks open outwards by this much per metre of flight
STAIR_STEPS = 22


def terrace_stair(m):
    """West end of the CTT roof terrace (survey C; C04, C05): the real wide stair down to the pad lies where the
    game jumps off the deck, so only its flanks are built, outside the jump corridor: a narrow flight with white
    concrete cheeks on the north side and the sloped concrete surface on the south side. Both start at the deck
    edge beyond the parapet gap (5.5 m) and open outwards along the flight."""
    pts = E.route_points()
    j = E.ROUTE["jump"]["index"]
    o, f = pts[j], pts[j + 3]
    d = dist((o["x"], o["y"]), (f["x"], f["y"]))
    dx, dy = (f["x"] - o["x"]) / d, (f["y"] - o["y"]) / d
    Q = C.frame((o["x"], o["y"]), (dx, dy))                 # Q(along the flight, to the left of it)
    # V10 integration: the flanks start at the deck's level; the jump point itself is the kicker's lip since V10i
    top, low = min(pts[k]["track_z"] for k in range(j - 8, j + 1)), f["track_z"]
    going = 0.77
    z_at = lambda a: top - (top - low) * min(max((a - 0.3) / (STAIR_STEPS * going), 0.0), 1.0)   # noqa: E731
    end = 0.3 + STAIR_STEPS * going

    def cheek(l0, l1, a0, a1, sign, rail=True):
        """Wall between lateral offsets l0 < l1 (on the side `sign`), top 0.25 m above the flight line."""
        rows = []
        for k in range(7):
            a = a0 + (a1 - a0) * k / 6
            s = STAIR_SPLAY * a
            rows.append((Q(a, sign * (l0 + s)), Q(a, sign * (l1 + s)), z_at(a) + 0.25))
        for (i0, o0, z0), (i1, o1, z1) in zip(rows, rows[1:]):
            g = min(E.ground_z(*i0), E.ground_z(*i1), E.ground_z(*o0), E.ground_z(*o1)) - 0.4
            m.up([(*i0, z0), (*i1, z1), (*o1, z1), (*o0, z0)], CONC)
            for a_, b_ in ((i0, i1), (o1, o0)):
                p, q = facing(a_, b_, lerp2(lerp2(a_, b_, 0.5), lerp2(lerp2(i0, i1, 0.5), lerp2(o0, o1, 0.5), 0.5), -5.0))
                za, zb = (z0, z1) if p == a_ else (z1, z0)
                m.face([(*p, g), (*q, g), (*q, zb), (*p, za)], WALL)
        for (i0, o0, z0) in (rows[0], rows[-1]):
            g = min(E.ground_z(*i0), E.ground_z(*o0)) - 0.4
            m.towards([(*i0, g), (*o0, g), (*o0, z0), (*i0, z0)], WALL,
                      (*Q(a0 - 5 if (i0, o0, z0) == rows[0] else a1 + 5, sign * (l0 + l1) / 2), z0))
        if rail:
            m.railing([(*lerp2(i, o_, 0.5), z) for i, o_, z in rows], h=0.9)

    # north side: flight between two cheeks
    n0, n1 = STAIR_SIDE + 0.3, STAIR_SIDE + 2.5
    cheek(STAIR_SIDE, n0, -0.3, end, -1)
    cheek(n1, n1 + 0.3, -0.3, end, -1)
    for k in range(STAIR_STEPS):
        a0, a1 = 0.3 + k * going, 0.3 + (k + 1) * going
        z = top - (k + 1) * (top - low) / STAIR_STEPS
        c = [Q(a0, -(n0 + STAIR_SPLAY * a0)), Q(a1, -(n0 + STAIR_SPLAY * a1)), Q(a1, -(n1 + STAIR_SPLAY * a1)),
             Q(a0, -(n1 + STAIR_SPLAY * a0))]
        m.up([(*p, z) for p in c], CONC)
        m.towards([(*c[0], z), (*c[3], z), (*c[3], z + (top - low) / STAIR_STEPS), (*c[0], z + (top - low) / STAIR_STEPS)],
                  CONC, (*Q(a0 + 5, -(n0 + n1) / 2), z + 2))
    c = [Q(-0.6, -n0), Q(0.3, -n0), Q(0.3, -n1), Q(-0.6, -n1)]
    m.up([(*p, top) for p in c], CONC)                                          # landing at deck level
    # south side: sloped concrete surface with a cheek towards the jump and a railing on its outer edge
    s0, s1 = STAIR_SIDE + 0.3, STAIR_SIDE + 3.2
    cheek(STAIR_SIDE, s0, 1.2, end + 1.2, 1, rail=False)
    rows = []
    for k in range(7):
        a = 1.2 + (end - 0.0) * k / 6
        s = STAIR_SPLAY * a
        rows.append((Q(a, s0 + s), Q(a, s1 + s), z_at(a - 0.9)))
    for (i0, o0, z0), (i1, o1, z1) in zip(rows, rows[1:]):
        g = min(E.ground_z(*o0), E.ground_z(*o1)) - 0.4
        m.up([(*i0, z0), (*i1, z1), (*o1, z1), (*o0, z0)], CONC)
        p, q = facing(o0, o1, lerp2(o0, i0, -5.0))
        za, zb = (z0, z1) if p == o0 else (z1, z0)
        m.face([(*p, g), (*q, g), (*q, zb), (*p, za)], WALL)
    m.railing([(*lerp2(i, o_, 0.95), z) for i, o_, z in rows], h=1.0)
    keep_out([Q(-1.0, STAIR_SIDE - 0.2), Q(end + 2.5, STAIR_SIDE - 0.2), Q(end + 2.5, s1 + 2.5), Q(-1.0, s1 + 1.0)])
    keep_out([Q(-1.0, -STAIR_SIDE + 0.2), Q(-1.0, -n1 - 1.0), Q(end + 2.5, -n1 - 2.5), Q(end + 2.5, -STAIR_SIDE + 0.2)])


def _wall_strip(m, a, e, z0, z1, thick, mat):
    nx, ny = right_normal(a, e)
    h = thick / 2
    ring = ccw([(a[0] + nx * h, a[1] + ny * h), (e[0] + nx * h, e[1] + ny * h),
                (e[0] - nx * h, e[1] - ny * h), (a[0] - nx * h, a[1] - ny * h)])
    m.prism(ring, z0, z1, mat, mat)


def _clear_of_route(ring, clear):
    """The ring pulled back from the race line: every point at least `clear` from the centreline (moved straight
    away from the nearest route point); None if less than half its area is left."""
    out = []
    for x, y in densify(ring, 1.5, closed=True):
        for _ in range(4):
            p = E.nearest_route_point(x, y)
            d = math.hypot(x - p["x"], y - p["y"])
            if d >= clear:
                break
            k = (clear - d + 0.02) / (d or 1.0)
            x, y = x + (x - p["x"]) * k, y + (y - p["y"]) * k
        if not out or dist(out[-1], (x, y)) > 0.2:
            out.append((x, y))
    return out if len(out) >= 4 else None


COURT_GROUPS = [(7.0, 72.0), (17.5, 78.5), (27.0, 83.5), (12.0, 39.5), (24.0, 44.0), (36.5, 48.0)]


def court_furniture(veg):
    """A few parasol tables on the floors of the two sunken courtyards (existing prototypes, fixed spots; a spot
    moves 1.5 m at a time until it stands 2 m clear of the trees)."""
    out = []
    for k, (x, y) in enumerate(COURT_GROUPS):
        for _ in range(6):
            if all(math.hypot(p["x"] - x, p["y"] - y) > 2.2 for p in veg):
                break
            x += 1.5
        if not (C.inside(x, y, C.COURT_N) or C.inside(x, y, C.COURT_S)):
            continue
        z, turn = C.COURT_Z, 0.5 * k
        out.append({"proto": "Prop_Parasol", "x": round(x, 3), "y": round(y, 3), "z": z, "rot_z": 0.0, "scale": 1.0})
        tx, ty = x + 0.9 * math.cos(turn), y + 0.9 * math.sin(turn)
        out.append({"proto": "Prop_BistroTable", "x": round(tx, 3), "y": round(ty, 3), "z": z, "rot_z": 0.0, "scale": 1.0})
        for j in range(3):
            a = turn + 2.1 * j + 1.0
            out.append({"proto": "Prop_BistroChair", "x": round(tx + 0.75 * math.cos(a), 3),
                        "y": round(ty + 0.75 * math.sin(a), 3), "z": z, "rot_z": round(a + math.pi, 4), "scale": 1.0})
    return out


def dress(veg, props):
    """Drops the generated plants and props that stand in the pools, on the berms and steps or on the lower plaza
    (every other placement stays as it is) and rewrites the dressing files."""
    import json
    rings = [(min(p[0] for p in r), min(p[1] for p in r), max(p[0] for p in r), max(p[1] for p in r), r) for r in KEEP_OUT]

    def out(x, y):
        return any(b[0] <= x <= b[2] and b[1] <= y <= b[3] and C.inside(x, y, b[4]) for b in rings)
    veg2, k = [], 0
    for p in veg:
        if out(p["x"], p["y"]) or C.inside(p["x"], p["y"], C.LOWER_PLAZA):
            continue
        if C.inside(p["x"], p["y"], C.COURT_N) or C.inside(p["x"], p["y"], C.COURT_S):
            k += 1
            if k % 2:                                      # every second generated tree in the courtyards goes
                continue
        veg2.append(p)
    props2 = [p for p in props if not out(p["x"], p["y"])]
    gone = sorted(f"{p['proto']}@{p['x']:.0f},{p['y']:.0f}" for p in props if out(p["x"], p["y"]))
    print(f"  campus dressing: {len(veg) - len(veg2)} plants and {len(props) - len(props2)} props removed "
          f"(pools, berms, steps, lower plaza): {', '.join(gone)}")
    props2 = props2 + court_furniture(veg2)
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
