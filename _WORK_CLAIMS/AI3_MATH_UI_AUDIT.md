# AI3 — MATH UI / QA / INTEGRATION AUDIT

Updated: 2026-09-07
Owner: AI3 — Math UI / QA / Integration
Project: `D:\APP HOC TAP`

## 1. Flow thực tế đã audit

Flow child hiện tại sau wave AI3-004:

`Home → Toán lớp 2 → chương → chủ đề/bài → lesson detail → Luyện 8 câu hôm nay → generated question → answer → hint/feedback → next → completion/result → quay lại Math Hub/Home`

Resume flow hiện đã có contract thật:

`Học một phần → mở câu tiếp theo → Dừng và học tiếp sau / đóng app → Suspend → mở app → Start() resume cùng session → đúng open question → tiếp tục progress cũ`.

Lesson detail dùng data thật AI1. Exercise vẫn là adaptive mission toàn Math, chưa phải target đúng lesson đang xem vì engine chưa publish targeted-session/unlock contract; AI3 không gắn nhãn giả “Luyện bài này”.

## 2. Inventory UI

| Hạng mục | Trạng thái | Audit thực tế |
|---|---|---|
| Math entry từ Home | DONE | `MainForm` mở `MathHubForm` |
| Math hub | DONE baseline | 7 chương / 17 chủ đề / 67 bài từ catalog thật |
| Chapter/topic selection | DONE | Scrollable navigation + selected state |
| Chapter progress | DONE | số bài đã học + số `STABLE` lấy từ engine state thật |
| Continue lesson CTA | DONE | chọn lesson active/review gần nhất theo `LastSeenAtUtc`; disabled nếu chưa có progress |
| Lesson selection | DONE | Lesson cards theo topic, có progress/mastery thật |
| Theory/objective/concept | DONE baseline | Consume objective, explanation, concepts từ catalog |
| Worked example | DONE baseline | Prompt + solution steps + answer |
| Practice metadata | DONE | Hiển thị số câu basic/medium/application từ lesson |
| Lesson-targeted practice | BLOCKED / contract | Coordinator chưa nhận lesson/skill target first-class |
| Exercise choice UI | DONE | 2/3/4 choice layout, selected/correct/incorrect/muted states |
| Hint UI | DONE | 2 level, visual cập nhật theo level |
| Feedback UI | DONE | Companion + child-safe feedback + correct answer highlight |
| Interactive answer UI | DONE | generated `interaction_integer` → segment control; no fake choices |
| Double-submit UI guard | DONE | `_submitting` + disable controls; engine/DB có idempotency riêng |
| Keyboard choice | DONE | D1-D4, NumPad1-4 |
| Keyboard/navigation | DONE baseline | Enter/Escape; hub tab focus; interaction arrows/Home/End/Space |
| Accessibility | PASS baseline | chapter/lesson/continue/mission/result name/description; interaction description động |
| Result screen | DONE baseline / advanced blocked | durable attempts, independent/hinted/wrong, distinct skills, garden reward + đúng route về Math Hub; score/mastery delta/XP/next lesson chưa có first-class contract |
| Progress presentation | DONE baseline | Hub đọc `SkillSnapshot`; session dùng target/completed count thật |
| Mastery presentation | DONE baseline | dùng `LearningState` + `MasteryScore`; UI không tự tính mastery |
| Locked/unlocked lesson | BLOCKED / contract | catalog có prerequisites nhưng engine chưa publish unlock state/rule first-class |
| Loading state | PARTIAL | catalog sync load + safe failure; chưa có async loading experience riêng |
| Error state | DONE baseline | missing/corrupt catalog, corrupt resume cache và fatal exercise đều child-safe |
| Empty state | DONE baseline | catalog missing → empty state + adaptive mission fallback |
| Responsive/min window | PASS targeted | 1180×760 và 900×640 layout-tree gate; existing visual smoke 100%/125% |
| Long prompt/content | PASS baseline | exercise co font; lesson detail scroll/wrap |
| Resume exact session | DONE | engine exact restore + UI `Suspend`/notice/progress integration |
| Offline Math | PASS baseline | catalog + DB + generator local |

## 3. Wave AI3-001 — interactive segment answer

Commit `c63e110`:

- thước 0..max theo `segmentdraw|target|max`;
- chọn A/B bằng mouse;
- keyboard Left/Right, Home/End, Space;
- selected length = `abs(B-A)` và serialize integer answer;
- submit chỉ enabled khi có hai đầu mút khác nhau;
- hint level 1/2;
- dynamic accessible description;
- locked correct/incorrect visual;
- `MathLessonForm` switch theo `AnswerKind == interaction_integer`;
- Enter submit interaction.

Upstream `9e275de` đã đóng generator contract. Regression AI3 hiện dùng **generator thật**, không dựng tay question:

- `AnswerKind == interaction_integer`;
- `DisplayChoices.Count == 0`;
- `segmentdraw|...` tồn tại;
- generated target được chọn trên control và serialize đúng.

Generated interaction E2E: **PASS**.

## 4. Wave AI3-002 — Math Hub + lesson content

Commit `11d7914`:

`MathLessonCatalogSource`:

- parse schema/subject/language;
- đọc chapter/topic/lesson/concept/worked example/practice/prerequisite;
- validate unique IDs/skills;
- validate topic→chapter, lesson→topic/chapter, prerequisite references;
- fail-closed nếu content bắt buộc thiếu hoặc lesson không `CHILD_READY`.

`MathHubForm` baseline:

- chapter/topic/lesson từ catalog thật;
- detail: mục tiêu, kiến thức, concept, example steps, answer, practice count, prerequisite;
- progress/mastery từ `LearnerSessionService.LoadSkillSnapshots()`;
- state `Chưa học` / `Đang học` / `Cần ôn` / `Đã vững` từ engine;
- review date từ `NextReviewAtUtc`;
- corrupt/missing catalog không crash;
- adaptive mission vẫn khả dụng;
- Escape về Home, tab-focus actions;
- 900×640 scroll/layout PASS.

## 5. Wave AI3-003 — continue progress + resume integration

Commit `2d2813c` — `Toán UI: hoàn thiện tiếp tục bài và resume phiên học`.

### Math Hub

- chapter button trình bày số bài học, số đã học và số `STABLE` từ `SkillSnapshot` thật;
- `FindContinueLesson()` ưu tiên lesson đã attempt nhưng chưa `STABLE`, chọn gần nhất theo `LastSeenAtUtc`;
- nếu tất cả đã stable thì dùng lesson đã học gần nhất;
- CTA “Tiếp tục bài đang học” disabled khi chưa có evidence;
- CTA mở đúng chapter + lesson target;
- sau adaptive mission, `PopulateChapters()` + continue state được refresh để không hiển thị progress cũ.

### Math Lesson resume UX

Upstream resume contract đã commit `a1d5146`, `beb0c0e`, regression `a36c4cb`.

AI3 consume:

- `_targetQuestionCount` lấy từ `MathSessionStartResult.TargetQuestionCount`, không hard-code 8 khi resume;
- progress bar/value lấy `CompletedQuestionCount` và `Summary.Attempts` thật;
- restored open question → “Mình tiếp tục đúng câu con đang làm dở nhé.”;
- corrupt open cache → thông báo phần đã làm vẫn an toàn và tiếp tục bằng câu mới;
- resumed session không có open question → child-safe continue notice;
- stop button đổi thành `Dừng và học tiếp sau`;
- `RequestStop()` gọi `Suspend("child_requested_stop")`;
- `OnFormClosing()` gọi `Suspend("lesson_window_closed")`;
- fatal runtime path vẫn dùng `Abort` vì đó là fail-closed error, không phải UX “học tiếp sau”.

### Exact resume regression

Resume regression vẫn được giữ trong MathSessionPersistenceRuntimeSmoke; full smoke hiện **64 assertions PASS** sau khi AI2 bổ sung authored-bank coverage, gồm các gate resume cũ:

- suspend leaves session active/unended;
- no reward on suspend;
- same session id after restart;
- persisted target overrides constructor target;
- committed count reconstructed;
- exact open question id/content restored;
- `NextQuestion()` idempotent while open;
- stale already-committed cached question không replay;
- corrupt open question cache chỉ bị bỏ cache, không reset attempt/progress;
- deterministic next question across restart;
- completed reward exactly once.

AI3 UI regression kiểm thêm resume notice + stop-button semantics.

## 6. Wave AI3-004 — result baseline bằng dữ liệu durable

- `BuildCompletionPerformanceText()` chỉ consume `MathSessionSummary` và hiển thị:
  - tổng câu đã làm;
  - tự làm đúng = correct - hinted correct;
  - đúng nhờ gợi ý;
  - cần luyện lại = wrong, bounded theo attempts/correct để fail-safe.
- `BuildCompletionSupportText()` hiển thị distinct skills + garden progress/unlock message thật.
- Không có numeric score/XP/mastery delta tự tính ở UI.
- `_feedback` và `_support` có accessible summary sau completion.
- CTA kết quả đổi thành `Về thư viện Toán`, đúng route thực tế về `MathHubForm`.
- Fatal-state CTA dùng cùng route label để không nói sai là vào khu vườn.
- Regression result kiểm cả normal summary và inconsistent counters.
- ChildUiRuntimeSmoke hiện **674 assertions PASS**.

## 7. Remaining P1 integration blockers

### P1-01 — lesson target + prerequisite unlock contract

AI1 đã có prerequisite graph và hard guard. AI2 đã commit `a5119d4` loader authored bank 201 câu theo lesson, nhưng coordinator vẫn chưa publish API/state first-class để:

- bắt đầu session đúng lesson/skill đã chọn;
- quyết định lesson `locked/unlocked`;
- mark lesson completion theo product contract.

AI3 không tự suy đoán ngưỡng mastery/unlock.

### P1-02 — production clean build Data/SQLite

Old-style `src/Data/WAHU.Data.csproj` với `PackageReference` vẫn không resolve `System.Data.SQLite` khi build bằng `dotnet msbuild` trong workstation hiện tại.

AI3 đã xác minh SDK-style x86/net48 harness compile cùng production Data source + SQLite thật PASS, nhưng đây không thay thế Definition of Done `clean build pass` của production solution.

### P1-03 — result contract nâng cao

Baseline result đã dùng counters/reward durable. Engine vẫn chưa có lesson-level score / numeric XP / mastery delta summary / next-lesson contract first-class; AI3 không tự tính các giá trị này ở frontend.

## 8. Tests hiện tại

- `WAHU.Learning.csproj` Release x86: PASS.
- `WAHUKidsLearn.csproj` targeted Release x86: PASS.
- `WAHU.ChildUiRuntimeSmoke.csproj` targeted Release x86: PASS.
- ChildUiRuntimeSmoke: **PASS — 674 assertions**.
- MathSessionPersistenceRuntimeSmoke: **PASS — 64 assertions** (exact resume + authored-bank loader/traceability).
- MathEngineRuntimeSmoke baseline: **PASS — 47 assertions** ở gate gần nhất.
- MathContentDataSmoke: **PASS — 16/16 tests**.
- Full old-style solution Release x86: **FAIL — Data/SQLite compile-reference blocker**.

Assertions AI3 khóa hiện tại bao gồm:

- catalog 7 / 17 / 67;
- objectives/concepts/worked examples/practice;
- missing catalog safe state;
- chapter/lesson accessibility;
- responsive 1180×760 + 900×640;
- continue lesson disabled/enabled đúng evidence;
- chapter studied count từ real skill state;
- continue opens exact lesson;
- resume child-safe messaging cho restored/corrupt/progress cases;
- stop CTA có semantics học tiếp;
- generated interaction question → no choices → segment UI → correct serialized answer;
- result counters: independent/hinted/needs-practice + bounded inconsistent input;
- result support: distinct skills + garden/unlock message;
- completion/fatal CTA route label đúng `Về thư viện Toán` + accessibility.

## 9. Việc AI3 tiếp theo

1. Chốt commit/push wave AI3-004.
2. Theo dõi contract targeted lesson/prerequisite unlock; khi publish, thêm “Luyện bài này”, lock state và Flow 5.
3. Khi result contract nâng cao publish, bổ sung score/mastery-delta/XP/next lesson vào result baseline hiện có.
4. Khi SQLite production build blocker đóng, chạy full clean solution + toàn bộ smoke/E2E làm release gate.
