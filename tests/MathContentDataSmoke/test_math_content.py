#!/usr/bin/env python3
# -*- coding: utf-8 -*-
from __future__ import annotations

import importlib.util
import json
import sys
import unittest
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / "curriculum" / "math_grade2" / "moet_baseline_v1.json"
LESSONS = ROOT / "content_packs" / "math_grade2_v1" / "lesson_catalog_v1.json"
QUESTIONS = ROOT / "content_packs" / "math_grade2_v1" / "question_bank_v1.json"
VALIDATOR = ROOT / "tools" / "math_content_validator" / "validate_math_content.py"
AUTHORING = ROOT / "tools" / "math_content_authoring" / "generate_grade2_content.py"


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, str(path))
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


validator = load_module("math_content_validator", VALIDATOR)
authoring = load_module("math_content_authoring", AUTHORING)


class MathContentDataSmoke(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.baseline = json.loads(BASELINE.read_text(encoding="utf-8"))
        cls.catalog = json.loads(LESSONS.read_text(encoding="utf-8"))
        cls.bank = json.loads(QUESTIONS.read_text(encoding="utf-8"))
        cls.lessons = cls.catalog["lessons"]
        cls.questions = cls.bank["questions"]
        cls.lesson_by_id = {x["id"]: x for x in cls.lessons}
        cls.question_by_id = {x["id"]: x for x in cls.questions}
        cls.skills = {s for values in cls.baseline["domains"].values() for s in values}

    def test_semantic_validator_clean(self):
        errors, metrics = validator.validate(BASELINE, LESSONS, QUESTIONS)
        self.assertEqual([], errors)
        self.assertEqual(67, metrics["lessons"])
        self.assertEqual(201, metrics["questions"])
        self.assertEqual(201, metrics["valid_questions"])

    def test_every_baseline_skill_has_exactly_one_loadable_lesson(self):
        self.assertEqual(67, len(self.skills))
        self.assertEqual(67, len(self.lessons))
        counts = Counter(x["skill_id"] for x in self.lessons)
        self.assertEqual(self.skills, set(counts))
        self.assertTrue(all(v == 1 for v in counts.values()))

    def test_every_lesson_has_instructional_content(self):
        for lesson in self.lessons:
            with self.subTest(lesson=lesson["id"]):
                self.assertGreaterEqual(len(lesson["objectives_vi"]), 2)
                self.assertTrue(lesson["explanation_vi"].strip())
                self.assertGreaterEqual(len(lesson["concepts"]), 1)
                self.assertGreaterEqual(len(lesson["worked_examples"]), 1)
                example = lesson["worked_examples"][0]
                self.assertTrue(example["prompt_vi"].strip())
                self.assertTrue(example["answer"].strip())
                self.assertGreaterEqual(len(example["solution_steps_vi"]), 1)

    def test_every_lesson_has_basic_medium_application(self):
        for lesson in self.lessons:
            with self.subTest(lesson=lesson["id"]):
                self.assertEqual({"basic", "medium", "application"}, set(lesson["practice_sets"]))
                for difficulty in ("basic", "medium", "application"):
                    refs = lesson["practice_sets"][difficulty]
                    self.assertGreaterEqual(len(refs), 1)
                    for qid in refs:
                        self.assertIn(qid, self.question_by_id)
                        q = self.question_by_id[qid]
                        self.assertEqual(lesson["id"], q["lesson_id"])
                        self.assertEqual(difficulty, q["difficulty"])

    def test_every_question_has_stable_source_and_valid_answer(self):
        self.assertEqual(201, len(self.questions))
        self.assertEqual(201, len({q["id"] for q in self.questions}))
        for q in self.questions:
            with self.subTest(question=q["id"]):
                self.assertTrue(q["id"].startswith("m2_q_"))
                self.assertIn(q["lesson_id"], self.lesson_by_id)
                self.assertIn(q["skill_id"], self.skills)
                self.assertEqual(q["skill_id"], self.lesson_by_id[q["lesson_id"]]["skill_id"])
                self.assertTrue(q["prompt_vi"].strip())
                self.assertTrue(q["explanation_vi"].strip())
                self.assertGreaterEqual(len(q["hints_vi"]), 2)
                self.assertGreaterEqual(len(q["tags"]), 3)
                self.assertTrue(q["accepted_answers"])
                if q["answer_kind"] == "numeric_input":
                    self.assertIs(type(q["correct_answer"]), int)
                    lo = q["validation"]["numeric_min"]
                    hi = q["validation"]["numeric_max"]
                    self.assertLessEqual(lo, q["correct_answer"])
                    self.assertLessEqual(q["correct_answer"], hi)
                    self.assertIn(str(q["correct_answer"]), q["accepted_answers"])
                elif q["answer_kind"] == "multiple_choice":
                    choices = q["choices"]
                    ids = [c["id"] for c in choices]
                    self.assertIn(q["correct_answer"], ids)
                    self.assertEqual(len(ids), len(set(ids)))
                    self.assertEqual(len(choices), len({c["text"] for c in choices}))
                    self.assertTrue(all(c["rationale_vi"].strip() for c in choices))
                    correct_text = next(c["text"] for c in choices if c["id"] == q["correct_answer"])
                    self.assertIn(correct_text, q["accepted_answers"])
                else:
                    self.fail(f"unsupported answer_kind: {q['answer_kind']}")

    def test_no_orphan_question_and_every_question_referenced_once(self):
        refs = []
        for lesson in self.lessons:
            for values in lesson["practice_sets"].values():
                refs.extend(values)
        counts = Counter(refs)
        self.assertEqual(set(self.question_by_id), set(counts))
        self.assertTrue(all(v == 1 for v in counts.values()))

    def test_prerequisites_resolve_and_are_acyclic(self):
        graph = {x["skill_id"]: x["prerequisite_skills"] for x in self.lessons}
        for skill, prereqs in graph.items():
            self.assertNotIn(skill, prereqs)
            for prereq in prereqs:
                self.assertIn(prereq, self.skills)
        self.assertIsNone(validator.find_cycle(graph))

    def test_balanced_difficulty_coverage(self):
        counts = Counter(q["difficulty"] for q in self.questions)
        self.assertEqual({"basic": 67, "medium": 67, "application": 67}, dict(counts))
        by_skill = Counter(q["skill_id"] for q in self.questions)
        self.assertTrue(all(by_skill[s] == 3 for s in self.skills))

    def test_authoring_is_deterministic_and_matches_committed_json(self):
        catalog, bank = authoring.build()
        self.assertEqual(self.catalog, catalog)
        self.assertEqual(self.bank, bank)

    def test_required_answer_kinds_only(self):
        kinds = Counter(q["answer_kind"] for q in self.questions)
        self.assertEqual({"numeric_input", "multiple_choice"}, set(kinds))
        self.assertGreater(kinds["numeric_input"], 0)
        self.assertGreater(kinds["multiple_choice"], 0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
