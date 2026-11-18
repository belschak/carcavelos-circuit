"""Key art of the Web loading page: sharp pictures in modern formats, one size per screen (18 Nov 2026).

The four pictures behind the loading page (index.html, .cc-key) are real captures of our own game, shot in the Web
build's photo mode (?photo=1&dpr=2: no menus, no HUD, the canvas at 2x the window). They were rendered at 6400 x 3600
(window 3200 x 1800 at device pixel ratio 2) from a build without the Web texture crunch, so every texture is at its
full editor quality. This script turns those raw PNGs into what the page loads:

  1. optional crop (a 16:9 box, e.g. to drop a strip of sky), then one high-quality downscale (Lanczos) to the
     3200 x 1800 master: every output pixel is the average of about four rendered ones (supersampling = no jaggies)
  2. the master in several widths, each as AVIF (smallest), WebP (older Safari / Edge) and JPEG (fallback)
  3. index.html picks with <picture>: the browser takes the first format it supports and, from `srcset` + `sizes`,
     the smallest width that still fills the screen sharply. Phones only ever see the 960 / 1600 files.

Writes into Assets/WebGLTemplates/CarcavelosCircuit/TemplateData:
  keyart-<shot>-<width>.avif / .webp   widths 960, 1600, 1920, 2400, 3200
  keyart-<shot>-<width>.jpg            widths 960, 1920 (only browsers without AVIF and WebP)
New files need a .meta next to them (Unity is the only one that may make them; on a laptop without Unity copy an
existing .meta of the same kind and give it a new random GUID).
Needs Pillow 11+ (AVIF + WebP built in). Run from the repo root:
  python tools/ui_art/make_keyart.py <folder with the raw PNGs>
"""
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/WebGLTemplates/CarcavelosCircuit/TemplateData"

MASTER = (3200, 1800)
WIDTHS = [960, 1600, 1920, 2400, 3200]
JPEG_WIDTHS = [960, 1920]

# Quality per format: chosen by eye at 100 % on the 1920 file (no visible blocks or banding in the sky), then
# checked against the page budget (the four pictures a 1920 x 1080 screen loads stay under the old 880 KB).
AVIF_QUALITY = 60
WEBP_QUALITY = 78
JPEG_QUALITY = 84

# shot name -> (raw file, crop box in raw pixels or None). Crops keep 16:9.
SHOTS = {
    "grid": ("grid-sunset-6400.png", None),            # the start grid: Rookie, Tiago, Nova, Amara + two rivals
    "beach": ("beach-sunset-6400.png", (320, 360, 6080, 3600)),  # sunset over the Atlantic; top strip of cloud cut
    "campus": ("campus-pack-6400.png", None),          # the pack bursting into the atrium under the START arch
    "items": ("itemboxes-sunset-6400.png", None),      # a kart through the azulejo item boxes
}


def master_of(raw_folder, raw_name, crop):
    image = Image.open(raw_folder / raw_name).convert("RGB")
    if crop is not None:
        image = image.crop(crop)
    return image.resize(MASTER, Image.Resampling.LANCZOS)


def export(master, shot):
    sizes = {}
    for width in WIDTHS:
        height = round(width * MASTER[1] / MASTER[0])
        picture = master if width == MASTER[0] else master.resize((width, height), Image.Resampling.LANCZOS)
        for ext, options in (("avif", {"quality": AVIF_QUALITY, "speed": 4}),
                             ("webp", {"quality": WEBP_QUALITY, "method": 6})):
            path = DATA / f"keyart-{shot}-{width}.{ext}"
            picture.save(path, **options)
            sizes[path.name] = path.stat().st_size
        if width in JPEG_WIDTHS:
            path = DATA / f"keyart-{shot}-{width}.jpg"
            picture.save(path, quality=JPEG_QUALITY, optimize=True, progressive=True, subsampling="4:2:0")
            sizes[path.name] = path.stat().st_size
    return sizes


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(1)
    raw_folder = Path(sys.argv[1])
    total = {}
    for shot, (raw_name, crop) in SHOTS.items():
        total.update(export(master_of(raw_folder, raw_name, crop), shot))
    for name in sorted(total):
        print(f"{name:32} {total[name] / 1024:8.1f} KB")


if __name__ == "__main__":
    main()
