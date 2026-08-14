# -*- coding: utf-8 -*-
"""T-050/T-051/T-052 验收实测（周密 QA）：独立库 nextword_qa_t05052（验完删库，不动 dev 库）。
前置：API 以 Development + 连接串指向 nextword_qa_t05052 运行在 localhost:5129（自动迁移+种子）。
覆盖：
  T-050 ① GET /api/words/daily 默认 15 词；count=10/20 可选生效；响应带 stage/quizMode 字段（回忆考察口径字段存在）；
        ② 12+3 接触词/回忆考察位 ≥40% 口径由 LearningPlanTests 守护（本脚本只验 API 层）。
  T-051 ① GET /api/spelling/queue 默认 count=12。
  T-052 ① mode=review 只返回到期词（isReview=true，未到期关系不出现）；
        ② mode=new 只返回新词（isReview=false，到期词不出现）且新词落用户难度带 [vocab, vocab+12]；
        ③ mode=mixed（默认/非法 mode 回退）count=12 → 新 4 复习 8（3:7）；
        ④ 复习不足新词补位凑满（2 到期 → 2 复习 + 10 新）；
        ⑤ 双侧都空 → 空数组。"""
import io
import json
import subprocess
import sys
import time
import urllib.request
import urllib.error

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5129"
DB = "nextword_qa_t05052"

DIFF_MAP = {"Basic": 25, "Intermediate": 50, "Advanced": 75}


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
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    if out.returncode != 0:
        raise SystemExit(f"psql 失败: {out.stderr}")
    return out.stdout.strip()


def register(name):
    email = f"qa-t05052-{name}-{int(time.time()*1000)}@example.com"
    resp = call("POST", "/api/auth/register", body={
        "email": email, "password": "Passw0rd!234", "displayName": name})
    uid = psql(f"SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = '{email}'")
    return resp["token"], uid


def set_progress(uid, vocab=None, cefr="B1"):
    sets = ['"HasCompletedInitialAssessment" = true', f'"CefrDisplay" = \'{cefr}\'']
    if vocab is not None:
        sets.append(f'"VocabularyScore" = {vocab}')
    psql(f'UPDATE "UserProgress" SET {", ".join(sets)} WHERE "UserId" = \'{uid}\'')


def pick_words(n, exclude_ids=()):
    """从词表取 n 个词 id（排除已用）。"""
    excl = ""
    if exclude_ids:
        excl = "AND \"Id\" NOT IN ('" + "','".join(exclude_ids) + "')"
    rows = psql(f'SELECT "Id" FROM "Words" WHERE 1=1 {excl} ORDER BY "Lemma" LIMIT {n}')
    return [line for line in rows.splitlines() if line]


def add_rel(uid, word_id, due):
    due_sql = "now() - interval '1 hour'" if due else "now() + interval '3 days'"
    psql(
        f'INSERT INTO "UserWordRelationships" ("Id","UserId","WordId","LifecycleStage","MasteryScore","RepeatCount",'
        f'"IntervalDays","EaseFactor","NextReviewDue","EstimatedKnownRate","TimesLearned","TimesCorrect","Source","IsFavorite")'
        f" VALUES (gen_random_uuid(),'{uid}','{word_id}','Recognized',25,1,1,2.5,{due_sql},0.5,1,1,'Review',false)")


def intrinsic_of(word_id):
    row = psql(
        f'SELECT COALESCE((SELECT "IntrinsicScore" FROM "WordDifficultyAnnotations"'
        f' WHERE "WordId" = \'{word_id}\' AND "IsCurrent" LIMIT 1)::text,'
        f' (SELECT "DifficultyLevel" FROM "Words" WHERE "Id" = \'{word_id}\'))')
    if row in DIFF_MAP:
        return DIFF_MAP[row]
    return int(row)


def ids_of(queue):
    return [item["id"] for item in queue]


# ── T-050：每日词默认 15 / 可选量 / 字段口径 ─────────────
token_a, uid_a = register("daily")
set_progress(uid_a, vocab=50, cefr="B1")

daily_default = call("GET", "/api/words/daily", token_a)
assert len(daily_default) == 15, f"T-050 默认词量应 15: {len(daily_default)}"
for item in daily_default:
    assert "stage" in item and "quizMode" in item and "isExposure" in item and "fromPlan" in item, \
        f"T-050 每日词响应缺 stage/quizMode/isExposure/fromPlan 字段: {sorted(item.keys())}"
    assert item["quizMode"] in ("recognition", "recall"), f"quizMode 非法: {item['quizMode']}"
print(f"T-050① /api/words/daily 默认 15 词 ✓（stage/quizMode/isExposure/fromPlan 字段齐全）")

d10 = call("GET", "/api/words/daily?count=10", token_a)
d20 = call("GET", "/api/words/daily?count=20", token_a)
assert len(d10) == 10 and len(d20) == 20, f"T-050 count=10/20 未生效: {len(d10)}/{len(d20)}"
print("T-050① count=10/20 可选生效 ✓（上限 20）")
print("T-050② 12+3 接触词、回忆考察位 ≥40% 口径由 LearningPlanTests 守护（已审查：DailyWordCount=15、MaxExposureRatio=0.2→3、RecallExamQuotaRatio=0.4 未动）")

# ── T-052 造数用户：8 到期 + 2 未到期，带 [50,62] ───────
token_b, uid_b = register("spell")
set_progress(uid_b, vocab=50, cefr="B1")
due_ids = pick_words(8)
future_ids = pick_words(2, exclude_ids=due_ids)
for wid in due_ids:
    add_rel(uid_b, wid, due=True)
for wid in future_ids:
    add_rel(uid_b, wid, due=False)

# T-051：默认 count=12（mixed 默认：8 复习占满剩余名额 → 4 新 + 8 复习）
q_default = call("GET", "/api/spelling/queue", token_b)
assert len(q_default) == 12, f"T-051 默认题量应 12: {len(q_default)}"
n_rev = sum(1 for item in q_default if item["isReview"])
n_new = sum(1 for item in q_default if not item["isReview"])
assert n_rev == 8 and n_new == 4, f"T-052 mixed count=12 应 新4/复习8: 新{n_new}/复习{n_rev}"
assert set(ids_of(q_default)) & set(future_ids) == set(), "未到期关系不应进队列"
print(f"T-051① /api/spelling/queue 默认 count=12 ✓；T-052③ mixed 默认新:旧 = {n_new}:{n_rev}（3:7，count=12→新4复习8）✓")

# T-052：mode=review 只到期复习词
q_review = call("GET", "/api/spelling/queue?mode=review&count=12", token_b)
assert len(q_review) == 8, f"review 模式应只返回 8 个到期词: {len(q_review)}"
assert all(item["isReview"] for item in q_review), "review 模式出现非复习词"
assert set(ids_of(q_review)) == set(due_ids), "review 模式集合与到期关系不一致"
print("T-052① mode=review 只返回到期复习词（8/8，isReview 全 true，未到期/新词不出现）✓")

# T-052：mode=new 只新词且落带内 [50,62]
q_new = call("GET", "/api/spelling/queue?mode=new&count=12", token_b)
assert len(q_new) == 12, f"new 模式应补满 12: {len(q_new)}"
assert all(not item["isReview"] for item in q_new), "new 模式出现复习词"
assert set(ids_of(q_new)) & set(due_ids + future_ids) == set(), "new 模式混入已学词"
band_bad = [(item["lemma"], intrinsic_of(item["id"])) for item in q_new
            if not (50 <= intrinsic_of(item["id"]) <= 62)]
assert not band_bad, f"new 模式新词出带 [50,62]: {band_bad}"
print(f"T-052② mode=new 只返回新词（12 个 isReview 全 false）且全部落难度带 [50,62] ✓")

# T-052：非法 mode 回退 mixed
q_bogus = call("GET", "/api/spelling/queue?mode=bogus&count=12", token_b)
n_rev_b = sum(1 for item in q_bogus if item["isReview"])
assert len(q_bogus) == 12 and n_rev_b == 8, f"非法 mode 应回退 mixed（新4复习8）: 总长{len(q_bogus)} 复习{n_rev_b}"
print("T-052③ 非法 mode=bogus 回退 mixed ✓")

# ── T-052 补位：只有 2 个到期词 → 新词补位凑满 12 ──────
token_c, uid_c = register("backfill")
set_progress(uid_c, vocab=50, cefr="B1")
due2 = pick_words(2, exclude_ids=due_ids + future_ids)
for wid in due2:
    add_rel(uid_c, wid, due=True)
q_fill = call("GET", "/api/spelling/queue?mode=mixed&count=12", token_c)
n_rev_f = sum(1 for item in q_fill if item["isReview"])
n_new_f = len(q_fill) - n_rev_f
assert len(q_fill) == 12 and n_rev_f == 2 and n_new_f == 10, \
    f"补位失败：应 复习2+新10: {len(q_fill)}/{n_rev_f}/{n_new_f}"
assert set(ids_of(q_fill)) & set(due2) == set(due2), "2 个到期词应全部出现"
band_bad = [(item["lemma"], intrinsic_of(item["id"])) for item in q_fill if not item["isReview"]
            and not (50 <= intrinsic_of(item["id"]) <= 62)]
assert not band_bad, f"补位新词出带: {band_bad}"
print("T-052④ 复习不足（2 个到期）→ 新词补位凑满 12（复习2+新10，新词仍带内）✓")

# ── T-052 双空：无关系 + 带内无词 → 空数组 ─────────────
# vocab=0 → 带 [0,12]；先确认全局词池该带无词（Basic 映射 25 起步）
gap = psql(
    'SELECT count(*) FROM "Words" w WHERE COALESCE('
    '(SELECT "IntrinsicScore" FROM "WordDifficultyAnnotations" a WHERE a."WordId" = w."Id" AND a."IsCurrent" LIMIT 1),'
    ' CASE w."DifficultyLevel" WHEN \'Basic\' THEN 25 WHEN \'Intermediate\' THEN 50 WHEN \'Advanced\' THEN 75 ELSE 40 END)'
    ' BETWEEN 0 AND 12')
assert gap == "0", f"带 [0,12] 存在 {gap} 个词，双空场景不可用"
token_d, uid_d = register("empty")
set_progress(uid_d, vocab=0, cefr="A1")
q_empty = call("GET", "/api/spelling/queue?mode=mixed&count=12", token_d)
assert q_empty == [], f"双侧都空应返回空数组: {len(q_empty)}"
print("T-052⑤ 无复习关系且带内无新词 → 返回空数组 ✓")

print("\nT-050/T-051/T-052 API 层实测全部通过。")
