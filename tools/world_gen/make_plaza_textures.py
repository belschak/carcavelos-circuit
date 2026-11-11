"""Procedural textures of the upper plaza (V10i, 2026-11-11), made with the toolkit of make_textures.py.

Run after make_textures.py and make_street_textures.py:  python3 make_plaza_textures.py
Output in textures/ (no photos; colours are the survey's sunny averages, V10h section 3, a little warmer for our light):
  plaza_ochre_albedo      sand-ochre concrete slabs 3 m x 3 m with fine sawn joints (one slab per tile)
  skylight_glass_albedo   frosted glass, pale blue-green, opaque: two panels 1.25 m x 2.5 m per tile, dark joints
"""
import numpy as np

import make_textures as T
import make_street_textures as ST

rgb, noise, blur, smoothstep, lerp, grid, save = T.rgb, T.noise, T.blur, T.smoothstep, T.lerp, T.grid, T.save

OCHRE = "#E2BF88"
GLASS = "#9FBDC2"


def make_ochre(S=1024, seed=3101):
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    px_m = S / 3.0
    alb = ST.concrete_base(S, S, rng, px_m)                       # pale concrete clouds, stains, hairline cracks
    alb = alb / alb.mean(axis=(0, 1), keepdims=True) * rgb(OCHRE)[None, None, :]
    alb *= (1.0 + 0.035 * noise((S, S), 0.5 * px_m, rng))[..., None]          # slab-sized tone drift
    alb += 0.012 * noise((S, S), 1.2, rng)[..., None]                         # exposed aggregate
    d = np.minimum(np.minimum(X, S - X), np.minimum(Y, S - Y))
    alb = ST.joints(d, alb, depth=0.2)
    alb = T.match_mean(alb, OCHRE, 0.9)
    save("plaza_ochre_albedo.png", alb)


def make_glass(S=512, seed=3102):
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    px_m = S / 2.5
    v = 0.03 * noise((S, S), 0.4 * px_m, rng) + 0.012 * noise((S, S), 2.0, rng)
    alb = rgb(GLASS)[None, None, :] * (1.0 + v)[..., None]
    # frost: a soft lighter sheen towards the middle of each panel
    px, py = np.minimum(X % (S / 2), S / 2 - X % (S / 2)), np.minimum(Y, S - Y)
    edge = np.minimum(px, py)
    alb *= (0.97 + 0.05 * smoothstep(0.0, 0.25 * px_m, edge))[..., None]
    line = smoothstep(2.6, 1.0, edge)                                         # dark joint, steel frame
    alb = lerp(alb, rgb("#3C4548")[None, None, :] * np.ones((S, S, 1)), (0.85 * line)[..., None])
    save("skylight_glass_albedo.png", alb)


if __name__ == "__main__":
    make_ochre()
    make_glass()
