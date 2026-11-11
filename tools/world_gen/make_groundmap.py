"""Paints the ground colour map (one texture over the whole world) from the OSM shapes.

Run with the media-tools Python after world_data.py:  ~/Documents/nova/.media-tools/bin/python make_groundmap.py
Output: textures/ground_colour.png (2048 px wide, about 0.34 m per pixel), out/groundmap_preview.png
Smooth borders (drawn at 3x and scaled down), subtle variation, soft contact shadows around buildings,
footpaths, a wet-sand strip along the real coastline. Ground mesh UVs: u = (x - X0) / width, v = (y - Y0) / height.
"""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = json.load(open(os.path.join(HERE, "world_data.json")))
X0, Y0, X1, Y1 = DATA["bounds"]
W = 2048
H = int(round(W * (Y1 - Y0) / (X1 - X0)))
SS = 3                                          # supersampling for smooth edges

COLOURS = {
    "ground": (166, 170, 130), "grass": (143, 179, 122), "wood": (104, 140, 90), "paved": (201, 197, 188),
    "pitch": (122, 170, 104), "sand": (227, 196, 141), "water": (64, 140, 200), "seabed": (58, 118, 150),
    "wet_sand": (201, 168, 119), "road": (85, 88, 94), "footway": (218, 213, 200), "steps": (200, 192, 178),
}
ROADS = {"primary", "primary_link", "secondary", "tertiary", "residential", "unclassified", "service", "living_street"}


def to_px(x, y, scale=1):
    return ((x - X0) / (X1 - X0) * W * scale, (Y1 - y) / (Y1 - Y0) * H * scale)


def fbm(h, w, seed, octaves=5):
    """Periodic-free smooth noise (sum of blurred random layers), values about 0..1."""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cell = 2 ** (o + 3)
        small = rng.random((h // cell + 2, w // cell + 2)).astype(np.float32)
        layer = np.array(Image.fromarray((small * 255).astype(np.uint8)).resize((w + cell, h + cell), Image.BICUBIC),
                         np.float32)[:h, :w] / 255.0
        out += layer * amp
        total += amp
        amp *= 0.5
    return out / total


MASK_W = 1024
SHORE_FILE = os.path.join(HERE, "textures", "sea_shore_data.png")       # env_backdrop.write_shore_map
SHORE_MARGIN = (300.0, 400.0)                                            # env_backdrop.shore_rect
WET_RANGE = 20.0                                                         # metres of beach coded in the blue channel


def write_mask(sea):
    """textures/ground_mask_data.png (1024 px wide, same UVs as ground_colour.png; linear numbers, not colour) for
    the ground shader: R = lawn (grass, wood, pitch), G = sand, both without the paths that cross them (open ground
    counts as a bit of both, paved areas as neither), and
    B = how far up the beach a point is, 0 at the waterline .. 1 at WET_RANGE metres or more (wet sand band, dry
    sand further up). The waterline comes from the shore map of the sea (the real 3D shoreline) when it exists,
    else from the 5 m sea mask."""
    from scipy.ndimage import distance_transform_edt
    big = Image.new("RGB", (W * SS, H * SS), (140, 90, 0))      # open ground: dry scrub, part lawn and part sand
    draw = ImageDraw.Draw(big)
    for kind in ("grass", "wood", "paved", "pitch", "sand", "water"):
        fill = {"grass": (255, 0, 0), "wood": (255, 0, 0), "pitch": (255, 0, 0), "sand": (0, 255, 0)}.get(kind, (0, 0, 0))
        for area in DATA["areas"]:
            if area["kind"] == kind:
                draw.polygon([to_px(x, y, SS) for x, y in area["ring"]], fill=fill)
    for p in DATA["paths"]:
        if not p["tunnel"]:
            width = max(1, int(p["width"] / (X1 - X0) * W * SS))
            draw.line([to_px(x, y, SS) for x, y in p["points"]], fill=(0, 0, 0), width=width, joint="curve")
    mh = int(round(MASK_W * H / W))
    mask = np.asarray(big.resize((MASK_W, mh), Image.LANCZOS), np.float32) / 255.0
    if os.path.exists(SHORE_FILE):                         # sea = shore distance > 0, cut to the world bounds
        shore = Image.open(SHORE_FILE).convert("L")
        sx0, sy0, sx1, sy1 = X0 - SHORE_MARGIN[0], Y0 - SHORE_MARGIN[1], X1 + SHORE_MARGIN[0], Y1
        box = ((X0 - sx0) / (sx1 - sx0) * shore.width, (sy1 - Y1) / (sy1 - sy0) * shore.height,
               (X1 - sx0) / (sx1 - sx0) * shore.width, (sy1 - Y0) / (sy1 - sy0) * shore.height)
        water = np.asarray(shore.resize((MASK_W, mh), Image.BILINEAR, box=box)) > 0
        from scipy.ndimage import binary_dilation       # only the sea itself, not gaps in the ground further inland
        open_sea = np.asarray(sea.resize((MASK_W, mh), Image.BILINEAR)) > 127
        water &= binary_dilation(open_sea, iterations=int(20.0 / ((X1 - X0) / MASK_W)))
    else:
        water = np.asarray(sea.resize((MASK_W, mh), Image.BILINEAR)) > 127
    metres = distance_transform_edt(~water) * (X1 - X0) / MASK_W
    out = np.dstack([mask[..., 0], mask[..., 1], np.clip(metres / WET_RANGE, 0.0, 1.0)])
    Image.fromarray(np.round(out * 255.0).astype(np.uint8), "RGB").save(os.path.join(HERE, "textures", "ground_mask_data.png"))
    print(f"ground_mask_data.png {MASK_W}x{mh} px")


def main():
    os.makedirs(os.path.join(HERE, "textures"), exist_ok=True)
    big = Image.new("RGB", (W * SS, H * SS), COLOURS["ground"])
    draw = ImageDraw.Draw(big)

    # sea: the 5 m grid sea mask, smoothly upscaled and thresholded (the 3D shoreline follows the OSM coastline)
    classes = DATA["grid"]["classes"]
    mask = np.array([[1.0 if c == "sea" else 0.0 for c in row] for row in classes[::-1]], np.float32)
    sea = Image.fromarray((mask * 255).astype(np.uint8)).resize((W * SS, H * SS), Image.BILINEAR)
    sea = sea.point(lambda v: 255 if v > 127 else 0)
    big.paste(Image.new("RGB", big.size, COLOURS["seabed"]), mask=sea)

    for kind in ("grass", "wood", "paved", "pitch", "sand", "water"):
        for area in DATA["areas"]:
            if area["kind"] == kind:
                draw.polygon([to_px(x, y, SS) for x, y in area["ring"]], fill=COLOURS[kind])

    # paths: roads, footways, steps
    for p in DATA["paths"]:
        if p["tunnel"]:
            continue
        colour = COLOURS["road"] if p["type"] in ROADS else (COLOURS["steps"] if p["type"] == "steps" else COLOURS["footway"])
        width = max(1, int(p["width"] / (X1 - X0) * W * SS))
        draw.line([to_px(x, y, SS) for x, y in p["points"]], fill=colour, width=width, joint="curve")

    # coastline: wet sand strip
    for line in DATA["coast"]:
        draw.line([to_px(x, y, SS) for x, y in line], fill=COLOURS["wet_sand"], width=int(4 / (X1 - X0) * W * SS))

    img = big.resize((W, H), Image.LANCZOS)
    arr = np.asarray(img, np.float32)
    write_mask(sea)

    # soft contact shadow around buildings (baked ambient occlusion on the ground)
    shadow = Image.new("L", (W, H), 0)
    sd = ImageDraw.Draw(shadow)
    for b in DATA["buildings"]:
        sd.polygon([to_px(x, y) for x, y in b["points"]], fill=255)
    shadow = shadow.filter(ImageFilter.GaussianBlur(6))
    occl = np.asarray(shadow, np.float32)[..., None] / 255.0
    arr *= 1.0 - 0.25 * occl

    # subtle large and small variation so big lawns don't look flat
    var = fbm(H, W, 7) - 0.5
    arr *= (1.0 + 0.16 * var)[..., None]
    arr = np.clip(arr, 0, 255).astype(np.uint8)

    os.makedirs(os.path.join(HERE, "textures"), exist_ok=True)
    os.makedirs(os.path.join(HERE, "out"), exist_ok=True)
    Image.fromarray(arr).save(os.path.join(HERE, "textures", "ground_colour.png"))
    Image.fromarray(arr).resize((1024, int(1024 * H / W))).save(os.path.join(HERE, "out", "groundmap_preview.png"))
    print(f"ground_colour.png {W}x{H} px, {(X1 - X0) / W:.2f} m per pixel")


if __name__ == "__main__":
    main()
