# MATH LIVE STATUS

Updated: 2026-09-07
LAST_FULLY_VERIFIED_HEAD=`1c4ce9e`
MATH_3AI_DONE=YES
AI3_LANE_DONE=YES
PLAYABLE_FIRST_FIVE_P0=GREEN
PLAYABLE_INSTALLER_E2E=BLOCKED_SAFETY_UNOWNED_LOCAL_DATA

Definition: trạng thái dưới đây dùng strict three-lane Math Definition of Done, không lấy việc “mở được màn hình” làm DONE.

## Playable Event V1 — current

- **First-five playable P0: GREEN** — Home/Hub → 5 nhiệm vụ cứu hộ production → intro → 3 checkpoint → wrong/retry/hint/repair → suspend/resume exact event/session/question → completion → Garden reward idempotent.
- P1 hardening đã khóa keyboard/prerequisite, exact resume-shell routing, valid→corrupt→valid recovery và dynamic accessibility theo state.
- Start-failure accessibility cũng fail-safe: status nhìn thấy và screen-reader cùng giữ thông điệp tiến bộ đã lưu an toàn.
- Lifecycle post-DONE hardening: 3/3 answered → suspend trước kết quả không grant reward; reopen complete đúng 3 checkpoint, không có câu 4, Garden +1 đúng lúc completion.
- Garden milestone hardening `88ecf34`: session `completed` nhưng **0 attempt** không được tính `CompletedMathSessions`, không tiến milestone và không bị backfill reward; rescue completion có attempt vẫn +1 idempotent.
- Durable reward hardening `9736443`: `GrantCompletedMathSession` tự đếm **attempt đã persist trong DB**; caller không thể giả `attempts>0` để tạo Garden reward cho session rỗng.
- Completion boundary `478e89b`: `Complete()` từ chối session 0-attempt; targeted lesson còn thiếu câu đã chọn cũng không được terminal hóa, giữ active/open question và reward=0.
- Release gate `e3bc9e0`: Build-Setup chạy `MathSessionPersistenceRuntimeSmoke` ở `10b/15` trước staging và ghi status + assertion count vào `release_manifest_dev.json`; artifact không còn có thể PASS nếu persistence/rescue lifecycle đỏ.
- Garden milestone regression `7496388`: 5 rescue production khóa chính xác seedling ở mốc 1, flower patch ở mốc 3; sau rescue #5 lantern vẫn khóa và còn đúng 1 session.
- Release gate `3af8d4a`: production validator `402/402 + 5/5`, full Math content/event/pool suite và Portable E2E trở thành gate bắt buộc trong cùng `Build-SetupArtifacts.ps1`; count được ghi động vào release manifest.
- Provenance gate `f4bb08a`: release build fail-closed nếu Git tree có tracked/untracked dirty entry, khóa HEAD không đổi trong build, chạy Python `-B` để không sinh `__pycache__`, và ghi `source_provenance/source_tree_clean/source_commit_stable=PASS`.
- Durable child boundary `069372d`: Garden eligibility chỉ nhận attempt Math bền vững thuộc đúng `session_id + child_id`; attempt của child khác không thể làm session của owner rewardable.
- Canonical reward repair `85a1d2a`: stale row trùng `source_key` được tự sửa về canonical Garden reward; orphan/non-canonical reward không còn làm phình `GrowthSteps`.
- Full milestone regression `0e91702`: khóa chính xác và replay-safe toàn bảng Garden **1 / 3 / 6 / 10**.
- Post-installer provenance `f5e5202`: thêm `15b/15`, HEAD/tree phải vẫn sạch sau installer + update-manifest trước khi được in `BUILD_SETUP_ARTIFACTS_PASS`; manifest ghi `source_provenance_after_installer=PASS`.
- Canonical milestone consistency `1c4ce9e`: milestone/next milestone/UI Garden derive từ canonical `GrowthSteps`, không từ số completed session; `ReadProgress()` không tin inventory stale/missing và reconcile tự chuẩn hóa materialized Garden inventory.
- DPI hardening: rescue intro + active gameplay đều được scale 125%; active prompt/checkpoint/break target vẫn đọc và thao tác được, flow retry/suspend tiếp tục bình thường.
- Child UI **3696 assertions PASS**; persistence **7933 assertions PASS**; full Math content/event/pool suite **90/90 PASS**.
- `0.1.68-dev`: Build-Setup **15/15 PASS** + provenance/content/persistence/Portable first-class gates + post-installer provenance, Portable E2E **PASS**, installer compile **PASS**.
- Portable SHA256 `1F351A2206B913E49B37FD1F054E77CCAE9BE93E7E72BAFDA6291FC4A262308E`; Installer SHA256 `26ECC47FCB14C37C255EEF8D26655631595DBCC53E590F4DE7BCE8F4124985BB`.
- Release payload hard-requires `game_events_v1.json`; fresh checkout SHA của cả 4 Math runtime JSON khớp manifest. Event SHA `F9F25EA94DA7FCD20E360509EE53E6E758039CCF788FAFEC729A35F80D39A8B1`.
- Full Installer E2E **BLOCKED_SAFETY** vì learner DB hiện hữu không có `.wahu-e2e-owned`; Portable E2E xác nhận DB hash trước/sau không đổi.
- Production signing và real Windows 7 validation vẫn `UNAVAILABLE/PENDING`.

## Overall

- **Content: 100% theo Math lane** — 7 chương, 17 chủ đề, 67 lesson, **402 authored questions**, đúng 6 câu/lesson = 2 basic + 2 medium + 2 application. Production semantic validator: **402/402 valid, 0 errors**; full Math content/event/pool suite **90/90 PASS**.
- **Engine: 100% theo Math lane** — schema V5 + pack identity, targeted session 3 câu từ pool 6, durable ordered selected-set, exact resume/retry/corrupt recovery, rescue runtime/checkpoint/repair/terminal/reward idempotency. `MathSessionPersistenceRuntimeSmoke`: **7933 assertions PASS**.
- **UI/QA: playable P0 GREEN cho 5 bài đầu** — Home/Hub rescue entry, 5 mission cards, 3 checkpoint cards, behavior-aware repair/break, exact rescue resume, fail-safe event fallback, Garden completion, accessibility/keyboard/responsive. `ChildUiRuntimeSmoke`: **3696 assertions PASS**.
- **Distribution playable dev artifact** — Build-Setup 15/15 + Portable E2E PASS + installer compile PASS trên clean `1c4ce9e`, artifact `0.1.68-dev`; full Installer E2E safety-blocked trên máy hiện tại.
- **Three-AI Math DoD: DONE** — AI1, AI2 và AI3 đã hoàn tất baseline; AI3 playable first-five lane đã đạt STOP RULE và chuyển `LANE_DONE=YES`.

## Contract hiện hành

- Runtime authored bank: **402 questions** / **67 lessons** / **6 per lesson**.
- Targeted lesson session: **3 questions**, chọn bền vững đúng 1 basic + 1 medium + 1 application.
- `SelectedContentQuestionIds` được persist; suspend/resume/retry/corrupt recovery giữ/recover selected set đúng contract.
- Hub hiển thị bank size 6 nhưng CTA/badge/accessibility không biến nó thành session size 6.
- Integer `answer_unit` chỉ là display metadata: raw integer grading không đổi; feedback có thể hiển thị `24 giờ` trong khi đáp án raw là `24`.
- Adaptive mission count derive từ `MathSessionCoordinator.DefaultTargetQuestionCount`.
- Numeric XP không được UI tự phát minh khi chưa có first-class product contract.

## Request đã đóng

### Request 009 — expanded pool vs 3-question session

CLOSED:

- engine `05cdb2a` + self-heal `3b8a10b`;
- runtime 402 publish `68145ea`;
- AI3 UI E2E `f567be8`;
- 402 compatibility sweep `e728204`.

Real-bank E2E xác nhận pool 6 → session/progress/result đúng 3, exact resume giữ ordered selected IDs và exact open question.

### Request 006 — answer unit

CLOSED:

- engine `3cf7fc6`;
- UI presentation/resume `3c0cfaa`; full 48-question / 8-unit matrix `bb0f636`.

Raw integer grading giữ nguyên; correct-answer feedback giữ display unit qua loader/runtime/suspend/resume.

### Request 008 — release packaging / portable / installer

CLOSED:

- E2E payload guards `109ee5d`;
- source/staged guards `490fd41`;
- manifest/runtime version guard `73d4c93`;
- 402 Child UI compatibility `e728204`; dynamic smoke evidence `3a53af7`.

Artifact dev `0.1.45-dev` từ clean `3a53af7`:

- schema: **5**;
- publish files: **55**;
- installed E2E file count: **57**;
- full release smokes: SetupPreflight 42, Behavior 15, LearningSession 800, Motion 25, Child UI **3267**, Content 21, Security 19, Audio 14, Performance 13, Update 33, SQLite 179 — tất cả PASS; các count được lấy trực tiếp từ output smoke;
- Portable E2E: first/second bootstrap exit 0, no installed-data mutation;
- Installer E2E: install 0, preflight 0, bootstrap 0, recovery 0, tamper exit 42, restored bootstrap 0, reinstall 0, uninstall 0;
- learner DB SHA256 trước/sau reinstall giống nhau: `FCA49F3ED8406D24262F41BD736F584A09E42454E2FB6F4EEE8DB4F692E3107C`;
- uninstall giữ learner DB/sentinel và remove startup registry;
- test-owned local data đã cleanup an toàn sau E2E.

Artifact hashes:

- Portable: `E644E73F49E28156E065DDDD944F8C46A154A7AE8E01D05381BE762C10CBE2CE`.
- Installer: `6D79CDD3B43B4E9CCF1DCCB42C521DDA08D680B4070C525DDB7F280E1AD322E8`.

## Latest fully verified gates — `1c4ce9e`

- Source provenance đầu build, sau Portable và **sau installer/update-manifest**: **PASS**; manifest ghi `source_provenance/source_tree_clean/source_commit_stable/source_provenance_after_installer=PASS`.
- Dirty-source negative probe: **PASS**, fail-closed đúng `REFUSE_DIRTY_SOURCE`.
- `WAHUKidsLearn.sln` Release/x86 rebuild inside Build-Setup: **PASS**.
- Learning session vertical-slice: **800 assertions PASS**; Child UI: **3696 assertions PASS**.
- Production Math validator: **402/402 valid**, game events **5/5 first-five**.
- Full Math content/event/pool suite: **90/90 PASS**, count parse động và ghi vào manifest.
- Persistence: Build-Setup `10b/15` build **0 warnings / 0 errors**, **7933 assertions PASS**.
- Garden: durable-child eligibility + canonical stale-key repair + exact milestones **1/3/6/10** + partial-reward fault + stale/missing inventory reconciliation đều nằm trong persistence regression.
- Content runtime: **21 assertions PASS**; SQLite/Data runtime: **179 assertions PASS**.
- Build-Setup `0.1.68-dev`: **15/15 PASS** cùng sub-gates `0/15`, `6b`, `6c`, `10b`, `13b`, `13c`, `15b`.
- Portable E2E: **PASS** trong Build-Setup; bootstrap 2 lần exit 0; learner DB thật trước/sau đều `0C970952435DAC03FE56C8400D003E87C37CED5D91B79C8A7D459A8EC30868C1`.
- Installer compile: **PASS**; update-manifest installer SHA khớp artifact. Full Installer E2E: **BLOCKED_SAFETY** vì learner data hiện hữu không có `.wahu-e2e-owned`.
- Payload `game_events_v1.json` SHA256 `F9F25EA94DA7FCD20E360509EE53E6E758039CCF788FAFEC729A35F80D39A8B1`, khớp manifest.
- Artifact `0.1.68-dev`: Portable `1F351A2206B913E49B37FD1F054E77CCAE9BE93E7E72BAFDA6291FC4A262308E`; Installer `26ECC47FCB14C37C255EEF8D26655631595DBCC53E590F4DE7BCE8F4124985BB`.
- Production signing và real Windows 7 validation vẫn `PENDING/UNAVAILABLE`; dev artifact không được mô tả là production-signed.

## Blocker cũ đã đóng trong AI3 no-wait wave

- hard-code adaptive `8/tám`: `82629a8`.
- pool-size/session-size UI coupling: `09c6551`, `f567be8`.
- generated segment/UI integration: `d21a665` + `ef3d35a`.
- valid→corrupt Hub stale state: `9c35ce5`.
- stale Continue accessibility name: `2bb1285`.
- 67-lesson accessibility breadth: `67c350e`.
- ContentRuntime pack-version stale assertion: `73d4c93`.
- 201-question Child UI assumptions sau publish 402: `e728204`.
- selected-set self-heal child-safe resume: `08a9268`.
- 48-question / 8-unit answer-unit matrix: `bb0f636`.
- 402-prompt min-window readability: `1e27ff8`.
- stale release assertion literals: `3a53af7` (manifest derives counts from smoke output).

## Ngoài strict Math lane

Không còn P0/P1 Math theo board 3-AI. Còn hai release-wide mục của toàn ứng dụng không được phép đánh tráo thành Math blocker:

1. production Authenticode signing — artifact `0.1.45-dev` hiện là unsigned dev build (`signed=false`);
2. compatibility smoke trên máy Windows 7 thật — current environment không có Win7 target, nên gate vẫn `PENDING/UNAVAILABLE`; preflight trên Windows mới báo `NEWER_WINDOWS_DEV_COMPATIBLE`.
