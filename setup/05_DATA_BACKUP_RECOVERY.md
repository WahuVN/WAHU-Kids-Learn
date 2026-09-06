# 05 — DATA, SQLITE, BACKUP, MIGRATION & RECOVERY

## 1. Data architecture

Tách **static verified content** khỏi **dynamic learner data**.

Static content:
- curriculum;
- lesson/question packs;
- images/audio;
- skill/prerequisite definitions.

Dynamic SQLite:
- child profile/settings;
- sessions;
- attempts;
- child_skill current state;
- mastery/error/behavior events;
- review schedule;
- reward/inventory;
- parent notes;
- installed content state;
- backup/migration metadata.

Không copy toàn bộ question bank vào learner DB nếu không cần.

## 2. SQLite provider

Baseline đã test trên workstation:
- `System.Data.SQLite 2.0.4`;
- `SourceGear.sqlite3 3.53.4` / SQLite engine 3.53.4;
- x86 / `e_sqlite3.dll`;
- .NET Framework 4.8.

`System.Data.SQLite 1.0.119` đã bị NuGet đánh dấu deprecated vì critical bugs nên không còn là V1 lock.

Không EF, không ORM nặng.

Repository dùng parameterized SQL.

## 3. Connection rules

- foreign_keys ON;
- busy_timeout bounded;
- một write coordinator;
- transaction cho logical event batch;
- không giữ transaction qua UI wait;
- không write trên mỗi animation frame;
- retry DB busy có backoff nhỏ và giới hạn.

## 4. Journal mode

WAL chỉ bật sau test trên filesystem/máy thật.

Nếu WAL ổn:
- app writer + readers đơn giản;
- checkpoint ở safe points;
- không checkpoint liên tục;
- coi `learning.db-wal` là một phần persistent DB state khi còn tồn tại;
- không tách/copy riêng main DB trong lúc active;
- không dùng WAL cho network/shared filesystem;
- portable USB benchmark riêng, có thể ép rollback journal nếu an toàn hơn.

Chi tiết hardened flow: `20_SQLITE_DURABILITY_AND_BACKUP.md`.

Nếu WAL không ổn trên target:
- rollback journal mặc định.

Config lưu mode để diagnostics biết.

## 5. Attempt immutability

Attempt đã commit không update để “đẹp dữ liệu”.

Correction bằng event mới.

Mastery state có thể recompute từ history/versioned events.

## 6. Session transaction model

Khi câu được trả lời:
1. insert attempt;
2. insert error/mastery event nếu có;
3. update materialized child_skill;
4. update review_schedule;
5. commit;
6. reward event tách idempotent theo source key nếu milestone.

Nếu commit fail → không hiện reward như đã lưu.

## 7. Backup

Backup phải là snapshot SQLite hợp lệ, không file-copy DB đang write một cách mù quáng.

Ưu tiên:
1. SQLite **Online Backup API/provider equivalent**;
2. đóng/checkpoint/close connection sạch rồi copy trong maintenance;
3. raw hot-copy chỉ `learning.db` trong lúc active: **CẤM**.

Snapshot phải open/verify được trước khi publish thành backup hợp lệ.

Mỗi backup có:
- db file;
- metadata JSON;
- SHA-256;
- app/schema version;
- active content manifest list.

## 8. Rotation baseline

Engineering default:
- 5 backup gần nhất;
- 4 weekly snapshots nếu dung lượng nhỏ;
- pre-migration backup luôn giữ tới khi release mới ổn.

Parent có thể export manual backup ra Documents/USB.

## 9. Integrity

Startup bình thường không cần `PRAGMA integrity_check` full mỗi lần nếu tốn thời gian.

Chiến lược:
- startup thường: schema/metadata + essential-query sanity; `quick_check` theo cadence/suspicion nếu phù hợp;
- crash/recovery/maintenance: `PRAGMA integrity_check`;
- chạy `PRAGMA foreign_key_check` riêng vì integrity_check không thay thế FK check;
- hash + open + schema check backup trước restore.

Chi tiết hardened flow: `20_SQLITE_DURABILITY_AND_BACKUP.md`.

## 10. Recovery flow

```text
DB open fail
→ safe read-only recovery shell
→ locate newest verified backup
→ offer Restore
→ restore to NEW temp DB
→ run integrity/schema check
→ atomic replace original
→ keep corrupt original as .corrupt timestamp
→ relaunch
```

Không overwrite corrupt DB trước khi recovery copy thành công.

## 11. Migration

Mỗi migration:
- id/version;
- checksum;
- transactional nếu SQLite hỗ trợ operations đó;
- idempotency guard;
- automated test from every supported previous schema.

Trước major migration → backup.

## 12. Uninstall/data retention

Uninstaller không xóa `%LOCALAPPDATA%\WAHU Kids Learn\Data` mặc định.

Explicit delete flow phải ở Parent/Uninstaller với cảnh báo rõ.