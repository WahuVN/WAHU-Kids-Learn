# MATH LIVE STATUS

Updated: 2026-09-07
Definition: % dưới đây đo theo Definition of Done strict của Math, không lấy việc “mở được màn hình” làm DONE.

## Overall

- **Content: 96%** — 7 chương, 17 chủ đề, 67 lesson, 201 câu, prerequisite graph và semantic validator đã có; MathContentDataSmoke hiện **16/16 PASS**. Phần còn lại chủ yếu là product/runtime consumption chứ không phải thiếu content hàng loạt.
- **Engine: 87%** — answer validation, mastery/review/reward, schema V3 idempotency, exact suspend/resume, deterministic open-question restore, `interaction_integer` finalization và loader ngân hàng authored 201 câu đều PASS. Coordinator vẫn chưa có lesson-targeted session/unlock và lesson score/XP/retry/skip contract first-class.
- **UI: 92%** — Math Hub, chapter/topic/lesson, lesson content, mastery thật, chapter progress, “Tiếp tục bài đang học”, adaptive exercise, exact resume, generated interaction và result baseline bằng counters durable đều có. Còn locked/unlocked first-class và result nâng cao score/mastery delta/next lesson.
- **Test: 85%** — Child UI **674 assertions PASS**, Math session persistence/authored-bank **64 assertions PASS**, Math engine baseline PASS, Math content **16/16 PASS**. Full old-style solution build vẫn bị Data/SQLite compile-reference chặn.
- **E2E: 68%** — Home → Math Hub → lesson → adaptive exercise → result → quay lại Math Hub baseline chạy; Flow 2 exact resume PASS; generated interaction PASS; Flow 5 prerequisite unlock và lesson-targeted practice chưa có product contract.
- **Tổng Math: ~84%** theo strict production Definition of Done hiện tại.

## P0

- Không phát hiện P0 UI/integration mới trong wave AI3-004.

## P1

1. **Lesson-targeted session + prerequisite unlock:** catalog có prerequisite graph nhưng engine chưa publish first-class API/state để mở đúng lesson và quyết định locked/unlocked. AI3 không fake “Luyện bài này” hoặc lock frontend.
2. **Clean production build graph:** `WAHU.Data.csproj` old-style net48 vẫn không resolve `System.Data.SQLite` compile reference bằng `dotnet msbuild`; SDK x86/net48 smoke harness dùng cùng production source + SQLite thật vẫn PASS.
3. **Result contract nâng cao:** baseline result đã hiển thị attempts / tự làm đúng / đúng nhờ gợi ý / cần luyện lại / số kỹ năng / garden reward từ summary thật; vẫn chưa có first-class lesson score / mastery delta summary / numeric XP / next-lesson contract.

## Closed blockers in current wave

- **Exact resume:** CLOSED upstream bởi `a1d5146` + `beb0c0e` + regression `a36c4cb`; AI3 đã consume `Suspend/Resume` đúng semantics.
- **Generated interaction finalizer:** CLOSED upstream bởi `9e275de`; `interaction_integer` được giữ nguyên và không sinh fake choices.
- **UI stop/close semantics:** CLOSED AI3 wave 003 — “Dừng và học tiếp sau” và close lesson dùng `Suspend`, runtime fatal error vẫn dùng `Abort`.
- **Result route mismatch:** CLOSED AI3 wave 004 — CTA completion/fatal dùng `Về thư viện Toán`, đúng hành vi thực tế quay về Math Hub thay vì ghi sai `Về khu vườn`.

## Current tests

- `src/Learning/WAHU.Learning.csproj` Release x86: **PASS**.
- `src/App/WAHUKidsLearn.csproj` Release x86 (`BuildProjectReferences=false`, với dependency artifacts hiện có): **PASS**.
- `tests/ChildUiRuntimeSmoke` Release x86 (`BuildProjectReferences=false`): **PASS**.
- ChildUiRuntimeSmoke: **PASS — 674 assertions**.
- MathSessionPersistenceRuntimeSmoke: **PASS — 64 assertions** (resume + authored-bank loader/traceability).
- MathEngineRuntimeSmoke baseline: **PASS — 47 assertions** ở gate trước; generated interaction được khóa thêm trong Child UI smoke bằng generator thật.
- MathContentDataSmoke: **PASS — 16/16 tests**.
- `WAHUKidsLearn.sln` old-style Release x86 clean build: **vẫn FAIL tại `src/Data` / `System.Data.SQLite` reference resolution** trên toolchain `dotnet msbuild` hiện có.

## Integration wave status

### AI3-001 — interactive segment answer

- Commit: `c63e110` — `Toán UI: hỗ trợ vẽ đoạn thẳng tương tác`.
- Mouse + keyboard + accessibility + hint/result state: PASS.
- Sau upstream `9e275de`, Child UI smoke dùng **MathQuestionGenerator thật** và xác nhận:
  - `AnswerKind == interaction_integer`;
  - `DisplayChoices.Count == 0`;
  - có `segmentdraw|...` payload;
  - UI chọn hai đầu mút, tính đúng độ dài và serialize đúng answer.
- Generated interaction E2E: **PASS**.

### AI3-002 — Math Hub + lesson content integration

- Commit: `11d7914` — `Toán UI: hoàn thiện hub và nội dung bài học`.
- Consume `lesson_catalog_v1.json` thật.
- Flow: Home → Toán lớp 2 → chương → chủ đề/bài → objective → concept → worked example → practice metadata → prerequisite presentation.
- Progress/mastery đọc từ `LearnerSessionService.LoadSkillSnapshots()`, không fake frontend.
- Missing/corrupt catalog: child-safe state, không white-screen.
- Responsive 1180×760 + 900×640: PASS.

### AI3-003 — continue progress + exact resume integration

- Commit: `2d2813c` — `Toán UI: hoàn thiện tiếp tục bài và resume phiên học`.
- Math Hub:
  - chapter hiện số bài đã học + số bài `STABLE` từ state engine thật;
  - CTA “Tiếp tục bài đang học” chọn lesson active/review gần nhất theo `LastSeenAtUtc`;
  - không có progress thì CTA disabled;
  - sau adaptive mission, chapter progress + continue target refresh lại.
- Math Lesson:
  - consume `TargetQuestionCount` + `CompletedQuestionCount` khi resume;
  - resume đúng open question hiện thông báo child-safe “tiếp tục đúng câu đang làm dở”;
  - corrupt open cache thông báo phần đã làm vẫn an toàn và chuyển câu mới;
  - nút stop đổi thành “Dừng và học tiếp sau”;
  - user stop / window close gọi `Suspend`, không `Abort`;
  - fatal runtime path vẫn `Abort` fail-closed.
- Flow 2 engine regression:
  - same session id;
  - exact open question id/content;
  - persisted target count;
  - committed counters;
  - no stale replay / no duplicate attempt;
  - corrupt cache keeps committed progress;
  - deterministic generation across restart;
  - suspend grants no reward.

### AI3-004 — result baseline bằng dữ liệu durable

- Source: READY, đang chốt commit hiện tại.
- Result hiển thị đúng các counters thật từ `MathSessionSummary`: attempts, independent correct, hinted correct, cần luyện lại, distinct skills.
- Counter presentation được clamp fail-safe để dữ liệu bất thường không tạo số hiển thị vượt attempt.
- Garden reward/unlock vẫn consume từ summary/`LessonCompletionVisual`, không fake XP.
- Accessibility: feedback/support có summary text riêng cho screen reader.
- Route label sửa từ `Về khu vườn` thành `Về thư viện Toán` vì hành vi thật là đóng lesson và quay về Math Hub.
- Child UI regression tăng lên **674 assertions PASS**.

## Latest owner commits observed

- Upstream prerequisite/content guard: `2ac3027` — `Toán: siết hard guard thời gian và thứ tự prerequisite`.
- AI2 resume: `a1d5146`, `beb0c0e`, regression `a36c4cb`.
- AI2 interaction finalizer: `9e275de` — `fix(toán): giữ dạng trả lời tương tác khi hoàn thiện câu`.
- AI2 authored bank loader: `a5119d4` — `feat(toán): nạp ngân hàng 201 câu theo bài học`.
- AI3 latest committed: `2d2813c`; wave AI3-004 đang được chốt từ trạng thái hiện tại.

## Next integration gates

1. Publish lesson-target/prerequisite unlock contract → AI3 thêm “Luyện bài này”, locked/unlocked state và Flow 5.
2. Publish result-level score/mastery-delta/XP/next-lesson contract → nâng result baseline hiện có, không thay counters durable đang dùng.
3. Fix production SQLite reference/toolchain → full clean solution build + complete regression gate.
4. Sau targeted lesson contract, chạy E2E toàn bộ lesson/prerequisite path thay vì chỉ adaptive mission toàn Math.
