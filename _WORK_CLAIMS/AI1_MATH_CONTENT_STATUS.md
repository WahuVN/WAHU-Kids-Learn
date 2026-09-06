# AI1 — MATH CONTENT STATUS

Updated: 2026-09-07

## Current metrics

- Baseline skills: 67
- Lessons: 0 machine-readable complete / 67 required
- Lessons incomplete/missing: 67
- Generator templates: 57
- Template-covered skills: 65 / 67
- Static question bank: 0
- Valid static questions: 0
- Semantic validator errors: N/A (validator chưa tồn tại)
- Existing content smoke: chưa chạy sau audit
- AI1 commits: none yet
- Blockers: runtime/UI chưa có contract consume lesson catalog/question bank; working tree có thay đổi song song ở Math engine và template pack

## Wave tracking

- [x] Audit source/database/seed/data/migration/tests/UI consumption
- [x] Inventory written
- [ ] Curriculum hierarchy + lesson catalog
- [ ] Question bank
- [ ] Semantic validator
- [ ] Content tests
- [ ] Manifest/hash/version integration
- [ ] Final validator clean
- [ ] Final tests clean

## Rules observed

- Không reset work song song.
- Không sửa engine/mastery/UI lớn.
- Shared contract changes chỉ ghi vào `MATH_SHARED_CONTRACT_REQUESTS.md`.
