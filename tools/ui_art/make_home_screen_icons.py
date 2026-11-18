"""Home screen icons for the Web page (phone round 17.11, Role 4): "Add to Home Screen" on a phone shows these.
A square crop of our own key art (the start grid under the START arch, keyart-1.jpg, a capture from the build's photo
mode) with a soft grade, and the diamond tile of the favicon / HUD countdown in the corner. No transparency: iPhones
round the corners themselves, Android masks the "maskable" icon (the arch and kart stay inside the safe 80 % circle).
Run from the repo root: python tools/ui_art/make_home_screen_icons.py (Pillow). Writes TemplateData/icon-*.png."""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/WebGLTemplates/CarcavelosCircuit/TemplateData"
SS = 4  # supersampling for the diamond's smooth edges


def diamond_tile(size):
    """The favicon mark: navy rounded square, white diamond, orange inner diamond (same shapes as the favicon SVG)."""
    s = size * SS
    tile = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(tile)
    draw.rounded_rectangle((0, 0, s - 1, s - 1), radius=s // 4, fill=(18, 59, 94, 255))
    c = s / 2
    for half, colour in ((s * 0.40, (234, 234, 236, 255)), (s * 0.20, (255, 138, 31, 255))):
        draw.polygon([(c, c - half), (c + half, c), (c, c + half), (c - half, c)], fill=colour)
    return tile.resize((size, size), Image.LANCZOS)


def make_icon(size):
    art = Image.open(DATA / "keyart-1.jpg").convert("RGB")  # 1600 x 900
    crop = art.crop((330, 0, 1230, 900)).resize((size, size), Image.LANCZOS).convert("RGBA")
    # Soft dark fall-off at the bottom so the mark reads on the cobblestones.
    shade = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(shade)
    for y in range(size):
        t = max(0.0, (y / size - 0.55) / 0.45)
        draw.line([(0, y), (size, y)], fill=(7, 9, 13, int(150 * t)))
    crop.alpha_composite(shade)
    mark = diamond_tile(round(size * 0.24))
    pad = round(size * 0.14)  # inside the maskable safe zone
    crop.alpha_composite(mark, (size - pad - mark.width, size - pad - mark.height))
    return crop.convert("RGB")


for px in (180, 192, 512):
    # 256 colours (dithered): a quarter of the bytes, no visible loss at home screen size.
    make_icon(px).quantize(256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG).save(DATA / f"icon-{px}.png", optimize=True)
    print("wrote", DATA / f"icon-{px}.png")
