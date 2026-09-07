# MATH — 3 AI MASTER PARALLEL BOARD

Updated: 2026-09-07
Mode: **STRICT NO-WAIT / SAME WORKTREE / SMALL COMMITS**

Mục đích của board này: AI1, AI2, AI3 có thể chạy **đồng thời liên tục** trên cùng repo mà không ghi đè nhau. Dependency chỉ là **publish/integration gate**, tuyệt đối không phải wait gate.

> Lệnh dùng cho người điều phối:
> - AI1: `Đọc _WORK_CLAIMS/AI1_PARALLEL_NOW.md và tự làm liên tục theo file đến khi đạt STOP RULE. Không hỏi lại nếu có thể tự kiểm repo.`
> - AI2: `Đọc _WORK_CLAIMS/AI2_PARALLEL_NOW.md và tự làm liên tục theo file đến khi đạt STOP RULE. Không hỏi lại nếu có thể tự kiểm repo.`
> - AI3: `Đọc _WORK_CLAIMS/AI3_PARALLEL_NOW.md và tự làm liên tục theo file đến khi đạt STOP RULE. Không hỏi lại nếu có thể tự kiểm repo.`

Mỗi file AI riêng đã chứa đủ: trạng thái ổn định, việc phải verify, backlog tiếp theo, fallback, ownership, test gate, commit rule và điều kiện dừng. AI không cần người dùng nhắc lại lịch sử hội thoại.

---

## 1. Luật NO-WAIT bắt buộc

1. AI nào bị block bởi file/dependency của lane khác phải **chuyển ngay sang NEXT/FALLBACK** của chính mình.
2. Không được ngồi chờ một commit của AI khác nếu vẫn còn audit/test/hardening độc lập có thể làm.
3. Chỉ bước publish/merge thật vào file shared mới được chờ handoff; code/fixture/draft/test phải làm trước bằng synthetic fixture hoặc draft path.
4. Một AI thấy `LANE_DONE=YES` **không được dừng ngay** khi người dùng vừa gọi lại. Phải:
   - đọc current `git status` + `git log`;
   - verify các contract quan trọng trên HEAD mới;
   - chạy backlog cải tiến trong file riêng;
   - chỉ dừng khi STOP RULE cuối file vẫn đạt.
5. Không phát minh product semantics chưa có contract. Ví dụ skip/XP/streak chỉ làm khi product contract đã tồn tại; nếu chưa có thì test/harden phần đã định nghĩa và ghi blocker rõ.

---

## 2. Luật shared worktree — không thương lượng

- Cấm: `git reset`, `git checkout -- <file>`, `git restore`, `git clean`, `git stash`, `git rebase` trên shared working tree.
- Cấm `git add -A` và `git add .`.
- Trước khi sửa file: `git status --short -- <file...>`.
- Nếu file đang modified bởi lane khác và không nằm trong ownership của mình: **không sửa**.
- Commit bằng exact file list, ưu tiên `git commit --only -- <files>`.
- Một concern/commit; gate xanh thì push sớm.
- Push rejected: inspect `git status/log`; không reset/overwrite người khác.
- Không sửa shared contract docs để “giành trạng thái”; handoff/status ghi vào file lane riêng.
- Build làm bẩn `packages.lock.json`/line endings nhưng `git diff` rỗng: chỉ refresh/dọn exact HEAD bytes khi chắc chắn đó là side effect của chính mình; không dọn file content/UI của lane khác.

---

## 3. Ownership cứng

### AI1 — CONTENT / DATA / AUTHORING
Own:
- `content_packs/math_grade2_v1/lesson_catalog_v1.json`
- `content_packs/math_grade2_v1/question_bank_v1.json`
- `tools/math_content_authoring/**`
- `tools/math_content_validator/**`
- `tests/MathContentDataSmoke/**`
- `_WORK_CLAIMS/AI1_MATH_CONTENT_STATUS.md`
- `_WORK_CLAIMS/AI1_PARALLEL_NOW.md`

Conditional shared file:
- `content_packs/math_grade2_v1/manifest.json`: AI1 chỉ sửa khi không có template/pack wave đang giữ lock; phải preserve template hash/version của current HEAD và chỉ thay hash content mình sở hữu.

Never edit:
- `src/Learning/**`, `src/Session/**`, `src/Data/**`, Math UI/release files.

### AI2 — ENGINE / SESSION / PERSISTENCE
Own:
- `src/Learning/**` cho Math engine/validator/selector/generator
- `src/Session/**` cho Math authored/session/progress
- Math-specific persistence/runtime code dưới `src/Data/**`
- `tests/MathEngineRuntimeSmoke/**`
- `tests/MathDataEngineRuntimeSmoke/**`
- `tests/LearningSessionRuntimeSmoke/**` khi sửa Math generator/selector
- `tests/MathSessionPersistenceRuntimeSmoke/**`
- `_WORK_CLAIMS/AI2_MATH_ENGINE_STATUS.md`
- `_WORK_CLAIMS/AI2_PARALLEL_NOW.md`

Conditional shared file:
- `verified_templates_v1.json` + `manifest.json` chỉ khi có adaptive-template wave thực sự; commit nhỏ rồi trả lock FREE.

Never edit:
- AI1 authored catalog/bank/source content; AI3 UI/release E2E.

### AI3 — UI / QA / RELEASE / INTEGRATION
Own:
- `src/App/MathHubForm.cs`
- `src/App/MathLessonForm.cs`
- `tests/ChildUiRuntimeSmoke/**`
- `tools/build/Build-SetupArtifacts.ps1`
- `tools/build/Test-PortableE2E.ps1`
- `tools/build/Test-InstallerE2E.ps1`
- Math-related release/preflight/integration tests
- `_WORK_CLAIMS/AI3_MATH_UI_AUDIT.md`
- `_WORK_CLAIMS/AI3_PARALLEL_NOW.md`
- `_WORK_CLAIMS/MATH_LIVE_STATUS.md`

Never edit:
- `src/Learning/**`, `src/Session/**`, authored question/catalog JSON, authoring generator/validator.

---

## 4. Stable contracts hiện hành — mọi AI phải bảo vệ

- Curriculum: 7 chapters / 17 topics / 67 lessons.
- Runtime authored bank: **402 questions**.
- Mỗi lesson: **6 questions = 2 basic + 2 medium + 2 application**.
- Stable IDs: `m2_q_<skill>_01..06` contiguous.
- Targeted lesson session: **exactly 3 questions**, selected durable **1 basic + 1 medium + 1 application**.
- Pool size 6 không được biến thành session target 6.
- Selected IDs survive suspend/resume/retry/corrupt-open recovery; corrupt selection metadata chỉ được self-heal deterministic trong cùng content pack.
- Request 010: expression per-question `allowed_operators` được preserve/enforce; bài add/sub không chấp nhận multiply/divide equivalent.
- Request 006: integer `answer_unit` là display-only metadata; raw grading vẫn số, feedback có unit.
- Adaptive `draw_segment_given_length`: `interaction_integer`, không fake choices.
- Adaptive UI mission count derive từ `MathSessionCoordinator.DefaultTargetQuestionCount`, không hard-code `8/tám`.
- Schema: V5; runtime session pin `pack_id + pack_version`; partial pair bị DB reject; mismatched old pack không được mixed-resume.
- Math runtime payload phải có đủ `verified_templates_v1.json`, `lesson_catalog_v1.json`, `question_bank_v1.json`.
- Product không có first-class numeric XP/daily streak/skip contract thì UI/engine không tự invent.

Milestone commits cần biết, không nhất thiết phải re-run implementation nếu vẫn hiện diện:
- Request 010: `7afbb7b`
- adaptive segment: `d21a665`
- Request 009 selected-set: `05cdb2a`, self-heal `3b8a10b`
- Request 006: `3cf7fc6`
- runtime 402 publish: `68145ea`
- UI pool6/session3: `f567be8`
- UI answer-unit: `3c0cfaa`
- AI2 stress: `99fd100`, `98fd589`

Không tin tuyệt đối SHA “common head” cũ trong status docs. Khi bắt đầu phiên mới, lấy `git rev-parse HEAD` và verify trên HEAD hiện tại.

---

## 5. Cách chạy 3 AI đồng thời

### AI1 queue
1. Verify production 402-bank + deterministic regeneration + manifest hashes.
2. Chạy semantic/content gates.
3. Audit pedagogy/content regressions: duplicate, difficulty progression, hint leak, distractor diagnosis, explanation evidence, source/ID/readability/Unicode/prerequisite.
4. Nếu có lỗi: sửa AI1 files, regenerate, test, commit/push.
5. Nếu không có lỗi: tiếp tục audit/fuzz authoring/draft-only improvements; không tự tăng runtime bank >402 nếu chưa có product contract mới.

### AI2 queue
1. Verify Request 006/009/010 + V5 on current HEAD.
2. Real 402-bank persistence/session stress.
3. Selector/answer-validator/generator fuzz + deterministic stress.
4. Retry/idempotency/write-failure/corrupt-cache/pack-identity/concurrency hardening.
5. Nếu product contract mới xuất hiện, implement trong engine ownership; nếu chưa, không invent skip/XP/streak.

### AI3 queue
1. Verify Child UI on current HEAD and 402 answer surfaces.
2. Verify pool6/session3, answer-unit, adaptive target, generated interaction, 67-lesson accessibility/error-state.
3. Run release Build-Setup + staged payload + portable + installer E2E when source changes warrant it.
4. Maintain no stale state/no hard-code/reflection/accessibility guards.
5. Signing/real Windows 7 only when actual signing credentials/machine exist; otherwise keep explicit release-wide blocker and continue automation/readiness work.

---

## 6. Cross-lane handoff protocol

- AI1 content contract change → update AI1 own status + commit SHA; AI2/AI3 verify consumer via current HEAD, không cần AI1 sửa engine/UI.
- AI2 runtime contract change → update AI2 own status + commit SHA; AI1 chỉ sửa metadata/content contract nếu cần; AI3 thêm presentation/E2E.
- AI3 không thay runtime semantics; nếu UI test phát hiện engine/content bug, ghi evidence trong AI3 status và chuyển sang fallback, không sửa chéo lane.
- Manifest/template lock: người cần file shared phải kiểm status first. Nếu dirty bởi lane khác, chuyển fallback; không chờ.

---

## 7. Common-head final gates

Một common HEAD chỉ được coi là Math green khi tối thiểu:

- Production content validator: 402/402 valid, 0 errors.
- Content tests + pool6 publish tests: PASS.
- 67 lesson × 6; exactly 2/difficulty.
- MathEngineRuntimeSmoke: PASS.
- MathDataEngineRuntimeSmoke: PASS.
- LearningSessionRuntimeSmoke: PASS.
- MathSessionPersistenceRuntimeSmoke: PASS trên real 402-bank.
- SQLite production runtime smoke: PASS.
- ChildUiRuntimeSmoke: PASS, 402/402 surfaces renderable.
- ContentRuntime/release manifest pack-version contract: PASS.
- Build-Setup Release/x86: PASS khi chạy final distribution gate.
- Portable E2E: PASS.
- Installer/reinstall/uninstall E2E: PASS.
- `git diff --check`: PASS cho files của wave.
- Không commit nhầm WIP lane khác.

Số assertion có thể tăng theo regression mới; **không hard-code rằng phải đúng bằng số lịch sử**. Chỉ được giảm assertion khi có giải thích/contract change rõ.

---

## 8. STOP RULE chung

AI chỉ được dừng khi:

1. Đã verify contract thuộc lane trên **current HEAD**;
2. Không còn P0/P1 bug/regression có bằng chứng trong ownership;
3. Backlog cải tiến có thể làm an toàn đã được xử lý hoặc ghi rõ vì sao không có product contract/tool/machine;
4. Tests lane xanh;
5. Files lane không còn uncommitted diff của chính AI;
6. Commit/push đã xong;
7. Own status file được cập nhật với evidence mới;
8. Nếu common-head final gate bị lane khác làm đỏ, AI không tuyên bố toàn Math DONE — chỉ tuyên bố own lane green và tiếp tục fallback work.

`LANE_DONE=YES` nghĩa là strict current DoD đã đạt, **không phải lệnh bỏ qua verify/backlog khi người dùng gọi AI trở lại**.
