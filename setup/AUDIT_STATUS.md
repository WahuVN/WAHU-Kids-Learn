# SETUP AUDIT STATUS

Cập nhật: 2026-09-06

## Scope

Audit này kiểm **source + setup + build pipeline + runtime config + SQLite/Data + portable ZIP + installer E2E trên workstation hiện tại**. Audit không thay thế smoke-test trên Windows 7 thật/VM mục tiêu và không cho phép claim production-ready khi chưa có signing/certificate/target evidence.

## PASS — source/repository

```text
TRACKED_SOURCE_FILES_AFTER_AUDIT_COMMIT = 133
SOURCE_JSON_FILES                     = 25
PACKAGE_LOCKS                         = 2
BUILD/BIN/OBJ/USERDATA                = IGNORED
```

NuGet lock:

```text
System.Data.SQLite = 2.0.4
SourceGear.sqlite3 = 3.53.4
```

Không còn active lock `System.Data.SQLite.Core 1.0.119` hay `SQLite.Interop.dll`.

GitHub repository:

```text
WahuVN/WAHU-Kids-Learn
visibility = PRIVATE
branch     = main
```

Lịch sử được chia theo công đoạn riêng: repo hygiene → research/spec → content/policy → setup spec → runtime config → platform/preflight → data/SQLite → app/bootstrap → build/release → audit.

## PASS — solution/build

- `WAHUKidsLearn.sln`, `.NET Framework 4.8`, x86.
- Full `/restore + rebuild` PASS.
- NuGet lock-file restore PASS.
- App manifest có Win7 supportedOS + system-DPI-aware.
- Runtime build identity được inject qua `config/install_manifest_v1.json`.
- Runtime hiện load **9 config bắt buộc** và fail-closed nếu safety-critical config sai.

## PASS — Platform/preflight

```text
SETUP_PREFLIGHT_SMOKE_PASS assertions=35
```

Đã test:
- Win7 RTM reject / Win7 SP1 accept classifier;
- .NET Framework 4.8 Release boundary;
- SHA-256 known vector;
- real OS/net48 probe;
- unsigned signature detection;
- named mutex: primary/secondary/reacquire;
- runtime marker: clean create/remove + stale marker recovery;
- runtime config schema/invariant validation;
- installed/portable path isolation;
- portable path traversal reject;
- build identity config.

## PASS — SQLite/Data runtime

Development lock:

```text
System.Data.SQLite 2.0.4
SourceGear.sqlite3 3.53.4
e_sqlite3.dll x86
SQLite engine 3.53.4
Current learner schema V2
```

```text
SQLITE_RUNTIME_SMOKE_PASS assertions=135
```

Đã test:
- schema V1 bootstrap + migration V2;
- migration SHA-256/history validation;
- migration V1/V2 tamper reject;
- existing V1 DB **không được migrate nếu thiếu backup context**;
- verified pre-migration schema-V1 backup trước khi V1 → V2;
- DELETE + WAL provider smoke;
- foreign keys + bounded busy timeout;
- serialized write coordinator, 12 concurrent writers với `maxActive=1`;
- `AnswerCommitService` commit attempt + error/mastery + child_skill + review trong một transaction;
- late CHECK-constraint failure rollback toàn bộ logical answer batch;
- reward `source_key` idempotency;
- attempt immutability trigger chặn `UPDATE attempt`;
- correction append qua `attempt_correction_event`;
- SQLite Online Backup API;
- backup reopen + integrity/FK checks;
- managed metadata + SHA-256 verification;
- 8-week rotation giữ đúng 5 recent + 4 weekly;
- pre-migration backup không bị rotation xóa;
- metadata/hash tamper reject;
- verified restore + preserve original target DB.

## PASS — latest dev build 0.1.7

Build command chạy sạch toàn pipeline và Inno Setup 7.1.0 compile thành công.

Latest installer:

```text
build\installer\WAHU-Kids-Learn-Setup-win7-x86-0.1.7-dev.exe
SHA256 = 7FBA8032FC39A8890215BEF400F54A8FB13095D509DDD1D7B44BE90F90FDE7EB
```

Latest portable ZIP:

```text
build\portable\WAHU-Kids-Learn-Portable-win7-x86-0.1.7-dev.zip
SHA256 = AD955822030C99178BD4A5061A57624C748305B428E608B434AFE828ABF2838B
```

Generated artefacts được `.gitignore` loại khỏi repository.

## PASS — Portable ZIP E2E

```text
first_bootstrap_exit       = 0
second_bootstrap_exit      = 0
storage_mode               = PORTABLE
schema_version             = 2
migration_version          = 2
portable_db_bytes          = 208896
installed_root_before      = False
installed_db_before/after  = none
TEST_RESULT                = PASS
```

Đã xác minh:
- test chạy từ **ZIP đã giải nén**, không dựa staging folder;
- `portable.mode` được nhận đúng;
- data tạo dưới `./UserData`;
- second boot không recreate schema;
- portable không tạo/sửa `%LOCALAPPDATA%\WAHU Kids Learn`;
- app version trong runtime config khớp `0.1.7-dev`.

## PASS — Installer E2E

```text
install_exit                    = 0
installed_file_count            = 41
preflight_exit                  = 0
bootstrap_exit                  = 0
recovery_bootstrap_exit         = 0
config_tamper_exit              = 42
config_restored_bootstrap_exit  = 0
reinstall_exit                  = 0
uninstall_exit                  = 0
app_removed_after_uninstall     = True
db_preserved_after_uninstall    = True
sentinel_preserved              = True
TEST_RESULT                     = PASS
```

Đã xác minh:
- installed payload có provider/native DLL và cả migration `001 + 002`;
- app bootstrap tạo learner DB schema V2;
- clean boot không để stale marker;
- simulated stale crash marker được phát hiện và dọn;
- safety config tamper `network=true` → exit 42 / `CONFIG_INVALID` trước khi DB bị thay đổi;
- restore config → bootstrap sạch;
- learner DB hash không đổi qua reinstall;
- uninstall xóa app nhưng giữ learner data;
- injected runtime app version khớp build version.

## PASS — production unsigned negative gate

Dev binary cố ý unsigned:

```text
WAHU.SetupPreflight --production
expected exit = 12
actual exit   = 12
GATE          = PASS
```

Production gate không cho unsigned build giả làm production.

## PENDING — chưa được phép claim PASS

### Windows 7 target evidence

Chưa chạy trên target thật/VM đã khóa:
- Win7 SP1 x86;
- Win7 SP1 x64 chạy x86 app;
- clean/legacy SHA-2 update states;
- `System.Data.SQLite 2.0.4 + e_sqlite3.dll` native load trên Win7;
- 2 GB RAM + HDD cũ;
- 1024×768;
- 96/120 DPI;
- audio/no-audio/mic;
- sleep/wake;
- process-kill giữa session/backup/migration;
- WAL filesystem/crash benchmark;
- portable USB surprise-removal test.

### Production signing

Chưa có production certificate/toolchain secret:
- Authenticode SHA-256;
- RFC3161 SHA-256 timestamp;
- signed installer/uninstaller;
- disconnected Win7 certificate-chain trust test.

### Product/runtime subsystem còn thiếu

- global exception/recovery UI cho Child Mode;
- runtime override + config migration layer;
- secure content-pack parser/import adversarial implementation;
- asset render/cache pipeline;
- MotionScheduler/WahuTween/SpriteSheet runtime;
- BehaviorController runtime;
- audio runtime;
- full Child/Parent learning UI;
- Parent Mode PIN/backup UI;
- performance autotune/leak/target hardware gates.

## Release status

```text
SETUP_DESIGN_LOCK                  = PASS
SOURCE_CONFIG_VALIDATION           = PASS
NET48_X86_BUILD                    = PASS
PREFLIGHT_PLATFORM_SMOKE           = PASS_35_ASSERTIONS
RUNTIME_CONFIG_FAIL_CLOSED         = PASS
SQLITE_DATA_RUNTIME_SMOKE          = PASS_135_ASSERTIONS
SQLITE_SCHEMA_V2_MIGRATION         = PASS_DEV_WORKSTATION
PRE_MIGRATION_BACKUP               = PASS
BACKUP_ROTATION_5_PLUS_4           = PASS
ANSWER_TRANSACTION_ATOMICITY       = PASS
ATTEMPT_IMMUTABILITY               = PASS
SINGLE_INSTANCE_RUNTIME            = PASS
UNCLEAN_SESSION_MARKER             = PASS
PORTABLE_ISOLATION_E2E             = PASS
INNO_SETUP_COMPILE                 = PASS
INSTALLER_E2E_CURRENT_WORKSTATION  = PASS
PRODUCTION_UNSIGNED_REJECTION      = PASS
WIN7_TARGET_SMOKE                  = PENDING_HARDWARE
PRODUCTION_SIGNING                 = PENDING_CERT_TOOLCHAIN
V1_RELEASE_READY                   = NO
```

`V1_RELEASE_READY=NO` là trạng thái đúng: setup/data/deployment nền đã có regression evidence mạnh, nhưng chưa đủ target Win7, signing và các subsystem học tập/UI để phát hành V1.