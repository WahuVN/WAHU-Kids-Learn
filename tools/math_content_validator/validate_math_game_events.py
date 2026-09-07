from __future__ import annotations

import argparse
import json
import re
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_EVENTS = ROOT / "content_packs" / "math_grade2_v1" / "game_events_v1.json"
DEFAULT_LESSONS = ROOT / "content_packs" / "math_grade2_v1" / "lesson_catalog_v1.json"

EXPECTED_THEME_BY_LESSON = {
    "m2_ls_num_count_read_write_0_1000": "forest_path",
    "m2_ls_num_full_hundreds_recognize": "hundred_station",
    "m2_ls_num_predecessor_successor": "number_path",
    "m2_ls_place_value_hundreds_tens_ones": "place_value_workshop",
    "m2_ls_num_expanded_form_hto": "number_machine",
}
CHECKPOINT_REQUIRED_PATTERN_BY_THEME = {
    "forest_path": r"biển\s+số",
    "hundred_station": r"trạm",
    "number_path": r"đoạn",
    "place_value_workshop": r"ngăn\s+hàng",
    "number_machine": r"bộ\s+phận",
}

EXPECTED_FIRST_FIVE = [
    "m2_ls_num_count_read_write_0_1000",
    "m2_ls_num_full_hundreds_recognize",
    "m2_ls_num_predecessor_successor",
    "m2_ls_place_value_hundreds_tens_ones",
    "m2_ls_num_expanded_form_hto",
]
EVENT_KEYS = {
    "id", "kind", "title_vi", "intro_vi", "completion_vi", "target_lesson_id", "target_skill_id",
    "question_count", "checkpoint_nouns_vi", "theme", "repair_copy_vi", "break_copy_vi", "reward_presentation",
}
TEXT_LIMITS = {
    "title_vi": 60,
    "intro_vi": 180,
    "completion_vi": 180,
    "repair_copy_vi": 180,
    "break_copy_vi": 180,
}
BANNED_PATTERNS = [
    r"hết\s+giờ", r"còn\s+\d+\s*(?:giây|phút)", r"nếu\s+con\s+không", r"mất\s+(?:quà|thưởng|chuỗi)",
    r"khóc", r"sắp\s+chết", r"bỏ\s+rơi", r"nguy\s+hiểm", r"nhanh\s+lên", r"bị\s+phạt",
    r"countdown", r"streak", r"leaderboard", r"gacha", r"loot\s*box", r"daily\s+chest",
]
SAFE_ID = re.compile(r"^[a-z0-9][a-z0-9_]{2,79}$")
SAFE_THEME = re.compile(r"^[a-z0-9][a-z0-9_]{2,47}$")
AMBIGUOUS_MATH_PATTERNS = ((r"\bphần\s+trăm\b", "percent_vs_hundreds"),)
PRESSURE_PATTERNS = (
    r"\bcon\s+phải\b", r"phải\s+làm\s+(?:đủ|hết)", r"chỉ\s+còn\s+\d+", r"cố\s+lên\s+để\s+nhận",
)
COMPLETION_REQUIRED_PATTERNS_BY_THEME = {
    "forest_path": (r"đúng\s+chỗ", r"rõ\s+ràng"),
    "hundred_station": (r"nhãn", r"rõ\s+ràng"),
    "number_path": (r"đúng\s+thứ\s+tự", r"liền\s+mạch"),
    "place_value_workshop": (r"đúng\s+ngăn", r"gọn"),
    "number_machine": (r"đúng\s+giá\s+trị", r"chạy\s+êm"),
}
BREAK_PROGRESS_PATTERNS = (r"\blưu\b", r"giữ\s+nguyên")
BREAK_PROMISE_PATTERNS = (r"hoàn\s+thành", r"nhận\s+(?:quà|thưởng)", r"mở\s+khóa", r"được\s+thưởng")
MAX_BREAK_TOKEN_JACCARD = 0.60
REPAIR_REQUIRED_PATTERNS_BY_SKILL = {
    "NUM_COUNT_READ_WRITE_0_1000": (r"\bhàng\b",),
    "NUM_FULL_HUNDREDS_RECOGNIZE": (r"hai\s+chữ\s+số\s+cuối",),
    "NUM_PREDECESSOR_SUCCESSOR": (r"bớt\s+1", r"thêm\s+1"),
    "PLACE_VALUE_HUNDREDS_TENS_ONES": (r"vị\s+trí", r"\bhàng\b"),
    "NUM_EXPANDED_FORM_HTO": (r"tách", r"từng\s+hàng"),
}
REPAIR_ANSWER_LEAK_PATTERNS = (
    r"đáp\s+án\s*(?:là|:)", r"câu\s+trả\s+lời\s*(?:là|:)",
    r"chọn\s+đáp\s+án\s+[a-d0-9]", r"=\s*\d+",
)


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def bad_text_reason(text: object, limit: int | None = None) -> str | None:
    if not isinstance(text, str) or not text.strip():
        return "missing"
    if text != text.strip():
        return "outer_whitespace"
    if unicodedata.normalize("NFC", text) != text:
        return "not_nfc"
    if any(unicodedata.category(ch).startswith("C") for ch in text):
        return "control_character"
    if limit is not None and len(text) > limit:
        return f"too_long_{len(text)}_{limit}"
    lowered = text.casefold()
    for pattern in BANNED_PATTERNS:
        if re.search(pattern, lowered, re.IGNORECASE):
            return "dark_pattern_" + pattern.replace("\\s", "s")[:36]
    return None


def validate(events_path: Path = DEFAULT_EVENTS, lessons_path: Path = DEFAULT_LESSONS):
    errors = []
    try:
        root = load_json(events_path)
    except Exception as ex:
        return [f"game_events_parse_error:{type(ex).__name__}"], {"events": 0}
    try:
        lesson_root = load_json(lessons_path)
    except Exception as ex:
        return [f"lesson_catalog_parse_error:{type(ex).__name__}"], {"events": 0}

    if not isinstance(root, dict):
        return ["game_events_root_not_object"], {"events": 0}
    if set(root) != {"schema_version", "catalog_id", "language", "events"}:
        errors.append(f"game_events_root_keys:{sorted(root)}")
    if root.get("schema_version") != 1:
        errors.append("game_events_schema_version")
    if root.get("catalog_id") != "math_grade2_game_events_v1":
        errors.append("game_events_catalog_id")
    if root.get("language") != "vi":
        errors.append("game_events_language")

    lessons = lesson_root.get("lessons") if isinstance(lesson_root, dict) else None
    lesson_by_id = {x.get("id"): x for x in lessons or [] if isinstance(x, dict) and isinstance(x.get("id"), str)}
    events = root.get("events")
    if not isinstance(events, list):
        errors.append("game_events_list_missing")
        events = []
    if len(events) != 5:
        errors.append(f"game_events_v1_requires_exact_first_five:{len(events)}")

    seen_ids = set()
    seen_lessons = []
    break_texts = []
    for index, event in enumerate(events):
        where = f"event[{index}]"
        if not isinstance(event, dict):
            errors.append(f"{where}:not_object")
            continue
        if set(event) != EVENT_KEYS:
            errors.append(f"{where}:keys:{sorted(event)}")
        event_id = event.get("id")
        if not isinstance(event_id, str) or not SAFE_ID.fullmatch(event_id):
            errors.append(f"{where}:id_invalid")
        elif event_id in seen_ids:
            errors.append(f"{where}:id_duplicate:{event_id}")
        else:
            seen_ids.add(event_id)
        if event.get("kind") != "quick_rescue":
            errors.append(f"{where}:kind")
        if event.get("question_count") != 3:
            errors.append(f"{where}:question_count")
        if event.get("reward_presentation") != "garden_progress":
            errors.append(f"{where}:reward_presentation")
        theme = event.get("theme")
        if not isinstance(theme, str) or not SAFE_THEME.fullmatch(theme):
            errors.append(f"{where}:theme")

        for key, limit in TEXT_LIMITS.items():
            value = event.get(key)
            reason = bad_text_reason(value, limit)
            if reason:
                errors.append(f"{where}:{key}:{reason}")
            if isinstance(value, str):
                lowered_value = value.casefold()
                for pattern, label in AMBIGUOUS_MATH_PATTERNS:
                    if re.search(pattern, lowered_value, re.IGNORECASE):
                        errors.append(f"{where}:{key}:ambiguous_math_copy:{label}")
                if any(re.search(pattern, lowered_value, re.IGNORECASE) for pattern in PRESSURE_PATTERNS):
                    errors.append(f"{where}:{key}:pressure_copy")

        completion_text = event.get("completion_vi")
        theme_for_completion = event.get("theme")
        required_completion_patterns = COMPLETION_REQUIRED_PATTERNS_BY_THEME.get(theme_for_completion, ())
        if isinstance(completion_text, str) and required_completion_patterns:
            lowered_completion = completion_text.casefold()
            if not all(re.search(pattern, lowered_completion, re.IGNORECASE) for pattern in required_completion_patterns):
                errors.append(f"{where}:completion_vi:not_restorative:{theme_for_completion}")

        break_text = event.get("break_copy_vi")
        if isinstance(break_text, str):
            lowered_break = break_text.casefold()
            if not any(re.search(pattern, lowered_break, re.IGNORECASE) for pattern in BREAK_PROGRESS_PATTERNS):
                errors.append(f"{where}:break_copy_vi:progress_not_preserved")
            if any(re.search(pattern, lowered_break, re.IGNORECASE) for pattern in BREAK_PROMISE_PATTERNS):
                errors.append(f"{where}:break_copy_vi:completion_or_reward_promise")
            break_texts.append((where, break_text))

        repair_text = event.get("repair_copy_vi")
        repair_skill = event.get("target_skill_id")
        if isinstance(repair_text, str):
            lowered_repair = repair_text.casefold()
            required_patterns = REPAIR_REQUIRED_PATTERNS_BY_SKILL.get(repair_skill, ())
            if required_patterns and not all(re.search(pattern, lowered_repair, re.IGNORECASE) for pattern in required_patterns):
                errors.append(f"{where}:repair_copy_vi:not_skill_specific:{repair_skill}")
            if any(re.search(pattern, lowered_repair, re.IGNORECASE) for pattern in REPAIR_ANSWER_LEAK_PATTERNS):
                errors.append(f"{where}:repair_copy_vi:answer_leak")

        checkpoints = event.get("checkpoint_nouns_vi")
        if not isinstance(checkpoints, list) or len(checkpoints) != 3:
            errors.append(f"{where}:checkpoints_count")
        else:
            normalized = []
            for cp_index, value in enumerate(checkpoints):
                reason = bad_text_reason(value, 42)
                if reason:
                    errors.append(f"{where}:checkpoint[{cp_index}]:{reason}")
                if isinstance(value, str):
                    normalized.append(value.strip().casefold())
            if len(set(normalized)) != len(normalized):
                errors.append(f"{where}:checkpoint_duplicate")

        lesson_id = event.get("target_lesson_id")
        skill_id = event.get("target_skill_id")
        seen_lessons.append(lesson_id)
        expected_theme = EXPECTED_THEME_BY_LESSON.get(lesson_id)
        if expected_theme is not None and theme != expected_theme:
            errors.append(f"{where}:theme_mismatch:{theme}:{expected_theme}")
        checkpoint_pattern = CHECKPOINT_REQUIRED_PATTERN_BY_THEME.get(theme)
        if checkpoint_pattern and isinstance(checkpoints, list):
            for cp_index, value in enumerate(checkpoints):
                if isinstance(value, str) and not re.search(checkpoint_pattern, value.casefold(), re.IGNORECASE):
                    errors.append(f"{where}:checkpoint[{cp_index}]:theme_mismatch:{theme}")
        lesson = lesson_by_id.get(lesson_id)
        if lesson is None:
            errors.append(f"{where}:unknown_lesson:{lesson_id}")
        elif lesson.get("skill_id") != skill_id:
            errors.append(f"{where}:skill_mismatch:{skill_id}:{lesson.get('skill_id')}")

    for a in range(len(break_texts)):
        where_a, text_a = break_texts[a]
        tokens_a = set(re.findall(r"\w+", text_a.casefold(), re.UNICODE))
        for b in range(a + 1, len(break_texts)):
            where_b, text_b = break_texts[b]
            tokens_b = set(re.findall(r"\w+", text_b.casefold(), re.UNICODE))
            similarity = len(tokens_a & tokens_b) / max(1, len(tokens_a | tokens_b))
            if similarity > MAX_BREAK_TOKEN_JACCARD:
                errors.append(f"break_copy_template_reuse:{where_a}:{where_b}:{similarity:.3f}")

    if seen_lessons != EXPECTED_FIRST_FIVE:
        errors.append("game_events_first_five_order_or_coverage:" + "|".join(str(x) for x in seen_lessons))

    metrics = {
        "events": len(events),
        "covered_first_five": sum(1 for lesson_id in EXPECTED_FIRST_FIVE if lesson_id in seen_lessons),
    }
    return errors, metrics


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--events", type=Path, default=DEFAULT_EVENTS)
    parser.add_argument("--lessons", type=Path, default=DEFAULT_LESSONS)
    parser.add_argument("--json", action="store_true", dest="as_json")
    args = parser.parse_args()
    errors, metrics = validate(args.events, args.lessons)
    result = {"ok": not errors, "errors": errors, "metrics": metrics}
    if args.as_json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    elif errors:
        print(f"MATH_GAME_EVENTS_INVALID errors={len(errors)}")
        for error in errors:
            print("ERROR " + error)
    else:
        print(f"MATH_GAME_EVENTS_VALID events={metrics['events']} covered_first_five={metrics['covered_first_five']}")
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
