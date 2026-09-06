# MATH LIVE STATUS

Updated: 2026-09-07
Definition: % dưới đây đo theo Definition of Done strict của Math, không lấy việc “mở được màn hình” làm DONE.

## Overall

- **Content: 98%** — 7 chương, 17 chủ đề, 67 lesson, 201 câu authored, prerequisite graph, difficulty progression, distractor rationale và semantic validator đều có. `MathContentDataSmoke`: **22/22 PASS** trên HEAD hiện tại.
- **Engine: 93%** — answer validation/equivalence, mastery/review/reward, idempotency, exact suspend/resume, authored bank, schema V4 lesson progress, lesson-targeted session, prerequisite unlock, targeted score/best score và targeted resume đã có contract first-class. Clean `MathSessionPersistenceRuntimeSmoke` tại `656a94b`: **92 assertions PASS**.
- **UI: 96%** — Math Hub, chapter/topic/lesson, theory/example, adaptive mission, lesson-targeted practice, locked/unlocked, exact resume, choice/typed/interaction answer surfaces, result counters + lesson score/best score và Flow 5 đều chạy bằng contract thật.
- **Test: 93%** — Child UI **1133 assertions PASS**; all-201 authored render sweep PASS; targeted Flow 5 UI E2E PASS; persistence targeted/resume **92 assertions PASS**; content **22/22 PASS**. Full old-style solution build vẫn còn blocker Data/SQLite.
- **E2E: 88%** — Home → Math Hub → lesson → targeted authored practice → result → persisted lesson progress → Hub refresh → prerequisite unlock PASS; exact resume PASS; adaptive mission baseline PASS. Chưa có release-clean full solution gate và advanced result fields chưa được UI consume từ commit ổn định.
- **Tổng Math: ~91%** theo strict production Definition of Done hiện tại.

## P0

- Không phát hiện P0 Math UI/integration mới trong wave AI3-005.

## P1 còn mở

1. **Production clean build Data/SQLite:** `WAHU.Data.csproj` old-style net48 vẫn là blocker của full clean solution build trên `dotnet msbuild`. SDK-style x86/net48 harness compile cùng production Data source + SQLite thật PASS nhưng chưa thay thế production clean build gate.
2. **Advanced result presentation:** upstream `8b32944` đã publish ổn định `MasteryChanges`, target mastery before/after/delta và `NextLessonId/Title`; AI3-005 chưa mở rộng scope để consume, sẽ tích hợp ở wave kế tiếp. Numeric XP vẫn chưa có product rule first-class.
3. **Adaptive segment generator:** UI interaction contract và authored interaction đều render được; case generator `draw_segment_given_length` đang thấy trong WIP Learning nhưng chưa nằm trong HEAD ổn định. Child UI smoke chỉ test UI contract; generator behavior thuộc engine smoke AI2.

## Blocker đã đóng

- **Exact resume:** CLOSED — `Suspend/Resume`, exact open question, no duplicate/stale replay.
- **Authored bank runtime:** CLOSED — 201 câu loadable/traceable theo lesson.
- **Lesson target + prerequisite unlock:** CLOSED upstream bởi `656a94b` — session mode `lesson`, target lesson durable, access snapshot, completion/score, unlock.
- **Typed authored answers:** CLOSED AI3-005 — numeric input, word problem, expression và unit dùng typed-answer surface thay vì fatal vì thiếu choices.
- **All authored UI compatibility:** CLOSED AI3-005 — **201/201** câu render được: **109 typed + 91 choice + 1 interaction**.
- **Flow 5:** CLOSED AI3-005 — hoàn thành prerequisite qua UI thật → lesson progress 100% persisted → Hub reload → bài phụ thuộc unlock.
- **Result route mismatch:** CLOSED AI3-004 — `Về thư viện Toán` đúng route thực tế.

## Current verified gates

- `src/App/WAHUKidsLearn.csproj` Release x86 (`BuildProjectReferences=false`, với dependency artifacts đã dựng): **PASS**.
- `tests/ChildUiRuntimeSmoke` Release x86: **PASS — 1133 assertions**.
- All-201 authored answer-surface sweep: **PASS — 201/201**.
- Targeted lesson UI Flow 5: **PASS**.
- Clean detached worktree tại `656a94b` + đúng 3 file AI3:
  - App Release x86: **PASS**.
  - MathSessionPersistenceRuntimeSmoke: **PASS — 92 assertions**.
  - ChildUiRuntimeSmoke: **PASS — 1133 assertions**.
  - Data SDK net48/x86 artifact: **0 warning / 0 error**.
  - Session SDK net48/x86 artifact: **0 warning / 0 error**.
- HEAD hiện tại sau AI1 content waves (`ee7079b`): App Release **PASS**, Child UI **1133 PASS**, Math content **22/22 PASS**.
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
Source đã qua clean gate, đang chốt commit.

- Hub consume `MathLessonProgressService.GetAllAccess()`.
- Locked lesson vẫn đọc theory được; practice CTA bị khóa và nêu prerequisite thiếu.
- Unlocked lesson có `Luyện 3 câu bài này`; completed lesson có `Luyện lại` + last/best score.
- `MathLessonForm` nhận `lessonId` và dùng `MathSessionCoordinator(..., lessonId)`.
- Result targeted dùng official `LessonScorePercent` / `LessonBestScorePercent`, không tự tính ở UI.
- Typed answer hỗ trợ numeric / word_problem / expression / unit + Enter submit + empty guard + accessibility.
- 201-question sweep và Flow 5 UI E2E PASS.

## Next integration gates

1. Chốt/push AI3-005.
2. Khi AI2 commit advanced result contract ổn định, consume mastery delta + next lesson trực tiếp từ summary.
3. Đóng production SQLite reference/toolchain và chạy full clean solution + toàn bộ smoke/E2E release gate.
4. Sau full clean build, chạy regression packaging/installer để xác nhận Math content/schema V4 được đóng gói đầy đủ.
