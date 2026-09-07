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
| Retry cùng câu / first-try | DONE | AI3-008 consume `CanRetry`, `QuestionCompleted`, `RetryPending`, `IndependentCorrect`, `RetriedQuestions`, `RetriedCorrect` |
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

## 9. AI3-007 — full lesson-detail/access sweep

Commit `50fa4c0` — `Toán QA: quét đủ 67 bài học trên hub`.

AI3 thêm regression chọn lần lượt toàn bộ 67 lesson qua `MathHubForm.SelectLessonInCatalog()`.

Mỗi lesson phải thỏa đồng thời:

- selected lesson ID đúng catalog;
- detail render title, `Mục tiêu`, `Ví dụ có lời giải`;
- practice count đúng `PracticeSets.TotalCount`;
- practice CTA tồn tại và có accessible description;
- CTA `Enabled` khớp trực tiếp `MathLessonAccessSnapshot.IsUnlocked` từ engine-owned access map.

Gate này không tự suy luận prerequisite/unlock ở frontend và không mở session 67 lần; nó khóa toàn bộ presentation path + access binding với chi phí test hợp lý.

## 10. AI3-008 — retry cùng câu / first-try semantics

Upstream retry contract: `7c9a9ea`.

- First attempt gọi `SubmitAnswerWithRetry`; retry pending gọi `SubmitRetryAnswer`, không gọi API cũ theo kiểu đoán state.
- Nếu `QuestionCompleted=false` + `CanRetry=true`, UI giữ nguyên `_question`, không tăng progress và không cho chuyển câu.
- Typed answer mở lại textbox, select-all và giữ Enter-submit.
- Choice answer chỉ đánh dấu lựa chọn vừa sai; các lựa chọn khác vẫn idle/enabled và không reveal correct answer trước retry.
- Interaction answer không gọi `ShowResult` ở lần sai đầu, nên thước vẫn chỉnh được; chỉ final retry mới khóa control.
- `MathSessionStartResult.RetryPending` được presentation khi resume đúng attempt 2.
- Completion presentation dùng `IndependentCorrect`, `RetriedQuestions`, `RetriedCorrect`; retry-correct không còn bị suy ra nhầm thành independent success.
- Flow 5 thật cố ý sai câu typed đầu tiên rồi retry đúng: completed count giữ 0 sau first try, final lesson vẫn score 100%, result ghi `Tự làm đúng 2` + `Thử lại 1 câu (đúng 1)`.
- Choice retry E2E dùng lesson root `m2_ls_point_recognize`, xác nhận first-try sai không reveal đáp án đúng và final retry mới khóa choices.
- Interaction retry E2E hoàn thành prerequisite chain `POINT_RECOGNIZE → LINE_SEGMENT_RECOGNIZE` qua UI thật, sau đó authored medium `m2_q_draw_segment_given_length_02` sai độ dài → redraw → retry đúng → thước khóa sau final.

## 11. AI3-009 — exact retry-resume UI regression

Commit `c5cb9da` — `Toán QA: khóa retry resume đúng câu`.

Test-only wave khóa presentation/lifecycle khi đóng app đúng lúc đang ở attempt 2:

- first form trả lời sai authored choice ở attempt 1 rồi `Suspend` với retry pending;
- second form mở lại cùng lesson phải restore đúng authored `ContentQuestionId`;
- `_retryPending=true`, progress label có `thử lại`, support text nói rõ đang tiếp tục lần thử lại của cùng câu;
- answer choices sau restore vẫn idle/enabled và không reveal đáp án đúng;
- retry đúng sau resume finalize đúng một question với `Attempts=1`, `AnswerAttempts=2`, `RetriedQuestions=1`, `RetriedCorrect=1`, `IndependentCorrect=0`;
- test cleanup abort session sau khi đã xác minh durable retry lifecycle.

Clean detached `c08c7cf`: Child UI **1534 assertions PASS**, persistence **171 assertions PASS**, content **30/30 PASS**.

## 12. AI3-010 — recoverable answer-write failure UI

Upstream durable reconcile contract: `400fd0c`; concurrent active-session guard hiện đã có trong `b8fd18b`.

- Submit exception không còn mặc định phá hủy buổi học nếu engine đã rollback sạch và giữ đúng open question.
- UI chỉ phục hồi khi đồng thời có `IsActive`, `HasOpenQuestion` và `NextQuestion().QuestionId == _question.QuestionId`; guard fail thì vẫn `FailCurrentSession()`/`Abort` như trước.
- Recovery reset `_submitting=false`, giữ `_retryPending` hiện tại, không tăng progress và không hiện nút Next.
- Typed input được enable/select-all; interaction submit được enable lại nếu control còn answer; choice buttons về idle/enabled và không reveal correct answer.
- Child-facing feedback nói rõ câu chưa lưu, dữ liệu đã lưu trước đó vẫn an toàn và có thể thử lại chính câu này.
- SQLite trigger E2E làm fail `mastery_event` write ở first attempt: counters vẫn 0/0, cùng authored question còn mở; bỏ trigger rồi submit lại commit một lần với `IndependentCorrect=1`.
- SQLite trigger E2E làm fail đúng retry attempt 2: `AnswerAttempts` vẫn 1, `_retryPending=true`, không ghost retry; bỏ trigger rồi submit lại cho `RetriedQuestions=1`, `RetriedCorrect=1`, `IndependentCorrect=0`.

Clean detached `2da39b5`: Child UI **1558 assertions PASS**, persistence **171 assertions PASS**, content **34/34 PASS**. Data/Session SDK x86/net48 được clean-build trực tiếp từ cùng runtime HEAD với **0 warning / 0 error**.

## 13. AI3-011 — multi-surface write-failure recovery regression

Test-only wave mở rộng E2E của AI3-010 sang hai answer surfaces còn lại:

- typed authored root lesson: inject trigger fail `mastery_event`, submit đúng phải giữ session active/open question, `Attempts=0`, `AnswerAttempts=0`, textbox + submit được mở lại và accessibility báo câu chưa lưu; bỏ trigger rồi submit lại cho `IndependentCorrect=1`, không tạo retry giả;
- authored interaction lesson: hoàn thành prerequisite qua UI thật, vào medium segment, vẽ đúng rồi inject write failure; session giữ medium open, thước vẫn chỉnh/redraw được và submit được mở lại; bỏ trigger rồi submit lại commit medium đúng một lần, sau final mới khóa thước;
- cùng choice first-attempt/retry-attempt recovery đã có ở AI3-010, cả choice/typed/interaction đều được khóa bằng SQLite transaction failure thật.

Clean detached `847be1b`: production solution Rebuild Release/x86 **PASS**, Child UI **1593 assertions PASS**, persistence **183 assertions PASS**, content **36/36 PASS**, release-required runtime smokes **11/11 PASS**.

## 14. AI3-012 — decouple lesson bank size from practice CTA

UI-side Request 009 coupling được đóng mà không hard-code `3`:

- lesson detail vẫn dùng `PracticeSets.TotalCount` để trình bày kích thước **ngân hàng** câu hỏi;
- CTA unlocked/completed đổi thành `Luyện bài này` / `Luyện lại bài này`, bỏ numeric badge và không còn nhận `practiceCount` làm tham số;
- regression mô phỏng in-memory pool 6 câu (2 basic + 2 medium + 2 application): detail hiện `6 câu trong ngân hàng bài học`, CTA vẫn trung tính và badge rỗng;
- Flow 5/prerequisite unlock vẫn tìm và mở đúng targeted lesson practice.

Clean detached `31e1991`: production solution Rebuild Release/x86 **PASS**, Child UI **1596 assertions PASS**, persistence **194 assertions PASS**, content **36/36 PASS**, release-required smokes **11/11 PASS**.

## 15. Verification gates

Current clean release evidence tại `31e1991`:

- Production `WAHUKidsLearn.sln` Rebuild Release/x86 bằng Visual Studio 2022 Community MSBuild: **PASS**.
- ChildUiRuntimeSmoke: **PASS — 1596 assertions**.
- MathSessionPersistenceRuntimeSmoke: **PASS — 194 assertions**.
- Release-required runtime smokes: **11/11 PASS** — SetupPreflight 42, Behavior 15, LearningSession 794, Motion 25, Child UI 1596, Content 21, Security 19, Audio 14, Performance 13, Update 33, SQLite 166.
- MathContentDataSmoke: **PASS — 36/36**.
- `git diff --check`: **PASS**.
- 67/67 lesson-detail/access sweep: **PASS**.
- 201/201 authored answer-surface sweep: **PASS**.
- Retry typed/choice/authored-interaction E2E: **PASS**.
- Exact retry-resume UI E2E: **PASS**.
- Recoverable SQLite write-failure E2E trên choice/typed/interaction: **PASS**.
- Request 007 corrupt-medium ordinal recovery có regression chính thức tại `7f79367`.
- Retry/first-try engine contract `7c9a9ea` và write-failure reconcile `400fd0c` đã được AI3 UI consume; clean persistence suite hiện **194 assertions**.
- Generated `draw_segment_given_length` vẫn chỉ ở WIP AI2; chưa coi adaptive-generator blocker CLOSED trước upstream commit.

## 16. Production release build audit

Clean detached `31e1991` dùng đúng Visual Studio 2022 Community MSBuild production toolchain:

`C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe WAHUKidsLearn.sln /restore /m /t:Rebuild /p:Configuration=Release /p:Platform=x86`

Kết quả: **PASS — exit 0**. `WAHU.Data`, `WAHU.Session`, App và toàn solution production path đều build được.

Release-required smoke executables trên cùng clean tree: **11/11 PASS**.

- SetupPreflight **42**; Behavior **15**; LearningSession **794**; Motion **25**; Child UI **1596**; Content **21**; Security **19**; Audio **14**; Performance **13**; Update **33**; SQLite **166**.
- `0437122` đóng runtime-config schema mismatch bằng cách đồng bộ `RuntimeConfigBundle` với database runtime schema 4.
- `847be1b` đóng `bundled_english_verified`: `.gitattributes` giữ exact LF bytes của `verified_core_v1.json` trên Windows `core.autocrlf=true`, nên manifest SHA-256 ổn định trên clean checkout.

Vì vậy release runtime smoke chain đã **CLOSED**; strict distribution DoD hiện còn packaging/installer payload Request 008 và Math pool/session contract Request 009.

## 17. Remaining blockers

### P1-01 — adaptive interaction generator ownership

Authored `interaction_integer` + UI control pass. Shared WIP AI2 đã có `draw_segment_given_length` + LearningSession smoke cho interactive no-fake-choice, nhưng chưa nằm trong stable HEAD. AI3 không stage/edit generator-owned WIP.

### P1-02 — release packaging/E2E — Request 008

- Staging logic copy toàn `content_packs` + `data/schema`; schema V4 đã có guard.
- `Build-SetupArtifacts.ps1`, `Test-PortableE2E.ps1`, `Test-InstallerE2E.ps1` vẫn chưa hard-require đủ `lesson_catalog_v1.json`, `question_bank_v1.json`, `verified_templates_v1.json`.
- Artifact `0.1.41-dev` cũ không phải release evidence cho Math hiện tại.
- Chờ release lane đóng Request 008 rồi AI3 chạy portable/installer Math load + relaunch/reinstall regression.

### P1-03 — expanded authored pool vs 3-question session — Request 009

UI audit hiện tại:

- lesson detail **đúng**: dùng `PracticeSets.TotalCount` để hiển thị `N câu trong ngân hàng bài học`;
- lesson form/progress/result **đúng**: dùng `StartResult.TargetQuestionCount` / outcome `TargetQuestionCount` thật;
- Hub CTA **đã decouple AI3-012**: `CreateLessonPracticeButton()` không còn nhận pool count; unlocked/completed dùng `Luyện bài này` / `Luyện lại bài này` và badge rỗng.
- Synthetic pool 6 đã khóa regression: detail hiện bank size 6 nhưng CTA không claim session 6 câu.

Phần còn mở của Request 009 là engine first-class selected 3-question session set/target từ pool mở rộng; sau khi AI2 publish contract, AI3 sẽ khóa E2E pool >=6 nhưng session/progress/result vẫn 3.

## 18. Next AI3 actions

1. Theo dõi release lane đóng Request 008; không sửa `tools/build/*` khi đang có owner/WIP khác.
2. Theo dõi AI2 Request 009; khi có first-class target/selected-set contract, thêm >=6-pool → 3-question E2E cho session/progress/result.
3. Khi AI2 commit generator `draw_segment_given_length`, chạy adaptive interaction regression rồi cập nhật status.
4. Khi các gate trên đóng, chạy full portable/installer/reinstall Math release regression và chốt strict DoD.
