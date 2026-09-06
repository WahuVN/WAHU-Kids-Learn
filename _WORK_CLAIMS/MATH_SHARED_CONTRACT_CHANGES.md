# Math shared contract changes

Cập nhật: 2026-09-07
Owners liên quan: AI1 Content/Data · AI2 Engine/Progress · AI3 UI/Integration

## 2026-09-07 — Answer validation contract (AI2, commit `11fa47d`)

`MathQuestion` có thêm metadata engine-level:

- `AcceptedAnswers: IList<string>` — các đáp án hợp lệ bổ sung.
- `NumericTolerance: double` — sai số tuyệt đối cho answer kind số; `0` nghĩa là so sánh chính xác.
- `ExpectedUnit: string` — đơn vị chuẩn khi `AnswerKind="unit"`.
- `AcceptedUnits: IList<string>` — alias đơn vị hợp lệ.

Các `AnswerKind` engine đã hiểu:

- `integer`, `interaction_integer`, `number`
- `decimal`
- `fraction`
- `text`
- `unit`
- `expression`

Quy tắc quan trọng:

1. AI1 **không** sửa hàng loạt câu hỏi để né string-equality. Hãy gắn metadata đúng loại; engine chịu trách nhiệm equivalence.
2. Fraction/decimal/integer có thể tương đương về Toán: ví dụ `4/2`, `2.0`, `2,0` có thể bằng `2`.
3. `expression` chỉ nhận biểu thức số giới hạn `+ - * / ( )`; không nhận variable/function tùy ý. Đây là fail-closed cố ý.
4. Unknown answer kind fallback về text comparison hẹp; engine không tự broaden parser.
5. AI3 truyền raw answer string vào `SubmitAnswer`; không tự normalize theo cách làm mất dấu âm, fraction, decimal separator hoặc unit.
6. `text` normalize Unicode Form C, không phân biệt hoa/thường và collapse whitespace; không fuzzy-match nội dung.

## 2026-09-07 — Durable idempotent answer commit (AI2, commit `a9dfcdf`)

Khóa semantic durable của attempt:

`(session_id, question_id, attempt_index)`

- Gửi lại cùng semantic key + cùng payload trả lại commit đã có (`AlreadyCommitted=true`), **không** ghi thêm attempt/error/mastery/child_skill/review.
- Cùng semantic key nhưng payload answer/correctness/hint khác phải fail conflict; không silently coi là replay.
- Retry thật sự của cùng question phải tăng `attempt_index`; không được tạo retry bằng GUID mới nhưng vẫn dùng cùng `attempt_index`.
- Attempt history vẫn append-only; migration V3 không UPDATE/DELETE attempt cũ.
- DB V2 từng có semantic duplicate được giữ nguyên lịch sử; `attempt_commit_key` pin vào earliest committed attempt.

AI3 không cần tự tạo idempotency logic ở UI. UI guard double-click vẫn hữu ích cho UX nhưng correctness nằm ở coordinator + DB.

## 2026-09-07 — Math suspend/resume contract (AI2, commits `a1d5146`, `beb0c0e`)

Ba đường kết thúc được tách rõ:

- `Complete()` — kết thúc thành công; session thành `completed`; runtime checkpoint bị xóa; mới đủ điều kiện nhận completed-session reward.
- `Abort(reason)` — chủ động bỏ phiên; session thành `aborted`; runtime checkpoint bị xóa; không resume.
- `Suspend(reason)` — đóng cửa sổ/app với ý nghĩa học tiếp; session vẫn `active`, runtime checkpoint được giữ, không reward.
- `Dispose()` của `MathSessionCoordinator` hiện mặc định gọi `Suspend`, **không** gọi `Abort`.

AI3 nên:

- để close/app shutdown tự rơi vào `Dispose()`/`Suspend` nếu muốn “học tiếp lần sau”;
- chỉ gọi `Abort` khi UX thực sự là “bỏ phiên này”;
- không tự complete chỉ vì đóng cửa sổ.

Runtime persistence giữ:

- session id;
- deterministic session seed;
- target question count;
- generated-question ordinal;
- current open question JSON;
- selection metadata cần để submit/audit;
- question start timestamp;
- forced prerequisite-repair template nếu có.

`Start(displayName)` behavior:

- ưu tiên resume latest active Math session có `math_session_runtime` cho primary child;
- nếu resume, constructor seed/target mới **không override** seed/target đã persist;
- chỉ recover dangling legacy session không có runtime checkpoint;
- `MathSessionStartResult` có thêm:
  - `CompletedQuestionCount`;
  - `ResumedExistingSession`;
  - `RestoredOpenQuestion`;
  - `DiscardedCorruptOpenQuestion`.

`NextQuestion()` behavior:

- nếu đã có câu đang mở, gọi lại trả đúng **cùng object/cùng question id**, không regenerate;
- câu mới được persist xuống DB trước khi trả cho caller;
- generation dùng deterministic per-question seed từ `(session seed, generated ordinal, template id)`;
- adaptive audit là diagnostic downstream; audit fail sau persistence không làm regenerate câu.

Khi resume:

- counters đúng/sai/hinted/distinct skills và behavior window được reconstruct từ committed DB attempts, không tin cache in-memory;
- mastery source-of-truth vẫn là `child_skill`/mastery events đã commit;
- nếu cached open question hỏng: engine bỏ **chỉ câu đang mở**, giữ toàn bộ committed progress;
- nếu cached question đã có committed semantic attempt (crash giữa answer commit và clear-cache): engine xóa stale open state và không hiển thị lại;
- suspend/restart không tạo garden reward; completed reward vẫn idempotent theo session.

## 2026-09-07 — Lesson-target / prerequisite / progress contract (AI2, schema V4)

Engine publish first-class lesson contract, không để UI tự suy đoán:

- `MathSessionCoordinator(..., lessonId)` mở targeted session cho đúng lesson đã chọn.
- Targeted session dùng đúng authored question IDs từ `practice_sets` theo thứ tự `basic → medium → application`; không chen adaptive repair question ngoài lesson.
- `MathSessionStartResult` publish `SessionMode`, `TargetLessonId`, `TargetLessonTitleVi`, `LessonAccess`.
- `math_session_runtime` persist `session_mode` + `target_lesson_id`, nên suspend/resume quay lại đúng targeted lesson và exact open question.
- `MathLessonProgressService.GetAccess/GetAllAccess` là source-of-truth cho locked/unlocked presentation.
- Prerequisite thỏa khi prerequisite lesson đã có `completed_count > 0`; dữ liệu legacy được tương thích nếu prerequisite skill đã `STABLE`.
- Coordinator kiểm lock trước khi tạo session; caller bypass UI vẫn không mở được lesson đang khóa.
- `math_lesson_progress` persist `started_count`, `completed_count`, `last_score_percent`, `best_score_percent`.
- Targeted lesson chỉ mark complete khi đã commit đủ authored target question count; score = `correct / target_count * 100`.
- Session completion + lesson completion/score được ghi trong cùng SQLite transaction.
- `MathSessionSummary` publish `SessionMode`, `TargetLessonId`, `LessonCompleted`, `LessonScorePercent`, `LessonBestScorePercent`.

AI3 có thể dùng contract này để render “Luyện bài này”, lock state và result score; không tự tính unlock threshold hoặc lesson score.

## 2026-09-07 — Advanced result contract (AI2)

Result summary được mở rộng bằng dữ liệu học thật, không thêm điểm thưởng giả:

- `MasteryChanges`: danh sách per-skill `{ SkillId, ScoreBefore, ScoreAfter, Delta }`; với nhiều attempt cùng skill, `ScoreBefore` là trước attempt đầu tiên và `ScoreAfter` là sau attempt cuối cùng của session.
- `ImprovedSkillCount`: số skill có delta dương.
- Targeted lesson publish shortcut `TargetSkillMasteryBefore`, `TargetSkillMasteryAfter`, `TargetSkillMasteryDelta` cho đúng skill của lesson.
- Suspend/resume reconstruct mastery change từ committed `attempt` + `mastery_event`; không tin cache/in-memory counter.
- Khi reconstruct, engine kiểm `mastery_event.delta == score_after - score_before`; dữ liệu persisted mâu thuẫn bị reject thay vì hiển thị delta sai.
- `NextLessonId` + `NextLessonTitleVi` chỉ được publish sau khi targeted lesson đã durable-complete và **bài liền kế trong thứ tự curriculum đang unlocked**.
- Nếu bài liền kế còn locked hoặc đã ở bài cuối, `NextLessonId/Title` để `null`; UI không tự nhảy qua lesson khóa để tìm một lesson khác.
- Numeric XP **không được publish** ở wave này vì product chưa có rule first-class; garden reward hiện tại vẫn là reward contract riêng.

AI3 có thể dùng các field trên trực tiếp cho result screen; không tự tính mastery delta hoặc “bài tiếp theo”.

## 2026-09-07 — Request 007: corrupt authored cursor recovery (AI2)

Targeted lesson không được bỏ qua authored ordinal khi cache câu đang mở bị hỏng:

- Sau restore, committed attempts vẫn là source-of-truth cho tiến độ lesson.
- Nếu open-question cache bị thiếu/hỏng và `session_mode=lesson`, engine reconcile `generated_question_count` về số authored attempts đã commit trước khi clear checkpoint.
- Vì vậy fixture `basic committed → medium opened → medium cache corrupt` phải resume ở đúng **medium**, rồi mới tới application.
- Lesson chỉ complete khi đủ toàn bộ authored target attempts; corrupt cache không được làm pool hết sớm ở 2/3.
- Adaptive session giữ behavior recovery cũ; cursor rollback này chỉ áp dụng targeted lesson.
- Regression `MathSessionPersistenceRuntimeSmoke` khóa cả durable cursor sau reconcile và đúng `ContentQuestionId` medium/application.

## 2026-09-07 — Retry / first-try / hint scoring contract (AI2)

Retry là opt-in engine contract; API one-shot hiện tại vẫn giữ nguyên để không đổi hành vi UI cũ:

- `SubmitAnswer(...)` / `SubmitAnswerAt(...)`: one-shot, một submit luôn finalize câu như trước.
- `SubmitAnswerWithRetry(...)`: **initial intent** cho attempt 1; nếu sai và chưa fatigue-stop thì giữ nguyên câu để retry.
- `SubmitRetryAnswer(...)`: **explicit retry intent** cho attempt 2. Engine reject initial intent gửi trùng khi câu đang chờ retry, nên double-submit không tự tiêu retry.
- `MaxAttemptsPerQuestion = 2`; semantic key vẫn là `(session_id, question_id, attempt_index)` với `attempt_index` lần lượt 1 rồi 2.
- Sai attempt 1 trong retry flow vẫn ghi immutable `attempt` + error/behavior evidence, nhưng **không** ghi `mastery_event`, không update `child_skill`, `review_schedule`, lesson score hoặc completed-question progress.
- Attempt 2 finalize đúng một lần. Retry đúng dùng assisted mastery weight tối thiểu tương đương hint level 1 và reason `retry_assisted_attempt`; không được tính là independent success. Retry sai chỉ tạo **một** negative mastery update ở attempt finalize.
- Suspend/resume phục hồi đúng cùng `QuestionId`; `MathSessionStartResult.RetryPending=true` và `CurrentAttemptIndex=2` khi đang chờ retry.
- `MathAnswerOutcome` publish `AttemptIndex`, `QuestionCompleted`, `CanRetry`, `IsRetry`, `IndependentSuccess`; `Mastery/Review` có thể `null` ở first-wrong pending retry.
- `MathSessionSummary.Attempts` tiếp tục nghĩa là **completed questions** để backward-compatible. Thêm `AnswerAttempts`, `IndependentCorrect`, `RetriedQuestions`, `RetriedCorrect`.
- `HintedCorrect` chỉ đếm success có **gợi ý thật sự được mở**; retry-correct không hint nằm ở `RetriedCorrect`, không được UI suy `independent = correct - hinted` nữa. UI phải dùng `IndependentCorrect`/`RetriedCorrect` khi hiển thị result retry-aware.
- Resume/rebuild lấy final question state từ mastery-bearing attempt; pending first-wrong không làm tăng completed-question count và không double mastery.

## Contract còn chưa chốt

Các mục sau chưa được UI/content tự invent cho tới khi AI2 publish contract:

- skip policy;
- numeric XP nếu product thật sự cần;
- daily streak first-class contract.

## Coordination rules

- AI1 có thể tăng content bằng answer metadata đã publish, không thêm validator thủ công theo từng lesson.
- AI3 không tính score/mastery/XP ở UI; UI chỉ gửi answer/hint/input intent và hiển thị outcome/summary.
- Mọi thay đổi tiếp theo vào answer kind, attempt semantics, scoring hoặc resume API phải cập nhật file này trước/đồng thời với commit tương ứng.
