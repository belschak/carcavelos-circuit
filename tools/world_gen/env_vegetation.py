"""Vegetation for the Carcavelos campus world: low-poly plant prototypes and the rules for where they grow.

Run inside Blender 5.2 (headless works), for example from another script:

    import sys
    sys.path.insert(0, "<this folder>")   # the folder with env_common.py
    import env_vegetation as V
    protos = V.build_prototypes()               # {name: object} in collection Proto_Vegetation
    placements = V.place()                      # list of dicts, also written to out/placements_vegetation.json
    V.instance_for_preview(placements, protos)  # linked duplicates in World_Vegetation, only for renders

Prototypes: one mesh object each, origin at the base centre, z up, metres, flat shaded, triangulated, 2-3 flat
colours (trunk + foliage + highlight). Trunks reach 0.4 m below z = 0, so a plant on a slope never floats.
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
import env_common as E  # noqa: E402

PROTO_COLLECTION = "Proto_Vegetation"
WORLD_COLLECTION = "World_Vegetation"
PLACEMENTS_FILE = os.path.join(E.OUT, "placements_vegetation.json")
SEED = 20261108

# Hard triangle limits per plant type (both colour variants of a type share the limit).
TRI_BUDGET = {"Veg_UmbrellaPine": 440, "Veg_Palm": 500, "Veg_Olive": 330, "Veg_Shrub": 120, "Veg_Grass": 80}

# Per plant type: crown radius at scale 1 (m, widest reach of the foliage from the trunk axis, measured on the
# prototypes and checked by test_env_assets.py; used for the keep-clear rules),
# spacing radius (m, how much room it claims from other plants) and the random scale range.
KIND = {
    "pine": dict(protos=("Veg_UmbrellaPine", "Veg_UmbrellaPine_B"), crown=5.0, space=3.4, scale=(0.86, 1.12)),
    "palm": dict(protos=("Veg_Palm", "Veg_Palm_B"), crown=5.6, space=2.4, scale=(0.88, 1.12)),
    "olive": dict(protos=("Veg_Olive", "Veg_Olive_B"), crown=2.75, space=2.1, scale=(0.85, 1.15)),
    "shrub": dict(protos=("Veg_Shrub", "Veg_Shrub_B"), crown=1.1, space=0.62, scale=(0.85, 1.25)),
    "grass": dict(protos=("Veg_Grass",), crown=1.2, space=0.55, scale=(0.8, 1.2)),
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

    def v(self, co, normal=None):
        """New vertex. With `normal` it belongs to a smooth surface: every face whose corners all carry a normal
        is shaded smooth with these normals (spherical crown normals); all other faces stay flat."""
        self.verts.append(Vector((co[0], co[1], co[2])))
        if normal is not None:
            self.normals[len(self.verts) - 1] = Vector(normal).normalized()
        return len(self.verts) - 1

    def f(self, idx, mat):
        self.faces.append(tuple(idx))
        self.mats.append(mat)

    def build(self, name, materials, col):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        for m in materials:
            mesh.materials.append(m)
        mesh.polygons.foreach_set("material_index", self.mats)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])          # every island points outwards
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        origin = [v.index for v in bm.verts]                           # new vertex order -> builder index
        bm.to_mesh(mesh)
        bm.free()
        smooth_vert = [origin[k] in self.normals for k in range(len(origin))]
        for poly in mesh.polygons:                                       # flat shading, faceted look ...
            poly.use_smooth = bool(self.normals) and all(smooth_vert[k] for k in poly.vertices)   # ... or puffy
        mesh.validate()
        if self.normals:
            loops = []
            for poly in mesh.polygons:
                for li in poly.loop_indices:
                    vi = mesh.loops[li].vertex_index
                    loops.append(tuple(self.normals[origin[vi]]) if poly.use_smooth else tuple(poly.normal))
            mesh.normals_split_custom_set(loops)
        mesh.update()
        return E.new_object(name, mesh, col)


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


def umbrella_pine(name, seed, crown, top, bark, col):
    """Stone pine, puffy (design reference 2026-11-09): slim trunk with a slight lean and S-bend, two short
    limbs, and a wide flat umbrella of four smooth lobes (a big centre lobe and three rim lobes). About 9 m tall,
    9 m across. Replaces the faceted 12 m pad pine: softer silhouette, 424 instead of ~600 triangles."""
    rng = random.Random(seed)
    mb = MeshBuilder()
    BARK, CROWN, TOP = 0, 1, 2
    lean = rng.uniform(0.35, 0.55)
    bend = rng.uniform(0.1, 0.2) * rng.choice((-1, 1))

    def trunk_at(z):
        t = max(z, 0.0) / 6.0
        return Vector((lean * t ** 1.4, bend * math.sin(t * math.pi), z))

    zs = [-0.4, 0.5, 2.6, 4.6, 6.0]
    radii = [0.42, 0.34, 0.29, 0.24, 0.18]
    wob = [[1.0 + rng.uniform(-0.06, 0.06) for _ in range(6)] for _ in zs]
    tube(mb, [trunk_at(z) for z in zs], radii, 6, BARK, wobble=wob, tip=trunk_at(6.7))
    fork = trunk_at(5.0)
    centre = Vector((fork.x + 0.1, fork.y, 7.4))

    def mat_for(z):                                   # rim lobes: light only on the flat upper cap
        return TOP if z > 0.45 else CROWN

    turn = rng.uniform(0, 2 * math.pi)
    rims = []
    for k in range(3):
        az = turn + 2 * math.pi * k / 3 + rng.uniform(-0.25, 0.25)
        dist = rng.uniform(2.15, 2.45)
        rims.append((Vector((centre.x + math.cos(az) * dist, centre.y + math.sin(az) * dist,
                             centre.z - 0.15 + rng.uniform(-0.15, 0.15))), az))
    for c, az in rims[:2]:                                # two limbs from the trunk up into rim lobes
        start = Vector((fork.x, fork.y, fork.z - 0.3))
        end = Vector((c.x - math.cos(az) * 0.6, c.y - math.sin(az) * 0.6, c.z - 0.2))
        mid = start.lerp(end, 0.5) + Vector((0, 0, 0.35))
        tube(mb, [start, mid, end], [0.16, 0.12, 0.08], 5, BARK, tip=end + (end - mid).normalized() * 0.4)
    puff_lobe(mb, centre + Vector((0, 0, 0.35)), (2.7, 2.5, 1.25), rng, lambda z: TOP if z > -0.1 else CROWN,
              centre, yaw=turn)
    for c, az in rims:
        puff_lobe(mb, c, (rng.uniform(2.05, 2.3), rng.uniform(1.9, 2.1), rng.uniform(1.0, 1.1)), rng, mat_for, centre,
                  yaw=az)
    return mb.build(name, [bark, crown, top], col)


def palm(name, seed, frond, under, trunk_mat, col, n_fronds=16):
    """Canary Island date palm: thick straight trunk whose rings are turned half a facet against each other
    and alternate slightly in width (diamond leaf-scar look), a round knob of old frond bases, and wide arched
    fronds in three tiers. Each frond is a closed sliver (flat top, keel underneath), so it reads from below
    and from above. About 9.5 m."""
    rng = random.Random(seed)
    mb = MeshBuilder()
    TRUNK, FROND, UNDER = 0, 1, 2
    sway = Vector((rng.uniform(0.15, 0.3), rng.uniform(-0.1, 0.1), 0))
    zs = [-0.4, 1.0, 2.3, 3.6, 4.9, 6.0, 6.75, 7.35, 7.9]
    radii = [0.62, 0.5, 0.49, 0.45, 0.47, 0.5, 0.66, 0.72, 0.55]     # the last three rings are the knob
    path = [Vector((0, 0, z)) + sway * (max(z, 0) / 8.0) ** 2 for z in zs]
    phases = [(k % 2) * math.pi / 7 for k in range(len(zs))]
    tube(mb, path, radii, 7, TRUNK, phases=phases, tip=path[-1] + Vector((0, 0, 0.32)))

    top_n = 5 if n_fronds < 19 else 6
    tiers = (  # (count, base height above the knob centre, base radius, start elevation, droop, length)
        (top_n, 0.45, 0.3, 52, 84, 4.5),
        (7, 0.1, 0.55, 24, 88, 5.1),
        (n_fronds - top_n - 7, -0.25, 0.62, -4, 72, 4.7),
    )
    knob = path[-2]
    for count, dz, r0, el0_deg, droop_deg, length in tiers:
        offset = rng.uniform(0, 2 * math.pi)
        for i in range(count):
            az = offset + 2 * math.pi * i / count + rng.uniform(-0.18, 0.18)
            el0 = math.radians(el0_deg + rng.uniform(-6, 6))
            droop = math.radians(droop_deg + rng.uniform(-10, 10))
            base = knob + Vector((math.cos(az) * r0, math.sin(az) * r0, dz))
            add_frond(mb, base, az, el0, droop, length * rng.uniform(0.92, 1.08), FROND, UNDER, rng)
    return mb.build(name, [trunk_mat, frond, under], col)


def add_frond(mb, base, az, el0, droop, length, top_mat, keel_mat, rng):
    """One arched palm frond (18 triangles): base point, three cross-sections (two top edges + keel), tip.
    The spine starts at elevation el0 and bends down by `droop` over its length (closed-form arc)."""
    horiz = Vector((math.cos(az), math.sin(az), 0))
    side = Vector((math.sin(az), -math.cos(az), 0))

    def spine(t):
        el = el0 - droop * t
        if abs(droop) < 1e-6:
            h, z = length * t * math.cos(el0), length * t * math.sin(el0)
        else:
            h = length * (math.sin(el0) - math.sin(el)) / droop
            z = length * (math.cos(el) - math.cos(el0)) / droop
        tangent = horiz * math.cos(el) + Vector((0, 0, math.sin(el)))
        return base + horiz * h + Vector((0, 0, z)), tangent

    b = mb.v(base)
    sections = []
    for t, width, keel in ((0.18, 0.66, 0.13), (0.5, 1.05, 0.18), (0.8, 0.72, 0.12)):
        c, tangent = spine(t)
        up = side.cross(tangent).normalized()
        w = width * rng.uniform(0.92, 1.08) / 2
        left = mb.v(c + side * w)
        right = mb.v(c - side * w)
        k = mb.v(c - up * keel)
        sections.append((left, right, k))
    tip = mb.v(spine(1.0)[0])
    l1, r1, k1 = sections[0]
    mb.f((b, l1, r1), top_mat)
    mb.f((b, r1, k1), keel_mat)
    mb.f((b, k1, l1), keel_mat)
    for (la, ra, ka), (lb, rb, kb) in zip(sections, sections[1:]):
        mb.f((la, lb, rb, ra), top_mat)
        mb.f((ra, rb, kb, ka), keel_mat)
        mb.f((ka, kb, lb, la), keel_mat)
    l3, r3, k3 = sections[-1]
    mb.f((l3, tip, r3), top_mat)
    mb.f((r3, tip, k3), keel_mat)
    mb.f((k3, tip, l3), keel_mat)


def olive(name, seed, crown, top, bark, col):
    """Olive tree, puffy (design reference 2026-11-09): short twisted trunk that splits into two leaning stems,
    carrying a round crown of three smooth lobes (one high centre lobe, one lower lobe over each stem), wider
    than tall. About 4.8 m tall, 4.8 m across; light tops give the silvery look."""
    rng = random.Random(seed)
    mb = MeshBuilder()
    BARK, CROWN, TOP = 0, 1, 2
    turn = rng.uniform(0, 2 * math.pi)
    lean = Vector((math.cos(turn), math.sin(turn), 0)) * 0.3
    zs = [-0.45, 0.3, 0.9, 1.5]
    path = [Vector((0, 0, z)) + lean * (max(z, 0) / 1.5) + Vector((0.08 * math.sin(z * 3.1), 0.06 * math.cos(z * 2.2), 0))
            for z in zs]
    wob = [[1.0 + rng.uniform(-0.15, 0.15) for _ in range(6)] for _ in zs]
    tube(mb, path, [0.38, 0.3, 0.27, 0.25], 6, BARK, wobble=wob, phases=[k * 0.5 for k in range(len(zs))])
    fork = path[-1]
    centre = fork + Vector((0, 0, 2.0))

    ends = []
    for side in (-1, 1):                               # two stems leaning apart, each ending inside a lobe
        az = turn + math.pi / 2 + side * rng.uniform(1.2, 1.45)
        end = fork + Vector((math.cos(az) * 0.85, math.sin(az) * 0.85, rng.uniform(1.15, 1.35)))
        mid = fork.lerp(end, 0.5) + Vector((rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), 0.05))
        tube(mb, [fork - Vector((0, 0, 0.2)), mid, end], [0.22, 0.17, 0.13], 4, BARK,
             tip=end + (end - mid).normalized() * 0.3)
        ends.append((end, az))
    # one colour per lobe (a light top lobe over two darker side lobes): a split inside a lobe shows as a zigzag
    puff_lobe(mb, centre + Vector((0, 0, 0.3)), (1.55, 1.45, 1.1), rng, lambda z: TOP, centre, yaw=turn, floor=-0.7)
    for end, az in ends:
        c = Vector((end.x + math.cos(az) * 0.3, end.y + math.sin(az) * 0.3, end.z + 0.55))
        puff_lobe(mb, c, (rng.uniform(1.15, 1.3), rng.uniform(1.05, 1.15), rng.uniform(0.9, 1.0)), rng,
                  lambda z: CROWN, centre, yaw=az, floor=-0.7)
    return mb.build(name, [bark, crown, top], col)


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
        "Veg_UmbrellaPine": umbrella_pine("Veg_UmbrellaPine", 11, m("pine_crown", "#47744A", roughness=0.9),
                                          m("pine_crown_top", "#80A361", roughness=0.9),
                                          m("pine_bark", "#7A5238", roughness=0.95), col),
        "Veg_UmbrellaPine_B": umbrella_pine("Veg_UmbrellaPine_B", 23, m("pine_crown_b", "#3F6A44", roughness=0.9),
                                            m("pine_crown_top_b", "#739A58", roughness=0.9), m("bark", roughness=0.95),
                                            col),
        "Veg_Palm": palm("Veg_Palm", 5, m("palm_leaf", roughness=0.8), m("palm_leaf_dark", "#4A6E33", roughness=0.85),
                         m("palm_trunk", "#9A8262", roughness=0.95), col, n_fronds=18),
        "Veg_Palm_B": palm("Veg_Palm_B", 17, m("palm_leaf_b", "#6D9443", roughness=0.8),
                           m("palm_leaf_dark_b", "#557538", roughness=0.85), m("bark_light", roughness=0.95), col,
                           n_fronds=20),
        "Veg_Olive": olive("Veg_Olive", 3, m("olive_crown", "#647D57", roughness=0.9),
                           m("olive_crown_top", "#A0AB77", roughness=0.9), m("olive_bark", "#7D7366", roughness=0.95),
                           col),
        "Veg_Olive_B": olive("Veg_Olive_B", 19, m("olive_crown_b", "#5F7F4F", roughness=0.9),
                             m("olive_crown_top_b", "#9DB06E", roughness=0.9), m("olive_bark_b", "#8A7D6B", roughness=0.95),
                             col),
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
    for cx, cy in seeds:
        for _ in range(rng.randint(3, 7)):
            if count(("shrub", "grass")) >= MAX_LOW:
                break
            add("grass", cx + rng.gauss(0, 2.2), cy + rng.gauss(0, 2.2), factor=0.9)

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
