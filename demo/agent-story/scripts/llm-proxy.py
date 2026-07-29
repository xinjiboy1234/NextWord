# -*- coding: utf-8 -*-
"""LLM 记录代理：NextWord 后端 → 本代理 → DashScope compatible-mode。

用途：在不改后端代码的前提下，把全部 Agent ↔ LLM 对话完整留痕。
后端配置 Llm__OpenAI__BaseUrl=http://localhost:5299/v1 即可。

每条调用追加写入 output/llm-conversations.jsonl：
  { seq, ts, latencyMs, caller, model, request, response, usage, error }

caller 归属规则（按 system/user prompt 特征串，来自 LlmChatClientProvider 的固定模板）：
  weakness-profile    → Profiler Agent
  bottleneck-insight  → Insight Agent
  learning assessment + Target Word: free expression → 自由表达评分（feedback-rich）
  learning assessment → 造句/测评评分（grading-stable）
  contextual word definitions → 阅读查词
  vocabulary extraction → 阅读词汇提取
  reading tutor → 批注回复
  scenario annotation → 场景标注
"""
import io
import json
import sys
import time
import urllib.request
import urllib.error
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

UPSTREAM = "https://dashscope.aliyuncs.com/compatible-mode/v1"
LISTEN_PORT = 5299
OUT_FILE = Path(__file__).resolve().parent.parent / "output" / "llm-conversations.jsonl"

_seq = 0


def attribute(messages):
    text = "\n".join(str(m.get("content", ""))[:4000] for m in messages if isinstance(m, dict))
    sys_text = "\n".join(str(m.get("content", "")) for m in messages
                         if isinstance(m, dict) and m.get("role") == "system")
    if "English learner weakness profile" in sys_text or "Profiler agent" in text:
        return "profiler-agent"
    if "English learner bottleneck insight" in sys_text or "Insight agent" in text:
        return "insight-agent"
    if "vocabulary scenario annotation" in sys_text:
        return "scenario-annotation"
    if "vocabulary extraction" in sys_text:
        return "vocab-extract"
    if "contextual word definitions" in sys_text:
        return "reading-lookup"
    if "reading tutor" in sys_text:
        return "comment-reply"
    if "English learning assessment" in sys_text:
        if "Target Word: free expression" in text:
            return "free-expression-rating"
        return "sentence-rating"
    return "unknown"


def record(entry):
    OUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    with OUT_FILE.open("a", encoding="utf-8") as f:
        f.write(json.dumps(entry, ensure_ascii=False) + "\n")


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *args):  # 静音默认访问日志
        pass

    def do_POST(self):
        global _seq
        started = time.time()
        length = int(self.headers.get("Content-Length") or 0)
        body = self.rfile.read(length) if length else b""
        upstream_url = UPSTREAM + self.path[len("/v1"):] if self.path.startswith("/v1") else UPSTREAM + self.path

        req = urllib.request.Request(upstream_url, data=body, method="POST")
        for h in ("Authorization", "Content-Type", "Accept"):
            if self.headers.get(h):
                req.add_header(h, self.headers.get(h))

        entry = {"ts": time.strftime("%Y-%m-%dT%H:%M:%S"), "path": self.path}
        status, resp_body = 502, b'{"error":"upstream failure"}'
        try:
            with urllib.request.urlopen(req, timeout=120) as resp:
                status, resp_body = resp.status, resp.read()
        except urllib.error.HTTPError as e:
            status, resp_body = e.code, e.read()
        except Exception as e:  # 网络异常等
            entry["error"] = repr(e)

        try:
            req_json = json.loads(body.decode("utf-8")) if body else {}
        except Exception:
            req_json = {"_raw": body[:500].decode("utf-8", "replace")}
        try:
            resp_json = json.loads(resp_body.decode("utf-8"))
        except Exception:
            resp_json = {"_raw": resp_body[:2000].decode("utf-8", "replace")}

        _seq += 1
        messages = req_json.get("messages") or []
        content = ""
        try:
            content = resp_json["choices"][0]["message"]["content"]
        except Exception:
            pass
        entry.update({
            "seq": _seq,
            "latencyMs": int((time.time() - started) * 1000),
            "httpStatus": status,
            "caller": attribute(messages),
            "model": req_json.get("model"),
            "messages": messages,
            "responseText": content,
            "usage": resp_json.get("usage"),
        })
        record(entry)
        print(f"[#{_seq}] {entry['caller']:<24} {entry['latencyMs']:>6}ms -> {status}  ({len(content)} chars)")

        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(resp_body)))
        self.end_headers()
        self.wfile.write(resp_body)

    def do_GET(self):  # 健康检查
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", "2")
        self.end_headers()
        self.wfile.write(b"{}")


if __name__ == "__main__":
    server = ThreadingHTTPServer(("127.0.0.1", LISTEN_PORT), Handler)
    print(f"LLM 记录代理 listening on :{LISTEN_PORT} -> {UPSTREAM}")
    print(f"对话记录写入 {OUT_FILE}")
    server.serve_forever()
