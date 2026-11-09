"""Small building kit for the Nova interior (nova_hall.py): one mesh with named materials, a straight coordinate
frame, and furniture/structure parts. Plain Python + bmesh, no Blender operators, so it is easy to read.

Coordinates: a Frame turns (u, o, z) into world positions: u = metres forward along the hall, o = metres to the
LEFT of that axis, z = metres above the floor. Every part below takes (u, o, z) so nothing is placed by hand.
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

import env_common as E


# ---------- materials (all named N_*; the Unity importer reads colour/emission from the FBX material names) ----------
def _mat(name, colour, rough=0.6, metal=0.0, emission=None, alpha=1.0):
    def make():
        mat = E.material(name, colour, rough, metal, emission, alpha)
        if alpha < 1.0 and hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "BLENDED"        # only the Blender preview needs this; Unity reads the alpha value
        return mat
    return make


MATERIALS = {
    "floor": _mat("N_floor", "#6A5B43", 0.28),                 # polished concrete, warm brown-grey (the videos)
    "wall": _mat("N_wall", "#E9E7E1", 0.85),
    "soffit": _mat("N_soffit", "#F4F3EF", 0.9),
    "steel_white": _mat("N_steel_white", "#ECECE8", 0.5, 0.3),
    "steel_dark": _mat("N_steel_dark", "#2B2E33", 0.4, 0.6),
    "deck": _mat("N_deck", "#85888E", 0.6, 0.4),               # roof deck underside, grey corrugated sheet
    "timber": _mat("N_timber", "#A8643A", 0.55),               # the orange-brown wooden joists under the gallery
    "glass": _mat("N_glass", "#BCD6E6", 0.05, 0.0, None, 0.22),
    "black": _mat("N_black", "#1E1F22", 0.7),
    "grey": _mat("N_grey", "#3A3D44", 0.9),
    "board": _mat("N_board", "#34405A", 0.9),                  # dark blue partition boards
    "table": _mat("N_table", "#F2F1ED", 0.45),
    "leaf": _mat("N_leaf", "#3F6B3A", 0.9),
    "palm": _mat("N_palm", "#4F7F3C", 0.9),
    "pot": _mat("N_pot", "#47484B", 0.8),
    "rail": _mat("N_rail", "#B9BCC2", 0.3, 0.8),
    "mat": _mat("N_door_mat", "#2A2B2E", 0.95),
    "light": _mat("N_light", "#FFF6E2", 0.5, emission="#FFF6E2"),
    "exit": _mat("N_exit_sign", "#2FB36B", 0.5, emission="#2FB36B"),
    "brown": _mat("N_brown_wall", "#5B4638", 0.8),             # dark brown recessed wall in the study hall
    "piano": _mat("N_piano", "#101113", 0.2, 0.2),
    "red": _mat("N_art_red", "#D84A3A", 0.8),
    "blue": _mat("N_art_blue", "#2F5FA8", 0.8),
    "yellow": _mat("N_art_yellow", "#E9B93A", 0.8),
    "concrete": _mat("N_terrace", "#C9C6BE", 0.85),
    "skylight": _mat("N_skylight", "#DDE9F2", 0.3, emission="#C9DBEA"),     # glass roof bands, lit by the sky
    "stair": _mat("N_stair", "#F2F1EC", 0.55),                               # white stairs and gallery floors
    "azulejo": _mat("N_azulejo_pot", "#EEF1F6", 0.4),                        # white tall planters (blue pattern = "blue")
    "pit": _mat("N_pit", "#3A3530", 0.9),                                    # walls and floor of the stair void
}


class Mesh:
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

    def beam(self, a, b, w, h, material, up=(0, 0, 1)):
        """Bar from point a to point b with a w x h rectangular section (h along `up`). Used for steel, joists, rods."""
        a, b = Vector(a), Vector(b)
        axis = b - a
        if axis.length < 1e-6:
            return
        t = axis.normalized()
        side = t.cross(Vector(up))
        if side.length < 1e-6:
            side = t.cross(Vector((1, 0, 0)))
        side.normalize()
        vert = side.cross(t).normalized()
        corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
        ra = [a + side * sx * w / 2 + vert * sy * h / 2 for sx, sy in corners]
        rb = [b + side * sx * w / 2 + vert * sy * h / 2 for sx, sy in corners]
        for i in range(4):
            j = (i + 1) % 4
            mid = (ra[i] + ra[j] + rb[i] + rb[j]) / 4 - (a + b) / 2
            self.face([ra[i], ra[j], rb[j], rb[i]], material, mid)
        self.face(ra[::-1], material, -t)
        self.face(rb, material, t)

    def tube(self, a, b, r, material, sides=10):
        """Round tube from a to b (columns, steel pipes)."""
        a, b = Vector(a), Vector(b)
        axis = b - a
        if axis.length < 1e-6:
            return
        t = axis.normalized()
        side = t.cross(Vector((0, 0, 1)))
        if side.length < 1e-3:
            side = Vector((1, 0, 0))
        side.normalize()
        up = side.cross(t).normalized()
        ra = [a + (side * math.cos(2 * math.pi * i / sides) + up * math.sin(2 * math.pi * i / sides)) * r for i in range(sides)]
        rb = [p + axis for p in ra]
        for i in range(sides):
            j = (i + 1) % sides
            self.face([ra[i], ra[j], rb[j], rb[i]], material, (ra[i] + ra[j]) / 2 - a)

    def box(self, c, hx, hy, hz, material, rot=0.0):
        cs, sn = math.cos(rot), math.sin(rot)

        def w(dx, dy, dz):
            return (c[0] + dx * cs - dy * sn, c[1] + dx * sn + dy * cs, c[2] + dz)

        v = [w(sx * hx, sy * hy, sz * hz) for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        for idx, n in (((0, 1, 3, 2), (0, 0, -1)), ((4, 6, 7, 5), (0, 0, 1)), ((0, 4, 5, 1), (0, -1, 0)),
                       ((2, 3, 7, 6), (0, 1, 0)), ((0, 2, 6, 4), (-1, 0, 0)), ((1, 5, 7, 3), (1, 0, 0))):
            self.face([v[i] for i in idx], material, (n[0] * cs - n[1] * sn, n[0] * sn + n[1] * cs, n[2]))

    def cylinder(self, base, radius, height, material, sides=12, top_radius=None):
        top_radius = radius if top_radius is None else top_radius
        r0 = [(base[0] + radius * math.cos(2 * math.pi * i / sides), base[1] + radius * math.sin(2 * math.pi * i / sides), base[2])
              for i in range(sides)]
        r1 = [(base[0] + top_radius * math.cos(2 * math.pi * i / sides), base[1] + top_radius * math.sin(2 * math.pi * i / sides),
               base[2] + height) for i in range(sides)]
        for i in range(sides):
            j = (i + 1) % sides
            self.face([r0[i], r0[j], r1[j], r1[i]], material, ((r0[i][0] + r0[j][0]) / 2 - base[0], (r0[i][1] + r0[j][1]) / 2 - base[1], 0))
        self.face(r1, material, (0, 0, 1))
        self.face(r0[::-1], material, (0, 0, -1))

    def ring(self, c, r_out, r_in, height, material, sides=28, basis=None):
        """Ring (annulus) extruded `height`. basis = (ex, ey, ez) vectors: the ring lies in ex/ey and is extruded along ez
        (default: standing on the floor, axis = z)."""
        ex, ey, ez = [Vector(v) for v in (basis or ((1, 0, 0), (0, 1, 0), (0, 0, 1)))]
        c = Vector(c)

        def P(r, a, h):
            return c + ex * (r * math.cos(a)) + ey * (r * math.sin(a)) + ez * h

        for i in range(sides):
            a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
            am = (a0 + a1) / 2
            for r, n in ((r_out, 1), (r_in, -1)):
                self.face([P(r, a0, 0), P(r, a1, 0), P(r, a1, height), P(r, a0, height)], material, (ex * math.cos(am) + ey * math.sin(am)) * n)
            self.face([P(r_out, a0, height), P(r_out, a1, height), P(r_in, a1, height), P(r_in, a0, height)], material, ez)
            self.face([P(r_out, a0, 0), P(r_out, a1, 0), P(r_in, a1, 0), P(r_in, a0, 0)], material, -ez)

    def blob(self, c, radius, material, seed, squash=1.0):
        """Lumpy low-poly sphere (foliage)."""
        rnd = random.Random(seed)
        geo = bmesh.ops.create_icosphere(self.bm, subdivisions=2, radius=radius)
        idx = self.mat(material)
        for v in geo["verts"]:
            v.co = Vector((c[0] + v.co.x * (0.85 + rnd.random() * 0.3), c[1] + v.co.y * (0.85 + rnd.random() * 0.3),
                           c[2] + v.co.z * squash * (0.85 + rnd.random() * 0.3)))
        for f in {f for v in geo["verts"] for f in v.link_faces}:
            f.material_index = idx

    def to_object(self, name, col):
        ngons = [f for f in self.bm.faces if len(f.verts) > 4]
        if ngons:
            bmesh.ops.triangulate(self.bm, faces=ngons, quad_method="BEAUTY", ngon_method="EAR_CLIP")
        self.bm.normal_update()
        uv = self.bm.loops.layers.uv.verify()
        for f in self.bm.faces:                       # planar UVs in metres
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
        faces = len(mesh.polygons)
        self.bm.free()
        for n in self.names:
            mesh.materials.append(MATERIALS[n]())
        obj = E.new_object(name, mesh, col)
        obj["triangles"] = faces
        return obj


class Frame:
    """Straight axis: u forward, o left, z up (floor = z 0)."""

    def __init__(self, x=0.0, y=0.0, heading_deg=90.0):
        self.x, self.y, self.h = x, y, math.radians(heading_deg)

    def at(self, u, o, z=0.0):
        t = (math.cos(self.h), math.sin(self.h))
        n = (-t[1], t[0])
        return (self.x + t[0] * u + n[0] * o, self.y + t[1] * u + n[1] * o, z)

    def uo(self, x, y):
        """World (x, y) -> hall coordinates (u, o)."""
        t = (math.cos(self.h), math.sin(self.h))
        dx, dy = x - self.x, y - self.y
        return (dx * t[0] + dy * t[1], -dx * t[1] + dy * t[0])

    def tangent(self, u=0.0):
        return (math.cos(self.h), math.sin(self.h))

    def left(self, u=0.0):
        return (-math.sin(self.h), math.cos(self.h))

    def heading(self, u=0.0):
        return self.h

    def samples(self, u0, u1, step=2.0):
        n = max(1, int(math.ceil((u1 - u0) / step)))
        return [u0 + (u1 - u0) * i / n for i in range(n + 1)]


# ---------- polygons (footprints) ----------
def clip_polygon(poly, keep):
    """Sutherland-Hodgman clip of a polygon by a half plane. keep(p) returns a signed value, >= 0 = keep.
    Points on the cut get the exact crossing position."""
    out = []
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        ka, kb = keep(a), keep(b)
        if ka >= 0:
            out.append(a)
        if (ka >= 0) != (kb >= 0):
            t = ka / (ka - kb)
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out


def signed_area(poly):
    return 0.5 * sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(poly, poly[1:] + poly[:1]))


def polygon_face(m, poly, z, material, up=True):
    m.face([(x, y, z) for x, y in (poly if up == (signed_area(poly) > 0) else poly[::-1])], material, (0, 0, 1 if up else -1))


# ---------- surfaces ----------
def floor_rect(m, f, u0, u1, o0, o1, z, material, up=True):
    m.face([f.at(u0, o0, z), f.at(u1, o0, z), f.at(u1, o1, z), f.at(u0, o1, z)], material, (0, 0, 1 if up else -1))


def wall_u(m, f, u, o0, o1, z0, z1, material, facing=1):
    """Wall across the hall at position u (normal along +u when facing = 1)."""
    t = f.tangent()
    m.face([f.at(u, o0, z0), f.at(u, o1, z0), f.at(u, o1, z1), f.at(u, o0, z1)], material, (t[0] * facing, t[1] * facing, 0))


def wall_o(m, f, o, u0, u1, z0, z1, material, facing=1):
    """Wall along the hall at lateral offset o (normal towards +o when facing = 1)."""
    n = f.left()
    m.face([f.at(u0, o, z0), f.at(u1, o, z0), f.at(u1, o, z1), f.at(u0, o, z1)], material, (n[0] * facing, n[1] * facing, 0))


def block(m, f, u0, u1, o0, o1, z0, z1, material):
    """Solid axis-aligned block in hall coordinates."""
    c = f.at((u0 + u1) / 2, (o0 + o1) / 2, (z0 + z1) / 2)
    m.box(c, (u1 - u0) / 2, (o1 - o0) / 2, (z1 - z0) / 2, material, f.h - math.pi / 2 + math.pi / 2)


def rod(m, f, a, b, w, material, h=None):
    """Beam between two hall points a = (u, o, z), b = (u, o, z)."""
    m.beam(f.at(*a), f.at(*b), w, h or w, material)


# ---------- structure ----------
def round_column(m, f, u, o, z0, z1, r=0.25, material="wall"):
    p = f.at(u, o, z0)
    m.cylinder(p, r, z1 - z0, material, 14)


def square_column(m, f, u, o, z0, z1, s=0.7, material="wall"):
    block(m, f, u - s / 2, u + s / 2, o - s / 2, o + s / 2, z0, z1, material)


def truss(m, f, u, o0, o1, z_bottom, z_top, bays=6):
    """Warren truss across the hall at position u: two chords and zig-zag diagonals."""
    rod(m, f, (u, o0, z_bottom), (u, o1, z_bottom), 0.12, "steel_white", 0.18)
    rod(m, f, (u, o0, z_top), (u, o1, z_top), 0.14, "steel_white", 0.2)
    for k in range(bays):
        a = o0 + (o1 - o0) * k / bays
        b = o0 + (o1 - o0) * (k + 1) / bays
        if k % 2 == 0:
            rod(m, f, (u, a, z_bottom), (u, b, z_top), 0.08, "steel_white")
        else:
            rod(m, f, (u, a, z_top), (u, b, z_bottom), 0.08, "steel_white")


# ---------- furniture ----------
def chair(m, f, u, o, face_deg):
    h = f.h + math.radians(face_deg)
    p = f.at(u, o, 0)
    c, s = math.cos(h), math.sin(h)
    m.box((p[0], p[1], 0.45), 0.22, 0.22, 0.025, "black", h)
    m.box((p[0] - 0.2 * c, p[1] - 0.2 * s, 0.76), 0.02, 0.21, 0.26, "black", h)
    for dx in (-0.18, 0.18):
        for dy in (-0.18, 0.18):
            m.box((p[0] + dx * c - dy * s, p[1] + dx * s + dy * c, 0.22), 0.015, 0.015, 0.22, "steel_dark", h)


def round_table(m, f, u, o, chairs=4):
    p = f.at(u, o, 0)
    m.cylinder((p[0], p[1], 0.72), 0.62, 0.04, "table", 20)
    m.cylinder((p[0], p[1], 0.0), 0.07, 0.72, "table", 10)
    m.cylinder((p[0], p[1], 0.0), 0.34, 0.03, "table", 16)
    for k in range(chairs):
        a = 2 * math.pi * k / chairs + 0.5
        chair(m, f, u + 0.95 * math.cos(a), o + 0.95 * math.sin(a), math.degrees(a) + 180)


def long_table(m, f, u, o, seats=3, along_u=True):
    h = f.h + (0 if along_u else math.pi / 2)
    p = f.at(u, o, 0)
    m.box((p[0], p[1], 0.74), 1.0 * seats * 0.5 + 0.1, 0.42, 0.02, "table", h)
    for sgn in (-1, 1):
        q = f.at(u + (sgn * (0.5 * seats * 1.0 - 0.1)) * (1 if along_u else 0), o + (0 if along_u else sgn * (0.5 * seats * 1.0 - 0.1)), 0)
        m.box((q[0], q[1], 0.37), 0.025, 0.4, 0.37, "table", h)
    for k in range(seats):
        d = (k - (seats - 1) / 2) * 1.0
        for side in (-1, 1):
            if along_u:
                chair(m, f, u + d, o + side * 0.72, 90 * side * -1 + 0)
            else:
                chair(m, f, u + side * 0.72, o + d, 0 if side < 0 else 180)


def planter(m, f, u, o, seed, palm=False):
    p = f.at(u, o, 0)
    m.box((p[0], p[1], 0.28), 0.7, 0.3, 0.28, "black", f.h)
    rnd = random.Random(seed)
    for k in range(2):
        q = f.at(u + (k - 0.5) * 0.7, o, 0)
        if palm:
            for j in range(7):                            # palm: a bundle of leaning fronds
                a = 2 * math.pi * j / 7 + rnd.random()
                m.beam((q[0], q[1], 0.5), (q[0] + 0.7 * math.cos(a), q[1] + 0.7 * math.sin(a), 1.5 + rnd.random() * 0.35),
                       0.1, 0.03, "palm")
            m.blob((q[0], q[1], 1.55), 0.28, "palm", seed * 5 + k, 0.8)
        else:
            m.cylinder((q[0], q[1], 0.55), 0.03, 0.7 + rnd.random() * 0.3, "steel_dark", 5)
            for j in range(3):
                m.blob((q[0] + (j - 1) * 0.22, q[1] + (j % 2 - 0.5) * 0.2, 1.5 + j * 0.1 + rnd.random() * 0.2),
                       0.34 + rnd.random() * 0.08, "leaf", seed * 7 + k * 3 + j, 0.9)


def big_plant(m, f, u, o, seed):
    """Fiddle-leaf fig in a round grey pot."""
    p = f.at(u, o, 0)
    m.cylinder((p[0], p[1], 0), 0.38, 0.55, "pot", 14, top_radius=0.45)
    rnd = random.Random(seed)
    m.cylinder((p[0], p[1], 0.55), 0.04, 1.2, "steel_dark", 6)
    for j in range(7):
        a = 2 * math.pi * j / 7
        m.blob((p[0] + 0.35 * math.cos(a), p[1] + 0.35 * math.sin(a), 1.7 + rnd.random() * 0.6), 0.32 + rnd.random() * 0.1, "leaf", seed * 11 + j, 0.7)
    m.blob((p[0], p[1], 2.3), 0.4, "leaf", seed * 13, 0.8)


def board(m, f, u, o, width=1.8, height=2.0, material="board"):
    """Free-standing partition board on a foot."""
    block(m, f, u - 0.03, u + 0.03, o - width / 2, o + width / 2, 0.15, 0.15 + height, material)
    block(m, f, u - 0.25, u + 0.25, o - 0.04, o + 0.04, 0.0, 0.15, "steel_dark")


def grand_piano(m, f, u, o, turn_deg=0):
    h = f.h + math.radians(turn_deg)
    p = f.at(u, o, 0)
    c, s = math.cos(h), math.sin(h)
    m.box((p[0], p[1], 0.9), 1.0, 0.7, 0.12, "piano", h)                  # body
    m.box((p[0] + 0.5 * c, p[1] + 0.5 * s, 0.82), 0.5, 0.58, 0.05, "piano", h)
    for dx, dy in ((0.8, 0.5), (0.8, -0.5), (-0.7, 0.0)):
        m.box((p[0] + dx * c - dy * s, p[1] + dx * s + dy * c, 0.39), 0.05, 0.05, 0.39, "piano", h)
    m.box((p[0] - 1.15 * c, p[1] - 1.15 * s, 0.45), 0.2, 0.3, 0.02, "black", h)    # stool


def desk(m, f, u, o, length=6.0, depth=1.2):
    block(m, f, u - depth / 2, u + depth / 2, o - length / 2, o + length / 2, 0.0, 1.05, "grey")
    block(m, f, u - depth / 2 - 0.02, u + depth / 2 + 0.02, o - length / 2 - 0.02, o + length / 2 + 0.02, 1.05, 1.1, "table")


def downlight(m, f, u, o, z):
    p = f.at(u, o, z)
    m.cylinder((p[0], p[1], z - 0.02), 0.11, 0.02, "light", 10)


def info_stand(m, f, u, o, seed):
    """Tall white sign board with a coloured graphic."""
    block(m, f, u - 0.04, u + 0.04, o - 0.45, o + 0.45, 0.0, 2.1, "table")
    block(m, f, u - 0.05, u + 0.05, o - 0.35, o + 0.35, 0.9, 1.9, ("blue", "red", "yellow")[seed % 3])
