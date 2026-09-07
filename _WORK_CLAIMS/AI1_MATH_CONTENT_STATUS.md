# AI1 — MATH CONTENT STATUS

Updated: 2026-09-07
Owner: AI1 — Math Content & Data

## Current metrics

- Baseline skills: **67**
- Chapters: **7**
- Topics: **17**
- Machine-readable lessons: **67 / 67 complete**
- Lessons incomplete/missing: **0**
- Static question bank: **402 questions**
- Valid static questions: **402 / 402**
- Difficulty coverage: **134 basic + 134 medium + 134 application**
- Answer kinds used, aligned with AI2 engine contract:
  - `integer`: 214
  - `text`: 182
  - `expression`: 2
  - `unit`: 2
  - `interaction_integer`: 2
- Question types represented:
  - numeric input
  - multiple choice
  - true/false
  - expression input
  - unit input
  - interactive measurement
  - word problem
- Semantic validator errors: **0**
- Math content unittest: **70 / 70 PASS**
- Pack manifest/hash/listing check on current working tree: **PASS** (`version=1.9.0`, 3 listed files)
- Real 402-bank session/persistence integration: **289 assertions PASS** trên snapshot trước final engine stress; AI2 committed status ghi final persistence **374 PASS**. Current unstaged AI2 partial-pack-identity fixture đang được phát triển riêng và không thuộc AI1.
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

- baseline traceability root: `curriculum_id = vn_moet_math_grade2_tt32_2018`, Grade 2 Math, status `VERIFIED_A_BASELINE`, source bắt buộc `MOET_TT32_2018` + `TT32_FULL_ANNEX_MIRROR`, source không trùng; catalog/bank phải bind đúng curriculum này;
- root metadata contract: `catalog_id=math_grade2_lesson_catalog_v1`, `bank_id=math_grade2_static_question_bank_v1`, `language=vi`, catalog `id_policy` cố định; answer-kind/question-type declaration phải đúng tập thực tế và không duplicate;
- baseline hard guards bị đổi/mất: `max_number=1000`, carry/borrow rounds `1`, bảng nhân `[2,5]`, bảng chia `[2,5]`, mental add/sub max `20`, vị trí kim phút baseline `[3,6]`; validator fail nếu bất kỳ giá trị nào lệch; riêng max-number còn được enforce trên **2.870 numeric literals child-facing**, hiện max=1000 / over1000=0; **26 câu integer** thuộc mental/ý nghĩa nhân-chia/bảng 2-5/bài toán nhân-chia đã có semantic range `0..20/50/10` thay vì mặc định `0..1000`, và validator khóa theo skill;
- duplicate ID toàn catalog/bank;
- deterministic ID contract: lesson=`m2_ls_<skill>`, concept/example=`m2_cp|ex_<skill>_<ordinal>`, question=`m2_q_<skill>_<ordinal>` với ordinal contiguous `01..N`; gate tương thích pool mở rộng vì không khóa N=3;
- question tag contract lệch/stale/duplicate: 201/201 câu phải có đúng 5 tag `{skill.lower, domain, difficulty, question_type, answer_kind}`; không cho tag dư hoặc thiếu sau khi đổi surface/difficulty;
- validation schema phải khớp answer surface chính xác: numeric/interaction `{integer_required,numeric_min,numeric_max}`, expression 5 key riêng, unit 3 key, choice/text `{choice_count,single_correct}`; key dư/thiếu đều fail;
- missing lesson/question/reference;
- orphan question;
- curriculum roadmap phải có đúng 1 chapter cho mỗi baseline domain, mọi topic có ít nhất 1 lesson, và `order_in_domain` contiguous `1..N` không duplicate/gap; hiện **7/7 domain, 17/17 topic, 67/67 lesson** đạt gate;
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
- child-facing text không NFC, có control/format character (kể cả zero-width), leading/trailing/repeated space hoặc spacing dấu câu bẩn; phép chia chuẩn `a : b` được miễn đúng mục đích toán học; audit hiện **6426 strings sạch**;
- readability overflow trên core child surfaces: lesson title ≤60, lesson explanation ≤200, objective ≤130, concept name/definition ≤50/120, worked prompt/answer/step ≤130/80/100, question prompt/explanation ≤180/200, choice text ≤80; corpus hiện max lần lượt **38/165/110/31/99/107/44/86/151/173/62**;
- mục tiêu học đầu tiên dùng placeholder `Nhận biết và thực hiện đúng nội dung:` hoặc bị tái dùng quá mức;
- mục tiêu học thứ hai generic/placeholder hoặc bị tái dùng quá mức;
- explanation câu hỏi quá ngắn, không đủ bước giải thích/kiểm tra cho feedback học tập;
- phương trình số **và quan hệ so sánh số** (`<`, `>`, `<=`, `>=`, `≤`, `≥`) trong lesson explanation / concept definition / worked answer+solution / question explanation / correct answer / **mọi hint / phần instructional của mọi choice rationale** bị sai giá trị; distractor sai cố ý ở đầu rationale được strip trước khi kiểm quan hệ; corpus hiện có **73 quan hệ instructional, 0 vi phạm**;
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
- authored add/sub expression static contract chỉ khai báo `+`, `-`, `(`, `)`; Request 010 đã CLOSED tại AI2 `7afbb7b` và clean rebuild runtime smoke xác nhận whitelist được preserve/enforce;
- nội dung tiền Việt Nam không hard-code mệnh giá khi chưa có source/book mapping;
- skill quan hệ thời gian không mở rộng thành phép nhân/chia ngoài yêu cầu cần đạt;
- mọi phép nhân/chia literal child-facing nằm trong bảng 2 hoặc 5, kể cả distractor;
- mọi MCQ có choice khác nhau sau normalize text và không có hai biểu thức choice cùng giá trị số; câu equal-group `5 × 2` đã loại distractor `10 : 5` có thể mô tả cùng cấu trúc nhóm và khóa regression ambiguity; **26 câu nhận dạng/khái niệm** đã thay distractor giveaway khác miền bằng lỗi nhầm gần kiến thức và có regression chặn các mẫu vô lý cũ;
- 402/402 hint cấp 1 và 402/402 hint cấp 2 actionable, **mỗi cấp 402 unique / max repeat 1**; toàn bộ 804 hint slots đạt 0 unseen-answer leak, max length **124/130**.
- **534/534 distractor rationale unique / max repeat 1**; diagnosis coverage gồm 338 structured, 39 component-term và 157 strict explicit wrong→correct contrast; `missing_choice_specific_diagnosis=0`, max 300 ký tự.
- vị trí đáp án đúng cân bằng deterministic: **176 câu 4-choice = 44/44/44/44**, **6 true/false = 3/3**.
- **48 câu integer có `answer_unit`** giữ display-only contract; prompt alias gate và real-bank runtime smoke đều PASS.
- **39 application regression quan trọng** được khóa riêng: 38 regression trước đó + câu đọc/viết số đã đổi từ ghép hàng trực tiếp sang sửa lỗi bỏ quên hàng chục;
- child-facing lesson/question text có **0 internal-engine vocabulary**, **0 Unicode/whitespace/control-character hygiene violation** và **0 phép tính dính operator** (`8+2=10` kiểu cũ đã giảm 9 → 0); metadata kỹ thuật như `application`, `numeric_input`, `deterministic` vẫn được phép;
- **67/67 worked example unique**, mỗi ví dụ có ít nhất 2 bước giải, **0 exact/near overlap** với 402 câu practice và 67/67 solution chốt đáp án tường minh;
- cả hai mục tiêu học đạt **67/67 unique** và gắn concept; objective 1 đã loại **67/67** placeholder `Nhận biết và thực hiện đúng nội dung:` và đổi động từ theo answer surface, objective 2 placeholder chung = 0;
- 67/67 concept definition hiện dài ít nhất **40 ký tự**; 3 definition quá mỏng (`Thêm vào`, `Ngày và giờ`, `Giờ và phút`) đã được nâng thành quan hệ có ý nghĩa, vẫn giữ Grade-2 scope;
- **402/402 `explanation_vi`** đạt semantic/evidence/readability gate và **402/402 unique sau normalize**; validator nay fail-closed `duplicate_question_explanation`; MCQ correct-rationale đồng bộ explanation.
- toàn ngân hàng đạt **0 exact duplicate + 0 cross-lesson near-duplicate ≥ 0.95**; 2 cặp near-duplicate đã được viết lại theo ngữ cảnh khác;
- **24 câu cộng/trừ viết** khớp chính xác số lượt nhớ/mượn theo skill contract;
- **166 phương trình số instructional** đang được kiểm bằng arithmetic parser, hiện **0 sai**; detector có regression cho cả phép cộng chuỗi và phép chia dùng dấu `:`.

Latest runtime result: **49/49 PASS**; pool-6 regression **8/8 PASS**; full `MathContentDataSmoke` **70/70 PASS**; clean rebuild real 402-bank persistence/session smoke **289 assertions PASS**.

## Current priority — FIRST 10 LESSONS FIRST

Theo ưu tiên hiện tại của người dùng, AI1 **không tiếp tục polish đồng đều 67 bài**. Trước mắt khóa chất lượng thật kỹ 10 lesson đầu của Chương 1 (`NUM_COUNT_READ_WRITE_0_1000` → `ESTIMATE_OBJECTS_BY_TENS`); các lesson phía sau vẫn giữ production-valid và sẽ update sau.

- First block: **10 lessons / 60 questions**.
- Generic distractor fallback trong first block: **5 → 0**.
- Hint normalized reuse trong first block: **0 group** ở cả hint cấp 1 và cấp 2.
- Hai application pair máy móc nhất đã được viết lại: `NUM_MIN_MAX_UP_TO_4` similarity **0.75 → 0.293**; `ESTIMATE_OBJECTS_BY_TENS` **0.803 → 0.521**.
- Max application-pair similarity của cả 10 bài đầu hiện **0.597 < 0.70** và có regression fail-closed.
- Full content tests hiện **70/70 PASS**, validator **402/402 valid / 0 errors**.
- Whole-bank fallback generic giảm thêm **36 → 31** như hệ quả phụ; **không dùng thời gian hiện tại để polish 31 case phía sau** trước khi first block được kiểm kỹ hơn.
- Question-bank SHA hiện tại: `C15B08D33F4165137B19AA46D95DE230352BAD3B1F60FEDE976AA50A87076907`.

## First-block polish wave — same-domain distractors

- `NUM_COMPARE_0_1000` 6/6 questions in the first block no longer use cross-domain `+`/`-` giveaway distractors; fourth choices are now plausible comparison misconceptions (`không thể so sánh`, `không đủ dữ kiện`, `chưa thể kết luận`) with structured diagnosis.
- `ESTIMATE_OBJECTS_BY_TENS_03` now uses nearby misconceptions `60 / 80 / 73` around correct rounded value `70`, replacing giveaway `7 / 700 / 20`.
- Regressions lock all **18 comparison distractors** to the comparison domain and lock the 73-rounding misconception set.
- Result: **70/70 content tests PASS**, validator 402/402 valid, first-block generic fallback remains **0**.
- Question-bank SHA: `C15B08D33F4165137B19AA46D95DE230352BAD3B1F60FEDE976AA50A87076907`.

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
- `49329b3` — `feat(toán): dựng shadow pool 402 câu cho 67 bài`
- `68145ea` — `feat(toán): publish ngân hàng 402 câu cho 67 bài`

## P1 quality wave — explanation uniqueness

- Audit phát hiện `_01` và `_04` của `TIME_HOUR_60_MINUTES` dùng exact cùng `explanation_vi`; bank chỉ có 401 explanation unique dù 402 câu.
- Sửa tại authored source `_04`; regenerate production deterministic.
- Production validator thêm fail-closed `duplicate_question_explanation`; content smoke thêm regression 402/402 explanation normalized unique.
- Kết quả: **402/402 explanation unique, validator 0 errors, 58/58 content tests PASS**.
- Question-bank SHA mới: `AE7BAB730CBB98DD21CA062194981D4E208B6CD541EC142039471AF15818043D`; manifest 1.9.0 giữ nguyên version/template/catalog hash.

## P1 quality wave — application variant diversity

- Audit normalize-number phát hiện `PICTOGRAPH_SIMPLE_INFERENCE` application `_03` và `_06` cùng một prompt shape, chỉ đổi số.
- `_06` được viết lại thành error-analysis: học sinh phải nhận ra lỗi chỉ so số biểu tượng mà quên dùng chú giải.
- Validator thêm fail-closed `application_prompt_number_swap_duplicate` cho hai application cùng lesson; content smoke khóa toàn bộ 67 lesson.
- Kết quả: **59/59 content tests PASS**, validator 0 errors, application pair không còn number-swap duplicate.
- Question-bank SHA mới: `14454A2B5CFB9ECE5D388D829C11BCA063E227422CDC9EA3C5F1FFCC21E5555C`.

## P1 quality wave — distinct table 2/5 hint strategies

- Audit normalize-number phát hiện **15 nhóm hint template trùng** ở mỗi cấp; 12 nhóm nằm ở bảng nhân/chia 2 và 5, chỉ khác chữ số.
- Generator nay dùng chiến lược riêng theo skill: bảng nhân hai nhấn cặp hai; bảng nhân năm nhấn nhóm/bước năm; bảng chia hai/năm dùng phép nhân ngược tương ứng.
- Normalized hint duplicate groups giảm **15 → 3** ở cả hint cấp 1 và cấp 2; 3 nhóm còn lại là câu nhận diện thành phần phép tính có cùng cấu trúc khái niệm.
- Validator thêm fail-closed `table_family_hint_strategy_duplicate`; content smoke khóa 24 hint surfaces của 4 bảng.
- Kết quả: **60/60 content tests PASS**, validator 0 errors, không hint nào leak unseen answer.
- Question-bank SHA mới: `A3F6FAFA1293699AD656F5AB25B61E347B85EF1C4597F499C2A9F808375C6CDB`.

## P1 quality wave — distinct table 2/5 lesson objectives

- Audit normalize-number phát hiện đúng **4 cặp objective** của bảng nhân/chia 2 và 5 chỉ khác chữ số.
- Authoring nay dùng objective riêng theo skill: nhân hai nhấn cặp hai; nhân năm nhấn nhóm/bước năm; chia hai/năm nhấn tách nhóm và phép nhân ngược tương ứng.
- Normalized objective duplicate groups toàn 67 lesson giảm **4 → 0**.
- Validator thêm fail-closed `table_family_objective_template_duplicate`; content smoke khóa cả 8 objective surfaces của 4 lesson.
- Kết quả: **61/61 content tests PASS**, validator 0 errors.
- Lesson-catalog SHA mới: `82204B665BE8BA8D2CDB2205098E37AE3CFA5D90D51D0F63DD4DD42805A33F9D`; question bank giữ `A3F6FAFA1293699AD656F5AB25B61E347B85EF1C4597F499C2A9F808375C6CDB`.

## P1 quality wave — structured geometry distractor diagnoses

- Audit cho thấy 27/27 distractor của `_04/_05/_06` ở `POINT_RECOGNIZE`, `LINE_SEGMENT_RECOGNIZE`, `POLYLINE_RECOGNIZE` vẫn rơi vào fallback “Dữ kiện dẫn tới…”, dù 3 câu gốc đã có misconception rule.
- Generator + validator được mở rộng đồng bộ theo wording mới: tên điểm vs số đo/đường, đặc trưng đầu mút đoạn thẳng, đường thẳng/đường cong, chuỗi đoạn nối tiếp và quy tắc 6 điểm → 5 đoạn.
- Regression khóa **27/27** distractor mở rộng phải có `structured_choice_reason` và rationale production phải chứa đúng diagnosis đó.
- Whole-bank fallback classifier giảm **157 → 130**; structured distractors hiện **365**.
- Kết quả: **62/62 content tests PASS**, validator 0 errors.
- Question-bank SHA mới: `C8B1F2892D048844ED9A4F4194C59C014097C51E6AE1BAC690E358EBF1B5F500`.

## P1 quality wave — structured mass/time/measurement distractor diagnoses

- Audit tiếp theo phát hiện 27/27 distractor `_04/_05/_06` của `HEAVIER_LIGHTER`, `CLOCK_MINUTE_HAND_AT_3_OR_6`, `MEASUREMENT_ESTIMATE_BASIC` vẫn dùng fallback generic.
- Generator + validator nay phân tích trực tiếp quan hệ cân thấp/ngang, phép suy luận nặng–nhẹ, vị trí kim phút 3/6 và giờ “vừa qua/giữa”, cùng thang đo hợp lý cho tẩy/bàn/vật dài ba lần thanh chuẩn.
- Regression khóa **27/27** distractor mở rộng phải có structured diagnosis tương ứng trong rationale production.
- Whole-bank fallback classifier giảm **130 → 103**; structured distractors tăng **365 → 392**.
- Kết quả: **63/63 content tests PASS**, validator 0 errors.
- Question-bank SHA mới: `7D503EDB64A06B3B1766868C8DEF51FD2F64634D32F5A8BF08164A4C6C003CAB`.

## P1 quality wave — structured shape-reasoning distractor diagnoses

- Audit `SPHERE_RECOGNIZE`, `THREE_COLLINEAR_POINTS`, `FOLD_CUT_COMPOSE_SHAPES` cho thấy 22/27 distractor mở rộng còn rơi vào fallback do wording alias mới.
- Generator + validator mở rộng đồng bộ cho vật gần khối cầu, đặc trưng cạnh/đáy/đỉnh, ba điểm trên/ngoài một đường thẳng, và ghép/cắt hình phẳng.
- Regression khóa **27/27** distractor `_04/_05/_06` của ba skill phải có structured diagnosis.
- Whole-bank fallback classifier giảm **103 → 81**; structured distractors tăng **392 → 414**.
- Rationale dài nhất sau hardening là **300 ký tự**, đúng trần validator.
- Kết quả: **64/64 content tests PASS**, validator 0 errors.
- Question-bank SHA mới: `F3B6952594AF484EB2E33BEF50B186DAD474DDF0B03FE5C72B2899C36CC76858`.

## P1 quality wave — semantic component-term distractor parser

- Bốn skill `ADD_COMPONENTS_RECOGNIZE`, `SUB_COMPONENTS_RECOGNIZE`, `MULTIPLICATION_COMPONENTS`, `DIVISION_COMPONENTS` trước đây dựa nhiều vào wording-specific rule/fallback.
- Generator + validator nay dùng chung parser phương trình để map số → vai trò (`số hạng/tổng`, `số bị trừ/số trừ/hiệu`, `thừa số/tích`, `số bị chia/số chia/thương`) và chẩn đoán lựa chọn sai theo chính phương trình.
- Composer giữ đồng thời **structured diagnosis + định nghĩa thuật ngữ sai**, không bỏ contract giải thích term.
- Regression khóa **36/36** distractor `_04/_05/_06`; parser còn cover thêm các câu component cũ có cùng semantics.
- Structured distractors tăng **414 → 465**; whole-bank fallback classifier giảm **81 → 60**.
- Kết quả: **65/65 content tests PASS**, validator 0 errors; max rationale vẫn **300 ký tự**.
- Question-bank SHA mới: `83F5A8564EBDDCF02D2D97B24573BE8EF2D2EA0C29C3E077227D820FB6BE9030`.

## P1 quality wave — structured curve/cylinder/time/money distractors

- Audit 4 skill `CURVE_RECOGNIZE`, `CYLINDER_RECOGNIZE`, `TIME_HOUR_60_MINUTES`, `MONEY_VND_NOTE_RECOGNITION` cho thấy 24/31 distractor `_04/_05/_06` còn fallback do wording alias mới.
- Generator + validator mở rộng đồng bộ cho đặc trưng đường cong/khối trụ, quan hệ `1 giờ = 60 phút`, và đọc/so sánh tiền theo con số mệnh giá + đơn vị thay vì màu/trang trí.
- Regression khóa **31/31** distractor mở rộng của 4 skill phải có structured diagnosis.
- Structured distractors tăng **465 → 489**; whole-bank fallback classifier giảm **60 → 36**.
- Kết quả: **66/66 content tests PASS**, validator 0 errors; max rationale vẫn **300 ký tự**.
- Question-bank SHA mới: `0EFA35C4066E5319F54C7B71CA1CCB45A55EADF99E1219346EC3FC306D3D5B3A`.

## Pool-6 runtime publish — COMPLETE

- 201 expansion questions `_04/_05/_06` đã được promote vào production; runtime bank hiện **402 questions**.
- 67 lesson × 6 câu; mỗi lesson đúng **2 basic + 2 medium + 2 application**; IDs `_01..06` contiguous.
- Production semantic validator: **402/402 valid, 0 errors**; pool-6 wrapper báo `draft_fallback_rationales=0`.
- Full content tests hiện **66/66 PASS** sau P1 hardening thêm structured diagnosis cho curve/cylinder/time/money expansions; clean rebuild `MathSessionPersistenceRuntimeSmoke`: **289 assertions PASS** trên real bank.
- Deterministic regenerate hiện giữ SHA catalog `82204B66...3F9D` và bank `0EFA35C4...D5B3A`.
- Manifest `1.9.0`: **PASS**, 3/3 listed files khớp SHA256 và không thiếu/thừa file pack.
- Request 009 content handoff `05cdb2a` đã được tiêu thụ; AI1 không còn breadth/replay content blocker.

## Current blockers outside AI1 content ownership

1. Request 005 functional path đã được commit end-to-end: engine `656a94b` + UI `1436705`. Targeted lesson dùng authored bank đúng 3 câu, all-201 answer-surface sweep PASS, Flow 5 prerequisite unlock PASS; current persistence WIP đạt **171 assertions PASS** và Child UI đạt **1596 assertions PASS**. Release-clean full solution vẫn bị SQLite/toolchain chặn.
2. Request 007 **CLOSED** tại `7f79367`: targeted corrupt regression xác nhận cursor rollback 2→1, phát lại đúng medium, sau đó application, đủ 3 attempts mới complete; current persistence smoke hiện **171 assertions PASS**.
3. Request 006 **CLOSED tại AI2 `3cf7fc6`**: content hiện có 48 integer `answer_unit` display-only; loader/runtime/resume preserve metadata và raw integer grading không bị mở rộng sang unit text.
4. UI smoke full project-reference build vẫn gặp lỗi reference `System.Data.SQLite` khi build `WAHU.Data.csproj`; App + Child UI targeted build với `BuildProjectReferences=false` PASS. Đây là build/dependency WIP ngoài AI1.
5. Retry UI presentation trong current working tree hiện **PASS** sau rebuild App + Child UI với `BuildProjectReferences=false`; Child UI đạt **1596 assertions**. Chưa coi là commit-owned closure cho tới khi lane UI chốt các file WIP của họ.
6. Legacy generator vẫn chỉ phủ 65/67 skill, nhưng lesson-authored path đã cho phép hai skill `FOLD_CUT_COMPOSE_SHAPES` và `MONEY_VND_NOTE_RECOGNITION` có bài luyện thật mà không cần template giả.
7. **Breadth / replay — Request 009 CLOSED cho AI1:** runtime 402 + selected-set engine handoff đã integrate; pool 6/session target 3; AI2 final committed stress ghi persistence 374 PASS.
8. **Expression operator grading — Request 010 CLOSED ở AI2 `7afbb7b`:** per-question expression whitelist đã được engine preserve/enforce; content contract cộng-trừ `+ - ( )` của AI1 đã có runtime consumer.

## Lane verdict

**Content/Data lane: COMPLETE — runtime 402, validator/tests/hash/integration đều GREEN.**

Runtime 402 hiện không còn lesson/question/reference/answer/prerequisite/difficulty/semantic-validator error. Prerequisite graph giữ 9 root và 67/67 reachability. AI1 không còn content/data blocker.
