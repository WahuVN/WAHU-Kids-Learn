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

## Contract còn chưa chốt

Các mục sau chưa được UI/content tự invent cho tới khi AI2 publish contract:

- retry cùng question và `attempt_index` semantics;
- skip policy;
- first-try / retry / hint scoring;
- lesson score và numeric XP nếu product thật sự cần;
- lesson completion/unlock/prerequisite/daily streak first-class persistence.

## Coordination rules

- AI1 có thể tăng content bằng answer metadata đã publish, không thêm validator thủ công theo từng lesson.
- AI3 không tính score/mastery/XP ở UI; UI chỉ gửi answer/hint/input intent và hiển thị outcome/summary.
- Mọi thay đổi tiếp theo vào answer kind, attempt semantics, scoring hoặc resume API phải cập nhật file này trước/đồng thời với commit tương ứng.
