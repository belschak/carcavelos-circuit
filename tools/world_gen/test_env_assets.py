"""Builds, checks and previews the vegetation and street-prop layer (env_vegetation.py, env_props.py).

Run:  /Applications/Blender.app/Contents/MacOS/Blender -b -P test_env_assets.py
Out:  out/assets_lineup.png       every prototype side by side (EEVEE, sun + sky; Workbench if EEVEE fails)
      out/assets_placements.png   top-down maps of all placements over the world (matplotlib)
      out/assets_prototypes.blend the prototypes only (Proto_Vegetation, Proto_Props), to inspect or export
      out/placements_vegetation.json, out/placements_props.json
      console: table of prototype / triangles / instances, and every rule check.

The map is drawn by this same file under the media-tools Python (it has matplotlib, Blender's Python has not):
    ~/Documents/nova/.media-tools/bin/python test_env_assets.py --map
"""
import json
import math
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
MEDIA_PY = os.path.expanduser("~/Documents/nova/.media-tools/bin/python")
LINEUP_PNG = os.path.join(OUT, "assets_lineup.png")
MAP_PNG = os.path.join(OUT, "assets_placements.png")

try:
    import bpy                      # inside Blender
except ImportError:
    bpy = None


# =====================================================================================================
# Map (media-tools Python, matplotlib)
# =====================================================================================================
# Categorical slots 1-3 of the validated default palette (dataviz skill): at most three per panel, because a
# point map compares every pair of colours. Ink for text and railings, neutral context underneath.
SLOTS = ("#2a78d6", "#eb6834", "#1baf7a")
INK, INK_2 = "#0b0b0b", "#52514e"
GROUND_TINT = {"ground": "#f1efe9", "grass": "#e3eedb", "wood": "#d3e4c9", "paved": "#e6e6e3", "pitch": "#dcead2",
               "sand": "#f6ead0", "water": "#d6e7f5", "sea": "#cfe2f3"}
PANELS = (  # title, [(label, prototype names, colour)], view ("world" or (x0, y0, x1, y1)), marker size
    ("Trees", [("Umbrella pine", ("Veg_UmbrellaPine", "Veg_UmbrellaPine_B"), 0),
               ("Palm", ("Veg_Palm", "Veg_Palm_B"), 1), ("Olive", ("Veg_Olive", "Veg_Olive_B"), 2)], "world", 9),
    ("Shrubs and grass", [("Shrub", ("Veg_Shrub", "Veg_Shrub_B"), 0), ("Pampas grass", ("Veg_Grass",), 1)],
     "world", 7),
    ("Lamps and railings", [("Campus lamp", ("Prop_LampCampus",), 0), ("Road lamp", ("Prop_LampRoad",), 1)],
     "route", 26),
    ("Benches, bins, bollards", [("Bench", ("Prop_Bench",), 0), ("Bin", ("Prop_Bin",), 1),
                                 ("Bollard", ("Prop_Bollard",), 2)], "route", 26),
    ("Terrace deck (zoom)", [("Lounge chair", ("Prop_LoungeChair",), 0)], (-25.0, 30.0, 55.0, 90.0), 40),
    ("Promenade and beach bar (zoom)", [("Parasol", ("Prop_Parasol",), 0), ("Bench", ("Prop_Bench",), 1),
                                        ("Road lamp", ("Prop_LampRoad",), 2)], (-150.0, -185.0, -20.0, -110.0), 40),
)


def draw_map():
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.colors import ListedColormap
    from matplotlib.lines import Line2D

    data = json.load(open(os.path.join(HERE, "world_data.json")))
    route = json.load(open(os.path.join(HERE, "route.json")))["points"]
    placed = []
    for name in ("placements_vegetation.json", "placements_props.json"):
        placed += json.load(open(os.path.join(OUT, name)))["placements"]
    x0, y0, x1, y1 = data["bounds"]
    grid = data["grid"]
    classes = list(GROUND_TINT)
    image = [[classes.index(c) for c in row] for row in grid["classes"]]
    rx = [p["x"] for p in route]
    ry = [p["y"] for p in route]
    route_view = (min(rx) - 30, min(ry) - 30, max(rx) + 30, max(ry) + 30)

    plt.rcParams.update({"font.family": "DejaVu Sans", "font.size": 9, "axes.titlesize": 11,
                         "axes.titleweight": "bold", "axes.titlecolor": INK, "axes.edgecolor": "#c9c8c2",
                         "xtick.color": INK_2, "ytick.color": INK_2})
    fig, axes = plt.subplots(2, 3, figsize=(19.2, 12.0), dpi=100)
    for ax, (title, series, view, size) in zip(axes.flat, PANELS):
        ax.imshow(image, origin="lower", interpolation="nearest", vmin=0, vmax=len(classes) - 1,
                  cmap=ListedColormap([GROUND_TINT[c] for c in classes]),
                  extent=(x0 - grid["step"] / 2, x0 + (grid["cols"] - 0.5) * grid["step"],
                          y0 - grid["step"] / 2, y0 + (grid["rows"] - 0.5) * grid["step"]))
        for b in data["buildings"]:
            ring = b["points"] + b["points"][:1]
            ax.fill([q[0] for q in ring], [q[1] for q in ring], color="#c4c2bb" if b["campus"] else "#d6d4ce", lw=0)
        for p in data["paths"]:
            if p["type"] in ("primary", "primary_link", "secondary", "tertiary", "residential", "service",
                             "living_street", "unclassified") and not p["tunnel"]:
                ax.plot([q[0] for q in p["points"]], [q[1] for q in p["points"]], color="#d9d8d3",
                        lw=max(1.0, p["width"] * 0.35), solid_capstyle="round", zorder=1)
        ax.plot(rx + rx[:1], ry + ry[:1], color="#ffffff", lw=5.5, zorder=2, solid_capstyle="round")
        ax.plot(rx + rx[:1], ry + ry[:1], color="#9a9890", lw=1.2, zorder=3)
        handles = []
        if title.startswith("Lamps") or "zoom" in title:
            rails = [p for p in placed if p["proto"] == "Prop_RailingSegment"]
            for r in rails:
                ax.plot([r["x"], r["x"] + 2 * math.cos(r["rot_z"])], [r["y"], r["y"] + 2 * math.sin(r["rot_z"])],
                        color=INK_2, lw=1.6, zorder=4, solid_capstyle="butt")
            handles.append(Line2D([], [], color=INK_2, lw=1.6, label=f"Railing ({len(rails)} x 2 m)"))
        for label, protos, slot in series:
            pts = [p for p in placed if p["proto"] in protos]
            ax.scatter([p["x"] for p in pts], [p["y"] for p in pts], s=size, color=SLOTS[slot],
                       edgecolors="#ffffff", linewidths=0.6, zorder=5)
            handles.append(Line2D([], [], marker="o", lw=0, markersize=7, markerfacecolor=SLOTS[slot],
                                  markeredgecolor="#ffffff", label=f"{label} ({len(pts)})"))
        vx0, vy0, vx1, vy1 = (x0, y0, x1, y1) if view == "world" else route_view if view == "route" else view
        ax.set_xlim(vx0, vx1)
        ax.set_ylim(vy0, vy1)
        ax.set_aspect("equal")
        ax.set_title(title, loc="left")
        ax.tick_params(labelsize=7, length=2)
        ax.legend(handles=handles, loc="lower right", fontsize=8, frameon=True, framealpha=0.92,
                  edgecolor="#d9d8d3", labelcolor=INK)
    fig.suptitle("Vegetation and props placements (metres from the world origin, north up; grey line = route)",
                 x=0.01, ha="left", fontsize=13, color=INK)
    fig.tight_layout(rect=(0, 0, 1, 0.97))
    fig.savefig(MAP_PNG, facecolor="#fcfcfb")
    print(f"map -> {MAP_PNG}")


# =====================================================================================================
# Blender part
# =====================================================================================================
def shaped_terrain():
    """Same terrain as the world build (shaped 5 m grid + high-res corridor), so z values match the world.
    Falls back to the raw elevation data if those modules are missing or fail."""
    try:
        import env_corridor
        import env_terrain
        env_terrain.apply()
        env_corridor.build_data()
        return "shaped terrain + corridor (env_terrain, env_corridor)"
    except Exception as exc:          # noqa: BLE001  keep the asset test independent of the terrain modules
        return f"raw terrain (terrain modules unavailable: {exc.__class__.__name__}: {exc})"


def mesh_checks(obj):
    """Triangles, loose verts, non-manifold edges, inverted faces (normal pointing at the part's centre),
    lowest point, and how far the base centre is from the origin."""
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    loose = sum(1 for v in bm.verts if not v.link_faces)
    open_edges = sum(1 for e in bm.edges if not e.is_manifold)
    inverted = 0
    islands, seen = [], set()
    for f in bm.faces:                     # connected parts
        if f.index in seen:
            continue
        stack, part = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            part.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        islands.append(part)
    for part in islands:                   # a closed part's normals must point away from its volume
        vol = sum(f.calc_center_median().dot(f.normal) * f.calc_area() for f in part)
        if vol < 0:
            inverted += len(part)
    zmin = min(v.co.z for v in bm.verts)
    reach = max(math.hypot(v.co.x, v.co.y) for v in bm.verts)
    bm.free()
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    return dict(tris=tris, loose=loose, open_edges=open_edges, inverted=inverted, zmin=zmin, reach=reach,
                height=max(v.co.z for v in obj.data.vertices), mats=len(obj.data.materials))


def rule_checks(veg, props, V, P):
    """Re-checks the placement rules on the written placements; returns a list of problems."""
    import env_common as E
    problems = []
    site_v = V.Site()
    for p in veg:
        kind = V._kind(p["proto"])
        crown = V.KIND[kind]["crown"] * p["scale"]
        d = E.distance_to_route(p["x"], p["y"])
        if d < E.KEEP_CLEAR + crown - 0.01:
            problems.append(f"{p['proto']} at ({p['x']:.1f}, {p['y']:.1f}) only {d:.1f} m from the route")
        if site_v.buildings.clearance(p["x"], p["y"]) < V.BUILDING_GAP - 0.01:
            problems.append(f"{p['proto']} at ({p['x']:.1f}, {p['y']:.1f}) inside / within 2 m of a building")
        if site_v.roads.clearance(p["x"], p["y"]) <= V.ROAD_GAP:
            problems.append(f"{p['proto']} at ({p['x']:.1f}, {p['y']:.1f}) on a road")
        if E.ground_class(p["x"], p["y"]) in ("sea", "water"):
            problems.append(f"{p['proto']} at ({p['x']:.1f}, {p['y']:.1f}) in the water")
    trees = sum(1 for p in veg if V._kind(p["proto"]) in V.TREE_KINDS)
    if trees > V.MAX_TREES or len(veg) - trees > V.MAX_LOW:
        problems.append(f"caps broken: {trees} trees, {len(veg) - trees} shrubs/grass")
    site_p = P.Site()
    deck_like = {"Prop_LoungeChair", "Prop_RailingSegment"}
    for p in props:
        if p["proto"] in deck_like:
            continue
        d = E.distance_to_route(p["x"], p["y"])
        if p["proto"] in P.HALL_PROTOS and E.nearest_route_point(p["x"], p["y"]).get("zone") == "atrium":
            if d < P.HALL_ROAD_BAND + P.FOOTPRINT[p["proto"]] * 0.5 - 0.01:      # atrium bay furniture
                problems.append(f"{p['proto']} at ({p['x']:.1f}, {p['y']:.1f}) only {d:.1f} m from the route (hall)")
            continue
        if d < E.KEEP_CLEAR - 0.01:
            problems.append(f"{p['proto']} at ({p['x']:.1f}, {p['y']:.1f}) only {d:.1f} m from the route")
        if site_p.building_clearance(p["x"], p["y"]) < P.BUILDING_GAP - 0.01:
            problems.append(f"{p['proto']} at ({p['x']:.1f}, {p['y']:.1f}) inside / within 2 m of a building")
        if site_p.road_clearance(p["x"], p["y"]) <= P.ROAD_GAP:
            problems.append(f"{p['proto']} at ({p['x']:.1f}, {p['y']:.1f}) on a road")
        if E.nearest_route_point(p["x"], p["y"]).get("zone") in P.NO_PROP_ZONES:
            problems.append(f"{p['proto']} at ({p['x']:.1f}, {p['y']:.1f}) in the tunnel / atrium")
    street = [p for p in props if p["proto"] != "Prop_RailingSegment"]
    for v in veg:                          # a plant growing through a bench, lamp or parasol
        reach = V.KIND[V._kind(v["proto"])]["space"] * v["scale"]
        for p in street:
            if math.hypot(v["x"] - p["x"], v["y"] - p["y"]) < reach + P.FOOTPRINT[p["proto"]]:
                problems.append(f"{v['proto']} at ({v['x']:.1f}, {v['y']:.1f}) overlaps {p['proto']}")
    for p in props:
        if p["proto"] == "Prop_LoungeChair":
            d = E.distance_to_route(p["x"], p["y"])
            if not (P.CHAIR_BAND[0] - 0.01 <= d <= P.CHAIR_BAND[1] + 0.01):
                problems.append(f"lounge chair at ({p['x']:.1f}, {p['y']:.1f}) is {d:.1f} m from the route")
    return problems


LINEUP = (  # rows from back to front: (y, gap, [name or (name, centre x)]); without x a row is spaced evenly.
    # The tall back row stands behind the gaps of the middle row; the small props are nearest the camera.
    (30.0, 0.0, [("Veg_UmbrellaPine", -21.0), ("Veg_Palm", -9.0), ("Prop_LampRoad", 0.6), ("Veg_Palm_B", 10.0),
                 ("Veg_UmbrellaPine_B", 21.5)]),
    (13.0, 0.0, [("Veg_Olive", -10.4), ("Prop_LampCampus", -3.1), ("Prop_Parasol", 3.6), ("Veg_Olive_B", 10.8)]),
    (0.0, 0.9, ["Veg_Shrub", "Veg_Grass", "Prop_Bench", "Prop_LoungeChair", "Prop_Bin", "Prop_Bollard",
                "Prop_RailingSegment", "Prop_RailingSegment", "Veg_Shrub_B"]),
    (-6.0, 0.9, ["Prop_TroughBloom", "Prop_BistroTable", "Prop_BistroChair", "Prop_PlanterRound", "Prop_StudyTable",
                 "Prop_GrandPiano"]),
)


def render_lineup(protos):
    """Linked copies of every prototype in three rows (tall at the back, small in front), one camera."""
    import env_common as E
    from bpy_extras.object_utils import world_to_camera_view
    from mathutils import Vector

    scene = bpy.context.scene
    col = E.collection("Lineup")
    for c in ("Proto_Vegetation", "Proto_Props"):
        if bpy.data.collections.get(c):
            bpy.data.collections[c].hide_render = True
    turn = {"Prop_LoungeChair": math.radians(25), "Prop_Bench": math.radians(-8)}
    objs = []

    def add(name, x_left, y):
        src = protos[name]
        obj = bpy.data.objects.new("L_" + name, src.data)
        obj.location = (x_left - min(v.co.x for v in src.data.vertices), y, 0)
        obj.rotation_euler.z = turn.get(name, 0.0)
        col.objects.link(obj)
        objs.append(obj)

    for y, gap, items in LINEUP:
        if all(isinstance(item, tuple) for item in items):          # explicit centre x
            for name, cx in items:
                xs = [v.co.x for v in protos[name].data.vertices]
                add(name, cx - (max(xs) - min(xs)) / 2, y)
            continue
        steps = []                                    # evenly spaced: how far x moves after each object
        for k, name in enumerate(items):
            xs = [v.co.x for v in protos[name].data.vertices]
            if name == "Prop_RailingSegment":         # segments go end to end: two make a 4 m railing
                steps.append(2.0 if k + 1 < len(items) and items[k + 1] == name else 2.0 + gap)
            else:
                steps.append(max(xs) - min(xs) + gap)
        x = -(sum(steps) - gap) / 2
        for name, step in zip(items, steps):
            add(name, x, y)
            x += step
    # ground and sky
    ground = bpy.data.meshes.new("LineupGround")
    r = 600.0
    ground.from_pydata([(math.cos(a) * r, math.sin(a) * r, 0.0) for a in (2 * math.pi * k / 48 for k in range(48))],
                       [], [tuple(range(48))])
    ground.materials.append(E.material("lineup_ground", "#D9D3C5", roughness=1.0))
    g = bpy.data.objects.new("LineupGround", ground)
    col.objects.link(g)
    world = bpy.data.worlds.new("LineupSky")       # bright sky for the camera, softer sky light on the models
    scene.world = world
    world.use_nodes = True
    nodes, links = world.node_tree.nodes, world.node_tree.links
    fill = nodes["Background"]
    fill.inputs["Color"].default_value = E.hex_rgba("#BCD8F2")
    fill.inputs["Strength"].default_value = 0.6
    seen = nodes.new("ShaderNodeBackground")
    seen.inputs["Color"].default_value = E.hex_rgba("#A9CFF2")
    seen.inputs["Strength"].default_value = 1.0
    path = nodes.new("ShaderNodeLightPath")
    mix = nodes.new("ShaderNodeMixShader")
    links.new(path.outputs["Is Camera Ray"], mix.inputs["Fac"])
    links.new(fill.outputs["Background"], mix.inputs[1])
    links.new(seen.outputs["Background"], mix.inputs[2])
    links.new(mix.outputs["Shader"], nodes["World Output"].inputs["Surface"])
    sun = bpy.data.objects.new("LineupSun", bpy.data.lights.new("LineupSun", "SUN"))
    col.objects.link(sun)
    sun.data.energy = 2.7
    sun.data.angle = math.radians(2.0)
    sun.data.color = (1.0, 0.97, 0.92)
    sun.rotation_euler = (math.radians(42), 0.0, math.radians(-38))     # from the front left, late morning
    for m in bpy.data.materials:
        m.use_backface_culling = True      # like Unity: a flipped face would show as a hole here
    # camera: fixed view direction, pulled back until every object fits with a margin
    cam = bpy.data.objects.new("LineupCam", bpy.data.cameras.new("LineupCam"))
    col.objects.link(cam)
    scene.camera = cam
    cam.data.lens = 28
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    bpy.context.view_layer.update()                    # matrix_world is only valid after an update
    corners = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    corners = [Vector((c.x, c.y, max(c.z, 0.0))) for c in corners]
    aim = Vector((0.0, 6.0, 4.0))                      # above the props, so they sit in the lower third
    direction = Vector((0.0, math.cos(math.radians(4)), -math.sin(math.radians(4))))   # 4 degrees down
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    lo, hi = 5.0, 200.0
    for _ in range(40):
        mid = (lo + hi) / 2
        cam.location = aim - direction * mid
        bpy.context.view_layer.update()
        fit = all(0.03 <= c.x <= 0.97 and 0.04 <= c.y <= 0.96 and c.z > 0
                  for c in (world_to_camera_view(scene, cam, k) for k in corners))
        lo, hi = (lo, mid) if fit else (mid, hi)
    cam.location = aim - direction * hi
    # render
    scene.render.filepath = LINEUP_PNG
    scene.view_settings.view_transform = "Standard"
    engine = "BLENDER_EEVEE"
    try:
        scene.render.engine = engine
        ev = scene.eevee
        for attr, value in (("taa_render_samples", 64), ("use_shadows", True), ("use_gtao", True),
                            ("gtao_distance", 1.5), ("shadow_ray_count", 2), ("shadow_step_count", 8)):
            if hasattr(ev, attr):
                try:
                    setattr(ev, attr, value)
                except (TypeError, AttributeError):
                    pass
        bpy.ops.render.render(write_still=True)
    except Exception as exc:              # noqa: BLE001  headless machines without a GPU context
        print(f"  EEVEE failed ({exc}); falling back to Workbench")
        engine = "BLENDER_WORKBENCH"
        scene.render.engine = engine
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "MATERIAL"
        scene.display.shading.show_shadows = True
        bpy.ops.render.render(write_still=True)
    for m in bpy.data.materials:
        m.use_backface_culling = False
    return engine


def blender_main():
    sys.path.insert(0, HERE)
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import env_props as P
    import env_vegetation as V

    terrain = shaped_terrain()
    protos = {}
    protos.update(V.build_prototypes())
    protos.update(P.build_prototypes())
    bpy.context.preferences.filepaths.save_version = 0      # no .blend1 backup next to it
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "assets_prototypes.blend"))

    print("\nPROTOTYPE CHECKS")
    print(f"  {'prototype':22s} {'tris':>5s} {'budget':>6s} {'mats':>4s} {'height':>7s} {'reach':>6s} "
          f"{'z min':>6s} {'loose':>5s} {'open':>5s} {'flip':>5s}")
    checks, bad = {}, []
    for name, obj in protos.items():
        c = mesh_checks(obj)
        checks[name] = c
        budget = (V.TRI_BUDGET.get(name.removesuffix("_B")) or P.TRI_BUDGET[name])
        print(f"  {name:22s} {c['tris']:5d} {budget:6d} {c['mats']:4d} {c['height']:7.2f} {c['reach']:6.2f} "
              f"{c['zmin']:6.2f} {c['loose']:5d} {c['open_edges']:5d} {c['inverted']:5d}")
        if c["tris"] > budget or c["loose"] or c["inverted"]:
            bad.append(name)
        kind = V._kind(name)
        if kind and c["reach"] > V.KIND[kind]["crown"] + 0.05:
            bad.append(f"{name}: reach {c['reach']:.2f} m > crown radius {V.KIND[kind]['crown']} used by the rules")
    print("  'open' = boundary edges of parts that are buried or hidden inside another part (trunk bases, pole ends)")

    engine = render_lineup(protos)
    print(f"\nlineup ({engine}) -> {LINEUP_PNG}")

    print(f"\nPLACEMENT on {terrain}")
    veg = V.place()
    props = P.place(avoid=veg)
    print(f"  vegetation -> {V.PLACEMENTS_FILE}\n  props      -> {P.PLACEMENTS_FILE}")
    print(f"  OSM trees: {V.REPORT['osm_trees']} found, {V.REPORT['osm_moved']} moved to fit the rules, "
          f"{len(V.REPORT['osm_dropped'])} dropped {V.REPORT['osm_dropped']}")
    for key, value in P.REPORT.items():
        print(f"  {key}: {value}")
    problems = rule_checks(veg, props, V, P)
    print(f"  rule check: {len(problems)} problems")
    for line in problems[:20]:
        print("    " + line)

    print("\nTABLE")
    print(f"  {'prototype':22s} {'triangles':>9s} {'instances':>9s} {'scene tris':>10s}")
    total_i = total_t = 0
    for name, obj in protos.items():
        count = sum(1 for p in veg + props if p["proto"] == name)
        tris = checks[name]["tris"]
        total_i += count
        total_t += count * tris
        print(f"  {name:22s} {tris:9d} {count:9d} {count * tris:10d}")
    trees = sum(1 for p in veg if V._kind(p["proto"]) in V.TREE_KINDS)
    print(f"  {'TOTAL':22s} {'':9s} {total_i:9d} {total_t:10d}")
    print(f"  trees {trees} (cap {V.MAX_TREES}), shrubs + grass {len(veg) - trees} (cap {V.MAX_LOW}), props {len(props)}")

    if os.path.exists(MEDIA_PY):
        subprocess.run([MEDIA_PY, os.path.abspath(__file__), "--map"], check=False)
    else:
        print(f"  no matplotlib Python at {MEDIA_PY}: map skipped")
    if bad:
        print("\nFAILED: " + "; ".join(bad))
    print("done")


if bpy is None:
    if "--map" in sys.argv:
        draw_map()
    else:
        print(__doc__)
else:
    blender_main()
