"""Procedural textures of the coast section (V16e, 2026-11-11). No photo data: noise, joints and averaged survey colours.

Run:  python3 make_coast_textures.py
Writes textures/coast_atlas_albedo.png        1024 px: the bands of coast_plan.ATLAS (each tiles along u only) and the
                                              flat colours of coast_plan.PALETTE; one material for the whole section
       textures/coast_render_beige_albedo.png 512 px, tiling: sand-beige render of the tunnel ramp's walls and the
                                              north portal (#c8b890, survey 3.6)
       textures/coast_boardmarked_albedo.png  512 px, tiling: board-marked in-situ concrete (tunnel ceiling, 2 m tile)
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


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    make_atlas()
    make_render_beige()
    make_boardmarked()
