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

## Request 004 — Preserve `interaction_integer` through generator finalization

- Contract cũ/WIP: `DrawSegmentGivenLength()` tạo `AnswerKind = "interaction_integer"`, không có choice; nhưng `MathQuestionGenerator.FinalizeAnswerOptions()` hiện ép mọi non-text question về `AnswerKind = "integer"` và tự sinh 4 lựa chọn.
- Contract cần: nếu `AnswerKind == "interaction_integer"`, giữ nguyên answer kind, `CorrectAnswerText`, để `Choices`/`ChoiceTexts` rỗng và return trước nhánh auto-build numeric choices.
- Lý do: AI3 đã có UI tương tác vẽ đoạn thẳng; nếu generator đổi kind thành `integer`, runtime không bao giờ đi vào control tương tác và bài mới lại thành trắc nghiệm.
- Regression cần từ AI2: generated `draw_segment_given_length` phải có `AnswerKind == "interaction_integer"`, `DisplayChoices.Count == 0`, và `IsCorrectAnswer()` vẫn chấm numeric đúng.
- File owner sửa: `src/Learning/MathQuestionGenerator.cs` + engine smoke tương ứng.
- Owner: AI2 engine. AI3 không sửa generator để tránh conflict ownership.

## Request 005 — Route authored question bank into real Math sessions

- Trạng thái WIP hiện tại: core path đã xuất hiện trong working tree. `MathSessionCoordinator` có `lesson` mode, load `lesson_catalog_v1.json` + `question_bank_v1.json`, lấy đúng `basic -> medium -> application` từ `PracticeSets`, đặt target theo số câu của bài và tạo runtime instance giữ `ContentQuestionId`.
- Runtime regression hiện tại: `MathSessionPersistenceRuntimeSmoke` build x86 sạch và PASS **92 assertions**, đã cover lesson lock/unlock, authored basic/medium/application, exact open-question resume, completion score và unlock bài phụ thuộc.
- UI handoff WIP: `MathHubForm` đã có nút `Luyện 3 câu bài này`; `MathLessonForm` nhận `targetLessonId` và mở `MathSessionCoordinator(..., lessonId)` thay vì adaptive generator path.
- Stable identity: khi dùng authored content, giữ `ContentQuestionId` deterministic cho trace/chống lặp; `QuestionId` tiếp tục là instance ID unique để giữ idempotency attempt hiện tại.
- Trạng thái đóng: **chưa CLOSED** cho tới khi engine/UI WIP được commit, full UI/build gate xanh, và corrupt-cache lesson-mode ở Request 007 có regression riêng. Không được coi 92 assertions hiện tại là đủ để bỏ Request 007.
- Owner: AI2 engine/session + AI3 UI handoff.

## Request 006 — Preserve `answer_unit` as display metadata without forcing unit input

- Static bank hiện có 23 câu `answer_kind = "integer"` mang `answer_unit` (`cm`, `kg`, `l`, `dm`, `m`, `ngày`, `giờ`, `phút`). Các câu này cố ý cho trẻ nhập số, vì đơn vị đã nêu rõ trong prompt.
- Loader `MathAuthoredQuestionSource` hiện bỏ qua `answer_unit`; `CorrectAnswerDisplay` vì thế có thể thành `5`, `8`, `60` thay vì `5 kg`, `8 cm`, `60 phút` trong feedback/result.
- Không đổi hàng loạt các câu này sang `answer_kind = "unit"`: điều đó sẽ thay contract input và bắt trẻ gõ đơn vị, không đúng ý đồ UX hiện tại.
- Contract cần: thêm/preserve display-only unit metadata (có thể `AnswerUnit` hoặc tương đương), để validator vẫn chấm integer nhưng UI/feedback có thể format đáp án kèm đơn vị. Metadata này phải survive load -> runtime instance -> suspend/resume JSON.
- Regression cần: authored integer question có `answer_unit = "cm"` vẫn chấp nhận raw answer `8`, không chấp nhận nội dung sai, và outcome/display có thể render `8 cm` mà không thay `AnswerKind`.
- Owner: AI2 model/session + AI3 presentation.

## Request 007 — Corrupt authored open-question recovery must not skip lesson ordinal

- Edge case hiện tại: lesson mode tăng `generated_question_count` khi phát authored question. Nếu `current_question_json` của câu đang mở bị hỏng sau đó, restore rebuild committed attempts nhưng chỉ nâng `_generatedQuestionCount` khi nó **nhỏ hơn** attempts; giá trị lớn hơn attempts vẫn được giữ.
- Với bài 3 câu: đã commit basic (`attempts=1`), medium đang mở (`generated=2`) rồi cache medium hỏng. Restore discard cache nhưng giữ `generated=2`; `NextQuestion()` lấy `_targetQuestions[2]` = application, tức **skip medium**. Sau application có thể thành `attempts=2`, `generated=3`, pool hết dù target vẫn 3.
- Contract cần: khi lesson-mode discard một open question chưa commit, ordinal phát câu phải rollback/reconcile về committed authored progress, để câu bị mất cache được phát lại từ stable `ContentQuestionId` đúng vị trí thay vì bị skip.
- Không được rollback committed attempts/mastery/progress; chỉ sửa/reconcile cursor của open authored question.
- Regression bắt buộc: tạo lesson session 3 câu, commit câu 1, mở câu 2, corrupt `current_question_json`, suspend/resume; sau restore phải có `DiscardedCorruptOpenQuestion=true`, `CompletedQuestionCount=1`, câu tiếp theo phải có đúng `ContentQuestionId` của **medium**, sau đó application; đủ 3 attempts mới complete lesson.
- Owner: AI2 session/persistence. AI1 không sửa coordinator để tránh conflict ownership.
