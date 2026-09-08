# AI3 — MATH UI / QA / INTEGRATION AUDIT

Updated: 2026-09-08
Owner: AI3
Last fully verified release HEAD: `b45dc56`
Latest UI/performance verified HEAD: `594f4ba`
Latest persistence verified HEAD: `594f4ba`
Status: `LANE_DONE=YES`; `PLAYABLE_UI_P0=GREEN`; `INSTALLER_E2E=BLOCKED_SAFETY_UNOWNED_LOCAL_DATA`

## PLAYABLE EVENT V1 — current evidence

- Post-closure visual wave `468dd55` adds richer Home/Hub/lesson/rescue cards and button depth, procedural rescue hero/feedback/Garden art, improved typed-answer surface and child-facing completion feedback. Runtime/session/content semantics were not changed.
- Motion hardening `af48f89` binds the new art to the existing performance contract: LOW decorative motion stays off, NORMAL respects its FPS cap, and learning-focus feedback stays static. `a0344f4` tracks the parent visibility hierarchy; `dcf844b` additionally closes detach/re-attach lifecycle so controls removed from the visual tree stop animation timers and resume only after being attached again, with subscriptions still disposed cleanly.
- Integration intentionally retained the newer remote accessibility/lifecycle guards: locked rescue missions stay keyboard-focusable for prerequisite explanation, Enter/Esc remain wired, exact resume and failure-safe status remain intact, and 900×640 plus 125% DPI regressions still pass.
- Latest QA gate on `594f4ba`: Child UI **3743 assertions PASS**, offscreen capture **10 PNG / 3774 assertions PASS**, Performance **13 assertions PASS**, Motion **25 assertions PASS**, Math persistence **8068 assertions PASS**, `git diff --check` PASS. Full Build-Setup release gate remains `b45dc56`.

- Home/Math Hub có entry **Toán nhanh — Nhiệm vụ cứu hộ**; 5 event production cho 5 bài đầu.
- Flow UI E2E khóa: intro → 3 checkpoint → cố tình sai → repair/hint → suspend khi retry pending → resume exact event/session/open question → hoàn thành → Garden tăng đúng 1 lần.
- Behavior presentation FLOW / STRAINED / FRUSTRATED / FATIGUED đọc public action DTO của engine; không tự suy state.
- Missing/corrupt event metadata fail-safe về lesson presentation, không mất session/progress.
- Mission cards + checkpoint cards có accessibility và fit 900×640; banner **KHÔNG ĐẾM NGƯỢC** được regression bảo vệ.
- Mission khóa vẫn focus được để nghe đúng prerequisite nhưng không thể start; Enter/Esc wired; shell ưu tiên đúng lesson session đang dở.
- Rescue shell khóa valid→corrupt→valid recovery; dynamic AccessibleName/Description đồng bộ đúng Bắt đầu/Tiếp tục/Chưa thể.
- Khi mở rescue lesson gặp exception, visible status và screen-reader status cùng báo phần học đã lưu an toàn; shell không văng khỏi flow.
- Lifecycle UI khóa thêm trạng thái 3/3 đã trả lời nhưng chưa complete: suspend không grant reward; reopen tự complete không tạo câu 4 và Garden chỉ +1 sau completion.
- Garden accounting `88ecf34` khóa thêm zero-attempt terminal: session `completed` nhưng chưa có attempt không được tính completed milestone, không được reward/backfill; valid rescue completion vẫn +1 đúng một lần.
- Reward boundary `9736443` không tin caller attempt count: chỉ durable attempt trong DB mới cho phép Garden reward.
- Completion boundary `478e89b` chặn `Complete()` khi chưa có attempt hoặc targeted lesson chưa đủ selected questions; rejected call giữ session active/exact open question, không tăng progress/reward.
- Build/release `e3bc9e0` đưa persistence smoke vào chính Build-Setup `10b/15` và lưu assertion count trong release manifest.
- Garden regression `7496388` khóa exact milestone cho cả 5 rescue production: seedling #1, flower patch #3, lantern vẫn khóa sau #5.
- Release integration `3af8d4a` đưa production validator + full 90-test content/event/pool suite + Portable E2E vào Build-Setup và manifest.
- Provenance `f4bb08a` fail-closed dirty tracked/untracked source, khóa HEAD stable và clean tree trước/sau build; Python chạy `-B` nên không để lại cache làm bẩn worktree.
- Reward eligibility `069372d` yêu cầu durable Math attempt thuộc đúng child/session; foreign-child attempt không thể tạo Garden reward.
- Reward canonicalization `85a1d2a` tự sửa stale same-child `source_key` collision và loại orphan/non-canonical reward khỏi `GrowthSteps`.
- Milestone regression `0e91702` khóa chính xác/replay-safe các mốc Garden 1/3/6/10.
- Post-installer provenance `f5e5202` kiểm HEAD/tree thêm lần cuối sau installer + update-manifest trước `BUILD_SETUP_ARTIFACTS_PASS`.
- Garden consistency `1c4ce9e` derive milestone/visual từ canonical reward count, bỏ raw inventory trust và reconcile stale/missing milestone inventory; partial reward outage không thể mở mốc tương lai sớm.
- Progress chronology/score `d6002a9` rejects malformed/missing/inverted completion chronology, skill/count mismatch and stale score-only evidence; effective completion/score therefore fail closed and fresh start repairs untrusted metadata.
- UI/rescue regression `b45dc56` injects malformed completion timestamp into bài 1 and verifies Hub hides false completion/scores, bài 2 stays locked, rescue #2 remains focusable for prerequisite narration but cannot start.
- Gameplay rescue đã được scale 125% trong chính active session: prompt vẫn fit từ 12pt trở lên, checkpoint/break target giữ kích thước thao tác và wrong→retry→hint→suspend vẫn chạy sau scale.
- Latest UI/performance/persistence on `594f4ba`: Child UI **3743 assertions PASS**, offscreen capture **10 PNG / 3774 assertions PASS**, Performance **13 assertions PASS**, Motion **25 assertions PASS**, persistence **8068 assertions PASS**. Most recent Math content/event/pool suite remains **90/90 PASS** with no content files changed by the visual/motion waves.
- `0.1.80-dev`: Build-Setup **15/15 PASS** + provenance/content/persistence/Portable/post-installer gates, installer compile **PASS**; publish payload chứa `game_events_v1.json` SHA `F9F25EA94DA7FCD20E360509EE53E6E758039CCF788FAFEC729A35F80D39A8B1`.
- Full Installer E2E hiện **BLOCKED_SAFETY** vì `%LOCALAPPDATA%\WAHU Kids Learn\data\learning.db` tồn tại nhưng không có `.wahu-e2e-owned`; không được xóa/ghi đè dữ liệu này để ép test qua.
- Production signing và real Windows 7 validation vẫn `UNAVAILABLE/PENDING`.

## 1. Kết luận

AI3 Math UI/QA/integration lane đã hoàn tất playable P0/P1 cho 5 bài đầu và đạt STOP RULE. Latest full release gate chạy trên clean common HEAD `b45dc56` với artifact `0.1.80-dev`; lane giữ `LANE_DONE=YES`.

Current evidence:

- runtime Math bank: **402 questions / 67 lessons / 6 per lesson / 2 per difficulty**;
- Child UI latest common-head: **3743 assertions PASS**;
- production Math validator: **402/402 valid**;
- full Math content/event/pool suite: **90/90 PASS**;
- difficulty distribution: **134 basic / 134 medium / 134 application**;
- Math persistence latest: **8068 assertions PASS** on `594f4ba`; Performance **13 assertions PASS** and Motion **25 assertions PASS** on the same QA head; offscreen UI capture **10 PNG / 3774 assertions PASS**. Release Build-Setup `10b/15` on `b45dc56` vẫn ghi **8053 assertions PASS** với build **0 warning / 0 error**.
- full Build-Setup `0.1.80-dev`: **15/15 PASS**, provenance + validator + 90-suite + persistence + Portable E2E + post-installer provenance recorded in release manifest;
- Portable E2E: **PASS**;
- Installer compile: **PASS**; full installer E2E **BLOCKED_SAFETY** trên máy hiện tại.

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
- `e728204`: full 402-bank Child UI compatibility.
- `08a9268`: selected-set metadata self-heal remains child-safe through real UI resume.
- `bb0f636`: all 48 integer answer-unit questions across 8 units covered by UI sweep.
- `1e27ff8`: prompt typography measures real text; 402/402 prompts fit at 900×640 with font >=12pt.
- `3a53af7`: release manifest assertion counts derive from actual smoke outputs, not stale literals.

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

Deep resume regression uses `m2_ls_time_day_24_hours`; full-bank matrix additionally covers **48 answer-unit questions / 8 units** (`cm`, `dm`, `m`, `kg`, `l`, `ngày`, `giờ`, `phút`):

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
- `3a53af7`: smoke assertion evidence is captured dynamically from each executable.

Artifact `0.1.45-dev`, clean `3a53af7`:

- release manifest schema 5, publish file count 55, Child UI evidence **3267**;
- staged payload includes 3 Math runtime JSON and migrations `001..005`;
- full release smokes PASS: SetupPreflight 42, Behavior 15, LearningSession 800, Motion 25, Child UI **3267**, Content 21, Security 19, Audio 14, Performance 13, Update 33, SQLite 179; counts are captured directly from smoke outputs;
- Portable SHA256 `E644E73F49E28156E065DDDD944F8C46A154A7AE8E01D05381BE762C10CBE2CE`;
- Installer SHA256 `6D79CDD3B43B4E9CCF1DCCB42C521DDA08D680B4070C525DDB7F280E1AD322E8`.

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

## 10. Latest full cross-lane gate

All on clean `b45dc56`:

- source provenance start / post-Portable / post-installer: **PASS**; dirty-source negative probe: **PASS** (`REFUSE_DIRTY_SOURCE`);
- Learning session: **800 assertions PASS**; Child UI: **3743 assertions PASS**;
- production validator: **402/402 valid**, game events **5/5**;
- full Math content/event/pool suite: **90/90 PASS**;
- `MathSessionPersistenceRuntimeSmoke`: **8053 assertions PASS**; Build-Setup `10b/15` build **0 warning / 0 error**;
- Garden exact milestones 1/3/6/10, durable-child eligibility, stale canonical reward repair, partial-reward milestone safety, stale/missing inventory self-heal: **PASS**;
- release manifest records provenance including `source_provenance_after_installer=PASS`, validator `402/402 + 5/5`, Python suite `90`, persistence `PASS/8053`, Child UI `3743`, Portable E2E PASS;
- full Build-Setup `0.1.80-dev`: **15/15 PASS** plus first-class sub-gates through `15b`;
- Portable E2E: **PASS**, bootstrap 2 lần exit 0, learner DB thật SHA trước/sau không đổi (`0C970952435DAC03FE56C8400D003E87C37CED5D91B79C8A7D459A8EC30868C1`);
- installer compile: **PASS**; update-manifest installer SHA khớp; Full Installer E2E **BLOCKED_SAFETY** do learner data không có ownership marker;
- `game_events_v1.json` SHA256 `F9F25EA94DA7FCD20E360509EE53E6E758039CCF788FAFEC729A35F80D39A8B1`, khớp manifest;
- Portable SHA256 `85EE0FB9DA514BD7362CE2C07695167EC6DE3D76ED2AE7A12AAC43D58A60862C`; Installer SHA256 `676AAB4606B2D9A22F123135DD365C14F39B37A945B2B7BB8FBEBB2255231A97`.

AI1 `LANE_DONE=YES`; AI2 `LANE_DONE=YES`; AI3 `LANE_DONE=YES`.

## 11. Remaining outside AI3 Math scope

No remaining Math UI/QA/integration blocker. The following are release-wide application gates, not Math blockers:

- production Authenticode signing;
- compatibility execution on a real Windows 7 machine.

`0.1.80-dev` is an unsigned dev artifact and must not be represented as a production-signed release. Real Windows 7 execution remains `UNAVAILABLE` in the current environment; the manifest correctly leaves that gate `PENDING`.
