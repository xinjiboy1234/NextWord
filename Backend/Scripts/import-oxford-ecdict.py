# -*- coding: utf-8 -*-
"""Oxford 3000/5000 ∩ ECDICT → 开发库 Words 幂等导入。

设计：docs/superpowers/specs/2026-08-14-oxford-ecdict-import-design.md
用法：
  python Backend/Scripts/import-oxford-ecdict.py --dry-run
  python Backend/Scripts/import-oxford-ecdict.py
  python Backend/Scripts/import-oxford-ecdict.py --database nextword
"""
from __future__ import annotations

import argparse
import csv
import json
import re
import subprocess
import sys
import uuid
import urllib.request
from collections import Counter
from pathlib import Path

OXFORD_URL = "https://raw.githubusercontent.com/nalgeon/words/main/data/oxford-5k.csv"
ECDICT_URL = "https://raw.githubusercontent.com/skywind3000/ECDICT/master/ecdict.csv"
CACHE_DIR = Path(__file__).resolve().parent / "wordlist-work" / "oxford-ecdict"
DOCKER_PG = "nextword-postgres-1"
DEFAULT_DB = "nextword"
BATCH = 150

CEFR_ORDER = ("A1", "A2", "B1", "B2", "C1", "C2")
CEFR_RANK = {level: index for index, level in enumerate(CEFR_ORDER)}

CJK_RE = re.compile(r"[\u4e00-\u9fff]")
ASCII_LINE_RE = re.compile(r"^[A-Za-z0-9 ,.;:'\"()\-/]+$")
POS_PREFIX_RE = re.compile(
    r"^(?:n|v|vt|vi|adj|adv|prep|conj|pron|int|art|num|aux|pl|abbr)\.\s*",
    re.IGNORECASE,
)
POS_TAG_MAP = {
    "n": "n.",
    "v": "v.",
    "a": "adj.",
    "adj": "adj.",
    "r": "adv.",
    "adv": "adv.",
    "prep": "prep.",
    "conj": "conj.",
    "pron": "pron.",
    "int": "int.",
}


def normalize_cefr(raw: str) -> str | None:
    level = (raw or "").strip().upper()
    return level if level in CEFR_RANK else None


def lowest_cefr(levels: list[str]) -> str | None:
    best: str | None = None
    for raw in levels:
        level = normalize_cefr(raw)
        if level is None:
            continue
        if best is None or CEFR_RANK[level] < CEFR_RANK[best]:
            best = level
    return best


def ensure_cache(force: bool = False) -> tuple[Path, Path]:
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    oxford_path = CACHE_DIR / "oxford-5k.csv"
    ecdict_path = CACHE_DIR / "ecdict.csv"
    for url, path in ((OXFORD_URL, oxford_path), (ECDICT_URL, ecdict_path)):
        if force or not path.exists() or path.stat().st_size == 0:
            print(f"Downloading {url} -> {path}")
            urllib.request.urlretrieve(url, path)
    return oxford_path, ecdict_path


def parse_oxford_csv(path: Path) -> dict[str, str]:
    by_lemma: dict[str, list[str]] = {}
    with path.open("r", encoding="utf-8", newline="") as fh:
        reader = csv.DictReader(fh)
        for row in reader:
            lemma = (row.get("word") or "").strip().lower()
            if not lemma or len(lemma) > 80:
                continue
            level = normalize_cefr(row.get("level") or "")
            if level is None:
                continue
            by_lemma.setdefault(lemma, []).append(level)
    result: dict[str, str] = {}
    for lemma, levels in by_lemma.items():
        best = lowest_cefr(levels)
        if best is not None:
            result[lemma] = best
    return result


def prefer_ecdict_row(current: dict, candidate: dict) -> dict:
    cur_ox = 1 if str(current.get("oxford") or "").strip() else 0
    can_ox = 1 if str(candidate.get("oxford") or "").strip() else 0
    if can_ox != cur_ox:
        return candidate if can_ox > cur_ox else current

    def collins_score(row: dict) -> int:
        raw = str(row.get("collins") or "").strip()
        return int(raw) if raw.isdigit() else 0

    cur_c, can_c = collins_score(current), collins_score(candidate)
    if can_c != cur_c:
        return candidate if can_c > cur_c else current

    cur_len = len(str(current.get("translation") or ""))
    can_len = len(str(candidate.get("translation") or ""))
    if can_len != cur_len:
        return candidate if can_len > cur_len else current
    return current  # first-seen wins on full tie


def scan_ecdict(path: Path, lemmas: set[str]) -> dict[str, dict]:
    chosen: dict[str, dict] = {}
    with path.open("r", encoding="utf-8", newline="") as fh:
        reader = csv.DictReader(fh)
        for row in reader:
            lemma = (row.get("word") or "").strip().lower()
            if lemma not in lemmas:
                continue
            phonetic = (row.get("phonetic") or "").strip()
            translation = (row.get("translation") or "").strip()
            if not phonetic or not translation:
                continue
            item = {
                "phonetic": phonetic,
                "translation": translation,
                "pos": (row.get("pos") or "").strip(),
                "oxford": (row.get("oxford") or "").strip(),
                "collins": (row.get("collins") or "").strip(),
            }
            if lemma not in chosen:
                chosen[lemma] = item
            else:
                chosen[lemma] = prefer_ecdict_row(chosen[lemma], item)
    return chosen


def split_meanings(translation: str) -> list[str]:
    text = (translation or "").replace("\r\n", "\n").replace("\r", "\n")
    meanings: list[str] = []
    seen: set[str] = set()
    for line in text.split("\n"):
        raw = line.strip()
        if not raw:
            continue
        if ASCII_LINE_RE.fullmatch(raw):
            continue
        cleaned = POS_PREFIX_RE.sub("", raw).strip()
        if not cleaned:
            continue
        if cleaned[0:2].lower() in {"a.", "n.", "v."} and not CJK_RE.search(cleaned):
            continue
        if not CJK_RE.search(cleaned):
            continue
        if cleaned in seen:
            continue
        seen.add(cleaned)
        meanings.append(cleaned)
        if len(meanings) >= 5:
            break
    return meanings


def map_pos(pos: str, lemma: str) -> str:
    raw = (pos or "").strip()
    if not raw:
        return "phr." if " " in lemma else "n."
    best_tag = None
    best_weight = -1
    for part in raw.split("/"):
        part = part.strip()
        if not part:
            continue
        if ":" in part:
            tag, weight_s = part.split(":", 1)
            try:
                weight = int(weight_s)
            except ValueError:
                weight = 0
        else:
            tag, weight = part, 0
        tag = tag.strip().lower()
        if tag.startswith("adj"):
            key = "adj"
        elif tag.startswith("adv"):
            key = "adv"
        else:
            key = tag[:1] if tag[:1] in POS_TAG_MAP else tag
        mapped = POS_TAG_MAP.get(key) or POS_TAG_MAP.get(tag)
        if mapped and weight >= best_weight:
            best_tag, best_weight = mapped, weight
    if best_tag:
        return best_tag[:40]
    return "phr." if " " in lemma else "n."


def format_phonetics(phonetic: str) -> str:
    value = (phonetic or "").strip()
    if not value:
        return ""
    if not (value.startswith("/") and value.endswith("/")):
        value = f"/{value}/"
    return value[:120]


def difficulty_for(cefr: str) -> str:
    if cefr in {"A1", "A2"}:
        return "Basic"
    if cefr in {"B1", "B2"}:
        return "Intermediate"
    return "Advanced"


def build_word_rows(oxford: dict[str, str], ecdict: dict[str, dict]) -> tuple[list[dict], Counter]:
    drops: Counter = Counter()
    rows: list[dict] = []
    for lemma, cefr in sorted(oxford.items()):
        if lemma not in ecdict:
            drops["no_ecdict"] += 1
            continue
        entry = ecdict[lemma]
        meanings = split_meanings(entry["translation"])
        if not meanings:
            drops["no_meanings"] += 1
            continue
        phonetics = format_phonetics(entry["phonetic"])
        if not phonetics:
            drops["no_phonetics"] += 1
            continue
        if not (1 <= len(lemma) <= 80):
            drops["lemma_length"] += 1
            continue
        rows.append(
            {
                "lemma": lemma,
                "cefr": cefr,
                "difficulty": difficulty_for(cefr),
                "phonetics": phonetics,
                "meanings": meanings,
                "pos": map_pos(entry["pos"], lemma),
            }
        )
    return rows, drops


def sql_quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def psql(sql: str, database: str) -> str:
    out = subprocess.run(
        [
            "docker",
            "exec",
            "-i",
            DOCKER_PG,
            "psql",
            "-U",
            "nextword",
            "-d",
            database,
            "-v",
            "ON_ERROR_STOP=1",
            "-t",
            "-A",
            "-c",
            sql,
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if out.returncode != 0:
        raise SystemExit(f"psql failed: {out.stderr}")
    return out.stdout.strip()


def existing_lemmas(database: str) -> set[str]:
    raw = psql('SELECT lower("Lemma") FROM "Words"', database)
    return {line.strip() for line in raw.splitlines() if line.strip()}


def insert_batches(rows: list[dict], database: str) -> int:
    inserted = 0
    for start in range(0, len(rows), BATCH):
        batch = rows[start : start + BATCH]
        values = []
        for row in batch:
            meanings_json = json.dumps(row["meanings"], ensure_ascii=False)
            values.append(
                "("
                + ",".join(
                    [
                        sql_quote(str(uuid.uuid4())),
                        sql_quote(row["lemma"]),
                        sql_quote(row["pos"]),
                        sql_quote(row["phonetics"]),
                        sql_quote(meanings_json),
                        sql_quote("[]"),
                        sql_quote(row["difficulty"]),
                        sql_quote(row["cefr"]),
                        "TRUE",
                        "0",
                    ]
                )
                + ")"
            )
        sql = (
            "BEGIN;\n"
            'INSERT INTO "Words" '
            '("Id","Lemma","PartOfSpeech","Phonetics","Meanings","ExampleSentences",'
            '"DifficultyLevel","CefrLevel","IsCore","ScenarioAnnotationVersion") VALUES\n'
            + ",\n".join(values)
            + ";\nCOMMIT;"
        )
        psql(sql, database)
        inserted += len(batch)
        print(f"  inserted batch {start // BATCH + 1}: +{len(batch)} (total {inserted})")
    return inserted


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Import Oxford∩ECDICT words into Words table")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force-download", action="store_true")
    parser.add_argument("--database", default=DEFAULT_DB)
    args = parser.parse_args(argv)

    oxford_path, ecdict_path = ensure_cache(force=args.force_download)
    oxford = parse_oxford_csv(oxford_path)
    print(f"Oxford lemmas: {len(oxford)}")
    ecdict = scan_ecdict(ecdict_path, set(oxford))
    print(f"ECDICT hits: {len(ecdict)}")
    rows, drops = build_word_rows(oxford, ecdict)
    print(f"Field drops: {dict(drops)}")
    print(f"Candidates after filter: {len(rows)}")

    existing = existing_lemmas(args.database)
    print(f"Existing Words lemmas: {len(existing)}")
    to_insert = [row for row in rows if row["lemma"] not in existing]
    skipped = len(rows) - len(to_insert)
    print(f"Skip existing: {skipped}")
    print(f"{'Would insert' if args.dry_run else 'Insert'}: {len(to_insert)}")

    if args.dry_run:
        return 0
    if not to_insert:
        print("Nothing to insert.")
        return 0
    inserted = insert_batches(to_insert, args.database)
    print(f"Done. Inserted {inserted}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
