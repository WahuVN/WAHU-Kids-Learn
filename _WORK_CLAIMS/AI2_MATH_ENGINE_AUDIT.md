# AI2 — Math engine audit

Cập nhật: 2026-09-07 (Asia/Ho_Chi_Minh)
Owner: AI2 — Math Engine / Exercise / Progress

## Scope đã đọc

Luồng đã kiểm tra trực tiếp:

`content Math -> selector -> generator -> MathSessionCoordinator -> MathQuestion.IsCorrectAnswer -> MathErrorClassifierV1 -> MasteryEngineV1 -> ReviewSchedulerV1 -> AnswerCommitService -> SQLite attempt/error/mastery/child_skill/review -> session complete -> GameWorldRewardService -> reload`

Đã đọc thêm schema/migration, learner session service, roadmap/progress aggregation, app Math form đang consume session API và runtime smoke tests.

## P0 — crash / data loss

- Chưa thấy đường ghi attempt nào UPDATE lịch sử: migration V2 đã khóa `attempt` immutable bằng trigger. Đây là điểm tốt.
- Build/runtime test toàn Data hiện phụ thuộc restore `System.Data.SQLite`; baseline `dotnet msbuild` không restore trước nên không resolve SQLite. Cần chuẩn hóa lệnh test restore + build tuần tự, không kết luận engine fail từ lỗi môi trường này.
- `MathSessionCoordinator.Dispose()`/UI close hiện abort session; nếu app đóng bình thường giữa lesson thì session không còn resumable. Progress các attempt đã commit vẫn còn, nhưng session continuity bị mất. Phân loại P1/P2 về persistence, không mất committed attempt.

## P1 — sai kết quả / sai progress

1. **Answer validation cũ chỉ text equality hoặc `int.TryParse`**
   - Không nhận fraction tương đương, decimal/comma, tolerance, unit, nhiều đáp án hợp lệ.
   - Đã sửa trong wave answer-validation bằng `MathAnswerValidator` và regression smoke.

2. **Duplicate submit / duplicate event chưa có idempotency key ở DB**
   - `attempt_id` là GUID mới mỗi submit, nên cùng `(session, question, attempt_index)` có thể được ghi hai lần nếu submit concurrency lọt qua UI.
   - `SerializedWriteCoordinator` chỉ serialize phần write; mastery được tính trước transaction nên hai submit đồng thời có thể cùng dựa trên snapshot cũ.
   - Cần unique constraint semantic + idempotent commit result + coordinator submit gate.

3. **Stale mastery overwrite**
   - `AnswerCommitService.UpsertChildSkill` ghi snapshot tuyệt đối. Nếu hai answer cùng skill được tính từ cùng state cũ rồi lần lượt commit, lần commit sau có thể overwrite tiến bộ của lần trước.
   - Khóa submit theo coordinator giảm đường phổ biến; DB semantic idempotency và test concurrency vẫn bắt buộc.

4. **Resume hiện không phải resume**
   - `Start()` gọi `RecoverDanglingSessions()` rồi luôn `BeginSession()`.
   - Session trước bị chuyển sang `recovered`; summary/counters session mới về 0.
   - Không persist current open question hoặc deterministic runtime seed/session position.

## P2 — thiếu chức năng

- Chưa có persistence contract cho open Math question, seed, generated-question index và target count.
- Chưa có resume API trả lại active Math session + counters.
- Chưa có retry cùng question; hiện mỗi submit đóng question, sai thì session ép repair template cho câu sau.
- Skip chưa có API.
- Lesson score/XP theo điểm số chưa có; hiện reward Math là `garden_growth` theo completed session, idempotent bằng `source_key`.
- Unlock/prerequisite lesson-level chưa có engine riêng; roadmap hiện chỉ aggregate `child_skill` theo nhóm.
- Daily progress/streak chưa có contract Math riêng trong engine hiện tại.
- Generated question random có constructor seed và có fuzz test, nhưng seed không được persist theo session nên restart không deterministic theo cùng session.

## P3 — UX / edge cases

- UI có `_submitting` guard, nhưng engine/API vẫn cần tự bảo vệ double-submit.
- Error classifier cũ gọi fraction/decimal sai là `INPUT_FORMAT_ERROR`; đã sửa để validator quyết correctness trước và phân biệt well-formed numeric wrong answer.
- Current unanswered question khi app đóng hiện không recover được nguyên trạng.
- Corrupted cached/runtime session chưa có recovery policy vì chưa có runtime persistence table.

## Existing strengths cần giữ

- `attempt` append-only từ schema V2.
- Answer commit ghi attempt + error + mastery + child_skill + review trong một SQLite transaction.
- Completed-session garden reward đã idempotent theo `UNIQUE(child_id, source_key)` và `INSERT OR IGNORE`.
- Mastery phân biệt independent success và hinted success; hinted delta thấp hơn first-try.
- Review scheduler có due time rõ theo wrong/hinted/mastery/stable.
- Selector + generator nhận seed; generator fuzz coverage hiện có trong LearningSessionRuntimeSmoke.

## Kế hoạch sửa theo wave

1. Answer validation — DONE, commit `11fa47d`.
2. Session idempotency — semantic attempt uniqueness + submit gate + duplicate replay test.
3. Persistence/resume — schema/runtime state, seed/target/current question, reconstruct counters, restart test.
4. Scoring/mastery — chốt retry/hint semantics, stale-state regression, reward anti-farm.
5. Test integration — restore/build command, unit + integration + persistence + regression status.

## Resolution update — 2026-09-07

Các finding baseline sau đã được đóng:

- Answer string equality P1 → FIXED `11fa47d`.
- Semantic duplicate submit/event P1 → FIXED DB V3 `a9dfcdf` + coordinator submit gate `beb0c0e`.
- Resume giả / mất session continuity P1/P2 → FIXED `a1d5146` + `beb0c0e`.
- Session seed/target/open-question không persist → FIXED.
- Dispose mặc định abort → FIXED, hiện suspend.
- Stale cached question sau answer commit → FIXED bằng `attempt_commit_key` check khi resume.
- Corrupted cached current question → FIXED theo fail-soft: bỏ câu mở, giữ committed progress.

Regression hiện có: Math answer 47 assertions, Data/idempotency 34 assertions, Session persistence/resume 43 assertions đều PASS.

Finding còn mở: retry/skip first-class, retry/hint scoring semantics, lesson-level completion/score/XP/unlock/prerequisite/daily progress contract và concurrency regression rộng hơn ngoài một coordinator.

## Concurrent-work note

Khi audit bắt đầu, working tree đã có thay đổi song song cho `draw_segment_given_length`, `interaction_integer` và UI control. AI2 không revert/overwrite các thay đổi đó. Commit AI2 stage theo file/hunk để không nuốt phần UI/content của AI1/AI3.
