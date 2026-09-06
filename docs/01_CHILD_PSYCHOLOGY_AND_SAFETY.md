# 01 — CHILD PSYCHOLOGY, MOTIVATION & SAFETY

## 1. Mục tiêu

Dùng tâm lý học học tập và thiết kế hành vi để tăng khả năng trẻ tự bắt đầu, duy trì tập trung, chịu thử lại sau khi sai và hình thành cảm giác năng lực.

Không sử dụng thao túng gây nghiện hoặc dark pattern.

## 2. Các trụ cột

### A. Autonomy — cảm giác tự chủ
Cho trẻ lựa chọn có giới hạn:
- “Con muốn làm Toán hay English trước?”
- Chọn companion.
- Chọn một trong hai skin nhiệm vụ.

Không đưa quá nhiều lựa chọn.

### B. Competence — cảm giác mình đang tiến bộ
Phản hồi cần cụ thể:
- “Lần trước dạng này con cần 2 gợi ý, hôm nay con tự làm được.”
- “Con đã nhớ cách tách hàng chục và hàng đơn vị.”

Tránh:
- “Con là thiên tài.”
- “Sao câu này cũng sai?”

### C. Relatedness — cảm giác có bạn đồng hành
Companion phản ứng với nỗ lực và chiến lược, không chỉ đúng/sai.

### D. Flow
Hệ thống giữ thử thách trong vùng vừa sức.

Baseline heuristic:
- Dưới ~60% đúng ở cửa sổ gần: hạ độ khó / scaffold.
- 60–75%: giữ hoặc thêm hỗ trợ.
- 75–85%: vùng luyện tập ưu tiên.
- Trên ~90% liên tục: tăng độ khó hoặc giảm scaffolding.

Các con số là baseline cần hiệu chỉnh bằng dữ liệu thực tế, không phải chân lý cố định.

## 3. Frustration Controller

Tín hiệu có thể dùng:
- Sai liên tiếp cùng micro-skill.
- Thời gian trả lời tăng đột biến.
- Click rất nhanh nhưng sai nhiều — khả năng đoán.
- Bỏ qua/thoát nhiều.
- Dùng hint tối đa nhiều lần.

### Response ladder

```text
Sai lần 1
→ phản hồi nhẹ + 1 gợi ý nhỏ

Sai lần 2
→ đổi biểu diễn trực quan

Sai lần 3
→ worked example tương tự

Tiếp tục khó
→ câu prerequisite dễ hơn

Ổn lại
→ quay lại câu mục tiêu bằng biến thể khác
```

### Không được làm
- Flash đỏ mạnh.
- Âm thanh buzzer khó chịu.
- Trừ coin.
- Chế giễu.
- Ép hoàn thành trước khi thoát.

## 4. Reward psychology

### Giai đoạn đầu
Có reward trực quan để tạo thói quen:
- sao;
- sticker;
- đồ trang trí;
- companion cosmetic.

### Giai đoạn sau
Giảm trọng số extrinsic reward, tăng:
- progress reflection;
- “trước đây/con bây giờ”;
- unlock câu chuyện;
- quyền lựa chọn;
- mastery celebration.

## 5. Không dùng streak kiểu đe dọa

Có thể hiển thị “nhịp học gần đây”, nhưng:
- nghỉ không làm mất thành tựu;
- không có countdown đe dọa;
- không push “hôm nay không học sẽ mất X”.

Thay vào đó dùng:
- “Hôm nay mình có thể tiếp tục khu vườn nếu con muốn.”
- “Mình nghỉ hôm qua rồi, hôm nay bắt đầu nhẹ nhé.”

## 6. Error language

Từ khóa nên dùng:
- “Thử cách khác nhé.”
- “Gần đúng rồi, xem hàng chục trước.”
- “Câu này hơi khó, mình làm một câu mẫu.”

Tránh:
- “Sai!” to, đỏ.
- “Không đúng nữa rồi.”
- “Con chưa học à?”

## 7. Choice architecture an toàn

Một màn hình chỉ nên có 1 primary action, tối đa 2 lựa chọn học tập.

Không dùng:
- nút thoát bị giấu;
- confirm-shaming;
- timer giả;
- scarcity giả;
- phần thưởng ngẫu nhiên kiểu loot box;
- variable-ratio reward để giữ trẻ trong app.

## 8. Session design

Baseline cho lớp 2:
- 2 phút warm-up.
- 7–10 phút Math.
- microbreak.
- 7–10 phút English.
- 2–3 phút recall/review.
- kết thúc tích cực.

Không hard-code thời lượng; Parent Mode cho phép điều chỉnh.

## 9. Break Engine

App cần đề xuất nghỉ khi:
- session vượt thời lượng cấu hình;
- frustration score tăng;
- nhiều phản hồi chậm;
- phụ huynh bật chế độ nghỉ bắt buộc.

Break screen:
- đứng dậy;
- nhìn xa;
- uống nước;
- vận động 30–60 giây.

Không có nội dung reward kích thích cao trong break.

## 10. Behavior Learning Controller

Chi tiết nghiên cứu và policy runtime nằm tại:
- `research/08_BEHAVIORAL_PSYCHOLOGY_LEARNING_MECHANISMS.md`
- `core/learning/behavior/behavior_policy_v1.json`

Controller chỉ dùng dữ liệu hành vi học trong phiên để suy ra `READY / FLOW_LIKELY / BORED_OR_UNDERCHALLENGED / STRAINED / FRUSTRATED_LIKELY / FATIGUED_LIKELY`. Đây là nhãn vận hành, không phải chẩn đoán tâm lý.

Thứ tự ưu tiên: fatigue/safety → chặn frustration spiral → repair knowledge → competence → challenge → autonomy → reward.

## 11. Safety acceptance criteria

Một tính năng chỉ được release nếu:
- Không trừng phạt trẻ vì nghỉ.
- Không cần mua để tiếp tục học.
- Không so sánh với trẻ khác.
- Không gây áp lực bằng mất mát giả.
- Không tăng session length để tối ưu retention.
- Có thể tắt animation/audio reward.
- Parent có toàn quyền giới hạn thời gian.