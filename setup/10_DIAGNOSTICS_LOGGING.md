# 10 — DIAGNOSTICS & LOGGING

## 1. Mục tiêu

Có đủ dữ liệu debug crash/performance/content nhưng không biến app thành telemetry product.

Tất cả local.

## 2. Log levels

- ERROR: luôn bật.
- WARN: luôn bật.
- INFO: bounded.
- DEBUG: Parent/Developer Mode only.
- TRACE: build dev only.

## 3. Format

JSONL UTF-8 hoặc structured text dễ parse.

Fields chung:

```text
timestamp_utc
level
event_code
app_version
schema_version
session_id_optional
module
details_safe
```

Không log exception stack ra Child UI.

## 4. Rotation

Default:
- max 5 files;
- khoảng 2 MB/file;
- crash dump/text riêng bounded.

Config có thể đổi ở dev build.

## 5. Event codes quan trọng

- APP_START / APP_CLEAN_EXIT.
- DB_OPEN_FAIL / DB_RECOVERY.
- MIGRATION_START/PASS/FAIL.
- PACK_VERIFY_FAIL.
- AUDIO_DEVICE_FAIL.
- MOTION_DEGRADE.
- INPUT_DOUBLE_SUBMIT_BLOCKED.
- CONTENT_HOLD_BLOCKED.
- BACKUP_PASS/FAIL.
- UNHANDLED_EXCEPTION.

## 6. Learning decision audit

Không dùng general log làm source-of-truth.

Adaptive decision cần event riêng trong learner DB:
- chosen_question_id;
- candidate reasons;
- behavior state/confidence;
- difficulty fit;
- review due reason;
- action taken.

Cho phép Parent Advanced/Dev xem explainability.

## 7. Performance sample

Chỉ sample aggregate:
- working set peak;
- frame p95;
- input delay p95;
- DB write p95;
- cache hit ratio.

Không sample mỗi frame vào disk.

## 8. Export diagnostics

Parent explicit action tạo ZIP:
- app/version info;
- hardware profile;
- sanitized logs;
- content manifest;
- config;
- optional DB schema metadata, KHÔNG learner DB mặc định.

Nếu cần gửi learner DB để debug phải là lựa chọn riêng rõ ràng.