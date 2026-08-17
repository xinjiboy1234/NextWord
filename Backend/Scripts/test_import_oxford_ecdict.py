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

    def test_all_invalid_returns_none(self):
        self.assertIsNone(mod.lowest_cefr(["xx", ""]))


class MeaningsTests(unittest.TestCase):
    def test_keeps_cjk_drops_ascii(self):
        raw = "n. memory\n记忆\n回忆\njust english"
        self.assertEqual(mod.split_meanings(raw), ["记忆", "回忆"])

    def test_empty_when_no_cjk(self):
        self.assertEqual(mod.split_meanings("n. foo\nbar"), [])

    def test_cr_split_prefix_dedupe_max5(self):
        raw = "n. 一\r一\r二\r三\r四\r五\r六"
        self.assertEqual(mod.split_meanings(raw), ["一", "二", "三", "四", "五"])


class PosTests(unittest.TestCase):
    def test_weighted_pos(self):
        self.assertEqual(mod.map_pos("n:10/v:5", "memory"), "n.")

    def test_phrase_fallback(self):
        self.assertEqual(mod.map_pos("", "get up"), "phr.")


class PreferTests(unittest.TestCase):
    def test_prefer_oxford_flag(self):
        a = {"oxford": "", "collins": "5", "translation": "短", "phonetic": "a", "pos": "n"}
        b = {"oxford": "1", "collins": "1", "translation": "更长一些", "phonetic": "b", "pos": "n"}
        self.assertIs(mod.prefer_ecdict_row(a, b), b)

    def test_prefer_longer_translation_when_tied(self):
        a = {"oxford": "", "collins": "1", "translation": "短", "phonetic": "a", "pos": "n"}
        b = {"oxford": "", "collins": "1", "translation": "更长的释义", "phonetic": "b", "pos": "n"}
        self.assertIs(mod.prefer_ecdict_row(a, b), b)


class PhoneticsTests(unittest.TestCase):
    def test_wrap_slashes(self):
        self.assertEqual(mod.format_phonetics("ˈmeməri"), "/ˈmeməri/")

    def test_truncate_120(self):
        long = "x" * 200
        self.assertEqual(len(mod.format_phonetics(long)), 120)


class DifficultyTests(unittest.TestCase):
    def test_bands(self):
        self.assertEqual(mod.difficulty_for("A2"), "Basic")
        self.assertEqual(mod.difficulty_for("B1"), "Intermediate")
        self.assertEqual(mod.difficulty_for("C1"), "Advanced")


if __name__ == "__main__":
    unittest.main()
