"""Draws the checkpoint banner picture (task V13) -> Assets/_Project/Visuals/CheckpointGates/CheckpointBanner.png

A tall feather banner: cream cloth, the text standing on its side (read from bottom to top, like on real event
flags), two gold chevrons under it and a gold band along the free edge (u = 1, away from the pole).
Font: Unbounded Black from the HUD (SIL OFL 1.1, Assets/_Project/RaceFlow/UI/Fonts/Unbounded/, in THIRD_PARTY.md).
Change TEXT and run again, then rebuild the campus world:   python3 tools/textures/make_checkpoint_banner.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

TEXT = "NOVA KART"
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
FONT = os.path.join(REPO, "Assets/_Project/RaceFlow/UI/Fonts/Unbounded/Unbounded-Black.ttf")
OUT = os.path.join(REPO, "Assets/_Project/Visuals/CheckpointGates/CheckpointBanner.png")
W, H = 256, 1024          # final picture: u across the cloth (left = pole side), v up
BAND = 34                 # gold band along the free edge
CLOTH = (244, 239, 227)
INK = (30, 36, 48)
GOLD = (222, 160, 38)


def main():
    # The text part is drawn lying down (H wide, W - BAND high) and then turned 90 degrees left,
    # so the text reads from the bottom of the flag to the top.
    lying = Image.new("RGB", (H, W - BAND), CLOTH)
    draw = ImageDraw.Draw(lying)
    size = 150
    while True:
        font = ImageFont.truetype(FONT, size)
        box = draw.textbbox((0, 0), TEXT, font=font)
        if (box[2] - box[0] <= H - 260 and box[3] - box[1] <= W - BAND - 60) or size <= 40:
            break
        size -= 4
    text_w, text_h = box[2] - box[0], box[3] - box[1]
    draw.text((170 + (H - 260 - text_w) // 2, (W - BAND - text_h) // 2 - box[1]), TEXT, font=font, fill=INK)
    mid = (W - BAND) // 2
    for k in range(2):                                            # two chevrons, pointing up the flag
        cx = 70 + k * 40
        draw.polygon([(cx, mid - 48), (cx + 30, mid), (cx, mid + 48), (cx - 16, mid + 48), (cx + 14, mid),
                      (cx - 16, mid - 48)], fill=GOLD)

    flag = Image.new("RGB", (W, H), GOLD)                         # gold shows only as the band on the right
    flag.paste(lying.rotate(90, expand=True), (0, 0))
    flag.save(OUT)
    print("written:", os.path.relpath(OUT, REPO), "| text:", TEXT, "| font size", size)


if __name__ == "__main__":
    main()
