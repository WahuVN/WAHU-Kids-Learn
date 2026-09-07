# AI3 — PARALLEL NOW — UI/RELEASE/INTEGRATION

LANE: AI3
LANE_DONE=NO
BASELINE=d1665dc
BUILD_SETUP_LOCK=WAIT_AI2
REQUEST_009_READY=WAIT

## Mission now

Own UI, Child UI integration and release-E2E surfaces only. Never edit Learning/Session engine files or Math runtime content/manifest.

### A. Immediate UI work — no dependency
Fix adaptive mission target coupling:
- remove literal `Luyện 8 câu hôm nay`; derive display from `MathSessionCoordinator.DefaultTargetQuestionCount`;
- remove/derive any literal badge `8`;
- accessibility text must derive from the same constant and not hard-code `tám`;
- add Child UI regression using reflection/current constant so a future engine target change cannot make UI lie.

Commit this as an isolated UI wave.

### B. Request 008 release tests — start now
In `Test-PortableE2E.ps1` and `Test-InstallerE2E.ps1`, require all three Math runtime files:
- `content_packs\math_grade2_v1\verified_templates_v1.json`
- `content_packs\math_grade2_v1\lesson_catalog_v1.json`
- `content_packs\math_grade2_v1\question_bank_v1.json`
Keep schema expectation at V5. Commit these two files independently if tests pass.

Do not touch `Build-SetupArtifacts.ps1` while `BUILD_SETUP_LOCK=WAIT_AI2`.

### C. Request 008 staging after handoff
When `AI2_PARALLEL_NOW.md` says `BUILD_SETUP_LOCK=AI3`, add preflight/staged publish `Require-File` guards for the same three JSON in `Build-SetupArtifacts.ps1`. Then run staged payload + portable + installer E2E.

### D. After Request 009
When `AI2_PARALLEL_NOW.md` publishes `REQUEST_009_READY=<sha>` and AI1 merges real pool=6:
- add real-bank E2E: Hub detail says 6-bank count, session target/progress/result remain 3;
- different fresh selection identities may produce different selected sets;
- exact resume/retry keeps the same selected set.
Do not reimplement selection in UI.

### E. After Request 006
Add presentation E2E for integer answers with display units if engine model exposes it. Raw child input remains number-only.

### F. Status cleanup
Update stale V5 text: `ece2a0c` is stable main, so old `NOT STABLE` wording must be removed after active functional waves are committed.

### Commit discipline
Use exact file commits only. Never stage engine/content files. Do not edit shared request/contract docs during the parallel run; publish progress in this file and AI3-owned status docs.
