# AI3 — MATH UI / QA / INTEGRATION AUDIT

Updated: 2026-09-07
Owner: AI3 — Math UI / QA / Integration
Project: `D:\APP HOC TAP`

## 1. Flow thực tế đã audit

Flow child hiện tại sau wave AI3-002:

`Home → Toán lớp 2 → chương → chủ đề/bài → lesson detail → Luyện 8 câu hôm nay → generated question → answer → hint/feedback → next → completion/garden reward → Math Hub/Home`

Lesson detail hiện dùng data thật từ AI1. Exercise vẫn là adaptive mission toàn Math, chưa phải session target đúng lesson đang xem vì AI2 chưa có targeted-session contract; AI3 không gắn nhãn giả “Luyện bài này”.

## 2. Inventory UI

| Hạng mục | Trạng thái | Audit thực tế |
|---|---|---|
| Math entry từ Home | DONE | `MainForm` mở `MathHubForm` |
| Math hub | DONE baseline | 7 chương / 17 chủ đề / 67 bài từ catalog thật |
| Chapter/topic selection | DONE | Scrollable navigation + selected state |
| Lesson selection | DONE | Lesson cards theo topic, có trạng thái progress thật |
| Theory/objective/concept | DONE baseline | Consume objective, explanation, concepts từ catalog |
| Worked example | DONE baseline | Prompt + solution steps + answer |
| Practice metadata | DONE | Hiển thị số câu basic/medium/application từ lesson |
| Lesson-targeted practice | BLOCKED / AI2 | Coordinator chưa nhận lesson/skill target |
| Exercise choice UI | DONE | 2/3/4 choice layout, selected/correct/incorrect/muted states |
| Hint UI | DONE | 2 level, visual cập nhật theo level |
| Feedback UI | DONE | Companion + child-safe feedback + correct answer highlight |
| Interactive answer UI | AI3 READY | `SegmentDrawingAnswerControl`; generated runtime còn Request 004 |
| Double-submit UI guard | DONE | `_submitting` + disable controls |
| Keyboard choice | DONE | D1-D4, NumPad1-4 |
| Keyboard/navigation | DONE baseline | Enter/Escape; hub button tab focus; interaction arrows/Home/End/Space |
| Accessibility | PASS baseline | Hub chapter/lesson/mission name/description; interaction description động |
| Result screen | PARTIAL | Completion/reward có; score/mastery delta/next lesson chưa có contract |
| Progress presentation | DONE baseline | Hub đọc `SkillSnapshot` thật, session progress thật |
| Mastery presentation | DONE baseline | Hub dùng `LearningState` + `MasteryScore`; không tự tính mastery |
| Locked/unlocked lesson | BLOCKED / AI2 | Catalog có prerequisites nhưng chưa có unlock rule first-class |
| Loading state | PARTIAL | Catalog sync load + safe failure; chưa có async loading UI |
| Error state | DONE baseline | Missing/corrupt catalog và fatal exercise đều child-safe, không white-screen |
| Empty state | DONE baseline | Catalog missing có empty state + adaptive mission fallback |
| Responsive/min window | PASS targeted | 1180×760 và 900×640 layout-tree gate; existing visual smoke 100%/125% |
| Long prompt/content | PASS baseline | Exercise co font; lesson detail dùng scroll + wrapping |
| Resume exact session | FAIL / AI2 | Coordinator chưa expose exact suspend/resume |
| Offline Math | PASS baseline | Catalog + learning DB + generator local |

## 3. Wave AI3-001 — interactive segment answer

Đã commit `c63e110`:

- thước 0..max theo `segmentdraw|target|max`;
- chọn A/B bằng mouse;
- keyboard Left/Right, Home/End, Space;
- selected length = `abs(B-A)` và serialize integer answer;
- submit chỉ enabled khi có hai đầu mút khác nhau;
- hint level 1/2;
- dynamic accessible description;
- locked correct/incorrect result visual;
- `MathLessonForm` switch theo `AnswerKind == interaction_integer`;
- Enter submit interaction;
- completion/fatal state ẩn đúng cả choice/interaction input.

### Blocker còn lại

`DrawSegmentGivenLength()` tạo `interaction_integer`, nhưng `MathQuestionGenerator.FinalizeAnswerOptions()` vẫn ép mọi non-text question về `integer` và sinh choices. Request 004 đã ghi cho AI2.

## 4. Wave AI3-002 — Math Hub + lesson content

Đã tạo `src/Content/MathLessonCatalogSource.cs`:

- parse `schema_version=1`, `subject=math`, `language=vi`;
- đọc chapter/topic/lesson/concept/worked example/practice/prerequisite;
- validate unique chapter/topic/lesson/skill;
- validate topic→chapter, lesson→topic/chapter, prerequisite skill references;
- fail-closed nếu lesson không `CHILD_READY` hoặc dữ liệu bắt buộc thiếu.

Đã tạo `src/App/MathHubForm.cs`:

- chapter list và topic/lesson list từ catalog thật;
- detail: mục tiêu, kiến thức, concept, example steps, answer, practice count, prerequisite;
- progress theo `LearnerSessionService.LoadSkillSnapshots()`;
- state text `Chưa học` / `Đang học` / `Cần ôn` / `Đã vững` từ engine state;
- mastery % chỉ trình bày `MasteryScore`, không tính lại frontend;
- review date nếu engine có `NextReviewAtUtc`;
- corrupt/missing catalog không crash, hiện child-safe empty state;
- adaptive mission vẫn khả dụng khi catalog lỗi;
- Escape về Home, tab-focus cho actions;
- responsive layout scroll được ở 900×640.

`MainForm` đã đổi Math CTA sang mở hub thay vì nhảy thẳng vào mission.

### Quyết định integration cố ý

- Không khóa lesson chỉ dựa vào prerequisite ở frontend vì chưa có engine unlock contract.
- Không gọi adaptive mission là “Luyện bài này” vì coordinator chưa nhận target lesson/skill.
- Không fake score/XP/mastery delta ở result.

## 5. P1 integration blockers

### P1-01 — interaction finalizer — owner AI2

`FinalizeAnswerOptions()` vẫn làm mất `interaction_integer`. Generated segment E2E chưa đạt.

### P1-02 — exact resume — owner AI2

Persistence schema V3/idempotency đã được commit `a9dfcdf`, nhưng coordinator hiện chưa có exact `Suspend/Resume` API và open-question reconstruction contract dùng được từ UI.

### P1-03 — lesson target + prerequisite unlock — owner AI2 contract

AI1 đã cung cấp prerequisite graph và content validator PASS, nhưng engine chưa publish rule/API để AI3 quyết định lock/unlock và bắt đầu session đúng lesson.

### P1-04 — clean build graph Data/SQLite — owner AI2/core

`dotnet msbuild WAHUKidsLearn.sln /t:Build /p:Configuration=Release /p:Platform=x86 /m` vẫn FAIL ở `src/Data` vì `System.Data.SQLite` không được resolve compile reference.

Các project chạy trước điểm fail (`Platform`, `Audio`, `Security`, `Motion`, `Learning`, `Content`, …) build PASS. AI3 targeted App/Smoke build PASS với existing Release Data artifact.

## 6. Tests hiện tại

- `WAHU.Content.csproj` Release x86: PASS.
- `WAHUKidsLearn.csproj` Release x86, `BuildProjectReferences=false`: PASS.
- `WAHU.ChildUiRuntimeSmoke.csproj` Release x86, `BuildProjectReferences=false`: PASS.
- ChildUiRuntimeSmoke: **PASS — 650 assertions** (baseline trước AI3-002: 605).
- MathEngineRuntimeSmoke: **PASS — 47 assertions**.
- MathContentDataSmoke: **PASS — 12/12 tests**.
- Full solution Release x86: **FAIL — Data/SQLite compile reference blocker**.

Assertions AI3-002 khóa thêm:

- catalog 7 chapters / 17 topics / 67 lessons;
- lesson lookup + skill→lesson mapping;
- objectives/concepts/worked example/practice count;
- hub renders chapter + topic/lesson navigation;
- lesson detail sections;
- accessibility name/description + keyboard-focusable mission;
- 1180×760 + minimum 900×640 layout tree;
- missing catalog → zero chapters + child-safe messages + detail hidden + adaptive mission available.

## 7. Việc AI3 tiếp theo

1. Chốt commit/push wave AI3-002.
2. Khi AI2 fix Request 004: thêm generated segment E2E gate.
3. Khi AI2 publish `Suspend/Resume`: nối close/restart/resume UI + Flow 2 regression.
4. Khi AI2 publish targeted lesson/prerequisite unlock: thêm “Luyện bài này”, locked/unlocked và Flow 5.
5. Khi result contract có score/mastery/reward/next lesson: hoàn thiện result presentation.
6. Khi SQLite build blocker đóng: full clean build + LearningSession/MathDataEngine + toàn bộ E2E gate.
