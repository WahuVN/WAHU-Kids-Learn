# 10 — CONTENT SYSTEM & LESSON PACK

## 1. Mục tiêu

Nội dung không hard-code vào app.

App runtime phải có thể nạp lesson/content pack versioned.

## 2. Pack format dự kiến

```text
math_grade2_v1.wahu/
├── manifest.json
├── skills.json
├── lessons/
├── questions/
├── audio/
├── images/
├── atlas/
└── checksums.json
```

Pack thực tế có thể là archive signed/validated.

## 3. Manifest

Fields:
- pack_id;
- version;
- subject;
- grade;
- locale;
- min_app_version;
- created_at;
- content_schema_version;
- checksum;
- optional signature.

## 4. Question schema

Mỗi item cần:
- id;
- primary skill;
- prerequisites;
- type;
- prompt;
- choices/answer;
- distractor rationale;
- difficulty features;
- representation;
- hints;
- feedback;
- assets;
- tags.

## 5. Template generation

Có thể dùng generator nhưng phải tránh các câu vô nghĩa.

Ví dụ math template có constraints:
- range;
- carry/borrow;
- duplicate answer;
- distractor uniqueness;
- language agreement.

## 6. Content validation pipeline

```text
Author
→ schema validation
→ asset validation
→ answer validation
→ duplicate detection
→ difficulty sanity
→ distractor audit
→ language review
→ pack build
→ runtime smoke test
```

## 7. AI content generation

AI có thể hỗ trợ ở máy phụ huynh/dev để:
- tạo candidate;
- tạo variation;
- tạo illustration brief;
- gợi ý distractor;
- phân tích coverage.

Nhưng content đưa cho trẻ phải qua validation.

Không để model online tự sinh câu trực tiếp trong Child Mode V1.

## 8. Parent Content Studio — tương lai

Chức năng:
- chọn chương đang học;
- bật/tắt skills;
- import pack;
- xem coverage;
- preview child question;
- kiểm tra answer/hint;
- export `.wahu`.

## 9. Asset budget per pack

Cần giới hạn:
- max image dimensions;
- max audio length;
- total pack size;
- number of simultaneous assets.

Mục đích: không làm PC cũ lag.

## 10. Content provenance

Mỗi pack cần ghi nguồn/author/version để biết nội dung nào đang chạy và rollback được.