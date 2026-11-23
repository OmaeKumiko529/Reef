"""Minimal MCP-over-SSE client for the Affinity side-car (http://localhost:6767/sse).

Why this exists: the `affinity` MCP server is registered in opencode's config but NOT in this
DSH session's MCP registry, so there is no mcp__affinity__* tool namespace. The server itself is
plain MCP over HTTP+SSE, so this script speaks the protocol directly.

Usage:
    python Tools/.uiart/affinity-mcp.py tools/list
    python Tools/.uiart/affinity-mcp.py tools/call @params.json [--text] [--out file]
    python Tools/.uiart/affinity-mcp.py run Tools/.uiart/some.js [--text]
    python Tools/.uiart/affinity-mcp.py topic application.js [--text]
    python Tools/.uiart/affinity-mcp.py hints "draw a star shape" [--text]
    python Tools/.uiart/affinity-mcp.py image render_spread '{"document_session_uuid":"..."}' --png out.png

Every connection is a fresh MCP session, and the server refuses execute_script until the
'preamble' topic has been read in that same session, so every call is preceded by that read.
"""
import base64
import json
import queue
import sys
import threading
import urllib.request

BASE = "http://localhost:6767"
SSE = BASE + "/sse"
PROTOCOL = "2025-11-25"  # the server rejects anything else ("Unsupported protocol version")


def load_json(path):
    if path.lstrip()[:1] in ("{", "["):
        return json.loads(path)
    with open(path, "rb") as fh:
        raw = fh.read()
    for enc in ("utf-8-sig", "utf-16", "utf-8"):
        try:
            return json.loads(raw.decode(enc))
        except Exception:
            continue
    raise RuntimeError("cannot decode JSON from " + path)


class Client:
    def __init__(self, timeout=60.0):
        self.timeout = timeout
        self.q = queue.Queue()
        self.endpoint = None
        self.ready = threading.Event()
        self.resp = urllib.request.urlopen(SSE, timeout=timeout)
        self.t = threading.Thread(target=self._pump, daemon=True)
        self.t.start()
        if not self.ready.wait(timeout):
            raise RuntimeError("no endpoint event within %.0fs" % timeout)

    def _pump(self):
        event = None
        while True:
            raw = self.resp.readline()
            if not raw:
                return
            line = raw.decode("utf-8", "replace").rstrip("\r\n")
            if line.startswith("event:"):
                event = line[6:].strip()
            elif line.startswith("data:"):
                data = line[5:].strip()
                if event == "endpoint":
                    self.endpoint = BASE + data if data.startswith("/") else data
                    self.ready.set()
                elif event == "message":
                    try:
                        self.q.put(json.loads(data))
                    except ValueError:
                        pass

    def post(self, payload):
        body = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            self.endpoint, data=body, headers={"Content-Type": "application/json"}
        )
        urllib.request.urlopen(req, timeout=self.timeout).read()

    def call(self, msg_id, method, params=None):
        m = {"jsonrpc": "2.0", "id": msg_id, "method": method}
        if params is not None:
            m["params"] = params
        self.post(m)
        while True:
            got = self.q.get(timeout=self.timeout)
            if got.get("id") == msg_id:
                return got

    def notify(self, method, params=None):
        m = {"jsonrpc": "2.0", "method": method}
        if params is not None:
            m["params"] = params
        self.post(m)


def tool_call(c, msg_id, name, arguments):
    return c.call(msg_id, "tools/call", {"name": name, "arguments": arguments})


def content_of(node):
    res = node.get("result", node)
    if isinstance(res, dict) and res.get("content"):
        return res["content"]
    if isinstance(res, dict) and res.get("error"):
        return [{"type": "text", "text": "ERROR: " + json.dumps(res["error"], ensure_ascii=False)}]
    return [{"type": "text", "text": json.dumps(res, ensure_ascii=False)}]


def main():
    args = sys.argv[1:]
    want_text = "--text" in args
    if want_text:
        args.remove("--text")
    out_path = png_path = None
    for flag in ("--out", "--png"):
        if flag in args:
            i = args.index(flag)
            value = args[i + 1]
            del args[i:i + 2]
            if flag == "--out":
                out_path = value
            else:
                png_path = value

    method, params = None, None
    mode = args[0] if args else "tools/list"

    if mode == "run":
        js = open(args[1], encoding="utf-8-sig").read()
        method, params = "tools/call", {"name": "execute_script", "arguments": {"script": js}}
    elif mode == "topic":
        method, params = "tools/call", {"name": "read_sdk_documentation_topic",
                                        "arguments": {"filename": args[1]}}
    elif mode == "hints":
        method, params = "tools/call", {"name": "search_sdk_hints",
                                        "arguments": {"prompt": args[1]}}
    elif mode == "image":
        method, params = "tools/call", {"name": args[1], "arguments": load_json(args[2])}
    elif mode == "call":
        # 通用工具调用：call <toolName> '{...json args...}'
        method, params = "tools/call", {"name": args[1],
                                        "arguments": load_json(args[2]) if len(args) > 2 else {}}
    elif mode == "tools/list":
        method = "tools/list"
    else:
        method = mode
        if len(args) > 1 and args[1].startswith("@"):
            params = load_json(args[1][1:])
        elif len(args) > 1:
            params = json.loads(args[1])

    c = Client()
    init = c.call(1, "initialize", {
        "protocolVersion": PROTOCOL,
        "capabilities": {},
        "clientInfo": {"name": "dsh-lead", "version": "0.1"},
    })["result"]
    c.notify("notifications/initialized")
    pre = tool_call(c, 2, "read_sdk_documentation_topic", {"filename": "preamble"})
    result = c.call(3, method, params) if method != "initialize" else pre

    out = {
        "server": init.get("serverInfo"),
        "preambleRead": not content_of(pre)[0].get("text", "").startswith(("File not found",
                                                                            "The preamble")),
    }
    if method != "initialize":
        out[method] = result

    if png_path:
        for part in content_of(result):
            if part.get("type") == "image" and part.get("data"):
                raw = base64.b64decode(part["data"])
                # the server sends JPEG; keep the bytes, name it .jpg next to the requested path
                jpg = png_path[:-4] + ".jpg" if png_path.lower().endswith(".png") else png_path
                with open(jpg, "wb") as fh:
                    fh.write(raw)
                out["savedImage"] = {"path": jpg, "bytes": len(raw),
                                     "mime": part.get("mimeType", "?")}
                if not want_text:
                    break

    text = json.dumps(out, ensure_ascii=False, indent=2)
    if out_path:
        with open(out_path, "w", encoding="utf-8") as fh:
            fh.write(text)
    if want_text and method != "initialize":
        text = "\n".join(p.get("text", "") for p in content_of(result))
    print(text)


if __name__ == "__main__":
    main()
