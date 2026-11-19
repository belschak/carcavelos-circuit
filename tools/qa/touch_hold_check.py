"""Touch hold stress test for the phone controls (bug 19.11.2026: holding Gas on a phone gave gas only briefly,
tapping fast worked).

A held touch button must never let go while the finger is down, and must always let go when it should. The test
drives the page with REAL touch input (Chrome DevTools Protocol Input.dispatchTouchEvent, the same path a phone
finger takes: pointerdown/move/up/cancel, pointer capture, hit testing) in its own headless Chrome with phone
emulation, and records every keydown/keyup the touch buttons send to the game canvas.

Two levels:
  page mode (default, about 4 min with --quick 1 min): the page and its touch script, the Unity engine is stubbed
    out, so it runs on any machine and every case is deterministic. This is the regression test.
  --game: also loads the real game (GPU), starts a ?net=local race and reads the kart's own speed from the pose
    messages the game broadcasts (Online/Pose/KartPoseCodec.cs), so "the kart really keeps driving" is checked too.

usage:
  python tools/qa/touch_hold_check.py                      # live build https://nova-kart.github.io/
  python tools/qa/touch_hold_check.py --quick              # 3 s holds instead of 10 s
  python tools/qa/touch_hold_check.py --url http://localhost:8000/   # a local build (tools/serve_web_build.py)
  python tools/qa/touch_hold_check.py --patch-from <git ref or worktree folder>  # live build + a branch's touch code
  python tools/qa/touch_hold_check.py --game               # plus the real-game speed check
  options: --profile android|iphone|android-bars  --json <file>  --only <case substring>
--patch-from <ref|folder>: the diff origin/main..<ref> (or origin/main..the folder's working files) of Assets/WebGLTemplates/CarcavelosCircuit/index.html is applied to
the live page HTML and the ref's style.css is served instead of the live one, so a fix can be tested before CI built
it. Needs the repo (runs git in the repo this file lives in).

Exit code 0 = every case passed, 1 = at least one failed. Needs: pip install playwright==1.65.2 (tools/requirements.txt)
and Google Chrome (channel "chrome")."""
import argparse, asyncio, base64, json, math, random, re, struct, subprocess, sys, tempfile, time
from pathlib import Path
from playwright.async_api import async_playwright

REPO = Path(__file__).resolve().parents[2]
TEMPLATE = "Assets/WebGLTemplates/CarcavelosCircuit/index.html"
STYLE = "Assets/WebGLTemplates/CarcavelosCircuit/TemplateData/style.css"
ANDROID_UA = ("Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) "
              "Chrome/130.0.0.0 Mobile Safari/537.36")
IPHONE_UA = ("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) "
             "Version/18.0 Mobile/15E148 Safari/604.1")
PROFILES = {  # CSS px of the page area, user agent
    "android": (915, 412, ANDROID_UA),        # Pixel 7 full screen / home screen
    "android-bars": (800, 336, ANDROID_UA),   # Galaxy S Chrome with browser bars (smallest buttons)
    "iphone": (844, 340, IPHONE_UA),          # iPhone 14/15 Safari with bars
}
BUTTONS = {  # aria-label -> short name, keys it must hold
    "Gas": ("gas", ["KeyW"]),
    "Brake and reverse": ("brake", ["KeyS"]),
    "Drift": ("drift", ["KeyW", "Space"]),
    "Steer left": ("left", ["KeyA"]),
    "Steer right": ("right", ["KeyD"]),
}
FRAME = 1 / 60  # a phone sends about one touch move per display frame
SHOTS = Path(tempfile.gettempdir()) / "touch_hold_check"  # --game screenshots (throwaway; --shots to change)

# Records every key the touch buttons send to the canvas (type, code, ms) plus the pointer events on the touch panel.
INIT = r"""(() => {
  window.__keys = []; window.__ptr = {};
  const t0 = performance.now();
  const rec = e => { if (e.target && e.target.id === 'cc-canvas' && !e.isTrusted)
    window.__keys.push([e.type, e.code, Math.round(performance.now() - t0)]); };
  document.addEventListener('keydown', rec, true); document.addEventListener('keyup', rec, true);
  ['pointerdown','pointerup','pointercancel','lostpointercapture','contextmenu','touchcancel'].forEach(n =>
    document.addEventListener(n, () => { window.__ptr[n] = (window.__ptr[n] || 0) + 1; }, true));
  window.__now = () => Math.round(performance.now() - t0);
  // --game: the kart's own pose messages (?net=local test transport) -> speed timeline
  window.__poses = [];
  const post = BroadcastChannel.prototype.postMessage;
  BroadcastChannel.prototype.postMessage = function (m) {
    if (typeof m === 'string' && m.startsWith('pose|')) window.__poses.push([Math.round(performance.now() - t0), m.split('|')[2]]);
    return post.apply(this, arguments);
  };
})();"""

# Page mode: the Unity loader is replaced by a stub that never finishes, the touch panel is switched on by hand
# (exactly what showTouchControls + the game's "cc-race-state" event do).
STUB_LOADER = "window.createUnityInstance = function () { return new Promise(function () {}); };"
FORCE_RACE = r"""() => {
  const l = document.getElementById('cc-loader'); if (l) l.hidden = true;
  document.getElementById('cc-touch').hidden = false;
  window.dispatchEvent(new CustomEvent('cc-race-state', { detail: { racing: true } }));
}"""
RECTS = r"""() => { const o = {}; document.querySelectorAll('#cc-touch [data-keys]').forEach(b => {
  const r = b.getBoundingClientRect(); if (r.width) o[b.getAttribute('aria-label')] = [r.left, r.top, r.right, r.bottom]; });
  return o; }"""


# ---------------------------------------------------------------- patching a branch's touch code into the live page
def git(*args):
    return subprocess.run(["git", "-C", str(REPO), *args], capture_output=True, text=True, encoding="utf-8",
                          check=True).stdout


def apply_unified_diff(text, diff):
    """Applies a git unified diff (one file) to text by matching each hunk's old lines (context + removed)."""
    hunks, cur = [], None
    for line in diff.splitlines():
        if line.startswith("@@"):
            cur = ([], [])
            hunks.append(cur)
        elif cur is not None and not line.startswith("\\"):
            if line.startswith(" ") or line == "":
                cur[0].append(line[1:]); cur[1].append(line[1:])
            elif line.startswith("-"):
                cur[0].append(line[1:])
            elif line.startswith("+"):
                cur[1].append(line[1:])
    lines = text.split("\n")
    for old, new in hunks:
        n = len(old)
        at = next((i for i in range(len(lines) - n + 1) if lines[i:i + n] == old), None)
        if at is None:  # tolerate trailing whitespace / CRLF differences
            strip = [o.rstrip() for o in old]
            at = next((i for i in range(len(lines) - n + 1) if [x.rstrip() for x in lines[i:i + n]] == strip), None)
        if at is None:
            raise RuntimeError("patch hunk does not match the live page:\n" + "\n".join(old[:6]))
        lines[at:at + n] = new
    return "\n".join(lines)


# ---------------------------------------------------------------- touch helpers
class Touch:
    def __init__(self, page, cdp):
        self.page, self.cdp, self.fingers = page, cdp, {}

    # CDP semantics (checked 19.11.2026): touchStart/touchMove list ALL fingers on the glass (matched by id),
    # touchEnd lists only the fingers that lift, touchCancel with no points cancels all of them.
    async def send(self, kind, fingers=None):
        fingers = self.fingers if fingers is None else fingers
        pts = [{"x": x, "y": y, "id": i, "radiusX": 9, "radiusY": 9, "force": 1} for i, (x, y) in fingers.items()]
        await self.cdp.send("Input.dispatchTouchEvent", {"type": kind, "touchPoints": pts})

    async def down(self, fid, x, y):
        self.fingers[fid] = (x, y)
        await self.send("touchStart")

    async def move(self, fid, x, y):
        self.fingers[fid] = (x, y)
        await self.send("touchMove")

    async def up(self, fid):
        if fid in self.fingers:
            await self.send("touchEnd", {fid: self.fingers.pop(fid)})

    async def cancel(self):
        self.fingers = {}
        await self.cdp.send("Input.dispatchTouchEvent", {"type": "touchCancel", "touchPoints": []})


async def keys_since(page, n):
    return (await page.evaluate("window.__keys"))[n:]


async def key_count(page):
    return await page.evaluate("window.__keys.length")


def held_after(events):
    """Key -> net presses (keydown +1, keyup -1) over the events."""
    m = {}
    for t, c, _ in events:
        m[c] = m.get(c, 0) + (1 if t == "keydown" else -1)
    return {c: v for c, v in m.items() if v}


def drops(events, code, t_end):
    """Times (ms) a key was up while it should be held: list of (keyup ms, next keydown ms or None)."""
    out, up_at = [], None
    for t, c, ms in events:
        if c != code:
            continue
        if t == "keyup" and ms < t_end:
            up_at = ms
        elif t == "keydown" and up_at is not None:
            out.append((up_at, ms)); up_at = None
    if up_at is not None:
        out.append((up_at, None))
    return out


class Results:
    def __init__(self):
        self.rows = []

    def add(self, name, ok, expected, actual):
        self.rows.append({"case": name, "pass": bool(ok), "expected": expected, "actual": actual})
        print(f"  {'PASS' if ok else 'FAIL'}  {name}\n        expected: {expected}\n        actual:   {actual}", flush=True)


# ---------------------------------------------------------------- the cases
def edge_point(rect, side, inset):
    """A point `inset` px inside (negative = outside) the given side of the rect, in the middle of that side."""
    x0, y0, x1, y1 = rect
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    return {"left": (x0 + inset, cy), "right": (x1 - inset, cy), "top": (cx, y0 + inset), "bottom": (cx, y1 - inset)}[side]


def free_outside(rects, label, side, depth):
    """True when the strip `depth` px outside this side of the button touches no other button."""
    x, y = edge_point(rects[label], side, -depth)
    for other, (a, b, c, d) in rects.items():
        if other != label and a - 1 <= x <= c + 1 and b - 1 <= y <= d + 1:
            return False
    return True


async def hold_case(page, touch, res, rects, label, mode, seconds, rng):
    short, codes = BUTTONS[label]
    r = rects[label]
    w, h = r[2] - r[0], r[3] - r[1]
    side = None
    if mode == "still" or mode == "wobble":
        ax, ay = (r[0] + r[2]) / 2, (r[1] + r[3]) / 2
    elif mode == "rim":  # inside the button at rest, but in the strip the 0.95 pressed shrink gives up
        side = next(s for s in ("left", "top", "right", "bottom") if free_outside(rects, label, s, 6))
        rim = min(w, h) * 0.025
        ax, ay = edge_point(r, side, rim * 0.5)
    else:  # "edge": wobble across the edge, up to 4 px out, on a side with no other button next to it
        side = next(s for s in ("left", "top", "right", "bottom") if free_outside(rects, label, s, 6))
        ax, ay = edge_point(r, side, 0.5)
    n0 = await key_count(page)
    await touch.down(0, ax, ay)
    t_start = await page.evaluate("window.__now()")
    end = time.perf_counter() + seconds
    k = 0
    while time.perf_counter() < end:
        if mode == "still":
            await asyncio.sleep(0.05)
            continue
        k += 1
        if mode == "wobble":
            dx, dy = rng.uniform(-3, 3), rng.uniform(-3, 3)
            if abs(dx) < 1 and abs(dy) < 1:
                dx = math.copysign(1, dx)
        elif mode == "rim":  # along the edge +-3 px, across it only within the rim (never leaves the resting button)
            rim = min(w, h) * 0.025
            along, across = rng.uniform(-3, 3), rng.uniform(-0.45, 0.45) * rim
            dx, dy = (across, along) if side in ("left", "right") else (along, across)
            if side in ("right",):
                dx = -dx
            if side in ("bottom",):
                dy = -dy
        else:
            along, across = rng.uniform(-3, 3), rng.uniform(-4, 2.5)  # negative = outside the button
            nx, ny = {"left": (1, 0), "right": (-1, 0), "top": (0, 1), "bottom": (0, -1)}[side]
            dx, dy = nx * across + (0 if nx else along), ny * across + (0 if ny else along)
        await touch.move(0, ax + dx, ay + dy)
        await asyncio.sleep(FRAME)
    t_lift = await page.evaluate("window.__now()")
    await touch.up(0)
    await asyncio.sleep(0.15)
    ev = await keys_since(page, n0)
    downs = {c: sum(1 for t, cc, _ in ev if t == "keydown" and cc == c) for c in codes}
    gaps = {c: drops(ev, c, t_lift) for c in codes}
    ok = all(downs[c] == 1 for c in codes) and not any(gaps.values()) and not held_after(ev)
    where = f" ({side} edge)" if side else ""
    actual = (f"keydown {downs}, drops while held {sum(len(g) for g in gaps.values())}"
              + (f" first {[(a - t_start, (b - t_start) if b else None) for a, b in list(gaps.values())[0][:3]]} ms" if any(gaps.values()) else "")
              + f", stuck after lift {held_after(ev) or 'none'}, moves sent {k}")
    res.add(f"hold {short} {seconds:.0f}s {mode}{where}", ok,
            f"{'+'.join(codes)} down once, never up until lift, all up after", actual)


def centre(rect):
    return (rect[0] + rect[2]) / 2, (rect[1] + rect[3]) / 2


async def slide(touch, fid, a, b, steps=14):
    for i in range(1, steps + 1):
        await touch.move(fid, a[0] + (b[0] - a[0]) * i / steps, a[1] + (b[1] - a[1]) * i / steps)
        await asyncio.sleep(FRAME)


async def slide_cases(page, touch, res, rects):
    gas, drift, brake = centre(rects["Gas"]), centre(rects["Drift"]), centre(rects["Brake and reverse"])
    # Gas -> Drift -> Gas: W must stay down the whole time, Space comes and goes with Drift
    n0 = await key_count(page)
    await touch.down(0, *gas); await asyncio.sleep(0.4)
    await slide(touch, 0, gas, drift); await asyncio.sleep(0.6)
    await slide(touch, 0, drift, gas); await asyncio.sleep(0.4)
    t_lift = await page.evaluate("window.__now()")
    await touch.up(0); await asyncio.sleep(0.15)
    ev = await keys_since(page, n0)
    w_gaps = drops(ev, "KeyW", t_lift)
    sp = [(t, ms) for t, c, ms in ev if c == "Space"]
    ok = not w_gaps and [t for t, _ in sp] == ["keydown", "keyup"] and not held_after(ev)
    res.add("slide gas -> drift -> gas", ok, "W down once and never up until lift (no gap), Space down then up, nothing stuck",
            f"W drops {w_gaps}, Space {[t for t, _ in sp]}, stuck {held_after(ev) or 'none'}, events {[(t[3:], c) for t, c, _ in ev]}")
    # Gas -> Brake: W hands over to S (both slide buttons), nothing stuck
    n0 = await key_count(page)
    await touch.down(0, *gas); await asyncio.sleep(0.3)
    await slide(touch, 0, gas, brake); await asyncio.sleep(0.4)
    await touch.up(0); await asyncio.sleep(0.15)
    ev = await keys_since(page, n0)
    order = [(t[3:], c) for t, c, _ in ev]
    ok = ("down", "KeyS") in order and ("up", "KeyW") in order and not held_after(ev)
    res.add("slide gas -> brake", ok, "W up, S down, after lift nothing held", f"events {order}")
    # Steer left -> right -> left: the held direction follows the thumb, never both, nothing stuck
    left, right = centre(rects["Steer left"]), centre(rects["Steer right"])
    n0 = await key_count(page)
    await touch.down(0, *left); await asyncio.sleep(0.3)
    await slide(touch, 0, left, right); await asyncio.sleep(0.4)
    mid = held_after(await keys_since(page, n0))
    await slide(touch, 0, right, left); await asyncio.sleep(0.3)
    end = held_after(await keys_since(page, n0))
    await touch.up(0); await asyncio.sleep(0.15)
    ev = await keys_since(page, n0)
    ok = mid == {"KeyD": 1} and end == {"KeyA": 1} and not held_after(ev)
    res.add("slide steer left -> right -> left", ok, "on right only D held, back on left only A held, nothing after lift",
            f"on right {mid}, back on left {end}, after lift {held_after(ev) or 'none'}")


async def two_thumb_cases(page, touch, res, rects, rng, seconds):
    gas, left = centre(rects["Gas"]), centre(rects["Steer left"])
    for lift, keep, keep_code in ((1, 0, "KeyW"), (0, 1, "KeyA")):
        n0 = await key_count(page)
        await touch.down(0, *gas); await touch.down(1, *left)
        end = time.perf_counter() + seconds / 2
        while time.perf_counter() < end:  # both thumbs wobble a little
            for fid, p in ((0, gas), (1, left)):
                touch.fingers[fid] = (p[0] + rng.uniform(-2, 2), p[1] + rng.uniform(-2, 2))
            await touch.send("touchMove"); await asyncio.sleep(FRAME)
        t_lift1 = await page.evaluate("window.__now()")
        await touch.up(lift)
        end = time.perf_counter() + 1.0
        while time.perf_counter() < end:  # the other thumb keeps wobbling
            p = gas if keep == 0 else left
            await touch.move(keep, p[0] + rng.uniform(-2, 2), p[1] + rng.uniform(-2, 2)); await asyncio.sleep(FRAME)
        t_lift2 = await page.evaluate("window.__now()")
        mid = held_after(await keys_since(page, n0))
        await touch.up(keep); await asyncio.sleep(0.15)
        ev = await keys_since(page, n0)
        lifted = "steer" if lift == 1 else "gas"
        ok = mid == {keep_code: 1} and not drops(ev, keep_code, t_lift2) and not held_after(ev)
        res.add(f"two thumbs (gas + steer left), lift {lifted}", ok,
                f"after lifting {lifted} only {keep_code} held, it never dropped, nothing after the last lift",
                f"held after first lift {mid}, {keep_code} drops {drops(ev, keep_code, t_lift2)}, after all {held_after(ev) or 'none'}")


async def tap_case(page, touch, res, rects):
    gas = centre(rects["Gas"])
    n0 = await key_count(page)
    for _ in range(10):
        await touch.down(0, *gas); await asyncio.sleep(0.06)
        await touch.up(0); await asyncio.sleep(0.06)
    await asyncio.sleep(0.1)
    ev = await keys_since(page, n0)
    d = sum(1 for t, c, _ in ev if t == "keydown" and c == "KeyW")
    u = sum(1 for t, c, _ in ev if t == "keyup" and c == "KeyW")
    res.add("tap gas 10x fast", d == 10 and u == 10 and not held_after(ev), "10 W down, 10 W up", f"{d} down, {u} up")


async def release_cases(page, touch, res, rects):
    """Negative cases: the key MUST go up, and stay up, with nothing stuck after."""
    gas = centre(rects["Gas"])

    async def one(name, action, finger_still_down):
        n0 = await key_count(page)
        await touch.down(0, *gas); await asyncio.sleep(0.3)
        await action()
        await asyncio.sleep(0.15)
        after_action = held_after(await keys_since(page, n0))
        if finger_still_down:  # the finger stays and wobbles: it must not press Gas again by itself
            for _ in range(20):
                await touch.move(0, gas[0] + random.uniform(-2, 2), gas[1] + random.uniform(-2, 2)); await asyncio.sleep(FRAME)
            await touch.up(0)
        await asyncio.sleep(0.15)
        ev = await keys_since(page, n0)
        await page.evaluate(FORCE_RACE)
        ok = not after_action and not held_after(ev)
        res.add(f"release on {name}", ok, "W up right after it, nothing held later (also after the finger moves and lifts)",
                f"held right after: {after_action or 'none'}, held at the end: {held_after(ev) or 'none'}, events {[(t[3:], c) for t, c, _ in ev]}")

    await one("finger up", lambda: touch.up(0), False)
    await one("pointercancel (touchCancel)", touch.cancel, False)
    await one("page hidden (visibilitychange)", lambda: page.evaluate("""() => {
        Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' });
        Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
        document.dispatchEvent(new Event('visibilitychange'));
        delete document.visibilityState; delete document.hidden; }"""), True)
    await one("window blur", lambda: page.evaluate("() => window.dispatchEvent(new Event('blur'))"), True)
    await one("race end (cc-race-state racing:false)", lambda: page.evaluate(
        "() => window.dispatchEvent(new CustomEvent('cc-race-state', { detail: { racing: false } }))"), True)
    await one("pause (cc-race-state paused)", lambda: page.evaluate(
        "() => window.dispatchEvent(new CustomEvent('cc-race-state', { detail: { racing: false, paused: true } }))"), True)


async def break_cases(page, touch, res, rects, rng):
    """Extra attempts to break a held Gas."""
    gas, item = centre(rects["Gas"]), centre(rects["Use item"]) if "Use item" in rects else None
    # Gas held, the other thumb taps Item 6x
    if item:
        n0 = await key_count(page)
        await touch.down(0, *gas); await asyncio.sleep(0.2)
        for _ in range(6):
            await touch.down(1, *item); await asyncio.sleep(0.07); await touch.up(1); await asyncio.sleep(0.1)
        t = await page.evaluate("window.__now()")
        await touch.up(0); await asyncio.sleep(0.15)
        ev = await keys_since(page, n0)
        e = sum(1 for tt, c, _ in ev if tt == "keydown" and c == "KeyE")
        res.add("gas held while the other thumb taps Item 6x", not drops(ev, "KeyW", t) and e == 6 and not held_after(ev),
                "W never drops, 6 E presses, nothing stuck", f"W drops {drops(ev, 'KeyW', t)}, E presses {e}, stuck {held_after(ev) or 'none'}")
    # Big thumb roll: the contact point wanders 12 px around inside Gas, fast
    n0 = await key_count(page)
    await touch.down(0, *gas)
    for i in range(240):
        a = i / 8
        await touch.move(0, gas[0] + 12 * math.cos(a), gas[1] + 12 * math.sin(a)); await asyncio.sleep(FRAME / 2)
    t = await page.evaluate("window.__now()")
    await touch.up(0); await asyncio.sleep(0.15)
    ev = await keys_since(page, n0)
    res.add("gas held, thumb rolls in 12 px circles", not drops(ev, "KeyW", t) and not held_after(ev),
            "W never drops", f"W drops {len(drops(ev, 'KeyW', t))}, stuck {held_after(ev) or 'none'}")
    # Thumb slides off Gas into the empty game area and back (real thumbs do this in a hard corner)
    r = rects["Gas"]
    side = next(s for s in ("left", "top", "right", "bottom") if free_outside(rects, "Gas", s, 6))
    out = edge_point(r, side, -30 if side in ("top",) else -5)
    n0 = await key_count(page)
    await touch.down(0, *gas); await asyncio.sleep(0.2)
    await slide(touch, 0, gas, out, 10); await asyncio.sleep(0.3)
    await slide(touch, 0, out, gas, 10); await asyncio.sleep(0.2)
    t = await page.evaluate("window.__now()")
    await touch.up(0); await asyncio.sleep(0.15)
    ev = await keys_since(page, n0)
    res.add(f"gas held, thumb slides off the {side} edge into the game and back (info)", not held_after(ev),
            "nothing stuck after lift (W may or may not stay held off the button: design choice)",
            f"W drops {drops(ev, 'KeyW', t)}, stuck {held_after(ev) or 'none'}")
    # Fuzz: 3 fingers, 600 random steps (down / move / big jump / up, now and then a cancel or blur) all over the
    # buttons and the game area. Invariants: no key goes down twice without an up between (no double press),
    # never an up without a down, and when every finger is off the glass nothing is held.
    def anywhere():
        if rng.random() < 0.75:
            lab = rng.choice(list(rects))
            x0, y0, x1, y1 = rects[lab]
            return rng.uniform(x0 - 8, x1 + 8), rng.uniform(y0 - 8, y1 + 8)
        vw, vh = page.viewport_size["width"], page.viewport_size["height"]
        return rng.uniform(0, vw), rng.uniform(vh * 0.4, vh)
    n0 = await key_count(page)
    bad, idle_checks = [], 0
    for step in range(600):
        roll = rng.random()
        free = [f for f in (0, 1, 2) if f not in touch.fingers]
        if free and (roll < 0.15 or not touch.fingers):
            await touch.down(free[0], *anywhere())
        elif roll < 0.25 and touch.fingers:
            await touch.up(rng.choice(list(touch.fingers)))
        elif roll < 0.255:
            await touch.cancel()
        elif roll < 0.26:
            await page.evaluate("() => window.dispatchEvent(new Event('blur'))")
        elif touch.fingers:
            f = rng.choice(list(touch.fingers))
            x, y = touch.fingers[f]
            if roll < 0.9:
                await touch.move(f, x + rng.uniform(-4, 4), y + rng.uniform(-4, 4))
            else:
                await touch.move(f, *anywhere())
        await asyncio.sleep(0.004)
        if not touch.fingers:
            await asyncio.sleep(0.03)
            idle_checks += 1
            h = held_after(await keys_since(page, n0))
            if h:
                bad.append(f"step {step}: no finger down but held {h}")
    for f in list(touch.fingers):
        await touch.up(f)
    await asyncio.sleep(0.15)
    ev = await keys_since(page, n0)
    state = {}
    for t, c, ms in ev:
        v = state.get(c, 0) + (1 if t == "keydown" else -1)
        if v not in (0, 1):
            bad.append(f"{c} {'pressed twice' if v > 1 else 'released while up'} at {ms} ms")
        state[c] = max(0, min(1, v))
    res.add("fuzz: 3 fingers, 600 random touch steps", not bad and not held_after(ev),
            "no double press, no release without press, nothing held whenever all fingers are off (and at the end)",
            f"{len(ev)} key events, {idle_checks} all-fingers-off checks, problems {bad[:4] or 'none'}, stuck {held_after(ev) or 'none'}")
    await page.evaluate(FORCE_RACE)
    # Three fingers at once on gas, drift, brake; lift in a mixed order
    n0 = await key_count(page)
    await touch.down(0, *gas); await touch.down(1, *centre(rects["Drift"])); await touch.down(2, *centre(rects["Brake and reverse"]))
    await asyncio.sleep(0.3)
    await touch.up(0); await asyncio.sleep(0.1)
    mid = held_after(await keys_since(page, n0))
    await touch.up(2); await touch.up(1); await asyncio.sleep(0.15)
    ev = await keys_since(page, n0)
    res.add("three fingers gas + drift + brake, lift gas first", mid.get("KeyW") == 1 and mid.get("KeyS") == 1 and not held_after(ev),
            "W still held by Drift after Gas lifts, S held, nothing stuck at the end", f"after gas lift {mid}, at the end {held_after(ev) or 'none'}")


# ---------------------------------------------------------------- --game: the kart's own speed
def decode_speed(b64):
    raw = base64.b64decode(b64)
    if len(raw) != 33 or raw[0] != 1:
        return None
    return struct.unpack_from("<h", raw, 27)[0] / 100.0, struct.unpack_from("<b", raw, 29)[0] / 127.0


async def game_cases(browser, args, profile, res):
    w, h, ua = PROFILES[profile]
    ctx = await browser.new_context(viewport={"width": w, "height": h}, device_scale_factor=2, is_mobile=True,
                                    has_touch=True, user_agent=ua)
    page = await ctx.new_page()
    log = []
    page.on("console", lambda m: log.append(f"[{m.type}] {m.text}"[:200]))
    await page.add_init_script(INIT)
    await install_patch(page, args)
    # ?autostart needs at least 2 players: a second tab of the same browser (BroadcastChannel) joins as a bot-like
    # player (autopilot, no rendering), the tested tab is the host and starts 5 s after both are in.
    await page.goto(args.url + "?test=mobile&photo=0&net=local&room=carcavelos&name=Stress&autostart=2")
    cdp = await ctx.new_cdp_session(page)
    touch = Touch(page, cdp)
    await page.wait_for_selector("#cc-load-anyway", state="visible", timeout=30000)
    await page.tap("#cc-load-anyway")
    await page.wait_for_function("() => { const p = document.getElementById('cc-panel-play'), l = document.getElementById('cc-loader');"
                                 " return (!!p && !p.hidden) || (!!l && (l.hidden || l.classList.contains('cc-done'))); }",
                                 timeout=300000, polling=500)
    if await page.evaluate("(() => { const l = document.getElementById('cc-loader'); return !(l.hidden || l.classList.contains('cc-done')); })()"):
        await touch.down(0, w / 2, h / 2); await asyncio.sleep(0.09); await touch.up(0)
    print("  game loaded, second player joins, waiting for the local online race to start", flush=True)
    other = await ctx.new_page()
    await other.goto(args.url + "?photo=0&net=local&room=carcavelos&name=Buddy&autopilot=1&render=0")
    try:
        await page.wait_for_function("() => document.documentElement.classList.contains('cc-racing')", timeout=90000, polling=500)
    except Exception:
        shot = SHOTS / "touch-hold-game-no-race.png"
        await page.screenshot(path=str(shot))
        res.add("game: local online race starts", False, "cc-racing within 90 s", f"no race (screenshot {shot}); console tail {log[-8:]}")
        await ctx.close()
        return
    await asyncio.sleep(5)  # countdown
    rects = await page.evaluate(RECTS)
    gas = centre(rects["Gas"])
    rng = random.Random(7)
    side = next(s for s in ("left", "top", "right", "bottom") if free_outside(rects, "Gas", s, 6))
    rim = min(rects["Gas"][2] - rects["Gas"][0], rects["Gas"][3] - rects["Gas"][1]) * 0.025
    for mode, anchor in (("still", gas), ("wobble", gas), ("rim", edge_point(rects["Gas"], side, rim * 0.5)),
                         ("edge", edge_point(rects["Gas"], side, 0.5))):
        # brake to a stop first, so every case starts at 0 km/h
        s = centre(rects["Brake and reverse"])
        await touch.down(0, *s); await asyncio.sleep(2.5); await touch.up(0); await asyncio.sleep(1.5)
        p0 = len(await page.evaluate("window.__poses"))
        t0 = await page.evaluate("window.__now()")
        await touch.down(0, *anchor)
        end = time.perf_counter() + args.seconds
        while time.perf_counter() < end:
            if mode == "still":
                await asyncio.sleep(0.05); continue
            if mode == "wobble":
                await touch.move(0, anchor[0] + rng.uniform(-3, 3), anchor[1] + rng.uniform(-3, 3))
            elif mode == "edge":  # up to 4 px out over the edge and back
                al, ac = rng.uniform(-3, 3), rng.uniform(-4, 2.5)
                nx, ny = {"left": (1, 0), "right": (-1, 0), "top": (0, 1), "bottom": (0, -1)}[side]
                await touch.move(0, anchor[0] + nx * ac + (0 if nx else al), anchor[1] + ny * ac + (0 if ny else al))
            else:
                al, ac = rng.uniform(-3, 3), rng.uniform(-0.45, 0.45) * rim
                await touch.move(0, anchor[0] + (ac if side in ("left", "right") else al),
                                 anchor[1] + (al if side in ("left", "right") else ac))
            await asyncio.sleep(FRAME)
        await touch.up(0)
        poses = (await page.evaluate("window.__poses"))[p0:]
        speeds = [(ms - t0, decode_speed(b)) for ms, b in poses]
        speeds = [(ms, v[0]) for ms, v in speeds if v]
        if not speeds:
            res.add(f"game: hold gas {args.seconds:.0f}s {mode}, kart speed", False, "pose messages with speed", "no pose messages")
            continue
        top = max(v for _, v in speeds)
        late = [v for ms, v in speeds if ms > args.seconds * 1000 * 0.6]
        # the kart must not lose speed while gas is held: after the first 2 s, any drop > 15 % of the top speed fails
        worst = 0.0
        best = 0.0
        for ms, v in speeds:
            if ms < 2000:
                best = max(best, v); continue
            best = max(best, v)
            worst = max(worst, best - v)
        ok = top > 5 and worst < 0.15 * top
        res.add(f"game: hold gas {args.seconds:.0f}s {mode}, kart speed", ok,
                "speed rises and never drops more than 15 % of its top while held",
                f"top {top * 3.6:.0f} km/h, biggest drop {worst * 3.6:.0f} km/h, last 40 % avg "
                f"{(sum(late) / len(late) * 3.6 if late else 0):.0f} km/h, {len(speeds)} poses")
    await page.screenshot(path=str(SHOTS / "touch-hold-game.png"))
    await ctx.close()


async def install_patch(page, args):
    if args.patch_from:
        if Path(args.patch_from).is_dir():  # a worktree folder: its files on disk, committed or not
            wt = Path(args.patch_from)
            diff = subprocess.run(["git", "-C", str(wt), "diff", "origin/main", "--", TEMPLATE], capture_output=True,
                                  text=True, encoding="utf-8", check=True).stdout
            css = (wt / STYLE).read_text(encoding="utf-8")
        else:
            diff = git("diff", f"origin/main..{args.patch_from}", "--", TEMPLATE)
            css = git("show", f"{args.patch_from}:{STYLE}")
        print(f"  patching the live page with {args.patch_from}: {diff.count(chr(10) + '@@')} hunk(s) in index.html", flush=True)

        async def html(route):
            resp = await route.fetch()
            body = apply_unified_diff(await resp.text(), diff)
            await route.fulfill(response=resp, body=body, headers={**resp.headers, "content-type": "text/html; charset=utf-8"})

        async def style(route):
            await route.fulfill(status=200, body=css, headers={"content-type": "text/css; charset=utf-8", "cache-control": "no-store"})

        base = re.escape(args.url)
        await page.route(re.compile(base + r"(index\.html)?(\?.*)?$"), html)
        await page.route(re.compile(base + r"TemplateData/style\.css.*"), style)


async def page_cases(browser, args, profile, res):
    w, h, ua = PROFILES[profile]
    ctx = await browser.new_context(viewport={"width": w, "height": h}, device_scale_factor=2, is_mobile=True,
                                    has_touch=True, user_agent=ua)
    page = await ctx.new_page()
    await page.add_init_script(INIT)
    await install_patch(page, args)

    async def build(route):
        if route.request.resource_type == "script":
            await route.fulfill(status=200, body=STUB_LOADER, headers={"content-type": "text/javascript"})
        else:
            await route.abort()
    await page.route("**/Build/**", build)
    await page.goto(args.url + "?test=mobile&photo=0")
    await page.wait_for_selector("#cc-touch", state="attached")
    await page.wait_for_timeout(800)
    await page.evaluate(FORCE_RACE)
    await page.wait_for_timeout(300)
    rects = await page.evaluate(RECTS)
    missing = [l for l in BUTTONS if l not in rects]
    if missing:
        res.add("touch buttons visible", False, "all driving buttons", f"missing {missing}")
        return
    print(f"  {profile} {w}x{h}: Gas {rects['Gas'][2] - rects['Gas'][0]:.0f} px, "
          f"Drift/Gas gap {rects['Gas'][1] - rects['Drift'][3]:.1f} px", flush=True)
    cdp = await ctx.new_cdp_session(page)
    touch = Touch(page, cdp)
    rng = random.Random(19112026)
    want = lambda name: not args.only or args.only in name
    for label in BUTTONS:
        for mode in ("still", "wobble", "rim", "edge"):
            if want(f"hold {BUTTONS[label][0]} {mode}"):
                await hold_case(page, touch, res, rects, label, mode, args.seconds, rng)
    if want("slide"):
        await slide_cases(page, touch, res, rects)
    if want("two thumbs"):
        await two_thumb_cases(page, touch, res, rects, rng, args.seconds)
    if want("tap"):
        await tap_case(page, touch, res, rects)
    if want("release"):
        await release_cases(page, touch, res, rects)
    if want("break"):
        await break_cases(page, touch, res, rects, rng)
    all_ev = await page.evaluate("window.__keys")
    res.add("no key stuck at the very end", not held_after(all_ev), "every keydown has its keyup", f"{held_after(all_ev) or 'none'}")
    print("  pointer events seen:", await page.evaluate("window.__ptr"), flush=True)
    await ctx.close()


async def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--url", default="https://nova-kart.github.io/")
    ap.add_argument("--patch-from", help="git ref whose touch code (index.html diff + style.css) is put into the page")
    ap.add_argument("--profile", default="android", choices=sorted(PROFILES))
    ap.add_argument("--quick", action="store_true", help="3 s holds instead of 10 s")
    ap.add_argument("--game", action="store_true", help="also run the real game and check the kart's speed")
    ap.add_argument("--game-only", action="store_true")
    ap.add_argument("--only", help="run only cases whose name contains this")
    ap.add_argument("--json", help="write the results to this file")
    ap.add_argument("--shots", help="folder for the --game screenshots (default: the temp folder)")
    args = ap.parse_args()
    global SHOTS
    SHOTS = Path(args.shots) if args.shots else SHOTS
    SHOTS.mkdir(parents=True, exist_ok=True)
    args.url = args.url if args.url.endswith("/") else args.url + "/"
    args.seconds = 3.0 if args.quick else 10.0
    res = Results()
    async with async_playwright() as pw:
        browser = await pw.chromium.launch(channel="chrome", headless=True, args=[
            "--use-angle=d3d11", "--enable-gpu", "--ignore-gpu-blocklist", "--mute-audio",
            "--autoplay-policy=no-user-gesture-required"])
        print(f"touch hold check: {args.url}{' + ' + args.patch_from if args.patch_from else ''}", flush=True)
        if not args.game_only:
            await page_cases(browser, args, args.profile, res)
        if args.game or args.game_only:
            await game_cases(browser, args, args.profile, res)
        await browser.close()
    failed = [r for r in res.rows if not r["pass"]]
    print(f"\n{len(res.rows) - len(failed)}/{len(res.rows)} passed" + (": FAILED " + "; ".join(r["case"] for r in failed) if failed else ""))
    if args.json:
        Path(args.json).write_text(json.dumps({"url": args.url, "patch_from": args.patch_from, "profile": args.profile,
                                               "seconds": args.seconds, "results": res.rows}, indent=1), encoding="utf-8")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
