"""Puts the procedural textures (textures/*.png from make_textures.py and make_groundmap.py) onto the
flat-colour materials. Missing files are skipped, so the world always builds.

Unity note: the FBX files carry material names and UVs in real metres (1 texture tile per UV unit);
assign the same PNGs to URP Lit materials with tiling 1 x 1.
"""
import os

import bpy

import env_common as E

# material name -> (texture base name, roughness when there is no roughness map)
TEXTURED = {
    "W_T_asphalt": ("asphalt", 0.85),
    "W_T_concrete_sand": ("concrete_sand", 0.8),
    "W_T_concrete_polished": ("concrete_polished", 0.3),
    "W_T_concrete_light": ("concrete_polished", 0.8),     # outdoor deck: matte (MATTE), else the low sun glares
    "W_T_tunnel_floor": ("concrete_polished", 0.6),
    "W_T_calcada": ("calcada", 0.8),
    "W_T_terracotta": ("terracotta_steps", 0.8),
    "W_T_lawn": ("grass", 0.95),
    "W_T_kerb": ("kerb_stripes", 0.6),
    "W_T_cut_wall": ("concrete_sand", 0.9),
    "W_tunnel_mural": ("mural_original", 0.7),
    "W_B_wall_white": ("facade_white", 0.85),
    "W_B_glass": ("window_band", 0.08),
    "W_ground_map": ("ground_colour", 0.95),
    "W_T_sand": ("sand", 0.95),
}


TEXTURED.update({"W_B_solar": ("solar_panel", 0.75), "W_T_cut_wall": ("render_ochre", 0.9)})   # V10c; V10d: solar matte
# (at 0.35 / metallic 0.2 the panels mirrored the bright sky at the aerial camera's grazing angle and read light grey)

TEXTURED["W_B_deck_paving"] = ("concrete_sand", 0.9)      # V10c2: decks and terraces at plaza level (env_campus)

MATTE = {"W_T_concrete_light", "W_B_deck_paving"}   # use the albedo only, constant roughness (no polished-floor roughness map)


# V16h: materials that keep their own colour and get a neutral texture multiplied onto it in Unity (WorldLit.shader /
# URP Lit tint; export_unity_bundle.py "tint"), so walls of many colours share one texture and no memory is added.
# name -> (texture base, normal strength, world-space UVs or None). World-space UVs (tile metres across, up, roof
# scale; 0 = roofs read one plain texel) are for meshes without UVs: WorldLit.shader projects the texture by world
# position. In Blender these materials stay flat colour.
RENDER = ("facade_white", 0.6, None)                       # rendered walls: UVs in metres already (3 m tile)
CONCRETE = ("concrete_panel", 0.8, (2.4, 2.4, 1.0 / 2.4))  # fair-faced concrete with panel joints and tie holes
FAR_HOUSE = ("far_facade", 0.0, (12.0, 9.0, 0.0))          # the buildings beyond the map edge: windows, sills
TINTED = {
    "W_B_wall_0": RENDER, "W_B_wall_1": RENDER, "W_B_wall_2": RENDER, "W_B_wall_3": RENDER, "W_B_wall_4": RENDER,
    "W_B_wall_cream": RENDER, "W_B_wall_umber": RENDER,
    "W_tunnel_concrete": CONCRETE, "W_portal_concrete": CONCRETE, "W_entrance_concrete": CONCRETE,
    "W_concrete_light": CONCRETE, "W_B_interior_wall": CONCRETE,
    "W_backdrop_building": FAR_HOUSE, "W_far_white": FAR_HOUSE, "W_far_cream": FAR_HOUSE, "W_far_ochre": FAR_HOUSE,
}


def tint_all():
    import json
    path = os.path.join(E.TEXTURES, "tint_gain.json")                     # make_textures.py: 1 / mean of each texture
    gains = json.load(open(path)) if os.path.exists(path) else {}
    done = 0
    for name, (base, strength, world_uv) in TINTED.items():
        mat = bpy.data.materials.get(name)
        if not mat or not texture_path(base, "albedo"):
            continue
        mat["tint_albedo"] = base + "_albedo.png"
        mat["tint_normal"] = base + "_normal.png" if strength > 0 and texture_path(base, "normal") else ""
        mat["tint_normal_strength"] = strength
        mat["tint_gain"] = float(gains.get(base + "_albedo.png", 1.0))
        if world_uv:
            mat["world_uv"] = list(world_uv)
        done += 1
    print(f"  materials: {done} keep their colour and get a shared texture in Unity (tint)")


def texture_path(base, kind):
    name = f"{base}.png" if base == "ground_colour" and kind == "albedo" else f"{base}_{kind}.png"
    path = os.path.join(E.TEXTURES, name)
    return path if os.path.exists(path) else None


def image_node(nodes, path, colour=True):
    node = nodes.new("ShaderNodeTexImage")
    node.image = bpy.data.images.load(path, check_existing=True)
    if not colour:
        node.image.colorspace_settings.name = "Non-Color"
    node.interpolation = "Linear"
    return node


def upgrade(mat, base, roughness):
    albedo = texture_path(base, "albedo")
    if not albedo:
        return False
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    tex = image_node(nodes, albedo)
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    rough = None if mat.name in MATTE else texture_path(base, "rough")
    if rough:
        r = image_node(nodes, rough, colour=False)
        links.new(r.outputs["Color"], bsdf.inputs["Roughness"])
    else:
        bsdf.inputs["Roughness"].default_value = roughness
    normal = texture_path(base, "normal")
    if normal:
        n = image_node(nodes, normal, colour=False)
        nm = nodes.new("ShaderNodeNormalMap")
        nm.inputs["Strength"].default_value = 0.6
        links.new(n.outputs["Color"], nm.inputs["Color"])
        links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return True


GROUND_SHADER = "CarcavelosCircuit/Ground"     # Assets/_Project/Visuals/Shaders/Ground.shader (URP Lit if missing)
GROUND_TEXTURES = {"ground_mask": "ground_mask_data.png",               # make_groundmap.py
                   "ground_detail": "ground_detail_data.png",           # make_textures.py ground_detail
                   "ground_detail_normal": "ground_detail_normal_data.png"}


def upgrade_all():
    done = []
    ground = bpy.data.materials.get("W_ground_map")
    if ground:                             # export_unity_bundle.py -> materials.json "ground" -> Ground.shader
        ground["ground_shader"] = GROUND_SHADER
        for key, name in GROUND_TEXTURES.items():
            ground[key] = name
    for mat in bpy.data.materials:
        if mat.name in TEXTURED and mat.node_tree:
            base, roughness = TEXTURED[mat.name]
            if upgrade(mat, base, roughness):
                done.append(mat.name)
    tint_all()
    print(f"  materials: {len(done)} textured ({', '.join(sorted(done))})")
    return done
