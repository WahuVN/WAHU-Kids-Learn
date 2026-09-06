# AI3 — MATH UI / QA / INTEGRATION AUDIT

Updated: 2026-09-07
Owner: AI3 — Math UI / QA / Integration
Project: `D:\APP HOC TAP`

## 1. Flow thực tế đã audit

Runtime child flow hiện tại:

`Home -> Math roadmap -> Bắt đầu Toán -> adaptive Math session -> generated question -> answer -> hint/feedback -> next -> completion/garden reward -> Home`

Đây chưa phải flow lesson catalog theo `chapter -> topic -> lesson`, vì AI1 xác nhận machine-readable lesson catalog hiện chưa có. AI3 không hard-code hierarchy giả trong UI; chờ contract `lesson_catalog_v1.json` theo `MATH_SHARED_CONTRACT_REQUESTS.md`.

## 2. Inventory UI

| Hạng mục | Trạng thái | Audit thực tế |
|---|---|---|
| Math entry từ Home | DONE | `MainForm` mở Math mission ổn định |
| Math roadmap/hub | PARTIAL | Có 6 nhóm mastery thật; chưa có chapter/topic/lesson catalog |
| Chapter/topic selection | BLOCKED | Chưa có catalog machine-readable từ AI1 |
| Lesson selection | BLOCKED | Chưa có lesson metadata/prerequisite contract |
| Theory/objective/concept | MISSING | Runtime hiện chỉ có generated question + hint |
| Worked example | MISSING | Chờ lesson catalog/content contract |
| Exercise choice UI | DONE | 2/3/4 choice layout, selected/correct/incorrect/muted states |
| Hint UI | DONE | 2 level, visual cập nhật theo level |
| Feedback UI | DONE | Companion + child-safe feedback + correct answer highlight |
| Interactive answer UI | AI3 READY | Đã thêm `SegmentDrawingAnswerControl`; runtime còn blocker AI2 Request 004 |
| Double-submit UI guard | DONE | `_submitting` + disable controls |
| Keyboard choice | DONE | D1-D4, NumPad1-4 |
| Keyboard navigation/action | DONE/PARTIAL | Enter/Escape hiện có; segment control thêm arrows/Home/End/Space/Enter submit |
| Accessibility | PARTIAL | AccessibleName/Description rộng; segment interaction có description động |
| Result screen | PARTIAL | Có attempts, independent correct và garden reward; chưa có score/mastery/next lesson |
| Progress presentation | DONE/PARTIAL | Roadmap đọc mastery thật; session progress thật; chưa có lesson progress |
| Mastery presentation | PARTIAL | Roadmap aggregate mastery; result chưa show mastery delta cụ thể |
| Locked/unlocked lesson | MISSING/BLOCKED | Engine/content chưa có prerequisite/unlock lesson first-class |
| Loading state | PARTIAL | Start synchronous; có safe failure nhưng chưa có loading skeleton/state riêng |
| Error state | DONE baseline | Không white-screen; fatal child-safe message, committed data giữ lại |
| Empty state | PARTIAL | Roadmap có trạng thái chưa bắt đầu; lesson catalog empty state chưa có vì catalog chưa tồn tại |
| Responsive/min window | PASS baseline | Form min 900x640, AutoScaleMode.Dpi; existing runtime smoke 100%/125% |
| Long prompt | DONE baseline | Prompt font co theo độ dài, AutoEllipsis |
| Resume exact session | FAIL / AI2 | App close/Start hiện recover dangling rồi tạo session mới, không resume open question |
| Offline Math | PASS baseline | Math content/session local; không cần network cho learning flow |

## 3. Wave AI3-001 — interactive segment answer

Đã thêm UI độc lập tại `src/App/MathInteractiveControls.cs`:

- thước 0..max theo `IllustrationData = segmentdraw|target|max`;
- chọn A/B bằng mouse;
- keyboard: Left/Right, Home/End, Space;
- selected length = `abs(B-A)`;
- serialize integer answer;
- submit chỉ enabled khi có hai đầu mút khác nhau;
- hint level 1/2;
- dynamic accessible description;
- locked result visual đúng/sai;
- `MathLessonForm` switch giữa choice grid và interaction theo `AnswerKind == interaction_integer`;
- Enter submit interaction;
- completion/fatal state ẩn đúng cả choice/interaction input.

AI3 cố ý không phụ thuộc compile vào property WIP `UsesInteractiveAnswer`; UI dùng stable `AnswerKind` string để commit không phụ thuộc thay đổi chưa commit của AI2.

## 4. P1 integration blockers

### P1-01 — generator đang làm mất `interaction_integer` — owner AI2

`DrawSegmentGivenLength()` tạo `interaction_integer`, nhưng `MathQuestionGenerator.FinalizeAnswerOptions()` hiện ép mọi non-text question thành `integer` rồi sinh 4 choices.

Hậu quả: UI interaction AI3 không thể được runtime chọn dù control đã sẵn sàng.

Đã ghi `Request 004` vào `MATH_SHARED_CONTRACT_REQUESTS.md`.

### P1-02 — resume không phải resume — owner AI2

`MathSessionCoordinator.Start()` recover dangling sessions rồi tạo session mới. Open question, generated seed/index và current counters chưa persist để reconstruct đúng session.

Flow E2E 2 theo Definition of Done hiện FAIL cho tới khi AI2 wave persistence/resume hoàn tất.

### P1-03 — clean build graph bị Data/SQLite package-reference blocker — owner AI2/core

- `dotnet msbuild ... /t:Restore`: PASS, package assets có `System.Data.SQLite.dll`.
- Full dependent build: FAIL ở `src/Data` vì compile reference không resolve `System.Data.SQLite`.
- AI3 không sửa Data project vì ngoài ownership và AI2 đã ghi cùng blocker trong engine audit.

Để kiểm độc lập wave UI, AI3 build App + ChildUiSmoke với `BuildProjectReferences=false` dựa trên Release DLL đã có.

## 5. Tests đã chạy cho AI3-001

- `WAHUKidsLearn.csproj` Release x86, `BuildProjectReferences=false`: PASS.
- `WAHU.ChildUiRuntimeSmoke.csproj` Release x86, `BuildProjectReferences=false`: PASS.
- `WAHU.ChildUiRuntimeSmoke.exe`: PASS — **605 assertions**.
- Baseline trước wave: 594 assertions.
- Assertions mới khóa: no-answer initial, two-endpoint completion, absolute length, integer serialization, hint2 render, locked correct render, form interactive guidance, submit-disabled-before-answer, submit-enabled-after-answer.

## 6. Việc AI3 tiếp theo

1. Nhận AI2 fix Request 004 -> chạy generated interactive question E2E.
2. Nhận AI2 resume/idempotency -> thêm restart/resume regression gate.
3. Nhận AI1 lesson catalog -> tạo Math hub chapter/topic/lesson + theory/example screens từ data thật.
4. Bổ sung result: score/mastery/next lesson khi engine/content publish contract.
5. Bổ sung locked/unlocked states theo prerequisite contract.
6. Chạy clean build + full E2E khi Data/SQLite blocker được đóng.
