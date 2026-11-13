"""Unity bundle: everything the Unity importer (CampusWorldBuilder.cs) needs, already in Unity axes.

Run (after build_world.py):  ~/Documents/nova/.media-tools/bin/python export_unity_bundle.py [out_dir]
Default out_dir: out/unity_export. Layout = the Unity folder Assets/_Project/Visuals/CampusWorld/:
  Models/    CampusWorld.fbx, TrackSurface.fbx, RouteGuide.fbx, Dressing_Prototypes.fbx (from build_world.py)
  Textures/  albedo and normal maps as made by make_textures.py; for every roughness map a *_metallic_smoothness.png
             (RGBA: metallic 0, alpha = 1 - roughness), the form URP Lit reads ("Metallic Alpha")
  Data/      materials.json  one entry per material name in the FBX files (colour, maps, smoothness, emission)
             dressing.json   850-odd trees and props: prototype, position, yaw/pitch in degrees, scale
             track.json      route points, start line, checkpoints, spawn (for the race placeholders)
             check.json      world bounds of sample objects, for the importer's self-test

Axes: Unity x = east, y = up, z = north (Blender world x, y, z -> Unity x, z, y), checked in Unity 6000.3.28f1.
Rotations: Blender rot_z (counter-clockwise from above) -> Unity yaw = -rot_z; Blender rot_y (railing pitch) ->
Unity roll about z = -rot_y, applied before the yaw. The self-test in Unity compares bounds to prove it.
"""
import json
import math
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
BUNDLE = sys.argv[1] if len(sys.argv) > 1 else os.path.join(OUT, "unity_export")
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
MODELS = ["CampusWorld.fbx", "TrackSurface.fbx", "RouteGuide.fbx", "Dressing_Prototypes.fbx"]
ROAD_LIFT = 0.06            # env_track.ROAD_LIFT: road surface above track_z
CHECKPOINTS = 12
EXTRA_TEXTURES = ["sky_panorama.png"]   # make_sky.py: skybox + reflections (CampusWorldBuilder.AddSunAndSky)

# The grass tufts have no Blender material (CampusWorldBuilder builds them): their entry for materials.json.
GRASS_TUFT = {"name": "W_grass_tuft", "colour": [1.0, 1.0, 1.0], "metallic": 0.0, "albedo": "grass_tuft_cutout_albedo.png",
              "normal": None, "normal_strength": 1.0, "emission": None, "alpha": 1.0, "metallic_smoothness": None,
              "smoothness": 0.1, "alpha_clip": 0.45, "double_sided": True,
              "wind": {"shader": "CarcavelosCircuit/Foliage", "bend": 0.0, "flutter": 0.07, "upright": True,
                       "shrink_from": 52.0, "shrink_to": 64.0}}       # tufts sink into the lawn before their cell is hidden

DUMP = r'''
import bpy, json, sys
from mathutils import Vector
out = sys.argv[sys.argv.index("--") + 1]
INSTANCES = ("World_Vegetation", "World_Props")
bpy.context.view_layer.update()      # matrix_world of objects placed by script is only fresh after this


def srgb(c):
    return [round(min(max(v, 0.0), 1.0) ** (1 / 2.2) if v > 0.0031308 else v * 12.92, 4) for v in c[:3]]


def linked_image(sock):
    if not sock.is_linked:
        return None
    node = sock.links[0].from_node
    if node.type == "TEX_IMAGE" and node.image:
        return bpy.path.basename(node.image.filepath)
    if node.type == "NORMAL_MAP":
        return linked_image(node.inputs["Color"])
    return None


mats = {}
for obj in bpy.data.objects:
    if obj.type != "MESH" or any(c.name in INSTANCES for c in obj.users_collection):
        continue
    for slot in obj.material_slots:
        m = slot.material
        if not m or m.name in mats:
            continue
        e = {"name": m.name, "colour": srgb(m.diffuse_color), "roughness": 0.85, "metallic": 0.0,
             "albedo": None, "rough": None, "normal": None, "normal_strength": 1.0, "emission": None, "alpha": 1.0,
             # leaf cutouts (env_vegetation.cutout_material): alpha clipping cutoff (0 = off), both faces drawn
             "alpha_clip": round(float(m.get("alpha_clip", 0.0)), 3), "double_sided": bool(m.get("double_sided", 0))}
        if m.get("wind_shader"):        # swaying trees (env_vegetation.set_wind): shader + metres of bend and flutter
            e["wind"] = {"shader": m["wind_shader"], "bend": round(float(m["wind_bend"]), 3),
                         "flutter": round(float(m["wind_flutter"]), 3)}
        if m.get("ground_shader"):      # detailed lawn and sand (env_materials.upgrade_all): shader + its textures
            e["ground"] = {"shader": m["ground_shader"], "mask": m["ground_mask"], "detail": m["ground_detail"],
                           "detail_normal": m["ground_detail_normal"]}
        if m.get("water_shader"):       # the moving sea (env_backdrop.sea_water_data): shader + its textures and numbers
            e["water"] = {"shader": m["water_shader"], "normal": m["water_normal"], "shore_map": m["water_shore_map"],
                          "shore_rect": [round(v, 3) for v in m["water_shore_rect"]],
                          "shore_range": float(m["water_shore_range"]), "coast": float(m["water_coast"])}
        b = m.node_tree.nodes.get("Principled BSDF") if m.node_tree else None
        if b:
            e["colour"] = srgb(b.inputs["Base Color"].default_value)
            e["roughness"] = round(b.inputs["Roughness"].default_value, 3)
            e["metallic"] = round(b.inputs["Metallic"].default_value, 3)
            e["alpha"] = round(b.inputs["Alpha"].default_value, 3)
            e["albedo"] = linked_image(b.inputs["Base Color"])
            e["rough"] = linked_image(b.inputs["Roughness"])
            e["normal"] = linked_image(b.inputs["Normal"])
            if b.inputs["Normal"].is_linked:
                e["normal_strength"] = round(b.inputs["Normal"].links[0].from_node.inputs["Strength"].default_value, 3)
            strength = b.inputs["Emission Strength"].default_value
            col = b.inputs["Emission Color"].default_value
            if strength > 0 and max(col[:3]) > 0.01:
                e["emission"] = {"colour": srgb(col), "strength": round(strength, 3)}
        mats[m.name] = e


def bounds(obj):
    ws = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = [min(w[k] for w in ws) for k in range(3)]
    hi = [max(w[k] for w in ws) for k in range(3)]
    return [lo[0], lo[2], lo[1]], [hi[0], hi[2], hi[1]]       # Unity axes: x, up, north


checks = []
for name in INSTANCES:
    col = bpy.data.collections.get(name)
    objs = sorted(col.all_objects, key=lambda o: o.name) if col else []
    seen = {}
    for o in objs:
        kind = o.name.rsplit(".", 1)[0]
        if seen.get(kind, 0) >= (6 if kind == "Prop_RailingSegment" else 2):
            continue
        if kind == "Prop_RailingSegment" and abs(o.rotation_euler.y) < 0.02 and seen.get(kind, 0) >= 2:
            continue                                             # prefer sloped railings
        seen[kind] = seen.get(kind, 0) + 1
        lo, hi = bounds(o)
        checks.append({"name": o.name, "min": lo, "max": hi})
objects = []
for name in ("Track_JumpKicker", "Landmark_FortSaoJuliao", "Tunnel_Shell", "Track_Road"):
    o = bpy.data.objects.get(name)
    if o:
        lo, hi = bounds(o)
        objects.append({"name": name, "min": lo, "max": hi})
json.dump({"materials": list(mats.values()), "instances": checks, "objects": objects}, open(out, "w"), indent=1)
'''


def smoothness_map(src, dst):
    rough = np.asarray(Image.open(src).convert("L"), np.uint8)
    rgba = np.zeros(rough.shape + (4,), np.uint8)
    rgba[..., 3] = 255 - rough
    Image.fromarray(rgba, "RGBA").save(dst)


def unity_dressing():
    out = []
    for fname in ("placements_vegetation.json", "placements_props.json"):
        data = json.load(open(os.path.join(OUT, fname)))
        for k, p in enumerate(data["placements"]):
            out.append({"name": f"{p['proto']}.{k:04d}", "proto": p["proto"],
                        "x": round(p["x"], 3), "y": round(p["z"], 3), "z": round(p["y"], 3),
                        "yaw": round(-math.degrees(p["rot_z"]), 3), "roll": round(-math.degrees(p.get("rot_y", 0.0)), 3),
                        "scale": round(p["scale"], 4)})
    return {"note": "Unity axes (x east, y up, z north); rotation = Euler(0, yaw, 0) * Euler(0, 0, roll)", "items": out}


def heading_change(pts, i, span):
    n = len(pts)
    a, b, c = pts[(i - span) % n], pts[i], pts[(i + span) % n]
    h1 = math.atan2(b["y"] - a["y"], b["x"] - a["x"])
    h2 = math.atan2(c["y"] - b["y"], c["x"] - b["x"])
    return abs((h2 - h1 + math.pi) % (2 * math.pi) - math.pi)


def unity_track():
    route = json.load(open(os.path.join(HERE, "route.json")))
    pts, n = route["points"], len(route["points"])
    jump = route["jump"]["index"]
    # start/finish: the straightest point on the promenade (long straight by the beach, fort in view)
    if "start_index" in route:                          # waypoint routes: start/finish = point 0 (Noah's #0)
        start = route["start_index"]
    else:
        cands = [i for i, p in enumerate(pts) if p["zone"] == "promenade" and not p["tunnel"]]
        start = min(cands, key=lambda i: heading_change(pts, i, 15))
    step = n / CHECKPOINTS
    cps = []
    for c in range(CHECKPOINTS):
        i = int(round(start + c * step)) % n
        while (i - jump) % n < 10 or (jump - i) % n < 3 or pts[i]["building"] and c > 0 and False:
            i = (i + 1) % n                                    # never in the air after the terrace edge
        cps.append(i)
    spawn = (start - 4) % n                                   # 8 m behind the line

    def u(p):
        return {"x": round(p["x"], 3), "y": round(p["track_z"] + ROAD_LIFT, 3), "z": round(p["y"], 3),
                "width": p.get("width", 8.0), "s": p["s"], "zone": p["zone"], "tunnel": bool(p["tunnel"])}
    return {"note": "Unity axes; y = road surface height. checkpoints[0] = start/finish line. Indices into points.",
            "length_m": route["length_m"], "jump_index": jump, "start_index": start, "spawn_index": spawn,
            "checkpoints": cps, "points": [u(p) for p in pts]}


def main():
    for m in MODELS:
        if not os.path.exists(os.path.join(OUT, m)):
            sys.exit(f"missing out/{m}: run build_world.py first")
    with tempfile.TemporaryDirectory() as tmp:
        script, dump = os.path.join(tmp, "dump.py"), os.path.join(tmp, "dump.json")
        open(script, "w").write(DUMP)
        subprocess.run([BLENDER, "-b", os.path.join(OUT, "CampusWorld.blend"), "-P", script, "--", dump],
                       check=True, capture_output=True)
        info = json.load(open(dump))
    for sub in ("Models", "Textures", "Data"):
        os.makedirs(os.path.join(BUNDLE, sub), exist_ok=True)
    for m in MODELS:
        shutil.copy2(os.path.join(OUT, m), os.path.join(BUNDLE, "Models", m))
    tex = os.path.join(HERE, "textures")
    for e in info["materials"]:
        for key in ("albedo", "normal"):
            if e[key]:
                shutil.copy2(os.path.join(tex, e[key]), os.path.join(BUNDLE, "Textures", e[key]))
        for key in ("mask", "detail", "detail_normal"):        # textures of the ground shader
            if e.get("ground") and os.path.exists(os.path.join(tex, e["ground"][key])):
                shutil.copy2(os.path.join(tex, e["ground"][key]), os.path.join(BUNDLE, "Textures", e["ground"][key]))
        for key in ("normal", "shore_map"):                    # textures of the sea shader
            if e.get("water") and os.path.exists(os.path.join(tex, e["water"][key])):
                shutil.copy2(os.path.join(tex, e["water"][key]), os.path.join(BUNDLE, "Textures", e["water"][key]))
        if e["rough"]:
            ms = e["rough"].replace("_rough.png", "_metallic_smoothness.png")
            smoothness_map(os.path.join(tex, e["rough"]), os.path.join(BUNDLE, "Textures", ms))
            e["metallic_smoothness"] = ms
        else:
            e["metallic_smoothness"] = None
        e["smoothness"] = round(1.0 - e.pop("roughness"), 3)
        del e["rough"]
    for extra in EXTRA_TEXTURES:                    # not on a material: the importer uses them by name
        if os.path.exists(os.path.join(tex, extra)):
            shutil.copy2(os.path.join(tex, extra), os.path.join(BUNDLE, "Textures", extra))
    data = os.path.join(BUNDLE, "Data")
    tufts = os.path.join(OUT, "grass_tufts.json")            # make_grass_tufts.py: 3D grass tufts, built in Unity
    if os.path.exists(tufts):
        shutil.copy2(tufts, os.path.join(data, "grass_tufts.json"))
        shutil.copy2(os.path.join(tex, GRASS_TUFT["albedo"]), os.path.join(BUNDLE, "Textures", GRASS_TUFT["albedo"]))
        info["materials"].append(dict(GRASS_TUFT))
    json.dump({"materials": info["materials"]}, open(os.path.join(data, "materials.json"), "w"), indent=1)
    dressing = unity_dressing()
    json.dump(dressing, open(os.path.join(data, "dressing.json"), "w"), indent=0)
    track = unity_track()
    json.dump(track, open(os.path.join(data, "track.json"), "w"), indent=0)
    json.dump({"instances": info["instances"], "objects": info["objects"]}, open(os.path.join(data, "check.json"), "w"), indent=1)
    size = sum(os.path.getsize(os.path.join(r, f)) for r, _, fs in os.walk(BUNDLE) for f in fs)
    print(f"bundle: {BUNDLE}\n  {len(info['materials'])} materials, {len(dressing['items'])} dressing items, "
          f"{len(track['checkpoints'])} checkpoints (start s = {track['points'][track['start_index']]['s']:.0f} m), "
          f"{len(info['instances'])} check instances, {size / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
