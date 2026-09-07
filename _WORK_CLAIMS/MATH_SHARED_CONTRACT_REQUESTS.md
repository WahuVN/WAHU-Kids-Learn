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
- Regression cần từ AI2: generated `draw_segment_given_length` phải có `AnswerKind == "interaction_integer"`, `DisplayChoices.Count == 0`, và `IsCorrectAnswer()` vẫn chấm numeric đúng. AI3 đã tách WIP generator lên clean `0dc756a` và fuzz 100 seed: **1.100 assertions PASS**; temp Child UI thay fixture dựng tay bằng `MathQuestionGenerator(...).Generate(...)` vẫn **1596 assertions PASS**. Vì vậy behavior kỹ thuật đã được verify, nhưng Request 004 chỉ CLOSED khi owner AI2 commit generator + engine smoke vào stable HEAD.
- File owner sửa: `src/Learning/MathQuestionGenerator.cs` + engine smoke tương ứng.
- Owner: AI2 engine. AI3 không sửa generator để tránh conflict ownership.

## Request 005 — Route authored question bank into real Math sessions

- Engine core đã được commit vào `main` tại `656a94b`: `MathSessionCoordinator` có `lesson` mode, load `lesson_catalog_v1.json` + `question_bank_v1.json`, lấy đúng `basic -> medium -> application` từ `PracticeSets`, đặt target theo số câu của bài và tạo runtime instance giữ `ContentQuestionId`.
- Runtime regression hiện tại: `MathSessionPersistenceRuntimeSmoke` build x86 sạch và PASS **92 assertions**, đã cover lesson lock/unlock, authored basic/medium/application, exact open-question resume, completion score và unlock bài phụ thuộc.
- UI handoff đã được commit tại `1436705`: `MathHubForm` có `Luyện 3 câu bài này`; `MathLessonForm` nhận `targetLessonId`, mở `MathSessionCoordinator(..., lessonId)` và render đủ 201 authored questions qua 109 typed + 91 choice + 1 interaction surface.
- Stable identity: khi dùng authored content, giữ `ContentQuestionId` deterministic cho trace/chống lặp; `QuestionId` tiếp tục là instance ID unique để giữ idempotency attempt hiện tại.
- Trạng thái functional integration: **CLOSED cho normal path** — engine + UI đều đã commit; Child UI PASS **1133 assertions**, all-201 sweep PASS và Flow 5 prerequisite unlock PASS. Release-clean full solution vẫn bị production Data/SQLite toolchain chặn. Request 007 vẫn mở độc lập cho corrupt-cache recovery; không được dùng normal-path PASS để bỏ edge case này.
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

## Request 008 — Release payload must hard-guard Math lesson runtime files

- Packaging logic ở stable HEAD đã đúng hướng: `Build-SetupArtifacts.ps1` copy đệ quy toàn bộ `content_packs\\*` và `data\\schema\\*.sql`; Inno Setup cũng copy đệ quy toàn bộ staged publish tree. Schema `004_math_lesson_progress.sql` đã nằm trong hard deployment guard.
- Tuy nhiên release E2E hiện chỉ bắt buộc `content_packs\\math_grade2_v1\\manifest.json`, chưa bắt buộc ba file runtime mà Math Hub/targeted lesson thật sự cần: `lesson_catalog_v1.json`, `question_bank_v1.json`, `verified_templates_v1.json`.
- Evidence artifact hiện có: `build/win7_x86/release_manifest_dev.json` là build `0.1.41-dev` từ commit `e299c41`, database schema 2. Publish tree và portable ZIP của artifact này có `verified_templates_v1.json` nhưng **không có** `lesson_catalog_v1.json`, `question_bank_v1.json` hoặc `004_math_lesson_progress.sql`; đây là artifact cũ, không được dùng làm release evidence cho Math hiện tại.
- Contract/gate cần ở release lane: staged publish guard phải `Require-File` cả ba Math runtime JSON; `Test-PortableE2E.ps1` và `Test-InstallerE2E.ps1` phải thêm cả ba vào required payload list, cùng toàn bộ schema hiện hành. WIP release hiện đã tiến tới schema V5 (`005_math_runtime_pack_identity.sql`) nhưng vẫn chưa hard-guard ba JSON này, nên Request 008 vẫn OPEN.
- Gate mạnh hơn nên mở app từ portable/installed payload và xác nhận Math catalog load được 7 chương / 17 chủ đề / 67 bài, authored bank load đủ 201 câu; không chỉ kiểm file tồn tại.
- Khi rebuild release artifact mới, release manifest phải phản ánh **database schema version hiện hành** (WIP hiện là V5, không được lùi về 4) và git commit chứa `424185f`/sau đó; portable + installer phải giữ learner DB/lesson progress qua relaunch/reinstall theo policy hiện tại.
- Owner: release/build lane. AI3 chỉ audit/integration regression, không sửa `tools/build/*` khi file đang có owner/WIP khác.

## Request 009 — Decouple authored lesson pool size from 3-question session target

- Contract hiện tại: mỗi lesson có đúng `1 basic + 1 medium + 1 application`; `MathSessionCoordinator` nối toàn bộ `PracticeSets` theo thứ tự `basic -> medium -> application` rồi đặt `TargetQuestionCount = PracticeSets.TotalCount`. AI3 đã dựng synthetic pool 6 ngoài repo trên stable `424185f`: coordinator trả `TargetQuestionCount=6`, tiêu thụ đủ 6 `ContentQuestionId` theo toàn bộ bucket order và chỉ complete sau 6 attempts (**32 assertions PASS**). Vì vậy tăng bank lên 6/9 câu hiện chắc chắn đồng thời kéo dài phiên lên 6/9 câu; Request 009 engine vẫn OPEN.
- Mục tiêu UX/content: cho phép AI1 mở rộng mỗi lesson thành **pool >= 6 câu** (tối thiểu 2 basic + 2 medium + 2 application) nhưng **mỗi lesson session mặc định vẫn 3 câu**, lấy cân bằng **1 basic + 1 medium + 1 application**. `Luyện 3 câu bài này` không được biến thành phiên dài chỉ vì pool lớn hơn.
- Contract engine cần: tách `authored_pool_count` khỏi `TargetQuestionCount`; khi bắt đầu fresh targeted session, chọn một ordered set 3 `ContentQuestionId` từ ba difficulty buckets. Cùng seed/selection identity phải deterministic để test; các fresh session với selection identity khác phải có khả năng chọn set khác thay vì luôn lấy phần tử đầu.
- Persistence bắt buộc: ordered selected `ContentQuestionId` của session phải survive suspend/resume. Resume, retry và corrupt-open-question recovery phải tiếp tục trên **selected session set**, không re-select từ pool và không skip ordinal. Request 007 semantics vẫn áp dụng trên selected set.
- Progress/mastery: completion/score vẫn dựa trên `TargetQuestionCount = 3`, không dựa trên tổng số câu trong authored pool. Pool size tăng không được làm lesson khó complete hơn chỉ do có thêm nội dung.
- UI contract: Hub hiển thị `N câu trong ngân hàng bài học` theo pool size; AI3-012 đã decouple CTA thành `Luyện bài này` / `Luyện lại bài này` và bỏ numeric badge để không invent target trước khi engine publish preview contract. Lesson form/progress/result tiếp tục dùng `TargetQuestionCount` thật từ session start. Không hard-code pool size = target size.
- Content contract sau khi AI2/AI3 chốt: AI1 sẽ cho phép nhiều ID trong từng `practice_sets.basic/medium/application`, giữ mỗi question được reference đúng một lần, giữ difficulty metadata khớp bucket và mở rộng bank deterministic. Không tạo orphan pool field song song mà runtime không consume.
- Regression AI2 cần: lesson fixture có >=2 câu mỗi difficulty; hai fresh session với hai selection identity cố định phải tạo set hợp lệ và ít nhất một ID khác nhau; resume cùng session phải giữ nguyên ordered selected set; retry/corrupt recovery không đổi set; completion vẫn đúng sau 3 committed attempts.
- Regression AI3 cần: Hub phân biệt pool count với session target; neutral CTA + no numeric badge đã PASS ở AI3-012 với synthetic pool 6. Sau khi AI2 publish selected-set contract, targeted lesson phải render đúng selected 3 câu bất kể pool có 6+; result/progress vẫn `3/3`, resume/retry không đổi selected set.
- Owner: AI2 session/persistence + AI3 UI/integration. AI1 chỉ mở rộng bank sau khi contract này có runtime regression xanh để tránh phá end-to-end hiện tại.
