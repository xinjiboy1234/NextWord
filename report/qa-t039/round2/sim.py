#!/usr/bin/env python3
"""NextWord 菜鸟用户「小菜」30 天使用仿真。

- 真实 API: http://localhost:5108（Development + DashScope qwen-plus）
- 独立仿真库: nextword_sim（docker exec nextword-postgres-1 psql）
- 时间穿越：每仿真日结束后把业务表时间戳整体回拨 1 天（见 TIME_TRAVEL_STATEMENTS）
- 断点续跑：data/checkpoint.json 记录下一个待跑仿真日
- 产物：data/day-log.jsonl / data/anomalies.log / data/run.log / data/final-state.json

用法：
  python sim.py                # 从断点跑满 30 天
  python sim.py --until 1      # 只跑到仿真日 1（冒烟用）
"""
import argparse
import json
import os
import random
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
from datetime import date, datetime, timedelta
from pathlib import Path

API_BASE = os.environ.get("NEXTWORD_API_BASE", "http://localhost:5190")
DASHSCOPE_URL = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"
DASHSCOPE_MODEL = "qwen-plus"
DB_CONTAINER = "nextword-postgres-1"
DB_NAME = "nextword_qa_t039b"
DB_USER = "nextword"

EMAIL = "xiaocai.sim@example.com"
PASSWORD = "Xiaocai@2026"
DISPLAY_NAME = "小菜"

TOTAL_DAYS = 30
ACTIVITY_RATE = 0.85
RECOGNITION_CORRECT = 0.80
RECALL_CORRECT = 0.55
SPELLING_CORRECT = 0.65
ASSESS_VOCAB_CORRECT = 0.75   # 词义选择目标正确率
ASSESS_READING_CORRECT = 0.70  # 阅读题目标正确率
CHALLENGE_DAYS = {7, 14, 21, 28}        # 1-indexed 仿真日
INSIGHT_DAYS = {10, 17, 24}             # 第 10 天起每 7 天
FREE_EXPR_TOPICS = ["吃饭", "上班", "天气", "周末", "购物", "运动", "家人", "通勤"]

ROOT = Path(__file__).resolve().parent
DATA_DIR = ROOT / "data"
DAY_LOG = DATA_DIR / "day-log.jsonl"
ANOMALIES = DATA_DIR / "anomalies.log"
CHECKPOINT = DATA_DIR / "checkpoint.json"
FINAL_STATE = DATA_DIR / "final-state.json"
TIME_TRAVEL_SQL_FILE = ROOT / "time-travel.sql"

# ── 时间穿越 SQL：逐个表列明确列出，不同步 BackgroundJobs / ProfileScoreSnapshots ──
TIME_TRAVEL_STATEMENTS = [
    'UPDATE "UserWordRelationships" SET "NextReviewDue" = "NextReviewDue" - interval \'1 day\', '
    '"LastReviewDate" = "LastReviewDate" - interval \'1 day\', '
    '"StageUpdatedAt" = "StageUpdatedAt" - interval \'1 day\', '
    '"PromptedUseConfirmedAt" = "PromptedUseConfirmedAt" - interval \'1 day\', '
    '"PersonalUpdatedAt" = "PersonalUpdatedAt" - interval \'1 day\'',
    'UPDATE "WordLearningLogs" SET "Timestamp" = "Timestamp" - interval \'1 day\'',
    'UPDATE "SentenceLogs" SET "Timestamp" = "Timestamp" - interval \'1 day\'',
    'UPDATE "FreeExpressionLogs" SET "Timestamp" = "Timestamp" - interval \'1 day\'',
    'UPDATE "SpellingLogs" SET "Timestamp" = "Timestamp" - interval \'1 day\'',
    'UPDATE "ReadingLogs" SET "StartTime" = "StartTime" - interval \'1 day\', '
    '"EndTime" = "EndTime" - interval \'1 day\', "Timestamp" = "Timestamp" - interval \'1 day\'',
    'UPDATE "ArticleComments" SET "Timestamp" = "Timestamp" - interval \'1 day\'',
    'UPDATE "LearningPlans" SET "StartDate" = "StartDate" - 1, "CreatedAt" = "CreatedAt" - interval \'1 day\'',
    'UPDATE "Assessments" SET "StartAt" = "StartAt" - interval \'1 day\', "EndAt" = "EndAt" - interval \'1 day\'',
    'UPDATE "AssessmentRecords" SET "Timestamp" = "Timestamp" - interval \'1 day\'',
    'UPDATE "ChallengeRecords" SET "Timestamp" = "Timestamp" - interval \'1 day\'',
    'UPDATE "ChallengeSessions" SET "CreatedAt" = "CreatedAt" - interval \'1 day\', '
    '"ExpiresAt" = "ExpiresAt" - interval \'1 day\'',
    'UPDATE "LevelHistories" SET "Timestamp" = "Timestamp" - interval \'1 day\'',
    'UPDATE "BottleneckInsights" SET "CreatedAt" = "CreatedAt" - interval \'1 day\'',
    'UPDATE "WeaknessProfiles" SET "CreatedAt" = "CreatedAt" - interval \'1 day\'',
    'UPDATE "EvaluationReports" SET "CreatedAt" = "CreatedAt" - interval \'1 day\'',
    'UPDATE "LearningEvents" SET "OccurredAt" = "OccurredAt" - interval \'1 day\'',
    'UPDATE "UserProgress" SET "LastStudyDate" = "LastStudyDate" - 1, '
    '"LevelStartDate" = "LevelStartDate" - 1, "ScoresUpdatedAt" = "ScoresUpdatedAt" - interval \'1 day\'',
    'UPDATE "Users" SET "CreatedAt" = "CreatedAt" - interval \'1 day\'',
    'UPDATE "UserFeedbacks" SET "CreatedAt" = "CreatedAt" - interval \'1 day\'',
    'UPDATE "UserWordExcludes" SET "CreatedAt" = "CreatedAt" - interval \'1 day\'',
]

WEEKDAYS_ZH = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"]


def log_run(msg):
    print(f"[{datetime.now().strftime('%H:%M:%S')}] {msg}", flush=True)


def log_anomaly(kind, detail):
    entry = {"ts": datetime.now().isoformat(timespec="seconds"), "kind": kind, "detail": detail}
    with ANOMALIES.open("a", encoding="utf-8") as fh:
        fh.write(json.dumps(entry, ensure_ascii=False) + "\n")
    log_run(f"ANOMALY [{kind}] {str(detail)[:200]}")


# ── HTTP ──────────────────────────────────────────────────────────────────────
class ApiClient:
    def __init__(self):
        self.token = None

    def request(self, method, path, body=None, timeout=180, _retried_auth=False):
        url = API_BASE + path
        payload = json.dumps(body).encode() if body is not None else None
        last_err = None
        for attempt in range(3):
            req = urllib.request.Request(url, data=payload, method=method)
            req.add_header("Content-Type", "application/json")
            if self.token:
                req.add_header("Authorization", f"Bearer {self.token}")
            try:
                with urllib.request.urlopen(req, timeout=timeout) as resp:
                    text = resp.read().decode("utf-8")
                    return resp.status, json.loads(text) if text else {}
            except urllib.error.HTTPError as exc:
                text = exc.read().decode("utf-8", errors="replace")[:500]
                if exc.code == 401 and not _retried_auth and self.token:
                    log_anomaly("http-401", f"{method} {path} → 401，重新登录后重试")
                    self.login()
                    return self.request(method, path, body, timeout, _retried_auth=True)
                if 400 <= exc.code < 500:
                    # 4xx 不重试，属于业务错误
                    return exc.code, {"_error": text, "_status": exc.code}
                last_err = f"HTTP {exc.code}: {text}"
            except (urllib.error.URLError, TimeoutError, ConnectionError, OSError) as exc:
                last_err = f"{type(exc).__name__}: {exc}"
            wait = 2 ** attempt * 2
            log_run(f"  retry {attempt + 1}/3 {method} {path}: {last_err}（{wait}s 后重试）")
            time.sleep(wait)
        log_anomaly("http-fail", f"{method} {path} 3 次重试均失败: {last_err}")
        raise RuntimeError(f"API call failed: {method} {path}: {last_err}")

    def get(self, path, **kw):
        return self.request("GET", path, **kw)

    def post(self, path, body=None, **kw):
        return self.request("POST", path, body or {}, **kw)

    def login(self):
        status, data = self.post("/api/auth/login", {"email": EMAIL, "password": PASSWORD})
        if status != 200 or "token" not in data:
            raise RuntimeError(f"登录失败: {status} {data}")
        self.token = data["token"]
        return data["user"]

    def register_or_login(self):
        status, data = self.post("/api/auth/register", {
            "email": EMAIL, "password": PASSWORD, "displayName": DISPLAY_NAME})
        if status == 200 and "token" in data:
            self.token = data["token"]
            log_run(f"注册成功: {data['user']}")
            return data["user"], True
        log_run(f"注册返回 {status}（用户可能已存在），改为登录")
        return self.login(), False


# ── SQL ───────────────────────────────────────────────────────────────────────
def psql(sql, tuples_only=False, fatal=False):
    cmd = ["docker", "exec", DB_CONTAINER, "psql", "-U", DB_USER, "-d", DB_NAME,
           "-v", "ON_ERROR_STOP=1"]
    if tuples_only:
        cmd += ["-A", "-t", "-F", "|"]
    cmd += ["-c", sql]
    result = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
    if result.returncode != 0:
        log_anomaly("sql-fail", f"{sql[:120]}... → {result.stderr.strip()[:300]}")
        if fatal:
            raise RuntimeError(f"SQL 执行失败，中止仿真: {result.stderr.strip()[:300]}")
    return result.stdout.strip()


def time_travel():
    """每仿真日结束后整体回拨 1 天；失败即中止，不带病继续。"""
    for stmt in TIME_TRAVEL_STATEMENTS:
        psql(stmt, fatal=True)


def clean_idempotency(prefix):
    psql(f'DELETE FROM "BackgroundJobs" WHERE "IdempotencyKey" LIKE \'{prefix}%\'')


def insert_snapshot(user_id, sim_date, scores):
    scores_json = json.dumps({
        "vocabulary": scores.get("vocabulary"),
        "reading": scores.get("reading"),
        "writing": scores.get("writing"),
        "spelling": scores.get("spelling"),
        "overall": scores.get("overall"),
        "difficultyBucket": scores.get("difficultyBucket"),
        "cefrDisplay": scores.get("cefrDisplay"),
        "updatedAt": f"{sim_date.isoformat()}T00:00:00+00:00",
    }, ensure_ascii=False).replace("'", "''")
    psql(
        f'INSERT INTO "ProfileScoreSnapshots" ("UserId", "Date", "ScoresJson") '
        f"VALUES ('{user_id}', '{sim_date.isoformat()}', '{scores_json}') "
        f'ON CONFLICT ("UserId", "Date") DO UPDATE SET "ScoresJson" = EXCLUDED."ScoresJson"',
        fatal=True)


def lifecycle_stats(user_id):
    out = {}
    rows = psql(
        f'SELECT "LifecycleStage", count(*) FROM "UserWordRelationships" '
        f"WHERE \"UserId\" = '{user_id}' GROUP BY 1 ORDER BY 1", tuples_only=True)
    dist = {}
    for line in rows.splitlines():
        if "|" in line:
            stage, n = line.rsplit("|", 1)
            dist[stage] = int(n)
    out["lifecycleDist"] = dist
    due = psql(
        f'SELECT count(*) FROM "UserWordRelationships" '
        f"WHERE \"UserId\" = '{user_id}' AND \"NextReviewDue\" <= now()", tuples_only=True)
    out["dueReviews"] = int(due or 0)
    mastery = psql(
        f'SELECT "MasteryScore"::int, count(*) FROM "UserWordRelationships" '
        f"WHERE \"UserId\" = '{user_id}' GROUP BY 1 ORDER BY 1", tuples_only=True)
    out["masteryDist"] = {line.rsplit("|", 1)[0]: int(line.rsplit("|", 1)[1])
                          for line in mastery.splitlines() if "|" in line}
    out["totalRelationships"] = sum(dist.values())
    return out


# ── DashScope 菜鸟文案生成 ────────────────────────────────────────────────────
def dashscope(prompt, system=None, temperature=0.9, max_tokens=200):
    key = os.environ.get("DASHSCOPE_API_KEY")
    if not key:
        return None
    messages = []
    if system:
        messages.append({"role": "system", "content": system})
    messages.append({"role": "user", "content": prompt})
    body = json.dumps({
        "model": DASHSCOPE_MODEL, "messages": messages,
        "temperature": temperature, "max_tokens": max_tokens,
    }).encode()
    for attempt in range(3):
        req = urllib.request.Request(DASHSCOPE_URL, data=body, method="POST")
        req.add_header("Content-Type", "application/json")
        req.add_header("Authorization", f"Bearer {key}")
        try:
            with urllib.request.urlopen(req, timeout=60) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                return data["choices"][0]["message"]["content"].strip()
        except Exception as exc:
            log_run(f"  DashScope retry {attempt + 1}/3: {type(exc).__name__}")
            time.sleep(2 ** attempt * 2)
    log_anomaly("dashscope-fail", f"生成降级到模板句: {prompt[:80]}")
    return None


BEGINNER_SYSTEM = (
    "You are role-playing as a Chinese beginner English learner (middle-school level). "
    "Write like a real beginner: simple everyday words, short sentences, occasional grammar "
    "mistakes (missing articles, wrong verb forms, singular/plural errors) and Chinglish "
    "word order or collocations. Never use advanced vocabulary. Reply with only the English text."
)


def gen_beginner_sentence(target_word, extra_context=""):
    prompt = (f"Write one short English sentence (8-15 words) using the word \"{target_word}\". "
              f"The word must appear exactly. {extra_context}")
    text = dashscope(prompt, system=BEGINNER_SYSTEM)
    if text:
        # 只取第一行，去掉引号
        text = text.splitlines()[0].strip().strip('"')
        if target_word.lower() in text.lower():
            return text
    return f"I {target_word} every day."


def gen_scenario_answer(scenario_zh, prompt_text):
    prompt = (f"Situation: {scenario_zh}. Task: {prompt_text}. "
              "Write a short English answer (6-15 words) as the beginner.")
    text = dashscope(prompt, system=BEGINNER_SYSTEM)
    if text:
        return text.splitlines()[0].strip().strip('"')
    return "I am happy. I like it very much."


def gen_free_text(topic):
    prompt = (f"Write a short passage of 4-6 simple English sentences about daily life: {topic}. "
              "Use only simple everyday words. Keep it short.")
    text = dashscope(prompt, system=BEGINNER_SYSTEM, max_tokens=300)
    if text:
        return " ".join(line.strip() for line in text.splitlines() if line.strip())
    return ("I get up at seven. I eat breakfast with my family. "
            "Today is a good day. I feel very happy.")


def mcq_index(question, options, accuracy):
    """让 qwen 作答，再按目标正确率随机改错，模拟菜鸟识别水平。"""
    opts = "\n".join(f"{i}. {o}" for i, o in enumerate(options))
    text = dashscope(f"{question}\n{opts}\nReply with only the option number.",
                     system="You are an English teacher. Answer the multiple choice question.",
                     temperature=0.1, max_tokens=10)
    correct = None
    if text:
        m = re.search(r"\d+", text)
        if m and int(m.group()) < len(options):
            correct = int(m.group())
    if correct is None:
        return random.randrange(len(options))
    if random.random() < accuracy or len(options) < 2:
        return correct
    wrong = [i for i in range(len(options)) if i != correct]
    return random.choice(wrong)


def typo_of(word):
    """典型菜鸟拼写错误：漏字母 / 相邻字母换序 / 双写漏一个。"""
    if len(word) < 4:
        return word[:-1] if len(word) > 2 else word
    mode = random.choice(["drop", "swap", "drop_double"])
    if mode == "swap":
        i = random.randrange(len(word) - 1)
        return word[:i] + word[i + 1] + word[i] + word[i + 2:]
    if mode == "drop_double":
        for i in range(len(word) - 1):
            if word[i] == word[i + 1]:
                return word[:i] + word[i + 1:]
        return typo_of_drop(word)
    return typo_of_drop(word)


def typo_of_drop(word):
    i = random.randrange(1, len(word))
    return word[:i] + word[i + 1:]


# ── 仿真动作 ──────────────────────────────────────────────────────────────────
def do_assessment(api, rng):
    """首次测评：产出题 DashScope 菜鸟作答，识别题按目标正确率作答。"""
    _, start = api.post("/api/assessment/initial/start", {"userId": None})
    aid = start["assessmentId"]
    log_run(f"测评开始: {aid}")
    final = None
    for _ in range(5):  # 最多 3 块，留余量
        _, nb = api.get(f"/api/assessment/{aid}/next-block")
        if nb.get("converged"):
            final = nb.get("final")
            break
        block = nb["block"]
        answers = []
        for prod in block.get("production", []):
            if prod.get("kind") == "scenario":
                text = gen_scenario_answer(prod.get("scenarioZh") or "日常生活",
                                           prod.get("prompt") or "")
            else:
                text = gen_beginner_sentence(prod.get("targetWord") or "like",
                                             prod.get("prompt") or "")
            answers.append({"id": prod["id"], "text": text, "selectedIndex": None, "lookupCount": None})
        for vocab in block.get("vocabulary", []):
            idx = mcq_index(f"What is the Chinese meaning of \"{vocab['word']}\"?",
                            vocab["options"], ASSESS_VOCAB_CORRECT)
            answers.append({"id": vocab["id"], "text": None, "selectedIndex": idx, "lookupCount": None})
        reading = block.get("reading")
        if reading:
            idx = mcq_index(
                f"Read: {reading.get('content', '')[:800]}\nQuestion: {reading['question']}",
                reading["options"], ASSESS_READING_CORRECT)
            answers.append({"id": reading["id"], "text": None, "selectedIndex": idx,
                            "lookupCount": rng.randint(1, 4)})
        log_run(f"  提交块 {block['blockIndex']}（band={block.get('band')}，{len(answers)} 题，LLM 评分中…）")
        _, res = api.post(f"/api/assessment/{aid}/blocks/{block['blockIndex']}/submit",
                          {"answers": answers}, timeout=600)
        if res.get("converged"):
            final = res.get("final")
            break
    if not final:
        log_anomaly("assessment", "测评 5 块仍未收敛，异常")
        return None
    log_run(f"测评定级: {final.get('overallLevel')} 表达力={final.get('expressionScore')} "
            f"词汇参考={final.get('vocabularyReferenceScore')} 阅读参考={final.get('readingReferenceScore')}")
    return {"assessmentId": aid, "final": final}


def wait_for_plan(api, timeout_s=420):
    """等评估报告任务（画像 LLM + Planner）处理完，拿到当日有效计划。"""
    deadline = time.time() + timeout_s
    while time.time() < deadline:
        _, cur = api.get("/api/planner/current")
        if cur.get("active"):
            return cur
        time.sleep(5)
    log_anomaly("planner", "等待计划生成超时")
    return None


def ensure_plan(api, day_log):
    """计划过期则清理幂等键并触发新计划。"""
    _, cur = api.get("/api/planner/current")
    if cur.get("active"):
        return cur
    log_run("  无有效计划，清理 planner 幂等键并触发新计划")
    clean_idempotency("planner:")
    api.post("/api/planner/jobs")
    plan = wait_for_plan(api, timeout_s=180)
    if plan is None:
        log_anomaly("planner", "触发后仍无有效计划")
    return plan


def plan_summary(plan):
    if not plan or not plan.get("active"):
        return {"active": False}
    return {
        "active": True,
        "startDate": plan.get("startDate"),
        "dayIndex": plan.get("dayIndex"),
        "focusScenarios": plan.get("focusScenarios"),
        "sourceBadge": "个性化" if plan.get("sourceFindingIds") else "探索期",
        "todayWordCount": plan.get("todayWordCount"),
        "todayExposureCount": plan.get("todayExposureCount"),
        "todaySentenceTargets": plan.get("todaySentenceTargets"),
    }


def do_learning(api, rng, day_log):
    """每日 10 词：按 quizMode 作答 + 自评一致。"""
    _, words = api.get("/api/words/daily?count=10")
    if not isinstance(words, list) or not words:
        log_anomaly("daily-words", f"每日词为空或形状异常: {str(words)[:200]}")
        day_log["learning"] = {"total": 0}
        return
    total, correct = 0, 0
    modes = {}
    for item in words:
        mode = item.get("quizMode") or "recognition"
        modes[mode] = modes.get(mode, 0) + 1
        if mode == "recall":
            ok = rng.random() < RECALL_CORRECT
            answer = item["lemma"] if ok else typo_of(item["lemma"])
        else:
            ok = rng.random() < RECOGNITION_CORRECT
            meanings = item.get("meanings") or ["?"]
            answer = meanings[0] if ok else "不知道"
        rating = "Remembered" if ok else "Forgot"
        status, res = api.post("/api/learning/submit", {
            "userId": None, "wordId": item["id"], "answer": answer,
            "rating": rating, "responseTimeMs": rng.randint(2000, 9000), "mode": mode})
        if status != 200:
            log_anomaly("learning-submit", f"{item.get('lemma')} → {status} {str(res)[:200]}")
            continue
        if res.get("isCorrect") != ok:
            log_anomaly("learning-judge", f"{item.get('lemma')} 预期{'对' if ok else '错'}"
                                          f"实判{'对' if res.get('isCorrect') else '错'} answer={answer!r}")
        total += 1
        correct += 1 if res.get("isCorrect") else 0
    day_log["learning"] = {"total": total, "correct": correct, "modes": modes,
                           "fromPlan": any(w.get("fromPlan") for w in words)}


def do_spelling(api, rng, day_log):
    _, queue = api.get("/api/spelling/queue")
    if not isinstance(queue, list) or not queue:
        day_log["spelling"] = {"total": 0}
        return
    total, correct = 0, 0
    for item in queue[:5]:
        word = item["lemma"]
        ok = rng.random() < SPELLING_CORRECT
        spelling = word if ok else typo_of(word)
        status, res = api.post("/api/spelling/submit", {
            "userId": None, "wordId": item["id"], "userSpelling": spelling,
            "attempts": 1 if ok else 2})
        if status != 200:
            log_anomaly("spelling-submit", f"{word} → {status} {str(res)[:200]}")
            continue
        total += 1
        correct += 1 if res.get("isCorrect") else 0
    day_log["spelling"] = {"total": total, "correct": correct, "queueSize": len(queue)}


def do_sentences(api, rng, user_level, day_log):
    _, prompts = api.get("/api/sentences/prompts?count=5")
    if not isinstance(prompts, list) or not prompts:
        log_anomaly("sentence-prompts", "造句出题为空")
        day_log["sentences"] = {"total": 0}
        return
    results = []
    for prompt in prompts[:3]:
        target = prompt.get("targetWord") or ""
        sentence = gen_beginner_sentence(target, prompt.get("content") or "")
        status, res = api.post("/api/sentences/rate", {
            "userId": None, "wordId": prompt.get("wordId"), "targetWord": target,
            "userSentence": sentence, "scene": prompt.get("scene"), "userLevel": user_level},
            timeout=300)
        if status != 200:
            log_anomaly("sentence-rate", f"{target} → {status} {str(res)[:200]}")
            continue
        grade = res.get("overallGrade")
        if grade == "A":
            log_anomaly("grading", f"菜鸟句拿 A: {sentence!r} target={target}")
        dims = [res.get("grammarScore"), res.get("naturalScore"),
                res.get("vocabularyScore"), res.get("relevanceScore")]
        if all(d == 5 for d in dims):
            log_anomaly("grading", f"菜鸟句四维满分: {sentence!r}")
        results.append({"target": target, "sentence": sentence, "grade": grade,
                        "grammar": res.get("grammarScore"), "natural": res.get("naturalScore"),
                        "vocabulary": res.get("vocabularyScore"), "relevance": res.get("relevanceScore"),
                        "revision": (res.get("aiRevision") or "")[:120],
                        "fromPlan": prompt.get("fromPlan")})
    day_log["sentences"] = {"total": len(results), "samples": results[:2],
                            "grades": [r["grade"] for r in results]}


def do_free_expression(api, rng, user_level, day_log):
    topic = FREE_EXPR_TOPICS[rng.randrange(len(FREE_EXPR_TOPICS))]
    text = gen_free_text(topic)
    status, res = api.post("/api/free-expression/rate",
                           {"userId": None, "userText": text, "userLevel": user_level}, timeout=300)
    if status != 200:
        log_anomaly("free-expression", f"{status} {str(res)[:200]}")
        return
    if res.get("overallGrade") == "A":
        log_anomaly("grading", f"菜鸟自由表达拿 A: {text[:80]!r}")
    day_log["freeExpression"] = {"topic": topic, "text": text[:150],
                                 "grade": res.get("overallGrade"), "aiScore": res.get("aiScore")}


def do_reading(api, rng, day_log):
    _, rec = api.get("/api/articles/recommended")
    articles = rec.get("articles") or []
    if not articles:
        day_log["reading"] = {"done": False}
        return
    article = articles[rng.randrange(len(articles))]
    _, detail = api.get(f"/api/articles/{article['id']}")
    content = detail.get("content") or ""
    status, log = api.post(f"/api/articles/{article['id']}/reading/start", {"userId": None})
    if status != 200:
        log_anomaly("reading-start", f"{status} {str(log)[:200]}")
        return
    log_id = log["id"]
    # 从正文挑 3-6 个稍长的词查义
    candidates = []
    for m in re.finditer(r"[A-Za-z]{7,}", content):
        w = m.group().lower()
        if w not in candidates:
            candidates.append(w)
    rng.shuffle(candidates)
    lookups = candidates[:rng.randint(3, 6)]
    sentences = re.split(r"(?<=[.!?])\s+", content)
    looked = 0
    for w in lookups:
        ctx = next((s for s in sentences if re.search(rf"\b{re.escape(w)}\b", s, re.I)), content[:200])
        status, _ = api.post("/api/reading/lookup", {
            "userId": None, "word": w, "sentence": ctx[:300], "articleId": article["id"]}, timeout=120)
        if status == 200:
            looked += 1
    status, fin = api.post(f"/api/reading-logs/{log_id}/finish",
                           {"lookupCount": looked, "commentsCount": 0})
    if status != 200:
        log_anomaly("reading-finish", f"{status} {str(fin)[:200]}")
    day_log["reading"] = {"done": True, "title": article.get("title"),
                          "fromPlan": rec.get("fromPlan"), "lookups": looked,
                          "durationSeconds": fin.get("durationSeconds")}


def do_challenge(api, rng, user_level, day_log):
    status, start = api.post("/api/challenge/start",
                             {"userId": None, "confirmationChallenge": False})
    if status != 200:
        log_anomaly("challenge-start", f"{status} {str(start)[:200]}")
        return
    pack = start["pack"]
    vocab_answers = [mcq_index(f"What is the Chinese meaning of \"{q['word']}\"?",
                               q["options"], ASSESS_VOCAB_CORRECT)
                     for q in pack.get("vocabulary", [])]
    sent = pack.get("sentence") or {}
    target = sent.get("word") or "like"
    sentence = gen_beginner_sentence(target)
    reading = pack.get("reading") or {}
    reading_idx = mcq_index(
        f"Read: {(reading.get('articleExcerpt') or '')[:800]}\nQuestion: {reading.get('question')}",
        reading.get("options") or ["?"], ASSESS_READING_CORRECT)
    lookup_count = rng.randint(1, 5)
    status, res = api.post("/api/challenge/submit", {
        "userId": None, "challengeSessionId": start["challengeSessionId"],
        "challengeType": "Daily", "vocabAnswers": vocab_answers,
        "sentenceAnswer": sentence, "targetWord": target,
        "scene": sent.get("scene") or "life", "sentenceWordId": sent.get("wordId"),
        "readingSelectedIndex": reading_idx, "lookupCount": lookup_count}, timeout=600)
    if status != 200:
        log_anomaly("challenge-submit", f"{status} {str(res)[:200]}")
        return
    day_log["challenge"] = {"passed": res.get("passed"), "totalScore": res.get("totalScore"),
                            "vocabularyScore": res.get("vocabularyScore"),
                            "writingScore": res.get("writingScore"),
                            "readingScore": res.get("readingScore"),
                            "attemptedLevel": pack.get("attemptedLevel")}
    log_run(f"  挑战: passed={res.get('passed')} total={res.get('totalScore')}")


def do_insight(api, day_log):
    _, before = api.get("/api/insights/bottleneck/latest")
    before_created = before.get("createdAt") if before.get("found") else None
    clean_idempotency("insight:")
    status, res = api.post("/api/insights/bottleneck/jobs", timeout=300)
    if status not in (200, 202):
        log_anomaly("insight-job", f"{status} {str(res)[:200]}")
        return
    if not res.get("triggered"):
        day_log["insight"] = {"triggered": False}
        log_run("  瓶颈筛查未触发")
        return
    log_run(f"  瓶颈筛查触发 signals={res.get('signals')}，等待 InsightAgent…")
    deadline = time.time() + 300
    latest = None
    while time.time() < deadline:
        _, cur = api.get("/api/insights/bottleneck/latest")
        if cur.get("found") and cur.get("createdAt") != before_created:
            latest = cur
            break
        time.sleep(5)
    if latest is None:
        log_anomaly("insight", "触发后 5 分钟内未见新洞察")
        day_log["insight"] = {"triggered": True, "pending": True}
        return
    day_log["insight"] = {"triggered": True, "nature": latest.get("nature"),
                          "statement": latest.get("statement"),
                          "replanTriggered": latest.get("replanTriggered"),
                          "signals": latest.get("signals")}
    log_run(f"  洞察: nature={latest.get('nature')} replan={latest.get('replanTriggered')}")
    if latest.get("replanTriggered"):
        # 性质变化 → 画像重生成 + force Planner，等新计划落地
        time.sleep(10)
        ensure_plan(api, day_log)


# ── 主流程 ────────────────────────────────────────────────────────────────────
def load_checkpoint():
    if CHECKPOINT.exists():
        return json.loads(CHECKPOINT.read_text(encoding="utf-8"))
    return {"nextDay": 0, "assessmentDone": False, "userId": None}


def save_checkpoint(cp):
    CHECKPOINT.write_text(json.dumps(cp, ensure_ascii=False, indent=2), encoding="utf-8")


def append_day_log(entry):
    with DAY_LOG.open("a", encoding="utf-8") as fh:
        fh.write(json.dumps(entry, ensure_ascii=False) + "\n")


def sim_date_for(day_index):
    return date.today() - timedelta(days=(TOTAL_DAYS - 1) - day_index)


def get_user_level(api):
    _, profile = api.get("/api/profile")
    return profile.get("overallLevel") or "A2", profile


def run_day(api, cp, day_index):
    day_number = day_index + 1  # 1-indexed
    sim_date = sim_date_for(day_index)
    rng = random.Random(20260730 * 100 + day_index)
    user_id = cp["userId"]
    entry = {"day": day_number, "date": sim_date.isoformat(),
             "weekday": WEEKDAYS_ZH[sim_date.weekday()]}
    log_run(f"══ 仿真日 {day_number}/{TOTAL_DAYS}（{sim_date} {entry['weekday']}）开始 ══")
    started = time.time()

    user_level, profile = get_user_level(api)

    # 0. 计划检查（日 0 由测评链路触发；之后每天开头检查过期）
    if day_index == 0 and not cp["assessmentDone"]:
        entry["active"] = True
        result = do_assessment(api, rng)
        cp["assessmentDone"] = True
        cp["assessment"] = result
        save_checkpoint(cp)
        if result and result.get("final"):
            entry["assessment"] = result["final"]
        log_run("  等待评估报告（画像 LLM）与首份学习计划…")
        plan = wait_for_plan(api)
        entry["plan"] = plan_summary(plan)
        if plan is None:
            ensure_plan(api, entry)
    else:
        active = rng.random() < ACTIVITY_RATE
        entry["active"] = active
        plan = ensure_plan(api, entry)
        entry["plan"] = plan_summary(plan)
        if active:
            do_learning(api, rng, entry)
            do_spelling(api, rng, entry)
            do_sentences(api, rng, user_level, entry)
            if day_index % 2 == 0:
                do_free_expression(api, rng, user_level, entry)
            if day_index % 2 == 1 or rng.random() < 0.25:
                do_reading(api, rng, entry)
        else:
            log_run("  小菜今天没打开 App（跳过例行）")

    # 周期性事件（挑战/瓶颈筛查，无论当天是否活跃——用户专门打开 App 做一次）
    if day_number in CHALLENGE_DAYS:
        do_challenge(api, rng, user_level, entry)
    if day_number in INSIGHT_DAYS:
        do_insight(api, entry)

    # 1. 采集状态（分数/等级/连胜）
    _, profile = api.get("/api/profile")
    _, scores = api.get("/api/profile/scores")
    entry["profile"] = {
        "overallLevel": profile.get("overallLevel"),
        "streakDays": profile.get("streakDays"),
        "totalLearned": profile.get("totalLearned"),
        "dueReviews": profile.get("dueReviews"),
        "accuracyPercent": profile.get("accuracyPercent"),
        "isUpgradeCandidate": profile.get("isUpgradeCandidate"),
    }
    entry["scores"] = scores
    entry["sql"] = lifecycle_stats(user_id)

    # 2. 时间穿越（最后一天停在真实 now）
    if day_index < TOTAL_DAYS - 1:
        time_travel()
        entry["timeTravel"] = True
    else:
        entry["timeTravel"] = False

    # 3. Score 快照（Worker 24h 等不到，手工按模拟日期写入）
    insert_snapshot(user_id, sim_date, scores)

    entry["durationSec"] = round(time.time() - started, 1)
    append_day_log(entry)
    log_run(f"── 仿真日 {day_number} 完成（{entry['durationSec']}s）"
            f" 分数 V/R/W={scores.get('vocabulary')}/{scores.get('reading')}/{scores.get('writing')}"
            f" overall={scores.get('overall')} 等级={entry['profile']['overallLevel']}"
            f" 连胜={entry['profile']['streakDays']} 阶段分布={entry['sql']['lifecycleDist']}")


def dump_final_state(api, user_id):
    log_run("采集 final-state.json …")
    state = {"generatedAt": datetime.now().isoformat(timespec="seconds")}

    def grab(name, path):
        status, data = api.get(path)
        state[name] = data if status == 200 else {"_status": status, "_error": data}

    grab("profile", "/api/profile")
    grab("profileScores", "/api/profile/scores")
    grab("scoreHistory", "/api/profile/scores/history?days=30")
    grab("weakness", "/api/profile/weakness")
    grab("evaluationLatest", "/api/evaluation/latest")
    grab("plannerCurrent", "/api/planner/current")
    grab("bottleneckLatest", "/api/insights/bottleneck/latest")
    grab("levelHistory", "/api/level/history")
    grab("challengeRecent", "/api/challenge/recent")
    state["sql"] = {
        "lifecycle": lifecycle_stats(user_id),
        "logCounts": {
            "wordLearningLogs": psql(f'SELECT count(*) FROM "WordLearningLogs" WHERE "UserId" = \'{user_id}\'', tuples_only=True),
            "sentenceLogs": psql(f'SELECT count(*) FROM "SentenceLogs" WHERE "UserId" = \'{user_id}\'', tuples_only=True),
            "freeExpressionLogs": psql(f'SELECT count(*) FROM "FreeExpressionLogs" WHERE "UserId" = \'{user_id}\'', tuples_only=True),
            "spellingLogs": psql(f'SELECT count(*) FROM "SpellingLogs" WHERE "UserId" = \'{user_id}\'', tuples_only=True),
            "readingLogs": psql(f'SELECT count(*) FROM "ReadingLogs" WHERE "UserId" = \'{user_id}\'', tuples_only=True),
            "learningPlans": psql(f'SELECT count(*) FROM "LearningPlans" WHERE "UserId" = \'{user_id}\'', tuples_only=True),
            "bottleneckInsights": psql(f'SELECT count(*) FROM "BottleneckInsights" WHERE "UserId" = \'{user_id}\'', tuples_only=True),
            "snapshots": psql(f'SELECT count(*) FROM "ProfileScoreSnapshots" WHERE "UserId" = \'{user_id}\'', tuples_only=True),
        },
    }
    FINAL_STATE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")
    log_run(f"final-state.json 已写入（{FINAL_STATE.stat().st_size} bytes）")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--until", type=int, default=TOTAL_DAYS - 1,
                        help="跑到的最后一个仿真日（0-indexed，含），默认 29")
    args = parser.parse_args()

    DATA_DIR.mkdir(parents=True, exist_ok=True)
    TIME_TRAVEL_SQL_FILE.write_text(
        "-- 每仿真日结束执行一次（整体回拨 1 天）；由 sim.py TIME_TRAVEL_STATEMENTS 自动执行\n"
        + ";\n".join(TIME_TRAVEL_STATEMENTS) + ";\n", encoding="utf-8")

    cp = load_checkpoint()
    api = ApiClient()
    if cp.get("userId"):
        user = api.login()
        log_run(f"断点续跑：已登录 {EMAIL}，下一仿真日 = {cp['nextDay'] + 1}")
    else:
        user, _ = api.register_or_login()
        cp["userId"] = user["id"]
        save_checkpoint(cp)
        log_run(f"用户就绪: {user['id']} ({user.get('displayName')})")

    until = min(args.until, TOTAL_DAYS - 1)
    for day_index in range(cp["nextDay"], until + 1):
        try:
            run_day(api, cp, day_index)
        except Exception:
            log_anomaly("day-abort", f"仿真日 {day_index + 1} 异常中止", )
            raise
        cp["nextDay"] = day_index + 1
        save_checkpoint(cp)

    if cp["nextDay"] >= TOTAL_DAYS:
        dump_final_state(api, cp["userId"])
    log_run(f"本次运行结束，nextDay={cp['nextDay']}")


if __name__ == "__main__":
    main()
