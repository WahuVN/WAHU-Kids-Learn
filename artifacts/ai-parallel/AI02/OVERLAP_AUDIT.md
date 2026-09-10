# AI02 — OVERLAP / OVERFLOW AUDIT

Ngày: 2026-09-11

Worktree: `D:\APP HOC TAP.ai\AI02`

Branch: `ai02-layout-visual-dpi`

## Phạm vi

AI02 audit các lỗi ảnh đè chữ, chữ bị cắt, control đè nhau, fixed-size gây overflow, FlowLayoutPanel ăn scrollbar, resize/DPI làm mất CTA, và các workaround kiểu `BringToFront()`.

## Lỗi lõi đã sửa

1. `ChildActionButton` trước đây luôn vẽ text với `EndEllipsis`, kể cả CTA quan trọng. Đã đổi sang text fit/wrap; ellipsis chỉ còn opt-in qua `AllowTextEllipsis`. Font chỉ co đến ngưỡng đọc được (`MinimumTextPointSize`, mặc định 8.5pt).
2. Thêm `ChildVisualTheme.MeasureWrappedTextHeight` và `DrawFittedText` để đo/wrap text thống nhất, tránh mỗi màn tự giảm font vô hạn.
3. Thêm `ChildArtTextCardLayout` với `ArtHost` và `ContentHost` tách cột thật, có min/max art width + minimum content width; test resize 180x120, 320x160, 900x300 không sinh bounds âm và không lấn cột.
4. `GameAssetLibrary.DrawContain` nay `SetClip(bounds)` và restore toàn bộ Graphics state. Bicubic sampling không thể bleed sang vùng text cạnh bên.
5. `ChildWindowSizing.ApplyLearnerWindowDefaults` đã sửa semantics: `MinimumSize` là outer-window size, trong khi contract 900x640 là client area. Helper giờ cộng đúng non-client chrome và tính lại khi `DpiChanged`.
6. Thêm `AvailableVerticalFlowChildWidth` để FlowLayoutPanel dọc trừ padding + vertical scrollbar trước khi gán width con. Đã áp dụng một integration line vào `MathWorldPage.ResizeLessonNodes`.
7. Radius mặc định của card/button được lấy từ `LearnerDesignTokens`; thêm token min/max cho art/content.
8. Thêm `ChildLayoutDiagnostics` chỉ ở DEBUG để báo `[UI-BOUNDS]` và `[UI-OVERLAP]` trong cây learner UI; Release không mang chi phí call-site.

## Audit hình ảnh thực từ offscreen capture

Đã xuất 39 PNG và kiểm tra ảnh gốc, không chỉ dựa vào build.

| Màn | 900x640 | Kết quả AI02 |
|---|---|---|
| Home | Art hero và copy tách sạch, CTA dưới copy không bị che | PASS |
| Typing Space | Header/HUD/canvas/keyboard không chồng ở 100% DPI | PASS ở 100%; rủi ro DPI thật ghi cho AI08 |
| Math World | Body 2 cột sạch; vertical lesson list không ăn scrollbar | PASS phần lõi; chapter strip còn overflow ngang có chủ đích/partial item |
| Rescue Map | Mission art không lấn title/intro/checkpoints/CTA | PASS phần art-text; event strip còn partial item ở bên phải |
| Lesson Play | Prompt xuống dòng, answer host và bottom CTA không bị che | PASS |

Ở 1366x768 smoke tree không phát hiện sibling overlap/out-of-parent trái phép trong các route đã đưa vào ma trận.

## Pattern nguy hiểm còn thấy nhưng không sửa sâu vì thuộc owner khác

- `MathLessonForm.cs:353-355`: `_typedAnswerSurface` và `_typedAnswerBox` được add cùng cell `(1,0)` rồi `BringToFront()`. Đây đúng pattern AI02 cấm dùng làm bandaid; giao AI03/AI04 refactor thành host/card chứa textbox.
- `MathWorldPage.cs:129-139, 363-380`: chapter strip horizontal `AutoScroll`, button fixed 148x44; ở 900px ảnh gốc hiện item cuối bên phải bị cắt một phần. Giao AI03.
- `RescueMapPage.cs:224-234, 289-300`: event strip horizontal `AutoScroll`, mission button fixed 196x58; ở 900px item thứ 5 bị cắt một phần. Giao AI03.
- `LessonPlayPage.cs:258-305`: prompt/support/feedback vẫn `AutoEllipsis=true`, trái rule “prompt/hint/error/action phải wrap/fit”. Giao AI04.
- `TypingSpaceForm.cs:210-215`: header có 4 cột Absolute (205/170/126/100) và main target/feedback vẫn ellipsis. Giao AI08.
- `ParentDashboardForm.cs:41,49,53`: minimum outer size hard-code, action row 180px, summary ellipsis. Giao AI09.

Chi tiết handoff nằm trong `CROSS_PAGE_FINDINGS.md`.

## Kết luận

Các primitive AI02 sở hữu đã được harden để ngăn ảnh đè chữ, CTA tự cắt text, sai client-size và width ăn scrollbar. Các overflow còn lại đều đã xác định đúng file/line và owner; AI02 không sửa sâu cross-feature để tránh giẫm worktree của AI03/04/08/09.
