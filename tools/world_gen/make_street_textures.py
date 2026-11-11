"""Procedural textures of the street section (V10f, 2026-11-11), made with the toolkit of make_textures.py.

Run after make_textures.py:  python3 make_street_textures.py [name ...]
Output in textures/ (no photos; the colours are the survey's averages in sunlight, tuned in Unity under our light):
  street_concrete_albedo   pale concrete slabs, 4 m x 4 m, sawn joints on the tile border, hairline cracks, stains
  street_bays_albedo       the same concrete as one parking bay, 2.5 m x 5 m, a white line on both long edges
  street_brick_albedo      red-brown concrete pavers 21 x 10 cm in herringbone, one bay 2.5 m x 5 m with white lines
  street_setts_albedo/_normal   grey granite setts about 10 cm, in gently bowed rows, 2 m x 2 m
  street_rust_albedo       weathering steel, 2 m
  street_hedge_albedo      clipped hedge, 2 m
  street_slats_albedo      white horizontal timber boards 12 cm with 3 cm shadow gaps, 0.6 m
  street_palette_albedo    flat colours (street_plan.PALETTE, 8 x 8 cells)
"""
import sys
import time

import numpy as np

import make_textures as T
import street_plan as S

rgb, noise, blur, smoothstep, lerp, grid, save = T.rgb, T.noise, T.blur, T.smoothstep, T.lerp, T.grid, T.save

CONCRETE = "#CFC6B8"
LINE = "#ECE9E0"


def concrete_base(h, w, rng, px_m):
    """Pale concrete: clouds, fine aggregate, a few oil stains and hairline cracks. px_m = pixels per metre."""
    clouds = 0.6 * noise((h, w), 0.20 * px_m, rng) + 0.5 * noise((h, w), 0.07 * px_m, rng)
    v = 0.022 * clouds + 0.008 * noise((h, w), 0.02 * px_m, rng) + 0.006 * noise((h, w), 1.0, rng)
    alb = rgb(CONCRETE)[None, None, :] * (1.0 + v)[..., None]
    alb += 0.006 * noise((h, w), 0.3 * px_m, rng)[..., None] * np.array([1.0, 0.3, -0.8])
    stain = smoothstep(1.4, 2.6, noise((h, w), 0.13 * px_m, rng))                  # a few faint oil / tyre stains
    alb *= (1.0 - 0.06 * stain)[..., None]
    crack = np.exp(-(noise((h, w), 0.16 * px_m, rng) / 0.012) ** 2) * smoothstep(0.2, 1.2, noise((h, w), 0.5 * px_m, rng))
    alb *= (1.0 - 0.16 * blur(crack, 0.6))[..., None]
    return alb


def joints(d_px, alb, depth=0.17):
    """Dark sawn joint where d_px (distance to the joint line in pixels) is small, a soft dirt halo around it.
    Kept thin and soft: on the 8 m road two slabs lie side by side and the joint must not read as a lane line."""
    line = smoothstep(1.9, 0.7, d_px)
    halo = 1.0 - smoothstep(1.0, 8.0, d_px)
    return alb * (1.0 - depth * line - 0.025 * halo)[..., None]


def make_concrete(S_=1024, seed=2101):
    rng = np.random.default_rng(seed)
    X, Y = grid(S_, S_)
    alb = concrete_base(S_, S_, rng, S_ / 4.0)
    d = np.minimum(np.minimum(X, S_ - X), np.minimum(Y, S_ - Y))
    alb = joints(d, alb)
    alb = T.match_mean(alb, CONCRETE, 0.85)
    save("street_concrete_albedo.png", alb)


def bay_lines(alb, W, H, px_m):
    """White bay line: 6 cm on each long edge of the tile (12 cm when the tile repeats), slightly worn."""
    X, _Y = grid(H, W)
    rng = np.random.default_rng(7)
    d = np.minimum(X, W - X) / px_m                       # metres from the nearest long edge
    wear = 0.85 + 0.15 * smoothstep(-0.6, 0.8, noise((H, W), 0.05 * px_m, rng))
    cover = smoothstep(0.066, 0.054, d) * wear
    return lerp(alb, rgb(LINE)[None, None, :] * (1.0 + 0.02 * noise((H, W), 3, rng))[..., None], cover[..., None])


def make_bays(W=512, H=1024, seed=2102):
    rng = np.random.default_rng(seed)
    px_m = W / 2.5
    X, Y = grid(H, W)
    alb = concrete_base(H, W, rng, px_m)
    oil = np.exp(-(((X - W * 0.5) / (0.45 * px_m)) ** 2 + ((Y - H * 0.42) / (0.9 * px_m)) ** 2))   # drips under the engine
    alb *= (1.0 - 0.07 * oil * (0.6 + 0.4 * smoothstep(-1, 1, noise((H, W), 0.06 * px_m, rng))))[..., None]
    d = np.minimum(np.minimum(Y, H - Y), np.abs(Y - H / 2))          # slab joints across the bay every 2.5 m
    alb = joints(d, alb, 0.15)
    alb = T.match_mean(alb, CONCRETE, 0.85)
    save("street_bays_albedo.png", bay_lines(alb, W, H, px_m))


def make_brick(W=512, H=1024, seed=2103):
    """Herringbone of 2:1 pavers. In units of the paver width the cell (i, j) belongs to a lying paver when
    (i + j) % 4 is 0 or 1 and to a standing one when it is 2 or 3; 6 periods of 4 cells across the 2.5 m bay."""
    rng = np.random.default_rng(seed)
    px_m = W / 2.5
    X, Y = grid(H, W)
    unit = W / 24.0                                         # pixels per paver width (10.4 cm)
    gx, gy = X / unit, Y / unit
    i, j = np.floor(gx).astype(np.int64), np.floor(gy).astype(np.int64)
    c = (i + j) % 4
    ax = np.where(c == 1, i - 1, i)                        # anchor cell of the paver
    ay = np.where(c == 3, j - 1, j)
    lying = c < 2
    fx, fy = gx - ax, gy - ay                              # position inside the paver (0..2 x 0..1 or 0..1 x 0..2)
    d = np.where(lying, np.minimum(np.minimum(fx, 2 - fx), np.minimum(fy, 1 - fy)),
                 np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 2 - fy))) * unit
    pid = (ax % 24) * 131 + (ay % 48) * 17 + lying * 7
    tone = np.random.default_rng(seed + 1).uniform(-1.0, 1.0, 131 * 24 + 17 * 48 + 8)[pid]
    base = rgb("#9C7F6B")
    brick = np.where(tone[..., None] >= 0, lerp(base, rgb("#B28E76"), tone[..., None] * 0.55),
                     lerp(base, rgb("#6E564A"), -tone[..., None] * 0.6))
    brick = brick * (1.0 + 0.03 * noise((H, W), 0.04 * px_m, rng) + 0.02 * noise((H, W), 1.2, rng))[..., None]
    brick *= (1.0 - 0.07 * (1.0 - smoothstep(0.0, 5.0, d)))[..., None]             # chamfered edge
    joint = rgb("#5A5048")[None, None, :] * (1.0 + 0.05 * noise((H, W), 1.5, rng))[..., None]
    alb = lerp(joint, brick, smoothstep(0.8, 2.0, d)[..., None])
    alb *= (1.0 + 0.03 * noise((H, W), 0.25 * px_m, rng))[..., None]
    save("street_brick_albedo.png", bay_lines(alb, W, H, px_m))


def make_setts(S_=512, seed=2104):
    """Granite setts about 10 cm in rows that bow gently (one wave per metre), staggered, dark sandy joints."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S_, S_)
    rows, per_row = 20, 20
    rh = S_ / rows
    bow = 0.22 * rh * np.sin(2 * np.pi * X / (S_ / 2.0))
    ry = (Y + bow) / rh
    r = np.floor(ry).astype(np.int64) % rows
    shift = np.random.default_rng(seed + 1).uniform(0, 1, rows)[r]
    cx = X / (S_ / per_row) + shift
    k = np.floor(cx).astype(np.int64) % per_row
    fx, fy = cx - np.floor(cx), ry - np.floor(ry)
    wob = 0.06 * noise((S_, S_), 5.0, rng)
    d = np.minimum(np.minimum(fx, 1 - fx) * (S_ / per_row), np.minimum(fy, 1 - fy) * rh) + wob * rh
    sid = r * per_row + k
    rs = np.random.default_rng(seed + 2)
    bright = rs.normal(1.0, 0.07, rows * per_row)[sid]
    warm = rs.uniform(-1, 1, rows * per_row)[sid][..., None]
    base = rgb("#A8A49B")                                  # pale grey granite (survey: #c9c5bb in the sun, #86837c in shade)
    stone = np.where(warm >= 0, lerp(base, rgb("#B4AC9C"), warm * 0.7), lerp(base, rgb("#989A9C"), -warm * 0.7))
    stone = stone * (bright * (1.0 + 0.035 * noise((S_, S_), 2.0, rng) + 0.02 * noise((S_, S_), 0.8, rng)))[..., None]
    cov = smoothstep(1.2, 2.6, d)
    pillow = 1.0 - (1.0 - smoothstep(0.0, 7.0, np.maximum(d - 1.2, 0.0))) ** 2
    stone *= (0.93 + 0.07 * pillow)[..., None]
    joint = rgb("#6A665E")[None, None, :] * (1.0 + 0.06 * noise((S_, S_), 1.2, rng))[..., None]
    alb = lerp(joint, stone, cov[..., None])
    height = lerp(0.1, 0.6 + 0.4 * pillow + 0.05 * rs.normal(0, 1, rows * per_row)[sid], cov)
    save("street_setts_albedo.png", alb)
    save("street_setts_normal.png", T.normal_map(height * 14.0, 1.0))


def make_rust(S_=256, seed=2105):
    rng = np.random.default_rng(seed)
    n = 0.6 * noise((S_, S_), 22, rng) + 0.4 * noise((S_, S_), 6, rng)
    streak = noise((S_, S_), 2.0, rng, sy=40.0)                                    # run-off streaks down the plate
    t = np.clip(0.5 + 0.28 * n + 0.2 * streak, 0, 1)[..., None]
    alb = lerp(rgb("#6F4030"), rgb("#A2613F"), t)
    alb *= (1.0 + 0.05 * noise((S_, S_), 1.0, rng))[..., None]
    save("street_rust_albedo.png", alb)


def make_hedge(S_=256, seed=2106):
    rng = np.random.default_rng(seed)
    leaf = noise((S_, S_), 1.6, rng)
    clump = noise((S_, S_), 9.0, rng)
    t = np.clip(0.45 + 0.22 * leaf + 0.2 * clump, 0, 1)[..., None]
    alb = lerp(rgb("#2C3A2B"), rgb("#56683F"), t)
    alb = lerp(alb, rgb("#7B8A52")[None, None, :], (smoothstep(1.5, 2.6, leaf) * 0.5)[..., None])   # lit leaf tips
    save("street_hedge_albedo.png", alb)


def make_slats(S_=256, seed=2107):
    rng = np.random.default_rng(seed)
    _X, Y = grid(S_, S_)
    period = S_ / 4.0                                       # 4 boards per 0.6 m: 12 cm board + 3 cm gap
    f = (Y % period) / period
    gap = smoothstep(0.80, 0.84, f) * (1.0 - smoothstep(0.985, 1.0, f))
    board = np.floor(Y / period).astype(np.int64)
    tone = np.random.default_rng(seed + 1).normal(1.0, 0.025, 4)[board % 4]
    alb = rgb("#EAE3D2")[None, None, :] * (tone * (1.0 + 0.012 * noise((S_, S_), 30.0, rng, sy=1.5)))[..., None]
    alb *= (1.0 - 0.08 * smoothstep(0.55, 0.8, f) * (1 - gap))[..., None]            # the board's lower edge in shade
    alb = lerp(alb, rgb("#4A443C")[None, None, :], gap[..., None])
    save("street_slats_albedo.png", alb)


def make_palette(S_=256):
    n = 8
    cell = S_ // n
    img = np.zeros((S_, S_, 3))
    img[...] = rgb("#B0B0B0")
    for k, hexc in enumerate(S.PALETTE.values()):
        r, c = k // n, k % n
        img[r * cell:(r + 1) * cell, c * cell:(c + 1) * cell] = rgb(hexc)
    save("street_palette_albedo.png", img, dither=False)


BUILDERS = {"street_concrete": make_concrete, "street_bays": make_bays, "street_brick": make_brick,
            "street_setts": make_setts, "street_rust": make_rust, "street_hedge": make_hedge,
            "street_slats": make_slats, "street_palette": make_palette}


def main(argv):
    for name in argv or list(BUILDERS):
        t0 = time.time()
        BUILDERS[name]()
        print(f"{name:18s} {time.time() - t0:5.1f} s")


if __name__ == "__main__":
    main(sys.argv[1:])
