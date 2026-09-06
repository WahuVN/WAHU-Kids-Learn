# 20 — SQLITE DURABILITY, WAL & BACKUP HARDENING

Cập nhật: 2026-09-06
Trạng thái: V1 DESIGN LOCK — target benchmark pending.

## 1. WAL là state, không phải file phụ bỏ được

Nếu runtime dùng WAL, file `learning.db-wal` khi tồn tại là một phần persistent state của database. Không copy/move `learning.db` riêng lẻ trong khi DB đang active rồi coi đó là backup hợp lệ.

## 2. Backup policy

Ưu tiên thứ tự:
1. SQLite Online Backup API/provider equivalent;
2. maintenance mode: close/checkpoint/close connections sạch rồi file copy;
3. raw hot-copy DB file: CẤM.

Backup flow:

```text
request backup
→ serialize with write coordinator as needed
→ SQLite-safe snapshot to temp
→ open snapshot
→ schema/version check
→ integrity strategy
→ SHA-256
→ write metadata
→ atomic publish backup
```

## 3. WAL vs DELETE

WAL chỉ bật nếu target tests pass:
- local filesystem;
- crash/kill recovery;
- checkpoint behavior;
- backup while active;
- update/portable behavior;
- no network/shared filesystem dependency.

Portable USB mode được benchmark riêng; có thể ép `DELETE` nếu WAL durability/USB removal risk không đáng.

## 4. Integrity tiers

### Normal startup
- DB open;
- schema version;
- migration metadata;
- essential table/query sanity;
- optional `quick_check` theo cadence, không nhất thiết mọi launch.

### Abnormal shutdown / recovery / pre-release maintenance
- `PRAGMA integrity_check`;
- `PRAGMA foreign_key_check` riêng;
- schema/migration checks.

Lý do: `integrity_check` không thay thế `foreign_key_check`; `quick_check` nhanh hơn nhưng không kiểm đầy đủ như full integrity check.

## 5. Recovery

Không atomic-replace original cho tới khi restored temp DB:
- open được;
- đúng schema supported;
- integrity pass;
- foreign keys pass;
- backup hash/metadata pass.

Giữ original `.corrupt.<timestamp>` cho tới khi Parent xác nhận cleanup.

## 6. Migration

Before migration có destructive/schema-sensitive changes:
- verified pre-migration backup;
- available disk headroom;
- migration checksum/version;
- rollback = restore backup, không tự chạy reverse SQL chưa test.

## 7. Tests

- backup during active read/write;
- kill during backup;
- kill during WAL checkpoint;
- copy-only-main-DB negative test;
- malformed WAL/recovery scenario;
- `quick_check`/`integrity_check`/`foreign_key_check` fixture tests;
- USB portable removal simulation;
- near-full disk during backup;
- restore then continue learning without duplicate reward.
