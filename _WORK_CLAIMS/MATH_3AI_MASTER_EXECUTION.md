# MATH — 3 AI MASTER EXECUTION — USER LAUNCH SHEET

Updated: 2026-09-07

File này dành cho người điều phối. Không cần kể lại lịch sử cho AI. Chỉ gửi đúng một câu tương ứng:

## AI1

`BẠN LÀ AI1. Đọc toàn bộ _WORK_CLAIMS/AI1_PARALLEL_NOW.md rồi tự kiểm current HEAD và làm liên tục đúng master plan. Không hỏi lại nếu repo/test tự giải được. Gặp lock thì chuyển FALLBACK, không chờ AI khác. Commit/push từng wave nhỏ.`

## AI2

`BẠN LÀ AI2. Đọc toàn bộ _WORK_CLAIMS/AI2_PARALLEL_NOW.md rồi tự kiểm current HEAD và làm liên tục đúng master plan. Không hỏi lại nếu repo/test tự giải được. Gặp lock thì chuyển FALLBACK, không chờ AI khác. Commit/push từng wave nhỏ.`

## AI3

`BẠN LÀ AI3. Đọc toàn bộ _WORK_CLAIMS/AI3_PARALLEL_NOW.md rồi tự kiểm current HEAD và làm liên tục đúng master plan. Không hỏi lại nếu repo/test tự giải được. Gặp lock thì chuyển FALLBACK, không chờ AI khác. Commit/push từng wave nhỏ.`

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
