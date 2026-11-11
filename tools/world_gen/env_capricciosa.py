"""Capricciosa hairpin forecourt, restaurant sea side and car-park corner (V16g, 2026-11-11), from capricciosa_plan.py.

Called once by build_world.build_dressing (dress): builds the static pieces, adds one prototype and edits the dressing
lists after dressing_freeze (like env_street_objects.dress_promenade), so nothing outside this area moves.

  Street_Capricciosa        the entrance wedge with its timber stair and cream canopy, the court's planter and cable
                            balustrade, the blank sign wall, the rubble sea wall with its stone walk and footing ledge,
                            the car-park corner (stone-clad parapet, mast plinth, stair, planted retaining boxes, rubble
                            parapet with a terracotta cap, limestone cliff and outcrops, shower), the terracotta-red
                            walls of the west beach stair. All of it is at least 13 m from the race centreline.
  Prop_ScreenBox            a blank dark screen on a thick post (the advert screen's place; nothing on it)

Materials: the street palette, slats (boards), concrete, hedge and white wall of env_streets, the timber of
env_beachfront, and ONE new one, cap_rubble (make_capricciosa_textures.py), for sea wall, parapet, cliff and rocks.
"""
import json
import math
import os
import random

import bpy

import env_common as E
import env_streets as ST
import street_plan as S
import capricciosa_plan as CP
from env_street_objects import Solid, Proto, surface, _entry, heading_rot, facing_rot

SEED = 20261116
MIN_CLEAR = [1e9, ""]           # nearest static piece to the race centreline (printed; the build stops below PROP_CLEAR)


def _mat(key):
    if key in ("rubble", "rock"):          # rock: the same material, mapped so large that a face shows one stone's tone
        import env_materials
        mat = E.material("cap_rubble", "#B49C7E", 0.92)
        if not mat.get("v16g_textured") and env_materials.upgrade(mat, "cap_rubble", 0.92):
            mat["v16g_textured"] = 1
        return mat
    if key == "timber":
        import env_beachfront
        return env_beachfront.timber_material()
    if key == "boards":
        return ST.mat("slats")
    return ST.mat(key)


class CSolid(Solid):
    """Solid with this module's materials. Boards: the slat texture stretched to 4 boards per 0.85 m (about 18 cm
    boards with a shadow gap, survey Appendix G 2.2)."""
    TILE = dict(Solid.TILE, boards=0.85, rubble=2.2, rock=26.0, timber=2.0)

    def mesh(self, name):
        keys = []
        for _f, key, _c in self.faces:
            if key not in keys:
                keys.append(key)
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], [f[0] for f in self.faces])
        for k in keys:
            mesh.materials.append(_mat(k))
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

    def quad(self, a, b, c, d, key, out, colour=None):
        self.face([a, b, c, d], key, out, colour)


def _clear(what, pts):
    d = min(S.route_distance(p[0], p[1]) for p in pts)
    if d < MIN_CLEAR[0]:
        MIN_CLEAR[0], MIN_CLEAR[1] = d, what
    if d < S.PROP_CLEAR:
        raise RuntimeError(f"env_capricciosa: {what} stands {d:.2f} m from the race centreline (limit {S.PROP_CLEAR} m)")
    return d


# =====================================================================================================
# The entrance wedge and the court behind it
# =====================================================================================================
def portal(g):
    """Funnel-shaped white wedge: two board-clad trapezoid walls, a cream canopy falling to the back, a full-width
    timber stair going down as far as the ground lets it, a dark doorway at the back. Returns the report line."""
    hp = S.hedge_pt
    p = CP.PORTAL
    tm, s0, s1 = p["t_mid"], p["s0"], p["s1"]
    zp = surface(*hp(tm, s0 - 0.5))
    lo = min(ST.ground(*hp(tm + k, s)) for k in (-2.5, 0.0, 2.5) for s in (s0, s1)) - 0.5
    zf, zb = zp + p["front_h"], zp + p["back_h"]
    corners = []
    for side in (-1.0, 1.0):
        of, inf = hp(tm + side * p["mouth"] / 2, s0), hp(tm + side * (p["mouth"] / 2 - p["wall"]), s0)
        ob, inb = hp(tm + side * (p["back"] / 2 + 0.22), s1), hp(tm + side * p["back"] / 2, s1)
        corners += [of, ob]
        out = S.unit(of[0] - inf[0], of[1] - inf[1])
        back = S.unit(ob[0] - of[0], ob[1] - of[1])
        g.quad((*of, lo), (*ob, lo), (*ob, zb + 0.12), (*of, zf), "boards", (out[0], out[1], 0))
        g.quad((*inf, lo), (*inb, lo), (*inb, zb + 0.12), (*inf, zf), "boards", (-out[0], -out[1], 0))
        g.quad((*of, lo), (*inf, lo), (*inf, zf), (*of, zf), "boards", (-back[0], -back[1], 0))
        g.quad((*ob, lo), (*inb, lo), (*inb, zb + 0.12), (*ob, zb + 0.12), "boards", (back[0], back[1], 0))
        g.quad((*of, zf), (*inf, zf), (*inb, zb + 0.12), (*ob, zb + 0.12), "boards", (0, 0, 1))
    _clear("the entrance wedge", corners)
    half = lambda s: (p["mouth"] / 2 - p["wall"]) + (p["back"] / 2 - (p["mouth"] / 2 - p["wall"])) * (s - s0) / (s1 - s0)   # noqa: E731
    # canopy: one cream plane from the front top edge down to the door head, seen from above and from below
    a, b = hp(tm - half(s0 + 0.08), s0 + 0.08), hp(tm + half(s0 + 0.08), s0 + 0.08)
    c, d = hp(tm + half(s1), s1), hp(tm - half(s1), s1)
    for dz, up in ((0.0, 1), (-0.05, -1)):
        g.quad((*a, zf - 0.12 + dz), (*b, zf - 0.12 + dz), (*c, zb + dz), (*d, zb + dz), "palette", (0, 0, up), "cream")
    g.quad((*a, zf - 0.17), (*b, zf - 0.17), (*b, zf - 0.12), (*a, zf - 0.12), "palette",
           (S.left_of(S.HEDGE_U)[0], S.left_of(S.HEDGE_U)[1], 0), "cream")
    # stair: full width, risers of 0.16 m, down to the ground behind the hedge (the race corridor's ground: fixed)
    u = S.HEDGE_U
    s, z, steps = p["stair_s"], zp, 0
    while s + p["tread"] < s1 - 0.3:
        gz = max(ST.ground(*hp(tm + k, s + p["tread"] * j)) for k in (-1.5, 0.0, 1.5) for j in (0.0, 0.5, 1.0))
        if z - p["riser"] < gz + 0.05:
            break
        z -= p["riser"]
        steps += 1
        g.obox(hp(tm, s + p["tread"] / 2), u, p["stair_half"], p["tread"] / 2, lo, z, "timber", skip=("bottom", "+u", "-u"))
        s += p["tread"]
    for side in (-1.0, 1.0):                                      # V18: white cheeks beside the 2 m stair, at forecourt level
        w = half(s1) - p["stair_half"]
        g.obox(hp(tm + side * (p["stair_half"] + w / 2), (p["stair_s"] + s1) / 2), u, w / 2, (s1 - p["stair_s"]) / 2, lo, zp,
               "palette", ("bottom",), "white")
    zl = max(ST.ground(*hp(tm + k, sx)) for k in (-1.5, 0.0, 1.5) for sx in (s, (s + s1) / 2, s1 + 0.3)) + 0.05
    zl = min(zl, z)
    g.obox(hp(tm, (s + s1 + 0.4) / 2), u, half(s1) + 0.1, (s1 + 0.4 - s) / 2, lo, zl, "timber", skip=("bottom", "+u", "-u"))
    g.obox(hp(tm, p["stair_s"] - 0.05), u, half(p["stair_s"]) - 0.04, 0.05, zp + 0.006, zp + 0.014, "palette", ("bottom",),
           "yellow")
    for side in (-1.0, 1.0):                                      # steel handrails
        t0 = lambda sx: tm + side * (p["stair_half"] - 0.06)      # noqa: E731  V18: on the 2 m stair's edges
        top, end = hp(t0(p["stair_s"] - 0.3), p["stair_s"] - 0.3), hp(t0(s + 0.3), s + 0.3)
        g.beam((*top, zp + 0.9), (*end, zl + 0.9), 0.05, 0.05, "palette", "steel_grey")
        g.post(*top, zp - 0.1, zp + 0.9, 0.05, "palette", "steel_grey")
        g.post(*end, zl - 0.1, zl + 0.9, 0.05, "palette", "steel_grey")
    # the doorway: dark glass between the walls' back ends, under the canopy's low edge
    a, b = hp(tm - p["back"] / 2, s1 + 0.38), hp(tm + p["back"] / 2, s1 + 0.38)
    g.quad((*a, zl - 0.1), (*b, zl - 0.1), (*b, zb + 0.1), (*a, zb + 0.1), "palette",
           (S.left_of(u)[0], S.left_of(u)[1], 0), "glass")
    return (f"entrance wedge at {hp(tm, (s0 + s1) / 2)[0]:.1f}, {hp(tm, (s0 + s1) / 2)[1]:.1f}: forecourt {zp:.2f} m, "
            f"{steps} risers down to {zl:.2f} m (terrace {S.TERRACE_Z} m: the corridor's ground lies between)")


def court(g):
    """Behind the hedge: the white slatted planter beside the door (its olive is in the dressing), a steel
    post-and-cable balustrade on the retaining wall, the blank sign wall on the hedge line."""
    hp = S.hedge_pt
    (t0, t1), (s0, s1) = CP.PLANTER["t"], CP.PLANTER["s"]
    c = hp((t0 + t1) / 2, (s0 + s1) / 2)
    zg = min(ST.ground(*hp(t, s)) for t in (t0, t1) for s in (s0, s1))
    ztop = max(ST.ground(*hp(t, s)) for t in (t0, t1) for s in (s0, s1)) + CP.PLANTER["height"]
    g.obox(c, S.HEDGE_U, (t1 - t0) / 2, (s1 - s0) / 2, zg - 0.3, ztop, "boards", skip=("bottom", "top"))
    g.obox(c, S.HEDGE_U, (t1 - t0) / 2 - 0.08, (s1 - s0) / 2 - 0.08, ztop - 0.12, ztop - 0.08, "palette", ("bottom",), "soil")
    up = S.left_of(S.HEDGE_U)
    line = [hp(t, S.WALL_S + 0.1) for t in (4.0, 17.2)]
    n = int(round(S.line_length(line) / 1.5))
    pts = []
    for k in range(n + 1):
        (x, y), _u = S.at_s(line, S.line_length(line) * k / n)
        z = ST.ground(x + up[0] * 0.9, y + up[1] * 0.9) + 0.25
        pts.append((x, y, z))
        g.post(x, y, z - 0.05, z + 1.0, 0.05, "palette", "steel_grey")
    for a, b in zip(pts, pts[1:]):
        for h in (0.35, 0.68, 1.0):
            g.beam((a[0], a[1], a[2] + h), (b[0], b[1], b[2] + h), 0.02, 0.02, "palette", "steel_grey", ends=False)
    w = CP.SIGN_WALL
    x, y = w["at"]
    _clear("the sign wall", [(x, y)])
    zw = ST.ground(x, y)
    g.obox((x, y), S.HEDGE_U, w["length"] / 2, 0.11, zw - 0.3, zw + w["height"], "wall_white")


# =====================================================================================================
# The sea wall below the restaurants' terrace
# =====================================================================================================
def sea_wall(g):
    line = CP.SEA_LINE
    total = CP.line_length(line)
    kink = CP.line_length(line[:2])
    n1, n2 = max(1, round(kink / 4.5)), max(1, round((total - kink) / 4.5))
    stops = [kink * k / n1 for k in range(n1)] + [kink + (total - kink) * k / n2 for k in range(n2 + 1)]
    zt = S.TERRACE_Z + CP.SEA_TOP
    zf = CP.SEA_FOOT_Z - 0.6
    dl, hl = CP.SEA_LEDGE
    rows = []
    for s in stops:
        back, top, base, ledge = (CP.side_pt(line, s, d) for d in (-CP.SEA_BACK, CP.SEA_FACE, CP.SEA_FACE + 0.3,
                                                                  CP.SEA_FACE + 0.3 + dl))
        zl = min(ST.ground(*ledge) + hl, zt - 0.4)
        rows.append((back, top, base, ledge, zl))
    _clear("the sea wall", [r[0] for r in rows])
    for (b0, t0, f0, l0, z0), (b1, t1, f1, l1, z1) in zip(rows, rows[1:]):
        out = (f0[1] - f1[1], f1[0] - f0[0], 0.0)
        out = out if (out[0] * (f0[0] - b0[0]) + out[1] * (f0[1] - b0[1])) > 0 else (-out[0], -out[1], 0.0)
        g.quad((*b0, zt), (*b1, zt), (*t1, zt), (*t0, zt), "concrete", (0, 0, 1))                 # the stone walk
        g.quad((*b0, zt - 1.0), (*b1, zt - 1.0), (*b1, zt), (*b0, zt), "palette", (-out[0], -out[1], 0), "kerb")
        g.quad((*t0, zt), (*t1, zt), (*t1, zt - 0.14), (*t0, zt - 0.14), "palette", out, "kerb")   # coping edge
        g.quad((*t0, zt - 0.14), (*t1, zt - 0.14), (*f1, zf), (*f0, zf), "rubble", out)            # battered face
        g.quad((*f0, z0), (*f1, z1), (*l1, z1), (*l0, z0), "palette", (0, 0, 1), "concrete")       # footing ledge
        g.quad((*l0, z0), (*l1, z1), (*l1, zf), (*l0, zf), "palette", out, "concrete")
    for (b, t, f, l, z), sign in ((rows[0], -1.0), (rows[-1], 1.0)):                               # end faces
        u = CP.at_s(line, 0.0 if sign < 0 else total)[1]
        out = (u[0] * sign, u[1] * sign, 0.0)
        g.quad((*b, zf), (*f, zf), (*t, zt), (*b, zt), "rubble", out)
        g.quad((*f, zf), (*l, zf), (*l, z), (*f, z), "palette", out, "concrete")
    return len(rows) - 1


# =====================================================================================================
# The south-west corner of the car park
# =====================================================================================================
def _box_along(g, line, s0, s1, d0, d1, z_top, z_face, top_colour, face_key, face_colour=None, step=4.0, parapet=None):
    """A retaining box along a polyline: from d0 to d1 metres to the right of it, top at z_top(x, y) (sampled at the
    line), the right-hand face down to z_face. parapet = (width, height, colour): a wall on the left edge."""
    n = max(1, int(math.ceil((s1 - s0) / step)))
    rows = []
    for k in range(n + 1):
        s = s0 + (s1 - s0) * k / n
        a, b = CP.side_pt(line, s, d0), CP.side_pt(line, s, d1)
        rows.append((a, b, z_top(*CP.side_pt(line, s, 0.0))))
    for (a0, b0, z0), (a1, b1, z1) in zip(rows, rows[1:]):
        out = (b0[0] - a0[0], b0[1] - a0[1], 0.0)
        g.quad((*a0, z0), (*a1, z1), (*b1, z1), (*b0, z0), "palette", (0, 0, 1), top_colour)
        g.quad((*b0, z_face), (*b1, z_face), (*b1, z1), (*b0, z0), face_key, out, face_colour)
        g.quad((*a0, z0 - 0.5), (*a1, z1 - 0.5), (*a1, z1), (*a0, z0), face_key, (-out[0], -out[1], 0), face_colour)
    for (a, b, z), sign in ((rows[0], -1.0), (rows[-1], 1.0)):
        u = CP.at_s(line, s0 if sign < 0 else s1)[1]
        g.quad((*a, z - 0.5), (*b, z_face), (*b, z), (*a, z), face_key, (u[0] * sign, u[1] * sign, 0), face_colour)
    if parapet:
        w, h, colour = parapet
        for (a0, _b0, z0), (a1, _b1, z1) in zip(rows, rows[1:]):
            u = S.unit(a1[0] - a0[0], a1[1] - a0[1])
            mid = ((a0[0] + a1[0]) / 2 + u[1] * w / 2, (a0[1] + a1[1]) / 2 - u[0] * w / 2)
            g.obox(mid, u, math.hypot(a1[0] - a0[0], a1[1] - a0[1]) / 2, w / 2, z0 - 0.05, z0 + h, "palette",
                   colour=colour, z0b=z1 - 0.05, z1b=z1 + h)
    return rows


def corner(g, rng):
    cp = CP.corner_pt
    q, d = CP.CORNER_Q, CP.CORNER_D
    zc = surface(*cp(3.5, -0.4))                          # the car park at the stair's top
    ze = CP.ESPL_Z
    front = CP.STEP_D + CP.BOX_D
    (q0, q1), st = CP.STAIR["q"], CP.STAIR
    stone, white = "bollard_pale", "render_white"
    edge = [cp(-0.3, -0.15), cp(7.2, -0.15)]              # the car park's south edge (boxes hang on its right = south)
    _clear("the car-park corner", edge)

    def top_at(x, y):
        return surface(x - d[0] * 0.5, y - d[1] * 0.5) + 0.04

    # stone-clad parapet with planted retaining boxes either side of the stair
    for s0, s1 in ((0.0, q0 + 0.3 - 0.45), (q1 + 0.3 + 0.45, 7.5)):
        _box_along(g, edge, s0, s1, 0.0, front + 0.15, top_at, ze - 0.4, "bank_green", "palette", white,
                   parapet=(0.45, 0.9, stone))
    # the stair: a landing, then the flight; stone-clad cheeks whose tops fall with it
    drop = zc - ze
    rise = drop / st["risers"]
    mid_q = (q0 + q1) / 2
    g.obox(cp(mid_q, st["top"] / 2 - 0.1), q, (q1 - q0) / 2, st["top"] / 2 + 0.1, ze - 0.4, zc + 0.02, "palette",
           ("bottom", "+u", "-u"), "concrete")
    for k in range(st["risers"] - 1):
        z = zc - rise * (k + 1)
        g.obox(cp(mid_q, st["top"] + st["tread"] * (k + 0.5)), q, (q1 - q0) / 2, st["tread"] / 2, ze - 0.4, z, "palette",
               ("bottom", "+u", "-u"), "concrete")
    foot = st["top"] + st["tread"] * (st["risers"] - 1)
    for qa in (q0 - 0.225, q1 + 0.225):
        g.obox(cp(qa, st["top"] / 2 - 0.075), q, 0.225, st["top"] / 2 + 0.075, ze - 0.4, zc + 0.9, "palette", colour=stone)
        a, b = cp(qa, st["top"]), cp(qa, foot + 0.3)
        c = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        g.obox(c, d, (foot + 0.3 - st["top"]) / 2, 0.225, ze - 0.4, zc + 0.9, "palette", colour=stone, z1b=ze + 0.9)
    # the mast's stone plinth on the east box, at the corner
    pl = CP.PLINTH
    g.obox(cp(pl["q"], pl["d"]), q, pl["size"] / 2, pl["size"] / 2, zc - 0.1, zc + pl["height"], "palette", colour=stone)
    # the parapet runs on east along the ends of the south bay row
    sa, su = (-241.0, -72.6), S.unit(39.0, -8.9)
    east = [cp(7.5, 0.08), S.pt(sa, su, 1.5, -5.32), S.pt(sa, su, 21.0, -5.32)]
    g.wall(east, 0.45, lambda x, y: ST.ground(x, y) - 0.4, lambda x, y: surface(x - d[0] * 0.6, y - d[1] * 0.6) + 0.9,
           "palette", colour=stone, step=5.0)
    _clear("the car-park parapet", east)
    # retaining boxes with a white face along the esplanade's north side (west of the stair) and its east end
    n_len = CP.line_length(CP.ESPL_N[:2])
    _box_along(g, CP.ESPL_N[:2], 1.5, n_len - 0.3, -0.3, CP.BOX_D,
               lambda x, y: ST.ground(x + d[0] * -1.2, y + d[1] * -1.2) + 0.12, ze - 0.4, "bank_green", "wall_white")
    e_line = [cp(6.9, CP.STEP_D + 9.0 + 0.4), cp(6.9, front - 0.1)]            # south to north: the box is on its left
    _box_along(g, e_line[::-1], 0.0, CP.line_length(e_line), -0.3, CP.BOX_D,
               lambda x, y: ST.ground(x + q[0] * 1.3, y + q[1] * 1.3) + 0.12, ze - 0.4, "bank_green", "wall_white")
    # rubble parapet with a terracotta brick cap on the sea side, 1 m inside the cliff line
    total = CP.line_length(CP.ESPL_S)
    run = (CP.CLIFF_RUN[0], total - CP.BOX_D + 0.1)
    par = [CP.side_pt(CP.ESPL_S, s, -CP.PARAPET_IN) for s in run]
    zpar = lambda x, y: max(ze, ST.ground(x, y)) + 0.55         # noqa: E731
    g.wall(par, 0.4, lambda x, y: ze - 0.3, zpar, "rubble", step=4.5)
    g.wall(par, 0.5, zpar, lambda x, y: zpar(x, y) + 0.07, "palette", colour="terracotta", step=4.5)
    # the cliff: honey-grey limestone under the parapet, three ragged bands down to the lowered sand
    n = int(round((total - run[0]) / 2.0))
    rows = []
    for k in range(n + 1):
        s = run[0] + (total - run[0]) * k / n
        j = [rng.uniform(-0.25, 0.25) for _ in range(6)]
        w = CP.smooth(min(s - run[0], total - s) / 2.5)
        rows.append([(*CP.side_pt(CP.ESPL_S, s, -CP.PARAPET_IN + 0.15), ze + 0.04),
                     (*CP.side_pt(CP.ESPL_S, s, 0.25 + 0.5 * w + j[0]), ze - 0.12 + j[1] * 0.4),
                     (*CP.side_pt(CP.ESPL_S, s, 0.9 + 0.5 * w + j[2]), ze - 0.55 + j[3]),
                     (*CP.side_pt(CP.ESPL_S, s, 1.9 + 0.8 * w + j[4]), CP.CLIFF_Z - 0.45)])
    for r0, r1 in zip(rows, rows[1:]):
        for b in range(3):
            g.face([r0[b], r1[b], r1[b + 1]], "rock", (0.2, -1, 0.6))
            g.face([r0[b], r1[b + 1], r0[b + 1]], "rock", (0.2, -1, 0.6))
    for row, sign in ((rows[0], -1.0), (rows[-1], 1.0)):
        g.face([row[0], row[1], row[2], row[3], (row[0][0], row[0][1], CP.CLIFF_Z - 0.45)], "rock", (q[0] * sign, q[1] * sign, 0))
    # loose blocks at the cliff's foot and low jagged outcrops in the sand in front of it
    rocks = 0
    for k in range(9):
        s = run[0] + 1.5 + (total - run[0] - 3.0) * (k + rng.uniform(0.1, 0.9)) / 9
        x, y = CP.side_pt(CP.ESPL_S, s, rng.uniform(2.6, 3.8))
        a = rng.uniform(0, math.pi)
        size = rng.uniform(0.3, 0.75)
        zg = ST.ground(x, y)
        g.obox((x, y), (math.cos(a), math.sin(a)), size, size * rng.uniform(0.6, 0.9), zg - 0.3, zg + size * rng.uniform(0.6, 1.1),
               "rock", z1b=zg + size * rng.uniform(0.4, 0.9))
        rocks += 1
    for k in range(7):
        s = run[0] + (total - run[0]) * (k + rng.uniform(0.2, 0.8)) / 7
        cx, cy = CP.side_pt(CP.ESPL_S, s, rng.uniform(5.5, 13.0))
        r, h = rng.uniform(1.2, 2.4), rng.uniform(0.35, 0.95)
        m = 6
        ph = rng.uniform(0, 1)
        base = [(cx + r * rng.uniform(0.75, 1.2) * math.cos(2 * math.pi * (i + ph) / m),
                 cy + r * rng.uniform(0.75, 1.2) * math.sin(2 * math.pi * (i + ph) / m)) for i in range(m)]
        zg = min(ST.ground(x, y) for x, y in base) - 0.25
        zt = ST.ground(cx, cy)
        top = [(cx + (x - cx) * 0.55, cy + (y - cy) * 0.55, zt + h * rng.uniform(0.55, 1.0)) for x, y in base]
        for i in range(m):
            i1 = (i + 1) % m
            g.face([(*base[i], zg), (*base[i1], zg), top[i1], top[i]], "rock", (base[i][0] - cx, base[i][1] - cy, 0.3))
        g.face(top[:3], "rock", (0, 0, 1))
        g.face([top[0], top[2], top[3], top[5]], "rock", (0, 0, 1))
        g.face(top[3:6], "rock", (0, 0, 1))
        rocks += 1
    # the beach shower: a steel post with a head on the dark grating (paint)
    x, y = CP.SHOWER
    zs = surface(x, y)
    g.post(x, y, zs - 0.1, zs + 2.1, 0.07, "palette", "steel_grey")
    g.obox((x + d[0] * 0.18, y + d[1] * 0.18), d, 0.2, 0.05, zs + 2.02, zs + 2.1, "palette", (), "steel_grey")
    return zc, drop, rise, rocks


def west_stair(g):
    """The beach stair of the west car park: steps between two terracotta-red rendered walls."""
    w = CP.WEST_STAIR
    u = w["heading"]
    n = S.left_of(u)
    for side in (-1.0, 1.0):
        line = [S.pt(w["at"], u, t, side * (w["width"] / 2 + 0.15)) for t in (-0.5, w["length"] / 2, w["length"])]
        g.wall(line, 0.3, lambda x, y: ST.ground(x, y) - 0.5, lambda x, y: ST.ground(x, y) + w["wall"], "palette",
               colour="terracotta", step=2.0)
    z0, z1 = ST.ground(*w["at"]), ST.ground(*S.pt(w["at"], u, w["length"]))
    steps = max(1, min(12, int(round((z0 - z1) / 0.16))))
    for k in range(steps):
        c = S.pt(w["at"], u, w["length"] * (k + 0.5) / steps)
        g.obox(c, u, w["length"] / steps / 2, w["width"] / 2, z1 - 0.5, z0 - (z0 - z1) * (k + 1) / steps + 0.02, "palette",
               ("bottom", "+v", "-v"), "concrete")
    _ = n
    return z0, z1, steps


# =====================================================================================================
# Prototype, static build, dressing
# =====================================================================================================
def screen_box(name, col):
    """A blank dark screen on a single thick post: panel 3 x 2 m, top 4 m. Faces +-y. Nothing is shown on it."""
    g = Proto()
    g.box(-0.16, 0.16, -0.12, 0.12, -0.15, 2.0, "totem_green")
    g.box(-1.5, 1.5, -0.14, 0.14, 2.0, 4.0, "totem_green", skip=())
    for side in (1, -1):
        g.face([(-1.38, side * 0.145, 2.12), (1.38, side * 0.145, 2.12), (1.38, side * 0.145, 3.88), (-1.38, side * 0.145, 3.88)],
               "palette", (0, side, 0), "black")
    return g.build(name, col)


def build(col):
    rng = random.Random(SEED)
    g = CSolid()
    lines = [portal(g)]
    court(g)
    segs = sea_wall(g)
    zc, drop, rise, rocks = corner(g, rng)
    z0, z1, steps = west_stair(g)
    E.new_object("Street_Capricciosa", g.mesh("Street_Capricciosa"), col)
    tris = sum(len(f[0]) - 2 for f in g.faces)
    print(f"  capricciosa: {lines[0]}")
    print(f"  capricciosa: sea wall {CP.line_length(CP.SEA_LINE):.0f} m in {segs} pieces, top {S.TERRACE_Z + CP.SEA_TOP:.2f} m, "
          f"sand at its foot {CP.SEA_FOOT_Z} m")
    print(f"  capricciosa: car-park corner {zc:.2f} m, esplanade {CP.ESPL_Z} m: {CP.STAIR['risers']} risers of {rise:.3f} m; "
          f"cliff foot {CP.CLIFF_Z} m, {rocks} rocks; west beach stair {z0:.2f} -> {z1:.2f} m, {steps} steps")
    print(f"  capricciosa: structures {tris} triangles; nearest to the race centreline: {MIN_CLEAR[1]}, {MIN_CLEAR[0]:.1f} m")


def _zones():
    cp = CP.corner_pt
    return [CP.SEA_FOOT, CP.CLIFF_FOOT,
            [cp(-0.6, -0.4), cp(7.6, -0.4), cp(7.6, CP.STEP_D + 9.6), (-265.0, -91.6), (-264.6, -83.4), cp(-0.6, 0.4)],
            [CP.side_pt(CP.SEA_LINE, s, dd) for s, dd in ((0.0, -CP.SEA_BACK - 0.3), (CP.line_length(CP.SEA_LINE[:2]), -CP.SEA_BACK - 0.3),
                                                           (CP.line_length(CP.SEA_LINE), -CP.SEA_BACK - 0.3),
                                                           (CP.line_length(CP.SEA_LINE), 0.2), (0.0, 0.2))]]


def dress(veg, props, protos):
    """Builds the statics, then edits the dressing lists (after dressing_freeze): removes what stands in the reshaped
    places, adds this area's own plants and props. Rewrites the two placement files. Everything else stays as it was."""
    col = E.collection(ST.COLLECTION)
    build(col)
    protos["Prop_ScreenBox"] = screen_box("Prop_ScreenBox", E.collection("Proto_Props"))
    rng = random.Random(SEED + 1)
    zones = _zones()
    cp = CP.corner_pt

    def gone(p):
        x, y = p["x"], p["y"]
        if any(CP.inside(x, y, z) for z in zones):
            return True
        if p["proto"] == "Prop_LampMast" and math.hypot(x + 243.0, y + 77.5) < 1.0:       # the mast moves onto its plinth
            return True
        if p["proto"].startswith("Prop_Car") and CP.inside(x, y, [cp(1.0, -6.5), cp(7.9, -6.5), cp(7.9, 0.5), cp(1.0, 0.5)]):
            return True                                                                    # the stair's and the plinth's bays
        if math.hypot(x - CP.BED_C[0], y - CP.BED_C[1]) < CP.ISLAND_R and p["proto"].startswith(("Veg_", "Prop_Sign")):
            return True
        return p["proto"].startswith("Veg_Shrub") and (math.hypot(x - CP.DATE_PALM[0], y - CP.DATE_PALM[1]) < 2.0
                                                       or math.hypot(x - CP.SIGN_WALL["at"][0], y - CP.SIGN_WALL["at"][1]) < 1.8)
    removed = [p for p in list(veg) + list(props) if gone(p)]
    ids = {id(p) for p in removed}
    new_v, new_p, skipped = [], [], []

    def add(lst, proto, x, y, rot=0.0, scale=1.0, clear=S.PROP_CLEAR, z=None):
        if S.route_distance(x, y) < clear:
            skipped.append(proto)
            return False
        lst.append(_entry(proto, x, y, rot, scale, z))
        return True

    # the island inside the hairpin (V18, team walk 7 Nov): plain calcada, a stop sign at its west tip; the bike dock, the bins
    add(new_p, "Prop_SignStop", *CP.STOP_SIGN, facing_rot(S.bearing(270.0)))
    (ax, ay), (ex, ey) = CP.BIKE_DOCK
    du = S.unit(ex - ax, ey - ay)
    length = math.hypot(ex - ax, ey - ay)
    k = 0
    while 1.35 + k * 2.75 <= length:
        add(new_p, "Prop_BikeRack", ax + du[0] * (1.35 + k * 2.75), ay + du[1] * (1.35 + k * 2.75), heading_rot(du))
        k += 1
    (ax, ay), (ex, ey) = CP.BIN_PAD
    add(new_p, "Prop_BinsUnderground", (ax + ex) / 2, (ay + ey) / 2, heading_rot(S.unit(ex - ax, ey - ay)))
    add(new_p, "Prop_ScreenBox", *CP.SCREEN_AT, facing_rot(S.bearing(20.0)))
    add(new_p, "Prop_SignRoundTriangle", *CP.SIGN_AT, facing_rot(S.bearing(110.0)))
    # forecourt: the broad date-type palm, the olive in the court's planter
    add(new_v, "Veg_Palm", *CP.DATE_PALM, 115.0, 0.55, clear=S.PROP_CLEAR + 4.0, z=ST.ground(*CP.DATE_PALM) - 0.05)
    (t0, t1), (s0, s1) = CP.PLANTER["t"], CP.PLANTER["s"]
    ox, oy = S.hedge_pt((t0 + t1) / 2, (s0 + s1) / 2)
    add(new_v, "Veg_Olive", ox, oy, 60.0, 0.5, z=ST.ground(ox, oy) + CP.PLANTER["height"] - 0.15)
    # the row of tall palms on the sea wall
    import env_beachfront
    lay = env_beachfront.restaurant_layout("w213238769")
    pr = CP.PALM_ROW
    for k in range(pr["n"]):
        x, y = env_beachfront.at(lay, pr["s0"] + k * pr["step"], 0.0)
        s, _d = CP.along(CP.SEA_LINE, x, y)
        x, y = CP.side_pt(CP.SEA_LINE, s, -pr["back"])
        add(new_v, "Veg_SlimPalm", x, y, rng.uniform(0, 360), rng.uniform(0.98, 1.12), z=S.TERRACE_Z - 0.1)
    # the car-park corner: the mast on its plinth, a pay machine (blank) beside it
    pl = CP.PLINTH
    add(new_p, "Prop_LampMast", *cp(pl["q"], pl["d"]), z=surface(*cp(3.5, -0.4)) + pl["height"] - 0.5)
    add(new_p, "Prop_PayMachine", *cp(7.9, -0.75), facing_rot((-CP.CORNER_D[0], -CP.CORNER_D[1])))
    # the west car park: fan palms 7-9 m along its north edge
    for k, (x, y) in enumerate(CP.WEST_PALMS):
        add(new_v, "Veg_FanPalm" if k % 2 else "Veg_FanPalm_B", x, y, rng.uniform(0, 360), rng.uniform(0.7, 0.82),
            z=ST.ground(x, y) - 0.1)
    veg2 = [p for p in veg if id(p) not in ids] + new_v
    props2 = [p for p in props if id(p) not in ids] + new_p
    for fname, items in (("placements_vegetation.json", veg2), ("placements_props.json", props2)):
        path = os.path.join(E.OUT, fname)
        data = json.load(open(path))
        data["placements"] = items
        counts = {}
        for it in items:
            counts[it["proto"]] = counts.get(it["proto"], 0) + 1
        data["counts"] = counts
        json.dump(data, open(path, "w"), indent=1)
    near = min((S.route_distance(it["x"], it["y"]), it["proto"]) for it in new_v + new_p)
    print("  capricciosa: dressing: removed " + (", ".join(sorted(f"{p['proto']}@{p['x']:.0f},{p['y']:.0f}" for p in removed)) or "nothing"))
    print(f"  capricciosa: dressing: {len(new_v)} plants, {len(new_p)} props added; nearest to the centreline {near[1]} "
          f"{near[0]:.1f} m" + (f"; left out, too close: {skipped}" if skipped else ""))
    return veg2, props2
