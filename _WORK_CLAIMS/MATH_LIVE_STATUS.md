# MATH LIVE STATUS

Updated: 2026-09-07
Definition: % dưới đây đo theo Definition of Done strict của Math, không lấy việc “mở được màn hình” làm DONE.

## Overall

- **Content: 98%** — 7 chương, 17 chủ đề, 67 lesson, 201 câu authored, prerequisite graph, difficulty progression, distractor rationale, worked-example separation, lesson-specific objectives và semantic validator đều có. `MathContentDataSmoke`: **36/36 PASS** tại clean HEAD `8f10d4d`.
- **Engine: 97%** — answer validation/equivalence, mastery/review/reward, idempotency, exact suspend/resume, authored bank, schema V4 lesson progress, lesson-targeted session, prerequisite unlock, targeted score/best score, mastery delta, next lesson, corrupt authored cursor recovery, retry/first-try scoring, write-failure reconciliation và concurrent session/mastery guards đều có contract/gate first-class. Clean persistence gate tại `8f10d4d`: **171 assertions PASS**. Generator segment vẫn chờ commit ổn định riêng.
- **UI: 99%** — Math Hub, toàn bộ 67 lesson detail, theory/example, adaptive mission, lesson-targeted practice, locked/unlocked, exact resume, choice/typed/interaction answers, retry cùng câu cho cả 3 answer surfaces, exact retry-resume, child-safe write-failure recovery và result first-try/retry counters + lesson score/best score + mastery delta + next lesson đều consume contract thật.
- **Test: 98%** — Child UI **1593 assertions PASS**; 67/67 lesson-detail/access sweep PASS; 201/201 authored answer-surface sweep PASS; targeted Flow 5 + retry typed/choice/interaction + exact retry-resume + injected SQLite write-failure recovery trên choice/typed/interaction + advanced-result UI E2E PASS; persistence **171 assertions PASS**; content **36/36 PASS**. Full production solution **Rebuild Release/x86 PASS** bằng Visual Studio MSBuild; release smoke còn 3 blocker ngoài Math UI.
- **E2E: 95%** — Home → Math Hub → lesson → targeted authored practice → retry cùng câu → suspend/relaunch → exact attempt-2 resume → recoverable DB-write failure → retry same question → result → mastery/next-lesson presentation → persisted lesson progress → Hub refresh → prerequisite unlock PASS; adaptive mission baseline PASS. Release-clean distribution gate chưa xanh vì runtime-config/English smoke + Request 008 packaging.
- **Tổng Math: ~95%** theo strict production Definition of Done hiện tại.

## P0

- Không phát hiện P0 Math UI/integration mới trong wave AI3-011.

## P1 còn mở

1. **Release runtime-config smoke:** full solution build đã PASS, nhưng `SetupPreflightSmoke` và `UpdateRuntimeSmoke` cùng fail vì `database_runtime_v1.json schema_version=4, expected=3` trong `RuntimeConfigBundle`. Đây là Platform/release blocker, không phải Data/SQLite compile blocker.
2. **Bundled English content smoke:** `ContentRuntimeSmoke` fail `ASSERT_FAIL: bundled_english_verified`; blocker này nằm ngoài Math UI nhưng chặn full distribution smoke gate.
3. **Adaptive segment generator:** UI interaction contract và authored interaction đều render được; case generator `draw_segment_given_length` đang thấy trong WIP Learning nhưng chưa nằm trong HEAD ổn định. Child UI smoke chỉ test UI contract; generator behavior thuộc engine smoke AI2.
4. **Release packaging/E2E — Request 008:** staged/portable/installer guards chưa hard-require đủ `lesson_catalog_v1.json`, `question_bank_v1.json`, `verified_templates_v1.json`; artifact `0.1.41-dev` cũ không phải release evidence cho Math hiện tại.
5. **Expanded authored pool — Request 009:** lesson detail đã phân biệt pool count, lesson form/progress dùng `TargetQuestionCount` thật; riêng Hub CTA hiện vẫn lấy `PracticeSets.TotalCount` làm số câu session. Chưa sửa bằng hard-code `3`; chờ AI2 publish first-class targeted-session target/selected-set contract rồi AI3 khóa pool >=6 → session vẫn 3.

## Blocker đã đóng

- **Exact resume:** CLOSED — `Suspend/Resume`, exact open question, no duplicate/stale replay.
- **Authored bank runtime:** CLOSED — 201 câu loadable/traceable theo lesson.
- **Lesson target + prerequisite unlock:** CLOSED upstream bởi `656a94b` — session mode `lesson`, target lesson durable, access snapshot, completion/score, unlock.
- **Typed authored answers:** CLOSED AI3-005 — numeric input, word problem, expression và unit dùng typed-answer surface thay vì fatal vì thiếu choices.
- **All authored UI compatibility:** CLOSED AI3-005 — **201/201** câu render được: **109 typed + 91 choice + 1 interaction**.
- **Flow 5:** CLOSED AI3-005 — hoàn thành prerequisite qua UI thật → lesson progress 100% persisted → Hub reload → bài phụ thuộc unlock.
- **Advanced result presentation:** CLOSED AI3-006 — mastery hiện tại/delta và `NextLessonTitleVi` hiển thị trực tiếp từ summary `8b32944`; adaptive mission dùng `ImprovedSkillCount`; không tự tính XP hay next lesson.
- **Result route mismatch:** CLOSED AI3-004 — `Về thư viện Toán` đúng route thực tế.
- **Corrupt authored resume ordinal:** CLOSED upstream `7f79367` — cache câu medium hỏng được rollback cursor về committed ordinal, phát lại medium rồi application; đủ 3 attempts mới complete lesson.
- **Retry/first-try UI:** CLOSED AI3-008 — typed, choice và interaction giữ nguyên câu sau first-try sai; progress không tăng trước khi question finalize; resume retry giữ attempt 2; result dùng `IndependentCorrect`, `RetriedQuestions`, `RetriedCorrect` first-class thay vì suy luận.
- **Recoverable answer-write failure UI:** CLOSED AI3-010 — khi engine rollback transaction và giữ đúng open question, UI không abort session; answer surface được mở lại child-safe, không ghost attempt, và retry lưu lại giữ đúng independent/retried semantics. Nếu coordinator không xác nhận được cùng open question thì UI vẫn fail-closed/abort như trước.
- **Production Data/SQLite clean build:** CLOSED — clean detached `12cb5ee` dùng đúng `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`, `WAHUKidsLearn.sln /restore /t:Rebuild /p:Configuration=Release /p:Platform=x86` PASS; `WAHU.Data`, `WAHU.Session`, App và SQLite smoke project đều build production path thật.

## Current verified gates

- Current workspace: ChildUiRuntimeSmoke **1593 assertions PASS**.
- 67/67 lesson-detail/access sweep: **PASS** — mỗi lesson select được, title/objective/example/practice render, CTA accessibility tồn tại và `Enabled` khớp `MathLessonAccessSnapshot.IsUnlocked`.
- All-201 authored answer-surface sweep: **PASS — 201/201**.
- Targeted lesson Flow 5 + advanced result presentation: **PASS**.
- Retry UI E2E: **PASS** — typed, choice, authored interaction; first-try sai không tăng completed count, retry đúng finalize đúng một lần, result giữ first-try semantics.
- Retry-resume UI E2E: **PASS** — suspend ở attempt 2 → mở lại đúng authored `ContentQuestionId`, `_retryPending=true`, progress/support đúng retry state, choices editable và retry-correct finalize assisted.
- Submit-write recovery E2E: **PASS** — injected SQLite trigger làm fail transaction ở first attempt và retry attempt 2; UI giữ cùng open question/session, counters không tăng, không reveal answer, bỏ trigger rồi submit lại commit đúng một durable attempt.
- Clean detached worktree tại `8f10d4d` + đúng 1 file test AI3-011:
  - Production `WAHUKidsLearn.sln` Rebuild Release/x86 bằng Visual Studio 2022 Community MSBuild: **PASS**.
  - ChildUiRuntimeSmoke: **PASS — 1593 assertions**.
  - MathSessionPersistenceRuntimeSmoke: **PASS — 171 assertions**.
  - MathContentDataSmoke: **PASS — 36/36**.
  - `git diff --check`: **PASS**.
- Request 007 đã có regression chính thức trong `7f79367`; retry/first-try engine contract ở `7c9a9ea` và write-failure reconcile ở `400fd0c` đã được AI3 UI consume. Generated segment vẫn chỉ ở WIP AI2 nên chưa tính CLOSED.
- Production solution clean rebuild tại detached `12cb5ee` bằng Visual Studio 2022 Community MSBuild: **PASS — Release/x86, exit 0**.
- Release-required runtime smokes trên cùng clean tree: **8 PASS / 3 FAIL**. PASS: Behavior 15, LearningSession 794, Motion 25, Child UI 1558, Security 19, Audio 14, Performance 13, SQLite 166. FAIL: SetupPreflight + Update (`database_runtime_v1.json schema_version=4, expected=3`) và Content (`bundled_english_verified`).

## Integration waves

### AI3-001 — interactive segment answer
Commit `c63e110`. Segment control, mouse/keyboard, hint/result/accessibility DONE. UI smoke hiện dùng trực tiếp `MathQuestion` interaction contract để không phụ thuộc generator-owned behavior.

### AI3-002 — Math Hub + lesson content
Commit `11d7914`. Consume lesson catalog thật; 7 chương / 17 chủ đề / 67 bài; theory/concept/worked example/prerequisite + safe error state.

### AI3-003 — continue + exact resume
Commit `2d2813c`. Continue lesson từ durable skill evidence; stop/close dùng `Suspend`; exact resume presentation DONE.

### AI3-004 — durable result baseline
Commit `37f0b97` + regression `b6a1ce4`. Attempts/independent/hinted/wrong/skills/garden reward từ summary thật; result route về Math Hub đúng.

### AI3-005 — targeted lesson + all answer surfaces
Commit `1436705` — `Toán UI: hoàn thiện luyện theo bài và toàn bộ dạng nhập đáp án`.

- Hub consume `MathLessonProgressService.GetAllAccess()`.
- Locked lesson vẫn đọc theory được; practice CTA bị khóa và nêu prerequisite thiếu.
- Unlocked lesson có `Luyện 3 câu bài này`; completed lesson có `Luyện lại` + last/best score.
- `MathLessonForm` nhận `lessonId` và dùng `MathSessionCoordinator(..., lessonId)`.
- Result targeted dùng official `LessonScorePercent` / `LessonBestScorePercent`, không tự tính ở UI.
- Typed answer hỗ trợ numeric / word_problem / expression / unit + Enter submit + empty guard + accessibility.
- 201-question sweep và Flow 5 UI E2E PASS.

### AI3-006 — mastery delta + bài tiếp theo
Commit `3905c09` — `Toán UI: hiển thị tiến bộ và bài tiếp theo`.

- Targeted result hiển thị `TargetSkillMasteryAfter` theo %, và `TargetSkillMasteryDelta` dương theo điểm phần trăm.
- Adaptive result dùng `ImprovedSkillCount` thay vì tự suy ra mastery change.
- `NextLessonId` + `NextLessonTitleVi` chỉ được presentation khi engine publish đủ cặp; UI không tự tìm bài khác hoặc tự mở lesson.
- Flow 5 E2E xác nhận support label nhận mastery + immediate next lesson thật từ coordinator.
- Numeric XP vẫn không hiển thị vì chưa có product contract first-class.

### AI3-007 — sweep 67 lesson detail/access
Commit `50fa4c0` — `Toán QA: quét đủ 67 bài học trên hub`.

- Chọn lần lượt toàn bộ 67 lesson qua `SelectLessonInCatalog()` trên Hub thật.
- Mỗi lesson phải render title + mục tiêu + ví dụ + đúng practice count.
- Mỗi practice CTA phải có accessible description.
- `Enabled` của CTA phải khớp first-class `MathLessonAccessSnapshot.IsUnlocked`; không suy luận lock ở test/UI.
- Clean gate: Child UI **1475 assertions PASS**.

### AI3-008 — retry cùng câu + first-try semantics
Commit `a06d1a3` — `Toán UI: thêm retry an toàn cho mọi dạng đáp án`.

- First try dùng `SubmitAnswerWithRetry`; attempt 2 dùng `SubmitRetryAnswer` đúng engine contract.
- First-try sai giữ nguyên question, không tăng completed progress và không hiện nút sang câu mới.
- Typed retry giữ input editable/select-all; choice retry chỉ đánh dấu lựa chọn sai, không reveal đáp án đúng trước lần 2; interaction retry giữ thước editable.
- Resume khi `RetryPending=true` hiển thị thông báo tiếp tục lần thử lại của đúng câu.
- Final retry mới khóa answer surface và tăng completed count đúng một lần.
- Result dùng `IndependentCorrect`, `RetriedQuestions`, `RetriedCorrect` trực tiếp từ summary; retry-correct không bị tính nhầm là tự làm đúng.
- Authored interaction E2E mở đúng chuỗi prerequisite qua UI thật rồi kiểm medium segment sai → retry đúng → thước khóa sau final.
- Clean detached `40dc075`: Child UI **1522 assertions PASS**, persistence **158 assertions PASS**, content **29/29 PASS**.

### AI3-009 — exact retry-resume UI regression
Commit `c5cb9da` — `Toán QA: khóa retry resume đúng câu`.

- Cố ý trả lời sai first attempt của authored choice rồi `Suspend` khi `RetryPending=true`.
- Mở lại cùng lesson bằng form mới phải restore đúng `ContentQuestionId`, attempt 2 và progress label `thử lại`.
- Support text giải thích đang tiếp tục lần thử lại của cùng câu; choices vẫn editable/idle, không reveal correct answer.
- Retry đúng sau resume phải cho `Attempts=1`, `AnswerAttempts=2`, `RetriedQuestions=1`, `RetriedCorrect=1`, `IndependentCorrect=0`.
- Clean detached `c08c7cf`: Child UI **1534 assertions PASS**, persistence **171 assertions PASS**, content **30/30 PASS**.

### AI3-010 — phục hồi UI sau lỗi ghi đáp án
Commit `12cb5ee` — `Toán UI: phục hồi an toàn sau lỗi lưu đáp án`.

- Nếu submit ném lỗi nhưng coordinator vẫn `IsActive`, `HasOpenQuestion` và `NextQuestion()` trả đúng `QuestionId` hiện tại, UI coi đây là recoverable write failure thay vì abort session.
- Typed/choice/interaction answer surface được mở lại, progress không tăng, nút Next vẫn ẩn và child-facing feedback nói rõ dữ liệu trước đó vẫn an toàn.
- Choice recovery reset visual về idle, không reveal đáp án đúng sau transaction fail.
- Inject SQLite trigger thật để làm fail durable mastery write ở cả first attempt và retry attempt 2.
- First-attempt failure → bỏ trigger → submit lại vẫn `IndependentCorrect=1`, không ghost retry.
- Retry-attempt failure → bỏ trigger → submit lại vẫn `RetriedQuestions=1`, `RetriedCorrect=1`, `IndependentCorrect=0`.
- Nếu engine không xác nhận được cùng open question thì UI vẫn dùng fail-closed `Abort` cũ.
- Clean detached `2da39b5`: Child UI **1558 assertions PASS**, persistence **171 assertions PASS**, content **34/34 PASS**; Data/Session clean source-equivalent build **0 warning / 0 error**.

### AI3-011 — khóa write-failure recovery trên typed + interaction
Test-only wave đã qua clean production gate tại `8f10d4d`.

- Typed authored root lesson: inject SQLite `mastery_event` failure → session/counters giữ nguyên, textbox + submit được mở lại, accessibility báo câu chưa lưu; bỏ trigger → submit lại commit đúng một independent attempt.
- Authored interaction lesson: mở prerequisite qua UI thật, vào medium segment → inject SQLite write failure → thước vẫn chỉnh/redraw được, submit được mở lại; bỏ trigger → submit đúng commit medium đúng một lần rồi mới khóa control.
- Kết hợp với choice recovery của AI3-010, cả 3 answer surfaces đều có E2E transaction-failure recovery.
- Clean detached `8f10d4d`: production solution Rebuild Release/x86 **PASS**, Child UI **1593 assertions PASS**, persistence **171 assertions PASS**, content **36/36 PASS**.

## Next integration gates

1. Platform/release đóng runtime-config schema mismatch `4 vs 3`; English/content lane đóng `bundled_english_verified`, rồi AI3 rerun 11 release-required smoke executables.
2. Release lane đóng Request 008: hard-guard 3 Math runtime JSON trong staged payload + portable/installer E2E, rồi rebuild artifact schema V4 từ commit hiện tại.
3. AI2 đóng Request 009 bằng first-class selected 3-question session target; AI3 sau đó tách Hub CTA khỏi pool count và khóa pool >=6 → session/progress/result vẫn 3.
4. Theo dõi AI2 commit generator `draw_segment_given_length`, rồi khóa adaptive interaction regression.
5. Khi có artifact mới, chạy portable/installer upgrade regression và xác nhận learner DB + lesson progress không mất.
