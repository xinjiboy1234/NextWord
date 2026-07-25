# -*- coding: utf-8 -*-
"""T-006 验收补充实测（周密）：构造有场景维度 Verified weakness Finding 的用户，
验证「主攻场景来自 Verified Finding、生成依据可追溯」真实链路；存疑 Finding 不进 Plan；
接触词纪律（≤20% 全超带；产出/测评选词无超带）；过期回退；同日幂等；
并回扫基线脚本用户的 Plan 内容（7 天 × 带内/超带纪律）。
库：nextword_verify_t006（验完删库）。前置同 verify-planner-t006.py。"""
import io
import json
import subprocess
import sys
import time
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"
DB = "nextword_verify_t006"
CEFR_ORDER = ["A1", "A2", "B1", "B2", "C1", "C2"]


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
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d", DB, "-q", "-t", "-A", "-c", sql],
        capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit(f"psql 失败：{out.stderr}")
    return out.stdout.strip()


def register(name):
    email = f"qa-t006-{name}-{int(time.time() * 1000)}@example.com"
    token = call("POST", "/api/auth/register", body={
        "email": email, "password": "Passw0rd!234", "displayName": name})["token"]
    uid = psql(f'SELECT "Id" FROM "Users" WHERE "Email" = \'{email}\'')
    return token, uid


def set_band(uid, cefr):
    psql(f'''INSERT INTO "UserProgress"
        ("Id","UserId","OverallLevel","VocabLevel","SpellingLevel","SentenceLevel","ReadingLevel",
         "StreakDays","IsLevelLocked","HasCompletedInitialAssessment","PendingReviewCount","IsUpgradeCandidate",
         "CefrDisplay","VocabularyScore")
        VALUES (gen_random_uuid(), '{uid}', '{cefr}','{cefr}','{cefr}','{cefr}','{cefr}',
                0, false, false, 0, false, '{cefr}', 50)
        ON CONFLICT ("UserId") DO UPDATE SET "CefrDisplay" = EXCLUDED."CefrDisplay"''')


def insert_finding(profile_id, dimension, key, polarity, verification, confidence="Medium"):
    return int(psql(f'''INSERT INTO "ProfileFindings"
        ("ProfileId","Dimension","DimensionKey","Polarity","Statement","EvidenceJson","Confidence","Verification")
        VALUES ({profile_id}, '{dimension}', '{key}', '{polarity}', 'QA 构造 Finding', '[]', '{confidence}', '{verification}')
        RETURNING "Id"'''))


def insert_profile(uid):
    return int(psql(f'''INSERT INTO "WeaknessProfiles" ("UserId","ModelProfileId","CreatedAt")
        VALUES ('{uid}', 'qa-seed', now()) RETURNING "Id"'''))


def wait_plan(token, timeout=60):
    deadline = time.time() + timeout
    while time.time() < deadline:
        plan = call("GET", "/api/planner/current", token)
        if plan.get("active"):
            return plan
        time.sleep(2)
    raise SystemExit("Plan 超时未生成")


def word_cefr(lemmas):
    if not lemmas:
        return {}
    quoted = ",".join("'" + lemma.replace("'", "''") + "'" for lemma in lemmas)
    rows = psql(f'SELECT "Lemma" || E\'\\t\' || "CefrLevel" FROM "Words" WHERE "Lemma" IN ({quoted})')
    return dict(row.split("\t") for row in rows.splitlines() if row)


# ══ 场景一：Verified 场景 weakness 驱动主攻场景（真实链路重点项） ══
print("── 场景一：Verified 场景 weakness Finding 用户 ──")
token_a, uid_a = register("scenarioweak")
set_band(uid_a, "B1")
pid = insert_profile(uid_a)
f_dining = insert_finding(pid, "Scenario", "dining_out", "Weakness", "Verified")
f_interview = insert_finding(pid, "Scenario", "work_smalltalk", "Weakness", "Verified", "Low")
f_travel_q = insert_finding(pid, "Scenario", "travel_lodging", "Weakness", "Questioned")
f_grammar = insert_finding(pid, "Skill", "grammar", "Weakness", "Verified", "High")
print(f"findings: dining(Verified)={f_dining} interview(Verified)={f_interview} "
      f"travel(Questioned)={f_travel_q} grammar(Verified,skill)={f_grammar}")

call("POST", "/api/planner/jobs", token_a, {})
plan = wait_plan(token_a)
print(f"plan: focus={plan['focusScenarios']} sourceFindings={plan['sourceFindingIds']} "
      f"today={plan['todayWordCount']}+{plan['todayExposureCount']} targets={plan['todaySentenceTargets']}")

# 主攻场景必须全部来自 Verified 场景 weakness，且生成依据精确等于这两条 id
assert plan["focusScenarios"], "主攻场景为空"
assert set(plan["focusScenarios"]) <= {"dining_out", "work_smalltalk"}, \
    f"主攻场景混入非 Verified 场景：{plan['focusScenarios']}"
assert set(plan["sourceFindingIds"]) == {f_dining, f_interview}, \
    f"生成依据不等于 Verified 场景 Finding：{plan['sourceFindingIds']}"
assert f_travel_q not in plan["sourceFindingIds"], "存疑 Finding 进入生成依据"
assert f_grammar not in plan["sourceFindingIds"], "技能维度 Finding 进入场景生成依据"
assert "travel_lodging" not in plan["focusScenarios"], "存疑场景成为主攻场景"
print("① 主攻场景来自 Verified 场景 Finding、生成依据精确可追溯、存疑/技能维度被排除 ✓")

# 接触词纪律：每日词 fromPlan、≤2 个、全部超带；带内词不超带
daily = call("GET", "/api/words/daily?count=10", token_a)
assert daily and all(item.get("fromPlan") for item in daily), "每日词未执行 Plan"
lemmas = [item["lemma"] for item in daily]
cefr = word_cefr(lemmas)
exposure = [item["lemma"] for item in daily if item.get("isExposure")]
inband = [item["lemma"] for item in daily if not item.get("isExposure")]
assert len(exposure) <= 2, f"接触词占比超 20%：{len(exposure)}/10"
assert all(CEFR_ORDER.index(cefr[w]) > CEFR_ORDER.index("B1") for w in exposure), \
    f"接触词未全部超带：{[(w, cefr[w]) for w in exposure]}"
assert all(CEFR_ORDER.index(cefr[w]) <= CEFR_ORDER.index("B1") for w in inband), \
    f"带内词混超带：{[(w, cefr[w]) for w in inband]}"
print(f"② 接触词 {len(exposure)}/10 全部超带（{[(w, cefr[w]) for w in exposure]}）、"
      f"带内词全部 ≤B1 ✓")

# 产出任务（造句）：Plan 目标、无超带
prompts = call("GET", "/api/sentences/prompts?count=3", token_a)
assert prompts and all(p.get("fromPlan") for p in prompts), "造句出题未执行 Plan"
targets = [p["targetWord"] for p in prompts]
assert set(targets) <= set(plan["todaySentenceTargets"]), "造句目标与 Plan 不符"
tcefr = word_cefr(targets)
assert all(CEFR_ORDER.index(tcefr[w]) <= CEFR_ORDER.index("B1") for w in targets), \
    f"产出任务选词超带：{[(w, tcefr[w]) for w in targets]}"
print(f"③ 造句目标 {[(w, tcefr[w]) for w in targets]} 全部带内、来自 Plan ✓")

rec = call("GET", "/api/articles/recommended", token_a)
assert rec.get("fromPlan"), "阅读推荐未来自 Plan"
print(f"④ 阅读推荐 fromPlan ✓（{[a['title'] for a in rec['articles']]}）")

# 同日幂等
call("POST", "/api/planner/jobs", token_a, {})
call("POST", "/api/planner/jobs", token_a, {})
time.sleep(6)
count = psql(f'SELECT count(*) FROM "LearningPlans" WHERE "UserId" = \'{uid_a}\'')
assert count == "1", f"同日重复触发不幂等：{count}"
print(f"⑤ 同日幂等 ✓（LearningPlans={count}）")

# 过期回退：StartDate 拨回 8 天
psql(f'UPDATE "LearningPlans" SET "StartDate" = current_date - 8 WHERE "UserId" = \'{uid_a}\'')
expired = call("GET", "/api/planner/current", token_a)
assert not expired.get("active"), "过期 Plan 仍视为有效"
daily_e = call("GET", "/api/words/daily?count=10", token_a)
assert daily_e and all(not item.get("fromPlan") for item in daily_e), "过期后每日词未回退难度带"
prompts_e = call("GET", "/api/sentences/prompts?count=3", token_a)
assert all(not p.get("fromPlan") for p in prompts_e), "过期后造句未回退"
rec_e = call("GET", "/api/articles/recommended", token_a)
assert not rec_e.get("fromPlan"), "过期后阅读推荐未回退"
psql(f'DELETE FROM "LearningPlans" WHERE "UserId" = \'{uid_a}\'')
print("⑥ Plan 过期（>7 天）→ planner/current 失效 + 每日词/造句/阅读三处全部回退 ✓")

# ══ 场景二：画像只有存疑 Finding → 兜底且生成依据为空 ══
print("\n── 场景二：仅存疑 Finding 用户 ──")
token_b, uid_b = register("questionedonly")
set_band(uid_b, "A2")
pid_b = insert_profile(uid_b)
fq = insert_finding(pid_b, "Scenario", "travel_lodging", "Weakness", "Questioned")
call("POST", "/api/planner/jobs", token_b, {})
plan_b = wait_plan(token_b)
assert plan_b["sourceFindingIds"] == [], f"存疑 Finding 进入生成依据：{plan_b['sourceFindingIds']}"
assert 1 <= len(plan_b["focusScenarios"]) <= 2, "兜底主攻场景数量异常"
print(f"⑦ 仅存疑 Finding → 生成依据为空、走覆盖率兜底 ✓（focus={plan_b['focusScenarios']}）")

# ══ 场景三：测评词池无超带（真实链路抽验） ══
print("\n── 场景三：测评选词带内抽验 ──")
token_c, _ = register("assesspool")
aid = call("POST", "/api/assessment/initial/start", token_c, {})["assessmentId"]
resp = call("GET", f"/api/assessment/{aid}/next-block", token_c)
block = resp["block"]
band = block["band"]
pool_words = [p["targetWord"] for p in block["production"] if p.get("targetWord")] + [block["vocabulary"][0]["word"]]
pcefr = word_cefr(pool_words)
over = [(w, pcefr.get(w)) for w in pool_words
        if w in pcefr and CEFR_ORDER.index(pcefr[w]) > CEFR_ORDER.index(band)]
assert not over, f"测评词池超带：{over}（块带 {band}）"
print(f"⑧ 测评首块（band={band}）选词 {[(w, pcefr.get(w)) for w in pool_words]} 无超带 ✓")

# ══ 场景四：回扫基线用户 Plan 内容（7 天纪律 + 主攻场景词源） ══
print("\n── 场景四：基线真实链路用户 Plan 内容回扫 ──")
rows = psql('''SELECT u."Email" || E'\\t' || up."CefrDisplay" || E'\\t' || p."ContentJson"
    FROM "LearningPlans" p JOIN "Users" u ON u."Id" = p."UserId"
    LEFT JOIN "UserProgress" up ON up."UserId" = p."UserId" ORDER BY u."Email"''')
for row in rows.splitlines():
    parts = row.split("\t", 2)
    if len(parts) < 3:
        continue
    email, band, content_json = parts
    band = band or "A2"
    content = json.loads(content_json)
    focus = content["focusScenarios"]
    src = content["sourceFindingIds"]
    # 生成依据必须全部指向该用户画像的 Verified 场景 weakness Finding
    if src:
        id_list = ",".join(str(i) for i in src)
        bad = psql(f'''SELECT count(*) FROM "ProfileFindings" f
            JOIN "WeaknessProfiles" wp ON wp."Id" = f."ProfileId"
            JOIN "Users" u ON u."Id" = wp."UserId"
            WHERE u."Email" = '{email}' AND f."Id" IN ({id_list})
              AND NOT (f."Verification" = 'Verified' AND f."Dimension" = 'Scenario' AND f."Polarity" = 'Weakness')''')
        assert bad == "0", f"{email} 生成依据含非 Verified 场景 weakness Finding"
    word_ids = set()
    exposure_ids = set()
    for day in content["days"]:
        assert len(day["exposureWordIds"]) <= 2, f"{email} 某天接触词超 2 个"
        word_ids.update(day["wordIds"])
        exposure_ids.update(day["exposureWordIds"])
    def cefr_of(ids):
        if not ids:
            return {}
        id_list = ",".join(f"'{i}'" for i in ids)
        r = psql(f'SELECT "Id" || E\'\\t\' || "CefrLevel" FROM "Words" WHERE "Id" IN ({id_list})')
        return dict(x.split("\t") for x in r.splitlines() if x)
    wc, ec = cefr_of(word_ids), cefr_of(exposure_ids)
    assert all(CEFR_ORDER.index(v) <= CEFR_ORDER.index(band) for v in wc.values()), \
        f"{email} 带内队列混超带词：{set(wc.values())}（用户 {band}）"
    assert all(CEFR_ORDER.index(v) > CEFR_ORDER.index(band) for v in ec.values()), \
        f"{email} 接触词未全超带：{set(ec.values())}（用户 {band}）"
    print(f"⑨ {email[:44]} band={band} focus={focus} src={len(src)} 条 "
          f"7 天带内 {len(wc)} 词全 ≤{band}、接触词 {len(ec)} 词全 >{band} ✓")

print("\nQA 补充实测全部通过。")
