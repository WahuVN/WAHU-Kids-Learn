# 16 — RESEARCH → SETUP MAPPING

Cập nhật: 2026-09-06

Mục tiêu: mọi phát hiện nghiên cứu có giá trị phải đi vào đúng setup/config/test gate, tránh để kiến thức nằm riêng trong research.

| Phát hiện | Setup đích | Implementation/Test |
|---|---|---|
| Win7 SHA-2 readiness | `00`, `02`, `03`, installer | preflight KB/readiness classification |
| System-DPI aware Win7 | `08`, app manifest | 96/120 DPI visual regression |
| WinForms Timer ~55ms | `04`, motion policy | Stopwatch pacing + frame coalescing test |
| Double buffering | `04`, Child UI | flicker/render stress test |
| GDI Dispose | `04`, diagnostics | GDI/working-set leak soak |
| SQLite WAL state | `05` | no raw hot-copy; WAL/DELETE test |
| Online Backup API | `05` | backup while active + restore verify |
| quick/integrity/fk checks | `05` | normal vs abnormal startup tests |
| WCAG target/dragging | `08` | target audit; drag alternate path |
| Reduced motion | `08`, motion config | Normal/Reduced/Minimal test |
| Cognitive predictability | `08`, Child UX | consistent navigation/content language audit |
| UI Automation semantics | `08` | Accessibility Insights/manual UIA audit |
| UDL learner variability | `11`, Learning Engine | multiple representation/response coverage |
| EEF feedback principles | Learning/Child UX | feedback taxonomy test |
| IES spacing/retrieval | Learning Engine | scheduler/simulation tests |
| AAP child-centered design | safety/reward/session | no time-in-app optimization gate |
| FTC minimization/retention | `09` | data inventory/retention tests |
| OWASP archive validation | `06`, `09` | zip-slip/bomb/malformed archive tests |
| NAudio 2.x pin only | `01`, `07` | dependency lock + audio smoke |
| Authenticode SHA256/RFC3161 | `02`, `12` | sign/verify installer + uninstaller |
| GCompris/Sugar activity patterns | Child UX/Game | activity naming/discoverability review |
| Kolibri offline channels | `06` | USB pack import/export workflow |
