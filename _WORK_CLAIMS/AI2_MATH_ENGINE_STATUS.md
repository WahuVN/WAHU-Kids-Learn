# AI2 — Math engine status

Cập nhật: 2026-09-07
Branch: `main`

## Question types / answer validation

- `integer`: PASS — 0, âm, whitespace, dấu +, decimal/fraction tương đương.
- `interaction_integer`: PASS qua cùng numeric validator.
- `number`: PASS — BigInteger-backed exact rational comparison.
- `decimal`: PASS — `.` / `,`, exact và configurable tolerance.
- `fraction`: PASS — fraction equivalence, sign normalization, zero denominator rejected.
- `text`: PASS — Unicode normalized, case-insensitive, collapsed whitespace.
- `unit`: PASS — numeric equivalence + expected unit + aliases.
- `expression`: PASS — restricted arithmetic `+ - * / ( )`, no variables/functions, div-zero rejected.
- multiple valid answers: PASS qua `AcceptedAnswers`.
- unknown answer kind: safe fallback text only; không tự broaden parser.

## Tests

- `tests/MathEngineRuntimeSmoke`: PASS — **47 assertions**.
- `tests/MathDataEngineRuntimeSmoke`: PASS — **35 assertions**.
- `tests/MathSessionPersistenceRuntimeSmoke`: PASS — **111 assertions** (authored bank + targeted lesson + prerequisite unlock + exact resume + mastery delta + next lesson + corrupt authored cursor recovery).
- `tests/SQLiteRuntimeSmoke`: PASS — **166 assertions** trên Visual Studio MSBuild/net48/x86 production toolchain.
- `tests/LearningSessionRuntimeSmoke`: PASS — **794 assertions** trên Visual Studio MSBuild/net48/x86 production toolchain.
- PowerShell release/build scripts: schema V4 payload/bootstrap expectations đã cập nhật; parse/build gate PASS.
- `git diff --check` / staged `--check`: PASS ở các wave đã commit; wave V4 phải chạy lại trước commit.

## Session

- start / next / submit / complete: PASS.
- `Suspend`: PASS — giữ active session + runtime checkpoint, không reward.
- `Dispose`: PASS — mặc định suspend thay vì abort.
- exact resume: PASS — giữ nguyên session id, target count, seed, committed counters và open question.
- open-question idempotency: PASS — gọi `NextQuestion()` lại khi câu đang mở trả đúng cùng câu, không regenerate.
- deterministic generation qua restart: PASS — per-question seed = stable hash `(session seed, ordinal, template id)`.
- stale open question sau committed answer: PASS — phát hiện qua `attempt_commit_key`, không hiển thị/ghi điểm lại.
- corrupted current-question cache: PASS — chỉ bỏ cache câu mở, giữ committed attempts/mastery/progress; lesson-mode reconcile authored cursor về committed ordinal để không skip câu.
- double-submit: PASS ở engine lane — coordinator serialize submit + DB semantic idempotency.
- lesson-targeted session: PASS — constructor nhận `lessonId`, engine lấy đúng authored practice set của lesson theo thứ tự basic → medium → application.
- prerequisite guard: PASS — lesson bị khóa bị chặn ngay ở coordinator, kể cả caller bypass UI.
- targeted exact resume: PASS — persist/restore `session_mode=lesson`, `target_lesson_id`, committed counters và exact open authored question.
- retry/skip: **chưa có API first-class**; hiện mỗi submit kết thúc question; prerequisite repair tự động chỉ áp dụng cho adaptive session, không chen câu ngoài lesson-targeted set.

## Persistence

- committed attempt transaction: PASS.
- immutable attempt history: PASS (migration V2 trigger).
- semantic attempt idempotency: PASS (schema V3 `attempt_commit_key`).
- schema V4: PASS — thêm `session_mode`, `target_lesson_id`, `math_lesson_progress`; checksum/tamper guard và deployment payload gate đã có.
- V1 → V4: PASS với pre-migration verified backup; migration history giữ đủ V1/V2/V3/V4.
- V2 → V3 historical duplicate semantic attempt: PASS, không xóa lịch sử; key pin vào earliest committed attempt; sau đó V4 apply bình thường.
- session seed / target / generated ordinal / mode / targeted lesson id: PASS durable.
- current question / selection / started timestamp / forced repair: PASS durable.
- resume after close: PASS.
- complete/abort removes runtime checkpoint: PASS.
- suspend leaves session resumable: PASS.
- refresh/restart farming reward: PASS regression — suspend không reward; completed reward vẫn unique theo completed session.

## Progress / mastery

- independent vs hinted mastery: PASS baseline.
- review scheduling: PASS baseline.
- completed-session garden reward idempotent: PASS.
- attempt replay không nhân mastery/child_skill/review: PASS.
- resume counters được reconstruct từ committed DB attempts, không từ in-memory cache: PASS.
- stale mastery risk do concurrent submit cùng coordinator: mitigated bằng submit gate + semantic key; regression replay PASS.
- lesson completion/progress: PASS first-class persistence — `started_count`, `completed_count`, last/best score.
- lesson score: PASS first-class cho targeted session; score = correct / authored target count × 100, persisted cùng transaction session completion.
- lesson prerequisite unlock: PASS first-class — prerequisite được thỏa bởi completed targeted lesson; legacy `STABLE` skill được công nhận để không khóa ngược dữ liệu cũ.
- mastery-delta result: PASS first-class — per-skill before/after/delta được reconstruct từ committed mastery events, kể cả sau suspend/resume; targeted result có shortcut cho target skill.
- next lesson result: PASS first-class — chỉ publish bài liền kế trong curriculum sau durable completion và chỉ khi bài đó unlocked.
- numeric XP / daily streak: chưa có first-class contract; UI không tự invent.

## Known edge cases

PASS: `0`, số âm, số rất lớn, decimal, fraction, malformed, empty, divide-by-zero, unit, expression safety, duplicate event, same-payload replay, conflict payload, app close giữa lesson, exact resume, stale cached open question, corrupted cached question, V1/V2/V3/V4 migration, locked lesson direct-start, targeted suspend/resume, prerequisite completion → unlock.

Còn phải làm: retry semantics, hint/first-try scoring semantics, skip policy nếu product cho phép, numeric XP/daily streak nếu product cần, stale-state/concurrency regression mở rộng và network/write-failure behavior ở integration boundary. Lesson-target/completion/score/prerequisite unlock + mastery-delta/next-lesson result contract đã đóng.

## Commits

- `11fa47d` — `feat(toán): hoàn thiện kiểm tra đáp án tương đương` — pushed.
- `a9dfcdf` — `feat(toán): khóa idempotency và nâng persistence lên schema v3` — pushed.
- `a1d5146` — `feat(toán): lưu trạng thái phiên học để tiếp tục` — pushed.
- `beb0c0e` — `feat(toán): tiếp tục chính xác phiên học sau khi đóng app` — pushed.
- `a5119d4` — `feat(toán): nạp ngân hàng 201 câu theo bài học` — pushed.
- `656a94b` — `feat(toán): thêm phiên học theo bài và mở khóa prerequisite` — pushed.
- `8b32944` — `feat(toán): publish mastery delta và bài tiếp theo` — pushed.
- Request 007 corrupt authored cursor recovery — regression PASS và được chốt trong wave selective hiện tại.

## Blocker / coordination

AI1/AI3 đang sửa song song content/Math Hub/UI trên cùng `main`. AI2 chỉ stage file/hunk thuộc engine/persistence/test/release contract; không commit hộ content pack 1.9, generator/template hoặc UI thay đổi của lane khác. `PackVersion` trong selective commit phải tiếp tục khớp manifest đang commit ở `HEAD` cho tới khi lane content publish version mới.
