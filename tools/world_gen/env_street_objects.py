"""Street section (V10f): the things that stand up. Built from street_plan.py by env_streets.build().

Static meshes (collection World_Streets):
  Street_Structures       walls, hedges, the entrance portal with its stair, the planted bank, steel frames, the
                          roundabout's five corten beds ... (mesh collider in Unity like every world mesh)
  Street_NoCollide_Kerbs  low kerbs: the Marginal's median, island kerbs (no collider: see env_streets)
Prototypes (Proto_Props / Proto_Vegetation) placed through the dressing files so Unity instances them: parked cars,
bollards, lamps, masts, signs (generic pictograms, no text), bins, the tall fan palms and slim date palms, saplings.
dress() drops the older generated plants and props that stand where the streets now are and adds ours.
"""
import json
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector

import env_common as E
import env_streets as ST
import street_plan as S

SEED = 20261111


# =====================================================================================================
# Mesh builders
# =====================================================================================================
class Solid:
    """Faces with their own vertices (flat shading), a material key per face; palette faces carry a colour name."""
    TILE = {"wall_white": 3.0, "roof": 3.0, "hedge": 2.0, "rust": 2.0, "slats": 0.6, "lawn": 2.0, "concrete": 4.0,
            "calcada": 1.0, "setts": 2.0, "asphalt": 2.0}

    def __init__(self):
        self.verts, self.faces = [], []              # faces: (indices, key, colour or None)

    def face(self, corners, key, out=None, colour=None):
        pts = [Vector(c) for c in corners]
        if out is not None:
            n = Vector((0.0, 0.0, 0.0))
            for a, b in zip(pts, pts[1:] + pts[:1]):
                n.x += (a.y - b.y) * (a.z + b.z)
                n.y += (a.z - b.z) * (a.x + b.x)
                n.z += (a.x - b.x) * (a.y + b.y)
            if n.dot(Vector(out)) < 0:
                pts.reverse()
        base = len(self.verts)
        self.verts.extend(pts)
        self.faces.append((list(range(base, base + len(pts))), "palette" if colour else key, colour))

    def obox(self, c, u, half_u, half_v, z0, z1, key, skip=("bottom",), colour=None, z0b=None, z1b=None):
        """Box centred at c, long axis u, half sizes along u and across; z0..z1 (z0b/z1b: heights at the +u end)."""
        ux, uy = u
        vx, vy = -uy, ux
        z0b = z0 if z0b is None else z0b
        z1b = z1 if z1b is None else z1b

        def p(a, b, top):
            z = (z1 if a < 0 else z1b) if top else (z0 if a < 0 else z0b)
            return (c[0] + ux * a * half_u + vx * b * half_v, c[1] + uy * a * half_u + vy * b * half_v, z)
        sides = {"+u": ((1, -1), (1, 1), (ux, uy)), "-u": ((-1, 1), (-1, -1), (-ux, -uy)),
                 "+v": ((1, 1), (-1, 1), (vx, vy)), "-v": ((-1, -1), (1, -1), (-vx, -vy))}
        for name, (k0, k1, out) in sides.items():
            if name not in skip:
                self.face([p(*k0, False), p(*k1, False), p(*k1, True), p(*k0, True)], key, (out[0], out[1], 0.0), colour)
        ring = ((-1, -1), (1, -1), (1, 1), (-1, 1))
        if "top" not in skip:
            self.face([p(a, b, True) for a, b in ring], key, (0, 0, 1), colour)
        if "bottom" not in skip:
            self.face([p(a, b, False) for a, b in ring], key, (0, 0, -1), colour)

    def beam(self, a, b, w, h, key, colour=None, ends=True):
        a, b = Vector(a), Vector(b)
        d = b - a
        flat = Vector((d.x, d.y, 0.0))
        side = Vector((-flat.y, flat.x, 0.0)).normalized() * (w / 2) if flat.length > 1e-6 else Vector((w / 2, 0, 0))
        up = d.cross(side).normalized() * (h / 2)
        if up.z < 0:
            up = -up
        for (s0, t0), (s1, t1) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
            o0, o1 = side * s0 + up * t0, side * s1 + up * t1
            self.face([a + o0, a + o1, b + o1, b + o0], key, (o0 + o1) / 2, colour)
        if ends:
            ring = [side * s + up * t for s, t in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            self.face([a + o for o in ring], key, -d, colour)
            self.face([b + o for o in ring], key, d, colour)

    def post(self, x, y, z0, z1, size, key, colour=None):
        self.obox((x, y), (1.0, 0.0), size / 2, size / 2, z0, z1, key, colour=colour)

    def wall(self, line, thick, z0, z1, key, colour=None, step=4.0, side=0.0):
        """Wall along a polyline: z0(x, y), z1(x, y) give foot and top; `side` shifts it to the left of the line."""
        total = S.line_length(line)
        n = max(1, int(math.ceil(total / step)))
        pts = [S.at_s(line, total * k / n) for k in range(n + 1)]
        for (pa, ua), (pb, _ub) in zip(pts, pts[1:]):
            u = S.unit(pb[0] - pa[0], pb[1] - pa[1])
            length = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
            c = ((pa[0] + pb[0]) / 2 - u[1] * side, (pa[1] + pb[1]) / 2 + u[0] * side)
            self.obox(c, u, length / 2 + 0.01, thick / 2, z0(*pa), z1(*pa), key, colour=colour, z0b=z0(*pb), z1b=z1(*pb))

    def mesh(self, name):
        keys = []
        for _f, key, _c in self.faces:
            if key not in keys:
                keys.append(key)
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], [f[0] for f in self.faces])
        for k in keys:
            mesh.materials.append(ST.mat(k))
        mesh.polygons.foreach_set("material_index", [keys.index(f[1]) for f in self.faces])
        uv = mesh.uv_layers.new(name="UVMap")
        for poly, (_idx, key, colour) in zip(mesh.polygons, self.faces):
            if colour:
                t = ST.pal_uv(colour)
                for li in poly.loop_indices:
                    uv.data[li].uv = t
                continue
            n = poly.normal
            t = self.TILE.get(key, 2.0)
            h = math.hypot(n.x, n.y) or 1.0
            for li in poly.loop_indices:
                co = mesh.vertices[mesh.loops[li].vertex_index].co
                if abs(n.z) > 0.7:
                    uv.data[li].uv = (co.x / t, co.y / t)
                else:
                    uv.data[li].uv = ((co.x * -n.y / h + co.y * n.x / h) / t, co.z / t)
        mesh.validate()
        mesh.update()
        return mesh


def surface(x, y):
    """Top of the paving if there is paving, else the ground."""
    h = ST.paving_height(x, y)
    return ST.ground(x, y) if h is None else h


class Proto(Solid):
    """A prop prototype at the origin: palette colours only."""

    def box(self, x0, x1, y0, y1, z0, z1, colour, skip=("bottom",)):
        self.obox(((x0 + x1) / 2, (y0 + y1) / 2), (1.0, 0.0), (x1 - x0) / 2, (y1 - y0) / 2, z0, z1, "palette", skip, colour)

    def cyl(self, r0, r1, z0, z1, n, colour, x=0.0, y=0.0, cap=True, phase=0.0):
        a = [(x + r0 * math.cos(phase + 2 * math.pi * k / n), y + r0 * math.sin(phase + 2 * math.pi * k / n), z0)
             for k in range(n)]
        b = [(x + r1 * math.cos(phase + 2 * math.pi * k / n), y + r1 * math.sin(phase + 2 * math.pi * k / n), z1)
             for k in range(n)]
        for k in range(n):
            k1 = (k + 1) % n
            mid = ((a[k][0] + a[k1][0]) / 2 - x, (a[k][1] + a[k1][1]) / 2 - y, 0.0)
            self.face([a[k], a[k1], b[k1], b[k]], "palette", mid, colour)
        if cap:
            self.face(b, "palette", (0, 0, 1), colour)

    def disc_y(self, x, y, z, r, n, colour, facing):
        """Flat n-gon in the x-z plane at depth y, facing +y or -y."""
        pts = [(x + r * math.cos(2 * math.pi * (k + 0.5) / n), y, z + r * math.sin(2 * math.pi * (k + 0.5) / n))
               for k in range(n)]
        self.face(pts, "palette", (0, facing, 0), colour)

    def plate(self, pts_xz, y, thick, colour, back=None):
        """Thin plate in the x-z plane (a sign): front face at y, back face at y - thick."""
        self.face([(px, y, pz) for px, pz in pts_xz], "palette", (0, 1, 0), colour)
        self.face([(px, y - thick, pz) for px, pz in pts_xz], "palette", (0, -1, 0), back or colour)

    def build(self, name, col):
        obj = E.new_object(name, self.mesh(name), col)
        return obj


def ngon(cx, cz, r, n, phase=0.0):
    return [(cx + r * math.cos(phase + 2 * math.pi * k / n), cz + r * math.sin(phase + 2 * math.pi * k / n))
            for k in range(n)]


# =====================================================================================================
# Prototypes
# =====================================================================================================
def car(name, col, colour, kind):
    """Generic low-poly car, nose along +x, about 80 triangles: body profile, glass, dark wheels. No make, no plate."""
    length, width, roof, bonnet = {"hatch": (3.95, 1.74, 1.46, 0.86), "sedan": (4.5, 1.8, 1.43, 0.84),
                                   "suv": (4.45, 1.86, 1.66, 1.0)}[kind]
    h = length / 2
    belt = bonnet + 0.06
    if kind == "sedan":
        prof = [(-h, 0.3), (h, 0.3), (h, 0.62), (h - 0.35, bonnet - 0.06), (0.75, bonnet), (0.1, roof), (-1.05, roof),
                (-1.6, belt), (-h + 0.1, belt - 0.02), (-h, 0.66)]
        glass = (4, 6)
        win = [(0.55, belt), (0.0, roof - 0.06), (-1.0, roof - 0.06), (-1.45, belt)]
    else:
        prof = [(-h, 0.3), (h, 0.3), (h, 0.64), (h - 0.3, bonnet - 0.05), (0.8, bonnet), (0.2, roof), (-h + 0.45, roof),
                (-h + 0.08, belt + 0.1), (-h, 0.62)]
        glass = (4, 6)
        win = [(0.62, belt), (0.1, roof - 0.06), (-h + 0.55, roof - 0.06), (-h + 0.3, belt)]
    g = Proto()
    w = width / 2
    n = len(prof)
    for k in range(n):
        (xa, za), (xb, zb) = prof[k], prof[(k + 1) % n]
        out = (zb - za, 0.0, -(xb - xa))
        g.face([(xa, -w, za), (xb, -w, zb), (xb, w, zb), (xa, w, za)], "palette", out, "glass" if k in glass else colour)
    g.face([(x, w, z) for x, z in prof], "palette", (0, 1, 0), colour)
    g.face([(x, -w, z) for x, z in prof], "palette", (0, -1, 0), colour)
    for side in (1, -1):
        g.face([(x, side * (w + 0.012), z) for x, z in win], "palette", (0, side, 0), "glass")
        for wx in (h - 0.78, -h + 0.72):
            g.disc_y(wx, side * (w + 0.015), 0.31, 0.31, 8, "tyre", side)
    g.box(-h + 0.25, h - 0.25, -w + 0.1, w - 0.1, 0.12, 0.3, "tyre", skip=("top",))
    return g.build(name, col)


def bollard_square(name, col):
    g = Proto()
    g.box(-0.1, 0.1, -0.1, 0.1, -0.15, 1.05, "bollard_pale")
    return g.build(name, col)


def bollard_banded(name, col):
    g = Proto()
    for z0, z1, c in ((-0.15, 0.45, "black"), (0.45, 0.55, "yellow"), (0.55, 0.68, "black"), (0.68, 0.78, "yellow"),
                      (0.78, 0.86, "black")):
        g.cyl(0.07, 0.07, z0, z1, 5, c, cap=z1 > 0.8)
    return g.build(name, col)


def timber_post(name, col):
    g = Proto()
    g.box(-0.07, 0.07, -0.07, 0.07, -0.15, 0.65, "timber")
    return g.build(name, col)


def lamp_arm(name, col, height, arms, colour="lamp_sand", reach=1.8):
    """Street lamp: tapered pole, one or two straight arms along +x (and -x), flat luminaires."""
    g = Proto()
    g.cyl(0.16, 0.14, -0.15, 0.9, 6, colour, cap=False)
    g.cyl(0.125, 0.07, 0.9, height, 6, colour)
    for sign in (1, -1)[:arms]:
        g.beam((0, 0, height - 0.25), (sign * reach, 0, height + 0.05), 0.08, 0.08, "palette", colour)
        g.obox((sign * (reach + 0.25), 0.0), (1.0, 0.0), 0.42, 0.14, height - 0.02, height + 0.1, "palette", (), colour)
        g.obox((sign * (reach + 0.25), 0.0), (1.0, 0.0), 0.34, 0.1, height - 0.04, height - 0.02, "palette", ("top",), "light")
    return g.build(name, col)


def lamp_globe(name, col):
    g = Proto()
    g.cyl(0.07, 0.05, -0.15, 3.3, 6, "steel_grey", cap=False)
    for z0, z1, r0, r1 in ((3.25, 3.4, 0.12, 0.24), (3.4, 3.62, 0.24, 0.24), (3.62, 3.78, 0.24, 0.1)):
        g.cyl(r0, r1, z0, z1, 8, "light", cap=z1 > 3.7)
    return g.build(name, col)


def high_mast(name, col, height=17.0):
    """Floodlight mast: plinth, tapered shaft, a ring of six floodlights at the top."""
    g = Proto()
    g.box(-0.5, 0.5, -0.5, 0.5, -0.2, 0.5, "render_white")
    g.cyl(0.23, 0.11, 0.5, height, 8, "mast")
    g.cyl(0.5, 0.5, height - 0.5, height - 0.38, 8, "steel_grey")
    for k in range(6):
        a = 2 * math.pi * k / 6
        g.obox((0.62 * math.cos(a), 0.62 * math.sin(a)), (math.cos(a), math.sin(a)), 0.12, 0.2, height - 0.75, height - 0.4,
               "palette", (), "steel_grey")
    return g.build(name, col)


def sign_pole(g, top, x=0.0):
    g.cyl(0.035, 0.035, -0.15, top, 6, "steel_grey", x=x)


def sign_stop(name, col):
    """Stop octagon (plain red with a white rim, no lettering) under a round blue direction sign; faces +y."""
    g = Proto()
    sign_pole(g, 3.0)
    g.plate(ngon(0, 2.05, 0.34, 8, math.pi / 8), 0.06, 0.03, "white", "grey_back")
    g.plate(ngon(0, 2.05, 0.29, 8, math.pi / 8), 0.075, 0.01, "sign_red", "sign_red")
    g.plate(ngon(0, 2.72, 0.3, 12), 0.06, 0.03, "sign_blue", "grey_back")
    g.plate([(-0.17, 2.69), (0.02, 2.69), (0.02, 2.62), (0.18, 2.72), (0.02, 2.82), (0.02, 2.75), (-0.17, 2.75)], 0.075,
            0.01, "white", "white")
    return g.build(name, col)


def sign_round(name, col, triangle=False):
    """Round sign with a red ring (blank white centre), optionally a warning triangle above; faces +y."""
    g = Proto()
    sign_pole(g, 3.0 if triangle else 2.6)
    g.plate(ngon(0, 2.25, 0.3, 12), 0.06, 0.03, "sign_red", "grey_back")
    g.plate(ngon(0, 2.25, 0.22, 12), 0.075, 0.01, "white", "white")
    if triangle:
        g.plate([(-0.36, 2.62), (0.36, 2.62), (0.0, 3.24)], 0.06, 0.03, "sign_red", "grey_back")
        g.plate([(-0.25, 2.685), (0.25, 2.685), (0.0, 3.115)], 0.075, 0.01, "white", "white")
    return g.build(name, col)


def sign_parking(name, col):
    """Blue square parking sign: a white block letter P as the generic pictogram; faces +y."""
    g = Proto()
    sign_pole(g, 2.5)
    g.plate([(-0.3, 1.9), (0.3, 1.9), (0.3, 2.5), (-0.3, 2.5)], 0.06, 0.03, "sign_blue", "grey_back")
    # (seen from the front, +y, the viewer's right is -x: the letter's stem stands at +x)
    for x0, x1, z0, z1 in ((0.05, 0.14, 1.98, 2.42), (-0.14, 0.05, 2.34, 2.42), (-0.14, -0.06, 2.2, 2.34),
                           (-0.14, 0.05, 2.14, 2.2)):
        g.plate([(x0, z0), (x1, z0), (x1, z1), (x0, z1)], 0.075, 0.01, "white", "white")
    return g.build(name, col)


def sign_crossing(name, col):
    """Blue square with a white triangle (pedestrian crossing, no figure); faces +y."""
    g = Proto()
    sign_pole(g, 2.6)
    g.plate([(-0.3, 2.0), (0.3, 2.0), (0.3, 2.6), (-0.3, 2.6)], 0.06, 0.03, "sign_blue", "grey_back")
    g.plate([(-0.22, 2.08), (0.22, 2.08), (0.0, 2.5)], 0.075, 0.01, "white", "white")
    return g.build(name, col)


def direction_board(name, col, width=3.0, height=2.5, under=2.0):
    """Big direction board on two posts: blue face with white blank bars and arrows (no place names); faces +y,
    grey back."""
    g = Proto()
    for x in (-width / 2 + 0.3, width / 2 - 0.3):
        g.cyl(0.07, 0.07, -0.15, under + height, 6, "steel_grey", x=x, y=-0.1)
    g.plate([(-width / 2, under), (width / 2, under), (width / 2, under + height), (-width / 2, under + height)], 0.0,
            0.06, "white", "grey_back")
    m = 0.07
    g.plate([(-width / 2 + m, under + m), (width / 2 - m, under + m), (width / 2 - m, under + height - m),
             (-width / 2 + m, under + height - m)], 0.012, 0.004, "sign_blue", "sign_blue")
    rows = max(2, int(height / 0.62))
    for k in range(rows):
        z = under + height - 0.45 - k * (height - 0.5) / rows
        left = k % 2 == 0
        x0 = -width / 2 + 0.75 if left else -width / 2 + 0.3
        x1 = x0 + width - 1.05 - 0.3 * (k % 3)
        g.plate([(x0, z - 0.1), (x1, z - 0.1), (x1, z + 0.1), (x0, z + 0.1)], 0.02, 0.004, "white", "white")
        ax = -width / 2 + 0.25 if left else width / 2 - 0.25
        s = 1 if left else -1
        g.plate([(ax, z), (ax + s * 0.22, z - 0.17), (ax + s * 0.22, z + 0.17)] if left else
                [(ax, z), (ax + s * 0.22, z + 0.17), (ax + s * 0.22, z - 0.17)], 0.02, 0.004, "white", "white")
    return g.build(name, col)


def billboard(name, col, width=8.0, height=3.0, under=2.6):
    """Advert hoarding on two legs with a blank pale face (no advert); faces +y."""
    g = Proto()
    for x in (-width / 4, width / 4):
        g.box(x - 0.1, x + 0.1, -0.25, -0.05, -0.2, under + height * 0.5, "steel_grey")
    g.plate([(-width / 2, under), (width / 2, under), (width / 2, under + height), (-width / 2, under + height)], 0.0, 0.2,
            "steel_grey", "grey_back")
    m = 0.14
    g.plate([(-width / 2 + m, under + m), (width / 2 - m, under + m), (width / 2 - m, under + height - m),
             (-width / 2 + m, under + height - m)], 0.012, 0.004, "cream", "cream")
    g.plate([(-width / 2 + m, under + m), (width / 2 - m, under + m), (width / 2 - m, under + height * 0.28),
             (-width / 2 + m, under + height * 0.36)], 0.02, 0.004, "terracotta", "terracotta")
    return g.build(name, col)


def chevron_board(name, col, width=3.0):
    """Roundabout chevron board: black with yellow chevrons pointing to the viewer's right; faces +y; top 1.6 m."""
    g = Proto()
    n = max(2, int(round(width / 0.9)))
    for k in range(n + 1):
        sign_pole(g, 1.55, x=-width / 2 + width * k / n)
    g.plate([(-width / 2, 1.0), (width / 2, 1.0), (width / 2, 1.6), (-width / 2, 1.6)], 0.04, 0.04, "black", "grey_back")
    step = width / max(1, int(round(width / 0.6)))
    x = -width / 2 + 0.1
    while x + 0.4 <= width / 2:
        g.plate([(-x, 1.06), (-x - 0.16, 1.06), (-x - 0.4, 1.3), (-x - 0.16, 1.54), (-x, 1.54), (-x - 0.24, 1.3)], 0.052,
                0.004, "chevron_yellow", "chevron_yellow")        # pointing to the viewer's right (he looks along -y)
        x += step
    return g.build(name, col)


def bollard_round(name, col, colour, height, radius, collar=True):
    """V18h: a round post: peeled timber bollards along the car park's edges (coast-walk survey: 1.0 m, d 15 cm,
    grey-brown, a small concrete collar), white round bollards on the island at the throat (0.8 m)."""
    g = Proto()
    if collar:
        g.cyl(radius + 0.07, radius + 0.05, -0.1, 0.04, 8, "concrete", cap=True)
    g.cyl(radius, radius * 0.94, -0.1, height, 8, colour)
    return g.build(name, col)


def ad_totem(name, col):
    """Advert totem: dark case with a blank pale lit face on both sides; faces +-y."""
    g = Proto()
    g.box(-0.6, 0.6, -0.125, 0.125, -0.15, 2.2, "totem_green")
    for side in (1, -1):
        g.face([(-0.5, side * 0.13, 0.45), (0.5, side * 0.13, 0.45), (0.5, side * 0.13, 2.05), (-0.5, side * 0.13, 2.05)],
               "palette", (0, side, 0), "cream")
    return g.build(name, col)


def bins(name, col):
    """Four underground-bin tops in a row along x: steel cylinders with coloured lids."""
    g = Proto()
    for k, c in enumerate(("bin_grey", "bin_green", "bin_yellow", "bin_blue")):
        x = (k - 1.5) * 1.15
        g.cyl(0.3, 0.3, -0.1, 0.85, 8, "steel_grey", x=x, cap=False)
        g.cyl(0.33, 0.3, 0.85, 1.02, 8, c, x=x)
    return g.build(name, col)


def bike_rack(name, col):
    """Three steel hoops, 0.8 m high, along x."""
    g = Proto()
    for k in range(3):
        x = (k - 1) * 0.9
        g.box(x - 0.025, x + 0.025, -0.35, -0.3, -0.1, 0.8, "steel_grey")
        g.box(x - 0.025, x + 0.025, 0.3, 0.35, -0.1, 0.8, "steel_grey")
        g.box(x - 0.025, x + 0.025, -0.35, 0.35, 0.76, 0.81, "steel_grey", skip=())
    return g.build(name, col)


def pay_machine(name, col):
    g = Proto()
    g.box(-0.2, 0.2, -0.15, 0.15, -0.1, 1.55, "pay_blue")
    g.face([(-0.14, 0.155, 0.95), (0.14, 0.155, 0.95), (0.14, 0.155, 1.4), (-0.14, 0.155, 1.4)], "palette", (0, 1, 0),
           "steel_grey")
    return g.build(name, col)


def cabinet(name, col, colour, size):
    g = Proto()
    w, d, h = size
    g.box(-w / 2, w / 2, -d / 2, d / 2, -0.1, h, colour)
    return g.build(name, col)


def sapling(name, col):
    """Young staked tree, 3 m: thin trunk, a small two-tone crown, three timber stakes."""
    g = Proto()
    g.cyl(0.035, 0.025, -0.1, 1.9, 4, "wood_dark", cap=False)
    g.cyl(0.25, 0.75, 1.5, 2.2, 5, "leaf_dark", cap=False)
    g.cyl(0.75, 0.0, 2.2, 3.2, 5, "leaf", cap=False, phase=0.6)
    for k in range(2):
        a = 2 * math.pi * k / 2 + 0.5
        g.box(0.4 * math.cos(a) - 0.03, 0.4 * math.cos(a) + 0.03, 0.4 * math.sin(a) - 0.03, 0.4 * math.sin(a) + 0.03,
              -0.1, 1.4, "timber")
    return g.build(name, col)


def fan_palm(name, col, seed, height, trunk, fronds, frond_len, variant=""):
    """Tall slender palm (Washingtonia look for the restaurant row, slim date type for the car park): a thin, slightly
    leaning trunk and a small round crown. Uses the palm materials and frond cutout of env_vegetation."""
    import env_vegetation as V
    rng = random.Random(seed)
    mb = V.MeshBuilder()
    lean = Vector((rng.uniform(0.25, 0.6), rng.uniform(-0.2, 0.2), 0))
    zs = [-0.4, 0.3, height * 0.25, height * 0.5, height * 0.75, height - 0.9, height - 0.45, height - 0.1, height + 0.12]
    radii = [trunk * 1.5, trunk * 1.15, trunk, trunk * 0.92, trunk * 0.88, trunk * 0.95, trunk * 1.5, trunk * 1.35,
             trunk * 0.7]
    path = [Vector((0, 0, z)) + lean * (max(z, 0) / height) ** 2 for z in zs]
    V.soft_tube(mb, path, radii, 7, 0, tip=path[-1] + Vector((0, 0, 0.12)),
                shade=[0.8, 0.8, 0.78, 0.72, 0.66, 0.5, 0.36, 0.28, 0.24])
    knob = path[-3]
    top_n = max(3, fronds // 4)
    low_n = (fronds - top_n) // 2
    tiers = ((top_n, 0.35, 0.12, 60, 55, frond_len * 0.75, 0.75),
             (fronds - top_n - low_n, 0.15, 0.22, 34, 78, frond_len, 0.9),
             (low_n, -0.1, 0.26, 4, 70, frond_len * 0.95, 0.9))
    for count, dz, r0, el0_deg, droop_deg, length, wide in tiers:
        offset = rng.uniform(0, 2 * math.pi)
        for i in range(count):
            az = offset + 2 * math.pi * i / count + rng.uniform(-0.2, 0.2)
            el0 = math.radians(el0_deg + rng.uniform(-6, 6))
            droop = math.radians(droop_deg + rng.uniform(-10, 10))
            base = knob + Vector((math.cos(az) * r0, math.sin(az) * r0, dz))
            V.add_frond(mb, base, az, el0, droop, length * rng.uniform(0.9, 1.1), 1, rng, wide)
    key = "palm" + variant
    tex = "palm_frond_b_cutout_albedo.png" if variant else "palm_frond_cutout_albedo.png"
    return mb.build(name, [V.ramp_material(key), V.cutout_material("palm_frond" + variant, tex)], col, sway=True)


# ---- V15c: promenade prototypes (photospheres S_p01..S_p05) ----
def bollard_stone(name, col):
    """Pale square limestone post of the beach promenade: 0.25 x 0.25 m, 0.85 m above the ground."""
    g = Proto()
    h, top = S.PROM_BOLLARD["size"] / 2, S.PROM_BOLLARD["height"]
    g.box(-h, h, -h, h, -0.15, top, "bollard_pale")
    return g.build(name, col)


def flood_mast(name, col, height=None):
    """White flood-light mast of the promenade: small plinth, slim tapered shaft, a head frame with four floodlights
    (two each side of the mast's local x axis, which is turned along the promenade)."""
    height = height or S.PROM_MAST["height"]
    g = Proto()
    g.box(-0.3, 0.3, -0.3, 0.3, -0.2, 0.3, "render_white")
    g.cyl(0.15, 0.075, 0.3, height, 6, "mast")
    g.beam((-0.8, 0, height - 0.12), (0.8, 0, height - 0.12), 0.09, 0.09, "palette", "mast")
    for sx in (-1, 1):
        for sy in (-1, 1):
            c = (sx * 0.52, sy * 0.32)
            g.obox(c, (1.0, 0.0), 0.27, 0.22, height - 0.58, height - 0.2, "palette", ("bottom",), "steel_grey")
            g.obox(c, (1.0, 0.0), 0.23, 0.18, height - 0.59, height - 0.58, "palette", ("top", "+u", "-u", "+v", "-v"), "light")
    return g.build(name, col)


def fitness(name, col, kind):
    """Outdoor fitness machine, a generic shape of steel tubes (no real product): 'bars', 'twist' or 'wheel'."""
    g = Proto()
    tube, steel, dark = 0.07, "steel_grey", "black"
    if kind == "bars":                                   # two portal frames with a high and a low bar
        for x in (-0.75, 0.75):
            g.cyl(0.045, 0.045, -0.1, 2.0, 5, steel, x=x)
        g.beam((-0.75, 0, 1.95), (0.75, 0, 1.95), tube, tube, "palette", steel)
        g.beam((-0.75, 0, 1.15), (0.75, 0, 1.15), tube, tube, "palette", dark)
    elif kind == "twist":                                # a centre post with a handle ring and two turning plates
        g.cyl(0.055, 0.055, -0.1, 1.3, 5, steel)
        g.beam((-0.55, 0, 1.25), (0.55, 0, 1.25), tube, tube, "palette", steel)
        g.beam((0, -0.3, 1.05), (0, 0.3, 1.05), tube, tube, "palette", dark)
        for x in (-0.55, 0.55):
            g.cyl(0.05, 0.05, -0.1, 0.12, 5, steel, x=x, cap=False)
            g.cyl(0.24, 0.24, 0.12, 0.17, 8, dark, x=x)
    else:                                                # a post with a hand wheel on each side and a seat
        g.cyl(0.06, 0.06, -0.1, 1.75, 5, steel)
        for y, f in ((0.09, 1), (-0.09, -1)):
            g.disc_y(0.0, y, 1.35, 0.3, 8, dark, f)
            g.disc_y(0.0, y + 0.01 * f, 1.35, 0.08, 6, steel, f)
        g.beam((0, 0, 0.5), (0.6, 0, 0.5), tube, tube, "palette", steel)
        g.cyl(0.04, 0.04, -0.1, 0.5, 5, steel, x=0.6, cap=False)
        g.box(0.42, 0.78, -0.17, 0.17, 0.5, 0.55, dark)
    return g.build(name, col)


CAR_KINDS = (("Prop_CarA", "car_silver", "hatch"), ("Prop_CarB", "car_grey", "sedan"), ("Prop_CarC", "car_white", "suv"),
             ("Prop_CarD", "car_navy", "hatch"), ("Prop_CarE", "car_red", "sedan"), ("Prop_CarF", "car_black", "suv"),
             ("Prop_CarG", "car_sand", "hatch"), ("Prop_CarH", "car_white", "sedan"))


def build_prototypes():
    """Our prototypes, added to the collections env_props / env_vegetation made. Returns {name: object}."""
    pc = E.collection("Proto_Props")
    vc = E.collection("Proto_Vegetation")
    protos = {name: car(name, pc, colour, kind) for name, colour, kind in CAR_KINDS}
    protos.update({
        "Prop_BollardSquare": bollard_square("Prop_BollardSquare", pc),
        "Prop_BollardBanded": bollard_banded("Prop_BollardBanded", pc),
        "Prop_TimberPost": timber_post("Prop_TimberPost", pc),
        "Prop_TimberBollard": bollard_round("Prop_TimberBollard", pc, "timber_grey", 1.0, 0.075),          # V18h
        "Prop_BollardWhite": bollard_round("Prop_BollardWhite", pc, "render_white", 0.8, 0.09, collar=False),   # V18h
        "Prop_LampMarginal": lamp_arm("Prop_LampMarginal", pc, 11.5, 1),
        "Prop_LampAvenue": lamp_arm("Prop_LampAvenue", pc, 10.5, 2, colour="steel_grey"),
        "Prop_LampSidewalk": lamp_arm("Prop_LampSidewalk", pc, 9.0, 1, colour="steel_grey", reach=1.5),
        "Prop_LampGlobe": lamp_globe("Prop_LampGlobe", pc),
        "Prop_LampMast": high_mast("Prop_LampMast", pc),
        "Prop_SignStop": sign_stop("Prop_SignStop", pc),
        "Prop_SignRound": sign_round("Prop_SignRound", pc),
        "Prop_SignRoundTriangle": sign_round("Prop_SignRoundTriangle", pc, triangle=True),
        "Prop_SignParking": sign_parking("Prop_SignParking", pc),
        "Prop_SignCrossing": sign_crossing("Prop_SignCrossing", pc),
        "Prop_BoardDirection": direction_board("Prop_BoardDirection", pc),
        "Prop_BoardDirectionTall": direction_board("Prop_BoardDirectionTall", pc, 2.2, 2.8, 1.5),
        "Prop_Billboard": billboard("Prop_Billboard", pc),
        "Prop_BillboardSmall": billboard("Prop_BillboardSmall", pc, 4.0, 3.0, 3.0),
        "Prop_ChevronBoard": chevron_board("Prop_ChevronBoard", pc),
        "Prop_AdTotem": ad_totem("Prop_AdTotem", pc),
        "Prop_BinsUnderground": bins("Prop_BinsUnderground", pc),
        "Prop_BikeRack": bike_rack("Prop_BikeRack", pc),
        "Prop_PayMachine": pay_machine("Prop_PayMachine", pc),
        "Prop_CabinetGrey": cabinet("Prop_CabinetGrey", pc, "bin_grey", (1.2, 0.45, 1.1)),
        "Prop_CabinetTerracotta": cabinet("Prop_CabinetTerracotta", pc, "terracotta", (2.3, 1.0, 1.7)),
        "Prop_BollardStone": bollard_stone("Prop_BollardStone", pc),          # V15c: promenade
        "Prop_FloodMast": flood_mast("Prop_FloodMast", pc),
        "Prop_FitnessBars": fitness("Prop_FitnessBars", pc, "bars"),
        "Prop_FitnessTwist": fitness("Prop_FitnessTwist", pc, "twist"),
        "Prop_FitnessWheel": fitness("Prop_FitnessWheel", pc, "wheel"),
        "Veg_Sapling": sapling("Veg_Sapling", vc),
        "Veg_FanPalm": fan_palm("Veg_FanPalm", vc, 31, 10.5, 0.19, 13, 1.9),
        "Veg_FanPalm_B": fan_palm("Veg_FanPalm_B", vc, 47, 11.5, 0.2, 12, 1.8, "_b"),
        "Veg_SlimPalm": fan_palm("Veg_SlimPalm", vc, 59, 8.6, 0.24, 14, 3.2),
    })
    tris = {n: sum(len(p.vertices) - 2 for p in o.data.polygons) for n, o in protos.items()}
    print("  streets: prototypes " + ", ".join(f"{n} {t}" for n, t in tris.items()))
    return protos


# =====================================================================================================
# Static structures
# =====================================================================================================
def _zone_polys():
    return [lay["ring"] for lay in S.LAYERS]


def build_kerbs(col):
    """Raised kerbs without a collider: islands (kerb ring + top) and the Marginal's median (two kerb rows and a
    dirt joint, 0.2 m high), only where the centreline is at least PROP_CLEAR away."""
    g = Solid()
    skipped = 0
    for r in S.RAISED:
        if r["kind"] == "island":
            ring = ST.ccw(r["ring"])
            if min(S.route_distance(x, y) for x, y in ring) < S.PROP_CLEAR:
                skipped += 1
                continue
            h = r["height"]
            base = [surface(x, y) for x, y in ring]
            top = [b + h for b in base]
            n = len(ring)
            side_key = r["side"] if r["side"] != "kerb" else "palette"
            side_col = "kerb" if r["side"] == "kerb" else None
            cx, cy = sum(p[0] for p in ring) / n, sum(p[1] for p in ring) / n
            rim = r.get("rim", 0.0)
            inner = [(p[0] + (cx - p[0]) * rim / max(math.hypot(cx - p[0], cy - p[1]), 1e-6),
                      p[1] + (cy - p[1]) * rim / max(math.hypot(cx - p[0], cy - p[1]), 1e-6)) for p in ring]
            for k in range(n):
                k1 = (k + 1) % n
                p, q = ring[k], ring[k1]
                out = (q[1] - p[1], -(q[0] - p[0]), 0.0)
                g.face([(*p, base[k] - 0.25), (*q, base[k1] - 0.25), (*q, top[k1]), (*p, top[k])], side_key, out, side_col)
                if rim:
                    g.face([(*p, top[k]), (*q, top[k1]), (*inner[k1], top[k1]), (*inner[k], top[k])], side_key, (0, 0, 1),
                           side_col)
            top_key = r["top"] or "lawn"
            zin = [t - (0.1 if rim else 0.0) for t in top]
            if rim:
                for k in range(n):
                    k1 = (k + 1) % n
                    g.face([(*inner[k], top[k]), (*inner[k1], top[k1]), (*inner[k1], zin[k1]), (*inner[k], zin[k])],
                           side_key, (cx - inner[k][0], cy - inner[k][1], 0.0), side_col)
            g.face([(*p, z) for p, z in zip(inner, zin)], top_key, (0, 0, 1))
        elif r["kind"] == "median":
            line, half, h = r["line"], r["half"], r["height"]
            left, right = S.offset(line, half), S.offset(line, -half)
            jl, jr = S.offset(line, 0.14), S.offset(line, -0.14)
            ok = [min(S.route_distance(*left[k]), S.route_distance(*right[k])) >= S.PROP_CLEAR + 0.4 for k in range(len(line))]
            for k in range(len(line) - 1):
                if not (ok[k] and ok[k + 1]):
                    skipped += 1
                    continue
                za, zb = surface(*line[k]), surface(*line[k + 1])
                u = S.unit(line[k + 1][0] - line[k][0], line[k + 1][1] - line[k][1])
                nl = (-u[1], u[0], 0.0)
                for (a0, a1), colour, zt in (((left, jl), "kerb", h), ((jl, jr), "soil", h - 0.03), ((jr, right), "kerb", h)):
                    g.face([(*a0[k], za + zt), (*a0[k + 1], zb + zt), (*a1[k + 1], zb + zt), (*a1[k], za + zt)], "palette",
                           (0, 0, 1), colour)
                g.face([(*left[k], za - 0.1), (*left[k + 1], zb - 0.1), (*left[k + 1], zb + h), (*left[k], za + h)], "palette",
                       nl, "kerb")
                g.face([(*right[k], za - 0.1), (*right[k + 1], zb - 0.1), (*right[k + 1], zb + h), (*right[k], za + h)],
                       "palette", (-nl[0], -nl[1], 0.0), "kerb")
                for end, cond in ((k, not ok[k - 1] if k else True), (k + 1, k + 2 >= len(line) or not ok[k + 2])):
                    if cond:                                      # end face of a run
                        z = surface(*line[end])
                        sign = -1.0 if end == k else 1.0
                        g.face([(*left[end], z - 0.1), (*right[end], z - 0.1), (*right[end], z + h), (*left[end], z + h)],
                               "palette", (u[0] * sign, u[1] * sign, 0.0), "kerb")
    E.new_object("Street_NoCollide_Kerbs", g.mesh("Street_Kerbs"), col)
    print(f"  streets: kerbs {sum(len(f[0]) - 2 for f in g.faces)} triangles ({skipped} pieces left flush near the road)")


def build_structures(col):
    g = Solid()
    hp, bp = S.hedge_pt, S.brick_pt
    # --- E: white retaining wall behind the brick bays and the planted bank up to the Marginal's sidewalk ---
    up_b = S.left_of(S.BRICK_U)                                   # from the bays up the bank (north-north-east)
    wall_line = [bp(0.0, -0.12), bp(S.BRICK_LEN, -0.12)]
    g.wall(wall_line, 0.25, lambda x, y: ST.ground(x, y) - 0.4,
           lambda x, y: surface(x - up_b[0] * 0.75, y - up_b[1] * 0.75) + 0.75, "wall_white", step=5.0)
    n = 8
    edge = []
    for k in range(n + 1):
        foot = bp(S.BRICK_LEN * k / n, -0.25)
        d = 1.0
        while d < 12.0 and S.layer_at(foot[0] + up_b[0] * d, foot[1] + up_b[1] * d)["name"] == "E bank":
            d += 0.1
        sx = (foot[0] + up_b[0] * (d + 0.05), foot[1] + up_b[1] * (d + 0.05))
        edge.append(((*foot, surface(*bp(S.BRICK_LEN * k / n, 0.75)) + 0.68), (*sx, surface(*sx) + 0.02)))
    for a, b in zip(edge, edge[1:]):
        g.face([a[0], b[0], b[1], a[1]], "palette", (0, 0, 1), "bank_green")
    # --- E: clipped hedge behind the hedge-side bays (open at the portal), the terrace's retaining wall ---
    up_h = S.left_of(S.HEDGE_U)                                   # from the terrace up to the forecourt (north-east)
    for t0, t1 in ((-0.8, S.PORTAL_T[0] - 0.15), (S.PORTAL_T[1] + 0.15, S.HEDGE_LEN + 1.3)):
        line = [hp(t0, S.BAY[1] + S.HEDGE_DEPTH / 2 + 0.3), hp(t1, S.BAY[1] + S.HEDGE_DEPTH / 2 + 0.3)]
        g.wall(line, S.HEDGE_DEPTH, lambda x, y: ST.ground(x, y) - 0.3, lambda x, y: ST.ground(x, y) + 1.25, "hedge", step=4.0)
        kerb = [hp(t0, S.BAY[1] + 0.15), hp(t1, S.BAY[1] + 0.15)]          # V16g: concrete upstand in front of the hedge
        g.wall(kerb, 0.3, lambda x, y: ST.ground(x, y) - 0.2, lambda x, y: ST.ground(x, y) + 0.25, "palette", colour="kerb",
               step=4.0)
        wall = [hp(t0, S.WALL_S + 0.1), hp(t1, S.WALL_S + 0.1)]
        g.wall(wall, 0.3, lambda x, y: S.TERRACE_Z - 0.5,
               lambda x, y: ST.ground(x + up_h[0] * 0.9, y + up_h[1] * 0.9) + 0.25, "wall_white", step=4.0)
    # the low white wall right of the portal (blank: the real one carries the restaurant's name)
    g.wall([hp(11.0, 6.9), hp(17.0, 6.9)], 0.25, lambda x, y: ST.ground(x, y) - 0.3, lambda x, y: ST.ground(x, y) + 1.5,
           "wall_white", step=6.0)
    # --- E: the Capricciosa entrance portal: V16g, env_capricciosa.portal (a funnel-shaped wedge, not a box) ---
    # --- E: retaining walls of the terrace on the Pastorinha side and along the service ramp ---
    lane = [(-321.0, 0.4), (-306.0, -8.0), (-297.0, -15.0), (-292.7, -17.5)]
    cu = S.unit(9.0, -7.0)                                         # along the lane edge in front of A Pastorinha
    cc = (-301.5, -11.5)                                           # middle of its entrance
    gap_a, gap_b = S.pt(cc, cu, -2.1), S.pt(cc, cu, 2.1)
    for piece in ([lane[0], lane[1], gap_a], [gap_b, lane[2], lane[3]]):
        g.wall(piece, 0.3, lambda x, y: S.TERRACE_Z - 0.5, lambda x, y: ST.ground(*_out(x, y, lane, 0.9)) + 0.95,
               "wall_white", step=5.0)
    # its entrance: a flat white canopy on four round columns at the lane, steps down to the terrace under it
    zl = surface(*S.pt(cc, cu, 0.0, 1.2))
    g.obox(S.pt(cc, cu, 0.0, -1.6), cu, 6.0, 3.0, zl + 3.0, zl + 3.28, "roof", skip=())
    for t in (-5.4, -1.9, 1.9, 5.4):
        x, y = S.pt(cc, cu, t, 0.9)
        for k in range(6):
            a0, a1 = math.pi * k / 3, math.pi * (k + 1) / 3
            g.face([(x + 0.13 * math.cos(a0), y + 0.13 * math.sin(a0), zl - 0.1), (x + 0.13 * math.cos(a1), y + 0.13 * math.sin(a1), zl - 0.1),
                    (x + 0.13 * math.cos(a1), y + 0.13 * math.sin(a1), zl + 3.0), (x + 0.13 * math.cos(a0), y + 0.13 * math.sin(a0), zl + 3.0)],
                   "palette", (math.cos((a0 + a1) / 2), math.sin((a0 + a1) / 2), 0.0), "render_white")
    drop = zl - S.TERRACE_Z
    steps = max(2, int(round(drop / 0.165)))
    for k in range(steps):
        g.obox(S.pt(cc, cu, 0.0, -0.15 - 0.3 * (k + 0.5)), cu, 1.95, 0.15, S.TERRACE_Z - 0.4, zl - drop * (k + 1) / steps,
               "palette", colour="concrete")
    for t in (-2.0, 2.0):                                          # cheek walls beside the steps
        g.obox(S.pt(cc, cu, t, -0.15 - 0.15 * steps), cu, 0.1, 0.15 * steps + 0.1, S.TERRACE_Z - 0.4, zl + 0.5, "wall_white")
    ramp_e = [S.SERVICE_RAMP[1], S.SERVICE_RAMP[2]]
    g.wall(ramp_e, 0.25, lambda x, y: S.TERRACE_Z - 0.5, lambda x, y: ST.ground(*_out(x, y, ramp_e, -0.9)) + 0.95,
           "wall_white", step=4.0, side=-0.15)
    # --- C: the five corten beds on the roundabout's island (V18r; street_plan.BLADES): long wedge-shaped boxes of
    # weathering steel across the pale gravel, dark gravel inside the steel rim, each rising from 0.2 m at one end to
    # about 1 m at the other. The island lies on a 6 % slope: the beds stand on the plane through its ground. ---
    samples = [S.RB] + [S.rb_pt(a, S.RB_BED - 1.0) for a in range(0, 360, 45)] + [S.rb_pt(a, S.RB_BED / 2) for a in range(0, 360, 90)]
    zs = [ST.ground(x, y) for x, y in samples]
    n = len(samples)
    mx, my, mz = sum(q[0] for q in samples) / n, sum(q[1] for q in samples) / n, sum(zs) / n
    sxx = sum((q[0] - mx) ** 2 for q in samples)
    syy = sum((q[1] - my) ** 2 for q in samples)
    sxy = sum((q[0] - mx) * (q[1] - my) for q in samples)
    sxz = sum((q[0] - mx) * (z - mz) for q, z in zip(samples, zs))
    syz = sum((q[1] - my) * (z - mz) for q, z in zip(samples, zs))
    det = sxx * syy - sxy * sxy
    gx, gy = (sxz * syy - syz * sxy) / det, (syz * sxx - sxz * sxy) / det

    def island_z(x, y):
        return mz + gx * (x - mx) + gy * (y - my)
    off = max(abs(island_z(x, y) - z) for (x, y), z in zip(samples, zs))
    near = 1e9
    for t0, t1, high_end, height in S.BLADES:
        outer = S.blade_outline(t0, t1)
        inner = S.blade_outline(t0, t1, S.BLADE_RIM)
        near = min(near, min(S.route_distance(x, y) for x, y in outer))

        def top(q):
            return (q[0], q[1], island_z(*q) + S.blade_height(q[0], q[1], t0, t1, high_end, height))
        m = len(outer)
        cx, cy = sum(q[0] for q in outer) / m, sum(q[1] for q in outer) / m
        for k in range(m):
            a_, b_ = outer[k], outer[(k + 1) % m]
            g.face([(a_[0], a_[1], island_z(*a_) - 0.35), (b_[0], b_[1], island_z(*b_) - 0.35), top(b_), top(a_)], "rust",
                   ((a_[0] + b_[0]) / 2 - cx, (a_[1] + b_[1]) / 2 - cy, 0.0))
            g.face([top(a_), top(b_), top(inner[(k + 1) % m]), top(inner[k])], "rust", (0, 0, 1))
        g.face([top(q) for q in inner], "palette", (0, 0, 1), "gravel")
    if near < S.PROP_CLEAR:
        raise RuntimeError(f"env_street_objects: a corten bed of the roundabout stands {near:.2f} m from the centreline")
    print(f"  streets: roundabout island: 5 corten beds on the ground's plane (slope {100 * math.hypot(gx, gy):.1f} %, the "
          f"ground differs from it by at most {off * 100:.0f} cm), nearest {near:.2f} m from the centreline")
    # --- C: rust-coloured steel portal frames in the verges (posts 11 cm, 4.7 m high, some with a rail at 1.1 m) ---

    def frames(points, rail):
        zs = [ST.ground(x, y) for x, y in points]
        for (x, y), z in zip(points, zs):
            if S.route_distance(x, y) >= S.PROP_CLEAR:
                g.post(x, y, z - 0.3, z + 4.7, 0.11, "rust")
        for (a, za), (b, zb_) in zip(zip(points, zs), list(zip(points, zs))[1:]):
            if min(S.route_distance(*a), S.route_distance(*b)) < S.PROP_CLEAR:
                continue
            g.beam((*a, za + 4.64), (*b, zb_ + 4.64), 0.11, 0.11, "rust")
            if rail:
                g.beam((*a, za + 1.1), (*b, zb_ + 1.1), 0.06, 0.06, "rust", ends=False)
    frames([(-129.8, -42.6), (-126.6, -38.3)], False)
    row = [S.at_s([(-124.8, -82.9), (-135.0, -72.3)], k * 14.71 / 5)[0] for k in range(6)]   # V18r: behind the rebuilt
    frames(row, True)                                                                        # south sidewalk
    frames([(-135.0, -72.3), (-137.9, -69.3), (-140.9, -67.6)], False)
    # --- B: the old wire fence on timber posts along the west side of the avenue ---
    fence = [S.av_pt(y, 17.2) for y in range(-10, 99, 4)]             # V18r: the north arm's sidewalk bends west south of it
    fz = [ST.ground(x, y) for x, y in fence]
    for (x, y), z in zip(fence, fz):
        g.post(x, y, z - 0.2, z + 1.4, 0.1, "palette", "timber")
    for (a, za), (b, zb_) in zip(zip(fence, fz), list(zip(fence, fz))[1:]):
        for h in (0.75, 1.3):
            g.beam((*a, za + h), (*b, zb_ + h), 0.025, 0.025, "palette", "steel_grey", ends=False)
    E.new_object("Street_Structures", g.mesh("Street_Structures"), col)
    print(f"  streets: structures {sum(len(f[0]) - 2 for f in g.faces)} triangles")


def _out(x, y, line, d):
    """(x, y) moved d metres to the left of the polyline's direction."""
    s = S.s_of(line, x, y)
    _p, u = S.at_s(line, s)
    return (x - u[1] * d, y + u[0] * d)


# =====================================================================================================
# Dressing: what goes, what comes
# =====================================================================================================
def _entry(proto, x, y, rot_deg=0.0, scale=1.0, z=None):
    return {"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(surface(x, y) if z is None else z, 3),
            "rot_z": round(math.radians(rot_deg), 4), "scale": round(scale, 3)}


def heading_rot(u):
    """rot_z (degrees) that turns a prototype's +x axis onto the direction u."""
    return math.degrees(math.atan2(u[1], u[0]))


def facing_rot(u):
    """rot_z (degrees) that turns a prototype's face (+y) towards the direction u."""
    return math.degrees(math.atan2(u[1], u[0])) - 90.0


def placements():
    """(plants, props): everything of ours, each with the clearance it needs from the centreline."""
    rng = random.Random(SEED)
    plants, props, skipped = [], [], []

    def add(lst, proto, x, y, rot=0.0, scale=1.0, clear=S.PROP_CLEAR, z=None):
        if S.route_distance(x, y) < clear:
            skipped.append(proto)
            return False
        lst.append(_entry(proto, x, y, rot, scale, z))
        return True

    hp, bp, kp = S.hedge_pt, S.brick_pt, S.kerb_pt
    hu, bu, ku = S.HEDGE_U, S.BRICK_U, S.NE_KERB_U
    cars = [c[0] for c in CAR_KINDS]

    def park(origin_fn, n, nose, share, skip=()):
        """Cars in a bay row: origin_fn(t, s) frame, n bays from t = 0, nose = direction the cars point."""
        for k in range(n):
            if k in skip or rng.random() > share:
                continue
            x, y = origin_fn((k + 0.5) * S.BAY[0] + rng.uniform(-0.12, 0.12), 2.5 + rng.uniform(-0.25, 0.3))
            rot = heading_rot(nose) + rng.uniform(-2.5, 2.5) + (180.0 if rng.random() < 0.3 else 0.0)
            add(props, cars[rng.randrange(len(cars))], x, y, rot, clear=S.CAR_CLEAR)

    # ---------------- E: forecourt ----------------
    back = (-S.left_of(hu)[0], -S.left_of(hu)[1])                 # from the bay front towards the hedge
    portal_bays = {int(S.PORTAL_T[0] / S.BAY[0]), int(S.PORTAL_T[0] / S.BAY[0]) + 1}
    park(hp, 19, back, 0.9, skip=portal_bays)
    park(lambda t, s: bp(t, 5.0 - s), 14, S.left_of(bu), 0.85, skip={1, 3})
    # tall fan palms in the planted strip (8, slender, slightly leaning), low ones on the terrace, a date palm at the end
    for k in range(8):
        t = 3.3 + k * 5.85 + rng.uniform(-0.5, 0.5)
        if S.PORTAL_T[0] - 0.8 < t < S.PORTAL_T[1] + 0.8:
            t = S.PORTAL_T[1] + 1.2
        x, y = hp(t, 7.3 + rng.uniform(-0.25, 0.25))
        add(plants, "Veg_FanPalm" if k % 2 else "Veg_FanPalm_B", x, y, rng.uniform(0, 360), rng.uniform(0.9, 1.08),
            clear=S.PROP_CLEAR + 2.0, z=ST.ground(x, y) - 0.1)
    for k in range(7):
        t = 2.0 + k * 6.6 + rng.uniform(-1.0, 1.0)
        if S.PORTAL_T[0] - 2.5 < t < S.PORTAL_T[1] + 2.5:
            continue
        x, y = hp(t, S.WALL_S + rng.uniform(2.2, 3.6))
        add(plants, "Veg_FanPalm", x, y, rng.uniform(0, 360), rng.uniform(0.4, 0.5), z=S.TERRACE_Z - 0.05)
    x, y = hp(S.HEDGE_LEN + 2.6, 7.0)
    add(plants, "Veg_Palm", x, y, 40.0, 0.62, clear=S.PROP_CLEAR + 4.0)
    for k in range(9):                                            # low shrubs in the strip behind the hedge
        t = 1.5 + k * 5.3 + rng.uniform(-1, 1)
        x, y = hp(t, 7.9 + rng.uniform(-0.3, 0.3))
        if not S.PORTAL_T[0] - 1.2 < t < S.PORTAL_T[1] + 1.2:
            add(plants, "Veg_Shrub" if k % 2 else "Veg_Shrub_B", x, y, rng.uniform(0, 360), rng.uniform(0.8, 1.1))
    # the grass island on the outside of the turn: high mast, direction board (grey back to the car park), low posts
    add(props, "Prop_LampMast", *S.MAST_A)
    north = S.at_s(S.MEDIAN, S.med_s(-262.0))[1]
    add(props, "Prop_BoardDirection", -262.6, -14.4, facing_rot(S.left_of(north)))
    isl = S.GRASS_ISLAND
    edge = [isl[k] for k in (2, 3, 4, 5, 6)]
    cx, cy = sum(p[0] for p in isl) / len(isl), sum(p[1] for p in isl) / len(isl)
    total = S.line_length(edge)
    for k in range(9):
        (x, y), _u = S.at_s(edge, total * (k + 0.5) / 9)
        d = math.hypot(cx - x, cy - y)
        add(props, "Prop_TimberPost", x + (cx - x) * 0.55 / d, y + (cy - y) * 0.55 / d, z=surface(x, y) + 0.25)
    # signs: stop + direction disc and the advert totem on the lawn island's tip (the apex marker), the round pair
    # on the lawn beyond, parking and prohibition signs and a lamp on the sidewalk west of the mouth
    # (V16g: the stop sign, advert totem and round sign stood on the old island tip, under the widened hairpin; the
    # island inside the loop and the south verge are dressed by env_capricciosa)
    add(props, "Prop_SignParking", *S.med_pt(-259.0, S.S_WALK[0] + 0.3), facing_rot(S.bearing(110.0)))
    add(props, "Prop_SignRound", *S.med_pt(-263.0, S.S_WALK[0] + 0.3), facing_rot(S.bearing(110.0)))
    add(props, "Prop_LampSidewalk", *S.med_pt(-259.6, S.S_WALK[0] + 0.35), heading_rot(S.left_of(north)))
    # pay machine and parking sign at the north-west end of the hedge bays, cabinets in the hedge, globe lamps
    add(props, "Prop_PayMachine", *hp(-1.3, 3.2), facing_rot(S.left_of(hu)))
    add(props, "Prop_SignParking", *hp(-1.3, 4.3), facing_rot(S.left_of(hu)))
    add(props, "Prop_CabinetGrey", *hp(7.6, 5.75), heading_rot(hu), z=ST.ground(*hp(7.6, 5.75)))
    add(props, "Prop_CabinetGrey", *hp(9.1, 5.75), heading_rot(hu), 0.85, z=ST.ground(*hp(9.1, 5.75)))
    add(props, "Prop_LampGlobe", -287.4, -15.6)
    add(props, "Prop_LampGlobe", -297.0, -9.6)
    for k in (0, 2):                                              # blue signs at the disabled bays (at the wall)
        add(props, "Prop_SignParking", *bp(k * 2.5 + 2.5, 0.3), facing_rot((-S.left_of(bu)[0], -S.left_of(bu)[1])), 0.88)
    px, py = bp(21.0, -2.6)                                       # one small palm on the bank
    add(plants, "Veg_Palm_B", px, py, 70.0, 0.42, z=surface(*bp(21.0, 0.75)) + 0.45)
    add(props, "Prop_LampMast", -335.0, -5.0)                     # the high mast beyond A Pastorinha
    for d, face in ((S.N_WALK[0] + 0.6, 120.0), (S.S_WALK[1] - 0.6, 300.0)):       # the pedestrian crossing west of it
        add(props, "Prop_SignCrossing", *S.med_pt(-303.0, d), facing_rot(S.bearing(face)))
    for k in range(7):                                            # ground cover on the bank
        x, y = bp(2.5 + k * 5.0 + rng.uniform(-1, 1), -2.0 - rng.uniform(0.0, 1.4))
        add(plants, "Veg_Shrub_B" if k % 2 else "Veg_Shrub", x, y, rng.uniform(0, 360), rng.uniform(0.7, 1.0),
            z=surface(*bp(2.5 + k * 5.0, 0.6)) + 0.5)

    # ---------------- F: car park ----------------
    sw = S.left_of(ku)
    sw = (-sw[0], -sw[1])                                         # from the north-east kerb towards the sea
    ne = (-sw[0], -sw[1])
    pits = {int(t // S.BAY[0]) for t in S.PIT_T}
    park(lambda t, s: kp(t, 5.0 - s), 38, ne, 0.66, skip=pits)
    z1 = int((S.s_kerb(-198.0, -82.0) - S.SW_ROW_T0) / S.BAY[0])
    yb = int((S.s_kerb(-177.0, -102.0) - S.SW_ROW_T0) / S.BAY[0])
    park(lambda t, s: kp(t + S.SW_ROW_T0, 12.5 + s), 41, sw, 0.62, skip={z1 - 1, z1, yb})
    park(lambda t, s: kp(t + S.CENTRAL_T0, 22.5 - s), 11, ne, 0.8)
    sa, su = (-241.0, -72.6), S.unit(39.0, -8.9)
    park(lambda t, s: S.pt(sa, su, t, -s), 16, (su[1], -su[0]), 0.85)
    for k, t in enumerate(S.PIT_T):                               # slim date palms in the pits (lawn side if too close)
        for s in (1.35, -1.3, -2.6):
            x, y = kp(t, s)
            if S.route_distance(x, y) >= S.PROP_CLEAR + 1.2:
                add(plants, "Veg_SlimPalm", x, y, rng.uniform(0, 360), rng.uniform(0.92, 1.12), z=ST.ground(x, y) - 0.1)
                break
    for x, y in ((-233.0, -45.0), (-227.5, -48.0)):               # the first twin palms at the head of the row
        for back_off in (0.0, 2.0, 4.0, 6.0):
            px, py = x + ne[0] * back_off, y + ne[1] * back_off
            if S.route_distance(px, py) >= S.PROP_CLEAR + 1.5:
                add(plants, "Veg_SlimPalm", px, py, rng.uniform(0, 360), rng.uniform(0.95, 1.05), z=ST.ground(px, py) - 0.1)
                break
    t = -2.0
    while t <= S.NE_KERB_LEN + 2.0:                               # pale square bollards behind the north-east kerb
        add(props, "Prop_BollardSquare", *kp(t, -0.5), heading_rot(ku))
        t += 1.9
    t = 28.0
    while t <= S.NE_KERB_LEN - 5.0:                               # and along the south-west kerb
        add(props, "Prop_BollardSquare", *kp(t, 18.0), heading_rot(ku))
        t += 1.9
    for x, y in ((-243.0, -77.5), (-176.6, -86.6), (-121.0, -120.0)):
        add(props, "Prop_LampMast", x, y, clear=S.PROP_CLEAR + 0.6)
    for x, y in ((-149.6, -108.6), (-146.0, -108.9)):             # blue signs at the disabled bays of the square
        add(props, "Prop_SignParking", x, y, facing_rot(S.bearing(190.0)), 0.9)
    add(props, "Prop_PayMachine", -157.0, -101.0, facing_rot(sw))
    add(props, "Prop_SignParking", -156.2, -101.6, facing_rot(sw))
    add(props, "Prop_CabinetTerracotta", -180.0, -105.0, heading_rot(ku))
    add(props, "Prop_CabinetGrey", -176.0, -110.0, heading_rot(ku))
    add(props, "Prop_BillboardSmall", -112.0, -92.0, facing_rot(S.bearing(250.0)))

    # ---------------- D: Avenida Marginal ----------------
    for x, y in ((-122.7, -43.9), (-140.0, -48.3), (-172.2, -40.4), (-200.6, -27.4), (-233.5, -9.6), (-266.0, 9.0),
                 (-298.0, 27.5), (-330.0, 46.0)):                 # north sidewalk lamps (single arm towards the road)
        s = S.s_of(S.MEDIAN, x, y)
        p, u = S.at_s(S.MEDIAN, s)
        off = S.N_WALK[1] - 0.35
        while S.route_distance(*S.pt(p, u, 0.0, off)) < S.PROP_CLEAR + 0.25 and off < S.N_WALK[1] + 8.0:
            off += 0.25                                           # the road lies on the sidewalk here: further out
        q = S.pt(p, u, 0.0, off)
        add(props, "Prop_LampMarginal", q[0], q[1], heading_rot((u[1], -u[0])))
    for k in range(38):                                           # (V18g: no saplings, the ground north of the road is
        rng.uniform(0, 1), rng.uniform(0, 1), rng.uniform(0, 1), rng.uniform(0, 1)   # the dusty car park: streets_v18g();
                                                                  # the random sequence stays as it was)
    # ---------------- C: roundabout (V18r: positions on the rebuilt layout) ----------------
    c = S.RB
    for ang in (94.5, 211.0, 5.0):                                # chevron boards on the island's paved band, one facing
        radial = (math.cos(math.radians(ang)), math.sin(math.radians(ang)))          # each entry (north, west, east)
        add(props, "Prop_ChevronBoard", c[0] + radial[0] * 11.9, c[1] + radial[1] * 11.9, facing_rot(radial))
    for x, y in ((-118.6, -40.6), (-92.2, -46.6), (-83.6, -62.6), (-97.0, -87.2), (-115.2, -87.9), (-131.8, -73.6)):
        add(props, "Prop_LampMarginal", x, y, heading_rot(S.unit(c[0] - x, c[1] - y)), 0.88)
    add(props, "Prop_SignRoundTriangle", -131.4, -60.4, facing_rot(S.bearing(270.0)))       # on the west splitter
    add(props, "Prop_SignRound", -84.9, -73.4, facing_rot(S.bearing(110.0)))                # on the east splitter
    add(props, "Prop_SignCrossing", -138.4, -67.6, facing_rot(S.bearing(290.0)))            # zebra of the west arm
    add(props, "Prop_SignCrossing", -114.8, -33.2, facing_rot(S.bearing(0.0)))              # zebra of the north arm
    add(props, "Prop_SignCrossing", -86.6, -85.2, facing_rot(S.bearing(110.0)))             # zebra of the east arm
    add(props, "Prop_AdTotem", -110.0, -93.0, facing_rot(S.bearing(0.0)), 1.9)
    add(props, "Prop_CabinetGrey", -103.0, -90.0, 0.0)
    add(props, "Prop_CabinetGrey", -101.6, -90.2, 0.0, 0.75)
    add(props, "Prop_Billboard", -121.6, -37.6, facing_rot(S.bearing(85.0)))
    for k in range(17):                                           # clipped round shrubs closing the ring's south side
        ang = math.radians(204.0 + k * 6.7)
        r = S.RB_OUT + 5.2 + rng.uniform(-0.6, 0.6)
        add(plants, "Veg_Shrub" if k % 2 else "Veg_Shrub_B", c[0] + r * math.cos(ang), c[1] + r * math.sin(ang),
            rng.uniform(0, 360), rng.uniform(2.0, 2.6))

    # ---------------- A + B: avenue N6-7 ----------------
    for y in (60.0, 25.0, -8.0, -41.0):                           # east sidewalk lamps, arm towards the road
        x, yy = S.av_pt(y, S.AV_EAST_WALK[1] - 0.45)
        add(props, "Prop_LampSidewalk", x, yy, 180.0)
    for y in (80.0, 47.0, 14.5, -20.0):                           # double-arm lamps of the median (at its west edge:
        x, yy = S.av_pt(y, 6.95)                                  # the real ones stand 1.25 m from our road edge)
        add(props, "Prop_LampAvenue", x, yy, 0.0)
    for x in (-89.0, -86.0):                                      # black bollards with two yellow bands, 3 m apart
        for k in range(8):
            add(props, "Prop_BollardBanded", x, 20.0 - k * 3.0 - (1.5 if x > -88 else 0.0))
    add(props, "Prop_SignRound", -85.3, 33.3, facing_rot(S.bearing(180.0)))
    add(props, "Prop_BoardDirectionTall", -104.5, -27.5, facing_rot(S.bearing(0.0)))        # V18r: on the north splitter
    add(props, "Prop_SignCrossing", -85.2, -32.4, facing_rot(S.bearing(0.0)))
    for k in range(16):                                           # reeds along the fence
        x, y = S.av_pt(-22.0 + k * 7.4 + rng.uniform(-2, 2), 16.3 + rng.uniform(-0.3, 0.5))
        add(plants, "Veg_Grass", x, y, rng.uniform(0, 360), rng.uniform(1.0, 1.4))
    for k in range(9):                                            # (V18g: the car park's cars are streets_v18g()'s)
        rng.randrange(len(cars)), rng.uniform(-0.3, 0.3), rng.choice((0.0, 180.0))
    # the belt between the Marginal and the car park (survey D 6, F 4): a shrub hedge at its east end, dark round
    # trees in the middle, open lawn with a few palms towards the hairpin, dense shrubs on the bank in the south-east
    for k in range(12):
        x, y = S.med_pt(-149.0 - k * 2.6, S.S_WALK[0] - 3.2 + rng.uniform(-0.5, 0.5))
        add(plants, "Veg_Shrub" if k % 2 else "Veg_Shrub_B", x, y, rng.uniform(0, 360), rng.uniform(1.7, 2.2), clear=S.PROP_CLEAR + 2.0)
    for k in range(9):
        t = 34.0 + k * 4.6 + rng.uniform(-1.0, 1.0)
        x, y = kp(t, -(8.5 + rng.uniform(-2.5, 3.5)))
        add(plants, "Veg_Olive" if k % 2 else "Veg_Olive_B", x, y, rng.uniform(0, 360), rng.uniform(1.25, 1.55), clear=S.PROP_CLEAR + 4.5)
    for t, off in ((8.0, 9.5), (17.0, 8.0), (25.0, 11.0)):
        x, y = kp(t, -off)
        add(plants, "Veg_Palm" if t < 15 else "Veg_Palm_B", x, y, rng.uniform(0, 360), rng.uniform(0.78, 0.9), clear=S.PROP_CLEAR + 5.0)
    for k in range(11):
        t = 70.0 + k * 2.5 + rng.uniform(-0.6, 0.6)
        x, y = kp(t, -(4.5 + rng.uniform(0.0, 5.0)))
        add(plants, "Veg_Shrub_B" if k % 2 else "Veg_Shrub", x, y, rng.uniform(0, 360), rng.uniform(1.8, 2.6), clear=S.PROP_CLEAR + 2.0)
    left = {}
    for name in skipped:
        key = "parked cars" if name.startswith("Prop_Car") else name
        left[key] = left.get(key, 0) + 1
    print(f"  streets: {len(plants)} plants, {len(props)} props placed"
          + (f"; left out, too close to the road: {left}" if left else ""))
    return plants, props


def dress(veg, props):
    """Drop the generated plants and props that stand on the new streets, add ours, rewrite the dressing files."""
    rings = [(min(p[0] for p in r) - 0.5, min(p[1] for p in r) - 0.5, max(p[0] for p in r) + 0.5, max(p[1] for p in r) + 0.5, r)
             for r in _zone_polys()]

    def in_zone(x, y):
        return any(b[0] <= x <= b[2] and b[1] <= y <= b[3] and S.inside(x, y, b[4]) for b in rings)

    belt = next(lay["ring"] for lay in S.LAYERS if lay["name"] == "D south belt")
    osm = [tuple(t) for t in E.DATA["trees"]]
    kept_v, dropped, turned = [], 0, 0
    for p in veg:
        x, y = p["x"], p["y"]
        if not in_zone(x, y):
            kept_v.append(p)
            continue
        top = S.layer_at(x, y)
        if top is not None and top["mat"] is None and top["ground"] not in ("meadow", "gravel") \
                and not p["proto"].startswith("Veg_Palm"):
            kept_v.append(p)                             # lawn, bank, dusty ground: the other trees and shrubs stay
            continue
        real = p["proto"].startswith("Veg_Palm") and S.inside(x, y, belt) \
            and any(math.hypot(x - tx, y - ty) < 8.5 for tx, ty in osm)
        if real and x > -205.0:
            # the real trees in the belt east of the hairpin are dark round-crowned trees, not palms (survey D 6)
            kept_v.append(dict(p, proto="Veg_Olive" if turned % 2 else "Veg_Olive_B", scale=round(1.25 + 0.2 * (turned % 3), 3)))
            turned += 1
        elif real:
            kept_v.append(p)
        else:
            dropped += 1
    kept_p = [p for p in props if not in_zone(p["x"], p["y"])]
    plants, ours = placements()
    print(f"  streets: dressing: {dropped} plants and {len(props) - len(kept_p)} props removed from the street area, "
          f"{turned} belt palms became round trees")
    veg2, props2 = kept_v + plants, kept_p + ours
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


# =====================================================================================================
# V15c: the beach promenade (street_plan section G): seat-walls and beach steps (static), bollards, flood masts,
# fitness machines and the old olive (instanced, added to the dressing after dressing_freeze: see dress_promenade)
# =====================================================================================================
PROM_REPORT = []            # (what, minimum distance to the centreline): printed by dress_promenade


def _strip_wall(g, line, width, z0, z1, colour):
    """A wall with mitred joints along a polyline (smooth in plan, top follows z1): sides, top, end caps."""
    lo, hi = S.offset(line, -width / 2), S.offset(line, width / 2)
    for k in range(len(line) - 1):
        a0, a1, b0, b1 = lo[k], lo[k + 1], hi[k], hi[k + 1]
        za, zb, fa, fb = z1(*line[k]), z1(*line[k + 1]), z0(*line[k]), z0(*line[k + 1])
        u = S.unit(line[k + 1][0] - line[k][0], line[k + 1][1] - line[k][1])
        g.face([(*a0, fa), (*a1, fb), (*a1, zb), (*a0, za)], "palette", (u[1], -u[0], 0.0), colour)
        g.face([(*b0, fa), (*b1, fb), (*b1, zb), (*b0, za)], "palette", (-u[1], u[0], 0.0), colour)
        g.face([(*a0, za), (*a1, zb), (*b1, zb), (*b0, za)], "palette", (0, 0, 1), colour)
    for k, j in ((0, 1), (len(line) - 1, len(line) - 2)):
        out = (line[k][0] - line[j][0], line[k][1] - line[j][1], 0.0)
        g.face([(*lo[k], z0(*line[k])), (*hi[k], z0(*line[k])), (*hi[k], z1(*line[k])), (*lo[k], z1(*line[k]))],
               "palette", out, colour)


def seat_wall_lines():
    w = S.PROM_SEAT
    return [S.route_line(a, b, w["offset"], 0.8) for a, b in w["runs"]]


def build_promenade(col):
    """Street_Promenade: the curved white seat-walls on the land side and the white steps down to the cove sand."""
    g = Solid()
    w = S.PROM_SEAT
    for line in seat_wall_lines():
        _strip_wall(g, line, w["width"], lambda x, y: ST.ground(x, y) - 0.15, lambda x, y: ST.ground(x, y) + w["height"],
                    "render_white")
        near = min(S.route_distance(*p) for p in S.offset(line, -w["width"] / 2))
        PROM_REPORT.append((f"seat-wall {S.line_length(line):.1f} m", near))
    # beach steps on the sea side: top level with the road, down to the sand; risers white, treads concrete
    st = S.PROM_STEPS
    t0, t1 = st["run"]
    stations = [t0 + (t1 - t0) * k / 4 for k in range(5)]
    top = min(S.ROUTE[int(t)]["track_z"] for t in stations) + 0.02

    def sand(t, d):
        return ST.ground(*S.route_side(t, -d))
    n = 1
    while n < 12 and any(top - n * st["rise"] > sand(t, st["start"] + 0.35 + n * st["tread"]) + 0.05 for t in stations):
        n += 1
    edges = [st["start"], st["start"] + 0.35] + [st["start"] + 0.35 + k * st["tread"] for k in range(1, n + 1)]
    floor = min(sand(t, edges[-1] + 0.3) for t in stations) - 0.4
    pt3 = lambda t, d, z: (*S.route_side(t, -d), z)                                   # noqa: E731
    for ta, tb in zip(stations, stations[1:]):
        (_p, u) = S.route_at((ta + tb) / 2)
        sea = (u[1], -u[0], 0.0)
        for k in range(n + 1):
            z = top - k * st["rise"]
            d0, d1 = edges[k], edges[k + 1]
            g.face([pt3(ta, d0, z), pt3(tb, d0, z), pt3(tb, d1, z), pt3(ta, d1, z)], "palette", (0, 0, 1), "concrete")
            zn = z - st["rise"] if k < n else floor
            g.face([pt3(ta, d1, z), pt3(tb, d1, z), pt3(tb, d1, zn), pt3(ta, d1, zn)], "palette", sea, "render_white")
    for t, sign in ((stations[0], -1.0), (stations[-1], 1.0)):                        # the two cheeks
        (_p, u) = S.route_at(t)
        for k in range(n + 1):
            z = top - k * st["rise"]
            g.face([pt3(t, edges[k], z), pt3(t, edges[k + 1], z), pt3(t, edges[k + 1], floor), pt3(t, edges[k], floor)],
                   "palette", (sign * u[0], sign * u[1], 0.0), "render_white")
    PROM_REPORT.append((f"beach steps ({n} risers, top {top:.2f} m, at or below road level)", st["start"]))
    E.new_object("Street_Promenade", g.mesh("Street_Promenade"), col)
    print(f"  promenade (V15c): seat-walls {', '.join(f'{S.line_length(l):.1f} m' for l in seat_wall_lines())}; "
          f"beach steps {n} risers; {sum(len(f[0]) - 2 for f in g.faces)} triangles")


def promenade_placements(veg, props):
    """(plants, props, removed): the promenade's instanced dressing and the older props it replaces."""
    i0, i1 = S.PROM
    walls = seat_wall_lines()
    masts = [(t, S.route_side(t, S.PROM_MAST["offset"])) for t in S.PROM_MAST["at"]]
    solids = []                                           # (x, y, radius): our own things that need room
    for line in walls:
        solids += [(x, y, 1.1) for x, y in line]
    solids += [(x, y, 1.6) for _t, (x, y) in masts]
    machines = []
    for f in S.PROM_FITNESS:
        u = S.bearing(f["bearing"])
        for k, proto in enumerate(f["protos"]):
            x, y = S.pt(f["at"], u, (k - (len(f["protos"]) - 1) / 2) * 2.3)
            machines.append((proto, x, y, f["bearing"]))
            solids.append((x, y, 1.6))
    st = S.PROM_STEPS
    solids += [(*S.route_side(t, -(st["start"] + 1.0)), 2.0) for t in (st["run"][0], sum(st["run"]) / 2, st["run"][1])]

    def near_route(p):
        k = min(range(i0 - 4, i1 + 3), key=lambda i: (S.ROUTE[i]["x"] - p["x"]) ** 2 + (S.ROUTE[i]["y"] - p["y"]) ** 2)
        return i0 - 3 <= k <= i1 + 1 and S.route_distance(p["x"], p["y"]) < 16.0
    removed = [p for p in props if (p["proto"] == "Prop_LampRoad" and near_route(p))
               or (not p["proto"].startswith("Prop_Railing")
                   and any(math.hypot(p["x"] - x, p["y"] - y) < r for x, y, r in solids))]
    gone = {id(p) for p in removed}
    others = [p for p in props if id(p) not in gone] + list(veg)
    new_p, new_v, skipped = [], [], {}

    def add(lst, proto, x, y, rot=0.0, scale=1.0, clear=S.PROP_CLEAR):
        d = S.route_distance(x, y)
        if d < clear:
            skipped[proto] = skipped.get(proto, 0) + 1
            return False
        lst.append(_entry(proto, x, y, rot, scale))
        return True

    # bollards: along the land side (and the sea-side lawn at the start), one every 2.75 m; gaps at the path mouths
    # (street_plan), at seat-walls, masts and older props
    b = S.PROM_BOLLARD
    gaps = {}
    for side, (ta, tb) in ((1, b["land"]), (-1, b["sea"])):
        last, t = None, ta
        while t <= tb:
            x, y = S.route_side(t, side * b["offset"])
            _p, u = S.route_at(t)
            t += 0.05
            if last is not None and math.hypot(x - last[0], y - last[1]) < b["step"]:
                continue
            why = None
            if side > 0 and any(g0 <= t <= g1 for g0, g1 in b["gaps"]):
                why = "path"
            elif any(math.hypot(x - sx, y - sy) < r + 0.3 for sx, sy, r in solids):
                why = "seat-wall, mast or machine"
            elif E.in_building(x, y, 0.6):
                why = "building"
            elif any(math.hypot(x - q["x"], y - q["y"]) < (1.3 if q["proto"].startswith("Veg_") else 0.9) for q in others):
                why = "older prop or plant"
            else:
                top = S.layer_at(x, y)
                if top is not None and top["mat"] in ("asphalt", "bays"):
                    why = "car park"
            if why:
                gaps[why] = gaps.get(why, 0) + 1
                last = (x, y)
                continue
            if add(new_p, "Prop_BollardStone", x, y, heading_rot(u)):
                last = (x, y)
    for t, (x, y) in masts:
        add(new_p, "Prop_FloodMast", x, y, heading_rot(S.route_at(t)[1]))
    for proto, x, y, brg in machines:
        add(new_p, proto, x, y, 90.0 - brg)
    o = S.OLD_OLIVE
    add(new_v, "Veg_Olive", o["at"][0], o["at"][1], 205.0, o["scale"], clear=S.PROP_CLEAR + 2.5 * o["scale"])
    print(f"  promenade (V15c): bollard gaps {gaps}" + (f"; left out, too close to the road: {skipped}" if skipped else ""))
    return new_v, new_p, removed


UPPER_PATH_S = (772.0, 810.0)      # V18: the straight of the upper promenade path towards the tunnel forecourt (A4)
UPPER_PATH_STEP = 7.5              # m between seat blocks (orthophoto: about 6 on the north edge, 3 on the south edge)
UPPER_PATH_OFF = 6.9               # m off the centreline: the real path is 6-7 m wide, our road 8 m, so the blocks
                                   # stand on the lawn edge just outside the 6.6 m clearance


def upper_path_seats(props):
    """V18 (team walk 7 Nov, video IMG_7723 7:15-7:57): white block seats on both sides of the upper promenade path."""
    pts = E.route_points()
    s_acc, out = [0.0], []
    for a, b in zip(pts, pts[1:]):
        s_acc.append(s_acc[-1] + math.hypot(b["x"] - a["x"], b["y"] - a["y"]))
    taken = [(p["x"], p["y"]) for p in props]
    nxt = {1: UPPER_PATH_S[0], -1: UPPER_PATH_S[0] + UPPER_PATH_STEP / 2}
    for i in range(1, len(pts) - 1):
        for side in (1, -1):
            if not (nxt[side] <= s_acc[i] <= UPPER_PATH_S[1]) or (side == -1 and s_acc[i] > UPPER_PATH_S[0] + 26.0):
                continue
            nxt[side] = s_acc[i] + UPPER_PATH_STEP
            a, b = pts[i - 1], pts[i + 1]
            d = math.hypot(b["x"] - a["x"], b["y"] - a["y"])
            nx, ny = -(b["y"] - a["y"]) / d * side, (b["x"] - a["x"]) / d * side          # side 1 = left = north here
            x, y = pts[i]["x"] + nx * UPPER_PATH_OFF, pts[i]["y"] + ny * UPPER_PATH_OFF
            if S.route_distance(x, y) < S.PROP_CLEAR or any(math.hypot(x - tx, y - ty) < 2.0 for tx, ty in taken):
                continue
            out.append({"proto": "Prop_SeatBlockPlaza", "x": round(x, 3), "y": round(y, 3), "z": round(E.ground_z(x, y) + 0.04, 3),
                        "rot_z": round(math.atan2(b["y"] - a["y"], b["x"] - a["x"]), 4), "scale": 1.0})
            taken.append((x, y))
    print(f"  upper promenade path (V18): {len(out)} seat blocks, min "
          f"{min((S.route_distance(o['x'], o['y']) for o in out), default=0):.2f} m off the centreline")
    return out


# =====================================================================================================
# V18g: the dusty car park north-west of the roundabout and the N6 east of it. An explicit edit of the dressing
# lists after dressing_freeze.apply (called from dress_promenade), like the promenade's: nothing else moves.
# =====================================================================================================
def streets_v18g(veg, props):
    """(veg, props) without the older items that stand in the car park (saplings, reeds, cars) or on the N6's new
    paving, plus the parked cars, the scrub's bushes, the verges' bushes, the N6's lamps, palms and shrub row."""
    rng = random.Random(SEED + 18)
    lot = S.dust_outline()
    cars = [c[0] for c in CAR_KINDS]

    def goes(it):
        x, y = it["x"], it["y"]
        if S.inside(x, y, lot) and S.ring_distance(x, y, lot) > 0.6:
            return it["proto"].startswith(("Veg_", "Prop_Car"))
        top = S.layer_at(x, y)
        return top is not None and top["name"].startswith("N ") and (top["mat"] is not None or it["proto"].startswith("Veg_"))
    veg2, props2 = [p for p in veg if not goes(p)], [p for p in props if not goes(p)]
    removed = len(veg) - len(veg2) + len(props) - len(props2)
    plants, ours = [], []

    def car(x, y, nose):
        if S.route_distance(x, y) < S.CAR_CLEAR + 1.0:
            return
        rot = heading_rot(nose) + rng.uniform(-4.0, 4.0) + (180.0 if rng.random() < 0.35 else 0.0)
        ours.append(_entry(cars[rng.randrange(len(cars))], x, y, rot))

    # ---- the car park: loose rows on the dust (no bays, no lines) ----
    for row in S.DUST_ROWS:
        total = S.line_length(row["line"])
        t = 1.2
        while t < total:
            (x, y), u = S.at_s(row["line"], t)
            n = S.left_of(u)
            for side in ((-1, 1) if row["double"] else (1,)):
                if rng.random() < row["share"]:
                    off = side * 2.55 if row["double"] else rng.uniform(-0.3, 0.3)
                    car(x + n[0] * off + u[0] * rng.uniform(-0.15, 0.15), y + n[1] * off + u[1] * rng.uniform(-0.15, 0.15),
                        (n[0] * side, n[1] * side))
            t += 2.7
    r = S.DUST_ROAD_ROW
    x = r["x"][0]
    while x <= r["x"][1]:                                 # the row along the Marginal, noses to the road
        p, u = S.at_s(S.MEDIAN, S.med_s(x))
        off = r["offset"]
        while S.route_distance(*S.pt(p, u, 0.0, off)) < S.CAR_CLEAR + 2.0 and off < r["offset"] + 9.0:
            off += 0.5
        if rng.random() < r["share"]:
            q = S.pt(p, u, rng.uniform(-0.2, 0.2), off + rng.uniform(-0.4, 0.4))
            car(q[0], q[1], (u[1], -u[0]))
        x += 2.55

    # ---- bushes: in the scrub patches, on the two verges ----
    def bush(x, y, lo, hi, reeds=0.0):
        if S.route_distance(x, y) < S.PROP_CLEAR + 2.5:
            return
        proto = "Veg_Grass" if rng.random() < reeds else ("Veg_Shrub" if rng.random() < 0.5 else "Veg_Shrub_B")
        plants.append(_entry(proto, x, y, rng.uniform(0, 360), rng.uniform(lo, hi), z=ST.ground(x, y) - 0.15))
    for ring, n in zip(S.DUST_SCRUB, (20, 4, 7)):                 # dry clumps and a few low bushes in the scrub
        x0, x1 = min(q[0] for q in ring), max(q[0] for q in ring)
        y0, y1 = min(q[1] for q in ring), max(q[1] for q in ring)
        done = 0
        while done < n:
            x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
            if S.inside(x, y, ring) and S.ring_distance(x, y, ring) > 2.0:
                bush(x, y, 1.5, 2.3, reeds=0.6)
                done += 1
    for y in (-4.0, 9.0, 24.0, 40.0, 52.0, 71.0, 84.0, 101.0, 116.0, 131.0, 146.0):     # dark bushes west of the avenue
        x, yy = S.av_pt(y + rng.uniform(-2.0, 2.0), S.AV_WEST_WALK[1] + rng.uniform(3.0, 8.5))
        bush(x, yy, 1.8, 2.7)
    for x in (-284.0, -262.0, -247.0, -224.0, -203.0, -188.0, -166.0):                # low bushes on the Marginal's verge
        q = S.med_pt(x + rng.uniform(-3.0, 3.0), S.N_WALK[1] + rng.uniform(1.5, 4.5))
        bush(q[0], q[1], 1.2, 1.9)

    # ---- the N6 east of the roundabout ----
    walk_n, kerb_s = S.n6e_line(lambda row: row[3] + S.N6E_WALK[0] - 0.45, 1.0), S.n6e_line(4, 1.0)

    def at_x(line, x):
        return min(line, key=lambda q: abs(q[0] - x))
    for x in (-12.0, 24.0, 60.0, 96.0, 132.0, 166.0):             # lamps on the north sidewalk, arm to the road
        q = at_x(walk_n, x)
        ours.append(_entry("Prop_LampMarginal", q[0], q[1], heading_rot((0.0, -1.0))))
    for y in (113.0, 146.0):                                      # the avenue's median lamps go on north of y 100
        ours.append(_entry("Prop_LampAvenue", *S.av_pt(y, 6.95)))
    for x in (78.0, 108.0, 152.0):                                # lower lamps in the grass strip south of the road
        q = at_x(kerb_s, x)
        ours.append(_entry("Prop_LampSidewalk", q[0], q[1] - S.N6E_WALK[1] - 0.8, heading_rot((0.0, 1.0))))
    for k, (x, y) in enumerate(S.N6E_PALMS):                      # date palms in the grass strip
        plants.append(_entry("Veg_Palm" if k % 2 else "Veg_Palm_B", x, y, rng.uniform(0, 360), rng.uniform(0.8, 0.95),
                             z=ST.ground(x, y) - 0.1))
    for k in range(12):                                           # the row of clipped round shrubs south of the road
        x = -82.0 + k * 6.2 + rng.uniform(-0.6, 0.6)
        q = at_x(S.n6e_line(lambda row: row[4] - 7.4, 1.0), x) if x > -29.0 else S.east_pt((x + 78.5) / S.EAST_U[0], S.EAST["exit"] - 7.4)
        if S.route_distance(*q) >= S.PROP_CLEAR + 2.0:
            plants.append(_entry("Veg_Shrub" if k % 2 else "Veg_Shrub_B", q[0], q[1], rng.uniform(0, 360), rng.uniform(2.0, 2.5)))
    x0, x1 = S.N6E_BAYS
    n = int((x1 - x0) / S.BAY[0])
    for k in range(n):                                            # parked cars in the bay row, noses to the road
        if rng.random() < 0.42:
            x = x0 + (k + 0.5) * (x1 - x0) / n
            car(x, S.n6e_lot_edge(x) - 2.5 + rng.uniform(-0.2, 0.2), (0.0, 1.0))
    print(f"  streets (V18g): car park and N6: {removed} older items removed; {len(ours)} props "
          f"({sum(1 for it in ours if it['proto'].startswith('Prop_Car'))} parked cars) and {len(plants)} plants added")
    return veg2 + plants, props2 + ours


def carpark_v18h(veg, props):
    """V18h (builder Q): the parked cars of the Capricciosa forecourt, the coastal car park and the west car park,
    bay by bay as on the satellite view of 2026 (nearly full), aligned with the painted bays. The older cars in
    these places go. A car is left out where any of its corners would stand closer than PROP_CLEAR to the race
    centreline: the race road is wider than the real aisle, so both bay rows of the aisle stay empty where it runs
    between them. Own random sequence: nothing outside these car parks moves."""
    rng = random.Random(SEED + 26)
    names = ("E concrete", "F aisle concrete", "F asphalt square", "E brick bays", "H west car park", "H island")
    rings = [lay["ring"] for lay in S.LAYERS if lay["name"] in names]

    def mine(it):
        x, y = it["x"], it["y"]
        if it["proto"] == "Prop_BollardSquare":                   # the pale square posts along both kerbs of the car park
            t = S.s_kerb(x, y)
            d = (x - S.NE_KERB_A[0]) * -S.left_of(S.NE_KERB_U)[0] + (y - S.NE_KERB_A[1]) * -S.left_of(S.NE_KERB_U)[1]
            return -6.0 < t < 112.0 and -3.0 < d < 21.0
        if it["proto"] == "Prop_TimberPost":                      # the posts on the grass island at the throat
            return S.inside(x, y, S.GRASS_ISLAND)
        return it["proto"].startswith("Prop_Car") and x < -135.0 and any(S.inside(x, y, r) for r in rings)
    props2 = [p for p in props if not mine(p)]
    apron = next(lay["ring"] for lay in S.LAYERS if lay["name"] == "H apron")     # lawn until V18h: its plants go
    veg2 = [p for p in veg if not S.inside(p["x"], p["y"], apron)]
    cars = [c[0] for c in CAR_KINDS]
    ours, left = [], 0

    def fits(x, y, nose):
        side = S.left_of(nose)
        return all(S.route_distance(x + nose[0] * a + side[0] * b, y + nose[1] * a + side[1] * b) >= S.PROP_CLEAR + 0.2
                   for a in (-2.3, 0.0, 2.3) for b in (-1.0, 1.0))

    def row(frame, t0, n, s_mid, nose, share, skip=(), nudge=None):
        nonlocal left
        for k in range(n):
            if k in skip or rng.random() > share:
                continue
            t = t0 + (k + 0.5) * S.BAY[0] + rng.uniform(-0.1, 0.1) + (nudge or {}).get(k, 0.0)
            x, y = frame(t, s_mid + rng.uniform(-0.2, 0.25))
            if not fits(x, y, nose):
                left += 1
                continue
            rot = heading_rot(nose) + rng.uniform(-2.5, 2.5) + (180.0 if rng.random() < 0.25 else 0.0)
            ours.append(_entry(cars[rng.randrange(len(cars))], x, y, rot))

    hp, bp, kp = S.hedge_pt, S.brick_pt, S.kerb_pt
    hu, bu, ku = S.HEDGE_U, S.BRICK_U, S.NE_KERB_U
    back = (-S.left_of(hu)[0], -S.left_of(hu)[1])                 # from the bay front towards the hedge
    row(hp, 0.0, 7, 2.5, back, 0.94)
    row(hp, S.PORTAL_T[1], 9, 2.5, back, 0.94)
    row(bp, 0.0, int(S.BRICK_LEN / S.BAY[0]), 2.5, S.left_of(bu), 0.9, skip={1, 3})
    ne = S.left_of(ku)
    sw = (-ne[0], -ne[1])
    nudge = {}
    for t in S.PIT_T:                                             # a palm's pit stands on the line between two bays:
        k = int(round(t / S.BAY[0]))                              # the cars beside it keep 0.3 m more to their far side
        nudge[k - 1], nudge[k] = -0.3, 0.3
    # bay 37 holds the coast section's first slim palm (env_coast WASHINGTONIAS, placed only if nothing stands within
    # 2.5 m: a car there would drop it and shift that module's random sequence): no car in it, its neighbours keep off
    nudge[36], nudge[38] = -0.4, 0.3
    row(kp, 0.0, S.NE_BAYS, 2.6, ne, 0.95, skip={37}, nudge=nudge)
    z1 = int((S.s_kerb(-198.0, -82.0) - S.SW_ROW_T0) / S.BAY[0])
    yb = int((S.s_kerb(-177.0, -102.0) - S.SW_ROW_T0) / S.BAY[0])
    row(lambda t, s: kp(t, s), S.SW_ROW_T0, 41, 15.0, sw, 0.95, skip={z1 - 1, z1, yb})
    row(kp, S.CENTRAL_T0, 11, 20.0, ne, 0.95)
    sa, su = (-241.0, -72.6), S.unit(39.0, -8.9)
    row(lambda t, s: S.pt(sa, su, t, -s), 0.0, 16, 2.5, (su[1], -su[0]), 0.95)
    wa, wu = (-348.0, 16.5), S.unit(-41.0, 21.0)                  # the west car park: the row along its north edge
    row(lambda t, s: S.pt(wa, wu, t, s), 2.0, 15, 2.8, (-S.left_of(wu)[0], -S.left_of(wu)[1]), 0.85)
    row(lambda t, s: S.pt(wa, wu, t, s), 9.5, 9, 10.3, S.left_of(wu), 0.8)
    n_cars = len(ours)
    # peeled timber bollards (1 m, every 2.5 m) instead of the pale square posts: behind the calcada strip in the
    # west half of the north-east edge, at the kerb further east, and on the grass strip of the south-west edge
    def post(proto, x, y):
        if S.route_distance(x, y) >= S.PROP_CLEAR:
            ours.append(_entry(proto, x, y, rng.uniform(0.0, 360.0)))
    t = 2.5
    while t <= 103.0:
        post("Prop_TimberBollard", *kp(t, -4.55 if t <= 33.0 else -0.75))
        t += 2.5
    t = 28.5
    while t <= 91.0:
        post("Prop_TimberBollard", *kp(t, 18.15))
        t += 2.7
    isl = S.GRASS_ISLAND                                          # white round bollards on the island at the throat
    cx, cy = sum(q[0] for q in isl) / len(isl), sum(q[1] for q in isl) / len(isl)
    for k in (3, 4, 5, 6):
        x, y = isl[k]
        d = math.hypot(cx - x, cy - y)
        ours.append(_entry("Prop_BollardWhite", x + (cx - x) * 0.7 / d, y + (cy - y) * 0.7 / d, 0.0, z=surface(x, y) + 0.25))
    print(f"  streets (V18h): car parks at the Capricciosa: {len(props) - len(props2)} older cars and posts removed, {n_cars} "
          f"cars parked in the bays, {len(ours) - n_cars} bollards; {left} bays stay empty because the race road is too near; {len(veg) - len(veg2)} plants removed "
          f"from the concrete apron at the lawn's tip")
    return veg2, props2 + ours


def dress_promenade(veg, props):
    """V15c, called after dressing_freeze.apply: an explicit edit of the dressing lists. Removes the dark road lamps
    along the promenade (the real ones are white flood masts) and the few older props that stand where a seat-wall,
    mast, machine or the beach steps now are; adds the limestone bollards, flood masts, fitness machines and the old
    olive on the lawn path. Everything else stays exactly as it was. Rewrites the dressing files."""
    veg, props = streets_v18g(veg, props)         # V18g: the dusty car park and the N6 east of the roundabout
    veg, props = carpark_v18h(veg, props)         # V18h: the cars of the Capricciosa forecourt and the coastal car park
    plants, ours, removed = promenade_placements(veg, props)
    gone = {id(p) for p in removed}
    veg2, props2 = list(veg) + plants, [p for p in props if id(p) not in gone] + ours
    import env_coast                       # V16e: the coast section's own edits (berm, forecourt, cove, portal deck)
    veg2, props2 = env_coast.dress(veg2, props2)
    props2 += upper_path_seats(props2)       # V18 (team walk 7 Nov 2026): white seat blocks along the upper promenade path
    for fname, items in (("placements_vegetation.json", veg2), ("placements_props.json", props2)):
        path = os.path.join(E.OUT, fname)
        data = json.load(open(path))
        data["placements"] = items
        counts = {}
        for it in items:
            counts[it["proto"]] = counts.get(it["proto"], 0) + 1
        data["counts"] = counts
        json.dump(data, open(path, "w"), indent=1)
    added = {}
    for it in plants + ours:
        d = S.route_distance(it["x"], it["y"])
        n, lo = added.get(it["proto"], (0, 1e9))
        added[it["proto"]] = (n + 1, min(lo, d))
    print("  promenade (V15c): removed " + (", ".join(sorted(f"{p['proto']}@{p['x']:.0f},{p['y']:.0f}" for p in removed)) or "nothing"))
    print("  promenade (V15c): clearance to the race centreline (minimum per kind, limit "
          f"{S.PROP_CLEAR} m for everything that stands up):")
    for proto, (n, lo) in sorted(added.items()):
        print(f"    {proto:<22s} {n:3d} placed   min {lo:5.2f} m")
    for what, lo in PROM_REPORT:
        print(f"    {what:<34s} min {lo:5.2f} m")
    return veg2, props2


def build(col):
    build_kerbs(col)
    build_structures(col)
    build_promenade(col)            # V15c
