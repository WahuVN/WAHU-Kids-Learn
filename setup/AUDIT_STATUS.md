# SETUP AUDIT STATUS

Cập nhật: 2026-09-06

## Phạm vi

Audit này kiểm source, solution, runtime config, SQLite/Data, updater, portable ZIP, Inno installer và các luồng E2E trên workstation hiện tại. Audit **không** thay thế test trên Windows 7 SP1 thật/VM mục tiêu và không cho phép claim production-ready khi chưa có production certificate/signing evidence.

## PASS — repository / solution / toolchain

- Repository: `WahuVN/WAHU-Kids-Learn`, visibility **PUBLIC**, branch `main`.
- `.NET Framework 4.8`, app/runtime x86.
- `WAHUKidsLearn.sln` chỉ còn `Debug|x86` và `Release|x86`; 180 mapping AnyCPU/x64 thừa đã được loại và full solution rebuild PASS.
- Inno Setup 7.1.0 compile PASS.
- Build output/bin/obj/UserData/learner DB không được track vào Git.
- NuGet lock hiện dùng `System.Data.SQLite 2.0.4` + `SourceGear.sqlite3 3.53.4`; không có active dependency `System.Data.SQLite.Core`/`SQLite.Interop.dll`.

## PASS — automated runtime gates

```text
SETUP_PREFLIGHT_SMOKE_PASS      = 42 / 42
BEHAVIOR_RUNTIME_SMOKE_PASS     = 15 / 15
LEARNING_SESSION_SMOKE_PASS     = 92 / 92
MOTION_RUNTIME_SMOKE_PASS       = 25 / 25
CONTENT_RUNTIME_SMOKE_PASS      = 20 / 20
SECURITY_RUNTIME_SMOKE_PASS     = 19 / 19
AUDIO_RUNTIME_SMOKE_PASS        = 14 / 14
PERFORMANCE_RUNTIME_SMOKE_PASS  = 13 / 13
UPDATE_RUNTIME_SMOKE_PASS       = 33 / 33
UPDATE_LIVE_SMOKE_PASS          = 6 / 6 (final feed/version check)
SQLITE_RUNTIME_SMOKE_PASS       = 160 / 160
```

`UPDATE_LIVE_SMOKE` đã tải thật manifest + installer từ GitHub Releases, đi qua HTTPS redirect, UTF-8 BOM handling, size limits, SHA-256 verification và staged-state reload.

## PASS — runtime config / update policy

Runtime load 10 config bắt buộc và fail-closed khi safety-critical config bị sửa.

Update policy hiện khóa:

```text
provider                  = github_releases
repo                      = WahuVN/WAHU-Kids-Learn
channel                   = dev
feed_tag                  = update-dev
manifest                  = https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/update-dev/update-manifest.json
startup_check             = true
normal_check_interval     = 6h
failure_retry             = 1h
auto_download             = true
auto_install_next_start   = true
staged_retention          = 7d
download_temp_retention   = 24h
portable_binary_update    = false
```

Các invariant đã test:
- HTTPS only; wrong origin / HTTP / bad SHA rejected.
- Stable unsigned manifest rejected trước staging.
- Final redirect host phải là GitHub/release-assets host đã allowlist.
- Manual check bypass được throttle; background/manual check được serialize để tránh tải trùng.
- Manifest UTF-8 BOM hợp lệ.
- Update temp/staged/helper cleanup có retention và không follow reparse point ra ngoài update root.
- Portable không tự thay binary.

## PASS — updater helper negative gates

`WAHU.Updater.exe` là WinExe để không bật cửa sổ console khi cập nhật. Exit contract đã test bằng process wait thật:

```text
--self-test                  => exit 0
missing/invalid arguments    => exit 2
installer SHA changed        => exit 1, installer KHÔNG chạy
production unsigned          => exit 1, installer KHÔNG chạy
successful update            => exit 0
```

Helper kiểm lại SHA-256 ngay trước install. Stable production còn chạy Authenticode verification trước Inno.

## PASS — updater apply E2E thật

Đã chạy cross-version bằng chính installed app + staged installer + external helper; ngoài local staging còn có **full live-feed apply** để app cũ tự tải từ GitHub:

```text
0.1.21-dev -> 0.1.22-dev = PASS
0.1.22-dev -> 0.1.23-dev = PASS
0.1.23-dev -> 0.1.24-dev = PASS (local staging)
0.1.23-dev -> 0.1.24-dev = PASS (GitHub live feed -> app download -> apply)
```

Đã test cả hai preference:
- startup cùng Windows **TẮT** trước update -> vẫn TẮT sau update;
- startup cùng Windows **BẬT** trước update -> vẫn BẬT sau update.

Mỗi update E2E xác minh:
1. cài phiên bản cũ bằng Inno;
2. bootstrap learner DB khỏe;
3. stage installer mới qua `UpdateStagingService` + SHA-256;
4. app cũ phát hiện staged update;
5. tạo verified pre-update SQLite backup;
6. app thoát, `WAHU.Updater.exe` chạy ngoài process;
7. helper hash lại installer và chạy Inno silent;
8. app mới được mở với `--post-update`;
9. `updated_to:<version>` được ghi;
10. staged state + installer được cleanup;
11. learner sentinel và learner DB còn nguyên;
12. DB mới vẫn `integrity=ok`, `foreign_key_issues=0`;
13. verified pre-update backup metadata/DB SHA-256 khớp;
14. uninstall phiên bản mới vẫn giữ learner data.

## PASS — installer E2E

Candidate/release gần nhất đã test: `0.1.24-dev`.

```text
install_exit                           = 0
installed_file_count                   = 52
startup_default                        = PASS
preflight_exit                         = 0
bootstrap_exit                         = 0
crash-marker recovery                  = PASS
config tamper network=true             = exit 42 / CONFIG_INVALID
config restore                         = PASS
reinstall                              = PASS
learner DB hash across reinstall       = unchanged
startup disabled preserved reinstall   = PASS
uninstall                              = PASS
app binary removed                     = PASS
learner DB/sentinel preserved          = PASS
startup registry removed uninstall     = PASS
```

Installer `0.1.24-dev` release hash:

```text
SHA-256 = BD83A2567FBDD21D22B63DB2F4E891D93B0D4741C9D78C0B8C632D4CA9A6EA4C
```

## PASS — portable E2E

Candidate/release gần nhất đã test: `0.1.24-dev`.

```text
first_bootstrap_exit      = 0
second_bootstrap_exit     = 0
storage_mode              = PORTABLE
schema_version            = 2
portable DB               = ./UserData/data/learning.db
installed LOCALAPPDATA    = untouched
TEST_RESULT               = PASS
```

Portable ZIP test hash:

```text
SHA-256 = 27BFAB6373E3BE929B89DC351BBCD5FFB2C0D3635C72B866B987FCBA50C69FF9
```

## PASS — release / manifest guards

- `update-manifest.json` version/bytes/SHA-256 khớp installer được build.
- Technical GitHub feed `update-dev` tồn tại và có đúng asset `update-manifest.json`.
- `Publish-GitHubRelease.ps1` parser PASS.
- Publish script từ chối working tree dirty (`REFUSE_RELEASE_DIRTY_TREE`).
- Publish script yêu cầu HEAD local == `origin/main` trước release.
- Dev/stable dùng fixed channel feed riêng, không phụ thuộc `releases/latest`.

- Release `v0.1.24-dev` đã được publish thật; 5 asset versioned tải ngược từ GitHub có SHA/bytes khớp local.
- `update-dev/update-manifest.json` tải ngược từ GitHub khớp `app_version=0.1.24-dev`, installer SHA-256 và bytes.
- Full live updater E2E: installed `0.1.23-dev` tự check/download feed GitHub và apply `0.1.24-dev` PASS.

## PASS — production negative gates

Dev artefact cố ý unsigned:

```text
WAHU.SetupPreflight --production => expected 12, actual 12
SignTool verify installer         => unsigned / exit 1
Updater --production true         => Authenticode reject / exit 1
```

Do đó unsigned dev build không thể đi nhầm đường production.

## PASS — SQLite/Data durability liên quan setup

```text
System.Data.SQLite = 2.0.4
SQLite engine      = 3.53.4
Current schema     = V2
```

Đã có evidence cho migration checksum, pre-migration backup, Online Backup, 5 recent + 4 weekly rotation, restore verified, serialized writes, atomic answer commit, attempt immutability, crash marker và recovery.

## PENDING — bắt buộc test trên target thật/VM

Chưa được claim PASS cho:
- Windows 7 SP1 x86;
- Windows 7 SP1 x64 chạy app x86;
- SHA-2 legacy/updated images;
- SQLite native load trên Win7;
- 2 GB RAM + HDD cũ;
- 1024×768, DPI 96/120;
- audio/no-audio/mic;
- sleep/wake;
- process kill giữa update/backup/session;
- portable USB surprise-removal;
- updater HTTPS/TLS thực tế trên Win7.

## PENDING — production signing

Chưa có production certificate/toolchain secret để chứng minh:
- Authenticode SHA-256;
- RFC3161 SHA-256 timestamp;
- signed installer/uninstaller;
- disconnected Win7 publisher-chain trust.

## Trạng thái release

```text
SETUP_SOURCE_BUILD                   = PASS
SOLUTION_X86_ONLY                    = PASS
RUNTIME_CONFIG_FAIL_CLOSED           = PASS
SQLITE_DATA_DURABILITY               = PASS
PORTABLE_E2E                         = PASS
INSTALLER_E2E                        = PASS
GITHUB_UPDATE_LIVE_DOWNLOAD          = PASS
CROSS_VERSION_UPDATE_APPLY           = PASS
UPDATE_STARTUP_PREF_ON_OFF           = PASS
UPDATE_PRE_BACKUP                    = PASS
UPDATE_STAGING_CLEANUP               = PASS
PRODUCTION_UNSIGNED_REJECTION        = PASS
WIN7_TARGET_SMOKE                    = PENDING_HARDWARE
PRODUCTION_SIGNING                   = PENDING_CERT_TOOLCHAIN
SETUP_PRODUCTION_READY               = NO
```

Setup/update hiện có automated evidence mạnh trên workstation, nhưng `SETUP_PRODUCTION_READY=NO` vẫn là trạng thái đúng cho tới khi đóng Win7 target + signing gates.
