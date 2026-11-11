"""V16h: where the ground's fine "near" tile lies. Shared by make_groundmap.py (colour), make_shadowmap.py (baked sun
shadow, in Blender) and export_unity_bundle.py (the rectangle for Ground.shader). No dependencies.

The whole world keeps its one map (ground_colour.png, 2048 px wide, 0.34 m per pixel). The lap and its surroundings get
a second, finer map (ground_colour_near.png, 2048 px wide): the lap's bounding box with a margin, about 0.2 m per pixel.
"""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
WORLD_W = 2048
NEAR_W = 2048
NEAR_MARGIN = 20.0            # metres round the lap's bounding box


def world_rect():
    return tuple(json.load(open(os.path.join(HERE, "world_data.json")))["bounds"])


def near_rect():
    """(x0, y0, x1, y1) in map metres: the lap's bounding box with the margin, on whole 5 m, inside the world."""
    wx0, wy0, wx1, wy1 = world_rect()
    pts = json.load(open(os.path.join(HERE, "route.json")))["points"]
    xs, ys = [p["x"] for p in pts], [p["y"] for p in pts]
    return (max(math.floor((min(xs) - NEAR_MARGIN) / 5.0) * 5.0, wx0), max(math.floor((min(ys) - NEAR_MARGIN) / 5.0) * 5.0, wy0),
            min(math.ceil((max(xs) + NEAR_MARGIN) / 5.0) * 5.0, wx1), min(math.ceil((max(ys) + NEAR_MARGIN) / 5.0) * 5.0, wy1))


def near_size():
    x0, y0, x1, y1 = near_rect()
    return NEAR_W, int(round(NEAR_W * (y1 - y0) / (x1 - x0)))


# What receives the baked sun shadow (name prefixes of build_world's objects): the top-down bake camera of
# make_shadowmap.py sees these, and export_unity_bundle.py marks the materials that are used on nothing else
# ("ground_shadow": WorldLit.shader reads the baked shadow for them). Objects with the ground material count too.
RECEIVERS = ("World_Ground", "Track_", "Street_Paving", "Street_NoCollide", "Street_Promenade", "Entrance_",
             "World_Campus", "Promenade_")
