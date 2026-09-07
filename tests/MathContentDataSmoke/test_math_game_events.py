import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVENT_PATH = ROOT / "content_packs" / "math_grade2_v1" / "game_events_v1.json"
LESSON_PATH = ROOT / "content_packs" / "math_grade2_v1" / "lesson_catalog_v1.json"
MANIFEST_PATH = ROOT / "content_packs" / "math_grade2_v1" / "manifest.json"
GEN_PATH = ROOT / "tools" / "math_content_authoring" / "generate_game_events_v1.py"
VAL_PATH = ROOT / "tools" / "math_content_validator" / "validate_math_game_events.py"


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class MathGameEventsSmoke(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.events_root = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        cls.lesson_root = json.loads(LESSON_PATH.read_text(encoding="utf-8"))
        cls.manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        cls.generator = load_module("math_game_event_generator", GEN_PATH)
        cls.validator = load_module("math_game_event_validator", VAL_PATH)

    def test_first_five_event_catalog_shape(self):
        self.assertEqual(1, self.events_root["schema_version"])
        self.assertEqual("math_grade2_game_events_v1", self.events_root["catalog_id"])
        self.assertEqual("vi", self.events_root["language"])
        events = self.events_root["events"]
        self.assertEqual(5, len(events))
        self.assertEqual(self.validator.EXPECTED_FIRST_FIVE, [x["target_lesson_id"] for x in events])
        self.assertEqual(5, len({x["id"] for x in events}))

    def test_event_references_match_lesson_skills(self):
        lesson_by_id = {x["id"]: x for x in self.lesson_root["lessons"]}
        for event in self.events_root["events"]:
            with self.subTest(event=event["id"]):
                lesson = lesson_by_id[event["target_lesson_id"]]
                self.assertEqual(lesson["skill_id"], event["target_skill_id"])
                self.assertEqual(3, event["question_count"])
                self.assertEqual("quick_rescue", event["kind"])
                self.assertEqual("garden_progress", event["reward_presentation"])

    def test_event_checkpoints_are_three_distinct_child_facing_labels(self):
        for event in self.events_root["events"]:
            with self.subTest(event=event["id"]):
                checkpoints = event["checkpoint_nouns_vi"]
                self.assertEqual(3, len(checkpoints))
                self.assertEqual(3, len({x.casefold() for x in checkpoints}))
                self.assertTrue(all(0 < len(x) <= 42 for x in checkpoints))

    def test_event_copy_passes_psychology_and_readability_validator(self):
        errors, metrics = self.validator.validate(EVENT_PATH, LESSON_PATH)
        self.assertEqual([], errors)
        self.assertEqual(5, metrics["events"])
        self.assertEqual(5, metrics["covered_first_five"])

    def test_event_generator_matches_production_bytes(self):
        expected = self.generator.render(self.generator.build()).encode("utf-8")
        self.assertEqual(expected, EVENT_PATH.read_bytes())

    def test_event_manifest_entry_matches_sha(self):
        entries = [x for x in self.manifest["files"] if x["path"] == "game_events_v1.json"]
        self.assertEqual(1, len(entries))
        actual = hashlib.sha256(EVENT_PATH.read_bytes()).hexdigest().upper()
        self.assertEqual(actual, entries[0]["sha256"])

    def test_event_validator_rejects_dark_pattern_copy(self):
        broken = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        broken["events"][0]["intro_vi"] = "Nhanh lên, nếu con không làm sẽ mất quà."
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "events.json"
            path.write_text(json.dumps(broken, ensure_ascii=False), encoding="utf-8")
            errors, _ = self.validator.validate(path, LESSON_PATH)
        self.assertTrue(any("dark_pattern" in x for x in errors), errors)

    def test_break_copy_preserves_progress_without_reward_pressure(self):
        for event in self.events_root["events"]:
            text = event["break_copy_vi"].casefold()
            with self.subTest(event=event["id"]):
                self.assertTrue(("lưu" in text) or ("giữ nguyên" in text))
                self.assertNotIn("hoàn thành", text)
                self.assertNotIn("nhận thưởng", text)
                self.assertNotIn("mở khóa", text)

    def test_event_validator_rejects_break_copy_without_saved_progress(self):
        broken = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        broken["events"][0]["break_copy_vi"] = "Con nghỉ một chút nhé. Khi nào muốn mình quay lại tiếp tục."
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "events.json"
            path.write_text(json.dumps(broken, ensure_ascii=False), encoding="utf-8")
            errors, _ = self.validator.validate(path, LESSON_PATH)
        self.assertTrue(any("progress_not_preserved" in x for x in errors), errors)

    def test_event_validator_rejects_template_cloned_break_copy(self):
        broken = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        broken["events"][1]["break_copy_vi"] = broken["events"][0]["break_copy_vi"]
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "events.json"
            path.write_text(json.dumps(broken, ensure_ascii=False), encoding="utf-8")
            errors, _ = self.validator.validate(path, LESSON_PATH)
        self.assertTrue(any("break_copy_template_reuse" in x for x in errors), errors)

    def test_event_validator_rejects_generic_repair_copy(self):
        broken = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        broken["events"][2]["repair_copy_vi"] = "Mình thử lại một bước nhỏ nhé."
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "events.json"
            path.write_text(json.dumps(broken, ensure_ascii=False), encoding="utf-8")
            errors, _ = self.validator.validate(path, LESSON_PATH)
        self.assertTrue(any("repair_copy_vi:not_skill_specific" in x for x in errors), errors)

    def test_event_validator_rejects_pressure_intro_copy(self):
        broken = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        broken["events"][0]["intro_vi"] = "Con phải làm đủ ba câu để hoàn thành nhiệm vụ."
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "events.json"
            path.write_text(json.dumps(broken, ensure_ascii=False), encoding="utf-8")
            errors, _ = self.validator.validate(path, LESSON_PATH)
        self.assertTrue(any("pressure_copy" in x for x in errors), errors)

    def test_event_validator_requires_restorative_completion_copy(self):
        broken = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        broken["events"][3]["completion_vi"] = "Giỏi lắm! Con đã hoàn thành nhiệm vụ và nhận thưởng."
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "events.json"
            path.write_text(json.dumps(broken, ensure_ascii=False), encoding="utf-8")
            errors, _ = self.validator.validate(path, LESSON_PATH)
        self.assertTrue(any("completion_vi:not_restorative" in x for x in errors), errors)

    def test_event_validator_rejects_ambiguous_percent_wording(self):
        broken = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        broken["events"][4]["intro_vi"] = "Con giúp ghép lại phần trăm, chục và đơn vị nhé."
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "events.json"
            path.write_text(json.dumps(broken, ensure_ascii=False), encoding="utf-8")
            errors, _ = self.validator.validate(path, LESSON_PATH)
        self.assertTrue(any("ambiguous_math_copy:percent_vs_hundreds" in x for x in errors), errors)

    def test_event_validator_rejects_answer_leak_in_repair_copy(self):
        broken = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        broken["events"][1]["repair_copy_vi"] = "Mình nhìn hai chữ số cuối nhé; đáp án là: 300."
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "events.json"
            path.write_text(json.dumps(broken, ensure_ascii=False), encoding="utf-8")
            errors, _ = self.validator.validate(path, LESSON_PATH)
        self.assertTrue(any("repair_copy_vi:answer_leak" in x for x in errors), errors)

    def test_event_validator_rejects_wrong_lesson_skill_and_question_count(self):
        broken = json.loads(EVENT_PATH.read_text(encoding="utf-8"))
        broken["events"][1]["target_skill_id"] = "NUM_COUNT_READ_WRITE_0_1000"
        broken["events"][2]["question_count"] = 8
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "events.json"
            path.write_text(json.dumps(broken, ensure_ascii=False), encoding="utf-8")
            errors, _ = self.validator.validate(path, LESSON_PATH)
        self.assertTrue(any("skill_mismatch" in x for x in errors), errors)
        self.assertTrue(any("question_count" in x for x in errors), errors)


if __name__ == "__main__":
    unittest.main()
