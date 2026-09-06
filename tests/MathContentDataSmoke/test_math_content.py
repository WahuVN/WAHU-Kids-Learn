#!/usr/bin/env python3
# -*- coding: utf-8 -*-
from __future__ import annotations

import importlib.util
import json
import re
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
                self.assertGreaterEqual(len(q["tags"]), 5)
                self.assertIn(q["question_type"], q["tags"])
                self.assertIn(q["answer_kind"], q["tags"])
                self.assertTrue(q["accepted_answers"])
                kind = q["answer_kind"]
                if kind in {"integer", "interaction_integer"}:
                    self.assertIs(type(q["correct_answer"]), int)
                    lo = q["validation"]["numeric_min"]
                    hi = q["validation"]["numeric_max"]
                    self.assertLessEqual(lo, q["correct_answer"])
                    self.assertLessEqual(q["correct_answer"], hi)
                    self.assertIn(str(q["correct_answer"]), q["accepted_answers"])
                elif kind == "text":
                    choices = q["choices"]
                    ids = [c["id"] for c in choices]
                    self.assertIn(q["correct_choice_id"], ids)
                    self.assertEqual(len(ids), len(set(ids)))
                    self.assertEqual(len(choices), len({c["text"] for c in choices}))
                    self.assertTrue(all(c["rationale_vi"].strip() for c in choices))
                    correct_text = next(c["text"] for c in choices if c["id"] == q["correct_choice_id"])
                    self.assertEqual(correct_text, q["correct_answer"])
                    self.assertIn(correct_text, q["accepted_answers"])
                elif kind == "expression":
                    expected = validator.Fraction(q["validation"]["expected_numeric"], 1)
                    self.assertEqual(expected, validator.eval_restricted_expression(q["correct_answer"]))
                    for accepted in q["accepted_answers"]:
                        self.assertEqual(expected, validator.eval_restricted_expression(accepted))
                elif kind == "unit":
                    number, unit = validator.parse_unit_answer(q["correct_answer"])
                    self.assertEqual(validator.Fraction(q["validation"]["expected_numeric"], 1), number)
                    self.assertIn(unit, {x.lower().rstrip(".") for x in q["accepted_units"]})
                    self.assertIn(q["expected_unit"], q["accepted_units"])
                else:
                    self.fail(f"out-of-grade answer_kind: {kind}")

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
        chapter_position = {x["id"]: i for i, x in enumerate(self.catalog["chapters"])}
        topic_position = {x["id"]: i for i, x in enumerate(self.catalog["topics"])}
        lesson_by_skill = {x["skill_id"]: x for x in self.lessons}

        def order_key(lesson):
            return (chapter_position[lesson["chapter_id"]], topic_position[lesson["topic_id"]], lesson["order_in_domain"])

        for skill, prereqs in graph.items():
            self.assertNotIn(skill, prereqs)
            for prereq in prereqs:
                self.assertIn(prereq, self.skills)
                self.assertLess(order_key(lesson_by_skill[prereq]), order_key(lesson_by_skill[skill]))
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
        grade2_kinds = {"integer", "interaction_integer", "text", "unit", "expression"}
        self.assertEqual(grade2_kinds, set(kinds))
        self.assertEqual(validator.ENGINE_ANSWER_KINDS, set(self.bank["supported_answer_kinds"]))
        self.assertEqual(grade2_kinds, set(self.bank["grade2_used_answer_kinds"]))
        self.assertTrue(grade2_kinds.isdisjoint({"number", "decimal", "fraction"}))
        self.assertTrue(all(kinds[x] > 0 for x in grade2_kinds))

        question_types = Counter(q["question_type"] for q in self.questions)
        required_types = {"numeric_input", "multiple_choice", "true_false", "expression_input", "unit_input", "interactive_measurement", "word_problem"}
        self.assertEqual(required_types, set(question_types))
        self.assertEqual(required_types, set(self.bank["question_types"]))
        self.assertTrue(all(question_types[x] > 0 for x in required_types))

    def test_expression_validator_fails_closed(self):
        self.assertEqual(validator.Fraction(75, 1), validator.eval_restricted_expression("100 - 30 + 5"))
        with self.assertRaises(ZeroDivisionError):
            validator.eval_restricted_expression("8 / (3 - 3)")
        with self.assertRaises((SyntaxError, ValueError)):
            validator.eval_restricted_expression("2 + foo")
        with self.assertRaises((SyntaxError, ValueError)):
            validator.eval_restricted_expression("__import__('os').system('echo bad')")

    def test_money_content_does_not_hardcode_unsourced_denomination(self):
        self.assertIsNotNone(validator.MONEY_DENOMINATION_RE.search("10 000 đồng"))
        money_lessons = [x for x in self.lessons if x["skill_id"] == "MONEY_VND_NOTE_RECOGNITION"]
        money_questions = [x for x in self.questions if x["skill_id"] == "MONEY_VND_NOTE_RECOGNITION"]
        self.assertEqual(1, len(money_lessons))
        self.assertEqual(3, len(money_questions))
        for item in money_lessons + money_questions:
            with self.subTest(item=item["id"]):
                serialized = json.dumps(item, ensure_ascii=False)
                self.assertIsNone(validator.MONEY_DENOMINATION_RE.search(serialized))

    def test_time_relation_skills_do_not_expand_into_extra_arithmetic(self):
        time_questions = [x for x in self.questions if x["skill_id"] in validator.TIME_RELATION_SKILLS]
        self.assertEqual(6, len(time_questions))
        for item in time_questions:
            with self.subTest(item=item["id"]):
                serialized = json.dumps(item, ensure_ascii=False)
                self.assertIsNone(validator.TIME_OUT_OF_SCOPE_ARITH_RE.search(serialized))

    def test_mul_div_literals_stay_inside_tables_2_and_5(self):
        self.assertEqual([], validator.grade2_operation_scope_violations("2 × 10; 4 × 5; 20 : 2; 50 : 5"))
        self.assertEqual([], validator.grade2_operation_scope_violations("Giữ hàng chục bằng 0: 400 + 0 + 9 = 409."))
        for sample, marker in (
            ("36 × 9", "mul:36x9"),
            ("30 × 5", "mul:30x5"),
            ("10 : 3", "div:10:3"),
            ("5 : 2", "div:5:2"),
            ("60 : 5", "div:60:5"),
        ):
            self.assertIn(marker, validator.grade2_operation_scope_violations(sample))
        for item in self.lessons + self.questions:
            with self.subTest(item=item["id"]):
                serialized = json.dumps(item, ensure_ascii=False)
                self.assertEqual([], validator.grade2_operation_scope_violations(serialized))

    def test_integer_answer_unit_is_display_only_metadata(self):
        unit_questions = [x for x in self.questions if "answer_unit" in x]
        self.assertEqual(23, len(unit_questions))
        self.assertEqual({"cm", "kg", "l", "dm", "m", "ngày", "giờ", "phút"}, {x["answer_unit"] for x in unit_questions})
        for item in unit_questions:
            with self.subTest(item=item["id"]):
                self.assertEqual("integer", item["answer_kind"])
                self.assertIn(item["question_type"], {"numeric_input", "word_problem"})
                self.assertIs(type(item["correct_answer"]), int)
                self.assertIn(str(item["correct_answer"]), item["accepted_answers"])

    def test_correct_choice_positions_are_balanced(self):
        choice_questions = [x for x in self.questions if x.get("choices")]
        by_count = {}
        for item in choice_questions:
            by_count.setdefault(len(item["choices"]), []).append(item)
        self.assertEqual({2, 4}, set(by_count))
        self.assertEqual(3, len(by_count[2]))
        self.assertEqual(88, len(by_count[4]))
        for choice_count, items in by_count.items():
            with self.subTest(choice_count=choice_count):
                positions = Counter(ord(item["correct_choice_id"]) - ord("a") for item in items)
                counts = [positions[index] for index in range(choice_count)]
                self.assertTrue(all(value > 0 for value in counts))
                self.assertLessEqual(max(counts) - min(counts), 1)
        self.assertEqual([22, 22, 22, 22], [Counter(x["correct_choice_id"] for x in by_count[4])[key] for key in "abcd"])

    def test_multiple_choice_options_are_semantically_distinct(self):
        self.assertEqual("có thể", validator.normalize_choice_text("  CÓ   THỂ "))
        self.assertEqual(validator.Fraction(352, 1), validator.try_eval_numeric_choice("300 + 52"))
        self.assertEqual(validator.Fraction(352, 1), validator.try_eval_numeric_choice("300 + 50 + 2"))
        for item in self.questions:
            choices = item.get("choices", [])
            if not choices:
                continue
            with self.subTest(item=item["id"]):
                normalized = [validator.normalize_choice_text(c["text"]) for c in choices]
                self.assertEqual(len(normalized), len(set(normalized)))
                numeric_values = [validator.try_eval_numeric_choice(c["text"]) for c in choices]
                numeric_values = [x for x in numeric_values if x is not None]
                self.assertEqual(len(numeric_values), len(set(numeric_values)))

    def test_written_add_sub_transfer_counts_match_skill_contract(self):
        constrained = [
            x for x in self.questions
            if x["skill_id"] in validator.ADD_CARRY_RULES or x["skill_id"] in validator.SUB_BORROW_RULES
        ]
        self.assertEqual(12, len(constrained))
        for item in constrained:
            with self.subTest(item=item["id"]):
                operands = [int(x) for x in re.findall(r"\d+", item["prompt_vi"])]
                self.assertGreaterEqual(len(operands), 2)
                a, b = operands[0], operands[1]
                if item["skill_id"] in validator.ADD_CARRY_RULES:
                    self.assertEqual(a + b, item["correct_answer"])
                    self.assertEqual(validator.ADD_CARRY_RULES[item["skill_id"]], validator.addition_carry_count(a, b))
                else:
                    self.assertEqual(a - b, item["correct_answer"])
                    self.assertEqual(validator.SUB_BORROW_RULES[item["skill_id"]], validator.subtraction_borrow_count(a, b))


if __name__ == "__main__":
    unittest.main(verbosity=2)
