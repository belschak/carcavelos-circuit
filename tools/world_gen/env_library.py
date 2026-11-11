"""The library (building H, w496301632) as in Luc's photos and the campus survey (V10i, 2026-11-11; plaza_plan.py).

A long white box (south wing): a blank white upper wall over a glazed ground floor between white piers; at its south
end the loggia (a porch under the roof: two full-height square columns, a one-storey corner pier, a first-floor
balcony behind the front 2 m, glazing at the back, a solid east wall). Across its north end a lower block whose west
12 m are a bridge: a white box with a glass ribbon on Floor 2 over the drivable passage (3.4 m clear), standing on a
white pier wall on the courtyard side and on the block's glazed ground floor on the other. Grey roof with rows of
roof lights and dark panels as in the satellite picture. No lettering.
One object B_w496301632_... (collider in Unity), replacing the generic box of env_buildings.
"""
import math

import bmesh
import bpy

import env_common as E
import plaza_plan as P

OSM_ID = "w496301632"
BAY = 5.5
PIER_W = 0.9
INSET = 0.4                 # the ground-floor glazing stands this far behind the wall plane
Z0 = P.DECK_Z - 0.05        # everything starts just under the deck
ZG = P.FLOOR1 + 3.6         # top of the ground floor (underside of the upper wall)
ZT = P.LIB_TOP              # roof of the south wing
ZN = P.FLOOR1 + 7.4         # roof of the north block and the bridge
ZS = P.DECK_Z + P.PASSAGE_CLEAR      # soffit of the bridge
KEYS = ["wall", "glass", "roof", "ribbon", "dark", "rail"]


def _mat(key):
    return {"wall": lambda: E.material("B_wall_white", "#EAEAEC", 0.85),
            "glass": lambda: E.material("B_glass", "#2E3A46", roughness=0.08, metallic=0.5),
            "roof": lambda: E.material("B_roof", "#D5D3CE", 0.9),
            "ribbon": lambda: E.material("library_ribbon", "#7C9DB0", roughness=0.12, metallic=0.4),
            "dark": lambda: E.material("plaza_dark", "#3B3E42", 0.7),
            "rail": lambda: E.material("plaza_rail", "#4A4E53", roughness=0.45, metallic=0.6)}[key]()


class B:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def quad(self, pts, key, facing=None, band=False):
        """Face through pts; turned so its normal looks along `facing` (3-D direction) if given."""
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        f.normal_update()
        if facing is not None and f.normal.x * facing[0] + f.normal.y * facing[1] + f.normal.z * facing[2] < 0:
            f.normal_flip()
        f.material_index = KEYS.index(key)
        n = f.normal
        zs = [l.vert.co.z for l in f.loops]
        for loop in f.loops:
            co = loop.vert.co
            if abs(n.z) > 0.7:
                loop[self.uv].uv = (co.x / 3.0, co.y / 3.0)
            else:
                h = math.hypot(n.x, n.y) or 1.0
                along = (co.x * -n.y + co.y * n.x) / h
                if band:                                        # window band: 6 m wide, one band high
                    loop[self.uv].uv = (along / 6.0, (co.z - min(zs)) / max(max(zs) - min(zs), 0.01))
                else:
                    loop[self.uv].uv = (along / 3.0, co.z / 3.0)
        return f

    def wall(self, a, b, z0, z1, out, key="wall", band=False):
        self.quad([(a[0], a[1], z0), (b[0], b[1], z0), (b[0], b[1], z1), (a[0], a[1], z1)], key, (out[0], out[1], 0.0), band)

    def flat(self, ring, z, key, up=True):
        self.quad([(x, y, z) for x, y in ring], key, (0, 0, 1 if up else -1))

    def prism(self, ring, z0, z1, key="wall", top=True):
        cx, cy = sum(p[0] for p in ring) / len(ring), sum(p[1] for p in ring) / len(ring)
        for a, b in zip(ring, ring[1:] + ring[:1]):
            mx, my = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cy
            self.wall(a, b, z0, z1, (mx, my), key)
        if top:
            self.flat(ring, z1, key)


def _edge(a, b):
    """Unit direction, outward normal (for a counter-clockwise outline) and length of the edge a-b."""
    ex, ey = b[0] - a[0], b[1] - a[1]
    d = math.hypot(ex, ey)
    return (ex / d, ey / d), (ey / d, -ex / d), d


def _at(a, t, out, u, off=0.0):
    """Point u metres along the edge from a, off metres inside the wall plane."""
    return (a[0] + t[0] * u - out[0] * off, a[1] + t[1] * u - out[1] * off)


def colonnade(b, a, c, z1, start=0.0, end=None, upper=None):
    """Ground floor along the edge a-c between `start` and `end` metres: white piers PIER_W wide at the ends of bays
    of about BAY, dark glazing INSET behind the wall plane between them, a white soffit over the recess.
    `upper`: top of the blank white wall above (None = no wall above)."""
    t, out, length = _edge(a, c)
    end = length if end is None else end
    if upper is not None:
        b.wall(_at(a, t, out, start), _at(a, t, out, end), z1, upper, out)
    bays = max(1, int(round((end - start) / BAY)))
    step = (end - start - PIER_W) / bays
    for k in range(bays + 1):
        u0 = start + k * step
        p0, p1 = _at(a, t, out, u0), _at(a, t, out, u0 + PIER_W)
        q0, q1 = _at(a, t, out, u0, INSET), _at(a, t, out, u0 + PIER_W, INSET)
        b.wall(p0, p1, Z0, z1, out)
        b.wall(q0, p0, Z0, z1, (-t[0], -t[1]))
        b.wall(p1, q1, Z0, z1, t)
        if k < bays:
            g0, g1 = q1, _at(a, t, out, u0 + step, INSET)
            b.wall(g0, g1, Z0, z1, out, "glass", band=True)
            b.quad([(*p1, z1), (*_at(a, t, out, u0 + step), z1), (*g1, z1), (*g0, z1)], "wall", (0, 0, -1))


def build(bld, index):
    """Returns (mesh, empty details mesh) like env_buildings.build_one."""
    b = B()
    sw, se, ne, nw = P.LIB_SOUTH_WING
    # ---- south wing --------------------------------------------------------------------------------------
    ts, outs, ls = _edge(sw, se)                       # south edge (the loggia front)
    north = (-outs[0], -outs[1])
    depth = P.LOGGIA_DEPTH
    tw, outw, lw = _edge(nw, sw)                       # west facade, running south
    te, oute, le = _edge(se, ne)                       # east facade, running north
    # west facade: blank upper wall along its whole length, colonnade north of the loggia
    wd = depth / abs(tw[0] * north[0] + tw[1] * north[1])          # the loggia's depth measured along the west wall
    colonnade(b, nw, sw, ZG, 0.0, lw - wd, upper=None)
    b.wall(nw, sw, ZG, ZT, outw)
    # east facade: solid for the loggia's depth, then the colonnade
    ed = depth / abs(te[0] * north[0] + te[1] * north[1])
    b.wall(se, _at(se, te, oute, ed), Z0, ZT, oute)
    b.wall(se, _at(se, te, oute, ed), Z0, ZT, (-oute[0], -oute[1]))   # its inner face, seen from the porch
    colonnade(b, se, ne, ZG, ed, le, upper=None)
    b.wall(_at(se, te, oute, ed), ne, ZG, ZT, oute)
    # roof slab: top, the beam over the loggia front, the soffit over the porch
    b.flat([sw, se, ne, nw], ZT, "roof")
    b.wall(sw, se, ZT - 0.7, ZT, outs)
    l1 = _at(se, te, oute, ed)                                       # back line of the porch
    l0 = P._hit((sw[0] + north[0] * depth, sw[1] + north[1] * depth), ts, nw, sw)
    b.quad([(*sw, ZT - 0.7), (*se, ZT - 0.7), (*l1, ZT - 0.7), (*l0, ZT - 0.7)], "wall", (0, 0, -1))
    # back of the porch: glazing on both floors with a white band between
    b.wall(l0, l1, Z0, ZG - 0.3, (-north[0], -north[1]), "glass", band=True)
    b.wall(l0, l1, ZG - 0.3, ZG + 0.9, (-north[0], -north[1]))
    b.wall(l0, l1, ZG + 0.9, ZT - 0.7, (-north[0], -north[1]), "glass", band=True)
    # first-floor balcony: slab behind the front 2 m, its edge and a railing
    f0 = P._hit((sw[0] + north[0] * 2.0, sw[1] + north[1] * 2.0), ts, nw, sw)
    f1 = P._hit((se[0] + north[0] * 2.0, se[1] + north[1] * 2.0), ts, se, ne)
    b.quad([(*f0, ZG - 0.3), (*f1, ZG - 0.3), (*l1, ZG - 0.3), (*l0, ZG - 0.3)], "wall", (0, 0, -1))
    b.quad([(*f0, ZG), (*f1, ZG), (*l1, ZG), (*l0, ZG)], "wall", (0, 0, 1))
    b.wall(f0, f1, ZG - 0.3, ZG, (-north[0], -north[1]))
    b.wall(f0, f1, ZG + 1.0, ZG + 1.06, (-north[0], -north[1]), "rail")
    b.wall(f0, f1, ZG + 0.1, ZG + 1.0, (-north[0], -north[1]), "dark")      # dark bar railing read as one panel
    # the two full-height columns and the corner pier
    for cx, cy in P.LOGGIA_COLUMNS:
        h = P.COLUMN / 2
        ring = [(cx + ts[0] * sx * h + north[0] * sy * h, cy + ts[1] * sx * h + north[1] * sy * h)
                for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        b.prism(ring, Z0, ZT - 0.7, top=False)
    px, py = P.LOGGIA_PIER
    h = P.COLUMN / 2
    ring = [(px + tw[0] * sx * h + outw[0] * sy * h, py + tw[1] * sx * h + outw[1] * sy * h) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    b.prism(ring, Z0, ZG, top=False)
    # roof lights (white boxes) and dark panels, in two rows along the wing (satellite picture)
    for row, key, wid, off in ((0, "wall", 1.1, 0.30), (1, "dark", 3.6, 0.68)):
        for k in range(6):
            u = 9.0 + k * 3.6
            c0 = (sw[0] + north[0] * u + ts[0] * ls * off, sw[1] + north[1] * u + ts[1] * ls * off)
            hw, hl = wid / 2, 1.25
            ring = [(c0[0] + ts[0] * sx * hw + north[0] * sy * hl, c0[1] + ts[1] * sx * hw + north[1] * sy * hl)
                    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            b.prism(ring, ZT, ZT + (0.45 if key == "wall" else 0.25), key)
    # the wing's north wall above the lower north block
    b.wall(ne, nw, ZN, ZT, (north[0], north[1]))

    # ---- north block east of the passage -------------------------------------------------------------------
    ps, pn = P.passage_point(-0.1, P.PASSAGE_HALF), P.passage_point(P.PASSAGE_LEN + 0.1, P.PASSAGE_HALF)
    e0, e1 = P.LIB_NORTH_BLOCK[1], P.LIB_NORTH_BLOCK[2]
    ring = [ps, e0, e1, pn]
    tn, outn, ln = _edge(e1, pn)
    te2, oute2, le2 = _edge(e0, e1)
    colonnade(b, e0, e1, ZG, upper=ZN)                               # east face
    colonnade(b, e1, pn, ZG, upper=ZN)                               # north face
    colonnade(b, pn, ps, ZS, upper=None)                             # the passage's east side: glazing between piers
    b.wall(ps, nw, Z0, ZN, _edge(ps, nw)[1])                         # short south face west of the wing
    b.wall(ne, e0, Z0, ZN, _edge(ne, e0)[1])
    b.flat(ring, ZN, "roof")

    # ---- the bridge over the passage and its pier -------------------------------------------------------------
    w0, w1 = P.PIER[0], P.PIER[3]                                    # the bridge's west face = the pier's west face
    bridge = [w0, ps, pn, w1]
    b.prism(P.PIER, Z0, ZS, top=False)
    b.flat(bridge, ZS, "wall", up=False)
    b.flat(bridge, ZN, "roof")
    b.wall(w1, w0, ZS, ZN, _edge(w1, w0)[1])
    for a, c in ((w0, ps), (pn, w1)):                                # south and north face with the glass ribbon
        t, out, length = _edge(a, c)
        b.wall(a, c, ZS, ZS + 0.75, out)
        b.wall(a, c, ZN - 0.55, ZN, out)
        b.wall(a, _at(a, t, out, 0.5), ZS + 0.75, ZN - 0.55, out)
        b.wall(_at(a, t, out, length - 0.5), c, ZS + 0.75, ZN - 0.55, out)
        panes, gap = 9, 0.1
        w = (length - 1.0) / panes
        for k in range(panes):
            g0, g1 = _at(a, t, out, 0.5 + k * w, 0.08), _at(a, t, out, 0.5 + (k + 1) * w - gap, 0.08)
            b.wall(g0, g1, ZS + 0.75, ZN - 0.55, out, "ribbon")
            b.wall(_at(a, t, out, 0.5 + (k + 1) * w - gap), _at(a, t, out, 0.5 + (k + 1) * w), ZS + 0.75, ZN - 0.55, out)

    mesh = bpy.data.meshes.new("B_" + bld["id"])
    b.bm.to_mesh(mesh)
    b.bm.free()
    for key in KEYS:
        mesh.materials.append(_mat(key))
    return mesh, bpy.data.meshes.new("BD_" + bld["id"])
