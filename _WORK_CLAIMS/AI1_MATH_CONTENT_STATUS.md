# AI1 — MATH CONTENT STATUS

Updated: 2026-09-07
Owner: AI1 — Math Content & Data

## Current metrics

- Baseline skills: **67**
- Chapters: **7**
- Topics: **17**
- Machine-readable lessons: **67 / 67 complete**
- Lessons incomplete/missing: **0**
- Static question bank: **201 questions**
- Valid static questions: **201 / 201**
- Difficulty coverage: **67 basic + 67 medium + 67 application**
- Answer kinds used, aligned with AI2 engine contract:
  - `integer`: 107
  - `text`: 91
  - `expression`: 1
  - `unit`: 1
  - `interaction_integer`: 1
- Question types represented:
  - numeric input
  - multiple choice
  - true/false
  - expression input
  - unit input
  - interactive measurement
  - word problem
- Semantic validator errors: **0**
- Math content unittest: **18 / 18 PASS**
- Pack manifest/hash/listing check on current working tree: **PASS** (`version=1.9.0`, 3 listed files)

## Curriculum/content completeness

Mỗi baseline skill hiện có đúng một lesson với:

- mục tiêu học;
- giải thích ngắn;
- concept + definition;
- worked example + solution steps;
- practice basic / medium / application;
- hints;
- correct answer + accepted answer;
- explanation;
- difficulty metadata;
- tags;
- prerequisite graph;
- stable deterministic lesson/concept/example/question IDs.

Static bank phủ **67/67 skill**, kể cả:

- `FOLD_CUT_COMPOSE_SHAPES`
- `MONEY_VND_NOTE_RECOGNITION`

## Legacy generator-template coverage

- Generator templates hiện tại: **57**
- Generator-template skill coverage: **65 / 67**
- Hai skill chưa có generator runtime template:
  - `FOLD_CUT_COMPOSE_SHAPES`
  - `MONEY_VND_NOTE_RECOGNITION`

Hai skill này **không còn thiếu content**: lesson + curated question đã có và validator PASS. Commit AI2 `a5119d4` đã có loader/runtime mapping cho static bank, nhưng session học thật vẫn đang sinh câu qua `verified_templates_v1.json`; AI1 không sửa generator/selector/session routing lớn thuộc AI2.

## Validator gates implemented

`tools/math_content_validator/validate_math_content.py` hiện chặn:

- duplicate ID toàn catalog/bank;
- missing lesson/question/reference;
- orphan question;
- bad prerequisite + prerequisite cycle + prerequisite trỏ về bài ở phía sau lộ trình;
- invalid difficulty / practice-set mismatch;
- invalid numeric range;
- missing answer / accepted answer;
- malformed expression / divide by zero / unsupported expression nodes;
- invalid unit metadata;
- `answer_unit` display-only metadata sai kind/type hoặc rỗng;
- invalid MC correct choice / duplicate choices / rationale missing;
- MCQ trùng nghĩa sau normalize Unicode/case/whitespace hoặc hai biểu thức choice cho cùng giá trị số;
- vị trí đáp án đúng bị lệch pattern; bank phải phân bố cân bằng theo số lượng choices;
- invalid true/false shape;
- mệnh giá tiền Việt Nam bị hard-code khi chưa có source/book mapping;
- phép nhân/chia mở rộng không thuộc skill quan hệ thời gian `1 ngày = 24 giờ`, `1 giờ = 60 phút`;
- phép nhân/chia literal ngoài bảng 2 và 5, kể cả trong distractor/rationale/worked example;
- số lượt nhớ/mượn không khớp contract `NO_CARRY/NO_BORROW` hoặc `_ONE_*_MAX`;
- answer kind ngoài Grade-2 bank;
- question type không hợp contract;
- exact/near duplicate prompt trong cùng lesson;
- question không được practice set tham chiếu hoặc bị tham chiếu nhiều lần.

## Test gates

`tests/MathContentDataSmoke/test_math_content.py`:

- mọi baseline skill có đúng một lesson;
- mọi lesson có instructional content;
- mọi lesson có basic/medium/application;
- mọi question có stable source + valid answer;
- mọi question được reference đúng một lần;
- prerequisite resolve, acyclic và luôn trỏ về bài đã xuất hiện trước;
- difficulty cân bằng;
- authoring deterministic;
- answer kinds/question types đúng contract;
- expression validator fail-closed, kể cả divide-by-zero và payload không phải arithmetic;
- nội dung tiền Việt Nam không hard-code mệnh giá khi chưa có source/book mapping;
- skill quan hệ thời gian không mở rộng thành phép nhân/chia ngoài yêu cầu cần đạt;
- mọi phép nhân/chia literal child-facing nằm trong bảng 2 hoặc 5, kể cả distractor;
- mọi MCQ có choice khác nhau sau normalize text và không có hai biểu thức choice cùng giá trị số;
- vị trí đáp án đúng được cân bằng deterministic: 88 câu 4-choice = 22/22/22/22 cho A/B/C/D; 3 true/false = 2/1;
- 23 câu integer có `answer_unit` giữ đúng contract display-only, không đổi sang unit-input;
- 12 câu cộng/trừ viết khớp chính xác số lượt nhớ/mượn theo skill contract.

Latest result: **18 tests PASS**.

## Commits / waves

- `58b0ae8` — `Toán: audit nội dung và chốt inventory dữ liệu`
- `e288fc4` — `Toán: hoàn thiện 67 bài học và ngân hàng 201 câu`
- `20c2dd0` — `Toán: thêm validator và test toàn vẹn nội dung`
- `3993e20` — `Toán: đăng ký catalog và question bank vào content pack`
- `e4a6217` — `Toán: chuẩn hóa question bank theo contract engine`
- `ad5db15` — `Toán: chốt status content và test biểu thức fail-closed`
- `c7e0b0e` — `Toán: bỏ mệnh giá tiền chưa map nguồn khỏi nội dung`
- `2ac3027` — `Toán: siết hard guard thời gian và thứ tự prerequisite`
- `234eafc` — `Toán: khóa phép nhân chia và nhớ mượn theo baseline`
- `dcedca2` — `Toán: loại ambiguity trong lựa chọn trắc nghiệm`
- `c20ca91` — `Toán: khóa contract authored session và đơn vị hiển thị`

## Current blockers outside AI1 content ownership

1. `a5119d4` đã load/map đủ 201 authored questions và smoke x86 hiện PASS **64 assertions**, nhưng `MathSessionCoordinator.NextQuestion()` vẫn chỉ chọn `_templates` rồi gọi `MathQuestionGenerator`; cần đóng Request 005 để static bank đi vào session học thật.
2. 23 câu integer có `answer_unit` đang được content giữ làm display-only metadata, nhưng `MathAuthoredQuestionSource` chưa preserve field này; cần Request 006 để feedback có thể hiện `8 cm`, `5 kg`, `60 phút` mà vẫn chấm raw integer.
3. Exact suspend/resume đã có engine + regression và AI3 đã nối UI tiếp tục bài; blocker cũ này đã đóng. Lesson catalog UI cũng đã được AI3 tích hợp, nên phần còn thiếu là handoff từ lesson đã chọn sang authored exercise/session policy.
4. Legacy generator vẫn chỉ phủ 65/67 skill. Đây không còn là content absence; khi Request 005 hoàn tất, hai skill `FOLD_CUT_COMPOSE_SHAPES` và `MONEY_VND_NOTE_RECOGNITION` đã có static authored exercises để không cần template giả.

## Lane verdict

**Content/Data curated lane: CLEAN.**

Không còn lesson/question/reference/answer/prerequisite/difficulty/semantic-validator error trong dữ liệu AI1. Phần chưa chạy end-to-end là integration với engine/UI và legacy generator path, đã phân owner rõ cho AI2/AI3.
