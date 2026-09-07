# AI1 — MATH CONTENT INVENTORY

Audit: 2026-09-07
Owner: AI1 — Math Content & Data
Scope: `D:\APP HOC TAP`

## 1. Cấu trúc dữ liệu Toán tại thời điểm audit ban đầu

Tại thời điểm audit ban đầu, runtime chưa có hierarchy lesson đầy đủ. Cấu trúc khi đó là:

`subject(math) -> grade(2) -> curriculum skill -> VERIFIED generator template -> generated MathQuestion`

Nguồn chính:

- `curriculum/math_grade2/moet_baseline_v1.json`: baseline máy đọc, 67 skill.
- `docs/05_MATH_GRADE2_CURRICULUM.md`: mô tả curriculum.
- `content_packs/math_grade2_v1/verified_templates_v1.json`: 57 template, phủ 65/67 skill ở thời điểm audit.
- `src/Content/MathVerifiedTemplateSource.cs`: loader chỉ đọc template VERIFIED và variant.
- `src/Learning/MathQuestionGenerator.cs`: generator câu hỏi runtime.
- `src/Learning/MathLearningModels.cs`: contract câu hỏi hiện hỗ trợ integer/text choices và interaction integer.
- `src/Session/MathSessionCoordinator.cs`: nạp template và điều phối câu hỏi.
- `src/App/MathLessonForm.cs`: UI đang consume `MathQuestion`, không consume lesson catalog/lý thuyết tĩnh.
- `data/schema/001_initial.sql`: learner DB cố ý không lưu static curriculum/question bank.
- `tests/ContentRuntimeSmoke/Program.cs`, `tests/LearningSessionRuntimeSmoke/Program.cs`: smoke test hiện có.

## 2. Inventory theo trạng thái

| Hạng mục | Trạng thái | Kết quả audit |
|---|---|---|
| Baseline skill list | DONE | 67 skill, có hard guard Grade 2 |
| Đồng bộ docs -> baseline | DONE | Có smoke test kiểm ID từ docs có trong baseline |
| Generator templates | PARTIAL | 57 template / 65 skill; thiếu 2 skill baseline |
| Curriculum hierarchy chương/chủ đề/bài | MISSING | Chưa có machine-readable chapter/topic/lesson hierarchy |
| Lesson objectives | MISSING | Không có data source chuẩn |
| Lesson explanation/concepts | MISSING | Không có data source chuẩn |
| Worked examples + worked solutions | MISSING | Chỉ có prompt/template và hint runtime, không có catalog bài học |
| Static question bank | MISSING | Runtime sinh câu theo template; không có bank stable question ID |
| Stable question ID | BROKEN | Generated `QuestionId` dùng GUID, không deterministic |
| Hint metadata trong content | PARTIAL | Hint nằm trong generator code, không nằm trong content data |
| Difficulty metadata trong content | MISSING | Runtime tính `DifficultyFit`; content không có difficulty level/features |
| Prerequisite graph | MISSING | Docs yêu cầu prerequisite nhưng không có graph dữ liệu |
| Answer explanation metadata | MISSING | Không có explanation trong content JSON |
| Wrong-answer rationale | MISSING | Distractor sinh trong generator; không có rationale metadata chuẩn |
| Validation metadata | PARTIAL | Một số template có `validator`, nhiều template chỉ có prompt |
| Content validator | PARTIAL | `ContentPackValidator` chỉ kiểm manifest/hash/path/status, không kiểm semantic Math content |
| Content tests | PARTIAL | Có smoke test pack + generator nhưng chưa kiểm orphan/ref/difficulty/prerequisite/question bank |
| Data migration | DONE / N/A | Static curriculum đúng thiết kế nằm trong content pack, không migrate vào learner DB |
| Duplicate IDs | DONE (hiện tại) | Loader từ chối duplicate VERIFIED descriptor; cần validator toàn bộ content |
| Orphan/reference checks | MISSING | Chưa có semantic cross-reference validator |

## 3. Coverage hiện tại

- Baseline skill: **67**.
- Template: **57**.
- Skill được template phủ: **65/67**.
- Skill thiếu template tại thời điểm audit:
  - `FOLD_CUT_COMPOSE_SHAPES`
  - `MONEY_VND_NOTE_RECOGNITION`

## 4. Engine/UI consume data như thế nào

1. `MathVerifiedTemplateSource.Load()` chỉ lấy item `VERIFIED_A_TEMPLATE`.
2. `MathSessionCoordinator` map descriptor -> `MathTemplateRef`.
3. `AdaptiveMathSelector` chọn template.
4. `MathQuestionGenerator` sinh `MathQuestion` runtime.
5. `MathLessonForm` render `MathQuestion` và dùng `HintLevel1/HintLevel2` do generator tạo.

Hệ quả: thêm lesson catalog/question bank sẽ không tự xuất hiện trên UI nếu không có integration ở lane AI3. AI1 sẽ không refactor UI/engine; yêu cầu contract được ghi riêng.

## 5. Rủi ro dữ liệu đã phát hiện

- GUID ở `QuestionId` làm câu runtime không có stable content identity.
- Content pack chưa lưu mục tiêu học, prerequisite, concept, worked example, explanation hoặc rationale.
- Nhiều template mới chỉ có `prompt_vi`; semantic correctness phụ thuộc hoàn toàn generator code.
- Manifest đang có thay đổi song song chưa commit lúc audit; AI1 không reset hoặc ghi đè thay đổi đó.
- `tests/ContentRuntimeSmoke` đang hard-code pack version `1.8.0` trong khi working tree manifest đã là `1.9.0`; đây là integration drift cần tránh sửa shared test nếu không thuộc lane.

## 6. Quy ước ID AI1 sẽ dùng

- Chapter: `m2_chNN_<slug>`
- Topic: `m2_tpNN_<slug>`
- Lesson: `m2_ls_<skill-lower-snake>`
- Concept: `m2_cp_<skill-lower-snake>_NN`
- Example: `m2_ex_<skill-lower-snake>_NN`
- Question: `m2_q_<skill-lower-snake>_NN`

ID chỉ dùng ASCII lowercase + `_`, deterministic theo curriculum skill. Không đổi các skill ID uppercase hiện hữu để tránh phá learner progress.

## 7. Kế hoạch đóng gap

1. Tạo machine-readable curriculum/lesson catalog cho đủ 67 skill. — **DONE**
2. Tạo static curated question bank có stable ID và metadata. — **DONE**
3. Bổ sung semantic validator riêng cho Math content. — **DONE**
4. Bổ sung smoke test tự động cho lesson/question/reference/answer/prerequisite/difficulty. — **DONE**
5. Chỉ sau khi data pass validator mới cập nhật manifest/hash và version nếu không xung đột work song song. — **DONE cho file AI1; version/template hash của lane khác không bị stage**

## 8. Post-completion reclassification

Bảng ở mục 2 là ảnh chụp trạng thái **tại thời điểm audit ban đầu**. Sau các wave AI1, trạng thái Content/Data được phân loại lại như sau:

| Hạng mục | Trạng thái sau hoàn thiện | Kết quả |
|---|---|---|
| Baseline skill list | DONE | 67/67 skill |
| Curriculum hierarchy | DONE | 7 chapter, 17 topic, 67 lesson |
| Lesson objectives | DONE | Mỗi lesson có tối thiểu 2 mục tiêu |
| Lesson explanation/concepts | DONE | 67/67 lesson có explanation + concept definition |
| Worked examples + solutions | DONE | 67/67 lesson có worked example, solution steps, answer |
| Static question bank | DONE | 402 curated question, 6 question/skill; mỗi difficulty có 2 câu, session target vẫn 3 theo Request 009 |
| Stable static question ID | DONE | Deterministic `m2_q_<skill>_NN` |
| Hint metadata | DONE | 2 hint/question |
| Difficulty metadata | DONE | 134 basic + 134 medium + 134 application |
| Prerequisite graph | DONE | 9 root, 76 direct edge, 67/67 lesson reachable; no self-edge/cycle/redundant transitive edge |
| Answer explanation | DONE | 402/402 có explanation và semantic evidence |
| Wrong-answer rationale | DONE | 534/534 rationale unique, max repeat 1; structured/component/explicit contrast diagnosis đều fail-closed |
| Validation metadata | DONE | Numeric/text/unit/expression/interactive contract được kiểm semantic |
| Semantic content validator | DONE | 0 error ở final run |
| Content tests | DONE | 57/57 PASS ở current run |
| Duplicate IDs | DONE | 0 duplicate |
| Orphan/reference checks | DONE | 0 orphan, mọi practice ref resolve đúng lesson/difficulty |
| Manifest hash cho AI1 files | DONE | Catalog + question bank hash match manifest |
| VERIFIED runtime generator templates | PARTIAL | 57 template, phủ 65/67 baseline skill; thiếu generator template cho `FOLD_CUT_COMPOSE_SHAPES`, `MONEY_VND_NOTE_RECOGNITION` |
| Runtime/UI consumption của static catalog/bank | DONE cho content/session integration | Runtime 402-question surface + selected 3 from pool 6 + persistence smoke PASS |

## 9. Question-type và answer-kind contract sau khi AI2 mở rộng engine

AI1 tách rõ hai khái niệm:

- `question_type`: cách biểu diễn/tương tác của câu hỏi;
- `answer_kind`: cách engine kiểm tra đáp án.

Question bank Grade 2 hiện có đủ các dạng phù hợp curriculum và engine hiện hành:

- `numeric_input`;
- `multiple_choice`;
- `true_false`;
- `word_problem`;
- `expression_input`;
- `unit_input`;
- `interactive_measurement`.

Engine hiện hiểu `integer`, `interaction_integer`, `number`, `decimal`, `fraction`, `text`, `unit`, `expression`. Grade 2 content chỉ sử dụng `integer`, `interaction_integer`, `text`, `unit`, `expression`; `number`, `decimal`, `fraction` được khai báo là engine-supported nhưng cố ý không đưa vào bank lớp 2 vì không thuộc baseline hiện tại.

Matching/order không được tạo thành answer kind riêng vì engine chưa publish contract riêng cho hai dạng đó. Bài sắp xếp số vẫn có trong curriculum nhưng được biểu diễn bằng lựa chọn text theo contract đang chạy.

## 10. Final lane conclusion

Content/Data lane hiện **COMPLETE: 67/67 lesson, 402/402 question valid, 0 validator error, 57/57 content tests PASS, manifest/hash PASS và real-bank persistence integration PASS**. Request 006 đã CLOSED tại AI2 `3cf7fc6`; Request 009 đã CLOSED cho AI1 với pool 6 câu/lesson; Request 008/release-toolchain thuộc lane AI3. Legacy adaptive generator vẫn phủ 65/67 skill nhưng authored lesson path phủ đủ 67/67.
