# -*- coding: utf-8 -*-
"""T-014 词毕业四阶段生命周期 + T-013 僵尸任务回收 真实链路实测
（DashScope qwen-plus + 独立验证库 nextword_verify_t014，验完删库，不动 dev 库 nextword）。
流程：
  ① 背词认识模式连续正确 ×2（SM-2 成熟）→ recognized → recalled（掌握度 25→50）；
  ② 自评对比：Forgot 提交后掌握度与 UserProgress 四维 Score 不变（SM-2 interval 重置证明排程生效）；
  ③ 回忆模式（看义想词）答对 → prompted_use（掌握度 75，进产出候选池）；
  ④ Planner 真实触发 → 当日造句目标优先取候选池词；
  ⑤ /api/words/daily 返回阶段与考察模式字段；
  ⑥ 指定目标词造句（真实 LLM 评分）→ 不算自发（不毕业），A/B 档确认 PromptedUseConfirmedAt；
  ⑦ 自由表达（真实 LLM 评分达标）中自发使用 → spontaneous_use 毕业 + 留痕 FreeExpressionLog Id；
  ⑧ T-013：超时 Processing 任务回收重跑（RetryCount+1）；超上限（RetryCount=3）→ Failed 留痕。
前置：API 以 DashScope 真实 LLM + 连接串指向 nextword_verify_t014 运行在 localhost:5108。"""
import io
import json
import subprocess
import sys
import time
import urllib.request
import urllib.error

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"
DB = "nextword_verify_t014"


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
    email = f"verify-t014-{name}-{int(time.time())}@example.com"
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


def submit_word(token, word_id, answer, rating, mode):
    return call("POST", "/api/learning/submit", token, {
        "wordId": word_id, "answer": answer, "rating": rating,
        "responseTimeMs": 800, "mode": mode})


def rel_row(uid, word_id):
    return psql(
        f'SELECT "LifecycleStage" || \'|\' || "MasteryScore"::text || \'|\' || COALESCE("RepeatCount"::text, \'\')'
        f' || \'|\' || COALESCE("PromptedUseConfirmedAt"::text, \'\')'
        f' || \'|\' || COALESCE("GraduatedFreeExpressionLogId"::text, \'\')'
        f' FROM "UserWordRelationships" WHERE "UserId" = \'{uid}\' AND "WordId" = \'{word_id}\'')


token, uid = register("lifecycle")
psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid}\'')
print(f"用户: {uid[:8]}...")

# 选词：带内（A2）真实词，限可接不定式的动词（按语义自然度优先 plan/decide），便于造自然句
row = psql(
    'SELECT "Id" || \'|\' || "Lemma" || \'|\' || "Meanings" FROM "Words"'
    ' WHERE "Lemma" IN (\'plan\',\'decide\',\'hope\',\'try\',\'want\',\'learn\',\'prefer\',\'agree\')'
    ' ORDER BY CASE "Lemma" WHEN \'plan\' THEN 0 WHEN \'decide\' THEN 1 ELSE 2 END LIMIT 1')
word_id, lemma, meanings_json = row.split("|", 2)
meaning = json.loads(meanings_json)[0]
print(f"测试词: {lemma} ({meaning}) id={word_id[:8]}...")

# ① 认识模式：第 1 次连续正确 → 仍 recognized（未达 SM-2 成熟阈值）
r1 = submit_word(token, word_id, meaning, "Remembered", "recognition")
assert r1["isCorrect"], f"认识模式答义应判对: {r1}"
assert r1["stage"] == "recognized" and r1["masteryScore"] == 25, f"第 1 次不应推进: {r1}"
print(f"① 认识 ×1: stage={r1['stage']} mastery={r1['masteryScore']}（未达成熟阈值不推进）")

# 第 2 次连续正确（RepeatCount=2 达 SM-2 成熟）→ recalled
r2 = submit_word(token, word_id, meaning, "Remembered", "recognition")
assert r2["stage"] == "recalled" and r2["masteryScore"] == 50, f"第 2 次应进回忆阶段: {r2}"
print(f"① 认识 ×2: stage={r2['stage']} mastery={r2['masteryScore']}（SM-2 成熟 → recalled）")

# ② 自评对比：Forgot 只改 SM-2 排程，不改掌握度与 Score
scores_before = psql(f'SELECT COALESCE("ReadingScore"::text,\'x\') || \'|\' || COALESCE("SpellingScore"::text,\'x\')'
                     f' || \'|\' || COALESCE("VocabularyScore"::text,\'x\') || \'|\' || COALESCE("WritingScore"::text,\'x\')'
                     f' FROM "UserProgress" WHERE "UserId" = \'{uid}\'')
r3 = submit_word(token, word_id, "完全不对的答案", "Forgot", "recognition")
scores_after = psql(f'SELECT COALESCE("ReadingScore"::text,\'x\') || \'|\' || COALESCE("SpellingScore"::text,\'x\')'
                    f' || \'|\' || COALESCE("VocabularyScore"::text,\'x\') || \'|\' || COALESCE("WritingScore"::text,\'x\')'
                    f' FROM "UserProgress" WHERE "UserId" = \'{uid}\'')
assert r3["masteryScore"] == 50, f"Forgot 不应改掌握度: {r3}"
assert r3["stage"] == "recalled", f"Forgot 不应改阶段: {r3}"
assert r3["intervalDays"] == 1, f"Forgot 应重置 SM-2 interval: {r3}"
assert scores_before == scores_after, f"自评不应改 Score: {scores_before} → {scores_after}"
print(f"② Forgot 自评: mastery 50 不变、stage recalled 不变、Score 四维不变、SM-2 interval 重置为 1 ✓")

# ③ 回忆模式（看义想词）答对 → prompted_use（进产出候选池）
r4 = submit_word(token, word_id, lemma, "Remembered", "recall")
assert r4["isCorrect"], f"回忆模式答词应判对: {r4}"
assert r4["stage"] == "prompted_use" and r4["masteryScore"] == 75, f"回忆通过应进候选池: {r4}"
print(f"③ 回忆通过: stage={r4['stage']} mastery={r4['masteryScore']}（进产出候选池）")

# ④ Planner：候选池词优先编入当日造句目标
call("POST", "/api/planner/jobs", token, {})
current = wait_value(
    lambda: call("GET", "/api/planner/current", token),
    lambda data: data.get("active"), timeout=120, label="LearningPlan")
assert lemma in current["todaySentenceTargets"], f"候选池词未优先编排: {current['todaySentenceTargets']}"
print(f"④ Planner 当日造句目标: {current['todaySentenceTargets']}（候选池词 {lemma} 在列 ✓）")

# ⑤ 背词接口按阶段返回考察模式字段
daily = call("GET", "/api/words/daily?count=5", token)
assert daily and all("stage" in item and "quizMode" in item for item in daily), f"每日词缺阶段/模式字段: {daily[:1]}"
print(f"⑤ 每日词 {len(daily)} 个均带 stage/quizMode（首个: {daily[0]['stage']}/{daily[0]['quizMode']}）")

# ⑥ 指定目标词造句（真实 LLM 评分）：不算自发，不毕业
sentence = f"Every morning I {lemma} to read English aloud, because it helps me speak with more confidence."
rated = call("POST", "/api/sentences/rate", token, {
    "wordId": word_id, "targetWord": lemma, "userSentence": sentence,
    "scene": "life", "userLevel": "A2"})
stage_after_sentence = rel_row(uid, word_id).split("|")
assert stage_after_sentence[0] == "PromptedUse", f"指定目标词产出不应毕业: {stage_after_sentence}"
assert stage_after_sentence[4] == "", f"指定目标词产出不应留毕业痕: {stage_after_sentence}"
grade = rated["overallGrade"]
if grade in ("A", "B"):
    assert stage_after_sentence[3] != "", f"达标造句应确认 prompted use: {stage_after_sentence}"
print(f"⑥ 造句评分 {grade}（真实 LLM）: stage 仍 PromptedUse、无毕业痕（指定目标词不算自发 ✓）"
      f"{'、已确认 PromptedUseConfirmedAt' if grade in ('A','B') else '、未达标不确认'}")

# ⑦ 自由表达自发使用（真实 LLM 达标）→ 毕业留痕
# ⑦ 自由表达自发使用（真实 LLM 达标）→ 毕业留痕；qwen 偏严，备 2 段文本重试
free_texts = [
    (f"These days, I {lemma} to read English articles aloud every morning, because it makes me more confident. "
     f"Although it was difficult at first, I have kept the habit for a month, "
     f"and my friends say my pronunciation sounds much more natural now."),
    (f"Last weekend, I told my friend that I {lemma} to join the English corner at school, "
     f"because talking with others there has helped me a lot. "
     f"Although I still make mistakes, I am not afraid of speaking anymore, and that feels wonderful."),
]
free = None
for text in free_texts:
    candidate = call("POST", "/api/free-expression/rate", token, {"userText": text, "userLevel": "A2"})
    if candidate["overallGrade"] in ("A", "B"):
        free = candidate
        break
    print(f"  自由表达评分 {candidate['overallGrade']} 未达标，换下一段文本重试…")
assert free is not None, "两段自由表达文本均未达 A/B（真实 LLM 偏严），无法验证毕业"
final = rel_row(uid, word_id).split("|")
assert final[0] == "SpontaneousUse", f"自发使用应毕业: {final}"
assert final[4] == free["id"], f"毕业留痕应指向本次 FreeExpressionLog: {final} vs {free['id']}"
assert final[1] == "100", f"毕业掌握度应 100: {final}"
print(f"⑦ 自由表达评分 {free['overallGrade']}（真实 LLM）: 毕业 spontaneous_use、mastery=100、留痕 log={free['id'][:8]}... ✓")

# ⑧ T-013 僵尸任务回收
key1, key2 = f"t013-reclaim-{int(time.time())}", f"t013-exhaust-{int(time.time())}"
psql(f'INSERT INTO "BackgroundJobs" ("JobType","PayloadJson","Status","IdempotencyKey","CreatedAt","StartedAt","RetryCount")'
     f" VALUES ('EvaluationReport','{{}}','Processing','{key1}', now() - interval '40 minutes', now() - interval '30 minutes', 0),"
     f" ('EvaluationReport','{{}}','Processing','{key2}', now() - interval '40 minutes', now() - interval '30 minutes', 3)")
wait_value(
    lambda: psql(f'SELECT "RetryCount" FROM "BackgroundJobs" WHERE "IdempotencyKey" = \'{key1}\''),
    lambda v: v.isdigit() and int(v) >= 1, timeout=30, label="僵尸回收(重跑)")
exhaust = psql(f'SELECT "Status" || \'|\' || "RetryCount"::text || \'|\' || COALESCE("ErrorMessage",\'\')'
               f' FROM "BackgroundJobs" WHERE "IdempotencyKey" = \'{key2}\'')
status, retry, error = exhaust.split("|", 2)
assert status == "Failed" and int(retry) >= 4 and "僵尸" in error, f"超限应 Failed 留痕: {exhaust}"
print(f"⑧ T-013: 超时 Processing 回收重跑（RetryCount 0→1 后被 worker 重新捞起）✓；超限任务 Failed 留痕（{error}）✓")

print("\nT-014/T-013 真实链路实测全部通过。")
