# 18 — RESEARCH STATUS

Cập nhật: 2026-09-06

## Trạng thái vòng setup/design research

Pha tìm nguồn chính thức + pattern mã nguồn mở đã hoàn tất cho các gap ưu tiên hiện tại.

Đã chuyển research thành setup/spec/config thật:
- Win7 legacy SHA-2 readiness + production signing;
- MotionScheduler timing đúng cho WinForms/.NET Framework;
- system-DPI + accessibility/drag alternatives/UI Automation semantics;
- SQLite WAL/Online Backup/integrity hardening;
- NAudio 2.x compatibility guard;
- archive import adversarial tests;
- data minimization/retention;
- release/test gates A–R.

Các phát hiện không được coi là implementation đã hoàn thành. Những mục còn cần code/toolchain/hardware được giữ PENDING trong `setup/AUDIT_STATUS.md` và `setup/16_IMPLEMENTATION_CHECKLIST.md`.

Regression cuối vòng phải gồm:
1. parse toàn bộ JSON;
2. execute SQLite schema + foreign-key check;
3. installer static audit;
4. stale-spec sweep;
5. placeholder/TODO accidental artifact sweep;
6. tree inventory.
