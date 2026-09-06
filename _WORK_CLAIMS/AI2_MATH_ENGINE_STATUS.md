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
- `tests/MathDataEngineRuntimeSmoke`: PASS — **34 assertions**.
- `tests/MathSessionPersistenceRuntimeSmoke`: PASS — **43 assertions**.
- PowerShell release/build scripts: parse PASS ở wave schema V3.
- `git diff --check` / staged `--check`: PASS ở các wave đã commit.
- Máy A Wahu 4 không có Visual Studio MSBuild mà old-style net48 solution dùng để resolve SQLite; AI2 dùng SDK-style x86/net48 smoke harness link đúng production source + SQLite thật để test Data/Session, không đổi production toolchain.

## Session

- start / next / submit / complete: PASS.
- `Suspend`: PASS — giữ active session + runtime checkpoint, không reward.
- `Dispose`: PASS — mặc định suspend thay vì abort.
- exact resume: PASS — giữ nguyên session id, target count, seed, committed counters và open question.
- open-question idempotency: PASS — gọi `NextQuestion()` lại khi câu đang mở trả đúng cùng câu, không regenerate.
- deterministic generation qua restart: PASS — per-question seed = stable hash `(session seed, ordinal, template id)`.
- stale open question sau committed answer: PASS — phát hiện qua `attempt_commit_key`, không hiển thị/ghi điểm lại.
- corrupted current-question cache: PASS — chỉ bỏ cache câu mở, giữ committed attempts/mastery/progress.
- double-submit: PASS ở engine lane — coordinator serialize submit + DB semantic idempotency.
- retry/skip: **chưa có API first-class**; hiện mỗi submit kết thúc question và sai có thể trigger prerequisite repair cho câu sau.

## Persistence

- committed attempt transaction: PASS.
- immutable attempt history: PASS (migration V2 trigger).
- semantic attempt idempotency: PASS (schema V3 `attempt_commit_key`).
- V1 → V3: PASS với pre-migration verified backup.
- V2 → V3 có historical duplicate semantic attempt: PASS, không xóa lịch sử; key pin vào earliest committed attempt.
- session seed / target / generated ordinal: PASS durable.
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
- lesson score / numeric XP: chưa có first-class contract hiện tại.
- lesson unlock/prerequisite/daily streak: chưa có engine first-class hiện tại; cần chốt theo lesson catalog/product contract, không invent ở UI.

## Known edge cases

PASS: `0`, số âm, số rất lớn, decimal, fraction, malformed, empty, divide-by-zero, unit, expression safety, duplicate event, same-payload replay, conflict payload, app close giữa lesson, exact resume, stale cached open question, corrupted cached question, V1/V2 migration.

Còn phải làm: retry semantics, hint/first-try scoring semantics, skip policy nếu product cho phép, lesson-level score/completion/progress/mastery/unlock contract, stale-state/concurrency regression mở rộng và network/write-failure behavior ở integration boundary.

## Commits

- `11fa47d` — `feat(toán): hoàn thiện kiểm tra đáp án tương đương` — pushed.
- `a9dfcdf` — `feat(toán): khóa idempotency và nâng persistence lên schema v3` — pushed.
- `a1d5146` — `feat(toán): lưu trạng thái phiên học để tiếp tục` — pushed.
- `beb0c0e` — `feat(toán): tiếp tục chính xác phiên học sau khi đóng app` — pushed.

## Blocker / coordination

AI1/AI3 đang commit song song content/Math Hub/UI trên cùng `main`. AI2 chỉ stage file/hunk thuộc engine; không revert/commit hộ `PackVersion`, content pack, generator/template UI hoặc Math Hub thay đổi của lane khác.
