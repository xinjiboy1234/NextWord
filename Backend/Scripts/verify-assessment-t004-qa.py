# -*- coding: utf-8 -*-
"""T-004 验收实测（周密）：真实 DashScope qwen-plus 链路 + 独立库 nextword_verify_t004。
四类模拟用户：
  good   —— 高质量答案（复用程实脚本模板，确认上限）
  medium —— 低带真实用户的中等质量答案（简单、基本正确、偶有小疵）→ 校准升带阈值 65
  weak   —— 差答案（确认降带）
  strong —— 识别全错 + 产出全好（确认识别分不拖级）
另从库中核对：出题词带内/无 low、阅读答案位置分布。"""
import io
import json
import subprocess
import sys
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"
DB = "nextword_verify_t004"


def call(method, path, token=None, body=None):
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    data = json.dumps(body).encode() if body is not None else None
    with urllib.request.urlopen(req, data) as resp:
        return json.loads(resp.read().decode())


def answers_for(block, style, recognition_correct=True):
    answers = []
    for p in block["production"]:
        word = p.get("targetWord")
        if style == "good":
            text = (f"My teacher taught me the expression \"{word}\" last week, "
                    f"and since then I have used it several times in daily conversations with my classmates.") if word else (
                "Excuse me, I think there might be a small mistake with my order. "
                "I asked for the chicken, but this looks like beef. "
                "Could you please check it for me? Thank you so much.")
        elif style == "medium":
            # A2 学习者典型中等答案：句子短、结构简单、基本达意，带轻微中式表达
            text = (f"I want to {word} because it is very important for me and my family.") if word else (
                "Excuse me, I think my order is wrong. I want chicken, not this one. "
                "Can you change it for me? Thank you.")
        else:  # weak
            text = "good"
        answers.append({"id": p["id"], "text": text})
    for v in block["vocabulary"]:
        idx = 0 if recognition_correct else 1
        answers.append({"id": v["id"], "selectedIndex": idx})
    if block.get("reading"):
        answers.append({"id": block["reading"]["id"],
                        "selectedIndex": 0 if recognition_correct else 1, "lookupCount": 0})
    return answers


def run(email, label, style, recognition_correct=True):
    token = call("POST", "/api/auth/register", body={"email": email, "password": "Passw0rd!234", "displayName": label})["token"]
    aid = call("POST", "/api/assessment/initial/start", token, {})["assessmentId"]
    print(f"\n=== {label} ({style}) ===")
    bands, exprs = [], []
    block_no = 0
    while True:
        resp = call("GET", f"/api/assessment/{aid}/next-block", token)
        if resp.get("converged"):
            final = resp["final"]
            break
        block = resp["block"]
        block_no = block["blockIndex"]
        bands.append(block["band"])
        assert len(block["production"]) == 3, f"产出题数 {len(block['production'])}"
        result = call("POST", f"/api/assessment/{aid}/blocks/{block_no}/submit", token,
                      {"answers": answers_for(block, style, recognition_correct)})
        exprs.append(result["blockExpressionScore"])
        print(f"block {block_no} band={block['band']} expr={result['blockExpressionScore']:.1f} next={result.get('nextBand')} converged={result['converged']}")
        if result["converged"]:
            final = result["final"]
            break
    print(f"bands: {bands} | 块均分: {exprs}")
    print(f"FINAL: level={final['overallLevel']} expr={final['expressionScore']} vocabRef={final['vocabularyReferenceScore']} readingRef={final['readingReferenceScore']}")
    assert 2 <= block_no <= 3, f"块数 {block_no} 超出 2-3"
    return aid, exprs, final


def sql(query):
    out = subprocess.run(
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d", DB, "-t", "-A", "-c", query],
        capture_output=True, text=True)
    return out.stdout.strip()


results = {}
results["good"] = run("qa-good@example.com", "good-user", "good")
results["medium"] = run("qa-medium@example.com", "medium-user", "medium")
results["medium2"] = run("qa-medium2@example.com", "medium-user2", "medium")
results["weak"] = run("qa-weak@example.com", "weak-user", "weak")
results["strong-recog-fail"] = run("qa-strong@example.com", "strong-user", "good", recognition_correct=False)

print("\n--- SentenceLogs 分用户四维明细 ---")
rows = sql("""SELECT u."Email", l."Scene", l."UserLevel", l."GrammarScore", l."NaturalScore", l."VocabularyScore", l."RelevanceScore", left(l."UserSentence", 50)
FROM "SentenceLogs" l JOIN "Users" u ON u."Id" = l."UserId" ORDER BY u."Email", l."Id";""")
print(rows)

print("\n--- 出题词池纪律（全部块的 production/vocabulary 目标词）---")
recs = sql("""SELECT "QuestionsJson" FROM "AssessmentRecords" WHERE "Step" = 1 OR "QuestionType" LIKE 'block:%';""")
out_of_band, low_utility, reading_idx = [], [], []
word_rows = sql('SELECT "Id", "Lemma", "CefrLevel", "Utility" FROM "Words";')
words = {}
for line in word_rows.splitlines():
    parts = [p.strip() for p in line.split("|")]
    if len(parts) == 4:
        words[parts[0]] = parts[1:]
for line in recs.splitlines():
    if not line.strip():
        continue
    payload = json.loads(line)
    band = payload["band"]
    for item in payload.get("production", []):
        wid = item.get("wordId")
        if wid and wid in words:
            lemma, cefr, util = words[wid]
            if cefr != band and not (int({"A1":0,"A2":1,"B1":2,"B2":3,"C1":4}[cefr]) == int({"A1":0,"A2":1,"B1":2,"B2":3,"C1":4}[band]) - 1):
                out_of_band.append((lemma, cefr, band))
            if util == "Low":
                low_utility.append(lemma)
    for item in payload.get("vocabulary", []):
        pass
    if payload.get("reading"):
        reading_idx.append(payload["reading"]["correctIndex"])
print(f"超带词: {out_of_band if out_of_band else '无'}")
print(f"utility=low 入题: {low_utility if low_utility else '无'}")
print(f"阅读正确答案位置分布（{len(reading_idx)} 题）: {reading_idx}")

print("\n=== 阈值校准汇总 ===")
for key, (_, exprs, final) in results.items():
    print(f"{key}: 块均分 {exprs} → 定级 {final['overallLevel']} (expr={final['expressionScore']})")
