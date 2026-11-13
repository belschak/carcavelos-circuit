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
  north_buff_albedo/normal  V18j: buff brushed concrete (apron along the hall's north face, floor at Student Central),
                            panels of 3 x 3 m on a 6 m tile, saw cuts, rain stains, 1024 px
  north_grating_albedo/normal  V18j: galvanised steel bars (drain gratings in the north street, the louvre screen), 256 px
  north_basalt_albedo/normal   V20d: the forecourt ring's band of dark basalt cubes, ONE stone wide, 1.5 x 0.14 m, 1024 x 96 px
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


BUFF = "#B3AB95"             # V18j: buff brushed concrete of the apron along the hall's north face (survey: #b3b0a4 overcast)
GALV = "#8C908C"             # V18j: galvanised steel of the drain gratings and the louvre screen (survey: #8c908c)


def make_buff(S_=1024, seed=1405):
    """V18j: the buff in-situ concrete of the apron along the hall's north face, of the drip-line strip at the
    north-east face and of the floor at Student Central. A tile of 6 x 6 m (u along the facade): four panels of
    3.0 x 1.5 m per row would be too busy, so two rows of 3 m with saw cuts every 3 m along u, offset by half a
    panel from row to row; brushed across the facade (fine streaks along v), a tone per panel, darker rain stains
    along the saw cuts, a few rust-brown runs, sand specks. Normal map: 3 mm saw cuts and the brushing."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S_, S_)
    rows, cols = 2, 2
    rh, cw = S_ / rows, S_ / cols
    r = np.minimum((Y / rh).astype(np.int64), rows - 1)
    xo = (X + (r % 2) * cw / 2) % S_
    c = np.minimum((xo / cw).astype(np.int64), cols - 1)
    fy, fx = Y / rh - r, xo / cw - c
    d = np.minimum(np.minimum(fy, 1 - fy) * rh, np.minimum(fx, 1 - fx) * cw)          # px to the nearest saw cut
    pid = r * cols + c
    tone = rng.normal(1.0, 0.022, rows * cols)[pid]
    warm = rng.uniform(-1, 1, rows * cols)[pid]
    base = rgb(BUFF)
    panel = np.where(warm[..., None] >= 0, lerp(base, rgb("#BDB096"), warm[..., None] * 0.6),
                     lerp(base, rgb("#ACA89A"), -warm[..., None] * 0.6))
    brush = noise((S_, S_), 0.8, rng, 30.0)                              # brushed across the facade: streaks along v
    cloud = noise((S_, S_), 0.10 * S_, rng)
    mott = noise((S_, S_), 9.0, rng)
    panel = panel * (tone * (1.0 + 0.030 * brush + 0.035 * cloud + 0.022 * mott + 0.012 * noise((S_, S_), 0.7, rng)))[..., None]
    stain = smoothstep(0.35, 1.3, noise((S_, S_), 22.0, rng, 60.0))       # rain runs, long across the strip
    panel = lerp(panel, panel * rgb("#8F8674")[None, None, :] / base[None, None, :], (0.22 * stain)[..., None])
    edge = 1.0 - smoothstep(2.0, 26.0, d)                                 # dirt gathered along the saw cuts
    panel = panel * (1.0 - 0.07 * edge * (0.6 + 0.4 * mott))[..., None]
    spk = smoothstep(1.5, 2.4, noise((S_, S_), 0.6, rng))                  # dark sand specks
    panel = lerp(panel, rgb("#7E7868")[None, None, :], (0.35 * spk)[..., None])
    cov = smoothstep(0.6, 1.7, d)
    joint = rgb("#6F6A5E")[None, None, :] * (1.0 + 0.08 * noise((S_, S_), 1.0, rng))[..., None]
    alb = lerp(joint, panel, cov[..., None])
    alb = T.match_mean(alb, BUFF, 1.0)
    save("north_buff_albedo.png", alb)
    height = cov * (0.6 + (tone - 1.0) * 1.5) + 0.030 * brush + 0.010 * mott
    save("north_buff_normal.png", T.normal_map(height * 6.0, 1.0), dither=False)


def make_grating(W=256, H=256, seed=1406):
    """V18j: galvanised steel grating / louvre blades. u (x) runs along the bars' pitch: 8 flat bars per tile with
    dark gaps between them; v (y) along the bars, with one cross bar per tile and a frame line at both v edges of a
    drain (the mesh maps v 0..1 across the drain's width). Zinc spangle, a little white rust and dirt in the gaps.
    Used across the north street's setts (tile 0.4 m along the run), on the apron's grating strip and, with the
    bars lying horizontally (tile 1.0 m), as the grey louvre screen on the hall's north face."""
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    bars = 8
    f = (X / W * bars) % 1.0
    k = np.floor(X / W * bars).astype(np.int64) % bars
    gap = 1.0 - smoothstep(0.16, 0.24, f) * (1.0 - smoothstep(0.76, 0.84, f))          # 1 in the slot between two bars
    v = Y / H
    cross = 1.0 - smoothstep(0.035, 0.06, np.abs(v - 0.5))                              # the cross bar in the middle
    frame = 1.0 - smoothstep(0.045, 0.075, np.minimum(v, 1 - v))                        # the frame along the run
    steel = np.maximum(np.maximum(1.0 - gap, cross), frame)
    spangle = noise((H, W), 2.2, rng)
    tone = rng.normal(1.0, 0.03, bars)[k]
    zinc = rgb(GALV)[None, None, :] * (tone * (1.0 + 0.07 * spangle + 0.03 * noise((H, W), 0.7, rng)))[..., None]
    shine = smoothstep(0.42, 0.5, f) * (1.0 - smoothstep(0.5, 0.58, f)) * (1.0 - np.maximum(cross, frame))   # bar crown
    zinc = zinc * (1.0 + 0.10 * shine)[..., None]
    white = smoothstep(0.9, 1.8, noise((H, W), 5.0, rng))                                # white rust blooms
    zinc = lerp(zinc, rgb("#B9BDB8")[None, None, :], (0.35 * white)[..., None])
    rust = smoothstep(1.3, 2.2, noise((H, W), 3.0, rng, 9.0))
    zinc = lerp(zinc, rgb("#7A5A44")[None, None, :], (0.30 * rust)[..., None])
    dark = rgb("#1E2120")[None, None, :] * (1.0 + 0.25 * noise((H, W), 4.0, rng))[..., None]
    alb = lerp(dark, zinc, steel[..., None])
    alb = lerp(alb, alb * 0.8, (0.6 * frame * (1.0 - smoothstep(0.0, 0.03, np.minimum(v, 1 - v))))[..., None])
    save("north_grating_albedo.png", alb)
    save("north_grating_normal.png", T.normal_map(blur_safe(steel) * 9.0, 1.0), dither=False)


def blur_safe(a):
    """A slightly softened copy (wrapping), so the bars' edges give a clean bevel in the normal map."""
    return (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.0


BASALT = "#4A4B4E"           # V20d: dark basalt cubes of the forecourt's ring (Street View N02, Jun 2019: blue-grey)
BASALT_JOINT = "#A39A86"     # ... set in the pale calcada's sand joints


def make_basalt(W=1024, H=96, seed=1407):
    """V20d: the thin band of dark basalt cubes in the forecourt's pale setts (env_north.RING_BAND, 0.14 m), ONE
    stone wide, as in Portuguese calcada: u (x) runs along the band (a tile of 1.5 m), v (y) across it (0.14 m).
    Stones 8.5-12.5 cm long and 9.5-12 cm across, each set a little off the band's axis, hand-cut edges, a tone of
    their own (blue-grey to warm grey, never black), worn tops a step lighter; pale sand joints of 6-9 mm between
    them and along both edges, so the band meets the pale setts beside it without a hard line."""
    rng = np.random.default_rng(seed)
    X, Y = grid(H, W)
    px_m = W / 1.5                                                     # pixels per metre (both axes: 96 px = 0.14 m)
    cuts, x = [], 0.0
    while x < W - 0.06 * px_m:
        cuts.append(x)
        x += rng.uniform(0.085, 0.125) * px_m
    cuts.append(float(W))                                              # the tile wraps on a joint
    cuts = np.array(cuts)
    n = len(cuts) - 1
    k = np.clip(np.searchsorted(cuts, X, side="right") - 1, 0, n - 1)
    left, right = cuts[k], cuts[k + 1]
    half_w = rng.uniform(0.0475, 0.06, n)[k] * px_m                    # half the stone's width across the band
    off = rng.normal(0.0, 0.004, n)[k] * px_m                          # off the band's axis
    skew = rng.normal(0.0, 0.06, n)[k]                                 # a slightly turned stone
    cx = (left + right) / 2.0
    vy = (Y - H / 2.0 - off) - skew * (X - cx)
    d_end = np.minimum(X - left, right - X)                            # px to the butt joints
    d_side = half_w - np.abs(vy)                                       # px to the stone's long edges
    d = np.minimum(d_end, d_side) + 0.9 * noise((H, W), 2.5, rng) + 0.4 * noise((H, W), 0.9, rng)   # hand-cut
    jh = rng.uniform(2.0, 3.1, n)[k]                                   # half joint: 3-4.5 mm
    cov = smoothstep(jh - 0.8, jh + 0.8, d)                            # 1 = stone
    inner = np.maximum(d - jh, 0.0)
    warm = rng.uniform(-1, 1, n)[k][..., None]
    base = rgb(BASALT)
    stone = np.where(warm >= 0, lerp(base, rgb("#57544F"), warm * 0.6), lerp(base, rgb("#3F4349"), -warm * 0.6))
    bright = np.clip(rng.normal(1.0, 0.07, n), 0.85, 1.18)[k]
    grain = 1.0 + 0.06 * noise((H, W), 0.8, rng) + 0.04 * noise((H, W), 2.5, rng)            # vesicles and grain
    polish = 1.0 + 0.10 * smoothstep(3.0, 18.0, inner)                                        # walked-smooth tops
    stone = stone * (bright * grain * polish)[..., None]
    joint = rgb(BASALT_JOINT)[None, None, :] * (1.0 + 0.10 * noise((H, W), 1.0, rng) + 0.05 * noise((H, W), 4.0, rng))[..., None]
    joint = joint * (0.80 + 0.20 * smoothstep(0.0, 3.0, d + jh))[..., None]                  # darker deep in the joint
    alb = lerp(joint, stone, cov[..., None])
    save("north_basalt_albedo.png", alb)
    tilt_a = rng.uniform(0, 2 * np.pi, n)[k]
    tilt_s = rng.uniform(0.0, 0.12, n)[k]
    dome = 1.0 - (1.0 - smoothstep(0.0, 14.0, inner)) ** 2
    fu = (X - cx) / np.maximum(right - left, 1.0) * 2.0
    fv = vy / np.maximum(half_w, 1.0)
    height = lerp(0.05 + 0.04 * noise((H, W), 1.0, rng),
                  0.55 + 0.40 * dome + tilt_s * (np.cos(tilt_a) * fu + np.sin(tilt_a) * fv) + 0.02 * noise((H, W), 1.2, rng), cov)
    save("north_basalt_normal.png", T.normal_map(height * 12.0, 1.0), dither=False)


BUILDERS = {"entrance_calcada": make_calcada, "entrance_drain": make_drain, "entrance_planks": make_planks,
            "entrance_edging": make_edging, "north_buff": make_buff, "north_grating": make_grating,
            "north_basalt": make_basalt}


def main(argv=()):
    for name in argv or list(BUILDERS):
        t0 = time.time()
        BUILDERS[name]()
        print(f"{name:18s} {time.time() - t0:5.1f} s")


if __name__ == "__main__":
    main(sys.argv[1:])
