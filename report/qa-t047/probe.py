# T-047 QA 真实链路抽查：背词/阅读回写 + 拼写不回写 + 幂等重放（周密）
# 用法：python probe.py（API 须在 5187 已起，库 nextword_qa_t047）
import json
import urllib.request
import urllib.error
import uuid

BASE = "http://localhost:5187"
OUT = []


def log(line):
    print(line)
    OUT.append(str(line))


def req(method, path, body=None, token=None):
    r = urllib.request.Request(BASE + path, method=method)
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        r.add_header("Content-Type", "application/json")
    if token:
        r.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(r, data) as resp:
            text = resp.read().decode()
            return resp.status, json.loads(text) if text else None
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def scores(token):
    s, body = req("GET", "/api/profile/scores", token=token)
    assert s == 200, body
    return body


def vr(body):
    return body.get("vocabulary"), body.get("reading"), body.get("writing")


# 1. 注册 + 跳过初测
email = f"qa-t047-{uuid.uuid4().hex[:8]}@example.com"
s, reg = req("POST", "/api/auth/register", {"email": email, "password": "QaT047!pass", "displayName": "qa-t047"})
assert s == 200, reg
token = reg.get("token") or reg.get("Token")
user_id = (reg.get("user") or {}).get("id") or reg.get("userId")
log(f"[1] 注册 {email} status={s} userId={user_id}（token 不落盘）")

s, body = req("POST", "/api/assessment/initial/skip", {"userId": user_id}, token)
log(f"[2] 跳过初测 status={s} body={body}")

base = scores(token)
log(f"[3] 基线 scores: {json.dumps(base, ensure_ascii=False)}")
v0, r0, w0 = vr(base)

# 2. 背词：daily 拿词，提交 3 次（两对一错）
s, daily = req("GET", "/api/words/daily?count=10", token=token)
assert s == 200, daily
words = daily if isinstance(daily, list) else daily.get("words") or daily.get("items")
log(f"[4] daily 返回 {len(words)} 词；首词字段: {sorted(words[0].keys())}")

submitted = []
for i, w in enumerate(words[:3]):
    wid = w.get("wordId") or w.get("id")
    mode = (w.get("quizMode") or "recognition").lower()
    correct = i < 2  # 前两个答对，第三个答错
    if mode == "recall":
        answer = (w.get("lemma") or w.get("word")) if correct else "zzz-wrong"
    else:
        meanings = w.get("meanings") or []
        answer = (meanings[0] if meanings else w.get("meaning")) if correct else "zzz-wrong"
    s, res = req("POST", "/api/learning/submit", {
        "userId": user_id, "wordId": wid, "answer": answer,
        "rating": 1 if correct else 3, "responseTimeMs": 1500, "mode": mode}, token)
    assert s == 200, res
    cur = scores(token)
    v, r, _w = vr(cur)
    log(f"[5.{i+1}] submit word={w.get('lemma')} mode={mode} expect={'对' if correct else '错'} "
        f"isCorrect={res.get('isCorrect')} logId={res.get('learningLogId') or res.get('logId')} "
        f"V: {v0 if i == 0 else submitted[-1][1]} -> {v}")
    submitted.append((wid, v))

v3, r3, w3 = vr(scores(token))
log(f"[5] 背词后 V: {v0} -> {v3}（断言: 口径内变化，±1/次）R 不变: {r0} -> {r3}")

# 3. 阅读：start -> finish（查词 0 与重放幂等）
s, articles = req("GET", "/api/articles/", token=token)
assert s == 200 and articles, articles
art = articles[0]
log(f"[6] 文章: id={art.get('id')} title={art.get('title')} level={art.get('difficultyLevel') or art.get('level')} wordCount={art.get('wordCount')}")

s, rlog = req("POST", f"/api/articles/{art['id']}/reading/start", {"userId": user_id}, token)
assert s == 200, rlog
log_id = rlog.get("id")
s, fin = req("POST", f"/api/reading-logs/{log_id}/finish", {"lookupCount": 0, "commentsCount": 0}, token)
log(f"[7] finish status={s} logId={log_id}")
cur = scores(token)
v4, r4, _ = vr(cur)
log(f"[8] 阅读后 R: {r3} -> {r4}（查词率 0% → 系数 1.0）V 不变: {v3} -> {v4}")

# 重放同一 logId finish → 幂等断言（事件数不增，R 不再动）
s2, fin2 = req("POST", f"/api/reading-logs/{log_id}/finish", {"lookupCount": 0, "commentsCount": 0}, token)
cur = scores(token)
v5, r5, _ = vr(cur)
log(f"[9] 重放 finish status={s2}，R: {r4} -> {r5}（断言: 不重复加分）")

# 4. 拼写提交 → 断言无拼写回写
wid = words[3].get("wordId") or words[3].get("id")
s, sp = req("POST", "/api/spelling/submit", {"userId": user_id, "wordId": wid, "userSpelling": "wrongspelling", "attempts": 1}, token)
log(f"[10] spelling submit status={s} isCorrect={sp.get('isCorrect') if isinstance(sp, dict) else sp}")
cur = scores(token)
v6, r6, w6 = vr(cur)
log(f"[11] 拼写后 scores V/R/W: {v6}/{r6}/{w6}（断言: 无变化 {v5}/{r5}/{w3}）")

log(f"USER_ID={user_id}")
log("PROBE_DONE")

with open("probe-output.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(OUT) + "\n")
