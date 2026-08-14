# -*- coding: utf-8 -*-
"""T-049 阅读查词体验修复 验收实测（周密 QA）：Mock 环境 + 独立库 nextword_qa_t049（验完删库，不动 dev 库）。
验证点：
  ① 降级（Mock 占位）释义响应 offline=true、fromCache=false、contextDefinition 带 [离线模式] 前缀；
  ② 重复查词仍 offline=true / fromCache=false，且 ArticleVocabMappings 不写入降级内容（不产生缓存占位）；
  ③ 边界：文章内不存在的词 + 带 articleId 查词 → 不炸、offline=true、不写映射；
  ④ 边界：无 articleId 查词 → 不炸、offline=true。
前置：API 以 Development + 连接串指向 nextword_qa_t049 运行在 localhost:5119。"""
import io
import json
import re
import subprocess
import sys
import time
import urllib.request
import urllib.error

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = "http://localhost:5119"
DB = "nextword_qa_t049"
FAILURES = []


def call(method, path, token=None, body=None):
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    data = json.dumps(body).encode() if body is not None else None
    with urllib.request.urlopen(req, data) as resp:
        return json.loads(resp.read().decode())


def psql(sql, db=DB):
    out = subprocess.run(
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword", "-d", db, "-t", "-A", "-c", sql],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    if out.returncode != 0:
        raise SystemExit(f"psql 失败: {out.stderr}")
    return out.stdout.strip()


def check(label, ok, evidence):
    status = "PASS" if ok else "FAIL"
    print(f"[{status}] {label} — {evidence}")
    if not ok:
        FAILURES.append(f"{label}: {evidence}")


def wait_ready(timeout=180):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            with urllib.request.urlopen(BASE + "/api/articles/", timeout=5) as resp:
                if resp.status == 200:
                    return
        except urllib.error.HTTPError as err:
            if err.code in (200, 401, 403):  # 需鉴权也算就绪
                return
        except Exception:
            pass
        time.sleep(3)
    raise SystemExit("API 启动超时")


def mapping_count(article_id):
    out = psql(f'SELECT count(*) FROM "ArticleVocabMappings" WHERE "ArticleId" = \'{article_id}\'')
    return int(out or 0)


def main():
    wait_ready()

    # 注册用户
    email = f"qa-t049-{int(time.time()*1000)}@example.com"
    reg = call("POST", "/api/auth/register", body={
        "email": email, "password": "Passw0rd!234", "displayName": "qa-t049"})
    token = reg["token"]
    print(f"注册用户 OK: {email}")

    # 取一篇文章，挑正文里的一个实词（>=5 字母，避开常见停用词）
    articles = call("GET", "/api/articles/", token)
    check("种子文章就绪", len(articles) == 21, f"文章数={len(articles)}")
    article_id = articles[0]["id"]
    detail = call("GET", f"/api/articles/{article_id}", token)
    content = detail["content"]
    stop = {"there", "their", "would", "could", "should", "about", "which", "where", "these", "those", "after", "before", "again", "every"}
    candidates = [w.lower() for w in re.findall(r"[A-Za-z]{5,}", content)]
    word = next(w for w in candidates if w not in stop)
    m = re.search(r"[^.!?]*\b" + re.escape(word) + r"\b[^.!?]*[.!?]", content, re.IGNORECASE)
    sentence = m.group(0).strip() if m else content[:200]
    print(f"选词: '{word}' 出自文章 {detail['title']!r}")

    # 基线：种子不写 ArticleVocabMappings
    base = mapping_count(article_id)
    check("映射基线为 0（种子不预置 ArticleVocabMappings）", base == 0, f"count={base}")

    # ① 首次查词
    r1 = call("POST", "/api/reading/lookup", token, {
        "word": word, "sentence": sentence, "articleId": article_id})
    check("首查 offline=true", r1.get("offline") is True, f"offline={r1.get('offline')}")
    check("首查 fromCache=false", r1.get("fromCache") is False, f"fromCache={r1.get('fromCache')}")
    check("首查带 [离线模式] 前缀", str(r1.get("contextDefinition", "")).startswith("[离线模式]"),
          f"contextDefinition={r1.get('contextDefinition')!r}")
    check("首查有例句（Mock contextual）", bool(r1.get("examples")), f"examples={len(r1.get('examples') or [])} 条")

    # ② 重复查词：仍不命中缓存，且不写降级映射
    r2 = call("POST", "/api/reading/lookup", token, {
        "word": word, "sentence": sentence, "articleId": article_id})
    check("复查 offline=true", r2.get("offline") is True, f"offline={r2.get('offline')}")
    check("复查 fromCache=false（未产生缓存占位）", r2.get("fromCache") is False, f"fromCache={r2.get('fromCache')}")
    after = mapping_count(article_id)
    check("复查后 ArticleVocabMappings 无新增", after == base, f"baseline={base} after={after}")
    row = psql(f'SELECT count(*) FROM "ArticleVocabMappings" WHERE "ArticleId" = \'{article_id}\' AND "WordLemma" = \'{word}\'')
    check("该词无映射行", int(row or 0) == 0, f"rows={row or 0}")

    # ③ 边界：文章里没有的词（无映射）+ 带 articleId
    ghost = "zephyr"
    r3 = call("POST", "/api/reading/lookup", token, {
        "word": ghost, "sentence": "A gentle zephyr moved.", "articleId": article_id})
    check("非文章词查词不炸且 offline=true", r3.get("offline") is True,
          f"offline={r3.get('offline')} def={r3.get('contextDefinition')!r}")
    after3 = mapping_count(article_id)
    check("非文章词也不写映射", after3 == base, f"count={after3}")

    # ④ 边界：无 articleId 查词
    r4 = call("POST", "/api/reading/lookup", token, {
        "word": word, "sentence": sentence, "articleId": None})
    check("无 ArticleId 查词不炸且 offline=true", r4.get("offline") is True,
          f"offline={r4.get('offline')} def={r4.get('contextDefinition')!r}")

    total_rows = psql('SELECT count(*) FROM "ArticleVocabMappings"')
    check("全库 ArticleVocabMappings 仍为 0", int(total_rows or 0) == 0, f"total={total_rows or 0}")

    print()
    if FAILURES:
        print(f"结论：{len(FAILURES)} 项失败")
        for f in FAILURES:
            print(f"  - {f}")
        sys.exit(1)
    print("结论：全部实测项通过")


if __name__ == "__main__":
    main()
