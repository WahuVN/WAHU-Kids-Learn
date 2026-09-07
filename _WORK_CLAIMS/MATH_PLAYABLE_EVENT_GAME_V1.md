# MATH — PLAYABLE EVENT / GAME V1 — SHARED CONTRACT

Updated: 2026-09-07
Status: ACTIVE P0
Goal: đưa phần Toán hiện có từ lesson/session UI sang **vòng chơi học tập dùng được ngay** mà không tạo lại grading engine.

## 1. Product slice phải chạy được

Tên hiển thị tạm: **Toán nhanh — Nhiệm vụ cứu hộ**.

Ý nghĩa “nhanh” = một nhiệm vụ ngắn, rõ mục tiêu, ít bước. **Không dùng countdown, không thưởng vì trả lời nhanh, không trừ điểm vì chậm.**

“Cứu hộ” chỉ là phục hồi đồ vật/thế giới: sửa biển số, sửa đường, ghép bảng, làm sáng khu vườn. Không dùng nhân vật khóc, bị nguy hiểm, mất đồ hay gây cảm giác tội lỗi nếu trẻ dừng.

V1 release slice chỉ cần 5 lesson đầu:
1. `m2_ls_num_count_read_write_0_1000`
2. `m2_ls_num_full_hundreds_recognize`
3. `m2_ls_num_predecessor_successor`
4. `m2_ls_place_value_hundreds_tens_ones`
5. `m2_ls_num_expanded_form_hto`

Mỗi event dùng targeted Math session hiện có: **3 câu = 1 basic + 1 medium + 1 application**. Không tạo grading path thứ hai.

## 2. Trải nghiệm V1 bắt buộc

Flow child-facing:

```text
Home / Math Hub
→ chọn “Toán nhanh — Nhiệm vụ cứu hộ”
→ intro 1 màn hình ngắn: chuyện gì cần sửa + 1 primary CTA
→ 3 checkpoint học tập
→ mỗi câu trả lời cập nhật checkpoint/world state
→ nếu khó: cue / visual / worked example / repair theo BehaviorDecision
→ nếu mệt: cho “Nghỉ ở đây”, lưu tiến độ, không mất reward đã earned
→ hoàn thành: world restoration + Garden reward hiện có
→ reflection ngắn + kết thúc tự nhiên
```

Không autoplay event tiếp theo.

## 3. Shared event schema V1 — FROZEN

AI1 tạo production catalog theo schema này. AI2/AI3 được phép code consumer bằng synthetic fixtures trước khi file AI1 land.

File mục tiêu:
`content_packs/math_grade2_v1/game_events_v1.json`

Root:

```json
{
  "schema_version": 1,
  "catalog_id": "math_grade2_game_events_v1",
  "language": "vi",
  "events": []
}
```

Mỗi event:

```json
{
  "id": "m2_evt_number_sign_rescue_01",
  "kind": "quick_rescue",
  "title_vi": "Sửa biển số trong Rừng Toán",
  "intro_vi": "Gió làm ba biển số bị lộn xộn. Con giúp đặt lại từng biển nhé.",
  "completion_vi": "Ba biển số đã về đúng chỗ. Đường trong rừng lại rõ ràng rồi.",
  "target_lesson_id": "m2_ls_num_count_read_write_0_1000",
  "target_skill_id": "NUM_COUNT_READ_WRITE_0_1000",
  "question_count": 3,
  "checkpoint_nouns_vi": ["biển số 1", "biển số 2", "biển số 3"],
  "theme": "forest_path",
  "repair_copy_vi": "Mình xem lại một bước nhỏ rồi sửa tiếp nhé.",
  "break_copy_vi": "Phần đã làm được lưu rồi. Khi nào muốn mình quay lại tiếp nhé.",
  "reward_presentation": "garden_progress"
}
```

Hard rules:
- `schema_version == 1`.
- `kind == quick_rescue` cho V1.
- `question_count == MathSessionCoordinator.TargetedLessonQuestionCount == 3`.
- `target_lesson_id` và `target_skill_id` phải khớp catalog.
- title ≤ 60 ký tự; intro/completion/repair/break ≤ 180 ký tự.
- không có timer, countdown, score-speed, fail-loss, streak-loss, random reward.
- không chứa copy gây guilt/shame/danger: khóc, bỏ rơi, sắp chết, mất quà, hết giờ, nếu con không làm...
- reward presentation chỉ dùng tiến bộ hiện có; V1 không thêm currency.

## 4. Event identity / persistence contract

AI2 sở hữu runtime semantics.

Event **không thay session identity**. Một event instance bind vào:
- `event_id`
- existing `session_id`
- `target_lesson_id`
- selected 3 authored content IDs
- current checkpoint = completed question count

Resume rules:
- app restart phải resume exact active Math session như hiện tại;
- event_id phải được khôi phục deterministic hoặc persisted rõ ràng;
- selected set/open question/retry ordinal không đổi;
- nếu event metadata hỏng nhưng Math session hợp lệ: fail-safe về lesson presentation, **không xóa attempt/mastery/progress**;
- nếu child dừng: dùng suspend/resume contract hiện có, không mark failure, không mất world progress cũ.

V1 ưu tiên tránh migration mới nếu có thể derive event từ target lesson + active session. Nếu cần persistence mới thật sự, AI2 phải thêm migration + rollback/fault/corruption tests.

## 5. Behavior psychology mapping — FROZEN SEMANTICS

Behavior labels là trạng thái vận hành, không phải chẩn đoán tâm lý.

### READY
- event bình thường;
- feedback cụ thể, ngắn;
- companion calm.

### FLOW_LIKELY
- giảm animation chen ngang;
- feedback tối giản;
- không bật reward popup giữa câu;
- giữ nhịp 3 checkpoint.

### BORED_OR_UNDERCHALLENGED
- ưu tiên context/representation transfer nếu engine có candidate phù hợp;
- không tăng số câu;
- không biến thành speed challenge.

### STRAINED
- giảm chữ phụ;
- mở cue nhỏ / visual support;
- giữ cùng learning goal;
- event copy: “Mình làm từng bước nhé.”

### FRUSTRATED_LIKELY
- neutral failure language;
- worked example / prerequisite repair / meaningful success recovery theo engine;
- world không “xấu đi” vì sai;
- không mất checkpoint đã hoàn thành.

### FATIGUED_LIKELY
- ưu tiên safety;
- hiện CTA `Nghỉ ở đây`;
- preserve mastery nếu engine xác định không có knowledge evidence;
- suspend/positive close;
- không cố hoàn thành event để lấy reward.

UI không tự suy ra state khác engine. AI3 chỉ consume `MathAnswerOutcome.Behavior`, `OfferBreak`, `SuggestPositiveEnd` và contract mới AI2 expose.

## 6. Reward contract V1

Dùng lại `GameWorldRewardService` hiện có.

- reward chỉ sau **completed Math session**;
- idempotent theo session như hiện tại;
- world change là predictable;
- không reward mỗi click;
- không coin-loss;
- không loot box;
- không daily chest/streak reset;
- event completion UI có thể diễn giải garden growth/unlock hiện có thành world event.

Nếu session dừng sớm do fatigue/suspend: không tạo fake completion reward. Phần học đã commit vẫn giữ nguyên.

## 7. First-five event set

AI1 phải ship tối thiểu 5 event:

1. Lesson 1 — `Sửa biển số trong Rừng Toán`
   - theme `forest_path`
   - checkpoint: 3 biển số.

2. Lesson 2 — `Khôi phục các trạm trăm`
   - theme `hundred_station`
   - checkpoint: 3 trạm có nhãn số tròn trăm.

3. Lesson 3 — `Nối lại đường số`
   - theme `number_path`
   - checkpoint: 3 đoạn đường cần số liền trước/sau.

4. Lesson 4 — `Sắp đúng kho hàng`
   - theme `place_value_workshop`
   - checkpoint: trăm / chục / đơn vị.

5. Lesson 5 — `Sửa máy ghép số`
   - theme `number_machine`
   - checkpoint: 3 bộ phận dạng khai triển.

Text phải mô tả restoration, không tạo nguy hiểm giả.

## 8. AI1 lane — EVENT CONTENT / PEDAGOGY / PSYCHOLOGY COPY

Ownership mới:
- `content_packs/math_grade2_v1/game_events_v1.json`
- source authoring tương ứng trong `tools/math_content_authoring/**`
- validator + `tests/MathContentDataSmoke/**`
- manifest khi safe
- AI1 status/parallel docs

P0:
1. finish/commit current first-five content WIP trước, không bỏ dở.
2. implement deterministic event catalog V1 đúng schema frozen.
3. 5 first-lesson events, child-safe copy, 3 checkpoint names/event.
4. validator fail-closed schema/ref/readability/psychology dark-pattern terms.
5. regression: target lesson/skill match, question_count=3, unique IDs, all five lessons covered.
6. content event copy không hard-code answer/correct choice.
7. ship SHA/handoff cho AI2/AI3.

FALLBACK khi manifest/shared file dirty: build event source + synthetic JSON + validator/tests, không chờ.

AI1 không sửa Session/App.

## 9. AI2 lane — EVENT RUNTIME / BEHAVIOR / PERSISTENCE

Ownership mới:
- Math event/session DTO/orchestration under `src/Session/**`
- behavior/event semantics under Math-related `src/Learning/**`
- runtime/persistence under `src/Data/**` only if required
- Math engine/session/persistence tests

P0:
1. define public V1 event DTO/state matching schema frozen.
2. event launch selects existing targeted lesson session; exactly 3 questions.
3. expose current event progress/checkpoint + resume state without duplicating grading.
4. map `BehaviorDecision` to an event action DTO usable by UI, e.g. `normal`, `minimize_interruptions`, `small_cue`, `repair`, `offer_break`, `positive_close`.
5. preserve current retry/idempotency/selected-set/open-question contracts.
6. fatigue: safe suspend/close semantics; no reward if incomplete.
7. completion: existing `GameWorldRewardService` exactly once.
8. corruption/fault/concurrency tests for event wrapper; durable attempts/mastery never lost.
9. consumer-safe fallback if event catalog missing/corrupt: ordinary Math lesson/adaptive flow remains usable.

AI2 may use synthetic event fixtures immediately; do not wait for AI1 JSON.

AI2 không sửa authored event text/UI layout.

## 10. AI3 lane — PLAYABLE GAME UI / EVENT E2E / RELEASE

Ownership mới:
- `src/App/**` Math/game presentation
- Child UI tests
- release payload/tests

P0:
1. add visible entry from Home/Math Hub: `Toán nhanh — Nhiệm vụ cứu hộ`.
2. event intro card: title + short story + one primary CTA + easy exit.
3. reuse `MathLessonForm` answer surfaces; do not duplicate answer UI.
4. add event presentation state: 3 checkpoint visuals, theme copy, companion reaction.
5. consume AI2 behavior action/state:
   - FLOW: minimal interruption;
   - STRAINED: simplify support/visual cue;
   - FRUSTRATED: repair presentation;
   - FATIGUED: `Nghỉ ở đây`, safe save message.
6. completion presentation: restoration animation + existing Garden progress/unlock; no autoplay.
7. resume E2E: exact event/session/question/checkpoint after close/reopen.
8. 900×640, keyboard, accessibility names/descriptions, reduced motion/performance fallback.
9. missing/corrupt event file falls back safely to ordinary Math Hub/lesson.
10. release payload includes `game_events_v1.json` once AI1 publishes it.

AI3 can build event controls against an internal test fixture first; do not wait for AI1/AI2. Final thin adapter/wiring happens after public runtime contract lands.

AI3 không sửa mastery/session semantics.

## 11. Parallel execution order — NO WAIT

All three start together:

```text
AI1: content catalog + validator + first-five scenario copy
AI2: runtime DTO/orchestrator + behavior mapping + persistence tests using fixtures
AI3: event UI/components + ChildUI E2E using fixtures
```

When one lane lands:
- others fetch/inspect current HEAD;
- integrate only at their boundary;
- continue own fallback work if boundary still unavailable.

No lane waits idle.

## 12. Definition of “xài được luôn”

P0 is DONE only when current HEAD proves:
- app launches;
- Home/Math Hub shows quick rescue entry;
- at least all 5 first lessons have event metadata;
- event starts exact targeted session of 3 questions;
- typed/choice/interactive surfaces still work through existing MathLessonForm;
- wrong → retry/repair works;
- STRAINED/FRUSTRATED/FATIGUED presentation paths have regression coverage;
- stop/reopen resumes exact event checkpoint/open question;
- completion updates existing Garden reward exactly once;
- incomplete/suspended event does not grant completion reward;
- no timer pressure/dark pattern/streak loss;
- Child UI + MathEngine + MathData + MathSessionPersistence + Behavior + SQLite affected gates PASS;
- release payload contains event JSON and portable/installer smoke PASS when relevant.

## 13. Final integration gate after 3 lanes land

Run in this order:
1. content validator + full MathContentDataSmoke;
2. BehaviorRuntimeSmoke;
3. MathEngineRuntimeSmoke;
4. MathDataEngineRuntimeSmoke;
5. LearningSessionRuntimeSmoke if selector touched;
6. MathSessionPersistenceRuntimeSmoke;
7. ChildUiRuntimeSmoke;
8. SQLiteRuntimeSmoke if schema/data touched;
9. Release/x86 build;
10. Build-Setup + portable/installer E2E if payload changed;
11. one real manual-style automated journey: Home → Rescue event → wrong/retry → hint → correct → suspend/resume → finish → Garden update.

## 14. Explicit non-goals for V1

Do not add before P0 playable slice is green:
- real-time countdown;
- speed leaderboard;
- PvP/social ranking;
- coins/economy shop;
- random chest/gacha;
- daily streak punishment;
- 67-lesson bespoke game scenarios;
- large world map rewrite;
- audio/voice dependency required for completion.

V1 first makes the first 5 lessons feel like a small playable learning game; breadth can update later.
