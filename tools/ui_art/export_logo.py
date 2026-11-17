"""Exports the logo layers of tools/ui_art/logo.svg as PNGs (2x of the size shown at 1280 x 720), cropped to their
content, into UI/Art/Logo (the game) and the web page's TemplateData. Needs Python Playwright and the fonts from
Google Fonts (the page loads them). Run from the repo root: python tools/ui_art/export_logo.py
Prints each layer's box; the boxes in UI/Postcard.uss (.lg-*) and TemplateData/style.css must match them."""
from pathlib import Path
from playwright.sync_api import sync_playwright
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SVG = (ROOT / "tools/ui_art/logo.svg").read_text(encoding="utf-8")
PAGE = ('<!DOCTYPE html><html><head><link href="https://fonts.googleapis.com/css2?family=Shrikhand&family=Lilita+One&display=block" '
        'rel="stylesheet"><style>html,body{margin:0;background:transparent}svg{width:1120px;height:auto;display:block}</style></head><body>'
        + SVG + "</body></html>")
LAYERS = ["sun", "wave", "word", "badge", "ribbon"]
OUTS = [ROOT / "Assets/_Project/RaceFlow/UI/Art/Logo", ROOT / "Assets/WebGLTemplates/CarcavelosCircuit/TemplateData"]

with sync_playwright() as p:
    browser = p.chromium.launch(channel="msedge")
    page = browser.new_page(viewport={"width": 1120, "height": 628})
    page.set_content(PAGE)
    page.evaluate("document.fonts.ready")
    page.wait_for_timeout(800)
    for layer in LAYERS:
        page.evaluate("L => document.querySelectorAll('svg > g').forEach(g => g.style.visibility = g.classList.contains('lg-' + L + '-g') ? 'visible' : 'hidden')", layer)
        tmp = ROOT / f"logo-{layer}.tmp.png"
        page.screenshot(path=str(tmp), omit_background=True, clip={"x": 0, "y": 0, "width": 1120, "height": 628})
        img = Image.open(tmp)
        x0, y0, x1, y1 = img.getbbox()
        x0, y0 = x0 - 4, y0 - 4
        w, h = (x1 + 4 - x0 + 3) // 4 * 4, (y1 + 4 - y0 + 3) // 4 * 4  # multiples of 4 for texture compression
        crop = img.crop((x0, y0, x0 + w, y0 + h))
        tmp.unlink()
        for out in OUTS:
            crop.save(out / f"logo-{layer}.png", optimize=True)
        print(f"{layer}: left {x0 / 11.2:.3f}% top {y0 / 6.28:.3f}% width {w / 11.2:.3f}% height {h / 6.28:.3f}%")
    browser.close()
