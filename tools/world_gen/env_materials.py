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

MATTE = {"W_T_concrete_light"}   # use the albedo only, constant roughness (no polished-floor roughness map)


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
    print(f"  materials: {len(done)} textured ({', '.join(sorted(done))})")
    return done
