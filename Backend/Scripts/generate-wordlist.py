#!/usr/bin/env python3
"""T-002 词库生成脚本：按 docs/DESIGN-scenario-taxonomy.md §4 标准，用 LLM（DashScope OpenAI 兼容接口）
为 20 个子场景各生成 >=60 个有效词 + core 通用桶 >=500 词，产出带 scenario/utility/role 标注的
种子词表 JSON（Backend/NextWord.Infrastructure/Data/wordlist-scenarios.json）。

用法：DASHSCOPE_API_KEY=... python Backend/Scripts/generate-wordlist.py
幂等：已生成的批次写入 Backend/Scripts/wordlist-work/，重跑自动续跑。
"""
import json
import os
import re
import sys
import time
import urllib.request

API_URL = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"
API_KEY = os.environ.get("DASHSCOPE_API_KEY", "")
MODEL = os.environ.get("WORDLIST_MODEL", "qwen-plus")
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
WORK_DIR = os.path.join(ROOT, "Backend", "Scripts", "wordlist-work")
OUT_PATH = os.path.join(ROOT, "Backend", "NextWord.Infrastructure", "Data", "wordlist-scenarios.json")

TAXONOMY = [
    ("daily_life", "居家生活", [("daily_routine", "日常起居"), ("home_cooking", "下厨饮食"), ("housing_chores", "居住与家务")]),
    ("getting_around", "出门在外", [("directions", "问路导航"), ("transport", "交通出行"), ("travel_lodging", "旅行住宿")]),
    ("shopping_money", "消费交易", [("shopping", "购物"), ("dining_out", "点餐就餐"), ("payment_services", "付款与办事")]),
    ("social", "社交表达", [("small_talk", "寒暄闲聊"), ("making_plans", "邀约安排"), ("requests_gratitude", "求助与致谢")]),
    ("feelings_opinions", "情感观点", [("emotions", "表达情绪"), ("opinions", "表达观点"), ("agree_disagree", "同意与反对")]),
    ("describing_narrating", "描述叙述", [("describing", "描述人事物"), ("past_experiences", "讲述经历"), ("future_plans", "计划打算")]),
    ("study_work", "学习与工作（生活化）", [("study_talk", "谈论学习"), ("work_smalltalk", "日常工作沟通")]),
]
SUB_KEYS = [sub for _, _, subs in TAXONOMY for sub, _ in subs]
SUB_ZH = {sub: zh for _, _, subs in TAXONOMY for sub, zh in subs}
VALID_ROLES = {"core_verb", "connector", "scene_noun", "phrase_pattern"}
VALID_CEFR = {"A1", "A2", "B1", "B2", "C1", "C2"}
SCENE_TARGET = 62          # 每子场景有效词下限（设计 >=60，留余量）
SCENE_CALL_WORDS = 34      # 每次调用请求的词数
CORE_TARGET = 520          # core 通用桶目标（设计 >=500，留余量）
CORE_CALL_WORDS = 52


def call_llm(prompt, max_tokens=6000, retries=5):
    body = json.dumps({
        "model": MODEL,
        "messages": [
            {"role": "system", "content": "You build vocabulary lists for English learners. Return compact valid JSON only."},
            {"role": "user", "content": prompt},
        ],
        "temperature": 0.4,
        "max_tokens": max_tokens,
    }).encode("utf-8")
    for attempt in range(retries):
        try:
            req = urllib.request.Request(API_URL, data=body, headers={
                "Authorization": f"Bearer {API_KEY}",
                "Content-Type": "application/json",
            })
            with urllib.request.urlopen(req, timeout=180) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            text = data["choices"][0]["message"]["content"]
            return extract_json(text)
        except Exception as exc:  # noqa: BLE001
            print(f"    call failed (attempt {attempt + 1}): {exc}", flush=True)
            time.sleep(5 * (attempt + 1))
    raise RuntimeError("LLM call failed after retries")


def extract_json(text):
    start = text.find("{")
    end = text.rfind("}")
    if start < 0 or end <= start:
        raise ValueError("no JSON object in response")
    return json.loads(text[start:end + 1])


def validate_word(raw, seen_lemmas):
    """校验并规范化单个词条；不合格返回 None。"""
    if not isinstance(raw, dict):
        return None
    lemma = str(raw.get("lemma", "")).strip().lower()
    if not lemma or not re.fullmatch(r"[a-z][a-z' -]{0,40}", lemma) or lemma in seen_lemmas:
        return None
    pos = str(raw.get("pos", "")).strip()[:10] or "n."
    role = str(raw.get("role", "")).strip().lower()
    if role not in VALID_ROLES:
        return None
    utility = str(raw.get("utility", "")).strip().lower()
    if utility == "low":
        return None  # 设计：low 不入库
    if utility not in ("high", "medium"):
        utility = "medium"
    cefr = str(raw.get("cefr", "")).strip().upper()
    if cefr not in VALID_CEFR:
        cefr = "A2"
    meanings = raw.get("meanings") or []
    if isinstance(meanings, str):
        meanings = [meanings]
    meanings = [str(m).strip() for m in meanings if str(m).strip()][:2]
    if not meanings:
        return None
    example = str(raw.get("example", "")).strip()
    phonetics = str(raw.get("phonetics", "")).strip()[:60]
    extras = [k for k in (raw.get("extra_scenarios") or []) if k in SUB_KEYS][:2]
    return {
        "lemma": lemma,
        "pos": pos,
        "phonetics": phonetics,
        "meanings": meanings,
        "examples": [example] if example else [],
        "cefr": cefr,
        "role": role,
        "utility": utility,
        "extra_scenarios": extras,
    }


def batch_path(name):
    return os.path.join(WORK_DIR, f"{name}.json")


def load_batch(name):
    path = batch_path(name)
    if os.path.exists(path):
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    return []


def save_batch(name, words):
    with open(batch_path(name), "w", encoding="utf-8") as fh:
        json.dump(words, fh, ensure_ascii=False)


SCENE_MIX = (
    "Mix (roughly): 40% high-frequency verbs & phrasal verbs, 25% key nouns, "
    "15% everyday adjectives, 20% useful spoken phrases / sentence frames."
)

WORD_FIELDS = """For each word return:
- lemma: base form, lowercase (phrases allowed for phrasal verbs / chunks, e.g. "pick up", "would rather")
- pos: one of "v." "n." "adj." "adv." "conj." "prep." "phr."
- phonetics: IPA in slashes, e.g. "/pɪk ʌp/" (empty string if unsure)
- meanings: 1-2 concise Chinese glosses
- example: one short natural spoken sentence (<=12 words) using the word
- cefr: A1|A2|B1|B2|C1|C2 (mostly A1-B2)
- role: core_verb | connector | scene_noun | phrase_pattern
- utility: high | medium (everyday spoken frequency x irreplaceability; do NOT include rare or bookish words)
- extra_scenarios: 0-2 OTHER sub-scenario keys from the list below where the word is also highly useful (empty for core words)

Sub-scenarios: """ + ", ".join(SUB_KEYS) + """

Return only JSON: {"words":[{"lemma":"...","pos":"...","phonetics":"...","meanings":["..."],"example":"...","cefr":"A2","role":"scene_noun","utility":"high","extra_scenarios":[]}]}"""


def generate_scene(sub_key, sub_zh, cat_key, cat_zh):
    # 只在本场景内去重；跨场景重复词在最终装配时合并场景标签（多对多，最多 3 个）
    words = load_batch(f"scene-{sub_key}")
    local_seen = {w["lemma"] for w in words}
    round_no = len(words) // SCENE_CALL_WORDS
    while len(words) < SCENE_TARGET and round_no < 10:
        want = SCENE_CALL_WORDS if len(words) < SCENE_TARGET - 10 else SCENE_TARGET - len(words) + 4
        exclusion = ""
        if local_seen:
            exclusion = "\nDo NOT include any of these already-collected words: " + ", ".join(sorted(local_seen))
        prompt = f"""Build an English vocabulary list for Chinese-speaking learners, for the life-expression scenario "{sub_key}" ({sub_zh}, category {cat_key} {cat_zh}).

Produce {want} words/short phrases most useful for SPEAKING in this scenario.
{SCENE_MIX}
Everyday spoken English only; no medical/business/academic jargon.
{WORD_FIELDS}{exclusion}"""
        print(f"  [{sub_key}] round {round_no}: requesting {want} words (have {len(words)})", flush=True)
        data = call_llm(prompt)
        added = 0
        for raw in data.get("words", []):
            w = validate_word(raw, local_seen)
            if w is None:
                continue
            local_seen.add(w["lemma"])
            words.append(w)
            added += 1
        print(f"    +{added} (total {len(words)})", flush=True)
        save_batch(f"scene-{sub_key}", words)
        round_no += 1
    return words


# core 桶按可枚举子类定向生成（泛化提示词会快速枯竭）：
# (类别名, 提示词描述, 该类目标词数)
CORE_CATEGORIES = [
    ("phrasal-verbs", "common phrasal verbs (verb + up/out/off/on/in/down/away/back/over/through), e.g. pick up, figure out, put off", 160),
    ("connectors", "connectors & discourse markers for speech: adding/contrasting/cause-result/time-sequence/hedging, e.g. however, although, actually, anyway, I mean, you know", 120),
    ("spoken-chunks", "spoken chunks & sentence frames, e.g. would you mind, I'm looking forward to, it depends, that reminds me of, as far as I know, let me know", 170),
    ("core-verbs", "high-frequency everyday verbs (not phrasal), e.g. seem, guess, suppose, realize, notice, afford, manage, avoid", 110),
    ("core-adj-adv", "everyday adjectives & adverbs for spoken evaluation and degree/time, e.g. convenient, stuff, pretty, quite, probably, suddenly", 100),
    ("time-frequency", "spoken time & frequency expressions, e.g. these days, once in a while, right away, so far, no longer, for ages", 60),
    ("reaction-chunks", "short spoken reaction & response chunks, e.g. that makes sense, fair enough, no wonder, I guess so, not really, good point", 60),
]


def generate_core(scene_lemmas):
    words = load_batch("core")
    local_seen = {w["lemma"] for w in words}
    total_target = sum(target for _, _, target in CORE_CATEGORIES)
    for cat_name, cat_desc, cat_target in CORE_CATEGORIES:
        cat_start = len(words)
        round_no = 0
        while len(words) - cat_start < cat_target and len(words) < total_target and round_no < 8:
            want = min(CORE_CALL_WORDS, cat_target - (len(words) - cat_start) + 6)
            exclusion = "\nDo NOT include any of these already-collected words: " + ", ".join(
                sorted(local_seen | scene_lemmas))
            prompt = f"""Build the CORE bucket of an English vocabulary list for Chinese-speaking learners:
cross-scenario words that belong to NO single scenario.

Sub-category: {cat_name} — {cat_desc}.
Produce {want} more core words/chunks in this sub-category.
Everyday spoken English only.
{WORD_FIELDS}{exclusion}"""
            print(f"  [core:{cat_name}] round {round_no}: have {len(words)}", flush=True)
            try:
                data = call_llm(prompt)
            except RuntimeError:
                print(f"    category {cat_name} aborted, moving on", flush=True)
                break
            added = 0
            for raw in data.get("words", []):
                w = validate_word(raw, local_seen | scene_lemmas)
                if w is None:
                    continue
                w["extra_scenarios"] = []  # core 桶定义：0 个场景
                local_seen.add(w["lemma"])
                words.append(w)
                added += 1
            print(f"    +{added} (total {len(words)})", flush=True)
            save_batch("core", words)
            round_no += 1
    return words


def main():
    if not API_KEY:
        sys.exit("DASHSCOPE_API_KEY not set")
    os.makedirs(WORK_DIR, exist_ok=True)

    scene_words = {}
    for cat_key, cat_zh, subs in TAXONOMY:
        for sub_key, sub_zh in subs:
            print(f"[scenario] {sub_key} ({sub_zh})", flush=True)
            scene_words[sub_key] = generate_scene(sub_key, sub_zh, cat_key, cat_zh)
    print("[core bucket]", flush=True)
    scene_lemmas = {w["lemma"] for words in scene_words.values() for w in words}
    core_words = generate_core(scene_lemmas)

    # 装配：按 lemma 合并跨场景重复（先出现的词条数据优先，场景标签取并集，cap 3，当前主场景优先）
    merged = {}
    for sub_key, words in scene_words.items():
        for w in words[:SCENE_TARGET]:
            scenarios = [sub_key] + [k for k in w.pop("extra_scenarios") if k != sub_key]
            existing = merged.get(w["lemma"])
            if existing is None:
                w["scenarios"] = scenarios[:3]
                merged[w["lemma"]] = w
            else:
                existing["scenarios"] = list(dict.fromkeys([sub_key] + existing["scenarios"] + scenarios[1:]))[:3]
    for w in core_words[:CORE_TARGET]:
        if w["lemma"] in merged:
            continue
        w.pop("extra_scenarios", None)
        w["scenarios"] = []
        merged[w["lemma"]] = w

    final = list(merged.values())

    with open(OUT_PATH, "w", encoding="utf-8") as fh:
        json.dump({"source": f"generate-wordlist.py ({MODEL})", "words": final}, fh, ensure_ascii=False, indent=1)

    # 汇总验收数字
    per_scene = {k: sum(1 for w in final if k in w["scenarios"]) for k in SUB_KEYS}
    core_count = sum(1 for w in final if not w["scenarios"])
    expressive = sum(1 for w in final if w["role"] in ("core_verb", "connector"))
    print("\n===== SUMMARY =====")
    print(f"total words: {len(final)}")
    print(f"core bucket: {core_count}")
    short = {k: c for k, c in per_scene.items() if c < 60}
    print(f"per-scenario min: {min(per_scene.values())} (below 60: {short or 'none'})")
    print(f"core_verb+connector: {expressive}/{len(final)} = {expressive / len(final):.1%} (target >=40%)")


if __name__ == "__main__":
    main()
