# 15 — TECHNICAL IMPLEMENTATION FINDINGS

Cập nhật: 2026-09-06

## Motion scheduler trên WinForms/.NET Framework 4.8

### Phát hiện
`System.Windows.Forms.Timer` chạy trên UI thread và tài liệu Microsoft ghi độ chính xác bị giới hạn khoảng 55 ms.

### Hệ quả
Không triển khai `30 FPS = Timer.Interval 33 ms` rồi coi đó là timing chính xác.

### Kiến trúc nên dùng
- elapsed-time source: `System.Diagnostics.Stopwatch`;
- target frame interval chỉ là cap/pacing;
- tick source có thể dùng timer/background wake-up nhưng phải coalesce;
- background callback không trực tiếp mutate WinForms controls;
- marshal một invalidate/update request về UI thread;
- nếu UI đang bận, bỏ frame trung gian và tính state theo elapsed time;
- không queue nhiều `BeginInvoke` frame requests cùng lúc;
- endpoint animation luôn deterministic.

`Stopwatch.IsHighResolution` có thể cho biết hệ thống đang dùng high-resolution performance counter.

### Rendering
Custom animated controls dùng built-in `DoubleBuffered=true` hoặc `OptimizedDoubleBuffer` theo hướng dẫn Microsoft để giảm flicker.

### Resource lifetime
`Image`, `Graphics`, `Bitmap` và resource GDI tạo chủ động phải được Dispose deterministic; không chờ GC/finalizer.

## DPI trên Windows 7

- dùng app manifest `dpiAware=true` → system-DPI aware trên Vista/Win7/Win8;
- không dựa vào Per-Monitor V2 hay enhanced WinForms High-DPI APIs của Windows 10;
- layout phải dùng scale helper và kiểm 96/120 DPI;
- pixel-perfect sprite có thể cần nearest-neighbor riêng, còn text/control dùng DPI scaling.

## Accessibility WinForms

Standard controls có UI Automation providers từ Windows. Custom controls phải cung cấp semantics.

Checklist custom control:
- AccessibleName;
- AccessibleDescription khi thực sự bổ sung ý nghĩa;
- AccessibleRole;
- keyboard/tab path ở Parent Mode và nơi phù hợp;
- không biến PictureBox thành nút chỉ bằng MouseClick mà không có semantic role/keyboard equivalent.

## Audio

### BCL baseline
`System.Media.SoundPlayer` chỉ phục vụ WAV; `Play()` async, nhưng lần đầu có thể load file. Preload bằng `LoadAsync`/bounded cache để không giật input.

### NAudio
NAudio 2.2.1 target .NET Framework 4.7.2 nên compatible với net48. NAudio 3 đã bỏ .NET Framework và yêu cầu net9.0, vì vậy V1 nếu cần NAudio phải pin 2.2.1/2.x và không auto-upgrade major.

### Mic
WinMM `waveInOpen` tồn tại trên Windows 7 và đủ cho record/replay đơn giản. Có thể dùng trực tiếp P/Invoke hoặc thông qua NAudio 2.x; chọn sau benchmark/complexity comparison.

## Code signing

- SHA-256 file digest;
- RFC3161 timestamp với SHA-256;
- Windows 7 SP1 trở lên đủ nền SHA-2 có thể dùng SHA-256-only signing;
- Inno `SignTool` có thể ký Setup và SignedUninstaller;
- Authenticode certificate khác `.issig`; `.issig` hữu ích cho integrity của source/external files nhưng không xóa Unknown Publisher.

Release pipeline nên có:
`sign app binaries → verify → compile installer with SignTool/SignedUninstaller → verify installer/uninstaller → hash release`.
