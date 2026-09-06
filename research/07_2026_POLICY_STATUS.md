# 07 — 2026 CURRENT POLICY STATUS

Cập nhật: 2026-09-06

## Vì sao cần file này

Dự án không chỉ dựa vào tài liệu 2018 mà phải kiểm tra xem đến năm 2026 đã có thay đổi pháp lý nào làm thay đổi baseline lớp 2 hay chưa.

## Kết quả kiểm tra hiện tại

### Chương trình GDPT 2018 vẫn là baseline đang được cơ quan Bộ sử dụng
Nguồn chính thức năm 2026 về quản lý chất lượng vẫn tham chiếu việc triển khai Chương trình GDPT 2018.

### Tiếng Anh như ngôn ngữ thứ hai
Ngày 17/08/2026, Cục Quản lý Chất lượng/BGDĐT thông tin Bộ đang **lấy ý kiến góp ý dự thảo Thông tư** về tiêu chuẩn, tiêu chí đánh giá cơ sở giáo dục triển khai Đề án “Đưa tiếng Anh thành ngôn ngữ thứ hai trong trường học, giai đoạn 2025–2035, tầm nhìn 2045”.

Dự thảo/đề án không tự động thay thế yêu cầu cần đạt lớp 2 của chương trình hiện hành. Do đó app không được tự đổi baseline Grade 2 chỉ vì định hướng mới này.

### Thực tế lớp 1–2
Báo cáo tổng kết Đề án Ngoại ngữ 2017–2025 của hệ thống BGDĐT ghi tỷ lệ học sinh làm quen tiếng Anh lớp 1–2 tăng từ 43% năm 2020 lên 69% năm 2024; trong khi học sinh tiểu học lớp 3–5 học chương trình tiếng Anh hệ 10 năm đạt 100%.

Điều này phù hợp với việc vẫn phải xử lý lớp 1–2 như chương trình làm quen, không giả định mọi trẻ đều có cùng đầu vào English.

## Policy rule cho app

- `current_curriculum_baseline = CTGDPT_2018_consolidated` cho đến khi có văn bản hiệu lực thay thế/sửa yêu cầu Grade 2.
- Policy news/draft chỉ gắn `WATCH`, không đổi content release gate.
- Mỗi lần build content major version phải re-check BGDĐT.
- Nếu có văn bản mới có hiệu lực: tạo curriculum version mới; không sửa âm thầm pack cũ.

## Nguồn

- Cục QLCL/BGDĐT 17/08/2026 — dự thảo tiêu chí tiếng Anh ngôn ngữ thứ hai: https://vqa.moet.gov.vn/vi/news/tin-tuc-su-kien/quy-dinh-tieu-chuan-tieu-chi-danh-gia-trong-trien-khai-thuc-hien-dua-tieng-anh-thanh-ngon-ngu-thu-hai-293.html
- Đề án Ngoại ngữ Quốc gia — tổng kết 2017–2025: https://ngoainguquocgia.moet.gov.vn/tong-ket-de-an-day-va-hoc-ngoai-ngu-trong-he-thong-giao-duc-quoc-dan-giai-doan-2017-2025-6064105.html
- Cục QLCL/BGDĐT — nhiệm vụ quản lý chất lượng 2026–2027, tiếp tục tham chiếu triển khai CTGDPT 2018: https://vqa.moet.gov.vn/vi/news/tin-tuc-su-kien/bo-gddt-huong-dan-thuc-hien-nhiem-vu-quan-ly-chat-luong-nam-hoc-2026-2027-294.html
