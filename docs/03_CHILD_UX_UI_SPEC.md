# 03 — CHILD UX/UI SPEC

## 1. UX goal

Một trẻ lớp 2 phải có thể:
- mở app;
- biết nút nào cần bấm;
- bắt đầu nhiệm vụ;
- làm bài;
- xin gợi ý;
- nghỉ;
- kết thúc;

mà gần như không cần đọc hướng dẫn dài.

## 2. Visual hierarchy

- Một primary CTA/màn hình.
- Target lớn: baseline 56–80 px cho nút quan trọng.
- Text chính lớn, ít chữ.
- Icon + text, không dùng icon mơ hồ đơn độc.
- Không dồn dashboard vào màn hình của trẻ.

## 3. Home screen

```text
┌──────────────────────────────────────────┐
│ Avatar           Chào buổi chiều, Bảo! │
│                                          │
│       🚀 NHIỆM VỤ HÔM NAY               │
│                                          │
│  [ 🐉 TOÁN — Giúp Rồng tìm 5 viên ngọc ]│
│                                          │
│  [ 🐼 ENGLISH — Tìm đồ ăn cho Panda    ]│
│                                          │
│  [ 🔁 ÔN NHẸ — 3 điều con từng học      ]│
│                                          │
│ 🏡 Nhà     🎒 Bộ sưu tập     🌎 Thế giới │
└──────────────────────────────────────────┘
```

Không hiển thị:
- số liệu mastery kỹ thuật;
- session analytics;
- leaderboard;
- cài đặt phức tạp.

## 4. Lesson screen

```text
┌──────────────────────────────────────────┐
│ ❤️ companion      Tiến trình ●●○○○     │
│                                          │
│          27 + 15 = ?                     │
│                                          │
│       [ 32 ] [ 42 ] [ 52 ]              │
│                                          │
│       💡 Gợi ý        ⏸ Nghỉ             │
└──────────────────────────────────────────┘
```

Không dùng đồng hồ đếm ngược trong lesson thường.

## 5. Feedback states

### Correct
- animation ngắn 300–800 ms;
- phản hồi cụ thể nếu đáng giá;
- tự chuyển câu sau sau khoảng ngắn hoặc nút Tiếp tục.

### Incorrect
Không rung toàn màn hình, không đỏ mạnh.

Ví dụ:
- viền nhẹ;
- companion nói “Thử nhìn hàng đơn vị trước nhé”.

### Hint
Hint chia tầng:
1. cue;
2. visual support;
3. partial step;
4. worked example;
5. guided solution.

## 6. Reading load

Ưu tiên:
- câu ngắn;
- voice instruction optional;
- hình minh họa;
- từng bước.

Không để đoạn hướng dẫn >2–3 dòng cho Child Mode.

## 7. Mouse ergonomics

- Không yêu cầu drag nhỏ/chính xác trừ mini-game được thiết kế riêng.
- Hit area lớn hơn hình hiển thị.
- Không double-click bắt buộc.
- Không right-click bắt buộc.
- Không hover-only.

## 8. Keyboard optional

Có thể hỗ trợ:
- 1–4 chọn đáp án;
- Enter tiếp tục;
- Space nghe lại audio;
- Esc pause.

Nhưng mouse vẫn phải đủ để dùng toàn app.

## 9. Break screen

```text
🌿 Nghỉ một chút nhé!

Đứng lên, vươn vai 30 giây.

[ Con sẵn sàng rồi ]
```

Không có carousel reward kích thích trong break.

## 10. End-session screen

Không tập trung vào điểm.

Hiển thị:
- “Hôm nay con đã luyện 3 kỹ năng.”
- 1 tiến bộ cụ thể.
- 1 item/world progress.
- CTA: “Xong rồi” thay vì dụ học tiếp vô hạn.

## 11. Color & accessibility

- Contrast tốt.
- Không truyền đạt đúng/sai chỉ bằng màu.
- Font rõ, hỗ trợ Vietnamese đầy đủ.
- Tránh font decorative cho nội dung học.
- Cho Parent chỉnh text scale.
- Có chế độ giảm chuyển động.

## 12. Parent entry

Không hiển thị “CÀI ĐẶT PHỤ HUYNH” quá hấp dẫn.

Entry có thể là icon nhỏ ở góc + PIN, hoặc shortcut bàn phím.

## 13. Animation rule

Animation chỉ hợp lệ khi nó:
- giải thích trạng thái;
- khen tiến bộ;
- làm thế giới sống động vừa đủ.

Animation không được:
- trì hoãn bài học;
- che đáp án;
- phát liên tục gây phân tâm;
- làm CPU cao khi idle.