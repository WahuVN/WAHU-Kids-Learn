# AI1 — PARALLEL NOW — CONTENT/DATA

LANE: AI1
LANE_DONE=YES
BASELINE=73d4c93
REQUEST_009_READY=05cdb2a
MANIFEST_LOCK=FREE
SHADOW_POOL6_READY=YES
RUNTIME_POOL6_READY=68145ea

## Final state

AI1 content/data lane is complete. Request 009 publish gate đã được tiêu thụ; runtime không còn ở shadow-only mode.

### Completed

- 67/67 baseline skill có lesson đầy đủ.
- 201 câu gốc + 201 câu expansion `_04/_05/_06` = **402 runtime questions**.
- 67 lesson × 6 câu, đúng **2 basic + 2 medium + 2 application**; ID `_01..06` contiguous.
- Production semantic validator: **402/402 valid, 0 errors**.
- Full `MathContentDataSmoke`: **57/57 PASS**.
- Hint: 402/402 unique ở mỗi cấp, max repeat 1; max length 124/130.
- Distractor rationale: **534/534 unique**, max repeat 1; structured/component/explicit wrong→correct diagnosis đều fail-closed.
- MC 4-choice balance: **44/44/44/44**; true/false **3/3**.
- Deterministic regenerate giữ nguyên SHA catalog/bank.
- Manifest `1.9.0`: 3/3 listed files khớp SHA256, không thiếu/thừa file pack.
- Clean rebuild `MathSessionPersistenceRuntimeSmoke` trên real 402-bank: **289 assertions PASS**.

## Handoff

- AI3 có thể chạy real-bank pool-6 UI/E2E trên runtime 402.
- AI2 tiếp tục Request 006/engine work độc lập; AI1 không stage hoặc nhận ownership các WIP Learning/Session/Data của AI2.
- AI1 không còn content/data blocker.

## Content contract after publish

1. Runtime bank phải giữ **402 questions**, 6 câu/lesson và 2 câu/difficulty.
2. Session target vẫn do AI2 engine chọn/persist đúng 3 câu từ pool 6; content không hard-code selection.
3. Stable question IDs giữ `m2_q_<skill>_01..06`.
4. Mọi question phải được practice set tham chiếu đúng một lần, không orphan/duplicate reference.
5. Authoring phải deterministic; generated catalog/bank phải khớp committed JSON.
6. Manifest chỉ được cập nhật bằng SHA của file production thực tế; pack version hiện khóa ở `1.9.0` để khớp runtime pack identity.

## Commit discipline

Commit/push chỉ AI1-owned content/data/authoring/validator/test/status files. Không stage `src/Learning`, `src/Session`, `src/Data` hoặc UI/release WIP của lane khác.
