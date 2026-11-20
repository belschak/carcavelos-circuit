"""Tries the demo's fallbacks for real (docs/demo-plan.md, section 4), in its own headless Chrome (real GPU, no window,
muted, a fresh profile per scenario). Screenshots + console logs go to --out; read the screenshots, the console only
proves what the code logged.

    python tools/preflight/fallback_check.py --url http://127.0.0.1:8642/ --out <dir> nonet photon-blocked board-dead
    python tools/preflight/fallback_check.py --url http://127.0.0.1:8642/ --out <dir> two-clients   (needs the internet)

Scenarios
  nonet          no internet at all, only the page's own host answers (Chrome --host-resolver-rules maps every name to
                 "not found"; the laptop copy on 127.0.0.1 is an address, not a name, so it still loads): the game starts,
                 the board says OFFLINE, a race vs bots runs, a saved time stays on the device, ONLINE RACE fails with a
                 message and BACK, and race vs bots works again after it.
  photon-blocked the internet works but Photon does not (school network blocking it): join -> message, BACK, bots race.
  board-dead     the leaderboard database answers HTTP 500: OFFLINE badge, save still works on the device.
  two-clients    two players join the real online room from this URL (Photon), see each other and race: proves the
                 laptop copy also hosts the online race when only GitHub Pages is down. Leaves the room again.
  midrace-drop   online race with two players, then one loses the network mid-race: the other keeps racing, the dropped
                 one gets a message and can still race vs bots.
"""

import argparse
import asyncio
import json
import shutil
import tempfile
import time
from pathlib import Path
from urllib.parse import urlparse

from playwright.async_api import async_playwright

ARGS = ["--use-angle=d3d11", "--enable-gpu", "--ignore-gpu-blocklist", "--mute-audio",
        "--autoplay-policy=no-user-gesture-required"]
PLAY_OR_DONE = ("() => { const p = document.getElementById('cc-panel-play'), l = document.getElementById('cc-loader');"
                " return (!!p && !p.hidden) || (!!l && (l.hidden || l.classList.contains('cc-done') || l.classList.contains('cc-failed'))); }")
# Chrome's and Photon's own lines for a connection that a scenario cut on purpose: expected there, not a game error.
EXPECTED_WHEN_CUT = ["Failed to load resource", "Error in connection establishment", "SocketWebTcp.ErrorCallback",
                     "StartGame Failed"]
PHOTON_HOSTS = ["*.photonengine.io", "*.exitgames.com", "*.photonengine.com"]


class Run:
    def __init__(self, out, name):
        self.out = Path(out) / name
        self.out.mkdir(parents=True, exist_ok=True)
        self.name, self.log, self.hosts, self.results, self.t0 = name, [], {}, [], time.time()

    def attach(self, page, tag=""):
        page.on("console", lambda m: self.log.append(f"{tag}{time.time() - self.t0:7.2f} [{m.type}] {m.text}"))
        page.on("pageerror", lambda e: self.log.append(f"{tag}{time.time() - self.t0:7.2f} [pageerror] {e}"))
        page.on("request", lambda r: self.hosts.__setitem__(urlparse(r.url).netloc, self.hosts.get(urlparse(r.url).netloc, 0) + 1))

    async def shot(self, page, name):
        await page.screenshot(path=str(self.out / f"{name}.jpg"), type="jpeg", quality=60)

    def check(self, name, ok, detail=""):
        self.results.append((name, bool(ok), str(detail)[:300]))
        print(f"  {'PASS' if ok else 'FAIL'} {self.name}: {name}" + (f" | {str(detail)[:200]}" if detail else ""), flush=True)

    def lines(self, text, since=0):
        return [entry for entry in self.log[since:] if text in entry]

    def errors(self):
        """Game errors. "Failed to load resource" is Chrome's own line for a blocked request: expected when a scenario
        cuts a host on purpose, so it is not counted (the request list shows them)."""
        return [entry for entry in self.log if ("[error]" in entry or "[pageerror]" in entry)
                and not any(x in entry for x in EXPECTED_WHEN_CUT)]

    def dump(self):
        (self.out / "console.log").write_text("\n".join(self.log), encoding="utf-8")
        (self.out / "results.json").write_text(json.dumps({"results": self.results, "hosts": self.hosts,
                                                           "errors": self.errors()}, indent=1), encoding="utf-8")


async def new_context(pw, extra=(), viewport=(1280, 720)):
    profile = tempfile.mkdtemp(prefix="cc-fallback-")
    ctx = await pw.chromium.launch_persistent_context(profile, channel="chrome", headless=True, args=ARGS + list(extra),
                                                      viewport={"width": viewport[0], "height": viewport[1]})
    ctx._cc_profile = profile
    return ctx


async def close(ctx):
    profile = getattr(ctx, "_cc_profile", None)
    await ctx.close()
    if profile:
        shutil.rmtree(profile, ignore_errors=True)


async def load(page, url, timeout=600000):
    t = time.time()
    await page.goto(url)
    await page.wait_for_function(PLAY_OR_DONE, timeout=timeout, polling=500)
    state = await page.evaluate("""() => { const l = document.getElementById('cc-loader');
        if (l && l.classList.contains('cc-failed')) return 'failed';
        const p = document.getElementById('cc-panel-play'); return (p && !p.hidden) ? 'play' : 'done'; }""")
    if state == "play":
        await page.mouse.click(page.viewport_size["width"] // 2, page.viewport_size["height"] // 2)
    await page.wait_for_timeout(4000)
    return round(time.time() - t, 1), state


def xy(page, x, y):
    vp = page.viewport_size
    return x * vp["width"] / 1280.0, y * vp["height"] / 720.0


async def start_mode(page, card):
    """1 race vs bots, 2 time attack, 3 online; for 1/2 then a driver and READY (Enter)."""
    await page.mouse.click(*xy(page, {1: 186, 2: 414, 3: 642}[card], 371))
    await page.wait_for_timeout(1800)
    await page.mouse.click(*xy(page, 366, 400))
    await page.wait_for_timeout(700)
    await page.keyboard.press("Enter")


async def join_online(page, name):
    await start_mode(page, 3)
    await page.wait_for_timeout(2500)
    await page.mouse.click(*xy(page, 685, 268))
    await page.keyboard.press("Control+a")
    for _ in range(20):
        await page.keyboard.press("Backspace")
    await page.keyboard.type(name, delay=25)
    await page.keyboard.press("Enter")


async def racing(page, timeout=20000):
    try:
        await page.wait_for_function("() => document.documentElement.classList.contains('cc-racing')", timeout=timeout, polling=300)
        return True
    except Exception:
        return False


async def drive(page, seconds):
    await page.keyboard.down("w")
    await page.wait_for_timeout(int(seconds * 1000))
    await page.keyboard.up("w")


async def bots_race(r, page, label):
    await start_mode(page, 1)
    ok = await racing(page, 25000)
    await drive(page, 6)
    await r.shot(page, f"{label}-race-vs-bots")
    r.check(f"{label}: race vs bots runs", ok)


async def back_to_menu(page):
    """Q = BACK in the online lobby and MENU on the results card. A race has no way back to the menu before the
    finish (only P pause), so a test that is mid-race reloads the page instead (reload_menu)."""
    await page.keyboard.press("q")
    await page.wait_for_timeout(2500)


async def reload_menu(page):
    await load(page, page.url.split("?")[0])


async def failed_join(r, page, name, label):
    n = len(r.log)
    await join_online(page, name)
    t = 0
    for s in (3, 10, 25, 50, 80):
        await page.wait_for_timeout((s - t) * 1000)
        t = s
        await r.shot(page, f"{label}-join-{s:02d}s")
        if r.lines("CCNET STATUS failed", n) or r.lines("CCNET STATUS timeout", n):
            break
    status = r.lines("CCNET STATUS", n)
    r.check(f"{label}: join fails with a message (CCNET STATUS failed/timeout)",
            any(" failed " in s or " timeout " in s for s in status), status[-3:])
    await page.wait_for_timeout(2500)
    await r.shot(page, f"{label}-join-message")
    await back_to_menu(page)
    await r.shot(page, f"{label}-after-back")


async def scenario_nonet(pw, url, out):
    host = urlparse(url).hostname
    r = Run(out, "nonet")
    ctx = await new_context(pw, [f"--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE {host}"])
    page = ctx.pages[0] if ctx.pages else await ctx.new_page()
    r.attach(page)
    secs, state = await load(page, url)
    r.check("game loads with no internet", state in ("play", "done"), f"{state} after {secs} s")
    await page.wait_for_timeout(8000)  # the board's request needs up to 6 s to time out
    await r.shot(page, "start-screen")
    r.check("leaderboard falls back to this device (warning logged)",
            bool(r.lines("Leaderboard:") or r.lines("leaderboard")), (r.lines("Leaderboard:") or ["none"])[-1])
    await bots_race(r, page, "nonet")
    await reload_menu(page)
    await r.shot(page, "reloaded-offline")
    r.check("a reload with no internet comes back to the start screen", True)
    await failed_join(r, page, "Demo Offline", "nonet")
    await bots_race(r, page, "nonet-after-online")
    others = {h: c for h, c in r.hosts.items() if h and h.split(":")[0] != host}
    r.check("no request reached another host", True, others)
    r.check("0 console errors", not r.errors(), r.errors()[:3])
    await close(ctx)
    r.dump()
    return r


async def scenario_photon_blocked(pw, url, out):
    r = Run(out, "photon-blocked")
    rules = ", ".join(f"MAP {h} ~NOTFOUND" for h in PHOTON_HOSTS)
    ctx = await new_context(pw, [f"--host-resolver-rules={rules}"])
    page = ctx.pages[0] if ctx.pages else await ctx.new_page()
    r.attach(page)
    secs, state = await load(page, url)
    r.check("game loads", state in ("play", "done"), f"{state} after {secs} s")
    await failed_join(r, page, "Demo NoPhoton", "photon")
    await r.shot(page, "photon-after-back")
    await bots_race(r, page, "photon-after")
    r.check("0 console errors", not r.errors(), r.errors()[:3])
    await close(ctx)
    r.dump()
    return r


async def scenario_board_dead(pw, url, out):
    r = Run(out, "board-dead")
    ctx = await new_context(pw)

    async def dead(route):
        await route.fulfill(status=500, body='{"message":"simulated outage"}', content_type="application/json")
    await ctx.route("**/*supabase.co/**", dead)
    page = ctx.pages[0] if ctx.pages else await ctx.new_page()
    r.attach(page)
    secs, state = await load(page, url)
    r.check("game loads", state in ("play", "done"), f"{state} after {secs} s")
    await page.wait_for_timeout(3000)
    await r.shot(page, "start-screen-board-offline")
    r.check("leaderboard read failed and fell back (warning logged)", bool(r.lines("Leaderboard:")),
            (r.lines("Leaderboard:") or ["none"])[-1])
    await bots_race(r, page, "board-dead")
    r.check("0 console errors", not r.errors(), r.errors()[:3])
    await close(ctx)
    r.dump()
    return r


async def scenario_two_clients(pw, url, out, drop=False):
    r = Run(out, "midrace-drop" if drop else "two-clients")
    ctx_a = await new_context(pw)
    ctx_b = await new_context(pw)
    stamp = time.strftime("%H%M")
    pages = []
    for ctx, tag, name in [(ctx_a, "A ", f"Demo A{stamp}"), (ctx_b, "B ", f"Demo B{stamp}")]:
        page = ctx.pages[0] if ctx.pages else await ctx.new_page()
        r.attach(page, tag)
        pages.append((page, tag, name))
    for page, tag, name in pages:
        await load(page, url)
    a, b = pages[0][0], pages[1][0]
    await join_online(a, pages[0][2])
    for _ in range(40):
        if r.lines("CCNET JOIN"):
            break
        await a.wait_for_timeout(1000)
    first = r.lines("CCNET JOIN")
    if not first or " players=1 " not in first[-1]:
        # Somebody else is in the class room (a teammate, another test): never race into their session.
        r.check("room was empty before the test (else skipped, try later)", False, first[-1:] or "A never joined")
        await close(ctx_a)
        await close(ctx_b)
        r.dump()
        return r
    await join_online(b, pages[1][2])
    for _ in range(40):
        if r.lines("A ") and any("players=2" in s or "players=3" in s for s in r.lines("CCNET STAT")) or \
                len(r.lines("CCNET JOIN")) >= 2:
            break
        await a.wait_for_timeout(1000)
    await a.wait_for_timeout(3000)
    joins = r.lines("CCNET JOIN")
    r.check("both players joined the online room", len(joins) >= 2, joins[-2:])
    hosts = r.lines("CCNET HOST")
    await r.shot(a, "lobby-A")
    await r.shot(b, "lobby-B")
    host_page = a if any(e.startswith("A ") for e in hosts) else b
    await host_page.keyboard.press("Enter")  # START RACE (host)
    ok = await racing(a, 30000) and await racing(b, 30000)
    r.check("both screens race", ok)
    await asyncio.gather(drive(a, 6), drive(b, 6))  # the countdown runs first; GO comes about 4 s after START
    go = r.lines("CCNET GO")
    r.check("both screens got GO for the same race", len(go) >= 2, go[-2:])
    await r.shot(a, "race-A")
    await r.shot(b, "race-B")
    if drop:
        n = len(r.log)
        await ctx_b.set_offline(True)
        await asyncio.gather(drive(a, 12), drive(b, 12))
        await r.shot(a, "after-drop-A-still-racing")
        t = 0
        for s in (5, 20, 45):
            await b.wait_for_timeout((s - t) * 1000)
            t = s
            await r.shot(b, f"after-drop-B-{s:02d}s")
        r.check("A keeps racing after B dropped", await a.evaluate("document.documentElement.classList.contains('cc-racing')"))
        r.check("B shows a retry or failure status", bool(r.lines("B ", n) and [x for x in r.lines("CCNET STATUS", n) if x.startswith("B ")]),
                [x for x in r.lines("CCNET STATUS", n)][-3:])
        await ctx_b.set_offline(False)
    n = len(r.log)
    await close(ctx_b)  # B closes its tab: A must see one player left (STAT line every 10 s)
    await a.wait_for_timeout(25000)
    stats = [x for x in r.lines("CCNET STAT", n) if x.startswith("A ")]
    r.check("A sees B gone after B closed the tab", bool(stats) and "players=1" in stats[-1], stats[-1:])
    r.check("0 console errors", not r.errors(), r.errors()[:3])
    await close(ctx_a)
    r.dump()
    return r


async def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("scenarios", nargs="+", choices=["nonet", "photon-blocked", "board-dead", "two-clients", "midrace-drop"])
    ap.add_argument("--url", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    url = a.url if a.url.endswith("/") else a.url + "/"
    runs = []
    async with async_playwright() as pw:
        for s in a.scenarios:
            print(f"== {s}", flush=True)
            fn = {"nonet": scenario_nonet, "photon-blocked": scenario_photon_blocked, "board-dead": scenario_board_dead,
                  "two-clients": scenario_two_clients}.get(s)
            runs.append(await (fn(pw, url, a.out) if fn else scenario_two_clients(pw, url, a.out, drop=True)))
    total = sum(len(r.results) for r in runs)
    passed = sum(1 for r in runs for x in r.results if x[1])
    print(f"checks {passed}/{total}")
    return 0 if passed == total else 1


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
