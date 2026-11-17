"""Exports the five layers of the game's wordmark (realistic menu redesign) as PNGs, cropped to their content, into
UI/Art/Logo (the game) and the web page's TemplateData. The wordmark is drawn here in HTML with the repo's own font
files (Archivo Expanded + Barlow Condensed, SIL OFL 1.1), at 2x of its size in the game (600 x 200 at 1280 x 720).
Needs Python Playwright with Microsoft Edge. Run from the repo root: python tools/ui_art/export_wordmark.py
Prints each layer's box in % of the wordmark; the boxes in UI/FrontEnd.uss (.lg-*) and TemplateData/style.css must
match them. The five UXML names stay the same as before (sun, wave, word, badge, ribbon), so RaceHud.uxml,
UI/TitleScreen.cs (light sweep over the word) and the PlayMode test did not have to change:
  badge  = the orange bar + NOVA KART           word   = CARCAVELOS
  sun    = the orange light streak under it     ribbon = CIRCUIT
  wave   = the thin line that runs out to the right"""
from pathlib import Path
from playwright.sync_api import sync_playwright
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
FONTS = ROOT / "Assets/_Project/RaceFlow/UI/Fonts"
W, H = 1200, 400  # CSS px = 2x the game's 600 x 200
PAGE = f"""<!DOCTYPE html><html><head><meta charset="utf-8"><style>
@font-face {{ font-family: Wide; src: url("{(FONTS / 'ArchivoExpanded/ArchivoExpanded-ExtraBoldItalic.ttf').as_uri()}"); }}
@font-face {{ font-family: WideLight; src: url("{(ROOT / 'tools/ui_art/fonts/ArchivoExpanded-Light.ttf').as_uri()}"); }}
@font-face {{ font-family: Cond; src: url("{(FONTS / 'BarlowCondensed/BarlowCondensed-ExtraBold.ttf').as_uri()}"); }}
html, body {{ margin: 0; background: transparent; }}
#box {{ position: relative; width: {W}px; height: {H}px; }}
#box > div {{ position: absolute; }}
#badge {{ left: 8px; top: 30px; display: flex; align-items: center; gap: 22px; font: 38px/1 Cond; letter-spacing: .42em;
         color: #F6F2EA; text-shadow: 0 2px 10px rgba(0,0,0,.45); }}
#badge i {{ display: block; width: 64px; height: 7px; background: linear-gradient(90deg, #FF5A1F, #FF8A3D); transform: skewX(-24deg); }}
#word {{ left: 0; top: 78px; font: 168px/1 Wide; letter-spacing: -.012em; color: #F7F4EE; white-space: nowrap;
        text-shadow: 0 6px 26px rgba(0,0,0,.42), 0 2px 4px rgba(0,0,0,.35); padding: 0 6px; }}
#sun {{ left: 14px; top: 262px; width: 470px; height: 12px; transform: skewX(-28deg);
       background: linear-gradient(90deg, #FF5A1F 0%, #FF8A3D 46%, rgba(255,160,90,0) 100%); }}
#ribbon {{ left: 10px; top: 300px; font: 44px/1 WideLight; letter-spacing: .5em; color: rgba(247,244,238,.92);
          text-shadow: 0 2px 12px rgba(0,0,0,.5); }}
#wave {{ left: 520px; top: 321px; width: 668px; height: 3px;
        background: linear-gradient(90deg, rgba(247,244,238,.7), rgba(247,244,238,0)); }}
</style></head><body><div id="box">
<div id="sun"></div><div id="wave"></div><div id="badge"><i></i>NOVA KART</div><div id="word">CARCAVELOS</div><div id="ribbon">CIRCUIT</div>
</div></body></html>"""
LAYERS = ["sun", "wave", "word", "badge", "ribbon"]
OUTS = [ROOT / "Assets/_Project/RaceFlow/UI/Art/Logo", ROOT / "Assets/WebGLTemplates/CarcavelosCircuit/TemplateData"]

html = ROOT / "wordmark.tmp.html"
html.write_text(PAGE, encoding="utf-8")
with sync_playwright() as p:
    browser = p.chromium.launch(channel="msedge")
    page = browser.new_page(viewport={"width": W, "height": H})
    page.goto(html.as_uri())
    page.evaluate("document.fonts.ready")
    page.wait_for_timeout(600)
    # CARCAVELOS fills the width exactly (the wide face is measured, not guessed).
    page.evaluate(f"""() => {{ const w = document.getElementById('word'); const s = parseFloat(getComputedStyle(w).fontSize);
        w.style.fontSize = (s * ({W} - 16) / w.getBoundingClientRect().width) + 'px'; }}""")
    page.wait_for_timeout(200)
    for layer in LAYERS:
        page.evaluate("L => document.querySelectorAll('#box > div').forEach(d => d.style.visibility = d.id === L ? 'visible' : 'hidden')", layer)
        tmp = ROOT / f"logo-{layer}.tmp.png"
        page.screenshot(path=str(tmp), omit_background=True, clip={"x": 0, "y": 0, "width": W, "height": H})
        img = Image.open(tmp)
        x0, y0, x1, y1 = img.getbbox()
        x0, y0 = max(0, x0 - 4), max(0, y0 - 4)
        w, h = (min(W, x1 + 4) - x0 + 3) // 4 * 4, (min(H, y1 + 4) - y0 + 3) // 4 * 4  # multiples of 4 for compression
        part = img.crop((x0, y0, x0 + w, y0 + h))
        for out in OUTS:
            part.save(out / f"logo-{layer}.png", optimize=True)
        tmp.unlink()
        print(f".lg-{layer} {{ left: {x0 / W * 100:.3f}%; top: {y0 / H * 100:.3f}%; width: {w / W * 100:.3f}%; height: {h / H * 100:.3f}%; }}  ({w}x{h})")
    page.evaluate("document.querySelectorAll('#box > div').forEach(d => d.style.visibility = 'visible')")
    page.screenshot(path=str(ROOT / "tools/ui_art/wordmark-preview.png"), omit_background=True)
    browser.close()
html.unlink()
