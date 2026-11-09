"""Shared helpers for all environment modules (run inside Blender 5.2, `blender -b -P ...`).

Every env_*.py module imports this file instead of build_world.py, so modules stay independent:
    import env_common as E
    E.ground_z(x, y)          terrain height (m) at a point, from the current height field
    E.material("asphalt")     material from PALETTE (created once, reused)
    E.collection("World_Props")
    E.new_object(name, mesh, collection)
    E.route_points()          list of route dicts (x, y, track_z, s, tunnel, building, level, ...)
    E.distance_to_route(x, y) metres to the route centreline (for keep-clear rules)

World axes: x = east, y = north, z = up, metres, origin = lat 38.6780 / lon -9.3265.
"""
import json
import math
import os

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
TEXTURES = os.path.join(HERE, "textures")
DATA = json.load(open(os.path.join(HERE, "world_data.json")))
ROUTE_FILE = os.path.join(HERE, "route.json")
ROUTE = json.load(open(ROUTE_FILE)) if os.path.exists(ROUTE_FILE) else None

ROUTE_WIDTH = 8.0          # drivable width proposal (m); Role 2 decides the final value
KEEP_CLEAR = ROUTE_WIDTH / 2 + 2.0   # nothing placed closer than this to the route centreline


def half_width(p):
    """Half the road width at a route point: route.json 'width' (narrower in the alley), else ROUTE_WIDTH."""
    return p.get("width", ROUTE_WIDTH) / 2

# Colours: docs/art-style.md palette first, then environment additions (to be added to that doc).
PALETTE = {
    # art-style.md
    "kart_white": "#EAEAEC", "gloss_black": "#1D1D1F", "asphalt": "#55585E", "sand": "#E3C48D",
    "atlantic_blue": "#2D8CD6", "boost_orange": "#FF8A1F",
    # environment additions (lawn, concrete_sand, glass, sea: realism pass 2026-11-09, colours measured
    # in the walk video IMG_7723 and scaled so the white facade matches kart_white)
    "lawn": "#7E9658", "leaf": "#5E8F4E", "leaf_dark": "#3F6B3A", "olive_leaf": "#8A9A6B", "palm_leaf": "#5F8A3E",
    "bark": "#6B4F3A", "bark_light": "#9C8468", "terracotta": "#C8664A", "concrete_sand": "#C6BB98",
    "concrete_light": "#C9C5BC", "calcada": "#E6E1D6", "steel": "#A7ABB2", "glass": "#353B3F",
    "sea": "#34779A", "sea_deep": "#1F5577", "wet_sand": "#C9A877", "building_other": "#C9C5BC",
}


def hex_rgba(h, alpha=1.0):
    def lin(c):
        c = c / 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(int(h[1:3], 16)), lin(int(h[3:5], 16)), lin(int(h[5:7], 16)), alpha)


def material(name, colour=None, roughness=0.85, metallic=0.0, emission=None, alpha=1.0):
    """Material 'W_<name>'. Colour = PALETTE[name] unless a hex colour is given."""
    mat = bpy.data.materials.get("W_" + name)
    if mat:
        return mat
    mat = bpy.data.materials.new("W_" + name)
    rgba = hex_rgba(colour or PALETTE[name], alpha)
    mat.diffuse_color = rgba
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission:
        bsdf.inputs["Emission Color"].default_value = hex_rgba(emission)
        bsdf.inputs["Emission Strength"].default_value = 2.0
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
    return mat


def collection(name, parent=None):
    col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    parent = parent or bpy.context.scene.collection
    if col.name not in parent.children:
        parent.children.link(col)
    return col


def new_object(name, mesh, col):
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    return obj


# ---------- terrain ----------
_grid = {"heights": DATA["grid"]["heights"]}     # build_world.py may replace this with the edited terrain
X0, Y0, X1, Y1 = DATA["bounds"]
STEP, COLS, ROWS = DATA["grid"]["step"], DATA["grid"]["cols"], DATA["grid"]["rows"]


def set_heightfield(heights):
    """Use an edited height field (same grid) from now on."""
    _grid["heights"] = heights


_surface = {"fn": None}


def set_surface(fn):
    """Install a finer surface (env_corridor) that ground_z returns where it exists."""
    _surface["fn"] = fn


def ground_z(x, y):
    """Terrain height: the high-res track corridor where it exists, else the 5 m world grid."""
    fn = _surface["fn"]
    return fn(x, y) if fn else coarse_ground_z(x, y)


def coarse_ground_z(x, y):
    h = _grid["heights"]
    fx = min(max((x - X0) / STEP, 0), COLS - 1.001)
    fy = min(max((y - Y0) / STEP, 0), ROWS - 1.001)
    i, j = int(fx), int(fy)
    ax, ay = fx - i, fy - j
    return h[j][i] * (1 - ax) * (1 - ay) + h[j][i + 1] * ax * (1 - ay) + h[j + 1][i] * (1 - ax) * ay + h[j + 1][i + 1] * ax * ay


def ground_class(x, y):
    """Ground type at a point: ground, grass, wood, paved, pitch, sand, water, sea."""
    i = int(round(min(max((x - X0) / STEP, 0), COLS - 1)))
    j = int(round(min(max((y - Y0) / STEP, 0), ROWS - 1)))
    return DATA["grid"]["classes"][j][i]


def inside_polygon(x, y, poly):
    hit = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            hit = not hit
    return hit


def in_building(x, y, margin=0.0):
    for b in DATA["buildings"]:
        xs = [p[0] for p in b["points"]]
        ys = [p[1] for p in b["points"]]
        if min(xs) - margin <= x <= max(xs) + margin and min(ys) - margin <= y <= max(ys) + margin:
            if inside_polygon(x, y, b["points"]):
                return True
    return False


# ---------- route ----------
def route_points():
    return ROUTE["points"] if ROUTE else []


_route_cells = {}


def _route_index():
    if not _route_cells and ROUTE:
        for p in ROUTE["points"]:
            _route_cells.setdefault((int(p["x"] // 20), int(p["y"] // 20)), []).append(p)
    return _route_cells


def distance_to_route(x, y):
    """Metres from (x, y) to the nearest route point (2 m spacing, good to about 1 m)."""
    cells = _route_index()
    if not cells:
        return float("inf")
    cx, cy = int(x // 20), int(y // 20)
    best = float("inf")
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for p in cells.get((cx + dx, cy + dy), []):
                best = min(best, math.hypot(p["x"] - x, p["y"] - y))
    if best == float("inf"):
        best = min(math.hypot(p["x"] - x, p["y"] - y) for p in ROUTE["points"])
    return best


def nearest_route_point(x, y):
    return min(ROUTE["points"], key=lambda p: (p["x"] - x) ** 2 + (p["y"] - y) ** 2)
