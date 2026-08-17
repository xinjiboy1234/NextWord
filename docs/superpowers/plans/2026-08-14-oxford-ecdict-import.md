# Oxford ∩ ECDICT Words Import Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One-shot Python script that downloads Oxford 5k + ECDICT, joins them, and idempotently INSERTs missing lemmas into the current development `Words` table.

**Architecture:** Single stdlib script under `Backend/Scripts/` with pure functions for parse/filter and a `psql`-via-docker writer matching existing QA scripts. No API/DI/seed/migration changes.

**Tech Stack:** Python 3 stdlib only; PostgreSQL via `docker exec nextword-postgres-1 psql`; sources per `docs/superpowers/specs/2026-08-14-oxford-ecdict-import-design.md`.

**Spec:** `docs/superpowers/specs/2026-08-14-oxford-ecdict-import-design.md`

---

## File structure

| File | Responsibility |
|------|----------------|
| `Backend/Scripts/import-oxford-ecdict.py` | Download/cache, Oxford parse, ECDICT join, meanings/POS filter, dry-run + INSERT |
| `Backend/Scripts/test_import_oxford_ecdict.py` | Unit tests for pure helpers (CEFR rank, meanings split, POS map, row prefer) — no DB |
| `Backend/Scripts/wordlist-work/oxford-ecdict/*` | Local cache only (gitignored) |

---

## Chunk 1: Pure helpers + unit tests

### Task 1: Failing tests for pure helpers

**Files:**
- Create: `Backend/Scripts/test_import_oxford_ecdict.py`

- [ ] **Step 1: Write failing unit tests**

```python
# -*- coding: utf-8 -*-
"""Unit tests for import-oxford-ecdict pure helpers (no DB)."""
import importlib.util
import sys
import unittest
from pathlib import Path

SCRIPT = Path(__file__).with_name("import-oxford-ecdict.py")
spec = importlib.util.spec_from_file_location("import_oxford_ecdict", SCRIPT)
mod = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = mod
spec.loader.exec_module(mod)


class CefrTests(unittest.TestCase):
    def test_rank_case_insensitive_lowest(self):
        self.assertEqual(mod.lowest_cefr(["b2", "A1", "c1"]), "A1")

    def test_invalid_level_skipped(self):
        self.assertEqual(mod.lowest_cefr(["xx", "B1"]), "B1")


class MeaningsTests(unittest.TestCase):
    def test_keeps_cjk_drops_ascii(self):
        raw = "n. memory\n记忆\n回忆\njust english"
        self.assertEqual(mod.split_meanings(raw), ["记忆", "回忆"])

    def test_empty_when_no_cjk(self):
        self.assertEqual(mod.split_meanings("n. foo\nbar"), [])


class PosTests(unittest.TestCase):
    def test_weighted_pos(self):
        self.assertEqual(mod.map_pos("n:10/v:5", "memory"), "n.")

    def test_phrase_fallback(self):
        self.assertEqual(mod.map_pos("", "get up"), "phr.")


class PreferTests(unittest.TestCase):
    def test_prefer_oxford_flag(self):
        a = {"oxford": "", "collins": "5", "translation": "短"}
        b = {"oxford": "1", "collins": "1", "translation": "更长一些"}
        self.assertIs(mod.prefer_ecdict_row(a, b), b)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run tests — expect fail (module missing)**

Run: `python Backend/Scripts/test_import_oxford_ecdict.py`
Expected: ImportError / file not found for helpers

### Task 2: Implement helpers + main pipeline skeleton

**Files:**
- Create: `Backend/Scripts/import-oxford-ecdict.py`

- [ ] **Step 1: Implement helpers matching spec §3–§5**

Implement at minimum:

- `CEFR_ORDER`, `lowest_cefr(levels)`, `parse_oxford_csv(path) -> dict[str,str]`
- `prefer_ecdict_row(a, b)`, `scan_ecdict(path, lemmas) -> dict`
- `split_meanings(translation)`, `map_pos(pos, lemma)`, `format_phonetics(p)`, `difficulty_for(cefr)`
- `build_word_rows(oxford, ecdict) -> (rows, drop_counts)`
- CLI: `--dry-run`, `--force-download`, `--database` (default `nextword`). Write path is **docker/psql only** (same as existing QA scripts); do not add half-specified `--connection-string`.
- Stream ECDICT with `csv.reader` (do not load full 66MB into a list).

Constants:

```python
OXFORD_URL = "https://raw.githubusercontent.com/nalgeon/words/main/data/oxford-5k.csv"
ECDICT_URL = "https://raw.githubusercontent.com/skywind3000/ECDICT/master/ecdict.csv"
CACHE_DIR = Path(__file__).resolve().parent / "wordlist-work" / "oxford-ecdict"
DOCKER_PG = "nextword-postgres-1"
DEFAULT_DB = "nextword"
BATCH = 150
```

Download: `urllib.request.urlretrieve` into cache if missing. Print counters: Oxford lemmas / ECDICT hits / drop categories / skipped-existing / inserted.

- [ ] **Step 2: Re-run unit tests — expect PASS**

Run: `python Backend/Scripts/test_import_oxford_ecdict.py`
Expected: `OK`

- [ ] **Step 3: Commit**

```bash
git add Backend/Scripts/import-oxford-ecdict.py Backend/Scripts/test_import_oxford_ecdict.py
git commit -m "feat: Oxford∩ECDICT import script helpers and unit tests"
```

---

## Chunk 2: DB writer + dry-run + real import

### Task 3: Wire psql writer and dry-run

**Files:**
- Modify: `Backend/Scripts/import-oxford-ecdict.py`

- [ ] **Step 1: Implement `psql(sql, database)` via docker**

```python
def psql(sql: str, database: str) -> str:
    out = subprocess.run(
        ["docker", "exec", "-i", DOCKER_PG, "psql", "-U", "nextword", "-d", database, "-v", "ON_ERROR_STOP=1", "-t", "-A", "-c", sql],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if out.returncode != 0:
        raise SystemExit(f"psql failed: {out.stderr}")
    return out.stdout.strip()
```

- [ ] **Step 2: Load existing lemmas + INSERT batches**

```sql
SELECT lower("Lemma") FROM "Words";
```

INSERT columns: `"Id","Lemma","PartOfSpeech","Phonetics","Meanings","ExampleSentences","DifficultyLevel","CefrLevel","IsCore","ScenarioAnnotationVersion"`  
Values: uuid, lemma, pos, phonetics, meanings_json (escaped), `'[]'`, difficulty, cefr, `true`, `0`  
`Utility`/`Role`/`LlmAnnotationId` omit → NULL.

Escape single quotes in SQL strings by doubling `''`. Use `json.dumps(list, ensure_ascii=False)` for Meanings.

Each batch wrapped in `BEGIN;` … `COMMIT;` (single transaction). On batch failure: nonzero exit; prior committed batches kept; re-run is idempotent.

- [ ] **Step 3: Dry-run against live sources**

Run: `python Backend/Scripts/import-oxford-ecdict.py --dry-run`
Expected: downloads (first time), prints Oxford count / ECDICT hits / drop categories / would-insert &gt; 0; no COUNT change

Verify:  
`docker exec nextword-postgres-1 psql -U nextword -d nextword -t -A -c 'SELECT COUNT(*) FROM "Words"'`  
unchanged before/after dry-run.

### Task 4: Real import + acceptance checks

- [ ] **Step 1: Record before count + lemma snapshot**

```bash
docker exec nextword-postgres-1 psql -U nextword -d nextword -t -A -c "SELECT COUNT(*) FROM \"Words\""
docker exec nextword-postgres-1 psql -U nextword -d nextword -t -A -c "SELECT COUNT(*) FROM \"WordScenarios\""
docker exec nextword-postgres-1 psql -U nextword -d nextword -t -A -c "SELECT lower(\"Lemma\") FROM \"Words\"" > /tmp/words-before-lemmas.txt
```

- [ ] **Step 2: Run import**

Run: `python Backend/Scripts/import-oxford-ecdict.py`
Expected: inserted ≈ 2k–4k (print actual); exit 0

- [ ] **Step 3: Acceptance SQL**

Sample **new** lemmas only (`after − before`). For ≥20 new rows: `Phonetics`/`CefrLevel` non-empty; `json.loads(Meanings)` is non-empty `list[str]`.

```sql
SELECT COUNT(*) FROM "Words";
SELECT "Lemma", COUNT(*) FROM "Words" GROUP BY "Lemma" HAVING COUNT(*) > 1;
SELECT COUNT(*) FROM "WordScenarios";
```

Also: `git status -- Backend/NextWord.Infrastructure/Data/wordlist-scenarios.json Backend/NextWord.Infrastructure/Migrations` must be clean (unchanged).

- [ ] **Step 4: Idempotency re-run**

Run: `python Backend/Scripts/import-oxford-ecdict.py --dry-run`
Expected: would-insert ≈ 0 (all remaining candidates already present or filtered)

- [ ] **Step 5: Commit script only (no cache files)**

```bash
git add Backend/Scripts/import-oxford-ecdict.py Backend/Scripts/test_import_oxford_ecdict.py
git commit -m "feat: import Oxford∩ECDICT words into development Words table"
```

Note: DB data is local only; do not dump Words into git.

---

## Chunk 3: Task registry note (optional light docs)

### Task 5: Register follow-up if needed

**Files:**
- Modify: `team/tasks.csv` only if opening a tracked task id for this work / daily-pad follow-up

- [ ] **Step 1: Append backlog row** (if team process requires) for「daily 短队列补齐 / force replan」as separate P2 — not blocking this import

- [ ] **Step 2: Skip CURRENT-STATE / development-log unless user asks** — AGENTS sync docs when feature lands; this is a one-shot local DB ops script; mention in commit body is enough unless PM asks for log entry

---

## Execution notes

- Postgres container name: `nextword-postgres-1` (compose default); if missing, `docker compose up -d postgres` from repo root.
- ECDICT download ~66MB — allow several minutes on slow networks.
- Never UPDATE existing lemmas.
- Do not modify `wordlist-scenarios.json` or migrations.
