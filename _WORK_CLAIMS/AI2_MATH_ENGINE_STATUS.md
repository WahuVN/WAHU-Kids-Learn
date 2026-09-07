# AI2 — Math engine status

Cập nhật: 2026-09-07
Branch: `main`
Playable Event Engine P0: **GREEN**

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
- `tests/MathSessionPersistenceRuntimeSmoke`: PASS — **7828 assertions** (toàn bộ gate cũ + real 402-bank selector breadth + targeted write-failure/corruption repair + deterministic authored runtime IDs + concurrent pending-retry semantics + generated/open-ordinal self-heal + late-review transaction rollback + operational DB failure fail-safe).
- Pending-retry early complete: q2 wrong/pending retry rồi gọi `Complete()` bị reject nhưng `RetryPending`, checkpoint=1, exact q2 và reward=0 đều giữ nguyên; suspend/resume tiếp tục flow cũ.
- Corrupt-catalog completion: synthetic event q1 complete rồi catalog hỏng; fallback lesson giữ selected q2/q3, đạt 3/3 nhưng chưa terminal cho tới `Complete()`, sau đó đúng 3 attempts/3 mastery + 1 reward; khôi phục catalog mở cùng event tạo session mới 0/3 và không duplicate reward.
- Mismatched event/lesson fail-closed: production event 1 ID ghép nhầm fallback lesson event 2 bị `ResolveEventFailSafe()` reject trước session start; regression xác nhận 0 active session, 0 `math_session_runtime`, 0 `math_lesson_progress`, 0 reward.
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
- Deterministic adaptive runtime identity: coordinator override generator GUID bằng stable ID derive từ `(session_id, ordinal, template_id)`; corrupt q2 regenerate giữ cùng semantic ID, và hai coordinator cùng adaptive ordinal dùng chung idempotency key nên replay không tạo duplicate attempt/mastery.
- FIRST-LESSONS-FIRST golden slice: 5 bài đầu curriculum chạy sequential targeted sessions trên cùng child, verify lock/unlock graph, selected basic→medium→application, 15 completed questions = 15 durable attempts/mastery events, progress 5/5 completed và không còn active session. Riêng bài 1 dùng seed set tối thiểu để exercise đủ 6/6 authored IDs qua real sessions; phiên đầu wrong medium → retry → suspend/resume → assisted correct, rồi complete đúng 3 questions từ 4 answer attempts.
- FIRST-5 all-variants E2E: từng bài trong 5 bài đầu dùng seed plan tối thiểu để chạm đủ 2 basic + 2 medium + 2 application qua coordinator/SQLite thật; tổng **30/30 authored IDs** có durable attempt, mọi session complete 3/3 đúng bucket, progress replay counts đúng và không để active session.
- FIRST-5 retry/error matrix: mỗi bài đầu có một câu cố ý sai rồi retry đúng; phủ `multiple_choice/text`, `true_false/text`, `numeric_input/integer`. Mỗi session kết thúc 3 completed questions từ 4 answer attempts, đúng 1 retried question/correct, `IndependentCorrect=2`, DB có 4 attempts nhưng chỉ 3 mastery events; tổng 5 bài = 20 answer attempts/15 mastery, không để active session.
- FIRST-5 hint/mastery matrix: mỗi bài đầu có một câu đúng với `hint_level=2` và hai câu independent; outcome hinted không được tính independent, mastery reason chứa `hinted_correct_lower_weight`, review reason `hinted_success_short_recall`, DB persist đúng 1 max-hint + 2 no-hint attempts và child_skill đúng `independent_success_count=2`, `hinted_success_count=1`.
- Pack-byte identity audit: production `Program.BootstrapRuntime()` bắt buộc `ContentPackValidator.ValidateDirectory(..., true)` cho bundled Math pack trước UI; manifest SHA-256 mismatch fail startup, nên V5 `pack_id+version` kết hợp verified manifest đủ contract hiện tại, chưa cần invent schema V6 chỉ để lưu hash lần hai.
- Late-review transaction fault: injected failure tại `review_schedule` (sau attempt/mastery/child_skill trong cùng transaction) rollback sạch toàn chain + semantic key; retry sau khi gỡ trigger ghi đúng một attempt/mastery/child_skill/review duy nhất.
- Expanded-bank integration: `373d9d1` bỏ test coupling `Single(...)`/`_01`, chạy được cả baseline 201 và pool 402; current real 402-bank persistence tổng **7828 assertions PASS**.
- PowerShell release/build scripts: schema V5 payload/bootstrap expectations đã cập nhật; staged `Build-SetupArtifacts.ps1` PASS qua Release x86 + runtime smokes + staged payload/preflight + portable packaging.
- `git diff --check` + staged `git diff --cached --check`: PASS cho wave V5.

## Playable Event / Game V1

- `492a942` — public event runtime V1 landed without creating a second grading path. `MathGameEventCoordinator` delegates start/next/submit/retry/suspend/complete to existing targeted `MathSessionCoordinator`.
- Frozen schema consumer is fail-closed: schema/catalog/language/exact keys/kind/question_count/checkpoint count/reward presentation/lesson+skill refs; V1 unique target lesson makes event identity deterministic on resume without schema V6.
- Behavior mapping public to UI: READY=`normal`; FLOW=`minimize_interruptions`; BORED=`context_transfer`; STRAINED=`small_cue`; FRUSTRATED=`repair`; FATIGUED=`offer_break` + positive-close flag; every action preserves completed checkpoint.
- Event completion guard rejects completion before 3 durable checkpoints; caller must suspend for break instead, so incomplete/fatigue path cannot create Garden reward.
- Resume regression: wrong medium -> retry pending -> suspend -> restart with **no event_id supplied** -> derive same event by target lesson -> exact session/selected set/q2/checkpoint/attempt 2 restored.
- Reward regression: completion uses existing `GameWorldRewardService`; wrapper completion replay is idempotent and durable reward_event remains exactly one per session.
- Corrupt event metadata regression: malformed catalog falls back to ordinary targeted lesson presentation while preserving exact active session/open q2/attempt/mastery; no fake reward.
- Fault regression: injected mastery write failure leaves checkpoint 0/open q1/zero attempts/mastery/reward; after fault removal same q1 commits once and event completes with exactly 3 learning events + 1 reward.
- Concurrency regression: two event wrappers bind same durable session/question identity, replay all 3 checkpoints without duplicate attempt/mastery; stale second completion cannot duplicate reward.
- Production 5-event integration (`bd1809e`): exactly first 5 lesson/skill mappings load from shipped `game_events_v1.json`; sequential same-child play completes 5 targeted sessions = 15 attempts + 15 mastery + 5 lesson-progress completions + exactly 5 Garden rewards, no active session left.
- Production event-1 resume journey: q1 correct → q2 wrong/max-hint → suspend → restart with `event_id=null` and target lesson only; loader derives same production event, restores exact selected set/q2/attempt2/checkpoint2, retry-correct remains assisted, completion writes 3 mastery and one reward.
- Reward fault reconciliation: `GameWorldRewardService.ReconcileMissingCompletedMathSessionRewards` discovers only durable `state=completed` Math sessions with attempts but missing Garden reward, then reuses idempotent `GrantCompletedMathSession`. Event start repairs prior missing reward best-effort; injected reward write failure keeps 3 mastery/progress, next unlocked event repairs exactly one reward, replay repairs 0, active session never receives reward.
- Durable active-event precedence: event-only internal resume override allows a requested event/lesson to yield to an already-active targeted Math lesson; wrapper then re-resolves production event by durable `target_lesson_id`. Regression proves ordinary `MathSessionCoordinator` still rejects different active lesson, event wrapper preserves exact q2/selected set/checkpoint and does not increment the newly clicked lesson `started_count`; after original completion, the newly requested event starts normally.
- Behavior-action resume: targeted event can naturally reach non-READY support after repeated wrong/max-hint attempts; suspend/resume now maps rebuilt `Summary.FinalBehaviorState` back to the same event action, preserving support mode and exact open q3/retry state.
- Fatigue audit: V1 targeted event has 3 questions on one skill, while conservative BehaviorController fatigue requires >=4 recent observations and >=2 distinct skills. AI2 intentionally does **not** weaken global thresholds to force fatigue. Explicit `Nghỉ ở đây`/`SuspendForBreak` remains available independent of fatigue detection; FATIGUED mapper contract remains covered for UI consumption when engine evidence legitimately produces it.
- Terminal-runtime cleanup: `MathSessionRuntimeService.DeleteTerminalCheckpoints(childId)` removes only derived runtime rows whose Math session is already terminal. `MathSessionCoordinator.Start` invokes it best-effort before resume lookup. Injected DELETE failure after event completion leaves learning + reward durable and one repairable runtime row; next event start cleans old row, keeps new active runtime, and preserves prior 3 attempts/3 mastery/1 reward.
- Stale event-id fallback: nếu caller giữ một `event_id` cũ không còn trong catalog nhưng `fallbackLessonId` vẫn trỏ đúng lesson, runtime resolve lại event hiện tại bằng lesson; ID tồn tại nhưng trỏ sai lesson vẫn fail-closed.
- Post-q3 close boundary: `MathGameEventState.IsComplete` chỉ là terminal completion, không phải 3/3 checkpoint. Regression q3 durable rồi rời `using`/`Dispose()` mà không gọi Suspend thủ công: session vẫn active, 3 attempts, 0 reward; reopen 3/3 + `NextQuestion()==null`; Complete chuyển terminal và tạo đúng 1 Garden reward.
- Explicit repair precedence: `MathGameEventBehaviorMapper` giờ tôn trọng `BehaviorDecision.TriggerPrerequisiteRepair`/`Actions=prerequisite_repair` ngay cả khi state còn READY/STRAINED. Runtime regression: q1 sai + retry sai => state vẫn READY nhưng repair action bật; suspend/resume đọc full durable `LastBehaviorDecision`, giữ repair action và exact q2. FATIGUED vẫn giữ break precedence.
- Concurrent reward reconcile: hai DB/service instance độc lập được barrier-thả cùng lúc trên cùng completed session thiếu reward; cả hai không lỗi, tổng repaired=1, đúng 1 `garden_growth` + 1 `garden_seedling`, progress nhất quán và next event không duplicate repair.
- Dispose-safe resume: regression đóng coordinator không gọi Suspend thủ công sau q1 + q2 wrong/pending retry. `Dispose()` giữ session `active`, durable q1 + pending q2 attempt, 1 mastery, runtime row, 0 reward; coordinator mới resume same session + ordered selected set + exact q2 + retry pending, retry-correct vẫn assisted rồi chuyển checkpoint 2.
- Fresh completed-event replay: sau terminal event + 1 Garden reward, mở lại cùng event tạo session ID mới với checkpoint 0/3, `started_count=2/completed_count=1`, reward vẫn 1; đóng replay ở q1 rồi reopen resume đúng replay session/q1 chứ không terminal session cũ.
- Concurrent fresh replay: sau terminal event, wrapper replay A tạo session mới; wrapper replay B đồng thời resume đúng session A + ordered selected set, `started_count=2/completed_count=1` giữ nguyên và reward count vẫn 1.
- Corrupt-catalog pending retry: synthetic event q1 complete + q2 wrong pending, suspend rồi phá `game_events_v1.json`; wrapper fallback ordinary lesson vẫn restore selected set + retry state + exact q2, retry-correct assisted đưa checkpoint lên 2, DB giữ 3 answer attempts/2 mastery và 0 reward.
- Metadata recovery: sau fallback q2 assisted + suspend, khôi phục catalog hợp lệ rồi reopen bằng `event_id=null`/lesson fallback; wrapper resume cùng session với completed=2, event presentation trở lại ở checkpoint 3, exact selected q3 và vẫn 0 reward trước terminal completion.
- No-autoplay: trong production 5-event sequential regression, sau completion event 1→4 `NextLessonId` trỏ đúng event kế nhưng DB vẫn không có active session hay `math_lesson_progress` row cho lesson kế; chỉ iteration Start kế tiếp mới tạo session/progress.
- Public lifecycle guard: cùng coordinator Start lần hai bị reject và giữ đúng một active session/started_count; sau 3/3 + terminal Complete, NextQuestion/Submit/Start bị reject, Suspend trả summary no-op, Complete trả cached object, Dispose hai lần vẫn giữ 3 attempts/3 mastery/1 reward.
- Playable Event P0 persistence total: **7828 assertions PASS**. Production AI1 catalog `bd1809e` đã tích hợp thật 5/5 event; synthetic fixtures vẫn giữ để fault/corruption/race test độc lập.

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
