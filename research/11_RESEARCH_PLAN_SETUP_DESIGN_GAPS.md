# 11 — RESEARCH PLAN: SETUP & DESIGN GAPS

Cập nhật: 2026-09-06
Trạng thái: ACTIVE RESEARCH

Mục tiêu vòng này: bổ sung các khoảng trống còn ảnh hưởng trực tiếp tới implementation/release của WAHU Kids Learn, ưu tiên nguồn chính thức và khả năng biến thành rule/config/test.

## Nhóm đang nghiên cứu

1. Windows 7 prerequisite thực tế ngoài SP1/.NET 4.8: SHA-2/servicing stack/code-signing readiness.
2. SQLite durability: WAL/rollback, online backup, integrity/foreign-key checks.
3. Child accessibility: target size, non-drag alternatives, reduced distraction, multiple response modes.
4. Universal Design for Learning: choice/autonomy, challenge/support, multiple representations, transfer.
5. Learning science: spacing, retrieval, worked-example interleave, concrete↔abstract, metacognition.
6. Child-centered digital design: không tối ưu prolonged use; ưu tiên privacy/safety/meaningful learning.
7. Content import security: archive limits, path traversal, malformed/oversized input.
8. Parent PIN/data minimization/retention.
9. Installer release hardening: prerequisite classification, signing/readiness, update/rollback evidence.
10. Chuyển các phát hiện thành setup spec + test gate, không giữ ở dạng note nghiên cứu.

## Nguyên tắc nguồn

Ưu tiên theo thứ tự:
- vendor/standards/gov/official education guidance;
- systematic review/practice guide;
- GitHub/project docs cho implementation pattern;
- blog/community chỉ dùng để tìm vấn đề, không dùng làm source-of-truth nếu có nguồn mạnh hơn.
