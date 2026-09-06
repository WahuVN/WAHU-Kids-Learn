# 12 — CURRENT FINDINGS: SETUP & DESIGN RESEARCH

Cập nhật: 2026-09-06
Trạng thái: DRAFT — sẽ được mở rộng và chuyển thành setup gates.

## Các kết luận đã đủ mạnh để áp dụng

### Windows 7 readiness phải có nhiều tầng
Không coi `Windows 7 SP1` đồng nghĩa với `ready`.

Tối thiểu phân biệt:
- OS không phải Win7 SP1 → unsupported.
- Win7 SP1 nhưng thiếu nền SHA-2/servicing stack → compatibility warning/block prerequisite path cho các gói hiện đại.
- Win7 SP1 + .NET 4.8 + prerequisite phù hợp → runtime-compatible candidate.
- Chỉ sau smoke-test app/driver/audio/DPI mới → target-ready.

### SQLite backup không được copy nóng file DB một cách mù quáng
Ưu tiên SQLite Online Backup API hoặc close sạch rồi copy. Với WAL, file `-wal` là một phần persistent state khi còn tồn tại; không được tách DB khỏi WAL khi copy/move tùy tiện.

### Startup integrity nên tiered
- startup bình thường: metadata/schema + `quick_check` khi hợp lý;
- unclean shutdown/recovery/migration nghi ngờ: `integrity_check` + `foreign_key_check`;
- không chạy full integrity scan mỗi lần nếu làm chậm máy cũ.

### Accessibility cho trẻ phải mạnh hơn mức WCAG tối thiểu
WCAG 2.2 nêu target pointer tối thiểu 24×24 CSS px và mọi chức năng dùng drag nên có phương án single-pointer không drag. WAHU giữ engineering target lớn hơn nhiều (~64 px primary target baseline) vì người dùng là trẻ lớp 2 dùng chuột trên PC cũ.

### Motion phải có control và không phá tập trung
- motion tự chạy song song với nội dung học phải có cách pause/stop/hide nếu kéo dài;
- interaction-triggered nonessential motion phải disable được;
- WAHU dùng `Normal / Reduced / Minimal`, decorative motion OFF trong read/listen/think.

### UI cần predictable/conventional
W3C cognitive accessibility nhấn mạnh control đơn giản, quen thuộc, consistent, nội dung ngắn, rõ, giảm distraction. Vì vậy WAHU không dùng navigation “sáng tạo” khiến trẻ phải học lại pattern.

### Windows 7 DPI strategy
Win7 hỗ trợ system-DPI awareness qua manifest `dpiAware=true`; không nên dựa vào enhanced WinForms High-DPI features vốn chỉ có khi chạy Windows 10 Creators Update trở lên. Thiết kế phải test thực tế 96 DPI và 120 DPI/125% trên Win7.

### WinForms accessibility
Standard WinForms controls đã có UI Automation provider; custom controls cần expose accessibility tương ứng. `AccessibleName`, `AccessibleDescription`, `AccessibleRole` phải là requirement cho custom/semantic controls.

### UDL / learner variability
CAST UDL 3.0 hỗ trợ các quyết định:
- choice/autonomy có giới hạn;
- optimize challenge + support;
- multiple representations;
- multiple response/navigation methods;
- graduated support;
- transfer/generalization;
- progress monitoring/reflection.

### Feedback
EEF: feedback hiệu quả nên tập trung task/subject/self-regulation và phải actionable; feedback về cá nhân/fixed trait kém hiệu quả hơn. Cần feedback cả khi làm đúng, không chỉ sai.

### Learning science
IES/WWC hỗ trợ:
- spacing;
- retrieval practice;
- worked example xen problem solving;
- graphics + verbal;
- concrete ↔ abstract connection;
- giúp trẻ nhận biết mình biết/chưa biết gì.

### Digital child-centered design
AAP 2026 cảnh báo digital ecosystem tối ưu engagement/commercialization có thể kéo dài sử dụng và lấn hoạt động lành mạnh; child-centered design nên ưu tiên privacy, safety, meaningful learning/well-being. Điều này củng cố hard rule: app không tối ưu time-in-app.

### Privacy/data minimization
FTC/COPPA guidance nhấn mạnh chỉ thu dữ liệu cần thiết, bảo mật, retention có mục đích và xóa khi không cần. Dù WAHU V1 local-only, đây vẫn là privacy-by-design baseline.

### Archive/content import
OWASP khuyến nghị validate file/archive trước extraction, gồm path, compression/estimated unzip size và maximum file size. Điều này khớp content import pipeline hiện có và cần test zip-slip/decompression bomb.
