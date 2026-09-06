# 00 — SOURCE OF TRUTH & VERIFICATION POLICY

Cập nhật: 2026-09-06

## Mục tiêu

Mọi kiến thức đưa cho trẻ phải truy được về nguồn, có phạm vi rõ ràng và qua kiểm tra máy hoặc kiểm tra chéo. Không dùng câu “AI nghĩ là đúng” làm bằng chứng.

## Thứ tự ưu tiên nguồn

### TIER A — pháp lý/chương trình chính thức Việt Nam
1. Bộ Giáo dục và Đào tạo (BGDĐT), Chương trình GDPT 2018, Thông tư 32/2018/TT-BGDĐT và văn bản hợp nhất/sửa đổi còn hiệu lực.
2. Chương trình GDPT môn Toán, phần yêu cầu cần đạt Lớp 2.
3. Chương trình GDPT Làm quen Tiếng Anh lớp 1 và lớp 2, phần Lớp 2.
4. Văn bản hướng dẫn triển khai của BGDĐT khi cần xác định tính bắt buộc/tự chọn.

### TIER B — bằng chứng khoa học dạy học
- What Works Clearinghouse / Institute of Education Sciences (IES), U.S. Department of Education.
- Education Endowment Foundation (EEF) khi cần tổng hợp bằng chứng triển khai.

Tier B quyết định CÁCH DẠY, không được tự ý thay đổi NỘI DUNG CHƯƠNG TRÌNH Tier A.

### TIER C — sách giáo khoa/bộ sách cụ thể
Chỉ dùng để map thứ tự bài/chủ đề sau khi xác định bộ sách thật mà bé đang học. Không mặc định một bộ sách đại diện cho toàn quốc.

### TIER R — hình ảnh tham khảo trên web
Chỉ dùng để nghiên cứu cách biểu diễn. Không copy vào sản phẩm nếu chưa có quyền sử dụng rõ ràng. Production ưu tiên SVG/vector tự dựng hoặc asset do dự án sở hữu.

## Trạng thái nội dung

- `VERIFIED_A`: đúng với yêu cầu/khung chương trình Tier A.
- `VERIFIED_B`: phương pháp có nguồn bằng chứng Tier B.
- `BOOK_MAPPED`: đã map với bộ sách cụ thể.
- `SUPPLEMENTARY`: nội dung bổ trợ hợp lứa tuổi nhưng không tuyên bố là yêu cầu bắt buộc lớp 2.
- `HOLD`: chưa đủ bằng chứng; tuyệt đối không phát cho Child Mode.
- `REJECTED`: sai, vượt phạm vi, mơ hồ hoặc không phù hợp.

## Quy tắc release một câu hỏi

Một question production phải có tối thiểu:
- `question_id` duy nhất;
- `subject`, `grade`, `skill_id`;
- `source_ids`;
- `curriculum_status`;
- đáp án xác định được bằng validator;
- rationale cho distractor nếu là trắc nghiệm;
- kiểm tra range/đơn vị/thuật ngữ;
- kiểm tra asset khớp dữ liệu;
- không có ambiguity;
- content version;
- trạng thái `VERIFIED`.

Nếu validator không chứng minh được đáp án duy nhất → HOLD.

## Quy tắc hình ảnh giáo dục

1. Hình toán được sinh từ dữ liệu nguồn (số, tọa độ, đơn vị), không vẽ tay tùy ý.
2. Số vật thể trên hình phải machine-countable khi có thể.
3. Đồng hồ phải tính góc kim từ thời gian, không đặt bằng mắt.
4. Number line phải có bước đều và label sinh bằng code.
5. Base-ten phải đúng quan hệ 1 chục = 10 đơn vị, 1 trăm = 10 chục.
6. Hình học dùng vector/toạ độ; không dùng AI-art cho bài nhận dạng cần độ chính xác hình học.
7. Ảnh từ vựng tiếng Anh phải được human review về nghĩa; tránh ảnh đa nghĩa.

## Quy tắc không hứa “100% bằng niềm tin”

Không hệ thống thực tế nào nên tuyên bố không thể sai. Mục tiêu dự án là: **không phát nội dung chưa xác minh**, giữ provenance đầy đủ, test tự động được và có HOLD gate. Đây là cơ chế để đạt độ tin cậy cao nhất có thể và phát hiện sai trước khi trẻ nhìn thấy.

## Nguồn lõi đang dùng

- BGDĐT — Thông tư 32/2018/TT-BGDĐT, Chương trình GDPT 2018.
- BGDĐT — văn bản hợp nhất Thông tư 32/2018 với các sửa đổi liên quan: https://moet.gov.vn/content/vanban/Lists/VBPQ/Attachments/1483/vbhn-ttu-322018-202021-132022-ttbgddt.pdf
- Bản toàn văn phụ lục chương trình dùng để đối chiếu dòng yêu cầu môn Toán và Làm quen Tiếng Anh 1–2: https://static3.luatvietnam.vn/genfile/contentmix/2018/12/26/noi-dung-mix-thong-tu-so-32-2018-tt-bgddt-110829.pdf
- IES/WWC — Organizing Instruction and Study to Improve Student Learning: https://ies.ed.gov/ncee/wwc/practiceguide/1
- IES/WWC — Assisting Students Struggling with Mathematics: https://ies.ed.gov/ncee/wwc/practiceguide/26
- IES/WWC — Foundational Skills to Support Reading for Understanding K–3: https://ies.ed.gov/ncee/wwc/PracticeGuide/21
- EEF — Metacognition and self-regulation: https://educationendowmentfoundation.org.uk/education-evidence/teaching-learning-toolkit/metacognition-and-self-regulation
