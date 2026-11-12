"""Procedural textures of the coast section (V16e, 2026-11-11). No photo data: noise, joints and averaged survey colours.

Run:  python3 make_coast_textures.py
Writes textures/coast_atlas_albedo.png        1024 px: the bands of coast_plan.ATLAS (each tiles along u only) and the
                                              flat colours of coast_plan.PALETTE; one material for the whole section
       textures/coast_render_beige_albedo.png 512 px, tiling: sand-beige render of the tunnel ramp's walls and the
                                              north portal (#c8b890, survey 3.6)
       textures/coast_boardmarked_albedo.png  512 px, tiling: board-marked in-situ concrete (tunnel ceiling, 2 m tile)
V18h (2026-11-12), the ground and edges seen close from the kart (Street View Oct 2009 / Oct 2019 / Dec 2022):
       textures/coast_concrete_{beige,ochre,pale}_albedo.png + _normal.png   1024 px, tiling both ways: in-situ concrete
                                              slabs with saw-cut joints, broom finish, aggregate, stains, hairline
                                              cracks (coast_plan.CONCRETE: colour, tile and slab size in metres)
       textures/coast_trim_albedo.png         1024 px: the bands of coast_plan.TRIM (white precast blocks, kerb stones,
                                              white wall panels, timber, cast-iron covers) and coast_plan.TRIM_PALETTE
"""
import os

import numpy as np
from PIL import Image

import coast_plan as P

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "textures")
S = P.ATLAS_SIZE


def rgb(h):
    return np.array([int(h[k:k + 2], 16) for k in (1, 3, 5)], np.float32)


def tile_noise(h, w, rng, octaves=4, wrap_y=False):
    """Smooth noise that tiles along x (and along y if wrap_y), about -0.5..0.5."""
    out = np.zeros((h, w), np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cw, ch = max(2, w >> (o + 2 if w > 64 else 1)), max(2, h >> (o + 2 if h > 64 else 1))
        cw, ch = max(2, w // max(1, cw)), max(2, h // max(1, ch))
        small = rng.random((ch, cw)).astype(np.float32)
        small = np.concatenate([small, small[:, :1]], axis=1)
        small = np.concatenate([small, small[:1] if wrap_y else small[-1:]], axis=0)
        big = np.asarray(Image.fromarray((small * 255).astype(np.uint8)).resize((w + w // cw, h + h // ch), Image.BICUBIC),
                         np.float32)[:h, :w] / 255.0
        out += (big - 0.5) * amp
        total += amp
        amp *= 0.55
    return out / total


def blocks(h, w, rng, course_px, len_px, joint=1, jitter=0.35):
    """Coursed masonry: (block id map, joint mask). Blocks of varying length, joints staggered per course; tiles in x."""
    ids = np.zeros((h, w), np.int32)
    joints = np.zeros((h, w), np.float32)
    n = 0
    for c, y0 in enumerate(range(0, h, course_px)):
        x = int(rng.integers(0, len_px))
        edges = []
        while x < w + len_px:
            edges.append(x)
            x += int(len_px * (1.0 + jitter * rng.uniform(-1, 1)))
        for a, b in zip(edges, edges[1:]):
            n += 1
            cols = np.arange(a, b) % w
            ids[y0:y0 + course_px, cols] = n
            joints[y0:y0 + course_px, np.arange(a, a + joint) % w] = 1.0
        joints[y0:y0 + joint, :] = 1.0
    return ids, joints


def per_block(ids, rng, spread):
    table = rng.uniform(-spread, spread, ids.max() + 1).astype(np.float32)
    return table[ids]


def band_stone(h, w, rng):
    """Coursed grey-buff sea wall (survey #a49280, shown a little lighter): courses 0.3 m, blocks 0.4-0.8 m, darker
    and greener at the foot."""
    ids, joints = blocks(h, w, rng, course_px=h // 10, len_px=int(w * 0.6 / 12.8), joint=2)
    base = rgb("#B4A48E")
    col = base[None, None] * (1.0 + per_block(ids, rng, 0.09) + 0.10 * tile_noise(h, w, rng))[..., None]
    col *= (1.0 - 0.38 * joints)[..., None]
    foot = np.clip((np.arange(h) / h - 0.72) / 0.28, 0, 1)[:, None]            # rows count downwards: the foot is last
    col = col * (1.0 - 0.22 * foot)[..., None] + rgb("#6C7458")[None, None] * (0.16 * foot)[..., None]
    return col


def band_rubble(h, w, rng):
    """Rounded cream stones set in mortar (revetment, #baa993)."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    n = 420
    cx, cy = rng.uniform(0, w, n), rng.uniform(0, h, n)
    d1 = np.full((h, w), 1e9, np.float32)
    d2 = np.full((h, w), 1e9, np.float32)
    who = np.zeros((h, w), np.int32)
    for k in range(n):
        dx = np.abs(xs - cx[k])
        dx = np.minimum(dx, w - dx)
        d = np.hypot(dx, (ys - cy[k]) * 1.25)
        closer = d < d1
        d2 = np.where(closer, d1, np.minimum(d2, d))
        who = np.where(closer, k, who)
        d1 = np.where(closer, d, d1)
    edge = np.clip((d2 - d1) / 5.0, 0, 1)
    tone = rng.uniform(-0.10, 0.10, n).astype(np.float32)[who]
    col = rgb("#C6B69F")[None, None] * (1.0 + tone + 0.06 * tile_noise(h, w, rng))[..., None]
    mortar = rgb("#9C917F")[None, None] * np.ones((h, w, 1), np.float32)
    return mortar + (col - mortar) * edge[..., None]


def band_broom(h, w, rng):
    """Beige broom-finished concrete (#c7b18f sun): fine cross streaks, a joint every 4 m along u and at the band's
    two edges (the band is one 4 m panel wide)."""
    streak = np.repeat(rng.normal(0, 1, (1, w)).astype(np.float32), h, axis=0)
    streak = (streak + np.roll(streak, 1, 1) + np.roll(streak, -1, 1)) / 3.0
    col = rgb("#CDB894")[None, None] * (1.0 + 0.035 * streak + 0.07 * tile_noise(h, w, rng))[..., None]
    joint = np.zeros((h, w), np.float32)
    for x in range(0, w, w // 4):
        joint[:, x:x + 2] = 1.0
    joint[:2, :] = 1.0
    joint[-2:, :] = 1.0
    return col * (1.0 - 0.3 * joint)[..., None]


def band_tiles(h, w, rng):
    """Terracotta roof tiles (#ad6d65 from the air, warmer here): rows down the slope, half-round tiles across."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    row = h / 12.0
    across = 0.5 + 0.5 * np.cos(2 * np.pi * xs / (w / 32.0))
    down = (ys % row) / row
    ids = (ys // row).astype(np.int32) * 64 + (xs // (w / 32.0)).astype(np.int32)
    tone = rng.uniform(-0.08, 0.08, ids.max() + 1).astype(np.float32)[ids]
    col = rgb("#BC735B")[None, None] * (0.82 + 0.22 * across + tone - 0.20 * (down > 0.88))[..., None]
    return col


def band_boards(h, w, rng):
    """White-painted vertical boards (bar walls, deck boarding)."""
    board = w // 40
    xs = np.arange(w)
    gap = ((xs % board) < 1).astype(np.float32)[None, :]
    tone = np.repeat(rng.uniform(-0.035, 0.035, w // board + 1), board)[:w][None, :]
    col = rgb("#EAE6DC")[None, None] * (1.0 + tone + 0.03 * tile_noise(h, w, rng) - 0.22 * gap)[..., None]
    return col * np.ones((h, 1, 1), np.float32)


def band_oldwall(h, w, rng):
    """Old grey stone and concrete wall with lichen (the field's corner, 1.2 m)."""
    ids, joints = blocks(h, w, rng, course_px=h // 4, len_px=int(w * 0.55 / 9.6), joint=2, jitter=0.5)
    n = tile_noise(h, w, rng, 5)
    col = rgb("#8F8B80")[None, None] * (1.0 + per_block(ids, rng, 0.12) + 0.22 * n)[..., None]
    col *= (1.0 - 0.3 * joints)[..., None]
    lichen = np.clip((tile_noise(h, w, rng, 3) - 0.05) * 6.0, 0, 1)
    return col + (rgb("#A9A27A")[None, None] - col) * (0.35 * lichen)[..., None]


def band_render(h, w, rng):
    """Rough white render with faint weather streaks (the prow, the hut, white walls)."""
    n = tile_noise(h, w, rng, 5)
    streak = np.clip(tile_noise(h, w // 8, rng, 3).repeat(8, axis=1)[:, :w] * 2.0, -1, 1)
    down = (np.arange(h) / h)[:, None]
    col = rgb("#EDE9E0")[None, None] * (1.0 + 0.035 * n - 0.05 * down * (0.5 + streak))[..., None]
    return col


def band_deck(h, w, rng):
    """Pale weathered deck boards running along u."""
    board = h // 12
    ys = np.arange(h)
    gap = ((ys % board) < 1).astype(np.float32)[:, None]
    tone = np.repeat(rng.uniform(-0.06, 0.06, h // board + 1), board)[:h][:, None]
    grain = tile_noise(h, w // 4, rng, 3).repeat(4, axis=1)[:, :w]
    col = rgb("#CFC4AE")[None, None] * (1.0 + tone + 0.06 * grain - 0.3 * gap)[..., None]
    return col


def band_hedge(h, w, rng):
    """Tamarisk: grey-green, feathery (#40433b at dusk, lighter in sun), darker towards the foot."""
    n = tile_noise(h, w, rng, 5)
    fine = rng.normal(0, 1, (h, w)).astype(np.float32)
    down = (np.arange(h) / h)[:, None]
    col = rgb("#6C7A5C")[None, None] * (1.0 + 0.5 * n + 0.07 * fine - 0.35 * down)[..., None]
    return col


BAND_FN = {"stone": band_stone, "rubble": band_rubble, "broom": band_broom, "tiles": band_tiles, "boards": band_boards,
           "oldwall": band_oldwall, "render": band_render, "deck": band_deck, "hedge": band_hedge}


def make_atlas(seed=1611):
    rng = np.random.default_rng(seed)
    img = np.zeros((S, S, 3), np.float32)
    for name, b in P.ATLAS.items():
        r0, h = b["row"], b["px"]
        if name == "pal":
            for k, colour in enumerate(P.PALETTE.values()):
                img[r0:r0 + h, k * 64:(k + 1) * 64] = rgb(colour)
            continue
        img[r0:r0 + h] = BAND_FN[name](h, S, rng)
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "coast_atlas_albedo.png"))
    print("coast_atlas_albedo.png", S, "px,", len(P.ATLAS), "bands,", len(P.PALETTE), "flat colours")


def make_render_beige(size=512, seed=1612):
    rng = np.random.default_rng(seed)
    n = tile_noise(size, size, rng, 5, wrap_y=True)
    fine = rng.normal(0, 1, (size, size)).astype(np.float32)
    col = rgb("#C8B890")[None, None] * (1.0 + 0.07 * n + 0.012 * fine)[..., None]
    Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "coast_render_beige_albedo.png"))
    print("coast_render_beige_albedo.png", size, "px")


def make_boardmarked(size=512, seed=1613):
    """2 m tile: ten boards of 0.2 m along v, grey-beige, a few stains."""
    rng = np.random.default_rng(seed)
    board = size // 10
    xs = np.arange(size)
    gap = ((xs % board) < 2).astype(np.float32)[None, :]
    tone = np.repeat(rng.uniform(-0.04, 0.04, 11), board)[:size][None, :]
    grain = np.repeat(tile_noise(size // 8, size, rng, 3, wrap_y=True), 8, axis=0)[:size]
    stain = np.clip(tile_noise(size, size, rng, 3, wrap_y=True) * 1.6, -0.5, 0.5)
    col = rgb("#BEB6A6")[None, None] * (1.0 + tone + 0.05 * grain + 0.08 * stain - 0.2 * gap)[..., None]
    Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "coast_boardmarked_albedo.png"))
    print("coast_boardmarked_albedo.png", size, "px")


# =====================================================================================================
# V18h: concrete slabs (tiling both ways) and the trim atlas
# =====================================================================================================
def wrap_noise(n, cells, rng):
    """Smooth noise in -0.5..0.5 that tiles both ways: a random grid of `cells` x `cells`, bicubic."""
    small = rng.random((cells, cells)).astype(np.float32)
    pad = np.pad(small, 2, mode="wrap")
    k = n // cells
    big = np.asarray(Image.fromarray((pad * 255).astype(np.uint8)).resize(((cells + 4) * k, (cells + 4) * k), Image.BICUBIC),
                     np.float32) / 255.0
    out = big[2 * k:2 * k + cells * k, 2 * k:2 * k + cells * k]
    if out.shape[0] != n:
        out = np.asarray(Image.fromarray((out * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC), np.float32) / 255.0
    return out - 0.5


def fbm(n, rng, cells=(4, 8, 16, 32), gain=0.55):
    out, amp, total = np.zeros((n, n), np.float32), 1.0, 0.0
    for c in cells:
        out += wrap_noise(n, c, rng) * amp
        total += amp
        amp *= gain
    return out / total


def wrap_blots(n, rng, count, r0, r1, stretch=(1.0, 1.0)):
    """Soft round blots (0..1) at random places, wrapped; radius r0..r1 px, optionally stretched along x / y."""
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    out = np.zeros((n, n), np.float32)
    for _ in range(count):
        cx, cy, r = rng.uniform(0, n), rng.uniform(0, n), rng.uniform(r0, r1)
        dx = np.abs(xs - cx)
        dx = np.minimum(dx, n - dx) / stretch[0]
        dy = np.abs(ys - cy)
        dy = np.minimum(dy, n - dy) / stretch[1]
        out = np.maximum(out, np.clip(1.0 - np.hypot(dx, dy) / r, 0, 1) ** 1.5 * rng.uniform(0.4, 1.0))
    return out


def normal_from_height(h, strength):
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    nz = np.ones_like(h)
    ln = np.sqrt(gx * gx + gy * gy + nz * nz)
    return np.stack([(-gx / ln + 1) * 127.5, (gy / ln + 1) * 127.5, (nz / ln) * 255.0], -1)


def make_concrete(name, spec, n=1024):
    """In-situ concrete slabs: u = along the road. spec: colour, tile (m), slab (m along u, m along v), seed, stain."""
    rng = np.random.default_rng(spec["seed"])
    px = n / spec["tile"]                                   # pixels per metre
    su, sv = spec["slab"]
    nu, nv = int(round(spec["tile"] / su)), int(round(spec["tile"] / sv))
    ys, xs = np.mgrid[0:n, 0:n]
    slab_id = (ys * nv // n) * nu + (xs * nu // n)
    tone = rng.uniform(-0.035, 0.035, nu * nv).astype(np.float32)[slab_id]
    warm = rng.uniform(-1.0, 1.0, nu * nv).astype(np.float32)[slab_id]
    # distance (px) to the nearest slab joint; the joints lie on the tile's border too, so the tile repeats cleanly
    du = np.minimum(xs % (n // nu), (n // nu) - xs % (n // nu)).astype(np.float32)
    dv = np.minimum(ys % (n // nv), (n // nv) - ys % (n // nv)).astype(np.float32)
    dj = np.minimum(du, dv)
    cut = np.clip(1.0 - dj / max(1.0, 0.008 * px), 0, 1)                # the saw cut, 1.6 cm
    grime = np.clip(1.0 - dj / (0.22 * px), 0, 1) ** 2                  # dirt that gathers along a joint
    blotch = fbm(n, rng, (3, 6, 12, 24))
    fine = fbm(n, rng, (64, 128), 0.7)
    grain = rng.normal(0, 1, (n, n)).astype(np.float32)
    broom = np.repeat(rng.normal(0, 1, (n, 1)).astype(np.float32), n, axis=1)
    broom = (broom + np.roll(broom, 1, 0)) / 2.0
    broom = broom.T                                                     # broom strokes run across the road (along v)
    specks = (rng.random((n, n)) < 0.006).astype(np.float32) * rng.uniform(0.5, 1.0, (n, n)).astype(np.float32)
    pale_specks = (rng.random((n, n)) < 0.004).astype(np.float32)
    stains = wrap_blots(n, rng, spec.get("stains", 14), 0.10 * px, 0.45 * px)
    drips = wrap_blots(n, rng, spec.get("drips", 40), 0.015 * px, 0.05 * px)
    wear = wrap_blots(n, rng, 5, 0.5 * px, 1.3 * px, stretch=(3.0, 1.0))             # long pale worn patches along the road
    # hairline cracks: random walks that start on a joint
    crack = np.zeros((n, n), np.float32)
    for _ in range(spec.get("cracks", 5)):
        x, y = float(rng.integers(0, nu) * (n // nu)), float(rng.uniform(0, n))
        ang = rng.uniform(-0.6, 0.6)
        for _step in range(int(rng.uniform(0.8, 2.6) * px)):
            crack[int(y) % n, int(x) % n] = 1.0
            ang += rng.normal(0, 0.18)
            ang = float(np.clip(ang, -1.2, 1.2))
            x += np.cos(ang)
            y += np.sin(ang)
    lum = (1.0 + tone + 0.10 * blotch + 0.05 * fine + 0.022 * grain + 0.022 * broom - 0.16 * specks + 0.07 * pale_specks
           - spec.get("stain", 0.13) * stains - 0.18 * drips + 0.04 * wear - 0.11 * grime - 0.24 * crack)
    lum *= 1.0 - 0.5 * cut
    base = rgb(spec["colour"])
    col = base[None, None] * lum[..., None]
    col[..., 0] *= 1.0 + 0.012 * warm + 0.015 * blotch                 # a little colour drift: warmer / greyer slabs
    col[..., 2] *= 1.0 - 0.018 * warm - 0.03 * stains
    col = col + (rgb("#6E6A5E")[None, None] - col) * (0.25 * grime * (0.5 + blotch))[..., None] * 0.6
    Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(os.path.join(OUT, name + "_albedo.png"))
    height = -1.0 * cut - 0.25 * crack + 0.05 * grain + 0.08 * fine + 0.03 * broom - 0.2 * specks
    Image.fromarray(np.clip(normal_from_height(height, 1.6), 0, 255).astype(np.uint8)).save(os.path.join(OUT, name + "_normal.png"))
    print(name + "_albedo.png / _normal.png", n, "px,", spec["tile"], "m tile,", nu * nv, "slabs")


def strip_noise(h, w, rng, cells=(8, 32, 96)):
    """Noise for a band w x h that tiles along x."""
    big = fbm(w, rng, cells)
    return big[:h] if h <= w else np.tile(big, (h // w + 1, 1))[:h]


def trim_precast(h, w, rng, top=False):
    """White precast concrete blocks, 2.4 m long (four per tile): joints, bug holes, a chamfered top edge, drip streaks
    and a damp green foot on the faces (`top`: the top face, without foot and streaks)."""
    xs = np.arange(w)
    block = w // 4
    bid = xs // block
    tone = rng.uniform(-0.03, 0.03, 4).astype(np.float32)[bid][None, :]
    joint = ((xs % block) < 3).astype(np.float32)[None, :]
    n1 = strip_noise(h, w, rng)
    grain = rng.normal(0, 1, (h, w)).astype(np.float32)
    holes = (rng.random((h, w)) < 0.004).astype(np.float32)
    down = (np.arange(h) / h)[:, None]
    lum = 1.0 + tone + 0.05 * n1 + 0.012 * grain - 0.2 * holes - 0.45 * joint
    col = rgb("#E6E2D8")[None, None] * lum[..., None]
    if top:
        edge = np.minimum(np.arange(h), h - 1 - np.arange(h))[:, None]
        col *= (1.0 - 0.10 * np.clip(1.0 - edge / 5.0, 0, 1))[..., None]            # the two arrises, a little darker
        dirt = np.clip(strip_noise(h, w, rng, (6, 20)) * 3.0, 0, 1)
        return col + (rgb("#B9B2A0")[None, None] - col) * (0.25 * dirt)[..., None]
    streak = np.clip(np.repeat(rng.normal(0, 1, (1, w)).astype(np.float32), h, axis=0), 0, 3) / 3.0
    streak = (streak + np.roll(streak, 1, 1) + np.roll(streak, -1, 1)) / 3.0
    col *= (1.0 - 0.10 * streak * (0.3 + down))[..., None]
    col[:4] *= 1.06                                                                  # chamfer catches the light
    foot = np.clip((down - 0.72) / 0.28, 0, 1) * (0.6 + 0.8 * (n1 + 0.5))
    return col + (rgb("#8E8F72")[None, None] - col) * (0.42 * np.clip(foot, 0, 1))[..., None]


def trim_kerb(h, w, rng):
    """Pale concrete kerb stones, 1 m long (eight per tile)."""
    xs = np.arange(w)
    stone = w // 8
    tone = rng.uniform(-0.06, 0.06, 8).astype(np.float32)[xs // stone][None, :]
    joint = ((xs % stone) < 3).astype(np.float32)[None, :]
    grain = rng.normal(0, 1, (h, w)).astype(np.float32)
    lum = 1.0 + tone + 0.06 * strip_noise(h, w, rng, (16, 64)) + 0.03 * grain - 0.4 * joint
    edge = np.minimum(np.arange(h), h - 1 - np.arange(h))[:, None]
    lum = lum * (1.0 - 0.16 * np.clip(1.0 - edge / 6.0, 0, 1))
    return rgb("#C4BCAC")[None, None] * lum[..., None]


def trim_wall(h, w, rng):
    """White painted in-situ concrete wall: 2.4 m panels, tie holes, weather streaks from the coping, a damp foot."""
    xs = np.arange(w)
    panel = w // 4
    joint = ((xs % panel) < 2).astype(np.float32)[None, :]
    tone = rng.uniform(-0.02, 0.02, 4).astype(np.float32)[xs // panel][None, :]
    n1 = strip_noise(h, w, rng, (6, 24, 96))
    grain = rng.normal(0, 1, (h, w)).astype(np.float32)
    down = (np.arange(h) / h)[:, None]
    streak = np.clip(np.repeat(rng.normal(0, 1, (1, w)).astype(np.float32), h, axis=0), 0.4, 3) / 3.0
    for _ in range(3):
        streak = (streak + np.roll(streak, 1, 1) + np.roll(streak, -1, 1)) / 3.0
    lum = 1.0 + tone + 0.035 * n1 + 0.008 * grain - 0.22 * joint - 0.16 * streak * np.clip(1.2 - down * 1.4, 0, 1)
    col = rgb("#ECE8DF")[None, None] * lum[..., None]
    for k in range(4):                                                               # form-tie holes, two rows
        for fy in (0.3, 0.7):
            for fx in (0.25, 0.75):
                cx, cy = int((k + fx) * panel), int(fy * h)
                col[cy - 2:cy + 3, cx - 2:cx + 3] *= 0.8
    col[:5] *= 0.93                                                                  # the coping's shadow line
    foot = np.clip((down - 0.8) / 0.2, 0, 1) * (0.5 + n1 + 0.5)
    return col + (rgb("#A49F88")[None, None] - col) * (0.4 * np.clip(foot, 0, 1))[..., None]


def trim_timber(h, w, rng):
    """Weathered grey-brown timber, grain along u."""
    grain = np.repeat(rng.normal(0, 1, (h, 1)).astype(np.float32), w, axis=1)
    grain = (grain + np.roll(grain, 1, 0)) / 2.0
    n1 = strip_noise(h, w, rng, (8, 32))
    check = (rng.random((h, w)) < 0.002).astype(np.float32)
    for _ in range(18):
        check = np.maximum(check, np.roll(check, 1, 1) * 0.92)                       # short drying cracks along the grain
    lum = 1.0 + 0.10 * grain + 0.12 * n1 - 0.3 * check
    return rgb("#8C7A62")[None, None] * lum[..., None]


def trim_iron(h, w, rng):
    """Cast-iron covers, one per square cell (h x h): 0 round manhole cover in its concrete frame, 1 slot gully grate,
    2 dark planting soil / mulch, 3 plain cast iron. The other cells stay plain concrete grey."""
    out = np.zeros((h, w, 3), np.float32) + rgb("#B4AC9C")[None, None]
    ys, xs = np.mgrid[0:h, 0:h].astype(np.float32)
    r = np.hypot(xs - h / 2, ys - h / 2) / (h / 2)
    iron = rgb("#4E4B48")
    noise = 1.0 + 0.10 * rng.normal(0, 1, (h, h)).astype(np.float32)
    cell = np.zeros((h, h, 3), np.float32) + rgb("#A9A294")[None, None]              # 0: the round cover
    studs = ((np.sin(xs * 0.9) * np.sin(ys * 0.9)) > 0.35).astype(np.float32)
    lid = iron[None, None] * (noise * (0.9 + 0.22 * studs * (r < 0.78)))[..., None]
    ring = ((r > 0.80) & (r < 0.86)) | (r > 0.93)
    cell = np.where((r < 0.97)[..., None], lid, cell)
    cell = np.where((ring & (r < 0.97))[..., None], iron[None, None] * 0.6, cell)
    out[:, 0:h] = cell
    cell = np.zeros((h, h, 3), np.float32) + iron[None, None] * noise[..., None]     # 1: the gully grate
    slots = ((xs % 14) < 6) & (ys > 0.12 * h) & (ys < 0.88 * h) & (xs > 0.08 * h) & (xs < 0.92 * h)
    cell = np.where(slots[..., None], rgb("#1E1D1C")[None, None], cell)
    out[:, h:2 * h] = cell
    soil = rgb("#5A4A3A")[None, None] * (1.0 + 0.25 * rng.normal(0, 1, (h, h)).astype(np.float32)
                                         + 0.3 * strip_noise(h, h, rng, (8, 16)))[..., None]
    out[:, 2 * h:3 * h] = soil                                                       # 2: mulch
    out[:, 3 * h:4 * h] = iron[None, None] * noise[..., None]                        # 3: plain iron
    return out


TRIM_FN = {"precast": trim_precast, "precast_top": lambda h, w, rng: trim_precast(h, w, rng, top=True), "kerb": trim_kerb,
           "wall": trim_wall, "timber": trim_timber, "iron": trim_iron}


def make_trim(seed=1811):
    rng = np.random.default_rng(seed)
    img = np.zeros((S, S, 3), np.float32)
    for name, b in P.TRIM.items():
        r0, h = b["row"], b["px"]
        if name == "pal":
            for k, colour in enumerate(P.TRIM_PALETTE.values()):
                img[r0:r0 + h, k * 64:(k + 1) * 64] = rgb(colour)
            continue
        img[r0:r0 + h] = TRIM_FN[name](h, S, rng)
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "coast_trim_albedo.png"))
    print("coast_trim_albedo.png", S, "px,", len(P.TRIM), "bands,", len(P.TRIM_PALETTE), "flat colours")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    make_atlas()
    make_render_beige()
    make_boardmarked()
    for _name, _spec in P.CONCRETE.items():
        make_concrete("coast_concrete_" + _name, _spec)
    make_trim()
