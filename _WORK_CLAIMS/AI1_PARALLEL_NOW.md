# AI1 — MASTER EXECUTION PLAN — MATH CONTENT / DATA

LANE=AI1
ROLE=MATH_CONTENT_DATA_OWNER
LANE_DONE=YES
EXECUTION_MODE=VERIFY_THEN_IMPROVE_NO_WAIT
PRIORITY_POLICY=PLAYABLE_FIRST_FIVE_FIRST
ACTIVE_PHASE=PLAYABLE_EVENT_GAME_V1
ACTIVE_FOCUS=CH01_LESSONS_01_05_PLUS_GAME_EVENT_CONTENT
LATER_LESSON_POLISH=DEFERRED_UNTIL_PLAYABLE_P0_GREEN

> Khi được người dùng bảo “đọc file và làm”: đọc **toàn bộ file này**, tự inspect repo/current HEAD, rồi làm từ START PROCEDURE xuống BACKLOG. Không dừng chỉ vì `LANE_DONE=YES`; dòng đó chỉ nói strict Math DoD trước đây đã đạt.

## PLAYABLE EVENT V1 OVERRIDE

Đọc và tuân thủ `_WORK_CLAIMS/MATH_PLAYABLE_EVENT_GAME_V1.md` trước mọi P1 polish khác. Hoàn tất/commit WIP first-five hiện có rồi chuyển ngay sang event catalog V1 cho 5 bài đầu. AI1 sở hữu scenario text, child-safe psychology copy, event JSON/authoring/validator/tests. Không sửa Session/App. Nếu manifest bận, làm source + fixture + validator trước, không chờ.

## 0. Mission

AI1 chịu trách nhiệm duy nhất cho **curriculum/content/data/authoring/validator** của Math Grade 2. Mục tiêu là giữ content production đúng chuẩn, deterministic, child-safe, không duplicate, có difficulty progression thật, và không làm hỏng contract engine/UI khi content thay đổi.

Không sửa engine/session/UI để “cho test qua”. Nếu consumer có bug, ghi evidence + SHA/status handoff và chuyển sang task AI1 khác.

## 0.1. Priority override — làm kỹ bài đầu trước

Theo chỉ đạo hiện tại, sau START PROCEDURE AI1 phải ưu tiên **10 lesson đầu của Chương 1** trước mọi P1 polish ở lesson phía sau. Thứ tự trong block: lesson 01 → 10 theo thứ tự catalog, không sort `order_in_domain` xuyên chapter.

Trong first block, ưu tiên theo thứ tự: correctness/fail-closed → prompt/application diversity → distractor diagnosis → hint/explanation/worked-example readability → language/typography. Chỉ khi first block không còn issue P0/P1 có evidence mới chuyển sang lesson 11+; các lesson sau hiện vẫn production-valid và có thể update sau.

## 1. Stable state phải bảo vệ

- 7 chapters / 17 topics / 67 lessons.
- Runtime bank: **402 questions**.
- 67 lesson × 6 questions.
- Mỗi lesson đúng **2 basic + 2 medium + 2 application**.
- IDs question contiguous `_01..06` theo skill.
- Difficulty total: 134 basic + 134 medium + 134 application.
- Answer kinds hiện hành gồm integer/text/expression/unit/interaction_integer; metadata phải khớp engine contract.
- Production validator: 0 errors.
- Content smoke lịch sử: 57 tests PASS (49 core + 8 pool6); số test có thể tăng, không được vô cớ giảm.
- Manifest pack version hiện hành phải khớp runtime `PackVersion`; không tự bump chỉ vì regenerate.
- Request 009 consumer đã tồn tại: engine chọn 3 từ pool 6; AI1 không hard-code selection/session target.
- Request 006 consumer đã tồn tại: integer `answer_unit` display-only.
- Request 010 consumer đã tồn tại: expression per-question operator whitelist.

Các milestone đã xong, không làm lại mù nếu HEAD vẫn chứa contract:
- shadow 402: `49329b3`
- runtime 402 publish: `68145ea`
- Request009 engine: `05cdb2a`
- Request006 engine: `3cf7fc6`
- Request010 engine: `7afbb7b`

## 2. Ownership

AI1 được sửa:
- `content_packs/math_grade2_v1/lesson_catalog_v1.json`
- `content_packs/math_grade2_v1/question_bank_v1.json`
- `tools/math_content_authoring/**`
- `tools/math_content_validator/**`
- `tests/MathContentDataSmoke/**`
- `_WORK_CLAIMS/AI1_MATH_CONTENT_STATUS.md`
- `_WORK_CLAIMS/AI1_PARALLEL_NOW.md`

Conditional:
- `content_packs/math_grade2_v1/manifest.json` chỉ khi không dirty/locked bởi template/pack wave khác. Preserve version + verified-template hash của current HEAD; chỉ thay hash file AI1 thực sự đổi.

AI1 **không sửa**:
- `src/Learning/**`
- `src/Session/**`
- `src/Data/**`
- `src/App/**`
- UI/release E2E.

## 3. START PROCEDURE — làm mỗi lần được gọi lại

1. `git status --short --branch`.
2. `git log --oneline -15`.
3. Kiểm AI1-owned files có WIP không. Không reset/restore/stash shared tree.
4. Đọc `AI1_MATH_CONTENT_STATUS.md` nhưng coi metric cũ là historical evidence, không là sự thật nếu HEAD đã đổi.
5. Đếm current production:
   - lessons;
   - questions;
   - bucket sizes;
   - difficulty counts;
   - answer-kind/question-type distribution.
6. Chạy py_compile authoring/validator.
7. Chạy deterministic regenerate vào production path hoặc safe temp path theo tool hiện hành.
8. Chạy semantic validator.
9. Chạy full `MathContentDataSmoke` + pool6 tests.
10. Check SHA manifest/catalog/bank và `git diff --check`.
11. Nếu tất cả xanh, chuyển ngay sang BACKLOG IMPROVEMENT; không dừng chỉ vì baseline xanh.

## 4. P0 — Production integrity / fail-closed gates

Luôn ưu tiên nếu phát hiện regression:

- 67/67 lesson tồn tại, không orphan/missing.
- 402 questions; 6/lesson; 2/difficulty.
- Practice-set mỗi question được reference đúng 1 lần.
- Lesson/question/skill/difficulty reference khớp.
- Stable IDs `_01..06`; không rename stable ID tùy tiện.
- Prerequisite graph acyclic/reachable, không edge bắc cầu dư thừa, không prerequisite future lesson.
- Baseline Grade-2 hard guards: max number, carry/borrow, tables 2/5, mental ≤20, time relations, unit scope.
- `answer_unit` chỉ integer display metadata, prompt phải nêu unit rõ theo alias contract.
- Expression metadata chỉ operator được lesson cho phép; không widen content để né engine validator.
- Accepted answers fail-closed, không extra incorrect equivalent.
- Choice canonical answer duy nhất; không duplicate semantic/numeric choices.
- Manifest SHA phải đúng bytes production thực tế.
- Authoring deterministic: regenerate 2 lần phải cùng JSON/hash.

Nếu P0 fail: sửa root cause trong authoring source trước, regenerate JSON, thêm regression test, commit nhỏ, push.

## 5. P1 — Pedagogical quality audit liên tục

Sau khi P0 xanh, audit từng nhóm; task nào tìm thấy lỗi có bằng chứng thì sửa + regression:

### 5.1 Prompt diversity / duplicate
- exact duplicate toàn bank = 0;
- near duplicate cross-lesson theo threshold validator = 0;
- trong cùng lesson, `_04..06` không chỉ đổi số từ `_01..03`;
- application không có shape-similarity kiểu basic/medium chỉ thay literal.

### 5.2 Difficulty progression
- basic = recall/nhận biết trực tiếp;
- medium = áp dụng có 1 bước suy luận/biến đổi;
- application = transfer/error-analysis/context inference, không chỉ số lớn hơn;
- giữ Grade-2 scope, không “nâng khó” bằng kiến thức ngoài baseline.

### 5.3 Hints
- 2 hint/question;
- không leak canonical answer chưa có trong đề;
- actionable, ngắn, child-facing;
- không quay về template lặp kiểu “Nhớ kiến thức…”;
- uniqueness/reuse/readability gates giữ xanh.

### 5.4 Explanation / worked example
- giải thích đủ evidence dẫn tới answer;
- mọi arithmetic equality/comparison instructional đúng;
- worked example khác practice và có ≥2 solution steps;
- solution chốt answer tường minh;
- không technical internal vocabulary.

### 5.5 Distractor/rationale
- distractor là misconception gần kiến thức, không giveaway khác miền;
- 4-choice không có 2 answer cùng nghĩa;
- rationale choice-specific, chỉ rõ vì sao choice sai và cách đúng;
- không vượt readability limit hiện hành;
- true/false balance và correct-position balance không lệch pattern.

### 5.6 Language / typography / accessibility-from-content
- NFC, no zero-width/control chars;
- whitespace/dấu câu sạch;
- phép tính có spacing nhất quán;
- mục tiêu/objective/concept definition tự nhiên, không boilerplate generated;
- text child-facing không chứa runtime/template/validator/json jargon.

### 5.7 Source / traceability
- curriculum/source root phải còn bind `vn_moet_math_grade2_tt32_2018` và source IDs đã xác minh;
- không bịa page/book citation hoặc denomination/source chưa có evidence;
- nếu cần source mapping chi tiết hơn nhưng repo chưa có dữ liệu nguồn đủ chắc, ghi TODO/handoff thay vì tự chế.

## 6. P2 — Breadth / future content

Runtime 402 hiện là contract ổn định. **Không tự nâng 402 → 603/804** chỉ để “nhiều câu hơn”. Chỉ mở rộng runtime nếu có product/board contract mới.

Trong lúc chưa có contract mới, AI1 vẫn có việc:
- draft-only alternate questions trong `tools/math_content_authoring/drafts/**`;
- synthetic content QA fixtures;
- audit coverage theo concept/misconception;
- chuẩn bị mapping cho future generator/template nhưng không sửa AI2 engine/template files;
- cải thiện authoring ergonomics/validators/tests để future expansion an toàn.

Hai skill historically không có adaptive generator template (`FOLD_CUT_COMPOSE_SHAPES`, `MONEY_VND_NOTE_RECOGNITION`) **không phải content gap** vì authored path có đủ bài. AI1 chỉ chuẩn bị content/spec nếu cần adaptive coverage; AI2 mới sở hữu generator runtime.

## 7. Test/acceptance gates trước mỗi commit content

Tối thiểu:
- Python compile PASS.
- Semantic validator 0 errors.
- Full content tests PASS.
- Pool6/publish regression PASS.
- Deterministic regenerate PASS.
- 402/67/6/2-per-difficulty invariant PASS.
- Manifest/hash PASS nếu JSON production đổi.
- `git diff --check` PASS.
- Nếu thay answer surface/metadata có consumer impact: chạy current persistence/Child UI smoke hoặc ghi handoff rõ cho AI2/AI3; không sửa chéo lane.

## 8. Commit discipline

- Một wave/commit.
- Commit source authoring + generated JSON + validator/test + AI1 status liên quan.
- Manifest dùng safe candidate từ current HEAD; preserve field lane khác.
- `git commit --only` explicit files.
- Push ngay khi gate xanh.
- Không stage WIP engine/UI.

## 9. FALLBACK — khi file shared bị lock

Không chờ. Chuyển ngay sang:
- draft-only question audit;
- duplicate/difficulty/readability scan;
- source/traceability audit;
- hint/rationale/explanation quality;
- Unicode/typography;
- prerequisite/ID/tag/validation-schema contracts;
- deterministic authoring tests;
- statistics/reporting trong AI1 status.

## 10. Handoff

Nếu AI1 đổi production content contract:
- ghi SHA + contract delta trong `AI1_MATH_CONTENT_STATUS.md` / file này;
- không sửa engine/UI consumer;
- AI2 verify grading/session/persistence;
- AI3 verify presentation/render/release payload.

Nếu AI2/AI3 commit consumer change trong lúc AI1 làm: inspect HEAD, adapt test/content only khi contract đã rõ; không reset work của họ.

## 11. STOP RULE AI1

AI1 chỉ dừng khi:
- current HEAD production content đã verify;
- không còn P0/P1 content bug có bằng chứng;
- backlog audit an toàn đã được chạy đủ một wave trong phiên hiện tại hoặc không tìm thấy issue mới;
- tests/hash/determinism xanh;
- AI1-owned files không còn uncommitted WIP của chính AI;
- commit/push hoàn tất;
- `AI1_MATH_CONTENT_STATUS.md` cập nhật evidence hiện tại.

`LANE_DONE=YES` chỉ có nghĩa strict Math content DoD đã đạt; lần sau được gọi vẫn phải START PROCEDURE + audit tiếp.
