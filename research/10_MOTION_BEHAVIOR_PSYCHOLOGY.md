# 10 — MOTION × BEHAVIORAL PSYCHOLOGY

Cập nhật: 2026-09-06

## 1. Hoạt ảnh không phải phần trang trí mặc định

Trong app học tập, motion chỉ có giá trị nếu nó làm ít nhất một việc sau:

- **SIGNAL:** hướng chú ý tới phần liên quan;
- **EXPLAIN:** biểu diễn thay đổi/quy trình mà ảnh tĩnh khó thể hiện;
- **FEEDBACK:** cho trẻ biết thao tác vừa được ghi nhận;
- **ORIENT:** giúp hiểu màn hình/đối tượng vừa chuyển từ đâu tới đâu;
- **SOCIAL:** companion dùng ánh mắt/cử chỉ nhẹ để hướng chú ý, khi thật sự cần.

Nếu không đạt một mục trên → xem như `DECORATIVE`.

## 2. Tại sao phải chặn decorative motion trong lúc học

Meta-analysis 2026 về seductive details (50 studies, 177 effect sizes) báo cáo tác động âm nhỏ lên overall learning, comprehension, recall và transfer; phân tích cho rằng extraneous cognitive load là cơ chế trung gian chính.

Nguồn:
https://doi.org/10.1007/s10648-025-10099-z

Một nghiên cứu về decorative animations cũng thấy recall kém hơn khi nội dung đi cùng animation trang trí so với still image.

Nguồn:
https://pubmed.ncbi.nlm.nih.gov/32628527/

Runtime rule:
- khi câu hỏi xuất hiện → background idle pause hoặc giảm còn mức gần tĩnh;
- không có particle loop quanh đáp án;
- không có companion nhảy múa trong lúc trẻ đọc/nghe;
- reward animation chỉ chạy SAU khi response đã xử lý xong.

## 3. Motion hướng chú ý có bằng chứng tốt hơn motion trang trí

Meta-analysis signaling trong multimedia learning cho thấy cue/signaling thường giúp retention/transfer và có thể giảm cognitive load.

Nguồn:
- https://www.sciencedirect.com/science/article/pii/S1747938X17300581
- meta-analysis 2026 cue interventions: https://www.frontiersin.org/journals/psychology/articles/10.3389/fpsyg.2026.1717604/full

Ứng dụng:
- highlight hàng đơn vị đang xử lý;
- pulse MỘT lần ở block cần kéo;
- đường dẫn ngắn từ vật thể → phương trình;
- companion nhìn/chỉ vào target thay vì đứng nhảy vô nghĩa.

Không stack quá nhiều cue cùng lúc. Meta-analysis 2026 cho thấy hiệu quả phụ thuộc cue type/context; combined cues không mặc định tốt hơn.

## 4. Emotional design: dễ thương có ích nếu không chiếm bài

Meta-analysis về pleasant colors/anthropomorphic elements tìm thấy hiệu ứng tích cực nhỏ–vừa cho một số learning outcomes; systematic review 2024 cho thấy kết quả learning còn không hoàn toàn nhất quán nhưng motivation/emotional state thường tích cực hơn.

Nguồn:
- https://www.sciencedirect.com/science/article/abs/pii/S1747938X18302148
- https://link.springer.com/article/10.1007/s10639-024-12823-8

Rule:
- dùng màu dễ chịu, companion có mặt/cử chỉ thân thiện;
- companion KHÔNG được to hơn nội dung học;
- trong `FLOW_LIKELY`, companion gần như đứng yên;
- companion chỉ cue khi có learning reason.

## 5. Behavior-state → Motion-state

### READY
- motion nhẹ cho orientation;
- idle companion rất chậm hoặc sprite 6–10 fps;
- không loop nhiều vùng.

### FLOW_LIKELY
- tắt `DECORATIVE`;
- giữ feedback tối thiểu;
- không popup reward giữa chuỗi câu.

### BORED_OR_UNDERCHALLENGED
- không tăng dopamine bằng particle;
- đổi representation/context;
- có thể dùng một transition mới để báo “mức mới”, rồi trở về yên tĩnh.

### STRAINED
- giảm motion tổng;
- dùng một cue rõ ràng để chỉ bước tiếp theo;
- instructional animation theo từng bước, có thể replay.

### FRUSTRATED_LIKELY
- tuyệt đối không shake đỏ / buzzer mạnh;
- chuyển sang worked example;
- animation giải thích chậm, từng bước;
- sau success recovery chỉ dùng feedback ấm, ngắn.

### FATIGUED_LIKELY
- `decorative_motion = OFF`;
- không có celebration dài;
- ưu tiên đóng phiên nhẹ nhàng.

## 6. Motion taxonomy runtime

Mọi animation phải có metadata:

```text
motion_id
class = INSTRUCTIONAL | SIGNAL | FEEDBACK | NAVIGATION | DECORATIVE
essential = true/false
state_allowlist
max_duration_ms
loop = none | finite | idle
can_reduce = true/false
can_disable = true/false
cpu_tier = low | medium | high
```

BehaviorController có quyền hủy/pause animation nếu state không cho phép.

## 7. Timing policy — engineering heuristic, không coi là định luật tâm lý

Các mốc dưới đây là target để test trên máy thật, không tuyên bố là universal scientific optimum:

- press feedback: ~80–140 ms;
- simple UI transition: ~120–220 ms;
- page/mission transition: ~180–300 ms;
- success micro-celebration: ~250–450 ms;
- instructional animation: dài đúng mức cần giải thích, thường 400–1200+ ms và phải pause/replay được nếu có nhiều bước;
- decorative idle: không được quyết định pacing học.

Nếu máy drop frame → rút hiệu ứng trước, KHÔNG giảm responsiveness của input.

## 8. Reduced Motion

W3C WCAG 2.3.3 khuyến nghị cho phép tắt motion không thiết yếu vì motion có thể gây mất tập trung hoặc khó chịu ở một số người.

Nguồn:
https://www.w3.org/WAI/WCAG21/Understanding/animation-from-interactions

Desktop app cần setting:

```text
Motion: Normal / Reduced / Minimal
```

- `Normal`: instructional + signal + restrained feedback + idle.
- `Reduced`: bỏ travel/zoom lớn, giảm idle, giữ essential cue.
- `Minimal`: chỉ motion cần truyền thông tin; còn lại instant/fade rất nhẹ.

Parent Mode có thể khóa profile.

## 9. Companion behavior

Companion được dùng như pedagogical/social cue nhẹ, không phải mascot retention engine.

Cho phép:
- nhìn về target;
- gật đầu nhẹ;
- cử chỉ chỉ dẫn khi trẻ cần cue;
- ngồi chờ yên khi trẻ đang suy nghĩ.

Cấm:
- khóc vì trẻ thoát app;
- nhắc “con bỏ mình à?”;
- buồn khi mất streak;
- nhảy liên tục;
- nói quá nhiều;
- che hoặc cạnh tranh với bài học.

## 10. Reward motion

Reward không chạy theo variable-ratio/random-chest.

Pattern:

```text
learning event
→ xác nhận nhỏ
→ nếu milestone thật: 1 celebration ngắn
→ quay về calm state
```

Không:

```text
correct click
→ coin explosion
→ roulette
→ chest
→ streak
→ countdown
→ one-more-task
```

## 11. Performance cũng là tâm lý học

UI lag làm mất cảm giác control và tăng khả năng trẻ click lặp/đoán. Vì vậy responsiveness ưu tiên hơn độ mượt thị giác.

Runtime priority:

```text
INPUT > AUDIO/QUESTION > ESSENTIAL SIGNAL > NAVIGATION > FEEDBACK > DECORATIVE
```

Khi performance xấu, scheduler giảm/tắt từ phải sang trái.

## 12. Release gate

Một animation chỉ được release khi trả lời được:

1. Nó phục vụ learning/feedback/orientation gì?
2. Khi tắt nó, kiến thức có mất không?
3. Nó có chạy trong lúc trẻ phải đọc/nghe/suy nghĩ không?
4. Behavior state nào cho phép?
5. Reduced Motion xử lý ra sao?
6. Máy Win7 LOW tier chạy có drop input/frame không?
7. Có seductive-detail risk không?

Không trả lời được → HOLD hoặc DECORATIVE-OFF-DURING-LEARNING.
