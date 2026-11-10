"""Beauty renders (EEVEE): physical sky, low afternoon sun from the south-west (the walk was at 16:00),
ray-traced reflections, cameras at kart height along the route plus one aerial view.
Output: out/beauty_<name>.png. Only for looking at the world; nothing here is exported to Unity.
"""
import glob
import math
import os

import bpy
from mathutils import Vector

import env_common as E

SUN_AZIMUTH = 225.0      # degrees clockwise from north: south-west
SUN_ELEVATION = 24.0
RES = (1920, 1080)


def setup_world():
    scene = bpy.context.scene
    world = bpy.data.worlds.get("Sky") or bpy.data.worlds.new("Sky")
    scene.world = world
    world.use_nodes = True
    nodes, links = world.node_tree.nodes, world.node_tree.links
    nodes.clear()
    sky = nodes.new("ShaderNodeTexSky")
    try:
        sky.sky_type = "MULTIPLE_SCATTERING"
    except TypeError:
        sky.sky_type = "HOSEK_WILKIE"
    if hasattr(sky, "sun_elevation"):
        sky.sun_elevation = math.radians(SUN_ELEVATION)
        sky.sun_rotation = math.radians(-SUN_AZIMUTH)
        sky.sun_disc = False
    if hasattr(sky, "air_density"):
        sky.air_density = 1.2
    if hasattr(sky, "aerosol_density"):
        sky.aerosol_density = 1.6
    bg = nodes.new("ShaderNodeBackground")
    bg.inputs["Strength"].default_value = 0.3
    out = nodes.new("ShaderNodeOutputWorld")
    links.new(sky.outputs["Color"], bg.inputs["Color"])
    links.new(bg.outputs["Background"], out.inputs["Surface"])


    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 4.0
    sun_data.angle = math.radians(1.5)
    sun_data.color = (1.0, 0.95, 0.86)
    sun = bpy.data.objects.new("Sun", sun_data)
    scene.collection.objects.link(sun)
    az, el = math.radians(SUN_AZIMUTH), math.radians(SUN_ELEVATION)
    towards_sun = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
    sun.rotation_euler = (-towards_sun).to_track_quat("-Z", "Y").to_euler()


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = RES
    scene.render.film_transparent = False
    ev = scene.eevee
    for attr, value in (("taa_render_samples", 48), ("use_raytracing", True), ("use_shadows", True),
                        ("shadow_ray_count", 2), ("shadow_step_count", 8), ("use_fast_gi", True),
                        ("fast_gi_distance", 6.0), ("use_gtao", True), ("gtao_distance", 2.0)):
        if hasattr(ev, attr):
            try:
                setattr(ev, attr, value)
            except (TypeError, AttributeError):
                pass
    if hasattr(ev, "ray_tracing_options"):
        ev.ray_tracing_options.resolution_scale = "2"
    vs = scene.view_settings
    try:
        vs.view_transform = "AgX"
        vs.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    vs.exposure = -1.1


def passage_lights():
    col = E.collection("Render_Lights")
    pts = [p for p in E.route_points() if p["zone"] == "atrium"]
    for k, p in enumerate(pts[::5]):
        light = bpy.data.lights.new(f"HallLight{k}", type="AREA")
        light.energy = 900.0
        light.size = 4.0
        light.color = (1.0, 0.97, 0.9)
        obj = bpy.data.objects.new(f"HallLight{k}", light)
        obj.location = (p["x"], p["y"], p["track_z"] + 6.7)
        col.objects.link(obj)


def tunnel_lights():
    col = E.collection("Render_Lights")
    pts = [p for p in E.route_points() if p["tunnel"]]
    for k, p in enumerate(pts[1:-1:5]):
        light = bpy.data.lights.new(f"TunnelLight{k}", type="POINT")
        light.energy = 120.0
        light.color = (1.0, 0.92, 0.78)
        light.shadow_soft_size = 0.5
        obj = bpy.data.objects.new(f"TunnelLight{k}", light)
        obj.location = (p["x"], p["y"], p["track_z"] + 3.4)
        col.objects.link(obj)


# Luc's kart roster (.blend, not in the repo): set NOVA_KART_ROSTER, else it is looked for in the Drive folder.
ROSTER = os.environ.get("NOVA_KART_ROSTER") or next(iter(sorted(glob.glob(os.path.expanduser(
    "~/Library/CloudStorage/GoogleDrive-*/My Drive/Nova SBE/Semester 1/Introduction to Programming/"
    "Project/Nova Kart/Art/Blender/NovaKart_Roster.blend")))), "")


def load_hero_kart():
    """Appends Luc's Nova Classic kart + Rookie driver from the roster file under one empty 'HeroKart'."""
    if not os.path.exists(ROSTER):
        return None
    with bpy.data.libraries.load(ROSTER, link=False) as (src, dst):
        dst.objects = [n for n in src.objects]
    col = E.collection("Hero")
    keep = set()
    for root in ("Kart_Classic", "Driver_Rookie"):
        obj = bpy.data.objects.get(root)
        if obj:
            keep.add(obj)
            keep.update(obj.children_recursive)
    hero = bpy.data.objects.new("HeroKart", None)
    col.objects.link(hero)
    for obj in [o for o in dst.objects if o is not None]:
        if obj in keep:
            col.objects.link(obj)
            if obj.parent is None:
                obj.location = (0.0, 0.0, obj.location.z)
                obj.parent = hero
        else:
            bpy.data.objects.remove(obj)
    return hero


def place_hero(hero, s, lift=0.0, pitch=0.0):
    a, b = point_at(s), point_at(s + 4)
    dx, dy = b["x"] - a["x"], b["y"] - a["y"]
    hero.location = (a["x"], a["y"], a["track_z"] + 0.06 + lift)
    hero.rotation_euler = (pitch, 0.0, math.atan2(dx, -dy))   # the roster kart faces -Y


def point_at(s):
    pts = E.route_points()
    return min(pts, key=lambda p: abs(p["s"] - (s % E.ROUTE["length_m"])))


def route_camera(s, ahead=24.0, height=2.6, side=0.0, lens=24.0, look_up=0.0):
    a, b = point_at(s), point_at(s + ahead)
    nxt = point_at(s + 2)
    dx, dy = nxt["x"] - a["x"], nxt["y"] - a["y"]
    d = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / d, dx / d
    loc = Vector((a["x"] + nx * side, a["y"] + ny * side, a["track_z"] + height))
    target = Vector((b["x"], b["y"], b["track_z"] + 1.2 + look_up))
    return loc, target, lens


def render_shots():
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("BeautyCam")
    cam = bpy.data.objects.new("BeautyCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam_data.clip_start, cam_data.clip_end = 0.1, 6000
    pts = E.route_points()
    jump = E.ROUTE["jump"]["s"]
    shots = {
        "aerial": (Vector((-310, -330, 150)), Vector((40, 40, 5)), 30),
        "tunnel_inside": route_camera(30, ahead=30, height=2.2, lens=22),
        "ramp_exit": route_camera(80, ahead=40, height=2.4, lens=24, look_up=2.0),
        "steps": route_camera(118, ahead=40, height=2.4, lens=24, look_up=2.5),
        "upper_plaza": route_camera(200, ahead=40, height=2.6, side=-1.5, lens=24),
        "atrium": route_camera(372, ahead=36, height=2.4, lens=22),
        "terrace_jump": route_camera(jump - 30, ahead=50, height=3.2, lens=24, look_up=-3.0),
        "coastal_road": route_camera(640, ahead=50, height=2.6, lens=26),
        "promenade_fort": route_camera(960, ahead=60, height=2.4, side=-2.0, lens=26),
    }
    hero = load_hero_kart()
    if hero:
        jump = E.ROUTE["jump"]
        j = point_at(jump["s"])
        hero_shots = {
            "hero_promenade": (980, 0.0, 0.0, route_camera(973, ahead=14, height=1.3, side=1.4, lens=32)),
            "hero_jump": (jump["s"] + 5, 1.6, math.radians(-6), (Vector((j["x"] - 30, j["y"] - 9, j["track_z"] - 2.4)),
                                                                Vector((j["x"] - 5, j["y"] - 0.5, j["track_z"] + 2.0)), 30)),
            "hero_tunnel_exit": (92, 0.0, math.radians(-4), route_camera(78, ahead=16, height=1.4, side=-1.2, lens=28)),
        }
    else:
        hero_shots = {}
    for name, (s_kart, lift, pitch, (loc, target, lens)) in hero_shots.items():
        place_hero(hero, s_kart, lift, pitch)
        cam.location = loc
        cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
        cam_data.lens = lens
        scene.render.filepath = os.path.join(E.OUT, f"beauty_{name}.png")
        bpy.ops.render.render(write_still=True)
        print(f"  rendered beauty_{name}.png")
    if hero:
        hero.location = (0, 0, -500)                         # out of the way for the other shots
    for name, (loc, target, lens) in shots.items():
        cam.location = loc
        cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
        cam_data.lens = lens
        scene.render.filepath = os.path.join(E.OUT, f"beauty_{name}.png")
        bpy.ops.render.render(write_still=True)
        print(f"  rendered beauty_{name}.png")
    bpy.data.objects.remove(cam)


def render_all():
    setup_world()
    setup_render()
    tunnel_lights()
    passage_lights()
    render_shots()
