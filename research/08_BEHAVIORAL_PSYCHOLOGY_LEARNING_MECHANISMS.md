# 08 — BEHAVIORAL PSYCHOLOGY & COMFORT-FIRST LEARNING MECHANISMS

Cập nhật: 2026-09-06

## 1. Mục tiêu thiết kế

Mục tiêu không phải tối đa hóa thời gian trong app. Mục tiêu là tối đa hóa **chất lượng học trong trạng thái tâm lý dễ chịu**: trẻ cảm thấy có quyền kiểm soát, hiểu mình đang làm gì, thấy mình có tiến bộ, sai nhưng không xấu hổ, và kết thúc buổi học trước khi bị quá tải.

App không chẩn đoán cảm xúc/tâm lý. App chỉ suy luận **learning state** từ hành vi quan sát được trong phiên học.

## 2. Nền tảng tâm lý chính

### 2.1 Self-Determination Theory — autonomy / competence / relatedness

Cơ chế cốt lõi:
- **Autonomy:** trẻ có lựa chọn có giới hạn và có ý nghĩa.
- **Competence:** bài vừa sức, feedback làm trẻ biết bước tiếp theo.
- **Relatedness:** companion/giọng hướng dẫn mang cảm giác đồng hành, không đánh giá con người trẻ.

Rule runtime:
- mỗi block học nên có tối đa 1–2 lựa chọn thật;
- lựa chọn không làm mất chuẩn đầu ra;
- cho trẻ chọn thứ tự, chủ đề bối cảnh, companion hoặc dạng hoạt động khi nhiều lựa chọn đều đạt cùng skill;
- không giả lựa chọn nếu hệ thống đã quyết định một phương án duy nhất.

Nguồn:
- Niemiec & Ryan (2009), autonomy/competence/relatedness in education.
- Systematic review/meta-analysis SDT interventions in education (2024/2026 literature).

## 3. Flow nhưng không biến thành game gây nghiện

Flow trong học tập thường liên quan:
- challenge–skill balance;
- mục tiêu rõ;
- feedback rõ và kịp thời;
- cảm giác kiểm soát.

Rule:
- không cố giữ trẻ ở trạng thái kích thích cao;
- target là **calm focus**, không phải hyper-arousal;
- khi challenge quá thấp → tăng novelty/transfer trước khi tăng số lượng;
- khi challenge quá cao → scaffold hoặc lùi prerequisite;
- nếu trẻ đã mệt → kết thúc tốt đẹp thay vì cố “cứu streak”.

## 4. Behavioral Learning State Model

Các trạng thái là operational labels, không phải chẩn đoán tâm lý.

### READY
Tín hiệu:
- phản hồi ổn định;
- chưa có chuỗi lỗi;
- input có chủ đích.

Hành động:
- lesson bình thường.

### FLOW_LIKELY
Tín hiệu gợi ý:
- độ chính xác ổn;
- response time không quá nhanh/không quá chậm so với baseline cá nhân;
- hint thấp;
- trẻ tiếp tục chủ động;
- không có rapid guessing.

Hành động:
- giữ challenge;
- không chen reward/animation làm gián đoạn;
- feedback tối giản.

### BORED_OR_UNDERCHALLENGED
Tín hiệu:
- đúng gần như tuyệt đối;
- response cực nhanh liên tục;
- skill đã stable;
- lặp cùng representation.

Hành động theo thứ tự:
1. đổi representation;
2. thêm transfer/context;
3. giảm scaffold;
4. tăng difficulty feature nhỏ;
5. chuyển skill nếu mastery đã đủ.

Không giải pháp bằng cách spam thêm 20 câu giống nhau.

### STRAINED
Tín hiệu:
- response time tăng rõ;
- 1–2 lỗi gần nhau;
- hint tăng;
- backtracking nhiều.

Hành động:
- giảm lượng chữ;
- đưa cue nhỏ;
- đổi sang representation trực quan;
- giữ mục tiêu nhưng giảm extraneous load.

### FRUSTRATED_LIKELY
Tín hiệu tổ hợp:
- lỗi lặp cùng pattern;
- max hint;
- rapid wrong clicks;
- skip/exit tăng;
- response time dao động lớn.

Hành động:
1. không báo “sai” mạnh;
2. worked example tương tự;
3. prerequisite repair ngắn;
4. một câu thành công có ý nghĩa;
5. quay lại target hoặc break.

### FATIGUED_LIKELY
Tín hiệu:
- toàn bộ response chậm dần qua nhiều skill;
- lỗi không còn pattern kiến thức rõ;
- click miss tăng;
- hiệu suất giảm trên cả skill đã vững;
- session gần/qua time budget.

Hành động:
- không hạ mastery vì fatigue suspected;
- đề nghị nghỉ/kết thúc;
- review còn lại dời sang lần sau.

## 5. Tách knowledge error khỏi state error

Đây là rule quan trọng.

Ví dụ:
- nếu trẻ sai carry liên tục nhưng vẫn phản hồi ổn → khả năng knowledge gap;
- nếu trẻ bắt đầu sai cả câu cực dễ từng làm tốt → fatigue/attention state đáng nghi hơn;
- không dùng 1 lỗi đơn để kết luận frustration;
- state inference phải dùng rolling window và confidence.

Mỗi decision cần:
- `state_label`;
- `confidence`;
- `evidence_features`;
- `action_taken`;
- `reversible = true`.

## 6. Cognitive Load Controller

Nguyên tắc:
- giữ **intrinsic load** cần thiết cho skill;
- giảm **extraneous load** do UI, chữ dài, animation, memory juggling không liên quan;
- scaffold phần cần thiết rồi fade dần.

Rule UI/lesson:
- một nhiệm vụ chính/màn hình;
- không hiển thị instruction dài + hình + animation + voice cùng lúc nếu không cần;
- chia bài nhiều bước thành từng bước;
- worked example cho skill mới/khó;
- không bắt trẻ nhớ số liệu ở màn hình trước để giải câu hiện tại nếu đó không phải skill mục tiêu.

## 7. Feedback Psychology

Feedback tốt trả lời:
1. Điều gì vừa xảy ra?
2. Bước tiếp theo là gì?

Ưu tiên feedback:
- ngắn;
- cụ thể;
- actionable;
- không phán xét nhân cách.

Ví dụ tốt:
- “Con cộng hàng đơn vị đúng rồi. Giờ nhớ thêm 1 chục nhé.”
- “Cách này chưa ra. Mình thử dùng khối chục nhé.”
- “Lần này con tự làm mà không cần gợi ý.”

Tránh:
- “Con thông minh quá.”
- “Câu này dễ mà.”
- “Sai nữa rồi.”
- “Cố lên!” lặp vô nghĩa không chỉ ra chiến lược.

Nghiên cứu về praise cho thấy praise có thể tốt/xấu tùy cách dùng; person/trait praise có thể làm trẻ phản ứng kém hơn khi gặp thất bại. Vì vậy engine ưu tiên **process/strategy/informational feedback**.

## 8. Reward Mechanism

Gamification có thể cải thiện động lực nhưng hiệu ứng trung bình nhỏ và novelty/extrinsic reward có thể giảm theo thời gian.

Rule:
- reward **low-stakes**;
- reward không quyết định trẻ “có giá trị” hay không;
- reward gắn với progress/milestone, không mỗi click;
- cho trẻ một chút quyền chọn reward/cosmetic;
- không random variable-ratio;
- không loss aversion;
- không daily chest cần login;
- không streak bị reset;
- không leaderboard.

Reward ladder:
```text
Immediate: feedback hiểu bài
→ Session: world progress nhỏ
→ Mastery milestone: unlock/cosmetic
→ Long term: thấy rõ mình đã làm được điều trước đây chưa làm được
```

## 9. Choice Architecture

Choice có thể tăng intrinsic motivation ở trẻ tiểu học khi là lựa chọn có ý nghĩa.

Rule:
- ưu tiên ít lựa chọn có ý nghĩa (thường 2 lựa chọn rõ ràng) thay vì đưa quá nhiều lựa chọn cùng lúc;
- app tự chọn default tốt;
- mọi lựa chọn đều an toàn và đạt mục tiêu học;
- không hỏi trẻ những lựa chọn kỹ thuật như “difficulty 1–10”.

Ví dụ:
- “Học Toán trước hay English trước?”
- “Con muốn luyện bằng khối số hay tia số?” khi cả hai đều phù hợp phase;
- “Panda hay Rồng đi cùng?”

## 10. Error Recovery Loop

```text
attempt
→ feedback nhỏ
→ retry có cue
→ đổi representation
→ worked example
→ prerequisite repair
→ success recovery
→ target revisit
```

`success recovery` không phải câu vô nghĩa quá dễ; nó phải liên quan prerequisite hoặc chiến lược vừa sửa để trẻ nhận lại cảm giác competence thật.

## 11. Session Rhythm

Một buổi học không cố định cứng theo phút nhưng có rhythm:

```text
warm start
→ focus block
→ micro-reset
→ focus block
→ recall
→ closure
```

Warm start:
- 1–3 câu child can likely succeed;
- mục tiêu tạo orientation và competence, không fake praise.

Micro-reset:
- animation ngắn, thay activity modality, hoặc đứng dậy nghỉ;
- không biến thành reward game dài.

Closure:
- kết thúc khi vẫn còn cảm giác tốt;
- không autoplay một quest nữa.

## 12. Warm Start nhưng không “dễ giả tạo”

Warm-up lấy từ:
- skill đã vững nhưng đến hạn review nhẹ;
- prerequisite gần target;
- không đưa câu dễ vô nghĩa chỉ để tạo dopamine.

Mục tiêu:
- khởi động retrieval;
- tạo cảm giác competence có thật;
- chuẩn bị mental model cho bài mới.

## 13. Không sử dụng các behavioral dark patterns

Cấm:
- variable-ratio rewards;
- random surprise chest nhằm tăng retention;
- streak loss;
- fake scarcity;
- countdown giả;
- companion khóc/buồn vì trẻ nghỉ;
- social comparison;
- shame;
- forced continuation;
- “one more task” loop vô hạn;
- nhiệm vụ dễ bất tận để kéo session length.

## 14. Telemetry nội bộ tối thiểu cho controller

Không cần camera.
Không cần micro emotion recognition.
Không biometric.

Chỉ cần:
- answer correctness;
- response time;
- click timing;
- hint usage;
- retry count;
- skip/pause/exit;
- representation;
- difficulty;
- known mastery;
- session elapsed;
- input miss/error rate nếu đo được an toàn.

## 15. Behavior Controller Decision Priority

Thứ tự ưu tiên:

```text
1. Safety / fatigue
2. Prevent frustration spiral
3. Repair knowledge gap
4. Preserve competence
5. Maintain suitable challenge
6. Support autonomy
7. Reward/game polish
```

Reward luôn đứng sau learning state.

## 16. Nguồn nghiên cứu chính

- Niemiec, C.P. & Ryan, R.M. (2009), Autonomy, competence, and relatedness in the classroom: https://doi.org/10.1177/1477878509104318
- SDT intervention systematic review/meta-analysis: https://www.sciencedirect.com/science/article/pii/S0023969024000572
- Learning flow meta-analysis (2026): https://pmc.ncbi.nlm.nih.gov/articles/PMC13441015/
- Challenge–skill balance meta-analysis: https://doi.org/10.1080/17439760.2014.967799
- Gamification intrinsic motivation meta-analysis: https://link.springer.com/article/10.1007/s11423-023-10337-7
- Reward strategies systematic review (2026): https://doi.org/10.1016/j.edurev.2026.100766
- Gamification motivation systematic review: https://pubmed.ncbi.nlm.nih.gov/37636393/
- Praise and intrinsic motivation review: https://pubmed.ncbi.nlm.nih.gov/12206194/
- Praise for intelligence and motivation: https://pubmed.ncbi.nlm.nih.gov/9686450/
- EEF Feedback: https://educationendowmentfoundation.org.uk/education-evidence/teaching-learning-toolkit/feedback
- IES Elementary Mathematics practice guide: https://ies.ed.gov/ncee/wwc/practiceguide/26
- Spacing and retrieval review: https://www.nature.com/articles/s44159-022-00089-1
