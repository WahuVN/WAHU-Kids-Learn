# 09 — GITHUB LIGHTWEIGHT MOTION STACK

Cập nhật: 2026-09-06

## Mục tiêu

Tìm thư viện/skill GitHub giúp app có chuyển động đẹp, rõ, có cảm giác sống nhưng vẫn chạy tốt trên PC Windows 7 cũ. Tiêu chí ưu tiên theo thứ tự:

1. chạy được với Windows 7 SP1 / .NET Framework;
2. ít dependency và ít native DLL;
3. offline;
4. dễ pin version / audit source / license;
5. có thể tắt hoặc giảm motion;
6. không cần Chromium/WebView2/Electron;
7. không làm learning screen thành nơi biểu diễn hiệu ứng.

## Runtime shortlist

### A. `falahati/WinFormAnimation` — ƯU TIÊN THAM KHẢO / CÓ THỂ VENDOR TỐI GIẢN

Repo: https://github.com/falahati/WinFormAnimation
License: MIT.

Điểm phù hợp:
- thiết kế cho WinForms;
- README công bố .NET 3.5+;
- chỉ phụ thuộc `System.Drawing` ở lõi;
- có keyframe/path, easing và FPS limiter;
- đủ cho opacity giả lập, position, size, progress, shake nhỏ, slide/fade logic.

Rủi ro:
- codebase không phải thư viện đang đổi mới liên tục;
- không cần mang nguyên cả sample/project vào production.

Quyết định:
- KHÔNG thêm NuGet bừa ngay.
- Khi skeleton app có thật: review source + pin commit SHA + chỉ vendor phần tween/easing thật sự cần, giữ LICENSE/NOTICE.
- Nếu tự viết `WahuTween` dưới ~500–800 LOC đạt đủ tính năng và test được thì ưu tiên code nội bộ để giảm dependency.

### B. `awaescher/FluentTransitions` — THAM KHẢO, KHÔNG CẦN CẢ HAI TWEEN ENGINE

Repo: https://github.com/awaescher/FluentTransitions
License: MIT.

Điểm mạnh:
- API transition mượt;
- hỗ trợ .NET Framework 4.8;
- dùng được với WinForms.

Quyết định:
- chỉ chọn `WinFormAnimation-derived/custom WahuTween` HOẶC FluentTransitions, không lấy cả hai.
- V1 ưu tiên custom/WahuTween vì mục tiêu dependency nhỏ.

### C. `svg-net/SVG` — ƯU TIÊN DEV/BUILD-TIME, KHÔNG BẮT BUỘC Ở CHILD RUNTIME

Repo: https://github.com/svg-net/SVG
License: MS-PL.

Repo hiện có target gồm `net462`, `net472`, `net481` cùng các target mới hơn.

Ứng dụng ưu tiên:
- dùng SVG.NET ở **tool/build pipeline trên máy dev** để render asset SVG giáo dục tự dựng sang PNG/Bitmap đã kiểm chứng;
- child runtime đọc PNG trực tiếp;
- chỉ dùng SVG.NET runtime nếu sau benchmark chứng minh thật sự cần scale động nhiều mức;
- không dùng animated SVG trong core.

Điểm lợi: child runtime có thể không cần thêm DLL SVG nào, giảm startup/dependency/licensing surface.

Phù hợp cho:
- base-ten;
- number line;
- đồng hồ;
- hình học;
- pictograph;
- icon UI.

### D. `mono/SkiaSharp` — OPTIONAL ADVANCED RENDERER, KHÔNG CORE

Repo: https://github.com/mono/SkiaSharp

Repo hỗ trợ Windows Classic Desktop và .NET Framework 4.6.2+, nhưng đi kèm native Skia và làm deployment/phân phối phức tạp hơn GDI+.

Quyết định:
- không dùng ở V1 core;
- chỉ cân nhắc một `HD motion renderer` sau khi benchmark máy thật cho thấy dư CPU/RAM và có use-case không làm được tốt bằng GDI+.

### E. `quicoli/LottieSharp` — LOẠI KHỎI CORE

Repo: https://github.com/quicoli/LottieSharp
License: MIT.

LottieSharp là WPF-only, target .NET Framework 4.7 / .NET 8 và dùng SkiaSharp + Skottie.

Lý do không dùng core:
- app dự kiến WinForms/GDI+;
- kéo thêm renderer/native stack;
- lợi ích thẩm mỹ không đủ để đổi lấy rủi ro trên máy Win7 cũ.

Có thể dùng Lottie ở tool thiết kế trên PC mạnh để PRE-RENDER sprite/GIF, rồi app con chỉ phát asset nhẹ.

### F. `CommunityToolkit/Lottie-Windows` — LOẠI

Repo: https://github.com/CommunityToolkit/Lottie-Windows

Dùng WinUI/UWP và Windows Composition cho Windows 10/11. Không phù hợp mục tiêu Windows 7.

## Audio repo liên quan setup

### `naudio/NAudio`
Repo: https://github.com/naudio/NAudio
License: MIT.

- NAudio 3 hiện yêu cầu .NET 9 → KHÔNG dùng cho Win7.
- Nếu sau này cần recording/playback MP3 phức tạp, pin NAudio **2.x** vì nhánh này còn dành cho .NET Framework/.NET Standard legacy.
- V1 ưu tiên `System.Media.SoundPlayer` + WAV cho SFX đơn giản để zero-extra-dependency.

## Design-time Agent Skills nên học, KHÔNG bundle vào runtime

### `pbakaus/impeccable`
https://github.com/pbakaus/impeccable

Giá trị:
- motion design;
- interaction design;
- accessibility;
- anti-pattern;
- typography/spatial hierarchy;
- audit/polish/distill/animate.

Cách dùng:
- trích principle vào DESIGN.md / motion policy;
- không bê web CSS/API vào WinForms.

### `dawitlabs/ui-skills`
https://github.com/dawitlabs/ui-skills

Skill hữu ích:
- `/animate`;
- `/a11y`;
- `/ui-design-principles`;
- `/copy`;
- `/tokens`.

Cách dùng:
- học quy trình audit motion/accessibility/copy;
- bỏ phần Playwright/web-specific khỏi runtime desktop.

### `hueyexe/frontend-agent-skills`
https://github.com/hueyexe/frontend-agent-skills

Hữu ích cho:
- accessibility-inclusive-design;
- visual composition;
- interaction pattern.

### `hungdqdesign/ui-ux-pro-max-skill`
https://github.com/hungdqdesign/ui-ux-pro-max-skill

Hữu ích như database ý tưởng UI/UX, nhưng không lấy style trend làm mục tiêu. App trẻ lớp 2 cần readability + behavior coherence trước “57 style”.

## Stack motion đề xuất chính thức V1

```text
WinForms + GDI+
  ├─ WahuTween (custom, lấy ý tưởng từ WinFormAnimation)
  ├─ SpriteSheetPlayer (PNG, frame-index)
  ├─ GIF player (chỉ asset idle nhỏ)
  ├─ Pre-rendered PNG + procedural GDI+
  ├─ MotionPolicy / BehaviorController
  └─ SoundPlayer WAV

DEV/BUILD TOOL ONLY (nếu cần): SVG.NET để chuyển verified SVG → PNG.
```

Không dùng trong core:

```text
Electron
Chromium/WebView2 dependency
Lottie runtime
SkiaSharp runtime
Unity/Godot chỉ để làm UI
video background
WebGL
60fps decorative loops
```

## Kết luận

GitHub nên được dùng theo hai cách khác nhau:

1. **Runtime:** cực ít dependency, audit/pin/version rõ ràng.
2. **Design-time skill:** lấy kiến thức motion/a11y/UX để nâng chất lượng thiết kế nhưng không làm binary nặng thêm.

Đây là cấu hình hợp lý nhất với mục tiêu: đẹp + sống + tâm lý học tốt + vẫn chạy trên Win7 cũ.
