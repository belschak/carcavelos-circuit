"""Contact sheet of the drive-along frames: thumbnails in lap order with the metre mark and zone.

Run:  ~/Documents/nova/.media-tools/bin/python contact_sheet.py out/qa_r1/drive [columns] [thumb_width]
Writes <dir>/contact_sheet_<n>.jpg (24 frames per sheet).
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

folder = sys.argv[1]
cols = int(sys.argv[2]) if len(sys.argv) > 2 else 4
tw = int(sys.argv[3]) if len(sys.argv) > 3 else 480
index = json.load(open(os.path.join(folder, "drive_index.json")))["frames"]
per_sheet = cols * 6
try:
    font = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 18)
except OSError:
    font = ImageFont.load_default()
for n in range(0, len(index), per_sheet):
    chunk = index[n:n + per_sheet]
    th = round(tw * 9 / 16)
    rows = (len(chunk) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (tw + 8) + 8, rows * (th + 32) + 8), (24, 24, 26))
    d = ImageDraw.Draw(sheet)
    for k, f in enumerate(chunk):
        x, y = 8 + (k % cols) * (tw + 8), 8 + (k // cols) * (th + 32)
        img = Image.open(os.path.join(folder, f["file"])).convert("RGB").resize((tw, th), Image.LANCZOS)
        sheet.paste(img, (x, y + 26))
        d.text((x, y + 2), f"s={f['s']} m  {f['zone']}  {f['surface']}", fill=(235, 235, 235), font=font)
    out = os.path.join(folder, f"contact_sheet_{n // per_sheet + 1}.jpg")
    sheet.save(out, quality=88)
    print(out)
