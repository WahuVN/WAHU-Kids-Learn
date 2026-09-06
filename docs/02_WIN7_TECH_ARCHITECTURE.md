# 02 — WINDOWS 7 TECH ARCHITECTURE

## 1. Mục tiêu kỹ thuật

- Chạy ổn trên Windows 7 SP1 PC cũ.
- Offline-first.
- Startup nhanh.
- Không phụ thuộc browser hiện đại.
- RAM thấp.
- GPU optional.
- Crash-safe.
- Dữ liệu học không mất khi mất điện/crash hợp lý.

## 2. Runtime V1 — DEVELOPMENT LOCK

```text
C#
.NET Framework 4.8
WinForms
System.Drawing / GDI+
x86 primary
Windows 7 SP1 minimum
```

V1 không còn chạy spike C++/Qt/SDL2 song song. Lý do:
- net48 là dòng .NET Framework cuối hỗ trợ Win7;
- WinForms/GDI+ đủ cho UI 1024×768 và motion nhẹ;
- x86 chạy được cả Win7 32-bit và 64-bit;
- một native SQLite architecture giúp giảm lỗi deployment;
- memory target <=250 MB không cần x64.

Machine benchmark vẫn là **release gate**. Chỉ reopen runtime decision nếu target PC fail requirement cốt lõi sau profile/optimization, không chỉ vì một microbenchmark.

Setup source-of-truth: `setup/README.md`.

## 3. Module architecture

```text
AppShell
├── ChildUI
├── ParentUI
├── SessionOrchestrator
│
├── LearningCore
│   ├── MasteryEngine
│   ├── DifficultyEngine
│   ├── ReviewScheduler
│   ├── ErrorClassifier
│   ├── BehaviorController
│   └── RecommendationEngine
│
├── MotionRuntime
│   ├── MotionScheduler
│   ├── WahuTween
│   ├── SpriteSheetPlayer
│   └── RenderBudgetMonitor
│
├── Curriculum/ContentRuntime
│   ├── Math
│   └── English
│
├── GameWorld
│   ├── QuestEngine
│   ├── RewardEngine
│   └── CosmeticInventory
│
├── AudioRuntime
├── LocalStorage
│   ├── System.Data.SQLite
│   ├── Backup
│   └── Migration
└── Diagnostics
```

## 4. Dependency policy

Required V1:
- `System.Data.SQLite 2.0.4` + `SourceGear.sqlite3 3.53.4` x86 (`e_sqlite3.dll`).

Không dùng EF/ORM nặng.

Ưu tiên internal/BCL:
- tween/scheduler;
- sprite playback;
- WAV SFX;
- hashing/import helpers.

Không core:
- Electron/Chromium/WebView2;
- Skia/Lottie;
- game engine chỉ để làm UI.

Static SVG được render thành PNG lúc build khi có thể.

## 5. Performance budgets V1

Engineering targets:
- Idle RAM <=150 MB, ưu tiên <=100 MB.
- Normal lesson RAM <=250 MB.
- CPU idle thấp.
- NORMAL motion cap 30 FPS.
- LOW motion cap 18 FPS nhưng input vẫn tức thì.
- Input responsiveness ưu tiên trước animation.
- Cold startup target <5 giây trên HDD cũ là mục tiêu đo, không release claim trước benchmark.

## 6. Autotune

Chỉ LOW/NORMAL.

Khi áp lực render/memory tăng:

```text
DECORATIVE off
→ reduce idle FPS
→ stop offscreen motion
→ skip tween intermediate frames
→ evict derived cache
→ temporary LOW-motion
```

Không được trì hoãn answer commit/pause/exit.

## 7. Asset strategy

- Verified SVG/procedural là source instructional.
- Static instructional → PNG build-time.
- Dynamic instructional → GDI+.
- Character/world → sprite sheet PNG.
- Lazy-load theo lesson/scene.
- LRU cache có byte ceiling.
- Không video background.

## 8. SQLite/data

Static curriculum/question packs nằm ngoài learner DB.

Learner DB chỉ giữ dynamic state/events.

- provider official System.Data.SQLite;
- parameterized SQL;
- attempts immutable;
- one write coordinator;
- transaction theo logical answer event;
- reward idempotent source key;
- WAL conditional theo test;
- migrations versioned + pre-migration backup.

Schema thật: `data/schema/001_initial.sql`.

## 9. Crash recovery

Session states:
- `started`;
- `active`;
- `completed`;
- `aborted`;
- `recovered`.

Attempt đã commit giữ nguyên.

Sau crash:
- không bắt làm lại toàn session;
- resume/micro-session mới;
- no duplicate reward;
- DB corruption đi qua recovery temp + integrity + atomic replace.

## 10. Update/install

V1:
- Inno Setup 7.1.x, x86, per-user;
- app under `%LOCALAPPDATA%\Programs`;
- learner data separate under `%LOCALAPPDATA%\WAHU Kids Learn`;
- offline installer/USB;
- no Internet auto-update;
- uninstall preserves data by default.

## 11. Security/privacy

- no listening port;
- no analytics/ads;
- Parent PIN hashed/salted;
- content import path/size/hash/schema validation;
- no executable inside lesson pack;
- logs sanitized/local.

## 12. Audio

- voice priority;
- SFX calm;
- music OFF default;
- PCM WAV baseline;
- mic optional/local record-replay;
- no fake AI pronunciation score.

## 13. Hardware release gate

Ghi/test:
- Win7 SP1 x86/x64;
- CPU/RAM/GPU;
- 1024×768/1366×768/DPI;
- HDD/SSD;
- audio/mic;
- available disk;
- render/input/SQLite performance.

Chi tiết đầy đủ ở `setup/`.