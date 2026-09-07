#!/usr/bin/env python3
# -*- coding: utf-8 -*-
from __future__ import annotations

import importlib.util
import json
import sys
import unittest
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DRAFT_ROOT = ROOT / "tools" / "math_content_authoring" / "drafts"
VALIDATOR_PATH = ROOT / "tools" / "math_content_validator" / "validate_math_pool6_draft.py"
BUILDER_PATH = DRAFT_ROOT / "generate_pool6_preview.py"
RUNTIME_BANK_PATH = ROOT / "content_packs" / "math_grade2_v1" / "question_bank_v1.json"
PREVIEW_BANK_PATH = DRAFT_ROOT / "pool6_preview" / "question_bank_pool6_preview.json"
PREVIEW_CATALOG_PATH = DRAFT_ROOT / "pool6_preview" / "lesson_catalog_pool6_preview.json"
DIFFICULTIES = ("basic", "medium", "application")


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, str(path))
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


class MathPool6DraftSmoke(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.validator = load_module("math_pool6_draft_validator_test", VALIDATOR_PATH)
        cls.builder = load_module("math_pool6_draft_builder_test", BUILDER_PATH)
        cls.runtime_bank = json.loads(RUNTIME_BANK_PATH.read_text(encoding="utf-8"))
        cls.preview_bank = json.loads(PREVIEW_BANK_PATH.read_text(encoding="utf-8"))
        cls.preview_catalog = json.loads(PREVIEW_CATALOG_PATH.read_text(encoding="utf-8"))
        cls.built_catalog, cls.built_bank, cls.draft_questions = cls.builder.build_preview()

    def test_strict_draft_validator_clean(self):
        errors, metrics = self.validator.validate()
        self.assertEqual([], errors)
        self.assertEqual(201, metrics["runtime_questions"])
        self.assertEqual(201, metrics["draft_questions"])
        self.assertEqual(402, metrics["preview_questions"])
        self.assertEqual(67, metrics["preview_lessons"])

    def test_preview_rebuild_is_deterministic(self):
        self.assertEqual(self.preview_catalog, self.built_catalog)
        self.assertEqual(self.preview_bank, self.built_bank)

    def test_runtime_bank_remains_201_and_is_exact_preview_prefix(self):
        self.assertEqual(201, len(self.runtime_bank["questions"]))
        self.assertEqual(self.runtime_bank["questions"], self.preview_bank["questions"][:201])

    def test_exactly_201_future_questions_cover_all_67_skills(self):
        by_skill = defaultdict(list)
        for question in self.draft_questions:
            by_skill[question["skill_id"]].append(question)
        self.assertEqual(201, len(self.draft_questions))
        self.assertEqual(67, len(by_skill))
        for skill, questions in by_skill.items():
            self.assertEqual(3, len(questions), skill)
            self.assertEqual(Counter({difficulty: 1 for difficulty in DIFFICULTIES}),
                             Counter(q["difficulty"] for q in questions), skill)

    def test_future_ids_are_04_05_06_and_combined_pool_is_contiguous(self):
        expected = {"basic": 4, "medium": 5, "application": 6}
        for question in self.draft_questions:
            self.assertTrue(question["id"].endswith(f"_{expected[question['difficulty']]:02d}"), question["id"])
        by_skill = defaultdict(list)
        for question in self.preview_bank["questions"]:
            by_skill[question["skill_id"]].append(int(question["id"].rsplit("_", 1)[1]))
        self.assertEqual(67, len(by_skill))
        for skill, ordinals in by_skill.items():
            self.assertEqual([1, 2, 3, 4, 5, 6], sorted(ordinals), skill)

    def test_every_preview_lesson_has_two_questions_per_difficulty(self):
        referenced = Counter()
        for lesson in self.preview_catalog["lessons"]:
            for difficulty in DIFFICULTIES:
                refs = lesson["practice_sets"][difficulty]
                self.assertEqual(2, len(refs), (lesson["id"], difficulty))
                referenced.update(refs)
        self.assertEqual(402, len(referenced))
        self.assertTrue(all(count == 1 for count in referenced.values()))

    def test_all_402_questions_have_unique_ids_and_prompts_within_each_lesson(self):
        questions = self.preview_bank["questions"]
        self.assertEqual(402, len({q["id"] for q in questions}))
        by_lesson = defaultdict(list)
        for question in questions:
            by_lesson[question["lesson_id"]].append(" ".join(question["prompt_vi"].split()).casefold())
        for lesson_id, prompts in by_lesson.items():
            self.assertEqual(len(prompts), len(set(prompts)), lesson_id)

    def test_difficulty_distribution_doubles_runtime_evenly(self):
        counts = Counter(q["difficulty"] for q in self.preview_bank["questions"])
        self.assertEqual({"basic": 134, "medium": 134, "application": 134}, dict(counts))


if __name__ == "__main__":
    unittest.main(verbosity=2)
