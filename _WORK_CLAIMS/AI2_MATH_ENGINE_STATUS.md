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

- `tests/MathEngineRuntimeSmoke`: PASS — **72 assertions** (baseline equivalence + adversarial Unicode/NFD, int overflow, answer-length, unit alias, expression depth/token-bomb, Unicode operators và invalid per-question whitelist fail-closed).
- `tests/MathDataEngineRuntimeSmoke`: PASS — **104 assertions** (idempotency + terminal-session guard + optimistic skill-state guard + true cross-process mastery/session contention + atomic startup + subject/child-scoped dangling recovery).
- `tests/MathSessionPersistenceRuntimeSmoke`: PASS — **7199 assertions** (toàn bộ gate cũ + real 402-bank selector breadth + targeted write-failure/corruption repair + deterministic authored runtime IDs + concurrent pending-retry semantics + generated/open-ordinal self-heal + late-review transaction rollback + operational DB failure fail-safe).
- `tests/SQLiteRuntimeSmoke`: PASS — **179 assertions** trên Visual Studio MSBuild/net48/x86 production toolchain.
- `tests/LearningSessionRuntimeSmoke`: PASS — **800 assertions** trên Visual Studio MSBuild/net48/x86 production toolchain.
- Request 009: CLOSED tại `05cdb2a` — authored pool >=6 dùng selected set đúng 3 câu (1 basic + 1 medium + 1 application), deterministic theo seed/lesson, persisted qua checkpoint JSON; retry/resume/corrupt-open recovery giữ nguyên selected set và complete sau 3 câu. Synthetic pool-6 regression + legacy path đạt **264 assertions PASS**.
- Request 010: CLOSED tại `7afbb7b` — authored expression preserve `allowed_operators`; `75`, `100 - 30 + 5`, `70 + 5` PASS; `15*5`, `150/2` FAIL; JSON roundtrip giữ whitelist; expression legacy không whitelist vẫn backward-compatible.
- Request 006: CLOSED tại `3cf7fc6` — integer `answer_unit` được preserve qua authored loader -> runtime instance -> current-question JSON -> coordinator resume; raw grading vẫn chỉ nhận số, còn feedback outcome xuất `N đơn vị` cho UI. Persistence **277 assertions PASS**.
- Selected-set corruption stress: CLOSED tại `3b8a10b` — malformed/missing persisted selection metadata được dựng lại deterministic theo seed+lesson trong cùng pack, checkpoint được sửa và ordinal tiếp tục đúng; same seed giữ cùng ordered set.
- V5 pack-identity hardening: DB trigger reject partial `pack_id/pack_version` pair; rejected write leaves complete pair intact and session remains resumable.
- Selected-set stress: synthetic pool-6 chạy 16 seed liên tiếp, mỗi bucket basic/medium/application xoay đủ 2 biến thể, target luôn 3. Hardening mới trên **real 402-bank** quét đủ 67 lesson × 3 bucket × 16 seed, selector deterministic/in-range và mỗi bucket xoay đủ 2 variant; persistence tổng **7080 assertions PASS**.
- Targeted selected-set write-failure stress: injected `mastery_event` failure giữ nguyên selected checkpoint byte-for-byte, giữ exact open basic và counters=0, không ghost attempt/mastery; bỏ trigger rồi retry commit đúng một lần và tiếp tục đúng selected medium.
- Selected-set semantic-corruption repair matrix: JSON thiếu key, duplicate IDs, swap sai bucket và unknown ID đều self-heal về deterministic ordered set, giữ committed progress và tiếp tục đúng medium ordinal.
- Concurrent targeted ordinal hardening: hai coordinator có thể giữ hai runtime GUID khác nhau cho cùng selected medium; nếu một coordinator đã finalize medium, coordinator stale bị reject một lần rồi reconcile theo stable `ContentQuestionId`, counters lên đúng 2 và chuyển application; stale cached medium sau restart cũng bị bỏ. Rebuild targeted history fail-closed nếu finalized authored IDs trùng hoặc sai selected ordinal order.
- Operational DB failure guard: khóa file SQLite tạm thời làm `Start()` surface operational error; session cũ vẫn `active`, runtime checkpoint còn nguyên và resume đúng session ngay sau khi lock được gỡ — không bị classify nhầm thành corrupt/quarantine.
- Deterministic authored runtime identity: lesson-mode `QuestionId` được derive ổn định từ `(session_id, content_question_id)`, nên hai coordinator cùng ordinal dùng cùng semantic idempotency key. Pending-wrong do coordinator A ghi buộc coordinator B reconcile sang attempt 2; retry-correct vẫn assisted (`IndependentSuccess=false`), counters được rebuild từ durable attempts và replay không tạo attempt thứ ba. Legacy random-GUID cache vẫn được nhận diện qua stable `ContentQuestionId` để resume an toàn.
- Targeted generated-ordinal repair: nếu lesson checkpoint không có open question nhưng `generated_question_count` lệch số câu finalized (ví dụ cursor=3 nhưng durable attempts=1), restore tự sửa cursor về committed ordinal và rewrite checkpoint; medium/application không bị skip.
- Targeted open-question ordinal guard: kể cả `current_question_json` là application hợp lệ và `generated_question_count=3`, nếu durable progress mới finalized 1 câu thì restore reject cache semantic-skip, rollback cursor về 1 và phát medium trước application.
- Canonical authored-cache guard: targeted resume không chỉ tin `ContentQuestionId`; toàn bộ prompt/type/difficulty/template/skill/grading/units/operators/choices/illustration/representation/hints/difficulty-fit phải khớp bank canonical. Cache giữ đúng stable ID nhưng sửa đáp án/nội dung bị discard và replay câu canonical; legacy random runtime `QuestionId` vẫn tương thích.
- Adaptive cache canonical regeneration: corrupt/malformed adaptive open question rollback `generated_question_count` về durable completed count để không skip ordinal; JSON hợp lệ nhưng sửa prompt/answer hoặc selection `template_id/skill_id` cũng bị discard. Engine regenerate bằng `(session seed, ordinal, persisted template)` và yêu cầu fingerprint canonical khớp trước restore.
- Pack-byte identity audit: production `Program.BootstrapRuntime()` bắt buộc `ContentPackValidator.ValidateDirectory(..., true)` cho bundled Math pack trước UI; manifest SHA-256 mismatch fail startup, nên V5 `pack_id+version` kết hợp verified manifest đủ contract hiện tại, chưa cần invent schema V6 chỉ để lưu hash lần hai.
- Late-review transaction fault: injected failure tại `review_schedule` (sau attempt/mastery/child_skill trong cùng transaction) rollback sạch toàn chain + semantic key; retry sau khi gỡ trigger ghi đúng một attempt/mastery/child_skill/review duy nhất.
- Expanded-bank integration: `373d9d1` bỏ test coupling `Single(...)`/`_01`, chạy được cả baseline 201 và pool 402; current real 402-bank persistence tổng **7199 assertions PASS**.
- PowerShell release/build scripts: schema V5 payload/bootstrap expectations đã cập nhật; staged `Build-SetupArtifacts.ps1` PASS qua Release x86 + runtime smokes + staged payload/preflight + portable packaging.
- `git diff --check` + staged `git diff --cached --check`: PASS cho wave V5.

## Session

- start / next / submit / complete: PASS.
- `Suspend`: PASS — giữ active session + runtime checkpoint, không reward.
- `Dispose`: PASS — mặc định suspend thay vì abort.
- exact resume: PASS — giữ nguyên session id, target count, seed, committed counters và open question.
- open-question idempotency: PASS — gọi `NextQuestion()` lại khi câu đang mở trả đúng cùng câu, không regenerate.
- deterministic generation qua restart: PASS — per-question seed = stable hash `(session seed, ordinal, template id)`.
- stale open question sau committed answer: PASS — phát hiện qua `attempt_commit_key`, không hiển thị/ghi điểm lại.
- corrupted current-question cache: PASS — chỉ bỏ cache câu mở, giữ committed attempts/mastery/progress; lesson-mode reconcile authored cursor về committed ordinal để không skip câu.
- content-pack identity resume: PASS — runtime mới pin `pack_id` + `pack_version`; legacy zero-attempt runtime bind current pack, runtime đã học bằng pack khác bị recover thay vì trộn content version.
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
- atomic Math session/runtime startup: PASS — `TryCreateSession` commit session + runtime + targeted lesson `started_count` cùng transaction; race loser restore durable winner, không recovery/abort nhầm session process khác; fault ở lesson-progress rollback toàn chain.
- dangling recovery subject/child scope: PASS — chỉ Math session thiếu `math_session_runtime` của child đang start bị recovery; active `english`/`mixed` và Math session của child khác không bị thu hồi nhầm. Global overload vẫn giữ cho maintenance.
- schema V4: PASS — thêm `session_mode`, `target_lesson_id`, `math_lesson_progress`.
- schema V5: PASS — thêm runtime `pack_id`/`pack_version`; migration backfill chỉ khi durable attempts có đúng một non-empty pack identity; mixed/blank history để unbound cho runtime quarantine; SQLite trigger cấm partial/blank pair; checksum/tamper/deployment payload gate đã có.
- V1 → V5: PASS với pre-migration verified backup; migration history giữ đủ V1/V2/V3/V4/V5.
- V2 → V3 historical duplicate semantic attempt: PASS, không xóa lịch sử; key pin vào earliest committed attempt; các migration V4/V5 apply bình thường.
- session seed / target / generated ordinal / mode / targeted lesson id: PASS durable.
- current question / selection / started timestamp / forced repair: PASS durable.
- resume after close: PASS.
- complete/abort removes runtime checkpoint: PASS.
- corrupt core runtime quarantine: PASS — deterministic malformed runtime metadata được isolate theo đúng child/session; checkpoint hỏng bị xóa nhưng attempt/mastery/child_skill durable vẫn giữ nguyên; SQLite/I/O failure không bị classify nhầm thành corruption.
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
- `b8fd18b` — `fix(toán): chặn tạo trùng phiên học đồng thời` — pushed.
- `2da39b5` — `fix(toán): tạo phiên và runtime nguyên tử` — pushed.
- `c4ff314` — `fix(toán): khóa nguyên tử tiến độ khi mở bài` — pushed.
- `21cc224` — `fix(toán): giới hạn recovery đúng phiên toán` — pushed.
- `d3f2d21` — `fix(toán): giới hạn recovery theo hồ sơ học` — pushed.
- `a871626` — `fix(toán): cô lập lỗi sau khi hoàn tất bài` — pushed.
- `03fc6c1` — `fix(toán): cách ly runtime hỏng khi tiếp tục bài` — pushed.
- `ece2a0c` — runtime schema V5/content-pack identity đã khóa và pushed; persistence regression hiện vẫn PASS.

## Blocker / coordination

AI1/AI3 đang sửa song song content/Math Hub/UI trên cùng `main`. AI2 chỉ stage file/hunk thuộc engine/persistence/test/release contract; không commit hộ content pack 1.9, generator/template hoặc UI thay đổi của lane khác. `PackVersion` trong selective commit phải tiếp tục khớp manifest đang commit ở `HEAD` cho tới khi lane content publish version mới.

## Final AI2 gate

- MathEngineRuntimeSmoke: **47 PASS**.
- MathDataEngineRuntimeSmoke: **104 PASS** — race/idempotency/concurrency.
- LearningSessionRuntimeSmoke: **800 PASS** — selector/generator fuzz.
- MathSessionPersistenceRuntimeSmoke: **378 PASS** — targeted pool, retry, corrupt cache, selected-set repair, Request 006/009/010, V5 resume.
- AI1 working pool 402 integration: **378 PASS** trên engine AI2.
- Lane AI2 core engine/session/persistence: **DONE**. Các mục `skip policy` và numeric XP/daily streak không có product contract nên không tự invent semantics.
