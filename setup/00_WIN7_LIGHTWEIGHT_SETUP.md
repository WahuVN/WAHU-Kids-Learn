# 00 — WINDOWS 7 LIGHTWEIGHT SETUP BASELINE

Cập nhật: 2026-09-06
Trạng thái: V1 DEVELOPMENT LOCK. Xem `setup/README.md` là setup master.

## 1. Runtime target đã khóa cho V1

```text
OS minimum       Windows 7 SP1
App platform     x86 (chạy trên Win7 x86 và x64)
Framework        .NET Framework 4.8
UI               WinForms
Renderer         System.Drawing / GDI+
Data             SQLite local
Network          OFF mặc định
```

Microsoft liệt kê Windows 7 SP1 có thể cài .NET Framework tới 4.8; Windows 7 bản thân đã hết support. Vì vậy app offline-first, không phụ thuộc browser/network hiện đại.

Nguồn:
- https://learn.microsoft.com/en-us/dotnet/framework/get-started/system-requirements
- https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server

Đây là **development stack đã khóa**, không còn benchmark C++/Qt/SDL2 song song. Benchmark máy thật là release/performance gate; chỉ mở lại quyết định runtime nếu stack này fail requirement cốt lõi sau khi đã profile/optimize hợp lý.

## 2. Dependency baseline

Runtime bắt buộc:
- `System.Data.SQLite 2.0.4` + `SourceGear.sqlite3 3.53.4`, x86, ADO.NET trực tiếp; native runtime là `e_sqlite3.dll`.

Ưu tiên internal/BCL:
- WahuTween;
- MotionScheduler;
- sprite player;
- WAV playback;
- SHA-256/import validation.

Build-time optional:
- SVG.NET để verified SVG → PNG.

Không core:
- Electron/WebView2;
- Lottie/SkiaSharp;
- game engine chỉ để dựng UI;
- video background.

Chi tiết: `setup/01_RUNTIME_DEPENDENCY_LOCK.md`.

## 3. First-run hardware audit

Ghi local:

```text
os_version
service_pack
os_arch
cpu_name
logical_cores
ram_total_mb
screen_resolution
system_dpi
gpu_name_if_available
audio_output_available
microphone_available
net_framework_release
free_disk_mb
benchmark_profile
```

Không upload.

## 4. Performance profiles

### LOW

```text
motion_fps_cap = 18
idle/decorative = minimal/off
max_animated_regions = 1
particles = off
image_cache_mb = 48
audio_cache_mb = 12
```

### NORMAL

```text
motion_fps_cap = 30
decorative = off_during_question
max_animated_regions = 2
particles = milestone_small_only
image_cache_mb = 96
audio_cache_mb = 24
```

Đây là engineering defaults; autotune bằng benchmark local. Không có HIGH V1.

## 5. Motion runtime

```text
MotionScheduler
WahuTween / Easing
SpriteSheetPlayer
GifPlayer (nếu cần idle nhỏ)
MotionPolicy
RenderBudgetMonitor
```

Rule:
- một scheduler;
- no Timer per control;
- cancellable;
- invisible => stop;
- final state deterministic;
- input luôn ưu tiên motion.

## 6. Asset runtime

Instructional static:
`verified SVG source → build-time PNG → child runtime`.

Instructional dynamic:
`data → procedural GDI+`.

Character:
`sprite sheet PNG`, frame count nhỏ.

Không Lottie/video/animated SVG trong core.

## 7. Audio

Default:
- Voice ON;
- SFX calm ON;
- Music OFF;
- Mic OFF tới khi Parent bật/lesson cần.

PCM WAV là baseline zero-extra-codec. NAudio 2.x chỉ optional nếu record/compressed playback thực sự cần.

## 8. Installer

- Inno Setup 7.1.x.
- 32-bit setup.
- `MinVersion=6.1sp1`.
- per-user / `PrivilegesRequired=lowest`.
- install: `%LOCALAPPDATA%\Programs\WAHU Kids Learn`.
- learner data: `%LOCALAPPDATA%\WAHU Kids Learn`.
- .NET 4.8 preflight bằng Release key `>=528040`.
- không tự download Internet.

Actual script: `setup/installer/WAHU_Kids_Learn.iss`.

## 9. SQLite/backup

- learner DB riêng static content;
- attempts immutable;
- reward source-key idempotent;
- WAL chỉ nếu target test pass;
- backup bằng SQLite-safe snapshot;
- pre-migration backup bắt buộc;
- recovery restore vào temp rồi atomic replace.

Actual schema: `data/schema/001_initial.sql`.

## 10. Setup install/first-run flow

```text
installer preflight
→ install per-user
→ data dirs
→ DB create/migrate
→ content hash verify
→ hardware benchmark
→ LOW/NORMAL
→ audio test
→ child calibration nhẹ
→ READY
```

Không account. Không network bắt buộc.

## 11. Child calibration

Không gọi IQ/thi đầu vào.

- volume;
- click/drag đơn giản;
- vài warm-up items;
- response-time personal prior;
- text/target-size sanity.

Không khóa mastery từ vài câu đầu.

## 12. Machine acceptance gate

Phải test thật:
- Win7 SP1 x86;
- Win7 SP1 x64 chạy x86 app;
- 2 GB / 4 GB RAM;
- HDD chậm;
- 1024×768 / 1366×768;
- 125% DPI nếu có;
- audio/mic missing;
- sleep/wake;
- rapid click;
- process kill;
- DB corrupt/busy;
- backup/restore;
- installer/update/uninstall;
- offline hoàn toàn.

Stack chỉ bị reopen nếu các gate cốt lõi fail sau tối ưu có kiểm chứng.

## 13. Legacy Win7 readiness

`Windows 7 SP1` chỉ là OS floor, không đồng nghĩa target-ready.

Preflight/QA phải ghi thêm:
- `.NET 4.8 Release >= 528040`;
- SHA-2/update readiness `PASS | WARN | UNKNOWN`;
- evidence về KB4490628 / KB4474419 hoặc state thay thế phù hợp;
- production signature self-test;
- target smoke result.

Không hard-fail chỉ vì không tìm thấy đúng một KB ID; xem `18_WIN7_COMPATIBILITY_SIGNING.md`.

## 14. Timing/DPI clarification

- LOW 18 / NORMAL 30 là render caps, không phải độ chính xác timer.
- `System.Windows.Forms.Timer` không được dùng như clock 33 ms cho 30 FPS.
- elapsed time dùng `Stopwatch`; wake-up coalesced, frame trung gian có thể bỏ.
- Win7 dùng system-DPI aware manifest; test 96/120 DPI.
- drag không thiết yếu phải có non-drag alternative.

Chi tiết: `19_MOTION_DPI_ACCESSIBILITY_ARCHITECTURE.md`.
