# AI3 — Rescue Game handoff

Ngày: 2026-09-09
Branch: `ai3-rescue-content-0909`
Base: `origin/main` @ `8336cf3`

## Phạm vi đã hoàn thành

AI3 chỉ thay đổi tầng **nội dung Toán lớp 2 + adaptive + hint/repair + anti-repeat**. Không sửa map, UI shell, gameplay state machine, reward, persistence hay SQLite.

### 1. Content pack cứu hộ riêng

- `content_packs/math_quick_rescue_v1/manifest.json`
- `content_packs/math_quick_rescue_v1/learning_content_v1.json`

Pack V1 khóa đúng 3 checkpoint:

1. `basic` — tạo cảm giác làm được ngay từ câu đầu.
2. `medium` — tăng nhẹ yêu cầu đọc/nhận diện số.
3. `application` — áp dụng để phát hiện/sửa lỗi đặt sai hàng.

Mỗi checkpoint có 2 câu authored có sẵn, gồm một biến thể `support` và một biến thể `transfer`, tổng cộng dùng 6 câu khác nhau của lesson `m2_ls_num_count_read_write_0_1000`.

Mỗi checkpoint có:

- hint level 1;
- hint level 2;
- repair copy;
- tối thiểu 2 common-error pattern + cue/repair riêng;
- wording thân thiện, không đếm ngược, không ép tốc độ, không shame, không reward pressure, không lộ đáp án.

Anti-repeat: cửa sổ 3 câu gần nhất, ưu tiên unseen; khi không còn unseen thì fallback deterministic về câu ít gần đây hơn.

### 2. Adaptive content selector

File: `src/Learning/MathQuickRescueContent.cs`

API tích hợp:

```csharp
var pack = new MathQuickRescueContentSource().Load(contentPath);
var decision = new MathQuickRescueAdaptiveSelector().Select(
    pack,
    checkpointNumber,
    behaviorDecision,
    recentContentQuestionIds,
    seed);
```

`decision` cung cấp:

- `QuestionId`
- `Difficulty`
- `Variant`
- `RecommendedHintLevel`
- `UseRepair`
- `OfferBreak`
- `MinimalFeedback`
- `SupportVi`
- `Reason`

Mapping BehaviorController được lấy từ data pack:

- `READY`: câu bình thường, hint 0.
- `FLOW_LIKELY`: giữ nhịp, feedback tối giản.
- `BORED_OR_UNDERCHALLENGED`: ưu tiên `transfer`, không ép hint.
- `STRAINED`: ưu tiên `support`, hint level 1.
- `FRUSTRATED_LIKELY`: `support` + hint level 2 + repair.
- `FATIGUED_LIKELY`: **không chọn câu mới**, trả `OfferBreak=true` và break copy an toàn.

AI1/runtime chỉ nên tiêu thụ quyết định này; không cần nhân đôi rule adaptive ở gameplay. AI2/UI chỉ render các field child-facing nếu cần.

### 3. Test mới

- `tests/MathContentDataSmoke/test_quick_rescue_learning.py`
- `tests/QuickRescueLearningRuntimeSmoke/`

Kết quả đã chạy trên worktree sạch:

- Quick Rescue content data: `10/10 PASS`.
- Toàn `MathContentDataSmoke`: `100/100 PASS`.
- Quick Rescue runtime: `25/25 assertions PASS`.
- Existing `BehaviorRuntimeSmoke`: `15/15 assertions PASS`.
- Build `WAHU.QuickRescueLearningRuntimeSmoke.csproj` Release/x86: `0 warning, 0 error`.
- `git diff --check`: PASS.

## Full-solution build note

`dotnet build WAHUKidsLearn.sln -c Release -p:Platform=x86` trong worktree sạch dừng ở module `WAHU.Data` với 49 lỗi thiếu `System.Data.SQLite`/`SQLiteConnection`. `WAHU.Learning`, `WAHU.Content` và các test thuộc scope AI3 đều build thành công trước điểm này. Đây là dependency/package của Data/persistence và AI3 không sửa sang scope đó.

## Ranh giới tích hợp cho AI5

Không cherry-pick bất kỳ thay đổi generated nào từ build. Commit AI3 chỉ chứa content/adaptive/tests/handoff. Worktree chính `D:\APP HOC TAP` không bị AI3 ghi đè; toàn bộ công việc nằm ở `D:\APP HOC TAP_AI3_RESCUE` trên branch riêng.

## Wave 2 — hardening sau audit sâu

AI3 tiếp tục khóa thêm các khoảng trống integration mà wave đầu chưa bắt buộc:

1. `ValidateAgainstAuthoredQuestions(...)` fail-closed nếu pack cứu hộ trỏ ID không tồn tại, sai lesson, sai skill hoặc sai difficulty so với authored question bank.
2. `variant` chỉ còn chấp nhận `support|transfer`; mỗi checkpoint bắt buộc có cả hai. `preferred_variant` chỉ chấp nhận `any|support|transfer`.
3. Semantic guard khóa cấu hình an toàn cho từng `BehaviorState`: READY zero-hint, FLOW minimal feedback, BORED transfer, STRAINED support+hint, FRUSTRATED support+repair, FATIGUED break/no-next-question.
4. Anti-repeat được nâng thành hard child-UX guard: khi tất cả option vừa xuất hiện, selector lấy câu **ít gần đây nhất trước**, adaptive variant chỉ phá tie. Vì vậy đổi BehaviorState không thể vô tình ép lặp đúng prompt vừa thấy.
5. Thêm `ResolveErrorSupport(...)` để consumer lấy common-error cue/repair trực tiếp. Error ID match không phân biệt hoa thường; lỗi lạ fallback về hint/repair checkpoint; FATIGUED luôn override về break copy.
6. Runtime adversarial tests khóa malformed variant, drift rule, missing authored question, mismatch lesson/difficulty, anti-repeat-vs-adaptive và common-error/fatigue path.

Gate wave 2:

- Quick Rescue runtime smoke: `44/44 assertions PASS`.
- Quick Rescue runtime Release/x86 build: `0 warning, 0 error`.
- Full `MathContentDataSmoke`: `100/100 PASS`.
- Existing `BehaviorRuntimeSmoke`: `15/15 assertions PASS`.
- `git diff --check`: PASS.
