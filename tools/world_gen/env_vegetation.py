"""Vegetation for the Carcavelos campus world: low-poly plant prototypes and the rules for where they grow.

Run inside Blender 5.2 (headless works), for example from another script:

    import sys
    sys.path.insert(0, "<this folder>")   # the folder with env_common.py
    import env_vegetation as V
    protos = V.build_prototypes()               # {name: object} in collection Proto_Vegetation
    placements = V.place()                      # list of dicts, also written to out/placements_vegetation.json
    V.instance_for_preview(placements, protos)  # linked duplicates in World_Vegetation, only for renders

Prototypes: one mesh object each, origin at the base centre, z up, metres, triangulated. Shrubs, grass and olives
are flat shaded with 2 flat colours (trunk + foliage + highlight). Soft-look test (2026-11-10): the
umbrella pine, the palm and the olive are smooth shaded all over (custom normals) and use ONE material per tree
with a small colour ramp texture (textures/veg_ramp_*_albedo.png, written here; foliage and bark column): every
vertex picks its shade through its UV (dark underneath and in the creases between clumps, light on top; trunks
light at the base, dark under the crown), see ramp_material() and soft_crown(). Trunks reach 0.4 m below z = 0, so a plant on a slope never floats.
Variety per instance comes from rotation, scale and the "_B" colour/shape variant of each plant.

Placement entries (the game instances from out/placements_vegetation.json, key "placements"):
    {"proto": "Veg_Palm", "x": .., "y": .., "z": .., "rot_z": .., "scale": ..}
World metres in the frame of route.json (x east, y north, z up); rot_z in radians, counter-clockwise seen from
above, about the prototype origin; scale is uniform. z is the terrain height at call time (E.ground_z), so call
place() after the terrain is shaped (env_terrain.apply(), then env_corridor.build_data(), as build_world.py does).
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import coast  # noqa: E402
import env_common as E  # noqa: E402

PROTO_COLLECTION = "Proto_Vegetation"
WORLD_COLLECTION = "World_Vegetation"
PLACEMENTS_FILE = os.path.join(E.OUT, "placements_vegetation.json")
SEED = 20261108

# Hard triangle limits per plant type (both colour variants of a type share the limit).
TRI_BUDGET = {"Veg_UmbrellaPine": 1700, "Veg_Palm": 700, "Veg_Olive": 1150, "Veg_Shrub": 120, "Veg_Grass": 80,
              "Veg_PlazaTree": 520}          # V15a: the young staked tree of the entrance plaza

# Per plant type: crown radius at scale 1 (m, widest reach of the foliage from the trunk axis, measured on the
# prototypes and checked by test_env_assets.py; used for the keep-clear rules),
# spacing radius (m, how much room it claims from other plants) and the random scale range.
KIND = {
    "pine": dict(protos=("Veg_UmbrellaPine", "Veg_UmbrellaPine_B"), crown=5.0, space=3.4, scale=(0.86, 1.12)),
    "palm": dict(protos=("Veg_Palm", "Veg_Palm_B"), crown=5.6, space=2.4, scale=(0.88, 1.12)),
    "olive": dict(protos=("Veg_Olive", "Veg_Olive_B"), crown=2.75, space=2.1, scale=(0.85, 1.15)),
    "shrub": dict(protos=("Veg_Shrub", "Veg_Shrub_B"), crown=1.1, space=0.62, scale=(0.85, 1.25)),
    "grass": dict(protos=("Veg_Grass",), crown=1.2, space=0.55, scale=(0.8, 1.2)),
    # V15a: young staked tree of the entrance plaza (placed only by env_entrance.dress, never by place())
    "plaza": dict(protos=("Veg_PlazaTree", "Veg_PlazaTree_B"), crown=1.25, space=1.0, scale=(0.85, 1.15)),
}
TREE_KINDS = ("pine", "palm", "olive")
MAX_TREES = 900
MAX_LOW = 600                      # shrubs + grass clumps

# Placement rules (metres)
BUILDING_GAP = 2.0                 # never inside or within 2 m of a building footprint
ROAD_GAP = 1.0                     # trunk at least width/2 + 1 m from a road centreline
FOOT_GAP = 0.5                     # generated plants stay off footways too (real OSM trees may stand on them)
PALM_NEAR_PRIMARY = 15.0           # OSM tree this close to the Avenida Marginal / its links -> palm
PALM_COAST_Z = 6.0                 # ... or this low (terrain height) and within COAST_NEAR of the coastline
COAST_NEAR = 120.0
CAMPUS_NEAR = 35.0                 # OSM tree this close to a campus building -> olive
WOOD_AREA_PER_PINE = 250.0         # m^2 of "wood" ground per scattered umbrella pine
PALM_SPACING = 14.0                # palm rows along the Marginal and the promenade (12-16 m)
NUDGE_RADII = (1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0)   # an OSM tree that breaks a rule moves to the nearest
                                                          # spot that fits (at most 8 m)
REPORT = {}                                # filled by place(): OSM trees kept / moved / dropped
CLEARINGS = (((-75.0, -148.0), 9.0),)   # open terraces kept free of invented plants: the beach bar (parasols)
# V10b beach pass (2026-11-10): no invented plants on the restaurant terraces, the straw-parasol rows, the cove sand
# (photos K03-K07: open sand) and the pavilion terrace
BEACH_CLEARINGS = (((-290.2, -60.5), 15.0), ((-309.0, -46.0), 9.0), ((-343.6, -32.5), 20.0), ((-322.0, -30.0), 6.0),
                   ((-66.0, -182.0), 26.0), ((-46.0, -147.0), 6.0))      # checked for the pampas clumps (step 5)
LOW_PINE_AREA = (-140.0, -152.0, -96.0, -130.0)   # x0, y0, x1, y1: the lawn islands by the promenade (K02): the real
LOW_PINE_SCALE = (0.52, 0.62)                    # trees there are low, wind-shaped pines, not palms
SAND_EDGE_CLEAR = 1.2                            # V10d: m a dune clump keeps from the beach's backshore line
DECK_PALM_SCALE = (0.68, 0.78)                   # palms on Capricciosa's terrace (env_beachfront.deck_spots)
ROAD_TYPES = {"primary", "primary_link", "secondary", "tertiary", "residential", "unclassified", "service",
              "living_street"}
FOOT_TYPES = {"footway", "pedestrian", "steps", "path", "cycleway"}


# =====================================================================================================
# Geometry helpers
# =====================================================================================================
class MeshBuilder:
    """Collects vertices and faces (each face with a material slot) and turns them into one clean mesh."""

    def __init__(self):
        self.verts, self.faces, self.mats = [], [], []
        self.normals = {}                  # vertex index -> custom normal (smooth "puffy" foliage), see v()
        self.uvs = {}                      # vertex index -> (u, v) for ramp-textured foliage
        self.twins = {}                    # vertex index -> twin indices: other normal + UV at the same point
        self.kept = set()                  # faces whose winding must not be recalculated, see f()
        self.flutter = {}                  # vertex index -> flutter weight 0..1 (leaf tips), see sway_layer()

    def twin(self, i, normal, uv):
        """A second normal + UV for vertex i (e.g. the dark underside of a leaf edge). Each face corner at i uses
        the variant whose normal agrees best with the face, so the mesh stays one closed body."""
        self.twins.setdefault(i, []).append(self.v(self.verts[i], normal, uv))

    def v(self, co, normal=None, uv=None):
        """New vertex. With `normal` it belongs to a smooth surface: every face whose corners all carry a normal
        is shaded smooth with these normals (spherical crown normals); all other faces stay flat."""
        self.verts.append(Vector((co[0], co[1], co[2])))
        if normal is not None:
            self.normals[len(self.verts) - 1] = Vector(normal).normalized()
        if uv is not None:
            self.uvs[len(self.verts) - 1] = (uv[0], uv[1])
        return len(self.verts) - 1

    def f(self, idx, mat, keep=False):
        """New face. keep: an open sheet drawn from both sides (palm frond); its winding stays as given."""
        if keep:
            self.kept.add(len(self.faces))
        self.faces.append(tuple(idx))
        self.mats.append(mat)

    def build(self, name, materials, col, sway=False):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        for m in materials:
            mesh.materials.append(m)
        mesh.polygons.foreach_set("material_index", self.mats)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bm.faces.index_update()
        bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f.index not in self.kept])   # islands point outwards
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
        bm.verts.index_update()
        keep = [v.index for v in bm.verts if v.link_faces]             # twins have no faces of their own
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        origin = keep                                                  # new vertex order -> builder index
        bm.to_mesh(mesh)
        bm.free()
        smooth_vert = [origin[k] in self.normals for k in range(len(origin))]
        for poly in mesh.polygons:                                       # flat shading, faceted look ...
            poly.use_smooth = bool(self.normals) and all(smooth_vert[k] for k in poly.vertices)   # ... or puffy
        mesh.validate()
        if self.normals:
            loops, corner = [], {}
            for poly in mesh.polygons:
                for li in poly.loop_indices:
                    vi = origin[mesh.loops[li].vertex_index]
                    if vi in self.twins:
                        vi = max([vi] + self.twins[vi], key=lambda k: self.normals[k].dot(poly.normal))
                    corner[li] = vi
                    loops.append(tuple(self.normals[vi]) if poly.use_smooth else tuple(poly.normal))
            mesh.normals_split_custom_set(loops)
        if self.uvs:
            layer = mesh.uv_layers.new(name="UVMap")
            for loop in mesh.loops:
                vi = corner.get(loop.index, origin[loop.vertex_index]) if self.normals else origin[loop.vertex_index]
                layer.data[loop.index].uv = self.uvs.get(vi, (0.5, 0.5))
        if sway:
            self.sway_layer(mesh, origin, corner if self.normals else {})
        mesh.update()
        return E.new_object(name, mesh, col)

    def sway_layer(self, mesh, origin, corner):
        """Second UV layer "Sway" for the wind (Foliage.shader): u = bend weight, 0 at the ground and 1 at the top of
        the plant (height ^ 1.5, so the trunk base stands still); v = flutter weight from self.flutter (0 at the base
        of a frond, 1 at its tip). Baked into the mesh because Unity merges the static trees into world-space batches,
        where a shader no longer knows a tree's own height or origin."""
        top = max(v.co.z for v in mesh.vertices)
        layer = mesh.uv_layers.new(name="Sway")
        for loop in mesh.loops:
            z = mesh.vertices[loop.vertex_index].co.z
            vi = corner.get(loop.index, origin[loop.vertex_index])
            layer.data[loop.index].uv = (min(max(z / top, 0.0), 1.0) ** 1.5, self.flutter.get(vi, 0.0))


def frames(path):
    """Tangent + two normals at every point of a polyline (parallel transport, so tubes don't twist)."""
    pts = [Vector(p) for p in path]
    tangents = [(pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized() for k in range(len(pts))]
    ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.95 else Vector((1, 0, 0))
    normal = ref.cross(tangents[0]).normalized()
    out = []
    for t in tangents:
        normal = (normal - t * normal.dot(t)).normalized()
        out.append((t, normal, t.cross(normal)))
    return pts, out


def tube(mb, path, radii, sides, mats, phases=None, wobble=None, tip=None, cap_end=False, start=None):
    """Tube along a polyline. mats: one material per band (or a single int); phases: ring rotation per point
    (alternating half steps give the diamond facets of a palm trunk); wobble[k][j]: radius factor per vertex;
    tip: close the end with a cone to this point; start: begin with a cone from this point (else the start
    stays open, because it is buried in the ground or hidden inside another part)."""
    pts, fr = frames(path)
    rings = []
    for k, (p, (_t, n, b)) in enumerate(zip(pts, fr)):
        ring = []
        for j in range(sides):
            a = (phases[k] if phases else 0.0) + 2 * math.pi * j / sides
            r = radii[k] * (wobble[k][j] if wobble else 1.0)
            ring.append(mb.v(p + (n * math.cos(a) + b * math.sin(a)) * r))
        rings.append(ring)
    for k, (r0, r1) in enumerate(zip(rings, rings[1:])):
        m = mats[k] if isinstance(mats, (list, tuple)) else mats
        for j in range(sides):
            mb.f((r0[j], r0[(j + 1) % sides], r1[(j + 1) % sides], r1[j]), m)
    first = mats[0] if isinstance(mats, (list, tuple)) else mats
    if start is not None:
        c = mb.v(start)
        for j in range(sides):
            mb.f((rings[0][(j + 1) % sides], rings[0][j], c), first)
    last = mats[-1] if isinstance(mats, (list, tuple)) else mats
    if tip is not None:
        c = mb.v(tip)
        for j in range(sides):
            mb.f((rings[-1][j], rings[-1][(j + 1) % sides], c), last)
    elif cap_end:
        mb.f(rings[-1], last)
    return rings


_ICO = {}


def ico(subdiv):
    """Unit icosphere (vertices, faces), cached. subdivisions=1 -> 20 faces, 2 -> 80 faces."""
    if subdiv not in _ICO:
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
        bm.verts.index_update()
        _ICO[subdiv] = ([tuple(v.co) for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces])
        bm.free()
    return _ICO[subdiv]


def face_normal(mb, idx):
    a, b, c = (mb.verts[i] for i in idx[:3])
    return (b - a).cross(c - a).normalized()


def blob(mb, centre, radii, subdiv, rng, mat_for_normal, jitter=0.12, yaw=0.0, floor=None):
    """Lumpy foliage clump: an icosphere squashed to `radii`, every vertex pushed in or out a little.
    floor: flattens the underside at this fraction of the vertical radius (e.g. -0.5)."""
    verts, faces = ico(subdiv)
    cy, sy = math.cos(yaw), math.sin(yaw)
    idx = []
    for x, y, z in verts:
        k = 1.0 + rng.uniform(-jitter, jitter)
        x, y, z = x * radii[0] * k, y * radii[1] * k, z * radii[2] * k
        if floor is not None:
            z = max(z, floor * radii[2])
        idx.append(mb.v((centre[0] + x * cy - y * sy, centre[1] + x * sy + y * cy, centre[2] + z)))
    for f in faces:
        face = [idx[i] for i in f]
        mb.f(face, mat_for_normal(face_normal(mb, face)))


def pad(mb, centre, rx, ry, top, bottom, n, rng, side_mat, top_mat, scallop=0.1, turn=0.0):
    """Flat foliage pad of an umbrella pine: scalloped rim, domed top, shallow cone underneath (4n triangles)."""
    cx, cy, cz = centre
    rim, upper = [], []
    for j in range(n):
        a = turn + 2 * math.pi * j / n
        s = 1.0 + (scallop if j % 2 == 0 else -scallop) + rng.uniform(-0.05, 0.05)
        rim.append(mb.v((cx + math.cos(a) * rx * s, cy + math.sin(a) * ry * s, cz + rng.uniform(-0.12, 0.12))))
        a2 = a + math.pi / n
        s2 = 0.64 + rng.uniform(-0.06, 0.06)
        upper.append(mb.v((cx + math.cos(a2) * rx * s2, cy + math.sin(a2) * ry * s2,
                           cz + top * (0.72 + rng.uniform(-0.08, 0.08)))))
    crown = mb.v((cx + rng.uniform(-0.25, 0.25), cy + rng.uniform(-0.25, 0.25), cz + top))
    under = mb.v((cx, cy, cz - bottom))
    for j in range(n):
        j1 = (j + 1) % n
        mb.f((rim[j], rim[j1], upper[j]), side_mat)
        mb.f((upper[j], rim[j1], upper[j1]), side_mat)
        mb.f((upper[j], upper[j1], crown), top_mat)
        mb.f((rim[j1], rim[j], under), side_mat)


# =====================================================================================================
# Prototypes
# =====================================================================================================
def puff_lobe(mb, centre, radii, rng, mat_for, crown_centre, yaw=0.0, jitter=0.05, floor=-0.55, blend=0.3, subdiv=2):
    """Puffy foliage lobe (design reference: round, cloud-like crowns): an 80-triangle icosphere squashed to
    `radii`, the underside flattened at `floor` (fraction of the vertical radius), shaded smooth with custom
    normals = (1 - blend) x the lobe's own ellipsoid normal + blend x the direction from the crown centre, so
    the overlapping lobes light up like one soft canopy. mat_for(z): material for a face whose centre sits at
    height z (unit-sphere units, -1 bottom .. 1 top) on the lobe: light tops, darker sides and underside.
    subdiv 1 = 20 triangles (flower heads, small bushes)."""
    verts, faces = ico(subdiv)
    cy, sy = math.cos(yaw), math.sin(yaw)
    cc = Vector(crown_centre)
    idx = []
    for x, y, z in verts:
        k = 1.0 + rng.uniform(-jitter, jitter)
        lx, ly, lz = x * radii[0] * k, y * radii[1] * k, max(z, floor) * radii[2] * k
        p = Vector((centre[0] + lx * cy - ly * sy, centre[1] + lx * sy + ly * cy, centre[2] + lz))
        nx, ny, nz = x / radii[0], y / radii[1], z / radii[2]
        if z < floor:                                    # flat underside: mostly down, a little outwards
            nx, ny, nz = nx * 0.35, ny * 0.35, -1.0 / radii[2]
        n_lobe = Vector((nx * cy - ny * sy, nx * sy + ny * cy, nz)).normalized()
        n_crown = (p - cc).normalized() if (p - cc).length > 1e-6 else n_lobe
        idx.append(mb.v(p, n_lobe * (1.0 - blend) + n_crown * blend))
    for f in faces:
        zc = sum(verts[i][2] for i in f) / 3.0
        mb.f([idx[i] for i in f], mat_for(zc))


# ---- soft look (test 2026-11-10): smooth clumps, round trunks, one ramp-shaded material per tree --------------------
# Per tree variant: foliage ramp from deep shade (0) to sunlit (1) as (position, hex), and the bark colour.
RAMPS = {
    "pine": (((0.0, "#30533F"), (0.35, "#4B794D"), (0.7, "#76A35C"), (1.0, "#B4CB7C")), "#7A5238"),
    "pine_b": (((0.0, "#2C4D3B"), (0.35, "#447048"), (0.7, "#6A9854"), (1.0, "#A6C172")), "#6B4F3A"),
    "palm": (((0.0, "#2C4A2A"), (0.35, "#4A6E33"), (0.7, "#6B9A42"), (1.0, "#B2CF68")), "#9A8262"),
    "palm_b": (((0.0, "#31502D"), (0.35, "#557538"), (0.7, "#789F47"), (1.0, "#BCD470")), "#9C8468"),
    "olive": (((0.0, "#465C4A"), (0.35, "#687F5C"), (0.7, "#96A77C"), (1.0, "#CCD3A6")), "#7D7366"),
    "olive_b": (((0.0, "#42594A"), (0.35, "#638255"), (0.7, "#92AA74"), (1.0, "#C8D69E")), "#8A7D6B"),
    # V15a: young plaza tree, a lighter and fresher green; the "bark" column is pale timber (stakes light, trunk dark)
    "plaza": (((0.0, "#4C7340"), (0.35, "#6F9C52"), (0.7, "#9CC468"), (1.0, "#D6E690")), "#A9814F"),
}
RAMP_SIZE = (32, 64)
U_LEAF, U_BARK = 0.25, 0.75                # the ramp texture has two columns: foliage (left) and bark (right)


FOLIAGE_SHADER = "CarcavelosCircuit/Foliage"     # Assets/_Project/Visuals/Shaders/Foliage.shader (URP Lit if missing)
WIND = {"palm": (0.22, 0.28), "pine": (0.05, 0.04), "olive": (0.03, 0.035),
        "plaza": (0.05, 0.04)}                                      # V15a: thin young tree   # metres: bend at the top, flutter at the tips


def set_wind(mat, kind):
    """Marks a tree material for the wind shader: export_unity_bundle.py writes it as the "wind" block of
    materials.json. All materials of one tree get the same numbers, or trunk and leaves would drift apart."""
    mat["wind_shader"], (mat["wind_bend"], mat["wind_flutter"]) = FOLIAGE_SHADER, WIND[kind]


def _rgb(hexc):
    return [int(hexc[i:i + 2], 16) / 255 for i in (1, 3, 5)]


def _ramp_colour(stops, t):
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t0 <= t <= t1:
            k = (t - t0) / (t1 - t0)
            return [a + (b - a) * k for a, b in zip(_rgb(c0), _rgb(c1))]
    return _rgb(stops[-1][1])


def ramp_material(key):
    """The one material 'W_tree_<key>' of a soft tree. Its base colour is a small ramp texture (32 x 64 px), saved
    as textures/veg_ramp_<key>_albedo.png so export_unity_bundle.py ships it like any other albedo: the left half
    is the foliage ramp, the right half the bark ramp (bark colour: 50 % darker at v = 0, lighter at v = 1). Every
    vertex picks column and shade through its UV: a gradient without vertex colours (URP Lit ignores those), and
    the whole tree is one draw call."""
    leaf, bark_hex = RAMPS[key]
    bark = _rgb(bark_hex)
    bark_stops = ((0.0, [c * 0.5 for c in bark]), (0.55, bark), (1.0, [c + (1.0 - c) * 0.28 for c in bark]))
    mat = E.material("tree_" + key, leaf[1][1], roughness=0.9)
    set_wind(mat, key.removesuffix("_b"))
    w, h = RAMP_SIZE
    pixels = []
    for y in range(h):
        t = y / (h - 1)
        a = _ramp_colour(leaf, t)
        for (t0, c0), (t1, c1) in zip(bark_stops, bark_stops[1:]):
            if t0 <= t <= t1:
                b = [p + (q - p) * (t - t0) / (t1 - t0) for p, q in zip(c0, c1)]
        for x in range(w):
            pixels += [*(a if x < w // 2 else b), 1.0]
    os.makedirs(E.TEXTURES, exist_ok=True)
    img = bpy.data.images.new(f"veg_ramp_{key}", w, h, alpha=False)        # byte image: values are sRGB as written
    img.pixels = pixels
    img.filepath_raw, img.file_format = os.path.join(E.TEXTURES, f"veg_ramp_{key}_albedo.png"), "PNG"
    img.save()
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    if not any(n.type == "TEX_IMAGE" for n in nodes):
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = img
        tex.extension = "EXTEND"
        links.new(tex.outputs["Color"], nodes.get("Principled BSDF").inputs["Base Color"])
    return mat


def _shade(s):
    return min(max(s, 0.04), 0.96)


_GEO = {}


def geo(freq):
    """Unit geodesic sphere (vertices, faces): an icosahedron with every edge cut into `freq` parts, 20 x freq^2
    triangles (2 -> 80, 3 -> 180, 4 -> 320). Finer steps than the power-of-two icosphere, so a clump can be round
    enough for its size without wasting triangles."""
    if freq not in _GEO:
        base_v, base_f = ico(1)
        verts, index, faces = [], {}, []

        def vid(p):
            p = p.normalized()
            key = (round(p.x, 5), round(p.y, 5), round(p.z, 5))
            if key not in index:
                index[key] = len(verts)
                verts.append(tuple(p))
            return index[key]

        for fa, fb, fc in base_f:
            a, b, c = Vector(base_v[fa]), Vector(base_v[fb]), Vector(base_v[fc])
            grid = {(i, j): vid((a * (freq - i - j) + b * i + c * j) / freq)
                    for i in range(freq + 1) for j in range(freq + 1 - i)}
            for i in range(freq):
                for j in range(freq - i):
                    faces.append((grid[i, j], grid[i + 1, j], grid[i, j + 1]))
                    if i + j < freq - 1:
                        faces.append((grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
        _GEO[freq] = (verts, faces)
    return _GEO[freq]


def soft_crown(mb, lobes, mat, crown_centre, z_range, blend=0.35, base=0.34, under=0.15, crease=0.16, reach=4.5):
    """Fluffy crown made of smooth clumps. lobes: dicts with c (centre), r (rx, ry, rz), yaw, freq (see geo()),
    floor (unit height below which the underside is squashed to a soft, nearly flat belly) and phase (slow wobble
    so no clump is a perfect ellipsoid). Smooth shading with custom normals = the clump's own normal mixed with the
    direction from the crown centre (`blend`), so light wraps around the whole crown, not around every clump.
    Shade per vertex (UV v into the foliage ramp of ramp_material): `base`, plus height in the crown and facing
    up = lighter; facing down = darker (`under`); close to or inside a neighbouring clump = darker (`crease`: the
    gaps between clumps read as soft shadow)."""
    cc = Vector(crown_centre)
    z_lo, z_hi = z_range

    def local(lobe, p):
        d = p - lobe["c"]
        cy, sy = math.cos(lobe["yaw"]), math.sin(lobe["yaw"])
        rx, ry, rz = lobe["r"]
        return math.sqrt(((d.x * cy + d.y * sy) / rx) ** 2 + ((-d.x * sy + d.y * cy) / ry) ** 2 + (d.z / rz) ** 2)

    for lobe in lobes:
        verts, faces = geo(lobe["freq"])
        rx, ry, rz = lobe["r"]
        cy, sy = math.cos(lobe["yaw"]), math.sin(lobe["yaw"])
        floor, ph = lobe.get("floor", -0.45), lobe.get("phase", 0.0)
        idx = []
        for x, y, z in verts:
            k = 1.0 + 0.05 * math.sin(3.1 * x + ph) * math.sin(2.7 * y + 1.7 * ph) + 0.035 * math.sin(4.3 * z + 2.3 * ph)
            belly = min(max((floor - z) / 0.35, 0.0), 1.0)                 # 0 above the belly line, 1 well below it
            zz = z if z >= floor else floor + (z - floor) * 0.35
            lx, ly, lz = x * rx * k, y * ry * k, zz * rz * k
            p = Vector((lobe["c"][0] + lx * cy - ly * sy, lobe["c"][1] + lx * sy + ly * cy, lobe["c"][2] + lz))
            n = Vector((x / rx, y / ry, z / rz)).normalized()
            n = (n * (1.0 - belly) + Vector((n.x * 0.4, n.y * 0.4, -1.0)).normalized() * belly).normalized()
            n = Vector((n.x * cy - n.y * sy, n.x * sy + n.y * cy, n.z))
            out = (p - cc).normalized() if (p - cc).length > 1e-6 else n
            n = (n * (1.0 - blend) + out * blend).normalized()
            h = min(max((p.z - z_lo) / (z_hi - z_lo), 0.0), 1.0)
            shade = base + 0.36 * h + 0.3 * max(n.z, 0.0) - under * max(-n.z, 0.0)
            for other in lobes:
                if other is not lobe:
                    shade -= crease * min(max((1.3 - local(other, p)) / 0.3, 0.0), 1.0)
            idx.append(mb.v(p, n, (U_LEAF, _shade(shade))))
            mb.flutter[idx[-1]] = min((p - cc).length / reach, 1.0)       # wind: the outer leaves move most
        for f in faces:
            mb.f([idx[i] for i in f], mat)


def soft_tube(mb, path, radii, sides, mat, tip=None, shade=None, knots=None):
    """Round trunk or branch: a tube along `path`, shaded smooth with normals pointing straight out from the
    centre line, closed with a point at `tip`. The start stays open (buried in the ground or inside the trunk).
    shade: bark ramp value per ring (light at the base, dark under the crown). knots(k, angle): radius factor per
    vertex for a gnarled trunk (smooth in the angle, so it stays soft)."""
    pts, fr = frames(path)
    rings = []
    for k, (p, (_t, n, b), r) in enumerate(zip(pts, fr, radii)):
        ring = []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            d = n * math.cos(a) + b * math.sin(a)
            uv = (U_BARK, _shade(shade[k])) if shade else None
            ring.append(mb.v(p + d * (r * (knots(k, a) if knots else 1.0)), d, uv))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for j in range(sides):
            mb.f((r0[j], r0[(j + 1) % sides], r1[(j + 1) % sides], r1[j]), mat)
    if tip is not None:
        c = mb.v(tip, Vector(tip) - pts[-1], (U_BARK, _shade(shade[-1])) if shade else None)
        for j in range(sides):
            mb.f((rings[-1][j], rings[-1][(j + 1) % sides], c), mat)
    return rings


def umbrella_pine(name, seed, mat, col):
    """Stone pine, soft look (test 2026-11-10, animated-film reference): a round, tapering trunk with a lean and an
    S-bend, three round limbs, and a wide, fairly flat umbrella of ten smooth clumps: one big centre clump, five
    along the rim and four low ones on top. One ramp-shaded material (soft_crown). About 8.8 m tall, 9.5 m across."""
    rng = random.Random(seed)
    mb = MeshBuilder()
    lean = rng.uniform(0.35, 0.55)
    bend = rng.uniform(0.14, 0.24) * rng.choice((-1, 1))

    def trunk_at(z):
        t = max(z, 0.0) / 6.0
        return Vector((lean * t ** 1.4, bend * math.sin(t * math.pi), z))

    zs = [-0.4, 0.15, 0.8, 1.9, 3.1, 4.3, 5.3, 6.1]
    radii = [0.52, 0.4, 0.33, 0.3, 0.275, 0.25, 0.22, 0.17]
    soft_tube(mb, [trunk_at(z) for z in zs], radii, 9, 0, tip=trunk_at(6.8),
              shade=[0.85, 0.85, 0.8, 0.72, 0.62, 0.5, 0.36, 0.26])
    fork = trunk_at(5.0)
    centre = Vector((fork.x + 0.1, fork.y, 7.3))

    turn = rng.uniform(0, 2 * math.pi)
    lobes = [dict(c=centre + Vector((0, 0, 0.4)), r=(2.75, 2.6, 1.15), yaw=turn, freq=3, floor=-0.4, phase=rng.uniform(0, 6))]
    rims = []
    for k in range(5):
        az = turn + 2 * math.pi * k / 5 + rng.uniform(-0.16, 0.16)
        dist = rng.uniform(2.45, 2.7)
        c = Vector((centre.x + math.cos(az) * dist, centre.y + math.sin(az) * dist, centre.z - 0.05 + rng.uniform(-0.15, 0.15)))
        rims.append((c, az))
        lobes.append(dict(c=c, r=(rng.uniform(1.6, 1.75), rng.uniform(1.8, 2.0), rng.uniform(0.85, 0.98)), yaw=az, freq=3,
                          floor=-0.35, phase=rng.uniform(0, 6)))
    for k in range(4):
        az = turn + 0.6 + 2 * math.pi * k / 4 + rng.uniform(-0.3, 0.3)
        dist = rng.uniform(1.3, 1.7)
        lobes.append(dict(c=Vector((centre.x + math.cos(az) * dist, centre.y + math.sin(az) * dist,
                                    centre.z + 0.82 + rng.uniform(-0.08, 0.1))),
                          r=(rng.uniform(1.3, 1.5), rng.uniform(1.15, 1.3), rng.uniform(0.6, 0.7)), yaw=az, freq=2,
                          floor=-0.6, phase=rng.uniform(0, 6)))
    for c, az in rims[::2]:                                # three limbs from the trunk up into rim clumps
        start = Vector((fork.x, fork.y, fork.z - 0.4))
        end = Vector((c.x - math.cos(az) * 0.5, c.y - math.sin(az) * 0.5, c.z - 0.25))
        m1 = start.lerp(end, 0.35) + Vector((0, 0, 0.3))
        m2 = start.lerp(end, 0.7) + Vector((0, 0, 0.3))
        soft_tube(mb, [start, m1, m2, end], [0.17, 0.14, 0.11, 0.08], 6, 0, tip=end + (end - m2).normalized() * 0.4,
                  shade=[0.42, 0.34, 0.26, 0.2])
    soft_crown(mb, lobes, 0, centre + Vector((0, 0, -0.9)), (centre.z - 1.0, centre.z + 1.6), blend=0.45)
    return mb.build(name, [mat], col, sway=True)


FROND_TEX_WIDTH = 1.6       # metres across the cutout texture (make_textures.FROND_M); its length is the frond


def cutout_material(name, texture):
    """Leaf material 'W_<name>' whose outline is cut by the alpha of textures/<texture> (make_textures.py): the
    custom properties alpha_clip (cutoff) and double_sided go through export_unity_bundle.py into materials.json,
    where CampusWorldBuilder turns them into URP Lit alpha clipping with both faces drawn."""
    mat = E.material(name, "#6B9A42", roughness=0.85)
    mat["alpha_clip"], mat["double_sided"] = 0.5, 1
    set_wind(mat, "palm")
    mat.use_backface_culling = False
    path = os.path.join(E.TEXTURES, texture)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    if os.path.exists(path) and not any(n.type == "TEX_IMAGE" for n in nodes):
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(path, check_existing=True)
        tex.extension = "EXTEND"
        bsdf = nodes.get("Principled BSDF")
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    return mat


def palm(name, seed, mat, frond_mat, col, n_fronds=20):
    """Canary Island date palm, soft look (test 2026-11-10): a round, gently curved trunk that swells into a smooth
    knob of old frond bases (ramp material, darker towards the crown), and feathered fronds in three tiers: young
    ones pointing up, a full middle tier, and old ones arching outwards. Each frond is one smooth, slightly folded
    and twisted blade (add_frond); its feathered outline is the alpha of the cutout texture. About 9.5 m."""
    rng = random.Random(seed)
    mb = MeshBuilder()
    sway = Vector((rng.uniform(0.2, 0.36), rng.uniform(-0.12, 0.12), 0))
    zs = [-0.4, 0.2, 1.0, 2.3, 3.6, 4.9, 6.0, 6.75, 7.35, 7.9, 8.2]
    radii = [0.66, 0.54, 0.48, 0.45, 0.43, 0.44, 0.5, 0.66, 0.74, 0.6, 0.34]     # the last four rings are the knob
    path = [Vector((0, 0, z)) + sway * (max(z, 0) / 8.0) ** 2 for z in zs]
    soft_tube(mb, path, radii, 8, 0, tip=path[-1] + Vector((0, 0, 0.16)),
              shade=[0.82, 0.82, 0.8, 0.76, 0.7, 0.62, 0.52, 0.42, 0.34, 0.28, 0.24])

    top_n = 5
    low_n = (n_fronds - top_n) // 2
    tiers = (  # (count, base height above the knob centre, base radius, start elevation, droop, length, width factor)
        (top_n, 0.5, 0.25, 58, 62, 3.3, 0.8),                         # young fronds, pointing up
        (n_fronds - top_n - low_n, 0.15, 0.5, 32, 80, 4.7, 1.0),
        (low_n, -0.15, 0.6, 8, 62, 4.5, 1.0),                        # old fronds arch outwards, not down the trunk
    )
    knob = path[-3]
    for count, dz, r0, el0_deg, droop_deg, length, wide in tiers:
        offset = rng.uniform(0, 2 * math.pi)
        for i in range(count):
            az = offset + 2 * math.pi * i / count + rng.uniform(-0.18, 0.18)
            el0 = math.radians(el0_deg + rng.uniform(-6, 6))
            droop = math.radians(droop_deg + rng.uniform(-10, 10))
            base = knob + Vector((math.cos(az) * r0, math.sin(az) * r0, dz))
            add_frond(mb, base, az, el0, droop, length * rng.uniform(0.92, 1.08), 1, rng, wide)
    return mb.build(name, [mat, frond_mat], col, sway=True)


# (position along the spine, half width of the blade in metres): a loose hull around the leaflets of the texture
FROND_SECTIONS = ((0.0, 0.34), (0.1, 0.62), (0.28, 0.78), (0.5, 0.8), (0.72, 0.7), (0.88, 0.5), (1.0, 0.22))


def add_frond(mb, base, az, el0, droop, length, mat, rng, wide=1.0):
    """One feathered palm frond: a single smooth blade of 24 triangles (seven cross-sections of left edge, midrib,
    right edge), drawn from both sides. The leaflets are not geometry: the material's cutout texture (u across the
    blade, v from base to tip) cuts the feathered outline. The blade has a soft V-fold along the midrib near the
    base that opens towards the tip, a slight twist and a sideways curl; smooth normals (edges lean outwards)
    keep it soft. The spine starts at elevation el0 and bends down by `droop` over its length (closed-form arc)."""
    horiz = Vector((math.cos(az), math.sin(az), 0))
    side = Vector((math.sin(az), -math.cos(az), 0))
    curl = rng.uniform(-0.3, 0.3)
    twist = rng.uniform(-0.35, 0.35)

    def spine(t):
        el = el0 - droop * t
        if abs(droop) < 1e-6:
            h, z = length * t * math.cos(el0), length * t * math.sin(el0)
        else:
            h = length * (math.sin(el0) - math.sin(el)) / droop
            z = length * (math.cos(el) - math.cos(el0)) / droop
        tangent = horiz * math.cos(el) + Vector((0, 0, math.sin(el)))
        return base + horiz * h + Vector((0, 0, z)) + side * (curl * t * t), tangent

    rows = []
    for t, hw in FROND_SECTIONS:
        c, tangent = spine(t)
        up0 = side.cross(tangent).normalized()
        a = twist * t                                             # the blade turns about its midrib towards the tip
        s = side * math.cos(a) + up0 * math.sin(a)
        up = up0 * math.cos(a) - side * math.sin(a)
        w = hw * wide
        fold = 0.32 * (1.0 - t) - 0.12 * t                        # edges raised near the base, hanging at the tip
        u = 0.5 * w / (FROND_TEX_WIDTH / 2 * wide)                # the narrow young fronds show the whole texture
        v = 0.01 + 0.98 * t
        left = mb.v(c + s * w + up * (fold * w), up - s * (fold * 0.9) + s * 0.25, (0.5 + u, v))
        mid = mb.v(c, up, (0.5, v))
        right = mb.v(c - s * w + up * (fold * w), up + s * (fold * 0.9) - s * 0.25, (0.5 - u, v))
        mb.flutter[mid] = t * t                                   # wind: the base is held by the trunk, the tip is free
        mb.flutter[left] = mb.flutter[right] = min(t * t + 0.12 * t, 1.0)
        rows.append((left, mid, right))
    for ra, rb in zip(rows, rows[1:]):                            # wound so the face normals point up (kept as is)
        mb.f((ra[0], rb[0], rb[1], ra[1]), mat, keep=True)
        mb.f((ra[1], rb[1], rb[2], ra[2]), mat, keep=True)


def olive(name, seed, mat, col):
    """Olive tree, soft look (test 2026-11-10): a short, gnarled trunk that leans and splits into three stems, and
    an irregular crown of nine small smooth clumps (one on top, one over each stem, five smaller ones around at
    different heights), wider than tall. One ramp-shaded material with a silvery green ramp. About 4.8 m tall,
    5 m across."""
    rng = random.Random(seed)
    mb = MeshBuilder()
    turn = rng.uniform(0, 2 * math.pi)
    lean = Vector((math.cos(turn), math.sin(turn), 0)) * 0.28
    zs = [-0.45, 0.1, 0.55, 1.0, 1.4]
    path = [Vector((0, 0, z)) + lean * (max(z, 0) / 1.4) + Vector((0.09 * math.sin(z * 3.1 + turn), 0.07 * math.cos(z * 2.6 + turn), 0))
            for z in zs]
    ph = rng.uniform(0, 6)
    soft_tube(mb, path, [0.47, 0.37, 0.31, 0.3, 0.27], 8, 0, shade=[0.82, 0.8, 0.72, 0.62, 0.5],
              knots=lambda k, a: 1.0 + 0.14 * math.sin(3 * a + ph + 0.9 * k))
    fork = path[-1]
    centre = fork + Vector((0, 0, 1.9))
    lobes = [dict(c=fork + Vector((rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), 2.35)), r=(1.15, 1.05, 0.9),
                  yaw=turn, freq=3, floor=-0.7, phase=rng.uniform(0, 6))]
    for k in range(3):                                    # three stems leaning apart, each ending inside a clump
        az = turn + 2 * math.pi * k / 3 + rng.uniform(-0.35, 0.35)
        d, h = rng.uniform(0.6, 0.85), rng.uniform(1.0, 1.45)
        end = fork + Vector((math.cos(az) * d, math.sin(az) * d, h))
        m1 = fork.lerp(end, 0.4) + Vector((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 0.12))
        m2 = fork.lerp(end, 0.75) + Vector((rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), 0.08))
        soft_tube(mb, [fork - Vector((0, 0, 0.25)), m1, m2, end], [0.2, 0.16, 0.13, 0.09], 6, 0,
                  tip=end + (end - m2).normalized() * 0.3, shade=[0.5, 0.42, 0.34, 0.28])
        lobes.append(dict(c=Vector((end.x + math.cos(az) * 0.25, end.y + math.sin(az) * 0.25, end.z + 0.45)),
                          r=(rng.uniform(0.95, 1.1), rng.uniform(0.85, 1.0), rng.uniform(0.75, 0.88)), yaw=az,
                          freq=3 if k == 0 else 2, floor=-0.7, phase=rng.uniform(0, 6)))
    for k in range(5):                                    # smaller clumps around, at different heights
        az = turn + 0.5 + 2 * math.pi * k / 5 + rng.uniform(-0.4, 0.4)
        dist = rng.uniform(1.15, 1.55)
        lobes.append(dict(c=Vector((fork.x + math.cos(az) * dist, fork.y + math.sin(az) * dist, fork.z + rng.uniform(1.1, 2.5))),
                          r=(rng.uniform(0.62, 0.8), rng.uniform(0.58, 0.72), rng.uniform(0.5, 0.62)), yaw=az, freq=2,
                          floor=-0.7, phase=rng.uniform(0, 6)))
    soft_crown(mb, lobes, 0, centre, (fork.z + 0.7, fork.z + 3.3), blend=0.3, base=0.36, crease=0.22, reach=2.6)
    return mb.build(name, [mat], col, sway=True)


def plaza_tree(name, seed, mat, col):
    """Young staked tree of the entrance plaza (V15a; V16: reshaped after the photospheres N10, N12, N13 of Jul 2020:
    young broadleaf trees 5-6.5 m tall with a narrow upright crown of 2-2.5 m and a clear stem of 2.2 m; V15a's crown
    was four clumps on twigs, 3 m wide, which read as a small umbrella tree). A thin, nearly straight trunk, an upright
    ovoid crown (one tall clump, a smaller one on top, two low side clumps), 5.6 m tall and 1.9 m across at scale 1;
    three square timber stakes of 1.8 m around it, each tied to the trunk by a dark band. One ramp-shaded material
    ("plaza": fresh green; the bark column is pale timber, so the stakes read light and the trunk dark). About 420
    triangles. Placed only by env_entrance.dress (its gravel pit is part of the plaza paving)."""
    rng = random.Random(seed)
    mb = MeshBuilder()
    turn = rng.uniform(0, 2 * math.pi)
    sway = Vector((math.cos(turn), math.sin(turn), 0)) * rng.uniform(0.04, 0.08)
    zs = [-0.35, 0.7, 1.8, 2.7, 3.5]
    path = [Vector((0, 0, z)) + sway * math.sin(max(z, 0) * 1.1) for z in zs]
    soft_tube(mb, path, [0.075, 0.066, 0.055, 0.044, 0.03], 6, 0, shade=[0.3, 0.3, 0.26, 0.22, 0.18],
              tip=path[-1] + Vector((0, 0, 0.6)))
    top = path[-1]
    centre = Vector((top.x, top.y, 3.9))
    lobes = [dict(c=Vector((top.x, top.y, 3.8)), r=(0.8, 0.74, 1.45), yaw=turn, freq=2, floor=-0.8,
                  phase=rng.uniform(0, 6)),
             dict(c=Vector((top.x + rng.uniform(-0.1, 0.1), top.y + rng.uniform(-0.1, 0.1), 4.8)),
                  r=(0.54, 0.5, 0.82), yaw=turn + 1.0, freq=2, floor=-0.8, phase=rng.uniform(0, 6))]
    for k in range(2):                                    # two low side clumps: the crown is widest in its lower third
        az = turn + math.pi * k + rng.uniform(-0.5, 0.5)
        lobes.append(dict(c=Vector((top.x + math.cos(az) * 0.36, top.y + math.sin(az) * 0.36, 3.1 + 0.3 * k)),
                          r=(0.56, 0.5, 0.6), yaw=az, freq=2, floor=-0.75, phase=rng.uniform(0, 6)))
    soft_crown(mb, lobes, 0, centre, (2.2, 5.6), blend=0.3, base=0.4, crease=0.14, reach=1.6)

    def post(x, y, half, z0, z1, shade):                  # square timber post: flat faces, bark column of the ramp
        uv = (U_BARK, _shade(shade))
        lo = [mb.v((x + sx * half, y + sy * half, z0), None, uv) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        hi = [mb.v((x + sx * half, y + sy * half, z1), None, uv) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        for j in range(4):
            mb.f((lo[j], lo[(j + 1) % 4], hi[(j + 1) % 4], hi[j]), 0)
        mb.f(hi, 0)

    for k in range(3):                                    # the three stakes and their ties
        az = turn + 0.6 + 2 * math.pi * k / 3
        sx, sy = math.cos(az) * 0.42, math.sin(az) * 0.42
        post(sx, sy, 0.04, -0.3, 1.8, 0.86 - 0.05 * k)
        tz = 1.6
        t0 = Vector((sx, sy, tz))
        t1 = Vector((0, 0, tz)) + sway * math.sin(tz * 1.1)
        side = Vector((-math.sin(az), math.cos(az), 0)) * 0.012
        uv = (U_BARK, _shade(0.06))
        quad = [mb.v(t0 - side - Vector((0, 0, 0.035)), None, uv), mb.v(t1 - side - Vector((0, 0, 0.035)), None, uv),
                mb.v(t1 + side + Vector((0, 0, 0.035)), None, uv), mb.v(t0 + side + Vector((0, 0, 0.035)), None, uv)]
        mb.f(quad, 0, keep=True)
        mb.f(quad[::-1], 0, keep=True)                    # a band seen from both sides
    return mb.build(name, [mat], col, sway=True)


def shrub(name, seed, leaf, dark, col, size=(0.72, 0.66, 0.62), lobe=(0.46, 0.42, 0.4)):
    """Pittosporum-like round shrub: one big lumpy clump and a smaller one leaning on it, flattened where it
    meets the ground (bottom 6 cm below z = 0). The faces that look down get the darker green, a cheap
    built-in contact shadow."""
    rng = random.Random(seed)
    mb = MeshBuilder()
    LEAF, DARK = 0, 1

    def shade(nrm):
        return DARK if nrm.z < -0.25 else LEAF

    rz = size[2]
    blob(mb, (0.0, 0.0, 0.8 * rz - 0.06), size, 2, rng, shade, jitter=0.1, yaw=rng.uniform(0, 3), floor=-0.8)
    az = rng.uniform(0, 2 * math.pi)
    d = size[0] * 0.78
    blob(mb, (math.cos(az) * d, math.sin(az) * d, 0.8 * lobe[2] - 0.06), lobe, 1, rng, shade, jitter=0.08,
         yaw=rng.uniform(0, 3), floor=-0.8)
    return mb.build(name, [leaf, dark], col)


def pampas(name, seed, blades, plume, col, n=8, plumes=3):
    """Pampas / ornamental grass clump, about 2.2 m with plumes. The leaf mass is a low fountain: n leaves rise
    from drooping tips near the ground to upright tips around a sunken heart, with notches between them
    (7n triangles). Each cream plume is a long spindle rising out of the clump (6 triangles)."""
    rng = random.Random(seed)
    mb = MeshBuilder()
    LEAF, PLUME = 0, 1
    heart = mb.v((0, 0, 0.8))
    B, T, C, U = [], [], [], []              # base, drooping tip, notch, upright tip
    for j in range(n):
        a = 2 * math.pi * j / n + rng.uniform(-0.06, 0.06)
        h = a + math.pi / n                  # tips sit half a step between the notches
        B.append(mb.v((math.cos(a) * 0.3, math.sin(a) * 0.3, -0.12)))
        C.append(mb.v((math.cos(a) * 0.46, math.sin(a) * 0.46, rng.uniform(0.62, 0.74))))
        r_tip = rng.uniform(1.0, 1.18)
        T.append(mb.v((math.cos(h) * r_tip, math.sin(h) * r_tip, rng.uniform(0.06, 0.2))))
        r_up = rng.uniform(0.3, 0.42)
        U.append(mb.v((math.cos(h) * r_up, math.sin(h) * r_up, rng.uniform(1.1, 1.32))))
    for j in range(n):
        k = (j + 1) % n
        mb.f((C[j], B[j], T[j]), LEAF)             # underside of the drooping leaf, down to the base
        mb.f((B[j], B[k], T[j]), LEAF)
        mb.f((B[k], C[k], T[j]), LEAF)
        mb.f((C[j], T[j], U[j]), LEAF)             # upper side: from the drooping tip up to the upright tip
        mb.f((T[j], C[k], U[j]), LEAF)
        mb.f((U[j], C[k], U[k]), LEAF)             # valley between two upright tips
        mb.f((U[j], U[k], heart), LEAF)            # sunken heart
    for k in range(plumes):
        az = 2 * math.pi * k / plumes + rng.uniform(-0.4, 0.4)
        lean = rng.uniform(0.28, 0.42)
        widest = rng.uniform(1.55, 1.8)
        root = Vector((0, 0, 0.6))
        ring_c = Vector((math.cos(az) * lean, math.sin(az) * lean, widest))
        axis = (ring_c - root).normalized()
        tip = ring_c + axis * 0.68 + Vector((math.cos(az) * 0.08, math.sin(az) * 0.08, 0))
        side = axis.cross(Vector((0, 0, 1))).normalized()
        other = axis.cross(side)
        ring = [mb.v(ring_c + (side * math.cos(a) + other * math.sin(a)) * 0.17)
                for a in (az + 2 * math.pi * q / 3 for q in range(3))]
        r_id, t_id = mb.v(root), mb.v(tip)
        for q in range(3):
            mb.f((ring[(q + 1) % 3], ring[q], r_id), PLUME)    # stalk widening into the plume
            mb.f((ring[q], ring[(q + 1) % 3], t_id), PLUME)    # plume tapering to its tip
    return mb.build(name, [blades, plume], col)


def build_prototypes():
    """Creates every plant prototype in collection Proto_Vegetation and returns {name: object}.
    Rebuilding replaces the old objects. Raises if a prototype breaks its triangle budget."""
    col = E.collection(PROTO_COLLECTION)
    for obj in list(col.objects):
        mesh = obj.data
        bpy.data.objects.remove(obj)
        if mesh and mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    m = E.material
    protos = {
        "Veg_UmbrellaPine": umbrella_pine("Veg_UmbrellaPine", 11, ramp_material("pine"), col),
        "Veg_UmbrellaPine_B": umbrella_pine("Veg_UmbrellaPine_B", 23, ramp_material("pine_b"), col),
        "Veg_Palm": palm("Veg_Palm", 5, ramp_material("palm"),
                         cutout_material("palm_frond", "palm_frond_cutout_albedo.png"), col, n_fronds=19),
        "Veg_Palm_B": palm("Veg_Palm_B", 17, ramp_material("palm_b"),
                           cutout_material("palm_frond_b", "palm_frond_b_cutout_albedo.png"), col, n_fronds=21),
        "Veg_Olive": olive("Veg_Olive", 3, ramp_material("olive"), col),
        "Veg_Olive_B": olive("Veg_Olive_B", 19, ramp_material("olive_b"), col),
        "Veg_PlazaTree": plaza_tree("Veg_PlazaTree", 29, ramp_material("plaza"), col),        # V15a
        "Veg_PlazaTree_B": plaza_tree("Veg_PlazaTree_B", 53, ramp_material("plaza"), col),
        "Veg_Shrub": shrub("Veg_Shrub", 7, m("leaf", roughness=0.85), m("leaf_dark", roughness=0.9), col),
        "Veg_Shrub_B": shrub("Veg_Shrub_B", 41, m("shrub_leaf_b", "#4F7F45", roughness=0.85),
                             m("shrub_leaf_dark_b", "#365C34", roughness=0.9), col,
                             size=(0.86, 0.74, 0.5), lobe=(0.5, 0.46, 0.42)),
        "Veg_Grass": pampas("Veg_Grass", 13, m("pampas_leaf", "#8EA167", roughness=0.9),
                            m("pampas_plume", "#EDE2C4", roughness=0.9), col),
    }
    for name, obj in protos.items():
        tris = triangles(obj)
        budget = TRI_BUDGET[name.removesuffix("_B")]
        if tris > budget:
            raise ValueError(f"{name}: {tris} triangles, budget {budget}")
    return protos


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# =====================================================================================================
# Placement
# =====================================================================================================
def _segment_distance(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    length2 = dx * dx + dy * dy
    t = 0.0 if length2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / length2))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


class SegmentGrid:
    """Path centrelines in a 20 m grid. clearance(x, y) = distance to the nearest centreline minus its half
    width (how far the point is from the path edge); inf when nothing is within `margin` metres."""

    def __init__(self, paths, margin=20.0, cell=20.0):
        self.cell, self.grid = cell, {}
        for p in paths:
            hw = p.get("width", 0.0) / 2
            for (ax, ay), (bx, by) in zip(p["points"], p["points"][1:]):
                seg = (ax, ay, bx, by, hw)
                for i in range(int((min(ax, bx) - margin) // cell), int((max(ax, bx) + margin) // cell) + 1):
                    for j in range(int((min(ay, by) - margin) // cell), int((max(ay, by) + margin) // cell) + 1):
                        self.grid.setdefault((i, j), []).append(seg)

    def clearance(self, x, y):
        best = float("inf")
        for ax, ay, bx, by, hw in self.grid.get((int(x // self.cell), int(y // self.cell)), ()):
            best = min(best, _segment_distance(x, y, ax, ay, bx, by) - hw)
        return best

    def distance(self, x, y):
        """Distance to the nearest centreline (inf beyond `margin`)."""
        return min((_segment_distance(x, y, ax, ay, bx, by)
                    for ax, ay, bx, by, _hw in self.grid.get((int(x // self.cell), int(y // self.cell)), ())),
                   default=float("inf"))


class Footprints:
    """Building footprints. clearance(x, y) = distance to the nearest footprint edge, negative inside."""

    def __init__(self, buildings):
        self.items = []
        for b in buildings:
            pts = b["points"]
            xs, ys = [p[0] for p in pts], [p[1] for p in pts]
            self.items.append((min(xs), min(ys), max(xs), max(ys), pts))

    def clearance(self, x, y, reach=40.0):
        best = float("inf")
        for x0, y0, x1, y1, pts in self.items:
            if x < x0 - reach or x > x1 + reach or y < y0 - reach or y > y1 + reach:
                continue
            d = min(_segment_distance(x, y, ax, ay, bx, by)
                    for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]))
            best = min(best, -d if E.inside_polygon(x, y, pts) else d)
        return best


class Occupancy:
    """Placed plants in a 5 m grid, so new ones keep their distance (sum of both spacing radii)."""

    def __init__(self):
        self.grid = {}

    def free(self, x, y, r, factor=1.0):
        i, j = int(x // 5), int(y // 5)
        for di in (-1, 0, 1):
            for dj in (-1, 0, 1):
                for ox, oy, orad in self.grid.get((i + di, j + dj), ()):
                    if math.hypot(ox - x, oy - y) < (r + orad) * factor:
                        return False
        return True

    def add(self, x, y, r):
        self.grid.setdefault((int(x // 5), int(y // 5)), []).append((x, y, r))


class Site:
    """Everything the placement rules ask about a point: roads, footways, buildings, campus, coast, route."""

    def __init__(self):
        paths = [p for p in E.DATA["paths"] if not p.get("tunnel")]
        self.roads = SegmentGrid([p for p in paths if p["type"] in ROAD_TYPES])
        self.foot = SegmentGrid([p for p in paths if p["type"] in FOOT_TYPES])
        self.primary = SegmentGrid([p for p in paths if p["type"] in ("primary", "primary_link")])
        self.buildings = Footprints(E.DATA["buildings"])
        self.campus = Footprints([b for b in E.DATA["buildings"] if b["campus"]])
        self.coast = [c for c in E.DATA.get("coast", []) if len(c) > 1]

    def coast_distance(self, x, y):
        return min((_segment_distance(x, y, ax, ay, bx, by) for line in self.coast
                    for (ax, ay), (bx, by) in zip(line, line[1:])), default=float("inf"))

    def fits(self, x, y, crown, generated=False):
        """The hard rules for a plant whose foliage reaches `crown` metres from its trunk."""
        if not (E.X0 + 2 < x < E.X1 - 2 and E.Y0 + 2 < y < E.Y1 - 2):
            return False
        ground = E.ground_class(x, y)
        if ground in ("sea", "water"):
            return False
        if E.distance_to_route(x, y) < E.KEEP_CLEAR + crown:
            return False
        if self.buildings.clearance(x, y) < max(BUILDING_GAP, crown + 0.3):   # no crown inside a wall either
            return False
        if self.roads.clearance(x, y) <= ROAD_GAP:
            return False
        if generated:  # taste rules for plants we invent (real OSM trees may stand on paving or by a footway)
            if any(math.hypot(x - cx, y - cy) < r + crown for (cx, cy), r in CLEARINGS):
                return False
            low_plant = crown < 1.5                      # shrubs and grass would spill onto the path
            if ground in ("paved", "pitch") or self.foot.clearance(x, y) <= (0.8 * crown if low_plant else FOOT_GAP):
                return False
        return True


def _ground_z(x, y):
    """Terrain height (E.ground_z: the shaped corridor surface once the world build installed it). If the terrain
    is still the raw elevation data, a plant within 8 m of the centreline sits on the driving height instead
    (route.json track_z, where the road bed will be) when that is less than 3 m away from the terrain."""
    z = E.ground_z(x, y)
    if E.distance_to_route(x, y) < 8.0:
        p = E.nearest_route_point(x, y)
        shaped = abs(E.ground_z(p["x"], p["y"]) - p["track_z"]) < 0.3       # terrain already follows the road
        indoor = p["tunnel"] or p.get("zone") in ("tunnel", "atrium", "terrace deck")
        if not shaped and not indoor and abs(p["track_z"] - z) < 3.0:
            return p["track_z"] - 0.05
    return z


def _entry(proto, x, y, rot, scale):
    return {"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(_ground_z(x, y), 3),
            "rot_z": round(rot, 4), "scale": round(scale, 3)}


def _nudged(ok, x, y):
    """(x, y) itself if ok(x, y), else the closest spot within 8 m that is ok (12 directions per ring)."""
    if ok(x, y):
        return x, y
    for r in NUDGE_RADII:
        for k in range(12):
            a = 2 * math.pi * k / 12
            if ok(x + math.cos(a) * r, y + math.sin(a) * r):
                return x + math.cos(a) * r, y + math.sin(a) * r
    return None


def place(seed=SEED, write=True):
    """Computes all plant placements from the rules below and writes out/placements_vegetation.json.

    1. Every OSM tree gets a tree: a palm within 15 m of the Avenida Marginal (primary / primary_link) or low
       (terrain < 6 m) near the coast, an olive near campus buildings, an umbrella pine otherwise. A tree that
       breaks a hard rule moves to the nearest spot within 8 m that fits (at a smaller scale if needed).
    2. Palm rows (every ~14 m) along both sides of the Avenida Marginal and on the land side of the promenade.
    3. Umbrella pines scattered on 'wood' ground, one per ~250 m2 (jittered grid).
    4. Olive groups and shrub beds on the campus lawns, in clusters with open lawn between them.
    5. Pampas grass clumps in clusters on the dune edge (land along the beach sand, or the top 5 m of the sand),
       at least 20 m from the water.
    Hard rules for all: >= KEEP_CLEAR + crown radius from the route, >= 2 m (and the crown) from buildings,
    not on sea/water, trunk >= width/2 + 1 m from road centrelines. Deterministic for a given seed.
    """
    rng = random.Random(seed)
    site = Site()
    occ = Occupancy()
    placed, stats = [], {"osm_trees": 0, "osm_moved": 0, "osm_dropped": []}

    def add(kind, x, y, scale=None, check=True, generated=True, factor=1.0):
        spec = KIND[kind]
        s = scale or rng.uniform(*spec["scale"])
        crown = spec["crown"] * s
        if check and (not site.fits(x, y, crown, generated) or not occ.free(x, y, spec["space"] * s, factor)):
            return False
        proto = spec["protos"][rng.randrange(len(spec["protos"]))]
        placed.append(_entry(proto, x, y, rng.uniform(0, 2 * math.pi), s))
        occ.add(x, y, spec["space"] * s)
        return True

    def count(kinds):
        names = {p for k in kinds for p in KIND[k]["protos"]}
        return sum(1 for p in placed if p["proto"] in names)

    # 1. OSM trees ------------------------------------------------------------------------------------------
    for x, y in sorted(map(tuple, E.DATA["trees"])):
        if not (E.X0 < x < E.X1 and E.Y0 < y < E.Y1):
            continue
        stats["osm_trees"] += 1
        if site.primary.distance(x, y) <= PALM_NEAR_PRIMARY or (
                E.ground_z(x, y) < PALM_COAST_Z and site.coast_distance(x, y) < COAST_NEAR):
            kind = "palm"
        elif site.campus.clearance(x, y) <= CAMPUS_NEAR:
            kind = "olive"
        else:
            kind = "pine"
        spec = KIND[kind]
        done = False
        for s in (rng.uniform(*spec["scale"]), spec["scale"][0] * 0.95):
            crown = spec["crown"] * s
            # real trees may stand closer together than invented ones, but never on top of each other
            spot = _nudged(lambda px, py: site.fits(px, py, crown) and occ.free(px, py, 1.2), x, y)
            if spot:
                add(kind, spot[0], spot[1], scale=s, check=False, generated=False)
                stats["osm_moved"] += spot != (x, y)
                done = True
                break
        if not done:
            stats["osm_dropped"].append((round(x, 1), round(y, 1), kind))

    # 1b. V10b: the real trees on the lawn islands by the promenade are low, wind-shaped pines (K02), not palms.
    # Swapped after the fact with their own random numbers, so every other plant keeps its place and variant.
    low_rng = random.Random(seed + 11)
    for k, p in enumerate(placed):
        if _kind(p["proto"]) == "palm" and LOW_PINE_AREA[0] <= p["x"] <= LOW_PINE_AREA[2] \
                and LOW_PINE_AREA[1] <= p["y"] <= LOW_PINE_AREA[3]:
            placed[k] = dict(p, proto=KIND["pine"]["protos"][low_rng.randrange(2)],
                             scale=round(low_rng.uniform(*LOW_PINE_SCALE), 3))
            stats["low_pines"] = stats.get("low_pines", 0) + 1

    # 2. Palm rows -------------------------------------------------------------------------------------------
    def row(points, offsets, side_sign=None):
        """Palms every ~PALM_SPACING m along a polyline, at the first lateral offset that fits."""
        travelled, next_at = 0.0, PALM_SPACING / 2
        for (ax, ay), (bx, by) in zip(points, points[1:]):
            seg = math.hypot(bx - ax, by - ay)
            if seg < 1e-6:
                continue
            ux, uy = (bx - ax) / seg, (by - ay) / seg
            while next_at <= travelled + seg:
                t = next_at - travelled
                cx, cy = ax + ux * t, ay + uy * t
                for sign in ((side_sign,) if side_sign else (1, -1)):
                    for off in offsets:
                        x, y = cx - uy * off * sign, cy + ux * off * sign
                        if not occ.free(x, y, 6.0, 1.0):        # a tree already stands here
                            break
                        if add("palm", x, y, factor=1.0):
                            break
                next_at += PALM_SPACING + rng.uniform(-1.5, 1.5)
            travelled += seg

    for p in sorted(E.DATA["paths"], key=lambda q: q["id"]):
        if p["type"] == "primary" and p.get("name") == "Avenida Marginal":
            hw = p["width"] / 2
            row(p["points"], [hw + 2.5 + k * 1.5 for k in range(5)])
    prom = [q for q in E.route_points() if q.get("zone") == "promenade"]
    if len(prom) > 2:
        land = _land_side(prom)
        row([(q["x"], q["y"]) for q in prom], [E.KEEP_CLEAR + KIND["palm"]["crown"] * 1.12 + 0.3 + k * 1.5
                                               for k in range(3)], side_sign=land)

    # 2b. Palms on Capricciosa's terrace (V10b): they stand on the deck, so they skip the building rule ---------
    import env_beachfront as B
    deck_rng = random.Random(seed + 10)              # own random numbers: the other steps stay as they were
    for bid in B.RESTAURANTS:
        for kind_, x, y, z, _rot in B.deck_spots(bid):
            if kind_ != "palm":
                continue
            s = deck_rng.uniform(*DECK_PALM_SCALE)
            if E.distance_to_route(x, y) < E.KEEP_CLEAR + KIND["palm"]["crown"] * s:
                continue
            proto = KIND["palm"]["protos"][deck_rng.randrange(2)]
            placed.append({"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(z, 3),
                           "rot_z": round(deck_rng.uniform(0, 2 * math.pi), 4), "scale": round(s, 3)})
            occ.add(x, y, KIND["palm"]["space"] * s)

    # 3. Umbrella pines on 'wood' ground ----------------------------------------------------------------------
    cell = math.sqrt(WOOD_AREA_PER_PINE)
    gx = E.X0
    while gx < E.X1:
        gy = E.Y0
        while gy < E.Y1:
            x, y = gx + rng.uniform(0, cell), gy + rng.uniform(0, cell)
            if E.ground_class(x, y) == "wood":
                add("pine", x, y, factor=0.85)
            gy += cell
        gx += cell

    # 4. Campus lawns: olive groups and shrub beds -------------------------------------------------------------
    lawn = [(E.X0 + i * E.STEP, E.Y0 + j * E.STEP) for j in range(E.ROWS) for i in range(E.COLS)
            if E.DATA["grid"]["classes"][j][i] == "grass"]
    lawn = [c for c in lawn if 3.0 < site.campus.clearance(c[0], c[1], reach=60.0) <= 60.0]
    rng.shuffle(lawn)
    seeds = []
    for cx, cy in lawn:
        if all(math.hypot(cx - sx, cy - sy) > 21.0 for sx, sy in seeds):
            seeds.append((cx, cy))
    for cx, cy in seeds:
        if count(("shrub", "grass")) >= MAX_LOW - 150:
            break
        if rng.random() < 0.45:                         # olive group with a few shrubs around it
            for _ in range(rng.randint(1, 4)):
                add("olive", cx + rng.gauss(0, 4.0), cy + rng.gauss(0, 4.0))
            for _ in range(rng.randint(1, 4)):
                add("shrub", cx + rng.gauss(0, 5.0), cy + rng.gauss(0, 5.0))
        else:                                            # shrub bed: a tight blob or a short curved line
            n = rng.randint(4, 9)
            heading = rng.uniform(0, math.pi)
            for k in range(n):
                if rng.random() < 0.5:
                    x, y = cx + rng.gauss(0, 1.8), cy + rng.gauss(0, 1.8)
                else:
                    t = (k - n / 2) * 1.25
                    bow = 0.06 * t * t
                    x = cx + math.cos(heading) * t - math.sin(heading) * bow
                    y = cy + math.sin(heading) * t + math.cos(heading) * bow
                add("shrub", x, y, factor=0.95)

    # 5. Pampas grass on the dune edge (land cells next to beach sand, away from the waterline) -----------------
    classes = E.DATA["grid"]["classes"]

    def near(i, j, wanted, reach):
        return any(classes[jj][ii] in wanted
                   for jj in range(max(0, j - reach), min(E.ROWS, j + reach + 1))
                   for ii in range(max(0, i - reach), min(E.COLS, i + reach + 1)))

    land = ("ground", "grass", "wood")
    dune = [(E.X0 + i * E.STEP, E.Y0 + j * E.STEP) for j in range(E.ROWS) for i in range(E.COLS)
            if not near(i, j, ("sea", "water"), 4) and (
                (classes[j][i] in land and near(i, j, ("sand",), 2))           # land along the beach
                or (classes[j][i] == "sand" and near(i, j, land, 1)))]         # or the top 5 m of the beach
    rng.shuffle(dune)
    seeds = []
    for cx, cy in dune:
        if all(math.hypot(cx - sx, cy - sy) > 13.0 for sx, sy in seeds):
            seeds.append((cx, cy))
    first_dune = len(placed)
    for cx, cy in seeds:
        for _ in range(rng.randint(3, 7)):
            if count(("shrub", "grass")) >= MAX_LOW:
                break
            gx, gy = cx + rng.gauss(0, 2.2), cy + rng.gauss(0, 2.2)
            if any(math.hypot(gx - bx, gy - by) < r + KIND["grass"]["crown"] for (bx, by), r in BEACH_CLEARINGS):
                continue                                 # V10b: open sand at the terraces, parasol rows and cove
            add("grass", gx, gy, factor=0.9)
    # V10d: no clumps on the sand itself: the beach zone and the cove of the traced coast (coast.py), a clump closer
    # than SAND_EDGE_CLEAR to the backshore line, and the OSM sand areas (the beach east of the fort); nor on the
    # open ground round the beach restaurants that the ground map now paints as paving (coast_trace open_to_paved).
    # The clumps are placed as before and dropped afterwards, so every other plant keeps its place and its random
    # numbers.
    sand_areas = [a["ring"] for a in E.DATA["areas"] if a["kind"] == "sand"] + coast.C.get("open_to_paved", [])

    def on_sand(x, y):
        return (coast.in_beach(x, y) or coast.in_cove(x, y) or coast.backshore_distance(x, y) < SAND_EDGE_CLEAR
                or any(coast.inside(x, y, [tuple(q) for q in ring]) for ring in sand_areas))
    kept = [p for p in placed[first_dune:] if not on_sand(p["x"], p["y"])]
    stats["dune_clumps_on_sand_dropped"] = len(placed) - first_dune - len(kept)
    placed[first_dune:] = kept

    # caps (never expected to bite, but the game budget is a promise)
    trees = [p for p in placed if _kind(p["proto"]) in TREE_KINDS][:MAX_TREES]
    low = [p for p in placed if _kind(p["proto"]) not in TREE_KINDS][:MAX_LOW]
    placed = trees + low
    REPORT.clear()
    REPORT.update(stats)
    if write:
        os.makedirs(E.OUT, exist_ok=True)
        with open(PLACEMENTS_FILE, "w") as fh:
            json.dump({
                "frame": "world metres like route.json: x east, y north, z up; rot_z radians counter-clockwise "
                         "seen from above; uniform scale; prototypes from env_vegetation.py",
                "seed": seed,
                "counts": {name: sum(1 for p in placed if p["proto"] == name)
                           for spec in KIND.values() for name in spec["protos"]},
                "placements": placed,
            }, fh, indent=1)
    return placed


def _kind(proto):
    for kind, spec in KIND.items():
        if proto in spec["protos"]:
            return kind
    return None


def _land_side(points):
    """+1 if the land is on the left of the driving direction along these route points, -1 if on the right
    (the side with the higher terrain 15 m out)."""
    votes = 0
    for a, b in zip(points, points[1:]):
        d = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
        nx, ny = -(b["y"] - a["y"]) / d, (b["x"] - a["x"]) / d
        left = E.ground_z(a["x"] + nx * 15, a["y"] + ny * 15)
        right = E.ground_z(a["x"] - nx * 15, a["y"] - ny * 15)
        votes += 1 if left > right else -1
    return 1 if votes >= 0 else -1


def instance_for_preview(placements, protos):
    """Linked duplicates (they share the prototype mesh) in collection World_Vegetation, for renders only.
    The prototype collection is hidden from renders, because the prototypes stand at the world origin."""
    col = E.collection(WORLD_COLLECTION)
    for obj in list(col.objects):
        bpy.data.objects.remove(obj)
    proto_col = bpy.data.collections.get(PROTO_COLLECTION)
    if proto_col:
        proto_col.hide_render = True
    objects = []
    for k, p in enumerate(placements):
        src = protos[p["proto"]]
        obj = bpy.data.objects.new(f"{p['proto']}.{k:04d}", src.data)
        obj.location = (p["x"], p["y"], p["z"])
        obj.rotation_euler = (0.0, p.get("rot_y", 0.0), p["rot_z"])
        obj.scale = (p["scale"],) * 3
        col.objects.link(obj)
        objects.append(obj)
    return objects
