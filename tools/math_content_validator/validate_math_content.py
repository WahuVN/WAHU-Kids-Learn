#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Semantic validator for Grade-2 Math static lesson/question content."""
from __future__ import annotations

import argparse
import ast
import difflib
import json
import re
import sys
import unicodedata
from collections import Counter, defaultdict
from fractions import Fraction
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_BASELINE = ROOT / "curriculum" / "math_grade2" / "moet_baseline_v1.json"
DEFAULT_LESSONS = ROOT / "content_packs" / "math_grade2_v1" / "lesson_catalog_v1.json"
DEFAULT_QUESTIONS = ROOT / "content_packs" / "math_grade2_v1" / "question_bank_v1.json"

ID_RE = re.compile(r"^[a-z0-9][a-z0-9_]{2,127}$")
EXPECTED_CURRICULUM_ID = "vn_moet_math_grade2_tt32_2018"
EXPECTED_CATALOG_ID = "math_grade2_lesson_catalog_v1"
EXPECTED_BANK_ID = "math_grade2_static_question_bank_v1"
EXPECTED_LANGUAGE = "vi"
EXPECTED_ID_POLICY = "deterministic_ascii_lower_snake_preserve_existing_uppercase_skill_ids"
EXPECTED_BASELINE_STATUS = "VERIFIED_A_BASELINE"
REQUIRED_BASELINE_SOURCE_IDS = {"MOET_TT32_2018", "TT32_FULL_ANNEX_MIRROR"}
EXPECTED_BASELINE_HARD_GUARDS = {
    "max_number_baseline": 1000,
    "max_carry_or_borrow_rounds": 1,
    "official_multiplication_tables": [2, 5],
    "official_division_tables": [2, 5],
    "mental_add_sub_max": 20,
    "clock_minute_hand_allowed_numbers_for_baseline_tasks": [3, 6],
}
ANSWER_UNIT_PROMPT_ALIASES = {
    "cm": ("cm", "xăng-ti-mét", "xăng ti mét"),
    "kg": ("kg", "ki-lô-gam", "kilôgam", "ki lô gam"),
    "l": ("l", "lít"),
    "dm": ("dm", "đề-xi-mét", "đề xi mét"),
    "m": ("m", "mét"),
    "ngày": ("ngày",),
    "giờ": ("giờ",),
    "phút": ("phút",),
}
DIFFICULTIES = {"basic", "medium", "application"}
ENGINE_ANSWER_KINDS = {"integer", "interaction_integer", "number", "decimal", "fraction", "text", "unit", "expression"}
GRADE2_USED_ANSWER_KINDS = {"integer", "interaction_integer", "text", "unit", "expression"}
QUESTION_TYPES = {"numeric_input", "multiple_choice", "true_false", "expression_input", "unit_input", "interactive_measurement", "word_problem"}
ADD_SUB_EXPRESSION_ALLOWED_OPERATORS = {"+", "-", "(", ")"}
SKILL_NUMERIC_MAX = {
    "MENTAL_ADD_SUB_WITHIN_20": 20,
    "MULTIPLICATION_MEANING": 50,
    "DIVISION_MEANING": 10,
    "TIMES_TABLE_2": 20,
    "TIMES_TABLE_5": 50,
    "DIVIDE_TABLE_2": 10,
    "DIVIDE_TABLE_5": 10,
    "WP_ONE_STEP_MULTIPLICATION_CONTEXT": 50,
    "WP_ONE_STEP_DIVISION_CONTEXT": 10,
}
ADD_CARRY_RULES = {"ADD_WITHIN_1000_NO_CARRY": 0, "ADD_WITHIN_1000_ONE_CARRY_MAX": 1}
SUB_BORROW_RULES = {"SUB_WITHIN_1000_NO_BORROW": 0, "SUB_WITHIN_1000_ONE_BORROW_MAX": 1}
MONEY_DENOMINATION_RE = re.compile(r"\b\d[\d\s.,]*\s*đồng\b", re.IGNORECASE)
TIME_RELATION_SKILLS = {"TIME_DAY_24_HOURS", "TIME_HOUR_60_MINUTES"}
TIME_OUT_OF_SCOPE_ARITH_RE = re.compile(r"\b\d+\s*(?:×|\*|÷|/|:)\s*\d+\b")
GRADE2_MUL_LITERAL_RE = re.compile(r"(?<!\d)(\d+)\s*(?:×|\*)\s*(\d+)(?!\d)")
GRADE2_DIV_LITERAL_RE = re.compile(r"(?<!\d)(\d+)(?:\s*÷\s*|\s+:\s+)(\d+)(?!\d)")
NUMERIC_CHOICE_EXPR_RE = re.compile(r"^[\d\s+\-−–*/×÷:().,]+$")
NUMERIC_RELATION_RE = re.compile(r"(?=(?<!\d)([+-]?\d+)\s*(<=|>=|≤|≥|<|>)\s*([+-]?\d+)(?!\d))")
NUMERIC_EQUALITY_RES = (
    re.compile(r"(?<!\d)((?:\d+\s*(?:[+\-×*÷])\s*)+\d+)\s*=\s*(-?\d+)(?!\d)"),
    re.compile(r"(?<!\d)((?:\d+\s+:\s+)+\d+)\s*=\s*(-?\d+)(?!\d)"),
)
MIN_QUESTION_EXPLANATION_CHARS = 32
MIN_CONCEPT_DEFINITION_CHARS = 40
MAX_LESSON_TITLE_CHARS = 60
MAX_LESSON_EXPLANATION_CHARS = 200
MAX_OBJECTIVE_CHARS = 130
MAX_CONCEPT_NAME_CHARS = 50
MAX_CONCEPT_DEFINITION_CHARS = 120
MAX_WORKED_PROMPT_CHARS = 130
MAX_WORKED_ANSWER_CHARS = 80
MAX_WORKED_STEP_CHARS = 100
MAX_QUESTION_PROMPT_CHARS = 180
MAX_QUESTION_EXPLANATION_CHARS = 200
MAX_CHOICE_TEXT_CHARS = 80
MAX_HINT_CHARS = 130
MAX_DISTRACTOR_RATIONALE_CHARS = 300
MAX_APPLICATION_LOWER_SHAPE_SIMILARITY = 0.75
GENERIC_SECOND_HINT = "Thực hiện từng bước và kiểm tra lại với dữ kiện của câu hỏi."
SHALLOW_FIRST_HINT_MARKER = "nhớ kiến thức:"
SHALLOW_SECOND_HINT_MARKER = "viết một phép tính hoặc quan hệ ngắn cho"
GENERIC_FIRST_OBJECTIVE_PREFIX = "Nhận biết và thực hiện đúng nội dung:"
GENERIC_SECOND_OBJECTIVE = "Giải thích được cách làm bằng ngôn ngữ ngắn gọn và kiểm tra kết quả theo dữ kiện."
GENERIC_DISTRACTOR_RATIONALE = "Lựa chọn này không phù hợp với quy tắc hoặc dữ kiện của bài."
SHALLOW_NUMERIC_EXPLANATION_MARKER = "kết quả này theo đúng quy tắc"
GENERIC_MULDIV_EXPLANATION_MARKER = "quan hệ nhân/chia tương ứng trong bảng đã học"
GENERIC_WORD_PROBLEM_EXPLANATION_MARKER = "để trả lời đúng đại lượng mà đề đang hỏi"
COMPONENT_SKILLS = {
    "ADD_COMPONENTS_RECOGNIZE",
    "SUB_COMPONENTS_RECOGNIZE",
    "MULTIPLICATION_COMPONENTS",
    "DIVISION_COMPONENTS",
}
COMPONENT_TERM_REASON_MARKERS = {
    "số hạng": "số hạng là số được đem cộng",
    "tổng": "tổng là kết quả của phép cộng",
    "số bị trừ": "số bị trừ là số đứng trước dấu trừ",
    "số trừ": "số trừ là lượng được bớt khỏi số bị trừ",
    "hiệu": "hiệu là kết quả của phép trừ",
    "thừa số": "thừa số là số được đem nhân",
    "tích": "tích là kết quả của phép nhân",
    "số bị chia": "số bị chia là lượng được đem chia",
    "số chia": "số chia là số dùng để chia số bị chia",
    "thương": "thương là kết quả của phép chia",
}
SHALLOW_DISTRACTOR_RATIONALE_MARKERS = (
    "chưa thỏa đủ dữ kiện",
    "có ít nhất một bước của",
    "phương án nhiễu gần đúng",
)
CHILD_FACING_KEYS = {
    "title_vi", "objectives_vi", "explanation_vi", "name_vi", "definition_vi",
    "prompt_vi", "solution_steps_vi", "hints_vi", "rationale_vi", "text",
    "answer", "correct_answer",
}
INTERNAL_CHILD_VOCAB_RE = re.compile(
    r"(?i)(?<![A-Za-z])(?:baseline|runtime|mapping|template|deterministic|numeric|metadata|validator|contract|engine|json|source|prompt)(?![A-Za-z])"
)


def structured_choice_reason(skill: str, prompt: str, choice_text: str) -> str | None:
    if skill == "NUM_EXPANDED_FORM_HTO":
        prompt_numbers = [int(x) for x in re.findall(r"\d+", prompt)]
        if not prompt_numbers:
            return None
        target = prompt_numbers[0]
        rhs = choice_text.split("=", 1)[1].strip() if "=" in choice_text else choice_text.strip()
        if re.fullmatch(r"\d+(?:\s*\+\s*\d+)+", rhs):
            value = sum(int(x) for x in re.findall(r"\d+", rhs))
            if value != target:
                return f"Vế phải của “{choice_text}” bằng {value}, không bằng số {target} cần biểu diễn."

    if skill == "NUM_COMPARE_0_1000":
        prompt_numbers = [int(x) for x in re.findall(r"\d+", prompt)]
        choice_numbers = [int(x) for x in re.findall(r"\d+", choice_text)]
        if len(choice_numbers) >= 2:
            left, right = choice_numbers[0], choice_numbers[1]
        elif len(prompt_numbers) >= 2:
            left, right = prompt_numbers[0], prompt_numbers[1]
        else:
            return None
        if "+" in choice_text:
            return "Dấu + là dấu phép cộng, không phải dấu dùng để so sánh hai số."
        if re.search(r"(?<!\d)-(?!\d)", choice_text) or choice_text.strip() == "-":
            return "Dấu - là dấu phép trừ, không phải dấu dùng để so sánh hai số."
        actual = "=" if left == right else (">" if left > right else "<")
        shown = next((symbol for symbol in (">", "<", "=") if symbol in choice_text), None)
        if shown and shown != actual:
            if actual == "=":
                relation = f"{left} bằng {right}"
            elif actual == ">":
                relation = f"{left} lớn hơn {right}"
            else:
                relation = f"{left} nhỏ hơn {right}"
            return f"Dấu {shown} chưa phù hợp vì {relation}; quan hệ đúng phải dùng dấu {actual}."

    if skill == "NUM_SORT_UP_TO_4":
        numbers = [int(x) for x in re.findall(r"\d+", choice_text)]
        if len(numbers) < 2:
            return None
        descending = "giảm dần" in prompt.casefold()
        direction = "giảm dần" if descending else "tăng dần"
        for left, right in zip(numbers, numbers[1:]):
            violates = left < right if descending else left > right
            if violates:
                sign = "<" if left < right else ">"
                return f"Trong “{choice_text}”, {left} đứng trước {right} dù {left} {sign} {right}, nên thứ tự chưa {direction}."

    if skill == "NUM_FULL_HUNDREDS_RECOGNIZE":
        if re.fullmatch(r"\d+", choice_text.strip()):
            value = int(choice_text.strip())
            tens = (value // 10) % 10
            ones = value % 10
            if tens != 0 or ones != 0:
                return (f"Số {value} còn hàng chục hoặc hàng đơn vị khác 0 "
                        f"({tens} chục, {ones} đơn vị), nên chưa phải một số trăm đầy đủ.")

    if skill == "MULTIPLICATION_MEANING" and "+" in choice_text:
        match = re.search(r"Có\s+(\d+)\s+nhóm, mỗi nhóm\s+(\d+)", prompt, re.IGNORECASE)
        terms = [int(x) for x in re.findall(r"\d+", choice_text)]
        if match and terms:
            groups, each = int(match.group(1)), int(match.group(2))
            if len(terms) != groups or any(term != each for term in terms):
                return (f"Phải cộng số {each} đúng {groups} lần; lựa chọn “{choice_text}” "
                        f"không giữ đúng số nhóm và số phần tử mỗi nhóm.")

    if skill in {"OPERATION_MEANING_FROM_VISUAL", "WP_SELECT_OPERATION_ONE_STEP"}:
        normalized_prompt = prompt.casefold()
        expected_op = None
        context_reason = None
        if "gộp" in normalized_prompt:
            expected_op, context_reason = "+", "gộp các nhóm nên cần phép cộng"
        elif "gạch bỏ" in normalized_prompt or ("ăn" in normalized_prompt and "còn lại" in normalized_prompt):
            expected_op, context_reason = "-", "bớt đi rồi hỏi phần còn lại nên cần phép trừ"
        elif "nhóm bằng nhau" in normalized_prompt or ("mỗi giỏ" in normalized_prompt and "tất cả" in normalized_prompt):
            expected_op, context_reason = "×", "nhiều nhóm bằng nhau và hỏi tất cả nên cần phép nhân"
        elif "chia đều" in normalized_prompt:
            expected_op, context_reason = ":", "chia đều thành các phần bằng nhau nên cần phép chia"
        if expected_op:
            shown_op = next((op for op in ("×", ":", "+", "-") if op in choice_text), None)
            if shown_op and shown_op != expected_op:
                return f"Tình huống này {context_reason}; “{choice_text}” dùng phép tính khác quan hệ đề bài."
            prompt_numbers = [int(x) for x in re.findall(r"\d+", prompt)]
            choice_numbers = [int(x) for x in re.findall(r"\d+", choice_text)]
            if shown_op == expected_op and len(prompt_numbers) >= 2 and len(choice_numbers) >= 2:
                if "gộp" in normalized_prompt and "hai nhóm" in normalized_prompt:
                    each = prompt_numbers[0]
                    if choice_numbers[:2] != [each, each]:
                        return f"Có hai nhóm cùng {each} chấm nên phép cộng phải dùng {each} và {each}, không phải “{choice_text}”."
                if "gạch bỏ" in normalized_prompt:
                    start, removed = prompt_numbers[0], prompt_numbers[1]
                    if choice_numbers[:2] != [start, removed]:
                        return f"Phải bắt đầu từ {start} rồi bớt {removed}; “{choice_text}” đặt sai số ban đầu hoặc số bị bớt."
                if "nhóm bằng nhau" in normalized_prompt:
                    groups, each = prompt_numbers[0], prompt_numbers[1]
                    if choice_numbers[:2] != [groups, each]:
                        return f"Có {groups} nhóm, mỗi nhóm {each} chấm nên phải giữ đúng hai số {groups} và {each}; “{choice_text}” đổi số nhóm."

    if skill in {"EVENT_POSSIBLE", "EVENT_CERTAIN", "EVENT_IMPOSSIBLE"}:
        label = choice_text.strip().casefold()
        if skill == "EVENT_POSSIBLE":
            if label == "chắc chắn":
                return "Sự kiện chỉ có thể xảy ra chứ không xảy ra ở mọi kết quả, nên chưa thể gọi là chắc chắn."
            if label == "không thể":
                return "Sự kiện có ít nhất một kết quả làm nó xảy ra, nên không thể xếp vào loại không thể."
            if label in {"bằng nhau", "luôn sai", "không có kết quả"}:
                return f"“{choice_text}” không phải cách phân loại mức độ có thể xảy ra của sự kiện trong tình huống này."
        if skill == "EVENT_CERTAIN":
            if label in {"có thể nhưng không chắc", "chỉ có thể"}:
                return "Mọi kết quả hợp lệ đều thỏa điều kiện, nên sự kiện không chỉ dừng ở mức có thể mà là chắc chắn."
            if label == "không thể":
                return "Sự kiện xảy ra với mọi kết quả hợp lệ, nên không thể gọi là không thể."
            if label in {"sai", "không có màu", "không xác định"}:
                return f"“{choice_text}” không phản ánh việc mọi kết quả hợp lệ đều làm sự kiện xảy ra."
        if skill == "EVENT_IMPOSSIBLE":
            if label == "có thể":
                return "Không có kết quả hợp lệ nào làm sự kiện xảy ra, nên không thể gọi là có thể."
            if label == "chắc chắn":
                return "Không có kết quả hợp lệ nào làm sự kiện xảy ra, trái hẳn với điều kiện của sự kiện chắc chắn."
            if label in {"luôn đúng", "luôn xảy ra"}:
                return f"“{choice_text}” nói sự kiện luôn xảy ra, nhưng thực tế không có kết quả hợp lệ nào làm nó xảy ra."
            if label == "bằng nhau":
                return "“bằng nhau” không phải cách phân loại mức độ có thể xảy ra của sự kiện."

    if skill == "MASS_KG_READ_WRITE":
        unit = choice_text.strip().casefold()
        reasons = {
            "km": "km là đơn vị độ dài quãng đường, không phải đơn vị khối lượng.",
            "l": "l là kí hiệu lít dùng cho dung tích, không phải kilôgam.",
            "cm": "cm là đơn vị độ dài, không phải đơn vị khối lượng.",
        }
        if unit in reasons:
            return reasons[unit]

    if skill == "CAPACITY_LITER_READ_WRITE":
        unit = choice_text.strip().casefold()
        reasons = {
            "kg": "kg là đơn vị khối lượng, không phải đơn vị dung tích.",
            "km": "km là đơn vị độ dài quãng đường, không phải đơn vị dung tích.",
            "dm": "dm là đơn vị độ dài, không phải đơn vị dung tích.",
        }
        if unit in reasons:
            return reasons[unit]

    if skill == "LENGTH_DM_M_KM_RECOGNIZE_RELATION" and "giữa hai làng" in prompt.casefold():
        unit = choice_text.strip().casefold()
        if unit == "kg":
            return "kg đo khối lượng chứ không đo quãng đường, nên không phù hợp giữa hai làng."
        if unit == "cm":
            return "cm phù hợp với độ dài vật nhỏ; quãng đường giữa hai làng cần đơn vị lớn hơn nhiều."
        if unit == "dm":
            return "dm phù hợp với độ dài ngắn; quãng đường giữa hai làng thường dùng km."

    if skill == "TIME_DAY_24_HOURS":
        label = choice_text.strip().casefold()
        if label == "2 ngày":
            return "24 giờ liên tiếp mới đủ đúng một ngày; 2 ngày là khoảng thời gian dài hơn một ngày."
        if label == "10 ngày":
            return "24 giờ liên tiếp mới đủ đúng một ngày; 10 ngày dài hơn rất nhiều so với một ngày."
        if label == "không thể biết":
            return "Quan hệ 1 ngày = 24 giờ đã biết nên có thể xác định trực tiếp, không phải thiếu dữ kiện."

    if skill == "TIME_HOUR_60_MINUTES":
        label = choice_text.strip().casefold()
        prompt_cf = prompt.casefold()
        if "45 phút" in prompt_cf:
            if label == "dài hơn":
                return "1 giờ có 60 phút, mà 45 phút ít hơn 60 phút nên không thể dài hơn 1 giờ."
            if label == "bằng nhau":
                return "45 phút chưa đủ 60 phút của một giờ nên hai khoảng thời gian không bằng nhau."
            if label == "không so sánh được":
                return "Đổi 1 giờ thành 60 phút thì có thể so sánh trực tiếp với 45 phút."
        if "60 phút" in prompt_cf:
            if label == "1 ngày":
                return "60 phút chỉ bằng một giờ, còn một ngày gồm 24 giờ nên không thể gọi là 1 ngày."
            if label == "30 phút":
                return "30 phút ngắn hơn 60 phút; 60 phút đầy đủ mới bằng một giờ."
            if label == "không thể biết":
                return "Quan hệ 1 giờ = 60 phút đã biết nên có thể gọi tên khoảng thời gian này trực tiếp."

    if skill == "CALENDAR_DAYS_IN_MONTH_DATE" and "ngày sau ngày 14 tháng 9" in prompt.casefold():
        label = choice_text.strip().casefold()
        if label == "13 tháng 9":
            return "Ngày 13 tháng 9 đứng trước ngày 14, nên đó không phải ngày kế tiếp."
        if label == "14 tháng 10":
            return "Ngày kế tiếp vẫn ở tháng 9; chuyển sang tháng 10 đã thay đổi cả tháng thay vì tăng một ngày."
        if label == "16 tháng 9":
            return "Sau ngày 14 phải đi qua ngày 15 trước; chọn ngày 16 đã bỏ qua một ngày."

    if skill == "CLOCK_MINUTE_HAND_AT_3_OR_6":
        prompt_cf = prompt.casefold()
        expected_hour = expected_minute = None
        minute_basis = None
        if "kim phút chỉ số 3" in prompt_cf and "vừa qua số 4" in prompt_cf:
            expected_hour, expected_minute, minute_basis = 4, 15, "Kim phút ở số 3 tương ứng 15 phút"
        elif "kim phút chỉ số 6" in prompt_cf and "giữa 7 và 8" in prompt_cf:
            expected_hour, expected_minute, minute_basis = 7, 30, "Kim phút ở số 6 tương ứng 30 phút"
        elif "từ số 3 đến số 6" in prompt_cf and "giữa 5 và 6" in prompt_cf:
            expected_hour, expected_minute, minute_basis = 5, 30, "Kim phút đi đến số 6 nên chuyển thành 30 phút"
        match = re.fullmatch(r"(\d+)\s+giờ\s+(\d+)\s+phút", choice_text.strip().casefold())
        if expected_hour is not None and match:
            hour, minute = int(match.group(1)), int(match.group(2))
            if minute != expected_minute:
                return f"{minute_basis}; “{choice_text}” ghi {minute} phút nên sai vị trí kim phút."
            if hour != expected_hour:
                return f"Kim giờ vẫn thuộc giờ {expected_hour}; “{choice_text}” đổi sang giờ {hour} quá sớm."

    if skill == "MEASUREMENT_ESTIMATE_BASIC":
        label = choice_text.strip().casefold()
        prompt_cf = prompt.casefold()
        if "bút chì" in prompt_cf:
            if label.endswith("km"):
                return "km dùng cho quãng đường rất dài, không phù hợp chiều dài một chiếc bút chì."
            if label.endswith("m"):
                return f"“{choice_text}” tính theo mét là quá lớn đối với một chiếc bút chì; cỡ xăng-ti-mét hợp lý hơn."
        if "cánh cửa" in prompt_cf:
            if label.endswith("cm"):
                return "2 cm quá nhỏ cho chiều cao một cánh cửa; kích thước này hợp lý hơn khi tính bằng mét."
            if label.endswith("km"):
                return f"“{choice_text}” dùng ki-lô-mét, đơn vị dành cho quãng đường rất dài chứ không phải chiều cao cửa."
        if "gấp đôi" in prompt_cf and "10 cm" in prompt_cf:
            if label == "5 cm":
                return "5 cm còn ngắn hơn thanh tham chiếu 10 cm nên không thể là độ dài gấp đôi."
            if label == "100 cm":
                return "100 cm lớn hơn nhiều so với hai lần thanh 10 cm nên không phải ước lượng gấp đôi."
            if label == "2 km":
                return "2 km là quãng đường rất dài, hoàn toàn khác thang đo xăng-ti-mét của thanh tham chiếu."

    if skill == "NUM_COUNT_READ_WRITE_0_1000" and "420" in prompt:
        label = choice_text.strip().casefold()
        reasons = {
            "bốn trăm hai": "Số 420 có 2 chục nên cách đọc phải thể hiện phần hai mươi; lựa chọn này đã bỏ mất hàng chục.",
            "bốn mươi hai": "“bốn mươi hai” chỉ biểu diễn 42, đã bỏ mất 4 trăm của số 420.",
            "hai trăm bốn mươi": "“hai trăm bốn mươi” biểu diễn 240, đã đổi 4 trăm thành 2 trăm và 2 chục thành 4 chục.",
        }
        if label in reasons:
            return reasons[label]

    if skill == "ESTIMATE_OBJECTS_BY_TENS":
        prompt_numbers = [int(x) for x in re.findall(r"\d+", prompt)]
        choice_numbers = [int(x) for x in re.findall(r"\d+", choice_text)]
        if prompt_numbers and choice_numbers:
            value, chosen = prompt_numbers[0], choice_numbers[0]
            nearest = ((value + 5) // 10) * 10
            if chosen != nearest:
                return (f"Ước lượng {value} theo chục gần nhất phải chọn {nearest}; “{choice_text}” "
                        f"cách xa giá trị cần ước lượng hơn.")

    if skill == "HEAVIER_LIGHTER":
        prompt_cf = prompt.casefold()
        label = choice_text.strip().casefold()
        if "đĩa bên trái hạ thấp hơn" in prompt_cf:
            reasons = {
                "nhẹ hơn": "Đĩa cân hạ thấp là phía nặng hơn, nên vật bên trái không thể nhẹ hơn.",
                "bằng nhau": "Nếu hai bên bằng nhau thì cân phải ngang; ở đây đĩa trái đang hạ thấp.",
                "không thể so sánh": "Độ cao hai đĩa đang cho đủ thông tin để so sánh: bên trái hạ thấp nên nặng hơn.",
            }
            if label in reasons: return reasons[label]
        if "vật a nặng hơn vật b" in prompt_cf:
            reasons = {
                "nặng hơn": "Nếu A nặng hơn B thì theo chiều ngược lại B phải nhẹ hơn A, không thể nặng hơn A.",
                "bằng nhau": "A đã nặng hơn B nên hai vật không thể có khối lượng bằng nhau.",
                "không thể kết luận": "Quan hệ A nặng hơn B đã cho trực tiếp, nên suy ra được B nhẹ hơn A.",
            }
            if label in reasons: return reasons[label]
        if "hai đĩa ngang bằng" in prompt_cf:
            reasons = {
                "bên trái nặng hơn": "Cân đang ngang nên không có căn cứ nói bên trái nặng hơn bên phải.",
                "bên phải nặng hơn": "Cân đang ngang nên không có căn cứ nói bên phải nặng hơn bên trái.",
                "không thể so sánh khối lượng hai bên": "Hai đĩa ngang bằng chính là dữ kiện cho thấy khối lượng hai bên bằng nhau trong phép cân đó.",
            }
            if label in reasons: return reasons[label]

    if skill == "MONEY_VND_NOTE_RECOGNITION":
        label = choice_text.strip().casefold()
        reasons = {
            "chỉ màu sắc của tờ tiền": "Màu sắc không tự xác định giá trị; cần đọc con số mệnh giá và đơn vị đồng trên tờ.",
            "chỉ kích thước của tờ tiền": "Kích thước không thay thế con số mệnh giá; muốn biết giá trị phải đọc thông tin in trên tờ.",
            "chỉ hình trang trí nổi bật trên tờ": "Hình trang trí không phải con số mệnh giá, nên không đủ để xác định giá trị tờ tiền.",
            "chỉ so sánh màu sắc của hai tờ": "Màu sắc không cho biết chắc tờ nào có giá trị lớn hơn; phải so sánh hai con số mệnh giá.",
            "chỉ so sánh kích thước của hai tờ": "Kích thước không phải giá trị tiền; cần đọc và so sánh con số mệnh giá trên hai tờ.",
            "chọn tờ có nhiều chữ hơn": "Số lượng chữ trên tờ không quyết định giá trị; con số mệnh giá mới là thông tin cần so sánh.",
        }
        if label in reasons:
            return reasons[label]

    if skill == "PICTOGRAPH_SIMPLE_INFERENCE" and "nhóm Cam 5 biểu tượng" in prompt:
        counts = {"cam": 5, "táo": 7, "chuối": 3}
        label = choice_text.strip().casefold()
        if label in counts:
            return f"Nhóm {choice_text.strip()} có {counts[label]} biểu tượng, ít hơn Táo có 7 biểu tượng nên không phải nhóm nhiều nhất."
        if label == "cả ba bằng nhau":
            return "Ba nhóm có số biểu tượng 5, 7 và 3 nên không bằng nhau; nhóm có 7 biểu tượng mới nhiều nhất."

    if skill == "POINT_RECOGNIZE":
        label = choice_text.strip().casefold()
        prompt_cf = prompt.casefold()
        if "kí hiệu nào phù hợp" in prompt_cf or "tên nào phù hợp" in prompt_cf:
            reasons = {
                "ab": "AB gồm hai chữ cái nên thường dùng để chỉ đối tượng gắn với hai điểm A và B, không phải tên của một điểm duy nhất.",
                "mn": "MN gồm hai chữ cái nên gợi hai điểm M và N, không phải tên của một điểm duy nhất.",
                "1": "Tên điểm trong hình học thường dùng một chữ cái in hoa; số 1 không phải kí hiệu tên điểm trong quy ước này.",
                "5 cm": "5 cm là một số đo độ dài, không phải kí hiệu dùng để đặt tên một vị trí hình học.",
                "3 cm": "3 cm là một số đo độ dài, không phải tên dùng để ghi một điểm.",
                "đường m": "Cụm “đường M” đang gọi một đường, còn tên của một điểm chỉ cần chữ cái M.",
            }
            if label in reasons: return reasons[label]
        if "phát biểu nào đúng về một điểm" in prompt_cf or "điều nào đúng khi nói về điểm" in prompt_cf:
            reasons = {
                "điểm có một độ dài xác định": "Điểm chỉ biểu diễn một vị trí nên không có độ dài riêng để đo.",
                "điểm có hai đầu mút": "Hai đầu mút là đặc điểm của đoạn thẳng, không phải của một điểm.",
                "điểm có thể kéo dài về hai phía": "Khả năng kéo dài về hai phía là đặc điểm của đường thẳng; một điểm chỉ là một vị trí.",
                "p có độ dài 5 cm": "Điểm P chỉ biểu diễn một vị trí nên không có độ dài 5 cm hay bất kì độ dài riêng nào.",
                "p có hai đầu mút": "Hai đầu mút thuộc về đoạn thẳng; điểm P không có hai đầu mút.",
                "p kéo dài mãi về hai phía": "Kéo dài mãi về hai phía là đặc điểm của đường thẳng, không phải của điểm P.",
            }
            if label in reasons: return reasons[label]
        expected_points = None
        point_names = None
        if "ba vị trí được đánh dấu a, b, c" in prompt_cf:
            expected_points, point_names = 3, "A, B, C"
        elif "bốn vị trí p, q, r, s" in prompt_cf:
            expected_points, point_names = 4, "P, Q, R, S"
        if expected_points is not None:
            nums = [int(x) for x in re.findall(r"\d+", choice_text)]
            if nums and nums[0] != expected_points:
                return (f"Hình đã nêu {expected_points} vị trí {point_names} nên có đúng {expected_points} điểm được đặt tên; "
                        f"chọn {nums[0]} là đếm thiếu hoặc thừa.")

    if skill == "LINE_SEGMENT_RECOGNIZE":
        label = choice_text.strip().casefold()
        prompt_cf = prompt.casefold()
        if "bao nhiêu đầu mút" in prompt_cf:
            nums = [int(x) for x in re.findall(r"\d+", choice_text)]
            if nums and nums[0] != 2:
                return f"Một đoạn thẳng luôn có đúng 2 đầu mút; lựa chọn {nums[0]} không đúng số đầu mút của đoạn AB."
        if "đoạn thẳng pq" in prompt_cf and "hai đầu mút" in prompt_cf:
            reasons = {
                "chỉ p": "Đoạn thẳng PQ có hai đầu mút P và Q; chỉ chọn P đã bỏ mất đầu mút Q.",
                "chỉ q": "Đoạn thẳng PQ có hai đầu mút P và Q; chỉ chọn Q đã bỏ mất đầu mút P.",
                "không có đầu mút": "Đoạn thẳng luôn bị giới hạn bởi hai đầu mút; với PQ đó chính là P và Q.",
            }
            if label in reasons: return reasons[label]
        if "mô tả nào đúng về đoạn thẳng" in prompt_cf or "mô tả nào nhận ra một đoạn thẳng" in prompt_cf:
            reasons = {
                "phần thẳng kéo dài mãi về hai phía": "Nét thẳng kéo dài mãi về hai phía là đường thẳng; đoạn thẳng bị giới hạn bởi hai đầu mút.",
                "nét thẳng kéo dài mãi hai phía": "Nét thẳng kéo dài mãi hai phía là đường thẳng; đoạn thẳng phải dừng ở hai đầu mút.",
                "nét uốn cong nối hai vị trí": "Đoạn thẳng phải là phần thẳng giữa hai đầu mút, không phải một nét uốn cong.",
                "nét cong không có đầu mút": "Nét cong không có đầu mút không phải đoạn thẳng vì đoạn thẳng vừa thẳng vừa bị giới hạn bởi hai đầu mút.",
                "chỉ một vị trí không có độ dài": "Một vị trí đơn lẻ là điểm; đoạn thẳng phải nối hai đầu mút và có độ dài.",
                "một chấm chỉ vị trí": "Một chấm chỉ một điểm; đoạn thẳng phải là phần thẳng nối giữa hai đầu mút.",
            }
            if label in reasons: return reasons[label]
        if "nối thẳng điểm m với điểm n" in prompt_cf or ("nối thẳng c với d" in prompt_cf and "dừng nét" in prompt_cf):
            reasons = {
                "đường thẳng mn": "Nối hai điểm M và N bằng phần thẳng chỉ giữa hai điểm tạo đoạn thẳng MN; đường thẳng còn kéo dài qua hai phía.",
                "đường cong mn": "Đề yêu cầu nối thẳng M với N nên kết quả không thể là đường cong.",
                "chỉ điểm m": "Hình mới phải liên hệ cả M và N; chỉ giữ điểm M thì chưa thực hiện việc nối hai điểm.",
                "đường thẳng cd": "Nét dừng đúng tại C và D nên bị giới hạn bởi hai đầu mút; đó là đoạn thẳng CD chứ không phải đường thẳng kéo dài.",
                "đường cong cd": "Đề yêu cầu nối thẳng C với D nên nét tạo ra không thể là đường cong.",
                "điểm cd": "CD gồm hai vị trí được nối bằng một nét thẳng; đó không phải một điểm đơn lẻ.",
            }
            if label in reasons: return reasons[label]

    if skill == "CURVE_RECOGNIZE":
        label = choice_text.strip().casefold()
        prompt_cf = prompt.casefold()
        reasons = {
            "nét thẳng kéo dài không đổi hướng": "Nét không đổi hướng là nét thẳng, trong khi đường cong phải có sự uốn hoặc đổi hướng liên tục.",
            "phần thẳng có hai đầu mút": "Phần thẳng có hai đầu mút là đoạn thẳng, không phải đường cong.",
            "một vị trí được đánh dấu": "Một vị trí được đánh dấu là điểm, không tạo thành một đường cong.",
            "đường thẳng": "Vòng cung có sự uốn cong nên không thể là đường thẳng.",
            "đoạn thẳng": "Vòng cung không phải phần thẳng giữa hai đầu mút nên không phải đoạn thẳng.",
            "điểm": "Vòng cung là một nét có chiều dài và đổi hướng, không phải một điểm đơn lẻ.",
            "nét đó vẫn là đường thẳng vì có đoạn đi thẳng": "Nét đã uốn thì có phần đường cong; một đoạn đi thẳng không làm cả nét trở thành đường thẳng.",
            "nét đó phải khép kín mới là đường cong": "Đường cong không bắt buộc khép kín; một nét uốn mở vẫn là đường cong.",
            "nét đó là một điểm vì không có cạnh": "Điểm chỉ là một vị trí; nét đã kéo dài và uốn sang bên nên không thể là một điểm.",
        }
        if label in reasons: return reasons[label]

    if skill == "STRAIGHT_LINE_RECOGNIZE":
        label = choice_text.strip().casefold()
        prompt_cf = prompt.casefold()
        reasons = {
            "có đúng hai đầu mút": "Đúng hai đầu mút là đặc điểm của đoạn thẳng; đường thẳng có thể kéo dài về cả hai phía.",
            "chỉ kéo dài về một phía": "Đường thẳng có thể kéo dài về cả hai phía, không chỉ một phía.",
            "luôn uốn cong": "Đường thẳng không uốn cong; các điểm của nó nằm theo cùng một hướng thẳng.",
            "đoạn thẳng": "Nét có thể kéo dài mãi theo hai hướng là đường thẳng, còn đoạn thẳng bị giới hạn bởi hai đầu mút.",
            "đường cong": "Đề mô tả nét không uốn cong nên không thể là đường cong.",
            "đường gấp khúc": "Đường gấp khúc gồm nhiều đoạn thẳng đổi hướng tại điểm nối; đề chỉ mô tả một đường thẳng kéo dài.",
            "a và b là hai đầu mút của d": "Đường thẳng d không bị giới hạn tại A và B, nên A và B không phải hai đầu mút của d.",
            "d chỉ gồm đoạn giữa a và b": "Đường thẳng d còn kéo dài ra ngoài A và B, không chỉ gồm phần nằm giữa hai điểm.",
            "a hoặc b không thuộc d": "Đề đã cho cả A và B cùng nằm trên d nên nói một trong hai điểm không thuộc d là trái dữ kiện.",
        }
        if label in reasons: return reasons[label]

    if skill == "POLYLINE_RECOGNIZE":
        label = choice_text.strip().casefold()
        prompt_cf = prompt.casefold()
        reasons = {
            "nhiều đoạn thẳng rời nhau": "Đường gấp khúc cần các đoạn thẳng nối tiếp nhau tại đầu mút; các đoạn rời nhau chưa tạo thành một đường gấp khúc.",
            "ba đoạn thẳng rời nhau": "Ba đoạn thẳng còn rời nhau nên chưa nối tiếp tại các đầu mút để tạo đường gấp khúc.",
            "một đoạn thẳng duy nhất": "Đường gấp khúc phải gồm nhiều đoạn thẳng nối tiếp, không chỉ một đoạn duy nhất.",
            "một nét cong liên tục": "Đường gấp khúc được tạo bởi các đoạn thẳng, không phải một nét cong liên tục.",
            "một nét cong duy nhất": "Một nét cong duy nhất không tạo thành chuỗi các đoạn thẳng nối tiếp của đường gấp khúc.",
            "một điểm và một đoạn rời": "Một điểm và một đoạn còn rời nhau không tạo thành nhiều đoạn thẳng nối tiếp, nên chưa phải đường gấp khúc.",
            "đoạn thẳng ad": "Ba đoạn AB, BC, CD có các chỗ đổi hướng tại B và C nên không thể gộp thành một đoạn thẳng AD.",
            "đường thẳng ad": "Chuỗi AB, BC, CD gồm nhiều đoạn nối tiếp và có thể đổi hướng, không phải một đường thẳng duy nhất AD.",
            "đường cong abcd": "Các phần AB, BC, CD đều là đoạn thẳng nên toàn hình là đường gấp khúc, không phải đường cong.",
            "đoạn thẳng mq": "MN, NP, PQ là ba đoạn nối tiếp qua N và P; chúng tạo đường gấp khúc MNPQ chứ không phải một đoạn thẳng MQ duy nhất.",
            "đường thẳng mq": "Chuỗi MN, NP, PQ gồm nhiều đoạn nối tiếp, không phải một đường thẳng duy nhất MQ.",
            "đường cong mnpq": "MN, NP, PQ đều là các đoạn thẳng nối tiếp nên hình là đường gấp khúc, không phải đường cong MNPQ.",
        }
        if label in reasons: return reasons[label]
        if "có 4 đoạn thẳng" in prompt_cf:
            nums = [int(x) for x in re.findall(r"\d+", choice_text)]
            if nums and nums[0] != 5:
                return f"Chuỗi mở có 4 đoạn thẳng cần 5 điểm theo thứ tự để tạo 4 khoảng nối; chọn {nums[0]} là thiếu điểm."
        if "sáu điểm liên tiếp" in prompt_cf:
            nums = [int(x) for x in re.findall(r"\d+", choice_text)]
            if nums and nums[0] != 5:
                return f"Sáu điểm liên tiếp tạo 5 khoảng nối giữa các điểm kề nhau, nên đường gấp khúc có 5 đoạn; chọn {nums[0]} là đếm sai số khoảng."

    if skill == "THREE_COLLINEAR_POINTS":
        label = choice_text.strip().casefold()
        reasons = {
            "không thẳng hàng": "Cả A, B, C đã cùng nằm trên một đường thẳng nên theo định nghĩa ba điểm là thẳng hàng.",
            "tạo thành tam giác": "Ba điểm cùng trên một đường thẳng không tạo được một tam giác có diện tích.",
            "chỉ a và b thẳng hàng": "Không chỉ A và B; đề đã cho cả C cũng nằm trên cùng đường thẳng đó.",
            "có": "C nằm lệch khỏi đường d trong khi A và B nằm trên d, nên cả ba không cùng một đường thẳng.",
            "luôn luôn": "Không thể nói luôn thẳng hàng khi dữ kiện cụ thể cho C nằm lệch khỏi đường chứa A và B.",
            "không thể biết từ vị trí của c": "Vị trí C lệch khỏi d chính là dữ kiện đủ để kết luận ba điểm không thẳng hàng.",
            "chỉ hai điểm có cùng nằm trên một đường thẳng": "Bất kỳ hai điểm đều xác định được một đường thẳng; để kiểm tra ba điểm thẳng hàng phải kiểm tra cả điểm thứ ba.",
            "ba điểm có cách đều nhau": "Khoảng cách bằng nhau không phải điều kiện định nghĩa thẳng hàng; điều cần kiểm tra là cùng nằm trên một đường thẳng.",
            "tên ba điểm có theo thứ tự bảng chữ cái": "Tên A, B, C không quyết định vị trí hình học; thứ tự chữ cái không chứng minh thẳng hàng.",
        }
        if label in reasons: return reasons[label]

    if skill == "QUADRILATERAL_RECOGNIZE":
        label = choice_text.strip().casefold()
        reasons = {
            "hình tam giác": "Tam giác có 3 cạnh, còn tứ giác phải là hình kín có đúng 4 cạnh.",
            "hình có 5 cạnh": "Hình có 5 cạnh không phải tứ giác vì tứ giác có đúng 4 cạnh.",
            "đường gấp khúc mở 4 đoạn": "Có 4 đoạn nhưng còn mở thì chưa tạo thành hình kín, nên chưa phải tứ giác.",
            "hình kín có 5 cạnh": "Hình đã kín nhưng có 5 cạnh, vượt quá đúng 4 cạnh của một tứ giác.",
            "đường gấp khúc mở": "Đường gấp khúc mở chưa khép kín, trong khi tứ giác là hình kín có 4 cạnh.",
        }
        if label in reasons: return reasons[label]

    if skill == "CYLINDER_RECOGNIZE":
        label = choice_text.strip().casefold()
        reasons = {
            "quả bóng": "Quả bóng gần dạng khối cầu, không có hai đáy tròn phẳng như khối trụ.",
            "hộp chữ nhật": "Hộp chữ nhật có các mặt phẳng hình chữ nhật, không có mặt cong bao quanh như khối trụ.",
            "tấm bìa phẳng": "Tấm bìa là vật gần dạng phẳng, không phải một khối có hai đáy và mặt cong.",
            "hai đáy vuông và các mặt phẳng xung quanh": "Khối trụ có hai đáy tròn và mặt cong xung quanh, không phải hai đáy vuông cùng các mặt phẳng.",
            "chỉ có một mặt tròn, không có mặt cong": "Khối trụ có hai đáy tròn và một mặt cong bao quanh, nên mô tả chỉ một mặt tròn là thiếu đặc trưng.",
            "có một đáy tròn và một đỉnh nhọn": "Đỉnh nhọn là đặc điểm của dạng nón, không phải khối trụ có hai đáy tròn song song.",
            "hình vuông": "Mặt trên và dưới của ống hình trụ là hai đáy tròn, không phải hình vuông.",
            "hình tam giác": "Khối trụ có đáy tròn, không có đáy hình tam giác.",
            "hình chữ nhật": "Mặt bên khi nhìn trải có thể liên hệ hình chữ nhật, nhưng mặt trên và mặt dưới của khối trụ là hình tròn.",
        }
        if label in reasons: return reasons[label]

    if skill == "SPHERE_RECOGNIZE":
        label = choice_text.strip().casefold()
        reasons = {
            "lon nước": "Lon nước gần dạng khối trụ với hai đáy tròn, không phải khối cầu tròn đều mọi phía.",
            "hộp chữ nhật": "Hộp chữ nhật có các mặt phẳng và cạnh, khác khối cầu không có cạnh hay đỉnh.",
            "thước thẳng": "Thước thẳng là vật dài và gần dạng phẳng, không gần dạng khối cầu.",
            "có cạnh nhưng không có đỉnh": "Khối cầu không có cạnh lẫn đỉnh, nên nói có cạnh là sai đặc trưng.",
            "không có cạnh nhưng có hai đỉnh": "Khối cầu không có cạnh và cũng không có đỉnh; hai đỉnh không thuộc đặc trưng của khối cầu.",
            "có cả cạnh và đỉnh": "Bề mặt khối cầu trơn liên tục nên không có cạnh hoặc đỉnh.",
            "khối cầu có hai đáy tròn song song": "Hai đáy tròn song song là đặc điểm của khối trụ, không phải khối cầu.",
            "khối cầu có một đáy phẳng và một đỉnh": "Khối cầu không có đáy phẳng hay đỉnh; bề mặt của nó tròn đều.",
            "khối cầu có các mặt phẳng và cạnh": "Khối cầu không được tạo bởi các mặt phẳng và không có cạnh.",
        }
        if label in reasons: return reasons[label]

    if skill == "FOLD_CUT_COMPOSE_SHAPES":
        label = choice_text.strip().casefold()
        prompt_cf = prompt.casefold()
        reasons = {
            "một điểm": "Ghép hai tam giác theo cạnh tạo một hình có diện tích, không thể thu lại thành một điểm đơn lẻ.",
            "một khối cầu": "Hai tam giác phẳng ghép theo cạnh vẫn tạo hình phẳng, không tự biến thành một khối cầu ba chiều.",
            "một đường thẳng vô hạn": "Hai tam giác là các hình hữu hạn; ghép chúng không thể tạo một đường thẳng kéo dài vô hạn.",
            "chỉ đặt hai đỉnh chạm nhau rồi để hở cạnh": "Chỉ chạm một đỉnh vẫn để hình hở; muốn kín phải ghép hai cạnh sát nhau.",
            "chồng khít hai tam giác lên cùng một vị trí": "Chồng hai mảnh không tạo tứ giác mới; cần đặt kề nhau theo cạnh.",
            "để hai tam giác cách xa nhau": "Hai mảnh cách xa nhau không tạo thành một hình kín chung.",
        }
        if label in reasons: return reasons[label]
        if "cắt một tờ giấy hình vuông" in prompt_cf:
            nums = [int(x) for x in re.findall(r"\d+", choice_text)]
            if nums and nums[0] != 2:
                return f"Một đường cắt thẳng từ một góc đến góc đối diện chia hình vuông thành đúng 2 mảnh; chọn {nums[0]} là sai số mảnh."

    if skill == "ADD_COMPONENTS_RECOGNIZE" and "hai số hạng" in prompt.casefold():
        match = re.search(r"(\d+)\s*\+\s*(\d+)\s*=\s*(\d+)", prompt)
        chosen = [int(x) for x in re.findall(r"\d+", choice_text)]
        if match and len(chosen) >= 2:
            left, right, total = map(int, match.groups())
            expected = [left, right]
            if chosen[:2] != expected:
                notes = []
                if total in chosen[:2]:
                    notes.append(f"{total} là tổng, không phải số hạng")
                missing = [str(x) for x in expected if x not in chosen[:2]]
                if missing:
                    notes.append("cặp này thiếu " + " và ".join(missing))
                return f"Hai số hạng phải là {left} và {right}; “{choice_text}” chưa đúng vì " + "; ".join(notes) + "."

    if skill == "MULTIPLICATION_COMPONENTS" and "hai thừa số" in prompt.casefold():
        match = re.search(r"(\d+)\s*×\s*(\d+)\s*=\s*(\d+)", prompt)
        chosen = [int(x) for x in re.findall(r"\d+", choice_text)]
        if match and len(chosen) >= 2:
            left, right, product = map(int, match.groups())
            expected = [left, right]
            if chosen[:2] != expected:
                notes = []
                if product in chosen[:2]:
                    notes.append(f"{product} là tích, không phải thừa số")
                missing = [str(x) for x in expected if x not in chosen[:2]]
                if missing:
                    notes.append("cặp này thiếu " + " và ".join(missing))
                return f"Hai thừa số phải là {left} và {right}; “{choice_text}” chưa đúng vì " + "; ".join(notes) + "."

    if skill == "SUB_COMPONENTS_RECOGNIZE" and "bạn bình gọi 90" in prompt.casefold():
        label = choice_text.strip().casefold()
        reasons = {
            "số trừ và hiệu": "Trong 90 - 35 = 55, 90 đứng trước dấu trừ nên là số bị trừ chứ không phải số trừ; 55 là hiệu.",
            "hiệu và số bị trừ": "Cặp này đã đảo vai trò: 90 không phải hiệu và 55 cũng không phải số bị trừ; 90 là số bị trừ, 55 là hiệu.",
            "số bị trừ và số trừ": "Tên của 90 đã đúng là số bị trừ, nhưng 55 là kết quả của phép trừ nên phải gọi là hiệu, không phải số trừ.",
        }
        if label in reasons: return reasons[label]

    if skill == "DIVISION_COMPONENTS" and "bạn lan nói trong 18" in prompt.casefold():
        label = choice_text.strip().casefold()
        reasons = {
            "số chia và thương": "Trong 18 : 2 = 9, 18 là lượng được đem chia nên là số bị chia chứ không phải số chia; 9 là thương.",
            "thương và số bị chia": "Cặp này đã đảo vai trò: 18 không phải thương và 9 không phải số bị chia; 18 là số bị chia, 9 là thương.",
            "số bị chia và số chia": "Tên của 18 đã đúng là số bị chia, nhưng 9 là kết quả của phép chia nên phải gọi là thương, không phải số chia.",
        }
        if label in reasons: return reasons[label]

    if skill == "NUM_FULL_HUNDREDS_RECOGNIZE" and prompt.strip().startswith("700") and choice_text.strip().casefold() == "sai":
        return "700 có hàng chục và hàng đơn vị đều bằng 0 nên đúng là một số tròn trăm; chọn Sai phủ nhận một tính chất đúng."

    if skill == "OPERATION_MEANING_FROM_VISUAL" and "Hai nhóm 4 chấm được gộp lại" in prompt and choice_text.strip() == "4 + 2":
        return "Dấu cộng đã phù hợp với việc gộp, nhưng hai nhóm đều có 4 chấm nên phải cộng 4 + 4; lựa chọn 4 + 2 đã đổi số chấm của nhóm thứ hai."

    if skill == "TIME_DAY_24_HOURS" and "6 giờ sáng hôm sau" in prompt.casefold() and choice_text.strip().casefold() == "sai":
        return "Từ 6 giờ sáng hôm nay đến đúng 6 giờ sáng hôm sau là đủ 24 giờ, tức một ngày đầy đủ; chọn Sai trái với quan hệ này."

    if skill == "MONEY_VND_NOTE_RECOGNITION" and "chỉ nhìn màu sắc" in prompt.casefold() and choice_text.strip().casefold() == "đúng":
        return "Màu sắc một mình không xác định chắc chắn giá trị tờ tiền; vẫn phải đọc con số mệnh giá và đơn vị đồng, nên chọn Đúng là sai."

    return None


def redundant_prerequisite_edges(graph: dict[str, list[str]]) -> list[tuple[str, str]]:
    redundant: list[tuple[str, str]] = []

    def ancestors(start: str) -> set[str]:
        seen: set[str] = set()
        stack = list(graph.get(start, []))
        while stack:
            node = stack.pop()
            if node in seen:
                continue
            seen.add(node)
            stack.extend(graph.get(node, []))
        return seen

    for skill, prereqs in graph.items():
        for prereq in prereqs:
            if any(prereq in ancestors(other) for other in prereqs if other != prereq):
                redundant.append((skill, prereq))
    return redundant


def load_json(path: Path, errors: list[str]) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        errors.append(f"missing_file:{path}")
        return {}
    except Exception as exc:
        errors.append(f"json_parse_error:{path}:{type(exc).__name__}:{exc}")
        return {}
    if not isinstance(value, dict):
        errors.append(f"root_not_object:{path}")
        return {}
    return value


def required_text(obj: dict, key: str, where: str, errors: list[str]) -> str:
    value = obj.get(key)
    if not isinstance(value, str) or not value.strip():
        errors.append(f"missing_text:{where}:{key}")
        return ""
    return value.strip()


def required_list(obj: dict, key: str, where: str, errors: list[str], min_len: int = 1) -> list:
    value = obj.get(key)
    if not isinstance(value, list) or len(value) < min_len:
        errors.append(f"missing_list:{where}:{key}")
        return []
    return value


def child_facing_internal_vocabulary(value: object) -> list[tuple[str, str, str]]:
    violations: list[tuple[str, str, str]] = []

    def scan(node: object, path: str, child_facing: bool = False) -> None:
        if isinstance(node, dict):
            for key, item in node.items():
                scan(item, f"{path}.{key}" if path else key, key in CHILD_FACING_KEYS)
            return
        if isinstance(node, list):
            for index, item in enumerate(node):
                scan(item, f"{path}[{index}]", child_facing)
            return
        if child_facing and isinstance(node, str):
            for match in INTERNAL_CHILD_VOCAB_RE.finditer(node):
                violations.append((path, match.group(0).lower(), node))

    scan(value, "")
    return violations


def child_facing_text_hygiene(value: object) -> list[tuple[str, str, str]]:
    violations: list[tuple[str, str, str]] = []

    def scan(node: object, path: str, child_facing: bool = False) -> None:
        if isinstance(node, dict):
            for key, item in node.items():
                scan(item, f"{path}.{key}" if path else key, key in CHILD_FACING_KEYS)
            return
        if isinstance(node, list):
            for index, item in enumerate(node):
                scan(item, f"{path}[{index}]", child_facing)
            return
        if not child_facing or not isinstance(node, str):
            return
        if unicodedata.normalize("NFC", node) != node:
            violations.append((path, "non_nfc", node))
        if any(unicodedata.category(ch) in {"Cc", "Cf"} for ch in node):
            violations.append((path, "control_or_format_character", node))
        if node != node.strip():
            violations.append((path, "outer_whitespace", node))
        if "  " in node:
            violations.append((path, "repeated_space", node))
        if re.search(r"\s+[,.!?;](?:\s|$)", node):
            violations.append((path, "space_before_punctuation", node))
        if re.search(r"[,.!?;](?=[A-Za-zÀ-ỹĐđ])", node):
            violations.append((path, "missing_space_after_punctuation", node))
        for match in re.finditer(r"\d(\s*)([+×=-])(\s*)\d", node):
            if match.group(1) != " " or match.group(3) != " ":
                violations.append((path, "numeric_operator_spacing", node))
                break

    scan(value, "")
    return violations


def child_facing_numeric_literal_violations(value: object, max_value: int = 1000) -> list[tuple[str, int, str]]:
    violations: list[tuple[str, int, str]] = []

    def scan(node: object, path: str, child_facing: bool = False) -> None:
        if isinstance(node, dict):
            for key, item in node.items():
                scan(item, f"{path}.{key}" if path else key, key in CHILD_FACING_KEYS)
            return
        if isinstance(node, list):
            for index, item in enumerate(node):
                scan(item, f"{path}[{index}]", child_facing)
            return
        if not child_facing:
            return
        if type(node) is int:
            if abs(node) > max_value:
                violations.append((path, node, str(node)))
            return
        if not isinstance(node, str):
            return
        # Plain integers and common thousands-group formatting (e.g. 1.200 / 1,200).
        for match in re.finditer(r"(?<![\w])([+-]?\d{1,3}(?:[.,]\d{3})+|[+-]?\d+)(?![\w])", node):
            token = match.group(1)
            compact = token
            if re.fullmatch(r"[+-]?\d{1,3}(?:[.,]\d{3})+", token):
                compact = token.replace(".", "").replace(",", "")
            try:
                number = int(compact)
            except ValueError:
                continue
            if abs(number) > max_value:
                violations.append((path, number, node))

    scan(value, "")
    return violations


def check_id(value: object, where: str, errors: list[str]) -> str:
    if not isinstance(value, str) or not ID_RE.fullmatch(value):
        errors.append(f"invalid_id:{where}:{value!r}")
        return ""
    return value


def normalize_prompt(text: str) -> str:
    text = text.lower().strip()
    text = re.sub(r"\d+", "#", text)
    text = re.sub(r"[^\w#]+", " ", text, flags=re.UNICODE)
    return re.sub(r"\s+", " ", text).strip()


def application_prompt_shape_similarity(prompt_a: str, prompt_b: str) -> float:
    """Compare prompt structure while ignoring numeric value swaps."""
    a = normalize_prompt(prompt_a)
    b = normalize_prompt(prompt_b)
    if not a or not b:
        return 0.0
    return difflib.SequenceMatcher(None, a, b).ratio()


def normalize_prompt_identity(text: str) -> str:
    text = text.lower().strip()
    text = re.sub(r"[^\w\d]+", " ", text, flags=re.UNICODE)
    return re.sub(r"\s+", " ", text).strip()


def prompts_are_near_duplicate(prompt_a: str, prompt_b: str, threshold: float = 0.95) -> bool:
    if not prompt_a or not prompt_b:
        return False
    shorter = min(len(prompt_a), len(prompt_b))
    longer = max(len(prompt_a), len(prompt_b))
    if longer == 0 or shorter / longer < threshold:
        return False
    matcher = difflib.SequenceMatcher(None, prompt_a, prompt_b)
    if matcher.quick_ratio() < threshold:
        return False
    return matcher.ratio() >= threshold


def worked_example_prompt_overlap(example_prompt: str, practice_prompt: str, threshold: float = 0.96) -> str | None:
    example = normalize_prompt_identity(example_prompt)
    practice = normalize_prompt_identity(practice_prompt)
    if not example or not practice:
        return None
    if example == practice:
        return "exact"
    if difflib.SequenceMatcher(None, example, practice).ratio() >= threshold:
        return "near"
    return None


def question_answer_display(question: dict) -> str:
    answer = question.get("correct_answer")
    if answer is None:
        return ""
    display = str(answer).strip()
    if question.get("answer_kind") == "integer" and isinstance(question.get("answer_unit"), str) and question["answer_unit"].strip():
        display += " " + question["answer_unit"].strip()
    return display


def explanation_states_answer(question: dict, explanation: str) -> bool:
    answer_display = question_answer_display(question)
    answer_evidence = normalize_prompt_identity(answer_display)
    explanation_evidence = normalize_prompt_identity(explanation)
    if answer_evidence:
        return answer_evidence in explanation_evidence
    return bool(answer_display and answer_display in explanation)


def hint_reveals_unseen_answer(question: dict, hint: str) -> bool:
    if question.get("question_type") == "true_false":
        return False
    answer = question.get("correct_answer")
    if answer is None:
        return False
    answer_kind = question.get("answer_kind")
    prompt = str(question.get("prompt_vi") or "")
    if answer_kind in {"integer", "interaction_integer"}:
        answer_token = str(answer)
        prompt_numbers = re.findall(r"(?<!\d)[+-]?\d+(?!\d)", prompt)
        hint_numbers = re.findall(r"(?<!\d)[+-]?\d+(?!\d)", hint)
        return answer_token not in prompt_numbers and answer_token in hint_numbers
    answer_text = str(answer).strip()
    answer_evidence = normalize_prompt_identity(answer_text)
    prompt_evidence = normalize_prompt_identity(prompt)
    hint_evidence = normalize_prompt_identity(hint)
    if answer_evidence:
        answer_phrase = f" {answer_evidence} "
        prompt_phrase = f" {prompt_evidence} "
        hint_phrase = f" {hint_evidence} "
        return answer_phrase not in prompt_phrase and answer_phrase in hint_phrase
    return bool(answer_text and answer_text not in prompt and answer_text in hint)


def grade2_operation_scope_violations(text: str) -> list[str]:
    violations: list[str] = []
    if not isinstance(text, str):
        return violations
    for match in GRADE2_MUL_LITERAL_RE.finditer(text):
        a, b = int(match.group(1)), int(match.group(2))
        if not ((a in {2, 5} and 0 <= b <= 10) or (b in {2, 5} and 0 <= a <= 10)):
            violations.append(f"mul:{a}x{b}")
    for match in GRADE2_DIV_LITERAL_RE.finditer(text):
        dividend, divisor = int(match.group(1)), int(match.group(2))
        if divisor not in {2, 5} or dividend % divisor != 0 or dividend // divisor > 10:
            violations.append(f"div:{dividend}:{divisor}")
    return violations


def addition_carry_count(a: int, b: int) -> int:
    carry = 0
    count = 0
    while a > 0 or b > 0:
        total = a % 10 + b % 10 + carry
        carry = 1 if total >= 10 else 0
        count += carry
        a //= 10
        b //= 10
    return count


def subtraction_borrow_count(a: int, b: int) -> int:
    if a < b:
        raise ValueError("negative_subtraction")
    borrow = 0
    count = 0
    while a > 0 or b > 0:
        top = a % 10 - borrow
        bottom = b % 10
        if top < bottom:
            borrow = 1
            count += 1
        else:
            borrow = 0
        a //= 10
        b //= 10
    return count


def eval_restricted_expression(text: str) -> Fraction:
    """Evaluate only numeric + - * / and parentheses, mirroring the engine's fail-closed contract."""
    if not isinstance(text, str) or not text.strip() or len(text) > 256:
        raise ValueError("empty_or_too_long")
    normalized = text.replace("×", "*").replace("÷", "/").replace("−", "-").replace("–", "-")
    tree = ast.parse(normalized, mode="eval")
    seen = 0

    def walk(node: ast.AST, depth: int = 0) -> Fraction:
        nonlocal seen
        seen += 1
        if seen > 96 or depth > 16:
            raise ValueError("expression_complexity")
        if isinstance(node, ast.Expression):
            return walk(node.body, depth + 1)
        if isinstance(node, ast.Constant) and type(node.value) in (int, float):
            if isinstance(node.value, float):
                return Fraction(str(node.value))
            return Fraction(node.value, 1)
        if isinstance(node, ast.UnaryOp) and isinstance(node.op, (ast.UAdd, ast.USub)):
            value = walk(node.operand, depth + 1)
            return value if isinstance(node.op, ast.UAdd) else -value
        if isinstance(node, ast.BinOp) and isinstance(node.op, (ast.Add, ast.Sub, ast.Mult, ast.Div)):
            left = walk(node.left, depth + 1)
            right = walk(node.right, depth + 1)
            if isinstance(node.op, ast.Add):
                return left + right
            if isinstance(node.op, ast.Sub):
                return left - right
            if isinstance(node.op, ast.Mult):
                return left * right
            if right == 0:
                raise ZeroDivisionError("division_by_zero")
            return left / right
        raise ValueError(f"unsupported_expression_node:{type(node).__name__}")

    return walk(tree)


def invalid_numeric_equalities(text: str) -> list[tuple[str, int, Fraction]]:
    if not isinstance(text, str) or not text:
        return []
    violations: list[tuple[str, int, Fraction]] = []
    seen: set[tuple[int, int]] = set()
    for pattern in NUMERIC_EQUALITY_RES:
        for match in pattern.finditer(text):
            span = (match.start(), match.end())
            if span in seen:
                continue
            seen.add(span)
            expression = re.sub(r"\s+:\s+", " / ", match.group(1).strip())
            expected = int(match.group(2))
            try:
                actual = eval_restricted_expression(expression)
            except (ArithmeticError, SyntaxError, ValueError, TypeError):
                actual = Fraction(expected + 1, 1)
            if actual != Fraction(expected, 1):
                violations.append((match.group(1).strip(), expected, actual))
    return violations


def validate_numeric_equalities(text: str, where: str, errors: list[str]) -> None:
    for expression, expected, actual in invalid_numeric_equalities(text):
        errors.append(f"invalid_instructional_numeric_equality:{where}:{expression}={expected}:actual={actual}")


def invalid_numeric_relations(text: str) -> list[tuple[int, str, int]]:
    if not isinstance(text, str) or not text:
        return []
    violations: list[tuple[int, str, int]] = []
    for match in NUMERIC_RELATION_RE.finditer(text):
        left = int(match.group(1))
        operator = match.group(2)
        right = int(match.group(3))
        valid = {
            "<": left < right,
            ">": left > right,
            "<=": left <= right,
            "≤": left <= right,
            ">=": left >= right,
            "≥": left >= right,
        }[operator]
        if not valid:
            violations.append((left, operator, right))
    return violations


def validate_numeric_relations(text: str, where: str, errors: list[str]) -> None:
    for left, operator, right in invalid_numeric_relations(text):
        errors.append(f"invalid_instructional_numeric_relation:{where}:{left}{operator}{right}")


def rationale_instructional_body(choice_text: str, rationale: str) -> str:
    if not isinstance(rationale, str):
        return ""
    prefix = f"“{choice_text}” chưa đúng. "
    return rationale[len(prefix):] if rationale.startswith(prefix) else rationale


def has_explicit_choice_contrast(choice_text: str, correct_text: str, rationale: str) -> bool:
    if not all(isinstance(value, str) for value in (choice_text, correct_text, rationale)):
        return False
    prefix = f"“{choice_text}” chưa đúng. Dữ kiện dẫn tới “{correct_text}”. "
    return rationale.startswith(prefix)


def normalize_choice_text(text: str) -> str:
    if not isinstance(text, str):
        return ""
    return " ".join(unicodedata.normalize("NFC", text).casefold().split())


def try_eval_numeric_choice(text: str) -> Fraction | None:
    if not isinstance(text, str) or not NUMERIC_CHOICE_EXPR_RE.fullmatch(text.strip()):
        return None
    normalized = re.sub(r"\s+:\s+", " / ", text.strip())
    try:
        return eval_restricted_expression(normalized)
    except (ArithmeticError, SyntaxError, ValueError, TypeError):
        return None


def prompt_mentions_answer_unit(prompt: object, answer_unit: object) -> bool:
    if not isinstance(prompt, str) or not isinstance(answer_unit, str):
        return False
    unit = answer_unit.strip().casefold()
    aliases = ANSWER_UNIT_PROMPT_ALIASES.get(unit)
    if not aliases:
        return False
    text = unicodedata.normalize("NFC", prompt.casefold())
    for alias in aliases:
        escaped = re.escape(alias.casefold())
        if re.search(rf"(?<![\w]){escaped}(?![\w])", text, flags=re.UNICODE):
            return True
    return False


def parse_unit_answer(text: str) -> tuple[Fraction, str]:
    if not isinstance(text, str):
        raise ValueError("unit_answer_not_text")
    match = re.fullmatch(r"\s*([+-]?\d+(?:[.,]\d+)?)\s+(.+?)\s*", text)
    if not match:
        raise ValueError("unit_answer_malformed")
    number_text = match.group(1).replace(",", ".")
    number = Fraction(number_text)
    unit = re.sub(r"\s+", " ", match.group(2).strip().lower()).rstrip(".")
    if not unit:
        raise ValueError("unit_missing")
    return number, unit


def validate_numeric_range(validation: dict, value: Fraction, where: str, errors: list[str]) -> None:
    minimum = validation.get("numeric_min")
    maximum = validation.get("numeric_max")
    if type(minimum) is not int or type(maximum) is not int or minimum > maximum:
        errors.append(f"invalid_numeric_range:{where}:{minimum!r}:{maximum!r}")
        return
    if value < minimum or value > maximum:
        errors.append(f"numeric_answer_out_of_range:{where}:{value}:{minimum}:{maximum}")


def find_cycle(graph: dict[str, list[str]]) -> list[str] | None:
    state: dict[str, int] = {}
    stack: list[str] = []

    def visit(node: str) -> list[str] | None:
        mark = state.get(node, 0)
        if mark == 1:
            try:
                i = stack.index(node)
            except ValueError:
                i = 0
            return stack[i:] + [node]
        if mark == 2:
            return None
        state[node] = 1
        stack.append(node)
        for nxt in graph.get(node, []):
            cycle = visit(nxt)
            if cycle:
                return cycle
        stack.pop()
        state[node] = 2
        return None

    for node in graph:
        cycle = visit(node)
        if cycle:
            return cycle
    return None


def baseline_traceability_violations(baseline: object) -> list[str]:
    if not isinstance(baseline, dict):
        return ["baseline_not_object"]
    violations: list[str] = []
    if baseline.get("schema_version") != 1:
        violations.append("baseline_bad_schema_version")
    if baseline.get("curriculum_id") != EXPECTED_CURRICULUM_ID:
        violations.append("baseline_curriculum_id_mismatch")
    if baseline.get("grade") != 2:
        violations.append("baseline_grade_mismatch")
    if baseline.get("subject") != "math":
        violations.append("baseline_subject_mismatch")
    if baseline.get("status") != EXPECTED_BASELINE_STATUS:
        violations.append("baseline_status_mismatch")
    source_ids = baseline.get("source_ids")
    if not isinstance(source_ids, list) or any(not isinstance(x, str) or not x.strip() for x in source_ids):
        violations.append("baseline_source_ids_invalid")
    else:
        normalized = [x.strip() for x in source_ids]
        if len(normalized) != len(set(normalized)):
            violations.append("baseline_source_ids_duplicate")
        missing = sorted(REQUIRED_BASELINE_SOURCE_IDS - set(normalized))
        if missing:
            violations.append("baseline_required_sources_missing:" + ",".join(missing))
    return violations


def baseline_hard_guard_violations(baseline: object) -> list[str]:
    if not isinstance(baseline, dict):
        return ["baseline_not_object"]
    hard_guards = baseline.get("hard_guards")
    if not isinstance(hard_guards, dict):
        return ["baseline_hard_guards_missing"]
    violations: list[str] = []
    for key, expected in EXPECTED_BASELINE_HARD_GUARDS.items():
        actual = hard_guards.get(key)
        if actual != expected:
            violations.append(f"baseline_hard_guard_mismatch:{key}:{actual!r}")
    return violations


def validate(baseline_path: Path, lesson_path: Path, question_path: Path) -> tuple[list[str], dict]:
    errors: list[str] = []
    baseline = load_json(baseline_path, errors)
    catalog = load_json(lesson_path, errors)
    bank = load_json(question_path, errors)
    if errors:
        return errors, {}

    errors.extend(baseline_traceability_violations(baseline))
    errors.extend(baseline_hard_guard_violations(baseline))

    for root, name in ((catalog, "lesson_catalog"), (bank, "question_bank")):
        if root.get("schema_version") != 1:
            errors.append(f"bad_schema_version:{name}")
        if root.get("subject") != "math":
            errors.append(f"bad_subject:{name}")
        if root.get("grade") != 2:
            errors.append(f"bad_grade:{name}")
        if root.get("curriculum_id") != baseline.get("curriculum_id"):
            errors.append(f"curriculum_mismatch:{name}")
        for path, term, _text in child_facing_internal_vocabulary(root):
            errors.append(f"internal_vocabulary_child_facing:{name}:{path}:{term}")
        for path, issue, _text in child_facing_text_hygiene(root):
            errors.append(f"child_facing_text_hygiene:{name}:{path}:{issue}")
        max_number = baseline.get("hard_guards", {}).get("max_number_baseline", 1000) if isinstance(baseline.get("hard_guards"), dict) else 1000
        for path, number, _text in child_facing_numeric_literal_violations(root, max_number):
            errors.append(f"child_facing_number_above_baseline:{name}:{path}:{number}:{max_number}")

    if catalog.get("catalog_id") != EXPECTED_CATALOG_ID:
        errors.append(f"catalog_id_mismatch:{catalog.get('catalog_id')!r}")
    if catalog.get("language") != EXPECTED_LANGUAGE:
        errors.append(f"catalog_language_mismatch:{catalog.get('language')!r}")
    if catalog.get("id_policy") != EXPECTED_ID_POLICY:
        errors.append(f"catalog_id_policy_mismatch:{catalog.get('id_policy')!r}")
    if bank.get("bank_id") != EXPECTED_BANK_ID:
        errors.append(f"bank_id_mismatch:{bank.get('bank_id')!r}")
    if bank.get("language") != EXPECTED_LANGUAGE:
        errors.append(f"bank_language_mismatch:{bank.get('language')!r}")

    baseline_skills = []
    skill_domain: dict[str, str] = {}
    domains = baseline.get("domains")
    if not isinstance(domains, dict):
        errors.append("baseline_domains_missing")
        domains = {}
    for domain, skills in domains.items():
        if not isinstance(skills, list):
            errors.append(f"baseline_domain_not_list:{domain}")
            continue
        baseline_skills.extend(skills)
        for skill_id in skills:
            if isinstance(skill_id, str):
                skill_domain[skill_id] = domain
    baseline_skill_set = set(baseline_skills)
    if len(baseline_skills) != len(baseline_skill_set):
        errors.append("baseline_duplicate_skill")

    chapters = catalog.get("chapters")
    topics = catalog.get("topics")
    lessons = catalog.get("lessons")
    questions = bank.get("questions")
    for value, label in ((chapters, "chapters"), (topics, "topics"), (lessons, "lessons"), (questions, "questions")):
        if not isinstance(value, list):
            errors.append(f"root_array_missing:{label}")
    chapters = chapters if isinstance(chapters, list) else []
    topics = topics if isinstance(topics, list) else []
    lessons = lessons if isinstance(lessons, list) else []
    questions = questions if isinstance(questions, list) else []

    all_ids: list[tuple[str, str]] = []
    chapter_ids: set[str] = set()
    topic_ids: set[str] = set()
    lesson_ids: set[str] = set()
    concept_ids: set[str] = set()
    example_ids: set[str] = set()
    question_ids: set[str] = set()

    chapter_domain: dict[str, str] = {}
    chapter_domain_counts = Counter()
    for i, chapter in enumerate(chapters):
        where = f"chapter[{i}]"
        if not isinstance(chapter, dict):
            errors.append(f"not_object:{where}")
            continue
        cid = check_id(chapter.get("id"), where, errors)
        if cid:
            chapter_ids.add(cid); all_ids.append((cid, where))
        required_text(chapter, "title_vi", where, errors)
        domain = required_text(chapter, "domain_key", where, errors)
        if domain and domain not in domains:
            errors.append(f"unknown_domain:{where}:{domain}")
        if domain:
            chapter_domain_counts[domain] += 1
        chapter_domain[cid] = domain

    for domain in domains:
        if chapter_domain_counts[domain] != 1:
            errors.append(f"chapter_domain_count:{domain}:{chapter_domain_counts[domain]}")

    topic_chapter: dict[str, str] = {}
    for i, topic in enumerate(topics):
        where = f"topic[{i}]"
        if not isinstance(topic, dict):
            errors.append(f"not_object:{where}")
            continue
        tid = check_id(topic.get("id"), where, errors)
        if tid:
            topic_ids.add(tid); all_ids.append((tid, where))
        cid = required_text(topic, "chapter_id", where, errors)
        if cid and cid not in chapter_ids:
            errors.append(f"bad_chapter_ref:{where}:{cid}")
        required_text(topic, "title_vi", where, errors)
        topic_chapter[tid] = cid

    lesson_by_id: dict[str, dict] = {}
    lesson_by_skill: dict[str, dict] = {}
    prereq_graph: dict[str, list[str]] = {}
    referenced_question_ids: list[str] = []
    lesson_skill_counts = Counter()
    topic_lesson_counts = Counter()
    lesson_orders_by_domain: dict[str, list[int]] = defaultdict(list)
    first_objective_counts = Counter()
    second_objective_counts = Counter()

    for i, lesson in enumerate(lessons):
        where = f"lesson[{i}]"
        if not isinstance(lesson, dict):
            errors.append(f"not_object:{where}")
            continue
        lid = check_id(lesson.get("id"), where, errors)
        if lid:
            lesson_ids.add(lid); all_ids.append((lid, where)); lesson_by_id[lid] = lesson
        skill = required_text(lesson, "skill_id", where, errors)
        if skill and lid and lid != "m2_ls_" + skill.lower():
            errors.append(f"lesson_id_skill_mismatch:{where}:{lid}:{skill}")
        order_in_domain = lesson.get("order_in_domain")
        if type(order_in_domain) is not int or order_in_domain < 1:
            errors.append(f"invalid_lesson_order:{where}:{order_in_domain!r}")
        elif skill in skill_domain:
            lesson_orders_by_domain[skill_domain[skill]].append(order_in_domain)
        if skill not in baseline_skill_set:
            errors.append(f"unknown_lesson_skill:{where}:{skill}")
        lesson_skill_counts[skill] += 1
        if skill in lesson_by_skill:
            errors.append(f"duplicate_lesson_skill:{skill}")
        lesson_by_skill[skill] = lesson
        cid = required_text(lesson, "chapter_id", where, errors)
        tid = required_text(lesson, "topic_id", where, errors)
        if cid not in chapter_ids:
            errors.append(f"bad_lesson_chapter_ref:{where}:{cid}")
        if tid not in topic_ids:
            errors.append(f"bad_lesson_topic_ref:{where}:{tid}")
        else:
            topic_lesson_counts[tid] += 1
        if tid in topic_chapter and cid and topic_chapter[tid] != cid:
            errors.append(f"topic_chapter_mismatch:{where}:{tid}:{cid}")
        if cid in chapter_domain and skill in baseline_skill_set:
            expected_domain = next((d for d, skills in domains.items() if skill in skills), None)
            if chapter_domain[cid] != expected_domain:
                errors.append(f"skill_domain_mismatch:{where}:{skill}:{chapter_domain[cid]}:{expected_domain}")
        lesson_title = required_text(lesson, "title_vi", where, errors)
        if lesson_title and len(lesson_title) > MAX_LESSON_TITLE_CHARS:
            errors.append(f"lesson_title_too_long:{where}:{len(lesson_title)}")
        lesson_explanation = required_text(lesson, "explanation_vi", where, errors)
        if lesson_explanation and len(lesson_explanation) > MAX_LESSON_EXPLANATION_CHARS:
            errors.append(f"lesson_explanation_too_long:{where}:{len(lesson_explanation)}")
        validate_numeric_equalities(lesson_explanation, where + ".explanation_vi", errors)
        validate_numeric_relations(lesson_explanation, where + ".explanation_vi", errors)
        serialized_lesson = json.dumps(lesson, ensure_ascii=False)
        for violation in grade2_operation_scope_violations(serialized_lesson):
            errors.append(f"out_of_scope_grade2_operation:{where}:{violation}")
        if skill == "MONEY_VND_NOTE_RECOGNITION" and MONEY_DENOMINATION_RE.search(serialized_lesson):
            errors.append(f"unsourced_money_denomination:{where}")
        if skill in TIME_RELATION_SKILLS and TIME_OUT_OF_SCOPE_ARITH_RE.search(serialized_lesson):
            errors.append(f"out_of_scope_time_arithmetic:{where}")
        objectives = required_list(lesson, "objectives_vi", where, errors, 2)
        if len(objectives) < 2:
            errors.append(f"insufficient_objectives:{where}")
        elif not all(isinstance(objective, str) and objective.strip() for objective in objectives):
            errors.append(f"invalid_objective_text:{where}")
        else:
            for objective_index, objective in enumerate(objectives):
                if len(objective.strip()) > MAX_OBJECTIVE_CHARS:
                    errors.append(f"objective_too_long:{where}:{objective_index}:{len(objective.strip())}")
            first_objective = " ".join(objectives[0].split())
            first_objective_counts[first_objective] += 1
            if first_objective.startswith(GENERIC_FIRST_OBJECTIVE_PREFIX):
                errors.append(f"generic_first_objective:{where}")
            second_objective = " ".join(objectives[1].split())
            second_objective_counts[second_objective] += 1
            if second_objective == GENERIC_SECOND_OBJECTIVE:
                errors.append(f"generic_second_objective:{where}")
        concepts = required_list(lesson, "concepts", where, errors)
        for j, concept in enumerate(concepts):
            cwhere = f"{where}.concept[{j}]"
            if not isinstance(concept, dict):
                errors.append(f"not_object:{cwhere}"); continue
            x = check_id(concept.get("id"), cwhere, errors)
            if x:
                concept_ids.add(x); all_ids.append((x, cwhere))
                expected_concept_id = f"m2_cp_{skill.lower()}_{j + 1:02d}"
                if x != expected_concept_id:
                    errors.append(f"concept_id_skill_ordinal_mismatch:{cwhere}:{x}:{expected_concept_id}")
            concept_name = required_text(concept, "name_vi", cwhere, errors)
            if concept_name and len(concept_name) > MAX_CONCEPT_NAME_CHARS:
                errors.append(f"concept_name_too_long:{cwhere}:{len(concept_name)}")
            concept_definition = required_text(concept, "definition_vi", cwhere, errors)
            if concept_definition and len(concept_definition) < MIN_CONCEPT_DEFINITION_CHARS:
                errors.append(f"concept_definition_too_short:{cwhere}:{len(concept_definition)}")
            if concept_definition and len(concept_definition) > MAX_CONCEPT_DEFINITION_CHARS:
                errors.append(f"concept_definition_too_long:{cwhere}:{len(concept_definition)}")
            validate_numeric_equalities(concept_definition, cwhere + ".definition_vi", errors)
            validate_numeric_relations(concept_definition, cwhere + ".definition_vi", errors)
        examples = required_list(lesson, "worked_examples", where, errors)
        for j, example in enumerate(examples):
            ewhere = f"{where}.example[{j}]"
            if not isinstance(example, dict):
                errors.append(f"not_object:{ewhere}"); continue
            x = check_id(example.get("id"), ewhere, errors)
            if x:
                example_ids.add(x); all_ids.append((x, ewhere))
                expected_example_id = f"m2_ex_{skill.lower()}_{j + 1:02d}"
                if x != expected_example_id:
                    errors.append(f"example_id_skill_ordinal_mismatch:{ewhere}:{x}:{expected_example_id}")
            example_prompt = required_text(example, "prompt_vi", ewhere, errors)
            if example_prompt and len(example_prompt) > MAX_WORKED_PROMPT_CHARS:
                errors.append(f"worked_prompt_too_long:{ewhere}:{len(example_prompt)}")
            answer = required_text(example, "answer", ewhere, errors)
            if answer and len(answer) > MAX_WORKED_ANSWER_CHARS:
                errors.append(f"worked_answer_too_long:{ewhere}:{len(answer)}")
            validate_numeric_equalities(answer, ewhere + ".answer", errors)
            validate_numeric_relations(answer, ewhere + ".answer", errors)
            solution_steps = required_list(example, "solution_steps_vi", ewhere, errors)
            if solution_steps:
                for step_index, step in enumerate(solution_steps):
                    if isinstance(step, str):
                        if len(step.strip()) > MAX_WORKED_STEP_CHARS:
                            errors.append(f"worked_step_too_long:{ewhere}:{step_index}:{len(step.strip())}")
                        validate_numeric_equalities(step, f"{ewhere}.solution_steps_vi[{step_index}]", errors)
                        validate_numeric_relations(step, f"{ewhere}.solution_steps_vi[{step_index}]", errors)
            if answer and solution_steps and all(isinstance(step, str) for step in solution_steps):
                answer_evidence = normalize_prompt_identity(answer)
                solution_evidence = normalize_prompt_identity(" ".join(solution_steps))
                if answer_evidence and answer_evidence not in solution_evidence:
                    errors.append(f"worked_example_answer_not_explicit:{ewhere}:{answer[:80]}")
        prereqs = lesson.get("prerequisite_skills", [])
        if not isinstance(prereqs, list):
            errors.append(f"prerequisite_not_list:{where}")
            prereqs = []
        for p in prereqs:
            if p not in baseline_skill_set:
                errors.append(f"bad_prerequisite:{where}:{p}")
            if p == skill:
                errors.append(f"self_prerequisite:{where}:{skill}")
        prereq_graph[skill] = [p for p in prereqs if isinstance(p, str)]
        span = lesson.get("difficulty_span")
        if not isinstance(span, list) or set(span) != DIFFICULTIES:
            errors.append(f"bad_difficulty_span:{where}:{span!r}")
        practice = lesson.get("practice_sets")
        if not isinstance(practice, dict):
            errors.append(f"missing_practice_sets:{where}")
        else:
            if set(practice) != DIFFICULTIES:
                errors.append(f"practice_difficulty_keys:{where}:{sorted(practice)}")
            for diff in DIFFICULTIES:
                refs = practice.get(diff)
                if not isinstance(refs, list) or not refs:
                    errors.append(f"empty_practice_set:{where}:{diff}")
                else:
                    referenced_question_ids.extend([x for x in refs if isinstance(x, str)])
        if lesson.get("status") != "CHILD_READY":
            errors.append(f"lesson_not_child_ready:{where}")

    # Sibling 2/5 table lessons need distinct objectives, not one template with a digit swapped.
    for left_skill, right_skill in (("TIMES_TABLE_2", "TIMES_TABLE_5"), ("DIVIDE_TABLE_2", "DIVIDE_TABLE_5")):
        left_lesson = lesson_by_skill.get(left_skill)
        right_lesson = lesson_by_skill.get(right_skill)
        if not isinstance(left_lesson, dict) or not isinstance(right_lesson, dict):
            continue
        left_objectives = left_lesson.get("objectives_vi")
        right_objectives = right_lesson.get("objectives_vi")
        if not isinstance(left_objectives, list) or not isinstance(right_objectives, list):
            continue
        for index in range(min(2, len(left_objectives), len(right_objectives))):
            left_objective = left_objectives[index]
            right_objective = right_objectives[index]
            if not isinstance(left_objective, str) or not isinstance(right_objective, str):
                continue
            if normalize_prompt(left_objective) == normalize_prompt(right_objective):
                errors.append(
                    f"table_family_objective_template_duplicate:{left_skill}:{right_skill}:objective{index + 1}")

    for tid in sorted(topic_ids):
        if topic_lesson_counts[tid] < 1:
            errors.append(f"empty_topic:{tid}")
    for domain, skills in domains.items():
        if not isinstance(skills, list):
            continue
        actual_orders = sorted(lesson_orders_by_domain.get(domain, []))
        expected_orders = list(range(1, len(skills) + 1))
        if actual_orders != expected_orders:
            errors.append(f"non_contiguous_domain_lesson_order:{domain}:{actual_orders}:{expected_orders}")

    missing_lesson_skills = sorted(baseline_skill_set - set(lesson_by_skill))
    extra_lesson_skills = sorted(set(lesson_by_skill) - baseline_skill_set)
    for s in missing_lesson_skills:
        errors.append(f"missing_lesson_for_skill:{s}")
    for s in extra_lesson_skills:
        errors.append(f"extra_lesson_skill:{s}")
    for skill, count in lesson_skill_counts.items():
        if count != 1:
            errors.append(f"lesson_skill_count:{skill}:{count}")
    for objective_text, count in first_objective_counts.items():
        if count > 3:
            errors.append(f"over_reused_first_objective:{count}:{objective_text[:80]}")
    for objective_text, count in second_objective_counts.items():
        if count > 3:
            errors.append(f"over_reused_second_objective:{count}:{objective_text[:80]}")

    cycle = find_cycle(prereq_graph)
    if cycle:
        errors.append("prerequisite_cycle:" + "->".join(cycle))
    else:
        for skill, prereq in redundant_prerequisite_edges(prereq_graph):
            errors.append(f"redundant_direct_prerequisite:{skill}:{prereq}")

    chapter_position = {x.get("id"): i for i, x in enumerate(chapters) if isinstance(x, dict)}
    topic_position = {x.get("id"): i for i, x in enumerate(topics) if isinstance(x, dict)}
    for skill, lesson in lesson_by_skill.items():
        target_key = (
            chapter_position.get(lesson.get("chapter_id"), 10**9),
            topic_position.get(lesson.get("topic_id"), 10**9),
            lesson.get("order_in_domain") if type(lesson.get("order_in_domain")) is int else 10**9,
        )
        for prereq in lesson.get("prerequisite_skills", []):
            prereq_lesson = lesson_by_skill.get(prereq)
            if not isinstance(prereq_lesson, dict):
                continue
            prereq_key = (
                chapter_position.get(prereq_lesson.get("chapter_id"), 10**9),
                topic_position.get(prereq_lesson.get("topic_id"), 10**9),
                prereq_lesson.get("order_in_domain") if type(prereq_lesson.get("order_in_domain")) is int else 10**9,
            )
            if prereq_key >= target_key:
                errors.append(f"forward_prerequisite:{skill}:{prereq}")

    question_by_id: dict[str, dict] = {}
    question_counts_by_skill = Counter()
    question_counts_by_difficulty = Counter()
    prompts_by_lesson: dict[str, list[tuple[str, str]]] = defaultdict(list)
    all_question_prompts: list[tuple[str, str, str]] = []
    first_hint_counts = Counter()
    second_hint_counts = Counter()
    distractor_rationale_counts = Counter()
    question_explanation_ids: dict[str, list[str]] = defaultdict(list)
    correct_choice_positions_by_count: dict[int, Counter] = defaultdict(Counter)
    question_ordinals_by_skill: dict[str, list[int]] = defaultdict(list)

    for i, q in enumerate(questions):
        where = f"question[{i}]"
        if not isinstance(q, dict):
            errors.append(f"not_object:{where}")
            continue
        qid = check_id(q.get("id"), where, errors)
        if qid:
            question_ids.add(qid); all_ids.append((qid, where)); question_by_id[qid] = q
        lid = required_text(q, "lesson_id", where, errors)
        skill = required_text(q, "skill_id", where, errors)
        if lid not in lesson_by_id:
            errors.append(f"orphan_question_lesson:{where}:{lid}")
        elif lesson_by_id[lid].get("skill_id") != skill:
            errors.append(f"question_skill_lesson_mismatch:{where}:{skill}:{lid}")
        if skill not in baseline_skill_set:
            errors.append(f"unknown_question_skill:{where}:{skill}")
        if skill and qid:
            prefix = "m2_q_" + skill.lower() + "_"
            suffix = qid[len(prefix):] if qid.startswith(prefix) else ""
            if not qid.startswith(prefix) or not re.fullmatch(r"\d{2}", suffix):
                errors.append(f"question_id_skill_ordinal_mismatch:{where}:{qid}:{prefix}NN")
            else:
                question_ordinals_by_skill[skill].append(int(suffix))
        question_counts_by_skill[skill] += 1
        difficulty = q.get("difficulty")
        if difficulty not in DIFFICULTIES:
            errors.append(f"invalid_difficulty:{where}:{difficulty!r}")
        else:
            question_counts_by_difficulty[difficulty] += 1
        prompt = required_text(q, "prompt_vi", where, errors)
        if prompt and len(prompt) > MAX_QUESTION_PROMPT_CHARS:
            errors.append(f"question_prompt_too_long:{where}:{len(prompt)}")
        if prompt:
            prompts_by_lesson[lid].append((qid, prompt))
            all_question_prompts.append((lid, qid, prompt))
        explanation = required_text(q, "explanation_vi", where, errors)
        if explanation and qid:
            question_explanation_ids[" ".join(explanation.split()).casefold()].append(qid)
        validate_numeric_equalities(explanation, where + ".explanation_vi", errors)
        validate_numeric_relations(explanation, where + ".explanation_vi", errors)
        if SHALLOW_NUMERIC_EXPLANATION_MARKER in explanation.casefold():
            errors.append(f"shallow_numeric_explanation:{where}")
        if GENERIC_MULDIV_EXPLANATION_MARKER in explanation.casefold():
            errors.append(f"generic_muldiv_explanation:{where}")
        if GENERIC_WORD_PROBLEM_EXPLANATION_MARKER in explanation.casefold():
            errors.append(f"generic_word_problem_explanation:{where}")
        if explanation and len(explanation) < MIN_QUESTION_EXPLANATION_CHARS:
            errors.append(f"question_explanation_too_short:{where}:{len(explanation)}")
        if explanation and len(explanation) > MAX_QUESTION_EXPLANATION_CHARS:
            errors.append(f"question_explanation_too_long:{where}:{len(explanation)}")
        if explanation and not explanation_states_answer(q, explanation):
            errors.append(f"question_explanation_missing_answer_evidence:{where}:{question_answer_display(q)[:80]}")
        correct_answer_text = q.get("correct_answer")
        if isinstance(correct_answer_text, str):
            validate_numeric_equalities(correct_answer_text, where + ".correct_answer", errors)
            validate_numeric_relations(correct_answer_text, where + ".correct_answer", errors)
        serialized_question = json.dumps(q, ensure_ascii=False)
        for violation in grade2_operation_scope_violations(serialized_question):
            errors.append(f"out_of_scope_grade2_operation:{where}:{violation}")
        if skill == "MONEY_VND_NOTE_RECOGNITION" and MONEY_DENOMINATION_RE.search(serialized_question):
            errors.append(f"unsourced_money_denomination:{where}")
        if skill in TIME_RELATION_SKILLS and TIME_OUT_OF_SCOPE_ARITH_RE.search(serialized_question):
            errors.append(f"out_of_scope_time_arithmetic:{where}")
        if skill in ADD_CARRY_RULES or skill in SUB_BORROW_RULES:
            operands = [int(x) for x in re.findall(r"\d+", prompt)]
            if len(operands) < 2:
                errors.append(f"missing_written_arithmetic_operands:{where}:{skill}")
            else:
                a, b = operands[0], operands[1]
                answer = q.get("correct_answer")
                if skill in ADD_CARRY_RULES:
                    expected = a + b
                    actual_transfers = addition_carry_count(a, b)
                    expected_transfers = ADD_CARRY_RULES[skill]
                else:
                    expected = a - b
                    actual_transfers = subtraction_borrow_count(a, b) if a >= b else -1
                    expected_transfers = SUB_BORROW_RULES[skill]
                if answer != expected:
                    errors.append(f"written_arithmetic_answer_mismatch:{where}:{a}:{b}:{answer}:{expected}")
                if actual_transfers != expected_transfers:
                    errors.append(f"written_arithmetic_transfer_count:{where}:{skill}:{actual_transfers}:{expected_transfers}")
        hints = required_list(q, "hints_vi", where, errors, 2)
        if len(hints) < 2 or any(not isinstance(x, str) or not x.strip() for x in hints):
            errors.append(f"invalid_hints:{where}")
        else:
            for hint_index, hint_text in enumerate(hints):
                validate_numeric_equalities(hint_text, f"{where}.hints_vi[{hint_index}]", errors)
                validate_numeric_relations(hint_text, f"{where}.hints_vi[{hint_index}]", errors)
                if len(hint_text.strip()) > MAX_HINT_CHARS:
                    errors.append(f"hint_too_long:{where}:{hint_index + 1}:{len(hint_text.strip())}")
                if hint_reveals_unseen_answer(q, hint_text):
                    errors.append(f"hint_reveals_unseen_answer:{where}:{hint_index + 1}")
            first_hint = " ".join(hints[0].split())
            first_hint_counts[first_hint] += 1
            if SHALLOW_FIRST_HINT_MARKER in first_hint.casefold():
                errors.append(f"shallow_first_hint:{where}")
            second_hint = " ".join(hints[1].split())
            second_hint_counts[second_hint] += 1
            if second_hint == GENERIC_SECOND_HINT:
                errors.append(f"generic_second_hint:{where}")
            if SHALLOW_SECOND_HINT_MARKER in second_hint.casefold():
                errors.append(f"shallow_second_hint:{where}")
        question_type = required_text(q, "question_type", where, errors)
        if question_type not in QUESTION_TYPES:
            errors.append(f"unsupported_question_type:{where}:{question_type!r}")
        tags = required_list(q, "tags", where, errors, 5)
        if len(tags) != len(set(tags)):
            errors.append(f"duplicate_question_tag:{where}")
        expected_tags = {skill.lower(), skill_domain.get(skill, ""), difficulty, question_type, q.get("answer_kind")}
        expected_tags.discard("")
        actual_tags = set(tags)
        if actual_tags != expected_tags or len(tags) != len(expected_tags):
            missing_tags = sorted(expected_tags - actual_tags)
            extra_tags = sorted(actual_tags - expected_tags)
            errors.append(f"question_tag_contract_mismatch:{where}:missing={missing_tags}:extra={extra_tags}")
        if q.get("status") != "CHILD_READY":
            errors.append(f"question_not_child_ready:{where}")

        kind = q.get("answer_kind")
        if kind not in ENGINE_ANSWER_KINDS:
            errors.append(f"unsupported_answer_kind:{where}:{kind!r}")
            continue
        if kind not in GRADE2_USED_ANSWER_KINDS:
            errors.append(f"answer_kind_outside_grade2_baseline:{where}:{kind}")
        if kind not in tags:
            errors.append(f"missing_answer_kind_tag:{where}:{kind}")
        accepted = q.get("accepted_answers")
        if not isinstance(accepted, list) or not accepted or any(not isinstance(x, str) or not x.strip() for x in accepted):
            errors.append(f"missing_accepted_answer:{where}")
            accepted = []
        normalized_accepted = [re.sub(r"\s+", " ", x.strip().casefold()) for x in accepted]
        if len(normalized_accepted) != len(set(normalized_accepted)):
            errors.append(f"duplicate_accepted_answer:{where}")
        validation = q.get("validation")
        if not isinstance(validation, dict):
            errors.append(f"missing_validation:{where}")
            validation = {}
        expected_validation_keys = {
            "integer": {"integer_required", "numeric_min", "numeric_max"},
            "interaction_integer": {"integer_required", "numeric_min", "numeric_max"},
            "expression": {"allowed_operators", "expected_numeric", "expression_syntax", "numeric_min", "numeric_max"},
            "unit": {"expected_numeric", "numeric_min", "numeric_max"},
            "text": {"choice_count", "single_correct"},
        }.get(kind)
        if expected_validation_keys is not None and set(validation) != expected_validation_keys:
            errors.append(f"validation_schema_mismatch:{where}:{sorted(validation)}:{sorted(expected_validation_keys)}")
        expected_skill_numeric_max = SKILL_NUMERIC_MAX.get(skill)
        if expected_skill_numeric_max is not None and kind in {"integer", "interaction_integer"}:
            if validation.get("numeric_min") != 0 or validation.get("numeric_max") != expected_skill_numeric_max:
                errors.append(f"skill_numeric_range_metadata_mismatch:{where}:{skill}:{validation.get('numeric_min')}:{validation.get('numeric_max')}:{expected_skill_numeric_max}")
        if "answer_unit" in q:
            answer_unit = q.get("answer_unit")
            if not isinstance(answer_unit, str) or not answer_unit.strip():
                errors.append(f"invalid_answer_unit:{where}:{answer_unit!r}")
            elif kind != "integer":
                errors.append(f"answer_unit_requires_integer_kind:{where}:{kind}")
            elif question_type not in {"numeric_input", "word_problem"}:
                errors.append(f"answer_unit_question_type_mismatch:{where}:{question_type}")
            elif not prompt_mentions_answer_unit(prompt, answer_unit):
                errors.append(f"answer_unit_not_stated_in_prompt:{where}:{answer_unit}")

        if kind in {"integer", "interaction_integer"}:
            answer = q.get("correct_answer")
            if type(answer) is not int:
                errors.append(f"numeric_answer_not_integer:{where}:{answer!r}")
            else:
                validate_numeric_range(validation, Fraction(answer, 1), where, errors)
                if str(answer) not in accepted:
                    errors.append(f"numeric_answer_not_accepted:{where}:{answer}")
                for accepted_answer in accepted:
                    if not re.fullmatch(r"[+-]?\d+", accepted_answer.strip()):
                        errors.append(f"numeric_accepted_answer_invalid:{where}:{accepted_answer!r}")
                        continue
                    if int(accepted_answer.strip()) != answer:
                        errors.append(f"numeric_accepted_answer_wrong_value:{where}:{accepted_answer!r}:{answer}")
            if validation.get("integer_required") is not True:
                errors.append(f"numeric_integer_required_missing:{where}")
            if kind == "interaction_integer" and question_type != "interactive_measurement":
                errors.append(f"interaction_question_type_mismatch:{where}:{question_type}")
            if kind == "integer" and question_type not in {"numeric_input", "word_problem"}:
                errors.append(f"integer_question_type_mismatch:{where}:{question_type}")

        elif kind == "expression":
            if question_type != "expression_input":
                errors.append(f"expression_question_type_mismatch:{where}:{question_type}")
            expression = q.get("correct_answer")
            if validation.get("expression_syntax") != "restricted_numeric_arithmetic":
                errors.append(f"expression_syntax_metadata_invalid:{where}")
            allowed_operators = validation.get("allowed_operators")
            if (skill == "ADD_SUB_TWO_OPERATORS_LEFT_TO_RIGHT" and
                    (not isinstance(allowed_operators, list) or
                     set(allowed_operators) != ADD_SUB_EXPRESSION_ALLOWED_OPERATORS or
                     len(allowed_operators) != len(ADD_SUB_EXPRESSION_ALLOWED_OPERATORS))):
                errors.append(f"add_sub_expression_allowed_operators_mismatch:{where}:{allowed_operators!r}")
            if isinstance(expression, str):
                used_operators = set(re.findall(r"[+\-*/()]", expression))
                declared = set(allowed_operators) if isinstance(allowed_operators, list) else set()
                if not used_operators.issubset(declared):
                    errors.append(f"expression_uses_undeclared_operator:{where}:{sorted(used_operators - declared)}")
            expected = validation.get("expected_numeric")
            if type(expected) is not int:
                errors.append(f"expression_expected_numeric_invalid:{where}:{expected!r}")
                expected_fraction = None
            else:
                expected_fraction = Fraction(expected, 1)
                validate_numeric_range(validation, expected_fraction, where, errors)
            try:
                actual_fraction = eval_restricted_expression(expression)
            except ZeroDivisionError:
                errors.append(f"expression_division_by_zero:{where}")
                actual_fraction = None
            except (SyntaxError, ValueError, TypeError) as exc:
                errors.append(f"malformed_expression:{where}:{type(exc).__name__}")
                actual_fraction = None
            if actual_fraction is not None and expected_fraction is not None and actual_fraction != expected_fraction:
                errors.append(f"expression_answer_mismatch:{where}:{actual_fraction}:{expected_fraction}")
            for accepted_answer in accepted:
                try:
                    parsed = eval_restricted_expression(accepted_answer)
                except (SyntaxError, ValueError, TypeError, ZeroDivisionError):
                    errors.append(f"malformed_accepted_expression:{where}:{accepted_answer!r}")
                    continue
                if expected_fraction is not None and parsed != expected_fraction:
                    errors.append(f"accepted_expression_not_equivalent:{where}:{accepted_answer!r}")

        elif kind == "unit":
            if question_type != "unit_input":
                errors.append(f"unit_question_type_mismatch:{where}:{question_type}")
            expected_unit = required_text(q, "expected_unit", where, errors).lower().rstrip(".")
            accepted_units = q.get("accepted_units")
            if not isinstance(accepted_units, list) or not accepted_units or any(not isinstance(x, str) or not x.strip() for x in accepted_units):
                errors.append(f"invalid_accepted_units:{where}")
                accepted_units = []
            normalized_units = {re.sub(r"\s+", " ", x.strip().lower()).rstrip(".") for x in accepted_units if isinstance(x, str)}
            if expected_unit and expected_unit not in normalized_units:
                errors.append(f"expected_unit_not_accepted:{where}:{expected_unit}")
            expected = validation.get("expected_numeric")
            if type(expected) is not int:
                errors.append(f"unit_expected_numeric_invalid:{where}:{expected!r}")
                expected_fraction = None
            else:
                expected_fraction = Fraction(expected, 1)
                validate_numeric_range(validation, expected_fraction, where, errors)
            try:
                number, unit = parse_unit_answer(q.get("correct_answer"))
            except (ValueError, ZeroDivisionError) as exc:
                errors.append(f"malformed_unit_answer:{where}:{type(exc).__name__}")
            else:
                if expected_fraction is not None and number != expected_fraction:
                    errors.append(f"unit_numeric_mismatch:{where}:{number}:{expected_fraction}")
                if unit not in normalized_units:
                    errors.append(f"unit_not_accepted:{where}:{unit}")
            if q.get("correct_answer") not in accepted:
                errors.append(f"unit_correct_answer_not_accepted:{where}")
            for accepted_answer in accepted:
                try:
                    accepted_number, accepted_unit = parse_unit_answer(accepted_answer)
                except (ValueError, ZeroDivisionError) as exc:
                    errors.append(f"malformed_accepted_unit_answer:{where}:{accepted_answer!r}:{type(exc).__name__}")
                    continue
                if expected_fraction is not None and accepted_number != expected_fraction:
                    errors.append(f"accepted_unit_numeric_mismatch:{where}:{accepted_answer!r}")
                if accepted_unit not in normalized_units:
                    errors.append(f"accepted_unit_not_allowed:{where}:{accepted_answer!r}:{accepted_unit}")

        elif kind == "text":
            if question_type not in {"multiple_choice", "true_false"}:
                errors.append(f"text_question_type_mismatch:{where}:{question_type}")
            choices = q.get("choices")
            if not isinstance(choices, list) or not (2 <= len(choices) <= 5):
                errors.append(f"invalid_choice_count:{where}")
                choices = []
            choice_ids = []
            choice_texts = []
            choice_rationales = []
            for j, choice in enumerate(choices):
                cwhere = f"{where}.choice[{j}]"
                if not isinstance(choice, dict):
                    errors.append(f"choice_not_object:{cwhere}"); continue
                cid = required_text(choice, "id", cwhere, errors)
                text = required_text(choice, "text", cwhere, errors)
                if text and len(text) > MAX_CHOICE_TEXT_CHARS:
                    errors.append(f"choice_text_too_long:{cwhere}:{len(text)}")
                rationale = required_text(choice, "rationale_vi", cwhere, errors)
                choice_ids.append(cid); choice_texts.append(text); choice_rationales.append(rationale)
            if len(choice_ids) != len(set(choice_ids)):
                errors.append(f"duplicate_choice_id:{where}")
            if len(choice_texts) != len(set(choice_texts)):
                errors.append(f"duplicate_choice_text:{where}")
            normalized_choice_texts = [normalize_choice_text(x) for x in choice_texts]
            if len(normalized_choice_texts) != len(set(normalized_choice_texts)):
                errors.append(f"duplicate_choice_text_normalized:{where}")
            numeric_choice_values: dict[Fraction, list[str]] = defaultdict(list)
            for cid, text in zip(choice_ids, choice_texts):
                value = try_eval_numeric_choice(text)
                if value is not None:
                    numeric_choice_values[value].append(cid)
            for value, ids in numeric_choice_values.items():
                if len(ids) > 1:
                    errors.append(f"duplicate_choice_numeric_value:{where}:{value}:{','.join(ids)}")
            correct_id = q.get("correct_choice_id")
            correct_text = q.get("correct_answer")
            if correct_id not in choice_ids:
                errors.append(f"correct_choice_missing:{where}:{correct_id!r}")
            else:
                correct_index = choice_ids.index(correct_id)
                correct_choice_positions_by_count[len(choice_ids)][correct_index] += 1
                if correct_text != choice_texts[correct_index]:
                    errors.append(f"correct_choice_text_mismatch:{where}:{correct_id}:{correct_text!r}")
            for cid, text, rationale in zip(choice_ids, choice_texts, choice_rationales):
                relation_body = rationale if cid == correct_id else rationale_instructional_body(text, rationale)
                validate_numeric_equalities(relation_body, where + f".choice[{cid}].rationale_vi", errors)
                validate_numeric_relations(relation_body, where + f".choice[{cid}].rationale_vi", errors)
                if cid == correct_id:
                    continue
                normalized_rationale = " ".join(rationale.split())
                structured_reason = structured_choice_reason(skill, prompt, text)
                if structured_reason and structured_reason.casefold() not in normalized_rationale.casefold():
                    errors.append(f"structured_distractor_missing_reason:{where}:{cid}:{text}")
                component_marker = COMPONENT_TERM_REASON_MARKERS.get(text.strip().casefold()) if skill in COMPONENT_SKILLS else None
                if component_marker and component_marker not in normalized_rationale.casefold():
                    errors.append(f"component_distractor_missing_term_reason:{where}:{cid}:{text}")
                explicit_contrast = has_explicit_choice_contrast(text, str(correct_text), normalized_rationale)
                if not structured_reason and not component_marker and not explicit_contrast:
                    errors.append(f"missing_choice_specific_diagnosis:{where}:{cid}:{text}")
                distractor_rationale_counts[normalized_rationale] += 1
                if len(normalized_rationale) > MAX_DISTRACTOR_RATIONALE_CHARS:
                    errors.append(f"distractor_rationale_too_long:{where}:{cid}:{len(normalized_rationale)}")
                if normalized_rationale == GENERIC_DISTRACTOR_RATIONALE:
                    errors.append(f"generic_distractor_rationale:{where}:{cid}")
                if any(marker in normalized_rationale.casefold() for marker in SHALLOW_DISTRACTOR_RATIONALE_MARKERS):
                    errors.append(f"shallow_distractor_rationale:{where}:{cid}")
                normalized_explanation = " ".join(explanation.split())
                if normalized_explanation and normalized_explanation not in normalized_rationale:
                    errors.append(f"distractor_rationale_missing_question_reason:{where}:{cid}")
            if correct_text not in accepted:
                errors.append(f"correct_choice_text_not_accepted:{where}:{correct_text}")
            normalized_correct_text = normalize_choice_text(correct_text) if isinstance(correct_text, str) else ""
            for accepted_answer in accepted:
                if normalize_choice_text(accepted_answer) != normalized_correct_text:
                    errors.append(f"accepted_text_not_correct_choice:{where}:{accepted_answer!r}")
            if validation.get("single_correct") is not True:
                errors.append(f"mc_single_correct_missing:{where}")
            if validation.get("choice_count") != len(choices):
                errors.append(f"mc_choice_count_metadata_mismatch:{where}")
            if question_type == "true_false":
                if len(choices) != 2 or set(choice_texts) != {"Đúng", "Sai"}:
                    errors.append(f"true_false_choices_invalid:{where}:{choice_texts!r}")

    for hint_text, count in first_hint_counts.items():
        if count > 3:
            errors.append(f"first_hint_overused:{count}:{hint_text[:80]}")

    for hint_text, count in second_hint_counts.items():
        if count > 3:
            errors.append(f"over_reused_second_hint:{count}:{hint_text[:80]}")

    for rationale_text, count in distractor_rationale_counts.items():
        if count > 3:
            errors.append(f"over_reused_distractor_rationale:{count}:{rationale_text[:80]}")

    # Sibling 2/5 table lessons must teach distinct strategies, not the same hint template with only digits swapped.
    for left_skill, right_skill in (("TIMES_TABLE_2", "TIMES_TABLE_5"), ("DIVIDE_TABLE_2", "DIVIDE_TABLE_5")):
        left_items = sorted(
            (q for q in questions if isinstance(q, dict) and q.get("skill_id") == left_skill and isinstance(q.get("id"), str)),
            key=lambda q: q["id"],
        )
        right_items = sorted(
            (q for q in questions if isinstance(q, dict) and q.get("skill_id") == right_skill and isinstance(q.get("id"), str)),
            key=lambda q: q["id"],
        )
        for left_item, right_item in zip(left_items, right_items):
            left_hints = left_item.get("hints_vi")
            right_hints = right_item.get("hints_vi")
            if not isinstance(left_hints, list) or not isinstance(right_hints, list):
                continue
            for level in range(min(2, len(left_hints), len(right_hints))):
                left_hint = left_hints[level]
                right_hint = right_hints[level]
                if not isinstance(left_hint, str) or not isinstance(right_hint, str):
                    continue
                if normalize_prompt(left_hint) == normalize_prompt(right_hint):
                    errors.append(
                        f"table_family_hint_strategy_duplicate:{left_item['id']}:{right_item['id']}:level{level + 1}")

    for choice_count, positions in correct_choice_positions_by_count.items():
        counts = [positions[index] for index in range(choice_count)]
        total = sum(counts)
        if total >= choice_count and any(value == 0 for value in counts):
            errors.append(f"correct_choice_position_missing:{choice_count}:{counts}")
        if counts and max(counts) - min(counts) > 1:
            errors.append(f"correct_choice_position_imbalanced:{choice_count}:{counts}")

    for skill in baseline_skill_set:
        if question_counts_by_skill[skill] < 3:
            errors.append(f"insufficient_questions_for_skill:{skill}:{question_counts_by_skill[skill]}")
    for diff in DIFFICULTIES:
        if question_counts_by_difficulty[diff] == 0:
            errors.append(f"missing_difficulty_questions:{diff}")

    # Resolve practice references and enforce source/difficulty consistency.
    practice_ref_counts = Counter(referenced_question_ids)
    for lid, lesson in lesson_by_id.items():
        practice = lesson.get("practice_sets", {})
        if not isinstance(practice, dict):
            continue
        for diff, refs in practice.items():
            if not isinstance(refs, list):
                continue
            for qid in refs:
                if qid not in question_by_id:
                    errors.append(f"missing_question_ref:{lid}:{diff}:{qid}")
                    continue
                q = question_by_id[qid]
                if q.get("lesson_id") != lid:
                    errors.append(f"cross_lesson_question_ref:{lid}:{qid}:{q.get('lesson_id')}")
                if q.get("difficulty") != diff:
                    errors.append(f"practice_difficulty_mismatch:{lid}:{qid}:{diff}:{q.get('difficulty')}")

        application_refs = practice.get("application", [])
        lower_refs = []
        for lower_diff in ("basic", "medium"):
            refs = practice.get(lower_diff, [])
            if isinstance(refs, list):
                lower_refs.extend(qid for qid in refs if isinstance(qid, str))
        if isinstance(application_refs, list):
            application_shapes = []
            for application_qid in application_refs:
                application_q = question_by_id.get(application_qid)
                if isinstance(application_q, dict) and isinstance(application_q.get("prompt_vi"), str):
                    application_shapes.append((application_qid, normalize_prompt(application_q["prompt_vi"])))
            for a in range(len(application_shapes)):
                for b in range(a + 1, len(application_shapes)):
                    qid_a, shape_a = application_shapes[a]
                    qid_b, shape_b = application_shapes[b]
                    if shape_a and shape_a == shape_b:
                        errors.append(f"application_prompt_number_swap_duplicate:{lid}:{qid_a}:{qid_b}")

            for application_qid in application_refs:
                application_q = question_by_id.get(application_qid)
                if not isinstance(application_q, dict) or not isinstance(application_q.get("prompt_vi"), str):
                    continue
                for lower_qid in lower_refs:
                    lower_q = question_by_id.get(lower_qid)
                    if not isinstance(lower_q, dict) or not isinstance(lower_q.get("prompt_vi"), str):
                        continue
                    similarity = application_prompt_shape_similarity(
                        application_q["prompt_vi"], lower_q["prompt_vi"])
                    if similarity >= MAX_APPLICATION_LOWER_SHAPE_SIMILARITY:
                        errors.append(
                            f"application_prompt_too_similar_to_lower_difficulty:{lid}:"
                            f"{application_qid}:{lower_qid}:{similarity:.3f}")

        practice_qids = []
        for refs in practice.values():
            if isinstance(refs, list):
                practice_qids.extend(qid for qid in refs if isinstance(qid, str))
        examples = lesson.get("worked_examples", [])
        if isinstance(examples, list):
            for example in examples:
                if not isinstance(example, dict):
                    continue
                example_id = example.get("id", "unknown_example")
                example_prompt = example.get("prompt_vi")
                if not isinstance(example_prompt, str):
                    continue
                for qid in practice_qids:
                    q = question_by_id.get(qid)
                    if not isinstance(q, dict) or not isinstance(q.get("prompt_vi"), str):
                        continue
                    overlap = worked_example_prompt_overlap(example_prompt, q["prompt_vi"])
                    if overlap:
                        errors.append(f"worked_example_practice_prompt_overlap:{overlap}:{lid}:{example_id}:{qid}")
    for qid in question_ids:
        count = practice_ref_counts[qid]
        if count == 0:
            errors.append(f"orphan_question_unreferenced:{qid}")
        elif count > 1:
            errors.append(f"question_referenced_multiple_times:{qid}:{count}")

    # Exact duplicate explanations make distinct practice questions feel machine-generated.
    for explanation_key, ids in question_explanation_ids.items():
        if len(ids) > 1:
            errors.append(f"duplicate_question_explanation:{'|'.join(ids)}:{explanation_key[:100]}")

    # Exact and very-near duplicate prompts inside one lesson.
    for lid, entries in prompts_by_lesson.items():
        exact = Counter(p.strip().lower() for _, p in entries)
        for prompt, count in exact.items():
            if count > 1:
                errors.append(f"duplicate_prompt:{lid}:{count}:{prompt[:80]}")
        normalized = [(qid, normalize_prompt(prompt)) for qid, prompt in entries]
        for a in range(len(normalized)):
            for b in range(a + 1, len(normalized)):
                qa, pa = normalized[a]
                qb, pb = normalized[b]
                if pa and pb and pa != pb and difflib.SequenceMatcher(None, pa, pb).ratio() >= 0.985:
                    errors.append(f"near_duplicate_prompt:{lid}:{qa}:{qb}")

    # Cross-lesson prompts should not be effectively the same question with only a tiny wording/value change.
    normalized_global = [(lid, qid, normalize_prompt_identity(prompt)) for lid, qid, prompt in all_question_prompts]
    for a in range(len(normalized_global)):
        lid_a, qid_a, prompt_a = normalized_global[a]
        for b in range(a + 1, len(normalized_global)):
            lid_b, qid_b, prompt_b = normalized_global[b]
            if lid_a == lid_b or not prompt_a or not prompt_b:
                continue
            if prompts_are_near_duplicate(prompt_a, prompt_b, 0.95):
                errors.append(f"cross_lesson_near_duplicate_prompt:{qid_a}:{qid_b}")

    for skill, count in question_counts_by_skill.items():
        actual_ordinals = sorted(question_ordinals_by_skill.get(skill, []))
        expected_ordinals = list(range(1, count + 1))
        if actual_ordinals != expected_ordinals:
            errors.append(f"non_contiguous_question_ordinals:{skill}:{actual_ordinals}:{expected_ordinals}")

    # Global ID uniqueness, including nested content entities.
    id_counts = Counter(x for x, _ in all_ids if x)
    for value, count in id_counts.items():
        if count > 1:
            locations = [where for x, where in all_ids if x == value]
            errors.append(f"duplicate_id:{value}:{count}:{'|'.join(locations)}")

    # Bank contract must match the engine contract while Grade-2 content uses only in-scope kinds.
    declared_engine_kinds = bank.get("supported_answer_kinds")
    if (not isinstance(declared_engine_kinds, list) or set(declared_engine_kinds) != ENGINE_ANSWER_KINDS or
            len(declared_engine_kinds) != len(ENGINE_ANSWER_KINDS)):
        errors.append(f"bank_supported_answer_kinds_mismatch:{declared_engine_kinds!r}")
    actual_answer_kinds = {q.get("answer_kind") for q in questions if isinstance(q, dict)}
    declared_used_kinds = bank.get("grade2_used_answer_kinds")
    if (not isinstance(declared_used_kinds, list) or set(declared_used_kinds) != actual_answer_kinds or
            len(declared_used_kinds) != len(actual_answer_kinds)):
        errors.append(f"bank_used_answer_kinds_mismatch:{declared_used_kinds!r}:{sorted(actual_answer_kinds)}")
    actual_question_types = {q.get("question_type") for q in questions if isinstance(q, dict)}
    declared_question_types = bank.get("question_types")
    if (not isinstance(declared_question_types, list) or set(declared_question_types) != actual_question_types or
            len(declared_question_types) != len(actual_question_types)):
        errors.append(f"bank_question_types_mismatch:{declared_question_types!r}:{sorted(actual_question_types)}")
    required_grade2_types = {"numeric_input", "multiple_choice", "true_false", "expression_input", "unit_input", "interactive_measurement", "word_problem"}
    missing_grade2_types = sorted(required_grade2_types - actual_question_types)
    if missing_grade2_types:
        errors.append("missing_required_grade2_question_types:" + ",".join(missing_grade2_types))

    metrics = {
        "baseline_skills": len(baseline_skill_set),
        "chapters": len(chapters),
        "topics": len(topics),
        "lessons": len(lessons),
        "questions": len(questions),
        "valid_questions": len(questions) if not errors else max(0, len(questions) - sum(1 for e in errors if e.startswith("question["))),
        "difficulty_counts": dict(sorted(question_counts_by_difficulty.items())),
        "answer_kind_counts": dict(sorted(Counter(q.get("answer_kind") for q in questions if isinstance(q, dict)).items())),
        "question_type_counts": dict(sorted(Counter(q.get("question_type") for q in questions if isinstance(q, dict)).items())),
    }
    return errors, metrics


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    parser.add_argument("--lessons", type=Path, default=DEFAULT_LESSONS)
    parser.add_argument("--questions", type=Path, default=DEFAULT_QUESTIONS)
    parser.add_argument("--json", action="store_true", dest="as_json")
    args = parser.parse_args()

    errors, metrics = validate(args.baseline, args.lessons, args.questions)
    result = {"ok": not errors, "errors": errors, "metrics": metrics}
    if args.as_json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        if errors:
            print(f"MATH_CONTENT_INVALID errors={len(errors)}")
            for error in errors:
                print("ERROR " + error)
        else:
            print("MATH_CONTENT_VALID " + " ".join(f"{k}={v}" for k, v in metrics.items() if not isinstance(v, dict)))
            print("difficulty=" + json.dumps(metrics.get("difficulty_counts", {}), ensure_ascii=False, sort_keys=True))
            print("answer_kind=" + json.dumps(metrics.get("answer_kind_counts", {}), ensure_ascii=False, sort_keys=True))
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
