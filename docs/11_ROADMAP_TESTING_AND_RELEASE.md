# 11 — ROADMAP, TESTING & RELEASE

## Phase 0 — Setup/architecture lock

Đã chốt development baseline:
- Win7 SP1;
- net48;
- WinForms/GDI+;
- x86 primary;
- System.Data.SQLite 2.0.4 + SourceGear.sqlite3 3.53.4 x86;
- Inno per-user;
- offline-first.

Source: `setup/README.md`.

## Phase 1 — Locked-stack technical spike trên máy thật

Không benchmark 2–3 framework song song nữa.

Benchmark stack đã khóa:
- render Child UI 1024×768;
- Vietnamese text/DPI;
- input under motion;
- PNG/sprite;
- audio playback;
- SQLite read/write/backup;
- 18/30 FPS motion profiles;
- installer/update/uninstall.

Gate:
- NORMAL nếu target đạt budget;
- LOW nếu render cần hạ;
- chỉ reopen runtime architecture nếu requirement cốt lõi fail sau profile/optimization hợp lý.

## Phase 2 — Core runtime + data

Làm:
- AppShell/routing;
- runtime config;
- hardware profile/autotune;
- SQLite schema/migrations;
- repository/write coordinator;
- session state;
- content loader;
- backup/recovery;
- diagnostics;
- installer/portable build.

## Phase 3 — Learning/Behavior Engine V1

Làm:
- skill graph;
- mastery state;
- question selection;
- hints/scaffolding;
- review scheduler;
- error taxonomy;
- BehaviorController 6 states;
- explainability events;
- motion integration.

Test simulated learner profiles.

## Phase 4 — Math vertical slice

End-to-end:
- place value;
- addition with one carry max;
- one-step word problem.

Bao gồm content validator + instructional representation + DB + behavior repair.

## Phase 5 — English vertical slice

Không hard-code 20 random vocabulary items.

Dùng:
- official verified core như numbers 1–20;
- letter/sound items đã source/map;
- listening activity;
- book-mapped hoặc explicitly supplementary vocabulary.

Audio semantic QA bắt buộc.

## Phase 6 — Child UX & accessibility

Test:
- biết bắt đầu không;
- nghe lại audio;
- hiểu hint/cue;
- target size/misclick;
- 1024×768/125% DPI;
- Normal/Reduced/Minimal motion;
- decorative motion không chạy khi đọc/nghe/suy nghĩ.

## Phase 7 — Parent Mode

- dashboard;
- controls;
- PIN;
- backup/export/restore;
- diagnostics;
- “Hôm nay hỏi con gì?”.

## Phase 8 — Game World

Chỉ sau learning vertical slice stable:
- companion;
- quest;
- world/garden progress;
- cosmetics;
- boss retrieval/transfer.

No lootbox/streak/retention dark pattern.

## Phase 9 — Full Grade 2 verified content

- coverage dashboard;
- representation diversity;
- provenance;
- question generator + validator;
- English book mapping;
- content pack smoke test.

## Phase 10 — Reliability/performance/release

### Test matrix
- Win7 SP1 x86;
- Win7 SP1 x64 chạy x86 app;
- 2/4 GB RAM;
- old integrated GPU;
- HDD slow;
- 1024×768 / 1366×768;
- 125% DPI;
- audio/mic present/missing;
- offline completely.

### Failure tests
- kill process giữa attempt;
- power-loss simulation;
- DB locked/corrupt;
- backup corrupt;
- corrupt/oversized/path-traversal pack;
- missing audio/image;
- disk nearly full;
- wrong pack/schema version;
- duplicate reward source key;
- sleep/wake.

## Automated tests

### Unit
- mastery;
- scheduler;
- behavior state inference;
- error classifier;
- motion policy;
- pack parser/validator;
- repository queries.

### Property/invariant
- mastery/confidence in [0,1];
- one wrong answer cannot define frustration/fatigue;
- fatigue alone cannot reduce mastery;
- reward source_key unique;
- due dates valid;
- generated question unambiguous;
- HOLD content never Child-loadable.

### Simulation bots
- strong;
- average;
- weak prerequisite;
- random guesser;
- slow accurate;
- frustration-prone;
- fatigue-like cross-skill slowdown.

### E2E
`install → launch → profile → quest → answer → hint → behavior/mastery → reward → close → reopen → parent report → backup → update → restore`.

## V1 release gates

### Learning
- no infinite question loop;
- no hinted mastery inflation;
- delayed review works;
- repair path works;
- decision explainability stored.

### UX/Safety
- mouse-first;
- no overflow 1024×768;
- no technical stack trace to child;
- no dark pattern/streak loss;
- reduced motion works.

### Data
- migration pass;
- backup/restore pass;
- crash recovery pass;
- attempt immutability;
- reward idempotency.

### Installer
- clean/update/repair/uninstall pass;
- missing .NET flow pass;
- non-admin per-user pass;
- uninstall preserves learner data.

### Performance
- no input freeze;
- no session memory leak;
- NORMAL reaches target if selected;
- LOW remains usable;
- working set within budget.

## Definition of Done

V1 chỉ Done khi:
1. Trẻ học end-to-end.
2. Adaptive/behavior engine có test evidence.
3. Chỉ verified content đến Child Mode.
4. Data survive restart/crash/update.
5. Parent hiểu trạng thái và backup được.
6. Win7 target chạy ổn.
7. Setup/release/safety gates pass.