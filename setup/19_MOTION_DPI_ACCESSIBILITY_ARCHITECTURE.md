# 19 — MOTION TIMING, DPI & ACCESSIBILITY ARCHITECTURE

Cập nhật: 2026-09-06
Trạng thái: V1 DESIGN LOCK — implementation/test pending.

## 1. FPS là render cap, không phải Timer promise

Microsoft ghi `System.Windows.Forms.Timer` là single-threaded và độ chính xác bị giới hạn khoảng 55 ms. Vì vậy:

```text
LOW 18 FPS
NORMAL 30 FPS
```

là **render/presentation caps**, tuyệt đối không triển khai bằng giả định `Timer.Interval = 1000 / FPS` sẽ chính xác.

## 2. MotionScheduler timing model

Elapsed time source:
- `System.Diagnostics.Stopwatch`;
- ghi diagnostics `Stopwatch.IsHighResolution`.

Scheduler requirements:
- một logical scheduler;
- wake-up source không phải source-of-truth cho thời gian;
- animation state tính từ elapsed monotonic time;
- tối đa một UI invalidate/update request pending;
- coalesce nhiều wake-up;
- UI bận → skip frame trung gian, không queue backlog;
- endpoint deterministic;
- input/answer commit luôn ưu tiên hơn render.

Background callback không được trực tiếp mutate WinForms control; marshal về UI thread.

## 3. Rendering

Custom animated controls:
- ưu tiên built-in double buffering (`DoubleBuffered`/`OptimizedDoubleBuffer`);
- invalidate vùng cần vẽ thay vì toàn form khi hợp lý;
- cache asset bounded;
- `Bitmap`, `Image`, `Graphics`, brush/pen tự tạo phải dispose đúng lifetime;
- soak test theo dõi working set + GDI object growth.

## 4. Win7 DPI model

V1 dùng **system-DPI awareness**.

Manifest baseline:
```xml
<dpiAware>true</dpiAware>
```

Không dựa vào:
- Per-Monitor V2;
- modern WinForms high-DPI enhancements yêu cầu Windows 10.

Test bắt buộc:
- 96 DPI;
- 120 DPI / 125%;
- 1024×768;
- 1366×768;
- text Việt dài;
- no clipping/overlap.

`AutoScaleMode=Dpi` chỉ giữ nếu visual regression trên Win7 pass.

## 5. Pointer target

WCAG 2.2 web baseline có minimum 24×24 CSS px; WAHU **không lấy 24 làm child target**.

Engineering target WAHU:
- primary child target ~64 px+ @96 DPI;
- secondary ~56 px+ khi có thể;
- spacing đủ để không misclick.

WCAG dùng làm safety floor/reference; child UX target của WAHU cố ý lớn hơn.

## 6. Dragging

Mọi chức năng drag không thiết yếu phải có cách single-pointer không-drag tương đương.

Ví dụ:
- drag item vào nhóm → click item rồi click nhóm;
- kéo object vào vị trí → click object + nút trái/phải hoặc target lớn;
- calibration drag không được là gate bắt buộc để dùng app.

Precision drag nhỏ bị cấm ở Child Mode.

## 7. Motion accessibility

Modes:
- Normal;
- Reduced;
- Minimal.

Rules:
- auto decorative motion off khi read/listen/think;
- nonessential interaction motion có thể disable;
- auto-moving content kéo dài phải pause/stop/hide hoặc được policy chặn;
- Reduced/Minimal không được xóa thông tin instructional;
- no flashing failure feedback.

## 8. WinForms accessibility semantics

Ưu tiên standard controls khi chức năng tương đương.

Custom semantic control phải review:
- `AccessibleName`;
- `AccessibleRole`;
- `AccessibleDescription` khi thực sự bổ sung thông tin;
- keyboard path nơi phù hợp;
- focus state nhìn thấy ở Parent Mode;
- không dùng `PictureBox + MouseClick` như button mà không có semantic equivalent.

## 9. High contrast / system colors

Parent/diagnostic surfaces cần test Windows High Contrast/Classic appearance. Child theme có thể custom nhưng thông tin/correctness không được dựa duy nhất vào màu.

## 10. Tests

- timing drift under UI load;
- wake-up burst coalescing;
- no queued animation backlog;
- input latency while 2 regions animate;
- double-buffer flicker test;
- GDI leak soak 30–60 min;
- DPI 96/120 visual regression;
- drag alternative E2E;
- accessibility semantic inspection;
- Normal/Reduced/Minimal behavior matrix.
