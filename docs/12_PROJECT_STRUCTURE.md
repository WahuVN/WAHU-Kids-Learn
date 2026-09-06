# 12 — PROJECT STRUCTURE — V1 LOCKED BASELINE

Cập nhật: 2026-09-06

```text
D:\APP HOC TAP\
├── README.md
├── docs\                         # product / architecture specs
├── research\                     # nguồn, evidence, psychology, motion research
├── setup\                        # SOURCE OF TRUTH cho build/install/runtime setup
│   ├── README.md
│   ├── 00_WIN7_LIGHTWEIGHT_SETUP.md
│   ├── 01_RUNTIME_DEPENDENCY_LOCK.md
│   ├── ...
│   ├── 17_SETUP_RUNTIME_STATE_MACHINE.md
│   ├── 18_WIN7_COMPATIBILITY_SIGNING.md
│   ├── 19_MOTION_DPI_ACCESSIBILITY_ARCHITECTURE.md
│   ├── 20_SQLITE_DURABILITY_AND_BACKUP.md
│   ├── config\
│   └── installer\
│
├── app\                          # UI/orchestration, sẽ code
│   ├── shell\
│   ├── child_ui\
│   ├── parent_ui\
│   └── diagnostics\
│
├── core\
│   ├── learning\
│   │   ├── mastery\
│   │   ├── difficulty\
│   │   ├── scheduler\
│   │   ├── error_classifier\
│   │   ├── behavior\             # policy + thresholds đã có
│   │   └── recommendation\
│   ├── motion\                   # policy đã có; runtime sẽ code
│   ├── session\
│   └── content\
│
├── curriculum\
│   ├── math_grade2\             # BGDĐT baseline JSON đã có
│   └── english_grade2\          # BGDĐT baseline JSON đã có
│
├── game\
│   ├── world\
│   ├── quest\
│   ├── reward\
│   └── inventory\
│
├── data\
│   ├── schema\                   # 001_initial.sql đã có
│   ├── migrations\
│   └── seed\
│
├── assets\
│   ├── verified_vectors\        # source instructional hiện có
│   ├── child_ui\
│   ├── companions\
│   ├── audio\
│   └── low_spec\
│
├── content_packs\
│   ├── math_grade2_v1\
│   └── english_grade2_v1\
│
├── tools\
│   ├── content_studio\
│   ├── pack_builder\
│   ├── pack_validator\
│   ├── asset_builder\
│   ├── setup_preflight\
│   └── learner_simulator\
│
├── tests\
│   ├── unit\
│   ├── property\
│   ├── simulation\
│   ├── integration\
│   ├── installer\
│   └── e2e\
│
└── build\
    ├── win7_x86\                # primary V1 output
    │   └── publish\
    └── installer\
```

`win7_x64` không còn là output mặc định. Chỉ tạo nếu benchmark sau này chứng minh có lợi rõ.

## Ownership boundaries

### `setup/`
Quyết định runtime, dependency, installer, first-run, performance, storage, backup, security, logging và release. Không được duplicate magic constants ở source nếu đã có config machine-readable.

### `app/`
Presentation + orchestration. Không đặt mastery/behavior logic trong button handlers.

### `core/learning/`
Domain logic deterministic/testable. Behavior labels là operational state, không diagnosis.

### `core/motion/`
MotionScheduler/WahuTween/RenderBudgetMonitor phải tuân `motion_policy_v1.json`. Motion không tự kéo dài session.

### `curriculum/`
Skill/program baseline và mapping; không UI.

### `game/`
Consume learning events. Không thay mastery và không chọn câu chỉ để tăng reward/time-in-app.

### `data/`
Learner data động, schema/migration/repository. Static question bank không nằm trong `learning.db`.

### `content_packs/`
Versioned, immutable per version, Child Runtime chỉ nạp `VERIFIED`.

### `tools/`
Các tool build/validate chạy trên máy dev; được phép nặng hơn child runtime miễn artifact đầu ra deterministic và provenance rõ.

## Dependency direction

```text
ChildUI / ParentUI
        ↓
SessionOrchestrator
        ↓
LearningCore ─────→ BehaviorController
     ↓                    ↓
Content/Curriculum      MotionPolicy
     ↓                    ↓
DataRepository         MotionRuntime

LearningEvent ─────→ GameWorld
LearningState ─────→ ParentUI
```

Không cho dependency ngược:
- GameWorld → Mastery mutation: CẤM.
- UI state → learner truth: CẤM.
- Motion → question selection: CẤM.
- Content pack → executable code: CẤM.

## Coding principles

- x86/net48 V1.
- Deterministic core khi cùng input/config/seed.
- Config/schema/version pin rõ.
- No magic threshold rải rác.
- Parameterized SQL.
- Attempts immutable.
- Reward idempotent.
- Event explainability cho adaptive decisions.
- Dispose GDI resources rõ.
- One MotionScheduler, no Timer per control.
- No blocking I/O trên UI thread.
- Performance gate từ vertical slice đầu tiên.
- Backup/recovery test trước khi full content.
- Installer/update/uninstall là một phần Definition of Done.