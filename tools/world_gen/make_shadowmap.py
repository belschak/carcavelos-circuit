"""V16h: bakes the fixed golden-hour sun's shadows and the sky's ambient occlusion onto the ground, for the whole map.

Run after build_world.py (it reads out/CampusWorld.blend and changes nothing in it):
    /Applications/Blender.app/Contents/MacOS/Blender -b out/CampusWorld.blend -P make_shadowmap.py
then run make_groundmap.py again: it packs the result into the ground colour maps (alpha = sun shadow, the ambient
occlusion is multiplied into the colour). About 1 minute; same input = same output (fixed seed, fixed samples).

Output (out/, git-ignored): ground_shade_world.npy and ground_shade_near.npy, uint8 (rows from the north, 2 channels:
0 = share of the sun that reaches the ground, 255 = fully lit; 1 = share of the open sky the ground sees), and
ground_shade.json with the rectangles they cover. "world" = the whole map at the resolution of ground_colour.png,
"near" = the lap with a margin at a finer resolution (the same rectangle as make_groundmap.py's near tile).

How: a top-down orthographic Cycles render of the walkable surfaces (terrain, road, paving, decks) with direct light
only, once with everything casting shadows and once with nothing casting; the ratio of the two is the shadow, free of
the ground's own slope and colour. Buildings, trees and props are invisible to that camera (the ground is seen right up
to the walls and under the crowns) but cast. Leaves keep their cutout, glass its transparency. Sun direction = the
game's sun (make_sky.py SUN_AZIMUTH / SUN_ELEVATION = CampusWorldBuilder.AddSunAndSky). The sun disc is 1.2 degrees
wide, so shadows are crisp at the foot of things and soften with the caster's height.

Noah's hall: in Unity the hall (NovaHallWorld.fbx) replaces the old main building. If the hall model is found (Unity
folder, or out/), it is loaded here and casts instead of the old building B_w665606788 / BD_w665606788; without it the
old building casts and the script says so.

Limits (one value per map point): where surfaces lie over each other (a bridge over the road, the tunnel), the
upper one counts. The tunnel's own materials are marked fully shaded in materials.json instead (env_materials.py).
"""
import json
import math
import os
import re
import sys

import bpy
import numpy as np
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ground_tiles  # noqa: E402
OUT = os.path.join(HERE, "out")
DATA = json.load(open(os.path.join(HERE, "world_data.json")))
X0, Y0, X1, Y1 = DATA["bounds"]
WORLD_W, NEAR_W = ground_tiles.WORLD_W, ground_tiles.NEAR_W
SUN_DISC = 1.2                                   # degrees
SUN_SAMPLES, SKY_SAMPLES = 24, 96
MAIN_BUILDING = "w665606788"
HALL_FILES = (os.path.join(HERE, "..", "..", "Assets", "_Project", "Visuals", "CampusWorld", "Models", "NovaHallWorld.fbx"),
              os.path.join(OUT, "NovaHallWorld.fbx"))
RECEIVERS = ground_tiles.RECEIVERS               # what the top-down camera sees = what receives the baked shadow
NEVER = ("ColliderOnly", "RouteGuide", "Marker_", "World_Paths_tunnel", "WorldOrigin", "Backdrop_Sea")


def sun_direction():
    src = open(os.path.join(HERE, "make_sky.py")).read()
    az, el = (float(v) for v in re.search(r"SUN_AZIMUTH, SUN_ELEVATION = ([\d.]+), ([\d.]+)", src).groups())
    a, e = math.radians(az), math.radians(el)
    return az, el, (math.sin(a) * math.cos(e), math.cos(a) * math.cos(e), math.sin(e))     # towards the sun (x east, y north)


def load_hall():
    """The hall instead of the old main building. Returns a line for the log."""
    path = next((p for p in HALL_FILES if os.path.exists(p)), None)
    old = [o for o in bpy.data.objects if MAIN_BUILDING in o.name]
    if not path:
        return f"hall model not found: the old main building casts ({len(old)} objects)"
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    turn = Matrix.Rotation(math.pi, 4, "Z")              # the FBX is turned for Unity (build_world.export_for_unity)
    for o in new:
        if o.parent is None:
            o.matrix_world = turn @ o.matrix_world
    bpy.context.view_layer.update()
    meshes = [o for o in new if o.type == "MESH"]
    xs = [(o.matrix_world @ v.co) for o in meshes for v in o.data.vertices]
    lo = [min(p[k] for p in xs) for k in range(3)]
    hi = [max(p[k] for p in xs) for k in range(3)]
    foot = next((b["points"] for b in DATA["buildings"] if str(b["id"]).endswith(MAIN_BUILDING[1:])), None)
    if foot:                                             # the hall must stand on the old building's footprint
        fx, fy = [p[0] for p in foot], [p[1] for p in foot]
        cx, cy = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
        if not (min(fx) - 30 < cx < max(fx) + 30 and min(fy) - 30 < cy < max(fy) + 30):
            for o in new:
                bpy.data.objects.remove(o)
            return "hall model is not on the main building's footprint: ignored, the old main building casts"
    for o in old:
        o.hide_render = True
    for o in meshes:
        o["shade_caster_only"] = True
    return (f"hall: {os.path.basename(path)} casts ({len(meshes)} meshes, x {lo[0]:.0f}..{hi[0]:.0f}, y {lo[1]:.0f}..{hi[1]:.0f}, "
            f"top {hi[2]:.1f} m) instead of {len(old)} objects of the old main building")


def plain_materials():
    """Every material white and matt; cutouts and glass keep their alpha. Only in memory."""
    for mat in bpy.data.materials:
        if not mat.node_tree:
            continue
        b = mat.node_tree.nodes.get("Principled BSDF")
        if not b:
            continue
        for name in ("Base Color", "Roughness", "Metallic", "Normal", "Emission Color", "Emission Strength"):
            sock = b.inputs.get(name)
            if sock is None:
                continue
            for link in list(sock.links):
                mat.node_tree.links.remove(link)
        b.inputs["Base Color"].default_value = (1, 1, 1, 1)
        b.inputs["Roughness"].default_value = 1.0
        b.inputs["Metallic"].default_value = 0.0
        b.inputs["Emission Strength"].default_value = 0.0
        if "Specular IOR Level" in b.inputs:
            b.inputs["Specular IOR Level"].default_value = 0.0


def setup_scene():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    c = scene.cycles
    c.device = "CPU"                                     # the same picture on every machine
    c.use_adaptive_sampling = False
    c.use_denoising = False
    c.max_bounces = 0
    c.diffuse_bounces = c.glossy_bounces = c.transmission_bounces = c.volume_bounces = 0
    c.transparent_max_bounces = 12
    c.seed = 16
    c.pixel_filter_type = "BLACKMAN_HARRIS"
    c.filter_width = 1.5
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "OPEN_EXR"
    scene.render.image_settings.color_depth = "32"
    scene.render.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "Standard"
    scene.render.use_persistent_data = True
    scene.render.threads_mode = "AUTO"
    world = bpy.data.worlds.new("ShadeWorld")            # a plain white sky of our own (strength set per render)
    scene.world = world
    world.use_nodes = True
    tree = world.node_tree
    tree.nodes.clear()
    bg = tree.nodes.new("ShaderNodeBackground")
    tree.links.new(bg.outputs["Background"], tree.nodes.new("ShaderNodeOutputWorld").inputs["Surface"])
    bg.inputs["Color"].default_value = (1, 1, 1, 1)
    cam = bpy.data.objects.new("ShadeCam", bpy.data.cameras.new("ShadeCam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.clip_start, cam.data.clip_end = 1.0, 2000.0
    scene.camera = cam
    sun = bpy.data.objects.new("ShadeSun", bpy.data.lights.new("ShadeSun", "SUN"))
    scene.collection.objects.link(sun)
    sun.data.angle = math.radians(SUN_DISC)
    az, el, d = sun_direction()
    # a sun lamp shines along its -Z: turn -Z onto "away from the sun"
    from mathutils import Vector
    sun.rotation_euler = Vector(d).to_track_quat("Z", "Y").to_euler()
    return scene, cam, sun, bg, (az, el)


def render(scene, cam, rect, width, tag):
    x0, y0, x1, y1 = rect
    height = int(round(width * (y1 - y0) / (x1 - x0)))
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = width, height, 100
    cam.location = ((x0 + x1) / 2, (y0 + y1) / 2, 800.0)
    cam.rotation_euler = (0, 0, 0)
    cam.data.ortho_scale = max(x1 - x0, y1 - y0)
    path = os.path.join(OUT, f"_shade_{tag}.exr")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    px = np.empty(width * height * 4, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    os.remove(path)
    return px.reshape(height, width, 4)[::-1, :, 0].copy()      # rows from the north


def main():
    os.makedirs(OUT, exist_ok=True)
    print("make_shadowmap:", load_hall())
    plain_materials()
    scene, cam, sun, bg, (az, el) = setup_scene()
    seen = hidden = 0
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        if any(k in o.name for k in NEVER):
            o.hide_render = True
        receiver = (o.name.startswith(RECEIVERS) or any(m and m.name == "W_ground_map" for m in o.data.materials)) \
            and not o.get("shade_caster_only")
        o.visible_camera = receiver
        seen += receiver
        hidden += not receiver
    print(f"make_shadowmap: sun azimuth {az:.0f}, elevation {el:.0f}; {seen} receiving objects, {hidden} cast only")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    rects = {"world": ((X0, Y0, X1, Y1), WORLD_W), "near": (ground_tiles.near_rect(), NEAR_W)}
    result = {}
    for tag, (rect, width) in rects.items():
        scene.cycles.max_bounces = scene.cycles.diffuse_bounces = 0
        sun.hide_render = False
        sun.data.energy = 3.0
        bg.inputs["Strength"].default_value = 0.0
        scene.cycles.samples = SUN_SAMPLES
        layers = {}
        for cast in (True, False):
            for o in meshes:
                o.visible_shadow = cast
            layers[cast] = render(scene, cam, rect, width, f"{tag}_sun_{int(cast)}")
        shadow = np.where(layers[False] > 1e-4, layers[True] / np.maximum(layers[False], 1e-4), 1.0)
        # the sky: a white sky and one bounce, so a point is as bright as the share of the sky it sees (open flat
        # ground = 1); things in the way are black (no light of their own, no second bounce)
        scene.cycles.max_bounces = scene.cycles.diffuse_bounces = 1
        sun.hide_render = True
        bg.inputs["Strength"].default_value = 1.0
        scene.cycles.samples = SKY_SAMPLES
        sky = render(scene, cam, rect, width, f"{tag}_sky")
        out = [np.clip(shadow, 0.0, 1.0), np.clip(sky, 0.0, 1.0)]
        arr = np.round(np.dstack(out) * 255.0).astype(np.uint8)
        np.save(os.path.join(OUT, f"ground_shade_{tag}.npy"), arr)
        result[tag] = {"rect": list(rect), "size": [arr.shape[1], arr.shape[0]]}
        print(f"make_shadowmap: {tag} {arr.shape[1]} x {arr.shape[0]} px, {(rect[2] - rect[0]) / arr.shape[1]:.2f} m per pixel; "
              f"in the sun's shadow {100.0 * float((arr[..., 0] < 128).mean()):.1f} %, mean sky {arr[..., 1].mean() / 255.0:.2f}")
    result["sun"] = {"azimuth": az, "elevation": el, "disc": SUN_DISC}
    json.dump(result, open(os.path.join(OUT, "ground_shade.json"), "w"), indent=1)
    print("make_shadowmap: done")


main()
