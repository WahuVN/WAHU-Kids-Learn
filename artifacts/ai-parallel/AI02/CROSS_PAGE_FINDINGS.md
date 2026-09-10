# AI02 — CROSS PAGE FINDINGS / HANDOFF

Ngày: 2026-09-11

AI02 chỉ sửa helper lõi và một integration line an toàn. Các finding dưới đây thuộc file/feature của AI khác; không sửa sâu trong worktree AI02 để tránh conflict song song.

## P1 — AI03 — Math World chapter strip overflow ngang

File: `src/App/MathWorldPage.cs`

- `129-139`: `_chapterStrip` là `FlowLayoutPanel`, `WrapContents=false`, `AutoScroll=true`, hướng LeftToRight.
- `363-380`: mỗi chapter button fixed `148x44` + margin 4px hai bên.
- Visual 900x640: dải chương chỉ hiện đầy đủ khoảng 5 item, item kế tiếp bị cắt một phần bên phải.

Đề xuất owner: responsive chapter button width / paging / wrap có kiểm soát; không dựa vào partial-card để báo còn nội dung. Giữ target >=48px.

AI02 đã sửa riêng vertical lesson list ở `MathWorldPage.cs:532`: width con lấy từ `AvailableVerticalFlowChildWidth`, có trừ scrollbar/padding.

## P1 — AI03 — Rescue Map mission strip partial card

File: `src/App/RescueMapPage.cs`

- `224-234`: `_eventStrip` LeftToRight + `WrapContents=false` + `AutoScroll=true`.
- `289-300`: mỗi `RescueMissionButton` fixed `196x58`.
- Visual 900x640: item thứ 5 nằm ngoài viewport và một phần card bị cắt ở mép phải.

Đề xuất owner: responsive card width / paging indicator / scroll affordance rõ. Nếu giữ horizontal scroll, tránh để card half-visible giống layout vỡ.

Ngoài ra `eventTitle`, `intro`, `status` đang `AutoEllipsis=true` (`RescueMapPage.cs:142,154,176`). Intro/status là nội dung hướng dẫn, nên wrap/fit thay vì cắt.

## P1 — AI04 — LessonPlay prompt/hint/feedback còn ellipsis

File: `src/App/LessonPlayPage.cs`

- `_prompt`: `AutoEllipsis=true` tại line 265.
- `_support`: `AutoEllipsis=true` tại line 277.
- `_feedback`: `AutoEllipsis=true` tại line 305.

Visual hiện tại ở 900x640 với câu test đang fit, nhưng policy AI02 yêu cầu prompt/hint/error/action không được phụ thuộc ellipsis. Cần row/profile responsive hoặc text-fit helper, không chỉ tăng fixed height tùy ý.

## P1 — AI03/AI04 — same-cell + BringToFront trong MathLessonForm

File: `src/App/MathLessonForm.cs`

- line 353: add `_typedAnswerSurface` vào cell `(1,0)`.
- line 354: add `_typedAnswerBox` vào cùng cell `(1,0)`.
- line 355: `_typedAnswerBox.BringToFront()`.

Đây là pattern nguy hiểm đúng checklist AI02. Đề xuất: `ChildCard/Panel host` chứa TextBox bên trong hoặc card vẽ border/background và child TextBox nằm trong padding thực; không add hai sibling vào cùng TableLayout cell.

Cùng file: prompt có fixed row 68 + `AutoEllipsis=true` (`201-221`), answer grid có horizontal padding 72 (`243-253`), nên cần kiểm lại 125% DPI.

## P1 — AI08 — Typing Space header absolute-width

File: `src/App/TypingSpaceForm.cs`

- row header/bottom fixed `92` và `172` (`196-198`).
- header 5 cột, trong đó 4 cột Absolute `205 / 170 / 126 / 100` (`210-215`).
- `_targetLabel AutoEllipsis=true` line 235.
- `_feedback AutoEllipsis=true` line 298.

Visual 900x640 DPI100 hiện sạch. Rủi ro còn ở native 125%/150% và chuỗi Việt dài. Đề xuất AI08 chuyển ít nhất stats/pause/close sang percent/min-width profile hoặc compact profile riêng.

## P2 — AI01 — Home mission summary dùng ellipsis

File: `src/App/LearnerHomePage.cs`

- `_missionSummary` là mô tả nhiệm vụ quan trọng nhưng `AutoEllipsis=true` line 125.

Visual 900x640 hiện đủ. Vẫn nên đổi sang wrap/dynamic row để text content dài hơn không mất nghĩa.

`_gardenProgress` line 234 có thể giữ ellipsis vì đây là metadata/status ngắn nếu owner xác nhận.

## P2 — AI09 — Parent dashboard sizing/text

File: `src/App/ParentDashboardForm.cs`

- `MinimumSize = new Size(800,600)` line 41 dùng outer-size semantics cũ.
- actions row fixed 180px line 49 với nhiều button Việt dài.
- `_summary AutoEllipsis=true` line 53 dù summary chứa health/backup/update/learning status nhiều dòng.

Đề xuất AI09 dùng helper client-size tương đương `ChildWindowSizing` hoặc logic riêng cho parent mode; summary nên wrap/scroll chứ không cắt.

## Finding đã xác minh là hợp lệ/không cần sửa

- `LearnerRouter.cs:87 _current.BringToFront()` dùng để chọn page hiện hành trong page host và không phải bandaid cho two-controls-same-cell.
- Các visual completion/instruction được đặt cùng Panel nhưng visibility mutually exclusive; capture/bounds gate không báo overlap ở trạng thái đã test.
- Home hero art/copy và LessonPlay visual/question hiện không overlap ở 900x640 sau core hardening.
