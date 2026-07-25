# -*- coding: utf-8 -*-
"""T-007 QA 边界补测：平台期斜率阈值 ±沿（修正 D 用例算术后的精确边界）。
四维 0-5 整数分 × 10 次窗口下，阈值 0.05 的精确边界：
  D1 仅末次 3 维 +1（斜率 0.0409 ≤ 0.05，标准差 0.225）→ 应判平台期
  D2 仅末次 4 维 +1（斜率 0.0545 > 0.05）→ 不应判平台期（首轮 D 实测已证，此处复测确认）
"""
import io
import json
import subprocess
import sys
import time
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
BASE = "http://localhost:5108"
DB = "nextword_verify_t007_qa"


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
        capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit(f"psql 失败: {out.stderr}")
    return out.stdout.strip()


def register(name):
    email = f"qa-t007-{name}-{int(time.time())}@example.com"
    resp = call("POST", "/api/auth/register", body={
        "email": email, "password": "Passw0rd!234", "displayName": name})
    uid = psql(f"SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = '{email}'")
    psql(f'UPDATE "UserProgress" SET "HasCompletedInitialAssessment" = true, "CefrDisplay" = \'A2\' WHERE "UserId" = \'{uid}\'')
    return resp["token"], uid


def seed(uid, rows):
    values = ",\n".join(
        f"(gen_random_uuid(), '{uid}', 'word{i}', 'life', $$Bump sentence {i}.$$, '', {g}, {n}, {v}, {r}, "
        f"'C', '[]', 'Basic', '', now() - interval '{10 - i} day')"
        for i, (g, n, v, r) in enumerate(rows))
    psql(f'INSERT INTO "SentenceLogs" ("Id","UserId","TargetWord","Scene","UserSentence","AiRevision",'
         f'"GrammarScore","NaturalScore","VocabularyScore","RelevanceScore","OverallGrade","ErrorTags",'
         f'"DifficultyLevel","Suggestion","Timestamp") VALUES\n{values}')


# D1：末次 3 维 +1（g/n/v 4、r 3）→ 均分 3.75，斜率 0.0409
token1, uid1 = register("d1-bump3dim")
seed(uid1, [(4, 4, 4, 3) if i == 9 else (3, 3, 3, 3) for i in range(10)])
resp1 = call("POST", "/api/insights/bottleneck/jobs", token1, {})
ok1 = resp1.get("triggered") and "plateau" in resp1.get("signals", [])
print(f"{'PASS' if ok1 else 'FAIL'} D1 末次3维+1(斜率0.0409)判平台期 signals={resp1.get('signals')}")

# D2：末次 4 维 +1 → 均分 4.0，斜率 0.0545
token2, uid2 = register("d2-bump4dim")
seed(uid2, [(4, 4, 4, 4) if i == 9 else (3, 3, 3, 3) for i in range(10)])
resp2 = call("POST", "/api/insights/bottleneck/jobs", token2, {})
ok2 = not resp2.get("triggered")
print(f"{'PASS' if ok2 else 'FAIL'} D2 末次4维+1(斜率0.0545)不判平台期 signals={resp2.get('signals')}")

sys.exit(0 if (ok1 and ok2) else 1)
