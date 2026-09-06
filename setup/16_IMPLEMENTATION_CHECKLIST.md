# 16 — SETUP IMPLEMENTATION CHECKLIST

Cập nhật: 2026-09-06

File này là checklist từ spec → code. Không được đánh dấu DONE chỉ vì file tồn tại; phải có test tương ứng.

## A. Solution/bootstrap

- [x] Tạo `WAHUKidsLearn.sln`; full `/restore + rebuild` PASS.
- [x] App project target `.NET Framework 4.8`, x86.
- [x] Release build explicit x86; solution chỉ map Debug/Release x86.
- [ ] App manifest/DPI behavior test trên Win7.
- [x] Global runtime issue/recovery boundary đưa lỗi config/content/database/platform về UI an toàn; Child Mode không hiện stack trace.
- [x] Single-instance named mutex implemented; smoke test primary/secondary/reacquire PASS.

## B. Runtime config

- [x] Loader đọc shipped runtime config; build/portable/installer E2E xác nhận 10 file config bắt buộc (gồm `update_policy_v1.json`) được stage và load.
- [x] Validate schema/version + invariant an toàn lúc startup; malformed/schema tamper bị reject.
- [x] Safety-critical config fail-closed; `network=true` installed E2E trả exit 42 / `CONFIG_INVALID` trước khi sửa DB.
- [ ] Runtime override tách khỏi shipped defaults.
- [ ] Config migration versioned.

## C. Paths/storage

- [x] Installed/per-user app + learner DB paths khớp `paths_v1.json`; installer E2E xác nhận.
- [x] Portable mode qua `portable.mode`/`--portable`; ZIP E2E tạo `./UserData` và không tạo/sửa installed `%LOCALAPPDATA%` data.
- [ ] Tạo directory atomically/safe.
- [ ] Cache/temp cleanup không chạm DB/backups.
- [ ] Unicode path test.

## D. SQLite

- [x] Pin `System.Data.SQLite 2.0.4` + `SourceGear.sqlite3 3.53.4` x86.
- [x] Native `e_sqlite3.dll` PE32/x86 được stage bởi PackageReference.
- [x] Apply `001_initial.sql` + migration `002_attempt_immutability.sql`; app bootstrap hiện khóa schema V2.
- [x] Migration history SHA-256 ghi/verify; schema tamper smoke bị reject.
- [x] Foreign keys ON mỗi connection.
- [x] Busy timeout 2500 ms.
- [x] Single write coordinator; 12 concurrent writers smoke cho `maxActive=1`.
- [x] `AnswerCommitService` commit attempt + error/mastery + child_skill + review trong một transaction; late constraint failure rollback toàn bộ.
- [x] Reward source-key idempotency test: duplicate bị SQLite reject.
- [x] Migration V2 tạo trigger chặn `UPDATE attempt`; correction chỉ append qua `attempt_correction_event`, smoke PASS.
- [ ] WAL/DELETE journal target benchmark.

## E. Backup/recovery

- [x] SQLite Online Backup API implementation + verified snapshot.
- [x] SHA-256 backup result được sinh/test.
- [x] Managed backup rotation test qua 8 tuần giữ đúng 5 recent + 4 weekly; malformed/unknown file không bị auto-delete.
- [x] Existing V1 DB fail-closed nếu thiếu backup context; có context thì verified schema-V1 pre-migration backup trước khi lên V2.
- [x] Recovery temp restore + verify + atomic replace; original target được preserve.
- [x] Runtime crash marker + full integrity/FK check; stale-marker E2E recovery PASS.
- [x] Restore preserve original target DB (`.pre_restore.*.db`) trước atomic replace; recovery smoke/E2E PASS.
- [ ] Manual export/import from Parent Mode.

## F. Content runtime

- [ ] Pack manifest/schema parser.
- [ ] `VERIFIED` hard gate.
- [ ] Version-specific immutable directories.
- [ ] `pack_id + pack_version + question_id` attempt reference.
- [ ] Content checksum validation.
- [ ] Missing asset fallback.
- [ ] HOLD/invalid pack never reaches Child Mode.

## G. Secure content import

- [ ] Compressed size limit.
- [ ] Uncompressed size limit.
- [ ] File count limit.
- [ ] Reject absolute/`..` paths.
- [ ] Reject executable/script entries.
- [ ] Reject duplicate normalized paths.
- [ ] Temp extraction + validation before atomic move.
- [ ] Parent explicit approval.

## H. Asset pipeline

- [ ] Verified SVG build-time render to PNG.
- [ ] Static instructional asset hash.
- [ ] Procedural GDI+ tests for dynamic geometry/clock/number line.
- [ ] Sprite atlas size gate.
- [ ] Image LRU bounded by profile.
- [ ] Explicit Bitmap/Image disposal.
- [ ] No child-runtime Lottie/Skia/WebView.

## I. Motion runtime

- [ ] `MotionScheduler` single timer/clock.
- [ ] `WahuTween` easing/cancel/final state.
- [ ] `SpriteSheetPlayer`.
- [ ] `RenderBudgetMonitor`.
- [ ] Respect motion class allowlist.
- [ ] Decorative OFF during question/read/listen/think.
- [ ] Normal/Reduced/Minimal.
- [ ] LOW 18 / NORMAL 30 engineering caps.
- [ ] Offscreen animation unregister.
- [ ] Input not blocked by motion.

## J. Behavior controller

- [ ] Rolling personal baseline.
- [ ] Six operational states.
- [ ] Confidence/evidence output.
- [ ] State hysteresis/cooldown.
- [ ] Speed alone never guesses/boredom.
- [ ] Fatigue requires cross-skill/global evidence.
- [ ] Fatigue protects mastery without knowledge-error evidence.
- [ ] Two target failures → repair policy test.
- [ ] Behavior decision audit stored.

## K. Audio/mic

- [ ] WAV playback baseline.
- [ ] Voice > SFX > music priority.
- [ ] Missing device safe mode.
- [ ] Replay consistent.
- [ ] Optional recording/playback.
- [ ] Recording temp retention cleanup.
- [ ] No pronunciation fake score.
- [ ] NAudio 2.x only if benchmarked need exists.

## L. Child UI

- [ ] 1024×768 no primary-flow scroll.
- [ ] 96 DPI + 125% test.
- [ ] Primary target ~64px baseline.
- [ ] No hover/double/right-click dependency.
- [ ] Double-submit guard.
- [ ] Correct/incorrect not color-only.
- [ ] No technical modal/stack trace.
- [ ] Break/closure non-punitive.

## M. Parent Mode

- [x] Local Parent PIN set/unlock/change + persistent lockout implemented; reset policy vẫn cần UX riêng nếu quên PIN.
- [x] Salted PBKDF2-HMAC-SHA256 record + random salt + atomic file; target Win7 iteration benchmark vẫn là hardware gate.
- [x] Parent dashboard đọc aggregate Vững / Đang học / Cần ôn từ Data service.
- [ ] Motion/audio/session controls.
- [x] Parent manual verified backup + verified recovery UI implemented.
- [ ] Diagnostics export sanitized.
- [ ] Content pack import.
- [ ] Delete-profile explicit confirmation.

## N. Performance/autotune

- [x] Hardware profile/preflight collection local-only implemented.
- [ ] Render/input/audio/SQLite benchmark.
- [x] LOW/NORMAL cold-start selection implemented/tested.
- [x] Runtime degradation order implemented/tested; one spike không tự downgrade.
- [ ] Working-set sampling aggregate only.
- [x] LOW/NORMAL image/audio cache ceilings lấy từ verified config.
- [ ] 15–30 minute leak test.

## O. Installer

- [x] Inno Setup 7.1.0 cài trên build machine.
- [x] Compile `WAHU_Kids_Learn.iss` cleanly bằng ISCC 7.1.0.
- [x] `.NET 4.8 Release >= 528040` check trong installer/preflight.
- [ ] Optional official offline redistributable path tested.
- [x] Per-user non-admin install; E2E exit 0.
- [x] App-only close filter `WAHUKidsLearn.exe`.
- [x] Reinstall/update preserves learner DB bit-for-bit trong E2E.
- [x] Uninstall xóa app nhưng giữ learner DB + sentinel trong E2E.
- [x] Startup-with-Windows task mặc định bật; installer E2E xác nhận HKCU Run được tạo và uninstall xóa sạch.
- [x] GitHub updater helper được stage trong installer; manifest/staging/tamper smoke PASS; cross-version local apply và full live GitHub-feed apply `0.1.23→0.1.24` PASS; startup ON/OFF đều được preserve.
- [ ] Repair mode.
- [ ] Vietnamese full wizard translation only after vendor/version/license review.

## P. Release/reproducibility

- [x] Dev release manifest populated với build/runtime/DB gates.
- [x] Git commit được ghi vào build manifest; publish script còn bắt buộc clean tree + HEAD == origin/main.
- [ ] Dependency hashes có trong manifest nhưng license inventory vẫn cần hoàn thiện.
- [ ] Content versions/hashes cần manifest đầy đủ hơn.
- [x] Installer SHA-256 được sinh cạnh installer.
- [x] Portable ZIP SHA-256 được sinh; ZIP E2E chạy trực tiếp từ archive extract PASS.
- [x] Build sinh `update-manifest.json`; publish script khóa repo public, clean tree, origin/main, fixed `update-dev/update-stable` feed và từ chối stable unsigned.
- [ ] Workstation setup/update matrix PASS; còn Win7 target + signing matrix nên chưa được đánh dấu all PASS.
- [ ] Win7 target cần test updater HTTPS/TLS thật; workstation live GitHub download/stage 6/6 + full network download/apply E2E đã PASS.

## Q. Target-PC final gates

- [ ] Win7 SP1 x86 physical/VM.
- [ ] Win7 SP1 x64 physical/VM running x86 app.
- [ ] 2 GB RAM scenario.
- [ ] Old HDD.
- [ ] 1024×768.
- [ ] Audio absent/present.
- [ ] Sleep/wake.
- [ ] Process kill during session.
- [ ] DB recovery.
- [ ] Backup restore.
- [ ] Installer update/uninstall.

## R. Research hardening 2026-09

### Windows compatibility/signing
- [x] Legacy Win7 SHA-2 readiness probe có state/evidence; exact KB absence chỉ WARN, không sole hard-fail.
- [ ] Test image thiếu KB4490628/KB4474419 và image đã update.
- [ ] Production signed-binary self-test.
- [ ] Authenticode SHA-256 + RFC3161 timestamp pipeline.
- [ ] Inno Setup/uninstaller signature verification.
- [ ] Private key/password không nằm repo/log/script plaintext.
- [ ] Production certificate chain verify trên Win7 disconnected image.
- [x] Preflight tách `signature_digest_valid` và `publisher_chain_trusted`; unsigned production gate expected-exit 12 PASS.
- [x] Child installer không có logic import arbitrary root CA/network remediation.
- [ ] Signature/trust check không gây network-timeout stall dài trên offline target.
- [x] Binary-audit `e_sqlite3.dll` x86 3.53.4: import table không phụ thuộc VCRUNTIME/MSVCP/UCRT ngoài; [ ] clean Win7 native-load smoke vẫn là target gate.

### Motion/render
- [ ] FPS là render cap, không `WinForms.Timer.Interval=33ms` assumption.
- [ ] `Stopwatch` elapsed-time source.
- [ ] Wake-up coalescing + max one UI update pending.
- [ ] Background callback marshals về UI thread.
- [ ] Frame backlog stress test.
- [ ] Double buffering cho animated custom controls.
- [ ] GDI object leak soak.

### DPI/accessibility
- [ ] Manifest `dpiAware=true` system-DPI on Win7.
- [ ] 96/120 DPI visual regression.
- [ ] Nonessential drag có non-drag alternative.
- [ ] `AccessibleName/Role` audit custom controls.
- [ ] Parent/diagnostic High Contrast/Classic smoke.

### SQLite durability
- [x] Online Backup API implementation + service integration smoke.
- [ ] Raw hot-copy main DB forbidden test ở API boundary.
- [x] WAL sidecar tồn tại khi connection mở đã được smoke-test; backup dùng Online Backup thay raw copy.
- [ ] WAL network/shared FS runtime guard chưa implement.
- [x] `integrity_check` + `foreign_key_check` health/recovery gate implemented/tested.
- [ ] Portable USB journal-mode/removal test trên thiết bị thật; portable desktop ZIP isolation đã PASS.

### Audio/privacy/import
- [ ] NAudio nếu dùng pin 2.2.1/verified 2.x; NAudio 3 dependency guard.
- [ ] SoundPlayer/WAV preload async/bounded.
- [ ] Data field purpose/retention inventory.
- [ ] Zip-slip/bomb/malformed/nested archive adversarial tests.

## Definition: Setup V1 implemented

Chỉ DONE khi các nhóm A–R quan trọng đã có implementation + automated/manual evidence; tài liệu một mình không tính là DONE.