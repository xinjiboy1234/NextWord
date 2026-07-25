# -*- coding: utf-8 -*-
"""T-007 真实 LLM 链路实测（DashScope qwen-plus + 独立验证库 nextword_verify_t007）。
流程：注册平台期用户 A（psql 置 assessed + 播 10 条扁平低分造句留痕）
     → POST /api/insights/bottleneck/jobs（指标筛查，零 LLM）触发 plateau
     → 等 BottleneckInsight 任务（InsightAgent 真实细读原文）→ 断言：
       ① 洞察落库：nature 属于 7 类瓶颈性质、statement 非空、证据引用 ⊆ 真实 SentenceLog、ReplanTriggered=true；
       ② 事件驱动重规划：WeaknessProfiles 新增 AssessmentId 为空的画像 + planner:replan 任务 → force Plan 落库；
       ③ 同日幂等：再次触发复用同一 job，洞察仍 1 行；
       ④ 正常用户 B（分数爬升 + 连接词稳定）不触发：triggered=false 且零洞察零 LLM；
       ⑤ 每周兜底：planner:weekly:{user}:{ISO 周} 任务存在且 force，处理后 Plan 仍 1 行（原地重建）。
验完删库（见 development-log T-007 记录），不动 dev 库 nextword。
前置：API 以 DashScope 真实 LLM + 连接串指向 nextword_verify_t007 运行在 localhost:5108。"""
import io
import json
import subprocess
import sys
import time
import urllib.request
import urllib.error

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"
DB = "nextword_verify_t007"

NATURES = {
    "VocabularyInsufficient", "CannotOrganizeSentences", "GrammarErrors",
    "MonotonousExpression", "AvoidancePattern", "ChinglishCollocation", "SafeWordStrategy"}


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
    email = f"verify-t007-{name}-{int(time.time())}@example.com"
    resp = call("POST", "/api/auth/register", body={
        "email": email, "password": "Passw0rd!234", "displayName": name})
    uid = psql(f"SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = '{email}'")
    return resp["token"], uid


def wait_value(producer, predicate, timeout=180, label="resource"):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            value = producer()
            if predicate(value):
                return value
        except urllib.error.HTTPError:
            pass
        time.sleep(3)
    raise SystemExit(f"{label} 超时未就绪")


def seed_sentence_logs(uid, rows):
    values = ",\n".join(
        f"(gen_random_uuid(), '{uid}', 'word{i}', 'life', $${text}$$, '', {score}, {score}, {score}, {score}, "
        f"'C', '[]', 'Basic', '', now() - interval '{len(rows) - i} day')"
        for i, (text, score) in enumerate(rows))
    psql(f'INSERT INTO "SentenceLogs" ("Id","UserId","TargetWord","Scene","UserSentence","AiRevision",'
         f'"GrammarScore","NaturalScore","VocabularyScore","RelevanceScore","OverallGrade","ErrorTags",'
         f'"DifficultyLevel","Suggestion","Timestamp") VALUES\n{values}')


# ── 用户 A：平台期（10 次产出四维均分恒 2、10 天内持续活跃）────────────
token_a, uid_a = register("plateau")
psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid_a}\'')
flat_rows = [
    ("He go to school every day.", 2),
    ("She don't like apples.", 2),
    ("They was happy yesterday.", 2),
    ("I has a dog at home.", 2),
    ("We goes to park on Sunday.", 2),
    ("He don't know the answer.", 2),
    ("She have two brother.", 2),
    ("I am go to bed early.", 2),
    ("They plays football every week.", 2),
    ("He don't want no help.", 2),
]
seed_sentence_logs(uid_a, flat_rows)
log_ids = set(psql(f'SELECT "Id" FROM "SentenceLogs" WHERE "UserId" = \'{uid_a}\'').splitlines())
assert len(log_ids) == 10
print(f"用户 A: {uid_a[:8]}... 播种 10 条平台期造句")

resp = call("POST", "/api/insights/bottleneck/jobs", token_a, {})
assert resp.get("triggered"), f"平台期未触发: {resp}"
assert "plateau" in resp["signals"], f"信号异常: {resp}"
job_id = resp["jobId"]
print(f"① 指标筛查触发: signals={resp['signals']} jobId={job_id}")

# ① 等 InsightAgent（真实 LLM 细读原文）产出洞察
latest = wait_value(
    lambda: call("GET", "/api/insights/bottleneck/latest", token_a),
    lambda data: data.get("found"),
    timeout=180, label="BottleneckInsight")
assert latest["nature"] in NATURES, f"瓶颈性质非法: {latest['nature']}"
assert latest["statement"].strip(), "洞察结论为空"
evidence = set(latest["evidenceLogIds"])
assert evidence, "洞察无证据引用"
assert evidence <= log_ids, f"证据引用越权/编造: {evidence - log_ids}"
assert latest["replanTriggered"], "首次发现瓶颈应触发重规划"
print(f"① 洞察落库: nature={latest['nature']} evidence={len(evidence)} 条（全部真实） replan=True")
print(f"   statement: {latest['statement']}")

# ② 事件驱动重规划：画像重生成（AssessmentId 空）+ planner:replan → force Plan
# （画像重生成与洞察落库同属一个后台任务、在洞察之后完成，需轮询等待）
profile_rows = int(wait_value(
    lambda: psql(f'SELECT count(*) FROM "WeaknessProfiles" WHERE "UserId" = \'{uid_a}\' AND "AssessmentId" IS NULL'),
    lambda count: count != "0", timeout=120, label="重生成画像"))
assert profile_rows == 1, f"重生成画像应为 1 行，实际 {profile_rows}"
replan_key = wait_value(
    lambda: psql(f'SELECT "IdempotencyKey" FROM "BackgroundJobs" WHERE "JobType" = \'Planner\' AND "IdempotencyKey" LIKE \'planner:replan:{uid_a}%\''),
    lambda value: bool(value), timeout=60, label="planner:replan 任务")
assert '"force":true' in psql(f'SELECT "PayloadJson" FROM "BackgroundJobs" WHERE "IdempotencyKey" = \'{replan_key}\''), "重规划任务未带 force"
wait_value(
    lambda: psql(f'SELECT "Status" FROM "BackgroundJobs" WHERE "IdempotencyKey" = \'{replan_key}\''),
    lambda status: status == "Completed", timeout=120, label="planner:replan 完成")
plan_count = int(psql(f'SELECT count(*) FROM "LearningPlans" WHERE "UserId" = \'{uid_a}\''))
assert plan_count == 1, f"重规划后 Plan 应为 1 行，实际 {plan_count}"
print(f"② 事件驱动重规划: 画像重生成 1 行 + {replan_key.split(':')[0]} 任务 force → Plan 落库 {plan_count} 行")

# ③ 同日幂等：再次触发复用同一 job，洞察仍 1 行
resp2 = call("POST", "/api/insights/bottleneck/jobs", token_a, {})
assert resp2["jobId"] == job_id, f"同日重复触发未复用 job: {resp2['jobId']} != {job_id}"
time.sleep(5)
insight_count = int(psql(f'SELECT count(*) FROM "BottleneckInsights" WHERE "UserId" = \'{uid_a}\''))
assert insight_count == 1, f"同日洞察应幂等为 1 行，实际 {insight_count}"
print(f"③ 同日幂等: 重复触发复用 jobId={job_id}，洞察仍 {insight_count} 行")

# ── 用户 B：正常（分数爬升 + 连接词稳定）不触发、零 LLM ─────────────
token_b, uid_b = register("normal")
psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid_b}\'')
improving_rows = [
    ("I stayed home because it was raining while my friend waited.", 1 + i // 3)
    for i in range(12)
]
seed_sentence_logs(uid_b, improving_rows)
resp_b = call("POST", "/api/insights/bottleneck/jobs", token_b, {})
assert not resp_b.get("triggered"), f"正常用户误触发: {resp_b}"
time.sleep(3)
b_insights = int(psql(f'SELECT count(*) FROM "BottleneckInsights" WHERE "UserId" = \'{uid_b}\''))
b_jobs = int(psql(f'SELECT count(*) FROM "BackgroundJobs" WHERE "JobType" = \'BottleneckInsight\' AND "PayloadJson" LIKE \'%{uid_b}%\''))
assert b_insights == 0 and b_jobs == 0, f"正常用户产生洞察/任务: insights={b_insights} jobs={b_jobs}"
print(f"④ 正常用户零触发: triggered=false、洞察 {b_insights} 行、洞察任务 {b_jobs} 个（零 LLM）")

# ⑤ 每周兜底：WeeklyReplanWorker（API 启动 60s 后首次运行）为活跃用户入队 force Planner
weekly_key = wait_value(
    lambda: psql(f'SELECT "IdempotencyKey" FROM "BackgroundJobs" WHERE "IdempotencyKey" LIKE \'planner:weekly:{uid_a}%\''),
    lambda value: bool(value), timeout=150, label="每周兜底任务")
assert '"force":true' in psql(f'SELECT "PayloadJson" FROM "BackgroundJobs" WHERE "IdempotencyKey" = \'{weekly_key}\''), "兜底任务未带 force"
weekly_key_b = psql(f'SELECT "IdempotencyKey" FROM "BackgroundJobs" WHERE "IdempotencyKey" LIKE \'planner:weekly:{uid_b}%\'')
assert weekly_key_b, "正常用户未获得每周兜底任务"
wait_value(
    lambda: psql(f'SELECT "Status" FROM "BackgroundJobs" WHERE "IdempotencyKey" = \'{weekly_key}\''),
    lambda status: status == "Completed", timeout=120, label="每周兜底任务完成")
plan_count_a = int(psql(f'SELECT count(*) FROM "LearningPlans" WHERE "UserId" = \'{uid_a}\''))
plan_count_b = int(psql(f'SELECT count(*) FROM "LearningPlans" WHERE "UserId" = \'{uid_b}\''))
assert plan_count_a == 1, f"force 原地重建应为 1 行，实际 {plan_count_a}"
assert plan_count_b == 1, f"兜底后正常用户应有 1 份 Plan，实际 {plan_count_b}"
print(f"⑤ 每周兜底: {weekly_key.split(':')[0]} 任务 force 完成，A/B 各 1 份 Plan（原地重建不重复）")

rows = psql('SELECT "Nature", "ReplanTriggered" FROM "BottleneckInsights"')
print(f"\nDB BottleneckInsights: {rows}")
print("\n真实 LLM 链路实测通过：指标筛查触发/不误触发 → InsightAgent 细读原文落库带证据 → 性质变化重规划（画像+force Plan）→ 同日幂等 → 每周兜底。")
