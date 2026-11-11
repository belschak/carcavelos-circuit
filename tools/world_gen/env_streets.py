"""Street section (V10f, 2026-11-11): builds street_plan.py, the plan of the avenue, the roundabout, the Avenida Marginal,
the restaurant forecourt at the hairpin and the coastal car park, measured by the Street View survey (atlas report V10e).

Objects (collection World_Streets, exported with CampusWorld.fbx):
  Street_Paving            flat paving draped 7 cm over the terrain: concrete slabs, bay rows (the white bay lines are
                           in the texture), brick bays, granite setts, calcada, asphalt. One mesh; every layer of the
                           plan is cut against the later ones (constrained Delaunay), so nothing overlaps.
                           Nothing lies within KEEP_OUT (4.95 m) of the route centreline: the road and its shoulders are
                           Role 2's (env_track only shows the matching surface on the road); the paving meets the foot
                           of the shoulder level with the terrain and rises to its 7 cm over 3 m.
  Street_NoCollide_Paint   road markings 4 cm above the paving (one palette material, no collider in Unity)
  Street_NoCollide_Kerbs   low raised kerbs (the Marginal's median, island kerbs up to 0.2 m): to look at, karts pass
                           through them instead of being launched (CampusWorldBuilder skips "Street_NoCollide")
  Street_Structures        things with a collider: walls, hedges, the entrance portal and its stair, steel frames ...
  World_GroundTerrace      the sunken restaurant terrace: a 1 m lattice replaces the 5 m terrain cells along its
                           retaining wall (like env_covebank), so the wall is a wall and not a 5 m slope
Props and trees (bollards, masts, signs, parked cars, fan palms ...) are prototypes placed through the dressing files,
so Unity instances them.

Everything keeps PROP_CLEAR (6.6 m) from the centreline; the three real kerbs that lie across the race line (the
Marginal's median, the island tip at the hairpin apex, the sidewalk kerb in A) are open or flush there.
"""
import json
import math
import os

import bpy
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt, tessellate_polygon

import env_common as E
import env_corridor
import env_terrain
import street_plan as S

COLLECTION = "World_Streets"
LIFT = 0.07                # m: paving above the terrain (the road lies 6 cm above its bed)
LIFT_EDGE = 0.004          # ... at the foot of the road's shoulder (which ends level with the terrain): no lip to hit
LIFT_NEAR = 0.035          # ... 1.2 m further out
TAPER = (1.2, 3.2)         # m from the edge at which the paving has reached LIFT_NEAR and LIFT
PAINT_LIFT = 0.04          # m: markings above the paving (2 cm flickered at a distance on the track's edge lines)
GRID = 10.0                # m: the paving is cut along every second terrain grid line, then refined where the ground bends
TILE = {"concrete": 4.0, "setts": 2.0, "calcada": 1.0, "asphalt": 2.0}

# one small palette texture carries every flat colour (markings, kerbs, signs, cars, bollards): one material
PALETTE = S.PALETTE
PALETTE_GRID = 8


# =====================================================================================================
# Materials
# =====================================================================================================
def _textured(name, colour, base, roughness):
    import env_materials
    env_materials.MATTE.add("W_" + name)               # albedo (and normal) only, constant roughness
    mat = E.material(name, colour, roughness)
    if not mat.get("v10f_textured") and env_materials.upgrade(mat, base, roughness):
        mat["v10f_textured"] = 1
    return mat


def mat(key):
    if key == "concrete":
        return _textured("street_concrete", "#CDC4B6", "street_concrete", 0.9)
    if key == "bays":
        return _textured("street_bays", "#CDC4B6", "street_bays", 0.9)
    if key == "brick":
        return _textured("street_brick", "#9C7F6B", "street_brick", 0.9)
    if key == "setts":
        return _textured("street_setts", "#8E8A84", "street_setts", 0.85)
    if key == "calcada":
        import env_track
        return env_track.surface_material("calcada")
    if key == "asphalt":
        # our own material with the track's asphalt texture: Unity imported the track's W_T_asphalt under the name
        # "Lit" when CampusWorld.fbx used it as well (it carries a roughness map), so the two files keep their own
        return _textured("street_asphalt", "#55585E", "asphalt", 0.88)
    if key == "lawn":
        import env_track
        return env_track.surface_material("lawn")
    if key == "rust":
        return _textured("street_rust", "#8E5238", "street_rust", 0.85)
    if key == "hedge":
        return _textured("street_hedge", "#3F4E3C", "street_hedge", 0.95)
    if key == "slats":
        return _textured("street_slats", "#E6DCC8", "street_slats", 0.85)
    if key == "wall_white":
        return E.material("B_wall_white", "#EAEAEC", 0.85)
    if key == "roof":
        return E.material("beach_roof", "#EFEBE1", 0.9)
    if key in ("palette", "kerb") or key.startswith("pal:"):
        return _textured("street_palette", "#B0B0B0", "street_palette", 0.8)
    raise KeyError(key)


def pal_uv(colour):
    """UV of the middle of a palette cell."""
    k = list(PALETTE).index(colour)
    return ((k % PALETTE_GRID + 0.5) / PALETTE_GRID, 1.0 - (k // PALETTE_GRID + 0.5) / PALETTE_GRID)


# =====================================================================================================
# Ground: the height the paving has to clear = the built terrain meshes themselves (ray casts). E.ground_z is the
# analytic surface; in tight bends the corridor mesh fans out between its cross-sections and differs from it by
# decimetres, and on the 5 m grid Unity splits each quad along one diagonal. So every quad goes into the tree with
# both possible splits and a ray from above returns the highest: the paving clears whatever Unity builds.
# =====================================================================================================
_bvh = {"tree": None}


def _ground_tree():
    from mathutils.bvhtree import BVHTree
    verts, tris = [], []
    col = bpy.data.collections.get("World_Ground")
    for obj in (col.objects if col else ()):
        if obj.type != "MESH":
            continue
        base = len(verts)
        verts.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
        for poly in obj.data.polygons:
            idx = [base + i for i in poly.vertices]
            if len(idx) == 3:
                tris.append(idx)
            elif len(idx) == 4:
                a, b, c, d = idx
                tris += [(a, b, c), (a, c, d), (a, b, d), (b, c, d)]
            else:
                tris += [(idx[0], idx[k], idx[k + 1]) for k in range(1, len(idx) - 1)]
    if not tris:
        raise RuntimeError("env_streets.ground: the terrain meshes are not built yet")
    return BVHTree.FromPolygons(verts, tris, epsilon=0.0)


def ground(x, y):
    """Height of the built terrain at (x, y) (the highest surface under the point)."""
    if _bvh["tree"] is None:
        _bvh["tree"] = _ground_tree()
    hit = _bvh["tree"].ray_cast(Vector((x, y, 200.0)), Vector((0.0, 0.0, -1.0)), 400.0)
    return hit[0].z if hit[0] is not None else E.ground_z(x, y)


def lift_at(d):
    """Paving height above the ground at d metres from the route centreline."""
    o = d - S.KEEP_OUT
    if o <= TAPER[0]:
        return LIFT_EDGE + (LIFT_NEAR - LIFT_EDGE) * max(o, 0.0) / TAPER[0]
    t = min((o - TAPER[0]) / (TAPER[1] - TAPER[0]), 1.0)
    return LIFT_NEAR + (LIFT - LIFT_NEAR) * t * t * (3 - 2 * t)


# =====================================================================================================
# Triangle soup tools: split edges, clip against the track's keep-out, follow the ground
# =====================================================================================================
class Soup:
    """Triangles over shared 2-D vertices. tris: [a, b, c, tag]."""

    def __init__(self):
        self.xy, self.tris, self._d = [], [], {}

    def add(self, x, y):
        self.xy.append((x, y))
        return len(self.xy) - 1

    def dist(self, i):
        if i not in self._d:
            self._d[i] = S.route_distance(*self.xy[i])
        return self._d[i]

    def split(self, want):
        """Split every edge for which want(a, b) is true at its middle; triangles are cut to match (no T-joints)."""
        mid = {}

        def m(a, b):
            key = (a, b) if a < b else (b, a)
            if key not in mid:
                mid[key] = self.add((self.xy[a][0] + self.xy[b][0]) / 2, (self.xy[a][1] + self.xy[b][1]) / 2) \
                    if want(a, b) else None
            return mid[key]
        out, any_split = [], False
        for a, b, c, tag in self.tris:
            ab, bc, ca = m(a, b), m(b, c), m(c, a)
            n = (ab is not None) + (bc is not None) + (ca is not None)
            if n == 0:
                out.append((a, b, c, tag))
                continue
            any_split = True
            if n == 3:
                out += [(a, ab, ca, tag), (ab, b, bc, tag), (ca, bc, c, tag), (ab, bc, ca, tag)]
            elif n == 1:
                if ab is not None:
                    out += [(a, ab, c, tag), (ab, b, c, tag)]
                elif bc is not None:
                    out += [(a, b, bc, tag), (a, bc, c, tag)]
                else:
                    out += [(a, b, ca, tag), (ca, b, c, tag)]
            else:
                if ab is None:
                    out += [(a, b, bc, tag), (a, bc, ca, tag), (ca, bc, c, tag)]
                elif bc is None:
                    out += [(a, ab, ca, tag), (ab, b, c, tag), (ab, c, ca, tag)]
                else:
                    out += [(a, ab, c, tag), (ab, bc, c, tag), (ab, b, bc, tag)]
        self.tris = out
        return any_split

    def length(self, a, b):
        return math.hypot(self.xy[a][0] - self.xy[b][0], self.xy[a][1] - self.xy[b][1])

    def refine_near_route(self, max_edge=1.4):
        """Short edges where the keep-out line will cut, so the cut follows the curve of the road."""
        def want(a, b):
            ln = self.length(a, b)
            return ln > max_edge and min(self.dist(a), self.dist(b)) - ln / 2 < S.KEEP_OUT + 0.6
        for _ in range(4):
            if not self.split(want):
                break

    def limit_edges(self, max_edge):
        for _ in range(6):
            if not self.split(lambda a, b: self.length(a, b) > max_edge):
                break

    def clip_route(self):
        """Keep only what lies at least KEEP_OUT from the centreline; cut triangles get exact crossing points."""
        cross = {}

        def crossing(a, b):                          # a inside the keep-out, b outside (or the reverse)
            key = (a, b) if a < b else (b, a)
            if key not in cross:
                (xa, ya), (xb, yb) = self.xy[key[0]], self.xy[key[1]]
                lo, hi = (0.0, 1.0) if self.dist(key[0]) < S.KEEP_OUT else (1.0, 0.0)
                for _ in range(14):                   # lo: inside the keep-out, hi: outside
                    t = (lo + hi) / 2
                    if S.route_distance(xa + (xb - xa) * t, ya + (yb - ya) * t) < S.KEEP_OUT:
                        lo = t
                    else:
                        hi = t
                cross[key] = self.add(xa + (xb - xa) * hi, ya + (yb - ya) * hi)
                self._d[cross[key]] = S.KEEP_OUT
            return cross[key]
        out = []
        for a, b, c, tag in self.tris:
            keep = [self.dist(v) >= S.KEEP_OUT for v in (a, b, c)]
            if all(keep):
                out.append((a, b, c, tag))
                continue
            if not any(keep):
                continue
            poly = []
            ring = (a, b, c)
            for k in range(3):
                p, q = ring[k], ring[(k + 1) % 3]
                if keep[k]:
                    poly.append(p)
                if keep[k] != keep[(k + 1) % 3]:
                    poly.append(crossing(p, q))
            for k in range(1, len(poly) - 1):
                out.append((poly[0], poly[k], poly[k + 1], tag))
        self.tris = out

    def follow_ground(self, zf, up=0.02, down=0.07, passes=5):
        """Split edges whose middle would lie more than `up` under or `down` over the real surface zf(x, y)."""
        z = {}

        def zz(i):
            if i not in z:
                z[i] = zf(*self.xy[i])
            return z[i]

        def want(a, b):
            if self.length(a, b) < 0.9:
                return False
            mx, my = (self.xy[a][0] + self.xy[b][0]) / 2, (self.xy[a][1] + self.xy[b][1]) / 2
            real = zf(mx, my)
            flat = (zz(a) + zz(b)) / 2
            tol = min(up, 0.6 * lift_at(S.route_distance(mx, my)))      # stay above the ground where the lift is small
            return real - flat > tol or flat - real > down
        for _ in range(passes):
            if not self.split(want):
                break


def ccw(ring):
    area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(ring, ring[1:] + ring[:1]))
    return ring if area > 0 else ring[::-1]


def overlay(layers):
    """Constrained Delaunay over all layer outlines and the terrain grid lines -> Soup; tag = index of the topmost
    layer covering the triangle."""
    verts, faces = [], []
    for lay in layers:
        ring = ccw(lay["ring"])
        faces.append(list(range(len(verts), len(verts) + len(ring))))
        verts += [Vector(p) for p in ring]
    xs, ys = [v.x for v in verts], [v.y for v in verts]
    x0, x1 = math.floor(min(xs) / GRID) * GRID, math.ceil(max(xs) / GRID) * GRID
    y0, y1 = math.floor(min(ys) / GRID) * GRID, math.ceil(max(ys) / GRID) * GRID
    edges = []
    gx = x0
    while gx <= x1 + 1e-6:
        edges.append((len(verts), len(verts) + 1))
        verts += [Vector((gx, y0 - 1.0)), Vector((gx, y1 + 1.0))]
        gx += GRID
    gy = y0
    while gy <= y1 + 1e-6:
        edges.append((len(verts), len(verts) + 1))
        verts += [Vector((x0 - 1.0, gy)), Vector((x1 + 1.0, gy))]
        gy += GRID
    out_v, _e, out_f, _ov, _oe, orig = delaunay_2d_cdt(verts, edges, faces, 1, 1e-5, True)
    soup = Soup()
    soup.xy = [(v.x, v.y) for v in out_v]
    for f, o in zip(out_f, orig):
        if o and len(f) == 3:
            soup.tris.append((f[0], f[1], f[2], max(o)))
    return soup


# =====================================================================================================
# Paving and paint
# =====================================================================================================
_paving = {"index": None}


_hulls = []


def _hull_radius(x, y):
    """Layers marked `hull` lie on the hull of the ground within that radius instead of on the ground itself: they
    bridge a seam in the terrain (the inner edge of the track corridor inside the hairpin steps by up to 0.4 m)."""
    if not _hulls:
        for lay in S.LAYERS:
            if lay.get("hull"):
                xs, ys = [q[0] for q in lay["ring"]], [q[1] for q in lay["ring"]]
                _hulls.append((min(xs) - 0.01, min(ys) - 0.01, max(xs) + 0.01, max(ys) + 0.01, lay["ring"], lay["hull"]))
        _hulls.append(None)
    r = 0.0
    for h in _hulls:
        if h and h[0] <= x <= h[2] and h[1] <= y <= h[3] and (S.inside(x, y, h[4]) or S.ring_distance(x, y, h[4]) < 0.02):
            r = max(r, h[5])
    return r


def _paving_z(d, x, y):
    z = ground(x, y)
    r = _hull_radius(x, y)
    if r:
        for k in range(8):
            a = math.pi * k / 4
            for f in (0.5, 1.0):
                px, py = x + math.cos(a) * r * f, y + math.sin(a) * r * f
                if S.route_distance(px, py) >= S.ROAD_HALF + 2.0:          # not the road itself
                    z = max(z, ground(px, py))
    return z + lift_at(d)


def build_paving(col):
    layers = S.LAYERS
    soup = overlay(layers)
    soup.tris = [t for t in soup.tris if layers[t[3]]["mat"] is not None]
    counts = [len(soup.tris)]
    soup.refine_near_route(3.0)
    counts.append(len(soup.tris))
    soup.clip_route()
    counts.append(len(soup.tris))
    soup.follow_ground(lambda x, y: _paving_z(S.route_distance(x, y), x, y))
    counts.append(len(soup.tris))
    worst, where, low = 1.0, None, 0                # proof: how close does the paving come to the ground anywhere?
    for a, b, c, _tag in soup.tris:
        pa, pb, pc = soup.xy[a], soup.xy[b], soup.xy[c]
        za, zb, zc = (_paving_z(soup.dist(v), *soup.xy[v]) for v in (a, b, c))
        for wa, wb, wc in ((1 / 3, 1 / 3, 1 / 3), (0.6, 0.2, 0.2), (0.2, 0.6, 0.2), (0.2, 0.2, 0.6), (0.5, 0.5, 0.0),
                           (0.0, 0.5, 0.5), (0.5, 0.0, 0.5)):
            x, y = wa * pa[0] + wb * pb[0] + wc * pc[0], wa * pa[1] + wb * pb[1] + wc * pc[1]
            gap = wa * za + wb * zb + wc * zc - ground(x, y)
            low += gap < -0.02
            if gap < worst:
                worst, where = gap, (round(x, 1), round(y, 1), layers[_tag]["name"])
    print(f"  streets: least clearance of the paving over the ground (7 samples per triangle): {worst * 100:.1f} cm at "
          f"{where}; {low} samples more than 2 cm under the ground")
    print(f"  streets: paving triangles after overlay / refine at the road / clip / follow the ground: {counts}")
    z = [_paving_z(soup.dist(i), x, y) for i, (x, y) in enumerate(soup.xy)]
    keys = []
    slot_of = {}
    for lay in layers:
        if lay["mat"] is None:
            continue
        key = "palette" if lay["mat"] == "kerb" or lay["mat"].startswith("pal:") else lay["mat"]
        slot_of[lay["mat"]] = key
        if key not in keys:
            keys.append(key)
    used = {}
    verts, faces, slots, uvs = [], [], [], []
    for a, b, c, tag in soup.tris:
        tri = []
        for v in (a, b, c):
            if v not in used:
                used[v] = len(verts)
                verts.append((soup.xy[v][0], soup.xy[v][1], z[v]))
            tri.append(used[v])
        (xa, ya), (xb, yb), (xc, yc) = soup.xy[a], soup.xy[b], soup.xy[c]
        if (xb - xa) * (yc - ya) - (xc - xa) * (yb - ya) < 0:
            tri = tri[::-1]
            a, b, c = c, b, a
        lay = layers[tag]
        faces.append(tri)
        slots.append(keys.index(slot_of[lay["mat"]]))
        uvs.append([_layer_uv(lay, soup.xy[v]) for v in (a, b, c)])
    mesh = bpy.data.meshes.new("Street_Paving")
    mesh.from_pydata(verts, [], faces)
    for k in keys:
        mesh.materials.append(mat(k))
    mesh.polygons.foreach_set("material_index", slots)
    uv = mesh.uv_layers.new(name="UVMap")
    for poly, tri_uv in zip(mesh.polygons, uvs):
        for li, t in zip(poly.loop_indices, tri_uv):
            uv.data[li].uv = t
    mesh.validate()
    mesh.update()
    E.new_object("Street_Paving", mesh, col)
    # index of the built triangles (2-D grid) so the markings can sit exactly on the paving
    index = {}
    for f in faces:
        pts = [verts[i] for i in f]
        for cx in range(int(min(p[0] for p in pts) // 4), int(max(p[0] for p in pts) // 4) + 1):
            for cy in range(int(min(p[1] for p in pts) // 4), int(max(p[1] for p in pts) // 4) + 1):
                index.setdefault((cx, cy), []).append(pts)
    _paving["index"] = index
    area = sum(abs((verts[f[1]][0] - verts[f[0]][0]) * (verts[f[2]][1] - verts[f[0]][1])
                   - (verts[f[2]][0] - verts[f[0]][0]) * (verts[f[1]][1] - verts[f[0]][1])) / 2 for f in faces)
    print(f"  streets: paving {len(faces)} triangles, {area:.0f} m2, {len(keys)} materials ({', '.join(keys)})")


def _layer_uv(lay, p):
    if lay["mat"] == "kerb":
        return pal_uv("kerb")
    if lay["mat"].startswith("pal:"):
        return pal_uv(lay["mat"][4:])
    if lay["uv"]:
        (ox, oy), (ux, uy), su, sv = lay["uv"]
        dx, dy = p[0] - ox, p[1] - oy
        return ((dx * ux + dy * uy) / su, (-dx * uy + dy * ux) / sv)
    t = TILE.get(lay["mat"], 2.0)
    return (p[0] / t, p[1] / t)


def paving_height(x, y):
    """Height of the built paving at (x, y), or None where there is none."""
    for a, b, c in _paving["index"].get((int(x // 4), int(y // 4)), ()):
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(d) < 1e-12:
            continue
        wa = ((b[1] - c[1]) * (x - c[0]) + (c[0] - b[0]) * (y - c[1])) / d
        wb = ((c[1] - a[1]) * (x - c[0]) + (a[0] - c[0]) * (y - c[1])) / d
        wc = 1.0 - wa - wb
        if wa >= -1e-6 and wb >= -1e-6 and wc >= -1e-6:
            return wa * a[2] + wb * b[2] + wc * c[2]
    return None


def _on_island(x, y):
    return any(r["kind"] == "island" and S.inside(x, y, r["ring"]) for r in S.RAISED)


def build_paint(col):
    soup = Soup()
    for k, p in enumerate(S.PAINT):
        ring = ccw(p["ring"])
        if len(ring) < 3:
            continue
        base = len(soup.xy)
        soup.xy += ring
        for tri in tessellate_polygon([[Vector((x, y, 0.0)) for x, y in ring]]):
            soup.tris.append((base + tri[0], base + tri[1], base + tri[2], k))
    soup.limit_edges(9.0)
    soup.refine_near_route(3.2)
    soup.clip_route()
    verts, faces, uvs = [], [], []
    dropped = 0
    for a, b, c, tag in soup.tris:
        pts = []
        for v in (a, b, c):
            x, y = soup.xy[v]
            h = paving_height(x, y)
            if h is None:                             # on the outline of the paving: look a hair inside the triangle
                cx = (soup.xy[a][0] + soup.xy[b][0] + soup.xy[c][0]) / 3
                cy = (soup.xy[a][1] + soup.xy[b][1] + soup.xy[c][1]) / 3
                h = paving_height(x + (cx - x) * 0.05, y + (cy - y) * 0.05)
            pts.append(None if h is None else (x, y, h + PAINT_LIFT))
        cx, cy = sum(soup.xy[v][0] for v in (a, b, c)) / 3, sum(soup.xy[v][1] for v in (a, b, c)) / 3
        if None in pts or _on_island(cx, cy):
            dropped += 1
            continue
        if (pts[1][0] - pts[0][0]) * (pts[2][1] - pts[0][1]) - (pts[2][0] - pts[0][0]) * (pts[1][1] - pts[0][1]) < 0:
            pts.reverse()
        faces.append([len(verts), len(verts) + 1, len(verts) + 2])
        verts += pts
        uvs.append(pal_uv(S.PAINT[tag]["colour"]))
    mesh = bpy.data.meshes.new("Street_Paint")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat("palette"))
    uv = mesh.uv_layers.new(name="UVMap")
    for poly, t in zip(mesh.polygons, uvs):
        for li in poly.loop_indices:
            uv.data[li].uv = t
    mesh.validate()
    mesh.update()
    E.new_object("Street_NoCollide_Paint", mesh, col)
    print(f"  streets: markings {len(faces)} triangles ({dropped} off the paving or in the road's keep-out)")


# =====================================================================================================
# The sunken restaurant terrace (survey 4.2: the restaurants stand 1.5-2 m below the forecourt)
# =====================================================================================================
FINE = 1.25
SUB = int(round(E.STEP / FINE))
REACH = 8.7            # m from the terrace outline to a cell centre
BLEND = 2.0
NODE_INSET = 5.0       # m: 5 m terrain nodes are lowered only this far inside the outline (the lattice does the rest)
SNAP = 0.5             # m: lattice points this close to the outline move onto it (FACE either side): a wall, not a slope
FACE = 0.12
_T = {"cells": set(), "z": {}, "ramp_top": None}
_T_SEGS = [(a, b) for ring in (S.TERRACE, S.SERVICE_RAMP) for a, b in zip(ring, ring[1:] + ring[:1])]
import capricciosa_plan  # noqa: E402  V16g: the sand below the sea wall, the esplanade and the cliff foot join the lattice
_T_SEGS += [(a, b) for ring in capricciosa_plan.TERRAIN_RINGS for a, b in zip(ring, ring[1:] + ring[:1])]


def _outline_distance(x, y):
    return min(S.seg_distance(x, y, a[0], a[1], b[0], b[1]) for a, b in _T_SEGS)


def lower_nodes(heights):
    """env_terrain.apply: the 5 m nodes well inside the terrace go down to its level (never up)."""
    n = 0
    for j in range(E.ROWS):
        for i in range(E.COLS):
            x, y = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            if not (-345 < x < -240 and -95 < y < 10):
                continue
            if S.inside(x, y, S.TERRACE) and not S.inside(x, y, S.SERVICE_RAMP) \
                    and S.ring_distance(x, y, S.TERRACE) >= NODE_INSET and E.distance_to_route(x, y) > 24.0 \
                    and heights[j][i] > S.TERRACE_Z:
                heights[j][i] = S.TERRACE_Z
                n += 1
    print(f"  streets: {n} terrain nodes lowered to the restaurant terrace ({S.TERRACE_Z} m)")


def _target(x, y, coarse):
    r = S.terrace_target(x, y)
    if r is None:
        return coarse
    if r[0] == "ramp":
        return min(coarse, _T["ramp_top"] + (S.TERRACE_Z - _T["ramp_top"]) * r[1])
    return min(coarse, r[1])


def _cell(x, y):
    return int(math.floor((x - E.X0) / E.STEP)), int(math.floor((y - E.Y0) / E.STEP))


def covers_cell(cx, cy):
    return _cell(cx, cy) in _T["cells"]


def _edge_distance(x, y):
    i, j = _cell(x, y)
    best = BLEND
    for di in (-1, 0, 1):
        for dj in (-1, 0, 1):
            if (i + di, j + dj) in _T["cells"]:
                continue
            x0, y0 = E.X0 + (i + di) * E.STEP, E.Y0 + (j + dj) * E.STEP
            dx = max(x0 - x, 0.0, x - (x0 + E.STEP))
            dy = max(y0 - y, 0.0, y - (y0 + E.STEP))
            best = min(best, math.hypot(dx, dy))
    return best


def _terrace_z(x, y, blend_at=None):
    coarse = E.coarse_ground_z(x, y)
    bx, by = blend_at or (x, y)
    t = env_corridor.smoothstep(_edge_distance(bx, by) / BLEND)
    return coarse + t * (_target(x, y, coarse) - coarse)


def plan_terrace():
    """Choose the lattice cells and install the terrace surface into E.ground_z (after env_covebank.plan)."""
    import env_covebank
    top = S.SERVICE_RAMP[0], S.SERVICE_RAMP[1]
    _T["ramp_top"] = E.coarse_ground_z((top[0][0] + top[1][0]) / 2, (top[0][1] + top[1][1]) / 2)
    cand = set()
    for j in range(E.ROWS - 1):
        for i in range(E.COLS - 1):
            x0, y0 = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            cx, cy = x0 + E.STEP / 2, y0 + E.STEP / 2
            if not (-345 < cx < -240 and -106 < cy < 10) or _outline_distance(cx, cy) > REACH:   # V16g: -95 -> -106
                continue
            if env_corridor.covers_cell(cx, cy) or env_covebank._near_corridor(x0, y0) or env_covebank.covers_cell(cx, cy):
                continue
            cand.add((i, j))
    _T["cells"] = cand
    changing = set()
    for i, j in cand:
        x0, y0 = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
        if any(abs(_target(x0 + u, y0 + v, E.coarse_ground_z(x0 + u, y0 + v)) - E.coarse_ground_z(x0 + u, y0 + v)) > 0.03
               for u in (0.4, 1.3, 2.5, 3.7, 4.6) for v in (0.4, 1.3, 2.5, 3.7, 4.6)):
            changing.add((i, j))
    _T["cells"] = {(i, j) for i, j in cand if any((i + di, j + dj) in changing for di in (-1, 0, 1) for dj in (-1, 0, 1))}
    _T["z"] = {}
    prev = E._surface["fn"] or E.coarse_ground_z

    def surface(x, y):
        return _terrace_z(x, y) if _cell(x, y) in _T["cells"] else prev(x, y)
    E.set_surface(surface)
    print(f"  streets: restaurant terrace, {len(_T['cells'])} cells of the 5 m grid replaced by a {FINE} m lattice "
          f"({len(changing)} change), ramp top {_T['ramp_top']:.2f} m")


def _on_edge(a, b):
    if a % SUB and b % SUB:
        return False
    i, j = a // SUB, b // SUB
    cells = {(i - (a % SUB == 0 and di), j - (b % SUB == 0 and dj)) for di in (0, 1) for dj in (0, 1)}
    return not all(c in _T["cells"] for c in cells)


def _lattice(a, b):
    key = (a, b)
    if key in _T["z"]:
        return _T["z"][key]
    x, y = E.X0 + a * FINE, E.Y0 + b * FINE
    if _on_edge(a, b):
        out = (x, y, E.coarse_ground_z(x, y))
    else:
        px, py = x, y
        best, foot = SNAP, None
        for p, q in _T_SEGS:
            dx, dy = q[0] - p[0], q[1] - p[1]
            l2 = dx * dx + dy * dy
            t = max(0.0, min(1.0, ((x - p[0]) * dx + (y - p[1]) * dy) / l2))
            fx, fy = p[0] + dx * t, p[1] + dy * t
            d = math.hypot(x - fx, y - fy)
            if d < best:
                best, foot = d, (fx, fy, -dy / math.sqrt(l2), dx / math.sqrt(l2))
        if foot:
            low = S.terrace_zone(x, y)                 # V16g: zones, not just in / out (terrace | sand below its wall)
            for sign in (1.0, -1.0):
                qx, qy = foot[0] + foot[2] * FACE * sign, foot[1] + foot[3] * FACE * sign
                if S.terrace_zone(qx, qy) == low:
                    px, py = qx, qy
                    break
        out = (px, py, _terrace_z(px, py, blend_at=(x, y)))
    _T["z"][key] = out
    return out


def build_terrace_mesh():
    verts, index, faces = [], {}, []

    def vid(a, b):
        if (a, b) not in index:
            index[(a, b)] = len(verts)
            verts.append(_lattice(a, b))
        return index[(a, b)]
    for i, j in sorted(_T["cells"]):
        for a in range(i * SUB, (i + 1) * SUB):
            for b in range(j * SUB, (j + 1) * SUB):
                faces.append((vid(a, b), vid(a + 1, b), vid(a + 1, b + 1), vid(a, b + 1)))
    if not faces:
        return
    mesh = bpy.data.meshes.new("GroundTerrace")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(E.material("ground_map", "#A6AA82", roughness=0.95))
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        co = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = ((co.x - E.X0) / (E.X1 - E.X0), (co.y - E.Y0) / (E.Y1 - E.Y0))
    E.new_object("World_GroundTerrace", mesh, E.collection("World_Ground"))
    print(f"  streets: terrace lattice {len(faces)} quads ({2 * len(faces)} triangles)")


# =====================================================================================================
# Build
# =====================================================================================================
def build():
    col = E.collection(COLLECTION)
    build_paving(col)
    build_paint(col)
    import env_street_objects
    env_street_objects.build(col)
