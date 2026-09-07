# MATH LIVE STATUS

Updated: 2026-09-07
LAST_FULLY_VERIFIED_HEAD=`3a53af7`
MATH_3AI_DONE=YES

Definition: trạng thái dưới đây dùng strict three-lane Math Definition of Done, không lấy việc “mở được màn hình” làm DONE.

## Overall

- **Content: 100% theo Math lane** — 7 chương, 17 chủ đề, 67 lesson, **402 authored questions**, đúng 6 câu/lesson = 2 basic + 2 medium + 2 application. Production semantic validator: **402/402 valid, 0 errors**; difficulty distribution **134 basic / 134 medium / 134 application**. `MathContentDataSmoke` **53/53 PASS**.
- **Engine: 100% theo Math lane** — schema V5 + pack identity, targeted session 3 câu từ pool 6, durable ordered selected-set, exact resume/retry/corrupt recovery, prerequisite unlock, validation/equivalence, mastery/result/next lesson, Request 010 expression whitelist, Request 006 display-only answer unit, adaptive segment generation, idempotency/write-failure/concurrency guards. `MathSessionPersistenceRuntimeSmoke`: **7159 assertions PASS** trên runtime 402, gồm stress/race/self-heal coverage mới nhất.
- **UI/QA: 100% theo AI3 scope** — Hub + 67 lesson detail/accessibility, theory/worked examples, targeted/adaptive session, choice/typed/interaction, hint/retry, exact resume, child-safe write failure, mastery/result/next lesson, error/empty/no-stale-state, responsive/keyboard. `ChildUiRuntimeSmoke`: **3267 assertions PASS**; **402/402 authored answer surfaces renderable**; **402/402 prompts fit** vùng câu hỏi ở 900×640 với font >=12pt; 48 answer-unit questions / 8 units được quét.
- **Distribution E2E: CLOSED cho Math dev artifact** — full Build-Setup 15/15, Portable E2E PASS, Installer E2E PASS trên clean `3a53af7`, artifact `0.1.45-dev`.
- **Three-AI Math DoD: DONE** — AI1, AI2 và AI3 đều `LANE_DONE=YES`; final cross-lane gate chạy trên cùng common HEAD.

## Contract hiện hành

- Runtime authored bank: **402 questions** / **67 lessons** / **6 per lesson**.
- Targeted lesson session: **3 questions**, chọn bền vững đúng 1 basic + 1 medium + 1 application.
- `SelectedContentQuestionIds` được persist; suspend/resume/retry/corrupt recovery giữ/recover selected set đúng contract.
- Hub hiển thị bank size 6 nhưng CTA/badge/accessibility không biến nó thành session size 6.
- Integer `answer_unit` chỉ là display metadata: raw integer grading không đổi; feedback có thể hiển thị `24 giờ` trong khi đáp án raw là `24`.
- Adaptive mission count derive từ `MathSessionCoordinator.DefaultTargetQuestionCount`.
- Numeric XP không được UI tự phát minh khi chưa có first-class product contract.

## Request đã đóng

### Request 009 — expanded pool vs 3-question session

CLOSED:

- engine `05cdb2a` + self-heal `3b8a10b`;
- runtime 402 publish `68145ea`;
- AI3 UI E2E `f567be8`;
- 402 compatibility sweep `e728204`.

Real-bank E2E xác nhận pool 6 → session/progress/result đúng 3, exact resume giữ ordered selected IDs và exact open question.

### Request 006 — answer unit

CLOSED:

- engine `3cf7fc6`;
- UI presentation/resume `3c0cfaa`; full 48-question / 8-unit matrix `bb0f636`.

Raw integer grading giữ nguyên; correct-answer feedback giữ display unit qua loader/runtime/suspend/resume.

### Request 008 — release packaging / portable / installer

CLOSED:

- E2E payload guards `109ee5d`;
- source/staged guards `490fd41`;
- manifest/runtime version guard `73d4c93`;
- 402 Child UI compatibility `e728204`; dynamic smoke evidence `3a53af7`.

Artifact dev `0.1.45-dev` từ clean `3a53af7`:

- schema: **5**;
- publish files: **55**;
- installed E2E file count: **57**;
- full release smokes: SetupPreflight 42, Behavior 15, LearningSession 800, Motion 25, Child UI **3267**, Content 21, Security 19, Audio 14, Performance 13, Update 33, SQLite 179 — tất cả PASS; các count được lấy trực tiếp từ output smoke;
- Portable E2E: first/second bootstrap exit 0, no installed-data mutation;
- Installer E2E: install 0, preflight 0, bootstrap 0, recovery 0, tamper exit 42, restored bootstrap 0, reinstall 0, uninstall 0;
- learner DB SHA256 trước/sau reinstall giống nhau: `FCA49F3ED8406D24262F41BD736F584A09E42454E2FB6F4EEE8DB4F692E3107C`;
- uninstall giữ learner DB/sentinel và remove startup registry;
- test-owned local data đã cleanup an toàn sau E2E.

Artifact hashes:

- Portable: `E644E73F49E28156E065DDDD944F8C46A154A7AE8E01D05381BE762C10CBE2CE`.
- Installer: `6D79CDD3B43B4E9CCF1DCCB42C521DDA08D680B4070C525DDB7F280E1AD322E8`.

## Latest fully verified gates — `3a53af7`

- `WAHUKidsLearn.sln` Release/x86 rebuild inside Build-Setup: **PASS**.
- Child UI: **3267 assertions PASS**.
- Production Math validator: **402/402 valid**.
- Core content smoke: **53/53 PASS**.
- Production validator: **402/402 valid**, 134 questions per difficulty.
- Persistence: `dotnet build` **0 warnings / 0 errors**; **7159 assertions PASS**.
- Content runtime: **21 assertions PASS**.
- SQLite/Data runtime: **179 assertions PASS**.
- Portable E2E: **PASS**.
- Installer/reinstall/uninstall E2E: **PASS**.
- `git diff --check`: **PASS**.

## Blocker cũ đã đóng trong AI3 no-wait wave

- hard-code adaptive `8/tám`: `82629a8`.
- pool-size/session-size UI coupling: `09c6551`, `f567be8`.
- generated segment/UI integration: `d21a665` + `ef3d35a`.
- valid→corrupt Hub stale state: `9c35ce5`.
- stale Continue accessibility name: `2bb1285`.
- 67-lesson accessibility breadth: `67c350e`.
- ContentRuntime pack-version stale assertion: `73d4c93`.
- 201-question Child UI assumptions sau publish 402: `e728204`.
- selected-set self-heal child-safe resume: `08a9268`.
- 48-question / 8-unit answer-unit matrix: `bb0f636`.
- 402-prompt min-window readability: `1e27ff8`.
- stale release assertion literals: `3a53af7` (manifest derives counts from smoke output).

## Ngoài strict Math lane

Không còn P0/P1 Math theo board 3-AI. Còn hai release-wide mục của toàn ứng dụng không được phép đánh tráo thành Math blocker:

1. production Authenticode signing — artifact `0.1.45-dev` hiện là unsigned dev build (`signed=false`);
2. compatibility smoke trên máy Windows 7 thật — current environment không có Win7 target, nên gate vẫn `PENDING/UNAVAILABLE`; preflight trên Windows mới báo `NEWER_WINDOWS_DEV_COMPATIBLE`.
