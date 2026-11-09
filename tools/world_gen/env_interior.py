"""Interior of the drive-through passages: the Nova atrium (grand hall) and the short covered stretches.

What the walk videos show (IMG_7725 261-313 s, IMG_7722 22-112 s):
  * glass entrance with a sliding door in the middle, slim metal frames, white square pillars outside;
  * a two-storey hall: polished concrete floor, a gallery with a glass balustrade on both sides,
    slender white round columns with a flared foot, a steel-and-glass roof about 10 m up;
  * a wide white straight staircase to the gallery, a low grey stage with two steps, a curved row of
    dark grey cube seats, white tables with black chairs, blue-grey partition boards,
    black planter troughs with ficus along the walls.

How it is built (all numbers are constants below, nothing is hand-placed in Blender):
  * `extents()` measures how wide the hall can be at every route point (inside the building footprint);
    build_world.py cuts exactly that shape out of the building and this file furnishes it.
  * Everything is placed with a tiny coordinate system along the route: u = metres along the route,
    o = metres to the left of it, z = metres above the floor. `Hall.at(u, o, z)` gives the world position,
    so the hall follows the bend of the route and every item can be moved by changing one number.
  * The short covered stretches (CTT Student Hall, the east edge of the main building) keep the simple
    dressing of the first version: a column every 8 m, glazing, ceiling lights.

Kept for the browser: about 40,000 triangles in total, one mesh `Interior_Passages`. The lane (4 m either side
of the route) stays free of furniture because the world meshes get mesh colliders in Unity.
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

import env_common as E

# ---------- numbers you can change ----------
HALL_HEIGHT = 10.0          # clear height of the atrium above the floor (m); the cut in build_world.py uses it
HALL_MAX_LEFT = 15.5        # widest the hall gets to the left (east) of the route (m)
HALL_MAX_RIGHT = 6.5        # widest to the right (west)
HALL_MIN = 4.8              # never narrower than the lane (4 m) plus a margin
WALL_GAP = 1.0              # distance kept to the building footprint (m)
ATRIUM_S_RANGE = (20.0, 125.0)   # route metres: a passage whose middle lies here is the atrium (data, not magic)
GALLERY_Z = 4.8             # floor level of the gallery (m above the hall floor)
GALLERY_LEFT = 4.5          # depth of the east gallery (m, less where the hall is narrow)
GALLERY_RIGHT = 2.2         # depth of the west balcony
COLUMN_EVERY = 7.0          # metres between the columns
LANE = 4.4                  # nothing is placed closer than this to the route centreline (road is 8 m wide)

# simple passages (first version)
SIMPLE_COLUMN_EVERY = 4     # route points (2 m each)
SIMPLE_COLUMN = 0.45
SIMPLE_HEIGHT = 7.0

STEP = 1.0                  # sampling distance along the route for walls and slabs (m)


# ---------- hall width from the building footprint ----------
def _ray_to_polygon(p, nx, ny, poly):
    best = 99.0
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        ex, ey = bx - ax, by - ay
        den = nx * ey - ny * ex
        if abs(den) < 1e-9:
            continue
        u = ((ax - p["x"]) * ey - (ay - p["y"]) * ex) / den
        v = ((ax - p["x"]) * ny - (ay - p["y"]) * nx) / den
        if u > 0 and 0 <= v <= 1:
            best = min(best, u)
    return best


def is_atrium(name, run):
    """True for the one long passage of the main building (the hall), not for the short covered stretches."""
    if name != "Edifício Principal":
        return False
    inside = [p for p in run if p["building"]]
    if len(inside) < 20:
        return False
    mid = inside[len(inside) // 2]["s"]
    return ATRIUM_S_RANGE[0] <= mid <= ATRIUM_S_RANGE[1]


def extents(run, poly):
    """[(left, right)] hall half-widths per route point, limited by the building footprint and smoothed."""
    raw = []
    for k, p in enumerate(run):
        a, b = run[max(k - 1, 0)], run[min(k + 1, len(run) - 1)]
        d = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
        nx, ny = -(b["y"] - a["y"]) / d, (b["x"] - a["x"]) / d        # left normal
        left = _ray_to_polygon(p, nx, ny, poly) - WALL_GAP
        right = _ray_to_polygon(p, -nx, -ny, poly) - WALL_GAP
        raw.append((max(HALL_MIN, min(HALL_MAX_LEFT, left)), max(HALL_MIN, min(HALL_MAX_RIGHT, right))))
    smooth = list(raw)
    for _ in range(3):                                                  # moving average, then back inside the footprint
        smooth = [tuple(sum(smooth[min(max(j, 0), len(smooth) - 1)][side] for j in range(k - 3, k + 4)) / 7.0
                        for side in (0, 1)) for k in range(len(smooth))]
        smooth = [(min(s[0], r[0]), min(s[1], r[1])) for s, r in zip(smooth, raw)]
    return smooth


# ---------- tiny mesh helpers ----------
class Mesh:
    """bmesh with named materials. Faces get a desired normal so the winding never has to be thought about."""

    def __init__(self):
        self.bm = bmesh.new()
        self.names = []

    def mat(self, name):
        if name not in self.names:
            self.names.append(name)
        return self.names.index(name)

    def face(self, pts, material, normal=None):
        verts = [self.bm.verts.new(p) for p in pts]
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return None
        f.material_index = self.mat(material)
        f.normal_update()
        if normal is not None and f.normal.dot(Vector(normal)) < 0:
            f.normal_flip()
        return f

    def box(self, c, hx, hy, hz, material, rot=0.0):
        """Axis-aligned box (optionally turned around z) with centre c and half sizes."""
        cs, sn = math.cos(rot), math.sin(rot)

        def w(dx, dy, dz):
            return (c[0] + dx * cs - dy * sn, c[1] + dx * sn + dy * cs, c[2] + dz)

        v = [w(sx * hx, sy * hy, sz * hz) for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        for idx, n in (((0, 1, 3, 2), (0, 0, -1)), ((4, 6, 7, 5), (0, 0, 1)), ((0, 4, 5, 1), (0, -1, 0)),
                       ((2, 3, 7, 6), (0, 1, 0)), ((0, 2, 6, 4), (-1, 0, 0)), ((1, 5, 7, 3), (1, 0, 0))):
            nx, ny = n[0] * cs - n[1] * sn, n[0] * sn + n[1] * cs
            self.face([v[i] for i in idx], material, (nx, ny, n[2]))

    def cylinder(self, base, radius, height, material, sides=10, top_radius=None):
        top_radius = radius if top_radius is None else top_radius
        ring0 = [(base[0] + radius * math.cos(2 * math.pi * i / sides), base[1] + radius * math.sin(2 * math.pi * i / sides), base[2])
                 for i in range(sides)]
        ring1 = [(base[0] + top_radius * math.cos(2 * math.pi * i / sides), base[1] + top_radius * math.sin(2 * math.pi * i / sides),
                  base[2] + height) for i in range(sides)]
        for i in range(sides):
            j = (i + 1) % sides
            mid = (ring0[i][0] + ring0[j][0]) / 2 - base[0], (ring0[i][1] + ring0[j][1]) / 2 - base[1]
            self.face([ring0[i], ring0[j], ring1[j], ring1[i]], material, (mid[0], mid[1], 0))
        if top_radius > 0:
            self.face(ring1, material, (0, 0, 1))

    def blob(self, c, radius, material, seed, squash=1.0):
        """Lumpy low-poly sphere (foliage)."""
        rnd = random.Random(seed)
        geo = bmesh.ops.create_icosphere(self.bm, subdivisions=2, radius=radius)
        for v in geo["verts"]:
            v.co = Vector((c[0] + v.co.x * (0.85 + rnd.random() * 0.3), c[1] + v.co.y * (0.85 + rnd.random() * 0.3),
                           c[2] + v.co.z * squash * (0.85 + rnd.random() * 0.3)))
        for f in self.bm.faces:
            if f.verts[0] in geo["verts"] and f.material_index == 0 and material:
                f.material_index = self.mat(material)

    def to_object(self, name, col):
        self.bm.normal_update()
        uv = self.bm.loops.layers.uv.verify()
        for f in self.bm.faces:                       # planar UVs in metres (1 texture tile per metre)
            n = f.normal
            for loop in f.loops:
                p = loop.vert.co
                if abs(n.z) >= abs(n.x) and abs(n.z) >= abs(n.y):
                    loop[uv].uv = (p.x, p.y)
                elif abs(n.x) >= abs(n.y):
                    loop[uv].uv = (p.y, p.z)
                else:
                    loop[uv].uv = (p.x, p.z)
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for n in self.names:
            mesh.materials.append(MATERIALS[n]())
        return E.new_object(name, mesh, col)


def _mat(name, colour, rough=0.6, metal=0.0, emission=None, alpha=1.0):
    return lambda: E.material(name, colour, rough, metal, emission, alpha)


MATERIALS = {
    "column": _mat("B_interior_column", "#F4F3F0", 0.6),
    "glass": lambda: E.material("B_glass"),
    "light": _mat("interior_light", "#FFF8EA", 0.5, emission="#FFF8EA"),
    "floor": _mat("I_floor_hall", "#B3A58B", 0.32),                  # warm polished concrete, no texture (the texture is near white)
    "glazing": _mat("I_glazing", "#7E95A8", 0.12, 0.25),             # light reflective glass for the hall walls
    "steel_dark": _mat("I_steel_dark", "#2B2E33", 0.4, 0.6),
    "steel_white": _mat("I_steel_white", "#EDEDEA", 0.45, 0.3),
    "soffit": _mat("I_soffit", "#F7F6F2", 0.9),
    "skylight": _mat("I_skylight", "#DDE9F2", 0.3, emission="#C4D6E6"),
    "stair": _mat("I_stair", "#F2F1EC", 0.55),
    "grey": _mat("I_grey_dark", "#3A3D44", 0.9),
    "black": _mat("I_black", "#1E1F22", 0.7),
    "ficus": _mat("I_ficus", "#3F6B3A", 0.9),
    "table": _mat("I_table_white", "#F0EFEA", 0.5),
    "board": _mat("I_board", "#4A5668", 0.9),
    "rail": _mat("I_rail", "#B9BCC2", 0.3, 0.8),
    "mat": _mat("I_door_mat", "#2A2B2E", 0.95),
}


# ---------- the hall coordinate system ----------
class Hall:
    """u = metres along the route inside the building, o = metres to the left, z = metres above the floor."""

    def __init__(self, pts, ext):
        self.pts, self.ext = pts, ext
        self.s = [0.0]
        for a, b in zip(pts, pts[1:]):
            self.s.append(self.s[-1] + math.hypot(b["x"] - a["x"], b["y"] - a["y"]))
        self.length = self.s[-1]

    def _seg(self, u):
        u = min(max(u, 0.0), self.length)
        k = 0
        while k < len(self.s) - 2 and self.s[k + 1] < u:
            k += 1
        t = (u - self.s[k]) / ((self.s[k + 1] - self.s[k]) or 1.0)
        return k, t

    def centre(self, u):
        k, t = self._seg(u)
        a, b = self.pts[k], self.pts[k + 1]
        return (a["x"] + (b["x"] - a["x"]) * t, a["y"] + (b["y"] - a["y"]) * t, a["track_z"] + (b["track_z"] - a["track_z"]) * t)

    def tangent(self, u):
        a, b = self.centre(u - 0.5), self.centre(u + 0.5)
        d = math.hypot(b[0] - a[0], b[1] - a[1]) or 1.0
        return ((b[0] - a[0]) / d, (b[1] - a[1]) / d)

    def left(self, u):
        t = self.tangent(u)
        return (-t[1], t[0])

    def heading(self, u):
        t = self.tangent(u)
        return math.atan2(t[1], t[0])

    def at(self, u, o, z=0.0):
        c, n = self.centre(u), self.left(u)
        return (c[0] + n[0] * o, c[1] + n[1] * o, c[2] + z)

    def width(self, u):
        k, t = self._seg(u)
        a, b = self.ext[k], self.ext[k + 1]
        return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)

    def left_edge(self, u):
        return self.width(u)[0]

    def right_edge(self, u):
        return self.width(u)[1]

    def samples(self, u0, u1, step=STEP):
        n = max(1, int(math.ceil((u1 - u0) / step)))
        return [u0 + (u1 - u0) * i / n for i in range(n + 1)]


# ---------- parts ----------
def horizontal(m, hall, u0, u1, o0, o1, z, material, up=True, o_fn=None):
    """Flat strip following the route. o0/o1 may be numbers or functions of u."""
    us = hall.samples(u0, u1)
    f0 = o0 if callable(o0) else (lambda u: o0)
    f1 = o1 if callable(o1) else (lambda u: o1)
    for a, b in zip(us, us[1:]):
        m.face([hall.at(a, f0(a), z), hall.at(b, f0(b), z), hall.at(b, f1(b), z), hall.at(a, f1(a), z)], material,
               (0, 0, 1 if up else -1))


def vertical(m, hall, u0, u1, o, z0, z1, material, facing, step=STEP):
    """Vertical strip at lateral offset o (a number or a function of u), facing +1 = towards the left, -1 = right."""
    fo = o if callable(o) else (lambda u: o)
    us = hall.samples(u0, u1, step)
    for a, b in zip(us, us[1:]):
        n = hall.left(a)
        m.face([hall.at(a, fo(a), z0), hall.at(b, fo(b), z0), hall.at(b, fo(b), z1), hall.at(a, fo(a), z1)], material,
               (n[0] * facing, n[1] * facing, 0))


def local_box(m, hall, u, o, z0, z1, half_u, half_o, material, turn=0.0):
    """Box centred at route position (u, o), z from z0 to z1, oriented with the route."""
    p = hall.at(u, o, (z0 + z1) / 2)
    m.box(p, half_u, half_o, (z1 - z0) / 2, material, hall.heading(u) + turn)


def column(m, hall, u, o, top):
    base = hall.at(u, o, 0.0)
    m.cylinder(base, 0.42, 0.55, "column", 10, top_radius=0.23)             # flared foot
    m.cylinder((base[0], base[1], base[2] + 0.55), 0.23, top - 0.55 - 0.25, "column", 10)
    m.cylinder((base[0], base[1], base[2] + top - 0.25), 0.23, 0.25, "column", 10, top_radius=0.3)   # small capital


def slab(m, hall, u0, u1, o0, o1, z0, z1, material, edge_material=None):
    """Gallery floor: top, underside (soffit) and the two long edges."""
    edge_material = edge_material or material
    horizontal(m, hall, u0, u1, o0, o1, z1, material, up=True)
    horizontal(m, hall, u0, u1, o0, o1, z0, "soffit", up=False)
    vertical(m, hall, u0, u1, o0, z0, z1, edge_material, facing=-1)      # o0 is the lower (more right) edge
    vertical(m, hall, u0, u1, o1, z0, z1, edge_material, facing=1)


def balustrade(m, hall, u0, u1, o, z, facing):
    """Glass panels in a steel frame with a handrail."""
    fo = o if callable(o) else (lambda u: o)
    vertical(m, hall, u0, u1, lambda u: fo(u), z + 0.12, z + 1.1, "glass", facing)
    vertical(m, hall, u0, u1, lambda u: fo(u) + 0.01 * -facing, z + 0.12, z + 1.1, "glass", -facing)
    for u in hall.samples(u0, u1, 2.0):
        local_box(m, hall, u, fo(u), z, z + 1.1, 0.03, 0.03, "rail")
    for u in hall.samples(u0, u1, 1.0)[:-1]:
        local_box(m, hall, u + 0.5, fo(u + 0.5), z + 1.08, z + 1.14, 0.55, 0.035, "rail")


def stair(m, hall, u0, u1, o0, o1, z_top):
    """Straight flight rising along +u from the floor at u0 to z_top at u1; side stringers and glass balustrade."""
    rise = 0.17
    n = int(round(z_top / rise))
    run = (u1 - u0) / n
    for i in range(n):
        top = rise * (i + 1)
        ua, ub = u0 + run * i, u0 + run * (i + 1)
        horizontal(m, hall, ua, ub, o0, o1, top, "stair", up=True)
        vertical(m, hall, ua, ua + 1e-3 + run * 0.999, o0, 0, top, "stair", facing=-1, step=run)
        vertical(m, hall, ua, ua + 1e-3 + run * 0.999, o1, 0, top, "stair", facing=1, step=run)
        a, b = hall.at(ua, o0, rise * i), hall.at(ua, o1, rise * i)
        t = hall.tangent(ua)
        m.face([a, b, (b[0], b[1], top), (a[0], a[1], top)], "stair", (-t[0], -t[1], 0))      # riser, faces back
    # balustrades follow the slope: a sloped glass band on each side
    for o, facing in ((o0, -1), (o1, 1)):
        n_o = hall.left(u0)
        a, b = hall.at(u0, o, 0.0), hall.at(u1, o, z_top)
        for sign, off in ((1, 0.0), (-1, 0.012)):
            shift = (n_o[0] * off * facing, n_o[1] * off * facing)
            m.face([(a[0] + shift[0], a[1] + shift[1], a[2] + 0.1), (b[0] + shift[0], b[1] + shift[1], b[2] + 0.1),
                    (b[0] + shift[0], b[1] + shift[1], b[2] + 1.1), (a[0] + shift[0], a[1] + shift[1], a[2] + 1.1)], "glass",
                   (n_o[0] * facing * sign, n_o[1] * facing * sign, 0))
        m.face([(a[0], a[1], a[2] + 1.08), (b[0], b[1], b[2] + 1.08), (b[0], b[1], b[2] + 1.14), (a[0], a[1], a[2] + 1.14)],
               "rail", (0, 0, 1))


def chair(m, hall, u, o, facing_deg):
    turn = math.radians(facing_deg)
    p = hall.at(u, o, 0)
    h = hall.heading(u) + turn
    c, s = math.cos(h), math.sin(h)
    m.box((p[0], p[1], p[2] + 0.45), 0.22, 0.22, 0.025, "black", h)
    m.box((p[0] - 0.2 * c, p[1] - 0.2 * s, p[2] + 0.75), 0.02, 0.22, 0.25, "black", h)
    for dx in (-0.18, 0.18):
        for dy in (-0.18, 0.18):
            m.box((p[0] + dx * c - dy * s, p[1] + dx * s + dy * c, p[2] + 0.22), 0.015, 0.015, 0.22, "steel_dark", h)


def table(m, hall, u, o):
    p = hall.at(u, o, 0)
    h = hall.heading(u)
    m.box((p[0], p[1], p[2] + 0.74), 0.8, 0.42, 0.02, "table", h)
    for sgn in (-1, 1):
        q = hall.at(u + sgn * 0.62, o, 0)
        m.box((q[0], q[1], q[2] + 0.36), 0.02, 0.36, 0.36, "steel_dark", h)
    for sgn in (-1, 1):
        chair(m, hall, u + sgn * 0.0, o + sgn * 0.78, 90 if sgn > 0 else -90)
    chair(m, hall, u - 1.0, o, 180)
    chair(m, hall, u + 1.0, o, 0)


def planter(m, hall, u, o, seed):
    h = hall.heading(u)
    p = hall.at(u, o, 0)
    m.box((p[0], p[1], p[2] + 0.3), 0.7, 0.3, 0.3, "black", h)
    rnd = random.Random(seed)
    for k in range(2):
        q = hall.at(u + (k - 0.5) * 0.7, o, 0)
        m.cylinder((q[0], q[1], q[2] + 0.6), 0.03, 0.6 + rnd.random() * 0.3, "steel_dark", 5)
        for j in range(3):                                               # a crown made of three overlapping lumps
            m.blob((q[0] + (j - 1) * 0.22, q[1] + (j % 2 - 0.5) * 0.2, q[2] + 1.4 + j * 0.12 + rnd.random() * 0.2),
                   0.34 + rnd.random() * 0.08, "ficus", seed * 7 + k * 3 + j, 0.9)


def bench_arc(m, hall, uc, oc, radius, a0, a1, count):
    for i in range(count):
        a = math.radians(a0 + (a1 - a0) * i / (count - 1))
        u, o = uc + radius * math.sin(a), oc - radius * math.cos(a)
        local_box(m, hall, u, o, 0.0, 0.46, 0.27, 0.27, "grey", turn=-a)


def stage(m, hall, u0, u1, o0, o1):
    local_box(m, hall, (u0 + u1) / 2, (o0 + o1) / 2, 0.0, 0.42, (u1 - u0) / 2, (o1 - o0) / 2, "grey")
    for i in range(2):                                          # two steps on the side facing the route
        local_box(m, hall, (u0 + u1) / 2 + 1.0, o0 - 0.3 - 0.3 * i, 0.0, 0.14 + 0.14 * (1 - i), 0.9, 0.15, "steel_dark")
    for k in range(4):                                          # leg trim
        local_box(m, hall, u0 + (u1 - u0) * k / 3, o0 - 0.01, 0.0, 0.42, 0.04, 0.01, "steel_dark")


def partition(m, hall, u, o):
    local_box(m, hall, u, o, 0.15, 1.9, 0.03, 0.9, "board")
    local_box(m, hall, u, o - 0.1, 0.0, 0.15, 0.04, 0.45, "steel_dark")


# ---------- the atrium ----------
def build_atrium(run, ext, col):
    inside = [(i, p) for i, p in enumerate(run) if p["building"]]
    pts = [p for _, p in inside]
    ex = [ext[i] for i, _ in inside]
    hall = Hall(pts, ex)
    m = Mesh()
    H, U = HALL_HEIGHT, hall.length
    L, R = hall.left_edge, hall.right_edge
    gl = lambda u: min(GALLERY_LEFT, max(2.4, L(u) - 8.0))               # east gallery depth, less where the hall narrows
    inner_l = lambda u: L(u) - gl(u)
    inner_r = lambda u: -(R(u) - GALLERY_RIGHT)

    # floor beside the road (the road itself is the track mesh); joints come from the polished concrete texture
    horizontal(m, hall, 0, U, LANE - 0.4, L, 0.03, "floor")
    horizontal(m, hall, 0, U, lambda u: -R(u), -(LANE - 0.4), 0.03, "floor")

    # galleries (first floor) with glass balustrades, the wall behind them is glazed
    slab(m, hall, 0, U, inner_l, L, GALLERY_Z - 0.4, GALLERY_Z, "stair", "soffit")
    slab(m, hall, 0, U, lambda u: -R(u), inner_r, GALLERY_Z - 0.4, GALLERY_Z, "stair", "soffit")
    balustrade(m, hall, 0, U, inner_l, GALLERY_Z, facing=-1)
    balustrade(m, hall, 0, U, inner_r, GALLERY_Z, facing=1)

    # walls: ground floor = white wall with glazed shop fronts, first floor = glass curtain wall with dark spandrel
    for side, wall, facing in ((1, L, -1), (-1, lambda u: -R(u), 1)):
        o_wall = (lambda u, w=wall, sd=side: (w(u) - 0.06) if sd > 0 else (w(u) + 0.06))
        for k in range(int(U // 6)):                                         # shop fronts: 4.5 m glass, 1.5 m white
            u0 = 1.5 + 6.0 * k
            vertical(m, hall, u0, u0 + 4.5, o_wall, 0.25, 3.9, "glazing", facing)
            for j in range(4):                                               # mullions
                local_box(m, hall, u0 + 1.12 * j + 0.0, o_wall(u0) + 0.0, 0.25, 3.9, 0.03, 0.04, "steel_dark")
            vertical(m, hall, u0, u0 + 4.5, o_wall, 3.9, 4.2, "steel_dark", facing)
        vertical(m, hall, 0, U, o_wall, GALLERY_Z + 0.05, H - 0.9, "glazing", facing)
        vertical(m, hall, 0, U, o_wall, H - 0.9, H, "steel_dark", facing)
        for u in hall.samples(0, U, 2.5)[:-1]:                               # first-floor mullions
            local_box(m, hall, u, o_wall(u), GALLERY_Z, H, 0.03, 0.05, "steel_white")

    # ceiling: white soffit over the galleries, a steel-and-glass skylight over the void
    horizontal(m, hall, 0, U, inner_l, L, H, "soffit", up=False)
    horizontal(m, hall, 0, U, lambda u: -R(u), inner_r, H, "soffit", up=False)
    horizontal(m, hall, 0, U, inner_r, inner_l, H, "skylight", up=False)
    for u in hall.samples(0, U, 3.0):                                       # cross beams (flange + web) over the void
        a, b = inner_r(u) - 0.2, inner_l(u) + 0.2
        local_box(m, hall, u, (a + b) / 2, H - 0.62, H - 0.5, 0.14, (b - a) / 2, "steel_white")
        local_box(m, hall, u, (a + b) / 2, H - 0.5, H - 0.08, 0.04, (b - a) / 2, "steel_white")
    for k in range(5):                                                     # long purlins
        f = (k + 0.5) / 5
        for a, b in zip(hall.samples(0, U, 3.0), hall.samples(0, U, 3.0)[1:]):
            o = inner_r(a) + (inner_l(a) - inner_r(a)) * f
            local_box(m, hall, (a + b) / 2, o, H - 0.2, H - 0.06, (b - a) / 2, 0.06, "steel_white")
    for u in hall.samples(0, U, 3.0)[:-1]:                                  # recessed downlights under the galleries
        for o in (inner_l(u) + 0.8, inner_r(u) - 0.8):
            local_box(m, hall, u + 1.5, o, GALLERY_Z - 0.43, GALLERY_Z - 0.4, 0.14, 0.14, "light")
    for u in hall.samples(0, U, 6.0)[:-1]:                                  # light strips on the gallery fronts
        local_box(m, hall, u + 3.0, inner_l(u + 3.0) + 0.3, GALLERY_Z - 0.46, GALLERY_Z - 0.4, 2.2, 0.06, "light")
        local_box(m, hall, u + 3.0, inner_r(u + 3.0) - 0.3, GALLERY_Z - 0.46, GALLERY_Z - 0.4, 2.2, 0.06, "light")

    # columns on the gallery edges
    for k in range(int((U - 3) // COLUMN_EVERY) + 1):
        u = 3.5 + COLUMN_EVERY * k
        if u < U - 1.0:
            column(m, hall, u, inner_l(u) + 0.25, H)
            column(m, hall, u + 2.0, inner_r(u + 2.0) - 0.25, H)

    # entrance and exit: glass wall with a wide sliding-door opening, transom, white pillars, door mat
    for u, facing in ((0.4, 1), (U - 0.4, -1)):
        l, r = L(u), R(u)
        opening = 3.2
        for a, b in ((-r, -opening), (opening, l)):                         # fixed glass beside the doors
            vertical_across(m, hall, u, a, b, 0.05, 4.3, "glass")
        vertical_across(m, hall, u, -r, l, 4.3, 4.6, "steel_dark")
        vertical_across(m, hall, u, -r, l, 4.6, H, "glass")
        for o in (-opening - 0.05, opening + 0.05, -r + 0.05, l - 0.05):
            local_box(m, hall, u, o, 0.0, 4.6, 0.06, 0.05, "steel_dark")
        local_box(m, hall, u, 0.0, 4.3, 4.55, 0.1, opening, "steel_dark")
        for o in (-opening - 1.0, opening + 1.0):
            local_box(m, hall, u + 0.2 * facing, o, 0.0, 4.3, 0.04, 0.55, "glass")   # open door leaves
        horizontal(m, hall, u - 1.2 * facing, u + 1.2 * facing, -opening + 0.4, opening - 0.4, 0.04, "mat")

    # the grand staircase on the east side, running along the route, up to a landing that joins the gallery
    su0, su1 = min(14.0, U * 0.16), min(22.5, U * 0.25)
    stair(m, hall, su0, su1, 6.0, 10.4, GALLERY_Z)
    horizontal(m, hall, su1, su1 + 3.5, 6.0, lambda u: inner_l(u) + 0.01, GALLERY_Z, "stair", up=True)
    vertical(m, hall, su1, su1 + 3.5, 6.0, GALLERY_Z - 0.4, GALLERY_Z, "stair", facing=-1)
    balustrade(m, hall, su1, su1 + 3.5, 6.0, GALLERY_Z, facing=-1)

    # lounge: stage with step, curved seat row, partition boards, tables with chairs, planters
    stage(m, hall, U * 0.33 - 2.5, U * 0.33 + 2.5, 8.8, 12.0)
    bench_arc(m, hall, U * 0.33, 10.4, 4.7, -52, 52, 13)
    partition(m, hall, U * 0.33 - 3.8, 7.2)
    for u in (U * 0.5, U * 0.58, U * 0.66, U * 0.74):
        table(m, hall, u, min(6.7, inner_l(u) - 1.6))
    partition(m, hall, U * 0.5 - 1.8, 5.9)
    partition(m, hall, U * 0.74 + 1.9, 6.0)
    for k, u in enumerate(hall.samples(5.0, U - 5.0, 3.6)):                  # troughs along the west wall
        if R(u) >= 5.6:
            planter(m, hall, u, -(R(u) - 0.55), 11 + k)
    for k, u in enumerate((6.0, 9.5, U - 8.0, U - 11.5)):                    # and near the east wall by the doors
        planter(m, hall, u, L(u) - 0.8, 40 + k)

    obj = m.to_object("Interior_Atrium", col)
    print(f"  atrium: {U:.0f} m hall, {len(obj.data.polygons)} faces")


def vertical_across(m, hall, u, o0, o1, z0, z1, material):
    """Wall panel across the hall at route position u from lateral o0 to o1."""
    a, b = hall.at(u, o0, z0), hall.at(u, o1, z0)
    t = hall.tangent(u)
    for sign in (1, -1):
        m.face([a, b, (b[0], b[1], hall.at(u, o1, z1)[2]), (a[0], a[1], hall.at(u, o0, z1)[2])], material, (t[0] * sign, t[1] * sign, 0))


# ---------- the short covered stretches (first version) ----------
def frame(run, k):
    a, b = run[max(k - 1, 0)], run[min(k + 1, len(run) - 1)]
    dx, dy = b["x"] - a["x"], b["y"] - a["y"]
    d = math.hypot(dx, dy) or 1.0
    return (dx / d, dy / d), (-dy / d, dx / d)


def box(bm, centre, t, nrm, half_t, half_n, z0, z1, mat):
    cx, cy = centre
    corners = []
    for a, s in ((-half_t, -half_n), (half_t, -half_n), (half_t, half_n), (-half_t, half_n)):
        corners.append((cx + t[0] * a + nrm[0] * s, cy + t[1] * a + nrm[1] * s))
    low = [bm.verts.new((x, y, z0)) for x, y in corners]
    high = [bm.verts.new((x, y, z1)) for x, y in corners]
    for k in range(4):
        f = bm.faces.new((low[k], low[(k + 1) % 4], high[(k + 1) % 4], high[k]))
        f.material_index = mat
    bm.faces.new(high).material_index = mat


def build_simple(runs, passage_width, col):
    runs = [[p for p in run if p["building"]] for run in runs]
    runs = [run for run in runs if len(run) >= 10]
    if not runs:
        return
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    half = passage_width / 2
    for run in runs:
        for k, p in enumerate(run):
            t, nrm = frame(run, k)
            floor = p["track_z"]
            if k % SIMPLE_COLUMN_EVERY == 0:
                for side in (1, -1):
                    c = (p["x"] + nrm[0] * side * (half - 0.6), p["y"] + nrm[1] * side * (half - 0.6))
                    box(bm, c, t, nrm, SIMPLE_COLUMN / 2, SIMPLE_COLUMN / 2, floor, floor + SIMPLE_HEIGHT, 0)
            if k % 3 == 0:                                   # ceiling light panel every 6 m
                corners = [(p["x"] + t[0] * a + nrm[0] * s, p["y"] + t[1] * a + nrm[1] * s, floor + SIMPLE_HEIGHT - 0.05)
                           for a, s in ((-1.2, -0.6), (-1.2, 0.6), (1.2, 0.6), (1.2, -0.6))]
                f = bm.faces.new([bm.verts.new(c) for c in corners])
                f.material_index = 2
            if k + 1 < len(run):                             # glazing on both walls, slightly in front of them
                q = run[k + 1]
                _, nq = frame(run, k + 1)
                for side in (1, -1):
                    off = side * (half - 0.08)
                    vs = [(p["x"] + nrm[0] * off, p["y"] + nrm[1] * off, floor + 0.2),
                          (q["x"] + nq[0] * off, q["y"] + nq[1] * off, q["track_z"] + 0.2),
                          (q["x"] + nq[0] * off, q["y"] + nq[1] * off, q["track_z"] + 4.2),
                          (p["x"] + nrm[0] * off, p["y"] + nrm[1] * off, floor + 4.2)]
                    face = bm.faces.new([bm.verts.new(v) for v in (vs if side == 1 else vs[::-1])])
                    face.material_index = 1
                    u0, u1 = p["s"] / 6.0, q["s"] / 6.0
                    for loop, c in zip(face.loops, ((u0, 0), (u1, 0), (u1, 1), (u0, 1)) if side == 1 else ((u0, 1), (u1, 1), (u1, 0), (u0, 0))):
                        loop[uv].uv = c
    bm.normal_update()
    for f in bm.faces:
        if f.material_index == 2 and f.normal.z > 0:
            f.normal_flip()                                  # light panels face down
    mesh = bpy.data.meshes.new("Interior_Passages")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("B_interior_column", "#F4F3F0", 0.6))
    mesh.materials.append(E.material("B_glass"))
    mesh.materials.append(E.material("interior_light", "#FFF8EA", 0.5, emission="#FFF8EA"))
    E.new_object("Interior_Passages", mesh, col)


def dress(runs, passage_width, extents_per_run=None):
    """runs: route pieces inside the drive-through buildings (already extended); extents_per_run: hall widths for
    the atrium run (see extents()), None for the short covered stretches."""
    if not runs:
        return
    col = E.collection("World_Interiors")
    simple = []
    for k, run in enumerate(runs):
        ext = extents_per_run[k] if extents_per_run else None
        if ext is not None:
            build_atrium(run, ext, col)
        else:
            simple.append(run)
    build_simple(simple, passage_width, col)
