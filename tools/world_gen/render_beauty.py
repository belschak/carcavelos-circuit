"""Opens out/CampusWorld.blend and renders the EEVEE beauty shots (env_render.py).

Run:  /Applications/Blender.app/Contents/MacOS/Blender -b -P render_beauty.py
"""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "out", "CampusWorld.blend"))
import env_render  # noqa: E402

env_render.render_all()
print("done")
