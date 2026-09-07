# AI1 — PARALLEL NOW — CONTENT/DATA

LANE: AI1
LANE_DONE=NO
BASELINE=d1665dc
REQUEST_009_READY=WAIT
MANIFEST_LOCK=WAIT_AI2

## Mission now

Work only in AI1-owned content/data/authoring/validator files. Do not touch Learning/Session/UI/release files.

### A. Immediate independent work
1. Create draft-only breadth source under `tools/math_content_authoring/drafts/` with exactly 201 extra authored questions: one new basic, medium and application question for each of 67 skills. Reserve IDs `_04`, `_05`, `_06`.
2. Draft questions must not be imported into the runtime bank yet. Current runtime bank stays 201 until AI2 publishes Request 009.
3. Add draft-only tests/validator proving:
   - 67 skills covered;
   - exactly 3 draft questions per skill and one per difficulty;
   - no near-duplicate prompt against runtime 01..03 or within draft;
   - Grade-2 max-number, tables 2/5, carry/borrow, time, unit, money and answer-surface rules remain valid;
   - stable future IDs fit existing contiguous `01..N` contract after merge.
4. Continue independent content quality audit if draft is complete; only fix content with concrete evidence.

### B. Handoff after AI2 Request 009
Only after `AI2_PARALLEL_NOW.md` contains `REQUEST_009_READY=<sha>`:
1. Merge draft questions into `generate_grade2_content.py`/runtime bank and lesson practice sets.
2. Target final bank: 402 questions, 6 per lesson, 2 basic + 2 medium + 2 application.
3. Keep every question referenced exactly once and no orphan IDs.
4. Regenerate deterministically and run full content tests.
5. Touch manifest only if `MANIFEST_LOCK=FREE`; use the safe HEAD-manifest candidate technique and change only AI1-owned file hashes.

### Commit discipline
Use `git commit --only` with AI1 file list. Never stage `manifest.json` while lock is held. Never stage `verified_templates_v1.json`. Push every passing wave.
