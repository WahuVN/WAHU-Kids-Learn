# AI3 — PARALLEL NOW — UI / RELEASE / INTEGRATION

LANE: AI3
LANE_DONE=YES
BASELINE=e728204
BUILD_SETUP_LOCK=FREE
REQUEST_009_READY=05cdb2a
REQUEST_006_READY=3cf7fc6
RUNTIME_POOL6_READY=68145ea
AI3_UI_RELEASE_READY=e728204

## Kết luận lane

AI3 đã hoàn tất queue no-wait và final cross-lane gate trên cùng clean `main` HEAD `e728204`.

### UI / QA đã đóng

- `82629a8`: adaptive mission text/badge/accessibility derive từ `MathSessionCoordinator.DefaultTargetQuestionCount`, không còn hard-code `8/tám`.
- `09c6551`: pool-size không bị trình bày thành session-size.
- `ef3d35a`: generated `draw_segment_given_length` đi qua UI interaction thật.
- `9c35ce5` + `2bb1285`: valid → corrupt reload fail-closed, không giữ stale state/accessibility.
- `67c350e`: accessibility sweep đủ 67/67 lesson, gồm keyboard và toàn bộ missing prerequisites.
- `f567be8`: Request 009 UI E2E — pool 6 nhưng selected session đúng 3, suspend/resume giữ exact ordered selected IDs và exact open question.
- `3c0cfaa`: Request 006 UI E2E — integer grading giữ raw value; feedback display giữ `answer_unit` qua resume.
- `e728204`: Child UI tương thích runtime bank 402; hard-code `_01/_02` đổi thành bucket-aware; **402/402 answer surfaces render PASS**.

### Release / Request 008 đã đóng

- `109ee5d`: Portable/Installer E2E bắt buộc đủ 3 Math runtime JSON + schema V5.
- `490fd41`: source/staged publish guards bắt buộc cùng payload.
- `73d4c93`: `ContentRuntimeSmoke` đối chiếu Math manifest version với runtime `PackVersion`, loại stale hard-code `1.8.0` nhưng vẫn fail-closed khi lệch contract.
- `e728204`: release manifest ghi đúng Child UI **2267 assertions**.

Artifact dev `0.1.43-dev` build từ clean `e728204`:

- full `Build-SetupArtifacts.ps1 -RequireInstaller`: **15/15 PASS**;
- SetupPreflight **42**, Behavior **15**, LearningSession **800**, Motion **25**, Child UI **2267**, Content **21**, Security **19**, Audio **14**, Performance **13**, Update **33**, SQLite **179**;
- staged payload chứa đủ `verified_templates_v1.json`, `lesson_catalog_v1.json`, `question_bank_v1.json` và migration `001..005`;
- Portable E2E: **PASS**, bootstrap lần 1/2 exit 0, không chạm installed learner data;
- Installer E2E: **PASS**, install/preflight/bootstrap/recovery/reinstall/uninstall; config tamper exit **42** fail-closed; DB hash giữ nguyên qua reinstall; uninstall giữ learner DB/sentinel và gỡ startup registry;
- E2E-owned test data đã được xóa sau test bằng `.wahu-e2e-owned` safety marker.

Hashes:

- Portable SHA256: `210D073E1ED6A7A2AAE12EEFEF9DCC629F22DB6D3A1BEABB5D828524C4428806`.
- Installer SHA256: `A379B31DA947EB5DD42B17C929E2E2CDCFB0E6D9A79007E34085B754FC506F3F`.

## Final common-HEAD gate

Trên clean `e728204`:

- production Math validator: **402/402 valid**, 67 lessons, 134 basic + 134 medium + 134 application;
- `MathContentDataSmoke`: **49/49 PASS**;
- pool-6 publish smoke: **8/8 PASS**; tổng content regression của wave = **57 tests PASS**;
- `MathSessionPersistenceRuntimeSmoke`: `dotnet build` **0 warning / 0 error**, runtime **378 assertions PASS**;
- `git diff --check`: **PASS**;
- AI1 `LANE_DONE=YES`; AI2 `LANE_DONE=YES`; AI3 `LANE_DONE=YES`.

## Ngoài lane Math

Không còn P0/P1 thuộc Math UI/QA/integration theo board 3-AI. Production signing và test trên máy Windows 7 thật vẫn là release-wide gate của toàn ứng dụng, không phải blocker Math lane. Artifact `0.1.43-dev` là unsigned dev artifact, không được coi là production-signed release.
