#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Strict validation for the AI1 shadow pool-6 Math preview.

The production validator remains unchanged. Its current structured-distractor classifier
knows the 201-question runtime prompt families; draft-only new prompt families use an
explicit fallback rationale that this wrapper validates instead of weakening production
rules. Every other production-validator error remains fatal.
"""
from __future__ import annotations

import importlib.util
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DRAFT_ROOT = ROOT / "tools" / "math_content_authoring" / "drafts"
PREVIEW_BUILDER = DRAFT_ROOT / "generate_pool6_preview.py"
PREVIEW_LESSONS = DRAFT_ROOT / "pool6_preview" / "lesson_catalog_pool6_preview.json"
PREVIEW_QUESTIONS = DRAFT_ROOT / "pool6_preview" / "question_bank_pool6_preview.json"
RUNTIME_LESSONS = ROOT / "content_packs" / "math_grade2_v1" / "lesson_catalog_v1.json"
RUNTIME_QUESTIONS = ROOT / "content_packs" / "math_grade2_v1" / "question_bank_v1.json"
BASELINE = ROOT / "curriculum" / "math_grade2" / "moet_baseline_v1.json"
PRODUCTION_VALIDATOR = ROOT / "tools" / "math_content_validator" / "validate_math_content.py"
DIFFICULTIES = ("basic", "medium", "application")
DIFFICULTY_ORDINAL = {"basic": 4, "medium": 5, "application": 6}
MISSING_REASON_RE = re.compile(r"^missing_choice_specific_diagnosis:question\[(\d+)\]:([^:]+):(.*)$")


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, str(path))
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load module: {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def validate_fallback_rationale(error: str, bank: dict, errors: list[str]) -> None:
    match = MISSING_REASON_RE.match(error)
    if not match:
        fail(errors, f"unparsed_production_validator_error:{error}")
        return
    question_index = int(match.group(1))
    choice_id = match.group(2)
    questions = bank.get("questions") or []
    if question_index < 0 or question_index >= len(questions):
        fail(errors, f"fallback_question_index_out_of_range:{question_index}")
        return
    question = questions[question_index]
    choices = question.get("choices") or []
    choice = next((item for item in choices if item.get("id") == choice_id), None)
    if choice is None:
        fail(errors, f"fallback_choice_missing:{question.get('id')}:{choice_id}")
        return

    choice_text = str(choice.get("text", ""))
    correct_text = str(question.get("correct_answer", ""))
    explanation = " ".join(str(question.get("explanation_vi", "")).split())
    rationale = " ".join(str(choice.get("rationale_vi", "")).split())
    prefix = f"“{choice_text}” chưa đúng. Dữ kiện dẫn tới “{correct_text}”."
    if not rationale.startswith(prefix):
        fail(errors, f"fallback_rationale_missing_explicit_contrast:{question.get('id')}:{choice_id}")
    if explanation and explanation not in rationale:
        fail(errors, f"fallback_rationale_missing_full_explanation:{question.get('id')}:{choice_id}")
    if len(rationale) > 300:
        fail(errors, f"fallback_rationale_too_long:{question.get('id')}:{choice_id}:{len(rationale)}")


def validate() -> tuple[list[str], dict]:
    errors: list[str] = []
    builder = load_module("math_pool6_preview_builder_for_validation", PREVIEW_BUILDER)
    production = load_module("math_content_validator_for_pool6_draft", PRODUCTION_VALIDATOR)

    runtime_catalog = json.loads(RUNTIME_LESSONS.read_text(encoding="utf-8"))
    runtime_bank = json.loads(RUNTIME_QUESTIONS.read_text(encoding="utf-8"))
    committed_catalog = json.loads(PREVIEW_LESSONS.read_text(encoding="utf-8"))
    committed_bank = json.loads(PREVIEW_QUESTIONS.read_text(encoding="utf-8"))
    built_catalog, built_bank, draft_questions = builder.build_preview()

    if built_catalog != committed_catalog:
        fail(errors, "preview_catalog_not_deterministic_or_stale")
    if built_bank != committed_bank:
        fail(errors, "preview_question_bank_not_deterministic_or_stale")

    if len(runtime_catalog.get("lessons") or []) != 67:
        fail(errors, f"runtime_lesson_count_changed:{len(runtime_catalog.get('lessons') or [])}")
    if len(runtime_bank.get("questions") or []) != 201:
        fail(errors, f"runtime_question_count_changed_before_request009:{len(runtime_bank.get('questions') or [])}")
    if len(committed_catalog.get("lessons") or []) != 67:
        fail(errors, f"preview_lesson_count:{len(committed_catalog.get('lessons') or [])}")
    if len(committed_bank.get("questions") or []) != 402:
        fail(errors, f"preview_question_count:{len(committed_bank.get('questions') or [])}")
    if len(draft_questions) != 201:
        fail(errors, f"draft_question_count:{len(draft_questions)}")

    if committed_bank.get("questions", [])[:201] != runtime_bank.get("questions", []):
        fail(errors, "preview_runtime_prefix_changed")

    draft_by_skill: dict[str, list[dict]] = defaultdict(list)
    for question in draft_questions:
        draft_by_skill[str(question.get("skill_id"))].append(question)
    if len(draft_by_skill) != 67:
        fail(errors, f"draft_skill_count:{len(draft_by_skill)}")
    for skill, questions in sorted(draft_by_skill.items()):
        if len(questions) != 3:
            fail(errors, f"draft_questions_per_skill:{skill}:{len(questions)}")
            continue
        difficulties = Counter(str(q.get("difficulty")) for q in questions)
        if difficulties != Counter({difficulty: 1 for difficulty in DIFFICULTIES}):
            fail(errors, f"draft_difficulty_mix:{skill}:{dict(difficulties)}")
        for question in questions:
            expected_ordinal = DIFFICULTY_ORDINAL.get(str(question.get("difficulty")))
            expected_suffix = f"_{expected_ordinal:02d}" if expected_ordinal else None
            if not expected_suffix or not str(question.get("id", "")).endswith(expected_suffix):
                fail(errors, f"draft_id_ordinal_mismatch:{question.get('id')}:{question.get('difficulty')}")

    all_questions = committed_bank.get("questions") or []
    ids_by_skill: dict[str, list[str]] = defaultdict(list)
    for question in all_questions:
        ids_by_skill[str(question.get("skill_id"))].append(str(question.get("id")))
    for skill, ids in sorted(ids_by_skill.items()):
        suffixes = sorted(int(qid.rsplit("_", 1)[1]) for qid in ids)
        if suffixes != [1, 2, 3, 4, 5, 6]:
            fail(errors, f"non_contiguous_pool6_ordinals:{skill}:{suffixes}")

    reference_counts = Counter()
    for lesson in committed_catalog.get("lessons") or []:
        practice = lesson.get("practice_sets") or {}
        for difficulty in DIFFICULTIES:
            refs = practice.get(difficulty) or []
            if len(refs) != 2:
                fail(errors, f"preview_practice_count:{lesson.get('id')}:{difficulty}:{len(refs)}")
            for qid in refs:
                reference_counts[str(qid)] += 1
    for question in all_questions:
        qid = str(question.get("id"))
        if reference_counts[qid] != 1:
            fail(errors, f"preview_question_reference_count:{qid}:{reference_counts[qid]}")

    production_errors, metrics = production.validate(BASELINE, PREVIEW_LESSONS, PREVIEW_QUESTIONS)
    fallback_count = 0
    for error in production_errors:
        if error.startswith("missing_choice_specific_diagnosis:"):
            fallback_count += 1
            validate_fallback_rationale(error, committed_bank, errors)
        else:
            fail(errors, f"production_validator:{error}")

    metrics = dict(metrics)
    metrics.update({
        "runtime_questions": len(runtime_bank.get("questions") or []),
        "draft_questions": len(draft_questions),
        "preview_questions": len(all_questions),
        "preview_lessons": len(committed_catalog.get("lessons") or []),
        "draft_fallback_rationales": fallback_count,
    })
    return errors, metrics


def main() -> int:
    errors, metrics = validate()
    if errors:
        print(f"MATH_POOL6_DRAFT_VALIDATE_FAIL errors={len(errors)}")
        for error in errors:
            print(error)
        return 1
    print("MATH_POOL6_DRAFT_VALIDATE_PASS " + " ".join(f"{key}={value}" for key, value in sorted(metrics.items())))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
