# WAHU Kids Learn — App học tập thích nghi cho trẻ lớp 2

> Trạng thái: **SETUP DESIGN LOCK V1**
> Nền tảng: Windows 7 SP1 PC cũ, x86, .NET Framework 4.8, WinForms/GDI+, offline-first.

## 1. Tầm nhìn

WAHU Kids Learn không phải app “làm đề rồi chấm điểm”. Sản phẩm là một learning system thích nghi kết hợp thế giới phiêu lưu nhẹ, liên tục ước lượng trẻ đang biết gì, vướng ở đâu, cần ôn gì, đang ở learning state nào và nên nhận kiểu hỗ trợ/thử thách nào.

Mục tiêu:
1. Trẻ chủ động bắt đầu, không cần bị thúc bằng sợ mất thưởng.
2. Duy trì thử thách vừa sức và calm focus.
3. Sai là tín hiệu đổi cách dạy.
4. Ưu tiên hiểu sâu, delayed recall, transfer.
5. Chạy tốt offline trên Win7 cũ.
6. Parent hiểu tiến bộ theo skill/evidence.
7. Không dark pattern/addictive reward loop.

## 2. Đối tượng V1

- Lớp 2.
- Toán + Tiếng Anh.
- Chuột + loa; mic optional.
- Baseline 1024×768, scale 1366×768+.
- Offline 100% cho core learning.

## 3. Setup/runtime đã chốt

```text
Windows 7 SP1+
.NET Framework 4.8
x86 primary
WinForms + GDI+
System.Data.SQLite 2.0.4 + SourceGear.sqlite3 3.53.4
PNG/sprite + procedural instructional drawing
18 FPS LOW / 30 FPS NORMAL motion
Inno Setup per-user
```

Chi tiết và source-of-truth triển khai: `setup/README.md`.

## 4. Cấu trúc tài liệu

### Product/architecture
- `docs/00_PRODUCT_VISION.md`
- `docs/01_CHILD_PSYCHOLOGY_AND_SAFETY.md`
- `docs/02_WIN7_TECH_ARCHITECTURE.md`
- `docs/03_CHILD_UX_UI_SPEC.md`
- `docs/04_ADAPTIVE_LEARNING_ENGINE.md`
- `docs/05_MATH_GRADE2_CURRICULUM.md`
- `docs/06_ENGLISH_GRADE2_CURRICULUM.md`
- `docs/07_GAME_WORLD_REWARD_SYSTEM.md`
- `docs/08_PARENT_MODE.md`
- `docs/09_DATA_MODEL_AND_SQLITE.md`
- `docs/10_CONTENT_SYSTEM.md`
- `docs/11_ROADMAP_TESTING_AND_RELEASE.md`
- `docs/12_PROJECT_STRUCTURE.md`

### Setup/build/runtime
- `setup/README.md`: master setup.
- `setup/00..20`: runtime, installer, first-run, performance, data, content, audio, security, diagnostics, defaults, release, dev environment, directory layout, decision register, implementation checklist, runtime state machine, Win7/SHA-2 signing, motion/DPI/accessibility và SQLite durability hardening.
- `setup/config/*.json`: machine-readable defaults.
- `setup/installer/WAHU_Kids_Learn.iss`: installer skeleton thật.
- `data/schema/001_initial.sql`: learner SQLite schema thật.

### Evidence/source
- `research/`: BGDĐT baseline, learning-science, behavior/motion, source registry.

## 5. 7 lớp hệ thống

| Lớp | Vai trò |
|---|---|
| P1 — Child UX | Trẻ hiểu cách dùng gần như không cần hướng dẫn |
| P2 — Curriculum | Toán/English chia micro-skill, provenance rõ |
| P3 — Adaptive Learning | Mastery, difficulty, error, next-question |
| P4 — Memory | Spacing + retrieval + delayed recall |
| P5 — Behavior/Motivation | Flow, fatigue, frustration, autonomy, feedback |
| P6 — Game World | Quest/world/cosmetic phục vụ progress, không retention dark pattern |
| P7 — Parent Intelligence | Skill state, recommendations, controls, backup |

## 6. Behavior Controller

Operational states:
- `READY`
- `FLOW_LIKELY`
- `BORED_OR_UNDERCHALLENGED`
- `STRAINED`
- `FRUSTRATED_LIKELY`
- `FATIGUED_LIKELY`

Không phải diagnosis tâm lý.

Priority:
`fatigue/safety → frustration prevention → knowledge repair → competence → challenge → autonomy → reward polish`.

## 7. Luồng session gợi ý

```text
Warm start
→ Math focus
→ micro break
→ English focus
→ smart review
→ calm closure
```

Timing là default mềm; BehaviorController có thể điều chỉnh/kết thúc sớm.

## 8. Nguyên tắc bất biến

- No ads/trackers/account requirement.
- No public leaderboard/streak loss/lootbox.
- No live AI content directly to child V1.
- Only `VERIFIED` content can Child-load.
- No punishment for rest/day off.
- Feedback strategy/process, not fixed-trait praise.
- Decorative motion off during reading/listening/thinking.
- Responsiveness > animation.
- Learner data separate from install/content.
- Uninstall preserves learner data by default.
- Parent Mode PIN local.

## 9. North Star

**Stable mastery after spaced/delayed recall and transfer, while the child can sustainably engage with low frustration and without manipulative retention mechanics.**

Không dùng raw time-in-app làm North Star.

## 10. Thứ tự triển khai

1. Locked-stack spike trên Win7 target.
2. Runtime + SQLite + setup/recovery.
3. Learning/Behavior engine.
4. Math vertical slice.
5. English verified vertical slice.
6. Child UX/accessibility/motion.
7. Parent Mode.
8. Game World.
9. Full verified Grade-2 content.
10. Reliability/performance/release gates.

Không làm game trước rồi mới nhét bài học.