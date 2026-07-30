#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""T-033 验收标准 2：林晓回避剧本回归（新相对基线下仍触发回避）。

T-017 剧本口径：近 12 句产出，前 6 句复杂连接率≈1.7（共 10 个连接词），后 6 句=0。
旧口径（绝对基线 0.3）能触发；v2 新口径（后半 ≤ 前半 ×0.5 且前半 >0）必须仍触发。

做法：注册独立用户 → 直接插 12 条 SentenceLog → POST /api/insights/bottleneck/jobs。
干扰隔离：分数前低后高（排除平台期）、前半段有连接词（排除零起步）、无计划（排除安全词）。
"""
import json
import subprocess
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

API = "http://localhost:5196"
DB = "nextword_qa_t033"
EMAIL = "linxiao.qa.t033@example.com"
PASSWORD = "Linxiao@2026"

# 前 6 句：连接词 2,2,2,2,1,1 → 率 10/6 ≈ 1.67；后 6 句：0 连接词
FIRST_HALF = [
    "I stayed home because it rained, although I really wanted to go out.",
    "When I was young, I believed that money could solve everything.",
    "Although the task was hard, she finished it before the deadline.",
    "He didn't call me when he arrived, which made me worried.",
    "Because the shop was closed, I went home.",
    "If it rains tomorrow, we will stay inside.",
]
SECOND_HALF = [
    "I like apples. They are sweet.",
    "She goes to work every day.",
    "We had dinner at home yesterday.",
    "The cat sleeps on the sofa.",
    "He plays football on weekends.",
    "I bought a new bag today.",
]


def http(method, path, body=None, token=None):
    req = urllib.request.Request(API + path, data=json.dumps(body).encode() if body is not None else None,
                                 method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            text = resp.read().decode()
            return resp.status, json.loads(text) if text else {}
    except urllib.error.HTTPError as exc:
        return exc.code, {"_error": exc.read().decode(errors="replace")[:300]}


def psql(sql):
    r = subprocess.run(["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword",
                        "-d", DB, "-v", "ON_ERROR_STOP=1", "-A", "-t", "-c", sql],
                       capture_output=True, text=True, timeout=60)
    if r.returncode != 0:
        raise RuntimeError(f"SQL 失败: {r.stderr.strip()[:300]}")
    return r.stdout.strip()


def main():
    status, data = http("POST", "/api/auth/register",
                        {"email": EMAIL, "password": PASSWORD, "displayName": "林晓QA"})
    if status == 200 and "token" in data:
        token = data["token"]
    else:
        status, data = http("POST", "/api/auth/login", {"email": EMAIL, "password": PASSWORD})
        assert status == 200, f"登录失败 {status} {data}"
        token = data["token"]
    user_id = psql(f'SELECT "Id" FROM "Users" WHERE "Email" = \'{EMAIL}\'')
    print(f"user_id={user_id}")

    psql(f'DELETE FROM "SentenceLogs" WHERE "UserId" = \'{user_id}\'')
    now = datetime.now(timezone.utc)
    sentences = FIRST_HALF + SECOND_HALF
    for i, text in enumerate(sentences):
        ts = now - timedelta(days=(12 - i))  # 前 6 旧、后 6 新
        score = 2 if i < 6 else 4            # 分数上行，排除平台期
        esc = text.replace("'", "''")
        psql(
            'INSERT INTO "SentenceLogs" ("Id","UserId","WordId","TargetWord","Scene","UserSentence",'
            '"AiRevision","GrammarScore","NaturalScore","VocabularyScore","RelevanceScore",'
            '"OverallGrade","ErrorTags","DifficultyLevel","Suggestion","Timestamp") VALUES ('
            f'gen_random_uuid(), \'{user_id}\', NULL, \'qa-t033\', \'life\', \'{esc}\', \'{esc}\', '
            f'{score}, {score}, {score}, {score}, \'B\', \'[]\', \'Basic\', \'qa\', \'{ts.isoformat()}\')')
    print(f"已插入 {len(sentences)} 条 SentenceLog（前 6 连接率≈1.67 / 后 6=0，分数 2→4）")

    psql(f'DELETE FROM "BackgroundJobs" WHERE "IdempotencyKey" LIKE \'insight:{user_id}%\'')
    status, res = http("POST", "/api/insights/bottleneck/jobs", token=token)
    print(f"POST /bottleneck/jobs → {status} {json.dumps(res, ensure_ascii=False)}")
    assert status in (200, 202), "任务提交失败"
    assert res.get("triggered"), f"未触发！signals={res.get('signals')}"
    signals = res.get("signals", [])
    assert "avoidance" in signals, f"回避未触发！signals={signals}"
    print(f"[OK] 回避模式在新口径下触发 signals={signals}")
    assert signals == ["avoidance"], f"预期只有 avoidance，实际 {signals}（隔离失败？）"

    # 等 InsightAgent 定性
    deadline = time.time() + 300
    while time.time() < deadline:
        _, cur = http("GET", "/api/insights/bottleneck/latest", token=token)
        if cur.get("found"):
            print(f"[OK] 洞察: nature={cur.get('nature')} signals={cur.get('signals')} "
                  f"replan={cur.get('replanTriggered')}")
            print(f"  statement: {str(cur.get('statement'))[:200]}")
            return
        time.sleep(5)
    print("[FAIL] 5 分钟内未见洞察落库（触发已验证，定性超时）")
    sys.exit(1)


if __name__ == "__main__":
    main()
