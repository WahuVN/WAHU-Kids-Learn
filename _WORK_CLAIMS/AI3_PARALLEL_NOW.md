# AI3 — MASTER EXECUTION PLAN — MATH UI / QA / RELEASE / INTEGRATION

LANE=AI3
ROLE=MATH_UI_QA_RELEASE_INTEGRATION_OWNER
LANE_DONE=NO
PLAYABLE_UI_P0=GREEN
PLAYABLE_RELEASE_PAYLOAD=GREEN
PLAYABLE_INSTALLER_E2E=BLOCKED_SAFETY_UNOWNED_LOCAL_DATA
EXECUTION_MODE=PLAYABLE_EVENT_UI_NO_WAIT
ACTIVE_PHASE=PLAYABLE_EVENT_GAME_V1
ACTIVE_FOCUS=FIRST_FIVE_QUICK_RESCUE_UI_E2E_RELEASE
LAST_FULLY_VERIFIED_HEAD=`65afcf2`
LAST_VERIFIED_CHILD_UI=3644_ASSERTIONS_PASS
LAST_VERIFIED_PERSISTENCE=7772_ASSERTIONS_PASS
LAST_VERIFIED_CONTENT=90_OF_90_PASS
LAST_VERIFIED_RELEASE=`0.1.49-dev`_BUILD_15_OF_15_PORTABLE_PASS_INSTALLER_COMPILE_PASS
EARLY_LESSON_DEEP_HEAD=`e34136d`
EARLY_LESSON_DEEP_CHILD_UI=3564_ASSERTIONS_PASS
EARLY_LESSON_DEEP_PERSISTENCE=7592_ASSERTIONS_PASS
EARLY_LESSON_DEEP_CONTENT=62_OF_62_PASS
EARLY_LESSON_DEEP_VALIDATOR=402_OF_402_VALID

> Khi người dùng bảo “đọc file và làm”: đọc toàn bộ file này, inspect current HEAD, verify UI/release trên contract hiện hành rồi tiếp tục QA/hardening. Không dừng chỉ vì `LANE_DONE=YES`.

## PLAYABLE EVENT V1 OVERRIDE

Đọc và tuân thủ `_WORK_CLAIMS/MATH_PLAYABLE_EVENT_GAME_V1.md`. P0 mới là đưa first-five thành flow chơi được: Home/Hub entry → intro → 3 checkpoints dùng lại MathLessonForm → behavior-aware repair/break → completion restoration/Garden → resume E2E. Dùng internal fixture/view-model trước khi AI1/AI2 land; final adapter mỏng sau. Không chờ và không đổi engine/content semantics.

## PRIORITY OVERRIDE — làm kỹ bài đầu trước

Theo chỉ đạo hiện tại, AI3 **không dàn thêm hardening mới ra toàn bộ 67 bài**. Ưu tiên sâu trước cho 5 bài nền đầu của Chương 1:

1. `m2_ls_num_count_read_write_0_1000` — Đếm, đọc và viết số đến 1000.
2. `m2_ls_num_full_hundreds_recognize` — Nhận biết số tròn trăm.
3. `m2_ls_num_predecessor_successor` — Số liền trước và số liền sau.
4. `m2_ls_place_value_hundreds_tens_ones` — Giá trị hàng trăm, chục, đơn vị.
5. `m2_ls_num_expanded_form_hto` — Viết số thành tổng trăm, chục, đơn vị.

Deep gate cho cụm này trước khi mở rộng bài 6+:
- pool đúng 6 câu/bài, session chọn đúng 3 câu = 1 basic + 1 medium + 1 application;
- surface đúng typed/choice theo authored question;
- hint 1/hint 2 hiển thị đúng, không rò đáp án/jargon nội bộ;
- correct/wrong/retry feedback và progress 1/3 → 3/3 đúng contract;
- suspend/resume giữ exact câu/selected-set ở flow nền;
- completion/result/persisted progress đúng và CTA thành luyện lại;
- prerequisite/unlock từ bài 1 sang bài 2–4, bài 4 mở bài 5 và flow bài 5 hoàn tất đúng snapshot engine;
- render/readability/accessibility ở cửa sổ tối thiểu 900×640.

Bài 6+ hiện chỉ giữ regression chung đã có; **không đầu tư thêm hardening mới** cho tới khi 5 bài đầu qua deep gate sạch.

## 0. Mission

AI3 chịu trách nhiệm Math presentation và distribution integration: Hub, lesson UI, answer surfaces, retry/resume/error states, accessibility/keyboard, Child UI regression, release payload, portable/installer E2E.

AI3 **không thay engine semantics** và không chỉnh authored content để UI test qua. Nếu consumer contract lỗi, ghi evidence + handoff và chuyển fallback.

## 1. Stable state phải bảo vệ

- Math Hub: 7 chapters / 17 topics / 67 lessons.
- Runtime bank: 402 questions; 6/lesson; UI có thể hiển thị bank size 6.
- Targeted session: engine selected exactly 3; progress/result dùng 3, **không biến pool=6 thành session=6**.
- Adaptive mission count derive `MathSessionCoordinator.DefaultTargetQuestionCount`; không literal `8/tám` coupling.
- Request009 UI E2E: exact ordered selected IDs + exact open question survive resume.
- Request006 UI E2E: raw integer input, feedback with display unit.
- Generated segment uses real interactive control.
- 402 authored answer surfaces renderable.
- Valid→corrupt reload fail-closed, không stale visible/internal/accessibility state.
- 67/67 lesson accessibility/access/prerequisite strings.
- Release payload requires 3 Math JSON + schema V5 migrations.
- Portable/installer preserve learner data across reinstall/uninstall contract.

Milestones đã có, không làm lại implementation mù nếu HEAD vẫn chứa:
- adaptive count: `82629a8`
- pool-size/session-size: `09c6551`, real E2E `f567be8`
- generated segment UI: `ef3d35a`
- corrupt reload: `9c35ce5`, `2bb1285`
- 67-lesson accessibility: `67c350e`
- answer-unit UI: `3c0cfaa`
- runtime 402 sweep: `e728204`
- release payload: `109ee5d`, `490fd41`, version guard `73d4c93`
- selected-set self-heal presentation: `08a9268`
- full answer-unit matrix (48 questions / 8 units): `bb0f636`
- 402-prompt min-window fit: `1e27ff8`
- dynamic release smoke evidence: `3a53af7`

Current verified evidence on `3a53af7`: Child UI **3267 assertions PASS**, persistence **7159 assertions PASS**, content **53/53 PASS**, validator **402/402 valid**, Build-Setup `0.1.45-dev` **15/15 PASS**, Portable/Installer E2E **PASS**. Assertion counts may continue to grow.

## 2. Ownership

AI3 được sửa:
- `src/App/MathHubForm.cs`
- `src/App/MathLessonForm.cs`
- `tests/ChildUiRuntimeSmoke/**`
- Math-specific UI integration tests
- `tools/build/Build-SetupArtifacts.ps1`
- `tools/build/Test-PortableE2E.ps1`
- `tools/build/Test-InstallerE2E.ps1`
- Math-related setup/preflight/release tests
- `_WORK_CLAIMS/AI3_MATH_UI_AUDIT.md`
- `_WORK_CLAIMS/AI3_PARALLEL_NOW.md`
- `_WORK_CLAIMS/MATH_LIVE_STATUS.md`

AI3 không sửa:
- `src/Learning/**`
- `src/Session/**`
- authored `lesson_catalog_v1.json` / `question_bank_v1.json`
- AI1 authoring/validator.

Nếu `Build-SetupArtifacts.ps1` đang dirty bởi lane khác: không chờ, chuyển UI/accessibility/portable-list/fallback tests.

## 3. START PROCEDURE — mỗi lần được gọi lại

1. `git status --short --branch`.
2. `git log --oneline -15`.
3. Xác nhận current runtime bank count/buckets và engine target contract qua public API/reflection tests hiện hành.
4. Build App/Child UI x86/Release bằng toolchain phù hợp.
5. Run full ChildUiRuntimeSmoke.
6. Verify 402/402 authored surfaces renderable.
7. Verify 67/67 lesson access/accessibility sweep.
8. Verify pool6/session3 + resume selected-set.
9. Verify answer-unit presentation.
10. Verify adaptive target no-hard-code + generated segment interaction.
11. Nếu source/release contract vừa đổi, chạy Build-Setup + payload/preflight + portable/installer E2E.
12. `git diff --check`.
13. Nếu xanh, chuyển ngay P1 UI/QA hardening.

## 4. P0 — UI/integration regression phải fix ngay

- Hub crash/blank/stale content;
- lesson access khác engine snapshot;
- locked lesson vẫn start được qua UI;
- prerequisite message thiếu/misleading;
- pool size 6 bị nói thành session 6;
- target/progress/result count sai;
- retry đổi câu hoặc enable/disable control sai;
- suspend/resume mất exact open question/selected set;
- typed/choice/interaction surface render sai;
- answer-unit feedback mất unit hoặc input bắt trẻ gõ unit trái contract;
- generated segment rơi về typed/fake choice;
- stale state/accessibility sau corrupt reload;
- hard-code `8/tám` quay lại;
- keyboard/accessibility broken;
- release payload thiếu Math JSON/migration;
- manifest/runtime pack version mismatch không fail-closed;
- reinstall/uninstall làm mất learner data trái contract.

Fix UI/release root cause + regression; không đổi engine/content semantics.

## 5. P1 — Child UI hardening backlog

### 5.1 Full answer-surface sweep
Cho toàn bộ current authored bank:
- typed integer/expression/unit guidance;
- MC/TF choices;
- interaction controls;
- correct/wrong/retry/final feedback;
- hint 1/hint 2;
- no clipped/disabled control due long content within supported window.

Không hard-code 402 forever trong logic; test có thể assert current contract nhưng loader/render phải future-proof.

### 5.2 Pool6/session3 matrix
- fresh start several lessons/seeds;
- bank detail says 6;
- start/result/progress says 3;
- selected set one/difficulty;
- q2 suspend/resume exact instance;
- no fourth question;
- completed lesson CTA becomes replay wording without claiming pool is target.

### 5.3 Answer-unit UI
Cover multiple units, không chỉ `giờ`:
- cm, dm, m, kg, l, ngày, giờ, phút where current bank contains them;
- raw typed value remains numeric;
- feedback displays value+unit;
- resume preserves presentation;
- accessibility/help text không yêu cầu nhập unit nếu answer kind integer.

### 5.4 Error/stale-state
- valid → corrupt catalog/bank;
- missing file;
- mismatched manifest/version;
- access load failure;
- resume checkpoint corrupt/recovered;
- answer write failure;
- UI clears stale labels/buttons/accessibility names/internal caches and presents safe fallback.

### 5.5 Accessibility/keyboard
For 67 lessons:
- lesson-specific accessible name/description;
- keyboard open/start/retry/hint/submit;
- all missing prerequisite lesson names announced;
- disabled controls explain reason;
- no stale accessible name after reload failure.

### 5.6 Layout/readability
- long Vietnamese lesson titles/objectives/prompts/rationales within supported content limits;
- common DPI/window sizes if test harness supports;
- no overlap/truncation of critical answer/feedback/action controls;
- child-facing strings no internal runtime jargon introduced by UI fallback.

## 6. P2 — Release / distribution hardening

Khi relevant source changes:

### Build-Setup
- Release/x86 build;
- all configured smoke suites;
- staged payload includes:
  - `verified_templates_v1.json`
  - `lesson_catalog_v1.json`
  - `question_bank_v1.json`
  - migrations through schema V5;
- release manifest records current assertion evidence; không hard-code stale counts nếu script có thể derive.

### Portable E2E
- first bootstrap exit 0;
- second bootstrap exit 0;
- portable learner data stays portable;
- does not mutate installed learner data;
- required Math payload present.

### Installer E2E
- install/preflight/bootstrap/recovery PASS;
- config tamper fail-closed expected code;
- restore then bootstrap PASS;
- reinstall preserves learner DB hash and startup-disabled state;
- uninstall removes app/startup registry but preserves learner DB/sentinel according to contract;
- cleanup only E2E-owned data after safety marker verification.

## 7. Production signing / real Windows 7

Đây là release-wide gate, không được fake:

- Nếu **thực sự có signing certificate/credential**: sign artifacts bằng production process, verify signature/chain/timestamp, rerun relevant setup E2E and record SHA/signature evidence.
- Nếu **thực sự có Windows 7 machine/VM** đáp ứng test requirement: run compatibility smoke/install/bootstrap/session/update/uninstall matrix và record evidence.
- Nếu không có cert hoặc real Win7 target: ghi blocker rõ `UNAVAILABLE`, không claim production-signed/Win7-verified; tiếp tục P1/P2 automation/readiness thay vì ngồi chờ.

Unsigned dev artifact phải luôn được gọi là unsigned dev artifact.

## 8. Acceptance gates trước commit AI3

Tùy wave:
- affected App/Child UI build x86/Release PASS;
- ChildUiRuntimeSmoke PASS;
- 67-lesson sweep PASS nếu Hub/access changes;
- 402 surface sweep PASS nếu rendering changes;
- pool6/session3 PASS nếu session presentation touched;
- answer-unit PASS nếu feedback/input touched;
- Build-Setup + portable/installer E2E nếu release scripts/payload touched;
- `git diff --check` PASS.

Nếu failure thuộc engine/content: capture exact failing assertion/contract and handoff; không sửa chéo lane.

## 9. Commit discipline

- Một concern/commit.
- Exact file list / `git commit --only`.
- Không stage engine/content WIP.
- Push ngay sau gate xanh.
- Update `AI3_MATH_UI_AUDIT.md`, `AI3_PARALLEL_NOW.md`, `MATH_LIVE_STATUS.md` chỉ bằng evidence current HEAD.
- Không để status docs nói common HEAD cũ là “final” nếu current HEAD đã đổi; dùng `last verified` hoặc cập nhật sau full gate.

## 10. FALLBACK — không bao giờ chờ AI1/AI2

Nếu engine/content/shared file đang bị sửa:
- accessibility audit;
- hard-code scan;
- stale-state/error-state tests;
- 67 lesson render/access sweep;
- synthetic pool6/answer-unit fixtures;
- portable/installer required-file list tests;
- release status cleanup;
- reflection tests bảo vệ engine constants/public contracts;
- layout/keyboard regression.

## 11. Handoff

- AI2 contract SHA mới → AI3 thêm presentation/E2E, không rewrite engine.
- AI1 bank/content SHA mới → AI3 rerun surface/access/release payload tests, không rewrite content.
- AI3 phát hiện consumer bug → ghi exact assertion + repro + owner trong own status, tiếp tục fallback.

## 12. STOP RULE AI3

AI3 chỉ dừng khi:
- current HEAD Child UI/integration contracts verify xanh;
- không còn P0/P1 UI/QA/release bug có evidence;
- release scripts liên quan current changes đã gate xong;
- cert/Win7 external gate nếu unavailable đã được ghi đúng, không fake completion;
- AI3-owned files sạch sau commit;
- push xong;
- `AI3_MATH_UI_AUDIT.md` + `MATH_LIVE_STATUS.md` phản ánh current evidence.

`LANE_DONE=YES` chỉ nói strict Math UI/QA DoD trước đây đạt; lần sau được gọi vẫn phải START PROCEDURE + QA/harden tiếp.
