# MATH LIVE STATUS

Updated: 2026-09-07
Definition: % dưới đây đo theo Definition of Done mở rộng của 3 lane hiện tại, không phải chỉ vertical-slice demo.

## Overall

- **Content: 40%** — generator/template coverage đã rộng, nhưng lesson catalog/static bank/validator đầy đủ đang do AI1 triển khai.
- **Engine: 66%** — answer validation đã mạnh; session/mastery/reward baseline chạy; idempotency + exact resume + lesson prerequisite contract chưa xong.
- **UI: 72%** — roadmap, choice exercise, hint, feedback, completion, error-safe flow đã có; interactive segment UI đã ready; lesson catalog UI/result mastery/locked lessons chưa xong.
- **Test: 62%** — Child UI smoke 605 assertions PASS, AI2 MathEngine smoke 47 assertions PASS; full graph còn Data/SQLite blocker.
- **E2E: 40%** — baseline adaptive mission chạy; exact restart/resume, prerequisite unlock, lesson-catalog flow và generated interaction chưa đạt gate.
- **Tổng Math: ~55%** theo strict production Definition of Done hiện tại.

## P0

- Không phát hiện P0 UI mới trong wave AI3 hiện tại.

## P1

1. `MathQuestionGenerator.FinalizeAnswerOptions()` làm mất `interaction_integer` và biến bài vẽ đoạn thành choice — owner AI2, Request 004.
2. Exact resume active Math session chưa tồn tại — owner AI2 persistence/resume wave.
3. Semantic duplicate-submit/idempotency engine đang được AI2 sửa.
4. Full clean build/test graph bị `System.Data.SQLite` compile-reference resolution ở `src/Data` chặn sau restore.

## P2 / missing product flow

- Machine-readable chapter/topic/lesson catalog chưa hoàn tất — AI1.
- Theory/objective/concept/worked example UI chờ catalog — AI3 integration sau AI1.
- Lesson prerequisite/unlock contract chưa có — AI1 + AI2 contract, AI3 presentation sau đó.
- Result screen chưa có numeric score/mastery delta/next lesson vì engine/content chưa publish contract.
- Loading/empty state cho lesson catalog chưa thể đóng khi catalog chưa tồn tại.

## Current tests

- AI3 App Release x86 build (`BuildProjectReferences=false`): **PASS**.
- AI3 ChildUiRuntimeSmoke Release x86 build (`BuildProjectReferences=false`): **PASS**.
- ChildUiRuntimeSmoke: **PASS — 605 assertions**.
- AI2 MathEngineRuntimeSmoke: **PASS — 47 assertions** (theo `AI2_MATH_ENGINE_STATUS.md`).
- Full dependent `dotnet msbuild` after restore: **FAIL at Data/SQLite reference**, not AI3 source compile.

## Integration wave status

### AI3-001 — interactive segment answer

- Source: READY.
- Child UI smoke: PASS.
- Runtime-generated E2E: BLOCKED by AI2 Request 004.
- Files AI3 owns in this wave:
  - `src/App/MathInteractiveControls.cs`
  - `src/App/MathLessonForm.cs`
  - `src/App/WAHUKidsLearn.csproj`
  - `tests/ChildUiRuntimeSmoke/Program.cs`
  - `_WORK_CLAIMS/AI3_MATH_UI_AUDIT.md`
  - `_WORK_CLAIMS/MATH_LIVE_STATUS.md`
- Shared coordination updated:
  - `_WORK_CLAIMS/MATH_SHARED_CONTRACT_REQUESTS.md` Request 004 only.

## Latest owner commits observed

- AI1: `58b0ae8` — `Toán: audit nội dung và chốt inventory dữ liệu`.
- AI2: `11fa47d` — `feat(toán): hoàn thiện kiểm tra đáp án tương đương`.
- AI3: wave AI3-001 đang chờ commit sau final diff/test gate.

## Next integration gates

1. AI2 preserve `interaction_integer` -> generated segment smoke/E2E.
2. AI2 exact resume -> close/restart/resume Flow 2 + persistence Flow 3.
3. AI1 lesson catalog -> Math hub + lesson theory/example integration.
4. AI1/AI2 prerequisite contract -> locked/unlocked Flow 5.
5. Data/SQLite build blocker closed -> full clean build + all Math smoke/E2E.
