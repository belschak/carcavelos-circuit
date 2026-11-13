"""Procedural textures of the campus west package (V16c, 2026-11-11). No photos: numbers and averaged colours come
from the photosphere survey V15 (Appendix B: W03, W05). Run:  python3 make_west_textures.py

  deck_sawcut_albedo.png          512 px = 6 x 6 m. The CTT terrace deck: almost white smooth in-situ concrete
                                  (#efeeda) with hairline saw cuts: two lines along the deck (3 m apart) and one
                                  across (6 m). env_campus_west lays it along the deck's axis.
  brick_screen_albedo.png         256 px = 1 x 1 m. Perforated terracotta brick screen (#755b41): 15 courses,
                                  every second brick left out (dark hole).
  railing_tan_cutout_albedo.png   512 x 256 RGBA = 2.0 x 1.1 m. One module of the terrace railing: flat bars 11 cm
                                  apart, flat top rail, bottom rail 10 cm above the deck, half a flat post at each
                                  end (posts every 2 m), bronze-tan (#a09075). Alpha = steel / air.
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "textures")


def rgb(h):
    return tuple(int(h[k:k + 2], 16) for k in (1, 3, 5))


def tile_noise(n, cell, seed):
    """Smooth noise 0..1 that repeats at the tile edge (random lattice, wrapped, cubic)."""
    rng = np.random.default_rng(seed)
    g = n // cell
    small = rng.random((g, g)).astype(np.float32)
    big = np.tile(small, (3, 3))
    img = Image.fromarray((big * 255).astype(np.uint8)).resize((3 * n, 3 * n), Image.BICUBIC)
    return np.asarray(img, np.float32)[n:2 * n, n:2 * n] / 255.0


def make_deck(n=512, seed=1603):
    base = np.array(rgb("#E9E7D4"), np.float32)             # #efeeda in full sun; a step down so it does not burn out
    cloud = tile_noise(n, 64, seed) * 0.6 + tile_noise(n, 16, seed + 1) * 0.4
    grain = np.random.default_rng(seed + 2).normal(0.0, 1.0, (n, n)).astype(np.float32)
    img = base[None, None, :] * (0.965 + 0.05 * cloud[..., None]) + grain[..., None] * 1.2
    warm = tile_noise(n, 128, seed + 3)                     # faint warm patches (the stain at the ramp foot)
    img[..., 2] -= 5.0 * np.clip(warm - 0.55, 0, 1) * 2
    line = np.zeros((n, n), np.float32)
    for v in (0, n // 2):                                   # along the deck, every 3 m
        line[v, :] = 1.0
        line[(v + 1) % n, :] = np.maximum(line[(v + 1) % n, :], 0.45)
    line[:, 0] = 1.0                                        # across, every 6 m
    line[:, 1] = np.maximum(line[:, 1], 0.45)
    cut = np.array(rgb("#8E8A7A"), np.float32)
    img = img * (1 - 0.8 * line[..., None]) + cut[None, None, :] * 0.8 * line[..., None]
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB").save(os.path.join(TEX, "deck_sawcut_albedo.png"))


def make_brick_screen(n=256, seed=1604):
    rng = np.random.default_rng(seed)
    img = Image.new("RGB", (n, n), rgb("#5E4A37"))          # mortar / shadow
    d = ImageDraw.Draw(img)
    courses, per = 16, 4                                    # 6.25 cm courses, 25 cm module (brick + hole)
    ch, bw = n / courses, n / per
    base = np.array(rgb("#8C6C4D"), np.float32)             # #755b41 in the photo; a step up for the game's light
    hole = rgb("#3A2C20")
    for c in range(courses):
        off = (bw / 2) if c % 2 else 0.0
        for k in range(-1, per + 1):
            x0 = k * bw + off
            col = tuple(int(v) for v in np.clip(base * rng.uniform(0.88, 1.1) + rng.normal(0, 3, 3), 0, 255))
            d.rectangle([x0 + 1, c * ch + 1, x0 + bw * 0.56, (c + 1) * ch - 1], fill=col)          # the brick
            d.rectangle([x0 + bw * 0.56 + 1, c * ch + 1, x0 + bw - 1, (c + 1) * ch - 1], fill=hole)  # the gap
    img = img.filter(ImageFilter.GaussianBlur(0.4))
    img.save(os.path.join(TEX, "brick_screen_albedo.png"))


def make_railing_tan(W=512, H=256):
    k = 4
    steel, shade = np.array(rgb("#A89878"), np.float32), np.array(rgb("#7F7359"), np.float32)
    img = Image.new("RGBA", (W * k, H * k), tuple(int(v) for v in steel) + (0,))
    d = ImageDraw.Draw(img)
    px_m, py_m = W * k / 2.0, H * k / 1.1

    def bar(x0, x1, y0, y1, col):                           # metres, y up from the foot
        d.rectangle([x0 * px_m, H * k - y1 * py_m, x1 * px_m - 1, H * k - y0 * py_m - 1],
                    fill=tuple(int(v) for v in col) + (255,))
    for i in range(1, 18):                                  # 17 flat bars, 11.1 cm apart
        x = i * 2.0 / 18
        bar(x - 0.014, x + 0.014, 0.10, 1.05, steel * 0.9 + shade * 0.1 if i % 2 else shade * 0.5 + steel * 0.5)
    bar(0.0, 2.0, 0.10, 0.14, shade)                        # bottom rail
    bar(0.0, 2.0, 1.05, 1.10, steel)                        # flat top rail
    bar(0.0, 0.035, 0.0, 1.10, shade)                       # half a flat post at each end
    bar(1.965, 2.0, 0.0, 1.10, shade)
    img.resize((W, H), Image.LANCZOS).save(os.path.join(TEX, "railing_tan_cutout_albedo.png"))


if __name__ == "__main__":
    os.makedirs(TEX, exist_ok=True)
    make_deck()
    make_brick_screen()
    make_railing_tan()
    print("west textures: deck_sawcut_albedo.png, brick_screen_albedo.png, railing_tan_cutout_albedo.png")
