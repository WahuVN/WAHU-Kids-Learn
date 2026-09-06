# 20 — DECISIONS FROM RESEARCH

Cập nhật: 2026-09-06

## Quyết định đã chuyển thành setup V1

1. **Win7 SP1 chỉ là OS floor.** Target-ready cần thêm runtime/signature/smoke evidence; legacy SHA-2 readiness được theo dõi riêng.
2. **Không dùng exact KB absence làm hard-fail duy nhất.** KB4490628/KB4474419 là evidence lịch sử quan trọng, nhưng target verification phải dựa cả signature/runtime self-test.
3. **18/30 FPS là render cap.** `System.Windows.Forms.Timer` không phải clock chính xác 33 ms; dùng `Stopwatch` cho elapsed time, coalesce wake-up và bỏ frame trung gian khi UI bận.
4. **Win7 dùng system-DPI aware.** Manifest `dpiAware=true`, test 96/120 DPI; không dựa Per-Monitor V2/Win10-only behavior.
5. **Drag không thiết yếu phải có non-drag equivalent.** WAHU giữ target 64/56 px, lớn hơn accessibility reference floor.
6. **Custom WinForms controls phải có accessibility semantics.** Ưu tiên standard controls; custom control có AccessibleName/Role và keyboard/semantic equivalent khi phù hợp.
7. **WAL là một phần state.** Không raw hot-copy main DB khi active; ưu tiên SQLite Online Backup API hoặc clean closed snapshot.
8. **Recovery integrity có nhiều lớp.** `integrity_check` + `foreign_key_check` khi abnormal/recovery; startup thường dùng check nhẹ hơn.
9. **NAudio chỉ là optional.** Nếu cần, pin verified 2.x/2.2.1; không auto-upgrade sang NAudio 3 trên net48.
10. **Production signing là release gate.** Authenticode SHA-256 + RFC3161; Inno SignTool/signed uninstaller; private key không ở repo.
11. **Content import phải adversarial-test.** Zip-slip, decompression bomb, malformed/nested archive, normalized duplicate paths, executable/script payload.
12. **Data minimization/retention là design rule.** Không lưu dữ liệu “để sau”; mic recording mặc định ephemeral; diagnostics aggregate/local.
13. **Open-source projects chỉ dùng làm pattern reference.** Học activity organization từ GCompris/Sugar và offline content distribution từ Kolibri; không kéo runtime nặng vào child app.
14. **Learning design tiếp tục khóa theo evidence.** Spacing/retrieval, worked examples, concrete↔abstract, multiple representation/response, autonomy có giới hạn, progress reflection; không tối ưu raw time-in-app.
15. **Supersede SQLite 1.0.119.** NuGet đánh dấu 1.0.119 deprecated vì critical bugs; V1 chuyển sang `System.Data.SQLite 2.0.4 + SourceGear.sqlite3 3.53.4` x86. Provider đã chạy net48/x86, SQLite 3.53.4 và DB/migration/write-coordinator/backup/restore integration smoke 58/58 PASS. `e_sqlite3.dll` PE import audit không thấy VCRUNTIME/MSVCP/UCRT ngoài; clean Win7 vẫn là release gate.
16. **Offline signature trust tách khỏi digest validity.** Win7 offline có thể thiếu/stale trusted roots; test production certificate chain riêng, không tự import root CA hay bật mạng từ child installer.

## Trạng thái

Các quyết định trên đã được map vào `setup/18..20`, các config v2, setup specs cũ và checklist A–R. Implementation/toolchain/Win7 hardware evidence vẫn phải hoàn thành trước release.