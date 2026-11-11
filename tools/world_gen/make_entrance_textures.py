"""Procedural textures of the entrance plaza (V15a, 2026-11-11), made with the toolkit of make_textures.py.

Run after make_textures.py:  python3 make_entrance_textures.py     (make_street_textures.py calls it at its end)
Output in textures/ (no photos; colours from the survey's sunlit averages, a step warmer for our light):
  entrance_calcada_albedo   pale limestone cubes 12.5 x 19 cm in straight staggered rows, 4 m x 4 m, 1024 px
  entrance_drain_albedo     dark-red drain grating: slots across the run, 0.5 m long x 0.35 m wide, 128 px
"""
import sys
import time

import numpy as np

import entrance_plan as P
import make_textures as T

rgb, noise, smoothstep, lerp, grid, save = T.rgb, T.noise, T.smoothstep, T.lerp, T.grid, T.save


def make_calcada(S_=1024, seed=1401):
    rng = np.random.default_rng(seed)
    X, Y = grid(S_, S_)
    rows, per_row = 32, 21
    rh, cw = S_ / rows, S_ / per_row
    wave = 0.05 * rh * np.sin(2 * np.pi * X / (S_ / 3.0))               # the rows are not ruler-straight
    ry = (Y + wave) / rh
    r = np.floor(ry).astype(np.int64) % rows
    shift = np.random.default_rng(seed + 1).uniform(0, 1, rows)[r]
    cx = X / cw + shift
    k = np.floor(cx).astype(np.int64) % per_row
    fx, fy = cx - np.floor(cx), ry - np.floor(ry)
    wob = 0.05 * noise((S_, S_), 4.0, rng)
    d = np.minimum(np.minimum(fx, 1 - fx) * cw, np.minimum(fy, 1 - fy) * rh) + wob * rh
    sid = r * per_row + k
    rs = np.random.default_rng(seed + 2)
    bright = rs.normal(1.0, 0.028, rows * per_row)[sid]
    warm = rs.uniform(-1, 1, rows * per_row)[sid][..., None]
    base = rgb(P.CALCADA)
    stone = np.where(warm >= 0, lerp(base, rgb("#E0D2A6"), warm * 0.4), lerp(base, rgb("#D2CDB8"), -warm * 0.4))
    stone = stone * (bright * (1.0 + 0.02 * noise((S_, S_), 2.0, rng) + 0.012 * noise((S_, S_), 0.8, rng)))[..., None]
    cov = smoothstep(0.7, 2.4, d)
    pillow = 1.0 - (1.0 - smoothstep(0.0, 6.0, np.maximum(d - 0.7, 0.0))) ** 2
    stone *= (0.955 + 0.045 * pillow)[..., None]
    joint = rgb("#BDB18F")[None, None, :] * (1.0 + 0.04 * noise((S_, S_), 1.2, rng))[..., None]
    alb = lerp(joint, stone, cov[..., None])
    alb *= (1.0 + 0.018 * noise((S_, S_), 0.16 * S_, rng) + 0.012 * noise((S_, S_), 0.05 * S_, rng))[..., None]   # soft clouds
    alb = T.match_mean(alb, P.CALCADA, 1.0)
    save("entrance_calcada_albedo.png", alb)


def make_drain(W=128, H=128, seed=1402):
    """u (x) runs along the channel (0.5 m), v (y) across it (0.35 m). Tiles in u; mirrored in v."""
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    v = np.abs(Y / H - 0.5) * 2.0                          # 0 in the middle, 1 at the rim
    slots = 12
    f = (X / W * slots) % 1.0
    slot = smoothstep(0.30, 0.38, f) * (1.0 - smoothstep(0.62, 0.70, f)) * (1.0 - smoothstep(0.62, 0.70, v))
    base = rgb(P.DRAIN)[None, None, :] * (1.0 + 0.05 * noise((H, W), 6.0, rng) + 0.03 * noise((H, W), 1.0, rng))[..., None]
    rim = smoothstep(0.80, 0.9, v)
    alb = lerp(base, rgb("#5E2C23")[None, None, :], (0.5 * rim)[..., None])       # the frame
    alb = lerp(alb, rgb("#2E1612")[None, None, :], slot[..., None])
    save("entrance_drain_albedo.png", alb)


BUILDERS = {"entrance_calcada": make_calcada, "entrance_drain": make_drain}


def main(argv=()):
    for name in argv or list(BUILDERS):
        t0 = time.time()
        BUILDERS[name]()
        print(f"{name:18s} {time.time() - t0:5.1f} s")


if __name__ == "__main__":
    main(sys.argv[1:])
