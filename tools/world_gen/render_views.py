"""Fixed-camera before/after views (workbench, flat texture colours + soft light). V10c.
Run:  Blender -b <blend> -P render_views.py -- <out_dir> <tag> [view,...]
Views are defined in VIEWS as (location, look-at point, lens mm). Same cameras for every run, so before/after match."""
import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
OUT, TAG = argv[0], argv[1]
ONLY = argv[2].split(",") if len(argv) > 2 else None
VIEWS = {
    "spine_roof_se": ((150, -75, 62), (45, 70, 14), 30),
    "atrium_roof_nw": ((-60, 215, 50), (45, 125, 14), 30),
    "portal_north": ((93.8, -69.2, 12.5), (70.6, -64.8, 7.0), 22),
    "portal_south": ((-17.3, -123.6, 9.0), (5.7, -118.2, 5.0), 22),
    "roundabout": ((-70.0, -32.0, 12.0), (-107.2, -63.5, 0.5), 24),
    "milestone_j": ((-62, 108, 26), (-55, 172, 9), 30),
}
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading
sh.light = "STUDIO"
sh.color_type = "TEXTURE"
sc.display.render_aa = "8"
sc.render.resolution_x, sc.render.resolution_y = 1280, 800
os.makedirs(OUT, exist_ok=True)
for name, (loc, tgt, lens) in VIEWS.items():
    if ONLY and name not in ONLY:
        continue
    cam_data = bpy.data.cameras.new(name)
    cam_data.lens = lens
    cam_data.clip_end = 2000
    cam = bpy.data.objects.new(name, cam_data)
    sc.collection.objects.link(cam)
    cam.location = loc
    d = Vector(tgt) - Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.filepath = os.path.join(OUT, f"{name}_{TAG}.png")
    bpy.ops.render.render(write_still=True)
    print("WROTE", sc.render.filepath)
