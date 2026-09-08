# 19 — REAL WINDOWS 7 TARGET VALIDATION

Trạng thái: automated target gate available; real-machine evidence vẫn bắt buộc.

## Mục tiêu

`win7_target_smoke=PASS` chỉ được ghi khi **đúng artifact phát hành** đã chạy trên Windows 7 SP1 thật và report khớp release manifest. Preflight mô phỏng trên Windows mới không thay thế gate này.

## Quy trình

Trên build host sạch, sau `Build-SetupArtifacts.ps1`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\build\New-Win7ValidationPack.ps1 -AppVersion <version>
```

Copy ZIP trong `build\win7-target\` sang máy Windows 7 SP1, giải nén và chạy:

```text
RUN-WIN7-VALIDATION.cmd
```

Target runner `Test-Win7Target.ps1` cố ý giữ tương thích PowerShell 2.0 và không cần Git/Python/dotnet SDK. Nó kiểm:

- OS thật là Windows 7 (`6.1`) build `>=7601` và Service Pack 1;
- .NET Framework Release `>=528040`;
- SHA-256 Portable ZIP khớp artifact build;
- production-art manifest SHA khớp release;
- `WAHU.SetupPreflight.exe` nhận đúng Win7, x86 và `WIN7_SP1_RUNTIME_COMPATIBLE`;
- bootstrap portable hai lần, schema/migration V6, SQLite integrity/FK, 2 bundled content packs;
- child UI thật tạo main window;
- portable run không tạo/sửa learner DB của installed mode.

Runner ghi `win7-target-report.json` kể cả khi fail.

Copy report về build host rồi chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\build\Import-Win7TargetEvidence.ps1 `
  -ReportPath <win7-target-report.json> -Apply
```

Importer fail-closed nếu report không khớp `app_version`, `git_commit`, Portable SHA, production-art SHA, schema V6 hoặc evidence máy Win7 SP1. Chỉ khi toàn bộ khớp mới cập nhật:

```text
gates.win7_target_smoke = PASS
win7_target.status = TARGET_SMOKE_VERIFIED
```

## Safety

- Target validation dùng Portable ZIP; không cài/gỡ app.
- Không xóa `%LOCALAPPDATA%\WAHU Kids Learn`.
- Nếu installed learner DB đã tồn tại, SHA trước/sau phải giống nhau.
- Production Authenticode vẫn là gate riêng; Win7 target PASS không biến unsigned dev artifact thành production-signed.
- Report từ VM/máy khác, commit khác hoặc artifact SHA khác không được tái sử dụng.

## Regression của importer

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\build\Import-Win7TargetEvidence.ps1 -SelfTest
```

Self-test bắt buộc accept report hợp lệ và reject ít nhất: wrong artifact SHA, non-Win7 OS, wrong Git commit.
