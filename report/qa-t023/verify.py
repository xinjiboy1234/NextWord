#!/usr/bin/env python3
"""T-023 首次测评定级校准 —— QA 独立验收脚本（周密）。

真实链路：注册菜鸟用户 → 完整首次测评（产出题 DashScope 菜鸟作答、
词义选择题故意答错一半、阅读题随机答）→ 断言定级 ≤B1 →
联动检查 /api/profile/scores 的 cefrDisplay 与 /api/planner/current 计划。

用法：python verify.py --rounds 2
环境：NEXTWORD_API_BASE（默认 http://localhost:5198）、DASHSCOPE_API_KEY
"""
import argparse
import json
import os
import random
import re
import time
import urllib.error
import urllib.request
from datetime import datetime

API_BASE = os.environ.get("NEXTWORD_API_BASE", "http://localhost:5198")
DASHSCOPE_URL = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"
DASHSCOPE_MODEL = "qwen-plus"

BEGINNER_SYSTEM = (
    "You are a weak Chinese middle-school student (初一) learning English. Your English is poor. "
    "Rules you MUST follow: "
    "1) Every sentence MUST contain 1-2 grammar mistakes (e.g. 'she go to school', 'I am agree', "
    "'yesterday I buy a pen', missing 'a/the', wrong preposition). "
    "2) Use only very basic everyday words (school, food, family, play). "
    "3) Keep it short: 4-8 words per sentence. "
    "4) NEVER use idioms, linking phrases (first of all, moreover, on the whole) or advanced words."
)

PASSWORD = "QaT023@2026"


def log(msg):
    print(f"[{datetime.now().strftime('%H:%M:%S')}] {msg}", flush=True)


def dashscope(prompt, system=None, temperature=0.9, max_tokens=200):
    key = os.environ.get("DASHSCOPE_API_KEY")
    if not key:
        raise RuntimeError("DASHSCOPE_API_KEY 未设置")
    payload = {
        "model": DASHSCOPE_MODEL,
        "messages": ([{"role": "system", "content": system}] if system else [])
        + [{"role": "user", "content": prompt}],
        "temperature": temperature,
        "max_tokens": max_tokens,
    }
    req = urllib.request.Request(
        DASHSCOPE_URL,
        data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json"},
    )
    req.add_header("Authorization", f"Bearer {key}")
    with urllib.request.urlopen(req, timeout=120) as resp:
        data = json.loads(resp.read().decode())
    return data["choices"][0]["message"]["content"].strip()


class Api:
    def __init__(self):
        self.token = None

    def call(self, method, path, body=None, timeout=600):
        payload = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(API_BASE + path, data=payload, method=method)
        if payload:
            req.add_header("Content-Type", "application/json")
        if self.token:
            req.add_header("Authorization", f"Bearer {self.token}")
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                return resp.status, json.loads(resp.read().decode())
        except urllib.error.HTTPError as exc:
            text = exc.read().decode(errors="replace")[:400]
            raise RuntimeError(f"{method} {path} -> {exc.code}: {text}")

    def get(self, path, **kw):
        return self.call("GET", path, **kw)

    def post(self, path, body, **kw):
        return self.call("POST", path, body, **kw)


def gen_sentence_answer(target_word, prompt_text):
    prompt = (f"Make one short English sentence using the word \"{target_word}\". "
              f"Task hint: {prompt_text}. One sentence only, 5-12 words.")
    text = dashscope(prompt, system=BEGINNER_SYSTEM, max_tokens=120)
    if text:
        text = text.splitlines()[0].strip().strip('"')
        if target_word.lower() in text.lower():
            return text
    return f"I {target_word} it very much."


def gen_scenario_answer(scenario_zh, prompt_text):
    prompt = (f"Situation: {scenario_zh}. Task: {prompt_text}. "
              "Write a short English answer (6-15 words) as the beginner.")
    text = dashscope(prompt, system=BEGINNER_SYSTEM, max_tokens=150)
    if text:
        return text.splitlines()[0].strip().strip('"')
    return "I am happy. I like it very much."


def mcq_index(question, options, accuracy, rng):
    """qwen 老师作答后按目标正确率随机改错；accuracy=0.5 即故意答错一半。"""
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
        return rng.randrange(len(options))
    if rng.random() < accuracy or len(options) < 2:
        return correct
    wrong = [i for i in range(len(options)) if i != correct]
    return rng.choice(wrong)


LEVEL_ORDER = ["A1", "A2", "B1", "B2", "C1", "C2"]


def run_round(round_no, rng):
    api = Api()
    email = f"qa.t023.r{round_no}.{int(time.time())}@example.com"
    status, data = api.post("/api/auth/register", {
        "email": email, "password": PASSWORD, "displayName": f"QA-T023-R{round_no}"})
    assert status == 200 and "token" in data, f"注册失败: {status} {data}"
    api.token = data["token"]
    log(f"R{round_no}: 注册 {email}")

    _, start = api.post("/api/assessment/initial/start", {"userId": None})
    aid = start["assessmentId"]
    log(f"R{round_no}: 测评开始 {aid}")

    final = None
    for _ in range(5):
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
                text = gen_sentence_answer(prod.get("targetWord") or "like",
                                           prod.get("prompt") or "")
            answers.append({"id": prod["id"], "text": text,
                            "selectedIndex": None, "lookupCount": None})
            log(f"R{round_no}:   产出[{prod.get('kind')}] {text[:90]}")
        for vocab in block.get("vocabulary", []):
            idx = mcq_index(f"What is the Chinese meaning of \"{vocab['word']}\"?",
                            vocab["options"], 0.5, rng)  # 故意答错一半
            answers.append({"id": vocab["id"], "text": None,
                            "selectedIndex": idx, "lookupCount": None})
        reading = block.get("reading")
        if reading:
            idx = rng.randrange(len(reading["options"]))  # 阅读题随机答
            answers.append({"id": reading["id"], "text": None, "selectedIndex": idx,
                            "lookupCount": rng.randint(1, 4)})
        log(f"R{round_no}:   提交块 {block['blockIndex']}（band={block.get('band')}，{len(answers)} 题）")
        _, res = api.post(f"/api/assessment/{aid}/blocks/{block['blockIndex']}/submit",
                          {"answers": answers})
        log(f"R{round_no}:   块结果 表达分={res.get('blockExpressionScore')} "
            f"nextBand={res.get('nextBand')} converged={res.get('converged')}")
        if res.get("converged"):
            final = res.get("final")
            break
    assert final, f"R{round_no}: 5 块未收敛"

    level = final.get("overallLevel")
    log(f"R{round_no}: ★ 定级={level} 表达力={final.get('expressionScore')} "
        f"词汇参考={final.get('vocabularyReferenceScore')}({final.get('vocabularyReferenceLevel')}) "
        f"阅读参考={final.get('readingReferenceScore')}({final.get('readingReferenceLevel')})")
    log(f"R{round_no}:   四维={json.dumps(final.get('dimensions', {}), ensure_ascii=False)[:300]}")

    # 联动 1：Profile CEFR 一致
    _, scores = api.get("/api/profile/scores")
    cefr_display = scores.get("cefrDisplay")
    log(f"R{round_no}: profile/scores overall={scores.get('overall')} cefrDisplay={cefr_display}")

    # 联动 2：Planner 计划（等评估报告后台任务跑完；不活跃则手动触发一次）
    plan = None
    deadline = time.time() + 300
    triggered = False
    while time.time() < deadline:
        _, cur = api.get("/api/planner/current")
        if cur.get("active"):
            plan = cur
            break
        if not triggered and time.time() > deadline - 240:
            api.post("/api/planner/jobs", {})
            triggered = True
            log(f"R{round_no}:   手动触发 planner job")
        time.sleep(5)
    if plan:
        targets = plan.get("todaySentenceTargets") or []
        log(f"R{round_no}: planner 计划已生成 dayIndex={plan.get('dayIndex')} "
            f"今日词={plan.get('todayWordCount')} 接触词={plan.get('todayExposureCount')} "
            f"造句目标={[t.get('word') if isinstance(t, dict) else t for t in targets]}")
    else:
        log(f"R{round_no}: ⚠ 等待 planner 超时")

    return {
        "round": round_no,
        "email": email,
        "overallLevel": level,
        "expressionScore": final.get("expressionScore"),
        "vocabRef": final.get("vocabularyReferenceScore"),
        "readingRef": final.get("readingReferenceScore"),
        "dimensions": final.get("dimensions"),
        "cefrDisplay": cefr_display,
        "planner": plan,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--rounds", type=int, default=2)
    parser.add_argument("--seed", type=int, default=20260730)
    args = parser.parse_args()

    results = []
    failures = []
    for i in range(1, args.rounds + 1):
        rng = random.Random(args.seed + i)
        r = run_round(i, rng)
        results.append(r)
        lv = r["overallLevel"]
        if LEVEL_ORDER.index(lv) > LEVEL_ORDER.index("B1"):
            failures.append(f"R{i}: 定级 {lv} > B1（失败）")
        if r["cefrDisplay"] and r["cefrDisplay"] != lv:
            failures.append(f"R{i}: profile cefrDisplay={r['cefrDisplay']} 与定级 {lv} 不一致")
        if not r["planner"]:
            failures.append(f"R{i}: planner 计划未生成（联动未验证）")

    out = {"apiBase": API_BASE, "results": results, "failures": failures}
    out_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "result.json")
    with open(out_path, "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=2)
    log(f"结果写入 {out_path}")
    if failures:
        log("验收失败: " + "；".join(failures))
        raise SystemExit(1)
    log("验收通过：全部轮次定级 ≤B1，联动一致")


if __name__ == "__main__":
    main()
