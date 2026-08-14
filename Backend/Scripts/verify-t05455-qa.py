# -*- coding: utf-8 -*-
"""T-054/T-055 验收实测（周密 QA）：Mock LLM 链路 + 独立库 nextword_qa_t05455（验完删库，不动 dev 库）。
前置：API 以连接串指向 nextword_qa_t05455 运行在 localhost:5139（Development，自动迁移+种子）。
覆盖设计 §4：
  a. GET /api/assessments 含新测评、倒序、ExpressionScore 非空、GuardAdjusted 字段在；
  b. GET /api/assessment/{id} 200，FinalResult.rubric 非空；块评分 ProductionScore 带 suggestion/aiRevision
     （Mock 评分产出 Suggestion/AiRevision 文案，应随测评记录落库）；
  c. 他人 id 访问 404；无 token 401（列表与详情）；
  d. rubric 总体标签与表达综合分分带一致（A1 0-20 起步/A2 20-35 粗糙/B1 35-70 凑合/B2 70-85 还不错/C1+ 85+ 很溜）；
     四维描述与分数档位（≤2 弱/3 中/≥4 强）一致；文案无英文 key；
  e. BackgroundJobWorker 生成 EvaluationReport 后 GET /api/evaluation/latest，summary 带「总体评价：」前缀；
  f. 降级：psql 造一条旧格式测评（FinalLevel 记录 ScoresJson 不含 rubric，块评分不含 suggestion/aiRevision），
     列表与详情不炸、字段为 null。"""
import io
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = os.environ.get("QA_BASE", "http://localhost:5139")
DB = os.environ.get("QA_DB", "nextword_qa_t05455")

OVERALL_BY_BAND = [("A1", "起步"), ("A2", "粗糙"), ("B1", "凑合"), ("B2", "还不错"), ("C1", "很溜"), ("C2", "很溜")]
DIM_TIERS = {
    "语法": ("句子结构常出错，时态单复数混乱", "大结构正确，小错不断", "稳定正确，能驾驭复合句"),
    "自然度": ("中式表达明显，句子生硬", "能看懂但不像地道说法", "表达自然地道"),
    "词汇": ("用词单调重复，有用词不当", "日常词够用但笼统", "用词准确有层次"),
    "相关度": ("表达过于简洁或答非所问", "切题但内容偏单薄", "切题且言之有物"),
}
ASCII_KEY = re.compile(r"[A-Za-z]{3,}")


def call(method, path, token=None, body=None, expect_error=False):
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    data = json.dumps(body).encode() if body is not None else None
    try:
        with urllib.request.urlopen(req, data) as resp:
            return resp.status, json.loads(resp.read().decode())
    except urllib.error.HTTPError as exc:
        if expect_error:
            return exc.code, None
        raise


def psql(sql, db=DB):
    out = subprocess.run(
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d", db, "-t", "-A", "-c", sql],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    if out.returncode != 0:
        raise SystemExit(f"psql 失败: {out.stderr}")
    return out.stdout.strip()


def register(name):
    email = f"qa-t05455-{name}-{int(time.time()*1000)}@example.com"
    _, resp = call("POST", "/api/auth/register", body={
        "email": email, "password": "Passw0rd!234", "displayName": name})
    uid = psql(f"SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = '{email}'")
    return resp["token"], uid


def answers_for(block):
    answers = []
    for p in block["production"]:
        word = p.get("targetWord")
        text = (f"My teacher taught me the expression \"{word}\" last week, "
                f"and since then I have used it several times in daily conversations with my classmates.") if word else (
            "Excuse me, I think there might be a small mistake with my order. "
            "I asked for the chicken, but this looks like beef. Could you please check it for me? Thank you so much.")
        answers.append({"id": p["id"], "text": text})
    for v in block["vocabulary"]:
        answers.append({"id": v["id"], "selectedIndex": 0})
    if block.get("reading"):
        answers.append({"id": block["reading"]["id"], "selectedIndex": 0, "lookupCount": 0})
    return answers


def run_assessment(token):
    _, start = call("POST", "/api/assessment/initial/start", token, {})
    aid = start["assessmentId"]
    while True:
        _, resp = call("GET", f"/api/assessment/{aid}/next-block", token)
        if resp.get("converged"):
            return aid, resp["final"]
        block = resp["block"]
        _, result = call("POST", f"/api/assessment/{aid}/blocks/{block['blockIndex']}/submit", token,
                         {"answers": answers_for(block)})
        print(f"  block {block['blockIndex']} band={block['band']} expr={result['blockExpressionScore']:.1f} converged={result['converged']}")
        if result["converged"]:
            return aid, result["final"]


def expected_band_label(score):
    for lo, hi, label in [(0, 20, "起步"), (20, 35, "粗糙"), (35, 70, "凑合"), (70, 85, "还不错"), (85, 101, "很溜")]:
        if lo <= score < hi:
            return label
    raise AssertionError(f"分数 {score} 越界")


def expected_dim_desc(name, score):
    weak, mid, strong = DIM_TIERS[name]
    return strong if score >= 4 else weak if score <= 2 else mid


def assert_no_english_keys(text, where):
    leaked = ASCII_KEY.findall(text)
    assert not leaked, f"{where} 文案含英文 key: {leaked} ← {text}"


# ── 用户 A 完成完整测评 ─────────────────────────────────
token_a, uid_a = register("usera")
token_b, uid_b = register("userb")
print("用户 A/B 注册完成")
aid, final = run_assessment(token_a)
print(f"A 测评完成: id={aid} level={final['overallLevel']} expr={final['expressionScore']}")

# ── a. 列表端点 ─────────────────────────────────────────
status, lst = call("GET", "/api/assessments", token_a)
assert status == 200 and isinstance(lst, list), f"列表响应异常: {status}"
mine = [item for item in lst if item["id"] == aid]
assert len(mine) == 1, f"列表未包含本次测评: {lst}"
item = mine[0]
assert item["expressionScore"] is not None, f"ExpressionScore 为空: {item}"
assert item["finalLevel"] == final["overallLevel"], f"FinalLevel 不一致: {item}"
assert item["status"] == "Completed" and item["endAt"], f"状态/结束时间异常: {item}"
assert "guardAdjusted" in item, f"缺矫正标记字段: {item}"
starts = [i["startAt"] for i in lst]
assert starts == sorted(starts, reverse=True), f"列表未按时间倒序: {starts}"
print(f"a. /api/assessments 含本次测评、倒序、ExpressionScore={item['expressionScore']}、guardAdjusted={item['guardAdjusted']} ✓")

# ── b. 详情：rubric + 逐题评语落库 ─────────────────────
# D1 已修复（详情端点返回 DTO 投影）：响应必须完整可解析，records 非空
status, detail = call("GET", f"/api/assessment/{aid}", token_a)
assert detail["records"], "详情无 Records"
records = detail["records"]
final_record = next(r for r in records if r["step"] == "FinalLevel")
final_json = json.loads(final_record["scoresJson"])
rubric = final_json.get("Rubric") or final_json.get("rubric")
assert rubric, f"FinalResult 无 Rubric 字段: {list(final_json.keys())}"

blocks = [r for r in records if r["step"] == "AdaptiveBlock"]
assert blocks, "无块记录"
prod_total = prod_with_suggestion = prod_with_revision = 0
for record in blocks:
    scores = json.loads(record["scoresJson"])
    questions = json.loads(record["questionsJson"])
    answers = json.loads(record["answersJson"])
    assert questions.get("production"), "块缺题目载荷"
    assert answers, "块缺作答记录"
    for p in scores.get("production", scores.get("Production", [])):
        prod_total += 1
        sug = p.get("suggestion", p.get("Suggestion"))
        rev = p.get("aiRevision", p.get("AiRevision"))
        if sug:
            prod_with_suggestion += 1
        if rev:
            prod_with_revision += 1
print(f"b. Records {len(records)} 条（块 {len(blocks)}，详情 API 完整可解析）；产出题 {prod_total} 题，"
      f"带评语 {prod_with_suggestion}、带改写 {prod_with_revision}")
assert prod_total > 0, "块评分无产出题"
assert prod_with_suggestion == prod_total, f"Mock 产出 Suggestion 但未全部落库: {prod_with_suggestion}/{prod_total}"
assert prod_with_revision == prod_total, f"Mock 产出 AiRevision 但未全部落库: {prod_with_revision}/{prod_total}"

# ── c. 越权与未登录 ─────────────────────────────────────
status_b, _ = call("GET", f"/api/assessment/{aid}", token_b, expect_error=True)
assert status_b == 404, f"他人 id 应 404，实际 {status_b}"
status_anon, _ = call("GET", f"/api/assessment/{aid}", expect_error=True)
assert status_anon == 401, f"未登录详情应 401，实际 {status_anon}"
status_anon_list, _ = call("GET", "/api/assessments", expect_error=True)
assert status_anon_list == 401, f"未登录列表应 401，实际 {status_anon_list}"
_, list_b = call("GET", "/api/assessments", token_b)
assert all(i["id"] != aid for i in list_b), "B 的列表含 A 的测评"
print("c. 他人 id 404、未登录 401（详情+列表）、B 列表不含 A 测评 ✓")

# ── d. rubric 总体标签与四维描述 ────────────────────────
expr = final_json.get("ExpressionScore", final_json.get("expressionScore"))
label = rubric.get("OverallLabel", rubric.get("overallLabel"))
desc = rubric.get("OverallDescription", rubric.get("overallDescription"))
dims = rubric.get("Dimensions", rubric.get("dimensions"))
expected_label = expected_band_label(expr)
assert label == expected_label, f"表达综合分 {expr} 应映射「{expected_label}」，实际「{label}」"
assert_no_english_keys(label + desc, "总体 rubric")
dim_names = set()
for d in dims:
    name = d.get("Name", d.get("name"))
    score = d.get("Score", d.get("score"))
    ddesc = d.get("Description", d.get("description"))
    dim_names.add(name)
    expected = expected_dim_desc(name, score)
    assert ddesc == expected, f"{name} {score} 分应「{expected}」，实际「{ddesc}」"
    assert_no_english_keys(name + ddesc, f"维度 {name}")
assert dim_names == {"语法", "自然度", "词汇", "相关度"}, f"维度名异常: {dim_names}"
print(f"d. rubric 总体「{label}——{desc}」与分带一致（expr={expr}）；四维描述与档位一致；文案全中文 ✓")

# ── e. 评估报告 summary 前缀 ────────────────────────────
deadline = time.time() + 90
report_summary = None
while time.time() < deadline:
    status, report = call("GET", "/api/evaluation/latest", token_a, expect_error=True)
    if status == 200:
        content = json.loads(report["contentJson"]) if isinstance(report.get("contentJson"), str) else report["contentJson"]
        if content.get("summary"):
            report_summary = content["summary"]
            break
    time.sleep(3)
assert report_summary is not None, "90s 内未生成评估报告"
assert report_summary.startswith("总体评价："), f"summary 缺人话前缀: {report_summary[:80]}"
prefix = report_summary.split("。", 1)[0]
valid_labels = {"起步", "粗糙", "凑合", "还不错", "很溜"}
assert any(lbl in prefix for lbl in valid_labels), f"报告前缀无合法人话标签: {prefix}"
# T-059：报告前缀与测评 rubric 同按表达带取标签，两者必须一致（含防伪闸矫正场景）
assert label in prefix, f"报告前缀标签与测评 rubric 不一致（{prefix} vs {label}）"
assert_no_english_keys(prefix, "报告前缀")
print(f"e. 评估报告 summary 头部: {prefix}。 ✓")

# ── f. 旧记录降级 ───────────────────────────────────────
legacy_aid = psql(
    f'INSERT INTO "Assessments" ("Id","UserId","Type","Status","StartAt","EndAt","FinalLevel")'
    f" VALUES (gen_random_uuid(),'{uid_a}','Initial','Completed', now() - interval '30 days', now() - interval '30 days' + interval '10 minutes', 'A2')"
    f' RETURNING "Id"').splitlines()[0]
# 与真实落库格式一致：camelCase 键 + 数字枚举（JsonSerializerDefaults.Web），仅缺 rubric/suggestion/aiRevision
legacy_final = json.dumps({
    "overallLevel": 2, "expressionScore": 30, "vocabularyReferenceScore": 50,
    "readingReferenceScore": 50, "vocabularyReferenceLevel": 2, "readingReferenceLevel": 2,
    "dimensions": {"grammar": 3.0, "natural": 3.0, "vocabulary": 3.0, "relevance": 3.0,
                   "topErrorTags": [], "comments": ["旧格式记录"]},
    "evaluationReportId": None, "originalLevelBeforeGuard": None}, ensure_ascii=False)
legacy_block_scores = json.dumps({
    "blockExpressionScore": 50,
    "production": [{"id": "p1", "score": 50, "grammar": 3, "natural": 3, "vocabulary": 3, "relevance": 3,
                    "errorTags": ["时态混乱"]}],
    "vocabulary": [], "reading": None, "nextBand": 2}, ensure_ascii=False)
legacy_block_questions = json.dumps({
    "blockIndex": 1, "band": 2,
    "production": [{"id": "p1", "kind": "sentence", "targetWord": "plan", "scenarioZh": "", "prompt": "用 plan 造句"}],
    "vocabulary": [], "reading": None}, ensure_ascii=False)
legacy_answers = json.dumps([{"id": "p1", "text": "I plan to study English."}], ensure_ascii=False)
esc = lambda s: s.replace("'", "''")
psql(
    f'INSERT INTO "AssessmentRecords" ("Id","AssessmentId","Step","QuestionType","QuestionsJson","AnswersJson","ScoresJson","Timestamp")'
    f" VALUES (gen_random_uuid(),'{legacy_aid}','FinalLevel','','{{}}','{{}}','{esc(legacy_final)}', now() - interval '30 days'),"
    f" (gen_random_uuid(),'{legacy_aid}','AdaptiveBlock','','{esc(legacy_block_questions)}','{esc(legacy_answers)}','{esc(legacy_block_scores)}', now() - interval '30 days')")

status, legacy_detail = call("GET", f"/api/assessment/{legacy_aid}", token_a)
legacy_records = legacy_detail["records"]
legacy_final_json = json.loads(next(r for r in legacy_records if r["step"] == "FinalLevel")["scoresJson"])
assert "Rubric" not in legacy_final_json and "rubric" not in legacy_final_json, "旧记录不应有 rubric"
legacy_prod = json.loads(next(r for r in legacy_records if r["step"] == "AdaptiveBlock")["scoresJson"])["production"][0]
assert legacy_prod.get("suggestion", legacy_prod.get("Suggestion")) is None, "旧记录不应有评语"
_, lst2 = call("GET", "/api/assessments", token_a)
legacy_item = next(i for i in lst2 if i["id"] == legacy_aid)
assert legacy_item["expressionScore"] == 30 and legacy_item["guardAdjusted"] is False, f"旧记录列表投影异常: {legacy_item}"
assert lst2[0]["id"] == aid and lst2[1]["id"] == legacy_aid, f"倒序被旧记录打乱: {[i['id'] for i in lst2]}"
print(f"f. 旧格式记录详情 200 不炸、无 rubric/评语字段（前端按 null 降级）；列表投影 expr=30、guardAdjusted=false、倒序正确 ✓")

print("\nT-054/T-055 主链路实测全部通过。")
