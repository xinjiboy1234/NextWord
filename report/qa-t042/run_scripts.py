# -*- coding: utf-8 -*-
"""T-042 QA driver: 三剧本实测测评防伪闸（仅标准库）。
用法: python run_scripts.py <rookie|strong|skip>
证据 JSON 写入 report/qa-t042/evidence-<script>.json（不含 JWT）。
"""
import json
import os
import subprocess
import sys
import time
import urllib.request
import uuid

API = "http://localhost:5192"
DASH = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"
KEY = os.environ["DASHSCOPE_API_KEY"]
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)))


def http(method, url, body=None, token=None, timeout=180):
    req = urllib.request.Request(url, method=method)
    data = None
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, data=data, timeout=timeout) as resp:
            return resp.status, json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read().decode("utf-8") or "{}")


def llm(prompt, temperature=0.7):
    body = {
        "model": "qwen-plus",
        "messages": [{"role": "user", "content": prompt}],
        "temperature": temperature,
    }
    status, data = http("POST", DASH, body, token=KEY)
    if status != 200:
        raise RuntimeError(f"LLM call failed: {status} {data}")
    return data["choices"][0]["message"]["content"].strip()


def psql(sql):
    out = subprocess.run(
        ["docker", "exec", "nextword-postgres-1", "psql", "-U", "nextword",
         "-d", "nextword_qa_t042", "-t", "-A", "-c", sql],
        capture_output=True, text=True)
    if out.returncode != 0:
        raise RuntimeError("psql failed: " + out.stderr)
    return out.stdout.strip()


def block_payload(assessment_id, block_index):
    """从 AssessmentRecords 取块 payload（含 correctIndex，仅 QA 用来控制对错）。"""
    raw = psql(
        "SELECT \"QuestionsJson\" FROM \"AssessmentRecords\" "
        f"WHERE \"AssessmentId\"='{assessment_id}' AND \"QuestionType\"='block:{block_index}'")
    return json.loads(raw)


def gen_answer(kind, target_word, scenario, prompt_text, level):
    if level == "rookie":
        persona = (
            "你是一名中国初中生，英语初学者。请用简单的短句回答下面的英语练习。"
            "只用初中词汇，句子短（6-12个词），语法基本正确但表达简单。"
            "绝对不要使用高级词汇或复杂句型。只输出答案本身，不要解释。"
        )
    else:
        persona = (
            "You are a fluent English learner at C1 level. Answer the following exercise "
            "with natural, accurate, and reasonably rich English (2-4 sentences for scenarios, "
            "one well-built sentence for sentence tasks). Output only the answer."
        )
    task = f"练习类型: {kind}\n目标词: {target_word or '(无)'}\n情境: {scenario}\n题目: {prompt_text}"
    return llm(persona + "\n\n" + task)


def run(script):
    tag = uuid.uuid4().hex[:8]
    email = f"qa-t042-{script}-{tag}@example.com"
    status, reg = http("POST", API + "/api/auth/register",
                       {"email": email, "password": "QaTest123!", "displayName": f"qa-{script}"})
    assert status == 200, reg
    token = reg["token"]
    user_id = reg["user"]["id"]

    status, started = http("POST", API + "/api/assessment/initial/start",
                           {"userId": user_id}, token)
    assert status == 200, started
    aid = started["assessmentId"]
    log = {"script": script, "email": email, "assessmentId": aid, "blocks": []}

    final = None
    for step in range(1, 8):
        status, resp = http("GET", API + f"/api/assessment/{aid}/next-block", token=token)
        assert status == 200, resp
        if resp.get("converged") or not resp.get("block"):
            final = resp.get("final")
            break
        block = resp["block"]
        bidx = block["blockIndex"]
        payload = block_payload(aid, bidx)
        answers = []

        for item in block["production"]:
            text = gen_answer(item["kind"], item.get("targetWord"),
                              item.get("scenarioZh", ""), item.get("prompt", ""),
                              "rookie" if script == "rookie" else "strong")
            answers.append({"id": item["id"], "text": text, "selectedIndex": None, "lookupCount": None})
            time.sleep(0.3)

        vocab_correct = {v["id"]: v["correctIndex"] for v in payload.get("vocabulary", [])}
        vocab_ids = [v["id"] for v in block["vocabulary"]]
        if script == "rookie":
            # 识别全答错（accuracy 0 → 参考 A1），与表达制造 ≥2 档差
            for i, vid in enumerate(vocab_ids):
                correct = vocab_correct[vid]
                nopts = len(block["vocabulary"][i]["options"])
                answers.append({"id": vid, "text": None,
                                "selectedIndex": (correct + 1) % nopts, "lookupCount": None})
        elif script == "strong":
            for i, vid in enumerate(vocab_ids):
                answers.append({"id": vid, "text": None,
                                "selectedIndex": vocab_correct[vid], "lookupCount": None})
        # skip 剧本：识别题完全不作答

        if block.get("reading") and script != "skip":
            rid = block["reading"]["id"]
            rpayload = payload.get("reading") or {}
            nopts = len(block["reading"]["options"])
            if script == "rookie":
                pick = 0  # 随机答（固定 0）
            else:
                pick = rpayload.get("correctIndex", 0)
            answers.append({"id": rid, "text": None, "selectedIndex": pick, "lookupCount": 2})

        status, result = http("POST", API + f"/api/assessment/{aid}/blocks/{bidx}/submit",
                              {"answers": answers}, token)
        assert status == 200, result
        log["blocks"].append({
            "blockIndex": bidx, "band": result.get("band"),
            "blockExpressionScore": result.get("blockExpressionScore"),
            "nextBand": result.get("nextBand"), "converged": result.get("converged"),
            "productionAnswers": [a["text"] for a in answers if a["text"]],
        })
        if result.get("converged"):
            final = result.get("final")
            break

    log["final"] = final

    # 评估报告（后台任务生成，轮询 /api/evaluation/latest）
    report_summary = None
    for _ in range(40):
        time.sleep(3)
        status, rep = http("GET", API + f"/api/evaluation/latest?userId={user_id}", token=token)
        if status == 200 and isinstance(rep, dict):
            content = rep.get("contentJson") or ""
            try:
                content = json.loads(content)
            except Exception:
                pass
            if isinstance(content, dict) and content.get("summary"):
                report_summary = content["summary"]
                log["reportSchemaVersion"] = content.get("schemaVersion")
                break
    log["reportSummary"] = report_summary

    # Assessment 留痕（DB 直查 FinalLevel 记录）
    raw = psql(
        "SELECT \"ScoresJson\" FROM \"AssessmentRecords\" "
        f"WHERE \"AssessmentId\"='{aid}' AND \"Step\"='FinalLevel'")
    log["finalLevelRecord"] = json.loads(raw) if raw else None
    log["progressLevel"] = psql(
        f"SELECT \"OverallLevel\" FROM \"UserProgress\" WHERE \"UserId\"='{user_id}'")

    # Planner 联动
    plan = None
    for _ in range(40):
        time.sleep(3)
        status, plan = http("GET", API + "/api/planner/current", token=token)
        if status == 200 and plan.get("active"):
            break
    log["planner"] = plan

    path = os.path.join(OUT, f"evidence-{script}.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(log, f, ensure_ascii=False, indent=2)
    print(json.dumps({"script": script, "final": final,
                      "reportSummary": report_summary,
                      "progressLevel": log["progressLevel"]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    run(sys.argv[1])
