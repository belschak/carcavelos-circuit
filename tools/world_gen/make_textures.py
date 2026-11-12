#!/usr/bin/env python3
"""Procedural texture set for the Carcavelos Circuit campus world (Nova SBE, Carcavelos).

Every pixel is computed here from fixed random seeds: no photos, no downloads, no third-party
images. A re-run writes identical files.

    ~/Documents/nova/.media-tools/bin/python make_textures.py              # everything
    ~/Documents/nova/.media-tools/bin/python make_textures.py sand mural   # only some (+ sheet)

Output
    textures/*_albedo.png   sRGB colour
    textures/*_normal.png   tangent-space normal map, OpenGL convention (green = up)
    textures/*_rough.png    roughness, linear greyscale (Unity URP wants smoothness = 1 - this)
    out/textures_sheet.png  every albedo tiled 2x2 (proves the seams), the maps, the full mural

Why it tiles: everything is built on a torus. Noise is white noise filtered in the Fourier
domain (periodic by construction), blurs are FFT convolutions, Voronoi cells and scattered
shapes wrap their coordinates with modulo, and the Sobel filter for normal maps takes its
neighbours with np.roll. The right edge continues into the left edge (and the bottom into the
top) exactly. The window band, kerb and mural only need to tile horizontally.
"""
import os
import sys
import time
import zlib

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
TEX_DIR = os.path.join(HERE, "textures")
OUT_DIR = os.path.join(HERE, "out")
TAU = 2.0 * np.pi

# docs/art-style.md palette, env_common.PALETTE additions, and the mural's bright extras (sRGB)
HEX = {
    "kart_white": "#EAEAEC", "gloss_black": "#1D1D1F", "asphalt": "#686764", "sand": "#E3C48D",
    "atlantic_blue": "#2D8CD6", "boost_orange": "#FF8A1F",
    "calcada": "#E6E1D6", "concrete_sand": "#D9C9A8", "concrete_light": "#C9C5BC",
    "terracotta": "#C8664A", "lawn": "#8FB37A", "glass": "#2E3A46", "wet_sand": "#C9A877",
    "sea_deep": "#1D5F96", "leaf_dark": "#3F6B3A",
    "yellow": "#F4C430", "red": "#E2483D", "green": "#3FA34D", "pink": "#F28AB2", "purple": "#7A4FB0",
    # mural stand-ins for the two signal colours (art-style.md: keep Atlantic Blue and Boost Orange rare in the scenery)
    "sea_glass": "#4FA3A5", "teal_deep": "#2F7F86",
}

# Real-world size of one tile (width x height in metres), used for the sheet labels
TILE_M = {
    "calcada": (1, 1), "concrete_sand": (2, 2), "concrete_polished": (4, 4), "asphalt": (2, 2),
    "terracotta_steps": (1, 1), "sand": (4, 4), "grass": (2, 2), "facade_white": (3, 3),
    "window_band": (6, 1.5), "kerb_stripes": (2, 0.5), "mural_original": (12, 3),
}


# ----------------------------------------------------------------------------------------------
# Toolkit: colour, periodic noise, sampling, Voronoi, normal maps, saving
# ----------------------------------------------------------------------------------------------
def rgb(name_or_hex):
    """Palette name or '#RRGGBB' -> float sRGB triple in 0..1."""
    h = HEX.get(name_or_hex, name_or_hex)
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], np.float64) / 255.0


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def grid(h, w):
    """Pixel-centre coordinates X (right) and Y (down), shape (h, w)."""
    return np.meshgrid(np.arange(w) + 0.5, np.arange(h) + 0.5)


def blur(a, sx, sy=None):
    """Gaussian blur on the torus (FFT). sx/sy are sigmas in pixels; HxW or HxWxC."""
    sy = sx if sy is None else sy
    if a.ndim == 3:
        return np.stack([blur(a[..., c], sx, sy) for c in range(a.shape[2])], axis=-1)
    fy = np.fft.fftfreq(a.shape[0])[:, None]
    fx = np.fft.rfftfreq(a.shape[1])[None, :]
    g = np.exp(-2.0 * np.pi ** 2 * ((sx * fx) ** 2 + (sy * fy) ** 2))
    return np.fft.irfft2(np.fft.rfft2(a) * g, s=a.shape)


def noise(shape, sigma, rng, sy=None):
    """Periodic smooth noise: white noise blurred on the torus, zero mean, unit std.
    Blobs are about 2-3 sigma across; sy != sigma gives stretched noise."""
    n = blur(rng.standard_normal(shape), sigma, sy)
    n -= n.mean()
    return n / (n.std() + 1e-12)


def sample(img, x, y):
    """Bilinear sample of a periodic image at float pixel coordinates (wraps both ways)."""
    h, w = img.shape[:2]
    x, y = x - 0.5, y - 0.5
    x0, y0 = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = x - x0, y - y0
    x0 %= w
    y0 %= h
    x1, y1 = (x0 + 1) % w, (y0 + 1) % h
    return (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x1] * fx * (1 - fy)
            + img[y1, x0] * (1 - fx) * fy + img[y1, x1] * fx * fy)


def warped(n, rng, amount, sigma):
    """Domain-warp a periodic field by a periodic displacement (stays tileable)."""
    h, w = n.shape
    X, Y = grid(h, w)
    return sample(n, X + amount * noise((h, w), sigma, rng), Y + amount * noise((h, w), sigma, rng))


def wrap(d, size):
    """Shortest signed offset on a ring of the given size."""
    return (d + size / 2) % size - size / 2


def voronoi(S, n, rng, jitter, k_round):
    """Wrap-aware Voronoi mosaic on an n x n jittered grid over an S x S torus.
    Returns per pixel: cell id, offset from the cell's seed (lx, ly, wrap-safe) and the distance
    to the cell edge as a soft minimum over all neighbour edges (k_round px = rounded corners)."""
    cell = S / n
    gx = (np.arange(n)[None, :] + 0.5 + rng.uniform(-jitter, jitter, (n, n))) * cell
    gy = (np.arange(n)[:, None] + 0.5 + rng.uniform(-jitter, jitter, (n, n))) * cell
    X, Y = grid(S, S)
    ci = np.floor(X / cell).astype(np.int64)
    cj = np.floor(Y / cell).astype(np.int64)
    best = np.full((S, S), np.inf)
    aj = np.zeros((S, S), np.int64)
    ai = np.zeros((S, S), np.int64)
    for dj in (-1, 0, 1):                                    # pass 1: nearest seed
        for di in (-1, 0, 1):
            jj, ii = (cj + dj) % n, (ci + di) % n
            d2 = wrap(gx[jj, ii] - X, S) ** 2 + wrap(gy[jj, ii] - Y, S) ** 2
            upd = d2 < best
            best = np.where(upd, d2, best)
            aj = np.where(upd, jj, aj)
            ai = np.where(upd, ii, ai)
    ax, ay = wrap(gx[aj, ai] - X, S), wrap(gy[aj, ai] - Y, S)   # pixel -> own seed
    acc = np.zeros((S, S))
    for dj in range(-2, 3):                                  # pass 2: distance to cell edges
        for di in range(-2, 3):
            if dj == 0 and di == 0:
                continue
            jj, ii = (aj + dj) % n, (ai + di) % n
            bx, by = wrap(gx[jj, ii] - X, S), wrap(gy[jj, ii] - Y, S)
            ex, ey = bx - ax, by - ay
            dist = ((ax + bx) * ex + (ay + by) * ey) / (2.0 * np.sqrt(ex * ex + ey * ey))
            acc += np.exp(-np.maximum(dist, -40.0) / k_round)
    return aj * n + ai, -ax, -ay, -k_round * np.log(acc)


def normal_map(height, strength):
    """Tangent-space normal map from a height map: Sobel filter with wrap-around neighbours.
    OpenGL convention (Unity, Blender): red = +U (right), green = +V (up in the image)."""
    def at(dy, dx):                       # neighbour at (y + dy, x + dx), wrapping
        return np.roll(height, (-dy, -dx), axis=(0, 1))
    tl, t, tr = at(-1, -1), at(-1, 0), at(-1, 1)
    l, r = at(0, -1), at(0, 1)
    bl, b, br = at(1, -1), at(1, 0), at(1, 1)
    gx = ((tr + 2 * r + br) - (tl + 2 * l + bl)) / 8.0      # dh/dx, x to the right
    gy = ((bl + 2 * b + br) - (tl + 2 * t + tr)) / 8.0      # dh/dy, y down the image
    nx, ny = -gx * strength, gy * strength                  # +V is up (= -y), so n_y = +dh/dy
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + 1.0)
    return np.stack([nx * inv, ny * inv, inv], axis=-1) * 0.5 + 0.5


def match_mean(img, target, weight=1.0, hue=1.0):
    """Scale colours so the tile's average equals the flat palette colour: at a distance the
    mipmaps then show the same colour as the grey-box materials. weight < 1 = part of the way;
    hue < 1 matches the brightness fully but the tint only partly."""
    m = img.reshape(-1, 3).mean(axis=0)
    luma = np.array([0.2126, 0.7152, 0.0722])
    lum = (rgb(target) @ luma) / (m @ luma)
    scale = lum * ((rgb(target) / m) / lum) ** hue
    return img * (1.0 + weight * (scale - 1.0))


def splat(shape, cx, cy, half, sdf, weights):
    """Draw many small shapes on the torus at once; returns sum_k weights[k] * coverage_k.
    sdf(lx, ly) gives signed distances (px, < 0 inside) in each shape's local frame, evaluated
    on a (2 half + 1)^2 window around each centre; indices wrap, so shapes cross the edges.
    weights: shape (n,) -> HxW result, or (n, c) -> HxWxc (one coverage pass for all columns)."""
    h, w = shape
    o = np.arange(-half, half + 1)
    ix = np.floor(cx).astype(np.int64)[:, None, None] + o[None, None, :]
    iy = np.floor(cy).astype(np.int64)[:, None, None] + o[None, :, None]
    cov = np.clip(0.5 - sdf(ix + 0.5 - cx[:, None, None], iy + 0.5 - cy[:, None, None]), 0.0, 1.0)
    flat = (((iy % h) * w + (ix % w)) + np.zeros_like(ix)).ravel()
    wt = np.asarray(weights, np.float64)
    cols = wt.reshape(len(cx), -1)
    out = [np.bincount(flat, weights=(cov * cols[:, c, None, None]).ravel(), minlength=h * w).reshape(h, w)
           for c in range(cols.shape[1])]
    return out[0] if wt.ndim == 1 else np.stack(out, axis=-1)


def capsule_sdf(length, width, angle):
    """SDF factory for short strokes (capsules) centred on their position; angle 0 = upright."""
    ux, uy = np.sin(angle)[:, None, None], -np.cos(angle)[:, None, None]
    half_len = (length / 2.0)[:, None, None]
    rad = (width / 2.0)[:, None, None]

    def f(lx, ly):
        t = np.clip(lx * ux + ly * uy, -half_len, half_len)
        return np.hypot(lx - ux * t, ly - uy * t) - rad
    return f


def ellipse_sdf(rx, ry, angle):
    """Approximate SDF of rotated ellipses (good enough for anti-aliased edges)."""
    c, s = np.cos(angle)[:, None, None], np.sin(angle)[:, None, None]
    a, b = rx[:, None, None], ry[:, None, None]

    def f(lx, ly):
        u = lx * c + ly * s
        v = -lx * s + ly * c
        return (np.hypot(u / a, v / b) - 1.0) * np.minimum(a, b)
    return f


def jitter_points(size, cell, rng, jitter=0.9):
    """One random point per cell of a size x size torus (even, blue-noise-like spread)."""
    n = size // cell
    j0 = (1.0 - jitter) / 2.0
    cx = (np.arange(n)[None, :] + rng.uniform(j0, 1.0 - j0, (n, n))) * cell
    cy = (np.arange(n)[:, None] + rng.uniform(j0, 1.0 - j0, (n, n))) * cell
    return cx.ravel(), cy.ravel()


def specks(S, cell, rng, keep, rmin, rmax, vmin, vmax, dark_share):
    """Sparse tiny grains: returns a relative brightness offset map (lighter > 0, darker < 0)."""
    cx, cy = jitter_points(S, cell, rng)
    k = rng.random(len(cx)) < keep
    n = int(k.sum())
    r = rng.uniform(rmin, rmax, n)
    val = np.where(rng.random(n) < dark_share, -1.0, 1.0) * rng.uniform(vmin, vmax, n)
    return splat((S, S), cx[k], cy[k], int(np.ceil(rmax)) + 1,
                 ellipse_sdf(r, r * rng.uniform(0.6, 1.0, n), rng.uniform(0, np.pi, n)), val)


def save(name, img, dither=True):
    """Write an 8-bit PNG (float 0..1 in; HxW greyscale or HxWx3 RGB). A fixed-seed +-0.5 LSB
    dither keeps soft gradients free of banding."""
    a = np.clip(img, 0.0, 1.0) * 255.0
    if dither:
        a = a + np.random.default_rng(zlib.crc32(name.encode())).uniform(-0.5, 0.5, a.shape)
    a = np.clip(np.round(a), 0, 255).astype(np.uint8)
    Image.fromarray(a, "L" if a.ndim == 2 else "RGB").save(os.path.join(TEX_DIR, name), optimize=True)


# ----------------------------------------------------------------------------------------------
# 1. Calcada: white limestone cobbles, 1 m x 1 m (1 px = 1 mm)
# ----------------------------------------------------------------------------------------------
def make_calcada(S=1024, seed=11):
    """Irregular, roughly square stones (8-12 cm) from a jittered Voronoi mosaic, thin grey
    joints, slightly pillowed and tilted stones, a faint per-stone tint."""
    rng = np.random.default_rng(seed)
    n = 10
    sid, lx, ly, d_edge = voronoi(S, n, rng, 0.30, 3.2)
    count = n * n
    joint_half = rng.uniform(1.9, 2.8, count)
    bright = np.clip(rng.normal(1.0, 0.016, count), 0.965, 1.035)
    warm = rng.uniform(-1.0, 1.0, count)
    grey = rng.random(count) < 0.08
    lift = rng.normal(0.0, 0.04, count)
    tilt_a = rng.uniform(0, TAU, count)
    tilt_s = rng.uniform(0.0, 0.0010, count)

    # hand-cut edges: a low-frequency wobble, no gravelly high-frequency noise
    d_in = d_edge + 0.85 * noise((S, S), 7.0, rng) + 0.30 * noise((S, S), 2.5, rng)
    jh = joint_half[sid]
    cov = smoothstep(jh - 0.75, jh + 0.75, d_in)                 # 1 = stone, 0 = joint
    inner = np.maximum(d_in - jh, 0.0)

    # height: pillowed stones (rounded rim ~2 cm), small tilt and lift each, sandy joint bed
    pillow = 1.0 - (1.0 - smoothstep(0.0, 20.0, inner)) ** 2
    tilt = tilt_s[sid] * (np.cos(tilt_a[sid]) * lx + np.sin(tilt_a[sid]) * ly)
    surf = 0.010 * noise((S, S), 9.0, rng) + 0.004 * noise((S, S), 2.0, rng)
    height = lerp(0.10 + 0.025 * noise((S, S), 1.2, rng), 0.62 + 0.38 * pillow + lift[sid] + tilt + surf, cov)

    # albedo: white limestone, slight warm/cool tint per stone, faint edge shading, grey joints
    base = rgb("#ECE8DF")
    t = (warm[sid] * 0.40)[..., None]
    stone = np.where(t >= 0, lerp(base, rgb("#EFE6D3"), t), lerp(base, rgb("#E6E7E4"), -t))
    stone = np.where(grey[sid][..., None], lerp(stone, rgb("#D9D7D1"), 0.45), stone)
    mott = 1.0 + 0.010 * noise((S, S), 11.0, rng) + 0.005 * noise((S, S), 3.0, rng)
    rim = 1.0 - 0.055 * (1.0 - smoothstep(0.0, 8.0, inner))
    stone = stone * (bright[sid] * mott * rim)[..., None]
    joint = rgb("#97948D") * (1.0 + 0.04 * noise((S, S), 1.5, rng))[..., None]
    joint = joint * (0.88 + 0.12 * smoothstep(0.0, 2.5, d_in))[..., None]
    alb = match_mean(lerp(joint, stone, cov[..., None]), "calcada", 0.5)

    # shift by half a stone so the tile edge runs through stones, not along the seed grid
    shift = (S // (2 * n), S // (2 * n))
    save("calcada_albedo.png", np.roll(alb, shift, axis=(0, 1)))
    save("calcada_normal.png", normal_map(np.roll(height, shift, axis=(0, 1)) * 24.0, 1.0))


# ----------------------------------------------------------------------------------------------
# 2. Sand-coloured smooth concrete, 2 m x 2 m, expansion joints on the tile border
# ----------------------------------------------------------------------------------------------
def make_concrete_sand(S=1024, seed=22):
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    clouds = 0.6 * noise((S, S), 48, rng) + 0.5 * noise((S, S), 18, rng)
    v = 0.016 * clouds + 0.006 * noise((S, S), 5, rng) + 0.004 * noise((S, S), 1.0, rng)
    alb = rgb("concrete_sand") * (1.0 + v)[..., None]
    alb += 0.004 * noise((S, S), 70, rng)[..., None] * np.array([1.0, 0.3, -0.8])
    grains = specks(S, 8, rng, 0.30, 0.6, 1.5, 0.02, 0.05, 0.55)          # fine aggregate
    pores = specks(S, 32, rng, 0.35, 0.8, 1.8, 0.10, 0.18, 1.0)           # small air holes
    alb = alb * (1.0 + grains + pores)[..., None]

    # expansion joints: half of the line on each border, so tiling draws a 2 m grid
    d = np.minimum(np.minimum(X, S - X), np.minimum(Y, S - Y))
    line = smoothstep(2.3, 1.0, d)                        # ~4 px = 8 mm sawn joint
    halo = 1.0 - smoothstep(1.0, 9.0, d)
    alb = alb * (1.0 - 0.24 * line - 0.035 * halo)[..., None]
    alb = match_mean(alb, "concrete_sand", 0.8)

    rough = 0.80 + 0.03 * noise((S, S), 40, rng) - 0.25 * v + 0.015 * noise((S, S), 4, rng)
    rough = lerp(rough, 0.93, np.maximum(line, np.clip(-pores * 5, 0, 1)))
    save("concrete_sand_albedo.png", alb)
    save("concrete_sand_rough.png", rough)


# ----------------------------------------------------------------------------------------------
# 3. Polished grey concrete (indoors), 4 m x 4 m
# ----------------------------------------------------------------------------------------------
def make_concrete_polished(S=1024, seed=33):
    rng = np.random.default_rng(seed)
    clouds = warped(0.55 * noise((S, S), 50, rng) + 0.50 * noise((S, S), 22, rng), rng, 12, 40)
    clouds /= clouds.std()
    blot = smoothstep(0.6, 2.0, noise((S, S), 20, rng))            # occasional darker trowel marks
    v = 0.021 * clouds - 0.018 * blot + 0.007 * noise((S, S), 4.5, rng) + 0.003 * noise((S, S), 1.1, rng)
    alb = rgb("#BDBAB4") * (1.0 + v)[..., None]
    alb += 0.004 * noise((S, S), 90, rng)[..., None] * np.array([0.8, 0.2, -1.0])
    alb = alb * (1.0 + specks(S, 6, rng, 0.30, 0.55, 1.3, 0.03, 0.07, 0.6))[..., None]   # salt & pepper
    alb = match_mean(alb, "concrete_light", 0.85)

    # roughness: glossy, a little duller in the darker clouds, faint scuff marks
    n = 900
    scuff = splat((S, S), rng.uniform(0, S, n), rng.uniform(0, S, n), 14,
                  capsule_sdf(rng.uniform(6, 24, n), rng.uniform(0.8, 1.6, n), rng.uniform(0, np.pi, n)),
                  rng.uniform(0.02, 0.05, n))
    rough = 0.25 - 0.03 * clouds + 0.04 * blot + 0.015 * noise((S, S), 6, rng) + np.minimum(scuff, 0.08)
    save("concrete_polished_albedo.png", alb)
    save("concrete_polished_rough.png", np.clip(rough, 0.08, 0.6))


# ----------------------------------------------------------------------------------------------
# 4. Asphalt with aggregate, 2 m x 2 m (1 px = 2 mm)
# ----------------------------------------------------------------------------------------------
def make_asphalt(S=1024, seed=44):
    """Dark binder with densely packed angular stones (Voronoi cells shrunk by a gap): big
    chippings ~16 mm, smaller ones filling the gaps, a few pores."""
    rng = np.random.default_rng(seed)
    alb = rgb("#3E3F43") * (1.0 + 0.025 * noise((S, S), 60, rng) + 0.018 * noise((S, S), 14, rng)
                            + 0.015 * noise((S, S), 1.2, rng))[..., None]
    height = 0.12 * noise((S, S), 40, rng) + 0.04 * noise((S, S), 2.5, rng)
    rough = 0.93 + 0.02 * noise((S, S), 50, rng)
    tones = np.array([rgb(c) for c in ("#5C5E62", "#686A6E", "#535559", "#625E5A", "#78797B", "#4A4C50")])
    probs = np.array([0.30, 0.18, 0.22, 0.12, 0.06, 0.12])
    free = np.ones((S, S))                                   # where no bigger stone sits yet
    for n, prob, gap, mix, k in ((96, 0.62, 1.6, 0.75, 1.4), (192, 0.55, 0.9, 0.60, 0.8)):
        sid, _, _, d_edge = voronoi(S, n, rng, 0.42, k)
        present = rng.random(n * n) < prob
        d_in = d_edge + 0.5 * noise((S, S), 1.5, rng)
        inside = smoothstep(gap - 0.6, gap + 0.6, d_in) * present[sid] * free
        col = (tones[rng.choice(len(tones), n * n, p=probs)] * rng.uniform(0.92, 1.07, (n * n, 1)))[sid]
        dome = smoothstep(gap, gap + 3.0 * (96 / n), d_in)
        col = col * (0.95 + 0.05 * dome)[..., None]
        alb = lerp(alb, lerp(alb, col, mix), inside[..., None])
        height = height + inside * (0.45 + 0.55 * dome) * (96.0 / n) ** 0.5
        rough = lerp(rough, 0.80 + 0.04 * rng.standard_normal(n * n)[sid], inside)
        free = free * (1.0 - inside)
    pores = np.clip(-specks(S, 12, rng, 0.25, 0.7, 1.5, 1.0, 1.0, 1.0), 0, 1) * free
    alb = alb * (1.0 - 0.22 * pores)[..., None]
    height = height - 0.35 * pores
    rough = lerp(rough, 0.97, pores)
    alb = match_mean(alb, "asphalt", 1.0, hue=0.4)
    save("asphalt_albedo.png", alb)
    save("asphalt_rough.png", np.clip(rough, 0, 1))
    save("asphalt_normal.png", normal_map(height * 1.8, 1.0))


# ----------------------------------------------------------------------------------------------
# 5. Terracotta steps, 1 m x 1 m (512 px, 1 px ~ 2 mm)
# ----------------------------------------------------------------------------------------------
def make_terracotta(S=512, seed=55):
    rng = np.random.default_rng(seed)
    v = (0.014 * noise((S, S), 34, rng) + 0.010 * noise((S, S), 11, rng) + 0.009 * noise((S, S), 2.5, rng)
         + 0.007 * noise((S, S), 0.8, rng) + 0.004 * noise((S, S), 40, rng, 0.8))   # last: faint broom finish
    alb = rgb("terracotta") * (1.0 + v)[..., None]
    alb += 0.006 * noise((S, S), 50, rng)[..., None] * np.array([0.6, -0.4, -0.8])
    alb = alb * (1.0 + specks(S, 5, rng, 0.30, 0.5, 1.1, 0.04, 0.09, 0.5))[..., None]
    alb = match_mean(alb, "terracotta", 1.0)
    save("terracotta_steps_albedo.png", alb)


# ----------------------------------------------------------------------------------------------
# 6. Beach sand with wind ripples, 4 m x 4 m (1 px ~ 4 mm)
# ----------------------------------------------------------------------------------------------
def make_sand(S=1024, seed=66):
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    # crests: wave vector with whole cycles per tile (26, 9) -> ripple spacing ~14.5 cm,
    # bent by smooth periodic phase noise so the crests meander and change spacing
    phase = TAU * (26 * X + 9 * Y) / S + 4.5 * noise((S, S), 110, rng) + 1.4 * noise((S, S), 34, rng)
    # forks: pairs of opposite phase dislocations across the crests (a crest forks at one point,
    # and the extra crest ends at the other). The angle the pair subtends jumps by 2 pi only on
    # the segment between the two points, where the window is 1, so sin(phase) stays smooth.
    k_dir = np.arctan2(9, 26)                                    # direction of the wave vector
    for _ in range(9):
        cx, cy = rng.uniform(0, S, 2)
        a, sep = k_dir + rng.normal(0, 0.25), rng.uniform(1.2, 2.6) * S / np.hypot(26, 9)
        v1x, v1y = wrap(X - cx - np.cos(a) * sep / 2, S), wrap(Y - cy - np.sin(a) * sep / 2, S)
        v2x, v2y = wrap(X - cx + np.cos(a) * sep / 2, S), wrap(Y - cy + np.sin(a) * sep / 2, S)
        ang = np.arctan2(v1x * v2y - v1y * v2x, v1x * v2x + v1y * v2y)
        r = np.hypot(wrap(X - cx, S), wrap(Y - cy, S))
        phase += ang * (1.0 - smoothstep(sep * 0.7, sep * 0.7 + 140, r))
    prof = (np.sin(phase) + 0.32 * np.sin(2 * phase) + 0.10 * np.sin(3 * phase)) / 1.35   # steeper lee side
    amp = 0.30 + 0.70 * smoothstep(-1.3, 1.0, noise((S, S), 80, rng))
    ripples = prof * amp
    grain = noise((S, S), 0.9, rng)
    height = ripples + 1.0 * noise((S, S), 120, rng) + 0.3 * noise((S, S), 35, rng) + 0.05 * grain

    tone = 0.012 * noise((S, S), 70, rng) + 0.008 * noise((S, S), 18, rng)
    alb = rgb("sand") * (1.0 + tone + 0.016 * ripples + 0.008 * grain)[..., None]
    trough = smoothstep(0.1, -0.9, prof) * amp                  # darker heavy grains in the troughs
    alb = lerp(alb, alb * np.array([0.91, 0.89, 0.87]), (0.30 * trough)[..., None])
    alb = alb * (1.0 + specks(S, 16, rng, 0.14, 0.7, 1.5, 0.06, 0.10, 0.5))[..., None]   # shell bits
    alb = match_mean(alb, "sand", 1.0)
    save("sand_albedo.png", alb)
    save("sand_normal.png", normal_map(height * 1.3, 1.0))


# ----------------------------------------------------------------------------------------------
# 7. Stylised lawn, 2 m x 2 m (512 px, 1 px ~ 4 mm)
# ----------------------------------------------------------------------------------------------
def make_grass(S=512, seed=77):
    """Hand-painted lawn: six layers of soft, tapered brush strokes in four greens, leaning
    together in patches. Darker and lighter areas come from which greens a patch prefers, so
    the variation is made of strokes instead of an overlay of clouds."""
    rng = np.random.default_rng(seed)
    tones = np.array([rgb(c) for c in ("#779F63", "#86AD71", "#94B87A", "#A4C27F")])
    patch = noise((S, S), 15, rng)                              # darker / lighter areas
    flow = 0.35 * noise((S, S), 55, rng)                        # stroke direction drifts slowly
    alb = np.ones((S, S, 3)) * tones[1]
    for _ in range(6):
        cx, cy = jitter_points(S, 8, rng, 1.0)
        n = len(cx)
        ix, iy = cx.astype(int) % S, cy.astype(int) % S
        pick = np.clip(np.rint(rng.normal(1.5 + 0.45 * patch[iy, ix], 0.80)), 0, 3).astype(int)
        col = tones[pick] * rng.uniform(0.975, 1.025, (n, 1))
        ang = flow[iy, ix] + rng.normal(0, 0.36, n) - np.pi / 2          # long axis roughly up
        shape = ellipse_sdf(rng.uniform(6.5, 10.5, n), rng.uniform(1.5, 2.3, n), ang)
        both = splat((S, S), cx, cy, 12, shape, np.column_stack([np.ones(n), col]))
        cov = both[..., 0]
        a = np.clip(cov, 0.0, 1.0)[..., None]
        alb = alb * (1.0 - a) + both[..., 1:] / np.maximum(cov, 1e-6)[..., None] * a
    # a few thin light blades on top for close-up sparkle
    cx, cy = jitter_points(S, 9, rng, 1.0)
    keep = rng.random(len(cx)) < 0.55
    k = int(keep.sum())
    ix, iy = cx[keep].astype(int) % S, cy[keep].astype(int) % S
    lite = splat((S, S), cx[keep], cy[keep], 7,
                 capsule_sdf(rng.uniform(5, 9, k), rng.uniform(0.9, 1.3, k), flow[iy, ix] + rng.normal(0, 0.3, k)),
                 rng.uniform(0.04, 0.07, k))
    alb = alb * (1.0 + np.clip(lite, 0, 0.08))[..., None]
    alb = match_mean(alb, "lawn", 1.0)
    save("grass_albedo.png", alb)


# ----------------------------------------------------------------------------------------------
# 8. White render facade with very faint panel joints, 3 m x 3 m (512 px)
# ----------------------------------------------------------------------------------------------
def make_facade(S=1024, seed=88):
    """V16h: white render with real detail, 3 m x 3 m at 1024 px (3 mm per pixel): fine sand grain and float marks,
    3.0 x 1.5 m panel joints, very subtle rain streaks that start under each horizontal joint and fade downwards,
    and a matching normal map (grain, joints). Also the neutral texture that env_materials.TINTED multiplies by the
    colour of the other rendered walls (cream, ochre, pink, umber)."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    k = S / 512.0
    # V18l (Luc, 12 Nov: the walls read like a blank canvas): about three times the detail of V16h, so the render
    # reads as a real wall in the hard summer sun: sand grain, trowel marks, cloudy patches of the render
    grain = 0.026 * noise((S, S), 0.8, rng) + 0.016 * noise((S, S), 2.2, rng)          # sand in the render
    marks = 0.018 * warped(noise((S, S), 5.0 * k, rng, 18.0 * k), rng, 14.0 * k, 40.0 * k)   # float marks
    v = 0.034 * noise((S, S), 40 * k, rng) + 0.016 * noise((S, S), 6 * k, rng) + grain + marks
    alb = rgb("kart_white") * (1.0 + v)[..., None]
    alb += 0.004 * noise((S, S), 70 * k, rng)[..., None] * np.array([1.0, 0.3, -0.8])
    # joints: vertical every 3 m (the tile edge), horizontal every 1.5 m -> 3.0 x 1.5 m panels
    d = np.minimum(np.minimum(X, S - X), np.minimum(np.abs(Y - S / 2), np.minimum(Y, S - Y)))
    line = smoothstep(1.6 * k, 0.4 * k, d)
    alb = alb * (1.0 - 0.17 * line - 0.035 * (1.0 - smoothstep(1.0 * k, 6.0 * k, d)))[..., None]
    # rain streaks: thin runs under each horizontal joint (image y grows downwards), strongest in the first 30 cm,
    # gone after about 1.1 m; a few of them, not a curtain
    below = np.mod(Y, S / 2) / (S / 2)
    runs = np.clip(noise((S, S), 2.2 * k, rng, 110.0 * k) - 0.55, 0.0, None) * 0.9
    streak = np.clip(runs, 0.0, 1.0) * (1.0 - smoothstep(0.0, 0.75, below)) * (0.6 + 0.4 * smoothstep(0.0, 0.03, below))
    grime = (1.0 - smoothstep(0.0, 0.06, below)) * (0.5 + 0.5 * noise((S, S), 3.0 * k, rng, 25.0 * k))
    alb = alb * (1.0 - np.clip(0.10 * streak + 0.035 * np.clip(grime, 0, 1), 0.0, 0.16)[..., None] * np.array([1.0, 0.96, 0.88]))
    alb = match_mean(alb, "kart_white", 0.7)
    save("facade_white_albedo.png", alb)
    height = grain * 7.0 + marks * 9.0 - 1.4 * line
    save("facade_white_normal.png", normal_map(height, 2.6), dither=False)
    _tint_gain("facade_white_albedo.png", alb)


def _tint_gain(name, alb):
    """textures/tint_gain.json: 1 / the texture's mean (linear light), so a colour multiplied by the texture
    (env_materials.TINTED, export_unity_bundle.py) keeps its brightness."""
    import json
    path = os.path.join(TEX_DIR, "tint_gain.json")
    gains = json.load(open(path)) if os.path.exists(path) else {}
    gains[name] = round(float(1.0 / (np.clip(alb, 0.0, 1.0) ** 2.2).mean()), 4)
    json.dump(gains, open(path, "w"), indent=1, sort_keys=True)


def make_concrete_panel(S=1024, seed=2323):
    """V16h: fair-faced concrete, 2.4 m x 2.4 m at 1024 px: formwork panels 2.4 x 1.2 m (joint on the tile edge and
    at mid height), form-tie holes (two rows per panel, 60 cm apart) with a faint run under each, pour clouds, pores.
    Near-neutral light grey: env_materials.TINTED multiplies it by the material's own colour. With a normal map."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    m = S / 2.4                                              # pixels per metre
    clouds = 0.030 * noise((S, S), 60, rng) + 0.016 * noise((S, S), 16, rng) + 0.010 * noise((S, S), 150, rng, 7)   # pour bands
    fine = 0.008 * noise((S, S), 1.0, rng)
    alb = rgb("#CFCDC8") * (1.0 + clouds + fine)[..., None]
    alb += 0.004 * noise((S, S), 70, rng)[..., None] * np.array([1.0, 0.3, -0.8])
    pores = specks(S, 24, rng, 0.40, 0.8, 2.0, 0.08, 0.17, 1.0)
    alb = alb * (1.0 + pores)[..., None]
    d = np.minimum(np.minimum(X, S - X), np.minimum(np.abs(Y - S / 2), np.minimum(Y, S - Y)))
    line = smoothstep(2.4, 0.9, d)
    alb = alb * (1.0 - 0.22 * line - 0.04 * (1.0 - smoothstep(1.0, 10.0, d)))[..., None]
    hole = np.zeros((S, S))
    run = np.zeros((S, S))
    for panel in range(2):
        for row in (0.30, 0.90):
            cy = (panel * 1.2 + row) * m
            for cx in (0.3 * m, 0.9 * m, 1.5 * m, 2.1 * m):
                r = np.hypot(X - cx, Y - cy)
                hole = np.maximum(hole, smoothstep(0.016 * m, 0.010 * m, r))
                run = np.maximum(run, np.exp(-((X - cx) / (0.008 * m)) ** 2) * smoothstep(0.0, 0.02 * m, Y - cy)
                                 * (1.0 - smoothstep(0.0, 0.32 * m, Y - cy)))
    rim = np.zeros((S, S)) + hole
    alb = alb * (1.0 - 0.40 * hole - 0.030 * run)[..., None]
    save("concrete_panel_albedo.png", alb)
    save("concrete_panel_normal.png", normal_map(fine * 10.0 - 1.1 * line - 1.6 * rim + 5.0 * pores, 2.0), dither=False)
    _tint_gain("concrete_panel_albedo.png", alb)


def make_far_facade(S=512, seed=2424):
    """V16h: a generic house front for the buildings beyond the map edge (no lettering, nothing from a real building):
    12 m wide x 9 m high (4 bays, 3 storeys) of pale render with window openings, sills, a floor line per storey and a
    darker plinth share. Near white, so env_materials.TINTED can multiply it by white, cream and ochre. The corner at
    (0, 0) is plain wall: WorldLit.shader reads that texel for roofs (world-space UVs)."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    u, v = X / S * 12.0, (S - Y) / S * 9.0                   # metres across, metres up
    alb = rgb("#EDEAE4") * (1.0 + 0.012 * noise((S, S), 30, rng) + 0.006 * noise((S, S), 1.5, rng))[..., None]
    bu, sv = np.mod(u, 3.0), np.mod(v, 3.0)                  # inside one bay, one storey
    win = (np.abs(bu - 1.5) < 0.62) & (sv > 1.0) & (sv < 2.45)
    edge = np.minimum(np.minimum(np.abs(bu - 1.5 + 0.62), np.abs(bu - 1.5 - 0.62)), np.minimum(np.abs(sv - 1.0), np.abs(sv - 2.45)))
    pane = np.floor(u / 3.0).astype(int) * 3 + np.floor(v / 3.0).astype(int)
    tone = np.array([0.0, 0.5, 0.25, 0.8, 0.1, 0.6, 0.35, 0.9, 0.2, 0.7, 0.45, 0.15])[pane % 12]
    glass = lerp(rgb("#4E6478"), rgb("#93AEC4"), (tone * (0.5 + 0.5 * (sv - 1.0) / 1.45))[..., None])
    alb = np.where(win[..., None], glass, alb)
    alb = np.where((win & (edge < 0.06))[..., None], rgb("#F4F2EE"), alb)                      # frame
    alb = np.where((win & (np.abs(bu - 1.5) < 0.03))[..., None], rgb("#F4F2EE"), alb)          # mullion
    sill = (np.abs(bu - 1.5) < 0.70) & (sv > 0.90) & (sv <= 1.0)
    alb = np.where(sill[..., None], rgb("#D9D5CC"), alb)
    shade = (np.abs(bu - 1.5) < 0.62) & (sv >= 2.45) & (sv < 2.52)                             # lintel shadow
    alb = alb * np.where(shade, 0.86, 1.0)[..., None]
    alb = alb * (1.0 - 0.05 * smoothstep(0.05, 0.0, np.minimum(sv, 3.0 - sv)))[..., None]      # floor line
    save("far_facade_albedo.png", alb)
    wall = np.where(win, np.nan, 1.0)[..., None] * alb
    _tint_gain("far_facade_albedo.png", wall[~np.isnan(wall[..., 0])])


# ----------------------------------------------------------------------------------------------
# 9. Ribbon window band, 6 m x 1.5 m (1024 x 256), tiles horizontally
# ----------------------------------------------------------------------------------------------
def make_window_band(W=1024, H=256, seed=99):
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    pane_w, frame = W / 4, 6.0                                  # 1.5 m panes, 3.5 cm frame
    pane = np.floor(X / pane_w).astype(int) % 4
    v = (Y - frame) / (H - 2 * frame)                           # 0 = top of the glass, 1 = sill
    # sky reflection: light at the top, fading into the dark blue-grey glass below mid height
    # design pass 2026-11-09: brighter glass (design reference: light sky reflections, warm rooms behind)
    refl = 0.72 * (1.0 - smoothstep(0.0, 0.75, v)) ** 1.1
    wisps = np.clip(noise((H, W), 34, rng, 6), 0, None) * (1.0 - smoothstep(0.05, 0.5, v))
    refl = refl + 0.07 * wisps
    refl = refl * np.array([1.0, 0.92, 1.06, 0.97])[pane]       # every pane reflects a bit differently
    alb = lerp(rgb("#4D677D"), rgb("#B4CDE2"), np.clip(refl, 0, 1)[..., None])
    room = smoothstep(0.55, 0.85, v) * (1.0 - smoothstep(0.93, 1.0, v)) * np.array([0.9, 0.5, 1.0, 0.7])[pane]
    alb = lerp(alb, rgb("#C9AE86"), 0.35 * room[..., None])       # warm interiors glimpsed in the lower glass
    # crisp diagonal glints, two per pane at different offsets (classic stylised glass)
    lx = X - pane * pane_w
    glint = np.zeros((H, W))
    ca, sa = np.cos(0.95), np.sin(0.95)
    for p in range(4):
        for off, wid, a in ((rng.uniform(40, 120), rng.uniform(20, 30), 0.10),
                            (rng.uniform(150, 215), rng.uniform(6, 9), 0.09)):
            s = (lx - off) * sa + (Y - H / 2) * ca
            glint = np.where(pane == p, np.maximum(glint, a * np.clip(wid / 2 - np.abs(s) + 0.5, 0, 1)), glint)
    alb = lerp(alb, rgb("#C4D6E6"), glint[..., None] * (1.0 - 0.5 * smoothstep(0.3, 1.0, v))[..., None])
    # mullions every 1.5 m (the one at x = 0 is split over both edges) and the top/bottom frame
    dm = np.abs((X + pane_w / 2) % pane_w - pane_w / 2)
    mull = smoothstep(4.6, 3.6, dm)
    frame_m = np.maximum(smoothstep(frame + 0.5, frame - 0.5, Y), smoothstep(H - frame - 0.5, H - frame + 0.5, Y))
    metal = rgb("gloss_black")
    alb = lerp(alb, metal + (np.exp(-((dm - 2.4) / 0.9) ** 2) * 0.10)[..., None], mull[..., None])
    alb = lerp(alb, metal + 0.05 * smoothstep(frame - 2, frame, np.minimum(Y, H - Y))[..., None],
               frame_m[..., None])
    save("window_band_albedo.png", alb)
    # V16h: the frame and the mullions stand proud of the glass and are matt; the glass is a mirror for the sky
    metal_mask = np.maximum(mull, frame_m)
    save("window_band_normal.png", normal_map(blur(metal_mask, 0.8) * 4.0, 1.6), dither=False)
    save("window_band_rough.png", lerp(0.06, 0.55, metal_mask))


# ----------------------------------------------------------------------------------------------
# 10. Kerb stripes: 1 m Boost Orange / 1 m Kart White (256 x 64 = 2 m x 0.5 m), tiles horizontally
# ----------------------------------------------------------------------------------------------
def make_kerb(W=256, H=64, seed=1010):
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    alb = np.where((X < W / 2)[..., None], rgb("boost_orange"), rgb("kart_white"))
    alb = alb * (1.0 + 0.012 * noise((H, W), 10, rng) + 0.008 * noise((H, W), 1.2, rng))[..., None]
    dj = np.abs((X + W / 4) % (W / 2) - W / 4)                  # distance to a block joint
    alb = alb * (1.0 - 0.30 * smoothstep(1.2, 0.3, dj) - 0.04 * smoothstep(5, 1, dj))[..., None]
    alb = alb * (1.0 - 0.06 * smoothstep(3.0, 0.0, np.minimum(Y, H - Y)))[..., None]
    save("kerb_stripes_albedo.png", alb, dither=False)


# ----------------------------------------------------------------------------------------------
# 10b. Beachfront objects (V10b, 2026-11-10): timber deck, granite promenade kerb, straw parasol
# ----------------------------------------------------------------------------------------------
def make_timber_deck(S=512, seed=1616):
    """Weathered timber decking, 2 m x 2 m (512 px), tiles both ways: 14 boards of 14.3 cm running along x
    (u), dark gaps between them, staggered butt joints, a grey-brown tone per board and fine grain streaks.
    For the restaurant terraces on the sand (Capricciosa, A Pastorinha) and the pergola slats."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    boards = 14
    bh = S / boards
    row = np.floor(Y / bh).astype(int) % boards
    tone = rng.uniform(-0.10, 0.08, boards)
    hue = rng.uniform(-0.03, 0.03, boards)
    base = lerp(rgb("#8F7154"), rgb("#B39373"), 0.5)
    alb = base * (1.0 + tone[row])[..., None] + (hue[row])[..., None] * np.array([1.0, 0.2, -0.8])
    grain = noise((S, S), 22.0, rng, 0.9) * 0.05 + noise((S, S), 5.0, rng, 0.5) * 0.035
    alb = alb * (1.0 + grain)[..., None]
    alb = lerp(alb, rgb("#A8A39A"), (0.10 + 0.08 * noise((S, S), 60.0, rng).clip(0, None))[..., None])   # silvering
    gap = np.minimum(np.mod(Y, bh), bh - np.mod(Y, bh))
    alb = alb * (1.0 - 0.55 * smoothstep(2.2, 0.6, gap))[..., None]
    for b in range(boards):                                      # two butt joints per board, wrap-aware
        for x0 in rng.uniform(0, S, 2):
            d = np.abs(wrap(X - x0, S))
            m = (row == b) & (d < 1.4)
            alb[m] = alb[m] * 0.55
    save("timber_deck_albedo.png", match_mean(alb, "#A2825F", 0.8))


def make_granite_kerb(W=512, H=128, seed=1717):
    """Promenade kerb of dressed stone blocks, 4 m x 1 m (512 x 128), tiles along x: blocks of 0.8-1.4 m with dark
    joints, mostly speckled grey granite with the odd lighter limestone block (photos K03-K07). v 0-0.35 is the
    top of the kerb, v 0.35-1 the face that drops to the sand (env_beachfront maps it like that)."""
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    lengths = []
    while sum(lengths) < W - 1:
        lengths.append(rng.uniform(0.8, 1.4) * W / 4)
    lengths = np.array(lengths) * (W / sum(lengths))
    edges = np.concatenate([[0.0], np.cumsum(lengths)])
    block = np.clip(np.searchsorted(edges, X, side="right") - 1, 0, len(lengths) - 1)
    pal = [rgb("#8B8F92"), rgb("#7E8386"), rgb("#969794"), rgb("#B9B2A3")]
    pick = rng.choice(len(pal), len(lengths), p=[0.34, 0.3, 0.22, 0.14])
    cols = np.array([pal[k] * rng.uniform(0.95, 1.05) for k in pick])
    alb = cols[block]
    speck = noise((H, W), 0.7, rng) * 0.07 + noise((H, W), 2.0, rng) * 0.03 + noise((H, W), 14.0, rng) * 0.03
    alb = alb * (1.0 + speck)[..., None]
    joint = np.min(np.abs(X[..., None] - edges[None, None, :]), axis=-1)
    joint = np.minimum(joint, np.abs(W - X))
    alb = alb * (1.0 - 0.45 * smoothstep(2.0, 0.5, joint))[..., None]
    v = Y / H
    alb = alb * (1.0 + 0.10 * smoothstep(0.30, 0.35, v) * smoothstep(0.42, 0.36, v))[..., None]   # worn arris
    alb = alb * (1.0 - 0.10 * smoothstep(0.55, 1.0, v))[..., None]                                 # damp foot
    save("granite_kerb_albedo.png", alb)


def make_thatch(W=256, H=256, seed=1818):
    """Straw of a beach parasol (the dried-grass parasols on Carcavelos beach), tiles along x (u = around the
    parasol), v = 0 at the tip to 1 at the rim: thin straw strands along v, lighter on top, darker and ragged
    towards the rim."""
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    strands = noise((H, W), 0.6, rng, 18.0) * 0.10 + noise((H, W), 2.5, rng, 40.0) * 0.07
    alb = lerp(rgb("#C8A96B"), rgb("#9C7E48"), smoothstep(0.1, 1.0, Y / H)[..., None])
    alb = alb * (1.0 + strands)[..., None]
    bands = 0.05 * np.sin(Y / H * TAU * 3.0 + 0.4 * noise((H, W), 20.0, rng))     # tied layers
    alb = alb * (1.0 - bands)[..., None]
    save("thatch_albedo.png", match_mean(alb, "#B39461", 0.6))


# ----------------------------------------------------------------------------------------------
# 11. Original tunnel mural, 12 m x 3 m (2048 x 512), tiles horizontally
# ----------------------------------------------------------------------------------------------
def sd_circle(X, Y, cx, cy, r):
    return np.hypot(X - cx, Y - cy) - r


def sd_box(X, Y, cx, cy, hx, hy, rad=0.0, ang=0.0):
    c, s = np.cos(ang), np.sin(ang)
    px = (X - cx) * c + (Y - cy) * s
    py = -(X - cx) * s + (Y - cy) * c
    qx, qy = np.abs(px) - (hx - rad), np.abs(py) - (hy - rad)
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - rad


def sd_segment(X, Y, ax, ay, bx, by, w):
    px, py, ex, ey = X - ax, Y - ay, bx - ax, by - ay
    t = np.clip((px * ex + py * ey) / (ex * ex + ey * ey), 0, 1)
    return np.hypot(px - ex * t, py - ey * t) - w / 2


def sd_poly(X, Y, pts):
    """Exact signed distance to a polygon (Inigo Quilez's formulation), vectorised."""
    pts = np.asarray(pts, np.float64)
    d = (X - pts[0, 0]) ** 2 + (Y - pts[0, 1]) ** 2
    sgn = np.ones(np.broadcast(X, Y).shape)
    j = len(pts) - 1
    for i in range(len(pts)):
        ex, ey = pts[j] - pts[i]
        wx, wy = X - pts[i, 0], Y - pts[i, 1]
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        d = np.minimum(d, (wx - ex * t) ** 2 + (wy - ey * t) ** 2)
        c1, c2, c3 = Y >= pts[i, 1], Y < pts[j, 1], ex * wy > ey * wx
        sgn = np.where((c1 & c2 & c3) | (~c1 & ~c2 & ~c3), -sgn, sgn)
        j = i
    return sgn * np.sqrt(d)


def bbox(pts, pad=4.0):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return min(xs) - pad, max(xs) + pad, min(ys) - pad, max(ys) + pad


class Wall:
    """Canvas that wraps horizontally. Shapes are signed-distance functions in unwrapped pixel
    coordinates, evaluated only inside their bounding box; columns wrap with modulo."""

    def __init__(self, W, H, colour):
        self.W, self.H = W, H
        self.img = np.ones((H, W, 3)) * rgb(colour)

    def _win(self, box):
        x0, x1, y0, y1 = box
        xs = np.arange(int(np.floor(x0)) - 1, int(np.ceil(x1)) + 1)
        if len(xs) >= self.W:
            xs = np.arange(self.W)
        ys = np.arange(max(0, int(np.floor(y0)) - 1), min(self.H, int(np.ceil(y1)) + 1))
        return np.ix_(ys, xs % self.W), (xs + 0.5)[None, :], (ys + 0.5)[:, None]

    def paint(self, sdf, box, colour, alpha=1.0):
        idx, X, Y = self._win(box)
        if idx[0].size:
            a = np.clip(0.5 - sdf(X, Y), 0.0, 1.0) * alpha
            self.img[idx] = lerp(self.img[idx], rgb(colour), a[..., None])

    def shadow(self, sdf, box, dx=8.0, dy=8.0, factor=0.80):
        """Flat drop shadow: darken whatever is underneath inside the offset shape."""
        idx, X, Y = self._win((box[0] + dx, box[1] + dx, box[2] + dy, box[3] + dy))
        if idx[0].size:
            a = np.clip(0.5 - sdf(X - dx, Y - dy), 0.0, 1.0)
            self.img[idx] = self.img[idx] * (1.0 - (1.0 - factor) * a[..., None])

    def shadow_union(self, items, dx=8.0, dy=8.0, factor=0.80):
        """One drop shadow for a group of overlapping shapes (no double-dark overlaps)."""
        acc = np.zeros((self.H, self.W))
        for sdf, box in items:
            idx, X, Y = self._win((box[0] + dx, box[1] + dx, box[2] + dy, box[3] + dy))
            if idx[0].size:
                acc[idx] = np.maximum(acc[idx], np.clip(0.5 - sdf(X - dx, Y - dy), 0.0, 1.0))
        self.img *= 1.0 - (1.0 - factor) * acc[..., None]

    def shape(self, sdf, box, colour, shadow=True):
        if shadow:
            self.shadow(sdf, box)
        self.paint(sdf, box, colour)


def make_mural(W=2048, H=512, seed=1111):
    """Original design: sun, palm fronds, kart wheels with speed lines, zig-zags, dots, blobs,
    confetti and layered waves on four slanted colour fields. 1 m ~ 171 px."""
    rng = np.random.default_rng(seed)
    m = Wall(W, H, "sand")
    keep_out = []                                               # (x, y, r) where confetti may not go
    keep_rect = []                                              # (x0, x1, y0, y1), same purpose

    # --- 1. four background fields with slanted borders (the last one wraps to the first) -----
    borders = [(-20, 50), (545, -48), (1065, 55), (1575, -55)]  # (x at mid height, lean)
    fields = ["sand", "pink", "#A3D2F2", "purple"]
    for i, col in enumerate(fields):
        (xa, la), (xb, lb) = borders[i], borders[(i + 1) % 4]
        xb += W if i == 3 else 0
        poly = [(xa - la, -2), (xb - lb, -2), (xb + lb, H + 2), (xa + la, H + 2)]
        m.paint(lambda X, Y, p=poly: sd_poly(X, Y, p), bbox(poly), col)

    def field_at(x, y):
        for i in range(4):
            (xa, la), (xb, lb) = borders[i], borders[(i + 1) % 4]
            xb += W if i == 3 else 0
            t = (y - H / 2) / (H / 2)
            if xa + la * t <= x < xb + lb * t:
                return i
        return 0

    # --- 2. soft background blobs bridging the borders ----------------------------------------
    def blob(cx, cy, r, sb):
        rb = np.random.default_rng(sb)
        ks = np.arange(2, 6)
        amps, phs = rb.uniform(0.04, 0.11, len(ks)), rb.uniform(0, TAU, len(ks))

        def f(X, Y):
            th = np.arctan2(Y - cy, X - cx)
            rr = r * (1 + sum(a * np.cos(k * th + p) for a, k, p in zip(amps, ks, phs)))
            return (np.hypot(X - cx, Y - cy) - rr) * 0.9
        return f, (cx - r * 1.5, cx + r * 1.5, cy - r * 1.5, cy + r * 1.5)

    for cx, cy, r, col, sb in ((585, 105, 112, "yellow", 1), (1585, 112, 100, "#F7A6C6", 3),
                               (2010, 300, 96, "pink", 4)):
        f, box = blob(cx, cy, r, sb)
        m.paint(f, box, col)

    # --- 3. setting sun: disc with horizontal cut stripes, ring of alternating rays -------------
    sx, sy, sr = 300.0, 250.0, 128.0
    n_rays = 16
    for i in range(n_rays):
        a0, da = TAU * i / n_rays + 0.2, TAU / n_rays * 0.27
        r0, r1 = 148, (218 if i % 2 == 0 else 190)
        pts = [(sx + r0 * np.cos(a0 - da), sy + r0 * np.sin(a0 - da)), (sx + r1 * np.cos(a0), sy + r1 * np.sin(a0)),
               (sx + r0 * np.cos(a0 + da), sy + r0 * np.sin(a0 + da))]
        m.shape(lambda X, Y, p=pts: sd_poly(X, Y, p) - 4.0, bbox(pts, 8), "terracotta" if i % 2 else "red")

    def sun(X, Y):
        disc = sd_circle(X, Y, sx, sy, sr)
        gaps = [np.abs(Y - (sy + off)) - h / 2 for off, h in ((34, 9), (66, 13), (98, 17))]
        return np.maximum(disc, -np.minimum.reduce(gaps))
    sbox = (sx - sr - 2, sx + sr + 2, sy - sr - 2, sy + sr + 2)
    m.shape(sun, sbox, "yellow")
    m.paint(lambda X, Y: np.maximum(np.abs(sd_circle(X, Y, sx, sy, sr - 26)) - 6, Y - sy - 18), sbox, "kart_white")
    keep_out.append((sx, sy, 268))                            # ray tips + confetti size

    # --- 4. palm fronds hanging in from above (field 2) -------------------------------------------
    def frond(p0, p1, p2, length, colour):
        p0, p1, p2 = (np.asarray(p, np.float64) for p in (p0, p1, p2))

        def at(t):
            return (1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t ** 2 * p2

        sh = []
        for t in np.linspace(0.04, 0.97, 21):                   # leaflets, longest mid-frond
            pt = at(t)
            tg = 2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1)
            tg /= np.linalg.norm(tg)
            nv = np.array([-tg[1], tg[0]])
            L = length * (0.22 + 0.78 * np.sin(np.pi * min(t * 1.1, 1.0)) ** 0.6)
            for side in (-1, 1):
                d = 0.60 * tg + side * 0.80 * nv
                d /= np.linalg.norm(d)
                tip = pt + d * L + tg * L * 0.22                # leaflets sweep towards the frond tip
                ctrl = pt + d * L * 0.55
                left, right = [], []
                for u in np.linspace(0.0, 0.85, 6):             # tapered, gently curved leaflet
                    c = (1 - u) ** 2 * pt + 2 * (1 - u) * u * ctrl + u ** 2 * tip
                    dc = 2 * (1 - u) * (ctrl - pt) + 2 * u * (tip - ctrl)
                    nc = np.array([-dc[1], dc[0]]) / np.linalg.norm(dc)
                    w = 8.0 * (1 - u) ** 0.9 + 0.8
                    left.append(tuple(c + nc * w))
                    right.append(tuple(c - nc * w))
                sh.append(left + [tuple(tip)] + right[::-1])
        spine = [at(t) for t in np.linspace(0, 1, 24)]
        items = [(lambda X, Y, a=a, b=b: sd_segment(X, Y, a[0], a[1], b[0], b[1], 12), bbox([a, b], 10))
                 for a, b in zip(spine[:-1], spine[1:])]
        items += [(lambda X, Y, p=pts: sd_poly(X, Y, p), bbox(pts)) for pts in sh]
        m.shadow_union(items)
        for f, box in items:
            m.paint(f, box, colour)

    crown = (875, -60)
    frond(crown, (720, -20), (575, 175), 78, "leaf_dark")
    frond(crown, (1000, 70), (1065, 285), 74, "#57B85F")
    frond(crown, (840, 120), (745, 330), 86, "green")
    keep_out += [(640, 120, 110), (790, 200, 150), (980, 160, 120), (1040, 260, 70), (875, 30, 170)]

    # --- 5. kart wheels with speed lines (field 3) --------------------------------------------------
    def wheel(cx, cy, R, rim_col, hub_col, rot):
        box = (cx - R - 3, cx + R + 3, cy - R - 3, cy + R + 3)
        m.shape(lambda X, Y: sd_circle(X, Y, cx, cy, R), box, "gloss_black")
        m.paint(lambda X, Y: np.maximum(np.abs(sd_circle(X, Y, cx, cy, R * 0.83)) - R * 0.035,
                                        sd_box(X, Y, cx - R * 0.42, cy - R * 0.42, R * 0.32, R * 0.32)),
                box, "#45474D")                                 # flat highlight arc on the tyre
        m.paint(lambda X, Y: sd_circle(X, Y, cx, cy, R * 0.64), box, "#2A2B30")
        m.paint(lambda X, Y: np.abs(sd_circle(X, Y, cx, cy, R * 0.58)) - R * 0.06, box, rim_col)
        for i in range(5):                                      # five spokes
            a = TAU * i / 5 + rot
            ex, ey = cx + R * 0.55 * np.cos(a), cy + R * 0.55 * np.sin(a)
            m.paint(lambda X, Y, ex=ex, ey=ey: sd_segment(X, Y, cx, cy, ex, ey, R * 0.15), box, rim_col)
        m.paint(lambda X, Y: sd_circle(X, Y, cx, cy, R * 0.19), box, hub_col)
        m.paint(lambda X, Y: sd_circle(X, Y, cx, cy, R * 0.07), box, "gloss_black")
        keep_out.append((cx, cy, R + 40))

    for y0, x0, x1 in ((150, 1110, 1245), (198, 1080, 1220), (246, 1125, 1232)):
        seg = [(x0, y0), (x1, y0)]
        m.shape(lambda X, Y, s=seg: sd_segment(X, Y, s[0][0], s[0][1], s[1][0], s[1][1], 17), bbox(seg, 12),
                "kart_white")
    keep_rect.append((1040, 1270, 110, 290))                    # speed lines
    wheel(1338, 205, 126, "kart_white", "yellow", 0.3)
    wheel(1482, 322, 66, "yellow", "red", 1.0)

    # --- 6. zig-zags and dots (field 4) ------------------------------------------------------------
    for yz, col, amp, wid, x_from in ((122, "yellow", 32, 20, 1640), (206, "kart_white", 24, 11, 1660)):
        xs_ = np.arange(x_from, x_from + 9 * 42 + 1, 42)
        pts = [(x, yz + (amp if i % 2 else -amp)) for i, x in enumerate(xs_)]
        zz = lambda X, Y, p=pts, w=wid: np.minimum.reduce(
            [sd_segment(X, Y, a[0], a[1], b[0], b[1], w) for a, b in zip(p[:-1], p[1:])])
        m.shape(zz, bbox(pts, wid), col)
    for j in range(3):
        for i in range(8):
            x, y = 1662 + i * 44 + (22 if j % 2 else 0), 278 + j * 32
            m.paint(lambda X, Y, x=x, y=y: sd_circle(X, Y, x, y, 7), (x - 9, x + 9, y - 9, y + 9), "yellow")
    keep_rect += [(1600, 2060, 60, 262), (1630, 2030, 250, 372)]  # zig-zags, dot grid
    keep_out += [(1585, 112, 140), (2010, 300, 135), (585, 105, 150)]

    # --- 7. confetti in the empty spaces: crosses, triangles, rings, dots -----------------------------
    pal = {0: ["purple", "sea_glass", "green"], 1: ["yellow", "kart_white", "purple"],
           2: ["purple", "pink", "yellow"], 3: ["pink", "kart_white", "#A3D2F2"]}
    placed, tries = 0, 0
    while placed < 20 and tries < 4000:
        tries += 1
        x, y = rng.uniform(0, W), rng.uniform(38, 330)
        if any(np.hypot(wrap(x - kx, W), y - ky) < kr for kx, ky, kr in keep_out) or \
                any(x0 - 30 < x + W * k < x1 + 30 and y0 - 30 < y < y1 + 30
                    for x0, x1, y0, y1 in keep_rect for k in (-1, 0, 1)):
            continue
        keep_out.append((x, y, 95))
        col = pal[field_at(x, y)][placed % 3]
        kind, s, a = placed % 4, rng.uniform(14, 18), rng.uniform(0, TAU)
        box = (x - 2 * s, x + 2 * s, y - 2 * s, y + 2 * s)
        if kind == 0:
            f = lambda X, Y, x=x, y=y, s=s, a=a: np.minimum(sd_box(X, Y, x, y, s, s * 0.28, 2, a),
                                                            sd_box(X, Y, x, y, s * 0.28, s, 2, a))
        elif kind == 1:
            tri = [(x + 1.2 * s * np.cos(a + k * TAU / 3), y + 1.2 * s * np.sin(a + k * TAU / 3)) for k in range(3)]
            f = lambda X, Y, p=tri: sd_poly(X, Y, p) - 2.0
        elif kind == 2:
            f = lambda X, Y, x=x, y=y, s=s: np.abs(sd_circle(X, Y, x, y, s * 0.8)) - s * 0.22
        else:
            f = lambda X, Y, x=x, y=y, s=s: sd_circle(X, Y, x, y, s * 0.55)
        m.shape(f, box, col)
        placed += 1

    # --- 8. layered waves along the whole bottom (arches with whole repeats per tile) ---------------
    def arches(base, amp, k, x_off):
        return lambda X: base - amp * np.abs(np.sin(np.pi * k * (X - x_off) / W)) ** 0.85

    full = (0, W, 0, H)
    back, mid, front = arches(392, 30, 9, 0.0), arches(430, 25, 12, W / 24), arches(470, 18, 16, W / 40)
    m.paint(lambda X, Y: (back(X) - Y) * 0.9, full, "#6DB6E8")
    m.paint(lambda X, Y: np.abs(Y - back(X) - 9) * 0.9 - 3.0, full, "kart_white")
    m.paint(lambda X, Y: (mid(X) - Y) * 0.9, full, "teal_deep")
    u = lambda X: np.mod(12 * (X - W / 24) / W, 1.0)            # position inside each arch, 0..1
    m.paint(lambda X, Y: np.maximum(np.abs(Y - mid(X) - 13) * 0.9 - 3.0, (np.abs(u(X) - 0.5) - 0.27) * 120), full,
            "kart_white")
    m.paint(lambda X, Y: (front(X) - Y) * 0.9, full, "sea_deep")

    # --- 9. finish: barely-there unevenness, as if painted on concrete -----------------------------
    img = m.img * (1.0 + 0.010 * noise((H, W), 40, rng) + 0.004 * noise((H, W), 2.0, rng))[..., None]
    save("mural_original_albedo.png", img, dither=False)


# ----------------------------------------------------------------------------------------------
# Contact sheet + seam check
# ----------------------------------------------------------------------------------------------
ALBEDOS = ["calcada_albedo", "concrete_sand_albedo", "concrete_polished_albedo", "asphalt_albedo",
           "terracotta_steps_albedo", "sand_albedo", "grass_albedo", "facade_white_albedo",
           "window_band_albedo", "kerb_stripes_albedo"]
MAPS = ["calcada_normal", "concrete_sand_rough", "concrete_polished_rough", "asphalt_rough", "asphalt_normal",
        "sand_normal"]
# textures whose tile edge lies on a joint by design (the seam ratio then compares joint pixels)
JOINT_ON_EDGE = ("concrete_sand", "facade_white", "window_band", "kerb_stripes")


def _font(size):
    for path in ("/System/Library/Fonts/Helvetica.ttc", "/System/Library/Fonts/SFNS.ttf"):
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            pass
    return ImageFont.load_default(size=size)


def tile_label(stem):
    w, h = TILE_M[stem.rsplit("_", 1)[0]]
    return f"{stem}.png  ({w:g} x {h:g} m per tile)"


def seam_ratio(a, axis):
    """Mean jump across the wrap seam / mean jump between ordinary neighbours (~1 = seamless)."""
    a = a.astype(np.float64)
    if axis == 1:
        return np.abs(a[:, 0] - a[:, -1]).mean() / max(np.abs(np.diff(a, axis=1)).mean(), 1e-9)
    return np.abs(a[0] - a[-1]).mean() / max(np.abs(np.diff(a, axis=0)).mean(), 1e-9)


def make_sheet():
    cell, gap, label_h, cols = 480, 24, 34, 4
    width = cols * cell + (cols + 1) * gap
    f_lab, f_head = _font(19), _font(26)

    def tiled(stem):
        im = Image.open(os.path.join(TEX_DIR, stem + ".png")).convert("RGB")
        w, h = im.size
        big = Image.new("RGB", (w * 2, h * 2))
        for i in range(2):
            for j in range(2):
                big.paste(im, (i * w, j * h))
        return big.resize((cell, max(1, round(2 * h * cell / (2 * w)))), Image.LANCZOS)

    blocks = []
    for title, stems in (("Albedo, each tiled 2 x 2 (a seam would show as a line or a jump)", ALBEDOS),
                         ("Normal (OpenGL, green up) and roughness maps, tiled 2 x 2", MAPS)):
        blocks.append((title, [[(s, tiled(s)) for s in stems[r:r + cols]] for r in range(0, len(stems), cols)]))
    mural = Image.open(os.path.join(TEX_DIR, "mural_original_albedo.png")).convert("RGB")
    mw = width - 2 * gap
    mural_full = mural.resize((mw, round(mural.height * mw / mural.width)), Image.LANCZOS)
    half = mural.resize((mw // 2, round(mural.height * (mw // 2) / mural.width)), Image.LANCZOS)
    two = Image.new("RGB", (half.width * 2, half.height))
    two.paste(half, (0, 0))
    two.paste(half, (half.width, 0))

    height = gap + sum(40 + sum(max(im.height for _, im in row) + label_h + gap for row in rows)
                       for _, rows in blocks)
    height += 2 * (40 + label_h + gap) + mural_full.height + two.height
    sheet = Image.new("RGB", (width, height), (246, 246, 244))
    d = ImageDraw.Draw(sheet)
    y = gap
    for title, rows in blocks:
        d.text((gap, y), title, fill=(30, 30, 32), font=f_head)
        y += 40
        for row in rows:
            for i, (s, im) in enumerate(row):
                x = gap + i * (cell + gap)
                sheet.paste(im, (x, y))
                d.text((x, y + im.height + 6), tile_label(s), fill=(40, 40, 44), font=f_lab)
            y += max(im.height for _, im in row) + label_h + gap
    d.text((gap, y), "mural_original_albedo.png, full width (12 x 3 m, tiles horizontally)", fill=(30, 30, 32),
           font=f_head)
    y += 40
    sheet.paste(mural_full, (gap, y))
    y += mural_full.height + label_h + gap
    d.text((gap, y), "Mural wrap check: two copies side by side, half size", fill=(30, 30, 32), font=f_head)
    sheet.paste(two, (gap, y + 40))
    sheet.save(os.path.join(OUT_DIR, "textures_sheet.png"), optimize=True)


def seam_report():
    print("seam check: jump across the wrap / ordinary neighbour jump (about 1 = seamless)")
    for stem in ALBEDOS + MAPS + ["mural_original_albedo"]:
        a = np.asarray(Image.open(os.path.join(TEX_DIR, stem + ".png")))
        h_only = stem.startswith(("window_band", "kerb_stripes", "mural"))
        vert = "   -  " if h_only else f"{seam_ratio(a, 0):5.2f}"
        note = "  edge on a joint by design" if stem.startswith(JOINT_ON_EDGE) else ""
        print(f"  {stem:26s} {a.shape[1]:4d}x{a.shape[0]:<4d} horizontal {seam_ratio(a, 1):5.2f}  vertical {vert}{note}")


# Palm frond cutout (soft-look test 2026-11-10): the feathered outline of the palm fronds comes from this texture's
# alpha, not from geometry (env_vegetation.add_frond maps u across the blade, v from base to tip).
FROND_M = (1.6, 4.6)        # metres covered by the texture: across, along (env_vegetation.FROND_TEX_WIDTH)
FROND_PALETTES = {          # shade at the leaflet base, leaflet tip, rachis
    "palm_frond": ("#3A642C", "#86B349", "#A3B862"),
    "palm_frond_b": ("#426B30", "#92BA50", "#ADBE6A"),
}


def make_palm_frond(W=256, H=512, seed=1212):
    """textures/palm_frond_cutout_albedo.png (+ _b): RGBA, alpha = leaf / no leaf. A rachis down the middle and 34
    narrow leaflets per side, angled towards the tip and bending slightly back (droop), each a little lighter
    towards its tip; the whole frond is darker at the base. Drawn 4x larger and scaled down, so the alpha edge is
    smooth; the colour under the cut-away parts is leaf green, so no dark fringe shows in the distance."""
    from PIL import ImageDraw
    k = 4
    w, h = W * k, H * k
    sx, sy = w / FROND_M[0], h / FROND_M[1]                 # pixels per metre across / along

    def px(x, y):                                           # metres (x across from the rachis, y from the base)
        return (w / 2 + x * sx, h - y * sy)                 # v = 0 (base) is the bottom row

    for stem, (c_base, c_tip, c_rachis) in FROND_PALETTES.items():
        rng = np.random.default_rng(seed)
        base, tip, rachis = (np.array(rgb(c)) * 255 for c in (c_base, c_tip, c_rachis))
        img = Image.new("RGBA", (w, h), tuple(int(v) for v in lerp(base, tip, 0.4)) + (0,))
        draw = ImageDraw.Draw(img)
        n = 34
        for side in (-1, 1):
            for i in range(n):
                t = (i + 0.5 + rng.uniform(-0.15, 0.15)) / n
                y0 = 0.2 + 4.15 * t                                         # where the leaflet leaves the rachis
                ang = np.radians(62 - 34 * t + rng.uniform(-4, 4))          # angle to the rachis: wide at the base
                length = (0.52 + 0.34 * np.sin(np.pi * t ** 0.8) ** 0.7) * rng.uniform(0.92, 1.06) * (1.0 - 0.45 * t ** 4)
                width = 0.14 * rng.uniform(0.9, 1.1)
                shade = (0.1 + 0.5 * t) + rng.uniform(-0.08, 0.08)           # frond: dark at the base, light at the tip
                steps = 10
                centre = []
                for j in range(steps + 1):
                    s = j / steps
                    a = ang + 0.3 * s * s                                    # bends back a little: droop
                    centre.append((s, np.sin(a), np.cos(a)))
                pos, pts = np.array([0.0, y0]), []
                for j, (s, dx, dy) in enumerate(centre):
                    if j:
                        pos = pos + np.array([dx, dy]) * length / steps
                    pts.append((s, pos.copy(), np.array([dy, -dx])))
                for (s0, p0, n0), (s1, p1, n1) in zip(pts, pts[1:]):         # tapering quads, lighter towards the tip
                    h0 = width / 2 * (1.0 - s0 ** 1.6) * (0.55 + 0.45 * min(s0 / 0.15, 1.0))
                    h1 = width / 2 * (1.0 - s1 ** 1.6) * (0.55 + 0.45 * min(s1 / 0.15, 1.0))
                    col = lerp(base, tip, float(np.clip(shade + 0.35 * s0, 0.0, 1.0)))
                    quad = [p0 + n0 * h0, p1 + n1 * h1, p1 - n1 * h1, p0 - n0 * h0]
                    draw.polygon([px(side * q[0], q[1]) for q in quad], fill=tuple(int(v) for v in col) + (255,))
        for j in range(40):                                                  # rachis, tapering
            y0, y1 = 0.02 + 4.4 * j / 40, 0.02 + 4.4 * (j + 1) / 40
            hw = 0.045 * (1.0 - 0.8 * j / 40)
            draw.polygon([px(-hw, y0), px(hw, y0), px(hw, y1), px(-hw, y1)], fill=tuple(int(v) for v in rachis) + (255,))
        img.resize((W, H), Image.LANCZOS).save(os.path.join(TEX_DIR, stem + "_cutout_albedo.png"))


def make_sea_waves(S=512, seed=1313):
    """textures/sea_waves_normal.png: soft, round ripples for the moving sea (Sea.shader scrolls it twice, in two
    directions and at two sizes). Height = a sum of sine waves with whole wave numbers, so it tiles exactly, with
    the crests sharpened a little so the sun glints sit on narrow lines."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:S, 0:S] / S
    h = np.zeros((S, S))
    for _ in range(18):
        kx, ky = rng.integers(-7, 8), rng.integers(1, 8)
        amp = 1.0 / (1.0 + 0.35 * np.hypot(kx, ky))
        h += amp * np.sin(TAU * (kx * x + ky * y) + rng.uniform(0, TAU))
    h = (h - h.min()) / (h.max() - h.min())
    h = 1.0 - np.abs(2.0 * h - 1.0) ** 1.4                    # round troughs, narrower crests
    save("sea_waves_normal.png", normal_map(h * S / 64.0, 1.0), dither=False)


def _tiling_noise(S, sigma, rng):
    """Smooth noise that tiles: blurred random values with wrap-around, stretched to 0..1."""
    from scipy.ndimage import gaussian_filter
    n = gaussian_filter(rng.random((S, S)), sigma, mode="wrap")
    return (n - n.min()) / (n.max() - n.min())


def make_ground_detail(S=1024, seed=1414):
    """Detail for the ground shader (Ground.shader), two tiling RGBA data textures (linear numbers, not colour):
    ground_detail_data.png          R grass: small round tufts of short blades     G soft patches (read at a large scale)
                                    B sand: wind ripples                           A sand: fine grain
    ground_detail_normal_data.png   RG grass normal x, y                           BA sand normal x, y (0.5 = flat)
    All four values average 0.5, so the detail changes the look of the ground colour map, not its colour."""
    from scipy.ndimage import gaussian_filter
    rng = np.random.default_rng(seed)
    k = 2                                                  # drawn 2x larger, then scaled down: soft edges
    img = Image.new("L", (S * k, S * k), 70)               # dark gaps between the tufts
    draw = ImageDraw.Draw(img)
    clump = _tiling_noise(S, 22.0, rng)
    for _ in range(5200):                                  # a mown lawn from above: small round tufts of short blades
        x, y = rng.uniform(0, S), rng.uniform(0, S)        # that fan out from their middle, light at the tips;
        local = clump[int(y) % S, int(x) % S]              # the same in every direction, so nothing smears sideways
        radius = rng.choice((9.0, 13.0, 18.0), p=(0.45, 0.4, 0.15)) * (0.8 + 0.5 * local) * rng.uniform(0.85, 1.15)
        blades = int(rng.integers(7, 12))
        turn = rng.uniform(0, TAU)
        tone = 150 + rng.normal(0, 18) + (local - 0.5) * 40
        for ox in (-S, 0, S):                              # wrap around the edges, so the texture tiles
            for oy in (-S, 0, S):
                if not (-30 < x + ox < S + 30 and -30 < y + oy < S + 30):
                    continue
                cx, cy = (x + ox) * k, (y + oy) * k
                for j in range(blades):
                    a = turn + TAU * j / blades + (j * 2.399) % 0.5
                    r = radius * (0.7 + 0.3 * ((j * 0.618) % 1.0)) * k
                    mx, my = cx + np.cos(a) * r * 0.5, cy + np.sin(a) * r * 0.5
                    draw.line([(cx, cy), (mx, my)], fill=int(np.clip(tone - 30, 0, 255)), width=5)
                    draw.line([(mx, my), (cx + np.cos(a) * r, cy + np.sin(a) * r)], fill=int(np.clip(tone + 55, 0, 255)), width=4)
    grass = np.asarray(img.resize((S, S), Image.LANCZOS), np.float64) / 255.0
    grass = gaussian_filter(grass, 0.8, mode="wrap")
    grass = np.clip(0.5 + (grass - grass.mean()) * 1.1 + (clump - 0.5) * 0.15, 0.0, 1.0)

    patches = 0.6 * _tiling_noise(S, 60.0, rng) + 0.4 * _tiling_noise(S, 18.0, rng)
    patches = np.clip(0.5 + (patches - patches.mean()) * 1.6, 0.0, 1.0)

    yy, xx = np.mgrid[0:S, 0:S] / S                        # ripples: wavy parallel ridges, a little lopsided
    warp = (_tiling_noise(S, 40.0, rng) - 0.5) * 2.4 + (_tiling_noise(S, 12.0, rng) - 0.5) * 0.5
    ph = TAU * (26 * yy + 3 * xx + warp)
    ripples = 0.5 + 0.5 * np.sin(ph + 0.45 * np.sin(ph))
    ripples = np.clip(0.5 + (ripples - ripples.mean()) * (0.55 + 0.45 * _tiling_noise(S, 30.0, rng)), 0.0, 1.0)
    grain = gaussian_filter(rng.random((S, S)), 0.7, mode="wrap")
    grain = np.clip(0.5 + (grain - grain.mean()) / grain.std() * 0.16, 0.0, 1.0)

    def rgba(*channels):
        return Image.fromarray(np.clip(np.round(np.stack(channels, axis=-1) * 255.0), 0, 255).astype(np.uint8), "RGBA")

    rgba(grass, patches, ripples, grain).save(os.path.join(TEX_DIR, "ground_detail_data.png"))
    n_grass = normal_map(gaussian_filter(grass, 1.0, mode="wrap") * 22.0, 1.0)
    n_sand = normal_map(ripples * 16.0 + grain * 2.5, 1.0)
    rgba(n_grass[..., 0], n_grass[..., 1], n_sand[..., 0], n_sand[..., 1]).save(
        os.path.join(TEX_DIR, "ground_detail_normal_data.png"))


def make_grass_tuft(W=256, H=128, seed=1515):
    """textures/grass_tuft_cutout_albedo.png: two small tufts of grass side by side (RGBA, alpha = blade / no blade)
    for the 3D grass tufts (crossed cards built by CampusWorldBuilder, Foliage shader). Each tuft is a fan of
    curved, pointed blades from the bottom middle, dark green at the root and light, saturated green at the tip.
    Every tuft is shifted a little towards the colour of the lawn under it (vertex colour, make_grass_tufts.py).
    Drawn 4x larger and scaled down; the colour under the cut-away parts is grass green, so no dark fringe shows."""
    rng = np.random.default_rng(seed)
    k = 4
    root, tip = np.array(rgb("#4A7A46")) * 255, np.array(rgb("#B0D684")) * 255
    img = Image.new("RGBA", (W * k, H * k), tuple(int(v) for v in lerp(root, tip, 0.5)) + (0,))
    draw = ImageDraw.Draw(img)
    half = W * k / 2
    for t in range(2):
        cx = half * (t + 0.5)
        order = rng.permutation(11)
        for j in order:
            lean = (j - 5) / 5.0 + rng.uniform(-0.12, 0.12)                 # -1 .. 1: left to right across the fan
            height = H * k * (0.93 - 0.42 * abs(lean) ** 1.5) * rng.uniform(0.82, 1.0)
            width = H * k * rng.uniform(0.075, 0.105)
            x0 = cx + lean * half * 0.16
            shade = rng.uniform(-0.08, 0.08)
            pts = []
            for i in range(9):
                s = i / 8.0
                x = x0 + lean * half * 0.27 * s ** 1.6
                y = H * k - 1 - height * s
                pts.append((s, x, y, width / 2 * (1.0 - s ** 1.8)))
            for (s0, xa, ya, wa), (s1, xb, yb, wb) in zip(pts, pts[1:]):
                col = lerp(root, tip, float(np.clip(s0 * 1.05 + shade, 0.0, 1.0)))
                draw.polygon([(xa - wa, ya), (xb - wb, yb), (xb + wb, yb), (xa + wa, ya)], fill=tuple(int(v) for v in col) + (255,))
    img.resize((W, H), Image.LANCZOS).save(os.path.join(TEX_DIR, "grass_tuft_cutout_albedo.png"))


# ----------------------------------------------------------------------------------------------
# V10c. Solar panel array tile (3 m x 3 m, 512 px) and smooth sandy-ochre render (cutting walls, 2 x 2 m)
# ----------------------------------------------------------------------------------------------
def make_solar_panel(S=512, seed=1301):
    """Dark blue-black photovoltaic cells in 6 x 6 panels per tile, silver frame lines, faint sky gradient."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    cell = S / 6.0
    fx = np.abs((X % cell) - cell / 2) / (cell / 2)            # 0 centre .. 1 edge of one panel
    fy = np.abs((Y % cell) - cell / 2) / (cell / 2)
    edge = np.maximum(fx, fy)
    cells_x = np.abs(((X % cell) / cell * 6 % 1.0) - 0.5) * 2    # 6 cell columns per panel
    base = lerp(rgb("#1B2436"), rgb("#2F4260"), np.clip(0.5 + 0.12 * noise((S, S), 40, rng), 0, 1)[..., None])
    base = base * (1.0 - 0.10 * smoothstep(0.78, 1.0, cells_x))[..., None]      # busbar-like cell lines
    frame = smoothstep(0.93, 0.98, edge)
    out = lerp(base, rgb("#A9B0B8"), frame[..., None])
    save("solar_panel_albedo.png", out)


def make_render_ochre(S=512, seed=1302):
    """Smooth warm rendered concrete wall (S01/S02/S08): sandy ochre, soft stains, a faint panel joint."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    n = noise((S, S), 60, rng) * 0.025 + noise((S, S), 6, rng) * 0.008
    base = rgb("#DAC399")
    out = base[None, None, :] * (1.0 + n)[..., None]
    joint = np.exp(-((np.abs((X % (S / 2)) - 1)) / 1.2) ** 2) * 0.05
    out = out * (1.0 - joint)[..., None]
    save("render_ochre_albedo.png", out)


BUILDERS = {
    "calcada": make_calcada, "concrete_sand": make_concrete_sand, "concrete_polished": make_concrete_polished,
    "asphalt": make_asphalt, "terracotta": make_terracotta, "sand": make_sand, "grass": make_grass,
    "facade": make_facade, "window": make_window_band, "kerb": make_kerb, "mural": make_mural,
    "palm_frond": make_palm_frond, "sea_waves": make_sea_waves,
    "ground_detail": make_ground_detail, "grass_tuft": make_grass_tuft,
    "solar_panel": make_solar_panel, "render_ochre": make_render_ochre,
}
BUILDERS.update({"timber_deck": make_timber_deck, "granite_kerb": make_granite_kerb, "thatch": make_thatch})   # V10b
BUILDERS.update({"concrete_panel": make_concrete_panel, "far_facade": make_far_facade})                       # V16h


# ----------------------------------------------------------------------------------------------
# V10c2 / V16f. Bar railing as a cutout: one 2 m module of the campus railings as the photosphere survey V15 shows
# them (section 1, point 1): 1.1 m high, flat top rail, bottom rail, close VERTICAL flat bars 11 cm apart, a post at
# each end (so every 2 m), light galvanised / satin steel (#9a978c, reads silver-grey); a second file in bronze-tan
# (#a09075) for the CTT terrace. 512 x 256 RGBA, alpha = steel / air. One module per sheet of about 2 m
# (env_campus.Detail.railing, env_props.railing); a bar stands exactly at u = 0.5, so solid parts of a mesh (posts,
# rails) can share the material with every corner at UV (0.5, 0.5).
# ----------------------------------------------------------------------------------------------
RAILING_M = 2.0                                          # metres of railing on one texture module
RAILING_BAR = 2.0 / 18                                   # bar spacing: 11.1 cm
RAILING_TONES = {"railing_cutout_albedo.png": ("#9A978C", "#84827A", "#A8A59A"),        # bars, shade, top rail
                 "railing_tan_cutout_albedo.png": ("#A09075", "#8A7C64", "#AD9D82")}


def make_railing(W=512, H=256, seed=1616):
    k = 4
    for fname, tones in RAILING_TONES.items():
        steel, shade, top = (np.array(rgb(c)) * 255 for c in tones)
        img = Image.new("RGBA", (W * k, H * k), tuple(int(v) for v in steel) + (0,))
        draw = ImageDraw.Draw(img)
        px_m = W * k / RAILING_M                         # pixels per metre (module 2 m wide, 1.1 m tall)
        py_m = H * k / 1.1

        def bar(x0, x1, y0, y1, col):                    # metres, y up from the foot
            draw.rectangle([x0 * px_m, H * k - y1 * py_m, x1 * px_m - 1, H * k - y0 * py_m - 1],
                           fill=tuple(int(v) for v in col) + (255,))
        for i in range(1, 18):                           # 17 bars between the posts, bar 9 at u = 0.5
            x = i * RAILING_BAR
            bar(x - 0.011, x + 0.011, 0.10, 1.04, steel if i % 2 else lerp(steel, shade, 0.3))
        bar(0.0, RAILING_M, 0.07, 0.115, shade)          # bottom rail
        bar(0.0, RAILING_M, 1.04, 1.10, top)             # flat top rail
        bar(0.0, 0.03, 0.0, 1.10, steel)                 # posts (half a post at each end: neighbours complete it)
        bar(RAILING_M - 0.03, RAILING_M, 0.0, 1.10, steel)
        img.resize((W, H), Image.LANCZOS).save(os.path.join(TEX_DIR, fname))


BUILDERS["railing"] = make_railing


def main(argv):
    os.makedirs(TEX_DIR, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    t_all = time.time()
    for name in argv or list(BUILDERS):
        t0 = time.time()
        BUILDERS[name]()
        print(f"{name:18s} {time.time() - t0:5.1f} s")
    if all(os.path.exists(os.path.join(TEX_DIR, s + ".png")) for s in ALBEDOS + MAPS + ["mural_original_albedo"]):
        t0 = time.time()
        make_sheet()
        print(f"{'contact sheet':18s} {time.time() - t0:5.1f} s")
        seam_report()
    print(f"total {time.time() - t_all:.1f} s")


if __name__ == "__main__":
    main(sys.argv[1:])
