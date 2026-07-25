# -*- coding: utf-8 -*-
"""T-007 验收实测（周密 QA）：真实 DashScope qwen-plus 链路 + 独立库 nextword_verify_t007_qa。
覆盖验收标准 1/2/3/5 + 重点 B 阈值边界：
  A  平台期（四维恒 2，10 天内）→ 触发 plateau；洞察落库带真实证据；首次发现→重规划
  B  正常用户（分数爬升+连接词稳定）→ 零触发零 LLM 痕迹
  C  边界：近 3 次四维各 +1（斜率 0.127>0.05）→ 不判平台期
  D  边界：仅最后 1 次四维各 +1（斜率 0.049≤0.05）→ 仍判平台期（阈值下沿）
  E  边界：均分 2/4 剧烈交替（斜率 0 但标准差 1.0>0.5）→ 不判平台期
  F  边界：扁平但跨度 36 天（>30 天）→ 不判平台期（非持续活跃）
  G  边界：回避恰腰斩（后半=前半×0.5，≤ 含边界）→ 触发 avoidance
  H  边界：回避近miss（后半=前半×0.75）→ 不触发
  I  安全词：Plan 目标词 3 篇自由产出全绕开 → 触发 safe_word
  J  安全词近miss：1 篇用了目标词 → 不触发
  K  性质变化：昨日洞察 VocabularyInsufficient + 今日语法烂句 → LLM 判别的性质 → 重规划
  L  性质未变：昨日洞察 GrammarErrors + 今日同样语法烂句 → 期望不重规划（观测 LLM 一致性）
前置：API 以 qwen-plus + nextword_verify_t007_qa 跑在 localhost:5108（启动 <60s，首轮每周兜底尚无用户）。
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
DB = "nextword_verify_t007_qa"

NATURES = {
    "VocabularyInsufficient", "CannotOrganizeSentences", "GrammarErrors",
    "MonotonousExpression", "AvoidancePattern", "ChinglishCollocation", "SafeWordStrategy"}

RESULTS = []  # (用例, 通过?, 说明)


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
    email = f"qa-t007-{name}-{int(time.time())}@example.com"
    resp = call("POST", "/api/auth/register", body={
        "email": email, "password": "Passw0rd!234", "displayName": name})
    uid = psql(f"SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = '{email}'")
    psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid}\'')
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
    """rows: [(text, g, n, v, r, days_ago)] 时间正序。"""
    values = ",\n".join(
        f"(gen_random_uuid(), '{uid}', 'word{i}', 'life', $${text}$$, '', {g}, {n}, {v}, {r}, "
        f"'C', '[]', 'Basic', '', now() - interval '{days_ago} day')"
        for i, (text, g, n, v, r, days_ago) in enumerate(rows))
    psql(f'INSERT INTO "SentenceLogs" ("Id","UserId","TargetWord","Scene","UserSentence","AiRevision",'
         f'"GrammarScore","NaturalScore","VocabularyScore","RelevanceScore","OverallGrade","ErrorTags",'
         f'"DifficultyLevel","Suggestion","Timestamp") VALUES\n{values}')


def flat(uid, score, days=1.0, count=10):
    seed_sentence_logs(uid, [(f"Flat sentence number {i}.", score, score, score, score, (count - i) * days) for i in range(count)])


def insert_yesterday_insight(uid, nature):
    psql(f'INSERT INTO "BottleneckInsights" ("UserId","Nature","Signals","Statement","EvidenceJson","ReplanTriggered","ModelProfileId","CreatedAt")'
         f' VALUES (\'{uid}\', \'{nature}\', \'plateau\', \'QA 预置昨日洞察\', \'[]\', true, \'qa-seed\', now() - interval \'1 day\')')


def seed_plan(uid, targets):
    days = ",".join('{"wordIds":[],"exposureWordIds":[],"sentenceTargets":' + json.dumps(targets) + '}' for _ in range(7))
    content = '{"focusScenarios":["dining_out"],"sourceFindingIds":[],"articleIds":[],"days":[' + days + ']}'
    psql(f'INSERT INTO "LearningPlans" ("UserId","StartDate","ContentJson","ModelProfileId","CreatedAt")'
         f' VALUES (\'{uid}\', CURRENT_DATE - 1, $${content}$$, \'qa-seed\', now())')


def seed_free(uid, texts):
    values = ",\n".join(
        f"(gen_random_uuid(), '{uid}', $${t}$$, 60, 'C', '', '[]', '[]', 'Basic', now() - interval '{i} hour')"
        for i, t in enumerate(texts))
    psql(f'INSERT INTO "FreeExpressionLogs" ("Id","UserId","UserText","AiScore","OverallGrade","AiRevision",'
         f'"ErrorSentences","Suggestions","DifficultyLevel","Timestamp") VALUES\n{values}')


# ══ A：平台期触发 → 洞察落库 → 首次发现重规划 ═══════════════════
token_a, uid_a = register("a-plateau")
bad_sentences = [
    "He go to school every day.", "She don't like apples.", "They was happy yesterday.",
    "I has a dog at home.", "We goes to park on Sunday.", "He don't know the answer.",
    "She have two brother.", "I am go to bed early.", "They plays football every week.",
    "He don't want no help."]
seed_sentence_logs(uid_a, [(t, 2, 2, 2, 2, 10 - i) for i, t in enumerate(bad_sentences)])
log_ids_a = set(psql(f'SELECT "Id" FROM "SentenceLogs" WHERE "UserId" = \'{uid_a}\'').splitlines())

resp = call("POST", "/api/insights/bottleneck/jobs", token_a, {})
check("A 平台期触发", resp.get("triggered") and "plateau" in resp["signals"], f"signals={resp.get('signals')}")

latest = wait_value(lambda: call("GET", "/api/insights/bottleneck/latest", token_a),
                    lambda d: d.get("found"), timeout=180, label="A 洞察")
evidence = set(latest["evidenceLogIds"])
check("A 洞察落库(性质合法/结论非空)", latest["nature"] in NATURES and bool(latest["statement"].strip()),
      f"nature={latest['nature']}")
check("A 证据引用⊆真实SentenceLog", bool(evidence) and evidence <= log_ids_a, f"evidence={len(evidence)} 条")
check("A 首次发现→ReplanTriggered", latest["replanTriggered"] is True)
print(f"   A statement: {latest['statement']}")

# 重规划链路：画像重生成 + planner:replan force → Plan 落库
wait_value(lambda: psql(f'SELECT count(*) FROM "WeaknessProfiles" WHERE "UserId" = \'{uid_a}\' AND "AssessmentId" IS NULL'),
           lambda c: c != "0", timeout=120, label="A 画像重生成")
replan_key = wait_value(
    lambda: psql(f'SELECT "IdempotencyKey" FROM "BackgroundJobs" WHERE "IdempotencyKey" LIKE \'planner:replan:{uid_a}%\''),
    lambda v: bool(v), timeout=60, label="A planner:replan")
wait_value(lambda: psql(f'SELECT "Status" FROM "BackgroundJobs" WHERE "IdempotencyKey" = \'{replan_key}\''),
           lambda s: s == "Completed", timeout=180, label="A replan 完成")
plan_a = int(psql(f'SELECT count(*) FROM "LearningPlans" WHERE "UserId" = \'{uid_a}\''))
check("A 事件驱动重规划→Plan 落库", plan_a == 1, f"plans={plan_a}")
focus_a = psql(f'SELECT "ContentJson"::jsonb->>\'focusScenarios\' FROM "LearningPlans" WHERE "UserId" = \'{uid_a}\'')
print(f"   A 新 Plan 主攻场景: {focus_a}")

# 同日幂等
resp2 = call("POST", "/api/insights/bottleneck/jobs", token_a, {})
time.sleep(5)
insight_count_a = int(psql(f'SELECT count(*) FROM "BottleneckInsights" WHERE "UserId" = \'{uid_a}\''))
check("A 同日幂等(复用job+洞察1行)", resp2["jobId"] == resp["jobId"] and insight_count_a == 1,
      f"insights={insight_count_a}")

# ══ B：正常用户零触发 ═════════════════════════════════════════
token_b, uid_b = register("b-normal")
seed_sentence_logs(uid_b, [("I stayed home because it was raining while my friend waited.",
                            1 + i // 3, 1 + i // 3, 1 + i // 3, 1 + i // 3, 12 - i) for i in range(12)])
resp_b = call("POST", "/api/insights/bottleneck/jobs", token_b, {})
check("B 正常用户不触发", not resp_b.get("triggered"))

# ══ C/D/E/F：平台期阈值边界 ═══════════════════════════════════
def screen(token):
    return call("POST", "/api/insights/bottleneck/jobs", token, {})


def boundary_user(name, rows):
    token, uid = register(name)
    seed_sentence_logs(uid, rows)
    return token, uid, screen(token)


# C：近 3 次四维各 +1 → 斜率 0.127 > 0.05 → 不触发
token_c, uid_c, resp_c = boundary_user("c-ramp3", [
    (f"Ramp sentence {i}.", 4 if i >= 7 else 3, 4 if i >= 7 else 3, 4 if i >= 7 else 3, 4 if i >= 7 else 3, 10 - i)
    for i in range(10)])
check("C 近3次各+1(斜率0.127)不判平台期", not resp_c.get("triggered"), f"signals={resp_c.get('signals')}")

# D：仅最后 1 次四维各 +1 → 斜率 0.049 ≤ 0.05、标准差 0.3 → 仍触发（阈值下沿）
token_d, uid_d, resp_d = boundary_user("d-bump1", [
    (f"Slight bump sentence {i}.", 4 if i == 9 else 3, 4 if i == 9 else 3, 4 if i == 9 else 3, 4 if i == 9 else 3, 10 - i)
    for i in range(10)])
check("D 仅末次+1(斜率0.049)仍判平台期", resp_d.get("triggered") and "plateau" in resp_d["signals"],
      f"signals={resp_d.get('signals')}")

# E：2/4 交替（斜率 0、标准差 1.0）→ 不触发
token_e, uid_e, resp_e = boundary_user("e-volatile", [
    (f"Volatile sentence {i}.", 2 if i % 2 == 0 else 4, 2 if i % 2 == 0 else 4,
     2 if i % 2 == 0 else 4, 2 if i % 2 == 0 else 4, 10 - i) for i in range(10)])
check("E 剧烈波动(标准差1.0)不判平台期", not resp_e.get("triggered"), f"signals={resp_e.get('signals')}")

# F：扁平但跨度 36 天 → 不触发
token_f, uid_f, resp_f = boundary_user("f-sparse", [
    (f"Sparse sentence {i}.", 3, 3, 3, 3, 36 - 4 * i) for i in range(10)])
check("F 扁平但跨36天不判平台期", not resp_f.get("triggered"), f"signals={resp_f.get('signals')}")

# ══ G/H：回避模式边界 ════════════════════════════════════════
# G：前半每句 because+while（率 2.0），后半每句 because（率 1.0 = 恰腰斩，≤含边界）→ 触发
rows_g = [("I stayed home because it rained while my friend waited." if i < 6
           else "I stayed home because it rained.", i // 2, i // 2, i // 2, i // 2, 12 - i) for i in range(12)]
token_g, uid_g, resp_g = boundary_user("g-avoid-edge", rows_g)
check("G 回避恰腰斩(后半=前半×0.5)触发", resp_g.get("triggered") and "avoidance" in resp_g["signals"],
      f"signals={resp_g.get('signals')}")

# H：后半率 1.5（2,1 交替 > 腰斩线）→ 不触发
rows_h = [("I stayed home because it rained while my friend waited." if i < 6
           else ("I stayed home because it rained while he left." if i % 2 == 0
                 else "I stayed home because it rained."),
           i // 2, i // 2, i // 2, i // 2, 12 - i) for i in range(12)]
token_h, uid_h, resp_h = boundary_user("h-avoid-miss", rows_h)
check("H 回避近miss(后半=前半×0.75)不触发", not resp_h.get("triggered"), f"signals={resp_h.get('signals')}")

# ══ I/J：安全词策略 ══════════════════════════════════════════
token_i, uid_i = register("i-safeword")
seed_plan(uid_i, ["targetalpha", "targetbeta"])
seed_free(uid_i, ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night."])
resp_i = screen(token_i)
check("I 安全词(目标词0出现)触发", resp_i.get("triggered") and "safe_word" in resp_i["signals"],
      f"signals={resp_i.get('signals')}")

token_j, uid_j = register("j-safeword-miss")
seed_plan(uid_j, ["targetgamma"])
seed_free(uid_j, ["I enjoy my daily routine.", "The targetgamma idea works well.", "She cooks dinner every night."])
resp_j = screen(token_j)
check("J 安全词近miss(1篇用目标词)不触发", not resp_j.get("triggered"), f"signals={resp_j.get('signals')}")

# ══ K/L：性质变化 vs 未变（真实 LLM 判定）══════════════════════
token_k, uid_k = register("k-changed")
seed_sentence_logs(uid_k, [(t, 2, 2, 2, 2, 10 - i) for i, t in enumerate(bad_sentences)])
insert_yesterday_insight(uid_k, "VocabularyInsufficient")
resp_k = screen(token_k)
check("K 触发(平台期)", resp_k.get("triggered"), f"signals={resp_k.get('signals')}")
latest_k = wait_value(lambda: call("GET", "/api/insights/bottleneck/latest", token_k),
                      lambda d: d.get("found") and d.get("statement") != "QA 预置昨日洞察",
                      timeout=180, label="K 洞察")
check("K 性质变化→重规划", latest_k["replanTriggered"] is True,
      f"昨日=VocabularyInsufficient 今日={latest_k['nature']}")
if latest_k["replanTriggered"]:
    wait_value(lambda: psql(f'SELECT "Status" FROM "BackgroundJobs" WHERE "IdempotencyKey" LIKE \'planner:replan:{uid_k}%\''),
               lambda s: s == "Completed", timeout=180, label="K replan 完成")

token_l, uid_l = register("l-unchanged")
seed_sentence_logs(uid_l, [(t, 2, 2, 2, 2, 10 - i) for i, t in enumerate(bad_sentences)])
insert_yesterday_insight(uid_l, "GrammarErrors")
resp_l = screen(token_l)
check("L 触发(平台期)", resp_l.get("triggered"), f"signals={resp_l.get('signals')}")
latest_l = wait_value(lambda: call("GET", "/api/insights/bottleneck/latest", token_l),
                      lambda d: d.get("found") and d.get("statement") != "QA 预置昨日洞察",
                      timeout=180, label="L 洞察")
# LLM 判定有不确定性：今日 nature 与昨日 GrammarErrors 相同 → 期望不重规划
if latest_l["nature"] == "GrammarErrors":
    check("L 性质未变→只记录不重规划", latest_l["replanTriggered"] is False, f"今日={latest_l['nature']}")
    time.sleep(5)
    replan_jobs_l = psql(f'SELECT count(*) FROM "BackgroundJobs" WHERE "IdempotencyKey" LIKE \'planner:replan:{uid_l}%\'')
    profiles_l = psql(f'SELECT count(*) FROM "WeaknessProfiles" WHERE "UserId" = \'{uid_l}\'')
    check("L 无重规划副作用(无replan任务/无新画像)", replan_jobs_l == "0" and profiles_l == "0",
          f"replanJobs={replan_jobs_l} profiles={profiles_l}")
else:
    check("L LLM判定一致性", False,
          f"LLM 对同样语法烂句判为 {latest_l['nature']}（昨日预置 GrammarErrors）→ 记录为口径风险实测样本")

# ══ 零 LLM 痕迹核对（B/C/E/F/H/J）══════════════════════════════
time.sleep(8)
for name, uid in [("B", uid_b), ("C", uid_c), ("E", uid_e), ("F", uid_f), ("H", uid_h), ("J", uid_j)]:
    ins = psql(f'SELECT count(*) FROM "BottleneckInsights" WHERE "UserId" = \'{uid}\'')
    jobs = psql(f'SELECT count(*) FROM "BackgroundJobs" WHERE "JobType" = \'BottleneckInsight\' AND "PayloadJson" LIKE \'%{uid}%\'')
    profs = psql(f'SELECT count(*) FROM "WeaknessProfiles" WHERE "UserId" = \'{uid}\'')
    check(f"{name} 零LLM痕迹(洞察0/任务0/画像0)", ins == "0" and jobs == "0" and profs == "0",
          f"insights={ins} jobs={jobs} profiles={profs}")

print("\n══ 实测汇总 ══")
failed = [r for r in RESULTS if not r[1]]
print(f"通过 {len(RESULTS) - len(failed)}/{len(RESULTS)}")
for name, ok, detail in failed:
    print(f"  FAIL: {name} {detail}")
sys.exit(1 if failed else 0)
