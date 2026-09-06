# 04 — VISUAL ASSET RESEARCH & COPYRIGHT POLICY

## Kết luận nghiên cứu hình ảnh

Các kiểu biểu diễn hữu ích cho lớp 2:
- base-ten blocks cho trăm/chục/đơn vị;
- ten-frame/counters cho cấu tạo số nhỏ và tính nhẩm;
- number line cho thứ tự, khoảng cách, cộng/trừ;
- đồng hồ analog chính xác;
- thước/cân/cốc lít dạng sơ đồ;
- biểu đồ tranh;
- shape cards hình học sạch;
- English picture cards đơn nghĩa, ít chi tiết;
- action cards cho TPR/câu lệnh.

## Nguồn nghiên cứu visual đã xem

Chỉ dùng làm reference layout/idea, KHÔNG tự động nhập hình vào app:
- White Rose Education — Dienes/base-ten usage: https://whiteroseeducation.com/latest-news/what-are-dienes-teach-using-them
- IES visual representations guidance: https://ies.ed.gov/rel-southeast/2025/01/infographic-26
- IES math practice guide: https://ies.ed.gov/ncee/wwc/practiceguide/26

Các kết quả Pinterest/TPT/product listing chỉ có giá trị tham khảo thị giác; không được coi là nguồn kiến thức hay nguồn license production.

## Production asset policy

### Toán
Ưu tiên SVG tự dựng từ code/data để:
- đếm được object;
- unit spacing chính xác;
- nhẹ trên Win7;
- scale không vỡ;
- dễ kiểm thử snapshot.

### English
Production image phải:
- một nghĩa mục tiêu rõ;
- không chứa chữ nếu task là nghe → chọn ảnh, trừ khi chủ đích;
- không có yếu tố văn hoá gây hiểu sai;
- qua human semantic review;
- lưu `concept_id` tách khỏi filename.

## AI-generated illustration

Có thể dùng cho:
- companion;
- background;
- story scene;
- cosmetic.

Không ưu tiên AI art cho:
- hình học chuẩn;
- đồng hồ;
- thước;
- tiền;
- biểu đồ cần đếm;
- letterform/word spelling;
- hình dùng để phân biệt số lượng chính xác.

## Asset QA

Mỗi asset instructional có metadata:
- `asset_id`;
- `concept_id`;
- `source_type=self_vector|licensed|generated`;
- `semantic_review=PASS/HOLD`;
- `geometry_review=PASS/HOLD` nếu cần;
- `license`;
- `version`.
