# 13 — SOURCE NOTES: SETUP & DESIGN

Cập nhật: 2026-09-06

Nguồn web đang dùng trong vòng nghiên cứu này sẽ được chốt vào registry sau khi mapping rule/test hoàn tất.

## Nhóm nguồn đã xác nhận

### Microsoft / Windows 7 / .NET
- .NET Framework system requirements — Windows 7 SP1 supports .NET Framework 4.8; 32/64-bit.
- Install .NET Framework on Windows — Windows 7 latest supported Framework is 4.8.
- SHA-2 support requirement — KB4490628 + KB4474419 required for modern SHA-2-only Windows update chain.
- High DPI Win32 guidance — Win7 can use manifest `dpiAware=true` as system-DPI aware.
- WinForms high-DPI modern enhancements require newer Windows; do not design Win7 around those APIs.
- UI Automation / WinForms accessibility — standard controls expose providers; custom controls need accessibility support.

### SQLite
- WAL documentation — WAL has performance/concurrency benefits but `-wal` is part of persistent state and network filesystem is not supported for WAL sharing.
- Online Backup API — preferred for live DB snapshots.
- PRAGMA docs — `quick_check`, `integrity_check`, `foreign_key_check` have distinct cost/coverage.

### W3C / accessibility
- WCAG 2.2 target size minimum 24×24 CSS px.
- Dragging Movement: provide single-pointer non-drag alternative unless essential.
- Pause/Stop/Hide for auto motion.
- Animation from Interactions: allow disabling nonessential motion.
- Cognitive accessibility: familiar controls, predictable design, short/plain content, reduce distraction.

### CAST UDL 3.0
- choice/autonomy;
- challenge + support;
- multiple representations;
- multiple interaction methods;
- graduated support;
- transfer/generalization;
- learner agency/progress reflection.

### IES / WWC
- spacing;
- worked examples interleaved with problem solving;
- graphics + verbal;
- concrete + abstract connection;
- active retrieval;
- metacognitive judgement of what is known/needs study.

### EEF
- Metacognition/self-regulation evidence high; updated guidance 2025.
- Feedback: task/subject/self-regulation + actionable; feedback on individual identity is weaker; feedback should also acknowledge correct work.

### AAP 2026
- Child-centered digital design should support privacy, safety, learning and well-being.
- Ecosystems optimized for engagement/commercialization may encourage prolonged use and displace healthy activities.

### FTC / COPPA privacy principles
- collect only needed data;
- security/integrity;
- parent controls/access where applicable;
- retention only as long as needed;
- do not condition child participation on unnecessary data.

### OWASP
- Validate archive/file before extraction;
- size/compression/target path checks;
- password/PIN storage should use salted slow KDF rather than fast raw hash.
