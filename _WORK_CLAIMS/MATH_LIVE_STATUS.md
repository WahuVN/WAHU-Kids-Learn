# MATH LIVE STATUS

Updated: 2026-09-07
Definition: % dưới đây đo theo Definition of Done strict của Math, không lấy việc “mở được màn hình” làm DONE.

## Overall

- **Content: 98%** — 7 chương, 17 chủ đề, 67 lesson, 201 câu authored, prerequisite graph, difficulty progression, distractor rationale, worked-example separation và semantic validator đều có. `MathContentDataSmoke`: **23/23 PASS** tại clean HEAD `9682572`.
- **Engine: 94%** — answer validation/equivalence, mastery/review/reward, idempotency, exact suspend/resume, authored bank, schema V4 lesson progress, lesson-targeted session, prerequisite unlock, targeted score/best score, mastery delta, next lesson và targeted resume đều có contract first-class. Clean `MathSessionPersistenceRuntimeSmoke`: **99 assertions PASS**.
- **UI: 97%** — Math Hub, chapter/topic/lesson, theory/example, adaptive mission, lesson-targeted practice, locked/unlocked, exact resume, choice/typed/interaction answers, result counters + lesson score/best score + mastery delta + next lesson đều consume contract thật.
- **Test: 94%** — Child UI **1138 assertions PASS**; all-201 authored render sweep PASS; targeted Flow 5 + advanced-result UI E2E PASS; persistence targeted/resume/result **99 assertions PASS**; content **23/23 PASS**. Full old-style solution build vẫn còn blocker Data/SQLite.
- **E2E: 90%** — Home → Math Hub → lesson → targeted authored practice → result → mastery/next-lesson presentation → persisted lesson progress → Hub refresh → prerequisite unlock PASS; exact resume PASS; adaptive mission baseline PASS. Chưa có release-clean full solution/packaging gate.
- **Tổng Math: ~92%** theo strict production Definition of Done hiện tại.

## P0

- Không phát hiện P0 Math UI/integration mới trong wave AI3-006.

## P1 còn mở

1. **Production clean build Data/SQLite:** `WAHU.Data.csproj` old-style net48 vẫn là blocker của full clean solution build trên `dotnet msbuild`. SDK-style x86/net48 harness compile cùng production Data source + SQLite thật PASS nhưng chưa thay thế production clean build gate.
2. **Adaptive segment generator:** UI interaction contract và authored interaction đều render được; case generator `draw_segment_given_length` đang thấy trong WIP Learning nhưng chưa nằm trong HEAD ổn định. Child UI smoke chỉ test UI contract; generator behavior thuộc engine smoke AI2.
3. **Release packaging/E2E:** static packaging path đã xác nhận copy đệ quy `content_packs` + `data/schema` và installer copy toàn publish tree, nhưng artifact mới nhất hiện có `0.1.41-dev` là build cũ từ `e299c41` / schema 2 nên thiếu lesson catalog, question bank và schema V4. Portable/Installer E2E hiện cũng chưa hard-guard 3 Math runtime JSON; đã mở Request 008 cho release lane.

## Blocker đã đóng

- **Exact resume:** CLOSED — `Suspend/Resume`, exact open question, no duplicate/stale replay.
- **Authored bank runtime:** CLOSED — 201 câu loadable/traceable theo lesson.
- **Lesson target + prerequisite unlock:** CLOSED upstream bởi `656a94b` — session mode `lesson`, target lesson durable, access snapshot, completion/score, unlock.
- **Typed authored answers:** CLOSED AI3-005 — numeric input, word problem, expression và unit dùng typed-answer surface thay vì fatal vì thiếu choices.
- **All authored UI compatibility:** CLOSED AI3-005 — **201/201** câu render được: **109 typed + 91 choice + 1 interaction**.
- **Flow 5:** CLOSED AI3-005 — hoàn thành prerequisite qua UI thật → lesson progress 100% persisted → Hub reload → bài phụ thuộc unlock.
- **Advanced result presentation:** CLOSED AI3-006 — mastery hiện tại/delta và `NextLessonTitleVi` hiển thị trực tiếp từ summary `8b32944`; adaptive mission dùng `ImprovedSkillCount`; không tự tính XP hay next lesson.
- **Result route mismatch:** CLOSED AI3-004 — `Về thư viện Toán` đúng route thực tế.

## Current verified gates

- Current workspace: App Release x86 targeted build **PASS**; ChildUiRuntimeSmoke **1138 assertions PASS**.
- All-201 authored answer-surface sweep: **PASS — 201/201**.
- Targeted lesson Flow 5 + advanced result presentation: **PASS**.
- Clean detached worktree tại `9682572` + đúng 2 file AI3-006:
  - App Release x86: **PASS**.
  - MathSessionPersistenceRuntimeSmoke: **PASS — 99 assertions**.
  - ChildUiRuntimeSmoke: **PASS — 1138 assertions**.
  - MathContentDataSmoke: **PASS — 23/23**.
  - `git diff --check`: **PASS**.
- Data/Session source không đổi giữa advanced-result commit `8b32944` và clean HEAD `9682572`; clean SDK artifacts đã được tái dùng có kiểm chứng.
- `WAHUKidsLearn.sln` old-style Release x86 clean build: **chưa đạt gate** vì production Data/SQLite reference/toolchain.

## Integration waves

### AI3-001 — interactive segment answer
Commit `c63e110`. Segment control, mouse/keyboard, hint/result/accessibility DONE. UI smoke hiện dùng trực tiếp `MathQuestion` interaction contract để không phụ thuộc generator-owned behavior.

### AI3-002 — Math Hub + lesson content
Commit `11d7914`. Consume lesson catalog thật; 7 chương / 17 chủ đề / 67 bài; theory/concept/worked example/prerequisite + safe error state.

### AI3-003 — continue + exact resume
Commit `2d2813c`. Continue lesson từ durable skill evidence; stop/close dùng `Suspend`; exact resume presentation DONE.

### AI3-004 — durable result baseline
Commit `37f0b97` + regression `b6a1ce4`. Attempts/independent/hinted/wrong/skills/garden reward từ summary thật; result route về Math Hub đúng.

### AI3-005 — targeted lesson + all answer surfaces
Commit `1436705` — `Toán UI: hoàn thiện luyện theo bài và toàn bộ dạng nhập đáp án`.

- Hub consume `MathLessonProgressService.GetAllAccess()`.
- Locked lesson vẫn đọc theory được; practice CTA bị khóa và nêu prerequisite thiếu.
- Unlocked lesson có `Luyện 3 câu bài này`; completed lesson có `Luyện lại` + last/best score.
- `MathLessonForm` nhận `lessonId` và dùng `MathSessionCoordinator(..., lessonId)`.
- Result targeted dùng official `LessonScorePercent` / `LessonBestScorePercent`, không tự tính ở UI.
- Typed answer hỗ trợ numeric / word_problem / expression / unit + Enter submit + empty guard + accessibility.
- 201-question sweep và Flow 5 UI E2E PASS.

### AI3-006 — mastery delta + bài tiếp theo
Commit `3905c09` — `Toán UI: hiển thị tiến bộ và bài tiếp theo`.

- Targeted result hiển thị `TargetSkillMasteryAfter` theo %, và `TargetSkillMasteryDelta` dương theo điểm phần trăm.
- Adaptive result dùng `ImprovedSkillCount` thay vì tự suy ra mastery change.
- `NextLessonId` + `NextLessonTitleVi` chỉ được presentation khi engine publish đủ cặp; UI không tự tìm bài khác hoặc tự mở lesson.
- Flow 5 E2E xác nhận support label nhận mastery + immediate next lesson thật từ coordinator.
- Numeric XP vẫn không hiển thị vì chưa có product contract first-class.

## Next integration gates

1. Release lane đóng Request 008: hard-guard 3 Math runtime JSON trong staged payload + portable/installer E2E, rồi rebuild artifact schema V4 từ commit hiện tại.
2. Đóng production SQLite reference/toolchain và chạy full clean solution + toàn bộ smoke/E2E release gate.
3. Theo dõi AI2 commit generator `draw_segment_given_length`, rồi khóa engine-owned adaptive interaction regression.
4. Khi có artifact mới, chạy portable/installer upgrade regression và xác nhận learner DB + lesson progress không mất.
