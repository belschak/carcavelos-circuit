"""Entrance plaza = the start plaza (V15a, 2026-11-11), built from entrance_plan.py (survey V15a: DGT orthophoto,
2026 satellite tile, Street View Jul 2020).

Objects (collection World_Entrance, exported with CampusWorld.fbx):
  Entrance_Paving   calcada of small pale cubes on the whole plaza, draped over the built terrain exactly like
                    env_streets' paving (its Soup tools: 7 cm over the ground, down to 4 mm at the foot of the road's
                    shoulder; nothing within 4.95 m of the centreline: the race road keeps its own surface), with the
                    thin dark-red drain channels cut into the same mesh (no second layer, nothing coplanar)
  Entrance_Ramp     the underground car-park ramp at the avenue: sett floor going down east to a black portal, pale
                    concrete side walls that grow out of the plaza, a low parapet with a steel handrail, the lintel
                    with a dark band (no text), the portal void as a black box
  World_GroundEntrance (collection World_Ground)   the 5 m terrain cells the ramp touches, rebuilt as an apron that
                    meets the ramp's outline (like env_campus' trays); E.ground_z returns the ramp floor inside it
Dressing (dress(), after dressing_freeze.apply and the other dress() hooks, as an explicit edit of the lists):
  every olive inside the plaza ring (+3 m) becomes the young staked tree Veg_PlazaTree / _B (env_vegetation), the
  generic campus lamps inside the ring become Prop_LampPlaza; added: the white seat blocks Prop_SeatBlockPlaza, six
  slim lamps in the tree rows, one parking sign at the ramp mouth. Everything else stays as frozen.
Nothing that stands up is closer than ROUTE_CLEAR (6.6 m) to the race centreline; report() prints the minimum per kind.

V16 visual pass (2026-11-11, photosphere survey V15, area N): drain channels 0.22 m rust-brown at 14.5 m, bearing 165;
the plank band along wing B with a flush straight edge, gravel pits under the young trees and the tan path to
Milestone's entrance as further layers of the same draped sheet (the paving is now built from dress(), when the tree
positions are known); slim lamp columns of 5.5 m with the lantern in the top; 16 seat blocks of 3.0 x 0.6 m; slim steel
bollards on the apron line in front of the west doors; the ramp with solid 1.1 m concrete parapets (no handrail) and a
dark fascia under a white slab edge over the portal; the paving sheet follows the ground more closely between the
ramp and the loop (drape(soften=...)). drape() is shared with env_north.py (drop-off forecourt).
"""
import json
import math
import os

import bpy
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt

import entrance_plan as P
import env_common as E

COLLECTION = "World_Entrance"
ROUTE_CLEAR = 6.6            # m from the race centreline: nothing of ours stands closer (road half-width 4 m)
PLAZA_KEEP_OUT = 4.02        # V18h: the plaza sheet reaches the road's edge (4.0 m); street_plan.KEEP_OUT is 4.95
SINK_UNDER_CORRIDOR = 0.35   # as build_world.build_ground
ALONG, ACROSS = 12, 6        # stations of the ramp: along each wall, across the floor
WALL_TILE = 2.0              # m per tile of the board-marked concrete on the ramp's walls (V18h)
_CLEAR = {}                  # kind -> (least distance to the centreline, where)


def route_distance(x, y):
    import street_plan
    return street_plan.route_distance(x, y)


def _note(kind, x, y, reach=0.0):
    d = route_distance(x, y) - reach
    if kind not in _CLEAR or d < _CLEAR[kind][0]:
        _CLEAR[kind] = (d, (round(x, 1), round(y, 1)))
    return d


# =====================================================================================================
# Small mesh builder: flat-shaded faces, one material per name, UVs optional
# =====================================================================================================
class Faces:
    def __init__(self):
        self.v, self.f, self.m, self.uv, self.mats = [], [], [], [], []

    def face(self, pts, mat, towards=None, uvs=None):
        """Face from 3D points; turned so that its normal points along `towards` (a direction) if given."""
        pts = [tuple(p) for p in pts]
        if towards is not None:
            n = Vector((0, 0, 0))
            for a, b in zip(pts, pts[1:] + pts[:1]):
                n += Vector(a).cross(Vector(b))
            if n.dot(Vector(towards)) < 0:
                pts = pts[::-1]
                uvs = uvs[::-1] if uvs else None
        if mat not in self.mats:
            self.mats.append(mat)
        base = len(self.v)
        self.v += pts
        self.f.append(list(range(base, base + len(pts))))
        self.m.append(self.mats.index(mat))
        self.uv.append(uvs or [(0.0, 0.0)] * len(pts))

    def box(self, c, u, hu, hv, z0, z1, mat, top=True, bottom=False):
        """Box centred at c (2D), half-size hu along the unit vector u and hv across it."""
        v = (-u[1], u[0])
        ring = [(c[0] + u[0] * a + v[0] * b, c[1] + u[1] * a + v[1] * b) for a, b in ((-hu, -hv), (hu, -hv), (hu, hv), (-hu, hv))]
        for a, b in zip(ring, ring[1:] + ring[:1]):
            out = (b[1] - a[1], a[0] - b[0], 0.0)
            self.face([(*a, z0), (*b, z0), (*b, z1), (*a, z1)], mat, out)
        if top:
            self.face([(*p, z1) for p in ring], mat, (0, 0, 1))
        if bottom:
            self.face([(*p, z0) for p in ring], mat, (0, 0, -1))

    def beam(self, a, b, half, mat):
        """Square bar between two 3D points (four long faces, open ends)."""
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized()
        side = d.cross(Vector((0, 0, 1)))
        side = side.normalized() if side.length > 1e-6 else Vector((1, 0, 0))
        up = side.cross(d).normalized()
        for s, t in ((side, up), (up, -side), (-side, -up), (-up, side)):
            self.face([a + (s - t) * half, b + (s - t) * half, b + (s + t) * half, a + (s + t) * half], mat, s)

    def build(self, name, col, materials):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.v, [], self.f)
        for key in self.mats:
            mesh.materials.append(materials[key])
        mesh.polygons.foreach_set("material_index", self.m)
        layer = mesh.uv_layers.new(name="UVMap")
        for poly, uvs in zip(mesh.polygons, self.uv):
            for li, t in zip(poly.loop_indices, uvs):
                layer.data[li].uv = t
        mesh.validate()
        mesh.update()
        return E.new_object(name, mesh, col)


def materials():
    import env_streets
    return {
        "calcada": env_streets._textured("entrance_calcada", P.CALCADA, "entrance_calcada", 0.9),
        "drain": env_streets._textured("entrance_drain", P.DRAIN, "entrance_drain", 0.75),
        "planks": env_streets._textured("entrance_planks", P.PLANK, "entrance_planks", 0.9),      # V16
        "palette": env_streets.mat("palette"),                    # V16: flat colours from the street palette atlas
        "white": E.material("B_wall_white", "#EAEAEC", 0.85),     # V16: slab edge over the ramp portal
        "concrete": env_streets._textured("entrance_concrete", P.CONCRETE, "coast_boardmarked", 0.9),   # V18h: board-marked
        "edging": env_streets._textured("entrance_edging", P.EDGING, "entrance_edging", 0.85),   # V18h: the fan's rim stones
        "setts": env_streets.mat("setts"),                         # V18h: the dark ring round the loop's centre
        # V18j (env_north: the street behind the hall): buff apron concrete, galvanised gratings and louvres, the
        # steel strip at the lawn, R. da Holanda's asphalt, the terrain's own material for the sheet's hems
        "buff": env_streets._textured("north_buff", "#B3AB95", "north_buff", 0.9),
        "grating": env_streets._textured("north_grating", "#8C908C", "north_grating", 0.55),
        "basalt": env_streets._textured("north_basalt", "#64615D", "north_basalt", 0.8),    # V20d: the ring's basalt band
        "rust": env_streets.mat("rust"),
        "asphalt": env_streets.mat("asphalt"),
        "ground_map": env_streets.mat("ground_map"),
        "void": E.material("entrance_void", "#050505", 1.0),
        "seat": E.material("entrance_seat", P.SEAT_WHITE, 0.8),
        "dark": E.material("entrance_dark", P.LAMP_DARK, roughness=0.5, metallic=0.3),
        "light": E.material("entrance_lamp_light", "#F4EFD8", roughness=0.4, emission="#FFE9B0"),
        "ground": E.material("ground_map", "#A6AA82", roughness=0.95),
    }


def calcada_uv(x, y):
    ux, uy = P.ROW_DIR
    return ((x * ux + y * uy) / P.CALCADA_TILE, (-x * uy + y * ux) / P.CALCADA_TILE)


# =====================================================================================================
# The ramp: terrain cut (plan_ground, build_ground_mesh) and its walls (build_ramp)
# =====================================================================================================
_G = {"cells": set(), "tris": [], "grid": {}, "ramp": None}


def _lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(len(a)))


def _cell(x, y):
    return int(math.floor((x - E.X0) / E.STEP)), int(math.floor((y - E.Y0) / E.STEP))


def covers_cell(cx, cy):
    """True if the 5 m terrain face centred at (cx, cy) is replaced by the ramp's apron (build_world.build_ground)."""
    return _cell(cx, cy) in _G["cells"]


def _ramp():
    """The ramp's stations and heights (once): the floor is a ruled surface from the natural ground along the mouth
    down to one level at the portal."""
    if _G["ramp"] is None:
        sw, se, ne, nw = P.ramp_inner()
        zg = E.coarse_ground_z
        mouth = [_lerp(sw, nw, k / ACROSS) for k in range(ACROSS + 1)]
        portal = [_lerp(se, ne, k / ACROSS) for k in range(ACROSS + 1)]
        z_mouth = [zg(*p) for p in mouth]
        beside = [zg(*p) for p in (se, ne, _lerp(se, ne, 0.5))]
        z_portal = sum(beside) / 3 - P.RAMP_DROP
        ax = P.ramp_axis()
        mid_m, mid_p = _lerp(sw, nw, 0.5), _lerp(se, ne, 0.5)
        _G["ramp"] = dict(sw=sw, se=se, ne=ne, nw=nw, mouth=mouth, portal=portal, z_mouth=z_mouth, z_portal=z_portal,
                          ax=ax, mid_m=mid_m, length=math.dist(mid_m, mid_p), inner=P.ramp_inner(), outer=P.ramp_outer())
    return _G["ramp"]


def _floor_z(t, s):
    """Floor height at t (0 mouth .. 1 portal) and s (0 south edge .. 1 north edge)."""
    r = _ramp()
    f = min(max(s, 0.0), 1.0) * ACROSS
    k = min(int(f), ACROSS - 1)
    zm = r["z_mouth"][k] + (r["z_mouth"][k + 1] - r["z_mouth"][k]) * (f - k)
    return zm + (r["z_portal"] - zm) * min(max(t, 0.0), 1.0)


def _floor_at(x, y):
    r = _ramp()
    t = ((x - r["mid_m"][0]) * r["ax"][0] + (y - r["mid_m"][1]) * r["ax"][1]) / r["length"]
    ds = P.seg_distance(x, y, *r["sw"], *r["se"])
    dn = P.seg_distance(x, y, *r["nw"], *r["ne"])
    return _floor_z(t, ds / max(ds + dn, 1e-6))


def _outline():
    """The outline the apron meets, counter-clockwise: south wall outside, portal wall outside, north wall outside,
    then back along the open mouth through the floor's own mouth points (shared vertices: no crack at the mouth)."""
    r = _ramp()
    o_sw, o_se, o_ne, o_nw = r["outer"]
    ring = [_lerp(o_sw, o_se, k / ALONG) for k in range(ALONG)]
    ring += [_lerp(o_se, o_ne, k / 4) for k in range(4)]
    ring += [_lerp(o_ne, o_nw, k / ALONG) for k in range(ALONG)]
    ring += [o_nw] + r["mouth"][::-1]
    return ring


def _touches_cell(ring, x0, y0, step, pad):
    ax, ay, bx, by = x0 - pad, y0 - pad, x0 + step + pad, y0 + step + pad
    sq = [(ax, ay), (bx, ay), (bx, by), (ax, by)]
    if any(P.inside(x, y, ring) for x, y in sq) or any(ax <= x <= bx and ay <= y <= by for x, y in ring):
        return True

    def side(u, v, w):
        return (v[0] - u[0]) * (w[1] - u[1]) - (v[1] - u[1]) * (w[0] - u[0])
    for p, q in zip(ring, ring[1:] + ring[:1]):
        for a, b in zip(sq, sq[1:] + sq[:1]):
            if (side(a, b, p) > 0) != (side(a, b, q) > 0) and (side(p, q, a) > 0) != (side(p, q, b) > 0):
                return True
    return False


def _node_z(x, y):
    """Height of a 5 m terrain node exactly as build_world.build_ground builds it."""
    import env_corridor
    sink = SINK_UNDER_CORRIDOR * env_corridor.smoothstep((env_corridor.inside_depth(x, y) - 0.5) / 2.0)
    return E.coarse_ground_z(x, y) - sink


def plan_ground():
    """Chooses the 5 m cells the ramp replaces and makes E.ground_z return the ramp floor (call after
    env_campus.plan_ground). A ramp that touches the track corridor stops the build."""
    import env_corridor
    import env_terrain
    ring = _outline()
    xs, ys = [p[0] for p in ring], [p[1] for p in ring]
    cells = set()
    for i in range(int((min(xs) - E.X0) // E.STEP) - 1, int((max(xs) - E.X0) // E.STEP) + 2):
        for j in range(int((min(ys) - E.Y0) // E.STEP) - 1, int((max(ys) - E.Y0) // E.STEP) + 2):
            x0, y0 = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            if not _touches_cell(ring, x0, y0, E.STEP, 0.15):
                continue
            cx, cy = x0 + E.STEP / 2, y0 + E.STEP / 2
            # V15b: since the start loop comes within 7.4 m of the ramp, a cell at the ramp's east end may count as
            # covered by the corridor (its centre and corners are); it is an apron cell all the same, sunk under the
            # corridor like the cells of the outer band. The corridor itself is held back from the ramp
            # (env_corridor._hold_back) and the check on the outline below still stops the build if it is not.
            if env_terrain.in_portal_hole(cx, cy):
                raise RuntimeError(f"entrance: the car-park ramp touches the track corridor at cell {i},{j} ({x0}, {y0})")
            cells.add((i, j))
    # The corridor's outermost band (22-26 m from the centreline) lies over some of these cells; the cells stay 5 m
    # ground under it as before (same node heights, sunk under the corridor). The ramp itself must stay outside it.
    near = [p for p in ring if env_corridor.has_face(*p) or any(env_corridor.has_face(p[0] + dx, p[1] + dy)
                                                                for dx, dy in ((0.8, 0), (-0.8, 0), (0, 0.8), (0, -0.8)))]
    if near:
        raise RuntimeError(f"entrance: the car-park ramp reaches into the track corridor at {near[0]}")
    rim = sum(1 for i, j in cells if any(env_corridor.has_face(E.X0 + (i + a / 4) * E.STEP, E.Y0 + (j + c / 4) * E.STEP)
                                         for a in range(5) for c in range(5)))
    _G["cells"] = cells
    _G["tris"] = _apron_triangles(ring)
    grid = {}
    for tri in _G["tris"]:
        xs, ys = [p[0] for p in tri], [p[1] for p in tri]
        for i in range(_cell(min(xs), 0)[0], _cell(max(xs), 0)[0] + 1):
            for j in range(_cell(0, min(ys))[1], _cell(0, max(ys))[1] + 1):
                grid.setdefault((i, j), []).append(tri)
    _G["grid"] = grid
    prev = E._surface["fn"] or E.coarse_ground_z
    inner = _ramp()["inner"]

    def surface(x, y):
        key = _cell(x, y)
        if key not in _G["cells"] or env_corridor.has_face(x, y):
            return prev(x, y)
        if P.inside(x, y, inner):
            return _floor_at(x, y)
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
    r = _ramp()
    print(f"  entrance: car-park ramp replaces {len(cells)} cells of the 5 m grid ({len(_G['tris'])} apron triangles; "
          f"{rim} cells lie partly under the corridor's outer band, which stays as it is); "
          f"floor {sum(r['z_mouth']) / len(r['z_mouth']):.2f} m at the mouth -> {r['z_portal']:.2f} m at the portal, "
          f"{100 * (sum(r['z_mouth']) / len(r['z_mouth']) - r['z_portal']) / r['length']:.1f} % over {r['length']:.1f} m")


def _apron_triangles(ring):
    cells = _G["cells"]
    node = lambda i, j: (E.X0 + i * E.STEP, E.Y0 + j * E.STEP)                  # noqa: E731
    key = lambda p: (round(p[0], 3), round(p[1], 3))                            # noqa: E731
    pts, index, heights, edges = [], {}, {}, set()

    def add(p, z):
        k = key(p)
        if k not in index:
            index[k] = len(pts)
            pts.append(p)
            heights[k] = z
        return index[k]
    for i, j in cells:
        ids = [add(node(a, c), _node_z(*node(a, c))) for a, c in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))]
        for k, (di, dj) in enumerate(((0, -1), (1, 0), (0, 1), (-1, 0))):       # edge to a cell that stays: fixed
            if (i + di, j + dj) not in cells:
                edges.add(tuple(sorted((ids[k], ids[(k + 1) % 4]))))
    ids = [add(p, E.coarse_ground_z(*p)) for p in ring]
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
        if _cell(cx, cy) not in cells or P.inside(cx, cy, ring):
            continue
        out.append(tuple((p[0], p[1], heights.get(key(p), E.coarse_ground_z(p[0], p[1]))) for p in (a, b, c)))
    return out


def build_ground_mesh():
    """The apron around the ramp (ground-map material, collection World_Ground: env_streets' ground rays see it)."""
    if not _G["tris"]:
        return
    m = Faces()
    w, h = E.X1 - E.X0, E.Y1 - E.Y0
    for tri in _G["tris"]:
        m.face(list(tri), "ground", (0, 0, 1), [((p[0] - E.X0) / w, (p[1] - E.Y0) / h) for p in tri])
    # the up-turn of face() reverses uvs with the points, so they stay matched
    obj = m.build("World_GroundEntrance", E.collection("World_Ground"), materials())
    print(f"  entrance ground: {len(obj.data.polygons)} triangles")


def build_ramp(col):
    r = _ramp()
    m = Faces()
    sw, se, ne, nw, ax = r["sw"], r["se"], r["ne"], r["nw"], r["ax"]
    o_sw, o_se, o_ne, o_nw = r["outer"]
    zg = E.coarse_ground_z
    zp = r["z_portal"]
    # --- floor: V18g (Google satellite, Street View 2026): grey concrete in bands across the ramp, not setts ---
    for j in range(ALONG):
        t0, t1 = j / ALONG, (j + 1) / ALONG
        for k in range(ACROSS):
            s0, s1 = k / ACROSS, (k + 1) / ACROSS
            quad = []
            for t, s in ((t0, s0), (t1, s0), (t1, s1), (t0, s1)):
                x, y = _lerp(_lerp(sw, nw, s), _lerp(se, ne, s), t)
                quad.append((x, y, _floor_z(t, s)))
            m.face(quad, "planks", (0, 0, 1), [((p[0] * ax[1] - p[1] * ax[0]) / P.PLANK_TILE,
                                                (p[0] * ax[0] + p[1] * ax[1]) / P.PLANK_TILE) for p in quad])
    # --- portal wall top (one level) ---
    top_p = max(zg(*p) for p in (se, ne, o_se, o_ne, _lerp(se, ne, 0.5))) + P.RAMP_PARAPET
    # --- side walls: inner face from the floor, top, outer face of the parapet ---
    for inner_a, inner_b, outer_a, outer_b, s in ((sw, se, o_sw, o_se, 0.0), (nw, ne, o_nw, o_ne, 1.0)):
        length = math.dist(inner_a, inner_b)
        nrm = (outer_a[0] - inner_a[0], outer_a[1] - inner_a[1])
        d = math.hypot(*nrm)
        nrm = (nrm[0] / d, nrm[1] / d, 0.0)
        st = []
        for j in range(ALONG + 1):
            t = j / ALONG
            i_p, o_p = _lerp(inner_a, inner_b, t), _lerp(outer_a, outer_b, t)
            g = max(zg(*i_p), zg(*o_p))
            top = g + P.RAMP_PARAPET * min(1.0, t * length / 3.0)
            if j == ALONG:
                top = top_p
            st.append((i_p, o_p, _floor_z(t, s), top, zg(*o_p)))
        # V18h: board-marked concrete (u along the wall, v up, 2 m tile) under a coping of pale precast stones
        seg = length / ALONG
        for j, ((i0, o0, f0, t0, g0), (i1, o1, f1, t1, g1)) in enumerate(zip(st, st[1:])):
            u0, u1 = j * seg / WALL_TILE, (j + 1) * seg / WALL_TILE
            m.face([(*i0, f0), (*i1, f1), (*i1, t1), (*i0, t0)], "concrete", (-nrm[0], -nrm[1], 0.0),
                   [(u0, f0 / WALL_TILE), (u1, f1 / WALL_TILE), (u1, t1 / WALL_TILE), (u0, t0 / WALL_TILE)])
            c0, c1 = j * seg / P.EDGING_TILE[0], (j + 1) * seg / P.EDGING_TILE[0]
            m.face([(*i0, t0), (*i1, t1), (*o1, t1), (*o0, t0)], "edging", (0, 0, 1), [(c0, 0.0), (c1, 0.0), (c1, 0.8), (c0, 0.8)])
            m.face([(*o0, g0 - 0.2), (*o1, g1 - 0.2), (*o1, t1), (*o0, t0)], "concrete", nrm,
                   [(u0, (g0 - 0.2) / WALL_TILE), (u1, (g1 - 0.2) / WALL_TILE), (u1, t1 / WALL_TILE), (u0, t0 / WALL_TILE)])
    # --- portal: lintel over the black opening, wall top, east face, the void as a closed black box ---
    # V16: a dark fascia band over the whole opening; the slab edge and the parapet above it are white (seen from the
    # avenue the mouth is a dark slot in a white wall)
    zc, zf_ = zp + P.PORTAL_H, zp + P.PORTAL_H + P.RAMP_FASCIA
    for z0, z1, key in ((zc, zf_, "dark"), (zf_, top_p, "white")):
        m.face([(*se, z0), (*ne, z0), (*ne, z1), (*se, z1)], key, (-ax[0], -ax[1], 0.0))
    wide = math.dist(se, ne)
    m.face([(*se, top_p), (*ne, top_p), (*o_ne, top_p), (*o_se, top_p)], "edging", (0, 0, 1),
           [(0.0, 0.0), (wide / P.EDGING_TILE[0], 0.0), (wide / P.EDGING_TILE[0], 0.8), (0.0, 0.8)])
    zb = min(zg(*o_se), zg(*o_ne)) - 0.2
    m.face([(*o_se, zb), (*o_ne, zb), (*o_ne, top_p), (*o_se, top_p)], "concrete", (ax[0], ax[1], 0.0),
           [(0.0, zb / WALL_TILE), (wide / WALL_TILE, zb / WALL_TILE), (wide / WALL_TILE, top_p / WALL_TILE), (0.0, top_p / WALL_TILE)])
    deep = 1.4
    b_se, b_ne = (se[0] + ax[0] * deep, se[1] + ax[1] * deep), (ne[0] + ax[0] * deep, ne[1] + ax[1] * deep)
    zc = zp + P.PORTAL_H
    m.face([(*b_se, zp), (*b_ne, zp), (*b_ne, zc), (*b_se, zc)], "void", (-ax[0], -ax[1], 0.0))
    m.face([(*se, zc), (*ne, zc), (*b_ne, zc), (*b_se, zc)], "void", (0, 0, -1))
    m.face([(*se, zp), (*ne, zp), (*b_ne, zp), (*b_se, zp)], "void", (0, 0, 1))
    across = (ne[0] - se[0], ne[1] - se[1], 0.0)
    m.face([(*se, zp), (*b_se, zp), (*b_se, zc), (*se, zc)], "void", across)
    m.face([(*ne, zp), (*b_ne, zp), (*b_ne, zc), (*ne, zc)], "void", (-across[0], -across[1], 0.0))
    obj = m.build("Entrance_Ramp", col, materials())
    for p in r["outer"] + [_lerp(o_sw, o_se, 0.5), _lerp(o_nw, o_ne, 0.5)]:
        _note("ramp walls (parapet 1.1 m)", *p)
    print(f"  entrance: ramp {sum(len(p.vertices) - 2 for p in obj.data.polygons)} triangles, portal "
          f"{math.dist(se, ne):.1f} x {P.PORTAL_H:.2f} m, wall top {top_p:.2f} m, floor at the portal {zp:.2f} m")


# =====================================================================================================
# Paving: calcada with the drain channels, draped like env_streets' paving
# =====================================================================================================
def _channel_quads():
    """Each drain channel cut to the plaza ring: [(start, direction, length)]. V18g (Google satellite): every channel
    runs from the plaza's north edge across the slab band to B's planted strip or wall (V16: they stopped at the band)."""
    out = []
    for k, ((px, py), (dx, dy)) in enumerate(P.channel_lines()):
        run, step = None, 0.25
        t = -160.0
        while t <= 160.0:
            x, y = px + dx * t, py + dy * t
            ok = P.inside(x, y, P.RING) and P.ring_distance(x, y, P.RING) > 0.2
            if ok and run is None:
                run = t
            if not ok and run is not None:
                if t - step - run > 1.0:
                    out.append(((px + dx * run, py + dy * run), (dx, dy), t - step - run))
                run = None
            t += step
    return out


def _cutters(ring=None, main=None):
    """Rings a paving sheet leaves free: the ramp, the entrance pool and its rim, buildings, the street section's own
    paving, env_campus' keep-out rings (berms, pools). ring: the sheet's outline (default: the plaza). main: the main
    building's outline to cut with (default: campus_plan.MAIN_BODY, which is open at the west entrance recess)."""
    import campus_plan as C
    import env_campus
    import street_plan as S
    ring = ring or P.RING
    xs, ys = [p[0] for p in ring], [p[1] for p in ring]
    box = (min(xs) - 1, min(ys) - 1, max(xs) + 1, max(ys) + 1)

    def near(r):
        return not (max(p[0] for p in r) < box[0] or min(p[0] for p in r) > box[2]
                    or max(p[1] for p in r) < box[1] or min(p[1] for p in r) > box[3])
    rings = [("ramp", P.ramp_outer(0.03)), ("pool", env_campus.grow(env_campus.ccw(list(C.A_POOL)), 0.3)),
             ("main building", list(main or C.MAIN_BODY))]
    for b in E.DATA["buildings"]:
        if b["id"] != C.MAIN_ID and near(b["points"]):
            rings.append(("building " + b["id"], [tuple(p) for p in b["points"]]))
    for lay in S.LAYERS:
        if near(lay["ring"]):
            if lay["name"] == "N Milestone bank":          # V18h: the fan's hem dips under the bank's first 0.7 m
                rings.append(("street " + lay["name"], P.bite(P.J_BANK_V18G, P.FAN_R + P.FAN_HEM)))
                continue
            rings.append(("street " + lay["name"], list(lay["ring"])))
    for r in env_campus.KEEP_OUT:
        if near(r):
            rings.append(("campus keep-out", list(r)))
    return [(n, r) for n, r in rings if len(r) >= 3 and near(r)]


# V16: between the ramp's portal wall and the loop the track corridor ends 0.2 m above the ramp's apron (it is held
# back from the ramp; seam at (-51.7, 132.4), where the sheet dipped up to 13.7 cm under the ground). Inside this box
# the sheet lies on a soft hull of the ground: at least as high as any ground point within SEAM_REACH minus
# SEAM_SLOPE per metre, so it comes down from the corridor's edge as a 7 % ramp instead of a crease.
SEAM_BOX = (-68.0, 114.0, -46.0, 144.0)      # V18h: the ramp's portal is 6 m further west
SEAM_REACH, SEAM_SLOPE = 3.0, 0.07


def drape(name, layers, cutters, col, soften=None, keep_out=None, level=None, sink=None):
    """A paving sheet draped over the built terrain like env_streets' paving: 7 cm over the ground, down to 4 mm at
    the foot of the road's shoulder, nothing within 4.95 m of the centreline. layers: [{name, ring, mat, uv, clip}],
    later ones lie on top; mat = a key of materials() or 'pal:<street palette colour>'; clip = a ring outside which
    the layer does not count. cutters: [(name, ring)] left free. soften = (x0, y0, x1, y1): inside this box the sheet
    lies on a soft hull of the ground (SEAM_REACH, SEAM_SLOPE) and is split down to 0.3 m edges wherever it would
    still lie more than 2 cm under it (a seam in the terrain). Returns the object.
    V18h: keep_out = the distance from the centreline at which this sheet is clipped (default street_plan.KEEP_OUT,
    4.95 m; the plaza: 4.02 m, it covers the road's shoulder); level(x, y, d, z) -> the height to use instead of the
    draped z (the plaza: road level beside the road); sink(x, y) -> metres the sheet lies lower there (a hem that dips
    under the lawn). A layer may carry "uvf": a function (x, y) -> uv instead of the planar "uv" frame."""
    import env_streets as ST
    import street_plan as S
    layers = list(layers) + [{"name": n, "ring": r, "mat": None, "uv": None} for n, r in cutters]
    std_keep = S.KEEP_OUT
    if keep_out:
        S.KEEP_OUT = keep_out                # Soup.clip_route / refine_near_route read it; set back below
    try:
        return _drape(name, layers, cutters, col, soften, level, sink, std_keep)
    finally:
        S.KEEP_OUT = std_keep


def _std_lift(d, keep):
    """env_streets.lift_at for the standard keep-out line (the taper starts at the foot of the road's shoulder)."""
    import env_streets as ST
    o = d - keep
    if o <= ST.TAPER[0]:
        return ST.LIFT_EDGE + (ST.LIFT_NEAR - ST.LIFT_EDGE) * max(o, 0.0) / ST.TAPER[0]
    t = min((o - ST.TAPER[0]) / (ST.TAPER[1] - ST.TAPER[0]), 1.0)
    return ST.LIFT_NEAR + (ST.LIFT - ST.LIFT_NEAR) * t * t * (3 - 2 * t)


def _drape(name, layers, cutters, col, soften, level, sink, std_keep):
    import env_streets as ST
    import street_plan as S
    soup = ST.overlay(layers)

    def keep(tr):
        lay = layers[tr[3]]
        if lay["mat"] is None:
            return False
        if lay.get("clip"):
            (ax, ay), (bx, by), (cx, cy) = (soup.xy[v] for v in tr[:3])
            return P.inside((ax + bx + cx) / 3, (ay + by + cy) / 3, lay["clip"])
        return True
    soup.tris = [tr for tr in soup.tris if keep(tr)]
    counts = [len(soup.tris)]
    soup.refine_near_route(3.0)
    soup.clip_route()
    counts.append(len(soup.tris))
    zc = {}

    def zf(x, y):
        key = (round(x, 4), round(y, 4))
        if key not in zc:
            d = S.route_distance(x, y)
            z = ST._paving_z(d, x, y) - ST.lift_at(d) + _std_lift(d, std_keep)
            if soften and soften[0] <= x <= soften[2] and soften[1] <= y <= soften[3]:
                g = ST.ground(x, y)
                hull = g
                for k in range(12):
                    ca, sa = math.cos(math.pi * k / 6), math.sin(math.pi * k / 6)
                    for r in (0.5, 1.0, 1.5, 2.0, 2.5, 3.0):
                        if r <= SEAM_REACH:
                            hull = max(hull, ST.ground(x + ca * r, y + sa * r) - SEAM_SLOPE * r)
                z += hull - g
            if level:
                z = level(x, y, d, z)
            if sink:
                z -= sink(x, y)
            zc[key] = z
        return zc[key]
    soup.limit_edges(5.0)
    soup.follow_ground(zf, up=0.012, down=0.05)
    if soften:
        x0, y0, x1, y1 = soften

        def want(a, b):
            (ax, ay), (bx, by) = soup.xy[a], soup.xy[b]
            mx, my = (ax + bx) / 2, (ay + by) / 2
            if not (x0 <= mx <= x1 and y0 <= my <= y1) or soup.length(a, b) < 0.3 \
                    or S.route_distance(mx, my) < S.KEEP_OUT:      # (the middle of a chord along the clipped edge)
                return False
            return zf(mx, my) - (zf(ax, ay) + zf(bx, by)) / 2 > 0.02
        for _ in range(6):
            if not soup.split(want):
                break
    soup.clip_route()        # a split edge along the clipped outline has its middle up to 2 cm inside the keep-out
    counts.append(len(soup.tris))
    worst, where, low = 1.0, None, 0                # proof: how close does the paving come to the ground anywhere?
    for a, b, c, tag in soup.tris:
        pa, pb, pc = soup.xy[a], soup.xy[b], soup.xy[c]
        za, zb, zc_ = (zf(*soup.xy[v]) for v in (a, b, c))
        for wa, wb, wc in ((1 / 3, 1 / 3, 1 / 3), (0.6, 0.2, 0.2), (0.2, 0.6, 0.2), (0.2, 0.2, 0.6), (0.5, 0.5, 0.0),
                           (0.0, 0.5, 0.5), (0.5, 0.0, 0.5)):
            x, y = wa * pa[0] + wb * pb[0] + wc * pc[0], wa * pa[1] + wb * pb[1] + wc * pc[1]
            gap = wa * za + wb * zb + wc * zc_ - ST.ground(x, y)
            if sink and sink(x, y) > 0.0:              # the hem lies under the lawn on purpose
                continue
            low += gap < -0.02
            if gap < worst:
                worst, where = gap, (round(x, 1), round(y, 1), layers[tag]["name"])
    m = Faces()
    area = 0.0
    for a, b, c, tag in soup.tris:
        lay = layers[tag]
        pts = [(soup.xy[v][0], soup.xy[v][1], zf(*soup.xy[v])) for v in (a, b, c)]
        area += abs((pts[1][0] - pts[0][0]) * (pts[2][1] - pts[0][1]) - (pts[2][0] - pts[0][0]) * (pts[1][1] - pts[0][1])) / 2
        uvs = [lay["uvf"](p[0], p[1]) for p in pts] if lay.get("uvf") else [ST._layer_uv(lay, p) for p in pts]
        m.face(pts, "palette" if lay["mat"].startswith("pal:") else lay["mat"], (0, 0, 1), uvs)
    # shared vertices (one smooth draped sheet, a third of the vertex count)
    obj = m.build(name, col, materials())
    _weld(obj.data)
    d_min = min(soup.dist(v) for tr in soup.tris for v in tr[:3])
    print(f"  {name}: {len(obj.data.polygons)} triangles, {area:.0f} m2, {len(cutters)} cut-outs; overlay / clip at the "
          f"road / follow the ground: {counts}; nearest point {d_min:.2f} m from the centreline")
    print(f"  {name}: least clearance over the ground (7 samples per triangle): {worst * 100:.1f} cm at "
          f"{where}; {low} samples more than 2 cm under the ground")
    return obj


def _plaza_level(x, y, d, z):
    """V18h: beside the road the plaza sheet lies level with the road (4 mm over it at the road's edge): it covers
    the road's shoulder and the 11 cm gutter at its foot, and returns to its draped height over FILL_REACH. Where the
    plaza ends (FILL_I, FILL_TAPER) it comes down onto the shoulder again."""
    import env_track
    import street_plan as S
    if d >= S.KEEP_OUT_STD + P.FILL_REACH:
        return z
    p = E.nearest_route_point(x, y)
    w = P.fill_weight(p["i"], len(E.ROUTE["points"]))
    top = env_track.road_z(p) + 0.004
    if d <= S.KEEP_OUT_STD:
        # the shoulder under the sheet: from the road's edge (HALF) down to the bed over SHOULDER_M
        f = min(max((d - env_track.HALF) / env_track.SHOULDER_M, 0.0), 1.0)
        shoulder = env_track.road_z(p) - f * (env_track.road_z(p) - (z - 0.004)) + 0.006
        return shoulder + max(0.0, top - shoulder) * w
    return z + max(0.0, top - z) * w * (1.0 - P._ss((d - S.KEEP_OUT_STD) / P.FILL_REACH))


def _hem_sink(x, y):
    """V18h: beyond the fan's rim the sheet goes down from 7 cm over the lawn to 7 cm under it (FAN_HEM)."""
    o = P.in_hem(x, y)
    return 0.14 * min(1.0, o / P.FAN_HEM) if o > 0.0 else 0.0


def build_paving(col, trees=()):
    """The plaza sheet. Layers from the bottom: calcada, the plank band along B, (V18h) the fan's rim stones with the
    hem beyond them and the dark ring round the loop's centre, a gravel pit under every young tree, the drain
    channels. trees: (x, y) of the young trees (dress() knows them)."""
    import street_plan as S
    t = P.CALCADA_TILE
    layers = [{"name": "plaza", "ring": list(P.RING), "mat": "calcada", "uv": ((0.0, 0.0), P.ROW_DIR, t, t)},
              {"name": "plank band", "ring": P.band_ring(), "mat": "planks", "clip": list(P.RING),
               "uv": (P.BAND_O, P.BAND_U, P.PLANK_TILE, P.PLANK_TILE)}]
    b0, b1 = P.FAN_ARC
    su, sv = P.EDGING_TILE
    rim_uv = P.polar_uv(b0, P.FAN_R - P.FAN_EDGE, P.FAN_R, su, sv)
    layers.append({"name": "fan rim stones", "ring": P.arc_band(b0, b1, P.FAN_R - P.FAN_EDGE, P.FAN_R, 2.0), "mat": "edging",
                   "uv": None, "uvf": rim_uv})
    layers.append({"name": "fan hem", "ring": P.arc_band(b0, b1, P.FAN_R, P.FAN_R + P.FAN_HEM, 2.0), "mat": "edging",
                   "uv": None, "uvf": rim_uv})
    if P.CENTRE_RING:
        r1 = P.CENTRE_RING["r"] + P.CENTRE_RING["band"] / 2
        r0 = r1 - P.CENTRE_RING["band"]
        for a in (0.0, 180.0):
            layers.append({"name": "centre ring", "ring": P.arc_band(a, a + 180.0, r0, r1, 6.0), "mat": "setts", "uv": None,
                           "uvf": P.polar_uv(a, r0, (r0 + r1) / 2, 2.0, 2.0)})
    h = P.PIT / 2
    for x, y in trees:
        ts = P.band_ts(x, y)
        layers.append({"name": "tree pit", "mat": "pal:" + P.PIT_COLOUR, "uv": None, "clip": list(P.RING),
                       "ring": [P.band_pt(ts[0] + a * h, ts[1] + b * h) for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]})
    n_pits = len(trees)
    w = P.CHANNEL_W
    quads = _channel_quads()
    for k, (a, d, length) in enumerate(quads):
        n = (-d[1], d[0])
        o = (a[0] - n[0] * w / 2, a[1] - n[1] * w / 2)
        e = (o[0] + d[0] * length, o[1] + d[1] * length)
        layers.append({"name": f"drain channel {k}", "mat": "drain", "uv": (o, d, 0.5, w),
                       "ring": [o, e, (e[0] + n[0] * w, e[1] + n[1] * w), (o[0] + n[0] * w, o[1] + n[1] * w)]})
    S.KEEP_OUT_STD = S.KEEP_OUT
    drape("Entrance_Paving", layers, _cutters(), col, soften=SEAM_BOX, keep_out=PLAZA_KEEP_OUT, level=_plaza_level,
          sink=_hem_sink)
    print(f"  entrance: paving layers: plank band {P.BAND_S[1] - max(P.BAND_S[0], 0.0):.1f} m wide along B, {n_pits} tree pits, "
          f"{len(quads)} drain channel pieces of {w} m; V18h: the sheet starts {PLAZA_KEEP_OUT} m from the centreline "
          f"(over the road's shoulder), rim stones and hem along bearings {b0:.0f}-{b1:.0f} at {P.FAN_R} m from the "
          f"loop's centre")


def _weld(mesh):
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0005)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def build():
    """The ramp (call after the buildings: env_campus' keep-out rings are known then). V16: the paving is built at
    the end of dress(), when the young trees have their final places (their gravel pits are part of the sheet)."""
    col = E.collection(COLLECTION)
    build_ramp(col)


# =====================================================================================================
# Prototypes and dressing
# =====================================================================================================
def seat_block(name, col):
    """Pale concrete seat block (entrance_plan.SEAT_SIZE: 3.0 x 0.6 x 0.45 m), long side along x, top edges bevelled 4 cm."""
    length, width, height = P.SEAT_SIZE
    hx, hy, bev = length / 2, width / 2, 0.04
    m = Faces()
    low = [(-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy)]
    top = [(-hx + bev, -hy + bev), (hx - bev, -hy + bev), (hx - bev, hy - bev), (-hx + bev, hy - bev)]
    for k in range(4):
        a, b, c, d = low[k], low[(k + 1) % 4], top[(k + 1) % 4], top[k]
        out = (b[1] - a[1], a[0] - b[0], 0.0)
        m.face([(*a, -0.12), (*b, -0.12), (*b, height - bev), (*a, height - bev)], "seat", out)
        m.face([(*a, height - bev), (*b, height - bev), (*c, height), (*d, height)], "seat", (out[0], out[1], 1.0))
    m.face([(*p, height) for p in top], "seat", (0, 0, 1))
    return m.build(name, col, materials())


def lamp(name, col):
    """Slim plaza lamp column (V16, photospheres N08, N13): a dark anthracite column of 0.16 m, 5.5 m high, with the
    lantern built into its top (a lit band under a dark cap), no arm. 40 triangles."""
    m = Faces()
    h, n = P.LAMP_H, 6

    def ring(r):
        return [(r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n)) for k in range(n)]

    def tube(r, z0, z1, key):
        pts = ring(r)
        for a, b in zip(pts, pts[1:] + pts[:1]):
            m.face([(*a, z0), (*b, z0), (*b, z1), (*a, z1)], key, (a[0] + b[0], a[1] + b[1], 0.0))
    tube(0.08, -0.2, h - 0.85, "dark")
    tube(0.068, h - 0.85, h - 0.2, "light")
    tube(0.085, h - 0.2, h, "dark")
    m.face([(*p, h) for p in ring(0.085)], "dark", (0, 0, 1))
    m.face([(*p, h - 0.2) for p in ring(0.085)], "dark", (0, 0, -1))
    return m.build(name, col, materials())


def bollard(name, col):
    """Slim steel bollard of the apron line in front of the west doors (N13): 0.95 m, 11 cm, street palette grey."""
    import env_street_objects as SO
    g = SO.Proto()
    g.cyl(0.055, 0.055, -0.15, 0.95, 6, "steel_grey")
    return g.build(name, col)


def build_prototypes():
    pc = E.collection("Proto_Props")
    protos = {"Prop_SeatBlockPlaza": seat_block("Prop_SeatBlockPlaza", pc), "Prop_LampPlaza": lamp("Prop_LampPlaza", pc),
              "Prop_BollardSlim": bollard("Prop_BollardSlim", pc)}
    print("  entrance: prototypes " + ", ".join(f"{n} {sum(len(p.vertices) - 2 for p in o.data.polygons)}"
                                                for n, o in protos.items()))
    return protos


def _hash01(x, y, salt=0):
    v = math.sin(x * 12.9898 + y * 78.233 + salt * 37.719) * 43758.5453
    return v - math.floor(v)


def _along_row(x, y, ok, reach=6.0, step=0.5):
    """V15b: the nearest spot along the plaza's rows (bearing 69 deg, entrance_plan.SEAT_STEP) within `reach` m for
    which ok(x, y) holds; None if there is none."""
    ux, uy = P._unit((0.0, 0.0), P.SEAT_STEP)
    k = 1
    while k * step <= reach + 1e-9:
        for sign in (1.0, -1.0):
            qx, qy = x + ux * sign * k * step, y + uy * sign * k * step
            if ok(qx, qy):
                return qx, qy
        k += 1
    return None


def _off_the_line(x, y, need, ok, reach=8.0, step=0.5):
    """V15b: (x, y) moved straight away from the race centreline until it is `need` m from it and ok(x, y) holds;
    None if that takes more than `reach` m."""
    q = E.nearest_route_point(x, y)
    d = math.hypot(x - q["x"], y - q["y"]) or 1.0
    ux, uy = (x - q["x"]) / d, (y - q["y"]) / d
    k = 1
    while k * step <= reach + 1e-9:
        qx, qy = x + ux * k * step, y + uy * k * step
        if route_distance(qx, qy) >= need and ok(qx, qy):
            return qx, qy
        k += 1
    return None


def need_clear(x, y):
    """V18h: the distance from the race centreline that anything standing at (x, y) must keep: ROUTE_CLEAR, and
    entrance_plan.FAN_CLEAR beside the curve plaza (route points FAN_I, fading over FAN_TAPER points)."""
    p = E.nearest_route_point(x, y)
    return ROUTE_CLEAR + (P.FAN_CLEAR - ROUTE_CLEAR) * P.fan_weight(p["i"], len(E.ROUTE["points"]))


def in_loop(x, y):
    """V18h: inside the race line's loop (the island a kart may cut across): nothing stands there."""
    return P.loop_polar(x, y)[1] < P.LOOP_R


def _across_row(x, y, ok, reach=9.0, step=0.25):
    """V18h: the nearest spot straight towards wing B (across the plaza's rows, to its edge) for which ok(x, y) holds,
    sliding up to 2.4 m along the row at each step; None if there is none within `reach` m."""
    ux, uy = P.BAND_U
    nx, ny = -P.BAND_N[0], -P.BAND_N[1]
    k = 1
    while k * step <= reach + 1e-9:
        for along in (0.0, 0.6, -0.6, 1.2, -1.2, 1.8, -1.8, 2.4, -2.4):
            qx, qy = x + nx * k * step + ux * along, y + ny * k * step + uy * along
            if ok(qx, qy):
                return qx, qy
        k += 1
    return None


def raise_nodes(heights):
    """V18h (one call at the end of env_terrain.apply): the 5 m terrain nodes south and south-west of the loop and
    along the start straight are raised to the plaza's level (entrance_plan.level_weight; never lowered). The DEM
    fell away towards wing B by up to 1 m from 7 m beside the race line; the slope now lies in B's planted strip."""
    n, top = 0, 0.0
    for j in range(E.ROWS):
        for i in range(E.COLS):
            x, y = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            w = P.level_weight(x, y)
            if w <= 0.0:
                continue
            dz = (P.PLAZA_LEVEL - heights[j][i]) * w
            if dz > 0.005:
                heights[j][i] += dz
                n += 1
                top = max(top, dz)
    print(f"  entrance (V18h): {n} terrain nodes raised to the curve plaza's level {P.PLAZA_LEVEL} m (by up to {top:.2f} m)")


def _rez(veg, props):
    """V18h: plants and props keep the height of the approved dressing; where raise_nodes lifted the ground over
    their foot they are set on the new ground."""
    n = 0
    for items, base, slack in ((veg, 0.0, 0.06), (props, 0.05, 0.03)):
        for k, p in enumerate(items):
            if P.level_weight(p["x"], p["y"]) <= 0.0:
                continue
            g = E.ground_z(p["x"], p["y"])
            if p["z"] < g + base - slack:
                items[k] = dict(p, z=round(g + base, 3))
                n += 1
    return n


TREE_MARGIN = 0.25           # m: a young tree's trunk and stakes keep ROUTE_CLEAR + this from the centreline
_MOVES = []                  # V15b: report lines, what the start straight moved or dropped on the plaza


def _clear_the_line(veg, props):
    """V15b (start straight): the frozen dressing was approved for the old line. Whatever now stands within its
    clearance of the changed line inside SWEEP_BOX (plants: KEEP_CLEAR + crown, as env_vegetation's rule; props:
    ROUTE_CLEAR) moves straight away from the line to the nearest free spot, or is dropped. The plaza's own trees and
    lamps (in the rows) are handled in dress(). Elsewhere nothing matches, so nothing changes."""
    import env_vegetation
    x0, y0, x1, y1 = P.SWEEP_BOX
    crown = {name: k["crown"] for k in env_vegetation.KIND.values() for name in k["protos"]}
    walls = [[tuple(q) for q in b["points"]] for b in E.DATA["buildings"]]

    def off_walls(x, y, gap):
        return all(not P.inside(x, y, w) and P.ring_distance(x, y, w) >= gap for w in walls
                   if min(q[0] for q in w) - 12 < x < max(q[0] for q in w) + 12)
    out_v = []
    for p in veg:
        x, y = p["x"], p["y"]
        plaza_tree = p["proto"] in ("Veg_Olive", "Veg_Olive_B") and P.near_ring(x, y, P.RING, P.TREE_PAD)
        if plaza_tree or not (x0 <= x <= x1 and y0 <= y <= y1):
            out_v.append(p)
            continue
        reach = crown.get(p["proto"], 1.0) * p.get("scale", 1.0)
        need = max(E.KEEP_CLEAR + reach, need_clear(x, y) + 0.5)       # V18h: the curve plaza keeps 10 m free
        d = route_distance(x, y)
        if d >= need:
            out_v.append(p)
            continue
        if in_loop(x, y):
            _MOVES.append(f"{p['proto']} ({x:.1f}, {y:.1f}) inside the loop: dropped (V18h)")
            continue
        spot = _off_the_line(x, y, need + 0.2, lambda qx, qy: off_walls(qx, qy, reach + 0.3)
                             and all(math.hypot(o["x"] - qx, o["y"] - qy) >= 2.5 for o in veg if o is not p))
        if spot:
            _MOVES.append(f"{p['proto']} ({x:.1f}, {y:.1f}), {d:.1f} m from the new line (needs {need:.1f}): moved to "
                          f"({spot[0]:.1f}, {spot[1]:.1f}), {route_distance(*spot):.1f} m")
            out_v.append(dict(p, x=round(spot[0], 3), y=round(spot[1], 3), z=round(E.ground_z(*spot), 3)))
        else:
            _MOVES.append(f"{p['proto']} ({x:.1f}, {y:.1f}), {d:.1f} m from the new line (needs {need:.1f}): dropped, no free spot")
    out_p = []
    for p in props:
        x, y = p["x"], p["y"]
        if not (x0 <= x <= x1 and y0 <= y <= y1) or p["proto"] in ("Prop_LampCampus", "Prop_RailingSegment") \
                or route_distance(x, y) >= need_clear(x, y):
            out_p.append(p)
            continue
        d = route_distance(x, y)
        if in_loop(x, y):                                           # V18h: the island is free; the bin goes to the rim
            if p["proto"] == "Prop_Bin":
                bx, by = P.loop_pt(*P.RIM_BIN)
                _MOVES.append(f"{p['proto']} ({x:.1f}, {y:.1f}) inside the loop: moved to the lawn behind the fan's rim "
                              f"({bx:.1f}, {by:.1f}), {route_distance(bx, by):.1f} m from the line (V18h)")
                out_p.append(dict(p, x=round(bx, 3), y=round(by, 3), z=round(E.ground_z(bx, by) + 0.05, 3)))
                _note("bin on the rim", bx, by, 0.35)
            else:
                _MOVES.append(f"{p['proto']} ({x:.1f}, {y:.1f}) inside the loop: dropped (V18h)")
            continue
        spot = _off_the_line(x, y, need_clear(x, y) + 0.5, lambda qx, qy: off_walls(qx, qy, 1.0)
                             and all(math.hypot(o["x"] - qx, o["y"] - qy) >= 1.2 for o in props if o is not p))
        if spot:
            _MOVES.append(f"{p['proto']} ({x:.1f}, {y:.1f}), {d:.1f} m from the new line: moved to "
                          f"({spot[0]:.1f}, {spot[1]:.1f}), {route_distance(*spot):.1f} m")
            out_p.append(dict(p, x=round(spot[0], 3), y=round(spot[1], 3), z=round(E.ground_z(*spot) + 0.05, 3)))
        else:
            _MOVES.append(f"{p['proto']} ({x:.1f}, {y:.1f}), {d:.1f} m from the new line: dropped, no free spot")
    return out_v, out_p


def dress(veg, props, protos):
    """Edits the dressing lists after dressing_freeze.apply (everything outside the plaza stays as it is) and rewrites
    the two placement files. protos: build_world's prototype table, our prototypes are added to it."""
    protos.update(build_prototypes())
    veg, props = _clear_the_line(veg, props)                    # V15b
    inner = _ramp()["outer"]
    veg2, trees, swapped = [], [], 0
    for p in veg:
        x, y = p["x"], p["y"]
        if P.inside(x, y, inner):
            continue                                            # nothing grows in the ramp
        if p["proto"] in ("Veg_Olive", "Veg_Olive_B") and P.near_ring(x, y, P.RING, P.TREE_PAD):
            # V16: 5-6.5 m tall, crown 1.7-2.2 m (the prototype is 5.6 m / 1.9 m at scale 1)
            p = dict(p, proto="Veg_PlazaTree" if _hash01(x, y) < 0.5 else "Veg_PlazaTree_B",
                     scale=round(0.9 + 0.24 * _hash01(x, y, 1), 3))
            swapped += 1
            ts = P.band_ts(x, y)
            if 6.0 < ts[1] < 7.6:                               # V18g: the south row stands 2.5 m inside the paving
                qx, qy = (round(v, 3) for v in P.band_pt(ts[0], P.TREE_ROW_S))
                if route_distance(qx, qy) >= ROUTE_CLEAR + TREE_MARGIN + 0.7:       # (not where the race line is near)
                    x, y = qx, qy
                    p = dict(p, x=x, y=y, z=round(E.ground_z(x, y), 3))
            trees.append((x, y))
        veg2.append(p)
    # V15b: a young tree whose trunk and stakes would stand within ROUTE_CLEAR (+ TREE_MARGIN) of the new line moves
    # along its row to the nearest free spot (3.5 m from the other trees, on the plaza), or is dropped
    for k, p in enumerate(veg2):
        if not p["proto"].startswith("Veg_PlazaTree"):
            continue
        x, y, extra = p["x"], p["y"], TREE_MARGIN + 0.5 * p["scale"]
        need = need_clear(x, y) + extra
        d = route_distance(x, y)
        if d >= need:
            _note("young trees (trunk and stakes)", x, y, 0.5 * p["scale"])
            continue
        others = [t for t in trees if t != (x, y)]
        # V18h: first straight towards wing B, to the plaza's edge (the tree rows close up on the slab band) ...
        spot = _across_row(x, y, lambda qx, qy: route_distance(qx, qy) >= need_clear(qx, qy) + extra
                           and P.inside(qx, qy, P.RING) and P.band_ts(qx, qy)[1] >= P.TREE_EDGE_S
                           and all(math.hypot(tx - qx, ty - qy) >= 3.0 for tx, ty in others)) \
            if need > ROUTE_CLEAR + extra + 0.01 else None
        # ... else along its row (V15b)
        spot = spot or _along_row(x, y, lambda qx, qy: route_distance(qx, qy) >= need_clear(qx, qy) + extra
                                  and P.near_ring(qx, qy, P.RING, P.TREE_PAD)
                                  and all(math.hypot(tx - qx, ty - qy) >= 3.5 for tx, ty in others))
        trees.remove((x, y))
        if spot:
            _MOVES.append(f"young tree ({x:.1f}, {y:.1f}), {d:.1f} m from the line (needs {need:.1f}): moved to "
                          f"({spot[0]:.1f}, {spot[1]:.1f}), {route_distance(*spot):.1f} m")
            veg2[k] = dict(p, x=round(spot[0], 3), y=round(spot[1], 3), z=round(E.ground_z(*spot), 3))
            trees.append((veg2[k]["x"], veg2[k]["y"]))
            _note("young trees (trunk and stakes)", veg2[k]["x"], veg2[k]["y"], 0.5 * p["scale"])
        else:
            _MOVES.append(f"young tree ({x:.1f}, {y:.1f}), {d:.1f} m from the new line (needs {need:.1f}): dropped, "
                          f"no free spot within 6 m in its row")
            veg2[k] = None
    veg2 = [p for p in veg2 if p is not None]
    props2, lamps = [], 0
    for p in props:
        if P.inside(p["x"], p["y"], inner):
            continue
        if p["proto"] == "Prop_LampCampus" and P.inside(p["x"], p["y"], P.RING):
            # the frozen campus lamp stands 6.5 m from the centreline: the slim one 0.3 m further out, head away
            # (V15b: a lamp the new line passes closer than that goes out to the same 6.8 m)
            q = E.nearest_route_point(p["x"], p["y"])
            d = math.hypot(p["x"] - q["x"], p["y"] - q["y"]) or 1.0
            ux, uy = (p["x"] - q["x"]) / d, (p["y"] - q["y"]) / d
            near = route_distance(p["x"], p["y"])
            if in_loop(p["x"], p["y"]):                             # V18h: the loop's lamp stands behind the fan's rim
                lx, ly = P.loop_pt(*P.RIM_LAMP)
                _MOVES.append(f"campus lamp ({p['x']:.1f}, {p['y']:.1f}) inside the loop: slim lamp on the lawn behind the "
                              f"fan's rim ({lx:.1f}, {ly:.1f}), {route_distance(lx, ly):.1f} m from the line (V18h)")
                props2.append(dict(p, proto="Prop_LampPlaza", x=round(lx, 3), y=round(ly, 3),
                                   z=round(E.ground_z(lx, ly) + 0.05, 3), rot_z=0.0))
                lamps += 1
                _note("slim lamps (pole)", lx, ly, 0.07)
                continue
            clear = need_clear(p["x"], p["y"])
            out = 0.3 if near >= clear - 0.05 else clear + 0.3 - near
            if out > 0.3:
                _MOVES.append(f"campus lamp ({p['x']:.1f}, {p['y']:.1f}), {near:.1f} m from the new line: slim lamp "
                              f"{out:.1f} m further out at ({p['x'] + ux * out:.1f}, {p['y'] + uy * out:.1f})")
            p = dict(p, proto="Prop_LampPlaza", x=round(p["x"] + ux * out, 3), y=round(p["y"] + uy * out, 3),
                     rot_z=round(math.atan2(uy, ux), 4))
            lamps += 1
            _note("slim lamps (pole)", p["x"], p["y"], 0.07)
        props2.append(p)
    added = []
    for x, y in P.LAMPS:
        for _ in range(8):
            if all(math.hypot(tx - x, ty - y) >= 1.3 for tx, ty in trees):
                break
            x, y = x + 0.35, y + 0.14
        if route_distance(x, y) < need_clear(x, y) + 0.7 and P.inside(x, y, P.RING):
            # V15b: too close to the new line: along its row to the nearest free spot, or dropped
            # (V18h: beside the curve plaza first straight towards wing B, to the plaza's edge)
            d = route_distance(x, y)

            def free(qx, qy, gap=8.0):
                return (route_distance(qx, qy) >= need_clear(qx, qy) + 0.7 and P.inside(qx, qy, P.RING)
                        and P.band_ts(qx, qy)[1] >= P.TREE_EDGE_S
                        and all(math.hypot(tx - qx, ty - qy) >= 1.3 for tx, ty in trees)
                        and all(math.hypot(a["x"] - qx, a["y"] - qy) >= gap for a in added)
                        and all(math.hypot(lx - qx, ly - qy) >= gap for lx, ly in P.LAMPS if (lx, ly) != (x, y)))
            spot = _across_row(x, y, lambda qx, qy: free(qx, qy, 6.0)) or _along_row(x, y, free)
            _MOVES.append(f"slim lamp of the tree rows ({x:.1f}, {y:.1f}), {d:.1f} m from the line: "
                          + (f"moved to ({spot[0]:.1f}, {spot[1]:.1f}), {route_distance(*spot):.1f} m" if spot
                             else "dropped, no free spot"))
            if spot:
                x, y = spot
        if route_distance(x, y) < need_clear(x, y) + 0.7 or not P.inside(x, y, P.RING):
            continue
        _note("slim lamps (pole)", x, y, 0.07)
        added.append({"proto": "Prop_LampPlaza", "x": round(x, 3), "y": round(y, 3), "z": round(E.ground_z(x, y) + 0.05, 3),
                      "rot_z": round(math.radians(110.0), 4), "scale": 1.0})
    n_lamps = len(added)
    blocks = P.seat_blocks(route_distance, trees + [(a["x"], a["y"]) for a in added], clear_at=need_clear)
    half = math.hypot(P.SEAT_SIZE[0], P.SEAT_SIZE[1]) / 2
    for k, (x, y, bearing, pushed) in enumerate(blocks):
        _note("seat blocks (nearest corner)", x, y, half)
        if pushed > 0:                                          # V15b: report each block the race line pushes
            _MOVES.append(f"seat block: pushed {pushed:.2f} m across its row (south-south-east, entrance_plan.seat_blocks) "
                          f"to ({x:.1f}, {y:.1f}), {route_distance(x, y):.1f} m from the line")
        added.append({"proto": "Prop_SeatBlockPlaza", "x": x, "y": y, "z": round(E.ground_z(x, y) + 0.06, 3),
                      "rot_z": round(math.radians(90.0 - bearing), 4), "scale": 1.0})
    n_bollards = 0
    for sb in P.BOLLARD_S:                                          # V16: slim steel bollards on the apron line
        x, y = P.band_pt(P.BAND_T[1], sb)
        if route_distance(x, y) < ROUTE_CLEAR + 0.1 or not P.inside(x, y, P.RING) or P.ring_distance(x, y, P.RING) < 0.6:
            continue
        _note("slim bollards", x, y, 0.06)
        added.append({"proto": "Prop_BollardSlim", "x": round(x, 3), "y": round(y, 3), "z": round(E.ground_z(x, y) + 0.06, 3),
                      "rot_z": 0.0, "scale": 1.0})
        n_bollards += 1
    sx, sy = P.RAMP_SW[0] - 1.2, P.RAMP_SW[1] - 1.6                 # a parking sign (no text) at the ramp mouth
    if "Prop_SignParking" in protos:
        _note("parking sign", sx, sy, 0.4)
        added.append({"proto": "Prop_SignParking", "x": round(sx, 3), "y": round(sy, 3), "z": round(E.ground_z(sx, sy) + 0.05, 3),
                      "rot_z": round(math.radians(90.0 - 256.0), 4), "scale": 1.0})
    props2 += added
    n_shrubs = 0                                                    # V18g: the clipped shrubs of B's planted strip
    if "Veg_Shrub" in protos:
        row = P.SHRUB_ROW
        k = 0
        while row["t0"] + k * row["step"] <= row["t1"]:
            x, y = P.band_pt(row["t0"] + k * row["step"], row["s"] + 0.5 * (_hash01(k, 3.0) - 0.5))
            if route_distance(x, y) >= E.KEEP_CLEAR + 1.5:
                veg2.append({"proto": "Veg_Shrub_B" if k % 2 and "Veg_Shrub_B" in protos else "Veg_Shrub", "x": round(x, 3),
                             "y": round(y, 3), "z": round(E.ground_z(x, y), 3), "rot_z": round(1.7 * k, 4),
                             "scale": round(0.85 + 0.3 * _hash01(k, 7.0), 3)})
                n_shrubs += 1
            k += 1
    print(f"  entrance dressing (V18g): {n_shrubs} clipped shrubs on B's planted strip")
    moved = sum(1 for b in blocks if b[3] > 0)
    print(f"  entrance dressing: {swapped} olives became young staked trees, {lamps} campus lamps became slim lamps; "
          f"added {n_lamps} slim lamps, {len(blocks)} seat blocks ({moved} of them moved off the race line), "
          f"{n_bollards} slim bollards on the apron line, {len(added) - n_lamps - len(blocks) - n_bollards} sign")
    n_rez = _rez(veg2, props2)                                      # V18h
    print(f"  entrance dressing (V18h): {n_rez} plants and props set on the raised ground of the curve plaza")
    build_paving(E.collection(COLLECTION), trees)                   # V16: the sheet with a gravel pit under every tree
    for fname, items in (("placements_vegetation.json", veg2), ("placements_props.json", props2)):
        path = os.path.join(E.OUT, fname)
        if not os.path.exists(path):
            continue
        data = json.load(open(path))
        data["placements"] = items
        counts = {}
        for it in items:
            counts[it["proto"]] = counts.get(it["proto"], 0) + 1
        data["counts"] = counts
        json.dump(data, open(path, "w"), indent=1)
    for line in _MOVES:
        print("  entrance, start straight (V15b): " + line)
    report()
    return veg2, props2


def report():
    """Clearance printout: least distance of every new solid kind to the race centreline."""
    for kind, (d, where) in sorted(_CLEAR.items()):
        flag = "" if d >= ROUTE_CLEAR else "   <-- TOO CLOSE"
        print(f"  entrance clearance: {kind}: {d:.2f} m at {where}{flag}")
    if any(d < ROUTE_CLEAR for d, _w in _CLEAR.values()):
        raise RuntimeError(f"entrance: something stands closer than {ROUTE_CLEAR} m to the race centreline")
