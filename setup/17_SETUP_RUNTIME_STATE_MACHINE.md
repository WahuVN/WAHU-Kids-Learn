# 17 — SETUP & RUNTIME STATE MACHINE

Cập nhật: 2026-09-06

## 1. Installer state machine

```text
SETUP_START
  ↓
OS_SUPPORTED?
  ├─ no → BLOCK_UNSUPPORTED_OS
  └─ yes
       ↓
LEGACY_SHA2/SIGNATURE_READINESS?
  ├─ fail production self-test → BLOCK/REPAIR_GUIDANCE
  ├─ warn/unknown → RECORD_DIAGNOSTIC_AND_CONTINUE_PREFLIGHT
  └─ pass
       ↓
NET48_PRESENT?
  ├─ yes → INSTALL_FILES
  └─ no
       ↓
BUNDLED_NET48?
  ├─ no → BLOCK_WITH_OFFLINE_PREREQ_GUIDANCE
  └─ yes → ELEVATE_AND_INSTALL_NET48
                ↓
             REBOOT_REQUIRED?
                ├─ yes → SETUP_RESTART_REQUIRED
                └─ no → VERIFY_NET48
                             ├─ fail → BLOCK_PREREQ_FAILED
                             └─ pass → INSTALL_FILES
```

`INSTALL_FILES` không xóa learner data.

## 2. App startup state machine

```text
PROCESS_START
  ↓
PATHS_WRITABLE?
  ├─ no → RECOVERY_PATH_ERROR
  └─ yes
       ↓
LOAD_CONFIG
  ├─ invalid safety-critical → RECOVERY_CONFIG_ERROR
  └─ pass
       ↓
DB_OPEN/MIGRATE
  ├─ fail → DB_RECOVERY
  └─ pass
       ↓
VERIFY_ACTIVE_CONTENT
  ├─ none valid → PARENT_CONTENT_RECOVERY
  └─ pass
       ↓
HARDWARE_PROFILE_EXISTS + CURRENT?
  ├─ no → BENCHMARK
  └─ yes → USE_PROFILE
       ↓
AUDIO_PROBE
       ↓
UNCLEAN_PREVIOUS_SESSION?
  ├─ yes → MARK/RECOVER_SESSION
  └─ no
       ↓
FIRST_CHILD_CALIBRATION_NEEDED?
  ├─ yes → CALIBRATION
  └─ no → CHILD_HOME
```

## 3. DB recovery state machine

```text
DB_OPEN_FAIL
  ↓
STOP_NORMAL_WRITES
  ↓
PRESERVE_ORIGINAL
  ↓
FIND_VERIFIED_BACKUP
  ├─ none → PARENT_MANUAL_RECOVERY
  └─ found
       ↓
RESTORE_TO_TEMP
       ↓
SQLITE_OPEN + INTEGRITY + SCHEMA
  ├─ fail → TRY_OLDER_BACKUP / MANUAL
  └─ pass
       ↓
ATOMIC_REPLACE
       ↓
REOPEN
       ↓
RECOVERY_SUCCESS
```

Không restore đè original trực tiếp.

## 4. Content import state machine

```text
PARENT_SELECT_PACK
  ↓
COPY_TO_TEMP
  ↓
ARCHIVE_LIMITS/PATH SAFETY
  ├─ fail → QUARANTINE/REJECT
  └─ pass
       ↓
EXTRACT_TEMP
       ↓
SCHEMA + HASH + PROVENANCE + CONTENT VALIDATION
  ├─ fail → QUARANTINE/REJECT
  └─ pass
       ↓
PARENT_CONFIRM
  ├─ cancel → DELETE_TEMP
  └─ confirm
       ↓
ATOMIC_INSTALL_VERSION_DIR
       ↓
SMOKE_LOAD
  ├─ fail → DISABLE/ROLLBACK ACTIVE PACK
  └─ pass → ACTIVATE
```

## 5. Runtime performance state machine

```text
NORMAL_PROFILE
  ↓ sustained budget pressure
NORMAL_NO_DECORATIVE
  ↓
REDUCED_IDLE_FPS
  ↓
STOP_OFFSCREEN / FRAME_SKIP
  ↓
TEMP_LOW_MOTION
```

Recovery upward phải có hysteresis; không flip LOW/NORMAL mỗi vài frame.

Input/question commit không nằm trong degradation candidates.

## 6. Learning behavior state machine

Behavior state không phải finite-state psychology diagnosis; đây là reversible operational classification.

```text
READY
 ├─ balanced evidence → FLOW_LIKELY
 ├─ high mastery + underchallenge evidence → BORED_OR_UNDERCHALLENGED
 ├─ rising load/errors → STRAINED
 ├─ repeated error + frustration signals → FRUSTRATED_LIKELY
 └─ cross-skill deterioration → FATIGUED_LIKELY
```

Mọi transition cần rolling evidence + confidence/hysteresis theo `behavior_thresholds_v1.json`.

Actions:
- FLOW → giảm gián đoạn.
- UNDERCHALLENGED → transfer/representation/difficulty, không thêm particle.
- STRAINED → giảm extraneous load.
- FRUSTRATED → worked example/prerequisite repair.
- FATIGUED → protect mastery + break/close.

## 7. Update state machine

```text
PARENT/RUN_NEW_INSTALLER
  ↓
APP_CLOSE_CLEANLY
  ↓
PRE_MIGRATION_BACKUP_IF_NEEDED
  ↓
INSTALL_NEW_BINARY
  ↓
START_NEW_APP
  ↓
SCHEMA/CONTENT COMPATIBILITY CHECK
  ├─ fail → RECOVERY/ROLLBACK GUIDANCE
  └─ pass → READY
```

Không auto-download/replace binary trong background V1.

## 8. Shutdown state machine

```text
EXIT_REQUEST
  ↓
STOP_NEW_QUESTIONS
  ↓
CANCEL_NONESSENTIAL_MOTION
  ↓
FINISH/ROLLBACK ACTIVE DB TRANSACTION
  ↓
MARK_SESSION completed/aborted
  ↓
FLUSH BOUNDED LOGS/METADATA
  ↓
CLOSE AUDIO/IMAGES/DB
  ↓
CLEAN_EXIT_MARKER
  ↓
PROCESS_EXIT
```

Không chờ animation celebration để thoát.