"""Task V32: a light "far" version of the kart and its tyre, for karts that are far from the camera (the bots).

Why: one kart in the game is about 111,000 triangles with 41 draw calls (body 48k / 23 materials, four tyres 50k / 20,
driver 14k / 2). With five bots that is six times as much, more than the whole campus world. Far away nobody sees
the V8's bolts, so Unity shows this light copy instead (LODGroup, built by NovaKartVisualBuilder).

Kart body (Kart_Classic_Far.fbx):
1. Split the kart by material and simplify every part on its own (Decimate "collapse"), so no colour bleeds.
2. Every part except the body paint gets ONE shared material, NK_KartLOD, whose texture is a tiny palette
   (palette.json): all corners of a part point at the centre of its cell, so it shows exactly its old colour.
   The body paint keeps NK_Paint_Body, so bots stay recolourable.

Tyre (Tyre_Slick_Far.fbx + baked textures): simplifying a round tyre makes it angular, so the far tyre is built new:
1. Measure the tyre's outline: for every distance from the axle, how far its two faces stick out, and the tread.
2. Spin that outline around the axle (40 steps): a round, clean low-poly tyre.
3. Bake the detail of the full tyre onto it with Cycles: which material is where (an ID map, one flat colour per
   material, one sample per pixel so edges never mix) and the shape (normal map: grooves, spokes, letters).
   Unity turns the ID map into the real colours and metal values of the tyre materials (NovaKartVisualBuilder).

Run (from the repo root, Blender 5.2 or newer; written and tested with 5.3):
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python tools/kart_lod/make_kart_lod.py
Optional after "--": kart_ratio (default below), or <Kart> <Tyre> [kart_ratio] for the other roster karts (driver
select, 17 Nov 2026), e.g. "-- Buggy Offroad". The Nova Classic + Slick keep their old file names (palette.json,
Kart_Classic_Far, Tyre_Slick_Far, NT_TyreFar, tyre_far_id.png ...); every other combo gets its own:
palette_<Kart>.json, Kart_<Kart>_Far.fbx, Tyre_<Tyre>_Far.fbx, NT_TyreFar_<Tyre>, tyre_far_<Tyre>_id.png,
tyre_far_<Tyre>_ao.png and Textures/TyreFar_<Tyre>_Normal.png.
Limits: the far kart's steering wheel does not turn (the tyres still roll and steer, they sit on the wheel anchors).
Thin parts (bolts, pin stripes) vanish on the body; on the tyre they survive only as normal-map detail.
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
MODELS = os.path.join(REPO, "Assets", "_Project", "Visuals", "Kart", "Models")
TEXTURES = os.path.join(REPO, "Assets", "_Project", "Visuals", "Kart", "Textures")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if ARGS and not ARGS[0].replace(".", "", 1).isdigit():
    KART, TYRE, ARGS = ARGS[0], ARGS[1], ARGS[2:]      # another roster kart: -- Buggy Offroad [ratio]
else:
    KART, TYRE = "Classic", "Slick"
CLASSIC = KART == "Classic"
SLICK = TYRE == "Slick"
PALETTE = json.load(open(os.path.join(HERE, "palette.json" if CLASSIC else "palette_" + KART + ".json")))
LOD_MATERIAL = "NK_KartLOD"                         # same name on every kart: Unity keeps one per kart folder
TYRE_MATERIAL = "NT_TyreFar" if SLICK else "NT_TyreFar_" + TYRE
TYRE_ID_MAP = os.path.join(HERE, "tyre_far_id.png" if SLICK else "tyre_far_" + TYRE + "_id.png")  # read by Unity
TYRE_NORMAL_MAP = os.path.join(TEXTURES, "TyreFar_Normal.png" if SLICK else "TyreFar_" + TYRE + "_Normal.png")
TYRE_AO_MAP = os.path.join(HERE, "tyre_far_ao.png" if SLICK else "tyre_far_" + TYRE + "_ao.png")  # darkens the colour

KART_RATIO = float(ARGS[0]) if len(ARGS) > 0 else 0.20   # keep 20 % of the kart's triangles (at 10 % edges turned jagged)
DECAL_RATIO = 0.6                                          # number plate and checkers (at 20 % the checkers smeared)
TYRE_STEPS = 40            # steps around the axle: at 40 the outline is off a true circle by at most 0.7 mm
TYRE_RINGS = 12            # outline points per tyre face, from the axle to the tread
TYRE_TREAD = 6             # outline points across the tread
TYRE_TEXTURE = PALETTE["tyre_bake"]["size"]
TYRE_IDS = {entry["name"]: entry for entry in PALETTE["tyre_bake"]["ids"]}


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def cell_centre(name):
    """UV of the centre of a material's palette cell (cell 0 = bottom left, row by row)."""
    index = PALETTE["materials"].index(name)   # a material missing from palette.json stops the script here
    grid = PALETTE["grid"]
    return ((index % grid + 0.5) / grid, (index // grid + 0.5) / grid)


def only_select(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def import_fbx(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, name + ".fbx"))
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def export(objs, name):
    only_select(objs)
    path = os.path.join(MODELS, name + ".fbx")
    # same settings as Art/Tools/kartkit.py, so Unity imports it the same way as the original
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'}, use_mesh_modifiers=True,
                             mesh_smooth_type='FACE', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, path_mode='AUTO', embed_textures=False)
    print("FBX written", path, os.path.getsize(path), "bytes")


# ---------------------------------------------------------------- kart body: simplify per material + palette

def make_palette_lod(meshes, ratio):
    """Joins the meshes, simplifies them per material and puts them on the palette. Returns the one finished mesh."""
    only_select(meshes)
    if len(meshes) > 1:
        bpy.ops.object.join()                          # keeps the first mesh's name, parent and transform
    whole = bpy.context.view_layer.objects.active
    before = triangles(whole)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL')             # one object per material
    bpy.ops.object.mode_set(mode='OBJECT')
    parts = [o for o in bpy.context.selected_objects]

    paint = bpy.data.materials.get(PALETTE["paint"])
    lod = bpy.data.materials.get(LOD_MATERIAL) or bpy.data.materials.new(LOD_MATERIAL)
    for part in parts:
        # the one material this part still uses (separate by material leaves the others as empty slots)
        used = {p.material_index for p in part.data.polygons}
        name = part.material_slots[used.pop()].material.name
        # 1) simplify this part on its own; decals carry the kart's look with few triangles, so they stay sharper
        mod = part.modifiers.new("lod", 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = max(ratio, DECAL_RATIO) if name.startswith("NK_Number") else ratio
        only_select([part])
        bpy.ops.object.modifier_apply(modifier=mod.name)
        # 2) one material per part: the body paint stays, everything else points at its palette cell
        part.data.materials.clear()
        if name == PALETTE["paint"]:
            part.data.materials.append(paint)
        else:
            part.data.materials.append(lod)
            uv = part.data.uv_layers.active or part.data.uv_layers.new(name="UVMap")
            centre = cell_centre(name)
            for loop_uv in uv.data:
                loop_uv.uv = centre

    parts = [p for p in parts if len(p.data.polygons) > 0]   # parts that decimated away completely
    only_select(parts)
    bpy.ops.object.join()
    result = bpy.context.view_layer.objects.active
    bpy.ops.object.shade_smooth_by_angle(angle=0.70)   # 40 degrees: round where it was round, hard edges stay hard
    print(f"LOD {result.name}: {before} -> {triangles(result)} triangles, materials {[m.name for m in result.data.materials]}")
    return result


def kart():
    meshes = import_fbx("Kart_" + KART)
    root = bpy.data.objects["Kart_" + KART]
    body = bpy.data.objects["Kart_Body"]
    meshes.remove(body)
    meshes.insert(0, body)                             # join into the body: the result keeps its place under the root
    result = make_palette_lod(meshes, KART_RATIO)
    result.name = "Kart_Far"
    # Export the mesh alone, its turn baked into the vertices. Exporting the root as well made the root come back
    # turned by 90 degrees in Unity (FBX axis round trip), so the far kart lay on its side.
    only_select([result])
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.data.objects.remove(root)                      # with it the wheel and exhaust anchors: the full kart keeps them
    export([result], "Kart_" + KART + "_Far")


# ---------------------------------------------------------------- tyre: round low-poly shell + baked detail

def tyre_outline(high):
    """The tyre's outline in (along the axle, distance from the axle), measured by shooting rays at the full tyre:
    at every distance from the axle, rays from in front of each face at 72 angles; the furthest hit is the outline.
    (Measuring at vertices missed the spokes: long faces have vertices only at their ends.)
    The axle is the tyre's thinnest direction. Returns the outline from the +side's centre, over the tread, to the
    -side's centre, plus which outline segments belong to the tread."""
    verts = [v.co for v in high.data.vertices]
    size = [max(v[i] for v in verts) - min(v[i] for v in verts) for i in range(3)]
    axle = size.index(min(size))
    a, b = [i for i in range(3) if i != axle]
    outer = max(math.hypot(v[a], v[b]) for v in verts)
    tree = BVHTree.FromObject(high, bpy.context.evaluated_depsgraph_get())   # works in the tyre's own space

    def point(x, r, angle):
        co = Vector((0.0, 0.0, 0.0))
        co[axle], co[a], co[b] = x, r * math.cos(angle), r * math.sin(angle)
        return co

    def face_depth(r, side):
        """Furthest point of the + (side 1) or - (side -1) face at distance r from the axle, over all angles and a
        thin band around r, so spokes and thin rings between two outline points are not missed."""
        best = None
        towards = Vector((0.0, 0.0, 0.0))
        towards[axle] = -side
        band = outer / TYRE_RINGS / 2
        for dr in (-band, -band / 2, 0.0, band / 2, band):
            rr = min(max(r + dr, 0.0), outer)
            for k in range(72):
                hit = tree.ray_cast(point(side * 1.0, rr, 2 * math.pi * k / 72), towards)[0]
                if hit is not None and (best is None or hit[axle] * side > best * side):
                    best = hit[axle]
        return best

    def tread_height(x):
        """Furthest point from the axle at position x across the tread, over all angles."""
        best = 0.0
        for k in range(72):
            angle = 2 * math.pi * k / 72
            start = point(x, 1.0, angle)
            hit = tree.ray_cast(start, -(start - point(x, 0.0, angle)))[0]
            if hit is not None:
                best = max(best, math.hypot(hit[a], hit[b]))
        return best

    # each face from the axle out to just inside the tread (the last 3 % belong to the rounded shoulder)
    plus, minus = [], []
    for ring in range(TYRE_RINGS):
        r = outer * 0.97 * ring / (TYRE_RINGS - 1)
        plus.append((face_depth(r, 1), r))
        minus.append((face_depth(r, -1), r))
    # the tread: across the width, between the two faces' outermost points (keeps the rounded shoulders)
    tread = []
    lo, hi = minus[-1][0], plus[-1][0]
    for step in range(TYRE_TREAD + 1):
        x0 = hi - (hi - lo) * step / TYRE_TREAD
        tread.append((x0, tread_height(x0 * 0.999)))
    outline = plus + tread + list(reversed(minus))
    tread_first = len(plus) - 1                        # segments from the last face ring over the tread
    tread_last = len(plus) + len(tread)
    return outline, axle, a, b, outer, tread_first, tread_last


def spin_outline(outline, axle, a, b, outer, tread_first, tread_last):
    """Spins the outline around the axle into a closed, smooth mesh with UVs for baking:
    +face as a disc in the top left, -face as a disc in the top right, the tread as a strip along the bottom."""
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    rows = []
    for (x, r) in outline:
        row = []
        for k in range(TYRE_STEPS):
            angle = 2 * math.pi * k / TYRE_STEPS
            co = [0.0, 0.0, 0.0]
            co[axle], co[a], co[b] = x, r * math.cos(angle), r * math.sin(angle)
            row.append(bm.verts.new(co))
        rows.append(row)

    def uv_for(i, k, part):
        x, r = outline[i]
        angle = 2 * math.pi * k / TYRE_STEPS
        if part == "plus":       # disc centre (0.25, 0.75), radius 0.24 = the tyre's outer radius
            return (0.25 + 0.24 * r / outer * math.cos(angle), 0.75 + 0.24 * r / outer * math.sin(angle))
        if part == "minus":
            return (0.75 - 0.24 * r / outer * math.cos(angle), 0.75 + 0.24 * r / outer * math.sin(angle))
        # tread strip: around the tyre left to right, across the tread bottom to top (k = TYRE_STEPS closes the seam)
        across = (i - tread_first) / (tread_last - tread_first)
        return (0.01 + 0.98 * k / TYRE_STEPS, 0.01 + 0.47 * across)

    for i in range(len(rows) - 1):
        part = "plus" if i < tread_first else ("tread" if i < tread_last else "minus")
        for k in range(TYRE_STEPS):
            k1 = (k + 1) % TYRE_STEPS
            corners = [(i, k), (i, k1), (i + 1, k1), (i + 1, k)]
            # on the axle all points of a row coincide: skip the doubled corner, the face becomes a triangle
            verts, keys = [], []
            for (ri, kk) in corners:
                v = rows[ri][kk]
                if v not in verts:
                    verts.append(v)
                    keys.append((ri, kk))
            if len(verts) < 3:
                continue
            face = bm.faces.new(verts)
            for loop, (ri, kk) in zip(face.loops, keys):
                seam_k = TYRE_STEPS if (part == "tread" and kk == 0 and k == TYRE_STEPS - 1) else kk
                loop[uv_layer].uv = uv_for(ri, seam_k, part)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)   # the axle points of each face become one vertex
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("Tyre_" + TYRE + "_Far")
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def bake(low, high, kind, image, samples):
    """Bakes from the full tyre (high) onto the far tyre (low) into image."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = samples
    scene.cycles.filter_width = 0.01 if samples == 1 else 1.5   # one sharp sample per pixel for the ID map
    node = low.data.materials[0].node_tree.nodes["bake_target"]
    node.image = image
    low.data.materials[0].node_tree.nodes.active = node
    only_select([low, high])
    bpy.context.view_layer.objects.active = low
    bpy.ops.object.bake(type=kind, use_selected_to_active=True, cage_extrusion=0.02, max_ray_distance=0.12,
                        margin=4, normal_space='TANGENT')
    image.filepath_raw = image.filepath_raw
    image.save()


def tyre():
    high = import_fbx("Tyre_" + TYRE)[0]
    outline, axle, a, b, outer, tread_first, tread_last = tyre_outline(high)
    low = bpy.data.objects.new("Tyre_" + TYRE + "_Far", spin_outline(outline, axle, a, b, outer, tread_first, tread_last))
    bpy.context.scene.collection.objects.link(low)
    low.matrix_world = high.matrix_world.copy()        # same place and turn as the full tyre
    for p in low.data.polygons:
        p.use_smooth = True

    # far tyre material with an image node the bakes write into
    mat = bpy.data.materials.new(TYRE_MATERIAL)
    mat.use_nodes = True
    target = mat.node_tree.nodes.new("ShaderNodeTexImage")
    target.name = "bake_target"
    low.data.materials.append(mat)

    # 1) ID map: every full-tyre material glows in its own flat ID colour (palette.json), nothing else
    for slot in high.material_slots:
        m = slot.material
        m.use_nodes = True
        nodes, links = m.node_tree.nodes, m.node_tree.links
        nodes.clear()
        emit = nodes.new("ShaderNodeEmission")
        rgb = TYRE_IDS[m.name]                         # a material missing from palette.json stops the script here
        emit.inputs["Color"].default_value = (rgb["r"], rgb["g"], rgb["b"], 1.0)
        out = nodes.new("ShaderNodeOutputMaterial")
        links.new(emit.outputs["Emission"], out.inputs["Surface"])
    ids = bpy.data.images.new("tyre_far_id", TYRE_TEXTURE, TYRE_TEXTURE, alpha=False, float_buffer=False)
    ids.colorspace_settings.name = 'Non-Color'
    ids.filepath_raw = TYRE_ID_MAP
    ids.file_format = 'PNG'
    bake(low, high, 'EMIT', ids, samples=1)

    # 2) normal map: grooves, spokes and letters as light and shade on the smooth shell
    normals = bpy.data.images.new("tyre_far_normal", TYRE_TEXTURE, TYRE_TEXTURE, alpha=False, float_buffer=False)
    normals.colorspace_settings.name = 'Non-Color'
    normals.filepath_raw = TYRE_NORMAL_MAP
    normals.file_format = 'PNG'
    bake(low, high, 'NORMAL', normals, samples=16)

    # 3) ambient occlusion: tread grooves and the deep brake disc get their shade back (the normal map alone leaves
    #    them flat, because a groove's floor faces the same way as the tread). The shell itself must not cast shade.
    for flag in ("visible_camera", "visible_diffuse", "visible_glossy", "visible_shadow", "visible_transmission"):
        setattr(low, flag, False)
    world = bpy.data.worlds.new("bake_world")
    bpy.context.scene.world = world
    world.light_settings.distance = 0.04               # only nearby parts shade each other (4 cm: groove, dish)
    occlusion = bpy.data.images.new("tyre_far_ao", TYRE_TEXTURE, TYRE_TEXTURE, alpha=False, float_buffer=False)
    occlusion.colorspace_settings.name = 'Non-Color'
    occlusion.filepath_raw = TYRE_AO_MAP
    occlusion.file_format = 'PNG'
    bake(low, high, 'AO', occlusion, samples=64)

    print(f"LOD Tyre_{TYRE}: {triangles(high)} -> {triangles(low)} triangles, round in {TYRE_STEPS} steps; "
          f"baked ID, normal and AO maps ({TYRE_TEXTURE} px)")
    bpy.data.objects.remove(high)
    export([low], "Tyre_" + TYRE + "_Far")


print("Blender", bpy.app.version_string, "kart", KART, "tyre", TYRE)
kart()
tyre()
