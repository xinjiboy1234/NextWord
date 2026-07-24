# -*- coding: utf-8 -*-
"""T-005 真实 LLM 链路实测（DashScope qwen-plus + 独立验证库 nextword_verify_t005）。
流程：注册用户 → 完成自适应测评（medium 答案）→ 等评估报告后台任务
     → 断言报告 schemaVersion=2 且 findings 为已验证条目
     → GET /api/profile/weakness 核对 Finding 核查状态
     → psql 核对 WeaknessProfiles/ProfileFindings 落库。
验完删库（见 development-log T-005 记录），不动 dev 库 nextword。"""
import io
import json
import subprocess
import sys
import time
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"
DB = "nextword_verify_t005"


def call(method, path, token=None, body=None):
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    data = json.dumps(body).encode() if body is not None else None
    with urllib.request.urlopen(req, data) as resp:
        return json.loads(resp.read().decode())


def medium_answer(prompt):
    word = prompt.get("targetWord")
    if word:
        return f"I want to {word} because it is very important for me and my family."
    return ("Excuse me, I think my order is wrong. I want chicken, not this one. "
            "Can you change it for me? Thank you.")


def run_assessment():
    token = call("POST", "/api/auth/register", body={
        "email": f"verify-t005-{int(time.time())}@example.com", "password": "Passw0rd!234", "displayName": "t005"})["token"]
    aid = call("POST", "/api/assessment/initial/start", token, {})["assessmentId"]
    print(f"assessment: {aid[:8]}...")
    while True:
        resp = call("GET", f"/api/assessment/{aid}/next-block", token)
        if resp.get("converged"):
            final = resp["final"]
            break
        block = resp["block"]
        answers = [{"id": p["id"], "text": medium_answer(p)} for p in block["production"]]
        answers.append({"id": block["vocabulary"][0]["id"], "selectedIndex": 0})
        if block.get("reading"):
            answers.append({"id": block["reading"]["id"], "selectedIndex": 0, "lookupCount": 0})
        result = call("POST", f"/api/assessment/{aid}/blocks/{block['blockIndex']}/submit", token, {"answers": answers})
        print(f"block {block['blockIndex']} band={block['band']} expr={result['blockExpressionScore']:.1f} converged={result['converged']}")
        if result["converged"]:
            final = result["final"]
            break
    print(f"FINAL: level={final['overallLevel']} expr={final['expressionScore']}")
    return token, aid


def wait_report(token, timeout=120):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            report = call("GET", "/api/evaluation/latest", token)
            if report.get("status") == "Ready":
                return report
        except urllib.error.HTTPError:
            pass
        time.sleep(3)
    raise SystemExit("评估报告超时未就绪")


def psql(sql):
    out = subprocess.run(
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d", DB, "-t", "-c", sql],
        capture_output=True, text=True)
    return out.stdout.strip()


token, aid = run_assessment()
report = wait_report(token)
content = json.loads(report["contentJson"])
print(f"\nreport schemaVersion={content.get('schemaVersion')}")
assert content.get("schemaVersion") == 2, "报告未切换为画像内容（全部存疑或画像失败）"
findings = content.get("findings", [])
print(f"verified findings in report: {len(findings)}")
for f in findings:
    print(f"  [{f['dimension']}/{f['dimensionKey']}] {f['polarity']} conf={f['confidence']} evidence={len(f['evidence'])} :: {f['statement']}")
assert len(findings) >= 1
# 报告只呈现已验证条目，且每条都带证据引用
for f in findings:
    assert len(f["evidence"]) >= 1, "报告内 Finding 缺证据引用"

profile = call("GET", "/api/profile/weakness", token)
print(f"\nprofile id={profile['id']} findings={len(profile['findings'])}")
verified = [f for f in profile["findings"] if f["verification"] == "verified"]
questioned = [f for f in profile["findings"] if f["verification"] == "questioned"]
print(f"verified={len(verified)} questioned={len(questioned)}")
for f in questioned:
    print(f"  [存疑] {f['statement'][:40]} :: {f['verificationNote']}")
assert len(verified) == len(findings), "报告条目与画像已验证条目不吻合"

rows = psql('SELECT "Verification", count(*) FROM "ProfileFindings" GROUP BY 1')
print(f"\nDB ProfileFindings: {rows}")
profiles = psql('SELECT count(*) FROM "WeaknessProfiles"')
print(f"DB WeaknessProfiles: {profiles}")
assert profiles == "1"
print("\n真实 LLM 链路实测通过：测评→Profiler→Verifier→报告画像展示全链路走通。")
