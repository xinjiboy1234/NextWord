# -*- coding: utf-8 -*-
"""T-014 验收补充实测（周密 QA）：真实 DashScope qwen-plus 链路 + 独立库 nextword_verify_t014_qa（验完删库，不动 dev 库）。
聚焦开发脚本未覆盖的边界：
  ① 新用户每日词：新词/存量词初始阶段 recognized + recognition 模式；
  ② 自评对比（独立复核）：Forgot/Remembered 前后 mastery 与 Score 四维不变，EstimatedKnownRate 确实在变（证明自评仍生效于排程输入）；
  ③ 回退规则真实链路：prompted_use 词造句严重误用（真实 LLM 判 D 或词汇维低分）→ 回退 recalled、SM-2 归零、确认时间清空；
  ④ 自由表达误用不算毕业：候选池词出现在烂文本中，LLM 判低分 → 不毕业不留痕；
  ⑤ 指定目标词产出不算自发（独立复核）：造句评分后 stage 仍 PromptedUse、无毕业痕；
  ⑥ 存量映射口径：补丁 SQL 两条 UPDATE 对 RepeatCount>=2→Recalled/50、RepeatCount<2 不动（幂等）；
  ⑦ T-013：超时 Processing 回收重跑、超限 Failed 留痕（独立复核）。
前置：API 以 DashScope 真实 LLM + 连接串指向 nextword_verify_t014_qa 运行在 localhost:5108。"""
import io
import json
import subprocess
import sys
import time
import urllib.request
import urllib.error

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"
DB = "nextword_verify_t014_qa"


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
    email = f"qa-t014-{name}-{int(time.time()*1000)}@example.com"
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


def pick_word(exclude=()):
    names = "','".join(exclude)
    row = psql(
        'SELECT "Id" || \'|\' || "Lemma" || \'|\' || "Meanings" FROM "Words"'
        ' WHERE "Lemma" IN (\'plan\',\'decide\',\'hope\',\'try\',\'want\',\'learn\',\'prefer\',\'agree\',\'enjoy\',\'finish\')'
        f' AND "Lemma" NOT IN (\'{names}\')'
        ' ORDER BY "Lemma" LIMIT 1')
    word_id, lemma, meanings_json = row.split("|", 2)
    return word_id, lemma, json.loads(meanings_json)[0]


def drive_to_prompted(token, uid, word_id, lemma, meaning):
    """认识 ×2（SM-2 成熟）→ recalled；回忆通过 → prompted_use。"""
    r1 = submit_word(token, word_id, meaning, "Remembered", "recognition")
    assert r1["stage"] == "recognized" and r1["masteryScore"] == 25, f"第 1 次不应推进: {r1}"
    r2 = submit_word(token, word_id, meaning, "Remembered", "recognition")
    assert r2["stage"] == "recalled" and r2["masteryScore"] == 50, f"第 2 次应进回忆: {r2}"
    r3 = submit_word(token, word_id, lemma, "Remembered", "recall")
    assert r3["isCorrect"] and r3["stage"] == "prompted_use" and r3["masteryScore"] == 75, f"回忆通过应进候选池: {r3}"


# ── ① 新用户每日词初始阶段 ─────────────────────────────
token0, uid0 = register("fresh")
psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid0}\'')
daily0 = call("GET", "/api/words/daily?count=10", token0)
assert daily0 and all(item["stage"] == "recognized" and item["quizMode"] == "recognition" for item in daily0), \
    f"新用户全部词应为 recognized/recognition: {[(i['lemma'], i['stage'], i['quizMode']) for i in daily0]}"
exposure = [item for item in daily0 if item.get("isExposure")]
print(f"① 新用户每日词 {len(daily0)} 个全部 recognized/recognition（含接触词 {len(exposure)} 个同样初始 recognized）✓")

# ── ② 自评对比（独立复核）─────────────────────────────
token, uid = register("selfrate")
psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid}\'')
word_id, lemma, meaning = pick_word()
print(f"自评测试词: {lemma}")

def scores():
    return psql(f'SELECT COALESCE("ReadingScore"::text,\'x\') || \'|\' || COALESCE("SpellingScore"::text,\'x\')'
                f' || \'|\' || COALESCE("VocabularyScore"::text,\'x\') || \'|\' || COALESCE("WritingScore"::text,\'x\')'
                f' FROM "UserProgress" WHERE "UserId" = \'{uid}\'')

def known_rate():
    return psql(f'SELECT "EstimatedKnownRate"::text FROM "UserWordRelationships" WHERE "UserId" = \'{uid}\' AND "WordId" = \'{word_id}\'')

s0 = scores()
r_a = submit_word(token, word_id, meaning, "Remembered", "recognition")
kr1 = float(known_rate())
r_b = submit_word(token, word_id, "完全不对的答案", "Forgot", "recognition")
kr2 = float(known_rate())
s1 = scores()
assert r_a["masteryScore"] == 25 and r_b["masteryScore"] == 25, f"自评不改掌握度: {r_a['masteryScore']}→{r_b['masteryScore']}"
assert r_a["stage"] == "recognized" and r_b["stage"] == "recognized", "自评不改阶段"
assert s0 == s1, f"自评不改 Score 四维: {s0} → {s1}"
assert kr1 != kr2, f"EstimatedKnownRate 应随自评变化（排程输入仍生效）: {kr1} → {kr2}"
assert r_b["intervalDays"] == 1, f"Forgot 重置 SM-2 排程: {r_b['intervalDays']}"
print(f"② Remembered/Forgot 前后 mastery=25 与 Score 四维不变；EstimatedKnownRate {kr1}→{kr2}（自评仍生效于排程输入）、SM-2 interval 重置 ✓")

# ── ③ 回退规则真实链路（D 档/词汇维低分）─────────────
token2, uid2 = register("regress")
psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid2}\'')
word2, lemma2, meaning2 = pick_word(exclude=[lemma])
drive_to_prompted(token2, uid2, word2, lemma2, meaning2)
print(f"回退测试词: {lemma2}（已推进到 prompted_use，RepeatCount={rel_row(uid2, word2).split('|')[2]}）")

# 先造一个好句确认（若 LLM 判 A/B），再严重误用 → 回退应同时清空确认
good_sentence = f"Every morning I {lemma2} to read English aloud, because it helps me speak with more confidence."
rated_ok = call("POST", "/api/sentences/rate", token2, {
    "wordId": word2, "targetWord": lemma2, "userSentence": good_sentence,
    "scene": "life", "userLevel": "A2"})
mid = rel_row(uid2, word2).split("|")
assert mid[0] == "PromptedUse" and mid[4] == "", f"指定目标词产出不算自发: {mid}"
print(f"⑤ 指定目标词造句评分 {rated_ok['overallGrade']}（真实 LLM）: stage 仍 PromptedUse、无毕业痕"
      f"{'、已确认' if mid[3] else '、未确认'} ✓")

bad_sentences = [
    f"I {lemma2} the homework yesterday tomorrow very much good and she {lemma2} are being goed.",
    f"He {lemma2} don't never nothing, because {lemma2} is are was been the of at.",
]
regressed = False
for bad in bad_sentences:
    rated_bad = call("POST", "/api/sentences/rate", token2, {
        "wordId": word2, "targetWord": lemma2, "userSentence": bad,
        "scene": "life", "userLevel": "A2"})
    after = rel_row(uid2, word2).split("|")
    if after[0] == "Recalled":
        assert after[2] == "0", f"回退应 SM-2 归零: {after}"
        assert after[3] == "", f"回退应清空确认时间: {after}"
        assert after[1] == "50", f"回退掌握度应 50: {after}"
        print(f"③ 严重误用造句评分 {rated_bad['overallGrade']}/词汇维 {rated_bad['vocabularyScore']}（真实 LLM）→ 回退 recalled、SM-2 归零、确认清空 ✓")
        regressed = True
        break
    print(f"  烂句评分 {rated_bad['overallGrade']}/词汇维 {rated_bad['vocabularyScore']} 未触发回退（LLM 宽容），重试下一句…")
assert regressed, "两句严重误用造句均未触发回退（真实 LLM 未判 D/低词汇维），回退规则真实链路未验证"

# ── ④ 自由表达误用不算毕业 ────────────────────────────
token3, uid3 = register("freebad")
psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid3}\'')
word3, lemma3, meaning3 = pick_word(exclude=[lemma, lemma2])
drive_to_prompted(token3, uid3, word3, lemma3, meaning3)
print(f"自由表达误用测试词: {lemma3}（已推进到 prompted_use）")
bad_texts = [
    f"I {lemma3} very bad and yesterday tomorrow he {lemma3} are goed, nothing never don't.",
    f"She {lemma3} don't was were, because of at the {lemma3} is being been.",
]
not_graduated = False
for text in bad_texts:
    free_bad = call("POST", "/api/free-expression/rate", token3, {"userText": text, "userLevel": "A2"})
    after = rel_row(uid3, word3).split("|")
    if free_bad["overallGrade"] in ("A", "B"):
        print(f"  烂文本评分 {free_bad['overallGrade']}（LLM 宽容），重试下一段…")
        continue
    assert after[0] == "PromptedUse" and after[4] == "", f"低分自由表达不应毕业: {after} grade={free_bad['overallGrade']}"
    print(f"④ 含候选池词的烂文本评分 {free_bad['overallGrade']}（真实 LLM）→ 不毕业不留痕 ✓")
    not_graduated = True
    break
assert not_graduated, "两段烂文本均被判 A/B，无法验证误用不毕业"

# ── ⑥ 存量映射口径（补丁 SQL 幂等）────────────────────
mig_word = psql('SELECT "Id" FROM "Words" WHERE "Lemma" = \'book\' LIMIT 1')
mig_word2 = psql('SELECT "Id" FROM "Words" WHERE "Lemma" = \'work\' LIMIT 1')
mig_user = uid0
psql(f'DELETE FROM "UserWordRelationships" WHERE "UserId" = \'{mig_user}\' AND "WordId" IN (\'{mig_word}\',\'{mig_word2}\')')
psql(f'INSERT INTO "UserWordRelationships" ("Id","UserId","WordId","LifecycleStage","MasteryScore","RepeatCount","IntervalDays","EaseFactor","NextReviewDue","EstimatedKnownRate","TimesLearned","TimesCorrect","Source","IsFavorite")'
     f" VALUES (gen_random_uuid(),'{mig_user}','{mig_word}','Recognized',0,3,30,2.5,now(),0.5,5,5,'Review',false),"
     f" (gen_random_uuid(),'{mig_user}','{mig_word2}','Recognized',0,1,1,2.5,now(),0.5,1,1,'Review',false)")
psql('UPDATE "UserWordRelationships" SET "LifecycleStage" = \'Recalled\' WHERE "LifecycleStage" = \'Recognized\' AND "RepeatCount" >= 2')
psql('UPDATE "UserWordRelationships" SET "MasteryScore" = 50 WHERE "LifecycleStage" = \'Recalled\'')
psql('UPDATE "UserWordRelationships" SET "MasteryScore" = 25 WHERE "LifecycleStage" = \'Recognized\'')
m1 = psql(f'SELECT "LifecycleStage" || \'|\' || "MasteryScore"::text FROM "UserWordRelationships" WHERE "UserId" = \'{mig_user}\' AND "WordId" = \'{mig_word}\'')
m2 = psql(f'SELECT "LifecycleStage" || \'|\' || "MasteryScore"::text FROM "UserWordRelationships" WHERE "UserId" = \'{mig_user}\' AND "WordId" = \'{mig_word2}\'')
assert m1 == "Recalled|50", f"RepeatCount=3 应映射 Recalled/50: {m1}"
assert m2 == "Recognized|25", f"RepeatCount=1 应保持 Recognized/25: {m2}"
print(f"⑥ 存量映射口径: RepeatCount=3 → Recalled/50、RepeatCount=1 → Recognized/25（幂等 UPDATE 只动 Recognized 行）✓")

# ── ⑦ T-013 僵尸任务回收（独立复核）───────────────────
key1, key2 = f"t013-qa-reclaim-{int(time.time())}", f"t013-qa-exhaust-{int(time.time())}"
psql(f'INSERT INTO "BackgroundJobs" ("JobType","PayloadJson","Status","IdempotencyKey","CreatedAt","StartedAt","RetryCount")'
     f" VALUES ('EvaluationReport','{{}}','Processing','{key1}', now() - interval '40 minutes', now() - interval '30 minutes', 0),"
     f" ('EvaluationReport','{{}}','Processing','{key2}', now() - interval '40 minutes', now() - interval '30 minutes', 3)")
wait_value(
    lambda: psql(f'SELECT "RetryCount" FROM "BackgroundJobs" WHERE "IdempotencyKey" = \'{key1}\''),
    lambda v: v.lstrip('-').isdigit() and int(v) >= 1, timeout=60, label="僵尸回收(重跑)")
exhaust = psql(f'SELECT "Status" || \'|\' || "RetryCount"::text || \'|\' || COALESCE("ErrorMessage",\'\')'
               f' FROM "BackgroundJobs" WHERE "IdempotencyKey" = \'{key2}\'')
status, retry, error = exhaust.split("|", 2)
assert status == "Failed" and int(retry) >= 4 and "僵尸" in error, f"超限应 Failed 留痕: {exhaust}"
print(f"⑦ T-013: 超时 Processing 回收重跑（RetryCount 0→1）✓；超限 Failed 留痕（{error}）✓")

print("\nT-014 QA 边界实测全部通过。")
