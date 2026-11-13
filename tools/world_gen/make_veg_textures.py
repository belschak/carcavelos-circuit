"""V18n: the leaf atlas of the plants (env_vegetation.leaf_material): textures/veg_leaves_cutout_albedo.png.

Run:  python3 make_veg_textures.py        (numpy + pillow; no photos, everything is drawn here)

One 1024 x 1024 RGBA sheet, alpha = leaf / air, for the leaf cards on shrubs, tree crowns, hedges and grass clumps.
The colours are NEUTRAL (light grey-green, 0.5-1.0): the plant's own colour comes from the vertex colours the
generator writes (dark and cool inside a crown, light and warm outside), so one material serves every plant.
Tiles (u0, v0, u1, v1; v up as in the meshes), each with a clear margin so neighbours never bleed in the mip maps:
    broad   (0.00, 0.50, 0.50, 1.00)   clump of round leaves (pittosporum, oleander mass): shrubs, hedges, young trees
    needle  (0.50, 0.50, 1.00, 1.00)   tufts of pine needles: umbrella pine
    spray   (0.00, 0.00, 0.50, 0.50)   twigs with narrow leaves: olive, tamarisk; tinted red it is a blossom spray
    blades  (0.50, 0.00, 0.75, 0.50)   a fan of long grass blades growing from the bottom centre: pampas grass
    solid   (0.75, 0.25, 1.00, 0.50)   leaves all over, no holes: the dark inner body of a shrub or hedge
    plume   (0.75, 0.00, 1.00, 0.25)   one feathery plume, growing up from the bottom centre: pampas grass
The alpha is slightly blurred (a soft edge around the 0.5 contour), so the outline stays put in the smaller mip maps,
and the colour is spread into the transparent part, so no dark fringe shows at the leaf edges.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
TEX_DIR = os.path.join(HERE, "textures")
S = 1024
K = 3                                   # drawn at 3x, then scaled down
TILES = {"broad": (0.0, 0.5, 0.5, 1.0), "needle": (0.5, 0.5, 1.0, 1.0), "spray": (0.0, 0.0, 0.5, 0.5),
         "blades": (0.5, 0.0, 0.75, 0.5), "solid": (0.75, 0.25, 1.0, 0.5), "plume": (0.75, 0.0, 1.0, 0.25)}


def _col(v, warm=0.0):
    """Neutral leaf tone: brightness v (0..1), warm > 0 a little yellower, < 0 a little bluer."""
    r = v * (1.0 + 0.10 * warm)
    g = v * (1.0 + 0.03 * abs(warm))
    b = v * (1.0 - 0.16 * warm)
    return tuple(int(255 * min(max(c, 0.0), 1.0)) for c in (r, g, b)) + (255,)


def _leaf(draw, x, y, ang, length, width, v, warm, rib=True, tip=1.0):
    """One leaf from its stalk end (x, y) along `ang`: a pointed oval, the far half a little lighter, a light midrib."""
    ca, sa = math.cos(ang), math.sin(ang)
    left, right = [], []
    n = 9
    for k in range(n + 1):
        t = k / n
        w = width * math.sin(math.pi * t ** 0.75) ** (0.8 / tip) * 0.5
        px, py = x + ca * length * t, y + sa * length * t
        left.append((px - sa * w, py + ca * w))
        right.append((px + sa * w, py - ca * w))
    mid = [(x + ca * length * k / n, y + sa * length * k / n) for k in range(n + 1)]
    draw.polygon(left + mid[::-1], fill=_col(v * 0.9, warm))            # the two halves: a soft fold along the rib
    draw.polygon(right + mid[::-1], fill=_col(v, warm))
    if rib:
        draw.line([mid[0], mid[-1]], fill=_col(min(v * 1.12, 1.0), warm + 0.3), width=max(1, int(width * 0.07)))


def _tile_canvas(w, h):
    return Image.new("RGBA", (w * K, h * K), (0, 0, 0, 0))


def _outline(rng):
    """Irregular outline of a clump: radius factor by angle (a few lobes), so a card never reads as a round rosette."""
    p1, p2, p3 = rng.uniform(0, 6.3, 3)
    return lambda a: 1.0 + 0.16 * math.sin(2 * a + p1) + 0.13 * math.sin(3 * a + p2) + 0.08 * math.sin(5 * a + p3)


def tile_broad(T, rng, n_leaves=460, reach=0.36, solid=False):
    """Clump of round leaves: three layers from dark (deep) to light (front); leaves grow outwards from twig ends
    scattered over a disc, so leaf tips poke out all round the outline."""
    img = _tile_canvas(T, T)
    d = ImageDraw.Draw(img)
    c = T * K / 2
    edge = _outline(rng)
    for layer, (share, v0, v1) in enumerate(((0.36, 0.42, 0.6), (0.34, 0.6, 0.82), (0.30, 0.8, 1.0))):
        for _ in range(int(n_leaves * share)):
            if solid:
                x, y = rng.uniform(-0.1, 1.1) * T * K, rng.uniform(-0.1, 1.1) * T * K
                ang = rng.uniform(0, 2 * math.pi)
            else:
                a = rng.uniform(0, 2 * math.pi)
                r = reach * edge(a) * T * K * rng.uniform(0, 1) ** 0.6 * (1.0 - 0.1 * layer) - T * K * 0.04
                x, y = c + r * math.cos(a), c + r * math.sin(a)
                ang = a + rng.normal(0, 1.25)                                  # loosely outwards
            length = T * K * rng.uniform(0.085, 0.125)
            _leaf(d, x, y, ang, length, length * rng.uniform(0.42, 0.56), rng.uniform(v0, v1), rng.normal(0, 0.3))
    return img


def tile_needle(T, rng, tufts=210, reach=0.35):
    """Tufts of pine needles: each tuft a fan of tapering needles round a twig end; dense in the middle, spiky outline."""
    img = _tile_canvas(T, T)
    d = ImageDraw.Draw(img)
    c = T * K / 2
    edge = _outline(rng)
    order = []
    for _ in range(tufts):
        a = rng.uniform(0, 2 * math.pi)
        r = reach * edge(a) * T * K * rng.uniform(0, 1) ** 0.6 - T * K * 0.04
        order.append((rng.uniform(0, 1), c + r * math.cos(a), c + r * math.sin(a), a + rng.normal(0, 0.9)))
    for depth, x, y, a in sorted(order):
        v = 0.42 + 0.58 * depth
        warm = rng.normal(0, 0.3)
        spread = rng.uniform(1.6, 2.8)
        n = int(rng.integers(13, 19))
        for k in range(n):
            ang = a + rng.normal(0, 0.25) + spread * (k / (n - 1) - 0.5)
            length = T * K * rng.uniform(0.075, 0.118)
            w = T * K * rng.uniform(0.010, 0.014)
            ca, sa = math.cos(ang), math.sin(ang)
            tipx, tipy = x + ca * length, y + sa * length
            d.polygon([(x - sa * w, y + ca * w), (x + sa * w, y - ca * w), (tipx, tipy)],
                      fill=_col(v * rng.uniform(0.84, 1.0), warm))
    return img


def tile_spray(T, rng, twigs=17):
    """Twigs with narrow leaves in pairs, growing out from the middle: a loose, feathery outline with small gaps."""
    img = _tile_canvas(T, T)
    d = ImageDraw.Draw(img)
    c = T * K / 2
    for layer in range(3):
        for k in range(twigs):
            a = 2 * math.pi * (k + rng.uniform(-0.3, 0.3)) / twigs + layer * 0.7
            length = T * K * rng.uniform(0.2, 0.37) * (1.0 - 0.1 * layer)
            bend = rng.normal(0, 0.35)
            x, y = c + rng.normal(0, 0.03) * T * K, c + rng.normal(0, 0.03) * T * K
            v_base = (0.45, 0.66, 0.86)[layer]
            n = int(length / (T * K * 0.03))
            pts = []
            for j in range(n + 1):
                t = j / n
                aa = a + bend * t
                x, y = x + math.cos(aa) * length / n, y + math.sin(aa) * length / n
                pts.append((x, y, aa))
            d.line([(p[0], p[1]) for p in pts], fill=_col(v_base * 0.62, 0.5), width=max(2, int(T * K * 0.006)))
            for j, (px, py, aa) in enumerate(pts[1:]):
                for side in (-1, 1):
                    la = aa + side * rng.uniform(0.55, 1.0)
                    ll = T * K * rng.uniform(0.07, 0.1) * (1.0 - 0.35 * j / n)
                    _leaf(d, px, py, la, ll, ll * rng.uniform(0.2, 0.27), v_base * rng.uniform(0.86, 1.14),
                          rng.normal(0, 0.4), tip=1.4)
            _leaf(d, x, y, aa, T * K * 0.08, T * K * 0.02, v_base * 1.1, 0.2, tip=1.4)
    return img


def tile_blades(W, H, rng, n=46):
    """A fan of long grass blades from the bottom centre: upright in the middle, arching over towards the sides."""
    img = _tile_canvas(W, H)
    d = ImageDraw.Draw(img)
    w, h = W * K, H * K
    blades = []
    for k in range(n):
        lean = rng.uniform(-1, 1)
        blades.append((rng.uniform(0, 1), lean))
    for depth, lean in sorted(blades):
        x0 = w / 2 + lean * w * 0.05 + rng.normal(0, w * 0.015)
        length = h * rng.uniform(0.6, 0.93) * (1.0 - 0.25 * abs(lean))
        droop = lean * rng.uniform(0.9, 1.5)
        width = w * rng.uniform(0.03, 0.05)
        v = 0.45 + 0.55 * depth
        warm = rng.normal(0.1, 0.45)
        left, right = [], []
        x, y, ang = x0, h * 0.985, -math.pi / 2 + lean * 0.22
        steps = 14
        for j in range(steps + 1):
            t = j / steps
            ww = width * (1.0 - t) ** 0.7 * 0.5 + 0.6
            left.append((x - math.sin(ang) * ww, y + math.cos(ang) * ww))
            right.append((x + math.sin(ang) * ww, y - math.cos(ang) * ww))
            ang += droop * 1.9 * t / steps * 2.0
            x, y = x + math.cos(ang) * length / steps, y + math.sin(ang) * length / steps
        d.polygon(left + right[::-1], fill=_col(v * rng.uniform(0.86, 1.0), warm))
    return img


def tile_plume(T, rng):
    """One feathery plume: a stalk from the bottom centre and a spindle of fine strands, fullest in the middle."""
    img = _tile_canvas(T, T)
    d = ImageDraw.Draw(img)
    s = T * K
    d.line([(s / 2, s * 0.98), (s / 2, s * 0.5)], fill=_col(0.7, 0.4), width=max(2, int(s * 0.02)))
    for k in range(150):
        t = rng.uniform(0, 1)
        y = s * (0.72 - 0.64 * t)
        half = s * 0.19 * math.sin(math.pi * min(t * 1.05 + 0.08, 1.0)) ** 0.8
        side = rng.choice((-1, 1))
        x1 = s / 2 + side * half * rng.uniform(0.35, 1.0)
        y1 = y - s * rng.uniform(0.05, 0.13)
        d.line([(s / 2 + rng.normal(0, s * 0.008), y), (x1, y1)], fill=_col(rng.uniform(0.72, 1.0), rng.normal(0.2, 0.3)),
               width=max(2, int(s * rng.uniform(0.012, 0.022))))
    return img


def _blur(a, sigma):
    """Gaussian blur of a 2D float array (separable, edges clamped)."""
    r = max(1, int(sigma * 3))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2).astype(np.float32)
    k /= k.sum()
    p = np.pad(a, ((r, r), (0, 0)), mode="edge")
    a = sum(k[i] * p[i:i + a.shape[0]] for i in range(2 * r + 1))
    p = np.pad(a, ((0, 0), (r, r)), mode="edge")
    return sum(k[i] * p[:, i:i + a.shape[1]] for i in range(2 * r + 1))


def _finish(img, w, h, pad=0.035, opaque=False):
    """Scales a tile down, clears the margin, softens the alpha edge and spreads the colour into the clear part."""
    img = img.resize((w, h), Image.LANCZOS)
    a = np.asarray(img, np.float32) / 255.0
    rgb, alpha = a[..., :3], a[..., 3]
    if not opaque:
        m = int(round(pad * min(w, h)))
        alpha[:m], alpha[-m:], alpha[:, :m], alpha[:, -m:] = 0, 0, 0, 0
    mask = (alpha > 0.5).astype(np.float32)
    filled = rgb * mask[..., None]
    weight = mask.copy()
    for sigma in (1.5, 3, 6, 12, 24, 48):                      # spread the leaf colour outwards, nearest first
        fb = np.stack([_blur(filled[..., c], sigma) for c in range(3)], -1)
        wb = _blur(weight, sigma)
        new = (weight < 1e-3) & (wb > 1e-3)
        filled[new] = fb[new] / wb[new][..., None]
        weight[new] = 1.0
    filled[weight < 1e-3] = filled[weight >= 1e-3].mean(axis=0) if (weight >= 1e-3).any() else 0.6
    if opaque:
        filled = filled * (0.45 + 0.55 * mask[..., None])     # between the leaves: the dark inside
        alpha = np.ones_like(alpha)
    else:
        soft = _blur(alpha, 0.9)
        alpha = np.clip(0.5 + (soft - 0.5) * 1.5, 0.0, 1.0)
    return np.dstack([filled, alpha])


def make_veg_leaves(seed=1811):
    rng = np.random.default_rng(seed)
    sheet = np.zeros((S, S, 4), np.float32)
    H = S // 2
    Q = S // 4

    def put(name, tile):
        u0, v0, u1, v1 = TILES[name]
        x0, x1, y0, y1 = int(u0 * S), int(u1 * S), int((1 - v1) * S), int((1 - v0) * S)
        sheet[y0:y1, x0:x1] = tile

    put("broad", _finish(tile_broad(H, rng), H, H))
    put("needle", _finish(tile_needle(H, rng), H, H))
    put("spray", _finish(tile_spray(H, rng), H, H))
    put("blades", _finish(tile_blades(Q, H, rng), Q, H, pad=0.02))
    put("solid", _finish(tile_broad(Q, rng, n_leaves=520, solid=True), Q, Q, opaque=True))
    put("plume", _finish(tile_plume(Q, rng), Q, Q, pad=0.02))
    os.makedirs(TEX_DIR, exist_ok=True)
    out = os.path.join(TEX_DIR, "veg_leaves_cutout_albedo.png")
    Image.fromarray((np.clip(sheet, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(out)
    cover = {n: float((sheet[int((1 - t[3]) * S):int((1 - t[1]) * S), int(t[0] * S):int(t[2] * S), 3] > 0.5).mean())
             for n, t in TILES.items()}
    print("veg_leaves_cutout_albedo.png", {n: round(c, 2) for n, c in cover.items()})
    return out


if __name__ == "__main__":
    make_veg_leaves()
