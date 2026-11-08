"""Serve the Unity Web build in Build-Web/ on http://localhost:8000 for a quick local test.

Our build uses gzip compression without "decompression fallback" (docs/decisions.md), so the browser
must receive the .gz files with the header "Content-Encoding: gzip", like itch.io does. Python's normal
`python -m http.server` does not send it and the game would not start; this small server does.

    python tools/serve_web_build.py            (folder Build-Web, port 8000)
    python tools/serve_web_build.py Build-Web 8080
"""

import http.server
import sys
from functools import partial

TYPES = {".js": "application/javascript", ".wasm": "application/wasm", ".data": "application/octet-stream"}


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


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else "Build-Web"
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 8000
    handler = partial(UnityWebHandler, directory=folder)
    print(f"Serving {folder} on http://localhost:{port} (Ctrl+C to stop)")
    http.server.ThreadingHTTPServer(("127.0.0.1", port), handler).serve_forever()


if __name__ == "__main__":
    main()
