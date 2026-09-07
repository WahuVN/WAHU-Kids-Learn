# AI3 — PARALLEL NOW — UI/RELEASE/INTEGRATION

LANE: AI3
LANE_DONE=NO
BASELINE=3d8674d
BUILD_SETUP_LOCK=AI3
REQUEST_009_READY=NO

## NO-WAIT rule
AI3 **không chờ AI2/AI1**. Dependency chỉ là publish gate; khi một gate chưa mở hoặc upstream đang đỏ, AI3 chuyển ngay sang UI/ChildUI/release/status QA độc lập.

### Queue luôn có việc
- DONE `82629a8`: adaptive mission text/badge/accessibility derive từ `MathSessionCoordinator.DefaultTargetQuestionCount`; Child UI reflection guard PASS.
- DONE `109ee5d`: Portable/Installer E2E hard-require đủ 3 Math runtime JSON và vẫn expect schema V5.
- DONE `09c6551`: synthetic pool-6 UI giữ bank count 6 nhưng CTA/badge/accessibility không invent session-size 6.
- DONE `490fd41`: `Build-SetupArtifacts.ps1` source + staged payload hard-require `verified_templates_v1.json`, `lesson_catalog_v1.json`, `question_bank_v1.json` cùng migration V5.
- DONE `ef3d35a`: Child UI dùng output thật của generated `draw_segment_given_length`; interaction kind/no-fake-choice/segment geometry/form routing PASS, Child UI 1619 assertions.
- NOW: V5 stable status cleanup + accessibility/no-stale-state/error-state sweeps.
- NEXT: Request 009 synthetic/real selected-set presentation khi AI2 publish `REQUEST_009_READY=<sha>`; không reimplement selection ở UI.
- NEXT: Request 006 display-unit presentation khi engine model publish contract.
- FALLBACK: 67-lesson render/access sweep, retry/resume/write-failure regression, no-hard-code reflection guards.

## Current release evidence

Request 008 guard implementation đã vào `main`:
- E2E required lists: `109ee5d`.
- Build source/staged guards: `490fd41`.

Artifact cũ `0.1.41-dev` là negative evidence: có `verified_templates_v1.json` nhưng thiếu `lesson_catalog_v1.json`, `question_bank_v1.json` và migration V5; test mới bắt đúng lỗ hổng này.

Full artifact attempt `0.1.42-dev` trên detached `d21a665` + Request 008 producer guards dừng **trước staging** tại stable `ContentRuntimeSmoke` với `ASSERT_FAIL: bundled_versions_explicit`: Math manifest đã là `1.9.0` nhưng smoke vẫn hard-code `1.8.0`. Trước điểm đó SetupPreflight **42**, Behavior **15**, LearningSession **800**, Motion **25**, Child UI **1617** đều PASS. Đây là upstream release/content-version regression, không phải Request 008 guard failure; AI3 không sửa file ContentRuntimeSmoke ngoài ownership.

## Current UI evidence

- Adaptive mission count: clean production rebuild PASS; Child UI **1615**; content data **49/49** tại wave `82629a8`.
- Synthetic pool-6 accessibility: Child UI **1617** tại `09c6551`.
- Stable generated segment UI integration: Child UI **1619** tại `ef3d35a`.
- V5/pack identity đã stable trên `main` từ `ece2a0c`; không còn trạng thái `NOT STABLE`.

## Pending publish gates

### Request 009
AI2 vẫn `REQUEST_009_READY=NO`. AI3 đã khóa UI synthetic pool-6; khi selected-set contract publish, thêm E2E session/progress/result theo target thật và exact resume giữ selected IDs.

### Request 006
Khi engine expose display-only `answer_unit`, thêm presentation E2E; raw integer input vẫn number-only và UI không tự ghép unit để chấm.

### Release rerun
Sau khi owner upstream sửa `ContentRuntimeSmoke` version expectation cho Math pack `1.9.0`, chạy lại Build-Setup `0.1.42+`, Portable E2E, Installer E2E/reinstall và xác nhận learner DB + lesson progress không mất.

## Commit discipline
Use exact file commits only. Never stage engine/content files. Không sửa shared request/contract docs trong parallel run; progress chỉ ghi ở AI3-owned status docs.
