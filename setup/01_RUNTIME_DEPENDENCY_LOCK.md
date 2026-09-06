# 01 — RUNTIME & DEPENDENCY LOCK

Cập nhật: 2026-09-06

## 1. Runtime chính

```text
TargetFramework: .NET Framework 4.8
PlatformTarget: x86
UI: WinForms
Renderer: GDI+ / System.Drawing
Language: C#
OS minimum: Windows 7 SP1
```

Windows 7 SP1 có thể cài .NET Framework 4.8; .NET Framework 4.8 là bản cuối hỗ trợ Windows 7. Windows 7 bản thân đã hết support nên app phải offline-first.

Nguồn Microsoft:
- https://learn.microsoft.com/en-us/dotnet/framework/get-started/system-requirements
- https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server

## 2. Vì sao x86 là primary

- chạy trên Win7 32-bit và Win7 64-bit;
- mục tiêu working set thấp hơn nhiều giới hạn 32-bit;
- đơn giản hóa native SQLite interop;
- chỉ cần một bộ dependency native;
- test matrix nhỏ hơn;
- installer dễ hơn.

Chỉ tạo x64 release nếu benchmark thực tế chứng minh có lợi rõ và không làm tăng support cost quá mức.

## 3. Dependency budget

### REQUIRED

#### System.Data.SQLite 2.x + e_sqlite3
- Managed provider pin: `System.Data.SQLite 2.0.4`.
- Native SQLite pin: `SourceGear.sqlite3 3.53.4` → `e_sqlite3.dll`, SQLite engine 3.53.4.
- Platform: explicit x86; không AnyCPU.
- Không dùng Entity Framework/LINQ provider; chỉ ADO.NET parameterized SQL/repository layer.
- `System.Data.SQLite 1.0.119` không còn được phép: NuGet đánh dấu deprecated vì critical bugs.
- 2026-09-06 local evidence: net48/x86 provider load PASS; PE machine `0x014C`; native import audit không thấy VCRUNTIME/MSVCP/UCRT ngoài; schema + migration checksum/tamper guard + serialized writes/rollback + DELETE/WAL + Online Backup/restore integration smoke 58/58 PASS.
- Clean Win7 SP1 x86/x64 native-load/crash smoke vẫn là release gate; local dev evidence không thay target-machine test.

Nguồn:
- https://www.nuget.org/packages/System.Data.SQLite/2.0.4
- https://system.data.sqlite.org/home/doc/trunk/www/build.md
- https://www.nuget.org/packages/SourceGear.sqlite3/3.53.4

### PREFER INTERNAL / BCL
- WahuTween.
- MotionScheduler.
- SpriteSheetPlayer.
- basic GIF playback nếu dùng.
- WAV SFX playback.
- JSON serializer phù hợp framework đã chọn.
- hashing SHA-256.
- file/archive validation helpers.

### DEV/BUILD-TIME OPTIONAL
- SVG.NET để render verified SVG sang PNG.
- image optimization tools.
- Python scripts cho content validation/build.

Không bắt buộc ship vào child runtime.

### OPTIONAL RUNTIME
- Nếu cần NAudio, baseline pin **NAudio 2.2.1 / 2.x đã verify**; 2.2.1 target .NET Framework 4.7.2 nên chạy trên net48 sau smoke-test.
- Chỉ thêm khi recording/mixing/compressed playback thực sự cần.
- **Không auto-upgrade NAudio 3**: nhánh 3.x đã bỏ .NET Framework và target runtime hiện đại.

### NOT CORE V1
- Electron.
- WebView2.
- Chromium.
- SkiaSharp.
- Lottie runtime.
- Unity/Godot chỉ để làm UI.
- video background.
- browser-based local server.

## 4. Quy tắc thêm dependency

Mọi dependency mới phải có record:

```text
name
version
source_url
license
runtime_or_buildtime
managed_or_native
x86_support
win7_support_evidence
binary_size
startup_cost
memory_cost
reason
rollback_plan
```

Không thêm package chỉ vì UI demo đẹp.

## 5. Version pinning

- Không dùng floating version.
- NuGet lock file/PackageReference version cố định.
- Installer compiler version ghi trong build manifest.
- Content schema version độc lập app version.
- Hash binary dependency trong release manifest.

## 6. Runtime configuration

- `Prefer32Bit=true` hoặc explicit x86 project.
- High DPI xử lý ở **system DPI**; app manifest Win7 phải có `dpiAware=true`.
- Không dựa vào per-monitor DPI V2 hoặc enhanced WinForms High-DPI behavior yêu cầu Windows 10.
- `AutoScaleMode=Dpi` chỉ giữ nếu visual regression 96/120 DPI trên Win7 không tạo layout drift.
- Culture UI: `vi-VN` mặc định cho shell phụ huynh/trẻ; English content giữ locale riêng.
- timestamps lưu UTC trong DB; convert local khi hiển thị.

## 7. Failure rule

Nếu dependency native không load:
- Child app không crash loop;
- vào recovery screen đơn giản;
- ghi diagnostic local;
- đề xuất Repair;
- không tự download DLL từ Internet.