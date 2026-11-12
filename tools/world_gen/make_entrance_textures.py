"""Procedural textures of the entrance plaza (V15a, 2026-11-11), made with the toolkit of make_textures.py.

Run after make_textures.py:  python3 make_entrance_textures.py     (make_street_textures.py calls it at its end)
Output in textures/ (no photos; colours from the survey's sunlit averages, a step warmer for our light):
  entrance_calcada_albedo   pale limestone cubes in straight rows, 1024 px (V18h: rows of 9.4 cm, stones cut to 9-18 cm
  entrance_calcada_normal   on a 3 m tile, crisp edges, sand joints, a tone per stone, domed stones in the normal map;
                            V16: 9.4 x 14.3 cm, soft; V15a: 12.5 x 19 cm on 4 m, yellow-tan)
  entrance_planks_normal    V18h: sawn joints and brushed surface of the slab band
  entrance_edging_albedo    V18h: pale precast kerb stones (fan rim, parapet coping), 3 x 1.0 m on 3.0 x 0.45 m,
  entrance_edging_normal    1024 x 128 px
  entrance_drain_albedo     rust-brown slot grating: slots across the run, 0.5 m long x 0.22 m wide, 128 px
  entrance_planks_albedo    V16: the plank band along wing B: large pale concrete planks laid lengthwise, 16 rows of
                            0.375 m on a 6 m tile, random lengths 0.6-1.5 m, 1024 px
"""
import sys
import time

import numpy as np

import entrance_plan as P
import make_textures as T

rgb, noise, smoothstep, lerp, grid, save = T.rgb, T.noise, T.smoothstep, T.lerp, T.grid, T.save


def _rows_of_stones(S_, rows, lo, hi, rng, wave=0.0):
    """Stones in straight rows with random butt joints: per pixel the stone id, the distance (px) to the nearest
    joint, and the stone's own (u, v) in -1..1. Row height S_/rows; stone lengths lo..hi px; everything wraps."""
    X, Y = grid(S_, S_)
    rh = S_ / rows
    yy = (Y + wave * rh * np.sin(2 * np.pi * X / (S_ / 3.0))) % S_
    r = np.minimum((yy / rh).astype(np.int64), rows - 1)
    fy = yy / rh - r
    sid = np.zeros((S_, S_), np.int64)
    d_end = np.zeros((S_, S_))
    fu = np.zeros((S_, S_))
    base = 0
    for k in range(rows):
        cuts, x = [], rng.uniform(0.0, lo)
        while x < S_ - 0.6 * lo:
            cuts.append(x)
            x += rng.uniform(lo, hi)
        cuts = np.array(cuts)
        n = len(cuts)
        row = r == k
        xs = X[row]
        idx = (np.searchsorted(cuts, xs) - 1) % n                       # the stone that starts at cuts[idx]
        left = cuts[idx]
        right = np.where(idx + 1 < n, cuts[(idx + 1) % n], cuts[0] + S_)
        xs_u = np.where(xs < left, xs + S_, xs)                         # the last stone wraps round the tile edge
        length = right - left
        fu[row] = (xs_u - left) / length * 2.0 - 1.0
        d_end[row] = np.minimum(xs_u - left, right - xs_u)
        sid[row] = base + idx
        base += n
    d = np.minimum(np.minimum(fy, 1 - fy) * rh, d_end)
    return sid, d, fu, fy * 2.0 - 1.0, base


def make_calcada(S_=1024, seed=1401):
    """V18h: pale limestone cubes in straight rows (9.4 cm), cut to 9-18 cm, on a 3 m tile (2.9 mm per pixel): crisp
    hand-cut edges, sand-filled joints of 6-9 mm, a tone of its own per stone (cream, a few grey, a few buff), domed
    and slightly tilted stones in the normal map. No large stains: a 3 m tile would show them as a pattern."""
    rng = np.random.default_rng(seed)
    sid, d, fu, fv, count = _rows_of_stones(S_, 32, 0.09 / 3.0 * S_, 0.18 / 3.0 * S_, rng, wave=0.05)
    rs = np.random.default_rng(seed + 2)
    d_in = d + 0.55 * noise((S_, S_), 3.0, rng) + 0.25 * noise((S_, S_), 1.0, rng)          # hand-cut, not ruled
    jh = rs.uniform(1.0, 1.6, count)[sid]                                                   # half joint: 3-4.7 mm
    cov = smoothstep(jh - 0.55, jh + 0.55, d_in)                                            # 1 = stone
    inner = np.maximum(d_in - jh, 0.0)
    bright = np.clip(rs.normal(1.0, 0.040, count), 0.90, 1.08)[sid]
    warm = rs.uniform(-1, 1, count)[sid][..., None]
    kind = rs.random(count)[sid]
    base = rgb(P.CALCADA)
    stone = np.where(warm >= 0, lerp(base, rgb("#E2D6B4"), warm * 0.45), lerp(base, rgb("#D2D0C6"), -warm * 0.45))
    stone = np.where((kind < 0.07)[..., None], lerp(stone, rgb("#B9B5A8"), 0.55), stone)    # grey stones
    stone = np.where((kind > 0.96)[..., None], lerp(stone, rgb("#D9C59A"), 0.50), stone)    # buff stones
    grain = 1.0 + 0.030 * noise((S_, S_), 0.8, rng) + 0.022 * noise((S_, S_), 2.2, rng) + 0.012 * noise((S_, S_), 6.0, rng)
    rim = 1.0 - 0.10 * (1.0 - smoothstep(0.0, 3.5, inner))                                  # worn arris
    polish = 1.0 + 0.035 * smoothstep(4.0, 12.0, inner)                                     # the top is walked smooth
    stone = stone * (bright * grain * rim * polish)[..., None]
    joint = rgb("#9E9684")[None, None, :] * (1.0 + 0.10 * noise((S_, S_), 0.9, rng) + 0.05 * noise((S_, S_), 4.0, rng))[..., None]
    joint = joint * (0.80 + 0.20 * smoothstep(0.0, 1.6, d_in))[..., None]                   # darker in the middle
    alb = lerp(joint, stone, cov[..., None])
    alb = T.match_mean(alb, P.CALCADA, 1.0)
    save("entrance_calcada_albedo.png", alb)
    tilt_a = rs.uniform(0, 2 * np.pi, count)[sid]
    tilt_s = rs.uniform(0.0, 0.10, count)[sid]
    lift = rs.normal(0.0, 0.05, count)[sid]
    dome = 1.0 - (1.0 - smoothstep(0.0, 7.0, inner)) ** 2
    height = lerp(0.05 + 0.04 * noise((S_, S_), 1.0, rng),
                  0.55 + 0.40 * dome + lift + tilt_s * (np.cos(tilt_a) * fu + np.sin(tilt_a) * fv)
                  + 0.020 * noise((S_, S_), 1.2, rng) + 0.012 * noise((S_, S_), 4.0, rng), cov)
    save("entrance_calcada_normal.png", T.normal_map(height * 9.0, 1.0), dither=False)


def make_edging(W=1024, H=128, seed=1404):
    """V18h: the fan's rim stones and the coping of the ramp's parapets: pale precast concrete kerb stones, three of
    1.0 m on a tile of 3.0 x 0.45 m (u along the rim), fine exposed grain, chamfered long edges, 8 mm joints."""
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    n = 3
    fx = (X / W * n) % 1.0
    k = np.floor(X / W * n).astype(np.int64) % n
    d_joint = np.minimum(fx, 1 - fx) * (W / n)                       # px to the butt joint
    d_edge = np.minimum(Y, H - Y)                                     # px to the long edge
    tone = np.array([1.0, 0.965, 1.03])[k]
    base = rgb(P.EDGING)
    speck = noise((H, W), 0.7, rng)
    alb = base[None, None, :] * (tone * (1.0 + 0.035 * speck + 0.02 * noise((H, W), 3.0, rng)
                                         + 0.015 * noise((H, W), 14.0, rng, 5.0)))[..., None]
    alb = lerp(alb, rgb("#8F8A7E")[None, None, :], (0.5 * smoothstep(0.55, 1.4, speck))[..., None] * 0.35)   # dark aggregate
    alb *= (0.93 + 0.07 * smoothstep(0.0, 9.0, d_edge))[..., None]                                          # chamfer
    cov = smoothstep(0.8, 2.0, d_joint)
    alb = lerp(rgb("#9A9384")[None, None, :] * (1.0 + 0.06 * noise((H, W), 1.0, rng))[..., None], alb, cov[..., None])
    alb = T.match_mean(alb, P.EDGING, 1.0)
    save("entrance_edging_albedo.png", alb)
    height = cov * (0.5 + 0.5 * smoothstep(0.0, 9.0, d_edge)) + 0.03 * speck
    save("entrance_edging_normal.png", T.normal_map(height * 6.0, 1.0), dither=False)


def make_drain(W=128, H=128, seed=1402):
    """u (x) runs along the channel (0.5 m), v (y) across it (0.22 m). Tiles in u; mirrored in v."""
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    v = np.abs(Y / H - 0.5) * 2.0                          # 0 in the middle, 1 at the rim
    slots = 10
    f = (X / W * slots) % 1.0
    slot = smoothstep(0.30, 0.38, f) * (1.0 - smoothstep(0.62, 0.70, f)) * (1.0 - smoothstep(0.62, 0.70, v))
    base = rgb(P.DRAIN)[None, None, :] * (1.0 + 0.05 * noise((H, W), 6.0, rng) + 0.03 * noise((H, W), 1.0, rng))[..., None]
    rim = smoothstep(0.80, 0.9, v)
    alb = lerp(base, rgb("#4A2E22")[None, None, :], (0.5 * rim)[..., None])       # the frame
    alb = lerp(alb, rgb("#2A1A14")[None, None, :], slot[..., None])
    save("entrance_drain_albedo.png", alb)


def make_planks(S_=1024, seed=1403):
    """u (x) runs along the slabs. V18 (team walk 7 Nov): 10 rows of 0.6 m, in every row the 6 m are cut into grey
    rectangular slabs of 0.9-1.8 m (V16 had 16 rows of 0.375 m planks of 0.6-1.5 m)."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S_, S_)
    rows = 10
    rh = S_ / rows
    r = np.minimum((Y / rh).astype(np.int64), rows - 1)
    fy = Y / rh - r
    px_m = S_ / P.PLANK_TILE
    tone = np.ones((S_, S_))
    warm = np.zeros((S_, S_))
    d_end = np.full((S_, S_), 1e9)
    for k in range(rows):
        cuts, x = [], rng.uniform(0.0, 0.6) * px_m
        while x < S_ - 0.5 * px_m:
            cuts.append(x)
            x += rng.uniform(0.9, 1.8) * px_m
        cuts = np.array(cuts)
        row = r == k
        xs = X[row]
        idx = np.searchsorted(cuts, xs) % len(cuts)                     # plank index (the last one wraps to the first)
        tone[row] = rng.normal(1.0, 0.035, len(cuts))[idx]
        warm[row] = rng.uniform(-1, 1, len(cuts))[idx]
        dist = np.abs(xs[:, None] - cuts[None, :])
        dist = np.minimum(dist, S_ - dist).min(axis=1)                  # to the nearest butt joint, wrapping
        d_end[row] = dist
    d = np.minimum(np.minimum(fy, 1 - fy) * rh, d_end)
    base = rgb(P.PLANK)
    plank = np.where(warm[..., None] >= 0, lerp(base, rgb("#CFCAC0"), warm[..., None] * 0.5),
                     lerp(base, rgb("#D6D4CF"), -warm[..., None] * 0.5))
    plank = plank * (tone * (1.0 + 0.025 * noise((S_, S_), 3.0, rng) + 0.012 * noise((S_, S_), 0.7, rng)))[..., None]
    streak = noise((S_, S_), 40.0, rng, 1.2)                             # faint brushing along the plank
    plank = plank * (1.0 + 0.012 * streak)[..., None]
    cov = smoothstep(0.5, 1.5, d)                                        # V18h: crisper sawn joints (was 0.6 .. 2.2)
    joint = rgb("#958E80")[None, None, :] * (1.0 + 0.06 * noise((S_, S_), 1.2, rng))[..., None]
    alb = lerp(joint, plank, cov[..., None])
    alb *= (1.0 + 0.015 * noise((S_, S_), 0.12 * S_, rng))[..., None]
    alb = T.match_mean(alb, P.PLANK, 1.0)
    save("entrance_planks_albedo.png", alb)
    # V18h: normal map: sawn joints 4 mm deep, each slab set a hair differently, brushed surface
    lift = np.zeros((S_, S_))
    rl = np.random.default_rng(seed + 5)
    lift = (tone - 1.0) * 1.2 + 0.03 * rl.standard_normal(1)[0]
    height = cov * (0.6 + lift) + 0.020 * streak + 0.012 * noise((S_, S_), 1.0, rng)
    save("entrance_planks_normal.png", T.normal_map(height * 7.0, 1.0), dither=False)


BUILDERS = {"entrance_calcada": make_calcada, "entrance_drain": make_drain, "entrance_planks": make_planks,
            "entrance_edging": make_edging}


def main(argv=()):
    for name in argv or list(BUILDERS):
        t0 = time.time()
        BUILDERS[name]()
        print(f"{name:18s} {time.time() - t0:5.1f} s")


if __name__ == "__main__":
    main(sys.argv[1:])
