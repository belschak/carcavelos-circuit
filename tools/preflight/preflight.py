"""One-minute check before the demo (docs/demo-plan.md): every dependency of the presentation gets GREEN, AMBER or RED,
and every non-green line says what to do. Run it 15 minutes before our turn, on the demo laptop, on the room's network.

    python tools/preflight/preflight.py                  live site + laptop copy + this laptop (about 30-60 s)
    python tools/preflight/preflight.py --no-join        the same without joining the online room
    python tools/preflight/preflight.py --copy           boot the laptop copy instead of the live site (no internet)

What it checks
  live site      both addresses answer and serve the same build; the newest main commit is the deployed one (GitHub)
  laptop copy    plan B (tools/preflight/presenter.py) is complete and holds the same build as the live site
  game boots     the real game in its own headless Chrome (real GPU, muted, no window, own profile so its cache stays
                 warm): time to the start screen, hardware WebGL (not the SwiftShader software fallback), console errors
  leaderboard    the game's own request to the online leaderboard (Supabase) answers 200
  online race    the game joins the class room on Photon as "Preflight" and leaves again; says how many others are in it
  this laptop    sound output (device, mute, volume), charger, screens and resolution, Chrome version

Exit code 0 = no RED. Needs Python 3 and Playwright (python -m pip install playwright==1.65.2; it uses the installed
Google Chrome, no browser download). Standard library otherwise.
"""

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
LIVE = "https://nova-kart.github.io/"
OLD = "https://belschak.github.io/carcavelos-circuit/"
REPO = "belschak/carcavelos-circuit"
MIRROR = "nova-kart/nova-kart.github.io"
PROFILE = Path(os.environ.get("LOCALAPPDATA", tempfile.gettempdir())) / "carcavelos-preflight" / "chrome"
DEFAULT_COPY = Path(os.environ.get("USERPROFILE", str(Path.home()))) / "Documents" / "nova" / "carcavelos-presenter"
NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)

GREEN, AMBER, RED, INFO = "GREEN", "AMBER", "RED", "INFO"
COLORS = {GREEN: "\033[92m", AMBER: "\033[93m", RED: "\033[91m", INFO: "\033[96m"}
results = []
lock = threading.Lock()
OFFLINE_HERE = False  # the live site was unreachable: board and Photon failures are then expected, not a new problem
NO_INTERNET = False  # --simulate-no-internet: every request to a name fails, as on a dead network (to test this script)


def report(level, what, detail, fix=""):
    with lock:
        results.append((level, what, detail, fix))
        color = COLORS[level] if sys.stdout.isatty() or os.environ.get("WT_SESSION") else ""
        end = "\033[0m" if color else ""
        print(f"{color}{level:5}{end}  {what:14} {detail}" + (f"\n{'':22}-> {fix}" if fix else ""), flush=True)


def get(url, timeout=8, as_json=True):
    if NO_INTERNET:
        raise urllib.error.URLError("simulated: no internet")
    req = urllib.request.Request(url, headers={"User-Agent": "carcavelos-preflight", "Cache-Control": "no-cache",
                                               "Accept": "application/vnd.github+json"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        body = resp.read()
    return json.loads(body.decode("utf-8")) if as_json else body


# ---------------------------------------------------------------- live site + deploy

def check_live():
    builds = {}
    for url in (LIVE, OLD):
        t = time.time()
        try:
            builds[url] = get(url + "version.json?t=%d" % time.time()).get("build")
            ms = (time.time() - t) * 1000
            if url == LIVE:
                level = GREEN if ms < 1500 else AMBER
                report(level, "live site", f"{url} answers, build {builds[url]} ({ms:.0f} ms)",
                       "" if level == GREEN else "slow network: keep the preloaded tab, do not reload; or the phone hotspot")
        except Exception as e:  # noqa: BLE001
            builds[url] = None
            if url == LIVE:
                report(RED, "live site", f"{url} not reachable ({e.__class__.__name__}: {str(e)[:80]})",
                       "no internet here or GitHub Pages down: use the phone hotspot, else plan B (Carcavelos OFFLINE)")
    if builds.get(LIVE) and builds.get(OLD) and builds[LIVE] != builds[OLD]:
        report(AMBER, "live site", f"the two addresses serve different builds ({builds[LIVE]} vs {builds[OLD]})",
               "a deploy is still running: wait 2 minutes and run again; demo only from nova-kart.github.io")
    elif builds.get(LIVE) and builds.get(OLD):
        report(GREEN, "mirror", f"{OLD} serves the same build")
    try:
        main = get(f"https://api.github.com/repos/{REPO}/commits/main")["sha"][:7]
        deployed_msg = get(f"https://api.github.com/repos/{MIRROR}/commits/main")["commit"]["message"]
        deployed = deployed_msg.rsplit("@", 1)[-1].strip()[:7]
        if deployed == main:
            report(GREEN, "deploy", f"main {main} is the deployed build")
        else:
            runs = get(f"https://api.github.com/repos/{REPO}/actions/workflows/web-build.yml/runs?branch=main&per_page=5")
            latest = next((r for r in runs.get("workflow_runs", []) if r["head_sha"].startswith(main)), None)
            state = (latest or {}).get("status", "?") + "/" + str((latest or {}).get("conclusion"))
            level = AMBER if latest and latest.get("status") != "completed" else RED
            report(level, "deploy", f"main is {main} but the live site is {deployed} (Web build: {state})",
                   "wait for Actions > Web build to finish" if level == AMBER else
                   "Actions > Web build failed for the newest main commit: the live site is the previous build, "
                   "which still works. Do not merge anything more today")
        return builds.get(LIVE), main
    except Exception as e:  # noqa: BLE001
        report(INFO, "deploy", f"GitHub API not reachable ({e.__class__.__name__}); live build {builds.get(LIVE)}")
        return builds.get(LIVE), None


def check_copy(copy_dir, live_build):
    b = Path(copy_dir) / "build"
    version = None
    try:
        version = json.loads((b / "version.json").read_text(encoding="utf-8")).get("build")
    except (OSError, ValueError):
        pass
    files = [f.name for f in (b / "Build").glob("*")] if (b / "Build").is_dir() else []
    complete = version and all(any(n.endswith(k) for n in files) for k in [".wasm", ".data", ".bundle", ".loader.js"])
    if not complete:
        report(RED, "laptop copy", f"no complete plan B copy in {b}",
               "python tools/preflight/presenter.py install   (needs internet once, about 140 MB)")
        return None
    if live_build and version != live_build:
        report(AMBER, "laptop copy", f"holds build {version}, the live site is {live_build}",
               "right after a merge the copy can be newer than the site for a few minutes (GitHub Pages publishes "
               "last): run again in 5 minutes; still different: python tools/preflight/presenter.py update")
    else:
        report(GREEN, "laptop copy", f"plan B ready, build {version}" + ("" if live_build else " (live site unknown)"))
    return version


# ---------------------------------------------------------------- the game itself

READY = ("() => { const p = document.getElementById('cc-panel-play'), l = document.getElementById('cc-loader');"
         " return (!!p && !p.hidden) || (!!l && (l.hidden || l.classList.contains('cc-done') || l.classList.contains('cc-failed'))); }")
RENDERER = ("() => { const c = document.createElement('canvas'); const g = c.getContext('webgl2');"
            " if (!g) return 'NO WEBGL2'; const d = g.getExtension('WEBGL_debug_renderer_info');"
            " return d ? g.getParameter(d.UNMASKED_RENDERER_WEBGL) : g.getParameter(g.RENDERER); }")


def check_game(url, join, timeout_s):
    try:
        from playwright.sync_api import sync_playwright
    except ImportError:
        report(RED, "game boots", "Playwright is not installed, so the game check cannot run",
               "python -m pip install playwright==1.65.2   (then run again)")
        return
    PROFILE.mkdir(parents=True, exist_ok=True)
    log, supa = [], []
    target = url + (("?room=carcavelos&name=Preflight") if join else "")
    with sync_playwright() as pw:
        try:
            ctx = pw.chromium.launch_persistent_context(
                str(PROFILE), channel="chrome", headless=True, viewport={"width": 1280, "height": 720},
                args=["--use-angle=d3d11", "--enable-gpu", "--mute-audio", "--autoplay-policy=no-user-gesture-required"]
                + (["--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE 127.0.0.1"] if NO_INTERNET else []))
        except Exception as e:  # noqa: BLE001
            msg = str(e)
            if "ProcessSingleton" in msg or "profile" in msg.lower():
                report(RED, "game boots", "the preflight Chrome profile is in use (a preflight still running?)",
                       "close other preflight windows and run again")
            else:
                report(RED, "game boots", f"Chrome did not start ({msg[:120]})", "is Google Chrome installed and updated?")
            return
        page = ctx.pages[0] if ctx.pages else ctx.new_page()
        t0 = time.time()
        page.on("console", lambda m: log.append((time.time() - t0, m.type, m.text)))
        page.on("pageerror", lambda e: log.append((time.time() - t0, "pageerror", str(e))))
        page.on("response", lambda r: supa.append((r.url, r.status)) if "supabase.co" in r.url else None)
        page.on("requestfailed", lambda r: supa.append((r.url, "failed: " + str(r.failure))) if "supabase.co" in r.url else None)
        try:
            page.goto(target, timeout=60000)
            renderer = page.evaluate(RENDERER)
            if "swiftshader" in renderer.lower() or "llvmpipe" in renderer.lower() or "NO WEBGL2" in renderer:
                report(RED, "GPU", f"WebGL renders on the CPU ({renderer})",
                       "Chrome > Settings > System > 'Use graphics acceleration when available' ON, restart Chrome")
            else:
                report(GREEN, "GPU", f"WebGL 2 on {renderer.split(',')[1].strip() if ',' in renderer else renderer}")
            page.wait_for_function(READY, timeout=timeout_s * 1000, polling=500)
        except Exception as e:  # noqa: BLE001
            report(RED, "game boots", f"no start screen after {time.time() - t0:.0f} s ({e.__class__.__name__})",
                   "first visit on a slow network downloads 53 MB: let it finish in the demo Chrome, or use plan B")
            ctx.close()
            return
        state = page.evaluate("() => { const l = document.getElementById('cc-loader');"
                              " return l && l.classList.contains('cc-failed') ? (document.getElementById('cc-panel-error') || {}).innerText || 'failed' : 'ok'; }")
        if state != "ok":
            report(RED, "game boots", "the loader shows an error: " + " ".join(state.split())[:160],
                   "reload once; if it stays: plan B (Carcavelos OFFLINE)")
            ctx.close()
            return
        loaded = time.time() - t0
        play = page.evaluate("() => { const p = document.getElementById('cc-panel-play'); return !!p && !p.hidden; }")
        if play:
            page.mouse.click(640, 360)
        report(GREEN if loaded < 30 else AMBER, "game boots", f"start screen after {loaded:.0f} s",
               "" if loaded < 30 else "slow load: open the demo tab now and leave it on the start screen")
        joined, failed, deadline = None, None, time.time() + (45 if join else 10)
        while time.time() < deadline:
            page.wait_for_timeout(500)
            lines = [x[2] for x in log]
            joined = next((x for x in lines if x.startswith("CCNET JOIN")), None)
            failed = next((x for x in lines if x.startswith("CCNET STATUS failed") or x.startswith("CCNET STATUS full")
                           or x.startswith("CCNET STATUS timeout")), None)
            board_done = any(s for s in supa if "/rest/v1/" in s[0]) or any("Leaderboard:" in x for x in lines)
            if (not join or joined or failed) and board_done:
                break
        # leaderboard: the game's own read of the top list
        reads = [s for s in supa if "/rest/v1/" in s[0]]
        warn = next((x[2] for x in log if "Leaderboard:" in x[2]), None)
        if reads and all(s[1] == 200 for s in reads) and not warn:
            report(GREEN, "leaderboard", f"online board answered ({len(reads)} request(s), HTTP 200)")
        elif (reads or warn) and OFFLINE_HERE:
            report(AMBER, "leaderboard", "no internet: the board shows this device's times (expected)",
                   "fine for the demo; say 'without internet the game keeps the times on this laptop'")
        elif reads or warn:
            report(RED, "leaderboard", f"online board failed: {(warn or str(reads[-1]))[:140]}",
                   "supabase.com > project > if 'Paused': Restore (takes ~2 min). The game falls back to this "
                   "device's times, so the demo can go on: say 'the database is not reachable'")
        else:
            report(AMBER, "leaderboard", "the game made no leaderboard request (an old build without the online board?)",
                   "check that the start screen shows the board with ONLINE")
        if join:
            if joined:
                m = re.search(r"players=(\d+)", joined)
                others = int(m.group(1)) - 1 if m else 0
                report(GREEN if others == 0 else AMBER, "online race",
                       f"joined the class room on Photon ({others} other player(s) in it), left again",
                       "" if others == 0 else "someone is in the room: before the demo, close every rehearsal tab "
                                              "(a forgotten tab is a player); during class this is normal")
            elif failed and OFFLINE_HERE:
                report(RED, "online race", "no internet, so no online race (the game says so and offers BACK)",
                       "phone hotspot for the demo laptop and run again; else skip the online race, show race vs bots")
            elif failed:
                report(RED, "online race", f"could not join: {failed[:140]}",
                       "this network blocks Photon or Photon is down: phone hotspot for the demo laptop; if still red, "
                       "skip the online race and show race vs bots (works without internet)")
            else:
                report(RED, "online race", "no answer from Photon within 45 s",
                       "slow or filtered network: phone hotspot; else show race vs bots")
        cut = ["Failed to load resource"] + (["Error in connection establishment", "SocketWebTcp.ErrorCallback",
                                              "StartGame Failed"] if OFFLINE_HERE or failed else [])
        errors = [x for x in log if x[1] in ("error", "pageerror") and not any(c in x[2] for c in cut)]
        if errors:
            report(AMBER, "console", f"{len(errors)} error line(s), first: {errors[0][2][:120]}",
                   "usually harmless if everything above is green; note it for the team")
        else:
            report(GREEN, "console", "0 errors in the browser console")
        ctx.close()  # leaves the online room


# ---------------------------------------------------------------- this laptop

AUDIO_PS = r'''
$code = @"
using System; using System.Runtime.InteropServices;
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume { int f1(IntPtr p); int f2(IntPtr p); int GetChannelCount(out uint c);
  int f4(float l, ref Guid g); int f5(float l, ref Guid g); int GetMasterVolumeLevel(out float l);
  int GetMasterVolumeLevelScalar(out float l); int f8(uint n, float l, ref Guid g); int f9(uint n, float l, ref Guid g);
  int f10(uint n, out float l); int f11(uint n, out float l); int SetMute(bool m, ref Guid g); int GetMute(out bool m); }
[ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropertyStore { int GetCount(out int c); int GetAt(int i, out PK k); int GetValue(ref PK k, out PV v); }
[StructLayout(LayoutKind.Sequential)] struct PK { public Guid f; public int p; }
[StructLayout(LayoutKind.Sequential)] struct PV { public short vt; short a, b, c; public IntPtr p; IntPtr q; }
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice { int Activate(ref Guid id, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
  int OpenPropertyStore(int access, out IPropertyStore ps); }
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator { int f1(int a, int b, out IntPtr c); int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice d); }
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDE {}
public static class CcAudio {
  public static string Get() {
    var e = (IMMDeviceEnumerator)new MMDE(); IMMDevice d;
    if (e.GetDefaultAudioEndpoint(0, 0, out d) != 0 || d == null) return "NONE";
    Guid iid = typeof(IAudioEndpointVolume).GUID; object o; d.Activate(ref iid, 23, IntPtr.Zero, out o);
    var v = (IAudioEndpointVolume)o; float level; bool mute; v.GetMasterVolumeLevelScalar(out level); v.GetMute(out mute);
    IPropertyStore ps; d.OpenPropertyStore(0, out ps);
    PK k = new PK { f = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), p = 14 }; PV pv; ps.GetValue(ref k, out pv);
    string name = pv.vt == 31 ? Marshal.PtrToStringUni(pv.p) : "?";
    return name + "|" + (int)Math.Round(level * 100) + "|" + mute;
  }
}
"@
Add-Type -TypeDefinition $code -Language CSharp
[CcAudio]::Get()
'''

LAPTOP_PS = r'''
$b = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue | Select-Object -First 1
if ($b) { "BATTERY|" + $b.BatteryStatus + "|" + $b.EstimatedChargeRemaining } else { "BATTERY|none|" }
Add-Type -AssemblyName System.Windows.Forms
foreach ($s in [System.Windows.Forms.Screen]::AllScreens) { "SCREEN|" + $s.Primary + "|" + $s.Bounds.Width + "x" + $s.Bounds.Height }
'''


def ps(script, timeout=25):
    return subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script],
                          capture_output=True, text=True, timeout=timeout, creationflags=NO_WINDOW).stdout.strip()


def check_laptop():
    if os.name != "nt":
        report(INFO, "this laptop", "sound, power and screen checks are Windows only")
        return
    try:
        name, vol, mute = ps(AUDIO_PS).splitlines()[-1].split("|")
        vol = int(vol)
        hdmi = any(w in name.lower() for w in ["hdmi", "displayport", "nvidia high definition", "intel(r) display", "projector"])
        if mute == "True" or vol == 0:
            report(RED, "sound", f"'{name}' is MUTED ({vol} %)", "unmute: click the speaker icon in the taskbar, volume 60-80 %")
        elif vol < 25:
            report(AMBER, "sound", f"'{name}' at {vol} %", "turn it up to 60-80 % (and check the game's M = SOUND ON)")
        else:
            report(GREEN, "sound", f"plays on '{name}' at {vol} %")
        if hdmi:
            report(INFO, "sound", "sound goes to the projector/HDMI: if the room has no speakers there, pick the "
                                  "laptop speakers in the taskbar sound menu")
    except Exception as e:  # noqa: BLE001
        report(INFO, "sound", f"could not read the sound device ({e.__class__.__name__}); check the taskbar speaker icon")
    try:
        lines = ps(LAPTOP_PS).splitlines()
        for line in lines:
            parts = line.split("|")
            if parts[0] == "BATTERY" and parts[1] != "none":
                on_ac = parts[1] == "2"
                report(GREEN if on_ac else AMBER, "power", ("charger plugged in" if on_ac else "on BATTERY") +
                       f", {parts[2]} %", "" if on_ac else "plug in the charger: on battery the GPU slows down")
        screens = [line.split("|") for line in lines if line.startswith("SCREEN|")]
        for _, primary, size in screens:
            w, h = (int(x) for x in size.split("x"))
            small = w < 1280 or h < 720
            report(AMBER if small else (GREEN if primary == "True" else INFO), "screen",
                   f"{'main' if primary == 'True' else 'second'} screen {size}" + (" (projector?)" if primary != "True" else ""),
                   "set the projector to Extend or a 16:9 mode of at least 1280x720 (Win+P, Display settings)" if small else "")
    except Exception as e:  # noqa: BLE001
        report(INFO, "this laptop", f"power/screen not readable ({e.__class__.__name__})")
    for c in [Path(os.environ.get("PROGRAMFILES", "")) / "Google/Chrome/Application",
              Path(os.environ.get("PROGRAMFILES(X86)", "")) / "Google/Chrome/Application",
              Path(os.environ.get("LOCALAPPDATA", "")) / "Google/Chrome/Application"]:
        versions = sorted([p.name for p in c.glob("*.*.*.*")], key=lambda v: [int(x) for x in v.split(".")]) if c.is_dir() else []
        if versions:
            pending = (c / "new_chrome.exe").is_file()  # Chrome stages an update here until the next restart
            report(AMBER if pending else INFO, "chrome", f"Google Chrome {versions[-1]}" +
                   (" (an update waits for a restart)" if pending else ""),
                   "restart Chrome now (the day before), never during the demo" if pending else "")
            break


# ---------------------------------------------------------------- main

def serve_copy(copy_dir):
    sys.path.insert(0, str(HERE))
    import presenter  # noqa: E402  (the presenter kit's own server)
    port = presenter.serve(copy_dir, background=True)
    return f"http://127.0.0.1:{port}/"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--no-join", action="store_true", help="do not join the online room")
    ap.add_argument("--copy", action="store_true", help="boot the laptop copy (plan B) instead of the live site")
    ap.add_argument("--dir", default=str(DEFAULT_COPY), help="the presenter kit folder (presenter.py install)")
    ap.add_argument("--url", default=LIVE, help="game address to boot (default the live site)")
    ap.add_argument("--timeout", type=int, default=240, help="seconds to wait for the start screen")
    ap.add_argument("--pause", action="store_true", help="wait for Enter at the end (desktop shortcut)")
    ap.add_argument("--simulate-no-internet", action="store_true", help=argparse.SUPPRESS)
    a = ap.parse_args()
    global NO_INTERNET
    NO_INTERNET = a.simulate_no_internet
    if os.name == "nt":
        os.system("")  # turn on colours in the Windows console
    t0 = time.time()
    print(f"Carcavelos Circuit preflight  {time.strftime('%Y-%m-%d %H:%M')}\n", flush=True)
    laptop = threading.Thread(target=check_laptop)
    laptop.start()
    live_build, _ = check_live()
    copy_build = check_copy(a.dir, live_build)
    global OFFLINE_HERE
    OFFLINE_HERE = live_build is None
    use_copy = a.copy or (live_build is None and copy_build is not None and a.url == LIVE)
    url = serve_copy(a.dir) if use_copy else (a.url if a.url.endswith("/") else a.url + "/")
    if use_copy and not a.copy:
        report(INFO, "target", "the live site is not reachable, so this run boots plan B (the laptop copy) instead")
    if use_copy:
        report(INFO, "target", f"booting the laptop copy at {url}")
    check_game(url, join=not a.no_join, timeout_s=a.timeout)
    laptop.join()
    reds = [r for r in results if r[0] == RED]
    ambers = [r for r in results if r[0] == AMBER]
    verdict = RED if reds else (AMBER if ambers else GREEN)
    color = COLORS[verdict] if sys.stdout.isatty() or os.environ.get("WT_SESSION") else ""
    print(f"\n{color}=== {verdict} ===\033[0m" if color else f"\n=== {verdict} ===", end="")
    print(f"  {len(reds)} red, {len(ambers)} amber, {time.time() - t0:.0f} s")
    if verdict == GREEN:
        print("Everything the demo needs works. Open 'Carcavelos LIVE', click to play, leave it on the start screen.")
    elif verdict == RED:
        print("Fix the red lines (the -> line under each). If the live site or the network is red: plan B = "
              "'Carcavelos OFFLINE' (docs/demo-plan.md, section 4).")
    if a.pause:
        input("\nPress Enter to close.")
    return 1 if reds else 0


if __name__ == "__main__":
    sys.exit(main())
