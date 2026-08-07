#!/usr/bin/env python3
"""T-039 复验 实弹探针：C 档 + 含池词自由表达 → 应触发 spontaneous_use 毕业（T-044）。

策略：从 DB 取当前 PromptedUse 池词，手工构造若干篇「质量递减、含池词」的文本逐篇提交，
直到拿到一篇 overallGrade == 'C' 且 graduatedWords 非空的响应为止（上限 6 篇）。
每篇用不同池词，避免前一篇 B 档提前毕业后无词可验。
"""
import json
import subprocess
import sys
import urllib.request

API = "http://localhost:5190"
EMAIL = "xiaocai.sim@example.com"
PASSWORD = "Xiaocai@2026"
UID = "9df789c3-e719-434a-97db-5eb8a7be6002"


def req(method, path, body=None, token=None):
    r = urllib.request.Request(API + path, data=json.dumps(body).encode() if body else None,
                               method=method)
    r.add_header("Content-Type", "application/json")
    if token:
        r.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(r, timeout=300) as resp:
        return json.loads(resp.read().decode())


def pool_words():
    out = subprocess.run(
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d",
         "nextword_qa_t039b", "-A", "-t", "-F", "|", "-c",
         f'SELECT w."Lemma" FROM "UserWordRelationships" r JOIN "Words" w ON w."Id"=r."WordId" '
         f"WHERE r.\"UserId\"='{UID}' AND r.\"LifecycleStage\"='PromptedUse' ORDER BY w.\"Lemma\""],
        capture_output=True, text=True)
    return [l.strip() for l in out.stdout.splitlines() if l.strip()]


# 每篇：目标池词 + 文本（刻意带菜鸟语法错误：缺冠词/主谓一致/时态，但整体可读）
ATTEMPTS = [
    ("therefore",
     "Yesterday I miss the bus, therefore I was late for work. My boss he was angry. "
     "I say sorry but he no listen. Next time I will get up more early."),
    ("afford",
     "I want buy new bike but I can not afford it. My salary is small and everything expensive now. "
     "So I still take bus every day. Maybe next year I can afford one."),
    ("nevertheless",
     "The weather was very bad yesterday, nevertheless I go to play basketball with friends. "
     "We was all wet but very happy. My mother say I will catch cold."),
    ("tip",
     "My friend give me a good tip for learn English. He say I should read every day. "
     "But I am always too tired after work. I try my best anyway."),
    ("concert",
     "Last week I go to a concert with my sister. The music was very loud and many people there. "
     "I not understand the words of songs but I enjoy it very much."),
    ("irritated",
     "This morning the bus was full and one man step on my foot. I was irritated but I say nothing. "
     "Then I arrive office late again. What a bad day."),
]


def main():
    login = req("POST", "/api/auth/login", {"email": EMAIL, "password": PASSWORD})
    token = login["token"]
    pool = pool_words()
    print(f"池词 {len(pool)} 个: {pool}", flush=True)
    results = []
    for word, text in ATTEMPTS:
        if word not in pool:
            print(f"-- {word} 已不在 PromptedUse 池（可能已毕业），跳过", flush=True)
            continue
        res = req("POST", "/api/free-expression/rate",
                  {"userId": None, "userText": text, "userLevel": None}, token)
        grade = res.get("overallGrade")
        graduated = res.get("graduatedWords") or []
        results.append({"word": word, "text": text, "grade": grade,
                        "aiScore": res.get("aiScore"), "graduatedWords": graduated,
                        "logId": res.get("id")})
        print(f"[{word}] grade={grade} aiScore={res.get('aiScore')} graduated={graduated}", flush=True)
        if grade == "C" and graduated:
            print("== 探针达成：C 档 + 含池词 → 毕业 ==")
            break
    with open("data/probe-c-grade.json", "w", encoding="utf-8") as fh:
        json.dump(results, fh, ensure_ascii=False, indent=2)
    ok = any(r["grade"] == "C" and r["graduatedWords"] for r in results)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
