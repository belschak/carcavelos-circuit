"""Draws the four trackside thank-you boards (task P4, teacher tribute) as one picture
-> Assets/_Project/RaceFlow/Tribute/Resources/TributeBoards.png

One 1024 x 1024 picture, four boards of 1024 x 256 stacked from the top: board 0 on top, board 3 at the bottom.
TributeBoards.cs shows one quarter of it on each board (TributeRules.BoardUv). The order of the names must match
TributeRules.Teachers. Names only, no photos (David, 18 Nov 2026).
Look: the Golden Hour menu style, a dark ink plate, an orange streak, the course in sand, the name in cream.
Fonts from the HUD (SIL OFL 1.1, Assets/_Project/RaceFlow/UI/Fonts/, in THIRD_PARTY.md).
Change a name and run again:   python tools/textures/make_tribute_boards.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

NAMES = ["RICARDO JOÃO GIL PEREIRA", "CLÁUDIO VIEIRA", "LUÍS CARVALHO", "PAULO MARQUES"]
KICKER = "THANK YOU  ·  INTRODUCTION TO PROGRAMMING 2026  ·  NOVA SBE"

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
FONTS = os.path.join(REPO, "Assets/_Project/RaceFlow/UI/Fonts")
DISPLAY = os.path.join(FONTS, "ArchivoExpanded/ArchivoExpanded-ExtraBoldItalic.ttf")
UI = os.path.join(FONTS, "BarlowCondensed/BarlowCondensed-ExtraBold.ttf")
OUT = os.path.join(REPO, "Assets/_Project/RaceFlow/Tribute/Resources/TributeBoards.png")

W, ROW = 1024, 256
INK = (14, 34, 56)
PLATE = (19, 49, 79)
CREAM = (255, 246, 227)
SAND = (227, 196, 141)
ORANGE = (255, 107, 44)
SUN = (255, 201, 60)


def fit(path, text, max_w, start):
    size = start
    while size > 20:
        font = ImageFont.truetype(path, size)
        if font.getlength(text) <= max_w:
            return font
        size -= 2
    return ImageFont.truetype(path, size)


def board(draw, top, name):
    draw.rectangle([0, top, W - 1, top + ROW - 1], fill=INK)
    draw.rectangle([10, top + 10, W - 11, top + ROW - 11], fill=PLATE)
    # The orange streak on the left, slanted like the italic display font, and a thin sun line at the bottom.
    draw.polygon([(40, top + 196), (78, top + 60), (106, top + 60), (68, top + 196)], fill=ORANGE)
    draw.polygon([(84, top + 196), (122, top + 60), (136, top + 60), (98, top + 196)], fill=SUN)
    draw.rectangle([150, top + 214, W - 40, top + 218], fill=SUN)

    left, right = 160, W - 48
    kicker = fit(UI, KICKER, right - left, 38)
    draw.text((left, top + 36), KICKER, font=kicker, fill=SAND)
    big = fit(DISPLAY, name, right - left, 96)
    box = draw.textbbox((0, 0), name, font=big)
    name_h = box[3] - box[1]
    draw.text((left, top + 140 - name_h // 2 - box[1]), name, font=big, fill=CREAM)


def main():
    picture = Image.new("RGB", (W, ROW * len(NAMES)), INK)
    draw = ImageDraw.Draw(picture)
    for i, name in enumerate(NAMES):
        board(draw, i * ROW, name)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    picture.save(OUT, optimize=True)
    print("written:", os.path.relpath(OUT, REPO), picture.size)


if __name__ == "__main__":
    main()
