#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Semantic validator for Grade-2 Math static lesson/question content."""
from __future__ import annotations

import argparse
import difflib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_BASELINE = ROOT / "curriculum" / "math_grade2" / "moet_baseline_v1.json"
DEFAULT_LESSONS = ROOT / "content_packs" / "math_grade2_v1" / "lesson_catalog_v1.json"
DEFAULT_QUESTIONS = ROOT / "content_packs" / "math_grade2_v1" / "question_bank_v1.json"

ID_RE = re.compile(r"^[a-z0-9][a-z0-9_]{2,127}$")
DIFFICULTIES = {"basic", "medium", "application"}
ANSWER_KINDS = {"numeric_input", "multiple_choice"}


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

    for i, lesson in enumerate(lessons):
        where = f"lesson[{i}]"
        if not isinstance(lesson, dict):
            errors.append(f"not_object:{where}")
            continue
        lid = check_id(lesson.get("id"), where, errors)
        if lid:
            lesson_ids.add(lid); all_ids.append((lid, where)); lesson_by_id[lid] = lesson
        skill = required_text(lesson, "skill_id", where, errors)
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
        if len(required_list(lesson, "objectives_vi", where, errors, 2)) < 2:
            errors.append(f"insufficient_objectives:{where}")
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
            required_text(example, "answer", ewhere, errors)
            required_list(example, "solution_steps_vi", ewhere, errors)
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

    cycle = find_cycle(prereq_graph)
    if cycle:
        errors.append("prerequisite_cycle:" + "->".join(cycle))

    question_by_id: dict[str, dict] = {}
    question_counts_by_skill = Counter()
    question_counts_by_difficulty = Counter()
    prompts_by_lesson: dict[str, list[tuple[str, str]]] = defaultdict(list)

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
        explanation = required_text(q, "explanation_vi", where, errors)
        hints = required_list(q, "hints_vi", where, errors, 2)
        if len(hints) < 2 or any(not isinstance(x, str) or not x.strip() for x in hints):
            errors.append(f"invalid_hints:{where}")
        tags = required_list(q, "tags", where, errors, 3)
        if skill and skill.lower() not in tags:
            errors.append(f"missing_skill_tag:{where}:{skill.lower()}")
        if q.get("status") != "CHILD_READY":
            errors.append(f"question_not_child_ready:{where}")

        kind = q.get("answer_kind")
        if kind not in ANSWER_KINDS:
            errors.append(f"unsupported_answer_kind:{where}:{kind!r}")
            continue
        accepted = q.get("accepted_answers")
        if not isinstance(accepted, list) or not accepted or any(not isinstance(x, str) or not x.strip() for x in accepted):
            errors.append(f"missing_accepted_answer:{where}")
        validation = q.get("validation")
        if not isinstance(validation, dict):
            errors.append(f"missing_validation:{where}")
            validation = {}

        if kind == "numeric_input":
            answer = q.get("correct_answer")
            if type(answer) is not int:
                errors.append(f"numeric_answer_not_integer:{where}:{answer!r}")
            minimum = validation.get("numeric_min")
            maximum = validation.get("numeric_max")
            if type(minimum) is not int or type(maximum) is not int or minimum > maximum:
                errors.append(f"invalid_numeric_range:{where}:{minimum!r}:{maximum!r}")
            elif type(answer) is int and not (minimum <= answer <= maximum):
                errors.append(f"numeric_answer_out_of_range:{where}:{answer}:{minimum}:{maximum}")
            if validation.get("integer_required") is not True:
                errors.append(f"numeric_integer_required_missing:{where}")
            if isinstance(accepted, list) and type(answer) is int and str(answer) not in accepted:
                errors.append(f"numeric_answer_not_accepted:{where}:{answer}")
        elif kind == "multiple_choice":
            choices = q.get("choices")
            if not isinstance(choices, list) or not (2 <= len(choices) <= 5):
                errors.append(f"invalid_choice_count:{where}")
                choices = []
            choice_ids = []
            choice_texts = []
            for j, choice in enumerate(choices):
                cwhere = f"{where}.choice[{j}]"
                if not isinstance(choice, dict):
                    errors.append(f"choice_not_object:{cwhere}"); continue
                cid = required_text(choice, "id", cwhere, errors)
                text = required_text(choice, "text", cwhere, errors)
                required_text(choice, "rationale_vi", cwhere, errors)
                choice_ids.append(cid); choice_texts.append(text)
            if len(choice_ids) != len(set(choice_ids)):
                errors.append(f"duplicate_choice_id:{where}")
            if len(choice_texts) != len(set(choice_texts)):
                errors.append(f"duplicate_choice_text:{where}")
            correct = q.get("correct_answer")
            if correct not in choice_ids:
                errors.append(f"correct_choice_missing:{where}:{correct!r}")
            else:
                correct_text = choice_texts[choice_ids.index(correct)]
                if not isinstance(accepted, list) or correct_text not in accepted:
                    errors.append(f"correct_choice_text_not_accepted:{where}:{correct_text}")
            if validation.get("single_correct") is not True:
                errors.append(f"mc_single_correct_missing:{where}")
            if validation.get("choice_count") != len(choices):
                errors.append(f"mc_choice_count_metadata_mismatch:{where}")

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

    # Global ID uniqueness, including nested content entities.
    id_counts = Counter(x for x, _ in all_ids if x)
    for value, count in id_counts.items():
        if count > 1:
            locations = [where for x, where in all_ids if x == value]
            errors.append(f"duplicate_id:{value}:{count}:{'|'.join(locations)}")

    metrics = {
        "baseline_skills": len(baseline_skill_set),
        "chapters": len(chapters),
        "topics": len(topics),
        "lessons": len(lessons),
        "questions": len(questions),
        "valid_questions": len(questions) if not errors else max(0, len(questions) - sum(1 for e in errors if e.startswith("question["))),
        "difficulty_counts": dict(sorted(question_counts_by_difficulty.items())),
        "answer_kind_counts": dict(sorted(Counter(q.get("answer_kind") for q in questions if isinstance(q, dict)).items())),
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
