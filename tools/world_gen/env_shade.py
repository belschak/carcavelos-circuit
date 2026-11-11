"""V16h: ambient occlusion on buildings, structures, props and trees as VERTEX COLOURS (no texture, no extra material).

build_world.py calls bake() once, after everything is built and before the .blend is saved and the FBX files are
exported. Every mesh gets a colour attribute "Col": rgb = how open the point is (1 = open, down to AO_FLOOR in inner
corners, under canopies, bridges and soffits, at wall feet, inside the tunnel mouth), alpha = 1.
In Unity: WorldLit.shader multiplies the sky light (and a little of the sun) by it; Foliage.shader already tints by
the vertex colour, so crowns get darker undersides for free. Meshes are flat shaded and exported per face corner, so
the colours add no vertices; static batching keeps them.

Why vertex colours and not an AO atlas on a second UV set: no texture memory, no second UV set to unwrap for 1,500
objects, no new material or draw call, and the big flat walls only need the soft gradients vertices give. The fine
occlusion (window reveals, joints) is in the facade textures.

How: for each face corner, 20 rays over the hemisphere of the face normal (cosine weighted, the same fixed set
everywhere), at most AO_REACH metres; near hits count more than far ones. World meshes are tested against the world
(terrain, track, buildings, structures; not the trees and props, which move with the dressing); each tree / prop
prototype is tested against itself and the ground plane it stands on.
"""
import math
import random

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

AO_REACH = 5.0            # metres
AO_FLOOR = 0.42           # the darkest value written
AO_GROUND = 0.55          # the ground counts this much as an occluder for upright faces (wall feet: a soft gradient)
RAYS = 20
ATTRIBUTE = "Col"
PROTOTYPES = ("Proto_Props", "Proto_Vegetation")
INSTANCES = ("World_Props", "World_Vegetation")
SKIP_COLLECTIONS = ("Track_RouteGuide", "World_TunnelGuides")
GROUND_PREFIX = ("World_Ground",)                       # occluders that count as "the ground" (weight AO_GROUND)
NO_OCCLUDER = ("ColliderOnly", "Backdrop_", "WorldOrigin", "RouteGuide", "Marker_", "World_Paths_tunnel", "NoCollide_Paint")
NO_TARGET = ("ColliderOnly", "Backdrop_Sea", "Backdrop_Land", "Backdrop_FarBeach", "Backdrop_FarTown", "Backdrop_Horizon",
             "WorldOrigin", "World_Ground", "Coast_Rocks", "Track_Road", "Track_Kerbs", "Track_EdgeLines", "NoCollide_Paint")


def _directions():
    """A fixed, cosine-weighted set of directions in the hemisphere round +Z."""
    rng = random.Random(1616)
    out = []
    for k in range(RAYS):
        u, v = (k + rng.random()) / RAYS, rng.random()
        r, a = math.sqrt(u), 2.0 * math.pi * ((k * 0.618034 + v * 0.3) % 1.0)
        out.append(Vector((r * math.cos(a), r * math.sin(a), math.sqrt(max(0.0, 1.0 - u)))))
    return out


DIRS = _directions()


def _frame(n):
    t = Vector((0.0, 0.0, 1.0)).cross(n) if abs(n.z) < 0.9 else Vector((1.0, 0.0, 0.0)).cross(n)
    t.normalize()
    return t, n.cross(t)


def _tree(objects):
    """One BVH over the objects' triangles in world space; returns it and the index of the first non-ground polygon."""
    verts, polys = [], []
    ordered = sorted(objects, key=lambda o: not o.name.startswith(GROUND_PREFIX))      # the ground first
    first_solid = 0
    for obj in ordered:
        mw, base = obj.matrix_world, len(verts)
        verts.extend(mw @ v.co for v in obj.data.vertices)
        polys.extend([base + i for i in p.vertices] for p in obj.data.polygons)
        if obj.name.startswith(GROUND_PREFIX):
            first_solid = len(polys)
    return BVHTree.FromPolygons(verts, polys, all_triangles=False, epsilon=0.0), first_solid


def _occlusion(tree, first_solid, p, n, cache):
    key = (round(p.x, 2), round(p.y, 2), round(p.z, 2), round(n.x, 1), round(n.y, 1), round(n.z, 1))
    hit = cache.get(key)
    if hit is None:
        t, b = _frame(n)
        total = 0.0
        for d in DIRS:
            direction = t * d.x + b * d.y + n * d.z
            _loc, _nor, index, dist = tree.ray_cast(p, direction, AO_REACH)
            if index is not None:
                w = 1.0 - (dist / AO_REACH) ** 2
                total += w * (AO_GROUND if index < first_solid and n.z < 0.5 else 1.0)
        hit = cache[key] = max(AO_FLOOR, 1.0 - total / RAYS)
    return hit


def _write(mesh, values):
    attr = mesh.color_attributes.get(ATTRIBUTE) or mesh.color_attributes.new(ATTRIBUTE, "BYTE_COLOR", "CORNER")
    flat = []
    for v in values:
        flat.extend((v, v, v, 1.0))
    attr.data.foreach_set("color", flat)
    mesh.color_attributes.active_color = attr
    mesh.color_attributes.render_color_index = list(mesh.color_attributes).index(attr)


def _bake_mesh(mesh, matrix, tree, first_solid, cache):
    normal_matrix = matrix.to_3x3().inverted_safe().transposed()
    values = [1.0] * len(mesh.loops)
    for poly in mesh.polygons:
        n = (normal_matrix @ poly.normal).normalized()
        centre = matrix @ poly.center
        for li in poly.loop_indices:
            p = matrix @ mesh.vertices[mesh.loops[li].vertex_index].co
            p = p + (centre - p) * 0.06 + n * 0.05            # a little inside the face and off it: no self hits
            values[li] = _occlusion(tree, first_solid, p, n, cache)
    _write(mesh, values)
    return values


def bake():
    bpy.context.view_layer.update()
    in_cols = lambda o, names: any(c.name in names for c in o.users_collection)
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and not in_cols(o, SKIP_COLLECTIONS)]
    world = [o for o in meshes if not in_cols(o, PROTOTYPES + INSTANCES)]
    occluders = [o for o in world if not any(k in o.name for k in NO_OCCLUDER)]
    tree, first_solid = _tree(occluders)
    cache, done, corners, dark = {}, set(), 0, 0
    for obj in world:
        if obj.data.name in done:
            continue
        done.add(obj.data.name)
        if any(k in obj.name for k in NO_TARGET):
            _write(obj.data, [1.0] * len(obj.data.loops))    # every mesh has the attribute: batches stay uniform
            continue
        values = _bake_mesh(obj.data, obj.matrix_world, tree, first_solid, cache)
        corners += len(values)
        dark += sum(1 for v in values if v < 0.8)
    protos = 0
    for obj in meshes:
        if not in_cols(obj, PROTOTYPES) or obj.data.name in done:
            continue
        done.add(obj.data.name)
        # the prototype alone on a flat ground at its foot (z = 0 of the prototype): self occlusion + contact
        verts = [v.co.copy() for v in obj.data.vertices]
        polys = [list(p.vertices) for p in obj.data.polygons]
        base = len(verts)
        verts += [Vector((-60, -60, 0)), Vector((60, -60, 0)), Vector((60, 60, 0)), Vector((-60, 60, 0))]
        local = BVHTree.FromPolygons(verts, [[base, base + 1, base + 2, base + 3]] + polys, epsilon=0.0)
        values = _bake_mesh(obj.data, Matrix.Identity(4), local, 1, {})
        corners += len(values)
        dark += sum(1 for v in values if v < 0.8)
        protos += 1
    for obj in meshes:                                       # anything left (instances of unbaked meshes): white
        if obj.data.name not in done:
            done.add(obj.data.name)
            _write(obj.data, [1.0] * len(obj.data.loops))
    print(f"  shade: vertex ambient occlusion on {len(done)} meshes ({protos} prototypes), {corners} face corners, "
          f"{100.0 * dark / max(corners, 1):.0f} % darker than 0.8")
