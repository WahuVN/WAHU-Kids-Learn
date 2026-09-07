# AI3 — MATH UI / QA / INTEGRATION AUDIT

Updated: 2026-09-07
Owner: AI3
Final common HEAD: `e728204`
Status: `LANE_DONE=YES`

## 1. Kết luận

AI3 Math UI/QA/integration lane đã hoàn tất strict board 3-AI trên cùng clean `main` HEAD `e728204`.

Final evidence:

- runtime Math bank: **402 questions / 67 lessons / 6 per lesson / 2 per difficulty**;
- Child UI: **2267 assertions PASS**;
- production Math validator: **402/402 valid**;
- core content smoke: **49/49 PASS**;
- pool-6 publish smoke: **8/8 PASS**;
- Math persistence: **378 assertions PASS** (`dotnet build` 0 warning / 0 error);
- full Build-Setup `0.1.43-dev`: **15/15 PASS**;
- Portable E2E: **PASS**;
- Installer/reinstall/uninstall E2E: **PASS**.

No P0/P1 remains inside AI3 Math scope.

## 2. Flow thật đã khóa

Adaptive:

`Home → Math Hub → adaptive mission → generated question → answer/retry/hint → result → Hub`.

Targeted lesson:

`Home → Math Hub → chapter/topic/lesson → theory/worked example → pool 6 → engine selects 3 (basic/medium/application) → answer/retry → result → persisted progress → Hub refresh/unlock`.

Resume:

`open selected question → suspend/close → relaunch → same lesson/session + same ordered selected set + exact open question/attempt → continue`.

Distribution:

`clean checkout → Build-Setup → staged payload V5 + 3 Math JSON → portable/installer → bootstrap → learner DB → reinstall preserves DB → uninstall preserves learner data`.

## 3. UI inventory final

| Hạng mục | Trạng thái |
|---|---|
| Math entry / Hub / 7 chapters / 17 topics / 67 lessons | DONE |
| Theory / objectives / concepts / worked examples | DONE |
| Lesson target + locked/unlocked + prerequisite explanation | DONE |
| Choice / typed / interaction answer surfaces | DONE |
| Hint / retry / first-try semantics | DONE |
| Exact retry-resume / corrupt-cache recovery | DONE |
| Write-failure recovery on choice/typed/interaction | DONE |
| Progress / lesson score / best score | DONE |
| Mastery delta / next lesson presentation | DONE |
| Keyboard / accessibility / responsive | DONE |
| Error / empty / corrupt reload / no stale state | DONE |
| Pool 6 vs session 3 presentation | DONE |
| Integer answer-unit presentation | DONE |
| 402 authored answer-surface sweep | DONE |
| Portable + Installer Math payload E2E | DONE |
| Numeric XP | NO FIRST-CLASS PRODUCT CONTRACT — UI không invent |

## 4. Key AI3 waves

- `c63e110`: interactive segment answer control.
- `11d7914`: real Math Hub/catalog/theory/worked example.
- `2d2813c`: continue + exact resume.
- `37f0b97` / `b6a1ce4`: durable result + correct route back to Math Hub.
- `1436705`: targeted lesson, prerequisite UI, typed authored input.
- `3905c09`: mastery delta + next lesson presentation.
- `50fa4c0`: first 67-lesson access/detail sweep.
- `a06d1a3`: retry same question / first-try semantics.
- `c5cb9da`: exact retry-resume UI regression.
- `12cb5ee`: recoverable answer-write failure UI.
- `09c6551`: pool-size/session-size decoupling.
- `82629a8`: remove adaptive count hard-code.
- `ef3d35a`: real generated segment → UI integration.
- `9c35ce5`: valid→corrupt reload clears visible/internal stale state.
- `67c350e`: accessibility sweep all 67 lessons and missing prerequisites.
- `2bb1285`: clear stale Continue accessible name on catalog failure.
- `f567be8`: Request 009 real selected-set UI E2E.
- `3c0cfaa`: Request 006 answer-unit UI E2E.
- `73d4c93`: release ContentRuntime version guard follows runtime `PackVersion`.
- `e728204`: full 402-bank Child UI compatibility + release evidence 2267.

## 5. 402-question answer-surface audit

Current runtime distribution verified by UI loader/render path:

- **218 typed**;
- **182 choice**;
- **2 interaction**;
- total **402/402 renderable**.

The sweep calls `ConfigureAnswerInput()` for every authored runtime question and verifies each surface is usable. Old 201-bank exact assumptions were removed after `68145ea` published the 402-bank.

## 6. 67-lesson accessibility/access audit

For every lesson:

- navigation button has lesson-specific `AccessibleName` and non-empty description;
- keyboard-open behavior is announced;
- detail renders lesson title/objective/example/practice count;
- practice CTA accessibility exists;
- CTA `Enabled` equals engine-owned `MathLessonAccessSnapshot.IsUnlocked`;
- every locked lesson names **all** unsatisfied prerequisite lesson titles in accessibility text.

Valid→corrupt reload regression additionally requires:

- catalog/access/navigation caches clear;
- stale visible controls clear;
- detail hides;
- Continue disables and resets accessible name/description;
- adaptive mission remains available as safe fallback.

## 7. Request 009 — pool 6 / selected session 3

Engine contract: `05cdb2a`; selected-set self-heal: `3b8a10b`; runtime 402 publish: `68145ea`; UI E2E: `f567be8`.

AI3 verifies:

- Hub reports bank size 6;
- CTA/badge/accessibility do not claim session size 6;
- targeted start selects exactly 3 unique IDs, one per difficulty;
- `_targetQuestionCount` and progress use 3;
- q1 → q2 suspend → new form resumes exact ordered selected set and exact q2 instance;
- q3 completes lesson; no fourth question;
- result/progress persists from selected 3;
- completed Hub still reports bank 6 with neutral `Luyện lại bài này` CTA.

## 8. Request 006 — integer answer unit

Engine: `3cf7fc6`; AI3: `3c0cfaa`.

Regression uses `m2_ls_time_day_24_hours`:

- `AnswerKind=integer`, `AnswerUnit=giờ`;
- raw correct display/input is `24`;
- feedback display is `24 giờ`;
- `24` validates; `24 giờ` does not silently widen integer grading;
- typed guidance does not demand a unit;
- suspend/resume preserves exact question + `AnswerUnit`;
- final wrong feedback displays `Đáp án đúng: 24 giờ`.

## 9. Request 008 — full distribution closure

Guards:

- `109ee5d`: Portable/Installer required lists;
- `490fd41`: source + staged payload required lists;
- `73d4c93`: manifest/runtime version contract;
- `e728204`: current Child UI release evidence.

Artifact `0.1.43-dev`, clean `e728204`:

- release manifest schema 5, publish file count 55, Child UI evidence 2267;
- staged payload includes 3 Math runtime JSON and migrations `001..005`;
- full release smokes PASS: SetupPreflight 42, Behavior 15, LearningSession 800, Motion 25, Child UI 2267, Content 21, Security 19, Audio 14, Performance 13, Update 33, SQLite 179;
- Portable SHA256 `210D073E1ED6A7A2AAE12EEFEF9DCC629F22DB6D3A1BEABB5D828524C4428806`;
- Installer SHA256 `A379B31DA947EB5DD42B17C929E2E2CDCFB0E6D9A79007E34085B754FC506F3F`.

Portable E2E:

- first bootstrap exit 0;
- second bootstrap exit 0;
- portable DB created under portable storage;
- no installed learner-data mutation.

Installer E2E:

- install/preflight/bootstrap/recovery exit 0;
- config tamper exit 42 fail-closed;
- restored bootstrap exit 0;
- reinstall exit 0;
- learner DB hash identical before/after reinstall;
- startup disabled state preserved on reinstall;
- uninstall exit 0, app removed, learner DB + sentinel preserved, startup registry removed;
- owned test data cleaned only after `.wahu-e2e-owned` marker verification.

## 10. Final cross-lane gate

All on clean `e728204`:

- production validator: **402/402 valid**;
- `MathContentDataSmoke`: **49/49 PASS**;
- pool-6 publish smoke: **8/8 PASS**;
- `MathSessionPersistenceRuntimeSmoke`: **378 assertions PASS**;
- Child UI: **2267 assertions PASS**;
- full Build-Setup: **PASS**;
- Portable E2E: **PASS**;
- Installer E2E: **PASS**;
- `git diff --check`: **PASS**.

AI1 `LANE_DONE=YES`; AI2 `LANE_DONE=YES`; AI3 `LANE_DONE=YES`.

## 11. Remaining outside AI3 Math scope

No remaining Math UI/QA/integration blocker. The following are release-wide application gates, not Math blockers:

- production Authenticode signing;
- compatibility execution on a real Windows 7 machine.

`0.1.43-dev` is an unsigned dev artifact and must not be represented as a production-signed release.
