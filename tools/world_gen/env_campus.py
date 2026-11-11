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
 RAIL, LETTER, UMBER, SLAT, SETT, TILES) = range(22)          # V15b: the last five
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
            railing_material(),
            # V15b: raised lettering, Student Central's cladding, slat panel, dark setts, roof tiles (existing name)
            E.material("B_letter_grey", "#8C8C8A", roughness=0.55, metallic=0.25),
            E.material("B_wall_umber", "#593126", 0.9),      # V16: SC's dark red-brown render (survey #402823 in the sun)
            E.material("B_slat_anthracite", "#3B3E41", 0.8),
            E.material("B_sett_dark", "#5F5C56", 0.9),
            E.material("B_roof_tiles", "#C8664A", 0.85)]


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
    entrance_frame(det, P, blocked, zs, zr)        # V15b: white space frame, lattice screen, mullions (was a dark band)
    end_wall_letters(det, P)                       # V15b: raised letters on the white end wall
    gallery_lattice(det, P, zs, zr)                # V15b: lattice fascia over the terrace's east end

    # --- atrium block: north, east and south faces ---
    # V15b: the east face's generic glazing ends where the north exit colonnade begins
    for a, e in ((P(69.0, 210.0), P(1.2, 210.0)), (P(69.3, 142.3), P(69.3, C.COLONNADE_V[0] - 0.3))):
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
    north_colonnade(det, P, zs)                    # V15b: colonnade under one white storey (was a canopy slab)

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
    """V15b (N_p03_dropoff_h190): Student Central is a dark blank angular pavilion, 6-8 m high, with one projecting
    framed window on its north-east face (V10c2 built a white two-storey box after V10h).
    V16 (photospheres N03_h180, N03_h225, N02_h135): the render is dark red-brown (#402823; B_wall_umber was a
    brown-grey that read black), and the window is a box of the same render, about 4 m wide and 3.5 m high with a
    thick frame, standing 1.1 m proud, dark glass set back in it (V15b: a white frame of 3.2 x 2.6 m)."""
    vol, det = Mesh(), Mesh()
    ring = ccw(C.SC_RING)
    tops = [F1 + h for h in C.SC_TOPS]
    base = min(E.ground_z(x, y) for x, y in ring) - 1.0
    vol.prism(ring, base, tops, UMBER, None)
    vol.face([(*p, base) for p in ring][::-1], UMBER)
    c = centroid(ring)
    zc = sum(tops) / len(tops) + 0.5
    n = len(ring)
    for k in range(n):                                   # faceted roof: a fan to a point above the middle
        j = (k + 1) % n
        vol.up([(*ring[k], tops[k]), (*ring[j], tops[j]), (*c, zc)], DARKROOF)
    a, e = ring[4], ring[5]                              # the face towards the drop-off plaza
    length = dist(a, e)
    nx, ny = right_normal(a, e)
    half, proud, bar = 2.0, 1.1, 0.32
    w0, w1 = lerp2(a, e, 0.5 - half / length), lerp2(a, e, 0.5 + half / length)
    at = lambda p, d: (p[0] + nx * d, p[1] + ny * d)     # noqa: E731
    z0, z1 = F1 + 0.9, F1 + 4.4
    box = ccw([at(w0, -0.2), at(w1, -0.2), at(w1, proud), at(w0, proud)])
    det.prism(box, z0, z1, UMBER, UMBER, UMBER)
    p, q = facing(at(w0, proud), at(w1, proud), at(w0, proud + 5.0))
    det.glass(p, q, z0 + bar, z1 - bar, off=0.03, inset=bar, mat=DARK)
    note_clear("Student Central pavilion", ring + [at(w0, proud), at(w1, proud)])
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


OWNED = {C.MAIN_ID: build_main, C.B_ID: build_B, C.D_ID: build_D, C.E_ID: build_E, C.F_ID: build_F,
         C.G_ID: build_G, C.SC_ID: build_SC}
# (V15b adds Milestone J and the two apartment blocks at the end of this file)


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
    build_v14b_extras(m)                               # V15b: bin enclosure, drop-off ring, clearance report
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


# ================================================================================================================
# --- V15b (2026-11-11): from the photosphere survey V15a (atlas report, campus_sv/P_*, N_*, luc_1..5) ---
# West entrance (space frame, lattice screen, raised letters on the end wall), Milestone J, north exit colonnade,
# drop-off ring, bin enclosure, the two apartment blocks, lattice fascia at the Gallery's west face.
# Every new solid notes its smallest distance to the race centreline (CLEAR_LOG, printed by clearance_report()).
# ================================================================================================================
CLEAR_LOG = []
_SEGS = []


def route_gap(x, y):
    """Exact distance (m) from (x, y) to the race centreline (the closed polyline through the route points)."""
    if not _SEGS:
        pts = [(p["x"], p["y"]) for p in E.route_points()]
        _SEGS.extend(zip(pts, pts[1:] + pts[:1]))
    return min(C.seg_distance(x, y, a[0], a[1], b[0], b[1]) for a, b in _SEGS)


def note_clear(name, pts, overhead=None):
    """Records the smallest centreline distance of a new solid (2D points of its footprint). overhead: its lowest
    point above the road in metres, for things that hang over the road and do not stand on the ground."""
    gap, at = min((route_gap(p[0], p[1]), p) for p in pts)
    CLEAR_LOG.append((name, gap, at, overhead))
    return gap


def clearance_report():
    print("  V15b clearance (smallest distance of each new solid to the race centreline):")
    for name, gap, at, overhead in CLEAR_LOG:
        extra = f", lowest point {overhead:.1f} m above the road" if overhead is not None else ""
        print(f"    {name:<44s} {gap:6.2f} m at ({at[0]:.1f}, {at[1]:.1f}){extra}")
        if overhead is None and gap < ROUTE_CLEAR:
            raise RuntimeError(f"V15b: '{name}' stands {gap:.2f} m from the race line (limit {ROUTE_CLEAR} m)")


def member(m, a, b, t, mat=WALL):
    """Square steel member (side t) from a to b (3D): four side faces, no caps."""
    a, b = Vector(a), Vector(b)
    d = b - a
    if d.length < 1e-4:
        return
    d.normalize()
    ref = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
    s = d.cross(ref).normalized() * (t / 2)
    w = d.cross(s).normalized() * (t / 2)
    corners = [s + w, -s + w, -s - w, s - w]
    mid = (a + b) / 2
    for k in range(4):
        c0, c1 = corners[k], corners[(k + 1) % 4]
        m.towards([tuple(a + c0), tuple(b + c0), tuple(b + c1), tuple(a + c1)], mat, tuple(mid + (c0 + c1) * 9))


def truss_layer(m, at, v0, v1, z0, z1, bays, t=0.14, flip=False):
    """Flat truss between two chords: verticals at every bay line, diagonals in a W pattern.
    at(v, z) -> 3D point on the truss plane. Returns the bay lines (v)."""
    lines = [v0 + (v1 - v0) * k / bays for k in range(bays + 1)]
    member(m, at(v0, z0), at(v1, z0), t)
    member(m, at(v0, z1), at(v1, z1), t)
    for v in lines:
        member(m, at(v, z0), at(v, z1), t * 0.8)
    for k, (va, vb) in enumerate(zip(lines, lines[1:])):
        lo, hi = (z0, z1) if (k % 2 == 0) != flip else (z1, z0)
        member(m, at(va, lo), at(vb, hi), t * 0.8)
    return lines


def lattice_screen(m, P, u, lines, z0, z1, name, t=0.11):
    """Full-height white lattice (posts with X bracing in two levels) in the bays between `lines`, only where a bay
    keeps ROUTE_CLEAR + 0.2 off the race line: over the drive-through it stays open."""
    zm = (z0 + z1) / 2
    posts, built = set(), []
    for va, vb in zip(lines, lines[1:]):
        probe = [P(u, va + (vb - va) * k / 4) for k in range(5)]
        if min(route_gap(*p) for p in probe) < ROUTE_CLEAR + 0.2:
            continue
        built += probe
        for v in (va, vb):
            if round(v, 3) not in posts:
                posts.add(round(v, 3))
                member(m, (*P(u, v), z0), (*P(u, v), z1), t * 1.3)
        member(m, (*P(u, va), zm), (*P(u, vb), zm), t)
        for lo, hi in ((z0, zm), (zm, z1)):
            member(m, (*P(u, va), lo), (*P(u, vb), hi), t * 0.8)
            member(m, (*P(u, va), hi), (*P(u, vb), lo), t * 0.8)
    if built:
        note_clear(name, built)
    return len(posts)


def entrance_frame(det, P, blocked, zs, zr):
    """West entrance (P_p00_h090, luc_2, luc_3): the roof edge over the glass front is a white steel space frame.
    Two truss planes (W pattern) 2 m apart with struts and plan bracing between them stand in front of the roof
    slab from just above the hall opening to the roof (nothing lower than the passage cut, 7 m), a fine white grid
    on dark glass behind them; below, the full-height lattice screen in the bays beside the drive-through, and
    thin white mullions on the glass front (tall panes)."""
    import env_buildings
    eu, (ev0, ev1) = C.ENTRANCE_U, C.ENTRANCE_V
    v0, v1 = ev0 + 0.2, ev1 - 0.2
    z0, z1 = zs + 0.15, zr - 0.1                       # 21.75 .. 24.4: the hall opening below ends at 21.5
    back, front, bays = 0.72, -1.3, 10
    lines = truss_layer(det, lambda v, z: (*P(front, v), z), v0, v1, z0, z1, bays)
    truss_layer(det, lambda v, z: (*P(back, v), z), v0, v1, z0, z1, bays, flip=True)
    for k, v in enumerate(lines):                      # struts between the two planes, bracing in the top plane
        member(det, (*P(back, v), z0), (*P(front, v), z0), 0.1)
        member(det, (*P(back, v), z1), (*P(front, v), z1), 0.1)
        if k + 1 < len(lines):
            a, e = (back, front) if k % 2 == 0 else (front, back)
            member(det, (*P(a, v), z1), (*P(e, lines[k + 1]), z1), 0.09)
            member(det, (*P(a, v), z0), (*P(e, lines[k + 1]), z0), 0.09)
    note_clear("entrance space frame (roof edge)", [P(front, v) for v in lines] + [P(back, v) for v in lines],
               overhead=z0 - F1)
    # dark glass with a fine white grid on the slab edge behind the frame
    det.glass(P(0.9, ev1 - 0.3), P(0.9, ev0 + 0.3), zs + 0.1, zr - 0.15, off=0.04, mat=DARK)
    west = (*P(-30.0, (ev0 + ev1) / 2), zs)
    fine = [v0 + (v1 - v0) * k / (bays * 2) for k in range(bays * 2 + 1)]
    for v in fine:
        det.towards([(*P(0.83, v - 0.035), z0), (*P(0.83, v + 0.035), z0), (*P(0.83, v + 0.035), z1),
                     (*P(0.83, v - 0.035), z1)], WALL, west)
    for z in (z0 + (z1 - z0) / 3, z0 + (z1 - z0) * 2 / 3):
        det.towards([(*P(0.83, v0), z - 0.035), (*P(0.83, v1), z - 0.035), (*P(0.83, v1), z + 0.035),
                     (*P(0.83, v0), z + 0.035)], WALL, west)
    # lattice screen under the frame, beside the drive-through only
    lattice_screen(det, P, back, lines, F1, z0, "entrance lattice screen")
    # mullions on the glass front: tall panes, one transom at door height
    g0, g1, um = F1 + 0.15, zs - 0.1, eu - 0.1
    for v in lines[1:-1]:
        if route_gap(*P(eu, v)) < E.ROUTE_WIDTH / 2 + 1.8:
            continue                                   # the glass is open there (passage)
        det.towards([(*P(um, v - 0.05), g0), (*P(um, v + 0.05), g0), (*P(um, v + 0.05), g1), (*P(um, v - 0.05), g1)],
                    WALL, west)
    for a, e in env_buildings.split_around(P(um, ev1 - 0.3), P(um, ev0 + 0.3), F1 + 2.9, F1 + 3.05, blocked):
        det.towards([(*a, F1 + 2.9), (*e, F1 + 2.9), (*e, F1 + 3.05), (*a, F1 + 3.05)], WALL, west)


def end_wall_letters(det, P):
    """The four letters on the west face of the white end wall (Luc cleared the lettering with the professor and
    Nova, 2026-11-11): real 3D geometry, raised LETTER_OUT from the wall, in the HUD's own display font
    (campus_plan.nova_letters: polygons, not a texture and not traced from the sign), with one thin line of small
    raised bars under them (no readable text)."""
    letters = C.nova_letters()
    scale = C.LETTER_H / max(l["bbox"][3] for l in letters if l["char"] != "O")
    widths = [(l["bbox"][2] - l["bbox"][0]) * scale for l in letters]
    total = sum(widths) + C.LETTER_GAP * (len(letters) - 1)
    wall_u, out = 0.9, C.LETTER_OUT
    v_left = C.LETTER_V + total / 2                    # seen from the west the word runs north -> south (v falls)
    used = []

    def world(lx, ly, d):
        return (*P(wall_u - d, v_left - lx), C.LETTER_Z + ly)
    x = 0.0
    for l, w in zip(letters, widths):
        x0 = x - l["bbox"][0] * scale
        pts = [(x0 + vx * scale, vy * scale) for vx, vy in l["verts"]]
        for tri in l["tris"]:
            det.face([world(*pts[i], out) for i in tri], LETTER)
        for loop in l["loops"]:
            for i, j in zip(loop, loop[1:] + loop[:1]):
                a, b = pts[i], pts[j]
                nx, ny = b[1] - a[1], -(b[0] - a[0])           # outside of the outline: right of a -> b
                mid = ((a[0] + b[0]) / 2 + nx * 9, (a[1] + b[1]) / 2 + ny * 9)
                det.towards([world(*a, 0.0), world(*b, 0.0), world(*b, out), world(*a, out)], LETTER,
                            world(*mid, out / 2))
        used += [P(wall_u - out, v_left - x), P(wall_u - out, v_left - x - w)]
        x += w + C.LETTER_GAP
    # the small line under the word: raised bars of different lengths
    bars = [0.42, 0.26, 0.5, 0.18, 0.36, 0.3, 0.46, 0.22, 0.4, 0.3, 0.52, 0.24, 0.34]
    gap = 0.13
    run = sum(bars) + gap * (len(bars) - 1)
    x = total - run                                    # flush with the word's right end, like a sub-line
    zb = -0.62
    for w in bars:
        ring = [(x, zb), (x + w, zb), (x + w, zb + 0.13), (x, zb + 0.13)]
        det.face([world(*p, 0.05) for p in ring], LETTER)
        for a, b in zip(ring, ring[1:] + ring[:1]):
            nx, ny = b[1] - a[1], -(b[0] - a[0])
            mid = ((a[0] + b[0]) / 2 + nx * 9, (a[1] + b[1]) / 2 + ny * 9)
            det.towards([world(*a, 0.0), world(*b, 0.0), world(*b, 0.05), world(*a, 0.05)], LETTER,
                        world(*mid, 0.02))
        x += w + gap
    note_clear("letters on the end wall", used, overhead=C.LETTER_Z + zb - F1)
    print(f"  campus letters: word {total:.2f} m wide, cap height {C.LETTER_H} m, base line at {C.LETTER_Z} m "
          f"({C.LETTER_Z - F1:.1f} m above the plaza), v {v_left - total:.1f}..{v_left:.1f} on the 198..210 wall")


def gallery_lattice(det, P, zs, zr):
    """Gallery west face where the CTT terrace runs into it (N_w2_terrace_h090): white steel lattice in front of
    the glass. Over the drive-through only the truss fascia on the roof edge (above the passage cut); the
    full-height lattice bays stand only where they keep clear of the race line."""
    v0, v1, u = 97.4, 113.4, -0.22
    z0, z1 = zs + 0.15, zr - 0.1
    lines = truss_layer(det, lambda v, z: (*P(u, v), z), v0, v1, z0, z1, 6)
    det.glass(P(0.06, v1), P(0.0, v0), z0, z1, off=0.05, mat=DARK)       # dark glass behind it, so the lattice reads
    note_clear("Gallery west lattice fascia", [P(u, v) for v in lines], overhead=z0 - F1)
    lattice_screen(det, P, u, lines, F1 + 0.1, z0, "Gallery west lattice screen")


def north_colonnade(det, P, zs):
    """North exit (N_p04_northexit_*, N_p03_dropoff_h270): the route leaves through a deep colonnade under one
    white storey with a continuous blue-grey window band. The recess itself is cut out of the body
    (campus_plan.MAIN_BODY_UV); here its soffit, the storey's fascia with the ribbon, the faces that close the
    storey towards the hall, glazing on the back wall and the square pillars. A pillar stands only where it keeps
    ROUTE_CLEAR off the race line (the line leaves the building along this face), else the bay stays open."""
    u0, (v0, v1) = C.COLONNADE_U, C.COLONNADE_V
    zc = F1 + C.COLONNADE_CLEAR
    det.down([(*p, zc) for p in rect(P, u0, 69.3, v0, v1)], WALL)                       # soffit
    det.wall(P(69.3, v0), P(69.3, v1), zc, zs, WALL)                                    # fascia (faces east)
    det.wall(P(u0 - 0.03, v1), P(u0 - 0.03, v0), zc, zs, WALL)                          # towards the hall (west)
    det.wall(P(u0, v0 - 0.03), P(69.3, v0 - 0.03), zc, zs, WALL)                        # ... and south
    det.glass(P(69.3, v0 + 0.6), P(69.3, v1 - 0.6), zc + 1.2, zc + 3.6, tile=6.0)       # the window band
    g = max(E.ground_z(*P(u0, v)) for v in (190.0, 194.0, 198.0, 202.0))
    det.glass(P(u0, 190.5), P(u0, 203.5), g + 0.15, F1 + 3.1, mat=DARK, bay=4.3, pier=0.5)   # doors / shop front
    pillars, spots = [], []
    for v in (184.5, 191.25, 198.0):                   # 6.75 m apart, the north panel (v 205..210) ends the row
        u = 68.7
        while u > u0 + 1.2 and min(route_gap(*P(u + a, v + b)) for a in (-0.5, 0.5) for b in (-0.5, 0.5)) < ROUTE_CLEAR + 0.1:
            u -= 0.1
        ring = rect(P, u - 0.5, u + 0.5, v - 0.5, v + 0.5)
        if min(route_gap(*p) for p in ring) < ROUTE_CLEAR + 0.1:
            spots.append(f"v {v}: bay left open")
            continue
        det.prism(ring, min(E.ground_z(*p) for p in ring) - 0.3, zc, WALL, None)
        pillars += ring
        spots.append(f"v {v}: pillar at u {u:.1f}")
    if pillars:
        note_clear("north colonnade pillars", pillars)
    note_clear("north colonnade soffit and storey", rect(P, u0, 69.3, v0, v1), overhead=C.COLONNADE_CLEAR)
    keep_out(rect(P, u0, 69.3, v0, v1))
    print(f"  campus colonnade: {', '.join(spots)}")


# ----------------------------------------------------------------------------------------------------------------
# J  Milestone residence (luc_1, luc_2, luc_5, P_p01_h000, N_r1_h000)
# ----------------------------------------------------------------------------------------------------------------
def build_J(b):
    """3 storeys on a grey plinth, white flat-roofed bar. South faces (towards the plaza and the run-in): a white
    grid of slabs and blades with a dark back wall J_LOGGIA_DEPTH behind it.
    V16 (photospheres N06_h225_t75, N14_h350_t65, N12_h000; V15b had a regular checkerboard of white panels with one
    small window and loggias behind bar railings on a white plinth): an irregular checker, cell by cell from a hash:
    a projecting white box frame around the loggia (0.55 m out, 0.2 m members), a flush anthracite panel beside a
    narrower loggia, a plain open loggia, now and then a flush white panel; every loggia has a glass balustrade. The
    ground floor is dark glazing with anthracite panels between the white blades. The plinth is dark grey; the
    entrance link (x -39..-31) is glazed blue over all three storeys; a grass bank rises to the plinth on the avenue
    side. Where the outline itself is closer than ROUTE_CLEAR + 0.2 to the race line (the east end, as before) the
    wall stays a plain flush wall; a projecting frame stands only where it keeps ROUTE_CLEAR + 0.4. No lettering."""
    vol, det = Mesh(), Mesh()
    ring = ccw([tuple(p) for p in b["points"]])
    n = len(ring)
    near = lambda pt: min(range(n), key=lambda k: dist(ring[k], pt))           # noqa: E731
    k4, k6, k10, k11 = near((-92.5, 163.9)), near((-39.0, 161.4)), near((-30.6, 166.7)), near((10.1, 173.3))
    k7, k9 = near((-38.7, 168.3)), near((-31.6, 169.3))
    depth = C.J_LOGGIA_DEPTH
    plinth, storey, storeys, parapet = 0.9, 2.95, 3, 0.4          # V16: 10.2-10.8 m over the ground (survey 9.5-10.5)
    proud, bar = 0.55, 0.2                                        # the projecting box frames
    t = 1.0
    while t > 0.3 and route_gap(*lerp2(ring[k10], ring[k11], t)) < ROUTE_CLEAR + 0.2:
        t -= 0.01
    east_end = lerp2(ring[k10], ring[k11], t)
    faces = [(ring[k4], ring[k6]), (ring[k10], east_end)]
    inward = []
    for a, e in faces:
        nx, ny = right_normal(a, e)                     # ccw ring: right of a -> e is outside
        inward.append((-nx, -ny))
    back = [((a[0] + i[0] * depth, a[1] + i[1] * depth), (e[0] + i[0] * depth, e[1] + i[1] * depth))
            for (a, e), i in zip(faces, inward)]
    body = []
    for k in range(n):
        if k == k4:
            body.append(back[0][0])
        elif k4 < k < k6:
            continue
        elif k == k6:
            body.append(back[0][1])
        elif k == k10:
            body.append(back[1][0])
        elif k == k11:
            body += [back[1][1], east_end, ring[k]]
        else:
            body.append(ring[k])
    samples = [lerp2(a, e, j / 12) for a, e in faces for j in range(13)]
    zp = max(E.ground_z(*p) for p in samples) + plinth                          # top of the plinth = ground floor
    top = zp + storeys * storey
    base = min(E.ground_z(x, y) for x, y in ring) - 1.0
    vol.prism(body, base, top, WALL, None)
    vol.face([(*p, base) for p in ccw(body)][::-1], WALL)
    vol.prism(ring, top, top + parapet, WALL, ROOF, WALL)                        # roof slab over the frame too
    cells = {"frame": 0, "panel": 0, "open": 0, "white": 0}
    frame_pts = []
    for fi, ((a, e), (ba, be), i) in enumerate(zip(faces, back, inward)):
        length = dist(a, e)
        units = max(2, int(round(length / C.J_UNIT)))
        w = length / units
        tx, ty = (e[0] - a[0]) / length, (e[1] - a[1]) / length
        at = lambda s, d: (a[0] + tx * s + i[0] * d, a[1] + ty * s + i[1] * d)   # noqa: E731
        out = at(length / 2, -30.0)

        def quad(s0, s1, d, z0, z1, mat):
            p, q = facing(at(s0, d), at(s1, d), out)
            det.face([(*p, z0), (*q, z0), (*q, z1), (*p, z1)], mat)

        def rnd(k, s, salt=0):
            v = math.sin((k + 1) * 12.9898 + (s + 1) * 78.233 + fi * 37.719 + salt * 11.13) * 43758.5453
            return v - math.floor(v)
        # dark back wall of the loggias, just in front of the body's wall
        quad(0.0, length, depth - 0.04, zp, top, DARK)
        # dark grey plinth and the white floor slabs
        det.prism(ccw([a, e, be, ba]), base, zp, SETT, SETT)
        for s in range(1, storeys):
            z = zp + s * storey
            det.prism(ccw([at(0.0, 0.0), at(length, 0.0), at(length, depth - 0.05), at(0.0, depth - 0.05)]),
                      z - 0.28, z, WALL, WALL, WALL)
        # blades between the units
        for k in range(units + 1):
            s = min(max(k * w, 0.12), length - 0.12)
            det.prism(ccw([at(s - 0.12, 0.0), at(s + 0.12, 0.0), at(s + 0.12, depth - 0.05), at(s - 0.12, depth - 0.05)]),
                      zp, top, WALL, None)
        for k in range(units):                           # ground floor: an anthracite panel in two cells of five
            if rnd(k, 0) < 0.4:
                quad(k * w + 0.12, (k + 1) * w - 0.12, 0.3, zp, zp + storey - 0.28, SLAT)
        for s in range(1, storeys):
            z, zt = zp + s * storey, zp + (s + 1) * storey - (0.28 if s + 1 < storeys else 0.0)
            for k in range(units):
                s0, s1 = k * w + 0.12, (k + 1) * w - 0.12
                r = rnd(k, s)
                if r < 0.36 and min(route_gap(*at(s0 - 0.12, -proud)), route_gap(*at(s1 + 0.12, -proud))) < ROUTE_CLEAR + 0.4:
                    r = 0.9                              # too close to the race line for a projecting frame: open loggia
                if r < 0.36:                             # projecting white box frame with a glass balustrade in front
                    o0, o1 = s0 - 0.12, s1 + 0.12
                    zf = zt + 0.28 if s + 1 < storeys else top + parapet
                    for u0, u1, z0, z1 in ((o0, o0 + bar, z - 0.28, zf), (o1 - bar, o1, z - 0.28, zf),
                                           (o0 + bar, o1 - bar, z - 0.28, z - 0.28 + bar),
                                           (o0 + bar, o1 - bar, zf - bar, zf)):
                        det.prism(ccw([at(u0, -proud), at(u1, -proud), at(u1, 0.0), at(u0, 0.0)]), z0, z1, WALL, WALL, WALL)
                    quad(o0 + bar, o1 - bar, -proud + 0.06, z - 0.28 + bar, z + 1.0, GLASS_ROOF)
                    frame_pts += [at(o0, -proud), at(o1, -proud)]
                    cells["frame"] += 1
                elif r < 0.64:                           # flush anthracite panel beside a narrower loggia
                    cut = s0 + (s1 - s0) * (0.5 + 0.12 * rnd(k, s, 1))
                    if rnd(k, s, 2) < 0.5:
                        quad(s0, cut, 0.03, z, zt, SLAT)
                        quad(cut, s1, 0.06, z, z + 1.0, GLASS_ROOF)
                    else:
                        quad(s0 + s1 - cut, s1, 0.03, z, zt, SLAT)
                        quad(s0, s0 + s1 - cut, 0.06, z, z + 1.0, GLASS_ROOF)
                    cells["panel"] += 1
                elif r < 0.72:                           # flush white panel with one window
                    quad(s0, s1, 0.0, z, zt, WALL)
                    c = (s0 + s1) / 2
                    quad(c - 0.55, c + 0.55, -0.04, z + 0.9, z + 2.3, DARK)
                    cells["white"] += 1
                else:                                    # open loggia with a glass balustrade
                    quad(s0, s1, 0.06, z, z + 1.0, GLASS_ROOF)
                    cells["open"] += 1
    # entrance link: blue glazing over all three storeys between thin white slab edges, no lettering
    a, e = ring[k7], ring[k9]
    for s in range(storeys):
        z = zp + s * storey
        det.glass(a, e, z + (0.1 if s == 0 else 0.0), z + storey - 0.22, tile=4.8, inset=0.25)
    # every other long face (north side, gables): plain windows in a regular rhythm
    for k in range(n):
        j = (k + 1) % n
        if k4 <= k < k6 or k == k10 or k7 <= k < k9 or dist(ring[k], ring[j]) < 6.0:
            continue
        for s in range(storeys):
            z = zp + s * storey
            det.glass(ring[k], ring[j], z + 0.95, z + 2.45, bay=C.J_UNIT, pier=C.J_UNIT - 1.3, inset=0.5, mat=DARK)
    # the lawn rises to the plinth on the avenue side (ST01: the west end stands on a grass bank)
    berm(vol, [(-97.4, 180.2), (-97.7, 169.5), (-97.8, 166.6)], (-112.0, 172.0), plinth, width=7.0)
    note_clear("Milestone J loggia grid", [p for f in faces for p in f])
    note_clear("Milestone J projecting frames", frame_pts)
    print(f"  campus J: plinth top {zp:.2f} m, roof {top + parapet:.2f} m "
          f"({top + parapet - (zp - plinth):.1f} m above the plaza side), grid ends {dist(east_end, ring[k11]):.1f} m "
          f"before the east corner; cells: {cells}")
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


# ----------------------------------------------------------------------------------------------------------------
# the two apartment blocks north-west of the drop-off plaza (N_p01_h000, N_p01_h270, N_p03_dropoff_h320)
# ----------------------------------------------------------------------------------------------------------------
def build_housing(b):
    """5-storey white apartment block with balconies, a terracotta roof edge and a set-back roof storey under a
    low tiled hip roof (it was a 3-storey generic box). Balconies only on the faces towards the campus."""
    vol, det = Mesh(), Mesh()
    ring = ccw([tuple(p) for p in b["points"]])
    grounds = [E.ground_z(x, y) for x, y in ring]
    base = min(grounds) - 1.0
    floor = (min(grounds) + max(grounds)) / 2
    top = floor + C.HOUSING_STOREYS * C.HOUSING_STOREY
    vol.prism(ring, base, top, CREAM, ROOF)
    vol.prism(grow(ring, 0.45), top, top + 0.3, TILES, TILES, WALL)                  # terracotta roof edge
    inner = grow(ring, -3.2)
    vol.prism(inner, top + 0.3, top + 2.9, CREAM, None)
    eave, c = grow(inner, 0.5), centroid(ring)
    for a, e in zip(eave, eave[1:] + eave[:1]):
        vol.up([(*a, top + 2.9), (*e, top + 2.9), (*c, top + 4.4)], TILES)
    vol.face([(*p, top + 2.9) for p in eave][::-1], WALL)
    edge_pts = []
    for a, e in zip(ring, ring[1:] + ring[:1]):
        nx, ny = right_normal(a, e)
        campus_side = nx * 0.35 - ny * 0.94 > 0.2 or nx > 0.8        # faces south-south-east or east
        for s in range(C.HOUSING_STOREYS):
            z = floor + s * C.HOUSING_STOREY
            if z + 0.9 < max(E.ground_z(*a), E.ground_z(*e)) + 0.3:
                continue
            det.glass(a, e, z + 0.9, z + 2.4, bay=4.4, pier=2.0, inset=0.9, mat=DARK)
            if not campus_side or s == 0:
                continue
            p0, p1 = lerp2(a, e, 0.1), lerp2(a, e, 0.9)
            q0, q1 = (p0[0] + nx * 1.1, p0[1] + ny * 1.1), (p1[0] + nx * 1.1, p1[1] + ny * 1.1)
            det.prism(ccw([p0, p1, q1, q0]), z - 0.18, z, WALL, WALL, WALL)
            det.railing([(*p0, z), (*q0, z), (*q1, z), (*p1, z)], h=1.0)
            edge_pts += [q0, q1]
    if edge_pts:
        note_clear(f"apartment block {b['id']} balconies", edge_pts, overhead=C.HOUSING_STOREY + floor - F1)
    note_clear(f"apartment block {b['id']} (OSM outline, as before)", ring)
    return vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])


OWNED[C.J_ID] = build_J
for _id in C.HOUSING_IDS:
    OWNED[_id] = build_housing


# ----------------------------------------------------------------------------------------------------------------
# drop-off plaza ring, bin enclosure (called at the end of build_extras)
# ----------------------------------------------------------------------------------------------------------------
def build_v14b_extras(m):
    """m: the campus grounds mesh. Bin enclosure against the housing block's south wall."""
    c, d = C.BIN_C, C.BIN_DIR
    hl, hd, h = C.BIN_LEN / 2, C.BIN_DEPTH / 2, C.BIN_H
    v = (-d[1], d[0])
    ring = [(c[0] + d[0] * a + v[0] * b_, c[1] + d[1] * a + v[1] * b_) for a, b_ in ((-hl, -hd), (hl, -hd), (hl, hd), (-hl, hd))]
    gs = [E.ground_z(*p) for p in ring]
    g, z0 = max(gs), min(gs) - 0.3
    for sgn in (-1, 1):                                                           # white frame: piers, beam, plinth
        m.box((c[0] + d[0] * sgn * (hl - 0.15), c[1] + d[1] * sgn * (hl - 0.15)), d, 0.15, hd, z0, g + h, WALL, WALL)
    m.box(c, d, hl, hd, g + h - 0.3, g + h, WALL, WALL, WALL)
    m.box(c, d, hl, hd, z0, g + 0.22, WALL, WALL)
    m.box(c, d, hl - 0.15, hd - 0.14, g + 0.22, g + h - 0.3, SLAT, None)          # anthracite panel
    for k in range(6):                                                            # horizontal slats
        z = g + 0.42 + k * 0.265
        m.box(c, d, hl - 0.15, hd - 0.07, z, z + 0.13, SLAT, SLAT, SLAT)
    keep_out(ring, 0.6)
    note_clear("bin enclosure", ring)
    # V16: the flush ring of dark setts is now part of the forecourt's draped sett sheet (env_north.build_forecourt),
    # at its surveyed place and size (centre (80.7, 177), 9.3 m across); V15b's own object
    # World_CampusPaving_NoCollide (r 6 m at (80.3, 177), on the bare ground) is gone.
    clearance_report()
