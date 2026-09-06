# 08 — UI, DPI, ACCESSIBILITY & MOTION DEFAULTS

## 1. Baseline

- Design canvas: 1024×768 @ 96 DPI.
- Scale up được 1366×768+.
- Không cần scroll cho primary lesson flow.
- Win7 dùng system DPI; không phụ thuộc per-monitor DPI V2.

## 2. Font

Primary UI:
- Segoe UI nếu có;
- fallback Arial/sans-serif.

Child prompt phải lớn hơn parent utility text.

Không dùng font custom bắt buộc nếu làm tăng installer/compatibility mà không có lợi rõ.

## 3. Control size

Engineering minimum:
- primary child target: khoảng 64 px trở lên ở 96 DPI;
- secondary target: không nhỏ hơn ~56 px nếu có thể;
- khoảng cách đủ để tránh misclick.

Không yêu cầu:
- double click;
- right click;
- hover-only discovery;
- precision drag nhỏ.

## 4. Color/contrast

- Không dùng màu là tín hiệu duy nhất.
- Text/body có contrast rõ.
- Correct/incorrect đi kèm icon/text/shape.
- Không flash đỏ toàn màn hình.

## 5. Motion modes

```text
Normal
Reduced
Minimal
```

BehaviorController có thể tạm giảm motion theo state mà không đổi user preference vĩnh viễn.

`FLOW_LIKELY`: decorative off.
`STRAINED`: một cue rõ.
`FRUSTRATED_LIKELY`: calm, stepwise.
`FATIGUED_LIKELY`: minimal.

## 6. Animation rules

- instructional motion replayable;
- decorative off khi đọc/nghe/suy nghĩ;
- no infinite particles;
- no red shake failure;
- success microcelebration ngắn;
- animation cancellable.

## 7. Input feedback

Press feedback phải nhanh hơn animation đẹp.

Nút sau click:
- đổi state ngay;
- disable double submit;
- animation tiếp theo không block event handling.

## 8. Child text

- instruction một câu ngắn;
- ưu tiên voice + visual khi phù hợp;
- tránh thuật ngữ kỹ thuật;
- no modal error details.

## 9. Parent UI

Có thể nhiều thông tin hơn Child UI nhưng vẫn:
- tiếng Việt rõ;
- trạng thái `Vững / Đang học / Cần ôn`;
- recommendation có lý do;
- advanced diagnostics nằm sâu hơn.

## 10. Keyboard fallback

Optional:
- 1–4 chọn đáp án;
- Enter xác nhận;
- Space nghe lại;
- Esc pause/back có guard.

Primary flow vẫn dùng chuột.

## 11. Win7 DPI

- app manifest: system-DPI aware (`dpiAware=true`);
- test 96 DPI + 120 DPI/125%;
- không dựa vào Per-Monitor V2/Win10-only WinForms high-DPI behavior;
- `AutoScaleMode=Dpi` chỉ giữ nếu visual regression pass;
- test Vietnamese long text/clipping ở 1024×768.

## 12. Dragging accessibility

WCAG 2.2 dùng như reference floor: chức năng drag không thiết yếu phải có phương án single-pointer không drag.

WAHU rule mạnh hơn:
- precision drag nhỏ bị cấm;
- drag activity luôn có click-select → click-target hoặc equivalent;
- calibration drag không là gate bắt buộc.

## 13. WinForms semantics

Ưu tiên standard WinForms controls. Custom semantic control phải có `AccessibleName`, `AccessibleRole` và description khi có giá trị bổ sung.

Không dùng `PictureBox + MouseClick` làm nút duy nhất mà không có semantic/keyboard equivalent.

Parent/diagnostics smoke-test High Contrast/Classic appearance.

Chi tiết: `19_MOTION_DPI_ACCESSIBILITY_ARCHITECTURE.md`.