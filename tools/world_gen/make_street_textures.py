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
V18h (builder Q, the Capricciosa forecourt and the coastal car park; Street View Dec 2022, satellite 2026):
  street_slab_albedo/_normal      car-park concrete, 10 m x 10 m = four cast panels of 5 m, each with its own tone: saw-cut
                                  joints with dirt, broom finish, aggregate, oil and tyre stains, dust (2048 px)
  street_slab_dark_albedo/_normal the dark tinted concrete of the square at the car park's south-east end, 10 m
  street_setts_dark_albedo        the grey setts darkened: gutters, borders, pad frames (+ the setts' normal map, resized)
  street_brick_albedo/_normal     red-brown concrete pavers 20 x 10 cm in herringbone, 2.4 m x 2.4 m, no lines (the bay
                                  lines are paint; the plan lays the bond at 45 degrees to the band)
  street_setts_albedo/_normal     now 1024 px (was 512), more depth between the stones
  street_kerbstone_albedo         flush kerb stones, 2 m x 0.25 m strip (two stones of 1 m), tiles along u
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



SLAB = "#A59B8C"            # V18h: the car park's concrete. Coast-walk survey (Street View 2019/2022): albedo #9a9083, darker
                            # than the promenade's; a little lighter here because the whole street palette is (calcada,
                            # asphalt and the older concrete were tuned brighter in Unity). Order kept: calcada > promenade
                            # concrete > car park > dark square > setts
SLAB_DARK = "#84786F"       # the dark tinted concrete of the square at the south-east end (survey #7b7068)


def _slab(name, colour, S_, seed, cracks, stain):
    """Cast concrete, 10 m x 10 m: four panels of 5 m with saw-cut joints (the survey: joints on every second bay
    line), each pour its own shade; broom finish, aggregate, oil drips and tyre scuffs, dust, a few hairline cracks."""
    from PIL import Image, ImageDraw, ImageFilter
    rng = np.random.default_rng(seed)
    px_m = S_ / 10.0
    k = S_ / 1024.0
    X, Y = grid(S_, S_)
    half = S_ / 2
    slab = (X >= half).astype(int) + 2 * (Y >= half).astype(int)
    tone = np.array([1.0, 0.972, 1.02, 0.987])[slab]
    warm = np.array([0.0, 0.5, -0.3, 0.7])[slab]
    clouds = 0.55 * noise((S_, S_), 1.3 * px_m, rng) + 0.45 * noise((S_, S_), 0.35 * px_m, rng)
    mott = noise((S_, S_), 0.08 * px_m, rng)
    broom = noise((S_, S_), 0.5, rng, sy=0.07 * px_m)                         # broom finish: fine lines across the panel
    grain = noise((S_, S_), 0.6, rng)
    v = 0.032 * clouds + 0.018 * mott + 0.016 * broom + 0.02 * grain
    alb = rgb(colour)[None, None, :] * (tone * (1.0 + v))[..., None]
    alb += (0.010 * warm + 0.008 * noise((S_, S_), 0.7 * px_m, rng))[..., None] * np.array([1.0, 0.25, -1.0])
    sp = noise((S_, S_), 0.9, rng)                                             # aggregate at the surface
    alb *= (1.0 + 0.08 * smoothstep(1.9, 2.6, sp) - 0.10 * smoothstep(1.9, 2.6, -sp))[..., None]
    oil = smoothstep(1.6, 2.5, noise((S_, S_), 0.14 * px_m, rng)) * smoothstep(-0.2, 1.0, noise((S_, S_), 0.7 * px_m, rng))
    alb *= (1.0 - stain * 0.20 * oil)[..., None]
    scuff = smoothstep(1.3, 2.4, noise((S_, S_), 0.05 * px_m, rng, sy=0.6 * px_m)) * smoothstep(0.3, 1.4, noise((S_, S_), 1.0 * px_m, rng))
    alb *= (1.0 - stain * 0.08 * scuff)[..., None]
    dust = smoothstep(0.7, 2.0, noise((S_, S_), 0.45 * px_m, rng))
    alb = lerp(alb, alb * 1.10 + 0.02, (0.25 * dust)[..., None])
    cimg = Image.new("L", (S_, S_), 0)
    cd = ImageDraw.Draw(cimg)
    for _k in range(cracks):                                                   # hairline cracks: wandering lines, wrapped
        x, y, a = rng.uniform(0, S_), rng.uniform(0, S_), rng.uniform(0, 6.283)
        pts = [(x, y)]
        for _j in range(int(rng.uniform(25, 55))):
            a += rng.normal(0.0, 0.2)
            x, y = x + 7.0 * k * np.cos(a), y + 7.0 * k * np.sin(a)
            pts.append((x, y))
        for ox in (-S_, 0, S_):
            for oy in (-S_, 0, S_):
                cd.line([(px + ox, py + oy) for px, py in pts], fill=255, width=1)
    crack = np.clip(np.asarray(cimg.filter(ImageFilter.GaussianBlur(0.6)), np.float64) / 255.0 * 1.6, 0.0, 1.0)
    alb *= (1.0 - 0.30 * crack)[..., None]
    # saw-cut joints on the panel edges: a dark slot 8 mm, dirt beside it, chipped arrises
    d = np.minimum(np.minimum(X % half, half - X % half), np.minimum(Y % half, half - Y % half))
    d = d + 0.8 * k * noise((S_, S_), 3.0 * k, rng) * smoothstep(1.0, 2.2, noise((S_, S_), 14.0 * k, rng))
    slot = smoothstep(0.012 * px_m + 0.9, 0.004 * px_m + 0.2, d)
    halo = 1.0 - smoothstep(1.0, 0.12 * px_m, d)
    alb *= (1.0 - 0.08 * halo)[..., None]
    alb = lerp(alb, rgb(colour)[None, None, :] * 0.42, (0.9 * slot)[..., None])
    save(name + "_albedo.png", T.match_mean(alb, colour, 0.9))
    height = 0.5 + 0.05 * grain + 0.05 * broom + 0.06 * mott - 0.9 * slot - 0.35 * crack - 0.10 * halo
    if S_ > 1024:                                                              # the normal map at 1024 px is enough
        height = height.reshape(S_ // 2, 2, S_ // 2, 2).mean(axis=(1, 3))
    save(name + "_normal.png", T.normal_map(height * 5.0, 1.0))


def make_slab(S_=2048, seed=2111):
    """The car park's and the forecourt's concrete. 2048 px for 10 m (205 px per metre): it is the one big surface
    of this area, 15,000 m2, and the race road itself for 130 m, seen from 2 m; at 1024 px a texel is 1 cm."""
    _slab("street_slab", SLAB, S_, seed, cracks=2, stain=1.0)


def make_slab_dark(S_=1024, seed=2112):
    """The dark tinted concrete of the square at the car park's south-east end (survey: not asphalt)."""
    _slab("street_slab_dark", SLAB_DARK, S_, seed, cracks=4, stain=0.6)


def make_setts_dark(S_=512, seed=2114):
    """Dark granite setts of the gutters, borders and pad frames (survey #5f5c56): the grey setts, darkened."""
    from PIL import Image
    import os
    src = os.path.join(T.TEX_DIR, "street_setts_albedo.png")
    if not os.path.exists(src):
        make_setts()
    a = np.asarray(Image.open(src).convert("RGB").resize((S_, S_), Image.LANCZOS), np.float64) / 255.0
    a = a * np.array([0.66, 0.655, 0.65])[None, None, :]
    save("street_setts_dark_albedo.png", a)
    nrm = os.path.join(T.TEX_DIR, "street_setts_normal.png")
    if os.path.exists(nrm):
        Image.open(nrm).convert("RGB").resize((S_, S_), Image.LANCZOS).save(os.path.join(T.TEX_DIR, "street_setts_dark_normal.png"))


def make_kerbstone(W=512, H=64, seed=2113):
    """Flush kerb stones seen from above: 2 m x 0.25 m, two stones of 1 m, a dark joint between them and a thin
    shadow line on both long edges (so the row reads as stone against concrete and against lawn)."""
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    stone = (X >= W / 2).astype(int)
    tone = np.array([1.0, 0.955])[stone]
    alb = rgb("#CFC9BC")[None, None, :] * (tone * (1.0 + 0.035 * noise((H, W), 9.0, rng) + 0.03 * noise((H, W), 0.8, rng)))[..., None]
    alb += 0.012 * noise((H, W), 20.0, rng)[..., None] * np.array([1.0, 0.3, -0.9])
    dj = np.minimum(np.minimum(X % (W / 2), W / 2 - X % (W / 2)), 1e9)
    alb = lerp(alb, rgb("#6A655C")[None, None, :], (0.8 * smoothstep(2.4, 0.8, dj))[..., None])
    de = np.minimum(Y, H - Y)
    alb *= (1.0 - 0.30 * smoothstep(4.0, 0.5, de))[..., None]
    alb *= (1.0 + 0.05 * smoothstep(5.0, 9.0, de) * smoothstep(16.0, 9.0, de))[..., None]      # worn arris catches light
    save("street_kerbstone_albedo.png", alb)


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


def make_brick(S_=1024, seed=2103):
    """Herringbone of 2:1 concrete pavers (20 x 10 cm), 2.4 m x 2.4 m (V18h: square tile, no bay lines; the plan turns
    the bond 45 degrees to the band, as it lies). In units of the paver width the cell (i, j) belongs to a lying
    paver when (i + j) % 4 is 0 or 1 and to a standing one when it is 2 or 3; 6 periods of 4 cells across the tile."""
    rng = np.random.default_rng(seed)
    n = 24
    px_m = S_ / 2.4
    X, Y = grid(S_, S_)
    unit = S_ / float(n)                                    # pixels per paver width (10 cm)
    gx, gy = X / unit, Y / unit
    i, j = np.floor(gx).astype(np.int64), np.floor(gy).astype(np.int64)
    c = (i + j) % 4
    ax = np.where(c == 1, i - 1, i)                        # anchor cell of the paver
    ay = np.where(c == 3, j - 1, j)
    lying = c < 2
    fx, fy = gx - ax, gy - ay                              # position inside the paver (0..2 x 0..1 or 0..1 x 0..2)
    d = np.where(lying, np.minimum(np.minimum(fx, 2 - fx), np.minimum(fy, 1 - fy)),
                 np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 2 - fy))) * unit
    pid = (ax % n) * 131 + (ay % n) * 17 + lying * 7
    rs = np.random.default_rng(seed + 1)
    size = 131 * n + 17 * n + 8
    tone = rs.uniform(-1.0, 1.0, size)[pid]
    grey = (rs.random(size) < 0.07)[pid]                    # the odd paver has lost its colour
    base = rgb("#9C7C68")
    brick = np.where(tone[..., None] >= 0, lerp(base, rgb("#B58D74"), tone[..., None] * 0.75),
                     lerp(base, rgb("#6B5145"), -tone[..., None] * 0.75))
    brick = np.where(grey[..., None], lerp(brick, rgb("#948C82")[None, None, :], 0.6), brick)
    fine = noise((S_, S_), 0.7, rng)
    brick = brick * (1.0 + 0.035 * noise((S_, S_), 0.035 * px_m, rng) + 0.03 * fine)[..., None]
    brick *= (1.0 - 0.10 * (1.0 - smoothstep(0.0, 6.0, d)))[..., None]             # chamfered edge
    worn = smoothstep(0.6, 1.8, noise((S_, S_), 0.5 * px_m, rng))                  # tyre-polished, paler patches
    brick = lerp(brick, brick * 1.05, (0.4 * worn)[..., None])
    joint = rgb("#5E5348")[None, None, :] * (1.0 + 0.06 * noise((S_, S_), 1.5, rng))[..., None]
    sand = smoothstep(0.4, 1.6, noise((S_, S_), 0.3 * px_m, rng))                  # sand swept into some joints
    joint = lerp(joint, rgb("#A89A84")[None, None, :], (0.35 * sand)[..., None])
    cov = smoothstep(1.2, 2.8, d)
    alb = lerp(joint, brick, cov[..., None])
    stain = smoothstep(1.5, 2.5, noise((S_, S_), 0.2 * px_m, rng))
    alb *= (1.0 - 0.10 * stain)[..., None]
    save("street_brick_albedo.png", alb)
    pillow = 1.0 - (1.0 - smoothstep(0.0, 6.0, np.maximum(d - 1.2, 0.0))) ** 2
    lift = rs.normal(0.0, 0.05, size)[pid]
    save("street_brick_normal.png", T.normal_map(lerp(0.1, 0.65 + 0.3 * pillow + lift + 0.03 * fine, cov) * 9.0, 1.0))


def make_setts(S_=1024, seed=2104):
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
    wob = 0.035 * noise((S_, S_), 5.0 * S_ / 512, rng)
    d = np.minimum(np.minimum(fx, 1 - fx) * (S_ / per_row), np.minimum(fy, 1 - fy) * rh) + wob * rh
    sid = r * per_row + k
    rs = np.random.default_rng(seed + 2)
    bright = rs.normal(1.0, 0.07, rows * per_row)[sid]
    warm = rs.uniform(-1, 1, rows * per_row)[sid][..., None]
    base = rgb("#A8A49B")                                  # pale grey granite (survey: #c9c5bb in the sun, #86837c in shade)
    stone = np.where(warm >= 0, lerp(base, rgb("#B4AC9C"), warm * 0.7), lerp(base, rgb("#989A9C"), -warm * 0.7))
    k_ = S_ / 512.0                                          # V18h: 1024 px, the same stones
    stone = stone * (bright * (1.0 + 0.04 * noise((S_, S_), 2.0 * k_, rng) + 0.03 * noise((S_, S_), 0.8, rng)))[..., None]
    cov = smoothstep(0.8 * k_, 2.1 * k_, d)
    pillow = 1.0 - (1.0 - smoothstep(0.0, 7.0 * k_, np.maximum(d - 1.2 * k_, 0.0))) ** 2
    stone *= (0.88 + 0.12 * pillow)[..., None]
    joint = rgb("#5F5B53")[None, None, :] * (1.0 + 0.06 * noise((S_, S_), 1.2, rng))[..., None]
    alb = lerp(joint, stone, cov[..., None])
    height = lerp(0.1, 0.6 + 0.4 * pillow + 0.05 * rs.normal(0, 1, rows * per_row)[sid], cov)
    save("street_setts_albedo.png", alb)
    save("street_setts_normal.png", T.normal_map(height * 14.0 * k_, 1.0))


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
            "street_slab": make_slab, "street_slab_dark": make_slab_dark, "street_kerbstone": make_kerbstone,
            "street_setts": make_setts, "street_rust": make_rust, "street_hedge": make_hedge,
            "street_slats": make_slats, "street_palette": make_palette, "street_setts_dark": make_setts_dark}


def main(argv):
    for name in argv or list(BUILDERS):
        t0 = time.time()
        BUILDERS[name]()
        print(f"{name:18s} {time.time() - t0:5.1f} s")


if __name__ == "__main__":
    main(sys.argv[1:])
    if not sys.argv[1:]:                     # V15a: the entrance plaza's textures (calcada cubes, drain grating)
        import make_entrance_textures
        make_entrance_textures.main()
