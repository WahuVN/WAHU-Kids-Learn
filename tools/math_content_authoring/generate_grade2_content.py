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


def first_objective(question_types: list[str], concept_name: str) -> str:
    """Describe the first concrete learning outcome instead of repeating the lesson title."""
    concept = concept_name.strip().lower()
    types = set(question_types)
    if "interactive_measurement" in types:
        return f"Thực hành {concept} trên vạch đo và kiểm tra độ dài tạo được."
    if "unit_input" in types:
        return f"Dùng đơn vị {concept} để tìm số đo và ghi đúng phần số cùng đơn vị."
    if "expression_input" in types:
        return f"Vận dụng quy tắc {concept} để viết hoặc tính biểu thức đúng thứ tự."
    if "word_problem" in types:
        return f"Giải được bài toán một bước về {concept} bằng phép tính phù hợp."
    if concept in {"có thể", "chắc chắn", "không thể"}:
        return f"Phân loại đúng sự kiện {concept} từ các kết quả có thể xảy ra."
    if concept == "chọn phép tính":
        return "Biết cách chọn phép tính đúng cho một tình huống một bước cơ bản."
    has_choice = bool(types & {"multiple_choice", "true_false"})
    has_numeric = "numeric_input" in types
    if has_choice and has_numeric:
        return f"Vận dụng kiến thức về {concept} để chọn hoặc tìm kết quả đúng."
    if has_choice:
        return f"Chọn đúng mô tả hoặc kết luận về {concept} trong tình huống đơn giản."
    return f"Vận dụng kiến thức về {concept} để tìm kết quả đúng trong bài tập cơ bản."


def second_objective(question_types: list[str], concept_name: str) -> str:
    """Create a lesson-specific second objective from the concept and authored practice surfaces."""
    concept = concept_name.strip().lower()
    types = set(question_types)
    if "interactive_measurement" in types:
        return f"Vận dụng {concept} để thao tác trên vạch đo và kiểm tra kết quả bằng cách đo lại."
    if "unit_input" in types:
        return f"Vận dụng {concept} để tìm số đo trong tình huống mới và ghi đúng đơn vị của kết quả."
    if "expression_input" in types:
        return f"Vận dụng {concept} để lập hoặc tính biểu thức đúng thứ tự và tự kiểm tra kết quả."
    if "word_problem" in types:
        return f"Vận dụng {concept} để chọn phép tính, giải một bài toán một bước và kiểm tra kết quả theo dữ kiện."
    if types & {"multiple_choice", "true_false"}:
        return f"Phân biệt và giải thích đúng {concept} khi gặp một ví dụ hoặc tình huống mới."
    return f"Vận dụng {concept} với dữ kiện mới, nêu cách tìm kết quả và tự kiểm tra bằng quy tắc đã học."


def numeric_feedback_check(concept_name: str) -> str:
    concept = concept_name.strip().lower()
    # Measurement concepts must be classified before multiplication/division: "vạch chia"
    # contains the word "chia" but is not a division skill.
    if any(token in concept for token in ("đo bằng thước", "vạch chia", "độ dài đường gấp khúc", "vẽ đoạn thẳng")):
        return " Kiểm tra lại khoảng cách giữa các vạch hoặc từng đoạn đã dùng trước khi chốt số đo."
    if "tính với số đo" in concept or "giải toán đo lường" in concept:
        return " Kiểm tra các số đo đã cùng đơn vị rồi làm lại phép tính một lần nữa."
    if concept in {"nhân 2", "nhân 5"}:
        return " Đếm thêm theo đúng bước của bảng nhân để kiểm tra tích vừa tìm được."
    if concept in {"chia 2", "chia 5"}:
        return " Dùng phép nhân ngược để kiểm tra thương nhân với số chia có trở lại số bị chia."
    if concept == "chia đều":
        return " Kiểm tra số nhóm và số phần tử mỗi nhóm có ghép lại đúng tổng ban đầu."
    if "nhân" in concept:
        return " Kiểm tra số nhóm bằng nhau và số phần tử mỗi nhóm trước khi chốt tích."
    if "chia" in concept:
        return " Dùng quan hệ ngược với phép nhân để kiểm tra kết quả chia."
    if any(token in concept for token in ("cộng", "trừ", "tính từ trái sang phải", "nhẩm")):
        return " Tính lại từng bước theo đúng thứ tự để kiểm tra kết quả."
    if "biểu đồ" in concept or "dữ liệu" in concept:
        return " Đếm lại biểu tượng hoặc dữ liệu cần dùng rồi kiểm tra phép tính."
    if "đọc lịch" in concept:
        return " Kiểm tra lại đúng ngày và tháng trên lịch trước khi chốt kết quả."
    if "ước lượng" in concept:
        return " So lại kết quả với mốc chục gần nhất để xem ước lượng có hợp lý không."
    if concept in {"kilôgam", "lít"}:
        return " Kiểm tra lại số đứng cùng đơn vị được hỏi trong đề."
    return f" Làm lại bước chính của {concept} để kiểm tra kết quả vừa tìm được."


def word_problem_feedback_check(concept_name: str) -> str:
    concept = concept_name.strip().lower()
    reasons = {
        "thêm vào": " Đề cho thêm vào lượng ban đầu và hỏi tất cả sau khi thêm, nên dùng phép cộng.",
        "bớt đi": " Đề cho bớt khỏi lượng ban đầu và hỏi phần còn lại, nên dùng phép trừ.",
        "nhiều hơn": " Đại lượng cần tìm nhiều hơn đại lượng đã biết một phần cho trước, nên cộng phần hơn.",
        "ít hơn": " Đại lượng cần tìm ít hơn đại lượng đã biết một phần cho trước, nên trừ phần kém.",
        "nhân trong tình huống": " Có nhiều nhóm bằng nhau và đề hỏi tất cả, nên dùng phép nhân.",
        "chia trong tình huống": " Đề chia thành các phần bằng nhau hoặc hỏi số nhóm, nên dùng phép chia.",
    }
    return reasons.get(concept, f" Đối chiếu quan hệ {concept} trong đề với phép tính vừa dùng để kiểm tra kết quả.")


def deepen_explanation(explanation: str, question_type: str, concept_name: str) -> str:
    """Keep concise authored math, but add the missing why/check step when feedback is too terse."""
    text = explanation.strip()
    if len(text) >= 32:
        return text
    concept = concept_name.strip().lower()
    suffixes = {
        "word_problem": word_problem_feedback_check(concept_name),
        "numeric_input": numeric_feedback_check(concept_name),
        "expression_input": f" Thứ tự các bước phải đúng với {concept}; tính lại từng bước sẽ kiểm tra được kết quả.",
        "multiple_choice": f" Lựa chọn này khớp với {concept}; các phương án khác lệch đặc điểm hoặc dữ kiện cần dùng.",
        "true_false": f" Mệnh đề được kiểm tra trực tiếp bằng {concept}, không dựa vào phỏng đoán.",
        "unit_input": f" Khi dùng {concept}, cần kiểm tra cả phần số và đơn vị của kết quả.",
        "interactive_measurement": f" Có thể kiểm tra lại bằng {concept} trên cùng các vạch đo.",
    }
    return text + suffixes.get(question_type, suffixes["numeric_input"])


def normalize_answer_evidence(text: object) -> str:
    value = str(text).lower().strip().replace("×", "x").replace("÷", ":")
    value = re.sub(r"[^\w\d]+", " ", value, flags=re.UNICODE)
    return re.sub(r"\s+", " ", value).strip()


def explanation_with_answer(explanation: str, answer_display: object) -> str:
    text = explanation.strip()
    answer = str(answer_display).strip()
    if not answer:
        return text
    evidence = normalize_answer_evidence(answer)
    if evidence and evidence in normalize_answer_evidence(text):
        return text
    return text + f" Vậy đáp án là {answer}."


def hint_reveals_unseen_answer(prompt: str, hint: str, answer: object, answer_kind: str, question_type: str) -> bool:
    if question_type == "true_false":
        return False
    answer_text = str(answer).strip()
    if not answer_text:
        return False
    if answer_kind in {"integer", "interaction_integer"}:
        answer_token = str(answer)
        prompt_numbers = re.findall(r"(?<!\d)[+-]?\d+(?!\d)", prompt)
        hint_numbers = re.findall(r"(?<!\d)[+-]?\d+(?!\d)", hint)
        return answer_token not in prompt_numbers and answer_token in hint_numbers
    answer_evidence = normalize_answer_evidence(answer_text)
    prompt_evidence = normalize_answer_evidence(prompt)
    hint_evidence = normalize_answer_evidence(hint)
    if answer_evidence:
        answer_phrase = f" {answer_evidence} "
        prompt_phrase = f" {prompt_evidence} "
        hint_phrase = f" {hint_evidence} "
        return answer_phrase not in prompt_phrase and answer_phrase in hint_phrase
    return answer_text not in prompt and answer_text in hint


def answer_safe_hint(candidate: str, prompt: str, answer: object, answer_kind: str, question_type: str, level: int) -> str:
    if not hint_reveals_unseen_answer(prompt, candidate, answer, answer_kind, question_type):
        return candidate
    focus = " ".join(prompt.split())
    if len(focus) > 54:
        focus = focus[:51].rstrip() + "…"
    first = {
        "multiple_choice": f"Nhìn lại “{focus}”. Xác định điều đề hỏi rồi đối chiếu từng dữ kiện.",
        "numeric_input": f"Nhìn lại “{focus}”. Tách số đã biết và điều cần tìm trước khi tính.",
        "word_problem": f"Nhìn lại “{focus}”. Nói rõ đã biết gì và cần tìm gì trước khi tính.",
        "expression_input": f"Nhìn lại “{focus}”. Xác định thứ tự phép tính trước khi viết biểu thức.",
        "unit_input": f"Nhìn lại “{focus}”. Xác định đại lượng cần tìm và đơn vị phải ghi.",
        "interactive_measurement": f"Nhìn lại “{focus}”. Xác định hai đầu mút và độ dài cần tạo.",
    }
    second = {
        "multiple_choice": f"Từ “{focus}”, loại phương án trái dữ kiện rồi kiểm tra lựa chọn còn lại.",
        "numeric_input": f"Từ “{focus}”, viết phép tính ngắn rồi kiểm tra kết quả theo điều đề hỏi.",
        "word_problem": f"Từ “{focus}”, chọn phép tính đúng quan hệ rồi kiểm tra ngược kết quả.",
        "expression_input": f"Từ “{focus}”, viết biểu thức đúng thứ tự rồi tính lại từng bước.",
        "unit_input": f"Từ “{focus}”, tính phần số rồi kiểm tra đơn vị ở cuối đáp án.",
        "interactive_measurement": f"Từ “{focus}”, đặt hai đầu mút rồi đo lại trước khi xác nhận.",
    }
    table = first if level == 1 else second
    return table.get(question_type, table["numeric_input"])


def numeric_application_hint(concept_name: str) -> str:
    concept = concept_name.strip()
    key = concept.casefold()
    if "nhân" in key or "nhóm bằng nhau" in key:
        return f"Với “{concept}”, xác định số nhóm và số phần tử mỗi nhóm rồi viết phép nhân phù hợp."
    if "chia" in key:
        return f"Với “{concept}”, xác định tổng và số phần cần chia đều rồi viết phép chia phù hợp."
    if "giá trị theo hàng" in key or "đọc và viết số" in key:
        return f"Với “{concept}”, giữ đúng vị trí trăm, chục, đơn vị rồi kiểm tra số vừa lập."
    if "liền trước" in key or "liền sau" in key:
        return f"Với “{concept}”, chỉ thay đổi một đơn vị theo đúng hướng rồi kiểm tra hai số kề nhau."
    if "tia số" in key:
        return f"Với “{concept}”, tìm độ tăng giữa hai vạch kề nhau rồi đếm đúng số bước cần đi."
    if "lớn nhất" in key or "bé nhất" in key or "so sánh" in key:
        return f"Với “{concept}”, so từ hàng trăm đến hàng chục, đơn vị trước khi chọn kết quả."
    if any(token in key for token in ("cộng", "trừ", "tính từ trái sang phải", "nhẩm")):
        return f"Với “{concept}”, viết đúng phép tính rồi kiểm tra lại từng bước theo thứ tự."
    if any(token in key for token in ("đo bằng thước", "vạch chia", "độ dài đường gấp khúc", "vẽ đoạn thẳng")):
        return f"Với “{concept}”, đánh dấu các vạch hoặc đoạn cần dùng rồi tính từ đúng đầu mút."
    if "số đo" in key or "đo lường" in key or key in {"lít", "kilôgam"}:
        return f"Với “{concept}”, kiểm tra đơn vị của các số đo trước khi thực hiện phép tính."
    if "lịch" in key:
        return f"Với “{concept}”, xác định đúng ngày và tháng rồi mới dịch sang ngày trước hoặc ngày sau."
    if "biểu đồ" in key or "dữ liệu" in key or "phân loại" in key:
        return f"Với “{concept}”, đếm đúng dữ liệu cần dùng và chú ý chú giải trước khi tính."
    if "ước lượng" in key:
        return f"Với “{concept}”, chọn mốc gần nhất rồi kiểm tra xem kết quả có cùng cỡ với dữ kiện."
    return f"Với “{concept}”, nêu quy tắc cần dùng, viết bước tính ngắn rồi kiểm tra điều đề hỏi."


def first_hint(question_type: str, difficulty: str, concept_name: str) -> str:
    """Start with a concrete observation step instead of repeating a lesson definition."""
    concept = concept_name.strip()
    hints = {
        "multiple_choice": {
            "basic": f"Trước tiên, nhắc lại dấu hiệu của “{concept}” rồi đọc từng lựa chọn; chưa cần chọn ngay.",
            "medium": f"Tìm chi tiết trong đề liên quan đến “{concept}” trước, rồi mới so từng phương án.",
            "application": f"Xác định lỗi hoặc dữ kiện quyết định của “{concept}” trong tình huống trước khi chọn.",
        },
        "true_false": {
            "basic": f"Khoanh dữ kiện liên quan đến “{concept}”, rồi xem mệnh đề đang khẳng định điều gì.",
            "medium": f"Tách mệnh đề thành dữ kiện và kết luận; dùng “{concept}” để kiểm tra từng phần.",
            "application": f"Tìm điểm có thể làm mệnh đề sai theo “{concept}” trước khi quyết định Đúng hay Sai.",
        },
        "numeric_input": {
            "basic": f"Gạch dưới số hoặc vị trí liên quan đến “{concept}”, rồi xác định điều cần tìm trước khi tính.",
            "medium": f"Tách dữ kiện của “{concept}” thành từng phần; chỉ tính sau khi biết rõ điều cần tìm.",
            "application": f"Tìm chỗ dễ nhầm của “{concept}” trong tình huống và xác định bước đầu tiên cần làm.",
        },
        "word_problem": {
            "basic": f"Nói lại bằng lời: đề đã cho gì và hỏi gì; sau đó nối dữ kiện với “{concept}”.",
            "medium": f"Chia đề thành phần đã biết và phần cần tìm, rồi nhận ra quan hệ “{concept}” giữa chúng.",
            "application": f"Tìm phép tính dễ bị chọn nhầm trong tình huống và đối chiếu nó với “{concept}”.",
        },
        "expression_input": {
            "basic": f"Đánh dấu các số và phép tính thuộc “{concept}”, chưa tính cho đến khi thứ tự đã rõ.",
            "medium": f"Xác định phép tính nào phải viết trước theo “{concept}”, rồi mới ghép thành biểu thức.",
            "application": f"Biến từng dữ kiện thành một phần của biểu thức theo “{concept}” trước khi tính.",
        },
        "unit_input": {
            "basic": f"Xác định đại lượng và đơn vị đề hỏi trong “{concept}” trước khi làm phần số.",
            "medium": f"Đánh dấu số đo và đơn vị liên quan đến “{concept}”; kiểm tra chúng có cùng loại không.",
            "application": f"Tìm đại lượng cuối cùng cần trả lời theo “{concept}” rồi mới chọn cách tính.",
        },
        "interactive_measurement": {
            "basic": f"Xác định điểm đầu, điểm cuối và độ dài cần tạo theo “{concept}” trước khi thao tác.",
            "medium": f"Quan sát các vạch đo của “{concept}” và chọn hai đầu mút trước khi kéo đoạn thẳng.",
            "application": f"Tìm vạch xuất phát và khoảng cách cần giữ theo “{concept}” trước khi đặt điểm cuối.",
        },
    }
    by_type = hints.get(question_type, hints["numeric_input"])
    return by_type.get(difficulty, by_type["medium"])


def second_hint(question_type: str, difficulty: str, concept_name: str) -> str:
    """Give a child a concrete next move without revealing the authored answer."""
    concept = concept_name.strip()
    hints = {
        "multiple_choice": {
            "basic": f"Đối chiếu từng lựa chọn với kiến thức “{concept}”; loại ngay lựa chọn trái với dữ kiện của đề.",
            "medium": f"Tự xác định đặc điểm đúng của “{concept}” trước, rồi so với từng lựa chọn để loại dần.",
            "application": f"Đừng đoán theo vị trí đáp án. Tự giải theo “{concept}” trước rồi mới chọn phương án khớp kết quả.",
        },
        "true_false": {
            "basic": f"Kiểm tra trực tiếp mệnh đề bằng quy tắc “{concept}”, rồi mới quyết định Đúng hay Sai.",
            "medium": f"Tìm một dữ kiện xác nhận hoặc bác bỏ mệnh đề theo “{concept}”; chỉ một điểm sai cũng đủ chọn Sai.",
            "application": f"Thử giải hoặc kiểm chứng mệnh đề độc lập bằng “{concept}”, không dựa vào cảm giác khi đọc câu.",
        },
        "numeric_input": {
            "basic": f"Khoanh các số cần dùng, áp dụng “{concept}”, rồi viết phần số của kết quả.",
            "medium": f"Tách dữ kiện theo “{concept}”, tính từng phần cần thiết rồi kiểm tra ngược kết quả.",
            "application": numeric_application_hint(concept_name),
        },
        "word_problem": {
            "basic": f"Gạch chân số đã cho và điều cần tìm; dùng quan hệ “{concept}” để chọn phép tính.",
            "medium": f"Nói lại đề theo mẫu “đã biết gì, cần tìm gì”, rồi chọn phép tính phù hợp với “{concept}”.",
            "application": f"Có thể vẽ sơ đồ hoặc chia tình huống thành nhóm; dùng “{concept}” để kiểm tra phép tính trước khi tính.",
        },
        "expression_input": {
            "basic": f"Xác định đúng các số và phép tính của “{concept}”, rồi viết biểu thức theo đúng thứ tự.",
            "medium": f"Viết biểu thức trước khi tính; kiểm tra từng dấu phép tính có đúng với “{concept}” hay chưa.",
            "application": f"Mô hình hóa dữ kiện thành một biểu thức duy nhất theo “{concept}”, sau đó mới tính để tự kiểm tra.",
        },
        "unit_input": {
            "basic": f"Tìm phần số bằng “{concept}” trước, sau đó viết kèm đúng đơn vị mà đề yêu cầu.",
            "medium": f"Kiểm tra số đo và đơn vị có cùng loại theo “{concept}”; tính phần số rồi ghi đơn vị ở cuối.",
            "application": f"Xác định đại lượng cần trả lời theo “{concept}”, tính kết quả và kiểm tra lại cả số lẫn đơn vị.",
        },
        "interactive_measurement": {
            "basic": f"Dùng “{concept}”: chọn hai đầu mút trên vạch đo rồi đếm số khoảng giữa hai điểm.",
            "medium": f"Đặt điểm đầu và điểm cuối rõ ràng; kiểm tra khoảng cách theo “{concept}” trước khi xác nhận.",
            "application": f"Thử đo lại từ hai đầu mút theo “{concept}”; kết quả phải giữ nguyên dù con kiểm tra lại lần nữa.",
        },
    }
    by_type = hints.get(question_type, hints["numeric_input"])
    return by_type.get(difficulty, by_type["medium"])


COMPONENT_SKILLS = {
    "ADD_COMPONENTS_RECOGNIZE",
    "SUB_COMPONENTS_RECOGNIZE",
    "MULTIPLICATION_COMPONENTS",
    "DIVISION_COMPONENTS",
}

COMPONENT_TERM_REASONS = {
    "số hạng": "Số hạng là số được đem cộng trong một phép cộng.",
    "tổng": "Tổng là kết quả của phép cộng.",
    "số bị trừ": "Số bị trừ là số đứng trước dấu trừ, tức lượng ban đầu bị bớt đi.",
    "số trừ": "Số trừ là lượng được bớt khỏi số bị trừ.",
    "hiệu": "Hiệu là kết quả của phép trừ.",
    "thừa số": "Thừa số là số được đem nhân trong một phép nhân.",
    "tích": "Tích là kết quả của phép nhân.",
    "số bị chia": "Số bị chia là lượng được đem chia.",
    "số chia": "Số chia là số dùng để chia số bị chia.",
    "thương": "Thương là kết quả của phép chia.",
}


def structured_choice_reason(skill: str, prompt: str, choice_text: str) -> str | None:
    """Return a deterministic, choice-specific diagnosis for structures we can prove from the text."""
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
        if "kí hiệu nào phù hợp" in prompt_cf:
            reasons = {
                "ab": "AB gồm hai chữ cái nên thường dùng để chỉ đối tượng gắn với hai điểm A và B, không phải tên của một điểm duy nhất.",
                "1": "Tên điểm trong hình học thường dùng một chữ cái in hoa; số 1 không phải kí hiệu tên điểm trong quy ước này.",
                "5 cm": "5 cm là một số đo độ dài, không phải kí hiệu dùng để đặt tên một vị trí hình học.",
            }
            if label in reasons: return reasons[label]
        if "phát biểu nào đúng về một điểm" in prompt_cf:
            reasons = {
                "điểm có một độ dài xác định": "Điểm chỉ biểu diễn một vị trí nên không có độ dài riêng để đo.",
                "điểm có hai đầu mút": "Hai đầu mút là đặc điểm của đoạn thẳng, không phải của một điểm.",
                "điểm có thể kéo dài về hai phía": "Khả năng kéo dài về hai phía là đặc điểm của đường thẳng; một điểm chỉ là một vị trí.",
            }
            if label in reasons: return reasons[label]
        if "ba vị trí được đánh dấu a, b, c" in prompt_cf:
            nums = [int(x) for x in re.findall(r"\d+", choice_text)]
            if nums and nums[0] != 3:
                return f"Hình đã nêu ba vị trí A, B, C nên có đúng 3 điểm được đặt tên; chọn {nums[0]} là đếm thiếu hoặc thừa."

    if skill == "LINE_SEGMENT_RECOGNIZE":
        label = choice_text.strip().casefold()
        prompt_cf = prompt.casefold()
        if "bao nhiêu đầu mút" in prompt_cf:
            nums = [int(x) for x in re.findall(r"\d+", choice_text)]
            if nums and nums[0] != 2:
                return f"Một đoạn thẳng luôn có đúng 2 đầu mút; lựa chọn {nums[0]} không đúng số đầu mút của đoạn AB."
        if "mô tả nào đúng về đoạn thẳng" in prompt_cf:
            reasons = {
                "phần thẳng kéo dài mãi về hai phía": "Nét thẳng kéo dài mãi về hai phía là đường thẳng; đoạn thẳng bị giới hạn bởi hai đầu mút.",
                "nét uốn cong nối hai vị trí": "Đoạn thẳng phải là phần thẳng giữa hai đầu mút, không phải một nét uốn cong.",
                "chỉ một vị trí không có độ dài": "Một vị trí đơn lẻ là điểm; đoạn thẳng phải nối hai đầu mút và có độ dài.",
            }
            if label in reasons: return reasons[label]
        if "nối thẳng điểm m với điểm n" in prompt_cf:
            reasons = {
                "đường thẳng mn": "Nối hai điểm M và N bằng phần thẳng chỉ giữa hai điểm tạo đoạn thẳng MN; đường thẳng còn kéo dài qua hai phía.",
                "đường cong mn": "Đề yêu cầu nối thẳng M với N nên kết quả không thể là đường cong.",
                "chỉ điểm m": "Hình mới phải liên hệ cả M và N; chỉ giữ điểm M thì chưa thực hiện việc nối hai điểm.",
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
            "một đoạn thẳng duy nhất": "Đường gấp khúc phải gồm nhiều đoạn thẳng nối tiếp, không chỉ một đoạn duy nhất.",
            "một nét cong liên tục": "Đường gấp khúc được tạo bởi các đoạn thẳng, không phải một nét cong liên tục.",
            "đoạn thẳng ad": "Ba đoạn AB, BC, CD có các chỗ đổi hướng tại B và C nên không thể gộp thành một đoạn thẳng AD.",
            "đường thẳng ad": "Chuỗi AB, BC, CD gồm nhiều đoạn nối tiếp và có thể đổi hướng, không phải một đường thẳng duy nhất AD.",
            "đường cong abcd": "Các phần AB, BC, CD đều là đoạn thẳng nên toàn hình là đường gấp khúc, không phải đường cong.",
        }
        if label in reasons: return reasons[label]
        if "có 4 đoạn thẳng" in prompt_cf:
            nums = [int(x) for x in re.findall(r"\d+", choice_text)]
            if nums and nums[0] != 5:
                return f"Chuỗi mở có 4 đoạn thẳng cần 5 điểm theo thứ tự để tạo 4 khoảng nối; chọn {nums[0]} là thiếu điểm."

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


def distractor_rationale(choice_text: str, explanation: str, skill: str, prompt: str) -> str:
    """Explain a wrong choice; prefer a provable choice-specific diagnosis when available."""
    reason = " ".join(explanation.strip().split())
    structured_reason = structured_choice_reason(skill, prompt, choice_text)
    if structured_reason:
        return f"“{choice_text}” chưa đúng. {structured_reason} {reason}"
    term_reason = COMPONENT_TERM_REASONS.get(choice_text.strip().casefold()) if skill in COMPONENT_SKILLS else None
    if term_reason:
        return f"“{choice_text}” chưa đúng. {term_reason} {reason}"
    return f"“{choice_text}” chưa đúng. {reason}"


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
            "allowed_operators": ["+", "-", "(", ")"],
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
    "ADD_WITHIN_1000_ONE_CARRY_MAX": ("m2_tp05_written_add_sub", "Cộng đến 1000 có nhớ một lượt", "Nếu tổng ở một cột từ 10 trở lên, viết phần đơn vị ở cột đó và nhớ 1 sang cột bên trái. Trong các bài này, chỉ có nhiều nhất một cột cần nhớ.", "Cộng có nhớ", "Một lượt nhớ xảy ra khi tổng của một hàng tạo thêm 1 đơn vị ở hàng kế tiếp.", ["ADD_WITHIN_1000_NO_CARRY"]),
    "SUB_WITHIN_1000_NO_BORROW": ("m2_tp05_written_add_sub", "Trừ đến 1000 không mượn", "Đặt các hàng thẳng cột rồi trừ từ đơn vị sang chục, trăm. Với dạng không mượn, chữ số trên ở mỗi cột không nhỏ hơn chữ số dưới.", "Trừ không mượn", "Trừ từng hàng trực tiếp khi mỗi chữ số của số bị trừ đủ lớn ở cột tương ứng.", ["PLACE_VALUE_HUNDREDS_TENS_ONES", "SUB_COMPONENTS_RECOGNIZE"]),
    "SUB_WITHIN_1000_ONE_BORROW_MAX": ("m2_tp05_written_add_sub", "Trừ đến 1000 có mượn một lượt", "Khi chữ số trên nhỏ hơn chữ số dưới ở một cột, mượn 1 từ hàng bên trái, tương đương thêm 10 vào hàng đang trừ. Trong các bài này, chỉ có nhiều nhất một cột cần mượn.", "Trừ có mượn", "Một lượt mượn đổi 1 đơn vị của hàng lớn hơn thành 10 đơn vị ở hàng nhỏ hơn.", ["SUB_WITHIN_1000_NO_BORROW"]),
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

    "OPERATION_MEANING_FROM_VISUAL": ("m2_tp09_problem_meaning", "Nhìn tình huống để chọn phép tính", "Không chỉ dựa vào từ khóa. Hãy xác định số lượng đang được gộp, bớt đi, tạo nhóm bằng nhau hay chia đều để chọn cộng, trừ, nhân hoặc chia.", "Quan hệ của phép tính", "Cộng gộp/thêm, trừ bớt/so sánh chênh lệch, nhân tạo nhóm bằng nhau, chia đều hoặc đếm số nhóm.", ["DIVISION_MEANING"]),
    "WP_ONE_STEP_ADD_MORE": ("m2_tp10_problem_contexts", "Bài toán thêm vào", "Khi một lượng ban đầu được thêm một lượng mới và hỏi tất cả có bao nhiêu, dùng phép cộng một bước.", "Thêm vào", "Khi có thêm một lượng mới, tổng mới bằng số ban đầu cộng số được thêm; lượng cần tìm tăng lên.", ["ADD_WITHIN_1000_NO_CARRY", "OPERATION_MEANING_FROM_VISUAL"]),
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
    "THREE_COLLINEAR_POINTS": ("m2_tp11_lines_shapes", "Ba điểm thẳng hàng", "Ba điểm thẳng hàng khi cả ba cùng nằm trên một đường thẳng. Nếu một điểm lệch khỏi đường qua hai điểm còn lại thì ba điểm không thẳng hàng.", "Thẳng hàng", "Các điểm thẳng hàng cùng nằm trên một đường thẳng duy nhất.", ["STRAIGHT_LINE_RECOGNIZE"]),
    "QUADRILATERAL_RECOGNIZE": ("m2_tp11_lines_shapes", "Nhận biết hình tứ giác", "Hình tứ giác là hình phẳng kín có bốn cạnh và bốn đỉnh. Hình vuông và hình chữ nhật đều là các ví dụ của tứ giác.", "Tứ giác", "Một hình kín có đúng bốn cạnh là hình tứ giác.", ["LINE_SEGMENT_RECOGNIZE"]),
    "CYLINDER_RECOGNIZE": ("m2_tp12_solids_construct", "Nhận biết khối trụ", "Khối trụ có hai mặt đáy tròn bằng nhau và một mặt cong bao quanh. Lon nước là một vật thể gần dạng khối trụ.", "Khối trụ", "Khối có hai đáy tròn song song và một mặt cong xung quanh.", []),
    "SPHERE_RECOGNIZE": ("m2_tp12_solids_construct", "Nhận biết khối cầu", "Khối cầu tròn đều theo mọi hướng và không có cạnh hay đỉnh. Quả bóng là vật thể gần dạng khối cầu.", "Khối cầu", "Khối tròn không có cạnh, đỉnh hay mặt phẳng đáy.", []),
    "DRAW_SEGMENT_GIVEN_LENGTH": ("m2_tp12_solids_construct", "Tạo đoạn thẳng có độ dài cho trước", "Đặt một đầu đoạn thẳng tại vạch đầu, rồi chọn đầu còn lại sao cho khoảng cách trên thước đúng bằng độ dài yêu cầu.", "Vẽ đoạn thẳng theo độ dài", "Độ dài đoạn thẳng bằng hiệu vị trí hai đầu mút trên cùng một thước chia đều.", ["LINE_SEGMENT_RECOGNIZE"]),
    "FOLD_CUT_COMPOSE_SHAPES": ("m2_tp12_solids_construct", "Gấp, cắt và ghép hình", "Có thể tạo hình mới bằng cách gấp, cắt hoặc ghép các hình đơn giản. Khi ghép, các cạnh phù hợp được đặt sát nhau mà không làm thay đổi bản chất của từng mảnh.", "Ghép và tạo hình", "Tách một hình thành các phần hoặc ghép các phần để tạo hình mới giúp nhận ra quan hệ giữa các hình.", ["QUADRILATERAL_RECOGNIZE"]),

    "HEAVIER_LIGHTER": ("m2_tp13_mass_length_capacity", "Nặng hơn và nhẹ hơn", "Khi so sánh bằng cân, phía hạ thấp hơn thường chứa vật nặng hơn; phía nâng cao hơn chứa vật nhẹ hơn nếu cân hoạt động cân bằng đúng.", "So sánh khối lượng", "Nặng hơn nghĩa là có khối lượng lớn hơn; nhẹ hơn nghĩa là có khối lượng nhỏ hơn.", []),
    "MASS_KG_READ_WRITE": ("m2_tp13_mass_length_capacity", "Đọc và viết số đo kilôgam", "Kilôgam, kí hiệu kg, là đơn vị dùng để đo khối lượng. Số đo phải đi cùng đúng đơn vị để biết đang nói về khối lượng.", "Kilôgam", "kg là kí hiệu của kilôgam, một đơn vị đo khối lượng thông dụng.", ["HEAVIER_LIGHTER"]),
    "CAPACITY_LITER_READ_WRITE": ("m2_tp13_mass_length_capacity", "Đọc và viết số đo lít", "Lít, kí hiệu l, là đơn vị dùng để đo dung tích chất lỏng. Khi cộng hoặc trừ các số đo cùng đơn vị lít, giữ nguyên đơn vị l.", "Lít", "l là kí hiệu của lít, đơn vị đo dung tích.", []),
    "LENGTH_DM_M_KM_RECOGNIZE_RELATION": ("m2_tp13_mass_length_capacity", "Đề-xi-mét, mét và ki-lô-mét", "Các đơn vị độ dài có quan hệ: 1 m = 10 dm và 1 km = 1000 m. Chọn đơn vị phù hợp với kích thước quãng đường hoặc vật cần đo.", "Quan hệ đơn vị độ dài", "dm, m và km là các đơn vị độ dài với 1 m = 10 dm, 1 km = 1000 m.", []),
    "TIME_DAY_24_HOURS": ("m2_tp14_time_money", "Một ngày có 24 giờ", "Một ngày đầy đủ gồm 24 giờ. Có thể dùng mốc sáng, trưa, chiều, tối để liên hệ các thời điểm trong ngày.", "Ngày và giờ", "Một ngày đầy đủ gồm 24 giờ; đủ 24 giờ liên tiếp tương ứng đúng một ngày.", []),
    "TIME_HOUR_60_MINUTES": ("m2_tp14_time_money", "Một giờ có 60 phút", "Phút là đơn vị nhỏ hơn giờ. Khi đủ 60 phút thì được 1 giờ.", "Giờ và phút", "Một giờ gồm 60 phút; đủ 60 phút liên tiếp tương ứng đúng một giờ.", ["TIME_DAY_24_HOURS"]),
    "CALENDAR_DAYS_IN_MONTH_DATE": ("m2_tp14_time_money", "Ngày và tháng trên lịch", "Lịch cho biết thứ tự ngày trong tháng. Một số tháng có 30 ngày, một số có 31 ngày; tháng 2 có 28 hoặc 29 ngày tùy năm.", "Đọc lịch", "Dùng số ngày và vị trí ngày trên lịch để xác định ngày trước, ngày sau và số ngày trong tháng.", ["NUM_PREDECESSOR_SUCCESSOR"]),
    "MONEY_VND_NOTE_RECOGNITION": ("m2_tp14_time_money", "Nhận biết tiền Việt Nam", "Trên tờ tiền, giá trị được thể hiện bằng con số và đơn vị đồng. Khi nhận biết, đọc đúng giá trị in trên tờ tiền và phân biệt với các giá trị khác.", "Giá trị tờ tiền", "Giá trị tiền được đọc theo con số mệnh giá và đơn vị đồng in trên tờ; không đoán chỉ từ màu sắc.", ["NUM_COUNT_READ_WRITE_0_1000"]),
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

WORKED_EXAMPLES = {'NUM_COUNT_READ_WRITE_0_1000': {'prompt_vi': 'Số gồm 3 trăm, 4 chục và 2 đơn vị là số nào?',
                                 'answer': '342',
                                 'solution_steps_vi': ['3 trăm = 300, 4 chục = 40, 2 đơn vị = 2.', 'Ghép các hàng: 300 + 40 + 2 = 342.']},
 'NUM_FULL_HUNDREDS_RECOGNIZE': {'prompt_vi': 'Số 300 có phải là số tròn trăm không?',
                                 'answer': 'Có',
                                 'solution_steps_vi': ['300 gồm 3 trăm, 0 chục và 0 đơn vị.', 'Vì không có chục và đơn vị nên 300 là số tròn trăm.']},
 'NUM_PREDECESSOR_SUCCESSOR': {'prompt_vi': 'Số liền sau của 249 là số nào?',
                               'answer': '250',
                               'solution_steps_vi': ['Số liền sau lớn hơn số đã cho đúng 1 đơn vị.', '249 thêm 1 được 250.']},
 'PLACE_VALUE_HUNDREDS_TENS_ONES': {'prompt_vi': 'Trong số 731, chữ số 3 có giá trị là bao nhiêu?',
                                    'answer': '30',
                                    'solution_steps_vi': ['Chữ số 3 đứng ở hàng chục.', '3 chục có giá trị là 30.']},
 'NUM_EXPANDED_FORM_HTO': {'prompt_vi': 'Viết số 641 dưới dạng tổng của trăm, chục và đơn vị.',
                           'answer': '600 + 40 + 1',
                           'solution_steps_vi': ['641 có 6 trăm, 4 chục và 1 đơn vị.', 'Vậy 641 = 600 + 40 + 1.']},
 'NUMBER_RAY_FILL': {'prompt_vi': 'Trên tia số có các vạch 100, 200, 300, __. Điền số tiếp theo.',
                     'answer': '400',
                     'solution_steps_vi': ['Mỗi vạch tăng đều 100.', 'Sau 300 thêm 100 là 400.']},
 'NUM_COMPARE_0_1000': {'prompt_vi': 'So sánh 712 và 721.',
                        'answer': '712 < 721',
                        'solution_steps_vi': ['Hai số cùng có 7 trăm.', 'So hàng chục: 1 chục < 2 chục nên 712 < 721.']},
 'NUM_MIN_MAX_UP_TO_4': {'prompt_vi': 'Trong các số 305, 350 và 503, số nào bé nhất?',
                         'answer': '305',
                         'solution_steps_vi': ['305 và 350 có 3 trăm, còn 503 có 5 trăm.', 'Giữa 305 và 350, 0 chục < 5 chục nên 305 bé nhất.']},
 'NUM_SORT_UP_TO_4': {'prompt_vi': 'Sắp xếp 210, 201, 120 theo thứ tự từ bé đến lớn.',
                      'answer': '120, 201, 210',
                      'solution_steps_vi': ['120 có 1 trăm nên bé nhất.', 'Trong 201 và 210, 0 chục < 1 chục nên 201 đứng trước 210. Vậy thứ tự là 120, 201, 210.']},
 'ESTIMATE_OBJECTS_BY_TENS': {'prompt_vi': 'Có khoảng 9 nhóm, mỗi nhóm gần 10 nút áo. Ước lượng có khoảng bao nhiêu nút áo?',
                              'answer': 'khoảng 90',
                              'solution_steps_vi': ['Mỗi nhóm gần một chục.', '9 chục là khoảng 90 nút áo.']},
 'ADD_COMPONENTS_RECOGNIZE': {'prompt_vi': 'Trong 14 + 25 = 39, số 39 được gọi là gì?',
                              'answer': 'tổng',
                              'solution_steps_vi': ['14 và 25 là hai số được cộng nên là các số hạng.', 'Kết quả 39 được gọi là tổng.']},
 'SUB_COMPONENTS_RECOGNIZE': {'prompt_vi': 'Trong 63 - 21 = 42, số 21 được gọi là gì?',
                              'answer': 'số trừ',
                              'solution_steps_vi': ['63 là số bị trừ.', '21 là số được lấy bớt nên gọi là số trừ; 42 là hiệu.']},
 'ADD_WITHIN_1000_NO_CARRY': {'prompt_vi': 'Tính 321 + 456.',
                              'answer': '777',
                              'solution_steps_vi': ['Cộng từng hàng: 1 + 6 = 7, 2 + 5 = 7.', 'Hàng trăm: 3 + 4 = 7, nên kết quả là 777.']},
 'ADD_WITHIN_1000_ONE_CARRY_MAX': {'prompt_vi': 'Tính 254 + 128.',
                                   'answer': '382',
                                   'solution_steps_vi': ['Hàng đơn vị: 4 + 8 = 12, viết 2 và nhớ 1 chục.',
                                                         'Hàng chục: 5 + 2 + 1 = 8; hàng trăm: 2 + 1 = 3, được 382.']},
 'SUB_WITHIN_1000_NO_BORROW': {'prompt_vi': 'Tính 865 - 432.',
                               'answer': '433',
                               'solution_steps_vi': ['Trừ từng hàng: 5 - 2 = 3, 6 - 3 = 3.', 'Hàng trăm: 8 - 4 = 4, được 433.']},
 'SUB_WITHIN_1000_ONE_BORROW_MAX': {'prompt_vi': 'Tính 463 - 127.',
                                    'answer': '336',
                                    'solution_steps_vi': ['Hàng đơn vị: 3 không trừ được 7, mượn 1 chục để có 13 - 7 = 6.',
                                                          'Còn 5 chục: 5 - 2 = 3; hàng trăm: 4 - 1 = 3, được 336.']},
 'ADD_SUB_TWO_OPERATORS_LEFT_TO_RIGHT': {'prompt_vi': 'Bắt đầu từ 70, cộng 20 rồi bớt 30. Kết quả cuối cùng là bao nhiêu?',
                                         'answer': '60',
                                         'solution_steps_vi': ['Làm theo đúng thứ tự đã nêu: 70 + 20 = 90.', 'Sau đó 90 - 30 = 60.']},
 'MENTAL_ADD_SUB_WITHIN_20': {'prompt_vi': 'Tính nhẩm 9 + 6.',
                              'answer': '15',
                              'solution_steps_vi': ['Tách 6 thành 1 và 5 để làm tròn 10.', '9 + 1 = 10, rồi 10 + 5 = 15.']},
 'MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000': {'prompt_vi': 'Tính nhẩm 600 + 300.',
                                             'answer': '900',
                                             'solution_steps_vi': ['6 trăm + 3 trăm = 9 trăm.', '9 trăm là 900.']},
 'MULTIPLICATION_MEANING': {'prompt_vi': 'Có 4 nhóm, mỗi nhóm 2 chấm. Viết phép cộng lặp lại và phép nhân tương ứng.',
                            'answer': '2 + 2 + 2 + 2 = 8; 4 × 2 = 8',
                            'solution_steps_vi': ['Có 4 nhóm bằng nhau nên cộng số 2 bốn lần: 2 + 2 + 2 + 2 = 8.', 'Vậy phép cộng lặp lại và phép nhân tương ứng là 2 + 2 + 2 + 2 = 8; 4 × 2 = 8.']},
 'DIVISION_MEANING': {'prompt_vi': 'Có 12 chiếc kẹo chia đều cho 2 bạn. Mỗi bạn được bao nhiêu chiếc?',
                      'answer': '6',
                      'solution_steps_vi': ['Chia 12 thành 2 phần bằng nhau.', '12 : 2 = 6 nên mỗi bạn được 6 chiếc.']},
 'MULTIPLICATION_COMPONENTS': {'prompt_vi': 'Trong 5 × 3 = 15, các thừa số và tích là những số nào?',
                               'answer': '5 và 3 là thừa số; 15 là tích',
                               'solution_steps_vi': ['Hai số được nhân là 5 và 3 nên chúng là các thừa số.', 'Vậy 5 và 3 là thừa số; 15 là tích.']},
 'DIVISION_COMPONENTS': {'prompt_vi': 'Trong 20 : 2 = 10, hãy gọi tên ba thành phần.',
                         'answer': '20 là số bị chia; 2 là số chia; 10 là thương',
                         'solution_steps_vi': ['20 là lượng được đem chia nên là số bị chia; 2 là số dùng để chia.', 'Vậy 20 là số bị chia; 2 là số chia; 10 là thương.']},
 'TIMES_TABLE_2': {'prompt_vi': 'Tính 2 × 4.', 'answer': '8', 'solution_steps_vi': ['Bốn nhóm 2 là 2 + 2 + 2 + 2.', 'Tổng bằng 8 nên 2 × 4 = 8.']},
 'TIMES_TABLE_5': {'prompt_vi': 'Tính 5 × 3.', 'answer': '15', 'solution_steps_vi': ['Ba nhóm 5 là 5 + 5 + 5.', 'Tổng bằng 15 nên 5 × 3 = 15.']},
 'DIVIDE_TABLE_2': {'prompt_vi': 'Tính 12 : 2.', 'answer': '6', 'solution_steps_vi': ['Tìm số mà 2 nhân với số đó bằng 12.', 'Vì 2 × 6 = 12 nên 12 : 2 = 6.']},
 'DIVIDE_TABLE_5': {'prompt_vi': 'Tính 30 : 5.', 'answer': '6', 'solution_steps_vi': ['Tìm số mà 5 nhân với số đó bằng 30.', 'Vì 5 × 6 = 30 nên 30 : 5 = 6.']},
 'OPERATION_MEANING_FROM_VISUAL': {'prompt_vi': 'Có 4 nhóm bằng nhau, mỗi nhóm 2 hình tròn. Phép tính nào mô tả tình huống?',
                                   'answer': '4 × 2',
                                   'solution_steps_vi': ['Các nhóm có số lượng bằng nhau nên dùng phép nhân.', 'Có 4 nhóm, mỗi nhóm 2 nên viết 4 × 2.']},
 'WP_ONE_STEP_ADD_MORE': {'prompt_vi': 'Minh có 32 thẻ, được cho thêm 15 thẻ. Minh có tất cả bao nhiêu thẻ?',
                          'answer': '47',
                          'solution_steps_vi': ['Tình huống thêm vào nên dùng phép cộng.', '32 + 15 = 47 thẻ.']},
 'WP_ONE_STEP_SUB_LESS': {'prompt_vi': 'Một giá sách có 63 quyển. Cô lấy xuống 21 quyển. Trên giá còn bao nhiêu quyển?',
                          'answer': '42',
                          'solution_steps_vi': ['Số sách trên giá giảm đi nên dùng phép trừ.', '63 - 21 = 42 quyển.']},
 'WP_ONE_STEP_MORE_THAN': {'prompt_vi': 'Hà có 40 nhãn dán. Nam có nhiều hơn Hà 6 nhãn. Nam có bao nhiêu nhãn?',
                           'answer': '46',
                           'solution_steps_vi': ['Nam nhiều hơn Hà 6 nên lấy số của Hà cộng 6.', '40 + 6 = 46 nhãn dán.']},
 'WP_ONE_STEP_LESS_THAN': {'prompt_vi': 'Hộp A có 52 viên bi. Hộp B ít hơn hộp A 7 viên. Hộp B có bao nhiêu viên?',
                           'answer': '45',
                           'solution_steps_vi': ['Hộp B ít hơn 7 nên lấy số của hộp A trừ 7.', '52 - 7 = 45 viên bi.']},
 'WP_ONE_STEP_MULTIPLICATION_CONTEXT': {'prompt_vi': 'Có 3 hộp, mỗi hộp 2 chiếc bút. Có tất cả bao nhiêu chiếc bút?',
                                        'answer': '6',
                                        'solution_steps_vi': ['Có 3 nhóm bằng nhau, mỗi nhóm 2.', '3 × 2 = 6 chiếc bút.']},
 'WP_ONE_STEP_DIVISION_CONTEXT': {'prompt_vi': 'Có 10 chiếc bút, xếp mỗi hộp 5 chiếc. Cần bao nhiêu hộp?',
                                  'answer': '2',
                                  'solution_steps_vi': ['Ta cần tìm số nhóm 5 có trong 10.', '10 : 5 = 2 nên cần 2 hộp.']},
 'WP_SELECT_OPERATION_ONE_STEP': {'prompt_vi': 'Có 18 chiếc bánh chia đều cho 2 đĩa. Muốn tìm số bánh mỗi đĩa, chọn phép tính nào?',
                                  'answer': '18 : 2',
                                  'solution_steps_vi': ['Đang chia một tổng thành 2 phần bằng nhau nên dùng phép chia.', 'Phép tính phù hợp là 18 : 2.']},
 'POINT_RECOGNIZE': {'prompt_vi': 'Trên hình có hai vị trí được đánh dấu P và Q. Có bao nhiêu điểm được đặt tên?',
                     'answer': '2',
                     'solution_steps_vi': ['Mỗi chữ P và Q đặt tên cho một vị trí.', 'Vậy có 2 điểm được đặt tên.']},
 'LINE_SEGMENT_RECOGNIZE': {'prompt_vi': 'Nối thẳng điểm C với điểm D bằng phần thẳng giới hạn bởi hai điểm. Hình đó gọi là gì?',
                            'answer': 'đoạn thẳng CD',
                            'solution_steps_vi': ['C và D là hai đầu mút xác định.', 'Phần thẳng nối hai đầu mút là đoạn thẳng CD.']},
 'CURVE_RECOGNIZE': {'prompt_vi': 'Một nét vẽ uốn thành hình vòng cung thuộc loại đường nào?',
                     'answer': 'đường cong',
                     'solution_steps_vi': ['Nét vẽ đổi hướng và không giữ thẳng.', 'Vì vậy đó là một đường cong.']},
 'STRAIGHT_LINE_RECOGNIZE': {'prompt_vi': 'Một nét không cong và có thể kéo dài mãi về hai phía gọi là gì?',
                             'answer': 'đường thẳng',
                             'solution_steps_vi': ['Nét không uốn cong nên có hướng thẳng.', 'Có thể kéo dài về hai phía nên đó là đường thẳng.']},
 'POLYLINE_RECOGNIZE': {'prompt_vi': 'Hai đoạn AB và BC nối tiếp nhau tại B tạo thành loại đường nào?',
                        'answer': 'đường gấp khúc ABC',
                        'solution_steps_vi': ['AB và BC là hai đoạn thẳng nối tiếp tại điểm B.', 'Chuỗi các đoạn nối tiếp tạo thành đường gấp khúc ABC.']},
 'THREE_COLLINEAR_POINTS': {'prompt_vi': 'Ba điểm P, Q, R đều nằm trên cùng mép thẳng của một chiếc thước. Ba điểm này thế nào?',
                            'answer': 'thẳng hàng',
                            'solution_steps_vi': ['Cả ba điểm cùng nằm trên một đường thẳng.', 'Vì vậy P, Q, R là ba điểm thẳng hàng.']},
 'QUADRILATERAL_RECOGNIZE': {'prompt_vi': 'Một hình kín có đúng 4 cạnh và 4 đỉnh được gọi chung là gì?',
                             'answer': 'hình tứ giác',
                             'solution_steps_vi': ['Đếm được đúng 4 cạnh của hình kín.', 'Hình kín có 4 cạnh là hình tứ giác.']},
 'CYLINDER_RECOGNIZE': {'prompt_vi': 'Một chiếc hộp có hai đáy tròn bằng nhau và mặt cong bao quanh gần dạng khối gì?',
                        'answer': 'khối trụ',
                        'solution_steps_vi': ['Hai mặt đáy đều là hình tròn và song song.', 'Đặc điểm đó phù hợp với khối trụ.']},
 'SPHERE_RECOGNIZE': {'prompt_vi': 'Một viên bi tròn đều, không có cạnh hay đỉnh, gần dạng khối gì?',
                      'answer': 'khối cầu',
                      'solution_steps_vi': ['Vật tròn đều theo mọi hướng và không có cạnh, đỉnh.', 'Đó là đặc điểm của khối cầu.']},
 'DRAW_SEGMENT_GIVEN_LENGTH': {'prompt_vi': 'Trên thước, một đầu đoạn thẳng ở vạch 4 cm. Muốn đoạn dài 3 cm về bên phải, đầu kia ở vạch nào?',
                               'answer': '7',
                               'solution_steps_vi': ['Đầu kia ở bên phải nên cộng thêm độ dài 3 cm.', '4 + 3 = 7, nên chọn vạch 7 cm.']},
 'FOLD_CUT_COMPOSE_SHAPES': {'prompt_vi': 'Ghép hai hình vuông nhỏ bằng nhau sát theo một cạnh, không chồng lên nhau. Có thể tạo thành hình gì?',
                             'answer': 'một hình chữ nhật',
                             'solution_steps_vi': ['Đặt hai cạnh bằng nhau sát nhau để hai mảnh liền thành một hình kín.',
                                                   'Hai hình vuông ghép cạnh theo cách này tạo thành một hình chữ nhật.']},
 'HEAVIER_LIGHTER': {'prompt_vi': 'Trên cân hai đĩa, đĩa bên phải hạ thấp hơn. Vật bên phải thường thế nào so với vật bên trái?',
                     'answer': 'nặng hơn',
                     'solution_steps_vi': ['Phía nặng hơn làm đĩa cân hạ thấp hơn.', 'Đĩa bên phải thấp hơn nên vật bên phải nặng hơn.']},
 'MASS_KG_READ_WRITE': {'prompt_vi': 'Một thùng hàng ghi 6 kg. Số đo khối lượng được đọc thế nào?',
                        'answer': '6 kg',
                        'solution_steps_vi': ['Đọc số 6 trước.', 'Giữ đơn vị khối lượng kg, nên đọc là 6 kg.']},
 'CAPACITY_LITER_READ_WRITE': {'prompt_vi': 'Một can nước ghi 3 l. Số đo dung tích được đọc thế nào?',
                               'answer': '3 lít',
                               'solution_steps_vi': ['Đọc số 3 trước.', 'Kí hiệu l là lít, nên đọc là 3 lít.']},
 'LENGTH_DM_M_KM_RECOGNIZE_RELATION': {'prompt_vi': '2 m bằng bao nhiêu dm?',
                                       'answer': '20 dm',
                                       'solution_steps_vi': ['1 m = 10 dm.', 'Vậy 2 m gồm hai nhóm 10 dm, bằng 20 dm.']},
 'TIME_DAY_24_HOURS': {'prompt_vi': 'Khoảng thời gian từ 0 giờ đến đủ 24 giờ liên tiếp bằng bao nhiêu ngày đầy đủ?',
                       'answer': '1 ngày',
                       'solution_steps_vi': ['Một ngày đầy đủ có 24 giờ.', 'Vì khoảng thời gian đủ 24 giờ nên bằng 1 ngày.']},
 'TIME_HOUR_60_MINUTES': {'prompt_vi': '60 phút bằng bao nhiêu giờ?',
                          'answer': '1 giờ',
                          'solution_steps_vi': ['Một giờ gồm 60 phút.', 'Vì đã đủ 60 phút nên bằng 1 giờ.']},
 'CALENDAR_DAYS_IN_MONTH_DATE': {'prompt_vi': 'Ngày sau ngày 20 tháng 6 là ngày nào?',
                                 'answer': '21 tháng 6',
                                 'solution_steps_vi': ['Ngày kế tiếp tăng số ngày thêm 1.', 'Sau ngày 20 là ngày 21, vẫn trong tháng 6. Vậy đáp án là 21 tháng 6.']},
 'MONEY_VND_NOTE_RECOGNITION': {'prompt_vi': 'Khi so sánh hai hình tờ tiền, nên đọc thông tin nào trước để biết tờ nào có giá trị lớn hơn?',
                                'answer': 'con số mệnh giá và đơn vị đồng',
                                'solution_steps_vi': ['Không dựa riêng vào màu sắc hay kích thước.',
                                                      'Để so sánh đúng, cần đọc con số mệnh giá và đơn vị đồng.']},
 'MEASURE_WITH_RULER_CM': {'prompt_vi': 'Một đoạn thẳng bắt đầu ở vạch 1 cm và kết thúc ở vạch 6 cm. Độ dài là bao nhiêu?',
                           'answer': '5 cm',
                           'solution_steps_vi': ['Đoạn không bắt đầu từ 0 nên lấy vị trí cuối trừ vị trí đầu.', '6 - 1 = 5 cm.']},
 'MEASURE_WITH_COMMON_SCALE': {'prompt_vi': 'Một thang chia đều có các vạch 0, 5, 10, 15, __. Vạch tiếp theo là bao nhiêu?',
                               'answer': '20',
                               'solution_steps_vi': ['Mỗi vạch tăng 5 đơn vị.', 'Sau 15 thêm 5 là 20.']},
 'CLOCK_MINUTE_HAND_AT_3_OR_6': {'prompt_vi': 'Lúc 6 giờ 15 phút, kim phút phải chỉ vào số nào trên mặt đồng hồ?',
                                 'answer': 'số 3',
                                 'solution_steps_vi': ['15 phút tương ứng với kim phút ở số 3.', 'Vì vậy lúc 6 giờ 15 phút, kim phút chỉ số 3.']},
 'MEASUREMENT_CONVERT_CALCULATE_LEARNED_UNITS': {'prompt_vi': '4 kg + 1 kg bằng bao nhiêu kilôgam?',
                                                 'answer': '5 kg',
                                                 'solution_steps_vi': ['Hai số đo cùng đơn vị kg nên cộng phần số.', '4 + 1 = 5, giữ đơn vị kg. Vậy kết quả là 5 kg.']},
 'MEASUREMENT_ESTIMATE_BASIC': {'prompt_vi': 'Một quyển vở học sinh rộng khoảng bao nhiêu là hợp lý: vài xăng-ti-mét, khoảng 20 cm hay vài ki-lô-mét?',
                                'answer': 'khoảng 20 cm',
                                'solution_steps_vi': ['Quyển vở là đồ vật nhỏ cầm trên tay nên dùng cm.',
                                                      'Độ rộng khoảng vài chục cm là hợp lý, nên chọn khoảng 20 cm.']},
 'POLYLINE_LENGTH_SUM_SEGMENTS': {'prompt_vi': 'Một đường gấp khúc có ba đoạn dài 2 cm, 3 cm và 4 cm. Tổng độ dài là bao nhiêu?',
                                  'answer': '9 cm',
                                  'solution_steps_vi': ['Cộng độ dài tất cả các đoạn cùng đơn vị.', '2 + 3 + 4 = 9 cm.']},
 'MEASUREMENT_REAL_WORLD_ONE_STEP': {'prompt_vi': 'Một sợi dây dài 4 m, nối thêm đoạn 2 m. Dây dài tất cả bao nhiêu?',
                                     'answer': '6 m',
                                     'solution_steps_vi': ['Hai độ dài cùng đơn vị mét và được nối thêm nên dùng phép cộng.', '4 + 2 = 6 m.']},
 'DATA_COLLECT_CLASSIFY_COUNT': {'prompt_vi': 'Các thẻ có màu: xanh, đỏ, xanh, xanh, vàng. Có bao nhiêu thẻ xanh?',
                                 'answer': '3',
                                 'solution_steps_vi': ['Chọn đúng nhóm có màu xanh.', 'Đếm được 3 thẻ xanh.']},
 'PICTOGRAPH_READ_DESCRIBE': {'prompt_vi': 'Một hàng biểu đồ có 4 biểu tượng; chú giải 1 biểu tượng = 2 quyển sách. Hàng đó biểu diễn bao nhiêu quyển?',
                              'answer': '8',
                              'solution_steps_vi': ['Mỗi biểu tượng đại diện 2 quyển sách.', '4 nhóm 2 quyển cho tổng cộng 8 quyển.']},
 'PICTOGRAPH_SIMPLE_INFERENCE': {'prompt_vi': 'Biểu đồ có nhóm A 3 biểu tượng và nhóm B 5 biểu tượng; mỗi biểu tượng = 1 vật. B nhiều hơn A bao nhiêu vật?',
                                 'answer': '2',
                                 'solution_steps_vi': ['Đọc được A có 3 vật và B có 5 vật.', 'Lấy 5 - 3 = 2, nên B nhiều hơn A 2 vật.']},
 'EVENT_POSSIBLE': {'prompt_vi': 'Trong túi có bóng đỏ và bóng xanh. Lấy ngẫu nhiên một bóng, lấy được bóng xanh là sự kiện gì?',
                    'answer': 'có thể',
                    'solution_steps_vi': ['Trong túi có ít nhất một bóng xanh nên kết quả này có thể xuất hiện.',
                                          'Nhưng còn bóng màu khác nên nó không chắc chắn xảy ra.']},
 'EVENT_CERTAIN': {'prompt_vi': 'Một hộp chỉ chứa thẻ số 1 và 2. Rút một thẻ, số rút được là 1 hoặc 2. Sự kiện này thế nào?',
                   'answer': 'chắc chắn',
                   'solution_steps_vi': ['Mọi thẻ trong hộp đều mang số 1 hoặc 2.', 'Vì mọi kết quả đều phù hợp nên sự kiện chắc chắn xảy ra.']},
 'EVENT_IMPOSSIBLE': {'prompt_vi': 'Trong túi chỉ có bóng vàng. Lấy ngẫu nhiên một bóng màu tím là sự kiện gì?',
                      'answer': 'không thể',
                      'solution_steps_vi': ['Trong túi không có bóng tím.', 'Không có kết quả nào phù hợp nên sự kiện không thể xảy ra.']}}


# Explicit, deterministic question content. Three questions per skill: basic, medium, application.
Q = {
    "NUM_COUNT_READ_WRITE_0_1000": [
        nq("Số gồm 5 trăm, 0 chục và 7 đơn vị là số nào?", 507, "5 trăm là 500, 0 chục là 0 và 7 đơn vị là 7; 500 + 0 + 7 = 507."),
        mc("Cách đọc đúng của số 420 là gì?", "bốn trăm hai mươi", ["bốn trăm hai", "bốn mươi hai", "hai trăm bốn mươi"], "420 có 4 trăm, 2 chục và 0 đơn vị nên đọc là bốn trăm hai mươi."),
        nq("Bạn Mai viết 7 trăm, 3 chục và 4 đơn vị thành 704 vì bỏ quên hàng chục. Số đúng trên thẻ phải là bao nhiêu?", 734, "Phải giữ đủ cả ba hàng: 7 trăm là 700, 3 chục là 30 và 4 đơn vị là 4; 700 + 30 + 4 = 734."),
    ],
    "NUM_FULL_HUNDREDS_RECOGNIZE": [
        mc("Số nào là số tròn trăm?", "600", ["610", "606", "660"], "600 có hàng chục và hàng đơn vị đều bằng 0."),
        tf("700 là một số tròn trăm.", True, "700 có hàng chục và hàng đơn vị đều bằng 0 nên đây là số tròn trăm."),
        mc("Trong các số 700, 750, 705, 570, số nào gồm đúng 7 trăm đầy đủ?", "700", ["750", "705", "570"], "700 có đúng 7 trăm và không có thêm chục hay đơn vị."),
    ],
    "NUM_PREDECESSOR_SUCCESSOR": [
        nq("Số liền trước của 500 là số nào?", 499, "Số liền trước nhỏ hơn 500 đúng 1 nên là 499."),
        nq("Số liền sau của 999 là số nào?", 1000, "Số liền sau lớn hơn 999 đúng 1 nên là 1000."),
        nq("Bạn An viết số liền sau của 399 là 401. Số nào cần thay vào 401 để câu trả lời đúng?", 400, "Số liền sau hơn số đã cho đúng 1 đơn vị; 399 + 1 = 400 nên cần thay 401 bằng 400."),
    ],
    "PLACE_VALUE_HUNDREDS_TENS_ONES": [
        nq("Trong số 684, chữ số 6 có giá trị là bao nhiêu?", 600, "Chữ số 6 đứng ở hàng trăm nên có giá trị 600."),
        nq("Trong số 572, chữ số hàng chục là chữ số nào?", 7, "Số 572 có 5 trăm, 7 chục và 2 đơn vị."),
        nq("Bạn Bình viết 4 trăm, 0 chục và 9 đơn vị thành 490. Số đúng phải là bao nhiêu?", 409, "Hàng trăm là 4, hàng chục phải giữ 0 và hàng đơn vị là 9: 400 + 0 + 9 = 409, không phải 490."),
    ],
    "NUM_EXPANDED_FORM_HTO": [
        mc("Dạng khai triển đúng của 352 là gì?", "300 + 50 + 2", ["300 + 5 + 2", "30 + 50 + 2", "300 + 50 + 20"], "352 có 3 trăm, 5 chục và 2 đơn vị nên bằng 300 + 50 + 2."),
        nq("Số nào bằng 600 + 20 + 7?", 627, "Cộng giá trị các hàng: 600 + 20 + 7 = 627."),
        mc("Bạn An viết 908 = 900 + 80. Cách sửa nào đúng?", "908 = 900 + 8", ["908 = 90 + 8", "908 = 900 + 80 + 8", "908 = 900 + 0 + 80"], "908 có 9 trăm, 0 chục và 8 đơn vị; hàng chục bằng 0 nên dạng đúng là 900 + 8."),
    ],
    "NUMBER_RAY_FILL": [
        nq("Trên tia số có các vạch 200, 300, __, 500. Số còn thiếu là gì?", 400, "Mỗi vạch tăng 100 nên sau 300 là 400."),
        nq("Trên tia số: 650, 700, __, 800. Mỗi bước bằng nhau. Số còn thiếu là gì?", 750, "Từ 650 đến 700 tăng 50, nên vạch tiếp theo là 750."),
        nq("Trên tia số, mỗi bước tăng 100. Từ số 300 đi 3 bước sang phải thì đến số nào?", 600, "Từ 300 đi lần lượt đến 400, 500 rồi 600; ba bước sang phải đưa ta đến 600."),
    ],
    "NUM_COMPARE_0_1000": [
        mc("Điền dấu đúng: 608 __ 680", "<", [">", "=", "+"], "Cùng 6 trăm nhưng 0 chục nhỏ hơn 8 chục nên 608 < 680."),
        mc("Điền dấu đúng: 945 __ 925", ">", ["<", "=", "-"], "Cùng 9 trăm; 4 chục lớn hơn 2 chục nên 945 > 925."),
        mc("Bạn An có 399 thẻ, bạn Bình có 403 thẻ. So sánh 399 và 403.", "399 < 403", ["399 > 403", "399 = 403", "399 + 403"], "399 còn dưới 400, trong khi 403 lớn hơn 400 nên 399 < 403."),
    ],
    "NUM_MIN_MAX_UP_TO_4": [
        nq("Số bé nhất trong nhóm 407, 470, 704, 740 là số nào?", 407, "Cả bốn số đều khác nhau; so sánh hàng trăm cho thấy 407 và 470 nhỏ hơn các số 7 trăm, rồi 407 < 470."),
        nq("Số lớn nhất trong nhóm 125, 512, 251, 215 là số nào?", 512, "512 có 5 trăm, lớn hơn các số chỉ có 1 hoặc 2 trăm."),
        nq("Bạn An chọn 990 là số lớn nhất trong 999, 909, 990, 900. Số nào chứng minh lựa chọn đó chưa đúng?", 999, "Cả 999 và 990 đều có 9 trăm, nhưng 999 có 9 chục bằng 990 rồi 9 đơn vị lớn hơn 0 đơn vị, nên 999 lớn hơn 990."),
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
        mc("Bạn An nói trong 120 + 230 = 350 thì 230 và 350 là hai số hạng. Cặp nào sửa lời bạn An?", "120 và 230", ["230 và 350", "120 và 350", "350 và 350"], "Hai số được cộng với nhau là 120 và 230 nên đó là hai số hạng; 350 là tổng."),
    ],
    "SUB_COMPONENTS_RECOGNIZE": [
        mc("Trong 50 - 12 = 38, số 50 được gọi là gì?", "số bị trừ", ["số trừ", "hiệu", "tổng"], "50 là số đứng trước dấu trừ nên là số bị trừ."),
        mc("Trong 72 - 20 = 52, số 20 được gọi là gì?", "số trừ", ["số bị trừ", "hiệu", "số hạng"], "20 là lượng được bớt khỏi số bị trừ nên là số trừ."),
        mc("Bạn Bình gọi 90 là số trừ và 55 là hiệu trong 90 - 35 = 55. Cặp tên nào sửa đúng lời bạn Bình?", "số bị trừ và hiệu", ["số trừ và hiệu", "hiệu và số bị trừ", "số bị trừ và số trừ"], "Trong phép trừ, 90 đứng trước dấu trừ nên là số bị trừ; kết quả 55 là hiệu."),
    ],
    "ADD_WITHIN_1000_NO_CARRY": [
        nq("Tính 243 + 125.", 368, "Cộng từng hàng: 3 + 5 = 8, 4 + 2 = 6, 2 + 1 = 3; được 368."),
        nq("Tính 410 + 230.", 640, "0 + 0 = 0, 1 chục + 3 chục = 4 chục, 4 trăm + 2 trăm = 6 trăm; được 640."),
        nq("Thư viện có 324 sách truyện và 253 sách khoa học. Có tất cả bao nhiêu quyển?", 577, "324 + 253 = 577; từng cột đều không cần nhớ."),
    ],
    "ADD_WITHIN_1000_ONE_CARRY_MAX": [
        nq("Tính 246 + 137.", 383, "6 + 7 = 13, viết 3 nhớ 1; 4 + 3 + 1 = 8; 2 + 1 = 3, nên kết quả 383."),
        nq("Tính 358 + 124.", 482, "8 + 4 = 12, viết 2 nhớ 1; 5 + 2 + 1 = 8; 3 + 1 = 4, nên 482."),
        nq("Kho A có 465 hộp, nhập thêm 217 hộp. Có tất cả bao nhiêu hộp?", 682, "465 + 217 = 682; chỉ hàng đơn vị tạo một lượt nhớ."),
    ],
    "SUB_WITHIN_1000_NO_BORROW": [
        nq("Tính 786 - 234.", 552, "6 - 4 = 2, 8 - 3 = 5, 7 - 2 = 5; được 552."),
        nq("Tính 900 - 400.", 500, "9 trăm trừ 4 trăm còn 5 trăm, tức 500."),
        nq("Kho có 654 hộp, chuyển đi 321 hộp. Còn lại bao nhiêu hộp?", 333, "654 - 321 = 333 và không cột nào cần mượn."),
    ],
    "SUB_WITHIN_1000_ONE_BORROW_MAX": [
        nq("Tính 352 - 138.", 214, "Mượn 1 chục: 12 - 8 = 4; còn 4 chục, 4 - 3 = 1; 3 - 1 = 2, được 214."),
        nq("Tính 641 - 223.", 418, "Mượn 1 chục: 11 - 3 = 8; còn 3 chục, 3 - 2 = 1; 6 - 2 = 4, được 418."),
        nq("Một cửa hàng có 730 chai, bán 412 chai. Còn lại bao nhiêu chai?", 318, "730 - 412 = 318; mượn 1 chục ở hàng đơn vị rồi các hàng còn lại trừ trực tiếp."),
    ],
    "ADD_SUB_TWO_OPERATORS_LEFT_TO_RIGHT": [
        nq("Tính từ trái sang phải: 50 + 20 - 10.", 60, "50 + 20 = 70, rồi 70 - 10 = 60."),
        eq("Tính 100 - 30 + 5. Có thể nhập kết quả hoặc một biểu thức cộng, trừ tương đương.", "100 - 30 + 5", 75, "100 - 30 = 70, rồi 70 + 5 = 75."),
        nq("Một hộp có 200 thẻ, thêm 150 thẻ rồi lấy ra 100 thẻ. Còn bao nhiêu thẻ?", 250, "Từ trái sang phải theo tình huống: 200 + 150 = 350, 350 - 100 = 250."),
    ],
    "MENTAL_ADD_SUB_WITHIN_20": [
        nq("Tính nhẩm 8 + 7.", 15, "Tách 7 thành 2 và 5: 8 + 2 = 10, rồi 10 + 5 = 15.", numeric_max=20),
        nq("Tính nhẩm 17 - 9.", 8, "Có thể trừ 10 rồi thêm lại 1: 17 - 10 = 7, 7 + 1 = 8.", numeric_max=20),
        nq("Mai có 12 nhãn dán, được cho thêm 5 nhãn. Mai có bao nhiêu nhãn?", 17, "12 + 5 = 17, vẫn trong phạm vi 20.", numeric_max=20),
    ],
    "MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000": [
        nq("Tính nhẩm 30 + 40.", 70, "3 chục + 4 chục = 7 chục, tức 70."),
        nq("Tính nhẩm 500 - 200.", 300, "5 trăm - 2 trăm = 3 trăm, tức 300."),
        nq("Một kho có 300 hộp, nhận thêm 400 hộp. Tính nhẩm tổng số hộp.", 700, "3 trăm + 4 trăm = 7 trăm = 700."),
    ],

    "MULTIPLICATION_MEANING": [
        mc("Có 3 nhóm, mỗi nhóm 2 chấm. Phép cộng lặp lại nào đúng?", "2 + 2 + 2", ["2 + 2 + 2 + 2", "2 + 3", "3 + 2 + 2"], "Ba nhóm, mỗi nhóm 2 tương ứng cộng số 2 ba lần."),
        nq("5 nhóm, mỗi nhóm 2 vật có tất cả bao nhiêu vật?", 10, "2 + 2 + 2 + 2 + 2 = 10, cũng là 5 × 2."),
        nq("Có 4 túi, mỗi túi 5 viên bi. Nam tính 4 + 5 = 9. Tổng số viên bi đúng là bao nhiêu?", 20, "Có 4 nhóm bằng nhau, mỗi nhóm 5 viên nên phải nhân: 4 × 5 = 20; phép cộng 4 + 5 không biểu diễn đủ bốn nhóm."),
    ],
    "DIVISION_MEANING": [
        nq("Có 10 chiếc bánh chia đều cho 2 bạn. Mỗi bạn được bao nhiêu chiếc?", 5, "10 chia đều thành 2 phần bằng nhau thì mỗi phần có 5."),
        nq("15 bông hoa xếp thành các nhóm 5 bông. Có bao nhiêu nhóm?", 3, "15 : 5 = 3 nhóm."),
        nq("20 thẻ chia đều vào 5 hộp. Lan định lấy 20 - 5 và nói mỗi hộp có 15 thẻ. Mỗi hộp thực sự có bao nhiêu thẻ?", 4, "Chia đều 20 thẻ vào 5 hộp phải dùng 20 : 5 = 4; phép trừ 20 - 5 không mô tả chia đều."),
    ],
    "MULTIPLICATION_COMPONENTS": [
        mc("Trong 2 × 5 = 10, số 2 được gọi là gì?", "thừa số", ["tích", "thương", "số bị chia"], "2 là một số được nhân nên là thừa số."),
        mc("Trong 5 × 6 = 30, số 30 là gì?", "tích", ["thừa số", "hiệu", "số chia"], "Kết quả của phép nhân gọi là tích."),
        mc("Bạn Mai nói trong 2 × 8 = 16 thì 8 và 16 là hai thừa số. Cặp nào sửa lời bạn Mai?", "2 và 8", ["8 và 16", "2 và 16", "16 và 16"], "Trong phép nhân, hai số được nhân với nhau là thừa số; 2 và 8 là hai thừa số, còn 16 là tích."),
    ],
    "DIVISION_COMPONENTS": [
        mc("Trong 10 : 2 = 5, số 10 là gì?", "số bị chia", ["số chia", "thương", "tích"], "10 là lượng được đem chia nên là số bị chia."),
        mc("Trong 20 : 5 = 4, số 5 là gì?", "số chia", ["số bị chia", "thương", "số hạng"], "5 là số dùng để chia nên là số chia."),
        mc("Bạn Lan nói trong 18 : 2 = 9 thì 18 là số chia và 9 là thương. Cặp tên nào sửa đúng lời bạn Lan?", "số bị chia và thương", ["số chia và thương", "thương và số bị chia", "số bị chia và số chia"], "Trong phép chia, 18 là số bị chia, 2 là số chia và kết quả 9 là thương."),
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
        nq("18 chiếc bánh được xếp vào các đĩa, mỗi đĩa 2 chiếc. Cần bao nhiêu đĩa?", 9, "18 : 2 = 9."),
    ],
    "DIVIDE_TABLE_5": [
        nq("Tính 25 : 5.", 5, "Vì 5 × 5 = 25 nên 25 : 5 = 5."),
        nq("Tính 40 : 5.", 8, "Vì 5 × 8 = 40 nên 40 : 5 = 8."),
        nq("50 viên bi xếp vào các túi, mỗi túi 5 viên. Cần bao nhiêu túi?", 10, "50 : 5 = 10 túi."),
    ],

    "OPERATION_MEANING_FROM_VISUAL": [
        mc("Hai nhóm 4 chấm được gộp lại. Phép tính nào mô tả việc gộp?", "4 + 4", ["4 - 4", "4 : 2", "4 + 2"], "Gộp hai lượng lại là quan hệ cộng; hai nhóm 4 là 4 + 4."),
        mc("Có 10 chấm, gạch bỏ 3 chấm. Phép tính nào mô tả tình huống?", "10 - 3", ["10 + 3", "10 : 2", "3 - 10"], "Bỏ bớt một phần khỏi lượng ban đầu là phép trừ."),
        mc("Có 5 nhóm bằng nhau, mỗi nhóm 2 chấm. Minh chọn 5 + 2 để tìm tất cả. Phép tính nào sửa đúng lựa chọn của Minh?", "5 × 2", ["5 + 2", "5 - 2", "2 × 2"], "Năm nhóm bằng nhau, mỗi nhóm có 2 chấm nên phép nhân 5 × 2 mới biểu diễn đủ các nhóm."),
    ],
    "WP_ONE_STEP_ADD_MORE": [
        nq("Lan có 24 bút chì, được cho thêm 13 bút. Lan có tất cả bao nhiêu bút?", 37, "24 + 13 = 37 vì số bút được thêm vào lượng ban đầu."),
        nq("Một giỏ có 125 quả, đặt thêm 243 quả. Giỏ có bao nhiêu quả?", 368, "125 + 243 = 368."),
        nq("Buổi sáng thư viện nhận 310 sách, buổi chiều nhận thêm 260 sách. An định lấy 310 - 260 để tìm tổng. Tổng số sách đúng là bao nhiêu?", 570, "Từ “nhận thêm” cho biết phải cộng: 310 + 260 = 570; phép trừ của An không đúng với tình huống."),
    ],
    "WP_ONE_STEP_SUB_LESS": [
        nq("Có 45 quả bóng, cho đi 12 quả. Còn lại bao nhiêu quả?", 33, "45 - 12 = 33 vì 12 quả bị bớt khỏi số ban đầu."),
        nq("Kho có 786 hộp, chuyển đi 234 hộp. Còn bao nhiêu hộp?", 552, "786 - 234 = 552."),
        nq("Một lớp có 40 tờ giấy màu, đã dùng 18 tờ. Mai định cộng 40 + 18 để tìm số còn lại. Số tờ còn lại đúng là bao nhiêu?", 22, "Đã dùng bớt 18 tờ nên phải trừ: 40 - 18 = 22; cộng 40 + 18 không trả lời số còn lại."),
    ],
    "WP_ONE_STEP_MORE_THAN": [
        nq("An có 25 viên bi. Bình có nhiều hơn An 7 viên. Bình có bao nhiêu viên?", 32, "Bình nhiều hơn An 7 viên nên 25 + 7 = 32."),
        nq("Cây A cao 120 cm. Cây B cao hơn cây A 30 cm. Cây B cao bao nhiêu cm?", 150, "120 + 30 = 150 cm.", unit="cm"),
        nq("Giỏ đỏ có 240 quả. Giỏ xanh nhiều hơn giỏ đỏ 120 quả. Nam định tính 240 - 120. Giỏ xanh thực sự có bao nhiêu quả?", 360, "Giỏ xanh nhiều hơn nên phải cộng lượng mốc với phần hơn: 240 + 120 = 360; phép trừ sẽ đi sai chiều so sánh."),
    ],
    "WP_ONE_STEP_LESS_THAN": [
        nq("Mai có 30 nhãn dán. Linh có ít hơn Mai 8 nhãn. Linh có bao nhiêu nhãn?", 22, "Linh ít hơn Mai 8 nên 30 - 8 = 22."),
        nq("Dây A dài 90 cm. Dây B ngắn hơn dây A 20 cm. Dây B dài bao nhiêu cm?", 70, "90 - 20 = 70 cm.", unit="cm"),
        nq("Kho lớn có 650 hộp. Kho nhỏ ít hơn kho lớn 230 hộp. Lan định tính 650 + 230. Kho nhỏ thực sự có bao nhiêu hộp?", 420, "Kho nhỏ ít hơn nên phải lấy lượng mốc trừ phần chênh lệch: 650 - 230 = 420; phép cộng sẽ làm số lượng lớn hơn."),
    ],
    "WP_ONE_STEP_MULTIPLICATION_CONTEXT": [
        nq("Có 6 bàn, mỗi bàn 2 bạn. Có tất cả bao nhiêu bạn?", 12, "6 nhóm 2 bạn nên 6 × 2 = 12."),
        nq("Có 4 hộp, mỗi hộp 5 bút. Có bao nhiêu bút?", 20, "4 × 5 = 20."),
        nq("Một tuần học có 5 ngày, mỗi ngày làm 2 bài luyện. Mai tính 5 + 2 = 7. Cả tuần thực sự làm bao nhiêu bài?", 10, "Mỗi ngày là một nhóm 2 bài và có 5 ngày, nên 5 × 2 = 10; phép cộng 5 + 2 không đếm đủ năm nhóm."),
    ],
    "WP_ONE_STEP_DIVISION_CONTEXT": [
        nq("Có 14 chiếc bánh chia đều cho 2 đĩa. Mỗi đĩa có bao nhiêu chiếc?", 7, "14 : 2 = 7."),
        nq("Có 35 bút xếp vào các hộp, mỗi hộp 5 bút. Cần bao nhiêu hộp?", 7, "35 : 5 = 7."),
        nq("20 học sinh chia đều thành 5 nhóm. An định tính 20 - 5. Muốn tìm số học sinh mỗi nhóm, kết quả đúng là bao nhiêu?", 4, "Chia đều thành 5 nhóm phải dùng 20 : 5 = 4, không dùng phép trừ 20 - 5."),
    ],
    "WP_SELECT_OPERATION_ONE_STEP": [
        mc("Có 10 quả, ăn 2 quả, hỏi còn lại. Chọn phép tính đúng.", "10 - 2", ["10 + 2", "10 × 2", "10 : 2"], "Tình huống lấy bớt và hỏi còn lại nên dùng phép trừ."),
        mc("Có 5 giỏ, mỗi giỏ 2 quả, hỏi tất cả. Chọn phép tính đúng.", "5 × 2", ["5 + 2", "5 - 2", "10 : 5"], "Nhiều nhóm bằng nhau và hỏi tổng nên dùng phép nhân."),
        mc("Có 30 bút chia đều cho 5 bạn. An chọn 30 - 5. Phép tính nào sửa đúng để tìm số bút mỗi bạn?", "30 : 5", ["30 + 5", "30 - 5", "5 + 5"], "Cần chia đều 30 bút thành 5 phần bằng nhau nên phép tính đúng là 30 : 5."),
    ],

    "POINT_RECOGNIZE": [
        mc("Kí hiệu nào phù hợp để đặt tên một điểm?", "A", ["AB", "1", "5 cm"], "Điểm thường được đặt tên bằng chữ cái in hoa như A."),
        mc("Phát biểu nào đúng về một điểm?", "Điểm biểu diễn một vị trí", ["Điểm có một độ dài xác định", "Điểm có hai đầu mút", "Điểm có thể kéo dài về hai phía"], "Điểm dùng để chỉ vị trí và không có độ dài."),
        mc("Trên hình có ba vị trí được đánh dấu A, B, C. Có bao nhiêu điểm được đặt tên?", "3", ["1", "2", "4"], "Mỗi tên A, B, C chỉ một điểm, nên có 3 điểm."),
    ],
    "LINE_SEGMENT_RECOGNIZE": [
        mc("Đoạn thẳng AB có bao nhiêu đầu mút?", "2", ["0", "1", "3"], "Đoạn thẳng AB có hai đầu mút A và B."),
        mc("Mô tả nào đúng về đoạn thẳng?", "Phần thẳng nối hai đầu mút", ["Phần thẳng kéo dài mãi về hai phía", "Nét uốn cong nối hai vị trí", "Chỉ một vị trí không có độ dài"], "Đoạn thẳng là phần thẳng nối hai điểm đầu mút."),
        mc("Nếu nối thẳng điểm M với điểm N, hình nhận được gọi là gì?", "đoạn thẳng MN", ["đường thẳng MN", "đường cong MN", "chỉ điểm M"], "Phần thẳng nối hai điểm M và N là đoạn thẳng MN."),
    ],
    "CURVE_RECOGNIZE": [
        mc("Đường nào được gọi là đường cong?", "Đường uốn lượn", ["Nét thẳng kéo dài không đổi hướng", "Phần thẳng có hai đầu mút", "Một vị trí được đánh dấu"], "Đường cong có hình dạng uốn lượn, không giữ hướng thẳng trên toàn bộ đường."),
        mc("Một nét vẽ hình vòng cung là ví dụ gần nhất của loại đường nào?", "đường cong", ["đường thẳng", "đoạn thẳng", "điểm"], "Vòng cung là một đường cong."),
        mc("Một nét đang đi thẳng rồi uốn sang bên và không khép kín. Kết luận nào phù hợp nhất?", "Nét đó có phần đường cong vì đã đổi hướng", ["Nét đó vẫn là đường thẳng vì có đoạn đi thẳng", "Nét đó phải khép kín mới là đường cong", "Nét đó là một điểm vì không có cạnh"], "Nét đã uốn và đổi hướng nên có phần đường cong; đường cong không bắt buộc phải khép kín."),
    ],
    "STRAIGHT_LINE_RECOGNIZE": [
        mc("Đường thẳng khác đoạn thẳng ở điểm nào?", "Có thể kéo dài về hai phía", ["Có đúng hai đầu mút", "Chỉ kéo dài về một phía", "Luôn uốn cong"], "Đường thẳng không bị giới hạn bởi hai đầu mút như đoạn thẳng."),
        mc("Một nét không uốn cong và có thể kéo dài mãi theo hai hướng là gì?", "đường thẳng", ["đoạn thẳng", "đường cong", "đường gấp khúc"], "Đó là mô tả của đường thẳng."),
        mc("Hai điểm A và B cùng nằm trên một đường thẳng d. Phát biểu nào đúng?", "A và B nằm trên d", ["A và B là hai đầu mút của d", "d chỉ gồm đoạn giữa A và B", "A hoặc B không thuộc d"], "Dữ kiện đã cho xác định cả hai điểm cùng thuộc đường thẳng d."),
    ],
    "POLYLINE_RECOGNIZE": [
        mc("Đường gấp khúc được tạo bởi gì?", "Nhiều đoạn thẳng nối tiếp", ["Nhiều đoạn thẳng rời nhau", "Một đoạn thẳng duy nhất", "Một nét cong liên tục"], "Đường gấp khúc là chuỗi các đoạn thẳng nối nhau."),
        mc("Một đường gồm ba đoạn AB, BC, CD nối tiếp nhau được gọi là gì?", "đường gấp khúc ABCD", ["đoạn thẳng AD", "đường thẳng AD", "đường cong ABCD"], "Ba đoạn thẳng nối tiếp tại B và C tạo một đường gấp khúc."),
        mc("Đường gấp khúc có 4 đoạn thẳng thì ít nhất có bao nhiêu điểm nối/đầu được nêu theo chuỗi mở?", "5", ["2", "3", "4"], "Một chuỗi mở 4 đoạn cần 5 điểm liên tiếp để tạo 4 đoạn."),
    ],
    "THREE_COLLINEAR_POINTS": [
        mc("Ba điểm A, B, C cùng nằm trên một đường thẳng. Ta nói ba điểm thế nào?", "thẳng hàng", ["không thẳng hàng", "tạo thành tam giác", "chỉ A và B thẳng hàng"], "Ba điểm cùng nằm trên một đường thẳng là ba điểm thẳng hàng."),
        mc("A và B nằm trên đường d, C nằm lệch khỏi d. Ba điểm A, B, C có thẳng hàng không?", "không", ["có", "luôn luôn", "không thể biết từ vị trí của C"], "C không nằm trên đường qua A và B nên ba điểm không thẳng hàng."),
        mc("Muốn kiểm tra ba điểm thẳng hàng, ta cần xem điều gì?", "Cả ba có cùng nằm trên một đường thẳng", ["Chỉ hai điểm có cùng nằm trên một đường thẳng", "Ba điểm có cách đều nhau", "Tên ba điểm có theo thứ tự bảng chữ cái"], "Điều kiện trực quan là ba điểm cùng thuộc một đường thẳng."),
    ],
    "QUADRILATERAL_RECOGNIZE": [
        nq("Một hình tứ giác có bao nhiêu cạnh?", 4, "Theo định nghĩa, hình tứ giác là hình kín có 4 cạnh."),
        mc("Hình nào chắc chắn là một tứ giác?", "hình chữ nhật", ["hình tam giác", "hình có 5 cạnh", "đường gấp khúc mở 4 đoạn"], "Hình chữ nhật có bốn cạnh và bốn đỉnh nên là tứ giác."),
        mc("Bạn Hà gọi một hình kín có đúng 4 cạnh là đường gấp khúc mở. Tên đúng của hình đó là gì?", "hình tứ giác", ["hình tam giác", "hình kín có 5 cạnh", "đường gấp khúc mở"], "Một hình kín có đúng bốn cạnh là hình tứ giác; đường gấp khúc mở chưa tạo thành hình kín."),
    ],
    "CYLINDER_RECOGNIZE": [
        mc("Vật nào gần dạng khối trụ nhất?", "lon nước", ["quả bóng", "hộp chữ nhật", "tấm bìa phẳng"], "Lon nước có hai đáy tròn và mặt cong xung quanh, gần dạng khối trụ."),
        mc("Khối trụ có đặc điểm nào?", "Hai đáy tròn và một mặt cong xung quanh", ["Hai đáy vuông và các mặt phẳng xung quanh", "Chỉ có một mặt tròn, không có mặt cong", "Có một đáy tròn và một đỉnh nhọn"], "Đặc trưng trực quan của khối trụ là hai đáy tròn cùng mặt cong bao quanh."),
        mc("Một ống hình trụ đứng thẳng, mặt trên và mặt dưới gần hình gì?", "hình tròn", ["hình vuông", "hình tam giác", "hình chữ nhật"], "Hai đáy của khối trụ là các hình tròn."),
    ],
    "SPHERE_RECOGNIZE": [
        mc("Vật nào gần dạng khối cầu nhất?", "quả bóng", ["lon nước", "hộp chữ nhật", "thước thẳng"], "Quả bóng tròn đều theo mọi hướng, gần dạng khối cầu."),
        mc("Khối cầu có cạnh hay đỉnh không?", "không", ["có cạnh nhưng không có đỉnh", "không có cạnh nhưng có hai đỉnh", "có cả cạnh và đỉnh"], "Khối cầu không có cạnh và không có đỉnh."),
        mc("Bạn Minh nói quả bóng giống khối trụ vì cả hai đều có dạng tròn. Mô tả nào giúp phân biệt khối cầu với khối trụ?", "Khối cầu tròn đều và không có hai đáy phẳng", ["Khối cầu có hai đáy tròn song song", "Khối cầu có một đáy phẳng và một đỉnh", "Khối cầu có các mặt phẳng và cạnh"], "Khối cầu tròn đều theo mọi hướng và không có hai đáy phẳng như khối trụ."),
    ],
    "DRAW_SEGMENT_GIVEN_LENGTH": [
        nq("Trên thước, đặt đầu A ở vạch 2 cm. Muốn AB dài 5 cm và B ở bên phải A, B ở vạch bao nhiêu?", 7, "Vị trí B = 2 + 5 = 7 cm."),
        iq("Trên thước cm, chọn hai đầu mút tại vạch 1 và vạch 9. Độ dài đoạn thẳng tạo được là bao nhiêu cm?", 8, "Hai đầu mút cách nhau 9 - 1 = 8 cm.", numeric_max=20),
        nq("Muốn tạo đoạn thẳng dài 6 cm với một đầu ở vạch 3 cm và đầu kia ở bên phải, chọn vạch nào?", 9, "3 + 6 = 9 cm."),
    ],
    "FOLD_CUT_COMPOSE_SHAPES": [
        mc("Ghép hai tam giác vuông bằng nhau theo một cạnh phù hợp có thể tạo thành hình nào trong nhiều cách ghép?", "một tứ giác", ["một điểm", "một khối cầu", "một đường thẳng vô hạn"], "Hai mảnh tam giác có thể ghép cạnh với cạnh để tạo một hình kín bốn cạnh trong cách ghép phù hợp."),
        mc("Khi cắt một tờ giấy hình vuông theo một đường thẳng từ góc này đến góc đối diện, thường thu được bao nhiêu mảnh?", "2", ["1", "3", "4"], "Một đường cắt chéo xuyên hết hình vuông chia tờ giấy thành hai mảnh."),
        mc("Bạn có hai tam giác vuông bằng nhau. Muốn ghép thành một tứ giác kín, cách nào hợp lý nhất?", "Đặt hai cạnh phù hợp sát nhau để hai mảnh không chồng lên nhau", ["Chỉ đặt hai đỉnh chạm nhau rồi để hở cạnh", "Chồng khít hai tam giác lên cùng một vị trí", "Để hai tam giác cách xa nhau"], "Muốn tạo một hình kín mới, cần ghép các cạnh phù hợp sát nhau mà không chồng hai mảnh lên nhau."),
    ],

    "HEAVIER_LIGHTER": [
        mc("Trên cân thăng bằng, đĩa bên trái hạ thấp hơn. Vật bên trái thường thế nào?", "nặng hơn", ["nhẹ hơn", "bằng nhau", "không thể so sánh"], "Với cân hoạt động đúng, phía hạ thấp hơn là phía nặng hơn."),
        mc("Nếu vật A nặng hơn vật B thì vật B thế nào so với A?", "nhẹ hơn", ["nặng hơn", "bằng nhau", "không thể kết luận"], "Quan hệ nặng hơn - nhẹ hơn là hai chiều đối lập."),
        mc("Cân cho hai đĩa ngang bằng. Kết luận phù hợp nhất là gì?", "Hai bên có khối lượng bằng nhau trong phép cân đó", ["Bên trái nặng hơn", "Bên phải nặng hơn", "Không thể so sánh khối lượng hai bên"], "Hai đĩa cân bằng cho thấy khối lượng hai bên bằng nhau trong điều kiện cân."),
    ],
    "MASS_KG_READ_WRITE": [
        nq("Một bao gạo ghi 5 kg. Số đo khối lượng là bao nhiêu kg?", 5, "Con số đi trước đơn vị kg là 5.", unit="kg"),
        mc("Kí hiệu đúng của kilôgam là gì?", "kg", ["km", "l", "cm"], "Kilôgam được kí hiệu là kg."),
        uq("Hai túi lần lượt nặng 2 kg và 3 kg. Hãy nhập kết quả kèm đơn vị.", 5, "kg", "Cùng đơn vị kg nên 2 kg + 3 kg = 5 kg.", aliases=["kilôgam"]),
    ],
    "CAPACITY_LITER_READ_WRITE": [
        nq("Một bình ghi 4 l. Dung tích được ghi là bao nhiêu lít?", 4, "Con số đi trước kí hiệu l là 4.", unit="l"),
        mc("Kí hiệu nào là đơn vị lít?", "l", ["kg", "km", "dm"], "Lít được kí hiệu là l."),
        nq("Bình có 7 l nước, rót thêm 2 l. Lan nói còn 5 l vì đã lấy 7 - 2. Có tất cả đúng bao nhiêu lít?", 9, "Đề cho rót thêm nên lượng nước tăng: 7 l + 2 l = 9 l, không phải lấy 7 - 2.", unit="l"),
    ],
    "LENGTH_DM_M_KM_RECOGNIZE_RELATION": [
        nq("1 m bằng bao nhiêu dm?", 10, "Theo quan hệ đơn vị, 1 m = 10 dm.", unit="dm"),
        nq("1 km bằng bao nhiêu m?", 1000, "Theo quan hệ đơn vị, 1 km = 1000 m.", unit="m"),
        mc("Đơn vị nào hợp lý hơn để nói quãng đường giữa hai làng?", "km", ["dm", "cm", "kg"], "Quãng đường dài thường được đo bằng ki-lô-mét."),
    ],
    "TIME_DAY_24_HOURS": [
        nq("Một ngày đầy đủ có bao nhiêu giờ?", 24, "Theo quan hệ thời gian, 1 ngày = 24 giờ.", unit="giờ"),
        mc("Khi đã đủ 24 giờ liên tiếp, khoảng thời gian đó bằng bao nhiêu ngày đầy đủ?", "1 ngày", ["2 ngày", "10 ngày", "không thể biết"], "Theo quan hệ đã học, 24 giờ liên tiếp bằng 1 ngày đầy đủ."),
        tf("Một hoạt động bắt đầu lúc 6 giờ sáng và kết thúc đúng 6 giờ sáng hôm sau kéo dài một ngày đầy đủ.", True, "Từ 6 giờ sáng hôm nay đến 6 giờ sáng hôm sau là 24 giờ, đúng bằng một ngày đầy đủ."),
    ],
    "TIME_HOUR_60_MINUTES": [
        nq("1 giờ bằng bao nhiêu phút?", 60, "Theo quan hệ thời gian, 1 giờ = 60 phút.", unit="phút"),
        mc("45 phút so với 1 giờ là khoảng thời gian thế nào?", "ngắn hơn", ["dài hơn", "bằng nhau", "không so sánh được"], "1 giờ = 60 phút; 45 phút ít hơn 60 phút nên ngắn hơn 1 giờ."),
        mc("Một hoạt động bắt đầu lúc 8 giờ và kết thúc lúc 9 giờ cùng buổi. Khoảng thời gian đó là 60 phút. Cách gọi nào tương đương?", "1 giờ", ["1 ngày", "30 phút", "không thể biết"], "Từ 8 giờ đến 9 giờ là 60 phút, mà 60 phút bằng đúng 1 giờ."),
    ],
    "CALENDAR_DAYS_IN_MONTH_DATE": [
        nq("Tháng 4 có bao nhiêu ngày?", 30, "Tháng 4 có 30 ngày.", unit="ngày"),
        nq("Tháng 5 có bao nhiêu ngày?", 31, "Tháng 5 có 31 ngày.", unit="ngày"),
        mc("Lan ghi ngày sau ngày 14 tháng 9 là ngày 14 tháng 10. Cách sửa nào đúng?", "15 tháng 9", ["13 tháng 9", "14 tháng 10", "16 tháng 9"], "Ngày kế tiếp chỉ tăng số ngày thêm 1 và vẫn ở tháng 9, nên phải sửa thành 15 tháng 9."),
    ],
    "MONEY_VND_NOTE_RECOGNITION": [
        mc("Khi xem hình một tờ tiền Việt Nam, thông tin nào cần đọc để nhận biết giá trị?", "Con số mệnh giá và chữ đồng trên tờ", ["Chỉ màu sắc của tờ tiền", "Chỉ kích thước của tờ tiền", "Chỉ hình trang trí nổi bật trên tờ"], "Để nhận biết giá trị, cần đọc con số mệnh giá và đơn vị đồng thể hiện trên tờ tiền."),
        tf("Chỉ nhìn màu sắc là đủ để xác định chắc chắn giá trị của một tờ tiền Việt Nam.", False, "Màu sắc có thể hỗ trợ quan sát nhưng cần đọc con số mệnh giá và đơn vị đồng để nhận biết giá trị."),
        mc("Có hai hình tờ tiền A và B với con số mệnh giá khác nhau. Muốn biết tờ nào có giá trị lớn hơn, bước nào phù hợp nhất?", "Đọc và so sánh con số mệnh giá trên hai tờ", ["Chỉ so sánh màu sắc của hai tờ", "Chỉ so sánh kích thước của hai tờ", "Chọn tờ có nhiều chữ hơn"], "Đọc đúng con số mệnh giá trên từng tờ rồi so sánh hai giá trị; không đoán chỉ từ màu sắc hoặc vị trí."),
    ],
    "MEASURE_WITH_RULER_CM": [
        nq("Một đoạn thẳng bắt đầu ở vạch 0 cm và kết thúc ở vạch 8 cm. Dài bao nhiêu cm?", 8, "8 - 0 = 8 cm.", unit="cm"),
        nq("Một đoạn bắt đầu ở vạch 3 cm và kết thúc ở vạch 10 cm. Dài bao nhiêu cm?", 7, "10 - 3 = 7 cm.", unit="cm"),
        nq("Bạn Minh đo bút chì từ vạch 2 cm đến vạch 11 cm nhưng đọc là 11 cm vì chỉ nhìn vạch cuối. Độ dài đúng là bao nhiêu cm?", 9, "Vật không bắt đầu ở vạch 0 nên phải lấy 11 - 2 = 9 cm; không thể chỉ đọc số ở vạch cuối.", unit="cm"),
    ],
    "MEASURE_WITH_COMMON_SCALE": [
        nq("Một thang đo có các vạch 0, 2, 4, 6, 8. Mỗi vạch tăng bao nhiêu đơn vị?", 2, "Hiệu giữa hai vạch liên tiếp là 2."),
        nq("Thang đo có vạch 10, 20, 30, __, 50. Điền giá trị vạch thiếu.", 40, "Mỗi bước tăng 10 nên vạch thiếu là 40."),
        nq("Trên thang chia đều, vạch thứ nhất là 0, vạch thứ hai là 5. Vạch thứ tư có giá trị bao nhiêu?", 15, "Các vạch lần lượt 0, 5, 10, 15."),
    ],
    "CLOCK_MINUTE_HAND_AT_3_OR_6": [
        mc("Kim phút chỉ số 3, kim giờ vừa qua số 4. Đồng hồ chỉ thời gian nào?", "4 giờ 15 phút", ["4 giờ 30 phút", "3 giờ 4 phút", "4 giờ 3 phút"], "Kim phút ở số 3 tương ứng 15 phút; kim giờ ở khoảng sau 4 nên là 4 giờ 15 phút."),
        mc("Kim phút chỉ số 6, kim giờ ở giữa 7 và 8. Đồng hồ chỉ thời gian nào?", "7 giờ 30 phút", ["7 giờ 15 phút", "6 giờ 7 phút", "8 giờ 30 phút"], "Kim phút ở số 6 là 30 phút; kim giờ giữa 7 và 8 là 7 giờ 30 phút."),
        mc("Đồng hồ đang chỉ 5 giờ 15 phút. Kim phút đi tiếp từ số 3 đến số 6, còn kim giờ vẫn nằm giữa 5 và 6. Khi đó đồng hồ chỉ thời gian nào?", "5 giờ 30 phút", ["5 giờ 15 phút", "6 giờ 30 phút", "5 giờ 45 phút"], "Kim phút từ số 3 đến số 6 chuyển từ 15 phút sang 30 phút; kim giờ vẫn trong khoảng sau 5 nên là 5 giờ 30 phút."),
    ],
    "MEASUREMENT_CONVERT_CALCULATE_LEARNED_UNITS": [
        nq("Một sợi dây dài 3 m. Nếu ghi độ dài bằng đề-xi-mét, số đo là bao nhiêu dm?", 30, "1 m = 10 dm nên 3 m = 30 dm.", unit="dm"),
        nq("2 kg + 3 kg bằng bao nhiêu kg?", 5, "Hai số đo cùng đơn vị kg nên cộng 2 + 3 = 5 kg.", unit="kg"),
        nq("Một đoạn dây dài 2 m, nối thêm đoạn 3 dm. Tổng độ dài là bao nhiêu dm?", 23, "Đổi 2 m = 20 dm, rồi cộng 20 + 3 = 23 dm.", unit="dm", numeric_max=1000),
    ],
    "MEASUREMENT_ESTIMATE_BASIC": [
        mc("Chiều dài một chiếc bút chì hợp lý nhất khoảng bao nhiêu?", "15 cm", ["15 km", "15 m", "150 m"], "Bút chì là vật nhỏ, đơn vị cm và độ dài khoảng vài chục cm là hợp lý."),
        mc("Chiều cao một cánh cửa hợp lý nhất gần giá trị nào?", "2 m", ["2 cm", "2 km", "20 km"], "Cửa cao khoảng vài mét, nên 2 m là hợp lý."),
        mc("Một thanh tham chiếu dài 10 cm. Một vật nhìn dài khoảng gấp đôi thanh đó. Ước lượng vật dài bao nhiêu?", "20 cm", ["5 cm", "100 cm", "2 km"], "Gấp đôi mốc 10 cm là khoảng 20 cm."),
    ],
    "POLYLINE_LENGTH_SUM_SEGMENTS": [
        nq("Đường gấp khúc có hai đoạn dài 4 cm và 6 cm. Tổng độ dài là bao nhiêu?", 10, "4 + 6 = 10 cm.", unit="cm"),
        nq("Ba đoạn của đường gấp khúc dài 3 cm, 5 cm và 2 cm. Độ dài cả đường là bao nhiêu?", 10, "3 + 5 + 2 = 10 cm.", unit="cm"),
        nq("Đường gấp khúc ABCD có AB = 7 cm, BC = 4 cm, CD = 6 cm. Bạn chỉ cộng AB và CD được 13 cm. Tổng độ dài đúng là bao nhiêu cm?", 17, "Phải cộng đủ ba đoạn AB, BC và CD: 7 + 4 + 6 = 17 cm; phép tính 13 cm đã bỏ sót đoạn BC.", unit="cm"),
    ],
    "MEASUREMENT_REAL_WORLD_ONE_STEP": [
        nq("Một sợi dây dài 2 m, nối thêm đoạn 3 m. Dây dài tất cả bao nhiêu mét?", 5, "2 m + 3 m = 5 m.", unit="m"),
        nq("Bình có 8 l nước, rót ra 3 l. Còn bao nhiêu lít?", 5, "8 l - 3 l = 5 l.", unit="l"),
        nq("Bao A nặng 6 kg, bao B nặng 4 kg. Bạn Bình cộng được 10 nhưng ghi đơn vị m. Cả hai bao nặng đúng bao nhiêu kg?", 10, "Hai đại lượng đều là khối lượng nên cộng 6 + 4 = 10 và giữ đơn vị kg; đơn vị m của Bình là sai.", unit="kg"),
    ],

    "DATA_COLLECT_CLASSIFY_COUNT": [
        nq("Có các thẻ: đỏ, xanh, đỏ, vàng, đỏ. Có bao nhiêu thẻ đỏ?", 3, "Phân loại theo màu rồi đếm ba thẻ đỏ."),
        nq("Danh sách vật: bút, sách, bút, thước, bút, sách. Có bao nhiêu bút?", 3, "Lọc các mục 'bút' rồi đếm được 3."),
        nq("Một lớp ghi loại quả yêu thích: táo, cam, táo, táo, cam, chuối. Có bao nhiêu bạn chọn cam?", 2, "Trong danh sách có hai mục 'cam'."),
    ],
    "PICTOGRAPH_READ_DESCRIBE": [
        nq("Biểu đồ tranh có 4 hình ngôi sao cho nhóm A; chú giải 1 hình = 1 bạn. Nhóm A có bao nhiêu bạn?", 4, "4 biểu tượng × 1 bạn mỗi biểu tượng = 4 bạn."),
        nq("Biểu đồ có 3 hình quả táo; chú giải 1 hình = 2 quả. Có bao nhiêu quả táo?", 6, "3 biểu tượng × 2 quả = 6 quả."),
        nq("Một hàng có 5 biểu tượng, chú giải 1 biểu tượng = 2 quyển sách. Minh nói hàng đó có 5 quyển vì chỉ đếm số hình. Hàng thực sự biểu diễn bao nhiêu quyển?", 10, "Mỗi hình đại diện 2 quyển nên phải dùng chú giải: 5 × 2 = 10 quyển; chỉ đếm 5 hình là chưa đủ."),
    ],
    "PICTOGRAPH_SIMPLE_INFERENCE": [
        nq("Biểu đồ: nhóm A có 6 biểu tượng, nhóm B có 4 biểu tượng; 1 biểu tượng = 1 bạn. A nhiều hơn B bao nhiêu bạn?", 2, "6 - 4 = 2 bạn."),
        mc("Biểu đồ có nhóm Cam 5 biểu tượng, Táo 7 biểu tượng, Chuối 3 biểu tượng; chú giải như nhau. Nhóm nào nhiều nhất?", "Táo", ["Cam", "Chuối", "Cả ba bằng nhau"], "7 biểu tượng lớn hơn 5 và 3 nên Táo nhiều nhất."),
        nq("Biểu đồ có 2 biểu tượng cho A và 5 biểu tượng cho B; 1 biểu tượng = 2 vật. B nhiều hơn A bao nhiêu vật?", 6, "B có 10 vật, A có 4 vật; 10 - 4 = 6."),
    ],
    "EVENT_POSSIBLE": [
        mc("Gieo xúc xắc có các mặt 1 đến 6. Xuất hiện số 4 là sự kiện gì?", "có thể", ["chắc chắn", "không thể", "bằng nhau"], "Số 4 là một mặt có thể xuất hiện nhưng không phải lần nào cũng ra 4."),
        mc("Trong túi có bóng đỏ và xanh. Lấy ngẫu nhiên một bóng, lấy được bóng đỏ là gì?", "có thể", ["chắc chắn", "không thể", "luôn sai"], "Có bóng đỏ trong túi nên có thể lấy được, nhưng còn bóng xanh nên không chắc chắn."),
        mc("Vòng quay có các số 1, 2, 3. Bạn Nam nói kim chắc chắn dừng ở số 2. Phân loại đúng sự kiện kim dừng ở số 2 là gì?", "có thể", ["chắc chắn", "không thể", "không có kết quả"], "Số 2 có trên vòng quay nên có thể xuất hiện, nhưng còn số 1 và 3 nên không chắc chắn."),
    ],
    "EVENT_CERTAIN": [
        mc("Gieo xúc xắc chuẩn 1 đến 6. Kết quả là một số từ 1 đến 6. Sự kiện này là gì?", "chắc chắn", ["có thể nhưng không chắc", "không thể", "sai"], "Mọi mặt của xúc xắc đều là một số từ 1 đến 6."),
        mc("Trong túi chỉ có bóng xanh. Lấy một bóng, bóng lấy ra màu xanh là gì?", "chắc chắn", ["không thể", "có thể nhưng không chắc", "không có màu"], "Mọi bóng trong túi đều xanh nên lấy bóng nào cũng xanh."),
        mc("Một hộp chỉ chứa thẻ số 2 và 5. Mai cho rằng rút được số 2 hoặc 5 chỉ là sự kiện có thể. Phân loại đúng là gì?", "chắc chắn", ["không thể", "chỉ có thể", "không xác định"], "Mọi thẻ trong hộp đều mang số 2 hoặc 5 nên khi rút một thẻ, sự kiện này chắc chắn xảy ra."),
    ],
    "EVENT_IMPOSSIBLE": [
        mc("Gieo xúc xắc chuẩn 1 đến 6. Xuất hiện số 8 là sự kiện gì?", "không thể", ["có thể", "chắc chắn", "luôn đúng"], "Không có mặt số 8 nên sự kiện không thể xảy ra."),
        mc("Trong túi chỉ có bóng đỏ. Lấy một bóng màu xanh là gì?", "không thể", ["chắc chắn", "có thể", "bằng nhau"], "Không có bóng xanh trong túi nên không thể lấy bóng xanh."),
        mc("Vòng quay chỉ có các số 1, 2, 3. An nói kim vẫn có thể dừng ở số 5. Phân loại đúng sự kiện dừng ở số 5 là gì?", "không thể", ["có thể", "chắc chắn", "luôn xảy ra"], "Số 5 không xuất hiện trên vòng quay nên không có kết quả hợp lệ nào làm sự kiện xảy ra; đó là sự kiện không thể."),
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
    if set(baseline_skills) != set(WORKED_EXAMPLES):
        missing = sorted(set(baseline_skills) - set(WORKED_EXAMPLES))
        extra = sorted(set(WORKED_EXAMPLES) - set(baseline_skills))
        raise SystemExit(f"WORKED_EXAMPLES mismatch missing={missing} extra={extra}")

    chapter_by_domain = {c["domain_key"]: c["id"] for c in CHAPTERS}
    topic_by_id = {t["id"]: t for t in TOPICS}
    questions = []
    lessons = []
    choice_position_counts: dict[int, int] = {}

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
                answer_display = str(spec["correct_answer"])
                if spec["answer_kind"] == "integer" and spec.get("answer_unit"):
                    answer_display += " " + str(spec["answer_unit"])
                question_explanation = explanation_with_answer(
                    deepen_explanation(spec["explanation_vi"], question_type, concept_name), answer_display)
                first_hint_text = answer_safe_hint(
                    first_hint(question_type, difficulty, concept_name), spec["prompt_vi"], spec["correct_answer"],
                    spec["answer_kind"], question_type, 1)
                second_hint_text = answer_safe_hint(
                    second_hint(question_type, difficulty, concept_name), spec["prompt_vi"], spec["correct_answer"],
                    spec["answer_kind"], question_type, 2)
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
                    "explanation_vi": question_explanation,
                    "hints_vi": [first_hint_text, second_hint_text],
                    "tags": [skill.lower(), domain_key, difficulty, question_type, spec["answer_kind"]],
                    "validation": spec["validation"],
                    "status": "CHILD_READY",
                }
                for optional_key in ("answer_unit", "expected_unit", "accepted_units"):
                    if optional_key in spec:
                        q[optional_key] = spec[optional_key]
                if "choices" in spec:
                    choices = [dict(choice) for choice in spec["choices"]]
                    if not choices:
                        raise SystemExit(f"Choice question has no choices: {qid}")
                    original_correct_id = spec.get("correct_choice_id")
                    correct = next((choice for choice in choices if choice.get("id") == original_correct_id), None)
                    if correct is None:
                        raise SystemExit(f"Choice question missing correct choice: {qid}")
                    correct["rationale_vi"] = q["explanation_vi"]
                    distractors = [choice for choice in choices if choice is not correct]
                    count = len(choices)
                    target = choice_position_counts.get(count, 0) % count
                    choice_position_counts[count] = choice_position_counts.get(count, 0) + 1
                    ordered = distractors[:target] + [correct] + distractors[target:]
                    for index, choice in enumerate(ordered):
                        choice["id"] = chr(ord("a") + index)
                        if choice is not correct:
                            choice["rationale_vi"] = distractor_rationale(
                                choice["text"], q["explanation_vi"], skill, q["prompt_vi"])
                    q["choices"] = ordered
                    q["correct_choice_id"] = ordered[target]["id"]
                questions.append(q)

            example = WORKED_EXAMPLES[skill]
            if not isinstance(example, dict) or set(example) != {"prompt_vi", "answer", "solution_steps_vi"}:
                raise SystemExit(f"Invalid worked example shape for {skill}")
            if not isinstance(example["prompt_vi"], str) or not example["prompt_vi"].strip():
                raise SystemExit(f"Worked example prompt missing for {skill}")
            if not isinstance(example["answer"], str) or not example["answer"].strip():
                raise SystemExit(f"Worked example answer missing for {skill}")
            if not isinstance(example["solution_steps_vi"], list) or len(example["solution_steps_vi"]) < 2 or not all(isinstance(x, str) and x.strip() for x in example["solution_steps_vi"]):
                raise SystemExit(f"Worked example needs at least two solution steps for {skill}")
            lesson_question_types = [question["question_type"] for question in questions[-3:]]
            lessons.append({
                "id": lesson_id,
                "chapter_id": chapter_id,
                "topic_id": topic_id,
                "skill_id": skill,
                "order_in_domain": order,
                "title_vi": title,
                "objectives_vi": [
                    first_objective(lesson_question_types, concept_name),
                    second_objective(lesson_question_types, concept_name),
                ],
                "explanation_vi": explanation,
                "concepts": [{
                    "id": f"m2_cp_{slug(skill)}_01",
                    "name_vi": concept_name,
                    "definition_vi": concept_def,
                }],
                "worked_examples": [{
                    "id": f"m2_ex_{slug(skill)}_01",
                    "prompt_vi": example["prompt_vi"],
                    "solution_steps_vi": list(example["solution_steps_vi"]),
                    "answer": example["answer"],
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
