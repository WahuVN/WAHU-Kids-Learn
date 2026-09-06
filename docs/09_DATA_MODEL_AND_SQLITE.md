# 09 — DATA MODEL & SQLITE

## 1. Kiến trúc dữ liệu đã chốt

Tách hai lớp:

### Static verified content
Nằm trong content pack versioned:
- curriculum/skill definitions;
- prerequisites;
- lesson/question bank;
- images/audio;
- provenance/checksums.

### Dynamic learner data
Nằm trong SQLite:
- profile/settings;
- session/attempt;
- child_skill materialized state;
- error/mastery/behavior/adaptive decision events;
- review schedule;
- reward/inventory;
- parent notes;
- content install state;
- backup/migration metadata.

Không copy toàn bộ question bank vào learner DB. Attempt tham chiếu `pack_id + pack_version + question_id` để audit chính xác.

Actual migration: `data/schema/001_initial.sql`.

## 2. Runtime DB

```text
Provider: System.Data.SQLite 2.0.4
Native SQLite: SourceGear.sqlite3 3.53.4 / e_sqlite3.dll
Platform: x86
ORM: none
Access: parameterized ADO.NET SQL
```

Rule:
- `foreign_keys=ON`;
- bounded busy timeout;
- một write coordinator;
- transaction theo logical event;
- no UI-wait transaction;
- no DB write per frame.

## 3. Core dynamic tables

- `app_meta`
- `migration_history`
- `child`
- `child_setting`
- `child_skill`
- `session`
- `attempt`
- `attempt_correction_event`
- `error_event`
- `mastery_event`
- `behavior_state_event`
- `adaptive_decision_event`
- `review_schedule`
- `reward_event`
- `inventory`
- `parent_note`
- `content_pack_state`
- `backup_history`

## 4. Attempt immutability

Attempt cũ không update để “làm đẹp”.

Correction tạo `attempt_correction_event` và giữ audit trail.

## 5. Answer transaction

```text
BEGIN
insert attempt
→ error/mastery event nếu có
→ update child_skill
→ update review_schedule
COMMIT
→ reward milestone bằng idempotent source_key
```

Nếu commit fail thì UI không được coi state/reward là đã lưu.

## 6. Behavior vs knowledge

`behavior_state_event` lưu operational learning state với confidence/evidence/action; không phải diagnosis.

Nếu `FATIGUED_LIKELY`, mastery negative update phải cần knowledge-error evidence riêng.

## 7. Explainability

`adaptive_decision_event` ghi:
- question chosen;
- skill;
- behavior state/confidence;
- difficulty fit;
- reason/candidate summary;
- engine version.

Giúp audit “vì sao app hỏi câu này?”.

## 8. Algorithm versioning

Events/materialized state phải biết version liên quan:
- mastery engine;
- scheduler;
- error classifier;
- behavior controller;
- adaptive selector.

Không thay thuật toán rồi reinterpret lịch sử một cách mơ hồ.

## 9. Journal mode

Không hard-code WAL trong migration.

Runtime:
- test WAL trên target;
- nếu pass thì dùng WAL + checkpoint safe points;
- nếu không thì rollback journal (`DELETE`).

## 10. Backup

Không copy file DB đang write một cách mù quáng.

Ưu tiên SQLite backup API hoặc close sạch rồi copy.

Backup metadata:
- SHA-256;
- app/schema version;
- active content versions;
- verification status.

Baseline rotation:
- 5 recent;
- 4 weekly;
- pre-migration backup.

## 11. Recovery

```text
open fail
→ recovery shell
→ newest verified backup
→ restore to temp DB
→ integrity/schema check
→ atomic replace
→ retain original as .corrupt timestamp
```

Không phá original trước khi restore pass.

## 12. Migration

Mỗi migration:
- integer version;
- name;
- checksum;
- applied timestamp;
- automated upgrade test từ mọi schema còn support.

Major migration có backup trước.

## 13. Privacy

Không cần:
- child email/phone/address;
- location;
- advertising ID;
- contact/browser data.

Nickname đủ cho profile.