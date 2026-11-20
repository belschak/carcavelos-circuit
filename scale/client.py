#!/usr/bin/env python
"""Headless test clients for the Carcavelos Circuit online race (M2 scale test).

Opens one game page per client in headless Chromium, waits for a common start moment, closes "Click to play" with
one Space, and records per client: every console line starting with CCNET, all errors/warnings, page crashes, the
frame rate (requestAnimationFrame counter, sampled every few seconds) and a screenshot every N seconds.
The same script runs on a GitHub runner (software WebGL, SwiftShader) and on a laptop (headless Chrome with GPU).

  python scale/client.py --config scale/config.json --runner r1 --out out
  python scale/client.py --base http://127.0.0.1:8000/ --client "?net=local&room=noon&name=A&autopilot=1&autostart=2" \
      --client "?net=local&room=noon&name=B&autopilot=1&autostart=2" --duration 240 --out out

Pages with ?net=local share one browser context (BroadcastChannel only links pages of one profile); every other
client gets its own context (own storage, like a separate machine). Nothing here opens a visible window.
"""

import argparse
import asyncio
import json
import re
import statistics
import sys
import time
from pathlib import Path

from playwright.async_api import async_playwright

PLAY_PANEL_JS = "(() => { const p = document.getElementById('cc-panel-play'); return !!p && !p.hidden; })()"
ERROR_PANEL_JS = ("(() => { const p = document.getElementById('cc-panel-error'); "
                  "return p && !p.hidden ? (p.innerText || 'error').slice(0, 400) : ''; })()")
FPS_INIT_JS = """
(() => {
  window.__ccFrames = 0;
  const tick = () => { window.__ccFrames++; requestAnimationFrame(tick); };
  requestAnimationFrame(tick);
})();
"""
FPS_READ_JS = "[window.__ccFrames || 0, performance.now(), document.visibilityState]"
WEBGL_JS = """
(() => {
  const c = document.createElement('canvas');
  const gl = c.getContext('webgl2') || c.getContext('webgl');
  if (!gl) return 'NO WEBGL';
  const ext = gl.getExtension('WEBGL_debug_renderer_info');
  const r = ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
  return (gl instanceof WebGL2RenderingContext ? 'webgl2 ' : 'webgl1 ') + r;
})()
"""

# Smaller drawing buffer for Unity (the page uses min(devicePixelRatio, 1.5)); screenshots keep the viewport size.
DPR_JS = "Object.defineProperty(window, 'devicePixelRatio', { get: () => %s, configurable: true });"

SOFTWARE_FLAGS = [  # GitHub runner: no GPU, WebGL through SwiftShader on the CPU
    "--use-angle=swiftshader", "--use-gl=angle", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
]
GPU_FLAGS_WINDOWS = ["--use-angle=d3d11", "--enable-gpu", "--ignore-gpu-blocklist"]
# Mesa on Linux (apt mesa-vulkan-drivers / libgl1-mesa-dri): often much faster than SwiftShader on many cores.
LAVAPIPE_FLAGS = ["--use-angle=vulkan", "--use-gl=angle", "--enable-features=Vulkan", "--enable-gpu",
                  "--ignore-gpu-blocklist", "--disable-vulkan-surface"]
LLVMPIPE_FLAGS = ["--use-angle=gl-egl", "--use-gl=angle", "--enable-gpu", "--ignore-gpu-blocklist"]
RENDERER_FLAGS = {"gpu": GPU_FLAGS_WINDOWS if sys.platform == "win32" else ["--enable-gpu", "--ignore-gpu-blocklist"],
                  "swiftshader": SOFTWARE_FLAGS, "lavapipe": LAVAPIPE_FLAGS, "llvmpipe": LLVMPIPE_FLAGS}
COMMON_FLAGS = [
    "--disable-background-timer-throttling", "--disable-renderer-backgrounding",
    "--disable-backgrounding-occluded-windows", "--autoplay-policy=no-user-gesture-required", "--mute-audio",
    "--no-first-run", "--no-default-browser-check",
]


MOBILE = [False]  # --mobile: iPhone 15 profile (touch, Safari UA, DPR 2)
IPHONE_UA = ("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) "
             "Version/18.0 Mobile/15E148 Safari/604.1")


def ctx_options(viewport):
    if MOBILE[0]:
        return {"viewport": viewport, "is_mobile": True, "has_touch": True, "device_scale_factor": 2, "user_agent": IPHONE_UA}
    return {"viewport": viewport}


def now():
    return time.time()


def stamp(t0):
    return f"{now() - t0:8.2f}"


def page_url(base, suffix):
    if suffix.startswith(("http://", "https://")):
        return suffix
    if suffix.startswith("?"):
        return base + suffix
    return base + "?" + suffix


def client_name(suffix, index):
    m = re.search(r"[?&]name=([^&#]+)", suffix)
    return re.sub(r"[^A-Za-z0-9_.-]", "_", m.group(1)) if m else f"client{index + 1}"


class Client:
    def __init__(self, index, entry, base, out_dir, t0):
        if isinstance(entry, dict):
            suffix = entry["url"]
            self.join_delay = float(entry.get("join_delay_s", 0) or 0)
            self.leave_at = float(entry["leave_at_s"]) if entry.get("leave_at_s") else None
            # phone sleep / hidden tab: CDP Page.setWebLifecycleState frozen at freeze_at_s for freeze_for_s
            self.freeze_at = float(entry["freeze_at_s"]) if entry.get("freeze_at_s") else None
            self.freeze_for = float(entry.get("freeze_for_s", 30) or 30)
        else:
            suffix = entry
            self.join_delay = 0.0
            self.leave_at = None
            self.freeze_at = None
            self.freeze_for = 0.0
        self.index = index
        self.suffix = suffix
        self.join_s = None
        self.leave_s = None
        self.url = page_url(base, suffix)
        self.name = client_name(suffix, index)
        self.dir = out_dir / f"{index + 1:02d}-{self.name}"
        self.dir.mkdir(parents=True, exist_ok=True)
        self.t0 = t0
        self.page = None
        self.console = open(self.dir / "console.log", "w", encoding="utf-8")
        self.ccnet = open(self.dir / "ccnet.log", "w", encoding="utf-8")
        self.errors = open(self.dir / "errors.log", "w", encoding="utf-8")
        self.fps = []          # (t since start, fps, visibility)
        self.ccnet_counts = {}
        self.finishes = []     # CCNET FINISH lines (newer builds)
        self.error_count = 0
        self.warning_count = 0
        self.crashed = False
        self.closed = False
        self.load_s = None
        self.space_pressed = False
        self.error_panel = ""
        self.last_frames = None
        self.shots = 0
        self.frozen = False

    def log_line(self, f, text):
        f.write(f"{stamp(self.t0)} {text}\n")
        f.flush()

    def on_console(self, msg):
        text = msg.text.rstrip("\r\n")
        kind = msg.type
        self.log_line(self.console, f"[{kind}] {text}")
        if text.startswith("CCNET"):
            self.log_line(self.ccnet, text)
            parts = text.split()
            key = parts[1] if len(parts) > 1 else "?"
            self.ccnet_counts[key] = self.ccnet_counts.get(key, 0) + 1
            if key == "FINISH":
                self.finishes.append(text)
        if kind in ("error", "warning"):
            if kind == "error":
                self.error_count += 1
            else:
                self.warning_count += 1
            self.log_line(self.errors, f"[{kind}] {text}")

    def on_pageerror(self, err):
        self.error_count += 1
        self.log_line(self.errors, f"[pageerror] {err}")

    def on_crash(self, _page):
        self.crashed = True
        self.log_line(self.errors, "[CRASH] page crashed")
        print(f"{stamp(self.t0)} {self.name}: PAGE CRASHED", flush=True)

    def on_close(self, _page):
        self.closed = True

    def attach(self, page):
        self.page = page
        page.on("console", self.on_console)
        page.on("pageerror", self.on_pageerror)
        page.on("crash", self.on_crash)
        page.on("close", self.on_close)

    def summary(self):
        values = [f for _, f, _ in self.fps if f is not None]
        racing = values[3:] if len(values) > 4 else values  # skip the loading samples
        vis = sorted({v for _, _, v in self.fps if v})
        return {
            "client": self.name, "url": self.suffix, "join_s": self.join_s, "leave_s": self.leave_s,
            "load_s": self.load_s, "space_pressed": self.space_pressed,
            "fps_median": round(statistics.median(racing), 1) if racing else None,
            "fps_min": round(min(racing), 1) if racing else None,
            "fps_max": round(max(racing), 1) if racing else None,
            "visibility": vis, "ccnet": self.ccnet_counts, "finishes": self.finishes,
            "errors": self.error_count, "warnings": self.warning_count, "crashed": self.crashed,
            "error_panel": self.error_panel, "screenshots": self.shots,
        }

    def close_files(self):
        for f in (self.console, self.ccnet, self.errors):
            f.close()


async def wait_until(epoch, label):
    wait = epoch - now()
    if wait > 0:
        print(f"waiting {wait:.0f} s for start_at ({label})", flush=True)
        await asyncio.sleep(wait)
    elif epoch > 0:
        print(f"start_at already passed by {-wait:.0f} s, starting now", flush=True)


async def wait_for_play_panel(client, stop_at):
    """Wait for "Click to play", then one Space closes it (autojoin itself already runs at scene load)."""
    page = client.page
    t_open = now()
    while now() < stop_at and not client.crashed and not client.closed:
        try:
            if MOBILE[0]:
                try:
                    if await page.is_visible("#cc-load-anyway"):
                        await page.tap("#cc-load-anyway")  # the phone "load anyway" gate
                except Exception:
                    pass
            if await page.evaluate(PLAY_PANEL_JS):
                client.load_s = round(now() - t_open, 1)
                if MOBILE[0]:
                    vp = page.viewport_size or {"width": 844, "height": 390}
                    await page.touchscreen.tap(vp["width"] / 2, vp["height"] / 2)  # the page's PLAY button
                else:
                    await page.keyboard.press("Space")
                client.space_pressed = True
                print(f"{stamp(client.t0)} {client.name}: loaded in {client.load_s} s, Space pressed", flush=True)
                return
            panel = await page.evaluate(ERROR_PANEL_JS)
            if panel:
                client.error_panel = panel
                client.log_line(client.errors, f"[error panel] {panel}")
                return
        except Exception as e:
            client.log_line(client.errors, f"[wait] {e}")
            await asyncio.sleep(2)
        await asyncio.sleep(0.5)


async def freeze(client, until):
    """Freeze the page like a phone that went to sleep (no frames, no timers), then wake it up."""
    if client.freeze_at is None:
        return
    await asyncio.sleep(max(0.0, client.t0 + client.freeze_at - now()))
    if now() >= until or client.crashed or client.closed:
        return
    try:
        cdp = await client.page.context.new_cdp_session(client.page)
        client.frozen = True
        await cdp.send("Page.setWebLifecycleState", {"state": "frozen"})
        client.log_line(client.console, f"[ccfreeze] FROZEN for {client.freeze_for:.0f} s")
        print(f"{stamp(client.t0)} {client.name}: FROZEN (phone asleep) for {client.freeze_for:.0f} s", flush=True)
        await asyncio.sleep(client.freeze_for)
        await cdp.send("Page.setWebLifecycleState", {"state": "active"})
        client.frozen = False
        client.log_line(client.console, "[ccfreeze] ACTIVE again")
        print(f"{stamp(client.t0)} {client.name}: ACTIVE again (phone woke up)", flush=True)
    except Exception as e:
        client.frozen = False
        client.log_line(client.errors, f"[freeze] {type(e).__name__} {e}")


async def sample(client, args, until):
    next_fps = now() + args.fps_every
    next_shot = now() + 5
    last = None
    while now() < until and not client.crashed and not client.closed:
        await asyncio.sleep(0.5)
        t = now()
        if client.frozen:
            continue
        if t >= next_fps:
            next_fps += args.fps_every
            try:
                frames, perf, vis = await asyncio.wait_for(client.page.evaluate(FPS_READ_JS), 10)
                fps = None
                if last is not None and perf > last[1]:
                    fps = (frames - last[0]) * 1000.0 / (perf - last[1])
                last = (frames, perf)
                client.fps.append((round(t - client.t0, 1), fps, vis))
                client.log_line(client.console, f"[ccfps] fps={fps if fps is None else round(fps, 1)} vis={vis}")
            except Exception as e:
                client.fps.append((round(t - client.t0, 1), None, "eval-timeout"))
                client.log_line(client.errors, f"[fps] {type(e).__name__} {e}")
        if t >= next_shot:
            next_shot += args.shot_every
            try:
                path = client.dir / f"shot-{int(t - client.t0):04d}s.jpg"
                await client.page.screenshot(path=str(path), type="jpeg", quality=60, timeout=20000)
                client.shots += 1
            except Exception as e:
                client.log_line(client.errors, f"[screenshot] {type(e).__name__} {e}")


async def run_client(client, args, stop_at, new_page):
    """One player: open the page join_delay_s after the start, close it at leave_at_s (or keep it to the end)."""
    if client.join_delay > 0:
        await asyncio.sleep(max(0.0, client.t0 + client.join_delay - now()))
    if now() >= stop_at:
        return
    client.attach(await new_page(client))
    client.join_s = round(now() - client.t0, 1)
    client.log_line(client.console, f"[ccjoin] JOIN {client.url}")
    print(f"{stamp(client.t0)} {client.name}: JOIN (page opened)", flush=True)
    until = stop_at if client.leave_at is None else min(stop_at, client.t0 + client.leave_at)
    try:
        await client.page.goto(client.url, wait_until="load", timeout=180000)
    except Exception as e:
        client.log_line(client.errors, f"[goto] {e}")
    await wait_for_play_panel(client, until)
    await asyncio.gather(sample(client, args, until), freeze(client, until))
    if client.leave_at is not None and now() < stop_at and not client.crashed and not client.closed:
        try:
            await client.page.screenshot(path=str(client.dir / "shot-leave.jpg"), type="jpeg", quality=70, timeout=20000)
        except Exception:
            pass
        try:
            await client.page.close()
        except Exception:
            pass
        client.leave_s = round(now() - client.t0, 1)
        client.log_line(client.console, "[ccleave] LEAVE (page closed)")
        print(f"{stamp(client.t0)} {client.name}: LEAVE (page closed)", flush=True)


def fmt(v):
    return "-" if v is None else str(v)


def write_summary(out_dir, runner, meta, clients):
    rows = [c.summary() for c in clients]
    (out_dir / "summary.json").write_text(json.dumps({"runner": runner, **meta, "clients": rows}, indent=2),
                                          encoding="utf-8")
    lines = [f"### Runner `{runner}`", "",
             f"WebGL: `{meta.get('webgl')}` ({meta.get('renderer')}), netem: `{meta.get('netem') or 'none'}`, "
             f"clients: {len(rows)}, viewport {meta.get('viewport')}, dpr {meta.get('dpr') or 'native'}", "",
             "| client | join s | leave s | load s | fps median | fps min | CCNET lines | finishes | errors | warnings | crashed |",
             "|---|---|---|---|---|---|---|---|---|---|---|"]
    for r in rows:
        cc = " ".join(f"{k}:{v}" for k, v in sorted(r["ccnet"].items())) or "-"
        fin = "; ".join(r["finishes"]) or "-"
        lines.append(f"| {r['client']} | {fmt(r['join_s'])} | {fmt(r['leave_s'])} | {fmt(r['load_s'])} | {fmt(r['fps_median'])} | {fmt(r['fps_min'])} | {cc} | "
                     f"{fin[:200]} | {r['errors']} | {r['warnings']} | {'YES' if r['crashed'] else 'no'} |")
        if r["error_panel"]:
            lines.append(f"| {r['client']} error panel: {r['error_panel'][:150]} |||||||||||")
    (out_dir / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n".join(lines), flush=True)


async def main_async(args):
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    renderer = args.renderer or ("gpu" if sys.platform == "win32" else "swiftshader")
    flags = COMMON_FLAGS + RENDERER_FLAGS[renderer]
    channel = args.channel or ("chrome" if sys.platform == "win32" else "chromium" if renderer != "swiftshader" else None)
    if args.port == 9222:
        sys.exit("port 9222 belongs to the QA Chrome, pick another")

    async with async_playwright() as p:
        launch = {"headless": True, "args": flags}
        if channel:
            launch["channel"] = channel
        if args.port:
            launch["args"] = flags + [f"--remote-debugging-port={args.port}"]
        browser = await p.chromium.launch(**launch)
        viewport = {"width": args.width, "height": args.height}

        probe_ctx = await browser.new_context(viewport=viewport)
        probe = await probe_ctx.new_page()
        webgl = await probe.evaluate(WEBGL_JS)
        await probe_ctx.close()
        print(f"WEBGL renderer: {webgl} (browser {browser.version}, channel {channel or 'bundled'}, "
              f"flags {' '.join(flags)})", flush=True)

        await wait_until(args.start_at, "common start")
        t0 = now()
        stop_at = t0 + args.duration
        clients = [Client(i, s, args.base, out_dir, t0) for i, s in enumerate(args.client)]

        init_js = FPS_INIT_JS + (DPR_JS % args.dpr if args.dpr else "")
        shared = []
        extra_browsers = []

        async def new_page(c):
            if "net=local" in c.suffix and args.contexts != "separate":
                if not shared:
                    ctx = await browser.new_context(**ctx_options(viewport))
                    await ctx.add_init_script(init_js)
                    shared.append(ctx)
                return await shared[0].new_page()
            # Not net=local: by default its own browser process (own GPU process, like a separate machine).
            own = browser
            if args.browsers == "per-client":
                own = await p.chromium.launch(**{**launch, "args": flags})  # no debug port twice
                extra_browsers.append(own)
            ctx = await own.new_context(**ctx_options(viewport))
            await ctx.add_init_script(init_js)
            return await ctx.new_page()

        print(f"{stamp(t0)} starting {len(clients)} client(s) for {args.duration:.0f} s", flush=True)
        tasks = []
        for i, c in enumerate(clients):
            if i and args.stagger:
                c.join_delay = max(c.join_delay, i * args.stagger)
            tasks.append(asyncio.create_task(run_client(c, args, stop_at, new_page)))
        while now() < stop_at:
            await asyncio.sleep(10)
            line = "  ".join(f"{c.name}:{(c.fps[-1][1] or 0):.0f}fps" if c.fps else f"{c.name}:-" for c in clients)
            print(f"{stamp(t0)} {line}", flush=True)
        await asyncio.gather(*tasks, return_exceptions=True)
        for c in clients:
            if c.page is None or c.closed:
                continue
            try:
                await c.page.screenshot(path=str(c.dir / "shot-final.jpg"), type="jpeg", quality=70, timeout=20000)
            except Exception:
                pass
        meta = {"webgl": webgl, "browser": browser.version, "netem": args.netem, "start_epoch": round(t0, 1),
                "duration_s": args.duration, "viewport": f"{args.width}x{args.height}", "dpr": args.dpr, "renderer": renderer}
        write_summary(out_dir, args.runner or "local", meta, clients)
        for c in clients:
            c.close_files()
        try:
            for b in extra_browsers + [browser]:
                await asyncio.wait_for(b.close(), 30)
        except Exception:
            pass


def parse_args():
    ap = argparse.ArgumentParser(description="Headless Carcavelos Circuit test clients (scale test).")
    ap.add_argument("--config", help="scale/config.json; with --runner it supplies clients, start_at, duration")
    ap.add_argument("--runner", help="runner name in the config")
    ap.add_argument("--base", default="http://127.0.0.1:8000/", help="served build (default http://127.0.0.1:8000/)")
    ap.add_argument("--client", action="append", default=[], metavar="SUFFIX", help="URL suffix per client, repeat")
    ap.add_argument("--start-at", type=float, default=None, help="unix epoch to open the pages at (0 = now)")
    ap.add_argument("--duration", type=float, default=None, help="seconds from start to the end (default 240)")
    ap.add_argument("--fps-every", type=float, default=None, help="fps sample interval s (default 10)")
    ap.add_argument("--shot-every", type=float, default=None, help="screenshot interval s (default 30)")
    ap.add_argument("--stagger", type=float, default=None, help="seconds between opening pages (default 0)")
    ap.add_argument("--width", type=int, default=None)
    ap.add_argument("--height", type=int, default=None)
    ap.add_argument("--dpr", type=float, default=None, help="fake devicePixelRatio, e.g. 0.25 (default: real one)")
    ap.add_argument("--browsers", choices=["per-client", "shared"], default="per-client",
                    help="clients without net=local: one browser process each (default) or all in one browser")
    ap.add_argument("--contexts", choices=["auto", "separate"], default="auto",
                    help="auto: net=local pages share one context; separate: one context per client")
    ap.add_argument("--renderer", choices=sorted(RENDERER_FLAGS),
                    help="gpu (default on Windows), swiftshader (default elsewhere), lavapipe / llvmpipe (Mesa, Linux)")
    ap.add_argument("--channel", help="playwright channel (default: chrome on Windows, bundled Chromium elsewhere)")
    ap.add_argument("--port", type=int, default=0, help="optional remote debugging port (never 9222)")
    ap.add_argument("--netem", default="", help="only noted in the summary (the workflow applies it with tc)")
    ap.add_argument("--out", default="scale-out", help="output folder")
    ap.add_argument("--mobile", action="store_true", help="iPhone 15 profile: touch, Safari user agent, DPR 2")
    args = ap.parse_args()

    cfg, runner = {}, {}
    if args.config:
        cfg = json.loads(Path(args.config).read_text(encoding="utf-8"))
        if args.runner:
            matches = [r for r in cfg.get("runners", []) if r.get("name") == args.runner]
            if not matches:
                sys.exit(f"runner {args.runner} not in {args.config}")
            runner = matches[0]
    if not args.client:
        args.client = runner.get("clients", [])
    if not args.client:
        sys.exit("no clients (use --client or --config + --runner)")

    def pick(value, key, default):
        if value is not None:
            return value
        if key in runner:
            return runner[key]
        return cfg.get(key, default)

    args.start_at = float(pick(args.start_at, "start_at_epoch", 0))
    args.duration = float(pick(args.duration, "duration_s", 240))
    args.fps_every = float(pick(args.fps_every, "fps_every_s", 10))
    args.shot_every = float(pick(args.shot_every, "screenshot_every_s", 30))
    args.stagger = float(pick(args.stagger, "stagger_s", 0))
    args.dpr = pick(args.dpr, "dpr", None)
    args.renderer = pick(args.renderer, "renderer", None)
    args.channel = pick(args.channel, "channel", None)
    vp = pick(None, "viewport", [960, 540])
    args.width = args.width or int(vp[0])
    args.height = args.height or int(vp[1])
    args.netem = args.netem or runner.get("netem", "")
    return args


def main():
    args = parse_args()
    MOBILE[0] = args.mobile
    asyncio.run(main_async(args))


if __name__ == "__main__":
    main()
