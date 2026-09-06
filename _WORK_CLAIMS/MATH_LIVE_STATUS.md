# MATH LIVE STATUS

Updated: 2026-09-07
Definition: % dưới đây đo theo Definition of Done strict của Math, không lấy việc “mở được màn hình” làm DONE.

## Overall

- **Content: 94%** — AI1 đã có 7 chương, 17 chủ đề, 67 lesson, 201 câu, prerequisite graph và semantic validator; 12/12 MathContentDataSmoke PASS. Phần còn lại chủ yếu là runtime consumption/contract với engine, không phải thiếu hàng loạt content.
- **Engine: 73%** — validator nhiều answer kind, mastery/review/reward, append-only attempts và semantic idempotency schema V3 đã có; exact resume, targeted lesson/prerequisite unlock và interaction finalizer vẫn chưa xong.
- **UI: 84%** — Math Hub + chapter/topic/lesson, lesson objective/concept/example/practice/prerequisite presentation, mastery thật theo skill, adaptive exercise, hint/feedback/completion và interactive segment control đã có. Còn result mastery/score/next lesson, locked/unlocked first-class và resume UX.
- **Test: 71%** — Child UI 650 assertions PASS, Math engine 47 assertions PASS, Math content 12/12 PASS; full solution build vẫn bị Data/SQLite compile-reference chặn nên chưa thể coi clean gate đạt.
- **E2E: 46%** — Home → Math Hub → lesson content và adaptive mission baseline chạy; Flow resume, prerequisite unlock, lesson-targeted practice và generated interaction chưa đạt full gate.
- **Tổng Math: ~70%** theo strict production Definition of Done hiện tại.

## P0

- Không phát hiện P0 UI/integration mới trong wave AI3-002.

## P1

1. `MathQuestionGenerator.FinalizeAnswerOptions()` vẫn làm mất `interaction_integer` và biến bài vẽ đoạn thành numeric choice — owner AI2, Request 004.
2. Exact resume active Math session chưa có API/runtime state hoàn chỉnh — owner AI2.
3. Chưa có targeted lesson/skill session + prerequisite unlock contract first-class; AI3 không fake “Luyện bài này” hoặc lock state ở frontend — owner AI2 contract, AI3 consume sau.
4. Full clean solution build vẫn FAIL tại `src/Data` vì `System.Data.SQLite` không được resolve thành compile reference dù package assets tồn tại — owner AI2/core build integration.

## P2 / missing product flow

- Result screen chưa có numeric score/mastery delta/next lesson vì engine chưa publish contract lesson-result tương ứng.
- Loading state catalog hiện là synchronous safe state; chưa có async loading experience riêng.
- Static question bank đã tồn tại nhưng adaptive runtime hiện vẫn chạy generator/template path; AI3 không tự thay engine source.

## Current tests

- `src/Content/WAHU.Content.csproj` Release x86: **PASS**.
- `src/App/WAHUKidsLearn.csproj` Release x86 (`BuildProjectReferences=false`): **PASS**.
- `tests/ChildUiRuntimeSmoke` Release x86 (`BuildProjectReferences=false`): **PASS**.
- ChildUiRuntimeSmoke: **PASS — 650 assertions**.
- MathEngineRuntimeSmoke: **PASS — 47 assertions**.
- MathContentDataSmoke: **PASS — 12/12 tests**.
- `WAHUKidsLearn.sln` Release x86 clean dependent build: **FAIL at `src/Data` / `System.Data.SQLite` reference resolution**; App/Content/Learning projects build trước điểm fail đều PASS.

## Integration wave status

### AI3-001 — interactive segment answer

- Commit: `c63e110` — `Toán UI: hỗ trợ vẽ đoạn thẳng tương tác`.
- UI control, keyboard, accessibility, hint/result state: PASS.
- Runtime-generated E2E: BLOCKED by AI2 Request 004.

### AI3-002 — Math Hub + lesson content integration

- Source: READY, đang chốt commit hiện tại.
- Consume trực tiếp `content_packs/math_grade2_v1/lesson_catalog_v1.json` qua `MathLessonCatalogSource`.
- Flow: Home → Toán lớp 2 → chương → chủ đề/bài → mục tiêu → kiến thức → concept → ví dụ có lời giải → practice metadata → prerequisite presentation.
- Progress/mastery: đọc thật từ `LearnerSessionService.LoadSkillSnapshots()`, không fake progress frontend.
- State: `Chưa học`, `Đang học`, `Cần ôn`, `Đã vững` dựa trên `LearningState` + `MasteryScore` engine đã persist.
- Missing/corrupt catalog: child-safe empty state; adaptive mission vẫn còn khả dụng.
- Responsive gate: 1180×760 + minimum 900×640 layout tree PASS.
- Accessibility: chapter/lesson/mission controls có name/description + keyboard focus.
- Lesson-targeted practice cố ý chưa mở vì coordinator chưa có targeted-session contract.

## Latest owner commits observed

- AI1 functional content: `ad5db15` — `Toán: chốt status content và test biểu thức fail-closed`.
- AI2: `a9dfcdf` — `feat(toán): khóa idempotency và nâng persistence lên schema v3`.
- AI3 functional: `c63e110` — interactive segment; AI3-002 là wave đang được commit từ trạng thái này.

## Next integration gates

1. AI2 preserve `interaction_integer` → generated segment E2E.
2. AI2 exact `Suspend/Resume` → Flow 2 close/restart/resume + Flow 3 restart progress.
3. AI2 targeted lesson/prerequisite contract → lesson-specific practice + locked/unlocked Flow 5.
4. Result contract → score/mastery/reward/next lesson presentation.
5. Data/SQLite build blocker closed → full clean build + LearningSession/MathDataEngine smoke + full E2E regression.
