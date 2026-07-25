# -*- coding: utf-8 -*-
"""T-012 复验（周密 QA）：安全词筛查误触发修复（窗口从 Plan.CreatedAt 起算 + 24h 宽限期）。
真实 DashScope qwen-plus 链路 + 独立库 nextword_verify_t012_qa：
  R 复现原误触发场景：3 篇自由产出（2h 前）→ 模拟每周兜底刚下发新 Plan（StartDate=今天、
    CreatedAt=现在、目标词全新）→ 旧口径必触发 safe_word，修复后应不触发、零洞察零重规划；
  G 宽限期边界：Plan 创建 23h（<24h），窗口内 3 篇产出均不含目标词 → 仍不判定（不触发）；
  S 真安全词：Plan 创建 30h（过宽限期），创建后 3 篇产出均不含目标词 → 应触发 safe_word
    且 InsightAgent 落库（真实 LLM 链路确认修复没有掐死真信号）。
前置：API 以 qwen-plus + nextword_verify_t012_qa 跑在 localhost:5108。
"""
import io
import json
import subprocess
import sys
import time
import urllib.request
import urllib.error

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"
DB = "nextword_verify_t012_qa"
RESULTS = []


def check(name, ok, detail=""):
    RESULTS.append((name, ok, detail))
    print(f"{'PASS' if ok else 'FAIL'} {name} {detail}")


def call(method, path, token=None, body=None):
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    data = json.dumps(body).encode() if body is not None else None
    with urllib.request.urlopen(req, data) as resp:
        return json.loads(resp.read().decode())


def psql(sql):
    out = subprocess.run(
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d", DB, "-t", "-A", "-c", sql],
        capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit(f"psql 失败: {out.stderr}")
    return out.stdout.strip()


def register(name):
    email = f"qa-t012-{name}-{int(time.time())}@example.com"
    resp = call("POST", "/api/auth/register", body={
        "email": email, "password": "Passw0rd!234", "displayName": name})
    uid = psql(f"SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = '{email}'")
    psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid}\'')
    return resp["token"], uid


def seed_plan(uid, targets, start_offset_days, created_interval):
    days = ",".join('{"wordIds":[],"exposureWordIds":[],"sentenceTargets":' + json.dumps(targets) + '}' for _ in range(7))
    content = '{"focusScenarios":["dining_out"],"sourceFindingIds":[],"articleIds":[],"days":[' + days + ']}'
    psql(f'INSERT INTO "LearningPlans" ("UserId","StartDate","ContentJson","ModelProfileId","CreatedAt")'
         f' VALUES (\'{uid}\', CURRENT_DATE + ({start_offset_days}), $${content}$$, \'qa-seed\', now() - interval \'{created_interval}\')')


def seed_free(uid, texts, interval):
    values = ",\n".join(
        f"(gen_random_uuid(), '{uid}', $${t}$$, 60, 'C', '', '[]', '[]', 'Basic', now() - interval '{interval}')"
        for t in texts)
    psql(f'INSERT INTO "FreeExpressionLogs" ("Id","UserId","UserText","AiScore","OverallGrade","AiRevision",'
         f'"ErrorSentences","Suggestions","DifficultyLevel","Timestamp") VALUES\n{values}')


def screen(token):
    return call("POST", "/api/insights/bottleneck/jobs", token, {})


FREE_NEUTRAL = ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night."]

# ══ R：复现 T-012 原误触发场景 ════════════════════════════════════
# 旧口径：planStart=今天 00:00 ≤ 产出（2h 前）→ 3 篇全不含 newtarget → 必触发 safe_word
token_r, uid_r = register("r-repro")
seed_free(uid_r, FREE_NEUTRAL, "2 hour")
seed_plan(uid_r, ["newtargetalpha", "newtargetbeta"], 0, "1 minute")  # 模拟每周兜底刚下发的新 Plan
resp_r = screen(token_r)
check("R 新Plan刚下发不误触发", not resp_r.get("triggered"), f"signals={resp_r.get('signals')}")

# ══ G：宽限期边界（23h < 24h）════════════════════════════════════
token_g, uid_g = register("g-grace23h")
seed_free(uid_g, FREE_NEUTRAL, "2 hour")
seed_plan(uid_g, ["targetfresh"], -1, "23 hour")
resp_g = screen(token_g)
check("G 宽限期内(23h)不判定", not resp_g.get("triggered"), f"signals={resp_g.get('signals')}")

# ══ S：真安全词（过宽限期 30h，产出在 Plan 创建后）══════════════════
token_s, uid_s = register("s-truesafe")
seed_plan(uid_s, ["targetlatealpha", "targetlatebeta"], -1, "30 hour")
seed_free(uid_s, FREE_NEUTRAL, "5 hour")  # 晚于 Plan 创建（30h 前）→ 在窗口内
resp_s = screen(token_s)
check("S 宽限期后真安全词仍触发", resp_s.get("triggered") and "safe_word" in resp_s.get("signals", []),
      f"signals={resp_s.get('signals')}")

# S 的 InsightAgent 真实链路落库（确认修复未破坏后续链路）
if resp_s.get("triggered"):
    deadline = time.time() + 180
    found = None
    while time.time() < deadline:
        try:
            data = call("GET", "/api/insights/bottleneck/latest", token_s)
            if data.get("found"):
                found = data
                break
        except urllib.error.HTTPError:
            pass
        time.sleep(3)
    check("S 洞察落库(SafeWordStrategy)", bool(found) and found["nature"] == "SafeWordStrategy",
          f"nature={found and found['nature']}")

# ══ R/G 零副作用（零洞察零任务零画像零重规划）══════════════════════
time.sleep(8)
for name, uid in [("R", uid_r), ("G", uid_g)]:
    ins = psql(f'SELECT count(*) FROM "BottleneckInsights" WHERE "UserId" = \'{uid}\'')
    jobs = psql(f'SELECT count(*) FROM "BackgroundJobs" WHERE "JobType" = \'BottleneckInsight\' AND "PayloadJson" LIKE \'%{uid}%\'')
    replans = psql(f'SELECT count(*) FROM "BackgroundJobs" WHERE "IdempotencyKey" LIKE \'planner:replan:{uid}%\'')
    profs = psql(f'SELECT count(*) FROM "WeaknessProfiles" WHERE "UserId" = \'{uid}\'')
    check(f"{name} 零副作用(洞察/任务/重规划/画像全 0)",
          ins == "0" and jobs == "0" and replans == "0" and profs == "0",
          f"insights={ins} jobs={jobs} replans={replans} profiles={profs}")

print("\n══ 复验汇总 ══")
failed = [r for r in RESULTS if not r[1]]
print(f"通过 {len(RESULTS) - len(failed)}/{len(RESULTS)}")
for name, ok, detail in failed:
    print(f"  FAIL: {name} {detail}")
sys.exit(1 if failed else 0)
