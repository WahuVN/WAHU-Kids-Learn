# MATH — 3 AI MASTER EXECUTION — USER LAUNCH SHEET

Updated: 2026-09-07

## USER PRIORITY OVERRIDE — FIRST LESSONS FIRST

Hiệu lực từ 2026-09-07: **ưu tiên hoàn thiện thật kỹ các bài Toán đầu trước; bài sau để cập nhật/hardening sau**.

Quy tắc chung cho cả 3 AI:
- P0 hiện tại là **5 bài đầu curriculum**; bài 1 là golden lesson.
- Chỉ sau khi 5 bài đầu content + engine + UI/E2E đều chắc mới mở rộng trọng tâm sang bài 6–10.
- Không tự mở backlog polish/hardening cho bài xa nếu không phải bug engine/UI dùng chung có thể ảnh hưởng bài đầu.
- AI1: ưu tiên nội dung, hint, rationale, readability, distractor, accepted answers và 6/6 authored variants của bài 1–5.
- AI2: ưu tiên golden-session, retry/resume/idempotency/progress/unlock/SQLite cho bài 1–5.
- AI3: ưu tiên UI, input surfaces, accessibility, overflow, feedback, resume/result E2E cho bài 1–5.
- Bài sau vẫn giữ dữ liệu hiện có và có thể update dần về sau; **không cần hoàn thiện đồng đều 67 bài trước khi phần đầu đạt chất lượng release**.
- Gặp bug dùng chung khi test bài đầu: sửa root cause và giữ regression chung, không patch riêng bài đầu để che lỗi.

Current AI2 evidence: 5 bài đầu sequential golden path PASS; đủ 30/30 authored IDs qua real sessions PASS; first-5 retry/error matrix PASS; Persistence **7542 assertions PASS**.

---

File này dành cho người điều phối. Không cần kể lại lịch sử cho AI. Chỉ gửi đúng một câu tương ứng:

## AI1

`BẠN LÀ AI1. Đọc USER PRIORITY OVERRIDE trong _WORK_CLAIMS/MATH_3AI_MASTER_EXECUTION.md, rồi đọc toàn bộ _WORK_CLAIMS/AI1_PARALLEL_NOW.md và tự kiểm current HEAD và làm liên tục đúng master plan. Không hỏi lại nếu repo/test tự giải được. Gặp lock thì chuyển FALLBACK, không chờ AI khác. Commit/push từng wave nhỏ.`

## AI2

`BẠN LÀ AI2. Đọc USER PRIORITY OVERRIDE trong _WORK_CLAIMS/MATH_3AI_MASTER_EXECUTION.md, rồi đọc toàn bộ _WORK_CLAIMS/AI2_PARALLEL_NOW.md và tự kiểm current HEAD và làm liên tục đúng master plan. Không hỏi lại nếu repo/test tự giải được. Gặp lock thì chuyển FALLBACK, không chờ AI khác. Commit/push từng wave nhỏ.`

## AI3

`BẠN LÀ AI3. Đọc USER PRIORITY OVERRIDE trong _WORK_CLAIMS/MATH_3AI_MASTER_EXECUTION.md, rồi đọc toàn bộ _WORK_CLAIMS/AI3_PARALLEL_NOW.md và tự kiểm current HEAD và làm liên tục đúng master plan. Không hỏi lại nếu repo/test tự giải được. Gặp lock thì chuyển FALLBACK, không chờ AI khác. Commit/push từng wave nhỏ.`

---

## Nguyên tắc điều phối

- Cả 3 AI có thể mở **cùng lúc**.
- Không AI nào phải chờ AI khác hết backlog.
- Dependency chỉ chặn publish/shared-file step.
- Mỗi AI có ownership riêng và FALLBACK độc lập.
- Mỗi AI phải đọc current `git status/log` vì cùng worktree nên commit lane khác xuất hiện ngay.
- Không dùng broad reset/restore/stash/rebase/clean.
- Không dùng `git add -A`/`git add .`.
- Chỉ commit exact files của lane.
- `LANE_DONE=YES` không có nghĩa AI được trả “đã xong” ngay khi được gọi lại; AI phải verify current HEAD và chạy backlog/hardening trong file master plan.

Chi tiết ownership, stable contracts, common final gates và STOP RULE chung: `_WORK_CLAIMS/MATH_3AI_PARALLEL_BOARD.md`.
