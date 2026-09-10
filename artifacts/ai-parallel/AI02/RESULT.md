# AI02 — RESULT

Ngày: 2026-09-11

Worktree: `D:\APP HOC TAP.ai\AI02`

Branch: `ai02-layout-visual-dpi`

## Trạng thái

AI02 hoàn thành phần sở hữu: chống ảnh đè chữ, text bị cắt ở CTA, sai client-size khi DPI/resize, width của FlowLayoutPanel ăn scrollbar, và bổ sung regression/gate để các lỗi này không quay lại.

Không còn blocker trong phần code AI02 sở hữu.

## Thay đổi đã triển khai

- `ChildActionButton`: bỏ `EndEllipsis` mặc định cho CTA; dùng wrap + fitted font có ngưỡng tối thiểu, vẫn cho opt-in ellipsis qua `AllowTextEllipsis` khi thật sự là metadata ngắn.
- `ChildVisualTheme`: thêm `MeasureWrappedTextHeight` và `DrawFittedText`.
- `ChildArtTextCardLayout`: tách `ArtHost` và `ContentHost`, có min/max art width và minimum content width để artwork không lấn text khi resize.
- `GameAssetLibrary.DrawContain`: clip đúng `bounds`, lưu/restore toàn bộ Graphics state để bicubic interpolation không bleed sang cột kế bên.
- `ChildWindowSizing`: sửa `MinimumSize` theo semantics outer-window/client-area; đảm bảo contract 900x640 là client area thật và tính lại khi `DpiChanged`.
- `AvailableVerticalFlowChildWidth`: tính width con có trừ padding/vertical scrollbar.
- `MathWorldPage.ResizeLessonNodes`: dùng helper mới để danh sách bài dọc không ăn vào scrollbar.
- `LearnerDesignTokens`: bổ sung token kích thước art/content dùng chung.
- `ChildLayoutDiagnostics`: DEBUG-only trace cho bounds bất thường và sibling overlap.
- `ChildUiRuntimeSmoke`: bổ sung regression cho CTA tiếng Việt dài, minimum client area, art/text separation, resize hẹp/rộng, icon 24/32/48/96, clipping, DPI resolver 125%/150%, và thêm viewport 1024x768 + 1366x768 cho các route chính.

## Gate đã chạy

- `WAHUKidsLearn.csproj` Release x86 rebuild: PASS.
- `ChildUiRuntimeSmoke` Debug x86 build: PASS.
- `ChildUiRuntimeSmoke` Release runtime: PASS.
- Official `Capture-ChildUiOffscreen.ps1`: PASS, 39 PNG.
- Capture run: `CHILD_UI_RUNTIME_SMOKE_PASS assertions=10279`.
- `TypingSpaceFormRuntimeSmoke`: PASS, 91 assertions.
- `git diff --check`: PASS.

## Viewport / DPI

Runtime visual/bounds matrix đã chạy cho các route Home, Typing Space, Math World, Rescue Map, Lesson Play tại:

- 900x640
- 1024x768
- 1180x760
- 1366x768

DPI resolver regression:

- 125% / DPI 120: logical 900x640 -> Compact, 1024x768 -> Standard, 1180x760 -> Standard, ~1366x768 -> Wide: PASS.
- 150% / DPI 144: logical 900x640 -> Compact, 1180x760 -> Standard: PASS.

Máy test thực tế đang ở DPI 96 / 100%, nên AI02 không đổi global Display Scale trong lúc các AI khác đang chạy song song. Vì vậy các case DPI 120/144 là regression của resolver/sizing primitives, không được khai báo sai thành native-monitor screenshot.

## Visual audit

Đã xem ảnh gốc offscreen, không chỉ nhìn kết quả build:

- Home 900x640: hero artwork và copy tách sạch, CTA không bị che.
- Typing Space 900x640: header, HUD, canvas, keyboard không chồng nhau ở DPI 100%.
- Math World 900x640: body 2 cột sạch; vertical lesson list không lấn scrollbar sau integration fix.
- Rescue Map 900x640: mission art không lấn title/intro/checkpoint/CTA.
- Lesson Play 900x640: prompt wrap đúng, input và CTA không bị artwork che.

## Finding giao cho AI khác

Các finding cross-feature đã ghi đầy đủ trong `CROSS_PAGE_FINDINGS.md`, không sửa sâu để tránh giẫm worktree song song:

- AI03: Math World chapter strip và Rescue Map event strip còn horizontal partial-card ở 900px; `MathLessonForm` còn same-cell + `BringToFront()`.
- AI04: prompt/support/feedback trong `LessonPlayPage` còn `AutoEllipsis=true`.
- AI08: Typing Space header còn nhiều cột absolute-width; target/feedback còn ellipsis.
- AI09: Parent Dashboard còn minimum outer-size hard-code, summary ellipsis và row action fixed-height.
- AI01: Home mission summary vẫn là ellipsis cho nội dung mô tả dài.

Các finding trên là handoff cho owner tương ứng, không phải blocker của phần primitive/layout-core AI02 đã hoàn thành.

## Commit

- `8175b09` — `AI02-core: chống đè chữ và lỗi DPI trong layout lõi`
- `a2eaddf` — `AI02-tests: mở rộng regression overlap DPI và viewport`
- `d816c1d` — `AI02-integration: giữ danh sách Math trong vùng cuộn`

Báo cáo audit được commit riêng để integration có thể cherry-pick core/tests/integration và tài liệu độc lập.

## Artifact

- `OVERLAP_AUDIT.md`
- `DPI_MATRIX.md`
- `CROSS_PAGE_FINDINGS.md`
- `RESULT.md`

## Kết luận

AI02 đạt DONE theo phạm vi được giao: code đã triển khai, regression đã bổ sung, runtime/capture gate đã PASS, visual audit đã thực hiện, finding cross-page đã handoff có file/line/owner rõ ràng, và thay đổi được tách commit để merge song song an toàn.
