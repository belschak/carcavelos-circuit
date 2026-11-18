"""Logo mark, browser tab icons, home screen icons and the link preview card of the Web page (share preview, 18.11).

The mark: an italic C (the C of Carcavelos) that frames the sun setting on the sea, on the orange streak of the
wordmark. Same "Golden Hour" look as the menus: warm dark ground, cream letter, orange accent, no cartoon.
Two versions of the same drawing: a flat one (tab icon, readable at 16 px) and a photo one for the home screen,
where the ground is our own key art (keyart-2.jpg, the sunset over Carcavelos beach from the build's photo mode
?photo=1) with the real sea horizon on the mark's horizon.
The card (1200 x 630, og:image / twitter:image) is the same key art with the game's wordmark, drawn with the repo's
own fonts (Archivo Expanded + Barlow Condensed, SIL OFL 1.1).

Writes into Assets/WebGLTemplates/CarcavelosCircuit/TemplateData:
  favicon.svg, favicon.ico (16 + 32 + 48), favicon-16/32/48.png   browser tab
  icon-180.png                                                    iPhone home screen (apple-touch-icon, iOS rounds it)
  icon-192.png, icon-512.png                                      Android / PWA "any"
  icon-maskable-192.png, icon-maskable-512.png                    Android "maskable" (mark inside the safe circle)
  og-card.jpg                                                     link preview, kept under 300 KB
Needs Python Playwright with Microsoft Edge (like export_wordmark.py) and Pillow.
Run from the repo root: python tools/ui_art/make_brand_icons.py   (add --sheet to also write a contact sheet)
"""
import io
import math
import sys
from pathlib import Path

from PIL import Image
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/WebGLTemplates/CarcavelosCircuit/TemplateData"
FONTS = ROOT / "Assets/_Project/RaceFlow/UI/Fonts"
KEYART = DATA / "keyart-2.jpg"  # 1600 x 900, sunset over the Atlantic; sea horizon at y 178, sun reflection at x 800
CARD_ART = DATA / "keyart-2.jpg"
CARD_MAX_BYTES = 300 * 1024

CREAM = ("#FFFFFF", "#F0E4D2")
ORANGE = ("#FF5A1F", "#FF8A3D", "#FFA860")


def mark_svg(small=False, ground=True, size_attr=""):
    """The mark in a 100 x 100 box. small = thicker letter and bigger sun for 16-32 px. ground = the flat tile."""
    r, stroke = (26, 17) if small else (27, 14.5)
    cx, cy = 45, 50
    gap = math.radians(44 if small else 40)  # half the opening of the C, on the right
    x = cx + r * math.cos(gap)
    y1, y2 = cy + r * math.sin(gap), cy - r * math.sin(gap)
    sun_x, horizon, sun_r = 71, 55, (13 if small else 11)
    reflections = "" if small or not ground else (
        f'<g fill="#FF9A4D"><rect x="{sun_x - 9}" y="{horizon + 6}" width="18" height="2.2" rx="1.1" opacity=".75"/>'
        f'<rect x="{sun_x - 5.5}" y="{horizon + 10.5}" width="11" height="2" rx="1" opacity=".5"/>'
        f'<rect x="{sun_x - 3}" y="{horizon + 14.5}" width="6" height="1.8" rx=".9" opacity=".3"/></g>')
    tile = ('<rect width="100" height="100" rx="22" fill="url(#bg)"/>'
            '<rect width="100" height="100" rx="22" fill="url(#glow)"/>') if ground else ""
    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"{size_attr}>
<defs>
<linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#13161D"/><stop offset=".6" stop-color="#2A160E"/><stop offset="1" stop-color="#5E2A12"/></linearGradient>
<radialGradient id="glow" cx=".72" cy=".56" r=".6"><stop offset="0" stop-color="#FF7A2E" stop-opacity=".5"/><stop offset="1" stop-color="#FF7A2E" stop-opacity="0"/></radialGradient>
<linearGradient id="cream" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{CREAM[0]}"/><stop offset="1" stop-color="{CREAM[1]}"/></linearGradient>
<linearGradient id="streak" x1="0" x2="1"><stop offset="0" stop-color="{ORANGE[0]}"/><stop offset=".7" stop-color="{ORANGE[1]}"/><stop offset="1" stop-color="{ORANGE[2]}"/></linearGradient>
<radialGradient id="sun" cx=".5" cy=".55" r=".6"><stop offset="0" stop-color="#FFE0A8"/><stop offset=".55" stop-color="#FF9A45"/><stop offset="1" stop-color="{ORANGE[0]}"/></radialGradient>
<clipPath id="sky"><rect width="100" height="{horizon}"/></clipPath>
</defs>
{tile}
<circle cx="{sun_x}" cy="{horizon}" r="{sun_r}" fill="url(#sun)" clip-path="url(#sky)"/>
<path d="M{x:.2f} {y1:.2f} A{r} {r} 0 1 1 {x:.2f} {y2:.2f}" fill="none" stroke="url(#cream)" stroke-width="{stroke}" transform="translate({cx} {cy}) skewX(-12) translate({-cx} {-cy})"/>
<path d="M{sun_x - 20} {horizon} H{sun_x + 24} L{sun_x + 22.5} {horizon + 3.4} H{sun_x - 21.5} Z" fill="url(#streak)"/>
{reflections}
</svg>"""


def photo_icon_html(px, inset):
    """Full-bleed square (no transparency: iOS and Android round or mask it themselves). inset = the mark's share
    of the edge, smaller for maskable icons so it stays inside Android's safe circle (80 % of the edge)."""
    crop = 300  # 300 x 300 of the key art: sea horizon (y 178) on the mark's horizon, sun reflection under its sun
    mark_px = px * inset
    off = (px - mark_px) / 2
    horizon_px = off + mark_px * 0.55
    sun_px = off + mark_px * 0.71
    scale = px / crop
    left = 800 * scale - sun_px
    top = 178 * scale - horizon_px
    return f"""<div style="position:relative;width:{px}px;height:{px}px;overflow:hidden;background:#13161D">
<div style="position:absolute;left:{-left:.2f}px;top:{-top:.2f}px;width:{1600 * scale:.2f}px;height:{900 * scale:.2f}px;
  background:url({KEYART.as_uri()}) 0 0/100% 100%;filter:saturate(1.08) contrast(1.04)"></div>
<div style="position:absolute;inset:0;background:#FF7A2E;mix-blend-mode:color;opacity:.38"></div>
<div style="position:absolute;inset:0;background:
  radial-gradient(42% 34% at {sun_px / px * 100:.1f}% {horizon_px / px * 100:.1f}%, rgba(255,150,70,.22), rgba(255,150,70,0) 70%),
  radial-gradient(75% 70% at {sun_px / px * 100:.1f}% {horizon_px / px * 100:.1f}%, rgba(8,8,12,0) 25%, rgba(8,8,12,.62) 100%),
  linear-gradient(180deg, rgba(10,11,16,.5), rgba(10,9,10,.12) 48%, rgba(22,9,4,.7))"></div>
<div style="position:absolute;left:{off:.2f}px;top:{off:.2f}px;width:{mark_px:.2f}px;height:{mark_px:.2f}px">{mark_svg(ground=False, size_attr=' width="100%" height="100%"')}</div>
</div>"""


def card_html():
    wide = (FONTS / "ArchivoExpanded/ArchivoExpanded-ExtraBoldItalic.ttf").as_uri()
    light = (ROOT / "tools/ui_art/fonts/ArchivoExpanded-Light.ttf").as_uri()
    cond = (FONTS / "BarlowCondensed/BarlowCondensed-ExtraBold.ttf").as_uri()
    return f"""<!DOCTYPE html><html><head><meta charset="utf-8"><style>
@font-face {{ font-family: Wide; src: url("{wide}"); }}
@font-face {{ font-family: WideLight; src: url("{light}"); }}
@font-face {{ font-family: Cond; src: url("{cond}"); }}
html, body {{ margin: 0; }}
#card {{ position: relative; width: 1200px; height: 630px; overflow: hidden; background: #07090D; color: #F7F4EE; }}
#art {{ position: absolute; left: -40px; top: -12px; width: 1280px; height: 720px; background: url({CARD_ART.as_uri()}) 0 0/100% 100%;
        filter: saturate(1.1) contrast(1.05); }}
#grade {{ position: absolute; inset: 0; background:
  radial-gradient(38% 30% at 50% 22%, rgba(255,160,80,.18), rgba(255,160,80,0) 70%),
  linear-gradient(180deg, rgba(7,9,13,.30) 0%, rgba(7,9,13,0) 18%, rgba(7,9,13,0) 34%, rgba(9,8,10,.72) 56%, rgba(7,9,13,.95) 80%),
  linear-gradient(90deg, rgba(7,9,13,.55) 0%, rgba(7,9,13,0) 55%); }}
#mark {{ position: absolute; left: 64px; top: 52px; width: 76px; height: 76px; filter: drop-shadow(0 6px 18px rgba(0,0,0,.45)); }}
#free {{ position: absolute; right: 64px; top: 70px; font: 22px/1 Cond; letter-spacing: .28em; color: #F7F4EE;
         padding: 12px 18px 11px 22px; border: 2px solid rgba(247,244,238,.5); background: rgba(7,9,13,.35); }}
#free b {{ color: #FF8A3D; }}
#logo {{ position: absolute; left: 64px; bottom: 74px; }}
#badge {{ display: flex; align-items: center; gap: 18px; font: 30px/1 Cond; letter-spacing: .42em; text-shadow: 0 2px 10px rgba(0,0,0,.5); }}
#badge i {{ display: block; width: 52px; height: 6px; background: linear-gradient(90deg, {ORANGE[0]}, {ORANGE[1]}); transform: skewX(-24deg); }}
#word {{ margin: 12px 0 0 -4px; font: 116px/1 Wide; letter-spacing: -.012em; white-space: nowrap;
        text-shadow: 0 6px 26px rgba(0,0,0,.5), 0 2px 4px rgba(0,0,0,.4); }}
#sun {{ margin: 14px 0 0 6px; width: 380px; height: 10px; transform: skewX(-28deg);
       background: linear-gradient(90deg, {ORANGE[0]} 0%, {ORANGE[1]} 46%, rgba(255,160,90,0) 100%); }}
#row {{ display: flex; align-items: center; gap: 28px; margin-top: 22px; }}
#ribbon {{ font: 34px/1 WideLight; letter-spacing: .5em; color: rgba(247,244,238,.94); text-shadow: 0 2px 12px rgba(0,0,0,.5); }}
#modes {{ display: flex; gap: 10px; font: 22px/1 Cond; letter-spacing: .12em; }}
#modes span {{ padding: 9px 14px 8px; background: rgba(247,244,238,.1); border: 1px solid rgba(247,244,238,.28); }}
#modes span:first-child {{ background: linear-gradient(90deg, {ORANGE[0]}, {ORANGE[1]}); border-color: transparent; }}
#tag {{ position: absolute; right: 64px; bottom: 82px; width: 300px; text-align: right; font: 24px/1.3 Cond;
       letter-spacing: .04em; color: rgba(247,244,238,.86); }}
</style></head><body><div id="card">
<div id="art"></div><div id="grade"></div>
<div id="mark">{mark_svg(size_attr=' width="100%" height="100%"')}</div>
<div id="free">PLAY FREE <b>&middot;</b> IN THE BROWSER</div>
<div id="logo">
  <div id="badge"><i></i>NOVA KART</div>
  <div id="word">CARCAVELOS</div>
  <div id="sun"></div>
  <div id="row"><div id="ribbon">CIRCUIT</div>
    <div id="modes"><span>RACE VS BOTS</span><span>TIME ATTACK</span><span>ONLINE RACE</span></div></div>
</div>
</div></body></html>"""


def render(page, html, w, h, transparent=False):
    """Opens the HTML as a file (a page from set_content may not load the file:// fonts and key art)."""
    if "<html" not in html:
        html = f'<!DOCTYPE html><html><head><meta charset="utf-8"></head><body style="margin:0;background:transparent">{html}</body></html>'
    tmp = ROOT / "brand.tmp.html"
    tmp.write_text(html, encoding="utf-8")
    try:
        page.set_viewport_size({"width": w, "height": h})
        page.goto(tmp.as_uri())
        page.evaluate("document.fonts.ready.then(() => Promise.all([...document.images].map(i => i.decode())))")
        page.wait_for_timeout(150)
        png = page.screenshot(clip={"x": 0, "y": 0, "width": w, "height": h}, omit_background=transparent)
    finally:
        tmp.unlink()
    return Image.open(io.BytesIO(png))


def save_png(img, path):
    # Full colour on purpose: a 256-colour palette shows rings in the sun glow and the sky.
    img.save(path, optimize=True)
    print(f"wrote {path.relative_to(ROOT)}  {path.stat().st_size // 1024} KB")


def main():
    with sync_playwright() as p:
        browser = p.chromium.launch(channel="msedge")
        page = browser.new_page(device_scale_factor=1)

        # Tab icons: flat mark, transparent corners. 16 and 32 use the small drawing; rendered at 4x and scaled down.
        tab = {}
        for px in (16, 32, 48):
            big = render(page, f'<div style="width:{px * 4}px;height:{px * 4}px">{mark_svg(small=px < 48, size_attr=" width=\"100%\" height=\"100%\"")}</div>',
                         px * 4, px * 4, transparent=True)
            tab[px] = big.convert("RGBA").resize((px, px), Image.LANCZOS)
            save_png(tab[px], DATA / f"favicon-{px}.png")
        tab[48].save(DATA / "favicon.ico", sizes=[(16, 16), (32, 32), (48, 48)], append_images=[tab[16], tab[32]])
        print("wrote", (DATA / "favicon.ico").relative_to(ROOT), (DATA / "favicon.ico").stat().st_size // 1024, "KB")
        (DATA / "favicon.svg").write_text(mark_svg(small=True) + "\n", encoding="utf-8", newline="\n")
        print("wrote", (DATA / "favicon.svg").relative_to(ROOT))

        # Home screen: photo ground. "any" icons fill most of the square; maskable ones keep the mark in the safe circle.
        for name, px, inset in (("icon-180", 180, .9), ("icon-192", 192, .9), ("icon-512", 512, .9),
                                ("icon-maskable-192", 192, .7), ("icon-maskable-512", 512, .7)):
            save_png(render(page, photo_icon_html(px, inset), px, px).convert("RGB"), DATA / f"{name}.png")

        # Link preview card: JPEG, highest quality that stays under the size budget.
        card = render(page, card_html(), 1200, 630).convert("RGB")
        for quality in (88, 85, 82, 78, 74, 70):
            buf = io.BytesIO()
            card.save(buf, "JPEG", quality=quality, optimize=True, progressive=True, subsampling="4:2:0")
            if buf.tell() <= CARD_MAX_BYTES:
                break
        (DATA / "og-card.jpg").write_bytes(buf.getvalue())
        print(f"wrote {(DATA / 'og-card.jpg').relative_to(ROOT)}  {buf.tell() // 1024} KB (q{quality})")

        if "--sheet" in sys.argv:  # contact sheet for review, outside the repo's asset folders
            sheet = Image.new("RGB", (1200 + 40, 630 + 40 + 560), "#2B2B2E")
            sheet.paste(card, (20, 20))
            x = 20
            for name in ("icon-512", "icon-maskable-512", "icon-180"):
                im = Image.open(DATA / f"{name}.png").convert("RGB").resize((360, 360) if name != "icon-180" else (180, 180), Image.LANCZOS)
                sheet.paste(im, (x, 630 + 60))
                x += im.width + 30
            for px in (48, 32, 16):
                sheet.paste(tab[px], (x, 630 + 60), tab[px])
                x += px + 30
            out = Path(sys.argv[sys.argv.index("--sheet") + 1]) if len(sys.argv) > sys.argv.index("--sheet") + 1 else ROOT / "brand-sheet.png"
            sheet.save(out)
            print("wrote", out)
        browser.close()


if __name__ == "__main__":
    main()
