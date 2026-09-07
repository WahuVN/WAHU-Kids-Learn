# AI1 — PARALLEL NOW — CONTENT/DATA

LANE: AI1
LANE_DONE=NO
BASELINE=3d8674d
REQUEST_009_READY=NOT_REQUIRED_FOR_SHADOW_WORK
MANIFEST_LOCK=LOCKED_BUT_NO_WAIT


## NO-WAIT rule
AI1 **không chờ AI2**. Cho tới khi Request 009 ready, toàn bộ pool-6 được hoàn thiện dưới draft/shadow path riêng; nếu draft xong thì tiếp tục quality audit + preview regeneration + metrics. Chỉ runtime publish bị khóa.

### Queue luôn có việc
- NOW: hoàn thiện 201 câu draft `_04/_05/_06`.
- NEXT: tạo shadow preview 402-question + shadow lesson practice sets dưới `tools/math_content_authoring/drafts/pool6_preview/`, không đụng runtime JSON.
- NEXT: draft validator/test độc lập, deterministic rebuild, no-near-duplicate, difficulty/progression, answer/hint/rationale/unit/range guards.
- FALLBACK: audit content wording/difficulty/source/prerequisite/readability và thêm fail-closed gates chỉ trong AI1-owned files.

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
Runtime publish only — đây là publish gate, không phải wait gate. Khi `AI2_PARALLEL_NOW.md` chứa `REQUEST_009_READY=<sha>`:
1. Merge draft questions into `generate_grade2_content.py`/runtime bank and lesson practice sets.
2. Target final bank: 402 questions, 6 per lesson, 2 basic + 2 medium + 2 application.
3. Keep every question referenced exactly once and no orphan IDs.
4. Regenerate deterministically and run full content tests.
5. Touch manifest only if `MANIFEST_LOCK=FREE`; use the safe HEAD-manifest candidate technique and change only AI1-owned file hashes.

### Commit discipline
Use `git commit --only` with AI1 file list. Never stage `manifest.json` while lock is held. Never stage `verified_templates_v1.json`. Push every passing wave.
