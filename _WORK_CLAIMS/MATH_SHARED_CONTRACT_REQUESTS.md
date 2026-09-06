# MATH SHARED CONTRACT REQUESTS

## Request 001 — Lesson catalog consumption

- Contract cũ: runtime chỉ nạp `verified_templates_v1.json` -> `MathTemplateRef` -> `MathQuestion`; UI không có lesson metadata model.
- Contract cần: AI3/UI-integration cần đọc lesson catalog do AI1 cung cấp để hiển thị title, objective, explanation, concepts, worked example và lesson grouping mà không copy hard-code sang UI.
- Lý do: content hiện không thể hiện curriculum theo chương/chủ đề/bài; mọi nội dung học thuật ngoài prompt đang nằm rải rác trong code.
- File AI1 dự kiến cung cấp: `content_packs/math_grade2_v1/lesson_catalog_v1.json`.
- File có thể bị ảnh hưởng ở lane khác: `src/Content/*`, `src/Session/*`, `src/App/MathLessonForm.cs`, navigation/roadmap UI.
- Owner thực thi integration: AI3; AI1 không sửa UI lớn.

## Request 002 — Stable content question identity

- Contract cũ: `MathQuestion.QuestionId` được gán bằng `TemplateId + GUID` ở runtime.
- Contract cần: thêm/giữ riêng một `ContentQuestionId` hoặc `SourceQuestionId` deterministic nếu runtime bắt đầu consume static bank; `QuestionId` attempt instance có thể tiếp tục unique.
- Lý do: audit/content validation cần stable ID để trace đáp án, source lesson, duplicate và analytics theo câu; không nên thay semantics `attempt.question_id` đột ngột.
- File AI1 dự kiến cung cấp: `content_packs/math_grade2_v1/question_bank_v1.json` với stable question ID.
- File có thể bị ảnh hưởng ở lane khác: `src/Learning/MathLearningModels.cs`, `src/Session/MathSessionCoordinator.cs`, persistence/audit contract.
- Owner quyết định contract: AI2 + AI3.

## Request 003 — Answer kinds hỗ trợ

- Contract cũ: runtime thực tế có text-choice, integer choice/input và interaction integer; không có expression parser/matching/order contract hoàn chỉnh.
- Contract cần: nếu mở rộng engine, publish danh sách `answer_kind` chính thức và validator cho từng kind.
- Lý do: AI1 không đưa matching/order/expression tự do vào child-ready bank khi engine chưa thật sự hỗ trợ.
- File bị ảnh hưởng: `src/Learning/MathLearningModels.cs`, `src/Learning/MathQuestionGenerator.cs`, answer submission/UI input controls.
- Owner: AI2 engine + AI3 UI.
