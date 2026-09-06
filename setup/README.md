# WAHU KIDS LEARN — SETUP MASTER SPEC

Cập nhật: 2026-09-06
Trạng thái: DESIGN LOCK V1 — chờ benchmark máy Windows 7 thật để release-lock.

## 1. Mục tiêu

Thư mục `setup/` là nguồn quyết định duy nhất cho cách build, cài, khởi động, hiệu chỉnh, lưu dữ liệu, cập nhật, chẩn đoán và phát hành WAHU Kids Learn.

Nếu tài liệu khác xung đột với `setup/`, ưu tiên:
1. pháp lý/nguồn chương trình trong `research/`;
2. setup master này;
3. spec module;
4. implementation.

Mọi thay đổi setup lớn phải cập nhật version/config và regression test.

## 2. Quyết định V1 đã khóa

- OS mục tiêu: Windows 7 SP1 trở lên, x86 hoặc x64.
- Child runtime chính: **x86** để một build chạy cả Win7 32-bit và 64-bit.
- Framework: **.NET Framework 4.8**.
- UI: **WinForms + GDI+**, design baseline 1024×768.
- Graphics runtime: PNG/sprite + procedural GDI+; SVG ưu tiên build-time.
- Motion: WahuTween nội bộ + một MotionScheduler; 18 FPS LOW / 30 FPS NORMAL.
- Data: SQLite local, `System.Data.SQLite 2.0.4` + `SourceGear.sqlite3 3.53.4` (`e_sqlite3.dll` x86), không EF.
- Network: OFF mặc định; V1 không cần Internet để học.
- Installer: Inno Setup 7.1.x, installer 32-bit/per-user, `MinVersion=6.1sp1`.
- Portable package: có, nhưng dành cho triển khai/diagnostics; không phải mode mặc định cho trẻ.
- Update: offline installer/USB package; không auto-update Internet V1.
- Audio: BCL WAV trước; NAudio 2.x chỉ optional nếu thật sự cần.
- Content: chỉ `VERIFIED` được Child Mode nạp.
- Analytics/ads/trackers: không có.
- Parent Mode: PIN local, không cần account.
- Uninstall: giữ dữ liệu học mặc định; muốn xóa phải là lựa chọn rõ của phụ huynh.

## 3. Setup flow tổng thể

```text
BUILD MACHINE
  ↓
compile x86 Release
  ↓
unit/simulation/content validation
  ↓
pre-render verified SVG → PNG
  ↓
assemble content + licenses + hashes
  ↓
build Inno installer + portable ZIP
  ↓
installer smoke test
  ↓
TARGET PC
  ↓
OS/SP1/.NET/disk preflight
  ↓
install per-user
  ↓
first-run hardware audit
  ↓
render/input/audio benchmark
  ↓
auto select LOW/NORMAL
  ↓
create/migrate SQLite
  ↓
verify built-in content hashes
  ↓
child-friendly calibration
  ↓
READY
```

## 4. Các file setup

- `00_WIN7_LIGHTWEIGHT_SETUP.md`: baseline kỹ thuật tổng quát.
- `01_RUNTIME_DEPENDENCY_LOCK.md`: runtime/dependency/version policy.
- `02_INSTALLER_PORTABLE_UPDATE.md`: installer, portable, update/rollback.
- `03_FIRST_RUN_CALIBRATION.md`: first-run và calibration.
- `04_PERFORMANCE_AUTOTUNE.md`: benchmark + LOW/NORMAL.
- `05_DATA_BACKUP_RECOVERY.md`: SQLite, backup, migration, recovery.
- `06_CONTENT_ASSET_PIPELINE.md`: content/asset build/install runtime.
- `07_AUDIO_VOICE_INPUT.md`: audio, mic, recording.
- `08_UI_ACCESSIBILITY_DEFAULTS.md`: DPI, font, touch target, motion modes.
- `09_SECURITY_PRIVACY.md`: PIN, import, privacy, local security.
- `10_DIAGNOSTICS_LOGGING.md`: logs/diagnostics/export.
- `11_PARENT_CHILD_DEFAULTS.md`: default behavior/session/settings.
- `12_BUILD_RELEASE_MATRIX.md`: build/test/release gates.
- `13_DEV_ENVIRONMENT.md`: máy dev/toolchain.
- `14_RUNTIME_DIRECTORY_LAYOUT.md`: layout file runtime/data.
- `15_DECISION_REGISTER.md`: sổ quyết định V1, locked/deferred/conditional.
- `16_IMPLEMENTATION_CHECKLIST.md`: checklist implementation A–Q + test evidence.
- `17_SETUP_RUNTIME_STATE_MACHINE.md`: state machine installer/startup/recovery/content/update/shutdown.
- `18_WIN7_COMPATIBILITY_SIGNING.md`: readiness Win7/SHA-2, Authenticode và production signing.
- `19_MOTION_DPI_ACCESSIBILITY_ARCHITECTURE.md`: timing thật của motion, system-DPI Win7, accessibility semantics/drag alternatives.
- `20_SQLITE_DURABILITY_AND_BACKUP.md`: WAL state, Online Backup API, integrity tiers, recovery hardening.
- `config/*.json`: config để implementation đọc trực tiếp.
- `installer/WAHU_Kids_Learn.iss`: installer skeleton.

## 5. Nguyên tắc không được phá

1. Responsiveness > animation.
2. Learning state > reward polish.
3. Verified content > content quantity.
4. Child data > convenience của updater.
5. Offline capability > cloud feature.
6. Một dependency chỉ được thêm khi lợi ích đo được lớn hơn cost triển khai/binary/performance.
7. Không có feature V1 nào được yêu cầu quyền admin sau khi prerequisites đã cài.
8. Không ghi file học vào thư mục cài app.
9. Không ghi database mỗi frame/click vô nghĩa.
10. Không để UI/animation quyết định source-of-truth của mastery.

## 6. Release-lock còn phụ thuộc máy thật

Các giá trị sau là engineering defaults và phải benchmark trên PC thật:
- startup target;
- cache MB;
- exact FPS cap;
- PBKDF2 iteration count;
- audio preload;
- WAL/checkpoint strategy;
- max active sprite atlas;
- memory gate LOW/NORMAL.

Không đổi kiến trúc chỉ vì một benchmark nhỏ chưa tối ưu; trước tiên profile bottleneck.