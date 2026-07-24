# -*- coding: utf-8 -*-
"""T-004 真实 LLM 链路实测（DashScope + 独立验证库 nextword_verify_t004）。
同一用户在同一块内交替给「好答案」与「坏答案」，验证真实评分有区分度；
再验证自适应块数 2–3 收敛、产出占比、最终定级结构完整。"""
import io
import json
import sys
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5108"


def call(method, path, token=None, body=None):
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    data = json.dumps(body).encode() if body is not None else None
    with urllib.request.urlopen(req, data) as resp:
        return json.loads(resp.read().decode())


def good_answer(prompt):
    if prompt.get("targetWord"):
        word = prompt["targetWord"]
        return (f"My teacher taught me the expression \"{word}\" last week, "
                f"and since then I have used it several times in daily conversations with my classmates.")
    return ("Excuse me, I think there might be a small mistake with my order. "
            "I asked for the chicken, but this looks like beef. "
            "Could you please check it for me? Thank you so much.")


def run(email, label):
    token = call("POST", "/api/auth/register", body={"email": email, "password": "Passw0rd!234", "displayName": label})["token"]
    aid = call("POST", "/api/assessment/initial/start", token, {})["assessmentId"]
    print(f"\n=== {label}: {aid[:8]}... ===")
    bands = []
    good_scores, bad_scores = [], []
    block_no = 0
    while True:
        resp = call("GET", f"/api/assessment/{aid}/next-block", token)
        if resp.get("converged"):
            final = resp["final"]
            break
        block = resp["block"]
        block_no = block["blockIndex"]
        bands.append(block["band"])
        kinds = [p["kind"] for p in block["production"]]
        assert len(block["production"]) == 3 and kinds.count("sentence") == 2 and kinds.count("scenario") == 1
        answers = []
        # 第一题给好答案，其余产出题给坏答案 → 验证 LLM 评分区分度
        for i, p in enumerate(block["production"]):
            answers.append({"id": p["id"], "text": good_answer(p) if i == 0 else "good"})
        answers.append({"id": block["vocabulary"][0]["id"], "selectedIndex": 0})
        if block.get("reading"):
            answers.append({"id": block["reading"]["id"], "selectedIndex": 0, "lookupCount": 0})
        result = call("POST", f"/api/assessment/{aid}/blocks/{block_no}/submit", token, {"answers": answers})
        print(f"block {block_no} band={block['band']} expr={result['blockExpressionScore']:.1f} next={result.get('nextBand')} converged={result['converged']}")
        if result["converged"]:
            final = result["final"]
            break
    print(f"bands: {bands}")
    print(f"FINAL: level={final['overallLevel']} expr={final['expressionScore']} vocabRef={final['vocabularyReferenceScore']} readingRef={final['readingReferenceScore']}")
    print(f"dims: grammar={final['dimensions']['grammar']} natural={final['dimensions']['natural']} vocab={final['dimensions']['vocabulary']} relevance={final['dimensions']['relevance']}")
    for c in final["dimensions"]["comments"]:
        print("  comment:", c)
    assert 2 <= block_no <= 3
    return aid, token


def sentence_logs(aid_user_email_token):
    pass


aid1, token1 = run("verify-a@example.com", "userA")
aid2, token2 = run("verify-b@example.com", "userB")

# 验证 SentenceLogs 留痕且好答案四维分高于坏答案（真实 LLM 区分度）
import subprocess
out = subprocess.run(
    ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d", "nextword_verify_t004", "-t", "-c",
     """SELECT "TargetWord", "GrammarScore", "NaturalScore", "VocabularyScore", "RelevanceScore", left("UserSentence", 40) FROM "SentenceLogs" ORDER BY "Id" """],
    capture_output=True, text=True)
print("\n--- SentenceLogs (真实 LLM 评分留痕) ---")
print(out.stdout)
rows = [line for line in out.stdout.splitlines() if line.strip()]
assert len(rows) >= 12, f"留痕不足: {len(rows)}"
good = [r for r in rows if "My teacher taught me" in r or "Excuse me" in r]
bad = [r for r in rows if r.rstrip().endswith("| good")]
def dim_sum(r):
    parts = [p.strip() for p in r.split("|")]
    return sum(int(p) for p in parts[1:5])
good_avg = sum(dim_sum(r) for r in good) / max(len(good), 1)
bad_avg = sum(dim_sum(r) for r in bad) / max(len(bad), 1)
print(f"好答案四维均分 {good_avg:.1f} vs 坏答案 {bad_avg:.1f}（各 {len(good)}/{len(bad)} 条）")
assert good_avg > bad_avg, "真实 LLM 评分未区分好坏答案"
print("\n真实 LLM 链路实测通过：评分有区分度、2–3 块收敛、产出占比 60%、留痕完整。")
