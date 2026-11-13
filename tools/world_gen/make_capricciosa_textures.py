"""Texture of the Capricciosa package (V16g): cap_rubble_albedo.png, a rubble-stone wall (sea wall, esplanade parapet,
limestone cliff and rocks). Procedural, no photo: toroidal Voronoi stones in honey to grey tones with dark mortar joints.
Survey colours (Appendix G 3): rubble sea wall #b99c7c in low sun, cliff rock lit faces about #b5a487.

Run: python3 make_capricciosa_textures.py     (after make_textures.py; 512 px, tiles every 3 m)
"""
import numpy as np

import make_textures as T

rgb, noise, smoothstep, lerp, save = T.rgb, T.noise, T.smoothstep, T.lerp, T.save


def make_rubble(size=512, stones=120, seed=2161):
    rng = np.random.default_rng(seed)
    pts = rng.uniform(0, size, (stones, 2))
    tone = rng.normal(0.0, 1.0, stones)
    warm = rng.uniform(0.0, 1.0, stones)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float64)
    d1 = np.full((size, size), 1e9)
    d2 = np.full((size, size), 1e9)
    idx = np.zeros((size, size), dtype=np.int64)
    for k, (px, py) in enumerate(pts):
        dx = np.abs(xx - px)
        dy = np.abs(yy - py)
        dx = np.minimum(dx, size - dx)                    # wraps: the texture tiles
        dy = np.minimum(dy, size - dy) * 1.35             # stones lie flat (wider than high)
        d = np.hypot(dx, dy)
        closer = d < d1
        d2 = np.where(closer, d1, np.minimum(d2, d))
        idx = np.where(closer, k, idx)
        d1 = np.where(closer, d, d1)
    edge = d2 - d1                                        # small at the joint between two stones
    base = lerp(rgb("#A39480")[None, None, :], rgb("#C2A680")[None, None, :], warm[idx][..., None])
    alb = base * (1.0 + 0.085 * tone[idx] + 0.03 * noise((size, size), 6.0, rng) + 0.02 * noise((size, size), 1.2, rng))[..., None]
    alb *= (1.0 - 0.10 * smoothstep(14.0, 3.0, edge))[..., None]                 # stones round off towards the joint
    alb = lerp(alb, rgb("#6E6456")[None, None, :], smoothstep(3.2, 1.0, edge)[..., None] * 0.85)
    alb = T.match_mean(alb, "#B09A7E", 0.9)
    save("cap_rubble_albedo.png", alb)


if __name__ == "__main__":
    make_rubble()
