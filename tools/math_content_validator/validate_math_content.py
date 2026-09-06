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
DIFFICULTIES = {"basic", "medium", "application"}
ENGINE_ANSWER_KINDS = {"integer", "interaction_integer", "number", "decimal", "fraction", "text", "unit", "expression"}
GRADE2_USED_ANSWER_KINDS = {"integer", "interaction_integer", "text", "unit", "expression"}
QUESTION_TYPES = {"numeric_input", "multiple_choice", "true_false", "expression_input", "unit_input", "interactive_measurement", "word_problem"}
ADD_CARRY_RULES = {"ADD_WITHIN_1000_NO_CARRY": 0, "ADD_WITHIN_1000_ONE_CARRY_MAX": 1}
SUB_BORROW_RULES = {"SUB_WITHIN_1000_NO_BORROW": 0, "SUB_WITHIN_1000_ONE_BORROW_MAX": 1}
MONEY_DENOMINATION_RE = re.compile(r"\b\d[\d\s.,]*\s*đồng\b", re.IGNORECASE)
TIME_RELATION_SKILLS = {"TIME_DAY_24_HOURS", "TIME_HOUR_60_MINUTES"}
TIME_OUT_OF_SCOPE_ARITH_RE = re.compile(r"\b\d+\s*(?:×|\*|÷|/|:)\s*\d+\b")
GRADE2_MUL_LITERAL_RE = re.compile(r"(?<!\d)(\d+)\s*(?:×|\*)\s*(\d+)(?!\d)")
GRADE2_DIV_LITERAL_RE = re.compile(r"(?<!\d)(\d+)(?:\s*÷\s*|\s+:\s+)(\d+)(?!\d)")
NUMERIC_CHOICE_EXPR_RE = re.compile(r"^[\d\s+\-−–*/×÷:().,]+$")
MIN_QUESTION_EXPLANATION_CHARS = 32
MAX_HINT_CHARS = 130
GENERIC_SECOND_HINT = "Thực hiện từng bước và kiểm tra lại với dữ kiện của câu hỏi."
GENERIC_SECOND_OBJECTIVE = "Giải thích được cách làm bằng ngôn ngữ ngắn gọn và kiểm tra kết quả theo dữ kiện."
GENERIC_DISTRACTOR_RATIONALE = "Lựa chọn này không phù hợp với quy tắc hoặc dữ kiện của bài."
CHILD_FACING_KEYS = {
    "title_vi", "objectives_vi", "explanation_vi", "name_vi", "definition_vi",
    "prompt_vi", "solution_steps_vi", "hints_vi", "rationale_vi", "text",
    "answer", "correct_answer",
}
INTERNAL_CHILD_VOCAB_RE = re.compile(
    r"(?i)(?<![A-Za-z])(?:baseline|runtime|mapping|template|deterministic|numeric|metadata|validator|contract|engine|json|source|prompt)(?![A-Za-z])"
)


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


def validate(baseline_path: Path, lesson_path: Path, question_path: Path) -> tuple[list[str], dict]:
    errors: list[str] = []
    baseline = load_json(baseline_path, errors)
    catalog = load_json(lesson_path, errors)
    bank = load_json(question_path, errors)
    if errors:
        return errors, {}

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

    baseline_skills = []
    domains = baseline.get("domains")
    if not isinstance(domains, dict):
        errors.append("baseline_domains_missing")
        domains = {}
    for domain, skills in domains.items():
        if not isinstance(skills, list):
            errors.append(f"baseline_domain_not_list:{domain}")
            continue
        baseline_skills.extend(skills)
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
        chapter_domain[cid] = domain

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
        order_in_domain = lesson.get("order_in_domain")
        if type(order_in_domain) is not int or order_in_domain < 1:
            errors.append(f"invalid_lesson_order:{where}:{order_in_domain!r}")
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
        if tid in topic_chapter and cid and topic_chapter[tid] != cid:
            errors.append(f"topic_chapter_mismatch:{where}:{tid}:{cid}")
        if cid in chapter_domain and skill in baseline_skill_set:
            expected_domain = next((d for d, skills in domains.items() if skill in skills), None)
            if chapter_domain[cid] != expected_domain:
                errors.append(f"skill_domain_mismatch:{where}:{skill}:{chapter_domain[cid]}:{expected_domain}")
        required_text(lesson, "title_vi", where, errors)
        required_text(lesson, "explanation_vi", where, errors)
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
            required_text(concept, "name_vi", cwhere, errors)
            required_text(concept, "definition_vi", cwhere, errors)
        examples = required_list(lesson, "worked_examples", where, errors)
        for j, example in enumerate(examples):
            ewhere = f"{where}.example[{j}]"
            if not isinstance(example, dict):
                errors.append(f"not_object:{ewhere}"); continue
            x = check_id(example.get("id"), ewhere, errors)
            if x:
                example_ids.add(x); all_ids.append((x, ewhere))
            required_text(example, "prompt_vi", ewhere, errors)
            answer = required_text(example, "answer", ewhere, errors)
            solution_steps = required_list(example, "solution_steps_vi", ewhere, errors)
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

    missing_lesson_skills = sorted(baseline_skill_set - set(lesson_by_skill))
    extra_lesson_skills = sorted(set(lesson_by_skill) - baseline_skill_set)
    for s in missing_lesson_skills:
        errors.append(f"missing_lesson_for_skill:{s}")
    for s in extra_lesson_skills:
        errors.append(f"extra_lesson_skill:{s}")
    for skill, count in lesson_skill_counts.items():
        if count != 1:
            errors.append(f"lesson_skill_count:{skill}:{count}")
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
    second_hint_counts = Counter()
    distractor_rationale_counts = Counter()
    correct_choice_positions_by_count: dict[int, Counter] = defaultdict(Counter)

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
        question_counts_by_skill[skill] += 1
        difficulty = q.get("difficulty")
        if difficulty not in DIFFICULTIES:
            errors.append(f"invalid_difficulty:{where}:{difficulty!r}")
        else:
            question_counts_by_difficulty[difficulty] += 1
        prompt = required_text(q, "prompt_vi", where, errors)
        if prompt:
            prompts_by_lesson[lid].append((qid, prompt))
            all_question_prompts.append((lid, qid, prompt))
        explanation = required_text(q, "explanation_vi", where, errors)
        if explanation and len(explanation) < MIN_QUESTION_EXPLANATION_CHARS:
            errors.append(f"question_explanation_too_short:{where}:{len(explanation)}")
        if explanation and not explanation_states_answer(q, explanation):
            errors.append(f"question_explanation_missing_answer_evidence:{where}:{question_answer_display(q)[:80]}")
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
                if len(hint_text.strip()) > MAX_HINT_CHARS:
                    errors.append(f"hint_too_long:{where}:{hint_index + 1}:{len(hint_text.strip())}")
                if hint_reveals_unseen_answer(q, hint_text):
                    errors.append(f"hint_reveals_unseen_answer:{where}:{hint_index + 1}")
            second_hint = " ".join(hints[1].split())
            second_hint_counts[second_hint] += 1
            if second_hint == GENERIC_SECOND_HINT:
                errors.append(f"generic_second_hint:{where}")
        question_type = required_text(q, "question_type", where, errors)
        if question_type not in QUESTION_TYPES:
            errors.append(f"unsupported_question_type:{where}:{question_type!r}")
        tags = required_list(q, "tags", where, errors, 5)
        if skill and skill.lower() not in tags:
            errors.append(f"missing_skill_tag:{where}:{skill.lower()}")
        if question_type and question_type not in tags:
            errors.append(f"missing_question_type_tag:{where}:{question_type}")
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
        if "answer_unit" in q:
            answer_unit = q.get("answer_unit")
            if not isinstance(answer_unit, str) or not answer_unit.strip():
                errors.append(f"invalid_answer_unit:{where}:{answer_unit!r}")
            elif kind != "integer":
                errors.append(f"answer_unit_requires_integer_kind:{where}:{kind}")
            elif question_type not in {"numeric_input", "word_problem"}:
                errors.append(f"answer_unit_question_type_mismatch:{where}:{question_type}")

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
            for cid, rationale in zip(choice_ids, choice_rationales):
                if cid == correct_id:
                    continue
                normalized_rationale = " ".join(rationale.split())
                distractor_rationale_counts[normalized_rationale] += 1
                if normalized_rationale == GENERIC_DISTRACTOR_RATIONALE:
                    errors.append(f"generic_distractor_rationale:{where}:{cid}")
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

    for hint_text, count in second_hint_counts.items():
        if count > 3:
            errors.append(f"over_reused_second_hint:{count}:{hint_text[:80]}")

    for rationale_text, count in distractor_rationale_counts.items():
        if count > 3:
            errors.append(f"over_reused_distractor_rationale:{count}:{rationale_text[:80]}")

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

    # Global ID uniqueness, including nested content entities.
    id_counts = Counter(x for x, _ in all_ids if x)
    for value, count in id_counts.items():
        if count > 1:
            locations = [where for x, where in all_ids if x == value]
            errors.append(f"duplicate_id:{value}:{count}:{'|'.join(locations)}")

    # Bank contract must match the engine contract while Grade-2 content uses only in-scope kinds.
    declared_engine_kinds = bank.get("supported_answer_kinds")
    if not isinstance(declared_engine_kinds, list) or set(declared_engine_kinds) != ENGINE_ANSWER_KINDS:
        errors.append(f"bank_supported_answer_kinds_mismatch:{declared_engine_kinds!r}")
    actual_answer_kinds = {q.get("answer_kind") for q in questions if isinstance(q, dict)}
    declared_used_kinds = bank.get("grade2_used_answer_kinds")
    if not isinstance(declared_used_kinds, list) or set(declared_used_kinds) != actual_answer_kinds:
        errors.append(f"bank_used_answer_kinds_mismatch:{declared_used_kinds!r}:{sorted(actual_answer_kinds)}")
    actual_question_types = {q.get("question_type") for q in questions if isinstance(q, dict)}
    declared_question_types = bank.get("question_types")
    if not isinstance(declared_question_types, list) or set(declared_question_types) != actual_question_types:
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
