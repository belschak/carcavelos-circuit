"""Builds the grey-box Carcavelos campus world in Blender from world_data.json.

Run:  /Applications/Blender.app/Contents/MacOS/Blender -b -P build_world.py
Output (outside the repo): out/CampusWorld.blend, out/CampusWorld.fbx, out/preview_*.png

Axes: Blender x = east, y = north, z = up, 1 unit = 1 m, origin = WORLD_ORIGIN in world_data.py.
Every building is its own object named B_<osm id>_<name>, so a landmark model can later replace it in place.
"""
import json
import math
import os
import unicodedata

import sys

import bmesh
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
sys.path.insert(0, HERE)
import env_common as E          # noqa: E402  shared helpers (terrain, materials, route)
import env_terrain              # noqa: E402
import env_track                # noqa: E402
import env_trackside            # noqa: E402
import env_tunnel               # noqa: E402
import env_buildings            # noqa: E402
import env_backdrop             # noqa: E402
import env_materials            # noqa: E402
import env_interior             # noqa: E402
import env_corridor             # noqa: E402
import env_covebank             # noqa: E402  V10d: the cove bank outside the corridor
import env_streets              # noqa: E402  V10f: the street section rebuilt from the Street View survey
import env_street_objects       # noqa: E402
import env_capricciosa          # noqa: E402  V16g: Capricciosa forecourt, sea wall, car-park corner
import env_campus               # noqa: E402  V10c2: campus buildings, courtyards, pools
import env_plaza                # noqa: E402  V10i: upper plaza, stairs, sunken courtyard
import dressing_freeze         # noqa: E402  V10i: dressing stays put outside changed route sections
import env_vegetation           # noqa: E402
import env_props                # noqa: E402
import env_entrance             # noqa: E402  V15a: entrance plaza (calcada, drain channels, car-park ramp, young trees)

DATA = E.DATA

COLOURS = {  # grey-box colours, close to docs/art-style.md; final look comes later
    "ground": "#B9B4A8", "grass": "#8FB37A", "wood": "#6E9B5E", "paved": "#8A8D92", "pitch": "#7FAE6A",
    "sand": "#E3C48D", "water": "#2D8CD6", "sea": "#2D8CD6",
    "road": "#55585E", "footway": "#A7A39A", "steps": "#7A766E", "tunnel": "#FF8A1F",
    "building_campus": "#EAEAEC", "building_other": "#C9C5BC", "crown": "#5E8F4E", "trunk": "#6B4F3A",
    "route": "#FF8A1F", "route_tunnel": "#B85A0A", "marker_jump": "#2ECC40", "marker_start": "#FFFFFF",
}
ROAD_TYPES = {"primary", "primary_link", "secondary", "tertiary", "residential", "unclassified", "service", "living_street"}


# ---------- helpers ----------
def hex_rgba(h):
    def lin(c):  # sRGB hex -> linear, which Blender colours expect
        c = c / 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(int(h[1:3], 16)), lin(int(h[3:5], 16)), lin(int(h[5:7], 16)), 1.0)


def material(name):
    mat = bpy.data.materials.get("W_" + name) or bpy.data.materials.new("W_" + name)
    mat.diffuse_color = hex_rgba(COLOURS[name])
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = hex_rgba(COLOURS[name])
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def collection(name):
    col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    return col


def ascii_name(text):
    return "".join(c for c in unicodedata.normalize("NFKD", text) if c.isalnum() and ord(c) < 128)[:40]


GRID = DATA["grid"]
X0, Y0, X1, Y1 = DATA["bounds"]
STEP, COLS, ROWS, H = GRID["step"], GRID["cols"], GRID["rows"], GRID["heights"]


def ground_z(x, y):
    """Ground height at (x, y): the shaped terrain from env_terrain (falls back to the raw grid before that)."""
    return E.ground_z(x, y)


def new_object(name, mesh, col):
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    return obj


# ---------- build ----------
SINK_UNDER_CORRIDOR = 0.35   # m


def build_ground(heights):
    classes = ["ground", "grass", "wood", "paved", "pitch", "sand", "water", "sea"]
    mesh = bpy.data.meshes.new("Ground")
    verts = []
    for j in range(ROWS):
        for i in range(COLS):
            x, y = X0 + i * STEP, Y0 + j * STEP
            # under the high-res corridor the 5 m ground sinks a little, so the two never lie 3 cm apart
            # (z-fighting); the corridor's edge skirt hides the step
            sink = SINK_UNDER_CORRIDOR * env_corridor.smoothstep((env_corridor.inside_depth(x, y) - 0.5) / 2.0)
            verts.append((x, y, heights[j][i] - sink))
    faces, mats = [], []
    for j in range(ROWS - 1):
        for i in range(COLS - 1):
            a = j * COLS + i
            cx, cy = X0 + (i + 0.5) * STEP, Y0 + (j + 0.5) * STEP
            if env_terrain.in_portal_hole(cx, cy) or env_corridor.covers_cell(cx, cy) or env_covebank.covers_cell(cx, cy) \
                    or env_streets.covers_cell(cx, cy) or env_campus.covers_cell(cx, cy):
                continue                       # the tunnel portal / the cut walls close this gap; V10d: the cove bank;
                                               # V10f: the restaurant terrace
            if env_entrance.covers_cell(cx, cy):   # V15a: the car-park ramp's apron replaces this cell
                continue
            faces.append((a, a + 1, a + 1 + COLS, a + COLS))
            corner = [GRID["classes"][j][i], GRID["classes"][j][i + 1], GRID["classes"][j + 1][i], GRID["classes"][j + 1][i + 1]]
            mats.append(classes.index(max(set(corner), key=corner.count)))   # majority class of the 4 corners
    mesh.from_pydata(verts, [], faces)
    if os.path.exists(os.path.join(E.TEXTURES, "ground_colour.png")):
        # one material with the painted ground colour map (make_groundmap.py), UVs = position in the world
        mesh.materials.append(E.material("ground_map", "#A6AA82", roughness=0.95))
        uv = mesh.uv_layers.new(name="UVMap")
        for loop in mesh.loops:
            co = mesh.vertices[loop.vertex_index].co
            uv.data[loop.index].uv = ((co.x - X0) / (X1 - X0), (co.y - Y0) / (Y1 - Y0))
    else:
        for name in classes:
            mesh.materials.append(material(name))
        for poly, m in zip(mesh.polygons, mats):
            poly.material_index = m
    new_object("World_Ground", mesh, collection("World_Ground"))


def ribbon(points, width, lift, heights=None):
    """Strip of the given width along a polyline, `lift` metres above the ground.
    With `heights` (driving height per point) it stays flat wherever the route is above the ground
    (atrium floor, terrace deck) instead of following the terrain."""
    heights = heights or [None] * len(points)
    dense = []
    for (xa, ya), (xb, yb), ha, hb in zip(points, points[1:], heights, heights[1:]):
        steps = max(1, int(math.hypot(xb - xa, yb - ya) / 2.0))
        dense += [(xa + (xb - xa) * k / steps, ya + (yb - ya) * k / steps,
                   None if ha is None else ha + (hb - ha) * k / steps) for k in range(steps)]
    dense.append((points[-1][0], points[-1][1], heights[-1]))
    left, right = [], []
    for k, (x, y, h) in enumerate(dense):
        xa, ya, _ = dense[max(k - 1, 0)]
        xb, yb, _ = dense[min(k + 1, len(dense) - 1)]
        length = math.hypot(xb - xa, yb - ya) or 1.0
        nx, ny = -(yb - ya) / length * width / 2, (xb - xa) / length * width / 2
        raised = h is not None and h - ground_z(x, y) > 0.5
        left.append((x + nx, y + ny, (h if raised else ground_z(x + nx, y + ny)) + lift))
        right.append((x - nx, y - ny, (h if raised else ground_z(x - nx, y - ny)) + lift))
    verts = left + right
    n = len(dense)
    faces = [(k, k + 1, n + k + 1, n + k) for k in range(n - 1)]
    return verts, faces


def clip_to_bounds(points):
    """Splits a polyline into the runs that lie inside the world, cut exactly at the edge."""
    def inside(p):
        return X0 <= p[0] <= X1 and Y0 <= p[1] <= Y1

    def edge_point(a, b):
        lo, hi = 0.0, 1.0                     # bisection: a is inside, b is outside
        for _ in range(30):
            mid = (lo + hi) / 2
            p = (a[0] + (b[0] - a[0]) * mid, a[1] + (b[1] - a[1]) * mid)
            lo, hi = (mid, hi) if inside(p) else (lo, mid)
        return (a[0] + (b[0] - a[0]) * lo, a[1] + (b[1] - a[1]) * lo)

    runs, run = [], []
    for k, p in enumerate(points):
        if inside(p):
            if not run and k > 0:
                run.append(edge_point(p, points[k - 1]))
            run.append(p)
        elif run:
            run.append(edge_point(run[-1], p))
            runs.append(run)
            run = []
    if run:
        runs.append(run)
    return [r for r in runs if len(r) >= 2]


def clearance_at(x, y, clear):
    """Extra clearance next to the tunnel approaches: the cut walls and portals own that ground."""
    p = E.nearest_route_point(x, y)
    if p["zone"] in ("ramp", "tunnel plaza", "tunnel") and math.hypot(p["x"] - x, p["y"] - y) < 30:
        return clear + 8.0
    return clear


def away_from_route(runs, clear=E.ROUTE_WIDTH / 2 + 2.0):
    """Splits path polylines so no piece lies on the track (the track surface replaces it there)."""
    out = []
    for run in runs:
        dense = []
        for (xa, ya), (xb, yb) in zip(run, run[1:]):
            steps = max(1, int(math.hypot(xb - xa, yb - ya) / 2.0))
            dense += [(xa + (xb - xa) * k / steps, ya + (yb - ya) * k / steps) for k in range(steps)]
        dense.append(run[-1])
        piece = []
        for q in dense:
            if E.distance_to_route(*q) < clearance_at(q[0], q[1], clear):
                if len(piece) >= 2:
                    out.append(piece)
                piece = []
            else:
                piece.append(q)
        if len(piece) >= 2:
            out.append(piece)
    return out


PATH_STRIPS = False     # True = OSM roads/footways also as raised strips (5-9 cm over the ground: z-fighting at
                        # distance over ~45,000 m2); they are already painted into textures/ground_colour.png


def build_paths():
    groups = {"road": ([], []), "footway": ([], []), "steps": ([], []), "tunnel": ([], [])}
    # a strip must stay clear of the track by its own half-width: wide OSM roads beside the route (the Avenida
    # Marginal at s 714-824) used to reach 3.5 m onto the road, 6-10 cm above it
    for p in [dict(q, points=run) for q in DATA["paths"]
              for run in away_from_route(clip_to_bounds(q["points"]), clear=E.ROUTE_WIDTH / 2 + q["width"] / 2 + 0.75)]:
        if not p["tunnel"] and not PATH_STRIPS:
            continue                           # roads and footways are painted into the ground colour map
        if p["tunnel"]:
            key, lift = "tunnel", -5.0          # guide line below ground, the track owner shapes the real tunnel
        elif p["type"] in ROAD_TYPES:
            key, lift = "road", 0.05
        elif p["type"] == "steps":
            key, lift = "steps", 0.12
        else:
            key, lift = "footway", 0.09
        verts, faces = ribbon(p["points"], p["width"], lift)
        base = len(groups[key][0])
        groups[key][0].extend(verts)
        groups[key][1].extend(tuple(v + base for v in f) for f in faces)
    for key, (verts, faces) in groups.items():
        if not faces:
            continue
        mesh = bpy.data.meshes.new("Paths_" + key)
        mesh.from_pydata(verts, [], faces)
        mesh.materials.append(material(key))
        new_object("World_Paths_" + key, mesh, collection("World_TunnelGuides" if key == "tunnel" else "World_Paths"))


def build_buildings():
    for b in DATA["buildings"]:
        pts = b["points"]
        grounds = [ground_z(x, y) for x, y in pts]
        base = min(grounds) - 1.0
        top = b["roof_z"] if b.get("roof_z") is not None else max(grounds) + b["height"]   # roof_z: e.g. a terrace deck
        bm = bmesh.new()
        bottom = [bm.verts.new((x, y, base)) for x, y in pts]
        face = bm.faces.new(bottom)
        ext = bmesh.ops.extrude_face_region(bm, geom=[face])
        moved = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]
        for v in moved:
            v.co.z = top
        bmesh.ops.reverse_faces(bm, faces=[face])          # bottom face points down
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        mesh = bpy.data.meshes.new("B_" + b["id"])
        bm.to_mesh(mesh)
        bm.free()
        kind = "building_campus" if b["campus"] else "building_other"
        mesh.materials.append(material(kind))
        name = "B_" + b["id"] + ("_" + ascii_name(b["name"]) if b["name"] else "")
        obj = new_object(name, mesh, collection("World_Buildings_Campus" if b["campus"] else "World_Buildings_Other"))
        obj["osm_id"] = b["id"]
        obj["height_m"] = b["height"]


DRESSING_INSTANCES = ("World_Vegetation", "World_Props")       # linked duplicates, only for renders
DRESSING_PROTOTYPES = ("Proto_Vegetation", "Proto_Props")       # one mesh per kind, at the origin


def build_dressing():
    """Trees, shrubs, grass and props (env_vegetation.py, env_props.py). Each kind is built once as a prototype;
    the placements are data (out/placements_vegetation.json, out/placements_props.json) so Unity can instance them.
    In the .blend they appear as linked duplicates for the renders. Replaces the old cone trees."""
    protos = {**env_vegetation.build_prototypes(), **env_props.build_prototypes(), **env_street_objects.build_prototypes()}
    veg = env_vegetation.place()
    props = env_props.place(avoid=veg)
    veg, props = env_street_objects.dress(veg, props)      # V10f: the street section's own trees, cars and furniture
    veg, props = dressing_freeze.apply(veg, props)         # V10i: outside the changed route sections: as approved
    veg, props = env_campus.dress(veg, props)              # V10c2: nothing in the pools, on the berms or the lower plaza
    veg, props = env_plaza.dress(veg, props)               # V10i: the upper plaza's own trees and furniture
    veg, props = env_entrance.dress(veg, props, protos)    # V15a: entrance plaza: young staked trees, seat blocks, lamps
    veg, props = env_street_objects.dress_promenade(veg, props)   # V15c: promenade bollards, flood masts, machines
    veg, props = env_capricciosa.dress(veg, props, protos)        # V16g: builds its statics, then its own dressing
    env_vegetation.instance_for_preview(veg, protos)
    env_props.instance_for_preview(props, protos)
    print(f"  dressing: {len(protos)} prototypes, {len(veg)} plants, {len(props)} props")


def add_origin_marker():
    empty = bpy.data.objects.new("WorldOrigin", None)
    empty["lat"], empty["lon"] = DATA["origin_latlon"]
    empty["credit"] = DATA["credit"]
    empty.empty_display_size = 5
    collection("World_Ground").objects.link(empty)


def render_previews():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1100
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    shots = {
        "top": dict(loc=((X0 + X1) / 2, (Y0 + Y1) / 2, 600), rot=(0, 0, 0), ortho=max(X1 - X0, (Y1 - Y0) * 1.6)),
        "south_view": dict(loc=(-60, -330, 90), rot=(math.radians(68), 0, math.radians(8)), lens=26),
        "campus_close": dict(loc=(150, -60, 60), rot=(math.radians(62), 0, math.radians(55)), lens=30),
        "route_overview": dict(loc=(-270, -240, 170), rot=(math.radians(58), 0, math.radians(-42)), lens=24),
    }
    shots.update(route_shots())
    for name, s in shots.items():
        cam.location, cam.rotation_euler = s["loc"], s["rot"]
        cam_data.type = "ORTHO" if "ortho" in s else "PERSP"
        if "ortho" in s:
            cam_data.ortho_scale = s["ortho"]
        else:
            cam_data.lens = s["lens"]
        cam_data.clip_end = 3000
        scene.render.filepath = os.path.join(OUT, f"preview_{name}.png")
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)


def stats():
    total = 0
    for col in bpy.data.collections:
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in col.objects if o.type == "MESH")
        total += tris
        print(f"  {col.name:28s} objects {len(col.objects):4d}  triangles {tris:7d}")
    print(f"  TOTAL triangles {total}")


ROUTE_FILE = os.path.join(HERE, "route.json")
ROUTE_WIDTH = 8.0      # metres, a proposal for the drivable road; Role 2 decides the real width


def oriented_box(centre, forward, width, length, height, ramp=False):
    """Box (or ramp rising in the driving direction) placed on the ground at `centre`, facing `forward`."""
    fx, fy = forward
    sx, sy = -fy, fx                                  # sideways
    cx, cy, cz = centre
    corners = []
    for along, top in ((-length / 2, 0.0 if ramp else height), (length / 2, height)):
        for side in (-width / 2, width / 2):
            x, y = cx + fx * along + sx * side, cy + fy * along + sy * side
            corners.append([(x, y, cz), (x, y, cz + top)])
    (a0, a1), (b0, b1), (c0, c1), (d0, d1) = corners  # a,b = back left/right, c,d = front left/right
    verts = [a0, b0, d0, c0, a1, b1, d1, c1]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return verts, faces


def build_route_guide():
    """Orange strip along the route from Luc's sketch (route.json), plus jump and start markers."""
    if not os.path.exists(ROUTE_FILE):
        return
    route = json.load(open(ROUTE_FILE))
    pts = route["points"]
    col = collection("Track_RouteGuide")
    runs, current, previous = [], None, None          # pieces: open air / tunnel, and a gap at the jump drop
    for p in pts + [pts[0]]:
        key = "route_tunnel" if p["tunnel"] else "route"
        drop = previous is not None and abs(p["track_z"] - previous["track_z"]) > 1.5
        if current is None or current[0] != key or drop:
            if current:
                if not drop:
                    current[1].append(p)               # close the gap to the next piece
                runs.append(current)
            current = [key, []]
        current[1].append(p)
        previous = p
    runs.append(current)
    for key in ("route", "route_tunnel"):
        verts, faces = [], []
        for run_key, run in runs:
            if run_key != key or len(run) < 2:
                continue
            v, f = ribbon([(p["x"], p["y"]) for p in run], ROUTE_WIDTH, 0.25, [p["track_z"] for p in run])
            faces += [tuple(i + len(verts) for i in face) for face in f]
            verts += v
        mesh = bpy.data.meshes.new("RouteGuide_" + key)
        mesh.from_pydata(verts, [], faces)
        mesh.materials.append(material(key))
        new_object("RouteGuide_" + ("Tunnel" if key == "route_tunnel" else "Open"), mesh, col)
    # jump: small kicker on the deck, ending at the deck edge; start: flat bar across the road
    for name, index, kind, ramp, size in (("Marker_Jump", route["jump"]["index"] - 1, "marker_jump", True, (ROUTE_WIDTH, 4.0, 0.6)),
                                          ("Marker_Start_s0", 0, "marker_start", False, (ROUTE_WIDTH, 1.0, 0.3))):
        a, b = pts[index % len(pts)], pts[(index + 2) % len(pts)]
        d = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
        verts, faces = oriented_box((a["x"], a["y"], a["track_z"] + 0.25),
                                    ((b["x"] - a["x"]) / d, (b["y"] - a["y"]) / d), size[0], size[1], size[2], ramp)
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces)
        mesh.materials.append(material(kind))
        new_object(name, mesh, col)


PASSAGE_RUNS = []                   # route pieces inside drive-through buildings (for env_interior)
PASSAGE_WIDTHS = []                 # per piece, per point (left, right) half-widths (env_interior.hall_widths)
PASSAGE_WIDTH = ROUTE_WIDTH + 2.0   # metres, a bit wider than the road so the walls don't touch it
PASSAGE_HEIGHT = 7.0                # metres of clear height above the road (the atrium roof stays on top)
PASSAGE_EXTEND = env_interior.PASSAGE_EXTEND   # route points (2 m each) the cut reaches past each wall


def inside_polygon(x, y, poly):
    """Ray-casting point-in-polygon test (Blender's Python has no matplotlib)."""
    hit = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            hit = not hit
    return hit


def passage_cutter(run, widths):
    """Closed box swept along route points: the space the kart drives through inside a building.
    widths: per point (left, right) half-widths (env_interior.hall_widths: wide bays in the atrium)."""
    bm = bmesh.new()
    rings = []
    for k, p in enumerate(run):
        a, b = run[max(k - 1, 0)], run[min(k + 1, len(run) - 1)]
        d = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
        nx, ny = -(b["y"] - a["y"]) / d, (b["x"] - a["x"]) / d
        left, right = widths[k]
        ground, drive = ground_z(p["x"], p["y"]), p.get("track_z", ground_z(p["x"], p["y"]))
        bottom = drive              # the building keeps a floor at driving height (no gap beside the road)
        top = max(drive, ground) + PASSAGE_HEIGHT
        lx, ly, rx, ry = p["x"] + nx * left, p["y"] + ny * left, p["x"] - nx * right, p["y"] - ny * right
        rings.append([bm.verts.new((lx, ly, bottom)), bm.verts.new((rx, ry, bottom)),
                      bm.verts.new((rx, ry, top)), bm.verts.new((lx, ly, top))])
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(4):
            bm.faces.new((r0[i], r0[(i + 1) % 4], r1[(i + 1) % 4], r1[i]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new("PassageCutter")
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def carve_drive_throughs():
    """Cuts a covered passage along the route into every building marked drive_through in buildings.csv
    (env_interior.passages(): in the atrium the passage opens into wide bays on both sides)."""
    if not os.path.exists(ROUTE_FILE):
        return
    objects = {o.get("osm_id"): o for o in bpy.data.objects if o.get("osm_id")}
    tmp = collection("Tmp_Cutters")
    for b in DATA["buildings"]:
        if b.get("drive_through") and b["id"] in objects and not any(
                E.inside_polygon(p["x"], p["y"], b["points"]) for p in E.route_points()):
            print(f"  {b['name'] or b['id']}: marked drive_through, but the route does not pass through it")
    for piece in env_interior.passages():
        b = piece["building"]
        if b["id"] not in objects:
            continue
        obj = objects[b["id"]]
        cutter_mesh = passage_cutter(piece["points"], piece["widths"])
        cutter_mesh.materials.append(E.material("B_interior_wall", "#F1EFEA", 0.8))
        cutter = new_object("Cutter_" + b["id"], cutter_mesh, tmp)
        mod = obj.modifiers.new("passage", "BOOLEAN")
        mod.operation, mod.solver, mod.object = "DIFFERENCE", "EXACT", cutter
        if hasattr(mod, "material_mode"):
            mod.material_mode = "TRANSFER"       # the cut faces get the interior wall material
        PASSAGE_RUNS.append(piece["points"])
        PASSAGE_WIDTHS.append(piece["widths"])
        carved = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        obj.modifiers.remove(mod)
        old = obj.data
        obj.data = carved
        bpy.data.meshes.remove(old)
        bpy.data.objects.remove(cutter)
        widest = max(max(w) for w in piece["widths"])
        print(f"  passage through {b['name'] or b['id']}: {len(piece['run']) * 2} m inside, "
              f"up to {widest:.1f} m from the centreline")
        obj["drive_through"] = True
    bpy.data.collections.remove(tmp)


def route_shots():
    """Camera positions at kart-eye height along the route, for preview renders."""
    if not os.path.exists(ROUTE_FILE):
        return {}
    route = json.load(open(ROUTE_FILE))
    pts, n = route["points"], len(route["points"])

    def shot(i_from, i_to, up=3.0, lens=22):
        a, b = pts[i_from % n], pts[i_to % n]
        za, zb = a["track_z"] + up, b["track_z"] + 1.5
        dx, dy = b["x"] - a["x"], b["y"] - a["y"]
        pitch = math.atan2(zb - za, math.hypot(dx, dy))
        return dict(loc=(a["x"], a["y"], za), rot=(math.radians(90) + pitch, 0, math.atan2(-dx, dy)), lens=lens)

    atrium = next((i for i, p in enumerate(pts) if p["building"] == "Edifício Principal"), None)
    j = route["jump"]["index"]
    shots = {"kart_jump": shot(j - 15, j + 12), "kart_tunnel_approach": shot(n - 12, 10),
             "jump_side": dict(loc=(pts[j]["x"] - 10, pts[j]["y"] - 45, pts[j]["track_z"] + 6),
                               rot=(math.radians(84), 0, math.radians(-12)), lens=24)}
    if atrium is not None:
        shots["kart_atrium_entry"] = shot(atrium - 12, atrium + 8)
    return shots


def export_for_unity():
    """Exports CampusWorld.fbx and RouteGuide.fbx so that in Unity +X = east, +Z = north, +Y = up.

    Unity maps Blender -Y to Unity +Z (the usual 'model faces -Y in Blender' rule) and ignores
    the exporter's axis choice, so north (+Y here) would end up at Unity -Z. Turning all geometry
    180 degrees around the vertical axis just for the export fixes that. Checked in Unity 6000.3.28f1.
    The .blend stays unturned, so it still looks like the map (north up in top view).
    """
    for name in DRESSING_INSTANCES:            # placements go to Unity as JSON, not as geometry
        col = bpy.data.collections.get(name)
        for obj in list(col.all_objects) if col else []:
            bpy.data.objects.remove(obj)
    turn = Matrix.Rotation(math.pi, 4, "Z")
    for mesh in {obj.data for obj in bpy.data.objects if obj.type == "MESH"}:
        mesh.transform(turn)                   # once per mesh: prototypes may be shared by several objects
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            obj.location = turn @ obj.location
    def members(name):
        col = bpy.data.collections.get(name)
        return set(col.all_objects) if col else set()

    guide, track = members("Track_RouteGuide"), members("Track_Surface")
    protos = set().union(*(members(n) for n in DRESSING_PROTOTYPES))
    settings = dict(object_types={"MESH", "EMPTY"}, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                    bake_space_transform=True, mesh_smooth_type="FACE", use_mesh_modifiers=True, use_selection=True,
                    use_triangles=True)
    for filename, wanted in (("CampusWorld.fbx", lambda o: o not in guide and o not in track and o not in protos),
                             ("TrackSurface.fbx", lambda o: o in track),
                             ("RouteGuide.fbx", lambda o: o in guide),
                             ("Dressing_Prototypes.fbx", lambda o: o in protos)):
        for obj in bpy.data.objects:
            obj.select_set(wanted(obj))
        if any(o.select_get() for o in bpy.data.objects):
            bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, filename), **settings)


def viewer_defaults(hide_guide):
    """Opening the .blend: textures on, clip range 0.1-5000 m (the default 0.01-1000 m makes surfaces a few cm
    apart flicker at a distance and cuts off the horizon), route guide hidden until switched on in the outliner."""
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.clip_start, space.clip_end = 0.1, 5000.0
                    space.shading.type = "MATERIAL"
    layer = bpy.context.view_layer.layer_collection.children.get("Track_RouteGuide")
    if layer:
        layer.hide_viewport = hide_guide


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    heights = env_terrain.apply()          # shapes the 5 m terrain to the driving profile
    env_corridor.build_data()              # high-res corridor along the track; from now on E.ground_z uses it
    env_plaza.install()                    # V10i: ... and the plaza's own surfaces (deck, courtyard, stairs)
    env_covebank.plan()                    # V10d: 0.5 m lattice where the cove kerb crosses the 5 m grid
    env_streets.plan_terrace()             # V10f: 1 m lattice along the retaining wall of the restaurant terrace
    env_campus.plan_ground()               # V10c2: sunken courtyards and D's pool (trays in the 5 m grid)
    env_entrance.plan_ground()             # V15a: the car-park ramp at the entrance plaza (cells -> apron + ramp floor)
    build_ground(heights)
    env_corridor.build_mesh()
    env_covebank.build_mesh()
    env_streets.build_terrace_mesh()
    env_campus.build_ground_mesh()
    env_entrance.build_ground_mesh()       # V15a
    env_streets.build()                    # V10f: paving, markings, kerbs, walls (before the dressing stands on it)
    build_paths()
    env_buildings.build()
    env_plaza.build()                      # V10i: deck with skylights, stairs, courtyard, fences
    env_entrance.build()                   # V15a: entrance plaza paving with drain channels, car-park ramp
    build_dressing()
    build_route_guide()
    env_track.build()
    env_trackside.build()                  # edge lines + corner signs (readability), exported with the track
    env_tunnel.build()
    carve_drive_throughs()
    env_interior.dress(PASSAGE_RUNS, PASSAGE_WIDTH, PASSAGE_WIDTHS)
    env_backdrop.build(heights)
    add_origin_marker()
    env_materials.upgrade_all()
    for obj in list(bpy.data.collections["Track_RouteGuide"].all_objects):
        obj.hide_render = True               # the guide is data for Role 2, not part of the look
    stats()
    viewer_defaults(hide_guide=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "CampusWorld.blend"))
    viewer_defaults(hide_guide=False)        # the FBX export selects the guide, so it must be visible again
    render_previews()
    export_for_unity()          # last step: it turns the geometry, the .blend above is already saved
    print("done")


main()
