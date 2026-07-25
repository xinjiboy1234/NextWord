# -*- coding: utf-8 -*-
"""T-006 真实 LLM 链路实测（DashScope qwen-plus + 独立验证库 nextword_verify_t006）。
流程：注册用户 → 完成自适应测评（medium 答案）→ 等评估报告后台任务（自动入队 Planner）
     → 等当日 LearningPlan → 断言：
       ① Plan 主攻场景/生成依据只来自 Verified Finding（存疑不进规划）；
       ② 每日词队列执行 Plan（fromPlan）、接触词 ≤20% 且标记 isExposure；
       ③ 造句出题用 Plan 目标（fromPlan）；阅读推荐按主攻场景（fromPlan）；
       ④ 同日重复触发幂等（LearningPlans 仅 1 行）；
       ⑤ 无画像用户按场景词覆盖率兜底出 Plan；
       ⑥ T-010：画像无同维度重复 Finding、证据不被多条 Finding 复用。
验完删库（见 development-log T-006 记录），不动 dev 库 nextword。
前置：API 以 DashScope 真实 LLM + 连接串指向 nextword_verify_t006 运行在 localhost:5108。"""
import io
import json
import subprocess
import sys
import time
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"
DB = "nextword_verify_t006"


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


def register(name):
    return call("POST", "/api/auth/register", body={
        "email": f"verify-t006-{name}-{int(time.time())}@example.com",
        "password": "Passw0rd!234", "displayName": name})["token"]


def run_assessment(token):
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
    return aid


def wait_ready(path, token, predicate, timeout=180, label="resource"):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            data = call("GET", path, token)
            if predicate(data):
                return data
        except urllib.error.HTTPError:
            pass
        time.sleep(3)
    raise SystemExit(f"{label} 超时未就绪")


def psql(sql):
    out = subprocess.run(
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d", DB, "-t", "-c", sql],
        capture_output=True, text=True)
    return out.stdout.strip()


# ── 有画像用户：测评 → 报告 → Planner 自动触发 ─────────────────
token = register("assessed")
run_assessment(token)
report = wait_ready("/api/evaluation/latest", token, lambda r: r.get("status") == "Ready", label="评估报告")
content = json.loads(report["contentJson"])
print(f"\nreport schemaVersion={content.get('schemaVersion')}")

profile = call("GET", "/api/profile/weakness", token)
findings = profile["findings"]
verified = [f for f in findings if f["verification"] == "verified"]
questioned = [f for f in findings if f["verification"] == "questioned"]
verified_ids = {f["id"] for f in verified}
verified_scenario_weakness_keys = {
    f["dimensionKey"] for f in verified
    if f["dimension"] == "scenario" and f["polarity"] == "weakness"}
print(f"findings: verified={len(verified)} questioned={len(questioned)}")

# ⑥ T-010：无同维度重复、无证据复用
dim_keys = [(f["dimension"], f["dimensionKey"]) for f in findings]
assert len(dim_keys) == len(set(dim_keys)), "存在同维度重复 Finding（T-010 未生效）"
seen_evidence = set()
for f in findings:
    for e in f["evidence"]:
        key = (e["kind"], e["refId"], e.get("metric"))
        assert key not in seen_evidence, f"证据被多条 Finding 复用：{key}"
        seen_evidence.add(key)
print("T-010 去重断言通过：无同维度重复、无证据复用")

plan = wait_ready("/api/planner/current", token, lambda p: p.get("active"), label="LearningPlan")
print(f"\nplan: start={plan['startDate']} day={plan['dayIndex']} focus={plan['focusScenarios']} "
      f"sourceFindings={plan['sourceFindingIds']} words={plan['todayWordCount']}+{plan['todayExposureCount']} "
      f"targets={plan['todaySentenceTargets']}")
# ① 生成依据只含 Verified id；有 Verified 场景 weakness 时主攻场景必须来自其中
assert set(plan["sourceFindingIds"]) <= verified_ids, "生成依据混入存疑 Finding"
assert not (set(plan["sourceFindingIds"]) & {f["id"] for f in questioned}), "存疑 Finding 进入生成依据"
if verified_scenario_weakness_keys:
    assert set(plan["focusScenarios"]) <= verified_scenario_weakness_keys, "主攻场景未来自 Verified 场景 weakness"
else:
    assert plan["sourceFindingIds"] == [], "兜底路径不应有生成依据"
print("① 只消费 Verified Finding 断言通过")

# ② 每日词执行 Plan：fromPlan + 接触词 ≤20% 且标记
daily = call("GET", "/api/words/daily?count=10", token)
assert daily and all(item.get("fromPlan") for item in daily), "每日词未执行 Plan"
exposure = [item for item in daily if item.get("isExposure")]
assert len(exposure) <= 2, f"接触词占比超 20%：{len(exposure)}/10"
print(f"② 每日词执行 Plan 断言通过（{len(daily)} 词，接触词 {len(exposure)} 个）")

# ③ 造句出题用 Plan 目标
prompts = call("GET", "/api/sentences/prompts?count=3", token)
assert prompts and all(p.get("fromPlan") for p in prompts), "造句出题未用 Plan 目标"
targets = set(plan["todaySentenceTargets"])
assert all(p["targetWord"] in targets for p in prompts), "造句目标与 Plan 不符"
print(f"③ 造句出题 Plan 目标断言通过（{[p['targetWord'] for p in prompts]}）")

# 阅读推荐按主攻场景
recommended = call("GET", "/api/articles/recommended", token)
assert recommended.get("fromPlan"), "阅读推荐未来自 Plan"
print(f"阅读推荐 Plan 断言通过（{[a['title'] for a in recommended['articles']]}）")

# ④ 同日重复触发幂等
call("POST", "/api/planner/jobs", token, {})
call("POST", "/api/planner/jobs", token, {})
time.sleep(6)
count = psql('SELECT count(*) FROM "LearningPlans"')
assert count == "1", f"同日重复触发不幂等：LearningPlans={count}"
print(f"④ 同日幂等断言通过（LearningPlans={count}）")

# ── 无画像用户：覆盖率兜底 ─────────────────────────────
token2 = register("fresh")
call("POST", "/api/planner/jobs", token2, {})
plan2 = wait_ready("/api/planner/current", token2, lambda p: p.get("active"), timeout=60, label="兜底 LearningPlan")
assert plan2["sourceFindingIds"] == [], "无画像用户不应有生成依据"
assert 1 <= len(plan2["focusScenarios"]) <= 2, "兜底主攻场景数量异常"
daily2 = call("GET", "/api/words/daily?count=10", token2)
assert daily2 and all(item.get("fromPlan") for item in daily2), "兜底 Plan 未驱动每日词"
print(f"⑤ 覆盖率兜底断言通过（focus={plan2['focusScenarios']}）")

rows = psql('SELECT "StartDate", length("ContentJson") FROM "LearningPlans"')
print(f"\nDB LearningPlans: {rows}")
print("\n真实 LLM 链路实测通过：测评→画像→Planner→每日内容来源切换全链路走通。")
