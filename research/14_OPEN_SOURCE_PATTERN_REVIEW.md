# 14 — OPEN-SOURCE PATTERN REVIEW

Cập nhật: 2026-09-06

## GCompris
GCompris là bộ phần mềm giáo dục mã nguồn mở dành cho trẻ khoảng 2–10 tuổi, gom nhiều hoạt động giáo dục dưới một giao diện thống nhất.

### Pattern đáng học
- activity catalog thống nhất thay vì hàng loạt tool rời;
- activity/game vẫn phải có learning purpose;
- dễ mở rộng thêm activity;
- localization là concern cấp hệ thống.

### Không copy nguyên kiến trúc
Bản hiện đại dùng QtQuick/QML, không phù hợp constraint Win7 cực cũ của WAHU V1. Ta học product organization/activity model, không lấy runtime.

## Sugar / OLPC HIG
Sugar được thiết kế cho trẻ nhỏ và phần cứng hạn chế.

### Pattern đáng học
- “activities, not applications”: đặt trọng tâm vào việc trẻ đang *làm*;
- interface phải discoverable/intuitive cho người ít kinh nghiệm máy tính;
- “low floor, no ceiling”;
- robustness/performance là nguyên tắc thiết kế, không phải tối ưu cuối dự án;
- naming theo hành động có thể dễ hiểu hơn tên module kỹ thuật.

### Áp dụng WAHU
Child Home nên nói theo nhiệm vụ/hành động: “Ghép số”, “Nghe và chọn”, “Xây cầu bằng phép cộng” thay vì module/course ID.

## Kolibri
Kolibri là nền tảng học offline-first cho bối cảnh thiếu Internet; content được curate thành channels và có thể chuyển bằng USB/ổ cứng.

### Pattern đáng học
- runtime và content distribution tách rời;
- content có metadata/version/curation;
- Internet không bắt buộc ở learner device;
- import/export qua removable media là first-class workflow;
- initial setup chỉ làm một lần rồi thay đổi settings sau.

### Áp dụng WAHU
Củng cố quyết định `.wahu`/content pack versioned + Parent Content Studio ngoài child runtime + USB import/export.

## Kết luận
Không lấy dependency/runtime của GCompris/Kolibri/Sugar vào WAHU V1. Chỉ học pattern đã chứng minh hữu ích cho:
- activity organization;
- child discoverability;
- low-resource robustness;
- offline content curation/distribution.
