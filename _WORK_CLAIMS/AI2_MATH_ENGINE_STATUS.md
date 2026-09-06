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
- `tests/MathDataEngineRuntimeSmoke`: PASS — **70 assertions** (idempotency + terminal-session guard + optimistic skill-state guard + true cross-process mastery/session contention smoke).
- `tests/MathSessionPersistenceRuntimeSmoke`: PASS — **171 assertions** (authored bank + targeted lesson + prerequisite unlock + exact resume + mastery delta + next lesson + corrupt authored cursor recovery + retry/resume/anti-double-submit + stale coordinator/skill guards + injected write-failure rollback/retry).
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
- retry: PASS first-class opt-in — initial intent và explicit retry intent tách riêng, tối đa 2 attempts/question; pending retry survive suspend/resume, duplicate initial intent không tự tiêu attempt 2. API one-shot cũ vẫn backward-compatible.
- skip: **chưa có API first-class**; prerequisite repair tự động chỉ áp dụng cho adaptive session, không chen câu ngoài lesson-targeted set.

## Persistence

- committed attempt transaction: PASS.
- immutable attempt history: PASS (migration V2 trigger).
- semantic attempt idempotency: PASS (schema V3 `attempt_commit_key`).
- terminal-session write guard: PASS — attempt mới chỉ insert khi session còn active; exact replay đã commit vẫn hợp lệ sau complete/abort.
- optimistic child-skill guard: PASS — mastery-bearing write kiểm expected mastery score + attempts count trong transaction; stale snapshot bị rollback trước attempt/key/mastery/review.
- injected write-failure rollback: PASS — failure tại `mastery_event` rollback toàn attempt/key/mastery/child_skill/review chain; coordinator giữ câu mở và retry sạch.
- single-active child+subject session guard: PASS — `BeginSession` atomic guard chặn duplicate active session khi hai process cold-start đồng thời; terminal state giải phóng slot.
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
- retry/first-try scoring: PASS — first-wrong pending không đổi mastery; retry finalize đúng một mastery update; retry-correct dùng assisted weight và không tính independent; explicit hint vẫn được theo dõi riêng.
- review scheduling: PASS baseline.
- completed-session garden reward idempotent: PASS.
- attempt replay không nhân mastery/child_skill/review: PASS.
- resume counters được reconstruct từ committed DB attempts, không từ in-memory cache: PASS.
- stale mastery risk do concurrent submit cùng coordinator: mitigated bằng submit gate + semantic key; regression replay PASS.
- stale coordinator/process sau terminal session: PASS — persistence atomic guard chặn attempt mới sau complete/abort, không ghi semantic key/mastery phụ.
- stale mastery snapshot giữa coordinator/process: PASS — expected skill state chống lost-update; snapshot cũ không được overwrite `child_skill`, exact replay vẫn idempotent.
- true cross-process contention: PASS lặp 3 vòng — hai worker/process độc lập cùng race từ một expected snapshot, đúng một write commit và durable mastery chain không mất update.
- failed-commit behavior recovery: PASS — observation chưa durable bị loại; retry sau fault có `RecentAttemptCount` đúng theo committed DB, không double behavior/mastery.
- lesson completion/progress: PASS first-class persistence — `started_count`, `completed_count`, last/best score.
- lesson score: PASS first-class cho targeted session; score = correct / authored target count × 100, persisted cùng transaction session completion.
- lesson prerequisite unlock: PASS first-class — prerequisite được thỏa bởi completed targeted lesson; legacy `STABLE` skill được công nhận để không khóa ngược dữ liệu cũ.
- mastery-delta result: PASS first-class — per-skill before/after/delta được reconstruct từ committed mastery events, kể cả sau suspend/resume; targeted result có shortcut cho target skill.
- next lesson result: PASS first-class — chỉ publish bài liền kế trong curriculum sau durable completion và chỉ khi bài đó unlocked.
- numeric XP / daily streak: chưa có first-class contract; UI không tự invent.

## Known edge cases

PASS: `0`, số âm, số rất lớn, decimal, fraction, malformed, empty, divide-by-zero, unit, expression safety, duplicate event, same-payload replay, conflict payload, app close giữa lesson, exact resume, stale cached open question, corrupted cached question, V1/V2/V3/V4 migration, locked lesson direct-start, targeted suspend/resume, prerequisite completion → unlock, retry suspend/resume, retry-correct/retry-wrong, duplicate initial retry intent, exact replay sau terminal state, stale coordinator submit sau complete/abort, stale child-skill snapshot / lost-update guard, cross-process contention, injected mid-transaction write failure + clean retry.

Còn phải làm: skip policy nếu product cho phép, numeric XP/daily streak nếu product cần; remote/network answer transport hiện không phải first-class path của Math engine local-DB nên không invent retry protocol chưa tồn tại. Lesson-target/completion/score/prerequisite unlock + mastery-delta/next-lesson + retry/first-try/hint scoring + terminal-session + stale-skill + true cross-process contention + write-failure rollback/recovery đã đóng.

## Commits

- `11fa47d` — `feat(toán): hoàn thiện kiểm tra đáp án tương đương` — pushed.
- `a9dfcdf` — `feat(toán): khóa idempotency và nâng persistence lên schema v3` — pushed.
- `a1d5146` — `feat(toán): lưu trạng thái phiên học để tiếp tục` — pushed.
- `beb0c0e` — `feat(toán): tiếp tục chính xác phiên học sau khi đóng app` — pushed.
- `a5119d4` — `feat(toán): nạp ngân hàng 201 câu theo bài học` — pushed.
- `656a94b` — `feat(toán): thêm phiên học theo bài và mở khóa prerequisite` — pushed.
- `8b32944` — `feat(toán): publish mastery delta và bài tiếp theo` — pushed.
- `7f79367` — `fix(toán): khôi phục đúng câu authored khi cache hỏng` — pushed.
- `7c9a9ea` — `feat(toán): thêm retry an toàn và chấm first-try` — pushed.
- `081f004` — `fix(toán): chặn ghi câu mới sau khi phiên đã kết thúc` — pushed.
- `c26e4e5` — `fix(toán): chặn ghi đè mastery từ snapshot cũ` — pushed.
- `641b7b4` — `test(toán): khóa race mastery giữa nhiều process` — pushed.
- `400fd0c` — `fix(toán): phục hồi state sau lỗi ghi đáp án` — pushed.
- single-active learner session guard — đang chốt selective commit hiện tại.

## Blocker / coordination

AI1/AI3 đang sửa song song content/Math Hub/UI trên cùng `main`. AI2 chỉ stage file/hunk thuộc engine/persistence/test/release contract; không commit hộ content pack 1.9, generator/template hoặc UI thay đổi của lane khác. `PackVersion` trong selective commit phải tiếp tục khớp manifest đang commit ở `HEAD` cho tới khi lane content publish version mới.
