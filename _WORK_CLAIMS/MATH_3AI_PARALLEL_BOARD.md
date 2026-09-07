# MATH — 3 AI PARALLEL BOARD (READ-ONLY)

Baseline after coordination: `3d8674d` (`Toán: chia 3 lane AI chạy song song không xung đột`).

Purpose: keep AI1/AI2/AI3 productive in parallel on the same working tree without overwriting, staging or committing another lane's work. This file is coordinator-owned and **READ-ONLY for AI1/AI2/AI3**. Each AI updates only its own `AI*_PARALLEL_NOW.md` status file.


## 0. NO-WAIT policy — 3 AI chạy cùng lúc liên tục

**Không AI nào được đứng chờ dependency của AI khác.** Mọi dependency chỉ chặn bước *publish/merge vào runtime*, không chặn công việc. Nếu gặp lock/handoff chưa mở, AI phải lập tức chuyển sang task độc lập kế tiếp trong backlog của chính lane.

- AI1 không chờ Request 009: làm **shadow pool-6 hoàn chỉnh** trong draft-only path, validator/test riêng, preview catalog/bank riêng. Chỉ bước copy/merge vào runtime `question_bank_v1.json` + `lesson_catalog_v1.json` mới phụ thuộc AI2.
- AI2 không chờ AI1 pool thật: Request 009 được phát triển/test bằng **synthetic >=6 pool fixture** trong engine tests. Request 010, adaptive generator và Request 006 đều độc lập với AI1 publish.
- AI3 không chờ AI2: UI target hard-code, Request 008 portable/installer lists, V5 status cleanup, accessibility/Child UI regression, synthetic pool-6 UI E2E đều làm ngay. Chỉ bước sửa `Build-SetupArtifacts.ps1` và real-bank pool6 E2E phụ thuộc handoff; khi chưa mở thì chuyển task khác.

Mỗi lane dùng queue `NOW -> NEXT -> FALLBACK`. Khi task NOW bị khóa bởi file-owner khác, **không đợi**: chạy NEXT/FALLBACK.

## 1. Non-negotiable shared-tree rules

1. Never run `git reset`, `git checkout -- <file>`, `git restore`, `git clean`, `git stash`, `git rebase` or broad rollback in the shared working tree.
2. Before editing any owned file, run `git status --short -- <file...>`. If a file is already modified and is not explicitly listed as owned by your lane below, do not edit it.
3. Never use `git add -A` or `git add .`. Commit with `git commit --only -- <explicit file list>` or equivalent exact staging.
4. One concern per commit. Push immediately after a passing gate. Do not bundle another AI's WIP into your commit.
5. Shared contract docs are read-only during the parallel run: `MATH_SHARED_CONTRACT_REQUESTS.md`, `MATH_SHARED_CONTRACT_CHANGES.md`. Put handoff evidence in your own lane status file instead.
6. `content_packs/math_grade2_v1/manifest.json` is a **single-writer handoff file**. Current lock: AI2 until its template/generator wave is committed. AI1 may touch it only after AI2 writes `MANIFEST_LOCK=FREE` in `AI2_PARALLEL_NOW.md`. AI3 never edits it.
7. `tools/build/Build-SetupArtifacts.ps1` is also a **single-writer handoff file**. Current lock: AI2 because it already contains the pending LearningSession assertion-count update. After AI2 commits that WIP and writes `BUILD_SETUP_LOCK=AI3`, AI3 owns it for Request 008.
8. Same worktree means commits from one AI become visible immediately. Do not `pull --rebase` while another lane has uncommitted work. If push is rejected, inspect `git log/status` first; never solve it by resetting others.

## 2. Current lock snapshot

### AI1 — CONTENT/DATA
Owned now and currently clean after `d1665dc`:
- `tools/math_content_authoring/**` except files explicitly created by another lane
- `tools/math_content_validator/**`
- `tests/MathContentDataSmoke/**`
- `content_packs/math_grade2_v1/lesson_catalog_v1.json`
- `content_packs/math_grade2_v1/question_bank_v1.json`
- `_WORK_CLAIMS/AI1_MATH_CONTENT_STATUS.md`
- `_WORK_CLAIMS/AI1_PARALLEL_NOW.md`

Temporary no-touch:
- `content_packs/math_grade2_v1/manifest.json` until AI2 releases lock
- `content_packs/math_grade2_v1/verified_templates_v1.json` (AI2 generator/template lane)

### AI2 — ENGINE/SESSION
Current WIP lock:
- `src/Learning/AdaptiveMathSelector.cs`
- `src/Learning/MathAnswerValidator.cs`
- `src/Learning/MathLearningModels.cs`
- `src/Learning/MathQuestionGenerator.cs`
- `src/Session/MathAuthoredQuestionSource.cs`
- `src/Session/MathSessionCoordinator.cs`
- `tests/LearningSessionRuntimeSmoke/Program.cs`
- engine/session persistence tests needed by Requests 006/009/010
- `content_packs/math_grade2_v1/verified_templates_v1.json`
- `content_packs/math_grade2_v1/manifest.json` until generator/template wave lands
- `_WORK_CLAIMS/AI2_MATH_ENGINE_STATUS.md`
- `_WORK_CLAIMS/AI2_PARALLEL_NOW.md`
- `tools/build/Build-SetupArtifacts.ps1` **only until current one-line assertion-count WIP is committed**

AI2 must not edit AI1 question bank/catalog or AI3 UI/release-E2E files.

### AI3 — UI/RELEASE/INTEGRATION
Owned now; these files are clean at coordination time:
- `src/App/MathHubForm.cs`
- `src/App/MathLessonForm.cs`
- `tests/ChildUiRuntimeSmoke/Program.cs`
- `tools/build/Test-PortableE2E.ps1`
- `tools/build/Test-InstallerE2E.ps1`
- `_WORK_CLAIMS/MATH_LIVE_STATUS.md`
- `_WORK_CLAIMS/AI3_MATH_UI_AUDIT.md`
- `_WORK_CLAIMS/AI3_PARALLEL_NOW.md`

Deferred ownership:
- `tools/build/Build-SetupArtifacts.ps1` only after AI2 explicitly hands it off.

AI3 must not edit `src/Learning/**`, `src/Session/**`, Math runtime content JSON, or the manifest.

## 3. Parallel execution graph

### Phase P0 — chạy NGAY đồng thời, không dependency

**AI1:** prepare authored breadth expansion without changing runtime bank yet. Curate one additional `basic`, `medium`, `application` question per skill (201 new draft questions total) in new draft-only source under `tools/math_content_authoring/drafts/`; add draft-only validation/tests. IDs must be future runtime IDs `_04`, `_05`, `_06`. Do not import the draft into `generate_grade2_content.py` yet. Continue independent content audits only in AI1-owned files.

**AI2:** first finish Request 010 as a small isolated commit: preserve/enforce per-question expression operators and add mandatory regression (`75`, `100 - 30 + 5`, `70 + 5` PASS; `15*5`, `150/2` FAIL; metadata survives runtime instance/resume). Do not include generator/template WIP in that commit. Then land the already-working adaptive `draw_segment_given_length` generator/template wave as a second commit.

**AI3:** fix adaptive mission UI hard-code (`Luyện 8 câu hôm nay`, badge/accessibility wording) to derive from `MathSessionCoordinator.DefaultTargetQuestionCount`; add Child UI regression. In parallel, update portable/installer required payload lists for Request 008 to require `verified_templates_v1.json`, `lesson_catalog_v1.json`, `question_bank_v1.json`. Do not touch `Build-SetupArtifacts.ps1` yet.

### Phase P1 — tiếp tục đồng thời; dependency chỉ quyết định publish, không quyết định có việc làm hay không

**AI1:** finish draft pool quality gates: every skill exactly 3 draft questions (1 per difficulty), no prompt near-duplicate against runtime 01..03 or other draft questions, Grade-2 operation/range/unit guards, deterministic `_04..06` IDs. Publish only draft artifacts/tests.

**AI2:** implement Request 009 first-class selected authored set: pool may be >=6, targeted session target remains 3, select exactly 1 basic + 1 medium + 1 application deterministically per selection identity, persist ordered selected `ContentQuestionId` set across suspend/resume/retry/corrupt-cache recovery, completion still 3 attempts. Use synthetic pool >=6 tests; do not alter AI1 runtime bank.

**AI3:** after AI2 hands off `Build-SetupArtifacts.ps1`, finish Request 008 preflight/staging hard guards for all three Math runtime JSON and run portable/installer payload tests. Also keep UI tests parameterized from engine constants; no numeric target hard-code.

### Sync S1 — Request 009 chỉ là publish gate, KHÔNG phải wait gate

AI2 writes `REQUEST_009_READY=<commit>` in `AI2_PARALLEL_NOW.md` only after its synthetic pool >=6 persistence/regression suite passes.

Khi marker xuất hiện, AI1 chuyển từ shadow/draft sang publish runtime:
- AI1 merges the draft 201 questions into the runtime authoring pipeline, yielding 402 authored questions / 6 per lesson / 2 per difficulty, updates catalog practice sets, validator/tests and final manifest hash after manifest lock is free.
- AI3 runs real-bank E2E: pool=6 but targeted session/progress/result remains 3; fresh selection identities can produce different valid sets; resume/retry preserves set.

### Permanent fallback backlog — dùng ngay nếu task chính chạm lock

**AI1 fallback:** audit/curate shadow pool quality, distractor/hint/explanation uniqueness, difficulty progression, prerequisite/source/ID/Unicode/readability guards, draft deterministic regeneration, content preview statistics. Không chạm engine/UI.

**AI2 fallback:** fuzz MathAnswerValidator, generated-template coverage, selector determinism, retry/idempotency/corrupt-cache/persistence stress, schema-V5 pack identity, synthetic selected-set permutations. Không chạm UI/content runtime bank.

**AI3 fallback:** accessibility strings, no-hard-code reflection tests, 67-lesson render sweep, stale-state/error-state UI, portable/installer required-file tests, status-doc cleanup, synthetic fixtures for pool 6 / display unit. Không chạm engine/content runtime bank.

### Phase P2 — final closure

**AI2:** Request 006 `answer_unit` display metadata: preserve through loader/model/runtime/resume without changing integer grading; regression for raw `8` accepted + display `8 cm`.

**AI3:** consume/display Request 006 if UI needs explicit formatting; otherwise add presentation E2E only. Update stale V5 status text now that `ece2a0c` is stable.

**AI1:** final static validator + deterministic regeneration + content hash/manifest verification on 402-bank; no engine/UI edits.

## 4. Required end-state gates

- Content validator: 0 errors; all AI1 tests PASS; runtime bank 67 lessons × 6 questions = 402, exactly 2 per difficulty per lesson.
- Request 010: add/sub expression accepts numeric/equivalent add-sub forms and rejects multiply/divide equivalents.
- Request 009: synthetic and real pool >=6 -> session target exactly 3, selected set durable through resume/retry/corrupt recovery.
- Request 006: 23 integer `answer_unit` questions keep raw integer grading while correct-answer display includes unit.
- Adaptive generated `draw_segment_given_length`: `interaction_integer`, zero fake choices, engine + UI smoke PASS.
- Adaptive UI mission count derives from `DefaultTargetQuestionCount`, no literal `8/tám` coupling.
- Request 008: staged/portable/installer all hard-require the three Math runtime JSON; schema remains V5.
- Production Release/x86 + Math engine/data/session/SQLite/Learning/ChildUI/content + portable/installer E2E all PASS.

## 5. Completion rule

No AI declares Math done based only on its own suite. Final closure requires all three lane status files to contain `LANE_DONE=YES` and the final cross-lane gate above to pass on one common `main` HEAD.
