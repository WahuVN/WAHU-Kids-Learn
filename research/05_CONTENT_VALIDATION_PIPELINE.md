# 05 — CONTENT VALIDATION PIPELINE

## Pipeline bắt buộc

```text
SOURCE
  ↓
CURRICULUM MAPPING
  ↓
QUESTION GENERATION
  ↓
DETERMINISTIC ANSWER CHECK
  ↓
RANGE / UNIT / LANGUAGE CHECK
  ↓
DISTRACTOR CHECK
  ↓
ASSET SEMANTIC CHECK
  ↓
AGE / LOAD CHECK
  ↓
DUPLICATE & TEMPLATE CHECK
  ↓
HUMAN REVIEW (khi semantic không chứng minh bằng máy)
  ↓
VERIFIED PACK
```

## Math validators

- integer range đúng theo skill;
- carry/borrow count đúng metadata;
- answer recompute độc lập;
- choices unique;
- không có 2 đáp án đúng;
- unit conversion exact;
- clock hand positions sinh từ time model;
- pictograph count khớp data;
- polyline length = tổng segment;
- multiplication/division Grade 2 baseline chỉ table 2/5 nếu gắn chuẩn BGDĐT;
- word problem numbers không vượt skill range.

## English validators

Máy có thể check:
- spelling target;
- number 1–20;
- day-of-week order;
- duplicate choices;
- audio/text ID mapping;
- asset/concept ID mapping;
- expected response whitelist.

Human review bắt buộc cho:
- tranh có đúng nghĩa không;
- câu có tự nhiên không;
- ngữ cảnh có phù hợp lứa tuổi không;
- phát âm/audio có đúng target accent/content không;
- câu hỏi có vô tình có nhiều đáp án hợp lý không.

## Release states

- DRAFT
- AUTO_VALIDATED
- HUMAN_REVIEWED
- VERIFIED
- HOLD
- RETIRED

Child runtime chỉ load `VERIFIED`.

## Regression

Mọi pack build phải chạy lại validator. Nếu engine/validator version thay đổi, pack cũ phải revalidate trước khi nâng trạng thái.

## Golden tests

Duy trì bộ test cố ý có lỗi:
- carry metadata sai;
- clock vẽ sai kim;
- duplicate choice;
- pictograph thiếu 1 icon;
- English picture ambiguous;
- wrong spelling;
- out-of-grade multiplication table;
- word problem phép toán mơ hồ.

Validator phải bắt được các lỗi machine-detectable; semantic ambiguous phải bị reviewer HOLD.
