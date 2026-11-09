"""Serve the Unity Web build in Build-Web/ on http://localhost:8000 for a quick local test.

Works for both kinds of build that Assets/_Project/RaceFlow/Editor/Role4WebBuild.cs makes (docs/decisions.md):
- Default build (compression Disabled, for GitHub Pages): plain .wasm/.data/.js files. Like GitHub Pages,
  this server gzips them on the fly when the browser accepts gzip, so the bytes over the wire match the real host.
- itch.io build (-webHost=itch, gzip, no decompression fallback): pre-compressed .gz files. The browser must get
  them with the header "Content-Encoding: gzip", like itch.io sends. Plain `python -m http.server` does not, and
  the game would not start; this small server does.

    python tools/serve_web_build.py            (folder Build-Web, port 8000)
    python tools/serve_web_build.py Build-Web 8080
"""

import gzip
import http.server
import os
import sys
from functools import partial

TYPES = {".js": "application/javascript", ".wasm": "application/wasm", ".data": "application/octet-stream"}
GZIP_ON_THE_FLY = (".wasm", ".data", ".js", ".html", ".css")
gzip_cache = {}  # (file path, modified time) -> gzipped bytes, so each file is compressed only once


class UnityWebHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        path = self.path.split("?")[0]
        if path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-store")  # always load the newest build
        super().end_headers()

    def guess_type(self, path):
        # foo.wasm.gz must be sent as application/wasm, not as a gzip download.
        name = str(path)
        if name.endswith(".gz"):
            name = name[:-3]
        for ending, mime in TYPES.items():
            if name.endswith(ending):
                return mime
        return super().guess_type(name)

    def do_GET(self):
        # Uncompressed build files: send them gzipped, as GitHub Pages does. Everything else: the normal way.
        file_path = self.translate_path(self.path)
        if os.path.isdir(file_path):
            file_path = os.path.join(file_path, "index.html")
        wants_gzip = "gzip" in self.headers.get("Accept-Encoding", "")
        if not wants_gzip or not file_path.endswith(GZIP_ON_THE_FLY) or not os.path.isfile(file_path):
            super().do_GET()
            return

        key = (file_path, os.path.getmtime(file_path))
        if key not in gzip_cache:
            with open(file_path, "rb") as f:
                gzip_cache[key] = gzip.compress(f.read(), compresslevel=6)
        body = gzip_cache[key]
        self.send_response(200)
        self.send_header("Content-Type", self.guess_type(file_path))
        self.send_header("Content-Encoding", "gzip")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else "Build-Web"
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 8000
    handler = partial(UnityWebHandler, directory=folder)
    print(f"Serving {folder} on http://localhost:{port} (Ctrl+C to stop)")
    http.server.ThreadingHTTPServer(("127.0.0.1", port), handler).serve_forever()


if __name__ == "__main__":
    main()
