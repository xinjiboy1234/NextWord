#!/usr/bin/env python3
"""装配最终词表：读取 wordlist-work/ 下各批次，合并跨场景重复词，输出种子 JSON 并打印验收数字。
与 generate-wordlist.py 的装配逻辑一致（当前主场景优先、cap 3、core 桶 0 场景）。"""
import json
import os

WD = os.path.join(os.path.dirname(os.path.abspath(__file__)), "wordlist-work")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "NextWord.Infrastructure", "Data", "wordlist-scenarios.json")
SCENE_TARGET = 62
CORE_TARGET = 520

TAXONOMY_SUBS = [
    "daily_routine", "home_cooking", "housing_chores",
    "directions", "transport", "travel_lodging",
    "shopping", "dining_out", "payment_services",
    "small_talk", "making_plans", "requests_gratitude",
    "emotions", "opinions", "agree_disagree",
    "describing", "past_experiences", "future_plans",
    "study_talk", "work_smalltalk",
]


def load(name):
    with open(os.path.join(WD, name), encoding="utf-8") as fh:
        return json.load(fh)


def main():
    merged = {}
    for sub_key in TAXONOMY_SUBS:
        words = load(f"scene-{sub_key}.json")
        for w in words[:SCENE_TARGET]:
            scenarios = [sub_key] + [k for k in w.pop("extra_scenarios", []) if k != sub_key and k in TAXONOMY_SUBS]
            existing = merged.get(w["lemma"])
            if existing is None:
                w["scenarios"] = scenarios[:3]
                merged[w["lemma"]] = w
            else:
                existing["scenarios"] = list(dict.fromkeys([sub_key] + existing["scenarios"] + scenarios[1:]))[:3]

    for w in load("core.json")[:]:
        if w["lemma"] in merged:
            continue
        w.pop("extra_scenarios", None)
        w["scenarios"] = []
        merged[w["lemma"]] = w

    final = list(merged.values())
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump({"source": "generate-wordlist.py + manual core top-up", "words": final}, fh, ensure_ascii=False, indent=1)

    per_scene = {k: sum(1 for w in final if k in w["scenarios"]) for k in TAXONOMY_SUBS}
    core_count = sum(1 for w in final if not w["scenarios"])
    expressive = sum(1 for w in final if w["role"] in ("core_verb", "connector"))
    print(f"total: {len(final)}")
    print(f"core bucket: {core_count}")
    print(f"per-scenario min: {min(per_scene.values())}")
    for k, c in sorted(per_scene.items(), key=lambda kv: kv[1]):
        if c < 62:
            print(f"  {k}: {c}")
    print(f"core_verb+connector: {expressive}/{len(final)} = {expressive / len(final):.1%}")


if __name__ == "__main__":
    main()
