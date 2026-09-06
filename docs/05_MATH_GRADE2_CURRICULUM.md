# 05 — MATH GRADE 2 CURRICULUM — VERIFIED BASELINE

## 1. Nguồn chuẩn

Baseline này bám yêu cầu cần đạt LỚP 2 trong Chương trình GDPT môn Toán ban hành kèm Thông tư 32/2018/TT-BGDĐT. Chi tiết provenance: `research/01_MOET_MATH_GRADE2_BASELINE.md`.

Không được mở rộng một skill thành “chuẩn lớp 2 bắt buộc” nếu chưa có source hoặc map bộ sách.

## 2. Skill tree V1

### A. Số tự nhiên
- `NUM_COUNT_READ_WRITE_0_1000`
- `NUM_FULL_HUNDREDS_RECOGNIZE`
- `NUM_PREDECESSOR_SUCCESSOR`
- `PLACE_VALUE_HUNDREDS_TENS_ONES`
- `NUM_EXPANDED_FORM_HTO`
- `NUMBER_RAY_FILL`
- `NUM_COMPARE_0_1000`
- `NUM_MIN_MAX_UP_TO_4`
- `NUM_SORT_UP_TO_4`
- `ESTIMATE_OBJECTS_BY_TENS`

### B. Cộng và trừ
- `ADD_COMPONENTS_RECOGNIZE`
- `SUB_COMPONENTS_RECOGNIZE`
- `ADD_WITHIN_1000_NO_CARRY`
- `ADD_WITHIN_1000_ONE_CARRY_MAX`
- `SUB_WITHIN_1000_NO_BORROW`
- `SUB_WITHIN_1000_ONE_BORROW_MAX`
- `ADD_SUB_TWO_OPERATORS_LEFT_TO_RIGHT`
- `MENTAL_ADD_SUB_WITHIN_20`
- `MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000`

### C. Nhân và chia
- `MULTIPLICATION_MEANING`
- `DIVISION_MEANING`
- `MULTIPLICATION_COMPONENTS`
- `DIVISION_COMPONENTS`
- `TIMES_TABLE_2`
- `TIMES_TABLE_5`
- `DIVIDE_TABLE_2`
- `DIVIDE_TABLE_5`

Không gắn bảng 3/4/6/7/8/9 là BGDĐT Grade-2 baseline.

### D. Bài toán thực tế một bước
- `OPERATION_MEANING_FROM_VISUAL`
- `WP_ONE_STEP_ADD_MORE`
- `WP_ONE_STEP_SUB_LESS`
- `WP_ONE_STEP_MORE_THAN`
- `WP_ONE_STEP_LESS_THAN`
- `WP_ONE_STEP_MULTIPLICATION_CONTEXT`
- `WP_ONE_STEP_DIVISION_CONTEXT`
- `WP_SELECT_OPERATION_ONE_STEP`

Không dùng keyword hack làm tiêu chí duy nhất.

### E. Hình học
- `POINT_RECOGNIZE`
- `LINE_SEGMENT_RECOGNIZE`
- `CURVE_RECOGNIZE`
- `STRAIGHT_LINE_RECOGNIZE`
- `POLYLINE_RECOGNIZE`
- `THREE_COLLINEAR_POINTS`
- `QUADRILATERAL_RECOGNIZE`
- `CYLINDER_RECOGNIZE`
- `SPHERE_RECOGNIZE`
- `DRAW_SEGMENT_GIVEN_LENGTH`
- `FOLD_CUT_COMPOSE_SHAPES`

### F. Đo lường
- `HEAVIER_LIGHTER`
- `MASS_KG_READ_WRITE`
- `CAPACITY_LITER_READ_WRITE`
- `LENGTH_DM_M_KM_RECOGNIZE_RELATION`
- `TIME_DAY_24_HOURS`
- `TIME_HOUR_60_MINUTES`
- `CALENDAR_DAYS_IN_MONTH_DATE`
- `MONEY_VND_NOTE_RECOGNITION`
- `MEASURE_WITH_RULER_CM`
- `MEASURE_WITH_COMMON_SCALE`
- `CLOCK_MINUTE_HAND_AT_3_OR_6`
- `MEASUREMENT_CONVERT_CALCULATE_LEARNED_UNITS`
- `MEASUREMENT_ESTIMATE_BASIC`
- `POLYLINE_LENGTH_SUM_SEGMENTS`
- `MEASUREMENT_REAL_WORLD_ONE_STEP`

### G. Thống kê
- `DATA_COLLECT_CLASSIFY_COUNT`
- `PICTOGRAPH_READ_DESCRIBE`
- `PICTOGRAPH_SIMPLE_INFERENCE`

### H. Xác suất sơ khai
- `EVENT_POSSIBLE`
- `EVENT_CERTAIN`
- `EVENT_IMPOSSIBLE`

## 3. Representation library

Có thể dùng khi đúng skill:
- vật thật/objects;
- base-ten blocks;
- number ray/number line;
- equation;
- tranh tình huống;
- đồng hồ vector;
- thước/cân/cốc đo;
- pictograph;
- shape vector.

Mọi representation có số lượng/đơn vị phải sinh từ data để kiểm được.

## 4. Worked-example fading

```text
model đầy đủ
→ model + trẻ hoàn thành bước cuối
→ cue nhỏ
→ independent
→ delayed recall
→ representation transfer
```

Success sau hint không có trọng số mastery bằng independent success.

## 5. Word problem engine

1. Xác định bối cảnh.
2. Xác định dữ kiện.
3. Xác định cần tìm.
4. Biểu diễn bằng hình/sơ đồ nếu cần.
5. Chọn phép tính dựa vào quan hệ, không dựa vào một keyword.
6. Tính.
7. Kiểm tra kết quả có hợp lý.

## 6. Distractor

Distractor dựa trên lỗi có thể giải thích: carry/borrow, place value, chọn sai phép tính, tính nhẩm sai. Không tạo số ngẫu nhiên vô nghĩa chỉ để đủ 4 lựa chọn.

## 7. Mastery gate

Stable khi có bằng chứng gồm:
- nhiều independent success;
- delayed recall;
- transfer representation khi skill phù hợp;
- không còn error pattern prerequisite mạnh.

Không gate chỉ theo `% đúng trong một phiên`.

## 8. Out-of-scope guard

Nếu item chứa nội dung ngoài baseline, phải gắn `SUPPLEMENTARY` hoặc `BOOK_MAPPED`; không được tự mang nhãn `VERIFIED_A`.