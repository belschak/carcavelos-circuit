"""Paints the ground colour map (one texture over the whole world) from the OSM shapes and the traced coast.

Run with the media-tools Python after world_data.py:  ~/Documents/nova/.media-tools/bin/python make_groundmap.py
Output: textures/ground_colour.png (2048 px wide, about 0.34 m per pixel), textures/ground_mask_data.png (for
Ground.shader), out/groundmap_preview.png.
Smooth borders (drawn at 3x and scaled down), subtle variation, soft contact shadows around buildings, footpaths.
The coast comes from coast_trace.json (coast.py), traced from the orthophoto: the sea is everything seaward of the
traced waterline (dark wet sand under the water, only a sliver of it ever shows), the beach zone is pale dry sand with
a darker wet band at the water, a slightly darker cove, faint tide lines, and ochre rock shelves with cracks.
Ground mesh UVs: u = (x - X0) / width, v = (y - Y0) / height.
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

import campus_plan
import coast
import street_plan

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = json.load(open(os.path.join(HERE, "world_data.json")))
X0, Y0, X1, Y1 = DATA["bounds"]
W = 2048
H = int(round(W * (Y1 - Y0) / (X1 - X0)))
SS = 3                                          # supersampling for smooth edges
MPP = (X1 - X0) / W                             # metres per pixel

COLOURS = {
    "ground": (166, 170, 130), "grass": (143, 179, 122), "wood": (104, 140, 90), "paved": (201, 197, 188),
    "pitch": (122, 170, 104), "sand": (232, 211, 174), "water": (64, 140, 200),
    "road": (85, 88, 94), "footway": (218, 213, 200), "steps": (200, 192, 178),
}
# the coast, stylised from the orthophoto samples (dry sand #d0ba9a, cove #c2aa8a, wet #b49c7a, rock #8e7d59)
# V10d (2026-11-11): judged in Unity under the golden-hour light and grading, which warm and saturate the sand
# (painted #DBC296 showed as about #BEA67C, hue saturation 0.35): paler and less saturated, still warm
SAND_DRY = np.array((232, 211, 174), np.float32)       # main beach (#E8D3AE)
SAND_COVE = np.array((219, 198, 162), np.float32)      # the sheltered cove: a little darker
SAND_WET = np.array((193, 171, 137), np.float32)       # the band the swash wets
SEABED = np.array((158, 140, 111), np.float32)         # under the water (only a sliver at the waterline shows)
ROCK = np.array((150, 136, 104), np.float32)           # ochre to grey-brown limestone (tops; Ground.shader darkens
ROCK_DARK = np.array((100, 90, 70), np.float32)        # the faces and wets the foot), cracks and strata lines
ROCK_WET = np.array((118, 112, 92), np.float32)        # rock in the swash: wet, a little green
WET_BAND = 11.0                                        # m of darker sand above the waterline
ROCK_PAD = 0.25                                        # m: rock colour reaches this far beyond a rock piece (V10d:
                                                       # the 3D rocks read their colour 0.9 m inside, so no halo)
FILL_AS_AREA = set(coast.C.get("fill_as_area", []))      # OSM ways painted as filled paved areas (the promenade)
OPEN_TO_PAVED = coast.C.get("open_to_paved", [])         # V10d: open ground that is paved (round the beach restaurants)
ROADS = {"primary", "primary_link", "secondary", "tertiary", "residential", "unclassified", "service", "living_street"}


# V10f: the street section (street_plan.py). Under the paving the ground is not seen; the map carries the same shapes
# so lawns, the dry meadow and the planted banks are right, the grass tufts keep off the paving, and nothing of the
# old OSM road bands shows beside the new streets.
STREET_COLOURS = {"concrete": (205, 196, 182), "bays": (205, 196, 182), "brick": (156, 127, 107), "setts": (142, 138, 132),
                  "calcada": (230, 225, 214), "asphalt": (85, 88, 94), "kerb": (207, 201, 188)}
STREET_GROUND = {"lawn": (143, 179, 122), "meadow": (178, 176, 126), "bank": (128, 134, 88), "soil": (138, 115, 85),
                 "paved": (201, 197, 188), "dust": (190, 172, 138), "gravel": (208, 198, 174)}
STREET_LAWN = {"lawn": 255, "meadow": 235, "bank": 150, "soil": 0, "paved": 0, "dust": 60, "gravel": 0}   # mask red: 255 = lawn


def street_layers(scale, lawn_mask=False):
    """(colour RGB, coverage) of the street plan's layers, drawn in order at `scale` pixels per map pixel."""
    img = Image.new("RGBA", (int(W * scale), int(H * scale)), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for lay in street_plan.LAYERS:
        if lay["mat"] is None or lay["mat"] == "lawn":
            c = (STREET_LAWN[lay["ground"]],) * 3 if lawn_mask else STREET_GROUND[lay["ground"]]
        else:
            c = (0, 0, 0) if lawn_mask else STREET_COLOURS.get(lay["mat"]) or tuple(
                int(street_plan.PALETTE[lay["mat"][4:]][k:k + 2], 16) for k in (1, 3, 5))
        d.polygon([to_px(x, y, scale) for x, y in lay["ring"]], fill=c + (255,))
    return img


def campus_layers(scale, lawn_mask=False):
    """V10c2: (colour RGB, coverage) of the campus plan's ground paint (campus_plan.GROUND_PAINT: entrance plaza,
    courtyards, lower plaza, pads and paths), drawn in order at `scale` pixels per map pixel."""
    img = Image.new("RGBA", (int(W * scale), int(H * scale)), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for lay in campus_plan.GROUND_PAINT:
        c = (lay["lawn"],) * 3 if lawn_mask else lay["colour"]
        d.polygon([to_px(x, y, scale) for x, y in lay["ring"]], fill=tuple(c) + (255,))
    return img


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


def polygon_mask(rings, w=W, h=H, pad_m=0.0):
    """Float mask (h, w), 1 inside any of the rings, anti-aliased (drawn at SS x and scaled down)."""
    img = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(img)
    s = w / W
    for ring in rings:
        d.polygon([to_px(x, y, SS * s) for x, y in ring], fill=255)
    if pad_m > 0:
        img = img.filter(ImageFilter.MaxFilter(2 * int(round(pad_m / MPP * SS * s)) + 1))
    return np.asarray(img.resize((w, h), Image.LANCZOS), np.float32) / 255.0


def is_fake_water(area):
    return area["kind"] == "water" and any(coast.inside(px, py, area["ring"]) for px, py in coast.C["water_to_lawn"])


def area_kind(area):
    return "grass" if is_fake_water(area) else area["kind"]


def coast_masks(w=W, h=H):
    """sea, beach zone, cove and rock masks (float, (h, w)) and the distance in metres from the water."""
    from scipy.ndimage import distance_transform_edt
    sea = polygon_mask([coast.sea_polygon(inland=0.0, west_x=X0 - 50, south_y=Y0 - 50, east_x=X1 + 50)], w, h)
    beach = polygon_mask([coast.BEACH_ZONE], w, h) * (1.0 - sea)
    cove = polygon_mask([coast.COVE], w, h) * (1.0 - sea)
    rock = polygon_mask([p["ring"] for p in coast.rock_pieces()], w, h, pad_m=ROCK_PAD)   # V10d: the broken-up pieces
    metres = distance_transform_edt(sea < 0.5) * (X1 - X0) / w
    return sea, beach, cove, rock, metres


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def paint_coast(arr, path_alpha):
    """Sea floor, sand (dry, wet band, cove, tide lines) and rock shelves over the painted areas; paths stay on top."""
    sea, beach, cove, rock, metres = coast_masks()
    hh, ww = arr.shape[:2]
    grain = fbm(hh, ww, 21, octaves=4) - 0.5
    swell = np.asarray(Image.fromarray((fbm(hh // 8, ww // 8, 25, 3) * 255).astype(np.uint8))   # slow wobble (20-80 m)
                       .resize((ww, hh), Image.BICUBIC), np.float32) / 255.0 - 0.5          # for the wet edge, tide lines
    streak = np.asarray(Image.fromarray((fbm(hh // 4, ww // 4, 22, 3) * 255).astype(np.uint8))
                        .resize((ww, hh * 3), Image.BICUBIC).resize((ww, hh), Image.BILINEAR), np.float32) / 255.0 - 0.5
    # sand: wet band at the water, dry above it (a little paler far up the beach), the cove a shade darker
    dry = SAND_DRY * (1.0 + 0.035 * smoothstep(30.0, 110.0, metres))[..., None]
    from scipy.ndimage import gaussian_filter
    cove = gaussian_filter(cove, 8.0)                                   # the cove's sand fades into the beach's
    dry = dry + (SAND_COVE - SAND_DRY) * cove[..., None]
    wet = smoothstep(WET_BAND, 1.5, metres + swell * 8.0 + grain * 1.5)
    sand = dry + (SAND_WET - dry) * wet[..., None]
    # faint tide lines: thin, slightly darker, wobbling bands of drift about 14 and 23 m up the beach
    for at, width, depth in ((14.5, 0.8, 0.09), (23.0, 0.6, 0.05)):
        line = np.exp(-((metres - at - swell * 9.0) / width) ** 2)
        sand *= (1.0 - depth * line)[..., None]
    sand *= (1.0 + 0.05 * grain + 0.04 * streak * cove)[..., None]       # wind streaks in the cove
    # rock: ochre shelves in layers: thin dark strata lines along the coast, a few cracks across them, coarse
    # grain and blotches, wet and greenish in the swash
    rows, cols = np.mgrid[0:hh, 0:ww].astype(np.float32)
    u = (cols * np.cos(0.45) - rows * np.sin(0.45)) * MPP                # metres along about WNW, the shore's direction
    layer = (u / 1.7 + 2.2 * (fbm(hh, ww, 26, 3) - 0.5)) % 1.0
    strata = np.exp(-((layer - 0.5) / 0.07) ** 2)
    cracks = np.exp(-((fbm(hh, ww, 23, octaves=3) - 0.5) / 0.008) ** 2)
    rock_col = ROCK * (1.0 + 0.25 * grain + 0.12 * (fbm(hh, ww, 24, 2) - 0.5))[..., None]
    rock_col = rock_col + (ROCK_DARK - rock_col) * np.clip(0.55 * strata + 0.6 * cracks, 0.0, 0.8)[..., None]
    rock_col = rock_col + (ROCK_WET - rock_col) * smoothstep(7.0, 1.0, metres)[..., None]
    beach = np.clip(gaussian_filter(beach, 2.5) * 1.15, 0.0, 1.0) * (1.0 - sea)   # soft sand-to-grass edge (~1 m)
    keep = 1.0 - path_alpha
    out = arr + (SEABED - arr) * sea[..., None]
    out = out + (sand - out) * (beach * keep)[..., None]
    out = out + (rock_col - out) * (rock * keep)[..., None]               # V10d: also the pieces standing in the swash
    return out


MASK_W = 1024
WET_RANGE = 20.0                                                         # metres of beach coded in the blue channel


def write_mask():
    """textures/ground_mask_data.png (1024 px wide, same UVs as ground_colour.png; linear numbers, not colour) for
    the ground shader: R = lawn (grass, wood, pitch), G = sand (OSM sand and the beach zone), both without the paths
    that cross them (open ground counts as a bit of both, paved areas as neither),
    B = how far up the beach a point is, 0 at the traced waterline .. 1 at WET_RANGE metres or more (wet band, dry
    sand further up), A = 1 - rock (the rock shelves; a mask without alpha reads as no rock)."""
    big = Image.new("RGB", (W * SS, H * SS), (140, 90, 0))      # open ground: dry scrub, part lawn and part sand
    draw = ImageDraw.Draw(big)
    for ring in OPEN_TO_PAVED:                                   # V10d: paved (neither lawn nor sand)
        draw.polygon([to_px(x, y, SS) for x, y in ring], fill=(0, 0, 0))
    for kind in ("grass", "wood", "paved", "pitch", "sand", "water"):
        fill = {"grass": (255, 0, 0), "wood": (255, 0, 0), "pitch": (255, 0, 0), "sand": (0, 255, 0)}.get(kind, (0, 0, 0))
        for area in DATA["areas"]:
            if area_kind(area) == kind:
                draw.polygon([to_px(x, y, SS) for x, y in area["ring"]], fill=fill)
    paths = Image.new("L", (W * SS, H * SS), 0)
    pd = ImageDraw.Draw(paths)
    for p in DATA["paths"]:
        if p["id"] in FILL_AS_AREA:
            pd.polygon([to_px(x, y, SS) for x, y in p["points"]], fill=255)
        elif not p["tunnel"]:
            width = max(1, int(p["width"] / (X1 - X0) * W * SS))
            pd.line([to_px(x, y, SS) for x, y in p["points"]], fill=255, width=width, joint="curve")
    mh = int(round(MASK_W * H / W))
    mask = np.asarray(big.resize((MASK_W, mh), Image.LANCZOS), np.float32) / 255.0
    path = np.asarray(paths.resize((MASK_W, mh), Image.LANCZOS), np.float32) / 255.0
    sea, beach, _cove, rock, metres = coast_masks(MASK_W, mh)
    land = 1.0 - sea
    campus = np.asarray(campus_layers(SS, lawn_mask=True).resize((MASK_W, mh), Image.LANCZOS), np.float32) / 255.0
    cover = campus[..., 3]                                                      # V10c2: campus paving is no lawn
    mask[..., 0] = mask[..., 0] * (1.0 - cover) + campus[..., 0] * cover
    mask[..., 1] = mask[..., 1] * (1.0 - cover)
    path = path * (1.0 - cover)
    streets = np.asarray(street_layers(SS, lawn_mask=True).resize((MASK_W, mh), Image.LANCZOS), np.float32) / 255.0
    cover = streets[..., 3]
    mask[..., 0] = mask[..., 0] * (1.0 - cover) + streets[..., 0] * cover       # V10f: lawn as the street plan says
    mask[..., 1] = mask[..., 1] * (1.0 - cover)
    path = path * (1.0 - cover)
    lawn = mask[..., 0] * (1.0 - beach) * (1.0 - path) * land
    sand = np.clip(mask[..., 1] * land + beach, 0.0, 1.0) * (1.0 - rock) * (1.0 - path)
    out = np.dstack([lawn, sand, np.clip(metres / WET_RANGE, 0.0, 1.0), 1.0 - rock])
    Image.fromarray(np.round(out * 255.0).astype(np.uint8), "RGBA").save(os.path.join(HERE, "textures", "ground_mask_data.png"))
    print(f"ground_mask_data.png {MASK_W}x{mh} px (RGBA: lawn, sand, beach height, 1 - rock)")


def main():
    os.makedirs(os.path.join(HERE, "textures"), exist_ok=True)
    big = Image.new("RGB", (W * SS, H * SS), COLOURS["ground"])
    draw = ImageDraw.Draw(big)
    for ring in OPEN_TO_PAVED:                           # V10d: under the OSM areas, so they still paint over it
        draw.polygon([to_px(x, y, SS) for x, y in ring], fill=COLOURS["paved"])
    for kind in ("grass", "wood", "paved", "pitch", "sand", "water"):
        for area in DATA["areas"]:
            if area_kind(area) == kind:
                draw.polygon([to_px(x, y, SS) for x, y in area["ring"]], fill=COLOURS[kind])

    # paths: roads, footways, steps (a separate layer, so they stay on top of the beach)
    paths = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    pd = ImageDraw.Draw(paths)
    for p in DATA["paths"]:
        if p["tunnel"]:
            continue
        colour = COLOURS["road"] if p["type"] in ROADS else (COLOURS["steps"] if p["type"] == "steps" else COLOURS["footway"])
        if p["id"] in FILL_AS_AREA:                      # a paved area (the promenade): filled, not stroked
            pd.polygon([to_px(x, y, SS) for x, y in p["points"]], fill=colour + (255,))
            continue
        width = max(1, int(p["width"] / (X1 - X0) * W * SS))
        pd.line([to_px(x, y, SS) for x, y in p["points"]], fill=colour + (255,), width=width, joint="curve")

    arr = np.asarray(big.resize((W, H), Image.LANCZOS), np.float32)
    layer = np.asarray(paths.resize((W, H), Image.LANCZOS), np.float32)
    alpha = layer[..., 3] / 255.0
    path_rgb = layer[..., :3]                     # Pillow scales RGBA premultiplied and returns straight colour
    arr = paint_coast(arr, alpha)
    arr = arr + (path_rgb - arr) * alpha[..., None]
    campus = np.asarray(campus_layers(SS).resize((W, H), Image.LANCZOS), np.float32)       # V10c2: campus paving
    arr = arr + (campus[..., :3] - arr) * (campus[..., 3:4] / 255.0)
    # V15a: the entrance plaza in its calcada colour (entrance_plan.py): seen only on the road's shoulders, the rest
    # lies under the paving mesh of env_entrance.py
    import entrance_plan
    ent = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(ent).polygon([to_px(x, y, SS) for x, y in entrance_plan.RING], fill=255)
    ent = np.asarray(ent.resize((W, H), Image.LANCZOS), np.float32)[..., None] / 255.0
    arr = arr + (np.array(entrance_plan.CALCADA_RGB, np.float32) - arr) * ent
    streets = np.asarray(street_layers(SS).resize((W, H), Image.LANCZOS), np.float32)      # V10f: over everything
    arr = arr + (streets[..., :3] - arr) * (streets[..., 3:4] / 255.0)
    write_mask()

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

    os.makedirs(os.path.join(HERE, "out"), exist_ok=True)
    Image.fromarray(arr).save(os.path.join(HERE, "textures", "ground_colour.png"))
    Image.fromarray(arr).resize((1024, int(1024 * H / W))).save(os.path.join(HERE, "out", "groundmap_preview.png"))
    print(f"ground_colour.png {W}x{H} px, {MPP:.2f} m per pixel")


if __name__ == "__main__":
    main()
