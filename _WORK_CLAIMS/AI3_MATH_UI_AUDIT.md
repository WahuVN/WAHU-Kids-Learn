# AI3 — MATH UI / QA / INTEGRATION AUDIT

Updated: 2026-09-07
Owner: AI3 — Math UI / QA / Integration
Project: `D:\APP HOC TAP`

## 1. Flow thực tế hiện tại

Adaptive flow:

`Home → Toán lớp 2 → Math Hub → Luyện 8 câu hôm nay → answer → hint/feedback → result → Math Hub/Home`.

Targeted lesson flow sau upstream `656a94b` + AI3-005:

`Home → Toán lớp 2 → chương → bài → theory/example → Luyện 3 câu bài này → authored basic/medium/application → result score → lesson progress persisted → quay lại Hub → prerequisite state refresh`.

Resume flow:

`Học một phần → dừng/đóng → Suspend → mở lại → same session + same mode/lesson + exact open question → tiếp tục progress cũ`.

## 2. Inventory UI

| Hạng mục | Trạng thái | Evidence |
|---|---|---|
| Math entry từ Home | DONE | `MainForm` mở `MathHubForm` |
| Math Hub | DONE | 7 chương / 17 chủ đề / 67 bài từ catalog thật |
| Chapter/topic/lesson selection | DONE | Scrollable + selected state + responsive |
| Theory/objective/concept | DONE baseline | consume catalog AI1 |
| Worked example | DONE baseline | prompt + solution steps + answer |
| Lesson-targeted practice | DONE | engine-owned target lesson contract `656a94b` |
| Locked/unlocked lesson | DONE | `MathLessonAccessSnapshot.IsUnlocked`; UI không tự đoán threshold |
| Locked lesson theory | DONE | vẫn đọc được; chỉ khóa practice CTA |
| Prerequisite explanation | DONE | accessible description nêu lesson prerequisite còn thiếu |
| Choice answer | DONE | 2/3/4 choices, selected/correct/incorrect/muted |
| Typed answer | DONE | numeric, word problem, expression, unit |
| Interaction answer | DONE | segment control `interaction_integer` |
| Hint | DONE | 2 level |
| Double submit guard | DONE | `_submitting` + controls disabled |
| Keyboard | DONE baseline | D1-D4/NumPad, Enter typed/interaction/next, Escape stop |
| Accessibility | PASS baseline | names/descriptions cho hub, lock, typed/interaction, result |
| Result counters | DONE | attempts, independent/hinted/wrong, distinct skills |
| Lesson score/best score | DONE | dùng `LessonScorePercent` / `LessonBestScorePercent` |
| Mastery delta / next lesson | DONE | AI3-006 consume trực tiếp `TargetSkillMasteryAfter/Delta`, `ImprovedSkillCount`, `NextLessonId/Title` từ `8b32944` |
| Numeric XP | NO PRODUCT CONTRACT | UI không tự invent XP |
| Progress/mastery presentation | DONE baseline | durable engine state |
| Exact resume | DONE | targeted + adaptive persistence regression |
| Error/empty state | DONE baseline | missing/corrupt catalog/runtime child-safe |
| Responsive | PASS targeted | 1180×760, 1080×720, 900×640 + 100/125% visual gates |

## 3. AI3-001 — interaction

Commit `c63e110`.

- Segment 0..max từ `segmentdraw|target|max`.
- Mouse + keyboard Left/Right/Home/End/Space.
- Serialize `abs(B-A)`.
- Hint/result/accessibility.
- UI smoke dùng direct `MathQuestion` interaction contract; generation/finalization thuộc AI2 engine smoke, tránh cross-owner test coupling.

## 4. AI3-002 — Hub/content

Commit `11d7914`.

- Catalog thật: chapter/topic/lesson/concept/worked example/practice/prerequisite.
- Safe missing/corrupt catalog.
- Progress/mastery từ `LearnerSessionService`, không fake frontend.

## 5. AI3-003 — continue/resume

Commit `2d2813c`.

- Continue target dựa trên durable `SkillSnapshot`.
- `TargetQuestionCount` / `CompletedQuestionCount` từ session start result.
- Stop và window close dùng `Suspend`.
- Exact open question resume + corrupt-cache child-safe messaging.

## 6. AI3-004 — result baseline

Commit `37f0b97`; route regression `b6a1ce4`.

- Result dùng counters durable.
- Counter display bounded fail-safe.
- Garden reward consume summary thật.
- `Về thư viện Toán` đúng modal route.

## 7. AI3-005 — targeted lesson / prerequisite / typed authored input

Commit `1436705` — `Toán UI: hoàn thiện luyện theo bài và toàn bộ dạng nhập đáp án`.

### Hub/access

- Load access bằng `MathLessonProgressService.GetAllAccess()`.
- Lesson state ưu tiên first-class lesson progress:
  - locked;
  - started/chưa hoàn thành;
  - completed + best score.
- `Luyện 3 câu bài này` chỉ enabled khi `IsUnlocked`.
- Locked CTA không làm theory biến mất.
- Missing prerequisite được resolve thành title lesson và đưa vào accessible description.
- Sau targeted/adaptive practice, refresh skill + lesson access + chapter/detail state.

### Targeted lesson form

- Internal ctor nhận `targetLessonId`.
- Dùng `MathSessionCoordinator(database, templatePath, profile, seed, lessonId)`.
- Session mode `lesson` hiển thị lesson title và engine-owned target question count.
- Completion targeted hiển thị `Hoàn thành bài học`.
- Score và best score lấy trực tiếp từ `MathSessionSummary`.

### Typed-answer surface

Blocker phát hiện khi audit authored bank: 201 câu có **109 câu choice-free** mà UI cũ chỉ hỗ trợ choice/segment, nên targeted practice sẽ fatal.

AI3-005 thêm typed answer cho:

- `numeric_input` / integer;
- `word_problem` choice-free;
- `expression_input` / expression;
- `unit_input` / unit.

Behavior:

- empty input → submit disabled;
- nhập text → submit enabled;
- Enter submit;
- raw answer string gửi thẳng engine, UI không normalize toán học;
- expression/unit có guidance riêng;
- incorrect feedback dùng `CorrectAnswerDisplay` engine;
- hint/progress/next/result dùng cùng lifecycle với choice/interaction.

### 201-question UI sweep

`ChildUiRuntimeSmoke` nạp authored bank thật và gọi `ConfigureAnswerInput()` cho toàn bộ 201 câu.

PASS distribution:

- **109 typed**;
- **91 choice**;
- **1 interaction**;
- tổng **201/201 renderable**.

### Flow 5 UI E2E

PASS:

1. DB schema V4 healthy.
2. Mở prerequisite lesson targeted.
3. 3 authored questions có lesson/content traceability đúng.
4. Submit qua actual typed/choice UI controls.
5. Result `Hoàn thành bài học`, score **100%**.
6. `MathLessonProgressStore`: completed=1, last=100, best=100.
7. Reload Hub.
8. Prerequisite lesson hiện completed/best score.
9. Dependent lesson chuyển từ locked sang enabled `Luyện 3 câu bài này`.

## 8. AI3-006 — advanced result presentation

Upstream contract: `8b32944`.

- Targeted result hiển thị `TargetSkillMasteryAfter` theo phần trăm.
- Khi `TargetSkillMasteryDelta > 0`, UI hiển thị mức tăng theo điểm phần trăm; không tự tính từ attempts.
- Adaptive result dùng `ImprovedSkillCount` trực tiếp từ summary.
- `NextLessonId` + `NextLessonTitleVi` chỉ được hiển thị khi engine publish đủ cặp; UI không tự dò một lesson khác và không tự điều hướng.
- Numeric XP không hiển thị vì product chưa có first-class XP contract.
- Synthetic result regression khóa 25% → 55% / +30 điểm phần trăm và next-lesson title.
- Flow 5 E2E khóa result support nhận mastery + immediate next lesson thật sau 3 authored questions.

## 9. Verification gates

Current clean HEAD `9682572` + đúng 2 file AI3-006:

- App Release x86 targeted build: **PASS**.
- ChildUiRuntimeSmoke: **PASS — 1138 assertions**.
- MathSessionPersistenceRuntimeSmoke: **PASS — 99 assertions**.
- MathContentDataSmoke: **PASS — 23/23**.
- `git diff --check`: **PASS**.
- Data/Session/Learning/Content source không đổi giữa upstream advanced-result `8b32944` và `9682572`; clean dependency artifacts tái dùng có kiểm chứng.

## 10. Remaining blockers

### P1-01 — production Data/SQLite clean build

Old-style `WAHU.Data.csproj` / SQLite reference vẫn chặn full clean production solution gate trên toolchain hiện tại. SDK source-equivalent harness pass nhưng không được coi là release-clean replacement.

### P1-02 — adaptive interaction generator ownership

Authored `interaction_integer` + UI control pass. Child UI không test `MathQuestionGenerator` để tránh ownership coupling; generator case `draw_segment_given_length` phải được engine smoke AI2 khóa khi commit ổn định.

### P1-03 — release packaging/E2E

Static audit tại HEAD:

- `Build-SetupArtifacts.ps1` copy đệ quy toàn bộ `content_packs\\*` và `data\\schema\\*.sql`; hard deployment guard đã có schema `004_math_lesson_progress.sql`.
- Inno Setup dùng `recursesubdirs/createallsubdirs` trên toàn staged publish tree; portable cũng copy toàn publish tree trước khi zip.
- Nhưng `Test-PortableE2E.ps1` và `Test-InstallerE2E.ps1` mới guard Math `manifest.json`, chưa guard `lesson_catalog_v1.json`, `question_bank_v1.json`, `verified_templates_v1.json`.
- Artifact mới nhất hiện có `0.1.41-dev` là build từ `e299c41`, database schema 2. Publish tree + portable ZIP có verified templates nhưng thiếu lesson catalog, question bank và schema V4; artifact này là **STALE**, không phải release evidence cho Math hiện tại.
- Request 008 đã mở cho release lane: hard-guard đủ ba Math runtime JSON và rebuild artifact schema V4; sau đó chạy portable/installer upgrade E2E giữ learner DB/lesson progress.

## 11. Next AI3 actions

1. Theo dõi release lane đóng Request 008 và production SQLite build blocker; không sửa `tools/build/*` khi đang có owner/WIP khác.
2. Khi có artifact schema V4 mới, chạy portable/installer Math payload + relaunch/reinstall regression.
3. Khi AI2 commit generator `draw_segment_given_length`, chạy engine-owned adaptive interaction regression rồi cập nhật status.
4. Sau các gate trên, chạy full Math release regression và chốt strict DoD.
