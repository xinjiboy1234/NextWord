# -*- coding: utf-8 -*-
"""T-061 词库标注回填（一次性环境修复）：
存量库（如 docker compose 旧镜像种子）词表仅有 CEFR/难度档，Utility/Role/Scenarios/ScenarioAnnotationVersion
全空——导致测评块（要求 Utility High/Medium）退化为 1 题、Planner 场景驱动失效。
本脚本从内置词表 wordlist-scenarios.json 按 lemma 回填：Utility/Role/CefrLevel/DifficultyLevel/
ScenarioAnnotationVersion，并重建 WordScenarios 关联（幂等：重复运行结果不变）。
用法：python Backend/Scripts/backfill-wordlist-annotations.py
"""
import io
import json
import os
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
JSON_PATH = os.path.join(BASE, "NextWord.Infrastructure", "Data", "wordlist-scenarios.json")
OUT_SQL = os.path.join(BASE, "Scripts", "backfill-wordlist-annotations.sql")

CEFR_TO_DIFF = {
    "A1": "Basic", "A2": "Basic",
    "B1": "Intermediate", "B2": "Intermediate",
    "C1": "Advanced", "C2": "Advanced",
}

def main():
    with open(JSON_PATH, encoding="utf-8") as f:
        words = json.load(f)["words"]

    lines = ["-- T-061 词库标注回填（由 backfill-wordlist-annotations.py 生成，幂等）"]
    lines.append("BEGIN;")
    for w in words:
        lemma = w["lemma"].replace("'", "''")
        utility = (w.get("utility") or "medium").lower()
        role = (w.get("role") or "scene_noun").lower()
        # EF 枚举转字符串（Enum.Parse 大小写敏感）：必须存枚举名 PascalCase
        utility = {"high": "High", "medium": "Medium"}.get(utility, "Medium")
        role = {"core_verb": "CoreVerb", "connector": "Connector",
                "scene_noun": "SceneNoun", "phrase_pattern": "PhrasePattern"}.get(role, "SceneNoun")
        cefr = (w.get("cefr") or "A2").upper()
        diff = CEFR_TO_DIFF.get(cefr, "Basic")
        lines.append(
            f"UPDATE \"Words\" SET \"Utility\"='{utility}', \"Role\"='{role}', "
            f"\"CefrLevel\"='{cefr}', \"DifficultyLevel\"='{diff}', "
            f"\"ScenarioAnnotationVersion\"=1 WHERE lower(\"Lemma\")='{lemma}';"
        )
    # 重建场景关联：先清空已回填词的相关联，再按 JSON 插入
    lines.append("DELETE FROM \"WordScenarios\" WHERE \"WordId\" IN (SELECT \"Id\" FROM \"Words\" WHERE \"ScenarioAnnotationVersion\"=1);")
    for w in words:
        lemma = w["lemma"].replace("'", "''")
        for key in (w.get("scenarios") or [])[:3]:
            key = key.replace("'", "''")
            lines.append(
                f"INSERT INTO \"WordScenarios\" (\"WordId\", \"ScenarioKey\") "
                f"SELECT w.\"Id\", '{key}' FROM \"Words\" w "
                f"WHERE lower(w.\"Lemma\")='{lemma}' "
                f"ON CONFLICT (\"WordId\", \"ScenarioKey\") DO NOTHING;"
            )
    lines.append("COMMIT;")

    with open(OUT_SQL, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"生成 {OUT_SQL}：{len(words)} 词回填 SQL")

if __name__ == "__main__":
    main()
