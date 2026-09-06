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
  - `integer`: 109
  - `text`: 89
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
- Math content unittest: **12 / 12 PASS**
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

Hai skill này **không còn thiếu content**: lesson + curated question đã có và validator PASS. Phần còn lại là runtime integration/engine support vì session hiện vẫn chủ yếu consume `verified_templates_v1.json`; AI1 không sửa generator/selector lớn thuộc AI2.

## Validator gates implemented

`tools/math_content_validator/validate_math_content.py` hiện chặn:

- duplicate ID toàn catalog/bank;
- missing lesson/question/reference;
- orphan question;
- bad prerequisite + prerequisite cycle;
- invalid difficulty / practice-set mismatch;
- invalid numeric range;
- missing answer / accepted answer;
- malformed expression / divide by zero / unsupported expression nodes;
- invalid unit metadata;
- invalid MC correct choice / duplicate choices / rationale missing;
- invalid true/false shape;
- mệnh giá tiền Việt Nam bị hard-code khi chưa có source/book mapping;
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
- prerequisite resolve và acyclic;
- difficulty cân bằng;
- authoring deterministic;
- answer kinds/question types đúng contract;
- expression validator fail-closed, kể cả divide-by-zero và payload không phải arithmetic;
- nội dung tiền Việt Nam không hard-code mệnh giá khi chưa có source/book mapping.

Latest result: **12 tests PASS**.

## Commits / waves

- `58b0ae8` — `Toán: audit nội dung và chốt inventory dữ liệu`
- `e288fc4` — `Toán: hoàn thiện 67 bài học và ngân hàng 201 câu`
- `20c2dd0` — `Toán: thêm validator và test toàn vẹn nội dung`
- `3993e20` — `Toán: đăng ký catalog và question bank vào content pack`
- `e4a6217` — `Toán: chuẩn hóa question bank theo contract engine`

## Current blockers outside AI1 content ownership

1. Session runtime chưa consume `question_bank_v1.json`; engine/integration cần map stable content question -> runtime `MathQuestion`.
2. Chapter/topic/lesson UI đang được AI3 tích hợp từ `lesson_catalog_v1.json`.
3. Prerequisite/unlock presentation và exact resume là engine/UI contract, không phải content absence.
4. `verified_templates_v1.json` đang có WIP song song của AI2; AI1 không chèn hai template còn thiếu vào file này trong khi owner engine đang sửa để tránh conflict.

## Lane verdict

**Content/Data curated lane: CLEAN.**

Không còn lesson/question/reference/answer/prerequisite/difficulty/semantic-validator error trong dữ liệu AI1. Phần chưa chạy end-to-end là integration với engine/UI và legacy generator path, đã phân owner rõ cho AI2/AI3.
