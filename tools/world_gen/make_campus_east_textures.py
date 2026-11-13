"""Procedural texture of the lower plaza (V16b, 2026-11-11), made with the toolkit of make_textures.py.

Run after make_textures.py:  python3 make_campus_east_textures.py
Output in textures/ (no photos; the colour is the survey's average, a step paler for our warm light):
  campus_east_concrete_albedo   beige-grey in-situ concrete, one panel of 4.5 m x 4.5 m with a thin saw-cut joint on
                                the tile border, soft clouds, darker rubbed patches, hairline cracks; 1024 px
"""
import time

import numpy as np

import campus_east_plan as P
import make_textures as T

rgb, noise, blur, smoothstep, grid, save = T.rgb, T.noise, T.blur, T.smoothstep, T.grid, T.save


def make_concrete(S_=1024, seed=1601):
    rng = np.random.default_rng(seed)
    px_m = S_ / P.PANEL
    X, Y = grid(S_, S_)
    clouds = 0.6 * noise((S_, S_), 0.22 * px_m, rng) + 0.5 * noise((S_, S_), 0.08 * px_m, rng)
    v = 0.024 * clouds + 0.008 * noise((S_, S_), 0.02 * px_m, rng) + 0.006 * noise((S_, S_), 1.0, rng)
    alb = rgb(P.PLAZA_CONCRETE)[None, None, :] * (1.0 + v)[..., None]
    alb += 0.006 * noise((S_, S_), 0.3 * px_m, rng)[..., None] * np.array([1.0, 0.3, -0.8])      # warm / cool drift
    rubbed = smoothstep(1.3, 2.5, noise((S_, S_), 0.15 * px_m, rng))                             # darker rubbed patches
    alb *= (1.0 - 0.05 * rubbed)[..., None]
    crack = (np.exp(-(noise((S_, S_), 0.16 * px_m, rng) / 0.012) ** 2)
             * smoothstep(0.2, 1.2, noise((S_, S_), 0.5 * px_m, rng)))
    alb *= (1.0 - 0.14 * blur(crack, 0.6))[..., None]
    d = np.minimum(np.minimum(X, S_ - X), np.minimum(Y, S_ - Y))                                 # saw-cut joint
    alb *= (1.0 - 0.20 * smoothstep(1.9, 0.7, d) - 0.03 * (1.0 - smoothstep(1.0, 9.0, d)))[..., None]
    alb = T.match_mean(alb, P.PLAZA_CONCRETE, 0.9)
    save("campus_east_concrete_albedo.png", alb)


def main():
    t0 = time.time()
    make_concrete()
    print(f"campus_east_concrete {time.time() - t0:5.1f} s")


if __name__ == "__main__":
    main()
