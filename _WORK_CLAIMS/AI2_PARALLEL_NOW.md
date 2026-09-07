# AI2 — PARALLEL NOW — ENGINE/SESSION

LANE: AI2
LANE_DONE=NO
BASELINE=3d8674d
MANIFEST_LOCK=FREE
BUILD_SETUP_LOCK=AI3
REQUEST_009_READY=05cdb2a
REQUEST_010_READY=7afbb7b
REQUEST_006_READY=3cf7fc6
ADAPTIVE_SEGMENT_READY=THIS_COMMIT


## NO-WAIT rule
AI2 **không chờ AI1/AI3**. Request 009 dùng synthetic pool >=6 ngay trong engine tests; Request 006/010/generator đều có fixture độc lập. Nếu một file đang lock bởi chính AI2 wave khác, commit wave nhỏ trước rồi chuyển task kế tiếp.

### Queue luôn có việc
- DONE: Request 010 isolated at `7afbb7b` — expression whitelist + current-question JSON regression, persistence 220 PASS.
- DONE/commit-now: adaptive `draw_segment_given_length` — LearningSession 800 PASS.
- DONE: Request 009 at `05cdb2a` — synthetic pool >=6 selects/persists exactly 3; persistence 264 PASS.
- DONE: Request 006 at `3cf7fc6` — `answer_unit` display-only survives loader/runtime/resume; raw integer grading unchanged; persistence 277 PASS.
- NOW: validator/generator fuzz, retry/idempotency/corrupt-cache/persistence stress, selector determinism, V5 pack-identity regression.

## Mission now

Keep all engine/session work inside AI2-owned files. Never stage AI1 question/catalog files or AI3 UI/release-E2E files.

### A. Commit Request 010 first, isolated
Current WIP already adds `AllowedExpressionOperators` to model/loader/validator. Finish mandatory regression before commit:
- authored add/sub expression loads whitelist `+ - ( )`;
- runtime instance preserves it;
- `75`, `100 - 30 + 5`, `70 + 5` => correct;
- `15*5`, `150/2` => incorrect;
- suspend/resume/current-question JSON preserves whitelist;
- global expression questions without a whitelist keep existing backward-compatible parser behavior.

Commit only Request-010 files/tests; do not include adaptive generator/template WIP.

### B. Commit adaptive segment generator second
Land current `draw_segment_given_length` WIP:
- selector supports template;
- generator returns `interaction_integer`;
- no fake choices;
- correct numeric answer still validates;
- fuzz/runtime smoke passes.
Include only its `verified_templates_v1.json`/manifest changes and the LearningSession assertion-count update. The current one-line `Build-SetupArtifacts.ps1` assertion-count WIP may land in this wave if its release gate passes.

After this commit:
- set `MANIFEST_LOCK=FREE` unless another AI2 template wave still needs it;
- set `BUILD_SETUP_LOCK=AI3` so AI3 can finish Request 008.

### C. Request 009 — selected 3 from pool >=6
Implement without changing AI1 runtime bank:
1. Fresh targeted session selects exactly 3 authored IDs: 1 basic + 1 medium + 1 application.
2. Selection is deterministic for fixed selection identity/seed; another fixed identity can produce a different valid set.
3. Persist ordered selected IDs as first-class session state.
4. Resume/retry/write-failure/corrupt-open recovery continues the selected set, never re-selects and never skips ordinal.
5. `TargetQuestionCount=3` and completion/score are based on selected set, not `PracticeSets.TotalCount`.
6. Synthetic lesson fixture with >=2 questions per difficulty must PASS all above semantics.

When green and committed, write `REQUEST_009_READY=<sha>` here.

### D. Request 006 after Request 009
Preserve integer `answer_unit` as display-only metadata through model -> authored loader -> runtime instance -> suspend/resume. Raw integer grading must remain unchanged; correct-answer display may include unit. Add regression for `8` accepted/display `8 cm`.

### Commit discipline
Use exact file commits. Do not edit `MATH_SHARED_CONTRACT_REQUESTS.md` during the parallel run. Publish handoff markers only in this file.
