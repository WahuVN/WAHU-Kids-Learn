#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Deterministically author the Grade-2 Math lesson catalog and static question bank.

This script is an authoring aid. Child runtime must consume only generated JSON after the
semantic validator passes. No network access or random generation is used.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASELINE_PATH = ROOT / "curriculum" / "math_grade2" / "moet_baseline_v1.json"
PACK_ROOT = ROOT / "content_packs" / "math_grade2_v1"
LESSON_PATH = PACK_ROOT / "lesson_catalog_v1.json"
QUESTION_PATH = PACK_ROOT / "question_bank_v1.json"


def slug(skill: str) -> str:
    return skill.lower()


def nq(prompt: str, answer: int, explanation: str, *, unit: str | None = None,
       numeric_min: int = 0, numeric_max: int = 1000,
       question_type: str = "numeric_input") -> dict:
    q = {
        "question_type": question_type,
        "answer_kind": "integer",
        "prompt_vi": prompt,
        "correct_answer": answer,
        "accepted_answers": [str(answer)],
        "explanation_vi": explanation,
        "validation": {
            "integer_required": True,
            "numeric_min": numeric_min,
            "numeric_max": numeric_max,
        },
    }
    if unit:
        q["answer_unit"] = unit
    return q


def mc(prompt: str, correct: str, distractors: list[str], explanation: str) -> dict:
    texts = [correct] + list(distractors)
    if len(set(texts)) != len(texts):
        raise ValueError(f"Duplicate choice text: {prompt}")
    choices = []
    for i, text in enumerate(texts):
        choices.append({
            "id": chr(ord("a") + i),
            "text": text,
            "rationale_vi": explanation if i == 0 else "Lựa chọn này không phù hợp với quy tắc hoặc dữ kiện của bài.",
        })
    return {
        "question_type": "multiple_choice",
        "answer_kind": "text",
        "prompt_vi": prompt,
        "choices": choices,
        "correct_choice_id": "a",
        "correct_answer": correct,
        "accepted_answers": [correct],
        "explanation_vi": explanation,
        "validation": {"choice_count": len(choices), "single_correct": True},
    }


def tf(prompt: str, correct: bool, explanation: str) -> dict:
    """Grade-2 true/false is a two-choice text question; engine has no separate bool kind."""
    correct_text = "Đúng" if correct else "Sai"
    wrong_text = "Sai" if correct else "Đúng"
    q = mc(prompt, correct_text, [wrong_text], explanation)
    q["question_type"] = "true_false"
    return q


def eq(prompt: str, expression: str, answer: int, explanation: str,
       *, numeric_min: int = 0, numeric_max: int = 1000) -> dict:
    """Expression input aligned with MathAnswerValidator's restricted arithmetic contract."""
    return {
        "question_type": "expression_input",
        "answer_kind": "expression",
        "prompt_vi": prompt,
        "correct_answer": expression,
        "accepted_answers": [str(answer)],
        "explanation_vi": explanation,
        "validation": {
            "expression_syntax": "restricted_numeric_arithmetic",
            "allowed_operators": ["+", "-", "*", "/", "(", ")"],
            "expected_numeric": answer,
            "numeric_min": numeric_min,
            "numeric_max": numeric_max,
        },
    }


def uq(prompt: str, answer: int, unit: str, explanation: str,
       *, aliases: list[str] | None = None, numeric_min: int = 0,
       numeric_max: int = 1000) -> dict:
    aliases = list(aliases or [])
    return {
        "question_type": "unit_input",
        "answer_kind": "unit",
        "prompt_vi": prompt,
        "correct_answer": f"{answer} {unit}",
        "accepted_answers": [f"{answer} {unit}"],
        "expected_unit": unit,
        "accepted_units": [unit] + [x for x in aliases if x != unit],
        "explanation_vi": explanation,
        "validation": {
            "numeric_min": numeric_min,
            "numeric_max": numeric_max,
            "expected_numeric": answer,
        },
    }


def iq(prompt: str, answer: int, explanation: str, *, numeric_min: int = 0,
       numeric_max: int = 1000) -> dict:
    """Integer answer produced by a direct manipulation / measurement control."""
    q = nq(prompt, answer, explanation, numeric_min=numeric_min, numeric_max=numeric_max,
           question_type="interactive_measurement")
    q["answer_kind"] = "interaction_integer"
    return q


CHAPTERS = [
    {"id": "m2_ch01_numbers", "title_vi": "Số tự nhiên đến 1000", "domain_key": "numbers"},
    {"id": "m2_ch02_add_sub", "title_vi": "Cộng và trừ", "domain_key": "addition_subtraction"},
    {"id": "m2_ch03_mul_div", "title_vi": "Nhân và chia", "domain_key": "multiplication_division"},
    {"id": "m2_ch04_word_problems", "title_vi": "Bài toán thực tế một bước", "domain_key": "word_problems"},
    {"id": "m2_ch05_geometry", "title_vi": "Hình học trực quan", "domain_key": "geometry"},
    {"id": "m2_ch06_measurement", "title_vi": "Đo lường", "domain_key": "measurement"},
    {"id": "m2_ch07_data_chance", "title_vi": "Dữ liệu và khả năng xảy ra", "domain_key": "statistics_probability"},
]

TOPICS = [
    {"id": "m2_tp01_count_place", "chapter_id": "m2_ch01_numbers", "title_vi": "Đếm, đọc, viết và cấu tạo số"},
    {"id": "m2_tp02_compare_sort", "chapter_id": "m2_ch01_numbers", "title_vi": "So sánh và sắp xếp số"},
    {"id": "m2_tp03_estimate_tens", "chapter_id": "m2_ch01_numbers", "title_vi": "Ước lượng theo chục"},
    {"id": "m2_tp04_add_sub_meaning", "chapter_id": "m2_ch02_add_sub", "title_vi": "Thành phần phép cộng và phép trừ"},
    {"id": "m2_tp05_written_add_sub", "chapter_id": "m2_ch02_add_sub", "title_vi": "Cộng trừ trong phạm vi 1000"},
    {"id": "m2_tp06_mental_expression", "chapter_id": "m2_ch02_add_sub", "title_vi": "Tính nhẩm và biểu thức"},
    {"id": "m2_tp07_mul_div_meaning", "chapter_id": "m2_ch03_mul_div", "title_vi": "Ý nghĩa và thành phần nhân chia"},
    {"id": "m2_tp08_tables_2_5", "chapter_id": "m2_ch03_mul_div", "title_vi": "Bảng nhân chia 2 và 5"},
    {"id": "m2_tp09_problem_meaning", "chapter_id": "m2_ch04_word_problems", "title_vi": "Chọn phép tính từ tình huống"},
    {"id": "m2_tp10_problem_contexts", "chapter_id": "m2_ch04_word_problems", "title_vi": "Giải bài toán một bước"},
    {"id": "m2_tp11_lines_shapes", "chapter_id": "m2_ch05_geometry", "title_vi": "Điểm, đường và hình"},
    {"id": "m2_tp12_solids_construct", "chapter_id": "m2_ch05_geometry", "title_vi": "Khối và tạo hình"},
    {"id": "m2_tp13_mass_length_capacity", "chapter_id": "m2_ch06_measurement", "title_vi": "Khối lượng, dung tích và độ dài"},
    {"id": "m2_tp14_time_money", "chapter_id": "m2_ch06_measurement", "title_vi": "Thời gian, lịch và tiền Việt Nam"},
    {"id": "m2_tp15_measure_practice", "chapter_id": "m2_ch06_measurement", "title_vi": "Đo, đổi đơn vị và giải quyết vấn đề"},
    {"id": "m2_tp16_data", "chapter_id": "m2_ch07_data_chance", "title_vi": "Thu thập và đọc dữ liệu"},
    {"id": "m2_tp17_chance", "chapter_id": "m2_ch07_data_chance", "title_vi": "Có thể, chắc chắn, không thể"},
]

# skill -> (topic_id, title, explanation, concept_name, concept_definition, prerequisites)
LESSON_INFO = {
    "NUM_COUNT_READ_WRITE_0_1000": ("m2_tp01_count_place", "Đếm, đọc và viết số đến 1000", "Mỗi số đến 1000 được đọc và viết theo giá trị của hàng trăm, hàng chục và hàng đơn vị. Khi một hàng có 0, vẫn phải giữ đúng vị trí của chữ số đó.", "Đọc và viết số", "Ghép số trăm, số chục và số đơn vị theo đúng vị trí để tạo hoặc đọc một số.", []),
    "NUM_FULL_HUNDREDS_RECOGNIZE": ("m2_tp01_count_place", "Nhận biết số tròn trăm", "Số tròn trăm có hàng chục và hàng đơn vị đều bằng 0, ví dụ 300, 700, 1000. Có thể xem đó là một số lượng gồm các nhóm 100 đầy đủ.", "Số tròn trăm", "Số có dạng n00 trong phạm vi 1000, với hàng chục và đơn vị bằng 0.", ["NUM_COUNT_READ_WRITE_0_1000"]),
    "NUM_PREDECESSOR_SUCCESSOR": ("m2_tp01_count_place", "Số liền trước và số liền sau", "Số liền trước của một số nhỏ hơn số đó đúng 1; số liền sau lớn hơn đúng 1. Quy tắc này dùng được cả khi chuyển qua chục hoặc trăm.", "Liền trước và liền sau", "Với số n, số liền trước là n - 1 và số liền sau là n + 1 khi còn trong phạm vi học.", ["NUM_COUNT_READ_WRITE_0_1000"]),
    "PLACE_VALUE_HUNDREDS_TENS_ONES": ("m2_tp01_count_place", "Giá trị hàng trăm, chục, đơn vị", "Vị trí của chữ số quyết định giá trị của nó: hàng trăm gấp 100 lần đơn vị, hàng chục gấp 10 lần đơn vị, hàng đơn vị giữ nguyên.", "Giá trị theo hàng", "Trong số có ba chữ số, chữ số thứ nhất là hàng trăm, thứ hai là hàng chục, thứ ba là hàng đơn vị.", ["NUM_COUNT_READ_WRITE_0_1000"]),
    "NUM_EXPANDED_FORM_HTO": ("m2_tp01_count_place", "Viết số thành tổng trăm, chục, đơn vị", "Một số có thể tách thành tổng giá trị của từng hàng. Ví dụ 352 = 300 + 50 + 2; nếu một hàng bằng 0 thì giá trị của hàng đó bằng 0.", "Dạng khai triển", "Dạng khai triển biểu diễn một số bằng tổng giá trị hàng trăm, hàng chục và hàng đơn vị.", ["PLACE_VALUE_HUNDREDS_TENS_ONES"]),
    "NUMBER_RAY_FILL": ("m2_tp01_count_place", "Điền số trên tia số", "Các vạch trên tia số được sắp theo thứ tự tăng dần với một bước đều. Muốn điền số thiếu, xác định khoảng cách giữa hai vạch kề nhau rồi cộng hoặc trừ đúng bước đó.", "Bước trên tia số", "Khoảng cách số giữa hai vạch liên tiếp là không đổi trong cùng một bài tia số.", ["NUM_PREDECESSOR_SUCCESSOR"]),
    "NUM_COMPARE_0_1000": ("m2_tp02_compare_sort", "So sánh hai số đến 1000", "So sánh từ hàng lớn nhất: hàng trăm, rồi hàng chục, rồi hàng đơn vị. Hàng đầu tiên khác nhau quyết định số nào lớn hơn.", "So sánh số", "Dùng các dấu >, <, = sau khi so sánh lần lượt từ hàng cao nhất xuống.", ["PLACE_VALUE_HUNDREDS_TENS_ONES"]),
    "NUM_MIN_MAX_UP_TO_4": ("m2_tp02_compare_sort", "Tìm số lớn nhất và bé nhất", "Để tìm số lớn nhất hoặc bé nhất trong nhóm không quá bốn số, so sánh các số theo giá trị hàng rồi giữ lại số lớn hơn hoặc bé hơn theo yêu cầu.", "Lớn nhất và bé nhất", "Số lớn nhất không nhỏ hơn số nào trong nhóm; số bé nhất không lớn hơn số nào trong nhóm.", ["NUM_COMPARE_0_1000"]),
    "NUM_SORT_UP_TO_4": ("m2_tp02_compare_sort", "Sắp xếp đến bốn số", "Sắp xếp tăng dần là từ bé đến lớn; giảm dần là từ lớn đến bé. Có thể so sánh từng cặp hoặc lần lượt chọn số nhỏ nhất/lớn nhất còn lại.", "Thứ tự tăng giảm", "Tăng dần: mỗi số sau không nhỏ hơn số trước; giảm dần: mỗi số sau không lớn hơn số trước.", ["NUM_COMPARE_0_1000"]),
    "ESTIMATE_OBJECTS_BY_TENS": ("m2_tp03_estimate_tens", "Ước lượng số đồ vật theo chục", "Khi không cần đếm chính xác từng vật, có thể gom hoặc hình dung theo nhóm 10 để nói số lượng gần đúng theo chục.", "Ước lượng theo chục", "Dùng các nhóm 10 làm mốc để chọn số chục gần và hợp lý với số lượng quan sát.", ["NUM_COUNT_READ_WRITE_0_1000"]),

    "ADD_COMPONENTS_RECOGNIZE": ("m2_tp04_add_sub_meaning", "Nhận biết thành phần phép cộng", "Trong phép cộng a + b = c, a và b là các số hạng, c là tổng. Đổi chỗ hai số hạng không làm thay đổi tổng.", "Số hạng và tổng", "Hai số được cộng là số hạng; kết quả của phép cộng là tổng.", ["NUM_COUNT_READ_WRITE_0_1000"]),
    "SUB_COMPONENTS_RECOGNIZE": ("m2_tp04_add_sub_meaning", "Nhận biết thành phần phép trừ", "Trong phép trừ a - b = c, a là số bị trừ, b là số trừ, c là hiệu. Vai trò các thành phần không hoán đổi như phép cộng.", "Số bị trừ, số trừ, hiệu", "Phép trừ gồm số bị trừ, số trừ và kết quả gọi là hiệu.", ["NUM_COUNT_READ_WRITE_0_1000"]),
    "ADD_WITHIN_1000_NO_CARRY": ("m2_tp05_written_add_sub", "Cộng đến 1000 không nhớ", "Đặt các hàng thẳng cột rồi cộng từ đơn vị sang chục, trăm. Với dạng không nhớ, tổng ở từng cột đều nhỏ hơn 10.", "Cộng không nhớ", "Cộng từng hàng mà không tạo thêm một chục hoặc một trăm để chuyển sang cột kế tiếp.", ["PLACE_VALUE_HUNDREDS_TENS_ONES", "ADD_COMPONENTS_RECOGNIZE"]),
    "ADD_WITHIN_1000_ONE_CARRY_MAX": ("m2_tp05_written_add_sub", "Cộng đến 1000 có nhớ một lượt", "Nếu tổng ở một cột từ 10 trở lên, viết phần đơn vị ở cột đó và nhớ 1 sang cột bên trái. Bài lớp 2 baseline giới hạn không quá một lượt nhớ.", "Cộng có nhớ", "Một lượt nhớ xảy ra khi tổng của một hàng tạo thêm 1 đơn vị ở hàng kế tiếp.", ["ADD_WITHIN_1000_NO_CARRY"]),
    "SUB_WITHIN_1000_NO_BORROW": ("m2_tp05_written_add_sub", "Trừ đến 1000 không mượn", "Đặt các hàng thẳng cột rồi trừ từ đơn vị sang chục, trăm. Với dạng không mượn, chữ số trên ở mỗi cột không nhỏ hơn chữ số dưới.", "Trừ không mượn", "Trừ từng hàng trực tiếp khi mỗi chữ số của số bị trừ đủ lớn ở cột tương ứng.", ["PLACE_VALUE_HUNDREDS_TENS_ONES", "SUB_COMPONENTS_RECOGNIZE"]),
    "SUB_WITHIN_1000_ONE_BORROW_MAX": ("m2_tp05_written_add_sub", "Trừ đến 1000 có mượn một lượt", "Khi chữ số trên nhỏ hơn chữ số dưới ở một cột, mượn 1 từ hàng bên trái, tương đương thêm 10 vào hàng đang trừ. Baseline giới hạn không quá một lượt mượn.", "Trừ có mượn", "Một lượt mượn đổi 1 đơn vị của hàng lớn hơn thành 10 đơn vị ở hàng nhỏ hơn.", ["SUB_WITHIN_1000_NO_BORROW"]),
    "ADD_SUB_TWO_OPERATORS_LEFT_TO_RIGHT": ("m2_tp06_mental_expression", "Biểu thức có hai dấu cộng trừ", "Khi biểu thức chỉ có cộng và trừ, thực hiện từ trái sang phải. Tính kết quả bước thứ nhất rồi dùng kết quả đó cho bước thứ hai.", "Tính từ trái sang phải", "Biểu thức chỉ gồm + và - được tính lần lượt từ trái qua phải.", ["ADD_WITHIN_1000_NO_CARRY", "SUB_WITHIN_1000_NO_BORROW"]),
    "MENTAL_ADD_SUB_WITHIN_20": ("m2_tp06_mental_expression", "Cộng trừ nhẩm trong 20", "Dùng tách số, làm tròn 10 hoặc các cặp số quen thuộc để tính nhanh trong phạm vi 20 mà không cần đặt tính.", "Tính nhẩm đến 20", "Biến đổi số thành các phần dễ cộng hoặc trừ, đặc biệt dựa vào mốc 10.", ["ADD_COMPONENTS_RECOGNIZE", "SUB_COMPONENTS_RECOGNIZE"]),
    "MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000": ("m2_tp06_mental_expression", "Cộng trừ nhẩm số tròn chục, tròn trăm", "Với số tròn chục hoặc tròn trăm, có thể tính số chục/số trăm trước rồi thêm lại chữ số 0 tương ứng.", "Nhẩm số tròn", "Xem 30 là 3 chục và 400 là 4 trăm để cộng trừ theo đơn vị chục hoặc trăm.", ["NUM_FULL_HUNDREDS_RECOGNIZE", "MENTAL_ADD_SUB_WITHIN_20"]),

    "MULTIPLICATION_MEANING": ("m2_tp07_mul_div_meaning", "Ý nghĩa phép nhân", "Phép nhân biểu diễn việc cộng nhiều nhóm bằng nhau. Ví dụ 3 nhóm, mỗi nhóm 2 vật có thể viết 2 + 2 + 2 = 6 và 3 × 2 = 6.", "Nhóm bằng nhau", "Phép nhân là cách viết gọn của phép cộng lặp lại các nhóm có cùng số lượng.", ["MENTAL_ADD_SUB_WITHIN_20"]),
    "DIVISION_MEANING": ("m2_tp07_mul_div_meaning", "Ý nghĩa phép chia", "Phép chia dùng khi chia đều một số lượng thành các nhóm bằng nhau hoặc khi muốn biết có bao nhiêu nhóm bằng nhau.", "Chia đều", "Chia đều nghĩa là mỗi nhóm nhận cùng số lượng và không bỏ sót đối tượng trong bài chia hết.", ["MULTIPLICATION_MEANING"]),
    "MULTIPLICATION_COMPONENTS": ("m2_tp07_mul_div_meaning", "Thành phần phép nhân", "Trong a × b = c, a và b là các thừa số, c là tích. Tích cho biết tổng số phần tử của các nhóm bằng nhau.", "Thừa số và tích", "Các số được nhân là thừa số; kết quả là tích.", ["MULTIPLICATION_MEANING"]),
    "DIVISION_COMPONENTS": ("m2_tp07_mul_div_meaning", "Thành phần phép chia", "Trong a : b = c, a là số bị chia, b là số chia, c là thương. Với bài chia hết, thương cho biết số phần mỗi nhóm hoặc số nhóm.", "Số bị chia, số chia, thương", "Phép chia gồm số bị chia, số chia và kết quả gọi là thương.", ["DIVISION_MEANING"]),
    "TIMES_TABLE_2": ("m2_tp08_tables_2_5", "Bảng nhân 2", "Mỗi bước của bảng nhân 2 tăng thêm 2. Có thể hiểu 2 × n là n nhóm, mỗi nhóm 2 hoặc cộng số 2 n lần.", "Nhân 2", "Nhân một số với 2 là lấy hai lần số đó hoặc tạo các cặp bằng nhau.", ["MULTIPLICATION_COMPONENTS"]),
    "TIMES_TABLE_5": ("m2_tp08_tables_2_5", "Bảng nhân 5", "Mỗi bước của bảng nhân 5 tăng thêm 5. Các tích của 5 có chữ số tận cùng là 0 hoặc 5 trong phạm vi bảng học.", "Nhân 5", "Nhân một số với 5 là cộng các nhóm 5 bằng nhau.", ["MULTIPLICATION_COMPONENTS"]),
    "DIVIDE_TABLE_2": ("m2_tp08_tables_2_5", "Bảng chia 2", "Bảng chia 2 là phép tính ngược của bảng nhân 2. Nếu 2 × n = a thì a : 2 = n.", "Chia 2", "Dùng quan hệ nhân - chia để tìm thương khi số chia là 2.", ["TIMES_TABLE_2", "DIVISION_COMPONENTS"]),
    "DIVIDE_TABLE_5": ("m2_tp08_tables_2_5", "Bảng chia 5", "Bảng chia 5 là phép tính ngược của bảng nhân 5. Nếu 5 × n = a thì a : 5 = n.", "Chia 5", "Dùng quan hệ nhân - chia để tìm thương khi số chia là 5.", ["TIMES_TABLE_5", "DIVISION_COMPONENTS"]),

    "OPERATION_MEANING_FROM_VISUAL": ("m2_tp09_problem_meaning", "Nhìn tình huống để chọn phép tính", "Không chỉ dựa vào từ khóa. Hãy xác định số lượng đang được gộp, bớt đi, tạo nhóm bằng nhau hay chia đều để chọn cộng, trừ, nhân hoặc chia.", "Quan hệ của phép tính", "Cộng gộp/thêm, trừ bớt/so sánh chênh lệch, nhân tạo nhóm bằng nhau, chia chia đều hoặc đếm số nhóm.", ["ADD_COMPONENTS_RECOGNIZE", "SUB_COMPONENTS_RECOGNIZE", "MULTIPLICATION_MEANING", "DIVISION_MEANING"]),
    "WP_ONE_STEP_ADD_MORE": ("m2_tp10_problem_contexts", "Bài toán thêm vào", "Khi một lượng ban đầu được thêm một lượng mới và hỏi tất cả có bao nhiêu, dùng phép cộng một bước.", "Thêm vào", "Tổng mới = số ban đầu + số được thêm.", ["ADD_WITHIN_1000_NO_CARRY", "OPERATION_MEANING_FROM_VISUAL"]),
    "WP_ONE_STEP_SUB_LESS": ("m2_tp10_problem_contexts", "Bài toán bớt đi", "Khi một lượng ban đầu bị lấy bớt và hỏi còn lại, dùng phép trừ một bước.", "Bớt đi", "Số còn lại = số ban đầu - số bị lấy bớt.", ["SUB_WITHIN_1000_NO_BORROW", "OPERATION_MEANING_FROM_VISUAL"]),
    "WP_ONE_STEP_MORE_THAN": ("m2_tp10_problem_contexts", "Bài toán nhiều hơn một số đơn vị", "Nếu B nhiều hơn A k đơn vị thì B = A + k. Cần xác định đúng đại lượng được hỏi trước khi cộng.", "Nhiều hơn", "Một lượng nhiều hơn lượng mốc k đơn vị bằng lượng mốc cộng k.", ["WP_ONE_STEP_ADD_MORE"]),
    "WP_ONE_STEP_LESS_THAN": ("m2_tp10_problem_contexts", "Bài toán ít hơn một số đơn vị", "Nếu B ít hơn A k đơn vị thì B = A - k. Điều quan trọng là nhận ra lượng nào là mốc lớn hơn.", "Ít hơn", "Một lượng ít hơn lượng mốc k đơn vị bằng lượng mốc trừ k.", ["WP_ONE_STEP_SUB_LESS"]),
    "WP_ONE_STEP_MULTIPLICATION_CONTEXT": ("m2_tp10_problem_contexts", "Bài toán nhóm bằng nhau dùng phép nhân", "Khi có nhiều nhóm bằng nhau và biết số phần tử mỗi nhóm, nhân số nhóm với số phần tử mỗi nhóm.", "Nhân trong tình huống", "Tổng số phần tử = số nhóm × số phần tử mỗi nhóm.", ["TIMES_TABLE_2", "TIMES_TABLE_5", "OPERATION_MEANING_FROM_VISUAL"]),
    "WP_ONE_STEP_DIVISION_CONTEXT": ("m2_tp10_problem_contexts", "Bài toán chia đều dùng phép chia", "Khi chia một tổng thành các phần bằng nhau, dùng phép chia để tìm số phần mỗi nhóm hoặc số nhóm.", "Chia trong tình huống", "Thương trả lời kích thước mỗi nhóm hoặc số nhóm khi chia đều.", ["DIVIDE_TABLE_2", "DIVIDE_TABLE_5", "OPERATION_MEANING_FROM_VISUAL"]),
    "WP_SELECT_OPERATION_ONE_STEP": ("m2_tp09_problem_meaning", "Chọn phép tính cho bài toán một bước", "Đọc dữ kiện, xác định điều cần tìm và mối quan hệ giữa các lượng rồi mới chọn phép tính; không dùng một từ khóa đơn lẻ để đoán.", "Chọn phép tính", "Phép tính phải mô tả đúng quan hệ giữa dữ kiện và đại lượng cần tìm.", ["OPERATION_MEANING_FROM_VISUAL"]),

    "POINT_RECOGNIZE": ("m2_tp11_lines_shapes", "Nhận biết điểm", "Điểm biểu diễn một vị trí xác định và thường được đặt tên bằng chữ cái in hoa như A, B, C. Điểm không có độ dài.", "Điểm", "Một điểm chỉ vị trí; tên điểm giúp phân biệt các vị trí trên hình.", []),
    "LINE_SEGMENT_RECOGNIZE": ("m2_tp11_lines_shapes", "Nhận biết đoạn thẳng", "Đoạn thẳng là phần thẳng nối hai điểm đầu mút. Độ dài đoạn thẳng là khoảng cách giữa hai đầu mút.", "Đoạn thẳng", "Đoạn thẳng có hai đầu mút xác định và phần nối giữa chúng là thẳng.", ["POINT_RECOGNIZE"]),
    "CURVE_RECOGNIZE": ("m2_tp11_lines_shapes", "Nhận biết đường cong", "Đường cong đổi hướng mềm mại và không phải là một đoạn thẳng. Có thể nhận biết bằng hình dạng uốn lượn.", "Đường cong", "Đường cong không giữ một hướng thẳng cố định trên toàn bộ đường.", ["POINT_RECOGNIZE"]),
    "STRAIGHT_LINE_RECOGNIZE": ("m2_tp11_lines_shapes", "Nhận biết đường thẳng", "Đường thẳng đi theo một hướng không uốn cong và có thể kéo dài về hai phía. Khác với đoạn thẳng, đường thẳng không bị giới hạn bởi hai đầu mút.", "Đường thẳng", "Đường thẳng có thể kéo dài mãi theo hai hướng và không cong.", ["LINE_SEGMENT_RECOGNIZE"]),
    "POLYLINE_RECOGNIZE": ("m2_tp11_lines_shapes", "Nhận biết đường gấp khúc", "Đường gấp khúc được tạo bởi nhiều đoạn thẳng nối tiếp nhau tại các điểm. Mỗi chỗ nối có thể làm đường đổi hướng.", "Đường gấp khúc", "Một chuỗi từ hai đoạn thẳng trở lên nối đầu mút với nhau tạo thành đường gấp khúc.", ["LINE_SEGMENT_RECOGNIZE"]),
    "THREE_COLLINEAR_POINTS": ("m2_tp11_lines_shapes", "Ba điểm thẳng hàng", "Ba điểm thẳng hàng khi cả ba cùng nằm trên một đường thẳng. Nếu một điểm lệch khỏi đường qua hai điểm còn lại thì ba điểm không thẳng hàng.", "Thẳng hàng", "Các điểm thẳng hàng cùng nằm trên một đường thẳng duy nhất.", ["POINT_RECOGNIZE", "STRAIGHT_LINE_RECOGNIZE"]),
    "QUADRILATERAL_RECOGNIZE": ("m2_tp11_lines_shapes", "Nhận biết hình tứ giác", "Hình tứ giác là hình phẳng kín có bốn cạnh và bốn đỉnh. Hình vuông và hình chữ nhật đều là các ví dụ của tứ giác.", "Tứ giác", "Một hình kín có đúng bốn cạnh là hình tứ giác.", ["LINE_SEGMENT_RECOGNIZE"]),
    "CYLINDER_RECOGNIZE": ("m2_tp12_solids_construct", "Nhận biết khối trụ", "Khối trụ có hai mặt đáy tròn bằng nhau và một mặt cong bao quanh. Lon nước là một vật thể gần dạng khối trụ.", "Khối trụ", "Khối có hai đáy tròn song song và một mặt cong xung quanh.", []),
    "SPHERE_RECOGNIZE": ("m2_tp12_solids_construct", "Nhận biết khối cầu", "Khối cầu tròn đều theo mọi hướng và không có cạnh hay đỉnh. Quả bóng là vật thể gần dạng khối cầu.", "Khối cầu", "Khối tròn không có cạnh, đỉnh hay mặt phẳng đáy.", []),
    "DRAW_SEGMENT_GIVEN_LENGTH": ("m2_tp12_solids_construct", "Tạo đoạn thẳng có độ dài cho trước", "Đặt một đầu đoạn thẳng tại vạch đầu, rồi chọn đầu còn lại sao cho khoảng cách trên thước đúng bằng độ dài yêu cầu.", "Vẽ đoạn thẳng theo độ dài", "Độ dài đoạn thẳng bằng hiệu vị trí hai đầu mút trên cùng một thước chia đều.", ["LINE_SEGMENT_RECOGNIZE"]),
    "FOLD_CUT_COMPOSE_SHAPES": ("m2_tp12_solids_construct", "Gấp, cắt và ghép hình", "Có thể tạo hình mới bằng cách gấp, cắt hoặc ghép các hình đơn giản. Khi ghép, các cạnh phù hợp được đặt sát nhau mà không làm thay đổi bản chất của từng mảnh.", "Ghép và tạo hình", "Tách một hình thành các phần hoặc ghép các phần để tạo hình mới giúp nhận ra quan hệ giữa các hình.", ["QUADRILATERAL_RECOGNIZE"]),

    "HEAVIER_LIGHTER": ("m2_tp13_mass_length_capacity", "Nặng hơn và nhẹ hơn", "Khi so sánh bằng cân, phía hạ thấp hơn thường chứa vật nặng hơn; phía nâng cao hơn chứa vật nhẹ hơn nếu cân hoạt động cân bằng đúng.", "So sánh khối lượng", "Nặng hơn nghĩa là có khối lượng lớn hơn; nhẹ hơn nghĩa là có khối lượng nhỏ hơn.", []),
    "MASS_KG_READ_WRITE": ("m2_tp13_mass_length_capacity", "Đọc và viết số đo kilôgam", "Kilôgam, kí hiệu kg, là đơn vị dùng để đo khối lượng. Số đo phải đi cùng đúng đơn vị để biết đang nói về khối lượng.", "Kilôgam", "kg là kí hiệu của kilôgam, một đơn vị đo khối lượng thông dụng.", ["HEAVIER_LIGHTER"]),
    "CAPACITY_LITER_READ_WRITE": ("m2_tp13_mass_length_capacity", "Đọc và viết số đo lít", "Lít, kí hiệu l, là đơn vị dùng để đo dung tích chất lỏng. Khi cộng hoặc trừ các số đo cùng đơn vị lít, giữ nguyên đơn vị l.", "Lít", "l là kí hiệu của lít, đơn vị đo dung tích.", []),
    "LENGTH_DM_M_KM_RECOGNIZE_RELATION": ("m2_tp13_mass_length_capacity", "Đề-xi-mét, mét và ki-lô-mét", "Các đơn vị độ dài có quan hệ: 1 m = 10 dm và 1 km = 1000 m. Chọn đơn vị phù hợp với kích thước quãng đường hoặc vật cần đo.", "Quan hệ đơn vị độ dài", "dm, m và km là các đơn vị độ dài với 1 m = 10 dm, 1 km = 1000 m.", []),
    "TIME_DAY_24_HOURS": ("m2_tp14_time_money", "Một ngày có 24 giờ", "Một ngày đầy đủ gồm 24 giờ. Có thể dùng mốc sáng, trưa, chiều, tối để liên hệ các thời điểm trong ngày.", "Ngày và giờ", "1 ngày = 24 giờ.", []),
    "TIME_HOUR_60_MINUTES": ("m2_tp14_time_money", "Một giờ có 60 phút", "Phút là đơn vị nhỏ hơn giờ. Khi đủ 60 phút thì được 1 giờ.", "Giờ và phút", "1 giờ = 60 phút.", ["TIME_DAY_24_HOURS"]),
    "CALENDAR_DAYS_IN_MONTH_DATE": ("m2_tp14_time_money", "Ngày và tháng trên lịch", "Lịch cho biết thứ tự ngày trong tháng. Một số tháng có 30 ngày, một số có 31 ngày; tháng 2 có 28 hoặc 29 ngày tùy năm.", "Đọc lịch", "Dùng số ngày và vị trí ngày trên lịch để xác định ngày trước, ngày sau và số ngày trong tháng.", ["NUM_PREDECESSOR_SUCCESSOR"]),
    "MONEY_VND_NOTE_RECOGNITION": ("m2_tp14_time_money", "Nhận biết tiền Việt Nam", "Trên tờ tiền, giá trị được thể hiện bằng con số và đơn vị đồng. Khi nhận biết, đọc đúng giá trị in trên tờ tiền và phân biệt với các giá trị khác.", "Giá trị tờ tiền", "Giá trị tiền được đọc theo con số in trên tờ và đơn vị đồng; bài học này chỉ rèn nhận biết, không mở rộng chuẩn số học ngoài baseline.", ["NUM_COUNT_READ_WRITE_0_1000"]),
    "MEASURE_WITH_RULER_CM": ("m2_tp15_measure_practice", "Đo độ dài bằng thước xăng-ti-mét", "Đặt vạch 0 của thước trùng một đầu vật; đầu còn lại chỉ đến vạch nào thì số đo là bấy nhiêu xăng-ti-mét. Nếu không bắt đầu từ 0, lấy vị trí cuối trừ vị trí đầu.", "Đo bằng thước cm", "Độ dài bằng hiệu số giữa hai vị trí đầu mút trên thước chia cm.", ["LINE_SEGMENT_RECOGNIZE"]),
    "MEASURE_WITH_COMMON_SCALE": ("m2_tp15_measure_practice", "Đọc dụng cụ đo có vạch chia", "Muốn đọc một dụng cụ có thang chia, trước hết xác định giá trị mỗi vạch rồi đếm số khoảng từ mốc đã biết đến vị trí cần đọc.", "Vạch chia", "Mỗi vạch trên cùng một thang đều cách nhau một giá trị cố định nếu thang chia đều.", ["NUM_COUNT_READ_WRITE_0_1000"]),
    "CLOCK_MINUTE_HAND_AT_3_OR_6": ("m2_tp14_time_money", "Đọc giờ khi kim phút chỉ 3 hoặc 6", "Khi kim phút chỉ số 3 là 15 phút; khi chỉ số 6 là 30 phút. Kết hợp với kim giờ để đọc giờ và phút.", "15 phút và 30 phút trên đồng hồ", "Kim phút ở số 3 tương ứng 15 phút; ở số 6 tương ứng 30 phút.", ["TIME_HOUR_60_MINUTES"]),
    "MEASUREMENT_CONVERT_CALCULATE_LEARNED_UNITS": ("m2_tp15_measure_practice", "Đổi và tính với đơn vị đã học", "Chỉ cộng trừ trực tiếp khi các số đo cùng đơn vị. Khi cần, đổi về cùng đơn vị trước rồi thực hiện phép tính.", "Tính với số đo", "Đưa các số đo về cùng đơn vị trước khi cộng, trừ hoặc so sánh.", ["MASS_KG_READ_WRITE", "CAPACITY_LITER_READ_WRITE", "LENGTH_DM_M_KM_RECOGNIZE_RELATION"]),
    "MEASUREMENT_ESTIMATE_BASIC": ("m2_tp15_measure_practice", "Ước lượng số đo cơ bản", "Ước lượng dùng một vật hoặc độ dài quen thuộc làm mốc rồi so sánh. Kết quả cần hợp lý với kích thước thực tế, không cần đúng tuyệt đối.", "Ước lượng số đo", "So sánh với mốc quen thuộc để chọn số đo và đơn vị gần đúng.", ["MEASURE_WITH_RULER_CM"]),
    "POLYLINE_LENGTH_SUM_SEGMENTS": ("m2_tp15_measure_practice", "Tính độ dài đường gấp khúc", "Độ dài đường gấp khúc bằng tổng độ dài tất cả các đoạn thẳng tạo nên đường đó, với các số đo cùng đơn vị.", "Độ dài đường gấp khúc", "Cộng độ dài từng đoạn thành phần để có tổng độ dài đường gấp khúc.", ["POLYLINE_RECOGNIZE", "MEASURE_WITH_RULER_CM"]),
    "MEASUREMENT_REAL_WORLD_ONE_STEP": ("m2_tp15_measure_practice", "Bài toán đo lường một bước", "Đọc đơn vị, xác định đại lượng được thêm, bớt hoặc so sánh rồi dùng một phép tính phù hợp. Giữ đơn vị trong kết quả.", "Giải toán đo lường", "Một bài đo lường một bước kết hợp số đo cùng đơn vị với cộng hoặc trừ phù hợp tình huống.", ["MEASUREMENT_CONVERT_CALCULATE_LEARNED_UNITS", "WP_SELECT_OPERATION_ONE_STEP"]),

    "DATA_COLLECT_CLASSIFY_COUNT": ("m2_tp16_data", "Thu thập, phân loại và kiểm đếm", "Để có dữ liệu rõ ràng, xác định tiêu chí phân loại, đưa mỗi đối tượng vào đúng nhóm rồi đếm số phần tử từng nhóm.", "Phân loại và kiểm đếm", "Mỗi đối tượng được xếp theo một tiêu chí rõ ràng trước khi đếm số lượng của từng nhóm.", ["NUM_COUNT_READ_WRITE_0_1000"]),
    "PICTOGRAPH_READ_DESCRIBE": ("m2_tp16_data", "Đọc và mô tả biểu đồ tranh", "Biểu đồ tranh dùng hình ảnh để biểu diễn số lượng. Luôn đọc chú giải để biết một hình tương ứng bao nhiêu đối tượng trước khi tính.", "Biểu đồ tranh", "Số lượng = số biểu tượng × giá trị mỗi biểu tượng theo chú giải.", ["DATA_COLLECT_CLASSIFY_COUNT"]),
    "PICTOGRAPH_SIMPLE_INFERENCE": ("m2_tp16_data", "Nhận xét từ biểu đồ tranh", "Sau khi đọc đúng số lượng từng nhóm, có thể so sánh để tìm nhóm nhiều nhất, ít nhất hoặc chênh lệch đơn giản.", "Suy luận từ dữ liệu", "Dùng các số đã đọc từ biểu đồ để so sánh và nêu nhận xét có căn cứ.", ["PICTOGRAPH_READ_DESCRIBE", "NUM_COMPARE_0_1000"]),
    "EVENT_POSSIBLE": ("m2_tp17_chance", "Sự kiện có thể xảy ra", "Một sự kiện là có thể nếu nó có ít nhất một kết quả phù hợp nhưng không bắt buộc xảy ra trong mọi lần thử.", "Có thể", "Có ít nhất một kết quả làm sự kiện xảy ra và cũng có kết quả làm nó không xảy ra.", []),
    "EVENT_CERTAIN": ("m2_tp17_chance", "Sự kiện chắc chắn xảy ra", "Một sự kiện là chắc chắn nếu mọi kết quả có thể của tình huống đều làm sự kiện đó xảy ra.", "Chắc chắn", "Sự kiện xảy ra trong tất cả các kết quả có thể của tình huống.", ["EVENT_POSSIBLE"]),
    "EVENT_IMPOSSIBLE": ("m2_tp17_chance", "Sự kiện không thể xảy ra", "Một sự kiện là không thể nếu không có kết quả nào của tình huống làm sự kiện đó xảy ra.", "Không thể", "Không có kết quả hợp lệ nào làm sự kiện xảy ra.", ["EVENT_POSSIBLE"]),
}

# Explicit, deterministic question content. Three questions per skill: basic, medium, application.
Q = {
    "NUM_COUNT_READ_WRITE_0_1000": [
        nq("Số gồm 5 trăm, 0 chục và 7 đơn vị là số nào?", 507, "5 trăm là 500, 0 chục là 0 và 7 đơn vị là 7; 500 + 0 + 7 = 507."),
        mc("Cách đọc đúng của số 420 là gì?", "bốn trăm hai mươi", ["bốn trăm hai", "bốn mươi hai", "hai trăm bốn mươi"], "420 có 4 trăm, 2 chục và 0 đơn vị nên đọc là bốn trăm hai mươi."),
        nq("Một thẻ số có 7 trăm, 3 chục và 4 đơn vị. Viết số trên thẻ.", 734, "7 trăm + 3 chục + 4 đơn vị = 700 + 30 + 4 = 734."),
    ],
    "NUM_FULL_HUNDREDS_RECOGNIZE": [
        mc("Số nào là số tròn trăm?", "600", ["610", "606", "660"], "600 có hàng chục và hàng đơn vị đều bằng 0."),
        tf("700 là một số tròn trăm.", True, "700 có hàng chục và hàng đơn vị đều bằng 0 nên đây là số tròn trăm."),
        mc("Trong các số 700, 750, 705, 570, số nào gồm đúng 7 trăm đầy đủ?", "700", ["750", "705", "570"], "700 có đúng 7 trăm và không có thêm chục hay đơn vị."),
    ],
    "NUM_PREDECESSOR_SUCCESSOR": [
        nq("Số liền trước của 500 là số nào?", 499, "Số liền trước nhỏ hơn 500 đúng 1 nên là 499."),
        nq("Số liền sau của 999 là số nào?", 1000, "Số liền sau lớn hơn 999 đúng 1 nên là 1000."),
        nq("Điền số còn thiếu: 398, 399, __, 401.", 400, "Dãy tăng mỗi lần 1 đơn vị: sau 399 là 400 rồi đến 401."),
    ],
    "PLACE_VALUE_HUNDREDS_TENS_ONES": [
        nq("Trong số 684, chữ số 6 có giá trị là bao nhiêu?", 600, "Chữ số 6 đứng ở hàng trăm nên có giá trị 600."),
        nq("Trong số 572, chữ số hàng chục là chữ số nào?", 7, "Số 572 có 5 trăm, 7 chục và 2 đơn vị."),
        nq("Số có 4 trăm, 0 chục và 9 đơn vị là số nào?", 409, "Giữ đúng vị trí hàng chục bằng 0: 400 + 0 + 9 = 409."),
    ],
    "NUM_EXPANDED_FORM_HTO": [
        mc("Dạng khai triển đúng của 352 là gì?", "300 + 50 + 2", ["300 + 5 + 2", "30 + 50 + 2", "300 + 50 + 20"], "352 có 3 trăm, 5 chục và 2 đơn vị nên bằng 300 + 50 + 2."),
        nq("Số nào bằng 600 + 20 + 7?", 627, "Cộng giá trị các hàng: 600 + 20 + 7 = 627."),
        mc("Dạng khai triển đúng của 908 là gì?", "900 + 0 + 8", ["90 + 8", "900 + 80", "900 + 8 + 80"], "908 có 9 trăm, 0 chục và 8 đơn vị."),
    ],
    "NUMBER_RAY_FILL": [
        nq("Trên tia số có các vạch 200, 300, __, 500. Số còn thiếu là gì?", 400, "Mỗi vạch tăng 100 nên sau 300 là 400."),
        nq("Trên tia số: 650, 700, __, 800. Mỗi bước bằng nhau. Số còn thiếu là gì?", 750, "Từ 650 đến 700 tăng 50, nên vạch tiếp theo là 750."),
        nq("Tia số bắt đầu 0, 100, 200, 300, 400, 500, __. Điền số tiếp theo.", 600, "Mỗi bước tăng 100; sau 500 là 600."),
    ],
    "NUM_COMPARE_0_1000": [
        mc("Điền dấu đúng: 608 __ 680", "<", [">", "=", "+"], "Cùng 6 trăm nhưng 0 chục nhỏ hơn 8 chục nên 608 < 680."),
        mc("Điền dấu đúng: 945 __ 925", ">", ["<", "=", "-"], "Cùng 9 trăm; 4 chục lớn hơn 2 chục nên 945 > 925."),
        mc("Bạn An có 399 thẻ, bạn Bình có 403 thẻ. So sánh 399 và 403.", "399 < 403", ["399 > 403", "399 = 403", "399 + 403"], "399 còn dưới 400, trong khi 403 lớn hơn 400 nên 399 < 403."),
    ],
    "NUM_MIN_MAX_UP_TO_4": [
        nq("Số bé nhất trong nhóm 407, 470, 704, 740 là số nào?", 407, "Cả bốn số đều khác nhau; so sánh hàng trăm cho thấy 407 và 470 nhỏ hơn các số 7 trăm, rồi 407 < 470."),
        nq("Số lớn nhất trong nhóm 125, 512, 251, 215 là số nào?", 512, "512 có 5 trăm, lớn hơn các số chỉ có 1 hoặc 2 trăm."),
        nq("Bốn thẻ ghi 999, 909, 990, 900. Thẻ lớn nhất ghi số nào?", 999, "Cùng 9 trăm; 999 có 9 chục và 9 đơn vị nên lớn nhất."),
    ],
    "NUM_SORT_UP_TO_4": [
        mc("Sắp xếp tăng dần 320, 302, 230, 203.", "203, 230, 302, 320", ["320, 302, 230, 203", "203, 302, 230, 320", "230, 203, 320, 302"], "Tăng dần là từ bé đến lớn: 203 < 230 < 302 < 320."),
        mc("Sắp xếp giảm dần 450, 405, 540, 504.", "540, 504, 450, 405", ["405, 450, 504, 540", "540, 450, 504, 405", "504, 540, 405, 450"], "Giảm dần là từ lớn đến bé: 540 > 504 > 450 > 405."),
        mc("Bốn số 111, 101, 110, 100 được xếp từ bé đến lớn theo thứ tự nào?", "100, 101, 110, 111", ["111, 110, 101, 100", "100, 110, 101, 111", "101, 100, 111, 110"], "So sánh hàng chục rồi hàng đơn vị để có 100 < 101 < 110 < 111."),
    ],
    "ESTIMATE_OBJECTS_BY_TENS": [
        nq("Có 6 nhóm, mỗi nhóm khoảng 10 que tính. Ước lượng có khoảng bao nhiêu que?", 60, "6 nhóm chục tương ứng khoảng 60 que."),
        nq("Một hộp được nhìn thấy khoảng 8 chục viên bi. Ước lượng số viên bi là bao nhiêu?", 80, "8 chục nghĩa là khoảng 80."),
        mc("Một khay có khoảng 73 hạt. Nếu chỉ ước lượng theo chục gần nhất, chọn số nào hợp lý nhất?", "khoảng 70", ["khoảng 7", "khoảng 700", "khoảng 20"], "73 gần 70 hơn 80 khi ước lượng theo chục gần nhất."),
    ],

    "ADD_COMPONENTS_RECOGNIZE": [
        mc("Trong 23 + 15 = 38, số 23 được gọi là gì?", "số hạng", ["tổng", "hiệu", "số bị trừ"], "23 là một trong hai số được cộng nên là số hạng."),
        mc("Trong 41 + 8 = 49, số 49 được gọi là gì?", "tổng", ["số hạng", "số trừ", "thương"], "Kết quả của phép cộng gọi là tổng."),
        mc("Phép cộng 120 + 230 = 350 có hai số hạng là cặp nào?", "120 và 230", ["230 và 350", "120 và 350", "350 và 350"], "Hai số đứng trước dấu bằng và được nối bằng dấu + là hai số hạng."),
    ],
    "SUB_COMPONENTS_RECOGNIZE": [
        mc("Trong 50 - 12 = 38, số 50 được gọi là gì?", "số bị trừ", ["số trừ", "hiệu", "tổng"], "50 là số đứng trước dấu trừ nên là số bị trừ."),
        mc("Trong 72 - 20 = 52, số 20 được gọi là gì?", "số trừ", ["số bị trừ", "hiệu", "số hạng"], "20 là lượng được bớt khỏi số bị trừ nên là số trừ."),
        mc("Trong 90 - 35 = 55, số 55 là gì?", "hiệu", ["số trừ", "số bị trừ", "tích"], "Kết quả của phép trừ gọi là hiệu."),
    ],
    "ADD_WITHIN_1000_NO_CARRY": [
        nq("Tính 243 + 125.", 368, "Cộng từng hàng: 3+5=8, 4+2=6, 2+1=3; được 368."),
        nq("Tính 410 + 230.", 640, "0+0=0, 1 chục + 3 chục = 4 chục, 4 trăm + 2 trăm = 6 trăm; được 640."),
        nq("Thư viện có 324 sách truyện và 253 sách khoa học. Có tất cả bao nhiêu quyển?", 577, "324 + 253 = 577; từng cột đều không cần nhớ."),
    ],
    "ADD_WITHIN_1000_ONE_CARRY_MAX": [
        nq("Tính 246 + 137.", 383, "6+7=13, viết 3 nhớ 1; 4+3+1=8; 2+1=3, nên kết quả 383."),
        nq("Tính 358 + 124.", 482, "8+4=12, viết 2 nhớ 1; 5+2+1=8; 3+1=4, nên 482."),
        nq("Kho A có 465 hộp, nhập thêm 217 hộp. Có tất cả bao nhiêu hộp?", 682, "465 + 217 = 682; chỉ hàng đơn vị tạo một lượt nhớ."),
    ],
    "SUB_WITHIN_1000_NO_BORROW": [
        nq("Tính 786 - 234.", 552, "6-4=2, 8-3=5, 7-2=5; được 552."),
        nq("Tính 900 - 400.", 500, "9 trăm trừ 4 trăm còn 5 trăm, tức 500."),
        nq("Kho có 654 hộp, chuyển đi 321 hộp. Còn lại bao nhiêu hộp?", 333, "654 - 321 = 333 và không cột nào cần mượn."),
    ],
    "SUB_WITHIN_1000_ONE_BORROW_MAX": [
        nq("Tính 352 - 138.", 214, "Mượn 1 chục: 12-8=4; còn 4 chục, 4-3=1; 3-1=2, được 214."),
        nq("Tính 641 - 223.", 418, "Mượn 1 chục: 11-3=8; còn 3 chục, 3-2=1; 6-2=4, được 418."),
        nq("Một cửa hàng có 730 chai, bán 412 chai. Còn lại bao nhiêu chai?", 318, "730 - 412 = 318; mượn 1 chục ở hàng đơn vị rồi các hàng còn lại trừ trực tiếp."),
    ],
    "ADD_SUB_TWO_OPERATORS_LEFT_TO_RIGHT": [
        nq("Tính từ trái sang phải: 50 + 20 - 10.", 60, "50 + 20 = 70, rồi 70 - 10 = 60."),
        eq("Tính 100 - 30 + 5. Có thể nhập kết quả hoặc một biểu thức số tương đương.", "100 - 30 + 5", 75, "100 - 30 = 70, rồi 70 + 5 = 75."),
        nq("Một hộp có 200 thẻ, thêm 150 thẻ rồi lấy ra 100 thẻ. Còn bao nhiêu thẻ?", 250, "Từ trái sang phải theo tình huống: 200 + 150 = 350, 350 - 100 = 250."),
    ],
    "MENTAL_ADD_SUB_WITHIN_20": [
        nq("Tính nhẩm 8 + 7.", 15, "Tách 7 thành 2 và 5: 8+2=10, rồi 10+5=15."),
        nq("Tính nhẩm 17 - 9.", 8, "Có thể trừ 10 rồi thêm lại 1: 17-10=7, 7+1=8."),
        nq("Mai có 12 nhãn dán, được cho thêm 5 nhãn. Mai có bao nhiêu nhãn?", 17, "12 + 5 = 17, vẫn trong phạm vi 20."),
    ],
    "MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000": [
        nq("Tính nhẩm 30 + 40.", 70, "3 chục + 4 chục = 7 chục, tức 70."),
        nq("Tính nhẩm 500 - 200.", 300, "5 trăm - 2 trăm = 3 trăm, tức 300."),
        nq("Một kho có 300 hộp, nhận thêm 400 hộp. Tính nhẩm tổng số hộp.", 700, "3 trăm + 4 trăm = 7 trăm = 700."),
    ],

    "MULTIPLICATION_MEANING": [
        mc("Có 3 nhóm, mỗi nhóm 2 chấm. Phép cộng lặp lại nào đúng?", "2 + 2 + 2", ["2 + 2 + 2 + 2", "2 + 3", "3 + 2 + 2"], "Ba nhóm, mỗi nhóm 2 tương ứng cộng số 2 ba lần."),
        nq("5 nhóm, mỗi nhóm 2 vật có tất cả bao nhiêu vật?", 10, "2 + 2 + 2 + 2 + 2 = 10, cũng là 5 × 2."),
        nq("Có 4 túi, mỗi túi 5 viên bi. Có tất cả bao nhiêu viên?", 20, "Bốn nhóm bằng nhau, mỗi nhóm 5: 4 × 5 = 20."),
    ],
    "DIVISION_MEANING": [
        nq("Có 10 chiếc bánh chia đều cho 2 bạn. Mỗi bạn được bao nhiêu chiếc?", 5, "10 chia đều thành 2 phần bằng nhau thì mỗi phần có 5."),
        nq("15 bông hoa xếp thành các nhóm 5 bông. Có bao nhiêu nhóm?", 3, "15 : 5 = 3 nhóm."),
        nq("20 thẻ chia đều vào 5 hộp. Mỗi hộp có bao nhiêu thẻ?", 4, "20 : 5 = 4 nên mỗi hộp có 4 thẻ."),
    ],
    "MULTIPLICATION_COMPONENTS": [
        mc("Trong 2 × 5 = 10, số 2 được gọi là gì?", "thừa số", ["tích", "thương", "số bị chia"], "2 là một số được nhân nên là thừa số."),
        mc("Trong 5 × 6 = 30, số 30 là gì?", "tích", ["thừa số", "hiệu", "số chia"], "Kết quả của phép nhân gọi là tích."),
        mc("Trong 2 × 8 = 16, hai thừa số là cặp nào?", "2 và 8", ["8 và 16", "2 và 16", "16 và 16"], "Hai số đứng hai bên dấu nhân là hai thừa số."),
    ],
    "DIVISION_COMPONENTS": [
        mc("Trong 10 : 2 = 5, số 10 là gì?", "số bị chia", ["số chia", "thương", "tích"], "10 là lượng được đem chia nên là số bị chia."),
        mc("Trong 20 : 5 = 4, số 5 là gì?", "số chia", ["số bị chia", "thương", "số hạng"], "5 là số dùng để chia nên là số chia."),
        mc("Trong 18 : 2 = 9, số 9 là gì?", "thương", ["số bị chia", "số chia", "tích"], "Kết quả của phép chia gọi là thương."),
    ],
    "TIMES_TABLE_2": [
        nq("Tính 2 × 6.", 12, "Bảng nhân 2: 2 × 6 = 12."),
        nq("Tính 2 × 9.", 18, "Bảng nhân 2: 2 × 9 = 18."),
        nq("Có 7 đôi tất. Mỗi đôi có 2 chiếc. Có tất cả bao nhiêu chiếc tất?", 14, "7 nhóm 2 chiếc: 7 × 2 = 14."),
    ],
    "TIMES_TABLE_5": [
        nq("Tính 5 × 4.", 20, "Bảng nhân 5: 5 × 4 = 20."),
        nq("Tính 5 × 8.", 40, "Bảng nhân 5: 5 × 8 = 40."),
        nq("Có 6 gói, mỗi gói 5 bút. Có tất cả bao nhiêu bút?", 30, "6 nhóm 5 bút: 6 × 5 = 30."),
    ],
    "DIVIDE_TABLE_2": [
        nq("Tính 16 : 2.", 8, "Vì 2 × 8 = 16 nên 16 : 2 = 8."),
        nq("Tính 20 : 2.", 10, "Vì 2 × 10 = 20 nên 20 : 2 = 10."),
        nq("18 chiếc bánh chia đều cho 2 bạn. Mỗi bạn được bao nhiêu chiếc?", 9, "18 : 2 = 9."),
    ],
    "DIVIDE_TABLE_5": [
        nq("Tính 25 : 5.", 5, "Vì 5 × 5 = 25 nên 25 : 5 = 5."),
        nq("Tính 40 : 5.", 8, "Vì 5 × 8 = 40 nên 40 : 5 = 8."),
        nq("50 viên bi xếp vào các túi, mỗi túi 5 viên. Cần bao nhiêu túi?", 10, "50 : 5 = 10 túi."),
    ],

    "OPERATION_MEANING_FROM_VISUAL": [
        mc("Hai nhóm 4 chấm được gộp lại. Phép tính nào mô tả việc gộp?", "4 + 4", ["4 - 4", "4 : 2", "4 + 2"], "Gộp hai lượng lại là quan hệ cộng; hai nhóm 4 là 4 + 4."),
        mc("Có 10 chấm, gạch bỏ 3 chấm. Phép tính nào mô tả tình huống?", "10 - 3", ["10 + 3", "10 : 2", "3 - 10"], "Bỏ bớt một phần khỏi lượng ban đầu là phép trừ."),
        mc("Có 5 nhóm bằng nhau, mỗi nhóm 2 chấm. Phép tính nào phù hợp nhất?", "5 × 2", ["5 + 2", "5 - 2", "10 : 5"], "Nhiều nhóm bằng nhau được mô tả bằng phép nhân."),
    ],
    "WP_ONE_STEP_ADD_MORE": [
        nq("Lan có 24 bút chì, được cho thêm 13 bút. Lan có tất cả bao nhiêu bút?", 37, "24 + 13 = 37 vì số bút được thêm vào lượng ban đầu."),
        nq("Một giỏ có 125 quả, đặt thêm 243 quả. Giỏ có bao nhiêu quả?", 368, "125 + 243 = 368."),
        nq("Buổi sáng thư viện nhận 310 sách, buổi chiều nhận thêm 260 sách. Tổng số sách nhận là bao nhiêu?", 570, "310 + 260 = 570."),
    ],
    "WP_ONE_STEP_SUB_LESS": [
        nq("Có 45 quả bóng, cho đi 12 quả. Còn lại bao nhiêu quả?", 33, "45 - 12 = 33 vì 12 quả bị bớt khỏi số ban đầu."),
        nq("Kho có 786 hộp, chuyển đi 234 hộp. Còn bao nhiêu hộp?", 552, "786 - 234 = 552."),
        nq("Một lớp có 40 tờ giấy màu, dùng 18 tờ. Còn bao nhiêu tờ?", 22, "40 - 18 = 22."),
    ],
    "WP_ONE_STEP_MORE_THAN": [
        nq("An có 25 viên bi. Bình có nhiều hơn An 7 viên. Bình có bao nhiêu viên?", 32, "Bình nhiều hơn An 7 viên nên 25 + 7 = 32."),
        nq("Cây A cao 120 cm. Cây B cao hơn cây A 30 cm. Cây B cao bao nhiêu cm?", 150, "120 + 30 = 150 cm.", unit="cm"),
        nq("Giỏ đỏ có 240 quả. Giỏ xanh có nhiều hơn 120 quả. Giỏ xanh có bao nhiêu quả?", 360, "240 + 120 = 360."),
    ],
    "WP_ONE_STEP_LESS_THAN": [
        nq("Mai có 30 nhãn dán. Linh có ít hơn Mai 8 nhãn. Linh có bao nhiêu nhãn?", 22, "Linh ít hơn Mai 8 nên 30 - 8 = 22."),
        nq("Dây A dài 90 cm. Dây B ngắn hơn dây A 20 cm. Dây B dài bao nhiêu cm?", 70, "90 - 20 = 70 cm.", unit="cm"),
        nq("Kho lớn có 650 hộp. Kho nhỏ ít hơn 230 hộp. Kho nhỏ có bao nhiêu hộp?", 420, "650 - 230 = 420."),
    ],
    "WP_ONE_STEP_MULTIPLICATION_CONTEXT": [
        nq("Có 6 bàn, mỗi bàn 2 bạn. Có tất cả bao nhiêu bạn?", 12, "6 nhóm 2 bạn nên 6 × 2 = 12."),
        nq("Có 4 hộp, mỗi hộp 5 bút. Có bao nhiêu bút?", 20, "4 × 5 = 20."),
        nq("Một tuần học có 5 ngày, mỗi ngày làm 2 bài luyện. Cả tuần làm bao nhiêu bài?", 10, "5 nhóm 2 bài nên 5 × 2 = 10."),
    ],
    "WP_ONE_STEP_DIVISION_CONTEXT": [
        nq("Có 14 chiếc bánh chia đều cho 2 đĩa. Mỗi đĩa có bao nhiêu chiếc?", 7, "14 : 2 = 7."),
        nq("Có 35 bút xếp vào các hộp, mỗi hộp 5 bút. Cần bao nhiêu hộp?", 7, "35 : 5 = 7."),
        nq("20 học sinh chia đều thành 5 nhóm. Mỗi nhóm có bao nhiêu học sinh?", 4, "20 : 5 = 4."),
    ],
    "WP_SELECT_OPERATION_ONE_STEP": [
        mc("Có 10 quả, ăn 2 quả, hỏi còn lại. Chọn phép tính đúng.", "10 - 2", ["10 + 2", "10 × 2", "10 : 2"], "Tình huống lấy bớt và hỏi còn lại nên dùng phép trừ."),
        mc("Có 5 giỏ, mỗi giỏ 2 quả, hỏi tất cả. Chọn phép tính đúng.", "5 × 2", ["5 + 2", "5 - 2", "10 : 5"], "Nhiều nhóm bằng nhau và hỏi tổng nên dùng phép nhân."),
        mc("Có 30 bút chia đều cho 5 bạn, hỏi mỗi bạn được bao nhiêu. Chọn phép tính đúng.", "30 : 5", ["30 + 5", "30 - 5", "5 + 5"], "Chia đều tổng số bút thành 5 phần nên dùng phép chia."),
    ],

    "POINT_RECOGNIZE": [
        mc("Kí hiệu nào phù hợp để đặt tên một điểm?", "A", ["5 cm", "2 kg", "10 l"], "Điểm thường được đặt tên bằng chữ cái in hoa như A."),
        mc("Phát biểu nào đúng về một điểm?", "Điểm biểu diễn một vị trí", ["Điểm có độ dài 5 cm", "Điểm luôn là một hình tròn lớn", "Điểm có hai đầu mút"], "Điểm dùng để chỉ vị trí và không có độ dài."),
        mc("Trên hình có ba vị trí được đánh dấu A, B, C. Có bao nhiêu điểm được đặt tên?", "3", ["1", "2", "4"], "Mỗi tên A, B, C chỉ một điểm, nên có 3 điểm."),
    ],
    "LINE_SEGMENT_RECOGNIZE": [
        mc("Đoạn thẳng AB có bao nhiêu đầu mút?", "2", ["0", "1", "3"], "Đoạn thẳng AB có hai đầu mút A và B."),
        mc("Mô tả nào đúng về đoạn thẳng?", "Phần thẳng nối hai đầu mút", ["Đường uốn cong không có đầu", "Khối tròn", "Một điểm duy nhất"], "Đoạn thẳng là phần thẳng nối hai điểm đầu mút."),
        mc("Nếu nối thẳng điểm M với điểm N, hình nhận được gọi là gì?", "đoạn thẳng MN", ["khối cầu", "đường cong", "điểm MN"], "Phần thẳng nối hai điểm M và N là đoạn thẳng MN."),
    ],
    "CURVE_RECOGNIZE": [
        mc("Đường nào được gọi là đường cong?", "Đường uốn lượn", ["Đường đi thẳng không đổi hướng", "Một điểm", "Một đoạn chỉ có hai đầu mút nhưng luôn thẳng"], "Đường cong có hình dạng uốn lượn, không giữ hướng thẳng trên toàn bộ đường."),
        mc("Một nét vẽ hình vòng cung là ví dụ gần nhất của loại đường nào?", "đường cong", ["đường thẳng", "đoạn thẳng", "điểm"], "Vòng cung là một đường cong."),
        mc("Phát biểu nào đúng?", "Đường cong có thể đổi hướng mềm mại", ["Đường cong luôn có bốn cạnh", "Đường cong là khối cầu", "Đường cong luôn kéo thẳng về hai phía"], "Đặc điểm nhận biết là đường bị uốn và thay đổi hướng."),
    ],
    "STRAIGHT_LINE_RECOGNIZE": [
        mc("Đường thẳng khác đoạn thẳng ở điểm nào?", "Có thể kéo dài về hai phía", ["Luôn cong", "Có đúng bốn cạnh", "Là một khối"], "Đường thẳng không bị giới hạn bởi hai đầu mút như đoạn thẳng."),
        mc("Một nét không uốn cong và có thể kéo dài mãi theo hai hướng là gì?", "đường thẳng", ["đường cong", "khối trụ", "điểm"], "Đó là mô tả của đường thẳng."),
        mc("Hai điểm A và B cùng nằm trên một đường thẳng d. Phát biểu nào đúng?", "A và B nằm trên d", ["d là khối cầu", "A là một đoạn thẳng", "B có độ dài"], "Dữ kiện đã cho xác định cả hai điểm cùng thuộc đường thẳng d."),
    ],
    "POLYLINE_RECOGNIZE": [
        mc("Đường gấp khúc được tạo bởi gì?", "Nhiều đoạn thẳng nối tiếp", ["Chỉ một điểm", "Chỉ một đường cong", "Một khối cầu"], "Đường gấp khúc là chuỗi các đoạn thẳng nối nhau."),
        mc("Một đường gồm ba đoạn AB, BC, CD nối tiếp nhau được gọi là gì?", "đường gấp khúc ABCD", ["khối trụ", "đường cong tròn", "một điểm"], "Ba đoạn thẳng nối tiếp tại B và C tạo một đường gấp khúc."),
        mc("Đường gấp khúc có 4 đoạn thẳng thì ít nhất có bao nhiêu điểm nối/đầu được nêu theo chuỗi mở?", "5", ["2", "3", "4"], "Một chuỗi mở 4 đoạn cần 5 điểm liên tiếp để tạo 4 đoạn."),
    ],
    "THREE_COLLINEAR_POINTS": [
        mc("Ba điểm A, B, C cùng nằm trên một đường thẳng. Ta nói ba điểm thế nào?", "thẳng hàng", ["vuông góc", "cong", "tạo khối cầu"], "Ba điểm cùng nằm trên một đường thẳng là ba điểm thẳng hàng."),
        mc("A và B nằm trên đường d, C nằm lệch khỏi d. Ba điểm A, B, C có thẳng hàng không?", "không", ["có", "luôn luôn", "không đủ vì điểm không có tên"], "C không nằm trên đường qua A và B nên ba điểm không thẳng hàng."),
        mc("Muốn kiểm tra ba điểm thẳng hàng, ta cần xem điều gì?", "Cả ba có cùng nằm trên một đường thẳng", ["Cả ba có cùng tên", "Có hai điểm trùng nhau", "Có một hình tròn bên cạnh"], "Điều kiện trực quan là ba điểm cùng thuộc một đường thẳng."),
    ],
    "QUADRILATERAL_RECOGNIZE": [
        nq("Một hình tứ giác có bao nhiêu cạnh?", 4, "Theo định nghĩa, hình tứ giác là hình kín có 4 cạnh."),
        mc("Hình nào chắc chắn là một tứ giác?", "hình chữ nhật", ["hình tam giác", "khối cầu", "đường cong"], "Hình chữ nhật có bốn cạnh và bốn đỉnh nên là tứ giác."),
        mc("Một hình kín có đúng 4 cạnh được gọi chung là gì?", "hình tứ giác", ["hình tam giác", "đường gấp khúc mở", "khối trụ"], "Hình kín có bốn cạnh là tứ giác."),
    ],
    "CYLINDER_RECOGNIZE": [
        mc("Vật nào gần dạng khối trụ nhất?", "lon nước", ["quả bóng", "tờ giấy phẳng", "điểm A"], "Lon nước có hai đáy tròn và mặt cong xung quanh, gần dạng khối trụ."),
        mc("Khối trụ có đặc điểm nào?", "Hai đáy tròn và một mặt cong xung quanh", ["Không có mặt nào", "Chỉ có một điểm", "Bốn cạnh phẳng"], "Đặc trưng trực quan của khối trụ là hai đáy tròn cùng mặt cong bao quanh."),
        mc("Một ống hình trụ đứng thẳng, mặt trên và mặt dưới gần hình gì?", "hình tròn", ["tam giác", "đường thẳng", "điểm"], "Hai đáy của khối trụ là các hình tròn."),
    ],
    "SPHERE_RECOGNIZE": [
        mc("Vật nào gần dạng khối cầu nhất?", "quả bóng", ["lon nước", "hộp chữ nhật", "thước thẳng"], "Quả bóng tròn đều theo mọi hướng, gần dạng khối cầu."),
        mc("Khối cầu có cạnh hay đỉnh không?", "không", ["có 1 cạnh", "có 2 đỉnh", "có 4 cạnh"], "Khối cầu không có cạnh và không có đỉnh."),
        mc("Mô tả nào phù hợp với khối cầu?", "Tròn đều và không có đáy phẳng", ["Có hai đáy tròn", "Có bốn cạnh", "Là một đoạn thẳng"], "Khối cầu tròn đều theo mọi hướng và không có mặt đáy phẳng."),
    ],
    "DRAW_SEGMENT_GIVEN_LENGTH": [
        nq("Trên thước, đặt đầu A ở vạch 2 cm. Muốn AB dài 5 cm và B ở bên phải A, B ở vạch bao nhiêu?", 7, "Vị trí B = 2 + 5 = 7 cm."),
        iq("Trên thước cm, chọn hai đầu mút tại vạch 1 và vạch 9. Độ dài đoạn thẳng tạo được là bao nhiêu cm?", 8, "Hai đầu mút cách nhau 9 - 1 = 8 cm.", numeric_max=20),
        nq("Muốn tạo đoạn thẳng dài 6 cm với một đầu ở vạch 3 cm và đầu kia ở bên phải, chọn vạch nào?", 9, "3 + 6 = 9 cm."),
    ],
    "FOLD_CUT_COMPOSE_SHAPES": [
        mc("Ghép hai tam giác vuông bằng nhau theo một cạnh phù hợp có thể tạo thành hình nào trong nhiều cách ghép?", "một tứ giác", ["một điểm", "một khối cầu", "một đường thẳng vô hạn"], "Hai mảnh tam giác có thể ghép cạnh với cạnh để tạo một hình kín bốn cạnh trong cách ghép phù hợp."),
        mc("Khi cắt một tờ giấy hình vuông theo một đường thẳng từ góc này đến góc đối diện, thường thu được bao nhiêu mảnh?", "2", ["1", "3", "4"], "Một đường cắt chéo xuyên hết hình vuông chia tờ giấy thành hai mảnh."),
        mc("Mục đích của hoạt động ghép hình là gì?", "Nhận ra có thể tạo hình mới từ các mảnh hình", ["Làm thay đổi giá trị số của hình", "Biến mọi hình thành khối cầu", "Bỏ qua cạnh của các mảnh"], "Ghép hình giúp thấy quan hệ giữa các mảnh và hình mới được tạo thành."),
    ],

    "HEAVIER_LIGHTER": [
        mc("Trên cân thăng bằng, đĩa bên trái hạ thấp hơn. Vật bên trái thường thế nào?", "nặng hơn", ["nhẹ hơn", "bằng 0 kg", "không có khối lượng"], "Với cân hoạt động đúng, phía hạ thấp hơn là phía nặng hơn."),
        mc("Nếu vật A nặng hơn vật B thì vật B thế nào so với A?", "nhẹ hơn", ["nặng hơn", "bằng nhau chắc chắn", "không đo được"], "Quan hệ nặng hơn - nhẹ hơn là hai chiều đối lập."),
        mc("Cân cho hai đĩa ngang bằng. Kết luận phù hợp nhất là gì?", "Hai bên có khối lượng bằng nhau trong phép cân đó", ["Bên trái nặng hơn", "Bên phải nặng hơn", "Không bên nào có khối lượng"], "Hai đĩa cân bằng cho thấy khối lượng hai bên bằng nhau trong điều kiện cân."),
    ],
    "MASS_KG_READ_WRITE": [
        nq("Một bao gạo ghi 5 kg. Số đo khối lượng là bao nhiêu kg?", 5, "Con số đi trước đơn vị kg là 5.", unit="kg"),
        mc("Kí hiệu đúng của kilôgam là gì?", "kg", ["km", "l", "cm"], "Kilôgam được kí hiệu là kg."),
        uq("Hai túi lần lượt nặng 2 kg và 3 kg. Hãy nhập kết quả kèm đơn vị.", 5, "kg", "Cùng đơn vị kg nên 2 kg + 3 kg = 5 kg.", aliases=["kilôgam"]),
    ],
    "CAPACITY_LITER_READ_WRITE": [
        nq("Một bình ghi 4 l. Dung tích được ghi là bao nhiêu lít?", 4, "Con số đi trước kí hiệu l là 4.", unit="l"),
        mc("Kí hiệu nào là đơn vị lít?", "l", ["kg", "km", "dm"], "Lít được kí hiệu là l."),
        nq("Bình có 7 l nước, rót thêm 2 l. Có tất cả bao nhiêu lít?", 9, "7 l + 2 l = 9 l.", unit="l"),
    ],
    "LENGTH_DM_M_KM_RECOGNIZE_RELATION": [
        nq("1 m bằng bao nhiêu dm?", 10, "Theo quan hệ đơn vị, 1 m = 10 dm.", unit="dm"),
        nq("1 km bằng bao nhiêu m?", 1000, "Theo quan hệ đơn vị, 1 km = 1000 m.", unit="m"),
        mc("Đơn vị nào hợp lý hơn để nói quãng đường giữa hai làng?", "km", ["dm", "cm", "kg"], "Quãng đường dài thường được đo bằng ki-lô-mét."),
    ],
    "TIME_DAY_24_HOURS": [
        nq("Một ngày đầy đủ có bao nhiêu giờ?", 24, "Theo quan hệ thời gian, 1 ngày = 24 giờ.", unit="giờ"),
        mc("Khi đã đủ 24 giờ liên tiếp, khoảng thời gian đó bằng bao nhiêu ngày đầy đủ?", "1 ngày", ["2 ngày", "10 ngày", "không thể biết"], "Theo quan hệ đã học, 24 giờ liên tiếp bằng 1 ngày đầy đủ."),
        tf("Một ngày đầy đủ có ít hơn 24 giờ.", False, "Một ngày đầy đủ có đúng 24 giờ nên phát biểu 'ít hơn 24 giờ' là sai."),
    ],
    "TIME_HOUR_60_MINUTES": [
        nq("1 giờ bằng bao nhiêu phút?", 60, "Theo quan hệ thời gian, 1 giờ = 60 phút.", unit="phút"),
        mc("45 phút so với 1 giờ là khoảng thời gian thế nào?", "ngắn hơn", ["dài hơn", "bằng nhau", "không so sánh được"], "1 giờ = 60 phút; 45 phút ít hơn 60 phút nên ngắn hơn 1 giờ."),
        mc("Một hoạt động kéo dài đúng 60 phút. Khoảng thời gian đó bằng gì?", "1 giờ", ["1 ngày", "30 phút", "không thể biết"], "Theo quan hệ đã học, 60 phút bằng 1 giờ."),
    ],
    "CALENDAR_DAYS_IN_MONTH_DATE": [
        nq("Tháng 4 có bao nhiêu ngày?", 30, "Tháng 4 có 30 ngày.", unit="ngày"),
        nq("Tháng 5 có bao nhiêu ngày?", 31, "Tháng 5 có 31 ngày.", unit="ngày"),
        mc("Ngày sau ngày 14 tháng 9 là ngày nào?", "15 tháng 9", ["13 tháng 9", "14 tháng 10", "16 tháng 9"], "Ngày kế tiếp tăng số ngày thêm 1 trong cùng tháng, nên là 15 tháng 9."),
    ],
    "MONEY_VND_NOTE_RECOGNITION": [
        mc("Khi xem hình một tờ tiền Việt Nam, thông tin nào cần đọc để nhận biết giá trị?", "Con số mệnh giá và chữ đồng trên tờ", ["Màu yêu thích của người xem", "Số trang của sách bên cạnh", "Chiều dài của bàn học"], "Để nhận biết giá trị, cần đọc con số mệnh giá và đơn vị đồng thể hiện trên tờ tiền."),
        tf("Chỉ nhìn màu sắc là đủ để xác định chắc chắn giá trị của một tờ tiền Việt Nam.", False, "Màu sắc có thể hỗ trợ quan sát nhưng cần đọc con số mệnh giá và đơn vị đồng để nhận biết giá trị."),
        mc("Có hai hình tờ tiền A và B với con số mệnh giá khác nhau. Muốn biết tờ nào có giá trị lớn hơn, bước nào phù hợp nhất?", "Đọc và so sánh con số mệnh giá trên hai tờ", ["Chọn tờ có màu mình thích hơn", "Chọn tờ nằm bên trái", "Đếm số chữ xuất hiện trên mỗi tờ"], "Đọc đúng con số mệnh giá trên từng tờ rồi so sánh hai giá trị; không đoán chỉ từ màu sắc hoặc vị trí."),
    ],
    "MEASURE_WITH_RULER_CM": [
        nq("Một đoạn thẳng bắt đầu ở vạch 0 cm và kết thúc ở vạch 8 cm. Dài bao nhiêu cm?", 8, "8 - 0 = 8 cm.", unit="cm"),
        nq("Một đoạn bắt đầu ở vạch 3 cm và kết thúc ở vạch 10 cm. Dài bao nhiêu cm?", 7, "10 - 3 = 7 cm.", unit="cm"),
        nq("Bút chì đặt từ vạch 2 cm đến vạch 11 cm. Bút dài bao nhiêu cm?", 9, "11 - 2 = 9 cm.", unit="cm"),
    ],
    "MEASURE_WITH_COMMON_SCALE": [
        nq("Một thang đo có các vạch 0, 2, 4, 6, 8. Mỗi vạch tăng bao nhiêu đơn vị?", 2, "Hiệu giữa hai vạch liên tiếp là 2."),
        nq("Thang đo có vạch 10, 20, 30, __, 50. Điền giá trị vạch thiếu.", 40, "Mỗi bước tăng 10 nên vạch thiếu là 40."),
        nq("Trên thang chia đều, vạch thứ nhất là 0, vạch thứ hai là 5. Vạch thứ tư có giá trị bao nhiêu?", 15, "Các vạch lần lượt 0, 5, 10, 15."),
    ],
    "CLOCK_MINUTE_HAND_AT_3_OR_6": [
        mc("Kim phút chỉ số 3, kim giờ vừa qua số 4. Đồng hồ chỉ thời gian nào?", "4 giờ 15 phút", ["4 giờ 30 phút", "3 giờ 4 phút", "4 giờ 3 phút"], "Kim phút ở số 3 tương ứng 15 phút; kim giờ ở khoảng sau 4 nên là 4 giờ 15 phút."),
        mc("Kim phút chỉ số 6, kim giờ ở giữa 7 và 8. Đồng hồ chỉ thời gian nào?", "7 giờ 30 phút", ["7 giờ 15 phút", "6 giờ 7 phút", "8 giờ 30 phút"], "Kim phút ở số 6 là 30 phút; kim giờ giữa 7 và 8 là 7 giờ 30 phút."),
        mc("Khi kim phút chỉ số 3 thì số phút là bao nhiêu?", "15 phút", ["3 phút", "30 phút", "60 phút"], "Mỗi số trên đồng hồ ứng 5 phút; 3 × 5 = 15 phút."),
    ],
    "MEASUREMENT_CONVERT_CALCULATE_LEARNED_UNITS": [
        nq("3 m bằng bao nhiêu dm?", 30, "1 m = 10 dm nên 3 m = 30 dm.", unit="dm"),
        nq("2 kg + 3 kg bằng bao nhiêu kg?", 5, "Hai số đo cùng đơn vị kg nên cộng 2 + 3 = 5 kg.", unit="kg"),
        nq("5 l - 2 l bằng bao nhiêu lít?", 3, "Hai số đo cùng đơn vị l nên 5 - 2 = 3 l.", unit="l"),
    ],
    "MEASUREMENT_ESTIMATE_BASIC": [
        mc("Chiều dài một chiếc bút chì hợp lý nhất khoảng bao nhiêu?", "15 cm", ["15 km", "15 m", "150 m"], "Bút chì là vật nhỏ, đơn vị cm và độ dài khoảng vài chục cm là hợp lý."),
        mc("Chiều cao một cánh cửa hợp lý nhất gần giá trị nào?", "2 m", ["2 cm", "2 km", "20 km"], "Cửa cao khoảng vài mét, nên 2 m là hợp lý."),
        mc("Một thanh tham chiếu dài 10 cm. Một vật nhìn dài khoảng gấp đôi thanh đó. Ước lượng vật dài bao nhiêu?", "20 cm", ["5 cm", "100 cm", "2 km"], "Gấp đôi mốc 10 cm là khoảng 20 cm."),
    ],
    "POLYLINE_LENGTH_SUM_SEGMENTS": [
        nq("Đường gấp khúc có hai đoạn dài 4 cm và 6 cm. Tổng độ dài là bao nhiêu?", 10, "4 + 6 = 10 cm.", unit="cm"),
        nq("Ba đoạn của đường gấp khúc dài 3 cm, 5 cm và 2 cm. Độ dài cả đường là bao nhiêu?", 10, "3 + 5 + 2 = 10 cm.", unit="cm"),
        nq("Một đường gấp khúc gồm các đoạn 7 cm, 4 cm, 6 cm. Tổng độ dài là bao nhiêu?", 17, "7 + 4 + 6 = 17 cm.", unit="cm"),
    ],
    "MEASUREMENT_REAL_WORLD_ONE_STEP": [
        nq("Một sợi dây dài 2 m, nối thêm đoạn 3 m. Dây dài tất cả bao nhiêu mét?", 5, "2 m + 3 m = 5 m.", unit="m"),
        nq("Bình có 8 l nước, rót ra 3 l. Còn bao nhiêu lít?", 5, "8 l - 3 l = 5 l.", unit="l"),
        nq("Bao A nặng 6 kg, bao B nặng 4 kg. Cả hai nặng bao nhiêu kg?", 10, "6 kg + 4 kg = 10 kg.", unit="kg"),
    ],

    "DATA_COLLECT_CLASSIFY_COUNT": [
        nq("Có các thẻ: đỏ, xanh, đỏ, vàng, đỏ. Có bao nhiêu thẻ đỏ?", 3, "Phân loại theo màu rồi đếm ba thẻ đỏ."),
        nq("Danh sách vật: bút, sách, bút, thước, bút, sách. Có bao nhiêu bút?", 3, "Lọc các mục 'bút' rồi đếm được 3."),
        nq("Một lớp ghi loại quả yêu thích: táo, cam, táo, táo, cam, chuối. Có bao nhiêu bạn chọn cam?", 2, "Trong danh sách có hai mục 'cam'."),
    ],
    "PICTOGRAPH_READ_DESCRIBE": [
        nq("Biểu đồ tranh có 4 hình ngôi sao cho nhóm A; chú giải 1 hình = 1 bạn. Nhóm A có bao nhiêu bạn?", 4, "4 biểu tượng × 1 bạn mỗi biểu tượng = 4 bạn."),
        nq("Biểu đồ có 3 hình quả táo; chú giải 1 hình = 2 quả. Có bao nhiêu quả táo?", 6, "3 biểu tượng × 2 quả = 6 quả."),
        nq("Một hàng có 5 biểu tượng, chú giải 1 biểu tượng = 2 quyển sách. Hàng đó biểu diễn bao nhiêu quyển?", 10, "5 × 2 = 10 quyển."),
    ],
    "PICTOGRAPH_SIMPLE_INFERENCE": [
        nq("Biểu đồ: nhóm A có 6 biểu tượng, nhóm B có 4 biểu tượng; 1 biểu tượng = 1 bạn. A nhiều hơn B bao nhiêu bạn?", 2, "6 - 4 = 2 bạn."),
        mc("Biểu đồ có nhóm Cam 5 biểu tượng, Táo 7 biểu tượng, Chuối 3 biểu tượng; chú giải như nhau. Nhóm nào nhiều nhất?", "Táo", ["Cam", "Chuối", "Cả ba bằng nhau"], "7 biểu tượng lớn hơn 5 và 3 nên Táo nhiều nhất."),
        nq("Biểu đồ có 2 biểu tượng cho A và 5 biểu tượng cho B; 1 biểu tượng = 2 vật. B nhiều hơn A bao nhiêu vật?", 6, "B có 10 vật, A có 4 vật; 10 - 4 = 6."),
    ],
    "EVENT_POSSIBLE": [
        mc("Gieo xúc xắc có các mặt 1 đến 6. Xuất hiện số 4 là sự kiện gì?", "có thể", ["chắc chắn", "không thể", "bằng nhau"], "Số 4 là một mặt có thể xuất hiện nhưng không phải lần nào cũng ra 4."),
        mc("Trong túi có bóng đỏ và xanh. Lấy ngẫu nhiên một bóng, lấy được bóng đỏ là gì?", "có thể", ["chắc chắn", "không thể", "luôn sai"], "Có bóng đỏ trong túi nên có thể lấy được, nhưng còn bóng xanh nên không chắc chắn."),
        mc("Quay vòng có các số 1, 2, 3. Kim dừng ở số 2 là sự kiện gì?", "có thể", ["chắc chắn", "không thể", "không có kết quả"], "Số 2 có trên vòng quay nên có thể xảy ra, nhưng không phải kết quả duy nhất."),
    ],
    "EVENT_CERTAIN": [
        mc("Gieo xúc xắc chuẩn 1 đến 6. Kết quả là một số từ 1 đến 6. Sự kiện này là gì?", "chắc chắn", ["có thể nhưng không chắc", "không thể", "sai"], "Mọi mặt của xúc xắc đều là một số từ 1 đến 6."),
        mc("Trong túi chỉ có bóng xanh. Lấy một bóng, bóng lấy ra màu xanh là gì?", "chắc chắn", ["không thể", "có thể nhưng không chắc", "không có màu"], "Mọi bóng trong túi đều xanh nên lấy bóng nào cũng xanh."),
        mc("Một hộp chỉ chứa thẻ số 2 và 5. Rút một thẻ, số rút được là 2 hoặc 5. Sự kiện gì?", "chắc chắn", ["không thể", "chỉ có thể", "không xác định"], "Tất cả thẻ đều là 2 hoặc 5 nên sự kiện luôn xảy ra."),
    ],
    "EVENT_IMPOSSIBLE": [
        mc("Gieo xúc xắc chuẩn 1 đến 6. Xuất hiện số 8 là sự kiện gì?", "không thể", ["có thể", "chắc chắn", "luôn đúng"], "Không có mặt số 8 nên sự kiện không thể xảy ra."),
        mc("Trong túi chỉ có bóng đỏ. Lấy một bóng màu xanh là gì?", "không thể", ["chắc chắn", "có thể", "bằng nhau"], "Không có bóng xanh trong túi nên không thể lấy bóng xanh."),
        mc("Vòng quay chỉ có các số 1, 2, 3. Kim dừng ở số 5 là sự kiện gì?", "không thể", ["có thể", "chắc chắn", "luôn xảy ra"], "Số 5 không có trên vòng quay nên sự kiện không thể xảy ra."),
    ],
}

DIFFICULTIES = ["basic", "medium", "application"]


def build() -> tuple[dict, dict]:
    baseline = json.loads(BASELINE_PATH.read_text(encoding="utf-8"))
    domain_skills = {k: list(v) for k, v in baseline["domains"].items()}
    baseline_skills = [s for values in domain_skills.values() for s in values]
    if set(baseline_skills) != set(LESSON_INFO):
        missing = sorted(set(baseline_skills) - set(LESSON_INFO))
        extra = sorted(set(LESSON_INFO) - set(baseline_skills))
        raise SystemExit(f"LESSON_INFO mismatch missing={missing} extra={extra}")
    if set(baseline_skills) != set(Q):
        missing = sorted(set(baseline_skills) - set(Q))
        extra = sorted(set(Q) - set(baseline_skills))
        raise SystemExit(f"Q mismatch missing={missing} extra={extra}")

    chapter_by_domain = {c["domain_key"]: c["id"] for c in CHAPTERS}
    topic_by_id = {t["id"]: t for t in TOPICS}
    questions = []
    lessons = []

    for domain_key, skills in domain_skills.items():
        chapter_id = chapter_by_domain[domain_key]
        for order, skill in enumerate(skills, 1):
            topic_id, title, explanation, concept_name, concept_def, prerequisites = LESSON_INFO[skill]
            if topic_by_id[topic_id]["chapter_id"] != chapter_id:
                raise SystemExit(f"Topic/chapter mismatch for {skill}")
            q_specs = Q[skill]
            if len(q_specs) != 3:
                raise SystemExit(f"Need exactly 3 questions for {skill}")
            lesson_id = "m2_ls_" + slug(skill)
            q_ids = []
            for i, (difficulty, spec) in enumerate(zip(DIFFICULTIES, q_specs), 1):
                qid = f"m2_q_{slug(skill)}_{i:02d}"
                q_ids.append(qid)
                question_type = spec["question_type"]
                if domain_key == "word_problems" and question_type == "numeric_input":
                    question_type = "word_problem"
                q = {
                    "id": qid,
                    "lesson_id": lesson_id,
                    "skill_id": skill,
                    "difficulty": difficulty,
                    "question_type": question_type,
                    "prompt_vi": spec["prompt_vi"],
                    "answer_kind": spec["answer_kind"],
                    "correct_answer": spec["correct_answer"],
                    "accepted_answers": spec["accepted_answers"],
                    "explanation_vi": spec["explanation_vi"],
                    "hints_vi": [
                        "Nhớ kiến thức: " + concept_def,
                        "Thực hiện từng bước và kiểm tra lại với dữ kiện của câu hỏi.",
                    ],
                    "tags": [skill.lower(), domain_key, difficulty, question_type, spec["answer_kind"]],
                    "validation": spec["validation"],
                    "status": "CHILD_READY",
                }
                for optional_key in ("answer_unit", "correct_choice_id", "expected_unit", "accepted_units", "choices"):
                    if optional_key in spec:
                        q[optional_key] = spec[optional_key]
                questions.append(q)

            basic = questions[-3]
            worked_answer = str(basic["accepted_answers"][0])
            lessons.append({
                "id": lesson_id,
                "chapter_id": chapter_id,
                "topic_id": topic_id,
                "skill_id": skill,
                "order_in_domain": order,
                "title_vi": title,
                "objectives_vi": [
                    "Nhận biết và thực hiện đúng nội dung: " + title.lower() + ".",
                    "Giải thích được cách làm bằng ngôn ngữ ngắn gọn và kiểm tra kết quả theo dữ kiện.",
                ],
                "explanation_vi": explanation,
                "concepts": [{
                    "id": f"m2_cp_{slug(skill)}_01",
                    "name_vi": concept_name,
                    "definition_vi": concept_def,
                }],
                "worked_examples": [{
                    "id": f"m2_ex_{slug(skill)}_01",
                    "prompt_vi": basic["prompt_vi"],
                    "solution_steps_vi": [basic["explanation_vi"], "Đối chiếu kết quả với yêu cầu và đơn vị của câu hỏi."],
                    "answer": worked_answer,
                }],
                "practice_sets": {
                    "basic": [q_ids[0]],
                    "medium": [q_ids[1]],
                    "application": [q_ids[2]],
                },
                "prerequisite_skills": prerequisites,
                "difficulty_span": ["basic", "medium", "application"],
                "status": "CHILD_READY",
            })

    lesson_catalog = {
        "schema_version": 1,
        "catalog_id": "math_grade2_lesson_catalog_v1",
        "subject": "math",
        "grade": 2,
        "curriculum_id": baseline["curriculum_id"],
        "language": "vi",
        "id_policy": "deterministic_ascii_lower_snake_preserve_existing_uppercase_skill_ids",
        "chapters": CHAPTERS,
        "topics": TOPICS,
        "lessons": lessons,
    }
    question_bank = {
        "schema_version": 1,
        "bank_id": "math_grade2_static_question_bank_v1",
        "subject": "math",
        "grade": 2,
        "curriculum_id": baseline["curriculum_id"],
        "language": "vi",
        "supported_answer_kinds": ["integer", "interaction_integer", "number", "decimal", "fraction", "text", "unit", "expression"],
        "grade2_used_answer_kinds": sorted({q["answer_kind"] for q in questions}),
        "question_types": sorted({q["question_type"] for q in questions}),
        "questions": questions,
    }
    return lesson_catalog, question_bank


def main() -> None:
    catalog, bank = build()
    PACK_ROOT.mkdir(parents=True, exist_ok=True)
    LESSON_PATH.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    QUESTION_PATH.write_text(json.dumps(bank, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"WROTE lessons={len(catalog['lessons'])} questions={len(bank['questions'])}")


if __name__ == "__main__":
    main()
