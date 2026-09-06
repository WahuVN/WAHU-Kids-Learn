# 15 — V1 DECISION REGISTER

Cập nhật: 2026-09-06

| Quyết định | Trạng thái | Lý do / Gate |
|---|---|---|
| Win7 SP1 minimum | LOCKED | Thiết bị mục tiêu |
| .NET Framework 4.8 | DEVELOPMENT LOCK | Bản .NET Framework cuối hỗ trợ Win7; smoke-test target bắt buộc |
| WinForms + GDI+ | DEVELOPMENT LOCK | Footprint/compatibility; đổi chỉ nếu machine gate fail rõ |
| x86 primary | LOCKED V1 | Một build cho Win7 32/64-bit, SQLite native đơn giản, RAM budget thấp |
| x64 build | DEFERRED | Chỉ nếu benchmark có lợi đo được |
| System.Data.SQLite 2.0.4 + SourceGear.sqlite3 3.53.4 x86 | DEVELOPMENT LOCK | 1.0.119 deprecated vì critical bugs; net48/x86 build + SQLite 3.53.4 + 58-assertion DB/migration/write-coordinator/backup/restore smoke PASS; Win7 target smoke vẫn bắt buộc |
| WAL | CONDITIONAL | Bật chỉ khi target filesystem/crash test pass |
| Inno Setup 7.1.x | DEVELOPMENT LOCK | Win7 support + per-user installer; kiểm license trước commercial release |
| Per-user install | LOCKED V1 | Không cần admin sau prerequisite |
| Internet auto-update | OFF V1 | Offline-first |
| SVG runtime | OFF BY DEFAULT | Pre-render static SVG → PNG |
| Skia/Lottie/WebView | NOT CORE | Footprint/dependency không đáng cho target |
| Motion NORMAL 30 FPS | ENGINEERING DEFAULT | Benchmark target có quyền hạ LOW |
| Motion LOW 18 FPS | ENGINEERING DEFAULT | Usability > decorative smoothness |
| Music default | OFF | Voice/attention priority |
| Mic default | OFF | Optional + privacy |
| Parent PIN | ON/LOCAL | Child/parent separation |
| Remote analytics | OFF | Privacy/offline |
| Child live AI | OFF V1 | Content verification gate |
| Only VERIFIED content | HARD GATE | Safety/accuracy |
| Learner DB separated from content | LOCKED | Smaller DB, safer updates/backup |
| Uninstall preserves learner data | HARD GATE | Prevent accidental loss |

## Change control

Một mục `LOCKED V1` chỉ thay khi có:
1. bằng chứng benchmark/test rõ;
2. impact analysis;
3. migration/rollback plan;
4. update tài liệu + config + regression test.

Một mục `ENGINEERING DEFAULT` có thể tune bằng benchmark mà không đổi product principle.