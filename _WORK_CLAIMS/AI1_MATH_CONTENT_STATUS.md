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
- Math content unittest: **36 / 36 PASS**
- Pack manifest/hash/listing check on current working tree: **PASS** (`version=1.9.0`, 3 listed files)
- Persistence runtime smoke: **171 assertions PASS**
- Child UI targeted build (`BuildProjectReferences=false`) + runtime smoke: **1593 assertions PASS**

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

Hai skill này **không còn thiếu content**: lesson + curated question đã có và validator PASS. Engine lesson-mode dùng authored `PracticeSets` trực tiếp đã được commit tại `656a94b`; UI targeted lesson + toàn bộ authored answer surfaces đã được commit tại `1436705`; adaptive mission vẫn giữ generator path. Targeted persistence smoke PASS **99 assertions**, Child UI PASS **1475 assertions**. Phần còn lại là corrupt-cache lesson-mode, display-unit contract và full production SQLite build gate; AI1 không sửa coordinator/selector lớn thuộc AI2.

## Validator gates implemented

`tools/math_content_validator/validate_math_content.py` hiện chặn:

- duplicate ID toàn catalog/bank;
- missing lesson/question/reference;
- orphan question;
- bad prerequisite + prerequisite cycle + prerequisite trỏ về bài ở phía sau lộ trình; direct prerequisite dư thừa do đã được một prerequisite khác bao hàm cũng bị chặn;
- invalid difficulty / practice-set mismatch;
- invalid numeric range;
- missing answer / accepted answer;
- accepted answer bị trùng hoặc mở rộng sang giá trị sai; integer/interaction phải đúng cùng giá trị số, text phải đúng canonical choice, unit phải đúng cả số + đơn vị, expression phải tương đương expected numeric;
- malformed expression / divide by zero / unsupported expression nodes;
- invalid unit metadata;
- `answer_unit` display-only metadata sai kind/type hoặc rỗng;
- invalid MC correct choice / duplicate choices / rationale missing;
- distractor rationale placeholder/generic, dùng 3 mẫu shallow cũ hoặc không chứa lý do riêng của chính câu;
- hint cấp 2 placeholder/generic hoặc bị tái dùng quá mức;
- hint cấp 1/2 tiết lộ canonical answer chưa xuất hiện trong đề; true/false được loại khỏi detector để “Đúng/Sai” vẫn dùng được như ngôn ngữ hướng dẫn; mỗi hint child-facing tối đa 130 ký tự;
- vocabulary kỹ thuật nội bộ lọt vào field child-facing (`baseline`, `runtime`, `template`, `validator`, ...);
- mục tiêu học đầu tiên dùng placeholder `Nhận biết và thực hiện đúng nội dung:` hoặc bị tái dùng quá mức;
- mục tiêu học thứ hai generic/placeholder hoặc bị tái dùng quá mức;
- explanation câu hỏi quá ngắn, không đủ bước giải thích/kiểm tra cho feedback học tập;
- phương trình số trong lesson explanation / concept definition / worked answer+solution / question explanation bị sai giá trị;
- prompt trùng/gần trùng giữa hai lesson khác nhau, tránh lãng phí ngân hàng câu hỏi;
- application có cùng khung câu với basic/medium chỉ bằng cách đổi số (`shape similarity >= 0.75`); gate so toàn bộ application bucket với toàn bộ lower-difficulty bucket nên tương thích Request 009 nhiều câu/bucket;
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
- worked example trùng hoặc gần trùng câu practice của chính lesson;
- worked example có đáp án nhưng solution không chốt lại đáp án tường minh;
- question không được practice set tham chiếu hoặc bị tham chiếu nhiều lần.

## Test gates

`tests/MathContentDataSmoke/test_math_content.py`:

- mọi baseline skill có đúng một lesson;
- mọi lesson có instructional content;
- mọi lesson có basic/medium/application;
- mọi question có stable source + valid answer;
- mọi question được reference đúng một lần;
- prerequisite resolve, acyclic, luôn trỏ về bài đã xuất hiện trước và không có direct edge bắc cầu dư thừa; graph hiện 9 root, 76 direct edges, 67/67 lesson reachable;
- difficulty cân bằng;
- application không được là bản đổi số của basic/medium cùng lesson; regression synthetic number-swap = 1.0 và toàn bank hiện dưới ngưỡng 0.75;
- authoring deterministic;
- answer kinds/question types đúng contract;
- accepted-answer contract fail-closed cho integer/interaction/text/unit/expression, không cho phép extra accepted value sai;
- expression validator fail-closed, kể cả divide-by-zero và payload không phải arithmetic;
- nội dung tiền Việt Nam không hard-code mệnh giá khi chưa có source/book mapping;
- skill quan hệ thời gian không mở rộng thành phép nhân/chia ngoài yêu cầu cần đạt;
- mọi phép nhân/chia literal child-facing nằm trong bảng 2 hoặc 5, kể cả distractor;
- mọi MCQ có choice khác nhau sau normalize text và không có hai biểu thức choice cùng giá trị số; câu equal-group `5 × 2` đã loại distractor `10 : 5` có thể mô tả cùng cấu trúc nhóm và khóa regression ambiguity; **26 câu nhận dạng/khái niệm** đã thay distractor giveaway khác miền bằng lỗi nhầm gần kiến thức và có regression chặn các mẫu vô lý cũ;
- 201/201 hint cấp 1 và 201/201 hint cấp 2 hiện actionable, **mỗi cấp 201 unique / max repeat 1**; toàn bộ 402 hint slots đạt **0 unseen-answer leak** và **0 hint >130 ký tự**; readability hiện hint1 p90/max = **103/115**, hint2 = **108/119** (trước các wave max 168/167); **176 hint1** dùng khung `Nhớ kiến thức:` đã giảm **176 → 0** và chuyển thành bước quan sát theo question type/difficulty/concept; **28 application numeric hint2** dùng khung `Viết một phép tính hoặc quan hệ ngắn...` đã giảm **28 → 0**;
- 267/267 distractor có **rationale unique / max repeat 1**, mỗi rationale sai chứa lời giải riêng của chính câu; 3 họ mẫu shallow cũ đã giảm **267 → 0**; **24 distractor thuật ngữ phép tính** có định nghĩa choice-specific; **24 distractor cấu trúc số** (`dạng khai triển`, `so sánh`, `sắp xếp`) được validator tự tái tính lỗi; **26 distractor** ở `số tròn trăm`, `ý nghĩa phép nhân`, `ý nghĩa phép tính`, `chọn phép tính một bước` có reason từ chữ số/số nhóm/quan hệ phép toán; **27 distractor sự kiện** (`có thể/chắc chắn/không thể`) có reason theo định nghĩa sample-space; **39 distractor** ở `đơn vị`, `thời gian`, `lịch`, `đồng hồ`, `ước lượng` có reason theo loại đơn vị/quan hệ 24 giờ-60 phút/vị trí kim/thang đo; **24 distractor** ở `đọc số`, `ước lượng theo chục`, `nặng/nhẹ`, `tiền`, `pictograph` có reason từ hàng số/thang đo/cân/mệnh giá/số biểu tượng; thêm **87 distractor hình học** có reason theo số đầu mút/độ thẳng-cong/điểm nối/thẳng hàng/số cạnh-độ kín/đáy-mặt khối/điều kiện ghép-cắt, làm số câu MC/TF dùng chung thân rationale giảm **88 → 4** mà không dùng heuristic suy đoán;
- vị trí đáp án đúng được cân bằng deterministic: 88 câu 4-choice = 22/22/22/22 cho A/B/C/D; 3 true/false = 2/1;
- 23 câu integer có `answer_unit` giữ đúng contract display-only, không đổi sang unit-input;
- **39 application regression quan trọng** được khóa riêng: 38 regression trước đó + câu đọc/viết số đã đổi từ ghép hàng trực tiếp sang sửa lỗi bỏ quên hàng chục;
- child-facing lesson/question text có **0 internal-engine vocabulary**; metadata kỹ thuật như `application`, `numeric_input`, `deterministic` vẫn được phép;
- **67/67 worked example unique**, mỗi ví dụ có ít nhất 2 bước giải, **0 exact/near overlap** với 201 câu practice và 67/67 solution chốt đáp án tường minh;
- cả hai mục tiêu học đạt **67/67 unique** và gắn concept; objective 1 đã loại **67/67** placeholder `Nhận biết và thực hiện đúng nội dung:` và đổi động từ theo answer surface, objective 2 placeholder chung = 0;
- 67/67 concept definition hiện dài ít nhất **40 ký tự**; 3 definition quá mỏng (`Thêm vào`, `Ngày và giờ`, `Giờ và phút`) đã được nâng thành quan hệ có ý nghĩa, vẫn giữ Grade-2 scope;
- 201/201 `explanation_vi` unique và dài ít nhất 32 ký tự; **57 lời giải quá ngắn đã được nâng lên 0**, MCQ giữ correct-rationale đồng bộ với explanation; **201/201 lời giải có evidence của đáp án**, 44 câu thiếu kết luận tường minh đã được nâng về 0; **33 numeric feedback tail máy móc** `Kết quả này theo đúng quy tắc...` đã giảm **33 → 0** và được thay bằng check-step theo nhóm concept;
- toàn ngân hàng đạt **0 exact duplicate + 0 cross-lesson near-duplicate ≥ 0.95**; 2 cặp near-duplicate đã được viết lại theo ngữ cảnh khác;
- 12 câu cộng/trừ viết khớp chính xác số lượt nhớ/mượn theo skill contract;
- **166 phương trình số instructional** đang được kiểm bằng arithmetic parser, hiện **0 sai**; detector có regression cho cả phép cộng chuỗi và phép chia dùng dấu `:`.

Latest result: **36 tests PASS**.

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
- `9472d63` — `Toán: cân bằng vị trí đáp án trắc nghiệm`
- `0bfabc5` — `Toán: nâng chất lượng gợi ý cho 201 câu`
- `719c80b` — `Toán: nâng rationale cho 267 đáp án nhiễu`
- `ee7079b` — `Toán: nâng progression cho câu vận dụng`
- `5a31c41` — `Toán: làm sạch ngôn ngữ kỹ thuật khỏi nội dung trẻ em`
- `9682572` — `Toán: tách ví dụ mẫu khỏi câu luyện`
- `19538b5` — `Toán: chốt đáp án rõ trong ví dụ mẫu`
- `45aa6e8` — `Toán: đặc thù hóa mục tiêu cho 67 bài học`
- `5e05fcc` — `Toán: nâng chiều sâu lời giải cho ngân hàng câu hỏi`
- `d7aea5d` — `Toán: loại câu gần trùng giữa các bài học`
- `896f2f0` — `Toán: nâng độ khó vận dụng theo hướng chuyển giao`
- `7d56516` — `Toán: chốt đáp án tường minh trong mọi lời giải`
- `a77bfdf` — `Toán: khóa accepted answer fail-closed`
- `0b71681` — `Toán: chặn gợi ý tiết lộ đáp án`
- `40dc075` — `Toán: gọn gợi ý và tinh giản prerequisite`
- `c08c7cf` — `Toán: khóa tính đúng của phép tính trong lời giải`
- `a0fa436` — `Toán: đặc thù hóa mục tiêu đầu tiên của bài học`
- `732a2d2` — `Toán: đặc thù hóa rationale cho đáp án nhiễu`
- `602bec7` — `Toán: nâng chất lượng đáp án nhiễu nhận dạng`
- `def7313` — `Toán: nâng thêm câu vận dụng theo hướng chuyển giao`
- `c7c2905` — `Toán: tăng chiều sâu câu vận dụng còn yếu`

## Current blockers outside AI1 content ownership

1. Request 005 functional path đã được commit end-to-end: engine `656a94b` + UI `1436705`. Targeted lesson dùng authored bank đúng 3 câu, all-201 answer-surface sweep PASS, Flow 5 prerequisite unlock PASS; current persistence WIP đạt **171 assertions PASS** và Child UI đạt **1593 assertions PASS**. Release-clean full solution vẫn bị SQLite/toolchain chặn.
2. Request 007 **CLOSED** tại `7f79367`: targeted corrupt regression xác nhận cursor rollback 2→1, phát lại đúng medium, sau đó application, đủ 3 attempts mới complete; current persistence smoke hiện **171 assertions PASS**.
3. Request 006 vẫn mở: 23 câu integer có `answer_unit` được content giữ display-only, nhưng `MathAuthoredQuestionSource`/`MathQuestion` chưa preserve field để feedback hiện `8 cm`, `5 kg`, `60 phút` mà vẫn chấm raw integer.
4. UI smoke full project-reference build vẫn gặp lỗi reference `System.Data.SQLite` khi build `WAHU.Data.csproj`; App + Child UI targeted build với `BuildProjectReferences=false` PASS. Đây là build/dependency WIP ngoài AI1.
5. Retry UI presentation trong current working tree hiện **PASS** sau rebuild App + Child UI với `BuildProjectReferences=false`; Child UI đạt **1593 assertions**. Chưa coi là commit-owned closure cho tới khi lane UI chốt các file WIP của họ.
6. Legacy generator vẫn chỉ phủ 65/67 skill, nhưng lesson-authored path đã cho phép hai skill `FOLD_CUT_COMPOSE_SHAPES` và `MONEY_VND_NOTE_RECOGNITION` có bài luyện thật mà không cần template giả.
7. **Breadth / replay blocker — Request 009 OPEN:** cả 67 lesson hiện có đúng `(1 basic, 1 medium, 1 application)` = 3 authored questions và targeted engine luôn nạp toàn bộ IDs theo đúng thứ tự, nên học lại gặp lại nguyên 3 câu. Request 009 đề xuất tách pool size khỏi session target: AI1 mở pool `>=6` câu/bài, AI2 chọn/persist đúng 3 câu cân bằng mỗi phiên, AI3 giữ CTA/progress 3 câu nhưng hiển thị pool count riêng. AI1 chưa mở rộng bank trước khi runtime contract/regression này xanh.

## Lane verdict

**Content/Data correctness lane: CLEAN; authored-bank breadth expansion: OPEN.**

Không còn lesson/question/reference/answer/prerequisite/difficulty/semantic-validator error trong dữ liệu AI1. Prerequisite graph đã bỏ 4 direct edge bắc cầu nhưng giữ nguyên 9 root và 67/67 reachability. Tuy nhiên 3 câu cố định/lesson vẫn chưa đạt mục tiêu breadth “không còn demo”; mở rộng phần này cần đổi shared targeted-session contract cùng AI2/AI3.
