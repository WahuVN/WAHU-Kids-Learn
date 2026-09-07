# AI2 — MASTER EXECUTION PLAN — MATH ENGINE / SESSION / PERSISTENCE

LANE=AI2
ROLE=MATH_ENGINE_SESSION_PERSISTENCE_OWNER
LANE_DONE=NO
EXECUTION_MODE=VERIFY_THEN_HARDEN_NO_WAIT
ACTIVE_PHASE=PLAYABLE_EVENT_GAME_V1
ACTIVE_FOCUS=FIRST_FIVE_EVENT_RUNTIME_BEHAVIOR_RESUME_REWARD

> Khi người dùng bảo “đọc file và làm”: đọc toàn bộ file này, inspect current HEAD, verify stable contracts rồi tiếp tục fuzz/stress/hardening. Không dừng chỉ vì `LANE_DONE=YES`.

## PLAYABLE EVENT V1 ENGINE STATUS

- Public runtime landed: `492a942` — `MathGameEventCatalogSource`, DTO/state/action mapper, `MathGameEventCoordinator`.
- Synthetic P0: **PASS** — exact targeted 3-question session, checkpoint progress, retry+suspend/resume, behavior action mapping, early-complete guard, Garden reward once, corrupt/missing metadata fallback, answer-transaction fault recovery, concurrent coordinator idempotency.
- Persistence current: **7745 PASS**.
- Event identity requires no migration: V1 enforces one event/lesson; resume can derive event deterministically from `target_lesson_id` when catalog is healthy.
- Production `game_events_v1.json` published by AI1 at `bd1809e` and integrated against runtime: 5/5 production events sequential PASS; production event-1 wrong→retry→suspend/resume PASS.
- Durable active-event precedence: nếu UI bấm event khác trong lúc một targeted event đang dở, event wrapper resume session/lesson/event durable cũ; caller lesson thường vẫn reject mismatch.
- Behavior-action resume: support action cuối được rebuild từ durable behavior history khi resume; không reset về `normal`. FATIGUED không bị ép bằng cách hạ threshold single-skill; CTA `Nghỉ ở đây` vẫn là explicit safe suspend path.

## PLAYABLE EVENT V1 OVERRIDE

Đọc và tuân thủ `_WORK_CLAIMS/MATH_PLAYABLE_EVENT_GAME_V1.md` trước backlog fuzz chung. P0 mới là event wrapper quanh targeted MathSession 3 câu, BehaviorDecision→event action, safe suspend/resume, no reward on incomplete, existing Garden reward exactly once on completion, fail-safe về ordinary Math khi event metadata hỏng. Dùng synthetic event fixture ngay, không chờ AI1. Không sửa authored event copy/UI.

## 0. Mission

AI2 chịu trách nhiệm runtime Math: answer validation, selector/generator, authored loader, targeted/adaptive session, retry/resume, persistence, concurrency/idempotency, pack identity và progress/mastery contract.

AI2 không sửa authored content để né engine bug và không sửa UI để né presentation regression.

## 1. Stable contracts đã đóng — phải bảo vệ

### Request 010 — expression operators
Milestone: `7afbb7b`.
- per-question `AllowedExpressionOperators` được load/preserve;
- add/sub lesson: `75`, `100 - 30 + 5`, `70 + 5` PASS;
- `15*5`, `150/2` FAIL;
- JSON/current-question resume preserve whitelist;
- legacy expression không whitelist vẫn backward-compatible theo global restricted arithmetic parser.

### Adaptive segment
Milestone: `d21a665`.
- `draw_segment_given_length` → `interaction_integer`;
- không fake choices;
- selector/generator runtime + UI consumer đã có regression.

### Request 009 — selected 3 from pool 6
Milestones: `05cdb2a`, self-heal `3b8a10b`.
- runtime pool có thể ≥6;
- targeted session chọn exactly 3 = basic + medium + application;
- deterministic theo seed/lesson/bucket;
- selected ordered IDs persist;
- resume/retry/corrupt-open giữ set/ordinal;
- corrupt selection metadata self-heal deterministic trong cùng pack;
- completion/score dùng selected target=3, không pool size.

### Request 006 — answer unit
Milestone: `3cf7fc6`.
- authored integer `answer_unit` survive loader/runtime/current-question JSON/resume;
- `CorrectAnswerDisplay` vẫn raw grading-compatible;
- feedback display thêm unit;
- raw integer validator không broaden sang `"8 cm"`.

### V5 pack identity
- runtime pin `pack_id + pack_version`;
- partial pair bị SQLite trigger reject;
- old mismatched pack runtime bị recover/quarantine thay vì mixed-resume;
- legacy zero-attempt bind current pack theo rule;
- durable attempt/mastery history không bị xóa khi quarantine runtime checkpoint.

### Current historical gates
- MathEngineRuntimeSmoke: **72 PASS** — thêm adversarial validator fuzz (Unicode/NFD, overflow, length, unit alias, expression depth/token-bomb, Unicode operators, invalid whitelist fail-closed).
- MathDataEngineRuntimeSmoke: 104 PASS.
- LearningSessionRuntimeSmoke: 800 PASS.
- SQLiteRuntimeSmoke production: 179 PASS.
- MathSessionPersistenceRuntimeSmoke current: **7745 PASS**; gồm real 402-bank breadth stress + write-failure/corruption repair + deterministic authored runtime IDs + concurrent pending-retry semantics + generated/open-ordinal self-heal + late-review transaction rollback + operational DB failure fail-safe.
- Real 402-bank integration đã PASS; selector breadth hiện verify đủ **67/67 lesson**, mỗi basic/medium/application bucket đều deterministic, in-range và xoay đủ 2 variant qua 16 seed.

Không hard-code assertion count; regression mới chỉ được tăng hoặc nếu giảm phải có lý do rõ.

## 1A. USER PRIORITY OVERRIDE — FIRST LESSONS FIRST

Ưu tiên hiện hành từ user: **làm thật kỹ các bài đầu trước; bài sau có thể cập nhật/hardening sau**.

AI2 phải ưu tiên theo thứ tự:
1. 5 bài đầu curriculum (`m2_ls_num_count_read_write_0_1000` → `m2_ls_num_expanded_form_hto`) là **golden slice P0/P1**.
2. Bài 1 phải được xem như **golden lesson**: cả 6 authored variants, retry/resume/idempotency/progress/unlock đều phải xanh.
3. Sau khi 5 bài đầu ổn mới mở rộng 6–10; chưa ưu tiên hardening bài xa nếu không phải regression chung ảnh hưởng bài đầu.
4. Bug engine chung phát hiện khi test bài đầu vẫn sửa ở root cause + regression toàn engine; không patch riêng content/UI để che lỗi.
5. Mỗi lần được gọi lại, trước backlog chung hãy verify golden slice đầu tiên còn xanh trên current HEAD/bank.

Current evidence:
- 5 bài đầu chạy sequential targeted completion trên bank thật: PASS.
- Bài 1 chạy đủ 6/6 authored IDs qua real sessions, gồm wrong→retry→suspend→resume: PASS.
- Persistence sau golden-slice hardening: **7592 PASS**.

## 2. Ownership

AI2 được sửa:
- Math-related `src/Learning/**`
- Math-related `src/Session/**`
- Math runtime/persistence code trong `src/Data/**`
- `tests/MathEngineRuntimeSmoke/**`
- `tests/MathDataEngineRuntimeSmoke/**`
- `tests/LearningSessionRuntimeSmoke/**` khi liên quan Math selector/generator
- `tests/MathSessionPersistenceRuntimeSmoke/**`
- `_WORK_CLAIMS/AI2_MATH_ENGINE_STATUS.md`
- `_WORK_CLAIMS/AI2_PARALLEL_NOW.md`

Conditional:
- `content_packs/math_grade2_v1/verified_templates_v1.json`
- `content_packs/math_grade2_v1/manifest.json`
chỉ khi AI2 có adaptive template wave thật. Check dirty/lock trước, commit nhanh rồi release lock.

AI2 không sửa:
- AI1 lesson/question bank/authoring validator;
- AI3 MathHub/MathLessonForm/Child UI/release E2E.

## 3. START PROCEDURE — mỗi lần được gọi lại

1. `git status --short --branch`.
2. `git log --oneline -15`.
3. Đọc current manifest + runtime `PackVersion`; phải khớp contract.
4. Kiểm real bank current count/buckets; engine tests không được assume 201 hoặc single `_01`.
5. Build/run MathEngine smoke.
6. Build/run MathData smoke.
7. Run LearningSession selector/generator smoke.
8. Build/run MathSessionPersistence smoke trên **real current bank**.
9. Run SQLite production smoke; nếu `dotnet build` toolchain fail vì `System.Data.SQLite` resolve nhưng known production binary/MSBuild path PASS, ghi rõ toolchain issue, không sửa engine vô cớ.
10. `git diff --check` AI2-owned files.
11. Nếu baseline xanh, chuyển ngay P1 HARDENING.

## 4. P0 — Contract regression phải fix ngay

Fix ngay nếu bất kỳ invariant sau đỏ:

- expression whitelist bị mất hoặc broaden;
- answer-unit metadata mất qua runtime/resume hoặc grading nhận unit text sai;
- pool6 targeted session target !=3;
- selected IDs không deterministic/persist;
- retry/resume đổi question instance/set;
- corrupt open-question skip ordinal hoặc chọn set khác;
- corrupt selection self-heal sai committed progress;
- stale/double submit tạo duplicate mastery/review/progress;
- write failure để ghost attempt/mastery/behavior;
- terminal session vẫn nhận write mới;
- pack mismatch bị mixed-resume;
- V5 partial pack pair lọt DB;
- prerequisite direct start bypass guard;
- completion/lesson score/progress không atomic/idempotent.

Sửa root cause engine/persistence + regression test; không chỉnh content/UI để che lỗi.

## 5. P1 — Continuous fuzz/stress backlog

### 5.1 Selected-set breadth stress
Mục tiêu nâng coverage từ một fixture lên representative/all lessons khi an toàn:
- với từng lesson pool6, chọn đúng 3 unique IDs;
- index 0 thuộc basic, 1 medium, 2 application;
- same seed/lesson → same ordered set;
- seed sample phải reach cả hai variant mỗi bucket khi hash distribution cho phép;
- target luôn 3;
- no pool-size coupling;
- fresh identity có nhiều valid selected signatures.

Ưu tiên test loop dùng temp DB/fixtures, không sửa AI1 bank.

### 5.2 Resume/retry/idempotency stress
- suspend tại q1/q2/q3;
- pending retry attempt 2 qua restart;
- duplicate initial intent không consume retry;
- retry correct = assisted, không independent;
- retry wrong finalize đúng một mastery update;
- exact replay after commit/complete remains idempotent;
- same question `NextQuestion()` repeated không regenerate.

### 5.3 Fault injection
- fail attempt/mastery/review/lesson-progress write ở các điểm transaction;
- toàn chain rollback hoặc commit atomic;
- coordinator giữ open question nếu durable commit fail;
- retry sau fault commit exactly once;
- selected-set checkpoint không đổi sau failed answer commit.

### 5.4 Corruption recovery
Fuzz deterministic invalid runtime metadata:
- malformed current-question JSON;
- selected-set JSON missing/invalid/duplicate/wrong bucket;
- generated ordinal inconsistent;
- timestamp corruption;
- pack identity mismatch/blank/mixed history.

Nguyên tắc: chỉ deterministic data corruption mới quarantine/self-heal; SQLite/I/O operational error không được classify nhầm thành corruption.

### 5.5 Answer-validator fuzz
Theo từng answer kind:
- integer: signs/whitespace/overflow/decimal-fraction non-equivalent;
- unit: aliases/case/spacing/wrong unit;
- expression: operators, unary, parentheses, div-zero, variables/functions, huge operands;
- text: Unicode normalization/case/whitespace;
- accepted answers: no unsafe broaden;
- unknown kind: safe fallback only.

### 5.6 Selector/generator fuzz
- deterministic same seed;
- no duplicate template starvation;
- generated constraints theo hard guards;
- interactive template always interaction surface;
- all verified supported templates generate valid answer/choices/representation;
- stress 40+ questions/session/seed where existing suite supports it.

### 5.7 Concurrency
- two coordinators/processes race same child/subject;
- active-session atomic guard;
- stale skill snapshot optimistic guard;
- stale coordinator after terminal state;
- exact replay remains idempotent across races.

## 6. P2 — Product-contract-only features

Không tự invent:
- skip semantics;
- numeric XP;
- daily streak;
- remote/network answer retry protocol.

Nếu product/shared contract xuất hiện, AI2 mới thiết kế engine semantics + persistence + tests. Khi chưa có, ghi `NO FIRST-CLASS CONTRACT` và tiếp tục P1 fuzz/stress.

## 7. V5 / migration hardening

Luôn giữ:
- V1→V5 migration path;
- migration history/checksum/tamper gate;
- verified pre-migration backup;
- partial/blank pack pair fail-closed;
- old-pack attempt history preserved;
- runtime checkpoint cleanup chỉ xóa runtime state, không xóa durable learning history;
- current `PackVersion` phải match shipped manifest.

Nếu pack version đổi do AI1/AI2 publish hợp lệ, update runtime contract + migration/release tests theo explicit handoff; không tự bump.

## 8. Acceptance gates trước commit engine

Tùy wave nhưng final tối thiểu:
- affected project builds x86/Release;
- MathEngine PASS;
- MathData PASS nếu persistence touched;
- LearningSession PASS nếu selector/generator touched;
- Persistence PASS nếu session/model/loader/runtime touched;
- real current 402-bank targeted smoke PASS;
- SQLite runtime PASS nếu DB/migration touched;
- `git diff --check` PASS.

Nếu UI/content consumer bị ảnh hưởng: ghi SHA/handoff cho AI1/AI3; không sửa chéo lane.

## 9. Commit discipline

- One concern per commit.
- Exact file list / `git commit --only`.
- Không stage content bank/catalog hoặc UI WIP.
- Nếu build làm dirty lock file nhưng diff rỗng, dọn side effect của chính mình only.
- Push ngay khi suite wave xanh.
- Update `AI2_MATH_ENGINE_STATUS.md` bằng evidence thật, không chỉ “DONE”.

## 10. FALLBACK — không bao giờ ngồi chờ

Nếu shared file/template/manifest bị lock:
- answer-validator fuzz;
- persistence/corruption tests;
- selected-set stress bằng synthetic fixture;
- concurrency/idempotency;
- pack-identity tests;
- selector determinism;
- status/audit of current runtime contracts.

## 11. Handoff

- Engine contract mới → ghi SHA + exact semantics trong own status.
- AI1 sửa metadata/content theo contract nếu cần.
- AI3 presentation/E2E theo contract.
- Không append shared request docs trong lúc parallel nếu board đang coi read-only.

## 12. STOP RULE AI2

AI2 chỉ dừng khi:
- current HEAD contracts 006/009/010 + V5 verify xanh;
- real 402-bank persistence/session xanh;
- không còn P0/P1 engine bug có evidence;
- đã chạy ít nhất một hardening/fuzz wave hoặc chứng minh backlog hiện không tạo issue mới;
- AI2-owned files sạch sau commit;
- push xong;
- `AI2_MATH_ENGINE_STATUS.md` cập nhật metric hiện tại.

`LANE_DONE=YES` không có nghĩa lần sau được gọi thì chỉ trả status; phải START PROCEDURE + harden tiếp.
