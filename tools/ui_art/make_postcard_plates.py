"""Makes the "Postcard" menu plates (menu redesign, Role 4): PNGs for UI/Art, shown 9-sliced by UI/HudArt.uss.

Same frame as the R15 plates (96 x 104 px at 2x, slices 36/36/36/48, hard shadow baked in), but with a thick ink
outline like the cartoon logo. Run: python tools/ui_art/make_postcard_plates.py  (needs Pillow). Overwrites the files.
"""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[2] / "Assets/_Project/RaceFlow/UI/Art"
SS = 4  # supersampling

INK = (14, 34, 56)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def gradient(w, h, stops):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        t = y / max(1, h - 1)
        for i in range(len(stops) - 1):
            if stops[i][0] <= t <= stops[i + 1][0]:
                u = (t - stops[i][0]) / max(1e-6, stops[i + 1][0] - stops[i][0])
                c = lerp(stops[i][1], stops[i + 1][1], u)
                break
        for x in range(w):
            px[x, y] = c + (255,)
    return img


def plate(name, w, h, stops, radius=28, outline=6, lip=12, lip_color=INK + (235,), shine=(255, 255, 255, 140)):
    W, H = w * SS, h * SS
    r, o, l = radius * SS, outline * SS, lip * SS
    body_h = H - l
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, l, W - 1, H - 1), r, fill=lip_color)          # hard shadow / lip
    d.rounded_rectangle((0, 0, W - 1, body_h - 1), r, fill=INK + (255,))  # ink outline
    fill = gradient(W - 2 * o, body_h - 2 * o, stops)
    mask = Image.new("L", fill.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, fill.size[0] - 1, fill.size[1] - 1), r - o, fill=255)
    img.paste(fill, (o, o), mask)
    # a thin bright line under the top edge (bevel)
    shine_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(shine_layer).rounded_rectangle((o + 3 * SS, o + 2 * SS, W - o - 3 * SS, body_h - o - 2 * SS), r - o - 3 * SS,
                                                  outline=shine, width=2 * SS)
    cut = Image.new("L", (W, H), 0)
    ImageDraw.Draw(cut).rectangle((0, 0, W, o + 2 * SS + r // 2), fill=255)
    img = Image.alpha_composite(img, Image.composite(shine_layer, Image.new("RGBA", (W, H)), cut))
    img = img.resize((w, h), Image.LANCZOS)
    img.save(OUT / f"{name}.png", optimize=True)
    print("wrote", name)


if __name__ == "__main__":
    # Title ribbons on every menu card: sunset, sun yellow on top to coral at the bottom.
    plate("plate-sunset", 96, 104, [(0, (255, 214, 102)), (0.45, (255, 150, 64)), (1, (238, 84, 76))])
    # Main buttons (PRESS ANY KEY, RETRY, START RACE, CLOSE): sun yellow, like the click-to-play button on the web page.
    plate("plate-sun", 96, 104, [(0, (255, 232, 132)), (0.55, (255, 184, 64)), (1, (255, 150, 40))])
    # Small buttons and pills (settings, credits, sound, noon/sunset): cream paper.
    plate("plate-paper", 96, 104, [(0, (255, 250, 238)), (1, (240, 226, 196))], shine=(255, 255, 255, 0))
    # The selected mode card and selected rows: azulejo blue.
    plate("plate-azul", 96, 104, [(0, (64, 150, 240)), (1, (27, 89, 184))], shine=(190, 225, 255, 170))
    # Unselected cards: navy, ink outline.
    plate("plate-ink", 96, 104, [(0, (31, 72, 128)), (1, (19, 46, 85))], shine=(140, 180, 240, 110))
