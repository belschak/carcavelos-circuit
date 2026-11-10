"""Drive-along QA renders: what a player sees from the chase camera, every STEP metres around the lap.

Run:  Blender -b -P render_drive.py -- [blend] [out_dir] [step_m] [width_px] [samples] [only_s, e.g. "128,496"]
Defaults: out/CampusWorld.blend, out/drive, 16 m, 1280 px (16:9), 24 EEVEE samples.

Writes drive_<s>.png per stop (s = metres along the lap, 4 digits) and drive_index.json with, per frame:
position, zone, surface, level, and the nearest frame from the team's walk videos that looks the same way
(for comparing with the real campus). The walk frames show people: they stay local, never in the repo.
"""
import csv
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BLEND = argv[0] if len(argv) > 0 and argv[0] else os.path.join(HERE, "out", "CampusWorld.blend")
OUT_DIR = argv[1] if len(argv) > 1 and argv[1] else os.path.join(HERE, "out", "drive")
STEP = float(argv[2]) if len(argv) > 2 else 16.0
WIDTH = int(argv[3]) if len(argv) > 3 else 1280
SAMPLES = int(argv[4]) if len(argv) > 4 else 24
ONLY = [float(v) for v in argv[5].split(",")] if len(argv) > 5 and argv[5] else None   # e.g. "128,496,1120"
MEDIA = os.path.expanduser("~/Documents/nova/campus-media/frames")

bpy.ops.wm.open_mainfile(filepath=BLEND)
import env_common as E  # noqa: E402
import env_render as R  # noqa: E402


def relink_images():
    """Texture paths in the .blend are relative; a copy in another folder would render magenta."""
    fixed = missing = 0
    for img in bpy.data.images:
        if img.source != "FILE" or not img.filepath or img.packed_file:
            continue
        if os.path.exists(bpy.path.abspath(img.filepath)):
            continue
        cand = os.path.join(E.TEXTURES, os.path.basename(bpy.path.abspath(img.filepath)))
        if os.path.exists(cand):
            img.filepath = cand
            img.reload()
            fixed += 1
        else:
            missing += 1
            print(f"  MISSING texture: {img.filepath}")
    print(f"relinked {fixed} textures, {missing} missing")


relink_images()

BACK, HEIGHT, AHEAD, LENS = 6.5, 2.3, 10.0, 22.0     # chase camera: 6.5 m behind, 2.3 m up, wide lens


def heading(a, b):
    return math.degrees(math.atan2(b["x"] - a["x"], b["y"] - a["y"])) % 360   # clockwise from north


def walk_frames():
    """Walk-video frames with position and walking direction (from the neighbouring frames)."""
    path = os.path.join(MEDIA, "frames_positions.csv")
    if not os.path.exists(path):
        return []
    rows = list(csv.DictReader(open(path)))
    by_video = {}
    for r in rows:
        by_video.setdefault(r["video"], []).append(r)
    out = []
    for video, rs in by_video.items():
        rs.sort(key=lambda r: float(r["video_s"]))
        for k, r in enumerate(rs):
            a, b = rs[max(k - 3, 0)], rs[min(k + 3, len(rs) - 1)]
            dx, dy = float(b["x_east"]) - float(a["x_east"]), float(b["y_north"]) - float(a["y_north"])
            hd = math.degrees(math.atan2(dx, dy)) % 360 if math.hypot(dx, dy) > 1.0 else None
            out.append({"file": os.path.join(MEDIA, video, r["file"]), "x": float(r["x_east"]),
                        "y": float(r["y_north"]), "heading": hd, "place": r["place"]})
    return out


def nearest_walk(frames, x, y, hd):
    best = None
    for f in frames:
        d = math.hypot(f["x"] - x, f["y"] - y)
        if d > 25 or f["heading"] is None:
            continue
        diff = abs((f["heading"] - hd + 180) % 360 - 180)
        if diff > 60:
            continue
        score = d + diff / 6
        if best is None or score < best[0]:
            best = (score, f, d, diff)
    if not best:
        return None
    _, f, d, diff = best
    return {"file": f["file"], "dist_m": round(d, 1), "heading_diff_deg": round(diff), "place": f["place"]}


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    R.setup_world()
    R.setup_render()
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = WIDTH, round(WIDTH * 9 / 16)
    if hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = SAMPLES
    R.tunnel_lights()
    R.passage_lights()
    for obj in bpy.data.objects:                       # the route guide never shows
        if obj.users_collection and any(c.name == "Track_RouteGuide" for c in obj.users_collection):
            obj.hide_render = True
    cam_data = bpy.data.cameras.new("DriveCam")
    cam = bpy.data.objects.new("DriveCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam_data.clip_start, cam_data.clip_end, cam_data.lens = 0.1, 6000, LENS
    hero = R.load_hero_kart()
    frames = walk_frames()
    length = E.ROUTE["length_m"]
    index = []
    stops = ONLY if ONLY is not None else [k * STEP for k in range(int((length - 1) // STEP) + 1)]
    for s in stops:
        k, c, t = R.point_at(s), R.point_at(s - BACK), R.point_at(s + AHEAD)
        loc = Vector((c["x"], c["y"], max(c["track_z"], k["track_z"]) + HEIGHT))
        target = Vector((t["x"], t["y"], t["track_z"] + 1.0))
        cam.location = loc
        cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
        if hero:
            R.place_hero(hero, s)
        name = f"drive_{int(round(s)):04d}.png"
        scene.render.filepath = os.path.join(OUT_DIR, name)
        bpy.ops.render.render(write_still=True)
        hd = heading(k, R.point_at(s + 4))
        index.append({"s": round(s), "file": name, "x": round(k["x"], 1), "y": round(k["y"], 1),
                      "track_z": round(k["track_z"], 2), "heading_deg": round(hd), "zone": k.get("zone"),
                      "surface": k.get("surface"), "level": k.get("level"), "building": k.get("building"),
                      "tunnel": k.get("tunnel"), "walk_ref": nearest_walk(frames, k["x"], k["y"], hd)})
        print(f"  {name}  zone={k.get('zone')}  surface={k.get('surface')}", flush=True)
    json.dump({"blend": BLEND, "step_m": STEP, "camera": {"back_m": BACK, "height_m": HEIGHT, "lens_mm": LENS},
               "frames": index}, open(os.path.join(OUT_DIR, "drive_index.json"), "w"), indent=1)
    print(f"done: {len(index)} frames in {OUT_DIR}")


main()
