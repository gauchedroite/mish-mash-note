import json, os, sys, urllib.parse
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler

ROOT = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(ROOT, "data.json")
ALLOW_ROOTS = []  # filled from data.json group dirs on startup


def safe_path(p):
    """Reject anything that escapes its allowed root. Single-user localhost;
    this is just to stop accidents, not a real trust boundary."""
    p = os.path.normpath(os.path.abspath(p))
    for root in ALLOW_ROOTS + [ROOT]:
        if p == root or p.startswith(root + os.sep):
            return p
    return None


def load_data():
    with open(DATA, "r", encoding="utf-8") as f:
        return json.load(f)


def save_data(d):
    with open(DATA, "w", encoding="utf-8") as f:
        json.dump(d, f, indent=2, ensure_ascii=False)


def refresh_roots():
    global ALLOW_ROOTS
    ALLOW_ROOTS = []
    try:
        for g in load_data().get("groups", {}).values():
            d = g.get("dir")
            if d:
                ALLOW_ROOTS.append(os.path.normpath(os.path.abspath(d)))
    except Exception:
        pass


class H(BaseHTTPRequestHandler):
    def log_message(self, *a): pass

    def _send(self, code, body=b"", ctype="application/json"):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        if body:
            self.wfile.write(body)

    def _body(self):
        n = int(self.headers.get("Content-Length", 0))
        return self.rfile.read(n) if n else b""

    def do_OPTIONS(self):
        self._send(200)

    def do_GET(self):
        u = urllib.parse.urlparse(self.path)
        q = urllib.parse.parse_qs(u.query)
        if u.path == "/" or u.path == "/index.html":
            with open(os.path.join(ROOT, "index.html"), "rb") as f:
                self._send(200, f.read(), "text/html; charset=utf-8")
            return
        if u.path == "/api/data":
            self._send(200, json.dumps(load_data()).encode())
            return
        if u.path == "/api/root":
            self._send(200, json.dumps(ROOT).encode()); return
        if u.path == "/api/datafile":
            with open(DATA, "r", encoding="utf-8") as f:
                self._send(200, f.read().encode(), "text/plain; charset=utf-8")
            return
        if u.path == "/api/list":
            p = safe_path(q.get("path", [""])[0])
            if not p or not os.path.isdir(p):
                self._send(200, b"[]"); return
            names = sorted((f for f in os.listdir(p) if os.path.isfile(os.path.join(p, f))), key=str.lower)
            self._send(200, json.dumps(names).encode())
            return
        if u.path == "/api/file":
            p = safe_path(q.get("path", [""])[0])
            if not p or not os.path.isfile(p):
                self._send(404); return
            with open(p, "r", encoding="utf-8") as f:
                self._send(200, f.read().encode(), "text/plain; charset=utf-8")
            return
        if u.path == "/api/open":
            p = safe_path(q.get("path", [""])[0])
            if not p or not os.path.exists(p):
                self._send(404); return
            os.startfile(p)  # Windows; use "open" / "xdg-open" elsewhere
            self._send(200, b'{"ok":true}')
            return
        if u.path == "/api/delfile":
            p = safe_path(q.get("path", [""])[0])
            if not p or not os.path.isfile(p):
                self._send(404); return
            os.remove(p)
            self._send(200, b'{"ok":true}')
            return
        self._send(404)

    def do_POST(self):
        u = urllib.parse.urlparse(self.path)
        q = urllib.parse.parse_qs(u.query)
        if u.path == "/api/data":
            try:
                save_data(json.loads(self._body()))
                refresh_roots()
                self._send(200, b'{"ok":true}')
            except Exception as e:
                self._send(400, json.dumps({"error": str(e)}).encode())
            return
        if u.path == "/api/datafile":
            try:
                DATA_bytes = self._body()
                json.loads(DATA_bytes)  # validate before writing
                with open(DATA, "w", encoding="utf-8") as f:
                    f.write(DATA_bytes.decode("utf-8"))
                refresh_roots()
                self._send(200, b'{"ok":true}')
            except Exception as e:
                self._send(400, json.dumps({"error": str(e)}).encode())
            return
        if u.path == "/api/file":
            p = safe_path(q.get("path", [""])[0])
            if not p:
                self._send(404); return
            os.makedirs(os.path.dirname(p), exist_ok=True)
            with open(p, "w", encoding="utf-8") as f:
                f.write(self._body().decode("utf-8"))
            self._send(200, b'{"ok":true}')
            return
        if u.path == "/api/newfile":
            d = safe_path(q.get("dir", [""])[0])
            name = q.get("name", [""])[0].strip()
            if not d or not name:
                self._send(400, b'{"error":"missing dir/name"}'); return
            if not name.endswith((".md", ".txt")):
                name += ".md"
            p = os.path.join(d, name)
            if os.path.exists(p):
                self._send(409, b'{"error":"exists"}'); return
            os.makedirs(d, exist_ok=True)
            open(p, "w", encoding="utf-8").close()
            self._send(200, json.dumps({"path": p}).encode())
            return
        self._send(404)


if __name__ == "__main__":
    refresh_roots()
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8000
    print(f"http://localhost:{port}")
    ThreadingHTTPServer(("127.0.0.1", port), H).serve_forever()
