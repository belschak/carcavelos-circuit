"""Painted sky panorama for the race (design reference 2026-11-09: deep blue sky, big puffy cumulus clouds, warm
haze at the horizon, golden-hour sun from the west-south-west). Our own procedural picture, no photo.

Run:  ~/Documents/nova/.media-tools/bin/python make_sky.py
Out:  textures/sky_panorama.png  (2048 x 1024, equirectangular / lat-long)

Mapping = Unity's lat-long convention (Skybox/Panoramic, and the cubemap Unity makes from a 2:1 image):
u = 0.25 + azimuth / 360 (azimuth clockwise from north; north = +z in Unity), v = 1 at the zenith.
SUN_AZIMUTH / SUN_ELEVATION must match the directional light in CampusWorldBuilder.cs (AddSunAndSky).
"""
import math
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "textures", "sky_panorama.png")
W, H = 2048, 1024
SUN_AZIMUTH, SUN_ELEVATION = 225.0, 42.0   # V18l: high summer-afternoon sun (Luc, 12 Nov: "summer at the beach")
SEED = 20261109


def rgb(h):
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5)])


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def main():
    rng = np.random.default_rng(SEED)
    col = (np.arange(W) + 0.5) / W
    row = (np.arange(H) + 0.5) / H
    U, R = np.meshgrid(col, row)
    az = np.degrees((U - 0.25) * 2 * np.pi) % 360.0          # compass azimuth of each pixel
    el = 90.0 - R * 180.0                                    # elevation, +90 zenith .. -90 nadir

    # sky gradient: warm haze at the horizon, soft blue, deep blue at the zenith
    t = np.clip(el / 90.0, 0, 1)[..., None]
    # V18l summer: a pale clear horizon (no warm haze) into a saturated deep blue
    sky = lerp(rgb("#DCEAF3"), rgb("#8CC4EE"), smoothstep(0.0, 0.10, t))
    sky = lerp(sky, rgb("#3E95E2"), smoothstep(0.08, 0.45, t))
    sky = lerp(sky, rgb("#1F62C4"), smoothstep(0.4, 1.0, t))
    below = lerp(rgb("#D9CDB6"), rgb("#A7A496"), smoothstep(0.0, 0.25, np.clip(-el / 90.0, 0, 1)[..., None]))
    img = np.where((el >= 0)[..., None], sky, below)

    # sun: warm glow and a wide golden halo along the horizon on its side
    sa, se = math.radians(SUN_AZIMUTH), math.radians(SUN_ELEVATION)
    sun = np.array([math.sin(sa) * math.cos(se), math.sin(se), math.cos(sa) * math.cos(se)])
    a, e = np.radians(az), np.radians(el)
    d = np.stack([np.sin(a) * np.cos(e), np.sin(e), np.cos(a) * np.cos(e)], axis=-1)
    ang = np.degrees(np.arccos(np.clip(d @ sun, -1, 1)))
    glow = 0.55 * np.exp(-ang / 9.0) + 0.25 * np.exp(-ang / 35.0)
    img = lerp(img, rgb("#FFF7E8"), np.clip(glow * 0.8, 0, 1)[..., None])                # V18l: white summer glare
    img = lerp(img, rgb("#FFF6E6"), smoothstep(2.2, 1.2, ang)[..., None])          # small soft sun disc
    side = np.exp(-(((az - SUN_AZIMUTH + 180) % 360 - 180) / 70.0) ** 2) * np.exp(-np.clip(el, 0, None) / 8.0)
    img = lerp(img, rgb("#F4EBD8"), (0.12 * side * (el > -2))[..., None])                 # V18l: hardly any warm halo

    # cumulus: clusters of overlapping round puffs (a row along the flat base, smaller ones stacked on top),
    # each puff shaded from a lit top to a cool underside, the cluster warmer on the sun side
    dens = np.zeros((H, W))
    tone = np.zeros((H, W))                     # 0 shadowed underside .. 1 lit top
    sunside = np.zeros((H, W))
    clusters = []
    for k in range(11):                         # fewer, separate clusters: mostly blue sky between them
        caz = (k * 360 / 11 + rng.uniform(-9, 9)) % 360
        if abs(((caz - SUN_AZIMUTH + 180) % 360) - 180) < 18 or rng.uniform() < 0.25:
            continue                            # keep the sun itself clear, leave some gaps
        base = rng.uniform(2.0, 8.0)
        width = rng.uniform(20.0, 40.0)
        tall = rng.uniform(6.0, 12.0) * (0.75 if base > 6 else 1.0)
        clusters.append((caz, base, width, tall))
    for k in range(6):                          # a few small high clouds
        caz = rng.uniform(0, 360)
        if abs(((caz - SUN_AZIMUTH + 180) % 360) - 180) < 20:
            continue
        clusters.append((caz, rng.uniform(22.0, 34.0), rng.uniform(9.0, 16.0), rng.uniform(3.0, 5.0)))
    for caz, base, width, tall in clusters:
        puffs = []
        n = int(width / 4.0) + 3
        for j in range(n):                      # base row
            x = -width / 2 + (j + 0.5) * width / n + rng.uniform(-1.0, 1.0)
            hump = max(0.0, 1.0 - (2 * x / width) ** 2)
            r = (2.6 + 2.6 * hump) * rng.uniform(0.85, 1.15) * (tall / 13.0) ** 0.5
            puffs.append((x, base + r * 0.7, r))
        for j in range(int(n * 0.8)):           # stacked puffs, larger and higher towards the middle
            x = rng.uniform(-width / 3, width / 3)
            hump = max(0.0, 1.0 - (2 * x / width) ** 2)
            r = (2.4 + 3.4 * hump) * rng.uniform(0.85, 1.15) * (tall / 13.0) ** 0.5
            y = base + r * 0.7 + rng.uniform(0.25, 0.75) * tall * hump
            puffs.append((x, y, r))
        top = max(y + r for _x, y, r in puffs)
        dx_all = ((az - caz + 180) % 360 - 180) * np.cos(np.radians(np.clip(el, 0, 80)))
        box = (np.abs(dx_all) < width / 2 + 10) & (el > base - 1) & (el < top + 1)
        if not box.any():
            continue
        dx, ely = dx_all[box], el[box]
        c_d = np.zeros(dx.shape)
        c_t = np.zeros(dx.shape)
        for x, y, r in sorted(puffs, key=lambda q: q[1]):          # higher puffs drawn over lower ones
            dist = np.hypot(dx - x, ely - y)
            m = smoothstep(r + 0.3, r - 0.3, dist)
            local = np.clip(0.5 + 0.5 * (ely - y) / r, 0, 1)        # top of this puff = lit
            glob = np.clip((ely - base) / max(top - base, 1e-3), 0, 1)
            t_p = 0.55 * local + 0.45 * glob
            c_t = np.where(m > 0.5, t_p, c_t)
            c_d = np.maximum(c_d, m)
        c_d = c_d * smoothstep(base - 0.3, base + 0.5, ely)             # flat base
        sun_dx = ((SUN_AZIMUTH - caz + 180) % 360 - 180)
        lit = np.clip(0.5 + 0.5 * np.sign(sun_dx) * dx / (width / 2 + 1e-3), 0, 1) if abs(sun_dx) < 120 else 0.25
        cur = dens[box]
        take = c_d > cur
        dens[box] = np.maximum(cur, c_d)
        tone[box] = np.where(take, c_t, tone[box])
        sunside[box] = np.where(take, lit, sunside[box])
    shade = lerp(rgb("#B4C3DC"), rgb("#FFFFFF"), smoothstep(0.1, 0.65, tone)[..., None])   # V18l: bright white cumulus
    shade = lerp(shade, rgb("#FFF6E4"), (0.4 * sunside * smoothstep(0.45, 1.0, tone))[..., None])
    img = lerp(img, shade, (dens * 0.97)[..., None])

    img = np.clip(img, 0, 1)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8), "RGB").save(OUT, optimize=True)
    print(f"{OUT}: {W}x{H}, {len(clusters)} cloud clusters, sun at {SUN_AZIMUTH} / {SUN_ELEVATION} deg")


if __name__ == "__main__":
    main()
