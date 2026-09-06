# 12 — BUILD, TEST & RELEASE MATRIX

## 1. Build outputs V1

```text
WAHU-Kids-Learn-win7-x86.zip
WAHU-Kids-Learn-Setup-win7-x86.exe
content manifests
SHA256SUMS.txt
LICENSES/
RELEASE_NOTES.md
```

Optional later:
`win7-x64` chỉ sau benchmark.

## 2. Build configuration

Release:
- x86;
- optimize on;
- debug symbols tách riêng;
- deterministic compiler option nếu toolchain hỗ trợ;
- no dev endpoints;
- debug logging off;
- content validator pass mandatory.

## 3. Test matrix minimum

OS:
- Win7 SP1 x86;
- Win7 SP1 x64 chạy x86 app.

RAM:
- 2 GB;
- 4 GB.

Display:
- 1024×768 96 DPI;
- 1366×768;
- 120/125% scaling nếu máy có.

Storage:
- HDD chậm;
- near-low-disk scenario.

Audio:
- loa có;
- không audio device;
- mic có/không.

## 4. Setup tests

- clean install;
- missing .NET;
- .NET already installed;
- update same user;
- reinstall/repair;
- uninstall keep data;
- explicit data delete;
- portable launch;
- path contains Vietnamese/space;
- non-admin current user.

## 5. Runtime tests

- cold/warm launch;
- session 15–30 phút;
- sleep/wake;
- force kill;
- rapid clicking;
- alt-tab;
- audio replay spam;
- motion Normal/Reduced/Minimal;
- corrupted content;
- DB busy/corrupt simulation;
- backup/restore.

## 6. Learning tests

- strong learner;
- average;
- weak prerequisite;
- random guesser;
- slow accurate;
- frustration-prone;
- fatigue-like cross-skill slowdown.

Assert:
- no mastery inflate từ hinted success;
- no frustration loop;
- fatigue không tự giảm mastery;
- review due hoạt động;
- HOLD content không load.

## 7. Performance gates

Gate trên target thật:
- no input freeze;
- no queued animation backlog;
- no memory/GDI leak qua repeated sessions;
- NORMAL dùng **30 FPS render cap** và đạt trải nghiệm mượt theo p95/dropped-frame/input metrics; không claim timer 33 ms accuracy;
- LOW dùng 18 FPS render cap và vẫn usable;
- idle CPU thấp;
- lesson RAM trong budget;
- DPI 96/120 không clipping/overlap.

## 8. Release checklist

Không release nếu:
- content validator fail;
- migration test fail;
- uninstall xóa data mặc định;
- Parent PIN bypass;
- child sees stack trace;
- installer cần Internet;
- decorative motion chạy trong question trái policy;
- duplicate reward event;
- backup restore chưa pass.

## 9. Release manifest

Mỗi release ghi:
- app version;
- git commit;
- build timestamp UTC;
- compiler/tool versions;
- dependency versions;
- installer version;
- content versions;
- schema version;
- SHA-256 files;
- compatibility preflight policy version;
- production signing status/certificate identity (không private key);
- timestamp/signature verification result;
- target Win7 smoke evidence.

## 10. Signing/compatibility release gates

Production release phải kiểm:
- Win7 SP1 legacy SHA-2 readiness scenario;
- signed EXE/DLL verify;
- installer signature verify;
- signed uninstaller verify nếu signing enabled;
- tampered artifact fail hash/signature test;
- offline setup không cần network;
- exact KB absence không tự hard-fail nếu signature/runtime self-test và approved compatibility evidence pass.

Chi tiết: `18_WIN7_COMPATIBILITY_SIGNING.md`.