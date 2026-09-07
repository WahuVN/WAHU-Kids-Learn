#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Build a deterministic shadow 402-question Math bank without touching runtime JSON."""
from __future__ import annotations

import copy
import importlib.util
import json
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DRAFT_ROOT = Path(__file__).resolve().parent
AUTHORING_PATH = ROOT / "tools" / "math_content_authoring" / "generate_grade2_content.py"
BASELINE_PATH = ROOT / "curriculum" / "math_grade2" / "moet_baseline_v1.json"
RUNTIME_LESSONS = ROOT / "content_packs" / "math_grade2_v1" / "lesson_catalog_v1.json"
RUNTIME_QUESTIONS = ROOT / "content_packs" / "math_grade2_v1" / "question_bank_v1.json"
PREVIEW_ROOT = DRAFT_ROOT / "pool6_preview"
PREVIEW_LESSONS = PREVIEW_ROOT / "lesson_catalog_pool6_preview.json"
PREVIEW_QUESTIONS = PREVIEW_ROOT / "question_bank_pool6_preview.json"
DIFFICULTIES = ("basic", "medium", "application")


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, str(path))
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


g = load_module("math_grade2_authoring_for_pool6_draft", AUTHORING_PATH)


def load_draft_specs() -> dict[str, list[dict]]:
    merged: dict[str, list[dict]] = {}
    for index in range(1, 5):
        path = DRAFT_ROOT / f"pool6_specs_{index:02d}.py"
        module = load_module(f"math_pool6_specs_{index:02d}", path)
        items = module.build(g)
        overlap = set(merged) & set(items)
        if overlap:
            raise ValueError(f"Duplicate draft skill specs: {sorted(overlap)}")
        merged.update(items)
    return merged


def _skill_domain(baseline: dict) -> dict[str, str]:
    return {skill: domain for domain, skills in baseline["domains"].items() for skill in skills}


def _runtime_choice_counts(bank: dict) -> Counter:
    counts = Counter()
    for question in bank["questions"]:
        choices = question.get("choices") or []
        if not choices:
            continue
        correct_id = question.get("correct_choice_id")
        position = next((i for i, choice in enumerate(choices) if choice.get("id") == correct_id), None)
        if position is None:
            raise ValueError(f"Runtime choice missing correct id: {question['id']}")
        counts[(len(choices), position)] += 1
    return counts


def _choose_balanced_position(choice_counts: Counter, count: int) -> int:
    values = [choice_counts[(count, position)] for position in range(count)]
    minimum = min(values)
    return next(position for position, value in enumerate(values) if value == minimum)


def _draft_fallback_rationale(choice_text: str, correct_text: str, explanation: str) -> str:
    return f"“{choice_text}” chưa đúng. Dữ kiện dẫn tới “{correct_text}”. {explanation}"


def _build_question(skill: str, domain: str, difficulty: str, ordinal: int, spec: dict,
                    lesson_info: tuple, choice_counts: Counter) -> dict:
    _, _, _, concept_name, _, _ = lesson_info
    lesson_id = "m2_ls_" + g.slug(skill)
    qid = f"m2_q_{g.slug(skill)}_{ordinal:02d}"
    question_type = spec["question_type"]
    if domain == "word_problems" and question_type == "numeric_input":
        question_type = "word_problem"

    answer_display = str(spec["correct_answer"])
    if spec["answer_kind"] == "integer" and spec.get("answer_unit"):
        answer_display += " " + str(spec["answer_unit"])
    explanation = g.explanation_with_answer(
        g.deepen_explanation(spec["explanation_vi"], question_type, concept_name), answer_display)
    hint1 = g.answer_safe_hint(
        g.first_hint(question_type, difficulty, concept_name), spec["prompt_vi"], spec["correct_answer"],
        spec["answer_kind"], question_type, 1)
    hint2 = g.answer_safe_hint(
        g.second_hint(question_type, difficulty, concept_name), spec["prompt_vi"], spec["correct_answer"],
        spec["answer_kind"], question_type, 2)

    question = {
        "id": qid,
        "lesson_id": lesson_id,
        "skill_id": skill,
        "difficulty": difficulty,
        "question_type": question_type,
        "prompt_vi": spec["prompt_vi"],
        "answer_kind": spec["answer_kind"],
        "correct_answer": spec["correct_answer"],
        "accepted_answers": list(spec["accepted_answers"]),
        "explanation_vi": explanation,
        "hints_vi": [hint1, hint2],
        "tags": [skill.lower(), domain, difficulty, question_type, spec["answer_kind"]],
        "validation": copy.deepcopy(spec["validation"]),
        "status": "CHILD_READY",
    }
    for optional_key in ("answer_unit", "expected_unit", "accepted_units"):
        if optional_key in spec:
            question[optional_key] = copy.deepcopy(spec[optional_key])

    if "choices" in spec:
        choices = [dict(choice) for choice in spec["choices"]]
        original_correct_id = spec.get("correct_choice_id")
        correct = next((choice for choice in choices if choice.get("id") == original_correct_id), None)
        if correct is None:
            raise ValueError(f"Draft choice missing correct option: {qid}")
        correct["rationale_vi"] = explanation
        distractors = [choice for choice in choices if choice is not correct]
        target = _choose_balanced_position(choice_counts, len(choices))
        ordered = distractors[:target] + [correct] + distractors[target:]
        for index, choice in enumerate(ordered):
            choice["id"] = chr(ord("a") + index)
            if choice is not correct:
                structured = g.structured_choice_reason(skill, question["prompt_vi"], choice["text"])
                component = (g.COMPONENT_TERM_REASONS.get(choice["text"].strip().casefold())
                             if skill in g.COMPONENT_SKILLS else None)
                if structured or component:
                    choice["rationale_vi"] = g.distractor_rationale(
                        choice["text"], explanation, skill, question["prompt_vi"])
                else:
                    choice["rationale_vi"] = _draft_fallback_rationale(
                        choice["text"], str(spec["correct_answer"]), explanation)
        question["choices"] = ordered
        question["correct_choice_id"] = ordered[target]["id"]
        choice_counts[(len(choices), target)] += 1
    return question


def build_preview() -> tuple[dict, dict, list[dict]]:
    baseline = json.loads(BASELINE_PATH.read_text(encoding="utf-8"))
    catalog = json.loads(RUNTIME_LESSONS.read_text(encoding="utf-8"))
    bank = json.loads(RUNTIME_QUESTIONS.read_text(encoding="utf-8"))
    specs = load_draft_specs()
    domains = _skill_domain(baseline)
    baseline_skills = set(domains)
    if set(specs) != baseline_skills:
        raise ValueError(f"Draft skill mismatch missing={sorted(baseline_skills-set(specs))} extra={sorted(set(specs)-baseline_skills)}")
    if any(len(items) != 3 for items in specs.values()):
        raise ValueError("Every draft skill must have exactly three questions")

    lesson_by_skill = {lesson["skill_id"]: lesson for lesson in catalog["lessons"]}
    choice_counts = _runtime_choice_counts(bank)
    draft_questions: list[dict] = []
    for skill in [s for values in baseline["domains"].values() for s in values]:
        if skill not in g.LESSON_INFO or skill not in lesson_by_skill:
            raise ValueError(f"Missing lesson metadata for draft skill {skill}")
        for offset, (difficulty, spec) in enumerate(zip(DIFFICULTIES, specs[skill]), 4):
            draft_questions.append(_build_question(
                skill, domains[skill], difficulty, offset, spec, g.LESSON_INFO[skill], choice_counts))

    preview_catalog = copy.deepcopy(catalog)
    preview_bank = copy.deepcopy(bank)
    by_skill_difficulty = {(q["skill_id"], q["difficulty"]): q["id"] for q in draft_questions}
    for lesson in preview_catalog["lessons"]:
        for difficulty in DIFFICULTIES:
            lesson["practice_sets"][difficulty].append(by_skill_difficulty[(lesson["skill_id"], difficulty)])
    preview_bank["questions"].extend(copy.deepcopy(draft_questions))
    preview_bank["grade2_used_answer_kinds"] = sorted({q["answer_kind"] for q in preview_bank["questions"]})
    preview_bank["question_types"] = sorted({q["question_type"] for q in preview_bank["questions"]})
    return preview_catalog, preview_bank, draft_questions


def write_preview() -> tuple[dict, dict, list[dict]]:
    catalog, bank, draft = build_preview()
    PREVIEW_ROOT.mkdir(parents=True, exist_ok=True)
    PREVIEW_LESSONS.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    PREVIEW_QUESTIONS.write_text(json.dumps(bank, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return catalog, bank, draft


def main() -> None:
    catalog, bank, draft = write_preview()
    print(f"POOL6_DRAFT_PREVIEW_WROTE lessons={len(catalog['lessons'])} total_questions={len(bank['questions'])} draft_questions={len(draft)}")


if __name__ == "__main__":
    main()
