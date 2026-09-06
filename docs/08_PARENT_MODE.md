# 08 — PARENT MODE

## 1. Mục tiêu

Parent Mode trả lời 4 câu hỏi:
1. Hôm nay con đã học gì?
2. Con đang vững gì?
3. Con đang vướng gì?
4. Tôi nên giúp gì tiếp theo?

## 2. Access

- PIN phụ huynh.
- Có thể kết hợp shortcut `Ctrl+Shift+P` hoặc icon kín đáo.
- Child Mode không tự vào được settings.

## 3. Dashboard hôm nay

Ví dụ:

```text
Hôm nay
Toán       14 phút
English    11 phút
Kỹ năng luyện    7
Recall ổn định   5
Cần hỗ trợ       2
```

Không mặc định khoe số câu làm được vì có thể khuyến khích quantity over quality.

## 4. Skill view

Ví dụ:

```text
Phép cộng có nhớ        Vững
Phép trừ có mượn        Cần luyện
Bài toán lời văn        Đang phát triển
Nghe từ tiếng Anh       Vững
Âm–chữ tiếng Anh       Đang phát triển
```

Click vào skill:
- lịch sử ngắn;
- lỗi phổ biến;
- mức hint;
- lần recall gần nhất;
- đề xuất.

## 5. “Hôm nay hỏi con gì?”

Mỗi ngày tối đa 1–3 gợi ý ngoài màn hình.

Ví dụ:
- “Có 35 viên kẹo, mẹ cho thêm 8 viên thì có bao nhiêu?”
- “Tìm một vật màu blue trong nhà.”

Gợi ý dựa trên skill đang học hoặc cần transfer.

## 6. Weekly summary

Không dùng rank.

Hiển thị:
- kỹ năng mới ổn định;
- kỹ năng cần quay lại;
- persistence/independence trend;
- session balance;
- đề xuất tuần tới.

## 7. Controls

Parent có thể chỉnh:
- session max;
- break interval;
- subject enable;
- audio/music;
- animation/reduced motion;
- difficulty intervention mức giới hạn;
- schedule reminder local optional;
- content packs;
- backup/export.

Không cho phụ huynh ép “100 câu/ngày” bằng default UI. Nếu có custom goal thì cảnh báo về chất lượng và tải nhận thức.

## 8. Parent notes

Cho phép ghi chú:
- “Tuần này ở trường đang học phép trừ có mượn.”
- “Con chưa học đơn vị mét.”

Engine có thể dùng note như soft constraint, không ghi đè mastery data.

## 9. Data export

Export:
- backup full;
- report summary CSV/PDF sau này;
- anonymized diagnostic package tùy chọn.

Không upload cloud mặc định.

## 10. Explainable recommendation

Ví dụ:
“Con làm tốt phép cộng có nhớ khi có block minh họa, nhưng 2 lần làm ký hiệu thuần vẫn cần gợi ý. Tuần này nên luyện chuyển từ block sang phép tính.”

Đây tốt hơn chỉ ghi `mastery = 68%`.