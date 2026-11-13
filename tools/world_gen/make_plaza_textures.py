"""Procedural textures of the upper plaza (V10i, 2026-11-11; V16: new look from the photosphere survey V15), made with
the toolkit of make_textures.py.

Run after make_textures.py and make_street_textures.py:  python3 make_plaza_textures.py
Output in textures/ (no photos; colours are the survey's averages):
  plaza_ochre_albedo       pale sand in-situ concrete, ONE bay of 5 m per tile: a thin dark saw cut on the tile's border,
                           fine aggregate, a few hairline cracks (V10i: 3 m slabs, darker and more orange)
  skylight_glass_albedo    frosted glass, light blue (Luc's decision: the hue stays), ONE pane per tile with half a grey
                           glazing bar on each border and a fine dot texture; env_plaza lays the tiles pane by pane
  plaza_terracotta_albedo  pale salmon terracotta of the stairs, one tread deep per tile: a soft dark line on the
                           tile's top and bottom border (nosing and foot of the riser), faint joints on the sides
"""
import numpy as np

import make_textures as T

rgb, noise, blur, smoothstep, lerp, grid, save = T.rgb, T.noise, T.blur, T.smoothstep, T.lerp, T.grid, T.save

OCHRE = "#C9B388"            # survey V15: #bea372 (E2's set) .. #d2c29c (E1's set); V10i: #E2BF88
GLASS = "#9FBDC2"            # unchanged (Luc: the skylights stay light blue)
BAR = "#A7ABA8"              # glazing bars and steel edge (survey: #9a9e9a, E14_h000_t60; a little brighter)
TERRACOTTA = "#DBC3A6"       # V18g: pale peach stone, lighter than the plaza (orthophoto 244/219/182 against the plaza's
#                              216/198/149; walk 7 Nov, overcast: #c9bcaa against #b9b293). V18: #C6AA93 (team walk); survey V15
#                              section 4, item 2: #B08A72; V10i: the track's brick red #C8664A


def make_ochre(S=1024, seed=3101):
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    px_m = S / 5.0
    clouds = 0.6 * noise((S, S), 0.45 * px_m, rng) + 0.4 * noise((S, S), 0.12 * px_m, rng)
    v = 0.016 * clouds + 0.010 * noise((S, S), 0.03 * px_m, rng) + 0.010 * noise((S, S), 1.1, rng)   # even, fine aggregate
    alb = rgb(OCHRE)[None, None, :] * (1.0 + v)[..., None]
    alb += 0.004 * noise((S, S), 0.5 * px_m, rng)[..., None] * np.array([1.0, 0.2, -0.8])
    crack = np.exp(-(noise((S, S), 0.22 * px_m, rng) / 0.010) ** 2) * smoothstep(0.6, 1.5, noise((S, S), 0.7 * px_m, rng))
    alb *= (1.0 - 0.10 * blur(crack, 0.7))[..., None]                         # a few hairlines across the bay
    d = np.minimum(np.minimum(X, S - X), np.minimum(Y, S - Y))
    line = smoothstep(3.6, 1.4, d)                                            # saw cut: about 2.5 cm when the tile repeats
    halo = 1.0 - smoothstep(2.0, 18.0, d)                                     # (the real 1-2 cm; a soft halo keeps it
    alb = alb * (1.0 - 0.30 * line - 0.045 * halo)[..., None]                 # readable in the mipmaps at chase distance)
    alb = T.match_mean(alb, OCHRE, 0.9)
    save("plaza_ochre_albedo.png", alb)


def make_glass(S=256, seed=3102):
    """One pane (about 1.3 x 1.8 m). The bar is 4.5 cm on each border = 9 cm between two panes."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    v = 0.022 * noise((S, S), 0.3 * S, rng) + 0.010 * noise((S, S), 2.0, rng)
    alb = rgb(GLASS)[None, None, :] * (1.0 + v)[..., None]
    dots = 0.5 + 0.5 * np.cos(X * TAU * 32 / S) * np.cos(Y * TAU * 44 / S)   # fine dot frit, 32 x 44 per pane
    alb *= (0.985 + 0.03 * dots)[..., None]
    ex, ey = np.minimum(X, S - X) / S, np.minimum(Y, S - Y) / S              # 0 on the border .. 0.5 in the middle
    edge = np.minimum(ex / 0.034, ey / 0.025)                                 # 1 = the bar's edge (4.5 cm on a 1.3 x 1.8 m pane)
    alb *= (0.955 + 0.06 * smoothstep(1.0, 6.0, edge))[..., None]             # frost: lighter towards the middle
    bar = smoothstep(1.15, 0.85, edge)
    alb = lerp(alb, rgb(BAR)[None, None, :] * np.ones((S, S, 1)), bar[..., None])
    seal = smoothstep(0.22, 0.05, edge)                                       # dark joint in the middle of the bar
    alb = alb * (1.0 - 0.35 * seal)[..., None]
    save("skylight_glass_albedo.png", alb)


def make_terracotta(S=512, seed=3103):
    """1.5 m across x one tread deep (about 1.1 m)."""
    rng = np.random.default_rng(seed)
    X, Y = grid(S, S)
    v = (0.020 * noise((S, S), 40, rng) + 0.012 * noise((S, S), 9, rng) + 0.010 * noise((S, S), 2.2, rng)
         + 0.008 * noise((S, S), 0.8, rng))
    alb = rgb(TERRACOTTA)[None, None, :] * (1.0 + v)[..., None]
    alb += 0.006 * noise((S, S), 60, rng)[..., None] * np.array([0.6, -0.3, -0.8])
    dy = np.minimum(Y, S - Y)
    alb *= (1.0 - 0.30 * smoothstep(7.0, 1.5, dy) - 0.05 * (1.0 - smoothstep(2.0, 40.0, dy)))[..., None]   # nosing / riser foot
    dx = np.minimum(X, S - X)
    alb *= (1.0 - 0.10 * smoothstep(2.2, 0.8, dx))[..., None]                 # faint joint every 1.5 m
    save("plaza_terracotta_albedo.png", alb)


TAU = T.TAU

if __name__ == "__main__":
    make_ochre()
    make_glass()
    make_terracotta()
