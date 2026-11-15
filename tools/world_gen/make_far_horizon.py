"""V35 (Luc, 15 Nov 2026): the far horizon - the real land 3 to 45 km away (Serra de Sintra, the Cascais coast, the
Caparica cliffs and the Serra da Arrabida across the Tagus mouth) rendered into the sky panorama.

Why: over the sea the game's horizon is an empty, flat haze line. The real view from Carcavelos shows faint blue land
across the water. Far scenery as geometry would cost draw calls in the web build; painted into the sky it costs nothing
at runtime (same skybox texture, same size). At 3 km and more nothing in the picture moves visibly while driving
(the lap is about 400 m across), so a fixed picture is honest.

How (Blender 5.2+, written for 5.3's EEVEE panoramic camera):
1. Height data: dem/far/11_<x>_<y>.png, AWS Terrain Tiles (terrarium, zoom 11, about 75 m per pixel), the same source
   the campus terrain uses. Downloaded once (see FAR_TILES); heights in metres = R * 256 + G + B / 256 - 32768.
2. One mesh of the land between MIN_DIST and MAX_DIST from the camera, sea left out. The earth's curvature (with the
   usual 13 % light refraction) lowers far land, so Arrabida sits as low on the horizon as in reality.
3. Shading: a sun at the mood's sun direction (make_sky.MOODS "sun", like the Unity sun) and an air haze that turns the
   land into the mood's horizon colour with distance (HAZE_KM). The film is transparent: only the land is drawn.
4. EEVEE equirectangular camera at the lap's centre, 2 m over the mean road height, rendered at twice the sky's size and
   turned into the sky's lat-long mapping (u = 0.25 + azimuth / 360, azimuth clockwise from north, v = 1 at the
   zenith). make_sky.py lays it over the painted sky (textures/far_horizon_<mood>.png).

Run (repo folder tools/world_gen):
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python make_far_horizon.py -- noon
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python make_far_horizon.py -- sunset
then python3 make_sky.py and python3 make_sky.py --mood sunset.
Limits: the land is a smooth height model (75 m), no buildings or bridges; towns show only as a warmer tint low down.
"""
import ast
import json
import math
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TRACK = os.path.join(HERE, "..", "..", "Assets", "_Project", "Visuals", "CampusWorld", "Data", "track.json")
DEM = os.path.join(HERE, "dem", "far")
LAT0, LON0 = 38.6780, -9.3265          # world origin (README "Fixed origin")
FAR_TILES = (11, range(969, 974), range(783, 788))   # zoom, x, y: Sintra (NW) to Arrabida (SE), about 45 x 45 km
MIN_DIST, MAX_DIST = 3000.0, 45000.0  # metres from the camera: the game world ends inside 3 km
STEP = 2                               # use every 2nd height sample (about 150 m): smooth enough at 3 km and more
EARTH_R, REFRACTION = 6371000.0, 0.13
HAZE_KM = 20.0                         # distance at which 63 % of the land colour has turned into haze (14 km hid Arrabida)
EYE = 2.0                              # camera height over the mean road (a kart driver's eye, plus a little)
W, H = 4096, 2048                      # render size; make_sky scales it to its 2048 x 1024


def moods():
    """make_sky.MOODS without importing make_sky (it needs PIL, which Blender's Python lacks)."""
    tree = ast.parse(open(os.path.join(HERE, "make_sky.py")).read())
    for node in tree.body:
        if isinstance(node, ast.Assign) and getattr(node.targets[0], "id", "") == "MOODS":
            # MOODS is written with dict(...) calls: evaluate just that expression, with dict as the only name
            return eval(compile(ast.Expression(node.value), "make_sky.py", "eval"), {"__builtins__": {}, "dict": dict})
    raise RuntimeError("make_sky.py has no MOODS")


def srgb_to_linear(hex_colour):
    c = [int(hex_colour[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]


def load_heights():
    """The tiles as one height grid plus the latitude / longitude of every sample."""
    z, xs, ys = FAR_TILES
    rows = []
    for ty in ys:
        row = []
        for tx in xs:
            img = bpy.data.images.load(os.path.join(DEM, f"{z}_{tx}_{ty}.png"))
            px = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)[::-1]   # top row first
            img.colorspace_settings.name = 'Non-Color'
            r, g, b = (px[..., k] * 255.0 for k in range(3))
            row.append(np.round(r) * 256.0 + np.round(g) + np.round(b) / 256.0 - 32768.0)
            bpy.data.images.remove(img)
        rows.append(np.hstack(row))
    h = np.vstack(rows)
    n = 2 ** z
    ny, nx = h.shape
    px_x = xs.start + (np.arange(nx) + 0.5) / 256.0
    px_y = ys.start + (np.arange(ny) + 0.5) / 256.0
    lon = px_x / n * 360.0 - 180.0
    lat = np.degrees(np.arctan(np.sinh(math.pi * (1 - 2 * px_y / n))))
    return h, lat, lon


def camera_spot():
    """The lap's centre (mean of the race line) and 2 m over its mean road height, in generator metres."""
    pts = json.load(open(TRACK))["points"]
    x = sum(p["x"] for p in pts) / len(pts)
    y = sum(p["z"] for p in pts) / len(pts)          # track.json z = generator y (north)
    road = sum(p["y"] for p in pts) / len(pts)
    return x, y, road + EYE


def build_land(cam):
    h, lat, lon = load_heights()
    h, lat, lon = h[::STEP, ::STEP], lat[::STEP], lon[::STEP]
    kx = math.cos(math.radians(LAT0)) * 111320.0
    ky = 110540.0
    X = (lon[None, :] - LON0) * kx + np.zeros_like(h)
    Y = (lat[:, None] - LAT0) * ky + np.zeros_like(h)
    d = np.hypot(X - cam[0], Y - cam[1])
    drop = d * d / (2 * EARTH_R) * (1 - REFRACTION)    # curvature: far land sinks under the horizon
    land = h > 0.5
    Z = np.where(land, h, -20.0) - drop
    ny, nx = h.shape
    verts = np.stack([X, Y, Z], axis=-1).reshape(-1, 3)
    faces = []
    for j in range(ny - 1):
        for i in range(nx - 1):
            q = (j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i)
            corners = [(j, i), (j, i + 1), (j + 1, i + 1), (j + 1, i)]
            if not any(land[c] for c in corners):
                continue                                # open sea: left out, the game's own sea is there
            dist = min(d[c] for c in corners)
            if dist < MIN_DIST or dist > MAX_DIST:
                continue
            faces.append(q)
    mesh = bpy.data.meshes.new("FarLand")
    mesh.from_pydata(verts.tolist(), [], faces)
    mesh.validate()
    for poly in mesh.polygons:
        poly.use_smooth = True
    obj = bpy.data.objects.new("FarLand", mesh)
    bpy.context.scene.collection.objects.link(obj)
    print(f"far land: {len(faces)} quads between {MIN_DIST / 1000:.0f} and {MAX_DIST / 1000:.0f} km, "
          f"highest {h.max():.0f} m, camera at ({cam[0]:.0f}, {cam[1]:.0f}, {cam[2]:.1f})")
    return obj


def land_material(mood):
    """Lit land (greens and ochre, warmer low down where the towns are) fading into the mood's horizon haze."""
    mat = bpy.data.materials.new("FarLand")
    mat.use_nodes = True
    nt = mat.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    geo = nodes.new("ShaderNodeNewGeometry")
    sep = nodes.new("ShaderNodeSeparateXYZ")
    links.new(geo.outputs["Position"], sep.inputs["Vector"])
    town = nodes.new("ShaderNodeMapRange")               # 0 at the coast .. 1 from 120 m up
    town.inputs["From Min"].default_value = 10.0
    town.inputs["From Max"].default_value = 120.0
    links.new(sep.outputs["Z"], town.inputs["Value"])
    ramp = nodes.new("ShaderNodeMix")
    ramp.data_type = 'RGBA'
    ramp.inputs["A"].default_value = (*srgb_to_linear("#B9A88A"), 1.0)   # built-up coast: pale ochre
    ramp.inputs["B"].default_value = (*srgb_to_linear("#5F6E50"), 1.0)   # hills: dusty green
    links.new(town.outputs["Result"], ramp.inputs["Factor"])
    diffuse = nodes.new("ShaderNodeBsdfDiffuse")
    links.new(ramp.outputs["Result"], diffuse.inputs["Color"])
    haze = nodes.new("ShaderNodeEmission")
    haze.inputs["Color"].default_value = (*srgb_to_linear(mood["sky"][0]), 1.0)
    haze.inputs["Strength"].default_value = 1.0
    cam = nodes.new("ShaderNodeCameraData")
    fog = nodes.new("ShaderNodeMath")                   # 1 - exp(-distance / HAZE_KM)
    fog.operation = 'MULTIPLY'
    fog.inputs[1].default_value = -1.0 / (HAZE_KM * 1000.0)
    links.new(cam.outputs["View Distance"], fog.inputs[0])
    ex = nodes.new("ShaderNodeMath")
    ex.operation = 'EXPONENT'
    links.new(fog.outputs[0], ex.inputs[0])
    inv = nodes.new("ShaderNodeMath")
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    links.new(ex.outputs[0], inv.inputs[1])
    mix = nodes.new("ShaderNodeMixShader")
    links.new(inv.outputs[0], mix.inputs["Fac"])
    links.new(diffuse.outputs["BSDF"], mix.inputs[1])
    links.new(haze.outputs["Emission"], mix.inputs[2])
    links.new(mix.outputs["Shader"], out.inputs["Surface"])
    return mat


def light_and_world(mood):
    az, el = (math.radians(a) for a in mood["sun"])
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3.2
    sun.data.color = srgb_to_linear(mood["glare"])
    # the light shines from (sin az cos el, cos az cos el, sin el) towards the land
    to_sun = (math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el))
    sun.rotation_euler = (0.0, 0.0, 0.0)
    sun.rotation_mode = 'QUATERNION'
    from mathutils import Vector
    sun.rotation_quaternion = Vector(to_sun).to_track_quat('Z', 'Y')     # the lamp's +Z points at the sun
    bpy.context.scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("Sky")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (*srgb_to_linear(mood["sky"][1]), 1.0)   # sky light from above
    bg.inputs["Strength"].default_value = 0.9
    bpy.context.scene.world = world


def camera(cam):
    data = bpy.data.cameras.new("Pano")
    data.type = 'PANO'
    data.panorama_type = 'EQUIRECTANGULAR'          # EEVEE renders it since Blender 5.3
    data.clip_start, data.clip_end = 10.0, 60000.0
    obj = bpy.data.objects.new("Pano", data)
    obj.location = cam
    # equirectangular cameras look along their local -Z with +Y up for the image's middle; turned so the middle of
    # the picture looks north (+Y) and the picture's up is the sky (+Z)
    obj.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.scene.camera = obj


def render(mood_name, test_markers=False):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = W, H
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'Standard'     # the sky's colours are plain sRGB: no filmic curve
    sc.eevee.taa_render_samples = 32
    out = os.path.join(HERE, "out", f"far_horizon_raw_{mood_name}.png" if not test_markers else "far_horizon_markers.png")
    sc.render.filepath = out
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    bpy.ops.render.render(write_still=True)
    return out


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    mood_name = args[0] if args else "noon"
    mood = moods()[mood_name]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = camera_spot()
    if "markers" in args:                              # orientation test: a red pillar north, a green one east
        for colour, (dx, dy) in (((1, 0, 0, 1), (0, 8000)), ((0, 1, 0, 1), (8000, 0))):
            bpy.ops.mesh.primitive_cylinder_add(radius=300, depth=3000, location=(cam[0] + dx, cam[1] + dy, 0))
            m = bpy.data.materials.new("m"); m.use_nodes = True
            m.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = colour
            m.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 1.0
            bpy.context.active_object.data.materials.append(m)
        camera(cam)
        print("markers rendered to", render(mood_name, test_markers=True))
        return
    land = build_land(cam)
    land.data.materials.append(land_material(mood))
    light_and_world(mood)
    camera(cam)
    print("far horizon rendered to", render(mood_name))


main()
