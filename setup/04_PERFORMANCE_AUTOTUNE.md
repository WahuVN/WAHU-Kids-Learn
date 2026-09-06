# 04 — PERFORMANCE BENCHMARK & AUTOTUNE

## 1. Triết lý

Performance là một phần UX/tâm lý: UI lag làm trẻ click lặp, mất cảm giác control và tăng rapid guessing giả.

Ưu tiên:

```text
INPUT > QUESTION STATE > AUDIO > INSTRUCTIONAL MOTION > SIGNAL > NAVIGATION > FEEDBACK > DECORATIVE
```

## 2. Chỉ có 2 profile V1

### LOW
- motion cap 18 FPS;
- decorative off;
- max animated region 1;
- particles off;
- shadows low/off;
- image cache nhỏ;
- sprite idle frame skip mạnh;
- pre-rendered PNG ưu tiên;
- audio preload nhỏ.

### NORMAL
- motion cap 30 FPS;
- decorative restrained ngoài question;
- max animated regions 2;
- milestone particles rất nhỏ nếu có;
- image cache vừa;
- idle sprite restrained.

Không có HIGH ở V1.

## 3. Benchmark local

Benchmark không cần Internet.

Test:
- draw 1024×768 background + 8–12 controls;
- text Vietnamese rendering;
- 1 sprite animation;
- one tween;
- PNG decode/cache;
- audio trigger latency;
- SQLite small transaction;
- UI input dispatch under motion.

Ghi:
- average frame ms;
- p95 frame ms;
- dropped frame ratio;
- working set MB;
- GC count;
- input callback delay;
- image decode ms;
- SQLite write ms.

## 4. Heuristic chọn profile

Đây là engineering heuristic, không phải định luật.

Chọn LOW nếu một trong các điều kiện bền vững xảy ra:
- RAM rất thấp;
- p95 frame time vượt budget 30 FPS rõ rệt;
- input callback bị delay đáng kể dưới motion;
- working set vượt budget;
- repeated GDI allocation pressure.

Nếu không → NORMAL.

Không downgrade chỉ vì một frame spike đơn.

## 5. Runtime degradation

RenderBudgetMonitor rolling window:

```text
NORMAL
→ drop DECORATIVE
→ reduce idle fps
→ pause offscreen animations
→ skip tween intermediate frames
→ reduce cache pressure
→ temporary LOW-motion behavior
```

Không được:
- delay answer registration;
- delay pause/exit;
- drop instructional endpoint;
- mất state vì frame skip.

## 6. Memory budget

Target ban đầu:
- idle <= 150 MB, ưu tiên <=100 MB;
- lesson <=250 MB;
- cache bounded.

32-bit process phù hợp budget này.

## 7. Bitmap discipline

- không giữ Bitmap decoded vô hạn;
- cache key: `asset_id|width|height|dpi|variant`;
- LRU + byte estimate;
- dispose khi evict;
- không clone image vô nghĩa;
- atlas chỉ preload theo scene;
- background chỉ một decoded copy nếu có thể.

## 8. Motion timing discipline

18/30 FPS là **render caps**, không phải timer interval guarantee.

Microsoft ghi `System.Windows.Forms.Timer` bị giới hạn khoảng 55 ms accuracy, nên cấm thiết kế `Interval=33ms => 30 FPS`.

Rule:
- một logical `MotionScheduler`;
- elapsed-time source = `System.Diagnostics.Stopwatch`;
- wake-up source chỉ đánh thức scheduler, không là clock nguồn sự thật;
- coalesce wake-up, tối đa 1 UI update/invalidate pending;
- UI bận → skip intermediate frame, không queue backlog;
- background callback phải marshal về UI thread;
- animation cancellable;
- invisible => unregister;
- final state deterministic;
- no Timer per control.

Custom animated controls ưu tiên double buffering. Soak test theo dõi GDI object growth và dispose `Bitmap/Image/Graphics` deterministic.

## 9. Startup

Startup path:
- không scan toàn bộ content file nếu manifest cache hợp lệ;
- không decode asset chưa dùng;
- DB open/migration nhỏ;
- show shell khi core ready;
- heavy diagnostics deferred sau shell nếu an toàn.

## 10. Runtime telemetry local

Giữ aggregate:
- session max working set;
- p95 render time;
- motion degradation count;
- crash count;
- DB recovery count.

Không có remote telemetry.