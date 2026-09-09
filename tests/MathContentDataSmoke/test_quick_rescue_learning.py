#!/usr/bin/env python3
# -*- coding: utf-8 -*-
from __future__ import annotations

import hashlib
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RESCUE = ROOT / "content_packs" / "math_quick_rescue_v1" / "learning_content_v1.json"
BANK = ROOT / "content_packs" / "math_grade2_v1" / "question_bank_v1.json"
LESSONS = ROOT / "content_packs" / "math_grade2_v1" / "lesson_catalog_v1.json"
MANIFEST = ROOT / "content_packs" / "math_quick_rescue_v1" / "manifest.json"

FORBIDDEN_PRESSURE = (
    "nhanh lên",
    "hết giờ",
    "đếm ngược",
    "mất quà",
    "mất thưởng",
    "nếu con không",
    "thất bại",
    "kém",
    "sai rồi",
    "phải làm đủ",
)


class QuickRescueLearningContentSmoke(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pack = json.loads(RESCUE.read_text(encoding="utf-8"))
        cls.bank = json.loads(BANK.read_text(encoding="utf-8"))
        cls.lessons = json.loads(LESSONS.read_text(encoding="utf-8"))
        cls.q_by_id = {q["id"]: q for q in cls.bank["questions"]}
        cls.lesson_by_id = {x["id"]: x for x in cls.lessons["lessons"]}

    def test_manifest_hashes_learning_content_exactly(self):
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual("math_grade2_quick_rescue_learning_v1", manifest["pack_id"])
        self.assertEqual("VERIFIED", manifest["status"])
        self.assertEqual(1, len(manifest["files"]))
        self.assertEqual("learning_content_v1.json", manifest["files"][0]["path"])
        actual = hashlib.sha256(RESCUE.read_bytes()).hexdigest().upper()
        self.assertEqual(actual, manifest["files"][0]["sha256"])

    def test_three_checkpoint_pacing_is_basic_medium_application(self):
        self.assertEqual(1, self.pack["schema_version"])
        self.assertEqual("math_grade2_quick_rescue_learning_v1", self.pack["pack_id"])
        self.assertEqual(3, self.pack["checkpoint_count"])
        self.assertEqual(["basic", "medium", "application"], self.pack["pacing"])
        checkpoints = self.pack["checkpoints"]
        self.assertEqual([1, 2, 3], [x["number"] for x in checkpoints])
        self.assertEqual(self.pack["pacing"], [x["difficulty"] for x in checkpoints])

    def test_every_checkpoint_has_two_unique_authored_options_at_matching_difficulty(self):
        target_lesson = self.pack["target_lesson_id"]
        target_skill = self.pack["target_skill_id"]
        self.assertIn(target_lesson, self.lesson_by_id)
        self.assertEqual(target_skill, self.lesson_by_id[target_lesson]["skill_id"])
        seen = set()
        for checkpoint in self.pack["checkpoints"]:
            options = checkpoint["question_options"]
            self.assertGreaterEqual(len(options), 2)
            self.assertIn("support", {x["variant"] for x in options})
            self.assertIn("transfer", {x["variant"] for x in options})
            for option in options:
                qid = option["question_id"]
                self.assertNotIn(qid, seen)
                seen.add(qid)
                self.assertIn(qid, self.q_by_id)
                question = self.q_by_id[qid]
                self.assertEqual(target_lesson, question["lesson_id"])
                self.assertEqual(target_skill, question["skill_id"])
                self.assertEqual(checkpoint["difficulty"], question["difficulty"])

    def test_first_checkpoint_is_confidence_first_and_no_harder_than_basic(self):
        first = self.pack["checkpoints"][0]
        self.assertEqual("basic", first["difficulty"])
        self.assertIn("vị trí", first["goal_vi"].casefold())
        self.assertTrue(all(self.q_by_id[x["question_id"]]["difficulty"] == "basic" for x in first["question_options"]))

    def test_each_checkpoint_has_two_level_hints_repair_and_common_error_content(self):
        for checkpoint in self.pack["checkpoints"]:
            with self.subTest(checkpoint=checkpoint["number"]):
                self.assertTrue(checkpoint["hint_level_1_vi"].strip())
                self.assertTrue(checkpoint["hint_level_2_vi"].strip())
                self.assertTrue(checkpoint["repair_vi"].strip())
                self.assertGreaterEqual(len(checkpoint["common_errors"]), 2)
                self.assertEqual(len(checkpoint["common_errors"]), len({x["id"] for x in checkpoint["common_errors"]}))
                for error in checkpoint["common_errors"]:
                    self.assertTrue(error["cue_vi"].strip())
                    self.assertTrue(error["repair_vi"].strip())

    def test_adaptive_rules_cover_behavior_controller_states_and_are_safe(self):
        rules = {x["state"]: x for x in self.pack["adaptive_rules"]}
        self.assertEqual({
            "READY", "FLOW_LIKELY", "BORED_OR_UNDERCHALLENGED",
            "STRAINED", "FRUSTRATED_LIKELY", "FATIGUED_LIKELY"
        }, set(rules))
        self.assertEqual(0, rules["READY"]["hint_level"])
        self.assertTrue(rules["FLOW_LIKELY"]["minimal_feedback"])
        self.assertEqual("transfer", rules["BORED_OR_UNDERCHALLENGED"]["preferred_variant"])
        self.assertGreaterEqual(rules["STRAINED"]["hint_level"], 1)
        self.assertTrue(rules["FRUSTRATED_LIKELY"]["use_repair"])
        self.assertGreaterEqual(rules["FRUSTRATED_LIKELY"]["hint_level"], 2)
        self.assertTrue(rules["FATIGUED_LIKELY"]["offer_break"])
        self.assertFalse(rules["FATIGUED_LIKELY"]["select_question"])

    def test_anti_repeat_window_is_explicit_and_nontrivial(self):
        policy = self.pack["anti_repeat"]
        self.assertTrue(policy["prefer_unseen"])
        self.assertGreaterEqual(policy["recent_question_window"], 2)

    def test_child_facing_copy_has_no_pressure_shame_countdown_or_answer_leak(self):
        pieces = []
        pieces.extend(self.pack["feedback"].values())
        for cp in self.pack["checkpoints"]:
            pieces.extend([cp["label_vi"], cp["goal_vi"], cp["hint_level_1_vi"], cp["hint_level_2_vi"], cp["repair_vi"]])
            for error in cp["common_errors"]:
                pieces.extend([error["cue_vi"], error["repair_vi"]])
        all_text = "\n".join(pieces).casefold()
        for phrase in FORBIDDEN_PRESSURE:
            self.assertNotIn(phrase, all_text)
        self.assertNotRegex(all_text, r"\bđáp án\s+là\b")
        self.assertNotRegex(all_text, r"\b\d{3}\b")

    def test_wrong_feedback_normalizes_retry_without_shame(self):
        wrong = self.pack["feedback"]["wrong_vi"].casefold()
        self.assertIn("thử lại", wrong)
        self.assertNotIn("sai", wrong)
        self.assertNotIn("thất bại", wrong)

    def test_break_copy_preserves_progress_without_reward_pressure(self):
        text = self.pack["feedback"]["break_vi"].casefold()
        self.assertIn("nghỉ", text)
        self.assertIn("giữ", text)
        self.assertNotIn("thưởng", text)
        self.assertNotIn("quà", text)


if __name__ == "__main__":
    unittest.main()
