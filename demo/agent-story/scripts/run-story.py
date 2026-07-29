# -*- coding: utf-8 -*-
"""《林晓的七天》演示驱动脚本。

通过公开 API 真实执行剧本全部多轮操作（不改代码、不改数据）。
前置：
  1. postgres 容器运行中，且已建空库 nextword_demo；
  2. llm-proxy.py 运行在 :5299（记录全部 LLM 对话）；
  3. API 以真实 LLM（经代理）+ nextword_demo 库运行在 :5108。

产物（写入 demo/agent-story/output/）：
  timeline.json            全部事件时间轴（含 story 时间标签与真实时间戳）
  api-snapshots/*.json     关键端点响应快照
  db/*.json                关键表只读 dump（留痕用）
"""
import io
import json
import re
import subprocess
import sys
import time
import urllib.request
import urllib.error
from pathlib import Path

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"
DB = "nextword_demo"
ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "output"
SNAP = OUT / "api-snapshots"
DBOUT = OUT / "db"
PERSONA = json.loads((ROOT / "data" / "persona.json").read_text(encoding="utf-8"))

EVENTS = []
TOKEN = None
UID = None


# ── 基础设施 ──────────────────────────────────────────────────────────

def call(method, path, body=None, token=None, ok=(200, 201, 202)):
    global TOKEN
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Content-Type", "application/json")
    tk = token or TOKEN
    if tk:
        req.add_header("Authorization", f"Bearer {tk}")
    data = json.dumps(body).encode() if body is not None else None
    try:
        with urllib.request.urlopen(req, data, timeout=180) as resp:
            payload = json.loads(resp.read().decode())
            if resp.status not in ok:
                raise RuntimeError(f"{method} {path} -> {resp.status}: {payload}")
            return payload
    except urllib.error.HTTPError as e:
        text = e.read().decode("utf-8", "replace")[:500]
        raise RuntimeError(f"{method} {path} -> HTTP {e.code}: {text}")


def psql(sql):
    out = subprocess.run(
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d", DB, "-t", "-A", "-c", sql],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    if out.returncode != 0:
        raise RuntimeError(f"psql 失败: {out.stderr.strip()}")
    return out.stdout.strip()


def psql_json(sql, name):
    """只读 dump 一张（组）表为 json 文件，返回解析后的对象。"""
    raw = psql(f"SELECT COALESCE(json_agg(row_to_json(t)), '[]') FROM ({sql}) t")
    obj = json.loads(raw) if raw else []
    DBOUT.mkdir(parents=True, exist_ok=True)
    (DBOUT / name).write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8")
    return obj


def snapshot(name, obj):
    SNAP.mkdir(parents=True, exist_ok=True)
    (SNAP / f"{name}.json").write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8")
    return obj


def event(story, actor, action, detail="", data=None):
    e = {"seq": len(EVENTS) + 1, "ts": time.strftime("%Y-%m-%dT%H:%M:%S"),
         "story": story, "actor": actor, "action": action, "detail": detail}
    if data is not None:
        e["data"] = data
    EVENTS.append(e)
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "timeline.json").write_text(json.dumps(EVENTS, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"[{story}] {actor} | {action} | {detail[:120]}")


def poll(label, producer, predicate, timeout=300, interval=4):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            value = producer()
            if predicate(value):
                return value
        except Exception:
            pass
        time.sleep(interval)
    raise RuntimeError(f"{label} 超时（{timeout}s）")


def safe(step_name, fn):
    """非关键步骤容错：失败记录事件但不中断剧本。"""
    try:
        return fn()
    except Exception as ex:
        event("幕后", "系统", f"步骤失败（容错继续）：{step_name}", str(ex)[:300])
        return None


# ── 第一幕：注册 + 测评定级 ─────────────────────────────────────────

def act1_assessment():
    global TOKEN, UID
    u = PERSONA["user"]
    try:
        auth = call("POST", "/api/auth/register", {
            "email": u["email"], "password": u["password"], "displayName": u["displayName"]}, token="")
        event("Day 1 上午", "林晓", "注册账号", u["email"])
    except RuntimeError:
        auth = call("POST", "/api/auth/login", {"email": u["email"], "password": u["password"]}, token="")
        event("Day 1 上午", "林晓", "登录账号（已注册）", u["email"])
    TOKEN = auth["token"]

    start = call("POST", "/api/assessment/initial/start", {})
    aid = start["assessmentId"]
    event("Day 1 上午", "林晓", "开始首次水平测评", f"assessmentId={aid}")

    sent_tpls = PERSONA["assessment"]["sentenceTemplates"]
    scen_txts = PERSONA["assessment"]["scenarioTexts"]
    final = None
    for block_index in range(3):
        nb = call("GET", f"/api/assessment/{aid}/next-block")
        if nb.get("converged"):
            final = nb.get("final")
            break
        block = nb["block"]
        answers = []
        for i, p in enumerate(block.get("production") or []):
            if p.get("kind") == "sentence":
                text = sent_tpls[(block_index * 2 + i) % len(sent_tpls)].replace("{word}", p["targetWord"])
            else:
                text = scen_txts[(block_index + i) % len(scen_txts)]
            answers.append({"id": p["id"], "text": text})
        for v in block.get("vocabulary") or []:
            answers.append({"id": v["id"], "selectedIndex": 0})
        if block.get("reading"):
            answers.append({"id": block["reading"]["id"], "selectedIndex": 0, "lookupCount": 0})
        res = call("POST", f"/api/assessment/{aid}/blocks/{block_index + 1}/submit", {"answers": answers})
        event("Day 1 上午", "规则引擎",
              f"测评第 {block_index + 1} 块评分（产出题真实 LLM 四维评分）",
              f"带={res.get('band')}→{res.get('nextBand')} 表达分={res.get('blockExpressionScore')}",
              res)
        snapshot(f"assessment-block-{block_index}", res)
        if res.get("converged"):
            final = res.get("final")
            break
    snapshot("assessment-final", final or {})
    level = (final or {}).get("finalLevel") or (final or {}).get("level")
    event("Day 1 上午", "规则引擎", "测评收敛定级 + 写入 Score 内核",
          f"final={json.dumps(final, ensure_ascii=False)[:400]}")
    event("Day 1 晚", "系统", "测评收敛自动入队 EvaluationReport 后台任务",
          "链路：Profiler Agent 生成画像 → Verifier Agent 核查 → 报告 v2 → 自动入队 Planner")
    return aid, level


# ── 第二幕：画像 + 首个计划 ─────────────────────────────────────────

def act2_profile_and_plan():
    report = poll("评估报告 Ready",
                  lambda: call("GET", "/api/evaluation/latest"),
                  lambda r: r.get("status") == "Ready", timeout=420)
    snapshot("evaluation-latest", report)
    content = json.loads(report.get("contentJson") or "{}")
    event("Day 1 晚", "Profiler Agent", "生成 WeaknessProfile 画像草稿（LLM 对话 P1，见 llm-conversations）",
          f"报告 schemaVersion={content.get('schemaVersion')}，findings={len(content.get('findings') or [])} 条")

    weakness = poll("画像可查",
                    lambda: call("GET", "/api/profile/weakness"),
                    lambda w: bool(w.get("findings")), timeout=120)
    snapshot("weakness-profile-1", weakness)
    verified = [f for f in weakness["findings"] if f.get("verification") == "verified"]
    questioned = [f for f in weakness["findings"] if f.get("verification") != "verified"]
    event("Day 1 晚", "Verifier Agent", "逐条机械核查 Finding（证据真实性/数值一致性/样本量）",
          f"Verified {len(verified)} 条，存疑 {len(questioned)} 条",
          {"verified": [f["id"] for f in verified], "questioned": [
              {"id": f["id"], "note": f.get("verificationNote")} for f in questioned]})

    plan = poll("当日 Plan 生成",
                lambda: call("GET", "/api/planner/current"),
                lambda p: p.get("active"), timeout=300)
    snapshot("plan-1", plan)
    event("Day 1 晚", "Planner Agent", "生成 7 日学习计划（只消费 Verified Finding，零 LLM）",
          f"主攻场景={plan.get('focusScenarios')} 今日词={plan.get('todayWordCount')}"
          f"（含接触词 {plan.get('todayExposureCount')}）造句目标={plan.get('todaySentenceTargets')}",
          plan)
    jobs = psql_json(
        f'SELECT "Id","JobType","Status","IdempotencyKey","CreatedAt","StartedAt","ProcessedAt","RetryCount"'
        f' FROM "BackgroundJobs" WHERE "PayloadJson" LIKE \'%{UID}%\' ORDER BY "CreatedAt"',
        "background-jobs-act2.json")
    event("Day 1 晚", "系统", "后台任务执行留痕（BackgroundJobs 表）",
          "；".join(f'{j["JobType"]}:{j["Status"]}' for j in jobs))
    return plan


# ── 第三幕：Day 2-4 按计划学习（风格正常期） ───────────────────────

def learn_daily_words(story_day):
    words = call("GET", "/api/words/daily?count=10")
    from_plan = sum(1 for w in words if w.get("fromPlan"))
    exposure = sum(1 for w in words if w.get("isExposure"))
    event(story_day, "林晓", "领取今日词队列（Plan 优先）",
          f"{len(words)} 词：fromPlan {from_plan}、超带接触词 {exposure}、"
          f"阶段分布={sorted({w.get('stage') for w in words})}",
          [{"lemma": w["lemma"], "fromPlan": w.get("fromPlan"), "isExposure": w.get("isExposure"),
            "stage": w.get("stage"), "quizMode": w.get("quizMode")} for w in words])
    for w in words[:5]:
        r = call("POST", "/api/learning/submit", {
            "wordId": w["id"], "answer": w["lemma"], "rating": "Remembered",
            "responseTimeMs": 1500, "mode": w.get("quizMode") or "recognition"})
        event(story_day, "林晓", f"背词作答：{w['lemma']}",
              f"mode={w.get('quizMode')} 正确={r.get('isCorrect')} 阶段={r.get('stage')}→下次 {r.get('quizMode')}")
    return words


def practice_sentences(story_day, templates, words_fallback, tag):
    prompts = call("GET", "/api/sentences/prompts?count=10")
    from_plan = [p for p in prompts if p.get("fromPlan")]
    targets = [p["targetWord"] for p in from_plan] or [p["targetWord"] for p in prompts]
    if words_fallback:
        targets += [w["lemma"] for w in words_fallback]
    results = []
    for i, tpl in enumerate(templates):
        word = targets[i % len(targets)]
        text = tpl.replace("{word}", word)
        r = call("POST", "/api/sentences/rate", {"targetWord": word, "userSentence": text, "scene": "demo"})
        results.append(r)
        event(story_day, "林晓", f"造句（{tag}）：{word}",
              f"「{text[:70]}…」评分 {r.get('grammarScore')}/{r.get('naturalScore')}/"
              f"{r.get('vocabularyScore')}/{r.get('relevanceScore')} 档={r.get('overallGrade')}")
    return results


def read_article(story_day):
    rec = call("GET", "/api/articles/recommended")
    arts = rec.get("articles") or []
    if not arts:
        return
    art = arts[0]
    detail = call("GET", f"/api/articles/{art['id']}")
    event(story_day, "林晓", f"阅读推荐文章：{art.get('title')}",
          f"fromPlan={rec.get('fromPlan')} 难度={art.get('cefrLevel')}")
    log = safe("reading start", lambda: call("POST", f"/api/articles/{art['id']}/reading/start", {}))
    content = detail.get("content") or ""
    sentences = re.split(r"(?<=[.!?])\s+", content)
    pick, sent = None, ""
    for s in sentences:
        for w in re.findall(r"[A-Za-z]{6,}", s):
            if w.lower() not in {"because", "people", "should", "little", "things", "english"}:
                pick, sent = w, s
                break
        if pick:
            break
    if pick:
        r = safe("reading lookup", lambda: call("POST", "/api/reading/lookup", {
            "word": pick, "sentence": sent.strip()[:300], "articleId": art["id"]}))
        if r:
            event(story_day, "林晓", f"点词查义：{pick}",
                  f"释义={(r.get('definition') or r.get('meaning') or '')[:80] if isinstance(r, dict) else str(r)[:80]}")
    if log and log.get("id"):
        safe("reading finish", lambda: call("POST", f"/api/reading-logs/{log['id']}/finish", {}))


def free_expression(story_day, item):
    r = call("POST", "/api/free-expression/rate", {"userText": item["text"]})
    event(story_day, "林晓", f"自由表达（{item['day']}）",
          f"aiScore={r.get('aiScore')} 档={r.get('overallGrade')} 文本={item['text'][:60]}…")


def act3_normal_days(words_pool):
    p1 = PERSONA["sentencePractice"]["phase1"]
    fe = PERSONA["freeExpressions"]
    w2 = learn_daily_words("Day 2")
    practice_sentences("Day 2", p1[0:2], w2, "正常期")
    safe("Day2 阅读", lambda: read_article("Day 2"))
    w3 = learn_daily_words("Day 3")
    practice_sentences("Day 3", p1[2:4], w3, "正常期")
    safe("Day3 自由表达", lambda: free_expression("Day 3", fe[0]))
    w4 = learn_daily_words("Day 4")
    practice_sentences("Day 4", p1[4:6], w4, "正常期")
    safe("Day4 阅读", lambda: read_article("Day 4"))
    event("Day 4", "系统", "行为基线形成",
          "近 6 条造句连接词率约 1.5-2 个/句（because/although/when/since/if/that 等），分数平稳")


# ── 第四幕：Day 5-6 回避期 ─────────────────────────────────────────

def act4_avoidance_days():
    p2 = PERSONA["sentencePractice"]["phase2"]
    fe = PERSONA["freeExpressions"]
    w5 = learn_daily_words("Day 5")
    practice_sentences("Day 5", p2[0:3], w5, "回避期·全简单句")
    safe("Day5 自由表达", lambda: free_expression("Day 5", fe[1]))
    w6 = learn_daily_words("Day 6")
    practice_sentences("Day 6", p2[3:6], w6, "回避期·全简单句")
    safe("Day6 自由表达", lambda: free_expression("Day 6", fe[2]))
    event("Day 6", "系统", "回避期数据落库（规则引擎沉默记录，无即时反馈）",
          "近 6 条造句零复杂连接词；分数未崩（简单句语法仍正确）——传统产品盲区")


# ── 第五幕：Day 7 Agent 介入 ───────────────────────────────────────

def act5_insight_and_replan():
    trig = call("POST", "/api/insights/bottleneck/jobs", {})
    event("Day 7", "规则引擎", "日级指标筛查（零 LLM；剧本以手动端点调整触发时机）",
          f"triggered={trig.get('triggered')} signals={trig.get('signals')}")
    if not trig.get("triggered"):
        event("Day 7", "系统", "警告：筛查未触发", "回避信号未命中，后续步骤按实际结果记录")
        return None
    insight = poll("洞察落库", lambda: call("GET", "/api/insights/bottleneck/latest"),
                   lambda i: i.get("found"), timeout=420)
    snapshot("insight-1", insight)
    event("Day 7", "Insight Agent", "细读近 20 条产出原文，判定瓶颈性质（LLM 对话 P2）",
          f"nature={insight.get('nature')} replanTriggered={insight.get('replanTriggered')} "
          f"证据={len(insight.get('evidenceLogIds') or [])} 条；结论：{insight.get('statement')}",
          insight)

    if insight.get("replanTriggered"):
        poll("planner:replan 完成",
             lambda: psql(f'SELECT "Status" FROM "BackgroundJobs"'
                          f' WHERE "IdempotencyKey" LIKE \'planner:replan:{UID}%\''
                          f' ORDER BY "CreatedAt" DESC LIMIT 1'),
             lambda s: s == "Completed", timeout=420)
        plan2 = call("GET", "/api/planner/current")
        snapshot("plan-2-replanned", plan2)
        event("Day 7", "Planner Agent", "重规划：当日 Plan 原地重建（force）",
              f"新主攻场景={plan2.get('focusScenarios')} 造句目标={plan2.get('todaySentenceTargets')}",
              plan2)
        weakness2 = call("GET", "/api/profile/weakness")
        snapshot("weakness-profile-2", weakness2)
        event("Day 7", "Profiler Agent", "画像重生成（assessmentId=null，对话 P3）",
              f"findings={len(weakness2.get('findings') or [])} 条")
    return insight


# ── 第六幕：终评素材 ────────────────────────────────────────────────

def act6_final(aid):
    for name, path in [("profile-scores", "/api/profile/scores"),
                       ("profile", "/api/profile"),
                       ("level-dashboard", "/api/level/dashboard"),
                       ("evaluation-latest-final", "/api/evaluation/latest"),
                       ("insight-latest-final", "/api/insights/bottleneck/latest"),
                       ("planner-current-final", "/api/planner/current")]:
        safe(name, lambda p=path, n=name: snapshot(n, call("GET", p)))
    psql_json(f'SELECT "Id","JobType","Status","IdempotencyKey","RetryCount","CreatedAt","StartedAt","ProcessedAt"'
              f' FROM "BackgroundJobs" WHERE "PayloadJson" LIKE \'%{UID}%\' ORDER BY "CreatedAt"',
              "background-jobs-all.json")
    psql_json(f'SELECT "Id","TargetWord","Scene","GrammarScore","NaturalScore","VocabularyScore","RelevanceScore",'
              f'"OverallGrade","Timestamp","UserSentence" FROM "SentenceLogs" WHERE "UserId" = \'{UID}\''
              f' ORDER BY "Timestamp"', "sentence-logs.json")
    psql_json(f'SELECT "Id","AiScore","OverallGrade","Timestamp","UserText" FROM "FreeExpressionLogs"'
              f' WHERE "UserId" = \'{UID}\' ORDER BY "Timestamp"', "free-expression-logs.json")
    psql_json(f'SELECT "Id","StartDate","CreatedAt","ContentJson" FROM "LearningPlans"'
              f' WHERE "UserId" = \'{UID}\' ORDER BY "CreatedAt"', "learning-plans.json")
    psql_json(f'SELECT "Nature","Signals","Statement","EvidenceJson","ReplanTriggered","CreatedAt"'
              f' FROM "BottleneckInsights" WHERE "UserId" = \'{UID}\' ORDER BY "CreatedAt"',
              "bottleneck-insights.json")
    psql_json(f'SELECT w."Id", w."CreatedAt", w."AssessmentId",'
              f' (SELECT json_agg(row_to_json(f)) FROM (SELECT "Dimension","DimensionKey","Polarity","Confidence",'
              f' "Verification","VerificationNote","Statement" FROM "ProfileFindings"'
              f' WHERE "ProfileId" = w."Id") f) AS findings'
              f' FROM "WeaknessProfiles" w WHERE w."UserId" = \'{UID}\' ORDER BY w."CreatedAt"',
              "weakness-profiles.json")
    event("终评", "系统", "全部快照与数据库留痕导出完成", "见 output/api-snapshots 与 output/db")


def main():
    global UID
    t0 = time.time()
    event("序幕", "系统", "演示环境就绪",
          f"API={BASE} DB={DB} LLM=qwen-plus（经 :5299 记录代理）")
    aid, level = act1_assessment()
    UID = psql(f'SELECT "Id" FROM "Users" WHERE "Email" = \'{PERSONA["user"]["email"]}\'')
    event("幕后", "系统", "用户 Id 解析", UID)
    act2_profile_and_plan()
    act3_normal_days(None)
    act4_avoidance_days()
    act5_insight_and_replan()
    act6_final(aid)
    event("终评", "系统", "剧本执行完毕", f"总耗时 {int(time.time() - t0)}s，事件 {len(EVENTS)} 条")
    print("\n=== 完成 ===")


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "finalize":
        # 收尾模式：剧本主体已跑完，仅补齐终评快照/数据库留痕与收尾事件
        u = PERSONA["user"]
        tl = OUT / "timeline.json"
        if tl.exists():  # 续跑：保留已有事件
            EVENTS.extend(json.loads(tl.read_text(encoding="utf-8")))
        auth = call("POST", "/api/auth/login", {"email": u["email"], "password": u["password"]}, token="")
        TOKEN = auth["token"]
        UID = psql(f'SELECT "Id" FROM "Users" WHERE "Email" = \'{u["email"]}\'')
        act6_final(None)
        event("终评", "系统", "剧本执行完毕", f"事件 {len(EVENTS)} 条（finalize 模式补齐）")
        print("\n=== 收尾完成 ===")
    else:
        main()
