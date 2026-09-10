# AI02 — DPI / VIEWPORT MATRIX

Ngày: 2026-09-11

## Runtime viewport matrix

`ChildUiRuntimeSmoke` đã được mở rộng để render/validate LearnerShell ở 4 viewport bắt buộc cho Home, Typing Space, Math World, Rescue Map và Lesson Play.

| Logical viewport | Profile mong đợi | Runtime bounds/layout | Offscreen PNG |
|---|---|---|---|
| 900x640 | Compact | PASS | PASS |
| 1024x768 | Standard | PASS | PASS |
| 1180x760 | Standard | PASS | PASS |
| 1366x768 | Wide | PASS | PASS |

Home có assertion riêng cho transition `Compact -> Standard -> Wide`.

## DPI resolver matrix

Test mới gọi trực tiếp `LearnerLayoutProfileResolver.Resolve(physicalSize, dpi)` để bảo đảm cùng logical viewport không bị chọn nhầm profile khi Windows scale.

| DPI | Scale | Physical size | Logical tương đương | Profile | Kết quả |
|---|---:|---:|---:|---|---|
| 120 | 125% | 1125x800 | 900x640 | Compact | PASS |
| 120 | 125% | 1280x960 | 1024x768 | Standard | PASS |
| 120 | 125% | 1475x950 | 1180x760 | Standard | PASS |
| 120 | 125% | 1708x960 | ~1366x768 | Wide | PASS |
| 144 | 150% | 1350x960 | 900x640 | Compact | PASS |
| 144 | 150% | 1770x1140 | 1180x760 | Standard | PASS |

## Client-area minimum-size regression

`ChildWindowSizing` trước đây gán trực tiếp `form.MinimumSize = minimumClientSize`, làm titlebar/border lấy mất một phần client area. Regression mới tạo Form thật, áp `minimumClientSize=900x640`, đặt `Size=MinimumSize` và assert `ClientSize >= 900x640`.

Kết quả: PASS.

`DpiChanged` cũng tính lại outer minimum dựa trên non-client chrome mới.

## Text / icon / resize matrix

- Chuỗi dài: `Nhiệm vụ hôm nay đang cần con luyện lại phép trừ có nhớ` render trên `ChildActionButton` 300x64: PASS, mặc định không ellipsis.
- Icon asset `icon_reward.png` render ở 24x24, 32x32, 48x48, 96x96: PASS.
- `ChildArtTextCardLayout` resize 180x120, 320x160, 900x300: PASS, không negative bounds, `ArtHost.Right <= ContentHost.Left + 1`.
- `DrawContain` source/runtime contract: có `SetClip(bounds)` và restore Graphics state: PASS.

## Native DPI note

Máy test hiện có một monitor 1920x1080 và phiên UI capture đang ở DPI 96 / 100%. Vì 10 AI đang chạy song song trên cùng Windows session, AI02 không thay global Display Scale sang 125%/150% để tránh phá trạng thái desktop của các AI khác.

Do đó:

- Các PNG 900/1024/1180/1366 là visual capture thật ở DPI 96.
- Các case DPI 120/144 là regression ở resolver + sizing primitives, không giả vờ là native-monitor screenshot.
- Tên capture legacy `*_125pct_window` đã có từ trước; nó là physical-size harness và **không** được dùng làm bằng chứng rằng `DeviceDpi` thật đã thành 120.

## Gate kết quả

- Release `WAHUKidsLearn.csproj` rebuild x86: PASS.
- Debug `ChildUiRuntimeSmoke` build x86: PASS (bao gồm DEBUG diagnostics).
- `ChildUiRuntimeSmoke`: PASS, 10,279 assertions khi capture.
- Official `Capture-ChildUiOffscreen.ps1`: PASS, 39 PNG.
- `TypingSpaceFormRuntimeSmoke`: PASS, 91 assertions.
- `git diff --check`: PASS.
