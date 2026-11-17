"""Plates of the realistic menu + HUD redesign ("Golden Hour", Role 4): smoked glass panels with a hairline edge,
a faint light line on top and a soft shadow under them, like the menus of modern racing games. They replace the
cartoon plates (ink outline + hard lip) with the SAME sizes and the same 9-slice cuts as UI/HudArt.uss, so every
panel of the HUD and the menus keeps its layout and only the look changes.
USS in Unity has no blur, gradients or box-shadow, so all of that is baked into these PNGs (2x, -unity-slice-scale .5).
Note: Editor/HudArtBuilder "Build All" would draw the old cartoon plates again: do not run it for these files.
Run from the repo root: python tools/ui_art/make_frontend_plates.py (Pillow)."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/_Project/RaceFlow/UI/Art"
SS = 4  # supersampling for smooth corners


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(4))


def plate(name, w, h, top, bottom, edge, radius, lip, light=(255, 255, 255, 26), shadow=110, accent=None):
    """w x h = picture size (2x). lip = px at the bottom kept for the soft shadow (the old plates' hard lip)."""
    W, H, R = w * SS, h * SS, radius * SS
    body = (2 * SS, 2 * SS, W - 2 * SS, H - lip * SS)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # Soft shadow under the panel (baked: USS has no box-shadow).
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle((body[0] + SS, body[1] + 4 * SS, body[2] - SS, body[3] + 3 * SS), R, fill=(0, 0, 0, shadow))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(3 * SS)))
    # Body: vertical gradient inside the rounded box.
    grad = Image.new("RGBA", (W, H))
    gd = ImageDraw.Draw(grad)
    for y in range(H):
        t = min(1, max(0, (y - body[1]) / max(1, body[3] - body[1])))
        gd.line([(0, y), (W, y)], fill=lerp(top, bottom, t))
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle(body, R, fill=255)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    layer.paste(grad, (0, 0), mask)
    # Hairline edge and the faint light line along the top.
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle(body, R, outline=edge, width=3 * SS if accent else 2 * SS)
    d.line([(body[0] + R, body[1] + 2 * SS), (body[2] - R, body[1] + 2 * SS)], fill=light, width=SS * 2)
    img.alpha_composite(layer)
    img = img.resize((w, h), Image.LANCZOS)
    img.save(ART / f"{name}.png", optimize=True)


GLASS_T, GLASS_B = (24, 28, 35, 214), (8, 10, 14, 224)
EDGE = (255, 255, 255, 46)
# 96 x 104 plates (slices 36/36/36/48) and 64 x 72 chips (26/26/26/34), as in UI/HudArt.uss.
plate("plate-navy", 96, 104, GLASS_T, GLASS_B, EDGE, 10, 12)
plate("plate-map", 96, 104, (20, 24, 30, 150), (8, 10, 14, 165), (255, 255, 255, 36), 10, 12)
plate("plate-blue", 96, 104, (46, 56, 68, 228), (20, 26, 34, 232), (255, 255, 255, 110), 10, 12, light=(255, 255, 255, 50))
plate("plate-orange", 96, 104, (255, 128, 64, 255), (232, 82, 26, 255), (255, 196, 160, 120), 10, 12, light=(255, 255, 255, 90))
plate("plate-sand", 96, 104, (246, 243, 236, 240), (222, 218, 210, 240), (255, 255, 255, 160), 10, 12, light=(255, 255, 255, 140))
plate("plate-navy-chip", 64, 72, GLASS_T, GLASS_B, EDGE, 8, 8)
plate("plate-cream", 64, 72, (246, 243, 236, 242), (222, 218, 210, 242), (255, 255, 255, 170), 8, 8, light=(255, 255, 255, 150))
plate("plate-coral", 64, 72, (226, 72, 60, 240), (178, 40, 36, 240), (255, 170, 160, 110), 8, 8, light=(255, 255, 255, 70))
plate("plate-mint", 64, 72, (52, 196, 128, 240), (28, 140, 88, 240), (190, 255, 220, 110), 8, 8, light=(255, 255, 255, 70))
plate("plate-orange-row", 64, 68, (255, 128, 64, 255), (232, 82, 26, 255), (255, 196, 160, 120), 7, 6, light=(255, 255, 255, 90))
# New: the selected entry of a menu (glass with an orange edge), used by UI/FrontEnd.uss.
plate("plate-select", 96, 104, (54, 40, 34, 232), (22, 16, 14, 236), (255, 110, 50, 255), 10, 12, light=(255, 170, 120, 70), accent=True)

# Cinematic light over the live 3D world behind the start screen and the menus (stretched, so tiny pictures).
def ramp(name, size, stops, horizontal):
    w, h = size
    img = Image.new("RGBA", size)
    d = ImageDraw.Draw(img)
    n = w if horizontal else h
    for i in range(n):
        t = i / (n - 1)
        for (t0, a0), (t1, a1) in zip(stops, stops[1:]):
            if t0 <= t <= t1:
                u = (t - t0) / (t1 - t0)
                u = u * u * (3 - 2 * u)
                a = round(a0 + (a1 - a0) * u)
                break
        c = (6, 8, 12, a)
        d.line([(i, 0), (i, h)] if horizontal else [(0, i), (w, i)], fill=c)
    img.save(ART / f"{name}.png", optimize=True)


ramp("shade-left", (256, 4), [(0, 205), (0.38, 150), (0.72, 0), (1, 0)], True)
ramp("shade-bottom", (4, 256), [(0, 0), (0.55, 40), (1, 200)], False)
ramp("shade-top", (4, 128), [(0, 120), (1, 0)], False)
print("plates written to", ART)
