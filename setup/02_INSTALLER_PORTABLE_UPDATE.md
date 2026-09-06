# 02 — INSTALLER, PORTABLE, UPDATE & ROLLBACK

Cập nhật: 2026-09-06

## 1. Installer chính

Baseline: **Inno Setup 7.1.x**, build installer 32-bit.

Inno Setup 7.1 hiện công bố hỗ trợ Windows 7; `MinVersion` mặc định/hợp lệ cho mục tiêu là `6.1sp1`.

Nguồn:
- https://jrsoftware.org/isdl.php
- https://jrsoftware.org/ishelp/topic_setup_minversion.htm

Nếu dự án được phân phối thương mại, phải kiểm tra license Inno Setup hiện hành trước khi phát hành.

## 2. Kiểu cài

V1 ưu tiên **per-user install**:

```text
PrivilegesRequired=lowest
DefaultDir={localappdata}\Programs\WAHU Kids Learn
```

Lợi ích:
- không cần admin để update app sau khi .NET đã có;
- tránh ghi Program Files;
- phù hợp máy gia đình cũ.

.NET Framework 4.8 nếu thiếu vẫn cần quyền admin để cài prerequisite.

### Khởi động cùng Windows

Installer có task `startup` **mặc định được chọn**. Task ghi HKCU `Software\Microsoft\Windows\CurrentVersion\Run` với `WAHUKidsLearn.exe --startup`, không cần quyền admin. Uninstall phải xóa value này; installer E2E kiểm cả create/remove. Người lớn có thể bỏ chọn task khi cài nếu không muốn tự khởi động.

## 3. .NET preflight

Check registry release key:

```text
HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\Release
minimum >= 528040
```

Microsoft khuyến nghị check `>=` để forward-compatible.

Nguồn:
https://learn.microsoft.com/en-us/dotnet/framework/install/how-to-determine-which-versions-are-installed

Flow:
1. Có net48+ → tiếp tục.
2. Không có → nếu redistributable offline được bundle hợp lệ, hỏi phụ huynh và chạy installer.
3. Không có bundle → dừng với hướng dẫn rõ, không tải ngầm.
4. Sau prerequisite nếu cần reboot → setup resume an toàn.

## 4. Disk preflight

- Nếu .NET đã có: yêu cầu dung lượng cho app + content + 2× headroom update + backup tối thiểu.
- Nếu cài .NET: Microsoft nêu disk requirement khoảng 4.5 GB cho framework installation; setup phải check thêm headroom.
- Không bắt đầu update nếu free disk dưới ngưỡng an toàn.

## 5. Dữ liệu không nằm trong install dir

Installer chỉ thay binary/assets built-in.

Dữ liệu học ở `%LOCALAPPDATA%\WAHU Kids Learn\` và không bị xóa khi update.

## 6. Portable package

Tạo thêm:

```text
WAHU-Kids-Portable-x86.zip
```

Portable mode:
- không registry bắt buộc;
- data ở `./UserData`;
- hữu ích cho USB, QA, máy không cho cài;
- banner Parent Mode cảnh báo không tháo USB khi app đang ghi;
- không phải mode khuyến nghị cho trẻ dùng hàng ngày.

## 7. Update V1 — GitHub Releases, fail-safe

V1 cho phép **auto-update binary ở installed mode** qua đúng release feed công khai của dự án:

```text
https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/update-dev/update-manifest.json
```

Đây là ngoại lệ network hẹp; Child Mode, analytics, ads, content runtime và learner data vẫn không có network chung.

Flow:
1. startup app bình thường, không chờ mạng;
2. background worker tối đa mỗi 6 giờ thử tải manifest qua HTTPS;
3. manifest phải đúng owner/repo/channel, URL installer đúng GitHub Releases của repo, size nằm trong policy;
4. installer tải vào `%LOCALAPPDATA%\WAHU Kids Learn\updates`, kiểm SHA-256 trước khi stage;
5. lần khởi động kế tiếp, chỉ khi DB bootstrap/integrity khỏe và phiên trước đóng sạch, tạo verified pre-update backup;
6. copy `WAHU.Updater.exe` ra ngoài install dir, app thoát;
7. helper hash lại installer, production còn phải qua Authenticode cache-only, rồi chạy Inno silent;
8. cài thành công thì mở app lại; lỗi update không xóa learner data và không chặn Child Mode ở lần check mạng.

Portable mode **không tự cập nhật binary** để tránh ghi đè ứng dụng trên USB/removable media. Có thể thay portable ZIP thủ công.

Dev channel hiện cho unsigned installer nhưng SHA-256 bắt buộc. Stable channel bị release pipeline từ chối nếu chưa `production_signed=true` và Authenticode gate chưa PASS.

App version và content version độc lập.

## 8. Pre-update transaction

Trước update app có migration:
1. đóng child session sạch;
2. tạo DB backup verified;
3. lưu app_version/content_version/schema_version;
4. update binary;
5. launch migration preflight;
6. nếu migration fail → app không mở Child Mode, cung cấp Restore.

## 9. Rollback

Binary rollback:
- giữ installer release trước trong thư mục release/USB nếu phụ huynh muốn.

Data rollback:
- chỉ restore từ backup trước migration;
- không tự downgrade schema bằng SQL ngược nếu chưa có test.

Content rollback:
- giữ manifest version cũ nếu dung lượng cho phép;
- switch active pack atomically.

## 10. Uninstall

Mặc định:
- xóa app binary/shortcut;
- GIỮ learner data, backup, parent settings.

Wizard có lựa chọn riêng, cảnh báo rõ nếu muốn xóa dữ liệu.

Không dùng checkbox đánh lừa/default bật xóa dữ liệu.

## 11. Repair mode

Repair kiểm:
- binary hash;
- built-in content hash;
- missing DLL;
- SQLite provider;
- config schema;
- asset manifest.

Repair không reset mastery/attempt history.

## 12. Legacy Win7 compatibility readiness

Installer minimum vẫn là `6.1sp1`, nhưng release QA không coi SP1 là đủ evidence.

Microsoft yêu cầu nền SHA-2 cho Win7 SP1 cũ với KB4490628 + KB4474419 và reboot trước chuỗi update hiện đại. V1 dùng các KB như diagnostic evidence, không hard-code exact-KB absence thành fail vĩnh viễn vì servicing state có thể bị supersede.

Production readiness ưu tiên:
1. OS/SP1;
2. net48 Release key;
3. SHA-2 readiness evidence;
4. signed-binary/self-test;
5. target smoke.

Không tự tải/cài Windows Update từ installer.

## 13. Production signing

Release production nên dùng Authenticode SHA-256 + RFC3161 timestamp SHA-256. Inno Setup dùng configured `SignTool`/signed-uninstaller khi signing environment sẵn sàng.

Private key/password không được nằm trong repo hoặc `.iss`.

`.issig` nếu dùng không thay thế Authenticode publisher identity.

Chi tiết: `18_WIN7_COMPATIBILITY_SIGNING.md`.