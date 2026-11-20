"""Presenter kit for the live demo (docs/demo-plan.md): a copy of the deployed game on the demo laptop that runs with no
internet, a dedicated Chrome window for the demo, and desktop shortcuts, so a dead network or a GitHub Pages outage never
ends the demo.

    python tools/preflight/presenter.py install   [--dir D]   first time: copy of the live build + shortcuts (needs internet)
    python tools/preflight/presenter.py update    [--dir D]   newest live build into the copy (run after the last merge)
    python tools/preflight/presenter.py live      [--dir D]   demo Chrome on https://nova-kart.github.io/ (plan A)
    python tools/preflight/presenter.py offline   [--dir D]   local server + demo Chrome on the laptop copy (plan B)
    python tools/preflight/presenter.py serve     [--dir D]   only the local server (foreground, Ctrl+C stops it)
    python tools/preflight/presenter.py warm      [--dir D]   load the live site once in the demo Chrome profile (cache)
    python tools/preflight/presenter.py status    [--dir D]   what the copy holds, compared with the live site

The copy is the GitHub Pages mirror nova-kart/nova-kart.github.io (every merge to main pushes the exact deployed files
there, commit message "Web build of belschak/carcavelos-circuit@<sha>"), cloned with depth 1: about 140 MB on disk.
The demo Chrome has its own profile in <dir>/chrome-profile: no extensions, no bookmarks bar, no notifications, sound may
start before the first click, and its cache keeps the game, so a reload in the room comes from the disk.
Default <dir>: %USERPROFILE%/Documents/nova/carcavelos-presenter (outside OneDrive on purpose: OneDrive must not turn the
build files into cloud placeholders). Standard library only (plus Playwright for "warm").
"""

import argparse
import json
import os
import shutil
import socket
import subprocess
import sys
import threading
import time
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
LIVE_URL = "https://nova-kart.github.io/"
MIRROR_GIT = "https://github.com/nova-kart/nova-kart.github.io"
PORT = 8642
DEFAULT_DIR = Path(os.environ.get("USERPROFILE", str(Path.home()))) / "Documents" / "nova" / "carcavelos-presenter"
CHROME_CANDIDATES = [
    Path(os.environ.get("PROGRAMFILES", r"C:\Program Files")) / "Google/Chrome/Application/chrome.exe",
    Path(os.environ.get("PROGRAMFILES(X86)", r"C:\Program Files (x86)")) / "Google/Chrome/Application/chrome.exe",
    Path(os.environ.get("LOCALAPPDATA", "")) / "Google/Chrome/Application/chrome.exe",
    Path("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"),
]
# The demo window: its own profile, maximized (the address bar shows the deployed URL; F11 or the game's full screen
# button hides it), sound allowed before the first click, no first-run or restore bubbles over the game.
CHROME_FLAGS = ["--no-first-run", "--no-default-browser-check", "--start-maximized", "--disable-notifications",
                "--autoplay-policy=no-user-gesture-required", "--hide-crash-restore-bubble",
                "--disable-session-crashed-bubble", "--disable-features=Translate,MediaRouter"]


def say(text):
    print(text, flush=True)


def build_dir(root):
    return Path(root) / "build"


def read_json(path):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def fetch_json(url, timeout=8):
    req = urllib.request.Request(url, headers={"User-Agent": "carcavelos-presenter", "Cache-Control": "no-cache"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def copy_info(root):
    """(build id, source sha, mirror commit date) of the laptop copy, or Nones."""
    b = build_dir(root)
    version = read_json(b / "version.json") or {}
    sha, date = None, None
    try:
        out = subprocess.run(["git", "-C", str(b), "log", "-1", "--format=%s|%ci"], capture_output=True, text=True,
                             timeout=10).stdout.strip()
        subject, date = out.split("|", 1)
        sha = subject.rsplit("@", 1)[-1] if "@" in subject else None
    except (OSError, ValueError, subprocess.SubprocessError):
        pass
    return version.get("build"), sha, date


def complete(root):
    """True when the copy has index.html, version.json and every Build file the page names."""
    b = build_dir(root)
    index = b / "index.html"
    if not index.is_file() or not (b / "version.json").is_file():
        return False
    files = list((b / "Build").glob("*"))
    kinds = [".wasm", ".framework.js", ".loader.js", ".data", ".bundle"]
    return all(any(f.name.endswith(k) and f.stat().st_size > 0 for f in files) for k in kinds)


# ---------------------------------------------------------------- update / install

def update(root):
    root = Path(root)
    root.mkdir(parents=True, exist_ok=True)
    b = build_dir(root)
    if (b / ".git").is_dir():
        say("Updating the laptop copy from " + MIRROR_GIT + " ...")
        ok = subprocess.run(["git", "-C", str(b), "fetch", "--depth", "1", "origin", "main"]).returncode == 0
        ok = ok and subprocess.run(["git", "-C", str(b), "reset", "--hard", "FETCH_HEAD"]).returncode == 0
        subprocess.run(["git", "-C", str(b), "clean", "-fdq"])
    else:
        say("Downloading the deployed build (about 140 MB) from " + MIRROR_GIT + " ...")
        tmp = root / "build-new"
        if tmp.exists():
            shutil.rmtree(tmp)
        ok = subprocess.run(["git", "clone", "--depth", "1", MIRROR_GIT, str(tmp)]).returncode == 0
        if ok:
            if b.exists():
                shutil.rmtree(b)
            tmp.rename(b)
    if not ok or not complete(root):
        say("UPDATE FAILED: the copy is missing files. Check the internet and run update again. The old copy, if any, "
            "was kept only if git could not start.")
        return 1
    build, sha, date = copy_info(root)
    say(f"Laptop copy ready: build {build}, main commit {sha}, deployed {date}.")
    return 0


def shortcut(lnk, target, args, workdir, description, icon=None):
    """A Windows .lnk via WScript.Shell (PowerShell, no window)."""
    ps = ("$s=(New-Object -ComObject WScript.Shell).CreateShortcut('{lnk}');$s.TargetPath='{t}';$s.Arguments='{a}';"
          "$s.WorkingDirectory='{w}';$s.Description='{d}';{i}$s.Save()").format(
        lnk=str(lnk).replace("'", "''"), t=str(target).replace("'", "''"), a=args.replace("'", "''"),
        w=str(workdir).replace("'", "''"), d=description.replace("'", "''"),
        i=("$s.IconLocation='" + str(icon).replace("'", "''") + "';") if icon else "")
    subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", ps], check=True,
                   creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))


def install(root):
    root = Path(root)
    root.mkdir(parents=True, exist_ok=True)
    tools = root / "tools"
    tools.mkdir(exist_ok=True)
    # The kit runs from its own copy of the scripts, so it never depends on what branch the repo clone is on.
    for name in ["presenter.py", "preflight.py"]:
        shutil.copy2(HERE / name, tools / name)
    shutil.copy2(HERE.parent / "serve_web_build.py", tools / "serve_web_build.py")
    runbook = HERE.parent.parent / "docs" / "demo-plan.md"
    if runbook.is_file():
        shutil.copy2(runbook, root / "demo-plan.md")  # the runbook, readable without internet
    (root / "plan-b-video").mkdir(exist_ok=True)  # plan D: put the recorded online race here
    code = update(root)
    if code != 0:
        return code
    if os.name != "nt":
        say("Shortcuts are Windows only. Start with: python " + str(tools / "presenter.py") + " offline")
        return 0
    py = Path(sys.executable)
    pyw = py.with_name("pythonw.exe") if py.with_name("pythonw.exe").is_file() else py
    chrome = find_chrome()
    # The real desktop folder (OneDrive backup can move it to OneDrive\Desktop)
    desktop = Path(subprocess.run(["powershell", "-NoProfile", "-Command", "[Environment]::GetFolderPath('Desktop')"],
                                  capture_output=True, text=True, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
                   .stdout.strip() or (Path.home() / "Desktop"))
    made = []
    for name, cmd, exe, desc in [
        ("Carcavelos LIVE", "live", pyw, "Demo Chrome on https://nova-kart.github.io/ (plan A)"),
        ("Carcavelos OFFLINE", "offline", pyw, "Demo Chrome on the laptop copy, no internet needed (plan B)"),
        ("Carcavelos PREFLIGHT", "", py, "One-minute GREEN/RED check before the demo"),
    ]:
        script = tools / ("preflight.py" if cmd == "" else "presenter.py")
        args = f'"{script}"' + (f' {cmd} --dir "{root}"' if cmd else f' --dir "{root}" --pause')
        lnk = desktop / (name + ".lnk")
        shortcut(lnk, exe, args, root, desc, icon=chrome)
        shutil.copy2(lnk, root / lnk.name)
        made.append(str(lnk))
    say("Shortcuts:\n  " + "\n  ".join(made))
    return 0


# ---------------------------------------------------------------- server

def port_serves_copy(port, build):
    try:
        v = fetch_json(f"http://127.0.0.1:{port}/version.json", timeout=2)
        return v.get("build") == build
    except Exception:
        return False


def port_free(port):
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        return s.connect_ex(("127.0.0.1", port)) != 0


def load_server_module():
    """tools/serve_web_build.py: from the kit's own tools folder, or the repo's tools folder."""
    for folder in [HERE, HERE.parent]:
        if (folder / "serve_web_build.py").is_file():
            sys.path.insert(0, str(folder))
            import serve_web_build  # noqa: E402
            return serve_web_build
    raise SystemExit("serve_web_build.py not found next to presenter.py or in tools/")


def prewarm(mod, folder):
    """Gzip the big files once now (as the server would on the first request), so the first load is not slower."""
    import gzip
    for f in Path(folder).rglob("*"):
        if f.is_file() and f.name.endswith(mod.GZIP_ON_THE_FLY) and ".git" not in f.parts:
            key = (str(f), os.path.getmtime(f))
            if key not in mod.gzip_cache:
                mod.gzip_cache[key] = gzip.compress(f.read_bytes(), compresslevel=6)


def serve(root, port=PORT, background=False):
    """Serve the copy on http://127.0.0.1:<port>/. Returns the port that serves it."""
    b = build_dir(root)
    if not complete(root):
        raise SystemExit("No complete laptop copy in " + str(b) + ". Run: presenter.py update (needs internet once).")
    build = copy_info(root)[0]
    for p in range(port, port + 10):
        if port_serves_copy(p, build):
            return p  # already running (a second click on the shortcut)
        if port_free(p):
            port = p
            break
    else:
        raise SystemExit("Ports %d-%d are all taken." % (port, port + 9))
    mod = load_server_module()
    from functools import partial
    import http.server
    # translate_path() maps the request to the folder; the module's handler keys its cache on that same path string
    handler = partial(mod.UnityWebHandler, directory=str(b))
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", port), handler)
    if background:
        threading.Thread(target=httpd.serve_forever, daemon=True).start()
        threading.Thread(target=prewarm, args=(mod, b), daemon=True).start()
    else:
        say(f"Serving the laptop copy (build {build}) on http://127.0.0.1:{port}/  (Ctrl+C to stop)")
        threading.Thread(target=prewarm, args=(mod, b), daemon=True).start()
        httpd.serve_forever()
    return port


# ---------------------------------------------------------------- Chrome

def find_chrome():
    for c in CHROME_CANDIDATES:
        if c.is_file():
            return c
    found = shutil.which("chrome") or shutil.which("google-chrome")
    return Path(found) if found else None


def open_chrome(root, url):
    chrome = find_chrome()
    if chrome is None:
        raise SystemExit("Google Chrome not found. Open " + url + " in any browser.")
    profile = Path(root) / "chrome-profile"
    profile.mkdir(parents=True, exist_ok=True)
    return subprocess.Popen([str(chrome), "--user-data-dir=" + str(profile), *CHROME_FLAGS, url])


def run_offline(root):
    port = serve(root, background=True)
    url = f"http://127.0.0.1:{port}/"
    proc = open_chrome(root, url)
    # Keep the server alive while the demo Chrome runs. Chrome may hand the window to an already running demo
    # Chrome and exit at once: then keep serving until no demo Chrome is left (checked every 5 s).
    proc.wait()
    while demo_chrome_running(root):
        time.sleep(5)
    return 0


def demo_chrome_running(root):
    if os.name != "nt":
        return False
    profile = str(Path(root) / "chrome-profile").lower()
    ps = ("Get-CimInstance Win32_Process -Filter \"name='chrome.exe'\" | Where-Object { $_.CommandLine -and "
          "$_.CommandLine.ToLower().Contains('" + profile.replace("'", "''") + "') } | Measure-Object | "
          "Select-Object -ExpandProperty Count")
    try:
        out = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", ps], capture_output=True,
                             text=True, timeout=20, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0)).stdout
        return int(out.strip() or "0") > 0
    except (OSError, ValueError, subprocess.SubprocessError):
        return True  # unsure: keep serving


def warm(root, url=LIVE_URL):
    """Load the game once in the demo profile (headless, no window), so its cache holds the build."""
    if demo_chrome_running(root):
        say("The demo Chrome is open: close it first (its profile is in use).")
        return 1
    from playwright.sync_api import sync_playwright
    profile = Path(root) / "chrome-profile"
    t0 = time.time()
    with sync_playwright() as pw:
        ctx = pw.chromium.launch_persistent_context(str(profile), channel="chrome", headless=True,
                                                    args=["--use-angle=d3d11", "--mute-audio"],
                                                    viewport={"width": 1280, "height": 720})
        page = ctx.pages[0] if ctx.pages else ctx.new_page()
        page.goto(url, wait_until="domcontentloaded", timeout=120000)
        page.wait_for_function("() => { const l = document.getElementById('cc-loader'); const p = document.getElementById('cc-panel-play');"
                               " return (!!p && !p.hidden) || (!!l && (l.hidden || l.classList.contains('cc-done'))); }",
                               timeout=900000, polling=1000)
        ctx.close()
    say(f"Demo profile warmed with {url} in {time.time() - t0:.0f} s.")
    return 0


def status(root):
    build, sha, date = copy_info(root)
    say(f"Laptop copy: {'complete' if complete(root) else 'MISSING OR INCOMPLETE'}, build {build}, main {sha}, deployed {date}")
    try:
        live = fetch_json(LIVE_URL + "version.json").get("build")
        say(f"Live site:   build {live}  -> " + ("same build" if live == build else "DIFFERENT: run presenter.py update"))
    except Exception as e:  # noqa: BLE001
        say(f"Live site:   not reachable ({e.__class__.__name__}). The copy still works offline.")
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", choices=["install", "update", "live", "offline", "serve", "warm", "status"])
    ap.add_argument("--dir", default=str(DEFAULT_DIR))
    ap.add_argument("--port", type=int, default=PORT)
    a = ap.parse_args(argv)
    root = Path(a.dir)
    if sys.stdout is None or sys.stderr is None:
        # Started by the desktop shortcut with pythonw (no console): the server logs every request to stderr, and
        # with no stderr each request failed with an empty reply (found by the real shortcut test, 20 Nov 2026).
        root.mkdir(parents=True, exist_ok=True)
        log = open(root / "presenter.log", "a", encoding="utf-8", buffering=1)
        sys.stdout = sys.stderr = log
        say(time.strftime("\n%Y-%m-%d %H:%M:%S ") + " ".join(sys.argv[1:]))
    if a.command == "install":
        return install(root)
    if a.command == "update":
        return update(root)
    if a.command == "live":
        open_chrome(root, LIVE_URL)
        return 0
    if a.command == "offline":
        return run_offline(root)
    if a.command == "serve":
        serve(root, a.port)
        return 0
    if a.command == "warm":
        return warm(root)
    return status(root)


if __name__ == "__main__":
    sys.exit(main())
