# 04 — ADAPTIVE LEARNING ENGINE

## 1. Mục tiêu

Learning Engine quyết định:
- trẻ đang biết gì;
- mức chắc chắn đến đâu;
- lỗi thuộc loại nào;
- có cần prerequisite không;
- khi nào ôn lại;
- câu tiếp theo nên là gì;
- khi nào nên giảm khó/nghỉ.

## 2. Micro-skill model

Mỗi question map đến:
- 1 primary skill;
- 0..N prerequisite skills;
- 0..N secondary skills;
- representation type;
- difficulty features.

Ví dụ `47 + 38`:
- primary: ADD_2DIGIT_WITH_CARRY;
- prereq: SINGLE_DIGIT_SUM_GT10;
- prereq: PLACE_VALUE_TENS_ONES;
- representation: symbolic;
- features: carry_once, no_zero, numeric_answer.

## 3. Mastery state

Mỗi child_skill lưu tối thiểu:
- mastery_score 0..1;
- confidence 0..1;
- last_seen_at;
- last_success_at;
- streak_success chỉ dùng nội bộ, không đe dọa trẻ;
- attempts_count;
- independent_success_count;
- hinted_success_count;
- transfer_success_count;
- next_review_at.

## 4. Không coi hinted success = independent success

Ví dụ weight baseline:
- independent correct: + mạnh;
- correct sau hint 1: + vừa;
- correct sau worked example: + nhỏ;
- incorrect: giảm theo confidence hiện tại;
- transfer correct ở representation mới: tăng confidence mạnh.

Trọng số phải config được và được calibration sau này.

## 5. Difficulty Engine

Question difficulty không chỉ là số lớp.

Feature examples:
- số chữ số;
- carry/borrow;
- distractor similarity;
- language complexity;
- representation abstraction;
- answer format;
- time pressure (V1 gần như không dùng);
- novelty.

Selection goal:
- ưu tiên câu có expected success probability khoảng 0.75–0.85 trong learning mode;
- assessment/review có thể khác.

## 6. Review Scheduler

Một skill chưa được “mastered” chỉ vì đúng 5 câu liên tiếp trong cùng 5 phút.

Scheduler dự kiến:

```text
acquire
→ short recall
→ next-day recall
→ multi-day recall
→ 1-week-ish recall
→ longer interval
```

Interval phụ thuộc:
- mastery;
- confidence;
- lần recall độc lập;
- lỗi gần đây;
- thời gian từ lần trước;
- độ khó representation.

Nếu fail recall: interval giảm và chèn repair.

## 7. Error taxonomy

### Math
- FACT_ERROR
- CARRY_MISSING
- BORROW_MISSING
- PLACE_VALUE_CONFUSION
- OPERATOR_CONFUSION
- WORD_PROBLEM_PARSE
- WRONG_OPERATION_SELECTION
- COUNTING_ERROR
- CARELESS_CLICK
- UNKNOWN

### English
- SOUND_CONFUSION
- LETTER_CONFUSION
- SPELLING_SEQUENCE
- VOCAB_RECALL
- LISTENING_DISCRIMINATION
- SENTENCE_PATTERN
- READING_DECODING
- CARELESS_CLICK
- UNKNOWN

## 8. Error classifier

Không kết luận từ một attempt duy nhất nếu chưa đủ bằng chứng.

Ví dụ nghi carry missing khi:
- nhiều câu có carry sai;
- non-carry vẫn đúng;
- pattern đáp án phù hợp với quên carry.

Classifier trả:
- label;
- confidence;
- evidence ids.

Nếu confidence thấp → UNKNOWN, không ép chẩn đoán.

## 9. Repair graph

Mỗi micro-skill có repair path.

Ví dụ:

```text
ADD_2DIGIT_WITH_CARRY
├── PLACE_VALUE_TENS_ONES
├── SINGLE_DIGIT_SUM
├── MAKE_TEN
└── CARRY_CONCEPT
```

Nếu skill chính fail do prerequisite:
- hạ xuống node thiếu;
- repair ngắn;
- quay lại target.

## 10. Representation transfer

Mastery cần nhiều representation:
- symbolic;
- objects;
- number line;
- story problem;
- inverse relationship.

Không spam cùng template chỉ thay số.

## 11. Question selection scoring

Baseline pseudo-score:

```text
score =
  due_review_weight
+ target_skill_weight
+ prerequisite_repair_weight
+ desired_difficulty_fit
+ representation_diversity
+ novelty_small_bonus
- recent_repeat_penalty
- frustration_risk_penalty
```

Không tối ưu “time in app”.

## 12. Behavioral state controller

Adaptive engine không chỉ có một `frustration_score`. V1 dùng state inference có confidence:
- `READY`;
- `FLOW_LIKELY`;
- `BORED_OR_UNDERCHALLENGED`;
- `STRAINED`;
- `FRUSTRATED_LIKELY`;
- `FATIGUED_LIKELY`.

State được suy từ rolling window của correctness, response time tương đối baseline cá nhân, hint, rapid guessing, skip/exit, representation, mastery và elapsed time. Không dùng camera/biometric.

Knowledge error và state error phải tách riêng; fatigue suspected không được tự động làm giảm mastery.

Policy chi tiết: `core/learning/behavior/behavior_policy_v1.json`.

## 13. Frustration score

Các feature:
- recent incorrect count;
- consecutive same-skill failures;
- response time deviation;
- rapid guessing;
- max-hint use;
- skip/exit behavior.

Output 0..1.

Threshold actions:
- low: normal;
- medium: easier/scaffold;
- high: repair or break;
- very high: kết thúc tích cực, không ép.

## 13. Cold start

Không làm placement test dài.

Dùng “soft diagnostic” trong vài buổi đầu:
- bắt đầu từ kỹ năng grade-level tương đối dễ;
- branch theo phản ứng;
- không gọi là kiểm tra đầu vào.

## 14. Session planner

Một session có quota mềm:
- 20–30% review;
- 40–60% current target;
- phần còn lại repair/transfer/new.

Tỷ lệ thay đổi theo child state.

## 15. Explainability cho Parent Mode

Mọi recommendation quan trọng phải giải thích được.

Ví dụ:
“Đang ôn phép trừ có mượn vì 2 lần recall cách nhau 3 ngày đều cần gợi ý ở bước mượn chục.”

Không hiển thị thuật toán/phần trăm phức tạp cho trẻ.